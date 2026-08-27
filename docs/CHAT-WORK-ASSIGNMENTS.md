# CHAT WORK ASSIGNMENTS — EliteSCADA

> Authoritative live execution board. GitHub branch/PR/head/CI state is operational truth. Permanent scheduling rules: `docs/DEVELOPMENT-WAVES.md` and `docs/PARALLEL-WORK.md`.

**Last coordinator synchronization:** 2026-08-27

## Mandatory `siga`

Every fixed EliteSCADA chat first rereads current `main`: `PROJECT GOAL.md`, `LAST CHANGE.md`, `docs/ROADMAP.md`, `docs/PARALLEL-WORK.md`, `docs/DEVELOPMENT-WAVES.md`, this board, current `MustReadSpecific` and `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`.

Then verify real GitHub branch/PR/head/CI and execute only the current authorized assignment. Workers never modify `main`, never merge their own PR, never work another DEV branch and never enlarge their own mission.

`NextQueuedTask` is planning only. A worker starts queued work only when this board promotes it to `ACTIVE` and its `StartCondition` is true.

## Current product gate

`INTERFACE-WAVE-04` is **ACTIVE**.

**WaveBaseSHA:** `37e64b8ff2bbc431ab1368eab2b3125ec5a5b636`  
**IntegrationBranch:** `integration/interface-wave-04`  
**Product objective:** make the non-graphical platform practically usable through project portability/backup, basic Trends and stronger local administration while preserving the Wave 03 lifecycle/runtime path.

The WaveBaseSHA is the immutable logical product-code base for this wave. Documentation/coordination commits on `main` after this SHA do not invalidate worker branches. Unrelated product-code changes remain frozen until the coordinator closes the wave.

### Previous wave evidence

`INTERFACE-WAVE-03` is **MERGED** through PR #74.

- worker PR #71 Runtime TAG Inspector: exact-head CI #411 green;
- worker PR #72 Acceptance Harness: exact-head CI #412 green;
- worker PR #73 Engineering Lifecycle Workspace: exact-head CI #410 green;
- integration head `41d44d513d337e8ef6d3cc0e04ef0cf07a697b41`;
- integration CI #418 / run `33124948655`: Web, backend/full tests, Runtime smoke and Chromium all SUCCESS;
- merge commit: `37e64b8ff2bbc431ab1368eab2b3125ec5a5b636`;
- integrated Chromium proves canonical Engineering Preview/Apply -> Save Revision -> Publish -> Activate -> durable Active/live Runtime consistency -> Server Memory TAG visible in Runtime TAG Inspector;
- `RUNTIME_NO_ACTIVE_SOURCES` remains enforced; no test/security/runtime guard was weakened.

First owner-facing validation remains `EliteSCADA v0.1 — Full Product Validation Preview`, after the Python + graphical Engineering path defined in `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`.

---

# COORDENADOR - EliteSCADA

**Role:** `COORDINATOR`  
**Wave:** `INTERFACE-WAVE-04`  
**CurrentTask:** coordinate portability + Trends + Administration delivery and integrate accepted slices  
**Branch:** `integration/interface-wave-04` plus `main` for coordination-only documentation  
**Status:** `ACTIVE`  
**BaseSHA:** `37e64b8ff2bbc431ab1368eab2b3125ec5a5b636`  
**StartCondition:** `TRUE`

**DependsOn:** Wave 03 PR #74 merged; CI #418 green; Wave 04 branches created from exact BaseSHA.

**Objective:** review worker PRs at Early Contract / Integration / Delivery events, own central composition/routing if needed, preserve the Wave 03 lifecycle path, integrate accepted slices once, run final Web/backend/tests/Runtime smoke/Chromium, merge only a coherent green Wave 04 and decide Wave 05 central Script contract gate.

**ReservedFiles:** `PROJECT GOAL.md`, `LAST CHANGE.md`, `docs/ROADMAP.md`, `docs/PARALLEL-WORK.md`, `docs/DEVELOPMENT-WAVES.md`, `docs/CHAT-WORK-ASSIGNMENTS.md`, `.github/workflows/**`, `src/Scada.Api/Program.cs`, `web/scada-web/src/engineering/EngineeringApp.tsx`, `web/scada-web/src/main.tsx`, `web/scada-web/src/AppNavigation.tsx`, central routing/shell/composition files.

**ForbiddenScope:** protocol expansion, Python/graphical editor implementation before their planned waves, weakening security/CAS/lifecycle/runtime authority, or worker-owned feature implementation without a demonstrated integration blocker.

**IntegrationTarget:** `integration/interface-wave-04`  
**ValidationMatrix:** final Web build + backend Release/full tests + Runtime smoke + Chromium cross-product acceptance.  
**CompletionCriteria:** all three accepted slices integrated; no duplicate paths; portability round trip usable; basic Trend usable; Administration coherent; Wave 03 lifecycle/Runtime acceptance remains green.  
**AfterCompletion:** `CONTINUE_COORDINATION`

---

# DEV 1 - EliteSCADA

**Role:** `WORKER`  
**Wave:** `INTERFACE-WAVE-04`  
**CurrentTask:** `Project Management / Portability Workspace`  
**Branch:** `feature/interface-wave-04-project-management`  
**Status:** `ACTIVE`  
**BaseSHA:** `37e64b8ff2bbc431ab1368eab2b3125ec5a5b636`  
**StartCondition:** `TRUE — branch created from exact WaveBaseSHA`

**DependsOn:** canonical Engineering import/export, Preview/Apply/CAS, persisted revision lifecycle and existing backup/package foundations. No new hidden frontend authority is allowed.

**ParallelSafeWith:** DEV 2 Basic Trend Viewer and DEV 3 Administration Workspace.

**Objective:** provide one understandable Engineering project-management surface for portability and recovery. At minimum expose JSON Export, JSON Import with Preview before Apply, project/package backup and restore using existing public contracts, and `.escadapkg` where the repository already provides the package contract. Preserve stable IDs, canonical schema/version and lifecycle semantics. Import/restore must never bypass Preview/validation/CAS when the underlying operation is an Engineering mutation.

**AllowedScope:** isolated project-management UI/API adapters/types/styles and focused tests under `web/scada-web/src/engineering/`; narrowly related existing portability/package adapters when required by real contracts.

**ForbiddenScope:** inventing a second Engineering model; direct database manipulation; plaintext secret export; changing central `EngineeringApp.tsx`/routing; redesigning lifecycle; protocols/Python/graphical editor; implementing a fake `.escadapkg` format if backend/public package support is not actually present. If a required package operation is only specified and not implemented, report `INTEGRATION REQUIRED` or `SPECIFIED / NOT IMPLEMENTED` rather than fabricating it.

**ReservedFiles:** prefer new `ProjectManagementWorkspace*`, `projectPortabilityApi*`, `projectPackage*` and focused tests. Coordinator owns central mounts.

**IntegrationRequired:** coordinator mounts the accepted workspace and resolves central navigation/composition.  
**IntegrationTarget:** `integration/interface-wave-04`

**ValidationMatrix:** TypeScript/Web build; JSON export/import round trip; Preview-before-Apply/CAS evidence; package/backup/restore focused tests for contracts that actually exist; exact worker-head CI green.

**CompletionCriteria:** user can understand/export a project, select/import a candidate, inspect Preview/errors before Apply, and exercise every currently real backup/package recovery operation without schema/identity loss or secret leakage. PR body includes `IMPLEMENTED IN PR`, `INTEGRATION REQUIRED`, `SPECIFIED / NOT IMPLEMENTED`.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`  
**NextQueuedTask:** `Wave 05 Script Engineering Workspace foundation`  
**NextStartCondition:** `QUEUED_ONLY — Wave 04 merged, coordinator central Script contract stabilized, board promotes task`

**MustReadSpecific:**
- `docs/DEVELOPMENT-WAVES.md`
- `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`
- canonical Engineering import/export/package docs and contracts discovered in current `main`
- `web/scada-web/src/engineering/api.ts`
- existing Engineering import/export/preview/apply/package tests
- persistence lifecycle APIs relevant to backup/restore

---

# DEV 2 - EliteSCADA

**Role:** `WORKER`  
**Wave:** `INTERFACE-WAVE-04`  
**CurrentTask:** `Basic Trend Viewer`  
**Branch:** `feature/interface-wave-04-trend-viewer`  
**Status:** `ACTIVE`  
**BaseSHA:** `37e64b8ff2bbc431ab1368eab2b3125ec5a5b636`  
**StartCondition:** `TRUE — branch created from exact WaveBaseSHA`

**DependsOn:** existing Runtime TAG catalog/current values, TimescaleDB historian APIs and TAG quality semantics. No historian redesign required.

**ParallelSafeWith:** DEV 1 Project Management and DEV 3 Administration Workspace.

**Objective:** create a basic operator Trend Viewer that lets the user choose a TAG, select a bounded interval/window, see historical samples with quality, refresh, and understand live-vs-historical context. Add simple multiple pens only if it fits the existing API/data model without introducing a new architecture contract.

**AllowedScope:** isolated Runtime trend components/model/API/types/styles and focused tests under `web/scada-web/src/runtime/` or a clearly isolated trend folder.

**ForbiddenScope:** new historian storage/query semantics; unbounded queries; custom database access; advanced Trend Designer; write/setpoint UI; central `main.tsx`/routing; protocols/Python/graphical editor.

**ReservedFiles:** prefer new `BasicTrendViewer*`, `trendApi*`, `trendModel*`, `trendTypes*`, styles and tests. Avoid DEV 1 Engineering and DEV 3 administration files.

**IntegrationRequired:** coordinator chooses the final Runtime placement/navigation.  
**IntegrationTarget:** `integration/interface-wave-04`

**ValidationMatrix:** TypeScript/Web build; bounded historian query contract; quality/timestamp rendering; empty/unavailable/auth states; live refresh behavior if used; exact worker-head CI green.

**CompletionCriteria:** at least one TAG can be selected and its bounded historical series displayed with timestamp/value/quality, interval control and clear error/empty states; no historian semantic invention; PR body has required evidence sections.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`  
**NextQueuedTask:** `Wave 05 Script Reference Runtime Adapter`  
**NextStartCondition:** `QUEUED_ONLY — Wave 04 merged, coordinator central Script contract stabilized, board promotes task`

**MustReadSpecific:**
- `docs/DEVELOPMENT-WAVES.md`
- `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`
- historian/TimescaleDB docs and current history endpoints in `main`
- `web/scada-web/src/runtime/RuntimeTagInspector.tsx` and its API/model contracts
- existing historian/runtime E2E tests

---

# DEV 3 - EliteSCADA

**Role:** `WORKER`  
**Wave:** `INTERFACE-WAVE-04`  
**CurrentTask:** `Administration Workspace ergonomics`  
**Branch:** `feature/interface-wave-04-administration-workspace`  
**Status:** `ACTIVE`  
**BaseSHA:** `37e64b8ff2bbc431ab1368eab2b3125ec5a5b636`  
**StartCondition:** `TRUE — branch created from exact WaveBaseSHA`

**DependsOn:** existing local user/session/authorization APIs and backend-trusted identity/role enforcement. No security redesign required.

**ParallelSafeWith:** DEV 1 Project Management and DEV 2 Basic Trend Viewer.

**Objective:** improve the existing local Administration experience for users, roles and sessions: clear user list/detail, create/update flows supported by current backend, enable/disable, role assignment, password-change/reset operation where current contract permits, explicit session/authorization consequences, confirmations and understandable 401/403/409/422 states. Support `pt-BR`, `en`, `es`.

**AllowedScope:** isolated administration UI/API/model/styles/tests and narrowly related current user-administration components.

**ForbiddenScope:** authentication protocol redesign; frontend-trusted identity/roles; exposing credentials/hashes; weakening invalidation/authorization; central `EngineeringApp.tsx`/routing unless coordinator explicitly integrates; protocols/Python/graphical editor.

**ReservedFiles:** administration/user/session-specific components and focused tests. Do not take DEV 1 portability or DEV 2 Trend files.

**IntegrationRequired:** coordinator mounts/reconciles any central navigation hook.  
**IntegrationTarget:** `integration/interface-wave-04`

**ValidationMatrix:** Web build; focused admin API/model tests; Chromium role/enable-disable/password/session consequences; authorization negative cases; localization; exact worker-head CI green.

**CompletionCriteria:** existing backend capabilities are exposed coherently without leaking credentials or claiming authority client-side; destructive/session-affecting actions are explicit; required error/localization states covered; PR body has required evidence sections.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`  
**NextQueuedTask:** `Wave 05 Script validation / compatibility`  
**NextStartCondition:** `QUEUED_ONLY — Wave 04 merged, coordinator central Script contract stabilized, board promotes task`

**MustReadSpecific:**
- `docs/DEVELOPMENT-WAVES.md`
- `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`
- `docs/SECURITY-AUTHORIZATION-AUDIT.md`
- current local-auth/user-administration/session components and tests
- backend local identity/session authorization endpoints in current `main`

---

## Coordinator note for future chats

Wave 03 is MERGED. Wave 04 is ACTIVE from logical BaseSHA `37e64b8ff2bbc431ab1368eab2b3125ec5a5b636`; all three worker branches were created from that exact commit. `siga` in each fixed DEV chat means start/continue only the Wave 04 assignment recorded above. Product-code `main` stays relatively frozen until integration; coordination/documentation commits do not invalidate WaveBaseSHA.
