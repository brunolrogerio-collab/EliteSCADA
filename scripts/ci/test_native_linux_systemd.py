"""Opt-in privileged runner proof; creates only one transient unit and temp files."""
import importlib.util
import json
import os
from pathlib import Path
import shutil
import socket
import subprocess
import sys
import tempfile
import uuid


def run():
    if sys.platform!='linux' or os.geteuid()!=0 or not Path('/run/systemd/system').is_dir():
        raise RuntimeError('An isolated elevated Linux runner with systemd is required')
    repo=Path(__file__).resolve().parents[2]
    folder=Path(tempfile.mkdtemp(prefix='scadivo-systemd-proof-')).resolve()
    name='scadivo-port-proof-'+uuid.uuid4().hex
    created=False
    def command(*args):return subprocess.run(list(args),check=True,capture_output=True,text=True,timeout=45)
    try:
        config=folder/'app.json';profile=folder/'profile.json'
        ports=set()
        while len(ports)<4:
            with socket.socket() as listener:listener.bind(('127.0.0.1',0));ports.add(listener.getsockname()[1])
        web,api,new_web,new_api=list(ports)
        original=json.dumps({'proof':'preserved','Kestrel':{'Endpoints':{'Web':{'Url':f'http://127.0.0.1:{web}'},'Api':{'Url':f'http://127.0.0.1:{api}'}}}}).encode()
        config.write_bytes(original)
        fixture=repo/'scripts/ci/native_port_proof_service.py'
        created=True
        command('systemd-run','--unit='+name,'--collect','--service-type=exec',sys.executable,str(fixture),str(config))
        spec=importlib.util.spec_from_file_location('native_systemd_adapter',repo/'web/scada-web/public/installation/apply-native-network-profile.py')
        adapter=importlib.util.module_from_spec(spec);spec.loader.exec_module(adapter)
        adapter.health(f'http://127.0.0.1:{web}/health');adapter.health(f'http://127.0.0.1:{api}/health')
        provision=repo/'web/scada-web/public/installation/provision-native-network-profile.py'
        command(sys.executable,str(provision),'--configuration',str(config),'--service',name+'.service','--destination',str(profile))
        adapter.run(profile,new_web,False,new_api)
        assert config.read_bytes()==original,'Plan mutated configuration'
        adapter.run(profile,new_web,True,new_api)
        adapter.health(f'http://127.0.0.1:{new_web}/health');adapter.health(f'http://127.0.0.1:{new_api}/health')
        assert json.loads(config.read_bytes())['proof']=='preserved'
        backup=next(folder.glob('app.json.network-backup-*'))
        journal=config.with_name(config.name+'.access-port-pending.json')
        descriptor=os.open(journal,os.O_WRONLY|os.O_CREAT|os.O_EXCL,0o600)
        with os.fdopen(descriptor,'w') as output:json.dump({'schema':'scadivo-access-port-recovery-v1','backup':str(backup)},output)
        adapter.run(profile,recover=True)
        assert config.read_bytes()==original and not journal.exists(),'Recovery incomplete'
        adapter.health(f'http://127.0.0.1:{web}/health');adapter.health(f'http://127.0.0.1:{api}/health')
        print('PASS: transient systemd service, plan, two port changes and explicit recovery. Fixture only; product host not validated.')
    finally:
        if created:
            stopped=subprocess.run(['systemctl','stop',name+'.service'],capture_output=True,text=True,timeout=45)
            if stopped.returncode:
                state=subprocess.run(['systemctl','show','--property=LoadState','--value',name+'.service'],capture_output=True,text=True,timeout=15)
                if state.stdout.strip()!='not-found':raise RuntimeError('Transient service cleanup failed; temporary files retained')
            # --collect unloads this transient unit after it stops; no unit file
            # or persistent service is installed by the proof.
        expected=Path(tempfile.gettempdir()).resolve()
        if folder.parent!=expected or not folder.name.startswith('scadivo-systemd-proof-'):raise RuntimeError('Unsafe cleanup target')
        shutil.rmtree(folder)


if __name__=='__main__':run()
