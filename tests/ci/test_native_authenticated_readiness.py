import importlib.util
import json
from pathlib import Path
import threading
from http.server import BaseHTTPRequestHandler,ThreadingHTTPServer
import unittest
from unittest.mock import patch

source=Path(__file__).resolve().parents[2]/'web/scada-web/public/installation/verify_native_readiness.py'
spec=importlib.util.spec_from_file_location('native_postflight',source)
module=importlib.util.module_from_spec(spec); spec.loader.exec_module(module)


class NativeAuthenticatedReadinessTests(unittest.TestCase):
    def test_authenticated_success_retry_refusal_and_redirect(self):
        class Handler(BaseHTTPRequestHandler):
            code=200; calls=0; authorization=None
            payload={'ready':True,'restartRequired':False,'recoveryRequired':False}
            def log_message(self,*args): pass
            def do_GET(self):
                Handler.calls+=1; Handler.authorization=self.headers.get('Authorization')
                self.send_response(Handler.code)
                if Handler.code==302:self.send_header('Location','/unexpected')
                self.end_headers(); self.wfile.write(json.dumps(Handler.payload).encode())
        server=ThreadingHTTPServer(('127.0.0.1',0),Handler)
        thread=threading.Thread(target=server.serve_forever,daemon=True);thread.start()
        origin=f'http://127.0.0.1:{server.server_port}'
        try:
            self.assertIn('verified',module.verify(origin,'temporary-fixture-token'))
            self.assertEqual(Handler.authorization,'Bearer temporary-fixture-token')
            for code in (401,403,302):
                Handler.code=code;before=Handler.calls
                with self.assertRaises(RuntimeError) as failure:module.verify(origin,'temporary-fixture-token')
                self.assertNotIn('temporary-fixture-token',str(failure.exception))
                self.assertEqual(Handler.calls,before+1)
            Handler.code=503
            with patch.object(module.time,'monotonic',side_effect=[0,61]),patch.object(module.time,'sleep'):
                with self.assertRaisesRegex(RuntimeError,'timed out'):module.verify(origin,'temporary-fixture-token')
            Handler.code=200;Handler.payload={'ready':True,'restartRequired':True,'recoveryRequired':False}
            with patch.object(module.time,'monotonic',side_effect=[0,61]):
                with self.assertRaisesRegex(RuntimeError,'timed out'):module.verify(origin,'temporary-fixture-token')
            before=Handler.calls
            with self.assertRaises(ValueError):module.verify(origin,'')
            with self.assertRaises(ValueError):module.verify('http://example.com','temporary-fixture-token')
            self.assertEqual(Handler.calls,before)
        finally:
            server.shutdown();server.server_close();thread.join()
