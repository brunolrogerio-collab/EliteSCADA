# Mitsubishi MELSEC address and capability matrix

## Status

Checkpoint: **1**

Contract: **C-INDUSTRIAL-MITSUBISHI-MELSEC-RESEARCH-01**

This matrix is a research artifact, not a production compatibility declaration.

A future driver must validate **family + CPU/module + firmware + frame + transport + device area + address range** before claiming support.

## 1. Matrix vocabulary

Disposition:

- **V1_CORE** — part of the portable first implementation target.
- **V1_PROFILE_GATED** — may be enabled only by an explicit family/model profile.
- **LATER** — officially meaningful but outside v1.
- **NOT_JUSTIFIED** — no v1 product need established.

Access:

- **R** — read in the proposed EliteSCADA profile.
- **RW** — read/write may be exposed when the canonical TAG is writable.
- **PROFILE** — exact access is profile/parameter dependent.

Important: protocol command availability does not override CPU mode, parameter, protection, system-area or safety restrictions.

## 2. Generic device-code matrix

The codes below follow the current **SLMP Reference Manual SH(NA)-080956ENG-N**. The compact one-byte code shown in parentheses in the manual is the relevant common Q/L-compatible form selected for v1.

| Device | Official meaning | Storage | Address radix | Common binary code | Proposed access | Disposition | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- |
| X | Input | bit | hex | 9C | R | V1_CORE | Product v1 keeps physical input read-only |
| Y | Output | bit | hex | 9D | RW | V1_CORE | Write still subject to PLC state/parameters |
| M | Internal relay | bit | decimal | 90 | RW | V1_CORE | Local-device variants/ranges remain profile-specific |
| L | Latch relay | bit | decimal | 92 | RW | V1_CORE | Range is family/model-specific |
| F | Annunciator | bit | decimal | 93 | PROFILE | LATER | Lower initial value |
| V | Edge relay | bit | decimal | 94 | PROFILE | LATER | Not portable across selected families; FX5 evidence has restrictions |
| B | Link relay | bit | hex | A0 | RW | V1_CORE | Network/link semantics still profile-specific |
| S | Step relay | bit | decimal | 98 | PROFILE | LATER | Supported-model restrictions |
| D | Data register | word | decimal | A8 | RW | V1_CORE | Core numeric storage |
| W | Link register | word | hex | B4 | RW | V1_CORE | Link semantics still profile-specific |
| R | File register, block switching method | word | decimal | AF | PROFILE | V1_PROFILE_GATED | Memory/file-register configuration matters |
| ZR | File register, serial-number method | word | hex in current generic SLMP table | B0 | PROFILE | LATER | Family incompatibilities; not portable v1 |
| SM | Special relay | bit | decimal | 91 | PROFILE | LATER | System/special semantics |
| SD | Special register | word | decimal | A9 | PROFILE | LATER | System/special semantics |
| SB | Link special relay | bit | hex | A1 | PROFILE | LATER | Network-specific |
| SW | Link special register | word | hex | B5 | PROFILE | LATER | Network-specific |
| TS | Timer contact | bit | decimal | C1 | PROFILE | LATER | Timer semantics deferred |
| TC | Timer coil | bit | decimal | C0 | PROFILE | LATER | Timer semantics deferred |
| TN | Timer current value | word | decimal | C2 | PROFILE | LATER | Timer semantics deferred |
| CS | Counter contact | bit | decimal | C4 | PROFILE | LATER | Counter semantics deferred |
| CC | Counter coil | bit | decimal | C3 | PROFILE | LATER | Counter semantics deferred |
| CN | Counter current value | word | decimal | C5 | PROFILE | LATER | Counter semantics deferred |
| STS/SS | Retentive timer contact | bit | decimal | C7 | PROFILE | LATER | Naming/code presentation varies by command profile |
| STC/SC | Retentive timer coil | bit | decimal | C6 | PROFILE | LATER | Family/profile-specific |
| STN/SN | Retentive timer current value | word | decimal | C8 | PROFILE | LATER | Family/profile-specific |
| DX | Direct access input | bit | hex | A2 | PROFILE | LATER | Direct-access profile |
| DY | Direct access output | bit | hex | A3 | PROFILE | LATER | Direct-access profile |
| Z | Index register | word | decimal | CC | PROFILE | LATER | Indirect-program semantics |
| LZ | Long index register | double word | decimal | extended code | PROFILE | LATER | Requires newer device form |
| RD | Refresh data register | word | decimal | extended code | PROFILE | LATER | Newer/extended subcommand only |
| G / module access | intelligent-module buffer access | word-oriented | profile syntax | extension | PROFILE | LATER | Module extension semantics |
| extended D | extended data register | word | decimal | extended form | PROFILE | LATER | Not in common first profile |
| extended W | extended link register | word | hex | extended form | PROFILE | LATER | Not in common first profile |

## 3. Why exact device ranges are profile-owned

The SLMP Reference Manual does **not** define one universal device-number range for every MELSEC CPU. For standard device access it repeatedly requires the number to be within the range of the access-destination module.

Family manuals show large differences.

Examples from the current QnU communication manual:

### QnU host-station example

- X: 0H..1FFFH
- Y: 0H..1FFFH
- M: 0..8191
- L: 0..8191
- B: 0H..1FFFH
- D: 0..143359
- W: 0H..1FFFH
- R: 0..32767
- ZR: 0..393215

The same manual explicitly notes that some point counts vary with CPU model and extended SRAM.

### iQ-R compatibility access

The QnU manual states that when communicating with MELSEC iQ-R through the compatibility path, device data is bounded to the range that the MELSEC-Q/L compatible path can handle.

This supports the proposed v1 choice: use a common bounded compatibility profile first rather than expose every iQ-R extended device form immediately.

### iQ-F

The QnU compatibility tables and the dedicated FX5 SLMP manual both demonstrate that iQ-F has its own ranges and notation constraints. The dedicated FX5 manual must win for the final exact FX5 driver profile.

### Consequence

Future production code needs two validation layers:

1. wire/profile-format bound;
2. exact selected family/model capability bound.

A valid wire number is not automatically a valid PLC device number.

## 4. Family capability matrix

| Family | Candidate Ethernet profile | 3E | 4E | Binary | ASCII | v1 | Required proof before compatibility claim |
| --- | --- | ---: | ---: | ---: | ---: | --- | --- |
| iQ-R | Ethernet-equipped CPU / RJ71EN71 with SLMP configured | YES, bounded common profile | PROFILE-SPECIFIC; not required by v1 | YES for selected profile | protocol supports where profile does | IN_SCOPE_WITH_PROFILE_GATE | exact CPU/module, firmware, GX Works setting, 3E test, device-range profile, L4 |
| iQ-F / FX5 | FX5 SLMP Ethernet function | YES | not established as common FX5 profile | YES | YES | IN_SCOPE | exact FX5 CPU/firmware, device range, L4 |
| Q / QnU | built-in Ethernet QnU or qualified QJ71E71-100 | YES | YES under documented profile/gates | YES | YES | IN_SCOPE_WITH_PROFILE_GATE | exact CPU/module/serial/firmware, L4 |
| L built-in Ethernet | LCPU built-in Ethernet | YES | **NO** | YES | YES | IN_SCOPE | exact LCPU/firmware, L4 |
| L module | LJ71E71-100 | YES | YES | YES | YES | IN_SCOPE_AS_SEPARATE_PROFILE | exact module/CPU/firmware, L4 |
| legacy FX3 | FX3U-ENET-L / FX3 Ethernet block/adapter | modern 3E v1 not established | NO common claim | YES in 1E documentation | YES in 1E documentation | OUT_OF_V1 | separate A-compatible 1E research/implementation profile |

## 5. FX5-specific findings

Dedicated FX5 SLMP documentation establishes:

- applicable Ethernet SLMP framing includes 3E and A-compatible 1E;
- both ASCII and binary are documented;
- for 3E, the message format corresponds to QnA-compatible 3E;
- binary X/Y device numbers use hexadecimal representation;
- the device table includes standard X/Y/M/L/F/B/S/D/W, timer/counter and R forms;
- some generic SLMP device forms are explicitly not compatible with FX5.

Therefore:

- FX5 is a modern 3E v1 candidate;
- "MELSEC-F/FX" must not be treated as one compatibility bucket;
- FX3 legacy and FX5/iQ-F require separate profiles.

## 6. L-series-specific findings

Built-in Ethernet LCPU documentation explicitly states:

**QnA-compatible 3E frames only are applicable to CPU modules.**

The same manual's comparison appendix shows:

- built-in Ethernet LCPU: 4E unavailable;
- LJ71E71-100: 4E available.

Therefore frame capability is tied to the Ethernet path, not only the family name.

This is the strongest direct reason not to choose 4E as v1.

## 7. Q-series-specific findings

Current QnU built-in Ethernet documentation lists:

- 4E frame;
- QnA-compatible 3E frame;
- A-compatible 1E frame under applicable conditions.

The QnU manual also includes model/serial gates for extended device capabilities.

Therefore:

- QnU/Q is a strong v1 candidate;
- "all Q" is not a valid compatibility claim;
- 3E remains the portable v1 choice even though qualified Q profiles can do 4E.

## 8. iQ-R-specific findings

The current iQ-R Ethernet manual configures external-device communication using an **SLMP Connection Module**.

The current SLMP Reference Manual:

- includes MELSEC iQ-R in the device-access command rules;
- distinguishes newer iQ-R/iQ-L subcommands from Q/L compatibility subcommands;
- lists RCPU model identities in Read Type Name behavior;
- includes RJ71EN71/RnENCPU in Ethernet-equipped module terminology.

V1 should intentionally use only the common compatibility subset.

Exact iQ-R CPU/module range and firmware stay profile-gated until L4.

## 9. Legacy FX3-specific findings

FX3U-ENET-L documentation describes MC Protocol Ethernet communication as a subset of **A-compatible 1E** frames.

Therefore:

**FX3 != FX5 for v1 compatibility.**

Future product options:

- add a separate 1E profile later under the same driver family; or
- add a distinct legacy driver identity if implementation/testing proves that cleaner.

Checkpoint 1 does not decide that later product split.

## 10. Canonical v1 address grammar

### Accepted families

~~~text
DEC_BIT  = M | L
HEX_BIT  = X | Y | B
DEC_WORD = D
HEX_WORD = W
PROFILE_DEC_WORD = R
~~~

### Lexical form

~~~text
address = mnemonic + digits
decimal digits = 0..9
hex digits = 0..9, A..F
~~~

Examples:

- D100
- M200
- X10
- Y20
- B1A
- W100
- L25
- R1000 when enabled by profile

### Input normalization

| Input | Result |
| --- | --- |
| d100 | D100 |
| D000100 | D100 |
| x00af | XAF |
| W0000 | W0 |
| M0 | M0 |

### Rejected forms

| Input | Reason |
| --- | --- |
| D-1 | signed address not allowed |
| D 100 | whitespace |
| D0x100 | explicit radix prefix not allowed |
| XG1 | invalid hex digit |
| M1A | decimal device with hex digit |
| D100:2 | selector mixed into portable address |
| 10.0.0.1/D100 | endpoint mixed into portable address |
| D100@1 | route mixed into portable address |
| ZR100 | device not enabled in v1 |
| TN0 | timer current value not enabled in v1 |

### Canonicalization

The parser should return a structured value conceptually containing:

- DeviceKind
- NumericAddress
- AddressRadix
- StorageKind
- CanonicalText

The codec receives the structured value, not the original arbitrary string.

## 11. Wire-format bound

For the common Q/L-compatible device form, the parser must enforce the encoded device-number field width before frame construction.

This wire-format bound is only the outer ceiling.

The family/model profile can and usually must impose a much smaller range.

## 12. Data Source versus TAG ownership

### Data Source

Owns:

- host
- TCP port
- transport
- frame profile
- encoding
- networkNo
- stationNo
- moduleIoNo
- multidropStationNo
- timeout
- selected family/model profile

### TAG binding

Owns:

- PortableAddress
- physicalDataType
- optional existing physical byte/word transform
- normal canonical TAG access policy/data type.

Do not repeat host, port or route in every TAG.

## 13. Routing matrix for v1

| Field | Direct connected default | Bounded v1 behavior |
| --- | --- | --- |
| networkNo | 00H | 00H direct; advanced 01H..EFH only when profile/routing is enabled |
| stationNo / legacy PC No. | FFH direct | FFH direct; ordinary remote 01H..78H |
| moduleIoNo | 03FFH CPU own station | profile-controlled; do not expose arbitrary free-form hex |
| multidropStationNo | 00H | 00H default; 00H..1FH only if a future profile proves multidrop need |
| extensionStationNo | none | OUT_OF_V1; station-number-extension frame is not part of 3E v1 |

"PC No." and "station No." must not become duplicate independent fields.

## 14. Physical data type matrix

| Physical type | Words/bits | Canonical TAG type | Native ordering | V1 |
| --- | ---: | --- | --- | --- |
| Bit | 1 bit | Boolean | protocol bit representation | YES |
| Int16 | 1 word | Int16 | low byte then high byte on Binary wire | YES |
| UInt16 | 1 word | Int32 | low byte then high byte | YES |
| Int32 | 2 words | Int32 | lower-number word = low 16; next = high 16 | YES |
| UInt32 | 2 words | Int64 | lower-number word = low 16; next = high 16 | YES |
| Float32 | 2 words | Float | same two-word ordering; Mitsubishi programming manual defines single-precision over 32 bits | YES |
| Float64 | 4 words | Double | requires explicit four-word validation | LATER |
| BCD | word(s) | numeric after explicit conversion | conversion contract not frozen | LATER |
| String | word sequence | String | length/encoding/padding not frozen | LATER |
| Byte array | word sequence | none direct | no shared byte-array TAG type | LATER |
| Array | multiple points | none direct | no shared array TAG type | LATER |

## 15. Endianness matrix

| Layer | Finding |
| --- | --- |
| 3E binary multi-byte protocol field | lower byte sent first |
| 16-bit word response example | 1234H appears as 34H 12H |
| 32-bit device storage | designated/lower-number word carries low 16 bits |
| next device word | carries high 16 bits |
| Float32 | two words; designated device carries lower 16 bits |
| default EliteSCADA ByteSwap | false |
| default EliteSCADA WordSwap | false |

The codec must implement Mitsubishi native order first. Shared ByteSwap/WordSwap is an explicit transform, not a substitute for correct native decoding.

## 16. Timer/counter matrix

| Area | Meaning | Storage | V1 |
| --- | --- | --- | --- |
| TS | timer contact | bit | LATER |
| TC | timer coil | bit | LATER |
| TN | timer current value | word | LATER |
| CS | counter contact | bit | LATER |
| CC | counter coil | bit | LATER |
| CN | counter current value | word | LATER |
| SS/STS | retentive timer contact | bit | LATER |
| SC/STC | retentive timer coil | bit | LATER |
| SN/STN | retentive timer current value | word | LATER |
| long timer/counter variants | family/newer profile forms | bit/double-word combinations | LATER |
| set value | PLC program/instruction parameter semantics | not ordinary current-value device | NOT_JUSTIFIED |

## 17. Initial write policy

| Device | Proposed v1 write exposure |
| --- | --- |
| X | NO |
| Y | YES, canonical writable TAG only |
| M | YES, canonical writable TAG only |
| L | YES, canonical writable TAG only |
| B | YES, canonical writable TAG only |
| D | YES, canonical writable TAG only |
| W | YES, canonical writable TAG only |
| R | PROFILE_GATED |
| special/timer/counter/extended | NO in v1 |

A protocol success is not proof that a physical actuator/process changed. Readback/process truth is a later checkpoint.

## 18. Family differences that must survive into implementation

The future driver must not erase these differences:

1. 3E/4E/1E frame availability;
2. built-in Ethernet versus Ethernet-module capabilities;
3. actual device-number ranges;
4. X/Y representation rules;
5. R/ZR/file-register behavior;
6. extended D/W availability;
7. V and other family-specific devices;
8. firmware/serial gates;
9. ability to route to another module/station;
10. RUN-mode/write/online-change restrictions;
11. timer/counter variants;
12. exact L4 hardware evidence.

## 19. V1 compatibility declaration template

A future support claim should look like:

~~~text
Manufacturer: Mitsubishi Electric
Family: MELSEC iQ-F
CPU: <exact model>
Ethernet: <built-in/module>
Firmware: <exact>
Transport: TCP
Frame: 3E
Encoding: Binary
Device profile: X/Y/M/L/B/D/W (+ R only if proven)
Read: tested
Write: tested per enabled areas
PointRead: tested
L4 date: <date>
~~~

Not:

~~~text
Supports Mitsubishi PLCs
~~~

## 20. Checkpoint 1 matrix conclusion

**V1 transport/frame:** TCP / 3E / Binary

**V1 modern family set:** iQ-R profile, iQ-F/FX5, QnU/Q profile, L profile

**Legacy FX3:** later 1E profile

**V1 core devices:** X, Y, M, L, B, D, W

**Profile-gated:** R

**V1 physical types:** Bit, Int16, UInt16, Int32, UInt32, Float32

**Parser:** strict structured grammar + profile range validation

**Timers/counters:** later

**Decision:** **GO_WITH_GATES**

## 21. Sources

Accessed 2026-10-06:

- SLMP Reference Manual SH(NA)-080956ENG-N  
  https://dl.mitsubishielectric.com/dl/fa/document/manual/plc/sh080956eng/sh080956engn.pdf
- MELSEC iQ-R Ethernet User's Manual (Application) SH(NA)-081257ENG-AD  
  https://dl.mitsubishielectric.com/dl/fa/document/manual/plc/sh081257eng/sh081257engad.pdf
- MELSEC iQ-F FX5 User's Manual (SLMP) JY997D56001K  
  https://dl.mitsubishielectric.com/dl/fa/document/manual/plcf/jy997d56001/jy997d56001k.pdf
- QnUCPU User's Manual (Communication via Built-In Ethernet Port) SH(NA)-080811ENG-Y  
  https://dl.mitsubishielectric.com/dl/fa/document/manual/plc/sh080811eng/sh080811engy.pdf
- MELSEC-L CPU Module User's Manual (Built-In Ethernet Function) SH(NA)-080891ENG-S  
  https://dl.mitsubishielectric.com/dl/fa/document/manual/plc/sh080891eng/sh080891engs.pdf
- FX3U-ENET-L User's Manual JY997D38001F  
  https://dl.mitsubishielectric.com/dl/fa/document/manual/plc_fx/jy997d38001/jy997d38001f.pdf
- QnUCPU User's Manual (Function Explanation, Program Fundamentals) SH(NA)-080807ENG-AE  
  https://dl.mitsubishielectric.com/dl/fa/document/manual/plc/sh080807eng/sh080807engae.pdf
- MELSEC-Q/L Programming Manual (Common Instruction) SH(NA)-080809ENG-X  
  https://dl.mitsubishielectric.com/dl/fa/document/manual/plc/sh080809eng/sh080809engx.pdf

**DOCS_ONLY / NO PRODUCT CODE CHANGED / NO DEPENDENCY CHANGED / NO CI CHANGED / NO MERGE PERFORMED**

---

# Checkpoint 2 — transaction and execution capability addendum

## 22. Command capability matrix

All limits below are bounded to the Q/L-compatible common forms selected for the v1 research profile. Exact family/module limits can be lower.

| Command | Code | Shape | Common documented bound | Proposed v1 role |
| --- | --- | --- | --- | --- |
| Batch Read | 0401 | consecutive devices | binary bit 1..7168; word 1..960 | **REQUIRED** primary scan |
| Batch Write | 1401 | consecutive devices | binary bit 1..7168; word 1..960 | **REQUIRED** one canonical write's physical span |
| Read Random | 0403 | nonconsecutive word/dword | word + dword points <=192 | **OPTIMIZATION** sparse reads |
| Write Random | 1402 | nonconsecutive writes | bit <=188; common encoded payload formula <=1920 | **NOT background WriteAsync coalescing** |
| Read Block | 0406 | multiple word/bit blocks | <=120 blocks; total points <=960 | **OPTIMIZATION** multiple contiguous runs |
| Write Block | 1406 | multiple blocks | <=120 blocks; blockCount*4 + total points <=960 | **NOT background WriteAsync coalescing** |
| Read Type Name | 0101 | controller/module identity | one bounded identity response | **ConnectionTest / diagnostics** |
| Self Test | 0619 | direct Ethernet module loopback | 1..960 bytes in documented form | optional Engineering evidence |

Protocol maximums are ceilings, not universal preferred batch sizes.

## 23. Scan planner invariants

| Invariant | Required |
| --- | --- |
| One request per TAG by default | **NO** |
| Group by Data Source | **YES** |
| Group by route tuple | **YES** |
| Structured parsed address before planning | **YES** |
| Keep typed point wholly inside one segment | **YES** |
| Validate family/profile range | **YES** |
| Use 0401 for contiguous areas | **YES** |
| Use 0403/0406 only when valid/cost-effective | **YES** |
| Read across unknown invalid gaps | **NO** |
| Merge unrelated Runtime.WriteAsync calls | **NO** |
| Validate exact response length before demux | **YES** |
| Treat Read Block as atomic snapshot | **NO** |

## 24. Planner segmentation model

Effective request bound is:

~~~text
min(
  protocolHardMaximum,
  familyProfileMaximum,
  frameSizeMaximum,
  operationalSoftCap
)
~~~

The planner must not split:

- Int32;
- UInt32;
- Float32;
- any future multiword typed value

across request boundaries.

A small-gap optimization is future/profile-driven. Correctness default is **zero unvalidated gap**.

## 25. Connection/session matrix

| Property | V1 decision |
| --- | --- |
| Transport | TCP |
| Connection ownership | Data Source |
| Connection lifetime | persistent while active |
| Connections per normal Data Source | one |
| Outstanding requests per 3E connection | **one** |
| Correlation serial | unavailable in 3E |
| Request gate | required |
| Parallel pipeline | no |
| 4E bounded pipelining | later/profile-specific |
| Socket reconnect | bounded backoff |
| Half-open truth | next fresh protocol exchange |
| PLC restart | reconnect + fresh acquisition |
| Replay writes after reconnect | **never** |

## 26. Timeout matrix

| Timeout/budget | Owner | Meaning |
| --- | --- | --- |
| SLMP monitoring timer | protocol request | PLC/module command-monitoring budget |
| client request timeout | driver session | maximum EliteSCADA wait for full operation |
| scan cancellation | runtime | lifecycle/scan cancellation |
| reconnect backoff | driver runtime | bounded recovery scheduling |

The client timeout must allow the selected monitoring timer plus network/processing margin.

## 27. Process-truth matrix

| Evidence | What it proves | What it does not prove |
| --- | --- | --- |
| Socket write completed | bytes handed toward transport | PLC processed request |
| Valid SLMP response | remote protocol response received | end code success |
| End code 0 | command processing reported normal | memory still has value later |
| Readback matches | addressed device memory observed expected | PLC logic preserved it indefinitely |
| HMI/feedback TAG changes | canonical observed feedback changed | mechanical process definitely achieved objective |
| physical sensor/process feedback | actual field evidence, depending system design | unrelated effects |

Do not collapse these levels.

## 28. Write retry matrix

| Failure point | Automatic retry? | Required behavior |
| --- | --- | --- |
| Local validation fails | NO | reject before I/O |
| Connect fails before request dispatch | bounded retry MAY be safe | reconnect within overall budget |
| Read request times out | bounded retry YES | reset/reconnect |
| Write may have been dispatched, response lost | **NO** | ambiguous -> reset -> readback if possible |
| Explicit nonzero end code | NO blind retry | preserve end code / fail |
| Reconnect after PLC restart | NO write replay | resume fresh reads only |
| HA authority loss | NO | fence effects immediately |

## 29. Engineering capability matrix

| Capability | V1 | Notes |
| --- | --- | --- |
| ConnectionTest | YES | TCP + bounded protocol identity/probe |
| PointReadTest | YES | exact address/type/route |
| Discover | NO | no generic device discovery required/proven |
| Browse | NO | manual address assistant is not browse |
| FileImport | NO | no project importer |
| Reconcile | NO | no discovered semantic namespace |
| Model identification | YES where 0101 supported | evidence, not automatic compatibility admission |
| Self Test | OPTIONAL | direct Ethernet module only where profile permits |

## 30. PointRead evidence matrix

| Surface | Mitsubishi evidence |
| --- | --- |
| Raw | bytes/words/bits + command/end-code metadata |
| Decoded | native physical type after Mitsubishi ordering and explicit transform |
| Engineering | canonical TAG value representation |
| Quality | Good / BadCommunication / BadConfiguration / BadDevice as supported |
| Latency | full request/response RTT |
| Mutation | none |

PointRead must use the runtime codec/parser but remains transient Engineering evidence.

## 31. Diagnostic protocolDetails matrix

Recommended safe keys:

| Key | Example |
| --- | --- |
| transport | tcp |
| frame | 3e |
| encoding | binary |
| familyProfile | fx5 / qnu / lcpu / iq-r-profile |
| networkNo | 00 |
| stationNo | FF |
| moduleIoNo | 03FF |
| multidropStationNo | 00 |
| monitoringTimer | bounded formatted value |
| modelName | observed sanitized model |
| modelCode | observed code |
| lastCommand | 0401 |
| lastSubcommand | 0000 |
| lastEndCode | 0000 |
| lastBatchKind | batchRead |
| lastBatchPoints | count |
| lastBatchBlocks | count |
| lastRttMilliseconds | duration |
| lastFailureKind | timeout/endCode/malformed/etc |
| ambiguousWriteCount | count |
| lastWriteConfirmation | protocolAck/confirmedByReadback/ambiguous |

Common reconnect/request/read/write counters stay in the shared diagnostic counters.

## 32. Security matrix

| Feature | V1 conclusion |
| --- | --- |
| Native TLS in selected 3E/TCP profile | **not present in reviewed v1 protocol profile** |
| Generic authenticated secure session | **not present in reviewed v1 profile** |
| Remote Password | profile-specific / later |
| Remote Password equivalent to TLS | **NO** |
| Trusted OT LAN/VLAN | recommended |
| Firewall ACL | recommended |
| VPN for remote access | recommended |
| Direct public-Internet exposure | **prohibited product posture** |
| Resolved password in diagnostics/logs | **never** |
| Windows/Linux/container | compatible with ordinary outbound TCP networking |

## 33. Implementation architecture matrix

| Option | Status | Reason |
| --- | --- | --- |
| Built-in .NET codec/session | **RECOMMENDED** | bounded scope, exact async/timeout/write semantics, no dependency |
| McpX | reference/fallback only | capable and MIT, but no need to outsource v1 core; version signals require revalidation |
| e_MCProtocol | not v1 dependency | narrow/UDP-oriented package, not selected TCP profile |
| McProtocol | not selected | stale package activity and license/maintenance revalidation required |
| MitsubishiRx | not selected | broad dependency surface / package status requires revalidation |
| Managed sidecar | **NOT_JUSTIFIED** | no native/vendor process requirement |
| Reject implementation | **NO** | technical path remains viable |

**V1_DEPENDENCY = NONE**

## 34. EliteSCADA convergence matrix

| Concern | Existing authority to reuse |
| --- | --- |
| Runtime lifecycle/read/write | ICommunicationDriver |
| Runtime plan/factory | CommunicationDriverRuntimePlanning contracts |
| HA effects | CommunicationDriverRuntimeServices.EffectAuthority |
| Address envelope | CommunicationTagBinding |
| Byte/word transform | TagPhysicalValueTransform |
| ConnectionTest | Driver Engineering capability |
| PointRead | shared DriverPointRead contract |
| Diagnostics | CommunicationDriverDiagnosticSnapshot / #500 |
| Working lifecycle | Preview/Apply/Save/Publish/Activate |
| Cache/process values | canonical CurrentTagCache/TAG path |

**RESEARCH_CONTRACT_DELTA_REQUIRED = NO**

## 35. Checkpoint 2 decision

**MITSUBISHI_MELSEC = GO_WITH_GATES**

Resolved in Checkpoint 2:

- bounded command surface;
- batching/planner shape;
- one-outstanding 3E session;
- reconnect posture;
- safe write ambiguity/no-replay model;
- Engineering capability truth;
- PointRead shape;
- diagnostics extension;
- security posture;
- built-in .NET architecture;
- no dependency adoption.

Remaining for Checkpoint 3:

- L0-L4 matrix;
- independent peer selection;
- hardware shortlist;
- exact test CPUs/modules/firmware;
- legal/trademark review;
- final v1 recommendation.

**DOCS_ONLY / NO PRODUCT CODE CHANGED / NO DEPENDENCY CHANGED / NO CI CHANGED / NO MERGE PERFORMED**

