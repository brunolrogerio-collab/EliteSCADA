# CHAT WORK ASSIGNMENTS — EliteSCADA

> Authoritative live coordination board. GitHub branch/PR/head/CI state is operational truth; if this file briefly lags GitHub, GitHub wins and the coordinator reconciles it.

**Coordination protocol introduced:** 2026-08-26  
**Last coordinator synchronization:** 2026-08-26

## Permanent `siga` protocol

Before any action, every fixed EliteSCADA chat rereads current `main`: `PROJECT GOAL.md`, `LAST CHANGE.md`, `docs/ROADMAP.md`, `docs/PARALLEL-WORK.md`, this file, and every document listed in its current `MustReadSpecific`. Then it verifies the real assigned branch, PR/head and CI.

Workers never choose a new task, alter `main`, merge their own PR, work another DEV branch, or broaden their assignment. `WAIT_FOR_COORDINATOR` means stop after delivery.

Repository terminology:

- **MERGED** = official `main` state.
- **IMPLEMENTED IN PR** = functional implementation exists only in an open branch/PR.
- **RESEARCH IN PR** = research/specification exists only in an open branch/PR and is not product implementation.
- **SPECIFIED / NOT IMPLEMENTED** = documented product intent without merged implementation.

---

# COORDENADOR - EliteSCADA

**Role:** `COORDINATOR`

**CurrentTask:** Integrate TAG Gateway Engineering and complete the coordinator-owned protocol-independent Gateway runtime/product block

**Branch:** `main` + coordinator integration branch after PR #50 merge

**Status:** `IN_PROGRESS`

**Objective:**

Finish the complete TAG Gateway product block required by `docs/TAG-GATEWAY.md`. DEV 2 has delivered the canonical Engineering/validation foundation; the coordinator now owns integration and the remaining runtime engine, active-revision staging, provider-routed destination writes, rate/quality/conversion semantics, trusted runtime authority, diagnostics, central API/UI/DI hooks and Modbus↔Server Memory runtime proof. DEV 1 and DEV 3 continue only their isolated research assignments.

**AllowedScope:** coordinator-owned shared/central files, integration hooks, Gateway runtime/API/UI/DI, workflow maintenance, assignment board, handoff/roadmap documentation, worker assignment and merge decisions.

**ForbiddenScope:**

- no known-failing merge;
- no force-reset/discard of worker commits;
- no concrete driver-to-driver Gateway coupling;
- no production OPC UA or S7 Data Source/runtime before the external-protocol gate opens;
- no new external protocol family in Active Runtime before Gateway, common diagnostics and interface-preview gates;
- no production graphical Screen/Popup/Dynamo editor before the Script/property prerequisite chain is satisfied.

**MustReadSpecific:**

- `docs/TAG-GATEWAY.md`
- `docs/INTERNAL-MEMORY-TAGS.md`
- `docs/ADR-004-ENGINEERING-IMPORT-EXPORT.md`
- `docs/COMMUNICATION-DRIVER-DIAGNOSTICS.md`
- `docs/INTERFACE-VALIDATION-MILESTONE.md`
- `docs/PYTHON-SCRIPTING-AND-VISUAL-RUNTIME.md`

**ObservedGitHubState:**

- Internal Memory: **MERGED / COMPLETE** through PR #49.
- PR #50 TAG Gateway Engineering: DEV 2 delivery complete; schema v9/Engineering/validation slice passed CI #305 on worker head and is being coordinator-reconciled with current `main` for final merge.
- PR #51 OPC UA research: **MERGED**.
- PR #52 Siemens S7 research: **MERGED**.
- DEV 1 visual-editor architecture research: assigned, research-only.
- DEV 3 Client Python editor/sandbox research: assigned, research-only.
- Complete TAG Gateway runtime/product integration remains **NOT YET MERGED** and is the coordinator's active block.

**Dependencies:**

- integrate PR #50 first so schema v9 and Gateway Engineering are official;
- runtime must use common TAG Event Bus/current cache and owning provider/driver write boundary;
- `builtin.memory.server` is valid; `builtin.memory.client` is forbidden;
- Gateway failures remain route diagnostics and do not corrupt source TAG quality;
- common multi-driver diagnostics remains the next product gate only after complete Gateway product integration.

**NextActions:**

1. reconcile PR #50 with current `main`, validate full CI and merge with exact head SHA;
2. build coordinator Gateway runtime service with transactional active-route replacement;
3. route destination writes through common active runtime ownership, supporting communication drivers and Server Memory without protocol coupling;
4. implement Good-only gating, startup synchronization, OnChange deadband/minimum interval/coalescing, Periodic cadence and checked numeric conversion/gain/offset;
5. implement bounded per-route diagnostics/state/counters and trusted internal runtime authority semantics without human-style audit flood;
6. add protected diagnostic/configuration hooks and practical Engineering UI Gateway tool as appropriate to the existing UI architecture;
7. prove Modbus→Server Memory and Server Memory→Modbus plus quality suppression, recovery, fan-out, cadence/coalescing and active-revision switching;
8. run full CI, merge coordinator integration, run post-merge main CI and reconcile roadmap/handoff;
9. only after complete Gateway is green on `main`, unlock common multi-driver diagnostics as the next source/protocol gate.

**AfterCompletion:** `CONTINUE_COORDINATION`

---

# DEV 1 - EliteSCADA

**Role:** `WORKER`

**CurrentTask:** Graphical Screen/Popup/Dynamo editor architecture and Engineering UX research spike

**Branch:** `research/visual-editor-architecture`

**Status:** `ASSIGNED`

**PullRequest:** none yet

**Objective:** Research and specify the future graphical Engineering editor for Screens, Popups and Dynamos. Architecture/UX research only; no production editor.

**AllowedScope:** isolated documentation under `docs/research/visual-editor/**`; renderer/editor comparison; canvas/palette/selection/transforms/grid/guides/snap/z-order/groups; property inspector consuming public schema; TAG/expression bindings; scripts/events; Dynamo composition; resources; undo/redo; Engineering Fragments; performance/test strategy.

**ForbiddenScope:** `main`; production editor/renderer; dependencies/lockfiles; central routing/shell; `EngineeringContracts.cs`; Script canonical integration; Python engine/editor; visual runtime composition; Gateway/runtime/protocol work; workflows.

**MustReadSpecific:**
- `docs/PYTHON-SCRIPTING-AND-VISUAL-RUNTIME.md`
- `docs/ADR-004-ENGINEERING-IMPORT-EXPORT.md`
- `docs/INTERFACE-VALIDATION-MILESTONE.md`
- `docs/ROADMAP.md`

**CompletionCriteria:** credible renderer direction; full authoring interaction model; schema-driven property inspector; bindings/script association UX; Screen/Popup/Dynamo composition; resource handling; undo/redo/fragments; performance targets; future slices; no production code/dependencies.

**NextActions:** continue only `research/visual-editor-architecture`; Draft research PR; stop under `WAIT_FOR_COORDINATOR` when complete.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`

---

# DEV 2 - EliteSCADA

**Role:** `WORKER`

**CurrentTask:** Public TAG Gateway Engineering contract and deterministic validation foundation

**Branch:** `feature/tag-gateway-engineering`

**Status:** `DELIVERED / WAIT_FOR_COORDINATOR`

**PullRequest:** `#50 — coordinator integration in progress`

**Objective:** completed worker slice: canonical public/versioned Gateway Engineering contract, schema v9, validation and persistence/package round trips.

**CompletionCriteria:** **SATISFIED BY WORKER DELIVERY.** Runtime execution/API/UI/DI/diagnostics were intentionally outside this assignment and are now coordinator-owned integration work.

**NextActions:** no new task. Keep branch/PR unchanged unless coordinator explicitly requests a focused correction. Do not implement runtime Gateway and do not merge own PR.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`

---

# DEV 3 - EliteSCADA

**Role:** `WORKER`

**CurrentTask:** Client Python script editor, browser sandbox and execution-engine technology research spike

**Branch:** `research/client-python-editor-sandbox`

**Status:** `ASSIGNED`

**PullRequest:** none yet

**Objective:** Research/specify browser/WASM Client Visual Python editor/sandbox technology. Research only; no production engine/editor.

**AllowedScope:** isolated docs under `docs/research/python-client/**`; Pyodide/MicroPython/WASM comparisons; editor options; Worker isolation; budgets/cancellation/event queues; EliteSCADA API injection/security; validation/diagnostics/autocomplete; preview; CSP/offline packaging; benchmarks/tests.

**ForbiddenScope:** `main`; production dependencies/lockfiles; production Python runtime/editor; central routing/shell; `EngineeringContracts.cs`; canonical Script integration; server Python; direct DOM/filesystem/shell/arbitrary network/drivers/database; production visual editor/runtime; workflows.

**MustReadSpecific:**
- `docs/PYTHON-SCRIPTING-AND-VISUAL-RUNTIME.md`
- `docs/ADR-004-ENGINEERING-IMPORT-EXPORT.md`
- `docs/INTERNAL-MEMORY-TAGS.md`
- `docs/INTERFACE-VALIDATION-MILESTONE.md`
- `docs/ROADMAP.md`

**CompletionCriteria:** browser Python engine recommendation; editor direction; isolation/budgets/cancellation; public API security boundary; diagnostics flow; sandbox preview; offline strategy; benchmarks/tests; future slices; no production dependency/code.

**NextActions:** continue only `research/client-python-editor-sandbox`; Draft research PR; stop under `WAIT_FOR_COORDINATOR` when complete.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`
