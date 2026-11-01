import importlib.util
import pathlib
import re
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

    def test_authority_policy_postgres_ha_evidence_runs_postgres_and_two_process_profiles(self):
        result = self.classify(
            ["tests/Scada.Drivers.Tests/AuthorityPolicyPostgreSqlHaTests.cs"],
            "VALIDATION_PROFILE: HA_DISTRIBUTED",
        )
        self.assertIn("HA_DISTRIBUTED", result["effective_profiles"])
        self.assertIn(router.DRIVERS, result["dotnet_projects"])
        self.assertTrue(result["run_ha_two_process"])
        self.assertFalse(result["run_ha_d2_acceptance"])

    def test_ha_d2_acceptance_is_a_separate_selected_gate(self):
        result = self.classify(
            ["tests/Scada.Drivers.Tests/AuthorityPolicyTwoApiPostgreSqlEvidenceTests.cs"],
            "VALIDATION_PROFILE: HA_DISTRIBUTED",
        )
        self.assertFalse(result["run_ha_two_process"])
        self.assertTrue(result["run_ha_d2_acceptance"])

    def test_ha_d2_evidence_path_cannot_be_suppressed_by_cheaper_profile(self):
        result = self.classify(
            ["tests/Scada.Drivers.Tests/AuthorityPolicyTwoApiPostgreSqlEvidenceTests.cs"],
            "VALIDATION_PROFILE: DOCS_I18N_HELP",
        )
        self.assertIn("HA_DISTRIBUTED", result["effective_profiles"])
        self.assertTrue(result["run_ha_d2_acceptance"])

    def test_ha_d1_script_remains_its_own_gate(self):
        result = self.classify(
            ["scripts/ci/ha_d1_two_process_evidence.py"],
            "VALIDATION_PROFILE: HA_DISTRIBUTED",
        )
        self.assertTrue(result["run_ha_two_process"])
        self.assertFalse(result["run_ha_d2_acceptance"])

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
        self.assertFalse(result["run_ha_d2_acceptance"])

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

    def test_installation_network_ui_owns_its_isolated_browser_spec(self):
        result = self.classify(
            ["web/scada-web/src/engineering/InstallationNetworkWorkspace.tsx"],
            "VALIDATION_PROFILE: INSTALLATION",
        )
        self.assertEqual(result["e2e_specs"], ["tests-e2e/installation-network-profile.spec.ts"])
        self.assertEqual(result["e2e_config"], "playwright.intelligence-ui.config.ts")
        self.assertTrue(result["run_web"])
        self.assertFalse(result["run_ha_two_process"])

    def test_intelligence_durable_restart_selects_only_its_cross_platform_hardening_gate(self):
        result = self.classify(
            ["tests/Scada.Drivers.Tests/AssetProcessIntelligenceDurableRestartTests.cs"],
            "VALIDATION_PROFILE: INTELLIGENCE_HARDENING",
        )
        self.assertEqual(result["effective_profiles"], ["INTELLIGENCE_HARDENING"])
        self.assertTrue(result["run_intelligence_hardening"])
        self.assertTrue(result["run_intelligence_ui"])
        self.assertFalse(result["run_dotnet"])
        self.assertFalse(result["run_web"])
        self.assertFalse(result["run_e2e"])
        self.assertFalse(result["run_ha_two_process"])
        self.assertFalse(result["run_ha_d2_acceptance"])

    def test_intelligence_stage6b_characterization_selects_only_its_cross_platform_gate(self):
        result = self.classify(
            ["tests/Scada.Drivers.Tests/AssetProcessIntelligenceStage6BCharacterizationTests.cs"],
            "VALIDATION_PROFILE: INTELLIGENCE_HARDENING",
        )
        self.assertEqual(result["effective_profiles"], ["INTELLIGENCE_HARDENING"])
        self.assertTrue(result["run_intelligence_hardening"])
        self.assertTrue(result["run_intelligence_ui"])
        self.assertFalse(result["run_dotnet"])
        self.assertFalse(result["run_web"])
        self.assertFalse(result["run_e2e"])
        self.assertFalse(result["run_ha_two_process"])
        self.assertFalse(result["run_ha_d2_acceptance"])

    def test_intelligence_hardening_frontend_script_selects_web_build_and_ui_gate(self):
        result = self.classify(
            ["web/scada-web/package.json"],
            "VALIDATION_PROFILE: CI_INFRA, INTELLIGENCE_HARDENING",
        )
        self.assertTrue(result["run_web"])
        self.assertTrue(result["run_intelligence_hardening"])
        self.assertTrue(result["run_intelligence_ui"])
        self.assertFalse(result["run_e2e"])
        self.assertFalse(result["run_ha_two_process"])
        self.assertFalse(result["run_ha_d2_acceptance"])

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
        self.assertTrue(result["run_installation_linux"])

    def test_unrelated_profile_does_not_start_linux_installation_evidence(self):
        result = self.classify(["docs/README.md"], "VALIDATION_PROFILE: DOCS_I18N_HELP")
        self.assertFalse(result["run_installation_linux"])

    def test_security_ui_keeps_security_evidence(self):
        result = self.classify(
            ["web/scada-web/src/security/UserAdministration.tsx"],
            "VALIDATION_PROFILE: AUTHORITY_UX",
        )
        self.assertTrue(result["run_web"])
        self.assertTrue(result["run_dotnet"])
        self.assertEqual(result["e2e_specs"], ["tests-e2e/security.spec.ts"])

    def test_intelligence_permit_rearm_ui_runs_authority_ha_and_mounted_browser_evidence(self):
        result = self.classify(
            ["web/scada-web/src/engineering/AssetProcessIntelligenceWorkspace.tsx"],
            "VALIDATION_PROFILE: AUTHORITY_UX, HA_DISTRIBUTED",
        )
        self.assertIn("AUTHORITY_UX", result["effective_profiles"])
        self.assertIn("HA_DISTRIBUTED", result["effective_profiles"])
        self.assertTrue(result["run_web"])
        self.assertTrue(result["run_dotnet"])
        self.assertTrue(result["run_e2e"])
        self.assertIn(
            "tests-e2e/wave-15-intelligence-permit-rearm-mounted.spec.ts",
            result["e2e_specs"],
        )

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

    def test_online_license_recovery_selects_focused_dotnet_and_browser_evidence(self):
        result = self.classify(
            [
                "src/Scada.Api/Licensing/OnlineLicenseDeviceClient.Confirmation.cs",
                "web/scada-web/src/licensing/OnlineLicenseActivation.tsx",
            ],
            "VALIDATION_PROFILE: ONLINE_LICENSE_RECOVERY",
        )
        self.assertIn("ONLINE_LICENSE_RECOVERY", result["effective_profiles"])
        self.assertEqual(result["dotnet_projects"], [router.DRIVERS])
        self.assertEqual(result["dotnet_test_filter"], router.ONLINE_LICENSE_DOTNET_FILTER)
        self.assertEqual(result["e2e_specs"], ["tests-e2e/online-license-confirmation.spec.ts"])
        self.assertEqual(result["e2e_config"], "playwright.online-license.config.ts")

    def test_other_session_licensing_keeps_its_existing_full_project_validation(self):
        result = self.classify(
            ["src/Scada.Api/Runtime/RuntimeSessionAdmission.cs"],
            "VALIDATION_PROFILE: SESSION_LICENSING",
        )
        self.assertEqual(result["dotnet_test_filter"], "")
        self.assertEqual(result["e2e_config"], "")

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

    def test_shared_workflow_edit_does_not_force_unrelated_driver_sidecar_audits(self):
        workflow = (ROOT / ".github/workflows/wave15-pr.yml").read_text(encoding="utf-8")
        probes = {
            "native_scope": "src/Scada.DriverHost/Sidecars/ZigbeeNative/package.json",
            "knx_scope": "src/Scada.DriverHost/Sidecars/KnxIp/main.py",
            "zwave_scope": "src/Scada.DriverHost/Sidecars/ZWaveJs/src/sidecar.mjs",
            "matter_scope": "src/Scada.DriverHost/Sidecars/MatterWifi/src/bootstrap.mjs",
        }
        for scope_name, driver_path in probes.items():
            start = workflow.index(f"      - id: {scope_name}")
            following = workflow.find("      - id:", start + 1)
            block = workflow[start:following if following >= 0 else len(workflow)]
            pattern_match = re.search(r'pattern = re\.compile\(r"([^"]+)"\)', block)
            self.assertIsNotNone(pattern_match, scope_name)
            pattern = re.compile(pattern_match.group(1))
            self.assertFalse(pattern.fullmatch(".github/workflows/wave15-pr.yml"), scope_name)
            self.assertTrue(pattern.fullmatch(driver_path), scope_name)

    def test_playwright_jobs_route_ubuntu_browser_libraries_over_https(self):
        workflow = (ROOT / ".github/workflows/wave15-pr.yml").read_text(encoding="utf-8")
        self.assertEqual(workflow.count("Prefer the reachable HTTPS Ubuntu archive for browser libraries"), 2)
        self.assertEqual(workflow.count("run: npx playwright install-deps chromium"), 2)
        self.assertEqual(workflow.count("https://archive.ubuntu.com/ubuntu/"), 4)

    def test_generic_dotnet_ci_quarantines_ha_d2_but_keeps_dedicated_gates(self):
        dotnet_workflow = (ROOT / ".github/workflows/dotnet-ci.yml").read_text(encoding="utf-8")
        generic_test_step = next(
            line.strip()
            for line in dotnet_workflow.splitlines()
            if line.strip().startswith("run: dotnet test ScadaPlatform.sln")
        )
        self.assertIn('FullyQualifiedName!~L3DriverInteractionS6Tests', generic_test_step)
        self.assertIn('FullyQualifiedName!~RealApiProcesses_HaD2', generic_test_step)
        self.assertIn('FullyQualifiedName!~RealApiProcesses_RevokeCancelRejoinAndRequireExplicitReactivation', generic_test_step)

        wave15_workflow = (ROOT / ".github/workflows/wave15-pr.yml").read_text(encoding="utf-8")
        ha_d2_job = wave15_workflow.split("  ha-d2-acceptance:", 1)[1].split("  native-zigbee-sidecar:", 1)[0]
        self.assertIn("name: HA D2 shared-database lifecycle and session continuity", ha_d2_job)
        selected_tests = next(line.strip() for line in ha_d2_job.splitlines() if "--filter '" in line)
        self.assertIn("FullyQualifiedName~RealApiProcesses_HaD2SharedDatabasePromotesAndFailsBack", selected_tests)
        self.assertNotIn("FullyQualifiedName~RealApiProcesses_HaD2SeparateDatabasesPromotesAndFailsBack", selected_tests)
        self.assertIn("Separate-database HA-D2 acceptance is temporarily quarantined", ha_d2_job)

        l3_workflow = (ROOT / ".github/workflows/l3-seven-driver-lab.yml").read_text(encoding="utf-8")
        self.assertIn(
            "name: Run mandatory L3 Z2M action to DNP3 PulseOn interaction route",
            l3_workflow,
        )
        self.assertIn('--filter "Category=L3DriverInteractionS6"', l3_workflow)


if __name__ == "__main__":
    unittest.main()
