"""Cross-adapter interrupted operations must not overwrite each other's backups."""
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import Mock, patch


class NativeOperationCoordinationTests(unittest.TestCase):
    def test_other_pending_operation_blocks_apply_and_recovery_before_service_actions(self):
        folder=Path(__file__).resolve().parents[2]/'web/scada-web/public/installation'
        for kind, filename in [('access','apply-native-network-profile.py'),('database','native_database_port.py'),('media','native_media_ports.py')]:
            spec=importlib.util.spec_from_file_location('coordination_'+kind,folder/filename)
            module=importlib.util.module_from_spec(spec); spec.loader.exec_module(module)
            for pending in {'access','database','media'}-{kind}:
                with self.subTest(adapter=kind,pending=pending), tempfile.TemporaryDirectory() as directory:
                    root=Path(directory); app=root/'app.json'; topology=root/'topology.json'; profile=root/'profile.json'
                    app.write_text('{}')
                    topology.write_text(json.dumps({'schemaVersion':1,'active':{'mode':'LocalManaged'},'pending':None}))
                    profile.write_text(json.dumps({'schema':'scadivo-native-network-v1','platform':'linux',
                        'configurationFile':str(app),'serviceName':'api','topologyFile':str(topology),
                        'database':{'serviceName':'database'}}))
                    journal=app.with_name(app.name+'.'+pending+'-port-pending.json'); journal.write_text('protected-pending-record')
                    service=Mock(); verify=Mock()
                    if kind=='access':
                        apply=lambda: module.run_locked(profile,28080,True)
                        recover=lambda: module.recover_locked(profile)
                    elif kind=='database':
                        apply=lambda: module.execute_locked(profile,25432,True)
                        recover=lambda: module.recover_locked(profile)
                    else:
                        apply=lambda: module.apply_locked(profile,service,verify,19997,18888,True)
                        recover=lambda: module.recover_locked(profile,service,verify)
                    with patch.object(module.subprocess,'run') as process:
                        for operation in (apply,recover):
                            with self.assertRaisesRegex(RuntimeError,'Another administrative operation'):
                                operation()
                        process.assert_not_called()
                    service.assert_not_called(); verify.assert_not_called()
                    self.assertEqual(app.read_text(),'{}')
                    self.assertEqual(journal.read_text(),'protected-pending-record')
