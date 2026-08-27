# CHAT WORK ASSIGNMENTS — EliteSCADA

> Authoritative live coordination board. GitHub branch/PR/head/CI state is operational truth; if this file briefly lags GitHub, GitHub wins and the coordinator reconciles it.

**Coordination protocol introduced:** 2026-08-26  
**Last coordinator synchronization:** 2026-08-27

## Permanent `siga` protocol

Before any action, every fixed EliteSCADA chat rereads current `main`: `PROJECT GOAL.md`, `LAST CHANGE.md`, `docs/ROADMAP.md`, `docs/PARALLEL-WORK.md`, this file, and every document listed in its current `MustReadSpecific`. Then it verifies the real assigned branch, PR/head and CI.

Workers never choose a new task, alter `main`, merge their own PR, work another DEV branch, or broaden their assignment. `WAIT_FOR_COORDINATOR` means stop after delivery.

Repository terminology:

- **MERGED** = official `main` state.
- **IMPLEMENTED IN PR** = functional implementation exists only in an open branch/PR.
- **RESEARCH IN PR** = research/specification exists only in an open PR and is not product implementation.
- **SPECIFIED / NOT IMPLEMENTED** = documented product intent without merged implementation.

---

# COORDENADOR - EliteSCADA

**Role:** `COORDINATOR`

**CurrentTask:** Integrate the first Interface Product Development checkpoint

**Branch:** `main` + `feature/interface-product-development`

**Status:** `IN_PROGRESS`

**Objective:**

Turn the merged worker UI primitives plus the coordinator product shell into one coherent industrial application experience across Runtime, Engineering and Audit. Keep new drivers and the provisional Windows presentation package postponed until this interface checkpoint is integrated and user-testable.

**AllowedScope:** coordinator-owned shared/central frontend shell/routing, `main.tsx`, `AppNavigation.tsx`, global/interface CSS, `EngineeringApp.tsx`, central localization/integration, browser tests, CI, assignment board, roadmap/handoff documentation and worker integration.

**ForbiddenScope:**

- no known-failing merge;
- no force-reset/discard of worker commits;
- no new production MQTT/OPC UA/BACnet/S7/Driver Module runtime during this block;
- no completion/handoff of the provisional Windows presentation package unless reprioritized;
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
- Interface worker slice DEV 3: PR #59 **MERGED** as `b0b58964f119f83356cf2edc8fecf5939fb905da`; exact-head CI #363 green.
- Interface worker slice DEV 1: PR #60 **MERGED** as `a7e6105fb65079ad1af8bcb56f8484225ff3dc8c`; exact-head CI #359 green.
- Interface worker slice DEV 2: PR #61 **MERGED** as `49c9e7261d63047b601f4b3c4f6e788168c8ee5c`; exact-head CI #360 green.
- Coordinator PR #58 remains Draft/Open on `feature/interface-product-development`; it contains the central product shell/navigation work and now requires reconciliation with current `main` plus worker integration.
- `integration/interface-validation-preview` remains **PARKED / NO PR / DO NOT MERGE YET**.
- PR #53 and PR #54 remain delivered research inputs, not production implementations.

**Dependencies:**

- canonical Engineering remains authoritative;
- merged worker components are isolated primitives and still require central composition;
- graphical HMI editor remains blocked by the Script/visual prerequisite chain;
- Windows validation packaging resumes after the interface reaches a materially useful validation state;
- additional drivers/protocols remain postponed.

**NextActions:**

1. reconcile `feature/interface-product-development` with current `main` without discarding PR #58 shell work;
2. integrate merged `UserSessionMenu` into the product shell;
3. integrate merged `EngineeringEntityBrowser` into the Engineering workspace without weakening Preview/Apply/CAS semantics;
4. integrate merged `RuntimeOperationsOverview` into Runtime while preserving the process demo;
5. normalize locale and visual behavior across the integrated surfaces;
6. extend Chromium coverage for the integrated UX;
7. run full Web/backend/smoke/Chromium CI on the reconciled candidate head;
8. merge PR #58 only when the integrated head is green and reviewed;
9. after integration, decide the next interface slice before assigning new worker missions;
10. keep drivers and provisional Windows packaging postponed unless product priority changes.

**AfterCompletion:** `CONTINUE_COORDINATION`

---

# DEV 1 - EliteSCADA

**Role:** `WORKER`

**CurrentTask:** Engineering workspace/entity-browser ergonomics primitives

**Branch:** `feature/engineering-workspace-ux`

**Status:** `MERGED / WAITING`

**PullRequest:** `#60 — MERGED / a7e6105fb65079ad1af8bcb56f8484225ff3dc8c`

**Delivered:**

- reusable controlled `EngineeringEntityBrowser<T>` master/detail primitive;
- search/filter, scalable list selection and keyboard navigation;
- explicit loading/empty/no-match/no-selection states;
- caller-owned authoritative Engineering values and selection;
- focused contract tests.

**Validation:** exact worker head `ab8e7e0b698a8533ea6a08048deb3c464840e843`; CI #359 **SUCCESS**.

**NextActions:** none. On `siga`, verify PR #60 is merged and report `MERGED / WAITING`. Do not begin another task until coordinator records a new assignment.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`

---

# DEV 2 - EliteSCADA

**Role:** `WORKER`

**CurrentTask:** Runtime operational overview UI primitives

**Branch:** `feature/runtime-operations-ux`

**Status:** `MERGED / WAITING`

**PullRequest:** `#61 — MERGED / 49c9e7261d63047b601f4b3c4f6e788168c8ee5c`

**Delivered:**

- isolated `RuntimeOperationsOverview` component;
- protected read-only diagnostics/Gateway/alarm aggregation;
- per-Data-Source communication health and TAG-quality summary;
- neutral handling of Simulation/no-external-source and restricted/partial diagnostics;
- loading/error/partial/empty states and localized copy;
- focused model/contract tests.

**Validation:** exact worker head `7fbd564c93af5ad3d4c83d2ddc8d5ed782d2957d`; CI #360 **SUCCESS**.

**NextActions:** none. On `siga`, verify PR #61 is merged and report `MERGED / WAITING`. Do not begin another task until coordinator records a new assignment.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`

---

# DEV 3 - EliteSCADA

**Role:** `WORKER`

**CurrentTask:** Authenticated session/user-menu UX primitive

**Branch:** `feature/session-ux`

**Status:** `MERGED / WAITING`

**PullRequest:** `#59 — MERGED / b0b58964f119f83356cf2edc8fecf5939fb905da`

**Delivered:**

- `UserSessionMenu` using the existing trusted `useAuth()` context;
- authenticated display identity, roles and logout;
- keyboard/Escape behavior and clean disabled-auth degradation;
- `pt-BR` / `en` / `es` presentation contract;
- no token/security/backend semantic changes;
- focused contract tests.

**Validation:** exact worker head `f0b120c3ec3e268b9c7875fc73450a150e1dda5a`; CI #363 **SUCCESS**.

**NextActions:** none. On `siga`, verify PR #59 is merged and report `MERGED / WAITING`. Research PR #54 remains separate. Do not begin another task until coordinator records a new assignment.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`
