# Native Zigbee Execution Research — Checkpoint 2

Status: RESEARCH_ONLY / DOCS_ONLY / NO_PRODUCT_CODE / NO_MERGE_BY_RESEARCHER

Issue owner: #541 — HOME-RESEARCH-02 — Zigbee2MQTT + Native Zigbee execution dossier

Contract: C-HOME-ZIGBEE-RESEARCH-01

Order: HOME-ZIGBEE-Z2M-NATIVE-RESEARCH-01

Research branch: research/home-zigbee-execution

Release base:
wave15/corrections-integration@69d5248e462ef12c41d99c6859a3afb77c6e3e2d

Checkpoint 1 HEAD:
ee0044302ce294b09a7cc5abc9fe562ca8b1269a

Access/revalidation date: 2026-10-06

This document is CHECKPOINT 2 only. It researches the Native Zigbee execution path. It does not authorize product implementation, dependency changes, CI changes, packaging changes, or a merge.

---

## 1. Preliminary decision

### Native Zigbee

PRELIMINARY NATIVE ZIGBEE = GO_WITH_GATES

The evidence supports a mature Native Zigbee path provided EliteSCADA does not reimplement the Zigbee stack, ZCL, coordinator protocols, or the device quirk ecosystem from scratch.

Preferred architecture:

EliteSCADA canonical Runtime/Product authority
-> managed LocalBridge / zigbee.coordinator sidecar
-> ZigbeeCoordinator Host Resource lease
-> pinned zigbee-herdsman
-> physical coordinator
-> Zigbee mesh

Device semantic/quirk layer inside the sidecar:

pinned zigbee-herdsman-converters

EliteSCADA remains the authority for:
- configured Data Source;
- Equipment;
- Capability;
- TAG;
- Command;
- Location;
- canonical Runtime writes and quality;
- diagnostics;
- protected-material policy;
- commissioning authorization/audit;
- Host Resource ownership.

The sidecar is a technical execution mechanism. It is not a second product authority.

---

## 2. Current upstream baseline

Upstream was revalidated from the official GitHub repositories on 2026-10-06.

### 2.1 zigbee-herdsman

Current official release observed:
- package: zigbee-herdsman
- version: 11.0.0
- release date: 2026-10-06
- release target commit: 1cce856563bac951b0c710d65d4c0c58ba46be5f
- license: MIT

Important current release fact:
- 11.0.0 contains a BREAKING CHANGE named unified transport.

Current package metadata also shows:
- Node engine: ^22.2.0 || ^24 || <=26.2
- @serialport/bindings-cpp dependency
- @serialport/stream dependency
- bonjour-service
- zigbee-on-host

The same-day major release is evidence of active maintenance, but it also means 11.0.0 must not be adopted as unqualified "latest".

Research recommendation:

CANDIDATE_BASELINE_HERDSMAN = 11.0.0

with:

QUALIFICATION_REQUIRED_BEFORE_PRODUCT_PIN

A future DEV must pin an exact version and run the Native Zigbee L0-L4 matrix before release.

### 2.2 zigbee-herdsman-converters

Current official release observed:
- package: zigbee-herdsman-converters
- version: 26.117.1
- release date: 2026-10-06
- release target commit: 56e5638e7c3249be0b8999bb050610e0d94db419
- license: MIT

Current package metadata shows:
- Node engine: ^22.2.0 || ^24 || <=26.2
- zigbee-herdsman dependency: ^11.0.0

Research recommendation:

CANDIDATE_BASELINE_CONVERTERS = 26.117.1

with exact version pinning.

### 2.3 Update cadence implication

Both upstream packages released new versions on the same date as this research.

This is not a reason to reject the stack.

It is a reason to:
- pin exact versions;
- record both versions in every compatibility result;
- test upgrades before promotion;
- never perform runtime "install latest";
- maintain rollback-compatible sidecar images/artifacts.

---

## 3. Why a sidecar instead of Native Zigbee in C#

Native Zigbee requires more than serial I/O.

A production stack must handle:
- adapter/coordinator protocol;
- ZDO;
- ZCL;
- endpoints and clusters;
- reads and writes;
- attribute reports;
- binding;
- reporting configuration;
- join/interview lifecycle;
- security;
- coordinator backup;
- device quirks;
- vendor-specific clusters;
- sleepy devices;
- retry/timing behavior;
- network database;
- coordinator-family differences.

zigbee-herdsman already implements these concerns and supports multiple adapter families.

zigbee-herdsman-converters adds the device definition and vendor quirk layer.

Therefore the recommended Native path is:

MANAGED_SIDECAR

and not:
- full C# Zigbee stack;
- copied Zigbee2MQTT core;
- copied vendor coordinator SDKs into Runtime;
- generic raw ZCL exposure to scripts/UI.

---

## 4. Native architecture

### 4.1 Product boundary

EliteSCADA:

Data Source / Equipment / Capability / TAG / Command
-> canonical Runtime
-> Native Zigbee Driver/Bridge client
-> versioned sidecar RPC

Sidecar:

RPC contract
-> converter/semantic execution
-> zigbee-herdsman
-> ZigbeeCoordinator lease
-> coordinator
-> mesh

### 4.2 Data Source

Recommended model:

one Native Zigbee Data Source
=
one owned Zigbee network / one coordinator execution context.

The Data Source owns product-level configuration and references.

It does not expose low-level herdsman objects directly.

### 4.3 Equipment

One selected physical Zigbee device becomes one canonical Equipment.

Stable source identity:
DataSourceId + IEEE address

Endpoint identity remains part of child binding where required.

### 4.4 TAG / Command / Capability

Stateful attributes and curated converter exposes project into canonical TAG/Capability.

Writable state projects through canonical Runtime write authority.

Set-only operations become Commands.

Transient actions remain transient events and do not become fake persistent state.

---

## 5. Sidecar contract

The sidecar contract must be versioned and transport-neutral.

Do not make product semantics depend on Node object shape.

### 5.1 Mandatory handshake

At minimum expose:

- contract version;
- sidecar build/version;
- Node runtime version;
- zigbee-herdsman version;
- zigbee-herdsman-converters version;
- startup state;
- readiness state;
- supported adapter families;
- enabled optional features;
- Host Resource lease identity;
- coordinator connected/disconnected state.

Sidecar/EliteSCADA version mismatch must fail with a clear compatibility error.

Do not silently downgrade contract semantics.

### 5.2 Health

Health response should distinguish:

- Starting
- Ready
- Degraded
- CoordinatorDisconnected
- NetworkUnavailable
- Faulted
- Stopping

Health must include timestamp/freshness.

The sidecar should not report Ready before:
- Host Resource lease is acquired;
- coordinator is connected;
- network identity is validated;
- persistent database is opened;
- converter dataset is loaded.

### 5.3 Coordinator identity

Expose:

- HostResourceId;
- adapter family;
- coordinator IEEE/EUI64;
- model/fingerprint when known;
- firmware/adapter version;
- transport type;
- sanitized transport endpoint;
- connected state.

Do not expose network keys or link keys.

### 5.4 Network identity

Expose non-secret identity/diagnostic metadata:

- PAN ID;
- extended PAN ID;
- channel;
- network update ID where available;
- network ready state.

Do not expose:
- network key;
- Trust Center link keys;
- install codes.

### 5.5 Device inventory

Expose per device:

- IEEE address;
- network address as transient metadata;
- type/role where known;
- interview state;
- manufacturer name;
- model ID;
- power source;
- endpoints;
- supported/converter mapping status;
- converter definition identity/version;
- last seen;
- availability/health evidence.

Network address is not canonical identity.

### 5.6 Endpoint/cluster/attribute metadata

Expose enough normalized metadata for Engineering diagnostics and curated mapping:

- endpoint ID;
- profile ID;
- device ID;
- input clusters;
- output clusters;
- known attributes;
- current converter/expose metadata.

Do not make raw cluster/attribute access a normal user-facing Capability automatically.

### 5.7 Reports/event stream

Provide a subscription stream carrying normalized messages:

- attribute reports;
- read responses;
- device announcements;
- join/interview state;
- leave;
- adapter disconnect/reconnect;
- converter semantic state;
- transient semantic events.

Every stream item needs:
- sequence or monotonic stream identity;
- timestamp;
- device IEEE;
- endpoint when applicable;
- semantic/property identity;
- event kind.

Stream reconnection must support a clear resync path.

Do not assume every event can be replayed.

### 5.8 Reads

Provide bounded read operations:

- device IEEE;
- endpoint;
- cluster;
- attribute(s);
- timeout;
- optional manufacturer code/context only through curated/internal mapping.

Normal product reads should use the semantic converter path when possible.

Raw ZCL read should be Engineering/internal-only if exposed at all.

### 5.9 Writes

Provide bounded write/command operations:

- stable device identity;
- endpoint;
- semantic property/command;
- typed value;
- timeout;
- correlation ID.

A successful sidecar enqueue is not process truth.

Confirmation follows:
- device response;
- readback;
- later authoritative report;
- converter-defined state reconciliation.

Requested value must not become TAG Good solely because a sidecar call succeeded.

### 5.10 Configure reporting

The underlying stack exposes configureReporting.

Product disposition:

PROVISIONING / ADMIN-OWNED

The sidecar may automatically configure reporting as part of a reviewed device definition.

Do not expose arbitrary reporting configuration as a normal HMI command.

### 5.11 Bind / unbind

The underlying stack supports endpoint bind/unbind.

Product disposition:

ADMIN / PROVISIONING

Bindings may be applied by the converter/device setup lifecycle.

Do not expose bind/unbind as ordinary Runtime process Commands.

### 5.12 Permit join

The stack exposes permitJoin.

Product disposition:

PRIVILEGED COMMISSIONING

Requirements:
- closed by default;
- bounded duration;
- explicit admin authorization;
- audit actor and duration;
- clear UI countdown/state;
- auto-close on timeout;
- close on sidecar stop/restart;
- no "always permit join" normal Runtime state.

### 5.13 Install code

The stack exposes install-code handling on supported adapter families.

Product disposition:

PRIVILEGED COMMISSIONING / PROTECTED MATERIAL

Install codes must never be logged or persisted in ordinary project JSON.

### 5.14 Interview / reinterview

Expose:
- start/retry interview;
- current interview state;
- sanitized failure reason.

Product disposition:

ADMIN / COMMISSIONING

Interview alone is not compatibility proof.

### 5.15 Remove device

Expose bounded removal with:
- target IEEE;
- explicit confirmation;
- force semantics only if contract proves meaning;
- timeout;
- result and sanitized reason.

Product disposition:

DESTRUCTIVE ADMIN

Do not make device removal an HMI Command.

### 5.16 Network backup

Expose:
- backup capability supported/not supported;
- create backup;
- backup metadata;
- compatibility metadata.

The backup itself is protected material/state.

### 5.17 Network restore

Expose only through a privileged lifecycle operation.

Restore is never a live HMI command.

Required gates:
- sidecar quiesced;
- coordinator lease exclusively held;
- expected adapter family checked;
- backup schema/version checked;
- protected material available;
- explicit admin authorization;
- rollback/recovery plan.

### 5.18 Diagnostics

Expose:

- sidecar version/health;
- coordinator connection state;
- adapter family/firmware;
- PAN/channel;
- number of known/interviewed devices;
- report/read/write counters;
- queue depth;
- retries/timeouts;
- unsupported clusters;
- converter mapping status;
- last successful coordinator communication;
- last error sanitized;
- event stream health.

Reuse #500 common diagnostics instead of inventing a second health authority.

---

## 6. Runtime versus commissioning

This separation is mandatory.

### 6.1 Normal Runtime

Allowed normal Runtime concerns:

- consume attribute reports;
- consume semantic state;
- point read;
- process writes;
- process command execution;
- quality/freshness;
- availability;
- diagnostics;
- reconnect/reconciliation.

### 6.2 Provisioning/admin

Provisioning/admin concerns:

- permit join;
- install code;
- accept/reject joining device;
- interview/reinterview;
- remove device;
- bind/unbind;
- configure reporting;
- reset device/network;
- channel/network mutation;
- backup;
- restore;
- coordinator migration/replacement;
- network-key changes;
- firmware/OTA administration.

These operations require higher authorization, audit, bounded execution, and recovery semantics.

### 6.3 No destructive HMI commands

Normal HMI/scripts must not receive raw methods for:
- permit join indefinitely;
- erase coordinator;
- reset network;
- remove device;
- change network key;
- restore backup;
- replace coordinator.

---

## 7. Stable identity

### 7.1 Physical device

Primary:
IEEE address / EUI64.

Recommended product binding:
DataSourceId + IEEE address

### 7.2 Endpoint identity

Child state may require:
IEEE address + endpoint + semantic property

### 7.3 Network address

The 16-bit Zigbee network address is transient and must not be persisted as product identity.

### 7.4 Coordinator identity

The coordinator itself requires independent Host Resource identity.

Do not use:
- serial path alone;
- TCP host:port alone;
- display name alone

as the canonical configured identity.

The connected coordinator IEEE/fingerprint is runtime evidence used to validate the configured Host Resource.

---

## 8. Coordinator-family ranking

The current candidate stack supports multiple adapters, but Native v1 should deliberately narrow the qualified set.

### PRIMARY_COORDINATOR_FAMILY

TI zStack / CC2652-class, including current CC2652P / CC2652P7-class coordinators.

Reasons:
- mature first-class zigbee-herdsman z-stack adapter;
- current stack supports backup;
- broad current hardware availability;
- USB and network/TCP coordinator choices exist;
- exact current herdsman discovery includes common devices;
- strong value as the primary repeatable lab family.

Concrete current candidates recognized/documented in the ecosystem:
- SONOFF ZBDongle-P — CC2652P USB;
- SMLIGHT SLZB-07p7 — CC2652P7 USB;
- SMLIGHT SLZB-06 — CC2652P network/USB family.

Research preference for first native lab:
- modern USB TI coordinator as the baseline;
- one wired Ethernet TI coordinator as optional remote-resource coverage.

No claim is made that every CC2652/CC1352 adapter is equivalent.

### SECONDARY_COORDINATOR_FAMILY

Silicon Labs Ember / EmberZNet, EFR32MG21 / EFR32MG24-class.

Reasons:
- first-class current zigbee-herdsman Ember adapter;
- current stack supports backup;
- broad ecosystem availability;
- cross-family qualification is valuable;
- exact current herdsman discovery includes common Ember coordinators.

Concrete current candidates:
- SONOFF ZBDongle-E V2 — EFR32MG21 USB;
- Home Assistant Connect ZBT-1 — recognized by current herdsman discovery;
- Home Assistant Connect ZBT-2 — recognized by current herdsman discovery;
- SMLIGHT MG21/MG24 network/USB variants documented by Zigbee2MQTT.

Research preference for secondary lab:
- USB EFR32MG21 baseline first;
- MG24/network variants after baseline.

### Other adapter families

The current herdsman tree contains additional adapters such as:
- deCONZ;
- ZBOSS;
- ZiGate;
- Zigbee-on-host.

They are not recommended as first Native EliteSCADA qualification families.

Reason:
the product needs a bounded support statement, not the full upstream adapter matrix.

---

## 9. USB, serial and TCP transport

Current zigbee-herdsman 11.0.0 introduced a unified transport and current source supports raw serial or TCP transport.

Therefore the Host Resource should model a coordinator endpoint, not only a serial port.

### 9.1 USB/serial

Use the existing server-side host serial foundation from #469 where possible.

Requirements:
- server-visible port;
- Windows COM support;
- Linux device path support;
- manual configured fallback;
- physical presence not required merely to save Engineering;
- actual activation acquires exclusive lease.

### 9.2 Linux

Prefer stable device identities such as /dev/serial/by-id when available.

Do not assume /dev/ttyUSB0 is stable across reconnect/reboot.

### 9.3 Windows

Use host serial catalog/COM naming.

Do not involve browser-local serial APIs.

### 9.4 Container

Only devices explicitly passed through to the EliteSCADA/sidecar container are visible.

Persistent sidecar state must be mounted separately from ephemeral container image layers.

### 9.5 TCP coordinators

Support a configured TCP endpoint under the same ZigbeeCoordinator abstraction.

Requirements:
- exclusive endpoint lease;
- bounded connect/reconnect;
- wired Ethernet preferred for remote coordinators;
- never auto-scan arbitrary networks;
- do not silently switch to another discovered coordinator.

Wi-Fi transport should not be a qualification baseline because packet loss/latency can destabilize coordinator traffic.

---

## 10. Coordinator backup and replacement

### 10.1 Stack capability

Current herdsman exposes coordinator backup capability.

Current z-stack and Ember adapters report backup support.

The current unified backup model includes:
- coordinator IEEE;
- PAN ID;
- extended PAN ID;
- channel;
- network update ID;
- network key;
- frame counter;
- per-device link keys when available.

Therefore coordinator backup is SECURITY-SENSITIVE MATERIAL.

### 10.2 Restore scope

First Native release should qualify:

SAME_FAMILY / SAME_STACK_RESTORE_FIRST

Examples:
- zStack -> qualified zStack replacement;
- Ember -> qualified Ember replacement.

Cross-family:
zStack <-> Ember

must not be promised as seamless.

Existing ecosystem guidance explicitly warns that cross-family migration results vary and re-pairing may be required.

### 10.3 Backup compatibility metadata

Every backup must record at least:

- backup schema/version;
- created timestamp;
- sidecar version;
- herdsman version;
- adapter family;
- coordinator model;
- coordinator IEEE;
- firmware version;
- network identity;
- converter version for operational audit.

Do not store only an opaque blob with no compatibility evidence.

---

## 11. Native security model

### 11.1 Trust Center

Native EliteSCADA owns the network lifecycle through the managed sidecar.

The sidecar/stack executes Trust Center mechanics.

EliteSCADA owns:
- authorization;
- protected material;
- commissioning policy;
- audit;
- backup/restore policy.

### 11.2 Network key

The Zigbee network key is 128-bit security material.

Requirement:
- generate a cryptographically random 16-byte key for a new network;
- never rely on the upstream library's historical default fallback key;
- store through canonical Protected Material Authority;
- inject only into trusted sidecar lifecycle;
- never log;
- never expose to browser;
- never export plaintext in .escadapkg.

### 11.3 Install codes

Install codes:
- are commissioning secrets;
- should be accepted only in privileged admin flow;
- stored/provisioned through Protected Material Authority when persistence is needed;
- should be deleted/retired according to device onboarding lifecycle;
- never shown in normal diagnostics.

### 11.4 Link keys

Device Trust Center link keys may exist in coordinator/backup state.

They are protected state.

Do not surface them as normal configuration.

### 11.5 Permit join

Default:
CLOSED

Opening join:
- explicit privileged action;
- bounded timer;
- audit;
- optional target-device policy/passlist when productized;
- always close after timeout/restart.

### 11.6 Device removal

Privileged destructive operation.

Do not assume a remote device always honors leave/removal.

After removal, product state must distinguish:
- removed from canonical project;
- removed from mesh;
- leave request failed/unknown.

### 11.7 Network-key rotation

No generic production-safe, adapter-independent online rotation contract was established by this checkpoint.

Disposition:

ADMIN_FUTURE_SCOPE / WAIT_FOR_STACK_AND_LAB_PROOF

Do not advertise routine network-key rotation until exact adapter behavior, device compatibility, rollback, and recovery are proven.

### 11.8 Coordinator replacement

Requires:
- protected backup;
- adapter compatibility check;
- exclusive Host Resource lease;
- sidecar quiesce;
- expected coordinator identity validation;
- restore;
- post-restore device validation;
- rollback plan.

Cross-family migration remains qualification-gated.

---

## 12. Secret ownership in EliteSCADA

Current EliteSCADA already has an integrated generic Protected Material Authority from #497/#499.

Native Zigbee should consume it.

Do not add another Zigbee-specific vault.

### Protected Material candidates

At minimum:
- Zigbee network key;
- install code material;
- coordinator/network backup payload or backup encryption key;
- other stack security material that must leave sidecar memory.

### Non-secret configured metadata

May include:
- HostResourceId;
- adapter family;
- serial/TCP endpoint reference;
- coordinator model;
- expected coordinator IEEE;
- channel/PAN display metadata;
- sidecar version pin.

### Package/export rule

Project/package export must not contain:
- network key;
- install codes;
- link keys;
- raw protected coordinator backup;
- host-specific protected-material reference if current package contract intentionally strips deployment references.

Cross-host restore requires deliberate protected-material provisioning.

---

## 13. Converter/quirk strategy

This is a core Native Zigbee dependency.

### 13.1 Why converters are required

Real Zigbee devices are not fully described by generic cluster names alone.

Production support requires:
- model fingerprinting;
- endpoint quirks;
- vendor-specific clusters;
- custom scaling;
- command peculiarities;
- configure/reporting setup;
- semantic exposes;
- device-specific workarounds.

zigbee-herdsman-converters is the current mature dataset used for this layer.

### 13.2 License

Current zigbee-herdsman-converters license:
MIT.

Open-source permission does not mean every device/model has been qualified by EliteSCADA.

### 13.3 Recommended strategy

PINNED_CONVERTER_DATASET

The sidecar package must contain an exact converter version.

Do not:
- download latest converters at Runtime startup;
- mutate converter dataset without deployment/version change;
- claim all upstream devices are EliteSCADA supported.

### 13.4 Compatibility record

Every validated device must record:
- manufacturer;
- model;
- Zigbee model ID;
- firmware if available;
- coordinator family/model;
- sidecar version;
- herdsman version;
- converter version;
- semantic capabilities tested;
- read;
- write;
- reporting;
- reconnect;
- restart;
- test date.

### 13.5 Unsupported devices

If no converter definition matches:
- identify device as unsupported/unmapped;
- expose safe raw metadata for Engineering diagnostics;
- do not invent known Capability mapping;
- do not fail the entire network.

### 13.6 External/custom converters

Arbitrary external converters execute JavaScript inside the Node process.

This is effectively code execution.

Production recommendation:

ARBITRARY_EXTERNAL_CONVERTERS = DISABLED

Future custom strategy:
- reviewed source;
- pinned/versioned artifact;
- explicit admin/developer workflow;
- security review;
- preferably upstreamed to zigbee-herdsman-converters;
- no project-uploaded arbitrary JavaScript executed automatically.

### 13.7 Updates

Converter update procedure should be:

1. select exact candidate herdsman + converters pair;
2. build immutable sidecar artifact;
3. run L0 mapping regression;
4. run L1 fake-sidecar/fixtures;
5. run L2 stack integration;
6. run L4 representative device matrix;
7. review changed device definitions/quirks;
8. promote version;
9. retain rollback artifact.

---

## 14. Device semantic mapping

The Native path should reuse the same canonical vocabulary as Zigbee2MQTT.

Priority:
- OnOff;
- Light;
- Dimmer;
- ColorLight;
- Temperature;
- Humidity;
- Occupancy;
- Illuminance;
- Contact;
- Leak;
- Smoke;
- Battery;
- Power;
- Energy;
- Voltage;
- Current;
- Cover;
- Lock;
- Climate;
- Fan.

Mapping must be based on:
- converter definition;
- expose/semantic feature;
- endpoint;
- access/read/write/reporting;
- unit/range;
- category/diagnostic meaning.

Do not map based only on a string label.

### Raw clusters

Vendor clusters remain implementation/diagnostic detail until a curated semantic mapping exists.

### LQI/topology

LQI and route information are diagnostics.

They are not process Capabilities.

---

## 15. Transient events

Button/remotes/scene controllers frequently produce event-like actions.

Examples:
- single;
- double;
- hold;
- release;
- move/step;
- scene activation.

Disposition:

TRANSIENT_EVENT

Do not persist the last action as a fake continuously-valid TAG.

Native Zigbee inherits:

RESEARCH_CONTRACT_DELTA_REQUIRED — DRIVER-TRANSIENT-EVENT-01

Until a canonical event path exists:
- show event capability in discovery;
- allow diagnostics/raw event observation;
- exclude it from persistent TAG materialization.

---

## 16. Sidecar persistence

Persistent sidecar state includes:
- herdsman device database;
- network metadata;
- converter/device mapping state required for restart;
- coordinator backup metadata;
- operational configuration.

Requirements:
- durable host volume;
- atomic writes where possible;
- backup before destructive migration;
- versioned schema;
- compatibility check on upgrade;
- no plaintext protected material in logs;
- no Runtime download/install of packages.

Project configuration is not a replacement for sidecar operational state.

---

## 17. Safe lifecycle

### 17.1 Start

Required order:

1. load immutable sidecar build;
2. validate contract version;
3. obtain ZigbeeCoordinator Host Resource exclusive lease;
4. resolve protected network material;
5. open persistent sidecar state;
6. open coordinator transport;
7. identify coordinator;
8. validate adapter family and expected identity;
9. validate/restore expected network state;
10. load converter dataset;
11. start report/event stream;
12. publish Ready.

### 17.2 Unexpected coordinator

If the physical coordinator does not match expected configured/runtime identity:

FAIL CLOSED

Do not:
- automatically form a new network;
- erase the coordinator;
- silently adopt another mesh.

Require admin resolution.

### 17.3 Reconnect

On transient disconnect:
- keep canonical identities;
- mark source degraded/unavailable;
- stop confirming writes;
- retry with bounded backoff;
- reacquire/verify transport;
- revalidate coordinator/network identity;
- reconcile state;
- only then return Ready.

Do not factory-reset on reconnect.

### 17.4 Stop

Recommended order:
- close permit join;
- reject new commissioning operations;
- quiesce new writes;
- drain/cancel bounded in-flight requests;
- persist database;
- create backup when policy requires;
- close coordinator;
- zero transient protected material;
- release Host Resource lease.

### 17.5 Crash

Supervisor should:
- reclaim stale resource lease;
- preserve durable state;
- restart within crash-loop policy;
- never reset coordinator merely because the process crashed.

---

## 18. Common LocalBridge lifecycle gap

A generic managed-sidecar contract is still required.

Native Zigbee needs:
- exact executable/image version;
- start/stop/restart;
- readiness;
- crash-loop handling;
- local endpoint allocation;
- protocol/schema negotiation;
- persistent state volume;
- sanitized log collection;
- protected material injection;
- host resource lease handoff;
- update/rollback;
- Windows/Linux/container lifecycle.

Record:

RESEARCH_CONTRACT_DELTA_REQUIRED — SIDECAR-LIFECYCLE-01

This checkpoint specifies the needed behavior.

It does not implement it.

---

## 19. Host Resource gap

The current product has reusable Host Serial infrastructure from #469.

Native Zigbee needs a stronger abstraction:
ZigbeeCoordinator.

It can use serial or TCP transport and owns more than a byte stream.

Record:

RESEARCH_CONTRACT_DELTA_REQUIRED — HOST-RESOURCE-GENERALIZATION-01

Detailed contract is in:
docs/research/home-building-drivers/ZIGBEE-COORDINATOR-HOST-RESOURCE.md

---

## 20. Diagnostics convergence

Reuse #500.

Native diagnostic ladder:

Host / sidecar supervisor
-> ZigbeeCoordinator Host Resource
-> coordinator transport
-> Zigbee network ready
-> device known/interviewed
-> converter supported
-> point read/write/report
-> canonical live TAG

Important distinction:
- coordinator reachable does not mean device reachable;
- device interview success does not prove all capabilities;
- converter match does not prove EliteSCADA compatibility;
- command dispatch does not prove process state.

---

## 21. OTA

The underlying ecosystem supports OTA for some devices through vendor/provider metadata.

Checkpoint 2 disposition:

ADMIN_FUTURE_SCOPE

Do not make OTA a normal Runtime/HMI process.

Reasons:
- firmware provenance;
- vendor variability;
- long-running operation;
- power-loss risk;
- sleepy devices;
- network load;
- rollback limitations.

OTA must be separately researched/qualified in later scope.

---

## 22. Known blockers / gates before Native implementation release

### Gate A — managed sidecar foundation

SIDECAR-LIFECYCLE-01

Must provide supervised, pinned, persistent, upgradeable LocalBridge lifecycle.

### Gate B — ZigbeeCoordinator Host Resource

HOST-RESOURCE-GENERALIZATION-01

Must provide stable identity + exclusive lease for serial/TCP coordinators.

### Gate C — exact stack pin qualification

Current candidate pair:
- zigbee-herdsman 11.0.0;
- zigbee-herdsman-converters 26.117.1.

Both released 2026-10-06.

The 11.0.0 transport breaking change must be validated before product pin.

### Gate D — protected network state

Network key/install codes/backup must use the existing Protected Material Authority or an approved protected state seam.

No plaintext in package/logs.

### Gate E — transient event path

DRIVER-TRANSIENT-EVENT-01

Without it, remotes/buttons remain excluded from persistent TAG materialization.

### Gate F — hardware qualification

At least TI primary + Ember secondary coordinators and representative devices must pass L4.

Exact lab matrix is Checkpoint 3.

### Gate G — backup/restore proof

Same-family restore must be physically proven before coordinator replacement is marketed as supported.

Cross-family migration remains non-guaranteed.

---

## 23. Preliminary implementation sequence

After all foundation gates are accepted:

1. SIDECAR-LIFECYCLE-01 foundation.
2. ZigbeeCoordinator Host Resource.
3. Native sidecar skeleton with pinned Node/herdsman/converters.
4. TI zStack baseline.
5. semantic device inventory + selected import.
6. state/report/read/write.
7. commissioning/admin boundary.
8. protected backup/restore.
9. Silicon Labs Ember secondary family.
10. device compatibility matrix.
11. transient events only after canonical event contract.

Do not start by adding every coordinator family.

---

## 24. Preliminary GO_WITH_GATES rationale

Reasons to proceed:
- mature maintained upstream stack;
- permissive MIT licenses for herdsman and converters;
- active releases;
- serial and TCP transport support;
- multiple proven coordinator families;
- device quirk ecosystem already exists;
- backup support exists for the two recommended families;
- current EliteSCADA canonical Equipment/Capability and Protected Material foundations can be reused.

Reasons not to mark unconditional GO:
- sidecar lifecycle is not yet a canonical common product contract;
- ZigbeeCoordinator Host Resource must be defined/implemented;
- current herdsman major release is same-day and contains a breaking transport change;
- converter dataset is fast-moving;
- physical-device support requires explicit qualification;
- backup/restore portability is not universal;
- transient events lack canonical process event semantics;
- network mutation needs privileged commissioning authority.

---

## 25. Official sources

All sources accessed/revalidated on 2026-10-06 unless otherwise noted.

### zigbee-herdsman

- https://github.com/Koenkk/zigbee-herdsman/releases/tag/v11.0.0
  - current release 11.0.0;
  - 2026-10-06;
  - unified transport breaking change.

- https://github.com/Koenkk/zigbee-herdsman/blob/master/package.json
  - version/license;
  - Node engine;
  - serialport dependencies.

- https://github.com/Koenkk/zigbee-herdsman/blob/master/LICENSE
  - MIT.

- https://github.com/Koenkk/zigbee-herdsman/tree/master/src/adapter
  - z-stack;
  - ember;
  - other adapter implementations.

- https://github.com/Koenkk/zigbee-herdsman/blob/master/src/adapter/transport.ts
  - shared raw serial/TCP transport.

- https://github.com/Koenkk/zigbee-herdsman/blob/master/src/controller/controller.ts
  - events;
  - device inventory;
  - permit join;
  - install code;
  - backup;
  - network lifecycle.

- https://github.com/Koenkk/zigbee-herdsman/blob/master/src/controller/model/endpoint.ts
  - read/write;
  - bind/unbind;
  - configure reporting.

- https://github.com/Koenkk/zigbee-herdsman/blob/master/src/models/backup-storage-unified.ts
  - unified backup schema;
  - network key/link key content.

- https://github.com/Koenkk/zigbee-herdsman/blob/master/src/adapter/adapterDiscovery.ts
  - current hardware discovery fingerprints.

### zigbee-herdsman-converters

- https://github.com/Koenkk/zigbee-herdsman-converters/releases/tag/v26.117.1
  - current release 26.117.1;
  - 2026-10-06.

- https://github.com/Koenkk/zigbee-herdsman-converters/blob/master/package.json
  - version/license;
  - Node engine;
  - herdsman dependency.

- https://github.com/Koenkk/zigbee-herdsman-converters/blob/master/LICENSE
  - MIT.

- https://github.com/Koenkk/zigbee-herdsman-converters/tree/master/src/devices
  - device definitions/quirk dataset.

### Adapter ecosystem documentation

- https://www.zigbee2mqtt.io/guide/adapters/
  - current recommended adapter families;
  - serial/network guidance;
  - coordinator backup support.

- https://www.zigbee2mqtt.io/guide/adapters/zstack.html
  - TI zStack hardware examples.

- https://www.zigbee2mqtt.io/guide/adapters/emberznet.html
  - Silicon Labs Ember hardware examples and firmware guidance.

- https://www.zigbee2mqtt.io/guide/faq/
  - coordinator migration limitations;
  - cross-family caution.

### Hardware vendor examples

- https://sonoff.tech/products/sonoff-zigbee-3-0-usb-dongle-plus-zbdongle-p
  - ZBDongle-P / CC2652P family.

- https://sonoff.tech/products/sonoff-zigbee-3-0-usb-dongle-plus-zbdongle-e
  - ZBDongle-E / EFR32MG21 family.

---

## 26. Checkpoint 2 conclusion

Preliminary Native status:

GO_WITH_GATES

Architecture:

MANAGED_ZIGBEE_SIDECAR

Stack candidate:

zigbee-herdsman 11.0.0
+
zigbee-herdsman-converters 26.117.1

License:

MIT
+
MIT

Update policy:

PINNED_STACK
+
PINNED_CONVERTER_DATASET
+
NO_RUNTIME_LATEST

Primary coordinator family:

TI zStack / CC2652-class

Secondary coordinator family:

Silicon Labs Ember / EFR32MG21/MG24-class

Security authority:

EliteSCADA Protected Material Authority
+
sidecar trusted execution

Normal Runtime:

reports / state / read / write / diagnostics

Privileged commissioning:

permit join / install code / interview / remove / bind / reporting setup / backup / restore / reset / network changes / coordinator replacement

Required contract deltas:

RESEARCH_CONTRACT_DELTA_REQUIRED — SIDECAR-LIFECYCLE-01

RESEARCH_CONTRACT_DELTA_REQUIRED — HOST-RESOURCE-GENERALIZATION-01

RESEARCH_CONTRACT_DELTA_REQUIRED — DRIVER-TRANSIENT-EVENT-01

Scope confirmation:

DOCS_ONLY

NO PRODUCT CODE CHANGED

NO DEPENDENCY CHANGED

NO CI CHANGED

NO MERGE PERFORMED
