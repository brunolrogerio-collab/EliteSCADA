#!/usr/bin/env python3
"""Deterministic Wave 15 PR validation-profile classification."""
from __future__ import annotations

import argparse
import json
import os
import re
import sys
from pathlib import Path

VOCABULARY = (
    "FOUNDATION_LIFECYCLE", "FOUNDATION_TIMING", "AUTHORITY_CORE",
    "SESSION_LICENSING", "SCRIPT_RUNTIME", "RUNTIME_RENDERER", "APP_SHELL",
    "UI_EDITOR", "SCRIPT_ENGINEERING", "AUTHORITY_UX", "LICENSING_UX",
    "ELITEGO_RUNTIME", "DATABASE_TOPOLOGY", "INSTALLATION", "HA_DISTRIBUTED",
    "DOCS_I18N_HELP", "EEE_PACKAGE", "CI_INFRA", "DRIVER_PROTOCOL",
)
ORDER = {profile: index for index, profile in enumerate(VOCABULARY)}

COORDINATION_ONLY = {
    "LAST CHANGE.md", "docs/CURRENT-COORDINATOR-HANDOFF.md",
    "docs/NEXT-COORDINATOR-CHAT-HANDOFF.md", "docs/ROADMAP.md",
    "docs/WAVE15-MAIN-COORDINATOR-HANDOFF.md",
}

# Path inference is a non-bypassable risk floor. Keep rules domain-specific:
# broad directory names such as engineering/, runtime/, installation or i18n must
# not drag unrelated browser suites into every PR.
PATH_RULES = (
    ("AUTHORITY_CORE", ("src/scada.security/", "src/scada.api/security/", "tests/scada.security.tests/")),
    ("SESSION_LICENSING", ("licensing/", "runtimesession", "runtime-session")),
    ("SCRIPT_RUNTIME", (
        "python-runtime", "script-runtime", "scripting/runtime",
        "src/scada.api/runtime/isolatedpythonscripthandlerexecutor.cs",
        "src/scada.api/runtime/serverscriptrunner.py",
        "src/scada.api/runtime/serverscriptruntimemanager.cs",
    )),
    ("SCRIPT_ENGINEERING", (
        "src/scada.engineering/", "script-engineering", "python-editor", "python-script", "tag-reference",
        "web/scada-web/src/engineering/scripts/",
    )),
    ("APP_SHELL", (
        "web/scada-web/src/appnavigation.tsx",
        "web/scada-web/src/main.tsx",
        "web/scada-web/src/appshell",
        "web/scada-web/src/engineering/engineeringapp.tsx",
    )),
    ("RUNTIME_RENDERER", (
        "visual-runtime", "renderer", "src/scada.runtime/", "web/scada-web/src/runtime/",
    )),
    ("UI_EDITOR", (
        "web/scada-web/src/engineering/visual-editor/",
        "visual-editor", "screen-editor", "visualeditorworkspace", "popupvisualeditorworkspace",
    )),
    ("AUTHORITY_UX", ("web/scada-web/src/security/", "effective-capabilities", "user-administration", "security.spec")),
    ("LICENSING_UX", ("web/scada-web/src/licensing/", "license-generator")),
    ("ELITEGO_RUNTIME", ("elitego",)),
    ("DATABASE_TOPOLOGY", (
        "database-topology", "databasetopology", "databasemaintenance",
        "durablewriteadmission", "postgresqldatabasetopologyoperations",
        "databaseruntimeconnection", "database-topology-mounted.spec",
    )),
    ("INSTALLATION", (
        "projectpackages", "systemrecovery", "persistedruntime",
        "web/scada-web/src/engineering/installationswitchingworkspace",
        "src/scada.api/installation/",
    )),
    ("HA_DISTRIBUTED", ("highavailability", "high-availability", "/ha/", "distributedruntimefoundation")),
    ("EEE_PACKAGE", (".escadapkg", "projectpackage", "eee/", "export")),
    ("DRIVER_PROTOCOL", ("src/scada.drivers/", "driverhost", "gateway", "interop-lab/", "communication/")),
    ("DOCS_I18N_HELP", ("docs/", "readme", "web/scada-web/src/help/", "contextualhelp")),
    ("CI_INFRA", (".github/workflows/", "scripts/ci/", "tests/ci/")),
)

CORE = "tests/Scada.Core.Tests/Scada.Core.Tests.csproj"
HISTORIAN = "tests/Scada.Historian.TimescaleDb.Tests/Scada.Historian.TimescaleDb.Tests.csproj"
SECURITY = "tests/Scada.Security.Tests/Scada.Security.Tests.csproj"
DRIVERS = "tests/Scada.Drivers.Tests/Scada.Drivers.Tests.csproj"

DOTNET_PROJECTS = {
    "FOUNDATION_LIFECYCLE": (CORE,),
    "FOUNDATION_TIMING": (HISTORIAN,),
    "AUTHORITY_CORE": (SECURITY,),
    "AUTHORITY_UX": (SECURITY,),
    "SESSION_LICENSING": (DRIVERS,),
    "LICENSING_UX": (DRIVERS,),
    "SCRIPT_RUNTIME": (DRIVERS,),
    "RUNTIME_RENDERER": (DRIVERS,),
    "SCRIPT_ENGINEERING": (DRIVERS,),
    "DATABASE_TOPOLOGY": (DRIVERS, HISTORIAN, SECURITY),
    "INSTALLATION": (DRIVERS,),
    "HA_DISTRIBUTED": (DRIVERS, SECURITY),
    "EEE_PACKAGE": (DRIVERS,),
    "DRIVER_PROTOCOL": (DRIVERS,),
    "ELITEGO_RUNTIME": (DRIVERS,),
}

WEB_PROFILES = {
    "APP_SHELL", "UI_EDITOR", "SCRIPT_ENGINEERING", "SCRIPT_RUNTIME",
    "RUNTIME_RENDERER", "AUTHORITY_UX", "LICENSING_UX", "ELITEGO_RUNTIME",
    "DATABASE_TOPOLOGY", "INSTALLATION", "HA_DISTRIBUTED",
}

E2E_PROFILE_DEFAULTS = {
    "APP_SHELL": ("tests-e2e/app-shell.spec.ts", "tests-e2e/effective-capabilities-contract.spec.ts"),
    "UI_EDITOR": ("tests-e2e/app-shell.spec.ts", "tests-e2e/visual-editor-workspace.spec.ts"),
    "SCRIPT_ENGINEERING": ("tests-e2e/script-engineering-workspace-contract.spec.ts",),
    "SCRIPT_RUNTIME": ("tests-e2e/python-runtime-host.spec.ts",),
    "RUNTIME_RENDERER": ("tests-e2e/runtime.spec.ts", "tests-e2e/wave-14-c25-runtime-session.spec.ts"),
    "AUTHORITY_UX": ("tests-e2e/security.spec.ts", "tests-e2e/database-topology-mounted.spec.ts"),
    "LICENSING_UX": ("tests-e2e/effective-capabilities-contract.spec.ts",),
    "ELITEGO_RUNTIME": ("tests-e2e/runtime.spec.ts",),
    "DATABASE_TOPOLOGY": ("tests-e2e/database-topology-mounted.spec.ts",),
    "INSTALLATION": ("tests-e2e/local-auth.spec.ts",),
    "HA_DISTRIBUTED": ("tests-e2e/ha-admin-workspace.spec.ts",),
}

E2E_PATH_RULES = (
    (("web/scada-web/src/database-topology/",), ("tests-e2e/database-topology-mounted.spec.ts",)),
    (("web/scada-web/src/engineering/ha/",), ("tests-e2e/ha-admin-workspace.spec.ts",)),
    (("web/scada-web/src/runtime/application/",), (
        "tests-e2e/runtime.spec.ts", "tests-e2e/wave-14-c25-runtime-session.spec.ts",
    )),
    (("web/scada-web/src/runtime/historical-browser/",), ("tests-e2e/app-shell.spec.ts",)),
    ((
        "web/scada-web/src/appnavigation.tsx",
        "web/scada-web/src/main.tsx",
        "web/scada-web/src/appshell",
        "web/scada-web/src/engineering/engineeringapp.tsx",
        "effective-capabilities",
    ), ("tests-e2e/app-shell.spec.ts", "tests-e2e/effective-capabilities-contract.spec.ts")),
    ((
        "web/scada-web/src/engineering/visual-editor/",
        "visualeditorworkspace", "popupvisualeditorworkspace", "visual-editor",
    ), (
        "tests-e2e/visual-editor-workspace.spec.ts",
        "tests-e2e/visual-editor-authoring-model.spec.ts",
        "tests-e2e/visual-editor-selection-model.spec.ts",
        "tests-e2e/visual-editor-z-order-model.spec.ts",
    )),
    ((
        "web/scada-web/src/engineering/securedengineeringeditors",
        "web/scada-web/src/engineering/tag",
        "web/scada-web/src/engineering/datasource",
        "web/scada-web/src/engineering/structurededitors",
    ), ("tests-e2e/data-source-catalog-editor-mounted.spec.ts",)),
    (("web/scada-web/src/security/",), ("tests-e2e/security.spec.ts",)),
    (("web/scada-web/src/licensing/",), ("tests-e2e/effective-capabilities-contract.spec.ts",)),
    (("installationswitchingworkspace",), ("tests-e2e/local-auth.spec.ts",)),
    (("web/scada-web/src/engineering/scripts/",), ("tests-e2e/script-engineering-workspace-contract.spec.ts",)),
)

HA_TWO_PROCESS_FRAGMENTS = (
    "src/scada.api/runtime/runtimehighavailability",
    "src/scada.api/runtime/distributedruntimefoundation",
    "src/scada.security/authorization/runtimesessionleasestore",
    "tests/scada.drivers.tests/runtimehighavailability",
    "scripts/ci/ha_d1_two_process_evidence.py",
)


class ProfileError(ValueError):
    pass


def parse_profiles(value: str, source: str) -> set[str]:
    profiles = {item.strip().upper() for item in re.split(r"[,\s]+", value.strip()) if item.strip()}
    unknown = profiles.difference(VOCABULARY)
    if unknown:
        raise ProfileError(f"unknown {source} profile(s): {', '.join(sorted(unknown))}")
    return profiles


def declared_profiles(pr_body: str) -> set[str]:
    match = re.search(r"(?im)^\s*VALIDATION_PROFILE\s*:\s*(.+?)\s*$", pr_body)
    return parse_profiles(match.group(1), "declared") if match else set()


def _normalized(paths: list[str]) -> list[str]:
    return [path.replace("\\", "/").lower() for path in paths]


def infer_profiles(paths: list[str]) -> set[str]:
    inferred: set[str] = set()
    for normalized in _normalized(paths):
        # API Runtime needs a specific owner; HA and Runtime Session are not visual renderer work.
        if normalized.startswith("src/scada.api/runtime/"):
            if any(fragment in normalized for fragment in (
                "highavailability", "distributedruntimefoundation"
            )):
                inferred.add("HA_DISTRIBUTED")
            elif any(fragment in normalized for fragment in (
                "isolatedpythonscripthandlerexecutor", "serverscriptrunner", "serverscriptruntimemanager"
            )):
                inferred.add("SCRIPT_RUNTIME")
            elif "runtimesession" in normalized or "runtime-session" in normalized:
                inferred.add("SESSION_LICENSING")
            else:
                inferred.add("RUNTIME_RENDERER")

        for profile, fragments in PATH_RULES:
            if any(fragment in normalized for fragment in fragments):
                inferred.add(profile)
    return inferred


def infer_e2e_specs(paths: list[str], effective: set[str], mode: str) -> list[str]:
    specs: set[str] = set()
    normalized_paths = _normalized(paths)

    # A changed spec owns its own evidence, without pulling its old profile's entire suite.
    for path, normalized in zip(paths, normalized_paths):
        if normalized.startswith("web/scada-web/tests-e2e/") and normalized.endswith(".spec.ts"):
            specs.add(path.replace("\\", "/").removeprefix("web/scada-web/"))

    for normalized in normalized_paths:
        for fragments, owned_specs in E2E_PATH_RULES:
            if any(fragment in normalized for fragment in fragments):
                specs.update(owned_specs)

    web_changed = any(path.startswith("web/scada-web/") for path in normalized_paths)
    # Dispatch is explicit evidence. For PRs, profile defaults are a fallback only when
    # web product changed but no path owner was found.
    if mode == "dispatch" or (web_changed and not specs):
        for profile in effective:
            specs.update(E2E_PROFILE_DEFAULTS.get(profile, ()))

    return sorted(specs)


def needs_ha_two_process(paths: list[str], effective: set[str], mode: str) -> bool:
    if "HA_DISTRIBUTED" not in effective:
        return False
    if mode == "dispatch":
        return True
    return any(
        fragment in normalized
        for normalized in _normalized(paths)
        for fragment in HA_TWO_PROCESS_FRAGMENTS
    )


def is_coordination_only(paths: list[str]) -> bool:
    return bool(paths) and all(path.replace("\\", "/") in COORDINATION_ONLY for path in paths)


def classify(paths: list[str], pr_body: str, override: str = "", mode: str = "pr") -> dict[str, object]:
    if mode not in {"pr", "dispatch"}:
        raise ProfileError(f"unknown router mode: {mode}")

    declared = declared_profiles(pr_body)
    manual = parse_profiles(override, "manual override") if override.strip() else set()
    exempt = is_coordination_only(paths)
    inferred = infer_profiles(paths)

    if mode == "pr" and not declared and not exempt:
        raise ProfileError("missing required PR declaration: VALIDATION_PROFILE: <profile[,profile]>")
    if mode == "dispatch" and not manual and not inferred and not exempt:
        raise ProfileError("manual dispatch needs an inferred profile or an explicit override")

    effective = declared | manual | inferred
    if not effective and not exempt:
        raise ProfileError("no effective validation profile could be resolved")

    ordered = sorted(effective, key=ORDER.__getitem__)
    projects = sorted({
        project
        for profile in ordered
        for project in DOTNET_PROJECTS.get(profile, ())
    })
    specs = infer_e2e_specs(paths, effective, mode)
    normalized_paths = _normalized(paths)
    web_changed = any(path.startswith("web/scada-web/") for path in normalized_paths)
    driver = "DRIVER_PROTOCOL" in effective

    return {
        "declared_profiles": sorted(declared, key=ORDER.__getitem__),
        "manual_override_profiles": sorted(manual, key=ORDER.__getitem__),
        "inferred_profiles": sorted(inferred, key=ORDER.__getitem__),
        "effective_profiles": ordered,
        "coordination_exempt": exempt,
        "run_common_sanity": not exempt,
        "run_web": web_changed and bool(effective & WEB_PROFILES),
        "run_dotnet": bool(projects),
        "run_e2e": bool(specs),
        "run_driver": driver,
        "run_ha_two_process": needs_ha_two_process(paths, effective, mode),
        "dotnet_projects": projects,
        "e2e_specs": specs,
        "driver_gate": "DEFERRED TO T2/T3" if driver else "UNCHANGED/TRUSTED",
    }


def write_outputs(result: dict[str, object], path: str) -> None:
    lines: list[str] = []
    for key, value in result.items():
        if isinstance(value, bool):
            rendered = str(value).lower()
        elif isinstance(value, list):
            rendered = json.dumps(value, separators=(",", ":"))
        else:
            rendered = str(value)
        lines.append(f"{key}={rendered}")
    with open(path, "a", encoding="utf-8") as output:
        output.write("\n".join(lines) + "\n")


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--changed-files", required=True, type=Path)
    parser.add_argument("--pr-body-file", type=Path)
    parser.add_argument("--manual-override", default="")
    parser.add_argument("--mode", choices=("pr", "dispatch"), default="pr")
    parser.add_argument("--github-output", default=os.environ.get("GITHUB_OUTPUT", ""))
    parser.add_argument("--summary-file", type=Path)
    args = parser.parse_args(argv)

    paths = [line.strip() for line in args.changed_files.read_text(encoding="utf-8").splitlines() if line.strip()]
    body = args.pr_body_file.read_text(encoding="utf-8") if args.pr_body_file else ""

    try:
        result = classify(paths, body, args.manual_override, args.mode)
    except ProfileError as error:
        print(f"profile-router: {error}", file=sys.stderr)
        return 2

    rendered = json.dumps(result, indent=2, sort_keys=True)
    print(rendered)
    if args.github_output:
        write_outputs(result, args.github_output)
    if args.summary_file:
        args.summary_file.write_text(
            "## Wave 15 T1 classification\n\n```json\n" + rendered + "\n```\n",
            encoding="utf-8",
        )
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
