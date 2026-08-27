# CHAT WORK ASSIGNMENTS — EliteSCADA

> Authoritative live coordination board. GitHub branch/PR/head/CI state is operational truth; if this file briefly lags GitHub, GitHub wins and the coordinator reconciles it.

**Last coordinator synchronization:** 2026-08-27

## Permanent `siga` protocol

Before any action, every fixed EliteSCADA chat rereads current `main`: `PROJECT GOAL.md`, `LAST CHANGE.md`, `docs/ROADMAP.md`, `docs/PARALLEL-WORK.md`, this file, and every document listed in its current `MustReadSpecific`. Then it verifies the real assigned branch, PR/head and CI.

Workers never choose a new task, alter `main`, merge their own PR, work another DEV branch, or broaden their assignment. `WAIT_FOR_COORDINATOR` means stop after delivery.

Repository terminology:

- **MERGED** = official `main` state.
- **IMPLEMENTED IN PR** = functional implementation exists only in an open branch/PR.
- **RESEARCH IN PR** = research/specification exists only in an open PR and is not product implementation.
- **RESEARCH MERGED / PRODUCTION NOT IMPLEMENTED** = research is part of official `main`, but no runtime/product capability is implied.
- **SPECIFIED / NOT IMPLEMENTED** = documented product intent without merged implementation.

## Current product gate

Active order remains:

`merged platform foundations -> INTERFACE PRODUCT DEVELOPMENT -> USER INTERFACE VALIDATION BUILD/PACKAGE -> additional external protocols`

The following research is now **MERGED as research only**:

- PR #53 graphical Screen/Popup/Dynamo editor architecture;
- PR #54 Client Visual Python editor/browser sandbox;
- PR #62 BACnet/IP + BACnet/SC architecture;
- PR #63 MQTT industrial Data Source architecture;
- PR #64 Allen-Bradley EtherNet/IP/CIP Logix architecture.

None of those merges authorizes production graphical editing, Python runtime/editor, MQTT, BACnet or Allen-Bradley implementation.

---

# COORDENADOR - EliteSCADA

**Role:** `COORDINATOR`

**CurrentTask:** Coordinate and integrate the second Interface Product Development wave

**Branch:** `main` plus coordinator-owned integration branches as needed

**Status:** `ACTIVE`

**Objective:**

Turn the first merged interface checkpoint into a more useful industrial product by coordinating three isolated worker slices: scalable Engineering alarm navigation, Runtime alarm operations/acknowledgement, and Audit workspace ergonomics. Preserve the common product shell and all backend security/Engineering boundaries.

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
- `docs/PYTHON-SCRIPTING-AND-VISUAL-RUNTIME.md`
- `docs/COMMUNICATION-DRIVER-DIAGNOSTICS.md`
- `docs/TAG-GATEWAY.md`

**ObservedGitHubState:**

- first integrated interface checkpoint: **MERGED** through PR #58;
- research PRs #53, #54, #62, #63 and #64: **RESEARCH MERGED / PRODUCTION NOT IMPLEMENTED**;
- `integration/interface-validation-preview`: parked;
- Engineering Schema remains v9;
- production external protocol expansion remains postponed.

**NextActions:**

1. keep worker scopes isolated;
2. review each worker PR against exact assignment and current `main`;
3. require exact-head Web/backend/test/smoke/Chromium CI before merge;
4. integrate worker surfaces into the common product experience where central composition is required;
5. after this second interface wave, reassess whether the UI is mature enough for the Windows validation package.

**AfterCompletion:** `CONTINUE_COORDINATION`

---

# DEV 1 - EliteSCADA

**Role:** `WORKER`

**CurrentTask:** Engineering Alarm Workspace ergonomics

**Branch:** `feature/interface-engineering-alarm-workspace`

**Status:** `ASSIGNED`

**Objective:**

Create a scalable Engineering alarm-navigation/master-detail primitive that brings Alarm definitions to the same usability level as TAG/Data Source browsing without changing canonical Engineering authority or mutation semantics.

**AllowedScope:**

- new or narrowly related files under `web/scada-web/src/engineering/**` for an Alarm browser/workspace primitive;
- reuse `EngineeringEntityBrowser` and current public `EngineeringPackageView` data;
- dedicated browser/contract tests under `web/scada-web/tests-e2e/**` where needed;
- localization for `pt-BR`, `en`, `es` within the new surface.

**ForbiddenScope:**

- `main.tsx`, `AppNavigation.tsx`, central routing or product shell;
- backend/API/schema/persistence changes;
- changing Alarm mutation Preview/Apply/CAS behavior;
- runtime alarm acknowledgement;
- production Screen/Popup/Dynamo or Python work;
- changing `main` or merging own PR.

**MustReadSpecific:**

- `docs/INTERFACE-DEVELOPMENT.md`
- `docs/ADR-004-ENGINEERING-IMPORT-EXPORT.md`
- current `EngineeringEntityBrowser.tsx`, `StructuredEditors.tsx`, Alarm editor/mutation components and Engineering types.

**CompletionCriteria:**

1. searchable Alarm definition list suitable for projects larger than demo size;
2. useful filters derived only from canonical Alarm fields, such as area/severity/type where available;
3. selected Alarm master-detail summary with stable identity, TAG reference and important Alarm configuration;
4. loading/empty/no-match/selection states consistent with existing browser primitives;
5. keyboard/focus behavior and `pt-BR`/`en`/`es` copy;
6. no private copy of Engineering truth and no mutation outside current protected editors;
7. Draft PR, exact-head CI green, then `WAIT_FOR_COORDINATOR`.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`

---

# DEV 2 - EliteSCADA

**Role:** `WORKER`

**CurrentTask:** Runtime Alarm Center and protected acknowledgement UX

**Branch:** `feature/interface-runtime-alarm-center`

**Status:** `ASSIGNED`

**Objective:**

Build an isolated Runtime alarm-operations surface using the existing protected Alarm APIs, including honest active-alarm visibility and authorized acknowledgement, without changing backend authority.

**AllowedScope:**

- new/narrow files under `web/scada-web/src/runtime/**` for Alarm API/model/component styling;
- existing `/api/alarms?activeOnly=true` and `/api/alarms/{id}/ack` contracts;
- current authenticated session behavior and backend authorization results;
- dedicated E2E/contract tests under `web/scada-web/tests-e2e/**`.

**ForbiddenScope:**

- backend authorization changes or frontend-only permission decisions;
- altering Alarm engine semantics;
- direct driver access;
- central `AppNavigation.tsx` / routing / `main.tsx` integration;
- fake acknowledgement or optimistic success when backend rejects;
- production protocol/Python/graphical-editor work;
- changing `main` or merging own PR.

**MustReadSpecific:**

- `docs/INTERFACE-DEVELOPMENT.md`
- current `RuntimeOperationsOverview.tsx`, `operationsApi.ts`, Alarm API endpoints in `src/Scada.Api/Program.cs`, auth/session components and existing security tests.

**CompletionCriteria:**

1. active Alarm list sorted for operational attention using actual Alarm fields;
2. show alarm identity/message/state/area/time/acknowledgement information available from backend;
3. acknowledge action calls protected backend endpoint and refreshes from authoritative state after success;
4. 401/403/404/error states remain explicit and never become fake success;
5. no acknowledgement button is treated as a security boundary by itself;
6. localized `pt-BR`/`en`/`es`, keyboard/focus accessible;
7. Draft PR, exact-head CI green, then `WAIT_FOR_COORDINATOR`.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`

---

# DEV 3 - EliteSCADA

**Role:** `WORKER`

**CurrentTask:** Audit workspace ergonomics and cross-product consistency

**Branch:** `feature/interface-audit-workspace`

**Status:** `ASSIGNED`

**Objective:**

Evolve the existing `/audit` route from a dense technical page into a productive desktop audit workspace while preserving current keyset pagination, backend filters, diagnostics and `SystemAdmin` enforcement.

**AllowedScope:**

- `web/scada-web/src/audit/**`;
- Audit-specific E2E/contract tests;
- layout, filtering ergonomics, result presentation, selected-event detail, diagnostics presentation and localization.

**ForbiddenScope:**

- changing Audit backend authorization/retention/storage contracts;
- weakening bounded date/query validation or opaque cursor handling;
- central shell/routing changes;
- new general AuditRead capability in this slice;
- production protocol/Python/graphical-editor work;
- changing `main` or merging own PR.

**MustReadSpecific:**

- `docs/INTERFACE-DEVELOPMENT.md`
- current `web/scada-web/src/audit/**`;
- existing Audit query/UI/security E2E tests.

**CompletionCriteria:**

1. compact desktop-oriented filter workflow with clear active-filter context;
2. scalable event results with clear timestamp/actor/action/outcome/resource presentation;
3. selected-event/master-detail view for metadata instead of forcing every detail into the list;
4. Audit diagnostics remain available but visually secondary when healthy;
5. keyset pagination and backend filter contract unchanged;
6. explicit unauthenticated/forbidden/invalid/unavailable states preserved;
7. localized `pt-BR`/`en`/`es` and keyboard/focus accessible;
8. Draft PR, exact-head CI green, then `WAIT_FOR_COORDINATOR`.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`
