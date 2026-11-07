"""Bounded Linux host-executor contract tests; never contact real systemd/Docker."""
import importlib.util
import json
import os
from pathlib import Path
import socket
import tempfile
import threading
import time
import unittest

SOURCE = Path(__file__).resolve().parents[2] / "web/scada-web/public/installation/network-host-executor.py"
spec = importlib.util.spec_from_file_location("scadivo_network_host_executor", SOURCE)
agent = importlib.util.module_from_spec(spec)
spec.loader.exec_module(agent)


@unittest.skipUnless(os.name == "posix", "Unix socket and file ACL gate is Linux-only")
class HostExecutorTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary.name)
        self.root.chmod(0o700)
        self.state = self.root / "state"
        self.state.mkdir(mode=0o700)
        self.socket = self.root / "network.sock"
        self.token = self.root / "token"
        self._file(self.token, "a" * 64)
        self.config = self.root / "appsettings.json"
        self._configure(web=18080, api=15120)
        self.profile = self.root / "native-profile.json"
        self._file(self.profile, json.dumps({
            "schema": "scadivo-native-network-v1",
            "platform": "linux",
            "serviceName": "scadivo-fixture",
            "configurationFile": str(self.config),
            "endpointName": "Web",
            "apiEndpointName": "Api"
        }))
        self.registration = self.root / "registration.json"
        self._file(self.registration, json.dumps({
            "schema": agent.SCHEMA,
            "instanceId": "scadivo-test",
            "profile": "native-linux",
            "socketPath": str(self.socket),
            "stateDirectory": str(self.state),
            "nativeProfilePath": str(self.profile),
            "tokenFile": str(self.token),
            "socketMode": "0600"
        }))
        self.calls = []
        self.block = None

        def runner(command):
            self.calls.append(command)
            if self.block is not None and "--apply" in command:
                self.block.wait(timeout=3)
            if "--apply" in command:
                index = command.index("--web-port")
                self._configure(web=int(command[index + 1]), api=int(command[command.index("--api-port") + 1]))
            if "--recover" in command:
                self._configure(web=18080, api=15120)
        self.executor = agent.Executor(self.registration, runner=runner)

    def tearDown(self):
        os.close(self.executor._lock_fd)
        self.temporary.cleanup()

    def _file(self, target, content):
        target.write_text(content, encoding="utf-8")
        target.chmod(0o600)

    def _configure(self, web, api):
        self._file(self.config, json.dumps({
            "Kestrel": {"Endpoints": {"Web": {"Url": "http://127.0.0.1:" + str(web)},
                                      "Api": {"Url": "http://127.0.0.1:" + str(api)}}}
        }))

    @staticmethod
    def body(web=28080, api=25120):
        return {"schema": agent.PLAN_SCHEMA, "module": "access", "ports": {"webPort": web, "apiPort": api}}

    def _wait(self, wanted):
        for _ in range(150):
            value = self.executor.snapshot().get("operation")
            if value and value["state"] == wanted:
                return value
            time.sleep(.01)
        self.fail("Operation did not reach " + wanted)

    def test_registration_denies_unprotected_files_and_unsupported_profiles(self):
        self.registration.chmod(0o666)
        with self.assertRaises(agent.ContractError):
            agent.Executor(self.registration, runner=lambda _: None)
        self.registration.chmod(0o600)
        data = json.loads(self.registration.read_text())
        data["profile"] = "stable-docker"
        self._file(self.registration, json.dumps(data))
        with self.assertRaisesRegex(agent.ContractError, "No validated"):
            agent.Executor(self.registration, runner=lambda _: None)

    def test_invalid_conflicting_or_arbitrary_plans_never_call_runner(self):
        attempts = [
            {"schema": agent.PLAN_SCHEMA, "module": "shell", "ports": {"command": 7}},
            {"schema": agent.PLAN_SCHEMA, "module": "access", "ports": {"webPort": 28080, "apiPort": 28080}},
            {"schema": agent.PLAN_SCHEMA, "module": "access", "ports": {"webPort": -1, "apiPort": 25120}},
            {"schema": agent.PLAN_SCHEMA, "module": "access", "ports": {"webPort": 28080, "apiPort": "25120"}},
            {"schema": agent.PLAN_SCHEMA, "module": "access", "ports": {"webPort": 28080, "apiPort": 25120}, "path": "/etc/shadow"},
            {"schema": agent.PLAN_SCHEMA, "module": "database", "ports": {"databasePort": 15432}},
        ]
        for attempt in attempts:
            with self.subTest(attempt=attempt), self.assertRaises(agent.ContractError):
                self.executor.preflight(attempt)
        self.assertEqual([], self.calls)

    def test_preflight_rejects_conflicts_across_registered_host_services(self):
        registered = json.loads(self.profile.read_text())
        registered["database"] = {"currentPort": 15432, "configurationFile": str(self.root / "pg.conf"),
                                  "serviceName": "pg-fixture"}
        self._file(self.root / "pg.conf", "port = 15432\n")
        self._file(self.profile, json.dumps(registered))
        with self.assertRaisesRegex(agent.ContractError, "conflict"):
            self.executor.preflight(self.body(web=15432, api=25120))
        with self.assertRaisesRegex(agent.ContractError, "conflict"):
            self.executor.preflight(self.body(web=28080, api=15432))
        self.assertEqual([], self.calls)

    def test_preflight_is_nonmutating_plan_expires_and_stale_config_rejected(self):
        before = self.config.read_bytes()
        planned = self.executor.preflight(self.body())
        self.assertEqual(before, self.config.read_bytes())
        self.assertNotIn("--apply", self.calls[0])
        self.assertTrue(planned["requiresRestart"])
        self.assertEqual(18080, planned["before"]["webPort"])
        self._configure(web=18081, api=15120)
        with self.assertRaisesRegex(agent.ContractError, "expired or configuration changed"):
            self.executor.apply({"schema": agent.PLAN_SCHEMA, "planId": planned["planId"], "confirm": True})
        self.assertEqual(1, len(self.calls))

    def test_confirmed_apply_persists_status_and_rechecks_config(self):
        planned = self.executor.preflight(self.body())
        with self.assertRaises(agent.ContractError):
            self.executor.apply({"schema": agent.PLAN_SCHEMA, "planId": planned["planId"], "confirm": False})
        accepted = self.executor.apply({"schema": agent.PLAN_SCHEMA, "planId": planned["planId"], "confirm": True})
        self.assertEqual("applying", accepted["state"])
        self.assertEqual("applied", self._wait("applied")["state"])
        self.assertEqual(28080, self.executor.snapshot()["configuredPorts"]["webPort"])
        persisted = json.loads((self.state / "network-operation.json").read_text())
        self.assertEqual(accepted["operationId"], persisted["id"])
        self.assertFalse((self.state / "network-plan.json").exists())

    def test_concurrent_apply_rejected_while_original_operation_runs(self):
        self.block = threading.Event()
        planned = self.executor.preflight(self.body())
        self.executor.apply({"schema": agent.PLAN_SCHEMA, "planId": planned["planId"], "confirm": True})
        with self.assertRaisesRegex(agent.ContractError, "already running"):
            self.executor.apply({"schema": agent.PLAN_SCHEMA, "planId": planned["planId"], "confirm": True})
        with self.assertRaisesRegex(agent.ContractError, "Previous host operation"):
            self.executor.preflight(self.body(28081, 25121))
        self.block.set()
        self._wait("applied")

    def test_failed_apply_stays_recovery_required_until_verified_recovery(self):
        def broken(command):
            self.calls.append(command)
            if "--apply" in command:
                raise agent.ContractError("simulated host failure")
            if "--recover" in command:
                self._configure(18080, 15120)
        self.executor.runner = broken
        planned = self.executor.preflight(self.body())
        self.executor.apply({"schema": agent.PLAN_SCHEMA, "planId": planned["planId"], "confirm": True})
        self._wait("recovery_required")
        self.executor.recover({"schema": agent.PLAN_SCHEMA, "confirm": True})
        self._wait("recovered")
        self.assertEqual(18080, self.executor.snapshot()["configuredPorts"]["webPort"])

    def test_interrupted_running_record_requires_recovery(self):
        self.executor._save(self.executor.state_file, {
            "id": "a" * 32, "module": "access", "state": "applying",
            "before": {"webPort": 18080, "apiPort": 15120},
            "desired": {"webPort": 28080, "apiPort": 25120}
        })
        self.executor._repair_interrupted()
        self.assertEqual("recovery_required", self.executor.snapshot()["operation"]["state"])

    def test_unix_ipc_denies_missing_token_and_accepts_status_for_registered_installation(self):
        with agent.Server(self.executor) as server:
            thread = threading.Thread(target=server.serve_forever, kwargs={"poll_interval": .01}, daemon=True)
            thread.start()
            self.assertEqual(0o600, self.socket.stat().st_mode & 0o777)
            def call(token):
                s = socket.socket(socket.AF_UNIX, socket.SOCK_STREAM)
                s.settimeout(3)
                s.connect(str(self.socket))
                request = ("GET /v1/status HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n"
                           "X-Scadivo-Installation-Token: " + token + "\r\n\r\n")
                s.sendall(request.encode())
                result = bytearray()
                while True:
                    chunk = s.recv(8192)
                    if not chunk: break
                    result.extend(chunk)
                s.close()
                return bytes(result)
            self.assertIn(b"403 Forbidden", call("bad-token"))
            response = call("a" * 64)
            self.assertIn(b"200 OK", response)
            self.assertIn(b'"instanceId":"scadivo-test"', response)
            server.shutdown()
            thread.join(timeout=2)


if __name__ == "__main__":
    unittest.main()
