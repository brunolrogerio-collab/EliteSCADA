# DRIVER AND INTEROPERABILITY LAB STATUS — EliteSCADA

Last evidence update: **2026-09-01 BRT**  
Scope: **DRIVER / INTEROPERABILITY EVIDENCE**

Live GitHub refs and exact-SHA Actions evidence override historical prose.

## Evidence levels

- **L0** — unit, codec and contract tests.
- **L1** — same-stack / in-process / loopback integration.
- **L2** — EliteSCADA Driver against an independent software peer over the real wire protocol.
- **L3** — one EliteSCADA build/runtime operating all seven converged Drivers concurrently against independent laboratory peers, including heterogeneous TAG Gateway and fault/recovery evidence.
- **L4** — physical hardware/site evaluation using a Preview build, accepted per actual device/model/firmware by the Development Lead.

## L2 — independent product-path laboratory

Status: **7/7 PASS / ACCEPTED**.

| Driver | Independent peer/evidence | L2 |
| --- | --- | --- |
| MQTT | Eclipse Mosquitto / HiveMQ | **PASS** |
| IEC-104 | lib60870-C | **PASS 13/13** |
| CIP / EtherNet/IP | independent Logix/CIP peer | **PASS** |
| OPC UA | open62541 | **PASS** |
| DNP3 | dnp3py | **PASS** |
| Siemens S7 ISO-on-TCP | python-snap7 | **PASS** |
| BACnet/IP | BACpypes | **PASS** |

## L3 — integrated seven-Driver laboratory

Status: **PASS / ACCEPTED / INTEGRATED**.

The accepted topology operated these seven communication Data Sources concurrently:

1. MQTT;
2. IEC-104;
3. CIP / EtherNet/IP;
4. OPC UA;
5. DNP3;
6. Siemens S7 ISO-on-TCP;
7. BACnet/IP.

### Acceptance evidence

First complete technical L3 proof:

- SHA `958bc9aa2bbaf788d9a15c19d986ba728a7562fd`;
- L3 Seven-Driver Lab #23, run `33478345659`: **SUCCESS**.

Final stabilization/exact-head proof before main integration:

- SHA `02b7408e68b81355de6a56dc0267c9e28c0c74bf`;
- EliteSCADA CI #1023, run `33510090342`: **SUCCESS**;
- Preview Licensing CI #80, run `33510090333`: **SUCCESS**;
- L3 Seven-Driver Lab #30, run `33510090344`: **SUCCESS**.

Main integration:

- PR #190 merged;
- main code SHA `9b963c40f013f115b9787049cdb90949a30cbcbc`;
- EliteSCADA CI #1024, run `33510855124`: **SUCCESS**;
- Preview Licensing CI #81, run `33510855126`: **SUCCESS**.

### Matrix proven

The accepted L3 sequence includes:

- seven-peer startup and endpoint verification;
- common runtime activation/readiness for all seven Drivers;
- deterministic concurrent acquisition 7/7 through the canonical TAG path;
- supported writes/commands;
- heterogeneous TAG Gateway transfer through canonical cache/event boundaries;
- serial peer fault isolation and recovery;
- Gateway source/destination fault and recovery;
- no Driver-to-Driver coupling introduced;
- persistent acceptance evidence artifacts;
- clean seven-peer/runtime shutdown;
- no assertion weakening to manufacture green evidence.

Resolved L3 defects remain historical evidence and must not be reopened without fresh failure evidence. These included BACnet loopback binding, IEC-104 common diagnostics, cross-slice CIP peer state contamination, BACnet REAL analog writes, commandable BACnet priority semantics and MQTT recovery stimulus handling.

## L3 release state

Issue #180 may be closed as completed. Driver convergence issue #174 may also close because its required main integration, post-main CI and L3 acceptance are complete.

L3 therefore no longer blocks Wave 11. However, on 2026-09-01 the Development Lead explicitly introduced an additional **pre-Wave-11 task**. Its scope is still to be supplied, so Wave 11 remains intentionally **NOT STARTED** until that task is recorded and completed.

## L4 — physical hardware/site validation

Status: **DEFERRED UNTIL A PREVIEW BUILD EXISTS**.

L4 is separate from L3 and is device-specific. It should record exact Preview build, Driver, manufacturer, model, firmware/software revision, topology/settings, reads, writes/commands, reconnect scenarios, observed quality/timestamps/diagnostics and final result.

A PASS on one physical model must not be generalized to every device implementing the protocol.

## Claim discipline

- Normal CI green alone is not L2 or L3.
- Seven independent L2 results alone are not L3.
- L3 does not imply physical hardware validation.
- L4 requires real hardware/site evidence.
- Licensing, conformance/certification and vendor breadth remain separate evidence claims.