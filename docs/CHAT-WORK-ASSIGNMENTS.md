# CHAT WORK ASSIGNMENTS — EliteSCADA

> Authoritative live execution board. GitHub branch/PR/head/CI state is operational truth. Permanent scheduling rules: `docs/DEVELOPMENT-WAVES.md` and `docs/PARALLEL-WORK.md`.

**Last coordinator synchronization:** 2026-08-27

## Mandatory `siga`

Every fixed EliteSCADA chat first rereads current `main`: `PROJECT GOAL.md`, `LAST CHANGE.md`, `docs/ROADMAP.md`, `docs/PARALLEL-WORK.md`, `docs/DEVELOPMENT-WAVES.md`, this board, current `MustReadSpecific` and, for work toward first validation, `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`.

Then verify real GitHub branch/PR/head/CI and execute only the current authorized assignment.

`NextQueuedTask` is planning only. A worker never starts queued work unless the board/start condition promotes/authorizes it according to the Development Wave rules.

## Current product gate

Wave 00 second interface integration is functionally complete through PR #69.

Next intended product wave: `INTERFACE-WAVE-03`, but its `WaveBaseSHA` must not be frozen until the coordinator resolves or deliberately isolates the PostgreSQL concurrent schema-initialization race and confirms a healthy `main`.

First owner-facing validation remains `EliteSCADA v0.1 — Full Product Validation Preview`, after the full Python + graphical Engineering path defined in `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`.

Production MQTT/OPC UA/BACnet/S7/Allen-Bradley remains unauthorized before the v0.1 owner-validation gate unless the product owner deliberately changes the roadmap.

---

# COORDENADOR - EliteSCADA

**Role:** `COORDINATOR`

**Wave:** `WAVE-00-CLOSEOUT / WAVE-03-PREP`

**CurrentTask:** Close Wave 00 documentation, harden PostgreSQL concurrent initialization, then establish INTERFACE-WAVE-03

**Branch:** `main` plus coordinator maintenance/integration branches as required

**Status:** `ACTIVE`

**BaseSHA:** `ee65ab51a39cd74ef6f14395d27b0ee16b8c6970` is the functional Wave 00 merge point; current documentation commits may advance `main` without changing that fact.

**Objective:**

1. preserve PR #69 integrated state;
2. make the approved Development Wave model and v0.1 roadmap repository-authoritative;
3. investigate/fix the PostgreSQL `CREATE SCHEMA IF NOT EXISTS elitescada` concurrency race observed in CI, using a dedicated maintenance change if needed;
4. confirm healthy `main`;
5. select the exact Wave 03 `WaveBaseSHA`;
6. create `integration/interface-wave-03`;
7. promote the three queued Wave 03 slices only after Definition of Ready is satisfied.

**AllowedScope:** coordinator-owned documentation, CI/infrastructure root-cause maintenance, PostgreSQL store initialization concurrency, central composition, wave/integration branches, assignments, review/merge.

**ForbiddenScope:**

- no new protocol production work;
- no premature Python/graphical editor implementation before its roadmap wave;
- no arbitrary third interface feature while Wave 03 is not opened;
- no known-failing merge;
- no weakening tests/concurrency merely to get green CI.

**MustReadSpecific:**

- `docs/DEVELOPMENT-WAVES.md`
- `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`
- `docs/INTERFACE-DEVELOPMENT.md`
- `docs/INTERFACE-VALIDATION-MILESTONE.md`
- PostgreSQL store initialization code/tests relevant to the race

**IntegrationRequired:** coordinator owns all shared hooks and final wave composition.

**ValidationMatrix:** maintenance fix must pass relevant focused concurrency tests plus normal affected build/test/smoke; Wave 03 final integration will require Web/backend/tests/smoke/Chromium.

**CompletionCriteria:** healthy `main`, PostgreSQL race fixed or explicitly isolated with evidence, Wave 03 base/integration branch recorded, worker tasks promoted to ACTIVE only when Ready.

**AfterCompletion:** `CONTINUE_COORDINATION`

---

# DEV 1 - EliteSCADA

**Role:** `WORKER`

**Wave:** `INTERFACE-WAVE-03` planned

**CurrentTask:** No active task

**Status:** `WAIT_FOR_COORDINATOR`

**BaseSHA:** `TBD_AFTER_WAVE_00_HARDENING`

**StartCondition:** `AFTER_WAVE_03_BASE_FROZEN_AND_TASK_PROMOTED_ACTIVE`

**ParallelSafeWith:** planned DEV 2 + DEV 3 Wave 03 slices, subject to final Definition of Ready.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`

**NextQueuedTask:** `Engineering Lifecycle Workspace`

**NextStartCondition:** `AFTER_POSTGRES_HARDENING_AND_INTERFACE_WAVE_03_READY`

**QueuedObjective:** expose the real Working -> Revision -> Published -> Active lifecycle as a practical Engineering workspace, including dirty/base/revision/published/active/runtime-consistency facts and protected Save/Checkout/Publish/Activate UX.

**QueuedPreferredScope:** isolated lifecycle workspace files/tests; coordinator owns `EngineeringApp.tsx`/routing/central composition unless a narrow exception is recorded.

**QueuedForbiddenScope:** backend lifecycle/schema/persistence/security redesign, frontend-supplied identity, central routing/shell, unrelated Engineering features, protocols/Python/graphical editor.

**MustReadSpecific when promoted:**

- `docs/DEVELOPMENT-WAVES.md`
- `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`
- lifecycle/persistence ADR/docs selected by coordinator
- current protected lifecycle API/types/tests

---

# DEV 2 - EliteSCADA

**Role:** `WORKER`

**Wave:** `INTERFACE-WAVE-03` planned

**CurrentTask:** No active task

**Status:** `WAIT_FOR_COORDINATOR`

**BaseSHA:** `TBD_AFTER_WAVE_00_HARDENING`

**StartCondition:** `AFTER_WAVE_03_BASE_FROZEN_AND_TASK_PROMOTED_ACTIVE`

**ParallelSafeWith:** planned DEV 1 + DEV 3 Wave 03 slices, subject to final Definition of Ready.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`

**NextQueuedTask:** `Runtime TAG Inspector + Recent History`

**NextStartCondition:** `AFTER_POSTGRES_HARDENING_AND_INTERFACE_WAVE_03_READY`

**QueuedObjective:** read-only operational TAG diagnosis using existing current/realtime/history APIs: search/filter, value/type/unit/quality/timestamp/source/description and master-detail recent history.

**QueuedPreferredScope:** isolated Runtime inspector files/tests; coordinator chooses central Runtime placement.

**QueuedForbiddenScope:** process writes/setpoints, driver access, backend/history semantic redesign, central `main.tsx`/routing, protocols/Python/graphical editor.

**MustReadSpecific when promoted:**

- `docs/DEVELOPMENT-WAVES.md`
- `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`
- current TAG/realtime/history API contracts
- historian/runtime diagnostics docs selected by coordinator

---

# DEV 3 - EliteSCADA

**Role:** `WORKER`

**Wave:** `INTERFACE-WAVE-03` planned

**CurrentTask:** No active task

**Status:** `WAIT_FOR_COORDINATOR`

**BaseSHA:** `TBD_AFTER_WAVE_00_HARDENING`

**StartCondition:** `AFTER_WAVE_03_BASE_FROZEN_AND_TASK_PROMOTED_ACTIVE`

**ParallelSafeWith:** planned DEV 1 + DEV 2 Wave 03 slices, subject to final Definition of Ready.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`

**NextQueuedTask:** `Interface Validation Readiness Harness`

**NextStartCondition:** `AFTER_POSTGRES_HARDENING_AND_INTERFACE_WAVE_03_READY`

**QueuedObjective:** cross-product browser acceptance covering login/session, Runtime, Operations, Alarm Center, Engineering Data Sources/TAGs/Alarms/Memory/Gateway/diagnostics/lifecycle, Audit, administration authorization, navigation and major localization states.

**QueuedPreferredScope:** isolated E2E acceptance specs/helpers/fixtures that consume real product boundaries.

**QueuedForbiddenScope:** silently fixing cross-domain product defects by expanding scope; weakening authorization/tests; production protocols/Python/graphical editor.

**IssueClassification:** findings are recorded as `BLOCKER`, `MAJOR UX`, `MINOR UX`, or `TEST GAP`; coordinator assigns repairs.

**MustReadSpecific when promoted:**

- `docs/DEVELOPMENT-WAVES.md`
- `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`
- current Chromium/E2E conventions
- interface/auth/Audit/lifecycle docs selected by coordinator

---

## Coordinator note for future chats

A new coordinator conversation must not infer assignments from old PRs. Read this board and GitHub. As of this synchronization the workers are deliberately idle with Wave 03 tasks only QUEUED. The next coordinator action is PostgreSQL concurrency hardening + WaveBase freeze, not worker implementation.