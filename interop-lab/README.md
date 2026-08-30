# EliteSCADA Driver Interoperability Lab

This directory is a destructive, reproducible protocol interoperability lab. It is deliberately separate from the EliteSCADA product runtime and from normal unit/integration tests.

The lab has two roles:

1. **Node-RED is the control plane**: scenario orchestration, value generation, fault injection coordination and reference-client tooling.
2. **Independent protocol peers are the data plane**: brokers, PLC simulators, outstations and servers that exercise EliteSCADA over the real wire protocol.

The lab must never be treated as a substitute for later human validation against representative hardware. Its job is to catch protocol, lifecycle and interoperability defects before hardware time is consumed.

## Current first cut

| Driver | Lab peer | State |
| --- | --- | --- |
| MQTT | Eclipse Mosquitto 2.1.2 + Node-RED | **Runnable now** |
| Allen-Bradley EtherNet/IP/CIP | Independent ControlLogix + CompactLogix simulator processes from `node-red-contrib-cip-suite` | **Runnable overlay** |
| OPC UA | open62541 1.5.7 server + `node-opcua` reference client from the Node-RED image | **Runnable independent-software overlay** |
| IEC 60870-5-104 | independent server/outstation sidecar | **Slot reserved** |
| DNP3 | independent outstation sidecar, intentionally not the same Step Function stack used by Driver 7 | **Slot reserved** |
| Siemens S7 ISO-on-TCP | independent S7 server/PLC simulator | **Slot reserved** |
| BACnet/IP | independent BACnet device simulator/reference peer | **Slot reserved** |

Machine-readable status lives in `scenarios/catalog.json`.

## Start base lab

```bash
cd interop-lab
cp .env.example .env
docker compose up -d --build
bash scripts/lab.sh smoke
```

Windows PowerShell:

```powershell
cd interop-lab
Copy-Item .env.example .env
./scripts/lab.ps1 start
./scripts/lab.ps1 smoke
```

Node-RED: `http://localhost:1880`

Control-plane health: `http://localhost:1880/lab/health`

Mosquitto: `localhost:1883`

The default EliteSCADA URL from inside the lab is `http://host.docker.internal:5000`. Override `ELITESCADA_BASE_URL` in `.env` if the runtime is elsewhere.

## Add Allen-Bradley simulators

The CIP overlay builds the simulator from a pinned third-party Git commit rather than copying its source into EliteSCADA.

```bash
docker compose -f compose.yaml -f compose.cip.yaml up -d --build
```

Exposed peers:

- ControlLogix: `localhost:44818`
- CompactLogix: `localhost:44819`

The pinned simulator source is external test infrastructure only. It is not shipped as an EliteSCADA runtime dependency.

## Add independent OPC UA peer

The OPC UA overlay builds an **open62541 1.5.7** server from the upstream single-file release. Both release files are SHA-256 pinned in the Docker build. The reference client runs with `node-opcua` already present in the Node-RED image, so client and server are independent OPC UA stacks.

```bash
docker compose -f compose.yaml -f compose.opcua.yaml up -d --build
docker compose -f compose.yaml -f compose.opcua.yaml exec -T node-red node /data/opcua-smoke.js
```

Exposed peer:

- open62541 OPC UA server: `opc.tcp://localhost:4841`

The first automated scenario proves:

- anonymous session establishment;
- browse visibility for stable NodeIds;
- typed read;
- typed write followed by readback;
- monitored-item subscription delivery after a write.

This is **L2 independent-software interoperability evidence**. It is not a replacement for later validation against industrial OPC UA servers and real certificates/security policies.

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

These endpoints exist to make scenarios scriptable. They are test-lab APIs, not EliteSCADA public APIs.

## Scenario philosophy

Every protocol scenario should distinguish at least four independent facts:

- **transport/session readiness**;
- **current TAG value + quality**;
- **source timestamp/event evidence**;
- **write/command outcome**.

A successful socket write is not command success. A connected source is not proof that every point is Good. A reconnect must not replay an old command. Late historical events must not be silently destroyed to protect a current-value cache.

See `scenarios/README.md` for the common scenario contract.

## Safety and licensing

- No production credentials belong in this directory or in Node-RED flows.
- Test-only anonymous MQTT is intentional and isolated to this lab.
- Third-party simulators are test infrastructure and retain their own licenses.
- open62541 is used only as an independent test peer; it is not an EliteSCADA runtime dependency.
- The DNP3 Step Function commercial-license question remains a **future commercial-release gate**, not a blocker for current development/integration/testing.
