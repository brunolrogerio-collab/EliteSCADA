# LAST CHANGE — EliteSCADA

Date: 2026-09-01 (BRT)

## Read first

Stable product intent: [`PROJECT GOAL.md`](PROJECT%20GOAL.md)  
Operational handoff: [`docs/COORDINATOR-HANDOFF-2026-08-31.md`](docs/COORDINATOR-HANDOFF-2026-08-31.md)  
L3 release gate: [issue #180](https://github.com/brunolrogerio-collab/EliteSCADA/issues/180)

Live GitHub refs and exact-SHA Actions evidence override stale SHAs copied into prose.

## Mandatory continuity / chat replacement rule

The repository must contain enough context to resume EliteSCADA coordination safely in a new ChatGPT conversation without relying on the previous chat history.

For every material coordination cycle, decision, blocker, fix, validation run or change of next action:

1. review and synchronize both `PROJECT GOAL.md` and `LAST CHANGE.md`;
2. persist exact branch/SHA/run/issue evidence when it matters to acceptance;
3. never leave a critical decision, blocker, diagnosis or next action only in chat;
4. keep stable product/architecture intent in `PROJECT GOAL.md` and mutable operational state in `LAST CHANGE.md`;
5. when the user says `siga`, continue executing the active sequence until completion or a real external/blocking condition, while maintaining this repository checkpoint.

A new coordinator/chat must begin by reading these files and the active handoff before acting.

## Current checkpoint

- Driver convergence v3 is **MERGED** to `main` through PR #187. Last audited merge SHA: `f6210a1539741847aab8949a7e453c8cf141162d`.
- Active L3 branch: `coordination/driver-l3-seven-protocol-lab`.
- Technical HEAD before this documentation checkpoint: `0f18b080ee0a116c1fe8e93bad1aff1629a79e30`.
- Issue #180: **OPEN / ACTIVE / RELEASE GATE**.
- Wave 11: **BLOCKED** until the complete L3 matrix and normal CI pass on one exact accepted SHA and issue #180 is accepted and closed.
- Current validation: L3 run #21, run ID `33470564345`, exact technical SHA `0f18b080ee0a116c1fe8e93bad1aff1629a79e30`, was **IN PROGRESS** when this checkpoint was written.

## L3 topology / acceptance authority

The integrated runtime must operate all seven communication Drivers concurrently:

1. MQTT;
2. IEC-104;
3. CIP / EtherNet/IP;
4. OPC UA;
5. DNP3;
6. Siemens S7 ISO-on-TCP;
7. BACnet/IP.

The full acceptance contract, including heterogeneous TAG Gateway behavior, supported writes, serial peer fault/recovery, Gateway source/destination fault isolation, clean shutdown and exact-SHA CI evidence, is issue #180.

Do not weaken a 7/7 assertion or skip a failing slice to manufacture acceptance.

## L3 failure/fix history that matters for resume

### 1. BACnet/IP interface ambiguity during activation — RESOLVED

Original failure at SHA `65fbb6ee67040610eef4b6ef88073c38e127913b`, run `33434301171`, job `99626884954`:

`Cannot determine the BACnet/IP broadcast address automatically: found 2 candidate IPv4 interface(s) [...]`

Fix:

`c906f3cbbb3f0c584d19475c3dbdfbc6a84b5668` — `fix(bacnet): bind explicit local endpoint for L3`

The BACnet configuration now supports optional `localEndpointIp`; the L3 fixtures use loopback (`127.0.0.1`) while the previous automatic behavior remains available when the setting is omitted.

### 2. Communication diagnostics exposed only 6/7 Drivers — RESOLVED

The failure occurred before acquisition. The missing diagnostic entry was proven to be **IEC-104**: `Iec104HostCommunicationDriver` did not implement/expose the common `ICommunicationDiagnosticsSource` contract consumed by `EngineeringRuntimeCoordinator.Describe()`.

Diagnostic commit:

`a9a6fb55cb56659e382e7d09085f63505aee27f4` — `test: diagnose missing L3 communication driver`

Fix:

`dff9adcf068d62a55f1fa2cf98f693cee8686739` — `fix(iec104): expose common runtime diagnostics`

Do not reopen the old 6/7 theory unless new evidence independently reproduces it.

### 3. Acquisition slice inherited CIP state from Gateway slice — RESOLVED

After diagnostics were fixed, the seven-Driver acquisition slice observed CIP `Quality=Good` but value `2222` instead of initial `1234`. The preceding Gateway slice had legitimately written `2222` to the same stateful CIP peer.

This was test-slice contamination, not a CIP acquisition failure.

Evidence persistence:

`223aea71a240daca1343c71e3b4ea903b9b5ed00` — acquisition TRX persisted by CI.

Isolation fix:

`46cdc53cb2cb9f37d04386b8e6923e0f08bbe2cd` — `ci: reset L3 peers before acquisition slice`

Run #16 (`33465195861`) then proved:

- heterogeneous TAG Gateway slice: **PASS**;
- one-runtime seven-Driver acquisition slice: **PASS 7/7**;
- supported write slice: advanced to its own blocker.

### 4. BACnet write fixture still hit interface ambiguity — RESOLVED

The write/fault tests had their own BACnet datasource fixtures. They were aligned to loopback:

- `6aacd8aeaf2a94cc67e1fee99611c1eb358c046c` — `test(l3): bind BACnet write fixture to loopback`;
- `c1ca151e1d65b06d50f163a240e4f132c676343c` — `test(l3): bind BACnet fault fixture to loopback`.

CI diagnostics were also expanded so write/fault TRX evidence survives failures:

`7f592424e4d33dcfbb5dff190ec1c3d2c1b32449` — `ci: persist L3 write and fault diagnostics`.

### 5. BACnet analog Present_Value write encoded the wrong application datatype — FIXED / VALIDATING

Run #19:

- run ID `33468204672`;
- job `99732361456`;
- exact SHA `c1ca151e1d65b06d50f163a240e4f132c676343c`;
- Gateway slice: **PASS**;
- acquisition slice: **PASS**;
- supported write slice: **FAIL**.

Persisted write artifact:

`l3-supported-write-test-results-c1ca151e1d65b06d50f163a240e4f132c676343c`, artifact ID `9785713648`.

Exact error:

`System.IO.IOException: Reject from device, reason: INVALID_TAG`

The failure happened during `runtime.WriteAsync(bacnetId, 32.5d)` for BACnet `analogValue:1`, property `Present_Value` (85).

Root cause is production-code datatype encoding, not endpoint selection:

- the independent bacpypes peer exposes `AnalogValueObject.presentValue` as BACnet **REAL**;
- `BacnetValueCodec` converted incoming BACnet REAL correctly to canonical EliteSCADA `TagDataType.Double` on reads;
- but on writes the codec encoded every EliteSCADA `Double` as BACnet **DOUBLE**;
- the device therefore rejected the application datatype with `INVALID_TAG`.

Production fix:

`7fe8c58409d38a5d1750e9b16e06041f32718598` — `fix(bacnet): encode analog present value as REAL`

Behavior after the fix:

- numeric writes to `AnalogInput`, `AnalogOutput` or `AnalogValue` `Present_Value` are encoded as `BACNET_APPLICATION_TAG_REAL` with CLR `float` payload;
- generic EliteSCADA `Double` writes to non-analog BACnet properties retain BACnet `DOUBLE` behavior.

Regression coverage:

`0f18b080ee0a116c1fe8e93bad1aff1629a79e30` — `test(bacnet): cover REAL analog present value writes`

The coordinator convergence test now explicitly asserts BACnet `REAL` and CLR `float` for the analog Present_Value write while canonical cache values remain EliteSCADA `double`.

Issue #180 contains the same evidence and remains open.

## Exact execution order from here

1. Inspect L3 run #21 (`33470564345`) on exact SHA `0f18b080ee0a116c1fe8e93bad1aff1629a79e30`.
2. Confirm build and supported write slice after the BACnet REAL fix.
3. If write fails, use the persisted write TRX and fix the exact new error. Do not return to the already-resolved two-IP or 6/7 diagnoses without fresh evidence.
4. If write passes, continue through `Run L3 serial peer fault and recovery slice` and then `Run L3 Gateway source and destination fault recovery slice`.
5. Use the persisted fault TRX artifacts for any failure and continue fixing the gate.
6. Synchronize `PROJECT GOAL.md`, `LAST CHANGE.md`, issue #180 and relevant L3 status/handoff docs after every material checkpoint.
7. Once the full L3 workflow is green, ensure the documentation checkpoint itself is included in the exact SHA that receives final L3 validation.
8. Confirm normal EliteSCADA CI and required licensing/acceptance checks on the same final SHA as required by #180.
9. Close #180 only after the complete acceptance matrix is green and evidence is recorded.
10. Only after #180 is accepted and closed may Wave 11 begin.

## IMPLEMENTED ON ACTIVE L3 BRANCH

- dedicated seven-Driver L3 workflow;
- one-runtime integrated seven-Driver laboratory test harness;
- heterogeneous TAG Gateway L3 slice;
- deterministic BACnet/IP loopback binding for CI fixtures;
- IEC-104 common runtime diagnostics;
- slice isolation/reset between stateful tests;
- persistent TRX evidence for acquisition, write and fault slices;
- protocol-correct BACnet REAL encoding for analog Present_Value writes.

These items are not L3 acceptance by themselves. The release gate remains the complete exact-SHA workflow and issue #180.

## SPECIFIED / NOT YET ACCEPTED

- complete L3 seven-Driver integrated acceptance: **NOT YET ACCEPTED**;
- issue #180 closure: **NOT YET**;
- Wave 11 release: **NOT AUTHORIZED**.

## Resume instruction for a new chat/coordinator

Read, in order:

1. `PROJECT GOAL.md`;
2. `LAST CHANGE.md`;
3. `docs/COORDINATOR-HANDOFF-2026-08-31.md` and/or the current coordinator handoff;
4. issue #180 including its latest comments;
5. `.github/workflows/l3-seven-driver-lab.yml`;
6. the latest L3 Actions run on `coordination/driver-l3-seven-protocol-lab`.

Then re-fetch the live branch HEAD and target file SHAs before any write. Continue the current L3 blocker until the gate is fully green. Do **not** start Wave 11 from documentation assumptions or chat memory.
