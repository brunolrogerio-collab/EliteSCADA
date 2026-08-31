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
4. Actions for the exact current branch/code head;
5. `docs/DRIVER-AND-INTEROP-LAB-STATUS.md` for L0-L4 evidence policy;
6. worker PRs only when historical Driver source/evidence must be audited.

Do not reconstruct current state from old worker PR descriptions, assignment documents or historical handoffs.

Status vocabulary:

- **MERGED** — present on `main`;
- **IMPLEMENTED IN PR** — implemented on coordinator/worker line, not yet in `main`;
- **SPECIFIED / NOT IMPLEMENTED** — requirement/architecture exists but production code does not satisfy it.

## 2. Current integration line

- Repository: `brunolrogerio-collab/EliteSCADA`
- Coordinator branch: `coordination/driver-convergence-v3`
- Draft PR: **#175** — `Driver convergence v3 — shared host contracts`
- Last functional convergence head: **`b7910119788c8dabb19229753f5f5599b8387a7a`**
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

CI #977 attempt 1 had one unrelated Modbus timing failure. All four new BACnet convergence tests passed in attempt 1. Failed jobs were rerun on the unchanged functional SHA; attempt 2 passed. No assertion or product code was weakened to manufacture green evidence.

Documentation-only `[skip ci]` commits after a validated code head do not create a new functional validation claim.

## 3. Engineering schema v15

Status: **IMPLEMENTED IN PR / GATE CLOSED**.

Validated behavior includes canonical `CommunicationBinding`, Preview/Apply preservation, compatibility Address mapping, fail-closed malformed/foreign/secret-like handling, CSV/JSON/package/revision/PostgreSQL persistence, <=v14 compatibility and shared transform/selector semantics.

## 4. Coordinator Driver convergence result

The intended Driver set is **7/7 CLOSED FOR COORDINATOR CONVERGENCE**.

| Order | Driver | Coordinator state | Product-path L2 |
| --- | --- | --- | --- |
| 1 | MQTT | **CLOSED** | **PASS / ACCEPTED** |
| 2 | IEC-104 | **CLOSED** | **PASS / ACCEPTED 13/13** |
| 3 | CIP / EtherNet/IP | **CLOSED** | **PASS / ACCEPTED** |
| 4 | OPC UA | **CLOSED** | **PASS / ACCEPTED** |
| 5 | DNP3 | **CLOSED** | **PASS / ACCEPTED** |
| 6 | Siemens S7 ISO-on-TCP | **CLOSED** | **PASS / ACCEPTED** |
| 7 | BACnet/IP | **CLOSED** | **PASS / ACCEPTED** |

Common independent peer infrastructure is **7/7 healthy** and independent product-path L2 is **7/7 PASS / ACCEPTED**.

## 5. Closed Driver checkpoints

### MQTT

- DriverType `mqtt.raw`;
- worker PR #128 / head `acd46cd9a4a49e324f2037a1994e6f579a0bae3f`;
- v15 binding, common planner/factory/readiness, Coordinator activation and host-scoped protected-material resolution closed.

### IEC-104

- DriverType `iec60870.5.104`;
- worker PR #146 / head `d597ef5ed1885b63dcd0b3568287bc1e34330bee`;
- independent lib60870-C L2 13/13 accepted;
- v15 binding, common readiness, GI startup, timestamps/quality/COT and command routing closed.

### CIP / EtherNet/IP

- DriverType `rockwell.logix.eip`;
- worker PR #111;
- symbolic Logix binding, common composition/readiness, real Coordinator read/write/cache path and runtime lifetime isolation closed.

### OPC UA

- worker PR #169;
- independent open62541 L2 accepted;
- stable NodeId/namespace identity, common composition/readiness, timestamp preservation, writes and host-scoped credential/certificate resolution closed.

### DNP3

- worker PR #108;
- independent dnp3py L2 accepted after canonical analog type defect correction;
- G30V1 Int32 preservation, startup integrity/readiness, timestamps/cache and output write routing closed.

### Siemens S7 ISO-on-TCP

- DriverType `siemens.s7.iso`;
- worker PR #135 / head `f8a50d7583795f683f02386c629bbdc2ec4aa8f7`;
- independent python-snap7 L2 accepted;
- TPKT/COTP, Setup Communication, negotiated PDU 240, typed read, Write Var/cache update and readiness closed.

### BACnet/IP

- DriverType `bacnet.ip`;
- binding schema `scada.driver.bacnet.ip.binding` v1;
- worker PR #109;
- independent BACpypes L2 accepted;
- protocol ingress `f6da0d9cb78949cc2cd090acd596c659326eba2f`;
- common adapter `097b695ed13d7e2cd6c983b3896d37069276e5c7`;
- readiness correction `a7a57d2050bb27d862a216dbe2f0ef9b76324901`;
- final Coordinator regressions `b7910119788c8dabb19229753f5f5599b8387a7a`;
- acquisition/cache, COV fallback, WriteProperty priority and timeout/recovery closed.

## 6. Shared architecture that must remain intact

- common Driver module registry keyed by stable DriverType;
- common runtime planner/factory component registry;
- shared protocol-neutral readiness contract;
- host-owned scoped short-lived protected-material resolver/lease seam;
- Engineering v15 `CommunicationBinding` as canonical rich communication TAG envelope;
- canonical TAG registry/cache/event flow;
- no protocol SDK/session objects across shared planning boundaries;
- no Driver-to-Driver runtime calls;
- no plaintext secret/private-key material;
- worker PRs are source/evidence history, not merge trains.

## 7. Evidence policy after convergence

EliteSCADA now uses the following operational evidence stages:

- **L0** — unit/codec/contracts;
- **L1** — same-stack/in-process/loopback;
- **L2** — Driver against an independent software peer over the real wire protocol;
- **L3** — **post-main integrated seven-Driver laboratory**, with one EliteSCADA build/runtime operating all seven converged Drivers concurrently;
- **L4** — **physical hardware/site evaluation using the Preview build**, performed and accepted by Development Lead **Bruno Luiz Rogerio**.

L3 is the next software-system acceptance gate. L4 is intentionally deferred until the Preview build exists and does not block Wave 11.

## 8. Immediate next action: mainline then L3

There is no eighth Driver ingress.

Required order:

1. finish final PR #175 pre-merge audit and exact-head validation;
2. merge the completed Driver convergence into `main` only after the gate is green;
3. require exact post-merge `main` CI green;
4. create/run the **L3 integrated seven-Driver laboratory** on that exact `main` build;
5. keep all seven Data Sources active in the same EliteSCADA runtime/project;
6. prove concurrent acquisition, supported writes/commands, shared readiness, cache identity isolation, one-peer fault isolation, recovery and clean shutdown;
7. if L3 is green, close the Driver convergence/laboratory stage and start **Wave 11**.

**Wave 11 MUST NOT start before the post-main seven-Driver L3 laboratory passes.**

## 9. Future L4 physical validation

After a Preview build exists, Development Lead **Bruno Luiz Rogerio** performs the physical Driver validation.

L4 evidence is device-specific and must record the exact Preview build, Driver, manufacturer, model, firmware, topology/settings, read/write/reconnect scenarios, diagnostics and final result.

A PASS for one physical device/model must not be generalized to every device using the protocol.

## 10. Non-negotiable integration rules

- No worker self-merges.
- Red CI does not enter `main`.
- Do not weaken a test to manufacture green evidence.
- No Driver-to-Driver calls or canonical TAG/cache/event bypass.
- No plaintext protected material.
- Shared readiness is not every TAG `Good`.
- `CommunicationBinding` remains canonical in schema v15.
- L2 does not imply L3.
- L3 does not imply physical L4.
- Licensing/formal conformance remain separate evidence claims.

## 11. Merge / stage boundary

PR #175 remains **DRAFT / OPEN / DO NOT MERGE** until the final pre-merge gate is complete.

Driver convergence implementation is complete. After controlled merge and post-main CI, the next mandatory gate is **L3 seven-Driver concurrent laboratory**. Only an L3 PASS releases Wave 11.

L4 physical evaluation is a later Preview-stage responsibility of the Development Lead.
