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
- k: 12;
- w: 8.

Validation requires `T2 < T1`, valid positive timers and valid IEC sequence/window ranges.

Reconnect uses a deterministic backoff policy. The research contract recommends 1, 2, 5, 10 and 30 second delays capped at 30 seconds. A fresh session performs STARTDT and GI. Operational commands are never automatically replayed after a failed session.

### 2.3 First-release monitored Type IDs

Implemented monitored indication matrix:

| IEC Type | Type ID | EliteSCADA value |
|---|---:|---|
| `M_SP_NA_1` | 1 | Boolean |
| `M_DP_NA_1` | 3 | Enum / four double-point states |
| `M_BO_NA_1` | 7 | Int32 bitstring |
| `M_ME_NA_1` | 9 | Float normalized value |
| `M_ME_NB_1` | 11 | Int16 |
| `M_ME_NC_1` | 13 | Float |
| `M_SP_TB_1` | 30 | Boolean + CP56Time2a |
| `M_DP_TB_1` | 31 | Enum + CP56Time2a |
| `M_BO_TB_1` | 33 | Int32 bitstring + CP56Time2a |
| `M_ME_TD_1` | 34 | Float normalized value + CP56Time2a |
| `M_ME_TE_1` | 35 | Int16 + CP56Time2a |
| `M_ME_TF_1` | 36 | Float + CP56Time2a |

`M_ST_*` step position and `M_IT_*` integrated totals/BCR are deferred pending canonical representation decisions.

Sequential (`SQ=1`) and explicit (`SQ=0`) object addressing are supported for the monitored matrix. Sequential IOA overflow beyond 24 bits is rejected rather than wrapped.

Unknown/unsupported Type IDs are not converted into process updates or Engineering candidates.

### 2.4 General Interrogation

GI uses `C_IC_NA_1`, Cause of Transmission Activation and QOI 20 per configured Common Address.

The GI state machine tracks activation confirmation, GI data and activation termination per CA. Incomplete or rejected GI remains visible to Engineering reconciliation so absence is not falsely promoted to a definitive Missing result.

TEST-marked GI control traffic does not advance a real operational GI transaction.

### 2.5 Quality mapping

Current precedence is:

1. communication/session loss -> `BadCommunication` through the future owning-provider/common runtime path;
2. IEC IV process-quality bit -> `BadDevice`;
3. IEC NT -> `Stale`;
4. SB / BL / OV -> `Uncertain`;
5. semantic uncertainty, such as indeterminate double-point state or non-finite short float -> `Uncertain` when no higher-priority quality applies;
6. healthy process value -> `Good`;
7. binding/type mismatch in the future public driver -> `BadConfiguration`;
8. disabled TAG -> `Disabled`.

For `M_ME_NC_1` / `M_ME_TF_1`, IEEE-754 `NaN` and positive/negative Infinity are preserved as values but downgraded to `Uncertain`. An IEC IV quality bit still outranks semantic uncertainty and maps to `BadDevice`.

### 2.6 CP56Time2a

`TagValue.Timestamp` remains the EliteSCADA arrival/publication time.

A valid CP56Time2a becomes `SourceTimestamp` using the configured station timezone. Invalid CP56 evidence does not fabricate a source timestamp.

Authoritative errata for the research document:

- minute bit 7 is IV;
- hour bit 7 is SU;
- CP56Time2a has no substituted-time flag;
- SU alone does not lower TAG quality;
- SB remains a process-value quality flag in SIQ/DIQ/QDS and maps to `Uncertain`.

### 2.7 Command support

Implemented command Type IDs:

| IEC Type | Type ID | Initial support |
|---|---:|---|
| `C_SC_NA_1` | 45 | Single command |
| `C_DC_NA_1` | 46 | Double command |
| `C_SE_NA_1` | 48 | Normalized setpoint |
| `C_SE_NB_1` | 49 | Scaled setpoint |
| `C_SE_NC_1` | 50 | Short-float setpoint |

Time-tagged command variants are deferred.

Both Direct Operate and Select-Before-Operate are implemented. SBO requires successful selection confirmation before execute is sent.

Command responses are correlated using Type ID, Common Address, Originator Address, IOA, request payload/select semantics and Cause of Transmission. TEST-marked command responses cannot satisfy a real operational command.

Public result vocabulary on this branch:

- `Accepted`;
- `Completed`;
- `Rejected`;
- `TimedOut`;
- `Ambiguous`;
- `Cancelled`.

A write failure after the transport has reserved an I-frame sequence number is surfaced as `Iec104AmbiguousTransmissionException`. The caller must not infer that the controlled station did not receive or execute the command.

Commands transmitted before a session loss are never replayed on the next connection.

---

## 3. Engineering surface delivered

The branch provides a unified private Engineering surface implementing the existing common feature interfaces without changing common contracts.

### 3.1 Connection test

`Iec104EngineeringConnectionTester` validates configuration, opens TCP, performs STARTDT, records available transport evidence, then performs STOPDT/disconnect best effort.

Current Data Source fields include:

- host;
- port;
- comma-separated Common Addresses;
- station timezone;
- Originator Address;
- T0/T1/T2/T3;
- k/w.

`TagBindingFields` is intentionally empty until the Coordinator approves a rich IEC-104 binding schema.

### 3.2 Bounded Observe/GI browse

IEC-104 does not expose a generic standard station-wide metadata browse comparable to OPC UA browse.

The implemented Engineering browser therefore performs bounded Observe+GI candidate capture and always reports partial evidence. It does not claim complete topology discovery.

Candidate identity is represented portably as:

`ca=<0..65535>;ioa=<0..16777215>`

Candidates contain observed Type IDs, suggested canonical data type, last observed value/quality/source timestamp/COT, observation count and type-conflict evidence.

TEST-marked process ASDUs are ignored and never become Engineering candidates.

No candidate is persisted or applied automatically.

### 3.3 Reconcile

Reconciliation is CA+IOA only at this stage because the future persisted binding schema does not yet contain semantic family/command profile.

Outcomes:

- invalid portable address -> Error;
- unconfigured CA -> Unsupported;
- observed point with compatible evidence -> Unchanged;
- observed type conflict -> Ambiguous;
- absent while GI evidence is incomplete/rejected/truncated -> Ambiguous;
- absent after complete configured GI without truncation -> Missing.

Reconcile performs no canonical mutation.

### 3.4 CSV point-list import/export

The protocol-local CSV shape is:

`commonAddress,informationObjectAddress,typeId,displayName`

Import accepts the first-release monitored matrix only. Command Type IDs are rejected from monitored point-list import.

Resource bounds are enforced by default:

- maximum rows: 100,000, hard cap 1,000,000;
- maximum physical line length: 65,536 characters, hard cap 1,048,576;
- maximum file bytes: 16 MiB, hard cap 256 MiB;
- source line metadata retains the first 64 source-line numbers per collapsed point.

The byte limit is enforced for seekable and non-seekable streams.

Importer grouping is performed while reading instead of buffering every CSV row.

The exporter is protocol-local because the current common Engineering contracts do not expose a generic driver file-export interface. Export is deterministic by CA/IOA/Type ID and deliberately rejects CR/LF in display names because the importer is physical-line-oriented and does not support multiline CSV records.

---

## 4. Diagnostics delivered

The private diagnostics surface captures transport/session/protocol evidence including:

- TCP connected/data-transfer state;
- N(S), oldest unacknowledged send sequence and expected receive sequence;
- unacknowledged send count;
- pending receive acknowledgement count;
- connection/disconnection counters;
- I/S/U sent/received counters;
- ASDU sent/received counters;
- STARTDT/STOPDT/TESTFR counters;
- T0/T1/T2/T3 events;
- protocol errors and session failures;
- sanitized last failure;
- last activity/frame timestamps;
- reconnect/session state;
- GI state;
- observed point updates;
- ignored TEST process ASDUs;
- command requested/outcome counters;
- current active transport snapshot where available.

Receive buffering is bounded at 1024 ASDUs. Overflow closes the session with an explicit protocol failure instead of dropping process data silently.

Diagnostics intentionally avoid raw payload capture, secrets and unbounded per-point metric labels.

---

## 5. Exact implementation files

### 5.1 Research / validation documents

- `docs/research/iec-60870-5-104/IEC-60870-5-104-RESEARCH.md`
- `docs/research/iec-60870-5-104/IEC-60870-5-104-RESEARCH-ERRATA.md`
- `docs/research/iec-60870-5-104/IEC-60870-5-104-ACCEPTANCE-AND-INTEROPERABILITY.md`
- `docs/research/iec-60870-5-104/IEC-60870-5-104-DRIVER-HANDOFF.md`

### 5.2 Driver source files

- `src/Scada.Drivers/Iec60870/IIec104ClientAdapter.cs`
- `src/Scada.Drivers/Iec60870/Iec104Apci.cs`
- `src/Scada.Drivers/Iec60870/Iec104AsduPrimitives.cs`
- `src/Scada.Drivers/Iec60870/Iec104ClientSessionRunner.cs`
- `src/Scada.Drivers/Iec60870/Iec104CommandCoordinator.cs`
- `src/Scada.Drivers/Iec60870/Iec104CommandTransaction.cs`
- `src/Scada.Drivers/Iec60870/Iec104Cp56Time2a.cs`
- `src/Scada.Drivers/Iec60870/Iec104Diagnostics.cs`
- `src/Scada.Drivers/Iec60870/Iec104EngineeringConnectionTester.cs`
- `src/Scada.Drivers/Iec60870/Iec104EngineeringProvider.cs`
- `src/Scada.Drivers/Iec60870/Iec104EngineeringReconciler.cs`
- `src/Scada.Drivers/Iec60870/Iec104EngineeringServices.cs`
- `src/Scada.Drivers/Iec60870/Iec104GeneralInterrogation.cs`
- `src/Scada.Drivers/Iec60870/Iec104InformationObjectDecoder.cs`
- `src/Scada.Drivers/Iec60870/Iec104ManagedClient.cs`
- `src/Scada.Drivers/Iec60870/Iec104ObservationCollector.cs`
- `src/Scada.Drivers/Iec60870/Iec104PointListExporter.cs`
- `src/Scada.Drivers/Iec60870/Iec104PointListImporter.cs`
- `src/Scada.Drivers/Iec60870/Iec104PortablePointAddress.cs`
- `src/Scada.Drivers/Iec60870/Iec104Reconnect.cs`
- `src/Scada.Drivers/Iec60870/Iec104SequenceState.cs`
- `src/Scada.Drivers/Iec60870/Iec104SessionPrimitives.cs`
- `src/Scada.Drivers/Iec60870/Iec104TcpClientAdapter.cs`
- `src/Scada.Drivers/Iec60870/Iec104TransmissionException.cs`
- `src/Scada.Drivers/Iec60870/Iec104TransportDiagnostics.cs`

### 5.3 Test files

- `tests/Scada.Drivers.Tests/Iec104ApciCodecTests.cs`
- `tests/Scada.Drivers.Tests/Iec104AsduPrimitivesTests.cs`
- `tests/Scada.Drivers.Tests/Iec104ClientSessionRunnerTests.cs`
- `tests/Scada.Drivers.Tests/Iec104CommandCoordinatorTests.cs`
- `tests/Scada.Drivers.Tests/Iec104CommandTransactionTests.cs`
- `tests/Scada.Drivers.Tests/Iec104CommandTransmissionAmbiguityTests.cs`
- `tests/Scada.Drivers.Tests/Iec104Cp56Time2aTests.cs`
- `tests/Scada.Drivers.Tests/Iec104DiagnosticsTests.cs`
- `tests/Scada.Drivers.Tests/Iec104EngineeringConnectionTesterTests.cs`
- `tests/Scada.Drivers.Tests/Iec104EngineeringProviderTests.cs`
- `tests/Scada.Drivers.Tests/Iec104EngineeringReconcilerTests.cs`
- `tests/Scada.Drivers.Tests/Iec104EngineeringServicesTests.cs`
- `tests/Scada.Drivers.Tests/Iec104GeneralInterrogationTests.cs`
- `tests/Scada.Drivers.Tests/Iec104InformationObjectDecoderTests.cs`
- `tests/Scada.Drivers.Tests/Iec104ManagedClientTests.cs`
- `tests/Scada.Drivers.Tests/Iec104ManagedInflightCommandReconnectTests.cs`
- `tests/Scada.Drivers.Tests/Iec104ManagedTestCotDiagnosticsTests.cs`
- `tests/Scada.Drivers.Tests/Iec104NonFiniteShortFloatTests.cs`
- `tests/Scada.Drivers.Tests/Iec104ObservationCollectorTests.cs`
- `tests/Scada.Drivers.Tests/Iec104PointListExporterTests.cs`
- `tests/Scada.Drivers.Tests/Iec104PointListImporterBoundsTests.cs`
- `tests/Scada.Drivers.Tests/Iec104PointListImporterTests.cs`
- `tests/Scada.Drivers.Tests/Iec104PortablePointAddressTests.cs`
- `tests/Scada.Drivers.Tests/Iec104ProtocolConformanceTests.cs`
- `tests/Scada.Drivers.Tests/Iec104ReceiveQueueBoundsTests.cs`
- `tests/Scada.Drivers.Tests/Iec104ReconnectTests.cs`
- `tests/Scada.Drivers.Tests/Iec104SequenceStateTests.cs`
- `tests/Scada.Drivers.Tests/Iec104SessionCommandRoutingTests.cs`
- `tests/Scada.Drivers.Tests/Iec104SessionPrimitivesTests.cs`
- `tests/Scada.Drivers.Tests/Iec104TcpClientAdapterTests.cs`
- `tests/Scada.Drivers.Tests/Iec104TcpFaultInjectionTests.cs`
- `tests/Scada.Drivers.Tests/Iec104TestCotIsolationTests.cs`
- `tests/Scada.Drivers.Tests/Iec104UnsupportedAsduIsolationTests.cs`

No common/core/mainline file was modified to implement the IEC-104 feature set.

---

## 6. Automated tests and current evidence

### 6.1 Test coverage written

There are 33 IEC-104-specific test files on the implementation baseline.

Coverage includes:

- APCI encoding/decoding and fixed binary vectors;
- sequence modulo and wrap-edge behavior;
- I/S/U session primitives;
- ASDU headers, COT and address primitives;
- CP56Time2a known vectors and malformed inputs;
- monitored information-object decode;
- quality semantics;
- non-finite short-float policy;
- GI state machine;
- command transaction/coordinator behavior;
- Direct and SBO flows;
- negative confirmations;
- missing confirmation/termination outcomes;
- transmission ambiguity classification;
- session command routing;
- TEST-COT isolation from command, GI, runtime telemetry and Engineering observations;
- managed-client reconnect behavior;
- in-flight command no-replay behavior;
- transport diagnostics aggregation;
- real loopback TCP STARTDT/STOPDT/TESTFR exchange;
- invalid peer acknowledgement;
- out-of-order N(S);
- partial APDU EOF;
- T1, T2 and T3 behavior;
- bounded receive queue overflow;
- unknown Type ID isolation;
- connection testing;
- Observe/GI browse;
- portable point addresses;
- CSV import/export;
- CSV resource bounds;
- reconcile semantics;
- deterministic conformance vectors.

### 6.2 Execution status

**No claim is made that these tests pass.**

At handoff time, the available execution environment did not provide `dotnet`, `csc` or `mcs`, and the branch HEAD had no CI status checks attached. Therefore the current evidence is:

- source written;
- tests written;
- static/manual review performed;
- tests **not compiled or executed** in this environment.

The first acceptance action must be a real .NET 10 restore/build/test run before integration conclusions are drawn.

### 6.3 Known compile-static areas to watch first

The implementation uses modern C#/NET APIs expected under the repository target but not yet compiled here, including:

- async iterators with cancellation/configure-await;
- collection expressions;
- record structs / `with` expressions;
- `Task.WaitAsync` overloads;
- modern `TcpClient`/`NetworkStream` cancellation APIs;
- `Stopwatch.GetElapsedTime`;
- xUnit 2.9.3 assertion overloads;
- `Stream.ReadAsync(Memory<byte>, CancellationToken)` overrides;
- nullable record/assertion combinations.

Treat compiler feedback as authoritative and fix branch-local issues before any semantic refactor.

---

## 7. Independent simulator and hardware validation still required

The authoritative validation plan is:

`IEC-60870-5-104-ACCEPTANCE-AND-INTEROPERABILITY.md`

Production acceptance still requires at minimum:

1. .NET 10 restore/build and all focused IEC-104 tests;
2. repeated loopback/fault-injection suite to eliminate timing flakiness;
3. an external reference station, such as lib60870-based station code;
4. a second independent simulator/stack from a different implementation family;
5. at least one representative real RTU/IED/utility gateway.

External validation must cover:

- STARTDT/STOPDT/TESTFR;
- long idle T3/TESTFR behavior;
- GI per CA;
- spontaneous/event indications;
- every supported first-release monitored Type ID;
- SQ=0 and SQ=1 where peer supports both;
- CP56 timestamps and DST/timezone edge cases;
- IEC quality bits;
- Direct commands;
- SBO commands;
- positive/negative confirmations;
- disconnect before and after command acceptance;
- confirmation/termination timeouts;
- multiple Common Addresses on one endpoint where available;
- burst/spontaneous load;
- k/w pressure;
- T1/T2 timing behavior;
- sequence wrap under sustained traffic;
- reconnect and GI bootstrap;
- proof that ambiguous commands are never replayed.

Capture packet traces or equivalent station logs for protocol-level acceptance cases where practical.

No hardware/simulator interoperability has been claimed on this branch yet.

---

## 8. Limitations, risks and deferred scope

### 8.1 No public persisted rich TAG binding yet

The branch does not invent a private persisted binding schema.

The intended future binding must preserve at least:

- Common Address;
- IOA;
- expected semantic/Type family;
- command profile where writable;
- Direct vs SBO mode;
- setpoint/scaling semantics where applicable.

Until that shared schema exists, Engineering portable candidate identity remains CA+IOA only and runtime/public TAG binding is intentionally not registered.

### 8.2 Public runtime registration is not implemented

There is no final public IEC-104 `ICommunicationDriver` registration in DriverHost on this branch.

This avoids bypassing the canonical provider catalog/composition and avoids freezing a binding model before Coordinator reconciliation.

### 8.3 `M_ST_*` and `M_IT_*` deferred

Step-position and integrated-total/BCR families are deferred because preserving transient/counter flags properly requires a deliberate canonical representation. They must not be shoehorned into `TagQuality` or discarded silently.

### 8.4 Time-tagged operational commands deferred

Time-tagged command variants are not part of this first release slice.

### 8.5 TLS / IEC 62351-3 deferred

Secure IEC-104 transport is not implemented. Future TLS support must use the host-owned common certificate/trust infrastructure rather than a protocol-private secret/certificate store.

### 8.6 Library/licensing decision remains open

The research evaluated `lib60870.NET`/lib60870 as an interoperability and potential implementation candidate. Public releases are GPLv3 and commercial licensing is available from the vendor.

This branch deliberately does not add the GPL package as a normal production dependency.

Before production adoption of that library, the project needs an explicit commercial licensing/redistribution/legal decision plus .NET 10 compatibility validation. The current narrow EliteSCADA-owned transport/protocol implementation remains the fallback/working implementation.

### 8.7 CSV format is deliberately physical-line oriented

Quoted commas and escaped quotes are supported. Multiline quoted CSV fields are not supported.

The exporter rejects CR/LF in display names so exported data remains importable by the current parser. Do not remove that guard unless the importer is upgraded to logical-record CSV parsing at the same time.

### 8.8 Diagnostics naming nuance

`TestAsdusIgnored` currently represents TEST-marked ASDUs ignored on the process-data path after command/GI routing. TEST command/GI responses are rejected by their respective transactions and are not necessarily counted by this process-data counter.

If the public diagnostics vocabulary is later normalized, consider renaming it to make that scope explicit or moving counting to a centralized ASDU ingress point.

### 8.9 Bootstrap command admission

The session runner may reach Running while initial GI activation dispatch is still being completed. The Coordinator should decide whether public command admission must wait for an explicit bootstrap-ready state.

### 8.10 Protocol COT hardening beyond current release

TEST is explicitly isolated. Monitored ASDUs carrying unusual negative-confirmation semantics should be checked against the normative IEC profile during external conformance validation before adding more protocol-specific rejection rules.

---

## 9. Coordinator/shared decisions required before mainline integration

The following decisions must be made centrally rather than by Driver 06 alone.

### 9.1 Rich IEC-104 binding schema

Approve a versioned public binding capable of representing:

- CA;
- IOA;
- semantic monitored family/expected Type profile;
- writable command type/profile;
- Direct/SBO mode;
- setpoint/scaling policy.

### 9.2 Common command outcome model

Decide whether `Accepted`, `Completed`, `Rejected`, `TimedOut`, `Ambiguous` and `Cancelled` become common runtime/API concepts or remain translated from IEC-specific results at the provider boundary.

Telecontrol ambiguity must not be flattened into generic success/failure in a way that encourages command replay.

### 9.3 Station timezone inheritance

Decide whether station timezone is always explicit on the IEC-104 Data Source or may inherit a canonical project/site timezone while retaining deterministic export/package behavior.

### 9.4 Engineering provider registration

Register `Iec104EngineeringServices` through the canonical DriverHost/provider catalog once the mainline composition model is ready. Do not add a one-off IEC registration path.

### 9.5 Canonical validate/preview/apply/persistence integration

Map imported/observed candidates into the normal Engineering candidate -> validate -> preview -> merge -> apply workflow after the binding schema is approved.

No current IEC Engineering operation should mutate canonical TAGs directly.

### 9.6 Common export mechanism

The branch has a protocol-local point-list exporter because no common driver file-export feature interface exists in the reviewed shared contract.

The Coordinator should decide whether a generic driver export interface is desirable or whether this stays as an Engineering service operation outside the current feature-interface set.

### 9.7 TLS certificate/trust resolver

Future IEC 62351-3/TLS support must use the same protected host-owned certificate/trust abstraction as other secure drivers.

### 9.8 Deferred data families

Decide canonical representations before enabling:

- `M_ST_*` step-position transient state;
- `M_IT_*` BCR/counter flags and sequence information.

### 9.9 Public runtime provider

After the decisions above, implement/register the public owning provider so:

- bound CA+IOA+family maps decoded points to canonical TAGs;
- type mismatch becomes `BadConfiguration` per point instead of corrupting a value;
- communication loss becomes `BadCommunication` through the common path;
- writes route through the owning provider and approved command profile;
- Gateway remains TAG-to-TAG only;
- common communication diagnostics surface the IEC private snapshot without leaking implementation-library types.

---

## 10. Integration order recommended to Coordinator

Recommended sequence after the branch receives an executable .NET environment:

1. run focused restore/build/test for `Scada.Drivers.Tests` and fix compile issues only;
2. run the IEC acceptance matrix tiers that do not require hardware;
3. resolve rich binding and common command-outcome decisions;
4. rebase/merge the parked branch only under Coordinator control because `main` has advanced substantially since the branch merge base;
5. implement the public IEC owning provider against the approved shared binding/runtime contracts;
6. register Engineering/runtime through canonical DriverHost composition;
7. run canonical project/package/import/export/Preview-Apply integration tests;
8. run independent external simulator interoperability;
9. run representative RTU/IED validation;
10. resolve production library/license and TLS/security decisions before declaring production-ready support.

Do not automatically rebase this parked branch before the Coordinator reviews the 64-commit mainline divergence and shared-contract evolution.

---

## 11. Branch hygiene / ownership statement

Driver 06 work was kept on:

`driver6/iec-60870-5-104`

No Driver 06 change was intentionally written to `main`.

No unrelated Driver DEV branch was modified.

No shared/common contract was changed merely to make IEC-104 implementation easier.

The branch is intentionally parked and is **not** self-authorized for merge into `main`.

---

## 12. Handoff conclusion

The private IEC 60870-5-104 slice is feature-substantial and has a broad deterministic test suite plus an explicit interoperability plan, but it is **not yet production-accepted**.

The most important remaining evidence is executable, not textual:

- compile under the repository's .NET 10 toolchain;
- pass the focused tests;
- validate against independent station implementations;
- validate against representative RTU/IED hardware;
- complete shared binding/runtime integration under Coordinator-approved contracts.

Until those steps occur, the correct status is:

**implementation and test design substantially complete; execution, integration and external conformance validation pending.**
