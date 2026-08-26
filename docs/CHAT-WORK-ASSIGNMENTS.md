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

**CurrentTask:** Reconcile the completed parallel wave and finish coordinator-owned Internal Memory integration

**Branch:** `main`

**Status:** `IN_PROGRESS`

**PullRequest:** none

**Objective:**

Preserve the now-merged Audit UI, Script Engineering foundation and Internal Memory Engineering/retention work; complete only the coordinator-owned shared runtime/DI/API/UI hooks required to make Internal Memory a complete product block; keep TAG Gateway blocked until those hooks are actually integrated and validated.

**AllowedScope:** coordinator-owned shared/central files, integration hooks, workflow maintenance, assignment board, handoff/roadmap documentation and merge decisions.

**ForbiddenScope:**

- no known-failing merge;
- no force-reset/discard of worker commits;
- no TAG Gateway before complete Internal Memory product integration is official on `main`;
- no new worker assignment without updating this board;
- no claim that the merged PR #48 by itself completes all runtime/client product integration hooks.

**MustReadSpecific:**

- `docs/INTERNAL-MEMORY-TAGS.md`
- `docs/PYTHON-SCRIPTING-AND-VISUAL-RUNTIME.md`
- `docs/ADR-004-ENGINEERING-IMPORT-EXPORT.md`
- merged PR #48 scope and integration notes

**ObservedGitHubState:**

- PR #46 Audit UI is **MERGED** as `5629f55699d68d70d11d7058c26033d54306b570` after CI #244 passed.
- PR #47 Script Engineering foundation is **MERGED** as `8a8a52e9e1725fca5b9e06ff9f560f583dab5bbb` after CI #248 passed.
- PR #48 Internal Memory Engineering + durable Server Memory retention is **MERGED** as `ad4a7aa8d17e4e7370e5801d470a69f1a096bab4` after final reconciled CI #265 passed Web build, backend build/tests/runtime smoke and Chromium E2E against `main` `35789b3f4910c5ba8130f6de71093e9d2e5fcb14`.
- Canonical Engineering is now schema v8 on `main`, with v7 backward compatibility for the Internal Memory evolution.
- Coordinator-owned E2E/schema assumptions were reconciled before PR #48 merge rather than weakened or bypassed.

**Dependencies:**

- Internal Memory complete product integration still precedes TAG Gateway.
- PR #48 completes the assigned Engineering/validation/durable-retention worker slice, but runtime/DI/client-session/API/UI composition remains coordinator-owned work.
- Canonical Script collection/schema/package integration may now proceed only after the coordinator reconciles the shared Engineering v8 state and without reopening DEV 3's completed branch.

**NextActions:**

1. validate the post-merge `main` CI for PR #48;
2. complete coordinator-owned Internal Memory runtime/DI composition, including durable PostgreSQL Server Memory retention wiring and shared TAG cache/Event Bus/realtime behavior;
3. design/implement per-runtime-client Client Memory composition without turning it into server-global state;
4. preserve capability authorization/Audit for external Server Memory writes and explicit reset/migration semantics;
5. expose only appropriate central API/Engineering UI hooks and never fabricate network diagnostics for memory sources;
6. run full CI and update roadmap/handoff after complete product integration;
7. keep TAG Gateway blocked until step 6 is satisfied.

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

**CurrentTask:** Internal Memory Engineering + durable Server Memory product integration

**Branch:** `feature/internal-memory-product-integration`

**Status:** `MERGED / WAITING`

**PullRequest:** `#48` — **MERGED**

**DeliveredHead:** `6c13b4d52b176a977156b4425374f11caccfe264`

**MergedCommit:** `ad4a7aa8d17e4e7370e5801d470a69f1a096bab4`

**MergedScope:**

- canonical Engineering schema v8;
- typed Internal Memory initial/default values in public Engineering;
- v7 JSON backward compatibility and legacy TAG CSV compatibility;
- validation for `builtin.memory.server` and `builtin.memory.client`;
- rejection of fake memory-source network addressing/configuration;
- Client Memory global Historian/Alarm misuse rejection, including unsafe source transitions;
- PostgreSQL durable Server Memory retention keyed by stable TAG ID;
- restart/path-rename preservation;
- incompatible retained type fail-closed behavior and explicit guarded reset semantics;
- focused Core/PostgreSQL/schema/import-export tests.

**Validation:** final reconciled CI #265 passed Web build, backend build/tests, runtime smoke and Chromium E2E against current pre-merge `main`.

**IntegrationRequired:** coordinator now owns final runtime/DI composition, per-client Client Memory lifecycle, central API/runtime/security/Audit wiring, appropriate Engineering UI hooks, and explicit reset/migration UX. Those hooks are not a new DEV 2 assignment.

**NextActions:** none. Do not start TAG Gateway, do not reopen this branch for new scope, and do not self-assign coordinator integration. On `siga`, report that PR #48 is merged and `WAIT_FOR_COORDINATOR` is active unless this board contains a new task.

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

**IntegrationRequired:** coordinator may now reconcile first-class Scripts/references with the canonical Engineering v8 schema/package path after the Internal Memory shared integration is stable.

**NextActions:** none. Do not start Python editor/sandbox or graphical editor. On `siga`, report that this assignment is merged and `WAIT_FOR_COORDINATOR` is active unless this board contains a new task.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`
