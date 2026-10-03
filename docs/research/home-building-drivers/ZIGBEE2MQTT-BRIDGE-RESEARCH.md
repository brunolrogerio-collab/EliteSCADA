# Zigbee2MQTT Bridge Research

Target: \`zigbee2mqtt.bridge\`  
Research date: **2026-10-02**  
Observed Zigbee2MQTT release: **2.14.2 (2026-10-01)**  
Readiness: **\`GO_AFTER_FOUNDATION\`**

## Decision summary

Recommended shape:

\`EliteSCADA -> MQTT -> Zigbee2MQTT -> coordinator -> Zigbee devices\`

This path should coexist with Native Zigbee and is valuable for broad device coverage. It must remain an explicit external/optional bridge boundary.

First release should support **user-managed Zigbee2MQTT**. An EliteSCADA-managed optional sidecar can follow only after the common LocalBridge lifecycle contract exists.

## MQTT contract

Zigbee2MQTT uses a configurable base topic, defaulting to \`zigbee2mqtt\`.

Important retained/control surfaces include:
- \`bridge/devices\`;
- \`bridge/info\`;
- \`bridge/state\` and health-related bridge messages;
- device state topics;
- \`FRIENDLY_NAME/set\`;
- availability;
- group and scene messages;
- bridge request/response topics including permit join/interview/backup where supported.

EliteSCADA must make the base topic configurable and must not hardcode friendly names as stable identity.

Use IEEE address/device identity from bridge metadata as the protocol identity. Friendly name is display/routing metadata and can change.

## Exposes -> Capabilities

\`bridge/devices\` includes the device definition, and Zigbee2MQTT exposes describe properties/features and access semantics.

Mapping algorithm:

1. read retained \`bridge/devices\`;
2. record IEEE identity and supported definition;
3. flatten \`exposes\` into typed capability candidates;
4. preserve nested endpoint/channel identity;
5. map known semantics to canonical Capability vocabulary;
6. retain unsupported exposes as diagnostic/unmapped metadata;
7. let the user select the candidate before canonical creation.

Access metadata must influence direction:
- readable expose -> TAG/read capability;
- writable expose -> Command or writable TAG according to canonical semantics;
- both -> canonical read/write binding;
- enum/numeric constraints -> authoring metadata, not ad-hoc UI code.

Do not infer writeability solely from seeing a \`/set\` topic.

## Device lifecycle

Handle:
- bridge start/restart;
- retained device list rebuild;
- join/interview progress;
- unsupported devices;
- rename;
- leave/remove;
- availability;
- reconnect to MQTT broker;
- bridge restart while devices remain;
- coordinator restart/backup events.

An MQTT reconnect must re-subscribe deterministically and re-read retained bridge/device metadata before declaring the integration synchronized.

## Permit join and interview

Permit join is an external-network mutation:
- explicit confirmation;
- bounded timer;
- visible remaining time;
- bridge response correlated with request;
- no permanent permit-join default.

Interview status should be surfaced as discovery progress, not canonical project truth until Apply.

## Groups and scenes

Zigbee groups/scenes are external network objects. First release may:
- discover and display them;
- optionally map a group to a canonical command target when semantics are clear.

Do not silently create a second automation engine. Canonical EliteSCADA Scenes, if/when defined, remain separate from protocol-side scene storage.

## MQTT deployment and security

Supported deployment modes:

### User-managed broker + user-managed Z2M — preferred v1

EliteSCADA stores:
- broker endpoint;
- TLS policy;
- credential secret references;
- base topic;
- bridge identity/expected version.

### User-managed Z2M + existing enterprise broker

Same contract. Do not assume Mosquitto specifically.

### EliteSCADA-managed Z2M sidecar — later

Requires:
- common sidecar lifecycle;
- bundled/external broker product decision;
- persistent Z2M data directory;
- coordinator lease;
- upgrade policy;
- GPL compliance packaging.

### MQTT requirements

- TLS when crossing untrusted networks;
- username/password or broker-supported auth stored as protected secrets;
- least-privilege topic ACL where practical;
- reject accidental subscription to unrelated wildcard hierarchies;
- bounded inbound queue/backpressure;
- sanitize secrets from diagnostics.

Local non-TLS MQTT may be supported only as an explicit trusted-LAN choice, not silently presented as secure.

## GPL-3.0 boundary

The current Zigbee2MQTT repository LICENSE is **GPL-3.0**.

Product rules:
- do not copy Zigbee2MQTT GPL source into proprietary EliteSCADA core;
- do not statically combine it with proprietary core;
- treat it as a separate process/container;
- if EliteSCADA redistributes a binary/image, include required GPL license/notices and corresponding-source compliance for the distributed version;
- modified redistributed Z2M builds require their GPL source obligations;
- a user-managed independently installed Z2M materially simplifies distribution obligations.

Process separation does not by itself answer every legal question. Any EliteSCADA-bundled Z2M package is \`LEGAL_REVIEW_REQUIRED\` before commercial distribution.

The MQTT wire integration itself should be implemented independently from documented public topics/messages.

## Coordinator responsibility

In this path Zigbee2MQTT owns:
- radio adapter;
- network key/PAN/channel;
- interview;
- Zigbee reporting/configuration;
- device converter quirks;
- coordinator backup.

EliteSCADA must not race it for the same coordinator. A managed mode therefore needs F4 lease ownership assigned to the Z2M process.

## Backup

Z2M documents \`bridge/request/backup\` and persistent data. Coordinator backup support varies by adapter; current docs identify zStack and Ember as supporting coordinator backup.

EliteSCADA-managed deployment should back up:
- Z2M data directory/config without leaking plaintext secrets;
- coordinator backup when supported;
- version metadata.

Restore must validate adapter family and warn about migrations that may require re-pairing.

## Performance / scale

No universal Zigbee2MQTT device count should be treated as an EliteSCADA guarantee.

**PROPOSED ELITESCADA LIMITS** for v1 validation:
- import only user-selected devices/capabilities;
- rate-limit/coalesce repeated state changes per property;
- bound MQTT processing queues;
- expose dropped/coalesced message counters;
- load-test at least 1,000 exposed properties and reconnect storms before freezing limits.

## Recommended architecture

V1:
\`EliteSCADA zigbee2mqtt.bridge -> existing MQTT broker -> user-managed Zigbee2MQTT\`

Optional later:
\`EliteSCADA -> managed MQTT boundary -> managed Z2M sidecar -> leased coordinator\`

## Candidate stack

EliteSCADA bridge:
- standard MQTT client already acceptable under project dependency policy;
- JSON schema adapter generated from observed/documented Z2M messages.

External component:
- Zigbee2MQTT 2.14.2 line.

## License / distribution

Zigbee2MQTT: GPL-3.0.  
Do not embed into proprietary core.

User-managed service: preferred initial commercial boundary.  
EliteSCADA-managed redistributable sidecar: \`LEGAL_REVIEW_REQUIRED\`.

## Hardware

Hardware remains Z2M-owned. Use its current supported-adapter list. For an EliteSCADA managed lab, test one zStack and one Ember coordinator plus representative devices.

## EliteSCADA dependencies

- #472 F2/F3/F6;
- protected secret references;
- MQTT connection settings;
- optional LocalBridge lifecycle for managed mode;
- F4 resource lease only for managed-Z2M coordinator ownership.

## Capability mapping

Prefer semantic exposes:
- binary ON/OFF -> OnOff;
- numeric brightness -> Dimmer;
- color features -> ColorLight;
- temperature/humidity/occupancy/illuminance -> sensor capabilities;
- contact/water leak/smoke -> matching binary sensor;
- battery -> Battery;
- power/current/voltage/energy -> electrical capabilities;
- cover position/state -> Cover.

Unknown or manufacturer-specific exposes remain visible as unmapped diagnostics until deliberately modeled.

## Implementation slices

1. MQTT connection + bridge identity/version;
2. retained bridge/devices/exposes parser;
3. discovery candidates + selectable import;
4. state subscriptions and writes;
5. availability/reconnect/backpressure;
6. permit-join/interview workflow;
7. groups/scenes bounded support;
8. L2 Z2M container fixture;
9. L4 coordinator/device matrix;
10. optional managed sidecar only after common lifecycle contract.

## Tests

- **L0:** topic parser, exposes mapper, access semantics, message correlation.
- **L1:** fake MQTT broker/messages, retained rebuild, reconnect storms.
- **L2:** real Zigbee2MQTT process + MQTT broker with software/fake adapter where possible.
- **L3:** imported TAG/Command through Runtime/Gateway/Alarm/Historian and bridge restart.
- **L4:** real coordinator + declared devices.

## Risks

- fast-moving Z2M message/device definitions;
- GPL redistribution;
- MQTT broker security/misconfiguration;
- friendly-name instability;
- coordinator ownership collision;
- retained-state assumptions after broker reset.

## Open decisions

- whether EliteSCADA ever ships Z2M or remains user-managed only;
- whether to manage an MQTT broker or require an external broker;
- exact import behavior for groups/scenes.

## Readiness

\`GO_AFTER_FOUNDATION\`

## Sources

Accessed 2026-10-02:
- https://github.com/Koenkk/zigbee2mqtt/releases — 2.14.2 released 2026-10-01.
- https://github.com/Koenkk/zigbee2mqtt/blob/master/LICENSE — GPL-3.0.
- https://www.zigbee2mqtt.io/guide/usage/mqtt_topics_and_messages.html
- https://www.zigbee2mqtt.io/guide/usage/exposes.html
- https://www.zigbee2mqtt.io/guide/usage/pairing_devices.html
- https://www.zigbee2mqtt.io/guide/adapters/
- https://www.zigbee2mqtt.io/guide/faq/
