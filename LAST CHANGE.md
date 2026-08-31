# LAST CHANGE — EliteSCADA

Date: 2026-08-30

## Current checkpoint

### MERGED

**Wave 10 is CLOSED / MERGED / POST-MAIN GREEN.**

Final product merge:

`15daff2cc076f46f9433812babbd5cbb4b8d9554`

Evidence:

- Wave 10 integration CI #873: SUCCESS;
- exact post-main CI #874: SUCCESS across Backend, Web and Chromium.

### IMPLEMENTED IN PR / INTEGRATION BRANCH

Project priority has changed to **Driver Convergence + Interoperability Lab before Wave 11**.

Active laboratory integration branch:

`integration/driver-interop-lab-finalization`

The common lab branch is based on post-Wave-10 `main` and currently implements:

- existing Mosquitto + Node-RED control plane;
- existing pinned ControlLogix + CompactLogix CIP peers;
- open62541 1.5.4 OPC UA peer with independent node-opcua browse/read/write/subscription smoke;
- pinned lib60870-C IEC-104 deterministic outstation;
- pinned dnp3py independent DNP3 outstation;
- unified Linux/Git-Bash and PowerShell commands for all peers and protocol-specific start/status/stop;
- common CI composition that validates/builds/starts all implemented peers and runs the base MQTT + OPC UA reference smokes;
- refreshed machine-readable scenario catalog and driver/lab coordination status.

The common lab must pass its own exact-head interoperability workflow and normal EliteSCADA CI before integration to `main`.

Current Driver evidence materially supersedes the older parked snapshot:

- Driver 4 BACnet/IP: worker head `de3357750f79266e43588e7bb26d66093f8cf3d5`, CI #860 green; independent peer still missing.
- Driver 5 CIP: worker head `18ff6dc989a65c1f8b006f83c08d8394a5510914`, CI #785 green; validation PR #165 L2 smoke #6 green.
- Driver 6 IEC-104: worker head `d597ef5ed1885b63dcd0b3568287bc1e34330bee`, CI #798 green; validation PR #168 L2 #7 green, 13/13.
- Driver 7 DNP3: worker head `ac0dd6944f53d19447f3353addd404c02da7249c`, CI #697 green; independent dnp3py validation reaches Online and receives all points but product L2 remains RED because configured Int32 G30V1 is published into canonical cache as Double.
- Driver 8 Siemens S7: worker head `0c37b922b44f591ebd143470abf3ebaa6b4bffae`, CI #789 green; independent peer still missing.
- Driver 9 OPC UA: worker head `5ce1f3c912bf3779e892fb136b51b54b0f19a5c6`, Draft PR #169 and CI #869 green; common open62541 peer now removes the basic L2 tooling blocker.
- Driver 10 MQTT: worker head `232383ec4b51b38775f674bf375cf7f7f595b875`, CI #858 green; independent live evidence includes Mosquitto + HiveMQ, TLS/auth, negative security, broker restart and live freshness. Freshness validation head `25a23c028fb096d77d51ff527a5d74ac54be7736` has all dedicated workflows plus CI #852 green.

### SPECIFIED / NOT IMPLEMENTED

Wave 11 remains **DEFERRED**, with scope unchanged, until Driver convergence is completed or explicitly reprioritized.

Laboratory gaps still to implement:

1. independent Siemens S7 ISO-on-TCP peer/simulator;
2. independent BACnet/IP peer/reference device with RP/RPM/WP/COV and later BBMD/FDR scenarios.

Driver 7 still requires a protocol-owned correction for canonical Analog Input type preservation before its independent DNP3 L2 test may be called green.

Shared Coordinator-owned Driver convergence remains to be implemented once the common lab is accepted:

1. fail-closed DriverHost registry/planner/factory composition;
2. canonical rich Communication TAG binding and compatibility migration;
3. common Data Source readiness activation;
4. protected credential/certificate/private-key resolution;
5. installable module/catalog/loading policy;
6. common rich operation/command surface where simple `WriteAsync` is insufficient;
7. source timestamp/current-value/historical-event ordering policy;
8. central Engineering ConnectionTest/Browse/Import/Reconcile registration and protected API/UI exposure;
9. exact integrated CI before each Driver mainline transition.

Planned convergence order after common lab validation: MQTT -> IEC-104 -> CIP -> OPC UA -> DNP3 after type fix -> S7 after peer evidence -> BACnet after peer evidence.

## CI policy

CI mode remains **NORMAL**. Exact integration heads require green evidence. Independent peer health and Driver product-path acceptance are separate claims. Never weaken a test to hide a canonical protocol/type mismatch.