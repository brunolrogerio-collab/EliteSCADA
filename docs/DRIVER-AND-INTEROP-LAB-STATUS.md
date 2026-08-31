# DRIVER AND INTEROPERABILITY LAB STATUS — EliteSCADA

Date: 2026-08-30 (BRT)  
Status: **DRIVER CONVERGENCE ACTIVE / COMMON SEVEN-PEER LAB MERGED / WAVE 11 DEFERRED**

This is a coordination snapshot. Re-read exact branch/PR/Actions state before every mutation or acceptance claim.

## Driver snapshot

| Driver | Audited product head | Product CI / handoff | Independent-peer evidence | Current action |
| --- | --- | --- | --- | --- |
| D4 BACnet/IP | `de3357750f79266e43588e7bb26d66093f8cf3d5` | Draft #109; CI #860 GREEN | Common BACpypes peer green in Lab #42 | **ACTIVE L2**: discovery, RP/RPM, WP, COV, recovery. |
| D5 Allen-Bradley CIP | `18ff6dc989a65c1f8b006f83c08d8394a5510914` | Draft #111; CI #785 GREEN | CIP L2 #6 GREEN; evidence PR #165 closed unmerged after acceptance | **READY FOR COORDINATOR CONVERGENCE**. |
| D6 IEC-104 | `d597ef5ed1885b63dcd0b3568287bc1e34330bee` | Draft #146; CI #798 GREEN | lib60870 L2 #7 GREEN, 13/13; evidence PR #168 closed unmerged after acceptance | **READY FOR COORDINATOR CONVERGENCE**. |
| D7 DNP3 | `ac0dd6944f53d19447f3353addd404c02da7249c` | Draft #108; CI #697 GREEN | PR #167 remains OPEN/RED on real Int32->Double canonical mismatch | **ACTIVE PRODUCT FIX**, then rerun L2. |
| D8 Siemens S7 | `0c37b922b44f591ebd143470abf3ebaa6b4bffae` | Draft #135; CI #789 GREEN | Common python-snap7 peer build/start/TCP green | **ACTIVE L2**: session/PDU/DB read-write/reconnect. |
| D9 OPC UA | `5ce1f3c912bf3779e892fb136b51b54b0f19a5c6` | Draft #169; CI #869 GREEN | Common open62541 peer/reference smoke green | **ACTIVE L2**: actual Driver read/write/subscription/reconnect. |
| D10 MQTT | `acd46cd9a4a49e324f2037a1994e6f579a0bae3f` | Draft #128; exact CI #865 GREEN | Mosquitto + HiveMQ + TLS/auth + negative security + restart + freshness green | **READY FOR COORDINATOR CONVERGENCE**. |

## Evidence levels

- **L0** — unit/codec/contracts;
- **L1** — same-stack/in-process/loopback;
- **L2** — independent software peer over real wire protocol;
- **L3** — representative vendor simulator/device;
- **L4** — representative hardware/site.

Normal CI, interoperability, licensing/conformance and hardware acceptance are separate gates.

## Common interoperability lab — MERGED

PR #173 is merged on `main`.

- merge: `a08cca94795a5afa14bf8af39b8bf2c6f7df71ae`;
- exact validated functional head: `3ff2d6393c4e8734b4b1c08abd2bd8466f78f400`;
- Interop Lab Smoke #42: SUCCESS;
- EliteSCADA CI #886: SUCCESS after rerunning unrelated Modbus timing failures on the unchanged functional SHA.

Common test-only peers:

- MQTT — Eclipse Mosquitto + Node-RED;
- CIP — pinned ControlLogix/CompactLogix simulator profiles;
- OPC UA — open62541 1.5.4 + node-opcua reference client;
- IEC-104 — pinned lib60870-C deterministic outstation;
- DNP3 — pinned dnp3py outstation;
- Siemens S7 ISO-on-TCP — python-snap7 3.1.2 server;
- BACnet/IP — BACpypes 0.19.0 peer.

The common peer/tool set is no longer the generic blocker for D4/D8/D9. Peer health remains distinct from actual Driver product acceptance.

## Accepted product-path evidence

### D10 MQTT

Accepted evidence covers two broker implementations, MQTT 5/3.1.1, QoS 0/1/2, retained delivery, trusted TLS/authentication, invalid credential/revoked certificate fail-closed behavior, persistent sessions across broker restart and live `Good -> Stale -> Good` recovery.

Validation-only PRs #160-#164 are now closed unmerged after evidence acceptance. Product work remains in #128.

### D6 IEC-104

Accepted independent lib60870-C L2 is 13/13 green, including TCP/STARTDT, GI, spontaneous process data, readiness, all five first-release command types in Direct + SBO, peer restart/reconnect and no command replay. Validation PR #168 is closed unmerged after evidence acceptance.

### D5 CIP

Accepted independent CIP L2 exercises RegisterSession/SendRRData, typed reads, write/readback and Driver polling/cache behavior. Validation PR #165 is closed unmerged after evidence acceptance.

## Active worker gates

### D7 DNP3

PR #167 remains open because the product defect remains unresolved:

`configured TagDataType.Int32 / G30V1 -> raw System.Int32 4242 -> canonical cache System.Double 4242`.

Communications are healthy. Worker must preserve configured canonical type, add deterministic regression coverage and rerun exact L2. Do not weaken the assertion.

### D9 OPC UA

Use the common open62541 peer to prove actual Driver 9 endpoint/session, stable NodeId read/write, monitored-item delivery, reconnect/resubscribe and SourceTimestamp/ServerTimestamp preservation. Secure/custom-data-type cases follow the first green product-path slice.

### D8 Siemens S7

Use common python-snap7 to prove S7 Setup Communication, negotiated PDU, deterministic DB reads, write/readback, PDU-aware multi-read and stop/start recovery.

### D4 BACnet/IP

Use common BACpypes to prove Who-Is/I-Am, stable Device Instance resolution, RP/RPM, WP/readback, COV and route loss/re-resolution/recovery. Priority/relinquish and BBMD/FDR follow when peer scenario supports them.

## Shared Coordinator convergence — issue #174 / PR #175

Branch: `coordination/driver-convergence-v3`  
Draft PR: #175  
Exact audited head: `06c7d408c76926bf5d37dfec4be20ea6044f52b1`  
Exact normal CI: #895 GREEN.

Implemented shared foundation:

1. fail-closed module registry;
2. common planner/factory registry;
3. protocol-neutral readiness contract;
4. scoped protected-material resolver/lease seam;
5. partial Communication TAG binding scaffold.

### Binding-v15 audit gate

The rich binding scaffold is **not functionally complete**:

- Engineering current schema remains v14;
- new binding validator is not called from TAG Preview;
- TAG Apply drops `CommunicationBinding`;
- CSV fidelity is missing;
- complete JSON/CSV/package/revision/PostgreSQL round-trip tests are missing.

Next Coordinator must complete v15 end-to-end before adapting MQTT. Then intended convergence order is:

`MQTT -> IEC-104 -> CIP -> OPC UA -> DNP3 -> Siemens S7 -> BACnet/IP`

## Repository hygiene

Closed unmerged as accepted/superseded evidence during handoff audit: #148, #160, #161, #162, #163, #164, #165, #166, #168.

Keep open: #175, Driver handoff PRs #108/#109/#111/#128/#135/#146/#169, and active DNP3 validation #167.

## Coordinator rules

- no direct Driver-branch merge merely because isolated CI is green;
- no protocol-private copy of shared contracts;
- no Driver-to-Driver calls;
- no plaintext protected material;
- `Address == CommunicationBinding.PortableAddress` during v15 migration;
- `TagValueSelector` remains the sole generic bit selector;
- ADR-007 physical transform precedes bit selection;
- no test weakening to hide a real type/protocol mismatch;
- no L2 claim presented as L3/L4 certification;
- current `main` wins implementation conflicts while locked architecture/ADR intent governs future shared contracts.