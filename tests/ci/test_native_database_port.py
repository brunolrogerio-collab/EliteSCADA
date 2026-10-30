import importlib.util
import json
from pathlib import Path
import socket
import subprocess
import tempfile
import types
import sys
import unittest
from unittest.mock import patch

source = Path(__file__).resolve().parents[2] / 'web/scada-web/public/installation/native_database_port.py'
spec = importlib.util.spec_from_file_location('native_db', source)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)

class NativeDatabasePortTests(unittest.TestCase):
    def test_readiness_retries_startup_but_rejects_wrong_database(self):
        with patch.object(module.time, 'sleep') as sleep:
            read = unittest.mock.Mock(side_effect=[subprocess.TimeoutExpired('psql', 15), ['plant', '15432']])
            module.wait_database_identity(read, 'plant', 15432)
            self.assertEqual(read.call_count, 2)
            sleep.assert_called_once_with(1)
            sleep.reset_mock()
            read = unittest.mock.Mock(return_value=['other', '15432'])
            with self.assertRaisesRegex(RuntimeError, 'identity'):
                module.wait_database_identity(read, 'plant', 15432)
            read.assert_called_once()
            sleep.assert_not_called()

    def test_readiness_timeout_is_bounded(self):
        read = unittest.mock.Mock(side_effect=OSError('unavailable'))
        with patch.object(module.time, 'monotonic', side_effect=[0, 61]), patch.object(module.time, 'sleep') as sleep:
            with self.assertRaisesRegex(RuntimeError, 'journal retained'):
                module.wait_database_identity(read, 'plant', 15432)
            sleep.assert_not_called()

    def test_windows_service_actions_are_bounded(self):
        with patch.object(module.subprocess, 'run') as command:
            module.control_service('windows', 'restart', 'scadivo-db')
            arguments = command.call_args.args[0]
            self.assertEqual(arguments[:3], ['powershell.exe', '-NoProfile', '-NonInteractive'])
            self.assertIn("Restart-Service -Name 'scadivo-db'", arguments[-1])
            self.assertIn("WaitForStatus('Running'", arguments[-1])
            command.reset_mock()
            with self.assertRaises(ValueError): module.control_service('windows', 'restart', "bad'; command")
            with self.assertRaises(ValueError): module.control_service('windows', 'delete', 'scadivo-db')
            command.assert_not_called()

    def test_concurrent_database_operation_is_rejected_before_mutation(self):
        with tempfile.TemporaryDirectory() as folder:
            config = Path(folder) / 'app.json'
            config.write_text('{}')
            profile = Path(folder) / 'profile.json'
            profile.write_text(json.dumps({'configurationFile':str(config)}))
            fake = types.SimpleNamespace(LOCK_EX=1, LOCK_NB=2, flock=lambda *args: (_ for _ in ()).throw(BlockingIOError()))
            with patch.dict(sys.modules, {'fcntl':fake}), patch.object(module, 'execute_locked') as operation:
                with self.assertRaises(BlockingIOError): module.execute(profile, 25432, True)
                operation.assert_not_called()
            self.assertEqual(config.read_text(), '{}')

    def test_windows_plan_apply_and_recovery_preserve_files_and_order(self):
        self._exercise_flow('windows')

    def test_plan_apply_and_recovery_preserve_files_and_order(self):
        self._exercise_flow('linux')

    def _exercise_flow(self, platform):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            app, pg, topology, psql, profile = [root / name for name in ('app.json','postgresql.conf','topology.json','psql','profile.json')]
            original = b'{"secret":"keep-private"}'
            app.write_bytes(original); pg.write_bytes(b'port = 5432\n'); psql.touch()
            topology.write_text(json.dumps({'schemaVersion':1,'active':{'mode':'LocalManaged'},'pending':None}))
            profile.write_text(json.dumps({'schema':'scadivo-native-network-v1','platform':platform,'topologyFile':str(topology),'configurationFile':str(app),'serviceName':'scadivo.service','database':{'serviceName':'postgresql.service','configurationFile':str(pg),'psqlExecutable':str(psql),'currentPort':5432,'username':'test','databaseName':'plant'}}))
            with socket.socket() as listener:
                listener.bind(('127.0.0.1',0)); port=listener.getsockname()[1]
            actions=[]; fail=False; transient=0; recovery_transient=0; timeout=False; wrong_identity=False; interrupted=False
            def command(args, **kwargs):
                if interrupted and args[0] in ('systemctl', 'powershell.exe'):
                    raise KeyboardInterrupt('simulated process interruption')
                if args[0]=='systemctl':
                    actions.append(args[1:]); return subprocess.CompletedProcess(args,0,'','')
                if args[0]=='powershell.exe':
                    script=args[-1]
                    action='restart' if 'Restart-Service' in script else 'stop' if 'Stop-Service' in script else 'start'
                    name='postgresql.service' if 'postgresql.service' in script else 'scadivo.service'
                    actions.append([action,name])
                    self.assertIn('WaitForStatus',script)
                    return subprocess.CompletedProcess(args,0,'','')
                query=args[-1]; at_port=int(args[args.index('-p')+1])
                nonlocal transient, recovery_transient
                if query == 'SELECT current_database(); SHOW port;' and recovery_transient:
                    recovery_transient -= 1
                    raise subprocess.TimeoutExpired('psql', 15)
                if at_port == port and transient:
                    transient -= 1
                    raise subprocess.CalledProcessError(1, ['psql'])
                if timeout and at_port == port:
                    raise subprocess.CalledProcessError(1, ['psql'])
                if fail and at_port==port:
                    raise RuntimeError('simulated new-port connection failure')
                if wrong_identity and at_port == port and query == 'SELECT current_database()':
                    return subprocess.CompletedProcess(args,0,'wrong-plant','')
                if query == 'SELECT current_database(); SHOW port;':
                    return subprocess.CompletedProcess(args,0,f'plant\n{at_port}\n','')
                result=str(pg) if query=='SHOW config_file' else str(at_port) if query=='SHOW port' else 'plant'
                return subprocess.CompletedProcess(args,0,result,'')
            with patch.object(module.subprocess,'run',side_effect=command), patch.object(module.time,'sleep'):
                pending = app.with_name(app.name + '.database-port-pending.json')
                pending.write_text('{}')
                with self.assertRaisesRegex(RuntimeError, 'interrupted'):
                    module.execute_locked(profile,port,True)
                self.assertEqual(actions, [])
                self.assertEqual(app.read_bytes(), original)
                pending.unlink()
                module.execute_locked(profile,port)
                self.assertEqual(app.read_bytes(),original); self.assertEqual(actions,[])
                transient=2
                module.execute_locked(profile,port,True)
                self.assertEqual(transient,0)
                self.assertFalse(pending.exists())
                self.assertEqual(json.loads(app.read_text())['DatabaseTopology']['LocalManagedPort'],port)
                self.assertEqual(json.loads(app.read_text())['secret'],'keep-private')
                self.assertEqual(actions,[['stop','scadivo.service'],['restart','postgresql.service'],['start','scadivo.service']])
                app.write_bytes(original); pg.write_bytes(b'port = 5432\n'); actions.clear(); fail=True
                with self.assertRaisesRegex(RuntimeError,'configuration restored'):
                    module.execute_locked(profile,port,True)
                self.assertEqual(app.read_bytes(),original); self.assertEqual(pg.read_bytes(),b'port = 5432\n')
                self.assertFalse(pending.exists())
                fail=False; timeout=True; actions.clear()
                with patch.object(module.time, 'monotonic', side_effect=[0, 61, 62]):
                    with self.assertRaisesRegex(RuntimeError, 'configuration restored'):
                        module.execute_locked(profile,port,True)
                self.assertEqual(app.read_bytes(), original)
                self.assertEqual(pg.read_bytes(), b'port = 5432\n')
                timeout=False; wrong_identity=True; actions.clear()
                with self.assertRaisesRegex(RuntimeError, 'configuration restored'):
                    module.execute_locked(profile,port,True)
                self.assertEqual(app.read_bytes(), original)
                self.assertEqual(pg.read_bytes(), b'port = 5432\n')
                wrong_identity=False; interrupted=True
                with self.assertRaises(KeyboardInterrupt): module.execute_locked(profile,port,True)
                recovery=json.loads(pending.read_text())
                self.assertEqual(Path(recovery['applicationBackup']).read_bytes(),original)
                self.assertNotIn('keep-private', pending.read_text())
                with self.assertRaisesRegex(RuntimeError,'interrupted'): module.execute_locked(profile,port,True)
                interrupted=False
                tampered=dict(recovery, applicationBackup=str(psql))
                pending.write_text(json.dumps(tampered))
                before=list(actions)
                with self.assertRaises(ValueError): module.recover_locked(profile)
                self.assertEqual(actions,before)
                self.assertTrue(pending.exists())
                pending.write_text(json.dumps(recovery))
                recovery_transient=2
                module.recover_locked(profile)
                self.assertEqual(recovery_transient,0)
                self.assertFalse(pending.exists())
                self.assertEqual(app.read_bytes(),original)
                self.assertEqual(pg.read_bytes(),b'port = 5432\n')
                topology.write_text(json.dumps({'schemaVersion':1,'active':{'mode':'Remote'},'pending':None}))
                with self.assertRaises(ValueError): module.execute_locked(profile,port,True)
