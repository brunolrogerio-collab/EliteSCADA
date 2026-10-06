# KNX/IP Tier A Research — Checkpoint 1

Status: CHECKPOINT_1_COMPLETE / RESEARCH_ONLY / DOCS_ONLY  
Issue: #539 — HOME-RESEARCH-01  
Contract: C-HOME-KNX-DALI-RESEARCH-01  
Order: HOME-KNX-DALI-TIER-A-RESEARCH-01  
Research branch: research/home-knx-dali-execution  
Release base revalidated before this checkpoint: wave15/corrections-integration@69d5248e462ef12c41d99c6859a3afb77c6e3e2d  
Research access date: 2026-10-06

## 1. Scope and boundary

This document is the first bounded research checkpoint for KNX/IP. It does not authorize implementation.

Checkpoint 1 covers:

- KNXnet/IP tunneling, routing, discovery and lifecycle;
- KNX IP Secure and KNX Data Secure;
- protected key material and ETS keyring implications;
- bounded v1 datapoint-type scope;
- implementation-stack comparison;
- a provisional implementation recommendation for a future DEV lane.

Explicitly out of scope here:

- DALI gateway selection;
- product code;
- Driver SDK changes;
- Runtime changes;
- Engineering schema/UI changes;
- tests;
- dependencies;
- CI/workflows;
- merge.

The existing EliteSCADA architecture remains authoritative: one canonical Runtime spine, normal TAG write authority, shared diagnostics, and host-owned protected material. KNX must fit those seams rather than create a parallel KNX Runtime.

## 2. Executive checkpoint conclusion

Provisional KNX decision: GO_WITH_GATES.

Recommended first architecture:

KNX/IP interface or router
-> one EliteSCADA KNX Data Source
-> group-address bindings with exact DPT identity
-> canonical TAGs / Equipment / Capabilities where metadata exists
-> canonical Runtime.WriteAsync(TAG) for normal process writes.

Recommended initial transport:

1. KNXnet/IP tunneling as the default first path.
2. KNX IP Secure tunneling when supported/configured.
3. KNXnet/IP routing as an explicit advanced deployment mode after multicast/VLAN/container validation.

Recommended implementation form:

BUILT_IN_DOTNET preferred over SIDECAR.

Preferred current candidate:

KNX Falcon 6 Public SDK, current public release line 6.4.0 / NuGet package 6.4.8671.

Fallback:

XKNX sidecar only if Falcon legal/technical gates fail or if it is intentionally selected as an isolated service. XKNX is also a strong independent L2 interoperability peer.

The recommendation is gated by:

- LEGAL_REVIEW_REQUIRED for Falcon redistribution/license/trademark terms;
- Linux/Windows/net10 packaging proof;
- container proof, especially routing multicast;
- protected keyring material integration;
- independent interoperability tests and real KNX/IP Secure hardware.

## 3. KNXnet/IP transport model

### 3.1 Tunneling

KNXnet/IP tunneling is the preferred first EliteSCADA transport because it gives a bounded client/session relationship to a known KNX/IP interface/router and is easier to reason about across firewalls, VLANs and containers than multicast routing.

Official KNX documentation identifies UDP port 3671 as the normal KNXnet/IP port and documents tunneling configuration around server/IP, host individual address, tunnel individual addresses and NAT handling.

Implementation expectations for a future driver:

- configure one interface/router endpoint per Data Source;
- establish and maintain a bounded tunnel/session;
- use the session for many TAG bindings rather than opening one connection per TAG;
- treat connection slots as a device-specific scarce resource;
- expose slot/session exhaustion through common diagnostics;
- handle sequence/acknowledgement state, connection-state heartbeat, disconnect and reconnect;
- fail closed when a secure session is required but cannot be established;
- do not silently fall back from secure to plain tunneling.

Exact timer values and wire-level behavior must come from the applicable KNX specification/SDK, not from copied third-party constants.

### 3.2 Routing

KNXnet/IP routing is multicast-oriented and should not be the default first deployment mode.

Official KNX documentation identifies:

- UDP port 3671;
- default routing multicast address 224.0.23.12;
- the same standardized system-setup multicast address for discovery/search behavior;
- support for installation-specific multicast groups where applicable.

EliteSCADA implications:

- routing requires deliberate multicast policy and interface selection;
- VLAN/firewall design must carry the required multicast traffic;
- IGMP/multicast infrastructure must be validated;
- Docker/container networking cannot be assumed to pass multicast correctly under every default bridge configuration;
- routing should be an opt-in Data Source mode with explicit host/network diagnostics.

Routing is attractive where the installation intentionally operates a routed KNX/IP backbone, but tunneling is a safer first product boundary.

### 3.3 Discovery is not project discovery

Network discovery can identify KNX/IP interfaces/routers and their advertised access capabilities. It does not provide the complete semantic project model.

The KNX ecosystem separates network transport from project semantics. The receiver still needs knowledge of group addresses, datapoint types and project meaning. ETS/project metadata can supply that context.

Therefore EliteSCADA must separate:

- KNX/IP interface/router discovery;
- group-address binding knowledge;
- ETS project/group-address metadata import.

A future Engineering discovery operation may discover interfaces. It must not claim to have reconstructed the KNX project.

## 4. Session and reconnect behavior

A future tunneling client should be designed around this lifecycle:

1. resolve endpoint and local interface;
2. establish tunnel/session;
3. establish secure session when configured;
4. exchange cEMI telegram payloads;
5. maintain connection/heartbeat state;
6. validate sequencing/acknowledgements;
7. disconnect cleanly;
8. reconnect with bounded backoff after transport/session failure.

Routing does not use the same per-tunnel session-slot model; it is multicast communication and should have separate readiness semantics.

Current active XKNX implementation history is useful non-normative evidence that practical clients need robust reconnect behavior, acknowledgement handling, duplicate/sequence handling and connection-state heartbeat. It must not replace the KNX normative contract.

## 5. cEMI, telegram and address semantics

KNXnet/IP carries KNX telegram information using cEMI-related message payloads. A future EliteSCADA driver should preserve protocol identity instead of flattening everything prematurely.

Required binding identity should include at least:

- group address;
- exact DPT identifier;
- direction/access role needed by the point;
- read/write expectations;
- secure/plain expectation;
- optional source/individual-address metadata for diagnostics;
- stable EliteSCADA DataSourceId/TAG identity.

Group addresses are 16-bit KNX addresses. Project semantics must not be inferred merely from a numeric group address.

Individual addresses describe KNX participants/interfaces. They are useful for diagnostics and engineering context, but process TAG bindings should primarily bind to the intended group objects/group addresses.

## 6. Secure architecture

### 6.1 KNX IP Secure

KNX IP Secure protects communication on the IP side. Official KNX security documentation describes confidentiality, integrity/authentication and freshness/replay protection for secure IP communication, with ETS-created/provisioned key material.

Relevant implementation concepts include:

- secure tunneling;
- secure routing;
- session authentication/establishment;
- sequence/freshness validation;
- replay rejection;
- backbone key material for secure routing;
- user/device authentication material for secure tunneling/device access.

EliteSCADA strategy:

- secure mode is explicit in Data Source configuration;
- if secure mode is required, connection failure must remain failure;
- never silently downgrade to plain KNXnet/IP;
- record secure/non-secure state in protocol diagnostics without exposing credentials.

### 6.2 KNX Data Secure

KNX Data Secure protects application/group communication end-to-end across the KNX installation, independent of the IP transport protection boundary.

Important distinction:

- IP Secure protects the KNX IP transport path.
- Data Secure protects secured application/group communication.

An installation can therefore require handling both dimensions.

Official KNX documentation also makes group security semantics important: secured and unsecured communication must not be casually mixed for the same secured group-address contract.

EliteSCADA binding metadata should preserve whether a group object is expected to use Data Secure and must fail closed when the required security material is unavailable or incompatible.

### 6.3 Replay and sequence handling

Sequence/freshness state is security state, not merely a diagnostic counter.

Future implementation requirements:

- validate received sequence/freshness data;
- reject replayed or invalid secure traffic;
- persist or reconstruct required security state exactly as the selected SDK/spec requires;
- surface sequence/replay failures through common diagnostics;
- never “repair” an authentication/replay error by retrying in plaintext.

## 7. ETS keyrings and Protected Material Authority

Official KNX documentation describes ETS keyring exports as security-bearing artifacts. A keyring can contain or derive material such as:

- backbone keys;
- tunnel passwords/authentication material;
- IP device management credentials;
- device tool keys;
- secured group-address keys.

The .knxkeys password protects security-relevant keyring data. The keyring is therefore protected input even if its container format is not itself a generic encrypted vault.

### 7.1 EliteSCADA storage recommendation

Converge on the existing host-owned ICommunicationDriverProtectedMaterialResolver boundary.

Engineering/package configuration should store references and non-secret metadata only, for example:

- keyring source/fingerprint reference;
- secure mode;
- endpoint identity;
- project/source fingerprint;
- secret reference identifiers.

Never place raw key material, passwords, authentication codes, secure tunnel credentials or decrypted group keys in:

- .escadapkg;
- Engineering JSON;
- logs;
- diagnostics;
- ordinary issue artifacts.

Recommended provisioning flow:

1. an administrative/Engineering action selects a KNX keyring/project security artifact;
2. protected code validates the artifact/password;
3. normalized secret material is stored in the host-owned protected-material authority;
4. Engineering stores only scoped references/fingerprints;
5. Runtime resolves only the secrets needed for the exact project/Data Source/driver/purpose;
6. leases are disposed/cleared at the defined boundary.

If retention of the original .knxkeys file is ever necessary, it should be retained only as a protected encrypted blob, not inside ordinary project JSON/package data.

### 7.2 Rotation

Credential/key rotation should be a controlled administrative operation:

- import/validate a new keyring snapshot;
- verify project/device/group identities;
- create new protected references;
- activate the new references at a controlled Save/Publish/Activate boundary;
- fail closed if expected identities/security keys no longer match.

KNX key changes, ETS programming and secure provisioning are commissioning/admin operations. They are not ordinary HMI commands.

## 8. KNX -> EliteSCADA canonical mapping

Recommended mapping:

| KNX concept | EliteSCADA projection |
| --- | --- |
| KNX/IP interface/router | Data Source |
| group address/group object | TAG binding |
| physical/logical device | Equipment when known/mapped |
| DPT identity | TAG type + Engineering Unit + Capability semantics |
| normal process write | Runtime.WriteAsync(TAG) |
| ETS group/device metadata | optional future Engineering import metadata |
| IP/router discovery | Engineering discovery, not Runtime mutation |
| commissioning/programming | privileged admin/commissioning flow, not HMI |

There must be no parallel “KNX Runtime” authority.

Equipment should not be fabricated from network discovery alone. It should come from explicit user mapping or trustworthy ETS/project metadata.

## 9. Datapoint type strategy

### 9.1 Design rule

The KNX standard defines DPTs as semantic types: format, encoding, value range and unit/meaning belong together.

EliteSCADA must therefore preserve the exact DPT identity in each KNX binding. Mapping DPT 9.xxx to “float” is insufficient because subtypes carry different engineering semantics.

Unknown or unsupported DPTs must not be guessed.

### 9.2 V1_DPT_SET

Recommended bounded first set:

| DPT | Purpose | EliteSCADA semantic projection |
| --- | --- | --- |
| 1.001 | Switch | Boolean / switch |
| 1.002 | Bool | Boolean |
| 1.008 | UpDown | Boolean-direction capability |
| 1.009 | OpenClose | Boolean/open-close capability |
| 1.100 | HeatCool | HVAC heat/cool selector |
| 3.007 | Control_Dimming | relative dimming command |
| 3.008 | Control_Blinds | relative blind control |
| 5.001 | Scaling | percentage 0..100% |
| 9.001 | Value_Temp | temperature; also usable for temperature setpoint role |
| 9.004 | Value_Lux | illuminance |
| 9.007 | Value_Humidity | humidity |
| 9.020 | Value_Volt | voltage |
| 9.021 | Value_Curr | current |
| 9.024 | Power | power, KNX 2-byte float range |
| 10.001 | TimeOfDay | time |
| 11.001 | Date | date |
| 12.001 | Value_4_Ucount | unsigned counter |
| 13.001 | Value_4_Count | signed counter |
| 13.010 | ActiveEnergy | Wh |
| 13.013 | ActiveEnergy_kWh | kWh |
| 14.056 | Value_Power | power in W, 4-byte float |
| 17.001 | SceneNumber | scene number |
| 18.001 | SceneControl | scene activate/learn semantics |
| 20.102 | HVACMode | Auto/Comfort/Standby/Economy/Building Protection |

Setpoint is treated as a Capability/binding role, not a made-up separate KNX type. Temperature setpoints commonly retain DPT 9.001 semantics while the Equipment/Capability model records that the point is a setpoint.

### 9.3 DEFERRED_DPT_SET

Defer until evidence from supported devices/gateways requires them:

- DPT 2.x controlled booleans;
- DPT 5.004 Percent_U8 unless a supported product specifically requires its alternative scale;
- broad 6.x/7.x/8.x families beyond an exact approved need;
- DPT 13.016 MWh and other extra metering variants;
- DPT 19.001 combined date/time because of richer flag semantics;
- advanced 20.x HVAC enumerations beyond 20.102;
- 21.x/22.x status bitfields;
- 16.x/28.x strings;
- 29.x 64-bit values;
- 229.x M-Bus metering composites;
- color/DT8-related 232/242/243/249-254 families until DALI gateway research establishes the required mappings;
- vendor/private/non-standard DPTs.

Rule: unsupported DPT -> explicit unsupported/validation result, not heuristic decoding.

## 10. Implementation stack comparison

### 10.1 KNX Falcon 6 Public SDK

Current public evidence:

- KNX Association public Falcon SDK;
- Falcon 6.4.0 release dated 2026-03-05;
- NuGet package 6.4.8671;
- net8.0/net48 package targets, with modern .NET compatibility;
- official Windows/Linux/macOS .NET support documented for the modern SDK family;
- KNXnet/IP tunneling/routing support;
- KNX IP Secure and KNX Data Secure support in the Falcon 6 line;
- active fixes include secure-tunneling and tunneling lifecycle items.

Strengths:

- official KNX implementation source;
- strong protocol/security coverage;
- direct .NET integration;
- no extra Python/Java runtime;
- best fit with EliteSCADA net10 host and canonical Driver composition.

Risks/gates:

- not a permissive open-source dependency;
- distribution/use governed by the KNX Tools Software License Agreement;
- trademark/logo/certification rights are separate;
- LEGAL_REVIEW_REQUIRED before committing the dependency;
- Linux/container and net10 behavior still requires an EliteSCADA proof.

Disposition: PREFERRED_IMPLEMENTATION_WITH_GATES.

### 10.2 ChrisTTian667/knx-dotnet

Current evidence:

- MIT license;
- .NET library;
- repository advertises routing, tunneling and gateway discovery;
- latest published GitHub prerelease is the 2023 alpha line;
- repository activity is materially older than current KNX secure work;
- no adequate current evidence for Tier A KNX Secure/Data Secure coverage.

Strength:

- permissive license and native .NET.

Risk:

- alpha/stale release posture and insufficient Secure evidence.

Disposition: REJECT_FOR_TIER_A_PRODUCTION. It may be useful as historical/reference material only.

### 10.3 XKNX

Current evidence:

- XKNX 3.20.0 released 2026-08-16;
- repository active through 2026-10;
- MIT license;
- Python 3.10+;
- current dependencies include cryptography and interface-address discovery support;
- mature tunneling/routing/reconnect behavior;
- secure tunneling/routing and Data Secure support in the current project history;
- broad DPT support;
- used as the underlying KNX library for Home Assistant.

Strengths:

- current, active, permissive;
- broad real-world protocol support;
- excellent independent interoperability peer for L2;
- strongest sidecar fallback.

Costs:

- Python runtime/process;
- IPC contract;
- lifecycle/service management;
- packaging/update surface;
- larger protected-material trust boundary.

Important license separation:

The separate XKNX ETS-project parser ecosystem must not be assumed to share XKNX's MIT license. Any project-parser dependency requires its own license review.

Disposition: SIDECAR_FALLBACK and PREFERRED_L2_INDEPENDENT_PEER.

### 10.4 Calimero Core

Current evidence:

- current 3.0 milestone release line in 2026;
- active repository;
- Java/JDK 21 runtime;
- KNXnet/IP discovery/tunneling/routing;
- KNX IP Secure;
- KNX Data Secure;
- cEMI and broad DPT support;
- software test-network tooling;
- GPLv2 with Classpath Exception.

Strengths:

- technically mature and broad;
- valuable independent protocol oracle/peer.

Costs:

- Java sidecar runtime;
- packaging/process complexity;
- legal review for distribution/integration.

Disposition: STRONG_L2_PEER / NOT_PREFERRED_RUNTIME.

### 10.5 Own .NET implementation

Strengths:

- full control;
- no external runtime/process;
- can be shaped exactly to EliteSCADA contracts.

Risks:

- highest implementation/security burden;
- exact KNXnet/IP, cEMI, DPT and Secure behavior must be independently implemented and maintained;
- larger conformance/certification/spec-access exposure;
- cryptographic/session/replay bugs would become EliteSCADA-owned protocol defects.

Disposition: NOT_FIRST_CHOICE. Consider only if the Falcon legal gate fails and a sidecar is also rejected.

## 11. BUILT_IN_DOTNET versus SIDECAR

Recommendation: BUILT_IN_DOTNET.

Why:

- EliteSCADA is net10 and already has a canonical in-process Driver/runtime spine;
- Falcon is an official current .NET SDK with the protocol/security surface needed;
- built-in avoids a second process, IPC authority and sidecar lifecycle;
- secrets can remain behind the existing protected-material resolver;
- diagnostics and Runtime.WriteAsync can remain on the existing shared contracts.

SIDECAR is a fallback, not the default. If used, XKNX is the strongest current candidate, but it requires explicit process lifecycle, authentication/IPC, resource limits, packaging and secret-scoping work.

## 12. Deployment implications

### Windows/Linux

A future implementation must validate both supported desktop/server OS families. Falcon's modern .NET requirements cover Windows/Linux support, but EliteSCADA must still test the exact package/runtime combination it ships.

### Container

Preferred first container posture:

- tunneling/unicast first;
- explicit local NIC/interface selection where required;
- no promise that routing multicast works through an arbitrary default Docker bridge.

Routing validation must include:

- multicast membership;
- outbound/inbound UDP 3671;
- IGMP/network-switch behavior;
- container NIC/host-network mode where necessary;
- Windows and Linux host differences.

### VLAN/firewall

Tunneling:

- allow the configured unicast KNX/IP path;
- restrict scope to the expected endpoint/network.

Routing:

- requires multicast-aware VLAN/firewall/switch policy.

### Remote/WAN

Do not expose KNX UDP 3671 directly to the public Internet.

Preferred remote patterns:

- private routed network/VPN;
- secure access infrastructure;
- KNX Secure as required by the installation.

Remote reachability must not be mistaken for an authorization model.

## 13. Diagnostics convergence

Extend #500 common diagnostics only.

Protocol-specific details can include:

- connected/disconnected;
- selected interface/router endpoint;
- tunneling/routing mode;
- secure/plain mode;
- secure-session authentication status;
- sequence/replay error count;
- telegram RX/TX count;
- last RX/TX time;
- last group address;
- reconnect count;
- tunnel/session/slot failure reason;
- multicast interface when routing.

Do not create another diagnostics framework.

The existing diagnostic ladder remains:

Host -> Driver -> Network Probe -> ConnectionTest -> PointReadTest -> Live TAG.

## 14. Commissioning boundary

Normal Runtime operations:

- receive group telegrams;
- update TAG values/quality;
- canonical read where supported/appropriate;
- canonical Runtime.WriteAsync(TAG);
- diagnostics.

Administrative/commissioning operations, not normal HMI commands:

- KNX individual-address programming;
- ETS programming;
- device enrollment/programming mode;
- key changes;
- keyring provisioning;
- factory/reset-style device mutation;
- secure credential rotation;
- network/project structure mutation.

## 15. Initial L0-L4 implications

Checkpoint 1 does not implement tests, but it establishes the future test shape.

L0:
- group/individual address codec;
- cEMI/KNXnet-IP frame codec where owned;
- exact DPT codecs for V1_DPT_SET;
- secure known-answer vectors where implementation owns crypto;
- sequence/replay;
- malformed packet handling.

L1:
- fake KNX/IP peer;
- discovery;
- tunneling;
- routing;
- secure session failure;
- reconnect/timeout;
- duplicates/malformed/flood cases.

L2:
- independent implementation, preferably XKNX and/or Calimero;
- do not use the same codec on both sides.

L3:
- real EliteSCADA Data Source/TAG/Equipment/Capability;
- canonical Runtime.WriteAsync;
- shared diagnostics;
- Save/Publish/Activate/restart behavior.

L4:
- real KNX/IP Secure interface/router;
- actuator/dimmer/sensors and later the selected KNX/DALI gateway.

The exact purchase matrix belongs to Checkpoint 3.

## 16. Legal/certification flags

LEGAL_REVIEW_REQUIRED:

1. Falcon 6 Tools Software License Agreement before product dependency/distribution approval.
2. KNX trademark/logo use and any certification claims.
3. Whether the exact planned EliteSCADA host software classification triggers any KNX certification/member obligations.
4. ETS project/keyring import/export/reuse rights for a future importer.
5. Any separate ETS project parser, including license terms independent from XKNX itself.
6. Calimero distribution/use if ever selected in shipped product.

Do not infer certification or branding rights from an open-source license.

## 17. Uncertainties carried forward

- Falcon legal approval has not been granted by this research lane.
- net10 compatibility is strongly indicated by modern .NET compatibility but still needs an EliteSCADA compile/runtime proof.
- secure hardware interoperability is unproven until L4.
- tunnel-slot limits vary by interface/router model and must be captured during hardware selection.
- routing multicast behavior varies with network/container policy and must be tested.
- ETS project-import scope and rights remain legal/design work.
- the DPT v1 set can be narrowed or expanded only from evidence of the first supported hardware/gateway portfolio.
- DALI/DT8-related DPTs intentionally wait for Checkpoint 2.

## 18. Sources reviewed

All source facts below were revalidated on 2026-10-06.

| Organization | Document/release | Version/date | URL | Fact supported |
| --- | --- | --- | --- | --- |
| KNX Association | KNX Specifications | V3, published 02/2025; page current 2026 | https://support.knx.org/hc/en-us/articles/360000040999-KNX-Specifications | current specification family and access model |
| KNX Association | Interfaces | current support article | https://support.knx.org/hc/en-us/articles/360012026220-Interfaces | tunneling/routing port, NAT and interface parameters |
| KNX Association | Connection Manager — Detailed | current support article | https://support.knx.org/hc/en-us/articles/4402353231762-Connection-Manager-Detailed | routing multicast and discovery/system-setup multicast |
| KNX Association | KNX Security overview | updated 2026-09-23 | https://support.knx.org/hc/en-us/articles/360012630199-KNX-Security-overview | IP Secure vs Data Secure boundary |
| KNX Association | KNX IP Secure | current support article | https://support.knx.org/hc/en-us/articles/360012666599-KNX-IP-Secure | secure IP properties, key/session concepts |
| KNX Association | KNX Data Secure | current support article | https://support.knx.org/hc/en-us/articles/360012689639-KNX-Data-Secure | group/application security semantics |
| KNX Association | Security | current support article | https://support.knx.org/hc/en-us/articles/360018505379-Security | ETS secure project/keyring behavior |
| KNX Association | Use keyring outside ETS / Falcon SDK | updated 2026-07-07 | https://support.knx.org/hc/en-us/articles/360001582259-Use-keyring-outside-ETS-Falcon-SDK | keyring material and derivation/access |
| KNX Association | Secure Tunneling | current support article | https://support.knx.org/hc/en-us/articles/360000653399-Secure-Tunneling | secure tunnel + external keyring use |
| KNX Association | Updating stack for KNX Secure | current support article | https://support.knx.org/hc/en-us/articles/4708008152338-Updating-stack-for-KNX-Secure | secure app-note/certification considerations |
| KNX Association | Group Address Export | current support article | https://support.knx.org/hc/en-us/articles/115001825324-Group-Address-Export | ETS metadata export including DPT/group-object semantics |
| KNX Association | Group Address Ranges | current support article | https://support.knx.org/hc/en-us/articles/115001825304-Group-Address-Ranges | group-address model |
| KNX Association | KNX Standard Interworking Datapoint Types | v02.02.01 | https://support.knx.org/hc/en-us/article_attachments/15392631105682 | exact DPT identities, encodings, units and semantics |
| KNX Association | Falcon 6 .NET SDK v6.4.0 | 2026-03-05 | https://support.knx.org/hc/en-us/articles/31860216353426-Falcon-6-NET-SDK-v6-4-0 | current Falcon release |
| KNX Association | Falcon requirements | current support article | https://support.knx.org/hc/en-us/articles/4410825434642-Requirements | OS/.NET/network requirements |
| KNX Association | What is the Falcon .NET SDK | current support article | https://support.knx.org/hc/en-us/articles/4410819043986-What-is-the-Falcon-NET-SDK | public SDK positioning/access |
| KNX Association | KNX Tools Software License Agreement | v6.18, 2025-12 | https://support.knx.org/hc/en-us/article_attachments/18554600032786 | Falcon distribution/license/trademark terms |
| NuGet / KNX Association | Knx.Falcon.Sdk | 6.4.8671 | https://www.nuget.org/packages/Knx.Falcon.Sdk/6.4.8671 | current package/targets |
| XKNX | xknx repository/release | 3.20.0, 2026-08-16 | https://github.com/XKNX/xknx | active MIT Python stack; Home Assistant basis |
| XKNX | xknx pyproject | 3.20.0 | https://github.com/XKNX/xknx/blob/3.20.0/pyproject.toml | Python/dependency/license requirements |
| ChrisTTian667 | knx-dotnet | alpha line; repository current status checked 2026-10-06 | https://github.com/ChrisTTian667/knx-dotnet | MIT .NET candidate, tunneling/routing/discovery claims and maintenance posture |
| Calimero project | calimero-core | v3.0-M2, 2026-04-04 | https://github.com/calimero-project/calimero-core | active Java stack with KNX IP Secure/Data Secure/DPT support |
| EliteSCADA live repo | Architecture / protected material | current base | docs/ARCHITECTURE.md and src/Scada.Drivers/Abstractions/CommunicationDriverProtectedMaterial.cs | canonical secret-reference / host-owned protected-material seam |

## 19. Checkpoint 1 disposition

State:

CHECKPOINT_1_COMPLETE / GO_WITH_GATES / RESEARCH_ONLY / DOCS_ONLY

Facts established:

- tunneling is the recommended first KNX/IP transport;
- routing is opt-in and multicast/network-policy dependent;
- discovery does not equal project/group-address semantic discovery;
- IP Secure and Data Secure are distinct and both matter;
- keyring material must converge on Protected Material Authority;
- exact DPT identity must survive into bindings;
- V1_DPT_SET is bounded rather than “all DPTs”;
- BUILT_IN_DOTNET is preferred;
- Falcon 6 is the preferred current candidate with legal/technical gates;
- XKNX is the strongest sidecar fallback and L2 peer;
- existing EliteSCADA Runtime.WriteAsync and #500 diagnostics remain authoritative.

No DALI gateway selection has been performed in this checkpoint.

DOCS_ONLY  
NO PRODUCT CODE CHANGED  
NO DEPENDENCY CHANGED  
NO CI CHANGED  
NO MERGE PERFORMED
