import importlib.util
import json
from pathlib import Path
import socket
import tempfile
import unittest
from unittest.mock import patch

MODULE = Path(__file__).resolve().parents[2] / 'web/scada-web/public/installation/apply-native-network-profile.py'
spec = importlib.util.spec_from_file_location('native_network_adapter', MODULE)
adapter = importlib.util.module_from_spec(spec)
spec.loader.exec_module(adapter)

class NativeNetworkAdapterTests(unittest.TestCase):
    def test_plan_apply_and_rollback_preserve_configuration(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            config = root / 'appsettings.json'
            original = json.dumps({'Kestrel': {'Endpoints': {'Http': {'Url': 'http://127.0.0.1:18080'}}}, 'secret': 'keep-private'}).encode()
            config.write_bytes(original)
            profile = root / 'profile.json'
            profile.write_text(json.dumps({'schema': 'scadivo-native-network-v1', 'platform': 'linux', 'serviceName': 'scadivo-test.service', 'configurationFile': str(config), 'endpointName': 'Http'}))
            with socket.socket() as probe:
                probe.bind(('127.0.0.1', 0))
                port = probe.getsockname()[1]
            with patch.object(adapter.subprocess, 'run') as service, patch.object(adapter, 'health') as health:
                adapter.run(profile)
                adapter.run(profile, port)
                self.assertEqual(config.read_bytes(), original)
                self.assertFalse(any(call.args[0][1] == 'restart' for call in service.call_args_list))
                adapter.run(profile, port, True)
                result = json.loads(config.read_text())
                self.assertEqual(result['secret'], 'keep-private')
                self.assertIn(str(port), result['Kestrel']['Endpoints']['Http']['Url'])
                config.write_bytes(original)
                service.reset_mock()
                health.side_effect = [RuntimeError('failure'), None]
                with self.assertRaisesRegex(RuntimeError, 'restored and healthy'):
                    adapter.run(profile, port, True)
                self.assertEqual(config.read_bytes(), original)
                self.assertEqual(sum(call.args[0][1] == 'restart' for call in service.call_args_list), 2)
                self.assertEqual(health.call_args.args[0], 'http://127.0.0.1:18080/health')

if __name__ == '__main__':
    unittest.main()