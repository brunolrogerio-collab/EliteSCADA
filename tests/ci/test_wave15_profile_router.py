import importlib.util
import pathlib
import unittest

ROOT = pathlib.Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location("router", ROOT / "scripts/ci/wave15_profile_router.py")
router = importlib.util.module_from_spec(SPEC)
assert SPEC.loader is not None
SPEC.loader.exec_module(router)


class Wave15ProfileRouterTests(unittest.TestCase):
    def classify(self, paths, body="VALIDATION_PROFILE: DOCS_I18N_HELP", override="", mode="pr"):
        return router.classify(paths, body, override, mode)

    def test_docs_only_has_no_backend_browser_or_driver_job(self):
        result = self.classify(["docs/guide.md"])
        self.assertFalse(result["run_dotnet"])
        self.assertFalse(result["run_e2e"])
        self.assertFalse(result["run_driver"])

    def test_web_editor_requires_web_not_driver(self):
        result = self.classify(["web/scada-web/src/engineering/Editor.tsx"], "VALIDATION_PROFILE: UI_EDITOR")
        self.assertTrue(result["run_web"])
        self.assertFalse(result["run_driver"])
        self.assertEqual(result["e2e_specs"], [
            "tests-e2e/app-shell.spec.ts",
            "tests-e2e/visual-editor-authoring-model.spec.ts",
            "tests-e2e/visual-editor-selection-model.spec.ts",
            "tests-e2e/visual-editor-workspace.spec.ts",
            "tests-e2e/visual-editor-z-order-model.spec.ts",
            "tests-e2e/wave-14-c25-engineering-lock.spec.ts",
        ])

    def test_runtime_renderer_runs_runtime_and_runtime_session_owner_specs(self):
        result = self.classify(["web/scada-web/src/runtime/application/RuntimeApplicationMount.tsx"], "VALIDATION_PROFILE: RUNTIME_RENDERER")
        self.assertEqual(result["e2e_specs"], [
            "tests-e2e/runtime.spec.ts",
            "tests-e2e/wave-14-c25-runtime-session.spec.ts",
        ])

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

    def test_dispatch_uses_inferred_runtime_profile_without_override(self):
        result = self.classify(["src/Scada.Api/Runtime/RuntimeSessionAdmission.cs"], "", mode="dispatch")
        self.assertIn("RUNTIME_RENDERER", result["effective_profiles"])

    def test_dispatch_without_inference_or_override_fails(self):
        with self.assertRaises(router.ProfileError):
            self.classify(["src/Scada.Api/UnclassifiedThing.cs"], "", mode="dispatch")

    def test_dispatch_override_unions_with_inferred_risk(self):
        result = self.classify(["src/Scada.Drivers/Driver.cs"], "", "UI_EDITOR", "dispatch")
        self.assertEqual(result["effective_profiles"], ["UI_EDITOR", "DRIVER_PROTOCOL"])

    def test_declared_and_inferred_are_deterministic_union(self):
        result = self.classify(["src/Scada.Security/Auth.cs"], "VALIDATION_PROFILE: UI_EDITOR")
        self.assertEqual(result["effective_profiles"], ["AUTHORITY_CORE", "UI_EDITOR"])

    def test_manual_override_can_add_but_not_remove_inferred_risk(self):
        result = self.classify(["src/Scada.Drivers/Driver.cs"], "VALIDATION_PROFILE: DOCS_I18N_HELP", "UI_EDITOR")
        self.assertEqual(result["effective_profiles"], ["UI_EDITOR", "DOCS_I18N_HELP", "DRIVER_PROTOCOL"])

    def test_coordination_only_is_exempt(self):
        result = self.classify(["docs/WAVE15-MAIN-COORDINATOR-HANDOFF.md"], "")
        self.assertTrue(result["coordination_exempt"])

    def test_real_fnd04_server_runtime_paths_require_script_runtime(self):
        paths = [
            "src/Scada.Api/Runtime/IsolatedPythonScriptHandlerExecutor.cs",
            "src/Scada.Api/Runtime/ServerScriptRunner.py",
            "src/Scada.Api/Runtime/ServerScriptRuntimeManager.cs",
        ]
        for path in paths:
            with self.subTest(path=path):
                result = self.classify([path], "VALIDATION_PROFILE: DOCS_I18N_HELP")
                self.assertIn("SCRIPT_RUNTIME", result["effective_profiles"])
                self.assertTrue(result["run_dotnet"])
                self.assertTrue(result["run_web"])
                self.assertTrue(result["run_e2e"])

    def test_generic_api_runtime_paths_require_runtime_renderer(self):
        paths = [
            "src/Scada.Api/Runtime/RuntimeSessionAdmission.cs",
            "src/Scada.Api/Runtime/DistributedRuntimeFoundationApi.cs",
            "src/Scada.Api/Runtime/RuntimeSessionWebSocketAdmission.cs",
        ]
        for path in paths:
            with self.subTest(path=path):
                result = self.classify([path], "VALIDATION_PROFILE: DOCS_I18N_HELP")
                self.assertIn("RUNTIME_RENDERER", result["effective_profiles"])
                self.assertTrue(result["run_dotnet"])
                self.assertTrue(result["run_web"])
                self.assertTrue(result["run_e2e"])

    def test_real_fnd04_web_authoring_paths_require_script_engineering(self):
        paths = [
            "web/scada-web/src/engineering/scripts/scriptEngineeringTypes.ts",
            "web/scada-web/src/engineering/scripts/ScriptEngineeringWorkspace.logic.ts",
            "web/scada-web/src/engineering/scripts/scriptAssistantModel.ts",
            "web/scada-web/src/engineering/scripts/scriptAssistantReferenceValidation.ts",
        ]
        for path in paths:
            with self.subTest(path=path):
                result = self.classify([path], "VALIDATION_PROFILE: DOCS_I18N_HELP")
                self.assertIn("SCRIPT_ENGINEERING", result["effective_profiles"])
                self.assertTrue(result["run_dotnet"])
                self.assertTrue(result["run_web"])
                self.assertTrue(result["run_e2e"])

    def test_authority_ux_has_backend_web_and_browser_evidence(self):
        result = self.classify([], "VALIDATION_PROFILE: AUTHORITY_UX")
        self.assertTrue(result["run_dotnet"])
        self.assertTrue(result["run_web"])
        self.assertTrue(result["run_e2e"])
        self.assertIn("Scada.Security.Tests", result["dotnet_projects"][0])

    def test_licensing_ux_has_backend_web_and_browser_evidence(self):
        result = self.classify([], "VALIDATION_PROFILE: LICENSING_UX")
        self.assertTrue(result["run_dotnet"])
        self.assertTrue(result["run_web"])
        self.assertTrue(result["run_e2e"])
        self.assertIn("Scada.Drivers.Tests", result["dotnet_projects"][0])

    def test_elitego_runtime_has_backend_web_and_browser_evidence(self):
        result = self.classify([], "VALIDATION_PROFILE: ELITEGO_RUNTIME")
        self.assertTrue(result["run_dotnet"])
        self.assertTrue(result["run_web"])
        self.assertTrue(result["run_e2e"])
        self.assertIn("Scada.Drivers.Tests", result["dotnet_projects"][0])

    def test_manual_dispatch_compares_full_branch_delta_from_integration_merge_base(self):
        workflow = (ROOT / ".github/workflows/wave15-pr.yml").read_text(encoding="utf-8")
        self.assertIn("refs/heads/wave15/corrections-integration", workflow)
        self.assertIn('git merge-base "$target_base" "$PR_HEAD_SHA"', workflow)
        self.assertNotIn('git diff --name-only "${GITHUB_SHA}^" "$GITHUB_SHA"', workflow)
        self.assertIn('router_mode=\'dispatch\'', workflow)
        self.assertIn('--mode "$router_mode"', workflow)


if __name__ == "__main__":
    unittest.main()
