"""Administrative Linux candidate; profile and credentials are provisioned by the host."""
import json
import os
from pathlib import Path
import re
import socket
import subprocess
import time
import uuid


def execute(profile_path, port=None, apply=False):
    profile = json.loads(Path(profile_path).read_text())
    if profile.get('schema') != 'scadivo-native-network-v1' or profile.get('platform') != 'linux':
        raise ValueError('Registered Linux native profile required')
    topology = json.loads(Path(profile['topologyFile']).read_text())
    if topology.get('schemaVersion') != 1 or topology.get('active', {}).get('mode') != 'LocalManaged' or topology.get('pending') is not None:
        raise ValueError('An idle LocalManaged topology is required')
    db = profile['database']
    api_service, db_service = profile['serviceName'], db['serviceName']
    if any(not re.fullmatch(r'[A-Za-z0-9_.-]+', item) for item in (api_service, db_service)) or api_service == db_service:
        raise ValueError('Distinct service identities required')
    app_path = Path(profile['configurationFile']).resolve(strict=True)
    pg_path = Path(db['configurationFile']).resolve(strict=True)
    psql = Path(db['psqlExecutable']).resolve(strict=True)
    app_bytes, pg_bytes = app_path.read_bytes(), pg_path.read_bytes()
    app = json.loads(app_bytes.decode('utf-8-sig'))
    old_port = int(db['currentPort'])
    port = old_port if port is None else int(port)
    if not 1 <= port <= 65535:
        raise ValueError('Invalid port')

    def sql(at_port, query):
        # Authentication uses the host account/libpq passfile; no password in arguments.
        result = subprocess.run([str(psql), '-X', '-w', '-h', '127.0.0.1', '-p', str(at_port),
                                 '-U', db['username'], '-d', db['databaseName'], '-At', '-c', query],
                                check=True, capture_output=True, text=True, timeout=15)
        return result.stdout.strip()

    def service(action, name):
        subprocess.run(['systemctl', action, name], check=True, capture_output=True, timeout=45)

    def wait_database(at_port):
        deadline = time.monotonic() + 60
        while True:
            try:
                actual_port = sql(at_port, 'SHOW port')
                actual_database = sql(at_port, 'SELECT current_database()')
            except (subprocess.SubprocessError, OSError):
                if time.monotonic() >= deadline:
                    raise RuntimeError('Database readiness timed out') from None
                time.sleep(1)
                continue
            if actual_port != str(at_port) or actual_database != db['databaseName']:
                raise RuntimeError('Database identity or effective port mismatch')
            return

    if Path(sql(old_port, 'SHOW config_file')).resolve() != pg_path or sql(old_port, 'SHOW port') != str(old_port):
        raise ValueError('PostgreSQL effective configuration differs from registered profile')
    if sql(old_port, 'SELECT current_database()') != db['databaseName']:
        raise ValueError('Database identity differs from profile')
    if port == old_port:
        return 'No change required'
    with socket.socket() as probe:
        probe.bind(('0.0.0.0', port))
    if not apply:
        return f'Plan: local PostgreSQL {old_port} -> {port}'
    backups = []
    for path, original in ((app_path, app_bytes), (pg_path, pg_bytes)):
        backup = path.with_name(path.name + '.network-backup-' + uuid.uuid4().hex)
        with os.fdopen(os.open(backup, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600), 'wb') as output:
            output.write(original)
        backups.append(backup)
    try:
        service('stop', api_service)
        app.setdefault('DatabaseTopology', {})['LocalManagedPort'] = port
        app_path.write_text(json.dumps(app, indent=2), encoding='utf-8')
        pg_path.write_bytes(pg_bytes + f'\n# SCADIVO managed local port\nport = {port}\n'.encode())
        service('restart', db_service)
        wait_database(port)
        service('start', api_service)
        return 'Database port applied; application readiness still requires validation'
    except Exception:
        try:
            service('stop', api_service)
            app_path.write_bytes(app_bytes)
            pg_path.write_bytes(pg_bytes)
            service('restart', db_service)
            wait_database(old_port)
            service('start', api_service)
        except Exception:
            raise RuntimeError('Recovery failed; use the protected configuration backups') from None
        raise RuntimeError('Change failed; configuration restored and database connection verified') from None