import importlib.util
import json
from pathlib import Path
import unittest
import tempfile
from unittest.mock import Mock

source = Path(__file__).resolve().parents[2] / 'web/scada-web/public/installation/native_media_ports.py'
spec = importlib.util.spec_from_file_location('native_media', source)
media = importlib.util.module_from_spec(spec)
spec.loader.exec_module(media)


class NativeMediaPortTests(unittest.TestCase):
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
