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
- Current code-validated head: **`8308a9ee953062ff8a5085a82ad26e532028a87c`**
- Base reconciled with `main` head `d0a4e13816992b0a0eb68c36e78c560cc1d88`
- Exact-head validation: **EliteSCADA CI #951 — SUCCESS**
- PR state: **DRAFT / OPEN / DO NOT MERGE**

CI #951 evidence:

- Release backend build: **SUCCESS**, 0 warnings, 0 errors;
- `Scada.Core.Tests`: **243 passed**;
- `Scada.Drivers.Tests`: **269 passed**;
- `Scada.Historian.TimescaleDb.Tests`: **23 passed**;
- `Scada.Security.Tests`: **27 passed**;
- `Scada.Persistence.PostgreSql.Tests`: **107 passed**;
- total backend tests: **669 passed / 0 failed**;
- runtime smoke: **SUCCESS**;
- Web build: **SUCCESS**;
- Chromium end-to-end: **SUCCESS**.

One IEC-104 T2 timing assertion failed once in CI #949 and passed on rerun of the unchanged SHA. The same assertion also passed in #942 and #951. No test was weakened.

Documentation-only `[skip ci]` commits after this SHA do not create a new code-validation claim.

## 3. Engineering schema v15

Status: **IMPLEMENTED IN PR / GATE CLOSED**.

Validated behavior includes Preview/Apply preservation, `Address == CommunicationBinding.PortableAddress`, fail-closed secret-like/malformed binding handling, generic CSV fidelity, project package/revision/PostgreSQL persistence and <=v14 compatibility.

## 4. Closed coordinator Driver gates

### MQTT

Status: **IMPLEMENTED IN PR / GATE CLOSED**.

- DriverType `mqtt.raw`;
- worker PR #128, head `acd46cd9a4a49e324f2037a1994e6f579a0bae3f`, CI #865 green;
- independent product L2 accepted;
- v15 binding, shared planner/factory/readiness and real coordinator activation closed;
- host-owned scoped protected-material resolution closed;
- CI #941 proves `Engineering password reference -> host composition -> scoped resolver -> MQTT factory -> transport credentials`.

### IEC-104

Status: **IMPLEMENTED IN PR / GATE CLOSED**.

- DriverType `iec60870.5.104`;
- worker PR #146, head `d597ef5ed1885b63dcd0b3568287bc1e34330bee`, CI #798 green;
- independent lib60870-C product-path L2 **13/13 accepted**;
- portable identity `ca=<CA>;ioa=<IOA>`;
- coordinator binding schema `elite.iec60870.5.104.point` v1;
- v15 `CommunicationBinding` canonical with explicit legacy migration warning;
- plain-TCP first release fails closed on protected-material references and physical byte/word transforms;
- common planner/factory composition and shared readiness integrated;
- readiness requires TCP + STARTDT + configured startup GI completion, not every TAG Good;
- real coordinator activation and canonical cache/registry publication proven;
- quality and CP56Time2a source timestamp preserved;
- Cause of Transmission preserved through monitored decode;
- configured Originator Address preserved for all five supported operational command types;
- real coordinator `WriteAsync` command completion proven with ACT_CON/ACT_TERM;
- exact coordinator CI #951 fully green.

Worker PRs remain source/evidence history and are not merge trains.

## 5. Current product-path L2 matrix

Common seven-peer lab infrastructure is **7/7 healthy**. Peer health is not Driver acceptance.

| Driver | Product L2 | Current meaning |
| --- | --- | --- |
| MQTT | **PASS / ACCEPTED** | Coordinator convergence closed. |
| IEC-104 | **PASS / ACCEPTED 13/13** | Coordinator convergence closed. |
| CIP / EtherNet/IP | **PASS / ACCEPTED** | **Current coordinator ingress.** |
| OPC UA | **PASS / ACCEPTED** | Independent open62541 path accepted; product head `b0b2a764...` normal CI #943 green. |
| DNP3 | **FAIL / OPEN** | Product head advanced to `a0d62d7...` with CI #940 green, but independent validation #167 remains unresolved. |
| Siemens S7 | **PASS / ACCEPTED** | Independent python-snap7 path accepted. |
| BACnet/IP | **PASS / ACCEPTED** | Independent BACpypes path accepted. |

Product-path count: **6 accepted / 1 failing independent L2**.

L3/L4, licensing and production-distribution decisions remain separate evidence claims.

## 6. Live worker checkpoints

These are snapshots, not branch locks. Re-read before mutation.

- CIP `driver5/allen-bradley-cip`, PR #111: `18ff6dc989a65c1f8b006f83c08d8394a5510914`; exact CI #785 green; independent L2 accepted.
- OPC UA `driver9/opc-ua`, PR #169: `b0b2a7642f6d0720eedcfe45597d82e4ee6d2488`; exact normal CI #943 green; independent open62541 L2 accepted.
- DNP3 `driver7/dnp3`, PR #108: `a0d62d7a4577a2f2799ef29d2ef67b1acabc3c3c`; exact normal CI #940 green; validation PR #167 still unresolved.
- Siemens S7 `driver8/siemens-s7-iso`, PR #135: `f8a50d7583795f683f02386c629bbdc2ec4aa8f7`; exact normal CI #939 green; independent python-snap7 L2 accepted.
- BACnet/IP `driver4/bacnet`, PR #109: `40c062fd9cfa5adccb323e285cb17694c005e4cc`; independent BACpypes L2 accepted through validation PR #177.
- IEC-104 `driver6/iec-60870-5-104`, PR #146: `d597ef5ed1885b63dcd0b3568287bc1e34330bee`; worker CI #798 green; coordinator convergence closed in CI #951.
- MQTT `driver10/mqtt`, PR #128: `acd46cd9a4a49e324f2037a1994e6f579a0bae3f`; worker CI #865 green; coordinator convergence closed in CI #941.

## 7. Serialized coordinator order

1. ~~MQTT~~ — **CLOSED**
2. ~~IEC-104~~ — **CLOSED**
3. **CIP / EtherNet/IP — ACTIVE**
4. OPC UA
5. DNP3
6. Siemens S7
7. BACnet/IP

Do not advance to OPC UA before CIP has its own coordinator activation/binding/readiness gate and exact-head full green CI.

## 8. Immediate next action: CIP / EtherNet/IP

Worker source:

- branch `driver5/allen-bradley-cip`;
- PR #111;
- exact worker head `18ff6dc989a65c1f8b006f83c08d8394a5510914`;
- exact normal CI #785 green;
- independent product-path L2 accepted;
- worker delta is isolated to Allen-Bradley protocol/runtime/Engineering code and tests.

Coordinator requirements:

1. port protocol-owned code/tests narrowly, not the worker branch wholesale;
2. preserve stable DriverType `rockwell.logix.eip` and schema `elitescada.driver.rockwell.logix.eip` v1;
3. make v15 `CommunicationBinding` canonical and require stable TAG IDs;
4. keep `TagValueSelector` as the generic physical integer-bit selector;
5. reject physical byte/word transforms for symbolic typed Logix values;
6. fail closed on CIP Security/protected-material requests until a secure runtime is actually implemented; never downgrade silently;
7. adapt the protocol planner/factory to the shared registry;
8. project worker readiness through `ICommunicationDriverReadinessSource` only after session/route establishment plus bounded acquisition execution;
9. keep source readiness distinct from point-local Good/Bad quality;
10. prove real `EngineeringRuntimeCoordinator` activation, read/write and canonical cache flow;
11. harden long-lived driver lifetime so startup caller cancellation is not accidentally the runtime lifetime token;
12. require exact-head normal CI before starting OPC UA.

Audit finding already identified: the worker planner still reads `TagEngineeringDto.Address` directly and permits generated IDs. Those behaviors must not cross the v15 convergence boundary unchanged.

## 9. Non-negotiable integration rules

- No worker self-merges.
- Red CI does not enter `main`.
- Worker branches are source/evidence, not merge trains.
- No protocol-private duplicate host contracts after convergence.
- No Driver-to-Driver runtime calls.
- No bypass of canonical TAG registry/cache/event flow.
- No plaintext secret/private-key material in Engineering, packages, logs or diagnostics.
- Protected material resolves only through host-owned scoped short-lived leases where supported.
- Shared readiness is not equivalent to every TAG being `Good`.
- `CommunicationBinding` is the canonical rich TAG communication envelope in schema v15.
- `TagValueSelector` remains the sole generic bit selector.
- ADR-007 physical transformation precedes generic bit selection where applicable; symbolic Logix values do not invent swap settings.
- L2, L3, L4, licensing and conformance remain separate claims.
- Never weaken an interoperability assertion to manufacture green evidence.

## 10. Merge boundary

PR #175 remains **DRAFT / DO NOT MERGE**.

Driver convergence is complete only when the intended Driver set is composed through the common host, canonical Engineering remains round-trip safe, readiness/protected material are central, required product-path evidence is accepted, and the final exact coordinator integration/main CI is green.

Wave 11 remains deferred until this stage closes or is explicitly reprioritized.