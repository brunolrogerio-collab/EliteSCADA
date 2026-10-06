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
