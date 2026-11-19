#!/usr/bin/env python3
"""Read-only operational checks and direct SMTP notifications for the Portal."""

import argparse
import datetime as dt
from email.message import EmailMessage
import json
import os
from pathlib import Path
import smtplib
import ssl
import subprocess
import syslog
from tempfile import NamedTemporaryFile
from urllib.error import HTTPError, URLError
from urllib.request import Request, urlopen


COMPONENTS = ("API", "Banco", "E-mails", "Renovação")
SQL = """
SELECT json_build_object(
  'email_failed', (SELECT count(*) FROM "EmailOutbox" WHERE "Status" = 'failed'
    AND "Attempts" >= 5 AND "CreatedAtUtc" >= now() - interval '24 hours'),
  'email_retry', (SELECT count(*) FROM "EmailOutbox" WHERE "Status" = 'retry'
    AND "Attempts" >= 2 AND "LastErrorCode" LIKE 'smtp-%'),
  'email_stale', (SELECT count(*) FROM "EmailOutbox" WHERE "Status" IN ('queued', 'retry')
    AND "CreatedAtUtc" < now() - interval '15 minutes'
    AND ("NextAttemptAtUtc" IS NULL OR "NextAttemptAtUtc" <= now() - interval '5 minutes')),
  'renewal_overdue', (SELECT count(*) FROM "ProductLicenseActivations" a
    JOIN "ProductLicenses" l ON l."Id" = a."LicenseId" WHERE a."Status" = 'active'
    AND l."Status" = 'active' AND (l."EntitlementExpiresAtUtc" IS NULL OR l."EntitlementExpiresAtUtc" > now())
    AND a."LastLeaseIssuedAtUtc" BETWEEN now() - interval '96 hours' AND now() - interval '54 hours')
);
"""

SIMULATION = {
    "API": "Simulação: a API pública deixou de responder.",
    "Banco": "Simulação: a consulta PostgreSQL falhou.",
    "E-mails": "Simulação: fila atrasada e envio com erro.",
    "Renovação": "Simulação: a rota de renovação está indisponível.",
}


def http_json(url, payload=None):
    request = Request(url, headers={"Accept": "application/json", "Cache-Control": "no-cache"})
    if payload is not None:
        request.data = json.dumps(payload).encode()
        request.add_header("Content-Type", "application/json")
    try:
        try:
            response = urlopen(request, timeout=12)
        except HTTPError as error:
            response = error
        with response:
            if response.geturl() != url:
                return None, {}
            status = response.code
            try:
                data = json.loads(response.read(16384))
            except (ValueError, UnicodeError):
                return status, {}
            return status, data if isinstance(data, dict) else {}
    except (URLError, OSError, TimeoutError):
        return None, {}


def database_snapshot():
    try:
        query = subprocess.run(
            ["docker", "compose", "-f", "compose.yml", "exec", "-T", "-e",
             "PGOPTIONS=-c default_transaction_read_only=on -c statement_timeout=4000",
             "database", "psql", "-X", "-U", "scadivo_portal", "-d", "scadivo_portal",
             "-At", "-v", "ON_ERROR_STOP=1", "-c", SQL],
            cwd="/opt/scadivo/portal", capture_output=True, text=True, timeout=12, check=True)
        data = json.loads(query.stdout)
        if all(type(data.get(key)) is int and data[key] >= 0
               for key in ("email_failed", "email_retry", "email_stale", "renewal_overdue")):
            return data
    except (OSError, ValueError, AttributeError, subprocess.SubprocessError):
        pass
    return None


def check(config, smtp_available):
    url = config["portal_url"].rstrip("/")
    code, session = http_json(url + "/api/session")
    api_ok = code == 200 and session.get("authenticated") is False
    database = database_snapshot()
    issues = {
        "API": "" if api_ok else "A sessão anônima da API pública não respondeu como esperado.",
        "Banco": "" if database is not None else "O PostgreSQL não respondeu à consulta de monitoramento.",
        "E-mails": None,
        "Renovação": None,
    }

    email = []
    if not smtp_available:
        email.append("A conexão ou autenticação SMTP do monitor falhou.")
    if api_ok and session.get("emailDeliveryConfigured") is not True:
        email.append("O Portal informa que o envio está desabilitado.")
    if database is not None:
        if database["email_failed"] or database["email_retry"] or database["email_stale"]:
            email.append("Falhas recentes: {email_failed}; novas tentativas SMTP: {email_retry}; "
                         "itens atrasados: {email_stale}.".format(**database))
    if email or (api_ok and database is not None):
        issues["E-mails"] = " ".join(email)

    if api_ok:
        renewal_code, result = http_json(
            url + "/api/licensing/device/activations/00000000-0000-0000-0000-000000000001/renew",
            {"requestId": "00000000-0000-0000-0000-000000000002", "nextRefreshCredential": "A" * 43})
        if renewal_code != 401 or result.get("error") != "invalid-credentials":
            issues["Renovação"] = "A rota ou o assinador de renovação não respondeu como esperado."
        elif database is not None:
            count = database["renewal_overdue"]
            issues["Renovação"] = (f"{count} ativação(ões) vigente(s) sem renovar há 54–96 h. "
                                    "O equipamento pode estar desligado.") if count else ""
    return issues


def record(state, issues, now):
    active, pending = state["active"], state["pending"]
    for name in COMPONENTS:
        problem = issues[name]
        if problem is None or bool(problem) == active.get(name, False):
            continue
        active[name] = bool(problem)
        pending[:] = [event for event in pending
                      if event["name"] != name or event["failed"] != bool(problem)]
        pending.append({"name": name, "failed": bool(problem), "time": now, "detail": problem})


def save_state(path, state):
    staged = NamedTemporaryFile(mode="w", encoding="utf-8", dir=path.parent, delete=False)
    temp = Path(staged.name)
    try:
        with staged:
            json.dump(state, staged, ensure_ascii=False)
            staged.flush()
            os.fsync(staged.fileno())
        os.replace(temp, path)
    finally:
        temp.unlink(missing_ok=True)


def open_smtp(config):
    client = smtplib.SMTP_SSL(config["smtp_host"], config["smtp_port"], timeout=12,
                              context=ssl.create_default_context())
    try:
        password = Path(config["smtp_password_file"]).read_text(encoding="utf-8").rstrip("\r\n")
        client.login(config["smtp_user"], password)
        if client.noop()[0] != 250:
            raise smtplib.SMTPException("SMTP probe rejected")
        return client
    except (smtplib.SMTPException, OSError, ValueError):
        client.close()
        raise


def send(client, config, events, test=False):
    message = EmailMessage()
    message["From"] = config["smtp_user"]
    message["To"] = config["recipient"]
    title = "SCADIVO Portal — alerta operacional"
    message["Subject"] = "[TESTE] " + title if test else title
    lines = ["Teste controlado. Nenhum serviço foi interrompido." if test else title, ""]
    for event in events:
        label = "Falha" if event["failed"] else "Sinal normalizado"
        lines += [f'{event["time"]} · {event["name"]} · {label}', event["detail"], ""]
    lines.append("Consulta executada no VPS SCADIVO.")
    message.set_content("\n".join(lines))
    client.send_message(message)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--config", default="/etc/scadivo/portal-monitor.json", type=Path)
    parser.add_argument("--state", default="/var/lib/scadivo-portal-monitor/state.json", type=Path)
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument("--check", action="store_true")
    mode.add_argument("--test-email", action="store_true")
    args = parser.parse_args()
    os.umask(0o077)
    syslog.openlog("scadivo-portal-monitor")
    config = json.loads(args.config.read_text(encoding="utf-8"))
    smtp = None
    now = dt.datetime.now(dt.timezone.utc).strftime("%Y-%m-%d %H:%M UTC")
    try:
        try:
            smtp = open_smtp(config)
        except (smtplib.SMTPException, OSError, ValueError):
            pass

        if args.test_email:
            if smtp is None:
                raise RuntimeError("test SMTP unavailable")
            state = {"active": {}, "pending": []}
            record(state, SIMULATION, now)
            send(smtp, config, state["pending"], test=True)
            print("SMTP aceitou o e-mail de teste dos quatro monitores.")
            return 0

        issues = check(config, smtp is not None)
        if args.check:
            print(json.dumps(issues, ensure_ascii=False))
            return int(any(issue != "" for issue in issues.values()))

        state = (json.loads(args.state.read_text(encoding="utf-8")) if args.state.exists()
                 else {"active": {}, "pending": []})
        record(state, issues, now)
        save_state(args.state, state)
        if state["pending"]:
            if smtp is None:
                raise RuntimeError("notifications pending")
            send(smtp, config, state["pending"])
            state["pending"].clear()
            save_state(args.state, state)
        return 0
    finally:
        if smtp is not None:
            smtp.close()


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (smtplib.SMTPException, RuntimeError, OSError, ValueError, KeyError, TypeError):
        syslog.syslog(syslog.LOG_ERR, "Monitor failed or SMTP unavailable; any queued events remain on disk.")
        print("Falha no monitor ou no transporte SMTP. Consulte o journal do VPS.")
        raise SystemExit(1)
