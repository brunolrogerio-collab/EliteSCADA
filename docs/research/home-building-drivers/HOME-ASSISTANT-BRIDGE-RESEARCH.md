# Home Assistant Bridge Research

Target: \`homeassistant.bridge\`  
Research date: **2026-10-02**  
Readiness: **\`GO_AFTER_FOUNDATION\`**

## Decision summary

Recommended first release:

\`EliteSCADA -> supported Home Assistant REST/WebSocket APIs -> user-managed Home Assistant\`

Use **WebSocket as the primary synchronization channel** and REST only for coarse bootstrap/health/action cases where it is simpler and publicly supported.

Do not read Home Assistant database internals and do not depend on private Python implementation objects.

## Authentication

Supported initial modes:
- Long-Lived Access Token entered by an administrator and stored through an EliteSCADA protected secret reference.
- HTTPS/WSS endpoint verification configurable with secure defaults.

Future:
- OAuth/IndieAuth flow using Home Assistant's supported authentication API for better token lifecycle/revocation.

The REST API documents Bearer authorization. The WebSocket API starts with \`auth_required -> auth -> auth_ok\`.

## Registry import

The official WebSocket surface includes registry commands used by Home Assistant clients. Current developer documentation and 2026 API-change notes confirm active use of:
- \`config/device_registry/list\`;
- entity-registry display/list commands;
- area relationships.

Core 2026.8/2026.9 changed device-registry ownership and introduced child-device serialization. Therefore the bridge must record the HA version and tolerate additive/different device shapes rather than pinning to one old JSON object layout.

### Mapping

\`HA Area -> EliteSCADA Location\`

- offer matching by name/path;
- user confirms/create/reuse;
- never make HA area id the EliteSCADA stable Location id.

\`HA Device -> EliteSCADA Equipment\`

- stable source identity stores HA device id + integration/source metadata;
- child devices are represented only when they add useful channels/capabilities;
- do not assume every device has manufacturer/model fields.

\`HA Entity -> Capability/TAG/Command\`

Use:
- entity domain;
- device_class where available;
- unit;
- state attributes;
- registry/device relation.

Examples:
- \`light\` -> Light / ColorLight / Dimmer;
- \`switch\` -> OnOff;
- numeric \`sensor\` -> temperature/humidity/power/energy/etc by device_class/unit;
- \`binary_sensor\` -> contact/occupancy/motion/leak/smoke/etc;
- \`cover\` -> Cover;
- \`lock\` -> Lock;
- \`climate\` -> Thermostat/Climate;
- \`fan\` -> Fan;
- \`scene\` -> Scene command;
- \`button\` -> Button/Command where appropriate.

Diagnostics/config entities should be excluded by default from normal import.

## User-selectable import

Do not mirror all HA entities automatically.

Flow:
1. connect/test;
2. load area/device/entity registries;
3. load current states;
4. present area/device tree;
5. user selects areas/devices/entities;
6. map capabilities;
7. preview generated Equipment/TAGs/Commands;
8. Apply.

Store source entity ids for reconciliation. When HA entities disappear/rename, mark source unavailable and offer remap; do not silently create duplicates.

## State and event model

Primary WebSocket use:
- initial states;
- \`state_changed\` subscription or current supported state subscription;
- registry change events where available/needed;
- action/service calls.

State updates must flow through normal canonical Runtime quality/event/historian handling.

HA \`unavailable\` or missing entities must become non-Good/unavailable source quality. Never fabricate the last value as healthy.

## Commands

Use supported action/service call mechanisms.

Before exposing a command:
- confirm entity domain/action;
- validate input schema as far as the public API provides;
- apply EliteSCADA authorization;
- send action;
- wait for authoritative subsequent HA state where applicable.

Do not show optimistic local state as process truth.

## Registry API compatibility

Home Assistant developer documentation changed materially in 2026:
- devices restricted to one config entry;
- child devices added;
- \`config/device_registry/list\` serialization changed/additive fields.

Version strategy:
- capture HA Core version at connect;
- maintain compatibility fixtures for currently supported HA release range;
- parse unknown fields permissively;
- fail a feature specifically when a required command is absent;
- no blanket assumption that undocumented registry internals remain stable forever.

## Network/security

Initial scope should be local/LAN Home Assistant.

Remote public-Internet HA endpoints may work technically over HTTPS/WSS but are not required for v1.

Requirements:
- token secret reference;
- TLS verification by default;
- configurable certificate trust without “accept all” shortcut;
- exponential reconnect + jitter;
- bounded subscriptions/queues;
- redact token from logs;
- revoke/replace credential without re-creating imported Equipment.

## Performance / scale

Home Assistant installations can have thousands of entities; EliteSCADA should not import indiscriminately.

**PROPOSED ELITESCADA LIMITS** for first qualification:
- 2,000 selected entities per HA bridge in L2/L3 load tests;
- bounded 10,000-event queue with latest-state coalescing per entity where safe;
- explicit lag/backpressure counters;
- registry refresh is incremental/diffed after initial load;
- reconnect uses one snapshot reconciliation rather than replaying uncontrolled duplicate events.

These are product test targets, not Home Assistant limits.

## Packaging

Built-in .NET network integration; no Home Assistant code needs to be redistributed.

Home Assistant itself is user-managed.

Its repository license is Apache-2.0, but this bridge only consumes documented APIs and therefore should not require packaging HA.

## Recommended architecture

\`EliteSCADA homeassistant.bridge -> WSS/HTTPS -> user-managed Home Assistant\`

No HA sidecar in EliteSCADA.

## Candidate stack

- standard .NET HTTP/WebSocket;
- JSON contract adapters;
- no HA Python library dependency required.

## License / distribution

Home Assistant Core repository: Apache-2.0.  
Bridge protocol use requires no copied HA implementation code.

Brand/trademark wording should remain factual (“Home Assistant integration”) and be reviewed for public marketing if logos are used.

## Hardware

No protocol-specific hardware for EliteSCADA. L4 equivalent is a real HA instance with multiple real integrations/devices.

Test host:
- current Home Assistant OS/container;
- at least Zigbee/MQTT, ESPHome, Shelly-like network entity and virtual/helper fixtures to exercise registry shapes.

## EliteSCADA dependencies

- Locations;
- Capability vocabulary;
- discovery candidate;
- protected token secret;
- common external integration diagnostics.

## Implementation slices

1. auth/connect/version/health;
2. registry snapshots;
3. states/subscriptions;
4. selectable import and mapping;
5. commands/readback;
6. reconciliation of renamed/removed/child devices;
7. load/backpressure/reconnect;
8. L2 pinned HA containers across supported release window;
9. L4 real HA instance.

## Tests

- **L0:** JSON parsers, mapping by domain/device_class, unavailable semantics.
- **L1:** fake WebSocket server/auth/state/registry changes.
- **L2:** real Home Assistant container via public API.
- **L3:** selected entities through Runtime/TAG/Command/Gateway/Alarm/Historian.
- **L4:** real HA integrations/devices and upgrade across supported versions.

## Risks

- registry WebSocket surface evolves;
- huge entity inventories;
- users importing diagnostic/config entities accidentally;
- long-lived token handling;
- HA service/action schema heterogeneity;
- duplicate physical device represented by multiple HA integrations.

## Open decisions

- minimum/maximum supported HA Core release window;
- whether OAuth is required for first public release or follows Long-Lived Tokens;
- default entity categories hidden from import.

## Readiness

\`GO_AFTER_FOUNDATION\`

## Sources

Accessed 2026-10-02:
- https://developers.home-assistant.io/docs/api/rest/
- https://developers.home-assistant.io/docs/api/websocket/
- https://developers.home-assistant.io/docs/auth_api/
- https://developers.home-assistant.io/docs/device_registry_index/
- https://developers.home-assistant.io/docs/entity_registry_index/
- https://developers.home-assistant.io/blog/2026/08/19/device-registry-websocket-api-changes/
- https://developers.home-assistant.io/blog/2026/07/21/device-registry-single-config-entry/
- https://github.com/home-assistant/core/blob/dev/LICENSE.md — Apache-2.0.
