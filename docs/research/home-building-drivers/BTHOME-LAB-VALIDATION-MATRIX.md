# BTHOME LAB VALIDATION MATRIX

Status: CHECKPOINT 3
Lane: RESEARCH_ONLY / DOCS_ONLY / NO_PRODUCT_CODE / NO_MERGE_BY_RESEARCHER
Contract: C-HOME-BLUETOOTH-BTHOME-RESEARCH-01
Order: HOME-BLUETOOTH-BTHOME-EXECUTION-RESEARCH-01
Owner issue: #545
Access date: 2026-10-06

## 1. Scope

This document defines the execution-grade validation plan for future native BTHome support.

It covers:

- protocol versus product scale boundaries;
- proposed v1 qualification targets;
- diagnostics;
- L0 / L1 / L2 / L3 / L4;
- Linux / Windows / multi-adapter / container evidence;
- encrypted and unencrypted devices;
- transient events;
- adapter loss/recovery;
- duplicate/replay/flood behavior;
- concrete hardware lab matrix;
- compatibility record requirements.

It does not implement any product code, test code, Runtime change, Bluetooth backend, Host Resource, schema, UI or CI.

## 2. Live dependency status

### #543 HOME-COMMON-S2

The common Host Resource / managed sidecar foundation is still an active DEV lane.

Current research-relevant facts observed on 2026-10-06:

- common Host Resource identity/locator model exists in the #543 branch;
- exclusive lease foundation exists;
- managed sidecar lifecycle foundation exists;
- protected configuration references reuse #497;
- diagnostics are intended to project into #500;
- #543 Checkpoint 3 is currently gated on its exact-head CI finishing green;
- Bluetooth protocol remains outside #543.

This BTHome research does not depend on #543 being merged in order to finish research, but a future product DEV does.

### #546 DRIVER-TRANSIENT-EVENT-01

Research Checkpoint 1 is complete.

Important conclusions to consume:

- transient device event != TAG state;
- proposed event ingress must be bounded;
- event payload must be bounded/typed;
- no global ordering promise;
- evidence-based dedup only;
- default transient-event retention is ephemeral;
- overflow must be explicit and diagnosable;
- no silent coalescing of distinct button/event occurrences.

BTHome event objects 0x3A / 0x3B / 0x3C remain gated on the future accepted common transient-event contract.

## 3. Scale: protocol / OS limit versus EliteSCADA target

### 3.1 No universal BTHome device-per-adapter limit

BTHome itself does not define a maximum number of devices per receiver.

The effective radio capacity depends on factors including:

- advertising interval;
- number of advertising repetitions;
- scan duty cycle;
- passive versus active scanning;
- 1M / coded PHY behavior;
- legacy versus extended advertising;
- RF interference;
- physical coverage;
- host/controller quality;
- OS duplicate filtering;
- number of adapters;
- device event bursts.

Therefore do not publish a product claim such as:

"Bluetooth supports N devices"

as a protocol truth.

### 3.2 Representative current device rates

Current official device documentation demonstrates very different advertising patterns.

Shelly BLU H&T:
- periodic BTHome sensor packets every 1 minute;
- button event packets when pressed.

Shelly BLU Button:
- advertisement on button press;
- optional periodic beacon mode.

Shelly BLU Door/Window:
- immediate open event;
- follow-up angle observation;
- optional periodic beacon mode.

Shelly BLU Motion:
- motion observations/events;
- configurable blind/hold behavior;
- motion test mode can intentionally produce more frequent observations.

Ecowitt WS90 Powered by Shelly:
- BTHome sensor packet interval documented at approximately 8.8 seconds;
- multiple packet types containing many objects.

This variability is the reason scale must be qualified empirically.

## 4. Proposed EliteSCADA v1 qualification targets

The following values are PRODUCT QUALIFICATION TARGETS.

They are not Bluetooth protocol limits and are not guaranteed radio reception capacities.

### 4.1 Commissioned devices

Initial v1 product target:

- 250 commissioned BTHome devices per BluetoothAdapter Host Resource;
- 500 total commissioned BTHome devices across two adapters;
- 1,000 synthetic device identities in L1 parser/state stress.

PASS means:

- stable bounded per-device state;
- no identity collisions;
- no O(N^2) scan processing behavior visible in profiling;
- no unbounded cache/replay-table growth.

### 4.2 Ingest throughput

Sidecar/backend normalized-observation target:

- 100 accepted BTHome advertisements/second sustained for 30 minutes;
- 500 advertisements/second burst for 10 seconds;
- 2 adapters each sustaining 100 advertisements/second in synthetic multi-adapter L1.

These are software-ingress targets.

Real RF L4 is not required to physically generate those rates.

### 4.3 Parser-only stress

L0 parser target:

- 1,000 advertisements/second for 5 minutes from deterministic in-memory vectors;
- at least 1,000,000 mixed valid/invalid frames in soak/fuzz regression;
- no unbounded allocation growth;
- no parser crash;
- no malformed frame escaping structural bounds.

### 4.4 Latency

At sustained target load:

- p99 backend-reception -> normalized BTHome observation <= 250 ms;
- transient event accepted into canonical bounded event ingress <= 250 ms after normalized observation;
- latency must be measured separately from RF airtime and device advertising delay.

This is a product-side processing target, not an end-to-end physical reaction guarantee.

### 4.5 Bounded queue

Candidate v1 qualification bound:

- maximum 4,096 queued normalized observations per adapter;
- additional byte-budget guard <= 4 MiB serialized/raw observation payload per adapter;
- whichever limit is reached first causes explicit overflow handling.

This number is a qualification starting point, not a frozen public contract.

Overflow behavior:

- do not allocate unbounded memory;
- do not silently claim delivery;
- for transient events, align with #546 common-event rule:
  - preserve already accepted events;
  - reject/drop newest when the bounded ingress cannot accept it;
  - increment dropped/gap diagnostics;
- state observations may later be coalesced only if Main explicitly approves a state-specific policy;
- never coalesce semantically distinct button/dimmer events.

### 4.6 Burst recovery

After a 500 adv/s 10-second synthetic burst:

- queue returns below 25% capacity within 30 seconds under normal consumer throughput;
- no process restart;
- no deadlock;
- no unbounded memory retention;
- any overflow is explicit and counted.

### 4.7 Soak

Minimum software soak:

- 24 hours;
- 100 commissioned synthetic devices;
- mixed periodic state + transient events;
- encrypted and unencrypted frame mix;
- 50 normalized observations/second average;
- forced duplicate bursts;
- malformed-frame injection at bounded rate.

Memory acceptance:

- no monotonic unbounded growth;
- post-GC steady-state working-set trend from hour 6 to hour 24 must not grow >10% solely from retained BTHome state.

Absolute RSS/CPU thresholds are intentionally deferred until a reference host is selected.

### 4.8 Restart storm

Qualification scenario:

- 10 adapter/backend stop/start cycles inside 5 minutes;
- sidecar/process restart interleaved with adapter disappearance;
- no ResourceId rebound to wrong PhysicalIdentity;
- no duplicate canonical Equipment creation;
- bounded retry/backoff;
- explicit restart diagnostics.

### 4.9 Multi-adapter

Qualification target:

- two Linux adapters concurrently;
- same remote frame observed through both;
- one canonical observation/event after dedup;
- both local-adapter observations preserved diagnostically;
- adapter affinity recorded;
- removing one adapter does not corrupt identity/state owned by the other.

Windows multi-adapter deterministic receive affinity remains NOT_PROVEN and is a separate L4 limitation.

## 5. Diagnostics

Reuse #500.

No new Diagnostics Framework.

### 5.1 Adapter / sidecar

Required protocol detail fields/counters:

- Host Resource ResourceId;
- backend type;
- adapter available;
- locator;
- physical identity evidence/confidence;
- model;
- firmware/driver;
- powered/radio status where available;
- scanning true/false;
- scan mode;
- scan-affinity capability;
- shared-observation lease status;
- authoritative EliteSCADA owner;
- sidecar lifecycle state;
- restart count;
- re-enumeration count;
- last adapter error.

### 5.2 Ingest

Counters:

- advertisements received;
- advertisements accepted;
- advertisements/sec rolling rate;
- non-BTHome advertisements ignored;
- queue depth;
- queue high-water mark;
- queue saturation count;
- queue overflow/drop count;
- raw bytes/sec;
- decode latency;
- normalization latency.

### 5.3 Per device

Details:

- StableDeviceIdentity;
- current address / address type;
- identity evidence type;
- last seen;
- BTHome version;
- trigger-based flag;
- encrypted flag;
- key reference present/missing;
- last packet id when applicable;
- last accepted encryption counter when applicable;
- last accepted observation timestamp;
- RSSI;
- local adapters that have observed device;
- stale/quality state.

Never show bindkey value.

### 5.4 Security/parser

Counters:

- AES-CCM authentication failure;
- wrong/missing key;
- plaintext downgrade rejection;
- replay rejection;
- duplicate drop;
- out-of-order rejection where evidence proves it;
- malformed AD element;
- truncated object;
- unknown object id;
- unsupported BTHome version;
- frame too large for product bound;
- address/identity collision;
- unexpected address rotation.

### 5.5 Event

Counters/details:

- transient events accepted;
- event duplicates rejected;
- event queue saturation;
- event drops/gaps;
- last event type;
- last event observed timestamp;
- no synthetic "last button TAG".

## 6. L0 — codec / security / taxonomy

L0 is deterministic and hardware-free.

### 6.1 Official wire vectors

Use immutable byte vectors sourced from:

- current bthome.io v2 format examples;
- current bthome.io encryption examples;
- documented Shelly BTHome payload examples where applicable.

Store source URL, access date and expected interpretation with every vector.

Do not generate all "expected" frames with the EliteSCADA encoder/decoder itself.

### 6.2 Frame recognition

Cases:

- service UUID 0xFCD2;
- wrong service UUID;
- missing Flags AD element;
- multiple AD elements;
- local name present/absent;
- unrelated manufacturer/service data alongside BTHome;
- unsupported BTHome version.

### 6.3 Objects

Cases:

- every v1-supported BTHome object selected for first release;
- signed values;
- unsigned values;
- little-endian fields;
- scaling;
- minimum/maximum encoded values;
- repeated valid object types where specification/device permits;
- object ordering;
- text/raw length;
- command argument length.

### 6.4 Unknown / additive objects

Cases:

- unknown object after known objects;
- unknown object first;
- new high object ID;
- unknown object followed by arbitrary bytes.

Expected:

- no speculative size inference;
- preserve already-decoded lower-ID objects only when structural framing is valid;
- stop safely at unknown object;
- diagnostic increment;
- no crash.

### 6.5 Malformed / truncated

Cases:

- zero-length AD;
- length beyond received buffer;
- truncated Service Data;
- Device Information only;
- object id without value;
- short multibyte value;
- variable-length object declaring more bytes than present;
- oversized object length;
- encrypted frame missing counter;
- encrypted frame missing MIC.

Expected:
fail closed for affected frame/object and remain bounded.

### 6.6 Encryption

Use official known-key encrypted vectors.

Cases:

- correct key;
- wrong key;
- missing key;
- corrupted ciphertext;
- corrupted MIC;
- corrupted MAC/nonce input;
- corrupted Device Information byte;
- counter boundary values;
- plaintext downgrade after encrypted commissioning.

Expected:

- only correct authenticated packet accepted;
- key never logged;
- downgrade rejected for commissioned encrypted identity.

### 6.7 Replay

Cases:

- strictly increasing counter;
- same encrypted frame replayed;
- decreasing counter;
- large jump;
- wrap/rekey/reset candidate;
- persisted replay state after process restart.

Do not weaken replay protection to make reset scenarios pass.

Legitimate reset/recommission behavior must be explicit.

### 6.8 Packet id / duplicates

Cases:

- packet id same inside duplicate window;
- packet id changes;
- wrap 255 -> 0;
- identical event payload with different packet evidence;
- identical state payload repeated;
- same frame from two adapters.

Event test must prove that legitimate repeated button presses are not lost merely because payload bytes match.

### 6.9 Identity normalization

Cases:

- public address;
- static random;
- RPA;
- NRPA;
- address changes;
- same name/object set from two devices;
- wrong device appears on old locator/address;
- resolved identity evidence available/unavailable.

Expected:

current BLE address alone never silently becomes StableDeviceIdentity without accepted evidence.

### 6.10 State/event classification

Explicit cases:

STATE:
- temperature;
- humidity;
- battery;
- illuminance;
- contact/window/opening;
- motion;
- occupancy;
- smoke;
- moisture;
- voltage/current/power/energy;
- rotation angle.

TRANSIENT_EVENT:
- button;
- dimmer left/right;
- command/event-like object.

DIAGNOSTIC:
- RSSI;
- packet id;
- decrypt failures;
- last seen;
- firmware/device metadata.

No transient event becomes a persistent fake TAG.

## 7. L1 — deterministic fake advertisement source

L1 exercises backend-independent runtime logic with a fake normalized advertisement source.

### 7.1 Multiple devices

Generate:

- 1;
- 10;
- 100;
- 250;
- 1,000 synthetic identities.

Mix:

- encrypted;
- unencrypted;
- periodic;
- trigger-based;
- state;
- event.

### 7.2 Burst/flood

Inject:

- sustained 100 adv/s;
- 500 adv/s burst;
- parser-only 1,000 adv/s;
- unrelated BLE advertisements;
- random but bounded malformed frames.

Verify:

- bounded queue;
- explicit high-water/saturation metrics;
- no unbounded allocation;
- authoritative devices still progress under noise.

### 7.3 Wrong/missing bindkey

Cases:

- protected reference unavailable;
- wrong material returned;
- correct material restored;
- key changed through recommission.

Never echo key in exception/diagnostics.

### 7.4 Address changes

Cases:

- public stable address;
- static random changes after simulated reboot;
- RPA sequence with resolved stable identity evidence;
- RPA sequence without resolver;
- NRPA rotation.

Expected:

- merge only when identity evidence proves continuity;
- otherwise candidate requires explicit recommission/re-identification.

### 7.5 Adapter loss

Sequence:

Ready -> scanning -> adapter unavailable -> sidecar/backend degraded -> adapter reappears -> physical identity verified -> authority revalidated -> scanning resumes.

Verify:

- no wrong-resource rebound;
- stale/quality behavior is explicit;
- no event duplication on resume.

### 7.6 Process loss

Sequence:

- sidecar crash;
- bounded restart;
- replay state restored;
- device state continuity;
- no plaintext key persisted;
- no duplicate Equipment.

### 7.7 Duplicate across adapters

Same remote frame enters through adapter A and B with different RSSI/timestamps.

Expected:

- diagnostic observations retain both adapters;
- canonical state/event accepted once;
- dedup evidence recorded.

### 7.8 Queue pressure

Explicitly slow downstream consumers.

Expected:

- Bluetooth ingest does not create unbounded queue;
- transient event contract follows #546 bounded-dispatch direction;
- no second Event Bus;
- drops are explicit.

## 8. L2 — independent peer

L2 must not use the future EliteSCADA encoder as the only peer.

Recommended L2 composition:

### 8.1 Independent real advertiser

Use one unmodified commercial BTHome device:

- Shelly BLU H&T ZB or Shelly BLU Door/Window ZB.

The device itself produces the RF frame.

### 8.2 Independent reference decoder/oracle

Capture the same advertisement bytes and decode them using an independent maintained implementation:

- Bluetooth-Devices/bthome-ble current release, currently v3.24.0 at this checkpoint.

This package is MIT and is used as a test oracle/reference only.

It is not selected as the product runtime dependency.

### 8.3 Oracle rule

L2 PASS requires agreement on:

- BTHome version;
- encryption flag;
- packet id;
- object IDs;
- values/scales;
- button/event classification;
- malformed rejection where applicable.

When the official specification and reference implementation disagree:

OFFICIAL_SPEC_WINS unless Main explicitly approves a de-facto compatibility extension.

### 8.4 Raw capture

Linux:

- BlueZ/btmon or equivalent independent host capture may record HCI advertisements for evidence.

Windows:

- independent Windows watcher/capture utility may record received advertisement data.

Raw captures become immutable test fixtures after secrets are redacted.

No bindkey in PCAP/test logs unless the test artifact is explicitly protected.

## 9. L3 — canonical EliteSCADA integration

Future canonical chain:

BluetoothAdapter Host Resource
-> managed Bluetooth sidecar
-> normalized BLE advertisement
-> BTHome Data Source / protocol decoder
-> commissioned Equipment
-> Capability
-> TAG state OR Transient Event
-> CurrentTagCache / common Event architecture
-> normal consumers

Normal consumers:

TAG state:
- Runtime;
- Historian when configured;
- Alarm when configured;
- Server Scripts;
- TAG Gateway;
- screens/visuals.

Transient event:
- common bounded event architecture;
- Server Scripts/automation when future contract permits;
- diagnostics;
- explicit Operational Event promotion only when configured.

No:

- second Runtime;
- second historian;
- second alarm engine;
- second event bus;
- protocol-specific automation engine.

### 9.1 State validation

Example:

Shelly BLU H&T
-> Equipment
-> Temperature Capability -> canonical TAG
-> Humidity Capability -> canonical TAG
-> Battery Capability -> canonical TAG
-> CurrentTagCache
-> normal history/alarm/script consumers.

### 9.2 Event validation

Example:

Shelly BLU Button
-> Equipment
-> Button Capability/Event definition
-> TransientDeviceEventOccurred (or Main-approved equivalent)
-> bounded common event dispatch
-> Server Script trigger / diagnostic consumer.

Must prove:

no persistent Button=true fake TAG.

### 9.3 Revision/authority

Only active/effective Runtime revision owns canonical effects.

Reuse existing:

CommunicationDriverRuntimeServices.CanOwnExternalEffects

or accepted public authority seam.

Do not add Bluetooth leadership.

## 10. L4 — hardware matrix

L4 is real hardware on current supported OS builds.

### 10.1 Linux host / adapter

Primary Linux ARM64 reference:

Raspberry Pi 5 onboard Bluetooth

Evidence:
- official Raspberry Pi 5 specification lists Bluetooth 5.0 / BLE;
- official product brief states production lifetime through at least January 2036.

Why:
- current, obtainable platform;
- ARM64 coverage;
- integrated adapter removes USB-driver ambiguity for the first Linux reference;
- BlueZ path is representative.

Required OS record:
- distro;
- kernel;
- BlueZ version;
- firmware package;
- adapter address;
- adapter firmware/driver evidence.

This does not replace later x64 Linux qualification.

### 10.2 Windows adapter

Primary Windows USB reference:

TP-Link UB500 V3

Current vendor evidence:
- official support page provides Windows 10/11 drivers;
- V3 support page states Bluetooth 5.4.

Why:
- current commodity USB adapter;
- explicit Windows 10/11 vendor support;
- removable/re-enumeration testing.

Required:
- exact hardware version;
- driver version;
- Windows build;
- USB VID/PID/container/device instance evidence.

Do not collapse V1/V2/V3 into one compatibility record.

### 10.3 Secondary Windows adapter

Optional:
- Plugable USB-BT5.

Vendor position:
- Windows 10/11;
- Bluetooth LE;
- Windows only;
- vendor explicitly marks Linux incompatible.

Use as:
- Windows diversity fixture;
- negative evidence that model-level "Bluetooth 5 adapter" does not imply cross-platform support.

Do not select it as Linux canonical fixture.

## 11. BTHome device purchase matrix

### P0 — buy for first lab

#### A. Shelly BLU H&T ZB

Role:
- environmental periodic sensor;
- encrypted and unencrypted state;
- battery;
- temperature;
- humidity;
- button event.

Current official store status observed:
- in stock on Shelly Europe/USA pages at checkpoint date.

Buy:
- 2 units.

Configure:
- unit A unencrypted;
- unit B encryption enabled with protected bindkey workflow.

Purpose:
- same model encrypted/unencrypted comparison;
- periodic state;
- button transient event;
- replay/counter;
- battery replacement/restart.

#### B. Shelly BLU Door/Window ZB

Role:
- contact/open-close;
- illuminance;
- rotation/tilt;
- battery;
- event/trigger-based behavior.

Current official store status observed:
- in stock.

Buy:
- 1 unit.

Purpose:
- Contact Capability;
- rotation state;
- immediate event + follow-up measurement;
- sleepy/trigger behavior.

#### C. Shelly BLU Motion ZB

Role:
- motion;
- illuminance;
- battery;
- motion-clear lifecycle;
- button event.

Current official store status observed:
- in stock.

Buy:
- 1 unit.

Purpose:
- motion state/observation;
- rapid test mode;
- burstier real-device behavior;
- stale semantics.

#### D. Shelly BLU Button Tough 1 ZB

Role:
- pure transient event fixture.

Official technical docs define:
- single;
- double;
- triple;
- long;
- hold event families.

Current official store status observed:
- in stock.

Buy:
- 1 unit.

Purpose:
- prove event semantics;
- duplicate suppression without losing repeated presses;
- trigger-based silence;
- no fake TAG.

### P1 — high-value scale/multisensor fixture

#### E. Ecowitt WS90 Powered by Shelly

Role:
- multivalue/noisier BTHome fixture.

Official Shelly technical docs:
- BTHome approximately every 8.8 seconds;
- multiple packet types;
- illuminance;
- rain status;
- wind;
- UV;
- pressure;
- humidity;
- temperature;
- precipitation;
- battery/voltage-related observations.

Ecowitt has an official Powered by Shelly product listing.

Availability:
- variable; recheck immediately before procurement.

Buy:
- 1 only if available.

Purpose:
- larger object sets;
- recurring traffic;
- uncommon object coverage;
- multi-packet-type device.

This is NOT a gate for initial BTHome DEV because availability is not sufficiently stable.

### P2 — optional event density fixture

Shelly BLU RC Button 4 / ZB equivalent.

Role:
- multiple button sources;
- simultaneous/repeated events;
- event identity/payload semantics.

Buy only after the single-button contract is proven.

## 12. Hardware purchase summary

Minimum initial lab:

- 1 Raspberry Pi 5;
- 1 TP-Link UB500 V3;
- 2 Shelly BLU H&T ZB;
- 1 Shelly BLU Door/Window ZB;
- 1 Shelly BLU Motion ZB;
- 1 Shelly BLU Button Tough 1 ZB.

Optional:

- 1 Plugable USB-BT5 for second Windows adapter;
- 1 WS90 Powered by Shelly when available;
- 1 four-button Shelly event device.

This minimum matrix covers:

- Linux ARM64;
- Windows;
- internal adapter;
- USB adapter;
- unencrypted state;
- encrypted state;
- contact;
- motion;
- environmental state;
- transient event;
- multiple object types;
- adapter unplug/re-enumeration.

## 13. Physical L4 scenarios

### 13.1 Linux

- Raspberry Pi 5;
- current supported Raspberry Pi OS / Linux;
- current distro BlueZ;
- passive/shared scan;
- all P0 Shelly devices;
- encrypted + unencrypted;
- adapter/service restart;
- bluetoothd restart;
- host reboot;
- sidecar restart;
- container main Runtime with host-side sidecar topology.

### 13.2 Windows

- supported Windows 11 current build;
- Windows 10 2004+ only if product still supports it;
- TP-Link UB500 V3;
- P0 devices;
- service-session execution;
- reboot;
- radio disable/enable;
- USB unplug/replug;
- suspend/resume.

Must explicitly verify:

- BluetoothAddressType;
- service startup without interactive user;
- WinRT watcher recovery;
- physical adapter identity;
- Windows multiple-adapter limitations.

### 13.3 Multiple adapters Linux

Add second adapter later.

Required:
- enumerate two adapters;
- choose affinity;
- scan both;
- same device observed through both;
- dedup;
- unplug one;
- identity remains correct.

### 13.4 Windows multiple adapters

Use onboard + UB500 if available.

Goal:
determine empirically whether public WinRT watcher gives deterministic source-adapter affinity.

Expected research position:
NOT PROVEN.

If OS remains system-managed:
- document limitation;
- do not invent affinity.

## 14. Encryption / provisioning L4

With one Shelly BLU H&T ZB:

1. record factory unencrypted behavior;
2. enter pairing mode;
3. enable encryption using documented vendor flow;
4. retrieve key through bonded characteristic / supported vendor tooling;
5. import key into Protected Material Authority;
6. confirm encrypted observations;
7. confirm plaintext spoof/downgrade rejected;
8. wrong key -> decrypt failure diagnostic;
9. remove protected key -> missing-key diagnostic;
10. recommission/rekey explicitly.

Never export key into:
- test log;
- issue;
- screenshot;
- .escadapkg;
- fixture source.

Test artifact may store only:
- opaque protected reference;
- known synthetic non-production keys for L0.

## 15. Replay / battery reset investigation

Real-device test:

- observe encryption counter over normal operation;
- power cycle;
- battery replace;
- factory reset;
- change encryption/passkey;
- firmware update if available.

Record exact counter behavior.

Do not pre-decide reset acceptance.

Result must inform the future explicit recovery policy.

Important upstream evidence:
current bthome-ble release history includes recent changes around encryption-counter handling, including a reverted counter-check change in 2026.

Therefore EliteSCADA must validate its own policy against the normative specification and real devices rather than blindly copy reference-library behavior.

## 16. Identity L4

For every real device capture:

- OS-reported address;
- address type;
- whether address changes over:
  - reboot;
  - battery replace;
  - pairing;
  - encryption enable;
  - factory reset;
  - firmware update;
- manufacturer data;
- device type id;
- firmware objects;
- vendor MAC evidence if exposed;
- any resolvable identity evidence.

Goal:

determine per-qualified-model identity strategy.

Do not generalize one Shelly model's address behavior to all BTHome devices.

## 17. RF/coverage qualification

Record:

- distance;
- walls/floors;
- RSSI distribution;
- missed expected periodic observations;
- duplicate rate;
- event-loss rate;
- adapter placement;
- USB 3 interference conditions where relevant;
- co-channel Wi-Fi environment.

No universal range claim.

Product documentation may publish only tested reference environments or vendor adapter specifications.

## 18. Compatibility record schema

Each L4 qualified device/adapter record must contain:

- manufacturer;
- model;
- hardware revision;
- firmware;
- BTHome version;
- encrypted / unencrypted;
- objects observed;
- event types observed;
- address type;
- identity evidence;
- OS;
- OS build/kernel;
- BlueZ version when Linux;
- Windows driver version when Windows;
- local adapter manufacturer/model/revision;
- adapter firmware/driver;
- read behavior;
- event behavior;
- counter behavior;
- stale behavior;
- test date;
- pass/fail/limitations.

No compatibility claim without this record.

## 19. Compatibility claim policy

Not acceptable:

"EliteSCADA supports BTHome devices"

based on one packet or one model.

Allowed future wording after L4:

- "BTHome v2 receiver support";
- plus a maintained tested-device matrix;
- plus known limitations;
- plus encrypted/unencrypted qualification status;
- plus OS/adapter matrix.

Device-specific automation/provisioning support must be separately claimed.

## 20. L4 exit criteria

Minimum before direct BTHome product DEV can be called hardware-qualified:

Linux:
- Raspberry Pi 5 BLE path green;
- BlueZ backend green;
- host-side sidecar container topology green.

Windows:
- UB500 V3 green on supported Windows build;
- service execution green;
- unplug/replug green.

Devices:
- 2 H&T units, one encrypted;
- Door/Window;
- Motion;
- Button.

Behavior:
- state mapping green;
- transient event path green;
- protected material green;
- replay/decrypt behavior documented;
- address/identity behavior documented;
- duplicate handling green;
- adapter loss/recovery green.

No unresolved secret leakage.

## 21. Final Checkpoint-3 research decision

Scale:

NO UNIVERSAL BLE/BTHOME DEVICE LIMIT CLAIM.

Use explicit EliteSCADA product qualification targets.

Diagnostics:

REUSE #500.

Testing:

L0/L1/L2/L3/L4 plan is sufficiently concrete for future DEV planning.

Hardware:

P0 lab matrix is currently purchasable from vendor channels at this checkpoint for the selected Shelly families; exact regional stock must be revalidated at procurement time.

Transient event dependency:

#546 research supports the Checkpoint-1 conclusion that Button/Dimmer/Command event families require a bounded common event path, not fake TAG state.

BTHOME decision remains:

BTHOME = GO_WITH_GATES

## 22. Sources

BTHome:
- https://bthome.io/
- https://bthome.io/format/
- https://bthome.io/encryption/

Reference parser:
- https://github.com/Bluetooth-Devices/bthome-ble
- https://github.com/Bluetooth-Devices/bthome-ble/releases

Shelly:
- https://shelly-api-docs.shelly.cloud/docs-ble/common/
- https://shelly-api-docs.shelly.cloud/docs-ble/encryption/
- https://shelly-api-docs.shelly.cloud/docs-ble/Devices/BLU/ht/
- https://shelly-api-docs.shelly.cloud/docs-ble/Devices/BLU/dw/
- https://shelly-api-docs.shelly.cloud/docs-ble/Devices/BLU/motion/
- https://shelly-api-docs.shelly.cloud/docs-ble/Devices/BLU/button/
- https://shelly-api-docs.shelly.cloud/docs-ble/Devices/BLU_ZB/ht_ZB/
- https://shelly-api-docs.shelly.cloud/docs-ble/Devices/BLU_ZB/BluMotionZB/
- https://shelly-api-docs.shelly.cloud/docs-ble/Devices/BLU_ZB/button1_ZB/
- https://shelly-api-docs.shelly.cloud/docs-ble/Devices/BLU_ZB/wstation/

Current store evidence:
- https://www.shelly.com/products/shelly-blu-h-t-zb
- https://www.shelly.com/products/shelly-blu-door-window-zb-white
- https://www.shelly.com/products/shelly-blu-motion-zb
- https://www.shelly.com/products/shelly-blu-button-tough-1-zb

Linux hardware:
- https://www.raspberrypi.com/products/raspberry-pi-5/
- https://datasheets.raspberrypi.com/rpi5/raspberry-pi-5-product-brief.pdf

Windows hardware:
- https://www.tp-link.com/br/support/download/ub500/
- https://plugable.com/products/usb-bt5

Optional high-density device:
- https://shop.ecowitt.com/collections/spring-promotion-2026/products/ecowitt-ws90-7-in-1-weather-station-powered-by-shelly

All public sources accessed/revalidated on 2026-10-06.

## 23. Declarations

DOCS_ONLY
NO PRODUCT CODE CHANGED
NO TEST CODE CHANGED
NO BLUETOOTH IMPLEMENTATION
NO HOST RESOURCE CODE CHANGED
NO DEPENDENCY CHANGED
NO CI CHANGED
NO MERGE PERFORMED
HA = HIGH AVAILABILITY
HAB = HOME ASSISTANT BRIDGE
