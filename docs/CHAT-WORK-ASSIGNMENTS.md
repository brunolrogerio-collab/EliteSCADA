# CHAT WORK ASSIGNMENTS — EliteSCADA

> Authoritative live coordination board. GitHub branch/PR/head/CI state is operational truth; if this file briefly lags GitHub, GitHub wins and the coordinator reconciles it.

**Coordination protocol introduced:** 2026-08-26
**Last coordinator synchronization:** 2026-08-26

## Permanent `siga` protocol

Before any action, every fixed EliteSCADA chat rereads current `main`: `PROJECT GOAL.md`, `LAST CHANGE.md`, `docs/ROADMAP.md`, `docs/PARALLEL-WORK.md`, this file, and all documents in its `MustReadSpecific` assignment. Then it verifies the real assigned branch, PR/head and CI.

Workers never choose a new task, alter `main`, merge their own PR, work another DEV branch, or broaden their assignment. `WAIT_FOR_COORDINATOR` means stop after delivery.

Repository terminology:

- **MERGED** = official `main` state.
- **IMPLEMENTED IN PR** = exists only in an open worker PR/branch.
- **SPECIFIED / NOT IMPLEMENTED** = documented intent without merged implementation.

---

# COORDENADOR - EliteSCADA

**Role:** `COORDINATOR`

**CurrentTask:** Reconcile merged Audit/Script foundations, stabilize central CI, and prepare Internal Memory integration

**Branch:** `main`

**Status:** `IN_PROGRESS`

**PullRequest:** none

**Objective:**

Keep the merged Audit UI and isolated Script Engineering foundation stable, own their central integration hooks, fix coordinator-owned CI assumptions, review DEV 2 Internal Memory completion, and preserve the locked source/protocol and visual dependency chains.

**AllowedScope:** coordinator-owned shared/central files, integration hooks, workflow maintenance, assignment board, handoff/roadmap documentation and merge decisions.

**ForbiddenScope:**

- no known-failing merge;
- no force-reset/discard of worker commits;
- no TAG Gateway before complete Internal Memory product integration is official on `main`;
- no canonical Script schema integration before DEV 2 central Engineering changes are reconciled;
- no claim that PR #48 is merged while it remains open.

**MustReadSpecific:**

- `docs/INTERNAL-MEMORY-TAGS.md`
- `docs/PYTHON-SCRIPTING-AND-VISUAL-RUNTIME.md`
- `docs/ADR-004-ENGINEERING-IMPORT-EXPORT.md`
- active PR #48 body/diff/CI

**ObservedGitHubState:**

- PR #47 is **MERGED** as `8a8a52e9e1725fca5b9e06ff9f560f583dab5bbb` after CI #248 passed. It adds the isolated Script Engineering domain only; canonical schema/package integration remains coordinator-owned and dependency-blocked by DEV 2.
- PR #46 is **MERGED** as `5629f55699d68d70d11d7058c26033d54306b570` after CI #244 passed.
- Audit central navigation files were added in `04199395fa62037d413781c7e7623bafe857a5e1` and `f1059b89bd41a3b9f9a0af0b602a1a683fef5f14`; `/audit` routing was wired in `838053a85d2382577f15db7da8a431eff92889c0`.
- PR #48 remains open Draft on `feature/internal-memory-product-integration`; observed current head `d676cae2c3cee76ecf4238d2e8b6c495aa907d4c`.
- CI #251 on PR #48: Web build PASS; backend restore/build PASS with 0 warnings/errors; all automated tests PASS; runtime smoke FAIL only because the coordinator-owned workflow asserted `engineeringSchemaVersion == 7`; Chromium was skipped after that failure.
- The hard-coded schema assertion was replaced on `main` by a dynamic persisted-vs-exported schema consistency check in commit `c77d3096e6a6d225d23e4077b07704a341b1993a`.
- Main CI #257 is the reconciled validation run for the current coordinator head and is in progress at this synchronization.

**Dependencies:**

- Internal Memory complete product integration precedes TAG Gateway.
- DEV 2 retains exclusive worker ownership of Internal Memory modifications to `EngineeringContracts.cs` until PR #48 handoff.
- Canonical Script collection/schema/package integration begins only after PR #48 central Engineering changes are accepted/reconciled.
- Cross-origin exposure of `X-EliteSCADA-Audit-Next-Cursor` remains conditional on a deployment topology that actually uses a distinct browser API origin; same-origin/Vite-proxy operation does not require it.

**NextActions:**

1. validate main CI #257 after Audit routing/navigation and workflow repair;
2. let DEV 2 finish PR #48, update its PR body and obtain CI on its final head using the corrected central workflow;
3. review PR #48 diff/semantics and integrate only after a green reconciled CI;
4. after Internal Memory is official, perform coordinator-owned canonical Script schema/package integration from the merged PR #47 contracts;
5. only then consider the next safe worker assignments and the TAG Gateway gate.

**AfterCompletion:** `CONTINUE_COORDINATION`

---

# DEV 1 - EliteSCADA

**Role:** `WORKER`

**CurrentTask:** Audit UI and diagnostics client foundation

**Branch:** `feature/audit-ui`

**Status:** `MERGED / WAITING`

**PullRequest:** `#46` — **MERGED**

**MergedCommit:** `5629f55699d68d70d11d7058c26033d54306b570`

**Validation:** CI #244 passed Web build, backend build/tests, runtime smoke and Chromium E2E on the delivered worker head.

**CoordinatorIntegration:** `/audit` central routing and Runtime/Engineering/Audit navigation are now on `main`. Cross-origin cursor-header exposure remains deployment-conditional.

**NextActions:** none. Do not create a branch or select another roadmap item. On `siga`, report that the assignment is merged and `WAIT_FOR_COORDINATOR` is active unless this board contains a new task.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`

---

# DEV 2 - EliteSCADA

**Role:** `WORKER`

**CurrentTask:** Internal Memory Engineering + durable Server Memory product integration

**Branch:** `feature/internal-memory-product-integration`

**Status:** `CI_FAILED / PR_OPEN`

**PullRequest:** `#48` — Draft — **IMPLEMENTED IN PR / NOT MERGED**

**ObservedHead:** `d676cae2c3cee76ecf4238d2e8b6c495aa907d4c`

**Objective:** complete public/versioned Internal Memory Engineering representation, typed initial values, durable Server Memory retention and explicit Client/Server semantics.

**AllowedScope:**

- `src/Scada.Core/InternalMemory/**`
- `src/Scada.Core/Sources/**` when required
- narrow Internal Memory-only exception to `src/Scada.Engineering/Contracts/EngineeringContracts.cs`
- relevant `src/Scada.Engineering/ImportExport/**` and `Validation/**`
- isolated PostgreSQL Server Memory retention storage
- focused schema/import/export/retention/migration tests
- PR body/test evidence

**ForbiddenScope:**

- `.github/workflows/**`
- `src/Scada.Api/Program.cs`
- central DI/composition unless reassigned
- frontend routing/shell
- unrelated Engineering domains
- TAG Gateway or new protocols
- globalizing Client Memory or allowing it as global Historian/Alarm truth
- mutable retained values inside immutable Engineering packages
- silent incompatible-type coercion
- coordinator docs

**MustReadSpecific:** `docs/INTERNAL-MEMORY-TAGS.md`, `docs/ADR-004-ENGINEERING-IMPORT-EXPORT.md`, `docs/ARCHITECTURE.md`, merged PR #40 contracts, current Engineering migration/import-export tests, PostgreSQL persistence conventions.

**ObservedImplementation:**

- Engineering schema v8 and typed `MemoryInitialValueDto`;
- JSON/CSV round-trip and v7 compatibility;
- `builtin.memory.server` / `builtin.memory.client` validation;
- fake network address/config rejection;
- Client Memory global Historian/Alarm rejection, including unsafe Server→Client transitions with existing consumers;
- PostgreSQL retained Server Memory keyed by stable TAG ID;
- restart/path-rename restoration;
- incompatible retained type fail-closed and explicit reset coverage.

**CurrentValidation:** CI #251 on the current head built cleanly and all automated tests passed. The only failing step was the old coordinator-owned runtime smoke literal `engineeringSchemaVersion == 7`; that workflow defect is fixed on `main` by `c77d3096e6a6d225d23e4077b07704a341b1993a`. A new green CI on the final PR head is still required before handoff/merge.

**IntegrationRequired:** coordinator owns final `Program.cs`/DI/runtime composition, central API/runtime wiring and shared frontend Engineering UI hooks.

**NextActions:**

1. remain on this exact task and branch;
2. finish any remaining assigned validation, update the PR body to actual final scope and exact `INTEGRATION REQUIRED` hooks;
3. obtain CI on the final head under the corrected central workflow;
4. do not start TAG Gateway;
5. when completion criteria are met, freeze the branch and enter `WAIT_FOR_COORDINATOR`.

**CompletionCriteria:** both memory types and typed initial values public; canonical import/export/preview/migration tested; durable stable-ID retention; rename preservation; incompatible type never silently coerced; Client Memory global Historian/Alarm misuse rejected; no fake network diagnostics; relevant final CI green; central hooks documented.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`

---

# DEV 3 - EliteSCADA

**Role:** `WORKER`

**CurrentTask:** Public Script Engineering integration foundation

**Branch:** `feature/script-engineering-integration`

**Status:** `MERGED / WAITING`

**PullRequest:** `#47` — **MERGED**

**MergedCommit:** `8a8a52e9e1725fca5b9e06ff9f560f583dab5bbb`

**Validation:** CI #248 passed Web build, backend build/tests, runtime smoke and Chromium E2E on the delivered head.

**MergedScope:** isolated `src/Scada.Engineering/Scripts/**` contracts, adapters, deterministic validation and focused tests. No central `EngineeringContracts.cs`, Python engine/editor or graphical editor was introduced.

**IntegrationRequired:** coordinator will add first-class Scripts/references to canonical Engineering schema, migrations, JSON, preview/apply, revision persistence, PostgreSQL and `.escadapkg` only after DEV 2 Internal Memory central Engineering changes are reconciled.

**NextActions:** none. Do not start Python editor/sandbox or graphical editor. On `siga`, report that the assignment is merged and `WAIT_FOR_COORDINATOR` is active unless this board contains a new task.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`
