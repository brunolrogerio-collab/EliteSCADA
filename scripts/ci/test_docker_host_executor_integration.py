#!/usr/bin/env python3
"""CI-only, real Docker host-executor proof over registered Unix IPC.

Invoked while two independently named SCADIVO Compose installations are up.
Never mounts the Docker socket inside Web/API, and never reuses production IDs.
"""
import argparse
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import secrets
import shutil
import socket
import subprocess
import sys
import tempfile
import time
import urllib.request


def choose_port(excluding):
    for _ in range(100):
        with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as probe:
            probe.bind(("127.0.0.1", 0))
            port = probe.getsockname()[1]
        if port not in excluding:
            excluding.add(port)
            return port
    raise AssertionError("No isolated host port could be allocated")


def secret_file(path, body):
    path.write_text(body, encoding="utf-8")
    path.chmod(0o600)


def request(adapter, registration, route, body=None):
    return adapter.admin_rpc(registration, route, body)


def await_state(adapter, registration, expected, timeout=250):
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        status = request(adapter, registration, "/v1/status")
        operation = status.get("operation")
        if operation and operation["state"] == expected:
            return operation
        if operation and operation["state"] == "recovery_required" and expected != "recovery_required":
            raise AssertionError("Executor reported required recovery rather than " + expected)
        time.sleep(1)
    raise AssertionError("Executor did not finish registered Docker operation: " + expected)


def assert_host_mapping(installation, instance, expected):
    compose = installation / "docker-compose.stable.yml"
    docker = shutil.which("docker")
    for service, internal, port in (
        ("web", "80/tcp", expected["webPort"]),
        ("api", "5080/tcp", expected["apiPort"]),
        ("database", "5432/tcp", expected["databasePort"]),
    ):
        ids = subprocess.check_output([docker, "compose", "--project-name", instance,
             "-f", str(compose), "ps", "--all", "--quiet", service], text=True).split()
        if len(ids) != 1:
            raise AssertionError("Registered Docker service does not have one instance")
        data = json.loads(subprocess.check_output([docker, "inspect", ids[0]], text=True))[0]
        mapping = (data.get("NetworkSettings", {}).get("Ports") or {}).get(internal) or []
        if not any(int(bound.get("HostPort") or 0) == port and
                   bound.get("HostIp") == "127.0.0.1" for bound in mapping):
            raise AssertionError("Registered host publication does not match " + service)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--installation", type=Path, required=True)
    parser.add_argument("--instance", required=True)
    parser.add_argument("--other-web", type=int, required=True)
    parser.add_argument("--other-api", type=int, required=True)
    parser.add_argument("--other-database", type=int, required=True)
    args = parser.parse_args()
    installation = args.installation.resolve(strict=True)
    profile_dir = installation / "web/scada-web/public/installation"
    script = profile_dir / "docker-host-executor.py"
    loader = importlib.util.spec_from_file_location("scadivo_docker_real_host", script)
    host = importlib.util.module_from_spec(loader)
    loader.loader.exec_module(host)
    docker_exec = Path(shutil.which("docker") or "").resolve(strict=True)
    allowed = {args.other_web, args.other_api, args.other_database}
    if len(allowed) != 3 or not args.instance.startswith("scad-ci-"):
        raise AssertionError("Real Docker host fixture requires isolated CI installations")
    with tempfile.TemporaryDirectory(prefix="scadivo-docker-host-ci-") as directory:
        root = Path(directory)
        root.chmod(0o700)
        state = root / "state"
        state.mkdir(mode=0o700)
        profile = root / "docker-host-profile.json"
        registration = root / "registration.json"
        token = root / "token"
        sock = root / "network.sock"
        original = {}
        # Parse only the known non-secret port keys; never print the .env.
        env = {}
        for line in (installation / ".env").read_text().splitlines():
            if "=" in line:
                k, v = line.split("=", 1)
                if k in {"ELITESCADA_WEB_PORT", "ELITESCADA_API_PORT", "ELITESCADA_DB_PORT",
                         "SCADIVO_MEDIA_CONTROL_PORT", "SCADIVO_MEDIA_PLAYBACK_PORT"}:
                    env[k] = int(v)
        original = {
            "webPort": env["ELITESCADA_WEB_PORT"], "apiPort": env["ELITESCADA_API_PORT"],
            "databasePort": env["ELITESCADA_DB_PORT"],
            "mediaControlPort": env["SCADIVO_MEDIA_CONTROL_PORT"],
            "mediaPlaybackPort": env["SCADIVO_MEDIA_PLAYBACK_PORT"]
        }
        allowed.update(original.values())
        compose = installation / "docker-compose.stable.yml"
        secret_file(profile, json.dumps({
            "schema": "scadivo-docker-host-network-v1", "instanceId": args.instance,
            "installationDirectory": str(installation),
            "composeSha256": hashlib.sha256(compose.read_bytes()).hexdigest(),
            "dockerExecutable": str(docker_exec)
        }))
        secret_file(token, secrets.token_hex(32))
        secret_file(registration, json.dumps({
            "schema": "scadivo-network-host-executor-v1", "profile": "stable-docker-linux",
            "instanceId": args.instance, "socketPath": str(sock),
            "stateDirectory": str(state), "dockerProfilePath": str(profile),
            "tokenFile": str(token), "socketMode": "0600", "socketGroup": None
        }))
        process = subprocess.Popen([sys.executable, str(script), "--registration", str(registration)],
                                   stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        try:
            for _ in range(50):
                if process.poll() is not None:
                    raise AssertionError("Registered Docker host executor failed to start")
                if sock.exists():
                    try:
                        initial = request(host, registration, "/v1/status")
                        if initial["available"]: break
                    except OSError:
                        pass
                time.sleep(.2)
            else:
                raise AssertionError("Registered Docker host socket not available")
            if initial["instanceId"] != args.instance or initial["profile"] != "stable-docker-linux":
                raise AssertionError("Host executor registration identity mismatched")
            planned = {"webPort": choose_port(allowed), "apiPort": choose_port(allowed)}
            old_env = (installation / ".env").read_bytes()
            plan = request(host, registration, "/v1/preflight", {
                "schema": host.native.PLAN_SCHEMA, "module": "access", "ports": planned})
            if (installation / ".env").read_bytes() != old_env:
                raise AssertionError("Docker preflight modified secrets before apply")
            result = request(host, registration, "/v1/apply", {
                "schema": host.native.PLAN_SCHEMA, "planId": plan["planId"], "confirm": True})
            if result["state"] != "applying":
                raise AssertionError("Host apply skipped the durable pending state")
            await_state(host, registration, "applied")
            expected = {**original, **planned}
            assert_host_mapping(installation, args.instance, expected)
            assert_host_mapping(installation, args.instance, expected)
            print("PASS: registered Docker host executor applied Web/API publication and health through protected IPC.")

            # With the new first installation running, reject a second proposal
            # whose port is already occupied before any Compose restart.
            occupied = choose_port(allowed)
            with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as hold:
                hold.bind(("0.0.0.0", occupied))
                hold.listen(1)
                try:
                    request(host, registration, "/v1/preflight", {
                        "schema": host.native.PLAN_SCHEMA, "module": "access",
                        "ports": {"webPort": occupied, "apiPort": planned["apiPort"]}})
                except host.native.ContractError:
                    pass
                else:
                    raise AssertionError("Occupied host port incorrectly passed executor preflight")
                assert_host_mapping(installation, args.instance, expected)

            # Deliberately invalidate a prepared port after preflight to verify
            # durable apply failure and recovery of the still-live prior profile.
            after = {"webPort": choose_port(allowed), "apiPort": choose_port(allowed)}
            retry = request(host, registration, "/v1/preflight", {
                "schema": host.native.PLAN_SCHEMA, "module": "access", "ports": after})
            with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as hold:
                hold.bind(("0.0.0.0", after["webPort"]))
                hold.listen(1)
                failed = request(host, registration, "/v1/apply", {
                    "schema": host.native.PLAN_SCHEMA, "planId": retry["planId"], "confirm": True})
                if failed["state"] != "applying":
                    raise AssertionError("Expected durable pending state")
                await_state(host, registration, "recovery_required")
            request(host, registration, "/v1/recover", {
                "schema": host.native.PLAN_SCHEMA, "confirm": True})
            await_state(host, registration, "recovered")
            assert_host_mapping(installation, args.instance, expected)
            print("PASS: occupied preflight, race-before-apply, durable failure and verified Docker recovery.")
        finally:
            process.terminate()
            try: process.wait(timeout=10)
            except subprocess.TimeoutExpired:
                process.kill()
                process.wait(timeout=10)


if __name__ == "__main__":
    main()
