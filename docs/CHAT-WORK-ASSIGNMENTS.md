# CHAT WORK ASSIGNMENTS — EliteSCADA

> Authoritative live coordination board. GitHub branch/PR/head/CI state is operational truth; if this file briefly lags GitHub, GitHub wins and the coordinator reconciles it.

**Coordination protocol introduced:** 2026-08-26  
**Last coordinator synchronization:** 2026-08-27

## Permanent `siga` protocol

Before any action, every fixed EliteSCADA chat rereads current `main`: `PROJECT GOAL.md`, `LAST CHANGE.md`, `docs/ROADMAP.md`, `docs/PARALLEL-WORK.md`, this file, and every document listed in its current `MustReadSpecific`. Then it verifies real branch, PR/head and CI state.

Workers never choose a new task, alter `main`, merge their own PR, work another DEV branch, or broaden their assignment. `WAIT_FOR_COORDINATOR` means stop after delivery.

Repository terminology:

- **MERGED** = official `main` state.
- **IMPLEMENTED IN PR** = functional implementation exists only in an open branch/PR.
- **RESEARCH IN PR** = research/specification exists only in an open PR and is not product implementation.
- **SPECIFIED / NOT IMPLEMENTED** = documented product intent without merged implementation.

---

# COORDENADOR - EliteSCADA

**Role:** `COORDINATOR`

**CurrentTask:** Review delivered future-protocol research and continue Interface Product Development

**Branch:** `main`

**Status:** `RESEARCH REVIEW / INTERFACE DEVELOPMENT ACTIVE`

**Objective:**

Keep the merged first interface checkpoint stable, review and reconcile the delivered future-protocol research PRs as research-only inputs, and continue coordination of the active Interface Product Development block. Production protocol implementation and the deferred Windows validation package remain behind their product gates.

**AllowedScope:** coordinator-owned shared/central frontend shell/routing, `main.tsx`, `AppNavigation.tsx`, global/interface CSS, `EngineeringApp.tsx`, central localization/integration, browser tests, CI, assignment board, roadmap/handoff documentation, worker integration, research review and merge/reconciliation work.

**ForbiddenScope:**

- no known-failing merge;
- no force-reset/discard of worker commits;
- no new production MQTT/OPC UA/BACnet/S7/Allen-Bradley/Driver Module runtime while the product gate remains closed;
- no completion/handoff of the provisional Windows validation package unless reprioritized;
- no frontend-only security decisions;
- no private Engineering truth;
- no production graphical Screen/Popup/Dynamo editor or Python engine/editor ahead of the locked prerequisite chain.

**MustReadSpecific:**

- `docs/INTERFACE-DEVELOPMENT.md`
- `docs/INTERFACE-VALIDATION-MILESTONE.md`
- `docs/PYTHON-SCRIPTING-AND-VISUAL-RUNTIME.md`
- `docs/COMMUNICATION-DRIVER-DIAGNOSTICS.md`
- `docs/TAG-GATEWAY.md`
- `docs/INTERNAL-MEMORY-TAGS.md`

**ObservedGitHubState:**

- Internal Memory: **MERGED / COMPLETE** through PR #49.
- TAG Gateway: **MERGED / COMPLETE** through PRs #50 and #55.
- Common communication diagnostics: **MERGED / COMPLETE** through PRs #56 and #57.
- Engineering Schema: **v9**.
- Interface DEV 3 PR #59: **MERGED** (`UserSessionMenu`).
- Interface DEV 1 PR #60: **MERGED** (`EngineeringEntityBrowser`).
- Interface DEV 2 PR #61: **MERGED** (`RuntimeOperationsOverview`).
- Coordinator PR #58: **MERGED** as `f3cc82f0d45a9f0162105b57ae6c42f643af6160` after exact-head CI #378 green.
- Future protocol research delivered as Draft research PRs: #62 BACnet/IP + BACnet/SC, #63 MQTT, #64 Allen-Bradley EtherNet/IP/CIP Logix. Each original worker head changed exactly one research document and had exact-head green CI before coordinator reconciliation.
- `integration/interface-validation-preview`: **PARKED / NO PR / DO NOT MERGE YET**.
- PR #53 and PR #54 remain delivered research inputs, not production implementations.

**Dependencies:**

- canonical Engineering remains authoritative;
- research may reduce future uncertainty but may not register production Data Sources or alter active runtime composition;
- graphical HMI editor remains blocked by the Script/visual prerequisite chain;
- Windows validation packaging resumes only after the interface reaches a materially useful validation state;
- production additional drivers/protocols remain postponed until the product gate is reopened.

**NextActions on coordinator `siga`:**

1. verify current `main`, open PRs, research branch heads and exact-head CI;
2. reconcile research PRs #62/#63/#64 with current `main` without modifying their research content, rerun exact-head CI, and merge only fully green semantically sound research;
3. classify any merged research strictly as **RESEARCH ONLY / PRODUCTION NOT IMPLEMENTED**;
4. continue Interface Product Development from `docs/INTERFACE-DEVELOPMENT.md` and `docs/ROADMAP.md` without regressing the merged first checkpoint;
5. if a new worker/interface split is needed, record it here before a DEV starts it;
6. keep the deferred Windows package and production external protocols behind their product gates.

**AfterCompletion:** `CONTINUE_COORDINATION`

---

# DEV 1 - EliteSCADA

**Role:** `WORKER`

**CurrentTask:** MQTT industrial Data Source/driver architecture research

**Branch:** `research/mqtt-industrial-driver`

**Status:** `READY_FOR_COORDINATOR_REVIEW — RESEARCH ONLY`

**PullRequest:** `#63` Draft — `RESEARCH IN PR / PRODUCTION NOT IMPLEMENTED`

**ExpectedPrimaryDeliverable:** `docs/research/mqtt/MQTT-INDUSTRIAL-DRIVER-RESEARCH.md`

**DeliveredHead:** `36dd986b12895974cd3c2f736be8d02322521c0f` before coordinator reconciliation.

**DeliveredEvidence:** exactly one research document; exact-head CI #376 green; no production source/package/schema/DI/API/frontend/workflow/runtime changes.

**MustReadSpecific:**

- `docs/COMMUNICATION-DRIVER-DIAGNOSTICS.md`
- `docs/TAG-GATEWAY.md`
- `docs/ADR-004-ENGINEERING-IMPORT-EXPORT.md`
- `docs/OPC-UA.md`
- `docs/research/opc-ua/OPC-UA-DISCOVERY-IMPORT-RESEARCH.md`

**AfterCompletion:** `WAIT_FOR_COORDINATOR`

---

# DEV 2 - EliteSCADA

**Role:** `WORKER`

**CurrentTask:** Allen-Bradley EtherNet/IP + CIP / Logix driver architecture research

**Branch:** `research/allen-bradley-ethernet-ip`

**Status:** `READY_FOR_COORDINATOR_REVIEW — RESEARCH ONLY`

**PullRequest:** `#64` Draft — `RESEARCH IN PR / PRODUCTION NOT IMPLEMENTED`

**ExpectedPrimaryDeliverable:** `docs/research/allen-bradley/ALLEN-BRADLEY-ETHERNET-IP-CIP-RESEARCH.md`

**DeliveredHead:** `3256546e8fb77d3eb2c1b91629383d7e8d836e4b` before coordinator reconciliation.

**DeliveredEvidence:** exactly one research document; exact-head CI #377 green; no production source/package/schema/DI/API/frontend/workflow/runtime changes.

**MustReadSpecific:**

- `docs/COMMUNICATION-DRIVER-DIAGNOSTICS.md`
- `docs/TAG-GATEWAY.md`
- `docs/S7-ISO-CONNECTION.md`
- `docs/research/s7/S7-ISO-CONNECTION-RESEARCH.md`
- `docs/ADR-004-ENGINEERING-IMPORT-EXPORT.md`

**AfterCompletion:** `WAIT_FOR_COORDINATOR`

---

# DEV 3 - EliteSCADA

**Role:** `WORKER`

**CurrentTask:** BACnet/IP + BACnet Secure Connect driver architecture research

**Branch:** `research/bacnet-ip-secure-connect`

**Status:** `READY_FOR_COORDINATOR_REVIEW — RESEARCH ONLY`

**PullRequest:** `#62` Draft — `RESEARCH IN PR / PRODUCTION NOT IMPLEMENTED`

**ExpectedPrimaryDeliverable:** `docs/research/bacnet/BACNET-IP-SC-RESEARCH.md`

**DeliveredHead:** `712bf5a7918b77a2df33c4f9822bb1ce86760fda` before coordinator reconciliation.

**DeliveredEvidence:** exactly one research document; exact-head CI #375 green after same-head rerun of timing-sensitive unrelated tests; no production source/package/schema/DI/API/frontend/workflow/runtime changes.

**MustReadSpecific:**

- `docs/COMMUNICATION-DRIVER-DIAGNOSTICS.md`
- `docs/TAG-GATEWAY.md`
- `docs/OPC-UA.md`
- `docs/research/opc-ua/OPC-UA-DISCOVERY-IMPORT-RESEARCH.md`
- `docs/ADR-004-ENGINEERING-IMPORT-EXPORT.md`

**AfterCompletion:** `WAIT_FOR_COORDINATOR`
