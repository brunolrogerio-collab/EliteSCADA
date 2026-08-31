# EliteSCADA — Current Coordinator Handoff

Last operational audit: **2026-08-31 BRT**  
Operational status: **DRIVER CONVERGENCE 7/7 CLOSED / PR #175 DRAFT / PRE-MERGE MAINLINE VALIDATION**

> **THIS FILE IS THE SINGLE OPERATIONAL HANDOFF FOR COORDINATOR CONTINUITY.**
>
> Live GitHub refs and exact-head Actions evidence win over SHAs copied into prose. Architecture semantics remain governed by ADRs and `DRIVER-CONVERGENCE-COORDINATION-V1.md`.

## 1. Resume protocol

A replacement Coordinator should read, in this order:

1. live PR **#175** and branch `coordination/driver-convergence-v3`;
2. this file;
3. live issue **#174**;
4. Actions for the exact current branch head;
5. worker PRs only when historical Driver source/evidence must be audited.

Do not reconstruct current state from old worker PR descriptions, assignment documents or historical handoffs.

Status vocabulary:

- **MERGED** — present on `main`;
- **IMPLEMENTED IN PR** — implemented on coordinator/worker line, not yet in `main`;
- **SPECIFIED / NOT IMPLEMENTED** — requirement/architecture exists but production code does not satisfy it.

## 2. Current integration line

- Repository: `brunolrogerio-collab/EliteSCADA`
- Coordinator branch: `coordination/driver-convergence-v3`
- Draft PR: **#175** — `Driver convergence v3 — shared host contracts`
- Last functional code-validated head before this documentation synchronization: **`b7910119788c8dabb19229753f5f5599b8387a7a`**
- Exact functional validation: **EliteSCADA CI #977 — SUCCESS, attempt 2 on unchanged SHA**
- Base used by CI #977: `main` head `d0a4e13816992b0a0eb0eb68c36e78c560cc1d88`
- PR state: **DRAFT / OPEN / DO NOT MERGE**

CI #977 evidence:

- Release backend build: **SUCCESS**, 0 warnings, 0 errors;
- `Scada.Core.Tests`: **243 passed**;
- `Scada.Drivers.Tests`: **347 passed**;
- `Scada.Historian.TimescaleDb.Tests`: **23 passed**;
- `Scada.Security.Tests`: **27 passed**;
- `Scada.Persistence.PostgreSql.Tests`: **107 passed**;
- total backend tests: **747 passed / 0 failed**;
- runtime smoke: **SUCCESS**;
- Web build: **SUCCESS**;
- Chromium end-to-end: **SUCCESS**.

CI #977 attempt 1 had one unrelated Modbus timing failure in `ModbusTcpDiagnosticsTests.Diagnostics_IsolateTwoInstancesAndRecoverOneAfterTimeouts`. All four new BACnet convergence tests passed in attempt 1. Failed jobs were rerun on the unchanged functional SHA; attempt 2 passed. No Modbus assertion or product code was weakened to obtain green evidence.

This documentation synchronization is intentionally followed by another exact-head CI before any merge-state transition.

## 3. Engineering schema v15

Status: **IMPLEMENTED IN PR / GATE CLOSED**.

Validated behavior includes:

- canonical `CommunicationBinding` on communication TAGs;
- Preview/Apply preservation;
- compatibility `Address == CommunicationBinding.PortableAddress`;
- fail-closed malformed/foreign/secret-like binding handling;
- CSV/JSON/package/revision/PostgreSQL persistence;
- <=v14 compatibility;
- `TagValueSelector` remains the generic bit selector;
- physical transformations occur before generic selection where the protocol representation supports them.

## 4. Coordinator Driver convergence result

The intended Driver set is now **7/7 CLOSED FOR COORDINATOR CONVERGENCE**.

| Order | Driver | Coordinator state | Product-path L2 |
| --- | --- | --- | --- |
| 1 | MQTT | **CLOSED** | **PASS / ACCEPTED** |
| 2 | IEC-104 | **CLOSED** | **PASS / ACCEPTED 13/13** |
| 3 | CIP / EtherNet/IP | **CLOSED** | **PASS / ACCEPTED** |
| 4 | OPC UA | **CLOSED** | **PASS / ACCEPTED** |
| 5 | DNP3 | **CLOSED** | **PASS / ACCEPTED** |
| 6 | Siemens S7 ISO-on-TCP | **CLOSED** | **PASS / ACCEPTED** |
| 7 | BACnet/IP | **CLOSED** | **PASS / ACCEPTED** |

Common independent peer infrastructure is **7/7 healthy**. Peer health alone is not Driver acceptance; the product-path L2 evidence above was separately accepted.

L3/L4, licensing, formal conformance/certification, vendor breadth and production-distribution remain separate claims.

## 5. Closed Driver checkpoints

### MQTT

- DriverType `mqtt.raw`;
- worker PR #128 / head `acd46cd9a4a49e324f2037a1994e6f579a0bae3f` / CI #865 green;
- v15 binding, common planner/factory/readiness and Coordinator activation closed;
- host-owned scoped protected-material resolution proven end-to-end;
- independent product-path L2 accepted.

### IEC-104

- DriverType `iec60870.5.104`;
- worker PR #146 / head `d597ef5ed1885b63dcd0b3568287bc1e34330bee` / CI #798 green;
- independent lib60870-C product-path L2 13/13 accepted;
- v15 binding, common composition/readiness, GI startup, quality/source time, command routing and Coordinator cache flow closed.

### CIP / EtherNet/IP

- DriverType `rockwell.logix.eip`;
- worker PR #111;
- independent product-path L2 accepted;
- symbolic Logix binding, common composition/readiness, real Coordinator read/write/cache path and runtime-lifetime isolation closed.

### OPC UA

- worker PR #169;
- independent open62541 product-path L2 accepted;
- stable NodeId/namespace identity, common composition/readiness, timestamp preservation, writes and host-scoped credential/certificate material resolution closed.

### DNP3

- worker PR #108;
- independent dnp3py product-path L2 accepted after the canonical analog type defect was fixed;
- canonical G30V1 Int32 preservation, startup integrity/readiness, timestamp/cache behavior and output write routing closed.

### Siemens S7 ISO-on-TCP

- DriverType `siemens.s7.iso`;
- worker PR #135 / worker head `f8a50d7583795f683f02386c629bbdc2ec4aa8f7` / CI #939 green;
- independent python-snap7 product-path L2 accepted;
- Coordinator commits include protocol ingress `3514dc072fa25f28ebc964ca8c73a20f18e1c877`, common adapter `bd9d9c16abe67667bf51f5f32329c54065eb3cd7`, deterministic ISO server `74b3dc6ca627f8a4bae2c3158a1a7876f4c286cb` and final regressions `5d45e5d77d7b725c46e512b282fabc8b17039156`;
- real TPKT/COTP/S7 Setup Communication, negotiated PDU 240, typed read, Write Var/cache update and source readiness despite point-local degradation are closed.

### BACnet/IP

- DriverType `bacnet.ip`;
- binding schema `scada.driver.bacnet.ip.binding` v1;
- worker PR #109;
- converged protocol-owned worker source audited from `de3357750f79266e43588e7bb26d66093f8cf3d5` / worker CI #860 green;
- independent BACpypes product-path L2 accepted;
- package `BACnet` 4.0.0 / `System.IO.BACnet`;
- protocol ingress `f6da0d9cb78949cc2cd090acd596c659326eba2f`;
- common v15 planner/factory/readiness/composition `097b695ed13d7e2cd6c983b3896d37069276e5c7`;
- readiness contract correction `a7a57d2050bb27d862a216dbe2f0ef9b76324901`;
- final Coordinator regressions `b7910119788c8dabb19229753f5f5599b8387a7a`.

BACnet closure proves:

1. v15 `CommunicationBinding` authoritative;
2. stable Device/Object/Property identity in `PortableAddress`;
3. COV and write priority kept in canonical binding settings, not duplicated into identity;
4. stable TAG IDs and Device Instance consistency;
5. foreign schema, protected material and byte/word transforms fail closed;
6. real `SystemIoBacnetSessionFactory` remains production default, with injectable session factory for deterministic tests;
7. readiness comes from Device Instance reachability + active protocol state, not every TAG Good;
8. real common composition -> compiler -> `EngineeringRuntimeCoordinator` -> `BacnetIpDriver` activation path;
9. typed first acquisition into canonical cache;
10. COV-unavailable fallback to polling;
11. WriteProperty with configured priority through Coordinator `WriteAsync`;
12. initial timeout followed by reachability/acquisition recovery inside the activation readiness window.

## 6. Shared architecture that must remain intact

- common Driver module registry keyed by stable DriverType;
- common runtime planner/factory component registry;
- shared protocol-neutral readiness contract;
- host-owned scoped short-lived protected-material resolver/lease seam;
- Engineering v15 `CommunicationBinding` as the canonical rich communication TAG envelope;
- canonical TAG registry/cache/event flow;
- no protocol SDK/session objects crossing shared planning boundaries;
- no Driver-to-Driver runtime calls;
- no plaintext secret/private-key material in Engineering, packages, logs or diagnostics;
- worker PRs remain source/evidence history, not merge trains.

## 7. Immediate next action: final mainline gate

There is **no eighth Driver ingress** in this convergence scope.

Before PR #175 can leave Draft / DO NOT MERGE:

1. validate the current documentation-synchronized branch head in normal CI;
2. re-read live `main` and confirm the PR remains mergeable with no unexpected base movement/conflict;
3. audit the final PR delta for accidental worker-private host contracts, duplicated composition seams, plaintext protected material, or bypass of canonical TAG/cache/event paths;
4. keep L2/L3/L4/licensing/conformance claims separated;
5. only after an exact final pre-merge green gate may merge readiness be considered;
6. after any controlled merge, require post-merge `main` CI green before issue #174 is closed.

## 8. Non-negotiable integration rules

- No worker self-merges.
- Red CI does not enter `main`.
- Do not weaken a test to manufacture green evidence.
- No Driver-to-Driver calls.
- No bypass of canonical TAG registry/cache/event flow.
- No plaintext protected material.
- Shared readiness is not every TAG `Good`.
- `CommunicationBinding` remains canonical in schema v15.
- Product L2 acceptance does not imply L3/L4, licensing, formal conformance or production-distribution closure.

## 9. Merge boundary

PR #175 remains **DRAFT / OPEN / DO NOT MERGE**.

Driver convergence itself is complete. The remaining boundary is final exact-head pre-merge validation plus controlled post-merge `main` validation. Issue #174 remains open until that mainline integration scope is actually complete.

Wave 11 remains deferred until this integration boundary closes or is explicitly reprioritized.