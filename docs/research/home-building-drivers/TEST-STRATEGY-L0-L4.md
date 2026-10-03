# HOME/BUILDING Test Strategy — L0 to L4

Research date: **2026-10-02**

## Principle

A protocol handshake is not a device compatibility claim.

Compatibility statements require model/firmware evidence at L4 when hardware behavior matters.

## L0 — pure logic

For every Tier A integration:
- wire codecs/framing;
- JSON/protobuf schemas;
- capability mapping;
- identity normalization;
- unit/scale conversion;
- command argument validation;
- secret redaction;
- retry/backoff calculations;
- bounded queue behavior;
- import reconciliation logic.

Examples:
- Zigbee cluster attribute -> Capability;
- Z2M expose access -> read/write direction;
- Matter endpoint/cluster -> capability;
- ESPHome Noise/framing vectors;
- Shelly digest challenge calculation;
- Z-Wave value metadata mapping.

## L1 — fake / loopback / in-process

Deterministic fake peer:
- connect/disconnect;
- inventory;
- state events;
- write success/failure;
- malformed payload;
- authentication failure;
- version mismatch;
- slow consumer;
- reconnect storm;
- queue saturation;
- duplicate/out-of-order event cases where transport allows.

Commissioning state machines should be testable without radio hardware.

## L2 — independent real protocol peer/software

| Integration | L2 peer |
|---|---|
| Native Zigbee | actual sidecar against adapter emulator/test harness where feasible |
| Zigbee2MQTT | real Z2M + real MQTT broker, fake/software adapter if available |
| DALI gateway | independent gateway protocol simulator or vendor test endpoint; native later with interface simulator |
| Home Assistant | real pinned HA container using public API |
| Matter | real matterjs-server with connectedhomeip/matter.js virtual devices |
| ESPHome | real ESPHome firmware/API peer or independently generated device fixture |
| Shelly | protocol-faithful HTTP/WS simulator plus independent real local device in CI-adjacent lab when feasible |
| Z-Wave | real Z-Wave JS Server using its test/mock facilities |
| KNX | xknx/KNX software peer or KNX virtual tooling |
| BTHome | BLE advertisement replay/synthetic scanner stream |

L2 must validate wire/API behavior independently of EliteSCADA fakes.

## L3 — EliteSCADA integrated

For each imported Capability prove:
- canonical Equipment/TAG/Command creation;
- correct read quality;
- write/command authorization;
- Cache/Event;
- Historian;
- Alarm;
- TAG Gateway;
- script/runtime consumers where applicable;
- Save/Publish/Activate;
- restart/reconnect;
- import/export without plaintext secrets;
- deletion/reconciliation.

Commissioning actions must remain Engineering-only and auditable.

## L4 — hardware

Record exact:
- controller/interface model;
- device model;
- hardware revision;
- firmware;
- sidecar/driver version;
- transport;
- security mode.

### Native Zigbee

At least:
- TI zStack coordinator;
- Silicon Labs Ember coordinator;
- router/light;
- sleepy sensor;
- IAS/contact;
- power meter;
- color/cover when supported.

### Z2M

Real Z2M with at least one supported coordinator plus devices using multiple expose types and one model-specific quirk.

### DALI

Certified gateway + DT6 + DT8 + input device; native later with selected DALI interface.

### Home Assistant

Real HA host with multiple integration sources and one upgrade across the supported version window.

### Matter

Wi-Fi + Thread; TBR; at least one ICD/sleepy device; attestation and restart/backup.

### ESPHome

Encrypted devices, different entity classes, deep sleep.

### Shelly

Gen2/Gen3/Gen4 representative models and authenticated configuration.

### Z-Wave

800/700 controller target, mains router, sleeping node, metering, security S2; Lock only if in declared scope.

## Failure injection

Mandatory across L1-L4 as applicable:
- process crash;
- host reboot;
- controller unplug/replug;
- network outage;
- broker outage;
- credential revoke;
- malformed/unsupported device;
- sidecar version mismatch;
- full queue/slow Historian;
- backup restore;
- external rename/removal.

## Version matrix

Fast-moving dependencies must have explicit pinned fixtures:
- Z2M;
- zigbee-herdsman;
- HA Core;
- matterjs-server/matter.js;
- ESPHome API;
- Shelly firmware;
- Z-Wave JS Server/zwave-js.

Before a support-range bump, run L0-L3 and representative L4.

## Compatibility declaration

A device support record should include:
- integration;
- model;
- firmware(s) tested;
- capabilities verified;
- security mode;
- test date;
- known limitations.

“Protocol compatible” is weaker than “tested EliteSCADA device.”
