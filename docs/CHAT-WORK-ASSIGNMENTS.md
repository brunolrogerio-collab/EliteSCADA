# CHAT WORK ASSIGNMENTS — EliteSCADA

> Authoritative live coordination board. GitHub branch/PR/head/CI state is operational truth; if this file briefly lags GitHub, GitHub wins and the coordinator reconciles it.

**Coordination protocol introduced:** 2026-08-26
**Last coordinator synchronization:** 2026-08-26

## Permanent `siga` protocol

The user's canonical short command is `siga`; `continue` is an equivalent alias.

Before any action, every fixed EliteSCADA chat must reread from current `main`:

1. `PROJECT GOAL.md`
2. `LAST CHANGE.md`
3. `docs/ROADMAP.md`
4. `docs/PARALLEL-WORK.md`
5. `docs/CHAT-WORK-ASSIGNMENTS.md`
6. all documents listed in its `MustReadSpecific`

Then it must verify its real GitHub branch, PR/head and relevant CI. Workers never choose a new roadmap task, never alter `main`, never merge their own PR and never expand their own assignment. `WAIT_FOR_COORDINATOR` means stop after delivery.

Repository terminology:

- **MERGED** = official `main` state.
- **IMPLEMENTED IN PR** = exists only in an open worker PR/branch.
- **SPECIFIED / NOT IMPLEMENTED** = documented product intent without merged implementation.

---

# COORDENADOR - EliteSCADA

**Role:** `COORDINATOR`

**CurrentTask:** Coordinate active worker wave and reconcile deliveries safely

**Branch:** `main`

**Status:** `IN_PROGRESS`

**PullRequest:** none

**Objective:**

Review the active DEV 1/2/3 work, preserve the locked dependency order, own central/shared integrations, merge only reviewed green work and keep documentation aligned with actual repository state.

**AllowedScope:**

Coordinator-owned shared/central files, integration hooks, assignment board, handoff/roadmap documentation and merge decisions.

**ForbiddenScope:**

- no known-failing merge;
- no force-reset of worker branches;
- no discarding valid worker commits merely to simplify conflicts;
- no TAG Gateway before complete Internal Memory product integration;
- no claim that an open worker PR is merged product state.

**MustReadSpecific:**

- `docs/INTERNAL-MEMORY-TAGS.md`
- `docs/PYTHON-SCRIPTING-AND-VISUAL-RUNTIME.md`
- `docs/ADR-004-ENGINEERING-IMPORT-EXPORT.md`
- active PR bodies and `INTEGRATION REQUIRED` sections

**Dependencies:**

- Internal Memory complete product integration precedes TAG Gateway.
- DEV 2 retains exclusive worker ownership of Internal Memory changes in `EngineeringContracts.cs` in this wave.
- DEV 3 canonical Script-schema integration must occur only after DEV 2 central Engineering changes are reconciled.
- DEV 1 central Audit route/navigation/CORS hooks remain coordinator-owned.

**ObservedGitHubState:**

- `main` functional/product baseline remains PR #45 plus coordinator documentation commits.
- PR #46 (`feature/audit-ui`) is open Draft, head `0659647be5c127a0555f585005a20597255fa990`, 8 commits ahead / 0 behind at the pre-sync main baseline; CI run #244 is currently in progress and Web build is already successful.
- PR #47 (`feature/script-engineering-integration`) is open Draft, head `da6dd4914741a6fa9ece4c758d245899fb20af92`, 6 commits ahead at observation; no PR workflow run was yet present for that head.
- PR #48 (`feature/internal-memory-product-integration`) is open Draft, head `8ea8f7770322de0c1244b70c26027ab0ba2b5a2a`, 13 commits ahead at observation; no PR workflow run was yet present for that head.

**NextActions:**

1. let DEV 1 finish CI #244 and correct only if validation fails;
2. let DEV 2 continue the Internal Memory completion criteria and obtain CI before handoff;
3. let DEV 3 finish focused Script Engineering validation and obtain CI before handoff;
4. review worker diffs and PR bodies on the next coordinator `siga`;
5. integrate DEV 2 central Engineering work before coordinator-owned Script canonical-schema hooks;
6. integrate DEV 1 route/navigation/CORS only after PR #46 passes review/CI;
7. do not start Gateway until Internal Memory is actually complete on `main`.

**CompletionCriteria:**

All active worker PRs are reviewed, required integration hooks are reconciled, relevant CI is green, merges follow dependency order, and worker assignments are advanced only after official merged state permits it.

**AfterCompletion:** `CONTINUE_COORDINATION`

---

# DEV 1 - EliteSCADA

**Role:** `WORKER`

**CurrentTask:** Audit UI and diagnostics client foundation

**Branch:** `feature/audit-ui`

**Status:** `PR_OPEN`

**PullRequest:** `#46` — Draft — **IMPLEMENTED IN PR / NOT MERGED**

**ObservedHead:** `0659647be5c127a0555f585005a20597255fa990`

**Objective:**

Provide an isolated production-oriented Audit frontend consuming the merged `/api/audit` and `/api/audit/diagnostics` contracts, including opaque keyset cursor handling, supported filters, explicit error/auth states and sanitized diagnostics presentation.

**AllowedScope:**

- `web/scada-web/src/audit/**`
- feature-local Audit client/types/components/styles
- focused Audit UI/API contract tests
- PR body/test evidence and `INTEGRATION REQUIRED`

**ForbiddenScope:**

- `web/scada-web/src/main.tsx` and central application shell/routing
- global/shared styles without reassignment
- backend Audit semantics/storage
- `src/Scada.Api/Program.cs`
- central DI/composition
- weaker Audit authorization capability
- coordinator-owned docs/workflows

**MustReadSpecific:**

- `PROJECT GOAL.md` Security/Audit rules
- `LAST CHANGE.md` PR #44/#45 state
- `docs/ROADMAP.md` Audit evolution
- current Audit query/diagnostic contracts
- frontend auth/error patterns

**Dependencies:** PRs #44/#45 are merged. No dependency on DEV 2/3.

**IntegrationRequired:**

Coordinator owns `/audit` central route/navigation registration, unavoidable shared localization integration and cross-origin exposure of `X-EliteSCADA-Audit-Next-Cursor` if required by deployment topology.

**CurrentValidation:**

CI #243 was cancelled after a replacement run. CI #244 is in progress for the same current head; Web build has passed while backend/test/smoke/Chromium completion is still pending at the last coordinator observation.

**NextActions:**

1. do not start new feature scope;
2. observe CI #244;
3. if CI fails, fix only failures attributable to this assigned branch without weakening tests;
4. if CI passes and completion criteria remain satisfied, update PR evidence and move to `WAIT_FOR_COORDINATOR` behavior.

**CompletionCriteria:**

- isolated Audit UI compiles;
- only backend-supported filters are used;
- cursor remains opaque;
- diagnostics/auth/error states are explicit and safe;
- no new sensitive information exposure;
- central hooks remain documented as integration requirements;
- relevant CI green.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`

---

# DEV 2 - EliteSCADA

**Role:** `WORKER`

**CurrentTask:** Internal Memory Engineering + durable Server Memory product integration

**Branch:** `feature/internal-memory-product-integration`

**Status:** `PR_OPEN`

**PullRequest:** `#48` — Draft — **IMPLEMENTED IN PR / NOT MERGED**

**ObservedHead:** `8ea8f7770322de0c1244b70c26027ab0ba2b5a2a`

**Objective:**

Advance the next locked source/protocol block by completing public/versioned Internal Memory Engineering representation, typed initial values and durable Server Memory retention foundations while preserving client/server memory semantics.

**AllowedScope:**

- `src/Scada.Core/InternalMemory/**`
- `src/Scada.Core/Sources/**` when required
- narrow Internal Memory-only exception to `src/Scada.Engineering/Contracts/EngineeringContracts.cs`
- relevant `src/Scada.Engineering/ImportExport/**` and `Validation/**`
- isolated PostgreSQL Server Memory retention storage
- focused schema/import/export/retention/migration tests

**ForbiddenScope:**

- `src/Scada.Api/Program.cs`
- central DI/composition unless reassigned
- frontend routing/shell
- unrelated Engineering domains
- TAG Gateway
- common multi-driver diagnostics
- new protocols
- globalizing Client Memory
- Client Memory global historian/alarm semantics
- embedding mutable retained values into immutable Engineering packages
- silent incompatible-type coercion
- coordinator-owned docs/workflows

**MustReadSpecific:**

- `docs/INTERNAL-MEMORY-TAGS.md`
- `docs/ADR-004-ENGINEERING-IMPORT-EXPORT.md`
- `docs/ARCHITECTURE.md`
- merged PR #40 Internal Memory/Core source contracts
- current Engineering migration/import-export tests
- PostgreSQL persistence conventions

**Dependencies:** PR #40 merged. DEV 2 is the sole worker allowed to touch `EngineeringContracts.cs` in this wave.

**IntegrationRequired:**

Coordinator owns final `Program.cs`/DI/runtime composition, central API/runtime wiring and shared frontend Engineering UI hooks.

**ObservedImplementation:**

Current diff remains inside assigned domains and includes Internal Memory core changes, the authorized central Engineering contract change, import/export validation, PostgreSQL retention storage and focused Internal Memory tests. PR body still declares the task in progress.

**NextActions:**

1. complete all PR #48 stated remaining work and update its body to actual state;
2. preserve schema migration and canonical round-trip compatibility;
3. complete durable stable-ID retention/reset/type-mismatch coverage;
4. prove Client Memory global historian/alarm rejection;
5. obtain relevant backend/PostgreSQL CI;
6. document exact coordinator integration hooks and stop at `WAIT_FOR_COORDINATOR` when completion criteria are met.

**CompletionCriteria:**

- both memory source types and typed initial values represented publicly;
- canonical import/export/preview/migration tested;
- durable Server Memory retention keyed by stable TAG ID;
- rename preserves compatible value;
- incompatible type never silently coerces;
- Client Memory global historian/alarm misuse rejected;
- no fabricated network diagnostics;
- relevant CI green;
- central composition/UI hooks explicitly handed off.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`

---

# DEV 3 - EliteSCADA

**Role:** `WORKER`

**CurrentTask:** Public Script Engineering integration foundation

**Branch:** `feature/script-engineering-integration`

**Status:** `PR_OPEN`

**PullRequest:** `#47` — Draft — **IMPLEMENTED IN PR / NOT MERGED**

**ObservedHead:** `da6dd4914741a6fa9ece4c758d245899fb20af92`

**Objective:**

Create an isolated Script Engineering domain on top of merged PR #41, ready for later coordinator-owned canonical schema/package integration without colliding with DEV 2's central Engineering contract work.

**AllowedScope:**

- `src/Scada.Engineering/VisualScripting/**`
- isolated `src/Scada.Engineering/Scripts/**`
- focused tests for Script identity/scope/language/source/entry points/dependencies/references
- adapters to merged PR #41 public runtime/sandbox contracts
- PR `INTEGRATION REQUIRED` documentation

**ForbiddenScope:**

- `src/Scada.Engineering/Contracts/EngineeringContracts.cs` in this wave
- central schema migration/version changes while DEV 2 owns the contract
- `src/Scada.Api/Program.cs` / central DI
- frontend routing/shell
- concrete Python engine/editor
- graphical visual editor
- Server Python runtime
- script authorization bypasses
- coordinator-owned docs/workflows

**MustReadSpecific:**

- `docs/PYTHON-SCRIPTING-AND-VISUAL-RUNTIME.md`
- `docs/ADR-004-ENGINEERING-IMPORT-EXPORT.md`
- `docs/ROADMAP.md` stages 11–15
- merged `src/Scada.Engineering/VisualScripting/**`
- existing Screen/Popup/Dynamo contracts as read-only compatibility targets

**Dependencies:** PR #41 merged. Final canonical Script collection/schema work depends on reconciliation of DEV 2 central Engineering changes.

**IntegrationRequired:**

Coordinator will later add first-class Scripts/references to the central Engineering schema, migrations, canonical JSON, preview/apply, revisions, PostgreSQL Engineering persistence and `.escadapkg`, preserving DEV 3's isolated semantics.

**ObservedImplementation:**

Current branch is isolated to new Script Engineering contracts, adapters, deterministic validation and focused tests; no central Engineering contract or application-composition files are modified. PR body states CI evidence is still pending.

**NextActions:**

1. complete focused tests and validation invariants;
2. obtain branch/PR CI;
3. keep central schema/package work out of this branch;
4. update the PR body with final test evidence and exact insertion points for coordinator integration;
5. stop at `WAIT_FOR_COORDINATOR` when completion criteria are met.

**CompletionCriteria:**

- stable typed Script Engineering contracts exist outside central shared contract;
- deterministic validation covers identity/scope/language/source/entry points/dependencies/reference integrity;
- adapters consume PR #41 public contracts rather than renderer/DOM-private models;
- no central Engineering schema changes;
- no premature Python engine/editor/graphical editor work;
- relevant CI green;
- coordinator integration requirements precise.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`
