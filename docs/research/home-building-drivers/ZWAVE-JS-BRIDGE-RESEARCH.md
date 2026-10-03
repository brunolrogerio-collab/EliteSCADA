# Z-Wave JS Bridge Research

Target: \`zwavejs.bridge\`  
Research date: **2026-10-02**  
Observed Z-Wave JS Server release: **3.10.1 (2026-08-07)**  
Readiness: **\`WAIT_DEPENDENCY\`**

## Decision summary

Recommended first Z-Wave implementation:

\`EliteSCADA -> managed Z-Wave JS Server -> leased USB Z-Wave controller -> Z-Wave network\`

Do not write a native Z-Wave stack in EliteSCADA first.

Z-Wave JS Server provides a dedicated process/WebSocket boundary and the underlying \`zwave-js\` project owns the complex Command Class, security, device configuration, interview and controller behavior.

Licenses:
- \`zwave-js-server\`: Apache-2.0;
- \`zwave-js\`: MIT.

## Why bridge first

Z-Wave complexity includes:
- controller NVM;
- inclusion/exclusion;
- S0/S2 security;
- SmartStart;
- interview stages;
- sleeping nodes;
- Command Classes;
- endpoint/value metadata;
- associations;
- routes/heal;
- firmware update;
- device compatibility/config database;
- controller migrations.

A mature dedicated stack sharply reduces protocol-maintenance risk.

## Controller hardware

First-release hardware family target:
- modern 700/800-series USB controllers;
- include one 500-series controller only for migration/legacy qualification if current stack support remains validated.

Do not promise every controller. Publish a tested model/firmware list.

F4 must own the physical controller lease. The sidecar receives only the leased resource.

## Inclusion / exclusion

External-network mutations:
- inclusion;
- exclusion;
- replace failed node;
- controller reset;
- NVM restore;
- SmartStart provisioning changes.

All require privileged confirmation/audit.

Normal discovery workflow:
1. acquire controller;
2. sidecar starts and opens network;
3. user starts inclusion with selected security policy;
4. S2 DSK/PIN confirmation where required;
5. node joins;
6. interview progresses;
7. node values/capabilities preview;
8. user assigns Location/name;
9. Apply canonical Equipment/TAGs/Commands.

Exclusion should clearly distinguish removing a device from the Z-Wave network from deleting EliteSCADA canonical configuration.

## Security

Protect:
- S0 network key;
- S2 Unauthenticated key;
- S2 Authenticated key;
- S2 Access Control key;
- LR keys where applicable;
- controller backup/NVM;
- SmartStart DSK/provisioning data.

Never persist these plaintext in \`.escadapkg\`.

The sidecar state directory is sensitive backup material.

## Node interview and sleepy devices

Interview is asynchronous and can take materially longer for sleeping battery nodes.

Represent:
- interview stage/progress;
- awake/asleep;
- last seen;
- ready/dead/alive state;
- pending config refresh.

Do not label sleeping devices unavailable merely because they do not answer immediately.

Z-Wave JS Server 3.10.0 added forwarding of interview-progress events, making it practical to expose truthful commissioning progress through the bridge.

## Command Classes / values

Do not directly expose raw Command Classes as the primary user model.

Use value metadata:
- command class;
- endpoint;
- property/propertyKey;
- readable/writeable;
- type;
- unit;
- min/max/states;
- metadata/CC semantics.

Map to common Capabilities only with clear semantics.

Examples:
- Binary Switch -> OnOff;
- Multilevel Switch -> Dimmer/Light;
- Color Switch -> ColorLight;
- Multilevel Sensor -> typed sensor capability;
- Notification Access Control -> Contact/Lock diagnostics where semantics match;
- Door Lock -> Lock;
- Thermostat Setpoint/Mode/Operating State -> Thermostat/Climate;
- Meter -> PowerMeter/EnergyMeter/Voltage/Current by scale;
- Battery -> Battery;
- Central Scene -> Button/scene event;
- Window Covering -> Cover where supported.

Keep unmapped values available in advanced diagnostics.

## Associations

Associations alter external network behavior. Do not silently create them as part of normal EliteSCADA automation.

First release:
- discover/read association info for diagnostics;
- only configure associations needed by the stack for correct operation;
- user-authored associations can be a later advanced feature with explicit confirmation.

## Heal / route maintenance

Network heal/rebuild routes is a maintenance operation, not ordinary polling. Expose only in Engineering/maintenance with progress and cancellation where supported.

Avoid automatic whole-network heal after every restart.

## Firmware / OTA

Treat node firmware update as later/advanced scope unless the first device matrix explicitly requires it.

If exposed:
- user supplies/chooses firmware with manufacturer/model validation;
- progress;
- device power/sleep warnings;
- failure/recovery;
- no silent update.

## Controller backup/recovery

Z-Wave JS includes NVM tooling/backup capabilities in the ecosystem. Backup/recovery must be part of L4 before production claim.

Requirements:
- controller identity/model/sdk version;
- encrypted/protected backup;
- same-controller restore;
- migration compatibility explicitly tested;
- rollback instructions;
- never overwrite live NVM without privileged confirmation.

## Server API / lifecycle

The WebSocket server schema is versioned. Client should:
- handshake version/schema;
- pin supported server range;
- fail with actionable incompatibility;
- subscribe to node/value/controller events;
- correlate commands;
- handle sidecar restart;
- persist server/controller state.

## Certification / trademark

Z-Wave protocol integration via Z-Wave JS does not automatically make EliteSCADA a Z-Wave Certified product.

Any Z-Wave Certified logo/marketing claim is separate from open-source library licensing and must follow current Z-Wave Alliance program requirements.

\`LEGAL_REVIEW_REQUIRED\`.

## Packaging

Managed sidecar/container:
- Node runtime;
- pinned Z-Wave JS Server;
- loopback WebSocket;
- persistent state volume;
- leased USB device;
- health/logs/restart policy;
- backup hooks.

User-managed external server can be supported later as an advanced deployment if the WS API remains compatible.

## Performance / scale

Z-Wave is a low-bandwidth mesh. Do not treat it like Ethernet telemetry.

**PROPOSED ELITESCADA QUALIFICATION TARGETS**
- 100 nodes in software/event fixtures;
- L4 network of at least 20 mixed mains/battery nodes if feasible;
- bounded command queue;
- no polling storm of sleeping nodes;
- backoff after controller reconnect;
- preserve event ordering for critical state transitions.

These are validation targets, not Z-Wave protocol limits.

## Recommended architecture

\`EliteSCADA zwavejs.bridge -> managed Z-Wave JS Server -> F4 ZWaveController -> network\`

## Candidate stack

- Z-Wave JS Server 3.10.1 — Apache-2.0;
- Z-Wave JS 15.x line under server — MIT.

## License / distribution

Permissive licenses permit commercial redistribution subject to notice/license terms. Certification/trademark remains independent.

## Hardware

L4:
- one tested 800-series USB controller;
- optional 700-series controller;
- optional 500-series legacy/migration controller;
- mains switch/router;
- battery contact sensor;
- motion/notification sensor;
- lock if S2 Access Control is in scope;
- multilevel/power meter;
- cover/thermostat representative if those capabilities ship.

## EliteSCADA dependencies

- F2/F3/F4/F6;
- LocalBridge lifecycle;
- protected S0/S2/NVM material;
- external-network mutation confirmation.

## Implementation slices

1. sidecar lifecycle/schema handshake;
2. F4 controller lease/open;
3. node/value inventory;
4. capability mapper;
5. subscriptions/commands;
6. inclusion/exclusion/S2 workflow;
7. sleepy/interview progress;
8. backup/recovery;
9. maintenance diagnostics;
10. L2/L4 validation.

## Tests

- **L0:** WS schemas/value metadata/capability mapping/security redaction.
- **L1:** fake WS server/event sequences.
- **L2:** real Z-Wave JS Server with mock/test driver facilities where supported.
- **L3:** TAG/Command/Gateway/Alarm/Historian + restart.
- **L4:** controller + declared physical node matrix.

## Risks

- controller firmware/NVM migration;
- certification wording;
- long interviews for sleeping nodes;
- large Command Class surface;
- sidecar/server schema versioning;
- RF mesh behavior only reproducible with real hardware.

## Open decisions

- first certified/tested controller model list;
- whether user-managed Z-Wave JS Server is supported in v1;
- exact initial Command Class/capability scope;
- whether firmware update is first-release or deferred.

## Readiness

\`WAIT_DEPENDENCY\`

Dependency: F4 Host Hardware/controller lease and common LocalBridge lifecycle must exist.

## Sources

Accessed 2026-10-02:
- https://github.com/zwave-js/zwave-js-server/releases — 3.10.1.
- https://github.com/zwave-js/zwave-js-server/blob/master/LICENSE — Apache-2.0.
- https://github.com/zwave-js/zwave-js/blob/master/LICENSE — MIT.
- https://zwave-js.github.io/zwave-js/
- https://zwave-js.github.io/nvmtool/
- https://zwave-js.github.io/qr/
- Z-Wave Alliance certification/trademark material, to be revalidated before release.
