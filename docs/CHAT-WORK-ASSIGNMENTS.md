# CHAT WORK ASSIGNMENTS — EliteSCADA

> Authoritative live execution board. GitHub branch/PR/head/CI state is operational truth. Permanent rules: `docs/DEVELOPMENT-WAVES.md`, `docs/PARALLEL-WORK.md` and `docs/CI-USAGE-POLICY.md`.

**Last coordinator synchronization:** 2026-08-28

## Mandatory `siga`

Every fixed EliteSCADA chat first rereads current `main`: `PROJECT GOAL.md`, `LAST CHANGE.md`, `docs/ROADMAP.md`, `docs/PARALLEL-WORK.md`, `docs/DEVELOPMENT-WAVES.md`, this board, `docs/CI-USAGE-POLICY.md`, `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md` and current `MustReadSpecific`. Then verify real branch/PR/head/CI and execute only the current authorized assignment.

`NextQueuedTask` is planning only. Workers never modify `main`, merge their own PR, work another DEV branch or broaden their mission.

## Current product gate

`SCRIPT-WAVE-05` is **COMPLETE / MERGED**.

- Logical WaveBaseSHA: `e9e596f482c83bf5864b34a7f54d9fd3b0b67baa`
- Frozen ContractSHA: `b08b45201bf25a6d4d403b07c511cc34444177db`
- Final integration head: `13d3f8283275dc957d9d6168fc7fb165df992d7e`
- Final CI #466 / run `33139334379`: Web SUCCESS; backend Release/full tests including PostgreSQL SUCCESS; Runtime smoke SUCCESS; Chromium SUCCESS
- Coordinator PR #79: MERGED
- Main merge: `7f629bf660bb16fd46fbf6abeb72b9ca1676e087`

Merged product now includes canonical Engineering schema v10 with first-class Scripts, Script stable references/validation, persistence/package fidelity, dependency-safe CAS delete, and a practical Script Engineering Workspace. Production Python execution remains **SPECIFIED / NOT IMPLEMENTED**.

`PYTHON-WAVE-06` is **QUEUED — COORDINATOR DEFINITION-OF-READY ONLY**. Workers are not authorized to start Wave 06 yet.

CI budget mode remains **CONSTRAINED** until the allowance resets on 2026-09-01. This changes CI frequency, not quality gates.

---

# COORDENADOR - EliteSCADA

**Role:** `COORDINATOR`  
**Wave:** `PYTHON-WAVE-06 PREPARATION`  
**Status:** `ACTIVE — DEFINITION_OF_READY`  
**CurrentTask:** pin the Wave 06 Client Visual Python editor/sandbox implementation boundary and derive parallel-safe worker slices  
**BaseCandidate:** `7f629bf660bb16fd46fbf6abeb72b9ca1676e087`  
**StartCondition:** `TRUE`

**DependsOn:** Wave 05 merged/green; canonical Script Engineering v10; merged Python/VisualScripting research/foundations.

**Objective:** prepare a production-safe Wave 06 plan for a practical Python editor and browser Client Visual sandbox while preserving the canonical Script model and denying direct driver/database/filesystem/shell/arbitrary-network/credential authority.

**Required coordinator decisions before worker promotion:**

- exact browser sandbox implementation boundary and isolation mechanism;
- versioned/narrow EliteSCADA Client Visual API exposed to Python;
- cancellation/time budget/bounded queue/error propagation rules;
- Script source validation/diagnostic contract including line/column;
- editor boundary versus execution boundary;
- TAG read/write permissions and Client Memory authority;
- events/timers lifecycle and deterministic disposal boundaries;
- final Wave 06 acceptance gate and parallel dependency map;
- CI strategy under `CONSTRAINED` mode.

**ReservedFiles:** coordination docs, `.github/workflows/**`, canonical Engineering schema/contracts, `Program.cs`, central DI/composition, `EngineeringApp.tsx`, `main.tsx`, central Runtime/visual authority contracts until explicitly delegated.

**ForbiddenScope:** starting worker implementation before Definition of Ready; Server Python; graphical editor; new protocols; direct infrastructure authority from Python; weakening security/CAS/lifecycle.

**ValidationMatrix:** architecture/research reconciliation first; no expensive full CI for docs-only planning. Full CI becomes mandatory when Wave 06 produces an integrated product checkpoint.

**CompletionCriteria:** Wave 06 architecture boundary is explicit; up to three parallel-safe worker tasks have branch/BaseSHA/AllowedScope/ReservedFiles/ValidationMatrix/CompletionCriteria; board explicitly promotes them ACTIVE.

**AfterCompletion:** create Wave 06 integration/worker branches from the selected healthy base and promote DEV assignments.

**MustReadSpecific:** `docs/PYTHON-SCRIPTING-AND-VISUAL-RUNTIME.md`, `docs/research/python-client/CLIENT-PYTHON-EDITOR-SANDBOX-RESEARCH.md`, `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`, `src/Scada.Engineering/Scripts/**`, `src/Scada.Engineering/VisualScripting/**`.

---

# DEV 1 - EliteSCADA

**Role:** `WORKER`  
**Status:** `WAIT_FOR_COORDINATOR — WAVE_05_COMPLETE`  
**CurrentTask:** none executable.

**Wave 05 delivery:** Script Engineering Workspace foundation accepted and merged through PR #82 into Wave 05 and then main via PR #79.

**NextQueuedTask:** `Wave 06 Python Editor UX` — `QUEUED`, not executable.

**NextStartCondition:** coordinator completes Wave 06 Definition of Ready, creates branch from official Wave 06 base and promotes this board entry to ACTIVE.

**AfterCompletion:** remain `WAIT_FOR_COORDINATOR` until promotion.

---

# DEV 2 - EliteSCADA

**Role:** `WORKER`  
**Status:** `WAIT_FOR_COORDINATOR — WAVE_05_COMPLETE`  
**CurrentTask:** none executable.

**Wave 05 delivery:** stable Script reference resolver accepted and merged through PR #81; coordinator wired it into canonical Preview/Apply before PR #79 merged.

**NextQueuedTask:** `Wave 06 Client Visual Python sandbox/runtime adapter` — `QUEUED`, not executable.

**NextStartCondition:** coordinator pins sandbox/API/isolation contracts, creates branch from official Wave 06 base and promotes this board entry to ACTIVE.

**AfterCompletion:** remain `WAIT_FOR_COORDINATOR` until promotion.

---

# DEV 3 - EliteSCADA

**Role:** `WORKER`  
**Status:** `WAIT_FOR_COORDINATOR — WAVE_05_COMPLETE`  
**CurrentTask:** none executable.

**Wave 05 delivery:** Script compatibility/acceptance validation accepted and merged through PR #80 into Wave 05 and then main via PR #79.

**NextQueuedTask:** `Wave 06 sandbox execution safety / acceptance` — `QUEUED`, not executable.

**NextStartCondition:** coordinator completes Wave 06 Definition of Ready, creates branch from official Wave 06 base and promotes this board entry to ACTIVE.

**AfterCompletion:** remain `WAIT_FOR_COORDINATOR` until promotion.

---

## Coordinator note for future chats

Current action boundary: **only COORDENADOR is executable. DEV 1/2/3 must remain stopped.** Wave 05 is merged. A worker receiving `siga` before explicit Wave 06 promotion rereads this board and stays `WAIT_FOR_COORDINATOR`. CI remains constrained until 2026-09-01; do not spend full matrices on planning or unchanged heads.