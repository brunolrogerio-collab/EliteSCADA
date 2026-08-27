# LAST CHANGE — EliteSCADA

> Operational handoff. Read with `PROJECT GOAL.md`, `docs/ROADMAP.md`, `docs/PARALLEL-WORK.md` and `docs/CHAT-WORK-ASSIGNMENTS.md` before every EliteSCADA task.

**Handoff date:** 2026-08-27  
**Development state:** **ACTIVE — COMMON DIAGNOSTICS COMPLETE / USER INTERFACE VALIDATION PREVIEW NEXT**

Repository truth is separated into **MERGED**, **IMPLEMENTED IN PR**, **RESEARCH IN PR** and **SPECIFIED / NOT IMPLEMENTED**.

## CURRENT CHECKPOINT

The locked functional source/protocol sequence remains:

`Internal Memory -> TAG Gateway -> common multi-driver diagnostics -> USER INTERFACE VALIDATION PREVIEW -> additional external protocols`

The locked visual/scripting prerequisite chain remains:

`canonical Script integration -> script editor/sandbox -> visual runtime object/property integration -> graphical Screen/Popup/Dynamo editor -> advanced visual libraries`

Current functional state:

- Internal Memory: **MERGED / COMPLETE** through PR #49;
- TAG Gateway: **MERGED / COMPLETE** through PRs #50 and #55;
- common multi-driver/Data Source diagnostics: **MERGED / COMPLETE** through PRs #56 and #57;
- canonical Engineering: **Schema v9**;
- USER INTERFACE VALIDATION PREVIEW: **ACTIVE NEXT PRODUCT BLOCK**;
- production MQTT/OPC UA/BACnet/S7 and Driver Module implementation remain blocked until product-owner preview/feedback;
- isolated Script Engineering foundation: **MERGED**, canonical package/schema integration still pending;
- production Python editor/sandbox, visual runtime integration and graphical editor remain **SPECIFIED / NOT IMPLEMENTED**.

## COMMON COMMUNICATION / DATA SOURCE DIAGNOSTICS — COMPLETE ON `main`

PR #56 merged the isolated protocol-neutral diagnostics contract and Modbus TCP instrumentation foundation into `main` at base checkpoint `5520d0190d74618addeeea40b05507e3cb772d21`.

PR #57 `Integrate common communication diagnostics product` then merged as:

`c8190cc119a2e288834d619084396107103b2f56`

The final PR #57 head was:

`9fffd193153f50a937be3b8343c255a498701808`

CI #350 passed Web build, backend build/tests, runtime smoke and Chromium E2E on that exact head. Post-merge main CI #351 (`33086312673`) also completed **SUCCESS** for all three jobs on merge SHA `c8190cc...`.

Merged diagnostic capabilities now include:

- protocol-neutral communication diagnostic snapshots only for real communication-capable drivers;
- canonical Data Source identity kept distinct from Driver type and runtime instance identity;
- healthy/degraded/reconnecting/faulted operational semantics with state timestamps;
- request/success/failure/timeout/connect/reconnect/read/write/update counters where meaningful;
- last successful/failed communication, sanitized error, recent failure rate, latency, data age and scan timing;
- TAG-quality aggregation per active Data Source without replacing point-level quality authority;
- Modbus TCP instrumentation with safe protocol details;
- automated proof with two simultaneous Modbus Data Sources, isolated failure/recovery/counters/TAG quality and correct write ownership;
- Simulation and Internal Memory excluded from fabricated network/reconnect/timeout semantics;
- protected runtime diagnostics surfaced through the existing diagnostics boundary;
- elaborated Engineering diagnostics UX with health summary, severity ordering, search, filters, automatic/manual refresh, master/detail navigation, quality/activity/timing/protocol drill-down, responsive layout and `pt-BR` / `en` / `es` copy.

The diagnostics gate is therefore closed as **MERGED / COMPLETE**.

## ACTIVE NEXT BLOCK — USER INTERFACE VALIDATION PREVIEW

The next official block is `docs/INTERFACE-VALIDATION-MILESTONE.md`.

The user explicitly requested that interface work be elaborated and that the project advance, while postponing delivery of the actual visual preview until a later request. Therefore the current coordinator work is to build the preview infrastructure/package now without presenting the preview as delivered yet.

Required implementation direction:

- primary practical target: Windows x64;
- single practical startup path rather than separate developer-only API/Vite terminals;
- built React application served from the packaged EliteSCADA runtime or an equivalently reliable single entry point;
- PostgreSQL/TimescaleDB and required services started through a reliable launcher/automation rather than hand reconstruction;
- local identity/login bootstrap without committed production credentials or secrets;
- sample/demo project suitable for Runtime and Engineering validation;
- visible build/version identity tied to an exact source state;
- short validation checklist;
- package/startup smoke test separate from repository-only `dotnet run` / Vite execution;
- preserve security, Audit, Engineering revision lifecycle, TAG quality, Gateway and diagnostics boundaries.

No new production external protocol family is authorized until this preview is actually handed to the product owner and feedback is reviewed.

## RESEARCH DELIVERIES WAITING FOR COORDINATOR

### DEV 1 — graphical visual editor architecture/UX

Draft PR #53, branch `research/visual-editor-architecture`, head `15e74e3b3915de7de6639e5c296fdcc2e229793a`.

Status: **RESEARCH IN PR / DELIVERED / WAIT_FOR_COORDINATOR**. No production editor code or dependency is authorized by this research.

### DEV 3 — Client Python editor/browser sandbox

Draft PR #54, branch `research/client-python-editor-sandbox`, head `d3bef9636f6ffd44a7be6f56a144296e38744474`.

Status: **RESEARCH IN PR / DELIVERED / WAIT_FOR_COORDINATOR**. The Pyodide/Monaco direction remains a research recommendation, not a selected production dependency.

## COORDINATOR RESUME POINT

On the next coordinator `siga`:

1. reread mandatory docs from current `main` and verify GitHub branch/PR/CI truth;
2. continue the coordinator-owned USER INTERFACE VALIDATION PREVIEW implementation;
3. preserve the product-owner direction that the preview itself is not handed off until requested, while continuing package/startup/UI-readiness work;
4. build a reliable Windows x64 package path with database/services launcher, local login/bootstrap, demo, build identity and validation checklist;
5. add package/startup smoke validation separate from repository developer execution;
6. merge only current-head green work and reconcile docs after each completed integration slice;
7. do not start MQTT/OPC UA/BACnet/S7/Driver Module production runtime before preview delivery and feedback;
8. keep DEV 1/DEV 3 research as non-production inputs until their prerequisite chains are explicitly opened.

## Permanent continuity rules

- GitHub branch/PR/head/CI state is operational truth.
- Open functional PRs are **IMPLEMENTED IN PR**, not MERGED.
- Research branches/PRs are architecture inputs, not implemented product functionality.
- Workers never choose their own next task or merge their own PR.
- Shared central integration belongs to the coordinator unless a narrow exception is explicitly assigned.
- Known-failing work is never merged.
- `siga` is the canonical short command; `continue` is equivalent.
