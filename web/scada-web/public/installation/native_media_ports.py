"""Coordinate native MediaMTX/API configuration under a caller-held installation lock."""
import json
import re
from urllib.parse import urlsplit, urlunsplit
from pathlib import Path
import os
import socket
import uuid
import subprocess
import time
from urllib.request import build_opener, ProxyHandler, HTTPRedirectHandler
from urllib.error import HTTPError


class NoRedirect(HTTPRedirectHandler):
    def redirect_request(self, request, fp, code, message, headers, new_url):
        return None


def verify_local_media(application_bytes):
    """Bounded control API and HLS listener check, without proxy or redirects."""
    sources = json.loads(application_bytes)['MediaSources']
    control, playback = [urlsplit(sources[key]) for key in ('GatewayControlUrl', 'GatewayPlaybackUrl')]
    for endpoint in (control, playback):
        if endpoint.scheme not in ('http', 'https') or endpoint.hostname not in ('127.0.0.1', '::1', 'localhost') or not endpoint.port or endpoint.username or endpoint.password or endpoint.query or endpoint.fragment or endpoint.path not in ('', '/'):
            raise ValueError('Registered local media endpoints required')
    opener = build_opener(ProxyHandler({}), NoRedirect())
    deadline = time.monotonic() + 60
    while True:
        try:
            with opener.open(sources['GatewayControlUrl'].rstrip('/') + '/v3/paths/list', timeout=2) as response:
                payload = response.read(1048577)
                if len(payload) > 1048576 or not isinstance(json.loads(payload).get('items'), list):
                    raise ValueError('Unexpected media control response')
            with socket.create_connection((playback.hostname, playback.port), timeout=2):
                pass
            return
        except OSError as failure:
            if isinstance(failure, HTTPError):
                failure.close()
            if time.monotonic() >= deadline:
                raise RuntimeError('Media readiness timed out') from None
            time.sleep(1)


def execute_locked(profile_path, control_port=None, hls_port=None, apply=False, recover=False):
    # Host profile is provisioned by the installer, not supplied by a web request.
    import native_database_port
    profile = json.loads(Path(profile_path).read_text())
    if profile.get('schema') != 'scadivo-native-network-v1' or profile.get('platform') not in ('windows', 'linux'):
        raise ValueError('Registered native profile required')
    content = Path(profile['configurationFile']).read_bytes()
    # Refuse external endpoints before even a plan, service action or recovery.
    endpoints = json.loads(content)['MediaSources']
    for key in ('GatewayControlUrl', 'GatewayPlaybackUrl'):
        if urlsplit(endpoints[key]).hostname not in ('127.0.0.1', '::1', 'localhost'):
            raise ValueError('Native media service must be local')
    service = lambda action, name: native_database_port.control_service(profile['platform'], action, name)
    if recover:
        if apply or control_port is not None or hls_port is not None:
            raise ValueError('Recovery cannot be combined with a port change')
        return recover_locked(profile_path, service, verify_local_media)
    return apply_locked(profile_path, service, verify_local_media, control_port, hls_port, apply)


def main():
    import argparse
    import fcntl
    parser = argparse.ArgumentParser(description='Administrative local media port configuration')
    parser.add_argument('--profile', required=True)
    parser.add_argument('--control-port', type=int)
    parser.add_argument('--hls-port', type=int)
    parser.add_argument('--apply', action='store_true')
    parser.add_argument('--recover', action='store_true')
    args = parser.parse_args()
    profile = json.loads(Path(args.profile).read_text())
    if profile.get('platform') != 'linux':
        raise ValueError('Use the Windows administrative wrapper on Windows')
    config = Path(profile['configurationFile']).resolve(strict=True)
    descriptor = os.open(config.with_name(config.name + '.network.lock'), os.O_RDWR | os.O_CREAT, 0o600)
    try:
        fcntl.flock(descriptor, fcntl.LOCK_EX | fcntl.LOCK_NB)
        print(execute_locked(args.profile, args.control_port, args.hls_port, args.apply, args.recover))
    finally:
        os.close(descriptor)


def recover_locked(profile_path, service_control, verify):
    """Restore both registered configurations after an interrupted operation."""
    profile = json.loads(Path(profile_path).read_text())
    if profile.get('schema') != 'scadivo-native-network-v1' or profile.get('platform') not in ('windows', 'linux'):
        raise ValueError('Registered native profile required')
    app = Path(profile['configurationFile']).resolve(strict=True)
    gateway = Path(profile['media']['configurationFile']).resolve(strict=True)
    api_service, media_service = profile['serviceName'], profile['media']['serviceName']
    if app == gateway or api_service == media_service or any(not re.fullmatch(r'[A-Za-z0-9_.-]+', name) for name in (api_service, media_service)):
        raise ValueError('Distinct registered files and services required')
    journal = app.with_name(app.name + '.media-port-pending.json')
    record = json.loads(journal.read_text())
    backups = record.get('backups')
    if record.get('schema') != 'scadivo-media-port-recovery-v1' or not isinstance(backups, list) or len(backups) != 2:
        raise ValueError('Invalid media recovery record')
    original = []
    for target, reference in zip((app, gateway), backups):
        backup = Path(reference).resolve(strict=True)
        if backup.parent != target.parent or not backup.name.startswith(target.name + '.network-backup-'):
            raise ValueError('Backup is outside registered configuration directory')
        original.append(backup.read_bytes())
    # Validate the complete pair before stopping any service or writing either file.
    prepare(*original)
    service_control('stop', api_service)
    app.write_bytes(original[0]); gateway.write_bytes(original[1])
    service_control('restart', media_service)
    verify(original[0])
    service_control('start', api_service)
    journal.unlink()
    return 'Previous media configuration restored; application and playback acceptance still required'


def prepare(application_bytes, media_bytes, control_port=None, hls_port=None):
    application = json.loads(application_bytes.decode('utf-8-sig'))
    media = media_bytes.decode('utf-8-sig')
    sources = application['MediaSources']
    changes = []
    ports = []
    for key, url_key, requested in (
        ('apiAddress', 'GatewayControlUrl', control_port),
        ('hlsAddress', 'GatewayPlaybackUrl', hls_port),
    ):
        # Work only with the registered root scalar, never a similarly named
        # nested value. Complex/duplicate YAML must be handled explicitly.
        matches = list(re.finditer(r'^' + key + r':\s*([^\r\n#]+)(?:\s*#[^\r\n]*)?$', media, re.MULTILINE))
        if len(matches) != 1:
            raise ValueError('A unique root media listener is required')
        listener = matches[0].group(1).strip().strip('"\'')
        address = re.fullmatch(r'(.*):([0-9]+)', listener)
        if not address:
            raise ValueError('Unsupported media listener address')
        current = int(address.group(2))
        target = current if requested is None else requested
        if not isinstance(target, int) or isinstance(target, bool) or not 1 <= target <= 65535:
            raise ValueError('Invalid media port')
        url = urlsplit(sources[url_key])
        if url.scheme not in ('http', 'https') or not url.hostname or url.username or url.password or url.query or url.fragment or url.path not in ('', '/'):
            raise ValueError('Unsupported media relay URL')
        if url.port != current:
            raise ValueError('Media listener and API relay port differ before the change')
        ports.append(target)
        changes.append((matches[0].span(), f'{key}: "{address.group(1)}:{target}"'))
        host = f'[{url.hostname}]' if ':' in url.hostname else url.hostname
        sources[url_key] = urlunsplit((url.scheme, f'{host}:{target}', '', '', ''))
    if len(set(ports)) != len(ports):
        raise ValueError('Media control and HLS ports must differ')
    if control_port is None and hls_port is None:
        return application_bytes, media_bytes
    for (start, end), replacement in reversed(changes):
        media = media[:start] + replacement + media[end:]
    return json.dumps(application, indent=2).encode('utf-8'), media.encode('utf-8')


def apply_locked(profile_path, service_control, verify, control_port=None, hls_port=None, apply=False):
    """Administrative caller holds the installation lock; callbacks own OS/health."""
    profile = json.loads(Path(profile_path).read_text())
    if profile.get('schema') != 'scadivo-native-network-v1' or profile.get('platform') not in ('windows', 'linux'):
        raise ValueError('Registered native profile required')
    app = Path(profile['configurationFile']).resolve(strict=True)
    gateway = Path(profile['media']['configurationFile']).resolve(strict=True)
    if app == gateway:
        raise ValueError('Distinct configuration files required')
    api_service, media_service = profile['serviceName'], profile['media']['serviceName']
    if api_service == media_service or any(not re.fullmatch(r'[A-Za-z0-9_.-]+', name) for name in (api_service, media_service)):
        raise ValueError('Distinct registered service identities required')
    journal = app.with_name(app.name + '.media-port-pending.json')
    if journal.exists():
        raise RuntimeError('Interrupted media operation requires recovery')
    original = app.read_bytes(), gateway.read_bytes()
    updated = prepare(*original, control_port, hls_port)
    if updated == original:
        return 'No change required'
    # Preflight only changed listeners. The callback checks actual service readiness.
    previous = json.loads(original[0])['MediaSources']
    desired = json.loads(updated[0])['MediaSources']
    for key in ('GatewayControlUrl', 'GatewayPlaybackUrl'):
        old, new = urlsplit(previous[key]), urlsplit(desired[key])
        if old.port != new.port:
            with socket.socket() as listener:
                listener.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
                listener.bind(('0.0.0.0', new.port))
                listener.listen(1)
    if not apply:
        return 'Media/API configuration plan validated'
    backups = []
    for target, content in zip((app, gateway), original):
        backup = target.with_name(target.name + '.network-backup-' + uuid.uuid4().hex)
        if os.name == 'nt':
            subprocess.run(['powershell.exe', '-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass', '-File',
                            str(Path(__file__).with_name('copy-native-protected-backup.ps1')),
                            '-SourcePath', str(target), '-DestinationPath', str(backup)],
                           check=True, capture_output=True, timeout=30)
        else:
            with os.fdopen(os.open(backup, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600), 'wb') as output:
                output.write(content); output.flush(); os.fsync(output.fileno())
        backups.append(str(backup))
    with os.fdopen(os.open(journal, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600), 'w') as output:
        json.dump({'schema':'scadivo-media-port-recovery-v1', 'backups':backups}, output)
        output.flush(); os.fsync(output.fileno())
    try:
        service_control('stop', api_service)
        app.write_bytes(updated[0]); gateway.write_bytes(updated[1])
        service_control('restart', media_service)
        verify(updated[0])
        service_control('start', api_service)
    except Exception:
        try:
            service_control('stop', api_service)
            app.write_bytes(original[0]); gateway.write_bytes(original[1])
            service_control('restart', media_service)
            verify(original[0])
            service_control('start', api_service)
        except Exception:
            raise RuntimeError('Media recovery failed; protected journal retained') from None
        journal.unlink()
        raise RuntimeError('Media change failed; previous configuration restored') from None
    journal.unlink()
    return 'Media configuration applied; application and playback acceptance still required'


if __name__ == '__main__':
    try:
        main()
    except Exception:
        raise SystemExit('Native media operation failed. Check protected backups and service diagnostics.')
