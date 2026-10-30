import importlib.util
import json
from pathlib import Path
import socket
import tempfile
import types
import sys
import unittest
import subprocess
import threading
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from unittest.mock import patch

MODULE = Path(__file__).resolve().parents[2] / 'web/scada-web/public/installation/apply-native-network-profile.py'
spec = importlib.util.spec_from_file_location('native_network_adapter', MODULE)
adapter = importlib.util.module_from_spec(spec)
spec.loader.exec_module(adapter)

class NativeNetworkAdapterTests(unittest.TestCase):
    def test_health_requires_product_response_and_does_not_follow_redirect(self):
        class Handler(BaseHTTPRequestHandler):
            body=b'{"status":"ok","service":"scada-api"}'
            redirect=False
            unexpected_hits=0
            def log_message(self,*args): pass
            def do_GET(self):
                if self.path != '/health': Handler.unexpected_hits += 1
                self.send_response(302 if self.redirect and self.path=='/health' else 200)
                if self.redirect: self.send_header('Location','/other')
                self.end_headers(); self.wfile.write(self.body)
        server=ThreadingHTTPServer(('127.0.0.1',0),Handler)
        thread=threading.Thread(target=server.serve_forever,daemon=True); thread.start()
        try:
            url=f'http://127.0.0.1:{server.server_port}/health'
            adapter.health(url)
            with patch.object(adapter.time,'sleep'):
                for body in (b'<html>Login</html>',b'{"status":"ok","service":"unrelated"}'):
                    Handler.body=body
                    with self.assertRaises(RuntimeError): adapter.health(url)
                Handler.body=b'{"status":"ok","service":"scada-api"}'; Handler.redirect=True
                with self.assertRaises(RuntimeError): adapter.health(url)
            self.assertEqual(Handler.unexpected_hits,0)
        finally:
            server.shutdown(); server.server_close(); thread.join()

    def test_interrupted_access_change_requires_explicit_recovery(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory); config=root/'app.json'; profile=root/'profile.json'
            original=b'{"Kestrel":{"Endpoints":{"Web":{"Url":"http://127.0.0.1:18080"},"Api":{"Url":"http://127.0.0.1:15120"}}},"secret":"preserved"}'
            config.write_bytes(original)
            profile.write_text(json.dumps({'schema':'scadivo-native-network-v1','platform':'linux','serviceName':'api',
                'configurationFile':str(config),'endpointName':'Web','apiEndpointName':'Api'}))
            with socket.socket() as listener:
                listener.bind(('127.0.0.1',0)); port=listener.getsockname()[1]
            with patch.object(adapter.subprocess,'run',side_effect=[None,KeyboardInterrupt()]), patch.object(adapter,'health'):
                with self.assertRaises(KeyboardInterrupt): adapter.run_locked(profile,port,True)
            journal=config.with_name(config.name+'.access-port-pending.json')
            self.assertTrue(journal.exists())
            self.assertNotIn('preserved',journal.read_text())
            record=json.loads(journal.read_text())
            with patch.object(adapter.subprocess,'run') as service, patch.object(adapter,'health') as health:
                with self.assertRaisesRegex(RuntimeError,'requires recovery'): adapter.run_locked(profile,port,True)
                service.assert_not_called()
                journal.write_text(json.dumps(dict(record,backup=str(profile))))
                with self.assertRaises(ValueError): adapter.recover_locked(profile)
                service.assert_not_called()
                journal.write_text(json.dumps(record))
                health.side_effect=RuntimeError('unavailable')
                with self.assertRaises(RuntimeError): adapter.recover_locked(profile)
                self.assertTrue(journal.exists())
                health.side_effect=None
                with patch.dict(adapter.os.environ,{'SCADIVO_INSTALLATION_BEARER':'fixture-only'}),patch.object(adapter,'authenticated_readiness',side_effect=lambda path:self.assertTrue(journal.exists())) as readiness:
                    adapter.recover_locked(profile,True)
                    readiness.assert_called_once()
                self.assertFalse(journal.exists())
                self.assertEqual(config.read_bytes(),original)
                self.assertEqual(health.call_args_list[-2].args[0],'http://127.0.0.1:18080/health')
                self.assertEqual(health.call_args_list[-1].args[0],'http://127.0.0.1:15120/health')

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
            profile.write_text(json.dumps({'platform':'linux','configurationFile': str(config)}))
            with config.with_name('app.json.network.lock').open('w') as lock:
                fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
                with patch.object(adapter, 'run_locked') as web, patch.object(database, 'execute_locked') as db:
                    with self.assertRaises(BlockingIOError): adapter.run(profile, 28080, True)
                    with self.assertRaises(BlockingIOError): database.execute(profile, 25432, True)
                    web.assert_not_called()
                    db.assert_not_called()
                # Exercise the actual Bash/Python process boundary while the
                # canonical installation lock is held by another process.
                result=subprocess.run(['bash',str(db_path.with_name('apply-native-database-port.sh')),
                    '--profile',str(profile),'--database-port','25432','--apply'],capture_output=True,text=True,timeout=10)
                self.assertNotEqual(result.returncode,0)
                self.assertIn('Native database operation failed',result.stderr)
                self.assertEqual(config.read_text(),'{}')
                invalid=subprocess.run(['bash',str(db_path.with_name('apply-native-database-port.sh')),
                    '--profile',str(profile),'--database-port','25432','--recover'],capture_output=True,text=True,timeout=10)
                self.assertNotEqual(invalid.returncode,0)
                self.assertIn('Recovery cannot be combined',invalid.stderr)
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
                health.side_effect=None
                with patch.dict(adapter.os.environ,{'SCADIVO_INSTALLATION_BEARER':'fixture-only'}),patch.object(adapter,'authenticated_readiness',side_effect=[RuntimeError('unready'),None]) as readiness:
                    with self.assertRaisesRegex(RuntimeError,'restored and healthy'):adapter.run_locked(profile,port,True,None,True)
                    self.assertEqual(readiness.call_count,2)
                self.assertEqual(config.read_bytes(),original)
                self.assertFalse(config.with_name(config.name+'.access-port-pending.json').exists())

if __name__ == '__main__':
    unittest.main()
