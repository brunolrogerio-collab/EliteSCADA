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

    def test_ci_infra_runs_only_common_sanity(self):
        result = self.classify([".github/workflows/wave15-pr.yml"], "VALIDATION_PROFILE: CI_INFRA")
        self.assertEqual(result["effective_profiles"], ["CI_INFRA"])
        self.assertTrue(result["run_common_sanity"])
        self.assertFalse(result["run_web"])
        self.assertFalse(result["run_dotnet"])
        self.assertFalse(result["run_e2e"])
        self.assertFalse(result["run_ha_two_process"])

    def test_docs_only_has_no_product_jobs(self):
        result = self.classify(["docs/guide.md"])
        self.assertFalse(result["run_web"])
        self.assertFalse(result["run_dotnet"])
        self.assertFalse(result["run_e2e"])

    def test_app_shell_has_small_owned_browser_surface(self):
        result = self.classify(
            ["web/scada-web/src/main.tsx"],
            "VALIDATION_PROFILE: APP_SHELL",
        )
        self.assertEqual(result["effective_profiles"], ["APP_SHELL"])
        self.assertTrue(result["run_web"])
        self.assertFalse(result["run_dotnet"])
        self.assertEqual(result["e2e_specs"], [
            "tests-e2e/app-shell.spec.ts",
            "tests-e2e/effective-capabilities-contract.spec.ts",
        ])

    def test_visual_editor_keeps_visual_editor_evidence_without_tag_duplication(self):
        result = self.classify(
            ["web/scada-web/src/engineering/visual-editor/VisualEditorWorkspace.tsx"],
            "VALIDATION_PROFILE: UI_EDITOR",
        )
        self.assertIn("UI_EDITOR", result["effective_profiles"])
        self.assertEqual(result["e2e_specs"], [
            "tests-e2e/visual-editor-authoring-model.spec.ts",
            "tests-e2e/visual-editor-selection-model.spec.ts",
            "tests-e2e/visual-editor-workspace.spec.ts",
            "tests-e2e/visual-editor-z-order-model.spec.ts",
        ])

    def test_runtime_application_runs_runtime_owned_specs(self):
        result = self.classify(
            ["web/scada-web/src/runtime/application/RuntimeApplicationMount.tsx"],
            "VALIDATION_PROFILE: RUNTIME_RENDERER",
        )
        self.assertEqual(result["e2e_specs"], [
            "tests-e2e/runtime.spec.ts",
            "tests-e2e/wave-14-c25-runtime-session.spec.ts",
        ])

    def test_backend_script_runtime_does_not_pull_web_or_browser(self):
        result = self.classify(
            ["src/Scada.Api/Runtime/ServerScriptRunner.py"],
            "VALIDATION_PROFILE: SCRIPT_RUNTIME",
        )
        self.assertTrue(result["run_dotnet"])
        self.assertFalse(result["run_web"])
        self.assertFalse(result["run_e2e"])

    def test_backend_script_engineering_does_not_pull_web_or_browser(self):
        result = self.classify(
            ["src/Scada.Engineering/Script/Resolver.cs"],
            "VALIDATION_PROFILE: SCRIPT_ENGINEERING",
        )
        self.assertTrue(result["run_dotnet"])
        self.assertFalse(result["run_web"])
        self.assertFalse(result["run_e2e"])

    def test_ha_backend_is_not_runtime_renderer_and_runs_two_process(self):
        result = self.classify(
            ["src/Scada.Api/Runtime/RuntimeHighAvailability.cs"],
            "VALIDATION_PROFILE: HA_DISTRIBUTED",
        )
        self.assertIn("HA_DISTRIBUTED", result["effective_profiles"])
        self.assertNotIn("RUNTIME_RENDERER", result["effective_profiles"])
        self.assertTrue(result["run_dotnet"])
        self.assertFalse(result["run_web"])
        self.assertFalse(result["run_e2e"])
        self.assertTrue(result["run_ha_two_process"])

    def test_ha_admin_ui_is_not_full_ui_editor_and_skips_two_process(self):
        result = self.classify(
            ["web/scada-web/src/engineering/ha/HighAvailabilityAdminWorkspace.tsx"],
            "VALIDATION_PROFILE: HA_DISTRIBUTED",
        )
        self.assertIn("HA_DISTRIBUTED", result["effective_profiles"])
        self.assertNotIn("UI_EDITOR", result["effective_profiles"])
        self.assertTrue(result["run_web"])
        self.assertEqual(result["e2e_specs"], ["tests-e2e/ha-admin-workspace.spec.ts"])
        self.assertFalse(result["run_ha_two_process"])

    def test_database_core_owns_three_relevant_dotnet_projects_without_browser(self):
        result = self.classify(
            ["src/Scada.Api/Persistence/DatabaseTopologyAdministrationService.cs"],
            "VALIDATION_PROFILE: DATABASE_TOPOLOGY",
        )
        self.assertEqual(result["effective_profiles"], ["DATABASE_TOPOLOGY"])
        self.assertEqual(result["dotnet_projects"], sorted([
            router.DRIVERS, router.HISTORIAN, router.SECURITY,
        ]))
        self.assertFalse(result["run_web"])
        self.assertFalse(result["run_e2e"])

    def test_database_ui_owns_only_database_browser_spec(self):
        result = self.classify(
            ["web/scada-web/src/database-topology/DatabaseTopologyApp.tsx"],
            "VALIDATION_PROFILE: DATABASE_TOPOLOGY",
        )
        self.assertTrue(result["run_web"])
        self.assertEqual(result["e2e_specs"], ["tests-e2e/database-topology-mounted.spec.ts"])

    def test_installation_store_name_does_not_trigger_installation_profile(self):
        result = self.classify(
            ["src/Scada.Persistence.PostgreSql/PostgreSqlEngineeringInstallationBindingStore.cs"],
            "VALIDATION_PROFILE: DATABASE_TOPOLOGY",
        )
        self.assertNotIn("INSTALLATION", result["effective_profiles"])

    def test_product_i18n_file_does_not_trigger_docs_profile(self):
        result = self.classify(
            ["web/scada-web/src/engineering/ha/i18n.ts"],
            "VALIDATION_PROFILE: HA_DISTRIBUTED",
        )
        self.assertNotIn("DOCS_I18N_HELP", result["effective_profiles"])

    def test_real_installation_ui_keeps_local_auth_evidence(self):
        result = self.classify(
            ["web/scada-web/src/engineering/InstallationSwitchingWorkspace.tsx"],
            "VALIDATION_PROFILE: INSTALLATION",
        )
        self.assertIn("INSTALLATION", result["effective_profiles"])
        self.assertTrue(result["run_web"])
        self.assertEqual(result["e2e_specs"], ["tests-e2e/local-auth.spec.ts"])

    def test_security_ui_keeps_security_evidence(self):
        result = self.classify(
            ["web/scada-web/src/security/UserAdministration.tsx"],
            "VALIDATION_PROFILE: AUTHORITY_UX",
        )
        self.assertTrue(result["run_web"])
        self.assertTrue(result["run_dotnet"])
        self.assertEqual(result["e2e_specs"], ["tests-e2e/security.spec.ts"])

    def test_changed_browser_spec_runs_itself_not_old_profile_bundle(self):
        result = self.classify(
            ["web/scada-web/tests-e2e/runtime.spec.ts"],
            "VALIDATION_PROFILE: CI_INFRA",
        )
        self.assertEqual(result["e2e_specs"], ["tests-e2e/runtime.spec.ts"])
        self.assertTrue(result["run_e2e"])

    def test_distributed_runtime_foundation_maps_to_ha_not_renderer(self):
        result = self.classify(
            ["src/Scada.Api/Runtime/DistributedRuntimeFoundationApi.cs"],
            "VALIDATION_PROFILE: HA_DISTRIBUTED",
        )
        self.assertIn("HA_DISTRIBUTED", result["effective_profiles"])
        self.assertNotIn("RUNTIME_RENDERER", result["effective_profiles"])

    def test_runtime_session_api_maps_to_session_not_renderer(self):
        result = self.classify(
            ["src/Scada.Api/Runtime/RuntimeSessionAdmission.cs"],
            "VALIDATION_PROFILE: SESSION_LICENSING",
        )
        self.assertIn("SESSION_LICENSING", result["effective_profiles"])
        self.assertNotIn("RUNTIME_RENDERER", result["effective_profiles"])

    def test_driver_cannot_be_suppressed_by_cheaper_declaration(self):
        result = self.classify(
            ["src/Scada.Drivers/Modbus/Driver.cs"],
            "VALIDATION_PROFILE: DOCS_I18N_HELP",
        )
        self.assertIn("DRIVER_PROTOCOL", result["effective_profiles"])
        self.assertTrue(result["run_driver"])

    def test_ha_cannot_be_suppressed_by_cheaper_declaration(self):
        result = self.classify(
            ["src/Scada.Api/Runtime/RuntimeHighAvailability.cs"],
            "VALIDATION_PROFILE: DOCS_I18N_HELP",
        )
        self.assertIn("HA_DISTRIBUTED", result["effective_profiles"])

    def test_database_cannot_be_suppressed_by_cheaper_declaration(self):
        result = self.classify(
            ["src/Scada.Api/Persistence/DatabaseTopologyApi.cs"],
            "VALIDATION_PROFILE: DOCS_I18N_HELP",
        )
        self.assertIn("DATABASE_TOPOLOGY", result["effective_profiles"])

    def test_ha_distributed_owns_mounted_admin_browser_evidence(self):
        result = self.classify(
            ["web/scada-web/src/engineering/ha/HaAdminWorkspace.tsx"],
            "VALIDATION_PROFILE: HA_DISTRIBUTED",
        )
        self.assertTrue(result["run_dotnet"])
        self.assertTrue(result["run_e2e"])
        self.assertIn("tests-e2e/ha-admin-workspace.spec.ts", result["e2e_specs"])

    def test_unknown_profile_fails(self):
        with self.assertRaises(router.ProfileError):
            self.classify(["docs/a.md"], "VALIDATION_PROFILE: FASTEST")

    def test_missing_declaration_fails_for_non_exempt_change(self):
        with self.assertRaises(router.ProfileError):
            self.classify(["src/Scada.Api/Program.cs"], "")

    def test_dispatch_unions_override_with_inferred_risk(self):
        result = self.classify(
            ["src/Scada.Drivers/Driver.cs"], "", "UI_EDITOR", "dispatch"
        )
        self.assertEqual(result["effective_profiles"], ["UI_EDITOR", "DRIVER_PROTOCOL"])

    def test_coordination_only_is_exempt(self):
        result = self.classify(["docs/WAVE15-MAIN-COORDINATOR-HANDOFF.md"], "")
        self.assertTrue(result["coordination_exempt"])

    def test_manual_dispatch_compares_full_branch_delta_from_integration_merge_base(self):
        workflow = (ROOT / ".github/workflows/wave15-pr.yml").read_text(encoding="utf-8")
        self.assertIn("refs/heads/wave15/corrections-integration", workflow)
        self.assertIn('git merge-base "$target_base" "$PR_HEAD_SHA"', workflow)
        self.assertNotIn('git diff --name-only "${GITHUB_SHA}^" "$GITHUB_SHA"', workflow)
        self.assertIn("router_mode='dispatch'", workflow)
        self.assertIn('--mode "$router_mode"', workflow)


if __name__ == "__main__":
    unittest.main()
