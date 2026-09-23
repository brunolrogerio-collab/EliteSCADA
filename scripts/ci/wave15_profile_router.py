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
    "SESSION_LICENSING", "SCRIPT_RUNTIME", "RUNTIME_RENDERER", "UI_EDITOR",
    "SCRIPT_ENGINEERING", "AUTHORITY_UX", "LICENSING_UX", "ELITEGO_RUNTIME",
    "INSTALLATION", "HA_DISTRIBUTED", "DOCS_I18N_HELP", "EEE_PACKAGE",
    "DRIVER_PROTOCOL",
)
ORDER = {profile: index for index, profile in enumerate(VOCABULARY)}

# Deliberately narrow: ordinary documentation still requires a declaration.
COORDINATION_ONLY = {
    "LAST CHANGE.md", "docs/CURRENT-COORDINATOR-HANDOFF.md",
    "docs/NEXT-COORDINATOR-CHAT-HANDOFF.md", "docs/ROADMAP.md",
    "docs/WAVE15-MAIN-COORDINATOR-HANDOFF.md",
}

PATH_RULES = (
    ("AUTHORITY_CORE", ("src/scada.security/", "src/scada.api/security/", "tests/scada.security.tests/")),
    ("SESSION_LICENSING", ("licensing/", "runtimesession", "runtime-session", "license")),
    ("SCRIPT_RUNTIME", (
        "src/scada.runtime/", "python-runtime", "script-runtime", "scripting/runtime",
        "src/scada.api/runtime/isolatedpythonscripthandlerexecutor.cs",
        "src/scada.api/runtime/serverscriptrunner.py",
        "src/scada.api/runtime/serverscriptruntimemanager.cs",
    )),
    ("SCRIPT_ENGINEERING", (
        "src/scada.engineering/", "script-engineering", "python-editor", "python-script", "tag-reference",
        "web/scada-web/src/engineering/scripts/",
    )),
    ("RUNTIME_RENDERER", ("visual-runtime", "renderer", "src/scada.runtime/", "web/scada-web/src/runtime/")),
    ("UI_EDITOR", ("web/scada-web/src/engineering/", "visual-editor", "screen-editor")),
    ("AUTHORITY_UX", ("web/scada-web/src/security/", "effective-capabilities", "user-administration", "security.spec")),
    ("LICENSING_UX", ("web/scada-web/src/licensing/", "license-generator", "licensing")),
    ("ELITEGO_RUNTIME", ("elitego",)),
    ("INSTALLATION", ("projectpackages", "systemrecovery", "installation", "persistedruntime")),
    ("HA_DISTRIBUTED", ("highavailability", "high-availability", "/ha/", "distributed")),
    ("EEE_PACKAGE", (".escadapkg", "projectpackage", "eee/", "export")),
    ("DRIVER_PROTOCOL", ("src/scada.drivers/", "driverhost", "gateway", "interop-lab/", "communication/")),
    ("DOCS_I18N_HELP", ("docs/", "readme", "i18n", "localization", "help")),
)

DOTNET_PROJECTS = {
    "FOUNDATION_LIFECYCLE": "tests/Scada.Core.Tests/Scada.Core.Tests.csproj",
    "FOUNDATION_TIMING": "tests/Scada.Historian.TimescaleDb.Tests/Scada.Historian.TimescaleDb.Tests.csproj",
    "AUTHORITY_CORE": "tests/Scada.Security.Tests/Scada.Security.Tests.csproj",
    "AUTHORITY_UX": "tests/Scada.Security.Tests/Scada.Security.Tests.csproj",
    "SESSION_LICENSING": "tests/Scada.Drivers.Tests/Scada.Drivers.Tests.csproj",
    "LICENSING_UX": "tests/Scada.Drivers.Tests/Scada.Drivers.Tests.csproj",
    "SCRIPT_RUNTIME": "tests/Scada.Drivers.Tests/Scada.Drivers.Tests.csproj",
    "RUNTIME_RENDERER": "tests/Scada.Drivers.Tests/Scada.Drivers.Tests.csproj",
    "SCRIPT_ENGINEERING": "tests/Scada.Drivers.Tests/Scada.Drivers.Tests.csproj",
    "INSTALLATION": "tests/Scada.Drivers.Tests/Scada.Drivers.Tests.csproj",
    "HA_DISTRIBUTED": "tests/Scada.Drivers.Tests/Scada.Drivers.Tests.csproj",
    "EEE_PACKAGE": "tests/Scada.Drivers.Tests/Scada.Drivers.Tests.csproj",
    "DRIVER_PROTOCOL": "tests/Scada.Drivers.Tests/Scada.Drivers.Tests.csproj",
    "ELITEGO_RUNTIME": "tests/Scada.Drivers.Tests/Scada.Drivers.Tests.csproj",
}
WEB_PROFILES = {"UI_EDITOR", "SCRIPT_ENGINEERING", "SCRIPT_RUNTIME", "RUNTIME_RENDERER", "AUTHORITY_UX", "LICENSING_UX", "ELITEGO_RUNTIME"}
E2E_SPECS = {
    "UI_EDITOR": "tests-e2e/visual-editor-workspace.spec.ts",
    "SCRIPT_ENGINEERING": "tests-e2e/script-engineering-workspace-contract.spec.ts",
    "SCRIPT_RUNTIME": "tests-e2e/python-runtime-host.spec.ts",
    "RUNTIME_RENDERER": "tests-e2e/runtime.spec.ts",
    "AUTHORITY_UX": "tests-e2e/security.spec.ts",
    "LICENSING_UX": "tests-e2e/effective-capabilities-contract.spec.ts",
    "ELITEGO_RUNTIME": "tests-e2e/runtime.spec.ts",
}


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


def infer_profiles(paths: list[str]) -> set[str]:
    inferred: set[str] = set()
    for path in paths:
        normalized = path.replace("\\", "/").lower()
        for profile, fragments in PATH_RULES:
            if any(fragment in normalized for fragment in fragments):
                inferred.add(profile)
    return inferred


def is_coordination_only(paths: list[str]) -> bool:
    return bool(paths) and all(path.replace("\\", "/") in COORDINATION_ONLY for path in paths)


def classify(paths: list[str], pr_body: str, override: str = "") -> dict[str, object]:
    declared = declared_profiles(pr_body)
    manual = parse_profiles(override, "manual override") if override.strip() else set()
    exempt = is_coordination_only(paths)
    if not declared and not manual and not exempt:
        raise ProfileError("missing required PR declaration: VALIDATION_PROFILE: <profile[,profile]>")
    inferred = infer_profiles(paths)
    effective = declared | manual | inferred
    if not effective and not exempt:
        raise ProfileError("no effective validation profile could be resolved")
    ordered = sorted(effective, key=ORDER.__getitem__)
    projects = sorted({DOTNET_PROJECTS[p] for p in ordered if p in DOTNET_PROJECTS})
    specs = sorted({E2E_SPECS[p] for p in ordered if p in E2E_SPECS})
    driver = "DRIVER_PROTOCOL" in effective
    return {
        "declared_profiles": sorted(declared, key=ORDER.__getitem__),
        "manual_override_profiles": sorted(manual, key=ORDER.__getitem__),
        "inferred_profiles": sorted(inferred, key=ORDER.__getitem__),
        "effective_profiles": ordered,
        "coordination_exempt": exempt,
        "run_common_sanity": not exempt,
        "run_web": bool(effective & WEB_PROFILES),
        "run_dotnet": bool(projects),
        "run_e2e": bool(specs),
        "run_driver": driver,
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
    parser.add_argument("--github-output", default=os.environ.get("GITHUB_OUTPUT", ""))
    parser.add_argument("--summary-file", type=Path)
    args = parser.parse_args(argv)
    paths = [line.strip() for line in args.changed_files.read_text(encoding="utf-8").splitlines() if line.strip()]
    body = args.pr_body_file.read_text(encoding="utf-8") if args.pr_body_file else ""
    try:
        result = classify(paths, body, args.manual_override)
    except ProfileError as error:
        print(f"profile-router: {error}", file=sys.stderr)
        return 2
    rendered = json.dumps(result, indent=2, sort_keys=True)
    print(rendered)
    if args.github_output:
        write_outputs(result, args.github_output)
    if args.summary_file:
        args.summary_file.write_text("## Wave 15 T1 classification\n\n```json\n" + rendered + "\n```\n", encoding="utf-8")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
