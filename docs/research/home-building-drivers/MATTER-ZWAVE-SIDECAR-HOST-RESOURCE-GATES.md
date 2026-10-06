# Matter + Z-Wave Common Sidecar / Host Resource Gates

Research date: **2026-10-06**  
Issue owner: **#542 — HOME-RESEARCH-03**  
Contract: **C-HOME-MATTER-ZWAVE-RESEARCH-01**  
Order: **HOME-MATTER-ZWAVE-READINESS-RESEARCH-01**  
Branch: **research/home-matter-zwave-readiness**  
Scope: **RESEARCH_ONLY / DOCS_ONLY / NO_PRODUCT_CODE / NO_MERGE_BY_RESEARCHER**

Related checkpoint decisions:

- Matter: **GO_WITH_GATES**
- Z-Wave JS: **GO_WITH_GATES**

This document freezes the common foundation required before either protocol is released as product code.

---

# 1. Executive decision

Both integrations should consume one **managed local sidecar lifecycle contract** rather than inventing protocol-specific process supervision.

Required shared delta:

**RESEARCH_CONTRACT_DELTA_REQUIRED — SIDECAR-LIFECYCLE-01**

Z-Wave additionally requires a first-class host-owned physical controller resource.

Required shared delta:

**RESEARCH_CONTRACT_DELTA_REQUIRED — HOST-RESOURCE-GENERALIZATION-01**

The common foundation should be deliberately small:

1. generic sidecar process supervision;
2. generic version/schema/health negotiation;
3. generic local control-plane isolation;
4. generic persistent-state/backup hooks;
5. generic host-resource identity/discovery/lease;
6. reuse of the already integrated Protected Material Authority;
7. reuse of the already integrated #500 diagnostics framework;
8. protocol-specific adapters for Matter and Z-Wave.

Do **not** create:

- a second Runtime;
- a second diagnostics framework;
- a second secret store;
- a browser-owned hardware catalog;
- one generic "radio" abstraction that erases protocol-specific recovery semantics.

---

# 2. Current EliteSCADA foundation already available

GitHub live revalidated against:

**wave15/corrections-integration@69d5248e462ef12c41d99c6859a3afb77c6e3e2d**

## 2.1 Driver integration metadata already anticipates this architecture

Current Driver SDK includes:

- IntegrationDomain;
- DriverConnectionModel:
  - DirectNetwork;
  - HostSerial;
  - HostRadio;
  - LocalBridge;
  - Cloud;
- DriverExternalDependencyKind:
  - OptionalSidecar;
  - CertifiedHardware;
  - HostResource;
  - other dependency classes.

This means the product already has vocabulary for:

- sidecar-backed integrations;
- host-radio/controller integrations;
- external hardware dependencies.

No parallel integration catalog is needed.

## 2.2 Current configuration value kinds are not yet a general Host Resource selector

Current DriverConfigurationValueKind includes:

- SerialPort;
- SecretReference;
- CertificateReference;
- ordinary scalar/network kinds.

There is not yet a generic typed Host Resource selector for:

- ZWaveController;
- BluetoothAdapter;
- ThreadRadio/RCP.

Therefore Host Resource generalization should extend the current model rather than create protocol-specific browser fields.

## 2.3 Protected Material Authority is integrated

Main integrated #497.

Current protected-material contract is appropriate for:

- Matter Wi-Fi commissioning passwords;
- Matter Thread operational datasets;
- sidecar remote-control credentials if ever allowed;
- Z-Wave S0/S2/LR network keys;
- backup encryption keys;
- other operator-provisioned secrets.

The authority already provides:

- opaque references;
- resource/purpose scope;
- server-only resolve;
- encrypted host persistence;
- Windows/Linux/container deployment rules;
- no plaintext package export.

Therefore neither Matter nor Z-Wave may introduce a new feature-specific secret store.

## 2.4 Diagnostics convergence is integrated

#500 is integrated into the current foundation.

Canonical ladder:

**Host -> Driver/Data Source -> Network -> Connection Test -> Test Read -> Active TAG**

Matter and Z-Wave must reuse this ladder.

Sidecar/controller details are protocol-specific evidence **inside** these stages, not a new top-level diagnostics product.

Examples:

Matter:
- Host: sidecar executable/container, host networking, BLE adapter when used;
- Driver/Data Source: controller/server schema and fabric readiness;
- Network: IPv6/mDNS, Wi-Fi/Thread, Border Router reachability;
- Connection Test: controller/fabric session;
- Test Read: bounded cluster/attribute evidence;
- Active TAG: canonical process state.

Z-Wave:
- Host: ZWaveController presence/lease/USB;
- Driver/Data Source: sidecar/server/core version and controller open;
- Network: Home ID, RF region, network/security/interview state;
- Connection Test: controller/network ready;
- Test Read: bounded node/value evidence;
- Active TAG: canonical process state.

Do not add a separate "Sidecar diagnostics app."

## 2.5 Discovery/materialization already protects transient-event semantics

Current Driver materialization contract explicitly states that transient button/action events must not be materialized as persistent TAG state without a separate event-capability contract.

This directly supports:

- Matter events;
- Z-Wave Central Scene/value notifications.

Required future event delta remains:

**RESEARCH_CONTRACT_DELTA_REQUIRED — DRIVER-TRANSIENT-EVENT-01**

---

# 3. SIDECAR-LIFECYCLE-01

## 3.1 Purpose

Provide one host-owned supervisor contract for local protocol services that EliteSCADA depends on but does not embed directly.

Initial consumers:

- Matter(.js) Server;
- Z-Wave JS Server.

Future consumers may use the same infrastructure only when their lifecycle semantics genuinely fit.

The sidecar supervisor does not know Matter clusters or Z-Wave Command Classes.

Protocol adapters own protocol semantics.

---

# 4. Sidecar instance identity

Every configured sidecar instance should have stable EliteSCADA identity independent from process ID and TCP port.

Suggested conceptual fields:

- SidecarInstanceId;
- SidecarKind;
- OwnerDataSourceId or owning integration identity;
- deployment mode;
- configured version;
- configured protocol/schema range;
- persistent-state identity;
- Host Resource references where applicable;
- runtime authority/lease identity.

Runtime-observed fields:

- process/container ID;
- effective version;
- protocol/schema version;
- executable/image digest where available;
- startup time;
- restart count;
- current health.

Never use:

- PID;
- random local port;
- container ID

as canonical sidecar identity.

---

# 5. Sidecar lifecycle state machine

Minimum conceptual states:

1. NotConfigured
2. Stopped
3. ResolvingDependencies
4. Starting
5. Negotiating
6. Ready
7. Degraded
8. RestartBackoff
9. Blocked
10. Failed
11. Stopping

Examples of Blocked:

- missing protected material;
- Host Resource owned elsewhere;
- unsupported server version;
- incompatible schema;
- unsupported platform;
- persistent-state migration requires intervention;
- Matter Windows local controller path where upstream is unsupported.

Examples of Degraded:

- one Matter node unavailable while fabric/controller remains ready;
- one Z-Wave node interview incomplete;
- external Thread Border Router unavailable for Thread nodes while Wi-Fi nodes still operate.

Do not collapse node/device failures into sidecar process failure.

---

# 6. Startup contract

Common startup sequence:

1. verify effective Active/runtime authority;
2. resolve configured sidecar instance;
3. validate supported host platform;
4. resolve Host Resource references if required;
5. acquire required exclusive leases;
6. resolve protected configuration material;
7. validate persistent state directory;
8. pin executable/image/runtime;
9. bind control plane locally;
10. start process/container;
11. wait for process liveness;
12. establish sidecar control connection;
13. negotiate server/API/schema version;
14. verify persistent protocol identity;
15. verify controller/network readiness;
16. expose Ready;
17. allow protocol adapter to reconcile nodes/values/subscriptions.

Protocol-specific verification:

Matter:
- expected controller/fabric state;
- persistent fabric storage loaded;
- IPv6/mDNS prerequisites;
- Thread network/BLE prerequisites only when needed.

Z-Wave:
- expected ZWaveController resource;
- Home ID;
- firmware/SDK/RF region;
- security-key completeness;
- driver/controller ready.

If persistent identity differs from expected identity, fail closed and require admin reconciliation.

Do not silently attach a newly discovered controller/fabric to an existing Data Source.

---

# 7. Shutdown contract

Common shutdown sequence:

1. reject new privileged admin operations;
2. stop accepting new process writes where safe shutdown requires it;
3. drain bounded client work;
4. stop/release protocol subscriptions;
5. request graceful sidecar shutdown;
6. wait bounded timeout;
7. close local control channel;
8. verify child/container exit;
9. flush/close persistent state;
10. release Host Resource lease.

Forced kill is an escalation path, not normal shutdown.

If forced termination occurs:

- record it;
- require persistent-state integrity validation at next startup;
- do not silently assume backup consistency.

---

# 8. Restart and crash recovery

Supervisor requirements:

- detect unexpected exit;
- bounded exponential backoff;
- jitter;
- restart budget;
- crash-loop detection;
- sanitized exit reason;
- no endless tight loop;
- retain resource ownership during an immediate supervised restart when safe;
- release ownership after terminal Blocked/Failed state according to policy.

After restart:

- reconnect sidecar control plane;
- renegotiate schema;
- validate protocol identity;
- rebuild subscriptions/listeners;
- reconcile current state incrementally;
- avoid mass device traffic.

Matter:

- do not issue full reads for all commissioned nodes immediately if subscriptions/controller state can recover naturally;
- treat ICD/sleepy nodes specially.

Z-Wave:

- do not immediately poll/interview the whole network;
- respect sleeping nodes;
- preserve last confirmed TAG state with truthful quality/staleness.

---

# 9. Version and schema pinning

Every sidecar release must pin:

- package/server version;
- underlying protocol library version;
- Node/runtime version;
- control protocol/schema version;
- state migration version if exposed.

## Matter current research baseline

- Matter(.js) Server: 1.4.0;
- matter.js: 0.17.9;
- WebSocket schema: 13;
- upstream stable Matter support: 1.6.0.

## Z-Wave current research baseline

- Z-Wave JS Server: 3.10.1;
- Z-Wave JS: 15.31.0;
- WebSocket schema line: 50;
- Node: >=20.

These are research baselines, not permanent product pins.

Future code must define:

- minimum supported;
- maximum tested;
- upgrade path;
- incompatible version behavior.

Unsupported version must be actionable, not a generic Connection Failed.

---

# 10. Local control-plane rules

## 10.1 Default topology

Preferred:

**EliteSCADA host -> local sidecar -> protocol**

Default control interface:

- Unix domain socket when supported and practical; otherwise
- loopback-only TCP/WebSocket.

Never default to all-interface bind.

## 10.2 Browser isolation

Browser/frontend must never connect directly to:

- Matter sidecar;
- Matter BLE proxy;
- Z-Wave JS Server;
- raw controller TCP/serial proxies.

Browser talks only to authorized EliteSCADA APIs.

## 10.3 Remote sidecar

Remote sidecar support is not first-release default.

If later authorized:

- TLS;
- mutual/service authentication;
- authorization;
- replay/session protection;
- certificate/credential rotation;
- explicit remote endpoint health;
- firewall guidance.

An unauthenticated upstream WebSocket protocol is not itself a safe remote management protocol.

---

# 11. Protected configuration injection

Do not pass long-lived secrets through:

- command-line arguments;
- project JSON;
- environment variables when process listings/crash reports make that unsafe;
- world-readable temp files;
- browser payloads;
- logs.

Preferred future launcher pattern:

1. feature-owned admin flow stores material in IProtectedMaterialAuthority;
2. Data Source/config persists only opaque reference;
3. trusted launcher resolves short-lived lease;
4. launcher provides material to sidecar through the narrowest supported protected mechanism;
5. temporary material is zeroed/deleted;
6. sidecar persistent secrets are protected according to upstream storage model.

Matter sidecar-generated fabric private material should remain opaque sidecar state.

Z-Wave host-provisioned network keys should originate from Protected Material Authority.

---

# 12. Persistent sidecar state

Persistent sidecar state is neither:

- canonical Engineering truth; nor
- disposable cache.

Common requirements:

- stable state directory identity;
- exclusive ownership;
- durable volume;
- backup classification;
- integrity check;
- version/migration metadata;
- no accidental deletion on project import/remove;
- no copying into ordinary .escadapkg.

Matter examples:

- fabric/controller credentials;
- commissioned-node controller database;
- ICD state;
- stored Thread/Wi-Fi credentials.

Z-Wave examples:

- node/value cache;
- SmartStart provisioning list;
- controller/network cache;
- device configuration state.

Z-Wave controller NVM remains a separate protected artifact.

---

# 13. Upgrade contract

A sidecar update is a stateful infrastructure update, not a normal NuGet/npm package bump.

Required process:

1. identify exact current versions;
2. check target compatibility;
3. stop privileged network mutation;
4. create protected backup;
5. stop sidecar;
6. update executable/image/runtime as one tested bundle;
7. start target;
8. allow supported state migration;
9. verify identity;
10. verify health;
11. verify a bounded protocol sample;
12. mark update accepted.

Do not auto-update sidecars independently from EliteSCADA release management.

---

# 14. Rollback contract

Rollback is only supported when state compatibility is proven.

Never assume:

- target vN+1 migrated state can be opened by vN;
- controller NVM changed by new software is automatically backward compatible.

Rollback package must specify:

- sidecar binary/image version;
- runtime version;
- compatible persistent-state snapshot;
- controller firmware compatibility;
- protocol-specific limitations.

If rollback cannot be proven, fail with explicit manual-recovery requirement.

---

# 15. Sidecar backup classes

Define separate backup classes.

## A. Host configuration backup

Contains:

- non-secret sidecar configuration;
- version pin;
- resource references;
- state metadata.

May be project/package-compatible if host-independent.

## B. Protected Material Authority backup

Deployment-secret authority backup/key management.

Never copied into normal project package.

## C. Sidecar protected-state backup

Protocol controller state.

Examples:

- Matter fabric/controller storage;
- Z-Wave JS host state.

Encrypted, access-controlled.

## D. Controller hardware backup

Z-Wave NVM.

Encrypted and compatibility-controlled.

Do not combine all four into an opaque unversioned archive.

---

# 16. Health model

Common health dimensions:

- supervisor health;
- process health;
- control-channel health;
- schema compatibility;
- protected-material readiness;
- state storage readiness;
- Host Resource readiness;
- protocol controller/network readiness;
- device/node readiness;
- subscription/report freshness;
- backup/recovery health.

Expose machine-readable issue codes.

Do not expose secrets in diagnostics.

---

# 17. Logging requirements

Every sidecar log record should preserve:

- timestamp;
- sidecar instance;
- protocol;
- sanitized operation;
- severity;
- correlation ID where useful.

Redact:

Matter:
- setup codes;
- Wi-Fi passwords;
- Thread dataset;
- fabric private material;
- lock credential material.

Z-Wave:
- network keys;
- DSK/PIN;
- SmartStart protected provisioning;
- raw NVM content.

Support bundle export must have a deterministic redaction pass.

---

# 18. Architecture / CPU matrix

Common packaging should separate:

- EliteSCADA host architecture;
- Node runtime architecture;
- sidecar support matrix;
- USB/native dependencies.

## Matter

Current upstream research:

- recent 64-bit Linux supported;
- recent 64-bit macOS supported;
- Windows/WSL not supported;
- Linux x64 first product candidate;
- Linux ARM64 requires exact qualification.

## Z-Wave

Current stack is suitable for Windows/Linux with serial access.

Qualify:

- Windows x64;
- Linux x64;
- Linux ARM64;
- container USB passthrough.

Do not claim a platform merely because Node itself supports it.

---

# 19. Runtime authority / HA

Both sidecars are **single-authority protocol controllers**.

No standby or second Runtime may independently mutate the same fabric/network.

Matter:

- two sidecars sharing the same controller state directory is forbidden;
- multi-admin Matter is a protocol feature between independent fabrics/controllers, not HA fencing;
- HA failover of one EliteSCADA Matter fabric requires protected state transfer and explicit single-active fencing.

Z-Wave:

- exactly one sidecar owns the physical controller;
- controller cannot be simultaneously opened by Active and Standby;
- failover requires hardware accessibility plus exclusive lease/fencing.

First implementation should not imply automatic HA failover for these integrations unless Main separately proves:

- sidecar state replication;
- Host Resource reachability;
- protected material continuity;
- fencing;
- no double control.

Reuse existing EliteSCADA effective-Active authority.

Do not create sidecar-level leader election.

---

# 20. HOST-RESOURCE-GENERALIZATION-01

## 20.1 Goal

Generalize the reusable part of current host serial infrastructure into typed server-owned resources.

Proposed conceptual resource families:

- SerialPort;
- ZWaveController;
- BluetoothAdapter;
- ThreadRadioRcp;
- future protocol interface resources.

The common contract must not imply identical lifecycle for all types.

---

# 21. Host Resource record

Conceptual configured resource:

- ResourceId;
- ResourceKind;
- DisplayName;
- ExpectedIdentity;
- user-selected locator/fallback locator;
- enabled/disabled;
- protocol-specific bounded settings.

Runtime observation:

- present/absent;
- current endpoint;
- OS metadata;
- USB metadata;
- firmware/version;
- capabilities;
- last observed;
- conflict/lease state.

Configured identity persists while resource is offline.

Transient discovery does not become canonical truth automatically.

---

# 22. Host Resource discovery

Server-side only.

Allowed discovery:

- serial ports visible to host;
- USB devices visible to host;
- Bluetooth adapters visible to host;
- explicitly supported controller hardware.

Browser must not enumerate its own machine.

Container sees only passed-through resources.

Discovery result is a candidate.

User/admin maps discovery to stable ResourceId.

---

# 23. Host Resource lease

Lease contract must include:

- ResourceId;
- lease holder;
- owner Data Source/integration;
- acquisition timestamp;
- generation/epoch;
- exclusivity policy;
- release reason.

Default for ZWaveController:

**exclusive**

Default for ThreadRadioRcp if later owned:

**exclusive by OTBR sidecar**

BluetoothAdapter policy may be shareable or exclusive depending on selected platform stack; do not guess globally.

SerialPort remains compatible with #469 sharing rules where protocol explicitly supports safe bus sharing.

Resource kind controls sharing semantics.

---

# 24. ZWaveController specialization

Mandatory first-class resource.

Additional observed/configured identity:

- USB VID/PID;
- serial number;
- stable OS path where available;
- controller family;
- model;
- firmware;
- SDK;
- RF region;
- Home ID;
- controller node ID;
- Long Range capability;
- Node ID width;
- NVM compatibility identity.

A COM path is a locator, not complete identity.

---

# 25. Matter Host Resource specialization

## Wi-Fi/Ethernet Matter

No dedicated radio Host Resource required.

Needs:

- host network interface;
- IPv6;
- mDNS;
- route to device networks.

## Matter over Thread with external TBR

No ThreadRadioRcp resource required in first release.

External Thread Border Router is a network dependency.

## Bluetooth commissioning

BluetoothAdapter may be needed when local BLE commissioning is used.

Treat it as Host Resource only if EliteSCADA actually owns/directly opens the adapter.

If Matter Server BLE proxy is used, proxy endpoint is a sidecar dependency, not automatically a local adapter.

## Owned OTBR

Deferred.

If Main later chooses EliteSCADA-owned OTBR:

- ThreadRadioRcp resource;
- exclusive lease;
- OTBR sidecar lifecycle;
- Thread dataset authority;
- backup/restore;
- radio firmware.

Do not create this resource in first Matter implementation merely for architectural completeness.

---

# 26. Host Resource package/import behavior

Machine-local discovery snapshot must not be exported as portable truth.

Portable project may carry:

- stable logical resource requirement;
- expected model/capability constraints;
- non-secret configuration.

On a different host:

- resource is Unbound/Missing;
- admin maps local hardware;
- protected material is reprovisioned or restored through deployment process;
- Runtime remains blocked until required resource binding is valid.

Do not silently bind the first matching USB device after import.

---

# 27. Common resource conflict behavior

Deterministic conflicts:

- ZWaveController already leased;
- same state directory owned by another sidecar;
- incompatible serial settings;
- same exclusive Bluetooth/Thread radio leased elsewhere;
- persistent protocol identity mismatch.

Conflict response:

- fail Preview/Activation when conflict is statically knowable;
- fail Runtime startup safely when OS/external process conflict is only runtime-detectable;
- show current owning resource/integration when authorized;
- never silently select another device.

---

# 28. Common administrative boundary

Normal Runtime:

- process reads;
- process writes;
- subscriptions/reports;
- approved Commands;
- process diagnostics.

Privileged administration:

Matter:
- commission;
- fabric mutation;
- network credential mutation;
- open commissioning window;
- remove fabric/node;
- restore controller state.

Z-Wave:
- inclusion;
- exclusion;
- security grant;
- SmartStart provisioning;
- associations;
- NVM restore;
- controller reset;
- RF-region change.

These operations require:

- authorization;
- explicit confirmation;
- audit;
- sanitized progress;
- recovery semantics.

Do not expose them as ordinary process Commands.

---

# 29. Common gate matrix

| Gate | Matter | Z-Wave | Common disposition |
|---|---|---|---|
| Managed process supervisor | Required | Required | SIDECAR-LIFECYCLE-01 |
| Local-only control plane | Required | Required | Required |
| Version/schema pin | Required | Required | Required |
| Protected Material Authority | Reuse integrated | Reuse integrated | No new secret store |
| Persistent sidecar state | Required | Required | Protected durable volume |
| Upgrade/rollback | Required | Required | Stateful release contract |
| Exclusive resource lease | Sidecar/state; BLE if local | ZWaveController mandatory | Host Resource contract |
| Hardware resource | Optional BLE/RCP only | Mandatory controller | Typed Host Resource |
| Transient events | Matter events | Central Scene | DRIVER-TRANSIENT-EVENT-01 |
| Diagnostics | Reuse #500 | Reuse #500 | No new framework |
| HA double ownership | Forbidden | Forbidden | Reuse effective Active fencing |
| Portable project secrets | Forbidden | Forbidden | Opaque refs only |

---

# 30. Protocol-specific gates that remain outside the common layer

## Matter-only

- upstream Matter(.js) Server Beta / not re-certified;
- Matter 1.6.0 stable stack vs current 1.6.1 spec;
- Windows local controller unsupported upstream;
- current upstream PASE/CASE resource-exhaustion issues;
- IPv6/mDNS;
- Thread Border Router;
- BLE;
- fabric attestation/DCL;
- Matter certification.

## Z-Wave-only

- ZWaveController resource identity;
- S0/S2/LR key set;
- RF region;
- NVM compatibility;
- open #8833 LR/NVM backup issue;
- sleeping-node queued-write truth;
- Z-Wave certification/marketing.

Do not push these into generic sidecar code.

---

# 31. Recommended implementation order for shared foundations

No product implementation is authorized by this research branch.

If Main releases foundation work later:

## Foundation A — Host Resource generalization

Build only enough generic resource identity/discovery/lease to support:

- existing SerialPort compatibility;
- ZWaveController.

Do not prematurely implement ThreadRadioRcp.

## Foundation B — Sidecar lifecycle

Build generic supervisor with:

- local launch;
- version/schema;
- health;
- restart;
- durable state ownership;
- logs;
- protected configuration injection;
- backup hooks.

Use a fake sidecar for deterministic tests.

## Foundation C — protocol consumers

Then release:

1. Z-Wave basic controller/inventory slice;
2. Matter Linux/Wi-Fi basic controller slice;

or vice versa according to Main's final sequencing decision.

---

# 32. Deterministic foundation tests

## Sidecar supervisor L0/L1

- executable missing;
- bad version;
- incompatible schema;
- startup timeout;
- clean stop;
- forced stop;
- crash;
- crash loop;
- restart budget;
- state directory conflict;
- protected material missing;
- log redaction;
- loopback-only bind validation;
- upgrade preflight;
- backup hook;
- rollback compatibility rejection.

## Host Resource L0/L1

- transient discovery;
- stable configured identity;
- resource absent;
- locator changed;
- expected identity matches;
- identity mismatch;
- exclusive lease;
- lease contention;
- release;
- stale lease cleanup only under proven owner death/fencing;
- import on another host remains unbound;
- browser cannot enumerate OS hardware.

## Integration contract

- Active authority owns resource;
- Standby cannot open resource;
- sidecar cannot start without required lease;
- resource stays held through bounded child restart;
- final failure releases according to policy;
- secret resolution occurs only after authorization/ownership.

---

# 33. Common blockers before any production implementation

1. Main must authorize SIDECAR-LIFECYCLE-01 implementation.
2. Main must authorize HOST-RESOURCE-GENERALIZATION-01 implementation.
3. Transient-event contract must exist before Matter events/Central Scene are product features.
4. Protocol-specific L0-L4 evidence remains mandatory.
5. Legal/certification claims remain separate and unresolved.
6. No sidecar implementation may weaken existing HA effective-Active authority.
7. No sidecar may bypass Protected Material Authority.
8. No controller/radio enumeration may be exposed directly to browser/Runtime scripts.

---

# 34. Checkpoint 3 sidecar / Host Resource conclusion

Common architecture is now sufficiently defined for a future foundation DEV.

Recommended common contracts:

**SIDECAR-LIFECYCLE-01**
+
**HOST-RESOURCE-GENERALIZATION-01**

Existing foundations that must be reused:

- Driver SDK integration metadata;
- #469 host serial;
- #497 Protected Material Authority;
- #500 diagnostics convergence;
- current discovery/materialization contract;
- current effective-Active/HA authority.

No new product framework is justified.

---

# 35. Source ledger

Accessed/revalidated 2026-10-06:

EliteSCADA live:
- #542
- #472
- #469
- #497
- #500
- wave15/corrections-integration source:
  - src/Scada.Drivers/Abstractions/DriverEngineeringContracts.cs
  - docs/WAVE15-PROTECTED-MATERIAL-AUTHORITY.md

Protocol research:
- MATTER-CONTROLLER-READINESS-RESEARCH.md
- ZWAVE-JS-READINESS-RESEARCH.md

Upstream:
- https://github.com/matter-js/matterjs-server
- https://github.com/matter-js/matter.js
- https://github.com/zwave-js/zwave-js-server
- https://github.com/zwave-js/zwave-js

---

**DOCS_ONLY**

**NO PRODUCT CODE CHANGED**

**NO DEPENDENCY CHANGED**

**NO CI CHANGED**

**NO HOST RESOURCE CODE CHANGED**

**NO SIDECAR CODE CHANGED**

**NO MATTER CODE IMPLEMENTED**

**NO ZWAVE CODE IMPLEMENTED**

**NO MERGE PERFORMED**
