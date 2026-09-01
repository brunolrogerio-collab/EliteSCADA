# LAST CHANGE — EliteSCADA

Date: 2026-09-01 (BRT)

## Read first

Stable product intent: [`PROJECT GOAL.md`](PROJECT%20GOAL.md)  
Operational handoff: [`docs/COORDINATOR-HANDOFF-2026-08-31.md`](docs/COORDINATOR-HANDOFF-2026-08-31.md)  
L3 release gate: [issue #180](https://github.com/brunolrogerio-collab/EliteSCADA/issues/180)

Live GitHub refs and exact-SHA Actions evidence override stale SHAs copied into prose.

## Mandatory continuity / chat replacement rule

The repository must contain enough context to resume EliteSCADA coordination safely in a new ChatGPT conversation without relying on previous chat history.

For every material coordination cycle, decision, blocker, fix, validation run or change of next action:

1. review and synchronize both `PROJECT GOAL.md` and `LAST CHANGE.md` when their recorded state is affected;
2. persist exact branch/SHA/run/issue evidence when it matters to acceptance;
3. never leave a critical decision, blocker, diagnosis or next action only in chat;
4. keep stable product/architecture intent in `PROJECT GOAL.md` and mutable operational state here;
5. when the user says `siga`, continue executing until completion or a real external blocker.

## Current checkpoint

- Active branch: `coordination/driver-l3-seven-protocol-lab`.
- Issue #180: **OPEN / ACTIVE / RELEASE GATE**.
- Wave 11: **BLOCKED** until complete L3 plus normal required CI pass on one exact accepted SHA and #180 is accepted and closed.
- Latest technical commit before this documentation checkpoint: `da86a93016bad9dfae29587bd556aac8007646f4` — `test(l3): make BACnet analog peer commandable`.
- Current validation: L3 run #22, run ID `33471176978`, exact technical SHA `da86a93016bad9dfae29587bd556aac8007646f4`, was **IN PROGRESS** when this checkpoint was written.
- The serial fault fixture was audited while #22 was starting. Its BACnet datasource already binds `localEndpointIp=127.0.0.1`, targets `127.0.0.1:47808`, and exercises BACnet restart/recovery by readback rather than by weakening the write contract.

## L3 topology / acceptance authority

The integrated runtime must operate all seven communication Drivers concurrently:

1. MQTT;
2. IEC-104;
3. CIP / EtherNet/IP;
4. OPC UA;
5. DNP3;
6. Siemens S7 ISO-on-TCP;
7. BACnet/IP.

Issue #180 is the authoritative acceptance contract. The complete matrix includes concurrent acquisition, supported writes, heterogeneous TAG Gateway behavior, serial peer fault/recovery, Gateway source/destination fault isolation, clean shutdown and exact-SHA CI evidence.

Do not weaken the seven-driver assertion or skip a failing slice to manufacture acceptance.

## L3 failure/fix history that matters for resume

### 1. BACnet/IP interface ambiguity during activation — RESOLVED

Original failure at SHA `65fbb6ee67040610eef4b6ef88073c38e127913b`, run `33434301171`, job `99626884954`:

`Cannot determine the BACnet/IP broadcast address automatically: found 2 candidate IPv4 interface(s) [...]`

Fix:

`c906f3cbbb3f0c584d19475c3dbdfbc6a84b5668` — `fix(bacnet): bind explicit local endpoint for L3`

L3 BACnet fixtures use loopback while automatic interface selection remains available outside explicitly bound configurations.

### 2. Communication diagnostics exposed only 6/7 Drivers — RESOLVED

The missing diagnostics entry was IEC-104. `Iec104HostCommunicationDriver` did not expose the common `ICommunicationDiagnosticsSource` contract consumed by `EngineeringRuntimeCoordinator.Describe()`.

- diagnostic: `a9a6fb55cb56659e382e7d09085f63505aee27f4` — `test: diagnose missing L3 communication driver`;
- fix: `dff9adcf068d62a55f1fa2cf98f693cee8686739` — `fix(iec104): expose common runtime diagnostics`.

Do not reopen the old 6/7 theory without fresh evidence.

### 3. Acquisition inherited CIP state from the Gateway slice — RESOLVED

The Gateway slice legitimately wrote CIP value `2222`; acquisition then expected the fresh-peer value `1234`. This was slice contamination, not an acquisition defect.

- evidence persistence: `223aea71a240daca1343c71e3b4ea903b9b5ed00`;
- isolation fix: `46cdc53cb2cb9f37d04386b8e6923e0f08bbe2cd` — `ci: reset L3 peers before acquisition slice`.

Run #16 (`33465195861`) proved Gateway **PASS** and acquisition **PASS 7/7**.

### 4. BACnet write/fault fixtures and persistent evidence — RESOLVED

- `7f592424e4d33dcfbb5dff190ec1c3d2c1b32449` — persist write/fault TRX evidence;
- `6aacd8aeaf2a94cc67e1fee99611c1eb358c046c` — bind BACnet write fixture to loopback;
- `c1ca151e1d65b06d50f163a240e4f132c676343c` — bind BACnet fault fixture to loopback.

### 5. BACnet analog Present_Value encoded as DOUBLE — RESOLVED

Run #19 (`33468204672`, job `99732361456`, SHA `c1ca151e1d65b06d50f163a240e4f132c676343c`) reached the real BACnet write and failed:

`System.IO.IOException: Reject from device, reason: INVALID_TAG`

The independent bacpypes peer exposes `AnalogValueObject.presentValue` as BACnet `REAL`. EliteSCADA reads converted REAL correctly to canonical `TagDataType.Double`, but write encoding sent BACnet `DOUBLE`.

Fixes:

- `7fe8c58409d38a5d1750e9b16e06041f32718598` — `fix(bacnet): encode analog present value as REAL`;
- `0f18b080ee0a116c1fe8e93bad1aff1629a79e30` — `test(bacnet): cover REAL analog present value writes`.

The codec now emits BACnet REAL/CLR `float` for numeric writes to AnalogInput/AnalogOutput/AnalogValue `Present_Value`, while generic non-analog EliteSCADA Double writes retain BACnet DOUBLE encoding.

### 6. BACnet peer rejected valid priority write because fixture was not commandable — FIXED / VALIDATING

Run #21 (`33470564345`, job `99739046939`, exact SHA `0f18b080ee0a116c1fe8e93bad1aff1629a79e30`) proved the datatype fix advanced the protocol exchange:

- Gateway: **PASS**;
- acquisition: **PASS 7/7**;
- supported write: **FAIL**;
- later fault slices: skipped because write gate failed.

Persisted write TRX changed from `INVALID_TAG` to:

`System.IO.IOException: Error from device, class: ERROR_CLASS_PROPERTY, code: ERROR_CODE_WRITE_ACCESS_DENIED`

This occurred at the same BACnet write using priority 8. The new error proved the device now accepted the BACnet REAL datatype but the lab object did not support command-priority semantics.

Root cause was the independent peer fixture, not the production driver: `interop-lab/bacnet/bacpypes/server.py` used plain `AnalogValueObject`, which is readable but is not a commandable priority-array object in BACpypes 0.19.

Fix:

`da86a93016bad9dfae29587bd556aac8007646f4` — `test(l3): make BACnet analog peer commandable`

The peer now registers a concrete `LabAnalogValueObject : AnalogValueCmdObject`, which uses BACpypes `Commandable(Real)` semantics, and declares `relinquishDefault=21.5`. L3 continues to exercise priority 8. The driver contract was **not weakened** to accommodate the fixture.

L3 run #22 (`33471176978`) is validating this exact technical SHA.

## Exact execution order from here

1. Inspect L3 run #22 (`33471176978`) on SHA `da86a93016bad9dfae29587bd556aac8007646f4`.
2. Confirm Gateway and acquisition remain green and determine whether the supported write slice passes with the commandable BACnet peer.
3. If write fails, download its persisted TRX and fix the exact new error.
4. If write passes, continue the serial peer fault/recovery slice and then the Gateway source/destination fault/recovery slice.
5. Use persisted TRX artifacts for every failing slice. Do not regress to old two-IP, 6/7, INVALID_TAG or WRITE_ACCESS_DENIED diagnoses without new evidence.
6. Synchronize `PROJECT GOAL.md`, this file, issue #180 and `docs/DRIVER-AND-INTEROP-LAB-STATUS.md` after material checkpoints.
7. After the complete L3 matrix first becomes green, create/update the final documentation/status checkpoint so the **documentation-inclusive HEAD** itself receives exact-SHA L3 validation.
8. Confirm normal EliteSCADA CI and all checks required by #180 on that same final SHA.
9. Close #180 only after complete acceptance evidence is recorded.
10. Only then may Wave 11 begin.

## IMPLEMENTED ON ACTIVE L3 BRANCH

- dedicated seven-Driver L3 workflow;
- one-runtime integrated seven-Driver lab;
- heterogeneous TAG Gateway slice;
- deterministic BACnet/IP loopback binding;
- IEC-104 common runtime diagnostics;
- stateful peer reset/isolation between slices;
- persistent TRX evidence for acquisition/write/fault slices;
- protocol-correct BACnet REAL encoding for analog Present_Value writes;
- commandable BACpypes AnalogValue peer for priority-write validation.

These are not acceptance by themselves. The gate is the complete exact-SHA workflow plus issue #180.

## SPECIFIED / NOT YET ACCEPTED

- complete L3 seven-Driver integrated acceptance: **NOT YET ACCEPTED**;
- issue #180 closure: **NOT YET**;
- Wave 11 release: **NOT AUTHORIZED**.

## Resume instruction for a new chat/coordinator

Read, in order:

1. `PROJECT GOAL.md`;
2. `LAST CHANGE.md`;
3. current coordinator handoff;
4. issue #180 and latest comments;
5. `.github/workflows/l3-seven-driver-lab.yml`;
6. latest L3 Actions run on the coordination branch.

Then re-fetch live branch HEAD and target file SHAs before any write. Continue the current L3 blocker until the gate is fully green. Do **not** start Wave 11 from documentation assumptions or chat memory.
