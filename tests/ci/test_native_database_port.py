import importlib.util
import json
from pathlib import Path
import socket
import subprocess
import tempfile
import unittest
from unittest.mock import patch

source = Path(__file__).resolve().parents[2] / 'web/scada-web/public/installation/native_database_port.py'
spec = importlib.util.spec_from_file_location('native_db', source)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)

class NativeDatabasePortTests(unittest.TestCase):
    def test_plan_apply_and_recovery_preserve_files_and_order(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            app, pg, topology, psql, profile = [root / name for name in ('app.json','postgresql.conf','topology.json','psql','profile.json')]
            original = b'{"secret":"keep-private"}'
            app.write_bytes(original); pg.write_bytes(b'port = 5432\n'); psql.touch()
            topology.write_text(json.dumps({'schemaVersion':1,'active':{'mode':'LocalManaged'},'pending':None}))
            profile.write_text(json.dumps({'schema':'scadivo-native-network-v1','platform':'linux','topologyFile':str(topology),'configurationFile':str(app),'serviceName':'scadivo.service','database':{'serviceName':'postgresql.service','configurationFile':str(pg),'psqlExecutable':str(psql),'currentPort':5432,'username':'test','databaseName':'plant'}}))
            with socket.socket() as listener:
                listener.bind(('127.0.0.1',0)); port=listener.getsockname()[1]
            actions=[]; fail=False
            def command(args, **kwargs):
                if args[0]=='systemctl':
                    actions.append(args[1:]); return subprocess.CompletedProcess(args,0,'','')
                query=args[-1]; at_port=int(args[args.index('-p')+1])
                if fail and at_port==port:
                    raise RuntimeError('simulated new-port connection failure')
                result=str(pg) if query=='SHOW config_file' else str(at_port) if query=='SHOW port' else 'plant'
                return subprocess.CompletedProcess(args,0,result,'')
            with patch.object(module.subprocess,'run',side_effect=command):
                module.execute(profile,port)
                self.assertEqual(app.read_bytes(),original); self.assertEqual(actions,[])
                module.execute(profile,port,True)
                self.assertEqual(json.loads(app.read_text())['DatabaseTopology']['LocalManagedPort'],port)
                self.assertEqual(json.loads(app.read_text())['secret'],'keep-private')
                self.assertEqual(actions,[['stop','scadivo.service'],['restart','postgresql.service'],['start','scadivo.service']])
                app.write_bytes(original); pg.write_bytes(b'port = 5432\n'); actions.clear(); fail=True
                with self.assertRaisesRegex(RuntimeError,'configuration restored'):
                    module.execute(profile,port,True)
                self.assertEqual(app.read_bytes(),original); self.assertEqual(pg.read_bytes(),b'port = 5432\n')
                topology.write_text(json.dumps({'schemaVersion':1,'active':{'mode':'Remote'},'pending':None}))
                with self.assertRaises(ValueError): module.execute(profile,port,True)