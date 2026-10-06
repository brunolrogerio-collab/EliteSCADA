# Z-Wave JS Readiness Research

Research date: **2026-10-06**  
Issue owner: **#542 — HOME-RESEARCH-03 — Matter + Z-Wave JS readiness and dependency gates**  
Parent: **#472 — HOME/BUILDING Integration Foundation**  
Historical input only: **#475 — HOME/BUILDING DRIVER RESEARCH**  
Contract: **C-HOME-MATTER-ZWAVE-RESEARCH-01**  
Order: **HOME-MATTER-ZWAVE-READINESS-RESEARCH-01**  
Branch: **research/home-matter-zwave-readiness**  
Release base revalidated before Checkpoint 2: **wave15/corrections-integration@69d5248e462ef12c41d99c6859a3afb77c6e3e2d**  
Scope: **RESEARCH_ONLY / DOCS_ONLY / NO_PRODUCT_CODE / NO_MERGE_BY_RESEARCHER**

## Checkpoint 2 decision

**Z-WAVE JS = GO_WITH_GATES**

The Z-Wave JS ecosystem is mature enough for a bounded future EliteSCADA implementation lane.

Recommended architecture:

**EliteSCADA -> managed Z-Wave JS Server -> ZWaveController Host Resource -> Z-Wave network**

Do not write a native Z-Wave protocol stack in EliteSCADA.

The historical 2026-10-02 dossier classified the path as WAIT_DEPENDENCY because Host Resource, LocalBridge lifecycle and protected-material foundations were not yet sufficiently established. Current GitHub live state changes that assessment:

- the generic Protected Material Authority from #497 is integrated and is an ancestor of the current release base;
- the generic host-owned serial work from #469 is also an ancestor of the current release base;
- Z-Wave JS core and Z-Wave JS Server remain active/current;
- modern 800-series controllers, S2, SmartStart, Long Range and NVM tooling are supported.

The remaining blockers are bounded product gates, not a reason to keep the entire integration in an undifferentiated WAIT state.

This is not unrestricted GO because production still requires:

1. a first-class **ZWaveController** Host Resource, not just a SerialPort string;
2. exclusive controller lease/ownership and deterministic handover;
3. managed sidecar lifecycle and an authenticated/local-only control plane;
4. secure injection of S0/S2/LR network keys through the integrated Protected Material Authority;
5. exact-controller NVM backup/restore/migration proof;
6. a current open upstream Long Range / 16-bit Node ID soft-reset issue associated with NVM backup;
7. truthful sleeping-node / queued-write semantics;
8. transient-event handling for Central Scene and similar stateless value notifications;
9. RF-region-specific hardware qualification;
10. L0-L4 evidence on exact server/core/controller firmware versions.

Therefore:

- **stack maturity: GO**;
- **EliteSCADA implementation readiness: GO_WITH_GATES**;
- **production readiness: NOT YET GO**.

---

# 1. Current stack revalidation

## 1.1 Z-Wave JS core

Current observed stable release:

- project: **Z-Wave JS**
- package: **zwave-js**
- version: **15.31.0**
- release date: **2026-09-28**
- repository: https://github.com/zwave-js/zwave-js
- license: **MIT**
- Node.js engine in current package: **>= 20**

The current driver remains actively maintained.

Recent release evidence relevant to EliteSCADA includes:

- 15.31.0 — current stable release;
- 15.29.0 — redundant/superseded command deduplication and persisted lastAwake;
- 15.25.0 — granular interview progress event;
- 15.24.3 — explicit Driver_Failed behavior when the serial port cannot reopen during recovery.

The core driver currently supports the controller generations needed for a future product path:

- 500 series — legacy/migration qualification;
- 700 series — supported;
- 800 series — preferred new-controller family;
- Z-Wave Long Range — supported in current driver line.

### License

The exact v15.31.0 package manifest reports MIT.

The repository LICENSE is the MIT license.

Commercial redistribution is permissive subject to the license terms and notices.

Certification/trademark is a separate question and is deferred for the common legal checkpoint.

---

## 1.2 Z-Wave JS Server

Current observed stable release:

- package: **@zwave-js/server**
- version: **3.10.1**
- release date: **2026-08-07**
- repository: https://github.com/zwave-js/zwave-js-server
- license: **Apache-2.0**
- Node.js engine: **>= 20**
- peer dependency: **zwave-js ^15.25.0**

Current core 15.31.0 is within the declared peer range of Server 3.10.1.

### WebSocket schema

Z-Wave JS Server 3.10.0:

- forwarded granular interview-progress events;
- bumped the API schema to **50**.

3.10.1 kept that 3.10.x line and introduced event-loop forwarding improvements.

The server handshake includes:

- driverVersion;
- serverVersion;
- Home ID;
- minimum schema version;
- maximum schema version.

The client selects a supported schema.

This is a suitable process boundary for EliteSCADA.

### Server responsibility

The server exposes the Z-Wave JS driver through WebSocket and provides access to:

- current driver/controller/node state;
- node values and metadata;
- controller methods;
- node methods;
- endpoint methods;
- controller events;
- node events;
- driver events;
- inclusion/exclusion;
- SmartStart provisioning;
- NVM backup/restore;
- health/route tools;
- schema compatibility.

This is substantially preferable to reproducing the Z-Wave protocol implementation inside .NET.

---

# 2. Current server security boundary

## 2.1 Upstream server does not authenticate clients

The current upstream README is explicit:

- Z-Wave JS Server does **not** handle authentication;
- all WebSocket API clients are allowed;
- upstream suggests authentication middleware/proxy if network exposure is desired.

The default development server bind is ws://0.0.0.0:3000.

This is not an acceptable EliteSCADA production default.

## 2.2 Required EliteSCADA control-plane posture

For a managed local sidecar:

- bind explicitly to loopback only;
- prefer 127.0.0.1 or an isolated local container network;
- firewall the port from untrusted interfaces;
- never rely on Z-Wave S2 security to protect the local WebSocket API.

If Main later allows a remote Z-Wave JS Server:

- WebSocket must be carried over an authenticated and encrypted channel;
- use a reverse proxy/tunnel with TLS and strong service authentication;
- authorize the EliteSCADA server, not arbitrary browser users;
- do not expose raw Z-Wave JS Server directly to the LAN/Internet.

The browser must never connect directly to Z-Wave JS Server.

### Security finding

**RESEARCH_FINDING / MAIN_DECISION_REQUIRED — ZWAVE-SIDECAR-CONTROL-PLANE**

Recommendation:

- v1 supports **managed local sidecar only**;
- remote/user-managed server support is deferred until an authenticated transport contract is explicitly designed.

Risk:

- an unauthenticated client can invoke privileged controller/network operations.

---

# 3. Recommended architecture

**EliteSCADA**
-> **managed Z-Wave JS Server**
-> **leased ZWaveController Host Resource**
-> **Z-Wave Classic / Long Range network**
-> **nodes/endpoints/values**

Z-Wave JS already owns the difficult protocol surface:

- Serial API;
- Command Classes;
- interviews;
- security bootstrap;
- SmartStart;
- value metadata;
- device configuration database;
- sleeping-node behavior;
- routes and health;
- firmware semantics;
- NVM conversion/tooling;
- 500/700/800 controller compatibility;
- Long Range.

EliteSCADA should own:

- lifecycle;
- controller resource authority;
- secret authority;
- canonical Equipment/TAG/Command/Capability projection;
- Engineering/admin authorization;
- diagnostics;
- backup policy;
- Runtime truth.

Directly embedding the Node library is not preferred for first release because it couples product code to Node driver internals and does not remove the Node runtime/process boundary.

A native Z-Wave implementation is rejected for first product path.

---

# 4. EliteSCADA foundation revalidation

## 4.1 Protected Material Authority is already integrated

Issue #497 delivered the generic host-owned Protected Material Authority.

Main integration checkpoint:

**ad3b53934f7d095bda502597a1d4a59d25d436d0**

GitHub live ancestry revalidation:

- that commit is an ancestor of release base 69d5248e462ef12c41d99c6859a3afb77c6e3e2d;
- release base is 87 commits ahead / 0 behind the integrated protected-material checkpoint.

The integrated contract includes:

- IProtectedMaterialAuthority;
- scoped store/resolve/replace/delete;
- opaque references;
- AES-256-GCM host storage;
- server-side-only resolution;
- Windows/Linux/container behavior;
- no plaintext project/package export.

Therefore protected-material storage itself is **not** a reason to keep Z-Wave in WAIT.

The Z-Wave implementation must consume this authority rather than inventing another secret store.

## 4.2 Generic host serial is already in the current base

#469 delivered:

- generic host-owned serial abstraction;
- server-visible port catalog/selection;
- Windows/Linux;
- resource ownership;
- offline authoring;
- shared serial arbitration for Modbus.

Exact #469 handoff commit:

**edab8a99b5058b6c971f3ffa07e1c77cd73772a0**

GitHub live ancestry revalidation:

- that commit is an ancestor of release base 69d5248e462ef12c41d99c6859a3afb77c6e3e2d;
- release base is 611 commits ahead / 0 behind it.

Therefore a generic host serial baseline exists.

A Z-Wave controller still requires a higher-level specialized resource.

## 4.3 ZWaveController is not a plain SerialPort

A Z-Wave controller has durable network identity/state that a generic serial port does not:

- Home ID / network identity;
- controller node ID;
- controller generation/family;
- controller firmware / SDK;
- RF region;
- Serial API capabilities;
- Long Range support;
- Node ID width behavior;
- NVM;
- security/network recovery implications;
- replacement/migration procedure.

Therefore:

**RESEARCH_CONTRACT_DELTA_REQUIRED — HOST-RESOURCE-GENERALIZATION-01**

Future resource:

**ZWaveController**

Do not model the user-facing integration as selecting a COM path and assuming it will always be the same controller.

---

# 5. Future ZWaveController Host Resource contract

## Stable configured identity

Persist a stable EliteSCADA Host Resource ID.

The configured identity must not be only:

- COM3;
- /dev/ttyUSB0;
- /dev/ttyACM0.

Those are transient OS endpoints.

Prefer a host resource record capable of remembering:

- resource ID;
- expected USB VID/PID where available;
- USB serial number where reliable;
- stable Linux /dev/serial/by-id path where available;
- configured controller family/model;
- expected RF region;
- expected network/Home ID after first successful binding;
- expected controller firmware/SDK;
- optional user label.

## Transient discovery

Host discovery may expose:

- OS device path;
- USB descriptors;
- serial number;
- manufacturer/product label;
- current presence;
- possible known Z-Wave controller match.

Discovery is not canonical project truth.

A configured resource may remain valid while hardware is offline.

## Exclusive lease

Exactly one effective sidecar/Runtime authority may own the physical controller.

Lease must cover:

- serial endpoint;
- controller state directory;
- sidecar process;
- NVM admin operations.

Contention must fail deterministically.

Do not let multiple sidecars or external software open the same controller simultaneously.

## USB / serial endpoint

V1 preferred:

- directly attached USB controller;
- host-owned serial path;
- Windows COM support;
- Linux stable device path.

Z-Wave JS core also supports raw serial-over-TCP style endpoints.

This can be a future advanced topology.

Do not make unauthenticated raw serial-over-TCP a normal product path.

## Windows

The Z-Wave JS stack does not have the Windows blocker found in current Matter Server research.

Current documentation/examples support Windows serial endpoints.

Future qualification still needs:

- Node runtime packaging;
- serial driver/device access;
- Windows Service lifecycle;
- stable device identity;
- controller reconnect after service restart.

## Linux

Preferred production path includes:

- /dev/serial/by-id where available;
- service user permissions;
- udev/device ACL;
- Node runtime;
- persistent sidecar state;
- loopback WebSocket.

## Container

Container deployment requires:

- explicit USB/device passthrough;
- stable mapped device identity;
- persistent Z-Wave JS state/cache;
- Protected Material Authority availability outside disposable container layer;
- host resource lease outside browser/client context.

Do not require privileged container mode merely as a shortcut.

## Controller family / firmware

Persist/diagnose:

- 500 / 700 / 800 family;
- controller model;
- firmware;
- SDK;
- Serial API version/capabilities;
- RF region;
- Long Range capability;
- current Node ID type;
- Home ID.

Use these fields in backup compatibility validation.

## Replacement

Replacement workflow:

1. stop existing owner;
2. acquire replacement controller;
3. verify model/firmware/region;
4. restore/migrate compatible NVM;
5. restore host security keys/state;
6. validate Home ID/node inventory;
7. update resource binding;
8. release old resource only after successful cutover.

---

# 6. Sidecar lifecycle

A future Z-Wave managed-sidecar path needs the same common lifecycle family identified in Matter research.

**RESEARCH_CONTRACT_DELTA_REQUIRED — SIDECAR-LIFECYCLE-01**

## Version pinning

Persist/diagnose:

- Z-Wave JS Server version;
- Z-Wave JS core version;
- WebSocket schema selected;
- Node runtime version;
- controller model;
- controller firmware/SDK;
- configuration database version where relevant.

Research baseline:

- Server 3.10.1;
- Z-Wave JS 15.31.0;
- schema 50;
- Node >=20.

Revalidate before code release.

## Startup

Conceptual order:

1. resolve configured ZWaveController Host Resource;
2. acquire exclusive lease;
3. resolve protected network keys;
4. prepare protected runtime configuration;
5. start sidecar bound to loopback;
6. open controller transport;
7. wait for driver/controller ready;
8. connect EliteSCADA WebSocket client;
9. negotiate schema;
10. start listening;
11. reconcile cached node/value inventory;
12. materialize Runtime provider state.

## Shutdown

- stop accepting new admin mutations;
- stop new writes;
- drain/abort bounded client work;
- close WebSocket;
- destroy Z-Wave JS driver/close serial;
- terminate child;
- release Host Resource lease only when process/controller is closed.

## Crash recovery

- detect child exit;
- preserve controller lease while supervised recovery is active;
- bounded exponential restart;
- identify crash loop;
- reconnect without mass refresh;
- expose sidecar vs controller vs network health separately.

## Health

At minimum:

- process alive;
- WebSocket reachable;
- schema compatible;
- driver ready;
- controller connected;
- controller Home ID expected;
- controller firmware expected;
- RF region expected;
- network ready;
- node interview progress;
- nodes ready/dead/asleep/awake;
- queued writes;
- last successful NVM backup;
- current NVM compatibility warning;
- security key configuration complete.

## Logs

Redact:

- S0 key;
- all S2 keys;
- LR S2 keys;
- DSK/PIN except bounded privileged inclusion prompt;
- SmartStart provisioning data where sensitive;
- backup contents.

Do not dump configuration objects containing keys.

---

# 7. Z-Wave security model

## S0 Legacy

Use only for devices that require it.

Do not make S0 the preferred security mode for new devices.

The S0 network key is protected material.

## S2 classes

Z-Wave JS current configuration supports:

- S2 Unauthenticated;
- S2 Authenticated;
- S2 Access Control.

These are distinct network keys/security classes.

Do not reuse one key for all classes.

## Long Range keys

Current Z-Wave JS configuration has dedicated Long Range key material for:

- S2 Authenticated;
- S2 Access Control.

Do not assume Classic network keys can be silently reused for LR.

## Minimum key set

For a controller intended to support the common security surface, prepare six independent keys:

1. S0 Legacy;
2. S2 Unauthenticated;
3. S2 Authenticated;
4. S2 Access Control;
5. LR S2 Authenticated;
6. LR S2 Access Control.

Each key:

- 16 bytes;
- unique to its class/network deployment;
- generated securely;
- stored only via Protected Material Authority;
- never serialized into project/package.

## DSK

The Device Specific Key is used during S2/SmartStart security bootstrap.

For authenticated inclusion:

- the user may need to validate the device DSK/PIN;
- only privileged Engineering/admin flow may do so;
- the full DSK/QR is not ordinary Runtime state.

A DSK is not a network key.

Treat it as privileged commissioning information.

## SmartStart

SmartStart provisioning is an administrative network mutation.

Provisioning record includes concepts such as:

- DSK;
- intended security classes;
- supported protocol preference;
- metadata.

The provisioning list is host-side persistent state.

Removing a provisioning entry is not the same as excluding an already included node.

## Security-class negotiation

Inclusion flow must distinguish:

- requested/available security classes;
- user grant;
- DSK confirmation;
- final granted class.

For Access Control devices, require deliberate S2 Access Control policy and do not silently downgrade.

## Long Range

Long Range adds:

- LR node identity width;
- LR-specific security keys;
- RF-region constraints;
- different topology characteristics.

Long Range is a separate qualification axis.

---

# 8. Secret ownership

## Protected Material Authority

Use integrated IProtectedMaterialAuthority for host-owned configuration secrets.

Recommended Z-Wave purposes:

- S0LegacyNetworkKey;
- S2UnauthenticatedNetworkKey;
- S2AuthenticatedNetworkKey;
- S2AccessControlNetworkKey;
- LRS2AuthenticatedNetworkKey;
- LRS2AccessControlNetworkKey;
- optional RemoteSidecarCredential.

Exact purpose token naming belongs to the future product contract.

## Sidecar protected persistent state

Z-Wave JS also owns non-project persistent state:

- node interview/cache database;
- provisioning list;
- device metadata cache;
- driver state required for efficient restart.

Treat this as protected operational sidecar state.

It is not interchangeable with controller NVM.

## Controller NVM

Controller NVM is physical-controller/network state.

It has separate lifecycle/backup/migration rules.

Total recoverable authority is at least:

**Protected network keys + Z-Wave JS host state + controller NVM**

Backing up only one is not a complete disaster-recovery plan.

## Never persist secrets in

- .escadapkg;
- Engineering JSON;
- browser state;
- diagnostics;
- logs;
- Git;
- ordinary audit payloads.

---

# 9. Normal Runtime vs administrative operations

## Normal Runtime

Normal process scope:

- receive value updates/reports;
- read process values;
- write canonical process setpoints/state;
- invoke approved process actions;
- expose availability;
- expose diagnostics;
- maintain canonical TAG/Equipment state.

## Administrative / network mutation

Not ordinary HMI Commands:

- inclusion;
- exclusion;
- SmartStart provision/unprovision;
- DSK/security grant;
- replace failed node;
- controller reset;
- network-key management;
- RF-region change;
- NVM restore;
- controller migration;
- association management;
- whole-network route maintenance/heal;
- node/controller firmware operations unless separately authorized.

These belong to Engineering/admin with authorization, confirmation and audit.

---

# 10. Inclusion / exclusion

## Classic inclusion

Future flow:

1. controller lease acquired;
2. sidecar/controller ready;
3. authorized user starts inclusion;
4. select intended security policy;
5. device joins;
6. if S2, grant bounded security classes;
7. validate DSK/PIN where required;
8. node ID assigned;
9. interview begins;
10. progress exposed;
11. values/capabilities previewed;
12. user assigns canonical name/location;
13. Apply creates Equipment/TAGs/Commands.

## Exclusion

Distinguish:

- remove node from Z-Wave network;
- delete/deactivate canonical EliteSCADA Equipment configuration.

Never silently combine both.

## Failed-node replacement

Replacement is admin lifecycle.

Need:

- old node identity;
- replacement status;
- security bootstrap;
- canonical Equipment continuity decision;
- audit.

## SmartStart

Provisioning flow:

1. scan/enter QR/DSK;
2. parse supported protocol/security;
3. add provisioning entry;
4. device joins when available;
5. interview;
6. candidate materialization;
7. explicit canonical Apply.

SmartStart provisioning must not silently mutate canonical Equipment.

---

# 11. NVM backup / restore / migration

This is production-critical.

## Server API

Current Z-Wave JS Server exposes controller operations for:

- raw NVM backup;
- compatibility-aware/converted restore;
- raw restore;
- progress events.

The existence of raw restore is not permission to use it casually.

## Prefer compatibility-aware restore

Z-Wave JS warns that raw restore bypasses compatibility checking and can render a controller unusable.

Product policy:

- normal EliteSCADA restore uses compatibility-aware conversion/restore;
- raw restore is not a normal product operation;
- any future raw emergency path requires explicit advanced/lab override.

## Controller generations

### 500 series

Legacy/migration only.

### 700 series

Supported.

Keep a representative 700 controller only if customer migration is strategically important.

### 800 series

Preferred new-controller generation.

Z-Wave JS 800 support tracker records:

- 800 controllers fully supported since 12.4.4;
- NVM backup/restore requires controller firmware based on Z-Wave SDK **7.19.0 or higher**.

Prefer materially newer firmware than the minimum.

## Current open NVM / Long Range blocker

GitHub live revalidated 2026-10-06:

**zwave-js/zwave-js #8833 remains OPEN**

Title:

NVM backup soft reset leaves node ID type at 8-bit, causing hours of Dropping message with invalid payload until restart

Reported condition:

- 700+ controller;
- Long Range / 16-bit node IDs;
- NVM backup;
- post-backup soft reset;
- node-ID-type synchronization failure;
- invalid-payload drops until recovery/restart.

A proposed PR:

**#8834 — Fix soft-reset NodeIDType recovery...**

was closed **without merge** on 2026-06-29.

Fresh issue evidence includes:

- recurrence on Z-Wave JS 15.29.0 with ZST39 firmware 7.24.2;
- a 2026-10-01 comment that simply awaiting the node-ID-type operation was not sufficient.

Therefore:

**GATE-ZWAVE-NVM-LR-01 = OPEN**

Until upstream resolves/revalidates this:

- do not make unattended automatic NVM backups on LR/16-bit production networks a default;
- require manual/supervised backup qualification;
- verify controller/node-ID-type health after backup;
- revalidate immediately before production LR release.

This does not block basic development or non-LR L0-L3 work.

## Closed sleeping-node queue issue

GitHub issue #9132 reported memory growth in a 144-node thermostat network caused by repeated writes accumulating on sleeping-node queues.

Current disposition:

- #9132 is CLOSED;
- PR #9136 merged 2026-09-02;
- PR #9137 merged 2026-09-02;
- Z-Wave JS 15.29.0 release notes include redundant/superseded command deduplication from those PRs.

Therefore this is **not a current 15.31.0 release blocker**.

It remains a required regression scenario.

## Backup metadata

Every EliteSCADA controller backup record should capture:

- timestamp;
- ZWaveController Resource ID;
- controller model;
- USB identity;
- controller family;
- firmware;
- SDK;
- RF region;
- Home ID;
- Node ID width / LR state;
- Z-Wave JS version;
- Server version;
- backup format/version;
- checksum;
- encrypted backup reference;
- success/failure;
- restore-compatibility status.

## Backup protection

NVM backups are sensitive operational material.

Store encrypted/protected.

Do not:

- put raw NVM in project package;
- attach raw NVM to normal diagnostics;
- log it;
- expose it to Runtime users.

## L4 restore acceptance

Must prove:

1. known-good source controller/network;
2. protected backup;
3. replacement/same controller in compatible firmware state;
4. compatibility-aware restore;
5. same Home ID/network identity;
6. node inventory preserved;
7. security keys restored separately;
8. secure nodes communicate;
9. sleeping nodes recover;
10. LR nodes recover where in scope;
11. canonical Equipment bindings still map correctly;
12. no mass re-inclusion needed.

---

# 12. RF region

Z-Wave is region-specific.

The configured ZWaveController resource must know/diagnose RF region.

Do not permit silent region mismatch.

## Brazil current reference

Silicon Labs current global-region table lists Brazil as:

- ANATEL;
- 919.8 MHz / 921.4 MHz;
- ANZ family.

Silicon Labs also warns customers to verify frequency/channel requirements for their jurisdiction.

Therefore:

**REGULATORY_REVIEW_REQUIRED** before purchasing/deploying production RF hardware.

## Controller regional SKUs

Aeotec Z-Stick 10 Pro variants include:

- ZWA060-A — US 908.42 MHz;
- ZWA060-B — AU 921.42 MHz;
- ZWA060-C — EU 868.42 MHz.

For a Brazil-based lab:

- ZWA060-B is the frequency-family candidate aligned with the current Brazil/ANZ table;
- this research does **not** establish Brazilian homologation for that exact SKU;
- verify ANATEL/product legality before RF transmission.

Do not procure US-only Zooz LR fixtures for a Brazil operational lab merely because they are convenient.

---

# 13. Source binding / identity

A stable source binding should include:

- configured integration/Data Source ID;
- ZWaveController Resource ID;
- Home ID / network context;
- node ID;
- endpoint;
- Command Class;
- property;
- property key.

Long Range introduces 16-bit node IDs.

Do not truncate to 8-bit.

Do not use friendly name as identity.

## Node ID reuse

Node IDs can be reused after lifecycle changes.

Therefore:

- canonical Equipment ID remains independent;
- exclusion + later different-node inclusion must not silently hijack existing Equipment;
- replacement workflow explicitly decides continuity.

---

# 14. Canonical EliteSCADA projection

## Data Source

One configured Z-Wave controller/network integration.

## Equipment

Normally one physical Z-Wave node.

Multi-endpoint devices remain one Equipment unless they clearly represent independent physical equipment.

## TAG

Persistent value state with clear process semantics.

## Command

Approved process mutation.

Do not expose arbitrary Command Class method invocation as a normal canonical Command.

## Capability

Semantic mapping over well-understood values/device metadata.

## Location

EliteSCADA canonical Location remains authoritative.

---

# 15. Capability mapping

| EliteSCADA capability | Z-Wave source | Preliminary status |
|---|---|---|
| OnOff | Switch Binary / bounded Switch Multilevel semantics | GO |
| Dimmer | Switch Multilevel | GO |
| ColorLight | Color Switch | GO_WITH_FEATURE_RULES |
| Temperature | Multilevel Sensor temperature | GO |
| Humidity | Multilevel Sensor humidity | GO |
| Occupancy | Notification / Binary Sensor | GO_WITH_DEVICE_RULES |
| Illuminance | Multilevel Sensor illuminance | GO |
| Contact | Notification / Binary Sensor | GO_WITH_DEVICE_RULES |
| Leak | Notification / sensor semantics | GO_WITH_DEVICE_RULES |
| Smoke | Notification / sensor semantics | GO_WITH_DEVICE_RULES |
| Battery | Battery CC | GO |
| Power | Meter / sensor semantics | GO_WITH_SCALE_RULES |
| Energy | Meter energy scale | GO_WITH_SCALE_RULES |
| Voltage | Meter / sensor voltage | GO_WITH_SCALE_RULES |
| Current | Meter / sensor current | GO_WITH_SCALE_RULES |
| Cover | Window Covering / mapped values | GO_WITH_DEVICE_RULES |
| Lock | Door Lock / Access Control | DEFER_OR_STRICT_GATES |
| Thermostat/Climate | Thermostat Mode/Setpoint/Operating State | GO_WITH_FEATURE_RULES |
| Fan | device/CC-specific semantics | GO_WITH_FEATURE_RULES |

Rules:

- use Z-Wave JS value metadata;
- honor readable/writeable/type/unit/min/max/states;
- interpret Command Class/property/propertyKey, not labels alone;
- use upstream device configuration quirks;
- do not duplicate device quirk tables without necessity.

---

# 16. Do not force-fit protocol administration into Capability

Do not create ordinary capabilities for:

- associations;
- configuration parameters;
- route data;
- neighbor tables;
- raw Command Class values;
- NVM operations;
- RF-region configuration;
- Central Scene event detail;
- SmartStart provisioning.

These belong to diagnostics, advanced Engineering, network administration or event path.

---

# 17. Central Scene and transient values

Central Scene is event-like/stateless interaction.

Examples:

- key pressed;
- key held;
- key released;
- scene number;
- key attribute.

Do not create a fake persistent TAG that remains true after a press.

Required:

**RESEARCH_CONTRACT_DELTA_REQUIRED — DRIVER-TRANSIENT-EVENT-01**

Event identity should preserve:

- controller/network context;
- node;
- endpoint;
- Command Class;
- property/property key / scene;
- event value;
- occurrence ordering/timestamp.

If first Z-Wave release ships before a canonical transient-event contract exists:

- do not claim Central Scene as a normal Capability;
- keep it diagnostics-only or defer it.

---

# 18. Sleeping nodes

Battery nodes may sleep for long periods.

## Availability

Do not equate sleeping with failed/offline.

Distinguish:

- asleep;
- awake;
- dead/unreachable;
- interview incomplete;
- last seen/last awake;
- stale process value.

Z-Wave JS 15.29.0 added persisted lastAwake.

## Queued writes

A write to a sleeping node may be deferred until wake-up.

Truth model:

- Requested;
- Queued/Deferred;
- Confirmed;
- Failed/Expired.

Do not tell Runtime/operator that a physical setpoint changed if the write is only queued.

If current Runtime.WriteAsync cannot represent deferred completion truthfully:

**RESEARCH_FINDING / MAIN_DECISION_REQUIRED — ZWAVE-SLEEPING-WRITE-TRUTH**

Recommendation:

- canonical process state remains last confirmed value;
- queued desired state is diagnostic/command state until confirmed.

## Deduplication

Current 15.29+ core deduplicates many redundant/superseded transactions.

EliteSCADA must still avoid command storms.

Do not repeatedly issue the same desired value every scan because a battery node has not woken yet.

---

# 19. Interviews

Node interview is asynchronous.

Current stack/server provides interview stage/progress.

Expose truthful states:

- included;
- interviewing;
- interview percent/stage;
- ready;
- partial/failed;
- asleep waiting for interview;
- dead/unreachable.

Do not create canonical process mappings until required metadata is known.

---

# 20. Associations / routes / firmware

## Associations

First release:

- read lifeline/association info for diagnostics;
- allow required stack/device lifeline behavior;
- do not automatically create arbitrary user associations.

## Routes / heal

Route data is diagnostic/maintenance.

Network-wide maintenance must not run automatically after every restart.

## Firmware

Node OTA is not required for first Z-Wave driver viability.

Controller firmware update is an even higher-risk Host Resource operation and should remain separate from ordinary Runtime.

---

# 21. Diagnostics

Reuse the common Driver diagnostics direction.

Recommended ladder:

**Host -> Sidecar -> Controller -> Network -> Node -> Value -> Live TAG**

## Host

- controller device present;
- device identity;
- lease owner;
- serial path;
- USB permissions;
- container passthrough.

## Sidecar

- process;
- server version;
- core version;
- Node version;
- WebSocket;
- schema;
- restart count.

## Controller

- connected;
- Home ID;
- controller node ID;
- family;
- firmware/SDK;
- RF region;
- Long Range support;
- current node ID type;
- jammed/unresponsive state;
- NVM backup status.

## Network

- inclusion state;
- SmartStart provisioning count;
- node count;
- Classic/LR mix;
- security configuration complete.

## Node

- interview stage/progress;
- security class;
- awake/asleep;
- last awake;
- ready/dead/alive;
- route/lifeline health;
- pending writes.

## Value

- Value ID;
- metadata;
- timestamp;
- current confirmed value;
- writeability;
- unit.

Do not expose protected key bytes.

---

# 22. Platform / packaging

Both current packages require Node >=20.

The sidecar package must pin a supported runtime.

## Windows

Expected viable:

- EliteSCADA Windows service;
- managed Node sidecar;
- COM/USB controller;
- loopback WebSocket;
- host Protected Material Authority.

Qualification needed for service startup, USB reconnect, identity and update/rollback.

## Linux x64

Strong path:

- /dev/serial/by-id;
- service account permissions;
- systemd/managed process;
- persistent sidecar data;
- loopback WebSocket.

## Linux ARM64

Plausible and useful for Edge.

Needs explicit qualification of Node, serialport binding, Z-Wave JS, USB passthrough and EliteSCADA arm64 packaging.

## Container

Requirements:

- USB passthrough;
- stable device mapping;
- persistent sidecar state;
- protected key injection;
- non-root/device permissions;
- exclusive controller lease.

The controller cannot be shared by replicas.

---

# 23. Hardware lab recommendation

Every record must pin:

- exact model;
- regional SKU;
- hardware revision;
- firmware;
- SDK where available;
- RF region;
- date;
- core/server versions.

## Primary 800 controller

**Aeotec Z-Stick 10 Pro — ZWA060**

Current vendor evidence:

- Series 800 / Gen8;
- EFR32ZG23;
- Z-Wave Plus Certified;
- Long Range;
- SmartStart;
- native S2;
- SDK 7.22.0 or later;
- regional A/B/C variants.

This is a strong primary fixture because SDK is above the 7.19.0 NVM threshold.

## Secondary controller

Optional adversarial fixture:

**Zooz ZST39 800LR**

Use only with exact firmware qualification.

It is useful specifically for LR/NVM regression because #8833 has recent reports on this family.

## S2 actuator + meter

For a US-region protocol lab:

**Zooz ZEN04 800LR Smart Plug**

Current evidence:

- 800 series;
- Long Range;
- energy monitoring;
- US-region.

Not a Brazil default.

## Contact / sleepy environmental fixture

**Aeotec Door Window Sensor 8 — ZWA055**

Variants:

- ZWA055-A US;
- ZWA055-B AU;
- ZWA055-C EU.

Current evidence:

- 800 series;
- Long Range;
- battery;
- contact;
- temperature;
- humidity;
- tilt.

## Motion / illuminance

**Aeotec TriSensor 8 — ZWA045**

Current evidence:

- Series 800;
- S2;
- Long Range;
- battery;
- motion;
- temperature;
- illuminance.

Pin regional variant.

## Dimmer / Central Scene

For US-region lab:

**Zooz ZEN77 800LR**

Current evidence:

- 800 series;
- Long Range;
- On/Off + brightness;
- multi-tap scene behavior;
- current firmware branches use SDK 7.24.1.

Useful for Dimmer and transient-event qualification.

## Sleepy temperature/humidity alternate

For US-region lab:

**Zooz ZSE44 800LR**

Battery, temperature/humidity, 800 series, LR, S2, SmartStart.

## Lock

Not mandatory.

Only add when Main authorizes access-control scope.

---

# 24. Brazil lab note

If the physical lab is in Brazil:

1. use current Silicon Labs region reference as planning input:
   - 919.8 / 921.4 MHz;
   - ANZ family;
2. candidate controller:
   - Aeotec ZWA060-B 921.42 MHz;
3. procure only devices with compatible regional SKU;
4. verify ANATEL/homologation before RF transmission;
5. do not mix US 908.42 MHz fixtures into the operational Brazil lab.

US Zooz fixtures remain model recommendations for a separate US-region test environment.

---

# 25. L0-L4 test model

## L0 — contract / mapper

Test:

- Server version handshake;
- schema compatibility;
- stable controller/network binding;
- 16-bit node ID handling;
- node/endpoint/CC/property/propertyKey identity;
- value metadata mapping;
- capability mapping;
- security-key reference redaction;
- six-key config validation;
- SmartStart/DSK validation;
- Central Scene classification;
- sleeping write state;
- malformed/oversized payload;
- unknown Command Class;
- friendly-name changes do not alter binding.

## L1 — fake Z-Wave JS Server

Simulate:

- version/schema negotiation;
- controller connect/disconnect;
- unexpected controller identity;
- inclusion;
- grant security classes;
- DSK validation;
- inclusion failure;
- SmartStart;
- node add/remove;
- interview progress/failure;
- sleeping node;
- wake-up;
- queued/confirmed/failed write;
- stale value;
- Central Scene/value notification;
- sidecar crash/restart;
- event flood;
- malformed event;
- NVM backup progress/failure;
- restore compatibility rejection;
- missing protected material;
- controller contention.

## L2 — real independent Z-Wave JS Server

Use exact pinned real server/core.

Validate:

- real schema;
- state event shape;
- Value IDs;
- command errors;
- controller methods;
- inclusion/security flow;
- NVM API shape;
- server restart;
- schema mismatch.

## L3 — EliteSCADA canonical Runtime

Verify:

- Data Source lifecycle;
- ZWaveController lease;
- protected keys resolved server-side;
- Equipment materialization;
- TAG updates;
- Runtime.WriteAsync path;
- readback;
- queued write truth;
- Alarm/Historian/Gateway consume canonical TAGs;
- transient-event path when available;
- diagnostics ladder;
- controller/sidecar restart;
- no keys/NVM in project export.

## L4 — real hardware

Minimum matrix:

- current 800 controller;
- S2 mains actuator;
- metering device;
- contact sensor;
- motion/illuminance sensor;
- sleepy temperature/humidity sensor;
- dimmer/scene device;
- LR node where region supports it.

Scenarios:

- startup;
- S2 Unauthenticated where available;
- S2 Authenticated;
- S2 Access Control only if later authorized;
- SmartStart;
- Classic inclusion;
- exclusion;
- re-inclusion;
- interview;
- sleeping interview;
- write/readback;
- write while asleep;
- controller unplug/replug;
- service restart;
- sidecar crash/restart;
- route/lifeline health;
- Central Scene;
- NVM backup;
- same-controller restore;
- spare-controller migration;
- LR 16-bit node behavior;
- post-backup integrity;
- key-loss failure;
- exact RF-region validation.

---

# 26. Scale / performance qualification

These are EliteSCADA targets, not protocol limits.

## L1/L2 synthetic

- 100 nodes;
- mixed endpoints/values;
- burst reports;
- sleeping-node population;
- queued writes;
- interview progress burst;
- sidecar reconnect.

## L4 first target

If inventory permits:

- at least 20 real mixed nodes;
- mains routers + battery nodes;
- multiple S2 classes;
- LR fixture;
- mixed report rates.

## Large-network qualification

Before enterprise/MDU claims:

- 100+ node physical/reference soak;
- restart without all-node traffic storm;
- memory trend;
- queue depth;
- 24h / 72h soak;
- controller recovery;
- NVM backup effect;
- sleeping-node backlog;
- repeated thermostat setpoint scenario.

Closed #9132 is a useful regression pattern even though 15.29+ contains the dedup fixes.

---

# 27. Exact gates

## GATE-ZWAVE-01 — ZWaveController Host Resource

**REQUIRED**

Stable identity, discovery, exclusive lease, Windows/Linux endpoint, controller metadata, firmware/SDK, RF region, NVM identity, replacement and safe restart.

Generic SerialPort is necessary but insufficient.

## GATE-ZWAVE-02 — managed sidecar lifecycle

**REQUIRED**

**RESEARCH_CONTRACT_DELTA_REQUIRED — SIDECAR-LIFECYCLE-01**

Process supervision, version/schema pin, startup/shutdown, health, logs, crash recovery, persistent state and upgrade/rollback.

## GATE-ZWAVE-03 — control-plane isolation

**REQUIRED**

Upstream WebSocket has no authentication and defaults to all interfaces.

V1 must bind loopback and stay behind EliteSCADA trust/authorization.

## GATE-ZWAVE-04 — protected key injection

**REQUIRED**

Protected Material Authority exists.

Future implementation must consume it for six network keys without plaintext persistence in Engineering/project/logs.

## GATE-ZWAVE-05 — NVM DR proof

**REQUIRED**

Encrypted backup, metadata, compatibility-aware restore, spare-controller test, Home ID/node preservation and keys.

## GATE-ZWAVE-06 — current LR/NVM backup bug

**OPEN**

zwave-js/zwave-js#8833 remains open 2026-10-06.

PR #8834 closed without merge.

Production LR + automatic NVM backup must not be declared ready until revalidated/fixed/mitigated.

## GATE-ZWAVE-07 — transient event contract

**REQUIRED FOR CENTRAL SCENE**

**RESEARCH_CONTRACT_DELTA_REQUIRED — DRIVER-TRANSIENT-EVENT-01**

## GATE-ZWAVE-08 — sleeping-node process truth

**MAIN_DECISION_REQUIRED IF CURRENT WRITE RESULT CANNOT EXPRESS DEFERRED**

Need honest queued vs confirmed behavior.

## GATE-ZWAVE-09 — RF region / hardware matrix

**REQUIRED**

Every supported controller/device pins region/model/firmware/date.

## GATE-ZWAVE-10 — L0-L4 evidence

**REQUIRED**

No production claim before fake sidecar, real server, canonical Runtime and physical recovery evidence.

---

# 28. RESEARCH_FINDING / MAIN_DECISION_REQUIRED

## RF-ZWAVE-01 — Host Resource specialization

Evidence:

- #469 generic serial authority is in current base;
- Z-Wave controller has persistent NVM/network identity.

Recommendation:

create ZWaveController resource family on generalized Host Resource contract.

Risk if ignored:

- two owners;
- accidental replacement;
- unstable OS path binding;
- unsafe restore target.

## RF-ZWAVE-02 — unauthenticated server API

Evidence:

upstream WebSocket API has no authentication.

Recommendation:

managed local sidecar only for v1; loopback bind; remote later behind authenticated TLS transport.

## RF-ZWAVE-03 — security-key injection

Evidence:

Protected Material Authority is integrated; Z-Wave JS requires key bytes at runtime.

Recommendation:

resolve scoped references inside trusted launcher and inject through a bounded protected mechanism.

Never canonical JSON.

## RF-ZWAVE-04 — NVM backup + LR

Evidence:

#8833 open; fresh recurrence; #8834 not merged.

Recommendation:

development proceeds; no default unattended LR NVM backup until resolved/qualified; supervised backup with post-backup health verification.

## RF-ZWAVE-05 — sleeping write semantics

Evidence:

sleeping devices defer writes; 15.29 added dedup to prevent redundant queue growth.

Recommendation:

TAG state remains last confirmed process state; queued desired state is separate command/diagnostic state.

---

# 29. Preliminary future development slices

This research lane does not authorize implementation.

## Slice 1 — Host Resource + sidecar

- ZWaveController identity/lease;
- Windows/Linux serial binding;
- server/core version pin;
- loopback WebSocket;
- schema negotiation;
- health;
- L1.

## Slice 2 — inventory / canonical values

- one controller/network Data Source;
- node inventory;
- endpoint/value IDs;
- OnOff;
- Dimmer;
- Temperature;
- Humidity;
- Contact;
- Battery;
- Power/Energy;
- subscriptions/reports;
- Runtime writes/readback.

## Slice 3 — secure inclusion

- six protected keys;
- S2;
- DSK;
- SmartStart;
- interview progress;
- exclusion;
- admin audit.

## Slice 4 — sleepy/events

- sleep/awake/lastAwake;
- queued writes;
- Central Scene event path.

## Slice 5 — backup/DR/LR

- exact 800 controller;
- NVM backup/restore;
- spare controller;
- LR;
- #8833 disposition;
- L4.

---

# 30. Source ledger

All time-sensitive sources accessed/revalidated **2026-10-06**.

## Z-Wave JS core

- https://github.com/zwave-js/zwave-js
- https://github.com/zwave-js/zwave-js/releases
- https://github.com/zwave-js/zwave-js/blob/master/CHANGELOG.md
- https://github.com/zwave-js/zwave-js/blob/v15.31.0/packages/zwave-js/package.json
- https://github.com/zwave-js/zwave-js/blob/v15.31.0/LICENSE
- https://zwave-js.github.io/zwave-js/
- https://github.com/zwave-js/zwave-js/tree/master/packages/nvmedit
- https://zwave-js.github.io/qr/

## Z-Wave JS Server

- https://github.com/zwave-js/zwave-js-server
- https://github.com/zwave-js/zwave-js-server/releases
- https://github.com/zwave-js/zwave-js-server/blob/master/README.md
- https://github.com/zwave-js/zwave-js-server/blob/3.10.1/package.json
- https://github.com/zwave-js/zwave-js-server/blob/3.10.1/LICENSE

## Upstream issues/fixes

- https://github.com/zwave-js/zwave-js/issues/8833
- https://github.com/zwave-js/zwave-js/pull/8834
- https://github.com/zwave-js/zwave-js/issues/9132
- https://github.com/zwave-js/zwave-js/pull/9136
- https://github.com/zwave-js/zwave-js/pull/9137
- https://github.com/zwave-js/zwave-js/issues/5257

## Silicon Labs / RF

- https://www.silabs.com/software-and-tools/z-wave-800-series
- https://www.silabs.com/wireless/z-wave/introduction-to-z-wave-800-series
- https://www.silabs.com/wireless/z-wave/z-wave-long-range-overview
- https://www.silabs.com/wireless/z-wave/global-regions

## Controller / devices

- https://aeotec.com/products/aeotec-z-stick-10-pro/
- https://aeotec.com/products/door-window-sensor-8/
- https://store.aeotec.com/products/trisensor-8-zwa045
- https://www.support.getzooz.com/kb/article/2425-zen04-smart-plug-product-manual/
- https://www.support.getzooz.com/kb/article/2227-zse41-open-close-xs-sensor-user-manual/
- https://www.support.getzooz.com/kb/article/2310-zse44-temperature-humidity-sensor-product-manual/
- https://www.support.getzooz.com/kb/article/2448-zen77-dimmer-product-manual/

## EliteSCADA live foundations

- https://github.com/brunolrogerio-collab/EliteSCADA/issues/469
- https://github.com/brunolrogerio-collab/EliteSCADA/issues/497
- https://github.com/brunolrogerio-collab/EliteSCADA/issues/472
- https://github.com/brunolrogerio-collab/EliteSCADA/issues/542

---

# 31. Uncertainties to revalidate before implementation/release

1. latest Z-Wave JS stable version;
2. latest Server stable version/schema;
3. whether #8833 is fixed/closed and in which release;
4. safety of scheduled NVM backup on exact LR controller/firmware;
5. exact NVM migration matrix for primary/spare controller;
6. exact regional RF/homologation status;
7. controller firmware/SDK;
8. exact six-key injection mechanism;
9. Runtime ability to report deferred sleeping-node writes;
10. transient-event contract availability;
11. whether v1 needs remote/user-managed Server;
12. whether v1 includes access-control/locks;
13. ARM64 packaging evidence;
14. controller stable identity behavior on Windows/Linux/container.

---

# 32. Checkpoint 2 conclusion

**Preliminary decision: Z-WAVE JS = GO_WITH_GATES**

The underlying stack is mature enough for a bounded future implementation.

Preferred architecture:

**EliteSCADA -> managed Z-Wave JS Server -> ZWaveController Host Resource -> Z-Wave network**

Compared with the 2026-10-02 WAIT_DEPENDENCY conclusion, two former foundation blockers are now materially resolved:

- generic host serial infrastructure exists in the current base;
- generic Protected Material Authority exists in the current base.

Remaining product deltas are explicit and bounded:

- specialize Host Resource as ZWaveController;
- managed sidecar lifecycle;
- loopback/authenticated control plane;
- six protected network keys;
- NVM DR qualification;
- current #8833 LR/NVM disposition;
- sleeping-node truth;
- transient-event path;
- regional hardware qualification;
- L0-L4 evidence.

This research does **not** authorize coding.

## Next <=3 actions

1. Main reviews/accepts or changes **Z-WAVE JS = GO_WITH_GATES**.
2. On a new **SIGA**, execute **CHECKPOINT 3 — COMMON SIDECAR / HOST RESOURCE / LAB / LEGAL** only.
3. After another continuation, perform final time-sensitive revalidation and handoff.

---

**DOCS_ONLY**

**NO PRODUCT CODE CHANGED**

**NO DEPENDENCY CHANGED**

**NO CI CHANGED**

**NO HOST RESOURCE CODE CHANGED**

**NO MATTER CODE IMPLEMENTED**

**NO ZWAVE CODE IMPLEMENTED**

**NO MERGE PERFORMED**
