# CHAT WORK ASSIGNMENTS — EliteSCADA

> Authoritative live coordination board. GitHub branch/PR/head/CI state is operational truth; if this file briefly lags GitHub, GitHub wins and the coordinator reconciles it.

**Last coordinator synchronization:** 2026-08-27

## Permanent `siga` protocol

Before any action, every fixed EliteSCADA chat rereads current `main`: `PROJECT GOAL.md`, `LAST CHANGE.md`, `docs/ROADMAP.md`, `docs/PARALLEL-WORK.md`, this file, and every document listed in its current `MustReadSpecific`. Then it verifies the real assigned branch, PR/head and CI.

Workers never choose a new task, alter `main`, merge their own PR, work another DEV branch, or broaden their assignment. `WAIT_FOR_COORDINATOR` means stop after delivery.

Repository terminology:

- **MERGED** = official `main` state.
- **IMPLEMENTED IN PR** = functional implementation exists only in an open branch/PR.
- **RESEARCH MERGED / PRODUCTION NOT IMPLEMENTED** = research is part of official `main`, but no runtime/product capability is implied.
- **SPECIFIED / NOT IMPLEMENTED** = documented product intent without merged implementation.

## Current product gate

Active order remains:

`merged platform foundations -> INTERFACE PRODUCT DEVELOPMENT -> USER INTERFACE VALIDATION BUILD/PACKAGE -> additional external protocols`

Driver SDK research convergence is **MERGED** through PR #68. It is architectural hardening only and does not authorize production external protocol implementation.

Current `main` at this synchronization: `8b5060e42c0d03eddb950a32e745d1273cf19f30`.

---

# COORDENADOR - EliteSCADA

**Role:** `COORDINATOR`

**CurrentTask:** Reconcile, review and integrate the second Interface Product Development wave

**Branch:** `main` plus coordinator-owned integration branches as needed

**Status:** `ACTIVE`

**Objective:**

Bring PRs #65, #66 and #67 onto current `main`, verify their exact delivered scope and CI, then integrate them into the common product experience without opening a new product wave prematurely.

**AllowedScope:** shared shell/routing/integration, `main.tsx`, `AppNavigation.tsx`, `EngineeringApp.tsx`, cross-product localization/visual system, worker integration, browser tests, CI, assignment board, roadmap/handoff docs.

**ForbiddenScope:**

- no known-failing merge;
- no new production MQTT/OPC UA/BACnet/S7/Allen-Bradley/Driver Module runtime in this block;
- no production Python engine/editor or graphical Screen/Popup/Dynamo editor;
- no frontend-only security or private Engineering truth;
- no weakening Preview/Apply/CAS, TAG quality, Audit or source-provider boundaries;
- no provisional Windows validation package until interface maturity is reviewed again.

**MustReadSpecific:**

- `docs/INTERFACE-DEVELOPMENT.md`
- `docs/INTERFACE-VALIDATION-MILESTONE.md`
- `docs/RESEARCH-CONVERGENCE-READINESS.md`
- `docs/DRIVER-SDK-RESEARCH-CONVERGENCE.md`

**ObservedGitHubState:**

- PR #65 Engineering Alarm Workspace: delivered green worker head, currently diverged and 17 commits behind `main`;
- PR #66 Audit workspace ergonomics: delivered green worker head, currently diverged and 17 commits behind `main`;
- PR #67 Runtime Alarm Center: delivered green worker head, currently diverged and 17 commits behind `main`;
- PR #68 Driver SDK research convergence: **MERGED**;
- Engineering Schema remains v9;
- production external protocol expansion remains postponed.

**NextActions:**

1. workers reconcile only their existing PRs with current `main`;
2. review each reconciled PR against exact assignment and changed-file scope;
3. require exact-head Web/backend/test/smoke/Chromium CI before merge;
4. integrate Runtime Alarm Center centrally where required;
5. perform cross-product browser acceptance after worker merges;
6. reassess interface maturity for the Windows validation package only after the second wave is integrated.

**AfterCompletion:** `CONTINUE_COORDINATION`

---

# DEV 1 - EliteSCADA

**Role:** `WORKER`

**CurrentTask:** Reconcile PR #65 Engineering Alarm Workspace with current `main`

**Branch:** `feature/interface-engineering-alarm-workspace`

**Status:** `ASSIGNED`

**Objective:**

Reconcile the already delivered Engineering Alarm Workspace implementation with current `main` after PR #68 and continuity-document updates, preserving the previously approved worker scope and behavior.

**AllowedScope:**

- update/rebase/merge current `main` into the assigned branch using normal Git reconciliation;
- resolve conflicts only inside files already changed by PR #65 or mechanically required by the reconciliation;
- adjust the existing Alarm workspace only where current-main API/types/build changes require it;
- dedicated existing tests for this slice;
- update PR #65 description/evidence with the new exact head and CI result.

**ForbiddenScope:**

- no new feature beyond the delivered Alarm workspace;
- no `main.tsx`, `AppNavigation.tsx`, central routing or product shell;
- no backend/API/schema/persistence changes;
- no Alarm mutation semantic change;
- no Runtime Alarm Center work;
- no protocol/Python/graphical-editor work;
- no merge of own PR and no modification of `main`.

**MustReadSpecific:**

- `docs/INTERFACE-DEVELOPMENT.md`
- `docs/ADR-004-ENGINEERING-IMPORT-EXPORT.md`
- `docs/RESEARCH-CONVERGENCE-READINESS.md`
- current PR #65 changed files and current-main equivalents.

**CompletionCriteria:**

1. branch is reconciled with current `main` and no longer behind it;
2. original PR #65 feature behavior and scope are preserved;
3. changed-file delta remains limited to the assigned Engineering Alarm slice plus unavoidable reconciliation metadata;
4. exact-head Web/backend/test/smoke/Chromium CI is green;
5. PR #65 records the reconciled head and evidence;
6. then `WAIT_FOR_COORDINATOR`.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`

---

# DEV 2 - EliteSCADA

**Role:** `WORKER`

**CurrentTask:** Reconcile PR #67 Runtime Alarm Center with current `main`

**Branch:** `feature/interface-runtime-alarm-center`

**Status:** `ASSIGNED`

**Objective:**

Reconcile the already delivered Runtime Alarm Center + protected acknowledgement UX with current `main`, preserving backend authority and the previously delivered isolated Runtime slice.

**AllowedScope:**

- update/rebase/merge current `main` into the assigned branch using normal Git reconciliation;
- resolve conflicts only inside files already changed by PR #67 or mechanically required by the reconciliation;
- adjust existing Runtime Alarm Center code only where current-main types/build/API contracts require it;
- dedicated existing tests for this slice;
- update PR #67 description/evidence with the new exact head and CI result.

**ForbiddenScope:**

- no new Runtime feature beyond the delivered Alarm Center;
- no central `AppNavigation.tsx`, routing or `main.tsx` integration;
- no backend authorization/Alarm-engine change;
- no fake/optimistic acknowledgement;
- no direct driver access;
- no protocol/Python/graphical-editor work;
- no merge of own PR and no modification of `main`.

**MustReadSpecific:**

- `docs/INTERFACE-DEVELOPMENT.md`
- `docs/RESEARCH-CONVERGENCE-READINESS.md`
- current PR #67 changed files;
- current Alarm API/auth/session contracts from `main`.

**CompletionCriteria:**

1. branch is reconciled with current `main` and no longer behind it;
2. existing protected ACK behavior remains backend-authoritative;
3. original PR #67 functionality and scope are preserved without central integration;
4. exact-head Web/backend/test/smoke/Chromium CI is green;
5. PR #67 records the reconciled head and evidence;
6. then `WAIT_FOR_COORDINATOR`.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`

---

# DEV 3 - EliteSCADA

**Role:** `WORKER`

**CurrentTask:** Reconcile PR #66 Audit Workspace with current `main`

**Branch:** `feature/interface-audit-workspace`

**Status:** `ASSIGNED`

**Objective:**

Reconcile the already delivered Audit workspace ergonomics with current `main`, preserving keyset pagination, backend filtering, diagnostics and `SystemAdmin` enforcement exactly as delivered.

**AllowedScope:**

- update/rebase/merge current `main` into the assigned branch using normal Git reconciliation;
- resolve conflicts only inside files already changed by PR #66 or mechanically required by the reconciliation;
- adjust existing Audit UI only where current-main build/types/contracts require it;
- dedicated existing Audit tests;
- update PR #66 description/evidence with the new exact head and CI result.

**ForbiddenScope:**

- no new Audit feature beyond the delivered ergonomics slice;
- no backend authorization/retention/storage changes;
- no weakening query/date/cursor semantics;
- no central shell/routing changes;
- no new AuditRead capability;
- no protocol/Python/graphical-editor work;
- no merge of own PR and no modification of `main`.

**MustReadSpecific:**

- `docs/INTERFACE-DEVELOPMENT.md`
- `docs/RESEARCH-CONVERGENCE-READINESS.md`
- current PR #66 changed files and current-main Audit equivalents.

**CompletionCriteria:**

1. branch is reconciled with current `main` and no longer behind it;
2. original PR #66 behavior, backend contract and keyset semantics are preserved;
3. changed-file delta remains within the assigned Audit slice plus unavoidable reconciliation metadata;
4. exact-head Web/backend/test/smoke/Chromium CI is green;
5. PR #66 records the reconciled head and evidence;
6. then `WAIT_FOR_COORDINATOR`.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`
