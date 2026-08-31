# DRIVER AND INTEROPERABILITY LAB STATUS — EliteSCADA

Last evidence audit: **2026-08-31 BRT**  
Scope: **LABORATORY EVIDENCE ONLY**

> Current coordinator implementation state, branch head, merge gates and next action live in `CURRENT-COORDINATOR-HANDOFF.md`.
>
> This file deliberately separates **peer laboratory health** from **EliteSCADA Driver product-path acceptance**. A healthy simulator/server is not proof that the product Driver passed L2.

## Evidence levels

- **L0** — unit/codec/contracts;
- **L1** — same-stack/in-process/loopback;
- **L2** — independent software peer over real wire protocol;
- **L3** — representative vendor simulator/device;
- **L4** — representative hardware/site.

Normal CI, interoperability, licensing/conformance and hardware acceptance are separate gates.

## Common seven-peer laboratory

Status: **MERGED / INFRASTRUCTURE HEALTH ACCEPTED**.

- PR #173 merged to `main`;
- merge commit: `a08cca94795a5afa14bf8af39b8bf2c6f7df71ae`;
- validated functional head: `3ff2d6393c4e8734b4b1c08abd2bd8466f78f400`;
- Interop Lab Smoke #42: **SUCCESS**;
- EliteSCADA CI #886: **SUCCESS** after an unchanged-code rerun resolved unrelated Modbus timing noise.

Common peers/tooling:

| Protocol | Independent lab peer/tool | Peer infrastructure |
| --- | --- | --- |
| MQTT | Eclipse Mosquitto + Node-RED; HiveMQ used in accepted product evidence | **GREEN** |
| CIP / EtherNet/IP | pinned ControlLogix/CompactLogix simulator profiles | **GREEN** |
| OPC UA | open62541 1.5.4 + node-opcua reference tooling | **GREEN** |
| IEC-104 | pinned lib60870-C deterministic outstation | **GREEN** |
| DNP3 | pinned dnp3py outstation | **GREEN** |
| Siemens S7 ISO-on-TCP | python-snap7 3.1.2 server | **GREEN** |
| BACnet/IP | BACpypes 0.19.0 peer | **GREEN** |

Therefore common peer infrastructure is **7/7 healthy**. That is not the product L2 score.

## EliteSCADA Driver product-path L2

| Driver | L2 state | Accepted / missing evidence |
| --- | --- | --- |
| D10 MQTT | **PASS / ACCEPTED** | Mosquitto + HiveMQ, MQTT 5/3.1.1, QoS 0/1/2, retained delivery, TLS/auth, negative credentials/certificate behavior, persistent session/restart, `Good -> Stale -> Good` recovery. |
| D6 IEC-104 | **PASS / ACCEPTED 13/13** | lib60870-C: TCP/STARTDT, GI, spontaneous process data, readiness, five first-release command types in Direct + SBO, peer restart/reconnect and no command replay. |
| D5 CIP | **PASS / ACCEPTED** | Independent CIP: RegisterSession/SendRRData, typed reads, write/readback and Driver polling/cache behavior. |
| D7 DNP3 | **FAIL / PRODUCT DEFECT** | Association and startup integrity communicate successfully, but configured `TagDataType.Int32` / G30V1 raw `System.Int32 4242` reaches canonical cache as `System.Double 4242`. Product fix and exact L2 rerun required. |
| D9 OPC UA | **PENDING** | open62541 peer/reference smoke is healthy. Actual EliteSCADA Driver endpoint/session, stable NodeId read/write, monitored-item delivery, reconnect/resubscribe and timestamp product path still requires accepted L2. |
| D8 Siemens S7 | **PENDING** | python-snap7 peer build/start/TCP is healthy. Actual Driver Setup Communication, negotiated PDU, deterministic DB reads, write/readback, PDU-aware multi-read and stop/start recovery still requires accepted L2. |
| D4 BACnet/IP | **PENDING** | BACpypes peer is healthy. Actual Driver Who-Is/I-Am, Device Instance resolution, RP/RPM, WP/readback, COV and route-loss/re-resolution recovery still requires accepted L2. |

Current product-path result:

- **3 accepted**;
- **1 executed and failed with a real product defect**;
- **3 pending completion**.

Do not summarize this as “four Drivers failed”.

## DNP3 defect boundary

The DNP3 L2 failure is deliberately retained as a product gate:

`configured TagDataType.Int32 / G30V1 -> raw System.Int32 4242 -> canonical cache System.Double 4242`

Communication is healthy. The configured canonical type is not preserved. The assertion must not be weakened to accept `Double`.

Validation PR #167 remains evidence-only until the product fix is made and exact L2 is rerun.

## Current pending L2 campaigns

### OPC UA

Prove the actual Driver against the common open62541 peer:

- endpoint/session;
- stable NodeId read/write;
- monitored-item delivery;
- reconnect/resubscribe;
- SourceTimestamp/ServerTimestamp preservation.

### Siemens S7

Prove the actual Driver against python-snap7:

- ISO/S7 session;
- Setup Communication;
- negotiated PDU;
- deterministic DB reads;
- write/readback;
- PDU-aware multi-read;
- peer stop/start and reconnect.

### BACnet/IP

Prove the actual Driver against BACpypes:

- Who-Is/I-Am;
- Device Instance resolution;
- RP/RPM;
- WP/readback;
- COV;
- route loss, re-resolution and recovery.

Priority/relinquish, BBMD/FDR and broader vendor evidence remain additional gates where applicable; they do not need to be mislabeled as already-proven first L2.

## Claim discipline

- Peer container/service health is not Driver acceptance.
- Unit/normal CI green is not L2.
- L2 is not L3/L4.
- A validation branch is evidence, not automatic product merge authorization.
- Red unrelated CI still prevents exact-head acceptance until classified or rerun; do not inherit a prior green SHA.
- Licensing and protocol conformance remain separate from interoperability.
