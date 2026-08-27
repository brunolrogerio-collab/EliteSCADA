# CHAT WORK ASSIGNMENTS — EliteSCADA

> Authoritative live coordination board. GitHub branch/PR/head/CI state is operational truth; if this file briefly lags GitHub, GitHub wins and the coordinator reconciles it.

**Coordination protocol introduced:** 2026-08-26  
**Last coordinator synchronization:** 2026-08-27

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

**CurrentTask:** Complete common multi-driver/Data Source diagnostics product integration

**Branch:** `main` + coordinator integration branch as needed

**Status:** `IN_PROGRESS`

**Objective:**

Implement the common external communication diagnostics product block required by `docs/COMMUNICATION-DRIVER-DIAGNOSTICS.md` after complete TAG Gateway integration. Keep Data Source identity distinct from Driver type, instrument per-instance runtime facts, preserve independent failure isolation and expose protected backend + Engineering diagnostics without fabricating network semantics for Internal Memory or simulation.

**AllowedScope:** coordinator-owned shared/central files, DriverHost aggregation/composition, protected API/authorization, Engineering diagnostics UI, central tests, workflows if necessary, assignment board, roadmap/handoff documentation, worker assignment and merge decisions.

**ForbiddenScope:**

- no known-failing merge;
- no force-reset/discard of worker commits;
- no new production external protocol family before the interface-preview gate;
- no fake reconnect/timeout/network metrics for Internal Memory or built-in simulation;
- no direct frontend-to-device diagnostics path;
- no production graphical editor or Python engine/editor ahead of the locked Script/visual chain.

**MustReadSpecific:**

- `docs/COMMUNICATION-DRIVER-DIAGNOSTICS.md`
- `docs/TAG-GATEWAY.md`
- `docs/INTERNAL-MEMORY-TAGS.md`
- `docs/INTERFACE-VALIDATION-MILESTONE.md`
- `docs/PARALLEL-WORK.md`
- `docs/PYTHON-SCRIPTING-AND-VISUAL-RUNTIME.md`

**ObservedGitHubState:**

- Internal Memory: **MERGED / COMPLETE** through PR #49.
- TAG Gateway Engineering + runtime/product: **MERGED / COMPLETE** through PRs #50 and #55.
- Engineering Schema: **v9**.
- PR #55 merge SHA: `41bc437ba64f60fba26754794a9dc5a4e9a034f7`.
- Gateway post-merge stabilization: `cb4d2c423c31cf7a52ea6ebe6de494c281901f3f`; main CI #336 all green.
- PR #53 visual-editor architecture research: Draft/Open, delivered, waiting.
- PR #54 Client Python editor/sandbox research: Draft/Open, delivered, waiting.
- common multi-driver diagnostics is now the active source/protocol block.

**Dependencies:**

- common communication diagnostics must remain protocol-neutral at the public contract boundary;
- DEV 2 owns only the isolated Abstractions + Modbus instrumentation slice below;
- coordinator owns DriverHost/API/DI/UI and cross-instance acceptance;
- Internal Memory and built-in simulation remain outside transport/network diagnostics;
- USER INTERFACE VALIDATION PREVIEW remains blocked until this diagnostics product block is complete.

**NextActions:**

1. monitor DEV 2 current-head implementation/CI and review semantics rather than only compilation;
2. integrate protocol-neutral snapshots with active DriverHost/Data Source identity and lifecycle;
3. expose protected runtime diagnostics endpoint with authorization and no secrets;
4. build Engineering communication diagnostics table + drill-down with quiet healthy-state presentation;
5. aggregate current TAG quality per active Data Source without replacing point-level quality authority;
6. prove two active Modbus Data Sources with independent endpoints, failure/recovery, counters, quality and write ownership;
7. ensure internal memory/simulation never surface fabricated network-failure metrics;
8. run full CI, merge only green current heads and reconcile docs;
9. after complete diagnostics is green on `main`, start USER INTERFACE VALIDATION PREVIEW before any additional external protocol runtime.

**AfterCompletion:** `CONTINUE_COORDINATION`

---

# DEV 1 - EliteSCADA

**Role:** `WORKER`

**CurrentTask:** Graphical Screen/Popup/Dynamo editor architecture and Engineering UX research spike

**Branch:** `research/visual-editor-architecture`

**Status:** `DELIVERED / WAIT_FOR_COORDINATOR`

**PullRequest:** `#53 — Draft/Open`

**Objective:** completed research-only architecture/UX spike for the future graphical Engineering editor.

**Delivered:**

- canonical Engineering-authoritative editor direction;
- SVG/DOM-first authoring recommendation with renderer-independent interaction/geometry services;
- palette/project tree/canvas/inspector, selection/transforms/grid/snap/z-order/groups;
- schema-driven property inspector, bindings/scripts/events, Screen/Popup/Dynamo composition;
- resources, undo/redo, Engineering Fragments and performance/test strategy.

**ForbiddenScope:** no production editor/renderer, dependencies/lockfiles, central routing/shell, `EngineeringContracts.cs`, Script canonical integration, Python engine/editor, visual runtime composition, diagnostics/protocol work or workflows unless coordinator assigns a new mission.

**MustReadSpecific:**
- `docs/PYTHON-SCRIPTING-AND-VISUAL-RUNTIME.md`
- `docs/ADR-004-ENGINEERING-IMPORT-EXPORT.md`
- `docs/INTERFACE-VALIDATION-MILESTONE.md`
- `docs/ROADMAP.md`

**NextActions:** none. Keep PR #53 unchanged and wait for coordinator review/integration decision.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`

---

# DEV 2 - EliteSCADA

**Role:** `WORKER`

**CurrentTask:** Common communication-driver diagnostics contract and Modbus TCP instrumentation foundation

**Branch:** `feature/communication-driver-diagnostics`

**Status:** `ASSIGNED`

**PullRequest:** none yet

**Objective:**

Implement the isolated backend driver-layer foundation for `docs/COMMUNICATION-DRIVER-DIAGNOSTICS.md`: evolve a protocol-neutral diagnostic contract in `Scada.Drivers.Abstractions`, instrument the current Modbus TCP driver/transport with meaningful per-instance metrics, and prove independent diagnostics across simultaneous Modbus Data Sources. Do not build API/UI/DI integration.

**AllowedScope:**

- `src/Scada.Drivers/Abstractions/**`;
- `src/Scada.Drivers/Modbus/**`;
- focused `tests/Scada.Drivers.Tests/**` and existing test helpers only as required;
- additive protocol-neutral types/interfaces and Modbus-specific detail types inside the above directories.

**ForbiddenScope:**

- `main`;
- `src/Scada.Api/**` and `Program.cs`;
- central DriverHost/runtime composition;
- frontend/UI/routing;
- `EngineeringContracts.cs` or Engineering schema/version changes;
- Internal Memory implementation;
- production MQTT/OPC UA/BACnet/S7 or Driver Module work;
- workflows/lockfiles/dependencies;
- fake network metrics on `builtin.memory.*` or `builtin.simulation`;
- changing Simulation semantics merely to satisfy a new mandatory diagnostics interface. Prefer an optional communication-diagnostics capability/interface so non-network sources need not lie.

**MustReadSpecific:**

- `docs/COMMUNICATION-DRIVER-DIAGNOSTICS.md`
- `docs/INTERNAL-MEMORY-TAGS.md`
- `docs/TAG-GATEWAY.md`
- `docs/INTERFACE-VALIDATION-MILESTONE.md`

**RequiredContractDirection:**

- preserve `Driver type != Data Source` and per-instance identity;
- extend beyond current minimal `DriverStatus` without leaking Modbus concepts into common abstractions;
- common snapshot should support stable identity/context, operational state and state-change time, last success/failure, sanitized error, associated TAG count, monotonic counters and useful bounded latency/scan information where meaningful;
- support at least healthy running vs degraded/reconnecting vs faulted semantics, with staged enum evolution if needed;
- expose TAG-quality summary hooks/counts without replacing per-TAG quality;
- Modbus detail may include safe host/port, scan interval, request timeout, poll-block count, represented Unit IDs, failed blocks/cycles, reconnects/timeouts, last poll and latency;
- secrets must never be returned;
- counters are isolated per runtime driver instance and monotonic for that instance lifetime;
- timeout and ordinary communication failure should be distinguishable where transport evidence permits;
- one Data Source failure must not contaminate another instance's counters/state/TAG quality.

**CompletionCriteria:**

1. common protocol-neutral diagnostics contract exists in `Scada.Drivers.Abstractions` and remains usable by future protocols;
2. Modbus TCP exposes meaningful per-instance diagnostics matching the documented fields where technically available;
3. current runtime behavior/read/write/poll semantics are preserved;
4. tests cover two independent Modbus instances, failure isolation, recovery/reconnect, counters, state/timestamps and TAG-quality summary;
5. no Internal Memory/simulation fake transport metrics are introduced;
6. build/tests/CI are green on the exact worker head;
7. Draft PR opened with clear **IMPLEMENTED IN PR / NOT MERGED** status and exact limitations.

**NextActions:** create/use only `feature/communication-driver-diagnostics`; implement this slice; run CI; open Draft PR; stop after delivery.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`

---

# DEV 3 - EliteSCADA

**Role:** `WORKER`

**CurrentTask:** Client Python script editor, browser sandbox and execution-engine technology research spike

**Branch:** `research/client-python-editor-sandbox`

**Status:** `DELIVERED / WAIT_FOR_COORDINATOR`

**PullRequest:** `#54 — Draft/Open`

**Objective:** completed research-only browser/WASM Client Visual Python editor/sandbox technology spike.

**Delivered:**

- Pyodide first laboratory candidate, not production dependency selection;
- Monaco first desktop Engineering editor candidate;
- per-Script Runtime Instance Worker isolation baseline;
- restricted JS globals and narrow EliteSCADA RPC/API boundary;
- timeout soft interrupt + hard Worker termination fallback;
- Preview isolation, offline packaging, CSP/security and benchmark/test direction.

**ForbiddenScope:** no production Python runtime/editor/dependency, canonical Script integration, server Python, central routing, visual editor/runtime, diagnostics/protocol work, workflows or `main` unless coordinator assigns a new mission.

**MustReadSpecific:**
- `docs/PYTHON-SCRIPTING-AND-VISUAL-RUNTIME.md`
- `docs/ADR-004-ENGINEERING-IMPORT-EXPORT.md`
- `docs/INTERNAL-MEMORY-TAGS.md`
- `docs/INTERFACE-VALIDATION-MILESTONE.md`
- `docs/ROADMAP.md`

**NextActions:** none. Keep PR #54 unchanged and wait for coordinator review/integration decision.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`
