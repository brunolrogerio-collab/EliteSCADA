#!/usr/bin/env python3
"""Host-only Docker installation executor using the Linux Unix IPC contract.

The API container is allowed to connect to this dedicated socket, never to
Docker, systemd, the host shell or another installation's Compose project.
"""
from __future__ import annotations

import argparse
import fcntl
import hashlib
import hmac
import http.client
import importlib.util
import json
import os
from pathlib import Path
import re
import secrets
import socket
import sys
import threading
import time

HERE = Path(__file__).resolve().parent


def load(name: str, filename: str):
    path = HERE / filename
    if path.is_symlink() or not path.is_file() or path.stat().st_mode & 0o022:
        raise RuntimeError("Registered host executor implementation is unprotected")
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(result)
    return result


native = load("scadivo_linux_native_executor_contract", "network-host-executor.py")
docker = load("scadivo_docker_host_adapter_contract", "docker-host-adapter.py")


class DockerExecutor(native.Executor):
    def __init__(self, registration_path: Path, *, runner=None, now=None):
        native.protected_file(registration_path)
        config = json.loads(registration_path.read_text(encoding="utf-8"))
        if config.get("schema") != native.SCHEMA or config.get("profile") != "stable-docker-linux":
            raise native.ContractError("Registered Docker executor profile is invalid")
        if set(config) != {"schema", "profile", "instanceId", "socketPath", "stateDirectory",
                           "dockerProfilePath", "tokenFile", "socketMode", "socketGroup"}:
            raise native.ContractError("Unexpected Docker executor host authority")
        if not re.fullmatch(r"[a-z0-9][a-z0-9-]{0,47}", config.get("instanceId", "")):
            raise native.ContractError("Invalid Docker installation identifier")
        self.instance_id = config["instanceId"]
        self.profile = config["profile"]
        self.socket_path = Path(config["socketPath"])
        self.state_dir = Path(config["stateDirectory"])
        self.profile_path = Path(config["dockerProfilePath"])
        self.token_path = Path(config["tokenFile"])
        native.secure_directory(self.state_dir)
        native.secure_directory(self.socket_path.parent)
        native.protected_file(self.profile_path)
        native.protected_file(self.token_path)
        if self.socket_path.exists() or self.socket_path.is_symlink():
            raise native.ContractError("Executor socket already exists; investigate the registered service")
        token = self.token_path.read_bytes().strip()
        if not 32 <= len(token) <= 256:
            raise native.ContractError("Docker executor token is missing")
        self.token = token
        self.socket_mode = config["socketMode"]
        self.socket_group = config["socketGroup"]
        if (self.socket_mode not in ("0600", "0660") or
            (self.socket_mode == "0660" and (type(self.socket_group) is not int or self.socket_group < 0)) or
            (self.socket_mode == "0600" and self.socket_group is not None)):
            raise native.ContractError("Invalid Docker IPC permissions")
        self.runner = runner or self._run
        self.now = now or time.time
        self.guard = threading.Lock()
        self.state_file = self.state_dir / "network-operation.json"
        self.plan_file = self.state_dir / "network-plan.json"
        self.audit_file = self.state_dir / "network-audit.jsonl"
        self._lock_fd = os.open(self.state_dir / "network-executor.lock", os.O_CREAT | os.O_RDWR, 0o600)
        try:
            fcntl.flock(self._lock_fd, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except BlockingIOError:
            raise native.ContractError("Another Docker executor owns this installation") from None
        self._adapter()
        self._repair_interrupted()

    def _adapter(self):
        selected = docker.DockerHostAdapter(self.profile_path)
        if selected.instance_id != self.instance_id:
            raise native.ContractError("Docker registration belongs to another installation")
        return selected

    def _registered_native(self):
        self._adapter()
        return {"docker": True}

    def _current(self):
        return self._adapter().current()

    def _revision(self):
        selected = self._adapter()
        digest = hmac.new(self.token, digestmod=hashlib.sha256)
        files = [self.profile_path, selected.env_path, selected.compose]
        if selected.override is not None:
            files.append(selected.override)
        for item in files:
            digest.update(item.read_bytes())
        return digest.hexdigest()

    def snapshot(self):
        status = super().snapshot()
        if status.get("available"):
            status["modules"] = ["access", "database", "media"]
        return status

    def _recover_worker(self, record: dict):
        try:
            adapter = self._adapter()
            journal = adapter._load_journal()
            observed = self._current()
            already_before = all(observed.get(key) == value for key, value in record["before"].items())
            if (journal and journal["phase"] == "pending") or not already_before:
                self.runner(self._command(record["module"], {}, "recover"))
            else:
                # An adapter may fail before mutation and have no recovery journal.
                # Require both a host preflight and real Docker health at old ports.
                self.runner(self._command(record["module"], {}, "verify"))
            actual = self._current()
            if any(actual.get(k) != v for k, v in record["before"].items()):
                raise native.ContractError("Recovered Docker ports differ from the saved profile")
            record["state"] = "recovered"
            record["detail"] = "Registered Docker host profile recovered and health checked"
        except Exception:
            record["state"] = "recovery_required"
            record["detail"] = "Registered Docker host recovery incomplete; protected journal retained"
        finally:
            record["finishedAt"] = int(self.now())
            with self.guard:
                self._save(self.state_file, record)
                self._audit("recovered" if record["state"] == "recovered" else "recovery-required",
                            record["module"], record["id"])

    def _command(self, module: str, ports: dict, mode: str):
        # Ports from the API are constrained to one typed module; combine with
        # the other effective host values, never accept executable or path.
        args = [sys.executable, str(HERE / "docker-host-adapter.py"),
                "--registration", str(self.profile_path), "--mode", mode]
        if mode not in ("recover", "verify"):
            target = {**self._current(), **ports}
            for key, flag in (
                ("webPort", "--web-port"), ("apiPort", "--api-port"),
                ("databasePort", "--database-port"),
                ("mediaControlPort", "--media-control-port"),
                ("mediaPlaybackPort", "--media-playback-port")
            ):
                args.extend((flag, str(target[key])))
        return args


def admin_rpc(registration: Path, route: str, body=None):
    native.protected_file(registration)
    config = json.loads(registration.read_text())
    if config.get("profile") != "stable-docker-linux" or config.get("schema") != native.SCHEMA:
        raise native.ContractError("No registered Docker host executor")
    token_file = Path(config["tokenFile"])
    native.protected_file(token_file)
    socket_path = Path(config["socketPath"])
    if not socket_path.is_absolute():
        raise native.ContractError("Invalid registered Docker socket")
    conn = http.client.HTTPConnection("localhost", timeout=20)
    conn.sock = socket.socket(socket.AF_UNIX, socket.SOCK_STREAM)
    try:
        conn.sock.connect(str(socket_path))
        conn.request("POST" if body is not None else "GET", route,
                     body=json.dumps(body) if body is not None else None,
                     headers={"X-Scadivo-Installation-Token": token_file.read_text().strip(),
                              "Content-Type": "application/json", "Connection": "close"})
        response = conn.getresponse()
        if response.length is not None and response.length > 65536:
            raise native.ContractError("Docker executor response too large")
        message = json.loads(response.read(65536))
        if response.status >= 300:
            raise native.ContractError(message.get("error", "Docker executor rejected operation"))
        return message
    finally:
        conn.close()


def main():
    if not sys.platform.startswith("linux"):
        raise SystemExit("Docker Unix-socket host executor is supported on Linux hosts only")
    parser = argparse.ArgumentParser()
    parser.add_argument("--registration", required=True)
    parser.add_argument("--status", action="store_true")
    parser.add_argument("--recover", action="store_true")
    parser.add_argument("--confirm-recovery", action="store_true")
    args = parser.parse_args()
    registration = Path(args.registration)
    if args.recover:
        if not args.confirm_recovery or args.status:
            raise SystemExit("Recovery requires --recover --confirm-recovery alone")
        print(json.dumps(admin_rpc(registration, "/v1/recover", {"schema": native.PLAN_SCHEMA, "confirm": True})))
    elif args.status:
        if args.confirm_recovery:
            raise SystemExit("Recovery confirmation requires --recover")
        print(json.dumps(admin_rpc(registration, "/v1/status")))
    elif args.confirm_recovery:
        raise SystemExit("Recovery confirmation requires --recover")
    else:
        executor = DockerExecutor(registration)
        with native.Server(executor) as server:
            server.serve_forever(poll_interval=.5)


if __name__ == "__main__":
    main()
