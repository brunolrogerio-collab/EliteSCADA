# Matter Controller Research

Target: \`matter.controller\`  
Research date: **2026-10-02**  
Readiness: **\`WAIT_DEPENDENCY\`**

## Decision summary

Preferred architecture remains a local controller sidecar:

\`EliteSCADA -> managed Matter controller server -> Matter devices\`

The strongest current candidate is **matterjs-server 1.4.0**, built on **matter.js 0.17.x**. Both are Apache-2.0. The project exposes a versioned WebSocket controller interface and is actively maintained.

Important maturity note: the current matterjs-server README explicitly describes the matter.js-based controller as a **Beta** version and says it is not yet officially re-certified by CSA. That is sufficient for an engineering integration candidate, but not enough to declare an EliteSCADA Matter-certified product.

## Candidate stacks

### Preferred: matterjs-server + matter.js

Observed:
- matterjs-server 1.4.0 dated 2026-08-07;
- WebSocket schema 13 in 1.4.0;
- matter.js stable 0.17.9 dated 2026-08-06;
- matter.js 0.17.x supports Matter 1.5/1.6 era functionality;
- both repositories use Apache-2.0.

Advantages:
- ready controller/server process boundary;
- WebSocket API instead of embedding Node objects into .NET;
- persistent controller state;
- commissioning;
- subscriptions/events;
- Thread/Wi-Fi credential management;
- backpressure improvements in current server;
- compatibility with the Python Matter Server WebSocket model.

Risks:
- server is still presented as Beta;
- current server/WS schema is evolving;
- Matter specification cadence is high.

### Official connectedhomeip SDK

The official Matter SDK is authoritative and Apache-2.0, but it is a large native C++ stack. Embedding it directly into EliteSCADA would impose significantly more build, platform, toolchain and upgrade cost.

Use as:
- specification/reference oracle;
- virtual test devices;
- protocol-level validation;
- fallback future implementation if sidecar maturity becomes unacceptable.

Not preferred for first product path.

## Core Matter concepts that must remain explicit

### Fabric

EliteSCADA controller participates as a Matter commissioner/controller in one or more fabrics. Fabric identity, operational certificates, node ids and fabric metadata are protected persistent controller state.

### PASE

Password-authenticated commissioning session used during initial device commissioning.

### CASE

Certificate-authenticated secure operational session after commissioning.

### Device attestation

Commissioning validates device attestation credentials. Test/dev DCL modes must never be enabled silently in production.

### ACL

Matter Access Control Lists are protocol-side authorization. EliteSCADA authorization still controls whether an operator is allowed to invoke a command. One must not replace the other.

### Endpoints/clusters

One Matter node can expose multiple endpoints and device types. Map semantically into one Equipment with capability channels unless the physical device clearly represents independent equipment.

### Subscriptions

Use protocol subscriptions for state. Do not poll every attribute per TAG.

## Commissioning workflow

1. controller sidecar healthy;
2. discover commissionable device;
3. user supplies/scans setup code;
4. PASE established;
5. attestation validated;
6. select Wi-Fi or Thread operational network as required;
7. device joins network;
8. operational credentials/fabric installed;
9. CASE session established;
10. read endpoint/cluster structure;
11. create discovery candidate;
12. user assigns Location/name and previews canonical Equipment/TAGs/Commands;
13. Apply.

Commissioning is an external-network mutation and must be explicitly confirmed/audited.

## Network transports

Matter operational traffic is IP:
- Ethernet;
- Wi-Fi;
- Thread.

Discovery relies on IPv6/mDNS/DNS-SD behavior. Container deployments therefore need a deliberate networking model; default bridged containers can make multicast discovery unreliable.

### Thread

Matter-over-Thread requires a Thread Border Router reachable from the controller host. The controller does not automatically become a border router merely because a Thread RCP is attached.

Possible deployments:
- existing user-managed Thread Border Router;
- future EliteSCADA-managed OTBR sidecar + Thread RCP.

The second option requires F4 Host Hardware, RCP lease, border-router lifecycle and network-credential ownership. It should not be bundled into the first Matter slice.

### BLE

BLE is commonly used for commissioning transport. Direct host BLE support adds platform/container complexity.

Recommended first options:
- support platform BLE where matterjs-server validates it;
- permit server-supported BLE proxy path where practical;
- do not require BLE after operational commissioning.

## Persistent state / backup

Protect:
- fabric credentials/certificates;
- fabric/node metadata;
- network credentials;
- commissioned-node state;
- server database/storage;
- any DCL/test-mode configuration.

Backups contain high-value credentials and must be encrypted/protected. A normal \`.escadapkg\` must not contain these values in plaintext.

Restore requires:
- compatible sidecar/schema version;
- explicit recovery workflow;
- conflict handling if the live fabric has advanced independently.

## Capability mapping

Common first-release device types/clusters:
- OnOff -> OnOff;
- LevelControl -> Dimmer;
- ColorControl -> ColorLight;
- temperature/humidity/occupancy -> sensor capabilities;
- BooleanState/Contact-like device semantics -> ContactSensor where the device type/cluster combination is unambiguous;
- ElectricalPowerMeasurement/ElectricalEnergyMeasurement -> PowerMeter/EnergyMeter;
- DoorLock -> Lock;
- Thermostat -> Thermostat/Climate;
- FanControl -> Fan;
- WindowCovering -> Cover.

Do not map arbitrary vendor/custom clusters into common capabilities without explicit semantics.

## Security

- sidecar endpoint bound to loopback/private IPC by default;
- controller API authentication if exposed beyond loopback;
- protected fabric and network credentials;
- no setup codes in logs after use;
- no test-DCL mode in production defaults;
- explicit fabric removal;
- local firewall/container network guidance;
- version-pinned updates because controller state compatibility matters.

## Certification / trademark

CSA certification is separate from library licensing. Current CSA material says certification:
- is available to Alliance members;
- requires authorized testing and application;
- enables Certified Product logos after approval.

An integration that controls Matter devices is not automatically a Matter Certified Product.

\`LEGAL_REVIEW_REQUIRED\` before any certified-product or logo claim.

## Packaging

Recommended:
- managed sidecar/container;
- pinned Node/runtime + matterjs-server build;
- persistent state volume;
- loopback WebSocket;
- explicit mDNS/IPv6 host networking requirements;
- optional BLE device/proxy mapping;
- health/readiness;
- controlled migration/rollback.

## Performance / scale

matterjs-server 1.2.0 added per-connection WebSocket backpressure and stale event coalescing to prevent unbounded memory growth. EliteSCADA should preserve that property on its own client side.

**PROPOSED ELITESCADA QUALIFICATION TARGETS**
- 100 commissioned nodes across Wi-Fi/Thread fixtures where practical;
- at least 1,000 subscribed attributes;
- sidecar restart with state preservation;
- bounded reconnect storm;
- ICD/sleepy-device coverage;
- no unbounded queue growth.

These are test targets, not Matter protocol limits.

## Recommended architecture

\`EliteSCADA matter.controller -> managed matterjs-server -> Matter fabric -> devices\`

Thread border routing is an external dependency in the first slice.

## Candidate stack

Primary:
- matterjs-server 1.4.0 — Apache-2.0;
- matter.js 0.17.9 line — Apache-2.0.

Reference/test:
- project-chip/connectedhomeip — Apache-2.0.

## License / distribution

All three candidate codebases above are Apache-2.0. Preserve LICENSE/NOTICE obligations for redistributed components. Certification/trademark is independent.

## Hardware

L4 lab:
- one Matter Wi-Fi plug/light;
- one Matter Thread plug/light;
- one Thread Border Router;
- one Thread sensor/ICD;
- one lock/cover/thermostat-class device if first capability set includes them;
- BLE-capable Linux and Windows hosts where commissioning is supported.

## EliteSCADA dependencies

- #472 F2/F3/F6;
- LocalBridge lifecycle;
- protected secret/material references;
- optional F4 BluetoothAdapter;
- future ThreadRadio/RCP only if EliteSCADA owns border routing;
- external-network mutation confirmation.

## Implementation slices

1. sidecar lifecycle + WS schema handshake;
2. persistent fabric and health;
3. Wi-Fi/Ethernet commissioned node import;
4. cluster/capability mapper;
5. subscriptions/commands;
6. backup/restart;
7. Thread using an existing Border Router;
8. BLE commissioning matrix;
9. L2 official SDK virtual peers;
10. L4 mixed Wi-Fi/Thread devices.

## Tests

- **L0:** endpoint/cluster mapper, schema adapters, secret redaction.
- **L1:** fake WS server, node/event/reconnect/backpressure.
- **L2:** real matterjs-server + connectedhomeip virtual devices.
- **L3:** canonical TAG/Command/Gateway/Alarm/Historian.
- **L4:** real Matter Wi-Fi and Thread devices + TBR.

## Risks

- sidecar currently Beta;
- WS schema evolution;
- multicast/container networking;
- BLE platform differences;
- Thread Border Router ownership;
- certification claims;
- persistent fabric state migrations.

## Open decisions

- accept Beta matterjs-server for first DEV or wait for its next certification/maturity milestone;
- exact supported Matter release window;
- whether EliteSCADA ever manages an OTBR;
- whether BLE proxy is an accepted first-release commissioning path.

## Readiness

\`WAIT_DEPENDENCY\`

Dependency: Main must decide the acceptable matterjs-server maturity/certification boundary and the common LocalBridge contract must exist.

## Sources

Accessed 2026-10-02:
- https://github.com/matter-js/matterjs-server
- https://github.com/matter-js/matterjs-server/blob/main/CHANGELOG.md — 1.4.0, WS schema 13.
- https://github.com/matter-js/matterjs-server/blob/main/LICENSE — Apache-2.0.
- https://github.com/matter-js/matter.js/releases — 0.17.9 stable observed.
- https://github.com/matter-js/matter.js/blob/main/LICENSE — Apache-2.0.
- https://github.com/project-chip/connectedhomeip/blob/master/LICENSE — Apache-2.0.
- https://csa-iot.org/certification/why-certify/
- https://csa-iot.org/certification/tools/certification-tool/
