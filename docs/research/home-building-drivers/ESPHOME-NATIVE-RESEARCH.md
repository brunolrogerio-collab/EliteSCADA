# ESPHome Native Research

Target: \`esphome.native\`  
Research date: **2026-10-02**  
Observed aioesphomeapi latest release: **45.7.0 (2026-07-21)**  
Readiness: **\`GO_AFTER_FOUNDATION\`**

## Decision summary

Recommended first implementation:

\`EliteSCADA built-in .NET client -> ESPHome Native API -> device\`

Unlike Zigbee/Matter/Z-Wave, ESPHome Native API is sufficiently documented at the wire level that a small .NET client is reasonable and avoids a permanent Python sidecar.

Use the official protobuf definitions and developer protocol documentation as the contract. Use \`aioesphomeapi\` as a behavioral oracle/test peer, not as a runtime dependency.

## Wire protocol

ESPHome Native API:
- uses Protocol Buffers;
- runs over TCP;
- supports plaintext framing and Noise-encrypted framing;
- Noise uses the NNpsk0 pattern;
- current developer documentation specifies ChaCha20-Poly1305;
- message type and data length are part of the framed protocol.

The protocol must be implemented from the official generated/proto definitions, not by scraping Home Assistant behavior.

## Discovery

Use local mDNS discovery where supported, plus explicit host/IP configuration.

Normal UX:
1. discover ESPHome devices;
2. show host/name/version;
3. user selects;
4. enter authentication/encryption material where required;
5. connect and enumerate entities;
6. preview capability mappings;
7. assign Location/name;
8. Apply.

Container deployments need explicit multicast/mDNS behavior or manual host configuration.

## Security

Preferred:
- Native API encryption enabled;
- encryption key stored as protected secret;
- API password where legacy devices still use it stored as protected secret;
- never serialize key/password plaintext in \`.escadapkg\`;
- redact credentials from logs;
- expose whether a connection is encrypted in diagnostics.

Plaintext Native API may be supported only as a deliberate compatibility mode on trusted networks and should be visibly marked insecure.

## Entities

The Native API supports entity listing and live state subscriptions. Map only well-understood entity types.

Common mappings:
- switch -> OnOff;
- light -> Light/Dimmer/ColorLight according to traits;
- sensor -> typed common sensors by device class/unit/name metadata;
- binary sensor -> Contact/Occupancy/Motion/Leak/Smoke when semantics are explicit;
- cover -> Cover;
- fan -> Fan;
- climate -> Climate/Thermostat;
- number/select -> writable parameter only when canonical command semantics are safe;
- button -> Button/Command;
- text sensor -> diagnostic/string TAG where supported by canonical TAG type;
- lock -> Lock where exposed by current API.

Entity key/id must be source identity; display name is not stable identity.

## Commands

Map Native API command messages to canonical Command/write boundaries. No direct frontend-to-device path.

After command:
- wait for authoritative state update when the entity provides state;
- timeout/failure produces Bad/Unavailable diagnostics;
- no fake optimistic process truth.

## Availability / reconnect

Maintain:
- device connection state;
- last successful message;
- API version;
- device firmware/build info;
- reconnect backoff/jitter;
- deep-sleep awareness where device info indicates it.

Current aioesphomeapi release notes explicitly mention deep-sleep reconnect behavior, confirming that ordinary always-online timeout policy is insufficient for some devices.

## Version compatibility

ESPHome's developer docs state API changes affect ESPHome, aioesphomeapi and Home Assistant together. Therefore:
- negotiate/read API version;
- keep protobuf generation versioned;
- ignore additive unknown fields;
- keep fixtures from multiple supported firmware versions;
- fail individual unsupported entities rather than the whole device when possible.

## Candidate client strategies

### Native .NET client — recommended

Implement:
- TCP framing;
- Noise handshake/transport;
- protobuf messages;
- entity enumeration;
- subscriptions;
- command messages;
- reconnect.

Benefits:
- no sidecar lifecycle;
- small dependency surface;
- Windows/Linux parity.

### Python sidecar with aioesphomeapi

Technically mature and MIT licensed, but adds Python lifecycle/packaging for a protocol whose public wire format is already documented.

Use as test oracle, not first runtime choice.

### Existing third-party .NET libraries

No candidate was identified with enough current activity, API coverage and licensing evidence to prefer over a small first-party client. Revalidate before DEV release.

## License / distribution

\`aioesphomeapi\`: MIT.

The ESPHome monorepo has mixed licensing: Python/other portions MIT, C++ runtime files GPLv3 according to its LICENSE. Do not copy ESPHome device runtime C++ into proprietary EliteSCADA.

A client independently implementing the documented wire protocol does not require bundling ESPHome firmware/runtime.

## Packaging

Built-in .NET driver/client:
- no sidecar;
- direct local TCP connection;
- mDNS optional helper;
- protected key reference;
- no host hardware.

## Performance / scale

Each device has a persistent API connection in normal operation.

**PROPOSED ELITESCADA QUALIFICATION TARGETS**
- 250 concurrent devices in software L2;
- 5,000 total entities;
- bounded reconnect with jitter after network outage;
- no one-device failure blocks others;
- deep-sleep devices do not generate continuous error storms.

These are EliteSCADA test targets, not ESPHome limits.

## Recommended architecture

\`EliteSCADA DriverHost -> .NET ESPHome Native API client -> device\`

## Candidate stack

- first-party .NET implementation from official protobuf/protocol docs;
- protobuf runtime under normal project dependency review;
- aioesphomeapi 45.7.0 as reference/test oracle.

## Hardware

L4:
- ESP32 Wi-Fi device with switch/light output;
- ESP32 sensor node;
- encrypted Native API;
- one deep-sleep battery sensor;
- one climate/cover or representative actuator.

## EliteSCADA dependencies

- F2/F3/F6;
- protected secret references;
- common discovery diagnostics;
- no F4 hardware dependency.

## Implementation slices

1. protobuf generation + plaintext frame codec;
2. Noise transport;
3. connect/version/device info;
4. entity list + source identity;
5. state subscriptions;
6. commands/readback;
7. discovery/mDNS;
8. reconnect/deep sleep;
9. capability mapper;
10. L2 aioesphomeapi/real ESPHome fixture + L4 hardware.

## Tests

- **L0:** framing, varints, Noise vectors, protobuf mapping.
- **L1:** fake Native API server.
- **L2:** independent ESPHome/aioesphomeapi-compatible peer.
- **L3:** canonical TAG/Command/Gateway/Alarm/Historian.
- **L4:** multiple ESP32 firmware versions/entities.

## Risks

- protocol evolves with ESPHome;
- Noise implementation correctness;
- heterogeneous custom entities;
- mDNS in containers;
- deep sleep/availability semantics;
- mixed licensing if implementation accidentally copies runtime code.

## Open decisions

- exact minimum ESPHome firmware/API version;
- whether plaintext mode is allowed in production or only compatibility/dev;
- whether text/number/select entities enter first release.

## Readiness

\`GO_AFTER_FOUNDATION\`

## Sources

Accessed 2026-10-02:
- https://developers.esphome.io/architecture/api/
- https://developers.esphome.io/architecture/api/protocol_details/
- https://github.com/esphome/aioesphomeapi/releases — 45.7.0.
- https://github.com/esphome/aioesphomeapi/blob/main/LICENSE — MIT.
- https://github.com/esphome/esphome/blob/dev/LICENSE — mixed MIT/GPLv3 as documented.
