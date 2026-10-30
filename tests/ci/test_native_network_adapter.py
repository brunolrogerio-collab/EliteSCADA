import importlib.util
import json
from pathlib import Path
import socket
import tempfile
import types
import sys
import unittest
from unittest.mock import patch

MODULE = Path(__file__).resolve().parents[2] / 'web/scada-web/public/installation/apply-native-network-profile.py'
spec = importlib.util.spec_from_file_location('native_network_adapter', MODULE)
adapter = importlib.util.module_from_spec(spec)
spec.loader.exec_module(adapter)

class NativeNetworkAdapterTests(unittest.TestCase):
    @unittest.skipUnless(sys.platform.startswith('linux'), 'Linux flock integration')
    def test_real_linux_lock_blocks_web_and_database_entrypoints(self):
        import fcntl
        db_path = MODULE.with_name('native_database_port.py')
        db_spec = importlib.util.spec_from_file_location('native_db_lock_test', db_path)
        database = importlib.util.module_from_spec(db_spec)
        db_spec.loader.exec_module(database)
        with tempfile.TemporaryDirectory() as directory:
            config = Path(directory) / 'app.json'
            config.write_text('{}')
            profile = Path(directory) / 'profile.json'
            profile.write_text(json.dumps({'configurationFile': str(config)}))
            with config.with_name('app.json.network.lock').open('w') as lock:
                fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
                with patch.object(adapter, 'run_locked') as web, patch.object(database, 'execute_locked') as db:
                    with self.assertRaises(BlockingIOError): adapter.run(profile, 28080, True)
                    with self.assertRaises(BlockingIOError): database.execute(profile, 25432, True)
                    web.assert_not_called()
                    db.assert_not_called()
            with patch.object(adapter, 'run_locked', return_value='released'):
                self.assertEqual(adapter.run(profile), 'released')

    def test_shared_lock_rejects_web_change_before_mutation(self):
        with tempfile.TemporaryDirectory() as directory:
            config = Path(directory) / 'app.json'
            config.write_text('{}')
            profile = Path(directory) / 'profile.json'
            profile.write_text(json.dumps({'configurationFile': str(config)}))
            fake = types.SimpleNamespace(LOCK_EX=1, LOCK_NB=2, flock=lambda *args: (_ for _ in ()).throw(BlockingIOError()))
            with patch.dict(sys.modules, {'fcntl': fake}), patch.object(adapter, 'run_locked') as operation:
                with self.assertRaises(BlockingIOError): adapter.run(profile, 28080, True)
                operation.assert_not_called()
            self.assertTrue(config.with_name('app.json.network.lock').exists())
            self.assertEqual(config.read_text(), '{}')

    def test_independent_web_api_ports_and_rollback(self):
        with tempfile.TemporaryDirectory() as directory:
            config = Path(directory) / 'app.json'
            original = json.dumps({'Kestrel': {'Endpoints': {'Web': {'Url':'http://127.0.0.1:18080'}, 'Api': {'Url':'http://127.0.0.1:15120'}}}}).encode()
            profile = Path(directory) / 'profile.json'
            profile.write_text(json.dumps({'schema':'scadivo-native-network-v1','platform':'linux','serviceName':'test','configurationFile':str(config),'endpointName':'Web','apiEndpointName':'Api'}))
            with socket.socket() as listener:
                listener.bind(('127.0.0.1',0)); target=listener.getsockname()[1]
            with patch.object(adapter.subprocess,'run') as service, patch.object(adapter,'health') as health:
                config.write_bytes(original)
                adapter.run_locked(profile,target,True)
                endpoints=json.loads(config.read_text())['Kestrel']['Endpoints']
                self.assertIn(str(target),endpoints['Web']['Url'])
                self.assertEqual(endpoints['Api']['Url'],'http://127.0.0.1:15120')
                config.write_bytes(original)
                adapter.run_locked(profile,None,True,target)
                endpoints=json.loads(config.read_text())['Kestrel']['Endpoints']
                self.assertEqual(endpoints['Web']['Url'],'http://127.0.0.1:18080')
                self.assertIn(str(target),endpoints['Api']['Url'])
                config.write_bytes(original); service.reset_mock()
                health.side_effect=[None,RuntimeError('API unavailable'),None,None]
                with self.assertRaisesRegex(RuntimeError,'restored and healthy'):
                    adapter.run_locked(profile,None,True,target)
                self.assertEqual(config.read_bytes(),original)
                self.assertEqual(sum(call.args[0][1]=='restart' for call in service.call_args_list),2)
                service.reset_mock()
                with self.assertRaises(ValueError): adapter.run_locked(profile,28080,True,28080)
                service.assert_not_called()

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
                adapter.run_locked(profile)
                adapter.run_locked(profile, port)
                self.assertEqual(config.read_bytes(), original)
                self.assertFalse(any(call.args[0][1] == 'restart' for call in service.call_args_list))
                adapter.run_locked(profile, port, True)
                result = json.loads(config.read_text())
                self.assertEqual(result['secret'], 'keep-private')
                self.assertIn(str(port), result['Kestrel']['Endpoints']['Http']['Url'])
                config.write_bytes(original)
                service.reset_mock()
                health.side_effect = [RuntimeError('failure'), None]
                with self.assertRaisesRegex(RuntimeError, 'restored and healthy'):
                    adapter.run_locked(profile, port, True)
                self.assertEqual(config.read_bytes(), original)
                self.assertEqual(sum(call.args[0][1] == 'restart' for call in service.call_args_list), 2)
                self.assertEqual(health.call_args.args[0], 'http://127.0.0.1:18080/health')

if __name__ == '__main__':
    unittest.main()