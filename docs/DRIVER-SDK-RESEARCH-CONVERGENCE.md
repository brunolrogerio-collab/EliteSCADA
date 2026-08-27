# Driver SDK research convergence

Status: **ARCHITECTURE ALIGNMENT / NO NEW PROTOCOL IMPLEMENTED**

This document consolidates the merged research for MQTT, OPC UA, BACnet/IP + BACnet/SC, Siemens S7 ISO Connection and Allen-Bradley Logix EtherNet/IP/CIP into common EliteSCADA Driver SDK rules. It also cross-checks the Client Python and graphical-editor research where those boundaries touch drivers, Engineering and runtime authority.

The purpose is to make later protocol implementation additive rather than forcing each driver to redesign the platform around its library.

## 1. Converged architecture

The stable flow remains:

`Canonical Engineering -> DriverHost/compiler -> Data Source runtime instances -> TAG current cache/Event Bus -> Historian/Alarms/Realtime/Gateway`

Three public concepts remain distinct:

- **Driver type**: implementation capability and versioned public schema;
- **Data Source**: one configured communication/device/session context;
- **TAG**: one canonical point owned by one Data Source, with a portable protocol-specific binding.

Concrete library types from MQTTnet, OPC Foundation, BACnet libraries, S7.NetPlus, libplctag or another stack never become canonical project truth.

## 2. Runtime and Engineering are separate Driver SDK surfaces

All protocol research converged on a distinction the early SDK only implied:

- Active Runtime needs lifecycle, acquisition, read/write, quality and diagnostics.
- Engineering needs connection tests, discovery, browse/observe/import, candidate inspection and reconciliation.

Discovery and browse are temporary evidence. They may use short-lived protected protocol sessions but must not require an Active Runtime driver and must never auto-create canonical TAGs/Data Sources.

Therefore:

- `ICommunicationDriver` remains the active runtime boundary.
- `ICommunicationDriverDescriptorProvider` exposes stable Driver type metadata.
- `ICommunicationDriverConnectionTester`, `ICommunicationDriverDiscoverySource`, `ICommunicationDriverBrowser`, `ICommunicationDriverFileImporter` and `ICommunicationDriverReconciler` are independent optional Engineering capabilities.
- `DriverEngineeringCapabilities` advertises those features separately from runtime capabilities.
- early `DriverCapabilities.Browse/Discover` flags remain for compatibility and are not the preferred future integration path.

This split is deliberate. MQTT does not have to pretend it exposes an OPC-UA-style address space, and a driver with project-file import does not need to invent network discovery merely to satisfy one oversized interface.

## 3. Acquisition modes

Future drivers may acquire values according to protocol semantics without changing the TAG pipeline:

- `Polling` — Modbus, S7 and initial Allen-Bradley scheduled reads;
- `Subscription` — OPC UA monitored items and BACnet COV where supported;
- `EventDriven` — raw MQTT broker messages;
- `Hybrid` — BACnet COV plus polling fallback or another protocol mixing modes.

The acquisition mechanism is internal to the driver/runtime adapter. All accepted values still enter EliteSCADA through the common TAG/cache/event path.

No protocol SDK subscription object becomes a public Engineering entity. Public profiles may describe requested scan/publish/subscription behavior while library objects remain replaceable runtime details.

## 4. Data Source identity by protocol

The common Data Source boundary is stable even though protocol identity differs.

| Driver family | One Data Source represents | Durable identity direction |
| --- | --- | --- |
| Modbus TCP | one configured TCP/device context | endpoint plus configured Unit/address context |
| MQTT raw | one broker/session identity | broker endpoint plus explicit client/session profile |
| OPC UA | one server/application communication context | ApplicationUri plus endpoint/security identity |
| BACnet | one target BACnet Device Instance | Device Instance; network address is resolution metadata |
| Siemens S7 ISO | one PLC communication context | host plus explicit Rack/Slot/TSAP profile |
| Allen-Bradley Logix | one controller communication context | controller identity plus ordered CIP route and symbolic namespace |

A protocol may share lower-level transport infrastructure among Data Sources when safe, for example a BACnet/IP UDP network adapter. Logical Data Source health, counters, TAG quality and write ownership remain isolated even when transport is shared.

## 5. Portable TAG binding rule

Canonical TAG bindings must preserve protocol meaning, not transient runtime handles.

Examples from merged research:

- OPC UA: server ApplicationUri + namespace URI + namespace-aware BrowsePath + last resolved NodeId;
- BACnet: Object Identifier + Property Identifier + optional array index, cross-checked against Device Instance;
- Allen-Bradley Logix: controller/program scope + symbolic path/member/index; Symbol/Template instance IDs are caches only;
- S7 ISO: typed absolute I/Q/M/DB address and connection-profile semantics; optimized symbolic-only DB members are not assigned guessed offsets;
- MQTT: exact topic/filter mapping plus deterministic scalar/JSON/binary extraction policy; observed wildcard traffic never creates canonical TAGs automatically.

The current canonical `TagEngineeringDto.Address` is sufficient for existing Modbus but is not the final rich binding contract for all protocols. A future deliberate schema migration must introduce a versioned protocol-owned binding representation while preserving backward compatibility, import/export, revisions and package round-trip.

Until then, no driver may make a library-specific object, session handle or browser cache the hidden replacement for canonical Engineering.

## 6. Public Driver configuration schema

Every Driver type/module must publish an EliteSCADA-owned, versioned schema describing:

- Data Source configuration fields;
- TAG-binding fields;
- field types and required/optional status;
- safe defaults and limits;
- allowed enum/profile values;
- secret/certificate references rather than plaintext secret values.

`DriverConfigurationSchemaDescriptor` is the first code-level foundation for that contract.

The descriptor is metadata, not persistence. Canonical Engineering remains authoritative and will later consume descriptors for validation, UI generation, import/export, module compatibility and migration.

## 7. Discovery, browse, import and reconciliation

There is intentionally no generic “scan network” implementation shared by all protocols. The host supplies common lifecycle, authorization, cancellation, result handling and Preview/Apply integration; the protocol adapter supplies bounded protocol semantics.

Examples:

- OPC UA: manual URL, FindServers/GetEndpoints, LDS/LDS-ME/mDNS, lazy Browse/BrowseNext and BrowsePath reconciliation;
- BACnet: Who-Is/I-Am and device/object browse while respecting BBMD/FDR topology;
- MQTT: manual mapping plus bounded Observe Topics/topic-template evidence, not fake address-space browse;
- Allen-Bradley: Symbol/Template online browse plus L5X/L5K import;
- Siemens S7: connection test plus Engineering-side TIA Openness/export import; production Runtime does not depend on TIA Portal.

Transient models are `DriverDiscoveryCandidate`, `DriverBrowseNode`, `DriverImportCandidate` and `DriverReconcileResult`. They never mutate canonical Engineering by themselves.

The mutation path remains:

`candidate -> validate -> preview -> choose merge mode -> apply`

Partial/capped results must be labelled partial rather than pretending the entire device/server/network was inspected.

## 8. Connection test is not Active Runtime

A connection test is a protected Engineering operation and may create a short-lived protocol session. It must not activate a project revision or silently replace a running Data Source.

A result may expose sanitized facts such as:

- endpoint/device identity;
- observed controller/server/broker identity;
- effective route/TSAP/security/profile;
- negotiated PDU/APDU/session limits;
- compatibility and certificate/trust issues;
- supported services/profile evidence.

Resolved passwords, private keys and tokens never cross into canonical Engineering, diagnostics or browser-visible result models.

## 9. Security convergence

Across MQTT TLS/mTLS, OPC UA certificates/user tokens, BACnet/SC certificates, CIP Security and future secure modules, the same rules apply:

- Engineering stores secret/certificate references, not secret material;
- unknown or changed secure endpoint identity fails closed until an explicit protected trust/reconciliation action;
- no permanent production “accept any certificate” mode;
- no silent downgrade from configured secure semantics to insecure communication;
- trust/configuration changes are auditable Engineering/system actions;
- protocol diagnostics remain sanitized.

The exact secret resolver/trust-store API is a future host security contract. It must be host-owned so plugin code receives only the minimum resolved credential material required for its own connection and cannot enumerate unrelated project secrets.

## 10. TAG timestamps and quality

The protocol research exposed a cross-protocol timestamp requirement.

`TagValue.Timestamp` remains the local EliteSCADA observation/publication timestamp. The common value contract additionally permits:

- `SourceTimestamp` — measurement/origin time supplied by the device/application;
- `ServerTimestamp` — intermediary/server time when exposed separately, notably by OPC UA.

Protocols without those timestamps leave them null. They must never fabricate source time from local receipt time merely to populate a field.

Quality remains an EliteSCADA semantic, not a transport flag:

- MQTT QoS is not TAG quality;
- TCP/session connected does not make every TAG Good;
- BACnet Status_Flags/Reliability/Out_Of_Service contribute to mapping but do not replace EliteSCADA quality;
- OPC UA StatusCode must be mapped deliberately;
- point-level address/type failures need not poison independent healthy points where protocol semantics permit isolation.

## 11. Read/write convergence

All writable drivers participate through the normal owning-provider write boundary used by Runtime and Gateway.

Protocol-specific semantics remain adapter-owned and explicit:

- MQTT publishes to configured write topics with deliberate retain/QoS policy;
- BACnet command priority/relinquish is Engineering configuration, never an invisible stack default;
- OPC UA writeability uses configured intent plus effective live access evidence;
- S7 classic writes require compatible absolute address/type and CPU permissions;
- Allen-Bradley writes fail closed on External Access/constant/safety/type ambiguity.

The Gateway never calls a concrete driver and never needs pairwise protocol code.

## 12. Diagnostics convergence

`CommunicationDriverDiagnosticSnapshot` remains the common authority for external communication Data Sources.

Protocol details may extend sanitized metadata, but drivers must not invent metrics that do not exist:

- MQTT event-driven sources may have no configured scan or scan duration;
- subscription protocols may expose requested/effective subscription behavior through protocol details;
- BACnet may expose COV lease/renewal or BBMD/FDR state;
- S7 may expose effective TSAP and negotiated PDU;
- Allen-Bradley may expose route and explicit-messaging mode;
- OPC UA may expose endpoint/security/session/subscription state without leaking secrets.

Shared transport failures may affect multiple Data Sources, but each logical Data Source still reports its own health, counters and TAG-quality aggregation.

## 13. Installable module implications

Future module loading should discover stable descriptors/factories rather than infer capabilities from arbitrary reflection or implementation-library types.

A module must eventually expose:

- stable module and Driver type identity;
- Driver SDK compatibility range;
- `CommunicationDriverTypeDescriptor` for every Driver type provided;
- versioned configuration schema;
- runtime factory/adapter registration;
- optional capability-specific Engineering adapters;
- configuration migration hooks;
- integrity/publisher metadata.

Missing or incompatible modules do not destroy canonical Engineering. They produce explicit validation/activation diagnostics.

## 14. Cross-check with Python and visual research

The Python and visual research reinforce the same authority boundary:

- scripts never receive direct driver APIs;
- Client Visual Python uses a narrow versioned EliteSCADA facade and normal backend authorization;
- graphical editor state is a projection of canonical Engineering, not renderer JSON;
- driver browsers/importers feed reusable Engineering workspace primitives rather than protocol-specific private React stores;
- visual bindings refer to canonical TAG identity, not PLC/device addresses.

Driver SDK, scripting and graphical editor can therefore evolve independently as long as all three meet at canonical Engineering/TAG/runtime contracts.

## 15. Deliberately deferred after this convergence

This alignment does **not** authorize:

- MQTT/OPC UA/BACnet/S7/Allen-Bradley production runtime;
- final rich canonical protocol TAG-binding DTO/schema migration;
- secret resolver/trust-store implementation;
- Driver Module loader/package/signing implementation;
- final runtime driver factory/DI/module isolation model;
- protocol library selection;
- Python runtime/editor or graphical Screen/Popup/Dynamo editor.

Those remain scheduled product slices. The purpose of this convergence is that, when they begin, their public contracts already point in the same direction.
