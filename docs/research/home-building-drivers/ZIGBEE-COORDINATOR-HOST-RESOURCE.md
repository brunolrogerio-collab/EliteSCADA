# ZigbeeCoordinator Host Resource — Research Contract Recommendation

Status: RESEARCH_ONLY / DOCS_ONLY / NO_PRODUCT_CODE / NO_MERGE_BY_RESEARCHER

Issue owner: #541 — HOME-RESEARCH-02 — Zigbee2MQTT + Native Zigbee execution dossier

Contract input: C-HOME-ZIGBEE-RESEARCH-01

Access/revalidation date: 2026-10-06

This document is a research contract recommendation for the future Native Zigbee implementation.

It does not implement a Host Resource.

---

## 1. Decision

Native Zigbee needs a first-class server-owned Host Resource:

ZigbeeCoordinator

It must not be represented as only:
- SerialPort;
- COM port;
- /dev path;
- TCP host:port;
- sidecar-local string.

A Zigbee coordinator is a persistent product-owned hardware/resource identity that may use serial or TCP transport but also carries:
- coordinator family;
- physical identity;
- firmware;
- exclusive ownership;
- Zigbee network identity;
- protected backup/recovery implications;
- safe lifecycle rules.

Research contract delta:

RESEARCH_CONTRACT_DELTA_REQUIRED — HOST-RESOURCE-GENERALIZATION-01

---

## 2. Relationship to existing Host Serial

#469 delivered reusable server-side serial authority.

Native Zigbee should reuse:
- host-visible serial discovery;
- Windows/Linux server authority;
- manual configured fallback;
- container visibility rules;
- resource arbitration concepts;
- diagnostics;
- no browser-local hardware authority.

But:

ZigbeeCoordinator != SerialPort

Why:
- one coordinator owns one Zigbee network;
- one coordinator can be TCP rather than serial;
- one sidecar must exclusively own it;
- identity is more important than current OS path;
- coordinator firmware/family changes semantics;
- backup/restore contains network security state;
- connecting the wrong coordinator can create catastrophic network-state confusion.

---

## 3. Core Host Resource shape

Recommended conceptual persisted shape:

ZigbeeCoordinatorResource
- Id
- DisplayName
- Enabled
- EndpointKind
- SerialEndpoint or TcpEndpoint
- AdapterFamily
- ExpectedCoordinatorIdentity
- ExpectedHardwareFingerprint optional
- ExpectedModel optional
- TransportOptions
- OwnershipMode = Exclusive
- Metadata

The exact public schema belongs to a future foundation implementation.

This research freezes semantics, not field names.

---

## 4. Stable configured identity

### 4.1 Resource ID

Every configured coordinator resource needs a stable HostResourceId.

This ID survives:
- display-name changes;
- COM number changes;
- Linux device-path changes;
- IP/hostname changes;
- firmware upgrade.

Runtime Data Sources reference HostResourceId, not a transient discovery row.

### 4.2 Physical identity

After connecting, the sidecar should obtain coordinator identity including:

- coordinator IEEE/EUI64;
- adapter family;
- coordinator/firmware version;
- hardware model/fingerprint where available.

The product may persist an expected coordinator IEEE or fingerprint after initial accepted provisioning.

If later activation sees a different coordinator:

EXPECTED_COORDINATOR_MISMATCH

Fail closed unless an authorized admin explicitly rebinds/replaces the resource.

### 4.3 Why path is not identity

Windows COM3 may become COM4.

Linux /dev/ttyUSB0 may become /dev/ttyUSB1.

TCP endpoints may be moved during managed deployment.

Therefore physical transport location is configuration, not identity.

---

## 5. Endpoint kinds

Initial recommended endpoint kinds:

SERIAL

TCP

Do not create separate product Host Resource types for:
- USB Zigbee;
- Ethernet Zigbee;
- PoE Zigbee.

Those are transport/deployment variants under one ZigbeeCoordinator authority.

---

## 6. Serial endpoint contract

### 6.1 Server authority

Serial ports are enumerated on the EliteSCADA server host.

Browser-local serial APIs are forbidden as canonical authority.

### 6.2 Configured fields

Recommended serial configuration:

- server-visible path/name;
- optional stable host-serial resource reference from #469;
- baud rate;
- RTS/CTS or flow-control option only where adapter requires it;
- bounded open timeout;
- optional adapter-family-specific transport flags.

Do not expose every Node serial option to normal Engineering.

### 6.3 Windows

Examples:
- COM3
- COM7

Requirements:
- enumerate via server host;
- allow persisted configured device while temporarily disconnected;
- activation reports missing port truthfully;
- no client/browser COM enumeration.

### 6.4 Linux

Prefer stable device aliases:
- /dev/serial/by-id/...

Allow:
- /dev/ttyUSB*
- /dev/ttyACM*
- explicitly configured container-visible paths

as bounded manual fallback.

### 6.5 Docker/container

Only devices passed into the container are visible.

Requirements:
- container device mapping documented;
- sidecar has permission to open the device;
- stable host resource remains product identity;
- transient container path does not become Equipment identity.

---

## 7. TCP endpoint contract

### 7.1 Fields

Recommended:
- hostname/IP;
- port;
- connect timeout;
- optional TLS only if the specific coordinator transport supports a defined secure protocol in future;
- expected coordinator family/identity.

Do not invent TLS around a raw coordinator TCP stream without protocol support.

### 7.2 Network coordinator guidance

For remote coordinators:
- wired Ethernet should be the first qualification baseline;
- Wi-Fi should not be assumed equivalent;
- latency/packet loss can destabilize coordinator protocol traffic.

### 7.3 Security boundary

A raw TCP coordinator endpoint is not automatically authenticated.

Network deployment should rely on:
- trusted local/industrial network segmentation;
- controlled host routing/firewall;
- expected coordinator identity validation after connection;
- optional vendor secure transport only when documented and qualified.

Do not expose arbitrary network scan.

---

## 8. Transient discovery

Discovery is assistance, not authority.

Possible discovery inputs:
- #469 host serial catalog;
- current herdsman USB fingerprint database;
- mDNS/Bonjour hints for supported network coordinators;
- manually configured TCP endpoint.

Discovery result may show:
- current path;
- VID/PID;
- manufacturer;
- product;
- serial number;
- suggested adapter family;
- current network endpoint.

Discovery must not:
- auto-create Active Data Sources;
- open/reset unknown coordinators;
- form a new Zigbee network;
- erase NVM;
- update firmware;
- change project configuration automatically.

Required flow:

discover
-> candidate
-> preview
-> apply

---

## 9. Adapter family

First qualified values:

zstack

ember

Do not make the enum permanently closed.

Future research may add:
- deCONZ;
- ZBOSS;
- others.

But unqualified family must fail as NOT_SUPPORTED rather than silently using generic serial.

---

## 10. PRIMARY coordinator family

PRIMARY_COORDINATOR_FAMILY:

TI zStack / CC2652-class

Current example devices:
- SONOFF ZBDongle-P — CC2652P;
- SMLIGHT SLZB-07p7 — CC2652P7;
- SMLIGHT SLZB-06 — CC2652P network/USB family.

Reasons:
- current first-class herdsman z-stack implementation;
- backup support;
- common USB hardware;
- current network coordinator options;
- recognized adapter fingerprints;
- good primary family for deterministic lab qualification.

This is a support prioritization decision, not a claim of universal superiority.

---

## 11. SECONDARY coordinator family

SECONDARY_COORDINATOR_FAMILY:

Silicon Labs Ember / EFR32MG21/MG24-class

Current example devices:
- SONOFF ZBDongle-E V2 — EFR32MG21;
- Home Assistant Connect ZBT-1;
- Home Assistant Connect ZBT-2;
- SMLIGHT MG21/MG24 variants.

Reasons:
- current first-class herdsman Ember implementation;
- backup support;
- broad ecosystem;
- valuable independent secondary stack;
- current adapter discovery support.

---

## 12. Firmware identity

Host Resource diagnostics should capture:
- adapter protocol/family;
- coordinator firmware version;
- stack version when available;
- hardware model;
- coordinator IEEE.

Firmware version is compatibility evidence.

Do not automatically upgrade coordinator firmware during normal Runtime startup.

Firmware update disposition:

ADMIN_FUTURE_SCOPE

---

## 13. Exclusive lease

A ZigbeeCoordinator is EXCLUSIVE.

Only one active sidecar/runtime owner may open one physical coordinator.

This differs from the #469 RTU Master design where multiple logical Data Sources may share one serialized Modbus bus.

For Zigbee:
- one coordinator;
- one network stack owner;
- one persistent device database authority;
- one report/event stream.

Required lease key:
HostResourceId

Runtime may additionally guard:
- resolved serial physical identity;
- TCP endpoint;
- coordinator IEEE after connect.

---

## 14. Contention

Contention scenarios:

- two Native Zigbee Data Sources reference one HostResourceId;
- old sidecar process still holds serial port;
- another external application owns the dongle;
- two sidecars point to same TCP coordinator;
- duplicate configured Host Resources resolve to same physical coordinator.

Expected outcome:

RESOURCE_IN_USE / OWNERSHIP_CONFLICT

Do not:
- open a second owner;
- auto-select another port;
- reset hardware;
- create a new mesh.

---

## 15. Lease lifecycle

### Acquire

Before sidecar opens coordinator transport:
1. validate configured HostResourceId;
2. acquire exclusive host lease;
3. resolve endpoint;
4. open transport;
5. identify coordinator;
6. confirm expected identity;
7. mark lease Active.

### Renew/health

Supervisor tracks:
- sidecar process;
- lease owner;
- endpoint;
- coordinator connection.

### Release

Release after:
- sidecar stopped;
- transport closed;
- durable state flushed.

### Crash

If process exits unexpectedly:
- supervisor detects owner death;
- closes/reclaims resources under host rules;
- lease becomes reclaimable;
- restart may reacquire.

Do not leave a permanent lease solely because process crashed.

---

## 16. Sidecar ownership

The Zigbee sidecar must be the direct owner of the coordinator transport during Native operation.

EliteSCADA core C# should not simultaneously open the same serial/TCP channel.

Flow:

Host Resource Authority
-> grants exclusive lease to managed sidecar instance
-> sidecar opens coordinator transport
-> sidecar reports connected identity
-> EliteSCADA validates readiness

The lease contract must make sidecar ownership explicit.

---

## 17. Safe activation

Activation should fail if:
- resource missing when activation requires Runtime start;
- lease unavailable;
- adapter family unsupported;
- coordinator cannot connect;
- coordinator identity mismatches configured expectation;
- protected network material unavailable;
- network state incompatible/corrupt.

Engineering persistence may still allow a currently disconnected coordinator to be configured.

This preserves offline authoring.

---

## 18. Reconnect behavior

Transient disconnect must not recreate a network.

Required behavior:
1. mark coordinator unavailable;
2. propagate source quality degradation;
3. reject/hold writes according to bounded Runtime policy;
4. close stale transport;
5. bounded reconnect backoff;
6. reacquire/revalidate endpoint;
7. identify coordinator;
8. validate expected coordinator/network identity;
9. restore report/event subscription;
10. reconcile device state;
11. return Ready.

Never factory reset on reconnect.

---

## 19. Safe restart

Sidecar restart sequence:

1. close permit join;
2. block new commissioning operations;
3. quiesce new writes;
4. cancel/drain bounded in-flight operations;
5. persist device database;
6. produce backup if lifecycle policy requests;
7. close coordinator transport;
8. release lease;
9. restart exact pinned sidecar;
10. reacquire same resource;
11. validate coordinator identity;
12. validate network identity;
13. resubscribe/reconcile;
14. mark Ready.

If identity validation fails:
Faulted / manual intervention required.

---

## 20. Network identity guard

After coordinator connect, capture:

- coordinator IEEE;
- PAN ID;
- extended PAN ID;
- channel;
- network update ID where available.

A mismatch may mean:
- wrong coordinator;
- erased coordinator;
- wrong backup;
- unexpected new network;
- corrupted state.

Do not silently adopt the mismatch.

Product should surface:

NETWORK_IDENTITY_MISMATCH

with sanitized expected/observed non-secret identity.

---

## 21. Resource replacement

Coordinator replacement is an admin operation.

Recommended flow:

1. stop normal writes;
2. close permit join;
3. create/validate protected backup;
4. stop old sidecar/coordinator;
5. release old resource lease;
6. bind replacement physical resource;
7. validate family/firmware compatibility;
8. restore backup;
9. validate coordinator/network identity;
10. start sidecar;
11. verify representative routers/end devices;
12. restore Ready only after health checks.

Same-family restore is the first qualification target.

Cross-family restore is not guaranteed.

---

## 22. Backup ownership

A coordinator backup contains sensitive network material including:
- network key;
- frame counters;
- device link keys where available.

Therefore the raw backup cannot be ordinary project JSON.

Recommended storage:

host-protected operational state

using:
- Protected Material Authority;
- or a protected backup store built on the same security authority.

The package may carry only a dependency/status descriptor, not the raw backup.

---

## 23. Protected material references

Potential protected purposes:

- ZigbeeNetworkKey
- ZigbeeInstallCode
- ZigbeeCoordinatorBackup

Exact purpose token naming belongs to implementation.

Requirements from existing Protected Material Authority:
- resource scoped;
- opaque reference;
- no plaintext readback endpoint;
- trusted internal resolve;
- zeroing lease where applicable;
- no secret logs;
- package-safe dependency behavior.

---

## 24. Host Resource to Data Source relationship

Recommended:

Native Zigbee Data Source
-> references ZigbeeCoordinator HostResourceId

The Data Source owns:
- scan/report/reconciliation policy;
- semantic import settings;
- commissioning policy references;
- sidecar version pin reference;
- Equipment/TAG mappings.

The Host Resource owns:
- physical coordinator selection;
- endpoint;
- adapter family;
- exclusive lease;
- expected hardware identity.

Do not duplicate serial/TCP endpoint in every TAG.

---

## 25. Host Resource to Equipment relationship

Equipment must not reference the coordinator path directly.

Equipment source binding:

DataSourceId
+
device IEEE

This keeps Equipment portable if:
- coordinator path changes;
- host changes;
- IP changes;
- coordinator is replaced with compatible restored hardware.

---

## 26. Windows contract

Required evidence for implementation:

- host enumerates COM ports;
- configured COM port round-trips;
- disconnected COM configuration persists;
- sidecar can open selected COM port;
- adapter USB fingerprint can refine family suggestion;
- exclusive lease blocks second owner;
- reconnect works after USB removal/reinsert;
- service account permissions/errors are diagnostic.

No desktop/interactively logged-in user requirement.

---

## 27. Linux contract

Required evidence:

- host enumerates serial devices;
- /dev/serial/by-id preferred where present;
- manual path fallback;
- permissions failure classified;
- USB remove/reinsert handled;
- exclusive lease;
- systemd/headless compatible;
- no desktop dependency.

Do not assume dialout group specifically; report actual permission failure and deployment guidance.

---

## 28. Container contract

Required evidence:

- explicit device passthrough;
- persistent sidecar state volume;
- Protected Material key/material injection;
- container replacement does not erase network database;
- container image upgrade retains persistent state;
- missing device returns clear resource unavailable;
- no host-wide device scan outside mounted/visible namespace.

For TCP coordinator:
- network route/firewall must permit configured endpoint;
- no privileged USB passthrough required.

---

## 29. TCP coordinator qualification

A network coordinator adds one extra failure domain:
- Ethernet;
- bridge MCU/firmware;
- TCP socket;
- radio coprocessor.

Qualification must include:
- network disconnect;
- reconnect;
- coordinator bridge reboot;
- sidecar restart;
- duplicate TCP owner;
- latency;
- packet loss;
- host restart;
- coordinator firmware mismatch.

Wired Ethernet is the recommended first remote transport.

---

## 30. Hardware fingerprinting

Transient discovery may use:
- USB VID;
- USB PID;
- manufacturer;
- product description;
- serial number;
- path patterns.

Current herdsman discovery already uses these types of fingerprints for known adapters.

But:
- USB metadata can be ambiguous;
- virtualization can rewrite metadata/path;
- some devices share USB bridge VID/PID.

Therefore fingerprinting is assistance, not unconditional identity.

Final identity validation should prefer connected coordinator identity plus explicit configured binding.

---

## 31. Firmware compatibility

Host Resource should record/diagnose firmware.

A future supported-hardware matrix needs:
- coordinator model;
- hardware revision if known;
- adapter family;
- firmware version;
- herdsman version;
- sidecar version;
- test date.

Do not claim:
"all CC2652"
or
"all EFR32MG21"

without qualification.

---

## 32. Resource diagnostics

Expose non-secret host-resource diagnostics:

- HostResourceId;
- configured endpoint kind;
- resolved endpoint sanitized;
- adapter family;
- lease state;
- lease owner instance;
- coordinator connected;
- coordinator IEEE;
- coordinator firmware;
- expected identity match;
- network ready;
- reconnect count;
- last connect success;
- last connect failure;
- sanitized error;
- sidecar version;
- herdsman version.

Do not expose:
- network key;
- install code;
- link key;
- raw backup.

---

## 33. Common diagnostics ladder

Reuse #500:

1. EliteSCADA host/supervisor.
2. Zigbee sidecar process.
3. ZigbeeCoordinator Host Resource.
4. serial/TCP transport.
5. coordinator protocol.
6. Zigbee network ready.
7. device/interview/converter health.
8. point read/write/report.
9. live canonical TAG.

A green serial port is not a green Zigbee device.

---

## 34. Resource errors

Recommended semantic error classes:

RESOURCE_NOT_CONFIGURED

RESOURCE_NOT_VISIBLE

RESOURCE_IN_USE

RESOURCE_PERMISSION_DENIED

TRANSPORT_CONNECT_TIMEOUT

TRANSPORT_DISCONNECTED

ADAPTER_FAMILY_UNSUPPORTED

COORDINATOR_IDENTITY_MISMATCH

COORDINATOR_FIRMWARE_UNSUPPORTED

NETWORK_IDENTITY_MISMATCH

PROTECTED_MATERIAL_UNAVAILABLE

BACKUP_INCOMPATIBLE

SIDECAR_VERSION_INCOMPATIBLE

These are product-semantic examples, not a mandated enum.

---

## 35. Serial sharing rule

Unlike Modbus RTU Master:

Zigbee coordinator serial transport is not shareable.

Even if two Data Sources use different Zigbee devices, the entire coordinator/network is one stack authority.

Multiple logical Zigbee Data Sources must not open one coordinator.

Preferred model:
one Data Source per owned Zigbee network.

---

## 36. TCP sharing rule

Also exclusive.

A raw network coordinator may accept or behave unpredictably with multiple clients.

EliteSCADA should prevent multiple managed owners before connection.

If an external application already owns the TCP coordinator:
- connection may fail;
- surface contention/connection error;
- do not fight/reset the adapter.

---

## 37. Discovery vs commissioning

Host Resource discovery:
- finds candidate coordinators.

Zigbee device discovery:
- enumerates already-known mesh devices.

Zigbee commissioning:
- opens permit join;
- accepts new device;
- interviews/configures it.

These are three separate operations.

Do not combine them into one "Scan" button with hidden network mutation.

---

## 38. Offline Engineering

Engineering must be able to define:
- Host Resource;
- Data Source;
- package configuration

while the physical coordinator is temporarily unavailable.

Preview should validate structure.

Connection/activation checks validate current host reality.

This follows #469's accepted pattern.

---

## 39. Project/package portability

Portable project truth:
- HostResource logical reference;
- required adapter family;
- safe transport configuration policy where appropriate;
- Data Source;
- Equipment/TAG/Capability mappings.

Deployment-local state:
- actual COM/Linux path if deployment policy treats it host-specific;
- protected network key;
- protected backup;
- coordinator device permissions;
- sidecar persistent database.

Package import on another host may become:

RESOURCE_REQUIRED
+
PROTECTED_MATERIAL_REQUIRED

until an administrator provisions the destination host.

---

## 40. No automatic resource adoption

If the configured coordinator is missing but another supported dongle is present:

do not auto-adopt it.

Show:
- configured resource unavailable;
- candidate hardware discovered;
- explicit rebind/replacement action.

This prevents accidental connection to another Zigbee network.

---

## 41. No automatic network formation

If a blank/new coordinator is connected:

do not automatically form a network during normal Runtime activation.

New network formation is a privileged commissioning/setup action requiring:
- Protected Material generation;
- network parameters;
- explicit operator/admin intent;
- audit;
- persistent state initialization.

---

## 42. New network creation

Future admin contract should:

1. acquire exclusive blank coordinator;
2. prove/confirm reset state;
3. generate secure random network key;
4. assign PAN/extPAN/channel under policy;
5. write protected material;
6. initialize sidecar database;
7. form network;
8. create protected backup;
9. record coordinator/network identity;
10. close permit join by default.

This is not normal Data Source startup.

---

## 43. Channel changes

Changing Zigbee channel mutates network behavior.

Disposition:

ADMIN COMMISSIONING / MAINTENANCE

Requirements:
- explicit warning;
- compatible device behavior understood;
- bounded operation;
- post-change validation;
- no HMI Command exposure.

---

## 44. Network key changes

Disposition:

ADMIN_FUTURE_SCOPE / WAIT_FOR_STACK_AND_LAB_PROOF

Do not expose merely because a low-level stack function may exist.

Need:
- exact Trust Center behavior;
- device impact;
- sleepy-device handling;
- rollback;
- backup;
- audit.

---

## 45. Reset

Soft reset:
may be a recovery mechanism only if exact adapter semantics are defined.

Hard/factory reset:
DESTRUCTIVE ADMIN

Never execute reset simply because:
- port disconnected;
- startup failed;
- firmware mismatch;
- database could not be read.

---

## 46. Firmware update

Coordinator firmware update:

ADMIN_FUTURE_SCOPE

A future contract must define:
- trusted firmware source;
- adapter model match;
- current/target versions;
- backup;
- sidecar stopped;
- exclusive lease;
- flash tool isolation;
- failure recovery;
- network preservation.

No Runtime auto-update.

---

## 47. Backup schedule

Recommended future policy:

- backup after initial network formation;
- backup after major commissioning changes;
- backup before coordinator firmware update;
- backup before coordinator replacement;
- bounded periodic operational backup where stack/adapter supports it.

Backup retention policy belongs to operations/security.

Do not create unbounded secret backups.

---

## 48. Backup restore guard

Before restore:
- verify backup schema;
- verify cryptographic/protected storage integrity;
- verify adapter family support;
- verify target coordinator;
- verify sidecar/herdsman compatibility;
- quiesce Runtime;
- require admin authorization.

After restore:
- verify network identity;
- verify representative routers;
- verify sleepy devices over time;
- record outcome.

Interview/announce alone is not enough.

---

## 49. Current backup-family conclusion

Current evidence supports backup for first-priority:

zStack

and:

Ember

This is a major reason to choose those two first.

Other herdsman adapter implementations are not automatically accepted for EliteSCADA Native v1.

---

## 50. Lab implications carried to Checkpoint 3

Checkpoint 3 should procure/validate at minimum:

PRIMARY:
one TI zStack coordinator.

SECONDARY:
one Silicon Labs Ember coordinator.

Optional:
one wired Ethernet coordinator.

Tests must include:
- Windows;
- Linux;
- container passthrough;
- disconnect/reconnect;
- sidecar crash;
- coordinator power cycle;
- host restart;
- backup/restore;
- duplicate lease;
- wrong coordinator insertion;
- device matrix.

Exact purchases belong to Checkpoint 3.

---

## 51. Source evidence

Revalidated on 2026-10-06.

### EliteSCADA

- #469 — host-owned serial abstraction and arbitration concepts.
- #500 — canonical communication diagnostics.
- #497/#499 — Protected Material Authority.
- #531 — canonical HOME Equipment/Capability/source binding foundation.

### zigbee-herdsman

- https://github.com/Koenkk/zigbee-herdsman/releases/tag/v11.0.0
- https://github.com/Koenkk/zigbee-herdsman/blob/master/src/adapter/transport.ts
- https://github.com/Koenkk/zigbee-herdsman/blob/master/src/adapter/adapterDiscovery.ts
- https://github.com/Koenkk/zigbee-herdsman/blob/master/src/adapter/z-stack/adapter/zStackAdapter.ts
- https://github.com/Koenkk/zigbee-herdsman/blob/master/src/adapter/ember/adapter/emberAdapter.ts
- https://github.com/Koenkk/zigbee-herdsman/blob/master/src/models/backup-storage-unified.ts

### Adapter documentation

- https://www.zigbee2mqtt.io/guide/adapters/
- https://www.zigbee2mqtt.io/guide/adapters/zstack.html
- https://www.zigbee2mqtt.io/guide/adapters/emberznet.html
- https://www.zigbee2mqtt.io/guide/faq/

### Vendor examples

- https://sonoff.tech/products/sonoff-zigbee-3-0-usb-dongle-plus-zbdongle-p
- https://sonoff.tech/products/sonoff-zigbee-3-0-usb-dongle-plus-zbdongle-e

---

## 52. Contract conclusion

Required future Host Resource:

ZigbeeCoordinator

Ownership:

SERVER_OWNED
+
EXCLUSIVE_LEASE
+
SIDECAR_EXECUTED

Endpoint kinds:

SERIAL
+
TCP

Primary family:

TI zStack / CC2652-class

Secondary family:

Silicon Labs Ember / EFR32MG21/MG24-class

Physical identity:

stable HostResourceId
+
connected coordinator IEEE/fingerprint verification

Transport path:

configuration, not canonical identity

Network secrets:

Protected Material Authority / protected operational state

Backup:

protected operational material

Normal Runtime:

no resource mutation

Commissioning/admin:

network formation / reset / backup / restore / replacement / key change

Required delta:

RESEARCH_CONTRACT_DELTA_REQUIRED — HOST-RESOURCE-GENERALIZATION-01

Related required delta:

RESEARCH_CONTRACT_DELTA_REQUIRED — SIDECAR-LIFECYCLE-01

Scope:

DOCS_ONLY

NO PRODUCT CODE CHANGED

NO DEPENDENCY CHANGED

NO CI CHANGED

NO MERGE PERFORMED
