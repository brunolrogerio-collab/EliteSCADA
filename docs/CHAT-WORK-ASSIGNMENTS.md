# CHAT WORK ASSIGNMENTS — EliteSCADA

> Authoritative live execution board. GitHub branch/PR/head/CI state is operational truth. Permanent rules: `docs/DEVELOPMENT-WAVES.md`, `docs/PARALLEL-WORK.md`, `docs/CI-USAGE-POLICY.md`.

**Last coordinator synchronization:** 2026-08-28 — Wave 06 product functionality green; final integrated gate blocked by legacy E2E harness reliability and constrained Actions budget.

## Mandatory `siga`

Every fixed EliteSCADA chat first rereads current `main`: `PROJECT GOAL.md`, `LAST CHANGE.md`, `docs/ROADMAP.md`, `docs/PARALLEL-WORK.md`, `docs/DEVELOPMENT-WAVES.md`, this board, `docs/CI-USAGE-POLICY.md`, `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`, and current wave-specific `MustReadSpecific`. Then verify real branch/PR/head/CI and execute only the current authorized assignment.

Workers never modify `main`, merge their own PR, choose a new mission or broaden scope. `NextQueuedTask` is planning only.

## Current product gate

`PYTHON-WAVE-06` is **IMPLEMENTED IN PR / FINAL MERGE BLOCKED_BY_CI_BUDGET + E2E HARNESS RELIABILITY**.

- Logical WaveBaseSHA: `7f629bf660bb16fd46fbf6abeb72b9ca1676e087`
- Central ContractSHA: `01d5b3092cf9c33ffa41c12b79133157b24cd148`
- Integration branch: `integration/python-wave-06`
- Integration PR: #83 Draft / Open / NOT MERGED
- Current integration head: `86b80ff72690d0b14cde9c1a315b908763ad4b49`
- CI mode: `CONSTRAINED` until owner explicitly reports reset

### Exact-head evidence

CI #485 on `89f892ba...`: Web green; backend/full tests/Runtime smoke green; Chromium 122/123. Every Wave 06 Python editor/runtime/real-Pyodide sandbox/native-escape test passed. Only the legacy multilingual Wave 03 readiness test failed with Vite `ECONNRESET` / navigation-session instability.

Coordinator then promoted `86b80ff...` to use SPA navigation inside that multilingual loop without weakening assertions or increasing timeout.

CI #486 / run `33191736584` on exact head `86b80ff...`: Web green; backend/full tests/Runtime smoke green; Chromium again 122/123. All Wave 06 Python tests remained green. The sole failure remained the legacy multilingual readiness test, this time with Chromium session closure while waiting for `.user-session-menu`, Vite `ECONNRESET`, and retry timeout loading `/`.

No further speculative or unchanged-head Actions run is authorized while budget remains constrained.

### Temporary Wave 07 Actions rule

Wave 07 does **not** start before Wave 06 is merged. Once Wave 06 is fully green, merged, and Wave 07 is explicitly promoted, Wave 07 implementation may proceed while the Actions allowance remains constrained, but GitHub Actions validation is deferred until the product owner explicitly reports that the allowance has reset.

During that interval workers may implement assigned slices and write/preserve required tests, but delivery status must remain `IMPLEMENTED / CI_DEFERRED` or equivalent. No worker slice or wave may be labeled fully validated, complete or merge-ready solely because CI was deferred.

---

# COORDENADOR - EliteSCADA

**Role:** `COORDINATOR`  
**Wave:** `PYTHON-WAVE-06`  
**Status:** `BLOCKED_BY_CI_BUDGET — FINAL E2E RELIABILITY EVIDENCE PENDING`  
**CurrentTask:** preserve the validated Wave 06 product correction, do not rerun unchanged/speculative CI under constrained budget, and when sufficient Actions allowance is available resolve/contain the legacy multilingual E2E harness session instability without weakening acceptance; then obtain one fully green exact-head matrix and merge PR #83.  
**IntegrationBranch:** `integration/python-wave-06`  
**CurrentIntegrationHead:** `86b80ff72690d0b14cde9c1a315b908763ad4b49`

**Known good Wave 06 evidence:** Monaco compile/Preview/Apply path, trusted capability provider, real Pyodide dynamic sandbox, cancellation/timeout/queue/disposal, native escape denial and Script workspace acceptance all pass in CI #485 and #486.

**Remaining blocker:** legacy `interface-wave-03-readiness.spec.ts` multilingual harness/session reliability under full CI. Do not mislabel it as a Wave 06 Python regression, but do not waive the red exact-head gate either.

**ForbiddenScope:** Server Python; Wave 07+ visual implementation before Wave 06 merge; new protocols; weakening security/CAS/lifecycle/persistence/tests; bypassing engine compile diagnostics; direct Python driver/database/filesystem/shell/arbitrary-network/credential authority.

**CompletionCriteria:** one exact final integration head passes Web + backend Release/full tests incl. PostgreSQL + Runtime smoke + Chromium + Wave 06 sandbox acceptance; PR #83 Ready and merged; docs synchronized.

**AfterCompletion:** freeze/promote Wave 07 architecture-first Definition of Ready. Under the owner's temporary budget rule, Wave 07 development is allowed after promotion but Actions validation remains deferred until owner reports reset.

**MustReadSpecific:** `docs/PYTHON-WAVE-06-IMPLEMENTATION-DECISION.md`, `docs/PYTHON-SCRIPTING-AND-VISUAL-RUNTIME.md`, `docs/research/python-client/CLIENT-PYTHON-EDITOR-SANDBOX-RESEARCH.md`, current `web/scada-web/src/python-runtime/**`, current Python editor/Script workspace files and Wave 06 E2E tests.

---

# DEV 1 - EliteSCADA

**Role:** `WORKER`  
**Wave:** `PYTHON-WAVE-06`  
**Status:** `WAIT_FOR_COORDINATOR — DELIVERY_ACCEPTED`  
**CurrentTask:** none executable.  
**Delivery:** PR #85 `Add Monaco Python editor UX`, already integrated.

**NextQueuedTask:** Wave 07 Visual Property Registry / Engineering projection — `QUEUED`, not executable.  
**NextStartCondition:** Wave 06 merged and explicit Wave 07 promotion. Once promoted under constrained budget, implement without GitHub Actions and return `IMPLEMENTED / CI_DEFERRED` until owner reports reset.

---

# DEV 2 - EliteSCADA

**Role:** `WORKER`  
**Wave:** `PYTHON-WAVE-06`  
**Status:** `WAIT_FOR_COORDINATOR — DELIVERY_ACCEPTED`  
**CurrentTask:** none executable.  
**Delivery:** PR #86 `Implement Client Visual Python worker runtime`, already integrated.

**NextQueuedTask:** Wave 07 Visual Runtime Instance — `QUEUED`, not executable.  
**NextStartCondition:** Wave 06 merged and explicit Wave 07 promotion. Once promoted under constrained budget, implement without GitHub Actions and return `IMPLEMENTED / CI_DEFERRED` until owner reports reset.

---

# DEV 3 - EliteSCADA

**Role:** `WORKER`  
**Wave:** `PYTHON-WAVE-06`  
**Status:** `WAIT_FOR_COORDINATOR — DYNAMIC TESTS INTEGRATED`  
**CurrentTask:** none executable. Dynamic/adversarial Wave 06 tests are integrated and green in CI #485/#486.

**NextQueuedTask:** Wave 07 Python <-> Visual API acceptance — `QUEUED`, not executable.  
**NextStartCondition:** Wave 06 merged and explicit Wave 07 promotion. Once promoted under constrained budget, implement/write tests without GitHub Actions and return `IMPLEMENTED / CI_DEFERRED` until owner reports reset.

---

## Coordinator note

All three worker chats remain stopped. Wave 07 is prepared in queue but is not authorized until Wave 06 is fully green and merged. The temporary no-Actions rule applies to Wave 07 only after that promotion.
