# Panasonic MEWTOCOL — Execution Research

**Issue:** #553 — INDUSTRIAL-RESEARCH-02  
**Parent:** #551 — INDUSTRIAL-PLC-ROADMAP  
**Contract:** `C-INDUSTRIAL-PANASONIC-MEWTOCOL-RESEARCH-01`  
**Order:** `INDUSTRIAL-PANASONIC-MEWTOCOL-EXECUTION-RESEARCH-01`  
**Checkpoint:** 1 — protocol + families + address + data types  
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
