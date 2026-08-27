# LAST CHANGE — EliteSCADA

> Operational handoff. Read with `PROJECT GOAL.md`, `docs/ROADMAP.md`, `docs/PARALLEL-WORK.md` and `docs/CHAT-WORK-ASSIGNMENTS.md` before every EliteSCADA task.

**Handoff date:** 2026-08-26  
**Development state:** **ACTIVE — TAG GATEWAY REVIEW + VISUAL EDITOR / CLIENT PYTHON RESEARCH IN PARALLEL**

Repository truth is separated into **MERGED**, **IMPLEMENTED IN PR**, **RESEARCH IN PR** and **SPECIFIED / NOT IMPLEMENTED**.

## CURRENT CHECKPOINT

The locked functional source/protocol sequence remains:

`Internal Memory -> TAG Gateway -> common multi-driver diagnostics -> USER INTERFACE VALIDATION PREVIEW -> additional external protocols`

The locked visual/scripting prerequisite chain remains:

`canonical Script integration -> script editor/sandbox -> visual runtime object/property integration -> graphical Screen/Popup/Dynamo editor -> advanced visual libraries`

Current functional state:

- Internal Memory: **MERGED / COMPLETE** through PR #49;
- TAG Gateway Engineering/validation: **IMPLEMENTED IN PR #50 / READY FOR COORDINATOR REVIEW**;
- common multi-driver diagnostics: **SPECIFIED / NOT IMPLEMENTED — BLOCKED BY GATEWAY**;
- interface validation preview: **SPECIFIED / NOT IMPLEMENTED — BLOCKED BY DIAGNOSTICS**;
- production MQTT/OPC UA/BACnet/S7 remain gated;
- isolated Script Engineering foundation: **MERGED**, but canonical package/schema integration remains pending;
- production Python editor/sandbox, visual runtime integration and graphical editor remain **SPECIFIED / NOT IMPLEMENTED**.

## TAG GATEWAY — DEV 2 DELIVERED FOR REVIEW

PR #50 `Add TAG Gateway Engineering foundation` is Draft/Open and mergeable.

Current head:

`002f87dd126854c9fd972e453930e229e02f7f30`

Current-head CI #304 completed **SUCCESS**. The worker reports a reconciled Gateway-only delta with schema v9, first-class routes, endpoint/type/rate validation, direct/indirect cycle rejection, multiple-writer rejection, Server Memory support, Client Memory rejection and canonical package/revision persistence coverage. Runtime Gateway execution/API/UI/DI/diagnostics remain outside this worker slice.

DEV 2 is now `READY_FOR_COORDINATOR_REVIEW / WAIT_FOR_COORDINATOR` and must not start another task.

## MERGED PROTOCOL RESEARCH

### OPC UA

DEV 1's previous research PR #51 is **MERGED** as `aa7735fcc15e00aea5bf19a543f53b2735ef48e3`. Production OPC UA remains gated.

### Siemens S7 ISO Connection

DEV 3's previous research PR #52 is **MERGED** as `bd825682ae0ccfdbdb938fab638a27f6961510bf`. Production S7 remains gated. S7.NetPlus is only the preferred first future laboratory candidate, not a selected production dependency.

## NEW PARALLEL VISUAL/EDITOR RESEARCH ASSIGNMENTS

The product owner requested use of idle DEV capacity to advance the future screen editor and adjacent prerequisites without violating the locked dependency chain.

### DEV 1 — Graphical visual editor architecture/UX research

Assigned branch:

`research/visual-editor-architecture`

Status: **ASSIGNED**.

Scope is documentation/research only. DEV 1 must define the future Screen/Popup/Dynamo authoring model, including renderer/editor direction, palette/canvas, selection/multi-select, transforms, grid/guides/snap, z-order/groups, property inspector using the public visual property schema, TAG/expression bindings, scripts/events, undo/redo, copy/paste/Engineering Fragments, resources, Dynamo composition, large-screen performance and implementation slices.

Forbidden: production editor code, dependencies/lockfiles, central frontend routing, central Engineering schema, Python runtime implementation or visual runtime composition.

### DEV 3 — Client Python editor/sandbox technology research

Assigned branch:

`research/client-python-editor-sandbox`

Status: **ASSIGNED**.

Scope is documentation/research only. DEV 3 must compare browser/WASM Python engines and editor technology, define worker/sandbox isolation, time/memory/event budgets, cancellation, EliteSCADA API injection, line/column diagnostics, autocomplete/stubs, test/preview semantics, CSP/network/package restrictions, offline packaging and benchmark/security test strategy.

Forbidden: production dependencies, lockfiles, production Python runtime/editor, central Script schema integration, server Python, central routing or graphical editor/runtime code.

These assignments deliberately reduce future implementation uncertainty while DEV 2's central Gateway contract ownership finishes. They do not authorize production graphical editor work before Script/sandbox/runtime prerequisites are official.

## COORDINATOR RESUME POINT

On the next coordinator `siga`:

1. reread mandatory docs from current `main`;
2. perform final semantic/diff review of PR #50 now that CI #304 is green;
3. merge Gateway Engineering only if the reviewed current head remains clean and within assignment;
4. after merge, reconcile official schema/roadmap state and assign the protocol-independent Gateway runtime engine;
5. monitor DEV 1 `research/visual-editor-architecture` and DEV 3 `research/client-python-editor-sandbox` for research-only compliance;
6. after Gateway central-contract ownership clears, resume canonical Script package/schema integration before any production Python editor or graphical editor implementation;
7. preserve both locked dependency chains.

## Permanent continuity rules

- GitHub branch/PR/head/CI state is operational truth.
- Open functional PRs are **IMPLEMENTED IN PR**, not MERGED.
- Research branches/PRs are architecture inputs, not implemented product functionality.
- Workers never choose their own next task or merge their own PR.
- Shared central integration belongs to the coordinator unless a narrow exception is explicitly assigned.
- Known-failing work is never merged.
- `siga` is the canonical short command; `continue` is equivalent.
