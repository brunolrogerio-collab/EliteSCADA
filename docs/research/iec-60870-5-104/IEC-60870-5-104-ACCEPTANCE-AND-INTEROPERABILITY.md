# IEC 60870-5-104 — Acceptance and Interoperability Runbook

Status: **DRIVER 6 LAB/HANDOFF CONTRACT — TESTS WRITTEN, EXECUTION PENDING**  
Branch owner: `driver6/iec-60870-5-104`  
Protocol role: EliteSCADA **client/master**  
Driver type: `iec60870.5.104`

This document converts the research acceptance criteria into a repeatable validation runbook. It is deliberately strict about evidence. A test existing in source control is not evidence that it passed, and an external peer accepting one connection is not evidence of interoperability.

## 1. Status vocabulary

Each acceptance case uses exactly one of these evidence states:

- **WRITTEN / NOT EXECUTED** — automated test exists in the branch but no .NET test runtime/CI result has executed it yet.
- **IMPLEMENTED / NO AUTOMATED CASE** — implementation exists, but a dedicated automated acceptance case is still missing.
- **SIMULATOR REQUIRED** — must be repeated against at least one external IEC-104 implementation.
- **INDEPENDENT SIMULATOR REQUIRED** — must also be repeated against an implementation family independent from the first reference peer.
- **HARDWARE REQUIRED** — must be demonstrated against a representative RTU, IED or utility gateway.
- **COORDINATOR BLOCKED** — depends on a shared EliteSCADA contract that Driver 6 is not authorized to define locally.
- **ACCEPTED** — may only be assigned after the required evidence is attached to the Driver 6 handoff.

No case in this document is currently marked `ACCEPTED` merely because code or tests exist.

## 2. Required validation tiers

### Tier A — deterministic local protocol tests

Purpose: prove codec, state-machine, transaction and mapping semantics without real hardware.

Expected command once a .NET 10 SDK is available:

```text
dotnet test tests/Scada.Drivers.Tests/Scada.Drivers.Tests.csproj --filter "FullyQualifiedName~Iec104"
```

Focused transport/fault run:

```text
dotnet test tests/Scada.Drivers.Tests/Scada.Drivers.Tests.csproj --filter "FullyQualifiedName~Iec104Tcp"
```

Focused command run:

```text
dotnet test tests/Scada.Drivers.Tests/Scada.Drivers.Tests.csproj --filter "FullyQualifiedName~Iec104Command|FullyQualifiedName~Iec104ManagedInflightCommandReconnect"
```

Evidence to retain:

1. exact branch/commit SHA;
2. `dotnet --info` output;
3. exact test command;
4. pass/fail/skip counts;
5. TRX/JUnit artifact if CI/lab infrastructure supports it;
6. any failing test name and full failure reason.

### Tier B — external reference peer

Minimum first peer: lib60870.NET or lib60870-C station implementation, subject to license/redistribution constraints already documented in the research contract.

The reference peer must run out-of-process over a real loopback or LAN TCP socket. Reusing `IIec104ClientAdapter` fakes does not qualify.

### Tier C — independent implementation family

Use a second simulator/stack that is not merely another wrapper around the same underlying lib60870 implementation. The exact product may be selected by the lab/Coordinator, but its name, version and configuration must be recorded.

### Tier D — representative hardware

At least one real RTU/IED/utility gateway must be exercised before production acceptance. Record vendor/model, firmware, IEC-104 profile/options, CA/IOA test map and any device-specific restrictions. Credentials, private keys and protected configuration must not be committed to the repository.

## 3. Standard lab profile

Unless a peer requires otherwise, start with:

- TCP port: `2404`;
- COT length: 2 octets;
- Common Address length: 2 octets;
- IOA length: 3 octets;
- Originator Address: `0`;
- `t0 = 30 s`;
- `t1 = 15 s`;
- `t2 = 10 s`;
- `t3 = 20 s`;
- `k = 12`;
- `w = 8`;
- GI: `C_IC_NA_1`, QOI `20`;
- one baseline CA and, when supported by the peer, a second CA on the same TCP session;
- station timezone explicitly configured for CP56Time2a tests.

Short timer values may be used in deterministic fault tests, but evidence must record the actual values.

## 4. Supported first-release monitored matrix

These types are in Driver 6 scope and therefore require external interoperability evidence:

| Type ID | IEC name | EliteSCADA value | Timestamp |
|---:|---|---|---|
| 1 | `M_SP_NA_1` | Boolean | arrival/publication |
| 30 | `M_SP_TB_1` | Boolean | CP56Time2a when valid |
| 3 | `M_DP_NA_1` | Enum | arrival/publication |
| 31 | `M_DP_TB_1` | Enum | CP56Time2a when valid |
| 7 | `M_BO_NA_1` | Int32 bitstring | arrival/publication |
| 33 | `M_BO_TB_1` | Int32 bitstring | CP56Time2a when valid |
| 9 | `M_ME_NA_1` | Float normalized | arrival/publication |
| 34 | `M_ME_TD_1` | Float normalized | CP56Time2a when valid |
| 11 | `M_ME_NB_1` | Int16 | arrival/publication |
| 35 | `M_ME_TE_1` | Int16 | CP56Time2a when valid |
| 13 | `M_ME_NC_1` | Float | arrival/publication |
| 36 | `M_ME_TF_1` | Float | CP56Time2a when valid |

`M_ST_*` and `M_IT_*` remain deferred pending explicit shared semantic decisions and are not acceptance blockers for the first Driver 6 slice.

## 5. Supported first-release command matrix

| Type ID | IEC name | Operation | Modes |
|---:|---|---|---|
| 45 | `C_SC_NA_1` | single command | Direct, SBO |
| 46 | `C_DC_NA_1` | double command | Direct, SBO |
| 48 | `C_SE_NA_1` | normalized setpoint | Direct, SBO |
| 49 | `C_SE_NB_1` | scaled setpoint | Direct, SBO |
| 50 | `C_SE_NC_1` | short-float setpoint | Direct, SBO |

Time-tagged control commands are deferred from the first slice.

## 6. Acceptance matrix

### 6.1 APCI, TCP lifecycle and supervision

| ID | Requirement | Automated evidence in branch | External evidence | Current state |
|---|---|---|---|---|
| IEC104-A001 | TCP connect and successful `STARTDT act/con` | `Iec104TcpClientAdapterTests.Adapter_ExchangesStartDataInformationAcknowledgementAndStopFrames` | Tier B/C/D connect trace | WRITTEN / NOT EXECUTED |
| IEC104-A002 | graceful `STOPDT act/con` | `Iec104TcpClientAdapterTests` basic exchange | Tier B/C/D disconnect trace | WRITTEN / NOT EXECUTED |
| IEC104-A003 | remote `TESTFR act` receives `TESTFR con` | `Iec104TcpClientAdapterTests.Adapter_RepliesToRemoteTestFrameActivation` | Tier B/C/D | WRITTEN / NOT EXECUTED |
| IEC104-A004 | idle `t3` sends `TESTFR act`; missing confirmation fails under `t1` | `Iec104TcpFaultInjectionTests.Adapter_T3TestFrameWithoutConfirmationFailsUnderT1` | Tier B/C long-idle test | WRITTEN / NOT EXECUTED |
| IEC104-A005 | invalid APDU start/length/control rejected | `Iec104ApciCodecTests.ParseRejectsMalformedFrames` | optional negative Tier B/C injection | WRITTEN / NOT EXECUTED |
| IEC104-A006 | partial APDU/EOF faults transport without inventing a protocol error | `Iec104TcpFaultInjectionTests.Adapter_PartialApduEofFaultsSessionWithoutMisclassifyingProtocolError` | Tier B/C network cut | WRITTEN / NOT EXECUTED |
| IEC104-A007 | receive queue overflow closes session rather than silently dropping process data | bounded channel implementation | burst/fault fixture required | IMPLEMENTED / NO AUTOMATED CASE |

### 6.2 Sequence numbers, windows and timers

| ID | Requirement | Automated evidence in branch | External evidence | Current state |
|---|---|---|---|---|
| IEC104-A010 | I/S sequence progression | `Iec104SequenceStateTests`, `Iec104TcpClientAdapterTests` | Tier B/C traces | WRITTEN / NOT EXECUTED |
| IEC104-A011 | modulo-32768 wrap `32767 -> 0` | `Iec104SequenceStateTests.SequenceNumbersWrapAt32768`, `Iec104ProtocolConformanceTests.MaximumSequenceNumber_UsesFffeControlEncodingAndRoundTrips` | Tier B/C stress if peer permits | WRITTEN / NOT EXECUTED |
| IEC104-A012 | peer ACK cannot advance beyond outstanding frames | `Iec104SequenceStateTests.PeerAcknowledgementCannotAdvanceBeyondOutstandingFrames`, `Iec104TcpFaultInjectionTests.Adapter_InvalidPeerAcknowledgementFaultsSessionAsProtocolError` | Tier B/C malformed injection optional | WRITTEN / NOT EXECUTED |
| IEC104-A013 | unexpected inbound `N(S)` is rejected before ASDU publication | `Iec104SequenceStateTests.UnexpectedReceiveSequenceDoesNotConsumePeerAcknowledgement`, `Iec104TcpFaultInjectionTests.Adapter_OutOfOrderIFrameFaultsBeforePublishingAsdu` | Tier B/C malformed injection optional | WRITTEN / NOT EXECUTED |
| IEC104-A014 | `k` send window blocks further I-frames until ACK advances | `Iec104SequenceStateTests.SendWindowBlocksUntilPeerAcknowledgementAdvances` | Tier B/C burst | WRITTEN / NOT EXECUTED |
| IEC104-A015 | `w` threshold triggers receive acknowledgement | adapter basic exchange plus sequence tests | Tier B/C burst | WRITTEN / NOT EXECUTED |
| IEC104-A016 | `t1` supervises unacknowledged I-frame | `Iec104TcpFaultInjectionTests.Adapter_T1ExpiresWhenPeerNeverAcknowledgesOutstandingIFrame` | Tier B/C delayed ACK | WRITTEN / NOT EXECUTED |
| IEC104-A017 | `t2` flushes pending receive ACK without faulting session | `Iec104TcpFaultInjectionTests.Adapter_T2FlushesPendingReceiveAcknowledgementWithoutFaultingSession` | Tier B/C delayed traffic | WRITTEN / NOT EXECUTED |
| IEC104-A018 | `t0` bounds initial TCP connect | adapter connect implementation/connection tester | Tier B/C unreachable target case | IMPLEMENTED / NO AUTOMATED CASE |

### 6.3 General Interrogation and reconnect bootstrap

| ID | Requirement | Automated evidence in branch | External evidence | Current state |
|---|---|---|---|---|
| IEC104-A020 | GI request encodes `C_IC_NA_1`, COT Activation, IOA 0, QOI 20 | `Iec104GeneralInterrogationTests` | Tier B/C/D | WRITTEN / NOT EXECUTED |
| IEC104-A021 | positive ACT_CON -> collecting -> ACT_TERM -> completed | `Iec104GeneralInterrogationTests` | Tier B/C/D | WRITTEN / NOT EXECUTED |
| IEC104-A022 | negative GI confirmation becomes rejected, not completed | `Iec104GeneralInterrogationTests` | Tier B/C if simulator supports rejection | WRITTEN / NOT EXECUTED |
| IEC104-A023 | GI is tracked independently per configured CA | session/observation tests | Tier B/C with multiple CAs | WRITTEN / NOT EXECUTED |
| IEC104-A024 | reconnect uses deterministic backoff and fresh session | `Iec104ReconnectTests`, `Iec104ManagedClientTests` | Tier B/C/D network interruption | WRITTEN / NOT EXECUTED |
| IEC104-A025 | reconnect performs new STARTDT and GI | `Iec104ManagedClientTests` | Tier B/C/D packet trace | WRITTEN / NOT EXECUTED |
| IEC104-A026 | operational command is never replayed after reconnect | `Iec104ManagedInflightCommandReconnectTests.LinkLossAfterExecuteTransmission_IsAmbiguousAndCommandIsNotReplayedAfterReconnect` | Tier B/C/D control interruption | WRITTEN / NOT EXECUTED |

### 6.4 Monitored indications, SQ and point identity

| ID | Requirement | Automated evidence in branch | External evidence | Current state |
|---|---|---|---|---|
| IEC104-A030 | all 12 approved monitored Type IDs decode with expected data type | `Iec104InformationObjectDecoderTests` | Tier B/C/D one value per type | WRITTEN / NOT EXECUTED |
| IEC104-A031 | SQ=0 per-object IOA decoding | decoder tests | Tier B/C | WRITTEN / NOT EXECUTED |
| IEC104-A032 | SQ=1 sequential IOA decoding | decoder tests | Tier B/C | WRITTEN / NOT EXECUTED |
| IEC104-A033 | SQ=1 IOA overflow is rejected rather than wrapping | `Iec104ProtocolConformanceTests.SequentialAsdu_RejectsIoaOverflowInsteadOfWrappingToZero` | no external malformed case required | WRITTEN / NOT EXECUTED |
| IEC104-A034 | stable point identity is CA + IOA | `Iec104PortablePointAddressTests`, observation/import tests | Tier B/C/D address map | WRITTEN / NOT EXECUTED |
| IEC104-A035 | one TCP session may carry multiple configured Common Addresses | data source/session implementation | Tier B/C with suitable peer | SIMULATOR REQUIRED |
| IEC104-A036 | spontaneous COT indications publish through the normal monitored path | session/decoder path | Tier B/C/D spontaneous change | SIMULATOR REQUIRED |
| IEC104-A037 | COT TEST process indications never publish as operational values | `Iec104TestCotIsolationTests.SessionRunner_DoesNotPublishTestMarkedProcessTelemetry` | Tier B/C if peer can generate test COT | WRITTEN / NOT EXECUTED |
| IEC104-A038 | unknown/unsupported Type IDs are not auto-created as TAGs | session filtering + Engineering candidate boundaries | Tier B/C unsupported type injection | IMPLEMENTED / NO AUTOMATED CASE |

### 6.5 Quality and CP56Time2a

| ID | Requirement | Automated evidence in branch | External evidence | Current state |
|---|---|---|---|---|
| IEC104-A040 | healthy quality -> `Good` | `Iec104AsduPrimitivesTests` | Tier B/C/D | WRITTEN / NOT EXECUTED |
| IEC104-A041 | IV -> `BadDevice` | `Iec104AsduPrimitivesTests` | Tier B/C if quality forcing is available | WRITTEN / NOT EXECUTED |
| IEC104-A042 | NT -> `Stale` | `Iec104AsduPrimitivesTests` | Tier B/C | WRITTEN / NOT EXECUTED |
| IEC104-A043 | SB/BL/OV -> `Uncertain` with documented precedence | `Iec104AsduPrimitivesTests` | Tier B/C | WRITTEN / NOT EXECUTED |
| IEC104-A044 | indeterminate double-point states publish value with semantic `Uncertain` | `Iec104AsduPrimitivesTests.DoublePointIndeterminateStatesCanBePublishedAsUncertain` | Tier B/C | WRITTEN / NOT EXECUTED |
| IEC104-A045 | valid CP56Time2a uses configured station timezone | `Iec104Cp56Time2aTests`, `Iec104ProtocolConformanceTests` | Tier B/C/D timestamp comparison | WRITTEN / NOT EXECUTED |
| IEC104-A046 | CP56 IV suppresses SourceTimestamp without fabricating one | `Iec104ProtocolConformanceTests.Cp56Time2a_InvalidBitSuppressesSourceTimestampWithoutInventingAnError` | Tier B/C if configurable | WRITTEN / NOT EXECUTED |
| IEC104-A047 | CP56 reserved bits/impossible date/DST gap rejected deterministically | `Iec104Cp56Time2aTests`, conformance tests | Tier B/C malformed timestamp optional | WRITTEN / NOT EXECUTED |
| IEC104-A048 | CP56 SU resolves ambiguous DST local time where possible and does not itself downgrade quality | `Iec104Cp56Time2aTests`; research errata | Tier B/C timezone-capable peer | WRITTEN / NOT EXECUTED |

### 6.6 Commands and safety outcomes

| ID | Requirement | Automated evidence in branch | External evidence | Current state |
|---|---|---|---|---|
| IEC104-A050 | `C_SC_NA_1` direct success and rejection | command transaction/coordinator tests | Tier B/C/D | WRITTEN / NOT EXECUTED |
| IEC104-A051 | `C_DC_NA_1` direct success and rejection | command tests | Tier B/C/D | WRITTEN / NOT EXECUTED |
| IEC104-A052 | normalized `C_SE_NA_1` validates finite -1..1 and correlates confirmation | command tests | Tier B/C/D | WRITTEN / NOT EXECUTED |
| IEC104-A053 | scaled `C_SE_NB_1` command | command tests | Tier B/C/D | WRITTEN / NOT EXECUTED |
| IEC104-A054 | short-float `C_SE_NC_1` rejects NaN/Infinity locally | command tests | Tier B/C/D | WRITTEN / NOT EXECUTED |
| IEC104-A055 | SBO sends select, waits positive echo, then execute | `Iec104CommandCoordinatorTests.SelectBeforeOperate_SendsExecuteOnlyAfterPositiveSelectionConfirmation` | Tier B/C/D | WRITTEN / NOT EXECUTED |
| IEC104-A056 | SBO failure before execute never physically executes command | coordinator tests | Tier B/C/D rejection/timeout | WRITTEN / NOT EXECUTED |
| IEC104-A057 | confirmation correlation includes Type, CA, OA, IOA, value and qualifier | command transaction tests | Tier B/C | WRITTEN / NOT EXECUTED |
| IEC104-A058 | COT TEST confirmation cannot advance an operational command | `Iec104TestCotIsolationTests.CommandTransaction_TestActivationConfirmationDoesNotAdvanceOperationalCommand` | Tier B/C optional | WRITTEN / NOT EXECUTED |
| IEC104-A059 | negative confirmation returns explicit `Rejected` | command tests | Tier B/C/D | WRITTEN / NOT EXECUTED |
| IEC104-A060 | no ACT_CON after execute -> `Ambiguous` | `Iec104CommandCoordinatorTests.MissingExecuteConfirmation_IsAmbiguousNotTimedOut` | Tier B/C/D interruption | WRITTEN / NOT EXECUTED |
| IEC104-A061 | accepted command with later disconnect -> `Ambiguous` | `Iec104CommandCoordinatorTests.SessionFailureAfterAcceptance_IsAmbiguous`, managed reconnect test | Tier B/C/D interruption | WRITTEN / NOT EXECUTED |
| IEC104-A062 | transport write failure after sequence reservation -> `Iec104AmbiguousTransmissionException` and no replay | concrete adapter implementation plus `Iec104CommandTransmissionAmbiguityTests` at coordinator seam | controlled socket write-failure case desirable | IMPLEMENTED / NO CONCRETE WRITE-FAILURE AUTOMATED CASE |
| IEC104-A063 | only one in-flight command per CA+IOA | `Iec104CommandCoordinatorTests.SamePointCannotHaveTwoInflightCommands` | no external evidence required | WRITTEN / NOT EXECUTED |
| IEC104-A064 | global command concurrency is bounded; overflow rejects instead of queueing | `Iec104CommandCoordinatorTests.GlobalCommandLimitRejectsInsteadOfQueueing` | no external evidence required | WRITTEN / NOT EXECUTED |

### 6.7 Engineering workflows

| ID | Requirement | Automated evidence in branch | External evidence | Current state |
|---|---|---|---|---|
| IEC104-A070 | connection test performs TCP + STARTDT + STOPDT without becoming runtime authority | `Iec104EngineeringConnectionTesterTests` | Tier B/C/D | WRITTEN / NOT EXECUTED |
| IEC104-A071 | Observe/Browse is bounded, GI-backed and always partial evidence | `Iec104ObservationCollectorTests`, `Iec104EngineeringProviderTests` | Tier B/C/D | WRITTEN / NOT EXECUTED |
| IEC104-A072 | COT TEST telemetry is not turned into an Engineering candidate | `Iec104TestCotIsolationTests.ObservationCollector_DoesNotCreateCandidateFromTestMarkedTelemetry` | Tier B/C optional | WRITTEN / NOT EXECUTED |
| IEC104-A073 | candidate cap truncates safely and reports partial result | observation/provider tests | Tier B/C high point count | WRITTEN / NOT EXECUTED |
| IEC104-A074 | Reconcile reports absence as Missing only after completed configured GI without candidate truncation | `Iec104EngineeringReconcilerTests` | Tier B/C | WRITTEN / NOT EXECUTED |
| IEC104-A075 | incomplete GI makes an unseen address Ambiguous, not Missing | reconciler tests | Tier B/C interruption | WRITTEN / NOT EXECUTED |
| IEC104-A076 | point-list import/export roundtrip preserves monitored types and conflicts | importer/exporter tests | file workflow only | WRITTEN / NOT EXECUTED |
| IEC104-A077 | CSV import enforces bounded rows, line length and bytes including non-seekable streams | `Iec104PointListImporterBoundsTests` | file workflow only | WRITTEN / NOT EXECUTED |
| IEC104-A078 | no automatic canonical TAG creation/apply from Browse/Import/Reconcile | contract and provider design | shared Preview/Apply integration | COORDINATOR BLOCKED |
| IEC104-A079 | persisted rich binding includes CA+IOA+semantic family/command profile | intentionally not implemented locally | shared schema migration | COORDINATOR BLOCKED |

### 6.8 Diagnostics and failure isolation

| ID | Requirement | Automated evidence in branch | External evidence | Current state |
|---|---|---|---|---|
| IEC104-A080 | transport diagnostics expose I/S/U counts, sequences, windows and timers without raw payloads | `Iec104DiagnosticsTests`, adapter tests | Tier B/C smoke comparison | WRITTEN / NOT EXECUTED |
| IEC104-A081 | managed diagnostics retain reconnect/session failure and command outcomes | managed diagnostics/client tests | Tier B/C/D failure run | WRITTEN / NOT EXECUTED |
| IEC104-A082 | ignored COT TEST ASDUs are counted across reconnect | `Iec104ManagedTestCotDiagnosticsTests` | Tier B/C optional | WRITTEN / NOT EXECUTED |
| IEC104-A083 | diagnostic error text is bounded and CR/LF sanitized | diagnostics implementation/tests | inject representative exception | WRITTEN / NOT EXECUTED |
| IEC104-A084 | one IEC-104 Data Source failure does not redefine unrelated Data Source health | requires common public driver integration | multi-Data-Source host test | COORDINATOR BLOCKED |
| IEC104-A085 | communication loss maps bound TAGs to `BadCommunication` through common path | requires public driver/binding integration | Tier B/C/D host test | COORDINATOR BLOCKED |

### 6.9 Robustness and load

| ID | Requirement | Automated evidence in branch | External evidence | Current state |
|---|---|---|---|---|
| IEC104-A090 | malformed ASDU length/object count cannot be silently decoded | decoder/ASDU tests | fuzz corpus run | WRITTEN / NOT EXECUTED |
| IEC104-A091 | bounded receive queue prevents unbounded process-data memory growth | transport implementation | burst test required | IMPLEMENTED / NO AUTOMATED CASE |
| IEC104-A092 | Engineering CSV import is bounded against excessive rows/line/file size | `Iec104PointListImporterBoundsTests` | no external peer required | WRITTEN / NOT EXECUTED |
| IEC104-A093 | sustained spontaneous burst preserves sequence correctness under acknowledgement pressure | partial sequence/window coverage | Tier B/C load generator | SIMULATOR REQUIRED |
| IEC104-A094 | long-running idle/reconnect cycle does not leak tasks/sockets or replay commands | reconnect/timer tests cover semantics | soak test | SIMULATOR REQUIRED |
| IEC104-A095 | hostile short-float NaN/Infinity indication has an explicit publication policy | decoder currently preserves IEEE float payload | policy review/test required before production | IMPLEMENTED / NO AUTOMATED CASE |

## 7. External-peer scenario script

For each Tier B and Tier C peer, execute in this order so failures are easy to localize.

### Phase 1 — connection/control plane

1. start station with one CA and known IOAs;
2. connect EliteSCADA client;
3. verify `STARTDT act/con`;
4. allow idle past `t3`; verify TESTFR exchange;
5. issue graceful shutdown; verify `STOPDT act/con`;
6. reconnect and confirm fresh sequence state.

### Phase 2 — GI and monitored data

1. reconnect and capture startup GI;
2. verify ACT_CON, monitored values and ACT_TERM;
3. change every approved untimed monitored type;
4. change every approved CP56-timed monitored type;
5. exercise SQ=0 and SQ=1 where peer supports both;
6. force spontaneous COT updates outside GI;
7. if supported, repeat with a second Common Address on the same socket.

### Phase 3 — quality/time

For each quality flag the peer can force, record raw IEC quality state and resulting EliteSCADA quality. Exercise a known CP56 timestamp with an explicitly recorded timezone. If the station can emit IV timestamps, verify no SourceTimestamp is fabricated.

### Phase 4 — controls

For each of the five command Type IDs:

1. positive Direct operation;
2. negative/rejected Direct operation where peer permits;
3. positive SBO;
4. failed/rejected selection where peer permits;
5. verify command echo identity/value/qualifier;
6. verify no automatic duplicate execute after reconnect.

Use a harmless lab point or simulator-only control target. Hardware controls must follow the site's normal authorization/interlock process.

### Phase 5 — fault injection

1. drop TCP before command send;
2. drop TCP immediately after execute reaches the peer but before ACT_CON;
3. drop TCP after positive ACT_CON but before ACT_TERM;
4. delay/drop I-frame ACK to cross `t1` in a controlled setup;
5. suppress TESTFR confirmation where simulator permits;
6. generate a sustained spontaneous burst;
7. reconnect and verify only bootstrap traffic is automatically sent.

## 8. Evidence record template

Create one record per external peer. The record may be attached to a handoff issue/artifact rather than committed if it contains sensitive network details.

```text
Peer label:
Implementation/vendor:
Version/firmware:
Tier: B / C / D
EliteSCADA branch:
EliteSCADA commit:
Date/time/timezone:
Operator:
Network topology (sanitized):
Station timezone:
CA list:
IOA/type test map reference:
Timer/window overrides:
Packet capture reference:
Application log reference:
Cases executed:
Cases passed:
Cases failed:
Cases not supported by peer:
Deviations/quirks:
Open defects:
```

Packet captures must be sanitized before sharing outside the controlled lab. Do not publish credentials, certificates/private keys, private addresses where prohibited, or unrelated process data.

## 9. Minimum production-acceptance gate for Driver 6

Driver 6 must not be called production-accepted until all of the following are true:

1. Tier A IEC-104 tests compile and pass on the target .NET 10 environment;
2. all supported first-release monitored Type IDs have Tier B evidence;
3. all supported first-release command Type IDs have positive Direct and SBO Tier B evidence;
4. STARTDT/STOPDT/TESTFR/GI/reconnect have Tier B and Tier C evidence;
5. spontaneous updates, CP56Time2a and quality mapping have Tier B and Tier C evidence to the extent the peers can generate them;
6. command interruption demonstrates explicit ambiguous outcome and no replay;
7. one representative Tier D RTU/IED/gateway has passed the applicable lifecycle, GI, indication and safe-control checks;
8. sustained event burst/soak testing has not shown unbounded queues/tasks/socket leakage;
9. all Coordinator-blocked shared binding/public-driver items have been resolved and tested through canonical TAG/Engineering paths;
10. the chosen production library/implementation licensing decision has been documented and approved.

## 10. Known acceptance blockers at creation time

- No .NET compiler/runtime is available in the current Driver 6 execution environment, so automated tests are **written but not executed**.
- No branch CI status checks are currently attached to the Driver 6 HEAD.
- No independent external IEC-104 simulator has yet been exercised by this Driver 6 workstream.
- No real RTU/IED/gateway has yet been exercised.
- Public canonical rich TAG binding, common command-outcome exposure, station-timezone inheritance, TLS certificate/trust integration and DriverHost registration remain Coordinator/shared-contract decisions.
- TLS/IEC 62351-3 is not part of this first runtime acceptance slice.

## 11. Handoff rule

The final Driver 6 handoff must include this matrix with each applicable case updated to one of:

- `ACCEPTED` with evidence reference;
- `FAILED` with defect reference;
- `NOT SUPPORTED BY PEER` with explanation and alternate evidence if required;
- `DEFERRED BY FIRST-RELEASE SCOPE` with the governing scope decision;
- `COORDINATOR BLOCKED` with the shared decision that remains open.

An unchecked or silently omitted case is not a pass.
