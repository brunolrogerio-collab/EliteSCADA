# BACnet/IP and BACnet Secure Connect architecture research

Status: **RESEARCH IN PR / PRODUCTION NOT IMPLEMENTED**  
Date: 2026-08-27  
Assignment: `DEV 3 - EliteSCADA` / `research/bacnet-ip-secure-connect`

This document is a non-production architecture and interoperability research deliverable for a future EliteSCADA BACnet driver family. It does **not** add a BACnet package, register a BACnet Data Source, alter `Program.cs`, DI, DriverHost, the canonical Engineering schema, API/frontend composition, or active runtime behavior.

The current product order remains authoritative: interface product development and the user-validation gate precede production external-protocol expansion. This research exists only to reduce uncertainty before that later implementation wave.

## Executive recommendation

1. Make **BACnet/IP over UDP/IP the first production BACnet transport**, with IPv4 as the minimum acceptance target and IPv6 treated as compatible follow-on capability where the selected stack proves it reliably.
2. Model **one EliteSCADA Data Source per target BACnet Device Instance**, not one Data Source per entire BACnet network. Device Instance is the durable BACnet device identity; current IP/MAC/network address is runtime resolution metadata.
3. Permit several BACnet Data Sources to share one lower-level BACnet/IP network transport/session manager when they use the same local interface/network profile. Logical device health, counters, TAG quality and failures must remain isolated per Data Source.
4. Use **Who-Is/I-Am** for bounded BACnet discovery, with manual target configuration always available. Do not disguise arbitrary IP scanning as BACnet discovery.
5. Treat BBMD, Broadcast Distribution and Foreign Device Registration as explicit BACnet/IP network topology concerns. Cross-subnet broadcast discovery requires configured BACnet infrastructure; ordinary IP routers do not forward local BACnet/IP broadcasts by magic.
6. Persist portable BACnet point identity around **Device Instance + Object Identifier + Property Identifier + optional array index**. Object names, descriptions, vendor/model text and resolved network addresses are metadata, not sole identity.
7. Prefer `ReadPropertyMultiple` for efficient acquisition when the device supports it, with bounded request partitioning and deterministic fallback to `ReadProperty` when service support, APDU size or segmentation constraints require it.
8. Support COV where devices genuinely support it, but retain polling as a first-class fallback. COV subscriptions require lease/renew/recreate diagnostics and must recover after address/session/runtime changes.
9. Do not reduce BACnet value semantics to `Present_Value` alone. Where applicable, read and preserve `Status_Flags`, `Reliability`, `Out_Of_Service`, `Units` and capability metadata so EliteSCADA quality and diagnostics remain honest.
10. Make BACnet command priority **explicit Engineering configuration** for commandable writes. Never silently choose a priority merely because a stack or protocol path offers a default. Relinquish must be an explicit operation that writes BACnet `NULL` at the intended priority.
11. Treat **BACnet/SC as a separate secure BACnet data-link option**, not a boolean “TLS mode” layered over BACnet/IP/UDP. BACnet/SC uses WebSockets and TLS, hub topology, certificates and different network-management semantics while carrying normal BACnet application/network messages.
12. Keep **MS/TP out of the first EliteSCADA BACnet transport scope**. A BACnet/IP client may still access devices behind a standard BACnet router. Native RS-485 MS/TP support is a later, separate transport/module decision.
13. For the first BACnet/IP laboratory adapter, **Ela-compil `BACnet` 4.0.0** is the strongest current pure-.NET candidate: MIT licensed, .NET 10 capable, BACnet/IP IPv4/IPv6, BBMD/FDR, RP/RPM/WP, COV and segmentation support. Its public feature list does not demonstrate BACnet/SC, so SC support must not be assumed.
14. For BACnet/SC interoperability research, use the **BACnet International BACnet/SC Reference Implementation/System Test Bench** and a current patched **bacnet-stack** build as laboratory/reference peers. Neither choice is a production dependency decision.
15. Re-review the exact ANSI/ASHRAE 135 revision, addenda, BTL requirements, library versions, licenses and security advisories immediately before production implementation. BACnet/SC certificate/configuration and Authentication/Authorization semantics have continued to evolve through ANSI/ASHRAE 135-2024 and later addenda.

## 1. Standards baseline and transport classification

BACnet is ANSI/ASHRAE Standard 135 and is also published internationally as ISO 16484-5. The current standard baseline at the time of this research is **ANSI/ASHRAE 135-2024**. The BACnet Committee states that the 2024 edition incorporated 17 addenda to 135-2020, including BACnet/SC certificate-authority interchange, BACnet/SC configuration/certificate management, Device Proxying, and Authentication/Authorization work.

The protocol must be considered in layers. EliteSCADA should not use the word `bacnet` as if every deployment uses one interchangeable wire format.

| Transport / path | Classification for EliteSCADA | Initial production scope |
| --- | --- | --- |
| BACnet/IP over UDP/IPv4 | Normal IP BACnet data link, Annex J family | **First target** |
| BACnet/IPv6 | BACnet IP transport variant with IPv6-specific behavior | Compatible follow-on after lab proof |
| BACnet/SC | Secure WebSocket + TLS BACnet data link with hub topology | Later dedicated secure transport slice |
| MS/TP | EIA-485 serial BACnet data link | Later separate transport/module |
| Device behind BACnet router | Normal BACnet internetwork routing; local EliteSCADA transport can remain BACnet/IP | Supported scenario once routing is proven |

BACnet/SC does not replace the BACnet object model or TAG mapping. It changes how BACnet messages are securely transported and distributed. Likewise, a device reachable through a BACnet/IP-to-MS/TP router does not require EliteSCADA to implement an MS/TP serial port merely to read that routed device.

### Initial driver boundary

The first production BACnet slice should be a **client/data-acquisition and controlled-write driver**, not a general-purpose BACnet server, router, BBMD, building controller, network-management station or commissioning suite.

Explicitly outside the first scope:

- implementing a BBMD for the plant;
- acting as a BACnet router between data links;
- native MS/TP token handling;
- BACnet alarm/event server behavior;
- device reinitialization or DeviceCommunicationControl as ordinary SCADA commands;
- file transfer, object creation/deletion or destructive device engineering;
- automatic rewriting of third-party BACnet device configuration;
- claiming BTL certification before the appropriate profile/test work is actually performed.

## 2. EliteSCADA Data Source model

### Recommendation: one target BACnet device per Data Source

A BACnet Device Object has a Device Instance that is intended to identify the device uniquely across the BACnet internetwork. Every other object is identified by its Object Identifier within that device.

For EliteSCADA, the most coherent mapping is therefore:

```text
Driver type      = future bacnet client implementation
Data Source      = one target BACnet device / Device Instance
TAG binding      = object + property (+ optional array index) on that device
Network transport= shared runtime infrastructure when compatible
```

Example:

| EliteSCADA Data Source | BACnet Device Instance | Resolved path at runtime |
| --- | ---: | --- |
| `AHU_01` | 12001 | BACnet/IP `10.40.1.21:47808` |
| `AHU_02` | 12002 | BACnet/IP `10.40.1.22:47808` |
| `VAV_FLOOR_2` | 22050 | BACnet network 20 behind router |

The address is not the identity. A controller replacement, DHCP change, NAT/topology change or routing adjustment can change the resolved network address while the engineered device identity remains Device Instance 12001. Any automatic address update must still be presented deterministically when identity or vendor/model evidence conflicts.

### Why not one Data Source per BACnet network?

One network-wide Data Source would make ordinary operational questions unnecessarily ambiguous:

- which controller is down?
- which device caused the timeout rate?
- which TAG set is stale?
- which target changed address?
- which device rejected a write?

It would also violate the existing EliteSCADA principle that failure of one Data Source must not contaminate independent devices.

### Shared transport beneath independent Data Sources

The logical Data Source boundary does not require one UDP socket per device. A future BACnet runtime may share a local BACnet/IP transport among many target devices using the same network/interface/BBMD-FDR configuration. That transport manager can own invoke-ID allocation, datagram reception, address cache and common network registration while routing per-device operations back to isolated Data Source runtimes.

This split is desirable:

```text
BACnet/IP network adapter
    |-- Data Source Device 12001 -> own request/COV/quality/health state
    |-- Data Source Device 12002 -> own request/COV/quality/health state
    `-- Data Source Device 22050 -> own request/COV/quality/health state
```

A shared socket failure may degrade all Data Sources using that adapter. A timeout, reject, COV failure or device restart affecting Device 12001 must not mark Device 12002 as failed merely because both share the socket.

## 3. BACnet/IP network configuration and discovery

### BACnet/IP first transport profile

Future public driver configuration should be able to represent, without committing this research to a final DTO shape:

- target Device Instance;
- enabled state and normal EliteSCADA Data Source identity;
- local network interface/bind-address selection;
- BACnet UDP port, normally 47808 (`0xBAC0`) but configurable;
- optional known/manual target address and BACnet network path hints;
- discovery/address-resolution policy;
- request timeout/retry policy;
- bounded outstanding-request/concurrency policy;
- scan/poll defaults;
- COV preference and renewal policy;
- BBMD/Foreign Device settings when used;
- sanitized vendor/model/protocol-revision metadata captured during Engineering inspection;
- explicit write priority policy for writable points, preferably overrideable per TAG where required.

### Who-Is / I-Am

Who-Is/I-Am is the primary standard device-discovery mechanism for the first product experience.

Recommended Engineering workflow:

1. choose a BACnet/IP network/interface profile;
2. run explicit discovery;
3. send bounded Who-Is, optionally restricted by Device Instance range;
4. collect I-Am responses for a defined time window;
5. reconcile duplicates by Device Instance;
6. inspect Device Object/vendor/model/capabilities;
7. select one discovered device as a Data Source candidate;
8. validate/preview normal Engineering changes;
9. Apply only through canonical Engineering.

Discovery results are transient assistance, not project truth.

### Manual configuration remains mandatory

Industrial networks frequently restrict broadcasts, traverse routers, use multiple IP subnets or deliberately suppress discovery. Engineering therefore must allow:

- known Device Instance + address;
- known Device Instance + routed BACnet network context;
- targeted address test/resolution;
- later rescan/re-resolve without silently changing canonical Engineering.

### BBMD and Foreign Device Registration

BACnet/IP broadcast messages do not naturally cross IP routers. BBMDs exist to distribute BACnet broadcasts across participating IP subnets. A Foreign Device can register with a BBMD and receive forwarded broadcasts for the registration lifetime, renewing before expiry.

EliteSCADA should treat this as explicit infrastructure configuration, not hidden retry logic.

A future BACnet/IP network profile may support modes such as:

- local subnet only;
- local BACnet/IP + configured existing BBMD topology;
- Foreign Device Registration to a known BBMD;
- routed/manual target mode without requiring global discovery.

Important rules:

- EliteSCADA does not auto-configure third-party BBMD Broadcast Distribution Tables.
- Foreign Device Registration address, TTL and renewal state are visible diagnostics.
- loss of FDR affects broadcast reachability/discovery but does not automatically mean every known unicast target is unreachable.
- multiple logical Data Sources sharing one network adapter should not create conflicting duplicate FDR registrations; the shared adapter owns that network-level state.

### No fake “network scan”

BACnet discovery is not OPC UA endpoint probing and it is not a generic TCP port scan. The first BACnet Engineering experience should prefer Who-Is/I-Am plus manual address/device entry. If a future bounded IP-probe aid is ever added, it must be labelled as a non-standard fallback and remain rate-limited/cancellable. It is not required by this research.

## 4. Canonical BACnet TAG binding identity

### Portable identity

A future BACnet communication TAG needs a binding equivalent to:

```text
deviceInstance
objectIdentifier:
  objectType
  objectInstance
propertyIdentifier
arrayIndex?          # only where property-array access is explicitly supported
```

Because the TAG already points to one Data Source, the Device Instance may be represented once at Data Source level in the final DTO. Nevertheless, import/export and validation must preserve or cross-check the device identity so a portable candidate cannot silently attach `analog-input:3` to the wrong controller.

### Names are metadata

Useful metadata includes:

- Object_Name;
- Description;
- object type display text;
- Units;
- vendor ID/name;
- model name;
- location;
- resolved BACnet/IP address;
- current BACnet network number/path;
- protocol revision;
- service/object support summary.

These fields improve Engineering UX and reconciliation, but none should replace the stable object/property identifiers.

### Proprietary objects and properties

BACnet permits vendor-specific extensions. EliteSCADA should not make them invisible and should not guess meanings.

Discovery/import should preserve:

- numeric proprietary object type or property identifier;
- vendor context;
- raw BACnet application datatype information where decode is possible;
- human-readable vendor metadata only when obtained from reliable published information.

An unsupported proprietary value becomes an explicit warning/rejected candidate. Reverse-engineered semantics must never be presented as standard BACnet behavior.

## 5. Object browse and TAG import

### Device-first browser

A useful Engineering sequence is:

```text
Discover/select device
  -> read Device Object/capabilities
  -> enumerate object identifiers
  -> lazy-load selected object metadata/properties
  -> select process properties
  -> build TAG candidates
  -> validate/preview
  -> apply
```

The Device Object's Object_List, Property_List where supported, and standard object/property services provide a better model than crawling arbitrary network addresses.

### Bounded enumeration

Large controllers can expose thousands of objects. The browser should therefore:

- load device/object lists with explicit progress/cancellation;
- page/virtualize on the UI side;
- use RPM for metadata batching when safe;
- partition batches according to device APDU/segmentation capability;
- fall back to RP when necessary;
- filter by object type/name/property/writability;
- clearly label partial results if device limits/errors prevent complete enumeration.

### Candidate model

Transient BACnet import candidates should carry enough evidence to create canonical TAGs without becoming a second Engineering model:

- Data Source/device identity;
- Object Identifier;
- selected Property Identifier;
- array index if applicable;
- proposed TAG path/name;
- proposed EliteSCADA data type;
- object name/description;
- units;
- read/write/commandability evidence;
- current status/reliability metadata when inspected;
- warnings for unsupported/proprietary/ambiguous values;
- proposed scan/COV profile;
- explicit write priority requirement for commandable points.

Selection never mutates the project. It flows into the canonical `validate -> preview -> merge mode -> apply` lifecycle.

### Rescan/reconciliation

A later rescan should produce a diff such as:

- device still resolved at same address;
- device address changed but Device Instance matches;
- object/property unchanged;
- object missing;
- data type changed;
- units changed;
- writeability/commandability changed;
- object name/description changed;
- new candidate object/property;
- Device Instance collision or identity mismatch.

Missing objects do not auto-delete EliteSCADA TAGs or historian data.

## 6. Read acquisition, APDU limits and segmentation

### ReadProperty and ReadPropertyMultiple

The first runtime should support both:

- `ReadProperty` as universal/basic read path;
- `ReadPropertyMultiple` as preferred batching optimization where supported.

RPM must not become a hard requirement merely because it is efficient. Devices can reject the service, restrict request size, impose APDU limits or expose segmentation constraints.

A robust request planner should:

1. discover/cache device max-APDU and segmentation capability from I-Am/Device Object evidence;
2. group compatible reads without assuming one huge request;
3. bound request size and outstanding invoke IDs;
4. reduce/partition a batch on segmentation/size-related reject/abort;
5. fall back to RP for affected points/device when appropriate;
6. remember effective limits for that runtime device instance;
7. expose fallback/segmentation counters in diagnostics.

One unsupported property must not poison every other TAG on the Data Source.

### Timestamps

BACnet property reads do not automatically provide an OPC-UA-like source timestamp for every Present_Value. EliteSCADA must distinguish:

- observation time at the BACnet client;
- device-origin time only where a BACnet service/object genuinely supplies it;
- COV notification receipt time;
- retained application/event timestamps from specific BACnet objects when explicitly modelled later.

Do not fabricate device source timestamps.

## 7. COV and polling

### COV is an optimization/capability, not universal truth

Change-of-Value subscriptions can reduce polling and improve event-driven updates where supported. Not every device/object/property supports the needed COV behavior, and real devices vary in limits.

Recommended initial policy:

- allow a Data Source/profile to prefer COV for eligible points;
- establish subscriptions only after device capability/response proves support;
- use bounded lease/lifetime and renewal where practical rather than relying on immortal hidden state;
- recreate subscriptions after runtime activation, reconnect/device restart/address re-resolution;
- detect silent expiry through age/health monitoring;
- fall back to polling per affected group/point when COV is unavailable or repeatedly fails;
- keep COV notifications inside the normal TAG Engine/current-cache/EventBus path.

### Diagnostics for COV

Useful per-device counters/state include:

- requested subscriptions;
- active subscriptions;
- subscription creation failures;
- renewal count/failures;
- notifications received;
- confirmed-notification acknowledgement failures where used;
- recreation/re-subscribe count;
- points currently using polling fallback;
- age since last update per TAG/group.

COV is not itself TAG quality. A subscription can exist while a device reports FAULT, and a healthy value can be polled when COV is unsupported.

## 8. BACnet value health and EliteSCADA TagQuality

Current EliteSCADA `TagQuality` already includes:

`Good`, `Uncertain`, `Bad`, `BadCommunication`, `BadConfiguration`, `BadDevice`, `Stale`, `Disabled`.

BACnet provides useful object-level status that should feed this model without conflating alarms with quality.

### Companion properties

For objects where the properties exist and are meaningful, inspect:

- `Present_Value`;
- `Status_Flags`;
- `Reliability`;
- `Out_Of_Service`;
- `Units`;
- object-specific capability/range metadata.

`Status_Flags` conveys conditions including IN_ALARM, FAULT, OVERRIDDEN and OUT_OF_SERVICE. `Reliability` conveys device/object reliability state. `Out_Of_Service` can indicate that Present_Value is decoupled from normal physical/process logic.

### Proposed initial mapping policy

This is a product mapping recommendation, not a claim that BACnet defines EliteSCADA quality codes.

| BACnet/runtime evidence | Proposed EliteSCADA treatment |
| --- | --- |
| valid read/update, no fault/oos/override evidence | `Good` |
| transport timeout/address unavailable/no response | `BadCommunication` |
| BACnet object `Reliability` fault or `Status_Flags.FAULT` | `BadDevice` unless a future finer mapping is deliberately added |
| `Out_Of_Service = true` | at least `Uncertain` by default; never silently `Good` |
| `Status_Flags.OVERRIDDEN` | generally `Uncertain` plus diagnostic metadata |
| data age exceeds engineered freshness policy | `Stale` |
| invalid/unsupported engineered binding | `BadConfiguration` |
| engineered TAG disabled | `Disabled` |

`IN_ALARM` alone does **not** make a value bad. Alarm state and point quality are separate product dimensions.

The exact precedence between simultaneous communication/device/OOS/stale conditions should be finalized in the production adapter against existing common quality rules. Protocol-specific detail should remain available in diagnostics even when several BACnet conditions map to one common quality enum.

## 9. WriteProperty, priorities and relinquish

BACnet commandable objects use a 16-level Priority_Array. Lower priority number means higher authority, and relinquishing a priority allows the next active slot or Relinquish_Default to determine the effective Present_Value.

This deserves first-class Engineering semantics because choosing the wrong priority can override schedules, local control logic or safety-related supervisory behavior.

### Fail-closed EliteSCADA policy

For a writable commandable BACnet TAG:

- Engineer must choose an allowed BACnet priority `1..16` before ordinary runtime writes are enabled, unless an explicit future project-wide policy supplies one.
- UI/API should show the effective configured priority.
- Runtime writes must send the configured priority explicitly rather than rely on an SDK/default omission.
- Relinquish is a distinct operation: send BACnet `NULL` at the selected priority.
- Priority change is Engineering configuration, not an arbitrary per-request caller field unless a future authorized command contract explicitly permits it.
- Read-only properties, denied writes and unsupported priorities fail closed.
- Writes still pass normal EliteSCADA authorization/Audit/TAG ownership boundaries.

### Priority is not EliteSCADA authorization

BACnet priority decides arbitration in the target device. It does not authenticate the human/operator and does not replace EliteSCADA capabilities/scopes. A user who lacks process-write authority cannot gain it by requesting priority 1.

### Gateway interaction

A future writable BACnet TAG can become a Gateway destination through the normal TAG/provider write boundary. The gateway does not choose BACnet priority privately. It uses the engineered destination TAG/Data Source write policy.

## 10. Common and BACnet-specific diagnostics

BACnet must join the existing common Data Source diagnostic snapshot rather than invent a browser-only protocol dashboard.

### Common identity/state

Per target BACnet Data Source expose, where meaningful:

- EliteSCADA Data Source key/name;
- driver/transport type;
- target Device Instance;
- vendor ID/name and model;
- current resolved address/network path;
- local network adapter/profile;
- current health state;
- last state change;
- last successful operation;
- last failed operation;
- sanitized last error;
- associated TAG count and quality summary.

### BACnet operation counters

Recommended detail:

- total confirmed requests;
- successful/failed requests;
- timeout count;
- Reject/Abort/Error PDU counts, with sanitized reason/status detail;
- RP count;
- RPM count;
- RPM partition/fallback count;
- WriteProperty count/failures;
- COV create/renew/recreate/notification counters;
- discovery/address-resolution count;
- address-change/rebind events;
- segmentation/reassembly failures where exposed;
- current/effective max-APDU and segmentation capability;
- rolling request latency where measurable;
- observed data age/scan duration.

### Network-adapter diagnostics

Shared BACnet/IP transport infrastructure may additionally expose adapter-level state such as:

- local bind/interface/UDP port;
- receive/send errors;
- BBMD/Foreign Device mode;
- FDR registration state, TTL and renewal failures;
- route/address-cache health.

These adapter facts must not erase per-device isolation.

### Discovery is not runtime health

Failure to receive I-Am during an Engineering scan is not proof that an already configured active Data Source is faulted. Discovery state and active device communication health remain distinct.

## 11. BACnet/SC architecture direction

### What BACnet/SC changes

BACnet Secure Connect uses **WebSockets and TLS** for authenticated, encrypted, reliable connection-oriented BACnet communication. The standard defines a hub-and-spoke topology with a primary hub, failover capability and optional direct connections.

For EliteSCADA this should become a separate transport option, for example conceptually:

```text
BACnet application/object adapter
        |
        +-- BACnet/IP UDP transport
        |
        `-- BACnet/SC secure transport
```

Do not implement it as `useTls=true` on a UDP BACnet/IP socket. BBMD/FDR behavior is also not simply reused for BACnet/SC.

### First BACnet/SC product slice

When the roadmap eventually permits SC production work, start conservatively with:

- client/node connection through configured primary hub;
- configured failover hub;
- certificate-based TLS peer identity;
- explicit trust store / CA profile;
- operational certificate and private-key references;
- reconnect/failover diagnostics;
- normal BACnet device discovery/object services over the established SC network;
- no requirement for optional direct peer connections in the first slice.

### Certificate and secret handling

Canonical Engineering may store only stable protected references and non-secret identity/configuration such as:

- primary hub URI;
- failover hub URI;
- expected BACnet/SC network context;
- trust/CA profile reference;
- node certificate reference;
- private-key secret reference;
- certificate identity/thumbprint metadata where safe;
- renewal/rotation policy references.

It must **not** store:

- private key PEM/PKCS content in canonical JSON;
- key passwords in plaintext;
- resolved secret values in diagnostics;
- “trust any certificate” production flags disguised as convenience.

Unknown, expired, not-yet-valid, untrusted or unexpectedly replaced identities fail closed and produce actionable sanitized diagnostics.

### Current-standard warning

BACnet/SC was introduced through Addendum 135-2016bj, but it has not stood still. ANSI/ASHRAE 135-2024 incorporates certificate/configuration improvements and Authentication/Authorization work, and later addenda continue continuous maintenance. Production SC architecture must therefore be checked against the then-current standard/addenda rather than freezing a 2019-era implementation model.

## 12. Library and implementation candidates

No dependency is selected or added by this research.

### Candidate A: Ela-compil `BACnet` / System.IO.BACnet

Current evidence as of 2026-08-27:

- NuGet `BACnet` **4.0.0**, released 2026-07-30;
- MIT license;
- package targets include .NET 10;
- pure-managed core BACnet package;
- BACnet/IP UDP IPv4/IPv6;
- BBMD and Foreign Device registration;
- Who-Is/I-Am discovery;
- ReadProperty, ReadPropertyMultiple, WriteProperty and related services;
- COV subscriptions/notifications;
- segmentation support;
- async confirmed-request API in 4.x.

**Strengths for EliteSCADA BACnet/IP lab:** natural .NET 10 fit, no native core dependency, useful service coverage, permissive license, active 2026 release activity.

**Risks/unknowns:** interoperability breadth must be proven on real vendors; public feature documentation reviewed here does not advertise BACnet/SC. Some source symbols may mention SC-related enums/properties, but that is not evidence of an operational SC transport. Therefore mark BACnet/SC capability **not demonstrated**.

**Research recommendation:** first BACnet/IP lab candidate, not production selection by decree.

### Candidate B: `bacnet-stack/bacnet-stack`

The C stack is mature and broad, with BACnet application/network/data-link support and a licensing model described as `GPL-2.0-or-later WITH GCC-exception-2.0` for the core, plus files under other licenses.

Current source/build material includes a BACnet/SC data-link option using `libwebsockets`, OpenSSL `ssl/crypto` and platform dependencies. This makes it valuable as a BACnet/SC reference/interoperability peer.

However, production adoption would add:

- native interop/packaging complexity;
- OpenSSL/libwebsockets lifecycle;
- SBOM/license review requirements;
- memory-safety exposure inherent to native parsing code;
- exact-version security maintenance obligations.

The project published numerous security fixes/advisories in 2026, including BACnet/SC certificate-validation and WebSocket-buffer issues plus decoder/memory-safety findings. Current supported lines include 1.4.x through 1.7.x, with patched point releases listed in the repository security policy.

**Research recommendation:** useful patched BACnet/SC and BACnet/IP interoperability peer; not the preferred first managed production dependency without a deliberate security/license/native-packaging review.

### Candidate C: BACnet International BACnet/SC Reference Implementation

BACnet International publishes an open-source Java BACnet/SC Reference Implementation and System Test Bench capable of creating multiple nodes/hubs for interoperability testing.

**Research recommendation:** laboratory oracle/test peer for SC topology, certificate and failover scenarios. Do not pull a Java reference stack into the .NET production runtime merely because it is authoritative test material.

### Commercial stacks

Commercial BACnet stacks exist and may offer stronger certification/support or BACnet/SC maturity. If open-source candidates fail interoperability/security/support requirements, the implementation decision should include vendor proposals covering:

- exact BACnet protocol revision and BIBBs;
- BACnet/IP and BACnet/SC client support;
- COV/segmentation/RPM behavior;
- .NET/native API and Windows/Linux packaging;
- vulnerability response/SBOM;
- redistribution/runtime fees;
- source/escrow/support terms;
- BTL/certification evidence.

No commercial library is recommended here without current vendor-specific evidence and licensing review.

## 13. Security and supply-chain posture

BACnet/IP historically provides no TLS-level confidentiality/authentication comparable to BACnet/SC. Plant-network segmentation and EliteSCADA authorization remain necessary, but they do not transform UDP BACnet/IP into a cryptographically authenticated protocol.

Therefore:

- clearly identify transport as BACnet/IP versus BACnet/SC in Engineering and diagnostics;
- never imply BACnet/IP device identity is cryptographically proven merely because an I-Am says Device Instance X;
- protect Engineering discovery/write endpoints with existing backend authorization;
- rate-limit/bound discovery and request concurrency;
- sanitize protocol errors before UI exposure;
- pin exact reviewed library versions in a future implementation;
- monitor upstream advisories;
- reject “latest” floating production dependencies;
- include native dependencies in SBOM/installer hardening if a C stack is selected;
- preserve certificate/private key separation for SC.

The volume of recent `bacnet-stack` advisories is not a reason to reject all open source. It is evidence that a network-protocol parser must be treated as security-sensitive supply chain, with exact patched versions and ongoing advisory review rather than casual vendoring.

## 14. Interoperability and acceptance plan

### Unit/contract tests

Future production code should cover at least:

- BACnet binding parse/serialization and stable identity;
- Device Instance/Object Identifier/Property Identifier validation;
- BACnet application datatype -> EliteSCADA datatype mapping;
- Status_Flags/Reliability/OOS -> common quality policy;
- priority range `1..16` and explicit relinquish;
- request batching/partition decisions;
- APDU/segmentation boundary handling;
- COV lease/state machine;
- timeout/retry/cancellation;
- diagnostic counter isolation;
- import candidate -> canonical Preview/Apply mapping.

### Software integration laboratory

Use multiple independent implementations rather than one library talking only to itself.

Recommended peers:

- Ela-compil BACnet examples/test devices for .NET-side development;
- current patched `bacnet-stack` sample/device applications;
- BACnet explorers/simulators where licensing and automation permit;
- BACnet International BACnet/SC Reference Implementation/System Test Bench for SC.

Scenarios:

1. local-subnet Who-Is/I-Am discovery;
2. multiple devices with stable unique Device Instances;
3. device address change followed by deterministic re-resolution;
4. BACnet router/network-number traversal;
5. BBMD across at least two IP subnets;
6. Foreign Device Registration, renewal and expiry;
7. RP acquisition;
8. RPM batching and fallback when RPM is rejected;
9. small max-APDU/segmentation boundary conditions;
10. COV creation, update, renewal, server/device restart and resubscribe;
11. polling fallback for a non-COV point/device;
12. `Status_Flags`, `Reliability` and `Out_Of_Service` quality behavior;
13. WriteProperty at explicit priority;
14. relinquish and resulting Priority_Array/Relinquish_Default behavior;
15. read-only/write-denied failures;
16. proprietary object/property visible but not guessed;
17. two target Data Sources on one shared BACnet/IP adapter with independent failure/recovery;
18. Gateway write/read participation only through normal TAG/provider boundaries.

### BACnet/SC laboratory

When the SC slice starts:

1. node connects through primary hub;
2. primary hub loss and failover hub recovery;
3. invalid/untrusted CA;
4. expired/not-yet-valid certificate;
5. changed/replaced node/hub certificate;
6. certificate rotation with deliberate reconciliation;
7. repeated reconnect without resource leak;
8. multiple SC nodes/devices with isolated Data Source health;
9. normal Who-Is/I-Am/object read semantics transported through SC;
10. test against BACnet International reference test bench plus at least one unrelated implementation.

Optional direct SC connections can be a later matrix extension rather than a first acceptance requirement.

### Real hardware acceptance

CI-only emulation is not sufficient for an industrial BACnet driver. Before production release, test at minimum:

- three or more vendors represented in the BTL ecosystem;
- at least one HVAC controller/plant controller;
- one VAV/room-controller class device;
- one remote I/O/meter/device with a different BACnet stack behavior;
- a BACnet router exposing an MS/TP downstream network;
- BBMD/FDR topology;
- device with constrained APDU/segmentation behavior;
- commandable points with Priority_Array;
- COV-capable and non-COV devices;
- at least one real BACnet/SC hub/device ecosystem before claiming SC support.

Record vendor/model/firmware/PICS/BTL listing with test evidence. Do not turn one friendly simulator into “multi-vendor compatibility.”

### BTL direction

BACnet Testing Laboratories certification independently tests products against BACnet requirements/interoperability profiles. EliteSCADA should use BTL-listed third-party devices in the acceptance lab. If EliteSCADA later exposes a BACnet device profile that itself falls within BTL certification scope, product management should evaluate formal BTL testing/certification before market claims are made.

## 15. Proposed production implementation slices

These slices are **future work only** and remain gated by the roadmap.

### Slice A: canonical BACnet Engineering contract

Coordinator-owned integration:

- versioned public BACnet Data Source configuration schema;
- portable TAG binding structure;
- validation for Device/Object/Property identity;
- secret/certificate reference shape reserved for SC;
- canonical JSON/import/export/revision/package round trip;
- migration/version policy.

### Slice B: BACnet/IP network adapter and device resolution

- selected library pinned after review;
- UDP/IP adapter;
- Who-Is/I-Am;
- manual Device Instance/address resolution;
- address cache/reconciliation;
- BBMD/FDR client behavior as configured;
- bounded lifecycle/cancellation.

### Slice C: read acquisition

- RP;
- RPM batching;
- APDU/segmentation-aware partitioning;
- datatype decode;
- companion health properties;
- current cache/EventBus publication;
- common diagnostics.

### Slice D: writes and command priority

- writability validation;
- explicit priority policy;
- WriteProperty;
- relinquish;
- authorization/Audit through existing write boundaries;
- real commandable-device tests.

### Slice E: COV

- capability detection;
- subscription manager;
- renewal/recreation;
- polling fallback;
- data-age/diagnostic semantics.

### Slice F: Engineering discovery/browse/import UX

- protected temporary discovery service;
- device/object browser;
- filters/bulk selection;
- candidate mapping;
- canonical Preview/Apply only;
- rescan/reconciliation.

### Slice G: hardening and multi-device acceptance

- per-Data-Source isolation on shared adapter;
- BBMD/FDR/routing lab;
- multiple vendors;
- failure/recovery/load tests;
- installer/SBOM/security review.

### Slice H: BACnet/SC secure transport

Separate later slice after current-standard and library review:

- primary/failover hub;
- WebSocket/TLS transport;
- certificate/trust/private-key reference infrastructure;
- fail-closed peer validation;
- SC diagnostics;
- reference-test-bench + real-vendor interoperability.

### Slice I: native MS/TP, only if product demand justifies it

- separate RS-485 transport/module;
- baud/MAC/max-master/topology Engineering;
- serial hardware acceptance;
- do not contaminate BACnet/IP/SC Data Source semantics.

## 16. INTEGRATION REQUIRED

The future production implementation requires coordinator-owned central work. None is performed in this research branch.

1. Add the canonical public BACnet Data Source configuration/binding schema to Engineering, including schema migration, validation and versioned import/export.
2. Preserve BACnet bindings through Preview/Apply, immutable revisions, PostgreSQL Engineering persistence and `.escadapkg` backup/restore.
3. Register the eventual BACnet source/compiler/runtime only through normal DriverHost/source-provider composition.
4. Define shared BACnet network-adapter ownership so many per-device Data Sources can reuse transport safely while retaining per-device diagnostic and failure isolation.
5. Finalize BACnet datatype mapping to current `TagDataType` and quality precedence to the current `TagQuality` model.
6. Add protected Engineering discovery/browse/import endpoints rather than allowing frontend-to-device communication.
7. Add BACnet protocol-specific diagnostic detail beneath the existing common Data Source diagnostic contract.
8. Add certificate/trust/private-key reference infrastructure required for BACnet/SC without plaintext secret material.
9. Integrate BACnet TAG writes with existing authorization/Audit and Gateway provider-write boundaries; priority/relinquish remain engineered provider semantics.
10. Add frontend device/object browse/import only after backend transient candidate services exist; browser state is not authoritative Engineering.
11. Pin exact reviewed protocol/library versions and perform license/SBOM/security-advisory review before production dependency addition.
12. Build a repeatable interoperability lab and retain exact device/PICS/BTL evidence for release qualification.

## 17. Key risks and decisions still open

| Topic | Current recommendation | Must be decided before production |
| --- | --- | --- |
| First library | Ela-compil BACnet 4.0.0 as lab candidate | exact pinned version after security/interoperability review |
| BACnet/IP IPv6 | compatible follow-on | acceptance scope/timing |
| Shared adapter | yes, beneath per-device Data Sources | exact runtime ownership API |
| Device identity | Device Instance | reconciliation policy for collisions/replacements |
| RPM | prefer, fallback to RP | adaptive batching algorithm/limits |
| COV | preferred where supported, polling fallback | profile/lifetime defaults |
| OOS/override quality | do not report Good; initial `Uncertain` direction | final common quality precedence |
| Write priority | explicit, no silent product default | per-DS vs per-TAG configuration UX |
| SC library | none selected | managed/native/commercial choice |
| SC direct connections | later optional | whether product demand requires them |
| MS/TP | not first scope | later module demand |
| Formal BTL certification | evaluate later | target BACnet device profile/product claim |

## 18. Sources reviewed

Primary/current BACnet sources:

- BACnet Committee, ANSI/ASHRAE 135-2024 publication notice: https://bacnet.org/news/ansi-ashrae-135-2024-now-published/
- BACnet Committee, current addenda index: https://bacnet.org/addenda/
- BACnet Committee home / standard maintenance: https://bacnet.org/
- BACnet International, BACnet Glossary: https://bacnetinternational.org/bacnet-glossary/
- BACnet International, BACnet Secure Connect: https://bacnetinternational.org/bacnetsc/
- BACnet International, BACnet/SC Reference Implementation release: https://bacnetinternational.org/press-releases/bacnet-international-releases-bacnet-secure-connect-reference-implementation-in-open-source/
- BACnet International, BTL Certification: https://bacnetinternational.org/btl-certification/
- ASHRAE/BACnet historical Annex J Addendum defining BACnet/IP and Foreign Device behavior: https://bacnet.org/wp-content/uploads/sites/4/2022/08/Add-1995-135a.pdf
- BACnet Committee developer material on Foreign Device registration: https://bacnet.org/wp-content/uploads/sites/4/2022/08/Foreign-devices....pdf
- BACnet Committee wide-area BACnet/IP networking/BBMD material: https://bacnet.org/wp-content/uploads/sites/4/2022/06/Building-Wide-Area-Networks-With-BACnet-Part-2.pdf
- BACnet Committee, “The Language of BACnet”: https://bacnet.org/wp-content/uploads/sites/4/2022/06/The-Language-of-BACnet-1.pdf
- ASHRAE Addendum material for commandable Present_Value / Priority_Array / Relinquish_Default: https://www.ashrae.org/File%20Library/Technical%20Resources/Standards%20and%20Guidelines/Standards%20Addenda/135-1995_Addendum-b-2000.pdf

Library/test evidence:

- Ela-compil BACnet repository: https://github.com/ela-compil/BACnet
- NuGet BACnet 4.0.0: https://www.nuget.org/packages/BACnet/4.0.0
- Ela-compil BACnet releases: https://github.com/ela-compil/BACnet/releases
- bacnet-stack repository: https://github.com/bacnet-stack/bacnet-stack
- bacnet-stack security policy/advisories: https://github.com/bacnet-stack/bacnet-stack/security
- bacnet-stack changelog: https://github.com/bacnet-stack/bacnet-stack/blob/master/CHANGELOG.md
- BACnet/SC Reference Stack / System Test Bench: https://sourceforge.net/projects/bacnet-sc-reference-stack/

## Final classification

**RESEARCH IN PR:** BACnet/IP + BACnet/SC architecture, discovery, binding, read/write/COV/quality/diagnostics, library and interoperability direction documented here.  
**PRODUCTION NOT IMPLEMENTED:** no BACnet package, source provider, Data Source registration, Engineering schema, API, UI, DI/runtime or BACnet/SC certificate infrastructure has been added.  
**MERGED:** no. Coordinator review and a future roadmap gate are required before any production implementation.