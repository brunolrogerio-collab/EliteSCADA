# CHAT WORK ASSIGNMENTS — EliteSCADA

> Authoritative live execution board. GitHub branch/PR/head/CI state is operational truth. Permanent rules: `docs/DEVELOPMENT-WAVES.md`, `docs/PARALLEL-WORK.md`, `docs/CI-USAGE-POLICY.md`.

**Last coordinator synchronization:** 2026-08-28 — Wave 06 final correction candidate prepared under ~50-minute Actions constraint

## Mandatory `siga`

Every fixed EliteSCADA chat first rereads current `main`: `PROJECT GOAL.md`, `LAST CHANGE.md`, `docs/ROADMAP.md`, `docs/PARALLEL-WORK.md`, `docs/DEVELOPMENT-WAVES.md`, this board, `docs/CI-USAGE-POLICY.md`, `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`, `docs/PYTHON-WAVE-06-IMPLEMENTATION-DECISION.md` when Wave 06 is relevant, and current `MustReadSpecific`. Then verify real branch/PR/head/CI and execute only the current authorized assignment.

Workers never modify `main`, merge their own PR, choose a new mission or broaden scope. `NextQueuedTask` is planning only.

## Current product gate

`PYTHON-WAVE-06` is **ACTIVE — FINAL INTEGRATION DEFECT CORRECTION**.

- Logical WaveBaseSHA: `7f629bf660bb16fd46fbf6abeb72b9ca1676e087`
- Central ContractSHA: `01d5b3092cf9c33ffa41c12b79133157b24cd148`
- Integration branch: `integration/python-wave-06`
- Integration PR: #83 Draft
- Current integration head before candidate promotion: `79546d9a8fb39786eec7b0bd34f87723c9261a8d`
- Coordinator correction branch: `fix/python-wave-06-compile-guard`
- Current correction candidate: `f98dbb0827e86317c6195ac10c6b3c0cf4d3ddfe`
- Candidate diff versus integration: 2 commits / 2 files; Pyodide guard isolation + targeted locale-readiness reliability correction
- Contract CI #468: fully green
- DEV 2 exact-head CI #469: fully green
- Latest integrated CI #483 / run `33183960875`: Web SUCCESS, backend/full tests/Runtime smoke SUCCESS, Chromium FAILURE, 118 passed / 5 failed
- CI mode: `CONSTRAINED` until 2026-09-01
- Remaining included Actions allowance reported by product owner: approximately 50 minutes

Wave 06 remains **IMPLEMENTED IN PR / NOT MERGED TO MAIN** until the correction is promoted, exact-final-head CI is fully green and PR #83 is merged.

### Current correction candidate

The CI #483 Python failures share one root boundary: the previous sandbox hardening permanently altered Pyodide engine-visible modules before later engine `runPython()` compile diagnostics. Candidate commit `092221f069064ff7c8e543259959f19e376197e6` keeps the Pyodide engine intact outside user execution and applies the denied-import/native-escape guard only around user top-level execution and handlers.

The separate locale-readiness failure is treated as test-composition reliability, not as a generic timeout problem. Candidate commit `f98dbb0827e86317c6195ac10c6b3c0cf4d3ddfe` keeps dedicated navigation coverage elsewhere and makes the multilingual readiness loop validate deterministic localized route state directly rather than repeatedly coupling locale acceptance to SPA navigation timing.

No CI has been intentionally triggered on the no-PR correction branch. The next expensive step is promotion of a coherent candidate to the integration branch and one exact-head Wave 06 matrix.

### Temporary Wave 07 Actions rule

After Wave 06 is merged and Wave 07 is explicitly promoted, Wave 07 implementation may proceed while the Actions allowance is constrained, but **Wave 07 GitHub Actions runs are deferred until the product owner explicitly reports that the allowance has reset**.

During that interval:
- workers may implement assigned Wave 07 slices and preserve/write the required tests;
- review/static/focused evidence that does not consume GitHub Actions is allowed;
- worker delivery state must explicitly remain `IMPLEMENTED / CI_DEFERRED` or equivalent;
- no Wave 07 slice or wave may be labeled fully validated, complete or merge-ready solely because CI was deferred;
- once the owner reports the reset, deferred Wave 07 validation resumes before the Wave 07 final gate.

---

# COORDENADOR - EliteSCADA

**Role:** `COORDINATOR`  
**Wave:** `PYTHON-WAVE-06`  
**Status:** `ACTIVE — FINAL_DEFECT_CORRECTION / CANDIDATE_READY_FOR_INTEGRATION_GATE`  
**CurrentTask:** review the no-PR correction candidate, promote it to `integration/python-wave-06` only as a coherent checkpoint, inspect the resulting exact-head CI, fix any real remaining failure without unchanged-head reruns, and merge PR #83 only if fully green.  
**IntegrationBranch:** `integration/python-wave-06`  
**CurrentIntegrationHead:** `79546d9a8fb39786eec7b0bd34f87723c9261a8d` before candidate promotion  
**CorrectionBranch:** `fix/python-wave-06-compile-guard`  
**CorrectionCandidate:** `f98dbb0827e86317c6195ac10c6b3c0cf4d3ddfe`  
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

**Candidate correction evidence before CI:**
- code inspection shows `getCompileDiagnostics()` now runs while Pyodide internals are intact;
- denied `micropip`, `pyodide.http`, `pyodide_js` and `pyodide.code.run_js` are isolated during user execution rather than removed permanently from the engine;
- dynamic sandbox and Script Workspace both depend on the same engine compile path that failed in CI #483;
- multilingual readiness no longer duplicates the separately-covered Runtime -> Engineering -> Audit SPA navigation journey inside every locale iteration;
- no security/CAS/lifecycle/persistence gate is bypassed;
- no generic timeout was increased.

**Execution rule:** approximately 50 included Actions minutes remain. Do not rerun unchanged failing heads. Reserve Actions for Wave 06 final integration evidence. Prefer code inspection/no-PR correction work between expensive runs.

**ReservedFiles:** coordination docs, `.github/workflows/**`, `web/scada-web/package.json`, `web/scada-web/vite.config.ts`, Python bridge/runtime central files, `web/scada-web/src/engineering/EngineeringApp.tsx`, central Script/Python composition, `main.tsx`, canonical Engineering schema/contracts and backend central composition.

**ForbiddenScope:** Server Python; Wave 07+ visual implementation before Wave 06 merge; new protocols; weakening security/CAS/lifecycle/persistence; bypassing engine compile diagnostics to force Preview green; direct Python driver/database/filesystem/shell/arbitrary-network/credential authority.

**CompletionCriteria:** root causes fixed; exact final integration head passes Web + backend Release/full tests incl. PostgreSQL + Runtime smoke + Chromium + Wave 06 sandbox acceptance; PR #83 Ready and merged; docs synchronized.

**AfterCompletion:** freeze Wave 07 architecture-first Definition of Ready, explicitly promote Wave 07 assignments, and apply the temporary `IMPLEMENTED / CI_DEFERRED` rule until the owner reports Actions reset.

**MustReadSpecific:** `docs/PYTHON-WAVE-06-IMPLEMENTATION-DECISION.md`, `docs/PYTHON-SCRIPTING-AND-VISUAL-RUNTIME.md`, `docs/research/python-client/CLIENT-PYTHON-EDITOR-SANDBOX-RESEARCH.md`, current `web/scada-web/src/python-runtime/**`, current Python editor/Script workspace files and Wave 06 E2E tests.

---

# DEV 1 - EliteSCADA

**Role:** `WORKER`  
**Wave:** `PYTHON-WAVE-06`  
**Status:** `WAIT_FOR_COORDINATOR — DELIVERY_ACCEPTED`  
**CurrentTask:** none executable.  
**Delivery:** PR #85 `Add Monaco Python editor UX`, head `35f46fd8b74aa710c924c02374900540f24e73ad`, already integrated.

**NextQueuedTask:** Wave 07 Visual Property Registry / Engineering projection — `QUEUED`, not executable.  
**NextStartCondition:** Wave 06 merged and explicit Wave 07 promotion. Once promoted under the current budget constraint, implementation is allowed but GitHub Actions validation remains deferred until owner reports reset.

---

# DEV 2 - EliteSCADA

**Role:** `WORKER`  
**Wave:** `PYTHON-WAVE-06`  
**Status:** `WAIT_FOR_COORDINATOR — DELIVERY_ACCEPTED`  
**CurrentTask:** none executable.  
**Delivery:** PR #86 `Implement Client Visual Python worker runtime`, head `d8db6e15829d16ee06eb471a7d2afb3f7f869f3c`, exact-head CI #469 fully green, already integrated.

**NextQueuedTask:** Wave 07 Visual Runtime Instance — `QUEUED`, not executable.  
**NextStartCondition:** Wave 06 merged and explicit Wave 07 promotion. Once promoted under the current budget constraint, implementation is allowed but GitHub Actions validation remains deferred until owner reports reset.

---

# DEV 3 - EliteSCADA

**Role:** `WORKER`  
**Wave:** `PYTHON-WAVE-06`  
**Status:** `WAIT_FOR_COORDINATOR — DYNAMIC TESTS INTEGRATED / FINDINGS UNDER COORDINATOR CORRECTION`  
**CurrentTask:** none executable.

The original DEV 3 chat reached a practical conversation limitation. The coordinator temporarily executed its authorized dynamic sandbox acceptance continuation. Relevant dynamic/adversarial tests are already in `integration/python-wave-06`. Branch `test/python-wave-06-sandbox-safety` is behind the integration train and does not contain an additional delivery that needs merging.

The test findings are coordinator integration defects. Current no-PR correction candidate is recorded above. Do not create a new DEV 3 task or branch unless the coordinator explicitly reassigns one.

**NextQueuedTask:** Wave 07 Python <-> Visual API acceptance — `QUEUED`, not executable.  
**NextStartCondition:** Wave 06 merged and explicit Wave 07 promotion. Once promoted under the current budget constraint, implementation is allowed but GitHub Actions validation remains deferred until owner reports reset.

---

## Coordinator note

Current execution boundary: **all three worker chats are stopped**. The next executable work belongs to the **COORDENADOR** on Wave 06 final defect correction. No Wave 07 implementation starts before Wave 06 exact-head final gate and merge. After Wave 06 closes, Wave 07 may be promoted for development while its Actions-based validation remains explicitly deferred.