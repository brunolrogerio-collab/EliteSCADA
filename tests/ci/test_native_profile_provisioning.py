import importlib.util
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch

source=Path(__file__).resolve().parents[2]/'web/scada-web/public/installation/provision-native-network-profile.py'
spec=importlib.util.spec_from_file_location('provision_native',source)
module=importlib.util.module_from_spec(spec); spec.loader.exec_module(module)


@unittest.skipUnless(os.name=='posix','Linux file permissions and flock')
class NativeProfileProvisioningTests(unittest.TestCase):
    def test_profile_permissions_no_overwrite_and_pending_recovery(self):
        import fcntl
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory); config=root/'app.json'; target=root/'profile.json'
            original=json.dumps({'secret':'not-copied','Kestrel':{'Endpoints':{'Web':{'Url':'http://127.0.0.1:18080'},'Api':{'Url':'http://127.0.0.1:15120'}}}})
            config.write_text(original)
            with patch.object(module.subprocess,'run',return_value=subprocess.CompletedProcess([],0,'not-found\n','')) as service:
                with self.assertRaises(ValueError): module.provision(config,'scadivo.service',target)
                self.assertFalse(target.exists())
                service.return_value=subprocess.CompletedProcess([],0,'loaded\n','')
                with config.with_name(config.name+'.network.lock').open('w') as lock:
                    fcntl.flock(lock,fcntl.LOCK_EX|fcntl.LOCK_NB)
                    with self.assertRaises(BlockingIOError): module.provision(config,'scadivo.service',target)
                pending=config.with_name(config.name+'.media-port-pending.json'); pending.write_text('{}')
                with self.assertRaises(RuntimeError): module.provision(config,'scadivo.service',target)
                pending.unlink()
                config.write_text(original.replace(':15120',':18080'))
                with self.assertRaises(ValueError): module.provision(config,'scadivo.service',target)
                config.write_text(original)
                module.provision(config,'scadivo.service',target)
                self.assertEqual(target.stat().st_mode & 0o777,0o600)
                self.assertNotIn('not-copied',target.read_text())
                self.assertEqual(json.loads(target.read_text())['configurationFile'],str(config))
                before=target.read_bytes()
                with self.assertRaises(FileExistsError): module.provision(config,'scadivo.service',target)
                self.assertEqual(target.read_bytes(),before)
                self.assertEqual(config.read_text(),original)
                registered=root/'registered-profile.json'
                module.provision(config,'scadivo.service',registered,register=True)
                updated=json.loads(config.read_text())
                self.assertEqual(updated['secret'],'not-copied')
                self.assertEqual(updated['Kestrel'],json.loads(original)['Kestrel'])
                self.assertEqual(updated['InstallationNetwork'],{'Profile':'native-linux','AdministrativeProfilePath':str(registered)})
                backup=next(root.glob('app.json.network-backup-*'))
                self.assertEqual(backup.read_text(),original)
                self.assertEqual(backup.stat().st_mode & 0o777,0o600)
                self.assertNotIn('not-copied',registered.read_text())
                media=root/'media.yml'; media.write_text('apiAddress: :9997\nhlsAddress: :8888\n')
                updated['MediaSources']={'GatewayControlUrl':'http://127.0.0.1:9997','GatewayPlaybackUrl':'http://127.0.0.1:8888'}
                config.write_text(json.dumps(updated))
                media_profile=root/'media-profile.json'
                with self.assertRaises(ValueError): module.provision(config,'scadivo.service',media_profile,media_configuration=media)
                with self.assertRaises(ValueError): module.provision(config,'scadivo.service',media_profile,media_configuration=media,media_service='scadivo.service')
                media.write_text('apiAddress: :9998\nhlsAddress: :8888\n')
                with self.assertRaises(ValueError): module.provision(config,'scadivo.service',media_profile,media_configuration=media,media_service='media.service')
                self.assertFalse(media_profile.exists())
                media.write_text('apiAddress: :9997\nhlsAddress: :8888\n')
                module.provision(config,'scadivo.service',media_profile,media_configuration=media,media_service='media.service')
                self.assertEqual(json.loads(media_profile.read_text())['media'],{'configurationFile':str(media),'serviceName':'media.service'})
                topology=root/'topology.json'; pg=root/'postgresql.conf'; psql=root/'psql'; declaration=root/'db.json'; db_profile=root/'db-profile.json'
                topology.write_text(json.dumps({'schemaVersion':1,'active':{'mode':'LocalManaged'},'pending':None})); pg.write_text('port=15432'); psql.touch()
                db=dict(topologyFile=str(topology),configurationFile=str(pg),psqlExecutable=str(psql),serviceName='postgresql.service',currentPort=15432,username='test-account',databaseName='plant')
                declaration.write_text(json.dumps(dict(db,password='must-reject')))
                with self.assertRaises(ValueError): module.provision(config,'scadivo.service',db_profile,database_registration=declaration)
                self.assertFalse(db_profile.exists())
                declaration.write_text(json.dumps(db))
                topology.write_text(json.dumps({'schemaVersion':1,'active':{'mode':'Remote'},'pending':None}))
                with self.assertRaises(ValueError): module.provision(config,'scadivo.service',db_profile,database_registration=declaration)
                topology.write_text(json.dumps({'schemaVersion':1,'active':{'mode':'LocalManaged'},'pending':None}))
                module.provision(config,'scadivo.service',db_profile,database_registration=declaration)
                generated=json.loads(db_profile.read_text())
                self.assertEqual(generated['database']['currentPort'],15432)
                self.assertEqual(generated['topologyFile'],str(topology))
                combined=root/'combined.json'
                declaration.write_text(json.dumps(dict(db,configurationFile=str(media))))
                with self.assertRaises(ValueError): module.provision(config,'scadivo.service',combined,media_configuration=media,media_service='media.service',database_registration=declaration)
                self.assertFalse(combined.exists())
                declaration.write_text(json.dumps(db))
                module.provision(config,'scadivo.service',combined,media_configuration=media,media_service='media.service',database_registration=declaration)
                generated=json.loads(combined.read_text())
                self.assertEqual(generated['database']['configurationFile'],str(pg))
                self.assertEqual(generated['media']['configurationFile'],str(media))
