# EliteSCADA Driver Interoperability Lab

This directory is a destructive, reproducible protocol interoperability lab. It is deliberately separate from the EliteSCADA product runtime and from ordinary unit tests.

The lab has two roles:

1. **Node-RED is the control plane**: scenario orchestration, value generation, fault-injection coordination and reference-client tooling.
2. **Independent protocol peers are the data plane**: brokers, PLC simulators, outstations and servers that exercise EliteSCADA over the real wire protocol.

The lab is not a substitute for later representative hardware/vendor acceptance. Its purpose is to expose protocol, lifecycle, type, quality, timestamp and command defects before hardware time is consumed.

## Common peer stack

| Driver | Lab peer | Common-lab state | Product-path evidence |
| --- | --- | --- | --- |
| MQTT | Eclipse Mosquitto 2.1.2 + Node-RED | **Runnable** | Separate validation lines have exercised Driver 10 against Mosquitto and HiveMQ, TLS/auth and broker restart. |
| Allen-Bradley EtherNet/IP/CIP | pinned `node-red-contrib-cip-suite` ControlLogix + CompactLogix simulators | **Runnable** | Driver 5 validation PR #165: L2 smoke green. |
| OPC UA | open62541 1.5.4 + independent `node-opcua` reference client | **Runnable** | Peer/reference-client L2 smoke green; Driver 9 product-path L2 is the next step. |
| IEC 60870-5-104 | pinned MZ Automation lib60870-C outstation | **Runnable** | Driver 6 validation PR #168: L2 13/13 green. |
| DNP3 | pinned `dnp3py` independent outstation | **Runnable peer** | Driver 7 validation PR #167 currently exposes a canonical analog-value type mismatch; do not call product L2 green. |
| Siemens S7 ISO-on-TCP | independent S7 server/PLC simulator | **Not yet implemented** | Driver 8 software/CI is green; external peer remains a lab priority. |
| BACnet/IP | independent BACnet/IP device simulator/reference peer | **Not yet implemented** | Driver 4 software/CI is green; independent COV/RPM/WP/BBMD evidence remains a lab priority. |

Machine-readable status lives in `scenarios/catalog.json`.

## One-command startup

Linux/macOS/Git Bash:

```bash
cd interop-lab
cp .env.example .env
bash scripts/lab.sh all-start
bash scripts/lab.sh status
bash scripts/lab.sh smoke
```

Windows PowerShell:

```powershell
cd interop-lab
Copy-Item .env.example .env
./scripts/lab.ps1 all-start
./scripts/lab.ps1 status
./scripts/lab.ps1 smoke
```

`all-start` builds and starts every peer currently implemented in the common lab. `reset` removes all current peer containers and volumes.

## Base control plane and MQTT

Node-RED: `http://localhost:1880`

Control-plane health: `http://localhost:1880/lab/health`

Mosquitto: `localhost:1883`

The default EliteSCADA URL from inside the lab is `http://host.docker.internal:5000`. Override `ELITESCADA_BASE_URL` in `.env` if the runtime is elsewhere.

Useful commands:

```bash
bash scripts/lab.sh start
bash scripts/lab.sh smoke
```

## Allen-Bradley EtherNet/IP/CIP

The CIP overlay builds independent ControlLogix and CompactLogix simulator profiles from pinned upstream commit `baf21c625c4f1250fa3cbae6cdd636ce15620ef2`.

```bash
bash scripts/lab.sh cip-start
bash scripts/lab.sh cip-status
```

Exposed peers:

- ControlLogix: `localhost:44818`
- CompactLogix: `localhost:44819`

The simulator is external test infrastructure only and is not shipped as an EliteSCADA runtime dependency.

## OPC UA

The OPC UA overlay builds an **open62541 1.5.4** server from official single-file release assets pinned by SHA-256. `node-opcua` in the Node-RED image is used as an independent reference client.

```bash
bash scripts/lab.sh opcua-start
bash scripts/lab.sh opcua-smoke
```

Exposed peer:

- open62541 OPC UA server: `opc.tcp://localhost:4841`

The reference smoke proves anonymous session establishment, stable-node browse, typed reads, typed write/readback and monitored-item notification. This proves the peer/reference-client laboratory slice, not yet Driver 9 product-path acceptance.

## IEC 60870-5-104

The IEC-104 overlay builds a deterministic outstation against pinned MZ Automation `lib60870-C` commit `7a388e3e133999e1ca77ba7521d55d074b7cd2bc`.

```bash
bash scripts/lab.sh iec104-start
bash scripts/lab.sh iec104-status
```

Exposed peer:

- IEC-104 outstation: `localhost:2404`, Common Address 1

The peer supports STARTDT/GI, monitored and spontaneous values, and the first-release Direct/SBO command matrix used by Driver 6 validation. Driver 6 PR #168 already obtained independent-software L2 evidence against this peer family.

## DNP3

The DNP3 overlay uses `dnp3py` pinned to commit `8a20d4c276274f2b98800716cd7da963f21da2c1`, deliberately independent from the Step Function stack used by the Driver 7 adapter.

```bash
bash scripts/lab.sh dnp3-start
bash scripts/lab.sh dnp3-status
```

Exposed peer:

- DNP3 outstation: `localhost:20000`
- outstation address: `1024`
- expected master address: `1`

Static points include Binary Input 0 = `true`, Analog Input 0 = `4242`, and Counter 0 = `123456`.

The peer itself is healthy and interoperable enough to expose a current Driver 7 issue: G30V1 arrives from the adapter as Int32, while the Driver publishes the configured Int32 TAG into the canonical cache as Double. Until that boundary is corrected and the L2 run is green, DNP3 product-path interoperability remains **RED**, not “mostly passed”.

## Node-RED control API

### Health

`GET /lab/health`

### Publish MQTT stimulus

`POST /lab/mqtt/publish`

Example body:

```json
{
  "topic": "elitescada/lab/tag/temperature",
  "payload": { "value": 25.3, "sourceTimestamp": "2026-08-30T12:00:00Z" },
  "qos": 1,
  "retain": false
}
```

### Last observed lab MQTT message

`GET /lab/mqtt/last`

### Reset volatile observation state

`POST /lab/reset`

These endpoints are test-lab APIs, not EliteSCADA public APIs.

## Scenario philosophy

Every protocol scenario should distinguish at least four independent facts:

- **transport/session readiness**;
- **current TAG value + quality**;
- **source timestamp/event evidence**;
- **write/command outcome**.

A successful socket write is not command success. A connected source is not proof that every point is Good. A reconnect must not replay an old command. A protocol-native numeric type must not silently become a different canonical TAG type merely because its numeric value still looks plausible.

See `scenarios/README.md` for the common scenario contract.

## Safety and licensing

- No production credentials belong in this directory or in Node-RED flows.
- Test-only anonymous MQTT is intentional and isolated to this lab.
- Third-party simulators are test infrastructure and retain their own licenses.
- open62541, lib60870 and dnp3py are independent laboratory peers, not EliteSCADA runtime dependencies.
- The DNP3 Step Function commercial-license question remains a commercial-release gate; it does not prevent use of the independent dnp3py laboratory peer for development/testing.
