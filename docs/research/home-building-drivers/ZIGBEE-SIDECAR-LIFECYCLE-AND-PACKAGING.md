# Zigbee Native Sidecar Lifecycle and Packaging — Checkpoint 3

Status: RESEARCH_ONLY / DOCS_ONLY / NO_PRODUCT_CODE / NO_MERGE_BY_RESEARCHER

Issue owner: #541 — HOME-RESEARCH-02

Access/revalidation date: 2026-10-06

This document refines the Native Zigbee managed-sidecar packaging recommendation. It does not authorize an implementation or dependency addition.

---

## 1. Decision

Native Zigbee packaging direction:

**MANAGED_SIDECAR**

Not:
- in-process C# Zigbee stack;
- runtime npm install;
- user-managed Node development environment;
- embedded Zigbee2MQTT.

The product should ship an immutable, versioned sidecar artifact built and qualified by EliteSCADA.

---

## 2. Candidate pinned stack

Current research candidate:

- Node.js **24.21.0 LTS**
- zigbee-herdsman **11.0.0**
- zigbee-herdsman-converters **26.117.1**

Why Node 24 LTS:
- Node project identifies v24 as LTS on 2026-10-06;
- production guidance recommends Active/Maintenance LTS rather than Current;
- herdsman package range includes Node 24;
- official Node binaries exist for Windows x64/ARM64 and Linux x64/ARM64.

Do not pick Node 26 Current merely because it is newer.

Before implementation:
- revalidate herdsman Node engine;
- revalidate native dependency support;
- run L2/L4 on the exact Node patch.

---

## 3. Exact pinning

Pin all of:

- sidecar contract version;
- sidecar source revision;
- Node exact patch;
- package manager exact version used to build;
- zigbee-herdsman exact version;
- zigbee-herdsman-converters exact version;
- every transitive dependency via lockfile;
- build target OS/architecture;
- container base-image digest when containerized.

Do not:
- use caret/range at deployed runtime;
- run npm update on customer host;
- resolve latest on service start.

---

## 4. Build artifact

Recommended artifact contents:

- sidecar JS/build output;
- package metadata;
- exact lockfile;
- production dependencies only;
- third-party license notices;
- SBOM;
- version manifest;
- sidecar contract schema;
- health/version endpoint;
- launcher/supervisor integration files.

If Node runtime is bundled:
- include the exact Node binary distribution;
- include Node LICENSE and required third-party notices.

---

## 5. Version manifest

Sidecar must expose and persist build evidence:

- EliteSCADA sidecar version;
- git/source revision;
- sidecar contract version;
- Node version;
- herdsman version;
- converter version;
- lockfile hash;
- build timestamp;
- target OS;
- target architecture.

This manifest belongs in diagnostics and compatibility records.

---

## 6. Platform targets

Initial recommended support targets:

### Windows x64

**REQUIRED**

Use official Node Windows x64 distribution or an approved self-contained packaging method.

Validate:
- service mode;
- serial USB;
- COM reconnect;
- sidecar restart;
- log/storage paths;
- protected-material injection.

### Linux x64

**REQUIRED**

Validate:
- systemd/service or container;
- /dev/serial/by-id;
- permissions;
- USB reconnect;
- persistent state.

### Linux ARM64

**RECOMMENDED INITIAL**

Reason:
common edge/SBC deployment and official Node ARM64 binaries.

Validate:
- native serialport dependency;
- USB;
- container;
- persistent volume.

### Windows ARM64

**SUPPORTED BY NODE, PRODUCT SUPPORT DEFERRED UNTIL LAB**

Official Node binary exists, but EliteSCADA should not claim Native Zigbee Windows ARM64 support before end-to-end lab evidence.

### Other architectures

Power/s390/AIX are outside the first EliteSCADA Native Zigbee scope even if Node publishes binaries.

---

## 7. Native modules

zigbee-herdsman currently depends on serialport native components.

Packaging must prove:
- compatible prebuild/build for each supported OS/arch;
- no compiler/toolchain needed on customer host;
- deterministic installation;
- no post-install network fetch.

If any target cannot produce a reproducible native module artifact:
classify that target as **WAIT_PLATFORM_PACKAGE**, not as generic product support.

---

## 8. Windows packaging

Recommended:

EliteSCADA installation
-> sidecar artifact directory
-> pinned Node runtime
-> service/supervisor launches sidecar as non-interactive process.

Requirements:
- no user profile dependency;
- no PATH/npm dependency;
- working directory controlled;
- writable state outside immutable application files;
- Windows service identity has serial and persistent-state permissions;
- logs are sanitized.

Upgrade:
- stage new artifact alongside old;
- stop/quiesce old;
- migrate/validate state if needed;
- start new;
- rollback to prior artifact if readiness fails.

---

## 9. Linux packaging

Recommended native package or container deployment.

Native-host requirements:
- sidecar service user;
- stable state directory;
- stable log path;
- /dev permissions;
- no shell/package-manager mutation at runtime.

Container requirements:
- immutable image digest;
- persistent state volume;
- explicit USB device passthrough;
- explicit network access for TCP coordinator;
- protected-material injection;
- restart policy;
- healthcheck.

---

## 10. Container image

A Native Zigbee sidecar container is acceptable if:

- Node exact version pinned;
- herdsman/converters exact versions pinned;
- dependencies locked;
- image digest recorded;
- state outside image;
- coordinator passthrough explicit;
- no privileged container required beyond actual device needs;
- network access bounded;
- logs redacted.

Do not use a floating base tag in release evidence.

---

## 11. Persistent state

Persistent Native Zigbee state must survive:

- EliteSCADA restart;
- sidecar restart;
- host restart;
- application upgrade;
- container replacement.

State includes:
- herdsman database;
- stack operational metadata;
- converter-required device state;
- backup metadata;
- network identity metadata.

Protected secrets are not ordinary sidecar JSON.

---

## 12. State path

Logical model:

immutable sidecar binaries
+
writable operational state
+
protected material

All three must be separate.

Do not write network database into:
- temporary directory;
- container writable layer only;
- installation directory that is replaced on upgrade.

---

## 13. Protected material injection

Use existing EliteSCADA Protected Material Authority.

Recommended lifecycle:

1. supervisor authorizes sidecar instance;
2. trusted host resolves required material;
3. sidecar receives only the minimum material for the running network;
4. no browser exposure;
5. no command-line plaintext when avoidable;
6. no environment-variable plaintext if process inspection policy makes it unsafe;
7. zero/close leases when no longer required.

Exact IPC secret-delivery mechanism remains implementation work.

---

## 14. Coordinator access

Serial coordinator:

sidecar process must have direct access only after ZigbeeCoordinator Host Resource lease.

TCP coordinator:

sidecar receives exact configured endpoint after lease.

Do not open the coordinator from both C# and Node.

---

## 15. Supervisor contract

Required future foundation:

**SIDECAR-LIFECYCLE-01**

Minimum supervisor actions:

- install/register immutable artifact;
- start;
- stop;
- graceful shutdown;
- kill after bounded timeout;
- restart;
- health;
- readiness;
- version;
- stdout/stderr capture with redaction boundary;
- crash detection;
- crash-loop suppression;
- resource lease handoff;
- state-volume binding;
- update;
- rollback.

---

## 16. Start lifecycle

1. select exact artifact version.
2. validate artifact integrity.
3. validate platform/architecture.
4. acquire sidecar instance lock.
5. acquire ZigbeeCoordinator Host Resource lease.
6. mount/resolve persistent state.
7. resolve protected material.
8. launch exact Node binary.
9. sidecar reports handshake.
10. validate contract and dependency versions.
11. validate coordinator identity/network.
12. wait for Ready.
13. connect Data Source runtime.

If any step fails:
- do not form/reset a network automatically;
- release resources safely;
- report exact dependency state.

---

## 17. Stop lifecycle

1. Data Source enters stopping.
2. close permit join.
3. deny new commissioning actions.
4. stop new process writes.
5. drain/cancel bounded operations.
6. flush state.
7. backup if policy requires.
8. close coordinator.
9. sidecar reports stopped or is terminated after timeout.
10. release lease.
11. close protected-material leases.

---

## 18. Crash loop

Supervisor must distinguish:

- one crash;
- repeated crash;
- startup incompatibility.

Recommended behavior:
bounded exponential restart.

After threshold:
**SIDECAR_CRASH_LOOP**

Stop auto-restart until admin intervention or cooldown.

Never factory-reset coordinator as crash recovery.

---

## 19. Health/readiness

Process alive != Ready.

Ready requires:
- correct contract version;
- expected Node/herdsman/converter versions;
- state database usable;
- Host Resource lease active;
- coordinator connected;
- expected coordinator identity;
- expected network identity;
- converter dataset loaded;
- event/report stream running.

---

## 20. Upgrade

A sidecar upgrade is an admin/deployment operation.

Required:

1. exact new artifact selected.
2. release notes reviewed.
3. backup current network/sidecar state.
4. stop old sidecar.
5. retain old artifact.
6. start new.
7. schema/state migration if required.
8. coordinator/network identity validation.
9. representative device checks.
10. promote.

No background automatic dependency upgrade.

---

## 21. Rollback

Rollback requires:
- old immutable artifact retained;
- old compatible state snapshot or reversible migration;
- protected material unchanged unless explicitly migrated;
- same Host Resource.

If state schema is not backward compatible:
upgrade must create a rollback snapshot.

Do not promise rollback without state compatibility evidence.

---

## 22. Converter update

Converter update is effectively protocol behavior update.

Treat it like product code:

- exact version;
- changelog review;
- compatibility diff;
- L0 regression;
- L4 representative devices;
- rollback artifact.

Do not independently update converters while leaving herdsman/sidecar unqualified.

---

## 23. Node update

Node patch/minor update requires:

- native dependency rebuild/prebuild check;
- L2;
- serial USB;
- TCP;
- restart;
- L4 smoke.

Node major update requires broader qualification.

---

## 24. Logging

Allowed:
- sidecar version;
- adapter family;
- device IEEE if product log policy permits identifiers;
- endpoint/cluster IDs;
- sanitized errors;
- timing/counters.

Forbidden:
- network key;
- install code;
- link key;
- raw coordinator backup;
- protected-material plaintext;
- authentication material.

Vendor raw payload may contain sensitive operational data; debug capture must be bounded/admin-controlled.

---

## 25. SBOM

Every release artifact should generate an SBOM containing:

- Node;
- herdsman;
- converters;
- serialport packages;
- all npm production dependencies;
- licenses;
- versions;
- hashes.

SBOM belongs to release evidence.

---

## 26. License notices

Bundle required OSS notices with the sidecar.

At minimum:
- Node license/notices;
- zigbee-herdsman MIT;
- zigbee-herdsman-converters MIT;
- all transitive dependency notices.

Do not infer all transitive packages are MIT.

Node itself includes externally maintained libraries under multiple permissive/open-source licenses.

---

## 27. Integrity

Recommended:
- cryptographic hash of sidecar artifact;
- signed release artifact when product signing infrastructure permits;
- verify before launch/update;
- container digest pin.

No npm install from public registry at customer runtime.

---

## 28. Network egress

Normal Native sidecar does not need open Internet access for process control.

Default:
- no arbitrary egress required.

Future OTA/admin features may require vendor firmware metadata/network access and must be separately governed.

Converter/package updates happen through product deployment, not sidecar Internet self-update.

---

## 29. OTA

Classification:

**ADMIN_FUTURE_SCOPE**

Do not enable device OTA automatically in normal Runtime.

A future OTA package needs:
- source allowlist;
- firmware identity/hash;
- device/model match;
- progress;
- power/network safety;
- audit;
- retry;
- post-update validation.

---

## 30. Packaging matrix

| Target | Research status | Required proof |
| --- | --- | --- |
| Windows x64 | REQUIRED | service + USB + restart + state |
| Linux x64 | REQUIRED | native/container + USB/TCP |
| Linux ARM64 | RECOMMENDED INITIAL | native deps + USB/TCP |
| Windows ARM64 | WAIT_LAB | end-to-end native module evidence |
| Docker x64 | REQUIRED deployment mode | volume + device passthrough |
| Docker ARM64 | RECOMMENDED | ARM64 dependency evidence |
| macOS | OUT OF FIRST SERVER SCOPE | not required |
| other Node architectures | OUT OF SCOPE | no product claim |

---

## 31. Source record

Revalidated 2026-10-06.

https://nodejs.org/en/about/previous-releases
- Node v24 is LTS;
- production guidance favors LTS.

https://nodejs.org/en/download/archive/v24.21.0
- Windows x64/ARM64 binaries;
- Linux x64/ARM64 binaries.

https://github.com/nodejs/node/blob/main/LICENSE
- Node license and third-party notices.

https://github.com/Koenkk/zigbee-herdsman/releases/tag/v11.0.0
- current herdsman release.

https://github.com/Koenkk/zigbee-herdsman/blob/master/package.json
- Node engine and dependencies.

https://github.com/Koenkk/zigbee-herdsman-converters/releases/tag/v26.117.1
- current converters release.

---

## 32. Packaging conclusion

Native delivery:

**MANAGED SIDECAR**

Candidate runtime:

**Node 24.21.0 LTS**

Candidate stack:

**herdsman 11.0.0 + converters 26.117.1**

Distribution:

**IMMUTABLE / PINNED / SBOM / OSS_NOTICES**

Platforms:

**Windows x64 + Linux x64 required**

**Linux ARM64 recommended**

**Windows ARM64 gated by lab**

State:

**PERSISTENT OUTSIDE ARTIFACT**

USB:

**EXPLICIT HOST/CONTAINER PASSTHROUGH**

Upgrades:

**ADMIN / QUALIFIED / ROLLBACK-CAPABLE**

Runtime self-update:

**REJECT**

Required foundation:

**RESEARCH_CONTRACT_DELTA_REQUIRED — SIDECAR-LIFECYCLE-01**

Scope:

DOCS_ONLY

NO PRODUCT CODE CHANGED

NO DEPENDENCY CHANGED

NO CI CHANGED

NO MERGE PERFORMED
