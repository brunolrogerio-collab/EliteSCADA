# Matter + Z-Wave Lab / Validation Matrix

Research date: **2026-10-06**  
Issue owner: **#542 — HOME-RESEARCH-03**  
Contract: **C-HOME-MATTER-ZWAVE-RESEARCH-01**  
Scope: **RESEARCH_ONLY / DOCS_ONLY / NO_PRODUCT_CODE**

This matrix defines the evidence required before EliteSCADA may claim support for Matter Controller or Z-Wave JS integration.

It deliberately separates:

- procurement candidate;
- lab fixture;
- qualified exact fixture;
- supported compatibility claim.

A model name alone is **not** a compatibility claim.

---

# 1. Qualification rule

Every L4 compatibility record must pin:

- protocol;
- vendor;
- exact commercial model;
- exact regional SKU;
- hardware revision;
- firmware version;
- controller/radio firmware where applicable;
- protocol stack/server version;
- OS/platform;
- deployment mode;
- RF/network region;
- test date;
- exact scenarios passed;
- limitations;
- regression owner.

If any of these are unknown:

**PROCUREMENT_CANDIDATE / NOT YET ELITESCADA_COMPATIBLE**

---

# 2. Validation levels

## L0 — deterministic pure contract

No real sidecar or device.

Purpose:

- codec/mapping;
- schema/version negotiation;
- identity;
- secret redaction;
- event/state classification;
- failure classification.

## L1 — fake protocol service

EliteSCADA adapter talks to a deterministic independent fake sidecar/server.

Purpose:

- lifecycle;
- malformed data;
- reconnect;
- inclusion/commissioning state machine;
- error behavior;
- event flood;
- secret absence;
- backup state machine.

## L2 — real independent protocol stack

Use a real upstream server/reference implementation.

Matter:
- real Matter(.js) Server;
- connectedhomeip/reference virtual peers.

Z-Wave:
- real Z-Wave JS Server;
- real zwave-js;
- stack test facilities / controller fixture as needed.

Purpose:

- wire/API/schema interoperability;
- real event structures;
- real stack errors;
- protocol semantics independent from EliteSCADA implementation.

## L3 — canonical EliteSCADA Runtime

Validate end-to-end through:

- Data Source;
- Equipment;
- Capability;
- TAG;
- Command;
- Runtime.WriteAsync;
- cache;
- Alarm;
- Historian;
- Gateway;
- diagnostics;
- Save/Publish/Activate/restart.

No protocol-specific bypass is acceptable.

## L4 — physical devices / controllers

Real hardware, real radio/network, exact firmware.

Only L4 may support concrete hardware compatibility claims.

---

# 3. Common L0 requirements

| ID | Requirement | Matter | Z-Wave |
|---|---|---|---|
| C-L0-01 | sidecar version parsing | yes | yes |
| C-L0-02 | schema negotiation | yes | yes |
| C-L0-03 | unsupported schema fails actionable | yes | yes |
| C-L0-04 | stable physical/source identity | yes | yes |
| C-L0-05 | friendly name not identity | yes | yes |
| C-L0-06 | secret reference redaction | yes | yes |
| C-L0-07 | no secret in logs/errors | yes | yes |
| C-L0-08 | transient event != persistent TAG | yes | yes |
| C-L0-09 | malformed payload bounded | yes | yes |
| C-L0-10 | unknown protocol feature remains diagnostic/unmapped | yes | yes |
| C-L0-11 | capability mapping feature-aware | yes | yes |
| C-L0-12 | portable project contains no protected bytes | yes | yes |

---

# 4. Common L1 sidecar lifecycle matrix

| Scenario | Expected result |
|---|---|
| child executable missing | Blocked / actionable |
| unsupported sidecar version | Blocked / version issue |
| incompatible schema | Blocked / schema issue |
| startup timeout | Failed / bounded |
| clean shutdown | no orphan process/resource |
| forced termination | recorded; state revalidated next start |
| single crash | bounded restart |
| repeated crash | crash-loop Blocked |
| protected material unavailable | fail closed |
| state directory conflict | fail closed |
| local control port conflict | fail closed |
| control channel disconnect | reconnect/backoff |
| child restarts | subscriptions/inventory reconciled |
| event flood | bounded queue/memory |
| malformed event | isolated failure |
| import to different host | resource/credential required |
| Standby attempts ownership | denied |

---

# 5. Matter L0 matrix

## Identity / mapping

- fabric/controller stable identity;
- node ID;
- endpoint;
- cluster ID;
- attribute ID;
- event ID;
- command ID;
- feature/revision.

## Capability mapping

At minimum:

- OnOff;
- Dimmer;
- ColorLight;
- Temperature;
- Humidity;
- Occupancy;
- Contact;
- Power;
- Energy;
- Cover;
- Lock mapping rules;
- Thermostat/Climate;
- Fan.

## Event classification

- Matter event occurrence is not TAG state;
- ordering/timestamp preserved;
- unknown event remains diagnostic.

## Security

- setup code redacted;
- Wi-Fi credential reference;
- Thread dataset reference;
- fabric-private material never exposed;
- attestation failure is admin/security failure.

---

# 6. Matter L1 matrix

Fake sidecar/controller must cover:

1. server startup;
2. schema compatible;
3. schema incompatible;
4. fabric ready;
5. fabric state missing/corrupt;
6. commission success;
7. invalid QR/manual code;
8. PASE failure;
9. attestation failure;
10. Wi-Fi credential missing;
11. Thread dataset missing;
12. Thread Border Router unavailable;
13. BLE unavailable;
14. node joins;
15. endpoint inventory;
16. attribute report;
17. write accepted;
18. write fails;
19. command invoke;
20. subscription loss;
21. subscription restore;
22. node reboot;
23. sidecar restart;
24. controller storage persists;
25. malformed event;
26. event flood;
27. sleepy/ICD long silence;
28. bounded reconnect;
29. multi-admin commissioning-window event;
30. remove-fabric flow;
31. backup metadata validation;
32. secret never appears in log.

---

# 7. Matter L2 matrix

Use real Matter(.js) Server at exact pinned version.

Independent peers:

- connectedhomeip chip-tool/reference virtual devices;
- another independent Matter SDK fixture when useful.

Required evidence:

- QR/manual commission;
- PASE;
- attestation;
- operational credentials;
- CASE;
- node/endpoint discovery;
- read;
- write;
- invoke;
- subscribe;
- event;
- reconnect;
- open commissioning window;
- multi-admin;
- remove fabric;
- state persistence;
- sidecar restart;
- backup/restore;
- Wi-Fi node;
- Thread node where reference tooling supports it.

---

# 8. Matter L3 matrix

EliteSCADA end-to-end:

- Matter Data Source persists;
- Equipment stable identity independent from device label;
- discovery candidate;
- preview/apply;
- TAG registration;
- Capability registration;
- subscription -> cache;
- write -> device -> authoritative read/report;
- Command does not fabricate state;
- Alarm/Historian/Gateway see only canonical TAGs;
- diagnostics follow #500 ladder;
- privileged commissioning is separate from Runtime;
- project export contains no setup/network/fabric secrets;
- restart preserves Data Source/Equipment binding;
- sidecar restart recovers without duplicate Equipment.

---

# 9. Matter L4 procurement candidates

## M-HW-01 — Wi-Fi Matter actuator

**TP-Link Tapo P125M**

Current vendor evidence, accessed 2026-10-06:

- Matter Certified;
- Wi-Fi 2.4 GHz;
- Bluetooth used for setup;
- multi-admin setup documentation;
- TP-Link Brazil support page exposes hardware family V1.

Important electrical constraint from current Brazil product page:

- 100-125 V AC;
- 15 A / 1800 W at 120 V.

Therefore:

- suitable only for a compatible 127 V lab circuit;
- not a generic 220 V Brazil fixture.

Status:

**PROCUREMENT_CANDIDATE / exact hardware suffix + firmware required**

Required record at receipt:

- P125M hardware version;
- exact firmware;
- serial/region;
- Matter certification record if available;
- test date.

## M-HW-02 — Thread contact / sleepy sensor

**Aqara Door and Window Sensor P2**

Vendor evidence:

- native Matter;
- Thread;
- battery;
- Matter-compatible Thread Border Router required.

Status:

**PROCUREMENT_CANDIDATE**

Required record:

- exact regional SKU;
- hardware revision;
- firmware;
- Matter specification/certification record;
- test date.

## M-HW-03 — Thread actuator/router

**Eve Energy — Matter-enabled regional model**

Role:

- mains-powered Thread routing node;
- OnOff;
- power/energy where exposed by exact firmware/spec.

Status:

**PROCUREMENT_CANDIDATE**

Do not claim compatibility until exact regional SKU/hardware/firmware is recorded.

## M-HW-04 — external Thread Border Router

**Google Nest Hub (2nd generation)**

Current Google documentation lists it among hubs supporting Matter over both Wi-Fi and Thread and acting as a Thread Border Router.

Role:

- independent external TBR;
- prove EliteSCADA does not need to own an RCP/OTBR for first Thread slice.

Status:

**LAB INFRASTRUCTURE CANDIDATE**

Record exact software version/date visible at test where available.

## M-HW-05 — BLE host

Recent Linux x64 host with a supported Bluetooth controller.

Exact adapter/chipset must be recorded.

A "Linux has Bluetooth" claim is insufficient.

## M-HW-06 — second Matter admin

Independent controller ecosystem for multi-admin tests.

Candidate:

- Google Home using Nest Hub 2;
- another certified Matter controller.

Purpose:

- commissioning window;
- second-fabric add;
- independent control;
- remove EliteSCADA fabric without deleting other fabric.

---

# 10. Matter L4 scenarios

## Commissioning

- Wi-Fi first commission;
- Thread first commission;
- QR;
- manual code;
- BLE bootstrap;
- on-network flow where supported;
- attestation success;
- attestation failure fixture/simulation;
- recommission after factory reset.

## Runtime

- read;
- write;
- readback/report;
- subscriptions;
- event path;
- device power cycle;
- network interruption;
- controller restart;
- sidecar restart.

## Thread

- Border Router restart;
- IPv6 disabled/misconfigured negative test;
- mDNS disruption;
- external TBR recover;
- sleepy Thread sensor;
- mixed Wi-Fi + Thread nodes.

## Multi-admin

- open commissioning window;
- second admin joins;
- both read/control;
- remove one fabric;
- factory reset.

## DR

- encrypted controller state backup;
- replacement host;
- same fabric identity;
- no mass recommissioning;
- subscriptions restored.

---

# 11. Matter current blockers in L4

Do not promote to production while unresolved:

- selected server successor remains Beta/not re-certified;
- current upstream PASE/CASE resource-exhaustion issues without accepted fix/mitigation;
- unsupported Windows local controller path;
- unproven controller-state DR;
- Thread networking not validated on actual deployment topology;
- legal/certification decision unresolved.

---

# 12. Z-Wave L0 matrix

## Identity

- Data Source/controller context;
- Home ID;
- node ID including 16-bit LR;
- endpoint;
- Command Class;
- property;
- propertyKey.

## Value metadata

- readable;
- writeable;
- type;
- unit;
- min/max;
- states;
- metadata changes.

## Security

- six independent network-key references;
- no raw key in project/log;
- DSK/PIN redaction;
- SmartStart provisioning classified as admin data.

## Event/state

- Central Scene/value notification -> transient event;
- no fake sticky TAG.

## Sleeping write truth

- requested;
- queued;
- confirmed;
- failed/expired.

Canonical TAG stays last confirmed process value.

---

# 13. Z-Wave L1 matrix

Fake Z-Wave JS Server:

1. server version/schema handshake;
2. incompatible schema;
3. controller present;
4. controller absent;
5. unexpected controller identity;
6. resource contention;
7. inclusion start;
8. inclusion stop;
9. requested security classes;
10. S2 grant;
11. DSK/PIN validation;
12. inclusion fail;
13. SmartStart provision;
14. SmartStart unprovision;
15. node added;
16. node removed;
17. interview progress;
18. interview stalls on sleeping node;
19. ready node;
20. dead node;
21. asleep/awake;
22. value update;
23. write success;
24. queued sleeping write;
25. write expires/fails;
26. Central Scene notification;
27. route/health diagnostic;
28. sidecar crash;
29. controller disconnect;
30. reconnect;
31. NVM backup progress;
32. NVM backup failure;
33. incompatible restore rejected;
34. protected material missing;
35. event flood;
36. malformed server frame.

---

# 14. Z-Wave L2 matrix

Use real:

- @zwave-js/server;
- zwave-js;
- exact Node runtime.

Required evidence:

- schema 50 line;
- real Value ID;
- node events;
- interview progress;
- controller methods;
- security callback/event shapes;
- SmartStart APIs;
- NVM backup API;
- restore compatibility behavior;
- sidecar restart;
- controller transport failure.

Where mock/test driver facilities cannot represent RF/controller behavior faithfully, move evidence to L4 rather than overstating L2.

---

# 15. Z-Wave L3 matrix

EliteSCADA end-to-end:

- one Data Source per controller/network;
- ZWaveController Host Resource lease;
- protected keys resolve only in trusted server code;
- node -> Equipment;
- values -> TAGs;
- semantic capabilities;
- Runtime.WriteAsync;
- readback/confirmed state;
- sleeping queued write remains non-confirmed;
- Central Scene uses transient event path or is deferred;
- Alarm/Historian/Gateway consume canonical TAG state;
- diagnostics use #500 ladder;
- restart preserves canonical bindings;
- project/package exports no keys/NVM.

---

# 16. Z-Wave L4 procurement candidates

## Z-HW-01 — primary 800-series controller

**Aeotec Z-Stick 10 Pro — ZWA060**

Current vendor research:

- Gen8/800 series;
- EFR32ZG23;
- S2;
- SmartStart;
- Long Range;
- regional variants;
- controller SDK line 7.22+ in current product material.

Brazil frequency-family candidate:

**ZWA060-B — 921.42 MHz**

Important:

- frequency-family match is not ANATEL product homologation;
- regulatory review required before use as production hardware.

Status:

**PRIMARY PROCUREMENT CANDIDATE / NOT YET ELITESCADA_COMPATIBLE**

## Z-HW-02 — secondary/adversarial 800LR controller

**Zooz ZST39 800LR**

Purpose:

- second-controller migration;
- LR;
- NVM regression;
- #8833 reproduction/guard.

Use only in RF region where SKU is lawful.

Status:

**ADVERSARIAL/SECONDARY CANDIDATE**

## Z-HW-03 — contact + environmental sleepy node

**Aeotec Door Window Sensor 8 — ZWA055**

Role:

- battery;
- contact;
- temperature/humidity;
- 800/LR family.

For Brazil/ANZ lab, exact regional variant must be confirmed.

Status:

**PROCUREMENT CANDIDATE**

## Z-HW-04 — motion/illuminance/temperature

**Aeotec TriSensor 8 — ZWA045**

Role:

- motion;
- temperature;
- illuminance;
- battery;
- S2/800/LR family.

Exact regional variant required.

## Z-HW-05 — US-only actuator/meter candidate

**Zooz ZEN04 800LR**

Role:

- OnOff;
- power/energy;
- S2;
- LR.

Do not use in Brazil operational RF lab unless exact regional/legal suitability exists.

## Z-HW-06 — US-only dimmer/scene candidate

**Zooz ZEN77 800LR**

Role:

- Dimmer;
- OnOff;
- Central Scene / multi-tap;
- LR.

Useful in a US-region protocol lab.

## Z-HW-07 — sleepy environmental alternate

**Zooz ZSE44 800LR**

Role:

- battery;
- temperature;
- humidity;
- S2/SmartStart/LR.

US-region candidate only unless vendor provides correct regional variant.

## Z-HW-08 — lock

Deferred.

Only procure after Main explicitly includes access-control scope.

---

# 17. Z-Wave Brazil RF lab rule

Current Silicon Labs global region table, accessed 2026-10-06:

Brazil:
- regulator reference: ANATEL;
- 919.8 MHz / 921.4 MHz;
- region family: ANZ.

Lab policy:

- controller and nodes must use same legal region;
- exact model/SKU must be checked;
- product homologation must be verified;
- do not mix 908.42 MHz US fixtures into Brazil RF tests.

Frequency reference is not a legal opinion.

---

# 18. Z-Wave L4 scenarios

## Controller

- initial bind;
- stable identity;
- unplug/replug;
- COM/device path changes;
- wrong replacement controller;
- service restart;
- sidecar restart;
- controller firmware recorded;
- RF region verified.

## Security

- S0 legacy only where needed;
- S2 Unauthenticated;
- S2 Authenticated;
- S2 Access Control only if later in scope;
- SmartStart;
- DSK confirmation;
- security downgrade failure.

## Nodes

- mains router;
- sleepy battery;
- metering;
- contact;
- motion;
- dimmer;
- scene event;
- LR node where lawful.

## Interview / sleep

- fast mains-node interview;
- sleeping-node delayed interview;
- lastAwake;
- write queued during sleep;
- wake + confirm;
- repeated desired write dedup/regression.

## RF/network

- node out of range;
- weak route;
- controller jam/unresponsive simulation where safe;
- route/lifeline health;
- restart without whole-network storm.

## NVM

- backup;
- post-backup traffic;
- verify Node ID width;
- same-controller restore;
- spare-controller restore/migration;
- secure nodes work after restore;
- LR nodes work after restore.

---

# 19. Mandatory #8833 regression

Current open upstream issue:

**zwave-js/zwave-js #8833**

A future L4 LR controller qualification must explicitly test:

1. LR/16-bit node IDs active;
2. start NVM backup;
3. generate node traffic during/after backup;
4. verify controller returns to correct Node ID mode;
5. verify no sustained invalid-payload drops;
6. verify no nodes become falsely dead;
7. repeat multiple times;
8. restart/backup soak.

Until upstream issue is fixed and exact stack/controller combination passes:

**NO DEFAULT UNATTENDED LR NVM BACKUP CLAIM**

---

# 20. Cross-platform matrix

| Platform | Matter | Z-Wave | Required evidence |
|---|---|---|---|
| Linux x64 native/service | primary | primary | sidecar/service/network/USB |
| Linux x64 container | primary candidate | candidate | host network / device passthrough |
| Linux ARM64 | qualify | qualify | runtime/native dependency test |
| Windows x64 native | upstream Matter server unsupported | candidate | Z-Wave service + COM/USB |
| Windows container/WSL | Matter not qualified | advanced only | no inferred support |
| macOS | upstream Matter supports | dev/non-product priority | optional |
| HA two-node | deferred explicit qualification | deferred explicit qualification | no double ownership |

---

# 21. Common backup / DR validation matrix

| Artifact | Matter | Z-Wave | Protected? | Portable project? |
|---|---|---|---|---|
| project config | yes | yes | non-secret | yes |
| opaque secret refs | yes | yes | non-secret refs | conditionally |
| protected material bytes | Wi-Fi/Thread/etc | S0/S2/LR | yes | no |
| sidecar state | fabric/controller | host cache/provisioning | yes | no |
| hardware NVM | n/a | controller NVM | yes | no |
| backup encryption key | yes | yes | yes | no |

DR test must prove that restoring project alone is insufficient and produces truthful missing-resource/credential state instead of silently generating new identity.

---

# 22. Common scale targets

Targets are qualification goals, not protocol limits.

## Matter

Synthetic/reference:

- 100 nodes;
- 1,000 subscribed attributes;
- mixed Wi-Fi/Thread;
- reconnect storm;
- sidecar restart;
- 24h/72h soak.

## Z-Wave

Synthetic:

- 100 nodes;
- mixed endpoints/values;
- sleeping population;
- queued writes;
- event bursts.

Physical initial:

- >=20 nodes if lab budget permits.

Large-network later:

- 100+ physical/reference;
- memory/queue trend;
- restart behavior;
- 24h/72h soak.

---

# 23. Pass/fail rules

A protocol/hardware record passes only if:

- exact versions recorded;
- all required safety cases pass;
- no unexplained sidecar/controller crashes;
- no unbounded queue/memory growth;
- no secret leakage;
- restart is deterministic;
- canonical TAG truth remains correct;
- unsupported features remain explicitly unsupported;
- physical identity survives friendly-name/address changes;
- backup/restore evidence exists where required.

A single successful read is not compatibility qualification.

---

# 24. Compatibility claim vocabulary

Allowed statuses:

- RESEARCH_CANDIDATE
- PROCUREMENT_CANDIDATE
- L0_VALIDATED
- L1_VALIDATED
- L2_VALIDATED
- L3_VALIDATED
- L4_VALIDATED
- ELITESCADA_SUPPORTED
- BLOCKED
- DEPRECATED

Only Main/release governance should promote a fixture to:

**ELITESCADA_SUPPORTED**

Research branch may only recommend candidates and acceptance criteria.

---

# 25. Evidence record template

For every L4 fixture:

## Identity

- Protocol:
- Vendor:
- Product:
- Model:
- Regional SKU:
- Hardware revision:
- Serial/lot:
- Firmware:
- SDK/API:
- Certification ID:
- RF/network region:

## EliteSCADA environment

- EliteSCADA commit:
- OS:
- Architecture:
- Container/native:
- Sidecar:
- Sidecar version:
- Core library:
- Schema:
- Host Resource:
- Date:

## Scenarios

- Commission/include:
- Read:
- Write:
- Report/subscription:
- Restart:
- Device power cycle:
- Network/RF interruption:
- Sleep:
- Event:
- Backup:
- Restore:
- Migration:
- Security:

## Result

- PASS / FAIL / LIMITED
- Known limitations:
- Logs/artifacts:
- Reviewer:

---

# 26. Lab procurement sequence

## Phase 1 — low-risk functional

Matter:
- Tapo P125M on compatible 127 V circuit;
- Linux controller host.

Z-Wave:
- one 800 controller;
- one region-correct S2 actuator.

## Phase 2 — sleepy / event

Matter:
- Aqara P2;
- external TBR.

Z-Wave:
- battery contact/environment sensor;
- scene/dimmer fixture in legal RF region.

## Phase 3 — DR / mixed network

Matter:
- second admin;
- Thread actuator/router;
- replacement controller host.

Z-Wave:
- spare controller;
- metering;
- LR fixture;
- NVM migration.

Do not buy a large fleet before Phase 1 validates the architecture.

---

# 27. Checkpoint 3 lab conclusion

The L0-L4 strategy is sufficient to release future implementation work without inventing protocol limits or universal compatibility claims.

Main principles:

- exact fixture evidence;
- current firmware;
- region-aware hardware;
- no model-name-only claim;
- no universal Matter/Z-Wave compatibility claim;
- DR is part of production readiness;
- transient events and sleeping nodes get dedicated evidence;
- Brazil RF/electrical constraints must be honored.

---

# 28. Current external source ledger

Accessed/revalidated 2026-10-06.

Matter:
- https://www.tp-link.com/br/home-networking/smart-plug/tapo-p125m/
- https://www.tp-link.com/br/support/download/tapo-p125m/
- https://www.aqara.com/us/product/door-and-window-sensor-p2
- https://support.google.com/googlehome/answer/12391458

Z-Wave:
- https://www.silabs.com/wireless/z-wave/global-regions
- https://aeotec.com/products/aeotec-z-stick-10-pro/
- https://aeotec.com/products/door-window-sensor-8/
- https://store.aeotec.com/products/trisensor-8-zwa045
- Z-Wave JS upstream issues/releases referenced in ZWAVE-JS-READINESS-RESEARCH.md

---

**DOCS_ONLY**

**NO PRODUCT CODE CHANGED**

**NO HARDWARE COMPATIBILITY CLAIM CREATED**

**NO RF TRANSMISSION PERFORMED**

**NO MERGE PERFORMED**
