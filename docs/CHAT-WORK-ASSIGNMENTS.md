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

**CurrentTask:** Build USER INTERFACE VALIDATION PREVIEW infrastructure and Windows x64 validation package

**Branch:** `main` + `integration/interface-validation-preview`

**Status:** `IN_PROGRESS`

**Objective:**

Implement the locked interface-validation milestone after complete Internal Memory, TAG Gateway and common communication diagnostics. Produce a reproducible product-owner validation path with a single practical startup experience, production-built Web UI, required PostgreSQL/TimescaleDB services, controlled local login/bootstrap, demo/readiness, visible build identity and package smoke. Continue improving product readiness without handing the actual preview to the product owner until explicitly requested.

**AllowedScope:** coordinator-owned shared/central files, API/static-host composition, build/package scripts, Windows launcher, validation-only service orchestration, demo/bootstrap integration, build/version surface, package smoke, workflows, central UI shell/routing when required, assignment board, roadmap/handoff documentation, worker assignment and merge decisions.

**ForbiddenScope:**

- no known-failing merge;
- no force-reset/discard of worker commits;
- no MQTT/OPC UA/BACnet/S7/Driver Module production runtime before preview delivery + product-owner feedback;
- no committed production credentials, signing secrets or reusable private keys;
- no weakening authentication/authorization/Audit merely to make preview startup easier;
- no private Engineering truth or demo path that bypasses canonical Engineering/revision lifecycle;
- no production graphical editor or Python engine/editor ahead of the locked Script/visual chain.

**MustReadSpecific:**

- `docs/INTERFACE-VALIDATION-MILESTONE.md`
- `docs/COMMUNICATION-DRIVER-DIAGNOSTICS.md`
- `docs/TAG-GATEWAY.md`
- `docs/INTERNAL-MEMORY-TAGS.md`
- `docs/PARALLEL-WORK.md`
- `docs/PYTHON-SCRIPTING-AND-VISUAL-RUNTIME.md`

**ObservedGitHubState:**

- Internal Memory: **MERGED / COMPLETE** through PR #49.
- TAG Gateway Engineering + runtime/product: **MERGED / COMPLETE** through PRs #50 and #55.
- Common communication diagnostics foundation + product integration: **MERGED / COMPLETE** through PRs #56 and #57.
- PR #57 merge SHA: `c8190cc119a2e288834d619084396107103b2f56`.
- PR #57 exact-head CI #350: all green.
- Post-merge main CI #351 on `c8190cc...`: all green.
- Engineering Schema: **v9**.
- PR #53 visual-editor architecture research: Draft/Open, delivered, waiting.
- PR #54 Client Python editor/sandbox research: Draft/Open, delivered, waiting.
- USER INTERFACE VALIDATION PREVIEW is now the active locked source/protocol gate.

**Dependencies:**

- package must exercise official `main` product boundaries rather than introduce preview-only bypasses;
- PostgreSQL Engineering persistence requires `ConnectionStrings:EliteScada` and therefore the validation launcher must provide a reliable database/service path;
- TimescaleDB historian should be exercised through the existing configured provider path;
- local identity bootstrap must keep JWT secrets/password material out of committed production configuration;
- existing Vite proxy remains developer tooling; validation package should converge on a single practical entry point/same-origin Web+API composition;
- actual preview handoff remains intentionally postponed until the product owner asks for it;
- additional external protocol runtime remains blocked until preview feedback.

**NextActions:**

1. create `integration/interface-validation-preview` from synchronized green `main`;
2. integrate production-built React assets with the packaged API/runtime using one practical browser entry point while preserving Vite dev mode;
3. add safe build/version identity visible to the validation user without exposing secrets/process detail;
4. define reliable Windows x64 package build and launcher flow;
5. automate/check PostgreSQL/TimescaleDB service startup for the validation environment;
6. implement controlled first-run local identity bootstrap without committed credentials;
7. provide/verify a canonical demo/readiness path suitable for Runtime + Engineering + Memory + Gateway + diagnostics validation;
8. add validation checklist and package/startup smoke separate from repository-only execution;
9. run full CI on each candidate head and merge only green slices;
10. do not declare preview DELIVERED until the product owner explicitly requests the build and it passes the final package smoke gate.

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

**Status:** `MERGED / WAIT_FOR_COORDINATOR`

**PullRequest:** `#56 — MERGED`

**Objective:** completed isolated backend driver-layer foundation for `docs/COMMUNICATION-DRIVER-DIAGNOSTICS.md`.

**Delivered:**

- protocol-neutral communication diagnostics contract in `Scada.Drivers.Abstractions`;
- optional communication diagnostics capability so Internal Memory/simulation do not fabricate network semantics;
- Modbus TCP per-instance diagnostics/counters/state/timestamps and safe protocol details;
- focused multi-instance/failure-isolation/recovery test coverage;
- exact worker slice merged through PR #56 and then centrally integrated by PR #57.

**ForbiddenScope:** no new task, branch expansion, API/UI/DI work, protocol runtime, workflow or `main` mutation until coordinator assigns a new mission.

**MustReadSpecific:**

- `docs/COMMUNICATION-DRIVER-DIAGNOSTICS.md`
- `docs/INTERNAL-MEMORY-TAGS.md`
- `docs/TAG-GATEWAY.md`
- `docs/INTERFACE-VALIDATION-MILESTONE.md`

**NextActions:** none. Wait for coordinator.

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
