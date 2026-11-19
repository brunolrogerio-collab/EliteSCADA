import importlib.util
import json
from pathlib import Path
import smtplib
from tempfile import TemporaryDirectory
import unittest
from unittest.mock import Mock, patch


spec = importlib.util.spec_from_file_location("monitor", Path(__file__).with_name("monitor-portal.py"))
monitor = importlib.util.module_from_spec(spec)
spec.loader.exec_module(monitor)

CONFIG = {"portal_url": "https://portal.example.test", "smtp_user": "monitor@example.test",
          "recipient": "owner@example.test"}
DATABASE = {"email_failed": 0, "email_retry": 0, "email_stale": 0, "renewal_overdue": 0}
SESSION = (200, {"authenticated": False, "emailDeliveryConfigured": True})
RENEWAL = (401, {"error": "invalid-credentials"})


class MonitorTests(unittest.TestCase):
    def test_live_probe_requires_the_expected_unauthenticated_response(self):
        with patch.object(monitor, "http_json", side_effect=[SESSION, RENEWAL]) as http, \
                patch.object(monitor, "database_snapshot", return_value=DATABASE):
            self.assertEqual(monitor.check(CONFIG, True), {name: "" for name in monitor.COMPONENTS})
        args = http.call_args.args
        self.assertTrue(args[0].endswith("/renew"))
        self.assertEqual(len(args[1]["nextRefreshCredential"]), 43)

    def test_api_and_database_failure_do_not_report_recovery_of_unobserved_checks(self):
        with patch.object(monitor, "http_json", return_value=(503, {})) as http, \
                patch.object(monitor, "database_snapshot", return_value=None):
            issues = monitor.check(CONFIG, True)
        self.assertEqual(http.call_count, 1)
        self.assertTrue(issues["API"])
        self.assertTrue(issues["Banco"])
        self.assertIsNone(issues["E-mails"])
        self.assertIsNone(issues["Renovação"])
        state = {"active": {"E-mails": True, "Renovação": True}, "pending": []}
        monitor.record(state, issues, "now")
        self.assertEqual([event["name"] for event in state["pending"]], ["API", "Banco"])

    def test_queue_failure_and_overdue_renewal_signals(self):
        snapshot = dict(DATABASE, email_failed=2, email_retry=1, email_stale=3, renewal_overdue=4)
        with patch.object(monitor, "http_json", side_effect=[SESSION, RENEWAL]), \
                patch.object(monitor, "database_snapshot", return_value=snapshot):
            issues = monitor.check(CONFIG, True)
        self.assertIn("Falhas recentes: 2", issues["E-mails"])
        self.assertIn("itens atrasados: 3", issues["E-mails"])
        self.assertIn("4 ativação", issues["Renovação"])
        with patch.object(monitor, "http_json", side_effect=[SESSION, (503, {})]), \
                patch.object(monitor, "database_snapshot", return_value=DATABASE):
            issues = monitor.check(CONFIG, False)
        self.assertIn("SMTP", issues["E-mails"])
        self.assertIn("assinador", issues["Renovação"])

    def test_unsent_alert_survives_smtp_failure_and_combines_with_recovery(self):
        healthy = {name: "" for name in monitor.COMPONENTS}
        failed = dict(healthy, API="falha")
        with TemporaryDirectory() as directory:
            config = Path(directory) / "config.json"
            config.write_text(json.dumps(CONFIG), encoding="utf-8")
            state = Path(directory) / "state.json"
            cli = ["monitor", "--config", str(config), "--state", str(state)]
            with patch("sys.argv", cli), patch.object(monitor, "check", return_value=failed), \
                    patch.object(monitor, "open_smtp", side_effect=smtplib.SMTPException):
                with self.assertRaises(RuntimeError):
                    monitor.main()
            self.assertTrue(json.loads(state.read_text(encoding="utf-8"))["pending"])
            client = Mock()
            with patch("sys.argv", cli), patch.object(monitor, "check", return_value=healthy), \
                    patch.object(monitor, "open_smtp", return_value=client):
                self.assertEqual(monitor.main(), 0)
                self.assertEqual(monitor.main(), 0)
            self.assertEqual(client.send_message.call_count, 1)
            message = client.send_message.call_args.args[0]
            self.assertIn("API · Falha", message.get_content())
            self.assertIn("API · Sinal normalizado", message.get_content())
            self.assertFalse(json.loads(state.read_text(encoding="utf-8"))["pending"])

    def test_simulated_email_has_four_components_without_changing_status(self):
        with TemporaryDirectory() as directory:
            config = Path(directory) / "config.json"
            config.write_text(json.dumps(CONFIG), encoding="utf-8")
            state = Path(directory) / "state.json"
            client = Mock()
            with patch("sys.argv", ["monitor", "--config", str(config), "--state", str(state), "--test-email"]), \
                    patch.object(monitor, "open_smtp", return_value=client):
                self.assertEqual(monitor.main(), 0)
            self.assertFalse(state.exists())
            message = client.send_message.call_args.args[0]
            self.assertTrue(message["Subject"].startswith("[TESTE]"))
            for component in monitor.COMPONENTS:
                self.assertIn(component + " · Falha", message.get_content())


if __name__ == "__main__":
    unittest.main()
