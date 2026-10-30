"""Deployment component: creates an access profile, never installs services."""
import argparse
import json
import os
from pathlib import Path
import re
import subprocess
import uuid
import importlib.util
from urllib.parse import urlsplit


def provision(configuration, service, destination, register=False, media_configuration=None, media_service=None, database_registration=None):
    import fcntl
    if not re.fullmatch(r'[A-Za-z0-9_.-]+', service):
        raise ValueError('Invalid service identity')
    config = Path(configuration).resolve(strict=True)
    target = Path(destination).absolute()
    if target.parent != config.parent:
        raise ValueError('Profile must remain in the registered configuration directory')
    descriptor = os.open(config.with_name(config.name + '.network.lock'), os.O_RDWR | os.O_CREAT, 0o600)
    try:
        fcntl.flock(descriptor, fcntl.LOCK_EX | fcntl.LOCK_NB)
        for operation in ('access', 'database', 'media'):
            if config.with_name(config.name + '.' + operation + '-port-pending.json').exists():
                raise RuntimeError('Complete pending recovery before provisioning')
        # Existence is necessary; the deployment executor must also verify that
        # this service actually loads the declared external configuration file.
        result = subprocess.run(['systemctl', 'show', '--property=LoadState', '--value', service],
                                check=True, capture_output=True, text=True, timeout=15)
        if result.stdout.strip() != 'loaded':
            raise ValueError('Registered service is not loaded')
        original = config.read_bytes()
        document = json.loads(original.decode('utf-8-sig'))
        ports = []
        for name in ('Web', 'Api'):
            uri = urlsplit(document['Kestrel']['Endpoints'][name]['Url'])
            if uri.scheme not in ('http', 'https') or not uri.hostname or uri.username or uri.password or uri.query or uri.fragment or uri.path not in ('', '/'):
                raise ValueError('Valid Web and Api listeners required')
            port = uri.port or (443 if uri.scheme == 'https' else 80)
            if not 1 <= port <= 65535:
                raise ValueError('Invalid listener port')
            ports.append(port)
        if ports[0] == ports[1]:
            raise ValueError('Web and API ports must differ')
        profile = dict(schema='scadivo-native-network-v1', platform='linux', configurationFile=str(config),
                       serviceName=service, endpointName='Web', apiEndpointName='Api')
        if (media_configuration is None) != (media_service is None):
            raise ValueError('Media configuration and service must be supplied together')
        if media_configuration is not None:
            media_path=Path(media_configuration).resolve(strict=True)
            if media_path == config or media_service == service or not re.fullmatch(r'[A-Za-z0-9_.-]+',media_service):
                raise ValueError('Distinct media configuration and service required')
            state=subprocess.run(['systemctl','show','--property=LoadState','--value',media_service],check=True,capture_output=True,text=True,timeout=15)
            if state.stdout.strip() != 'loaded': raise ValueError('Media service is not loaded')
            spec=importlib.util.spec_from_file_location('native_media_profile_validation',Path(__file__).with_name('native_media_ports.py'))
            media=importlib.util.module_from_spec(spec); spec.loader.exec_module(media)
            media.prepare(original,media_path.read_bytes())
            for key in ('GatewayControlUrl','GatewayPlaybackUrl'):
                if urlsplit(document['MediaSources'][key]).hostname not in ('127.0.0.1','::1','localhost'):
                    raise ValueError('Native media endpoints must be local')
            profile['media']={'configurationFile':str(media_path),'serviceName':media_service}
        if database_registration is not None:
            declaration=json.loads(Path(database_registration).read_text(encoding='utf-8-sig'))
            required={'topologyFile','configurationFile','psqlExecutable','serviceName','currentPort','username','databaseName'}
            if set(declaration) != required: raise ValueError('Only declared database registration fields are allowed; credentials are not accepted')
            topology_path=Path(declaration['topologyFile']).resolve(strict=True)
            topology=json.loads(topology_path.read_text(encoding='utf-8-sig'))
            if topology.get('schemaVersion') != 1 or topology.get('active',{}).get('mode') != 'LocalManaged' or topology.get('pending') is not None:
                raise ValueError('Idle local database topology required')
            pg=Path(declaration['configurationFile']).resolve(strict=True)
            psql=Path(declaration['psqlExecutable']).resolve(strict=True)
            if not pg.is_file() or not psql.is_file() or pg == config or (media_configuration is not None and pg == media_path) or psql in (config,pg) or declaration['serviceName'] in (service,media_service) or not re.fullmatch(r'[A-Za-z0-9_.-]+',declaration['serviceName']):
                raise ValueError('Distinct registered local database service and files required')
            port=declaration['currentPort']
            if type(port) is not int or not 1 <= port <= 65535 or document.get('DatabaseTopology',{}).get('LocalManagedPort',port) != port:
                raise ValueError('Database registration port differs from configuration')
            if any(not isinstance(declaration[key],str) or not declaration[key].strip() or declaration[key].startswith('-') for key in ('username','databaseName')):
                raise ValueError('Database account and identity required')
            state=subprocess.run(['systemctl','show','--property=LoadState','--value',declaration['serviceName']],check=True,capture_output=True,text=True,timeout=15)
            if state.stdout.strip() != 'loaded': raise ValueError('Database service not loaded')
            profile['topologyFile']=str(topology_path)
            profile['database']={key:value for key,value in declaration.items() if key != 'topologyFile'}
            profile['database'].update(configurationFile=str(pg),psqlExecutable=str(psql))
        with os.fdopen(os.open(target, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600), 'w') as output:
            json.dump(profile, output, indent=2)
            output.flush(); os.fsync(output.fileno())
        if register:
            backup = config.with_name(config.name + '.network-backup-' + uuid.uuid4().hex)
            with os.fdopen(os.open(backup, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600), 'wb') as output:
                output.write(original); output.flush(); os.fsync(output.fileno())
            try:
                installation = document.setdefault('InstallationNetwork', {})
                installation['Profile'] = 'native-linux'
                installation['AdministrativeProfilePath'] = str(target)
                # Existing file identity/mode is retained. No service restart.
                with config.open('w', encoding='utf-8') as output:
                    json.dump(document, output, indent=2)
                    output.flush(); os.fsync(output.fileno())
            except Exception:
                config.write_bytes(original)
                raise
            return 'Protected access profile registered; service reload and account access still require verification'
        return 'Protected access profile created; deployment registration still requires verification'
    finally:
        os.close(descriptor)


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--configuration', required=True)
    parser.add_argument('--service', required=True)
    parser.add_argument('--destination', required=True)
    parser.add_argument('--register', action='store_true')
    parser.add_argument('--media-configuration')
    parser.add_argument('--media-service')
    parser.add_argument('--database-registration')
    args = parser.parse_args()
    try:
        print(provision(args.configuration, args.service, args.destination, args.register,args.media_configuration,args.media_service,args.database_registration))
    except Exception:
        raise SystemExit('Native profile provisioning failed. Check deployment registration and protected files.')
