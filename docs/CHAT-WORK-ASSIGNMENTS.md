# CHAT WORK ASSIGNMENTS — EliteSCADA

> Authoritative live execution board. GitHub branch/PR/head/CI state is operational truth. Permanent rules: `docs/DEVELOPMENT-WAVES.md` and `docs/PARALLEL-WORK.md`.

**Last coordinator synchronization:** 2026-08-27

## Mandatory `siga`

Every fixed EliteSCADA chat first rereads current `main`: `PROJECT GOAL.md`, `LAST CHANGE.md`, `docs/ROADMAP.md`, `docs/PARALLEL-WORK.md`, `docs/DEVELOPMENT-WAVES.md`, this board, current `MustReadSpecific` and `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`. Then verify real branch/PR/head/CI and execute only the current authorized assignment.

`NextQueuedTask` is planning only. Workers never modify `main`, merge their own PR, work another DEV branch or broaden their mission.

## Current product gate

`SCRIPT-WAVE-05` is **ACTIVE — ARCHITECTURE-FIRST COORDINATOR PHASE**.

**WaveBaseSHA:** `e9e596f482c83bf5864b34a7f54d9fd3b0b67baa`  
**IntegrationBranch:** `integration/interface-wave-05`  
**Product objective:** make Scripts first-class canonical Engineering entities whose source, scope, enabled state, entry points, dependencies and stable references survive canonical JSON, Preview/Apply, revisions/PostgreSQL and `.escadapkg` before production Python editor/runtime work begins.

Wave 04 is **MERGED** through PR #78. Final integration head `f0762d12814496a223abe740c57eb995ca472e97`; CI #446 fully green; main merge `e9e596f482c83bf5864b34a7f54d9fd3b0b67baa`.

Documentation-only coordination commits after WaveBaseSHA do not invalidate the logical base. Product-code `main` remains frozen while the Wave 05 central contract is stabilized.

---

# COORDENADOR - EliteSCADA

**Role:** `COORDINATOR`  
**Wave:** `SCRIPT-WAVE-05`  
**CurrentTask:** stabilize canonical Script Engineering contract before worker start  
**Branch:** `integration/interface-wave-05` plus `main` for coordination docs  
**Status:** `ACTIVE — ARCHITECTURE_FIRST`  
**BaseSHA:** `e9e596f482c83bf5864b34a7f54d9fd3b0b67baa`  
**StartCondition:** `TRUE`

**DependsOn:** Wave 04 merged/green; merged isolated Script/VisualScripting foundations; canonical Engineering v9 and project lifecycle/package infrastructure.

**Objective:** promote the existing isolated Script Engineering foundation into the authoritative canonical Engineering model without creating a parallel script store/model.

**Required central work:**

- evolve canonical Engineering schema deliberately from v9 to v10 with backward compatibility;
- first-class canonical Scripts collection/entity kind;
- stable Script/event/dependency references using the merged Script foundation;
- Workspace-owned Script registry with normal dirty/changeVersion semantics;
- canonical JSON Export/Import + Preview/Apply;
- deterministic validation using existing `ScriptEngineeringValidator` and reference catalogs;
- revision/PostgreSQL fidelity through canonical persisted JSON;
- `.escadapkg` fidelity through canonical `engineering.json`;
- focused compatibility/round-trip/package/revision tests;
- exact-head CI on the integration branch before worker promotion.

**ReservedFiles:** `PROJECT GOAL.md`, `LAST CHANGE.md`, `docs/ROADMAP.md`, coordination docs, `.github/workflows/**`, `src/Scada.Api/Program.cs`, `src/Scada.Api/Runtime/EngineeringWorkspace.cs`, canonical Engineering schema/contracts, central DI/composition, `EngineeringApp.tsx`, `main.tsx`.

**ForbiddenScope:** production Python interpreter/editor/sandbox; graphical editor; new protocol production work; weakening canonical Engineering, Preview/Apply/CAS, security or lifecycle authority.

**ValidationMatrix:** Web + backend Release/full tests + Runtime smoke + Chromium after the shared contract compiles; focused schema-v10, v9 compatibility, Script validation, revision and package tests.

**CompletionCriteria:** central Script contract is canonical, compatible, persisted/package-safe and exact-head green; worker branches are then created from the frozen WaveBaseSHA and board tasks promoted with complete Definition of Ready.

**AfterCompletion:** promote DEV 1/2/3 Wave 05 slices and continue integration reviews.

**MustReadSpecific:** `docs/PYTHON-SCRIPTING-AND-VISUAL-RUNTIME.md`, `docs/research/python-client/CLIENT-PYTHON-EDITOR-SANDBOX-RESEARCH.md`, `src/Scada.Engineering/Scripts/**`, canonical Import/Export/Persistence/ProjectPackages contracts/tests.

---

# DEV 1 - EliteSCADA

**Role:** `WORKER`  
**Wave:** `SCRIPT-WAVE-05`  
**Status:** `WAIT_FOR_COORDINATOR — QUEUED_ONLY`  
**CurrentTask:** none executable yet.

**NextQueuedTask:** `Script Engineering Workspace foundation`.

**PlannedObjective:** list/create/delete/select/edit canonical Script metadata and source foundation using only the coordinator-stabilized schema/API: name/path, scope, enabled, description, entry points and dependency information. No Monaco/Python execution yet.

**NextStartCondition:** coordinator central Script contract exact-head CI green; worker branch/AllowedScope/ReservedFiles/ValidationMatrix written here; task promoted to ACTIVE.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`.

---

# DEV 2 - EliteSCADA

**Role:** `WORKER`  
**Wave:** `SCRIPT-WAVE-05`  
**Status:** `WAIT_FOR_COORDINATOR — QUEUED_ONLY`  
**CurrentTask:** none executable yet.

**NextQueuedTask:** `Script Reference Runtime / validation adapter`.

**PlannedObjective:** stable dependency/reference catalogs for TAGs, Client Memory, Server Memory and visual-definition boundaries over the coordinator-stabilized canonical Script model; preserve narrow runtime contracts and no direct driver access.

**NextStartCondition:** coordinator central Script contract exact-head CI green; worker branch/AllowedScope/ReservedFiles/ValidationMatrix written here; task promoted to ACTIVE.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`.

---

# DEV 3 - EliteSCADA

**Role:** `WORKER`  
**Wave:** `SCRIPT-WAVE-05`  
**Status:** `WAIT_FOR_COORDINATOR — QUEUED_ONLY`  
**CurrentTask:** none executable yet.

**NextQueuedTask:** `Script compatibility validation`.

**PlannedObjective:** independent compatibility/acceptance coverage for canonical JSON round-trip, revision/package fidelity, dependencies/cycles, missing references, invalid scope and migration determinism.

**NextStartCondition:** coordinator central Script contract exact-head CI green; worker branch/AllowedScope/ReservedFiles/ValidationMatrix written here; task promoted to ACTIVE.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`.

---

## Coordinator note for future chats

Current action boundary: **workers must remain stopped**. Coordinator alone is executable until the Wave 05 canonical Script contract is stabilized and proven green. A `siga` in DEV 1/2/3 before promotion means reread the board and remain `WAIT_FOR_COORDINATOR`.