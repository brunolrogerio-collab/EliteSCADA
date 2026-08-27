# CHAT WORK ASSIGNMENTS — EliteSCADA

> Authoritative live execution board. GitHub branch/PR/head/CI state is operational truth. Permanent scheduling rules: `docs/DEVELOPMENT-WAVES.md` and `docs/PARALLEL-WORK.md`.

**Last coordinator synchronization:** 2026-08-27

## Mandatory `siga`

Every fixed EliteSCADA chat first rereads current `main`: `PROJECT GOAL.md`, `LAST CHANGE.md`, `docs/ROADMAP.md`, `docs/PARALLEL-WORK.md`, `docs/DEVELOPMENT-WAVES.md`, this board, current `MustReadSpecific` and `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`.

Then verify real GitHub branch/PR/head/CI and execute only the current authorized assignment.

`NextQueuedTask` is planning only. A worker never starts queued work unless this board promotes it to `ACTIVE` and its `StartCondition` is true.

## Current product gate

`INTERFACE-WAVE-03` is **ACTIVE**.

**WaveBaseSHA:** `0aae8317aff5b0640eb713c1ce404224ccbcbbc2`  
**IntegrationBranch:** `integration/interface-wave-03`  
**Product objective:** prove the complete non-graphical operational cycle `edit -> save revision -> publish -> activate -> operate` while adding Runtime TAG inspection and cross-product acceptance evidence.

The WaveBaseSHA is the immutable logical product-code base for this wave. Documentation/coordination commits on `main` after this SHA do not invalidate worker branches or require reconciliation. Unrelated product-code changes must stay out of `main` while the wave is active.

PostgreSQL schema-initialization concurrency hardening is **MERGED** through PR #70, merge `0aae8317aff5b0640eb713c1ce404224ccbcbbc2`, exact-head CI #405 green.

First owner-facing validation remains `EliteSCADA v0.1 — Full Product Validation Preview`, after the Python + graphical Engineering path defined in `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`.

Production MQTT/OPC UA/BACnet/S7/Allen-Bradley remains unauthorized before that gate unless the product owner deliberately changes the roadmap.

---

# COORDENADOR - EliteSCADA

**Role:** `COORDINATOR`

**Wave:** `INTERFACE-WAVE-03`

**CurrentTask:** Coordinate Wave 03 worker delivery and integrate accepted slices

**Branch:** `integration/interface-wave-03` plus `main` for coordination-only documentation

**Status:** `ACTIVE`

**BaseSHA:** `0aae8317aff5b0640eb713c1ce404224ccbcbbc2`

**StartCondition:** `TRUE`

**DependsOn:** PR #70 merged; CI #405 green; WaveBase frozen; worker branches created.

**ParallelSafeWith:** all three worker slices, subject to reserved-file boundaries below.

**Objective:**

1. keep product-code `main` stable during the wave;
2. review worker Draft PRs at Early Contract / Integration / Delivery events;
3. integrate accepted slices into `integration/interface-wave-03` without forcing unrelated worker rebases;
4. own central `EngineeringApp.tsx`, `main.tsx`, routing/shell and any required composition hooks;
5. run final integrated Web/backend/tests/runtime smoke/Chromium CI;
6. merge only a coherent green Wave 03 composition;
7. update roadmap/handoff and decide the Wave 04 gate.

**AllowedScope:** coordinator-owned shared hooks, integration branch, cross-domain composition, review/merge, assignment board, roadmap/handoff, CI root-cause corrections that block the wave.

**ForbiddenScope:** new protocols; premature Python/graphical editor work; unrelated product features; weakening tests/security/CAS/runtime authority; merging known failures.

**MustReadSpecific:**

- `docs/DEVELOPMENT-WAVES.md`
- `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`
- `docs/INTERFACE-DEVELOPMENT.md`
- `docs/INTERFACE-VALIDATION-MILESTONE.md`
- current worker PRs and `INTEGRATION REQUIRED` notes

**ReservedFiles:** `PROJECT GOAL.md`, `LAST CHANGE.md`, `docs/ROADMAP.md`, `docs/PARALLEL-WORK.md`, `docs/DEVELOPMENT-WAVES.md`, `docs/CHAT-WORK-ASSIGNMENTS.md`, `.github/workflows/**`, `src/Scada.Api/Program.cs`, `web/scada-web/src/engineering/EngineeringApp.tsx`, `web/scada-web/src/main.tsx`, `web/scada-web/src/AppNavigation.tsx`, central shell/routing/composition files.

**IntegrationRequired:** yes, all worker slices compose here.

**IntegrationTarget:** `integration/interface-wave-03`

**ValidationMatrix:** final Web build + backend Release build/full tests + Runtime smoke + Chromium E2E, with lifecycle/TAG/history/authorization acceptance included.

**CompletionCriteria:** all accepted worker slices integrated; no duplicate old/new product paths; lifecycle cycle works; TAG Inspector works read-only; acceptance harness passes/classifies findings; final exact integrated CI green.

**AfterCompletion:** `CONTINUE_COORDINATION`

---

# DEV 1 - EliteSCADA

**Role:** `WORKER`

**Wave:** `INTERFACE-WAVE-03`

**CurrentTask:** `Engineering Lifecycle Workspace`

**Branch:** `feature/interface-wave-03-lifecycle-workspace`

**Status:** `ACTIVE`

**BaseSHA:** `0aae8317aff5b0640eb713c1ce404224ccbcbbc2`

**StartCondition:** `TRUE — WaveBase frozen and branch created from exact BaseSHA`

**DependsOn:** existing protected Engineering persistence/lifecycle endpoints; no new backend contract required.

**ParallelSafeWith:** DEV 2 Runtime TAG Inspector and DEV 3 Acceptance Harness.

**Objective:** expose the authoritative lifecycle as a clear Engineering workspace:

`Working -> Save Revision -> Publish -> Published -> Activate -> Active`

Show project/workspace identity, Dirty/Clean, Base Revision, revision list, Published Revision, Active Revision, live Runtime Revision and Runtime-vs-Active consistency/divergence.

Provide Save, Checkout, Publish and Activate UX with explicit confirmation for critical operations and understandable validation/error states including authorization/conflict/unavailable semantics (`401/403/409/422/503` as applicable). Support `pt-BR`, `en`, `es`.

**AllowedScope:** new isolated lifecycle workspace component/model/API adapter/styles and focused tests under `web/scada-web/src/engineering/` plus worker-owned contract/E2E tests when isolated.

**ForbiddenScope:**

- do not modify backend lifecycle/schema/persistence/security;
- do not modify `EngineeringApp.tsx`, central routing/shell or `main.tsx`;
- do not send trusted operator identity as frontend authority; request actor fields may be omitted/null because backend filter replaces identity from authenticated principal;
- do not change Preview/Apply/CAS semantics;
- no unrelated Engineering features, protocols, Python or graphical editor.

**ReservedFiles:** prefer new `EngineeringLifecycleWorkspace*`, `engineeringLifecycleApi*`, `engineering-lifecycle-workspace.css` and focused test files. Existing `web/scada-web/src/engineering/api.ts` / `types.ts` may be changed only if narrowly required and no competing worker touches them. Coordinator-owned files remain forbidden.

**IntegrationRequired:** coordinator mounts the finished workspace into `EngineeringApp.tsx` and resolves any shared localization/shell hook.

**IntegrationTarget:** `integration/interface-wave-03`

**ValidationMatrix:** TypeScript/Web build; focused lifecycle model/API/contract tests; relevant Chromium tests where independently mountable; exact worker head CI must be green before Delivery Review.

**CompletionCriteria:** lifecycle facts/actions represented from real backend contracts; dirty/base/revision/published/active/runtime consistency visible; Save/Checkout/Publish/Activate handled; critical confirmations; required error/localization states; no frontend identity authority; Draft PR body has `IMPLEMENTED IN PR`, `INTEGRATION REQUIRED`, `SPECIFIED / NOT IMPLEMENTED`.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`

**NextQueuedTask:** `Wave 04 Project Management / portability surface`

**NextStartCondition:** `QUEUED_ONLY — Wave 03 merged and coordinator promotes Wave 04 task`

**MustReadSpecific:**

- `docs/DEVELOPMENT-WAVES.md`
- `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`
- `src/Scada.Api/Persistence/EngineeringPersistenceApi.cs`
- `src/Scada.Api/Persistence/EngineeringPersistenceSecurityFilter.cs`
- `src/Scada.Api/Runtime/EngineeringWorkspace.cs`
- `web/scada-web/src/engineering/api.ts`
- `web/scada-web/src/engineering/types.ts`
- existing lifecycle/persistence tests under `tests/Scada.Core.Tests` and `tests/Scada.Drivers.Tests`

---

# DEV 2 - EliteSCADA

**Role:** `WORKER`

**Wave:** `INTERFACE-WAVE-03`

**CurrentTask:** `Runtime TAG Inspector + Recent History`

**Branch:** `feature/interface-wave-03-runtime-tag-inspector`

**Status:** `ACTIVE`

**BaseSHA:** `0aae8317aff5b0640eb713c1ce404224ccbcbbc2`

**StartCondition:** `TRUE — WaveBase frozen and branch created from exact BaseSHA`

**DependsOn:** existing protected `/api/tags`, `/api/tags/current`, `/api/tags/by-path/{path}`, `/api/history/{tagId}` and `/ws/tags`; no backend change required.

**ParallelSafeWith:** DEV 1 Lifecycle Workspace and DEV 3 Acceptance Harness.

**Objective:** create a read-only operational TAG Inspector showing Path, Name, Value, DataType, Unit, Quality, Timestamp, Source/Data Source context where available, Description and ReadOnly state, with search/filter, master-detail selection, realtime/refresh and recent history.

Handle Good/Bad/other qualities honestly and distinguish authorization/unavailable/loading/empty states. Support `pt-BR`, `en`, `es`.

**AllowedScope:** new isolated Runtime TAG Inspector component/model/types/API adapter/styles and focused tests under `web/scada-web/src/runtime/`.

**ForbiddenScope:**

- no setpoint/process write UI and no call to `/api/tags/{id}/write`;
- no backend/history semantic redesign;
- no direct driver access;
- no `main.tsx`, `AppNavigation.tsx`, central routing/shell changes;
- no protocols, Python or graphical editor.

**ReservedFiles:** prefer new `RuntimeTagInspector*`, `tagInspectorApi*`, `tagInspectorModel*`, `tagInspectorTypes*`, `runtime-tag-inspector.css` and focused test files. Do not modify DEV 1 Engineering files or DEV 3 acceptance harness files.

**IntegrationRequired:** coordinator chooses/mounts final Runtime placement in central composition after review.

**IntegrationTarget:** `integration/interface-wave-03`

**ValidationMatrix:** TypeScript/Web build; focused model/API tests; realtime/history contract evidence; exact worker head CI green before Delivery Review.

**CompletionCriteria:** scalable read-only browser/search/filter/master-detail; current values/quality/timestamps; recent history; realtime or explicit refresh behavior using existing contracts; required auth/unavailable/localization states; no process writes; Draft PR body has required three evidence sections.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`

**NextQueuedTask:** `Wave 04 Basic Trend Viewer`

**NextStartCondition:** `QUEUED_ONLY — Wave 03 merged and coordinator promotes Wave 04 task`

**MustReadSpecific:**

- `docs/DEVELOPMENT-WAVES.md`
- `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`
- `src/Scada.Api/Program.cs` TAG/history/realtime endpoints
- `src/Scada.Api/Realtime/TagRealtimeHub.cs`
- `web/scada-web/src/runtime/RuntimeOperationsOverview.tsx`
- `web/scada-web/src/runtime/operationsTypes.ts`
- `web/scada-web/src/runtime/operationsApi.ts`
- `web/scada-web/tests-e2e/runtime.spec.ts`

---

# DEV 3 - EliteSCADA

**Role:** `WORKER`

**Wave:** `INTERFACE-WAVE-03`

**CurrentTask:** `Interface Validation Readiness Harness`

**Branch:** `test/interface-wave-03-acceptance-harness`

**Status:** `ACTIVE`

**BaseSHA:** `0aae8317aff5b0640eb713c1ce404224ccbcbbc2`

**StartCondition:** `TRUE — WaveBase frozen and branch created from exact BaseSHA`

**DependsOn:** existing product surfaces and Playwright conventions. Harness may establish baseline on WaveBase and evolve tests for the Wave 03 integrated target without taking ownership of DEV 1/2 product code.

**ParallelSafeWith:** DEV 1 Lifecycle Workspace and DEV 2 Runtime TAG Inspector.

**Objective:** build cross-product Chromium acceptance evidence covering login/session, Runtime, Runtime Operations, Alarm Center, Engineering navigation/Data Sources/TAGs/Alarms/Internal Memory/Gateway/communication diagnostics/lifecycle, Audit, user administration authorization, Runtime↔Engineering↔Audit navigation and meaningful `pt-BR`/`en`/`es` states.

**AllowedScope:** isolated Playwright acceptance specs/helpers/fixtures under `web/scada-web/tests-e2e/`, plus test-only support that does not change product behavior.

**ForbiddenScope:**

- do not silently fix cross-domain product defects;
- do not modify DEV 1/2 implementation files;
- do not weaken authorization, assertions or existing tests merely for green CI;
- no product feature expansion, protocols, Python or graphical editor.

**ReservedFiles:** new Wave 03 acceptance spec/helper files. Existing shared E2E helpers may be changed only narrowly when unavoidable; avoid `app-shell.spec.ts` unless coordinator explicitly authorizes because it is an integration-sensitive shared acceptance surface.

**IntegrationRequired:** findings that require product changes are reported to coordinator; coordinator assigns repair. Test slice itself integrates into `integration/interface-wave-03`.

**IntegrationTarget:** `integration/interface-wave-03`

**IssueClassification:** every discovered gap is classified `BLOCKER`, `MAJOR UX`, `MINOR UX`, or `TEST GAP` with reproducible evidence; do not broaden own mission to repair it.

**ValidationMatrix:** Playwright Chromium focused/new acceptance; existing relevant E2E must remain green; Web build; exact worker head CI green before Delivery Review.

**CompletionCriteria:** cross-product harness covers the required surfaces/auth/navigation/languages; findings classified; no scope creep; Draft PR body has `IMPLEMENTED IN PR`, `INTEGRATION REQUIRED`, `SPECIFIED / NOT IMPLEMENTED` and a readiness conclusion for the current wave.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`

**NextQueuedTask:** `Wave 04 Administration Workspace ergonomics`

**NextStartCondition:** `QUEUED_ONLY — Wave 03 merged and coordinator promotes Wave 04 task`

**MustReadSpecific:**

- `docs/DEVELOPMENT-WAVES.md`
- `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`
- `web/scada-web/playwright.config.ts`
- current `web/scada-web/tests-e2e/*.spec.ts` conventions
- `docs/SECURITY-AUTHORIZATION-AUDIT.md`
- `docs/INTERFACE-DEVELOPMENT.md`

---

## Coordinator note for future chats

Wave 03 is open. All three worker tasks above are `ACTIVE`; `siga` in each fixed DEV chat means start/continue exactly that assignment from the recorded branch/BaseSHA. The coordinator must not merge unrelated product code to `main` during the wave. Documentation-only coordination commits may advance `main` without invalidating `WaveBaseSHA`.