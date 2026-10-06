# KNX -> DALI Integration Architecture — Checkpoint 3

Status: CHECKPOINT_3 / RESEARCH_ONLY / DOCS_ONLY  
Issue: #539 — HOME-RESEARCH-01  
Contract: C-HOME-KNX-DALI-RESEARCH-01  
Order: HOME-KNX-DALI-TIER-A-RESEARCH-01  
Research branch: research/home-knx-dali-execution  
Access date: 2026-10-06

## 1. Decision summary

The combined architecture is:

```
EliteSCADA
  -> canonical Communication Driver
  -> canonical TAGs / Runtime.WriteAsync
  -> gateway-facing protocol
  -> professional DALI gateway
  -> DALI bus
```

There are three approved architectural forms for a future implementation:

### A. Preferred first DALI release

```
EliteSCADA modbus.tcp
  -> Intesis INMBSDAL0640200 / INMBSDAL1280200
  -> DALI-2 bus
```

### B. Fallback rich-building path

```
EliteSCADA bacnet.ip
  -> LOYTEC LDALI-ME201-U / ME202-U / ME204-U
  -> DALI-2 bus
```

### C. Strategic KNX/DALI path

```
EliteSCADA future KNX/IP Data Source
  -> KNX/IP Secure tunneling or selected KNXnet/IP mode
  -> KNX TP
  -> Theben DALI-Gateway P64 KNX
  -> DALI-2 bus
```

No form requires a fake `dali.runtime` authority.

DALI is a downstream fieldbus and semantic domain. The provider that owns process values remains Modbus, BACnet or KNX.

## 2. Canonical EliteSCADA ownership

Live release-base evidence already provides:

- `modbus.tcp` with Read, Write and Diagnostics;
- `bacnet.ip` with Read, Write, Subscribe and Diagnostics;
- canonical `CommunicationTagBinding`;
- stable `DataSourceId`;
- canonical Runtime write routing through `Runtime.WriteAsync`;
- common diagnostics and PointRead/ConnectionTest convergence from #500.

The future KNX driver must fit the same spine.

### Process-value ownership

```
Data Source
  owns transport/session
TAG
  owns canonical process identity
CommunicationBinding
  owns protocol address + protocol semantics
Equipment
  groups physical/logical application objects
Capability
  projects user-facing meaning over TAGs / Commands
Runtime.WriteAsync(TAG)
  remains normal write authority
```

DALI gateway metadata must not create another value cache with competing authority.

## 3. KNX Data Source model

Recommended unit:

`one Data Source per KNX/IP interface/router access context`

A Data Source should own:

- endpoint/interface identity;
- tunneling versus routing mode;
- secure-mode requirement;
- local network interface selection where needed;
- reconnect/session policy;
- protected key material references;
- protocol diagnostics;
- many group-address TAG bindings.

Do not create one Data Source per KNX group address or per DALI luminaire.

### Binding identity

A future KNX binding should preserve at least:

- exact group address;
- exact DPT;
- read/write capability;
- acquisition/readback expectations;
- KNX Data Secure expectation;
- optional gateway/equipment semantic metadata;
- stable DataSourceId;
- stable TAG identity.

Do not reduce a DPT to only a primitive CLR type.

## 4. KNX/DALI gateway projection

The selected Theben P64 is a KNX/DALI application controller with:

- 64 DALI devices;
- 16 groups or individual control;
- DALI-2 multi-master;
- DALI-2 sensor support;
- DALI-2 push-button/generic input support on current firmware/application;
- DT8 colour/colour-temperature functions;
- emergency-light support;
- current energy-reporting features;
- KNX Data Secure;
- ETS/DCA/web commissioning.

The exact KNX communication-object set is project/application dependent.

Therefore the future EliteSCADA implementation must obtain the exact group-address/DPT mapping from:

1. explicit Engineering bindings; or
2. a future validated ETS/project metadata import.

It must not guess object meaning from gateway model alone.

## 5. DALI semantic projection over KNX

Conceptual mapping:

| DALI semantic | KNX-facing representation | EliteSCADA projection |
| --- | --- | --- |
| luminaire on/off | gateway KNX group object | Boolean TAG + Switch capability |
| actual dim level | KNX group object, usually scaling semantic | numeric TAG + Level capability |
| dim command | KNX relative/absolute dim object | writable TAG/Command through canonical runtime |
| DALI group control | KNX group objects configured for DALI group | Equipment/group capability metadata |
| DALI scene recall | scene-related KNX object | bounded Scene capability/command |
| DT8 colour temperature | gateway KNX colour/Tc object | numeric colour-temperature TAG/Capability only after exact DPT mapping |
| DT8 RGB/xy | gateway KNX colour object(s) | structured/paired semantic projection; no guessed primitive mapping |
| lamp/gear failure | fault/status KNX object | diagnostic/status TAG + Equipment fault semantic |
| occupancy | DALI-2 input -> KNX object | Boolean/event semantic |
| illuminance | DALI-2 sensor -> KNX DPT | numeric TAG + lux unit |
| emergency status | gateway emergency object(s) | status/maintenance TAGs |
| energy data | gateway energy objects | power/energy TAGs with exact DPT/unit |

The future device importer must not fabricate DALI device identities if the gateway exposes only group-level semantics.

## 6. Physical device and Equipment identity

### When Equipment is justified

Create Equipment when the implementation has trustworthy identity from:

- ETS project metadata;
- gateway-supported DALI address/GTIN metadata;
- explicit user mapping;
- a protocol object that unambiguously identifies the physical/logical device.

### When Equipment is not justified

Do not create a physical “DALI luminaire” Equipment merely because one KNX group address is named “Light 1.”

A group address may represent:

- one luminaire;
- several luminaires;
- one group;
- a scene;
- a synthetic logic object.

Group-address semantics and physical topology are distinct.

## 7. Runtime operations

Allowed normal Runtime behavior:

- receive KNX group telegrams;
- decode exact approved DPT;
- update canonical TAG value/quality/timestamp;
- read a point where supported;
- perform normal process write through `Runtime.WriteAsync(TAG)`;
- invoke a bounded canonical scene/action when its mapping is explicitly declared;
- surface gateway/lamp/gear/emergency status;
- surface DALI sensor state projected by the gateway.

No frontend or script may call a KNX/DALI gateway method directly.

Client Visual Script, Server Script, TAG Gateway and ordinary operator writes must remain consumers of the canonical TAG/Command layer.

## 8. Commissioning and administrative operations

The following are not ordinary HMI process operations:

- KNX physical-address programming;
- ETS application download;
- KNX device enrollment;
- KNX FDSK/security onboarding;
- authentication-code or commissioning-password changes;
- keyring generation/import/rotation;
- DALI randomization;
- DALI short-address assignment;
- DALI device scan that changes addressing;
- DALI group membership programming;
- scene-value commissioning;
- ballast/driver factory reset;
- gateway factory reset;
- firmware update;
- gateway network/security configuration.

These remain:

- ETS / DCA;
- vendor commissioning software;
- gateway-local administration;
- or a future deliberately privileged commissioning surface.

If EliteSCADA later automates any of them, Main must authorize a separate mutation contract with explicit authorization/audit/rollback.

## 9. Scene semantics

Scene activation can be a normal Runtime action if:

- the scene already exists in the gateway/DALI project;
- the action only recalls the scene;
- the binding is explicit and bounded.

Scene programming is commissioning if it:

- stores new DALI scene values;
- changes group membership;
- changes scene membership;
- rewrites gateway configuration.

Do not collapse “recall scene” and “program scene” into one writable TAG.

## 10. Event semantics

DALI-2 push-buttons and some KNX input objects are event-oriented.

Do not model a button press as permanent Boolean state if the gateway exposes a transient event.

The prior #475 research finding remains applicable:

`DRIVER-TRANSIENT-EVENT-01`

Future KNX implementation must use the canonical event/action semantics accepted by Main when available, rather than manufacturing sticky process state.

## 11. Security architecture

### 11.1 KNX IP Secure

Preferred KNX first transport remains secure tunneling where the target interface/router supports it.

Requirements:

- explicit secure-mode configuration;
- fail closed on authentication/key failure;
- no silent plain-tunnel fallback;
- sequence/replay validation;
- sanitized diagnostics;
- bounded reconnect.

Routing remains an advanced mode with explicit multicast deployment validation.

### 11.2 KNX Data Secure

If a group object is configured for KNX Data Secure:

- preserve that requirement in the binding/import metadata;
- use the matching group key;
- reject incompatible/missing security state;
- never retry unsecured for convenience.

IP Secure and Data Secure are separate layers.

### 11.3 DALI-side security

DALI itself is not treated as an IP security boundary.

Security is primarily enforced at:

- KNX/IP;
- Modbus/BACnet network segmentation;
- gateway administration;
- EliteSCADA authorization;
- protected credential/key storage.

Do not expose gateway admin interfaces to untrusted networks merely because process traffic is otherwise isolated.

## 12. Protected material — live architecture reconciliation

The release base has two relevant authorities:

### Existing Communication Driver boundary

`ICommunicationDriverProtectedMaterialResolver`

It is scoped by:

- ProjectKey;
- DataSourceKey;
- DriverType;
- Purpose;
- Reference.

It returns a short-lived protected-material lease and has no enumeration API.

### Generic Protected Material Authority

`IProtectedMaterialAuthority`

The generic host authority is present at the release base and is the canonical foundation for new server-side resources. It provides opaque `protected-material-v1:...` references and exact scope ownership.

The live foundation documentation intentionally did not migrate existing Communication Driver secret references.

### RESEARCH_FINDING / MAIN_DECISION_REQUIRED — KNX-PROTECTED-MATERIAL-BACKING-01

Problem:

The #539 order requires KNX keyring/tunnel material to converge on Protected Material Authority, while current Communication Drivers still resolve secrets through the Driver-specific protected-material contract.

Affected architecture:

- future KNX Communication Driver;
- Protected Material Authority;
- Driver protected-material resolver;
- Engineering secret references;
- package portability.

Options:

A. Keep KNX exclusively on the current Driver-specific environment resolver.  
B. Bypass Driver resolver and let KNX call the generic authority directly.  
C. Preserve `ICommunicationDriverProtectedMaterialResolver` as the Driver-facing contract, but back/provision future KNX references through an adapter over `IProtectedMaterialAuthority`.

Recommendation:

`OPTION C`

Rationale:

- preserves the canonical Driver boundary;
- fulfills the host-owned authority direction;
- avoids teaching one Driver to bypass DriverHost security composition;
- permits scoped purposes such as:
  - `knx.keyring`;
  - `knx.tunnel-password`;
  - `knx.authentication-code`;
  - `knx.backbone-key`;
  - `knx.group-keys`;
- keeps raw material out of Engineering/package/logs.

Main must authorize this adapter/convergence before the KNX DEV implements storage.

Researcher must not implement it.

## 13. ETS/project metadata import

ETS6 exports projects as `.knxproj` and applies licensing/signature rules to export.

KNX also documents its XML project scheme for external tools.

A future import can be useful to obtain:

- group-address hierarchy;
- group-address names/descriptions;
- DPT identity;
- linked group objects;
- physical device/product context;
- topology/location hints;
- secure metadata references/fingerprints.

### Import boundary

Import must be:

- read-only with respect to the live KNX installation;
- explicit user action;
- validated as untrusted input;
- version-aware;
- non-destructive;
- separate from bus commissioning.

Do not require ETS to be installed on the Runtime host merely to execute normal KNX process traffic.

### Secure project/keyring material

Project metadata and security material must be separated.

A `.knxproj` may be useful Engineering metadata.

A `.knxkeys`/keyring or security-bearing export is protected input.

Raw security material must not be retained in ordinary Engineering JSON.

## 14. Legal gate on ETS import

KNX publicly documents:

- project export licensing constraints;
- project-file signatures;
- a project XML scheme intended for external-tool import use cases;
- keyring use outside ETS/Falcon.

This is evidence that external tooling is technically contemplated.

It is not by itself a legal approval for every commercial redistribution/workflow.

Therefore:

`LEGAL_REVIEW_REQUIRED`

before productizing automatic ETS project import, especially around:

- bundled product/application data;
- DCA binaries;
- copyrighted manufacturer catalog content;
- secure key material;
- redistribution or re-export.

A first KNX release can avoid this blocker by supporting explicit manual group-address/DPT binding plus optional user-supplied metadata only after approval.

## 15. Deployment architecture

### 15.1 Tunneling default

Recommended first deployment:

```
EliteSCADA host
  -> unicast LAN
  -> KNX IP Interface 732 secure
  -> KNX TP
```

Benefits:

- bounded endpoint;
- no dependency on LAN multicast forwarding for normal process traffic;
- clear firewall policy;
- clear session diagnostics.

The Weinzierl 732 supports secure tunneling and up to eight simultaneous tunneling connections.

### 15.2 Routing validation path

Lab-only/advanced validation:

```
EliteSCADA host
  -> multicast-capable LAN/VLAN
  -> KNX IP Router 752 secure
  -> KNX TP
```

The 752 supports secure routing plus secure tunneling and up to eight simultaneous tunneling connections.

Routing release gate:

- multicast join;
- IGMP behavior;
- VLAN/firewall policy;
- multiple NIC/interface selection;
- container mode;
- packet-loss/duplicate behavior;
- secure routing key behavior.

### 15.3 Docker/container

For first product support:

- tunneling should work with ordinary outbound/inbound unicast networking;
- routing must not be promised through an arbitrary default bridge network;
- routing may require host networking or deliberate multicast-capable container networking;
- document exact supported topology after L4 proof.

### 15.4 Windows/Linux

The future KNX DEV must prove the exact chosen SDK/package on:

- Windows service deployment;
- Linux service deployment;
- selected container base image if container support is claimed.

No architecture should depend on interactive user profiles or desktop secret stores.

## 16. Network trust

### KNX/IP

- use KNX IP Secure when available;
- restrict LAN/VLAN reachability;
- never expose UDP 3671 directly to the public Internet;
- use VPN/private routed access for remote sites.

### Modbus/DALI

- local OT network/VLAN;
- firewall source restrictions;
- no Internet exposure;
- treat Modbus TCP as unauthenticated process protocol unless an external secure transport is deliberately provided.

### BACnet/DALI

Current EliteSCADA driver is `bacnet.ip`, not BACnet/SC.

Use:

- local/private network;
- BBMD/foreign-device routing only when deliberately designed;
- no claim of BACnet/SC security until that driver family supports it.

## 17. Diagnostics convergence

Use #500 common ladder:

`Host -> Driver -> Network Probe -> ConnectionTest -> PointReadTest -> Live TAG`

KNX-specific detail fields may include:

- interface/router identity;
- endpoint;
- tunneling/routing;
- secure mode;
- tunnel slot/session status;
- last RX/TX;
- telegram counters;
- group address;
- reconnect count;
- sequence error count;
- replay/authentication errors;
- secure/plain downgrade state = never allowed.

DALI-via-gateway details may include only what the underlying provider/gateway actually exposes:

- DALI bus online/fault;
- lamp failure;
- gear failure;
- emergency status;
- gateway fault;
- mapped DALI line/address/group;
- sensor/input health.

Do not create a second diagnostics subsystem.

## 18. Availability and quality

TAG quality must be based on point evidence, not merely socket/session state.

Examples:

- KNX tunnel connected does not make every group object Good forever;
- BACnet gateway reachable does not make every DALI ballast healthy;
- Modbus connection success does not erase a DALI communication-error register;
- lamp failure can be a semantic device fault while communication quality remains Good.

Keep:

- communication quality;
- process/device status;
- alarm/fault semantics

as separate concepts.

## 19. Multi-interface behavior

The future KNX Data Source should bind explicitly to one configured access context.

Multiple KNX/IP interfaces are separate Data Sources unless Main later defines a failover/shared-session abstraction.

Do not automatically spray discovery/writes across every discovered interface.

A duplicate group address on different installations must remain separated by DataSourceId.

## 20. Failure and reconnect rules

### KNX

- reconnect with bounded backoff;
- preserve secure requirement;
- invalidate stale connection state;
- reject malformed frames;
- reject replay/auth failures;
- avoid write retry patterns that can duplicate non-idempotent semantics without evidence.

### Gateway

- gateway unavailable -> affected TAGs degrade according to canonical quality policy;
- DALI device failure exposed by gateway -> preserve communication path but surface device fault;
- configuration mismatch -> fail readiness/PointRead with actionable error;
- no automatic recommissioning.

## 21. Save / Publish / Activate lifecycle

Engineering edits should remain non-live until the canonical lifecycle activates them.

For KNX:

- connection settings;
- secret references;
- group address;
- DPT;
- write permission;
- semantic metadata

must follow Save/Publish/Activate.

Activating a new KNX configuration may rebuild/reconnect the Data Source, but must not mutate ETS/DALI commissioning.

## 22. Architecture gates for future KNX DEV

Main should not release the KNX implementation until these are explicit:

1. Falcon/selected-stack legal approval.
2. `KNX-PROTECTED-MATERIAL-BACKING-01` decision.
3. exact v1 binding schema and DPT set.
4. explicit tunneling-first scope.
5. Secure required/no-downgrade policy.
6. L2 independent peer choice.
7. L4 secure interface hardware in hand.
8. #500 diagnostics integration contract.
9. no parallel Runtime authority.
10. no commissioning operations in normal HMI scope.

## 23. Combined architecture disposition

KNX:

`GO_WITH_GATES`

Architecture:

`BUILT_IN_DOTNET / TUNNELING_FIRST / SECURE_CAPABLE / CANONICAL_RUNTIME`

DALI first path:

`MODBUS_TCP -> INTESIS DALI-2`

DALI fallback:

`BACNET_IP -> LOYTEC L-DALI`

Strategic combined path:

`KNX/IP -> THEBEN P64 -> DALI-2`

Native DALI:

`NOT_JUSTIFIED`

Protected material:

`MAIN_DECISION_REQUIRED / RECOMMEND DRIVER_RESOLVER_ADAPTER_OVER_GENERIC_AUTHORITY`

DOCS_ONLY  
NO PRODUCT CODE CHANGED  
NO DEPENDENCY CHANGED  
NO CI CHANGED  
NO MERGE PERFORMED

## 24. Sources revalidated 2026-10-06

Official/public:

- KNX Association — Secure Tunneling: https://support.knx.org/hc/en-us/articles/360000653399-Secure-Tunneling
- KNX Association — Use keyring outside ETS & Falcon SDK: https://support.knx.org/hc/en-us/articles/360001582259-Use-keyring-outside-ETS-Falcon-SDK
- KNX Association — Project export: https://support.knx.org/hc/en-us/articles/360020990259-Project-export
- KNX Association — Project Scheme Documentation: https://support.knx.org/hc/en-us/article_attachments/360024169360
- KNX Association — ETS overview: https://www.knx.org/what-ets
- Weinzierl — KNX IP Interface 732 secure: https://weinzierl.de/en/products/knx-ip-interface-732-secure/
- Weinzierl — KNX IP Router 752 secure: https://weinzierl.de/en/products/knx-ip-router-752-secure/
- Theben — DALI-Gateway P64 KNX 4940303: https://www.theben.de/en/dali-gateway-p64-knx-4940303
- LOYTEC — L-DALI BACnet Controllers: https://www.loytec.com/products/dali/l-dali-wired/l-dali-bacnet

EliteSCADA live release base:

- `src/Scada.Drivers/Abstractions/CommunicationDriverProtectedMaterial.cs`
- `src/Scada.Api/Security/ProtectedMaterialAuthority.cs`
- `docs/WAVE15-PROTECTED-MATERIAL-AUTHORITY.md`
- `src/Scada.Drivers/Modbus/ModbusTcpDriverDescriptorProvider.cs`
- `src/Scada.Drivers/Bacnet/BacnetDriverDescriptor.cs`
- #497 protected-material foundation closure/integration
- #500 diagnostics convergence closure
