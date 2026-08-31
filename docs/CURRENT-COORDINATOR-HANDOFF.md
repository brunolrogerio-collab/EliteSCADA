# EliteSCADA — Current Coordinator Handoff

Last operational audit: **2026-08-31 BRT**  
Operational status: **DRIVER CONVERGENCE ACTIVE / PR #175 DRAFT / DO NOT MERGE**

> **THIS FILE IS THE SINGLE OPERATIONAL HANDOFF FOR COORDINATOR CONTINUITY.**
>
> If an older status table, worker PR description, assignment note or historical SHA conflicts with this file, re-read the live GitHub branch/PR/Actions state and use the live evidence. Architecture semantics remain governed by the ADRs and `DRIVER-CONVERGENCE-COORDINATION-V1.md`; current operational facts and next actions are recorded here.

## 1. Resume protocol for a new Coordinator

Do not reconstruct project state by reading every historical document.

Read in this order:

1. live PR **#175** metadata and current `coordination/driver-convergence-v3` head;
2. this file;
3. live issue **#174**;
4. GitHub Actions for the exact current code head;
5. only then inspect the worker branch/PR for the **next** Driver being converged or repaired.

Rules for conflicting evidence:

- live GitHub ref and exact-head Actions evidence beat a SHA copied into prose;
- this file owns **operational status, current blockers and next action**;
- `DRIVER-CONVERGENCE-COORDINATION-V1.md` owns shared architectural intent, not current heads;
- `DRIVER-AND-INTEROP-LAB-STATUS.md` owns laboratory evidence, not coordinator implementation progress;
- `PARALLEL-DRIVER-WORK-ASSIGNMENTS.md` is a historical authorization/ownership record, not current product status;
- worker PR descriptions are handoff snapshots and may lag their live branches;
- never inherit a green CI from an older SHA.

Status vocabulary:

- **MERGED** — present on `main`;
- **IMPLEMENTED IN PR** — implemented on the coordinator/worker line but not merged to `main`;
- **SPECIFIED / NOT IMPLEMENTED** — architecture or requirement exists, production code does not yet satisfy it.

## 2. Current shared integration line

- Repository: `brunolrogerio-collab/EliteSCADA`
- Coordinator branch: `coordination/driver-convergence-v3`
- Draft PR: **#175** — `Driver convergence v3 — shared host contracts`
- Last code-validated coordinator head: **`4178ced24279a29f64fee1079c16b8fa71803edc`**
- Exact-head validation: **EliteSCADA CI #918 — SUCCESS**
- PR state: **DRAFT / OPEN / DO NOT MERGE**

CI #918 evidence on `4178ced...`:

- Release backend build: **SUCCESS**, 0 warnings, 0 errors;
- `Scada.Core.Tests`: **243 passed**;
- `Scada.Drivers.Tests`: **91 passed**;
- `Scada.Historian.TimescaleDb.Tests`: **23 passed**;
- `Scada.Security.Tests`: **27 passed**;
- `Scada.Persistence.PostgreSql.Tests`: **107 passed**;
- total backend tests: **491 passed / 0 failed**;
- runtime smoke: **SUCCESS**;
- Web React/Vite build: **SUCCESS**;
- Chromium end-to-end: **SUCCESS**.

Documentation-only commits after that SHA do not inherit a new code-validation claim. Re-read the current branch before any mutation and distinguish the live documentation head from the last code-validated head above.

## 3. Engineering schema v15

Status: **IMPLEMENTED IN PR / GATE CLOSED**.

Completed and validated:

- `EngineeringExchangeService.CurrentSchemaVersion = 15`;
- `CommunicationBinding` validation from TAG Preview;
- binding preserved through Apply/runtime registry/export;
- compatibility invariant `Address == CommunicationBinding.PortableAddress` when both exist;
- plaintext secret-like binding settings fail closed;
- malformed/unsupported binding contracts fail closed;
- schema <= v14 remains readable, while rich binding in v14 is rejected;
- TAG CSV v15 uses one generic `CommunicationBindingJson` column;
- legacy TAG CSV without the column remains readable;
- JSON Preview -> Apply -> Export round-trip;
- CSV Preview -> Apply -> Export round-trip;
- `.escadapkg` round-trip;
- immutable Engineering revision round-trip;
- PostgreSQL Engineering revision round-trip.

Authoritative closure evidence began at CI #905 and remains covered by the later green coordinator CI #918.

## 4. MQTT coordinator convergence

Driver type: `mqtt.raw`  
Worker branch: `driver10/mqtt`  
Worker PR: #128  
Audited worker head: `acd46cd9a4a49e324f2037a1994e6f579a0bae3f`  
Worker CI evidence: #865 GREEN  
Independent product-path L2: **ACCEPTED**

Coordinator status: **IMPLEMENTED IN PR / FINAL MQTT EXIT GATE STILL OPEN**.

Implemented on PR #175:

- audited MQTT protocol primitives and MQTTnet transport;
- `MqttDriver` ported into the coordinator line;
- shared `ICommunicationDriverReadinessSource` bridge;
- `MqttCommunicationRuntimePlan`;
- `MqttCommunicationRuntimePlanner`;
- `MqttCommunicationRuntimeFactory`;
- schema-v15 `CommunicationBinding` as canonical rich TAG configuration;
- explicit backward-compatible Address/Metadata path with migration warning;
- generic `CommunicationPlans` compiler path;
- generic converged-driver activation in `EngineeringRuntimeCoordinator`;
- `CommunicationDriverRuntimeComposition.BuildForCurrentSchema()` registration;
- host-owned protected-material resolver composition;
- deterministic protected-material environment names bound to complete project/DataSource/Driver/purpose/reference scope;
- short-lived lease and buffer clearing behavior;
- coordinator activation can become Ready without waiting for the first MQTT telemetry sample;
- focused convergence/readiness/security/runtime activation tests green in CI #918.

### MQTT gate still required before starting IEC-104 convergence

One explicit end-to-end security-path regression is still missing:

`Engineering password reference -> coordinator composition -> host protected-material resolver -> MQTT factory -> MQTT transport credentials`

The existing coordinator activation test exercises MQTT activation without a password reference. Add the full reference-based activation test and run exact-head normal CI. Only after that exact head is fully green may MQTT be marked **CLOSED FOR COORDINATOR CONVERGENCE** and IEC-104 ingress begin.

Do not merge worker PR #128 wholesale. It is source/evidence; protocol code is re-ported/adapted through the shared contracts.

## 5. Driver laboratory status

Two claims must never be collapsed:

1. **common peer laboratory is healthy**;
2. **the EliteSCADA product Driver passed independent-software L2**.

Common seven-peer lab infrastructure: **7/7 healthy and MERGED** through PR #173.  
Validated lab functional head: `3ff2d6393c4e8734b4b1c08abd2bd8466f78f400`.  
Interop Lab Smoke #42: **SUCCESS**.  
Merge to `main`: `a08cca94795a5afa14bf8af39b8bf2c6f7df71ae`.  
Post-lab normal CI #886: **SUCCESS** after rerun of unrelated Modbus timing noise on unchanged functional code.

### Product-path L2 matrix

| Driver | Independent peer | Product L2 | Meaning |
| --- | --- | --- | --- |
| MQTT | Mosquitto/HiveMQ | **PASS / ACCEPTED** | Real broker path, QoS/TLS/auth/restart/freshness evidence accepted. |
| IEC-104 | lib60870-C | **PASS / ACCEPTED (13/13)** | STARTDT/GI/spontaneous data/commands/restart/no-replay accepted. |
| CIP / EtherNet/IP | independent simulator profile | **PASS / ACCEPTED** | RegisterSession/SendRRData/typed read/write-readback/polling accepted. |
| DNP3 | dnp3py | **FAIL / PRODUCT DEFECT** | Communication works; configured Int32 G30V1 `4242` reaches canonical cache as `System.Double 4242`. Fix product, do not weaken assertion. |
| OPC UA | open62541 | **PENDING** | Peer health/reference smoke is green; actual Driver read/write/subscription/reconnect product path is not yet accepted L2. |
| Siemens S7 | python-snap7 | **PENDING** | Peer build/start/TCP is green; full Driver session/PDU/DB read-write/reconnect product path is not yet accepted L2. |
| BACnet/IP | BACpypes | **PENDING** | Peer is healthy; full Driver Who-Is/I-Am/RP/RPM/WP/COV/recovery product path is not yet accepted L2. |

Therefore the current product L2 count is:

- **3 accepted**;
- **1 executed and failed with a real product defect**;
- **3 not yet completed**.

Do not describe the last four as “four failed Drivers”.

## 6. Live worker lines at this audit

| Driver | Branch | PR | Live/audited head | Current evidence / blocker |
| --- | --- | ---: | --- | --- |
| D4 BACnet/IP | `driver4/bacnet` | #109 | `6a39d09c7d7a436ca1b6026741d4239cbbefe3ef` | Latest CI #925 RED from unrelated `ModbusTcpDriverTests` 500 ms timeout; BACnet tests in that run passed. Current head therefore has **no accepted exact-head normal green yet**. Previous BACnet head `de335...` had CI #860 green. L2 product path still pending. |
| D5 CIP | `driver5/allen-bradley-cip` | #111 | `18ff6dc989a65c1f8b006f83c08d8394a5510914` | CI #785 green; product L2 accepted; ready for later coordinator convergence. |
| D6 IEC-104 | `driver6/iec-60870-5-104` | #146 | `d597ef5ed1885b63dcd0b3568287bc1e34330bee` | CI #798 green; independent L2 13/13 accepted; next ingress after MQTT closes. |
| D7 DNP3 | `driver7/dnp3` | #108 / validation #167 | `ac0dd6944f53d19447f3353addd404c02da7249c` | Worker CI green; L2 exposes real Int32 -> Double canonical mismatch. Product fix + exact L2 rerun required. |
| D8 Siemens S7 | `driver8/siemens-s7-iso` | #135 | `0c37b922b44f591ebd143470abf3ebaa6b4bffae` | CI #789 green; full independent product-path L2 pending. |
| D9 OPC UA | `driver9/opc-ua` | #169 | `5ce1f3c912bf3779e892fb136b51b54b0f19a5c6` | CI #869 green; full independent product-path L2 pending. |
| D10 MQTT | `driver10/mqtt` | #128 | `acd46cd9a4a49e324f2037a1994e6f579a0bae3f` | CI #865 green; L2 accepted; coordinator ingress implemented with final password-reference E2E gate pending. |

Always re-read these refs before mutation. This table is a handoff snapshot, not a branch lock.

## 7. Serialized coordinator ingress order

The accepted order remains:

1. MQTT
2. IEC-104
3. CIP / EtherNet/IP
4. OPC UA
5. DNP3
6. Siemens S7
7. BACnet/IP

Do not start IEC-104 coordinator ingress until the explicit MQTT password-reference activation test is green on the exact coordinator head.

Parallel protocol work may continue only inside its isolated worker scope:

- DNP3: canonical type fix + L2 rerun;
- OPC UA: actual Driver L2 against open62541;
- Siemens S7: actual Driver L2 against python-snap7;
- BACnet/IP: actual Driver L2 against BACpypes and exact-head CI stabilization.

## 8. Immediate next actions

1. Add MQTT coordinator activation test using a real protected password **reference**, host resolver and capture transport.
2. Run exact-head full normal CI.
3. If fully green, mark MQTT coordinator gate closed in this file, issue #174 and PR #175.
4. Begin IEC-104 convergence through the same shared registry/planner/factory/readiness/binding model.
5. Keep DNP3/OPC UA/S7/BACnet L2 worker gates isolated and evidence-driven.

## 9. Non-negotiable integration rules

- No worker self-merges.
- Red CI does not enter `main`.
- Protocol branches are source/evidence, not merge trains.
- Re-port/adapt narrowly against current coordinator contracts.
- No Driver-to-Driver calls.
- No bypass of canonical TAG registry/cache/event flow.
- No plaintext secret/private-key material in Engineering, packages, logs or diagnostics.
- Protected material resolves through host-owned scoped short-lived leases.
- Shared Data Source readiness is not equivalent to every TAG being `Good`.
- `CommunicationBinding` is the canonical rich TAG communication envelope for v15.
- Legacy Address/Metadata is compatibility input only where explicitly supported.
- `TagValueSelector` remains the sole generic bit selector.
- ADR-007 physical transform occurs before generic bit selection.
- L2 independent software, L3 representative vendor simulator/device, L4 hardware/site, licensing and protocol conformance are separate claims.
- Never weaken a test to convert a real protocol/type defect into a green badge.

## 10. Known CI noise versus product defects

Modbus timing tests have produced intermittent failures on unrelated Driver branches and coordinator runs. A red run is **not** automatically a flake. Classification requires evidence, preferably an unchanged-head rerun.

- Coordinator line: prior Modbus timing failure was rerun on the unchanged functional head and passed; later exact head `4178ced...` is fully green in CI #918.
- BACnet current head `6a39d09c...`: CI #925 failed on a 500 ms Modbus TCP timeout while BACnet tests passed. No unchanged-head green rerun has been accepted yet, so the current BACnet head remains red for normal-CI purposes.
- DNP3 Int32 -> Double mismatch is **not** CI noise. It is a reproducible product defect and must be fixed in product code.

## 11. Merge boundary

PR #175 remains **DRAFT / DO NOT MERGE**.

Driver convergence is not complete until the intended Driver set is composed through the common host, canonical Engineering round-trips remain valid, readiness/protected material are central, required product-path evidence is accepted, and the final exact coordinator integration head is green.

Wave 11 remains deferred until this stage closes or is explicitly reprioritized.
