# DRIVER AND INTEROPERABILITY LAB STATUS — EliteSCADA

Date: 2026-08-30  
Status: **DRIVER CONVERGENCE + INTEROPERABILITY LAB ACTIVE / WAVE 11 DEFERRED**

This is a coordination snapshot, not a substitute for live GitHub state. Re-read exact branches, PRs and workflow runs before every mutation or acceptance claim.

## Driver snapshot

| Driver | Observed product checkpoint | Normal CI / handoff | Independent-peer state | Current action |
| --- | --- | --- | --- | --- |
| D4 BACnet/IP | `de3357750f79266e43588e7bb26d66093f8cf3d5` | Draft #109; CI #860 GREEN | Common BACpypes peer tool gate GREEN in Lab #42 | **ACTIVE L2**: Who-Is/I-Am, RP/RPM, WP, COV, reconnect/re-resolution. |
| D5 Allen-Bradley CIP | `18ff6dc989a65c1f8b006f83c08d8394a5510914` | Draft #111; CI #785 GREEN | PR #165; CIP L2 #6 GREEN | **PARKED FOR COORDINATOR CONVERGENCE**. |
| D6 IEC-104 | `d597ef5ed1885b63dcd0b3568287bc1e34330bee` | Draft #146; CI #798 GREEN | PR #168; lib60870 L2 #7 GREEN, 13/13 | **PARKED FOR COORDINATOR CONVERGENCE**. |
| D7 DNP3 | `ac0dd6944f53d19447f3353addd404c02da7249c` | Draft #108; CI #697 GREEN | dnp3py peer healthy; product L2 RED on Int32->Double canonical mismatch | **ACTIVE FIX** then rerun L2. |
| D8 Siemens S7 | `0c37b922b44f591ebd143470abf3ebaa6b4bffae` | Draft #135; CI #789 GREEN | Common python-snap7 peer build/start/TCP GREEN | **ACTIVE L2**: session/PDU/DB read-write/reconnect. |
| D9 OPC UA | `5ce1f3c912bf3779e892fb136b51b54b0f19a5c6` | Draft #169; CI #869 GREEN | Common open62541 peer/reference smoke GREEN | **ACTIVE L2**: real Driver 9 read/write/subscription/reconnect. |
| D10 MQTT | `acd46cd9a4a49e324f2037a1994e6f579a0bae3f` | Draft #128; exact CI #865 GREEN | Mosquitto + HiveMQ + TLS/auth + negative security + restart + freshness evidence GREEN | **PARKED FOR COORDINATOR CONVERGENCE**. |

## Evidence levels

- **L0** — unit/codec/contract tests;
- **L1** — same-stack/in-process/loopback protocol evidence;
- **L2** — independent software peer over real wire protocol;
- **L3** — representative vendor simulator/device evidence;
- **L4** — representative hardware/site acceptance.

Normal CI, independent interoperability, licensing/conformance and hardware acceptance remain separate gates.

## Common interoperability lab

Branch: `integration/driver-interop-lab-finalization`  
PR: **#173**  
Exact functional head: `3ff2d6393c4e8734b4b1c08abd2bd8466f78f400`

### Dedicated gate

**Interop Lab Smoke #42 — SUCCESS.**

Proven in one workflow:

- all Compose models validate;
- base five-peer stack builds/starts: MQTT, CIP, OPC UA, IEC-104, DNP3;
- Siemens S7 peer builds/starts and TCP readiness passes;
- BACnet/IP peer builds/starts and explicit health passes;
- MQTT round-trip passes;
- OPC UA independent browse/read/write/subscription smoke passes;
- cleanup passes.

The common peer/tool set is therefore no longer a blocker for D4/D8/D9 product-path validation.

### Common peers

- MQTT — Eclipse Mosquitto + Node-RED;
- CIP — pinned ControlLogix/CompactLogix simulator profiles;
- OPC UA — open62541 1.5.4 + node-opcua reference client;
- IEC-104 — pinned lib60870-C deterministic outstation;
- DNP3 — pinned dnp3py 0.4.0 outstation;
- S7 ISO-on-TCP — python-snap7 3.1.2 deterministic server;
- BACnet/IP — BACpypes 0.19.0 independent device peer.

Third-party peers remain test-only infrastructure.

### Normal product CI on lab head

EliteSCADA CI #886 exact functional head `3ff2d639...`:

- initial Web/build were green;
- initial backend test attempt hit two unrelated Modbus timing failures (`ModbusTcpDiagnosticsTests` 500 ms request timeout and `GatewayRuntimeSameProtocolTests` 4 s condition timeout);
- no Modbus/product code was changed in response;
- failed workflow jobs were rerun on the exact same functional head;
- merge requires the rerun plus Chromium to be fully green.

The prior lab head `67d4da30...` had normal CI #884 fully green, which supports the classification of the #886 first-attempt failures as timing flakes rather than a lab-source regression, but it does not replace the required exact-head rerun.

## Product-path evidence already accepted

### D10 MQTT

Broad live evidence includes:

- Eclipse Mosquitto + HiveMQ Community Edition;
- MQTT 5.0 and 3.1.1;
- QoS 0/1/2 and retained;
- trusted TLS + mandatory authentication;
- invalid credentials and revoked certificate fail closed;
- persistent sessions across real broker restart;
- live freshness `Good -> Stale -> Good` without redefining broker readiness.

Current product checkpoint `acd46cd9...` has exact normal CI #865 GREEN. D10 now waits on shared Coordinator convergence, not generic MQTT feature growth.

### D6 IEC-104

Validation PR #168 passes 13/13 against independent lib60870-C over real TCP, including STARTDT, GI, spontaneous data, readiness, all five first-release command types Direct + SBO and peer restart/reconnect without command replay.

D6 now waits on shared Coordinator convergence plus later L3/L4/security decisions.

### D5 CIP

Validation PR #165 passes the independent CIP L2 gate through real RegisterSession/SendRRData, typed reads, write/readback and Driver polling/cache behavior. Hardware/ODVA/CIP Security remain separate later gates.

## Active worker gates

### D7 DNP3

Independent dnp3py evidence reaches `Online`, receives BI/AI/Counter Good and reports zero communication failures. The real mismatch is:

`configured Int32 G30V1 -> raw System.Int32 4242 -> canonical cache System.Double 4242`.

Worker must preserve canonical configured type and rerun PR #167. Do not weaken the L2 assertion.

### D9 OPC UA

Use common open62541 to prove the actual Driver 9 path: endpoint/session, stable NodeIds, typed reads/writes, monitored-item delivery, server restart/reconnect/resubscription and timestamp preservation. Secure identity/custom datatype cases follow the first green slice.

### D8 Siemens S7

Use common python-snap7 peer for real ISO-on-TCP/S7 Setup Communication, negotiated PDU, deterministic DB reads, write/readback, PDU-aware multi-read and stop/start recovery.

### D4 BACnet/IP

Use common BACpypes peer for Who-Is/I-Am, stable Device Instance resolution, RP/RPM, WP/readback, COV and route loss/recovery. Priority/relinquish and BBMD/FDR are follow-up scenarios when peer capability/topology permits.

## Shared Coordinator convergence — issue #174

Coordinator owns once:

1. Driver registry/planner/factory + central activation;
2. canonical rich Communication TAG binding + compatibility migration;
3. common readiness activation;
4. protected credential/certificate/private-key resolution;
5. module/catalog/loading policy;
6. common rich command/operation surface;
7. SourceTimestamp/ServerTimestamp/current/history ordering policy;
8. central Engineering ConnectionTest/Browse/Import/Reconcile API/UI;
9. exact integration/main CI.

Current intended order:

`MQTT -> IEC-104 -> CIP -> OPC UA -> DNP3 -> Siemens S7 -> BACnet/IP`

## Coordinator rules

- no direct Driver-branch merge merely because isolated CI is green;
- no protocol-private copy of shared contracts;
- no Driver-to-Driver calls;
- no plaintext protected material;
- no test weakening to hide a real type/protocol mismatch;
- no L2 claim presented as L3/L4 certification;
- current `main` wins implementation conflicts while locked architecture/ADR intent governs shared future contracts.
