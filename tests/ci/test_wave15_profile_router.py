import importlib.util
import pathlib
import unittest

ROOT = pathlib.Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location("router", ROOT / "scripts/ci/wave15_profile_router.py")
router = importlib.util.module_from_spec(SPEC)
assert SPEC.loader is not None
SPEC.loader.exec_module(router)


class Wave15ProfileRouterTests(unittest.TestCase):
    def classify(self, paths, body="VALIDATION_PROFILE: DOCS_I18N_HELP", override=""):
        return router.classify(paths, body, override)

    def test_docs_only_has_no_backend_browser_or_driver_job(self):
        result = self.classify(["docs/guide.md"])
        self.assertFalse(result["run_dotnet"])
        self.assertFalse(result["run_e2e"])
        self.assertFalse(result["run_driver"])

    def test_web_editor_requires_web_not_driver(self):
        result = self.classify(["web/scada-web/src/engineering/Editor.tsx"], "VALIDATION_PROFILE: UI_EDITOR")
        self.assertTrue(result["run_web"])
        self.assertFalse(result["run_driver"])

    def test_script_engineering_requires_script_and_web(self):
        result = self.classify(["src/Scada.Engineering/Script/Resolver.cs"], "VALIDATION_PROFILE: SCRIPT_ENGINEERING")
        self.assertIn("SCRIPT_ENGINEERING", result["effective_profiles"])
        self.assertTrue(result["run_web"])

    def test_script_runtime_requires_dotnet(self):
        result = self.classify(["src/Scada.Runtime/Scripting/Host.cs"], "VALIDATION_PROFILE: SCRIPT_RUNTIME")
        self.assertIn("SCRIPT_RUNTIME", result["effective_profiles"])
        self.assertTrue(result["run_dotnet"])

    def test_authority_requires_authority_evidence(self):
        result = self.classify(["src/Scada.Security/Authorization/Policy.cs"], "VALIDATION_PROFILE: AUTHORITY_CORE")
        self.assertIn("AUTHORITY_CORE", result["effective_profiles"])
        self.assertTrue(result["run_dotnet"])

    def test_licensing_session_requires_licensing_evidence(self):
        result = self.classify(["src/Scada.Api/Licensing/Product.cs"], "VALIDATION_PROFILE: SESSION_LICENSING")
        self.assertIn("SESSION_LICENSING", result["effective_profiles"])
        self.assertTrue(result["run_dotnet"])

    def test_driver_cannot_be_suppressed_by_cheaper_declaration(self):
        result = self.classify(["src/Scada.Drivers/Modbus/Driver.cs"], "VALIDATION_PROFILE: DOCS_I18N_HELP")
        self.assertIn("DRIVER_PROTOCOL", result["effective_profiles"])
        self.assertTrue(result["run_driver"])

    def test_ha_cannot_be_suppressed_by_ui_editor(self):
        result = self.classify(["src/Scada.Api/HighAvailability/State.cs"], "VALIDATION_PROFILE: UI_EDITOR")
        self.assertIn("HA_DISTRIBUTED", result["effective_profiles"])

    def test_unknown_profile_fails(self):
        with self.assertRaises(router.ProfileError):
            self.classify(["docs/a.md"], "VALIDATION_PROFILE: FASTEST")

    def test_missing_declaration_fails_for_non_exempt_change(self):
        with self.assertRaises(router.ProfileError):
            self.classify(["src/Scada.Api/Program.cs"], "")

    def test_declared_and_inferred_are_deterministic_union(self):
        result = self.classify(["src/Scada.Security/Auth.cs"], "VALIDATION_PROFILE: UI_EDITOR")
        self.assertEqual(result["effective_profiles"], ["AUTHORITY_CORE", "UI_EDITOR"])

    def test_manual_override_can_add_but_not_remove_inferred_risk(self):
        result = self.classify(["src/Scada.Drivers/Driver.cs"], "VALIDATION_PROFILE: DOCS_I18N_HELP", "UI_EDITOR")
        self.assertEqual(result["effective_profiles"], ["UI_EDITOR", "DOCS_I18N_HELP", "DRIVER_PROTOCOL"])

    def test_coordination_only_is_exempt(self):
        result = self.classify(["docs/WAVE15-MAIN-COORDINATOR-HANDOFF.md"], "")
        self.assertTrue(result["coordination_exempt"])

    def test_representative_fnd04_engineering_path_is_script_engineering(self):
        result = self.classify(["src/Scada.Api/Engineering/ScriptTagReferenceResolver.cs"], "VALIDATION_PROFILE: SCRIPT_ENGINEERING")
        self.assertIn("SCRIPT_ENGINEERING", result["effective_profiles"])

    def test_representative_fnd04_runtime_path_is_script_runtime(self):
        result = self.classify(["src/Scada.Runtime/Scripting/ScriptRuntimeHost.cs"], "VALIDATION_PROFILE: SCRIPT_RUNTIME")
        self.assertIn("SCRIPT_RUNTIME", result["effective_profiles"])


if __name__ == "__main__":
    unittest.main()
