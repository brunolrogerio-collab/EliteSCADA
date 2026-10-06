# Panasonic MEWTOCOL — Address and Family Matrix

**Issue:** #553  
**Checkpoint:** 1 complete; 2 addendum  
**Research date:** 2026-10-06  
**Scope:** family / transport / address / data-type evidence only  
**Status:** `DOCS_ONLY / NO_PRODUCT_CODE`

This matrix is deliberately conservative. A row means "supported by current official evidence for the named family/profile," not "all Panasonic PLCs."

## 1. Family matrix

| Family / CPU direction | Product status | Ethernet | Serial | MEWTOCOL-COM | MEWTOCOL7-COM | Modbus | MC Protocol | V1 disposition |
|---|---|---|---|---|---|---|---|---|
| FP0R F32 family, e.g. `AFP0RF32MT/CT` | current compact family | no integrated Ethernet on baseline CPU; external gateway/module only if separately proven | YES; current family has RS-232C/RS-485 variants | YES | NO evidence / not applicable | family-specific serial Modbus capability exists on selected profiles, but not a substitute for native COM | NO | V1 over Host Serial |
| FP-XH, e.g. `AFPXHC14RD` | current | via supported Ethernet communication hardware/profile such as AFPX-COM5; some model-specific options vary | YES | YES | NO official FP-XH requirement found | available on selected communications profiles | NO | V1 over Host Serial and TCP where hardware/profile supports it |
| FP7 current R-series, e.g. `AFP7CPS3RE/AFP7CPS4RE` | current higher-tier family | YES, integrated Ethernet on selected current CPUs | YES through supported CPU/SCU communication interfaces | YES | YES | available on selected profiles | YES, separately documented interoperability feature; not native-driver scope at CP1 | V1 bounded MEWTOCOL-COM; MEWTOCOL7 later |
| FP-X | legacy/discontinued Sep 2021 | model/module dependent | YES | YES | NO | selected models/profiles | NO | LATER / installed-base validation only |

### Notes

- Do not infer Ethernet support from family name alone.
- Do not infer serial line format from another family.
- Do not claim `Panasonic PLC supported`; claims must state family, CPU/model, transport, dialect, firmware/module, and validation level.
- Current FP7 R-series naming must be separated from older FP7 CPUs when later L4 evidence is recorded.

## 2. Protocol/dialect matrix

| Property | MEWTOCOL-COM | MEWTOCOL7-COM | MEWTOCOL-DAT |
|---|---|---|---|
| Primary role | Panasonic computer-link request/response | FP7-era extended computer-link profile | binary data-transfer procedure |
| Frame header | `%` or `<` | `>` | binary/special LAN framing |
| Station notation | classic two-digit family/profile station field | `@ddd` form in FP7 documentation | mode-specific |
| Check | XOR BCC, 2 ASCII hex chars | CRC-16-CCITT | DAT-specific |
| Normal response marker | `$` | command-specific MEWTOCOL7 response | binary |
| Error marker/model | `!` plus protocol error code | MEWTOCOL7 status/error model | DAT-specific |
| Max frame evidence | 118 chars `%`; 2048 chars `<` | up to 4096 chars in FP7 docs | larger binary transfers |
| V1 | YES | NO | NO |

## 3. Address grammar

### 3.1 Accepted V1 lexical forms

```text
X<contact>
Y<contact>
R<contact>
L<contact>
T<decimal>
C<decimal>

WX<decimal>
WY<decimal>
WR<decimal>
WL<decimal>
DT<decimal>
LD<decimal>
```

### 3.2 Contact-number rule

For `X`, `Y`, `R`, and `L`:

- last digit is hexadecimal `0..F`;
- digits before the final nibble are decimal;
- parser canonicalizes mnemonic case only;
- invalid mixed forms are rejected.

Examples:

- `X0`
- `X1F`
- `R10`
- `R12A`

For `T` and `C`, the index is decimal.

### 3.3 Word-number rule

`WX/WY/WR/WL/DT/LD` use decimal word indexes.

Examples:

- `DT100`
- `WR20`
- `WX0`

### 3.4 Rejected in v1

- free-form protocol commands;
- station prefixes embedded in the TAG address;
- whitespace normalization that changes meaning;
- `DT100.3` or arbitrary generic bit-in-word syntax;
- negative indexes;
- values beyond family/CPU configured range;
- device areas not supported by selected family/dialect.

The future parser must produce a bounded typed address object; the raw user string must never pass directly to the wire codec.

## 4. Device/address matrix

Ranges below are capability categories, not universal numeric constants. Exact numeric ranges must be selected from the target CPU's operation-memory table during implementation/L0 fixture construction.

| Device | Official semantic class | Native width | Radix/notation | Read | Write | Family notes | V1 |
|---|---|---:|---|---|---|---|---|
| `X` | external input contact | bit | contact notation; final nibble hex | YES | NO | common classic area | YES |
| `Y` | external output contact | bit | contact notation | YES | YES where permitted | physical output semantics; readback/process truth later | YES |
| `R` | internal relay | bit | contact notation | YES | YES | common classic area | YES |
| `L` | link relay | bit | contact notation | YES | profile-dependent | only when actual link-area semantics exist | YES, gated by family |
| `T` | timer contact | bit | decimal | YES | deferred | value storage differs by family | READ ONLY |
| `C` | counter contact | bit | decimal | YES | deferred | value storage differs by family | READ ONLY |
| `WX` | external input words | 16-bit word | decimal | YES | NO | word view of X area | YES |
| `WY` | external output words | 16-bit word | decimal | YES | YES where permitted | word view of Y area | YES |
| `WR` | internal relay words | 16-bit word | decimal | YES | YES | word view of R area | YES |
| `WL` | link relay words | 16-bit word | decimal | YES | profile-dependent | link area | YES, gated |
| `DT` | data register | 16-bit word | decimal | YES | YES | core data area | YES |
| `LD` | link data register | 16-bit word | decimal | YES | profile-dependent | only where link-area support is configured | YES, gated |
| `SV` | timer/counter set value | 16-bit word on classic families | decimal | YES | later | classic FP0R/FP-XH-style semantics | LATER |
| `EV` | timer/counter elapsed/current value | 16-bit word on classic families | decimal | YES | later | classic FP0R/FP-XH-style semantics | LATER |
| `TS` | FP7 timer set value | 32-bit | decimal | YES | later | FP7-specific modern area | LATER |
| `TE` | FP7 timer elapsed value | 32-bit | decimal | YES | later | FP7-specific modern area | LATER |
| `CS` | FP7 counter set value | 32-bit | decimal | YES | later | FP7-specific modern area | LATER |
| `CE` | FP7 counter elapsed value | 32-bit | decimal | YES | later | FP7-specific modern area | LATER |
| `FL` | file register / family-specific file area | word | decimal | unknown across selected set | unknown | not uniform enough for CP1 v1 | LATER |
| special R/DT, `SR`, `SD`, `UM`, `IN`, `OT`, `P`, `E` | special/system/extended | varies | varies | varies | varies | family-specific and potentially safety-sensitive | NOT V1 |

## 5. Family memory observations

### FP0R

Current official FP0R material describes a compact controller family with MEWTOCOL communication through serial interfaces.

For v1, use only the common classic areas that are confirmed by the CPU's actual operation-memory table:

- X/Y;
- R;
- T/C contacts;
- WX/WY/WR;
- DT;
- LD/L/WL only when selected CPU/project configuration proves the link area.

Do not assume FP7 extended devices exist.

### FP-XH

The official FP-XH Basic manual operation-memory table confirms classic FP-family areas including:

- X/Y;
- R;
- L;
- T/C;
- WX/WY/WR/WL;
- DT;
- LD;
- timer/counter value areas.

FP-XH memory capacity varies by control-unit model. Therefore the grammar is stable while numeric upper bounds are profile/CPU metadata.

### FP7 current R-series

Official FP7 CPU hardware documentation shows a much larger operation-memory model and current R-series CPUs.

Classic common areas still exist, but FP7 also adds/changes areas and capacities, including 32-bit timer/counter value regions.

Important v1 constraint:

`FP7 physical memory capacity != automatically MEWTOCOL-COM addressable capacity`

The first driver must expose only the subset proven reachable by the selected classic COM command/address format. Extended FP7 addressing is a MEWTOCOL7 gate, not a reason to bypass validation.

## 6. Station/unit addressing

V1 design:

```text
Data Source:
  family/profile
  dialect = mewtocol-com
  transport = tcp | host-serial
  station = N
  endpoint or serial line settings

TAG:
  device + address only
```

Do not encode station in each TAG address.

Validation:

- station range comes from selected family/profile;
- modern family manuals commonly expose 1..99 for computer-link station configuration;
- broadcast/no-response forms are not valid ordinary read targets;
- serial multidrop must route by Data Source station through the shared #469 host bus.

## 7. Data-type matrix

| Native storage | EliteSCADA type | Encoding decision | CP1 status |
|---|---|---|---|
| contact bit | Boolean | protocol contact read/write | V1 |
| one 16-bit word | UInt16 | raw word | V1 |
| one 16-bit word | Int16 | two's-complement interpretation | V1 |
| two words | UInt32 | requires authoritative word-order evidence | GATED |
| two words | Int32 | requires authoritative word-order evidence | GATED |
| two words | Float32 | requires authoritative word-order + floating representation evidence | GATED |
| four words | Float64 | no first-scope justification | LATER |
| one/more words | BCD | application/profile-specific | LATER |
| words | String | Engineering overlay, not a claimed native MEWTOCOL scalar | LATER |
| words | Byte array | no arbitrary v1 raw-byte TAG surface | LATER |
| contiguous words | block/array | transport planner may batch; public TAG-array contract later | INTERNAL V1 / PUBLIC LATER |

## 8. Byte/word order evidence

Official MEWTOCOL-COM examples return 16-bit word data as hexadecimal ASCII bytes in low-byte-first order inside the word.

Example pattern:

`0x0064 -> "6400"`

Therefore v1 can safely decode one word.

Checkpoint 1 does **not** establish the order of two consecutive 16-bit words for 32-bit integers or IEEE floating values across all selected Panasonic families.

Implementation rule:

`NO_MODBUS_ANALOGY`

Do not import Modbus byte/word-swap defaults.

Multiword support requires:

1. official Panasonic representation evidence;
2. deterministic L0 vectors;
3. independent L2/L4 confirmation before a compatibility claim.

## 9. Timer/counter matrix

| Family | Contact | Set/current values | Storage observation | V1 |
|---|---|---|---|---|
| FP0R | T/C | SV/EV | classic word-oriented value model | contacts read |
| FP-XH | T/C | SV/EV | classic word-oriented value model | contacts read |
| FP7 R-series | T/C | TS/TE/CS/CE | value areas are 32-bit double words | contacts read |

A single cross-family "timer value" abstraction is therefore deferred.

## 10. Transport matrix

| Family | TCP MEWTOCOL-COM | Host Serial MEWTOCOL-COM | Notes |
|---|---:|---:|---|
| FP0R | not baseline v1 claim | YES | serial is the representative current compact-family path |
| FP-XH | YES with supported Ethernet hardware/profile | YES | AFPX-COM5 official docs expose TCP/UDP and configurable server port; v1 TCP only |
| FP7 R-series | YES | YES with supported interface | integrated Ethernet on selected CPUs; connection resources are model/config specific |
| FP-X | later only | later only | legacy installed-base path |

## 11. Hardware candidates for later L4

Not procurement authorization; exact shortlist belongs to Checkpoint 3.

| Coverage goal | Candidate direction | Why |
|---|---|---|
| compact/current serial | FP0R F32, e.g. `AFP0RF32MT` or `AFP0RF32CT` | current family; direct serial coverage |
| current classic COM + Ethernet accessory | `AFPXHC14RD` + AFPX-COM5 | validates FP-XH family and configurable TCP computer-link path |
| modern higher-tier Ethernet | FP7 `AFP7CPS3RE` | current R-series with Ethernet; later useful for MEWTOCOL7 and MC cross-tests |

## 12. Compatibility-claim template

Future docs/tests must use claims in this form:

```text
Panasonic FP-XH / AFPXHC14RD
+ AFPX-COM5
+ firmware/version recorded
+ TCP
+ MEWTOCOL-COM
+ device areas X/Y/R/DT...
+ validation level L4
+ validation date
```

Never:

```text
Panasonic PLC supported
```

## 13. CP1 product recommendation carried by this matrix

`panasonic.mewtocol`

V1 profile:

- dialect: `MEWTOCOL-COM`;
- transports: TCP + Host Serial;
- families: FP0R, FP-XH, bounded current FP7 R-series;
- address model: strict typed device grammar;
- first memory areas: X/Y/R/L, WX/WY/WR/WL, DT/LD, with family-aware validation;
- timer/counter: contact read only initially;
- first types: Boolean, Int16, UInt16;
- multiword types: gated;
- MEWTOCOL7-COM: later explicit profile;
- MEWTOCOL-DAT: outside v1;
- FP-X: legacy/later.

`PANASONIC_MEWTOCOL = GO_WITH_GATES`

`DOCS_ONLY`  
`NO PRODUCT CODE CHANGED`  
`NO DEPENDENCY CHANGED`  
`NO CI CHANGED`  
`NO MERGE PERFORMED`


# Checkpoint 2 Addendum — Planner and FP7 MC Interoperability Matrix

**Checkpoint date:** 2026-10-06  
**Scope:** planner/command capabilities + FP7 MC subset only

## 14. MEWTOCOL-COM command/planner matrix

| Need | Command | Shape | Documented bound / note | V1 |
|---|---|---|---|---|
| one contact read | `RCS` | single contact | one target | YES |
| sparse contact read | `RCP` | explicitly listed contacts | 1..8 contacts | YES when useful |
| contiguous contact read | `RCC` | range | profile/frame bounded | YES |
| one contact write | `WCS` | single contact | one target | YES |
| sparse contact write | `WCP` | explicitly listed contacts | 1..8 contacts | only explicit multi-point operation |
| contiguous contact write | `WCC` | range | profile/frame bounded | bounded explicit operation |
| contiguous word read | `RD` | start/end word | profile/frame bounded | YES |
| contiguous word write | `WD` | start/end + data | profile/frame bounded | YES |
| PLC model/status | `RT` | status query | model/version/mode/status evidence | Engineering probe |
| timer/counter value commands | `RS/WS/RK/WK` | family-specific values | not uniform across v1 families | LATER |
| monitor registration | `MC/MD/MG` | stateful registration/monitor | extra PLC-side state | LATER |

Planner invariant:

`NO_1_TAG_1_REQUEST_DEFAULT`

Read grouping keys:

`DataSource -> transport/session/bus -> station -> dialect -> family/profile -> device area -> contiguous range -> compatible layout`

Do not group across any key boundary.

## 15. Frame/batch capability matrix

| Capability | Classic standard frame | Classic expanded frame | Product rule |
|---|---:|---:|---|
| maximum frame characters | 118 | 2048 | profile capability |
| sparse contacts RCP/WCP | 8 | 8 | command limit |
| contiguous read words | small short-frame subset | up to ~509 on officially documented current profiles | profile capability |
| contiguous write words | small short-frame subset | up to ~507 on officially documented current profiles | profile capability |

The exact short-frame word quantities must be frozen from deterministic L0 vectors rather than copied from an arithmetic estimate.

The future profile should expose explicit bounded values rather than infer limits from family name alone.

## 16. Session/scheduler matrix

| Transport | Physical/session authority | Outstanding transactions | Reuse | Failure recovery |
|---|---|---:|---|---|
| TCP | Panasonic Data Source TCP session | 1 per connection | persistent while healthy | close/reconnect on EOF/framing/BCC/ambiguous timeout |
| Host Serial | existing #469 `HostSerialBusCoordinator` | 1 per physical bus | shared master bus with compatible line settings | serialized recovery/reopen; no blind write retry |

Station belongs to the Data Source/session configuration.

## 17. FP7 MC Protocol — exact interoperability disposition

FP7 MC is not part of the native MEWTOCOL address grammar above.

Official FP7 documentation describes a Mitsubishi QnA-compatible subset:

- 3E frame;
- binary encoding;
- TCP/IP or UDP/IP;
- bulk read `0401`;
- bulk write `1401`;
- bit subcommand `0001`;
- word subcommand `0000`;
- up to 7168 bits or 960 words per documented bulk operation.

### 17.1 Restricted routing/header fields

The FP7 slave subset fixes/constrains MC routing fields approximately as follows:

| Field | FP7 documented value/restriction |
|---|---|
| network number | `00h` |
| PC number | `FFh` |
| destination unit I/O | `03FFh` |
| destination unit number | `00h` |
| CPU monitor timer | not supported in this FP7 subset |
| starting device number | 3-byte / 6-hex-digit QnA-compatible representation |

These restrictions make FP7 a useful interoperability target but not a universal MELSEC simulator.

### 17.2 Device mapping matrix

| MC device code/name | FP7 target | Access/use observation |
|---|---|---|
| `X` / code `9C` | FP7 X external input area | documented bit/word mapping |
| `Y` / `9D` | FP7 Y external output area | documented bit/word mapping |
| `B` / `A0` | FP7 L link relay | mapped interoperability area |
| `M` / `90` | FP7 R lower internal-relay range | mapped interoperability area |
| `L` / `92` | FP7 R higher/latch range | mapped interoperability area |
| `D` / `A8` | FP7 DT data-register range | word mapping |
| file register `R` / `AF` | FP7 extended DT range | word mapping |
| `ZR` / `B0` | FP7 extended DT range | word mapping |
| `W` / `B4` | FP7 LD link-data range | word mapping |
| `TN` / `C2` | FP7 TE timer elapsed/current | 16-bit interoperability subset |
| `TS` / `C1` | FP7 T timer contacts | status/contact mapping |
| `CN` / `C5` | FP7 CE counter elapsed/current | 16-bit interoperability subset |
| `CS` / `C4` | FP7 C counter contacts | status/contact mapping |
| `SM` / `91` | FP7 SR special relay | restricted semantics |
| `SD` / `A9` | FP7 SD special data | restricted semantics |

Important:

- only documented/global mappings are interoperability authority;
- do not infer Panasonic local-device support from MC;
- FP7 timer/counter current values are 32-bit in native Panasonic memory, while the documented MC compatibility path exposes only a limited 16-bit representation for those mapped values;
- therefore MC cannot replace MEWTOCOL for full native FP7 semantics.

## 18. FP7 MC architecture decision

Primary user-facing disposition:

`FP7_MC = OPTIONAL_ALTERNATIVE`

Validation/lab disposition:

`FP7_MC = FUTURE_MITSUBISHI_INTEROPERABILITY_PEER`

Implementation ownership if ever supported:

`COMMON_MC_PROVIDER`

Never:

`PANASONIC_MEWTOCOL_INTERNAL_MC_CODEC`

This means:

1. Panasonic native driver remains MEWTOCOL-based.
2. FP7 may later be connected through a common MC driver if the common provider explicitly supports the Panasonic 3E subset.
3. FP7 hardware is a useful independent L4 peer for the future Mitsubishi driver.
4. Passing against FP7 proves only its documented 3E binary bulk subset.

`FP7_MC_SUBSET != UNIVERSAL_MELSEC_SUPPORT`

## 19. Driver-type naming gate

The current Driver SDK has one `ConnectionModel` per driver type. Therefore the matrix now recommends:

| Driver type | Connection model | Shared family |
|---|---|---|
| `panasonic.mewtocol.tcp` | DirectNetwork | Panasonic MEWTOCOL |
| `panasonic.mewtocol.serial` | HostSerial | Panasonic MEWTOCOL |

Both share:

- `mewtocol-com` dialect;
- address parser;
- codec;
- family capability tables;
- TAG binding grammar;
- value codec;
- planner semantics.

This is a recommendation for Main to freeze before DEV.

If Main requires one literal `panasonic.mewtocol` type for both transports:

`RESEARCH_CONTRACT_DELTA_REQUIRED`

because the current generic descriptor/UI needs explicit alternative connection profiles and conditional configuration fields.

## 20. Checkpoint 2 state

`PANASONIC_MEWTOCOL = GO_WITH_GATES`

`MAIN_DECISION_REQUIRED` for exact transport-specific driver IDs vs shared SDK extension.

`DOCS_ONLY`  
`NO PRODUCT CODE CHANGED`  
`NO DEPENDENCY CHANGED`  
`NO CI CHANGED`  
`NO MERGE PERFORMED`
