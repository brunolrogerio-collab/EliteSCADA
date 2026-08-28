# LAST CHANGE — EliteSCADA

> Operational handoff. Resume from GitHub, not chat history.

**Handoff date:** 2026-08-28  
**Development state:** **PYTHON-WAVE-06 ACTIVE — DYNAMIC SANDBOX VALIDATION / CENTRAL INTEGRATION**  
**CI budget mode:** **CONSTRAINED until 2026-09-01**

## Mandatory resume reading

Read current `main` before action: `PROJECT GOAL.md`, `LAST CHANGE.md`, `docs/ROADMAP.md`, `docs/PARALLEL-WORK.md`, `docs/DEVELOPMENT-WAVES.md`, `docs/CHAT-WORK-ASSIGNMENTS.md`, `docs/CI-USAGE-POLICY.md`, `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`, `docs/PYTHON-WAVE-06-IMPLEMENTATION-DECISION.md`, and current `MustReadSpecific`.

GitHub branch/PR/head/CI state is operational truth. Distinguish `MERGED`, `MERGED_TO_INTEGRATION`, `IMPLEMENTED IN PR`, `RESEARCH MERGED / PRODUCTION NOT IMPLEMENTED`, and `SPECIFIED / NOT IMPLEMENTED`.

## SCRIPT-WAVE-05 — MERGED

Wave 05 is complete in `main` via PR #79 / merge `7f629bf660bb16fd46fbf6abeb72b9ca1676e087`; final CI #466 fully green. Canonical Engineering schema v10 includes first-class Scripts and the practical Script Engineering Workspace.

## PYTHON-WAVE-06 — ACTIVE

**Logical WaveBaseSHA:** `7f629bf660bb16fd46fbf6abeb72b9ca1676e087`  
**Central ContractSHA:** `01d5b3092cf9c33ffa41c12b79133157b24cd148`  
**Integration branch:** `integration/python-wave-06`  
**Integration PR:** #83 Draft  
**Contract CI:** #468 fully green  
**DEV 2 exact-head CI:** #469 fully green

### MERGED_TO_INTEGRATION / NOT MAIN

- PR #85 DEV 1, Monaco Python Editor UX, head `35f46fd8b74aa710c924c02374900540f24e73ad`, integrated as `ba688bf5990361f83cfc1a07204e035d34698abd`.
- PR #86 DEV 2, Client Visual Pyodide/Web Worker runtime, head `d8db6e15829d16ee06eb471a7d2afb3f7f869f3c`, integrated as `6e5724489d899e103d92c92171f6b2143a165b9d`.
- PR #84 DEV 3, foundation/static sandbox safety acceptance, head `e24c1cb03f5046cd436d79f4c7de3455bc96d386`, integrated as `c63911fddd7e6726b067b507507c3e74baf0c0f9`.

### Coordinator integration already added after worker slices

- same-origin Pyodide asset publication script under `web/scada-web/scripts/sync-pyodide.mjs`;
- package lifecycle hooks to publish pinned Pyodide assets for dev/build/preview;
- central `createClientVisualPythonCapabilityProvider` binding `tag.read` to the existing authenticated Runtime TAG detail API and `clientMemory.read/write` to the owning browser Runtime Client `ClientMemoryStore`;
- no direct shared/process TAG write, Server Memory write, driver, database, filesystem, shell, arbitrary network, credential, DOM or browser-storage authority was added.

These coordinator changes are **IMPLEMENTED IN PR #83 / NOT MERGED TO MAIN**.

## Worker state

### DEV 1

`WAIT_FOR_COORDINATOR — DELIVERY_ACCEPTED`. No executable task. Wave 07 queue remains non-executable.

### DEV 2

`WAIT_FOR_COORDINATOR — DELIVERY_ACCEPTED`. No executable task. Wave 07 queue remains non-executable.

### DEV 3

`ACTIVE — DYNAMIC_ACCEPTANCE_CONTINUATION`.

Branch `test/python-wave-06-sandbox-safety` was fast-forwarded by coordinator to integrated checkpoint `c63911fddd7e6726b067b507507c3e74baf0c0f9` after PR #84 merged. DEV 3 should continue on this branch and open a new Draft PR against `integration/python-wave-06`.

Required remaining dynamic acceptance includes real Pyodide compile/diagnostics, same-origin bootstrap, infinite-loop timeout/hard kill, cancellation/disposal, stale response rejection, bounded/coalesced queue, five-failure throttle, denied infrastructure capabilities, permitted TAG read, owning-client Client Memory read/write, sanitized faults and isolation from unrelated Runtime/backend state.

Production defects are reported/classified, not silently fixed by DEV 3.

## Coordinator next sequence

1. Wait for DEV 3 dynamic acceptance delivery and review it.
2. Finish central compile-diagnostics connection to Monaco and controlled Client Visual execution composition if still required by the acceptance findings.
3. Integrate non-duplicative DEV 3 tests/fixes into PR #83.
4. Run **one final exact-head Wave 06 matrix** only after composition is complete: Web, backend Release/full tests including PostgreSQL, Runtime smoke, Chromium and Wave-specific sandbox acceptance.
5. If the required matrix cannot run because of Actions allowance, use `BLOCKED_BY_CI_BUDGET`; do not weaken quality.
6. Mark PR #83 Ready and merge only if exact-head final gate is fully green.
7. Synchronize docs and begin Wave 07 architecture-first Definition of Ready.

## Wave 06 final gate

`canonical ClientVisual Script -> Monaco edit -> engine compile diagnostics -> isolated Pyodide execution -> permitted TAG read -> owning-client Client Memory read/write -> controlled event -> bounded timeout/failure -> understandable diagnostics`

A faulty Script must not destabilize unrelated Runtime clients, UI or backend.

## Permanent rules

Workers never modify `main`, merge their own PR, choose new work or broaden scope. Canonical Engineering remains authority. Research is not production implementation. CI economy changes frequency only, never security/tests/CAS/lifecycle/final evidence. Wave 07 remains queued until Wave 06 is merged and green.