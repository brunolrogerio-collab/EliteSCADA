# IEC 60870-5-104 Driver Handoff — DEV Driver 06

Date: 2026-08-30
Branch: `driver6/iec-60870-5-104`
Implementation baseline before this handoff document: `b5ee4319b7969357ab250a1c2fc2da7fafb0879d`
Main baseline reviewed for handoff: `b069e7a866845eda0d365e93bccb567dab8524ff`
Assignment status: **AUTHORIZED / PARALLEL — RESEARCH FIRST / PARKED FROM MAINLINE**

This document is the formal handoff of the IEC 60870-5-104 workstream. It records what the branch implements, what is only covered by unexecuted automated tests, what remains for independent simulator/hardware validation, and which integration decisions still belong to the Coordinator.

The handoff document itself is a documentation-only commit after the implementation baseline above. Use the current branch HEAD when integrating or reviewing rather than assuming the baseline SHA is the final branch tip.

---

## 1. Scope delivered

The branch implements a first-release IEC 60870-5-104 client/master slice with a private EliteSCADA-owned protocol implementation and no production dependency on an external GPL library.

Delivered scope includes:

- TCP client lifecycle with configurable host/port and IEC-104 session timers;
- STARTDT, STOPDT and TESTFR U-format handling;
- APCI I/S/U codec;
- 15-bit `N(S)` / `N(R)` sequencing modulo 32768;
- `k` send window and `w` receive acknowledgement threshold;
- T0/T1/T2/T3 supervision;
- bounded receive queue and fail-closed overflow behavior;
- deterministic reconnect policy;
- General Interrogation per configured Common Address;
- monitored ASDU decode for the first-release matrix;
- spontaneous/event indication handling;
- Cause of Transmission parsing including TEST isolation;
- Common Address + 24-bit IOA addressing;
- CP56Time2a source timestamps with configured station timezone;
- IEC quality descriptor mapping to canonical EliteSCADA `TagQuality`;
- Direct Operate and Select-Before-Operate command transactions;
- explicit command outcomes including ambiguous physical outcome;
- command replay prevention after reconnect;
- protocol/transport/managed diagnostics;
- Engineering connection test;
- bounded Observe+GI browse candidate capture;
- transient reconciliation;
- monitored point-list CSV import/export;
- resource bounds for CSV import;
- deterministic protocol/fault-injection tests;
- acceptance/interoperability matrix for future lab and hardware validation.

The branch intentionally does **not** integrate a persisted public IEC-104 TAG binding, public `ICommunicationDriver` registration, DriverHost composition, TLS/IEC 62351-3, or canonical project Preview/Apply persistence. Those require Coordinator/shared-contract decisions described later in this handoff.

---

## 2. Protocol contract and supported features

### 2.1 Driver identity and endpoint model

Planned driver type:

`iec60870.5.104`

One EliteSCADA Data Source represents one TCP/application endpoint. A single endpoint/session may serve multiple IEC Common Addresses.

Default TCP port is 2404 and is configurable.

The implemented/default IEC-104 application field widths are:

- Cause of Transmission: 2 bytes, including Originator Address;
- Common Address: 2 bytes;
- Information Object Address: 3 bytes;
- APDU start byte: `0x68`;
- maximum APDU length: 253 bytes;
- maximum ASDU payload within APCI constraints: 249 bytes.

### 2.2 Session defaults

Current default session options:

- T0: 30 seconds;
- T1: 15 seconds;
- T2: 10 seconds;
- T3: 20 seconds;
- K: 12;
- W: 8.

Validation enforces `T2 < T1` and sane positive values.

### 2.3 Reconnect

Reconnect delays are deterministic and bounded:

`1 s, 2 s, 5 s, 10 s, 30 s`, then 30-second cap.

Cancellation interrupts the delay. The backoff resets after a stable successful session.

Each fresh session performs STARTDT and new General Interrogation bootstrap. Operational commands are **never queued for replay across reconnect**.

---

## 3. APCI/TCP implementation

Key files:

- `Iec104Apci.cs`
- `Iec104SequenceState.cs`
- `Iec104SessionPrimitives.cs`
- `Iec104TcpClientAdapter.cs`
- `Iec104TransportDiagnostics.cs`
- `Iec104TransmissionException.cs`

Implemented behavior:

- strict I/S/U frame parsing and serialization;
- 15-bit sequence arithmetic modulo 32768;
- send-window reservation;
- peer acknowledgement validation;
- receive sequence validation;
- STARTDT/STOPDT/TESTFR U-function handling;
- TCP `NoDelay`;
- one active ASDU reader per session;
- bounded incoming queue (1024 ASDUs);
- fail closed rather than silently dropping process data if queue admission cannot proceed safely;
- T1 supervision of outstanding I-frames;
- T2 delayed S-frame acknowledgement;
- T3 TESTFR supervision;
- session failure propagation;
- protocol-error versus transport/session-error diagnostics;
- no sequence rollback/reuse after transmission ambiguity.

### 3.1 Ambiguous write boundary

An I-format send reserves `N(S)` before writing the APDU. Once that sequence is reserved, a write failure cannot prove whether the peer received zero, some, or all of the frame.

The concrete adapter therefore wraps failures after reservation in `Iec104AmbiguousTransmissionException` and faults the session. The sequence is not rolled back and the window slot is not reused within the failed session.

The command coordinator maps this condition to the explicit `Ambiguous` outcome and reconnect never replays the command automatically.

A deterministic coordinator-seam test exists. A deterministic real-socket test that forces `NetworkStream.WriteAsync` to fail at the precise post-reservation/pre-confirmation boundary remains desirable, but must not rely on timing races.

---

## 4. General Interrogation

Implemented GI contract:

- Type: `C_IC_NA_1`;
- Cause: Activation;
- IOA: 0;
- QOI: 20 (station interrogation);
- performed per configured Common Address;
- positive ACT_CON begins collection;
- data follows normal monitored decode path;
- ACT_TERM completes that CA transaction;
- negative confirmation becomes rejected;
- COT TEST responses do not advance the operational GI transaction.

Reconnect starts a fresh GI cycle.

---

## 5. Supported monitored ASDUs

First-release monitored matrix:

| Type ID | IEC name | EliteSCADA value |
|---:|---|---|
| 1 | `M_SP_NA_1` | Boolean |
| 30 | `M_SP_TB_1` | Boolean + CP56 |
| 3 | `M_DP_NA_1` | four-state enum |
| 31 | `M_DP_TB_1` | four-state enum + CP56 |
| 7 | `M_BO_NA_1` | Int32 bitstring |
| 33 | `M_BO_TB_1` | Int32 bitstring + CP56 |
| 9 | `M_ME_NA_1` | normalized Float |
| 34 | `M_ME_TD_1` | normalized Float + CP56 |
| 11 | `M_ME_NB_1` | Int16 |
| 35 | `M_ME_TE_1` | Int16 + CP56 |
| 13 | `M_ME_NC_1` | Float |
| 36 | `M_ME_TF_1` | Float + CP56 |

Both SQ=0 and SQ=1 layouts are supported. SQ=1 addressing is checked for 24-bit IOA overflow.

`M_ST_*` and `M_IT_*` are deliberately deferred pending shared semantic decisions.

### 5.1 Normalized values

`M_ME_NA_1/M_ME_TD_1` decode signed Int16 raw values to Float using:

`raw / 32768f`

### 5.2 Short float non-finite policy

For `M_ME_NC_1/M_ME_TF_1`, IEEE-754 NaN and positive/negative Infinity are preserved as the received Float value, but semantic quality becomes `Uncertain` unless the IEC quality descriptor already maps to a worse canonical quality.

This prevents a hostile/nonconforming-but-representable float from appearing with `Good` quality without unnecessarily terminating the whole IEC-104 session.

---

## 6. Point identity

Portable/transient IEC address syntax:

`ca=<CommonAddress>;ioa=<InformationObjectAddress>`

Current portable address supports:

- CA 0..65535;
- IOA 0..16777215;
- tolerant ordering/case/whitespace while parsing;
- rejection of duplicate keys and unknown keys.

This address intentionally does **not** represent the future full persisted binding contract. A canonical runtime binding also needs semantic family/type expectations and command profile information.

---

## 7. Cause of Transmission

COT parsing includes:

- cause code;
- positive/negative confirmation bit;
- TEST bit;
- Originator Address when using 2-byte COT.

COT TEST isolation is enforced across:

- operational commands;
- GI control progression;
- runtime monitored telemetry;
- Engineering observation candidates.

TEST-marked operational-looking traffic therefore cannot accidentally satisfy a real command/GI transaction or become live process telemetry.

---

## 8. CP56Time2a

`TagValue.Timestamp` remains the EliteSCADA arrival/publication time.

A valid CP56Time2a becomes `SourceTimestamp` using the configured station timezone.

Important corrected contract from the research errata:

- minute bit 7 = IV (invalid time);
- hour bit 7 = SU (summer-time/daylight-saving indication);
- CP56 has no substituted-time flag;
- SU alone does not lower process quality;
- IV suppresses `SourceTimestamp` rather than fabricating a timestamp;
- process-value SB remains a separate SIQ/DIQ/QDS quality bit.

---

## 9. Quality mapping

Current mapping precedence:

- transport/session communication failure -> `BadCommunication` through the future owning public-driver path;
- IEC IV -> `BadDevice`;
- IEC NT -> `Stale`;
- IEC SB/BL/OV -> `Uncertain`;
- semantic uncertainty (for example indeterminate double point or non-finite short float) -> `Uncertain` when no stronger IEC quality applies;
- healthy value -> `Good`;
- binding/type mismatch -> future public path `BadConfiguration`;
- disabled binding -> future public path `Disabled`.

Public bound-TAG communication quality is Coordinator-gated because the final persisted/public driver integration does not yet exist on this parked branch.

---

## 10. Supported commands

First-release command matrix:

| Type ID | IEC name | Operation |
|---:|---|---|
| 45 | `C_SC_NA_1` | single command |
| 46 | `C_DC_NA_1` | double command |
| 48 | `C_SE_NA_1` | normalized setpoint |
| 49 | `C_SE_NB_1` | scaled setpoint |
| 50 | `C_SE_NC_1` | short-float setpoint |

Modes:

- Direct Operate;
- Select-Before-Operate.

Time-tagged control commands are deferred.

### 10.1 Correlation

Command responses correlate on:

- Type ID;
- Common Address;
- Originator Address;
- IOA;
- encoded value/qualifier semantics;
- select/execute state.

TEST-marked command confirmation does not advance an operational transaction.

### 10.2 Safety outcomes

Public Driver 06 internal command outcomes:

- `Accepted`;
- `Completed`;
- `Rejected`;
- `TimedOut`;
- `Ambiguous`;
- `Cancelled`.

Notable rules:

- missing ACT_CON after execute is `Ambiguous`, not a safe timeout;
- disconnect after positive ACT_CON but before ACT_TERM is `Ambiguous` with accepted state retained;
- selection timeout/rejection/cancellation before execute is safe and does not transmit physical execute;
- same CA+IOA allows only one in-flight command;
- global command concurrency is bounded;
- overload rejects instead of silently queueing controls;
- reconnect does not replay commands.

---

## 11. Managed client

`Iec104ManagedClient` owns the long-lived reconnect lifecycle.

Each reconnect uses a fresh adapter/session. This is intentional: a failed session does not attempt to resurrect or continue the previous sequence domain.

Managed diagnostics retain reconnect/failure and command outcome information while allowing current transport diagnostics to be absent between sessions.

---

## 12. Diagnostics

The private transport diagnostics include:

- connected/data-transfer state;
- next send sequence;
- oldest unacknowledged send sequence;
- expected receive sequence;
- unacknowledged send count;
- pending receive acknowledgement count;
- connection/disconnection counts;
- I/S/U sent/received counts;
- ASDU sent/received counts;
- STARTDT/STOPDT/TESTFR counters;
- T0/T1/T2/T3 counters;
- protocol errors;
- session failures;
- bounded/sanitized last-failure message;
- activity/send/receive timestamps.

Managed diagnostics add reconnect/session/command-level information and COT TEST isolation evidence.

Raw APDU/ASDU payloads are not copied into diagnostic strings.

---

## 13. Engineering connection test

`Iec104EngineeringConnectionTester` implements the common `ICommunicationDriverConnectionTester` contract.

Descriptor includes:

- driver type `iec60870.5.104`;
- runtime capabilities Read, Write, Subscribe, Diagnostics, SourceTimestamp;
- Engineering ConnectionTest;
- EventDriven/Hybrid acquisition;
- Data Source fields for host, port, Common Addresses, station timezone, OA, timers and K/W.

The connection test:

1. validates Data Source settings;
2. connects TCP;
3. performs STARTDT;
4. records transport information when available;
5. performs STOPDT;
6. disconnects.

It is diagnostic/Engineering evidence and does not become runtime authority.

`TagBindingFields` remains intentionally empty pending the coordinated rich binding design.

---

## 14. Engineering bounded Observe/Browse

IEC-104 does not provide a universal server-side point browse equivalent to OPC UA browsing.

Driver 06 therefore implements bounded observation evidence:

- connect;
- STARTDT;
- issue GI for configured CAs;
- observe supported monitored ASDUs for a bounded window;
- group evidence by CA+IOA;
- record observed Type IDs and conflict state;
- expose transient partial candidates;
- never persist or apply TAGs directly.

The browser:

- implements `ICommunicationDriverBrowser`;
- is flat, not a fake hierarchy;
- returns identity `ca=<CA>;ioa=<IOA>`;
- is always partial;
- reports GI incomplete/rejected/candidate-cap warnings;
- rejects continuation tokens because observation pages are not a stable remote namespace;
- does not claim writeability for observed candidates without a command profile.

---

## 15. Engineering point-list CSV Import/Export

### Import

`Iec104PointListImporter` consumes monitored-point CSV rows with required columns:

- `commonAddress`;
- `informationObjectAddress`;
- `typeId`.

Optional:

- `displayName`.

Supported type notation:

- numeric Type ID;
- internal enum name;
- standard IEC name for the supported monitored matrix.

Command types are rejected by this monitored-point importer.

Rows are aggregated during streaming by CA+IOA rather than buffering every imported row.

Resource defaults:

- `maximumRows = 100000`;
- `maximumLineLength = 65536` characters;
- `maximumFileBytes = 16 MiB`.

Hard caps:

- rows: 1,000,000;
- line length: 1,048,576 characters;
- bytes: 256 MiB.

The byte bound is enforced for seekable and non-seekable streams. NUL characters are rejected.

Source line metadata is bounded to the first 64 line numbers while retaining total row count.

Multiline quoted CSV fields are intentionally unsupported by this physical-line parser.

### Export

`Iec104PointListExporter` is protocol-local because there is no current shared communication-driver file-export interface.

It:

- exports monitored candidates only;
- emits one row per Type ID;
- sorts deterministically by CA/IOA/Type ID;
- uses standard IEC Type names;
- escapes commas/quotes;
- rejects CR/LF in display names to preserve roundtrip with the line-oriented importer.

---

## 16. Engineering Reconcile

`Iec104EngineeringReconciler` classifies a portable address using fresh bounded observation evidence.

Possible results:

- `Unchanged` — point observed with no Type ID conflict;
- `Missing` — point absent only after complete configured GI with no candidate truncation;
- `Ambiguous` — type conflict or absence under incomplete/rejected/truncated observation;
- `Unsupported` — CA not configured;
- `Error` — invalid address or Engineering/browser error.

It does not mutate canonical configuration.

Binding comparison metadata is explicitly `caIoaOnly` because the final rich semantic/command binding is not yet shared.

---

## 17. Unified Engineering surface

`Iec104EngineeringServices` implements:

- `ICommunicationDriverConnectionTester`;
- `ICommunicationDriverBrowser`;
- `ICommunicationDriverFileImporter`;
- `ICommunicationDriverReconciler`.

The descriptor advertises:

- ConnectionTest;
- Browse;
- FileImport;
- Reconcile.

This service is ready for Coordinator-side registration once the common composition root and public binding decisions are resolved.

---

## 18. Exact source inventory

Driver 06 source files under `src/Scada.Drivers/Iec60870`:

1. `IIec104ClientAdapter.cs`
2. `Iec104Apci.cs`
3. `Iec104AsduPrimitives.cs`
4. `Iec104ClientSessionRunner.cs`
5. `Iec104CommandCoordinator.cs`
6. `Iec104CommandTransaction.cs`
7. `Iec104Cp56Time2a.cs`
8. `Iec104Diagnostics.cs`
9. `Iec104EngineeringConnectionTester.cs`
10. `Iec104EngineeringProvider.cs`
11. `Iec104EngineeringReconciler.cs`
12. `Iec104EngineeringServices.cs`
13. `Iec104GeneralInterrogation.cs`
14. `Iec104InformationObjectDecoder.cs`
15. `Iec104ManagedClient.cs`
16. `Iec104ObservationCollector.cs`
17. `Iec104PointListExporter.cs`
18. `Iec104PointListImporter.cs`
19. `Iec104PortablePointAddress.cs`
20. `Iec104Reconnect.cs`
21. `Iec104SequenceState.cs`
22. `Iec104SessionPrimitives.cs`
23. `Iec104TcpClientAdapter.cs`
24. `Iec104TransmissionException.cs`
25. `Iec104TransportDiagnostics.cs`

Research/acceptance docs at the implementation handoff baseline:

1. `IEC-60870-5-104-RESEARCH.md`
2. `IEC-60870-5-104-RESEARCH-ERRATA.md`
3. `IEC-60870-5-104-ACCEPTANCE-AND-INTEROPERABILITY.md`

Post-handoff lab-preparation docs may exist at later branch HEADs. Review the current branch tree, not only this baseline inventory.

---

## 19. Exact test inventory

IEC-104 test files at the implementation handoff baseline:

1. `Iec104ApciCodecTests.cs`
2. `Iec104AsduPrimitivesTests.cs`
3. `Iec104ClientSessionRunnerTests.cs`
4. `Iec104CommandCoordinatorTests.cs`
5. `Iec104CommandTransactionTests.cs`
6. `Iec104CommandTransmissionAmbiguityTests.cs`
7. `Iec104Cp56Time2aTests.cs`
8. `Iec104DiagnosticsTests.cs`
9. `Iec104EngineeringConnectionTesterTests.cs`
10. `Iec104EngineeringProviderTests.cs`
11. `Iec104EngineeringReconcilerTests.cs`
12. `Iec104EngineeringServicesTests.cs`
13. `Iec104GeneralInterrogationTests.cs`
14. `Iec104InformationObjectDecoderTests.cs`
15. `Iec104ManagedClientTests.cs`
16. `Iec104ManagedInflightCommandReconnectTests.cs`
17. `Iec104ManagedTestCotDiagnosticsTests.cs`
18. `Iec104NonFiniteShortFloatTests.cs`
19. `Iec104ObservationCollectorTests.cs`
20. `Iec104PointListExporterTests.cs`
21. `Iec104PointListImporterBoundsTests.cs`
22. `Iec104PointListImporterTests.cs`
23. `Iec104PortablePointAddressTests.cs`
24. `Iec104ProtocolConformanceTests.cs`
25. `Iec104ReceiveQueueBoundsTests.cs`
26. `Iec104ReconnectTests.cs`
27. `Iec104SequenceStateTests.cs`
28. `Iec104SessionCommandRoutingTests.cs`
29. `Iec104SessionPrimitivesTests.cs`
30. `Iec104TcpClientAdapterTests.cs`
31. `Iec104TcpFaultInjectionTests.cs`
32. `Iec104TestCotIsolationTests.cs`
33. `Iec104UnsupportedAsduIsolationTests.cs`

---

## 20. Test coverage highlights

Tests are written for:

- I/S/U codec and malformed frames;
- exact APCI conformance vectors;
- N(S)/N(R) sequence validation and wrap;
- send/receive windows;
- STARTDT/STOPDT/TESTFR;
- T1/T2/T3 behavior;
- TCP loopback fault injection;
- partial APDU EOF;
- impossible ACK;
- out-of-order I frame;
- receive queue overflow;
- ASDU header/COT parsing;
- TEST-bit isolation;
- GI state machine;
- monitored decoder including SQ0/SQ1;
- IOA overflow;
- CP56Time2a and DST/IV/reserved bits;
- quality mapping;
- non-finite short-float policy;
- command transaction correlation;
- Direct/SBO safety;
- rejection/timeout/ambiguity;
- no command replay after reconnect;
- managed diagnostics;
- Engineering connection test;
- bounded observation;
- browser behavior;
- import/export/reconcile;
- CSV resource limits;
- unsupported ASDU isolation.

---

## 21. Test execution result — critical limitation

**No Driver 06 .NET test has been executed in the current development environment.**

At handoff preparation time:

- no `dotnet` executable was available;
- no `csc` executable was available;
- no `mcs` executable was available;
- no `/usr/share/dotnet` installation was present;
- branch HEAD had no CI status checks attached;
- the available GitHub connector did not expose workflow dispatch.

Therefore the correct evidence statement is:

> Tests are written and have been statically reviewed, but compilation and runtime execution remain pending.

Do not rewrite this as “tests passing” until actual .NET/CI evidence exists.

---

## 22. Known static/implementation risks to validate during first build

Because the suite has not been compiled, first .NET execution must pay special attention to:

- modern .NET TCP cancellation overload availability;
- xUnit overload compatibility;
- async-enumerable cancellation/configure-await syntax;
- record/nullable/warnings-as-errors behavior;
- `TimeZoneInfo` overload behavior;
- concurrency/disposal behavior around command coordinator shutdown;
- timer fault-test stability under slow CI scheduling;
- live adapter sequence/window behavior under real sequence wrap;
- receive queue pressure under sustained load;
- concrete socket write-failure ambiguity injection;
- long-running reconnect/task/socket leak behavior.

No broad workaround should be introduced until an actual compile/runtime failure demonstrates it is necessary.

---

## 23. External interoperability still required

Before production acceptance, run the acceptance matrix against at least:

1. a lib60870.NET or lib60870-C external station/reference implementation;
2. an independent IEC-104 simulator/stack from a different implementation family;
3. one representative real RTU/IED/utility gateway.

Required external evidence includes:

- TCP startup;
- STARTDT/STOPDT/TESTFR;
- GI;
- spontaneous indications;
- all approved monitored Type IDs available on the peer;
- CP56 timestamps;
- quality flags;
- Direct command success/rejection;
- SBO success/rejection;
- network interruption during control;
- no command replay after reconnect;
- high event-rate/burst behavior;
- long idle behavior;
- multiple Common Addresses when peer supports them;
- soak/resource behavior.

The branch now also contains a dedicated interoperability lab playbook and result template at later HEADs to make these runs repeatable.

---

## 24. Production library/license decision

Research identified lib60870.NET as a strong interoperability/reference candidate and lib60870-C as another external reference peer.

lib60870 is GPLv3 in its public distribution and commercial licensing is available.

Driver 06 deliberately did **not** add the GPL package as a normal production dependency.

Before production integration, the Coordinator/project owner must choose one of:

1. obtain and validate an appropriate commercial license for the selected external stack;
2. retain the current narrow EliteSCADA-owned CS104 implementation and validate it thoroughly;
3. adopt another implementation with acceptable licensing after technical/legal review.

The public GPL package must not silently enter proprietary production output merely because it was convenient in the lab.

---

## 25. Deferred protocol scope

Not first-release blockers:

- `M_ST_*` step position;
- `M_IT_*` integrated totals/BCR;
- time-tagged control commands;
- file transfer;
- IEC 60870-5-7 secure authentication;
- IEC 62351-3 TLS transport;
- redundancy groups;
- full vendor-specific/extended ASDU universe.

These require explicit later scope rather than accidental partial support.

---

## 26. Coordinator/shared decisions required for integration

### 26.1 Rich canonical TAG binding

Need shared schema for at least:

- Common Address;
- IOA;
- expected monitored semantic family/type profile;
- command type/profile where writable;
- Direct vs SBO mode;
- setpoint scaling/range semantics where needed.

The persisted binding must not rely on display-name strings or only `ca;ioa` if type/command compatibility is required for safe runtime behavior.

### 26.2 Public command outcome model

Decide whether the common runtime/API should expose telecontrol-aware outcomes such as:

- Accepted;
- Completed;
- Rejected;
- Ambiguous;
- TimedOut;
- Cancelled.

A generic boolean write result is insufficient to express an ambiguous physical control outcome safely.

### 26.3 Station timezone

Decide whether IEC-104 Data Sources always store station timezone explicitly or may deterministically inherit a canonical project/site timezone.

Import/export/package behavior must preserve the effective policy.

### 26.4 Driver registration/composition

Register:

- public runtime `ICommunicationDriver` once binding exists;
- Engineering unified services;
- common diagnostics source.

Driver 06 intentionally did not edit DriverHost composition from the parked branch.

### 26.5 Canonical Preview/Apply persistence

Browse/Import/Reconcile output must feed the existing candidate -> validate -> preview -> merge -> apply authority flow.

Driver 06 does not directly persist observed/imported candidates.

### 26.6 TLS/certificate/trust infrastructure

IEC 62351-3 support must consume host-owned common certificate/trust resolution rather than a protocol-private secret store.

### 26.7 Common export mechanism

Point-list export exists as a protocol-local utility because the current common Engineering contracts expose FileImport but no generic file-export feature interface.

Coordinator may keep export protocol-local or introduce a shared export contract during broader integration.

---

## 27. Integration sequence recommended to Coordinator

When shared integration is authorized:

1. rebase/merge Driver 06 onto the selected integration baseline under Coordinator ownership;
2. compile/run all Driver 06 tests on .NET 10;
3. fix only demonstrated compatibility/build failures;
4. resolve rich binding schema;
5. implement/register public `ICommunicationDriver` using existing private IEC-104 layers;
6. route monitored CA+IOA+semantic evidence into canonical TAG publication;
7. type mismatch -> `BadConfiguration` for the affected binding rather than reinterpreting data;
8. communication loss -> canonical `BadCommunication` through owning-provider logic;
9. route writes through command profile and command coordinator;
10. map protocol diagnostics into the common `CommunicationDriverDiagnosticSnapshot`;
11. register unified Engineering services;
12. integrate Browse/Import/Reconcile with canonical Preview/Apply;
13. run common TAG/Gateway/import-export/project-package tests;
14. execute independent simulator acceptance;
15. execute representative real-device acceptance;
16. make final production implementation/licensing decision;
17. only then propose mainline integration.

---

## 28. No unassigned/mainline changes

Driver 06 work was confined to:

- its authorized IEC-104 research/acceptance documentation;
- `src/Scada.Drivers/Iec60870` private protocol/Engineering implementation;
- IEC-104-specific tests under `tests/Scada.Drivers.Tests`;
- later IEC-104 lab-preparation documentation on the same authorized branch.

No intentional changes were made directly to `main` by Driver 06.

No shared public contract was modified merely to satisfy IEC-104.

No automatic merge/rebase to main was performed.

---

## 29. Handoff readiness statement

The Driver 06 branch is ready for **Coordinator review and executable validation**, not for a claim of production completion.

The private IEC-104 protocol stack, managed reconnect lifecycle, monitored decode, command safety model, Engineering transient workflows, diagnostics and test suite are materially implemented.

The remaining gates are deliberately visible:

- .NET 10 build/test execution;
- independent software-peer interoperability;
- real RTU/IED/gateway validation;
- shared public rich binding;
- public runtime provider/DriverHost registration;
- common Preview/Apply persistence;
- common command-outcome integration;
- production implementation/license decision;
- later TLS/IEC 62351-3 integration.

Those gates should be resolved centrally rather than hidden by protocol-local shortcuts.
