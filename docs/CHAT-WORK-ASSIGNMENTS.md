# CHAT WORK ASSIGNMENTS — EliteSCADA

> Authoritative coordination board for EliteSCADA ChatGPT workstreams.
>
> Purpose: answer **who is doing what now, on which branch, under which boundaries, and what that chat must do when the user says only `continue`**.

**Coordination protocol introduced:** 2026-08-26
**Functional repository baseline verified before this documentation change:** `main` at `9d2fdf5a44f818af9705f05caa09fbcea24ee959`

This file is **coordination state**, not implementation truth. Before working, every chat must verify the real GitHub branch, PR, head commit and CI state. If this file and GitHub disagree about operational state, GitHub wins and the discrepancy must be reported to the coordinator. Stable product/architecture intent remains governed by `PROJECT GOAL.md`.

## 1. Permanent `continue` protocol

When the user writes only `continue` in an EliteSCADA development chat, the chat must:

1. identify itself by the fixed workstream/chat name;
2. read, from current GitHub `main`:
   - `PROJECT GOAL.md`;
   - `LAST CHANGE.md`;
   - `docs/ROADMAP.md`;
   - `docs/PARALLEL-WORK.md`;
   - `docs/CHAT-WORK-ASSIGNMENTS.md`;
   - every document listed in its own `MustReadSpecific` field;
3. locate its exact assignment section in this file;
4. verify the real GitHub state of its assigned branch, PR, head commit and relevant CI before editing anything;
5. obey `Status`, `AllowedScope`, `ForbiddenScope`, `Dependencies`, `NextActions`, `CompletionCriteria` and `AfterCompletion`;
6. continue automatically without asking the user to repeat the original task prompt.

### Decision table for `continue`

- `ASSIGNED` or `IN_PROGRESS`: start or continue the assigned task.
- `PR_OPEN`: continue the assigned PR task until its completion criteria are met.
- `CI_FAILED`: inspect the real failed CI, fix the assigned branch only, and revalidate.
- `READY_FOR_COORDINATOR_REVIEW`, `WAITING` or `COMPLETED` + `AfterCompletion: WAIT_FOR_COORDINATOR`: do **not** start new work. Inform the user that the current task is delivered and no new task is authorized.
- `MERGED` or `COMPLETED` + `AfterCompletion: TAKE_NEXT_ASSIGNED_TASK`: continue only if a separate explicit current/next task is already recorded in this file.
- `MERGED` or `COMPLETED` + `AfterCompletion: NEXT_TASK: <task>`: start exactly that named task, using its recorded branch/scope rules.
- assignment not found for the current fixed chat name: do not invent work, do not choose a roadmap item, and do not create a branch. Report that no assignment exists and wait for coordinator action.

A worker must never reinterpret `continue` as permission to select its own next roadmap item.

## 2. Authority to change assignments

Only **COORDENADOR - EliteSCADA** may add, remove or change work assignments for other chats in this file.

Worker chats may:

- read this file;
- verify their branch/PR/CI against GitHub;
- update code and tests inside their authorized branch/scope;
- update their own PR body with implementation status, CI evidence and `INTEGRATION REQUIRED` notes.

Worker chats must not:

- edit this file to give themselves new work;
- change another chat's assignment;
- create a new task/branch because the current task is complete;
- alter `main`;
- merge their own PR;
- work in another chat's branch or reserved domain unless explicitly reassigned here by the coordinator.

## 3. Status vocabulary

Assignment status values:

- `ASSIGNED`
- `IN_PROGRESS`
- `PR_OPEN`
- `CI_FAILED`
- `READY_FOR_COORDINATOR_REVIEW`
- `INTEGRATION_REQUIRED`
- `MERGED`
- `BLOCKED`
- `WAITING`
- `COMPLETED`

Repository/product terminology remains:

- **MERGED** — official `main` state;
- **IMPLEMENTED IN PR** — exists only in a feature branch/open PR;
- **SPECIFIED / NOT IMPLEMENTED** — architecture/product intent exists but implementation does not.

An open PR is never product state, even when its CI is green.

---

# COORDENADOR - EliteSCADA

**Role:** `COORDINATOR`

**CurrentTask:** Secured Engineering Mutation finalization and parallel integration coordination

**Branch:** `feature/engineering-secured-apply`

**Status:** `CI_FAILED`

**PullRequest:** `#42` — Draft / open / **IMPLEMENTED IN PR / NOT MERGED**

**ObservedHead:** `4fcc5ab5de03e5c7d9b194554aef25e97daed98d`

**ObservedCI:** EliteSCADA CI `#227` — **FAILED**. Backend build/tests/runtime smoke and Web build passed. Chromium E2E failed because existing `engineering.spec.ts` uses a strict `getByText('Demo.P01.Frequency')` locator that now resolves to multiple elements after the mutation UI additions.

**Objective:**

Finish the secured Engineering Apply/Delete/bulk-edit lifecycle safely, then review/reconcile/integrate worker PRs against the then-current `main` while maintaining shared architecture and coordination documents.

**Responsibilities:**

- own shared integration and merge ordering;
- maintain this assignment board and coordinator-owned documentation;
- reconcile worker PRs without discarding worker commits;
- implement coordinator-owned cross-domain integration hooks;
- run final relevant CI before any merge;
- preserve `MERGED` vs `IMPLEMENTED IN PR` distinction;
- update worker assignments only after real GitHub state is verified.

**AllowedScope:**

Coordinator may modify shared/central files when required, including Engineering mutation lifecycle, central API/runtime composition, security/audit integration, shared frontend routing/application composition and coordinator-owned documentation.

Current PR #42 changed domains include:

- `src/Scada.Api/Runtime/EngineeringWorkspace.cs`;
- `src/Scada.Api/Runtime/EngineeringMutationEndpoints.cs`;
- `src/Scada.Api/Runtime/EngineeringBulkEndpoints.cs`;
- explicit TAG/Alarm/Data Source registry deletion support;
- Engineering mutation frontend/API panels;
- Engineering mutation Chromium/security E2E;
- `src/Scada.Security/Audit/AuditModels.cs` action-key additions.

**ForbiddenScope:**

Do not silently rewrite worker branches, force-reset them, or merge a worker PR without reconciliation and final validation. Do not treat worker PR implementation as merged product state.

**MustReadSpecific:**

- `docs/ENGINEERING-UI.md`
- `docs/SECURITY-AUTHORIZATION-AUDIT.md`
- worker PR bodies and `INTEGRATION REQUIRED` sections before integration

**Dependencies:**

- PR #42 itself has no dependency on worker PRs for its existing mutation implementation.
- PR #44 adds shared Audit action keys/integration primitives that should be reconciled rather than duplicated when Audit is integrated.
- Worker PRs #40, #41, #43 and #44 all require coordinator review/reconciliation before merge.

**NextActions:**

1. fix the PR #42 Chromium strict-locator regression without weakening the assertion;
2. rerun/confirm full CI on the final PR #42 head;
3. review worker PRs #40, #41, #43 and #44 plus their integration requirements;
4. decide integration order from real conflict/dependency state;
5. reconcile each selected worker PR with then-current `main`, add only required coordinator-owned hooks, validate, and merge when green;
6. update this file immediately when a worker receives a new assignment or when an assignment materially changes.

**CompletionCriteria:**

- secured TAG/Data Source/Alarm Apply/Delete/bulk behavior is backend-authoritative and validated;
- authorization/audit/concurrency/dirty semantics are preserved;
- final PR #42 CI is fully green;
- PR body and operational docs accurately describe the final state;
- integration decisions are made from real current GitHub state.

**AfterCompletion:** `CONTINUE_COORDINATION`

---

# DEV 1 - EliteSCADA

**Role:** `WORKER`

**CurrentTask:** Audit Durability + Retention + Query Foundation

**Branch:** `feature/audit-durability-retention-query`

**Status:** `READY_FOR_COORDINATOR_REVIEW`

**PullRequest:** `#44` — Draft / open / **IMPLEMENTED IN PR / NOT MERGED**

**ObservedHead:** `8429b1bed28bd998ed25cf1b4a47caf364aef887`

**ObservedCI:** EliteSCADA CI `#229` — **SUCCESS**

**Objective:**

Provide an isolated durable Audit foundation with bounded query/pagination, retention policy, temporary-outage buffering, storage-boundary sanitization and focused tests without taking ownership of central API/DI integration.

**AllowedScope:**

- Audit domain abstractions/models/sink/query/retention components;
- PostgreSQL Audit persistence required by this foundation;
- focused Audit/PostgreSQL tests;
- fixes strictly necessary inside this assigned domain if coordinator requests follow-up.

**ForbiddenScope:**

- Historian/downsampling;
- Engineering Apply/Delete/Bulk implementation;
- Internal Memory/Gateway;
- Python/visual runtime;
- authentication/role redesign;
- central `Program.cs`/DI/routing;
- coordinator-owned documentation or workflow files.

**MustReadSpecific:**

- `docs/SECURITY-AUTHORIZATION-AUDIT.md`

**Dependencies:**

None for the isolated foundation. Coordinator integration is required for production API/DI/hosted-service wiring.

**IntegrationRequired:**

- configure Audit query/retention/buffer policies in central API/DI;
- wire `BufferedAuditSink` while preserving the underlying durable store for query/retention;
- evolve protected `/api/audit` to bounded keyset query with approved filters/cursor;
- run periodic retention through central hosted-service composition;
- retain `SystemAdmin` protection unless an explicitly approved capability replaces it;
- reconcile shared `EngineeringDelete` / `EngineeringBulkEdit` Audit action keys with PR #42 rather than duplicating literals.

**CompletionCriteria:**

The isolated foundation, focused tests, PR documentation and full CI must be complete. This criterion is currently satisfied on the observed head; integration remains coordinator-owned.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`

**ContinueBehaviorNow:**

On `continue`, report that PR #44 is delivered with green CI and wait. Do not create another branch or choose another task.

---

# DEV 2 - EliteSCADA

**Role:** `WORKER`

**CurrentTask:** Historian Retention + Downsampling Foundation

**Branch:** `feature/historian-retention-downsampling`

**Status:** `READY_FOR_COORDINATOR_REVIEW`

**PullRequest:** `#43` — Draft / open / **IMPLEMENTED IN PR / NOT MERGED**

**ObservedHead:** `98e75948bac3ebe68f424c3a45ebbaefdf9a9331`

**ObservedCI:** EliteSCADA CI `#215` — **SUCCESS**

**Objective:**

Provide isolated historian retention/downsampling policy and TimescaleDB infrastructure without prematurely owning public Engineering integration or Trend UI semantics.

**AllowedScope:**

- `Scada.Historian` retention/downsampling contracts and aggregation semantics;
- `Scada.Historian.TimescaleDb` infrastructure/policy reconciliation/continuous aggregate support;
- focused historian/TimescaleDB tests;
- fixes strictly necessary inside this assigned domain if coordinator requests follow-up.

**ForbiddenScope:**

- Audit durability/query work;
- Python/visual runtime;
- central Engineering contracts/import-export;
- central DI/`Program.cs`/frontend routing;
- Trend UI;
- Internal Memory PR #40 unless explicitly reassigned by the coordinator;
- coordinator-owned documentation/workflows.

**MustReadSpecific:**

- `docs/ADR-003-HISTORIAN-AND-ALARMS.md`

**Dependencies:**

The isolated foundation is independent. Public Engineering policy integration and runtime configuration are coordinator-owned follow-up.

**IntegrationRequired:**

- public/versioned Engineering historian storage-policy representation;
- Engineering validation/import-export/schema migration;
- central Historian configuration/DI wiring;
- later history/trend resolution selection between raw and aggregate data;
- any explicit legacy data-type migration policy.

**PreviousDeliveredWork:**

PR `#40` — Internal Memory / Source Provider Foundation, branch `feature/internal-memory-foundation`, head `77990fd161580f2e70de941632e5398dfac5c6bd`, CI `#184` **SUCCESS**, Draft/open/**IMPLEMENTED IN PR / NOT MERGED**. It is a previous DEV 2 delivery awaiting coordinator integration and is **not** the current authorized workstream.

**CompletionCriteria:**

The isolated retention/downsampling foundation, focused tests, PR documentation and full CI must be complete. This criterion is currently satisfied on the observed head; integration remains coordinator-owned.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`

**ContinueBehaviorNow:**

On `continue`, report that PR #43 is delivered with green CI and wait. Do not resume PR #40, create another branch or choose another task unless this file is updated by the coordinator.

---

# DEV 3 - EliteSCADA

**Role:** `WORKER`

**CurrentTask:** Python Scripting + Visual Property Foundation

**Branch:** `feature/python-scripting-foundation`

**Status:** `READY_FOR_COORDINATOR_REVIEW`

**PullRequest:** `#41` — Draft / open / **IMPLEMENTED IN PR / NOT MERGED**

**ObservedHead:** `77d9eb49acd56629aaae96764a48c25784ceb328`

**ObservedCI:** EliteSCADA CI `#210` — **SUCCESS**

**Objective:**

Provide the isolated public contracts/foundation required before the graphical Screen/Popup/Dynamo editor: typed visual properties, runtime presentation state, script scopes/sandbox boundaries, tween contracts, visual runtime instances, event/queue/execution diagnostics and Python validation contracts.

**AllowedScope:**

- `src/Scada.Engineering/VisualScripting/**` and isolated supporting types;
- focused tests for this foundation;
- fixes strictly necessary inside this assigned domain if coordinator requests follow-up.

**ForbiddenScope:**

- final graphical Screen/Popup/Dynamo editor;
- concrete central Engineering schema/import-export/revision/package wiring;
- central runtime/browser composition;
- Internal Memory/Gateway;
- Audit or Historian work;
- coordinator-owned documentation/workflows/central files.

**MustReadSpecific:**

- `docs/PYTHON-SCRIPTING-AND-VISUAL-RUNTIME.md`

**Dependencies:**

The isolated contracts are complete independently. Central Engineering/runtime/editor integration remains coordinator-owned/later work.

**IntegrationRequired:**

- authoritative Engineering Script/visual-property entities and import/export/revision/package integration;
- mapping Engineering Screen/Popup/Dynamo/Script definitions into runtime-instance contracts;
- concrete sandboxed Client Python engine selection/integration;
- renderer implementation behind tween scheduler;
- browser event/TAG/Client Memory/authorization-aware adapters;
- practical Python editor/sandbox preview;
- separate later Server Python host using Server scope boundaries.

**CompletionCriteria:**

The isolated foundation, focused tests, PR documentation and full CI must be complete. This criterion is currently satisfied on the observed head; integration remains coordinator-owned.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`

**ContinueBehaviorNow:**

On `continue`, report that PR #41 is delivered with green CI and wait. Do not create another branch or choose another task.

---

## 4. Adding future chats/workstreams

When a new fixed EliteSCADA chat is created, the coordinator must add a section before that chat receives only `continue` as an instruction. At minimum every assignment must contain:

- `Role`
- `CurrentTask`
- `Branch`
- `Status`
- `PullRequest` when one exists
- `Objective`
- `AllowedScope`
- `ForbiddenScope`
- `MustReadSpecific`
- `Dependencies`
- `IntegrationRequired` when applicable
- `NextActions` when useful
- `CompletionCriteria`
- `AfterCompletion`

The coordinator should update observed PR/head/CI data when materially useful, but every chat must still verify GitHub before acting.
