#!/usr/bin/env python3
"""Registered stable-Docker host adapter. Never run inside Web/API containers.

No paths, Compose project, bind addresses, Docker flags or service identities
come from the browser/API. This executable is invoked only by the host executor.
"""
from __future__ import annotations

import argparse
import fcntl
import hashlib
import json
import os
from pathlib import Path
import re
import socket
import stat
import subprocess
import tempfile
import time
import urllib.request

SCHEMA = "scadivo-docker-host-network-v1"
ENV_PORTS = {
    "webPort": ("ELITESCADA_WEB_PORT", 18080),
    "apiPort": ("ELITESCADA_API_PORT", 15120),
    "databasePort": ("ELITESCADA_DB_PORT", 15432),
    "mediaControlPort": ("SCADIVO_MEDIA_CONTROL_PORT", 9997),
    "mediaPlaybackPort": ("SCADIVO_MEDIA_PLAYBACK_PORT", 8888),
}
ENV_DEFAULTS = {"SCADIVO_WEB_BIND": "127.0.0.1", "SCADIVO_API_BIND": "127.0.0.1"}
FIELDS = frozenset(ENV_PORTS)


class AdapterError(Exception):
    pass


def secure_file(path: Path, *, secret=False) -> None:
    if not path.is_absolute() or path.is_symlink() or not path.is_file():
        raise AdapterError("Registered Docker file is missing or linked")
    st = path.stat()
    if st.st_uid not in (0, os.geteuid()) or st.st_mode & (0o077 if secret else 0o022):
        raise AdapterError("Registered Docker file permissions or ownership are unsafe")


def secure_dir(path: Path) -> None:
    if not path.is_absolute() or path.is_symlink() or not path.is_dir():
        raise AdapterError("Registered installation directory unavailable")
    st = path.stat()
    if st.st_uid not in (0, os.geteuid()) or st.st_mode & 0o022:
        raise AdapterError("Registered installation directory writable by untrusted user")


def parse_env(path: Path) -> dict:
    secure_file(path, secret=True)
    output = {}
    lines = path.read_text(encoding="utf-8-sig").splitlines()
    wanted = {v[0] for v in ENV_PORTS.values()} | set(ENV_DEFAULTS) | {"SCADIVO_INSTALLATION_ID"}
    for line in lines:
        match = re.match(r"^\s*([A-Za-z_][A-Za-z0-9_]*)=(.*)$", line)
        if not match or match[1] not in wanted:
            continue
        if match[1] in output:
            raise AdapterError("Duplicate registered Docker environment key")
        output[match[1]] = match[2].strip().strip("\"'")
    return output


def port(value: object) -> int:
    if type(value) is not int or not 1 <= value <= 65535:
        raise AdapterError("Invalid registered Docker port")
    return value


def current_ports(env: dict) -> dict:
    result = {}
    for name, (key, default) in ENV_PORTS.items():
        raw = env.get(key, str(default))
        if not re.fullmatch(r"[0-9]{1,5}", raw):
            raise AdapterError("Invalid registered Docker port value")
        result[name] = port(int(raw))
    if len({result[x] for x in ("webPort", "apiPort", "databasePort")}) != 3 or result["mediaControlPort"] == result["mediaPlaybackPort"]:
        raise AdapterError("Current registered Docker listeners conflict")
    return result


def _atomic(path: Path, data: bytes, *, mode=0o600) -> None:
    descriptor, tmp = tempfile.mkstemp(prefix=".scadivo-network-", dir=path.parent)
    try:
        os.fchmod(descriptor, mode)
        with os.fdopen(descriptor, "wb") as output:
            output.write(data)
            output.flush()
            os.fsync(output.fileno())
        os.replace(tmp, path)
        folder = os.open(path.parent, os.O_DIRECTORY | os.O_RDONLY)
        try:
            os.fsync(folder)
        finally:
            os.close(folder)
    finally:
        if os.path.exists(tmp):
            os.unlink(tmp)


class DockerHostAdapter:
    def __init__(self, registration: Path, *, runner=None, healthy=None, check_bind=None):
        secure_file(registration, secret=True)
        profile = json.loads(registration.read_text(encoding="utf-8"))
        required = {"schema", "instanceId", "installationDirectory", "composeSha256", "dockerExecutable"}
        if set(profile) not in (required, required | {"composeOverrideSha256"}) or profile["schema"] != SCHEMA:
            raise AdapterError("Invalid registered Docker host profile")
        if not isinstance(profile["instanceId"], str) or not re.fullmatch(r"[a-z0-9][a-z0-9-]{0,47}", profile["instanceId"]):
            raise AdapterError("Invalid registered Compose project")
        self.instance_id = profile["instanceId"]
        self.root = Path(profile["installationDirectory"])
        secure_dir(self.root)
        self.compose = self.root / "docker-compose.stable.yml"
        self.env_path = self.root / ".env"
        secure_file(self.compose)
        secure_file(self.env_path, secret=True)
        if not isinstance(profile["composeSha256"], str) or not re.fullmatch(r"[0-9a-f]{64}", profile["composeSha256"]):
            raise AdapterError("Missing pinned Compose digest")
        self.compose_sha256 = profile["composeSha256"]
        self.override = None
        self.override_sha256 = None
        if "composeOverrideSha256" in profile:
            override_digest = profile["composeOverrideSha256"]
            if not isinstance(override_digest, str) or not re.fullmatch(r"[0-9a-f]{64}", override_digest):
                raise AdapterError("Invalid pinned host executor Compose override digest")
            self.override = self.root / "docker/stable/compose-host-executor.override.yml"
            self.override_sha256 = override_digest
            secure_file(self.override)
        exe = Path(profile["dockerExecutable"])
        secure_file(exe)
        if not os.access(exe, os.X_OK):
            raise AdapterError("Registered Docker executable not available")
        self.docker = str(exe)
        self.runner = runner or self._run
        self.healthy = healthy or self._health
        self.check_bind = check_bind or self._check_bind
        self.state_dir = registration.parent
        secure_dir(self.state_dir)
        self.journal = self.state_dir / ("docker-network-" + self.instance_id + ".journal.json")
        self.lock_path = self.root / ".scadivo-host-network.lock"
        self._integrity()

    @staticmethod
    def _run(cmd: list[str]):
        try:
            return subprocess.run(cmd, capture_output=True, check=True, text=True, timeout=150).stdout
        except (subprocess.CalledProcessError, FileNotFoundError, subprocess.TimeoutExpired) as error:
            raise AdapterError("Registered Docker host command failed; inspect protected diagnostics") from error

    @staticmethod
    def _check_bind(address: str, value: int) -> None:
        with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as sock:
            try:
                sock.bind((address, value))
            except OSError as error:
                raise AdapterError("Requested published host port is occupied") from error

    def _integrity(self) -> dict:
        secure_file(self.compose)
        secure_file(self.env_path, secret=True)
        if hashlib.sha256(self.compose.read_bytes()).hexdigest() != self.compose_sha256:
            raise AdapterError("Registered Compose file differs from administrator-approved digest")
        if self.override is not None:
            secure_file(self.override)
            if hashlib.sha256(self.override.read_bytes()).hexdigest() != self.override_sha256:
                raise AdapterError("Registered host executor Compose override differs from approved digest")
        env = parse_env(self.env_path)
        if env.get("SCADIVO_INSTALLATION_ID", "elitescada-stable") != self.instance_id:
            raise AdapterError("Registered installation ID and effective Compose project differ")
        for key in ENV_DEFAULTS:
            if env.get(key, ENV_DEFAULTS[key]) not in ("127.0.0.1", "0.0.0.0"):
                raise AdapterError("Registered Docker bind address is unsupported")
        return env

    def current(self) -> dict:
        return current_ports(self._integrity())

    def _docker(self, *args: str) -> str:
        return self.runner([self.docker, *args])

    def _compose(self, *args: str, env_file: Path | None = None) -> str:
        arguments = ["compose", "--project-name", self.instance_id, "-f", str(self.compose)]
        if self.override is not None:
            arguments.extend(["-f", str(self.override)])
        if env_file is not None:
            arguments.extend(["--env-file", str(env_file)])
        return self._docker(*arguments, *args)

    def _owners(self) -> None:
        # Prevent a registered project name from controlling containers belonging
        # to a different Compose directory. Do not adopt or re-key data volumes.
        ids = self._compose("ps", "--all", "--quiet").split()
        for container in ids:
            if not re.fullmatch(r"[a-f0-9]{12,64}", container):
                raise AdapterError("Unexpected Compose container identifier")
            raw = self._docker("inspect", "--format", "{{json .Config.Labels}}", container)
            labels = json.loads(raw)
            if (labels or {}).get("com.docker.compose.project") != self.instance_id:
                raise AdapterError("Compose project identity mismatch")
            owner = (labels or {}).get("com.docker.compose.project.working_dir", "")
            if not owner or Path(owner).resolve() != self.root.resolve():
                raise AdapterError("Compose project belongs to another installation directory")

    def _docker_conflicts(self, changes: list[tuple[str, int]]) -> None:
        ids = self._docker("ps", "--quiet").split()
        if not ids:
            return
        if any(not re.fullmatch(r"[a-f0-9]{12,64}", i) for i in ids):
            raise AdapterError("Unrecognized Docker container identifier")
        containers = json.loads(self._docker("inspect", *ids))
        for address, number in changes:
            for container in containers:
                for binds in (container.get("NetworkSettings", {}).get("Ports") or {}).values():
                    for bound in binds or []:
                        if int(bound.get("HostPort") or 0) != number:
                            continue
                        host = bound.get("HostIp") or ""
                        if address == "0.0.0.0" or host in ("", "::", "0.0.0.0", address):
                            raise AdapterError("Requested port already published by Docker")

    def _candidate(self, ports: dict) -> bytes:
        existing = self.env_path.read_text(encoding="utf-8-sig").splitlines()
        values = {ENV_PORTS[key][0]: str(port(value)) for key, value in ports.items()}
        for key, value in values.items():
            matches = [i for i, line in enumerate(existing) if re.match(r"^\s*" + re.escape(key) + r"=", line)]
            if len(matches) > 1:
                raise AdapterError("Duplicate Docker environment entry")
            if matches:
                existing[matches[0]] = key + "=" + value
            else:
                existing.append(key + "=" + value)
        return ("\n".join(existing) + "\n").encode("utf-8")

    def _plan(self, desired: dict) -> tuple[dict, bytes]:
        if set(desired) != FIELDS:
            raise AdapterError("Only the five registered Docker port values may change")
        env = self._integrity()
        old = current_ports(env)
        new = {key: port(val) for key, val in desired.items()}
        if len({new[k] for k in ("webPort", "apiPort", "databasePort")}) != 3 or new["mediaControlPort"] == new["mediaPlaybackPort"]:
            raise AdapterError("Selected Docker ports conflict")
        self._owners()
        changes = []
        for name, address in (("webPort", env.get("SCADIVO_WEB_BIND", "127.0.0.1")),
                              ("apiPort", env.get("SCADIVO_API_BIND", "127.0.0.1")),
                              ("databasePort", "127.0.0.1")):
            if old[name] != new[name]:
                self.check_bind(address, new[name])
                changes.append((address, new[name]))
        self._docker_conflicts(changes)
        result = self._candidate(new)
        with tempfile.NamedTemporaryFile(prefix=".scadivo-network-preflight-", dir=self.root, delete=False) as tmp:
            temp_path = Path(tmp.name)
            os.chmod(temp_path, 0o600)
            tmp.write(result)
        try:
            self._compose("config", "--quiet", env_file=temp_path)
            services = set(self._compose("config", "--services", env_file=temp_path).split())
            if services != {"web", "api", "database", "media-gateway"}:
                raise AdapterError("Compose service scope differs from approved SCADIVO profile")
        finally:
            temp_path.unlink(missing_ok=True)
        return old, result

    def _load_journal(self) -> dict | None:
        if not self.journal.exists():
            return None
        secure_file(self.journal, secret=True)
        record = json.loads(self.journal.read_text())
        if record.get("schema") != "scadivo-docker-host-recovery-v1" or record.get("instanceId") != self.instance_id:
            raise AdapterError("Registered Docker journal cannot be verified")
        backup = Path(record.get("backup", ""))
        if backup.parent != self.state_dir or not re.fullmatch(r"docker-env-[0-9a-f]{32}\.backup", backup.name):
            raise AdapterError("Docker recovery backup is outside registered state")
        secure_file(backup, secret=True)
        if hashlib.sha256(backup.read_bytes()).hexdigest() != record.get("sha256"):
            raise AdapterError("Docker recovery backup digest mismatch")
        return record

    def _write_journal(self, body: dict) -> None:
        _atomic(self.journal, (json.dumps(body, separators=(",", ":")) + "\n").encode())

    def preflight(self, desired: dict) -> None:
        pending = self._load_journal()
        if pending and pending["phase"] == "pending":
            raise AdapterError("Previous Docker operation requires recovery")
        self._plan(desired)

    def apply(self, desired: dict) -> None:
        old, candidate = self._plan(desired)
        previous = self._load_journal()
        if previous and previous.get("phase") == "pending":
            raise AdapterError("Previous Docker operation requires recovery")
        import secrets
        backup = self.state_dir / ("docker-env-" + secrets.token_hex(16) + ".backup")
        _atomic(backup, self.env_path.read_bytes())
        record = {"schema": "scadivo-docker-host-recovery-v1", "instanceId": self.instance_id,
                  "phase": "pending", "backup": str(backup), "sha256": hashlib.sha256(backup.read_bytes()).hexdigest(),
                  "before": old, "desired": desired}
        self._write_journal(record)
        try:
            _atomic(self.env_path, candidate)
            self._compose("up", "-d")
            self.healthy(desired)
            if self.current() != desired:
                raise AdapterError("Registered Docker ports changed unexpectedly")
            record["phase"] = "applied"
            self._write_journal(record)
        except Exception:
            try:
                self._restore(record)
            except Exception:
                # Leave the durable journal and secret backup for manual recovery.
                raise AdapterError("Docker application and automatic recovery incomplete") from None
            raise AdapterError("Docker change failed; previous profile was restored and verified") from None

    def _health(self, desired: dict) -> None:
        deadline = time.monotonic() + 120
        while time.monotonic() < deadline:
            try:
                for name, suffix in (("webPort", "/healthz"), ("apiPort", "/health")):
                    with urllib.request.build_opener(urllib.request.ProxyHandler({})).open(
                        "http://127.0.0.1:" + str(desired[name]) + suffix, timeout=3) as response:
                        if response.status != 200:
                            raise AdapterError("Docker HTTP readiness failed")
                self._compose("exec", "-T", "database", "pg_isready", "-U", "elitescada", "-d", "elitescada")
                self._compose("exec", "-T", "api", "python3", "-c",
                              "import socket,urllib.request,sys; urllib.request.urlopen('http://media-gateway:'+sys.argv[1]+'/v3/paths/list',timeout=3).read(); socket.create_connection(('media-gateway',int(sys.argv[2])),timeout=3).close()",
                              str(desired["mediaControlPort"]), str(desired["mediaPlaybackPort"]))
                return
            except (AdapterError, OSError, ValueError):
                time.sleep(3)
        raise AdapterError("Registered Docker health checks timed out")

    def _restore(self, record: dict) -> None:
        backup = Path(record["backup"])
        secure_file(backup, secret=True)
        if hashlib.sha256(backup.read_bytes()).hexdigest() != record["sha256"]:
            raise AdapterError("Docker recovery backup integrity mismatch")
        _atomic(self.env_path, backup.read_bytes())
        self._owners()
        self._compose("up", "-d")
        self.healthy(record["before"])
        if self.current() != record["before"]:
            raise AdapterError("Docker recovered ports differ from the registered prior profile")
        record["phase"] = "recovered"
        self._write_journal(record)

    def verify(self) -> None:
        # Idempotent contract for a failed apply before any mutation:
        # real Compose identity check + active host/DB/media health.
        observed = self.current()
        self.preflight(observed)
        self.healthy(observed)

    def recover(self) -> None:
        self._integrity()
        record = self._load_journal()
        if not record or record["phase"] not in ("pending", "applied", "recovered"):
            raise AdapterError("No interrupted Docker operation can be recovered")
        if record["phase"] == "recovered":
            if self.current() != record["before"]:
                raise AdapterError("Docker recovery state has diverged")
            return
        self._restore(record)


def main() -> None:
    parser = argparse.ArgumentParser(description="Registered Docker host network adapter")
    parser.add_argument("--registration", required=True)
    parser.add_argument("--mode", required=True, choices=["preflight", "apply", "recover", "verify"])
    for key in ENV_PORTS:
        parser.add_argument("--" + re.sub(r"([A-Z])", r"-\1", key).lower(), type=int)
    args = parser.parse_args()
    adapter = DockerHostAdapter(Path(args.registration))
    # Locks all adapter processes for one registered Compose installation.
    with open(adapter.lock_path, "a+b") as lock:
        os.fchmod(lock.fileno(), 0o600)
        fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
        if args.mode == "recover":
            adapter.recover()
        elif args.mode == "verify":
            adapter.verify()
        else:
            desired = {key: getattr(args, re.sub(r"([A-Z])", r"_\1", key).lower()) for key in ENV_PORTS}
            if args.mode == "preflight":
                adapter.preflight(desired)
            else:
                adapter.apply(desired)


if __name__ == "__main__":
    main()
