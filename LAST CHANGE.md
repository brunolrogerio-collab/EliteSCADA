# LAST CHANGE — EliteSCADA

> Operational handoff. Read with `PROJECT GOAL.md`, `docs/ROADMAP.md`, `docs/PARALLEL-WORK.md`, `docs/CHAT-WORK-ASSIGNMENTS.md` and the current task-specific documents before every EliteSCADA action.

**Handoff date:** 2026-08-27  
**Development state:** **ACTIVE — INTERFACE PRODUCT DEVELOPMENT / CENTRAL INTEGRATION**

Repository truth remains separated into **MERGED**, **IMPLEMENTED IN PR**, **RESEARCH IN PR** and **SPECIFIED / NOT IMPLEMENTED**.

## CURRENT CHECKPOINT

Merged platform foundations remain:

- Internal Memory: **MERGED / COMPLETE** through PR #49;
- TAG Gateway: **MERGED / COMPLETE** through PRs #50 and #55;
- common multi-driver/Data Source diagnostics: **MERGED / COMPLETE** through PRs #56 and #57;
- canonical Engineering: **Schema v9**;
- current production external protocol baseline remains Modbus TCP plus built-in Simulation/Internal Memory;
- additional MQTT/OPC UA/BACnet/S7/Driver Module production work remains postponed;
- provisional Windows x64 validation/presentation packaging remains postponed until the interface matures further.

The active order remains:

`merged platform foundations -> interface product development -> user validation build/package -> additional external drivers/protocols`

## INTERFACE WORKER SLICES — NOW MERGED

All three worker assignments created for the first interface block are complete, reviewed, exact-head green and merged into official `main`.

### DEV 3 — session/user menu

PR #59 `Add authenticated session user-menu primitive`

- final worker head: `f0b120c3ec3e268b9c7875fc73450a150e1dda5a`;
- CI #363: **SUCCESS**;
- reviewed scope: exactly five authorized auth/session UI/test files;
- merge commit: `b0b58964f119f83356cf2edc8fecf5939fb905da`;
- status: **MERGED / WAITING**.

### DEV 1 — Engineering entity browser

PR #60 `Add Engineering entity browser workspace primitives`

- final worker head: `ab8e7e0b698a8533ea6a08048deb3c464840e843`;
- CI #359: **SUCCESS**;
- reviewed scope: exactly four authorized Engineering UI/test files;
- merge commit: `a7e6105fb65079ad1af8bcb56f8484225ff3dc8c`;
- status: **MERGED / WAITING**.

### DEV 2 — Runtime operational overview

PR #61 `Add runtime operational overview UI primitives`

- final worker head: `7fbd564c93af5ad3d4c83d2ddc8d5ed782d2957d`;
- CI #360: **SUCCESS**;
- reviewed scope: seven authorized Runtime UI/helper/test files;
- diagnostics remain read-only and protected; Simulation/restricted states do not fabricate communication failure;
- merge commit: `49c9e7261d63047b601f4b3c4f6e788168c8ee5c`;
- status: **MERGED / WAITING**.

The assignment board was reconciled in main commit `2996ca88cd58add0575962bc0440866012b0e90b`.

## COORDINATOR PR #58

`feature/interface-product-development` / PR #58 remains **IMPLEMENTED IN PR / DRAFT / NOT MERGED**.

Current known head before reconciliation with the worker merges:

`bc6ba6cd760649064984208b0b0584f9a9c28042`

It contains the coordinator product-shell/navigation slice. CI #357 on that head produced:

- Web build: **PASS**;
- backend build/tests/runtime smoke: **PASS**;
- Chromium E2E: **FAIL**.

Therefore PR #58 must not be merged yet. The coordinator must reconcile the branch with current `main`, integrate the three newly merged worker components, fix the Chromium failure at the root and rerun the full CI.

## PARKED WORK

`integration/interface-validation-preview` remains **PARKED / NO PR / NOT MERGED**. Its preparatory work is preserved but is not the active branch.

PR #53 graphical visual editor architecture and PR #54 Client Python editor/browser sandbox remain **RESEARCH IN PR / DELIVERED**, not production implementations.

## COORDINATOR RESUME POINT

On the next coordinator `siga`:

1. reread mandatory docs from current `main`;
2. verify live `main`, PR #58/head/CI and worker merge state;
3. reconcile `feature/interface-product-development` with current `main` without discarding shell work;
4. wire merged `UserSessionMenu` into the global product shell;
5. wire merged `EngineeringEntityBrowser` into the Engineering workspace while preserving Preview/Apply/CAS semantics;
6. wire merged `RuntimeOperationsOverview` into Runtime while preserving the process demo;
7. fix the current Chromium failure and add integrated UX coverage;
8. run full Web/backend/smoke/Chromium CI and merge only a green reviewed PR #58;
9. keep DEV 1/2/3 idle until the coordinator explicitly assigns the next interface slice;
10. do not resume new drivers or provisional Windows packaging unless product priority changes.

## Permanent continuity rules

- GitHub branch/PR/head/CI state is operational truth.
- Open functional PRs are **IMPLEMENTED IN PR**, not MERGED.
- Research branches/PRs are architecture inputs, not implemented product functionality.
- Workers never choose their own next task or merge their own PR.
- Shared central integration belongs to the coordinator unless a narrow exception is explicitly assigned.
- Known-failing work is never merged.
- `siga` is the canonical short command; `continue` is equivalent.
