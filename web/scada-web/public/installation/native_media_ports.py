"""Prepare native MediaMTX/API configuration together; no service mutation here."""
import json
import re
from urllib.parse import urlsplit, urlunsplit


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
