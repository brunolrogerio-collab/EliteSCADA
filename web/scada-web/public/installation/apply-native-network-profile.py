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


def run(profile_path, port=None, apply=False):
    profile = json.loads(Path(profile_path).read_text(encoding='utf-8-sig'))
    if profile.get('schema') != 'scadivo-native-network-v1' or profile.get('platform') != 'linux':
        raise ValueError('A registered Linux native profile is required')
    service = profile['serviceName']
    if not re.fullmatch(r'[A-Za-z0-9_.-]+', service):
        raise ValueError('Invalid service identity')
    path = Path(profile['configurationFile']).resolve(strict=True)
    original = path.read_bytes()
    config = json.loads(original.decode('utf-8-sig'))
    endpoint = config['Kestrel']['Endpoints'][profile['endpointName']]
    current = urlsplit(endpoint['Url'])
    if current.scheme not in ('http', 'https') or not current.hostname or current.username or current.password or current.query or current.fragment or current.path not in ('', '/'):
        raise ValueError('Invalid native listener URL')
    old_port = current.port or (443 if current.scheme == 'https' else 80)
    port = old_port if port is None else port
    if not 1 <= port <= 65535:
        raise ValueError('Port must be between 1 and 65535')
    subprocess.run(['systemctl', 'is-active', '--quiet', service], check=True)
    if port == old_port:
        print('No port change required')
        return
    with socket.socket() as probe:
        probe.bind(('0.0.0.0', port))
    if not apply:
        print(f'Plan: Web/API port {old_port} -> {port}; use --apply to apply')
        return
    backup = path.with_name(path.name + '.network-backup-' + uuid.uuid4().hex)
    with os.fdopen(os.open(backup, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600), 'wb') as stream:
        stream.write(original)
    host = f'[{current.hostname}]' if ':' in current.hostname else current.hostname
    endpoint['Url'] = urlunsplit((current.scheme, f'{host}:{port}', '', '', ''))
    try:
        # Preserve the protected configuration file's existing owner/mode.
        path.write_text(json.dumps(config, indent=2), encoding='utf-8')
        subprocess.run(['systemctl', 'restart', service], check=True)
        health(f'{current.scheme}://127.0.0.1:{port}/health')
    except Exception:
        try:
            path.write_bytes(original)
            subprocess.run(['systemctl', 'restart', service], check=True)
            health(f'{current.scheme}://127.0.0.1:{old_port}/health')
        except Exception:
            raise RuntimeError(f'Change and recovery failed; restore protected backup {backup}') from None
        raise RuntimeError('Change failed; previous configuration restored and healthy') from None
    print(f'Web/API now use port {port}; protected backup: {backup}')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--profile', required=True)
    parser.add_argument('--web-port', type=int)
    parser.add_argument('--apply', action='store_true')
    args = parser.parse_args()
    try:
        run(args.profile, args.web_port, args.apply)
    except Exception:
        # Never echo configuration or subprocess arguments containing private data.
        raise SystemExit('Native port operation failed. Check service/configuration and protected backup.')