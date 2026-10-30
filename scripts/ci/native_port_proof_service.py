"""Disposable systemd fixture, not the SCADIVO product host."""
import json
from pathlib import Path
import signal
import sys
import threading
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import urlsplit

config=json.loads(Path(sys.argv[1]).read_text())
class Handler(BaseHTTPRequestHandler):
    def log_message(self,*args):pass
    def do_GET(self):
        self.send_response(200 if self.path=='/health' else 404)
        self.end_headers()
        self.wfile.write(b'{"status":"ok","service":"scada-api"}' if self.path=='/health' else b'{}')
servers=[]
for name in ('Web','Api'):
    port=urlsplit(config['Kestrel']['Endpoints'][name]['Url']).port
    server=ThreadingHTTPServer(('127.0.0.1',port),Handler)
    threading.Thread(target=server.serve_forever,daemon=True).start();servers.append(server)
stopped=threading.Event()
signal.signal(signal.SIGTERM,lambda *args:stopped.set())
stopped.wait()
for server in servers:server.shutdown();server.server_close()
