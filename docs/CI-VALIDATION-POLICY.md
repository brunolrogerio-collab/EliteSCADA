# CI Validation Policy

**Status:** ACTIVE COORDINATION POLICY

This policy defines when EliteSCADA validation workflows run automatically and when a specialized workflow must be invoked manually. The objective is to preserve coverage while keeping GitHub state and CI execution proportional to the subsystem actually changed.

## 1. Universal acceptance gate

`EliteSCADA CI` is the universal acceptance gate for pull requests targeting `main`.

A change must not be merged to `main` with a red or incomplete required EliteSCADA CI result. This remains true when no specialized workflow is automatically selected.

As of 2026-09-01, GitHub branch protection / required status checks are not configured on `main`. Therefore this requirement is a Coordinator/Development Lead operational rule until repository protection is explicitly enabled. Documentation must not claim GitHub technically blocks a merge that violates this rule.

## 2. Preview Licensing CI

`Preview Licensing CI` is specialized product validation, not a universal PR pipeline.

It runs automatically for changes to licensing, License Generator, Demo/product capacity contracts, known shared persistence/TAG import boundaries that affect licensing, licensing tests, licensing policy/evidence documents, or the workflow itself.

It remains available through `workflow_dispatch` and must also be run manually when a cross-cutting change could materially affect licensing even if the changed path is outside the automatic matrix.

Run it for release/Preview integration validation whenever licensing or the Windows License Generator is part of the release evidence.

## 3. L3 Seven-Driver Lab

`L3 Seven-Driver Lab` is specialized integrated communication validation, not a universal PR pipeline.

It runs automatically for changes to Drivers, DriverHost, canonical communication/Data Source contracts, TAG Gateway, TAG/event core paths used by communication, Driver tests, interoperability lab infrastructure, or the workflow itself.

It remains available through `workflow_dispatch` and must be run manually for cross-cutting host/composition changes that can affect Driver startup, activation, readiness, TAG identity/event routing, Gateway behavior or communication security even when the exact file path is not in the automatic matrix.

Run it for full integration/release evidence whenever the release changes or depends materially on communication behavior.

## 4. Interop Lab Smoke

`Interop Lab Smoke` remains scoped to changes in `interop-lab/**` or its own workflow, with manual execution available when the common peer stack needs explicit validation.

## 5. Conservative override

Path filters are routing aids, not architectural truth.

If a change is structurally capable of affecting a specialized subsystem but does not match its automatic paths, the Coordinator must invoke the specialized workflow manually before merge. Examples include changes to top-level host composition, shared security/authorization, dependency/runtime configuration or release packaging that alter a specialized subsystem indirectly.

Do not broaden specialized workflow paths merely to avoid making this engineering judgment. Conversely, do not skip a specialized workflow merely because GitHub did not start it automatically.

## 6. Evidence and repository hygiene

Closed PRs, commits, Actions runs and retained artifacts are valid historical evidence. A PR does not need to remain open merely to preserve its evidence.

When a PR is superseded or its work has already been integrated through another PR, close it with a comment identifying the successor/integration lineage.

When a coordination issue has completed its acceptance purpose, close it. Future hardware, certification or release evidence should be tracked as explicit future/deferred issues rather than leaving completed convergence gates open.

Only genuinely active development/integration surfaces should remain open. This allows GitHub to communicate four states clearly: active, completed, superseded/historical, and explicitly deferred.

## 7. Branch hygiene

A closed/superseded branch may be deleted after its integration/evidence lineage is preserved by commits, PRs and Actions runs. Long-lived obsolete branches must not be interpreted as active work.

Branch deletion is a mechanical repository-maintenance action and is distinct from deleting historical evidence. Never rewrite or move old refs merely to make the branch list look clean.

## 8. Coverage invariant

This routing policy does **not** weaken the test bodies or acceptance criteria of any workflow. It changes automatic selection only.

The rule is:

`universal core CI + affected specialized CI + manual conservative override + explicit release/full-integration validation`.
