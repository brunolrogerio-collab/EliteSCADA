# CHAT WORK ASSIGNMENTS — EliteSCADA

> Authoritative live execution board. GitHub branch/PR/head/CI state is operational truth. Permanent rules: `docs/DEVELOPMENT-WAVES.md`, `docs/PARALLEL-WORK.md`, `docs/CI-USAGE-POLICY.md`.

**Last coordinator synchronization:** 2026-08-28 — coordinator handoff after CI #483

## Mandatory `siga`

Every fixed EliteSCADA chat first rereads current `main`: `PROJECT GOAL.md`, `LAST CHANGE.md`, `docs/ROADMAP.md`, `docs/PARALLEL-WORK.md`, `docs/DEVELOPMENT-WAVES.md`, this board, `docs/CI-USAGE-POLICY.md`, `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`, `docs/PYTHON-WAVE-06-IMPLEMENTATION-DECISION.md` when Wave 06 is relevant, and current `MustReadSpecific`. Then verify real branch/PR/head/CI and execute only the current authorized assignment.

Workers never modify `main`, merge their own PR, choose a new mission or broaden scope. `NextQueuedTask` is planning only.

## Current product gate

`PYTHON-WAVE-06` is **ACTIVE — FINAL INTEGRATION DEFECT CORRECTION**.

- Logical WaveBaseSHA: `7f629bf660bb16fd46fbf6abeb72b9ca1676e087`
- Central ContractSHA: `01d5b3092cf9c33ffa41c12b79133157b24cd148`
- Integration branch: `integration/python-wave-06`
- Integration PR: #83 Draft
- Integration head at handoff: `79546d9a8fb39786eec7b0bd34f87723c9261a8d`
- Contract CI #468: fully green
- DEV 2 exact-head CI #469: fully green
- Latest integrated CI #483 / run `33183960875`: Web SUCCESS, backend/full tests/Runtime smoke SUCCESS, Chromium FAILURE, 118 passed / 5 failed
- CI mode: `CONSTRAINED` until 2026-09-01

Wave 06 remains **IMPLEMENTED IN PR / NOT MERGED TO MAIN**.

### Current blocker

Real Pyodide runtime initialization reaches the engine but `getCompileDiagnostics()` returns `PYTHON_COMPILE_FAILED` because its `runPython()` compile helper throws. This blocks the dynamic sandbox cases and keeps canonical Script `Apply Preview` disabled in the editor workflow. The previous import-guard correction did not resolve the engine failure.

There is also one separate locale-readiness timeout with Vite WebSocket `EPIPE`; investigate as a targeted reliability finding rather than hiding it with larger generic timeouts.

---

# COORDENADOR - EliteSCADA

**Role:** `COORDINATOR`  
**Wave:** `PYTHON-WAVE-06`  
**Status:** `ACTIVE — FINAL_DEFECT_CORRECTION / HANDOFF_COMPLETE`  
**CurrentTask:** resolve the Pyodide compile-diagnostics blocker from CI #483, preserve canonical compile -> Preview/Apply/CAS semantics, diagnose the locale readiness timeout, then run one final exact-head Wave 06 matrix and merge only if fully green.  
**IntegrationBranch:** `integration/python-wave-06`  
**CurrentIntegrationHead:** `79546d9a8fb39786eec7b0bd34f87723c9261a8d`  
**LogicalBaseSHA:** `7f629bf660bb16fd46fbf6abeb72b9ca1676e087`  
**ContractSHA:** `01d5b3092cf9c33ffa41c12b79133157b24cd148`

**Coordinator-owned integrated work already present:**
- same-origin pinned Pyodide assets at `/pyodide/`;
- authorized `tag.read` provider;
- owning-client `clientMemory.read/write` provider;
- dynamic real-Pyodide adversarial tests;
- native Pyodide escape tests;
- compile-before-canonical-Preview integration;
- controlled Engineering handler preview;
- entry-point deduplication correction;
- shell acceptance correction;
- Pyodide import/escape hardening attempt.

**Latest CI findings:**
- `interface-wave-03-readiness.spec.ts` locale navigation timeout;
- `python-editor-workspace.spec.ts` Apply remains disabled after Preview attempt;
- both real `python-sandbox-dynamic.spec.ts` cases fail at initialization with `PYTHON_COMPILE_FAILED`;
- `python-sandbox-native-escapes.spec.ts` fails at initialization with `PYTHON_COMPILE_FAILED`.

**Execution rule:** do not rerun unchanged failing head. Diagnose with code inspection/focused evidence first. Prefer a no-PR correction branch for iterative fixes, then advance integration only at a coherent checkpoint so Actions minutes are not burned on every edit.

**ReservedFiles:** coordination docs, `.github/workflows/**`, `web/scada-web/package.json`, `web/scada-web/vite.config.ts`, Python bridge/runtime central files, `web/scada-web/src/engineering/EngineeringApp.tsx`, central Script/Python composition, `main.tsx`, canonical Engineering schema/contracts and backend central composition.

**ForbiddenScope:** Server Python; Wave 07+ visual model/editor; new protocols; weakening security/CAS/lifecycle/persistence; bypassing engine compile diagnostics to force Preview green; direct Python driver/database/filesystem/shell/arbitrary-network/credential authority.

**CompletionCriteria:** root causes fixed; exact final integration head passes Web + backend Release/full tests incl. PostgreSQL + Runtime smoke + Chromium + Wave 06 sandbox acceptance; PR #83 Ready and merged; docs synchronized.

**AfterCompletion:** prepare Wave 07 architecture-first Definition of Ready only after Wave 06 merge.

**MustReadSpecific:** `docs/PYTHON-WAVE-06-IMPLEMENTATION-DECISION.md`, `docs/PYTHON-SCRIPTING-AND-VISUAL-RUNTIME.md`, `docs/research/python-client/CLIENT-PYTHON-EDITOR-SANDBOX-RESEARCH.md`, current `web/scada-web/src/python-runtime/**`, current Python editor/Script workspace files and Wave 06 E2E tests.

---

# DEV 1 - EliteSCADA

**Role:** `WORKER`  
**Wave:** `PYTHON-WAVE-06`  
**Status:** `WAIT_FOR_COORDINATOR — DELIVERY_ACCEPTED`  
**CurrentTask:** none executable.  
**Delivery:** PR #85 `Add Monaco Python editor UX`, head `35f46fd8b74aa710c924c02374900540f24e73ad`, already integrated.

**NextQueuedTask:** Wave 07 Visual Property Registry / Engineering projection — `QUEUED`, not executable.  
**NextStartCondition:** Wave 06 merged and explicit Wave 07 promotion.

---

# DEV 2 - EliteSCADA

**Role:** `WORKER`  
**Wave:** `PYTHON-WAVE-06`  
**Status:** `WAIT_FOR_COORDINATOR — DELIVERY_ACCEPTED`  
**CurrentTask:** none executable.  
**Delivery:** PR #86 `Implement Client Visual Python worker runtime`, head `d8db6e15829d16ee06eb471a7d2afb3f7f869f3c`, exact-head CI #469 fully green, already integrated.

**NextQueuedTask:** Wave 07 Visual Runtime Instance — `QUEUED`, not executable.  
**NextStartCondition:** Wave 06 merged and explicit Wave 07 promotion.

---

# DEV 3 - EliteSCADA

**Role:** `WORKER`  
**Wave:** `PYTHON-WAVE-06`  
**Status:** `WAIT_FOR_COORDINATOR — DYNAMIC TESTS INTEGRATED / FINDINGS OPEN`  
**CurrentTask:** none executable.

The original DEV 3 chat reached a practical conversation limitation. The coordinator temporarily executed its authorized dynamic sandbox acceptance continuation. Relevant dynamic/adversarial tests are already in `integration/python-wave-06`. Branch `test/python-wave-06-sandbox-safety` is behind the integration train and does not contain an additional delivery that needs merging.

The test findings are now coordinator integration defects, especially real Pyodide `PYTHON_COMPILE_FAILED`. Do not create a new DEV 3 task or branch unless the coordinator explicitly reassigns one.

**NextQueuedTask:** Wave 07 Python <-> Visual API acceptance — `QUEUED`, not executable.  
**NextStartCondition:** Wave 06 merged and explicit Wave 07 promotion.

---

## Coordinator note

Current execution boundary: **all three worker chats are stopped**. The next executable work belongs to the **COORDENADOR** on Wave 06 final defect correction. No Wave 07 implementation starts before Wave 06 exact-head final gate and merge.
