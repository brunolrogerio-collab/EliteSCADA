# EliteSCADA Driver / TAG / Memory / Script Integration Research

Research date: **2026-10-02**  
Research owner: #475  
Parent architecture: #472  
Scope: **RESEARCH_ONLY / DOCS_ONLY / NO_PRODUCT_CODE**

## Purpose

This document connects the HOME/BUILDING driver dossiers to the **actual EliteSCADA runtime and Engineering architecture**.

The question is not only how Zigbee, Matter, Home Assistant, ESPHome, Shelly, Z-Wave and DALI communicate with their devices. The implementation question is:

> How does each integration enter canonical EliteSCADA so that TAGs, Memory Data Sources, Commands, Gateway, Scripts, Alarms, Historian, realtime HMI, security, Audit and Active lifecycle all continue to work without a second smart-home core?

The repository evidence strongly supports one answer:

\`\`\`text
Protocol / Bridge / Controller
        |
        v
Driver-owned Data Source
        |
        v
canonical TAG / Command / Equipment
        |
        v
ITagRegistry + CurrentTagCache
        |
        v
TagValueChanged
        |
        +--> Alarm Engine
        +--> Historian
        +--> TAG Gateway
        +--> Server Scripts
        +--> Realtime WebSocket / HMI
        +--> other canonical Runtime consumers
\`\`\`

External drivers **must not** write directly to Historian, Alarm, HMI, Server Memory or scripts.

They become part of EliteSCADA by owning canonical TAGs and publishing canonical \`TagValue\` state through the shared cache/event boundary.

---

# 1. Existing canonical runtime boundary

## 1.1 ICommunicationDriver

The current active external-driver contract is deliberately small:

- lifecycle: \`StartAsync\` / \`StopAsync\`;
- \`Tags\`;
- \`ReadAsync(tagId)\`;
- \`WriteAsync(tagId, value)\`;
- capabilities/status;
- disposal.

This is the correct boundary for HOME/BUILDING protocol adapters too.

A native Zigbee service, Matter sidecar, Z-Wave JS Server, Home Assistant API, ESPHome TCP client or Shelly WebSocket is an **implementation detail behind this boundary**.

Protocol SDK objects must not escape into Core or canonical Engineering.

## 1.2 Runtime planner / factory / descriptor

Modern converged communication drivers use three related pieces:

1. \`CommunicationDriverTypeDescriptor\`
   - stable DriverType;
   - runtime capabilities;
   - Engineering capabilities;
   - acquisition modes;
   - Data Source configuration schema;
   - TAG binding schema.

2. \`ICommunicationDriverRuntimePlanner\`
   - compiles canonical Engineering Data Source + TAG definitions into a library-independent runtime plan.

3. \`ICommunicationDriverRuntimeFactory\`
   - creates the active driver using shared Runtime services:
     - project key;
     - \`ICurrentTagCache\`;
     - \`ITagRegistry\`;
     - protected-material resolver.

New HOME/BUILDING communication drivers should join this explicit composition model rather than create private activation services.

A sidecar may exist, but the canonical Runtime still sees one normal driver/provider.

---

# 2. Data Source meaning

A Data Source is the canonical owner/provider instance for TAG values.

Current persisted shape contains:

- stable optional \`Id\`;
- \`Key\`;
- \`Name\`;
- \`Driver\`;
- \`Enabled\`;
- non-secret \`Settings\`;
- \`SecretReferences\`;
- metadata.

For HOME/BUILDING this should mean:

| Integration style | Natural Data Source instance |
|---|---|
| Native Zigbee | one Zigbee network/controller instance |
| Zigbee2MQTT | one Z2M bridge/broker/base-topic instance |
| Home Assistant | one HA server instance |
| Matter | one controller/fabric integration instance |
| Z-Wave JS | one controller/network instance |
| DALI native | one DALI controller/bus instance |
| DALI gateway | the underlying KNX/Modbus/BACnet/IP gateway Data Source |
| ESPHome | normally one ESPHome device connection with current runtime model |
| Shelly RPC | normally one Shelly device connection with current runtime model |
| KNX/IP | one KNX/IP interface/router/project context |
| Cloud connector | one tenant/project/account connection |

## 2.1 Controller integrations vs direct-device integrations

There are two different shapes.

### Controller / bridge / server integrations

One Data Source naturally owns many devices:

- Zigbee coordinator;
- Zigbee2MQTT;
- Home Assistant;
- Matter controller;
- Z-Wave controller;
- DALI bus/controller;
- KNX/IP project/router.

One active driver can therefore own many TAGs across many Equipment objects.

### Direct-IP device integrations

ESPHome and Shelly connect directly to individual devices.

With the current runtime model, the lowest-risk implementation is:

\`\`\`text
one discovered physical IP device
    -> one Data Source/provider instance
    -> many TAGs for that device
    -> one Equipment
\`\`\`

The HOME/BUILDING Engineering UX can still present one catalog integration and batch discovery. It may create multiple child Data Sources behind a friendly workflow.

Do **not** force one giant Shelly or ESPHome driver instance to multiplex hundreds of unrelated device sessions merely to make the UI look like one integration.

Provider isolation is operationally useful:
- one failed device does not fault all peers;
- credentials and endpoints remain scoped;
- diagnostics are per device;
- restart/reconciliation is easier.

# 3. Stable Data Source identity

Current communication compilation already treats \`Tag.DataSourceId\` as authoritative when present and uses legacy \`Source\` key matching only when there is no stable ID.

This is important for HOME/BUILDING because human names change frequently.

Future generated TAGs should therefore persist:
- stable TAG ID;
- stable Data Source ID;
- readable path/name as presentation.

A Data Source rename must not rebind a TAG accidentally.

## Finding: Memory association is not yet fully converged

The current \`InternalMemoryRuntimePlanner\` still locates memory TAGs by the legacy \`Source == dataSource.Key\` string.

Communication planners already have the stronger stable-ID reconciliation helper.

### RESEARCH_CONTRACT_DELTA_REQUIRED — MEMORY-DATASOURCE-STABLE-ID-01

Internal Memory planning should eventually consume the same stable DataSourceId authority as communication planning.

Reason:
- rename safety;
- delete/recreate safety;
- consistency with future generated HOME/BUILDING objects.

This research does **not** implement the delta.

---

# 4. TAG is the common process-data contract

A HOME/BUILDING device is **not** represented in Runtime by a protocol-specific object graph.

Its useful process values become canonical TAGs.

The persisted TAG already supports:
- stable ID;
- name/path;
- canonical data type;
- Data Source identity;
- read-only vs writable;
- engineering unit;
- Historian policy;
- AccessPolicy;
- protocol binding;
- metadata;
- optional selectors/physical transformations.

That is sufficient for a large part of the HOME/BUILDING first releases.

## 4.1 CommunicationTagBinding

For external communication TAGs the canonical envelope is:

- contract version;
- Driver-owned schema id/version;
- portable protocol address/identity;
- public non-secret settings;
- optional physical value transform.

Secrets belong at Data Source level as protected references.

### Rule

\`PortableAddress\` must encode protocol-stable meaning, **not a library/session object**.

Conceptual examples:

| Integration | Durable TAG binding content |
|---|---|
| Native Zigbee | device IEEE identity + endpoint + cluster + attribute/semantic property |
| Z2M | Zigbee stable device identity + endpoint/expose/property; do not make friendly name the identity |
| DALI | gateway/bus + addressed gear/input + semantic property |
| HA | source registry identity + current entity address/reference for reconciliation |
| Matter | fabric/controller context + node + endpoint + cluster + attribute/feature |
| ESPHome | device Data Source + stable entity key/type |
| Shelly | device Data Source + component id + property |
| Z-Wave | controller/home context + node + endpoint + Command Class/value identity |
| KNX/IP | group address + DPT/semantic metadata |
| BTHome | BLE device identity + object/measurement id |

The exact string grammar should be versioned by each driver schema.

## 4.2 Do not duplicate raw device state into Server Memory

A frequent anti-pattern would be:

\`\`\`text
device -> driver -> Server Memory -> real TAG
\`\`\`

This should **not** be the normal path.

The driver already owns the authoritative external TAG.

Duplicating every device state into Memory would create:
- two authorities;
- stale-copy races;
- duplicate Historian/alarm events;
- ambiguous writes;
- unnecessary persistence.

Correct:

\`\`\`text
device -> driver TAG -> CurrentTagCache
\`\`\`

Use Server Memory only for genuine internal application state.

---

# 5. CurrentTagCache is the runtime fan-out point

Every accepted external or Server Memory value should reach the common \`CurrentTagCache\`.

\`CurrentTagCache.UpdateAsync\`:
1. validates the TAG/value identity;
2. replaces the latest value;
3. publishes one canonical \`TagValueChanged\`.

That event is what connects a new driver to the rest of EliteSCADA.

## 5.1 Quality

Drivers own protocol evidence, but **EliteSCADA owns the quality vocabulary**.

Examples:
- successful Zigbee report -> Good;
- disconnected Matter controller -> BadCommunication/Unavailable according to canonical mapping;
- stale MQTT/Z2M property -> Stale;
- malformed payload -> Bad;
- sleeping device -> not automatically bad if its expected reporting model says otherwise.

Do not expose:
- MQTT QoS;
- Z-Wave node status;
- Matter status code;
- Zigbee LQI

as though they were themselves \`TagQuality\`.

They are inputs to a deliberate quality mapping.

## 5.2 Timestamps

Where the protocol exposes them:
- local publication/observation stays \`TagValue.Timestamp\`;
- device measurement time can become \`SourceTimestamp\`;
- intermediary server time can become \`ServerTimestamp\`.

Drivers must not manufacture protocol timestamps from receipt time.

---

# 6. What the driver gets automatically after cache publication

## 6.1 Historian

Historian implementations subscribe to \`TagValueChanged\`.

They do not know whether the source is:
- Modbus;
- Zigbee;
- Matter;
- HA;
- Server Memory;
- ESPHome.

Therefore a new driver needs **no Historian adapter**.

If the TAG's capture policy admits the sample, the normal asynchronous Historian pipeline handles it.

This preserves the key architecture property:

> A slow historian must not block the protocol scan/subscription directly.

## 6.2 Alarm Engine

The Alarm Engine also subscribes to \`TagValueChanged\`.

It evaluates canonical Alarm definitions against:
- value;
- TAG identity;
- quality.

Non-Good quality already feeds communication-alarm semantics.

A new home driver therefore must publish truthful value/quality; it must not implement private alarm logic.

## 6.3 Realtime / HMI

\`TagRealtimeHub\` subscribes to the same \`TagValueChanged\` stream.

It:
- re-checks authorization;
- publishes value/quality/timestamp/source;
- uses bounded per-client queues;
- fails closed for invalid sessions.

Therefore:
- a Zigbee temperature sensor;
- a Matter light;
- a Shelly power meter;
- a Z-Wave contact sensor

become normal HMI values simply by being normal TAGs.

No driver-to-React bridge is required.

## 6.4 High Availability / Active authority

Runtime events are wrapped by \`RuntimeEventGate\`.

Passive materialization does not start industrial sources and does not forward process effects.

New sidecars/controllers must therefore be lifecycle-owned by the active Runtime/provider boundary. They must not independently start on a Standby node and publish around the authority fence.

F4 resource leasing and LocalBridge lifecycle must preserve that rule.

---

# 7. Server Memory: where it belongs

\`builtin.memory.server\` is:
- server-owned;
- shared;
- retentive;
- one authoritative value;
- no network transport.

It participates in:
- CurrentTagCache;
- TagValueChanged;
- realtime;
- authorization;
- scripts;
- optional Historian;
- alarms;
- Gateway.

## Good uses with HOME/BUILDING

Examples:

- \`Building.Mode\` = Normal / Away / Night;
- retained occupancy aggregation state;
- last selected global HVAC strategy;
- internal sequence state;
- script-maintained counters;
- operator-adjustable virtual setpoint not stored in a physical device;
- a calculated demand-limiting state;
- a retained “automation enabled” flag.

## Bad uses

Do not mirror:
- every Zigbee attribute;
- every HA entity;
- every Shelly status;
- every Z-Wave value

into Server Memory merely to make them accessible to scripts.

They are already accessible as normal TAGs.

## Server Memory as a bridge endpoint

Server Memory is useful when an explicit internal state boundary is desired:

\`\`\`text
device TAG
   -> TAG Gateway or Server Script
   -> Server Memory TAG
   -> Script / Alarm / Historian / HMI
\`\`\`

or:

\`\`\`text
Server Memory desired value
   -> TAG Gateway / Script
   -> writable device TAG
   -> owning Driver
\`\`\`

This is deliberate application logic, not driver plumbing.

---

# 8. Client Memory: where it belongs

\`builtin.memory.client\` is intentionally different:
- owned by one Runtime Client/browser instance;
- not globally shared;
- non-retentive server-side;
- not a global Historian source;
- not a global Alarm source;
- not part of the server-wide current TAG cache.

Correct uses:
- selected room/equipment;
- local filters;
- popup state;
- navigation return target;
- local UI mode;
- local script temporary values.

Wrong uses:
- current Zigbee device state;
- process permissive;
- global occupancy;
- shared setpoint;
- security state;
- controller ownership.

### Rule for HOME/BUILDING

A physical driver must never use Client Memory as its backend state store.

Client Visual scripts may combine:
- shared driver TAGs;
- local Client Memory

to implement presentation behavior.

---

# 9. TAG Gateway is the canonical simple cross-source bridge

The existing Gateway is explicitly protocol independent:

\`\`\`text
Source TAG
   -> Gateway route
   -> Destination TAG
   -> destination owning provider
\`\`\`

Gateway subscribes to \`TagValueChanged\` or reads current cache periodically, performs validated conversion, then calls the same Runtime write boundary.

It does **not** call another driver directly.

## HOME/BUILDING examples

### Device -> Server Memory

\`\`\`text
Zigbee Occupancy TAG
  -> Gateway
  -> Server.Building.LastOccupancy
\`\`\`

only when a separate retained/internal value is actually useful.

### Server Memory -> physical device

\`\`\`text
Server.HVAC.NightSetpoint
  -> Gateway
  -> Matter Thermostat TargetTemperature TAG
\`\`\`

### Cross-protocol

\`\`\`text
Shelly Power TAG
  -> Gateway
  -> Modbus Server Holding TAG
\`\`\`

or:

\`\`\`text
DALI Light Level TAG
  -> Gateway
  -> HA imported helper/state target
\`\`\`

where the destination is legitimately writable.

## Existing protections

Gateway already validates:
- destination exists;
- destination writable;
- owning provider exists;
- no self-route;
- type compatibility;
- deterministic single-writer constraints;
- cycle constraints;
- quality policy.

No \`ZigbeeToModbusGateway\`, \`MatterToHA\` or other protocol-pair API should be created.

---

# 10. Server Scripts

Server Scripts already operate over the canonical TAG layer.

They do **not** receive:
- Driver objects;
- protocol SDK handles;
- network sockets;
- database access;
- secrets.

A script receives only declared dependencies from the Active revision.

## 10.1 Reading driver TAGs

Once a HOME/BUILDING driver publishes a canonical TAG, Server Script can consume it through normal declared \`Tag\` dependency.

Example conceptual automation:

\`\`\`text
Zigbee Occupancy TAG changed
  -> TagChanged server script
  -> read Matter illumination TAG
  -> calculate decision
  -> write DALI/Matter light TAG
\`\`\`

The script never knows how Zigbee or Matter works.

## 10.2 Writing driver TAGs

\`write_tag\` is replayed by the .NET host through:

\`\`\`text
Server Script
  -> ServerScriptRuntimeManager
  -> IEngineeringRuntimeCoordinator.WriteAsync(tagId, value)
  -> owning driver/provider
\`\`\`

The script therefore cannot bypass:
- active revision identity;
- TAG existence;
- read-only state;
- provider ownership.

## 10.3 Server Memory in scripts

For stateful automation, scripts can deliberately use Server Memory:
- \`read_server_memory\`;
- \`write_server_memory\`;
- qualified Server Memory publication where explicit quality is allowed.

Only Server Memory can receive script-originated explicit quality through the qualified-source contract.

A script **cannot** spoof arbitrary quality on a physical driver TAG.

## 10.4 TagChanged

Server scripts subscribe to the same \`TagValueChanged\` event stream.

Therefore driver-originated state changes can trigger server automation without a driver-specific callback API.

---

# 11. Client Visual Scripts

Client Visual Python has a separate constrained capability bridge.

Relevant capabilities:
- TAG read;
- TAG write;
- Client Memory read/write;
- visual property read/write;
- bounded backend operation requests.

TAG write path is still:

\`\`\`text
Client Visual Script
  -> protected Runtime TAG API
  -> authorization + Audit
  -> ScadaRuntimeFacade
  -> owning driver/provider
\`\`\`

The browser script does not receive the driver.

Before a scripted write, the client re-resolves the declared TAG and verifies its stable identity to prevent path-reuse drift.

Engineering Preview can explicitly disable TAG writes.

Client Memory remains local to the Runtime Client.

---

# 12. Operator / HMI write path

Normal Runtime TAG writes use:

\`\`\`text
HMI / client request
  -> /api/tags/{stableId}/write
  -> resolve active TAG
  -> reject ReadOnly
  -> TAG write authorization
  -> security Audit denied/succeeded/failed
  -> ScadaRuntimeFacade.WriteAsync
  -> HA / installation authority fence
  -> EngineeringRuntimeCoordinator.WriteAsync
  -> owning ICommunicationDriver / ServerMemory provider
\`\`\`

Every new driver gains the same write security boundary by becoming the owning provider.

Do not expose a protocol-specific public write endpoint merely because a protocol SDK makes that convenient.

---

# 13. Write/readback policy for HOME/BUILDING drivers

Not every protocol should update the cache in the same moment.

## Request/response with authoritative confirmed value

If the protocol response itself truthfully confirms the effective value, the driver may publish the confirmed state.

## Subscription/event-authoritative protocols

For:
- MQTT/Z2M;
- Home Assistant state_changed;
- Matter subscriptions;
- ESPHome state subscriptions;
- Shelly status notifications;
- Z-Wave value updates;

prefer:

\`\`\`text
write intent
  -> protocol request
  -> accepted/sent
  -> wait for authoritative state event/readback
  -> cache update
\`\`\`

Do not show an optimistic value as process truth.

If the device clamps or modifies a value, the canonical TAG must show the resulting device value.

## Timeouts

A write timeout/failure:
- must not fabricate success;
- should retain previous current value with truthful quality/diagnostic behavior;
- surfaces write failure to caller;
- does not make the frontend owner of truth.

---

# 14. Commands and writable TAGs

Current canonical Command is a deliberate action targeting a TAG with an engineered value.

This fits many common HOME/BUILDING operations.

Examples:
- turn on;
- turn off;
- close/open if represented as a target state;
- select a fixed mode;
- recall a simple engineered state that maps to a writable TAG.

Continuous state generally fits writable TAGs better:
- brightness;
- target temperature;
- cover position;
- fan speed.

## 14.1 Not every protocol method is a process Command

Do not expose these through ordinary TAG write just because the SDK has a method:
- pair/include;
- exclude/remove;
- Zigbee permit join;
- network heal;
- controller reset;
- firmware update;
- Matter fabric removal;
- DALI addressing;
- Shelly reboot/factory reset;
- Z-Wave association maintenance;
- HA administration/configuration services.

Those belong to protected Engineering/maintenance operations.

## 14.2 Rich method-style actions

Some useful process actions may not fit the current fixed-value Command model:
- protocol scene recall with parameters;
- rich Home Assistant service_data;
- Matter InvokeCommand payloads;
- vendor-specific button/action endpoints.

First releases should prefer stateful writable TAG semantics when truthful.

### RESEARCH_CONTRACT_DELTA_REQUIRED — RICH-COMMAND-BINDING-01

If future accepted capabilities require parameterized non-stateful actions, define a protocol-neutral versioned Command/Action binding rather than adding direct driver method calls from UI/scripts.

Required properties:
- stable target Equipment/Capability/provider identity;
- versioned typed arguments;
- authorization/Audit;
- active-revision resolution;
- driver-owned internal translation;
- no protocol SDK object in canonical Engineering.

Do not implement this until a real accepted first-release use case requires it.

---

# 15. Transient device events

A significant HOME/BUILDING difference from classic PLC values is the number of **edge events**:

- Zigbee remote button press;
- Z2M \`action\`;
- Z-Wave Central Scene;
- Matter switch/button events;
- DALI input-device button events;
- some Shelly input event types.

These are not necessarily stable process state.

Do not manufacture:
- a Boolean TAG that pulses unpredictably;
- a permanent string TAG called \`last_action\`

as the only canonical representation merely because TAGs already exist.

The current Driver SDK gives drivers cache/registry services but no protocol-neutral canonical transient-event publisher.

EliteSCADA does have \`OperationalEventOccurred\`, but current operational events are Active, engineer-authored definitions and the communication-driver runtime service does not directly own that emission boundary.

### RESEARCH_CONTRACT_DELTA_REQUIRED — DRIVER-TRANSIENT-EVENT-01

F3/F6 should define how a discovered \`Button\`/event capability becomes a canonical Runtime event without inventing persistent state.

A future contract should:
- map a protocol event to a stable Capability/Equipment identity;
- remain bounded and typed;
- publish through Active Runtime authority/event gate;
- support Script trigger and optional operational-event history;
- preserve authorization/visibility;
- not give Driver code access to arbitrary event-bus internals;
- not turn every raw radio frame into a public event.

Until that contract exists, first releases should scope event-only capabilities carefully.

---

# 16. Discovery / browse / import

Existing Driver SDK already separates Engineering evidence from active Runtime.

Discovery/browser/import returns **transient candidates**.

It must not mutate Working Engineering directly.

Canonical flow remains:

\`\`\`text
discover / browse
   -> candidate
   -> validate
   -> preview
   -> user selects
   -> apply
   -> Save / Publish / Activate
   -> runtime planner
   -> active Driver
\`\`\`

For HOME/BUILDING, F6 must enrich this enough to propose:
- Device identity;
- Equipment;
- Capabilities;
- TAGs;
- Commands;
- Location assignment;
- child endpoint/channel identity;
- potentially child Data Sources for direct-IP devices.

## Existing planned F6 gap

Current generic \`DriverDiscoveryCandidate\` mainly describes a potential Data Source.

HOME/BUILDING requires a richer composite candidate, already anticipated by #472.

For controller integrations:
- discovery occurs inside an existing Data Source/network.

For direct-IP device discovery:
- one candidate may need to propose a new child Data Source + Equipment + TAGs.

This should be resolved centrally in F6, not independently in Shelly, ESPHome, Matter, etc.

---

# 17. Equipment and Capability relationship

The parent #472 direction is correct:

\`\`\`text
user-facing: Location -> Device -> Capability
runtime authority: Equipment -> TAG / Command -> Driver
\`\`\`

Capability is semantic metadata/projection. It **does not replace TAG**.

Example:

\`\`\`text
Equipment: Sala / Sensor Janela
  Capability Contact
      -> TAG: Home.Sala.Janela.Contact
  Capability Battery
      -> TAG: Home.Sala.Janela.Battery

Data Source: Zigbee Casa
  owns both TAG bindings
\`\`\`

The same visual/control template can consume a \`Contact\` capability regardless of:
- Zigbee;
- Z-Wave;
- Matter;
- Home Assistant.

Protocol identity remains behind the binding.

---

# 18. Driver-by-driver wiring

## 18.1 Native Zigbee

### Data Source

One Data Source per Zigbee coordinator/network.

Data Source owns:
- F4 coordinator resource reference;
- network identity;
- channel/PAN policy;
- protected network-key reference;
- sidecar configuration;
- permit-join policy defaults;
- diagnostics.

### Equipment

One physical Zigbee device normally becomes one Equipment.

Multi-endpoint devices remain one Equipment unless endpoints are semantically independent equipment.

### TAG binding

Conceptual identity:
- IEEE address;
- endpoint;
- cluster;
- attribute or semantic property.

Examples:
- OnOff state -> Boolean writable TAG;
- CurrentLevel -> numeric writable TAG;
- Temperature -> numeric read-only TAG;
- battery -> numeric read-only TAG.

### Writes

\`\`\`text
Runtime TAG write -> Zigbee driver -> sidecar -> ZCL write/command -> device
                                            |
                                            +-> report/readback -> cache
\`\`\`

Prefer device reporting/readback as authority.

### Server Memory

Only for internal automation state, not Zigbee shadow storage.

### Scripts

Any Zigbee TAG is immediately usable as declared Script TAG dependency.

### Event gap

Remote-control button events require \`DRIVER-TRANSIENT-EVENT-01\` or deliberate first-release restriction.

---

# 19. Zigbee2MQTT

## Data Source

One Data Source represents one Z2M bridge connection:
- MQTT broker;
- base topic;
- TLS/auth secret references;
- expected bridge identity/version.

Z2M owns the coordinator/network in the user-managed v1 path.

## Equipment

One selected Zigbee2MQTT device -> one Equipment.

## TAG binding

Do not use Z2M friendly name as durable identity.

Persist stable Zigbee identity + endpoint/expose/property, then resolve current friendly-topic routing from bridge metadata.

## Runtime path

\`\`\`text
Z2M MQTT state
  -> zigbee2mqtt.bridge parser
  -> canonical TagValue
  -> CurrentTagCache
\`\`\`

A dedicated Z2M driver may internally reuse MQTT transport infrastructure, but canonical Engineering should not require users to construct a second hidden \`mqtt.raw\` object graph.

## Write

\`\`\`text
writable TAG
  -> bridge driver
  -> Z2M /set
  -> state response
  -> cache
\`\`\`

## Scripts / Gateway / Historian / Alarm

No special adapter; normal TAG pipeline.

## Event gap

Z2M \`action\` values should become proper transient events, not fake persistent process state.

---

# 20. DALI

## Gateway first path

When a DALI gateway exposes KNX/Modbus/BACnet/IP:

\`\`\`text
canonical existing protocol Data Source
  -> gateway points
  -> DALI semantic profile
  -> Equipment / Capability projection
\`\`\`

Do not duplicate those values into a fake \`dali.gateway\` runtime driver merely for naming.

A profile/template can give the underlying TAGs DALI Equipment/Capability semantics.

## Native future path

One DALI controller/bus Data Source owns:
- host DALI interface;
- bus topology;
- commissioning state;
- gear/input TAGs.

### TAGs

State-like:
- level;
- colour temperature;
- fault;
- presence;
- illuminance.

### Commands/writes

- on/off;
- level;
- colour;
- stateful scene target only where deliberately modeled.

### Engineering-only mutations

- address assignment;
- group programming;
- bus reset;
- commissioning.

### Events

Pushbuttons/input instances need the transient-event contract if edge-only.

---

# 21. Home Assistant Bridge

## Data Source

One Data Source per HA server instance:
- URL;
- TLS policy;
- token secret reference;
- expected/supported version.

## Equipment

Selected HA Device -> Equipment.

HA Area reconciles to EliteSCADA Location, but HA Area id does not become the EliteSCADA Location identity.

## TAGs

Selected HA entities become TAGs when they represent process state:
- sensors;
- binary sensors;
- switches;
- lights;
- covers;
- locks;
- climate;
- fans.

Do not import every diagnostic/config/helper entity automatically.

## Binding identity

Persist enough source-registry identity to reconcile rename/removal.

Treat current \`entity_id\` as an API address/presentation routing value, not the only identity if the registry supplies stronger identity.

## Runtime

\`\`\`text
HA WebSocket state_changed / supported subscriptions
  -> HA driver
  -> TagValue
  -> cache/event pipeline
\`\`\`

## Writes

Use supported HA action/service API, then wait for authoritative HA state where applicable.

## Server Memory

Useful for EliteSCADA automation state, not as a mirror of HA state.

## Scripts

Server Scripts can combine HA TAGs with native industrial TAGs naturally.

Example:
\`\`\`text
HA occupancy -> Script -> Modbus setpoint / DALI light
\`\`\`

No HA-specific scripting API is required.

---

# 22. Matter Controller

## Data Source

One Matter controller/fabric integration instance:
- sidecar endpoint/schema version;
- protected fabric material reference/state;
- network commissioning policy.

## Equipment

One Matter node generally -> one Equipment, with endpoint/device-type capability channels.

## TAG binding

Stable context:
- fabric/controller;
- node;
- endpoint;
- cluster;
- attribute/feature.

## Runtime values

Matter subscriptions become canonical TAG updates.

## Writes

Writable attributes/stateful commands use normal writable TAG path and authoritative subscription/readback.

## Rich commands

Some Matter InvokeCommand operations do not map naturally to a scalar state TAG.

First release should keep scope to cleanly mapped Capabilities. Rich actions may require \`RICH-COMMAND-BINDING-01\`.

## Commissioning

PASE/CASE/fabric changes are protected Engineering operations, never ordinary Runtime TAG writes.

---

# 23. ESPHome Native

## Data Source

With the current runtime model, recommend one Data Source per ESPHome device:
- host;
- port;
- encryption secret reference;
- API version/device identity.

Discovery UX can create these automatically.

## Equipment

One ESPHome device -> one Equipment.

## TAG binding

One ESPHome entity key/type per TAG.

Examples:
- switch;
- light;
- sensor;
- binary sensor;
- cover;
- climate.

## Runtime

\`\`\`text
Native API subscription
  -> ESPHome driver
  -> canonical TAG cache
\`\`\`

## Writes

Entity command -> wait for state update when state exists.

## Server Memory

Only for cross-device/internal automation state.

## Client Memory

May control local UI around ESPHome values; never device truth.

---

# 24. Shelly RPC

## Data Source

Recommend one Data Source per Shelly physical device:
- host;
- device identity/MAC;
- credential secret reference;
- firmware expectations.

## Equipment

One physical Shelly device -> one Equipment.

## TAG binding

Component + property:
- \`switch:0/output\`;
- power;
- energy;
- input;
- temperature;
- cover position.

Exact grammar remains driver-versioned.

## Runtime

Initial full status + WebSocket partial notifications update canonical TAGs.

## Writes

RPC set -> authoritative status notification/readback -> cache.

## Maintenance

Reboot/factory reset/configuration is not a normal TAG command.

## Events

Input edge events require transient-event semantics if no durable state represents them.

---

# 25. Z-Wave JS Bridge

## Data Source

One Data Source per controller/network:
- sidecar;
- F4 ZWaveController;
- security/NVM protected state.

## Equipment

One node normally -> one Equipment.

## TAG binding

Use durable Z-Wave value identity:
- controller/home context;
- node;
- endpoint;
- Command Class;
- property/propertyKey or equivalent stack-stable value identity.

## Runtime

Z-Wave JS value notifications become canonical TAG updates.

## Writes

Writable value -> Z-Wave JS -> node -> resulting value update -> cache.

## Server Memory

Use for automation state only.

## Events

Central Scene/button presses are prime examples needing a canonical transient event path.

## Engineering maintenance

Inclusion/exclusion/heal/NVM restore do not become TAG writes.

---

# 26. Data Source + Equipment + TAG creation examples

## Example A — Zigbee smart plug

\`\`\`text
DataSource
  Zigbee Home
  driver=zigbee.coordinator
  coordinator=<F4 resource ref>

Equipment
  Kitchen Plug

TAGs
  Kitchen.Plug.On
    DataSourceId -> Zigbee Home
    Capability -> OnOff
    writable=true

  Kitchen.Plug.Power
    DataSourceId -> Zigbee Home
    Capability -> PowerMeter
    writable=false

  Kitchen.Plug.Energy
    DataSourceId -> Zigbee Home
    Capability -> EnergyMeter
    writable=false
\`\`\`

No Memory TAG is required.

## Example B — Shelly device + internal retained automation

\`\`\`text
DataSource
  Shelly Pump Relay

Equipment
  Pump Relay

TAG
  Pump.Relay.Output        <- Shelly-owned writable TAG
  Pump.Relay.Power         <- Shelly-owned read-only TAG

Server Memory DataSource
  Building Automation State

TAG
  Automation.Pump.Enabled  <- shared retained internal state
\`\`\`

Server Script:

\`\`\`text
read Power + Automation.Enabled
  -> if policy says OFF
  -> write Pump.Relay.Output
\`\`\`

## Example C — HA bridge driving industrial TAG through Gateway

\`\`\`text
HA Occupancy TAG
   -> Gateway OnChange
   -> Server Memory or writable industrial destination
\`\`\`

No HA-specific industrial bridge API.

---

# 27. What should be stored where

| Data | Canonical place |
|---|---|
| protocol endpoint/host/port | Data Source settings |
| password/token/key reference | Data Source SecretReferences / protected material |
| physical controller resource | F4 Host Hardware reference |
| protocol stable point identity | TAG CommunicationBinding |
| current device value | CurrentTagCache |
| runtime quality | TagValue |
| source/server timestamps | TagValue |
| retained internal automation variable | Server Memory |
| per-browser UI variable | Client Memory |
| user-facing physical device | Equipment |
| semantic behavior | Capability mapping |
| fixed operational action | Command -> target TAG |
| simple cross-TAG transfer | TAG Gateway |
| complex deterministic automation | Server Script |
| process history | Historian subscriber |
| process alarm | Alarm Engine |
| browser live value | realtime subscriber |
| commissioning/network topology mutation | protected Engineering operation |
| controller/network secrets/backups | protected host material/state |

---

# 28. Anti-patterns to prohibit

## 28.1 Driver writes directly to Historian

Wrong.

The driver publishes TAG state; Historian subscribes.

## 28.2 Driver calls Alarm Engine

Wrong.

The driver publishes truthful value/quality; Alarm Engine evaluates canonical alarm definitions.

## 28.3 Driver calls React/HMI

Wrong.

Realtime subscription publishes canonical TAG changes.

## 28.4 Driver stores its process values in Server Memory

Wrong for ordinary device state.

Server Memory is a distinct internal source, not a cache layer for drivers.

## 28.5 Script receives Driver object

Wrong.

Script reads/writes declared canonical TAG dependencies.

## 28.6 Protocol-to-protocol direct bridge

Wrong.

Use TAG Gateway or Script.

## 28.7 Client Memory as process authority

Wrong.

Client Memory is UI/session-local.

## 28.8 Friendly names as protocol identity

Wrong.

Persist stable protocol identity and treat friendly names as presentation.

## 28.9 Commissioning through normal Runtime TAG writes

Wrong.

Pair/include/address/reset is Engineering maintenance authority.

## 28.10 Optimistic frontend value as truth

Wrong.

Authoritative cache readback/event wins.

---

# 29. Recommended driver implementation template

Every future HOME/BUILDING DEV should be bootstrapped with this checklist.

## Engineering

1. define stable \`DriverType\`;
2. define \`CommunicationDriverTypeDescriptor\`;
3. declare real capabilities only;
4. define versioned Data Source schema;
5. define versioned TAG binding schema;
6. place secrets in protected references;
7. implement protected discovery/browse/reconcile as applicable;
8. produce candidates only;
9. map candidates to Equipment/Capabilities/TAGs/Commands via F6;
10. no canonical mutation before Preview/Apply.

## Runtime planner

11. resolve stable Data Source/TAG association;
12. validate binding schema;
13. create library-independent plan;
14. reject unsupported semantic mappings;
15. keep sidecar/SDK types out of the plan.

## Factory / Runtime

16. acquire F4 resource if applicable;
17. start direct transport or LocalBridge;
18. register/upsert canonical TAGs;
19. subscribe/poll protocol;
20. map values + quality + timestamps;
21. update \`ICurrentTagCache\`;
22. expose write through \`ICommunicationDriver.WriteAsync\`;
23. wait for authoritative readback where appropriate;
24. expose diagnostics/readiness;
25. stop/release resources deterministically.

## Integration acceptance

26. Historian captures configured TAG without driver-specific code;
27. Alarm reacts to the same TAG;
28. realtime HMI receives it;
29. Server Script reads and writes it;
30. TAG Gateway can route from/to it where writable;
31. Command executes through same owning-provider path;
32. API write authorization/Audit remains authoritative;
33. HA Standby does not own/process the hardware;
34. restart/sidecar reconnect preserves correct source ownership;
35. L4 proves exact model/firmware.

---

# 30. Additional contract findings

## RESEARCH_CONTRACT_DELTA_REQUIRED — MEMORY-DATASOURCE-STABLE-ID-01

Internal Memory planner still uses legacy Source-key association. Converge to stable DataSourceId authority.

## RESEARCH_CONTRACT_DELTA_REQUIRED — DRIVER-TRANSIENT-EVENT-01

Add canonical mapping/publication semantics for event-only device Capabilities such as Button/Central Scene without fake persistent TAGs.

## RESEARCH_CONTRACT_DELTA_REQUIRED — RICH-COMMAND-BINDING-01

Only if real product scope requires parameterized non-stateful process actions, extend the canonical Command/Action contract rather than bypassing TAG/provider authority.

## Existing #472 F6 requirement — composite discovery

HOME/BUILDING discovery must be able to propose:
- Location reconciliation;
- Equipment;
- Capability mappings;
- TAGs;
- Commands;
- and, for direct-IP devices, possibly a new child Data Source.

This is already a planned foundation requirement and should be closed centrally before protocol DEVs invent incompatible candidate shapes.

## Existing research deltas remain

From the main driver research:
- \`SIDECAR-LIFECYCLE-01\`;
- \`SECRET-REFERENCE-01\`;
- \`EXTERNAL-NETWORK-MUTATION-01\`;
- \`HOST-RESOURCE-ENDPOINT-01\`.

---

# 31. Recommended end-to-end architecture

\`\`\`text
                        ENGINEERING
                            |
        +-------------------+------------------+
        |                                      |
 Driver descriptor/schema              Memory source schema
        |                                      |
 Discover/Browse/Import                         |
        |                                      |
 candidate -> Preview -> Apply -> Save/Publish/Activate
                            |
                            v
                ACTIVE ENGINEERING PACKAGE
                            |
             +--------------+--------------+
             |                             |
 communication planners             memory planner
             |                             |
      runtime factories                    |
             |                             |
     ICommunicationDriver          ServerMemoryRuntimeSource
             |                             |
             +-------------+---------------+
                           |
                   ITagRegistry
                           |
                   CurrentTagCache
                           |
                    TagValueChanged
                           |
       +---------+---------+---------+-----------+-----------+
       |         |         |         |           |           |
    Alarms   Historian   Gateway  Server      Realtime   other
                                  Scripts       HMI      consumers
       |         |         |         |           |
       +---------+---------+---------+-----------+
                           |
                  canonical TAG truth

WRITE PATHS
-----------

Operator/HMI -> Authorization/Audit -> Runtime.WriteAsync -> owning provider
Client Script -> protected TAG API -> Authorization/Audit -> owning provider
Server Script -> Active revision + declared deps -> Runtime.WriteAsync -> owning provider
Gateway -> validated route -> Runtime.WriteAsync -> owning provider
Command -> target TAG -> Runtime.WriteAsync -> owning provider

LOCAL UI STATE
--------------
Client Visual Script <-> Client Memory
(no global process authority)

ENGINEERING MUTATIONS
---------------------
pair/include/commission/heal/reset/restore
    -> protected Engineering operation
    -> controller/bridge
    -> discovery/reconcile
    -> Preview/Apply when canonical project state changes
\`\`\`

---

# 32. Final research conclusion

The existing EliteSCADA core already has the correct center of gravity for HOME/BUILDING.

The new drivers do **not** need new historian, alarm, scripting, HMI or inter-driver mechanisms.

They need to converge on five existing authorities:

1. **Data Source** — provider/controller/connection instance;
2. **TAG / Command / Equipment** — canonical process/device model;
3. **CurrentTagCache / TagValueChanged** — shared runtime truth distribution;
4. **owning-provider WriteAsync** — one canonical write path;
5. **Engineering Preview/Apply + Active lifecycle** — configuration authority.

Memory has a complementary, not intermediary, role:

- **Server Memory** = shared retained internal process/application state;
- **Client Memory** = per-client presentation/session state.

The highest-value remaining foundation gaps are not protocol-specific. They are:
- stable Memory DataSource identity convergence;
- composite HOME discovery candidate materialization;
- transient event capability semantics;
- optional rich typed action/command binding;
- sidecar lifecycle;
- protected secret references;
- external-network mutation authority;
- F4 host-controller resources.

If these common contracts are closed first, Zigbee, Z2M, HA, Matter, ESPHome, Shelly, Z-Wave and DALI can all enter EliteSCADA through the same canonical Runtime instead of fragmenting the product.

## Repository evidence reviewed

GitHub live, \`wave15/corrections-integration@056224d1498b217ccb9a7cf5d13e0a2eb27d27bb\`, including:

- \`src/Scada.Drivers/Abstractions/ICommunicationDriver.cs\`
- \`src/Scada.Drivers/Abstractions/DriverEngineeringContracts.cs\`
- \`docs/ADR-009-DRIVER-SDK-ENGINEERING-BOUNDARIES.md\`
- \`src/Scada.DriverHost/Engineering/CommunicationDriverRuntimePlanning.cs\`
- \`src/Scada.DriverHost/Engineering/CommunicationDriverRuntimeComposition.cs\`
- \`src/Scada.DriverHost/Engineering/EngineeringDataSourceTypeCatalog.cs\`
- \`src/Scada.DriverHost/Engineering/EngineeringDriverCompiler.cs\`
- \`src/Scada.DriverHost/Engineering/EngineeringTagDataSourceAssociation.cs\`
- \`src/Scada.Engineering/Contracts/EngineeringContracts.cs\`
- \`src/Scada.Core/Tags/CommunicationTagBinding.cs\`
- \`src/Scada.Core/Tags/TagDefinition.cs\`
- \`src/Scada.Core/Tags/CurrentTagCache.cs\`
- \`src/Scada.Core/Events/TagValueChanged.cs\`
- \`src/Scada.DriverHost/Runtime/EngineeringRuntimeCoordinator.cs\`
- \`src/Scada.DriverHost/Runtime/RuntimeEventGate.cs\`
- \`src/Scada.DriverHost/Runtime/GatewayRuntimeEngine.cs\`
- \`src/Scada.Core/Sources/SourceProviderContracts.cs\`
- \`src/Scada.Core/InternalMemory/ServerMemorySourceProvider.cs\`
- \`src/Scada.Core/InternalMemory/ClientMemorySourceProvider.cs\`
- \`src/Scada.DriverHost/Engineering/InternalMemoryRuntimePlanner.cs\`
- \`src/Scada.DriverHost/Runtime/ServerMemoryRuntimeSource.cs\`
- \`src/Scada.Core/Sources/ServerAuthoritativeSamplePublisher.cs\`
- \`docs/INTERNAL-MEMORY-TAGS.md\`
- \`docs/ADR-003-HISTORIAN-AND-ALARMS.md\`
- \`src/Scada.Core/Alarms/InMemoryAlarmEngine.cs\`
- \`src/Scada.Historian.TimescaleDb/TimescaleDbHistorian.cs\`
- \`src/Scada.Api/Realtime/TagRealtimeHub.cs\`
- \`src/Scada.Api/Runtime/ScadaRuntimeFacade.cs\`
- \`src/Scada.Api/Runtime/ServerScriptRuntimeManager.cs\`
- \`src/Scada.Api/Runtime/IsolatedPythonScriptHandlerExecutor.cs\`
- \`docs/WAVE14-C12-SERVER-RUNTIME-AUTOMATION.md\`
- Client Visual Python capability/provider code;
- existing Native Zigbee, Z2M, DALI, HA, Matter, ESPHome, Shelly and Z-Wave research dossiers.

**NO PRODUCT CODE CHANGED.**
