# Matter Controller Readiness Research

Research date: **2026-10-06**  
Issue owner: **#542 — HOME-RESEARCH-03 — Matter + Z-Wave JS readiness and dependency gates**  
Parent: **#472 — HOME/BUILDING Integration Foundation**  
Historical input only: **#475 — HOME/BUILDING DRIVER RESEARCH**  
Contract: **C-HOME-MATTER-ZWAVE-RESEARCH-01**  
Order: **HOME-MATTER-ZWAVE-READINESS-RESEARCH-01**  
Branch: **research/home-matter-zwave-readiness**  
Release base revalidated before work: **wave15/corrections-integration@69d5248e462ef12c41d99c6859a3afb77c6e3e2d**  
Scope: **RESEARCH_ONLY / DOCS_ONLY / NO_PRODUCT_CODE / NO_MERGE_BY_RESEARCHER**

## Checkpoint 1 decision

**MATTER = GO_WITH_GATES**

Matter is no longer best classified as a generic WAIT_DEPENDENCY. There is now a credible, actively maintained controller/server architecture that is suitable for a bounded EliteSCADA development lane:

**EliteSCADA -> managed Open Home Foundation Matter(.js) Server sidecar -> Matter fabric -> Matter devices**

The preferred controller server is the Open Home Foundation Matter(.js) Server (matterjs-server) stable 1.4.0 on matter.js 0.17.9.

This is not an unrestricted GO. The production path remains gated by:

1. the selected Matter(.js) Server successor is still explicitly described upstream as Beta and not yet officially re-certified by the CSA;
2. stable 1.4.0 supports Matter 1.6.0 while the current public CSA specification and connectedhomeip release line have advanced to Matter 1.6.1;
3. upstream officially supports recent 64-bit Linux and macOS, not Windows or WSL;
4. current open upstream issues include a pre-authentication PASE resource-exhaustion/DoS report and a CASE malformed-peer OOM report planned for a subsequent version;
5. EliteSCADA still needs a deliberate managed-sidecar lifecycle/protected-state contract before product implementation;
6. Thread commissioning requires a usable Thread network, IPv6/mDNS reachability and a Thread Border Router, but EliteSCADA does not need to own that Border Router for the first slice;
7. fabric/controller backup, restore and migration must be treated as protected operational state and proven by L1/L2/L4 testing;
8. Matter certification/trademark claims require separate CSA/legal review and cannot be inferred from Apache-2.0 licensing.

Therefore:

- **engineering architecture readiness: GO_WITH_GATES**;
- **production/certified shipping: NOT YET GO**;
- **Windows-native local controller: WAIT on an explicit Main platform decision or a supported upstream path**.

## What changed since the 2026-10-02 historical dossier

The previous research correctly selected a sidecar/server architecture but retained WAIT_DEPENDENCY.

Current evidence supports promotion to GO_WITH_GATES because:

- stable Matter(.js) Server 1.4.0 has a versioned WebSocket API, schema 13 with backward compatibility to supported older clients, persistent controller storage, named Wi-Fi/Thread credential management, commissioning APIs, event delivery, Thread diagnostics and ICD support;
- matter.js stable 0.17.9 remains actively maintained, with Matter 1.6.0 support introduced in 0.17.5 and continued fixes to subscriptions, sessions, BLE and sleepy/ICD behavior;
- the official connectedhomeip SDK has a v1.6.1.0 release and remains a strong independent protocol/reference implementation for L2;
- concrete Wi-Fi Matter, Thread actuator, Thread sleepy/contact sensor and Thread Border Router lab fixtures are available from current vendors.

The promotion is architectural, not a statement of certification or production release readiness.

---

# 1. Current stack revalidation

## 1.1 Open Home Foundation Matter(.js) Server — preferred sidecar

### Current stable

- package: **matter-server**
- stable/current latest tag observed: **1.4.0**
- release date: **2026-08-07**
- current dev tag observed on 2026-10-06: **1.4.1-alpha.10-20261006-ac744ef**
- upstream repository: https://github.com/matter-js/matterjs-server
- npm: https://www.npmjs.com/package/matter-server
- controller protocol: WebSocket
- stable WebSocket schema in 1.4.0: **13**
- minimum supported client schema documented for schema 13: **11**
- underlying stable matter.js in 1.4.0: **0.17.9**
- Matter specification level stated by upstream server: **Matter 1.6.0**

### License

Source files and the GitHub container identify the project as **Apache-2.0**.

Important packaging note:

- the npm package sidebar currently reports License: none for matter-server 1.4.0;
- sibling packages/source headers and the GitHub container identify Apache-2.0.

This is a metadata inconsistency, not evidence that the source changed license. Before redistribution, package/SBOM legal validation must verify the exact installed artifacts, LICENSE/NOTICE obligations and transitive dependencies.

**LEGAL_REVIEW_REQUIRED** for redistribution packaging, certification claims and logo/trademark use.

### Activity and maturity

The project is actively maintained, including same-day 1.4.1 alpha builds observed on 2026-10-06.

However, upstream currently states that this matter.js-based controller is:

- Beta;
- in testing phase;
- not yet officially re-certified by CSA;
- intended to be re-certified later.

This is the largest maturity gate.

### API/schema stability

The WebSocket API has explicit schema negotiation:

- server_info advertises schema_version;
- clients can gate operations by required schema;
- schema 13 keeps a documented minimum supported schema 11;
- the changelog records additions and compatibility expectations.

EliteSCADA must still pin both:

- sidecar semantic version;
- accepted WebSocket schema range.

Do not bind directly to undocumented JavaScript internals.

### Supported platforms

Upstream currently documents only:

- recent **64-bit macOS**;
- very recent **64-bit Linux**.

Upstream explicitly says non-64-bit and other operating systems, including WSL, are unsupported.

Implications:

| Platform | Research status |
|---|---|
| Linux x64 | **GO_WITH_GATES** |
| Linux ARM64 | **GO_WITH_GATES**, container artifacts exist; validate target kernel/network/BLE |
| macOS ARM64 | supported upstream for development/runtime, not an EliteSCADA deployment priority |
| Windows x64 native | **WAIT / upstream unsupported** |
| Windows ARM | **WAIT / upstream unsupported** |
| WSL | **WAIT / explicitly unsupported upstream** |
| Docker on Linux host | **GO_WITH_GATES** |
| Docker Desktop / VM-based Windows path | **NOT QUALIFIED**; do not infer support from Linux image availability |

### Container

The upstream publishes container images and documents a Linux host-network example with persistent /data storage.

Container deployment is not a transparent abstraction because Matter depends on:

- IPv6;
- link-local multicast;
- mDNS/DNS-SD;
- IPv6 Neighbor Discovery / Router Advertisements;
- same-LAN visibility;
- Thread Border Router routes;
- optional BLE/HCI access.

For the first product slice, qualify a Linux host/container topology explicitly. Do not promise generic Docker portability.

### Persistent storage

Default documented server storage path:

- ~/.matter_server

Container documentation uses:

- persistent /data volume.

The storage contains controller/fabric operational state and must not be treated as a disposable cache.

### Current known blockers / watch items

Open upstream issues observed 2026-10-06 include:

- #1092 — Pre-Authentication DoS in Matter PASE via unvalidated PBKDF2 iteration count; labeled planned for next version;
- #1039 — malformed CASE Sigma2 maxPathsPerInvoke=0 can crash server with OOM; labeled planned for next version;
- #1084 — an ICD/sleepy-device subscription-loss recovery case;
- additional commissioning/attestation/network edge reports.

**Release gate recommendation:** development may target 1.4.0 in isolated L1/L2/L4 validation, but a production release should require a stable upstream release containing the security/resource-exhaustion fixes above or an explicit security review proving equivalent mitigation.

---

## 1.2 matter.js — controller SDK under the preferred server

### Current stable

- package family: **@matter/**
- primary package: **@matter/main**
- stable version: **0.17.9**
- stable release date: **2026-08-06**
- license: **Apache-2.0**
- repository: https://github.com/matter-js/matter.js
- npm: https://www.npmjs.com/package/@matter/main

### Matter specification support

- matter.js 0.17.5 upgraded the implementation to **Matter 1.6.0**;
- stable 0.17.9 continues that line;
- current main/WIP has upgraded model material to **Matter 1.6.1**;
- 1.6.1 is therefore not yet the pinned stable basis used by Matter(.js) Server 1.4.0.

### Controller maturity

Current matter.js contains:

- controller commissioning;
- PASE/CASE/session handling;
- persistent storage/migration machinery;
- attribute read/write/subscribe;
- command invocation;
- event reporting;
- BLE support through Node platform packages;
- Thread Border Router client/diagnostics helpers;
- ICD/SIT/LIT support;
- subscription re-establishment fixes;
- multi-fabric/controller primitives.

Recent stable releases include meaningful fixes to:

- subscription recovery;
- event listeners;
- session selection;
- MRP timing;
- BLE shutdown/discovery;
- ICD behavior.

### API stability

The changelog still documents breaking changes between major minor lines, including storage and model/API changes.

Therefore:

- do not embed matter.js objects into EliteSCADA product contracts;
- keep the sidecar/WebSocket boundary;
- version-pin the server and schema;
- migrate persistent state only through validated upstream transitions.

---

## 1.3 project-chip/connectedhomeip — authoritative reference, not first embedding choice

### Current release

Observed official latest release:

- **v1.6.1.0**
- released **2026-09-25**
- repository: https://github.com/project-chip/connectedhomeip
- license: **Apache-2.0**

The release notes align the data model with Matter 1.6.1.

### Role for EliteSCADA

Use connectedhomeip for:

- specification-level reference;
- chip-tool/commissioning reference behavior;
- virtual/reference devices;
- independent L2 interop validation;
- comparison when a sidecar behavior is ambiguous.

Do not select direct C++ embedding for the first implementation without a new Main decision.

Reason:

- materially larger native build/toolchain surface;
- more OS packaging complexity;
- direct lifecycle/persistence ownership;
- higher upgrade/certification integration burden;
- unnecessary coupling compared with a stable WebSocket process boundary.

**Recommended:** reference implementation, not first product runtime.

---

## 1.4 Python Matter Server — historical certified predecessor, not new product basis

The previous Python Matter Server line was a CSA-certified Software Component but has been superseded by the matter.js-based server.

Use it only as:

- historical compatibility reference;
- migration evidence;
- secondary differential oracle when needed.

Do not choose the deprecated predecessor as the long-lived EliteSCADA implementation merely because the successor has not yet been re-certified.

---

# 2. Recommended architecture

## 2.1 Selected architecture

**EliteSCADA**
-> **managed Matter(.js) Server sidecar/service**
-> **Matter fabric**
-> **Matter nodes/endpoints/clusters**

This remains better than directly embedding connectedhomeip for the first release.

## 2.2 Sidecar lifecycle contract required

The future product implementation should define a common sidecar contract with at least:

### Version pinning

Persist/diagnose:

- Matter Server semantic version;
- matter.js version;
- WebSocket schema version;
- supported schema minimum/maximum;
- storage schema/migration level;
- Matter specification level.

### Startup

1. validate executable/image and version;
2. acquire exclusive ownership of configured storage;
3. bind privileged local IPC only;
4. load/migrate storage;
5. initialize controller/fabric;
6. initialize required network interfaces;
7. expose health/readiness only after the WebSocket API is accepting requests and fabric state is coherent;
8. start EliteSCADA client;
9. perform schema negotiation;
10. start node/subscription reconciliation.

### Shutdown

- stop new administrative operations;
- drain/stop controller client work;
- close subscriptions/sessions gracefully where supported;
- flush state;
- terminate child process with bounded timeout;
- escalate kill only after graceful timeout;
- never start a second process against the same storage concurrently.

### Restart/crash recovery

- supervised restart through systemd/container/service manager;
- bounded exponential backoff;
- record last exit reason/code;
- distinguish crash-loop from network/device unavailability;
- after restart, reconnect WebSocket and reconcile node/subscription state;
- do not generate a reconnect storm against all sleepy nodes.

### Health

At minimum separate:

- process alive;
- WebSocket reachable;
- schema compatible;
- storage loaded;
- fabric/controller ready;
- network interface ready;
- mDNS/IPv6 readiness;
- Thread network/TBR availability when applicable;
- BLE capability/proxy availability when commissioning needs it;
- node/session/subscription diagnostics.

Do not collapse all failures into Driver Offline.

### Local communication transport

Preferred order:

1. Unix domain socket on Linux if supported by the selected stable release and all required features;
2. otherwise localhost-only WebSocket TCP;
3. remote network control only behind an EliteSCADA-authenticated tunnel/proxy with explicit authorization.

The upstream warns that the WebSocket server can otherwise bind broadly. The BLE proxy protocol has no built-in authentication.

Treat both /ws and /ble as privileged control planes.

### Authentication / authorization

The Matter protocol's PASE/CASE security does not authenticate a local process connecting to the sidecar control API.

Therefore:

- localhost/Unix socket is the default trust boundary;
- OS account/socket ACLs are preferred where possible;
- if the sidecar is remote, add authenticated and encrypted transport outside the upstream protocol;
- normal Runtime users must never gain raw sidecar administrative access;
- Engineering authorization controls who may commission, remove, open a commissioning window or mutate network credentials.

### Logging

Logs must redact:

- QR/manual setup codes after use;
- Wi-Fi passwords;
- Thread operational datasets;
- fabric private material;
- Door Lock PIN/credential material;
- operational certificates/private keys where sensitive;
- backup encryption keys.

Diagnostics should expose references/state, not secret values.

### Backup / rollback

Back up the sidecar state as an atomic protected unit.

A release update procedure should be:

1. stop sidecar;
2. create encrypted/versioned backup;
3. record exact current sidecar/matter.js/schema version;
4. upgrade;
5. allow upstream migration;
6. validate fabric and node connectivity;
7. if rollback is supported, restore both binary/image **and compatible state** together.

Never assume a newer migrated state can be opened safely by an older binary.

**RESEARCH_CONTRACT_DELTA_REQUIRED — SIDECAR-LIFECYCLE-01**

No implementation is authorized by this research lane.

---

# 3. Commissioning and administrative boundary

## 3.1 Pairing code inputs

The controller stack supports:

- QR pairing code;
- manual pairing code.

EliteSCADA should accept either as commissioning input.

Validate before use:

- syntactic validity;
- expected Matter payload/passcode range;
- maximum length;
- one-time handling;
- redaction from logs/history.

Do not persist setup codes in Engineering JSON or .escadapkg.

## 3.2 Commissioning sequence

Conceptual flow:

1. sidecar/controller healthy;
2. user initiates privileged Commission action;
3. discover or address commissionable node;
4. user scans QR or enters manual setup code;
5. establish **PASE** commissioning session;
6. read/validate device attestation information;
7. select/provision operational network as needed:
   - Wi-Fi credentials for Wi-Fi devices;
   - Thread operational dataset for Thread devices;
8. device joins operational IP network;
9. create/install Matter operational credentials / NOC into selected fabric;
10. establish operational **CASE** session;
11. read node/endpoints/device types/clusters/features;
12. materialize a discovery candidate;
13. user maps Location/name/Equipment/Capabilities;
14. Apply canonical TAGs/Commands;
15. start subscriptions.

## 3.3 Device attestation

During commissioning, the commissioner must validate device attestation evidence against trusted PAA/PAI/DAC material and certification status policy.

Do not silently enable test-DCL/trust bypass in production.

Operational policy should classify:

- valid production attestation;
- unknown/unavailable DCL;
- test certificate;
- revoked/failed attestation;
- explicit engineering override if Main later authorizes one.

Attestation failure is not a normal process-quality state; it is a privileged commissioning/security failure.

## 3.4 PASE vs CASE

Keep the concepts distinct:

- **PASE**: onboarding/commissioning authenticated session based on setup passcode;
- **CASE**: operational certificate-authenticated secure session on the Matter fabric after commissioning.

Normal Runtime read/write/subscription must use operational controller state and must not re-enter commissioning flows.

## 3.5 Fabrics and multi-admin

A Matter node may belong to multiple independent fabrics.

Multi-admin therefore means:

- another authorized Matter controller/fabric can coexist;
- opening a commissioning window is a privileged admin operation;
- removing the EliteSCADA fabric does not imply deleting other fabrics;
- factory reset is broader than leaving one fabric.

The EliteSCADA canonical Equipment identity must not be identical to a Matter FabricIndex or NodeId.

## 3.6 Recommissioning / removal / reset

Separate actions:

- reconnect operational node;
- re-interview node;
- open commissioning window for another admin;
- remove node from EliteSCADA fabric;
- remove fabric;
- recommission after credentials/network changes;
- factory reset physical device.

Do not map these to generic HMI Commands.

### Normal Runtime

- read attributes;
- write supported writable process attributes;
- invoke approved process commands;
- receive subscriptions/reports;
- receive events;
- expose diagnostics.

### Commissioning/Admin

- pair/commission;
- network credential mutation;
- fabric creation/removal;
- open commissioning window;
- operational credential/fabric administration;
- controller/fabric restore;
- factory reset coordination.

Administrative operations require Engineering/admin authorization, confirmation and audit.

---

# 4. Matter over Thread

## 4.1 Thread is not required for all Matter

Matter can operate over:

- Ethernet;
- Wi-Fi;
- Thread.

A Matter-over-Wi-Fi plug/light does not require Thread.

## 4.2 Thread Border Router requirement

A Matter-over-Thread deployment needs a usable Thread Border Router connecting the Thread mesh to the operational IP network.

Current evidence supports the first EliteSCADA architecture as:

**EliteSCADA Matter controller consumes an existing Thread network/TBR.**

EliteSCADA does **not** need to own or manage an OTBR in the first slice.

Examples of current external Thread Border Routers include Google Nest Hub (2nd gen), Nest Hub Max, Nest Wifi Pro and Google TV Streamer (4K).

## 4.3 Direct Thread commissioning

To provision a factory-new Thread Matter node directly, the controller needs a Thread operational dataset/credentials for the target Thread network.

Matter(.js) Server schema 12+ supports:

- named Thread datasets;
- write-only credential handling;
- selecting stored Thread credentials during commissioning.

Alternative bootstrapping:

- device is first joined to an existing ecosystem's Thread network;
- then shared to EliteSCADA as another Matter administrator via multi-admin/commissioning window.

The exact permitted flow depends on the external ecosystem and available credential sharing.

## 4.4 IPv6 / mDNS / multicast

Required operational assumptions:

- IPv6 enabled on controller host;
- correct IPv6 Router Advertisement / Neighbor Discovery behavior;
- same effective LAN/link visibility as Matter devices/TBRs;
- mDNS/DNS-SD multicast not filtered/corrupted;
- no generic assumption that an enterprise mDNS forwarder improves Matter;
- routes advertised by TBR available to controller.

Container validation must prove these explicitly.

## 4.5 BLE bootstrap

BLE is commonly used to reach a factory-new, not-yet-networked node during commissioning.

Upstream current local BLE support is Linux-oriented.

Matter(.js) Server also has a BLE proxy mode.

For EliteSCADA:

- BLE is a commissioning transport, not the normal operational path;
- a BLE adapter/proxy may be required for some first-pair flows;
- do not require BLE after the node is operational over IP;
- do not expose the BLE proxy endpoint to an untrusted network;
- proxy liveness and reconnect need sidecar supervision.

## 4.6 ThreadRadio/RCP decision

**Do not create a ThreadRadio/RCP Host Resource for the first Matter slice.**

Only introduce one if Main later decides EliteSCADA must own an OTBR.

If that future architecture is selected, research/contract must then cover:

- RCP identity;
- exclusive lease;
- serial/USB device;
- firmware;
- OTBR lifecycle;
- Thread dataset authority;
- network ownership/migration;
- backup/recovery.

This is not required for consuming an existing Thread network.

---

# 5. Matter over Wi-Fi

Matter-over-Wi-Fi must be qualified separately from Thread.

For Wi-Fi nodes:

- operational transport is IP;
- Thread is not involved;
- commissioning may still use BLE before Wi-Fi credentials are provisioned;
- already-on-network commissionable nodes may support IP/on-network commissioning;
- mDNS/IPv6 remain important to discovery/session establishment.

First hardware qualification should include at least one Wi-Fi-only Matter device to prove the product does not accidentally depend on Thread.

---

# 6. Fabric persistence and protected material

## 6.1 Critical persistent controller state

Treat the following as protected persistent operational authority:

- fabric identity;
- controller operational identity;
- root/trust material;
- controller operational certificates and private keys;
- Fabric ID / controller node identity;
- commissioned node IDs/fabric-scoped metadata;
- ACL-relevant fabric state;
- Wi-Fi commissioning credentials when stored;
- Thread operational datasets when stored;
- ICD registration/monitor state as required by upstream;
- sidecar storage database/files;
- migration version;
- backup metadata.

## 6.2 Protected Material Authority vs sidecar-owned state

### Protected Material Authority should own/reference

Where EliteSCADA directly provisions or controls the secret:

- Wi-Fi passwords;
- Thread operational datasets;
- backup encryption keys;
- remote-sidecar authentication credentials;
- any operator-supplied external protected credentials;
- one-time pairing setup codes only transiently during commissioning.

EliteSCADA product data should store a stable secret reference, not the secret value.

### Sidecar-owned protected persistent state

Keep opaque inside the controller state when generated/managed by Matter stack:

- fabric root/operational key material;
- controller private keys;
- operational certificates;
- internal fabric/session/controller database;
- stack-specific persisted security state.

EliteSCADA should back up this state as an encrypted opaque unit instead of copying keys into Engineering documents.

### Never place Matter secrets in

- .escadapkg;
- Engineering JSON;
- revision diff;
- ordinary logs;
- diagnostics payloads;
- Git;
- user-visible exported support bundles without explicit redaction/encryption.

## 6.3 Backup / restore

Required future behavior:

- backup only while state is quiescent or through an upstream-safe snapshot method;
- encrypt backup at rest;
- include exact version metadata;
- validate integrity before restore;
- stop sidecar before replacement;
- restore full compatible state;
- restart and verify fabric identity;
- verify existing commissioned nodes reconnect without recommissioning.

## 6.4 Disaster recovery / controller migration

A successful DR test means:

1. original controller instance unavailable;
2. new host receives authorized protected backup;
3. compatible sidecar/version starts;
4. same fabric/controller identity is restored;
5. existing nodes are reachable;
6. subscriptions recover;
7. no mass recommissioning is required;
8. secrets are not exposed in logs/support artifacts.

If the selected stack cannot prove this, production Matter remains gated.

---

# 7. Protocol identity -> canonical EliteSCADA identity

Protocol identity never replaces canonical Equipment ID.

## 7.1 Matter identity

Store protocol binding with enough fabric context:

- controller/fabric stable identity;
- Matter Node ID;
- endpoint number;
- cluster ID;
- attribute ID;
- event ID where applicable;
- command ID where applicable;
- feature map/revision needed for interpretation.

Do not use:

- friendly name;
- display label;
- endpoint name;
- network IP address

as stable protocol identity.

## 7.2 Canonical projection

Recommended projection:

### Equipment

Usually represents the physical Matter node or a clearly independent physical function.

One physical node may expose multiple endpoints. Do not automatically turn every endpoint into separate Equipment if they are one real device.

### TAG

Persistent process state derived from:

- cluster attributes;
- measured values;
- writable setpoints where canonical TAG semantics are correct.

### Command

Approved process actions derived from Matter commands or writable process attributes.

Do not expose arbitrary cluster invoke as a generic user Command.

### Capability

Curated semantic projection across well-understood cluster/device-type combinations.

### Location

Canonical EliteSCADA topology assignment. Matter protocol location metadata, if present, is not the authority for EliteSCADA Location unless explicitly imported/mapped.

---

# 8. Capability readiness

Initial capability research priority:

| EliteSCADA capability | Matter source concept | Preliminary readiness |
|---|---|---|
| OnOff | On/Off cluster | GO |
| Light | device type + On/Off | GO |
| Dimmer | Level Control | GO |
| ColorLight | Color Control + device type | GO_WITH_FEATURE_DISCOVERY |
| Temperature | Temperature Measurement | GO |
| Humidity | Relative Humidity Measurement | GO |
| Occupancy | Occupancy Sensing | GO |
| Contact | Boolean State / device-type semantics | GO_WITH_DEVICE_TYPE_RULES |
| Power | Electrical Power Measurement | GO_WITH_CLUSTER_FEATURE_RULES |
| Energy | Electrical Energy Measurement | GO_WITH_CLUSTER_FEATURE_RULES |
| Cover | Window Covering | GO |
| Lock | Door Lock | GO_WITH_SECURITY/PIN_REDACTION_GATES |
| Thermostat/Climate | Thermostat | GO_WITH_FEATURE_RULES |
| Fan | Fan Control | GO_WITH_FEATURE_RULES |

Rules:

- inspect endpoint device type + Descriptor data;
- inspect cluster FeatureMap and revision;
- do not assume all optional features exist;
- do not map unknown/custom/vendor clusters into generic capabilities by name similarity;
- keep raw protocol diagnostics available separately.

---

# 9. Matter events are not persistent state

Matter events have occurrence semantics and event numbers; they are not persistent attribute state.

Examples include event-style interactions and device notifications that must retain:

- event identity;
- occurrence time/order;
- node/endpoint/cluster/event ID;
- optional event payload;
- urgency/priority where protocol supplies it.

Do not synthesize a permanent Boolean TAG that remains true because an event occurred.

If the EliteSCADA driver/runtime contract still lacks the required transient-event path:

**RESEARCH_CONTRACT_DELTA_REQUIRED — DRIVER-TRANSIENT-EVENT-01**

This delta was already identified by earlier HOME/BUILDING research and remains applicable.

For Checkpoint 1, event data should be:

- classified;
- testable;
- diagnosable;
- not forced into fake state.

---

# 10. Subscriptions, reconnect and ICD/sleepy devices

## 10.1 Subscriptions

Normal runtime should prefer Matter subscriptions/reports over polling every attribute per TAG.

The selected stack has recent work on:

- subscription persistence/re-establishment;
- resubscribe after device reboot/OTA;
- event reporting;
- backpressure/coalescing for slow WebSocket clients.

EliteSCADA must still maintain client-side bounded queues and latest-state coalescing where safe.

## 10.2 Reconnect

Reconnect policy should:

- reconnect the sidecar WebSocket independently from device sessions;
- allow the sidecar/controller to manage CASE/session recovery;
- back off after repeated failures;
- avoid issuing per-node full reads at once after process restart;
- stagger re-interview/resync;
- distinguish unavailable node from unavailable sidecar.

## 10.3 ICD / sleepy devices

matter.js and Matter(.js) Server now expose ICD-related functionality and management.

Battery/sleepy devices require different expectations:

- not continuously reachable;
- reports may arrive only in defined awake windows;
- controller can register as ICD monitor where supported;
- writes/commands may need an awake window;
- availability must not oscillate to fault simply because the node is sleeping;
- stale process values must be represented honestly;
- do not create reconnect storms.

An open upstream ICD recovery report remains a qualification item. Include at least one sleepy/ICD fixture in L4.

---

# 11. Certification, DCL and legal

## 11.1 Current public specification

CSA currently publishes public download entries for:

- Matter 1.6.1 Core Specification;
- Matter 1.6.1 Application Clusters Specification;
- Matter 1.6.1 Device Type Library;
- Matter 1.6.1 Standard Namespace.

This is ahead of the preferred stable Matter(.js) Server 1.4.0 stated support of Matter 1.6.0.

That version gap is manageable for engineering but must be explicit.

## 11.2 Certification is separate from open-source license

Apache-2.0 permission for matter.js, Matter(.js) Server source and connectedhomeip does **not** certify EliteSCADA.

CSA certification has separate:

- Alliance membership/process;
- authorized testing;
- PICS;
- final test report;
- Network Transport Attestation for Matter applications;
- Security Attestation for Matter applications;
- application/approval;
- certification declaration;
- DCL record;
- logo/trademark rules.

## 11.3 Software Component certification exists

CSA has a Software Component certification category and current certified controller/framework examples exist.

The previous Python Matter Server was certified; the current matter.js-based successor states that it has not yet been officially re-certified.

Therefore an EliteSCADA controller can use an uncertified open-source component during engineering, but no Matter Certified claim should be made until the final selected certification path is defined and completed.

## 11.4 Device attestation / DCL

The production commissioner must implement a clear policy for:

- PAA trust;
- device attestation;
- certification declaration/DCL validation as required by the stack/product policy;
- revocation/status handling;
- offline/temporarily unavailable DCL behavior.

## 11.5 Marketing/trademark

**LEGAL_REVIEW_REQUIRED**

Before using:

- Matter logo;
- Matter Certified wording;
- certification marks;
- claims implying CSA certification.

Open-source license and protocol interoperability are not sufficient authorization for those claims.

---

# 12. Hardware lab — Checkpoint 1 concrete recommendation

All procurement records must pin exact model, hardware/region variant, firmware and acquisition/test date.

## 12.1 Matter over Wi-Fi actuator

**TP-Link Tapo P125M**

Why:

- current vendor page states Matter Certified;
- Wi-Fi smart plug;
- simple OnOff command/readback target;
- isolates Matter-over-Wi-Fi from Thread dependencies.

Lab use:

- commission;
- OnOff;
- state subscription;
- restart/reconnect;
- factory reset/recommission;
- multi-admin if supported by fixture/firmware.

## 12.2 Matter over Thread actuator/router node

**Eve Energy** — select a Matter-enabled regional model appropriate to the lab mains voltage/socket.

Why:

- current vendor material identifies Eve Energy as Matter-enabled;
- Thread;
- mains-powered Thread router node;
- smart plug + power meter;
- exercises OnOff and metering plus Thread routing behavior.

Lab use:

- Thread commissioning;
- OnOff;
- power/energy if exposed by selected firmware/spec version;
- subscription;
- Border Router restart;
- fabric/controller restart.

## 12.3 Matter over Thread sleepy/contact sensor

**Aqara Door and Window Sensor P2 — DW-S02E / DW-S02D**

Why:

- vendor states native Matter;
- Thread + BLE;
- battery powered;
- explicit requirement for Matter-compatible Thread Border Router;
- suitable Contact/Boolean-state and sleepy-device behavior.

Lab use:

- BLE commissioning;
- Thread onboarding;
- contact state reports;
- sleep/availability;
- controller restart;
- subscription loss/recovery;
- multi-admin where supported.

## 12.4 External Thread Border Router

Primary external fixture:

**Google Nest Hub (2nd generation)**

Why:

- current Google documentation lists it as Matter-capable with integrated Thread Border Router;
- proves EliteSCADA can consume a third-party Thread network without owning an RCP.

Secondary controllable-lab option:

**Home Assistant Connect ZBT-2 + OpenThread Border Router on Home Assistant**

Caveat:

- ZBT-2 is a radio, not a standalone Border Router by itself;
- Home Assistant/OTBR software is required;
- current Home Assistant documentation describes the setup and also notes work-in-progress areas in its broader Thread integration.

Use the Google fixture first for independent external-TBR validation; use OTBR lab later when testing dataset/diagnostic control.

## 12.5 BLE host

At least one recent Linux x64 host with a supported Bluetooth adapter exposed to the Matter Server.

Also validate BLE proxy mode as a separate topology.

Do not declare a Windows BLE commissioning matrix supported while the upstream Matter Server itself marks Windows unsupported.

## 12.6 Multi-admin fixture

Use at least the Eve Energy or Aqara P2 fixture in:

- EliteSCADA fabric;
- one independent ecosystem fabric.

Validate:

- open commissioning window;
- second-admin add;
- independent read/control;
- removal of EliteSCADA fabric without deleting other admin;
- recommissioning after factory reset.

---

# 13. Test model L0-L4

## L0 — pure contract / mapper tests

No real sidecar.

Cover:

- supported WebSocket schema negotiation;
- server version parsing/pinning;
- fabric/node/endpoint/cluster/attribute stable identity;
- feature/revision aware capability mapping;
- attribute -> TAG mapping;
- writable attribute/command -> approved Command mapping;
- event vs state distinction;
- Matter event identity/order model;
- QR/manual pairing input validation;
- secret redaction;
- Thread/Wi-Fi credential references;
- admin operation authorization classification;
- malformed/oversized sidecar payload rejection;
- bigint/node-id precision;
- unknown cluster/attribute passthrough diagnostics without fake capability.

## L1 — fake sidecar/controller

Scripted fake WebSocket server.

Cover:

- process startup/ready;
- incompatible schema;
- fabric ready;
- commission success;
- attestation failure;
- invalid pairing code;
- node connect;
- node unavailable;
- sidecar crash;
- restart/reconnect;
- persistent fabric state survives restart;
- subscription drop;
- subscription re-establish;
- event flood/backpressure;
- malformed event/data payload;
- Thread unavailable;
- BLE unavailable;
- duplicate/stale reports;
- sleepy node long silence;
- bounded reconnect;
- protected value never appears in logs/diagnostics;
- backup/restore metadata validation.

## L2 — independent real Matter reference implementation

Use the real selected Matter(.js) Server against an independent reference peer, not EliteSCADA code on both ends.

Preferred references:

- connectedhomeip chip-tool / reference virtual devices;
- independent test nodes from another Matter SDK where useful.

Cover:

- commission by QR/manual;
- PASE;
- attestation;
- operational CASE;
- endpoint/cluster discovery;
- read/write;
- invoke;
- subscribe/events;
- open commissioning window;
- multi-admin;
- remove fabric;
- sidecar restart;
- persistence/restore;
- Wi-Fi and Thread network credential flows where virtual/reference tooling allows.

## L3 — canonical EliteSCADA Runtime

Integrate only after common foundations are ready.

Verify:

- Data Source lifecycle;
- canonical Equipment identity independent from Matter node friendly names;
- Capability materialization;
- TAG read/subscription path;
- Command / Runtime.WriteAsync path;
- command readback where applicable;
- Historian/Alarm receives canonical TAG state;
- transient Matter event path when available;
- diagnostics ladder;
- driver restart;
- sidecar restart;
- project revision lifecycle does not contain Matter secrets.

## L4 — real hardware

Minimum matrix:

1. Tapo P125M Matter/Wi-Fi;
2. Eve Energy Matter/Thread actuator;
3. Aqara Door/Window Sensor P2 Thread/battery;
4. Nest Hub 2nd generation external Thread Border Router;
5. Linux BLE-capable controller host;
6. independent second Matter administrator/ecosystem.

Scenarios:

- first commission Wi-Fi;
- first commission Thread;
- QR and manual code;
- BLE bootstrap;
- read;
- write;
- subscriptions;
- transient events if fixture exposes them;
- controller restart;
- sidecar crash/restart;
- TBR restart;
- IP network interruption;
- device power cycle;
- sleepy sensor;
- multi-admin;
- fabric removal;
- factory reset/recommission;
- encrypted backup;
- restore on replacement controller host.

---

# 14. Scale/performance qualification targets

These are EliteSCADA qualification targets, not protocol limits.

Initial proposed stress profile:

- 100 commissioned nodes across synthetic/L2 fixtures;
- 1,000 subscribed attributes minimum;
- mixed Wi-Fi/Thread logical topology;
- 10% simultaneous reconnect event after sidecar/network restart;
- no unbounded WebSocket queue growth;
- bounded memory during event flood;
- staggered re-read/reconciliation;
- controller restart with fabric retained;
- restored controller with no mass recommissioning;
- repeated sleepy-device wake/report cycles;
- repeated schema reconnect;
- 24h and 72h soak tests after L2/L4 functionality passes.

Do not claim 100-node hardware support from this target. Real supported scale must be established empirically with exact versions and topology.

---

# 15. Exact blockers and gates

## GATE-MATTER-01 — sidecar maturity/certification

**Type:** stack / certification  
**State:** OPEN

The selected Matter(.js) Server successor remains Beta and says it has not yet been officially re-certified by CSA.

Required for production decision:

- Main explicitly accepts uncertified upstream component for initial non-certified product path; or
- upstream re-certification/maturity milestone lands and is revalidated.

This gate does not prevent engineering development.

## GATE-MATTER-02 — upstream security/resource fixes

**Type:** stack / security  
**State:** OPEN

Revalidate before production:

- #1092 PASE PBKDF2 pre-auth resource-exhaustion/DoS;
- #1039 CASE malformed maxPathsPerInvoke OOM;
- other security advisories/current issues.

Preferred release gate:

- stable upstream version containing fixes;
- security regression tests.

## GATE-MATTER-03 — Windows product topology

**Type:** platform  
**State:** MAIN_DECISION_REQUIRED

Official Matter Server support excludes Windows/WSL.

Options:

A. **Recommended first path:** Linux x64/ARM64 local/edge sidecar; Windows EliteSCADA connects only through an explicitly secured supported remote architecture if Main wants cross-host control.

B. Delay Windows Matter controller support until upstream supports it.

C. Fund a separate native/controller architecture (for example connectedhomeip integration) after a dedicated cost/security/certification review.

Do not silently claim Windows local support.

## GATE-MATTER-04 — sidecar lifecycle contract

**Type:** foundation  
**State:** REQUIRED

**RESEARCH_CONTRACT_DELTA_REQUIRED — SIDECAR-LIFECYCLE-01**

Need product contract for:

- process ownership;
- version pin;
- health;
- IPC;
- restart;
- crash loop;
- persistence;
- backup;
- upgrade/rollback;
- exclusive storage ownership;
- protected logs.

## GATE-MATTER-05 — protected material

**Type:** foundation/security  
**State:** REQUIRED

Need protected provisioning/reference path for:

- Thread datasets;
- Wi-Fi credentials;
- backup encryption;
- optional remote-sidecar auth.

Sidecar fabric keys remain opaque protected state.

Never persist secrets in project/revision payloads.

## GATE-MATTER-06 — transient event contract

**Type:** foundation  
**State:** REQUIRED IF FIRST RELEASE EXPOSES MATTER EVENTS

**RESEARCH_CONTRACT_DELTA_REQUIRED — DRIVER-TRANSIENT-EVENT-01**

Matter events must not become fake persistent TAG state.

## GATE-MATTER-07 — Thread network qualification

**Type:** network/hardware  
**State:** REQUIRED FOR THREAD SCOPE

Need proof of:

- existing TBR discovery/reachability;
- IPv6 routing/RIO;
- mDNS;
- Thread dataset/bootstrap;
- BLE commissioning;
- multi-admin;
- container topology.

No ThreadRadio/RCP Host Resource is required for the first external-TBR architecture.

## GATE-MATTER-08 — fabric DR

**Type:** persistence  
**State:** REQUIRED

Need:

- encrypted backup;
- compatible restore;
- replacement host;
- same fabric identity;
- node recovery without recommissioning.

## GATE-MATTER-09 — legal/certification

**Type:** legal  
**State:** LEGAL_REVIEW_REQUIRED

Before any Matter Certified/logo marketing:

- CSA membership/certification route;
- test provider;
- PICS;
- Network Transport Attestation;
- Security Attestation;
- certification declaration/DCL;
- trademark/logo rules.

## GATE-MATTER-10 — stable version pin vs Matter 1.6.1

**Type:** version compatibility  
**State:** OPEN

Current:

- CSA public spec: 1.6.1;
- connectedhomeip latest: v1.6.1.0;
- matter.js stable used by server: 0.17.9 / Matter 1.6.0;
- Matter(.js) Server stable: 1.4.0 / Matter 1.6.0;
- matter.js main/WIP: 1.6.1 alignment.

For first DEV, pin the 1.4.0/0.17.9 stack and explicitly define supported feature/spec window. Do not code against WIP 1.6.1 APIs until stable.

---

# 16. RESEARCH_FINDING / MAIN_DECISION_REQUIRED

## RF-MATTER-01 — Windows controller support

Evidence:

- upstream Matter(.js) Server officially supports recent 64-bit Linux/macOS;
- other OSes including WSL are unsupported.

Affected contract:

- future Matter sidecar packaging/platform support.

Options:

1. Linux-first controller appliance/sidecar;
2. wait for upstream Windows support;
3. design a different native stack.

Recommendation:

**Linux-first managed sidecar. Do not block Matter engineering on Windows-native support, but do not advertise Windows Matter controller support.**

Risk:

- Wave 16 installer/platform expectations may otherwise conflict with real upstream support.

## RF-MATTER-02 — production version security gate

Evidence:

- open PASE resource-exhaustion and CASE OOM issues are marked planned for next version.

Affected contract:

- sidecar version pin / release qualification.

Recommendation:

- permit isolated engineering on 1.4.0;
- require stable fixed upstream release or explicit security acceptance before production.

Risk:

- remotely reachable or untrusted Matter peers can exercise controller parsing/session paths.

## RF-MATTER-03 — certification boundary

Evidence:

- selected successor is Beta/not re-certified;
- certification is a CSA process separate from Apache-2.0 license.

Affected contract:

- packaging/marketing/release.

Recommendation:

- treat first DEV as protocol integration, not Matter Certified product;
- create certification work item only when commercial release scope is defined.

Risk:

- incorrect logo/certification claim;
- false assurance that upstream open-source licensing equals product certification.

---

# 17. Preliminary development sequencing after Main accepts gates

This research lane does not authorize implementation.

If Main later releases a DEV lane, the safest first slices are:

1. **Sidecar contract + Linux lifecycle + schema/health + protected storage**
   - no commissioning UI yet;
   - no Windows claim;
   - fake/L1 first.

2. **Matter/Wi-Fi commissioning and OnOff device**
   - Tapo P125M;
   - prove QR/manual, PASE, attestation, CASE, subscriptions, command/readback;
   - avoids Thread complexity.

3. **Matter/Thread using existing external TBR**
   - Eve Energy + Aqara P2 + Nest Hub 2;
   - add BLE bootstrap, Thread dataset/multi-admin and sleepy/ICD.

Only after those pass should Main consider:

- owned OTBR/RCP;
- richer capabilities;
- locks/access control;
- certification program.

---

# 18. Source ledger

All time-sensitive sources accessed **2026-10-06**.

## Matter(.js) Server

- repository/readme — Beta, not re-certified, Matter 1.6.0, architecture:
  https://github.com/matter-js/matterjs-server
- changelog — 1.4.0 release 2026-08-07, WebSocket schema 13:
  https://github.com/matter-js/matterjs-server/blob/main/CHANGELOG.md
- WebSocket schema changelog:
  https://github.com/matter-js/matterjs-server/blob/main/docs/websocket-api-schema-changelog.md
- CLI/security/listen/storage:
  https://github.com/matter-js/matterjs-server/blob/main/docs/cli.md
- OS/network requirements:
  https://github.com/matter-js/matterjs-server/blob/main/docs/os_requirements.md
- Docker:
  https://github.com/matter-js/matterjs-server/blob/main/docs/docker.md
- BLE proxy protocol/security:
  https://github.com/matter-js/matterjs-server/blob/main/docs/ble-proxy-protocol.md
- npm stable/dev tags:
  https://www.npmjs.com/package/matter-server
- current open issues:
  https://github.com/matter-js/matterjs-server/issues

## matter.js

- repository:
  https://github.com/matter-js/matter.js
- changelog — 0.17.9 2026-08-06; Matter 1.6.0 in 0.17.5; WIP 1.6.1:
  https://github.com/matter-js/matter.js/blob/main/CHANGELOG.md
- npm @matter/main:
  https://www.npmjs.com/package/@matter/main

## connectedhomeip

- releases — v1.6.1.0:
  https://github.com/project-chip/connectedhomeip/releases
- repository:
  https://github.com/project-chip/connectedhomeip
- license:
  https://github.com/project-chip/connectedhomeip/blob/master/LICENSE

## CSA

- current specification downloads — Matter 1.6.1:
  https://csa-iot.org/developer-resource/specifications-download-request/
- certification process / programs / DCL / logo:
  https://csa-iot.org/certification/why-certify/
- certification application artifacts:
  https://csa-iot.org/certification/tools/certification-tool/
- PAA providers:
  https://csa-iot.org/certification/paa/
- certified product directory examples:
  https://csa-iot.org/csa_product/1home-mcontroller/

## Thread / lab

- Google Matter/Thread hub/TBR list:
  https://support.google.com/googlehome/answer/12391458
- TP-Link Tapo P125M:
  https://www.tp-link.com/br/home-networking/smart-plug/tapo-p125m/
- Eve Energy:
  https://www.evehome.com/en-us/eve-energy
- Eve Matter:
  https://www.evehome.com/en-us/matter
- Aqara Door and Window Sensor P2:
  https://www.aqara.com/en/product/door-and-window-sensor-p2
- Aqara P2 specs:
  https://www.aqara.com/en/product/detail/specs?product_id=115
- Home Assistant Thread:
  https://www.home-assistant.io/integrations/thread/
- Home Assistant OTBR:
  https://www.home-assistant.io/integrations/otbr

---

# 19. Uncertainties to revalidate before any implementation release

1. whether Matter(.js) Server has left Beta and/or regained CSA Software Component certification;
2. whether a stable post-1.4.0 release has closed #1092/#1039 and related security/resource bugs;
3. current supported operating-system matrix, especially any Windows development;
4. current stable WebSocket schema and minimum supported schema;
5. current stable Matter specification level;
6. current storage migration/rollback compatibility;
7. exact firmware/certification state of purchased lab fixtures;
8. exact external Thread dataset-sharing behavior of chosen ecosystem/TBR;
9. whether first EliteSCADA Matter release needs transient events or can gate them;
10. whether commercial release intends to use Matter certification/logo.

---

# 20. Checkpoint 1 conclusion

**Preliminary decision: MATTER = GO_WITH_GATES**

The controller/server ecosystem is now mature enough to justify a future bounded implementation lane, especially on Linux and with an external Thread Border Router.

The recommended path is:

**EliteSCADA -> managed Matter(.js) Server sidecar -> Matter fabric -> Wi-Fi/Thread devices**

The first implementation must not:

- embed connectedhomeip merely to avoid sidecar governance;
- claim Windows local support;
- own a Thread RCP/OTBR by default;
- place fabric/network secrets in project data;
- expose commission/fabric/network mutation as ordinary HMI commands;
- treat Matter events as persistent TAG state;
- claim Matter certification/logo rights from Apache-2.0 licensing.

The first production release remains gated by sidecar maturity/security, platform scope, protected persistence/DR, L0-L4 evidence and legal/certification decisions.

## Next <=3 actions

1. Main reviews/accepts or changes **MATTER = GO_WITH_GATES**, especially the Linux-first/Windows gate.
2. On new **SIGA**, execute **CHECKPOINT 2 — Z-WAVE JS** only.
3. After another **SIGA**, compare both protocols in the common sidecar/Host Resource/lab/legal checkpoint.

---

**DOCS_ONLY**

**NO PRODUCT CODE CHANGED**

**NO DEPENDENCY CHANGED**

**NO CI CHANGED**

**NO HOST RESOURCE CODE CHANGED**

**NO MATTER CODE IMPLEMENTED**

**NO ZWAVE CODE IMPLEMENTED**

**NO MERGE PERFORMED**
