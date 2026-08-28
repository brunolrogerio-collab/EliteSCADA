# CHAT WORK ASSIGNMENTS — EliteSCADA

> Authoritative live execution board. GitHub branch/PR/head/CI state is operational truth. Permanent rules: `docs/DEVELOPMENT-WAVES.md`, `docs/PARALLEL-WORK.md` and `docs/CI-USAGE-POLICY.md`.

**Last coordinator synchronization:** 2026-08-28

## Mandatory `siga`

Every fixed EliteSCADA chat first rereads current `main`: `PROJECT GOAL.md`, `LAST CHANGE.md`, `docs/ROADMAP.md`, `docs/PARALLEL-WORK.md`, `docs/DEVELOPMENT-WAVES.md`, this board, `docs/CI-USAGE-POLICY.md`, `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`, `docs/PYTHON-WAVE-06-IMPLEMENTATION-DECISION.md` when Wave 06 is relevant, and current `MustReadSpecific`. Then verify real branch/PR/head/CI and execute only the current authorized assignment.

`NextQueuedTask` is planning only. Workers never modify `main`, merge their own PR, work another DEV branch or broaden their mission.

## Current product gate

`PYTHON-WAVE-06` is **ACTIVE — PARALLEL WORKER PHASE**.

**Logical WaveBaseSHA:** `7f629bf660bb16fd46fbf6abeb72b9ca1676e087`  
**Central ContractSHA:** `01d5b3092cf9c33ffa41c12b79133157b24cd148`  
**IntegrationBranch:** `integration/python-wave-06`  
**Integration PR:** #83 `Establish Wave 06 Client Visual Python foundation` — Draft integration train  
**Contract validation:** CI #468 / run `33140329634` fully green: Web, backend Release/full tests including PostgreSQL, Runtime smoke and Chromium.  
**Implementation decision:** `docs/PYTHON-WAVE-06-IMPLEMENTATION-DECISION.md`.

Wave 05 is COMPLETE/MERGED through PR #79; main merge `7f629bf660bb16fd46fbf6abeb72b9ca1676e087`.

Wave 06 objective: deliver a practical Monaco-backed Python editor and a Pyodide/Web Worker Client Visual sandbox that executes canonical Scripts through a narrow versioned bridge, with bounded cancellation/failure isolation and no direct infrastructure authority.

CI budget mode remains **CONSTRAINED** until reset on 2026-09-01. This changes validation frequency, not final quality.

---

# COORDENADOR - EliteSCADA

**Role:** `COORDINATOR`  
**Wave:** `PYTHON-WAVE-06`  
**Status:** `ACTIVE — INTEGRATION_COORDINATION`  
**CurrentTask:** review and integrate Wave 06 editor, runtime and safety slices; own all central hooks and final Wave gate  
**Branch:** `integration/python-wave-06` plus `main` for coordination docs  
**LogicalBaseSHA:** `7f629bf660bb16fd46fbf6abeb72b9ca1676e087`  
**ContractSHA:** `01d5b3092cf9c33ffa41c12b79133157b24cd148`  
**StartCondition:** `TRUE`

**Central foundation IMPLEMENTED IN PR #83:** pinned `monaco-editor` 0.56.0 and `pyodide` 314.0.6; bridge v1 contracts; 250 ms timeout/50 ms hard-stop grace/128 queue/50 ms timer/5-failure throttle; explicit capability/denied-boundary model; versioned Worker messages; stale-runtime identity check; Vite COOP/COEP; exact-head CI #468 fully green.

**DependsOn:** Wave 05 merged; canonical Script Engineering v10; merged VisualScripting safety contracts; `docs/PYTHON-WAVE-06-IMPLEMENTATION-DECISION.md`.

**ReservedFiles:** `PROJECT GOAL.md`, `LAST CHANGE.md`, coordination docs, `.github/workflows/**`, `web/scada-web/package.json`, `web/scada-web/vite.config.ts`, `web/scada-web/src/python-runtime/pythonRuntimeContracts.ts`, `web/scada-web/src/engineering/EngineeringApp.tsx`, `web/scada-web/src/main.tsx`, canonical Engineering schema/contracts, `src/Scada.Api/Program.cs`, central DI/routing/composition and cross-domain Runtime hooks.

**ForbiddenScope:** Server Python; graphical editor/Wave 07+ visual identity changes; new protocols; weakening security/CAS/lifecycle; direct Python authority over drivers/database/filesystem/shell/arbitrary network/credentials.

**ValidationMatrix:** event-based worker reviews; focused worker evidence under constrained CI; coordinator integrates accepted slices into PR #83; final exact-head Web + backend Release/full tests + Runtime smoke + Chromium plus Wave 06 sandbox acceptance before Ready/merge.

**CompletionCriteria:** all accepted worker slices integrated; central editor/runtime hooks reconciled; a canonical ClientVisual Script can compile/execute, read a permitted TAG, read/write Client Memory, receive a controlled event, time out/fail without destabilizing unrelated product state, and expose understandable diagnostics; PR #83 exact-head final matrix green; merge to main; docs synchronized; Wave 07 readiness decision made.

**AfterCompletion:** prepare Wave 07 Visual Runtime Object Model architecture-first Definition of Ready.

**MustReadSpecific:** `docs/PYTHON-WAVE-06-IMPLEMENTATION-DECISION.md`, `docs/PYTHON-SCRIPTING-AND-VISUAL-RUNTIME.md`, `docs/research/python-client/CLIENT-PYTHON-EDITOR-SANDBOX-RESEARCH.md`, `src/Scada.Engineering/Scripts/**`, `src/Scada.Engineering/VisualScripting/**`, Wave 05 Script frontend/API files.

---

# DEV 1 - EliteSCADA

**Role:** `WORKER`  
**Wave:** `PYTHON-WAVE-06`  
**Status:** `ACTIVE`  
**CurrentTask:** `Python Editor UX`  
**Branch:** `feature/python-wave-06-editor`  
**LogicalWaveBaseSHA:** `7f629bf660bb16fd46fbf6abeb72b9ca1676e087`  
**BaseSHA / ContractSHA:** `01d5b3092cf9c33ffa41c12b79133157b24cd148`  
**StartCondition:** `TRUE`

**Objective:** replace the Wave 05 plain source textarea experience with a practical Monaco-backed Python editing surface over the same canonical Script Engineering authority.

**AllowedScope:** `web/scada-web/src/engineering/scripts/**`; new isolated editor files under `web/scada-web/src/engineering/python-editor/**`; focused E2E/component-style browser tests named for Python editor under `web/scada-web/tests-e2e/**`. Use the already-pinned Monaco dependency. Preserve canonical Script load/create/update Preview/Apply/CAS APIs and existing locale patterns.

Required behavior:

- Python syntax highlighting and line numbers;
- normal indentation/navigation and multiline source editing;
- stable Script source remains canonical Workspace state, not a second browser authority;
- diagnostics model supports 1-based line/column markers and can consume engine/preflight diagnostics without inventing a competing validator;
- scope/entry-point context remains visible;
- autocomplete/help for stable EliteSCADA API names where practical, preferably from a small descriptor layer rather than hand-coded editor-only authority;
- editor handles unavailable/invalid diagnostics without silently applying changes;
- `pt-BR`/`en`/`es` editor chrome where applicable.

**ReservedFiles:** `package.json`, `vite.config.ts`, `pythonRuntimeContracts.ts`, `EngineeringApp.tsx`, `main.tsx`, backend/Program.cs, canonical schema/contracts, coordination docs.

**ForbiddenScope:** Pyodide Worker/runtime host; sandbox policy implementation; Server Python; direct backend/driver/database/filesystem/network access; changing canonical Script schema; graphical editor.

**DependsOn:** ContractSHA `01d5b3092cf9c33ffa41c12b79133157b24cd148`.

**ParallelSafeWith:** DEV 2 and DEV 3 provided ReservedFiles are respected.

**IntegrationRequired:** `YES`. Coordinator owns final Engineering composition and any connection from editor diagnostics to the runtime/compile host.

**IntegrationTarget:** `integration/python-wave-06` / PR #83. Worker Draft PR should target the integration branch where practical.

**ValidationMatrix:** Web build/focused editor tests; source edit round-trip without bypassing Preview/Apply/CAS; marker line/column behavior; editor preserves existing Script metadata/entry points/dependencies; no execution authority. Under constrained mode do not rerun unchanged heads for reassurance.

**CompletionCriteria:** practical Monaco source editor works on canonical Scripts, diagnostics markers and editor UX are tested, no competing persistence model exists, Draft PR body separates `IMPLEMENTED IN PR`, `INTEGRATION REQUIRED`, `SPECIFIED / NOT IMPLEMENTED`, then worker returns to `WAIT_FOR_COORDINATOR`.

**NextQueuedTask:** `Wave 07 Visual Property Registry / Engineering projection` — `QUEUED`, not executable.

**NextStartCondition:** Wave 06 merged and Wave 07 board promotion.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`.

**MustReadSpecific:** `docs/PYTHON-WAVE-06-IMPLEMENTATION-DECISION.md`, `docs/PYTHON-SCRIPTING-AND-VISUAL-RUNTIME.md`, Wave 05 `web/scada-web/src/engineering/scripts/**`, `web/scada-web/src/python-runtime/pythonRuntimeContracts.ts` read-only.

---

# DEV 2 - EliteSCADA

**Role:** `WORKER`  
**Wave:** `PYTHON-WAVE-06`  
**Status:** `ACTIVE`  
**CurrentTask:** `Client Visual Python Worker / runtime adapter`  
**Branch:** `feature/python-wave-06-client-runtime`  
**LogicalWaveBaseSHA:** `7f629bf660bb16fd46fbf6abeb72b9ca1676e087`  
**BaseSHA / ContractSHA:** `01d5b3092cf9c33ffa41c12b79133157b24cd148`  
**StartCondition:** `TRUE`

**Objective:** implement the Pyodide/Web Worker execution adapter behind bridge v1 so a canonical ClientVisual Script can compile and execute under the existing bounded policy without receiving direct browser/infrastructure authority.

**AllowedScope:** new isolated production files under `web/scada-web/src/python-runtime/**` except coordinator-reserved `pythonRuntimeContracts.ts`; focused tests under an isolated `web/scada-web/src/python-runtime/**` test/helper area or `web/scada-web/tests-e2e/*python-runtime*` where useful. Use pinned Pyodide. Build host/Worker adapters with injected trusted capability providers so central TAG/Client Memory/backend composition remains coordinator-owned.

Required behavior:

- dedicated module Worker per Script Runtime Instance for first implementation;
- restricted Pyodide globals; no normal `globalThis` exposure;
- no user `micropip`, dynamic package installation or import-driven remote package loading;
- compile-only request with line/column diagnostics;
- initialize/dispatch/cancel/dispose using bridge v1 identity/request/execution envelopes;
- parent-owned 250 ms deadline, soft Pyodide interrupt, bounded 50 ms hard-stop grace, then `Worker.terminate()` and discarded interpreter state;
- one active handler at a time per Script Runtime Instance;
- bounded/coalesced event admission consistent with central contracts;
- stale Worker responses rejected by runtime identity;
- trusted capability dispatch limited to bridge-v1 capability families; unsupported/denied capability fails closed;
- interfaces for permitted TAG read and owning-client Client Memory read/write; no direct process write or Server Memory write;
- sanitized execution diagnostics;
- deterministic disposal/recreation.

**ReservedFiles:** `pythonRuntimeContracts.ts`, `package.json`, `vite.config.ts`, `EngineeringApp.tsx`, `main.tsx`, Program.cs/backend central runtime composition, canonical schema/contracts, coordination docs.

**ForbiddenScope:** direct DOM/storage/network/driver/database/filesystem/shell/credential access from Python; Server Python; graphical editor; changing public Script schema; silently weakening time/queue/failure policy because of engine behavior.

**DependsOn:** ContractSHA `01d5b3092cf9c33ffa41c12b79133157b24cd148` and `docs/PYTHON-WAVE-06-IMPLEMENTATION-DECISION.md`.

**ParallelSafeWith:** DEV 1 and DEV 3.

**IntegrationRequired:** `YES`. Coordinator owns binding capability-provider interfaces to actual authorized TAG/Client Memory/backend surfaces and final Runtime/Engineering composition.

**IntegrationTarget:** `integration/python-wave-06` / PR #83. Worker Draft PR should target the integration branch where practical.

**ValidationMatrix:** focused Web/TypeScript tests where possible plus browser acceptance for compile, normal execution, cancellation/hard kill, stale response rejection and denied capability behavior. A full worker matrix is not required on every intermediate head under constrained mode, but known-failing work is not deliverable.

**CompletionCriteria:** real Pyodide Worker adapter executes a bounded ClientVisual handler through bridge v1, honors cancellation/disposal/fail-closed boundaries, exposes integration interfaces rather than direct infrastructure, focused tests/evidence are green, PR body clearly separates implementation/integration/deferred work, then `WAIT_FOR_COORDINATOR`.

**NextQueuedTask:** `Wave 07 Visual Runtime Instance` — `QUEUED`, not executable.

**NextStartCondition:** Wave 06 merged and Wave 07 board promotion.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`.

**MustReadSpecific:** `docs/PYTHON-WAVE-06-IMPLEMENTATION-DECISION.md`, `docs/research/python-client/CLIENT-PYTHON-EDITOR-SANDBOX-RESEARCH.md`, `src/Scada.Engineering/VisualScripting/**`, `web/scada-web/src/python-runtime/pythonRuntimeContracts.ts` read-only, current TAG/Client Memory frontend/runtime API patterns.

---

# DEV 3 - EliteSCADA

**Role:** `WORKER`  
**Wave:** `PYTHON-WAVE-06`  
**Status:** `ACTIVE`  
**CurrentTask:** `Sandbox execution safety / acceptance`  
**Branch:** `test/python-wave-06-sandbox-safety`  
**LogicalWaveBaseSHA:** `7f629bf660bb16fd46fbf6abeb72b9ca1676e087`  
**BaseSHA / ContractSHA:** `01d5b3092cf9c33ffa41c12b79133157b24cd148`  
**StartCondition:** `TRUE`

**Objective:** independently attack the Wave 06 security/reliability claims and provide acceptance coverage, without duplicating the production runtime implementation.

**AllowedScope:** isolated new tests/helpers under `web/scada-web/tests-e2e/*python*`, and isolated test-only fixtures/helpers when necessary. Production-source defects are reported/classified; do not silently repair another worker's domain. Static contract assertions may inspect bridge constants and public browser behavior.

Required adversarial/acceptance coverage as implementation becomes available:

- cross-origin isolation is actually active where required;
- ordinary compile success and syntax line/column diagnostics;
- infinite loop/over-budget handler does not remain detached and does not freeze unrelated UI/backend;
- cancellation/disposal terminates or invalidates execution deterministically;
- stale Worker response cannot mutate replacement runtime instance;
- queue/event flood remains bounded/coalesced;
- repeated failures reach throttle behavior without global product failure;
- arbitrary network/DOM/storage/filesystem/shell/database/driver/credential paths are unavailable through the public bridge;
- Server Memory/direct shared TAG write capability is not exposed to ClientVisual;
- permitted TAG read and Client Memory read/write acceptance once coordinator integration exists;
- faults expose sanitized, understandable diagnostics;
- one Script failure does not break another runtime instance or backend health.

**ReservedFiles:** all production source, `package.json`, `vite.config.ts`, central bridge contract, existing central shell tests unless a narrow addition is explicitly necessary, coordination docs.

**ForbiddenScope:** production sandbox/editor implementation; weakening assertions; Server Python; graphical editor; protocol work; broad unrelated regression cleanup.

**DependsOn:** ContractSHA `01d5b3092cf9c33ffa41c12b79133157b24cd148`. Some dynamic tests may initially remain pending on DEV 2/coordinator integration; test design itself is ACTIVE.

**ParallelSafeWith:** DEV 1 and DEV 2.

**IntegrationRequired:** `YES`. Coordinator selects/integrates non-duplicative safety coverage and resolves any cross-domain defect owners.

**IntegrationTarget:** `integration/python-wave-06` / PR #83. Worker Draft PR should target the integration branch where practical.

**ValidationMatrix:** focused Playwright/contract tests and existing product health checks where necessary. Findings classified `BLOCKER`, `MAJOR UX`, `MINOR UX` or `TEST GAP`; no scope expansion disguised as a test fix.

**CompletionCriteria:** adversarial acceptance meaningfully proves or falsifies sandbox claims, any defects have reproducible evidence/classification, tests stay isolated and deterministic, PR body separates implemented tests/findings/deferred integration, then `WAIT_FOR_COORDINATOR`.

**NextQueuedTask:** `Wave 07 Python <-> Visual API acceptance` — `QUEUED`, not executable.

**NextStartCondition:** Wave 06 merged and Wave 07 board promotion.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`.

**MustReadSpecific:** `docs/PYTHON-WAVE-06-IMPLEMENTATION-DECISION.md`, `docs/PYTHON-SCRIPTING-AND-VISUAL-RUNTIME.md`, python-client research, `src/Scada.Engineering/VisualScripting/PythonScriptingContracts.cs`, `ScriptRuntimeExecutionCoordinator.cs`, bridge v1 contract read-only.

---

## Coordinator note for future chats

Current action boundary: **DEV 1, DEV 2 and DEV 3 are ACTIVE on the exact Wave 06 assignments above.** All branches start at ContractSHA `01d5b3092cf9c33ffa41c12b79133157b24cd148`, which passed CI #468. Workers must not re-add/change shared dependencies or bridge policy. They open Draft PRs against `integration/python-wave-06` where practical, remain inside AllowedScope/ReservedFiles, optimize CI frequency under constrained mode, and return to `WAIT_FOR_COORDINATOR` after delivery. Coordinator alone integrates central hooks and merges PR #83 to `main` after the final exact-head Wave 06 matrix is green.
