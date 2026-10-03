# Shelly RPC Research

Target: \`shelly.rpc\`  
Research date: **2026-10-02**  
Readiness: **\`GO_AFTER_FOUNDATION\`**

## Decision summary

Recommended architecture:

\`EliteSCADA built-in .NET JSON-RPC client -> local Shelly Gen2+ device\`

Use HTTP for bootstrap/one-shot operations and a persistent WebSocket for status/events/commands where appropriate.

Official Shelly documentation states **Gen2+ is controlled by JSON-RPC 2.0**. Current docs contain explicit Gen4 models, so the same RPC family is relevant across Gen2/Gen3/Gen4, subject to per-model component availability.

## Discovery / identification

Use:
- mDNS/local discovery where available;
- explicit IP/hostname;
- unauthenticated \`/shelly\` / \`Shelly.GetDeviceInfo\` for identification where current API permits it.

Record:
- device id;
- MAC;
- model;
- generation;
- firmware version;
- app/profile;
- auth enabled state.

Do not use IP address as the stable device identity.

## RPC channels

### HTTP

Best for:
- identification;
- one-shot status/config;
- fallback command.

Official docs state HTTP does not carry notifications.

### WebSocket — preferred runtime

Endpoint:
\`ws://<device>/rpc\` (or secure equivalent if device/platform supports it).

Use for:
- persistent connection;
- RPC commands;
- notifications/status updates;
- lower reconnect overhead.

Official docs state a client must send at least one request with a valid \`src\` before receiving notifications.

### MQTT

Shelly also exposes RPC via MQTT, but \`shelly.rpc\` should not require a broker. MQTT can be a later alternate transport.

## Authentication

Shelly Gen2+ HTTP/WebSocket authentication uses a digest mechanism based on RFC 7616 with SHA-256/HMAC conventions documented by Shelly.

Rules:
- password/derived credential is protected secret material;
- handle nonce expiry/retry correctly;
- never log challenge responses or password;
- support devices with auth disabled but show that status as a security diagnostic;
- encourage local auth on production deployments.

Do not store \`ha1\` or password in plaintext package fields.

## Component model

Shelly exposes component instances such as:
- \`switch:N\`;
- \`input:N\`;
- \`cover:N\`;
- \`light:N\` on applicable devices;
- \`temperature:N\`;
- \`humidity:N\`;
- \`em:N\` / \`em1:N\`;
- system/network components.

Dynamic/virtual components exist on newer firmware; import them only if semantics are explicit.

Gen4 devices can expose additional protocol features (for example Modbus, KNX, Matter, Zigbee on some models). \`shelly.rpc\` must still treat RPC as its own integration and not duplicate/claim those other protocols automatically.

## Capability mapping

| Shelly component | EliteSCADA |
|---|---|
| Switch | OnOff |
| Light | Light / Dimmer / ColorLight by traits |
| Input | Button or binary input diagnostic/capability |
| Cover | Cover |
| Temperature | TemperatureSensor |
| Humidity | HumiditySensor |
| EM/EM1 active power | PowerMeter |
| EM/EM1 energy | EnergyMeter |
| voltage/current | Voltage / Current |
| thermostat/climate component where documented | Thermostat/Climate |

Per-component status creates canonical TAGs; set methods become Commands/writable bindings.

## Runtime subscription model

On connect:
1. identify device/version/components;
2. read full status;
3. subscribe/listen to WebSocket notifications;
4. merge partial status updates by component;
5. emit canonical value changes;
6. reconnect with bounded backoff on disconnect;
7. perform full status reconciliation after reconnect.

Do not assume every firmware emits every field.

## Firmware compatibility

Record model + firmware. Maintain fixtures by major firmware line for supported generations.

If a method/component is absent:
- degrade that capability only;
- do not declare entire device offline unless transport is actually unavailable.

## Local-only first scope

No Shelly Cloud account is required for local RPC.

First release should not proxy through Shelly Cloud. This keeps operation local, deterministic and suitable for SCADA deployments.

## Security

- local network segmentation recommended;
- enable device authentication;
- protected credentials;
- no WAN exposure of device RPC;
- bounded RPC rate;
- read-only discovery before user confirms import;
- firmware update or factory-reset methods are **not** exposed as normal commands.

Configuration-destructive calls are external-device mutation and require a separate privileged engineering action if ever supported.

## Packaging

Built-in .NET client:
- HTTP/WebSocket;
- no native dependency;
- no sidecar;
- Windows/Linux/container friendly;
- mDNS optional.

## Performance / scale

Official Shelly RPC docs state a device supports a limited number of simultaneous non-persistent RPC channels (currently documented as 6). EliteSCADA should use one persistent WebSocket per device rather than opening many channels.

**PROPOSED ELITESCADA QUALIFICATION TARGETS**
- 250 devices / 1,500 components in L2;
- one persistent WS/device;
- bounded reconnect jitter;
- coalesce redundant high-frequency power updates only when Historian semantics permit;
- preserve energy counters and events.

## Recommended architecture

\`EliteSCADA DriverHost -> HTTP bootstrap + persistent WebSocket JSON-RPC -> Shelly Gen2+\`

## Candidate stack

First-party .NET JSON-RPC adapter using official Shelly API docs. No third-party runtime library is required beyond standard networking/JSON facilities.

## License / distribution

The integration consumes a public vendor API; no Shelly code is redistributed by default.

API documentation terms/trademark use should be checked at product-release time. \`LEGAL_REVIEW_REQUIRED\` only if EliteSCADA redistributes vendor assets/SDKs or uses brand logos beyond factual compatibility naming.

## Hardware

L4:
- one Gen2 relay/power meter;
- one Gen3 device;
- one Gen4 device;
- one cover-capable device;
- authenticated and unauthenticated configurations;
- different firmware versions where available.

## EliteSCADA dependencies

- F2/F3/F6;
- protected credential references;
- no Host Hardware.

## Implementation slices

1. discovery/GetDeviceInfo;
2. auth;
3. component inventory;
4. status mapper;
5. WS notifications/reconnect;
6. commands;
7. power/energy semantics;
8. Gen2/3/4 fixtures;
9. L4 model matrix.

## Tests

- **L0:** RPC framing/auth challenge/status merge/mapping.
- **L1:** fake HTTP/WS Shelly.
- **L2:** independent Shelly protocol simulator or recorded contract fixture plus local peer.
- **L3:** Runtime TAG/Command/Gateway/Alarm/Historian.
- **L4:** declared physical models/firmware.

## Risks

- component differences by model/firmware;
- local device security often disabled by users;
- high-rate power measurements;
- dynamic component growth;
- confusing Gen4 multi-protocol features with RPC scope.

## Open decisions

- minimum firmware version per generation;
- whether Gen2/3/4 all ship in one first-release support statement or staged;
- discovery implementation detail for mDNS.

## Readiness

\`GO_AFTER_FOUNDATION\`

## Sources

Accessed 2026-10-02:
- https://shelly-api-docs.shelly.cloud/gen2/General/RPCProtocol/
- https://shelly-api-docs.shelly.cloud/gen2/General/RPCChannels/
- https://shelly-api-docs.shelly.cloud/gen2/General/Authentication/
- https://shelly-api-docs.shelly.cloud/gen2/ComponentsAndServices/Shelly/
- https://shelly-api-docs.shelly.cloud/gen2/Devices/Gen4/Shelly1G4/
- https://shelly-api-docs.shelly.cloud/gen2/Devices/Gen4/ShellyEMG4/
