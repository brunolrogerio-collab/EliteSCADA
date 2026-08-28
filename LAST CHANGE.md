# LAST CHANGE — EliteSCADA

> Operational handoff. Resume from GitHub, not chat history.

**Handoff date:** 2026-08-28  
**Development state:** **PYTHON-WAVE-06 ACTIVE — FINAL INTEGRATION DEFECT CORRECTION / COORDINATOR HANDOFF**  
**CI budget mode:** **CONSTRAINED until 2026-09-01**

## Mandatory resume reading

Before any action read current `main`: `PROJECT GOAL.md`, `LAST CHANGE.md`, `docs/ROADMAP.md`, `docs/PARALLEL-WORK.md`, `docs/DEVELOPMENT-WAVES.md`, `docs/CHAT-WORK-ASSIGNMENTS.md`, `docs/CI-USAGE-POLICY.md`, `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`, `docs/PYTHON-WAVE-06-IMPLEMENTATION-DECISION.md`, `docs/PYTHON-SCRIPTING-AND-VISUAL-RUNTIME.md`, python-client research and current Wave 06 source/tests.

GitHub branch/PR/head/CI state is operational truth. Distinguish `MERGED`, `MERGED_TO_INTEGRATION`, `IMPLEMENTED IN PR`, `RESEARCH MERGED / PRODUCTION NOT IMPLEMENTED`, and `SPECIFIED / NOT IMPLEMENTED`.

## Official main state

Current `main` before this handoff update: `4fd7dc7ac858734e2e5811e731ac5e94c0d900f5` plus this documentation-only handoff commit.

Wave 05 is **MERGED** via PR #79 / merge `7f629bf660bb16fd46fbf6abeb72b9ca1676e087`; final CI #466 was fully green. Canonical Engineering schema v10 includes first-class Scripts and Script Engineering Workspace.

Wave 06 remains **NOT MERGED TO MAIN**.

## PYTHON-WAVE-06 exact state

- Logical WaveBaseSHA: `7f629bf660bb16fd46fbf6abeb72b9ca1676e087`
- Central ContractSHA: `01d5b3092cf9c33ffa41c12b79133157b24cd148`
- Integration branch: `integration/python-wave-06`
- Integration PR: #83 `Establish Wave 06 Client Visual Python foundation`
- PR state: **OPEN / DRAFT / mergeable / NOT MERGED**
- Current integration head: **`79546d9a8fb39786eec7b0bd34f87723c9261a8d`**
- Contract CI #468: fully green
- DEV 2 exact-head CI #469: fully green

### MERGED_TO_INTEGRATION / NOT MAIN

- PR #85 DEV 1: Monaco Python Editor UX.
- PR #86 DEV 2: Client Visual Pyodide/Web Worker runtime adapter.
- PR #84 DEV 3: foundation/static sandbox acceptance.
- same-origin pinned Pyodide publication at `/pyodide/` for dev/build/acceptance;
- trusted capability provider binding `tag.read` to authenticated Runtime TAG read and `clientMemory.read/write` to the owning browser Client Memory store;
- dynamic real-Pyodide adversarial Playwright acceptance;
- native Pyodide escape coverage;
- compile-before-canonical-Preview integration for `ClientVisual` drafts;
- controlled Engineering handler preview that does not save/publish/activate Engineering;
- Python entry-point completion deduplication fix;
- shell acceptance updated from the obsolete Wave 05 `Sem execução Python nesta wave` message;
- attempted Pyodide hardening correction preserving engine internals while denying `micropip`, `pyodide.http`, `pyodide_js` and `pyodide.code.run_js` exposure.

The temporary coordinator correction branch `fix/python-wave-06-final-integration` ended at `13d960ff3bfe74d387bb25a4912fe6863e4b8a8b` and is already an ancestor of integration head `79546d9...`; it currently contains no unique work that must be separately integrated.

## Latest integrated CI — #483 FAILED

Run: `33183960875` on integration head `79546d9a8fb39786eec7b0bd34f87723c9261a8d`.

- **Web build: SUCCESS**
- **Backend build/full tests/Runtime smoke: SUCCESS**
- **Chromium E2E: FAILURE**
- Browser result: **118 passed / 5 failed**

### Open Chromium failures

1. `interface-wave-03-readiness.spec.ts` locale loop timed out navigating to Engineering / finding `#engineering-locale`. This test has shown intermittent behavior in earlier CI and the log includes Vite WebSocket `EPIPE`; treat as an open targeted reliability finding, not automatically as a product regression.
2. `python-editor-workspace.spec.ts`: `Apply Preview` remains disabled after editing/Preview. This is downstream of the new compile-before-Preview path and must not be worked around by weakening the test.
3. `python-sandbox-dynamic.spec.ts` real sandbox capability/client-local-state case fails during runtime initialization with `PYTHON_COMPILE_FAILED`.
4. `python-sandbox-dynamic.spec.ts` timeout/cancellation/queue/disposal case fails during initialization with the same `PYTHON_COMPILE_FAILED`.
5. `python-sandbox-native-escapes.spec.ts` fails during initialization with the same `PYTHON_COMPILE_FAILED`.

The **primary Wave 06 product blocker is the real Pyodide engine compile-diagnostics path**. Bootstrap succeeds far enough to initialize the runtime, but `getCompileDiagnostics()` catches an exception from its `runPython()` compile helper and returns the sanitized fallback `Python source could not be compiled safely.` The previous import-guard correction was insufficient.

Do **not** rerun CI #483 unchanged hoping for green.

## Worker state at handoff

### DEV 1
`WAIT_FOR_COORDINATOR — DELIVERY_ACCEPTED`. No executable task. Wave 07 queue remains non-executable.

### DEV 2
`WAIT_FOR_COORDINATOR — DELIVERY_ACCEPTED`. No executable task. Wave 07 queue remains non-executable.

### DEV 3
`WAIT_FOR_COORDINATOR — DYNAMIC TESTS INTEGRATED / FINDINGS OPEN`.

The original DEV 3 chat reached a practical conversation limitation, so the coordinator temporarily executed the dynamic acceptance continuation. The relevant dynamic tests are already in `integration/python-wave-06`; branch `test/python-wave-06-sandbox-safety` is behind the integration train and must not be treated as containing additional unintegrated delivery. Do not start a new DEV 3 mission unless the new coordinator explicitly reassigns one.

## New coordinator next sequence

1. Verify current `main`, PR #83, `integration/python-wave-06`, head and CI again before writing anything.
2. Diagnose the **real Pyodide compile failure first**, using code inspection and focused evidence. Inspect `clientVisualPythonWorker.ts`, especially `getCompileDiagnostics`, Pyodide `runPython()`/globals behavior, `jsglobals`, import guard changes and Python proxy/return-value handling.
3. Prefer a coordinator correction branch with no PR while iterating so every tiny fix does not spend a full PR matrix. Promote to `integration/python-wave-06` only at a coherent checkpoint.
4. Ensure the canonical path remains: exact `ClientVisual` draft -> engine compile diagnostics -> diagnostic snapshot -> canonical Preview/Apply/CAS. Do not bypass engine diagnostics merely to re-enable Apply.
5. Keep the controlled handler preview sandboxed and non-authoritative. It must not save, publish, activate or gain direct process/driver/database/filesystem/network/credential authority.
6. Diagnose the locale readiness timeout separately with targeted evidence; do not mask it by raising timeouts indiscriminately.
7. Once blockers are fixed, run one meaningful exact-head Wave 06 matrix: Web + backend Release/full tests including PostgreSQL + Runtime smoke + Chromium + sandbox acceptance.
8. If Actions allowance cannot support the required final matrix, use `BLOCKED_BY_CI_BUDGET`. Do not weaken final quality.
9. Mark PR #83 Ready and merge only after the exact final head is fully green.
10. After merge, synchronize official docs and only then prepare Wave 07 architecture-first Definition of Ready.

## Wave 06 final gate remains

`canonical ClientVisual Script -> Monaco edit -> engine compile diagnostics -> isolated Pyodide execution -> permitted TAG read -> owning-client Client Memory read/write -> controlled event -> bounded timeout/failure -> understandable diagnostics`

A faulty Script must not destabilize unrelated Runtime clients, UI or backend.

## Permanent rules

Workers never modify `main`, merge their own PR, choose new work or broaden scope. Canonical Engineering remains authority. Research is not production implementation. CI economy changes frequency only, never security/tests/CAS/lifecycle/persistence/Runtime guards/final evidence. Wave 07 remains queued until Wave 06 is merged and green.
