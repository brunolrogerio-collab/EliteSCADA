# Zigbee Lab / Compatibility / Validation — Checkpoint 3

Status: RESEARCH_ONLY / DOCS_ONLY / NO_PRODUCT_CODE / NO_MERGE_BY_RESEARCHER

Issue owner: #541 — HOME-RESEARCH-02 — Zigbee2MQTT + Native Zigbee execution dossier

Contract: C-HOME-ZIGBEE-RESEARCH-01

Order: HOME-ZIGBEE-Z2M-NATIVE-RESEARCH-01

Research branch: research/home-zigbee-execution

Release base:
wave15/corrections-integration@69d5248e462ef12c41d99c6859a3afb77c6e3e2d

Checkpoint 2 HEAD:
acf0c1f2b147198fdffe978f260a1891b08831a0

Access/revalidation date: 2026-10-06

This document is CHECKPOINT 3 only. It defines the hardware lab, compatibility evidence model, L0-L4 validation ladders, scale targets, final research classifications and recommended implementation order. It does not execute tests or authorize product implementation.

---

## 1. Final research classifications

### Zigbee2MQTT

FINAL RESEARCH CLASSIFICATION:

**GO_WITH_GATES**

Preferred first implementation route:

EliteSCADA
-> MQTT broker
-> user-managed Zigbee2MQTT
-> coordinator
-> Zigbee mesh

Why:
- lowest product-owned Zigbee stack risk;
- mature documented MQTT contract;
- mature device/converter ecosystem;
- can reuse canonical Equipment/Capability/TAG/Command;
- does not require EliteSCADA to own radio lifecycle in v1;
- avoids GPL redistribution if Z2M remains user-managed external.

Principal gates:
- protected MQTT credentials;
- retained/stale/restart behavior;
- write/readback truth;
- transient event contract for buttons/remotes;
- explicit device qualification.

### Native Zigbee

FINAL RESEARCH CLASSIFICATION:

**GO_WITH_GATES**

Preferred architecture:

EliteSCADA
-> managed Zigbee sidecar
-> ZigbeeCoordinator Host Resource
-> pinned zigbee-herdsman
-> pinned zigbee-herdsman-converters
-> physical coordinator
-> mesh

Principal gates:
- SIDECAR-LIFECYCLE-01;
- HOST-RESOURCE-GENERALIZATION-01;
- DRIVER-TRANSIENT-EVENT-01;
- protected network key/backup path;
- exact pinned stack qualification;
- physical TI + Ember proof;
- same-family backup/restore proof.

### Recommended implementation order

**Z2M FIRST**

Then:

**NATIVE AFTER FOUNDATION**

Not full parallel implementation.

Reason:
- Z2M can validate canonical device/Capability/TAG/import/process-truth semantics before EliteSCADA owns radio and mesh lifecycle;
- Native requires two additional common foundations;
- the same device matrix can later be reused to compare Z2M versus Native;
- failures become attributable to bridge semantics versus direct stack semantics.

---

## 2. Current upstream baseline used by this matrix

Revalidated 2026-10-06:

Zigbee2MQTT:
- 2.14.2
- GPL-3.0
- release 2026-10-01

Native candidate:
- zigbee-herdsman 11.0.0
- MIT
- release 2026-10-06
- breaking change: unified transport

Converter candidate:
- zigbee-herdsman-converters 26.117.1
- MIT
- release 2026-10-06

The lab must pin exact versions and must never silently use "latest".

---

## 3. Hardware purchase list

This is a first qualification lab, not a claim that these are the only supported devices.

### 3.1 Coordinators

#### Primary coordinator — required

**SONOFF ZBDongle-P**

Observed official facts:
- model ZBDongle-P;
- TI CC2652P;
- USB CP2102(N);
- Z-Stack coordinator firmware;
- official vendor price observed 2026-10-06: USD 24.90 before tax/shipping.

Purpose:
- PRIMARY_COORDINATOR_FAMILY baseline;
- TI zStack;
- USB/serial;
- Windows/Linux/container passthrough;
- backup/restore qualification.

Quantity:
**2**

Reason:
one active + one replacement target for same-family backup/restore.

#### Secondary coordinator — required

**SONOFF ZBDongle-E**

Observed official facts:
- model ZBDongle-E;
- Silicon Labs EFR32MG21;
- USB;
- Ember/EZNet family coordinator firmware;
- official vendor price observed 2026-10-06: USD 24.90 before tax/shipping.

Purpose:
- SECONDARY_COORDINATOR_FAMILY baseline;
- independent Silicon Labs/Ember path;
- backup/restore qualification;
- cross-family behavioral comparison.

Quantity:
**2**

Reason:
one active + one replacement target.

#### Ethernet coordinator — optional but recommended

**SONOFF Dongle Max / Dongle-M**

Observed official facts:
- EFR32MG24 + ESP32;
- Ethernet/PoE, Wi-Fi and USB;
- vendor docs list Windows, Ubuntu, Raspberry Pi OS/Raspbian and Docker;
- official vendor price observed 2026-10-06: USD 42.90 before tax/shipping.

Purpose:
- TCP/remote coordinator Host Resource;
- wired Ethernet qualification;
- network disconnect/reboot/latency tests;
- Docker deployment.

Quantity:
**1**

Qualification rule:
use wired Ethernet/PoE first.
Do not use Wi-Fi as the primary remote-coordinator acceptance transport.

Alternative:
SMLIGHT SLZB-06U / SLZB-06P7U may be substituted as a TI Ethernet coordinator if the lab wants zStack-over-Ethernet specifically.

### 3.2 Device matrix

#### A. Mains-powered router / standard light

**IKEA LED2201G8**

Role:
- mains router/light;
- OnOff;
- brightness;
- color temperature;
- standard reporting/read/write;
- OTA-capable device for future admin-only investigation.

Why:
- exercises a mainstream standards-oriented vendor;
- useful contrast to vendor-specific devices.

Quantity:
**2**

#### B. Full dimmable/color light

**Philips Hue 9290012574**

Role:
- ColorLight;
- OnOff;
- brightness;
- color temperature;
- XY/HS color;
- effects;
- power-on behavior.

Quantity:
**1**

Acceptance:
verify canonical color model mapping does not create contradictory state between XY/HS/color temperature paths.

#### C. Smart plug with Power/Energy

**SONOFF S60ZBTPG** or exact regional S60 Zigbee variant that maps to the same supported converter contract.

Known upstream model page:
S60ZBTPG exposes:
- switch;
- current;
- voltage;
- power;
- energy;
- daily/month energy;
- protection/configuration functions.

Quantity:
**2**

Purchase guard:
because physical plug/socket variants differ by market, record the exact purchased model string before qualification.
Do not promote another S60 suffix based only on similar enclosure.

#### D. Contact sensor

**SONOFF SNZB-04P**

Role:
- Contact;
- Battery;
- battery voltage;
- battery low;
- tamper.

Quantity:
**2**

Purpose:
- passive battery device;
- report-only process truth;
- wake/sleep behavior;
- battery diagnostics.

#### E. Occupancy / PIR

**SONOFF SNZB-03P**

Role:
- Occupancy;
- illumination;
- battery;
- configurable timeout.

Quantity:
**2**

Purpose:
- event-driven sensor reporting;
- occupancy reset semantics;
- read/report behavior.

#### F. Temperature / humidity

**SONOFF SNZB-02P**

Role:
- Temperature;
- Humidity;
- Battery.

Quantity:
**2**

Purpose:
- periodic passive reports;
- sleepy-device behavior;
- calibration/configuration separation.

#### G. Explicit sleepy/quirky battery device

**Aqara WSDCGQ11LM**

Role:
- temperature;
- humidity;
- pressure;
- battery;
- voltage.

Quantity:
**1**

Reason:
the upstream documentation explicitly notes Xiaomi/Aqara behavior that is not fully Zigbee-standard-compliant and can disconnect in some environments.

This is intentionally a quirk/interoperability test, not merely another temperature sensor.

#### H. Cover

**SONOFF MINI-ZBRBS**

Role:
- Cover;
- state;
- position;
- stop;
- calibration/configuration.

Quantity:
**1**

Important:
cover calibration is commissioning/configuration, not normal process state.

Bench safety:
this is a mains-wired actuator.
Use an isolated/qualified lab fixture and appropriate electrical safety controls.

#### I. Remote/button with actions

**SONOFF SNZB-01P**

Role:
- transient actions such as single/double/hold;
- battery;
- sleeping remote.

Quantity:
**2**

Purpose:
prove that action events remain TRANSIENT_EVENT and never become fake persistent TAG state.

#### J. Vendor-specific quirk / complex converter

**Tuya TS0601 thermostat family**

Preferred purchasable white-label examples:
- Moes HY368;
- Moes HY369RT.

Role:
- Climate;
- vendor datapoints;
- schedules;
- presets;
- local temperature;
- setpoint;
- child lock;
- time synchronization quirks.

Quantity:
**1**

Purchase guard:
TS0601 is not a unique physical model.
Record exact manufacturer string and converter match from the purchased device.
Do not qualify "TS0601" generically.

#### K. Install-code device

Initial lab classification:

**OPTIONAL / NOT REQUIRED FOR FIRST GO**

Reason:
consumer lab devices commonly use ordinary Zigbee 3.0 commissioning and a repeatable install-code-capable device may be harder to source.

If a commercial/install-code device is procured later:
- record exact model and install-code format;
- treat code as protected material;
- add install-code commissioning to L4.

Do not delay first Zigbee bridge qualification solely for lack of install-code hardware.

---

## 4. Minimum physical inventory

Recommended initial purchase:

- 2 x ZBDongle-P
- 2 x ZBDongle-E
- 1 x Ethernet coordinator optional/recommended
- 2 x IKEA LED2201G8
- 1 x Philips Hue 9290012574
- 2 x S60 Zigbee power plugs
- 2 x SNZB-04P
- 2 x SNZB-03P
- 2 x SNZB-02P
- 1 x Aqara WSDCGQ11LM
- 1 x MINI-ZBRBS
- 2 x SNZB-01P
- 1 x Tuya TS0601 thermostat-family device

Core device count excluding coordinators:
**16**

This is large enough to test mixed router/end-device behavior without pretending to be a scale limit.

---

## 5. Compatibility record

Interview success is not compatibility.

Every validated device record must include:

- lab record ID;
- date;
- tester/build;
- manufacturer;
- retail model;
- Zigbee model ID;
- Zigbee manufacturer name/string;
- firmware/software build ID if available;
- IEEE captured for lab traceability;
- coordinator model;
- coordinator family;
- coordinator firmware;
- transport USB/TCP;
- OS;
- architecture x64/ARM64;
- container or host-native;
- Zigbee2MQTT version where applicable;
- zigbee-herdsman version;
- zigbee-herdsman-converters version;
- converter definition identity;
- interview result;
- supported/unmapped result;
- endpoints;
- expected capabilities;
- actual canonical capabilities;
- reads tested;
- writes tested;
- reporting tested;
- availability tested;
- sleepy behavior tested;
- reconnect tested;
- coordinator restart tested;
- application restart tested;
- power-cycle tested;
- stale/retained behavior tested where Z2M;
- backup/restore survival where relevant;
- known quirks;
- excluded exposes;
- transient events;
- pass/fail;
- notes.

Promotion states:

DISCOVERED
-> INTERVIEWED
-> MAPPED
-> FUNCTIONAL
-> RESTART_VALIDATED
-> QUALIFIED

Only QUALIFIED models may appear in an EliteSCADA compatibility claim.

---

## 6. Qualification rules

A device is not qualified merely because:
- Zigbee2MQTT lists it;
- a converter matches;
- interview succeeds;
- state appears once;
- one write succeeds;
- Home Assistant or another product supports it.

Minimum qualification requires:
- stable identity;
- semantic mapping;
- state/report path;
- relevant write/readback path;
- restart/reconnect evidence;
- quality/failure semantics;
- pinned stack/converter/coordinator evidence.

---

## 7. L0-L4 — Zigbee2MQTT

### L0 — pure contract / mapping

No broker or hardware.

Required deterministic tests:

1. IEEE identity normalization.
2. friendly_name rename does not change canonical Equipment identity.
3. endpoint/property binding uniqueness.
4. bridge/devices parser.
5. malformed bridge inventory.
6. definition=null unsupported device.
7. generic expose parsing.
8. nested composite features.
9. access bitmask 1/2/4/5/7.
10. config category excluded from process mapping.
11. diagnostic category excluded from process mapping.
12. OnOff mapping.
13. Light mapping.
14. Dimmer mapping.
15. ColorLight mapping.
16. Temperature/Humidity.
17. Occupancy/Illuminance.
18. Contact/Leak/Smoke.
19. Battery.
20. Power/Energy/Voltage/Current.
21. Cover.
22. Lock.
23. Climate.
24. Fan.
25. unknown expose remains unmapped.
26. action/button becomes TRANSIENT_EVENT.
27. set-only expose becomes Command.
28. stateful set does not immediately become Good TAG.
29. get-capable readback mapping.
30. groups/scenes do not become physical Equipment by default.

Pass:
100% deterministic expected mapping.

### L1 — fake MQTT / fake Z2M

Required scenarios:

1. bridge/state online.
2. bridge/state offline.
3. retained bridge/devices.
4. retained device availability.
5. retained device state.
6. non-retained device state.
7. persistent cached state after simulated restart.
8. malformed JSON.
9. missing expose fields.
10. duplicate friendly names rejected/handled safely.
11. friendly-name rename while running.
12. device leave/rejoin.
13. broker disconnect/reconnect.
14. bridge restart.
15. event flood.
16. repeated same state.
17. out-of-order state.
18. stale cached state matching a pending write.
19. pending write then device reports different value.
20. get timeout.
21. write timeout.
22. write then matching report.
23. sleeping device no immediate response.
24. availability disabled.
25. availability active/passive difference.

Event flood target:
at least 1,000 semantic/event messages in a bounded synthetic burst with:
- no process crash;
- no unbounded queue;
- preserved canonical ordering rules;
- bounded diagnostics.

### L2 — disposable real Zigbee2MQTT + broker

Real upstream service, no EliteSCADA production integration.

Pin:
- Z2M 2.14.2;
- exact broker image/version;
- exact coordinator adapter config for hardware-free/fake path where possible.

Validate:
- startup;
- bridge retained topics;
- cache republish on restart;
- MQTT reconnect;
- inventory;
- rename;
- request/response transactions;
- availability;
- set/get;
- shutdown;
- upgrade fixture only as separate experiment.

Pass:
documented behavior matches parser/runtime assumptions.

### L3 — EliteSCADA canonical Runtime

Future implementation gate.

Validate:
- Data Source lifecycle;
- selected discovery;
- preview;
- apply;
- Equipment source binding;
- Capability role bindings;
- TAG updates;
- Commands;
- quality/freshness;
- Historian;
- Alarm;
- Gateway;
- scripts consuming canonical TAG only;
- diagnostics;
- save/revision/publish/activate;
- restart;
- package round-trip.

No raw Z2M state should bypass canonical TAG authority.

### L4 — real coordinator + device matrix via Z2M

Run complete physical matrix on:
- primary TI coordinator;
- secondary Ember coordinator;
- optional Ethernet coordinator.

For every device:
- join;
- interview;
- mapping;
- read/report;
- write if applicable;
- readback/reconciliation;
- restart Z2M;
- restart broker;
- restart EliteSCADA;
- coordinator power cycle;
- device power/battery cycle where possible;
- leave/rejoin;
- renamed friendly_name;
- availability;
- quality degradation;
- recovered state.

Required special tests:
- remote action events;
- cover calibration vs process state;
- Tuya quirk device;
- Aqara sleepy/non-standard behavior;
- smart plug metering;
- color light transitions;
- stale cached state after Z2M restart.

---

## 8. L0-L4 — Native Zigbee

### L0 — sidecar and semantic contract

No hardware.

Required:

1. sidecar version handshake.
2. incompatible contract version.
3. coordinator identity schema.
4. network identity schema.
5. device identity by IEEE.
6. endpoint/cluster/attribute normalization.
7. converter/expose semantic mapping.
8. read request/response.
9. write request/correlation.
10. report stream.
11. transient event stream.
12. configure reporting admin classification.
13. bind/unbind admin classification.
14. permit join admin classification.
15. install-code protected classification.
16. remove device destructive admin classification.
17. backup/restore protected lifecycle.
18. raw unknown cluster remains unmapped.
19. quality/freshness model.
20. no optimistic Good after write dispatch.
21. protected values absent from logs/DTOs.

### L1 — fake sidecar

Required scenarios:

1. Starting -> Ready.
2. sidecar crash.
3. crash loop.
4. coordinator disconnect.
5. coordinator reconnect.
6. wrong coordinator identity.
7. wrong network identity.
8. join event.
9. rejected join.
10. interview success.
11. interview failure.
12. reinterview.
13. device leave.
14. report storm.
15. event storm.
16. failed read.
17. failed write.
18. write timeout.
19. later disagreeing report.
20. backup unavailable.
21. restore incompatible.
22. protected material unavailable.
23. Host Resource lease conflict.
24. graceful shutdown.
25. persistent state reopen.

Synthetic report/event storm target:
at least 1,000 messages in a bounded burst with:
- bounded memory/queue;
- no duplicate canonical mutation outside rules;
- recoverable backpressure;
- truthful degraded state if overloaded.

### L2 — independent actual native stack/service

Run actual pinned Node:
- Node 24.21.0 LTS candidate;
- zigbee-herdsman 11.0.0;
- converters 26.117.1.

No product integration yet.

Validate:
- sidecar startup;
- serial transport;
- TCP transport;
- zStack adapter;
- Ember adapter;
- persistent database;
- backup;
- converter load;
- controlled shutdown/restart;
- exact dependency lock.

### L3 — EliteSCADA Runtime + LocalBridge + Host Resource

Future gate after foundations.

Validate:
- managed sidecar lifecycle;
- HostResourceId;
- exclusive lease;
- protected-material injection;
- Data Source activation;
- candidate/preview/apply;
- canonical Equipment/TAG/Command/Capability;
- diagnostics;
- restart;
- package/lifecycle;
- rollback.

Required negative:
two Data Sources cannot own one coordinator.

### L4 — physical Native matrix

Run the same core device matrix used by Z2M.

Coordinator combinations:
- ZBDongle-P;
- replacement ZBDongle-P;
- ZBDongle-E;
- replacement ZBDongle-E;
- optional Ethernet coordinator.

Required:
- new network formation;
- secure random network key;
- join bounded;
- interview;
- converter configuration;
- report/read/write;
- sidecar restart;
- host restart;
- coordinator disconnect/reconnect;
- wrong coordinator insertion;
- network identity mismatch;
- same-family backup/restore;
- post-restore devices;
- sleepy-device recovery;
- router power cycle;
- reporting storm;
- event remote actions.

Cross-family restore:
record as experiment only.
Do not treat failure as a v1 blocker unless product chooses to promise it.

---

## 9. Backup/restore L4 qualification

### TI zStack

1. Form network on TI coordinator A.
2. Pair at least:
   - one router light;
   - one plug;
   - one sleepy sensor;
   - one remote.
3. Capture protected backup.
4. Stop sidecar.
5. Replace with TI coordinator B.
6. Restore.
7. Verify network identity.
8. Verify router connectivity.
9. Verify sleepy device after next wake/report.
10. Verify write/readback.
11. Restart again.

Pass:
same-family replacement preserves usable network without routine re-pairing for the qualified fixture.

### Ember

Repeat with Ember coordinator A/B.

### Failure evidence

If some devices must rejoin:
record exact device and firmware.
Do not generalize from one failure.

---

## 10. Scale plan

These are validation targets, not advertised maximums.

### Stage S — semantic baseline

16 physical devices from the purchase matrix.

Pass:
all intended capability classes exercised.

### Stage M — 25 physical nodes

Target composition:
- at least 8 powered routers;
- at least 17 battery/end devices;
- mixed vendors.

Validate:
- join;
- route recovery;
- reporting;
- restart;
- reconnect;
- sidecar/broker queues.

### Stage L — 50 physical nodes

Run only after Stage M is stable.

Validate:
- startup convergence time;
- full inventory;
- memory;
- CPU;
- report bursts;
- command latency;
- restart recovery;
- coordinator power cycle;
- broker/sidecar interruption.

No product maximum claim may be derived solely from this test.

### Synthetic scale

L1/L3 may additionally exercise:
- 100 logical devices;
- 1,000 simultaneous property updates;
- event bursts.

Synthetic success does not replace L4 radio evidence.

---

## 11. Performance measurements

Record for every L3/L4 run:

- device count;
- router/end-device mix;
- coordinator;
- OS/architecture;
- container/native;
- CPU;
- memory;
- startup-to-ready;
- inventory synchronization duration;
- average/95p read latency;
- average/95p write-to-confirm duration;
- report throughput;
- event throughput;
- queue depth;
- dropped/rejected messages;
- reconnect duration;
- restart-to-Good duration;
- backup duration;
- restore duration.

Do not define a hard SLA until evidence exists.

---

## 12. Failure injection

Required failure cases:

### Common

- coordinator unplugged;
- coordinator replugged;
- coordinator power cycled;
- host reboot;
- network interference;
- device offline;
- sleepy device silent;
- router removed;
- duplicate command;
- delayed report;
- wrong report value;
- malformed device data.

### Z2M

- broker down;
- broker auth failure;
- TLS trust failure;
- Z2M process restart;
- Z2M stale cache replay;
- availability disabled;
- friendly-name rename;
- retained messages disabled.

### Native

- sidecar killed;
- sidecar crash loop;
- Host Resource lease conflict;
- protected network key unavailable;
- protected backup unavailable;
- wrong physical coordinator;
- incompatible sidecar contract;
- corrupt sidecar database;
- backup restore mismatch.

---

## 13. Process truth acceptance

### Reads

A TAG becomes Good only from:
- authoritative report;
- successful explicit read/get;
- accepted current state with freshness policy.

### Writes

Requested value remains pending until an appropriate confirmation path.

Pass criteria:
- MQTT publish alone never marks Good;
- sidecar RPC success alone never marks Good;
- timeout becomes unconfirmed/failure;
- conflicting later report wins canonical state;
- stale bootstrap cache is distinguishable.

---

## 14. Availability/freshness acceptance

For every battery class:
- record normal report interval;
- record expected sleep behavior;
- distinguish sleeping from offline;
- do not ping passive devices as though mains-powered;
- verify availability after restart.

For mains devices:
- offline must be detectable within defined policy;
- reconnect must restore truthful quality.

---

## 15. Transient event acceptance

Devices:
- SNZB-01P;
- optional IKEA remote.

Validate:
- single;
- double;
- hold/release if supported;
- bursts;
- repeated identical actions.

Pass:
- events are delivered as events/diagnostics;
- no persistent fake state TAG;
- no stale action replay as current truth.

Until DRIVER-TRANSIENT-EVENT-01 exists:
- event-bearing devices may be marked PARTIALLY_SUPPORTED;
- battery/status TAGs may still be valid;
- action automation is not a v1 supported claim.

---

## 16. Cover acceptance

MINI-ZBRBS:

Validate:
- OPEN;
- CLOSE;
- STOP;
- position;
- movement reports;
- calibration status;
- restart;
- power cycle.

Product semantic rule:
- calibration actions are commissioning/configuration;
- current position/state are process semantics;
- write target is not Good until reconciled.

---

## 17. Climate/vendor quirk acceptance

Tuya TS0601 thermostat family:

Record exact:
- retail label;
- model;
- manufacturer string;
- firmware.

Validate:
- current temperature;
- setpoint;
- mode;
- running state;
- preset;
- child lock;
- relevant schedules/config;
- time synchronization requests;
- restart.

Do not map all configuration properties into normal process TAGs.

This device exists in the lab specifically to detect converter-version sensitivity.

---

## 18. Color-light acceptance

Philips Hue 9290012574:

Validate:
- on/off;
- brightness;
- color temperature;
- XY color;
- HS color;
- transition;
- readback/report;
- power cycle.

Canonical mapping should expose one coherent ColorLight capability rather than independent contradictory color truths.

---

## 19. Metering acceptance

Smart plug:

Validate:
- OnOff;
- current;
- voltage;
- power;
- energy;
- energy persistence semantics;
- load-off zero behavior;
- restart.

Use a safe known test load.

Do not infer calibration accuracy solely from converter values.
Lab may compare with a reference meter if metrology accuracy becomes a product claim.

---

## 20. Qualification result categories

PASS

PASS_WITH_QUIRK

PARTIALLY_SUPPORTED

UNSUPPORTED

BLOCKED_BY_EVENT_CONTRACT

BLOCKED_BY_FOUNDATION

FAIL

Every non-PASS state requires explicit reason.

---

## 21. Compatibility publication rule

Public/support documentation should say only:
- tested;
- qualified;
- known limitations.

Do not say:
- "supports all Zigbee devices";
- "all Zigbee2MQTT devices are supported";
- "all CC2652 coordinators are supported";
- "all Ember coordinators are supported".

Upstream breadth is not EliteSCADA qualification breadth.

---

## 22. L4 release gates for Zigbee2MQTT

Z2M implementation may progress to supported release when:

1. L0 mapping green.
2. L1 stale/restart/write truth green.
3. L2 real Z2M/broker behavior green.
4. L3 canonical Runtime path green.
5. L4 primary coordinator matrix green.
6. secondary coordinator sample green or explicitly out of first support statement.
7. at least:
   - OnOff;
   - Light/Dimmer/ColorLight;
   - Temperature/Humidity;
   - Occupancy;
   - Contact;
   - Power/Energy;
   - Cover
   have qualified models.
8. transient actions are explicitly excluded or event contract exists.
9. no package GPL bundling.
10. security review for MQTT credentials complete.

---

## 23. L4 release gates for Native

Native may progress only when:

1. SIDECAR-LIFECYCLE-01 implemented and accepted.
2. HOST-RESOURCE-GENERALIZATION-01 implemented and accepted.
3. protected network material path accepted.
4. exact Node/herdsman/converter versions pinned.
5. L0/L1/L2/L3 green.
6. TI primary L4 green.
7. Ember secondary L4 green before broad two-family claim.
8. same-family TI restore green.
9. same-family Ember restore green.
10. wrong coordinator/network identity fails closed.
11. no arbitrary external converter execution.
12. transient event scope is explicit.
13. packaging/license scan complete.

---

## 24. Lab safety

Mains-powered products require:
- appropriate insulated enclosure;
- circuit protection;
- qualified wiring;
- controlled test load;
- no exposed energized terminals;
- emergency disconnect.

Cover actuator tests require:
- current-limited/safe motor fixture or approved blind/shutter test rig;
- no uncontrolled mechanical pinch hazard.

Do not use lab convenience to bypass normal electrical safety.

---

## 25. Evidence retention

For every L4 run retain:

- test plan version;
- exact software versions;
- hardware serial/model;
- firmware;
- logs with secrets redacted;
- compatibility records;
- captures needed to diagnose failures;
- pass/fail summary;
- known limitations.

Do not retain:
- plaintext network key;
- install codes;
- link keys.

---

## 26. Source record

Sources revalidated 2026-10-06.

### Zigbee2MQTT

https://github.com/Koenkk/zigbee2mqtt/releases/tag/2.14.2
- release/version.

https://www.zigbee2mqtt.io/supported-devices/
- current broad upstream device catalog.

https://www.zigbee2mqtt.io/devices/MINI-ZBRBS.html
- cover behavior.

https://www.zigbee2mqtt.io/devices/S60ZBTPG.html
- plug/metering behavior.

https://www.zigbee2mqtt.io/devices/SNZB-04P.html
- contact/battery/tamper.

https://www.zigbee2mqtt.io/devices/SNZB-03P.html
- occupancy/illumination.

https://www.zigbee2mqtt.io/devices/SNZB-02P.html
- temperature/humidity.

https://www.zigbee2mqtt.io/devices/SNZB-01P.html
- sleepy button/action.

https://www.zigbee2mqtt.io/devices/WSDCGQ11LM.html
- Aqara quirk/sleepy sensor evidence.

https://www.zigbee2mqtt.io/devices/TS0601_thermostat.html
- Tuya vendor-specific climate/DP behavior.

https://www.zigbee2mqtt.io/devices/LED2201G8.html
- IKEA light.

https://www.zigbee2mqtt.io/devices/9290012574.html
- Philips Hue color light.

### Coordinator vendors

https://sonoff.tech/products/sonoff-zigbee-3-0-usb-dongle-plus-zbdongle-p
- CC2652P;
- Z-Stack;
- price snapshot.

https://sonoff.tech/products/sonoff-zigbee-3-0-usb-dongle-plus-zbdongle-e
- EFR32MG21;
- price snapshot.

https://sonoff.tech/products/sonoff-dongle-max-zigbee-thread-poe-dongle-dongle-m
- EFR32MG24;
- Ethernet/PoE/USB;
- price snapshot.

https://dongle.sonoff.tech/guide/dongle-m/hardware-specification-dongle-m/
- Dongle-M hardware and OS/container claims.

https://smlight.tech/products/slzb-06u
- optional TI Ethernet coordinator.

---

## 27. Checkpoint 3 conclusion

Hardware lab:
**DEFINED**

Compatibility evidence:
**DEFINED**

Z2M L0-L4:
**DEFINED**

Native L0-L4:
**DEFINED**

Scale ladder:
**DEFINED AS VALIDATION TARGETS, NOT PRODUCT MAXIMUMS**

Final research classifications:

ZIGBEE2MQTT:
**GO_WITH_GATES**

NATIVE ZIGBEE:
**GO_WITH_GATES**

Recommended order:
**Z2M FIRST**
then
**NATIVE AFTER FOUNDATION**

Required future contracts:

RESEARCH_CONTRACT_DELTA_REQUIRED — DRIVER-TRANSIENT-EVENT-01

RESEARCH_CONTRACT_DELTA_REQUIRED — SIDECAR-LIFECYCLE-01

RESEARCH_CONTRACT_DELTA_REQUIRED — HOST-RESOURCE-GENERALIZATION-01

Scope confirmation:

DOCS_ONLY

NO PRODUCT CODE CHANGED

NO DEPENDENCY CHANGED

NO CI CHANGED

NO TESTS EXECUTED

NO MERGE PERFORMED
