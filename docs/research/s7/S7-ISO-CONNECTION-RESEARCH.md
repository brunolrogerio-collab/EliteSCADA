# Siemens S7 ISO Connection research

Status: **RESEARCH IN PR / NOT IMPLEMENTED**

Research date: 2026-08-26

This document is a non-production architecture and interoperability spike for the future EliteSCADA Siemens S7 driver. It refines the locked direction in `docs/S7-ISO-CONNECTION.md` without registering an S7 Data Source, adding a protocol dependency, changing the central Engineering schema, or implementing runtime communication.

The target is classic Siemens S7 communication carried over **ISO-on-TCP / RFC1006 on TCP port 102**. This spike does not authorize S7commPlus, generic PROFINET I/O, CPU programming, RUN/STOP control, block upload/download/delete, firmware operations, or any other programming-device capability.

## 1. Executive recommendation

1. Keep the future public driver identity independent of any library, for example `siemens.s7.iso`, with an EliteSCADA-owned connection/address schema. S7.NetPlus, Sharp7 or Snap7 must remain replaceable implementation adapters rather than becoming Engineering truth.
2. Use **S7.NetPlus as the preferred first laboratory candidate**, not as a selected production dependency. It is managed C#, MIT licensed, supports the target CPU families, exposes explicit TSAP configuration, negotiates PDU size, supports multi-item PDU checks, and has asynchronous read/write/open APIs with cancellation tokens. Its published NuGet package is still 0.20.0 from 2023, so current source, open issues, .NET 10 behavior and real PLC interoperability must be revalidated before adoption.
3. Keep **Sharp7 as the second comparison/reference candidate**. It is MIT licensed and closely follows Snap7 semantics, but its current NuGet package is also from 2023 and its own documentation deliberately limits S7-1200/1500 base-protocol behavior to HMI-style basic data transfer. The future production decision should not inherit Snap7/Sharp7 native-style semantics merely because they are familiar.
4. Keep **Snap7 as a protocol/interoperability reference and possible fallback**, especially for deterministic test-server tooling. Its native runtime and LGPLv3 obligations add packaging and lifecycle complexity compared with a managed MIT dependency, so it should not be the default choice for EliteSCADA without a demonstrated protocol-correctness or performance reason.
5. Do not hide Rack/Slot/TSAP behind library defaults. Siemens current guidance gives TSAP examples `03.01` for S7-1200/1500 CPU slot 1, `03.02` for S7-300 slot 2 and `03.03` for an S7-400 CPU in slot 3. Elipse currently recommends Rack 0 / Slot 1 for S7-1200. S7.NetPlus source comments use Rack 0 / Slot 0 as its usual S7-1200/1500 constructor convention. EliteSCADA must therefore derive only **profile suggestions**, display the effective values, allow explicit override, and validate the chosen mode in a connection test.
6. Treat S7-1200/1500 optimized blocks as a hard compatibility boundary for this classic absolute-access driver. Siemens documents optimized blocks as symbolic-access-oriented. If a TIA candidate does not expose a safe classic absolute address, it must be marked unsupported for this driver rather than assigned a guessed offset.
7. Make TIA import an **Engineering workstation tool**, not a runtime dependency. Prefer a version-adapter around TIA Portal Openness for rich project extraction and also support Siemens-exported XLSX/XML/SDF as a lower-friction file path. Both paths produce neutral candidates that enter the canonical EliteSCADA `parse -> validate -> preview -> choose merge mode -> apply` workflow.
8. Start production runtime, when the roadmap gate opens, with one bounded connection per Data Source and a PDU-aware scan scheduler. Add extra parallel connections only after hardware evidence shows a real benefit and confirms PLC/resource limits. More sockets are not automatically more industrial, merely more opportunities to discover a resource limit at 03:00.

## 2. Research scope and non-goals

### In scope

- S7-300, S7-400, S7-1200 and S7-1500 classic S7/ISO-on-TCP connection behavior;
- Rack/Slot versus explicit TSAP addressing;
- PLC-side protection and PUT/GET prerequisites;
- absolute versus optimized DB access;
- typed I/Q/M/DB and legacy counter/timer addressing;
- PDU-aware grouping, bounded parallelism, reconnect and diagnostics;
- TIA Portal Openness and Siemens-supported exported-file import;
- non-destructive optional TCP/102 network assistance;
- candidate library/license/maintenance/testability comparison;
- future software-only CI plus representative PLC lab strategy.

### Explicitly out of scope

- production S7 networking code;
- a production NuGet/native dependency;
- S7 Data Source registration;
- central Engineering contract/schema changes;
- `Program.cs`, DI, API, DriverHost or frontend production integration;
- S7commPlus or proprietary engineering protocols;
- CPU mode/program/firmware operations;
- bypassing the locked sequence `TAG Gateway -> common diagnostics -> interface-validation preview -> additional external protocols`.

## 3. Elipse M-Prot comparison

Elipse M-Prot 4.0.38, updated 2026-04-27, is a useful current workflow reference for Siemens ISO/TCP Engineering. It exposes ISOTCP separately, supports Rack/Slot or explicit Destination TSAP, Source TSAP, PG/OP/PC connection type, additional connections and a maximum simultaneous-variable setting. For S7-1200 it documents ISO/TCP, Source TSAP `0100`, PG, Rack 0 and Slot 1. Its separate TIA importer uses Siemens TIA Portal Openness and presents imported project data in a tree/Tag Browser.

### Adopt

| M-Prot convenience | EliteSCADA direction |
| --- | --- |
| Rack/Slot or explicit Destination TSAP | Keep both as first-class connection addressing modes. |
| Source/Destination TSAP visibility | Expose advanced values explicitly, validate hexadecimal/range rules and report effective TSAP in diagnostics. |
| PG/OP/PC connection role | Preserve as a protocol-level connection-role option only if the selected stack maps it deterministically. |
| TIA Portal importer | Provide an optional Engineering-side importer with a version adapter and no runtime TIA dependency. |
| Tree/Tag Browser import workflow | Preserve hierarchy, subtree/multi-select and Preview rather than forcing manual address entry. |
| Superblock/multi-variable performance concept | Implement EliteSCADA-owned PDU-aware grouping and scan classes. |
| Connection/physical-state visibility | Feed the common Data Source diagnostics contract while preserving point-level TAG quality. |

### Improve

- Elipse's importer currently supports TIA versions 16 through 20 by shipping version-specific importer folders and `Siemens.Engineering.dll`. Current Siemens documentation is already at TIA Portal V21. EliteSCADA should isolate TIA version coupling behind an Engineering importer adapter rather than hard-code a fixed version ceiling into the SCADA runtime.
- Elipse documents that `LReal` variables are ignored by the importer and ArrayDB/InstanceDB import is unsupported. EliteSCADA must never silently drop such candidates. They must appear in Preview with explicit `Unsupported` or `RequiresMapping` status and a reason.
- Elipse's `Extra Connections` and `Max Simult Req` are valuable ideas but low-level knobs. EliteSCADA should prefer safe profiles such as Conservative/Balanced/HighThroughput, with expert overrides and runtime telemetry, instead of inviting operators to tune simultaneous requests by folklore.
- Connection status must distinguish socket/COTP/S7-session readiness from successful current TAG communication. A connected transport is not proof that every address is valid or every write is authorized.

### Avoid

- Do not copy Elipse's numeric N/B parameter encoding where data type and area are combined arithmetically. EliteSCADA should store a typed binding and optionally expose a canonical text representation.
- Do not make TIA Portal or `Siemens.Engineering.dll` a dependency of the production Runtime service.
- Do not silently ignore unsupported TIA types/blocks.
- Do not expose CPU administration/programming operations through the normal SCADA TAG path.

## 4. Candidate .NET stack comparison

### 4.1 S7.NetPlus

Current evidence:

- MIT licensed;
- public package `S7netplus` 0.20.0, published 2023-08-03;
- repository supports S7-200/300/400/1200/1500;
- pure managed .NET library;
- default protocol port 102;
- explicit `TsapPair` support;
- negotiated `MaxPDUSize` retained by the client;
- read/write PDU-size checks for multi-variable operations;
- asynchronous open/read/write methods with cancellation tokens;
- source comments explicitly describe cancellation as cooperative and not necessarily immediate in all cases;
- unit tests use a Snap7 server.

Strengths for EliteSCADA:

- easiest lifecycle fit for a .NET 10 Driver Module;
- permissive MIT license;
- explicit TSAP and PDU primitives align well with an EliteSCADA-owned schema/scheduler;
- managed async API allows integration with bounded cancellation and Data Source lifetime management;
- error responses include useful protocol categories such as object missing, type inconsistent/unsupported, access not allowed and address out of range that can seed sanitized diagnostics.

Risks/required validation:

- latest published package is old enough that adoption cannot rely on NuGet freshness alone;
- cancellation is documented as cooperative;
- its constructor documentation suggests Rack 0 / Slot 0 for S7-1200/1500 while Siemens and Elipse examples commonly express CPU slot 1. The EliteSCADA adapter must own TSAP derivation/profile defaults instead of trusting opaque library conventions;
- current open issues and multi-write behavior require real hardware regression coverage;
- production use must verify .NET 10, reconnect behavior, socket disposal, timeout semantics, PDU edge cases, strings/date-time codecs and repeated writes.

**Research recommendation:** preferred first library for the dedicated future laboratory spike, with no dependency added now.

### 4.2 Sharp7

Current evidence:

- MIT licensed;
- public package 1.1.84, published 2023-05-11;
- C# port of Snap7;
- documentation says S7-300/400/WinAC are fully supported and S7-1200/1500 support is HMI-style basic data transfer;
- its S7-1500 notes require global DBs, optimized block access disabled, suitable access level and GET/PUT enabled.

Strengths:

- small C# surface with a long-known Snap7 protocol model;
- useful second implementation for cross-checking framing, area codes and PLC interoperability;
- permissive MIT package license.

Risks:

- published package/source activity is older than desirable for a new production dependency;
- architecture inherits a lower-level Snap7 style rather than a modern cancellation-first .NET API;
- production would still require an EliteSCADA async/timeout/lifetime wrapper;
- base-protocol limitations on modern CPUs must remain explicit;
- multi-variable write edge cases deserve hardware and frame-level regression tests before use.

**Research recommendation:** retain as comparison/reference candidate, not first choice.

### 4.3 Snap7 native library

Current evidence:

- official repository starts with release 1.4.3;
- native multi-platform client/server/partner implementation;
- supports synchronous and asynchronous transfer models and the target S7 families;
- LGPLv3;
- provides useful server/demo/test tooling.

Strengths:

- mature protocol reference and useful deterministic server for tests;
- broad cross-platform history;
- possible fallback if managed libraries demonstrate protocol gaps that matter to EliteSCADA.

Risks:

- native binary packaging, platform/architecture distribution and crash boundary are more complex;
- LGPL compliance and redistribution need explicit legal/package review;
- exposes PLC administrative functions that are outside EliteSCADA's initial SCADA scope and must not leak through the adapter;
- native async semantics still need a .NET cancellation/lifetime contract.

**Research recommendation:** test/reference tool and fallback, not default production choice.

### 4.4 Selection gate for the future production task

The production library decision must be made only after an isolated lab spike scores each candidate against:

- S7-300/400/1200/1500 connection success;
- explicit Rack/Slot and explicit TSAP;
- connect/request timeout and cancellation;
- clean reconnect after cable/network loss and CPU restart;
- negotiated PDU reporting;
- multi-variable read correctness and batching limits;
- repeated/multi-variable write correctness;
- error classification;
- String/WString/date-time codec correctness;
- CP343/CP443 or equivalent communication-processor topology where available;
- simultaneous independent Data Sources;
- .NET 10 deployment behavior;
- license/redistribution obligations.

## 5. CPU family connection matrix

The values below are **Engineering suggestions**, not immutable protocol constants. Hardware configuration and explicit TSAP always win.

| Family | TCP | Suggested initial profile | Explicit override | Protection/access notes | Classic absolute DB direction |
| --- | --- | --- | --- | --- | --- |
| S7-300 | 102 | Rack 0 / Slot 2 for normal CPU-in-central-rack topology | Rack/Slot and Source/Destination TSAP | No modern 1200/1500 PUT/GET checkbox model should be assumed; CP/CPU topology matters. | Supported for standard classic areas/DBs subject to CPU/library limits. |
| S7-400 | 102 | No universal hidden slot. Slot 3 is a Siemens TSAP example, but CPU slot can vary by rack/configuration. | Rack/Slot and explicit TSAP mandatory for non-default topologies | Integrated interface vs CP443 and redundant/H systems need lab-specific handling. | Supported for classic areas/DBs subject to topology/library limits. |
| S7-1200 | 102 | Profile suggestion Rack 0 / CPU slot 1 for Engineering display/TSAP derivation; validate against selected stack | Explicit TSAP is first-class | PUT/GET permission may be required for remote classic access; current security configuration can further restrict access. | Only when a safe absolute address exists. Optimized/symbolic-only DB members are not silently mapped. |
| S7-1500 | 102 | Profile suggestion Rack 0 / CPU slot 1 for Engineering display/TSAP derivation; validate against selected stack | Explicit TSAP is first-class | PUT/GET connection mechanism and CPU protection/users/roles may restrict classic remote access. | Global/non-optimized absolute DB access only for initial classic driver unless lab evidence proves another safe base-protocol path. |

Siemens 2025 examples construct partner TSAP as connection resource `03` plus CPU slot: `03.01` for S7-1200/1500 slot 1, `03.02` for S7-300 slot 2 and `03.03` for an S7-400 CPU in slot 3. This reinforces that Rack/Slot is a convenience representation for TSAP derivation, not a magical device discovery mechanism.

### Connection modes proposed for Engineering

`RackSlot`:

- host;
- port, default 102;
- CPU family/profile;
- rack;
- slot;
- connection role/resource if used by the selected adapter;
- source TSAP override optional;
- derived destination TSAP shown as effective diagnostics.

`ExplicitTsap`:

- host;
- port;
- source TSAP;
- destination TSAP;
- optional CPU family/profile for diagnostics/import rules only;
- no invented Rack/Slot if the topology is defined only by TSAP.

A connection test should report the requested/effective TSAP, negotiated PDU and failure class without exposing raw packet dumps or secrets in normal diagnostics.

## 6. Protection, PUT/GET and failure classification

Siemens documents that S7-1200/1500 remote access using absolute PUT/GET-style services requires the CPU connection mechanism to permit PUT/GET. Siemens also documents that optimized S7-1200/1500 blocks use symbolic access; classic absolute PUT/GET cannot be assumed to address them.

EliteSCADA guidance must therefore avoid the dangerous instruction "enable full access" as a universal cure. The Engineering connection test should instead identify the narrowest known issue and direct the engineer to the CPU's current Protection & Security / connection mechanisms configuration.

### Proposed diagnostic classification

- `TransportUnavailable`: TCP connection refused/unreachable/reset/timeout.
- `IsoConnectionRejected`: COTP connection not confirmed or TSAP rejected.
- `S7SessionRejected`: S7 communication setup failed.
- `ProtectionDenied`: protocol response indicates access/object permission denied and evidence supports protection classification.
- `AddressInvalid`: address out of range/object missing.
- `TypeUnsupported`: PLC/library reports inconsistent/unsupported type.
- `WriteRejected`: session is healthy but write operation is denied/fails.
- `Timeout`: request budget exceeded.
- `ProtocolFault`: malformed/unexpected S7 response after sanitization.

Transport failures, protection failures and address/type failures must not be collapsed into one red "Communication error" if the stack supplies enough evidence to distinguish them.

## 7. Optimized versus non-optimized DB boundary

For the classic initial S7 ISO driver:

1. An absolute address is required for an active runtime binding.
2. If TIA reports an optimized/symbolic-only member and no safe absolute classic address exists, candidate support is `UnsupportedClassicAbsoluteAccess`.
3. The importer preserves its symbolic path, type, comments and support reason for Preview, but **does not fabricate an offset**.
4. The engineer may change the PLC DB design outside EliteSCADA, use a deliberately exposed non-optimized/global DB interface, or later choose another Siemens protocol/module that explicitly supports symbolic optimized access.
5. EliteSCADA must not weaken CPU protection automatically or modify TIA/PLC settings.

This restriction is a feature of honest Engineering, not a missing checkbox. A plausible-looking `DBX` guessed from declaration order is worse than an explicit unsupported diagnostic because it can read the wrong process value while looking perfectly healthy.

## 8. Typed S7 address and data-type proposal

The future driver-specific TAG binding should be structured and versioned. A suggested neutral shape is:

- `area`: `Input`, `Output`, `Marker`, `DataBlock`, `Counter`, `Timer`;
- `dbNumber`: required only for `DataBlock`;
- `byteOffset`;
- `bitOffset`: only for Bool/bit access, range 0..7;
- `dataType`;
- `elementCount`: for explicitly supported arrays;
- `stringMaxLength`: for String/WString where applicable;
- `rawByteLength`: derived/validated, not free-form truth;
- optional canonical text form for display/import/export;
- optional original TIA symbolic path as provenance, never as a substitute for a runtime absolute address in this classic driver.

### Canonical text examples

- `I0.1`
- `IB4`
- `IW6`
- `ID8`
- `Q0.0`
- `QW4`
- `M10.2`
- `MW20`
- `MD24`
- `DB12.DBX4.2`
- `DB12.DBB6`
- `DB12.DBW8`
- `DB12.DBD10`

Canonical text is convenience syntax. Parsing produces the typed binding; runtime must not repeatedly parse arbitrary user strings on the hot path.

### Initial production type support recommendation

The first production slice should deliberately start with types that have unambiguous classic byte layouts and hardware tests:

- Bool;
- Byte / USInt;
- SInt if the selected adapter exposes it correctly;
- Word / UInt;
- Int;
- DWord / UDInt;
- DInt;
- Real.

Then add only after codec + PLC-family verification:

- LReal;
- String;
- WString;
- S5Time/Time;
- Date / TimeOfDay / DateAndTime and newer date-time families;
- arrays;
- structures/UDTs.

Unsupported/lossy imports must remain visible candidates with reasons. Counter/Timer areas should be treated as legacy-profile capabilities and disabled for modern profiles unless explicitly proven supported by the chosen stack/CPU.

### Writability

`Writable` is not inferred solely from area. It is the conjunction of:

- imported/engineered intent;
- CPU/TIA HMI-writability metadata where available;
- area/type rules;
- Data Source write policy;
- current runtime authorization for a human/script operation;
- actual PLC response.

TIA metadata is valuable guidance, not permission to bypass EliteSCADA security.

## 9. PDU-aware polling and batching

### Read scheduler

Group active points by:

1. Data Source;
2. scan class/profile;
3. S7 area;
4. DB number where applicable;
5. compatible contiguous/near-contiguous byte ranges;
6. negotiated PDU request/response limits.

The planner may merge small gaps only when the extra bytes remain below an engineered safe threshold and do not cross a semantic or protocol boundary. One planned byte span can decode multiple TAGs after a successful read.

The runtime must:

- use negotiated PDU size rather than a hard-coded maximum;
- cap request size below the protocol/library limit with explicit framing overhead;
- bound outstanding requests;
- avoid duplicate reads for overlapping TAG spans in the same scan cycle;
- publish independent per-TAG quality;
- allow one bad address/block to be isolated by plan splitting/retry strategy rather than permanently contaminating unrelated points.

### Parallelism

Start with **one connection per Data Source**. A future expert performance profile may allow a small bounded parallel connection count only after target PLCs prove predictable resource behavior. Elipse's `Extra Connections` shows the practical value, but EliteSCADA should not assume every PLC has spare S7 connection resources.

### Writes

Do not merge unrelated operator/script/Gateway writes merely to save PDUs if doing so changes ordering, confirmation or audit semantics. Multi-variable writes may be used only when the write contract explicitly preserves operation identity and failure attribution. A command must not succeed merely because its neighbor in the same packet did.

## 10. Reconnect and lifecycle direction

Each S7 Data Source owns its own communication state machine:

`Stopped -> Connecting -> Healthy -> Degraded/Reconnecting -> Healthy | Faulted`

Recommended behavior:

- bounded connect timeout;
- bounded request timeout;
- cancellation tied to Active Revision/Data Source disposal;
- exponential reconnect backoff with a maximum and small jitter;
- no unbounded retry task accumulation;
- after communication loss, affected TAGs age/transition quality through the common TAG-quality rules;
- recovery rebuilds/validates the S7 session and negotiated PDU before declaring the Data Source healthy;
- failure of one S7 Data Source does not affect another S7, Modbus or Internal Memory source.

The future adapter must assume a socket can become invalid between any state check and I/O. Library `Connected` flags are hints, not an industrial liveness oracle.

## 11. Common diagnostics plus S7 detail

The future driver must implement the common Data Source diagnostics contract, not a private S7 dashboard.

Common fields include:

- Data Source identity/name/type;
- sanitized endpoint identity;
- health/state and last state change;
- last successful/failed communication;
- cycles/requests/success/failure/timeouts;
- consecutive failures;
- reconnect count;
- read/write operation counts;
- request/scan latency;
- data age;
- associated TAG count and quality counts;
- sanitized last error.

S7-specific extension fields may include:

- CPU family/profile;
- configured addressing mode;
- configured and effective Rack/Slot;
- configured/effective Source and Destination TSAP;
- connection role/resource where meaningful;
- negotiated PDU size;
- current planned read-block count;
- current connection count;
- last sanitized S7 error class/code.

Do not return raw PLC memory, packet dumps, project symbols, host stack traces or authentication/security secrets through the normal diagnostic snapshot.

## 12. TIA Portal import strategy

### 12.1 Preferred rich path: optional Openness importer

Create a separate **Engineering workstation importer process/tool** in the future, outside the Runtime service. It may use the TIA Portal Openness API installed with the engineer's TIA environment.

Design rules:

- version-specific Siemens assemblies stay behind importer adapters;
- the importer declares the TIA versions it can use and fails clearly for an unsupported local installation;
- the Runtime service never requires TIA Portal;
- the importer opens/extracts only on explicit user action;
- it produces a neutral candidate document/stream, not a direct database mutation;
- no PLC program is downloaded/changed;
- no credentials or proprietary project internals are copied beyond the fields required for Engineering candidates.

Useful extraction targets:

- project/device/CPU identity;
- CPU family/order/firmware metadata where accessible;
- PLC tag-table hierarchy;
- tag name/symbol path;
- logical address;
- Siemens data type;
- comments/descriptions;
- HMI visibility/accessibility/writability indicators;
- DB definitions/members;
- UDT information where safely available;
- optimized/non-optimized block information;
- source hierarchy for proposed TAG paths.

### 12.2 File-based path

TIA Portal V21 supports PLC-tag export as XLSX, XML or SDF. XLSX includes fields such as Name, Path, Data Type, Logical Address, Comment, HMI Visible, HMI Accessible and HMI Writeable. This is a strong no-automation fallback for normal global PLC tags.

Limit: Siemens documents that structured PLC tags based on a PLC data type are not expanded into editable individual elements in the simple PLC-tag export. Therefore a file-only importer must not claim full DB/UDT introspection from tag-table XLSX alone. Richer Openness/project/block exports are required for deeper structure.

### 12.3 Neutral import candidate

A future candidate should contain enough provenance to explain every Preview decision, for example:

- `sourceKind`: `TiaOpenness`, `TiaXlsx`, `TiaXml`, `TiaSdf`;
- source project/export identity and TIA version;
- device/CPU identity;
- source hierarchy/table/DB;
- source symbol path;
- logical/absolute S7 address if available;
- Siemens data type;
- proposed EliteSCADA data type;
- HMI visible/access/write metadata if supplied;
- optimized/non-optimized/global/instance/array block information when known;
- support status: `Supported`, `Warning`, `RequiresMapping`, `Unsupported`;
- issue codes/messages;
- proposed EliteSCADA TAG path/name;
- selected S7 Data Source reference;
- proposed scan profile;
- description/comment;
- original source provenance for rescan/diff.

### 12.4 Canonical flow

`TIA project/export -> parse -> candidates -> validate -> preview -> choose merge mode -> apply`

Preview must show unsupported candidates rather than suppress them. Applying ultimately creates/updates the public versioned Engineering model through the same backend-authoritative workflow as other imports.

## 13. Rescan and reconciliation direction

TIA import is not a one-time copy wizard. A future rescan should compare source provenance with existing engineered TAGs and classify:

- unchanged;
- new;
- source removed;
- renamed/moved;
- address changed;
- data type changed;
- access/writability changed;
- optimized/access model changed;
- ambiguous match.

Stable EliteSCADA TAG IDs remain authoritative after creation. Source symbol/path/address are reconciliation evidence, not permission to generate a new ID whenever TIA changes. Destructive removal must be explicit and dependency-aware.

## 14. Optional bounded TCP/102 network assistance

Classic S7 ISO does not provide OPC-UA-style authoritative server discovery. The future tool should therefore be named/worded as **network assistance** or **connection candidate scan**, never as guaranteed PLC discovery.

Recommended initial safety defaults for a future implementation, subject to product validation:

- explicit user initiation only;
- explicit interface/CIDR/range;
- default maximum scope equivalent to one `/24` or 256 addresses per invocation;
- maximum 16 concurrent TCP connect probes;
- 500-1000 ms per-host connect budget on a local industrial network;
- global cancellation and a bounded total time budget;
- TCP/102 connect probe only by default;
- optional COTP/S7 identity/session probe only after the selected library proves it is read-only/non-destructive on the supported families;
- never write values;
- never change CPU state;
- never enumerate/download PLC program blocks during scan;
- never auto-create a Data Source from a positive port probe.

A TCP/102 listener is only a candidate. Connection test with explicit Engineering parameters remains the deterministic acceptance path.

## 15. TIA/PLC test and CI strategy

### Software-only CI

Run on normal EliteSCADA CI without industrial hardware:

- typed address parser/formatter round-trip;
- area/type/offset validation;
- TSAP parsing and deterministic profile derivation;
- PDU planner boundary tests;
- importer candidate parsing for checked-in sanitized XLSX/XML/SDF fixtures;
- optimized/unsupported candidate diagnostics;
- scheduler cancellation/backoff state tests;
- protocol adapter tests against a deterministic Snap7/test server where legally/operationally appropriate;
- error sanitization;
- two independent simulated Data Source state machines.

A software server is useful for framing and deterministic failures but is **not** proof of Siemens PLC interoperability.

### Siemens simulation

- classic/basic PLCSIM must not be assumed to expose an external normal TCP/102 path suitable for third-party clients;
- PLCSIM Advanced can participate in virtual Ethernet/TCP testing for supported scenarios;
- simulator limitations, especially around communication processors, protection and some PUT/GET paths, must be documented per tested TIA/firmware version;
- simulator success never replaces hardware acceptance for writes/security/reconnect.

### Hardware lab acceptance

Minimum recommended representative lab before production release:

- at least one S7-1200;
- at least one S7-1500;
- representative S7-300 and S7-400 access, owned or scheduled/borrowed, before claiming those families production-supported;
- CP343/CP443 or another communication-processor topology if advertised;
- managed switch/network fault injection or controlled cable/interface interruption.

Test matrix:

- Rack/Slot profile connection;
- explicit TSAP connection;
- wrong Rack/Slot/TSAP classification;
- PUT/GET disabled/denied behavior;
- read-only vs writable points;
- optimized DB rejection;
- non-optimized/global DB reads/writes;
- I/Q/M areas as applicable;
- all supported scalar codecs and boundary values;
- String/WString/date-time only when promoted to supported;
- negotiated PDU boundaries;
- multi-variable reads;
- repeated/multi-variable writes;
- CPU restart/network disconnect/reconnect;
- latency/timeouts/backoff;
- two simultaneous S7 Data Sources and S7 + Modbus isolation;
- activation/revision disposal;
- Gateway interoperability only after the protocol-independent Gateway runtime is official.

Hardware tests should run as a scheduled/manual interoperability pipeline with exact CPU order number, firmware, TIA version and network topology recorded in the result. They must not make ordinary PR CI depend on a PLC sitting under someone's desk pretending to be a cloud service.

## 16. Future Engineering UX proposal

When production development is authorized:

1. create Siemens S7 ISO Data Source;
2. select CPU family/profile;
3. enter host/IP and optional port;
4. choose `Rack/Slot` or `Explicit TSAP`;
5. inspect/override profile suggestions;
6. choose connect/request/reconnect performance profile;
7. run read-only connection test;
8. optionally import TIA candidates or manually create TAG bindings;
9. preview address/type/access/protection issues;
10. apply through normal Engineering lifecycle;
11. activate revision transactionally;
12. inspect common communication diagnostics and point quality.

Warnings for modern CPUs should be contextual:

- PUT/GET permission not proven;
- optimized/symbolic-only DB candidate unsupported by classic absolute access;
- TIA says not HMI-accessible or not writable;
- explicit TSAP required/overridden;
- write capability enabled for the Data Source.

## 17. Later production implementation breakdown

These are future slices only after the external-protocol gate opens.

### Slice A: public driver schema and typed S7 binding

- versioned `siemens.s7.iso` Data Source schema;
- Rack/Slot and TSAP modes;
- timeouts/reconnect/performance profile;
- typed address model;
- import/export/validation/package coverage;
- no runtime until contract is stable.

### Slice B: library laboratory spike and adapter

- benchmark/interop S7.NetPlus vs Sharp7/Snap7 reference;
- select/pin one implementation or justify a thin EliteSCADA protocol layer;
- .NET 10, cancellation, timeout, PDU and licensing verification;
- hardware evidence attached to decision record.

### Slice C: read runtime

- Data Source compiler;
- one driver instance per Data Source;
- PDU-aware scan planner;
- cache/Event Bus/TAG quality integration;
- reconnect and independent-failure behavior.

### Slice D: write/runtime security

- typed writes through owning Data Source/provider;
- normal ProcessValueWrite/command/script authorization boundary;
- write result/error classification;
- Audit at human/configuration boundaries without flooding automated scans.

### Slice E: common diagnostics integration

- common counters/state/latency/quality summaries;
- S7-specific TSAP/PDU diagnostics;
- protected diagnostics API;
- no parallel private health system.

### Slice F: TIA/file importer

- optional Openness version adapters;
- XLSX/XML/SDF fallback;
- candidates/Preview/Apply;
- optimized/unsupported diagnostics;
- rescan/diff.

### Slice G: Engineering UI and network assistance

- S7 Data Source editor;
- connection test;
- bounded TCP/102 candidate scan;
- TIA import selection/Preview;
- diagnostics drill-down.

### Slice H: interoperability certification

- software CI fixtures;
- PLCSIM Advanced scenarios where valid;
- S7-300/400/1200/1500 hardware matrix;
- CP topology where claimed;
- packaging/module lifecycle tests;
- failure/reconnect/security validation.

## 18. Decisions intentionally deferred

This research does **not** lock:

- S7.NetPlus, Sharp7 or Snap7 as production dependency;
- exact public driver key or serialized property spelling;
- exact scan-class minimums;
- exact maximum parallel connection count;
- promotion of LReal/String/WString/date-time/array/UDT support;
- a specific TIA Portal version range;
- S7commPlus support;
- any PLC programming/admin function.

Those require a later implementation/lab decision and may not weaken the public Engineering, security, diagnostics or roadmap gates.

## 19. Source register

Primary and implementation-reference material used for this research:

### Siemens

- Siemens, *S7 communication between S7 CPU and PC station*, V2.2, 07/2025. Partner TSAP examples for S7-1200/1500/300/400: https://cache.industry.siemens.com/dl/files/801/67295801/att_1332525/v2/67295801_SIMATIC_NET_OPC_UA_S7variable_DOC_V2_2_en.pdf
- Siemens, *S7 communication with PUT/GET*, S7-1500 example: https://support.industry.siemens.com/cs/attachments/82212115/82212115_s7_communication_s7-1500_en.pdf
- Siemens, *Programming Guideline for S7-1200/1500*, optimized block access: https://support.industry.siemens.com/cs/attachments/90885040/81318674_Programming_guideline_DOC_v16_en.pdf
- Siemens STEP 7 V21, *Exporting PLC tags*: https://docs.tia.siemens.cloud/r/en-us/v21/declaring-plc-tags/editing-plc-tag-tables/exporting-and-importing-plc-tags/exporting-plc-tags
- Siemens STEP 7 V21, *Format of the export file (*.xlsx)*: https://docs.tia.siemens.cloud/r/en-us/v21/declaring-plc-tags/editing-plc-tag-tables/exporting-and-importing-plc-tags/format-of-the-export-file-.xlsx
- Siemens STEP 7 V21, *Basics for exporting and importing PLC tags*: https://docs.tia.siemens.cloud/r/en-us/v21/declaring-plc-tags/editing-plc-tag-tables/exporting-and-importing-plc-tags/basics-for-exporting-and-importing-plc-tags
- Siemens TIA Portal Openness V21, *Exporting PLC tag tables*: https://docs.tia.siemens.cloud/r/en-us/v21/tia-portal-openness-api-for-automation-of-engineering-workflows/export/import/importing/exporting-data-of-a-plc-device/tag-tables/exporting-plc-tag-tables

### Elipse M-Prot

- M-Prot 4.0.38 introduction: https://docs.elipse.com.br/documents/en-us/driver/mprot/latest/mprot_intro.html
- M-Prot ISO/TCP driver settings: https://docs.elipse.com.br/documents/en-us/driver/mprot/latest/mprot_driver_settings.html
- M-Prot TIA Portal Importer: https://docs.elipse.com.br/documents/en-us/driver/mprot/latest/mprot_tiaportalimporter.html
- M-Prot importing Tags / current limitations: https://docs.elipse.com.br/documents/en-us/driver/mprot/latest/mprot_importtags.html
- M-Prot syntactical Tag addressing: https://docs.elipse.com.br/documents/en-us/driver/mprot/latest/mprot_tags_reference_symbolic_addressing.html

### Candidate libraries

- S7.NetPlus repository: https://github.com/S7NetPlus/s7netplus
- S7.NetPlus NuGet 0.20.0: https://www.nuget.org/packages/S7netplus/0.20.0
- Sharp7 repository: https://github.com/fbarresi/Sharp7
- Sharp7 NuGet 1.1.84: https://www.nuget.org/packages/Sharp7/1.1.84
- Snap7 official repository: https://github.com/davenardella/snap7
- Snap7 project/license reference: https://sourceforge.net/projects/snap7/

## 20. Research completion assessment

This document satisfies the assigned documentation spike by providing:

- sourced Elipse comparison with adopt/improve/avoid decisions;
- a preferred library-spike direction without adding a dependency;
- S7-300/400/1200/1500 connection guidance;
- protection/PUT-GET and optimized-DB boundaries;
- typed addressing/data-type strategy;
- PDU-aware batching/reconnect/diagnostics direction;
- TIA Openness and file-import strategy;
- canonical candidate/Preview/Apply contract direction;
- bounded non-destructive network assistance;
- software/simulator/hardware interoperability strategy;
- a production implementation breakdown that remains behind the roadmap gate.

**RESEARCH IN PR:** architecture/recommendations only.

**NOT IMPLEMENTED:** Siemens S7 production driver, dependency, Data Source, schema, runtime, API, UI or DriverHost integration.
