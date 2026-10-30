"""Real PostgreSQL listener test; API service control is simulated explicitly."""
import importlib.util
import json
import os
from pathlib import Path
import socket
import subprocess
import tempfile
import unittest


def find_bin():
    configured = os.environ.get('SCADIVO_NATIVE_PG_BIN')
    candidates = [Path(configured)] if configured else sorted(Path('/usr/lib/postgresql').glob('*/bin'), reverse=True)
    for candidate in candidates:
        if all((candidate / tool).is_file() for tool in ('initdb', 'pg_ctl', 'psql')):
            return candidate
    return None


class NativePostgreSqlPortIntegrationTests(unittest.TestCase):
    @unittest.skipUnless(os.name == 'posix' and find_bin(), 'Native PostgreSQL binaries unavailable')
    def test_actual_postgresql_port_change_and_recovery(self):
        binaries = find_bin()
        module_path = Path(__file__).resolve().parents[2] / 'web/scada-web/public/installation/native_database_port.py'
        spec = importlib.util.spec_from_file_location('native_pg_real', module_path)
        adapter = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(adapter)
        def free_port():
            with socket.socket() as listener:
                listener.bind(('127.0.0.1', 0))
                return listener.getsockname()[1]
        with tempfile.TemporaryDirectory(prefix='scadivo-native-pg-') as temporary:
            root = Path(temporary)
            data = root / 'data'
            old_port, new_port = free_port(), free_port()
            while new_port == old_port:
                new_port = free_port()
            def pg(*args):
                return subprocess.run([str(binaries / 'pg_ctl'), '-D', str(data), '-l', str(root / 'server.log'), '-w', *args], check=True, capture_output=True, text=True, timeout=45)
            subprocess.run([str(binaries / 'initdb'), '-D', str(data), '-A', 'trust', '-U', 'scadivo_test', '--no-locale'], check=True, capture_output=True, timeout=45)
            configuration = data / 'postgresql.conf'
            with configuration.open('a') as output:
                output.write(f"\nlisten_addresses = '127.0.0.1'\nport = {old_port}\nunix_socket_directories = '{root}'\n")
            app = root / 'app.json'
            app.write_text('{"secret":"preserved"}')
            topology = root / 'topology.json'
            topology.write_text(json.dumps({'schemaVersion':1,'active':{'mode':'LocalManaged'},'pending':None}))
            profile = root / 'profile.json'
            profile.write_text(json.dumps({'schema':'scadivo-native-network-v1','platform':'linux','serviceName':'test-api','configurationFile':str(app),'topologyFile':str(topology),'database':{'serviceName':'test-pg','configurationFile':str(configuration),'psqlExecutable':str(binaries / 'psql'),'currentPort':old_port,'username':'scadivo_test','databaseName':'postgres'}}))
            api_actions = []
            fail_next_api_start = False
            def service(platform, action, name):
                nonlocal fail_next_api_start
                if name == 'test-pg':
                    pg(action)
                else:
                    api_actions.append(action)
                    if action == 'start' and fail_next_api_start:
                        fail_next_api_start = False
                        raise RuntimeError('Simulated API start failure')
            adapter.control_service = service
            started = False
            try:
                pg('start'); started = True
                adapter.execute(profile, new_port, True)
                self.assertEqual(api_actions, ['stop', 'start'])
                self.assertEqual(json.loads(app.read_text())['DatabaseTopology']['LocalManagedPort'],new_port)
                # Reuse the unchanged installation profile after a successful change.
                # The effective persisted application setting now owns the port.
                self.assertEqual(adapter.execute(profile), 'No change required')
                self.assertEqual(adapter.execute(profile, new_port, True), 'No change required')
                self.assertEqual(api_actions, ['stop', 'start'])
                # Restore through the recovery path using the retained real backups.
                backups = sorted(root.glob('app.json.network-backup-*'))
                pg_backups = sorted(data.glob('postgresql.conf.network-backup-*'))
                journal = app.with_name(app.name + '.database-port-pending.json')
                journal.write_text(json.dumps({'schema':'scadivo-database-port-recovery-v1','oldPort':old_port,'newPort':new_port,'applicationBackup':str(backups[0]),'postgresqlBackup':str(pg_backups[0])}))
                adapter.recover_locked(profile)
                self.assertEqual(app.read_text(),'{"secret":"preserved"}')
                self.assertFalse(journal.exists())
                fail_next_api_start = True
                with self.assertRaisesRegex(RuntimeError, 'configuration restored'):
                    adapter.execute(profile, new_port, True)
                self.assertEqual(app.read_text(), '{"secret":"preserved"}')
                self.assertFalse(journal.exists())
                result = subprocess.run([str(binaries / 'psql'), '-X', '-w', '-h', '127.0.0.1', '-p', str(old_port), '-U', 'scadivo_test', '-d', 'postgres', '-At', '-c', 'SHOW port'], check=True, capture_output=True, text=True)
                self.assertEqual(result.stdout.strip(), str(old_port))
            finally:
                if started:
                    pg('stop')
