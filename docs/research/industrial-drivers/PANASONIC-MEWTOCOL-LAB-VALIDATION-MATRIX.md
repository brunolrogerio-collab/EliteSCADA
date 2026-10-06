# Panasonic MEWTOCOL — Lab Validation Matrix

**Issue:** #553 — INDUSTRIAL-RESEARCH-02  
**Contract:** `C-INDUSTRIAL-PANASONIC-MEWTOCOL-RESEARCH-01`  
**Checkpoint:** 3 — L0-L4 / hardware / acceptance gates  
**Research date:** 2026-10-06  
**Status:** `RESEARCH_ONLY / DOCS_ONLY / NO_PRODUCT_CODE / NO_MERGE`

This document defines evidence required before any future Panasonic MEWTOCOL compatibility claim is accepted.

It is not a test result and does not claim that hardware has already been procured or executed.

## 1. Validation philosophy

Compatibility claims must be specific to:

- Panasonic family;
- exact CPU/model;
- communication module/cassette where used;
- transport;
- MEWTOCOL dialect;
- firmware observed on the device;
- address/device areas exercised;
- data types exercised;
- validation level;
- test date.

Never publish a generic claim such as:

`Panasonic PLCs supported`

without the bounded matrix below.

## 2. Required validation ladder

### L0 — deterministic codec / address / planner

No network or physical serial hardware.

Required:

1. MEWTOCOL-COM frame encode/decode:
   - `%` standard frame;
   - `<` expanded frame where selected profile supports it;
   - command/normal/error response markers;
   - CR terminator;
   - XOR BCC generation and verification.
2. BCC:
   - known-good vectors;
   - single-byte corruption;
   - invalid ASCII hex;
   - omitted/truncated BCC;
   - wrong station/response marker.
3. Address parser:
   - X/Y/R/L final hexadecimal nibble;
   - preceding decimal digits;
   - T/C decimal;
   - WX/WY/WR/WL/DT/LD decimal;
   - lower/upper case normalization;
   - no whitespace repair;
   - out-of-range;
   - profile-incompatible device;
   - unsupported area.
4. Value codec:
   - Boolean;
   - UInt16;
   - Int16;
   - low-byte-first one-word representation.
5. Explicitly unsupported:
   - UInt32;
   - Int32;
   - Float32;
   - Float64;
   - BCD;
   - String;
   - arbitrary byte array.
6. Command codecs:
   - RCS/RCP/RCC/RD/RT;
   - WCS/WD;
   - WCP/WCC only for explicit bounded operations.
7. Error codec:
   - `!` response;
   - exact error-code preservation;
   - canonical classification.
8. Planner:
   - no one-request-per-TAG default;
   - grouping by station/dialect/family/device area;
   - contiguous word groups;
   - sparse contact groups <= 8;
   - split at frame/profile limit;
   - no grouping across invalid gaps or station boundaries;
   - deterministic ordering.
9. Process-truth state:
   - pre-dispatch failure;
   - valid ACK;
   - readback match;
   - readback mismatch;
   - ambiguous post-dispatch timeout = UNKNOWN.

### L0 acceptance

`L0_GREEN` only when all deterministic protocol/address/planner vectors pass and unsupported types fail closed.

No hardware claim is allowed from L0.

## 3. L1 — deterministic fake peer / fault recovery

### 3.1 TCP fake Panasonic peer

Implement a test peer independent from the production session class.

Required cases:

- valid RCS/RCP/RCC/RD response;
- valid WCS/WD ACK;
- RT identity/status response;
- error reply;
- malformed BCC;
- wrong station;
- wrong command response;
- truncated frame;
- garbage before frame;
- multiple response chunks / partial reads;
- delayed response;
- timeout before any response;
- timeout after request dispatch;
- peer close before response;
- peer close after valid response;
- idle close and reconnect;
- cancelled request;
- unsupported frame/profile;
- boundary-size standard frame;
- boundary-size expanded frame;
- one byte over profile limit.

Session invariant:

`ONE_OUTSTANDING_REQUEST_PER_TCP_CONNECTION`

The fake peer must detect and fail any accidental pipelining.

### 3.2 Fake Host Serial

Use the existing #469 host-serial abstraction with an injectable fake provider/connection.

Required:

- two Panasonic Data Sources sharing one physical bus with compatible line settings;
- station 1 and station 2 serialized;
- no concurrent requests on the stream;
- conflicting line settings fail deterministically;
- malformed/truncated frame recovery;
- stale input handling;
- turnaround delay when configured;
- cancellation;
- bus release/reacquire;
- restart/disposal;
- ambiguous write timeout does not trigger blind retry.

No CI test requires real COM/tty hardware.

### 3.3 Fault/quality expectations

Prove mapping for:

- BCC/framing -> protocol/communication failure;
- address/range -> configuration/device error;
- PLC busy -> device/degraded operation;
- mode/protection denial -> rejected operation;
- timeout -> bad communication/reconnect path;
- ambiguous write -> UNKNOWN / uncertain evidence;
- recovery -> health returns without duplicate Runtime instances.

### L1 acceptance

`L1_GREEN` only when fake-peer coverage proves bounded timeouts, cancellation, reconnect, parser recovery, batching and serial arbitration.

## 4. L2 — independent software/reference evidence

L2 must not reuse the production MEWTOCOL codec as both sides of the test.

### 4.1 Panasonic Control FPWIN Pro7

Current official Panasonic site on 2026-10-06 lists:

- Control FPWIN Pro7 Full/Update 7.7.4.1, dated 2026-01-29;
- Control FPWIN Pro7 Free Basic 7.7.4.1, same date;
- Windows 10/11 support;
- support across Panasonic PLC families including FP0R, FP-XH and FP7.

Use as vendor reference for:

- setting station/network parameters;
- writing known memory values;
- verifying PLC model/profile;
- observing the same addresses independently from EliteSCADA.

Do not redistribute FPWIN Pro with EliteSCADA.

### 4.2 Panasonic FP Data7

Official Panasonic product pages list FP Data7 V1.12.0 as a tool that communicates with Panasonic PLCs and can load/save/edit internal PLC memory.

Use as an independent vendor-side check where compatible with the selected PLC:

`FP Data7 observed memory value == EliteSCADA PointRead / Runtime value`

Do not treat FP Data7 as proof of EliteSCADA protocol implementation correctness by itself.

### 4.3 FPWIN simulation

Panasonic documentation confirms PLC simulation support for several 16-bit PLC types including FP0R.

However, current research does **not** prove that the simulator exposes an independent external MEWTOCOL TCP/serial server endpoint suitable for wire-level interoperability testing.

Therefore:

`FPWIN_SIMULATOR = NOT_L2_PROTOCOL_PEER_UNLESS_EXTERNAL_MEWTOCOL_ENDPOINT_IS_PROVEN`

It may still be useful for PLC program/memory behavior, not for accepting the wire driver.

### 4.4 Community library

`OpenLogics/MewtocolNet` may be used only as optional corroborating evidence after exact-version/license review.

It is not required for L2 and is not a production dependency.

Reasons:

- current repository identifies GPL-3.0;
- NuGet 0.8.1 metadata identifies MIT;
- package version is from 2023;
- current project README states FP7 is not supported;
- only a few PLCs are claimed as tested.

No acceptance test should depend on it.

### L2 acceptance

At least one vendor-side independent tool must observe the same known memory state on real PLC hardware without using EliteSCADA's codec.

## 5. L3 — EliteSCADA product integration

Required future DEV evidence:

### Driver/Data Source

- transport-specific Panasonic Data Source appears in canonical driver catalog;
- no second Driver framework;
- TCP uses DirectNetwork metadata;
- serial uses HostSerial metadata;
- serial port field uses existing `SerialPort` kind;
- no browser serial enumeration.

### TAG binding

- strict portable Panasonic address;
- family/profile range validation;
- Boolean/UInt16/Int16 only in accepted v1;
- unsupported multiword types fail Preview/validation clearly;
- read-only areas reject writes.

### Runtime

- polling registers canonical TAGs;
- samples publish through normal cache/event path;
- Alarm/Historian/Gateway/Scripts/Realtime receive canonical TAG truth;
- no Panasonic-specific secondary Runtime;
- ordinary write goes through `Runtime.WriteAsync(TAG)`.

### PointRead

- draft PointRead works without Apply;
- persisted equivalent works coherently;
- raw/decoded/Engineering/quality/latency evidence;
- protocol code surfaced safely;
- no Active mutation;
- no Historian write;
- no process write.

### Diagnostics

Reuse #500 ladder:

`Host -> Driver/Data Source -> Network -> Connection Test -> Test Read -> Active TAG`

For serial, network step is N/A rather than fabricated.

### Lifecycle

- Save;
- Revision;
- Publish;
- Activate;
- restart/recovery;
- deactivation releases TCP/serial resources;
- package round-trip;
- no host serial discovery snapshot persisted;
- no secrets added by the Panasonic driver.

### HA

- only the High Availability external-effect authority may own active communication;
- passive/recovery node does not duplicate polling/writes;
- transfer/restart does not create two active Panasonic writers.

### L3 acceptance

`L3_GREEN` requires exact-head focused tests plus mounted Engineering evidence and the normal Wave 15 validation profile selected by the future implementation diff.

## 6. L4 — real hardware matrix

### L4-A — compact/current serial baseline

**Preferred PLC:** `AFP0RF32MT`

Official current FP0R product page lists:

- 16 inputs;
- 16 NPN outputs;
- built-in RS-485;
- battery-less data backup.

Purpose:

- current compact Panasonic family;
- true Host Serial acceptance;
- multidrop/station behavior;
- common classic MEWTOCOL-COM address areas.

Alternative if RS-232 is specifically needed:

`AFP0RF32CT` — RS-232C variant.

Required fixture:

- EliteSCADA host + known-good RS-485 adapter;
- #469 Host Serial;
- station configured explicitly;
- actual serial line settings recorded.

Mandatory tests:

- R/Y/DT read;
- R/Y/DT write where safe;
- read-only X rejection;
- at least two contiguous DT words in one planner batch;
- PointRead;
- disconnect/reconnect;
- power-cycle recovery;
- wrong station;
- BCC/error evidence where practical;
- ambiguous serial write fault injection on host side.

### L4-B — current FP-XH Ethernet computer-link baseline

**Preferred PLC:** `AFPXHC14RD`  
**Ethernet communication cassette:** `AFPX-COM5`

Official current FP-XH catalog still lists `AFPXHC14RD`, and current AFPX-COM5 documentation confirms:

- Ethernet on COM1;
- RS-232C on COM2;
- Computer Link over Ethernet;
- TCP server support;
- configurable source port;
- up to three server connections in the documented cassette profile.

Purpose:

- classic MEWTOCOL-COM over TCP;
- current FP-XH family;
- configurable Ethernet endpoint;
- validates cassette-backed Ethernet rather than only integrated-Ethernet CPUs.

Mandatory tests:

- persistent TCP session;
- idle close/reconnect;
- station/profile validation;
- contact + DT reads;
- safe writes + readback;
- planner standard/expanded-frame path where hardware configuration permits;
- RT identity/status;
- PointRead;
- TCP port change;
- activation/restart resource handoff.

### L4-C — current FP7 R-series modern Ethernet baseline

**Preferred CPU:** `AFP7CPS3RE`

Official current FP7 product/manual material lists `AFP7CPS3RE` with integrated Ethernet.

Purpose:

- modern higher-tier Panasonic family;
- native MEWTOCOL-COM bounded common subset;
- explicit proof that v1 does not silently require MEWTOCOL7;
- independent MC Protocol interoperability cross-test for future Mitsubishi work.

Mandatory MEWTOCOL tests:

- TCP MEWTOCOL-COM;
- X/Y/R/DT bounded common area;
- RT/model/status;
- read/write + readback;
- error/mode restriction;
- restart/reconnect;
- PointRead/common diagnostics.

Mandatory MC cross-test:

- enable documented FP7 MC slave profile separately;
- 3E binary bulk read/write only;
- one documented bit device;
- one documented word device;
- record exact mapped Panasonic memory;
- prove the future/common MC client does not claim commands outside the FP7 subset.

MC result is **not** part of Panasonic MEWTOCOL acceptance.

### L4-D — optional modern compact Ethernet expansion candidate

**Optional:** `AFP0HC32ET`

Current FP0H product/manual material shows:

- two Ethernet ports;
- RS-232C;
- MEWTOCOL-COM;
- MEWTOCOL-DAT;
- Modbus TCP;
- MC Protocol;
- up to ten simultaneous connections in the documented Ethernet model.

Use only if budget/time permits.

Purpose:

- modern compact integrated-Ethernet reference;
- future expansion candidate;
- additional cross-family COM evidence.

It does not need to enter the first published v1 compatibility claim.

## 7. Hardware procurement status

The model numbers above are currently listed in Panasonic Industry product/manual material as of 2026-10-06.

This means:

`CURRENT_CATALOG_EVIDENCE`

It does **not** prove local distributor stock, lead time or price.

Before purchasing, procurement must confirm:

- regional orderability;
- lead time;
- required power supply;
- communication cassette/accessories;
- terminal/connector requirements;
- isolated RS-485 adapter where needed.

## 8. Firmware recording rule

Do not invent a target firmware number before hardware is received.

For every L4 execution record:

- full part number;
- hardware revision if exposed;
- firmware version;
- FPWIN Pro version;
- communication-cassette firmware where exposed;
- system register/network configuration;
- MEWTOCOL station;
- Ethernet port or serial line settings;
- test date.

Compatibility is claimed against the observed firmware, not an assumed family-wide firmware.

## 9. Required L4 data set

Prepare known deterministic PLC values:

- one X input or safe simulated/program-driven input;
- one Y output or safe test output;
- two R internal relays;
- at least 16 contiguous DT words;
- signed 16-bit values around:
  - 0;
  - 1;
  - -1;
  - Int16 minimum/maximum;
- UInt16:
  - 0;
  - 1;
  - 0x00FF;
  - 0xFF00;
  - 0xFFFF.

Do not use production machinery for first validation.

Use bench PLCs and safe dummy loads or internal relays/registers.

## 10. Multiword-type gate

Checkpoint 3 does not remove the multiword gate.

A future DEV may only enable UInt32/Int32/Float32 if all are satisfied:

1. official Panasonic representation evidence;
2. L0 known vectors;
3. vendor-tool observation;
4. at least two L4 families agree for the selected profile;
5. no hidden Modbus-style word swap is required by analogy.

Until then:

`V1_TYPES = Boolean + UInt16 + Int16`

## 11. MEWTOCOL7 gate

MEWTOCOL7-COM remains out of v1.

To promote later:

- separate frame/CRC codec;
- official command/address limit matrix;
- FP7 L0 vectors;
- fake peer;
- real FP7 L4;
- explicit address-range benefit proven;
- no regression of classic COM product.

## 12. Acceptance claim after L4

Minimum publishable initial claim:

`Panasonic MEWTOCOL-COM compatible with the exact validated FP0R / FP-XH / FP7 models and transports listed in the release compatibility matrix.`

Do not broaden beyond tested family/profile/model/firmware evidence.

## 13. Required final evidence artifact

Future DEV/hardware acceptance should produce a table:

| Model | Firmware | Transport | Dialect | Station | Areas | Types | Read | Write | PointRead | Restart | Level | Date |
|---|---|---|---|---:|---|---|---|---|---|---|---|---|

and attach raw sanitized evidence without Panasonic proprietary software redistribution.

## 14. Current disposition

`PANASONIC_MEWTOCOL = GO_WITH_GATES`

`L0-L3 = FUTURE_DEV_REQUIRED`  
`L4 = HARDWARE_REQUIRED_FOR_PUBLISHED_COMPATIBILITY`

`DOCS_ONLY`  
`NO PRODUCT CODE CHANGED`  
`NO DEPENDENCY CHANGED`  
`NO CI CHANGED`  
`NO MERGE PERFORMED`
