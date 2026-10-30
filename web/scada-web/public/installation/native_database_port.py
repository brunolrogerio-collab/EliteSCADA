"""Administrative native candidate; profile and credentials are provisioned by the host."""
import json
import os
from pathlib import Path
import re
import socket
import subprocess
import time
import uuid


def wait_database_identity(read_identity, database_name, port):
    """Retry startup failures, but reject a responding unexpected database immediately."""
    deadline = time.monotonic() + 60
    while True:
        try:
            identity = read_identity()
        except (subprocess.SubprocessError, OSError):
            if time.monotonic() >= deadline:
                raise RuntimeError('Database readiness timed out; journal retained') from None
            time.sleep(1)
            continue
        if identity != [database_name, str(port)]:
            raise RuntimeError('Database identity or effective port mismatch; journal retained')
        return


def recover_locked(profile_path):
    """Caller must hold the same installation network lock as execute_locked."""
    profile = json.loads(Path(profile_path).read_text())
    if profile.get('schema') != 'scadivo-native-network-v1' or profile.get('platform') not in ('windows', 'linux'):
        raise ValueError('Registered native profile required')
    topology = json.loads(Path(profile['topologyFile']).read_text())
    if topology.get('schemaVersion') != 1 or topology.get('active', {}).get('mode') != 'LocalManaged' or topology.get('pending') is not None:
        raise ValueError('Idle local topology required for recovery')
    app = Path(profile['configurationFile']).resolve(strict=True)
    pg = Path(profile['database']['configurationFile']).resolve(strict=True)
    journal = app.with_name(app.name + '.database-port-pending.json')
    record = json.loads(journal.read_text())
    if record.get('schema') != 'scadivo-database-port-recovery-v1' or not isinstance(record.get('oldPort'), int) or not 1 <= record['oldPort'] <= 65535:
        raise ValueError('Invalid recovery record')
    restored = []
    for target, key in ((app, 'applicationBackup'), (pg, 'postgresqlBackup')):
        backup = Path(record[key]).resolve(strict=True)
        if backup.parent != target.parent or not backup.name.startswith(target.name + '.network-backup-'):
            raise ValueError('Backup is outside registered configuration directory')
        restored.append((target, backup.read_bytes()))
    json.loads(restored[0][1].decode('utf-8-sig'))
    db = profile['database']
    psql = Path(db['psqlExecutable']).resolve(strict=True)
    # Validate service names before any mutation.
    for name in (profile['serviceName'], db['serviceName']):
        if not re.fullmatch(r'[A-Za-z0-9_.-]+', name):
            raise ValueError('Invalid service identity')
    control_service(profile['platform'], 'stop', profile['serviceName'])
    for target, content in restored:
        target.write_bytes(content)
    control_service(profile['platform'], 'restart', db['serviceName'])
    def read_identity():
        result = subprocess.run([str(psql), '-X', '-w', '-h', '127.0.0.1', '-p', str(record['oldPort']),
                             '-U', db['username'], '-d', db['databaseName'], '-At', '-c',
                             'SELECT current_database(); SHOW port;'],
                            check=True, capture_output=True, text=True, timeout=15)
        return result.stdout.strip().splitlines()
    wait_database_identity(read_identity, db['databaseName'], record['oldPort'])
    control_service(profile['platform'], 'start', profile['serviceName'])
    journal.unlink()
    return 'Previous database configuration restored; application readiness still requires validation'


def control_service(platform, action, name):
    if not re.fullmatch(r'[A-Za-z0-9_.-]+', name):
        raise ValueError('Invalid service identity')
    if action not in ('stop', 'start', 'restart'):
        raise ValueError('Invalid service action')
    if platform == 'linux':
        command = ['systemctl', action, name]
    elif platform == 'windows':
        # Name is restricted above; no credentials or arbitrary commands allowed.
        verb = {'stop':'Stop-Service', 'start':'Start-Service', 'restart':'Restart-Service'}[action]
        state = 'Stopped' if action == 'stop' else 'Running'
        script = (f"$ErrorActionPreference='Stop'; {verb} -Name '{name}' -ErrorAction Stop; "
                  f"(Get-Service -Name '{name}').WaitForStatus('{state}', [TimeSpan]::FromSeconds(30))")
        command = ['powershell.exe', '-NoProfile', '-NonInteractive', '-Command', script]
    else:
        raise ValueError('Unsupported platform')
    subprocess.run(command, check=True, capture_output=True, timeout=45)


def execute(profile_path, port=None, apply=False, recover=False):
    # Linux advisory lock is released by the kernel after process termination.
    # All native administrative operations for this installation must share it.
    import fcntl
    profile = json.loads(Path(profile_path).read_text())
    config = Path(profile['configurationFile']).resolve(strict=True)
    lock_path = config.with_name(config.name + '.network.lock')
    descriptor = os.open(lock_path, os.O_RDWR | os.O_CREAT, 0o600)
    try:
        fcntl.flock(descriptor, fcntl.LOCK_EX | fcntl.LOCK_NB)
        return recover_locked(profile_path) if recover else execute_locked(profile_path, port, apply)
    finally:
        os.close(descriptor)


def execute_locked(profile_path, port=None, apply=False):
    profile = json.loads(Path(profile_path).read_text())
    if profile.get('schema') != 'scadivo-native-network-v1' or profile.get('platform') not in ('linux', 'windows'):
        raise ValueError('Registered native profile required')
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
    journal = app_path.with_name(app_path.name + '.database-port-pending.json')
    if journal.exists():
        raise RuntimeError('An interrupted database port operation requires recovery before another change')
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
        control_service(profile['platform'], action, name)

    def wait_database(at_port):
        wait_database_identity(
            lambda: [sql(at_port, 'SELECT current_database()'), sql(at_port, 'SHOW port')],
            db['databaseName'], at_port)

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
        if os.name == 'nt':
            subprocess.run(['powershell.exe', '-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass', '-File',
                            str(Path(__file__).with_name('copy-native-protected-backup.ps1')),
                            '-SourcePath', str(path), '-DestinationPath', str(backup)],
                           check=True, capture_output=True, timeout=30)
        else:
            with os.fdopen(os.open(backup, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600), 'wb') as output:
                output.write(original)
                output.flush()
                os.fsync(output.fileno())
        backups.append(backup)
    # Persist recovery references before the first disruptive action. No credentials.
    recovery = {'schema': 'scadivo-database-port-recovery-v1', 'oldPort': old_port,
                'newPort': port, 'applicationBackup': str(backups[0]),
                'postgresqlBackup': str(backups[1])}
    if os.name == 'nt':
        subprocess.run(['powershell.exe', '-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass', '-File',
                        str(Path(__file__).with_name('copy-native-protected-backup.ps1')),
                        '-SourcePath', str(app_path), '-DestinationPath', str(journal), '-CreateEmpty'],
                       check=True, capture_output=True, timeout=30)
        descriptor = os.open(journal, os.O_WRONLY)
    else:
        descriptor = os.open(journal, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
    with os.fdopen(descriptor, 'w') as output:
        json.dump(recovery, output)
        output.flush()
        os.fsync(output.fileno())
    try:
        service('stop', api_service)
        app.setdefault('DatabaseTopology', {})['LocalManagedPort'] = port
        app_path.write_text(json.dumps(app, indent=2), encoding='utf-8')
        pg_path.write_bytes(pg_bytes + f'\n# SCADIVO managed local port\nport = {port}\n'.encode())
        service('restart', db_service)
        wait_database(port)
        service('start', api_service)
        journal.unlink()
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
        journal.unlink()
        raise RuntimeError('Change failed; configuration restored and database connection verified') from None
