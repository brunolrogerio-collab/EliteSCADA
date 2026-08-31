# CHAT WORK ASSIGNMENTS — EliteSCADA

Date: 2026-08-30  
Stage: **DRIVER CONVERGENCE — ACTIVE / COMMON LAB MERGED / WAVE 11 DEFERRED**  
Integration owner: **Coordinator**  
Shared convergence issue: **#174**  
Coordinator integration branch: `coordination/driver-convergence-v3`  
Coordinator Draft PR: **#175**

## Priority

Wave 10 is CLOSED / MERGED / POST-MAIN GREEN. The common seven-peer interoperability laboratory is also MERGED. Current product priority is shared Driver convergence plus the remaining protocol-owned product-path L2 gates before Wave 11.

GitHub live state is authoritative. Re-read exact branch/PR/CI state before every mutation because worker branches advance independently.

CI policy remains NORMAL. Independent peer health, Driver product-path L2, vendor/hardware validation and normal product CI are separate evidence dimensions.

## Coordinator — Driver Convergence v3

Issue: `#174 — Driver Convergence v1 — shared runtime, Engineering and mainline integration`

Long-lived branch:

`coordination/driver-convergence-v3`

Base when created:

`4fe2897daf7de1771e470742442e20259cdcfdf8`

Draft PR:

`#175 — Driver convergence v3 — shared host contracts`

Current implemented first slice on the integration branch:

1. fail-closed communication Driver module registry keyed by stable DriverType;
2. common runtime planner/factory component registry;
3. protocol-neutral Data Source readiness contract independent from point quality;
4. host-owned scoped protected-material resolver/lease seam;
5. focused fail-closed tests for duplicate registration, capability/provider mismatch, readiness, protected-material request validation, planner/factory mismatch and activation issues.

The old `coordination/driver-convergence-mainline-v2` branch is **reference-only / obsolete as a merge source**. It is heavily behind current `main` and contains an incomplete Engineering communication-binding attempt whose mapper expects a DTO field absent from the actual current contract. Do not merge or rebase it wholesale.

Next Coordinator slice is the canonical rich Communication TAG binding in Engineering schema **v15**, rebuilt against current main with JSON/CSV/Preview/Apply/package/persistence compatibility and `Address == CommunicationBinding.PortableAddress` migration discipline. `TagValueSelector` remains the canonical bit selector and ADR-007 transform occurs before selection.

After the shared Engineering/runtime foundation is coherent, converge accepted Drivers in this evidence order:

`MQTT -> IEC-104 -> CIP -> OPC UA -> DNP3 -> Siemens S7 -> BACnet/IP`

Workers must not independently recreate shared contracts.

## Common interoperability lab — MERGED

PR #173 is MERGED to `main`.

Merge:

`a08cca94795a5afa14bf8af39b8bf2c6f7df71ae`

Exact validated functional lab head:

`3ff2d6393c4e8734b4b1c08abd2bd8466f78f400`

Evidence:

- **Interop Lab Smoke #42 — SUCCESS**;
- **EliteSCADA CI #886 — SUCCESS** after failed jobs were rerun on the unchanged functional SHA;
- no Modbus/product code was changed to obtain the green rerun;
- S7 peer build/start + TCP readiness GREEN;
- BACnet/IP peer build/start + health GREEN;
- MQTT round-trip GREEN;
- OPC UA independent browse/read/write/subscription reference smoke GREEN.

Common test peers on main: MQTT, CIP, OPC UA, IEC-104, DNP3, Siemens S7 and BACnet/IP.

The laboratory is test infrastructure, never a second product runtime and never production dependency authority. Tool/peer health is not automatic Driver product acceptance.

## Driver assignments

### Driver 10 — MQTT Industrial — READY FOR COORDINATOR CONVERGENCE

Branch: `driver10/mqtt`  
Observed worker checkpoint: `acd46cd9a4a49e324f2037a1994e6f579a0bae3f`  
Normal exact-head CI #865: GREEN.

Broad live evidence already includes Mosquitto + HiveMQ, MQTT 5/3.1.1, QoS/retained, TLS/authentication, negative certificate/credential cases, persistent broker restart and live freshness recovery.

Worker is standby for targeted MQTT defects only. Coordinator owns shared integration.

### Driver 6 — IEC 60870-5-104 — READY FOR COORDINATOR CONVERGENCE

Branch: `driver6/iec-60870-5-104`  
Product checkpoint: `d597ef5ed1885b63dcd0b3568287bc1e34330bee`  
Normal CI #798: GREEN.  
Validation PR #168: independent lib60870 L2 #7 GREEN, 13/13.

Worker is standby for targeted IEC-104 defects/evidence only. Coordinator owns shared integration.

### Driver 5 — Allen-Bradley Logix EtherNet/IP/CIP — READY FOR COORDINATOR CONVERGENCE

Branch: `driver5/allen-bradley-cip`  
Product checkpoint: `18ff6dc989a65c1f8b006f83c08d8394a5510914`  
Normal CI #785: GREEN.  
Validation PR #165: independent CIP L2 Smoke #6 GREEN.

Worker is standby for targeted CIP defects and later hardware/conformance evidence. Coordinator owns shared integration.

### Driver 9 — OPC UA — ACTIVE: PRODUCT-PATH L2

Branch: `driver9/opc-ua`  
Worker checkpoint: `5ce1f3c912bf3779e892fb136b51b54b0f19a5c6`  
Draft PR #169; normal CI #869 GREEN.

Assigned next milestone:

- actual Driver 9 session/read/write/subscription against common open62541 peer;
- server loss/reconnect/resubscription;
- independent SourceTimestamp and ServerTimestamp preservation;
- dedicated L2 + exact normal CI;
- follow-up secure username/certificate/custom-data-type evidence.

No shared registry/readiness/binding/security/module contracts may be redefined in this branch.

### Driver 7 — DNP3 — ACTIVE: CANONICAL TYPE FIX

Branch: `driver7/dnp3`  
Current product checkpoint: `ac0dd6944f53d19447f3353addd404c02da7249c`  
Draft PR #108; normal CI #697 GREEN.

Independent dnp3py L2 exposed a real product-boundary defect:

`G30V1 raw Int32 4242 -> Dnp3Driver -> canonical cache Double 4242`, while the configured TAG is `TagDataType.Int32`.

Assigned next milestone: preserve configured canonical type, add deterministic regression coverage, rerun validation PR #167 and exact normal CI. The L2 test must not be weakened to accept Double.

### Driver 8 — Siemens S7 ISO-on-TCP — ACTIVE: PRODUCT-PATH L2

Branch: `driver8/siemens-s7-iso`  
Worker checkpoint: `0c37b922b44f591ebd143470abf3ebaa6b4bffae`  
Draft PR #135; normal CI #789 GREEN.

The common python-snap7 peer has tool-level build/start/TCP evidence. Assigned next milestone: actual S7 connection/PDU negotiation, deterministic DB reads, write/readback, PDU-aware multi-read, peer restart/reconnect and dedicated L2 + normal CI.

### Driver 4 — BACnet/IP — ACTIVE: PRODUCT-PATH L2

Branch: `driver4/bacnet`  
Worker checkpoint: `de3357750f79266e43588e7bb26d66093f8cf3d5`  
Draft PR #109; normal CI #860 GREEN.

The common BACpypes peer is tool-level GREEN in Interop Lab Smoke #42. Assigned next milestone: Who-Is/I-Am, RP/RPM, WP/readback, COV, reachability/re-resolution/recovery and dedicated L2 + normal CI. Priority/relinquish and BBMD/FDR may follow when the peer scenario supports them.

## Shared locks

- Engineering Import/Export, Preview/Apply/CAS, revisions and project-package fidelity remain mandatory for public Engineering changes.
- Protected credentials/private keys are never plaintext Engineering/package data.
- TAG-bit identity remains stable `TagId + selector`; `.NN` is authoring/display only.
- ADR-007 byte/word transform remains binding-level; bit selection occurs after physical transform and typed decode.
- No arbitrary SQL, JavaScript `eval`/`Function`, unrestricted Python evaluation or implicit coercion engines.
- Visual precedence remains `Animation > Script > Binding/Expression > Engineering Base > Default`.
- Driver registry dispatch uses stable Driver type and duplicate registrations fail closed.
- Runtime readiness means protocol/Data Source readiness, not every point being `Good`.
- Drivers never call sibling Drivers or bypass TAG/cache/event architecture.

## Required worker handoff

Every worker handoff reports:

1. exact branch/head SHA;
2. delivered scope;
3. exact changed-file list;
4. tests/results and exact CI/L2 evidence;
5. known limitations/risks;
6. shared decisions requiring Coordinator action;
7. confirmation that no unassigned shared contracts were independently redefined.
