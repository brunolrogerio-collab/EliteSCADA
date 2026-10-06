# Mitsubishi MELSEC MC/SLMP execution research

## Status

Checkpoint: **1 — protocol + families + addresses + data types**

Contract: **C-INDUSTRIAL-MITSUBISHI-MELSEC-RESEARCH-01**

Order: **INDUSTRIAL-MITSUBISHI-MELSEC-EXECUTION-RESEARCH-01**

State: **RESEARCH_ONLY / DOCS_ONLY / NO_PRODUCT_CODE / NO_MERGE_BY_RESEARCHER**

Research branch at checkpoint start:

- branch: **research/industrial-mitsubishi-melsec**
- release base: **wave15/corrections-integration@d2569990bc53dfce61ca8043471719e958809d6c**
- research HEAD before this checkpoint: **d2569990bc53dfce61ca8043471719e958809d6c**
- before work: **ahead 0 / behind 0**

GitHub live is the authority. All compatibility claims below are bounded to the official manuals and exact family/profile evidence listed here.

## Checkpoint 1 preliminary decision

**MITSUBISHI_MELSEC = GO_WITH_GATES**

Recommended bounded v1:

- **DRIVER_ID_PRELIMINARY = mitsubishi.melsec.mc**
- **DISPLAY_NAME_PRELIMINARY = Mitsubishi MELSEC MC/SLMP**
- **V1_TRANSPORT = TCP**
- **V1_FRAME = 3E**
- **V1_FRAME_TERMINOLOGY = SLMP 3E / MC Protocol QnA-compatible 3E**
- **V1_ENCODING = Binary**
- **V1_SUBCOMMAND_PROFILE = Q/L-compatible device access (0000/0001 family where applicable)**
- **V1_ROUTE = directly connected CPU by default; bounded advanced routing fields at Data Source level**
- **V1_CORE_DEVICE_AREAS = X, Y, M, L, B, D, W**
- **V1_PROFILE_GATED_DEVICE_AREAS = R**
- **V1_TIMER_COUNTER = LATER**
- **V1_LEGACY_FX3 = LATER / separate 1E compatibility profile, not part of the 3E v1 claim**

The recommendation deliberately does **not** say that every MELSEC CPU supports 3E/4E, that SLMP and MC Protocol are synonyms, or that 3E is universally superior. It selects one portable first profile from the officially documented overlap.

## 1. Current EliteSCADA contract audit — read only

No shared product contract change is required by the Checkpoint 1 findings.

Current integration already provides the seams a future Mitsubishi driver needs:

### Communication TAG binding

Source: **src/Scada.Core/Tags/CommunicationTagBinding.cs**

The canonical binding already separates:

- driver-owned schema identity;
- stable **PortableAddress**;
- non-secret driver settings;
- protected material owned by Data Source secret references;
- protocol-independent physical **ByteSwap / WordSwap** transform.

Implication: Mitsubishi addresses such as D100 or X10 belong in PortableAddress. Host, port, routing and device-profile fields belong to Data Source/driver settings, not inside an opaque address string.

### Canonical Engineering data types

Source: **src/Scada.Core/Tags/TagDataType.cs**

Existing TAG types are:

- Boolean
- Int16
- Int32
- Int64
- Float
- Double
- String
- DateTime
- Enum

There is no canonical UInt16/UInt32 TAG type and no canonical array TAG type. A future Mitsubishi driver can still represent physical unsigned word/dword storage through driver-owned physical type metadata and widen into Int32/Int64 without changing the shared TAG enum.

### PointRead

Source: **src/Scada.Drivers/Abstractions/DriverEngineeringContracts.cs**

PointRead already carries:

- raw representation;
- decoded value;
- Engineering value;
- quality;
- latency;
- effective physical transform;
- bounded transient sample count/timeout;
- sanitized Engineering issues.

Implication: no Mitsubishi-specific PointRead framework is needed.

### Diagnostics

Source: **src/Scada.Drivers/Abstractions/CommunicationDriverDiagnostics.cs** and Issue **#500**

The common communication diagnostic snapshot already carries:

- driver/Data Source identity;
- endpoint;
- operational state;
- last success/failure;
- last sanitized error;
- data age;
- operation/scan timing;
- reconnect/read/write/request counters;
- TAG quality summary;
- bounded protocolDetails.

Implication: a future Mitsubishi driver should extend protocolDetails, not create MitsubishiDiagnostics.

### Runtime write and HA authority

Source: **src/Scada.Api/Runtime/ScadaRuntimeFacade.cs**

Canonical writes pass through Runtime.WriteAsync and are fenced by the existing High Availability external-effect authority before the active Engineering runtime receives the write.

Implication: the Mitsubishi driver must not expose a parallel write/control authority.

### Engineering lifecycle

Source: **docs/ENGINEERING-UI.md**

The canonical lifecycle remains:

**edit draft -> validate/preview -> apply Working -> save Revision -> publish -> activate**

A Mitsubishi Data Source and TAG binding must participate in this existing lifecycle.

## 2. Official source set

Access date for all URLs below: **2026-10-06**.

### Primary protocol authority

1. **SLMP Reference Manual**
   - manual: **SH(NA)-080956ENG-N**
   - revision date: **October 2025**
   - URL: https://dl.mitsubishielectric.com/dl/fa/document/manual/plc/sh080956eng/sh080956engn.pdf
   - role: protocol terminology, 3E/4E relationship, routing fields, device codes, request/response representation.

### Family authorities

2. **MELSEC iQ-R Ethernet User's Manual (Application)**
   - manual: **SH(NA)-081257ENG-AD**
   - revision date: **October 2024**
   - URL: https://dl.mitsubishielectric.com/dl/fa/document/manual/plc/sh081257eng/sh081257engad.pdf
   - role: iQ-R Ethernet external-device configuration and SLMP Connection Module.

3. **MELSEC iQ-F FX5 User's Manual (SLMP)**
   - manual: **JY997D56001K**
   - revision: **K**
   - revision date: **April 2022**
   - URL: https://dl.mitsubishielectric.com/dl/fa/document/manual/plcf/jy997d56001/jy997d56001k.pdf
   - role: FX5 3E/1E support, ASCII/Binary, device codes and FX5 restrictions.

4. **QnUCPU User's Manual (Communication via Built-In Ethernet Port)**
   - manual: **SH(NA)-080811ENG-Y**
   - revision date: **May 2026**
   - URL: https://dl.mitsubishielectric.com/dl/fa/document/manual/plc/sh080811eng/sh080811engy.pdf
   - role: current QnU built-in Ethernet MC Protocol frames, commands, compatibility and device ranges.

5. **MELSEC-L CPU Module User's Manual (Built-In Ethernet Function)**
   - manual: **SH(NA)-080891ENG-S**
   - revision date: **July 2020**
   - URL: https://dl.mitsubishielectric.com/dl/fa/document/manual/plc/sh080891eng/sh080891engs.pdf
   - role: LCPU built-in Ethernet MC Protocol frame support and device model.

6. **FX3U-ENET-L User's Manual**
   - manual: **JY997D38001F**
   - revision: **F**
   - revision date: **December 2021**
   - URL: https://dl.mitsubishielectric.com/dl/fa/document/manual/plc_fx/jy997d38001/jy997d38001f.pdf
   - role: legacy FX3 Ethernet evidence; A-compatible 1E framing rather than the selected modern 3E profile.

### Storage/data interpretation authority

7. **QnUCPU User's Manual (Function Explanation, Program Fundamentals)**
   - manual: **SH(NA)-080807ENG-AE**
   - revision date: **June 2024**
   - URL: https://dl.mitsubishielectric.com/dl/fa/document/manual/plc/sh080807eng/sh080807engae.pdf
   - role: 16-bit device storage and lower-word/higher-word ordering for 32-bit data.

8. **MELSEC-Q/L Programming Manual (Common Instruction)**
   - manual: **SH(NA)-080809ENG-X**
   - revision: **X**
   - URL: https://dl.mitsubishielectric.com/dl/fa/document/manual/plc/sh080809eng/sh080809engx.pdf
   - role: single-precision floating point stored across two word devices, designated device holding the lower 16 bits.

Community libraries and forums were not used as protocol authority.

## 3. MC Protocol versus SLMP

### Official relationship

Mitsubishi currently describes **SLMP** as the Seamless Message Protocol used to access SLMP-compatible devices over Ethernet.

The SLMP Reference Manual explicitly states that:

- the **SLMP 3E frame message format is the same as the QnA-compatible 3E frame in MC Protocol**;
- the **SLMP 4E frame message format is the same as the QnA-compatible 4E frame in MC Protocol**;
- external devices already using MC Protocol can therefore communicate directly with an SLMP-compatible device when using those corresponding formats.

Therefore:

**SLMP is not documented as a simple rename of all MC Protocol.**

The safe product statement is:

> EliteSCADA v1 uses the binary 3E wire format documented both as SLMP 3E and as MC Protocol QnA-compatible 3E.

This preserves the official relationship without claiming that every MC Protocol frame or every SLMP feature is identical.

### QnA-compatible frames

The QnA-compatible designation matters because MC Protocol also has legacy frame families such as A-compatible 1E. Legacy FX3 Ethernet documentation is a concrete example where the applicable frame is A-compatible 1E rather than the selected QnA-compatible 3E profile.

## 4. 3E versus 4E

### 3E

3E carries the common request destination route plus request data, but **does not carry a request serial number**.

Advantages for v1:

- present in the modern family overlap audited here;
- explicitly the only applicable MC frame for built-in Ethernet LCPU;
- supported by FX5/iQ-F;
- supported by current QnU;
- directly corresponds between SLMP and MC QnA-compatible terminology.

### 4E

4E extends 3E with an external-device-managed serial number. The response echoes that serial number, allowing response/request correlation when more than one request is outstanding.

4E is useful, but it is not a universal compatibility baseline:

- QnU supports it under documented conditions;
- LJ71E71-100 can support it;
- built-in Ethernet LCPU explicitly does **not**;
- the FX5 SLMP manual documents 3E/1E rather than a universal 4E profile.

Checkpoint 1 therefore classifies:

**4E = LATER / PROFILE_SPECIFIC**

Checkpoint 2 must respect the 3E consequence: because there is no serial number, the initial session model must not assume a parallel outstanding-request pipeline on one connection without separate proof.

## 5. Binary versus ASCII

Both binary and ASCII are valid data-code choices in the SLMP 3E/4E specification.

Mitsubishi documents that binary communication reduces the communication data amount by approximately half compared with ASCII.

Binary also removes an avoidable addressing ambiguity on FX5: the FX5 manual allows X/Y representation differences in ASCII configuration while binary X/Y device numbers are hexadecimal.

Checkpoint 1 selects:

**V1_ENCODING = Binary**

ASCII remains:

**LATER / INTEROPERABILITY_OPTION**

The driver must not silently auto-detect and switch encodings in v1.

## 6. TCP versus UDP

SLMP defines communication procedures for both TCP/IP and UDP/IP. Family manuals also expose both protocol choices where supported.

No official source reviewed here establishes "TCP is Mitsubishi's universal preferred mode." Therefore the following is an EliteSCADA design choice, not a vendor recommendation:

**V1_TRANSPORT = TCP**

Reasons:

- deterministic connection-oriented failure surface;
- natural fit for the current driver timeout/reconnect model;
- avoids adding packet loss/reordering/retry semantics to the first implementation;
- one bounded session model can be validated before adding UDP.

UDP remains:

**LATER**

The Data Source must own host and port. No fixed "Mitsubishi port" should be hard-coded because family Engineering settings allow configured ports.

## 7. 3E routing fields

The current SLMP 3E/4E request format includes:

- request destination network No.;
- request destination station No.;
- request destination module I/O No.;
- request destination multidrop station No.

For a directly connected station, the reference manual documents:

- network No. = **00H**
- station No. = **FFH**
- CPU own-station module I/O No. = **03FFH**
- multidrop station No. = **00H** when not using a multidrop destination.

For another network station, the reference manual documents network numbers **01H..EFH** and ordinary station numbers **01H..78H**. Higher station numbers require the station-number-extension frame and are therefore outside this v1.

### "PC number" terminology

Older MC Protocol documentation uses **PC No.** in the QnA-compatible frame where current SLMP documentation uses **request destination station No.**

These are not two independent routing dimensions for the same 3E request.

Future Engineering should expose one bounded route field and may label legacy help text as "Station / PC No." where useful. It must not store both and permit contradictory values.

### Data Source ownership

Recommended future Data Source fields:

- host
- port
- networkNo
- stationNo
- moduleIoNo
- multidropStationNo
- timeout
- PLC family/profile

PortableAddress remains only the device identity such as D100.

## 8. Recommended common v1 frame profile

The common v1 device-access profile should use the Q/L-compatible device form where applicable.

The current SLMP Reference Manual distinguishes:

- newer iQ-R/iQ-L-oriented device access subcommands 0002/0003;
- compatibility subcommands 0000/0001 for Q/L-compatible access.

For v1, the compatibility profile is preferred because it preserves a single bounded device-code/address representation across the selected families.

This intentionally defers:

- extended device-number features;
- long timer/counter device forms;
- direct/link/module access extensions;
- station-number-extension frame;
- label access;
- extended D/W addressing that requires newer subcommands.

## 9. PLC family matrix

| Family/profile | Ethernet path | Official frame evidence | V1 status | Important limits |
| --- | --- | --- | --- | --- |
| MELSEC iQ-R | Ethernet-equipped CPU / RJ71EN71 or model-supported Ethernet path configured with SLMP Connection Module | Current iQ-R Ethernet manual documents SLMP communications; SLMP reference includes iQ-R device access and RCPU identities | **IN_SCOPE_WITH_PROFILE_GATE** | Exact CPU/module/firmware must be recorded; v1 uses only the common 3E compatibility subset, not every iQ-R-specific extended device |
| MELSEC iQ-F / FX5 | FX5 Ethernet function covered by JY997D56001K | 3E and A-compatible 1E; ASCII/Binary; 3E corresponds to QnA-compatible 3E | **IN_SCOPE** | Do not infer support for every generic SLMP device; FX5 table has explicit incompatibilities; use model profile/range validation |
| MELSEC-Q | QnU built-in Ethernet and explicitly supported QJ71E71-100 profiles | Current QnU manual documents 4E and QnA-compatible 3E | **IN_SCOPE_WITH_PROFILE_GATE** | Do not claim every historical QCPU; extended ranges/features have model/serial gates |
| MELSEC-L | Built-in Ethernet LCPU; LJ71E71-100 is a separate module profile | Built-in Ethernet LCPU: QnA-compatible 3E only; LJ71E71-100 can additionally support 4E | **IN_SCOPE** for 3E | 4E cannot be a common L-family assumption |
| MELSEC-F / FX legacy (FX3) | FX3U-ENET-L / FX3 Ethernet block/adapter | Legacy Ethernet documentation uses A-compatible 1E | **OUT_OF_V1 / LATER** | Must be a separate legacy 1E compatibility profile; do not market 3E v1 as "all FX" |

### What "supports Mitsubishi" is not allowed to mean

The future driver must advertise exact tested profiles, for example:

- FX5U / exact firmware / built-in Ethernet / 3E Binary / TCP;
- QnU exact CPU / exact firmware / built-in Ethernet / 3E Binary / TCP;
- LCPU exact CPU / exact firmware / built-in Ethernet / 3E Binary / TCP;
- iQ-R exact CPU/module / exact firmware / 3E Binary / TCP.

A family name alone is insufficient for L4 support claims.

## 10. Device model

The current SLMP reference device table confirms the standard device-code families below. The exact device-number range is **not one universal SLMP range**; Mitsubishi repeatedly directs the reader to use the range of the access-destination module.

This is an important product constraint:

**syntax validation and model-range validation are two separate gates.**

### V1 core device areas

| Device | Official meaning | Storage | Radix | Binary code (common form) | V1 |
| --- | --- | --- | --- | --- | --- |
| X | Input | bit | hexadecimal | 9CH | **READ** |
| Y | Output | bit | hexadecimal | 9DH | **READ/WRITE** |
| M | Internal relay | bit | decimal | 90H | **READ/WRITE** |
| L | Latch relay | bit | decimal | 92H | **READ/WRITE** |
| B | Link relay | bit | hexadecimal | A0H | **READ/WRITE** |
| D | Data register | word | decimal | A8H | **READ/WRITE** |
| W | Link register | word | hexadecimal | B4H | **READ/WRITE** |

"WRITE" above means the driver can expose the normal protocol write path when the canonical TAG is writable. PLC operating mode, parameters, protected/system areas and protocol responses remain authoritative and can reject a write.

### V1 profile-gated

| Device | Reason |
| --- | --- |
| R | Useful file-register word area but range/memory configuration/block behavior is family/profile dependent. Enable only when the selected family profile proves the range/semantics. |

### Later

| Device/group | Reason |
| --- | --- |
| F, V, S | Lower initial value; family/profile restrictions; no need to widen v1 |
| ZR | Serial-number file-register method and family differences; FX5 evidence prevents a universal claim |
| SM, SD | Special/system semantics; unsafe to treat as ordinary general-purpose devices |
| SB, SW | Link-special semantics; network-specific |
| Z | Index register semantics; not needed for initial SCADA addressing |
| DX, DY | Direct-access I/O profile; not portable |
| G / module access | Requires module access extension semantics |
| extended D/W | Requires newer/extended device access profile |
| TS, TC, TN | Timer contact/coil/current-value semantics; deferred |
| CS, CC, CN | Counter contact/coil/current-value semantics; deferred |
| SS, SC, SN and long timer/counter forms | Family-dependent timer variants; deferred |

## 11. Address grammar for Engineering

### Canonical grammar

Future v1 Engineering should accept a **bounded parser**, not pass an arbitrary string to a codec.

Conceptual grammar:

~~~text
DEC_BIT  := (M|L) DIGIT+
HEX_BIT  := (X|Y|B) HEXDIGIT+
DEC_WORD := D DIGIT+
HEX_WORD := W HEXDIGIT+

PROFILE_DEC_WORD := R DIGIT+

PortableAddress := DEC_BIT | HEX_BIT | DEC_WORD | HEX_WORD | PROFILE_DEC_WORD
~~~

### Normalization

- device mnemonic input may be lower- or upper-case;
- canonical persistence is uppercase;
- no whitespace is allowed;
- no sign is allowed;
- no 0x prefix is allowed;
- no separator, route, host or port is allowed;
- leading zeros may be accepted on input but must canonicalize away:
  - D000100 -> D100
  - X0010 -> X10
- zero remains zero:
  - D000 -> D0.

### Radix

- decimal in v1: M, L, D, R;
- hexadecimal in v1: X, Y, B, W.

Do not infer decimal/hex from the presence of A-F alone. The device mnemonic determines the radix.

### Two-stage range validation

Stage 1 — **wire/profile format bound**

The common Q/L-compatible 3E device form is bounded by its encoded device number field. Reject overflow before frame construction.

Stage 2 — **selected PLC profile bound**

The selected CPU/module profile must validate the actual permitted range. This is mandatory because official manuals show substantial family/model range differences.

No implementation may use one enormous "universal MELSEC" range merely because the wire field can encode it.

### Invalid examples

The v1 parser must reject examples such as:

- D-1
- D 100
- D0x100
- X0x10
- M12.3
- ZR100 if ZR is not enabled by the profile
- TS10 while timers are not enabled
- 192.168.1.10:D100
- D100@network1
- arbitrary text.

Routing belongs in Data Source configuration.

## 12. Family range differences remain relevant

Official manuals show that the same mnemonic can have different accessible ranges by family/model.

Examples from the current QnU communication manual's family-compatibility tables include materially different limits for Q/L, iQ-R compatibility access, iQ-F and QnA targets. The manual also states that some maximum point counts vary with CPU model and extended SRAM configuration.

Therefore the future driver needs a **family/profile capability table** rather than one hard-coded global maximum.

Checkpoint 1 does not freeze every exact CPU range because:

1. a family can have multiple CPU models and memory configurations;
2. extended D/W and file-register ranges can depend on model/serial/memory settings;
3. L4 compatibility claims must be exact model/firmware level;
4. inventing a universal range would violate the official "range of access destination module" requirement.

The companion matrix records the required parser/range policy and known compatibility differences.

## 13. Device storage versus Engineering data type

These are separate concepts.

### Device storage

Mitsubishi device access exposes:

- bit devices;
- 16-bit word devices;
- paired/multiword storage for larger values;
- specialized timer/counter forms;
- other extended device classes.

### Engineering data type

EliteSCADA must decode physical storage into an existing canonical TAG data type.

Recommended v1 physical-type mapping:

| Driver physical type | Device storage | EliteSCADA TAG type | V1 |
| --- | --- | --- | --- |
| Bit | one bit | Boolean | YES |
| Int16 | one word | Int16 | YES |
| UInt16 | one word | Int32 | YES |
| Int32 | two consecutive words | Int32 | YES |
| UInt32 | two consecutive words | Int64 | YES |
| Float32 | two consecutive words | Float | YES |
| Float64 | four consecutive words | Double | LATER |
| BCD | word(s) | requires explicit conversion contract | LATER |
| String | word sequence | String | LATER |
| Byte array | word sequence | no canonical byte-array TAG type | LATER |
| Array | multiple points | no canonical array TAG type | LATER |
| Raw word pair | two words | diagnostic/raw representation only | LATER as public type |

The UInt16/UInt32 mapping above does not require new shared TAG enum values:

- UInt16 fits losslessly in Int32;
- UInt32 fits losslessly in Int64.

The future driver-owned binding schema must still record the physical representation explicitly so the same D address is not ambiguously interpreted.

## 14. Byte order and word order

### Binary frame fields

The SLMP Reference Manual specifies binary multi-byte protocol fields in **lower-byte to upper-byte** order.

The device-read communication examples also show a word value such as 1234H returned as bytes **34H 12H**.

Therefore the wire codec must decode each 16-bit word as little-endian protocol data.

### Multiword device values

Mitsubishi CPU programming documentation defines the lower-numbered word device as the lower 16 bits of a 32-bit value and the next word device as the higher 16 bits.

For a 32-bit value starting at Dn:

- Dn = low 16 bits;
- Dn+1 = high 16 bits.

Single-precision floating point likewise occupies two word devices with the designated device carrying the lower 16 bits.

### EliteSCADA transform default

Native Mitsubishi decode should therefore assemble the official wire/storage order directly.

Recommended default:

- **ByteSwap = false**
- **WordSwap = false**

The shared transform remains available only as an explicit physical interoperability option where field evidence requires it. The driver must not apply a generic Modbus-style word swap by default.

### Strings

String byte/word packing is intentionally **not frozen for v1**. A later string contract must define:

- fixed versus configured length;
- byte order inside each word;
- termination/padding;
- character encoding;
- write truncation behavior.

No generic string default is justified in Checkpoint 1.

## 15. Timers and counters

The official device model separates timer/counter logical status from current value.

### Timer

- TS = timer contact, bit
- TC = timer coil, bit
- TN = timer current value, word

Additional retentive/long variants exist and differ by family/profile.

### Counter

- CS = counter contact, bit
- CC = counter coil, bit
- CN = counter current value, word

Long-counter and other variants exist on newer profiles.

### Set value

Timer/counter set values belong to PLC program/instruction semantics and are not safely reducible to the ordinary current-value device mnemonics above.

Checkpoint 1 therefore classifies:

- TS/TC/TN = **LATER**
- CS/CC/CN = **LATER**
- retentive/long timer-counter forms = **LATER**
- timer/counter set-value authoring = **NOT_JUSTIFIED in v1**

This avoids presenting PLC program configuration as ordinary SCADA process memory.

## 16. Read/write posture

The SLMP device command set includes read and write operations. Product policy must still be narrower than raw protocol capability.

Recommended v1 posture:

- X: read-only in EliteSCADA;
- Y/M/L/B/D/W: read/write when the TAG is canonically writable;
- R: read/write only after profile/memory-range validation;
- system/special/extended/timer/counter devices: not exposed in v1.

No write success may be described as proof that the physical process changed. That process-truth/readback model belongs to Checkpoint 2.

## 17. Driver identity decision

Preliminary identity remains:

**mitsubishi.melsec.mc**

Reason:

- the selected frame is explicitly the MC Protocol QnA-compatible 3E / SLMP 3E shared wire format;
- "mc" is broad enough for future 4E profile evolution;
- renaming to mitsubishi.slmp now would not improve technical accuracy because official documentation intentionally relates both names.

Display name should make both terms visible:

**Mitsubishi MELSEC MC/SLMP**

Final naming remains subject to Checkpoint 3 legal/trademark review.

## 18. Known uncertainties and gates

### Gate G1 — exact iQ-R profile

The iQ-R family is in scope only through exact Ethernet CPU/module profiles that officially expose SLMP. Future DEV must not enable an abstract "any iQ-R" toggle.

### Gate G2 — exact per-model ranges

V1 must ship a bounded model/family range profile or an equally conservative validated range strategy. No universal range.

### Gate G3 — L4 compatibility claims

Every marketed family compatibility claim must name exact CPU/module/firmware tested at L4.

### Gate G4 — R file register

R is profile-gated until memory/range behavior is explicit for each enabled family profile.

### Gate G5 — 3E session serialization

3E has no serial number. Checkpoint 2 must specify request serialization and timeout ambiguity before implementation.

## 19. Contract deltas

**RESEARCH_CONTRACT_DELTA_REQUIRED = NO for Checkpoint 1 core scope**

Existing EliteSCADA shared contracts can carry:

- driver-owned Data Source fields;
- PortableAddress;
- driver-owned binding settings;
- physical transforms;
- PointRead evidence;
- common diagnostics;
- canonical Runtime writes;
- HA fencing.

No shared TAG enum or new protocol-neutral runtime model is required for the recommended v1.

Potential future driver-owned schema fields are implementation details, not shared-contract changes:

- family/profile;
- physicalDataType;
- host/port;
- network/station/module/multidrop route;
- timeout.

## 20. Checkpoint 1 answers

### 1. Which protocol/frame should be v1?

**TCP + Binary 3E using the SLMP 3E / MC Protocol QnA-compatible 3E common profile.**

### 2. Which PLC families enter v1?

- MELSEC iQ-R — exact Ethernet CPU/module profile required;
- MELSEC iQ-F / FX5 — supported FX5 profiles;
- MELSEC-Q — exact QnU/Q Ethernet profile required;
- MELSEC-L — built-in Ethernet 3E profile and separately qualified module profiles.

Legacy FX3 is **not** part of v1.

### 3. Which device areas enter?

Core:

**X, Y, M, L, B, D, W**

Profile-gated:

**R**

Timers/counters, special/system, ZR and extended/direct/module device families are later.

### 4. Which address grammar?

Strict mnemonic + numeric address, mnemonic-selected radix, uppercase canonical form, no routing/host/port in the address, with family-profile range validation.

### 5. Which data types?

V1:

- Boolean
- Int16
- UInt16 widened to canonical Int32
- Int32
- UInt32 widened to canonical Int64
- Float32

Later:

- Float64
- BCD
- String
- byte arrays
- arrays
- timer/counter specialized value forms.

### 6. Which family differences remain relevant?

- available MC/SLMP frame profile;
- built-in Ethernet versus module;
- exact device ranges;
- extended D/W and file-register behavior;
- availability of ZR/V/special/direct/extended devices;
- firmware/serial/module gates;
- route reachability to other modules/stations;
- legacy FX3 1E versus modern 3E;
- exact hardware/firmware evidence for support claims.

## 21. Preliminary recommendation

**MITSUBISHI_MELSEC = GO_WITH_GATES**

Rationale:

- the wire protocol is officially documented;
- modern MELSEC families share a useful 3E Binary subset;
- the current EliteSCADA driver contracts are sufficient;
- address and data-type semantics can be bounded without a shared schema change;
- remaining gates are profile/range/session/hardware validation questions, not blockers to continued research.

## 22. Checkpoint boundary

This document stops at Checkpoint 1.

Not researched to completion here:

- batch/random/block request limits;
- bounded scan planner;
- persistent connection/reconnect algorithm;
- timeout ambiguity and write readback;
- security posture;
- library versus built-in implementation decision;
- L0-L4 lab plan and hardware shortlist;
- legal/trademark final review.

Those belong to later checkpoints and require a new **SIGA**.

**DOCS_ONLY**

**NO PRODUCT CODE CHANGED**

**NO DEPENDENCY CHANGED**

**NO CI CHANGED**

**NO MERGE PERFORMED**

**HA = High Availability**

**HAB = Home Assistant Bridge**

---

# Checkpoint 2 — transaction engine, planner, session, process truth, Engineering and architecture

## 23. Checkpoint 2 status

Research branch at Checkpoint 2 start:

- branch: **research/industrial-mitsubishi-melsec**
- research HEAD: **8757ea3ec9c34e646e1266f13565bdfa3a4a2c2b**
- integration: **wave15/corrections-integration@d2569990bc53dfce61ca8043471719e958809d6c**
- start state: **ahead 1 / behind 0**
- merge-base: **d2569990bc53dfce61ca8043471719e958809d6c**

This checkpoint remains:

**RESEARCH_ONLY / DOCS_ONLY / NO_PRODUCT_CODE / NO_MERGE_BY_RESEARCHER**

Checkpoint 1 remains the protocol/family/address authority. Checkpoint 2 does not widen v1 beyond:

- TCP;
- binary 3E;
- Q/L-compatible common device-access profile;
- selected modern family profiles;
- X/Y/M/L/B/D/W core device areas;
- R only when a family profile proves it.

Preliminary decision remains:

**MITSUBISHI_MELSEC = GO_WITH_GATES**

## 24. Checkpoint 2 official protocol evidence

Primary authority remains:

- **SLMP Reference Manual SH(NA)-080956ENG-N**
- revision date: **October 2025**
- accessed: **2026-10-06**
- https://dl.mitsubishielectric.com/dl/fa/document/manual/plc/sh080956eng/sh080956engn.pdf

The following command numbers and limits are from that manual's Q/L-compatible common device-access forms unless stated otherwise.

### 24.1 Batch Read — command 0401

Batch Read reads consecutive devices.

For the common binary profile:

- bit-device access: **1..7168 points**;
- word-device access: **1..960 points**.

The 960-word / 7168-bit values are hard protocol limits for the documented request form. They are **not** evidence that maximum-size requests are universally optimal for every CPU/module/scan.

### 24.2 Batch Write — command 1401

Batch Write writes consecutive devices.

For the common binary profile:

- bit-device access: **1..7168 points**;
- word-device access: **1..960 points**.

A normal response carries protocol completion/end-code evidence; it does not echo the written process value.

### 24.3 Read Random — command 0403

Read Random reads nonconsecutive word/double-word devices.

For the Q/L-compatible common subcommand:

**1 <= word-point-count + double-word-point-count <= 192**

For the newer iQ-oriented subcommand the documented bound is lower. The v1 compatibility profile therefore uses the common Q/L-compatible limit, and a family profile may reduce it further.

### 24.4 Write Random — command 1402

The common Q/L-compatible form supports nonconsecutive writes with bounded encoded-size formulas.

Relevant common limits include:

- bit points: **<= 188**;
- word/double-word payload sizing:
  **1 <= wordPoints * 12 + doubleWordPoints * 14 <= 1920**.

The newer iQ-oriented form uses smaller corresponding bounds.

This command is valid protocol capability, but it is **not** the default execution shape for independent EliteSCADA Runtime.WriteAsync calls because combining unrelated effects changes timing, error and retry semantics.

### 24.5 Read Block — command 0406

Read Block can read multiple nonconsecutive blocks.

For the common Q/L-compatible form:

- total block count: **<= 120**;
- total word points plus bit-block points: **<= 960**.

A bit block is handled in 16-bit units according to the command definition.

The manual also documents restrictions on device forms and warns that data consistency across access can depend on CPU/service processing. Therefore Read Block must not be presented as an atomic multi-block snapshot unless exact hardware evidence proves such semantics.

### 24.6 Write Block — command 1406

For the common Q/L-compatible form:

- total block count: **<= 120**;
- encoded workload condition:
  **blockCount * 4 + totalWordPoints + totalBitPoints <= 960**.

As with Write Random, protocol capability does not justify automatically joining independent application writes.

### 24.7 CPU processing effect

The manual states that external-device processing is performed as part of CPU processing and that large accesses can extend scan time. It recommends splitting accesses when large communication processing affects control.

Therefore:

**PROTOCOL_MAXIMUM != RECOMMENDED_OPERATIONAL_BATCH_SIZE**

A future implementation must separate:

- protocol hard maximum;
- family/profile maximum;
- configured/derived operational soft cap;
- observed runtime latency/scan effect.

No arbitrary global "always 960 words" planner is justified.

## 25. V1 command surface

### Required runtime commands

**V1_REQUIRED_READ = 0401 Batch Read**

**V1_REQUIRED_WRITE = 1401 Batch Write**

Rationale:

- 0401 naturally implements contiguous polling;
- 1401 naturally implements one TAG's contiguous physical span;
- both are enough for correct bounded first production behavior;
- they are the simplest commands to validate independently at L0-L4.

### Read optimizations

**V1_READ_OPTIMIZATION = 0403 Read Random + 0406 Read Block**

They are recommended for implementation because they can materially reduce request count for sparse layouts, but correctness must not depend on selecting them.

A safe first implementation may fall back to segmented 0401 reads whenever the optimizer cannot prove a better valid plan.

### Write optimizations

**1402 Write Random = CODEC/EXPLICIT_OPERATION_CAPABILITY, NOT BACKGROUND COALESCING**

**1406 Write Block = CODEC/EXPLICIT_OPERATION_CAPABILITY, NOT BACKGROUND COALESCING**

Do not take two unrelated calls such as:

- Runtime.WriteAsync(TAG_A, valueA)
- Runtime.WriteAsync(TAG_B, valueB)

and silently transform them into one protocol write merely because the protocol can do so.

That would couple two canonical effects to one response and create ambiguous partial/intention timing that does not exist in the canonical Runtime API.

## 26. Bounded scan planner

The future driver must not implement:

**1 TAG = 1 TCP request**

except when one isolated TAG cannot validly share any request.

### 26.1 Planner grouping keys

Compile polling work in this order:

1. **Data Source**
2. **route tuple**
   - networkNo
   - stationNo / legacy PC No.
   - moduleIoNo
   - multidropStationNo
3. **frame/subcommand profile**
4. **operation direction**
5. **storage class**
   - bit
   - word/multiword
6. **device mnemonic / compatible command group**
7. **numeric address**

Never combine different Data Sources or different route tuples in one request.

### 26.2 Structured points

Each compiled point must carry at least:

- parsed DeviceKind;
- numeric device address;
- storage class;
- physical type;
- physical span;
- canonical TAG identity;
- read/write permission;
- selected family/profile;
- route ownership from the Data Source.

The planner must never reparse a free-form address while constructing a frame.

### 26.3 Primary contiguous planning

For a group of same-area contiguous devices:

1. sort by numeric start address;
2. compute each point's physical span;
3. form a contiguous range;
4. never split a typed point at a request boundary;
5. segment before the minimum of:
   - protocol hard maximum;
   - family/profile maximum;
   - frame response/request bound;
   - configured/derived operational soft cap.

Examples:

- Int32 and Float32 occupy two consecutive words;
- a boundary may move earlier to avoid splitting a two-word value;
- overlapping TAG physical spans must be rejected or explicitly resolved during Engineering validation, not silently double-decoded.

### 26.4 Gap handling

Correctness baseline:

**maxGap = 0**

A future optimizer may coalesce a small gap only when every intermediate device address is known to be valid/readable for the selected family/profile.

Do **not** inherit Modbus's existing max-gap value as a Mitsubishi default merely because the product has that optimization elsewhere.

Reading through:

- unsupported devices;
- system/protected ranges;
- unconfigured file-register regions;
- profile holes

must not be an optimization side effect.

### 26.5 Sparse points

Deterministic optimization order:

1. contiguous 0401 when possible;
2. evaluate 0403 for isolated word/dword points;
3. evaluate 0406 for multiple meaningful contiguous runs;
4. otherwise use multiple bounded 0401 requests.

The optimizer should use an explicit cost model based on:

- request bytes;
- response bytes;
- request count;
- number of blocks/points;
- family command support;
- operational soft caps.

Do not choose Random/Block solely because it has the highest theoretical point count.

### 26.6 Response demultiplexing

For every request, the plan must retain an immutable offset map from protocol response units to canonical TAGs.

Before decoding:

- validate subheader/frame kind;
- validate response length;
- validate end code;
- validate expected byte/word count;
- reject truncation and trailing/structurally inconsistent data.

Only then decode each TAG's physical span and update its canonical value/quality.

A malformed batch must not shift offsets and publish plausible values to the wrong TAGs.

## 27. Session and connection model

### 27.1 Persistent TCP session

Recommended v1:

**one persistent TCP session per active Mitsubishi Data Source**

The session is owned by the Driver/Data Source runtime, not by a TAG.

Do not:

- create one socket per TAG;
- create a new TCP connection for every normal scan;
- share one mutable socket across unrelated Data Sources.

### 27.2 One outstanding request

The selected v1 is **3E**.

The SLMP manual states that the serial number used to correlate multiple pending requests is a 4E feature and cannot be set in 3E.

Therefore:

**V1_MAX_OUTSTANDING_PER_SESSION = 1**

Implementation shape:

- one async request gate / actor queue per TCP session;
- write complete request;
- read complete response;
- validate response;
- release gate;
- next operation.

Throughput comes from bounded batching, not from unsafe pipelining.

### 27.3 4E future profile

4E can carry external-device-managed serial numbers and the response returns the serial number. The manual also documents module-specific limits on processable outstanding requests.

Therefore a future 4E profile may support a bounded request map, but only after:

- exact CPU/module concurrency limit is known;
- serial allocation/wrap behavior is tested;
- out-of-order response handling is tested;
- timeout and late-response behavior is tested at L0/L1/L2/L4.

**4E_PARALLEL_PIPELINE = NOT_V1**

## 28. Monitoring timer and client timeout

SLMP includes a request monitoring-timer field.

The reference manual expresses it in **250 ms units** and documents bounded recommended values for own-station and routed access.

The future driver must keep three concepts distinct:

1. **PLC monitoring timer** — carried in the SLMP request;
2. **client request timeout** — EliteSCADA socket/request deadline;
3. **scan/lifecycle cancellation** — cancellation because Runtime/Data Source/revision is stopping.

The client timeout should be greater than the effective PLC monitoring budget plus bounded network/processing margin.

Do not set an unlimited PLC monitoring timer merely to hide latency.

A timeout diagnostic should record which budget expired.

## 29. Reconnect and half-open behavior

### Transport failure

On:

- connect failure;
- socket read/write failure;
- timeout;
- truncated frame;
- impossible response length;
- framing/subheader mismatch;
- connection reset;

the session must be discarded rather than attempting to continue on an uncertain stream boundary.

### Reconnect

Reuse the product's existing reconnect/backoff conventions:

- bounded exponential/staged delay;
- maximum delay;
- small jitter when useful to avoid restart storms;
- cancellation on Data Source/Active revision disposal;
- no accumulating retry tasks.

Do not create a Mitsubishi-global reconnect authority.

### Half-open TCP

A socket "Connected" property is not protocol health.

Truth is established by successful fresh protocol exchange. A half-open connection is detected when a real operation fails or times out, after which the connection is reset.

### PLC restart

After PLC/module restart:

1. stale connection is discarded;
2. reconnect with bounded backoff;
3. re-establish identity/readiness evidence;
4. resume fresh reads;
5. rebuild CurrentTagCache truth from reads;
6. **do not replay queued/ambiguous writes**.

## 30. Write process truth

The following truths are distinct:

1. TCP bytes were handed to the socket;
2. a syntactically valid SLMP response arrived;
3. the SLMP end code reported normal completion;
4. the target device memory contains the expected value;
5. PLC logic subsequently preserved/used that value;
6. the physical process changed.

Only the first three can be established directly by the write exchange.

A normal SLMP write response with end code 0 means the protocol/module reports successful command processing.

It does **not** prove physical process truth.

### 30.1 Successful write

Recommended default v1 result:

**PROTOCOL_ACKNOWLEDGED**

The canonical WriteAsync completes only after:

- full response received;
- response structural validation succeeds;
- end code == 0.

Do not return success after TCP send alone.

### 30.2 Readback policy

Recommended driver-owned policy:

- **ProtocolAck** — default for ordinary device-memory writes;
- **Readback** — optional/required per future Engineering policy for points where stronger memory truth is needed and the device area is readable.

Readback confirms:

**DEVICE_MEMORY_OBSERVED_AS_EXPECTED**

It still does not prove a downstream physical actuator/process changed.

### 30.3 Ambiguous timeout after possible dispatch

A write timeout is fundamentally different from a safe pre-send validation failure.

If the request may have reached the PLC but the response was lost:

**WRITE_OUTCOME = AMBIGUOUS / UNKNOWN**

Required behavior:

1. do not blindly resend;
2. reset the uncertain connection;
3. reconnect;
4. if the target is readable, perform bounded readback;
5. if readback equals the expected raw/device value, classify diagnostics as **CONFIRMED_BY_READBACK**;
6. if readback differs or cannot be obtained, report the operation as failed/ambiguous;
7. never silently replay the write after reconnect.

This intentionally differs from safe idempotent read retry.

### 30.4 Retry policy

Reads:

- bounded retry after connection failure is allowed;
- retry still obeys lifecycle cancellation and overall budgets.

Writes:

- validation failure before network dispatch: no protocol effect occurred;
- connect failure before request bytes can be dispatched: a bounded retry may be safe;
- after request dispatch starts: **NO BLIND RETRY**.

This aligns with the existing EliteSCADA principle demonstrated by the Modbus TCP transport: reads may retry a connection failure while writes are not automatically retried.

### 30.5 No replay queue

The driver must never retain process writes for automatic replay after:

- reconnect;
- PLC restart;
- Active revision change;
- HA authority transfer;
- Driver restart.

A caller may deliberately issue a new write later through the canonical Runtime authority, but the communication layer must not manufacture that new effect.

## 31. Error model

The protocol parser must preserve at least these categories:

### Configuration

Examples:

- invalid address;
- invalid physical data type for device area;
- out-of-profile range;
- unsupported family/frame/device combination.

Quality/result direction:

**BadConfiguration**

### Protocol/device end code

A nonzero end code is explicit remote protocol evidence.

Preserve:

- hexadecimal end code;
- sanitized Mitsubishi description where mapped;
- command/subcommand;
- route;
- responding station error information when present.

Map to **BadDevice** or a more specific existing canonical quality where justified.

Do not flatten all nonzero end codes to "socket failed."

### Communication

Examples:

- DNS/connect refusal;
- socket reset;
- timeout;
- truncated frame;
- lost session.

Quality:

**BadCommunication**

### Malformed/unexpected response

Malformed frames are transport/protocol failures, not valid point values.

Reset the session if stream alignment cannot be trusted.

### Ambiguous write

Ambiguous write is an operation result/diagnostic condition, not a fake Good value.

Do not publish the requested value as process truth merely because the application attempted a write.

## 32. Engineering capabilities

Recommended v1 descriptor truth:

| Capability | V1 | Reason |
| --- | --- | --- |
| ConnectionTest | YES | Can prove TCP + protocol-level exchange |
| PointReadTest | YES | Address/device read is a primary protocol function |
| Discover | NO | No general controller/network discovery is required or proven for v1 |
| Browse | NO | MC/SLMP device access does not provide a generic configured TAG/symbol namespace browser |
| FileImport | NO | No project-file importer is justified for v1 |
| Reconcile | NO | No durable discovered namespace exists to reconcile |
| Acquisition | Polling | Selected v1 runtime model |

A **manual address/device assistant is not Browse**. It is local Engineering UX driven by the documented device/profile matrix.

## 33. Connection Test

ConnectionTest should remain distinct from PointRead.

Recommended sequence:

1. validate Data Source settings locally;
2. TCP connect;
3. send one bounded protocol request;
4. prefer **Read Type Name (0101)** to obtain model name/model code when supported;
5. verify end code and response shape;
6. return sanitized endpoint and observed identity/properties;
7. dispose the transient Engineering session.

The SLMP reference also defines **Self Test (0619)** as a loopback/data-communication test for the directly connected Ethernet-equipped module.

Self Test may be used as additional Engineering evidence where the selected module supports it, but it is not a substitute for:

- routed destination validation;
- PointRead;
- Active runtime acquisition.

## 34. PointRead design

Reuse the current shared **DriverPointReadTestRequest / DriverPointReadTestResult** contract.

PointRead must use the same:

- address parser;
- family/profile range validator;
- route encoder;
- 3E codec;
- native physical ordering;
- typed value decoder

as the future runtime.

It must not register a TAG or mutate Working/Active.

### Raw evidence

For word reads:

- contiguous raw hex;
- ordered 16-bit word elements;
- response/end-code metadata.

For bit reads:

- raw binary response representation;
- ordered bit values;
- relevant command/subcommand/device metadata.

### Decoded value

Decoded means:

- native Mitsubishi storage decoded;
- explicit shared byte/word transform applied where configured;
- physical data type interpreted.

### Engineering value

Engineering means the canonical value after any normal host-owned Engineering transformation that is part of the TAG binding contract.

### Quality

Suggested mapping:

- structurally valid response + end code 0 + successful decode -> **Good**;
- socket/timeout -> **BadCommunication**;
- invalid point binding/range/type -> **BadConfiguration**;
- explicit PLC/device end code -> **BadDevice** when appropriate.

### Latency

Capture complete request/response round-trip latency.

Do not report TCP connect success as PointRead success.

## 35. Discovery and model identification

### CPU/model identification

**SUPPORTED AS BOUNDED CONNECTION EVIDENCE**

Read Type Name (0101) provides model name/model code for supported targets.

This can populate diagnostics and ConnectionTest observed properties.

It must not be used to claim an unsupported model profile automatically.

### Address browse

**NOT A PROTOCOL V1 CAPABILITY**

The protocol allows reads of known device addresses; that is not the same as enumerating a controller's configured semantic TAG namespace.

Engineering should provide:

- device mnemonic picker;
- family-aware range hints;
- physical data type picker;
- address validator;
- PointRead button.

### Symbol/label browse

**NOT ADVERTISED IN V1**

SLMP has label-related commands on applicable CPUs, but that does not justify presenting a universal symbolic browse surface across the v1 family set.

A later family-specific symbolic feature requires its own research/profile contract.

## 36. Diagnostics

Reuse **#500 common communication diagnostics**.

Recommended sanitized Mitsubishi protocolDetails:

- transport = tcp
- frame = 3e
- encoding = binary
- familyProfile
- networkNo
- stationNo
- moduleIoNo
- multidropStationNo
- monitoringTimer
- modelName when known
- modelCode when known
- lastCommand
- lastSubcommand
- lastEndCode
- lastBatchKind
- lastBatchPoints
- lastBatchBlocks
- lastRttMilliseconds
- lastFailureKind
- ambiguousWriteCount
- lastWriteConfirmation = protocolAck | confirmedByReadback | ambiguous

Use common counters for:

- connections;
- disconnections;
- reconnects;
- requests;
- timeouts;
- reads;
- writes;
- failed/successful operations.

Do not duplicate those counters inside protocolDetails unless a protocol-specific distinction is necessary.

Never expose:

- remote password;
- resolved protected material;
- credentials;
- process values merely for diagnostics;
- raw internal socket handles.

## 37. Security and deployment

### 37.1 Native v1 protocol security reality

The reviewed SLMP 3E device-access protocol does not establish a TLS-protected authenticated session for the selected v1 transport profile.

Do not describe 3E/TCP as encrypted.

Do not imply that possession of an open TCP connection authenticates an EliteSCADA server.

### 37.2 Remote password

The SLMP reference documents Remote Password lock/unlock commands for applicable devices.

Important limitations include:

- password length/format differs between Q/L-era and newer iQ profiles;
- even in binary communication, password data is represented as ASCII-code bytes;
- the feature is module/profile-specific and is not equivalent to TLS.

Remote Password is therefore:

**LATER / PROFILE_SPECIFIC**

If ever supported, it must use Data Source **protected material** and must never appear in PortableAddress, logs or diagnostics.

### 37.3 Deployment posture

Recommended production posture:

- trusted industrial OT LAN/VLAN;
- firewall ACL restricting PLC SLMP access to authorized hosts;
- PLC/module IP filtering where available;
- VPN or equivalent protected network path for remote access;
- do not expose a PLC's SLMP service directly to the public Internet.

Mitsubishi product/security guidance for FX5 Ethernet also recommends network protections such as firewall/VPN/access restrictions when connecting to untrusted networks.

### 37.4 Windows/Linux/container

The selected TCP 3E architecture requires ordinary outbound TCP sockets and no platform-specific native serial library.

Therefore the core driver architecture is naturally compatible with:

- Windows;
- Linux;
- containerized server deployments,

subject to normal routing/firewall/network namespace access to the OT network.

No privileged host resource is required for the Ethernet v1.

## 38. Implementation architecture comparison

### A. Built-in .NET codec/session

**RECOMMENDED**

Scope needed by v1 is deliberately bounded:

- binary 3E frame encode/decode;
- route fields;
- device parser/codes;
- commands 0401/1401 plus 0403/0406 optimization;
- explicit end-code handling;
- one-outstanding-request TCP session;
- bounded reconnect/backoff;
- typed value codec;
- PointRead/ConnectionTest;
- common diagnostics.

Advantages:

- exact control of timeout and ambiguous-write semantics;
- no library-specific public model leakage;
- deterministic L0 frame vectors;
- direct CancellationToken support;
- no extra redistribution/license/supply-chain dependency;
- straightforward Windows/Linux/container behavior;
- direct integration with existing EliteSCADA Runtime/Engineering boundaries.

### B. Third-party .NET library

**NOT SELECTED FOR V1**

Candidate survey is useful as implementation/reference evidence, not as protocol authority.

#### McpX

Observed public project/package evidence:

- MIT license;
- cross-platform .NET positioning;
- 3E/4E, binary/ASCII and TCP/UDP scope;
- batch/random operations;
- asynchronous API.

NuGet indexing observed version **0.9.1**, updated 2026-09-26, while the project site advertises a newer v0.11 line. This version-signal mismatch must be revalidated at any future dependency gate.

Disposition:

**REFERENCE / FALLBACK CANDIDATE, NOT ADOPTED**

#### e_MCProtocol

Observed package:

- version **2.0.0**;
- MIT;
- updated 2026-06-20;
- narrow MC 3E binary implementation;
- UDP-focused package scope.

Disposition:

**NOT A V1 TCP DEPENDENCY**

It may be useful as an independent implementation reference where its transport/profile matches a later test.

#### McProtocol

Observed package:

- version **1.2.5**;
- .NET Standard 2.0;
- MC 1E/3E/4E scope;
- last package update observed in 2018.

Disposition:

**NOT SELECTED — maintenance/license details require fresh review and activity is too stale for preferred adoption**

#### MitsubishiRx

Observed package/public descriptions show broad Mitsubishi protocol scope, but the dependency/package line is larger and public package signals indicate replacement/deprecation direction.

Disposition:

**NOT SELECTED — too broad for bounded v1 and dependency status requires fresh review**

### C. Managed sidecar

**NOT_JUSTIFIED**

The protocol is simple TCP request/response and requires no vendor-only native runtime.

A sidecar would add:

- process lifecycle;
- IPC;
- packaging;
- health;
- version compatibility;
- additional failure modes

without providing a needed isolation boundary for this v1.

### D. Reject/not implement

**NOT_JUSTIFIED**

The protocol and integration path are sufficiently documented to continue toward implementation after research gates.

## 39. Dependency decision

**V1_DEPENDENCY = NONE**

Recommendation:

**built-in managed .NET codec/session**

Third-party libraries may be used later as independent-shape comparison peers or fallback references, subject to license/release revalidation. No package is adopted by this research.

Dependency research sources accessed 2026-10-06:

- https://github.com/YudaiKitamura/McpX
- https://www.nuget.org/packages/McpX
- https://www.nuget.org/packages/e_MCProtocol
- https://www.nuget.org/packages/McProtocol
- https://www.nuget.org/packages/MitsubishiRx

These sources are dependency-evaluation sources only. Mitsubishi official manuals remain protocol authority.

## 40. Current EliteSCADA contract convergence

Checkpoint 2 confirms the existing platform seams remain sufficient.

### Runtime

Use existing:

**ICommunicationDriver**

No Mitsubishi runtime API.

### Runtime planning

Use existing library-independent:

- ICommunicationDriverRuntimePlanner;
- ICommunicationDriverRuntimePlan;
- ICommunicationDriverRuntimeFactory;
- CommunicationDriverRuntimeServices.

The existing runtime services already carry external-effect authority.

### Engineering

Use the existing descriptor and optional Engineering capabilities:

- ConnectionTest;
- PointReadTest.

Do not falsely advertise Browse/Discover/FileImport/Reconcile.

### Diagnostics

Use:

**ICommunicationDiagnosticsSource / CommunicationDriverDiagnosticSnapshot**

### HA

**HA = High Availability**

The future runtime must honor existing effect authority.

A node without effective industrial authority must not perform writes or other external effects.

### Save / Publish / Activate

No alternative lifecycle.

### Contract delta

**RESEARCH_CONTRACT_DELTA_REQUIRED = NO**

A shared "AmbiguousWrite" return type could be a future product enhancement, but it is not required to implement a safe v1: WriteAsync can throw a sanitized protocol/ambiguous-outcome exception, record diagnostic evidence, optionally perform bounded readback and must not replay the write.

No product/schema change is authorized by this research.

## 41. Checkpoint 2 implementation contract proposal

For the future DEV lane, the minimum architecture should be:

~~~text
Engineering Data Source
  -> validated family/route/session options
Engineering TAG CommunicationBinding
  -> parsed Mitsubishi address + physical type
Runtime planner
  -> per-Data-Source Mitsubishi runtime plan
Driver
  -> scan planner
  -> persistent TCP 3E session
  -> one outstanding request
  -> 0401 primary reads
  -> 0403/0406 read optimization
  -> 1401 per-effect contiguous writes
  -> typed decode/encode
  -> CurrentTagCache
  -> common diagnostics

Runtime.WriteAsync(TAG)
  -> owning driver
  -> no blind replay
  -> protocol ACK
  -> optional readback
~~~

No protocol object crosses into Core/Engineering public contracts.

## 42. Checkpoint 2 decisions

### Batch planner

**GO**

- mandatory contiguous batching;
- hard protocol limits are ceilings, not operational targets;
- profile/soft-cap segmentation;
- no typed-point splitting.

### Random/block reads

**GO_AS_OPTIMIZATION**

### Random/block write coalescing

**NO for independent Runtime.WriteAsync effects**

### TCP session

**PERSISTENT / ONE PER DATASOURCE / ONE OUTSTANDING REQUEST**

### Parallel pipeline

**NO for 3E v1**

### Read retry

**BOUNDED YES**

### Write retry

**NO after possible dispatch**

### Ambiguous timeout

**UNKNOWN -> RESET -> READBACK IF POSSIBLE -> NEVER BLIND REPLAY**

### PointRead

**YES / shared contract**

### ConnectionTest

**YES / Read Type Name preferred bounded identity probe**

### Browse/discovery

**NO generic protocol browse/discovery in v1**

### Security

**PLAINTEXT PROTOCOL PROFILE / TRUSTED OT NETWORK + FIREWALL/VPN**

### Dependency

**BUILT-IN .NET / NO NEW PACKAGE**

### Preliminary product decision

**MITSUBISHI_MELSEC = GO_WITH_GATES**

Remaining gates are now primarily:

- L0-L4 proof;
- exact hardware/model/firmware profile;
- independent peer strategy;
- legal/trademark final review;
- final fresh-source revalidation.

## 43. Checkpoint 2 boundary

This checkpoint intentionally does not complete:

- L0 test-vector list;
- L1 fake peer implementation contract;
- L2 independent peer selection;
- L3 full EliteSCADA acceptance matrix;
- L4 hardware shortlist/purchase recommendation;
- trademark/legal final review;
- final production identity approval.

Those belong to Checkpoint 3 after a new **SIGA**.

**DOCS_ONLY**

**NO PRODUCT CODE CHANGED**

**NO DEPENDENCY CHANGED**

**NO CI CHANGED**

**NO MERGE PERFORMED**

**HA = High Availability**

**HAB = Home Assistant Bridge**

