# CHAT WORK ASSIGNMENTS — EliteSCADA

> Authoritative live execution board. GitHub branch/PR/head/CI state is operational truth. Permanent rules: `docs/DEVELOPMENT-WAVES.md`, `docs/PARALLEL-WORK.md`, `docs/CI-USAGE-POLICY.md`.

**Last coordinator synchronization:** 2026-08-28

## Mandatory `siga`

Every fixed EliteSCADA chat first rereads current `main`: `PROJECT GOAL.md`, `LAST CHANGE.md`, `docs/ROADMAP.md`, `docs/PARALLEL-WORK.md`, `docs/DEVELOPMENT-WAVES.md`, this board, `docs/CI-USAGE-POLICY.md`, `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`, `docs/PYTHON-WAVE-06-IMPLEMENTATION-DECISION.md` when Wave 06 is relevant, and current `MustReadSpecific`. Then verify real branch/PR/head/CI and execute only the current authorized assignment.

Workers never modify `main`, merge their own PR, choose a new mission or broaden scope. `NextQueuedTask` is planning only.

## Current product gate

`PYTHON-WAVE-06` is **ACTIVE — DYNAMIC SANDBOX VALIDATION / COORDINATOR INTEGRATION**.

- Logical WaveBaseSHA: `7f629bf660bb16fd46fbf6abeb72b9ca1676e087`
- Central ContractSHA: `01d5b3092cf9c33ffa41c12b79133157b24cd148`
- Integration branch: `integration/python-wave-06`
- Integration PR: #83 Draft
- Contract CI #468: fully green
- DEV 2 exact-head CI #469: fully green
- Current integration checkpoint: `c63911fddd7e6726b067b507507c3e74baf0c0f9` plus subsequent coordinator commits on the same integration branch
- CI mode: `CONSTRAINED` until 2026-09-01

Merged only to integration, not `main`:
- PR #85 DEV 1 Monaco Python Editor UX;
- PR #86 DEV 2 Pyodide/Web Worker runtime adapter;
- PR #84 DEV 3 foundation/static sandbox safety coverage.

Coordinator has additionally begun central integration for same-origin Pyodide assets and authorized TAG-read / owning-client Client Memory capability providers. Wave 06 is **not MERGED** and final product acceptance is not yet complete.

---

# COORDENADOR - EliteSCADA

**Role:** `COORDINATOR`  
**Wave:** `PYTHON-WAVE-06`  
**Status:** `ACTIVE — CENTRAL_INTEGRATION`  
**CurrentTask:** finish central Client Visual Python composition, integrate DEV 3 dynamic acceptance, run one final exact-head Wave 06 matrix, then merge only if fully green.  
**IntegrationBranch:** `integration/python-wave-06`  
**LogicalBaseSHA:** `7f629bf660bb16fd46fbf6abeb72b9ca1676e087`  
**ContractSHA:** `01d5b3092cf9c33ffa41c12b79133157b24cd148`

**Coordinator-owned remaining work:**
- ensure pinned Pyodide 314.0.6 assets are self-hosted at `/pyodide/` in dev/build/acceptance;
- bind `tag.read` to the existing authenticated Runtime TAG read surface;
- bind `clientMemory.read/write` only to the owning Runtime Client store;
- connect engine compile diagnostics to the Monaco exact-source diagnostic snapshot contract;
- provide a controlled integrated Client Visual Script execution surface without bypassing lifecycle/authorization;
- integrate DEV 3 dynamic adversarial tests;
- final exact-head Web + backend Release/full tests + Runtime smoke + Chromium + Wave 06 sandbox acceptance.

**ReservedFiles:** coordination docs, `.github/workflows/**`, `web/scada-web/package.json`, `web/scada-web/vite.config.ts`, `web/scada-web/src/python-runtime/pythonRuntimeContracts.ts`, `web/scada-web/src/engineering/EngineeringApp.tsx`, `web/scada-web/src/main.tsx`, canonical Engineering schema/contracts, backend central composition.

**ForbiddenScope:** Server Python; Wave 07+ visual model/editor; new protocols; weakening security/CAS/lifecycle; direct Python driver/database/filesystem/shell/arbitrary-network/credential authority.

**AfterCompletion:** synchronize docs and prepare Wave 07 Definition of Ready.

**MustReadSpecific:** `docs/PYTHON-WAVE-06-IMPLEMENTATION-DECISION.md`, `docs/PYTHON-SCRIPTING-AND-VISUAL-RUNTIME.md`, `docs/research/python-client/CLIENT-PYTHON-EDITOR-SANDBOX-RESEARCH.md`, `src/Scada.Engineering/Scripts/**`, `src/Scada.Engineering/VisualScripting/**`, current Wave 06 frontend/runtime files.

---

# DEV 1 - EliteSCADA

**Role:** `WORKER`  
**Wave:** `PYTHON-WAVE-06`  
**Status:** `WAIT_FOR_COORDINATOR — DELIVERY_ACCEPTED`  
**CurrentTask:** none executable.  
**Delivery:** PR #85 `Add Monaco Python editor UX`, head `35f46fd8b74aa710c924c02374900540f24e73ad`, merged to integration as `ba688bf5990361f83cfc1a07204e035d34698abd`.

**NextQueuedTask:** Wave 07 Visual Property Registry / Engineering projection — `QUEUED`, not executable.  
**NextStartCondition:** Wave 06 merged and explicit Wave 07 promotion.  
**AfterCompletion:** remain `WAIT_FOR_COORDINATOR`.

---

# DEV 2 - EliteSCADA

**Role:** `WORKER`  
**Wave:** `PYTHON-WAVE-06`  
**Status:** `WAIT_FOR_COORDINATOR — DELIVERY_ACCEPTED`  
**CurrentTask:** none executable.  
**Delivery:** PR #86 `Implement Client Visual Python worker runtime`, head `d8db6e15829d16ee06eb471a7d2afb3f7f869f3c`, exact-head CI #469 fully green, merged to integration as `6e5724489d899e103d92c92171f6b2143a165b9d`.

**NextQueuedTask:** Wave 07 Visual Runtime Instance — `QUEUED`, not executable.  
**NextStartCondition:** Wave 06 merged and explicit Wave 07 promotion.  
**AfterCompletion:** remain `WAIT_FOR_COORDINATOR`.

---

# DEV 3 - EliteSCADA

**Role:** `WORKER`  
**Wave:** `PYTHON-WAVE-06`  
**Status:** `ACTIVE — DYNAMIC_ACCEPTANCE_CONTINUATION`  
**CurrentTask:** complete adversarial sandbox execution acceptance against the current integrated Wave 06 runtime.  
**Branch:** `test/python-wave-06-sandbox-safety`  
**Branch checkpoint after coordinator fast-forward:** `c63911fddd7e6726b067b507507c3e74baf0c0f9`  
**StartCondition:** `TRUE`

**Context:** first DEV 3 slice PR #84 was accepted and merged to integration. Coordinator then fast-forwarded this branch to the integrated checkpoint containing PR #85, PR #86, PR #84 and central Wave 06 composition available at that checkpoint. Continue on the same branch; open a new Draft PR against `integration/python-wave-06` for the dynamic continuation.

**AllowedScope:** isolated new/updated tests and test-only helpers under `web/scada-web/tests-e2e/*python*`. Production defects must be reproduced/classified and returned to coordinator, not silently repaired.

**Required dynamic acceptance:**
- real Pyodide compile success and syntax line/column diagnostics;
- same-origin Pyodide bootstrap path and cross-origin isolation;
- infinite-loop timeout -> interrupt -> bounded hard kill;
- explicit cancellation/disposal;
- stale Worker response rejected after replacement runtime;
- queue flood remains bounded/coalesced;
- five consecutive failures throttle only the owning Script Runtime Instance;
- arbitrary network/DOM/storage/filesystem/shell/database/driver/credential access fails closed;
- Server Memory/direct shared TAG write is unavailable;
- permitted authenticated TAG read works through the host provider;
- owning-client Client Memory read/write works and remains client-local;
- runtime faults are sanitized and understandable;
- one faulty Script does not destabilize another instance, UI or backend health.

**ReservedFiles:** all production source, shared dependencies/configuration, bridge contract, coordinator docs.  
**ForbiddenScope:** fixing production sandbox/editor code, Server Python, graphical editor, protocol work, weakening assertions.  
**IntegrationRequired:** `YES`; coordinator reviews findings and integrates non-duplicative final acceptance.  
**ValidationMatrix:** focused Playwright/dynamic sandbox tests first; under `CONSTRAINED` mode do not trigger redundant full matrices.  
**CompletionCriteria:** dynamic safety claims are objectively proven or falsified; defects have reproducible classification; Draft PR separates implemented tests/findings/deferred work; then `WAIT_FOR_COORDINATOR`.

**NextQueuedTask:** Wave 07 Python <-> Visual API acceptance — `QUEUED`, not executable.  
**NextStartCondition:** Wave 06 merged and explicit Wave 07 promotion.  
**AfterCompletion:** `WAIT_FOR_COORDINATOR`.

**MustReadSpecific:** `docs/PYTHON-WAVE-06-IMPLEMENTATION-DECISION.md`, `docs/PYTHON-SCRIPTING-AND-VISUAL-RUNTIME.md`, python-client research, current `web/scada-web/src/python-runtime/**`, current Python editor files, current Client Memory/TAG read APIs.

---

## Coordinator note

Current execution boundary: **DEV 1 and DEV 2 are stopped. DEV 3 is the only ACTIVE worker.** Coordinator continues central integration in PR #83. No Wave 07 implementation starts before Wave 06 final integrated gate and merge.