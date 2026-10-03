# Host Hardware Dependency Map

Research date: **2026-10-02**

## Project boundary

#472 F4 is the canonical future authority:

- SerialPort
- ZigbeeCoordinator
- ZWaveController
- ThreadRadio/RCP
- BluetoothAdapter
- future protocol interface hardware

#469 is the first active implementation source for reusable host serial enumeration/lease concepts. This research does not change #469.

## Classification

| Integration | Connection model | Host physical resource | Local bridge | Cloud | Notes |
|---|---|---|---|---|---|
| Native Zigbee | HostRadio / LocalBridge | ZigbeeCoordinator | Recommended Zigbee service | No | USB serial or explicit wired network coordinator |
| Zigbee2MQTT user-managed | LocalBridge | None owned by EliteSCADA | External Z2M + MQTT | No | Z2M owns coordinator |
| Zigbee2MQTT managed future | HostRadio / LocalBridge | ZigbeeCoordinator | Z2M sidecar | No | resource leased to sidecar |
| DALI via IP/KNX/Modbus gateway | DirectNetwork | None | gateway is external appliance | No | protocol driver connects to gateway |
| Native DALI future | HostRadio-like dedicated interface | DALI Interface/Controller | Maybe | No | not an ordinary SerialPort even if USB transport is serial-like |
| Home Assistant Bridge | DirectNetwork / LocalBridge | None | user-managed HA | No | LAN WSS/HTTPS |
| Matter Controller | LocalBridge | optional BluetoothAdapter | matterjs-server | No | operational IP; Thread uses external TBR in first scope |
| Matter + EliteSCADA-managed OTBR future | HostRadio / LocalBridge | ThreadRadio/RCP + optional BluetoothAdapter | OTBR + Matter server | No | separate future product decision |
| ESPHome Native | DirectNetwork | None | No | No | local TCP/mDNS |
| Shelly RPC | DirectNetwork | None | No | No | local HTTP/WebSocket |
| Z-Wave JS Bridge | HostRadio / LocalBridge | ZWaveController | Z-Wave JS Server | No | USB controller lease |
| KNX/IP Tier B | DirectNetwork | None | optional sidecar | No | IP interface/router |
| Tuya Tier B | Cloud | None | No | **Yes** | account/project/plan |
| Intelbras GDI Tier B | Cloud | None | No | **Yes** | account/CNPJ/plan |
| BTHome Tier B | HostRadio | BluetoothAdapter | optional BLE proxy later | No | passive advertisements |

## Required F4 resource semantics

All local resources:
- server-side enumeration only;
- stable server-facing configured identity;
- transient OS path not treated as project truth;
- offline configured identity allowed;
- exclusive/shared lease policy;
- deterministic conflict;
- Windows/Linux;
- container-visible resources only;
- protected Engineering API;
- no arbitrary browser/runtime-script OS enumeration.

## Resource-specific notes

### ZigbeeCoordinator

Need attributes:
- transport: local serial | network serial;
- adapter family hint: zStack | Ember | other;
- stable hardware identity where available;
- current path/endpoint;
- firmware/version probe;
- lease owner;
- backup capability/result.

### ZWaveController

Need:
- USB path/stable identity;
- controller SDK/firmware;
- node/home id only through owning stack;
- exclusive lease;
- NVM backup metadata.

### ThreadRadio/RCP

Do not equate RCP with a Matter controller. If EliteSCADA manages an RCP it also needs an OpenThread Border Router lifecycle and network dataset ownership.

### BluetoothAdapter

Need passive scan capability and, for Matter commissioning, active BLE connection capability. Container/device permissions vary by OS and must be validated separately.

### DALI Interface

Dedicated protocol interface/controller. Even when presented as USB serial internally, it should have its own resource kind because electrical/protocol semantics and ownership differ from a generic serial port.

## RESEARCH_CONTRACT_DELTA_REQUIRED

### HOST-RESOURCE-ENDPOINT-01

F4 should support host resources whose hardware is accessed through an explicit local network endpoint (for example an Ethernet Zigbee coordinator) while still keeping server-side authority.

The persisted configuration may contain the explicit controller endpoint; the transient discovery catalog remains host-owned.

No implementation in this research lane.
