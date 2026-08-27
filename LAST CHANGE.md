# LAST CHANGE — EliteSCADA

> Operational handoff. Read with `PROJECT GOAL.md`, `docs/ROADMAP.md`, `docs/PARALLEL-WORK.md` and `docs/CHAT-WORK-ASSIGNMENTS.md` before every EliteSCADA task.

**Handoff date:** 2026-08-27  
**Development state:** **ACTIVE — TAG GATEWAY COMPLETE / COMMON MULTI-DRIVER DIAGNOSTICS NEXT**

Repository truth is separated into **MERGED**, **IMPLEMENTED IN PR**, **RESEARCH IN PR** and **SPECIFIED / NOT IMPLEMENTED**.

## CURRENT CHECKPOINT

The locked functional source/protocol sequence remains:

`Internal Memory -> TAG Gateway -> common multi-driver diagnostics -> USER INTERFACE VALIDATION PREVIEW -> additional external protocols`

The locked visual/scripting prerequisite chain remains:

`canonical Script integration -> script editor/sandbox -> visual runtime object/property integration -> graphical Screen/Popup/Dynamo editor -> advanced visual libraries`

Current functional state:

- Internal Memory: **MERGED / COMPLETE** through PR #49;
- TAG Gateway: **MERGED / COMPLETE** through PRs #50 and #55;
- canonical Engineering: **Schema v9**, with first-class Gateway routes;
- common multi-driver/Data Source diagnostics: **ACTIVE NEXT PRODUCT BLOCK**;
- USER INTERFACE VALIDATION PREVIEW: **SPECIFIED / NOT IMPLEMENTED — BLOCKED BY COMMON DIAGNOSTICS**;
- production MQTT/OPC UA/BACnet/S7 and Driver Module implementation remain gated by the preview;
- isolated Script Engineering foundation: **MERGED**, canonical package/schema integration still pending;
- production Python editor/sandbox, visual runtime integration and graphical editor remain **SPECIFIED / NOT IMPLEMENTED**.

## TAG GATEWAY — COMPLETE ON `main`

PR #50 merged the canonical public/versioned Gateway Engineering foundation and Schema v9.

PR #55 `Complete protocol-independent TAG Gateway runtime` merged as:

`41bc437ba64f60fba26754794a9dc5a4e9a034f7`

Validated Gateway capabilities now include:

- protocol-independent TAG-to-TAG routing over the common TAG/Event Bus/write boundary;
- destination writes through active runtime ownership rather than driver-to-driver coupling;
- OnChange and Periodic execution;
- Good-only source quality gating;
- startup synchronization policy;
- deadband, minimum interval and newest-value coalescing;
- Exact and CheckedNumeric conversion with gain/offset and checked overflow/narrowing;
- fan-out and canonical cycle/multiple-writer validation;
- Server Memory as valid server endpoint and Client Memory rejected;
- route-local diagnostics/counters without contaminating source TAG quality;
- transactional Active Revision route replacement;
- protected Gateway diagnostics API;
- Engineering Data Sources Gateway tool using canonical Preview/Apply + Workspace CAS;
- runtime diagnostics UI.

Automated runtime proof includes Modbus -> Server Memory, Server Memory -> Modbus, independent Modbus -> Modbus, quality suppression, destination failure/recovery, cadence/coalescing, conversion/overflow, fan-out and revision switching.

PR #55 branch CI #333 was fully green.

The first post-merge main CI #334 exposed two timing-sensitive tests. The coordinator hardened only the tests, not product semantics. Commit `782c65fe3c44061b6e2bb13f1a6b905db6b1c102` increased deterministic waits, and commit `cb4d2c423c31cf7a52ea6ebe6de494c281901f3f` corrected the resulting collection-access compile mistake. Main CI #336 on `cb4d2c42...` completed **SUCCESS** for Web build, backend build/tests, runtime smoke and Chromium E2E.

The TAG Gateway gate is therefore closed as **MERGED / COMPLETE**.

## ACTIVE NEXT BLOCK — COMMON COMMUNICATION / DATA SOURCE DIAGNOSTICS

The next official product block is `docs/COMMUNICATION-DRIVER-DIAGNOSTICS.md`.

Required direction:

- protocol-neutral diagnostic snapshot for external communication Data Sources;
- Data Source identity kept separate from Driver type;
- healthy/degraded/reconnecting/faulted operational distinction;
- success/failure/timeout/reconnect/request/read/write/update counters;
- last success/failure and sanitized error timestamps/messages;
- useful latency, failure-rate, data-age and observed-scan metrics where meaningful;
- per-Data-Source TAG-quality aggregation;
- strict isolation between simultaneous Data Sources;
- no fabricated transport/network metrics for Internal Memory or built-in simulation;
- protected backend diagnostic API and Engineering diagnostics UI owned by coordinator integration.

DEV 2 is assigned the isolated common-contract + Modbus instrumentation foundation. Coordinator retains central DriverHost/API/DI/UI integration and final multi-instance acceptance.

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
2. monitor DEV 2 `feature/communication-driver-diagnostics` against its exact assignment;
3. prepare coordinator-owned protected diagnostics API/DriverHost composition and Engineering diagnostics UI without overlapping DEV 2 files;
4. validate two simultaneous Modbus Data Sources with independent failure/recovery/counters/TAG quality;
5. merge only after current-head CI is fully green;
6. after common diagnostics is complete and green on `main`, create the USER INTERFACE VALIDATION PREVIEW build before any new production external protocol;
7. keep DEV 1/DEV 3 research as non-production inputs until their prerequisite chains are explicitly opened.

## Permanent continuity rules

- GitHub branch/PR/head/CI state is operational truth.
- Open functional PRs are **IMPLEMENTED IN PR**, not MERGED.
- Research branches/PRs are architecture inputs, not implemented product functionality.
- Workers never choose their own next task or merge their own PR.
- Shared central integration belongs to the coordinator unless a narrow exception is explicitly assigned.
- Known-failing work is never merged.
- `siga` is the canonical short command; `continue` is equivalent.
