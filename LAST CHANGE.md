# LAST CHANGE — EliteSCADA

> Operational handoff. Read with `PROJECT GOAL.md`, `docs/ROADMAP.md`, `docs/PARALLEL-WORK.md`, `docs/CHAT-WORK-ASSIGNMENTS.md` and the current task-specific documents before every EliteSCADA action.

**Handoff date:** 2026-08-27  
**Development state:** **INTERFACE CENTRAL INTEGRATION PAUSED BY PRODUCT OWNER / FUTURE PROTOCOL RESEARCH ACTIVE**

Repository truth remains separated into **MERGED**, **IMPLEMENTED IN PR**, **RESEARCH IN PR** and **SPECIFIED / NOT IMPLEMENTED**.

## CURRENT CHECKPOINT

Merged platform foundations remain:

- Internal Memory: **MERGED / COMPLETE** through PR #49;
- TAG Gateway: **MERGED / COMPLETE** through PRs #50 and #55;
- common multi-driver/Data Source diagnostics: **MERGED / COMPLETE** through PRs #56 and #57;
- canonical Engineering: **Schema v9**;
- current production external protocol baseline remains Modbus TCP plus built-in Simulation/Internal Memory;
- provisional Windows x64 validation/presentation packaging remains postponed;
- production MQTT/OPC UA/BACnet/S7/Allen-Bradley/Driver Module work remains postponed.

The active product order remains:

`merged platform foundations -> interface product development -> user validation build/package -> additional external drivers/protocols`

Research/specification spikes may run earlier only to reduce uncertainty. They do not authorize production runtime, Data Source registration or bypass of the product gate.

## FIRST INTERFACE WORKER SLICES — MERGED

All three interface worker assignments were reviewed, exact-head green and merged into official `main`:

- DEV 3 / PR #59 session user menu: head `f0b120c3ec3e268b9c7875fc73450a150e1dda5a`, CI #363 green, merge `b0b58964f119f83356cf2edc8fecf5939fb905da`;
- DEV 1 / PR #60 Engineering entity browser: head `ab8e7e0b698a8533ea6a08048deb3c464840e843`, CI #359 green, merge `a7e6105fb65079ad1af8bcb56f8484225ff3dc8c`;
- DEV 2 / PR #61 Runtime operational overview: head `7fbd564c93af5ad3d4c83d2ddc8d5ed782d2957d`, CI #360 green, merge `49c9e7261d63047b601f4b3c4f6e788168c8ee5c`.

## COORDINATOR PR #58 — PAUSED UNTIL USER `siga`

`feature/interface-product-development` / PR #58 remains **IMPLEMENTED IN PR / DRAFT / NOT MERGED**.

Known pre-reconciliation head: `bc6ba6cd760649064984208b0b0584f9a9c28042`.

CI #357 on that head:

- Web build: **PASS**;
- backend build/tests/runtime smoke: **PASS**;
- Chromium E2E: **FAIL**.

The product owner explicitly asked the coordinator not to continue its own interface integration yet. On the next coordinator `siga`, reconcile #58 with then-current `main`, integrate the three merged worker components, fix Chromium at the root and rerun full CI.

## NEW PARALLEL RESEARCH ASSIGNMENTS

The product owner explicitly authorized future-driver research now, comparable to the earlier OPC UA and Siemens S7 spikes, so that later production implementation can be coordinated from evidence instead of guesswork.

Assignment board commit: `76018909264511d7090db3e7d9fb2181763fe4ca`.

### DEV 1 — MQTT industrial driver research

Branch: `research/mqtt-industrial-driver`

Status: **ASSIGNED — RESEARCH ONLY**.

Expected primary deliverable:

`docs/research/mqtt/MQTT-INDUSTRIAL-DRIVER-RESEARCH.md`

Required direction includes MQTT 3.1.1/5.0, sessions/reconnect, QoS, retained messages, Last Will, TLS/mTLS and protected secret references, Topic↔TAG/payload mapping, write/publish semantics, honest topic discovery, common diagnostics, multi-broker behavior, library/broker comparison, test matrix and a strict separation between raw MQTT and optional Sparkplug B semantics.

No production MQTT dependency/runtime/Data Source may be added.

### DEV 2 — Allen-Bradley EtherNet/IP/CIP Logix research

Branch: `research/allen-bradley-ethernet-ip`

Status: **ASSIGNED — RESEARCH ONLY**.

Expected primary deliverable:

`docs/research/allen-bradley/ALLEN-BRADLEY-ETHERNET-IP-CIP-RESEARCH.md`

Required direction includes ControlLogix/CompactLogix-first scope, EtherNet/IP + CIP explicit messaging, routing/slot paths, controller/program symbolic TAGs, arrays/UDTs/common Logix types, External Access/write safety, browse/import, L5X/L5K investigation, batching/fragmentation/connection constraints, CIP Security implications, library/license comparison and real-hardware acceptance strategy. Generic implicit I/O and destructive controller engineering must not be silently treated as the first SCADA-driver scope.

No production Allen-Bradley dependency/runtime/Data Source may be added.

### DEV 3 — BACnet/IP + BACnet/SC research

Branch: `research/bacnet-ip-secure-connect`

Status: **ASSIGNED — RESEARCH ONLY**.

Expected primary deliverable:

`docs/research/bacnet/BACNET-IP-SC-RESEARCH.md`

Required direction includes BACnet/IP discovery (Who-Is/I-Am), device/object/property identity, BBMD/Foreign Device/cross-subnet behavior, ReadProperty/ReadPropertyMultiple, WriteProperty, COV/polling, segmentation/APDU constraints, Present_Value plus status/reliability/unit semantics, write priority/relinquish, proprietary object visibility, common diagnostics, and forward-compatible BACnet/SC TLS/WebSocket/certificate/hub behavior. BACnet/SC and MS/TP must be classified explicitly rather than conflated with BACnet/IP.

No production BACnet dependency/runtime/Data Source may be added.

## WHY THESE RESEARCH SPIKES ARE SAFE NOW

- each worker is restricted to research documents under its own `docs/research/**` domain;
- no Engineering schema, Program.cs, DI, DriverHost, frontend, workflow or package dependency changes are allowed;
- no worker may register a new Data Source or create an active protocol runtime;
- Draft PR + exact-head CI + coordinator review remain mandatory;
- after delivery each DEV returns to `WAIT_FOR_COORDINATOR`.

These spikes prepare future decisions while the active production priority remains the interface.

## PARKED / EXISTING RESEARCH

- `integration/interface-validation-preview`: **PARKED / NO PR / NOT MERGED**;
- PR #53 graphical visual editor architecture: **RESEARCH IN PR / DELIVERED**;
- PR #54 Client Python editor/browser sandbox: **RESEARCH IN PR / DELIVERED**;
- merged OPC UA research remains the reference style for discovery/import/security/acceptance research;
- merged Siemens S7 research remains the reference style for protocol/library/test-lab analysis.

## COORDINATOR RESUME POINT

Until the product owner sends the next coordinator `siga`, do not continue PR #58 implementation.

When `siga` arrives:

1. reread mandatory current-main docs;
2. verify live main, research PRs/heads/CI and PR #58 state;
3. review/merge any completed research PRs that are exact-head green and semantically sound;
4. keep those merges classified as **RESEARCH ONLY / PRODUCTION NOT IMPLEMENTED**;
5. then resume the coordinator interface integration from PR #58, reconciling with current main;
6. integrate `UserSessionMenu`, `EngineeringEntityBrowser` and `RuntimeOperationsOverview` centrally;
7. fix Chromium failure and run full CI before any #58 merge;
8. do not resume production protocol or provisional Windows package work unless priority changes.

## Permanent continuity rules

- GitHub branch/PR/head/CI state is operational truth.
- Open functional PRs are **IMPLEMENTED IN PR**, not MERGED.
- Research branches/PRs are architecture inputs, not implemented product functionality.
- Workers never choose their own next task or merge their own PR.
- Shared central integration belongs to the coordinator unless a narrow exception is explicitly assigned.
- Known-failing work is never merged.
- `siga` is the canonical short command; `continue` is equivalent.
