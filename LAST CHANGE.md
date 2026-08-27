# LAST CHANGE — EliteSCADA

> Operational handoff. Read with `PROJECT GOAL.md`, `docs/ROADMAP.md`, `docs/PARALLEL-WORK.md`, `docs/CHAT-WORK-ASSIGNMENTS.md` and the current task-specific documents before every EliteSCADA action.

**Handoff date:** 2026-08-27  
**Development state:** **FIRST INTERFACE PRODUCT CHECKPOINT MERGED / INTERFACE DEVELOPMENT REMAINS ACTIVE / FUTURE PROTOCOL RESEARCH ACTIVE**

Repository truth remains separated into **MERGED**, **IMPLEMENTED IN PR**, **RESEARCH IN PR** and **SPECIFIED / NOT IMPLEMENTED**.

## CURRENT CHECKPOINT

Official `main` includes:

- Internal Memory: **MERGED / COMPLETE** through PR #49;
- TAG Gateway: **MERGED / COMPLETE** through PRs #50 and #55;
- common multi-driver/Data Source diagnostics: **MERGED / COMPLETE** through PRs #56 and #57;
- canonical Engineering: **Schema v9**;
- first Interface Product Development checkpoint: **MERGED** through PR #58;
- production external protocol baseline remains Modbus TCP plus built-in Simulation/Internal Memory.

Active product order remains:

`merged platform foundations -> interface product development -> user validation build/package -> additional external drivers/protocols`

The Windows x64 validation package remains deferred until the interface is materially ready for product-owner validation. Production MQTT/OPC UA/BACnet/S7/Allen-Bradley/Driver Module implementation remains postponed. Research spikes are allowed only as research/specification inputs.

## FIRST INTERFACE PRODUCT CHECKPOINT — MERGED

Worker primitives merged before central integration:

- DEV 3 / PR #59 `UserSessionMenu`: merge `b0b58964f119f83356cf2edc8fecf5939fb905da`, CI #363 green;
- DEV 1 / PR #60 `EngineeringEntityBrowser`: merge `a7e6105fb65079ad1af8bcb56f8484225ff3dc8c`, CI #359 green;
- DEV 2 / PR #61 `RuntimeOperationsOverview`: merge `49c9e7261d63047b601f4b3c4f6e788168c8ee5c`, CI #360 green.

Coordinator PR #58 was reconciled with current `main`, integrated those primitives centrally and is now **MERGED**.

- feature head merged: `af98359c41a432ea34635c10024cf459c453d1eb`;
- exact-head CI #378 (`33097585182`): Web **PASS**, backend build/tests/runtime smoke **PASS**, Chromium E2E **PASS**;
- merge SHA: `f3cc82f0d45a9f0162105b57ae6c42f643af6160`.

Merged interface behavior now includes:

- persistent localized EliteSCADA product shell across Runtime / Engineering / Audit;
- authenticated user/session menu in the common shell;
- Runtime operational overview using existing protected runtime/diagnostic facts while preserving the process demo;
- Engineering Data Source and TAG entity-browser/search surfaces using the canonical Engineering model;
- existing protected structured Preview/Apply/CAS mutation flows preserved;
- Chromium coverage of the integrated shell and entity-browser path.

The earlier Chromium failures were not merged. CI #374 isolated the last failure to an ambiguous new test locator; it was scoped correctly and the final exact head passed CI #378 completely.

## PARALLEL FUTURE-PROTOCOL RESEARCH

These assignments remain **RESEARCH ONLY / PRODUCTION NOT IMPLEMENTED**:

### DEV 1 — MQTT

Branch: `research/mqtt-industrial-driver`  
Expected deliverable: `docs/research/mqtt/MQTT-INDUSTRIAL-DRIVER-RESEARCH.md`

Research must cover MQTT 3.1.1/5.0, sessions/reconnect, QoS/duplicates/order, retained/LWT, TLS/mTLS and secret references, Topic↔TAG/payload mapping, writable publish semantics, honest discovery/import, common diagnostics, multi-broker behavior, candidate libraries/test brokers and strict raw-MQTT versus Sparkplug B separation. No production MQTT dependency/runtime/Data Source may be added.

### DEV 2 — Allen-Bradley EtherNet/IP/CIP Logix

Branch: `research/allen-bradley-ethernet-ip`  
Expected deliverable: `docs/research/allen-bradley/ALLEN-BRADLEY-ETHERNET-IP-CIP-RESEARCH.md`

Research must cover ControlLogix/CompactLogix-first scope, explicit CIP messaging versus implicit I/O, routing/slot paths, symbolic TAGs, arrays/UDTs/types, External Access/write safety, browse/import and L5X/L5K, batching/fragmentation/limits, CIP Security implications, library/license comparison and real-hardware acceptance. No production Allen-Bradley runtime/Data Source may be added.

### DEV 3 — BACnet/IP + BACnet/SC

Branch: `research/bacnet-ip-secure-connect`  
Expected deliverable: `docs/research/bacnet/BACNET-IP-SC-RESEARCH.md`

Research must cover BACnet/IP discovery, device/object/property identity, BBMD/Foreign Device/cross-subnet behavior, ReadProperty/ReadPropertyMultiple/WriteProperty, COV/polling, segmentation/APDU limits, status/reliability/unit semantics, write priority/relinquish, proprietary objects, common diagnostics and forward-compatible BACnet/SC TLS/WebSocket/certificate/hub behavior. BACnet/SC and MS/TP must remain explicitly classified. No production BACnet runtime/Data Source may be added.

At this handoff the three research branches had no delivered research PR yet. GitHub state wins if that changes before the next coordinator action.

## PARKED / EXISTING RESEARCH

- `integration/interface-validation-preview`: **PARKED / NO PR / NOT MERGED**;
- PR #53 graphical visual editor architecture: **RESEARCH IN PR / DELIVERED / PRODUCTION NOT IMPLEMENTED**;
- PR #54 Client Python editor/browser sandbox: **RESEARCH IN PR / DELIVERED / PRODUCTION NOT IMPLEMENTED**;
- merged OPC UA and Siemens S7 research remain architecture inputs only.

## COORDINATOR RESUME POINT

On the next coordinator `siga`:

1. reread all mandatory current-main documents and coordinator `MustReadSpecific` files;
2. verify live `main`, open PRs, research branch heads and exact-head CI;
3. review/merge any completed research PRs only if their scope is research-only, semantically sound and exact-head green;
4. keep research merges classified as **RESEARCH ONLY / PRODUCTION NOT IMPLEMENTED**;
5. verify whether post-merge CI on `main` exists for merge `f3cc82f0d45a9f0162105b57ae6c42f643af6160` and investigate any failure before further interface work;
6. continue Interface Product Development according to `docs/INTERFACE-DEVELOPMENT.md` and roadmap dependencies; do not invent production protocol work or resume the Windows package ahead of the product gate;
7. if no explicit next interface slice has been recorded yet, coordinate from the current roadmap and repository facts without undoing the merged first checkpoint.

## Permanent continuity rules

- GitHub branch/PR/head/CI state is operational truth.
- Open functional PRs are **IMPLEMENTED IN PR**, not MERGED.
- Research branches/PRs are architecture inputs, not product functionality.
- Workers never choose their own next task or merge their own PR.
- Shared central integration belongs to the coordinator unless explicitly delegated.
- Known-failing work is never merged.
- `siga` is the canonical short command; `continue` is equivalent.
