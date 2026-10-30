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
