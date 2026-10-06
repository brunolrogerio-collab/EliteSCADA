# BTHOME EXECUTION RESEARCH

Status: CHECKPOINT 1 COMPLETE
Lane: RESEARCH_ONLY / DOCS_ONLY / NO_PRODUCT_CODE / NO_MERGE_BY_RESEARCHER
Contract: C-HOME-BLUETOOTH-BTHOME-RESEARCH-01
Order: HOME-BLUETOOTH-BTHOME-EXECUTION-RESEARCH-01
Owner issue: #545
Parent: #472
Historical research source: #475
Related foundation: #543
Access date for public sources: 2026-10-06

## 1. Checkpoint scope

This document covers CHECKPOINT 1 only:

- current BTHome protocol/specification;
- BLE advertising and wire model;
- encryption and bindkey handling;
- stable device identity;
- state versus transient-event taxonomy;
- preliminary Capability mapping;
- discovery/import boundary;
- process truth, stale/quality and duplicate/replay behavior;
- security/adversarial behavior;
- unknowns and contract deltas;
- preliminary GO/WAIT decision.

BluetoothAdapter, Windows/Linux/container backend design, adapter ownership, scale/lab/legal qualification, and L0-L4 execution are intentionally deferred to later checkpoints.

No product implementation is authorized by this document.

## 2. Live EliteSCADA constraints revalidated

GitHub live was revalidated before research:

- #545 is ACTIVE / RESEARCH_ONLY / DOCS_ONLY / NO_PRODUCT_CODE / NO_MERGE_BY_RESEARCHER.
- Authorized branch: research/home-bluetooth-bthome.
- Released base: wave15/corrections-integration@77b08d60333685b4ba06aa649e3125f23b475dcf.
- Before this checkpoint the research branch and current integration were identical to that base (ahead 0 / behind 0).
- #472 requires HOME/BUILDING integrations to converge into canonical Equipment / Capability / TAG / Command / Cache / Event authorities rather than create a second runtime model.
- #543 owns common Host Resource and managed-sidecar foundations. This research may inform #543 but must not modify it.
- #543 deliberately excludes Bluetooth protocol implementation and transient-event Runtime from its current product scope.
- HA means High Availability only.
- HAB means Home Assistant Bridge.

## 3. Sources and current versions

### Normative / primary sources

1. BTHome main site
   https://bthome.io/
   Accessed 2026-10-06.

2. BTHome v2 format/reference
   https://bthome.io/format/
   Accessed 2026-10-06.

3. BTHome encryption reference
   https://bthome.io/encryption/
   Accessed 2026-10-06.

4. BTHome v1 legacy reference
   https://bthome.io/v1/
   Accessed 2026-10-06.

5. Bluetooth Core Specification v6.3, LE Link Layer device-address rules
   https://www.bluetooth.com/wp-content/uploads/Files/Specification/HTML/Core_v6.3/out/en/low-energy-controller/link-layer-specification.html
   Accessed 2026-10-06.

6. Bluetooth Core Specification v6.3, Generic Access Profile / privacy
   https://www.bluetooth.com/wp-content/uploads/Files/Specification/HTML/Core_v6.3/out/en/host/generic-access-profile.html
   Accessed 2026-10-06.

### Reference implementation / ecosystem evidence

7. Bluetooth-Devices/bthome-ble
   https://github.com/Bluetooth-Devices/bthome-ble
   Releases:
   https://github.com/Bluetooth-Devices/bthome-ble/releases
   Current release observed: v3.24.0, released 2026-07-21, MIT.
   This is implementation evidence, not the normative BTHome wire specification.

8. Shelly BLU technical documentation
   https://shelly-api-docs.shelly.cloud/docs-ble/
   Encryption:
   https://shelly-api-docs.shelly.cloud/docs-ble/encryption/
   Accessed 2026-10-06.

### Current protocol version conclusion

BTHome v2 is still the latest version published by the BTHome specification on 2026-10-06. The current format page states that v1 and v2 are recognized and v2 is latest. BTHome v1 is legacy/deprecated for new projects.

BTHOME_CURRENT_WIRE_VERSION = V2

No published BTHome v3 or successor protocol was found in the official specification at this checkpoint.

## 4. BTHome scope and transport boundary

BTHome is an open format for broadcasting sensor data and button presses over Bluetooth Low Energy discovery/advertising packets.

The published BTHome scope explicitly states that the goal is to share sensor data and button presses efficiently over BLE discovery packets and that it is not a goal to provide a way for devices to share control.

Therefore:

- native BTHome support is fundamentally an observer/receiver path;
- a BTHome advertisement is an observation/report;
- BTHome does not by itself establish a bidirectional command transport;
- the BTHome "command" object is content carried inside an advertisement and is not evidence that a receiver can write back to the device;
- no EliteSCADA write/command Capability may be inferred merely because the physical transport is Bluetooth.

Boundary:

BTHOME_V2_DIRECT = ADVERTISEMENT_RECEIVE_PATH
BTHOME_V2_DIRECT != GENERIC_BLUETOOTH_CONTROL_PATH

Any future bidirectional Bluetooth protocol must be researched and contracted separately.

## 5. Wire / advertising model

### 5.1 Recognition

BTHome v2 uses 16-bit Service UUID 0xFCD2.

On the wire in the Service Data AD element, the UUID bytes appear as D2 FC.

The advertising payload should contain at least the Service Data AD element (AD type 0x16). The BTHome format also strongly recommends the standard Flags AD element and permits a local name.

### 5.2 Device Information byte

After UUID 0xFCD2, BTHome v2 carries one Device Information byte.

Published v2 bits:

- bit 0: encryption flag;
- bit 1: reserved for future use;
- bit 2: trigger-based / irregular-advertising flag;
- bits 3-4: reserved for future use;
- bits 5-7: BTHome version; value 2 is encoded as 010.

Trigger-based is operationally important. The specification explicitly indicates that a receiver may use it to avoid declaring a device unavailable merely because it does not advertise regularly.

### 5.3 Objects

After Device Information, the payload contains one or more typed objects.

For fixed-size numeric objects:

- object id identifies semantic property and data representation;
- multi-byte values use little-endian representation;
- a factor from the specification converts the encoded integer to engineering value.

Variable-length text/raw objects carry an explicit length byte.

The command object has a bounded argument-length field:
- high 3 bits reserved;
- low 5 bits argument length;
- argument length therefore encodes 0-31 bytes.

### 5.4 Object ordering and forward compatibility

The published BTHome v2 rule requires object IDs to be placed in ascending numeric order.

The explicit compatibility purpose is important: when a receiver encounters an unknown/new object ID, it can still have parsed older/lower supported object IDs before that point. The specification states that a receiver stops parsing when it encounters an unsupported object ID.

EliteSCADA parser recommendation:

1. parse only within the received AD element bounds;
2. require known BTHome version;
3. process supported objects in order;
4. when an unknown object ID is encountered:
   - retain already-decoded lower-ID observations from that structurally valid frame;
   - stop object parsing at the unknown ID;
   - record unknown-object diagnostic;
   - do not guess the unknown object's length or semantics;
5. when structural length/truncation is invalid:
   - reject the frame as malformed;
   - do not invent data from remaining bytes.

This preserves BTHome's additive compatibility model without permitting unbounded or heuristic parsing.

### 5.5 Packet size boundary

The current BTHome specification describes the BLE PDU/AD-element model but does not publish a separate universal BTHome application maximum that is safe to treat as the product limit across all OS backends and legacy/extended-advertising combinations.

Therefore:

PROTOCOL_PACKET_LIMIT = TRANSPORT_AND_AD_ELEMENT_BOUNDED
ELITESCADA_PRODUCT_MAX = TBD_AFTER_ADAPTER_BACKEND_RESEARCH

Do not invent a universal "31-byte BTHome limit" in product contracts. The future parser must enforce strict received-buffer and AD-element bounds and a product maximum derived from the chosen backend/compatibility profile.

## 6. Current object semantics relevant to EliteSCADA

The v2 table currently includes, among others:

Sensor/measurement examples:
- battery;
- temperature;
- humidity;
- illuminance;
- count;
- current;
- energy;
- power;
- voltage;
- pressure;
- CO2;
- TVOC;
- moisture;
- rotation angle;
- timestamp;
- text/raw;
- device settings revision.

Binary-state examples:
- low battery;
- battery charging;
- door;
- opening;
- motion;
- occupancy;
- moisture wet/dry;
- smoke;
- window;
- power on/off;
- presence;
- running;
- vibration;
- tamper;
- other explicit boolean classes.

Transient-event families:
- object 0x3A button;
- object 0x3B command/event-like broadcast;
- object 0x3C dimmer.

Device metadata:
- object 0xF0 device type id;
- object 0xF1 firmware version (4 components);
- object 0xF2 firmware version (3 components).

Misc:
- object 0x00 packet id.

Important semantic distinction:

- rotation measurement (0x3F) is state/measurement;
- dimmer rotate-left/rotate-right (0x3C) is a transient event.

## 7. State / transient event / diagnostic / configuration taxonomy

### 7.1 STATE

An object is STATE when it represents the latest observable condition or measurement and retaining the latest value is semantically valid.

Recommended STATE examples:

- Temperature;
- Humidity;
- Illuminance;
- Battery percentage;
- Low-battery binary state;
- Battery charging state;
- Door / Window / Opening;
- Motion;
- Occupancy;
- Smoke;
- Moisture/wet-dry;
- Voltage;
- Current;
- Power;
- Energy;
- Count, where the device semantics are a cumulative/current count;
- Rotation angle measurement;
- other explicit BTHome sensor/binary objects where "latest value" is meaningful.

A received state value is an authoritative observation of what the device advertised at that reception time. It is not proof that the physical condition remains true indefinitely.

### 7.2 TRANSIENT_EVENT

These must not be converted into fake persistent state TAGs:

- 0x3A button:
  - press;
  - double_press;
  - triple_press;
  - long_press;
  - long_double_press;
  - long_triple_press;
  - hold_press.
- 0x3C dimmer:
  - rotate_left with steps;
  - rotate_right with steps.
- 0x3B command/event-like advertisement:
  - off;
  - on;
  - toggle;
  - step up;
  - step down;
  - manufacturer-specific arguments where defined.

These are event occurrences. "Last button pressed" or "button=true" is not an equivalent semantic replacement.

Contract dependency:

RESEARCH_CONTRACT_DELTA_REQUIRED — DRIVER-TRANSIENT-EVENT-01

Issue/lane #546 owns the dedicated research for this event contract. This lane does not solve it.

### 7.3 DIAGNOSTIC / METADATA

Recommended non-process state:

- RSSI: diagnostic only by default;
- last advertisement time;
- packet id;
- decrypt success/failure;
- replay/duplicate drops;
- malformed frame count;
- unknown object count;
- missing bindkey;
- BTHome version;
- device type id;
- firmware version;
- trigger-based flag;
- raw BLE address + address type as identity evidence/locator, not automatically canonical identity.

Do not create a process Capability for RSSI by default.

### 7.4 CONFIGURATION / CONFIGURATION METADATA

BTHome advertisements do not provide a generic configuration-authority model.

Examples that are configuration metadata rather than process values:
- settings revision (0x65);
- device model/type metadata where used to choose a decoder/profile;
- local commissioning selection, labels and key references stored by EliteSCADA.

Do not treat BTHome-advertised metadata as authority to rewrite project configuration automatically.

## 8. Capability mapping into #472 vocabulary

Only explicit semantics should map.

| BTHome semantic | EliteSCADA preliminary mapping | Notes |
| --- | --- | --- |
| temperature | Temperature Capability -> TAG | State |
| humidity | Humidity Capability -> TAG | State |
| illuminance | Illuminance Capability -> TAG | State |
| battery % / low battery | Battery Capability -> TAG/state | Do not confuse with driver health |
| door/window/opening | Contact Capability -> TAG | Only where selected object/device semantics are explicitly contact/opening |
| motion | Motion Capability -> TAG | State |
| occupancy | Occupancy Capability -> TAG | State |
| smoke | Smoke Capability -> TAG | Explicit BTHome binary object |
| moisture wet/dry | Moisture/Wet Capability -> TAG | Do not automatically rename to Leak |
| voltage | Voltage Capability -> TAG | State |
| current | Current Capability -> TAG | State |
| power | Power Capability -> TAG | State |
| energy | Energy Capability -> TAG | State/cumulative semantics |
| count | Count Capability -> TAG | Device semantics must define meaning |
| rotation angle | Rotation/Position Capability -> TAG only if common vocabulary supports it | Measurement state |
| button | Event | Requires DRIVER-TRANSIENT-EVENT-01 |
| dimmer rotation event | Event | Requires DRIVER-TRANSIENT-EVENT-01 |
| command object | Event/intent observation, not write transport | Manufacturer-specific args may prevent generic mapping |
| RSSI | Diagnostic detail | Not a process Capability by default |
| packet id | Diagnostic/dedup metadata | Not TAG |
| device type / firmware | Device metadata | Not process TAG |

### Leak semantic warning

The current BTHome table does not define a generic object named "leak".

There are moisture/wet-dry and water/volume-related semantics, but those are not automatically equivalent to a water-leak detector.

Therefore:

LEAK_CAPABILITY = ONLY_IF_DEVICE_OR_OBJECT_SEMANTICS_EXPLICITLY_PROVE_LEAK

Do not force-fit generic moisture or water-volume objects into Leak.

## 9. Process truth, duplicate handling and stale/quality

### 9.1 New authoritative observation

A valid decoded advertisement becomes a new authoritative observation when:

- BTHome v2 structure is valid;
- the selected/commissioned device identity is acceptable;
- if encryption is required, AES-CCM authentication succeeds;
- if replay protection is available/required, counter checks pass;
- the frame is not discarded as duplicate/replay;
- the object's explicit semantics are supported.

Reception time is part of the observation evidence.

### 9.2 Packet id

Object 0x00 packet id is optional.

The specification says it may be used to filter duplicate advertisements and should change when data changes.

It is only 8 bits and wraps. It is not a globally unique event id, persistent sequence, timestamp or stable device identity.

Product recommendation:
- use packet id as bounded duplicate evidence where present;
- never use packet id as sole durable ordering or identity authority.

### 9.3 Encrypted counter

Encrypted v2 frames include a 4-byte counter used in the AES-CCM nonce.

The BTHome encryption documentation explicitly states that receivers must verify increasing counter behavior to obtain replay protection.

EliteSCADA recommendation:
- persist sufficient per-commissioned-device replay state to reject clear replay/decrease cases;
- define explicit recovery behavior for legitimate device counter reset/rekey in later design;
- never silently disable counter validation merely to accept data;
- expose replay drops diagnostically without logging secrets.

### 9.4 Duplicate advertisements

BLE devices may intentionally repeat the same advertisement to improve reception probability.

Duplicate policy must be bounded and device-aware:
- if packet id exists, use it as primary protocol duplicate hint;
- for encrypted packets, authenticated counter and ciphertext repetition provide additional evidence;
- repeated reception via more than one future BluetoothAdapter must converge to one observation/event, not duplicate canonical Events;
- event dedup must not suppress legitimate repeated presses solely because payload values are identical.

Exact time windows belong to implementation/lab qualification, not this protocol-only checkpoint.

### 9.5 Out-of-order

BTHome packet id is not sufficient to establish unlimited ordering across wrap/restart/proxy delays.

Encrypted counter gives stronger monotonic evidence but requires a recovery policy for legitimate counter resets.

Therefore:
- reject proven replays;
- deduplicate proven duplicates;
- do not invent total ordering where protocol evidence is insufficient.

### 9.6 Stale / quality

For periodic devices (trigger-based flag = 0):
- last-seen age may degrade quality after a device/profile-specific expected interval and grace;
- there is no protocol-wide universal stale timeout established by BTHome;
- product defaults should be evidence-based and overrideable after lab qualification.

For trigger-based/irregular devices (trigger-based flag = 1):
- silence is expected;
- absence of advertisements must not by itself turn the device unavailable after a normal periodic-sensor timeout;
- adapter/receiver health and explicit device-health evidence must be separated from "no event happened".

This directly prevents button/event devices from oscillating into false stale/unavailable states.

## 10. Encryption / bindkey

### 10.1 Current BTHome v2 cryptography

Published v2 encryption uses:

- AES in CCM mode;
- 16-byte pre-shared key (128-bit, represented as 32 hex characters);
- 4-byte unsigned little-endian advertisement counter;
- 4-byte MIC;
- nonce composed from:
  - device MAC address (6 bytes);
  - UUID bytes D2 FC;
  - BTHome Device Information byte;
  - 4-byte counter.

The v1 associated-data/header behavior is not used in v2.

### 10.2 Required receiver security behavior

The BTHome documentation explicitly warns that encryption alone is not a complete safety guarantee unless the receiver enforces the necessary checks. It identifies two central receiver responsibilities:

1. do not accept an unauthenticated/plaintext spoof as equivalent to an encrypted commissioned device;
2. verify increasing counter behavior to prevent trivial replay.

EliteSCADA proposed gate:

If a commissioned device has a bindkey/protected key reference:
- encrypted/authenticated BTHome is required for that identity unless the user explicitly recommissions it;
- plaintext advertisements claiming the same address/identity are a downgrade attempt and must not update canonical process state;
- invalid MIC/decrypt failures are rejected;
- replay failures are rejected;
- diagnostics are sanitized.

### 10.3 Protected Material Authority

Bindkeys are secrets.

Required convergence:

BTHOME_BINDKEY -> existing Protected Material Authority (#497)

Never store bindkeys:
- plaintext in .escadapkg;
- plaintext in Engineering JSON;
- plaintext in logs;
- plaintext in diagnostics;
- plaintext in URLs;
- as part of a visible StableDeviceIdentity string.

Project/config data should reference protected material by an opaque authority reference.

### 10.4 Provisioning is vendor-specific

There is no universal BTHome bindkey-provisioning flow in the BTHome wire specification.

Concrete current manufacturer evidence: Shelly BLU.

Shelly documents:
- BTHome payload encryption with AES-CCM;
- a random 16-byte encryption key;
- pairing/bonding required to read the key from a BLE characteristic;
- configuration through Shelly applications;
- key regeneration behavior tied to passkey changes in documented flows.

Therefore future EliteSCADA commissioning must support an abstraction such as:

ProtectedKeyProvisioning =
- manual secure key entry/import; OR
- vendor-specific authenticated local retrieval; OR
- future QR/export flow when explicitly documented by that vendor.

Do not invent a universal QR or vendor-app export contract.

## 11. StableDeviceIdentity

### 11.1 BTHome protocol finding

The published BTHome v2 payload defines:
- service UUID;
- Device Information flags/version;
- sensor/binary/event objects;
- optional device type id;
- firmware version;
- packet id.

It does not define a universal globally unique stable device identifier that is guaranteed to survive BLE address changes.

Device type id is a model/type discriminator, not a per-device unique identity.

Packet id is a changing 8-bit dedup field, not identity.

Local name is optional and not unique.

Therefore:

BTHOME_PROTOCOL_ALONE_DOES_NOT_PROVIDE_UNIVERSAL_STABLE_DEVICE_IDENTITY

### 11.2 BLE address types

Bluetooth Core v6.3 distinguishes:
- Public Device Address;
- Random Static Device Address;
- Resolvable Private Address (RPA);
- Non-resolvable Private Address (NRPA).

Important stability facts:
- a Public Device Address is an identity-address type;
- a Random Static address can be regenerated after a power cycle and is not universally persistent across reboot;
- an RPA is intentionally rotated and can be associated to an identity only when the resolving party has the relevant IRK/resolution context;
- an NRPA is intentionally not resolvable to a stable identity through the standard address-resolution procedure.

Therefore the current observed BLE address is a locator/observation attribute, not automatically StableDeviceIdentity.

### 11.3 Encryption does not magically solve address identity

BTHome v2 AES-CCM uses the MAC address in its nonce.

That authenticates/decrypts a packet when the correct key and address context are known, but it does not define a separate stable per-device identifier for an observer.

If a device rotates private addresses and no standard/vendor mechanism exposes a resolvable stable identity to the EliteSCADA observer, BTHome alone does not give enough information to guarantee persistent Equipment identity.

### 11.4 Proposed EliteSCADA identity model

StableDeviceIdentity should be an EliteSCADA-assigned immutable logical identity created only after SELECTED IMPORT / commissioning.

It should bind a set of identity evidence, not equal one raw address string.

IdentityEvidence candidates, in priority order when actually available:

1. OS/controller-resolved Bluetooth Identity Address backed by valid RPA resolution;
2. manufacturer-provisioned stable device identity/serial explicitly documented and authenticated;
3. public/static address only when the exact device/firmware qualification proves the expected persistence characteristics;
4. successful bindkey authentication as authenticity evidence linked to the commissioned identity, with the key itself remaining protected;
5. user-confirmed commissioning context and device model metadata as secondary evidence.

Not allowed:

StableDeviceIdentity = current BLE address

without evidence that the address is stable for that device/address mode.

### 11.5 Rotating-address hard gate

For a device that advertises with a rotating NRPA, or an RPA that the OS/backend cannot resolve to a stable identity and for which vendor provisioning supplies no stable identity seam:

PERSISTENT_DEVICE_BINDING = NOT_PROVEN

The future importer must not silently merge such observations into an existing Equipment by name, RSSI, object set or payload similarity.

MAIN_DECISION_REQUIRED — BTHOME-STABLE-DEVICE-IDENTITY

Before product DEV claims general direct-BTHome support, Main must approve the commissioning/identity-evidence contract and define what happens when stable identity cannot be proven.

## 12. Discovery / import

Required future conceptual flow:

scan
-> observed candidate
-> identify / identity evidence
-> user select
-> preview
-> apply
-> canonical Equipment / Capability / TAG / Event bindings

Forbidden:

scan
-> automatically create Equipment/TAG for every nearby device

Bluetooth scanning is physically promiscuous and can observe neighbors' devices. Whole-radio auto-import is therefore both noisy and a privacy boundary violation.

Minimum candidate preview should eventually show sanitized data such as:
- display/local name if present;
- current observed address + address type;
- BTHome version;
- encrypted/unencrypted;
- whether a protected key is required/available;
- device type id / firmware if present;
- supported object semantics;
- last seen;
- RSSI diagnostic;
- identity confidence/evidence class.

No bindkey value in preview/logs.

## 13. Security / adversarial analysis

### 13.1 Threats

Packet spoofing:
- unencrypted BTHome is observable and spoofable by nearby transmitters;
- MAC address alone is not authentication.

Replay:
- encrypted frames require counter enforcement;
- unencrypted event/state frames may be replayed/spoofed and have weaker trust.

Encryption downgrade:
- a known encrypted device can be impersonated with plaintext unless receiver policy refuses the downgrade.

Advertisement flood / duplicate storm:
- radio may receive large volumes of advertisements from unrelated or malicious devices.

Malformed/truncated frames:
- attacker-controlled lengths/object bytes must never drive unbounded reads/allocations.

Unknown object IDs / future fields:
- must not trigger speculative parsing.

Wrong/missing bindkey:
- must fail closed for that commissioned encrypted identity and expose sanitized diagnostic only.

Nearby unintended devices:
- discovered candidate is not project authority until selected/imported.

Identity collision:
- same name/model/object set or spoofed address must not auto-merge Equipment.

### 13.2 Required future parser/runtime guards

- bounded advertisement ingest queue;
- bounded per-frame parser;
- strict AD-element length checks;
- strict object length checks;
- stop-at-unknown-ID compatibility rule;
- no dynamic unbounded allocation from object length;
- selected-import allowlist;
- rate/duplicate suppression;
- event-aware deduplication;
- AES-CCM MIC verification;
- replay counter policy;
- no plaintext downgrade after encrypted commissioning;
- protected bindkey authority;
- sanitized diagnostics;
- per-device and global flood counters;
- no automatic canonical object creation from passive radio discovery.

## 14. Specification/reference implementation discrepancy to track

The current published BTHome v2 format page marks Device Information bit 1 as "Reserved for future use".

The current Bluetooth-Devices/bthome-ble reference parser source observed on 2026-10-06 contains logic interpreting bit 1 as a "MAC included" flag and, when set, reads six MAC bytes from service data.

Because the published specification is normative for this research checkpoint, EliteSCADA must not implement the bit-1 behavior as protocol truth without an official specification update or explicit compatibility decision.

Record:

BTHOME_SPEC_IMPLEMENTATION_DELTA_UNRESOLVED

Future DEV prerequisite:
- revalidate the published BTHome format and release notes immediately before implementation;
- if bit 1 becomes officially assigned, implement the then-current published rule;
- if it remains unpublished/reserved but real qualified devices require it, Main must explicitly decide whether to support that de-facto extension and how to gate it.

## 15. Unknowns after checkpoint 1

1. Exact stable-identity coverage across representative real BTHome devices is not yet qualified.
2. Whether chosen Windows/Linux backends expose address type and resolved identity consistently is CHECKPOINT 2 work.
3. A universal stale timeout cannot be derived from BTHome alone.
4. Event durability/dedup semantics require DRIVER-TRANSIENT-EVENT-01 (#546).
5. Counter-reset/rekey recovery policy needs L0/L4 evidence; it must not weaken replay protection by default.
6. BTHome bit-1 "MAC included" discrepancy needs revalidation before implementation.
7. Product packet/queue limits require adapter/backend and lab evidence.
8. Vendor bindkey provisioning differs; only qualified vendor-specific flows can be automated.
9. Unencrypted devices with rotating/unresolvable addresses cannot currently be promised persistent Equipment identity from BTHome protocol evidence alone.

## 16. Contract deltas / Main decisions

### Confirmed dependency

RESEARCH_CONTRACT_DELTA_REQUIRED — DRIVER-TRANSIENT-EVENT-01

Reason:
BTHome 0x3A button, 0x3B command/event-like broadcast and 0x3C dimmer are transient occurrences and must not become fake persistent TAG state.

Owned research: #546.

### Main decision required

MAIN_DECISION_REQUIRED — BTHOME-STABLE-DEVICE-IDENTITY

Reason:
BTHome v2 does not define a universal stable per-device identifier and Bluetooth private addresses may rotate. A future direct-BTHome product must define identity evidence and commissioning behavior before claiming persistent Equipment binding.

### No #543 code change requested in checkpoint 1

No change to HOME-COMMON-S2 is requested from this protocol checkpoint.

Potential BluetoothAdapter/Host Resource deltas are intentionally deferred to CHECKPOINT 2.

## 17. Preliminary decision

BTHOME = GO_WITH_GATES

Rationale:
- protocol is open, small and receiver-oriented;
- v2 has explicit sensor/binary/event semantics suitable for canonical mapping;
- encryption is defined and can converge on existing Protected Material authority;
- direct support avoids requiring HAB/Home Assistant or another bridge for basic BTHome observations;
- transient events fit the planned canonical Event direction rather than a second runtime.

Gates before product DEV:

1. Main approves StableDeviceIdentity / commissioning evidence policy.
2. DRIVER-TRANSIENT-EVENT-01 is available for 0x3A/0x3B/0x3C.
3. Bindkeys converge on #497 Protected Material Authority; no plaintext key storage.
4. Encrypted commissioned identities enforce MIC, counter/replay and anti-downgrade policy.
5. CHECKPOINT 2 proves BluetoothAdapter backend/identity visibility on Windows/Linux/container and chooses the v1 architecture.
6. L0/L1 later prove bounded parser, malformed/unknown handling, replay/duplicate behavior and address-change scenarios.
7. L4 later qualifies representative real devices and provisioning flows.

This is a preliminary checkpoint decision and may change after checkpoints 2 and 3.

## 18. Checkpoint declarations

DOCS_ONLY
NO PRODUCT CODE CHANGED
NO HOST RESOURCE CODE CHANGED
NO BLUETOOTH IMPLEMENTATION
NO DEPENDENCY CHANGED
NO CI CHANGED
HA = HIGH AVAILABILITY
HAB = HOME ASSISTANT BRIDGE
NO MERGE PERFORMED
