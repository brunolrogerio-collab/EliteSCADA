# IEC 60870-5-104 research and implementation contract

Status: **RESEARCH COMPLETE / RUNTIME NOT YET IMPLEMENTED**

Research date: 2026-08-29

Branch: `driver6/iec-60870-5-104`

This document is the mandatory research-first contract for the EliteSCADA IEC 60870-5-104 Driver workstream. It refines `docs/PARALLEL-DRIVER-WORK-ASSIGNMENTS.md` without registering a production Data Source, adding a protocol runtime dependency, changing the central Engineering schema, or modifying shared Driver SDK contracts.

The initial product role is **IEC 60870-5-104 client/master** connecting to one or more controlled stations/RTUs/IEDs over TCP. The driver must remain a normal EliteSCADA Data Source/TAG provider and must not create a protocol-private TAG, quality, persistence, Gateway, diagnostics, or authorization model.

## 1. Executive recommendation

1. Use a stable public Driver type identity owned by EliteSCADA, proposed as `iec60870.5.104`. One Data Source represents one configured IEC-104 TCP/application session endpoint. The TCP session may carry multiple Common Addresses (CA); points remain identified inside the Data Source by their IEC application identity.
2. Use **lib60870.NET 2.3.0 as the preferred first laboratory implementation candidate and interoperability reference**, behind an EliteSCADA-owned private adapter. The current library is pure C#, targets `netstandard2.0`, implements IEC 60870-5-101/104 client and server behavior, exposes asynchronous client operation, APCI parameters, the standard ASDU family, CP56Time2a, commands and TLS support.
3. Do **not** add lib60870.NET as a normal EliteSCADA product dependency under its public GPLv3 terms. MZ Automation offers commercial licensing. Production adoption is therefore gated by explicit commercial-license/redistribution review. Until that gate is satisfied, no GPL library code or package is committed to the EliteSCADA production dependency graph.
4. Keep a second implementation path available: an EliteSCADA-owned, narrow CS104 client/session/codec implementation for only the approved first-release profile. That fallback avoids license coupling but carries substantially higher protocol-correctness and interoperability burden. It must not be started casually merely because writing sockets looks easy for the first thirty minutes.
5. The initial runtime shall implement the IEC-104 data-transfer state machine explicitly: TCP connect, `STARTDT act/con`, numbered I-format transfer, S-format acknowledgements, U-format `STOPDT`/`TESTFR`, `k/w` windows, `t0/t1/t2/t3` supervision, sequence wrap at 32768, deterministic reconnect, and bounded queues.
6. On connection establishment, the default acquisition bootstrap is `STARTDT` followed by **General Interrogation** (`C_IC_NA_1`, QOI 20) for configured Common Addresses. After bootstrap, spontaneous/event and other valid process ASDUs update normal EliteSCADA TAGs through the common cache/event pipeline.
7. Stable point identity is **Data Source + Common Address + Information Object Address**. Engineering must additionally persist the expected semantic point/type family and optional command profile so a type change cannot silently reinterpret an existing point. Display labels are never identity.
8. The first release should support the common monitored families `M_SP`, `M_DP`, `M_ME` normalized/scaled/short-float and `M_BO`, with non-time-tagged and CP56Time2a variants where defined. CP24Time2a variants, protection-event ASDUs, integrated totals and file transfer are deferred until their extra semantics are deliberately mapped.
9. The first writable scope should support non-time-tagged single commands, double commands and setpoints (`C_SC_NA_1`, `C_DC_NA_1`, `C_SE_NA_1`, `C_SE_NB_1`, `C_SE_NC_1`) with explicit Direct Operate or Select-Before-Operate policy. Command success is never inferred from socket send success.
10. **Never automatically replay an operational command after connection loss or an ambiguous timeout.** If a command may have reached the controlled station but its confirmation was not observed, the outcome is `Unknown/Ambiguous` and must be surfaced as such. Reconnect may re-run reads/GI, not physical control actions.
11. CP56Time2a is a source timestamp, not the local publication time. Because the time format has no UTC offset, each IEC-104 Data Source must resolve timestamps using an explicit station/site timezone policy. Invalid device time must not be fabricated into `SourceTimestamp`.
12. IEC standard application values are already typed protocol fields; generic byte/word swap is therefore **not applicable to the normal IEC-104 ASDU families**. EliteSCADA must not add Modbus-style swap settings to this driver merely to satisfy visual symmetry. Future private/raw ASDU extensions would require an explicit separate contract.
13. There is no standardized IEC-104 address-space browse service. Engineering must not invent one. The useful workflow is a bounded **Observe + General Interrogation candidate capture** and/or bulk point-table import, always labelled partial evidence and always routed through normal Preview/Apply.
14. Production completion requires independent simulator and real RTU/IED interoperability evidence. Passing tests against the same library used by the client is necessary but not sufficient.

## 2. Scope and non-goals

### 2.1 In scope for the first implementation wave

- IEC 60870-5-104 client/master role;
- IPv4/IPv6-capable TCP endpoint configuration through normal .NET networking where supported by the selected adapter;
- TCP port 2404 default, configurable for deployment reality;
- STARTDT, STOPDT and TESTFR U-format handling;
- I-format transmit/receive sequencing and S-format acknowledgement;
- `t0`, `t1`, `t2`, `t3`, `k`, `w` supervision;
- deterministic reconnect and resynchronization;
- General Interrogation;
- spontaneous/event-driven process indications;
- Cause of Transmission validation and diagnostics;
- Common Address and Information Object Address identity;
- CP56Time2a source timestamps;
- monitored single point, double point, measured values and bitstring32 first-cut families;
- explicit quality mapping into `TagQuality`;
- single/double commands and setpoints selected for the first release;
- command confirmation/outcome state;
- common communication diagnostics plus IEC-104 protocol detail;
- canonical Engineering persistence/import/export once the shared rich binding slot is scheduled;
- bounded software-only tests plus simulator/hardware acceptance.

### 2.2 Explicitly deferred

- IEC 60870-5-101 serial transport;
- IEC 60870-5-103;
- IEC 61850;
- server/outstation role;
- redundant client-group/server topology beyond one active configured session per Data Source;
- CP24Time2a monitored/command variants;
- protection equipment event families `M_EP_*`;
- integrated totals `M_IT_*` until Binary Counter Reading flags/sequence semantics have a deliberate public mapping;
- packed single-point `M_PS_NA_1`;
- file transfer ASDUs;
- parameter loading;
- reset-process/device administration commands;
- clock synchronization until a dedicated permission/audit policy is approved;
- vendor-private Type IDs;
- IEC 62351-5 secure authentication;
- production TLS/certificate UX until the host-owned certificate/secret contract can carry it without raw private material;
- auto-discovery of endpoints or points;
- any automatic creation of canonical TAGs from observed traffic.

## 3. Fit with EliteSCADA architecture

IEC-104 does not change the common flow:

`Canonical Engineering -> DriverHost/compiler -> IEC-104 Data Source runtime -> TAG cache/Event Bus -> Historian/Alarms/Realtime/Gateway`

The protocol adapter owns transport/session mechanics. It does not own:

- stable EliteSCADA TAG IDs;
- TAG quality enum definitions;
- canonical revisions;
- historian identity;
- alarm identity;
- Gateway routing;
- browser authorization;
- project packages;
- Runtime/Engineering precedence.

Runtime acquisition is naturally **EventDriven/Hybrid**: spontaneous ASDUs provide event-driven updates, while General Interrogation and optional bounded reads provide deterministic bootstrap/recovery evidence.

One Data Source should normally represent one remote IEC-104 endpoint/session, not one individual Common Address. A single connection may transport multiple Common Addresses, and separating those into independent Data Sources would create artificial duplicate TCP sessions and misleading diagnostics unless a specific deployment requires it.

## 4. Protocol framing and connection state

### 4.1 TCP and APDU profile

IEC-104 uses TCP, with port **2404** as the standard/default port. The application protocol control information starts with `0x68`, followed by a one-byte APDU length and four control bytes.

The normal CS104 profile uses:

- Cause of Transmission: 2 octets, including originator address;
- Common Address: 2 octets;
- Information Object Address: 3 octets.

The first EliteSCADA implementation shall use this standard profile rather than exposing arbitrary address-size knobs. A future non-standard compatibility profile must be explicit and separately validated.

APDU/ASDU parsing must be bounded. The one-byte APDU length permits at most 253 bytes after the length byte; after the four control bytes the normal maximum ASDU payload is 249 bytes. Oversized, truncated or inconsistent frames are protocol errors, not partially trusted messages.

### 4.2 I-format

I-format frames carry ASDUs and two 15-bit sequence numbers:

- send sequence `N(S)`;
- receive acknowledgement `N(R)`.

Sequence counters advance modulo `32768`.

The driver must:

- validate expected receive progression;
- track sent-but-unacknowledged I frames;
- stop sending new I frames when the effective `k` window is full;
- consume peer acknowledgements from I/S frames;
- handle sequence wrap explicitly in tests;
- treat impossible acknowledgement jumps or receive-order violations as protocol/session faults requiring resynchronization rather than silently resetting counters.

### 4.3 S-format

S-format acknowledges received I-format frames without carrying an ASDU.

The receiver sends an acknowledgement when either:

- the configured `w` receive threshold is reached; or
- `t2` expires after unacknowledged received data and no outbound I frame has already carried the acknowledgement.

### 4.4 U-format

The initial driver must handle:

- `STARTDT act` / `STARTDT con`;
- `STOPDT act` / `STOPDT con`;
- `TESTFR act` / `TESTFR con`.

A TCP socket is not considered ready for normal process transfer until data transfer is started successfully.

Proposed runtime state distinction:

`Stopped -> Connecting -> TcpConnected -> StartingDataTransfer -> Running -> Stopping/Reconnecting/Faulted`

This protocol-specific state is adapter detail; common diagnostics project it into the normal EliteSCADA driver health model.

### 4.5 Timers and windows

Initial Engineering defaults:

| Parameter | Default | Rule |
| --- | ---: | --- |
| `t0` | 30 s | connection establishment/startup supervision; configurable |
| `t1` | 15 s | transmitted I/U frame confirmation supervision |
| `t2` | 10 s | delayed receive acknowledgement; must be `< t1` |
| `t3` | 20 s | idle time before TESTFR supervision |
| `k` | 12 | maximum unacknowledged sent I frames |
| `w` | 8 | acknowledgement threshold for received I frames; keep `w <= 2/3 k` by default |

Some implementations use different `t0` defaults, so these values are public defaults, not claims about every remote station.

Engineering validation must reject zero/negative values, `t2 >= t1`, `k`/`w` outside the 15-bit range, and obviously unsafe/unbounded settings. Expert overrides remain bounded.

### 4.6 Idle connection test

When a running connection has no traffic for `t3`, the driver sends `TESTFR act` and expects `TESTFR con` within the applicable `t1` supervision window. Missing confirmation closes the session and enters reconnect behavior.

Incoming `TESTFR act` must receive `TESTFR con` promptly even when no process data is flowing.

### 4.7 Graceful stop

A normal runtime stop/revision switch should attempt `STOPDT act`, wait only a bounded interval for `STOPDT con`, then close the socket. Shutdown must not hang a revision transition because a remote RTU disappeared.

## 5. Reconnect and session recovery

Reconnect behavior must be deterministic and bounded.

Recommended initial backoff profile per Data Source:

`1 s -> 2 s -> 5 s -> 10 s -> 30 s`, capped at 30 s until success.

The backoff resets after a stable successful data-transfer session. Cancellation from DriverHost must interrupt pending reconnect delay immediately.

On transport/session loss:

- associated IEC-104 TAGs enter the common bad-communication path according to normal runtime policy;
- protocol sequence state is discarded with the old TCP session;
- pending reads/GI may be retried after a new successful STARTDT;
- pending **commands are never automatically replayed**;
- a command that was sent but not conclusively confirmed becomes an ambiguous/unknown command result;
- diagnostics record disconnect reason, last sequence context and reconnect count without exposing sensitive packet payloads.

After reconnect and STARTDT confirmation, run startup General Interrogation again by default before declaring the Data Source fully synchronized.

## 6. General Interrogation and acquisition behavior

### 6.1 Startup GI

Default behavior after successful STARTDT:

1. determine configured Common Addresses represented by enabled TAG bindings;
2. send `C_IC_NA_1` with Cause of Transmission `ACTIVATION` and QOI 20 for each CA in a bounded sequence;
3. expect positive activation confirmation;
4. accept monitored ASDUs carrying interrogation-related causes;
5. track activation termination;
6. mark GI completion per CA in diagnostics.

A negative confirmation, unknown CA, timeout or disconnect is an explicit GI failure. It does not authorize synthetic Good values.

### 6.2 Spontaneous and other process causes

The runtime accepts configured monitored point updates from legitimate process causes including spontaneous, periodic/cyclic, background/requested and interrogation responses where valid for the Type ID.

Cause of Transmission remains diagnostic/provenance evidence. It is not part of TAG identity.

Unknown/unsupported Cause of Transmission values must be counted and surfaced. They must not crash the connection parser.

### 6.3 No automatic TAG creation

An ASDU for an unconfigured `CA + IOA` may be observed for Engineering diagnostics/candidate capture, but Runtime must not create a new canonical TAG simply because network traffic mentioned it.

## 7. Stable point identity and proposed binding direction

The protocol identity inside one Data Source is:

`{ commonAddress, informationObjectAddress }`

The binding must additionally declare expected semantics so a remote configuration change cannot silently turn one existing point into another kind of value.

Conceptual future public binding:

```json
{
  "protocol": "iec60870.5.104",
  "commonAddress": 1,
  "informationObjectAddress": 1001,
  "monitorType": "singlePoint",
  "timestampPolicy": "acceptNoneOrCp56",
  "command": {
    "type": "singleCommand",
    "mode": "selectBeforeOperate",
    "qualifier": 0
  }
}
```

The exact DTO names/schema version remain a future shared Engineering migration decision. This branch must not invent an opaque metadata island while Schema v9 is authoritative.

### 7.1 Type-family matching

For configured points, time-tagged and non-time-tagged members of the same semantic family may be accepted according to binding policy, for example:

- `M_SP_NA_1` and `M_SP_TB_1` -> `singlePoint`;
- `M_DP_NA_1` and `M_DP_TB_1` -> `doublePoint`;
- `M_ME_NC_1` and `M_ME_TF_1` -> `measuredShortFloat`.

An unexpected **different semantic family** at the same `CA + IOA` is a point configuration/type fault. It must not reinterpret a Boolean TAG as a floating value because the remote station changed configuration.

### 7.2 Address validation

Engineering validates Common Address and IOA in the standard field ranges before activation. Friendly display may use decimal and optional hexadecimal rendering, but saved values are explicit numeric fields, not a concatenated address string.

## 8. First-release monitored ASDU matrix

Recommended first monitored families:

| IEC Type | Type IDs | EliteSCADA type | Initial behavior |
| --- | --- | --- | --- |
| Single point | `M_SP_NA_1` (1), `M_SP_TB_1` (30) | `Boolean` | direct Boolean value plus quality |
| Double point | `M_DP_NA_1` (3), `M_DP_TB_1` (31) | `Enum` | preserve all four IEC states; do not coerce indeterminate/transient states to Boolean |
| Bitstring32 | `M_BO_NA_1` (7), `M_BO_TB_1` (33) | `Int32` bit pattern | preserve 32-bit bit pattern; compatible with canonical integer bit selectors |
| Measured normalized | `M_ME_NA_1` (9), `M_ME_TD_1` (34) | `Float` | standardized normalized value |
| Measured scaled | `M_ME_NB_1` (11), `M_ME_TE_1` (35) | `Int16` | signed scaled protocol value |
| Measured short float | `M_ME_NC_1` (13), `M_ME_TF_1` (36) | `Float` | IEEE short floating value after protocol decode |

### 8.1 Double point

Double point carries four protocol states. Proposed public Enum members:

- `Indeterminate0`;
- `Off`;
- `On`;
- `Indeterminate3`.

The exact common Enum dictionary mechanism must use existing EliteSCADA Engineering semantics. Do not publish only `false/true` and discard the two indeterminate states.

### 8.2 Bitstring32 and TAG-bit semantics

`M_BO_*` is a semantic 32-bit bitstring, not a raw register requiring byte/word swap.

The first implementation may publish its bit pattern as canonical `Int32`, preserving all bits. Normal `TagBitSemantics` then provides logical `.00 .. .31` projection after publication.

If direct writable Boolean bit binding over a writable `C_BO_*` command is later exposed, it must obey the common coordinated read-modify-write/unrelated-bit preservation contract. It must not claim protocol-native atomic bit writes that IEC-104 does not provide.

### 8.3 Deferred monitored types

`M_ST_*` step-position information carries a transient flag in addition to numeric position. Current `TagValue` has no dedicated transient-process-state field. Mapping this flag into generic `TagQuality` would conflate process state and data quality, so `M_ST_*` remains deferred until the Coordinator accepts a deliberate representation.

`M_IT_*` integrated totals carry Binary Counter Reading flags/sequence semantics beyond an Int32 value. They likewise remain deferred rather than silently discarding carry/adjust/sequence evidence.

CP24Time2a variants remain deferred because reconstructing full date/time context is a different timestamp contract from CP56Time2a.

## 9. CP56Time2a timestamp semantics

`TagValue.Timestamp` remains local EliteSCADA observation/publication time.

A valid CP56Time2a from the remote station maps to `TagValue.SourceTimestamp`.

### 9.1 Timezone requirement

CP56Time2a represents calendar/time fields and summer-time indication but not an explicit UTC offset. Therefore the Data Source must resolve timestamps against a configured **station timezone** or an explicit inherited project/site timezone.

The final Engineering contract should store a stable timezone identifier compatible with server deployment. Conversion to UTC/`DateTimeOffset` occurs before publication.

### 9.2 Invalid/substituted time

- CP56 invalid-time flag: do not publish a fabricated `SourceTimestamp`; retain the process value if otherwise usable and add timestamp diagnostics.
- CP56 substituted-time flag: the timestamp may be retained as source evidence, but if point quality would otherwise be Good, the initial recommendation is to downgrade to `Uncertain` so substituted timing is visible until richer timestamp-quality metadata exists.
- impossible calendar values: reject the timestamp and increment a protocol decode/timestamp diagnostic; do not tear down a healthy TCP session solely for one bad point timestamp unless framing itself is corrupt.

### 9.3 Event ordering

Source timestamps assist historian/event chronology but do not replace local arrival ordering. Duplicate or out-of-order source times are possible and must not corrupt the current TAG cache state machine.

## 10. Quality mapping

EliteSCADA quality remains authoritative. IEC quality bits are input evidence.

Current target mapping:

| IEC evidence | EliteSCADA quality | Notes |
| --- | --- | --- |
| communication/session unavailable | `BadCommunication` | applies through normal owning Data Source failure path |
| QDS/SIQ/DIQ `IV` invalid | `BadDevice` | remote value explicitly invalid |
| `NT` not topical | `Stale` | value is not current/topical |
| `SB` substituted | `Uncertain` | value substituted by remote/operator/system |
| `BL` blocked | `Uncertain` | blocked is remote process evidence, not EliteSCADA `Disabled` |
| `OV` overflow | `Uncertain` | numeric result exists but exceeded normal representation/range evidence |
| no adverse descriptor bits | `Good` | only when the session/point/configuration is otherwise healthy |
| binding/type mismatch | `BadConfiguration` for affected point | connection may remain healthy for other points |
| local Engineering disabled | `Disabled` | common EliteSCADA semantics, not derived from IEC `BL` |

When multiple quality bits are present, initial precedence is:

`IV -> NT -> SB/BL/OV -> Good`

A Double Point indeterminate state should remain an explicit Enum value and may be published as `Uncertain` when no stronger quality fault is present.

An unsupported or malformed single point must not poison every other point on the Data Source if the ASDU framing remains valid and independent objects can be processed safely.

## 11. Cause of Transmission and originator handling

The driver must retain enough sanitized diagnostics to show why a value/command message was received.

Important initial COT classes include:

- periodic/cyclic;
- background scan;
- spontaneous;
- requested;
- activation;
- activation confirmation;
- deactivation/deactivation confirmation where encountered;
- activation termination;
- interrogation results;
- unknown Type ID/COT/Common Address/IOA error causes.

Originator Address is part of the two-octet COT profile. Default local originator is `0`, with bounded Engineering override when interoperability requires it.

Originator Address is **not** added to normal point identity. It participates in command/request correlation and diagnostics.

Negative/test flags in COT must be parsed deliberately. A negative command confirmation is not success with an interesting bit set; it is a failed/rejected command outcome.

## 12. Commands and write safety

### 12.1 First writable Type IDs

Initial command scope:

- `C_SC_NA_1` (45) single command;
- `C_DC_NA_1` (46) double command;
- `C_SE_NA_1` (48) normalized setpoint;
- `C_SE_NB_1` (49) scaled setpoint;
- `C_SE_NC_1` (50) short floating setpoint.

Time-tagged command variants are deferred until clock/timezone and remote acceptance behavior are validated in the lab.

### 12.2 Command modes

Engineering must explicitly select one of:

- `DirectOperate`;
- `SelectBeforeOperate`.

No device-brand heuristic chooses the mode.

For Select-Before-Operate:

1. send command with select bit set and COT `ACTIVATION`;
2. require matching positive activation confirmation within the select timeout;
3. then send execute form with select bit cleared;
4. require matching positive activation confirmation within the execute timeout;
5. record activation termination when supplied by the controlled station.

A negative confirmation, mismatched point/type, timeout or disconnect ends the transaction with explicit failure/ambiguous state.

### 12.3 Command outcome model

The public operational result needs to distinguish at least:

- `Selected`;
- `Accepted`;
- `Completed` when positive termination is observed;
- `Rejected`;
- `TimedOut` when nothing ambiguous was transmitted/accepted;
- `Ambiguous` when delivery/execution may have happened but definitive response was lost;
- `Cancelled` before execution when locally cancelled safely.

A normal `Socket.Send`/library send return is only local transmission evidence. It is never the final control result.

### 12.4 No automatic replay

Automatic resend of commands after uncertain disconnect is forbidden by default.

Examples:

- disconnect before any command bytes are accepted locally: command may be safely failed before-send;
- command transmitted, then connection lost before ACT_CON: result is ambiguous;
- positive ACT_CON received, then connection lost before ACT_TERM: result is accepted but completion unknown/ambiguous.

In the ambiguous cases the runtime does not resend after reconnect. A human/system policy must decide whether another command is appropriate.

### 12.5 Concurrency

Allow only one in-flight transaction per point. Data Source-level command concurrency must be bounded to prevent response-correlation ambiguity and command floods.

Control transactions must be isolated from GI/reconnect queues so a large interrogation does not starve a time-sensitive command or vice versa.

### 12.6 Type/value validation

- Single command accepts Boolean only.
- Double command accepts only valid executable states such as Off/On; indeterminate states are rejected before transmission.
- Normalized setpoint validates protocol range.
- Scaled setpoint validates Int16 range.
- Short-float setpoint rejects NaN/Infinity.

Normal EliteSCADA write authorization remains mandatory before the driver is called.

## 13. Byte/word ordering and bit access

The common physical byte/word ordering requirement applies only where a protocol binding represents raw multi-byte storage whose physical ordering is an Engineering choice.

Standard IEC-104 process ASDUs have protocol-defined field encodings. Therefore:

- no `byteSwap` option for normal `M_ME`, `M_BO`, command or address fields;
- no `wordSwap` option for normal standard ASDUs;
- protocol decoder produces the canonical typed value first;
- integer TAG logical bit selectors operate on that canonical value;
- `M_BO_*` can expose 32 bit selectors through normal `Int32` bit semantics;
- future vendor-private/raw payload Type IDs require a separate explicit versioned profile before any ordering controls are introduced.

This is a protocol-specific **not applicable**, not an omission.

## 14. Engineering configuration direction

### 14.1 Data Source fields

Proposed public fields when the shared rich binding/schema migration is scheduled:

- Driver type `iec60870.5.104`;
- host/IP;
- port, default 2404;
- connect timeout / `t0`;
- `t1`, `t2`, `t3`;
- `k`, `w`;
- originator address, default 0;
- station timezone;
- startup GI enabled, default true;
- optional bounded periodic GI interval, default disabled;
- reconnect profile/limits;
- transport security mode when host certificate infrastructure is available;
- protected certificate/trust references for TLS, never raw private keys/passwords.

The normal CS104 address sizes remain fixed to 2-byte COT, 2-byte CA and 3-byte IOA in the first implementation.

### 14.2 TAG binding fields

Proposed semantic fields:

- Common Address;
- Information Object Address;
- monitored point family;
- accepted timestamp mode (`NoneOrCp56`, etc.);
- writable flag/command profile when configured;
- command Type ID family;
- Direct/SBO mode;
- command qualifier where applicable;
- point-level stale/expected-update policy only through common TAG semantics, not a hidden IEC timer.

### 14.3 Validation

Preview/activation must reject at least:

- invalid host/port;
- invalid timer/window relationships;
- CA/IOA outside standard profile range;
- TAG data type incompatible with monitored Type family;
- writable TAG without compatible configured command family;
- invalid double-command state mapping;
- normalized/scaled setpoint type/range mismatch;
- missing station timezone when a policy requires interpreting CP56 source time;
- TLS mode without resolvable protected certificate/trust references once TLS is enabled;
- duplicate conflicting bindings for the same `CA + IOA` inside one Data Source.

## 15. Engineering observe/import workflow

IEC-104 has no standardized address-space browse comparable to OPC UA.

The first useful Engineering workflow should therefore be:

### 15.1 Connection test

A protected short-lived connection test may report:

- endpoint reachability;
- TCP connect latency;
- STARTDT success/failure;
- effective APCI parameters;
- observed Common Addresses during a bounded GI/observation window;
- protocol errors;
- TLS/trust evidence when TLS is later supported.

It must not activate a project revision or mutate canonical TAGs.

### 15.2 Observe + GI candidates

A bounded Engineering operation may:

1. connect/start data transfer;
2. issue General Interrogation to selected/configured Common Addresses;
3. collect unique `CA + IOA + observed Type family` candidates;
4. include sample value, quality, COT and timestamp evidence;
5. mark the candidate set **partial**;
6. return candidates to normal Preview/Apply.

Points that only report under operating conditions may not appear. Silence is not proof a point does not exist.

### 15.3 Bulk point-table import

Large RTU projects are often engineered from point lists. EliteSCADA should support canonical import/export columns for CA, IOA, point type, TAG path/name, writable command profile and related mapping once the rich protocol binding schema is available.

A driver-private CSV format must not become the only durable project representation.

## 16. Diagnostics

IEC-104 extends, but does not replace, `CommunicationDriverDiagnosticSnapshot`.

Useful protocol details:

- remote host/port;
- TCP state;
- IEC data-transfer state (stopped/starting/running/stopping);
- last successful STARTDT;
- last STOPDT;
- last TESTFR sent/confirmed;
- current send/receive sequence numbers;
- current unacknowledged I-frame counts;
- effective `k/w/t0/t1/t2/t3`;
- sequence/protocol-error count;
- reconnect count;
- last General Interrogation start/completion/failure per CA;
- total ASDUs received/sent;
- bounded counters by monitored Type family and major COT class;
- spontaneous event count;
- unknown/unconfigured CA/IOA count;
- type mismatch count;
- invalid/substituted CP56 timestamp count;
- negative command confirmation count;
- command timeout/ambiguous outcome count;
- current in-flight command count;
- normal Good/BadCommunication/etc TAG quality summary.

Diagnostics must not expose raw command payloads, credentials, certificate private material or unbounded per-point high-cardinality metrics.

## 17. Library strategy and licensing

### 17.1 lib60870.NET

Current evidence reviewed 2026-08-29:

- repository/version documentation identifies lib60870.NET 2.3.0;
- pure C# implementation;
- package project targets `netstandard2.0`;
- supports IEC 60870-5-101/104 client/master and server roles;
- asynchronous client API;
- standard monitored and control ASDUs;
- configurable APCI parameters;
- CP56Time2a;
- TLS support;
- GPLv3 public license;
- commercial licenses/support available from MZ Automation.

Strengths:

- mature protocol implementation and broad type coverage;
- managed-code lifecycle is a substantially better fit for .NET 10 than a native C dependency;
- existing async events and connection callbacks align with an adapter into EliteSCADA event-driven acquisition;
- strong reference implementation for protocol fixtures and interoperability.

Risks/gates:

- GPLv3 is not acceptable as an unreviewed dependency for a proprietary/non-GPL product distribution;
- commercial license terms and redistribution must be resolved before product adoption;
- `netstandard2.0` consumption by a .NET 10 host is expected to be technically feasible but still requires explicit build/runtime validation;
- adapter cancellation/disposal/reconnect behavior must be tested rather than assumed from API shape;
- production trust cannot rely solely on loopback against the same library's server implementation.

**Research decision:** preferred laboratory candidate/private adapter target; production dependency only after commercial licensing and compatibility acceptance.

### 17.2 lib60870-C

The related C implementation supports CS104 client/server and broad application types, with optional IEC 62351-3 TLS support. It is dual GPLv3/commercial.

It remains a useful independent implementation/reference but is not the preferred EliteSCADA product dependency because native binary packaging, interop lifetime and platform architecture increase operational complexity compared with the C# library.

### 17.3 EliteSCADA-owned narrow implementation

Fallback strategy if commercial dependency terms are unacceptable:

- implement only CS104 client/master;
- implement only the first-release ASDUs in this document;
- no CS101 serial stack;
- no server role;
- dedicated bounded frame/APCI/ASDU codec;
- `Socket`/`NetworkStream` async cancellation through .NET 10;
- extensive golden-frame, fuzz, simulator and hardware testing.

This path gives full licensing/control ownership but shifts protocol correctness, security and long-term maintenance entirely onto EliteSCADA. It is not the default unless licensing or compatibility evidence makes it necessary.

## 18. Security direction

Plain IEC-104 over TCP provides no application encryption/authentication by itself.

The current assignment does not authorize inventing a custom security layer.

The architecture must permit a later explicit TLS/IEC 62351-3 transport profile using host-owned certificate/trust management. lib60870.NET and lib60870-C both provide TLS capability evidence, but EliteSCADA must not persist private keys or raw passwords in canonical Engineering.

Until TLS is implemented, Engineering and documentation must state the effective transport security honestly. A plain-TCP connection must never be labelled secure merely because it runs on a protected plant LAN.

## 19. Test strategy

### 19.1 Software-only unit tests

Required focused tests when implementation begins:

- I/S/U control-field encode/decode;
- `N(S)`/`N(R)` progression and wraparound;
- `k` transmit-window blocking/release;
- `w`/`t2` receive acknowledgement behavior;
- t1 timeout closes/resynchronizes session;
- TESTFR act/con success and timeout;
- STARTDT/STOPDT state transitions;
- APDU minimum/maximum length;
- truncated/oversized/malformed frames;
- ASDU header CA/IOA/COT decode;
- each approved monitored Type family;
- CP56 valid/invalid/substituted cases and timezone conversion;
- QDS quality mapping precedence;
- semantic type mismatch isolated to affected point;
- GI activation/confirmation/data/termination sequence;
- reconnect re-runs GI;
- no automatic command replay;
- positive and negative command confirmation;
- Direct Operate and SBO;
- ambiguous command outcome after disconnect;
- duplicate/unconfigured points;
- burst spontaneous ASDUs with bounded processing queues.

### 19.2 In-process deterministic fake station

Build a test-only fake IEC-104 endpoint around raw TCP/frame fixtures or an interface-controlled station adapter. It must support scripted scenarios such as:

- normal STARTDT/GI;
- delayed S-frame acknowledgement;
- dropped TESTFR confirmation;
- sequence mismatch;
- spontaneous event bursts;
- negative command confirmation;
- disconnect between command send and confirmation.

The fake station is for deterministic tests, not a claim of full outstation conformance.

### 19.3 Independent interoperability

Before production acceptance, test against at least:

- lib60870.NET or lib60870-C station implementation as an external/reference peer;
- one independent IEC-104 simulator/stack from another implementation family;
- at least one representative real RTU/IED or utility gateway.

Validate:

- startup/reconnect;
- GI;
- spontaneous indications;
- each approved Type ID family;
- CP56 timestamps;
- quality flags;
- Direct/SBO command acceptance/rejection;
- network interruption during control;
- high event rate/burst behavior;
- long idle TESTFR behavior;
- multiple Common Addresses on one endpoint where available.

Hardware/simulator validation still required must be listed explicitly in the final Driver handoff.

### 19.4 Fuzz/robustness

Feed bounded malformed protocol inputs including:

- invalid start byte;
- impossible APDU length;
- truncated control field/ASDU;
- excessive object count versus payload length;
- sequence-address overflow/wrap edge cases;
- unknown Type IDs;
- invalid COT combinations;
- invalid CA/IOA;
- malformed CP56Time2a;
- NaN/Infinity short float if received from a hostile/non-conforming peer.

Malformed traffic must not create unbounded allocation, unbounded task creation or process crashes.

## 20. Acceptance matrix for the Driver slice

Before the IEC-104 branch can be called implementation-complete, automated/lab evidence must prove at least:

1. TCP connect and STARTDT handshake;
2. graceful STOPDT and bounded forced shutdown;
3. TESTFR idle supervision;
4. correct I/S sequence handling with wraparound;
5. `k/w/t1/t2` limits and acknowledgement behavior;
6. deterministic reconnect after transport failure;
7. startup GI and completion tracking per CA;
8. spontaneous single/double/measured/bitstring updates;
9. stable Data Source + CA + IOA point resolution;
10. type mismatch does not silently reinterpret a point;
11. CP56Time2a maps to SourceTimestamp using station timezone policy;
12. invalid/substituted time behavior is visible and deterministic;
13. IEC quality descriptors map to canonical `TagQuality` as specified;
14. communication failure maps affected points to BadCommunication through the common path;
15. unrelated healthy Data Sources remain healthy;
16. Single Command direct operation success/rejection;
17. Double Command direct operation success/rejection;
18. normalized/scaled/short-float setpoint validation and confirmation;
19. Select-Before-Operate handshake;
20. command timeout/disconnect yields explicit non-success outcome;
21. ambiguous commands are never replayed automatically after reconnect;
22. normal backend write authorization remains authoritative;
23. no automatic TAG creation from unknown ASDUs;
24. Observe/GI candidates are marked partial and require Preview/Apply;
25. canonical import/export/package fidelity once the shared rich binding schema is available;
26. Gateway write routing uses the normal owning-provider boundary;
27. protocol diagnostics integrate with common driver diagnostics;
28. secrets/private key material never appears in Engineering exports or diagnostics;
29. standard IEC ASDUs do not expose fake byte/word-swap options;
30. independent simulator plus real hardware/IED validation is recorded.

## 21. Shared-contract decisions requiring Coordinator reconciliation

This research intentionally avoids changing shared contracts, but implementation will eventually require Coordinator decisions for:

1. **Rich protocol TAG binding schema**: Schema v9 currently cannot express the full `CA + IOA + semantic type + command profile` contract without a deliberate shared migration.
2. **Step-position transient state**: `M_ST_*` needs a representation that does not misuse `TagQuality`.
3. **Integrated-total BCR flags/sequence**: `M_IT_*` needs a deliberate way to preserve counter flags/sequence if added.
4. **Command outcome surface**: common runtime/API commands may need explicit `Accepted/Completed/Rejected/Ambiguous` semantics suitable for telecontrol protocols rather than only generic write success/failure.
5. **Station timezone inheritance**: decide whether Driver Engineering stores a timezone explicitly per Data Source or may inherit a canonical project/site timezone with deterministic export behavior.
6. **TLS certificate/trust resolver**: must remain host-owned and common across Driver Modules.
7. **Observed-point candidate capability**: decide whether IEC-104 Observe+GI maps best to an existing generic Engineering capability or warrants a small common observe/reconcile candidate extension. It must not be mislabelled as full browse.

No shared file should be changed by Driver 6 merely to make one of these decisions locally.

## 22. Recommended implementation sequence after this research milestone

1. Create an IEC-104-private adapter seam and protocol-domain models under `Scada.Drivers` without changing shared public contracts.
2. Add test-only frame/APCI fixtures and deterministic fake-station infrastructure.
3. Implement/adapter-wrap TCP lifecycle, STARTDT/STOPDT/TESTFR and sequence/window supervision.
4. Implement monitored first-release ASDU decoding and `CA + IOA` routing into existing TAG publication contracts.
5. Add General Interrogation and reconnect bootstrap.
6. Add quality/CP56 timestamp mapping and diagnostics.
7. Add single/double/setpoint command transactions with Direct/SBO and ambiguous-outcome protection.
8. Add Engineering connection-test/Observe candidates only through existing capability boundaries that fit honestly.
9. Add import/export/persistence only when the shared rich protocol-binding migration is coordinated.
10. Run independent simulator/hardware acceptance and revisit the production library/license decision before any mainline integration proposal.

## 23. Sources reviewed

Protocol/implementation references reviewed for this research include:

- MZ Automation lib60870.NET repository and licensing: https://github.com/mz-automation/lib60870.NET
- MZ Automation lib60870.NET user guide 2.3.0: https://github.com/mz-automation/lib60870.NET/blob/master/user_guide_dotnet.adoc
- MZ Automation lib60870.NET project metadata: https://github.com/mz-automation/lib60870.NET/blob/master/lib60870/lib60870.csproj
- MZ Automation lib60870.NET API documentation: https://support.mz-automation.de/doc/lib60870.NET/latest/
- MZ Automation lib60870 C repository/user guide: https://github.com/mz-automation/lib60870
- Beckhoff IEC 60870-5-104 telegram structure: https://infosys.beckhoff.com/content/1031/tf6500_tc3_iec60870_5_10x/984444939.html
- Beckhoff IEC 60870-5-104 master interoperability list: https://infosys.beckhoff.com/content/1033/tf6500_tc3_iec60870_5_10x/983629707.html
- Beckhoff standard IEC 60870-5-104 data types: https://infosys.beckhoff.com/content/1033/tf6500_tc3_iec60870_5_10x/984447883.html
- Weidmuller IEC 60870-5-104 interoperability profile: https://support.weidmueller.com/online-documentation/latest/322745/files/Interoperability60870/IEC60870-5-104/IEC60870-5-104.html

The IEC standards themselves remain the normative authority where available to the implementer. Public vendor/library documentation is used here to establish implementation direction and interoperability evidence, not to replace the standard text.
