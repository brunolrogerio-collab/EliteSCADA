# Allen-Bradley EtherNet/IP + CIP / Logix driver research

Status: **RESEARCH IN PR / PRODUCTION NOT IMPLEMENTED**

Research date: 2026-08-27

This document is a research/specification spike for a future EliteSCADA Allen-Bradley / Rockwell Automation communication driver. It does **not** add a production Data Source, protocol dependency, runtime connection, Engineering schema change, API, frontend behavior or DriverHost integration.

The intended first product target is SCADA/HMI access to **ControlLogix and CompactLogix Logix-family controllers** over EtherNet/IP using CIP explicit messaging and Rockwell Logix symbolic TAG services. Generic EtherNet/IP implicit I/O scanning, legacy PCCC/data-table PLC families, controller programming, RUN/STOP, firmware operations and safety engineering are deliberately outside the initial driver scope.

The design must preserve existing EliteSCADA boundaries:

`Data Source -> Driver / Source Provider -> TAG Engine -> Event Bus -> Historian / Alarms / Realtime / Gateway`

A future Allen-Bradley driver is one more Data Source implementation. It does not create a parallel runtime model, and it does not communicate directly with other drivers.

---

## 1. Executive recommendation

1. **Target ControlLogix and CompactLogix first.** Treat modern Logix controllers as the initial compatibility family. Do not claim that Micro800, PLC-5, SLC 500 or MicroLogix are the same driver profile merely because they can use Ethernet or CIP-related transports.
2. **Use EtherNet/IP explicit messaging only in the first production scope.** SCADA TAG read/write is request/response data access, not cyclic I/O scanner ownership. ODVA distinguishes explicit TCP-based request/response messaging from implicit UDP I/O connections. EliteSCADA should not become a generic EtherNet/IP I/O originator as a side effect of adding Logix TAG access.
3. **Treat Rockwell Logix TAG access as vendor-specific services above CIP/EtherNet/IP.** Rockwell documents vendor-specific Read Tag, Read Tag Fragmented, Write Tag, Write Tag Fragmented and Read Modify Write services. A generic CIP object client alone is not evidence of complete Logix symbolic TAG support.
4. **Persist symbolic Engineering identity, not online browse instance numbers.** A canonical binding should retain controller/program scope plus symbolic path/member/index information. Symbol Object instance IDs, Template Object instance IDs/handles and other browse/session artifacts are runtime/import caches and must be refreshable after controller project changes.
5. **Start with one bounded communication context per Data Source.** Connection reuse, connected explicit messaging and parallel channels may be evaluated later, but the initial implementation should favor deterministic bounded request concurrency and measured controller load rather than maximizing sockets.
6. **Use unconnected explicit messaging as the safest first laboratory baseline unless hardware evidence proves a connected-explicit strategy is materially better.** Connected explicit messaging reserves connection resources. The production adapter may later support a bounded connected mode, but Engineering must not assume that every controller/network path has the same connection budget.
7. **Fail closed for writes.** A TAG is writable only when EliteSCADA Engineering permits it **and** the controller/tag evidence supports external write access. `External Access = Read Only` or `None`, constant definitions, unsupported safety data, unresolved type/structure metadata and ambiguous target identity must prevent writes rather than waiting for repetitive runtime failures.
8. **Use L5X as the preferred file-based Engineering import candidate, with L5K as an optional fallback.** L5X is XML and can carry project/component/tag/type metadata. Import is Engineering-side only and must enter the canonical `parse -> validate -> preview -> choose merge mode -> apply` workflow. Runtime must never depend on Studio 5000 being installed.
9. **Support online browse as candidate discovery, not as hidden Engineering truth.** Rockwell publicly documents Symbol Object (`0x6B`), Template Object (`0x6C`) and paged tag enumeration. Browse should create/select candidates, preserve type/scope metadata and require Preview/Apply before canonical Engineering changes.
10. **Treat UDTs, arrays and BOOL packing as first-class protocol concerns.** A driver that can read only scalar DINT/REAL tags is useful for a proof but insufficient for a serious Logix SCADA integration.
11. **Make CIP Security an explicit compatibility/security gate.** ODVA specifies TLS for TCP-based EtherNet/IP traffic and X.509/PSK endpoint authentication. If the selected stack cannot communicate with a controller policy that requires CIP Security, EliteSCADA must report that limitation honestly. It must never silently downgrade to unsecured communication.
12. **Use `libplctag` / `libplctag.NET` as the strongest first laboratory candidate, not as a production dependency decision.** The core is mature, actively maintained and focused on Allen-Bradley TAG access, but it introduces a native C runtime and licensing/packaging considerations. The .NET wrapper also needs current-version and cancellation/reconnect validation.
13. **Keep `RustEtherNetIp` as a promising second lab candidate.** It is unusually aligned with .NET 10, routed Logix access, batching, UDTs and current real-hardware claims, but its 2026 release line and low adoption make it too young to select by documentation alone.
14. **Keep EEIP.NET / EEIP.NetStandard as a generic managed CIP/EtherNet/IP comparison tool, not the presumed Logix driver.** Its documented strength is standard CIP objects plus explicit/implicit messaging; the research did not find equivalent evidence for the complete Rockwell Symbol/Template/tag-fragmentation/browse model.
15. **Require real CompactLogix and ControlLogix acceptance before production.** FactoryTalk Logix Echo is valuable for HMI/Class 3 explicit-message integration testing, and software protocol fixtures can validate framing/error paths, but neither proves real chassis routing, controller resource limits, firmware-specific behavior, CIP Security policy, restart/recovery or plant-network failure modes.
16. **Do not implement CIP by folklore.** ODVA controls the authoritative CIP/EtherNet/IP specifications and development/licensing terms. Public Rockwell manuals are sufficient for architecture research and laboratory planning, but a production protocol implementation that goes below a vetted library requires formal access to the applicable ODVA specifications and legal/license review.

---

## 2. Scope and family classification

### 2.1 Initial production family

The first future driver should explicitly target:

- ControlLogix 5570/5580-class controllers where supported by the selected stack and firmware;
- CompactLogix 5370/5380-class controllers where supported by the selected stack and firmware;
- GuardLogix/Compact GuardLogix only for **standard non-safety SCADA data** that is intentionally externally accessible, after separate hardware validation.

The practical first laboratory pair should be a current CompactLogix 5380 and a current ControlLogix 5580-class controller. This gives both a direct/embedded Ethernet case and a chassis/routed case.

### 2.2 Micro800 is a separate profile

Micro800 uses EtherNet/IP/CIP concepts but has a different engineering ecosystem and symbolic behavior. Current Rockwell FactoryTalk Design Workbench documentation shows Micro800 CIP symbolic messaging and syntax such as `PROGRAM:<program>,<symbol>`, and documents its own global-variable/access rules.

Therefore:

- do not include Micro800 in the first `rockwell.logix` compatibility promise;
- evaluate it later as a separate `Micro800` profile or separate driver module;
- do not reuse ControlLogix browse, type or route assumptions without evidence;
- keep shared EtherNet/IP transport helpers possible, but keep product compatibility honest.

### 2.3 PLC-5, SLC 500 and MicroLogix are legacy/data-table families

Legacy Allen-Bradley families commonly use PCCC/data-table addressing and different file-based semantics. A future compatibility module may share transport infrastructure, but it must not be disguised as Logix symbolic TAG access.

Initial product language should therefore say **Allen-Bradley Logix EtherNet/IP/CIP** rather than the overly broad claim “all Allen-Bradley Ethernet PLCs.”

### 2.4 Explicit exclusions

The first SCADA driver must not expose:

- generic EtherNet/IP implicit I/O scanner/originator ownership;
- controller mode changes (RUN/PROGRAM/REMOTE);
- program/routine download or upload;
- firmware update;
- force installation/enabling;
- safety signature/project changes;
- safety programming or bypass;
- motion commissioning;
- module configuration writes unrelated to TAG process data;
- arbitrary raw CIP service execution exposed to normal operators/scripts;
- Studio 5000 automation as a runtime dependency.

If future administrative tooling ever needs any of these capabilities, it requires a separately designed product surface, authorization model, confirmation flow and Audit policy.

---

## 3. EtherNet/IP/CIP messaging model for EliteSCADA

### 3.1 Standard EtherNet/IP versus Logix TAG services

ODVA EtherNet/IP provides the standard network/application framework. Rockwell Logix controllers then expose vendor-specific data-access services used by HMI/SCADA clients.

That distinction must remain visible in the architecture:

- **EtherNet/IP/CIP transport/session/routing**: common protocol infrastructure;
- **Logix symbolic TAG access**: Rockwell-specific object/service behavior;
- **EliteSCADA TAG**: protocol-neutral runtime value owned by one Data Source.

The public Engineering model should never persist a library-specific handle such as `libplctag` attributes as its sole truth.

### 3.2 Explicit messaging is the first SCADA scope

ODVA documents:

- unconnected explicit messaging through UCMM/TCP for connection setup and infrequent request/response work;
- connected explicit messaging as point-to-point request/response communication with reserved connection resources;
- implicit I/O as periodic application-specific data carried using UDP and producer/consumer semantics.

The first EliteSCADA Logix driver should be a **Messaging Class client/originator for explicit TAG access**, not an I/O scanner.

Why:

- SCADA polls and writes symbolic process variables rather than owning a controller I/O connection contract;
- implicit I/O requires pre-agreed assembly/connection formats, timing and ForwardOpen lifecycle;
- implicit I/O changes network and controller resource behavior substantially;
- production HMI compatibility can be achieved with class-3/explicit semantics without making EliteSCADA responsible for real-time I/O ownership.

### 3.3 Unconnected versus connected explicit messaging

Initial recommendation:

- laboratory baseline: unconnected explicit messaging over the normal EtherNet/IP TCP session;
- allow a future adapter to use connected explicit messaging only behind a bounded connection strategy proven on hardware;
- never let a library default silently consume arbitrary controller connection resources;
- expose effective messaging mode in diagnostics if a future implementation supports both.

Connected explicit messaging can improve repeated request efficiency but reserves resources. A high-TAG-count SCADA deployment can contain many Data Sources, so one controller’s optimization must not become a fleet-wide connection exhaustion policy.

### 3.4 Rockwell Logix TAG services

Rockwell’s current `1756-PM020I-EN-P` programming manual documents these vendor-specific tag services:

| Service | Code | Initial EliteSCADA relevance |
| --- | ---: | --- |
| Read Tag | `0x4C` | normal scalar/small array/member reads |
| Read Tag Fragmented | `0x52` | values too large for one response |
| Write Tag | `0x4D` | bounded normal writes |
| Write Tag Fragmented | `0x53` | larger supported writes after strict validation |
| Read Modify Write Tag | `0x4E` | specialized atomic/bit-mask use; not a general first-write primitive |
| Multiple Service Packet | standard CIP service | batching multiple compatible requests when stack/controller allow |

The manual also distinguishes symbolic-segment addressing and Symbol Instance Addressing (available on supported Logix versions). Instance addressing can reduce path overhead after discovery, but the instance number must remain a cache optimization, never the persisted Engineering identity.

---

## 4. Endpoint and CIP route model

### 4.1 Data Source connection identity

A future public versioned Allen-Bradley Data Source schema should eventually represent, at minimum:

- stable EliteSCADA Data Source ID/key/name;
- driver type/versioned schema identity, independent of implementation library;
- host/IP;
- TCP port, default EtherNet/IP port `44818` unless an explicitly supported profile says otherwise;
- controller family/profile;
- ordered CIP route path, possibly empty for a direct embedded-Ethernet target;
- connect timeout;
- request timeout;
- reconnect/backoff limits;
- scan classes / default scan interval;
- bounded in-flight request limit;
- optional explicit-messaging mode when/if connected explicit is supported;
- security policy/certificate or PSK references when CIP Security is implemented;
- non-secret identity expectations useful to detect accidental connection to the wrong controller.

Secret/certificate material must follow EliteSCADA protected secret-reference rules and never be exported as plaintext Engineering.

### 4.2 Route path semantics

A CIP route is an ordered sequence of **port/link** hops. It must not be reduced to a single hidden “slot” integer in the canonical model.

Rockwell’s ControlLogix EtherNet/IP network documentation uses:

- port `1` for backplane;
- port `2` for Ethernet in the documented module context;
- a common local chassis path `1,<slot>`.

That is an important common case, not a universal topology rule.

Examples of Engineering intent:

- CompactLogix with embedded Ethernet and no bridge hop: direct target profile / empty route where supported by the stack;
- ControlLogix through a chassis EtherNet/IP module to CPU slot 0: route containing a backplane hop to slot 0;
- remote chassis/bridge topology: multiple ordered port/link segments.

The UI may provide convenient chassis/slot helpers, but the public driver schema should preserve the effective route, and connection-test diagnostics should display a sanitized human-readable route.

### 4.3 Target identity validation

Where the selected stack exposes CIP Identity Object information, the connection test should record/compare non-secret identity such as vendor/product/device type/revision/serial identity where appropriate.

Suggested policy:

- first connection may show observed identity as informational;
- Engineering may optionally pin expected controller identity;
- a mismatch after activation should fail closed or at least prevent writes according to explicit policy;
- IP address alone is not sufficient protection against maintenance/network reassignment.

The exact identity fields and matching policy require the future public driver-schema slice.

---

## 5. Canonical Logix TAG binding identity

### 5.1 Persist symbolic meaning, not browse handles

A future protocol-specific binding should conceptually contain:

```text
scope: Controller | Program
programName: optional, required for Program scope
symbolPath: canonical symbolic base/member/index path
expectedLogixType: optional validated type fingerprint/descriptor
accessIntent: Read | ReadWrite
```

Illustrative canonical symbols:

```text
Controller:Tank01.Level
Controller:Line1.Motor[3].Speed
Program:Packaging:Batch.CurrentRecipe
Program:Packaging:Machine.Status.Running
```

The exact serialization syntax is a future Engineering-contract decision. The important semantic rule is that controller/program scope and symbolic path are explicit.

### 5.2 Program-scoped addressing

Rockwell documents program-scoped remote TAG references for Logix messaging using a form such as:

`Program:<program_name>.<tag_name>`

EliteSCADA should not make this vendor text syntax the only structured truth. It should store scope/program/symbol separately and generate the adapter-specific request path.

### 5.3 Runtime-only/cache metadata

The following may be cached but must not become stable Engineering identity:

- Symbol Object instance ID;
- Template Object instance ID;
- Template Handle / Structure Handle;
- cached encoded CIP path bytes;
- packet/session sequence values;
- negotiated connection IDs;
- library-native handles;
- browsed list offsets/pagination cursors.

A controller project edit may add/delete tags or modify UDT definitions. Rockwell explicitly warns clients that tag/structure lists need to be refreshed when the program changes. Persisting an instance ID as if it were a stable TAG address would therefore create a dangerous stale-binding failure mode.

### 5.4 Refresh/reconciliation rule

After browse/import refresh:

1. match candidate by Data Source + scope + normalized symbolic path;
2. compare observed data type/shape/access metadata with the currently engineered binding;
3. show create/update/unchanged/conflict/unsupported in Preview;
4. preserve the existing EliteSCADA stable TAG ID when the canonical logical binding remains the same;
5. require explicit engineer action for incompatible type or scope change;
6. never silently rebind an existing EliteSCADA TAG to a different controller symbol because an online instance number was reused.

---

## 6. Online browse and structure discovery

### 6.1 Public Rockwell browse model

Rockwell’s Data Access Programming Manual documents two vendor-specific objects important for TAG-aware clients:

- **Symbol Object, Class `0x6B`**: symbol name/type information;
- **Template Object, Class `0x6C`**: structure template/member/size/handle information.

The manual documents `Get_Instance_Attribute_List (0x55)` for paged Symbol Object enumeration. A single response may not contain all symbols, so the client must continue from the last returned instance until successful completion.

For structures, Template Object information is needed to understand member names, member order, member data types and packing when a whole structure is read/written.

### 6.2 Browse is an Engineering operation

Proposed workflow:

`Connect/Test -> Browse -> Filter candidates -> Select -> Validate mappings -> Preview -> Apply`

Browse must not automatically create canonical TAGs.

Candidate information should include:

- scope and program where known;
- symbolic path;
- Logix type code/name;
- dimensions;
- structure/template information where available;
- description if provided by the chosen source/import path;
- External Access if available from authoritative browse/import metadata;
- constant/write-restriction metadata if available;
- proposed EliteSCADA `TagDataType`;
- proposed canonical TAG path;
- support status and warning/reason;
- Data Source association.

### 6.3 Controller-scope versus program-scope browse

Rockwell publicly documents controller-scope Symbol Object enumeration in detail. Program-scoped TAG syntax is also documented for symbolic data access.

The exact reliable online enumeration mechanism for program-scoped TAGs must be verified against:

- the selected client stack;
- current controller firmware families;
- official Rockwell documentation available to the implementation team;
- real hardware.

Do not claim program browse completeness until those tests pass. A production v1 may combine online controller browse with L5X import for richer program hierarchy if that proves more reliable.

### 6.4 Filtering system/generated symbols

Rockwell’s manual distinguishes user-created symbols from system/other symbols. The importer should preserve evidence and filtering decisions rather than expose every implementation artifact as a process TAG.

Recommended browse UX:

- default to user-relevant externally accessible symbols;
- allow an advanced view for filtered/unsupported/system-like candidates;
- never silently drop unsupported structures or types;
- make exclusions reviewable in Preview.

---

## 7. Data types, arrays, strings and UDTs

### 7.1 Current EliteSCADA core type boundary

Current `Scada.Core.Tags.TagDataType` supports:

- `Boolean`;
- `Int16`;
- `Int32`;
- `Int64`;
- `Float`;
- `Double`;
- `String`;
- `DateTime`;
- `Enum`.

A future Logix binding must map controller types into this public model honestly. Unsigned widths, raw bit strings, complex structures and controller-native time/date families that do not map losslessly require explicit policy or future public-type evolution rather than hidden coercion.

### 7.2 First atomic mapping direction

| Logix family/type | Candidate EliteSCADA mapping | Direction |
| --- | --- | --- |
| BOOL | Boolean | direct when scalar semantics are known |
| SINT | Int16 | widen safely; preserve original protocol type in binding metadata |
| INT | Int16 | direct signed width |
| DINT | Int32 | direct signed width |
| LINT | Int64 | direct signed width after target validation |
| REAL | Float | direct IEEE single |
| LREAL | Double | target/controller/library support must be validated |
| STRING/custom STRING | String | codec includes declared capacity/length semantics |
| unsigned integer variants | no exact current unsigned public type | require checked widening/range policy or public-type evolution |
| date/time families | DateTime only where semantics/time zone/epoch map explicitly | never assume from raw integer |

The protocol binding should retain the **native Logix type** even when the public value type is wider. That prevents an Int16 EliteSCADA value sourced from SINT from being written back outside the PLC’s 8-bit range.

### 7.3 Arrays

Array bindings require:

- rank/dimensions from browse/import;
- explicit element type;
- element/member path semantics;
- bounded requested count;
- fragmentation support when response size requires it.

Initial product options:

1. engineer individual array elements as scalar EliteSCADA TAGs;
2. later add richer array/public-value support if the canonical model evolves.

The first implementation should prefer predictable scalar TAG semantics over introducing a private driver-only array value type.

### 7.4 BOOL packing

BOOL handling is not merely “read one byte.” Rockwell documentation describes packed BOOL behavior in structures/arrays, and current Logix designs may pack consecutive Boolean members according to data-layout rules.

Requirements:

- selected library must prove correct scalar BOOL, BOOL array and UDT BOOL-member behavior;
- bit/member writes must not corrupt neighboring packed values;
- whole-structure writes containing packed BOOLs should be postponed until template/layout validation is robust;
- tests must include transitions on adjacent packed Boolean members.

### 7.5 STRING

Rockwell Logix STRING is structure-based and custom string types can have different capacities. A future adapter must:

- know the actual string type/capacity;
- validate encoding and length;
- reject/explicitly truncate according to Engineering policy, never silently;
- test empty, maximum-length and non-ASCII scenarios according to the exact Logix type/library behavior.

### 7.6 UDTs and structures

Rockwell’s Template Object exists because a client cannot safely infer whole-structure layout from names alone.

Recommended v1 strategy:

- browse/import UDT definitions;
- allow members to become scalar EliteSCADA TAG candidates;
- support reading individual members first;
- permit optimized whole-structure reads only as an internal batching optimization when the adapter has validated template handle/member layout and can invalidate cache safely;
- do **not** expose an opaque UDT binary blob as an ordinary EliteSCADA scalar TAG.

Whole-structure writes are higher risk and should remain outside the first write slice unless hardware tests prove atomicity/layout/access behavior across supported families.

---

## 8. Write safety, External Access and constants

### 8.1 External Access is authoritative controller intent

Rockwell documents three External Access states for Logix TAGs used by external applications/HMIs:

- `Read/Write`;
- `Read Only`;
- `None`.

EliteSCADA must never reinterpret `Read Only` or `None` as merely an informational property.

### 8.2 Constant handling

Rockwell also exposes a `Constant` property for applicable tags/parameters. Constant data is not a valid normal process-write target.

A future candidate should become writable only when all relevant gates pass:

```text
EliteSCADA TAG not read-only
AND Data Source write policy enabled
AND controller metadata allows external write
AND not constant
AND not safety-restricted/unsupported
AND exact binding resolved
AND value type/range validated
AND current target identity matches policy
```

If browse cannot prove External Access/constant metadata, fail closed by default for auto-imported writeability. The engineer may later set an explicit safe write intent only if the runtime connection test proves controller acceptance and the product contract permits that override.

### 8.3 Alias tags

Rockwell documents alias External Access as following the base target. The importer should:

- preserve alias information where available;
- resolve/display the target during Preview;
- avoid creating two independent writable semantics that actually modify the same underlying value without warning;
- apply the effective access of the base target.

### 8.4 Safety data

Initial policy:

- do not claim safety TAG write support;
- standard non-safety TAGs on GuardLogix may be considered only after normal External Access and hardware validation;
- Safety-locked/protected behavior must not be bypassed;
- a safety-related browse candidate should be read-only/unsupported until a separately approved safety integration design exists.

The SCADA driver is not a safety programming tool.

### 8.5 Runtime write errors

Even an Engineering-valid write can fail later because the controller changes state, project, access policy, route or security policy.

Runtime should distinguish where evidence allows:

- target unreachable;
- session/route failure;
- object/symbol not found;
- access denied;
- type mismatch;
- value/range invalid;
- packet too large / fragmented operation failure;
- secure transport required/unsupported;
- controller busy/resource failure;
- timeout;
- generic sanitized protocol fault.

One failed TAG write must not automatically poison unrelated healthy TAGs on the same Data Source unless evidence shows the whole communication instance is unhealthy.

---

## 9. L5X/L5K Engineering import

### 9.1 Preferred path: L5X

Rockwell Logix Designer supports full project import/export using:

- `.L5X` XML;
- `.L5K` ASCII.

It also supports component-level L5X export/import for several project domains, including user-defined data types.

For EliteSCADA, **L5X should be the first file-based investigation target** because XML gives a structured parser boundary and can preserve hierarchy/type/access metadata more explicitly than hand-parsing an unrestricted project text representation.

### 9.2 L5K fallback

L5K can be valuable where users already have that export or where needed data is clearer in full-project ASCII. It should be a separate parser adapter that produces the same neutral import candidate model.

No downstream Engineering logic should care whether a candidate came from L5X, L5K or online browse.

### 9.3 Runtime must not depend on Studio 5000

The production service must be able to start and communicate with the PLC without:

- Studio 5000 installation;
- an open project;
- FactoryTalk design tooling;
- vendor automation APIs.

L5X/L5K ingestion is an optional Engineering workstation/server import activity.

### 9.4 Import safety

Rockwell notes that full project exports can contain current tag values and force masks. Therefore the EliteSCADA importer must **not** interpret a full project file as a command to reproduce controller runtime state.

Candidate parser rules:

- read definitions/types/hierarchy/access/descriptions relevant to TAG candidates;
- ignore force-enable/runtime-control semantics as actions;
- never download the file back to the controller;
- never change controller mode;
- never treat exported online values as automatic EliteSCADA write commands;
- optionally import initial/reference values only through an explicitly designed, non-process-state field if the future product needs it.

### 9.5 Canonical import flow

Required flow:

`L5X/L5K/online browse -> neutral candidates -> validate -> preview -> choose merge mode -> apply`

Preview should show:

- source file/project/controller;
- controller or program scope;
- symbol path;
- native data type/dimensions/UDT;
- External Access and Constant state when available;
- proposed EliteSCADA path/type;
- existing TAG match by stable ID/path/binding reconciliation;
- create/update/unchanged/conflict;
- unsupported type/layout/access reason;
- selected Data Source;
- read-only/write recommendation;
- warnings about target firmware/security/profile mismatch.

### 9.6 Stable identity across import refresh

L5X/L5K does not replace EliteSCADA stable TAG IDs.

Suggested reconciliation key inside one Data Source:

`scope + programName? + normalized symbolic path`

If this remains the same, Preview should preserve the existing EliteSCADA TAG ID. A type/access change becomes an update/conflict. A rename is not automatically knowable as the same logical TAG unless a future importer has trustworthy vendor identity metadata; the engineer must reconcile it explicitly rather than letting heuristics silently move process history.

---

## 10. Batching, fragmentation and controller resource limits

### 10.1 Do not poll one TAG per TCP connection

The driver should share a bounded Data Source communication context and group compatible work. But batching must be based on measured request/response size and controller capability, not an arbitrary “tags per packet” constant.

Relevant constraints include:

- encoded symbolic path length;
- native data type and element count;
- response size;
- route path overhead;
- fragmentation support;
- Multiple Service Packet overhead;
- controller/network module firmware;
- security overhead;
- connected/unconnected messaging mode;
- configured scan interval;
- number of active Data Sources.

### 10.2 Multiple Service Packet

Where supported by the chosen adapter/controller, Multiple Service Packet can combine independent explicit operations.

Rules:

- cap operation count and encoded byte size;
- preserve per-service status so one failed symbol does not invalidate unrelated successful results;
- do not mix writes in a way that changes operator ordering/command semantics;
- record batch-level and operation-level diagnostics where practical;
- shrink/adapt after size/resource errors instead of creating retry storms.

### 10.3 Fragmented services

Read/Write Tag Fragmented services are essential for large arrays/structures.

Requirements:

- enforce a maximum value size accepted by Engineering/runtime policy;
- track offset/progress and detect incomplete/out-of-order fragments;
- apply the final TAG value only after complete successful decode;
- a failed fragmented read produces a bad/current-quality outcome for that TAG without publishing a half-updated value;
- fragmented writes should be disabled for normal operators until the future write slice proves recovery semantics. Partial remote writes are much harder to reason about than partial reads.

### 10.4 Scan scheduler

Future scheduler direction:

- group TAGs by Data Source and scan class;
- use bounded per-Data-Source concurrency;
- favor deterministic intervals over unbounded queue growth;
- coalesce a new poll request if an older cycle is still running according to explicit policy;
- measure observed scan duration/data age;
- use backoff on transport/session failure;
- do not let one slow controller consume another controller’s scheduler budget;
- do not retry invalid-symbol/type errors at network-reconnect frequency.

### 10.5 Connection/session limits

Controller and bridge resources vary by hardware/firmware/topology. No single connection count can be guaranteed from protocol branding alone.

Therefore:

- initial default = one bounded session/context per Data Source;
- connected explicit connections, if added, have a small explicit cap;
- parallel connections require lab evidence and user-visible diagnostics;
- connection/resource rejection is a distinct diagnostic class;
- security-enabled performance must be measured separately.

---

## 11. CIP Security and Rockwell policy implications

### 11.1 Security model

ODVA documents CIP Security for EtherNet/IP using:

- TLS for TCP-based EtherNet/IP communication including encapsulation/UCMM/transport class 3;
- DTLS for UDP class 0/1;
- X.509 certificates or PSKs for device authentication;
- message integrity/authentication;
- optional confidentiality/encryption;
- additional user authentication/security profiles as the ecosystem evolves.

A future EliteSCADA Logix driver must not treat CIP Security as “just another port.” It changes trust, credentials, session establishment and operational failure modes.

### 11.2 Rockwell support is family/firmware dependent

Current Rockwell Policy Manager documentation lists CIP Security support including:

- ControlLogix 5580 firmware 32.x or later in the documented product line;
- CompactLogix 5380 firmware 34.x or later in the documented product line;
- supporting products such as 1756-EN4TR and FactoryTalk Linx according to their versions.

The future product must verify the actual target catalog/firmware/security policy rather than assume every controller bearing a family name has identical capabilities.

### 11.3 First-release compatibility policy

If the selected client library does **not** support the required CIP Security profile:

- connection test returns an explicit `SecureTransportRequiredOrUnsupported`-style result when evidence supports it;
- Engineering prevents activation if policy requires security the driver cannot provide;
- do not silently retry unsecured communication;
- documentation tells the engineer which combination is unsupported rather than advising them to weaken controller policy.

A future security-capable schema should support protected references for:

- client/device certificate;
- trust anchors;
- private-key reference;
- PSK reference if product policy permits;
- expected peer identity/policy;
- security mode/profile.

Resolved secrets/private keys must never appear in Engineering export or diagnostic payloads.

### 11.4 FactoryTalk Policy Manager is an ecosystem dependency, not necessarily a runtime dependency

Rockwell deployments may use FactoryTalk Policy Manager to configure device communication policies. EliteSCADA should coexist with that policy environment rather than requiring Policy Manager APIs for every runtime read.

Research requirement for the future lab:

- test unsecured allowed controller;
- test controller requiring CIP Security;
- test untrusted certificate/PSK failure;
- test expired/changed trust material;
- test policy revocation while running;
- verify clean diagnostic and reconnect behavior;
- never auto-change the controller’s security policy.

---

## 12. Candidate library comparison

No library is selected for production by this document. These are **laboratory candidates**.

### 12.1 libplctag core + libplctag.NET

Current evidence reviewed in August 2026:

- libplctag core has existed since 2012 and presents a stable C API;
- active core release `v2.7.0` was published 2026-06-30;
- core is dual licensed MPL-2.0 or LGPL-2+;
- native builds/assets cover Windows/Linux/macOS and multiple architectures;
- project explicitly supports Allen-Bradley Logix TAG access and Modbus;
- public project material describes primitive/bit/array support, request packing and current TAG-related facilities;
- special TAG facilities in current documentation provide Logix tag-list/UDT retrieval concepts (`@tags`, UDT metadata) useful for browsing;
- `libplctag.NET` adds typed .NET wrappers, exceptions, async/await and native resource cleanup;
- the stable .NET NuGet shown by NuGet is `1.5.2`, with `1.6.0-alpha.*` prereleases extending into 2025.

Strengths:

- strongest maturity signal among candidates focused specifically on Allen-Bradley TAG access;
- active 2026 core maintenance;
- real cross-platform packaging history;
- broad Logix community usage and protocol edge-case exposure;
- existing tag-list/UDT concepts reduce need to invent browse plumbing;
- managed wrapper fits EliteSCADA service code better than direct P/Invoke scattered through the driver.

Risks:

- native C runtime complicates RID packaging, update lifecycle and crash/ABI boundaries;
- core and .NET wrapper have different release cadence;
- production must pin and audit exact native binaries rather than dynamically loading “whatever is installed”;
- cancellation semantics, connection sharing, reconnect and shutdown behavior need load/failure tests;
- wrapper API/library attributes must not leak into canonical Engineering;
- dual core licensing plus wrapper MPL terms require distribution review.

**Research recommendation:** first laboratory candidate.

### 12.2 RustEtherNetIp

NuGet evidence reviewed in August 2026 shows a young actively moving package line, including `1.2.1` listed on 2026-08-23. Package descriptions target .NET 10 and CompactLogix/ControlLogix and claim support for routed access, batch operations, UDTs, polling/subscriptions and real-hardware validation.

Strengths:

- unusually close to EliteSCADA’s current .NET 10 target;
- explicit modern focus on Logix rather than only generic CIP objects;
- current route/batch/UDT feature claims align with the desired lab matrix;
- MIT license according to NuGet/project metadata.

Risks:

- very young 2026 project/release line;
- low package adoption/download evidence compared with mature alternatives;
- native Rust component still creates FFI/RID packaging considerations;
- documentation/release behavior is evolving quickly;
- claimed hardware coverage must be independently reproduced;
- async/cancellation implementation needs code review and failure testing.

**Research recommendation:** second laboratory candidate, especially useful as a cross-check against libplctag and as a possible future managed-facing/native implementation if maturity grows.

### 12.3 EEIP.NET / EEIP.NetStandard

Public project/package material documents:

- MIT origin for EEIP.NET;
- generic EtherNet/IP explicit and implicit messaging;
- CIP-defined object library;
- EEIP.NetStandard community port targeting .NET Standard 2.0, latest package evidence `1.0.2` from late 2025.

Strengths:

- managed C# implementation is attractive for packaging/debugging;
- useful reference for standard EtherNet/IP session/object/explicit behavior;
- suitable as a protocol comparison and generic-device test client.

Risks for this specific Logix driver:

- reviewed public documentation emphasizes generic CIP objects and I/O scanner functionality;
- research did not find comparable evidence for the full Rockwell Logix Symbol Object/Template Object/tag-list/fragmented read-write/UDT workflow needed here;
- original EEIP package is old; the NetStandard port is community-maintained and must be assessed separately;
- being able to send a CIP explicit request is not equivalent to being a production-ready Logix symbolic SCADA stack.

**Research recommendation:** generic CIP reference/comparator, not first Logix runtime candidate unless a future code/lab review proves the missing Logix layer.

### 12.4 Custom EliteSCADA protocol implementation

Only consider a custom implementation if candidate libraries fail non-negotiable requirements for correctness, licensing, security, cancellation, diagnostics, platform support or maintainability.

A custom implementation has substantial obligations:

- formal ODVA specification access/licensing review;
- EtherNet/IP encapsulation/session lifecycle;
- CIP path encoding/routing;
- unconnected/connected explicit semantics;
- Logix vendor-specific TAG services;
- Symbol/Template browse;
- UDT/BOOL/string codecs;
- fragmentation/multi-service;
- error/status interpretation;
- CIP Security if required;
- extensive hardware conformance/interoperability tests.

Writing bytes is easy. Owning an industrial protocol implementation for years is the expensive part.

### 12.5 Laboratory scoring gate

Score each viable candidate on the same matrix:

| Area | Required evidence |
| --- | --- |
| .NET 10 | clean build/run and cancellation/shutdown on target OS |
| Direct CompactLogix | repeated read/write/reconnect on physical controller |
| Routed ControlLogix | bridge/backplane/slot path through physical chassis |
| Controller tags | scalar, array, nested member access |
| Program tags | read/write with program-scoped symbolic path |
| BOOL | scalar, array, packed UDT members |
| Integers/floats | signed widths, REAL/LREAL support profile |
| STRING | standard/custom capacity, boundaries |
| UDT | browse/template/member reads and change invalidation |
| Browse | complete paged list, large project, refresh after edits |
| Batching | per-item status and size-limit behavior |
| Fragmentation | large array/structure read; write only if approved |
| Access | Read/Write, Read Only, None, Constant |
| Failure | unplug/replug, CPU restart, bridge restart, symbol removed, route invalid |
| Multi-source | two independent controllers, one failing without contaminating the other |
| Security | CIP Security capability or explicit unsupported classification |
| Packaging | deterministic native/runtime deployment and license inventory |

Production dependency selection happens **after** this gate.

---

## 13. Diagnostics mapping to EliteSCADA common contract

The future driver should expose the existing common communication diagnostic snapshot rather than create an Allen-Bradley-only health system.

### 13.1 Common fields

Map where meaningful:

- Data Source key/name;
- driver type, e.g. future stable key such as `rockwell.logix.eip`;
- runtime instance ID;
- sanitized endpoint and route;
- state: Healthy / Degraded / Reconnecting / Faulted as supported by common contract;
- state-change timestamp;
- last successful communication;
- last failed communication;
- requests/successes/failures/timeouts;
- consecutive failures;
- connect/disconnect/reconnect counts;
- read/write counts;
- recent failure rate;
- last/average latency;
- configured scan interval and observed scan duration/data age;
- associated TAG count;
- Good/BadCommunication/other TAG quality counts;
- sanitized last error.

### 13.2 Protocol-specific diagnostic details

Useful extra detail may include:

- controller family/profile;
- observed CIP identity/product/revision when non-secret;
- effective route path;
- explicit messaging mode;
- active session state;
- number of pending/in-flight operations;
- last negotiated/effective request-size limit if exposed reliably;
- number of Multiple Service Packet batches;
- fragmented read count;
- browse cache generation/refresh timestamp;
- project/type-layout change detected flag;
- CIP Security mode/capability without exposing credentials.

### 13.3 Failure classification proposal

Protocol-specific sanitized categories can enrich `lastError`/details:

- `TransportUnavailable`;
- `SessionRegistrationFailed`;
- `RouteRejected`;
- `TargetIdentityMismatch`;
- `ControllerResourceUnavailable`;
- `SymbolNotFound`;
- `AccessDenied`;
- `ConstantOrReadOnly`;
- `TypeMismatch`;
- `StructureLayoutChanged`;
- `PacketTooLarge`;
- `FragmentationFailed`;
- `Timeout`;
- `SecureTransportRequiredOrUnsupported`;
- `CertificateOrTrustFailure`;
- `ProtocolFault`.

Do not expose raw credentials, certificate private-key paths, PSKs or unsanitized packet contents.

### 13.4 Driver state versus TAG quality

Examples:

- one removed TAG returning symbol-not-found: that TAG becomes bad configuration/device as appropriate; other successfully read TAGs can remain Good and the Data Source may be Degraded rather than Faulted;
- TCP/session loss: current communication TAGs age and transition to BadCommunication according to the runtime policy;
- one write rejected by External Access: source reads can remain healthy; the write failure is diagnosed independently;
- one controller failure must not change another Data Source’s counters/quality.

This follows the existing EliteSCADA principle that TAG quality is authoritative per point and driver health is a summary.

---

## 14. Multi-controller and Gateway behavior

Every configured Allen-Bradley Data Source owns its own:

- endpoint/route;
- communication state;
- scheduler;
- bounded request queue;
- reconnect/backoff state;
- diagnostic counters;
- browse/type cache;
- associated TAG set.

Required acceptance case:

- `CLX_LINE_A` healthy via routed chassis;
- `CPLX_SKID_B` healthy via embedded Ethernet;
- disconnect `CLX_LINE_A`;
- `CPLX_SKID_B` remains healthy with uninterrupted counters/data;
- reconnect A and verify independent recovery.

The TAG Gateway requires no Allen-Bradley-specific adapter. A future route such as:

`ControlLogix TAG -> Gateway -> Modbus TAG`

or

`Server Memory -> Gateway -> CompactLogix TAG`

must use normal TAG events and destination-owning provider writes. The Allen-Bradley driver only needs correct read/write/source-provider behavior. Gateway route diagnostics remain separate from CIP communication diagnostics.

---

## 15. Test strategy

### 15.1 Pure software/unit tests

Can run in normal CI without Rockwell software:

- symbolic path parser/encoder;
- route port/link parser/encoder;
- program/controller scope canonicalization;
- native Logix type -> EliteSCADA type mapping;
- checked write range conversion;
- STRING codecs;
- BOOL scalar/array/member packing helpers;
- UDT template/layout parser using recorded non-secret fixtures;
- Multiple Service Packet per-item response parsing;
- fragmented response assembly and interruption;
- CIP status/error classification;
- retry/backoff/scheduler behavior using a fake transport;
- browse pagination and cache invalidation;
- import reconciliation rules;
- L5X/L5K parser fixtures based on legally redistributable synthetic exports;
- multi-Data-Source metric isolation with fake protocol clients.

### 15.2 Protocol integration fixtures

Candidate software tools can be used only as bounded protocol fixtures and must not be labeled hardware acceptance.

Possible uses:

- libplctag’s test/server tooling where suitable;
- a purpose-built test double speaking only the request subset needed for deterministic negative/error tests;
- generic CIP test stacks for encapsulation/object cases;
- recorded/synthetic response fixtures for rare errors.

Do not build CI around proprietary packet captures or copyrighted controller projects that cannot be redistributed.

### 15.3 FactoryTalk Logix Echo

Rockwell’s current Logix Echo documentation supports emulated controller communication with HMI/EOI over Ethernet CIP Class 3 while intentionally blocking outgoing control of physical devices.

That makes Echo a strong **licensed integration/laboratory candidate** for:

- repeated explicit TAG read/write;
- project/type changes;
- HMI-style class-3 communication;
- large tag sets;
- some browse/import validation;
- application restart/reconnect scenarios.

Before making it part of automated CI, verify:

- license terms and unattended/automation rights;
- supported controller emulation families/firmware;
- deterministic installation in the CI environment;
- whether network/routing behavior matches the intended test.

Echo does not replace real hardware for bridge/chassis paths, physical connection resources, cable/network behavior or security appliance/controller interactions.

### 15.4 Real hardware acceptance matrix

Minimum pre-production lab:

**CompactLogix**

- current 5380-class controller with embedded EtherNet/IP;
- project containing controller and program tags;
- Read/Write, Read Only, None and Constant examples;
- scalar types, arrays, STRING, nested UDT, BOOL packing;
- security-enabled test if supported by selected firmware.

**ControlLogix**

- current 5580-class controller;
- at least one 1756 EtherNet/IP communication path where route to CPU slot is exercised;
- non-zero CPU/module slot variants if available;
- remote/bridged route if lab hardware permits.

Acceptance scenarios:

1. connect/test identity and route;
2. browse a large TAG set across multiple pages;
3. read controller-scope scalar tags;
4. read program-scope tags;
5. array/member access;
6. UDT member discovery/read;
7. STRING boundaries;
8. approved write, verify value;
9. write blocked for Read Only/None/Constant;
10. delete/rename TAG while client cache exists, verify no stale rebinding;
11. modify UDT, verify template cache invalidation/conflict;
12. cable unplug/replug;
13. controller power cycle;
14. bridge/module interruption in ControlLogix path;
15. invalid slot/route;
16. wrong target identity at same IP simulation/lab substitution;
17. concurrent scans + operator writes;
18. large reads requiring fragmentation;
19. large mixed batches with one bad symbol;
20. two controllers simultaneously with one failing;
21. Gateway write to/from Allen-Bradley TAG through normal EliteSCADA runtime;
22. CIP Security allowed/trusted case if supported;
23. CIP Security policy/certificate rejection;
24. long-duration soak and repeated reconnect;
25. activation/revision replacement stops old session before/while candidate becomes authoritative according to existing transactional runtime rules.

### 15.5 What cannot credibly be CI-only

CI cannot prove:

- actual ControlLogix bridge/backplane route behavior across supported modules;
- controller/network-module connection/resource ceilings;
- firmware-specific access/security behavior;
- real CIP Security certificate/policy interoperability for every target;
- cable/link interruption timing;
- CPU reboot and module restart behavior;
- plant-network latency/jitter/load effects;
- every UDT/layout nuance across Logix firmware generations;
- vendor safety/security restrictions;
- performance impact on a loaded production-style controller.

These require controlled hardware acceptance and versioned lab evidence.

---

## 16. Proposed future production slices

These are **planning inputs only**, not authorization to implement now.

### Slice A - Public Engineering driver schema

Coordinator-owned/public-contract work:

- stable Allen-Bradley driver key;
- endpoint/controller profile;
- ordered CIP route;
- timeouts/reconnect/scan policies;
- protocol-specific TAG binding with scope/program/symbol/native type;
- protected security references;
- schema migration/import-export validation.

### Slice B - Engineering candidate discovery/import

- online connection test/identity;
- bounded Symbol/Template browse;
- L5X parser, optional L5K adapter;
- neutral candidate model;
- support/access/type classification;
- Preview/Apply integration.

### Slice C - Runtime client adapter

- encapsulation/session lifecycle through selected library;
- explicit read path;
- cancellation/shutdown;
- route handling;
- value/type codecs;
- deterministic error mapping.

### Slice D - Polling and quality

- scan classes;
- bounded batching;
- Multiple Service Packet where proven;
- fragmentation;
- data age;
- TAG quality publication;
- reconnect/backoff.

### Slice E - Safe writes

- normal scalar/member writes;
- type/range/access/constant gates;
- route/identity verification;
- write diagnostics/audit through existing EliteSCADA boundaries;
- no generic raw CIP command API.

### Slice F - Common diagnostics

- standard communication snapshot;
- protocol details;
- multi-controller isolation;
- Engineering/runtime overview integration through existing protected API.

### Slice G - CIP Security

Only after a supported stack/profile and lab environment are selected:

- certificate/PSK reference model;
- trusted peer validation;
- secure session lifecycle;
- security diagnostics;
- no automatic downgrade.

### Slice H - Hardening / module packaging

- public Driver SDK/module compatibility;
- RID/native packaging if needed;
- license inventory/SBOM;
- version compatibility matrix;
- physical hardware acceptance.

---

## 17. INTEGRATION REQUIRED before production implementation

The future coordinator/production tasks must resolve these items explicitly.

1. **ODVA specification/licensing**
   - confirm rights/access required for production use of EtherNet/IP/CIP specifications and conformance marks;
   - if implementing protocol details directly, obtain authoritative specification rather than relying only on examples/public manuals.

2. **Library selection**
   - run the common hardware lab matrix for libplctag.NET, RustEtherNetIp and any serious managed alternative;
   - choose by evidence, not package popularity or API aesthetics.

3. **Public Engineering contract**
   - add a versioned protocol-owned Data Source/TAG binding schema without embedding library handles;
   - preserve import/export/package/revision lifecycle.

4. **Native packaging policy**
   - if using libplctag or Rust native core, define supported Windows/Linux RID matrix, exact binary provenance, update process, hash/SBOM and failure boundary.

5. **Security material model**
   - provide certificate/trust/private-key/PSK references through protected secret infrastructure before claiming CIP Security support.

6. **Controller identity policy**
   - decide optional/required expected identity matching and write fail-closed behavior after equipment replacement.

7. **Type gaps**
   - decide how unsigned Logix values and richer time/date/native types map to the current EliteSCADA type model;
   - no private frontend/driver type should silently become canonical truth.

8. **Program-scope browse proof**
   - validate complete online enumeration with the selected stack/firmware or document L5X as the authoritative richer import path for the first release.

9. **Write policy**
   - integrate External Access/Constant evidence into Engineering validation and active-runtime writeability;
   - default uncertain candidates to read-only.

10. **Hardware lab**
    - acquire/assign representative CompactLogix and ControlLogix hardware, firmware versions and bridge topology;
    - document acceptance artifacts and reproducible project fixtures.

11. **CIP Security lab**
    - provision supported controller/module/security policy and certificates/PSKs without weakening plant security;
    - verify trusted, rejected, rotated and revoked scenarios.

12. **Central integration**
    - DriverHost compiler/runtime composition, DI, protected diagnostics/API and Engineering UI remain coordinator-owned production work.

---

## 18. Production acceptance checklist

A future Allen-Bradley Logix driver is not production-ready until all applicable items are green:

- [ ] ControlLogix + CompactLogix compatibility matrix names actual tested catalog/firmware ranges.
- [ ] Driver uses explicit messaging; implicit I/O remains explicitly out of scope unless separately engineered.
- [ ] Direct and routed chassis paths are tested on hardware.
- [ ] Controller and program-scoped symbols are supported according to declared profile.
- [ ] Engineering persists stable symbolic binding, not browse/session instance IDs.
- [ ] Browse pagination and refresh after project edits are deterministic.
- [ ] UDT/template cache invalidates safely.
- [ ] BOOL packing/arrays and STRING capacity are tested.
- [ ] Supported atomic types map without unsafe coercion.
- [ ] Read Only/None/Constant writes fail closed before normal write dispatch where metadata is available.
- [ ] Safety programming/writes remain excluded.
- [ ] Multi-service batching preserves per-item results.
- [ ] Fragmented read failure cannot publish partial values.
- [ ] Reconnect/backoff is bounded and does not block other Data Sources.
- [ ] One failing controller does not contaminate another controller’s TAG quality/counters.
- [ ] Common Data Source diagnostics are populated and protected.
- [ ] Gateway participation occurs only through normal TAG/write boundaries.
- [ ] L5X/L5K import uses canonical Preview/Apply and never downloads controller projects.
- [ ] Studio 5000 is not a runtime dependency.
- [ ] CIP Security capability/limitations are explicit; no silent insecure fallback.
- [ ] Selected library/native artifacts have license/SBOM/provenance review.
- [ ] Software CI, Logix Echo where licensed/useful, and real-hardware acceptance are all documented.
- [ ] Exact-head full EliteSCADA CI is green before merge of production slices.

---

## 19. Evidence and source notes

Research intentionally prioritizes official ODVA/Rockwell sources for protocol/product semantics and primary package/project sources for library claims. The public Rockwell programming manual is architecture evidence, but the authoritative CIP/EtherNet/IP specification remains controlled by ODVA.

### ODVA

- EtherNet/IP Technology Overview, explicit/unconnected/connected messaging and implicit I/O:  
  https://www.odva.org/publication_download/ethernet-ip-technology-overview/
- CIP Security overview, TLS/DTLS, X.509/PSK and security profiles:  
  https://www.odva.org/technology-standards/distinct-cip-services/cip-security/
- CIP Security at a Glance:  
  https://www.odva.org/publication_download/cip-security-at-a-glance-pub-319/
- EtherNet/IP Developer / Quick Start material:  
  https://www.odva.org/

### Rockwell Automation

- Logix 5000 Controllers Data Access, Publication `1756-PM020I-EN-P`, September 2025. Public manual documents Logix tag services, symbolic/instance addressing, Symbol Object, Template Object and paged enumeration:  
  https://literature.rockwellautomation.com/idc/groups/literature/documents/pm/1756-pm020_-en-p.pdf
- Logix 5000 Controllers I/O and Tag Data, Publication `1756-PM004`, External Access behavior:  
  https://literature.rockwellautomation.com/idc/groups/literature/documents/pm/1756-pm004_-en-p.pdf
- Studio 5000 Logix Designer online help, External Access:  
  https://www.rockwellautomation.com/en-us/docs/studio-5000-logix-designer/38-02/contents-ditamap/studio-5000-logix-designer/tag-editor-and-data-monitor/about-the-tag-editor/about-external-access-to-public-and-private-data.html
- Studio 5000 full project L5X/L5K import/export:  
  https://www.rockwellautomation.com/en-us/docs/studio-5000-logix-designer/38-01/contents-ditamap/studio-5000-logix-designer/import-and-export/import-export-a-full-project.html
- Studio 5000 import/export formats:  
  https://www.rockwellautomation.com/en-us/docs/studio-5000-logix-designer/38-01/contents-ditamap/studio-5000-logix-designer/import-and-export.html
- ControlLogix EtherNet/IP Network Devices User Manual, Publication `1756-UM004`, route examples and port/backplane semantics:  
  https://literature.rockwellautomation.com/idc/groups/literature/documents/um/1756-um004_-en-p.pdf
- CIP Security with Rockwell Automation Products Application Technique, Publication `SECURE-AT001`:  
  https://literature.rockwellautomation.com/idc/groups/literature/documents/at/secure-at001_-en-p.pdf
- FactoryTalk Policy Manager documentation/product compatibility information:  
  https://www.rockwellautomation.com/en-gb/docs/factorytalk-policy-manager/6-50/release-notes-ditamap/concepts-warehouse.html
- FactoryTalk Logix Echo Getting Results Guide, Publication `9310-GR001`:  
  https://literature.rockwellautomation.com/idc/groups/literature/documents/gr/9310-gr001_-en-p.pdf
- Micro800 symbolic read/write syntax, used here only to justify separate-family treatment:  
  https://www.rockwellautomation.com/en-us/docs/factorytalk-design-workbench/1-01-00/ftdw-help-ditamap/micro800-controller/micro800-instruction-set/messaging-instructions/msg_cipsymbolic/cipsymboliccfg-data-type/symbolic-read-write-syntax.html

### Library candidates

- libplctag core project, features/license:  
  https://github.com/libplctag/libplctag
- libplctag release history, including `v2.7.0` in June 2026:  
  https://github.com/libplctag/libplctag/releases
- libplctag API/wiki:  
  https://github.com/libplctag/libplctag/wiki/API
- libplctag.NET NuGet:  
  https://www.nuget.org/packages/libplctag/
- libplctag.NET source:  
  https://github.com/libplctag/libplctag.NET
- EEIP.NET source:  
  https://github.com/rossmann-engineering/EEIP.NET
- EEIP.NetStandard NuGet:  
  https://www.nuget.org/packages/EEIP.NetStandard/
- RustEtherNetIp NuGet, current 2026 package line:  
  https://www.nuget.org/packages/RustEtherNetIp/

### Evidence freshness / caution

- Rockwell controller capabilities vary by catalog number, firmware and network module. The future compatibility matrix must cite the exact tested versions rather than extrapolate from family marketing names.
- CIP Security and FactoryTalk policy capabilities evolve. Recheck current ODVA/Rockwell documentation when production work starts.
- Library releases can move quickly. Pin the exact commit/package/native artifact used in the future laboratory scorecard.
- A package claim of “ControlLogix support” is not acceptance evidence. Reproduce the mandatory hardware matrix.

---

## 20. Final research disposition

**RESEARCH IN PR / PRODUCTION NOT IMPLEMENTED.**

The recommended future direction is:

`ControlLogix/CompactLogix -> EtherNet/IP explicit messaging -> Rockwell Logix symbolic TAG services -> EliteSCADA Data Source/TAG boundary`

with:

- symbolic stable Engineering identity;
- bounded per-Data-Source scheduling;
- online browse plus L5X/L5K Engineering import;
- UDT/array/BOOL/string-aware codecs;
- fail-closed External Access/write semantics;
- common diagnostics and TAG quality;
- normal protocol-independent Gateway participation;
- explicit CIP Security compatibility;
- software + Logix Echo + real-hardware acceptance;
- laboratory library selection before any dependency is approved.

Nothing in this research authorizes production Allen-Bradley source code, package references, Data Source registration or controller engineering operations before the coordinator reopens the external-protocol implementation gate.