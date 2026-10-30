import argparse
import json
import os
from pathlib import Path
import re
import socket
import subprocess
import time
from urllib.parse import urlsplit, urlunsplit
from urllib.request import urlopen
import uuid


def health(url):
    for _ in range(30):
        try:
            with urlopen(url, timeout=2) as response:
                if response.status == 200:
                    return
        except Exception:
            pass
        time.sleep(1)
    raise RuntimeError('Service health check failed')


def run(profile_path, port=None, apply=False, api_port=None, recover=False):
    import fcntl
    profile = json.loads(Path(profile_path).read_text(encoding='utf-8-sig'))
    config = Path(profile['configurationFile']).resolve(strict=True)
    lock = config.with_name(config.name + '.network.lock')
    descriptor = os.open(lock, os.O_RDWR | os.O_CREAT, 0o600)
    try:
        fcntl.flock(descriptor, fcntl.LOCK_EX | fcntl.LOCK_NB)
        if recover and (port is not None or api_port is not None or apply):
            raise ValueError('Recovery cannot be combined with a port change')
        return recover_locked(profile_path) if recover else run_locked(profile_path, port, apply, api_port)
    finally:
        os.close(descriptor)


def recover_locked(profile_path):
    profile = json.loads(Path(profile_path).read_text(encoding='utf-8-sig'))
    if profile.get('schema') != 'scadivo-native-network-v1' or profile.get('platform') != 'linux' or not re.fullmatch(r'[A-Za-z0-9_.-]+', profile['serviceName']):
        raise ValueError('Registered native profile required')
    path = Path(profile['configurationFile']).resolve(strict=True)
    journal = path.with_name(path.name + '.access-port-pending.json')
    record = json.loads(journal.read_text())
    if record.get('schema') != 'scadivo-access-port-recovery-v1':
        raise ValueError('Invalid recovery record')
    backup = Path(record['backup']).resolve(strict=True)
    if backup.parent != path.parent or not backup.name.startswith(path.name + '.network-backup-'):
        raise ValueError('Backup is outside registered configuration directory')
    original = backup.read_bytes()
    config = json.loads(original.decode('utf-8-sig'))
    listeners = []
    for name in [profile['endpointName']] + ([profile['apiEndpointName']] if profile.get('apiEndpointName') else []):
        uri = urlsplit(config['Kestrel']['Endpoints'][name]['Url'])
        if uri.scheme not in ('http', 'https') or not uri.hostname or uri.username or uri.password or uri.query or uri.fragment or uri.path not in ('', '/'):
            raise ValueError('Invalid backup listener')
        listeners.append(uri)
    path.write_bytes(original)
    subprocess.run(['systemctl', 'restart', profile['serviceName']], check=True)
    for uri in listeners:
        health(f'{uri.scheme}://127.0.0.1:{uri.port or (443 if uri.scheme == "https" else 80)}/health')
    journal.unlink()
    return 'Previous access configuration restored and health checks passed'


def run_locked(profile_path, port=None, apply=False, api_port=None):
    profile = json.loads(Path(profile_path).read_text(encoding='utf-8-sig'))
    if profile.get('schema') != 'scadivo-native-network-v1' or profile.get('platform') != 'linux':
        raise ValueError('A registered Linux native profile is required')
    service = profile['serviceName']
    if not re.fullmatch(r'[A-Za-z0-9_.-]+', service):
        raise ValueError('Invalid service identity')
    path = Path(profile['configurationFile']).resolve(strict=True)
    journal = path.with_name(path.name + '.access-port-pending.json')
    if journal.exists():
        raise RuntimeError('Interrupted access port operation requires recovery')
    original = path.read_bytes()
    config = json.loads(original.decode('utf-8-sig'))
    def read_listener(name, requested):
        endpoint = config['Kestrel']['Endpoints'][name]
        current = urlsplit(endpoint['Url'])
        if current.scheme not in ('http', 'https') or not current.hostname or current.username or current.password or current.query or current.fragment or current.path not in ('', '/'):
            raise ValueError('Invalid native listener URL')
        old = current.port or (443 if current.scheme == 'https' else 80)
        target = old if requested is None else requested
        if not 1 <= target <= 65535:
            raise ValueError('Port must be between 1 and 65535')
        return endpoint, current, old, target
    listeners = [read_listener(profile['endpointName'], port)]
    if profile.get('apiEndpointName'):
        if profile['apiEndpointName'] == profile['endpointName']:
            raise ValueError('Distinct Web/API endpoints required')
        listeners.append(read_listener(profile['apiEndpointName'], api_port))
        if listeners[0][3] == listeners[1][3]:
            raise ValueError('Web/API access ports must differ')
    elif api_port is not None:
        raise ValueError('Independent API endpoint is not registered')
    subprocess.run(['systemctl', 'is-active', '--quiet', service], check=True)
    if all(old == target for _, _, old, target in listeners):
        print('No port change required')
        return
    for _, _, old, target in listeners:
        if old != target:
            with socket.socket() as probe:
                probe.bind(('0.0.0.0', target))
    if not apply:
        print('Plan: ' + ', '.join(f'{old} -> {target}' for _, _, old, target in listeners) + '; use --apply to apply')
        return
    backup = path.with_name(path.name + '.network-backup-' + uuid.uuid4().hex)
    with os.fdopen(os.open(backup, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600), 'wb') as stream:
        stream.write(original)
        stream.flush(); os.fsync(stream.fileno())
    with os.fdopen(os.open(journal, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600), 'w') as stream:
        json.dump({'schema':'scadivo-access-port-recovery-v1','backup':str(backup)},stream)
        stream.flush(); os.fsync(stream.fileno())
    for endpoint, current, _, target in listeners:
        host = f'[{current.hostname}]' if ':' in current.hostname else current.hostname
        endpoint['Url'] = urlunsplit((current.scheme, f'{host}:{target}', '', '', ''))
    try:
        # Preserve the protected configuration file's existing owner/mode.
        path.write_text(json.dumps(config, indent=2), encoding='utf-8')
        subprocess.run(['systemctl', 'restart', service], check=True)
        for _, current, _, target in listeners:
            health(f'{current.scheme}://127.0.0.1:{target}/health')
    except Exception:
        try:
            path.write_bytes(original)
            subprocess.run(['systemctl', 'restart', service], check=True)
            for _, current, old, _ in listeners:
                health(f'{current.scheme}://127.0.0.1:{old}/health')
        except Exception:
            raise RuntimeError(f'Change and recovery failed; restore protected backup {backup}') from None
        journal.unlink()
        raise RuntimeError('Change failed; previous configuration restored and healthy') from None
    journal.unlink()
    print(f'Native access ports applied; protected backup: {backup}')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--profile', required=True)
    parser.add_argument('--web-port', type=int)
    parser.add_argument('--api-port', type=int)
    parser.add_argument('--apply', action='store_true')
    parser.add_argument('--recover', action='store_true')
    args = parser.parse_args()
    try:
        run(args.profile, args.web_port, args.apply, args.api_port, args.recover)
    except Exception:
        # Never echo configuration or subprocess arguments containing private data.
        raise SystemExit('Native port operation failed. Check service/configuration and protected backup.')
