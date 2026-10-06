# Panasonic MEWTOCOL — Execution Research

**Issue:** #553 — INDUSTRIAL-RESEARCH-02  
**Parent:** #551 — INDUSTRIAL-PLC-ROADMAP  
**Contract:** `C-INDUSTRIAL-PANASONIC-MEWTOCOL-RESEARCH-01`  
**Order:** `INDUSTRIAL-PANASONIC-MEWTOCOL-EXECUTION-RESEARCH-01`  
**Checkpoint:** 1-2 complete; 3 — L0-L4 + hardware + legal + final v1 recommendation  
**Research date / source access:** 2026-10-06  
**Branch:** `research/industrial-panasonic-mewtocol`  
**Release base:** `wave15/corrections-integration@d2569990bc53dfce61ca8043471719e958809d6c`

## 1. Boundary

This checkpoint is research/documentation only.

`RESEARCH_ONLY / DOCS_ONLY / NO_PRODUCT_CODE / NO_MERGE`

No source, test, web, schema, dependency, CI, Runtime, Driver SDK, or product behavior change is authorized or performed here.

The future Panasonic driver must converge into the existing EliteSCADA path:

`Panasonic PLC -> Driver/Data Source -> canonical TAG/Command -> CurrentTagCache -> Historian/Alarm/Gateway/Scripts/Realtime`

No Panasonic-specific Runtime, Historian, Alarm Engine, or diagnostics authority is justified.

## 2. Live repository revalidation

At checkpoint start, GitHub live reported:

- `wave15/corrections-integration = d2569990bc53dfce61ca8043471719e958809d6c`;
- `research/industrial-panasonic-mewtocol = d2569990bc53dfce61ca8043471719e958809d6c`;
- merge-base = the same SHA;
- ahead = 0;
- behind = 0.

Read-only audit confirmed that future implementation must reuse current product contracts:

- current Driver/Data Source descriptor and Engineering tooling;
- canonical TAG/Command and `Runtime.WriteAsync(TAG)` authority;
- optional/non-blocking `PointReadTest` with raw/decoded/Engineering/quality/latency evidence;
- common `CommunicationDriverDiagnosticSnapshot` authority from #500;
- host-owned serial infrastructure from #469, including `DriverConfigurationValueKind.SerialPort` and `HostSerialPort`;
- existing lifecycle and HA external-effect authority.

Canonical terminology remains:

- `HA = High Availability`
- `HAB = Home Assistant Bridge`

## 3. Official source register

Only Panasonic official material is treated as protocol/product authority.

| Source | Version/date used | Checkpoint use |
|---|---|---|
| MEWTOCOL Communication User's Manual | `WUME-MEWCP-03`, 2024.4; Panasonic Europe listing 2024-04-09 | MEWTOCOL-COM frame, BCC, commands, address notation; MEWTOCOL-DAT distinction |
| FP-XH User's Manual (Communication) | `WUME-FPXHCOMG-042`; Panasonic Europe listing 2024-10-30 | FP-XH COM commands, Ethernet AFPX-COM5, TCP/UDP/port/client limits |
| FP-XH User's Manual (Basic) | `WUME-FPXHBASG-061`; Panasonic Europe listing 2024-06-12 | FP-XH operation-memory ranges |
| FP0R User's Manual | `WUME-FP0R-03 (2025.10)`; Panasonic Europe listing 2025-12-18 | current FP0R family/manual status; serial product context |
| FP7 CPU Unit User's Manual — Hardware | `WUME-FP7CPUH-15`, 2024.4 | current FP7 R-series CPU models and operation-memory ranges |
| FP7 CPU Unit User's Manual — LAN Port Communication | `WUME-FP7LAN-09`; Panasonic Europe listing 2025-01-24 | MEWTOCOL-COM/MEWTOCOL7-COM/MEWTOCOL-DAT LAN behavior and frame differences |
| FP7 SCU Communication User's Manual | `WUME-FP7COM-81 (2025.06)`; Panasonic Europe listing 2025-12-06 | current FP7 serial family context |
| Panasonic FP0R product page | accessed 2026-10-06 | current family and RS-232/RS-485 variants |
| Panasonic FP-XH product page | accessed 2026-10-06 | current family, communication options, current manuals |
| Panasonic FP7 product page | accessed 2026-10-06 | current R-series CPUs, integrated Ethernet variants |
| Panasonic FP-X product/portfolio material | official material; FP-X discontinued September 2021 | legacy classification |

Official URLs:

- https://industry.panasonic.eu/storage/custom-upload/Factory%20%26%20Automation/PLC/Manuals/mn_all_plcs_mewtocol_user_pid_en.pdf
- https://industry.panasonic.eu/storage/download-files/import/mn_fpxh_communication_user_pid_en.pdf
- https://mediap.industry.panasonic.eu/assets/download-files/import/mn_fpxh_user_basic_pidsx_en.pdf
- https://industry.panasonic.eu/products/automation-devices-solutions/programmable-logic-controllers-plc/fp-series-plcs/plc-fp0r
- https://industry.panasonic.eu/products/automation-devices-solutions/programmable-logic-controllers-plc/fp-series-plcs/plc-fp-xh
- https://industry.panasonic.eu/products/automation-devices-solutions/programmable-logic-controllers-plc/fp-series-plcs/plc-fp7
- https://mediap.industry.panasonic.eu/assets/download-files/import/mn_fp7_cpu_hardware_pid_en.pdf
- https://industry.panasonic.eu/storage/download-files/import/mn_fp7_cpu_lan_user_pid_en.pdf

## 4. Protocol landscape

### 4.1 MEWTOCOL-COM

MEWTOCOL-COM is Panasonic's host/computer-link request/response protocol. A computer sends a command; the PLC returns the response. It does not require a PLC user program merely to service normal computer-link commands.

Key wire facts from the official manual:

- ASCII text framing;
- command headers `%` or `<`;
- `#` identifies command text;
- normal response uses `$`; error response uses `!`;
- terminator is CR;
- BCC is two ASCII hexadecimal characters calculated by XOR over the frame from header through the last text character;
- command-side `**` may be accepted in place of BCC where the manual permits, but the future driver should generate/verify the real BCC;
- standard `%` frame maximum is 118 characters;
- expanded `<` frame maximum is 2048 characters;
- read/write command families include contact reads/writes and data-area reads/writes;
- contact notation for `X/Y/R/L` uses a hexadecimal final digit with decimal digits before it; `T/C` uses decimal contact numbering.

The generic protocol manual shows historical/general unit-address constraints, while current family manuals allow station ranges up to 1–99. Therefore station validation must be family/profile-aware. Broadcast/no-response forms must not be used for ordinary read semantics.

**Checkpoint disposition:** `V1`.

### 4.2 MEWTOCOL7-COM

MEWTOCOL7-COM is **not** a synonym or wire-compatible alias for MEWTOCOL-COM.

Official FP7 documentation shows materially different framing:

- header `>`;
- station field represented as `@ddd`;
- frame number;
- CRC-16-CCITT text check rather than classic MEWTOCOL-COM XOR BCC;
- maximum frame length up to 4096 characters;
- FP7 documentation exposes `MMRD` and `MMWT` data-area operations;
- official FP7 communication documentation states MEWTOCOL7-COM does not support the master function.

This dialect exists to serve FP7-era communication/addressing needs and must not be parsed through the classic COM codec by a mode bit.

**Checkpoint disposition:** `LATER / SAME PRODUCT PROFILE CANDIDATE`.

It does not justify a second product identity in v1.

### 4.3 MEWTOCOL-DAT

MEWTOCOL-DAT is a separate binary data-transfer procedure, with its own LAN/special header and binary command model. It is not the ordinary ASCII computer-link protocol.

The generic manual documents DAT read/write command bytes and larger binary transfers. FP7 LAN documentation treats DAT as a distinct selectable communication mode beside MEWTOCOL-COM and MEWTOCOL7-COM.

**Checkpoint disposition:** `NOT_V1`.

Do not silently route `panasonic.mewtocol` through DAT as if it were merely an optimized COM frame.

## 5. Product-shape decision

### Decision

Keep one product identity:

`panasonic.mewtocol`

with an explicit protocol profile/dialect field.

Initial dialect:

`mewtocol-com`

Reserved future dialect after a separate acceptance gate:

`mewtocol7-com`

Rejected for v1:

- separate `panasonic.mewtocol7` product;
- implicit automatic wire-dialect guessing;
- a DAT-backed implementation marketed as MEWTOCOL-COM.

### Rationale

Classic MEWTOCOL-COM is the smallest technically true common denominator across the selected current/installed Panasonic FP families. MEWTOCOL7-COM has a different wire contract but remains semantically a Panasonic memory communication profile and can be represented later as an explicit dialect without duplicating Runtime/TAG/diagnostic authorities.

The final Driver SDK representation of one identity with both TCP and Host Serial profiles must be validated in Checkpoint 2. If the current descriptor metadata cannot truthfully express both connection models without ambiguity, report `RESEARCH_CONTRACT_DELTA_REQUIRED`; do not change the SDK inside this research lane.

## 6. Ethernet v1

**Decision:** Ethernet/TCP is in v1.

The v1 client direction is:

`EliteSCADA TCP client -> Panasonic MEWTOCOL-COM server endpoint`

No global Panasonic TCP port constant is valid.

### FP-XH + AFPX-COM5

Official FP-XH communication documentation shows:

- TCP or UDP selectable;
- server or client connection modes;
- Computer Link as a selectable communication mode;
- server/source port configurable in 1025–32767;
- default shown as 9094;
- response timeout configurable, with 5000 ms default for computer-link examples;
- idle timeout configurable;
- up to three connections can be established on the specified source-port configuration.

For EliteSCADA v1: use TCP only, explicit host + port, and do not hard-code 9094.

### FP7

FP7 LAN configuration exposes multiple user connections and per-connection operating/open/protocol modes. Current R-series product material advertises much greater user-connection capacity than older FP7 LAN examples. Therefore connection-count limits are model/firmware/configuration capabilities, not a universal driver constant.

For EliteSCADA v1: use explicit host + port and a server/open profile supported by the target CPU configuration. Exact persistent-session/concurrency behavior belongs to Checkpoint 2.

### Ethernet v1 invariant

`endpoint = host + port + station + dialect + family/profile`

A family-specific default may assist authoring, but persisted configuration must remain explicit.

## 7. Serial v1

**Decision:** Serial is in v1, not deferred.

Reason: FP0R is a current compact family whose official product/manual material emphasizes RS-232C/RS-485 MEWTOCOL communication. Excluding serial would make the first native driver materially less useful for a current Panasonic installed base.

Official Panasonic material supports:

- RS-232C;
- RS-485;
- multidrop use where the family/adapter supports it;
- 7/8 data bits;
- None/Odd/Even parity;
- 1/2 stop bits;
- family-dependent baud rates up to 115200 or 230400 where documented;
- station numbering, normally 1–99 on selected modern families;
- classic COM BCC at the protocol frame layer.

Mandatory EliteSCADA reuse:

`#469 Host Serial`

No second COM/tty enumeration, serial-port catalog, bus coordinator, or browser-owned serial implementation is allowed.

Serial multidrop station identity belongs to the Data Source/session configuration, not to an arbitrary unparsed TAG address string.

## 8. Family scope

Detailed ranges and evidence are in `PANASONIC-MEWTOCOL-ADDRESS-AND-FAMILY-MATRIX.md`.

Checkpoint 1 v1 family decision:

| Family | Product status at research | V1 | Transport/profile |
|---|---|---:|---|
| FP0R | current compact family | YES | Host Serial / MEWTOCOL-COM |
| FP-XH | current family | YES | Host Serial and TCP via supported Ethernet hardware/profile / MEWTOCOL-COM |
| FP7 current R-series | current modular family | YES, bounded | TCP and supported serial hardware / MEWTOCOL-COM; extended address space gated |
| FP-X | legacy/discontinued since Sep 2021 | NO initial claim | research matrix only; later installed-base validation |

Current FP7 R-series CPUs include `AFP7CPS3RE` and `AFP7CPS4RE` with integrated Ethernet. The older FP7 model numbers remain important installed-base evidence but must not be conflated with the current R-series hardware identity.

FP-XH L4 candidate direction: `AFPXHC14RD` plus `AFPX-COM5`, or a current FP-XH Ethernet-capable variant if selected later.

FP0R L4 candidate direction: a current F32 variant with native RS-485/RS-232C, e.g. `AFP0RF32MT` or `AFP0RF32CT`.

FP7 L4 candidate direction: current Ethernet CPU such as `AFP7CPS3RE`.

Exact L4 procurement is intentionally deferred to Checkpoint 3.

## 9. Bounded address grammar

### 9.1 Canonical normalization

The future codec must parse a typed address AST and only then build a wire command.

Rules:

- trim is **not** silent: leading/trailing whitespace is invalid;
- ASCII device mnemonics are case-normalized to uppercase;
- no arbitrary suffixes, separators, signed numbers, or free-form wire fragments;
- station is Data Source/session configuration in v1, not a TAG string prefix;
- ranges are validated against selected family/CPU/memory configuration;
- incompatible family/device combinations fail before network I/O.

### 9.2 V1 forms

Native contact devices:

- `X<addr>`
- `Y<addr>`
- `R<addr>`
- `L<addr>`
- `T<decimal>`
- `C<decimal>`

For `X/Y/R/L`, the final digit is hexadecimal `0..F`; preceding digits are decimal as defined by Panasonic notation. `T/C` are decimal.

Word devices:

- `WX<decimal>`
- `WY<decimal>`
- `WR<decimal>`
- `WL<decimal>`
- `DT<decimal>`
- `LD<decimal>`

Deferred/family-specific forms:

- `SV`, `EV` — classic timer/counter value areas, not uniform with FP7 32-bit timer/counter devices;
- `TS`, `TE`, `CS`, `CE` — FP7 32-bit timer/counter value areas;
- `FL` — only for families where current official evidence proves the area and protocol reachability;
- special/system devices such as special R/DT, `SR`, `SD`, `UM`, `IN`, `OT`, `P`, `E` — not v1.

### 9.3 Bit-in-word

V1 does **not** accept a generic `DT100.3`-style bit-in-word grammar.

Boolean v1 addressing uses native Panasonic contact areas. A future `.<bit>` selector may be added only after its semantics are deliberately mapped to the canonical EliteSCADA bit selector and validated per writable/read-only area.

### 9.4 Access policy

Initial conservative policy:

- `X/WX`: read-only;
- `Y/WY`: read/write where the PLC/profile permits;
- `R/WR`: read/write;
- `L/WL`: read/write only where link-area semantics are deliberately accepted;
- `DT/LD`: read/write;
- `T/C`: contact read first; write semantics deferred;
- timer/counter value devices: deferred.

## 10. Memory and data-type model

Native storage and EliteSCADA Engineering type are separate concepts.

### 10.1 Wire/native storage

Classic MEWTOCOL-COM data-area operations expose 16-bit word storage. Official FP-XH examples show a value of decimal 100 (`0x0064`) returned as ASCII `6400`, proving low byte then high byte within that 16-bit word representation.

This is sufficient to define **intra-word byte order** for v1 word decoding.

It is **not** sufficient to assume the ordering of two adjacent words for every Panasonic 32-bit/REAL representation.

### 10.2 V1 Engineering type baseline

| Engineering type | V1 disposition | Reason |
|---|---|---|
| Boolean | YES | native contact areas |
| UInt16 | YES | one 16-bit word |
| Int16 | YES | one 16-bit word, two's-complement interpretation |
| UInt32 | GATED | requires authoritative adjacent-word order + L0/L4 proof |
| Int32 | GATED | same |
| Float32 | GATED | same, plus IEEE/PLC representation proof |
| Float64 | LATER / NOT YET JUSTIFIED | no need proven for first bounded product |
| BCD | LATER | only if concrete Panasonic area/application evidence requires it |
| String | LATER | Engineering overlay over words; no v1 native typed string claim |
| Byte array | LATER | not exposed as arbitrary wire bytes in v1 |
| Word array/block | YES internally for batching; TAG array surface LATER | planner may use contiguous words without exposing arbitrary array TAG type |

### 10.3 Multiword gate

Do not copy Modbus word-order defaults.

Before enabling `Int32/UInt32/Float32` as supported v1 TAG types, the implementation dossier must record official Panasonic ordering evidence and deterministic fixtures. Until then, v1-safe support is Boolean + 16-bit signed/unsigned.

## 11. Timers and counters

Cross-family timer/counter storage is not uniform:

- FP0R/FP-XH expose `T/C` contacts and classic `SV/EV` word-oriented value areas;
- current FP7 exposes `T/C` contacts but `TS/TE/CS/CE` as 32-bit double-word areas.

Classification:

- `T/C contacts = V1_READ`;
- writes to T/C contacts = `LATER`;
- `SV/EV = LATER`;
- `TS/TE/CS/CE = LATER`;
- automatic cross-family timer/counter abstraction = `NOT_JUSTIFIED` for v1.

This avoids creating a false universal Panasonic timer model.

## 12. Checkpoint 1 answers

1. **MEWTOCOL-COM vs MEWTOCOL7-COM:** distinct wire dialects; classic COM is ASCII `%/<` + XOR BCC; MEWTOCOL7 uses `>`, `@ddd`, frame number and CRC-16-CCITT. They must not share a parser path by assumption.
2. **V1 dialect:** `MEWTOCOL-COM`.
3. **Ethernet v1:** YES — TCP, explicit endpoint/port, family-specific profile; no universal fixed port.
4. **Serial v1:** YES — mandatory reuse of #469 Host Serial.
5. **V1 families:** FP0R, FP-XH, current FP7 R-series (bounded COM-reachable subset). FP-X is legacy/LATER.
6. **V1 memory areas:** X/Y/R/L, WX/WY/WR/WL, DT, LD with conservative access policy; timer/counter contacts read-only. Special/system/extended/timer-value areas deferred.
7. **V1 data types:** Boolean, UInt16, Int16. UInt32/Int32/Float32 are gated on authoritative multiword ordering evidence; Float64/BCD/string/byte-array are later unless justified.
8. **Address grammar:** strict typed grammar with uppercase device normalization, Panasonic contact radix rules, decimal word indexes, station outside the TAG address, family/CPU bounds, and no arbitrary strings passed to the wire codec.

## 13. Uncertainties / gates

1. **32-bit/REAL word ordering:** intra-word byte order is evidenced; adjacent-word ordering for COM must be resolved before enabling multiword Engineering types.
2. **FP7 extended memory reachability under classic COM:** FP7 R-series has operation memory far beyond older COM address formats. The future capability matrix must define a bounded COM-reachable subset; MEWTOCOL7 is not silently enabled to bypass that gate.
3. **Single driver identity across TCP + Host Serial:** the product identity is technically coherent, but Checkpoint 2 must confirm the current Driver SDK descriptor/configuration surface can represent both transports truthfully without a shared-contract change.

No product-contract delta is implemented by this checkpoint.

If item 3 proves impossible without changing the shared SDK, report:

`RESEARCH_CONTRACT_DELTA_REQUIRED`

before any product code.

## 14. Preliminary decision

`PANASONIC_MEWTOCOL = GO_WITH_GATES`

The native driver is justified. The first bounded product should be classic MEWTOCOL-COM over TCP + Host Serial for FP0R, FP-XH and a bounded current FP7 profile, with strict address validation and no claim of universal Panasonic PLC support.

MEWTOCOL7-COM and MEWTOCOL-DAT are not first-driver blockers and are not v1.

## 15. Checkpoint stop

Checkpoint 1 is complete after this document and the address/family matrix are committed and the #553 checkpoint comment is published.

Do **not** start planner/session/process-truth/PointRead/FP7-MC implementation research until a new Main/Product Owner `SIGA`.

`DOCS_ONLY`  
`NO PRODUCT CODE CHANGED`  
`NO DEPENDENCY CHANGED`  
`NO CI CHANGED`  
`NO MERGE PERFORMED`


# Checkpoint 2 — Planner, Transport, Process Truth, PointRead, Security, Architecture and FP7 MC

**Checkpoint date / source access:** 2026-10-06  
**Checkpoint start HEAD:** `532d0fa999e64d30965d8a91673197bfe39dd996`  
**Checkpoint start ancestry:** `ahead 2 / behind 0` against release base `d2569990bc53dfce61ca8043471719e958809d6c`

This section is still:

`RESEARCH_ONLY / DOCS_ONLY / NO_PRODUCT_CODE / NO_MERGE`

## 16. Checkpoint 2 live product-contract audit

The current EliteSCADA Driver SDK is transport-aware, but its public descriptor exposes a **single optional `DriverConnectionModel` per driver type**:

- `DirectNetwork`
- `HostSerial`
- `HostRadio`
- `LocalBridge`
- `Cloud`

The descriptor also exposes a single configuration schema, plus `SupportsSharedTransportInfrastructure`.

The integrated Modbus family already demonstrates the product pattern:

- `modbus.tcp` -> `DirectNetwork`
- `modbus.rtu` -> `HostSerial`
- shared Modbus address/value concepts underneath
- `modbus.rtu` reuses `HostSerialBusCoordinator` and `DriverConfigurationValueKind.SerialPort`

The generic Engineering web contract mirrors one `connectionModel` value and one unconditional Data Source field set.

### Checkpoint 2 product-identity refinement

The Checkpoint 1 semantic product family remains correct:

`Panasonic MEWTOCOL`

However, **one literal driver type ID carrying both TCP and Host Serial cannot be represented truthfully by the current generic descriptor without either conditional transport profiles or misleading metadata**.

Therefore the recommended no-shared-contract-change shape is:

- `panasonic.mewtocol.tcp`
- `panasonic.mewtocol.serial`

with:

- one shared MEWTOCOL-COM codec;
- one shared address parser/model;
- one shared TAG-binding schema;
- common family/profile capabilities;
- transport-specific Data Source schemas;
- common Runtime/TAG/PointRead/diagnostics semantics.

This is the same conceptual product family, not two independent Panasonic products.

MEWTOCOL7-COM remains a later dialect/profile gate. It does **not** become a third first-class product merely because its frame is different.

### Required Main decision

`MAIN_DECISION_REQUIRED`

Main must freeze one of:

A. **Recommended:** transport-specific driver type IDs above, no shared Driver SDK change.

B. Keep one exact type ID `panasonic.mewtocol` across both transports, but authorize a shared descriptor/UI extension for explicit alternative connection profiles + conditional field schemas.

If B is selected:

`RESEARCH_CONTRACT_DELTA_REQUIRED`

This research lane does not implement either contract change.

## 17. MEWTOCOL-COM read/write command surface

Official Panasonic MEWTOCOL-COM documentation exposes a richer command set than the first driver needs.

Relevant command families include:

- `RCS` — single contact read;
- `RCP` — multiple individually specified contact read, 1..8 contacts;
- `RCC` — contiguous contact-range read;
- `WCS` — single contact write;
- `WCP` — multiple individually specified contact write, 1..8 contacts;
- `WCC` — contiguous contact-range write;
- `RD` — contiguous data-area read;
- `WD` — contiguous data-area write;
- `RS/WS` — set-value area operations where supported;
- `RK/WK` — elapsed/current value area operations where supported;
- `MC/MD/MG` — monitor registration/change/monitor family;
- `RT` — PLC status/model/version/program-capacity/status information;
- other maintenance/control commands that are outside the first Runtime polling product.

### 17.1 V1 command subset

Recommended v1:

**Reads**
- `RCS`
- `RCP` where sparse contact reads actually reduce traffic;
- `RCC` for contiguous contact regions;
- `RD` for contiguous word/data regions;
- `RT` as optional Engineering identity/status probe.

**Writes**
- `WCS`
- `WCP` only for an explicit multi-point operation, not to opportunistically merge unrelated Runtime writes;
- `WCC` only where a deliberate contiguous contact write operation exists;
- `WD` for one logical word/multiword value or deliberately bounded block operation.

Deferred:
- `RS/WS`
- `RK/WK`
- monitor registration `MC/MD/MG`
- remote-control/program-maintenance commands.

### 17.2 Why monitor registration is not the v1 scan planner

The monitor command family creates PLC-side registered state and has its own registration/error lifecycle.

Normal EliteSCADA polling can be implemented with stateless bounded reads and the existing scan planner authority. Introducing monitor registration in v1 would create extra PLC-side state without a demonstrated product benefit.

Disposition:

`MEWTOCOL_MONITOR_COMMANDS = LATER / NOT_REQUIRED_FOR_V1`

## 18. Bounded read limits

Panasonic documentation provides two classic MEWTOCOL-COM frame classes:

- `%` standard frame, up to 118 characters;
- `<` expanded frame, up to 2048 characters.

Official Panasonic communication material for current FP-family controllers also documents large register transfers around:

- up to **509 words received/read**;
- up to **507 words transmitted/written**;

for the expanded computer-link path where the family/profile supports that frame.

The exact usable limit remains command/family/profile-dependent.

### 18.1 Planner capability values

Do not hard-code one global Panasonic batch constant.

The future profile table should carry bounded capabilities such as:

`maxFrameChars`
`maxReadWords`
`maxWriteWords`
`maxSparseContacts`
`supportsExpandedFrame`

For classic RCP/WCP:

`maxSparseContacts = 8`

For a profile proven to support expanded COM frames, a practical upper capability may be:

- read words: 509;
- write words: 507.

For short-frame-only operation, smaller limits must be derived and covered by L0 vectors before production constants are frozen.

## 19. Bounded Panasonic scan planner

The future driver must **not** perform `1 TAG = 1 request` by default.

### 19.1 Grouping keys

Build immutable scan groups by:

1. Data Source/runtime instance;
2. transport/session or physical Host Serial bus;
3. station;
4. dialect;
5. family/profile;
6. device area + compatible command family;
7. contiguous address range;
8. compatible native storage/layout;
9. read/write access policy.

Never batch across:

- stations;
- TCP endpoints;
- physical serial buses;
- dialects;
- incompatible device areas;
- family/profile range boundaries;
- address holes that would cross an invalid range.

### 19.2 Contact planner

Use:

- `RCS` for one contact;
- `RCP` for a small sparse set up to the documented 8-contact limit;
- `RCC` for a contiguous contact interval where the selected profile permits it.

The planner should prefer one contiguous read over many single-contact requests when it remains inside a valid device range.

### 19.3 Word planner

For `DT/LD` and word views such as `WX/WY/WR/WL`:

- sort by device area and starting word;
- form bounded contiguous intervals;
- split at profile maximum request/response size;
- split at address-space boundaries;
- decode each TAG from its position within the returned interval.

A conservative initial `maxGapWords = 0` is recommended. Reading unrequested gap words is side-effect free, but a nonzero merge gap can cross unimplemented/reserved/end-of-range regions and complicate compatibility claims. A future advanced bounded gap setting can be added only if performance evidence justifies it.

### 19.4 Write planner

Runtime writes remain command-oriented, not scan-batched.

Do not merge unrelated `Runtime.WriteAsync(TAG)` calls simply because their addresses are adjacent.

One logical multiword TAG write may use a single `WD` operation.

Any future explicit multi-point command may use WCP/WCC/WD as appropriate, but must preserve command ordering and process-truth semantics.

## 20. Ethernet/TCP session model

Official FP7 and FP-XH communication configuration supports TCP server/client connection modes, configurable ports and connection/idle settings. This supports persistent TCP usage.

Classic MEWTOCOL-COM frames do **not** carry a transaction identifier comparable to Modbus TCP MBAP.

### 20.1 Recommended client session model

For EliteSCADA TCP client mode:

- establish a TCP connection;
- reuse it across polling cycles while healthy;
- allow exactly **one outstanding MEWTOCOL-COM request per connection**;
- match the next valid response to the only outstanding request;
- do not pipeline requests;
- parse until the complete CR-terminated frame and validate BCC;
- close/reconnect after framing loss, EOF, unrecoverable socket error or ambiguous request timeout;
- tolerate PLC-configured idle close by reconnecting cleanly on the next required request.

The one-outstanding-request rule is an engineering conclusion from the official framing/request-response model and lack of transaction identity. It is not presented as a Panasonic quotation.

### 20.2 Timeout/reconnect

Reuse EliteSCADA's existing bounded timeout/reconnect/backoff conventions.

Recommended product defaults may align with current communication-driver patterns:

- scan interval around 1000 ms;
- request timeout around 3000 ms;
- configurable bounded timeout;
- exponential reconnect backoff with a maximum and small jitter;
- cancellation tied to Data Source/Active Revision disposal.

Panasonic manuals contain their own controller-side timeout defaults for some profiles; those are not a reason to hard-code a single host timeout.

No Panasonic-specific global retry framework is justified.

### 20.3 Concurrency

Do not invent socket-level parallelism.

If a PLC/profile permits multiple independent TCP connections, separate Data Sources/sessions can use their own connections subject to PLC resource limits. One connection still has one outstanding COM transaction.

Current FP7 connection capacity differs across CPU generation/configuration. Capacity is model/firmware/profile metadata, not a universal product constant.

## 21. Host Serial scheduler

Serial v1 must reuse:

`HostSerialBusCoordinator`

The integrated host transport already:

- owns physical server-visible COM/tty devices;
- validates line settings;
- arbitrates shared-master ownership;
- serializes transactions through the bus lease;
- prevents browser-owned serial authority.

### 21.1 Panasonic serial scheduling

For MEWTOCOL-COM serial:

- physical bus key = host serial device;
- line settings must be compatible across sharers;
- station identifies the target PLC;
- one outstanding transaction on the bus;
- write complete request frame;
- flush when required by host transport;
- read the complete CR-terminated response;
- validate station/command/response marker/BCC;
- release transaction serialization before the next owner request.

Do not open the same physical port independently for every Panasonic Data Source.

### 21.2 Turnaround / wait

Panasonic communication configuration exposes send-wait/turnaround parameters on applicable serial profiles.

The host should not invent a fixed delay when the target does not require one.

If a device/profile needs it, expose a bounded advanced turnaround/wait parameter and include it in the serial profile capability.

### 21.3 Timeout and ambiguous bus state

After serial timeout or malformed/truncated response:

- mark the transaction failed/ambiguous;
- reset parser state;
- discard stale input only at a controlled recovery boundary;
- if necessary, reopen/reacquire the bus session;
- do not immediately send a second write as a blind retry.

## 22. Protocol errors and quality mapping

Official MEWTOCOL-COM responses distinguish normal `$` replies from `!` error replies and expose protocol error codes.

Important documented classes include:

- BCC/check error;
- command/frame format error;
- unsupported command;
- multi-frame procedure error;
- link/configuration error;
- response/transmission timeout/buffer conditions;
- busy state;
- parameter/address/range/data-format error;
- monitor registration state error;
- PLC mode restriction;
- memory/protection restriction.

The future driver must preserve the original Panasonic code in sanitized `ProtocolDetails` while mapping it into canonical EliteSCADA quality/operation status.

Do not collapse every `!` reply into "timeout" or "offline".

Examples of intended mapping categories:

- malformed/BCC response -> communication/protocol error;
- invalid configured address/range -> configuration/device-address error;
- PLC busy/transient execution rejection -> degraded/bad-device with exact protocol code;
- timeout/no response -> bad communication / reconnect path;
- protection/mode denial -> operation rejected, not connectivity failure.

## 23. WRITE process truth

Write truth must be layered.

### 23.1 Truth levels

1. **Local socket/serial write completed**
   - proves bytes were handed to the local transport;
   - does not prove the PLC received them.

2. **Valid Panasonic normal response (`$...`)**
   - proves the protocol request was processed sufficiently for the PLC to issue a normal command response;
   - is the first acceptable protocol-level success point.

3. **PLC memory readback matches**
   - proves a subsequent read observes the requested memory value;
   - stronger than protocol ACK, but still not physical-process proof.

4. **Physical feedback TAG changes**
   - proves the field/process feedback changed when a distinct feedback signal actually represents that truth.

Never present level 2 or 3 as automatic proof that a motor, valve, contactor or other physical process changed.

### 23.2 Ambiguous timeout after dispatch

If bytes may have reached the PLC and the response is lost:

`WRITE_RESULT = UNKNOWN`

Do not blindly resend a write after an ambiguous timeout.

A retry is only trivially safe when failure is proven **before dispatch**. Post-dispatch retry requires an operation-specific idempotency/process-risk decision.

Even an absolute memory write can have operational consequences through PLC logic. "Writing the same value again" is therefore not universally harmless.

### 23.3 Readback

For an operator/process write where confirmation matters:

- issue the write once;
- receive normal protocol response;
- perform bounded readback of the addressed PLC memory when configured/appropriate;
- expose readback mismatch as uncertainty/failure evidence;
- rely on a distinct physical feedback TAG for actual process confirmation.

Readback must not become an infinite retry loop.

### 23.4 EliteSCADA write path

Ordinary writes must stay:

`Runtime.WriteAsync(TAG) -> owning Panasonic runtime provider -> protocol write`

No raw MEWTOCOL command API should be exposed to browser scripts or ordinary Runtime clients.

## 24. PointRead and Engineering

### 24.1 Required v1 Engineering model

MEWTOCOL-COM does not provide an OPC-UA-style symbolic address-space browse contract.

Therefore truthful first Engineering can be:

- family/profile selection;
- transport-specific Data Source form;
- typed address assistant;
- exact station validation;
- optional Connection Test;
- optional `RT` identity/status probe;
- canonical `PointReadTest`;
- normal Preview/Apply/Save/Publish/Activate lifecycle.

Do not invent symbolic browse.

### 24.2 PointReadTest

Reuse the existing canonical `DriverPointReadTestRequest/Result`.

A Panasonic provider should return where applicable:

- sanitized endpoint or serial port;
- portable canonical address;
- raw frame or raw word/contact representation;
- decoded native value;
- Engineering value;
- quality;
- source/observed timestamp where meaningful;
- latency;
- effective value transform;
- exact Panasonic protocol error/status code.

PointRead remains:

`OPTIONAL / NON-BLOCKING / NEVER A LIFECYCLE GATE`

A PLC may be offline while Engineering configuration is still valid and saveable.

### 24.3 PLC identity/status probe

The official `RT` command can return PLC model/status information including model code/version/program-capacity/operation state fields.

The future tooling may expose that as observed identity/status.

Do not convert a protocol model code into a precise commercial SKU unless an official mapping for that CPU generation is implemented and validated.

## 25. Common diagnostics

Reuse:

`CommunicationDriverDiagnosticSnapshot`

No `PanasonicDiagnostics` authority.

Suggested sanitized `ProtocolDetails` keys:

- `transport` = `tcp` or `serial`;
- `dialect` = `mewtocol-com`;
- `station`;
- `familyProfile`;
- `endpoint` or `serialPort`;
- `observedModelCode`;
- `observedVersion`;
- `lastProtocolCommand`;
- `lastProtocolErrorCode`;
- `lastProtocolErrorName`;
- `lastBatchWords`;
- `lastBatchContacts`;
- `lastRttMilliseconds`;
- `bccErrorCount`;
- `frameMode` = standard/expanded when useful.

Use common counters for:

- requests;
- success/failure;
- consecutive failures;
- timeouts;
- connections/disconnections;
- reconnects;
- reads/writes;
- published TAG updates.

Do not duplicate counters in a private Panasonic health model.

## 26. Security and deployment

### 26.1 Native protocol reality

The documented MEWTOCOL-COM frames are plaintext ASCII over serial/TCP and contain no session-level encryption or TLS negotiation.

The protocol also does not provide a modern cryptographic peer-authentication handshake.

PLC protection/mode restrictions can reject certain operations, but they are **not transport confidentiality/integrity/authentication**.

Therefore:

`MEWTOCOL_COM_SECURITY = PLAINTEXT_LEGACY_OT_PROTOCOL`

### 26.2 Deployment posture

Recommended v1 guidance:

- trusted/segmented OT LAN for TCP;
- local controlled serial bus for RS-232/RS-485;
- firewall allow only required EliteSCADA-to-PLC endpoints;
- do not expose MEWTOCOL-COM directly to the public Internet;
- for remote/WAN access, use an authenticated VPN/leased/private network boundary;
- follow Panasonic network-security guidance for access limitation and firewalling.

Containers inherit the existing #469 rule: only serial devices mapped into the server/container are visible.

## 27. Implementation architecture choice

### A. Built-in .NET codec/session

**RECOMMENDED.**

Reasons:

- bounded ASCII request/response framing;
- simple XOR BCC;
- exact typed address parser required anyway;
- direct reuse of EliteSCADA TCP conventions and #469 Host Serial;
- no need for a vendor runtime/sidecar;
- one-outstanding-request session model is small and testable;
- avoids binding canonical contracts to a community library's device model;
- leaves a clean future seam for a separate MEWTOCOL7 codec/profile.

### B. Third-party library

Not recommended as production dependency for v1.

One current community implementation reviewed is `OpenLogics/MewtocolNet` / `Mewtocol.NET`.

Research findings:

- TCP and serial support exist;
- project activity resumed/current repository activity is visible in 2026;
- README still describes incomplete/tested-device limitations and explicitly lacks FP7 support;
- public GitHub repository currently reports GPL-3.0 licensing;
- NuGet 0.8.1 metadata reports MIT and is older.

That license/metadata mismatch alone requires legal review before reuse, and the FP7 gap conflicts with the selected first-family scope.

Disposition:

`NO_PRODUCTION_DEPENDENCY`

It may be considered later as an **independent-shaped L2 comparison peer** only if legal/tool-use terms permit and only where its supported PLC profile is relevant.

No dependency is added by this research.

### C. Sidecar

**REJECT for v1.**

No protocol/vendor-runtime constraint justifies adding:

- another process;
- lifecycle supervision;
- deployment packaging;
- IPC;
- extra failure modes.

### D. Modbus-only / no native driver

**REJECT as product strategy.**

Selected Panasonic FP families have native MEWTOCOL installed-base value and native address/status semantics. Modbus remains a valid optional alternate integration where a target PLC/profile is configured for it, but it does not eliminate the native-driver case.

## 28. FP7 MC Protocol interoperability — mandatory analysis

FP7 MC Protocol support is real, but it is a **bounded Mitsubishi-compatible interoperability subset**, not Panasonic's native-driver identity.

Official Panasonic FP7 Ethernet documentation describes:

- MC Protocol QnA compatibility;
- **3E frame**;
- **binary only** for the documented FP7 profile;
- TCP/IP and UDP/IP;
- bulk read/write only.

### 28.1 Supported command subset

Official command subset:

**Bulk read**
- command `0401`
- subcommand `0001` for bit units
- subcommand `0000` for word units

**Bulk write**
- command `1401`
- subcommand `0001` for bit units
- subcommand `0000` for word units

This is not a universal Mitsubishi command set.

### 28.2 FP7 3E-header restrictions

The Panasonic slave profile constrains routing/header fields, including values equivalent to:

- network number = `00h`;
- PC number = `FFh`;
- destination unit I/O = `03FFh`;
- destination unit number = `00h`;
- CPU monitor timer field not supported by the FP7 subset.

Starting device addresses use the QnA-compatible 3-byte/6-hex-digit representation.

### 28.3 Bulk limits

Official FP7 MC slave documentation gives bounded bulk limits up to approximately:

- **7168 bits** per bulk operation;
- **960 words** per bulk operation.

These limits are specific FP7 interoperability evidence, not universal MELSEC limits.

### 28.4 Device mapping

The FP7 MC slave maps Mitsubishi-style device codes into Panasonic global memory.

Documented examples include:

| MC device | FP7 mapping / semantic target | Notes |
|---|---|---|
| `X` | FP7 X input area | bit/word mapping in documented range |
| `Y` | FP7 Y output area | bit/word mapping |
| `B` | FP7 L link relay | mapped interoperability area |
| `M` | FP7 R internal relay lower range | mapped interoperability area |
| `L` | FP7 R higher/latch range | mapped interoperability area |
| `D` | FP7 DT data register range | word |
| file-register `R` | FP7 DT extended range | word |
| `ZR` | FP7 DT extended range | word |
| `W` | FP7 LD link data | word |
| `TN` | FP7 TE timer elapsed/current | word subset |
| `TS` | FP7 T timer contact | contact/status semantics |
| `CN` | FP7 CE counter elapsed/current | word subset |
| `CS` | FP7 C counter contact | contact/status semantics |
| `SM` | FP7 SR special relay | restricted write semantics |
| `SD` | FP7 SD special data register | restricted write semantics |

Important restrictions:

- MC slave exposes global devices; local-device semantics are not universal;
- FP7 timer/counter current values are 32-bit internally, while the documented MC interoperability path handles only a 16-bit subset for those mapped values;
- values outside that subset cannot be treated as full-fidelity Panasonic timer/counter access.

### 28.5 Architectural decision

For the question "FP7 MC Protocol should be what?":

Primary answer:

**B — optional alternative only.**

Secondary validation role:

**C — useful interoperability hardware for the future Mitsubishi MC driver.**

It must **not** be:

A. part of the native Panasonic MEWTOCOL driver.

If EliteSCADA later supports FP7 through MC Protocol, the implementation should consume the **common Mitsubishi/MC provider/codec** when its semantics truly match the Panasonic 3E subset.

Do not copy or fork an MC codec under `Panasonic.Mewtocol`.

### 28.6 Cross-test value

FP7 is valuable future L4 hardware for proving that a common MC 3E client can interoperate with a non-Mitsubishi QnA-compatible device.

But:

`FP7_MC_SUBSET != UNIVERSAL_MITSUBISHI_MELSEC_IMPLEMENTATION`

Passing against FP7 proves only the documented 3E binary bulk read/write subset and mapped devices.

## 29. Source additions for Checkpoint 2

Official authority reviewed/revalidated:

- Panasonic MEWTOCOL Communication User's Manual, command/error tables;
- FP-XH User's Manual (Communication), current communication/session settings;
- FP7 CPU Unit User's Manual — LAN Port Communication, TCP connection modes, COM/COM7/DAT/MC choices and security chapter;
- FP7 CPU Unit User's Manual — Ethernet Expansion Function, MC Protocol chapter / QnA-compatible 3E subset;
- Panasonic communication-parameter documentation for MEWTOCOL station/serial settings.

Additional non-authoritative dependency research:

- `OpenLogics/MewtocolNet` GitHub repository;
- NuGet `Mewtocol.NET` package metadata.

Official Panasonic manual URLs already recorded above plus:

- https://mediap.industry.panasonic.eu/assets/download-files/import/mn_fp7_ethernet_expansion_user_pidsx_en.pdf

Community/dependency metadata:

- https://github.com/OpenLogics/MewtocolNet
- https://www.nuget.org/packages/Mewtocol.NET/0.8.1

## 30. Checkpoint 2 decisions

### Planner

`BOUNDED_CONTIGUOUS_GROUPING`

Group by Data Source/session/bus + station + dialect + family/profile + device area + layout. Split at official frame/address limits. No one-request-per-TAG default.

### Ethernet

`PERSISTENT_TCP / ONE_OUTSTANDING_REQUEST / NO_PIPELINE`

Reconnect on socket/framing/BCC/ambiguous-timeout recovery boundary.

### Serial

`HOST_SERIAL_REUSE / SHARED_BUS / SERIALIZED_TRANSACTIONS`

No new host serial authority.

### Process truth

`ACK != PHYSICAL_PROCESS_TRUTH`

Ambiguous post-dispatch write timeout = `UNKNOWN`; no blind write retry.

### PointRead

`REUSE_CANONICAL_POINTREAD / OPTIONAL_NON_BLOCKING`

Manual address + typed assistant + optional RT probe are sufficient; no fabricated symbolic browse.

### Diagnostics

`REUSE_#500_COMMON_DIAGNOSTICS`

Panasonic details go only into bounded sanitized protocol details.

### Security

`PLAINTEXT_LEGACY_OT_PROTOCOL / SEGMENTED_LAN_OR_LOCAL_SERIAL / VPN_FOR_REMOTE`

### Dependency choice

`BUILT_IN_DOTNET_CODEC_SESSION`

No new dependency.

### FP7 MC

`OPTIONAL_COMMON_MC_PROVIDER_ONLY / NOT_PANASONIC_NATIVE_SCOPE`

Useful future Mitsubishi interoperability hardware, but only for the documented subset.

### Driver-type shape

Recommended:

- `panasonic.mewtocol.tcp`
- `panasonic.mewtocol.serial`

Shared MEWTOCOL-COM core + shared TAG binding.

`MAIN_DECISION_REQUIRED` before DEV branch release.

## 31. Checkpoint 2 contract deltas / uncertainties

### Contract delta

No contract was changed.

Potential delta only if Main rejects transport-specific driver type IDs:

`RESEARCH_CONTRACT_DELTA_REQUIRED`

Affected contract:
current Driver SDK connection-model/configuration-schema metadata.

Options:
1. two transport-specific Panasonic driver type IDs — recommended;
2. extend shared SDK for alternative connection profiles — more churn.

### Still-open technical gates

1. authoritative Panasonic adjacent-word order for 32-bit/REAL remains unresolved;
2. exact classic COM-reachable FP7 extended-memory subset remains to be frozen;
3. exact L0/L1 independent test vectors and L2 tool strategy belong to Checkpoint 3;
4. final hardware SKUs/firmware and legal/trademark/dependency disposition belong to Checkpoint 3.

## 32. Preliminary decision after Checkpoint 2

`PANASONIC_MEWTOCOL = GO_WITH_GATES`

The implementation path is now sufficiently bounded:

- native classic MEWTOCOL-COM;
- TCP and Host Serial transport variants;
- common typed address/codec/planner;
- bounded contiguous polling;
- canonical Runtime writes;
- conservative ambiguous-write semantics;
- canonical PointRead/diagnostics;
- no sidecar;
- no community production dependency;
- FP7 MC kept outside the Panasonic native product.

## 33. Checkpoint 2 stop

Publish `RESEARCH PANASONIC-MEWTOCOL — CHECKPOINT 2` to #553 and stop.

Do **not** execute L0-L4, hardware procurement shortlist finalization, legal conclusions, or final v1 handoff until a new `SIGA`.

`DOCS_ONLY`  
`NO PRODUCT CODE CHANGED`  
`NO DEPENDENCY CHANGED`  
`NO CI CHANGED`  
`NO MERGE PERFORMED`


# Checkpoint 3 — L0-L4, Hardware, Legal and Final V1 Recommendation

**Checkpoint date / source access:** 2026-10-06  
**Checkpoint start HEAD:** `5b28b55a71940e13a37f49ae3dc0325817eef276`  
**State:** `RESEARCH_ONLY / DOCS_ONLY / NO_PRODUCT_CODE / NO_MERGE`

Checkpoint 3 adds:

- `PANASONIC-MEWTOCOL-LAB-VALIDATION-MATRIX.md`
- `PANASONIC-MEWTOCOL-LEGAL-AND-DEPENDENCY-MATRIX.md`

## 34. Current product/hardware revalidation

Current Panasonic Industry catalog evidence accessed on 2026-10-06 confirms:

### FP0R

Current FP0R product page still lists F32 serial models including:

- `AFP0RF32CT` — RS-232C;
- `AFP0RF32MT` — RS-485;
- corresponding PNP variants.

Recommended L4 serial fixture:

`AFP0RF32MT`

Reason:

- current compact family;
- native RS-485;
- exercises #469 Host Serial;
- suitable for station/multidrop behavior.

### FP-XH

Current FP-XH catalog lists:

`AFPXHC14RD`

Official AFPX-COM5 communication material confirms:

- Ethernet interface on COM1;
- RS-232C on COM2;
- Computer Link support over Ethernet;
- TCP server mode;
- configurable port;
- documented multi-connection server behavior.

Recommended L4 classic COM TCP fixture:

`AFPXHC14RD + AFPX-COM5`

### FP7

Current FP7 product/manual material lists current Ethernet CPUs including:

- `AFP7CPS3RE`
- `AFP7CPS4RE`

Recommended L4 higher-tier fixture:

`AFP7CPS3RE`

Reason:

- current R-series;
- integrated Ethernet;
- native MEWTOCOL;
- useful separate FP7 MC 3E interoperability target.

### FP0H — optional additional evidence

Current FP0H material shows:

`AFP0HC32ET`

with:

- two Ethernet ports;
- RS-232C;
- MEWTOCOL-COM;
- MEWTOCOL-DAT;
- Modbus TCP;
- MC Protocol.

It is an excellent optional compact integrated-Ethernet lab device and future family candidate.

It is **not** added to the first v1 compatibility claim by this checkpoint.

### Availability meaning

The above models are currently present in Panasonic product/manual catalogs.

This is:

`CURRENT_CATALOG_EVIDENCE`

not proof of local distributor stock, price or lead time.

Procurement must revalidate regional orderability before purchase.

## 35. Exact L0-L4 strategy

The full matrix is in:

`docs/research/industrial-drivers/PANASONIC-MEWTOCOL-LAB-VALIDATION-MATRIX.md`

### L0

Required deterministic coverage:

- classic COM standard/expanded frame;
- XOR BCC;
- RCS/RCP/RCC/RD/RT;
- WCS/WD and bounded explicit WCP/WCC;
- strict address parser;
- exact family/profile validation;
- Boolean/UInt16/Int16;
- error mapping;
- batching/split boundaries;
- process-truth state machine;
- unsupported types fail closed.

No hardware claim from L0.

### L1

Independent fake TCP peer + fake #469 Host Serial provider.

Required:

- partial/malformed frames;
- BCC failure;
- wrong station;
- protocol `!` errors;
- delays/timeouts;
- connection close/reconnect;
- no TCP pipelining;
- serial bus serialization;
- station scheduling;
- conflicting serial settings;
- cancellation/restart;
- ambiguous write timeout with no blind retry.

### L2

Vendor-side independent reference:

- Control FPWIN Pro7;
- FP Data7 where compatible.

Current Panasonic pages list FPWIN Pro7 7.7.4.1 and FP Data7 V1.12.0.

FPWIN simulation exists for FP0R/other 16-bit PLC types, but research did not prove a separately reachable external MEWTOCOL server endpoint.

Therefore:

`FPWIN_SIMULATION != ACCEPTED_L2_WIRE_PEER_UNLESS_EXTERNAL_ENDPOINT_IS_PROVEN`

Community MewtocolNet is optional corroboration only after exact-license approval; it is not required for acceptance.

### L3

Canonical EliteSCADA integration:

- Driver/Data Source;
- TAG binding;
- polling;
- Runtime.WriteAsync;
- PointRead;
- #500 diagnostics;
- Save/Revision/Publish/Activate;
- package/restart;
- #469 Host Serial;
- HA external-effect ownership.

No second Runtime/TAG/diagnostics authority.

### L4

Minimum strong matrix:

1. `AFP0RF32MT` — Host Serial / RS-485 / MEWTOCOL-COM.
2. `AFPXHC14RD + AFPX-COM5` — TCP / MEWTOCOL-COM.
3. `AFP7CPS3RE` — TCP / MEWTOCOL-COM plus separate MC 3E cross-test.

Optional fourth:
`AFP0HC32ET`.

Every L4 record must capture actual firmware and communication configuration.

Do not pre-invent a firmware claim.

## 36. L4 publication gate

A published compatibility claim requires at least:

- one compact serial family;
- one TCP family;
- one modern FP7 profile;
- real read/write/PointRead/restart evidence;
- exact model/firmware/date;
- exact address/types tested.

Preferred first release claim after successful validation:

`Panasonic MEWTOCOL-COM compatible with the validated FP0R, FP-XH and FP7 models/transports listed in the EliteSCADA compatibility matrix.`

Do not claim all Panasonic PLCs.

## 37. Data-type final v1 decision

Checkpoint 3 did not find sufficient authoritative cross-family evidence to promote adjacent-word interpretation for:

- UInt32;
- Int32;
- Float32.

Therefore the final first-scope type set is:

`Boolean`  
`UInt16`  
`Int16`

and:

`UInt32 / Int32 / Float32 = LATER_GATE`

`Float64 / BCD / String / ByteArray = NOT_V1`

This is intentionally conservative.

A future type expansion must prove:

1. official Panasonic representation;
2. L0 vectors;
3. vendor-tool evidence;
4. L4 on at least two selected families/profiles.

## 38. Address/memory final v1 decision

First grammar remains:

Contacts:
- X
- Y
- R
- L
- T
- C

Words:
- WX
- WY
- WR
- WL
- DT
- LD

Access policy:

- X/WX: read-only;
- Y/WY: read/write where profile permits;
- R/WR: read/write;
- L/WL/LD: only where selected family/profile proves the area and intended semantics;
- DT: read/write;
- T/C: read-only in v1.

Deferred:

- SV/EV;
- TS/TE/CS/CE;
- FL;
- special/system devices;
- generic word-bit selector syntax.

The driver must validate numeric range against explicit family/profile capabilities before I/O.

## 39. MEWTOCOL7 final v1 decision

`MEWTOCOL7-COM = NOT_V1`

Reason:

- distinct frame/header;
- CRC-16-CCITT;
- distinct extended addressing/command behavior;
- no requirement to deliver first useful Panasonic native driver.

Future promotion requires its own:

- L0 codec/address vectors;
- L1 fake peer;
- FP7 L4;
- explicit product benefit.

It may remain under the Panasonic MEWTOCOL family later.

## 40. FP7 MC final relationship

`FP7_MC = OUTSIDE_PANASONIC_NATIVE_SCOPE`

If EliteSCADA later exposes MC communication to FP7:

- reuse common Mitsubishi/MC provider;
- enable only documented Panasonic-compatible subset;
- no Panasonic-local MC codec fork;
- no claim that FP7 represents universal MELSEC behavior.

FP7 remains useful L4 interoperability hardware for the Mitsubishi program.

## 41. Legal / trademark conclusion

Full matrix:

`docs/research/industrial-drivers/PANASONIC-MEWTOCOL-LEGAL-AND-DEPENDENCY-MATRIX.md`

Research findings:

- Panasonic website/manual content is copyright-protected;
- Panasonic logos/product media should not be redistributed without permission;
- Panasonic name/product/protocol terms are protected marks/names;
- public product naming must not imply endorsement/certification;
- vendor tools are separate lab software and must not be redistributed.

Required release gate:

`LEGAL_REVIEW_REQUIRED_FOR_PUBLIC_PRODUCT_NAMING`

Recommended engineering posture:

- original EliteSCADA code;
- original docs;
- cite/link official Panasonic manuals;
- no bundled Panasonic PDFs;
- no Panasonic logo;
- factual compatibility wording only;
- explicit no-affiliation/no-endorsement wording if counsel approves.

This does not block implementation of the protocol research result.

## 42. Dependency final decision

`DEPENDENCY = BUILT_IN_DOTNET`

No production MEWTOCOL library.

Current community-library finding:

- OpenLogics/MewtocolNet current repository: GPL-3.0;
- NuGet `Mewtocol.NET 0.8.1`: metadata says MIT;
- current repo README says FP7 unsupported;
- NuGet package dates to 2023.

The source/package license discrepancy and coverage gap reinforce the built-in choice.

Do not add it to the product.

## 43. Final v1 product recommendation

### Decision

`PANASONIC_MEWTOCOL = GO_WITH_GATES`

### Semantic product family

`Panasonic MEWTOCOL`

### Recommended concrete driver types

Preferred current-SDK fit:

`panasonic.mewtocol.tcp`

`panasonic.mewtocol.serial`

Shared core:

- MEWTOCOL-COM frame/error codec;
- address AST/parser;
- value codec;
- family/profile capabilities;
- bounded planner;
- TAG-binding schema;
- process-truth semantics.

### Dialect

`MEWTOCOL-COM`

### Transports

- TCP client;
- Host Serial through #469.

### First family claims after L4

- FP0R;
- FP-XH;
- current FP7 R-series bounded classic-COM subset.

FP0H is a future/optional additional candidate, not required for v1 claim.

### Engineering types

- Boolean;
- UInt16;
- Int16.

### Planner

`BOUNDED_CONTIGUOUS_GROUPING`

No one-request-per-TAG default.

### TCP

`PERSISTENT / ONE_OUTSTANDING / NO_PIPELINE`

### Serial

`SHARED_HOST_BUS / SERIALIZED_REQUESTS / STATION_ROUTING`

### Write truth

`ACK_IS_PROTOCOL_SUCCESS_NOT_PHYSICAL_SUCCESS`

Post-dispatch timeout:

`UNKNOWN / NO_BLIND_RETRY`

### Engineering

- typed manual address;
- address assistant;
- station/profile validation;
- Connection Test;
- optional RT model/status probe;
- canonical PointRead;
- no fabricated symbolic browse.

### Diagnostics

#500 common diagnostics only.

### Security

Legacy plaintext OT protocol; segmented LAN/local serial; VPN/private network for remote.

### Implementation

EliteSCADA-owned .NET implementation.

No sidecar.
No vendor Runtime.
No new protocol dependency.

## 44. Future DEV prerequisites

A future implementation branch must not be released until Main freezes:

### P0 — exact driver IDs

Recommended:

- `panasonic.mewtocol.tcp`
- `panasonic.mewtocol.serial`

If Main instead requires one literal `panasonic.mewtocol` type across both transports:

`RESEARCH_CONTRACT_DELTA_REQUIRED`

for alternative connection profiles / conditional schemas.

### P1 — frozen first family capability table

At minimum:

- FP0R;
- FP-XH;
- FP7 classic-COM subset.

Numeric bounds must be encoded as explicit profile data, not guessed from one family.

### P2 — v1 type freeze

`Boolean / UInt16 / Int16 only`

unless Main deliberately authorizes a later type research gate.

### P3 — validation plan accepted

Use the L0-L4 matrix as binding acceptance direction.

Hardware is not required before starting L0-L3 implementation, but L4 is required before broad compatibility claims.

### P4 — legal naming review before public marketing/release

Engineering can proceed with internal IDs while public display/trademark wording is reviewed.

## 45. Recommended future DEV implementation slices

### S0 — common MEWTOCOL-COM core

- address AST/parser;
- family/profile capability table;
- BCC/frame codec;
- command/error codec;
- 16-bit value codec;
- planner;
- deterministic L0.

### S1 — TCP

- `panasonic.mewtocol.tcp`;
- persistent one-outstanding session;
- fake TCP peer L1;
- read/write/RT;
- diagnostics;
- PointRead.

### S2 — Host Serial

- `panasonic.mewtocol.serial`;
- #469 Host Serial;
- shared bus/station scheduling;
- fake serial L1;
- PointRead/diagnostics.

### S3 — Engineering + Runtime convergence

- descriptor/catalog;
- typed address assistant;
- canonical TAG registration;
- Runtime.WriteAsync;
- #500 ladder;
- lifecycle/package/restart/HA.

### S4 — L4

- AFP0RF32MT;
- AFPXHC14RD + AFPX-COM5;
- AFP7CPS3RE;
- optional AFP0HC32ET;
- exact compatibility matrix.

Do not implement MEWTOCOL7, DAT or Panasonic-specific MC in these slices.

## 46. Checkpoint 3 decision summary

`PANASONIC_MEWTOCOL = GO_WITH_GATES`

Gates remaining before DEV release/marketing:

1. Main freezes transport driver IDs or explicitly authorizes SDK delta.
2. Future DEV passes L0-L3.
3. Real L4 passes before published compatibility claim.
4. Legal reviews public Panasonic/MEWTOCOL naming.
5. Multiword types remain out until separate evidence gate.

No blocker requires abandoning the native driver.

## 47. Checkpoint 3 stop

Publish `RESEARCH PANASONIC-MEWTOCOL — CHECKPOINT 3` to #553 and STOP.

A separate final continuation must perform:

- fresh GitHub revalidation;
- exact HEAD/base/merge-base/ahead-behind;
- required-doc inventory;
- final handoff to Main.

Do not perform that final handoff in the same checkpoint turn.

`RESEARCH_ONLY`  
`DOCS_ONLY`  
`NO PRODUCT CODE CHANGED`  
`NO HOST SERIAL DUPLICATED`  
`NO DEPENDENCY CHANGED`  
`NO CI CHANGED`  
`NO MERGE PERFORMED`
