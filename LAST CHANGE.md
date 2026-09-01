# LAST CHANGE — EliteSCADA

Date: 2026-09-01 (BRT)

## Read first

Stable product intent: [`PROJECT GOAL.md`](PROJECT%20GOAL.md)  
Operational handoff: [`docs/COORDINATOR-HANDOFF-2026-08-31.md`](docs/COORDINATOR-HANDOFF-2026-08-31.md)  
L3 release gate: [issue #180](https://github.com/brunolrogerio-collab/EliteSCADA/issues/180)

Live GitHub refs and exact-SHA Actions evidence override stale SHAs copied into prose.

## Mandatory continuity / chat replacement rule

The repository, not chat history, is the persistent coordination memory. A fresh coordinator must be able to resume safely from repository state alone.

For every material coordination cycle, decision, blocker, fix, validation run or change of next action:

1. review `PROJECT GOAL.md` and `LAST CHANGE.md` before changing code;
2. persist exact branch/SHA/run/issue evidence when it matters to acceptance;
3. never leave a critical decision, blocker, diagnosis or next action only in chat;
4. keep stable product/architecture intent in `PROJECT GOAL.md` and mutable operational state here;
5. when the Development Lead says `siga`, continue executing until completion or a real external blocker.

`PROJECT GOAL.md` was reviewed on 2026-09-01. Its stable release-sequencing rule remains correct: Wave 11 cannot begin until L3 is fully accepted and issue #180 is closed.

## Current checkpoint

- Active branch: `coordination/driver-l3-seven-protocol-lab`.
- Issue #180: **OPEN / RELEASE GATE**.
- Wave 11: **BLOCKED**.
- First complete green L3 technical SHA: `958bc9aa2bbaf788d9a15c19d986ba728a7562fd` — `test(l3): make MQTT recovery stimulus reconnect-safe`.
- L3 Seven-Driver Lab run #23: `33478345659`.
- L3 job: `99762245620`.
- Result on exact technical SHA `958bc9aa...`: **SUCCESS**.

Run #23 passed the complete dedicated L3 sequence:

- seven peer startup / endpoint verification: PASS;
- heterogeneous TAG Gateway slice: PASS;
- one-runtime seven-Driver acquisition slice: PASS 7/7;
- supported write slice: PASS;
- serial peer fault/recovery slice: PASS;
- Gateway source/destination fault/recovery slice: PASS;
- evidence uploads: PASS;
- clean lab shutdown: PASS.

This is the first complete technical proof of the integrated L3 matrix. It does **not** by itself release Wave 11 because issue #180 also requires the final documentation-inclusive accepted SHA and normal EliteSCADA CI on that same accepted SHA.

This documentation checkpoint changes the branch HEAD after `958bc9aa...`; therefore the new documentation-inclusive HEAD must receive its own exact-SHA validation before #180 closes.

At the time this checkpoint was prepared, querying Actions by technical SHA `958bc9aa...` returned only the dedicated L3 workflow. Normal EliteSCADA CI was not yet evidenced on that exact SHA.

## L3 topology / acceptance authority

Issue #180 remains authoritative. One runtime must operate all seven communication Drivers concurrently:

1. MQTT;
2. IEC-104;
3. CIP / EtherNet/IP;
4. OPC UA;
5. DNP3;
6. Siemens S7 ISO-on-TCP;
7. BACnet/IP.

Do not weaken assertions, reduce the seven-driver expectation, skip a failing slice or reinterpret a prior failure to manufacture acceptance.

## Resolved L3 blockers that must not be reopened without fresh evidence

### BACnet/IP multi-interface activation

Original runner failure: BACnet transport found multiple IPv4 candidates and refused automatic selection.

Fix: `c906f3cbbb3f0c584d19475c3dbdfbc6a84b5668` — `fix(bacnet): bind explicit local endpoint for L3`.

L3 loopback fixtures explicitly bind `localEndpointIp=127.0.0.1` while production auto-selection remains available when unset.

### Common runtime diagnostics exposed only 6/7 Drivers

Missing diagnostics Driver was IEC-104. `EngineeringRuntimeCoordinator.Describe()` only includes `ICommunicationDiagnosticsSource`; IEC-104 did not expose that common contract.

- diagnostic: `a9a6fb55cb56659e382e7d09085f63505aee27f4`;
- fix: `dff9adcf068d62a55f1fa2cf98f693cee8686739` — `fix(iec104): expose common runtime diagnostics`.

### Acquisition inherited CIP state from Gateway slice

Gateway legitimately wrote CIP `2222`; acquisition expected fresh-peer `1234`. This was cross-slice peer-state contamination, not acquisition failure.

- evidence persistence: `223aea71a240daca1343c71e3b4ea903b9b5ed00`;
- isolation fix: `46cdc53cb2cb9f37d04386b8e6923e0f08bbe2cd` — `ci: reset L3 peers before acquisition slice`.

Run #16 proved Gateway PASS and acquisition PASS 7/7 after isolation.

### Persistent TRX evidence for remaining slices

Write/fault failures were made recoverable through TRX artifacts rather than relying on the unreliable Actions job-log download path.

Key evidence/workflow commits include `7f592424e4d33dcfbb5dff190ec1c3d2c1b32449` and related write/fault evidence persistence changes.

### BACnet analog write datatype

BACnet Analog `Present_Value` is protocol REAL even when EliteSCADA canonical type is Double. Production write codec had emitted BACnet DOUBLE.

- fix: `7fe8c58409d38a5d1750e9b16e06041f32718598` — `fix(bacnet): encode analog present value as REAL`;
- test: `0f18b080ee0a116c1fe8e93bad1aff1629a79e30` — `test(bacnet): cover REAL analog present value writes`.

### BACnet peer command-priority fixture

The independent bacpypes peer initially used plain `AnalogValueObject`, causing `WRITE_ACCESS_DENIED` for valid priority-8 writes after the datatype fix.

Fix: `da86a93016bad9dfae29587bd556aac8007646f4` — `test(l3): make BACnet analog peer commandable`.

The peer uses BACpypes commandable Real semantics. The production write capability and acceptance requirement were not weakened.

### MQTT recovery stimulus

The final technical change before the first complete green L3 run was:

`958bc9aa2bbaf788d9a15c19d986ba728a7562fd` — `test(l3): make MQTT recovery stimulus reconnect-safe`.

That exact SHA produced L3 run #23 SUCCESS.

## Exact execution order for the next coordinator

1. Re-fetch live branch HEAD before any write. Do not assume the SHA recorded here is still HEAD.
2. Inspect issue #180 and the latest Actions runs.
3. Treat L3 run #23 (`33478345659`) on `958bc9aa...` as the first complete technical L3 proof.
4. Validate the current **documentation-inclusive branch HEAD** with the complete `L3 Seven-Driver Lab` workflow.
5. Run/obtain normal EliteSCADA CI on that **same exact SHA** and require all checks mandated by #180 to pass.
6. If the documentation-inclusive L3 or normal CI fails, fix the exact new failure and repeat exact-SHA validation. Do not regress to resolved BACnet two-IP, IEC-104 6/7, CIP contamination, INVALID_TAG or WRITE_ACCESS_DENIED diagnoses without fresh evidence.
7. Synchronize `PROJECT GOAL.md`, this file, issue #180 and `docs/DRIVER-AND-INTEROP-LAB-STATUS.md` with final evidence.
8. Close issue #180 only after all acceptance evidence is recorded on the accepted exact SHA.
9. Only after #180 is closed may Wave 11 begin.

## IMPLEMENTED ON ACTIVE L3 BRANCH

- dedicated seven-Driver L3 workflow;
- one-runtime concurrent seven-Driver lab;
- heterogeneous TAG Gateway validation;
- deterministic BACnet/IP loopback binding for L3 fixtures;
- IEC-104 common runtime diagnostics;
- peer state isolation between slices;
- persistent acquisition/write/fault TRX evidence;
- protocol-correct BACnet REAL analog writes;
- commandable BACpypes analog peer;
- serial peer fault/recovery coverage;
- Gateway source/destination fault/recovery coverage;
- reconnect-safe MQTT recovery stimulus.

## ACCEPTANCE STATE

- complete technical L3 matrix on `958bc9aa...`: **PASS — run #23**;
- documentation-inclusive final HEAD exact-SHA L3: **PENDING**;
- normal EliteSCADA CI on the same final accepted SHA: **PENDING / NOT YET EVIDENCED**;
- issue #180 closure: **NOT YET**;
- Wave 11 release: **NOT AUTHORIZED**.

## Resume instruction for a new chat/coordinator

Read, in order:

1. `PROJECT GOAL.md`;
2. `LAST CHANGE.md`;
3. `docs/COORDINATOR-HANDOFF-2026-08-31.md` and any newer current handoff;
4. issue #180 and latest comments;
5. `.github/workflows/l3-seven-driver-lab.yml`;
6. latest Actions runs on `coordination/driver-l3-seven-protocol-lab`.

Then re-fetch live branch HEAD and target file SHAs before any mutation. Repository/CI state wins over stale chat memory.