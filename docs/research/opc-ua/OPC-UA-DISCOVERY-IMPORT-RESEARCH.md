# OPC UA discovery, browse and TAG-import research

Status: **RESEARCH IN PR / NOT IMPLEMENTED**  
Date: 2026-08-26  
Assignment: `DEV 1 - EliteSCADA` / `research/opc-ua-discovery-import`

This document is a non-production research deliverable for the future OPC UA Engineering workflow. It does not register an OPC UA Data Source, add a runtime package, change the canonical Engineering schema, or authorize production OPC UA implementation before the locked Gateway -> common diagnostics -> interface-validation gates are complete.

The product requirements in `docs/OPC-UA.md` remain authoritative. This research turns those requirements into a concrete implementation direction and records the external evidence used.

## Executive recommendation

1. Use the **official OPC Foundation UA .NET Standard stack** rather than implementing the OPC UA protocol privately.
2. For a future EliteSCADA client implementation, prefer the split client packages rather than the all-in-one meta package: `OPCFoundation.NetStandard.Opc.Ua.Client`, required Core/Configuration dependencies, and `OPCFoundation.NetStandard.Opc.Ua.Client.ComplexTypes` only where complex-type support is deliberately enabled.
3. Re-review the exact stable package version immediately before implementation. As of this research, the official repository identifies **2.0** as the active development line, targets .NET 10, and keeps 1.5.378 as the supported 1.x maintenance branch. The 2.0 line also introduces breaking API changes, so the first production slice should pin an exact reviewed version rather than floating package versions.
4. The repository is published under the **OPC Foundation MIT License**. License review must still be repeated for the exact selected package graph at implementation time.
5. Build discovery in layers: explicit/manual URL -> standard FindServers/GetEndpoints -> LDS/LDS-ME/mDNS FindServersOnNetwork -> bounded TCP fallback only when necessary.
6. Treat discovery as temporary Engineering assistance. Discovered servers never become project configuration until normal Engineering Preview/Apply succeeds.
7. Store portable OPC UA identity as **server ApplicationUri + namespace URI + namespace-aware BrowsePath + last resolved NodeId**, not NamespaceIndex/NodeId alone.
8. Use lazy Browse/BrowseNext with opaque continuation points. Do not recursively download an entire server address space as the price of opening the browser.
9. Use OPC UA native Subscriptions/MonitoredItems for runtime acquisition. Keep public EliteSCADA subscription profiles independent from the library's private classes.
10. First production type support should be deliberately scalar and lossless. Unsupported arrays/structures/unsigned ranges must be visible import issues, not silent coercions.

## Evidence: Elipse E3 workflow comparison

Elipse E3 provides several workflow conveniences worth preserving conceptually:

- `Select Server` enumerates local/network OPC UA servers and their endpoints, including protocol/security/authentication alternatives.
- Selecting a compatible endpoint fills endpoint/security properties.
- Communication can be activated before import and server information can be inspected.
- `Import Tags` browses server items and supports multiple selection.
- OPC UA Subscription objects group Tags around a common scan/update policy.
- Imported Tags retain a browsing path and E3 can update NodeIds for all imported Tags from that path.
- Driver views expose server information and TAG counts.

EliteSCADA should adopt these conveniences but improve several boundaries:

| Elipse concept | EliteSCADA direction |
| --- | --- |
| Server/endpoints selection | Adopt. Show server/application identity, endpoint URL, transport, SecurityMode, SecurityPolicy and user-token policies. |
| Activate/test before import | Adopt as a temporary protected Engineering connection test, not Active Runtime activation. |
| Multi-select import | Adopt, but selection only creates candidates. Canonical `validate -> preview -> apply` remains mandatory. |
| Subscription object/scan grouping | Adopt concept as reusable subscription profiles, not as the public shape of the OPC Foundation SDK. |
| Stored path used to refresh NodeId | Adopt and strengthen: keep namespace URI and namespace-aware BrowsePath plus last NodeId and server identity. |
| Server information / TAG count | Adopt, with protected/sanitized Engineering diagnostics. |
| Accept any valid server certificate | **Do not adopt.** EliteSCADA should fail closed for an unknown or changed server identity and require explicit trust/reconciliation. |
| Username/password in driver properties | Improve: Engineering stores only secret references, never plaintext credentials in canonical packages. |
| Path representation based on namespace index | Improve: preserve namespace URI, because NamespaceIndex is server-session/runtime-local and may change. |

The Elipse documentation also notes current limitations around custom server-specific structures and some array mappings. EliteSCADA should expose unsupported/lossy candidates explicitly instead of pretending every server data type is a scalar SCADA value.

## Recommended OPC Foundation .NET stack direction

### Why the official stack

The OPC Foundation repository currently provides:

- Core, Client, Server, Configuration, Complex Types and GDS/LDS support;
- UA-TCP and optional HTTPS transports;
- X.509 certificate support and certificate stores;
- anonymous, username/password and X.509 user authentication;
- sessions, reconnect support, subscriptions and monitored items;
- discovery operations including FindServers and FindServersOnNetwork;
- .NET 10 support;
- reference client/server applications and a reference-server container suitable for testing.

For new 2.0 client code, official documentation recommends the newer managed/V2 subscription APIs for long-lived application subscriptions. The future EliteSCADA adapter should hide these SDK details behind protocol-neutral source-provider/runtime contracts.

### Package selection proposal

Future production project dependency direction:

- required: `OPCFoundation.NetStandard.Opc.Ua.Client`;
- transitive/required Core and Configuration packages as dictated by the reviewed release;
- optional: `OPCFoundation.NetStandard.Opc.Ua.Client.ComplexTypes` when complex-type decoding is intentionally enabled;
- optional HTTPS binding only if a product requirement justifies `opc.https`/HTTPS transport;
- do not reference Server/PubSub/GDS packages from the normal runtime OPC UA client unless a concrete feature requires them.

No package is added by this research branch.

### Version and security policy

Do not hard-code a future version in the Engineering schema. Pin the implementation dependency in project files when the production slice starts, and review:

- current stable 2.x release;
- OPC Foundation security advisories and `SECURITY.md`;
- transitive dependency/license graph;
- .NET 10 target compatibility;
- known reconnect/subscription/certificate regressions for the chosen release.

## Discovery architecture

### Layered workflow

```text
Known URL entered by engineer
        |
        v
GetEndpoints / FindServers on explicit discovery URL
        |
        +----> LDS on local/known host
        |
        +----> LDS-ME / mDNS / FindServersOnNetwork when available
        |
        +----> bounded TCP fallback (opc.tcp 4840 + explicitly supplied ports)
                        |
                        v
                 GetEndpoints candidate
                        |
                        v
         endpoint/security/certificate inspector
```

OPC UA Part 12 defines LDS, LDS-ME and mDNS-based MulticastExtension discovery. `FindServersOnNetwork` returns discovery URLs/capabilities on the multicast subnet. These mechanisms should be preferred over blind network probing.

### Proposed initial network-scan guardrails

These numbers are **EliteSCADA research recommendations**, not OPC UA specification limits. They should become configurable policy with safe defaults if the feature is implemented.

- Scan is always explicit and manually started.
- Default scope: selected local interface/subnet, capped to **256 host addresses** without additional confirmation.
- A user-entered CIDR may expand the scope, but one scan operation has a proposed hard cap of **1024 host addresses**.
- Fallback TCP probing checks **4840** plus ports explicitly entered by the engineer. It does not scan arbitrary port ranges.
- Maximum **16 concurrent host probes**.
- Maximum dispatch rate: **20 hosts/second**.
- Per TCP attempt timeout: **1 second**; GetEndpoints/FindServers service calls use a separate bounded operation timeout.
- Default overall scan budget: **60 seconds**; explicit extended scans may run up to **5 minutes**.
- Cancellation must stop scheduling new probes immediately and dispose outstanding work promptly.
- Duplicate candidates are reconciled by ApplicationUri/server identity plus discovery/endpoint URLs, not by IP address alone.
- Results are transient and never auto-create Data Sources.
- mDNS may be disabled by plant policy; failure to discover via multicast is not a server fault.

These defaults intentionally bias toward industrial-network courtesy. An engineer who knows a non-standard server port should be able to add the host/port directly instead of forcing a broad scan.

## Endpoint and certificate trust model

Endpoint inspection should show at least:

- ApplicationName, ApplicationUri and ProductUri;
- endpoint URL and transport profile;
- MessageSecurityMode;
- SecurityPolicy URI;
- supported UserTokenPolicies;
- server certificate subject, issuer, serial, validity and SHA-256 thumbprint;
- certificate chain/trust result;
- compatibility warnings.

### Trust rules

1. Do not auto-accept an unknown production server certificate.
2. First contact may place the certificate in a **rejected/pending review** store and present identity details to the engineer.
3. Explicit `Trust server certificate` is a deliberate protected Engineering/admin action and should be auditable.
4. Trust should bind to the expected server/application context, not merely "a certificate exists".
5. If a previously trusted server presents a different certificate, changed ApplicationUri, invalid chain, expired/not-yet-valid certificate or hostname/application mismatch, fail closed and require reconciliation.
6. Trust-store location/configuration belongs to deployment/security configuration; private keys and passwords are not serialized in Engineering JSON.
7. Username/password authentication uses an EliteSCADA secret reference. The browse/import service receives resolved credentials only at the protected runtime boundary.
8. `SecurityMode=None` may be shown when a server offers it, but should be visibly insecure and must not be silently preferred over compatible secure endpoints.

The official stack's sample/test `autoaccept` modes are useful for deterministic test fixtures only, not production policy.

## Address-space browser design

### Lazy browse

The Engineering browser should start from standard roots (normally Objects) and request children only when the engineer expands a node.

Each page should request a bounded number of references, initially proposed at **200 references per Browse request**. If the server returns a continuation point, the client uses BrowseNext. Continuation points are opaque server resources and must be explicitly released if the user abandons a branch before consuming them.

The UI model should distinguish Object, Folder-like Object, Variable, Method and other node classes. For Variables, read/display where available:

- NodeId;
- BrowseName and DisplayName;
- namespace URI plus current index;
- DataType;
- ValueRank/array dimensions;
- AccessLevel and **UserAccessLevel**;
- Historizing;
- MinimumSamplingInterval;
- Description;
- optional EUInformation/Range metadata.

Preview values are opt-in/bounded reads. Merely browsing a tree should not issue uncontrolled value reads.

### Search

Do not depend on the OPC UA Query Service as the universal search mechanism; real servers do not consistently implement it. Search should operate over lazy Browse with explicit bounds.

Proposed recursive search guardrails:

- selected subtree only unless the engineer explicitly selects whole-server search;
- maximum **10,000 visited references/nodes** per search operation;
- maximum **32 hierarchy levels**;
- **30 second** default search budget;
- cancellation at every Browse/BrowseNext boundary;
- filters applied as early as possible: node class, namespace, data type, readable/writable, historizing and imported/not-imported state.

If a limit is reached, the result must say it is partial rather than quietly pretending the whole server was searched.

### Subtree import candidate collection

Subtree selection is a candidate-generation operation, not project mutation.

Proposed guardrails:

- default warning above **1,000 Variables**;
- default candidate cap **5,000 Variables** per import operation;
- explicit advanced override up to **20,000 candidates** with progress/cancellation;
- folders/objects are traversal context; Variable nodes become TAG candidates;
- unsupported Variables remain visible as rejected/warning candidates rather than disappearing.

## Node identity and re-resolution

### Persisted binding identity proposal

A future OPC UA TAG binding should retain enough identity for deterministic reconciliation:

```text
serverApplicationUri
namespaceUri
lastResolvedNodeId
browsePath:
  startingNode: ObjectsFolder (or another explicit stable root)
  elements[]:
    referenceType
    isInverse
    includeSubtypes
    browseName.namespaceUri
    browseName.name
```

The current NamespaceIndex may be cached as diagnostic/runtime convenience but is not authoritative persistence.

### Refresh Node IDs algorithm

1. Connect to the configured server and verify expected server/application identity.
2. Read the current NamespaceArray and map stored namespace URIs to current indexes.
3. Rebuild the stored RelativePath using namespace-aware QualifiedNames.
4. Call `TranslateBrowsePathsToNodeIds` from the stored starting node.
5. If zero targets are returned, mark the binding **Missing**.
6. If multiple plausible targets are returned, mark it **Ambiguous** and do not pick one by name similarity.
7. Read current DataType, ValueRank and user access attributes for the resolved target.
8. Compare with existing Engineering binding and produce a diff.
9. NodeId-only change with compatible identity/type may be proposed as a safe update.
10. Breaking type/access changes require explicit review. No silent rebind.

`TranslateBrowsePathsToNodeIds` explicitly allows a server to return multiple target NodeIds; EliteSCADA therefore needs an ambiguity state rather than "take the first" for imported plant points.

### Rescan diff states

At minimum:

- `Unchanged`;
- `NodeIdChanged`;
- `AccessChanged`;
- `DataTypeChangedCompatible`;
- `DataTypeChangedBreaking`;
- `Missing`;
- `Ambiguous`;
- `NewCandidate`.

Server-side deletion never auto-deletes an EliteSCADA TAG or historian data.

## OPC UA -> EliteSCADA datatype mapping proposal

Current EliteSCADA scalar `TagDataType` values are Boolean, Int16, Int32, Int64, Float, Double, String, DateTime and Enum. The initial OPC UA importer should be lossless against that set.

| OPC UA type | Initial EliteSCADA mapping | Policy |
| --- | --- | --- |
| Boolean | Boolean | direct |
| SByte | Int16 | lossless widening |
| Byte | Int16 | lossless widening |
| Int16 | Int16 | direct |
| UInt16 | Int32 | lossless widening |
| Int32 | Int32 | direct |
| UInt32 | Int64 | lossless widening |
| Int64 | Int64 | direct |
| UInt64 | unsupported initially | cannot fit full range in signed Int64 |
| Float | Float | direct |
| Double | Double | direct |
| String | String | direct |
| DateTime | DateTime | direct |
| Enumeration subtype | Enum | preserve numeric value plus label metadata when available |
| ByteString | unsupported initially | requires explicit binary TAG/value contract |
| Guid | unsupported initially | do not silently convert semantic type to String |
| LocalizedText | warning/unsupported initially | String projection loses locale |
| QualifiedName | unsupported initially | String projection loses namespace identity |
| NodeId/ExpandedNodeId | unsupported initially | protocol identity is not a scalar process value |
| arrays/matrices | unsupported initially | current EliteSCADA TAG type model is scalar |
| Structures/ExtensionObject | unsupported initially | future ComplexTypes slice |
| Variant/DataValue/DiagnosticInfo | unsupported as process TAG type | wrapper/diagnostic semantics require explicit contract |

For any custom subtype, resolve the DataType hierarchy before mapping. Unknown custom structures should use the official ComplexTypes subsystem in a later explicit slice, not reflection/stringification hacks.

## Read/write capability mapping

Engineering should inspect both server-level capability (`AccessLevel`/`AccessLevelEx`) and effective user capability (`UserAccessLevel`). A TAG candidate is proposed writable only when the selected Engineering identity is allowed to write and the server reports a writable Value.

This is still only Engineering evidence. Runtime writes must validate the live server response and continue through normal EliteSCADA authorization/Audit boundaries.

A read-only node cannot be made writable merely by editing a checkbox in the browser.

## Subscription profiles

OPC UA Part 4 separates Subscription publishing interval from MonitoredItem sampling interval, filter and queue behavior. EliteSCADA should expose reusable public profiles rather than one opaque SDK object per TAG.

Proposed first profiles:

| Profile | Requested publishing | Sampling | Queue | Notes |
| --- | ---: | ---: | ---: | --- |
| Fast | 250 ms | inherit/request 250 ms | 1 | UI/process points requiring faster updates |
| Normal | 1000 ms | inherit | 1 | default import profile |
| Slow | 5000 ms | inherit | 1 | slow/status points |

Additional per-item options may include absolute/percent deadband where the server and type support it, queue size and discard-oldest policy.

The server is allowed to revise requested publishing/sampling parameters. Diagnostics should retain both requested and revised/effective values.

Large imports must respect server OperationLimits and monitored-item limits. Partitioning into multiple server subscriptions is an implementation detail and must not change TAG identity or public Engineering semantics. The official 2.0 stack already contains newer subscription-management/partitioning work that should be evaluated rather than reimplemented blindly.

## Candidate -> Preview -> Apply proposal

Discovery/browser DTOs are transient. A candidate should contain enough information for deterministic canonical TAG creation without becoming a second Engineering model.

Suggested candidate fields:

```text
candidateId (temporary)
serverApplicationUri
nodeId
namespaceUri
browsePath
browseName
displayName
description
dataTypeId
valueRank
access/userAccess
historizing
engineeringUnits/range (optional)
proposedTagPath
proposedTagDataType
proposedReadOnly
subscriptionProfileKey
issues[]
```

Workflow:

```text
discover -> inspect endpoint/trust -> temporary test session
         -> lazy browse/search -> select nodes/subtree
         -> build transient candidates
         -> OPC-UA-specific validation/mapping
         -> convert to canonical Engineering change proposal
         -> standard Preview + merge semantics
         -> standard Apply
```

OPC-UA-specific code must stop at candidate/binding translation. The actual project mutation remains the shared Engineering subsystem.

## Security and secret handling

Future public Data Source configuration may contain technical non-secret settings such as endpoint URL, expected ApplicationUri, SecurityMode, SecurityPolicy, certificate reference/thumbprint policy, timeouts and subscription defaults.

It must not contain plaintext:

- usernames/passwords;
- private keys;
- private-key passwords;
- bearer/access tokens;
- resolved secret values.

Credentials are referenced through the existing Engineering secret-reference model and resolved only by a trusted backend boundary.

Discovery/browse APIs are protected Engineering/system operations because they reveal plant topology, endpoints, certificates and address-space content.

## Test-server and interoperability strategy

### Deterministic CI fixture

Primary recommendation: pin an **OPC Foundation Console Reference Server** container/release for automated integration tests. The official stack documents a Reference Server container and the reference server is used for conformance-oriented testing.

CI scenarios should construct deterministic namespaces/nodes covering:

- scalar built-in types;
- writable and read-only Variables;
- namespace index changes between server restarts/configurations;
- large folders requiring BrowseNext;
- duplicate BrowseName/ambiguous path cases;
- arrays/custom structure candidates;
- subscription reconnect;
- certificate trust/rejection;
- username/password identity;
- changed certificate/application identity;
- missing/renamed nodes.

### Independent interoperability fixture

Use at least one implementation built on a different SDK. **Prosys OPC UA Simulation Server** is a practical manual/nightly candidate: its current 2026.1.2 product is cross-platform, supports OPC UA 1.05 and earlier, common secure policies, Data Access/Alarms/Historical Access, and a free edition.

This independent server is valuable because tests against only the OPC Foundation client and server stack can hide shared implementation assumptions.

### Certification testing

OPC Foundation CTT should be treated as a later compliance/certification tool, not a normal redistributable CI dependency. Current OPC Foundation terms restrict CTT availability/use and it should not be assumed available to public/ordinary CI runners.

## Later production implementation breakdown

The following order is a research recommendation for when the roadmap gate eventually permits OPC UA production work:

1. **SDK/package integration spike**: pin reviewed official client package(s), license/SBOM/security review, thin adapter tests only.
2. **Protected Engineering discovery service**: FindServers/GetEndpoints/LDS/LDS-ME/mDNS and bounded fallback probing; no Active Runtime registration yet.
3. **PKI/trust service integration**: certificate stores, pending/rejected certificates, explicit trust/reconcile operations and Audit.
4. **Temporary Engineering session + lazy browser**: Browse/BrowseNext, attributes, search limits, cancellation and candidate selection.
5. **Canonical import integration**: versioned OPC UA Data Source schema/binding, Preview/Apply, revisions, `.escadapkg`, secret references and migration tests.
6. **Runtime OPC UA Source Provider/driver**: session/reconnect, native subscriptions/monitored items, timestamps/quality, read/write through normal TAG boundaries.
7. **Common diagnostics integration**: Data Source state plus OPC-UA-specific session/subscription/keepalive/StatusCode details.
8. **Engineering UI**: Add Data Source workflow, endpoint inspector, certificate approval, Browse/Import, Rescan and Refresh Node IDs.
9. **Interoperability hardening**: Reference Server CI, Prosys/manual matrix, representative industrial vendor servers, fault injection and soak/reconnect tests.
10. **Optional later capabilities**: complex structures, Historical Access, Methods, Reverse Connect/GDS features only through separate explicit product slices.

Production steps 1-10 remain blocked until the roadmap external-protocol gate opens.

## Risks to carry forward

- Huge address spaces make uncontrolled recursive browse unacceptable.
- mDNS/LDS-ME may be disabled or unavailable on industrial networks; manual endpoint entry remains mandatory.
- NamespaceIndex is not stable identity.
- BrowsePath can be ambiguous; name-only rebind is unsafe.
- Servers may revise subscription/sampling parameters and impose lower OperationLimits than requested.
- Server certificates and ApplicationUri validation require deliberate PKI UX; auto-trust is unsafe.
- Custom Structures/ExtensionObjects need explicit ComplexTypes support and cannot be squeezed into scalar TAGs.
- OPC UA security policies and SDK releases evolve; exact dependency/security policy must be re-reviewed at implementation time.
- A server being reachable during Engineering discovery is not evidence that an Active Runtime Data Source is healthy.

## Sources reviewed

### OPC Foundation

- OPC Foundation UA .NET Standard repository/readme: https://github.com/OPCFoundation/UA-.NETStandard
- OPC Foundation UA .NET Standard license: https://github.com/OPCFoundation/UA-.NETStandard/blob/master/LICENSE.txt
- Developer Guide / packages and .NET support: https://github.com/OPCFoundation/UA-.NETStandard/blob/master/docs/DeveloperGuide.md
- Subscription APIs: https://github.com/OPCFoundation/UA-.NETStandard/blob/master/docs/Subscriptions.md
- Official online specification index: https://reference.opcfoundation.org/
- OPC UA Part 12 discovery overview: https://reference.opcfoundation.org/specs/OPC-10000-12/5.1
- OPC UA Part 12 client discovery process: https://reference.opcfoundation.org/specs/OPC-10000-12/4.3
- OPC UA Part 4 BrowseNext: https://reference.opcfoundation.org/specs/OPC-10000-4/5.9.3
- OPC UA Part 4 TranslateBrowsePathsToNodeIds: https://reference.opcfoundation.org/specs/OPC-10000-4/5.9.4
- OPC UA Part 4 MonitoredItem model: https://reference.opcfoundation.org/specs/OPC-10000-4/5.13.1
- OPC UA Part 3 Variables/access attributes: https://reference.opcfoundation.org/specs/OPC-10000-3/5.6
- OPC Foundation Compliance Test Tool information: https://opcfoundation.org/developer-tools/certification-test-tools/opc-ua-compliance-test-tool-uactt/

### Elipse Software

- OPC UA Driver: https://docs.elipse.com.br/documents/en-us/e3/latest/manual/e3/manual_driver_opcuadriver.html
- OPC UA Driver configuration/server selection: https://docs.elipse.com.br/documents/pt-br/e3/v6.7.236/manual/e3/manual_driver_opcuadriver_config.html
- OPC UA configuration / server information / Update IDs: https://docs.elipse.com.br/documents/en-us/e3/latest/manual/e3/manual_driver_opcuadriver_config.html
- OPC UA Subscription: https://docs.elipse.com.br/documents/en-us/e3/latest/manual/e3/manual_driver_opcuadriver_uasubscription.html
- OPC UA certificates: https://docs.elipse.com.br/documents/en-us/e3/latest/manual/e3/manual_driver_opcuadriver_certificate.html
- OPC UA datatype/path limitations: https://docs.elipse.com.br/documents/en-us/e3/latest/manual/e3/manual_driver_opcuadriver_limitation_data_type.html

### Independent interoperability server

- Prosys OPC UA Simulation Server: https://prosysopc.com/products/opc-ua-simulation-server/

## Final research conclusion

The locked EliteSCADA OPC UA UX is technically feasible without creating a private protocol stack. The official OPC Foundation .NET client provides the required discovery/session/browse/subscription/PKI primitives, while the public OPC UA services provide a clean basis for portable Node identity and bounded browsing. Elipse demonstrates that server selection, bulk import, subscription grouping and NodeId refresh are valuable industrial Engineering conveniences; EliteSCADA should preserve those conveniences but enforce stronger certificate trust, secret handling, bounded discovery/search, canonical Preview/Apply and fail-closed node reconciliation.

This branch is research only. **No OPC UA production driver, Data Source registration, runtime networking or package dependency is implemented here.**
