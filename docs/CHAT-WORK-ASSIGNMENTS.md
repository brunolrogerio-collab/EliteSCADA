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

**CurrentTask:** Review/integrate TAG Gateway Engineering while two isolated visual/scripting research spikes advance the future editor chain

**Branch:** `main`

**Status:** `IN_PROGRESS`

**Objective:**

Keep the locked functional path moving through TAG Gateway while using non-overlapping capacity to reduce uncertainty in the future visual editor chain. DEV 1 researches the graphical Screen/Popup/Dynamo editor architecture. DEV 3 researches the Client Python editor/sandbox technology. Neither worker may start production visual/editor runtime, canonical Script schema integration or central frontend composition while DEV 2 owns the active Gateway contract slice.

**AllowedScope:** coordinator-owned shared/central files, integration hooks, workflow maintenance, assignment board, handoff/roadmap documentation, worker assignment and merge decisions.

**ForbiddenScope:**

- no known-failing merge;
- no force-reset/discard of worker commits;
- no production OPC UA or S7 Data Source/runtime before the external-protocol gate opens;
- no new external protocol family in Active Runtime before Gateway, common diagnostics and interface-preview gates;
- no concurrent worker ownership of `src/Scada.Engineering/Contracts/EngineeringContracts.cs`;
- no canonical Script schema integration while DEV 2 owns overlapping central Engineering contract files;
- no production graphical Screen/Popup/Dynamo editor before the Script/property prerequisite chain is satisfied.

**MustReadSpecific:**

- `docs/TAG-GATEWAY.md`
- `docs/INTERNAL-MEMORY-TAGS.md`
- `docs/ADR-004-ENGINEERING-IMPORT-EXPORT.md`
- `docs/COMMUNICATION-DRIVER-DIAGNOSTICS.md`
- `docs/INTERFACE-VALIDATION-MILESTONE.md`
- `docs/PYTHON-SCRIPTING-AND-VISUAL-RUNTIME.md`
- `docs/OPC-UA.md`
- `docs/research/opc-ua/OPC-UA-DISCOVERY-IMPORT-RESEARCH.md`
- `docs/S7-ISO-CONNECTION.md`
- `docs/research/s7/S7-ISO-CONNECTION-RESEARCH.md`

**ObservedGitHubState:**

- Internal Memory: **MERGED / COMPLETE** through PR #49.
- PR #51 OPC UA research: **MERGED** `aa7735fcc15e00aea5bf19a543f53b2735ef48e3`; DEV 1's old assignment is complete.
- PR #52 Siemens S7 research: **MERGED** `bd825682ae0ccfdbdb938fab638a27f6961510bf`; DEV 3's old assignment is complete.
- PR #50 TAG Gateway Engineering: **DRAFT / OPEN / MERGEABLE**; head `002f87dd126854c9fd972e453930e229e02f7f30`; current-head CI #304 **SUCCESS**.
- TAG Gateway remains the active locked functional block.
- Production graphical visual Engineering remains behind the local prerequisite chain: `canonical Script integration -> script editor/sandbox -> visual runtime object/property integration -> graphical editor`.

**Dependencies:**

- DEV 2 exclusively owns the active public/versioned TAG Gateway Engineering slice and its narrow central Engineering contract exception.
- DEV 1 and DEV 3 may only perform the isolated research assignments below; they may not touch DEV 2's contract ownership or production editor/runtime composition.
- Canonical Script package/schema integration resumes only after the Gateway central-contract conflict clears.
- Production graphical editor work starts only after the required Script/property/sandbox/runtime prerequisites are official.

**NextActions:**

1. perform final semantic review of DEV 2 PR #50 now that CI #304 is green; merge only if clean;
2. let DEV 1 execute the isolated visual-editor architecture/UX research spike;
3. let DEV 3 execute the isolated Client Python editor/sandbox technology research spike;
4. after Gateway Engineering becomes official, schedule protocol-independent Gateway runtime work and reopen canonical Script integration without overlapping ownership;
5. use DEV 1/DEV 3 research outputs to make the later visual-editor implementation slices concrete rather than inventing architecture inside UI code;
6. preserve `Gateway -> common diagnostics -> interface preview -> external protocols` and the visual prerequisite chain independently.

**AfterCompletion:** `CONTINUE_COORDINATION`

---

# DEV 1 - EliteSCADA

**Role:** `WORKER`

**CurrentTask:** Graphical Screen/Popup/Dynamo editor architecture and Engineering UX research spike

**Branch:** `research/visual-editor-architecture`

**Status:** `ASSIGNED`

**PullRequest:** none yet

**Objective:**

Research and specify the future graphical Engineering editor for Screens, Popups and Dynamos so production implementation can later consume the existing public visual-property/scripting contracts instead of inventing a private canvas model. This assignment is architecture/UX research only and must not start the production editor.

**AllowedScope:**

- research current industrial HMI/SCADA graphical editor workflows and modern web visual-editor approaches;
- create/update only isolated documentation under `docs/research/visual-editor/**`;
- compare renderer/editor approaches such as SVG, DOM/CSS, Canvas/WebGL and justified libraries/frameworks without adding production dependencies;
- define object palette, canvas/workspace, selection/multi-selection, marquee selection, pan/zoom, rulers/grid/guides, snap/alignment/distribution, resize/rotate, z-order and grouping/container behavior;
- define property-inspector behavior that consumes the same public typed visual-property schema used by scripts;
- define design-time base values versus runtime overrides and make this visible in UX where useful;
- define TAG/expression binding UX, script/event association UX and conflict/precedence diagnostics;
- define Screen/Popup/Dynamo hierarchy, reusable Dynamo/template composition and instance overrides;
- define image/resource selection through stable Engineering asset references rather than filesystem paths;
- define undo/redo command model, copy/paste and future Engineering Fragment/dependency behavior;
- define keyboard/mouse interaction, accessibility considerations, localization and high-performance-HMI editing needs;
- define large-screen/object-count performance targets and test strategy;
- produce a staged future implementation breakdown and identify exact prerequisite integration hooks.

**ForbiddenScope:**

- `main`;
- production graphical editor code or production renderer integration;
- adding/changing frontend dependencies or lockfiles;
- central frontend routing/shell;
- `EngineeringContracts.cs` or any canonical schema change;
- Script canonical package integration;
- Python engine/editor implementation;
- runtime visual object composition;
- Gateway/runtime/protocol work;
- `.github/workflows/**`;
- claiming the graphical editor is implemented because research/prototypes are complete.

**MustReadSpecific:**

- `docs/PYTHON-SCRIPTING-AND-VISUAL-RUNTIME.md`
- `docs/ADR-004-ENGINEERING-IMPORT-EXPORT.md`
- `docs/INTERFACE-VALIDATION-MILESTONE.md`
- `docs/ROADMAP.md`

**CompletionCriteria:**

1. Compare credible renderer/editor approaches and recommend a direction with performance, maintainability, accessibility and testing rationale.
2. Define the complete authoring interaction model: palette, canvas, selection, transform, grid/guides/snap, z-order, groups/containers and keyboard workflows.
3. Define property-inspector architecture using the public visual property schema rather than renderer-private property lists.
4. Define TAG/expression bindings and script/event association UX with deterministic conflict/precedence visibility.
5. Define Screen/Popup/Dynamo composition, reusable definitions, instance context and controlled overrides.
6. Define stable visual asset/resource handling and preview behavior.
7. Define undo/redo plus future Engineering Fragment/copy-paste dependency semantics.
8. Define practical performance/test targets for large screens and many visual objects.
9. Produce concrete future implementation slices and `INTEGRATION REQUIRED` hooks.
10. Add no production editor code/dependencies and touch no central contracts.

**NextActions:**

1. create/use only `research/visual-editor-architecture` from current `main`;
2. reread mandatory/specific docs and inspect existing public visual/scripting contracts before recommending UI architecture;
3. write the isolated research deliverable with concrete EliteSCADA decisions, not generic UI advice;
4. open/update a Draft documentation-only PR marked `RESEARCH IN PR / NOT IMPLEMENTED`;
5. stop under `WAIT_FOR_COORDINATOR` when complete.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`

---

# DEV 2 - EliteSCADA

**Role:** `WORKER`

**CurrentTask:** Public TAG Gateway Engineering contract and deterministic validation foundation

**Branch:** `feature/tag-gateway-engineering`

**Status:** `READY_FOR_COORDINATOR_REVIEW`

**PullRequest:** `#50 DRAFT / OPEN`

**Objective:** implement the first-class public/versioned Gateway/TAG Bridge Engineering domain and deterministic Preview validation required by `docs/TAG-GATEWAY.md`, without runtime transfer execution, API/DI composition, diagnostics UI or protocol-specific behavior.

**AllowedScope:** new Gateway Engineering files; narrow exclusive Gateway exception for `src/Scada.Engineering/Contracts/EngineeringContracts.cs`; required Gateway integration in Engineering import/export; focused Core/PostgreSQL/package tests; PR evidence.

**ForbiddenScope:** `main`; Program.cs/API/central DI; DriverHost Gateway runtime; TAG event/write execution; frontend Gateway UI; workflows; protocol-specific code; common diagnostics; Script canonical integration; Client Memory Gateway endpoints; silent coercion.

**MustReadSpecific:**

- `docs/TAG-GATEWAY.md`
- `docs/INTERNAL-MEMORY-TAGS.md`
- `docs/ADR-004-ENGINEERING-IMPORT-EXPORT.md`
- `docs/COMMUNICATION-DRIVER-DIAGNOSTICS.md`

**CompletionCriteria:** first-class Gateway routes; stable endpoints/policies; deliberate schema evolution/compatibility; deterministic endpoint/type/cycle/multi-writer/rate validation; Server Memory accepted and Client Memory rejected; fan-out valid; canonical round trips/package persistence; no runtime engine/UI; focused tests and current-head CI green; future runtime `INTEGRATION REQUIRED` recorded.

**NextActions:** task is delivered for coordinator review. Keep PR #50 unchanged except for fixes specifically requested by coordinator. Do not start another task and do not merge own PR.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`

---

# DEV 3 - EliteSCADA

**Role:** `WORKER`

**CurrentTask:** Client Python script editor, browser sandbox and execution-engine technology research spike

**Branch:** `research/client-python-editor-sandbox`

**Status:** `ASSIGNED`

**PullRequest:** none yet

**Objective:**

Research and specify the practical Client Visual Python editor/sandbox stack required before graphical visual Engineering can begin. Select a defensible browser/WASM Python execution direction and editor integration strategy while preserving EliteSCADA security, deterministic event execution and the public scripting/property contracts. This assignment is research only and must not install or implement the production Python engine.

**AllowedScope:**

- research browser/WASM Python runtimes including Pyodide, MicroPython/WASM and other justified candidates;
- compare licensing, maintenance, browser compatibility, startup/download size, offline packaging, Python language/library coverage, JS interop, worker compatibility, interruption/cancellation, memory behavior and testability;
- research code-editor options such as Monaco/CodeMirror-class components without adding production dependencies;
- create/update only isolated documentation under `docs/research/python-client/**`;
- define Web Worker or equivalent isolation, execution budgets, cancellation/interrupt behavior, event queue/backpressure/coalescing and failure isolation;
- define the EliteSCADA script API injection boundary for TAG reads, Client Memory, visual-object/property access, animation requests and authorized backend actions without DOM/driver/database/filesystem exposure;
- define syntax/compile validation, line/column diagnostics, autocomplete/stubs/intellisense direction, source/version handling and event/entry-point association;
- define sandboxed test/preview behavior that cannot mutate Active Engineering silently;
- define Content Security Policy/network/file/package restrictions and explicit package/module policy;
- define offline/packaged Windows-preview requirements and browser caching/versioning strategy;
- define benchmark, security and automated test scenarios;
- produce staged future implementation slices and exact `INTEGRATION REQUIRED` hooks after canonical Script integration.

**ForbiddenScope:**

- `main`;
- adding Pyodide, MicroPython, Monaco, CodeMirror or another production dependency/lockfile change;
- implementing the production script editor or Python runtime;
- central frontend routing/shell;
- `EngineeringContracts.cs`, canonical Script schema/package integration or Gateway schema;
- server Python runtime;
- direct DOM, filesystem, shell, arbitrary network, industrial driver or database access;
- production visual runtime/editor code;
- `.github/workflows/**`;
- claiming Client Python scripting is implemented because research or an external demo works.

**MustReadSpecific:**

- `docs/PYTHON-SCRIPTING-AND-VISUAL-RUNTIME.md`
- `docs/ADR-004-ENGINEERING-IMPORT-EXPORT.md`
- `docs/INTERNAL-MEMORY-TAGS.md`
- `docs/INTERFACE-VALIDATION-MILESTONE.md`
- `docs/ROADMAP.md`

**CompletionCriteria:**

1. Compare viable browser Python engines and recommend a first implementation/lab direction with license, maintenance, size, compatibility and sandbox rationale.
2. Recommend a code-editor integration direction and explain autocomplete/diagnostic strategy.
3. Define client-side isolation architecture, worker lifecycle, execution budgets, cancellation/interruption and bounded event queues.
4. Define the public EliteSCADA API injection/security boundary and explicitly forbidden capabilities.
5. Define script validation and line/column diagnostic flow from edit through Preview/Apply.
6. Define sandboxed test/preview semantics that cannot mutate Active Engineering or escape user authorization.
7. Define offline packaging/cache/version strategy suitable for the planned Windows x64 product preview and later runtime clients.
8. Define performance/security/compatibility benchmarks and automated test scenarios.
9. Produce concrete future implementation slices and `INTEGRATION REQUIRED` hooks after canonical Script integration.
10. Add no production dependencies/code and touch no central contracts.

**NextActions:**

1. create/use only `research/client-python-editor-sandbox` from current `main`;
2. reread mandatory/specific docs and inspect merged Script/visual foundation contracts before selecting technology;
3. research current candidate engines/editors and write an EliteSCADA-specific recommendation;
4. open/update a Draft documentation-only PR marked `RESEARCH IN PR / NOT IMPLEMENTED`;
5. stop under `WAIT_FOR_COORDINATOR` when complete.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`
