# DNP3 Master / Client driver research — EliteSCADA

Status: **RESEARCH IN DRIVER BRANCH / PRODUCTION NOT IMPLEMENTED**

Research date: 2026-08-29

Branch: `driver7/dnp3`

Authorization baseline: `149a28c4bcc1e545ac2e43f7e7db40b9864724eb`

This document is the required research-first deliverable for **DEV Driver 7 — DNP3**. It defines the proposed first EliteSCADA DNP3 Master/Client boundary before substantial runtime implementation begins.

It does **not** add a production DNP3 Data Source, protocol dependency, runtime registration, Engineering schema migration, DriverHost composition, API endpoint or frontend behavior. The driver workstream remains intentionally parked from the mainline WAVEs until the Coordinator explicitly decides how and when driver branches should be integrated.

The design preserves the existing EliteSCADA authority flow:

`Canonical Engineering -> DriverHost/compiler -> Data Source/driver -> TAG Current Cache -> Event Bus -> Historian / Alarms / Realtime / Gateway`

DNP3 is one more communication provider behind that boundary. It does not create a DNP3-specific TAG database, quality model, historian path, Gateway implementation or browser-side source of project truth.

---

## 1. Executive recommendation

1. **Implement EliteSCADA as a DNP3 Master/Client first.** The initial transport is TCP. DNP3 serial must remain architecturally possible but is outside the first implementation cut unless separately promoted.
2. **Represent one target outstation association as one EliteSCADA Data Source in the initial design.** A DNP3 library may allow multiple associations to share one lower-level channel, but Data Source ownership, health, counters, point quality and writes remain isolated per configured outstation.
3. **Use a hybrid acquisition model.** Startup integrity, periodic integrity/event scans where configured, and unsolicited event responses all feed the normal TAG pipeline. The driver descriptor should therefore advertise `DriverAcquisitionMode.Hybrid`.
4. **Use the standard startup handshake pattern:** disable configured unsolicited Class 1/2/3, perform startup integrity for Class 1/2/3/0, then enable configured unsolicited classes. Repeat the required recovery sequence after an outstation restart is detected. This is also the default/conformance-oriented direction of the modern Step Function stack.
5. **Make point identity independent of response variation.** A DNP3 point must be identified by the configured outstation plus logical point kind and DNP3 index. Static/event group and preferred variation are persisted protocol configuration, but they are not the sole stable identity because one physical point is represented by different groups for static versus event data.
6. **Preserve DNP3 flags as protocol evidence while mapping them to EliteSCADA `TagQuality`.** Do not replace the common quality model with DNP3 flags, and do not discard the raw flag information needed for diagnostics.
7. **Preserve device/event time in `TagValue.SourceTimestamp` when the received variation actually carries a usable timestamp.** `TagValue.Timestamp` remains local EliteSCADA observation/publication time. A DNP3 object with no timestamp must not receive a fabricated source timestamp.
8. **Treat late/backlogged events as a first-class integration concern.** The current `CurrentTagCache` is last-write-wins by arrival order. A DNP3 reconnect can legitimately deliver historical event backlog after a newer current/static value. A future implementation must not let old event history silently replace a newer current process value. This requires an explicit common ingestion/current-value ordering decision before production integration.
9. **Support Binary Input, Double-Bit Binary Input, Analog Input, Counter and Frozen Counter in the first read scope.** Also support Binary Output Status and Analog Output Status as readback/status point families because commands should not be confused with feedback state.
10. **Use DNP3 command semantics explicitly.** Binary controls are CROB (`Group 12 Variation 1`) with operation code, trip/close code, count and on/off times. Analog outputs use Group 41 typed commands. A DNP3 command is never modeled as an unqualified generic “set value”.
11. **Default control execution to Select-Before-Operate where the device/profile supports it.** Direct Operate may be explicitly engineered when required. `DIRECT_OPERATE_NO_RESPONSE` is not in the first safety baseline because it removes command-result visibility.
12. **Declare command success only from a successful protocol operation and per-point command status.** Queueing/sending bytes is not success. Timeout, bad command status, response mismatch and transport loss are visible failures.
13. **Do not invent generic DNP3 network browsing.** DNP3 does not provide an OPC-UA-like universal address-space browse. Engineering may observe points returned by bounded integrity/class reads and turn them into transient candidates, clearly marked partial, but it must not claim a complete point list unless external device documentation/configuration provides one.
14. **Use Step Function I/O DNP3 1.6.x as the preferred technical implementation candidate for laboratory development, not as an unconditional production dependency.** It is current, asynchronous, supports Master/Outstation, TCP/TLS/serial, .NET bindings, unsolicited processing, commands and the required object families. However, its public license is explicitly non-commercial/non-production. Commercial production or redistribution requires a negotiated commercial license.
15. **Keep Triangle MicroWorks DNP3 Source Code Library as the primary commercial/vendor-supported comparison candidate.** Its current public information shows v3.34 (July 2026), a .NET component, DNP3 Master features, TLS/Secure Authentication options and long-term conformance support. Pricing, redistribution rights and exact .NET 10 packaging still require vendor evaluation.
16. **Do not select archived OpenDNP3 as the production default.** OpenDNP3 3.1.2 is Apache 2.0 and historically had .NET bindings, but the repository was archived on 2022-09-01. It is viable only if EliteSCADA deliberately accepts ownership of an internal security/maintenance fork.
17. **Do not implement IEEE 1815 from scratch for the first production driver.** DNP3 application/link/transport behavior, fragment handling, qualifiers, event classes, unsolicited state, command state machines and conformance requirements are too broad to justify a new protocol stack merely to avoid dependency selection.
18. **Acquire the applicable IEEE 1815/DNP3 specification before production protocol work is considered complete.** Library documentation is useful engineering evidence but is not a substitute for the standard.
19. **Keep DNP3 Secure Authentication and serial transport explicitly deferred from the first cut.** TLS can be supported when host-owned certificate/secret resolution is available and the selected production library supports the required profile. Secure Authentication SAv5 requires a deliberate later security slice rather than an opaque library switch.
20. **Require independent interoperability evidence before production.** Loopback tests using the same stack on both ends are valuable but insufficient. At least one independent simulator/test harness and representative field-device validation are required before claiming production compatibility.

---

## 2. Existing EliteSCADA contracts that govern this driver

### 2.1 Runtime boundary

The existing `ICommunicationDriver` contract already provides the essential active-runtime surface:

- `StartAsync` / `StopAsync`;
- `ReadAsync(tagId)`;
- `WriteAsync(tagId, value)`;
- Driver identity, capabilities, status and TAG collection;
- asynchronous disposal.

DNP3-specific channel, association, task, sequence, qualifier and object-header types must remain internal adapter details. They must not leak into `Scada.Core` or canonical Engineering.

### 2.2 Driver Engineering boundary

The existing Driver SDK already separates active runtime from Engineering operations and already has the concepts needed by the proposed driver:

- `CommunicationDriverTypeDescriptor`;
- versioned `DriverConfigurationSchemaDescriptor`;
- `ConnectionTest`;
- optional `Discover`;
- `Browse`;
- `FileImport`;
- `Reconcile`;
- acquisition modes including `Hybrid`.

The first DNP3 adapter should prefer `ConnectionTest`, bounded observed-point `Browse` and `Reconcile`. It must not advertise discovery or file import merely for UI symmetry if no trustworthy implementation exists.

### 2.3 Common TAG value semantics

Current Core already provides:

- `TagQuality`: Good, Uncertain, Bad, BadCommunication, BadConfiguration, BadDevice, Stale and Disabled;
- `TagDataType`: Boolean, Int16, Int32, Int64, Float, Double, String, DateTime and Enum;
- `TagValue.Timestamp`: local EliteSCADA observation/publication time;
- optional `TagValue.SourceTimestamp`;
- optional `TagValue.ServerTimestamp`.

DNP3 maps into these contracts. It does not add an alternate `DnpQuality`, `DnpTagValue` or parallel current-value cache.

### 2.4 Gateway boundary

DNP3 participates in the existing protocol-independent Gateway only through TAGs:

`DNP3 TAG -> normal TAG event -> Gateway -> destination TAG owner`

and:

`source TAG -> Gateway -> DNP3 TAG owning driver -> DNP3 write/command adapter`

No `DNP3-to-Modbus` or `DNP3-to-OPC-UA` pairwise API is permitted.

### 2.5 Bit-selector boundary

Native DNP3 Binary Input / Binary Output Status points are already Boolean process points. They do not need an artificial word-bit selector merely to satisfy the EliteSCADA TAG-bit contract.

If a future DNP3 object family exposes a bit-addressable integer storage concept that is deliberately mapped as one canonical integer TAG, normal EliteSCADA logical bit selectors may operate on the resulting canonical integer. The DNP3 driver must not create a second `.NN` identity system.

---

## 3. Library / implementation strategy and licensing gate

### 3.1 Step Function I/O DNP3 1.6.x — preferred laboratory candidate

Current research evidence checked on 2026-08-29:

- current stable documentation line is 1.6.0;
- Rust core with bindings for C, C++, .NET and Java;
- Master and Outstation components;
- TCP, mutually authenticated TLS and serial support;
- asynchronous implementation;
- Windows x64 is an officially supported platform;
- Master API exposes association configuration, startup integrity, unsolicited handling, periodic scans, time synchronization and command modes;
- the variation set covers the first-release point and command families proposed below.

The .NET API is suitable for an adapter in the current .NET product without making Rust types part of EliteSCADA public contracts.

**License gate:** Step Function states that the publicly available library is under a non-commercial / non-production license and that commercial use requires a purchased commercial license. Therefore:

- it may be evaluated for this research/laboratory branch subject to its license terms;
- EliteSCADA must not assume redistribution rights;
- a production dependency may be locked only after commercial terms, redistribution, support, platform packaging and upgrade obligations are reviewed and accepted;
- no binary/package should be committed as a permanent distributable dependency merely because the API is technically convenient.

Recommended status:

`PREFERRED TECHNICAL CANDIDATE / COMMERCIAL-LICENSE GATE OPEN`

### 3.2 Triangle MicroWorks — vendor-supported commercial comparison

Triangle MicroWorks currently advertises:

- DNP3 Master and Outstation source-code libraries;
- .NET component availability;
- current release v3.34 dated July 2026;
- Windows/Linux support in related products;
- TLS and DNP3 Secure Authentication capabilities;
- protocol/conformance tooling and long-term vendor support.

This is the strongest comparison when EliteSCADA values commercial support, protocol test tooling, formal conformance assistance and explicit Secure Authentication roadmapping.

Required evaluation before selection:

- commercial price and redistribution model;
- .NET 10 compatibility rather than only documented .NET 6/8 support;
- native deployment footprint on Windows x64;
- cancellation/async behavior under the EliteSCADA driver lifecycle;
- packaging implications for future installable Driver Modules;
- API ergonomics for per-Data-Source isolation;
- automated headless testability in CI;
- vendor update/security-advisory process.

Recommended status:

`COMMERCIAL PRODUCTION CANDIDATE / PROCUREMENT + API FIT TO VERIFY`

### 3.3 OpenDNP3 3.1.2 — open-license legacy fallback only

OpenDNP3 is Apache 2.0 and historically provides C++ plus .NET/Java bindings. Its repository was archived on 2022-09-01 and is explicitly end-of-life.

Advantages:

- permissive license;
- mature historical implementation;
- public source;
- useful conceptual/reference material.

Blocking concern:

- selecting it for production transfers protocol-stack maintenance, CVE triage, toolchain modernization, .NET/native compatibility and future IEEE 1815 evolution to EliteSCADA.

Recommended status:

`REJECT AS DEFAULT PRODUCTION STACK / ALLOW ONLY BY EXPLICIT MAINTENANCE-FORK DECISION`

### 3.4 From-scratch implementation

A new DNP3 stack is rejected for the initial product implementation.

The driver team should own the EliteSCADA adapter, Engineering identity, mapping, diagnostics and product behavior. It should not also volunteer to reimplement the entire wire protocol unless licensing/technical evidence later makes every mature stack unusable and the Coordinator deliberately authorizes that much larger scope.

### 3.5 Production dependency decision gate

Before production code locks one stack, re-check:

1. current stable version and support lifecycle;
2. license and redistribution terms;
3. Windows x64 and .NET 10 compatibility;
4. published security advisories;
5. TCP Master behavior and reconnect cancellation;
6. unsolicited event handling;
7. required group/variation coverage;
8. CROB/analog output command support;
9. time synchronization behavior;
10. TLS and future Secure Authentication path;
11. protocol logging sanitization;
12. independent interoperability evidence;
13. future serial support without public-contract breakage.

No implementation-library type is permitted in canonical Engineering regardless of which candidate wins.

---

## 4. Initial DNP3 product boundary

### 4.1 Initial role

EliteSCADA is a **DNP3 Master/Client**.

A configured Data Source initiates communication to one DNP3 outstation endpoint and owns the configured points associated with that outstation.

### 4.2 Initial transport

Required first transport:

- TCP client connection;
- configurable host/IP;
- configurable port, with UI/default profile able to offer the conventional DNP3 TCP port 20000;
- explicit local/master DNP3 link-layer address;
- explicit remote/outstation DNP3 link-layer address;
- bounded connect timeout;
- bounded response timeout;
- deterministic reconnect strategy/backoff;
- optional link-status keepalive when supported by the selected stack.

Not first-cut requirements:

- serial transport;
- UDP transport;
- Master acting as a TCP listener;
- DNP3 Outstation/Server role.

The public schema should use a transport discriminator so adding serial later does not require changing DNP3 point identity.

### 4.3 One Data Source per outstation association

Initial authoritative rule:

**One DNP3 EliteSCADA Data Source represents one configured DNP3 outstation association.**

That gives each target:

- one canonical Data Source identity;
- one remote DNP3 address;
- independent health and reconnect state;
- independent counters;
- independent point set;
- independent write/command ownership;
- independent authorization context through its TAGs;
- deterministic activation/disposal.

A production stack may later share a TCP/serial channel among multiple associations if the protocol topology and library make that useful. If implemented, shared transport is an internal optimization. Logical Data Source state and TAG quality remain isolated.

For the first descriptor, `SupportsSharedTransportInfrastructure` should remain `false` unless the actual implementation proves a safe host-level shared channel design.

### 4.4 Proposed stable Driver type identity

Recommended initial Driver type key:

`dnp3.master.tcp`

If the future module system prefers one family descriptor plus a transport field, a later compatible migration may use a broader `dnp3.master` identity. Do not create separate TAG identity semantics for TCP versus serial.

---

## 5. Proposed Data Source Engineering profile

This section defines semantic fields, not a canonical Engineering schema migration. Exact DTO/schema integration remains Coordinator-owned.

Suggested Data Source fields:

| Field | Type | Initial policy |
| --- | --- | --- |
| `transport` | Enum | `tcp` only initially; reserve `serial` |
| `host` | Host | required for TCP |
| `port` | Port | default 20000 |
| `masterAddress` | Integer | required, validated DNP3 link address |
| `outstationAddress` | Integer | required, validated DNP3 link address |
| `connectTimeout` | Duration | bounded |
| `responseTimeout` | Duration | bounded |
| `reconnectMinDelay` | Duration | bounded |
| `reconnectMaxDelay` | Duration | >= min, bounded |
| `keepAliveTimeout` | Duration/optional | no fake keepalive if stack/profile disables it |
| `startupIntegrityClasses` | Enum/set | default Class 0/1/2/3 |
| `disableUnsolicitedClassesOnStartup` | Enum/set | default 1/2/3 |
| `enableUnsolicitedClassesAfterIntegrity` | Enum/set | default 1/2/3 |
| `eventScanOnEventsAvailable` | Enum/set | configurable 1/2/3 |
| `integrityPollInterval` | Duration/optional | bounded; optional periodic resync |
| `class1PollInterval` | Duration/optional | fallback where unsolicited unavailable/disabled |
| `class2PollInterval` | Duration/optional | fallback |
| `class3PollInterval` | Duration/optional | fallback |
| `integrityOnEventBufferOverflow` | Boolean | recommended true after implementation verification |
| `timeSyncMode` | Enum | `none`, later explicit `lan`, `nonLan` |
| `timeSyncPolicy` | Enum | disabled by default; explicit automation only |
| `maxQueuedUserRequests` | Integer | bounded, protects commands/adhoc reads |
| `tlsMode` | Enum | `none` first; future explicit TLS profile |
| `clientCertificateRef` | CertificateReference | only when TLS implemented |
| `serverTrustRef` | CertificateReference | only when TLS implemented |
| `expectedDeviceIdentity` | Identifier/optional | future connection/reconciliation evidence |

Validation rules must include:

- local and remote DNP3 addresses must not be equal unless a deliberate protocol profile supports/justifies it;
- special/broadcast/self addresses are not accepted as ordinary unicast Data Source identity without explicit support;
- all durations are bounded and non-negative;
- reconnect max cannot be lower than reconnect min;
- polling intervals cannot be configured below product-safe limits;
- unsolicited class sets are limited to Class 1/2/3;
- Class 0 is integrity/static data, not an unsolicited event class;
- TLS fields are rejected/ignored only through explicit validation semantics, never silently accepted when TLS is not implemented;
- secret/certificate material is represented only by protected references.

---

## 6. Stable DNP3 point identity and binding

### 6.1 Why Group/Variation alone is not identity

DNP3 deliberately represents the same logical point family differently depending on context:

- Binary Input static data uses Group 1;
- Binary Input event data uses Group 2;
- Double-Bit Binary static uses Group 3;
- Double-Bit Binary events use Group 4;
- Analog Input static uses Group 30;
- Analog Input events use Group 32;
- Counter static uses Group 20;
- Counter events use Group 22.

Variations additionally change representation width, flags and timestamp presence.

Therefore a point that is persisted only as `group=2, variation=2, index=7` becomes unnecessarily unstable when a device reports the same point through a different valid event/static variation.

### 6.2 Recommended stable identity

Within one canonical Data Source, the durable DNP3 point identity is conceptually:

```text
pointKind + index
```

The Data Source already owns the remote outstation association, so the full project-level physical identity is conceptually:

```text
DataSourceId / outstationAddress / pointKind / index
```

Recommended `pointKind` values for first release:

- `binaryInput`;
- `doubleBitBinaryInput`;
- `analogInput`;
- `counter`;
- `frozenCounter`;
- `binaryOutputStatus`;
- `analogOutputStatus`.

Command capability attaches to the corresponding output/control binding but remains explicitly configured.

### 6.3 Persisted representation configuration

The binding should also preserve:

- DNP3 index;
- static object group expected/preferred for the point kind;
- optional preferred static variation or `deviceDefault`/`any`;
- event object group expected for the point kind;
- optional preferred event variation or `deviceDefault`/`any`;
- expected point class when known/configured, as validation/diagnostic evidence rather than hidden identity;
- access intent `read` / `readWrite`;
- command profile for writable output targets;
- optional engineering units/deadband metadata where canonical TAG Engineering owns those semantics.

### 6.4 Portable address direction

Until a richer canonical protocol binding DTO is deliberately migrated, any string `PortableAddress` used by Engineering candidates must be deterministic and round-trippable. Example **display/adapter concept only**:

```text
dnp3:binaryInput:42
ndnp3:analogInput:7
```

or a structured JSON-equivalent in the future rich binding model.

The authoritative persisted identity must never depend on a localized display label such as `Breaker Open`.

### 6.5 Sparse indices are normal

DNP3 point indices must not be assumed to be contiguous or zero-based in the device database. Engineering, browse candidates, batching and runtime maps must support sparse indices naturally.

---

## 7. First-release object groups and variations

The first driver should support a deliberately bounded but practical object set. `Variation 0` requests may be used when the outstation chooses its default representation, but the received concrete variation still determines decoding and timestamp/flag availability.

### 7.1 Binary Input

Static:

- G1V1 — packed;
- G1V2 — with flags.

Events:

- G2V1 — without time;
- G2V2 — absolute time;
- G2V3 — relative time.

EliteSCADA type: `Boolean`.

### 7.2 Double-Bit Binary Input

Static:

- G3V1 — packed;
- G3V2 — with flags.

Events:

- G4V1 — without time;
- G4V2 — absolute time;
- G4V3 — relative time.

EliteSCADA type: **`Enum`**, not Boolean.

Required logical states:

- `Intermediate`;
- `DeterminedOff`;
- `DeterminedOn`;
- `Indeterminate`.

A Double-Bit Binary point must not silently coerce intermediate/indeterminate to `false` merely to fit a Boolean UI.

### 7.3 Analog Input

Static G30:

- V1 — Int32 with flags;
- V2 — Int16 with flags;
- V3 — Int32 without flags;
- V4 — Int16 without flags;
- V5 — Float32 with flags;
- V6 — Float64 with flags.

Events G32:

- V1/V2 — Int32/Int16 without time;
- V3/V4 — Int32/Int16 with time;
- V5/V6 — Float32/Float64 without time;
- V7/V8 — Float32/Float64 with time.

EliteSCADA type maps directly to `Int32`, `Int16`, `Float`, or `Double` according to the engineered/observed point representation and canonical TAG type validation.

### 7.4 Counter

Static G20:

- V1 — UInt32 with flags;
- V2 — UInt16 with flags;
- V5 — UInt32 without flags;
- V6 — UInt16 without flags.

Events G22:

- V1 — UInt32 with flags;
- V2 — UInt16 with flags;
- V5 — UInt32 with flags and time;
- V6 — UInt16 with flags and time.

EliteSCADA mapping:

- DNP3 UInt16 counter -> `Int32` (preserves 0..65535);
- DNP3 UInt32 counter -> `Int64` (preserves 0..4294967295).

Do not reinterpret values above signed Int32 max as negative.

### 7.5 Frozen Counter

Static G21:

- V1/V2 — UInt32/UInt16 with flags;
- V5/V6 — UInt32/UInt16 with flags and time;
- V9/V10 — UInt32/UInt16 without flags.

Events G23:

- V1/V2 — UInt32/UInt16 with flags;
- V5/V6 — UInt32/UInt16 with flags and time.

EliteSCADA numeric mapping follows Counter.

Freeze commands themselves are **not** part of the first normal operator write surface. Reading frozen counters does not imply authorization to issue Immediate Freeze / Freeze Clear operations.

### 7.6 Binary Output Status / readback

Static:

- G10V1 packed;
- G10V2 with flags.

Events:

- G11V1 without time;
- G11V2 with time.

EliteSCADA type: `Boolean`.

This is feedback/status, not the command itself. A successful CROB does not allow EliteSCADA to fabricate feedback state before the outstation actually reports it.

### 7.7 Binary Output command

- G12V1 — Control Relay Output Block (CROB).

First command-profile fields:

- command mode: `selectBeforeOperate` or `directOperate`;
- operation: `latchOn`, `latchOff`, `pulseOn`, `pulseOff` where supported;
- trip/close code when meaningful: `nul`, `trip`, `close`;
- count;
- on-time;
- off-time;
- expected feedback TAG/reference optional, handled outside the protocol command status.

`directOperateNoResponse` is not first-cut supported.

### 7.8 Analog Output Status / readback

Static G40:

- V1 — Int32 with flags;
- V2 — Int16 with flags;
- V3 — Float32 with flags;
- V4 — Float64 with flags.

Events G42 should be supported for the corresponding status event representations supported by the selected production stack.

EliteSCADA type: matching numeric type.

### 7.9 Analog Output command

G41:

- V1 — Int32;
- V2 — Int16;
- V3 — Float32;
- V4 — Float64.

The engineered TAG type and command variation must be compatible. No silent numeric narrowing is allowed.

### 7.10 Time and class objects

Required protocol infrastructure includes:

- G50V1 absolute time as required by time-sync behavior;
- G51V1/V2 Common Time of Occurrence for relative event timestamps;
- G52V1/V2 time delay as required by non-LAN time synchronization;
- G60V1 Class 0;
- G60V2 Class 1;
- G60V3 Class 2;
- G60V4 Class 3;
- G80V1 Internal Indications as protocol state/diagnostic evidence, not a normal user TAG family by default.

### 7.11 Deferred object families

Not required in the first production cut unless separately promoted:

- Device Attributes G0;
- Frozen Analog Input G31/G33;
- Analog deadbands G34 write/configuration;
- Binary/Analog command-event groups G13/G43 as user-facing process TAGs;
- file transfer G70;
- octet strings G110/G111;
- datasets;
- unsigned integer G102;
- restart/application-control function codes;
- counter-freeze control operations.

The library may support them internally; that does not make them automatically exposed product features.

---

## 8. Class processing, integrity and unsolicited behavior

### 8.1 Acquisition mode

DNP3 should declare `Hybrid` acquisition because values may arrive from:

- startup integrity reads;
- periodic Class 0 integrity scans;
- periodic Class 1/2/3 event scans;
- automatic scans when event-available IIN bits are observed;
- unsolicited responses;
- ad-hoc Engineering/runtime reads where supported.

All accepted measurements ultimately publish normal EliteSCADA `TagValue`s.

### 8.2 Startup sequence

Recommended default association startup:

1. establish TCP and DNP3 association;
2. disable unsolicited Class 1/2/3 according to configured policy;
3. perform startup integrity read for Class 1/2/3/0;
4. process static and event values;
5. clear/reconcile restart state as the selected stack/standard requires;
6. enable configured unsolicited Class 1/2/3;
7. begin periodic fallback/integrity tasks.

The purpose is to establish a coherent baseline before live unsolicited event flow becomes authoritative.

### 8.3 Integrity polling

A periodic Class 0 integrity scan is recommended as an optional bounded resynchronization mechanism, particularly for sites using unsolicited events.

It must be configurable because device/network scale varies significantly. Engineering must reject pathological sub-second “integrity polling” that defeats the event-oriented protocol model.

### 8.4 Class 1/2/3 event scans

If unsolicited responses are unavailable, disabled, unreliable or deliberately not used, configurable periodic Class 1/2/3 polls provide deterministic fallback acquisition.

The driver must not hard-code semantic meaning such as “Class 1 always alarms”. Class assignment is an outstation engineering decision. EliteSCADA treats classes as DNP3 event-priority/acquisition evidence, while the canonical Alarm Engine remains separate.

### 8.5 Unsolicited responses

Unsolicited events are accepted only for configured classes and processed through the same point maps as solicited reads.

Requirements:

- sequence/fragment protocol rules remain the selected DNP3 stack’s responsibility;
- duplicated retransmission must not create fabricated state changes where the stack already de-duplicates/acknowledges protocol messages;
- an unsolicited event with bad DNP3 flags must map to degraded/bad TAG quality, not be dropped merely because it is inconvenient;
- unsolicited reception updates communication diagnostics separately from point quality;
- inability to enable unsolicited responses must be visible and must fall back only according to explicit Engineering policy.

### 8.6 Restart and buffer-overflow recovery

When outstation restart is indicated:

- mark the association/Data Source degraded/recovering;
- perform the configured startup integrity/recovery task sequence;
- do not assume the last cached values are still current merely because the TCP socket survived;
- preserve previous values only with non-Good quality where common runtime semantics require it.

When event-buffer overflow is indicated, an automatic integrity scan should be supported/configurable. Lost event history cannot be reconstructed from a static integrity scan, so diagnostics must explicitly record that event continuity was lost.

---

## 9. Measurement mapping to EliteSCADA values

### 9.1 Common rule

For every received measurement:

- resolve the canonical TAG by Data Source + point kind + index;
- validate that the received object family is compatible with the engineered TAG;
- decode the value without unsafe coercion;
- map DNP3 flags to `TagQuality`;
- preserve raw flag details for diagnostics;
- set local `Timestamp` when EliteSCADA publishes the sample;
- set `SourceTimestamp` only when the object provides a valid DNP3 time;
- do not fabricate `ServerTimestamp`; DNP3 has no OPC-UA-style separate server timestamp contract in this design.

### 9.2 Type table

| DNP3 family | EliteSCADA type |
| --- | --- |
| Binary Input | Boolean |
| Double-Bit Binary Input | Enum |
| Analog Input Int16 | Int16 |
| Analog Input Int32 | Int32 |
| Analog Input Float32 | Float |
| Analog Input Float64 | Double |
| Counter UInt16 | Int32 |
| Counter UInt32 | Int64 |
| Frozen Counter UInt16 | Int32 |
| Frozen Counter UInt32 | Int64 |
| Binary Output Status | Boolean |
| Analog Output Status | Int16/Int32/Float/Double |

### 9.3 No-flags variations

When a valid DNP3 variation contains no flags, the driver cannot pretend it received per-point ONLINE/RESTART/etc. evidence.

Policy:

- communication/protocol success can establish that the object was decoded successfully;
- point quality may be `Good` when the selected variation is intentionally flagless and no contrary association/protocol evidence exists;
- diagnostics must record that point flags were absent for that sample;
- if an engineer requires flag fidelity, select/request a variation carrying flags where the outstation supports it.

Do not synthesize a fake DNP3 ONLINE flag.

---

## 10. DNP3 flags -> EliteSCADA quality mapping

DNP3 flags are richer protocol evidence than the common `TagQuality` enum. The driver therefore performs two actions:

1. map them into one canonical `TagQuality` for normal product behavior;
2. retain a sanitized protocol flag set in diagnostics/sample metadata where the future common diagnostic contract permits it.

Recommended initial precedence:

| DNP3 evidence | EliteSCADA quality | Rationale |
| --- | --- | --- |
| communication/session timeout/no response | `BadCommunication` | no trustworthy live sample |
| `COMM_LOST` | `BadCommunication` | value is last known before communication failure |
| ONLINE clear | `BadDevice` | originating point reports itself not online/valid |
| `REFERENCE_ERR` | `BadDevice` | measurement accuracy/reference problem |
| `OVER_RANGE` | `Uncertain` or `BadDevice` by point policy | value representation is outside expected range; preserve value but do not call it fully Good |
| `RESTART` | `Uncertain` | value has not been refreshed since device restart |
| `REMOTE_FORCED` | `Uncertain` | downstream override is active |
| `LOCAL_FORCED` | `Uncertain` | local override is active |
| `CHATTER_FILTER` | `Uncertain` | device reports abnormal rapid changes/filtering |
| counter `DISCONTINUITY` | `Uncertain` | absolute value may be usable but delta continuity is broken |
| ONLINE and no adverse flags | `Good` | nominal point evidence |

If multiple flags are present, worst applicable quality wins.

`OVER_RANGE` requires a final shared mapping decision during implementation because some applications prefer `Uncertain` for a usable clipped representation while others consider it device-bad. The raw flag must remain visible either way.

A point-level bad flag does not necessarily fault the entire Data Source. Conversely, a healthy TCP channel does not force every point to `Good`.

---

## 11. DNP3 time and timestamp semantics

### 11.1 Local versus source time

EliteSCADA mapping:

- `TagValue.Timestamp` = local receipt/publication time;
- `TagValue.SourceTimestamp` = DNP3 measurement/event time when present and resolvable;
- `TagValue.ServerTimestamp` = null for the initial DNP3 model.

### 11.2 Absolute timestamps

Event variations carrying absolute time map directly to `SourceTimestamp` after validated UTC/time conversion according to DNP3 semantics and the selected library API.

### 11.3 Relative timestamps and CTO

Relative-time event variations require a Common Time of Occurrence (G51) context.

The selected stack should resolve this according to the standard. If the timestamp context is missing/invalid, the driver must not invent a source timestamp from local receipt time.

### 11.4 Synchronized versus unsynchronized DNP3 time

When the stack identifies a DNP3 timestamp as unsynchronized:

- preserve the timestamp as source evidence if it is syntactically usable;
- downgrade sample quality to at least `Uncertain` or expose a dedicated timestamp-quality diagnostic, according to the final common policy;
- never present it as authoritative synchronized event time without indication.

This is another shared policy that should be reconciled with historian/query UX so operators can distinguish “event time exists” from “device clock was trusted”.

### 11.5 Master time synchronization

Initial default: **disabled unless explicitly engineered**.

Future explicit modes:

- LAN time synchronization;
- non-LAN delay-measurement synchronization.

If automatic time sync is enabled in response to NEED_TIME IIN:

- the action is deliberate Data Source Engineering, not an invisible stack default;
- failure to synchronize is diagnosable;
- clock rollback and excessive delay errors are surfaced;
- synchronization must not become a generic unaudited system-clock write API exposed to frontend/scripts.

---

## 12. Late/backlogged events and current-value correctness

This is the most important cross-domain integration issue discovered by this research.

The current `CurrentTagCache.UpdateAsync` replaces the current value strictly by **arrival order**. DNP3 can legitimately deliver event history after reconnect, including values whose source timestamps are older than a more recent static/current value already received.

Unsafe behavior would be:

```text
12:00:10 current/static value = ON
12:00:11 reconnect event backlog arrives with SourceTimestamp 11:59:55 = OFF
CurrentTagCache becomes OFF merely because the old event arrived later
```

That would corrupt operator-visible current state while preserving a perfectly valid historical event.

Production integration therefore requires one of these deliberately selected common strategies:

### Option A — source-time-aware current cache policy

The TAG ingestion boundary accepts every sample for events/historian but updates the current cache only if the candidate is newer according to a trustworthy source-time ordering policy.

Risk: not every protocol/sample has a trustworthy source timestamp, and unsynchronized clocks complicate ordering.

### Option B — separate current/static update from historical event ingestion

The driver publishes event history through a common event-sample path and publishes only a current-worthy newest value to `CurrentTagCache`.

Risk: this requires a common protocol-independent event ingestion contract so DNP3 does not bypass Historian.

### Option C — driver-side coalescing with common publication

Within a DNP3 response/recovery window, retain/publish historical events in order through a future common API while selecting the newest trustworthy value as the current TAG update.

Risk: still needs a common way to preserve historical samples separately from current cache changes.

**Research recommendation:** do not patch this only inside DNP3. Ask the Coordinator to define a common late/out-of-order source-timestamp policy usable by future event/subscription protocols. Until that is resolved, substantial DNP3 production implementation should not claim correct event-history/current-state semantics.

---

## 13. Commands and write safety

### 13.1 General rule

DNP3 controls are protocol operations with command modes, statuses and sometimes pulse timing. A generic value assignment is insufficient to express the required safety semantics.

The existing `ICommunicationDriver.WriteAsync(tagId, value)` can remain the owning-provider entry point for simple configured output TAGs, but the DNP3 binding must contain enough command profile information to make the resulting operation deterministic.

For richer operator actions, the existing EliteSCADA Operational Command domain is the preferred future product surface rather than overloading a raw scalar write.

### 13.2 Binary control / CROB

A writable binary output binding requires a canonical DNP3 control profile containing at least:

- target index;
- `CommandMode`: Select-Before-Operate or Direct Operate;
- operation code: latch on/off or pulse on/off;
- Trip/Close Code if required by the device/application;
- pulse count;
- on time;
- off time;
- write permission/access intent;
- optional expected feedback point reference.

A Boolean TAG write may be mapped only when the profile explicitly defines how `true` and `false` translate to DNP3 control operations. Example:

- `true -> LATCH_ON`;
- `false -> LATCH_OFF`.

For a breaker Trip/Close profile, using simple Boolean semantics may be misleading. Prefer explicit operational commands such as `Trip` and `Close` with confirmation/Audit policy.

### 13.3 Select-Before-Operate

Default recommendation: `SelectBeforeOperate` for normal control points when supported.

The sequence must complete both protocol phases successfully. A successful SELECT followed by failed OPERATE is a failed command.

### 13.4 Direct Operate

`DirectOperate` may be deliberately selected for devices/profiles that require it.

It must remain explicit Engineering. Do not silently fall back from failed SBO to Direct Operate because doing so changes the safety contract.

### 13.5 Direct Operate No Response

Not supported in the first safety baseline.

Reason: it intentionally removes the normal response/status evidence needed for clear command outcome visibility. It may be considered later only for a documented device requirement and with explicit operational semantics.

### 13.6 Analog output commands

Group 41 command type must match the engineered canonical TAG type:

- G41V1 -> Int32;
- G41V2 -> Int16;
- G41V3 -> Float;
- G41V4 -> Double.

No implicit narrowing, booleanization or string conversion is allowed.

### 13.7 Command result

A DNP3 command is successful only if:

- association is connected/available;
- protocol task completes;
- response is structurally valid;
- response matches the request header/point expectations;
- every targeted point returns an acceptable command status;
- no relevant timeout/shutdown/association-removal error occurred.

Diagnostics/Audit should distinguish:

- select rejected;
- operate rejected;
- bad point command status;
- timeout;
- connection loss;
- malformed/mismatched response;
- queue/backpressure rejection;
- cancellation;
- unsupported command/profile.

### 13.8 Bounded command queue

The association must have a finite user-request queue. A burst of UI/Gateway/script requests must not build an unbounded control backlog that operates field equipment minutes later.

Queue saturation fails requests explicitly and contributes to diagnostics. It never silently drops a command while returning success.

### 13.9 Feedback is independent

A successful command does not mutate a Binary/Analog Output Status TAG optimistically unless the outstation subsequently reports the feedback/status point.

Command status answers “did the device accept/execute this protocol operation?” Feedback TAGs answer “what process/output state is currently reported?”. Those are different facts.

---

## 14. Connection lifecycle and reconnect

### 14.1 States

The DNP3 Data Source should map internal states into the common communication health model, including at least:

- stopped/disabled;
- connecting;
- performing startup integrity/recovery;
- healthy/running;
- degraded;
- reconnecting;
- faulted.

### 14.2 Reconnect behavior

Requirements:

- exponential or bounded configurable backoff;
- cancellation on runtime deactivation/shutdown;
- no zombie reconnect loop after project revision switch;
- no duplicated associations after transient network recovery;
- startup/recovery sequence reruns when required;
- pending user commands fail/cancel explicitly on association loss rather than being replayed blindly after reconnection.

Automatic replay of process commands after reconnect is forbidden unless a future command specifically engineers such behavior.

### 14.3 Read availability during reconnect

Existing current values may remain visible according to normal TAG semantics but must not remain `Good` indefinitely. Point/Data Source stale/bad-communication transition policy should reuse the common runtime quality model.

---

## 15. Diagnostics contract

DNP3 diagnostics extend, but do not replace, `CommunicationDriverDiagnosticSnapshot`.

Common fields should include:

- Data Source key/name;
- Driver type;
- sanitized host/port;
- local/master address;
- remote/outstation address;
- state and state-change time;
- last successful communication;
- last failed communication;
- connect/reconnect/disconnect counts;
- operation/request count;
- read/task count;
- write/command count;
- timeout count;
- consecutive failures;
- last/average request latency where meaningful;
- published TAG update count;
- associated TAG count;
- TAG quality counts;
- sanitized last error.

Useful DNP3-specific sanitized details:

- current association/link state;
- last response IIN bits;
- restart detections;
- event-buffer-overflow detections;
- startup integrity success/failure count;
- Class 0/1/2/3 scan counts;
- unsolicited responses received;
- unsolicited enable/disable failures;
- command success/rejection counts;
- last command-status summary without process-secret data;
- time-sync requested/success/failure count;
- last effective time-sync mode;
- source-time-unsynchronized sample count;
- late/backlog event count once ordering policy exists;
- received object family/variation diagnostics when useful for commissioning;
- effective TLS profile when implemented, without certificate private material.

Protocol frame hex dumps are debugging material, not default protected diagnostics. If ever exposed, they require bounded logging, authorization and secret/sensitive-process review.

---

## 16. Engineering browse/import/reconcile strategy

### 16.1 DNP3 does not provide universal browse

Do not pretend DNP3 has a complete standardized online namespace browser.

A Class 0/integrity response reveals points the outstation chooses to return and their object types/indices, which is useful evidence but may be incomplete relative to all configured controls, classes, disabled points or vendor-specific configuration.

### 16.2 Connection test

`ICommunicationDriverConnectionTester` should be supported.

A protected connection test may:

- open a short-lived TCP/DNP3 association;
- verify link/application communication;
- observe remote address/responding identity evidence available from protocol/device attributes where supported;
- perform a bounded minimal integrity/status operation;
- report IIN, timeout, addressing and compatibility issues;
- report selected stack/protocol profile information.

It must not activate a Runtime revision or create TAGs.

### 16.3 Observed-point browse

`ICommunicationDriverBrowser` may expose a synthetic tree by logical point family:

```text
Binary Inputs
Double-Bit Binary Inputs
Analog Inputs
Counters
Frozen Counters
Binary Output Status
Analog Output Status
```

Children are **observed candidates** from a bounded integrity/read procedure.

Every result page must be marked partial when complete device enumeration cannot be proven.

Candidate `StableIdentity` uses point kind + index. `PortableAddress` uses the deterministic DNP3 adapter representation, not a transient library handle.

### 16.4 File/import workflows

No protocol-standard DNP3 engineering project file is assumed.

Future practical import options may include:

- EliteSCADA-owned CSV/XLSX bulk point maps;
- vendor point-list exports through deliberately supported adapters;
- standardized device-profile artifacts if a future specification/product requirement defines them.

Do not advertise `ICommunicationDriverFileImporter` until an actual deterministic input format is selected.

### 16.5 Reconcile

`ICommunicationDriverReconciler` is useful because an engineered point map can be checked against observed device responses.

Possible results:

- unchanged / observed;
- missing/unobserved in bounded scan;
- type/family mismatch;
- variation mismatch but compatible point identity;
- access/command capability mismatch;
- unsupported point kind;
- ambiguous/incomplete evidence.

“Not observed in one Class 0 response” is not automatically “deleted from device”. Reconcile must distinguish `Missing` from `Unobserved/Partial` evidence where necessary.

---

## 17. Security direction

### 17.1 Plain TCP first does not mean security is ignored

Initial implementation authorization explicitly targets TCP. Production deployment must document that plain DNP3/TCP has no transport confidentiality/authentication by itself.

Network segmentation, VPN/site architecture and plant security controls remain deployment concerns, but EliteSCADA must not claim they are protocol authentication.

### 17.2 TLS

The preferred Step Function candidate supports mutually authenticated TLS, and Triangle also advertises TLS support.

When enabled later:

- certificates/trust anchors are host-owned protected references;
- no private key appears in Engineering JSON/package;
- unknown/changed server identity fails closed according to explicit trust policy;
- there is no permanent “accept any certificate” production mode;
- minimum TLS profile is explicit and versioned;
- diagnostics show sanitized effective security state only.

### 17.3 DNP3 Secure Authentication

Secure Authentication SAv5 is deliberately outside the first implementation cut.

Reason:

- it is a distinct application-layer security feature, not equivalent to TLS;
- library support differs materially across candidates;
- user/key/role management intersects host-owned security and secret-management contracts not yet finalized for Driver Modules.

Triangle publicly advertises DNP3 Secure Authentication support; Step Function 1.6 public feature documentation used for this research does not establish SAv5 as part of the selected first scope. Do not assume support merely because the stack is DNP3-compliant.

### 17.4 Dangerous DNP3 application functions

Normal SCADA operation must not expose arbitrary DNP3 function codes such as device restart, application start/stop, file delete or freeze controls through a raw “send function” API.

Any future administrative operation requires:

- a specific product use case;
- explicit capability;
- authorization;
- confirmation where hazardous;
- Audit;
- device compatibility evidence.

---

## 18. Byte/word ordering contract applicability

The ADR-007 byte/word swap contract applies when a protocol binding exposes raw multi-byte storage whose physical byte/word arrangement must be transformed before canonical typed decoding.

For the normal DNP3 object model in this first driver, the selected DNP3 stack already decodes typed DNP3 objects such as Int16, Int32, float and double according to protocol encoding. Therefore **user-configurable Byte Swap / Word Swap should not be exposed by default for standard DNP3 object groups**.

Adding arbitrary swap settings after the stack decodes a standard DNP3 Analog Input would corrupt protocol semantics and turn a common physical-binding feature into a folklore switch.

If a future vendor-specific DNP3 octet-string/raw-object profile truly contains embedded device-specific byte layouts, that profile may deliberately expose a transformation at the appropriate binding boundary. It must be versioned and validated, not enabled globally because of device brand.

---

## 19. Import/export, revisions and package fidelity

When the canonical Engineering schema is later extended for DNP3, all public configuration must participate in normal lifecycle behavior:

- versioned JSON export/import;
- validation;
- Preview/Apply/CAS;
- Working state;
- immutable revisions;
- PostgreSQL persistence;
- publish/activate lifecycle;
- `.escadapkg` backup/restore;
- future copy/paste/Engineering Fragments;
- migration across driver schema versions.

Must round-trip deterministically:

- Data Source endpoint/addresses/timeouts/reconnect profile;
- class/integrity/unsolicited policy;
- time-sync policy;
- TLS secret/certificate references when implemented;
- point kind/index identity;
- preferred static/event variations;
- access intent;
- command profile;
- validation-relevant point metadata.

Must **not** be persisted as authoritative project truth:

- live DNP3 channel/association IDs;
- library task handles;
- sequence numbers;
- connection state;
- current IIN;
- runtime counters;
- transient observed browse candidates;
- resolved private keys/passwords;
- current TAG values.

---

## 20. Testing strategy

### 20.1 Unit tests without hardware

Required deterministic tests:

- Data Source configuration validation;
- local/remote address validation;
- point-kind/index stable identity;
- static/event variation compatibility;
- sparse point indices;
- Binary Input mapping;
- Double-Bit Binary all four states;
- Int16/Int32/Float/Double analog mapping;
- UInt16/UInt32 counter range preservation;
- frozen-counter mapping;
- flags -> `TagQuality` precedence;
- flagless-variation policy;
- absolute timestamp mapping;
- relative timestamp + CTO handling through adapter evidence;
- unsynchronized timestamp diagnostic/quality behavior;
- command-profile validation;
- Boolean -> CROB profile mapping;
- SBO and Direct Operate selection;
- analog output type validation;
- command-status failure propagation;
- bounded request queue behavior;
- cancellation during reconnect/command;
- diagnostics sanitization;
- Import/Export adapter representation when canonical schema is available.

### 20.2 Adapter isolation tests

Wrap the selected protocol stack behind internal EliteSCADA-owned adapter seams so tests can inject:

- connection state transitions;
- solicited fragments;
- unsolicited fragments;
- static/event headers;
- flags;
- timestamps;
- IIN bits;
- command responses;
- timeouts/cancellation.

Do not make unit tests instantiate library objects everywhere. That would make library replacement and fault injection unnecessarily painful.

### 20.3 Loopback integration tests

For CI, run a real DNP3 master adapter against a deterministic local outstation simulator when licensing permits.

Scenarios:

1. startup integrity Class 1230;
2. enable/disable unsolicited;
3. Binary Input static + event;
4. Double-Bit Binary;
5. analog integer and floating variants;
6. counter/frozen counter;
7. event timestamps;
8. restart IIN/recovery;
9. event-buffer-overflow recovery signal;
10. TCP disconnect/reconnect;
11. command SBO success;
12. command rejection;
13. Direct Operate explicit profile;
14. analog output command;
15. timeout and cancellation;
16. two independent DNP3 Data Sources proving health/quality isolation.

A same-stack loopback is protocol-integration evidence, not final interoperability evidence.

### 20.4 Independent simulator/test-harness validation

Before production:

- test against an independent DNP3 simulator or protocol test harness;
- validate object variations and qualifiers not generated by the same library used by EliteSCADA;
- validate malformed/unsupported responses fail safely;
- validate retransmission/fragmentation/unsolicited behavior;
- exercise command negative statuses;
- exercise clock/time synchronization when enabled;
- capture interoperability matrix by device/profile.

Triangle MicroWorks Test Harness is one commercial candidate for this role. Final tooling is a product/testing procurement decision.

### 20.5 Hardware validation

Minimum production acceptance should include representative hardware from at least two distinct vendor/device families when practical, or one representative RTU/IED family plus an independent conformance-grade simulator.

Validate:

- real network reconnect after cable/switch interruption;
- outstation reboot;
- Class 0/1/2/3 behavior;
- unsolicited enable/disable/reconnect;
- sparse/non-zero point indices;
- event timestamps and clock state;
- event burst/backlog;
- configured analog/counter variations;
- CROB SBO and Direct Operate where required;
- trip/close or latch/pulse semantics on a safe test point;
- analog output command status;
- device command rejection and permissions;
- TLS when promoted;
- CPU/network load at realistic point counts.

No command test should target live process equipment merely to prove protocol code.

### 20.6 Conformance expectations

The selected library’s own conformance evidence is useful but does not certify the assembled EliteSCADA product behavior.

Before a production DNP3 compatibility claim, maintain:

- protocol feature/subset declaration;
- supported object/variation matrix;
- tested device matrix;
- known limitations;
- negative-test evidence;
- independent interoperability results.

---

## 21. Initial diagnostics and acceptance scenarios

Before the first production-capable branch can be considered reviewable, automated evidence should prove at least:

1. TCP Master connects to one configured outstation association;
2. wrong remote address or non-responsive endpoint fails visibly;
3. reconnect is bounded and cancellation-safe;
4. startup disable-unsolicited -> integrity -> enable-unsolicited sequence is deterministic;
5. Class 0 static values populate canonical TAGs;
6. Class 1/2/3 events update matching TAG identities;
7. unsolicited Binary/Analog events update through the normal TAG pipeline;
8. Binary Input quality flags map correctly;
9. Double-Bit Binary preserves four-state semantics;
10. Int16/Int32/Float/Double analogs map without unsafe coercion;
11. UInt32 counters preserve values above `Int32.MaxValue` using canonical `Int64`;
12. timestamped events populate `SourceTimestamp`;
13. no-time variations leave `SourceTimestamp` null;
14. bad communication never becomes a good zero/false value;
15. RESTART/forced/discontinuity/reference-error evidence is visible in quality/diagnostics;
16. multiple sparse indices work;
17. two DNP3 Data Sources fail/recover independently;
18. Gateway reads DNP3 source TAGs only through normal TAG events;
19. a configured writable DNP3 destination is invoked only through its owning driver;
20. CROB SBO completes select + operate and reports per-point status;
21. rejected SELECT/OPERATE is not reported as success;
22. Direct Operate runs only when explicitly configured;
23. Direct Operate No Response is rejected in first-cut configuration;
24. analog output command rejects incompatible canonical types;
25. output feedback is not fabricated from command intent;
26. pending commands do not replay blindly after reconnect;
27. request queue is bounded;
28. detailed diagnostics contain no secret material;
29. Engineering candidates are transient and do not auto-create TAGs;
30. Export/Import/revision/package fidelity is proven after the Coordinator provides the canonical protocol-binding schema integration;
31. late/backlogged event semantics are resolved against the common current-value policy before production activation.

---

## 22. Explicit first-cut limitations

The first authorized DNP3 implementation should state these limitations rather than hide them:

- Master/Client only;
- TCP only initially;
- no serial transport in first cut;
- no DNP3 Outstation/Server role;
- no UDP transport in first cut;
- no Secure Authentication SAv5 in first cut;
- TLS only after host certificate/trust-reference integration is deliberately implemented;
- no generic complete online point browse guarantee;
- no DNP3 file transfer;
- no datasets;
- no raw arbitrary function-code console;
- no cold/warm restart command exposed to normal users;
- no freeze-control surface merely because frozen counters can be read;
- no automatic device writes on reconnect;
- no optimistic output-feedback mutation;
- no protocol-specific bypass of TAG quality, Gateway, authorization, Audit or project lifecycle;
- no production library redistribution until licensing is resolved;
- no claim of all-vendor DNP3 compatibility without hardware/interoperability matrix.

---

## 23. Proposed implementation slices after research approval

Substantial implementation should begin only after this contract receives Coordinator review.

### Slice A — isolated protocol adapter foundation

- add `Scada.Drivers/Dnp3` isolated namespace/folder;
- internal stack abstraction;
- descriptor/configuration schema owned by driver layer only where permitted;
- point-binding model local to adapter pending canonical schema integration;
- type/variation/quality/timestamp mappers;
- no central DI/Program.cs edit.

### Slice B — TCP Master runtime proof

- one association per Data Source plan;
- start/stop/reconnect;
- startup integrity;
- Binary/Double-Bit/Analog/Counter/Frozen Counter reads;
- diagnostics;
- focused tests with simulator.

### Slice C — class/unsolicited event processing

- Class 1/2/3 polling;
- unsolicited responses;
- restart/buffer-overflow handling;
- timestamped event tests;
- resolve common late-event/current-cache integration decision.

### Slice D — command adapter

- CROB;
- SBO/Direct Operate;
- typed G41 analog outputs;
- bounded request queue;
- command result diagnostics;
- write-path tests.

### Slice E — Engineering capabilities

- protected connection test;
- bounded observed-point browse;
- reconcile;
- driver-specific validation;
- candidate tests.

### Coordinator integration required later

The Driver DEV should **not** directly take ownership of:

- canonical Engineering schema migration;
- `Program.cs` / central DI;
- public routing/API composition;
- central frontend editor pages;
- project-wide Driver Module loader;
- common current-cache timestamp ordering policy;
- official roadmap/assignment/continuity docs.

Those are Coordinator/shared-contract decisions.

---

## 24. Shared contract decisions requiring Coordinator reconciliation

### 24.1 Late/out-of-order event ingestion

**Required before production.** DNP3 exposes a gap in the current common event/current-cache model. The platform needs a deliberate rule for historical events arriving after newer current values.

### 24.2 Protocol-rich canonical TAG binding

The current common research already recognizes that richer protocols need a versioned protocol-owned binding representation beyond a single generic address string. DNP3 needs structured fields for point kind, index, variation preferences and command profile.

This must be introduced through the Coordinator-owned Engineering schema/migration path, not private metadata.

### 24.3 Command abstraction

Simple configured scalar `WriteAsync` can cover some latch/analog outputs, but breaker Trip/Close, pulse controls and command-result semantics fit better in the canonical Operational Command model. Coordinator should decide the integration seam before exposing operator UI.

### 24.4 Raw protocol flag diagnostics

Common `TagQuality` remains authoritative, but commissioning benefits from seeing DNP3 raw flags. A shared extensible sanitized per-sample/per-point diagnostic-detail mechanism may be preferable to DNP3-only fields.

### 24.5 Unsynchronized source timestamps

Need one cross-protocol policy for whether an unsynchronized device timestamp:

- remains `SourceTimestamp` with `Uncertain` quality;
- remains source time with separate timestamp-quality metadata;
- is excluded from historian event-time ordering.

DNP3 should not invent this policy alone.

### 24.6 Library licensing

Production stack selection must be an explicit product/commercial decision. The technical research prefers Step Function for laboratory/API fit, but its public license is insufficient for commercial production/redistribution.

---

## 25. Research evidence and references checked

Primary EliteSCADA references:

- `PROJECT GOAL.md`
- `docs/ADR-007-DRIVER-MODULES-AND-PROTOCOLS.md`
- `docs/ADR-009-DRIVER-SDK-ENGINEERING-BOUNDARIES.md`
- `docs/DRIVER-SDK-RESEARCH-CONVERGENCE.md`
- `docs/COMMUNICATION-DRIVER-DIAGNOSTICS.md`
- `docs/TAG-GATEWAY.md`
- `docs/TAG-BIT-ACCESS-AND-BIT-BINDING.md`
- `src/Scada.Drivers/Abstractions/ICommunicationDriver.cs`
- `src/Scada.Drivers/Abstractions/DriverEngineeringContracts.cs`
- `src/Scada.Core/Tags/TagQuality.cs`
- `src/Scada.Core/Tags/TagDataType.cs`
- `src/Scada.Core/Tags/TagValue.cs`
- `src/Scada.Core/Tags/CurrentTagCache.cs`

External evidence checked on 2026-08-29:

- Step Function I/O DNP3 1.6.0 guide/API: `https://docs.stepfunc.io/dnp3/1.6.0/guide/`
- Step Function 1.6.0 Rust/API reference: `https://docs.stepfunc.io/dnp3/1.6.0/rust/dnp3/`
- Step Function .NET 1.6.0 API/license notice: `https://docs.stepfunc.io/dnp3/1.6.0/dotnet/`
- Step Function non-commercial/commercial-license information through the library documentation/source license.
- OpenDNP3 archived project: `https://github.com/dnp3/opendnp3`
- Triangle MicroWorks DNP3 Source Code Library: `https://trianglemicroworks.com/products/source-code-libraries/dnp-scl-pages/what%27s-new`
- Triangle MicroWorks .NET components and Master feature pages.

The Step Function guide explicitly notes that its documentation is not a replacement for IEEE 1815 and recommends obtaining the DNP3 standard for product development. EliteSCADA should follow that advice before production protocol implementation is finalized.

---

## 26. Research conclusion / gate

The DNP3 driver is technically feasible within the existing EliteSCADA Driver SDK without creating a second runtime architecture.

The current common contracts already provide the correct high-level seams for:

- Driver lifecycle;
- Hybrid acquisition;
- TAG publication;
- source timestamps;
- common quality;
- diagnostics;
- protected Engineering connection/browse/reconcile;
- Gateway writes through the owning driver.

The first implementation should proceed only after Coordinator review of this contract, with these gates explicitly tracked:

1. **library/commercial license decision remains open for production**;
2. **late/backlogged event vs current-cache ordering requires a shared contract decision**;
3. **rich DNP3 canonical binding requires Coordinator-owned Engineering schema integration**;
4. **command UI/domain must preserve CROB/SBO/Direct Operate semantics rather than flattening DNP3 into generic writes**;
5. **production acceptance requires independent simulator/test-harness and field-device evidence**.

Research status after this document:

`RESEARCH CONTRACT COMPLETE ON DRIVER BRANCH / PRODUCTION DNP3 NOT IMPLEMENTED / READY FOR COORDINATOR EARLY CONTRACT REVIEW`
