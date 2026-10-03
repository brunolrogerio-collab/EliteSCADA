# Native Zigbee Research

Target: \`zigbee.coordinator\`  
Research date: **2026-10-02**  
Readiness: **\`GO_AFTER_FOUNDATION\`**

## Decision summary

Recommended first implementation:

\`EliteSCADA -> managed zigbee.coordinator sidecar -> Host ZigbeeCoordinator -> Zigbee mesh\`

The sidecar should be an EliteSCADA-owned integration surface built on **MIT-licensed \`zigbee-herdsman\`**, with an explicitly bounded model converter/quirk layer derived from **MIT-licensed \`zigbee-herdsman-converters\`**. It must not be Zigbee2MQTT with MQTT hidden behind another name.

Observed current project activity:
- \`zigbee-herdsman\` latest release observed: **10.10.0, 2026-09-30**;
- \`zigbee-herdsman-converters\` release stream observed: **26.114.x line in late September 2026**;
- both repository LICENSE files are MIT.

Pin exact package versions in the sidecar. Their APIs and device definitions evolve rapidly; EliteSCADA must expose its own stable internal contract.

A pure .NET implementation of TI ZNP + Silicon Labs EZSP + ZCL + device quirks is not recommended for first delivery. It would duplicate mature radio/quirk work and materially increase maintenance.

## Why this still counts as native Zigbee

EliteSCADA owns:
- coordinator selection;
- network lifecycle;
- secure network material references;
- permit-join policy;
- interview;
- device/capability projection;
- reporting/binding policy;
- commands;
- availability;
- diagnostics;
- backup/recovery UX.

There is no MQTT or Zigbee2MQTT requirement in this path. The Node process is an implementation component, not an external home-automation bridge.

## Coordinator families

### Texas Instruments / zStack

Primary lab family:
- CC2652 / CC1352 generation, especially P variants where appropriate;
- host communication generally appears as serial/ZNP through the adapter firmware;
- USB and wired Ethernet serial bridges are practical deployment forms.

Zigbee2MQTT's current adapter documentation recommends zStack and records coordinator backup support for zStack.

### Silicon Labs / EmberZNet

Primary alternate lab family:
- EFR32MG21/MG24-class coordinator/NCP hardware;
- EZSP over UART is a documented Host/NCP model;
- Silicon Labs also documents CPC for host/NCP communication.

Current Zigbee2MQTT documentation recommends EmberZNet and records coordinator backup support for Ember adapters.

### Other families

deCONZ is mature enough to keep as a compatibility candidate but is not proposed as the primary EliteSCADA native first-release target. ZiGate is currently listed by Zigbee2MQTT as not maintained; ZBOSS is currently listed as experimental. Do not promise them in v1.

## USB, serial and network coordinators

F4 Host Hardware should model coordinator identity independently from a transient COM/tty path.

Supported deployment concepts:
- local USB serial coordinator;
- explicit server-side serial path;
- wired Ethernet coordinator exposing a supported TCP serial endpoint.

For Ethernet, prefer wired links. Zigbee2MQTT warns that Wi-Fi remote adapters can suffer from serial-protocol sensitivity to latency/packet loss.

Container deployments require explicit device mapping for local USB or an explicit network endpoint for Ethernet coordinators.

## Network authority and persisted state

Persisted Zigbee controller state must include or reference, through protected material infrastructure:
- network key;
- PAN ID / extended PAN identity;
- channel;
- coordinator identity;
- trust-center/network state;
- device IEEE identities;
- relevant interview/configuration metadata;
- backup metadata and implementation version.

Changing network key or PAN can force re-pairing; channel changes can also affect devices. Treat those changes as guarded external-network mutations.

Coordinator backup/restore is not universally portable. Current ecosystem guidance says backup/restore support is strongest for zStack and Ember. Cross-family migration can work but is not guaranteed. UI must distinguish:
- same-family restore;
- cross-family migration attempt;
- re-pair-required fallback.

## Commissioning workflow

Normal user flow:

1. select an available Host ZigbeeCoordinator;
2. create/open network;
3. choose channel and security policy or accept validated defaults;
4. open \`Permit join\` for a bounded timer;
5. observe candidate;
6. interview endpoints/clusters;
7. resolve model identity and converter;
8. preview Equipment/Capabilities/TAGs/Commands;
9. assign Location/name;
10. Apply through canonical Engineering lifecycle.

\`Permit join\` is an explicit external-network mutation. It must auto-close and show remaining time.

## Zigbee object model

Keep protocol identity diagnostic, not user-facing primary authoring:
- IEEE address -> stable protocol identity;
- endpoint;
- profile/device id;
- input/output clusters;
- cluster attributes;
- commands;
- reporting configuration;
- bindings.

One physical device normally becomes one canonical Equipment. Endpoints/channels become capability bindings or child channels only when semantically useful.

## Initial cluster scope

First release should be deliberately bounded:

| Zigbee capability | EliteSCADA mapping |
|---|---|
| On/Off | OnOff |
| Level Control | Dimmer / Light |
| Color Control | ColorLight |
| Temperature Measurement | TemperatureSensor |
| Relative Humidity | HumiditySensor |
| Occupancy Sensing | OccupancySensor |
| Illuminance Measurement | IlluminanceSensor |
| IAS Zone: contact | ContactSensor |
| IAS Zone: leak | LeakSensor |
| IAS Zone: smoke, where semantics are verified | SmokeSensor |
| Power Configuration | Battery |
| Electrical Measurement | PowerMeter / Voltage / Current |
| Simple Metering | EnergyMeter |
| Window Covering | Cover |

Not first release:
- OTA orchestration;
- Green Power commissioning;
- manufacturer-specific features without a verified converter;
- universal Zigbee Smart Energy support;
- arbitrary raw-cluster authoring as the normal UX.

## Reporting, bindings and sleepy devices

Prefer device reporting/subscriptions over per-TAG polling where supported.

The driver needs explicit state for:
- configured reporting;
- expected report interval;
- last seen;
- sleepy/end-device behavior;
- routers;
- parent/route observations when the stack exposes them;
- stale/availability policy.

A sleeping device is not automatically unavailable merely because it is not continuously reachable. Availability policy must account for expected wake/report behavior.

Bindings are protocol topology and should not silently become project automation logic. If EliteSCADA exposes binding management later, it is a deliberate network mutation with confirmation.

## Device quirks

Required shape:

\`manufacturer/model/endpoints fingerprint -> converter/quirk\`

Rules:
- generic ZCL remains generic;
- model-specific behavior lives in a registry;
- each quirk declares match evidence, transformed properties and tested firmware/model evidence;
- no uncontrolled \`if model == ...\` spread through the driver;
- unsupported devices may still expose diagnostic raw identity, but they are not declared compatible.

The MIT \`zigbee-herdsman-converters\` project is a strong source/candidate for this layer. If shipped, retain required MIT notices and pin/version the dataset.

## Security

- store network keys as protected secrets, never plaintext in \`.escadapkg\`;
- permit-join closed by default;
- bounded join window;
- support install-code commissioning only when the adapter/stack path supports it;
- sanitize logs so keys/install codes do not leak;
- backup artifacts containing network material are protected/encrypted;
- coordinator reset/restore requires privileged confirmation.

Certification/trademark note: using Zigbee technology or compatible radio components does not automatically make EliteSCADA a Zigbee Certified Product. Any certification/logo claim must be reviewed against current Connectivity Standards Alliance membership/certification rules.

## Packaging

Recommended:
- Node runtime/component packaged as an optional EliteSCADA-managed sidecar;
- Windows and Linux builds;
- container image variant with explicit USB mapping;
- wired-network coordinator support without host USB;
- separate persistent state directory;
- health endpoint/IPC;
- no public network listener required if local IPC/loopback is sufficient.

The sidecar must consume F4's resource lease rather than opening arbitrary host serial devices outside authority.

## Performance / scale

Protocol capacity depends on coordinator firmware, mesh topology, routers, traffic mix and sleepy devices. Do not publish a universal device count.

**PROPOSED ELITESCADA LIMIT for first validation**, subject to L4 evidence:
- qualify at least 100 joined devices in a representative mixed mesh;
- prove bursts/rejoins without unbounded queue growth;
- coalesce repeated state reports where loss of intermediate values is acceptable;
- preserve command/commissioning messages without destructive coalescing.

## Recommended architecture

\`EliteSCADA DriverHost -> zigbee.coordinator managed sidecar -> leased ZigbeeCoordinator -> Zigbee network\`

Sidecar emits protocol-neutral discovery/capability events to the driver adapter; canonical Runtime remains Equipment/TAG/Command authority.

## Candidate stack

Primary:
- \`zigbee-herdsman\` 10.10.0 line — MIT;
- \`zigbee-herdsman-converters\` pinned dataset — MIT.

Rejected for first implementation:
- copying Zigbee2MQTT GPL code into core;
- \`zigpy\` as an embedded proprietary-core dependency because its repository is GPL-3.0;
- writing independent TI+Silabs radio stacks from scratch;
- experimental/not-maintained adapter families as promised v1 support.

## License / distribution

MIT stack is commercially redistributable subject to copyright/license notice obligations. Zigbee specification/certification/trademark rights are separate from open-source library rights.

\`LEGAL_REVIEW_REQUIRED\` before marketing any claim such as “Zigbee Certified”.

## Hardware

Minimum L4 matrix:
- one TI zStack coordinator (CC2652/CC1352-class);
- one Silicon Labs Ember coordinator (EFR32MG21/MG24-class);
- mains router/light;
- battery temperature/humidity sensor;
- contact sensor;
- occupancy sensor;
- smart plug with power/energy;
- cover device or actuator where available.

## EliteSCADA dependencies

Required before DEV release:
- #472 F2 Locations;
- #472 F3 Capability vocabulary;
- #472 F4 Host Hardware/lease;
- #472 F6 discovery candidate;
- protected secret references;
- LocalBridge lifecycle contract.

## Implementation slices

1. sidecar skeleton + versioned IPC + health;
2. F4 coordinator acquisition and zStack;
3. Ember adapter;
4. network create/open/backup;
5. pairing/interview/candidate;
6. bounded standard cluster mapping;
7. converter/quirk registry;
8. reporting/availability/recovery;
9. L2 independent peer + L4 dual-coordinator lab;
10. packaging/upgrade/backup evidence.

## Tests

- **L0:** ZCL/value codecs, capability mapper, fingerprint matcher, secret redaction.
- **L1:** fake coordinator events, interview state machine, reconnect and bounded queues.
- **L2:** sidecar integration against independent software fixtures/emulated adapter where feasible.
- **L3:** Runtime TAG/Command/Gateway/Alarm/Historian propagation and restart.
- **L4:** TI + Silicon Labs coordinators with declared model/firmware device matrix.

## Risks

- high churn in device converter data;
- coordinator firmware/API differences;
- backup portability;
- vendor quirks;
- certification/trademark claims;
- mesh behavior cannot be validated solely in software.

## Open decisions

- ship converters as a curated pinned subset or full upstream dataset;
- whether first release officially supports wired Ethernet coordinators in addition to USB;
- exact protected backup format.

## Readiness

\`GO_AFTER_FOUNDATION\`

## Sources

Accessed 2026-10-02:
- https://github.com/Koenkk/zigbee-herdsman — MIT LICENSE; releases.
- https://github.com/Koenkk/zigbee-herdsman-converters — MIT LICENSE; releases.
- https://github.com/zigpy/zigpy — GPL-3.0 candidate considered and rejected for proprietary embedding.
- https://www.zigbee2mqtt.io/guide/adapters/ — current adapter families/backup notes.
- https://www.zigbee2mqtt.io/guide/faq/ — migration/re-pair and install-code caveats.
- https://www.zigbee2mqtt.io/guide/configuration/adapter-settings.html — USB/TCP coordinator forms.
- https://docs.silabs.com/zigbee/latest/emberznet-serial-protocol-uart-host-interfacing-guide/ — Host/NCP EZSP.
- https://csa-iot.org/certification/tools/ — Zigbee certification tooling/membership context.
