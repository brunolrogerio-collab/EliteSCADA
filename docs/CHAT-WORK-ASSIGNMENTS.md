# CHAT WORK ASSIGNMENTS — EliteSCADA

> Authoritative live coordination board. GitHub branch/PR/head/CI state is operational truth; if this file briefly lags GitHub, GitHub wins and the coordinator reconciles it.

**Coordination protocol introduced:** 2026-08-26
**Last coordinator synchronization:** 2026-08-26

## Permanent `siga` protocol

Before any action, every fixed EliteSCADA chat rereads current `main`: `PROJECT GOAL.md`, `LAST CHANGE.md`, `docs/ROADMAP.md`, `docs/PARALLEL-WORK.md`, this file, and every document listed in its `MustReadSpecific` assignment. Then it verifies the real assigned branch, PR/head and CI.

Workers never choose a new task, alter `main`, merge their own PR, work another DEV branch, or broaden their assignment. `WAIT_FOR_COORDINATOR` means stop after delivery.

Repository terminology:

- **MERGED** = official `main` state.
- **IMPLEMENTED IN PR** = exists only in an open worker PR/branch.
- **SPECIFIED / NOT IMPLEMENTED** = documented intent without merged implementation.

---

# COORDENADOR - EliteSCADA

**Role:** `COORDINATOR`

**CurrentTask:** Coordinate the first TAG Gateway Engineering slice after completed Internal Memory product integration

**Branch:** `main`

**Status:** `IN_PROGRESS`

**PullRequest:** none

**Objective:**

Treat Internal Memory as a completed merged product block, preserve the mandatory source/protocol order, give one worker exclusive ownership of the first public/versioned TAG Gateway Engineering contract, and keep runtime Gateway implementation blocked until that contract is reviewed and official on `main`.

**AllowedScope:** coordinator-owned shared/central files, integration hooks, workflow maintenance, assignment board, handoff/roadmap documentation, worker assignment and merge decisions.

**ForbiddenScope:**

- no known-failing merge;
- no force-reset/discard of worker commits;
- no runtime TAG Gateway implementation that bypasses the public/versioned Engineering contract;
- no new external protocol family before Gateway and common diagnostics/interface preview gates;
- no concurrent worker ownership of `src/Scada.Engineering/Contracts/EngineeringContracts.cs`;
- no canonical Script schema integration while the active Gateway Engineering worker owns overlapping central Engineering contract files.

**MustReadSpecific:**

- `docs/TAG-GATEWAY.md`
- `docs/INTERNAL-MEMORY-TAGS.md`
- `docs/ADR-004-ENGINEERING-IMPORT-EXPORT.md`
- `docs/COMMUNICATION-DRIVER-DIAGNOSTICS.md`

**ObservedGitHubState:**

- PR #46 Audit UI is **MERGED** as `5629f55699d68d70d11d7058c26033d54306b570` after CI #244 passed.
- PR #47 Script Engineering foundation is **MERGED** as `8a8a52e9e1725fca5b9e06ff9f560f583dab5bbb` after CI #248 passed.
- PR #48 Internal Memory Engineering + durable Server Memory retention is **MERGED** as `ad4a7aa8d17e4e7370e5801d470a69f1a096bab4` after CI #265 and post-merge CI #266 passed.
- PR #49 complete Internal Memory runtime/product integration is **MERGED** as `18e0c5f55bf33958d040ed3a2f2b948e34d3cc5f`.
- PR #49 final head `f12e4dc7b65f4ab41f47f26aca779dcd9aa0fde9` passed CI #296 across Web, backend build/tests/runtime smoke and Chromium E2E.
- Post-merge `main` CI #297 passed the same full stack on merge commit `18e0c5f55bf33958d040ed3a2f2b948e34d3cc5f`.
- Internal Memory complete product integration is therefore **MERGED / COMPLETE** and no longer blocks TAG Gateway.
- Canonical Engineering remains schema v8 until the Gateway Engineering worker deliberately evolves it with compatibility coverage.

**Dependencies:**

- TAG Gateway is now the active locked source/protocol block.
- The first Gateway implementation slice is the public/versioned Engineering contract and deterministic validation; runtime execution follows only after that contract is official.
- DEV 2 has exclusive worker ownership of central Gateway Engineering contract changes for this slice.
- DEV 1 and DEV 3 remain `WAIT_FOR_COORDINATOR` to avoid artificial overlap.
- Canonical Script package/schema integration remains valid future work but is deferred while DEV 2 owns overlapping central Engineering contract surfaces.

**NextActions:**

1. let DEV 2 create `feature/tag-gateway-engineering` from the current assignment baseline and implement only the assigned Engineering/validation slice;
2. inspect DEV 2 branch/PR/head/diff/CI on the next coordinator `siga`;
3. reconcile and merge only after focused tests and full relevant CI are green;
4. after the Gateway Engineering contract is official on `main`, assign the protocol-independent runtime Gateway engine as the next slice;
5. keep common multi-driver diagnostics and interface-validation preview blocked until their preceding gates are complete;
6. keep DEV 1 and DEV 3 idle until a non-conflicting explicit assignment is recorded.

**AfterCompletion:** `CONTINUE_COORDINATION`

---

# DEV 1 - EliteSCADA

**Role:** `WORKER`

**CurrentTask:** Audit UI and diagnostics client foundation

**Branch:** `feature/audit-ui`

**Status:** `MERGED / WAITING`

**PullRequest:** `#46` — **MERGED**

**MergedCommit:** `5629f55699d68d70d11d7058c26033d54306b570`

**Validation:** CI #244 passed Web build, backend build/tests, runtime smoke and Chromium E2E.

**NextActions:** none. Do not create a branch or select another roadmap item. On `siga`, report that this assignment is merged and `WAIT_FOR_COORDINATOR` is active unless this board contains a new task.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`

---

# DEV 2 - EliteSCADA

**Role:** `WORKER`

**CurrentTask:** Public TAG Gateway Engineering contract and deterministic validation foundation

**Branch:** `feature/tag-gateway-engineering`

**Status:** `ASSIGNED`

**PullRequest:** none yet

**Objective:**

Implement the first-class public/versioned Gateway/TAG Bridge Engineering domain and deterministic Preview validation required by `docs/TAG-GATEWAY.md`, without implementing runtime transfer execution, API/DI composition, diagnostics UI or protocol-specific behavior.

**AllowedScope:**

- new isolated Gateway Engineering files under `src/Scada.Engineering/Gateway/**`;
- **narrow explicit exception:** `src/Scada.Engineering/Contracts/EngineeringContracts.cs` only for the canonical Gateway route collection/contracts required by this assignment;
- `src/Scada.Engineering/ImportExport/EngineeringExchangeService.cs` only for Gateway schema/export/parse/preview/apply integration;
- Gateway-specific additions under `src/Scada.Engineering/ImportExport/Handlers/**` and adjacent import/export helpers when necessary;
- focused tests under `tests/Scada.Core.Tests/**`;
- focused persistence/package compatibility tests under `tests/Scada.Persistence.PostgreSql.Tests/**` when required to prove revision/project-package preservation;
- PR body updates describing implementation, tests, CI and exact `INTEGRATION REQUIRED` runtime hooks.

**ForbiddenScope:**

- `main`;
- `src/Scada.Api/Program.cs`, API endpoints, central DI/composition and hosted services;
- `src/Scada.DriverHost/**`, runtime Gateway execution, TAG event subscriptions or destination write routing;
- frontend routing/shell or Gateway UI;
- `.github/workflows/**`;
- protocol-specific driver code;
- common communication-driver diagnostics implementation;
- Python/Script canonical integration;
- Client Memory as a Gateway endpoint;
- silent type coercion, hidden scripts or browser-only Gateway configuration;
- runtime Audit/event flooding behavior.

**MustReadSpecific:**

- `docs/TAG-GATEWAY.md`
- `docs/INTERNAL-MEMORY-TAGS.md`
- `docs/ADR-004-ENGINEERING-IMPORT-EXPORT.md`
- `docs/COMMUNICATION-DRIVER-DIAGNOSTICS.md`

**Dependencies:**

- Internal Memory complete product integration is official on `main` through PR #49 merge `18e0c5f55bf33958d040ed3a2f2b948e34d3cc5f` and post-merge CI #297.
- Canonical Engineering baseline is schema v8.
- This worker exclusively owns Gateway-related changes to `EngineeringContracts.cs` for the duration of this assignment.

**CompletionCriteria:**

1. Gateway routes are a first-class collection/entity in canonical `scada.engineering`, not metadata, scripts or browser state.
2. Public route semantics include stable ID, stable key/name, enabled state, source TAG stable ID/path, destination TAG stable ID/path, OnChange/Periodic mode, source-quality policy, deterministic type/conversion policy, bounded rate/deadband controls, startup/initial-transfer policy and description/metadata where appropriate.
3. If the canonical package shape requires a schema evolution, advance from v8 to the next schema version deliberately and preserve deterministic compatibility for v1-v8 inputs; never create a parallel private schema.
4. Preview validation deterministically rejects missing or ID/path-mismatched endpoints, Client Memory endpoints, read-only/unwritable destinations detectable from Engineering, invalid/disabled source ownership where applicable, duplicate route identity, direct or indirect active cycles, multiple active Gateway writers to one destination, pathological intervals/rate settings and unsafe type/conversion combinations.
5. Fan-out from one source to several distinct destinations remains valid.
6. `builtin.memory.server` is accepted as a normal server-owned source or destination; `builtin.memory.client` is rejected.
7. Canonical JSON export/parse/preview/apply round trips preserve Gateway routes and references.
8. Revision/project-package persistence/backup round trips preserve Gateway routes without adding mutable runtime diagnostics/state to Engineering.
9. No runtime route engine, API/DI hook, protocol-specific adapter or UI is introduced in this worker slice.
10. Focused tests cover the validation graph and compatibility rules, and relevant full CI is green.
11. PR body lists exact coordinator/runtime `INTEGRATION REQUIRED` items for the later Gateway engine slice.

**NextActions:**

1. create/use only `feature/tag-gateway-engineering` from current `main`;
2. implement the smallest coherent Gateway Engineering domain satisfying the completion criteria;
3. add focused automated tests before broad integration changes;
4. open/update a Draft PR to `main` with `IMPLEMENTED IN PR / NOT MERGED` wording;
5. run/observe CI, correct attributable failures without weakening tests;
6. when completion criteria are met and CI is green, stop under `WAIT_FOR_COORDINATOR`.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`

---

# DEV 3 - EliteSCADA

**Role:** `WORKER`

**CurrentTask:** Public Script Engineering integration foundation

**Branch:** `feature/script-engineering-integration`

**Status:** `MERGED / WAITING`

**PullRequest:** `#47` — **MERGED**

**MergedCommit:** `8a8a52e9e1725fca5b9e06ff9f560f583dab5bbb`

**Validation:** CI #248 passed Web build, backend build/tests, runtime smoke and Chromium E2E.

**MergedScope:** isolated Script Engineering contracts, adapters, deterministic validation and focused tests. No concrete Python engine/editor or graphical editor was introduced.

**IntegrationRequired:** first-class Scripts/references still require canonical Engineering schema/package integration. This remains deferred while the active Gateway Engineering slice owns overlapping central contract files.

**NextActions:** none. Do not start Python editor/sandbox, graphical editor or canonical Script schema work until this board gives a new task. On `siga`, report `WAIT_FOR_COORDINATOR`.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`
