# HOME/BUILDING Driver Research

Research owner: #475  
Parent architecture: #472  
Contract: \`C-HOME-BUILDING-DRIVER-RESEARCH-01\`  
Order: \`W15-HOME-BUILDING-DRIVER-RESEARCH-01\`  
Research date: **2026-10-02 (America/Sao_Paulo)**  
Release reference: \`wave15/corrections-integration@056224d1498b217ccb9a7cf5d13e0a2eb27d27bb\`

## Scope and authority

This tree is research/documentation only. It does not implement drivers, Driver SDK changes, Runtime behavior, Engineering UI, schemas, package dependencies, CI, or Host Hardware. GitHub live remains the project authority.

The parent architecture remains binding:

\`Integration/Driver -> Data Source -> canonical TAG/Equipment/Command -> Cache/Event -> Historian/Alarm/Gateway/Scripts/Runtime\`

Residential/building UX is a projection over the canonical model, not a second smart-home core.

The current coding freeze remains outside this lane. Research does not authorize a new DEV.

## Research conclusions at a glance

| Target | Recommended shape | Candidate stack | Readiness |
|---|---|---|---|
| \`zigbee.coordinator\` | EliteSCADA -> managed Zigbee service -> coordinator | pinned Node sidecar using \`zigbee-herdsman\` + bounded \`zigbee-herdsman-converters\` registry | \`GO_AFTER_FOUNDATION\` |
| \`zigbee2mqtt.bridge\` | EliteSCADA -> MQTT -> user-managed or optional managed Zigbee2MQTT | Zigbee2MQTT 2.14.2; external MQTT broker | \`GO_AFTER_FOUNDATION\` |
| DALI gateway path | EliteSCADA -> existing IP/KNX/Modbus/BACnet gateway -> DALI | certified third-party gateway, existing protocol drivers | \`GO_AFTER_FOUNDATION\` |
| Native DALI | EliteSCADA -> Host DALI interface -> DALI bus | no sufficiently low-risk cross-platform native stack selected | \`NOT_FIRST_RELEASE\` |
| \`homeassistant.bridge\` | EliteSCADA -> HA WebSocket/REST | supported Home Assistant APIs | \`GO_AFTER_FOUNDATION\` |
| \`matter.controller\` | EliteSCADA -> Matter controller sidecar -> devices | \`matterjs-server\` 1.4.0 / matter.js 0.17.x | \`WAIT_DEPENDENCY\` |
| \`esphome.native\` | EliteSCADA -> native .NET Native API client -> ESPHome | official protobuf + Noise wire contract; aioesphomeapi as oracle | \`GO_AFTER_FOUNDATION\` |
| \`shelly.rpc\` | EliteSCADA -> local HTTP/WebSocket JSON-RPC -> Shelly | built-in .NET client | \`GO_AFTER_FOUNDATION\` |
| \`zwavejs.bridge\` | EliteSCADA -> Z-Wave JS Server -> controller | Z-Wave JS Server 3.10.1 | \`WAIT_DEPENDENCY\` |

Readiness is engineering readiness for EliteSCADA, not a commercial ranking of vendors.

## Architectural findings

### 1. Native Zigbee and Zigbee2MQTT must remain different products

Native Zigbee should not be a renamed Zigbee2MQTT deployment. A strong first native implementation can still avoid writing Zigbee from zero by placing a permissively licensed Zigbee service behind an EliteSCADA-owned, versioned process contract.

Zigbee2MQTT remains a separate bridge path with its own MQTT and GPL-3.0 distribution boundary.

### 2. LocalBridge is a real product pattern

Matter, Z-Wave JS, optional Zigbee native service and optional managed Zigbee2MQTT all need the same infrastructure concerns: pinned version, process/container lifecycle, health, bounded restart, logs, persistent state, secrets, ports/device mapping and upgrade/rollback.

### 3. #472 F4 is the hardware authority

Do not create protocol-specific USB enumeration. F4 already plans \`SerialPort\`, \`ZigbeeCoordinator\`, \`ZWaveController\`, \`ThreadRadio/RCP\`, \`BluetoothAdapter\` and future interface hardware with stable host identity and lease arbitration. #469 is the first implementation input and must be revalidated before F4 work.

### 4. Secrets must not live as plaintext package fields

Zigbee network keys, Matter fabric material, Z-Wave S0/S2 keys, Home Assistant tokens, MQTT credentials, ESPHome encryption keys and BTHome bindkeys require protected host storage and package-safe secret references.

### 5. External-network mutation needs an explicit boundary

Permit-join, inclusion/exclusion, Matter commissioning/fabric removal, DALI addressing/commissioning and destructive controller restore are not ordinary discovery. They need explicit user confirmation, authorization, audit and progress/recovery reporting.

## Research contract deltas

These are findings only. They are **not implemented**.

### RESEARCH_CONTRACT_DELTA_REQUIRED — SIDECAR-LIFECYCLE-01

A common LocalBridge lifecycle contract is required for optional managed processes/containers:
- component id/version/digest;
- start/stop/restart ownership;
- health/readiness;
- bounded crash-loop policy;
- sanitized logs;
- persistent state directory;
- network ports/socket;
- host-device lease attachment;
- secret references;
- upgrade/rollback and backup hooks.

### RESEARCH_CONTRACT_DELTA_REQUIRED — SECRET-REFERENCE-01

Integration settings need protected material references rather than literal long-lived secrets. Export/package behavior must omit secret values by default and support deliberate backup/restore through protected material infrastructure.

### RESEARCH_CONTRACT_DELTA_REQUIRED — EXTERNAL-NETWORK-MUTATION-01

Discovery candidate flow should distinguish read-only discovery from network mutation. Pair/include/commission/remove/address/restore operations require explicit confirmation, authorization and audit.

### RESEARCH_CONTRACT_DELTA_REQUIRED — HOST-RESOURCE-ENDPOINT-01

When #469/F4 is revalidated, host resource identity should support both local physical resources and managed remote controller endpoints (for example a wired Ethernet Zigbee coordinator) without treating a browser-local endpoint as authority.

## Common proposed first-release guardrails

Where protocols provide no normative application-size limit, these are **PROPOSED ELITESCADA LIMITS**, not protocol facts:
- bounded event queue with latest-value coalescing where semantics allow;
- bounded concurrent commissioning operations;
- per-integration health and backpressure counters;
- reconnect jitter/backoff;
- user-selectable imports instead of indiscriminate mirror-all behavior;
- no universal compatibility claim from a protocol handshake alone.

Exact numeric limits should be measured in L2/L3 load tests before implementation release.

## Documents

- [Native Zigbee](NATIVE-ZIGBEE-RESEARCH.md)
- [Zigbee2MQTT Bridge](ZIGBEE2MQTT-BRIDGE-RESEARCH.md)
- [DALI](DALI-RESEARCH.md)
- [Home Assistant Bridge](HOME-ASSISTANT-BRIDGE-RESEARCH.md)
- [Matter Controller](MATTER-CONTROLLER-RESEARCH.md)
- [ESPHome Native](ESPHOME-NATIVE-RESEARCH.md)
- [Shelly RPC](SHELLY-RPC-RESEARCH.md)
- [Z-Wave JS Bridge](ZWAVE-JS-BRIDGE-RESEARCH.md)
- [Tier B survey](TIER-B-SURVEY.md)
- [License/distribution matrix](DRIVER-LICENSE-DISTRIBUTION-MATRIX.md)
- [Host Hardware dependency map](HOST-HARDWARE-DEPENDENCY-MAP.md)
- [Device/capability mapping](DEVICE-CAPABILITY-MAPPING.md)
- [Discovery/commissioning/security](DISCOVERY-COMMISSIONING-SECURITY.md)
- [Packaging / scale](PACKAGING-AND-SCALE.md)
- [Hardware lab](HARDWARE-LAB-MATRIX.md)
- [L0-L4 test strategy](TEST-STRATEGY-L0-L4.md)
- [Implementation sequencing](IMPLEMENTATION-SEQUENCING.md)

## Source policy

Every dossier records dated official sources. Open-source licensing was checked against repository LICENSE files where available. Fast-moving versions and certification/commercial terms must be revalidated when a DEV is actually released.

No legal conclusion in this tree replaces legal review. Ambiguity is marked \`LEGAL_REVIEW_REQUIRED\`.
