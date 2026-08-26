# CHAT WORK ASSIGNMENTS — EliteSCADA

> Authoritative coordination board for EliteSCADA ChatGPT workstreams.
>
> Purpose: answer **who is doing what now, on which branch, under which boundaries, and what that chat must do when the user says only `siga`**.

**Coordination protocol introduced:** 2026-08-26
**Current scheduling baseline:** `main` observed at `529b37259484542ff4f4a8bf6276088c56fd70a6` before this assignment update.

This file is coordination state, not implementation truth. Before working, every chat must verify the real GitHub branch, PR, head commit and CI state. If this file and GitHub disagree about operational state, GitHub wins and the discrepancy must be reconciled by the coordinator. Stable product/architecture intent remains governed by `PROJECT GOAL.md`.

## 1. Permanent `siga` protocol

The user's canonical short command is:

`siga`

`continue` is accepted as a backward-compatible alias with identical meaning.

When the user sends only `siga` or `continue` in an EliteSCADA chat, the chat must:

1. identify itself by its fixed workstream/chat name;
2. read, from current GitHub `main`:
   - `PROJECT GOAL.md`;
   - `LAST CHANGE.md`;
   - `docs/ROADMAP.md`;
   - `docs/PARALLEL-WORK.md`;
   - `docs/CHAT-WORK-ASSIGNMENTS.md`;
   - every document listed in its own `MustReadSpecific` field;
3. locate its exact assignment section in this file;
4. verify the real GitHub state of its assigned branch, PR, head commit and relevant CI before editing anything;
5. obey `Status`, `AllowedScope`, `ForbiddenScope`, `Dependencies`, `IntegrationRequired`, `NextActions`, `CompletionCriteria` and `AfterCompletion`;
6. continue automatically without asking the user to repeat the original task prompt.

### Decision table

- `ASSIGNED` or `IN_PROGRESS`: start or continue the assigned task.
- `PR_OPEN`: continue the assigned PR task until its completion criteria are met.
- `CI_FAILED`: inspect the failed CI, fix the assigned branch only, and revalidate.
- `READY_FOR_COORDINATOR_REVIEW`, `WAITING` or `COMPLETED` + `AfterCompletion: WAIT_FOR_COORDINATOR`: do **not** start new work.
- `MERGED` or `COMPLETED` + `AfterCompletion: TAKE_NEXT_ASSIGNED_TASK`: act only if a separate explicit next task is already recorded here.
- `MERGED` or `COMPLETED` + `AfterCompletion: NEXT_TASK: <task>`: start exactly that named task using its recorded branch/scope rules.
- assignment not found: do not infer work from roadmap/history/old branches; report no assignment and wait for coordinator action.

A worker must never reinterpret `siga` as permission to choose its own next roadmap item.

## 2. Authority to change assignments

Only **COORDENADOR - EliteSCADA** may add, remove or change work assignments for DEV chats in this file.

Workers may:

- read this file;
- verify their branch/PR/CI against GitHub;
- update code and tests inside their authorized branch/scope;
- update their own PR body with implementation status, CI evidence and `INTEGRATION REQUIRED` notes.

Workers must not:

- edit this file to give themselves new work;
- change another chat's assignment;
- create a new task/branch because the current task is complete;
- alter `main`;
- merge their own PR;
- resume an older merged branch as if it were a new assignment;
- work in another chat's branch or reserved domain unless explicitly reassigned here.

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

Repository/product terminology:

- **MERGED** — official `main` state;
- **IMPLEMENTED IN PR** — exists only in a feature branch/open PR;
- **SPECIFIED / NOT IMPLEMENTED** — architecture/product intent exists but functionality does not.

---

# COORDENADOR - EliteSCADA

**Role:** `COORDINATOR`

**CurrentTask:** Coordinate next dependency-safe parallel development wave

**Branch:** `main`

**Status:** `IN_PROGRESS`

**PullRequest:** none; coordinator assignment/documentation updates are maintained on `main`.

**Objective:**

Advance the next roadmap wave without violating the locked source/protocol order or creating shared-contract conflicts. Internal Memory remains the next mandatory source/protocol block. Public Script Engineering may advance in parallel. Audit UI may advance as an isolated administrative/frontend slice.

**AllowedScope:**

Coordinator may modify shared/central files and coordination documents when required by integration or scheduling.

**ForbiddenScope:**

Do not silently rewrite worker history, force-reset worker branches, merge known-failing work, invent product state, or schedule Gateway/new external protocols before Internal Memory product integration is complete.

**MustReadSpecific:**

- `docs/INTERNAL-MEMORY-TAGS.md`;
- `docs/PYTHON-SCRIPTING-AND-VISUAL-RUNTIME.md`;
- `docs/ADR-004-ENGINEERING-IMPORT-EXPORT.md`;
- current worker PR bodies and `INTEGRATION REQUIRED` sections during review.

**Dependencies:**

- Internal Memory complete product integration precedes TAG Gateway.
- Public Script Engineering integration precedes concrete script editor/sandbox and later visual runtime/editor work.
- DEV 2 is the sole worker in this wave authorized to modify the central Engineering contract file.

**NextActions:**

1. keep the three assigned branches based on current `main`;
2. user may now send `siga` to DEV 1, DEV 2 and DEV 3;
3. review Draft PRs and CI as they appear;
4. integrate DEV 2 central Engineering changes before any DEV 3 central Script-schema hook;
5. do not start TAG Gateway until the Internal Memory completion criteria in the roadmap are actually satisfied.

**CompletionCriteria:**

- all three worker assignments are explicit and non-overlapping;
- branches/PRs remain attributable to exactly one DEV;
- shared-contract ownership is unambiguous;
- worker deliveries are reviewed against CI and dependency order before merge.

**AfterCompletion:** `CONTINUE_COORDINATION`

---

# DEV 1 - EliteSCADA

**Role:** `WORKER`

**CurrentTask:** Audit UI and diagnostics client foundation

**Branch:** `feature/audit-ui`

**Status:** `ASSIGNED`

**PullRequest:** none yet; create a Draft PR after the first coherent implementation commit.

**Objective:**

Build an isolated, production-oriented frontend Audit feature on top of the already merged protected `/api/audit` and `/api/audit/diagnostics` surfaces. The feature must support the real bounded/keyset query contract rather than inventing client-side pagination semantics.

**AllowedScope:**

- new isolated files under `web/scada-web/src/audit/**`;
- feature-local React components, API client/types, query state, pagination state, diagnostics presentation and feature-local styling;
- focused frontend/test helpers required only by this feature;
- tests that exercise the Audit UI/API contract without changing backend production semantics;
- PR body documentation of integration hooks and CI evidence.

**ForbiddenScope:**

- `web/scada-web/src/main.tsx` or other central routing/application-shell files;
- shared/global styles unless explicitly reassigned by the coordinator;
- backend Audit storage/runtime behavior;
- `src/Scada.Api/Program.cs`;
- central DI/composition;
- Engineering contracts/import-export;
- adding a weaker Audit authorization capability;
- exposing sensitive metadata or assuming caller-supplied identity is trusted;
- modifying coordinator-owned documentation or workflows.

**MustReadSpecific:**

- `PROJECT GOAL.md` sections on Security and Audit;
- `LAST CHANGE.md` PR #44/#45 state;
- `docs/ROADMAP.md` Audit evolution;
- current `src/Scada.Security/Audit/**` query/diagnostic contracts;
- current API implementation for `/api/audit` and `/api/audit/diagnostics`;
- existing frontend authentication/error-handling patterns.

**Dependencies:**

PRs #44 and #45 are merged and are the authoritative backend contract. This task must not depend on DEV 2 or DEV 3.

**IntegrationRequired:**

Coordinator will own final central route/navigation registration and any unavoidable shared-shell/localization wiring. DEV 1 must list the exact required hook(s) in the PR body rather than editing reserved central files.

**NextActions:**

1. create/use `feature/audit-ui` from current `main`;
2. inspect the actual Audit query DTO, supported filters and cursor header before designing UI state;
3. implement a self-contained Audit feature with loading/empty/error/unauthorized states;
4. support only filters actually accepted by the backend and preserve the opaque cursor as opaque;
5. expose diagnostics without leaking sensitive internal exception text;
6. validate Web build and relevant tests;
7. open/update Draft PR with `IMPLEMENTED IN PR`, tests and `INTEGRATION REQUIRED`.

**CompletionCriteria:**

- isolated Audit UI feature compiles successfully;
- list/query uses backend-supported bounded filters and keyset cursor correctly;
- next-page navigation does not decode or manufacture cursors;
- diagnostics and authorization/error states are explicit;
- no sensitive metadata is newly exposed;
- central route/shell changes are left as clearly documented coordinator integration;
- relevant CI is green on the branch.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`

---

# DEV 2 - EliteSCADA

**Role:** `WORKER`

**CurrentTask:** Internal Memory Engineering + durable Server Memory product integration

**Branch:** `feature/internal-memory-product-integration`

**Status:** `ASSIGNED`

**PullRequest:** none yet; create a Draft PR after the first coherent implementation commit.

**Objective:**

Advance the **next locked source/protocol block** from merged PR #40 foundation toward complete product integration. This slice owns the public Engineering representation for memory source types/typed initial values and the durable Server Memory retention implementation, with explicit validation for client/server semantics.

**AllowedScope:**

- `src/Scada.Core/InternalMemory/**` and `src/Scada.Core/Sources/**` as required by the assigned domain;
- a narrowly scoped exception to `src/Scada.Engineering/Contracts/EngineeringContracts.cs` for Internal Memory fields/entities only;
- relevant `src/Scada.Engineering/ImportExport/**` and `src/Scada.Engineering/Validation/**` changes for schema migration, canonical JSON, preview validation and round-trip behavior;
- new/isolated PostgreSQL persistence implementation required for Server Memory retained runtime values under `src/Scada.Persistence.PostgreSql/**`;
- focused tests for Engineering schema compatibility, import/export, retention, stable-ID rename behavior and incompatible-type handling;
- PR body documentation of runtime/DI/UI hooks still requiring coordinator integration.

**ForbiddenScope:**

- `src/Scada.Api/Program.cs`;
- central DI/composition files unless separately reassigned;
- frontend routing/application shell;
- broad unrelated changes to Engineering entities;
- TAG Gateway implementation;
- common multi-driver diagnostics;
- new external protocols;
- treating Client Memory as one global server scalar;
- allowing Client Memory to drive global historian/alarm semantics;
- serializing mutable retained Server Memory values into immutable Engineering packages;
- silent coercion of incompatible retained values;
- modifying coordinator-owned documentation or workflows.

**MustReadSpecific:**

- `docs/INTERNAL-MEMORY-TAGS.md`;
- `docs/ADR-004-ENGINEERING-IMPORT-EXPORT.md`;
- `docs/ARCHITECTURE.md`;
- current `src/Scada.Core/InternalMemory/**` and `src/Scada.Core/Sources/**` from merged PR #40;
- current Engineering schema/migration/import-export tests;
- current PostgreSQL persistence conventions.

**Dependencies:**

PR #40 is merged. This work is the next mandatory source/protocol block. DEV 2 has exclusive worker ownership of `EngineeringContracts.cs` during this wave so DEV 3 must not edit it.

**IntegrationRequired:**

Coordinator owns final `Program.cs`/DI/runtime composition, central API hookup and any shared frontend Engineering UI wiring. DEV 2 must provide explicit hooks/types and document exactly what remains to connect. If full runtime behavior cannot be validated without central composition, the PR must state that clearly rather than faking integration.

**NextActions:**

1. create/use `feature/internal-memory-product-integration` from current `main`;
2. extend the public/versioned Engineering model for `builtin.memory.client` and `builtin.memory.server` plus typed initial/default values;
3. preserve supported older schema import/migration behavior and canonical round trips;
4. validate that memory sources require no fake network address/metrics;
5. implement durable Server Memory retained-value storage keyed by stable TAG ID, separate from immutable Engineering revisions;
6. make rename preservation and incompatible type/reset-or-migration behavior explicit and testable;
7. add validation that Client Memory cannot become a global historian/alarm source;
8. run backend/tests/PostgreSQL coverage as applicable;
9. open/update Draft PR with precise `INTEGRATION REQUIRED` notes.

**CompletionCriteria:**

- public Engineering representation for both memory source types and typed initial values exists in the PR;
- canonical export/import/preview/schema migration behavior is covered by tests;
- durable Server Memory retention survives restart semantics at the persistence/domain boundary and keys by stable TAG ID;
- path rename preserves retained value when ID/type remain compatible;
- incompatible retained type never silently coerces and requires explicit reset/migration semantics;
- Client Memory global historian/alarm misuse is rejected by validation;
- no fake network diagnostics are introduced;
- relevant CI is green;
- all central composition/UI hooks remaining are documented for coordinator integration.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`

---

# DEV 3 - EliteSCADA

**Role:** `WORKER`

**CurrentTask:** Public Script Engineering integration foundation

**Branch:** `feature/script-engineering-integration`

**Status:** `ASSIGNED`

**PullRequest:** none yet; create a Draft PR after the first coherent implementation commit.

**Objective:**

Advance roadmap stage 11 by turning the merged PR #41 scripting/visual foundation into an isolated Script Engineering domain ready for central canonical-schema integration, while deliberately avoiding the central Engineering contract file owned by DEV 2 in this wave.

**AllowedScope:**

- `src/Scada.Engineering/VisualScripting/**`;
- new isolated `src/Scada.Engineering/Scripts/**` files for Script Engineering definitions, validation, dependency/reference rules and adapters;
- focused Engineering tests for stable Script identity, scope/language markers, source, enabled state, entry-point/event metadata, dependency validation and visual-reference integrity;
- isolated helpers/adapters that map Script Engineering definitions to the existing PR #41 runtime/sandbox contracts without introducing a renderer-private model;
- PR body documentation of exact central Engineering/package hooks required after DEV 2 integration.

**ForbiddenScope:**

- `src/Scada.Engineering/Contracts/EngineeringContracts.cs` during this wave;
- central canonical schema version changes/migrations while DEV 2 owns that shared contract;
- `src/Scada.Api/Program.cs` or central DI/composition;
- frontend routing/application shell;
- concrete graphical Screens/Popups/Dynamos editor;
- concrete browser Python engine/editor implementation before the public Script Engineering integration is reconciled;
- server Python runtime;
- direct driver/database/filesystem/network access from scripts;
- any script API that bypasses normal backend authorization;
- modifying coordinator-owned documentation or workflows.

**MustReadSpecific:**

- `docs/PYTHON-SCRIPTING-AND-VISUAL-RUNTIME.md`;
- `docs/ADR-004-ENGINEERING-IMPORT-EXPORT.md`;
- `docs/ROADMAP.md` stages 11–15;
- current `src/Scada.Engineering/VisualScripting/**` merged from PR #41;
- current Screen/Popup/Dynamo Engineering contracts for stable-reference compatibility, read-only unless an isolated adapter can avoid editing the central contract.

**Dependencies:**

PR #41 is merged. Final first-class canonical Script collection/schema integration depends on the shared Engineering contract becoming available after DEV 2's Internal Memory schema work is reconciled. This task may proceed in parallel only because it is isolated from that central file.

**IntegrationRequired:**

After DEV 2's central Engineering changes are integrated, the coordinator will reconcile Script entities/references into `EngineeringContracts.cs`, schema migration, canonical JSON, `.escadapkg`, preview/apply and central persistence paths. DEV 3 must list exact insertion points and any semantic assumptions in the PR body.

**NextActions:**

1. create/use `feature/script-engineering-integration` from current `main`;
2. define an isolated Script Engineering model consistent with locked fields: stable ID/path, scope, language/version, Python source, enabled state, entry points/events, dependencies, description/metadata;
3. implement deterministic validation for duplicate identity/path, invalid scope/language, missing/invalid referenced entry points and dependency/reference failures;
4. provide explicit adapters to the existing PR #41 scripting/visual runtime contracts where appropriate;
5. keep Engineering base values and runtime presentation overrides separate;
6. add focused tests;
7. open/update Draft PR with exact central schema/package `INTEGRATION REQUIRED` notes.

**CompletionCriteria:**

- Script Engineering domain is represented by stable typed contracts outside the central shared file;
- validation covers identity, scope, language/version, source/entry-point metadata and dependency/reference integrity;
- adapters use the merged PR #41 public scripting/visual contracts rather than a private renderer/DOM model;
- no central Engineering schema file is modified;
- no concrete Python engine/editor or graphical editor is started prematurely;
- relevant CI is green;
- PR precisely documents the coordinator-owned changes needed to make Scripts first-class in canonical Engineering/import-export/revisions/packages.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`

---

## 4. Adding future chats/workstreams

When a new fixed EliteSCADA chat is created, the coordinator must add a section before that chat is expected to work from `siga` alone. At minimum every assignment must contain:

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

Every chat must still verify GitHub before acting, regardless of how current this board appears.
