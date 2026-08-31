# LAST CHANGE — EliteSCADA

Date: 2026-08-30

## Current checkpoint

### MERGED

**Wave 10 is CLOSED / MERGED / POST-MAIN GREEN.**

Final Wave 10 product merge:

`15daff2cc076f46f9433812babbd5cbb4b8d9554`

Evidence:

- Wave 10 integration CI #873: SUCCESS;
- exact post-main CI #874: SUCCESS across Backend, Web and Chromium.

**Common seven-peer interoperability laboratory is MERGED on `main`.**

PR #173 — `Driver interoperability lab — common multi-protocol peer stack`

Merge commit:

`a08cca94795a5afa14bf8af39b8bf2c6f7df71ae`

Exact validated functional head:

`3ff2d6393c4e8734b4b1c08abd2bd8466f78f400`

Evidence on that exact functional head:

- Interop Lab Smoke #42: SUCCESS;
- EliteSCADA CI #886: SUCCESS after rerunning the failed jobs on the unchanged SHA;
- the first CI #886 backend attempt had two unrelated Modbus timing failures; no Modbus/product code was changed to obtain the green rerun;
- the three commits after `3ff2d639...` and before merge were coordination-documentation-only commits marked `[skip ci]`.

The common test-only peer stack now includes:

- MQTT — Eclipse Mosquitto + Node-RED;
- Allen-Bradley CIP — pinned ControlLogix + CompactLogix simulator profiles;
- OPC UA — open62541 1.5.4 + independent node-opcua reference client;
- IEC 60870-5-104 — pinned lib60870-C outstation;
- DNP3 — pinned dnp3py outstation;
- Siemens S7 ISO-on-TCP — python-snap7 3.1.2 deterministic server;
- BACnet/IP — BACpypes 0.19.0 independent device peer.

Smoke #42 proves common peer/tool readiness, including S7 TCP readiness, BACnet health, MQTT round-trip and OPC UA browse/read/write/subscription reference behavior. Peer readiness is not automatically Driver product acceptance.

### IMPLEMENTED IN PR / ACTIVE WORKER BRANCHES

Current Driver evidence and assigned gates:

- Driver 4 BACnet/IP: product checkpoint `de3357750f79266e43588e7bb26d66093f8cf3d5`, CI #860 green; common BACpypes peer now green; active next gate is product-path L2 for Who-Is/I-Am, RP/RPM, WP, COV and recovery.
- Driver 5 CIP: product checkpoint `18ff6dc989a65c1f8b006f83c08d8394a5510914`, CI #785 green; independent L2 PR #165 / smoke #6 green; parked for Coordinator convergence.
- Driver 6 IEC-104: product checkpoint `d597ef5ed1885b63dcd0b3568287bc1e34330bee`, CI #798 green; independent L2 PR #168 / smoke #7 green, 13/13; parked for Coordinator convergence.
- Driver 7 DNP3: product checkpoint `ac0dd6944f53d19447f3353addd404c02da7249c`, CI #697 green; independent peer reaches Online but product L2 is red on configured Int32 -> canonical Double mismatch; active worker fix must preserve configured type before rerun.
- Driver 8 Siemens S7: product checkpoint `0c37b922b44f591ebd143470abf3ebaa6b4bffae`, CI #789 green; common python-snap7 peer is green; active next gate is product-path L2 read/write/PDU/reconnect.
- Driver 9 OPC UA: product checkpoint `5ce1f3c912bf3779e892fb136b51b54b0f19a5c6`, Draft PR #169, CI #869 green; common open62541 peer/reference smoke green; active next gate is product-path read/write/subscription/reconnect L2.
- Driver 10 MQTT: product checkpoint `acd46cd9a4a49e324f2037a1994e6f579a0bae3f`, exact CI #865 green; Mosquitto/HiveMQ, TLS/auth, negative security, restart and freshness evidence green; parked for Coordinator convergence.

### SPECIFIED / NOT IMPLEMENTED

**Shared Coordinator Driver convergence — issue #174** remains the current product priority before Wave 11.

Coordinator-owned shared scope must be implemented once rather than copied into protocol branches:

1. fail-closed DriverHost registry/planner/factory and central activation;
2. canonical rich Communication TAG binding and compatibility migration;
3. common Data Source readiness activation;
4. protected credential/certificate/private-key resolution;
5. module/catalog/loading policy;
6. common rich command/operation surface where simple `WriteAsync` is insufficient;
7. SourceTimestamp/ServerTimestamp/current-value/historical-event ordering policy;
8. central Engineering ConnectionTest/Browse/Import/Reconcile registration and protected API/UI;
9. exact integrated CI before accepted Driver transitions to `main`.

Evidence-driven convergence order:

`MQTT -> IEC-104 -> CIP -> OPC UA -> DNP3 -> Siemens S7 -> BACnet/IP`

OPC UA, DNP3, S7 and BACnet enter shared integration after their active product L2/fix gates close.

**Wave 11 remains DEFERRED**, with scope unchanged, until Driver convergence is completed or explicitly reprioritized.

## CI policy

CI mode remains **NORMAL**. Documentation-only checkpoints may use `[skip ci]`. Exact functional integration heads require green evidence. Normal CI, peer/tool readiness, independent Driver L2, licensing/conformance and L3/L4 hardware/vendor acceptance are separate claims. Never weaken a test to hide a real canonical protocol/type mismatch.