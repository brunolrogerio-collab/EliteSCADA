import importlib.util
import json
import os
from pathlib import Path
import socket
import subprocess
import tempfile
import time
import unittest
from urllib.request import urlopen
from urllib.error import HTTPError
from urllib.parse import urlsplit


class NativeMediaIntegrationTests(unittest.TestCase):
    @unittest.skipUnless(os.environ.get('SCADIVO_MEDIA_BINARY'), 'MediaMTX binary unavailable')
    def test_real_control_and_hls_port_change_and_rollback(self):
        module_path=Path(__file__).resolve().parents[2]/'web/scada-web/public/installation/native_media_ports.py'
        spec=importlib.util.spec_from_file_location('media_real',module_path)
        module=importlib.util.module_from_spec(spec); spec.loader.exec_module(module)
        def port():
            with socket.socket() as listener:
                listener.bind(('127.0.0.1',0)); return listener.getsockname()[1]
        ports=set()
        while len(ports)<4: ports.add(port())
        control,hls,new_control,new_hls=ports
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory); app=root/'app.json'; config=root/'media.yml'; profile=root/'profile.json'
            app.write_text(json.dumps({'MediaSources':{'GatewayControlUrl':f'http://127.0.0.1:{control}','GatewayPlaybackUrl':f'http://127.0.0.1:{hls}'}}))
            config.write_text(f'logLevel: error\napi: true\napiAddress: 127.0.0.1:{control}\nhls: true\nhlsAddress: 127.0.0.1:{hls}\nrtsp: false\nrtmp: false\nsrt: false\nwebrtc: false\nmoq: false\npaths: {{}}\n')
            original=app.read_bytes(),config.read_bytes()
            profile.write_text(json.dumps({'schema':'scadivo-native-network-v1','platform':'linux','configurationFile':str(app),'serviceName':'api','media':{'configurationFile':str(config),'serviceName':'media'}}))
            process=None
            log=(root/'media.log').open('w+')
            def service(action,name):
                nonlocal process
                if name=='api': return  # Explicitly simulated API lifecycle.
                if process:
                    process.terminate(); process.wait(timeout=10)
                process=subprocess.Popen([os.environ['SCADIVO_MEDIA_BINARY'],str(config)],stdout=log,stderr=log)
            def verify(content):
                sources=json.loads(content)['MediaSources']
                for _ in range(50):
                    try:
                        with urlopen(sources['GatewayControlUrl']+'/v3/paths/list',timeout=1) as response:
                            self.assertIn('items',json.load(response))
                        hls_endpoint=urlsplit(sources['GatewayPlaybackUrl'])
                        with socket.create_connection((hls_endpoint.hostname,hls_endpoint.port),timeout=1): pass
                        return
                    except (OSError,HTTPError): time.sleep(.1)
                log.flush()
                raise RuntimeError('Real media listeners not ready')
            try:
                service('restart','media'); verify(app.read_bytes())
                module.apply_locked(profile,service,verify,new_control,new_hls,True)
                verify(app.read_bytes())
                # Reset to original, then force new-endpoint verification failure.
                app.write_bytes(original[0]);config.write_bytes(original[1]);service('restart','media');verify(original[0])
                def fail_new(content):
                    if json.loads(content)['MediaSources']['GatewayControlUrl'].endswith(str(new_control)):
                        raise RuntimeError('Simulated acceptance failure')
                    verify(content)
                with self.assertRaisesRegex(RuntimeError,'previous configuration restored'):
                    module.apply_locked(profile,service,fail_new,new_control,new_hls,True)
                self.assertEqual((app.read_bytes(),config.read_bytes()),original)
                verify(app.read_bytes())
            finally:
                if process: process.terminate();process.wait(timeout=10)
                log.close()
