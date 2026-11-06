"""The Docker host executor, not a Docker adapter CLI alone, owns these tests.

No local Docker is used: a registered Compose boundary and its command/health
effects are simulated. Real Docker integration is a separate CI fixture.
"""
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import socket
import tempfile
import threading
import time
import unittest

ROOT = Path(__file__).resolve().parents[2] / "web/scada-web/public/installation"


def load(name, file):
    spec = importlib.util.spec_from_file_location(name, ROOT / file)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


docker = load("scadivo_docker_host_adapter_tests", "docker-host-adapter.py")
host = load("scadivo_docker_host_executor_tests", "docker-host-executor.py")


@unittest.skipUnless(os.name == "posix", "Host Unix socket only on Linux")
class DockerExecutorTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.root.chmod(0o700)
        self.install = self.root / "installed"
        self.install.mkdir(mode=0o700)
        self.state = self.root / "state"
        self.state.mkdir(mode=0o700)
        self.compose = self.install / "docker-compose.stable.yml"
        self.write(self.compose, "services:\n  api: {}\n  web: {}\n  database: {}\n  media-gateway: {}\n", 0o600)
        self.env = self.install / ".env"
        self.write(self.env, """SCADIVO_INSTALLATION_ID=host-docker-ci
SCADIVO_WEB_BIND=127.0.0.1
SCADIVO_API_BIND=127.0.0.1
ELITESCADA_WEB_PORT=18080
ELITESCADA_API_PORT=15120
ELITESCADA_DB_PORT=15432
SCADIVO_MEDIA_CONTROL_PORT=9997
SCADIVO_MEDIA_PLAYBACK_PORT=8888
ELITESCADA_DB_PASSWORD=never-log-this
""", 0o600)
        self.exe = self.root / "docker-probe"
        self.write(self.exe, "#!/bin/sh\nexit 1\n", 0o700)
        self.profile = self.root / "docker-host-profile.json"
        self.write(self.profile, json.dumps({
            "schema": docker.SCHEMA, "instanceId": "host-docker-ci",
            "installationDirectory": str(self.install),
            "composeSha256": hashlib.sha256(self.compose.read_bytes()).hexdigest(),
            "dockerExecutable": str(self.exe)
        }), 0o600)
        self.token_file = self.root / "token"
        self.write(self.token_file, "b" * 64, 0o600)
        self.socket = self.root / "network.sock"
        self.registration = self.root / "registration.json"
        self.write(self.registration, json.dumps({
            "schema": host.native.SCHEMA, "profile": "stable-docker-linux",
            "instanceId": "host-docker-ci", "socketPath": str(self.socket),
            "stateDirectory": str(self.state), "dockerProfilePath": str(self.profile),
            "tokenFile": str(self.token_file), "socketMode": "0600", "socketGroup": None
        }), 0o600)
        self.commands = []
        self.fail_apply = False
        self.owner_override = None

        def run(cmd):
            self.commands.append(cmd)
            if cmd[1] == "compose" and "ps" in cmd:
                return "a" * 64 + "\n" if self.owner_override else ""
            if cmd[1] == "inspect":
                if "--format" in cmd:
                    return json.dumps({
                        "com.docker.compose.project": "host-docker-ci",
                        "com.docker.compose.project.working_dir": self.owner_override
                    })
                return "[]"
            if cmd[1] == "ps": return ""
            if "config" in cmd and "--services" in cmd:
                return "web\napi\ndatabase\nmedia-gateway\n"
            if "up" in cmd and self.fail_apply:
                raise docker.AdapterError("Simulated Compose apply failure")
            return ""

        self.fake = run
        self.adapter = lambda: docker.DockerHostAdapter(
            self.profile, runner=self.fake, healthy=lambda _: None,
            check_bind=lambda _, __: None)

    @staticmethod
    def write(path, value, mode):
        path.write_text(value, encoding="utf-8")
        path.chmod(mode)

    def tearDown(self):
        if getattr(self, "executor", None):
            os.close(self.executor._lock_fd)
        self.temp.cleanup()

    def desired(self, web=28080, api=25120):
        return {"webPort": web, "apiPort": api, "databasePort": 15432,
                "mediaControlPort": 9997, "mediaPlaybackPort": 8888}

    def test_registered_project_compose_and_port_changes_are_bounded(self):
        before = self.env.read_bytes()
        self.adapter().preflight(self.desired())
        self.assertEqual(before, self.env.read_bytes())
        self.assertFalse(any("up" in command for command in self.commands))
        self.adapter().apply(self.desired())
        self.assertEqual(self.desired(), self.adapter().current())
        self.assertIn("never-log-this", self.env.read_text())
        record = self.adapter()._load_journal()
        self.assertEqual("applied", record["phase"])
        self.adapter().recover()
        self.assertEqual(18080, self.adapter().current()["webPort"])
        self.assertEqual("recovered", self.adapter()._load_journal()["phase"])
        self.assertTrue(any("up" in command for command in self.commands))

    def test_project_from_another_directory_denied_without_mutation(self):
        self.owner_override = str(self.root / "foreign")
        with self.assertRaisesRegex(docker.AdapterError, "another installation"):
            self.adapter().preflight(self.desired())
        self.assertNotIn("ELITESCADA_WEB_PORT=28080", self.env.read_text())
        self.assertFalse(any("up" in command for command in self.commands))

    def test_config_digest_and_script_scope_fail_closed(self):
        self.write(self.compose, "services: {evil: {}}\n", 0o600)
        with self.assertRaisesRegex(docker.AdapterError, "digest"):
            self.adapter().preflight(self.desired())
        self.assertFalse(any("up" in command for command in self.commands))
        self.write(self.compose, "services:\n  api: {}\n  web: {}\n  database: {}\n  media-gateway: {}\n", 0o600)
        self.write(self.env, self.env.read_text() + "ELITESCADA_API_PORT=22222\n", 0o600)
        with self.assertRaisesRegex(docker.AdapterError, "Duplicate"):
            self.adapter().preflight(self.desired())

    def test_actual_dedicated_unix_ipc_plan_apply_and_recovery(self):
        def executor_runner(cmd):
            flags = dict(zip(cmd[6::2], cmd[7::2]))
            mode = cmd[5]
            adapter = self.adapter()
            if mode == "recover":
                adapter.recover()
                return
            requested = {"webPort": int(flags["--web-port"]), "apiPort": int(flags["--api-port"]),
                         "databasePort": int(flags["--database-port"]),
                         "mediaControlPort": int(flags["--media-control-port"]),
                         "mediaPlaybackPort": int(flags["--media-playback-port"])}
            if mode == "preflight": adapter.preflight(requested)
            elif mode == "apply":
                if self.fail_apply: raise docker.AdapterError("Simulated Compose failure")
                adapter.apply(requested)
            else: self.fail("Unknown host executor mode")
        self.executor = host.DockerExecutor(self.registration, runner=executor_runner)
        with host.native.Server(self.executor) as server:
            worker = threading.Thread(target=server.serve_forever, kwargs={"poll_interval": .01}, daemon=True)
            worker.start()
            try:
                current = host.admin_rpc(self.registration, "/v1/status")
                self.assertEqual(["access", "database", "media"], current["modules"])
                plan = host.admin_rpc(self.registration, "/v1/preflight", {
                    "schema": host.native.PLAN_SCHEMA, "module": "access",
                    "ports": {"webPort": 28080, "apiPort": 25120}
                })
                self.assertEqual(18080, self.adapter().current()["webPort"])
                host.admin_rpc(self.registration, "/v1/apply", {
                    "schema": host.native.PLAN_SCHEMA, "planId": plan["planId"], "confirm": True
                })
                self.wait_state("applied")
                self.assertEqual(28080, self.adapter().current()["webPort"])
                self.fail_apply = True
                plan = host.admin_rpc(self.registration, "/v1/preflight", {
                    "schema": host.native.PLAN_SCHEMA, "module": "access",
                    "ports": {"webPort": 28081, "apiPort": 25121}
                })
                host.admin_rpc(self.registration, "/v1/apply", {
                    "schema": host.native.PLAN_SCHEMA, "planId": plan["planId"], "confirm": True
                })
                self.wait_state("recovery_required")
                host.admin_rpc(self.registration, "/v1/recover", {
                    "schema": host.native.PLAN_SCHEMA, "confirm": True
                })
                # In this test Docker health is simulated only; the state machine
                # must still avoid pretending failed apply was successful.
                self.wait_state("recovered")
            finally:
                server.shutdown()
                worker.join(timeout=3)

    def wait_state(self, wanted):
        for _ in range(150):
            state = host.admin_rpc(self.registration, "/v1/status").get("operation")
            if state and state["state"] == wanted: return
            time.sleep(.02)
        self.fail("Host operation never reached " + wanted)


if __name__ == "__main__":
    unittest.main()
