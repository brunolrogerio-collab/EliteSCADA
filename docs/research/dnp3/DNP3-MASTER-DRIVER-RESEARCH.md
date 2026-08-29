# DNP3 Master / Client driver research — EliteSCADA

Status: **RESEARCH IN DRIVER BRANCH / PRODUCTION NOT IMPLEMENTED**

Research date: 2026-08-29

Branch: `driver7/dnp3`

Authorization baseline: `149a28c4bcc1e545ac2e43f7e7db40b9864724eb`

This document is the required research-first deliverable for **DEV Driver 7 — DNP3**. It defines the proposed first EliteSCADA DNP3 Master/Client boundary before substantial runtime implementation begins.

It does **not** add a production DNP3 Data Source, protocol dependency, runtime registration, Engineering schema migration, DriverHost composition, API endpoint or frontend behavior. This workstream remains parked from the mainline WAVEs until the Coordinator explicitly authorizes integration.

The design preserves the existing EliteSCADA authority flow:

`Canonical Engineering -> DriverHost/compiler -> Data Source/driver -> TAG Current Cache -> Event Bus -> Historian / Alarms / Realtime / Gateway`

DNP3 is one more communication provider behind that boundary. It does not create a DNP3-specific TAG database, quality model, historian path, Gateway implementation or browser-side source of project truth.

---

## 1. Executive recommendation

1. **Implement EliteSCADA as a DNP3 Master/Client first.** Initial transport is TCP. Serial remains architecturally possible but is outside the first implementation cut unless separately promoted.
2. **Represent one target outstation association as one EliteSCADA Data Source initially.** Even if the selected stack can multiplex multiple associations over one lower-level channel, Data Source ownership, health, counters, point quality and writes remain isolated per configured outstation.
3. **Use `DriverAcquisitionMode.Hybrid`.** Startup integrity, periodic integrity/event scans where configured, automatic event scans and unsolicited responses all feed the normal TAG pipeline.
4. **Use the normal startup handshake:** disable configured unsolicited Classes 1/2/3, perform startup integrity for Classes 1/2/3/0, then enable configured unsolicited classes. Re-run recovery behavior after outstation restart as required.
5. **Make point identity independent of response variation.** Persist logical point kind + DNP3 index under the Data Source. Group/variation remain representation/acquisition configuration because the same point uses different static and event groups.
6. **Map DNP3 flags into EliteSCADA `TagQuality` while retaining the raw flag evidence for diagnostics.** Do not create a parallel DNP3 quality model.
7. **Use `TagValue.SourceTimestamp` only when DNP3 actually supplies a usable measurement/event time.** `TagValue.Timestamp` remains local EliteSCADA publication time. Never fabricate source time from receipt time.
8. **Resolve late/backlogged event ordering before production.** The current `CurrentTagCache` is arrival-order last-write-wins; DNP3 can deliver older event backlog after a newer current/static value. Historical events must not make the HMI current value travel backward in time.
9. **First read scope:** Binary Input, Double-Bit Binary Input, Analog Input, Counter, Frozen Counter, Binary Output Status and Analog Output Status.
10. **First command scope:** CROB (`G12V1`) and typed Analog Output (`G41V1..V4`) where explicitly engineered.
11. **Default control mode to Select-Before-Operate where supported.** Direct Operate is explicit configuration. Direct Operate No Response is excluded from the first safety baseline because it removes normal command-result evidence.
12. **A command succeeds only when protocol task and per-point command status succeed.** Queueing or sending bytes is not success.
13. **Do not fake an OPC-UA-like DNP3 browse tree.** Bounded integrity/class reads can produce transient observed candidates, clearly marked partial, but they do not prove a complete device point database.
14. **Preferred laboratory stack: Step Function I/O DNP3 1.6.x.** It is current, asynchronous, supports Master/Outstation, TCP/TLS/serial, .NET bindings, unsolicited responses, commands and the required object families. Its public license is explicitly non-commercial/non-production, so commercial production/redistribution requires a negotiated license.
15. **Primary commercial comparison: Triangle MicroWorks DNP3 Source Code Library.** Current public information shows v3.34 (July 2026), .NET components, Master features, TLS/Secure Authentication options and long-term conformance tooling/support. Procurement and exact .NET 10 fit remain to be verified.
16. **OpenDNP3 is not the production default.** It is Apache 2.0 and historically mature, but it was archived/EOL on 2022-09-01. Using it in production means EliteSCADA owns a maintenance/security fork.
17. **Do not implement IEEE 1815 from scratch for the first production driver.** The protocol stack is too broad to casually reimplement as a side quest.
18. **Obtain the applicable IEEE 1815/DNP3 standard before production implementation is finalized.** Library documentation is evidence, not the normative standard.
19. **Defer serial and DNP3 Secure Authentication SAv5 from the first cut.** TLS may be added when host-owned certificate/trust references are deliberately integrated.
20. **Require independent interoperability and representative hardware evidence before production compatibility claims.** Same-stack loopback is useful, not sufficient.

---

## 2. Existing EliteSCADA contracts that govern DNP3

### 2.1 Runtime

`ICommunicationDriver` already provides the required active-runtime seam:

- `StartAsync` / `StopAsync`;
- `ReadAsync(tagId)`;
- `WriteAsync(tagId, value)`;
- Driver identity/capabilities/status/TAG collection;
- asynchronous disposal.

DNP3 channel, association, task, sequence, qualifier and object-header types remain internal adapter details. They must not leak into `Scada.Core` or canonical Engineering.

### 2.2 Engineering

The Driver SDK already separates active Runtime from protected Engineering capabilities and exposes:

- `CommunicationDriverTypeDescriptor`;
- versioned `DriverConfigurationSchemaDescriptor`;
- `ConnectionTest`;
- optional `Discover` / `Browse` / `FileImport` / `Reconcile`;
- acquisition modes including `Hybrid`.

DNP3 should initially target `ConnectionTest`, bounded observed-point `Browse` and `Reconcile`. Do not advertise discovery or file import merely for UI symmetry.

### 2.3 TAG values

Core already has the required common semantics:

- `TagQuality`: Good, Uncertain, Bad, BadCommunication, BadConfiguration, BadDevice, Stale, Disabled;
- `TagDataType`: Boolean, Int16, Int32, Int64, Float, Double, String, DateTime, Enum;
- local `TagValue.Timestamp`;
- optional `SourceTimestamp`;
- optional `ServerTimestamp`.

DNP3 maps into these types. It does not add an alternate `DnpTagValue`, `DnpQuality` or current cache.

### 2.4 Gateway

DNP3 participates through normal TAG ownership only:

`DNP3 TAG -> normal TAG event -> Gateway -> destination TAG owner`

and

`source TAG -> Gateway -> DNP3 TAG owning driver -> DNP3 command/write adapter`

No pairwise `DNP3-to-Modbus`/`DNP3-to-OPC` API is allowed.

### 2.5 TAG bits

Native DNP3 Binary Input / Binary Output Status points are already Boolean. They do not need an artificial word-bit selector. Any future integer TAG still reuses the existing canonical `TagId + selector` bit semantics rather than inventing DNP3-specific bit notation.

---

## 3. Library / implementation strategy and licensing

### 3.1 Step Function I/O DNP3 1.6.x — preferred lab candidate

Evidence checked on 2026-08-29:

- current stable documentation line 1.6.0;
- Rust core with C, C++, .NET and Java bindings;
- Master and Outstation components;
- TCP, mutually authenticated TLS and serial;
- asynchronous implementation;
- Windows x64 officially supported;
- Master association configuration includes startup integrity, unsolicited policy, event scans, retry, keepalive and time synchronization;
- API includes the object/variation and command families required below.

The .NET binding fits EliteSCADA technically without exposing Rust/library types in public contracts.

**License gate:** Step Function states that the public library is non-commercial/non-production and that commercial use requires a purchased commercial license. Therefore:

- it may be evaluated in research/lab use subject to those terms;
- redistribution rights must not be assumed;
- a production dependency is locked only after commercial terms, redistribution, support and packaging obligations are accepted;
- no permanent distributable package/binary should be committed merely because the API is convenient.

Status:

`PREFERRED TECHNICAL CANDIDATE / COMMERCIAL-LICENSE GATE OPEN`

### 3.2 Triangle MicroWorks — commercial/vendor-supported candidate

Current public information advertises:

- DNP3 Master/Outstation source-code libraries;
- .NET component;
- current release v3.34, July 2026;
- TLS and DNP3 Secure Authentication options;
- protocol/conformance tooling and long-term support.

Before selecting it, verify:

- price and redistribution terms;
- .NET 10 compatibility, not merely published .NET 6/8 support;
- Windows x64 native deployment footprint;
- async/cancellation/reconnect behavior;
- future Driver Module packaging fit;
- per-Data-Source isolation;
- headless CI testability;
- security/advisory/update process.

Status:

`COMMERCIAL PRODUCTION CANDIDATE / PROCUREMENT + API FIT TO VERIFY`

### 3.3 OpenDNP3 3.1.2 — legacy fallback only

OpenDNP3 is Apache 2.0 with historical C++/.NET/Java support, but its repository was archived on 2022-09-01 and explicitly reached end-of-life.

It remains useful reference material. Selecting it for production transfers stack maintenance, CVE triage, toolchain modernization and future DNP3 evolution to EliteSCADA.

Status:

`REJECT AS DEFAULT PRODUCTION STACK / ALLOW ONLY BY EXPLICIT MAINTENANCE-FORK DECISION`

### 3.4 From-scratch implementation

Rejected for the initial driver. The DNP3 worker should own EliteSCADA integration, mappings, Engineering identity and diagnostics, not casually become the maintainer of a new IEEE 1815 stack.

### 3.5 Production stack gate

Before code locks a production stack, re-check:

1. stable version/support lifecycle;
2. licensing/redistribution;
3. Windows x64 and .NET 10;
4. security advisories;
5. Master TCP/reconnect/cancellation;
6. unsolicited behavior;
7. object/variation coverage;
8. CROB/analog command support;
9. time synchronization;
10. TLS and future Secure Authentication path;
11. logging sanitization;
12. independent interoperability evidence;
13. future serial capability without changing public point identity.

No library-specific handle becomes canonical Engineering regardless of winner.

---

## 4. Initial product boundary

### 4.1 Role

EliteSCADA is a **DNP3 Master/Client** connecting to configured DNP3 outstations.

### 4.2 Transport

First transport:

- TCP client;
- host/IP;
- port (product UI may default conventional DNP3/TCP 20000);
- local/master DNP3 link address;
- remote/outstation DNP3 link address;
- bounded connect/response timeouts;
- deterministic reconnect/backoff;
- optional link-status keepalive if supported/configured.

Deferred:

- serial;
- UDP;
- Master TCP listener mode;
- DNP3 Outstation/Server role.

The public configuration should carry a transport discriminator so later serial does not alter point identity.

### 4.3 One Data Source per outstation

Initial rule:

**One EliteSCADA DNP3 Data Source represents one configured DNP3 outstation association.**

This gives each target:

- stable Data Source identity;
- remote address;
- health/reconnect state;
- counters;
- point set;
- write/command ownership;
- TAG authorization context;
- deterministic activation/disposal.

A later implementation may share lower-level transport between associations, but that is an internal optimization. Logical Data Source state and TAG quality stay isolated.

For the first descriptor, keep `SupportsSharedTransportInfrastructure = false` unless a safe shared-channel design is actually implemented.

### 4.4 Driver type

Recommended initial key:

`dnp3.master.tcp`

A later module model may consolidate this into `dnp3.master` plus transport configuration, but point identity must not depend on TCP versus serial.

---

## 5. Proposed Data Source Engineering profile

These are semantic requirements, not a canonical schema migration.

| Field | Kind | Initial policy |
| --- | --- | --- |
| `transport` | Enum | `tcp`; reserve `serial` |
| `host` | Host | required for TCP |
| `port` | Port | default 20000 |
| `masterAddress` | Integer | required DNP3 link address |
| `outstationAddress` | Integer | required DNP3 link address |
| `connectTimeout` | Duration | bounded |
| `responseTimeout` | Duration | bounded |
| `reconnectMinDelay` | Duration | bounded |
| `reconnectMaxDelay` | Duration | >= min, bounded |
| `keepAliveTimeout` | Duration/optional | explicit |
| `startupIntegrityClasses` | class set | default 0/1/2/3 |
| `disableUnsolicitedClassesOnStartup` | class set | default 1/2/3 |
| `enableUnsolicitedClassesAfterIntegrity` | class set | default 1/2/3 |
| `eventScanOnEventsAvailable` | class set | configurable 1/2/3 |
| `integrityPollInterval` | Duration/optional | bounded periodic resync |
| `class1PollInterval` | Duration/optional | fallback |
| `class2PollInterval` | Duration/optional | fallback |
| `class3PollInterval` | Duration/optional | fallback |
| `integrityOnEventBufferOverflow` | Boolean | recommended after verification |
| `timeSyncMode` | Enum | `none`, future `lan`, `nonLan` |
| `timeSyncPolicy` | Enum | disabled by default |
| `maxQueuedUserRequests` | Integer | bounded |
| `tlsMode` | Enum | `none` first; future TLS profile |
| `clientCertificateRef` | CertificateReference | TLS only |
| `serverTrustRef` | CertificateReference | TLS only |
| `expectedDeviceIdentity` | Identifier/optional | future reconcile/trust evidence |

Validation must include:

- master/outstation ordinary unicast addresses are explicit and valid;
- local/remote addresses do not silently collapse into special/broadcast/self addresses;
- durations are bounded;
- reconnect max >= min;
- polling intervals have safe lower bounds;
- unsolicited sets contain only Classes 1/2/3;
- Class 0 remains integrity/static, not an unsolicited event class;
- TLS fields are not silently accepted while TLS is unavailable;
- secrets/certificates are references only.

---

## 6. Stable point identity and binding

### 6.1 Group/variation is representation, not sole identity

The same logical point family uses different groups for static and event data:

- Binary Input: G1 static, G2 event;
- Double-Bit Binary: G3 static, G4 event;
- Counter: G20 static, G22 event;
- Frozen Counter: G21 static, G23 event;
- Analog Input: G30 static, G32 event;
- Binary Output Status: G10 static, G11 event;
- Analog Output Status: G40 static, G42 event.

Variations also change width, flags and timestamp presence. Therefore a point persisted only as `G2V2 index 7` is unnecessarily unstable.

### 6.2 Stable identity

Within one canonical Data Source:

```text
pointKind + index
```

Full project physical identity is conceptually:

```text
DataSourceId / outstationAddress / pointKind / index
```

First-release `pointKind`:

- `binaryInput`;
- `doubleBitBinaryInput`;
- `analogInput`;
- `counter`;
- `frozenCounter`;
- `binaryOutputStatus`;
- `analogOutputStatus`.

Command capability attaches explicitly to output/control configuration.

### 6.3 Persisted representation/configuration

Binding should preserve:

- point kind;
- index;
- expected/preferred static group;
- optional static variation or `deviceDefault/any`;
- expected event group;
- optional event variation or `deviceDefault/any`;
- expected event class when known, as validation/diagnostic evidence;
- access intent;
- command profile for writable outputs.

### 6.4 Portable address

Until the Coordinator introduces the richer canonical protocol-binding DTO, any `PortableAddress` emitted by Engineering candidates must be deterministic. Illustrative adapter-only strings:

```text
dnp3:binaryInput:42
dnp3:analogInput:7
```

A future structured binding is preferable. Localized labels such as `Breaker Open` are never authoritative identity.

### 6.5 Sparse indices

Point indices need not be contiguous or start at zero. Engineering, browse candidates and runtime maps must naturally support sparse index sets.

---

## 7. First-release object groups and variations

`Variation 0` requests may be used to let the outstation choose a default, but the received concrete variation controls decoding, flags and timestamp availability.

### 7.1 Binary Input

Static:

- G1V1 packed;
- G1V2 with flags.

Events:

- G2V1 without time;
- G2V2 absolute time;
- G2V3 relative time.

EliteSCADA type: `Boolean`.

### 7.2 Double-Bit Binary Input

Static:

- G3V1 packed;
- G3V2 with flags.

Events:

- G4V1 without time;
- G4V2 absolute time;
- G4V3 relative time.

EliteSCADA type: **`Enum`**, preserving:

- `Intermediate`;
- `DeterminedOff`;
- `DeterminedOn`;
- `Indeterminate`.

Do not silently coerce intermediate/indeterminate to false.

### 7.3 Analog Input

Static G30:

- V1 Int32 + flags;
- V2 Int16 + flags;
- V3 Int32 no flags;
- V4 Int16 no flags;
- V5 Float32 + flags;
- V6 Float64 + flags.

Events G32:

- V1/V2 Int32/Int16 without time;
- V3/V4 Int32/Int16 with time;
- V5/V6 Float32/Float64 without time;
- V7/V8 Float32/Float64 with time.

Map to `Int32`, `Int16`, `Float`, `Double` with type validation.

### 7.4 Counter

Static G20:

- V1 UInt32 + flags;
- V2 UInt16 + flags;
- V5 UInt32 no flags;
- V6 UInt16 no flags.

Events G22:

- V1 UInt32 + flags;
- V2 UInt16 + flags;
- V5 UInt32 + flags/time;
- V6 UInt16 + flags/time.

Canonical mapping:

- UInt16 -> `Int32`;
- UInt32 -> `Int64`.

This preserves the full unsigned range without negative reinterpretation.

### 7.5 Frozen Counter

Static G21:

- V1/V2 UInt32/UInt16 + flags;
- V5/V6 UInt32/UInt16 + flags/time;
- V9/V10 UInt32/UInt16 no flags.

Events G23:

- V1/V2 UInt32/UInt16 + flags;
- V5/V6 UInt32/UInt16 + flags/time.

Use the same Int32/Int64 canonical mapping as Counter.

Reading frozen counters does not authorize freeze/clear operations.

### 7.6 Binary Output Status

Static G10 V1/V2; events G11 V1/V2.

Canonical type: `Boolean`.

Output Status is feedback, not command intent. Do not fabricate it after a successful CROB.

### 7.7 Binary Output command

G12V1 CROB.

Command profile fields:

- mode: `selectBeforeOperate` / `directOperate`;
- operation: `latchOn`, `latchOff`, `pulseOn`, `pulseOff` as supported;
- trip/close code: `nul`, `trip`, `close` where meaningful;
- count;
- on-time;
- off-time;
- access/write permission;
- optional expected feedback TAG reference outside protocol status.

`directOperateNoResponse` is excluded first-cut.

### 7.8 Analog Output Status

G40 V1..V4 for Int32, Int16, Float32, Float64 with flags. G42 status events should be supported for corresponding event representations available in the selected stack.

### 7.9 Analog Output commands

G41:

- V1 Int32;
- V2 Int16;
- V3 Float32;
- V4 Float64.

Command variation and canonical TAG type must be compatible. No implicit narrowing.

### 7.10 Time/classes/protocol infrastructure

Required infrastructure:

- G50V1 absolute time as required by time sync;
- G51V1/V2 Common Time of Occurrence;
- G52V1/V2 delay measurement;
- G60V1 Class 0;
- G60V2 Class 1;
- G60V3 Class 2;
- G60V4 Class 3;
- G80V1 IIN as protocol/diagnostic evidence, not ordinary user TAGs.

### 7.11 Deferred object/function families

Initially deferred unless promoted:

- Device Attributes G0;
- Frozen Analog G31/G33;
- Analog deadband configuration G34;
- command-event groups G13/G43 as user process TAGs;
- file transfer G70;
- octet strings G110/G111;
- datasets;
- G102 unsigned integer;
- restart/application-control functions;
- counter freeze-control operations.

Library support does not automatically make a feature part of EliteSCADA product scope.

---

## 8. Classes, integrity and unsolicited behavior

### 8.1 Hybrid acquisition

Values may arrive from:

- startup integrity;
- periodic Class 0 scans;
- periodic Class 1/2/3 scans;
- automatic scans triggered by event-available IIN;
- unsolicited responses;
- bounded ad-hoc reads.

All accepted values enter the normal TAG/event path.

### 8.2 Startup sequence

Default:

1. connect TCP/association;
2. disable configured unsolicited Class 1/2/3;
3. startup integrity Class 1/2/3/0;
4. process static/event values;
5. reconcile restart state as required;
6. enable configured unsolicited Class 1/2/3;
7. start bounded periodic/fallback tasks.

### 8.3 Integrity polling

Periodic Class 0 integrity is an optional bounded resynchronization mechanism. It must be configurable; pathological sub-second integrity scans should fail validation rather than turning DNP3 into an enthusiastic Modbus imitation.

### 8.4 Event class polls

When unsolicited is disabled/unavailable/unreliable, configurable Class 1/2/3 polls provide fallback.

Do not hard-code meanings such as “Class 1 = alarm”. DNP3 class is acquisition/event-priority evidence; the EliteSCADA Alarm Engine remains separate.

### 8.5 Unsolicited

Requirements:

- accept only configured classes;
- process through the same point maps as solicited reads;
- let the protocol stack own fragment/sequence/confirmation rules;
- bad point flags map to bad/degraded quality rather than silently dropping values;
- count unsolicited traffic in protocol diagnostics;
- failed unsolicited enablement is visible and only falls back according to explicit Engineering policy.

### 8.6 Restart and event-buffer overflow

On outstation restart:

- association becomes degraded/recovering;
- configured startup recovery runs;
- cached values do not remain indefinitely Good merely because TCP stayed open.

On event-buffer overflow:

- support/configure an automatic integrity scan;
- diagnostics must state that event continuity was lost because static integrity cannot reconstruct lost historical events.

---

## 9. Measurement mapping

For every received measurement:

1. resolve TAG by Data Source + point kind + index;
2. validate object family/type compatibility;
3. decode without unsafe coercion;
4. map flags to `TagQuality`;
5. preserve sanitized raw flag evidence;
6. set local `Timestamp` on EliteSCADA publication;
7. set `SourceTimestamp` only for valid DNP3 source/event time;
8. leave `ServerTimestamp` null in the initial DNP3 model.

### 9.1 Canonical type table

| DNP3 family | EliteSCADA type |
| --- | --- |
| Binary Input | Boolean |
| Double-Bit Binary Input | Enum |
| Analog Int16 | Int16 |
| Analog Int32 | Int32 |
| Analog Float32 | Float |
| Analog Float64 | Double |
| Counter UInt16 | Int32 |
| Counter UInt32 | Int64 |
| Frozen Counter UInt16 | Int32 |
| Frozen Counter UInt32 | Int64 |
| Binary Output Status | Boolean |
| Analog Output Status | Int16/Int32/Float/Double |

### 9.2 Flagless variations

When a valid variation carries no point flags:

- protocol success proves the object decoded;
- quality may be Good if the variation is intentionally flagless and no contrary association/device evidence exists;
- diagnostics must record that per-point flags were absent;
- do not synthesize a fake DNP3 ONLINE flag.

If flag fidelity is required, Engineering should request/prefer a compatible flags-carrying variation when the outstation supports it.

---

## 10. DNP3 flags -> EliteSCADA quality

DNP3 flags remain protocol evidence. Common `TagQuality` remains product authority.

Recommended initial mapping/precedence:

| DNP3 evidence | EliteSCADA quality | Note |
| --- | --- | --- |
| timeout/no response/session loss | `BadCommunication` | no trustworthy live sample |
| `COMM_LOST` | `BadCommunication` | last value before comm failure |
| ONLINE clear | `BadDevice` | point reports not nominal/valid |
| `REFERENCE_ERR` | `BadDevice` | accuracy/reference problem |
| `OVER_RANGE` | `Uncertain` or `BadDevice` by final policy | preserve raw flag |
| `RESTART` | `Uncertain` | not refreshed since restart |
| `REMOTE_FORCED` | `Uncertain` | downstream override |
| `LOCAL_FORCED` | `Uncertain` | local override |
| `CHATTER_FILTER` | `Uncertain` | abnormal rapid change/filter evidence |
| counter `DISCONTINUITY` | `Uncertain` | delta continuity broken |
| ONLINE and no adverse flags | `Good` | nominal |

Worst applicable state wins when multiple flags are present.

`OVER_RANGE` needs Coordinator/common-quality review because a clipped but usable value may be better represented as Uncertain than BadDevice. Raw flags must remain visible either way.

Point-level bad flags do not necessarily fault the whole Data Source, and a connected TCP session does not make every point Good.

---

## 11. Time and timestamps

### 11.1 Mapping

- `TagValue.Timestamp` = local receipt/publication;
- `SourceTimestamp` = DNP3 point/event time when present/resolvable;
- `ServerTimestamp` = null initially.

### 11.2 Absolute time

Timestamped absolute event variations map to `SourceTimestamp` after validated conversion.

### 11.3 Relative time + CTO

Relative event timestamps require G51 CTO context. The selected stack should resolve this according to the standard. Missing/invalid context never falls back to fabricated local source time.

### 11.4 Unsynchronized time

When the DNP3 stack indicates an unsynchronized timestamp:

- preserve the timestamp as source evidence if syntactically usable;
- downgrade overall quality to at least `Uncertain` or expose a common timestamp-quality diagnostic, according to the final platform decision;
- never present it as trusted synchronized event time without indication.

Historian UX needs a common policy for this, not a DNP3-only trick.

### 11.5 Time synchronization

Default: disabled unless explicitly engineered.

Future explicit modes:

- LAN sync;
- non-LAN delay-measurement sync.

If automated on NEED_TIME IIN:

- it is deliberate Data Source Engineering, not an invisible stack default;
- failures/clock rollback/excessive delay are diagnosable;
- it is not exposed as a generic unaudited clock-write API to UI/scripts.

---

## 12. Late/backlogged events and current-value correctness

This is the main cross-domain issue discovered in this research.

Current `CurrentTagCache.UpdateAsync` unconditionally replaces current value by arrival order. DNP3 can deliver event backlog after reconnect whose `SourceTimestamp` is older than a newer static/current value.

Unsafe example:

```text
12:00:10 static/current = ON
12:00:11 backlog event arrives, SourceTimestamp = 11:59:55, value = OFF
current cache becomes OFF solely because the historical event arrived later
```

The historical event is legitimate historian evidence, but it must not corrupt current process state.

Production integration requires a common decision, for example:

### Option A — source-time-aware current cache

All samples enter the event/historian path, while current cache updates only for a newer trustworthy source time.

Risk: not all protocols/timestamps are trustworthy.

### Option B — separate current and historical-event ingestion

Driver emits historical events through a common protocol-independent event-sample path while current-worthy samples update CurrentTagCache.

Risk: requires a new common seam so DNP3 does not bypass Historian.

### Option C — adapter coalescing plus common historical publication

Within recovery/response batches, preserve historical events while selecting the newest trustworthy sample as current.

Risk: still needs a common historical-sample publication seam.

**Recommendation:** do not patch this only inside DNP3. Coordinator should define a cross-protocol late/out-of-order source-time policy that future event/subscription drivers can reuse.

---

## 13. Commands and write safety

### 13.1 General rule

DNP3 controls contain mode, status and sometimes pulse timing. An unqualified generic set-value abstraction is insufficient.

`ICommunicationDriver.WriteAsync` may remain the owning-provider entry point for simple configured output TAGs, but the DNP3 binding must contain a deterministic command profile. Rich operator controls should integrate with the canonical Operational Command domain rather than hiding semantics in a scalar write.

### 13.2 CROB

Writable binary control profile requires:

- target index;
- SBO or Direct Operate;
- latch/pulse operation;
- Trip/Close Code when applicable;
- count;
- on/off times;
- write permission/access intent;
- optional feedback TAG/reference.

A Boolean write can map only when Engineering explicitly defines `true`/`false` operations, e.g. true -> LATCH_ON and false -> LATCH_OFF.

Breaker Trip/Close is semantically richer and should normally use explicit Operational Commands instead of pretending it is a checkbox.

### 13.3 SBO

Default recommendation. Success requires SELECT and OPERATE phases to complete successfully. Successful SELECT plus failed OPERATE is failure.

### 13.4 Direct Operate

Allowed only when explicitly configured. Never silently fall back from failed SBO to Direct Operate.

### 13.5 Direct Operate No Response

Not supported first-cut because it removes normal command-result visibility.

### 13.6 Analog output

G41 mapping:

- V1 -> Int32;
- V2 -> Int16;
- V3 -> Float;
- V4 -> Double.

No implicit narrowing/string/boolean conversion.

### 13.7 Command result

Success requires:

- live association;
- completed protocol task;
- structurally valid/matching response;
- acceptable per-point command status;
- no timeout/shutdown/association-removal error.

Diagnostics/Audit should distinguish:

- select rejection;
- operate rejection;
- bad command status;
- timeout;
- connection loss;
- response mismatch/malformed response;
- queue/backpressure rejection;
- cancellation;
- unsupported profile.

### 13.8 Bounded queue

User/command requests must have finite queue/backpressure. Bursts must not become commands that operate equipment minutes later. Saturation fails explicitly.

### 13.9 Feedback is independent

Successful command does not optimistically mutate Binary/Analog Output Status. Feedback changes only when the outstation reports feedback/status.

---

## 14. Lifecycle and reconnect

### 14.1 State mapping

Common states should express:

- stopped/disabled;
- connecting;
- startup integrity/recovery;
- healthy/running;
- degraded;
- reconnecting;
- faulted.

### 14.2 Reconnect requirements

- bounded exponential/configurable backoff;
- cancellation on runtime deactivation;
- no zombie loops after revision switch;
- no duplicate associations;
- recovery handshake reruns as required;
- pending user commands fail/cancel on association loss and are **not** blindly replayed after reconnect.

Automatic replay of process commands is forbidden unless a future command explicitly engineers it.

### 14.3 Cached reads while disconnected

Existing values may remain visible according to normal TAG semantics, but they must not remain Good indefinitely. Reuse common Stale/BadCommunication policy.

---

## 15. Diagnostics

DNP3 extends `CommunicationDriverDiagnosticSnapshot`, not replaces it.

Common fields:

- Data Source identity/name;
- driver type;
- sanitized host/port;
- local/master and remote/outstation addresses;
- state/state-change time;
- last success/failure;
- connect/reconnect/disconnect counts;
- request/read/write/command counts;
- timeouts/consecutive failures;
- latency where meaningful;
- published TAG updates;
- associated TAG count;
- quality summary;
- sanitized last error.

Useful DNP3-specific details:

- association/link state;
- last IIN bits;
- restart detections;
- event-buffer-overflow detections;
- integrity success/failure counts;
- Class 0/1/2/3 scan counts;
- unsolicited responses received;
- unsolicited enable/disable failures;
- command success/rejection counts;
- sanitized last command-status summary;
- time-sync attempt/success/failure;
- unsynchronized source-time sample count;
- late/backlog event count after ordering policy exists;
- observed object family/variation during commissioning;
- effective TLS profile when implemented.

Raw frame hex dumps are debugging material, not default diagnostics. Any future exposure requires authorization, bounded logging and sensitive-data review.

---

## 16. Engineering connection/browse/reconcile

### 16.1 No universal browse claim

DNP3 does not provide a universal complete point namespace browser. Class 0/integrity responses reveal useful evidence but may not enumerate all controls, disabled points, classes or vendor configuration.

### 16.2 Connection test

Support `ICommunicationDriverConnectionTester` with a short-lived protected session that may:

- open TCP/DNP3 association;
- verify addressing/application communication;
- perform a bounded minimal integrity/status operation;
- expose sanitized endpoint/remote evidence;
- report IIN, timeout, addressing and compatibility issues.

It never activates a Runtime revision or creates TAGs.

### 16.3 Observed-point browse

`ICommunicationDriverBrowser` may synthesize family folders:

```text
Binary Inputs
Double-Bit Binary Inputs
Analog Inputs
Counters
Frozen Counters
Binary Output Status
Analog Output Status
```

Children are observed candidates from bounded reads, not authoritative device Engineering. Pages/results are marked partial whenever complete enumeration is not proven.

Candidate identity uses point kind + index. `PortableAddress` is deterministic, not a stack handle.

### 16.4 Import

No protocol-standard DNP3 engineering project file is assumed.

Future import may use:

- EliteSCADA CSV/XLSX point maps;
- deliberately supported vendor exports;
- a standardized profile artifact if one is later selected.

Do not advertise `FileImport` before a deterministic format exists.

### 16.5 Reconcile

Useful results include:

- observed/unchanged;
- family/type mismatch;
- variation mismatch but same logical point;
- access/command mismatch;
- unsupported point kind;
- unobserved/partial evidence;
- genuinely missing where proven.

“Not seen in one Class 0 response” is not automatically “deleted”.

---

## 17. Security direction

### 17.1 Plain TCP scope

Initial TCP authorization does not imply protocol security. Plain DNP3/TCP has no built-in transport confidentiality/authentication guarantee equivalent to TLS.

### 17.2 TLS

Step Function and Triangle both advertise TLS capability.

When added:

- certificates/trust are host-owned protected references;
- no private key in Engineering JSON/package;
- changed/unknown server identity fails closed under explicit trust policy;
- no permanent production “accept any certificate” mode;
- minimum TLS policy is explicit/versioned;
- diagnostics expose sanitized effective security only.

### 17.3 Secure Authentication

DNP3 Secure Authentication SAv5 is deferred from first-cut.

It is application-layer security distinct from TLS and intersects host-owned key/user/role management. Triangle publicly advertises SAv5; the Step Function 1.6 evidence used here does not establish it as a selected feature. Do not assume it.

### 17.4 Dangerous application functions

No raw function-code console. Cold/warm restart, application start/stop, file deletion, freeze controls and similar operations require separate use case, capability, authorization, confirmation/Audit and hardware evidence before any future exposure.

---

## 18. Byte/word ordering applicability

ADR-007 requires explicit byte/word ordering when a driver exposes raw multi-byte physical storage whose layout must be transformed before canonical typed decoding.

Standard DNP3 object groups already encode typed protocol values that the DNP3 stack decodes. Therefore user-configurable Byte Swap / Word Swap should **not** be exposed for ordinary DNP3 Analog/Counter objects. Applying swap after standard DNP3 decoding would corrupt semantics.

A future vendor-specific raw/octet-string profile may deliberately expose a transformation if it truly contains embedded device-specific layout. That must be versioned and scoped to that profile, never inferred from manufacturer name.

---

## 19. Persistence and lifecycle fidelity

Future canonical DNP3 Engineering must round-trip through:

- versioned JSON;
- validation / Preview / Apply / CAS;
- Working state;
- immutable revisions;
- PostgreSQL persistence;
- publish/activate;
- `.escadapkg`;
- migration;
- future fragments/copy-paste.

Persist:

- endpoint/address/timeouts/reconnect profile;
- class/integrity/unsolicited policy;
- time-sync policy;
- TLS secret/certificate references when implemented;
- point kind/index;
- static/event variation preferences;
- access intent;
- command profile;
- validation-relevant point metadata.

Do not persist as project authority:

- channel/association runtime IDs;
- stack task handles;
- sequence numbers;
- live connection state/IIN;
- runtime counters;
- observed browse candidates;
- resolved secrets/private keys;
- current TAG values.

---

## 20. Testing strategy

### 20.1 Unit tests

Required deterministic coverage:

- Data Source validation;
- address validation;
- stable point identity;
- static/event variation compatibility;
- sparse indices;
- Binary Input mapping;
- all Double-Bit Binary states;
- analog type mapping;
- full UInt16/UInt32 counter range;
- frozen counters;
- flag -> quality precedence;
- flagless variations;
- absolute timestamps;
- relative timestamp/CTO adapter handling;
- unsynchronized timestamp behavior;
- command profile validation;
- Boolean -> CROB deterministic mapping;
- SBO/Direct Operate selection;
- analog command type validation;
- command failure/status propagation;
- bounded request queue;
- reconnect/command cancellation;
- diagnostics sanitization.

### 20.2 Internal adapter seam

Wrap the selected protocol stack behind EliteSCADA-owned internal adapters so tests can inject:

- connection state;
- solicited/unsolicited fragments;
- static/event headers;
- flags/timestamps/IIN;
- command responses;
- timeout/cancellation.

Do not make every unit test instantiate vendor stack objects.

### 20.3 Loopback CI integration

When licensing permits, run the real Master adapter against a deterministic local outstation simulator.

Scenarios:

1. startup integrity Class 1230;
2. disable/enable unsolicited;
3. Binary static/event;
4. Double-Bit Binary;
5. analog integer/float;
6. counter/frozen counter;
7. event timestamps;
8. restart recovery;
9. event-buffer-overflow signal;
10. TCP disconnect/reconnect;
11. SBO success;
12. command rejection;
13. explicit Direct Operate;
14. analog output;
15. timeout/cancellation;
16. two independent DNP3 Data Sources.

Same-stack loopback is integration evidence, not final interoperability evidence.

### 20.4 Independent interoperability

Before production, test with an independent simulator/test harness to exercise:

- different qualifiers/variations;
- fragment/retransmission/unsolicited behavior;
- unsupported/malformed responses;
- negative command status;
- timestamp/time-sync behavior;
- cross-stack interoperability.

Triangle MicroWorks Test Harness is one commercial candidate. Tool procurement remains a testing/product decision.

### 20.5 Hardware

Production acceptance should include at least two distinct vendor/device families where practical, or one representative RTU/IED plus an independent conformance-grade simulator.

Validate:

- cable/switch interruption and reconnect;
- outstation reboot;
- Class 0/1/2/3 behavior;
- unsolicited across reconnect;
- sparse/non-zero indices;
- timestamps/clock state;
- event burst/backlog;
- analog/counter variations;
- CROB SBO and explicit Direct Operate;
- safe trip/close/latch/pulse test points;
- analog output command status;
- device rejections/permissions;
- TLS when promoted;
- realistic CPU/network load.

Never use live process equipment as a protocol lab target merely because it is nearby and humans enjoy avoidable excitement.

### 20.6 Conformance evidence

Maintain:

- supported feature/subset declaration;
- group/variation matrix;
- device matrix;
- known limitations;
- negative tests;
- independent interoperability results.

Library conformance evidence does not automatically certify the assembled EliteSCADA product.

---

## 21. Acceptance scenarios before production-capable review

Automated/product evidence must eventually prove at least:

1. TCP Master connects to configured outstation;
2. wrong/nonresponsive endpoint fails visibly;
3. reconnect is bounded and cancellation-safe;
4. startup disable-unsolicited -> integrity -> enable-unsolicited is deterministic;
5. Class 0 populates canonical TAGs;
6. Class 1/2/3 events resolve to same stable point identities;
7. unsolicited events use normal TAG pipeline;
8. Binary flags map correctly;
9. Double-Bit Binary preserves four states;
10. analog types map without unsafe coercion;
11. UInt32 counters above `Int32.MaxValue` remain positive via Int64;
12. timestamped events set `SourceTimestamp`;
13. no-time variations leave it null;
14. bad communication never becomes Good false/zero;
15. restart/forced/discontinuity/reference-error evidence is visible;
16. sparse indices work;
17. two DNP3 Data Sources fail/recover independently;
18. Gateway reads source only via TAG events;
19. Gateway writes resolve owning DNP3 driver;
20. SBO executes SELECT + OPERATE and checks per-point status;
21. rejected SELECT/OPERATE is failure;
22. Direct Operate only when explicitly configured;
23. Direct Operate No Response rejected first-cut;
24. analog output rejects incompatible canonical type;
25. feedback is not fabricated from command intent;
26. pending commands are not replayed after reconnect;
27. request queue is bounded;
28. diagnostics contain no secrets;
29. observed Engineering candidates do not auto-create TAGs;
30. canonical Export/Import/revision/package fidelity after Coordinator schema integration;
31. late/backlogged event semantics resolved before production activation.

---

## 22. Explicit first-cut limitations

- Master/Client only;
- TCP only initially;
- no serial first-cut;
- no Outstation/Server role;
- no UDP first-cut;
- no Secure Authentication SAv5 first-cut;
- TLS only after host certificate/trust integration;
- no generic complete online browse guarantee;
- no file transfer;
- no datasets;
- no raw function-code console;
- no normal-user cold/warm restart;
- no freeze-control surface merely because frozen counters are readable;
- no automatic command replay after reconnect;
- no optimistic feedback mutation;
- no bypass of TAG quality/Gateway/security/Audit/project lifecycle;
- no production library redistribution until licensing is resolved;
- no “all DNP3 vendors supported” claim without interoperability/hardware matrix.

---

## 23. Proposed implementation slices after Coordinator review

### Slice A — isolated adapter foundation

- `Scada.Drivers/Dnp3` isolated folder/namespace;
- internal stack abstraction;
- driver descriptor/config schema only within permitted driver scope;
- local point-binding representation pending canonical schema integration;
- type/variation/quality/timestamp mappers;
- no central DI/Program.cs edit.

### Slice B — TCP Master read proof

- one association per Data Source;
- start/stop/reconnect;
- startup integrity;
- Binary/Double-Bit/Analog/Counter/Frozen Counter;
- diagnostics;
- focused simulator tests.

### Slice C — classes/events

- Class 1/2/3 polls;
- unsolicited;
- restart/buffer-overflow recovery;
- timestamp tests;
- common late-event/current-cache decision integrated.

### Slice D — commands

- CROB;
- SBO/Direct Operate;
- typed G41;
- bounded request queue;
- result diagnostics;
- write-path tests.

### Slice E — Engineering capabilities

- connection test;
- bounded observed-point browse;
- reconcile;
- validation;
- candidate tests.

Coordinator/shared ownership remains:

- canonical Engineering schema migration;
- `Program.cs` / central DI;
- public API/routing composition;
- central frontend;
- Driver Module loader;
- common current-cache late-event policy;
- official roadmap/assignment/continuity docs.

---

## 24. Shared contract decisions requiring Coordinator reconciliation

### 24.1 Late/out-of-order events

**Required before production.** Define a protocol-independent rule so old historical events do not replace newer current values while still reaching Historian.

### 24.2 Rich canonical protocol binding

DNP3 needs structured point kind/index/variation/command fields beyond one generic address string. This belongs in the Coordinator-owned versioned Engineering migration, not private metadata.

### 24.3 Command abstraction

Simple configured scalar writes can cover some latch/analog cases, but breaker Trip/Close, pulse controls and result semantics fit the canonical Operational Command domain. Decide the seam before operator UI.

### 24.4 Raw flag diagnostics

Keep common `TagQuality` authoritative while allowing sanitized raw DNP3 flags for commissioning. Prefer an extensible common diagnostic-detail seam over DNP3-only public DTO sprawl.

### 24.5 Unsynchronized source timestamps

Define cross-protocol behavior for unsynchronized device time: whether quality becomes Uncertain, whether separate timestamp-quality metadata is added, and how Historian ordering treats it.

### 24.6 Library license

Step Function is the preferred technical lab candidate but its public license is not sufficient for commercial production/redistribution. Production stack selection is an explicit commercial/product decision.

---

## 25. Evidence and references checked

EliteSCADA:

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

External evidence checked 2026-08-29:

- Step Function DNP3 1.6.0 guide/API: `https://docs.stepfunc.io/dnp3/1.6.0/guide/`
- Step Function Rust/API reference: `https://docs.stepfunc.io/dnp3/1.6.0/rust/dnp3/`
- Step Function .NET API/license notice: `https://docs.stepfunc.io/dnp3/1.6.0/dotnet/`
- OpenDNP3 archived project: `https://github.com/dnp3/opendnp3`
- Triangle MicroWorks DNP3 library release page: `https://trianglemicroworks.com/products/source-code-libraries/dnp-scl-pages/what%27s-new`
- Triangle MicroWorks .NET/Master/Secure Authentication pages.

The Step Function guide explicitly states that library documentation is not a replacement for IEEE 1815 and recommends obtaining the standard for product development. EliteSCADA should do so before production protocol implementation is finalized.

---

## 26. Research conclusion / gate

DNP3 is technically feasible within the existing EliteSCADA Driver SDK without creating a second runtime architecture.

Existing common contracts already provide the correct high-level seams for:

- lifecycle;
- Hybrid acquisition;
- TAG publication;
- source timestamps;
- common quality;
- diagnostics;
- protected Engineering connection/browse/reconcile;
- Gateway writes through the owning driver.

Substantial implementation should proceed only after Coordinator review with these gates tracked:

1. **production library/commercial license decision remains open**;
2. **late/backlogged events versus current-cache ordering needs a shared contract decision**;
3. **rich DNP3 canonical binding requires Coordinator-owned Engineering schema integration**;
4. **command UI/domain must preserve CROB/SBO/Direct Operate semantics instead of flattening controls into generic writes**;
5. **production acceptance requires independent simulator/test-harness and representative field-device evidence**.

Research status:

`RESEARCH CONTRACT COMPLETE ON DRIVER BRANCH / PRODUCTION DNP3 NOT IMPLEMENTED / READY FOR COORDINATOR EARLY CONTRACT REVIEW`
