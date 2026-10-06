# Zigbee2MQTT Execution Research — Checkpoint 1

Status: RESEARCH_ONLY / DOCS_ONLY / NO_PRODUCT_CODE / NO_MERGE_BY_RESEARCHER

Issue owner: #541 — HOME-RESEARCH-02 — Zigbee2MQTT + Native Zigbee execution dossier

Contract: C-HOME-ZIGBEE-RESEARCH-01

Order: HOME-ZIGBEE-Z2M-NATIVE-RESEARCH-01

Research branch: research/home-zigbee-execution

Release base revalidated before work: wave15/corrections-integration@69d5248e462ef12c41d99c6859a3afb77c6e3e2d

Access/revalidation date: 2026-10-06

This document is CHECKPOINT 1 only. It does not authorize Native Zigbee research implementation, product code, dependency changes, CI changes, packaging changes, or a merge.

---

## 1. Executive decision

### Preliminary disposition

**ZIGBEE2MQTT: GO_WITH_GATES**

Preferred first architecture:

EliteSCADA
-> MQTT broker
-> user-managed Zigbee2MQTT
-> Zigbee coordinator
-> Zigbee mesh

The first EliteSCADA Zigbee2MQTT implementation should treat Zigbee2MQTT as an external bridge owned and operated by the user. EliteSCADA should integrate through the documented MQTT contract and should not embed, copy, or redistribute Zigbee2MQTT in the first release.

Research classification:

- USER_MANAGED_OK
- BUNDLED_LEGAL_REVIEW_REQUIRED

USER_MANAGED_OK is an architecture/research classification, not a legal opinion. It means the proposed v1 boundary does not require EliteSCADA to redistribute Zigbee2MQTT: the user operates a separate GPL-3.0 Zigbee2MQTT instance and EliteSCADA communicates with it through MQTT.

Any future installer, Docker image, appliance image, managed sidecar, or other distribution that includes Zigbee2MQTT must be separately reviewed before release.

### Current upstream baseline

Latest official Zigbee2MQTT release observed on 2026-10-06:

- version: 2.14.2
- release date shown by the official GitHub release page: 2026-10-01
- release commit shown by GitHub: 5c0c1c6
- license: GPL-3.0

The implementation phase must pin and revalidate the exact Zigbee2MQTT version used for qualification. Do not silently follow latest.

---

## 2. Architecture boundary

### 2.1 Data Source

Recommended canonical model:

- one EliteSCADA Data Source = one configured Zigbee2MQTT instance / one logical Zigbee mesh connection;
- the Data Source owns the MQTT endpoint, base topic, authentication/trust references, availability/bridge diagnostics, and device inventory synchronization;
- it does not own the Zigbee radio, coordinator firmware, network key, pairing lifecycle, or coordinator backup in v1.

The Zigbee2MQTT instance remains the authority for the Zigbee network.

### 2.2 Equipment

Recommended model:

- one Equipment = one physical Zigbee device selected by the user for import;
- coordinator is infrastructure/diagnostic metadata, not normal process Equipment;
- groups are not physical Equipment and should not be auto-materialized as devices in v1.

### 2.3 TAG / Command / Capability

Recommended model:

- stateful readable exposes become candidate TAG bindings;
- writable stateful exposes can also support canonical write/Command semantics;
- write-only exposes should be Commands, not fake persistent state;
- configuration exposes remain configuration/admin metadata unless a product contract explicitly promotes them;
- diagnostic exposes remain diagnostics;
- transient event exposes remain outside persistent TAG state.

No Capability should be inferred from display labels alone.

---

## 3. MQTT contract

Zigbee2MQTT uses a configurable MQTT base topic, defaulting to zigbee2mqtt.

### 3.1 Device state

Device state is published at:

zigbee2mqtt/FRIENDLY_NAME

Payload is JSON and varies by device definition/exposes.

FRIENDLY_NAME is the IEEE address when no friendly name is configured, otherwise the configured friendly name. Therefore the MQTT topic name is routing metadata, not stable physical identity.

### 3.2 Set

Writes are published to:

zigbee2mqtt/FRIENDLY_NAME/set

Payload is JSON.

The exact writable properties must come from exposes/access metadata or a curated supported-device binding. Do not send arbitrary properties solely because they look familiar.

### 3.3 Get

Readable-on-demand properties can be requested at:

zigbee2mqtt/FRIENDLY_NAME/get

Whether a property supports get is described by exposes access rights.

A sleeping/passive device may expose state that cannot be actively read.

### 3.4 Bridge state

zigbee2mqtt/bridge/state is retained.

Documented values include:

- online at bridge startup;
- offline immediately before normal bridge stop.

EliteSCADA should treat bridge state as bridge connectivity evidence, not as device process quality.

### 3.5 Bridge inventory

zigbee2mqtt/bridge/devices is retained and contains device inventory including:

- ieee_address;
- friendly_name;
- network_address;
- supported;
- disabled;
- endpoints;
- definition;
- definition.exposes;
- power source;
- model identifiers;
- interview state.

For unsupported devices, definition can be null.

This topic is the preferred source for selected discovery/import.

### 3.6 Bridge information

zigbee2mqtt/bridge/info exposes bridge metadata including version, coordinator information, network metadata, MQTT information, permit-join state and stack/converter versions.

This should be used for diagnostics and compatibility evidence, not as process TAG data.

### 3.7 Bridge events

zigbee2mqtt/bridge/event publishes lifecycle events such as:

- device_joined;
- device_interview;
- device_leave;
- device_announce.

These are management/lifecycle events, not persistent process values.

### 3.8 Bridge request/response correlation

The documented bridge request API supports a transaction property on requests and echoes it in responses.

Where EliteSCADA uses bridge management requests in future admin/commissioning flows, it should always use transaction correlation and bounded timeout behavior.

Normal process writes to a device are not the same contract as bridge request/response management operations.

---

## 4. Retain, QoS, cache, restart and stale-state behavior

### 4.1 Retained metadata

The following important bridge topics are documented as retained:

- bridge/state;
- bridge/devices;
- bridge/groups.

Device availability messages are also retained when availability is enabled.

These retained topics are useful for late subscribers, but retained does not mean fresh process truth.

### 4.2 Device state retain

Per-device retain is configurable and defaults to false.

Per-group retain is configurable and defaults to false.

QoS is also configurable per device/group and may be 0, 1, or 2. EliteSCADA must not hard-code a universal upstream QoS assumption.

### 4.3 Persistent state cache

Zigbee2MQTT MQTT behavior currently documents:

- cache_state default true;
- cache_state_persistent default true;
- cache_state_send_on_startup default true.

Therefore a state message observed after a Zigbee2MQTT restart can still originate from persisted cache rather than a new physical report.

This is a critical process-truth constraint.

### 4.4 force_disable_retain

Zigbee2MQTT can be configured to disable retained output globally.

For v1, reliable selected discovery depends on normal bridge inventory retention or an equivalent proven live snapshot path.

Recommended preflight:

- subscribe before declaring the Data Source ready;
- require a bounded bridge/state + bridge/devices synchronization;
- if a reliable inventory snapshot is not obtained, mark discovery unavailable rather than guessing;
- do not restart or mutate the user-managed bridge automatically just to obtain inventory.

### 4.5 Broker disconnect

On broker disconnect:

- Data Source connectivity becomes unavailable/degraded;
- no cached local value should be promoted to new Good process truth;
- pending writes must not be marked confirmed;
- on reconnect, re-establish bridge state/inventory before normal operation.

### 4.6 Bridge restart

On bridge restart:

- bridge/state may transition offline -> online;
- bridge/devices is republished/retained;
- cached device state can be sent on startup;
- availability may initially mark devices offline depending on check-in behavior and persisted timeout state.

The runtime must distinguish bridge recovery from device-state freshness.

---

## 5. Availability contract

Zigbee2MQTT device availability is optional and disabled by default unless enabled globally or per device.

When enabled:

- device availability is published at zigbee2mqtt/FRIENDLY_NAME/availability;
- payload is online/offline;
- availability message is retained;
- active mains-powered devices can be pinged after missing check-ins;
- passive/battery devices use longer check-in timeout semantics and are not simply pinged like active devices;
- availability timeout state is persisted across Zigbee2MQTT restarts.

Recommended EliteSCADA v1 behavior:

1. Detect whether availability is enabled for selected/imported devices.
2. Strongly recommend or require it for normal stateful integrations where device online/offline quality is important.
3. Preserve passive-device semantics; a sleepy device is not equivalent to a failed always-on actuator.
4. Never convert Z2M availability alone into process value.
5. Use availability as one input into quality/diagnostics/freshness.

If availability is disabled, the Data Source must explicitly report reduced diagnostic confidence.

---

## 6. Stable identity

### 6.1 Physical identity

Primary physical identity:

**IEEE address**

The official security documentation describes the IEEE address as the device identity statically assigned to the Zigbee chip in normal use, while network address is assigned on join and may change.

Therefore:

- use normalized IEEE address as the primary physical device key;
- never use network_address as persistent identity;
- never use friendly_name as persistent identity.

Because upstream notes that IEEE address can be changed in some cases, EliteSCADA should treat it as the best available Zigbee physical identity, not as a metaphysical immutable identifier.

### 6.2 EliteSCADA canonical identity

Recommended composite identity:

DataSourceId
+ IEEE address
+ endpoint when relevant
+ expose/property path

This prevents collisions between different configured meshes and preserves device identity across friendly-name changes.

### 6.3 Friendly name

friendly_name is:

- display/routing metadata;
- user mutable;
- used in device MQTT topics;
- not stable identity.

The bridge API allows device rename and republishing of current inventory.

EliteSCADA should resolve the current friendly name from bridge/devices and update routing without changing canonical Equipment/TAG identity.

### 6.4 Endpoint and property identity

For multi-endpoint devices, bindings may require endpoint identity.

An expose may include endpoint and property information. Generic/composite exposes can also contain nested features.

Recommended binding identity should preserve enough upstream structure to avoid collisions:

- IEEE address;
- endpoint ID/name if present;
- expose type;
- property;
- nested feature/property path where needed.

Do not flatten two endpoint-specific properties into the same TAG identity.

---

## 7. Discovery and selected import

Required v1 flow:

connect
-> bridge synchronized
-> inventory
-> exposes
-> select devices
-> candidate
-> preview
-> apply

### 7.1 Connect

The Data Source connects to the configured broker/base topic using configured credentials/trust material.

No Zigbee pairing or permit-join operation occurs in discovery.

### 7.2 Bridge synchronization

Before showing import candidates, obtain:

- bridge/state;
- bridge/info;
- bridge/devices.

Optionally consume bridge/groups only for diagnostics/future features.

### 7.3 Candidate devices

Candidate physical devices come from bridge/devices.

Minimum candidate metadata:

- IEEE address;
- friendly name;
- model/vendor/description where definition exists;
- supported flag;
- interview state;
- power source;
- endpoints;
- exposes summary.

Unsupported devices with definition null must not be silently auto-mapped.

### 7.4 Select

The user explicitly selects which devices to import.

Do not import the whole mesh automatically.

### 7.5 Preview

Preview should show:

- Equipment identity;
- display name;
- stable IEEE identity;
- endpoint/property mappings;
- proposed Capabilities;
- TAGs;
- Commands;
- read/write/get support;
- configuration-only exposes;
- diagnostic exposes;
- transient event exposes excluded from persistent TAGs;
- unsupported/unmapped exposes;
- availability confidence.

### 7.6 Apply

Apply materializes only accepted candidates into canonical Equipment/TAG/Command/Capability structures.

Apply must not:

- enable permit join;
- rename Z2M devices;
- remove devices;
- change groups;
- change scenes;
- rewrite Z2M configuration;
- change network key;
- touch coordinator backup.

Those are separate admin/commissioning authorities.

---

## 8. Exposes model and safe mapping

Zigbee2MQTT currently documents two broad expose forms:

- generic: numeric, binary, enum, text, composite, list;
- specific: domain capabilities such as light or switch.

Important expose fields include:

- type;
- access;
- name;
- label;
- property;
- endpoint;
- category;
- features.

Specific/composite exposes can contain nested features.

### 8.1 Access bitmask

Documented access bits:

- bit 1 / value 1: property appears in published state;
- bit 2 / value 2: property can be set;
- bit 3 / value 4: property can be retrieved with get.

Examples:

- access 1 = published state only;
- access 2 = set only;
- access 5 = published + get;
- access 7 = published + set + get.

Access describes transport operations. It does not by itself define process semantics.

### 8.2 Category

Current exposes documentation distinguishes:

- category=config for configuration;
- category=diagnostic for diagnostic read-only information;
- no category for normal device use.

EliteSCADA should honor these categories.

Do not promote config/diagnostic fields into normal HMI process state solely because they are readable.

### 8.3 Capability mapping rule

**Do not infer Capability from name or label alone.**

Mapping should use a curated rule table combining:

- specific expose type where available;
- exact property/feature structure;
- access rights;
- endpoint;
- category;
- unit/range where meaningful;
- known supported upstream definition/model when ambiguity remains.

Unknown generic exposes remain unmapped/raw candidate metadata until a rule is deliberately added.

### 8.4 Preliminary canonical mappings

The following are safe mapping directions, not universal claims:

| Canonical capability | Z2M mapping direction | Required checks |
| --- | --- | --- |
| OnOff | specific switch or exact binary/state feature | normal category, state semantics, endpoint, access |
| Light | specific light expose | state feature present and normal category |
| Dimmer | light brightness feature | exact nested feature/property and numeric bounds |
| ColorLight | light color/color_temp features | exact feature structure; preserve separate color models |
| Temperature | curated temperature numeric expose | normal process category, expected unit/semantic rule |
| Humidity | curated humidity numeric expose | normal process category, unit/range rule |
| Occupancy | curated occupancy binary expose | normal process state, not action event |
| Illuminance | curated illuminance numeric expose | exact semantic/property/unit rule |
| Contact | curated contact binary expose | exact semantic rule and polarity |
| Leak | curated water_leak/leak binary expose | exact semantic rule |
| Smoke | curated smoke binary expose | exact semantic rule |
| Battery | battery numeric expose | read-only; preserve percent/voltage distinction |
| Power | power numeric expose | unit and category verified |
| Energy | energy numeric expose | cumulative semantic/unit verified |
| Voltage | voltage numeric expose | unit verified |
| Current | current numeric expose | unit verified |
| Cover | specific cover expose | state/position features and direction semantics verified |
| Lock | specific lock expose | state/write semantics verified |
| Climate | specific climate expose | feature-by-feature mapping; do not flatten configuration |
| Fan | specific fan expose | exact state/speed features verified |

Important:

- vendor-specific values stay unmapped until curated;
- config fields such as operation modes/power-on behavior remain config;
- diagnostics such as device temperature or outage counters remain diagnostic;
- topology/LQI data is diagnostic, not process Capability;
- action/button event properties are handled separately below.

---

## 9. Process truth and write/readback semantics

### 9.1 Core rule

**MQTT publish success is not process truth.**

The fact that EliteSCADA successfully publishes to an MQTT broker proves only that the client handed a message to the broker/client stack under the configured MQTT semantics.

It does not prove:

- Zigbee2MQTT processed the command;
- the coordinator transmitted it;
- the target device received it;
- the target applied it;
- the physical process now equals the requested value.

### 9.2 Requested value

A requested value must remain a pending/requested state until confirmed by an evidence path appropriate to that expose/device.

Do not immediately write the requested value into a canonical TAG as Good.

### 9.3 Confirmation levels

Recommended v1 confirmation hierarchy:

**Level A — reported state after command**

For an expose that publishes state, observe a later state publication matching the target property.

This is better than publish acknowledgement but is still converter/device dependent.

**Level B — explicit get/readback**

If the expose supports get, issue a bounded get after write when stronger verification is required and use the returned/published device state as reconciliation evidence.

**Level C — passive/report-only reconciliation**

For sleeping/passive devices or properties without get, wait for the next authoritative report. If no confirmation arrives before the configured command timeout, mark the write unconfirmed rather than Good.

**Level D — set-only command**

For access=2 or otherwise non-stateful actions, expose a Command result/timeout, not a persistent Good TAG value.

### 9.4 Cached/retained-state defense

A matching state value is not automatically fresh because:

- device state may be retained if configured;
- Zigbee2MQTT can persist and republish cached state at startup.

Recommended runtime safeguards:

- track bridge connection/restart epoch;
- track MQTT retained flag when available;
- track device availability;
- optionally use last_seen when the user enables it;
- distinguish cached bootstrap state from new post-command/post-device evidence;
- do not upgrade stale cached state to fresh Good merely because it arrived after reconnect.

### 9.5 Timeout and reconciliation

Each writable binding should define:

- requested value;
- command timestamp/correlation context;
- confirmation mode;
- timeout;
- last confirmed state;
- unconfirmed/failure outcome.

If a later authoritative report disagrees with the requested value, canonical state follows the reported device state and diagnostics record the mismatch.

---

## 10. Transient events

### 10.1 Problem

Zigbee2MQTT devices commonly expose action values such as:

- single;
- double;
- hold;
- release;
- numbered button actions;
- brightness move/step actions.

The official device documentation describes action as a triggered action such as a button click. It may appear in the published JSON state, but semantically it is an event occurrence.

Therefore a raw action property must not become a fictitious persistent process state.

### 10.2 Classification

Recommended classification:

**STATE**
- actual persistent/last-known process values such as switch state, temperature, occupancy, position, measured power.

**COMMAND**
- writable process intent or set-only operation.

**TRANSIENT_EVENT**
- action;
- button clicks;
- single/double/hold/release;
- remote-control gesture/action;
- lifecycle events such as join/interview/leave/announce;
- scene activation occurrence where represented as an event.

**DIAGNOSTIC**
- availability;
- bridge health/state;
- interview status;
- LQI/topology;
- device/bridge versions;
- configuration/diagnostic exposes.

### 10.3 First-release disposition

Until EliteSCADA has a canonical transient event path:

- do not materialize action/button events as persistent TAGs;
- show them in discovery/preview as excluded transient events;
- they may appear in diagnostics/raw event inspection;
- do not build automations that depend on fake retained action state.

Required future contract delta:

**RESEARCH_CONTRACT_DELTA_REQUIRED — DRIVER-TRANSIENT-EVENT-01**

This is important because button/remotes are a material Zigbee use case.

---

## 11. Groups and scenes

### 11.1 Groups

Zigbee2MQTT supports Zigbee groups and group control.

Important semantics:

- group inventory is available under bridge/groups;
- group membership is mutable;
- group state can use optimistic behavior;
- default group optimistic behavior is documented as true;
- group state semantics can depend on off_state policy;
- group control is broadcast-oriented and not equivalent to one physical Equipment identity.

Recommendation for first release:

**DEFER group import as canonical process Equipment.**

Reasons:

- identity is not one physical device;
- state can be aggregate/optimistic;
- membership can change outside EliteSCADA;
- readback/process-truth semantics need a separate aggregate contract.

A future release may expose curated group Commands or aggregate Equipment after defining explicit semantics.

### 11.2 Scenes

Zigbee2MQTT supports scene add/store/recall/remove/rename.

Recommendation:

- scene recall is Command-like;
- scene store/add/remove/rename are commissioning/configuration operations;
- scene is not persistent process state;
- do not create a TAG whose value pretends a scene remains continuously active.

First-release disposition:

**DEFER scenes from normal process TAG import.**

A future typed Command/action contract may safely expose scene recall.

---

## 12. Security and secret ownership

### 12.1 MQTT transport security

Current Zigbee2MQTT MQTT configuration supports:

- mqtts for TLS;
- CA certificate path;
- client certificate and private key;
- username/password;
- TLS certificate validation;
- SNI/server-name override.

The documented default for reject_unauthorized is true.

EliteSCADA should never encourage disabling certificate validation in production.

### 12.2 EliteSCADA-owned connection material

EliteSCADA needs only the material required to act as the MQTT client for the external Z2M instance:

- broker URI/host/port;
- base topic;
- username reference if used;
- password secret reference if used;
- CA/trust reference if custom;
- client certificate reference if used;
- client private-key secret reference if used.

Protected material must not be stored as plaintext in .escadapkg.

### 12.3 Z2M-owned Zigbee material

The following remain owned by the user-managed Zigbee2MQTT installation in v1:

- Zigbee network key;
- PAN/network configuration;
- coordinator backup;
- Zigbee2MQTT data directory;
- device database;
- install codes;
- coordinator firmware lifecycle;
- pairing/permit-join policy.

EliteSCADA does not need to copy the Zigbee network key in order to consume the MQTT bridge.

### 12.4 Zigbee2MQTT data directory

Official security guidance states that the Zigbee2MQTT data directory contains full configuration, network state and device data, and should be access-restricted.

EliteSCADA v1 should not mount or parse that directory.

Use MQTT as the product boundary.

### 12.5 Coordinator backup

Official adapter documentation currently states coordinator backup support is available for zStack and EmberZNet adapters.

This is an external Z2M operational concern for v1.

EliteSCADA may surface a future admin reminder/status, but should not ingest or restore the backup in the first bridge release.

---

## 13. Packaging and GPL boundary

### 13.1 Revalidated license

The Zigbee2MQTT repository LICENSE is GNU GPL version 3.

The current package metadata also declares GPL-3.0.

### 13.2 Preferred v1 boundary

**USER_MANAGED_EXTERNAL**

EliteSCADA:

- documents supported connection requirements;
- connects to an already-operated MQTT broker/Zigbee2MQTT instance;
- consumes documented MQTT messages;
- publishes documented MQTT commands;
- does not ship Zigbee2MQTT binaries/source/container image;
- does not copy GPL Zigbee2MQTT code into the proprietary product.

This is the preferred first release.

### 13.3 Bundling

Any future mode that:

- installs Zigbee2MQTT;
- embeds it in an EliteSCADA appliance image;
- distributes a container image containing it;
- redistributes modified Zigbee2MQTT;
- packages it as a managed sidecar delivered with EliteSCADA

must be classified:

**BUNDLED_LEGAL_REVIEW_REQUIRED**

No bundled conclusion is authorized by this checkpoint.

---

## 14. Diagnostics

The already accepted EliteSCADA diagnostics foundation (#500) should be reused.

Recommended Zigbee2MQTT diagnostic ladder:

1. DriverHost/API host health.
2. MQTT Data Source connection health.
3. Broker reachability/TLS/auth outcome.
4. Zigbee2MQTT bridge state/health.
5. Device availability/interview/support status.
6. Point-level read/get capability where supported.
7. Live canonical TAG value/quality/freshness.

Useful protocol details:

- Z2M version;
- bridge state;
- broker connection;
- coordinator type/IEEE from bridge info;
- network channel/PAN metadata where safe;
- device IEEE/friendly name;
- interview state;
- supported flag;
- availability;
- last_seen if enabled;
- read/write timeout counters;
- unmapped expose count.

Do not use LQI/topology as process Capability.

---

## 15. Known blockers and gates

### Gate 1 — transient event contract

Button/remotes/actions are important Zigbee use cases but must not be represented as fake persistent TAG state.

Required:

**DRIVER-TRANSIENT-EVENT-01**

Until it lands, action/button/gesture features are excluded from normal v1 process import.

### Gate 2 — protected MQTT credentials

Before implementation, confirm the current HOME/common protected-material path can store/reference:

- MQTT password;
- client private key;
- client certificate where sensitive handling requires it.

If a common protected-material contract is not yet product-ready, this remains an implementation gate.

### Gate 3 — retain/inventory preflight

Reliable selected discovery needs a deterministic bridge inventory snapshot.

The implementation must prove behavior when:

- retained messages are available;
- retained messages are disabled/broken;
- broker reconnect occurs;
- bridge restarts;
- stale cached state is replayed.

### Gate 4 — process-truth qualification

Write confirmation must be tested across representative classes:

- mains-powered switch/light with get;
- report-only sensor;
- sleepy device;
- set-only command;
- device offline during write;
- broker disconnect during write;
- bridge restart with cached state.

### Gate 5 — device compatibility claims

Zigbee2MQTT supporting a model does not automatically mean EliteSCADA has qualified that model.

Each future supported-device claim must pin:

- device manufacturer/model;
- firmware where available;
- Zigbee2MQTT version;
- converter version;
- coordinator family;
- tested capabilities;
- read/write/reporting/reconnect/restart evidence;
- test date.

---

## 16. V1 inclusion/defer table

| Area | V1 disposition |
| --- | --- |
| User-managed external Z2M | INCLUDE |
| MQTT TLS/auth connection | INCLUDE |
| Selected device import | INCLUDE |
| IEEE-based stable identity | INCLUDE |
| bridge/devices + exposes parsing | INCLUDE |
| Normal state TAGs | INCLUDE |
| Stateful writes with confirmation | INCLUDE WITH GATES |
| Explicit get/readback | INCLUDE WHEN EXPOSE SUPPORTS GET |
| Availability diagnostics | INCLUDE / STRONGLY REQUIRED |
| Config exposes | ADMIN/CONFIG ONLY |
| Diagnostic exposes | DIAGNOSTICS ONLY |
| action/button/remote events | DEFER FROM PERSISTENT TAGS |
| Groups as Equipment | DEFER |
| Scene state | REJECT AS PERSISTENT STATE |
| Scene recall | FUTURE COMMAND |
| Scene/group mutation | ADMIN/FUTURE |
| Pair/permit join/remove | OUTSIDE NORMAL RUNTIME |
| Network-key management | Z2M-OWNED |
| Coordinator backup/restore | Z2M-OWNED IN V1 |
| Bundled/managed Z2M | LEGAL_REVIEW_REQUIRED / DEFER |

---

## 17. Preliminary implementation contract

A future Zigbee2MQTT bridge DEV should be able to start from this minimum contract.

### Data Source settings

Non-secret:

- stable DataSourceId;
- broker URI;
- base topic;
- TLS enabled/implicit via URI;
- availability expectations;
- bounded operation timeouts.

Secret/trust references:

- MQTT password;
- client private key;
- optional client certificate;
- CA/trust material as required by platform conventions.

### Runtime binding

Minimum binding:

- DataSourceId;
- IEEE address;
- endpoint optional;
- expose/property path;
- semantic mapping rule ID;
- current friendly name resolved dynamically for routing.

### Import

Read-only until Apply:

- bridge inventory;
- exposes;
- candidate mapping;
- preview.

No commissioning side effects.

### Runtime read

Subscribe to current device topic and availability.

Map only curated state properties.

### Runtime write

- resolve current friendly name from IEEE inventory mapping;
- validate expose access/type;
- publish set;
- enter pending confirmation;
- reconcile from state/get as supported;
- timeout as unconfirmed/failure;
- never set Good only because MQTT publish returned success.

---

## 18. Preliminary GO_WITH_GATES rationale

Reasons to proceed:

- current upstream release is active and maintained;
- official MQTT contract is mature and documented;
- stable physical identity is available through IEEE address;
- bridge inventory exposes device definitions and exposes metadata;
- TLS/auth are supported by the MQTT connection;
- selected-import architecture cleanly matches EliteSCADA Equipment/TAG/Command projection;
- external user-managed deployment avoids making Z2M lifecycle a product responsibility in v1.

Reasons not to mark unconditional GO:

- transient button/action event path is not canonical yet;
- write/readback truth varies by expose/device;
- cached/retained state can be stale across restart;
- availability is optional;
- groups/scenes have aggregate/command semantics that should not be flattened;
- device compatibility is model/firmware/coordinator/version-specific;
- bundling GPL-3.0 software requires separate legal review.

---

## 19. Official sources

All sources below were accessed/revalidated on 2026-10-06.

| Source | Version/tag/date observed | Fact supported |
| --- | --- | --- |
| https://github.com/Koenkk/zigbee2mqtt/releases | latest 2.14.2; released 2026-10-01 | current release baseline |
| https://github.com/Koenkk/zigbee2mqtt/blob/master/LICENSE | current master | GPL-3.0 license text |
| https://github.com/Koenkk/zigbee2mqtt/blob/master/package.json | current master | package license GPL-3.0 |
| https://www.zigbee2mqtt.io/guide/usage/mqtt_topics_and_messages.html | current docs | device state/set/get; bridge state/devices/info/events; rename; transaction correlation |
| https://www.zigbee2mqtt.io/guide/usage/exposes.html | current docs | expose structure, access bitmask, categories |
| https://www.zigbee2mqtt.io/guide/configuration/mqtt.html | current docs | broker/TLS/auth; cache-state behavior; MQTT version/retain settings |
| https://www.zigbee2mqtt.io/guide/configuration/device-availability.html | current docs | online/offline, retained availability, active/passive behavior, restart persistence |
| https://www.zigbee2mqtt.io/guide/configuration/devices-groups.html | current docs | IEEE-keyed device config, retain/QoS/retention, group options |
| https://www.zigbee2mqtt.io/guide/usage/groups.html | current docs | group membership/control and optimistic state behavior |
| https://www.zigbee2mqtt.io/guide/usage/scenes.html | current docs | scene add/store/recall/remove/rename semantics |
| https://www.zigbee2mqtt.io/advanced/zigbee/03_secure_network.html | current docs | Z2M security model, secrets, network key, permit join |
| https://www.zigbee2mqtt.io/guide/configuration/zigbee-network.html | current docs | network key/PAN/channel configuration |
| https://www.zigbee2mqtt.io/guide/adapters/ | current docs | coordinator backup support statement |

---

## 20. Checkpoint 1 conclusion

Preliminary decision:

**ZIGBEE2MQTT = GO_WITH_GATES**

Packaging boundary:

**USER_MANAGED_EXTERNAL**

License classification:

**GPL-3.0**

Bundling classification:

**BUNDLED_LEGAL_REVIEW_REQUIRED**

Stable identity:

**DataSourceId + IEEE address**, extended by endpoint/property path for child bindings.

Import model:

**connect -> inventory -> exposes -> select -> candidate -> preview -> apply**

Process-truth rule:

**MQTT publish != process truth.**

Transient event rule:

**action/button/remote events are TRANSIENT_EVENT, not persistent TAG state.**

Required contract delta:

**RESEARCH_CONTRACT_DELTA_REQUIRED — DRIVER-TRANSIENT-EVENT-01**

Scope confirmation:

DOCS_ONLY

NO PRODUCT CODE CHANGED

NO DEPENDENCY CHANGED

NO CI CHANGED

NO MERGE PERFORMED
