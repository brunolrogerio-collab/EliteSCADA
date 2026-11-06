#!/usr/bin/env python3
"""SCADIVO installation-scoped host executor (Linux Unix-domain socket).

No request can supply an executable, command line, path, instance identity or
service name. Only the host administrator provisions those values. The Web/API
process never receives Docker or systemd privileges.
"""
from __future__ import annotations

import argparse
import fcntl
import hashlib
import hmac
import http.server
import json
import os
from pathlib import Path
import re
import secrets
import socket
import socketserver
import stat
import subprocess
import threading
import time
from urllib.parse import urlsplit

SCHEMA = "scadivo-network-host-executor-v1"
PLAN_SCHEMA = "scadivo-network-port-plan-v1"
PORT_FIELDS = {
    "access": ("webPort", "apiPort"),
    "database": ("databasePort",),
    "media": ("mediaControlPort", "mediaPlaybackPort"),
}
PORT_DEFAULTS = {
    "access": {"webPort": 18080, "apiPort": 15120},
    "database": {"databasePort": 15432},
    "media": {"mediaControlPort": 9997, "mediaPlaybackPort": 8888},
}


class ContractError(Exception):
    pass


def protected_file(path: Path) -> None:
    if not path.is_absolute() or path.is_symlink() or not path.is_file():
        raise ContractError("Protected registration file is missing or is a link")
    status = path.stat()
    if status.st_uid not in (0, os.geteuid()) or status.st_mode & 0o027:
        raise ContractError("Registration must be owner controlled and not readable by others")


def secure_directory(path: Path) -> None:
    if not path.is_absolute() or path.is_symlink() or not path.is_dir():
        raise ContractError("Provisioned state directory is missing")
    status = path.stat()
    if status.st_uid not in (0, os.geteuid()) or status.st_mode & 0o022:
        raise ContractError("State directory must be owned by the executor and not writable by others")


def port_number(value: object) -> int:
    if type(value) is not int or not 1 <= value <= 65535:
        raise ContractError("Port must be an integer from 1 to 65535")
    return value


def validate_ports(module: str, values: object) -> dict:
    if module not in PORT_FIELDS or not isinstance(values, dict):
        raise ContractError("Unsupported installation operation")
    fields = PORT_FIELDS[module]
    if set(values) != set(fields):
        raise ContractError("Only registered service port fields are permitted")
    ports = {key: port_number(values[key]) for key in fields}
    if len(set(ports.values())) != len(ports):
        raise ContractError("Selected service ports must be distinct")
    return ports


def local_urls(config: dict) -> dict:
    endpoints = config["Kestrel"]["Endpoints"]
    result = {}
    for key, target in (("Web", "webPort"), ("Api", "apiPort")):
        url = urlsplit(endpoints[key]["Url"])
        if url.scheme not in ("http", "https") or not url.hostname or url.username or url.password or url.query or url.fragment or url.path not in ("", "/"):
            raise ContractError("Invalid registered Kestrel listener")
        result[target] = port_number(url.port)
    return result


class Executor:
    def __init__(self, registration_path: Path, *, runner=None, now=None):
        protected_file(registration_path)
        config = json.loads(registration_path.read_text(encoding="utf-8"))
        if config.get("schema") != SCHEMA or not re.fullmatch(r"[a-z0-9][a-z0-9-]{0,47}", config.get("instanceId", "")):
            raise ContractError("Invalid executor registration")
        if config.get("profile") != "native-linux":
            raise ContractError("No validated host executor for this profile")
        self.instance_id = config["instanceId"]
        self.profile = config["profile"]
        self.socket_path = Path(config["socketPath"])
        self.state_dir = Path(config["stateDirectory"])
        self.profile_path = Path(config["nativeProfilePath"])
        self.token_path = Path(config["tokenFile"])
        secure_directory(self.state_dir)
        secure_directory(self.socket_path.parent)
        protected_file(self.profile_path)
        protected_file(self.token_path)
        token = self.token_path.read_bytes().strip()
        if len(token) < 32 or len(token) > 256:
            raise ContractError("Installation executor token is not provisioned")
        self.token = token
        self.socket_mode = config.get("socketMode", "0600")
        if self.socket_mode not in ("0600", "0660"):
            raise ContractError("Invalid Unix socket permissions")
        self.socket_group = config.get("socketGroup")
        if self.socket_mode == "0660" and (type(self.socket_group) is not int or self.socket_group < 0):
            raise ContractError("Group-restricted socket requires a registered group ID")
        if self.socket_mode == "0600" and self.socket_group is not None:
            raise ContractError("Group requires group-restricted socket")
        self.runner = runner or self._run
        self.now = now or time.time
        self.guard = threading.Lock()
        self.state_file = self.state_dir / "network-operation.json"
        self.plan_file = self.state_dir / "network-plan.json"
        self._lock_fd = os.open(self.state_dir / "network-executor.lock", os.O_CREAT | os.O_RDWR, 0o600)
        try:
            fcntl.flock(self._lock_fd, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except BlockingIOError:
            raise ContractError("Another executor already owns this installation") from None
        self._registered_native()
        self._repair_interrupted()

    def _registered_native(self) -> dict:
        registered = json.loads(self.profile_path.read_text(encoding="utf-8"))
        if registered.get("schema") != "scadivo-native-network-v1" or registered.get("platform") != "linux" or not re.fullmatch(r"[A-Za-z0-9_.-]+", registered.get("serviceName", "")):
            raise ContractError("Native installation is not registered")
        if registered.get("endpointName") != "Web" or registered.get("apiEndpointName") != "Api":
            raise ContractError("Independent native Web/API listeners must be registered")
        path = Path(registered["configurationFile"])
        if not path.is_absolute() or path.is_symlink() or not path.is_file():
            raise ContractError("Native effective configuration is missing")
        status = path.stat()
        if status.st_uid not in (0, os.geteuid()) or status.st_mode & 0o022:
            raise ContractError("Effective configuration is not protected by host ACL")
        return registered

    def _config_path(self) -> Path:
        return Path(self._registered_native()["configurationFile"])

    def _current(self) -> dict:
        profile = self._registered_native()
        app = json.loads(self._config_path().read_text(encoding="utf-8-sig"))
        current = local_urls(app)
        database = profile.get("database")
        if isinstance(database, dict):
            port = app.get("DatabaseTopology", {}).get("LocalManagedPort")
            if port is None:
                # The registered default belongs to the installer, not the UI.
                port = database.get("currentPort")
            if type(port) is int and 1 <= port <= 65535:
                current["databasePort"] = port
        media = profile.get("media")
        if isinstance(media, dict):
            try:
                control = urlsplit(app["MediaSources"]["GatewayControlUrl"])
                playback = urlsplit(app["MediaSources"]["GatewayPlaybackUrl"])
                if control.hostname in ("127.0.0.1", "localhost", "::1") and playback.hostname in ("127.0.0.1", "localhost", "::1"):
                    current["mediaControlPort"] = port_number(control.port)
                    current["mediaPlaybackPort"] = port_number(playback.port)
            except (KeyError, ValueError, TypeError, ContractError):
                pass
        return current

    def _revision(self) -> str:
        profile = self._registered_native()
        files = [self._config_path()]
        for field in ("database", "media"):
            entry = profile.get(field)
            if isinstance(entry, dict) and entry.get("configurationFile"):
                files.append(Path(entry["configurationFile"]))
        digest = hmac.new(self.token, digestmod=hashlib.sha256)
        for item in files:
            if not item.is_absolute() or item.is_symlink():
                raise ContractError("Registered service configuration is invalid")
            digest.update(item.read_bytes())
        return digest.hexdigest()

    def _load(self, path: Path) -> dict | None:
        if not path.exists():
            return None
        if path.is_symlink() or path.stat().st_mode & 0o077:
            raise ContractError("Operation record has unsafe permissions")
        return json.loads(path.read_text(encoding="utf-8"))

    def _save(self, path: Path, body: dict) -> None:
        temporary = path.with_name(path.name + ".tmp")
        raw = (json.dumps(body, separators=(",", ":")) + "\n").encode()
        descriptor = os.open(temporary, os.O_CREAT | os.O_EXCL | os.O_WRONLY, 0o600)
        try:
            with os.fdopen(descriptor, "wb") as output:
                output.write(raw)
                output.flush()
                os.fsync(output.fileno())
            os.replace(temporary, path)
            folder = os.open(self.state_dir, os.O_RDONLY | os.O_DIRECTORY)
            try:
                os.fsync(folder)
            finally:
                os.close(folder)
        finally:
            if temporary.exists():
                temporary.unlink()

    def _repair_interrupted(self) -> None:
        record = self._load(self.state_file)
        if record and record.get("state") == "applying":
            record["state"] = "recovery_required"
            record["detail"] = "Executor was interrupted; perform local recovery"
            self._save(self.state_file, record)

    def snapshot(self) -> dict:
        with self.guard:
            try:
                current = self._current()
            except (OSError, KeyError, ValueError, ContractError):
                return {"schema": SCHEMA, "available": False, "reason": "Registered host configuration cannot be verified"}
            record = self._load(self.state_file)
            profile = self._registered_native()
            modules = ["access"]
            if isinstance(profile.get("database"), dict) and "databasePort" in current:
                modules.append("database")
            if isinstance(profile.get("media"), dict) and "mediaControlPort" in current and "mediaPlaybackPort" in current:
                modules.append("media")
            return {
                "schema": SCHEMA, "available": True, "profile": self.profile,
                "instanceId": self.instance_id, "configuredPorts": current,
                "modules": modules, "operation": record,
                "observed": "host-configuration", "hostMappingVerified": False
            }

    def _command(self, module: str, ports: dict, mode: str) -> list[str]:
        script_dir = Path(__file__).resolve().parent
        selected = {
            "access": ("apply-native-network-profile.py", [("--web-port", "webPort"), ("--api-port", "apiPort")]),
            "database": ("native_database_port.py", [("--database-port", "databasePort")]),
            "media": ("native_media_ports.py", [("--control-port", "mediaControlPort"), ("--hls-port", "mediaPlaybackPort")]),
        }[module]
        script = script_dir / selected[0]
        if script.is_symlink() or not script.is_file() or script.stat().st_mode & 0o022 or script.stat().st_uid not in (0, os.geteuid()):
            raise ContractError("Administrator-owned execution adapter is unavailable")
        args = ["python3", str(script), "--profile", str(self.profile_path)]
        if mode != "recover":
            for flag, key in selected[1]:
                args.extend((flag, str(ports[key])))
        if mode == "apply":
            args.append("--apply")
        if mode == "recover":
            args.append("--recover")
        return args

    @staticmethod
    def _run(args: list[str]) -> None:
        result = subprocess.run(args, capture_output=True, timeout=300, check=False)
        if result.returncode != 0:
            raise ContractError("Registered host adapter rejected the operation; inspect protected host diagnostics")

    def preflight(self, body: object) -> dict:
        if not isinstance(body, dict) or set(body) != {"schema", "module", "ports"} or body["schema"] != PLAN_SCHEMA:
            raise ContractError("Unsupported request contract")
        module = body["module"]
        ports = validate_ports(module, body["ports"])
        with self.guard:
            record = self._load(self.state_file)
            if record and record.get("state") in ("applying", "recovery_required"):
                raise ContractError("Previous host operation must finish or be recovered")
            current = self._current()
            if any(field not in current for field in ports):
                raise ContractError("Service is not registered as locally managed")
            proposed = {**current, **ports}
            if len(set(proposed.values())) != len(proposed):
                raise ContractError("Requested ports conflict with another registered local service")
            self._command(module, ports, "preflight")
            self.runner(self._command(module, ports, "preflight"))
            plan = {
                "id": secrets.token_hex(16), "module": module, "ports": ports,
                "revision": self._revision(), "expiresAt": int(self.now()) + 600,
                "before": {key: current[key] for key in ports}
            }
            self._save(self.plan_file, plan)
            return {"planId": plan["id"], "module": module, "ports": ports, "before": plan["before"],
                    "expiresAt": plan["expiresAt"], "requiresRestart": ports != plan["before"],
                    "verified": "host-preflight-only"}

    def apply(self, body: object) -> dict:
        if not isinstance(body, dict) or set(body) != {"schema", "planId", "confirm"} or body["schema"] != PLAN_SCHEMA or body["confirm"] is not True:
            raise ContractError("Explicit confirmation and a prepared plan are required")
        plan_id = body["planId"]
        if not isinstance(plan_id, str) or not re.fullmatch(r"[0-9a-f]{32}", plan_id):
            raise ContractError("Invalid plan ID")
        with self.guard:
            plan = self._load(self.plan_file)
            record = self._load(self.state_file)
            if record and record.get("state") in ("applying", "recovery_required"):
                raise ContractError("Host operation is already running or requires recovery")
            if not plan or not hmac.compare_digest(plan["id"], plan_id) or plan["expiresAt"] <= int(self.now()) or plan["revision"] != self._revision():
                raise ContractError("Plan is expired or configuration changed; preflight again")
            if self._current() is None:
                raise ContractError("Unable to observe registered service")
            operation = {
                "id": secrets.token_hex(16), "module": plan["module"], "state": "applying",
                "before": plan["before"], "desired": plan["ports"], "startedAt": int(self.now()),
                "detail": "Host operation accepted; restart and verification pending"
            }
            self._save(self.state_file, operation)
            self.plan_file.unlink(missing_ok=True)
            threading.Thread(target=self._worker, args=(operation,), daemon=True).start()
            return {"operationId": operation["id"], "state": operation["state"]}

    def _worker(self, operation: dict) -> None:
        try:
            self.runner(self._command(operation["module"], operation["desired"], "apply"))
            actual = self._current()
            if any(actual.get(key) != value for key, value in operation["desired"].items()):
                raise ContractError("Effective registered service ports do not match the requested profile")
            operation["state"] = "applied"
            operation["detail"] = "Registered host adapter completed its health check and configured ports were re-read"
            operation["finishedAt"] = int(self.now())
        except Exception:
            # The native adapter owns journal/rollback; if the process was killed,
            # or rollback failed, do not declare the previous state healthy.
            operation["state"] = "recovery_required"
            operation["detail"] = "Operation failed or could not be verified; run the registered recovery action"
            operation["finishedAt"] = int(self.now())
        finally:
            with self.guard:
                self._save(self.state_file, operation)

    def recover(self, body: object) -> dict:
        if not isinstance(body, dict) or set(body) != {"schema", "confirm"} or body["schema"] != PLAN_SCHEMA or body["confirm"] is not True:
            raise ContractError("Recovery requires explicit confirmation")
        with self.guard:
            record = self._load(self.state_file)
            if not record or record.get("state") != "recovery_required":
                raise ContractError("No pending recovery exists")
            record["state"] = "applying"
            record["detail"] = "Restoring registered prior state"
            self._save(self.state_file, record)
            threading.Thread(target=self._recover_worker, args=(record,), daemon=True).start()
            return {"operationId": record["id"], "state": "applying"}

    def _recover_worker(self, record: dict) -> None:
        try:
            self.runner(self._command(record["module"], {}, "recover"))
            actual = self._current()
            if any(actual.get(k) != v for k, v in record["before"].items()):
                raise ContractError("Recovered configuration differs from original")
            record["state"] = "recovered"
            record["detail"] = "Previous registered configuration restored and checked"
        except Exception:
            record["state"] = "recovery_required"
            record["detail"] = "Local recovery incomplete; protected journal retained"
        finally:
            record["finishedAt"] = int(self.now())
            with self.guard:
                self._save(self.state_file, record)


class Server(socketserver.ThreadingMixIn, socketserver.UnixStreamServer):
    daemon_threads = True
    allow_reuse_address = False

    def __init__(self, executor: Executor):
        self.executor = executor
        path = executor.socket_path
        if path.exists() or path.is_symlink():
            raise ContractError("Socket path exists; inspect the previous executor before starting")
        super().__init__(str(path), Handler)
        try:
            if executor.socket_group is not None:
                os.chown(path, -1, executor.socket_group)
            os.chmod(path, int(executor.socket_mode, 8))
        except OSError:
            self.server_close()
            path.unlink(missing_ok=True)
            raise

    def server_close(self):
        super().server_close()
        self.executor.socket_path.unlink(missing_ok=True)


class Handler(http.server.BaseHTTPRequestHandler):
    protocol_version = "HTTP/1.1"

    def log_message(self, *_):
        pass  # Never log IPC headers, request bodies, credentials or file paths.

    def _respond(self, status: int, body: dict):
        encoded = json.dumps(body, separators=(",", ":")).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json")
        self.send_header("Cache-Control", "no-store")
        self.send_header("Content-Length", str(len(encoded)))
        self.end_headers()
        self.wfile.write(encoded)

    def _handle(self):
        executor = self.server.executor
        received = self.headers.get("X-Scadivo-Installation-Token", "").encode()
        if not hmac.compare_digest(received, executor.token):
            self._respond(403, {"error": "Executor authorization denied"})
            return
        try:
            if self.command == "GET":
                if self.path != "/v1/status":
                    raise ContractError("Unsupported operation")
                value = executor.snapshot()
                self._respond(200, value)
                return
            size = int(self.headers.get("Content-Length", "-1"))
            if self.command != "POST" or size < 2 or size > 4096:
                raise ContractError("Invalid request size or method")
            request = json.loads(self.rfile.read(size))
            actions = {
                "/v1/preflight": executor.preflight,
                "/v1/apply": executor.apply,
                "/v1/recover": executor.recover,
            }
            if self.path not in actions:
                raise ContractError("Unsupported operation")
            self._respond(200 if self.path == "/v1/preflight" else 202, actions[self.path](request))
        except (ContractError, ValueError, KeyError, TypeError, OSError) as error:
            self._respond(409, {"error": str(error) if isinstance(error, ContractError) else "Registered host operation unavailable"})
        except Exception:
            self._respond(503, {"error": "Host executor rejected operation; inspect protected diagnostics"})

    def do_GET(self):
        self._handle()

    def do_POST(self):
        self._handle()


def main():
    if os.name != "posix" or not hasattr(socket, "AF_UNIX"):
        raise SystemExit("This executor is Linux-only. Windows requires its registered ACL-protected named-pipe service.")
    parser = argparse.ArgumentParser(description="Installation-scoped Linux host executor")
    parser.add_argument("--registration", required=True)
    args = parser.parse_args()
    executor = Executor(Path(args.registration).resolve())
    with Server(executor) as server:
        server.serve_forever(poll_interval=0.5)


if __name__ == "__main__":
    main()
