"""Prepare native MediaMTX/API configuration together; no service mutation here."""
import json
import re
from urllib.parse import urlsplit, urlunsplit
from pathlib import Path
import os
import socket
import uuid


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
                listener.bind(('0.0.0.0', new.port))
    if not apply:
        return 'Media/API configuration plan validated'
    backups = []
    for target, content in zip((app, gateway), original):
        backup = target.with_name(target.name + '.network-backup-' + uuid.uuid4().hex)
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
