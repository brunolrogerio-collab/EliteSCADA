# EliteSCADA — Current Coordinator Handoff

Last operational audit: **2026-08-31 BRT**  
Operational status: **DRIVER CONVERGENCE ACTIVE / PR #175 DRAFT / DO NOT MERGE**

> **THIS FILE IS THE SINGLE OPERATIONAL HANDOFF FOR COORDINATOR CONTINUITY.**
>
> Live GitHub refs and exact-head Actions evidence win over SHAs copied into prose. Architecture semantics remain governed by ADRs and `DRIVER-CONVERGENCE-COORDINATION-V1.md`.

## 1. Resume protocol

A replacement Coordinator should read, in this order:

1. live PR **#175** and branch `coordination/driver-convergence-v3`;
2. this file;
3. live issue **#174**;
4. Actions for the exact current code head;
5. only the worker branch/PR for the next Driver being converged.

Do not reconstruct current state from old worker PR descriptions, assignment documents or historical handoffs.

Status vocabulary:

- **MERGED** — present on `main`;
- **IMPLEMENTED IN PR** — implemented on coordinator/worker line, not yet in `main`;
- **SPECIFIED / NOT IMPLEMENTED** — requirement/architecture exists but production code does not satisfy it.

## 2. Current integration line

- Repository: `brunolrogerio-collab/EliteSCADA`
- Coordinator branch: `coordination/driver-convergence-v3`
- Draft PR: **#175** — `Driver convergence v3 — shared host contracts`
- Current code-validated head: **`6c0f4b45209739de2c900b4280d3184fa6c22030`**
- Base reconciled with `main` head `d0a4e13816992b0a0eb0eb68c36e78c560cc1d88`
- Exact-head validation: **EliteSCADA CI #941 — SUCCESS**
- PR state: **DRAFT / OPEN / DO NOT MERGE**

CI #941 evidence:

- Release backend build: **SUCCESS**, 0 warnings, 0 errors;
- `Scada.Core.Tests`: **243 passed**;
- `Scada.Drivers.Tests`: **92 passed**;
- `Scada.Historian.TimescaleDb.Tests`: **23 passed**;
- `Scada.Security.Tests`: **27 passed**;
- `Scada.Persistence.PostgreSql.Tests`: **107 passed**;
- total backend tests: **492 passed / 0 failed**;
- runtime smoke: **SUCCESS**;
- Web build: **SUCCESS**;
- Chromium end-to-end: **SUCCESS**.

Documentation-only `[skip ci]` commits after this SHA do not create a new code-validation claim.

## 3. Engineering schema v15

Status: **IMPLEMENTED IN PR / GATE CLOSED**.

Validated behavior includes:

- `EngineeringExchangeService.CurrentSchemaVersion = 15`;
- TAG Preview validation of `CommunicationBinding`;
- binding preserved through Apply/runtime registry/export;
- `Address == CommunicationBinding.PortableAddress` compatibility invariant;
- plaintext secret-like binding settings fail closed;
- malformed/unsupported binding contracts fail closed;
- <= v14 compatibility while rich binding in v14 is rejected;
- generic TAG CSV `CommunicationBindingJson` plus legacy CSV compatibility;
- JSON, CSV, `.escadapkg`, immutable revision and PostgreSQL revision round-trips.

Initial closure was proven in CI #905 and remains covered by CI #941.

## 4. MQTT coordinator convergence

Driver type: `mqtt.raw`  
Worker branch: `driver10/mqtt`  
Worker PR: #128  
Audited worker head: `acd46cd9a4a49e324f2037a1994e6f579a0bae3f`  
Worker CI: #865 GREEN  
Independent product-path L2: **ACCEPTED**

Coordinator status: **IMPLEMENTED IN PR / GATE CLOSED**.

Completed on PR #175:

- audited MQTT protocol primitives, MQTTnet transport and `MqttDriver`;
- shared runtime plan/planner/factory registry;
- schema-v15 `CommunicationBinding` canonical path plus explicit legacy migration warning;
- shared Data Source readiness seam;
- generic `CommunicationPlans` compiler and runtime activation path;
- host composition through `CommunicationDriverRuntimeComposition.BuildForCurrentSchema()`;
- host-owned scoped protected-material resolver;
- deterministic environment secret name bound to Project/DataSource/Driver/Purpose/Reference;
- short-lived protected-material lease and buffer clearing behavior;
- MQTT can become Ready after connection/subscription without first telemetry sample;
- isolated MQTT factory still fails closed without a shared protected-material resolver;
- full regression proving `Engineering password reference -> host composition -> scoped resolver -> MQTT factory -> transport credentials`.

The final password-reference regression `Coordinator_UsesHostComposedProtectedMaterialForMqttPasswordReference` ran and passed in CI #941. MQTT convergence is therefore closed. Do not merge worker PR #128 wholesale; it remains source/evidence history.

## 5. Current product-path L2 matrix

Common seven-peer lab infrastructure is **7/7 healthy**. Peer health is not Driver acceptance.

| Driver | Product L2 | Current meaning |
| --- | --- | --- |
| MQTT | **PASS / ACCEPTED** | Independent broker evidence accepted; coordinator convergence closed. |
| IEC-104 | **PASS / ACCEPTED 13/13** | lib60870 product path accepted; **next coordinator ingress**. |
| CIP / EtherNet/IP | **PASS / ACCEPTED** | Independent product path accepted; queued after IEC-104. |
| DNP3 | **FAIL / PRODUCT DEFECT** | Configured Int32 G30V1 `4242` reaches canonical cache as `System.Double`; product fix + exact L2 rerun required. |
| OPC UA | **PENDING** | Peer healthy; actual Driver product L2 not yet accepted. |
| Siemens S7 | **PENDING** | Peer healthy; actual Driver product L2 not yet accepted. |
| BACnet/IP | **PENDING** | Peer healthy; actual Driver product L2 not yet accepted. |

Product-path count: **3 accepted, 1 real failure, 3 pending**.

## 6. Worker checkpoints

These are snapshots, not branch locks. Re-read before mutation.

- IEC-104 `driver6/iec-60870-5-104`, PR #146: last audited head `d597ef5ed1885b63dcd0b3568287bc1e34330bee`; CI #798 green; independent L2 13/13 accepted.
- CIP `driver5/allen-bradley-cip`, PR #111: `18ff6dc989a65c1f8b006f83c08d8394a5510914`; CI #785 green; L2 accepted.
- OPC UA `driver9/opc-ua`, PR #169: `5ce1f3c912bf3779e892fb136b51b54b0f19a5c6`; CI #869 green; product L2 pending.
- DNP3 `driver7/dnp3`, PR #108 / validation #167: `ac0dd6944f53d19447f3353addd404c02da7249c`; real Int32 -> Double defect remains.
- Siemens S7 `driver8/siemens-s7-iso`, PR #135: `0c37b922b44f591ebd143470abf3ebaa6b4bffae`; CI #789 green; product L2 pending.
- BACnet/IP `driver4/bacnet`, PR #109: last observed `6a39d09c7d7a436ca1b6026741d4239cbbefe3ef`; CI #925 red from unrelated 500 ms Modbus timeout while BACnet tests passed; no accepted exact-head normal green yet.
- MQTT `driver10/mqtt`, PR #128: `acd46cd9a4a49e324f2037a1994e6f579a0bae3f`; worker CI #865 green; L2 accepted; coordinator convergence closed in CI #941.

## 7. Serialized coordinator order

1. ~~MQTT~~ — **CLOSED**
2. **IEC-104 — ACTIVE NEXT**
3. CIP / EtherNet/IP
4. OPC UA
5. DNP3
6. Siemens S7
7. BACnet/IP

Do not advance to CIP before IEC-104 has its own coordinator activation/binding/readiness gate and exact-head green CI.

## 8. Immediate next action: IEC-104

Before editing code:

1. re-read live `driver6/iec-60870-5-104` head, PR #146 and exact-head Actions evidence;
2. audit its delta against current `main`/coordinator contracts;
3. identify protocol-owned code versus any private duplicate host contracts;
4. port/adapt narrowly to v15 `CommunicationBinding`, shared planner/factory registry and shared readiness;
5. preserve IEC-104 identity as Common Address + IOA and retain Cause of Transmission, quality and CP56Time2a evidence;
6. keep startup readiness tied to TCP + STARTDT + configured GI/startup completion;
7. prove real activation through `EngineeringRuntimeCoordinator`;
8. run exact-head full normal CI before starting CIP.

## 9. Non-negotiable integration rules

- No worker self-merges.
- Red CI does not enter `main`.
- Worker branches are source/evidence, not merge trains.
- No protocol-private duplicate host contracts after convergence.
- No Driver-to-Driver runtime calls.
- No bypass of canonical TAG registry/cache/event flow.
- No plaintext secret/private-key material in Engineering, packages, logs or diagnostics.
- Protected material resolves through host-owned scoped short-lived leases.
- Shared readiness is not equivalent to every TAG being `Good`.
- `CommunicationBinding` is the canonical rich TAG communication envelope in schema v15.
- `TagValueSelector` remains the sole generic bit selector.
- ADR-007 physical transformation precedes generic bit selection.
- L2, L3, L4, licensing and conformance remain separate claims.
- Never weaken an interoperability assertion to manufacture a green result.

## 10. Merge boundary

PR #175 remains **DRAFT / DO NOT MERGE**.

Driver convergence is complete only when the intended Driver set is composed through the common host, canonical Engineering remains round-trip safe, readiness/protected material are central, required product-path evidence is accepted, and the final exact coordinator integration/main CI is green.

Wave 11 remains deferred until this stage closes or is explicitly reprioritized.
