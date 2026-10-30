import importlib.util
import json
from pathlib import Path
import unittest
import tempfile
from unittest.mock import Mock, patch
import sys
import types
import socket
import threading
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

source = Path(__file__).resolve().parents[2] / 'web/scada-web/public/installation/native_media_ports.py'
spec = importlib.util.spec_from_file_location('native_media', source)
media = importlib.util.module_from_spec(spec)
spec.loader.exec_module(media)


class NativeMediaPortTests(unittest.TestCase):
    def test_real_local_readiness_and_redirect_refusal(self):
        class Handler(BaseHTTPRequestHandler):
            redirect = False
            def log_message(self, *args): pass
            def do_GET(self):
                self.send_response(302 if self.redirect else 200)
                if self.redirect: self.send_header('Location', '/unexpected')
                self.end_headers()
                self.wfile.write(b'{"items":[]}')
        server=ThreadingHTTPServer(('127.0.0.1',0),Handler)
        thread=threading.Thread(target=server.serve_forever,daemon=True); thread.start()
        try:
            with socket.socket() as listener:
                listener.bind(('127.0.0.1',0)); listener.listen(2)
                content=json.dumps({'MediaSources':{'GatewayControlUrl':f'http://127.0.0.1:{server.server_port}',
                    'GatewayPlaybackUrl':f'http://127.0.0.1:{listener.getsockname()[1]}'}}).encode()
                media.verify_local_media(content)
                Handler.redirect=True
                with patch.object(media.time,'monotonic',side_effect=[0,61]):
                    with self.assertRaisesRegex(RuntimeError,'timed out'): media.verify_local_media(content)
                self.assertRaises(ValueError,media.verify_local_media,content.replace(b'127.0.0.1',b'example.com'))
        finally:
            server.shutdown(); server.server_close(); thread.join()

    def test_administrative_dispatch_uses_existing_platform_service_control(self):
        for platform in ('windows','linux'):
            with self.subTest(platform=platform), tempfile.TemporaryDirectory() as directory:
                root=Path(directory); app=root/'app.json'; profile=root/'profile.json'
                app.write_bytes(self.application)
                profile.write_text(json.dumps({'schema':'scadivo-native-network-v1','platform':platform,'configurationFile':str(app)}))
                controller=Mock()
                def apply(*args):
                    args[1]('restart','media-service')
                    self.assertIs(args[2],media.verify_local_media)
                    return 'done'
                with patch.dict(sys.modules,{'native_database_port':types.SimpleNamespace(control_service=controller)}), patch.object(media,'apply_locked',side_effect=apply), patch.object(media,'recover_locked',return_value='recovered') as recovery:
                    self.assertEqual(media.execute_locked(profile,19997,18888,True),'done')
                    controller.assert_called_once_with(platform,'restart','media-service')
                    self.assertEqual(media.execute_locked(profile,recover=True),'recovered')
                    recovery.assert_called_once()
                    with self.assertRaises(ValueError): media.execute_locked(profile,19997,recover=True)

    def test_interruption_recovery_validates_backups_and_retains_journal_on_failure(self):
        for platform in ('windows', 'linux'):
            with self.subTest(platform=platform), tempfile.TemporaryDirectory() as directory:
                root=Path(directory)
                app=root/'app.json'; gateway=root/'media.yml'; profile=root/'profile.json'
                app.write_bytes(self.application); gateway.write_bytes(self.gateway)
                profile.write_text(json.dumps({'schema':'scadivo-native-network-v1','platform':platform,
                    'configurationFile':str(app),'serviceName':'api-service',
                    'media':{'configurationFile':str(gateway),'serviceName':'media-service'}}))
                import socket
                with socket.socket() as listener:
                    listener.bind(('127.0.0.1',0)); port=listener.getsockname()[1]
                service=Mock()
                service.side_effect=[None,KeyboardInterrupt()]
                with self.assertRaises(KeyboardInterrupt):
                    media.apply_locked(profile,service,Mock(),control_port=port,apply=True)
                journal=app.with_name(app.name+'.media-port-pending.json')
                record=json.loads(journal.read_text())
                self.assertNotIn('preserved',journal.read_text())
                self.assertNotEqual(app.read_bytes(),self.application)
                service=Mock(); verify=Mock(side_effect=RuntimeError('unavailable'))
                bad=dict(record,backups=[str(profile),record['backups'][1]])
                journal.write_text(json.dumps(bad))
                with self.assertRaises(ValueError): media.recover_locked(profile,service,verify)
                service.assert_not_called()
                journal.write_text(json.dumps(record))
                with self.assertRaises(RuntimeError): media.recover_locked(profile,service,verify)
                self.assertTrue(journal.exists())
                self.assertEqual(app.read_bytes(),self.application)
                self.assertEqual(gateway.read_bytes(),self.gateway)
                self.assertNotIn(unittest.mock.call('start','api-service'),service.call_args_list)
                service.reset_mock(); verify=Mock()
                media.recover_locked(profile,service,verify)
                self.assertFalse(journal.exists())
                self.assertEqual(service.call_args_list,[unittest.mock.call('stop','api-service'),
                    unittest.mock.call('restart','media-service'),unittest.mock.call('start','api-service')])
                verify.assert_called_once_with(self.application)

    def test_apply_and_failed_verification_restore_both_files(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory)
            app=root/'app.json'; gateway=root/'media.yml'; profile=root/'profile.json'
            app.write_bytes(self.application); gateway.write_bytes(self.gateway)
            profile.write_text(json.dumps({'schema':'scadivo-native-network-v1','platform':'linux',
                'configurationFile':str(app),'serviceName':'api-service',
                'media':{'configurationFile':str(gateway),'serviceName':'media-service'}}))
            import socket
            with socket.socket() as listener:
                listener.bind(('127.0.0.1',0)); port=listener.getsockname()[1]
            service=Mock(); verify=Mock()
            media.apply_locked(profile,service,verify,control_port=port)
            self.assertEqual(app.read_bytes(),self.application)
            service.assert_not_called()
            media.apply_locked(profile,service,verify,control_port=port,apply=True)
            self.assertEqual(service.call_args_list[0].args,('stop','api-service'))
            self.assertEqual(service.call_args_list[1].args,('restart','media-service'))
            self.assertEqual(service.call_args_list[2].args,('start','api-service'))
            self.assertEqual(json.loads(app.read_bytes())['secret'],'preserved')
            app.write_bytes(self.application); gateway.write_bytes(self.gateway)
            verify.side_effect=[RuntimeError('new media unreachable'),None]
            with self.assertRaisesRegex(RuntimeError,'previous configuration restored'):
                media.apply_locked(profile,service,verify,control_port=port,apply=True)
            self.assertEqual(app.read_bytes(),self.application)
            self.assertEqual(gateway.read_bytes(),self.gateway)
            self.assertFalse(app.with_name(app.name+'.media-port-pending.json').exists())

    def setUp(self):
        self.application = json.dumps({'secret':'preserved', 'MediaSources':{
            'GatewayControlUrl':'http://127.0.0.1:9997',
            'GatewayPlaybackUrl':'http://127.0.0.1:8888'}}).encode()
        self.gateway = b'api: true\napiAddress: :9997\nhls: true\nhlsAddress: :8888\npaths: {}\n'

    def test_defaults_preserved_and_consumers_updated_together(self):
        self.assertEqual(media.prepare(self.application,self.gateway),(self.application,self.gateway))
        app, gateway = media.prepare(self.application,self.gateway,19997,18888)
        result=json.loads(app)
        self.assertEqual(result['secret'],'preserved')
        self.assertEqual(result['MediaSources']['GatewayControlUrl'],'http://127.0.0.1:19997')
        self.assertEqual(result['MediaSources']['GatewayPlaybackUrl'],'http://127.0.0.1:18888')
        self.assertIn(b'apiAddress: ":19997"',gateway)
        self.assertIn(b'hlsAddress: ":18888"',gateway)
        self.assertIn(b'paths: {}',gateway)

    def test_collision_duplicate_and_preexisting_mismatch_refused(self):
        with self.assertRaises(ValueError): media.prepare(self.application,self.gateway,8888)
        with self.assertRaises(ValueError): media.prepare(self.application,self.gateway+b'apiAddress: :9997\n',19997)
        with self.assertRaises(ValueError): media.prepare(self.application,self.gateway.replace(b':9997',b':9998'),19997)
        with self.assertRaises(ValueError): media.prepare(self.application,self.gateway,70000)

    def test_single_port_change_preserves_other_service(self):
        app, gateway=media.prepare(self.application,self.gateway,hls_port=18888)
        self.assertEqual(json.loads(app)['MediaSources']['GatewayControlUrl'],'http://127.0.0.1:9997')
        self.assertIn(b'apiAddress: ":9997"',gateway)
