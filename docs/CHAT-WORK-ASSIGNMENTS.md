# CHAT WORK ASSIGNMENTS — EliteSCADA

Date: 2026-08-30 (BRT)  
Stage: **DRIVER CONVERGENCE — ACTIVE / COMMON LAB MERGED / WAVE 11 DEFERRED**  
Integration owner: **Coordinator**  
Shared issue: **#174**  
Integration branch: `coordination/driver-convergence-v3`  
Draft PR: **#175**

Start a new Coordinator chat with `PROJECT GOAL.md`, `LAST CHANGE.md` and `docs/COORDINATOR-HANDOFF.md`, then re-read live GitHub state.

## Current priority

Wave 10 is CLOSED / MERGED / POST-MAIN GREEN. The common seven-peer interoperability lab is MERGED. Current priority is shared Driver convergence plus the remaining protocol-owned product L2/fix gates.

Operational authority is live branch/PR `head_sha` + exact Actions evidence + current coordination docs. Long-lived PR bodies can be historically useful but stale.

## Coordinator — Driver Convergence v3

Issue: #174  
Branch: `coordination/driver-convergence-v3`  
PR: #175  
Exact audited head: `06c7d408c76926bf5d37dfec4be20ea6044f52b1`  
Exact normal CI: **#895 GREEN**

### Implemented on #175

- fail-closed Driver module registry keyed by stable DriverType;
- common runtime planner/factory registry;
- protocol-neutral Data Source readiness contract;
- scoped host-owned protected-material resolver/lease seam;
- focused shared-contract tests;
- partial `CommunicationTagBinding` / `TagPhysicalValueTransform` / Engineering DTO scaffold.

### Immediate Coordinator task — COMPLETE v15 BINDING

The v15 scaffold is **not yet end-to-end functional**:

- `EngineeringExchangeService.CurrentSchemaVersion` remains 14;
- TAG Preview does not invoke `CommunicationTagBindingEngineeringValidator`;
- TAG Apply drops `dto.CommunicationBinding`;
- preview materialization also omits the rich binding;
- TAG CSV fidelity is missing;
- no complete JSON/CSV/Preview/Apply/re-export/package/revision/PostgreSQL v15 regression exists.

Next Coordinator must complete this slice before adapting MQTT:

1. bump canonical Engineering schema to 15 with <=v14 compatibility;
2. wire binding validation into Preview;
3. preserve binding through Apply/materialization/export;
4. enforce `Address == CommunicationBinding.PortableAddress`;
5. implement CSV fidelity where applicable;
6. prove JSON/CSV/package/revision/PostgreSQL round-trip;
7. fail closed on malformed binding/plaintext protected material;
8. preserve `TagValueSelector` and ADR-007 transform-before-selection semantics;
9. exact-head CI.

The old `coordination/driver-convergence-mainline-v2` is **reference-only / obsolete as a merge source**.

## Common interoperability lab — MERGED

PR #173 merge: `a08cca94795a5afa14bf8af39b8bf2c6f7df71ae`  
Validated functional head: `3ff2d6393c4e8734b4b1c08abd2bd8466f78f400`  
Interop Lab Smoke #42: GREEN  
EliteSCADA CI #886: GREEN after rerun of unrelated Modbus timing failures on unchanged SHA.

Common test peers on main: MQTT, CIP, OPC UA, IEC-104, DNP3, Siemens S7 and BACnet/IP. Peer health is not Driver product acceptance.

## Driver assignments

### D10 MQTT — READY FOR COORDINATOR CONVERGENCE

Branch `driver10/mqtt`; audited head `acd46cd9a4a49e324f2037a1994e6f579a0bae3f`; Draft #128; exact CI #865 GREEN.

Broad live evidence: Mosquitto + HiveMQ, MQTT 5/3.1.1, QoS/retained, TLS/auth, negative security, persistent broker restart, live freshness recovery.

Worker standby for targeted defects only. After v15 is complete, D10 is the first shared integration candidate.

### D6 IEC-104 — READY FOR COORDINATOR CONVERGENCE

Branch `driver6/iec-60870-5-104`; head `d597ef5ed1885b63dcd0b3568287bc1e34330bee`; Draft #146; CI #798 GREEN.

Accepted independent lib60870 L2 evidence: former validation PR #168, smoke #7 GREEN, 13/13. #168 is now closed unmerged as completed evidence.

### D5 Allen-Bradley CIP — READY FOR COORDINATOR CONVERGENCE

Branch `driver5/allen-bradley-cip`; head `18ff6dc989a65c1f8b006f83c08d8394a5510914`; Draft #111; CI #785 GREEN.

Accepted independent CIP L2 evidence: former validation PR #165 / smoke #6 GREEN. #165 is closed unmerged as completed evidence.

### D9 OPC UA — ACTIVE PRODUCT-PATH L2

Branch `driver9/opc-ua`; head `5ce1f3c912bf3779e892fb136b51b54b0f19a5c6`; Draft #169; CI #869 GREEN.

Next worker gate: actual Driver 9 session/read/write/subscription against common open62541, server loss/reconnect/resubscription, SourceTimestamp/ServerTimestamp preservation, dedicated L2 and exact normal CI. Secure/custom datatype evidence follows first green slice.

### D7 DNP3 — ACTIVE CANONICAL TYPE FIX

Branch `driver7/dnp3`; head `ac0dd6944f53d19447f3353addd404c02da7249c`; Draft #108; CI #697 GREEN.

Validation PR #167 remains OPEN. Real defect: configured `TagDataType.Int32` G30V1 value 4242 reaches canonical cache as `Double`. Worker must preserve configured canonical type, add regression and rerun #167. Never weaken the assertion.

### D8 Siemens S7 — ACTIVE PRODUCT-PATH L2

Branch `driver8/siemens-s7-iso`; head `0c37b922b44f591ebd143470abf3ebaa6b4bffae`; Draft #135; CI #789 GREEN.

Next gate against common python-snap7: ISO/S7 session, negotiated PDU, deterministic DB reads, write/readback, PDU-aware multi-read and peer restart/reconnect.

### D4 BACnet/IP — ACTIVE PRODUCT-PATH L2

Branch `driver4/bacnet`; head `de3357750f79266e43588e7bb26d66093f8cf3d5`; Draft #109; CI #860 GREEN.

Next gate against common BACpypes: Who-Is/I-Am, RP/RPM, WP/readback, COV and route loss/re-resolution/recovery. Priority/relinquish and BBMD/FDR follow when peer topology supports them.

## Convergence order

`MQTT -> IEC-104 -> CIP -> OPC UA -> DNP3 -> Siemens S7 -> BACnet/IP`

D9/D7/D8/D4 enter shared integration after their active worker gates close.

## Repository hygiene at handoff

Wave 10 issues #149-#152 are closed completed.

Validation-only PRs closed unmerged after evidence acceptance/supersession: #148, #160, #161, #162, #163, #164, #165, #166, #168.

Keep open: #175, Driver handoff PRs #108/#109/#111/#128/#135/#146/#169, and active DNP3 validation #167.

## Shared locks

- Engineering Preview/Apply/revisions/package fidelity is mandatory.
- No plaintext credentials/private keys/tokens in Engineering/package data.
- Stable TAG bit identity is `TagId + TagValueSelector`; `.NN` is display/authoring only.
- ADR-007 physical byte/word transform occurs before typed decode/bit selection.
- No Driver-to-Driver calls or bypass of TAG/cache/event architecture.
- Runtime readiness is Data Source/protocol readiness, not every point Good.
- L0/L1/L2/L3/L4, normal CI, licensing and conformance are separate claims.
- Never weaken a test to improve status.
- Wave 11 remains deferred until Driver convergence closes or priority is explicitly changed.

## Required worker handoff

Every worker handoff reports exact branch/head, delivered scope, changed files, exact CI/L2 evidence, limitations/risks, shared decisions needing Coordinator action, and confirmation that unassigned shared contracts were not redefined.