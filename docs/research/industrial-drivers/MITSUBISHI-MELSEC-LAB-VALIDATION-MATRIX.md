# Mitsubishi MELSEC MC/SLMP lab validation matrix

## Status

Contract: **C-INDUSTRIAL-MITSUBISHI-MELSEC-RESEARCH-01**

Research order: **INDUSTRIAL-MITSUBISHI-MELSEC-EXECUTION-RESEARCH-01**

Checkpoint: **3 — L0-L4 + hardware + legal**

Decision:

**MITSUBISHI_MELSEC = GO_WITH_GATES**

This is a research validation contract. It does not contain an implementation and does not claim that untested hardware is supported.

## 1. Exact v1 under test

| Dimension | v1 |
| --- | --- |
| DriverType | mitsubishi.melsec.mc |
| display name | Mitsubishi MELSEC MC/SLMP — LEGAL_REVIEW_REQUIRED |
| wire terminology | SLMP 3E / MC Protocol QnA-compatible 3E |
| transport | TCP |
| encoding | Binary |
| session | persistent per Data Source |
| outstanding requests | one |
| core read | 0401 Batch Read |
| read optimization | 0403 Random Read / 0406 Read Block |
| write | 1401 Batch Write for one canonical contiguous effect |
| devices | X/Y/M/L/B/D/W subject exact profile |
| R | profile-gated |
| data types | Bit, Int16, UInt16, Int32, UInt32, Float32 |
| dependency | built-in managed .NET; none external |

## 2. Validation-level rules

### L0 — unit/model

No external peer.

Pass requires deterministic independent expected vectors for:

- frame codec;
- address parser;
- device codes;
- response/end-code handling;
- malformed/truncated frames;
- request segmentation;
- planner grouping/demultiplexing;
- typed conversions;
- retry/ambiguous-write state machine.

### L1 — independent-shaped fake peer

Real TCP socket.

Fake peer must not reuse the production frame parser/encoder.

Pass requires:

- real request/response;
- delayed/chunked responses;
- malformed/exception responses;
- disconnect/reconnect;
- batch splitting;
- write-response loss;
- no replay.

### L2 — independent software/vendor peer

Preferred candidate:

**GX Simulator3**, under a valid GX Works3 license.

Pass only when:

- future EliteSCADA connects directly to the simulator socket;
- actual raw 3E frames are demonstrated;
- no MX Component adapter/proxy carries the production path;
- exact software version/configuration is recorded.

If the direct protocol path cannot be independently demonstrated:

**L2 = SKIP_WITH_REASON**

Do not replace it with production-code loopback.

### L3 — actual EliteSCADA integration

Pass requires:

- Data Source;
- TAG;
- ConnectionTest;
- PointRead;
- Runtime polling;
- Runtime.WriteAsync;
- diagnostics;
- Preview/Apply/Save/Publish/Activate;
- restart;
- revision switch;
- multiple Data Sources;
- HA authority.

### L4 — physical hardware

Mandatory before first public compatibility claim:

1. FX5U-32MT/DS
2. R04ENCPU rig

Every public family claim needs its own accepted L4 row.

## 3. L0 frame vectors

Required cases:

| ID | Area | Expected |
| --- | --- | --- |
| L0-FRAME-001 | minimal valid 3E binary 0401 request | exact independent bytes |
| L0-FRAME-002 | normal 0401 response | exact decode |
| L0-FRAME-003 | 1401 word write | exact independent bytes |
| L0-FRAME-004 | 1401 bit write | exact independent bytes |
| L0-FRAME-005 | 0403 mixed word/dword | exact independent bytes |
| L0-FRAME-006 | 0406 multiple blocks | exact independent bytes |
| L0-FRAME-007 | 0101 type-name request | exact independent bytes |
| L0-FRAME-008 | direct route defaults | network/station/module/multidrop exact |
| L0-FRAME-009 | non-default allowed route | exact route bytes |
| L0-FRAME-010 | monitoring timer | exact 250-ms unit encoding |
| L0-FRAME-011 | nonzero end code | failure preserved |
| L0-FRAME-012 | malformed/truncated response | rejected |
| L0-FRAME-013 | impossible response length | rejected |
| L0-FRAME-014 | trailing/inconsistent data | rejected |

Expected vectors must be authored from official protocol evidence independently of the production implementation.

## 4. L0 address/device vectors

For each v1 device:

- minimum address;
- normal address;
- family/profile maximum;
- one above maximum;
- lowercase input normalization;
- leading-zero normalization.

Specific syntax negatives:

- D-1
- D 100
- D0x100
- X0x10
- XG1
- M1A
- D100:2
- 10.0.0.1/D100
- D100@1
- unsupported ZR100
- timer TN0 while disabled.

Required device-code assertions:

- X = selected common device code;
- Y;
- M;
- L;
- B;
- D;
- W;
- R only under enabled profile.

Unknown mnemonic fails closed.

## 5. L0 typed-value vectors

### Bit

- false;
- true;
- adjacent odd/even bit count packing cases.

### Int16

- -32768;
- -1;
- 0;
- 1;
- 32767.

### UInt16

Physical values:

- 0;
- 32767;
- 32768;
- 65535.

Canonical value must remain lossless in Int32.

### Int32

- representative negative;
- zero;
- representative positive;
- boundary raw patterns.

### UInt32

- 0;
- 2147483647;
- 2147483648;
- 4294967295.

Canonical Int64 must remain lossless.

### Float32

Use exact known IEEE-754 raw patterns for:

- 0;
- 1;
- -1;
- representative positive fraction;
- representative negative fraction.

### Ordering

Prove:

- lower byte first in a protocol word;
- lower-numbered device word = low 16 bits;
- next device word = high 16 bits;
- default ByteSwap=false;
- default WordSwap=false;
- explicit supported transforms are deterministic.

## 6. L0 planner matrix

| Case | Requirement |
| --- | --- |
| adjacent same-area points | one contiguous candidate |
| different device mnemonics | do not merge incorrectly |
| different Data Sources | never merge |
| different routes | never merge |
| Int32 at segment edge | move boundary; do not split |
| Float32 at segment edge | move boundary; do not split |
| protocol hard limit | split safely |
| family/profile lower limit | use lower bound |
| operational soft cap | use lower bound |
| unsupported gap | do not read through |
| sparse valid words | evaluate 0403 |
| multiple contiguous runs | evaluate 0406 |
| optimizer cannot prove validity | fallback to bounded 0401 |
| malformed response | publish no shifted values |

## 7. L0 write-state matrix

| State | Expected |
| --- | --- |
| invalid binding | fail before I/O |
| connect failure before dispatch | bounded retry may occur |
| normal response/end code 0 | protocol acknowledged |
| nonzero end code | fail; preserve code |
| timeout after possible dispatch | ambiguous |
| ambiguous + matching readback | confirmed-by-readback diagnostic |
| ambiguous + mismatching readback | remain failed/unknown |
| reconnect | no replay |
| PLC restart | no replay |
| revision switch | no replay |
| HA transfer | no replay |

## 8. L1 fake-peer requirements

The fake peer must implement independently:

- 3E framing;
- direct/default route validation;
- 0401;
- 1401;
- 0403;
- 0406;
- 0101;
- deterministic bit/word store.

It must expose test controls for:

- delay before response;
- split response at arbitrary byte offsets;
- close immediately after request;
- apply write then close before response;
- return selected end code;
- wrong declared length;
- truncated response;
- unexpected subheader;
- reject address;
- restart listener;
- stop responding;
- delayed old response.

It must log:

- connection sequence;
- request sequence;
- write sequence;
- command/subcommand;
- target area/address;
- applied data.

## 9. L1 acceptance scenarios

| ID | Scenario | Pass criterion |
| --- | --- | --- |
| L1-001 | persistent scanning | multiple cycles, one healthy session |
| L1-002 | concurrent callers | at most one 3E request in flight |
| L1-003 | contiguous batch | expected request grouping |
| L1-004 | segmentation | exact boundaries, no typed split |
| L1-005 | Random Read | correct sparse decode |
| L1-006 | Read Block | correct multi-block decode |
| L1-007 | read timeout | reset/reconnect, bounded retry |
| L1-008 | malformed response | fail/reset, no wrong TAG value |
| L1-009 | explicit end code | correct error category |
| L1-010 | write ACK | complete after valid response |
| L1-011 | write applied/response lost | ambiguous, no resend |
| L1-012 | ambiguous readback matches | confirmation recorded |
| L1-013 | ambiguous readback differs | operation remains failed/unknown |
| L1-014 | reconnect after write loss | fake write log proves no replay |
| L1-015 | server restart | fresh read truth after recovery |
| L1-016 | session cancellation | deterministic stop/no leaked task |

## 10. L2 candidate: GX Simulator3

Required environment:

- licensed current GX Works3;
- GX Simulator3;
- simulated iQ-F or iQ-R CPU;
- isolated lab port;
- packet capture.

Preflight:

1. start simulator;
2. record system No. and PLC No.;
3. record resulting simulator port;
4. independently verify the endpoint with a separate external tool/client if available;
5. capture evidence that actual raw 3E traffic is accepted.

EliteSCADA L2:

1. future built-in Mitsubishi codec connects directly;
2. 0101 or bounded known read;
3. D/M read/write;
4. PointRead;
5. simulator stop/start;
6. reconnect;
7. no write replay.

If step 5 in Preflight cannot establish raw 3E:

**L2 result = SKIPPED — GX Simulator3 direct raw-3E endpoint not proven**

Do not route EliteSCADA through MX Component and count that as native-driver L2.

## 11. L2 optional oracle record

If McpX is used:

~~~text
tool = McpX
exactVersion =
exactCommit =
license = MIT
source =
runtime =
simulatorOrHardware =
operations =
result =
capturedWireHash =
~~~

As of research date, NuGet 0.9.1 was observed, while public project pages show evolving GX Simulator support. Revalidate exact version immediately before use.

## 12. L3 Engineering matrix

| ID | Scenario | Expected |
| --- | --- | --- |
| L3-ENG-001 | create Data Source | canonical settings only |
| L3-ENG-002 | invalid host/route | Preview error |
| L3-ENG-003 | invalid TAG address | Preview error |
| L3-ENG-004 | unsupported device for profile | Preview error |
| L3-ENG-005 | valid Preview | no mutation |
| L3-ENG-006 | Apply | Working mutated only |
| L3-ENG-007 | Save Revision | durable revision |
| L3-ENG-008 | Publish | publication only |
| L3-ENG-009 | Activate | runtime starts |
| L3-ENG-010 | revision switch | old runtime stops before replacement ownership |

## 13. L3 ConnectionTest matrix

- reachable valid endpoint;
- refused endpoint;
- timeout;
- wrong configured port;
- wrong route;
- Read Type Name success where supported;
- nonzero end code;
- sanitized endpoint/error;
- no secret value;
- no TAG registration.

## 14. L3 PointRead matrix

For each first family and representative device:

- X Boolean;
- Y Boolean;
- M Boolean;
- D Int16;
- D UInt16;
- D Int32;
- D UInt32;
- D Float32;
- W numeric where enabled;
- B bit where enabled.

Verify:

- exact PortableAddress;
- raw representation;
- decoded representation;
- Engineering representation;
- quality;
- latency;
- no Working mutation;
- no Active Runtime authority granted.

## 15. L3 runtime scan matrix

- contiguous D points use batching;
- multiple physical types decode correctly;
- sparse D points use chosen plan or safe fallback;
- bit groups decode correctly;
- one bad configured point does not shift neighboring identities;
- communication failure marks affected quality;
- recovery uses fresh responses;
- diagnostic counters align with actual requests.

## 16. L3 Runtime.WriteAsync matrix

- writable D;
- writable M/Y;
- read-only X rejected;
- driver write-disabled policy if present;
- protocol ACK;
- nonzero end code;
- timeout before dispatch;
- ambiguous timeout after possible dispatch;
- optional readback;
- no fake publication of requested value;
- no retry/replay after possible dispatch.

## 17. L3 diagnostics matrix

Required common fields:

- Data Source;
- Driver type;
- endpoint;
- state;
- last success/failure;
- data age;
- latency;
- request/read/write counters;
- timeout/reconnect counters;
- TAG-quality summary.

Required safe protocol details:

- transport=tcp;
- frame=3e;
- encoding=binary;
- family profile;
- route;
- model identity if observed;
- last command/subcommand;
- last end code;
- last batch shape;
- last RTT;
- ambiguous-write count/status.

No password, secret, raw protected material or arbitrary process dump.

## 18. L3 multi-Data-Source matrix

At least:

- Mitsubishi A healthy;
- Mitsubishi B healthy;
- fail A only;
- B remains healthy;
- recover A;
- write to A reaches A only;
- write to B reaches B only;
- counters isolated.

When practical, run a different protocol Data Source in parallel and prove no cross-driver ownership breakage.

## 19. L3 HA matrix

**HA = High Availability**

Required:

| Scenario | Expected |
| --- | --- |
| authoritative node write | allowed |
| non-authoritative node write | fenced |
| authority lost mid-lifetime | new effects fenced |
| authority transferred | no old queued write replay |
| new authority | fresh session/read truth |
| read acquisition on allowed architecture | follows current HA contract |
| diagnostics | correctly identify active runtime state |

## 20. L4 evidence header

Every hardware result begins with:

~~~text
testId =
testDateUtc =
EliteSCADACommit =
driverContract =
driverType = mitsubishi.melsec.mc

family =
cpuModel =
serial =
hardwareRevision =
firmware =
ethernetProfile =
ethernetModule =
engineeringSoftware =
projectHash =
ip =
port =
frame = 3E
encoding = Binary
transport = TCP
networkNo =
stationNo =
moduleIoNo =
multidropStationNo =

result =
limitations =
operator =
evidenceFiles =
~~~

## 21. L4 bench A — FX5U-32MT/DS

Mandatory inventory:

- FX5U-32MT/DS;
- 24 V DC PSU;
- Ethernet;
- safe input switching;
- safe output loads;
- current licensed GX Works3.

Required physical cases:

1. X input OFF -> ON -> OFF.
2. Y output command OFF -> ON -> OFF.
3. independent observation of Y state/output.
4. D word values.
5. D multiword values.
6. Float32.
7. M internal bit.
8. B/W if enabled by the selected profile.
9. L only after profile/range validation.
10. R only after explicit file-register configuration.

Required fault cases:

- unplug Ethernet;
- restore Ethernet;
- restart CPU;
- invalid port/route;
- verify reconnect;
- verify no write replay.

## 22. L4 bench B — R04ENCPU

Mandatory inventory:

- R04ENCPU;
- R35B;
- R63P;
- RX40C7;
- RY40NT5P;
- 24 V DC bench supply/distribution;
- safe input switching;
- safe output loads;
- current licensed GX Works3.

Required physical cases:

1. RX40C7 X input OFF -> ON -> OFF.
2. RY40NT5P Y output command OFF -> ON -> OFF.
3. independent observation of output.
4. D word values.
5. D multiword values.
6. Float32.
7. M internal bit.
8. B/W when profile permits.
9. L only after profile/range validation.
10. R only after explicit profile configuration.

Fault cases match FX5U plus any iQ-R-specific Ethernet/profile restart evidence.

## 23. L4 data-type acceptance

For each mandatory bench retain a table:

| Address | Physical type | Raw before | Canonical before | Write | Raw after | Canonical after | Quality | PASS |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |

Include all v1 physical types.

## 24. L4 batching acceptance

Record:

- TAG count;
- planned blocks;
- command used;
- points/words per block;
- request count per scan;
- last/average RTT;
- scan duration;
- PLC observed scan effect where available.

Acceptance:

- no one-request-per-TAG fallback unless topology genuinely requires it;
- no hard-limit overflow;
- no typed split;
- stable values/quality;
- no uncontrolled CPU scan impact.

## 25. L4 ambiguous-write evidence

Preferred controlled method:

- route PLC traffic through an isolated test bridge/proxy capable of dropping a response after forwarding the request;
- verify target memory independently;
- capture request;
- suppress response;
- observe EliteSCADA ambiguous result;
- reconnect;
- readback;
- prove no second write appeared on packet capture/PLC observation.

If exact physical fault timing cannot be deterministic:

- retain L1 deterministic proof;
- run best-effort L4 fault;
- document limitation explicitly;
- Main decides release sufficiency.

## 26. Hardware procurement list

### Priority P0 — required

**Bench A**
- 1 x FX5U-32MT/DS

**Bench B**
- 1 x R04ENCPU
- 1 x R35B
- 1 x R63P
- 1 x RX40C7
- 1 x RY40NT5P

**Common**
- appropriately sized 24 V DC supplies;
- protected terminal/distribution hardware;
- Ethernet switch;
- patch leads;
- test switches;
- safe resistive/indicator loads;
- engineering workstation;
- packet-capture/fault-injection host.

### Priority P1 — software

- valid current GX Works3/iQ Works licensing;
- GX Simulator3;
- optional licensed MX Component Version 5 for independent comparison.

### Priority P2 — expansion

- Q03UDVCPU-based Q rig only after Q support approval;
- MELSEC-L only for explicit installed-base commercial need;
- FX3 only under a separate 1E contract.

## 27. Legal evidence checklist

Before external release:

- [ ] legal approves public display name;
- [ ] no Mitsubishi logo bundled without permission;
- [ ] no Mitsubishi manual PDFs/pages copied into product;
- [ ] protocol facts independently paraphrased;
- [ ] no claim of endorsement/certification without evidence;
- [ ] compatibility table lists exact tested hardware;
- [ ] proprietary Mitsubishi tools are not redistributed;
- [ ] all open-source lab tools have pinned versions/licenses/notices;
- [ ] product dependency list still shows no Mitsubishi runtime library dependency.

## 28. Final release checklist

Before Main can describe the v1 as release-ready:

- [ ] L0 PASS
- [ ] L1 PASS
- [ ] L2 PASS or Main-accepted SKIP_WITH_REASON
- [ ] L3 PASS
- [ ] FX5U-32MT/DS L4 PASS
- [ ] R04ENCPU rig L4 PASS
- [ ] exact firmware/hardware recorded
- [ ] security deployment guidance present
- [ ] no write replay proof
- [ ] PointRead proof
- [ ] diagnostics proof
- [ ] Save/Publish/Activate proof
- [ ] HA proof
- [ ] legal/public naming cleared

## 29. Final decision

**MITSUBISHI_MELSEC = GO_WITH_GATES**

The research path is implementation-ready, but release claims remain gated by the evidence above.

## 30. Sources

Accessed **2026-10-06**.

Primary protocol/family sources are listed in the execution research and address/capability matrix.

Current hardware/software/lifecycle/legal sources include:

- https://www.mitsubishielectric.com/fa/products/faspec/point.page?category=ex&formNm=FX5-_M-D-_FX5U-32MT%2FDS_19&id=spec&kisyu=%2Fplcf&lang=2
- https://www.mitsubishielectric.com/fa/products/faspec/point.page?formNm=RnENCPU_R04ENCPU_3323&kisyu=%2Fplcr&lang=2
- https://www.mitsubishielectric.com/fa/products/faspec/download.page?formNm=RnENCPU_R04ENCPU_3323&kisyu=%2Fplcr&lang=2&popup=1
- https://www.mitsubishielectric.com/fa/products/faspec/point.page?formNm=R35B&kisyu=%2Fplcr&popup=1
- https://www.mitsubishielectric.com/fa/id_en/products/faspec/point.page?formNm=RnP_R63P_3430&kisyu=%2Fplcr&lang=2
- https://www.mitsubishielectric.com/fa/products/faspec/point.page?formNm=RX40C7&kisyu=%2Fplcr&popup=1
- https://www.mitsubishielectric.com/fa/products/faspec/point.page?category=ex&formNm=R_IO_RY40NT5P_3432&id=spec&kisyu=%2Fplcr&lang=2
- https://www.mitsubishielectric.com/fa/document/technews/plc/fa-a-0466/faa0466a.pdf
- https://www.mitsubishielectric.com/fa/products/cnt/plceng/smerit/gx_works3/index.html
- https://www.mitsubishielectric.com/fa/products/cnt/plceng/smerit/gx_works3/debug.html
- https://www.mitsubishielectric.com/fa/products/faspec/detail.page?formNm=SW5DND-ACT_SW5DND-ACT-E_6364&kisyu=%2Fplcq&lang=2
- https://www.mitsubishielectric.com/en/terms/
- https://br.mitsubishielectric.com/pt/terms/
- https://www.nuget.org/packages/McpX/0.9.1
- https://www.nuget.org/packages/e_MCProtocol/
- https://store.sim3d.com/demo3d_2025/configuring_a_mitsubishi_connection_using_slmp

**DOCS_ONLY**

**NO PRODUCT CODE CHANGED**

**NO DEPENDENCY CHANGED**

**NO CI CHANGED**

**NO MERGE PERFORMED**

**HA = High Availability**

**HAB = Home Assistant Bridge**
