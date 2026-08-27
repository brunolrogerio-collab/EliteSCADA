# Driver SDK research convergence

Status: **ARCHITECTURE ALIGNMENT / NO NEW PROTOCOL IMPLEMENTED**

This document consolidates the merged research for MQTT, OPC UA, BACnet/IP + BACnet/SC, Siemens S7 ISO Connection and Allen-Bradley Logix EtherNet/IP/CIP into common EliteSCADA Driver SDK rules. It also cross-checks the Client Python and graphical-editor research where those boundaries touch drivers, Engineering and runtime authority.

The purpose is to make later protocol implementation additive rather than forcing each driver to redesign the platform around its library.

## 1. Converged architecture

The stable flow remains:

`Canonical Engineering -> DriverHost/compiler -> one or more Data Source runtime instances -> TAG current cache/Event Bus -> Historian/Alarms/Realtime/Gateway`

Three public concepts remain distinct:

- **Driver type**: implementation capability and versioned public schema;
- **Data Source**: one configured communication/device/session context;
- **TAG**: one canonical point owned by one Data Source, with a portable protocol-specific binding.

Concrete library types from MQTTnet, OPC Foundation, BACnet libraries, S7.NetPlus, libplctag or another stack never become canonical project truth.

## 2. Why Runtime and Engineering contracts are separate

All protocol research converged on a distinction the early SDK only implied:

- Active Runtime needs lifecycle, acquisition, read/write, quality and diagnostics.
- Engineering needs connection tests, discovery, browse/observe/import, candidate inspection and reconciliation.

Discovery and browse are temporary evidence. They may use short-lived protected protocol sessions but must not require an Active Runtime driver and must never auto-create canonical TAGs/Data Sources.

Therefore:

- `ICommunicationDriver` remains the active runtime boundary.
- `ICommunicationDriverEngineeringAdapter` is the optional protected Engineering tooling boundary.
- `DriverEngineeringCapabilities` advertises ConnectionTest/Discover/Browse/FileImport/Reconcile separately from runtime capabilities.
- early `DriverCapabilities.Browse/Discover` flags remain only for compatibility and are not the preferred future integration path.

## 3. Acquisition modes

Future drivers must be allowed to acquire values according to protocol semantics without changing the TAG pipeline.

Supported public acquisition categories are:

- `Polling` — Modbus, S7 and initial Allen-Bradley style scheduled reads;
- `Subscription` — OPC UA monitored items and BACnet COV where supported;
- `EventDriven` — raw MQTT broker messages;
- `Hybrid` — BACnet COV + polling fallback or another protocol mixing modes.

The acquisition mechanism is internal to the driver/runtime adapter. All resulting values still enter EliteSCADA through the common TAG/cache/event path.

No protocol SDK subscription object becomes a public Engineering entity. Public profiles may describe requested scan/publish/subscription behavior, while library objects remain replaceable runtime implementation details.

## 4. Data Source identity by protocol

The common Data Source boundary is stable even though protocol identity differs.

| Driver family | One Data Source represents | Durable identity direction |
| --- | --- | --- |
| Modbus TCP | one configured TCP/device context | endpoint + configured Unit/address context |
| MQTT raw | one broker/session identity | broker endpoint + explicit client/session profile |
| OPC UA | one configured server/application session context | ApplicationUri + endpoint/security identity |
| BACnet | one target BACnet Device Instance | Device Instance; network address is resolution metadata |
| Siemens S7 ISO | one PLC communication context | host + explicit Rack/Slot/TSAP profile |
| Allen-Bradley Logix | one controller communication context | controller identity + ordered CIP route + symbolic namespace |

A protocol may share lower-level transport infrastructure among Data Sources when safe, for example a BACnet/IP UDP network adapter. Logical Data Source health, counters, TAG quality and write ownership remain isolated even when transport is shared.

## 5. Portable TAG binding rule

Canonical TAG bindings must preserve protocol meaning, not transient runtime handles.

Examples from merged research:

- OPC UA: server ApplicationUri + namespace URI + namespace-aware BrowsePath + last resolved NodeId;
- BACnet: Object Identifier + Property Identifier + optional array index, cross-checked against Device Instance;
- Allen-Bradley Logix: controller/program scope + symbolic path/member/index; Symbol/Template instance IDs are caches only;
- S7 ISO: typed absolute I/Q/M/DB address and connection-profile semantics; optimized symbolic-only DB members are not assigned guessed offsets;
- MQTT: exact topic/filter mapping plus deterministic scalar/JSON/binary extraction policy; observed wildcard traffic never creates canonical TAGs automatically.

The current canonical `TagEngineeringDto.Address` is sufficient for existing Modbus but is not considered the final rich binding contract for all protocols. A future schema migration must introduce a versioned protocol-owned binding representation while preserving backward compatibility and import/export.

Until that schema migration is deliberately scheduled, new driver abstractions must not make a library-specific object or ad-hoc browser cache the hidden replacement for `Address`.

## 6. Public driver configuration schema

Every Driver type/module must publish an EliteSCADA-owned, versioned schema describing:

- Data Source configuration fields;
- TAG-binding fields;
- field types and required/optional status;
- safe defaults and limits;
- allowed enum/profile values;
- which values are secret/certificate references rather than plaintext values.

`DriverConfigurationSchemaDescriptor` is the first code-level foundation for that contract.

The schema descriptor is metadata, not a second persistence store. Canonical Engineering remains authoritative and will later consume the descriptor for validation, UI generation, import/export and module compatibility/migration.

## 7. Discovery, browse and import

The research shows that discovery cannot be one generic “scan network” button.

Protocol adapters therefore own bounded Engineering behavior behind the same high-level contract:

- OPC UA: manual URL, FindServers/GetEndpoints, LDS/LDS-ME/mDNS, lazy Browse/BrowseNext;
- BACnet: Who-Is/I-Am and device/object browse, with BBMD/FDR topology respected;
- MQTT: manual mapping plus bounded Observe Topics/topic-template candidate collection, not fake address-space browse;
- Allen-Bradley: online Symbol/Template browse and file-based L5X/L5K import;
- Siemens S7: connection test plus Engineering-side TIA Openness/export import; no mandatory runtime TIA dependency.

All results are transient `DriverDiscoveryCandidate`/`DriverBrowseNode` evidence. Canonical mutation remains:

`candidate -> validate -> preview -> choose merge mode -> apply`

Partial/capped results must be labelled partial rather than pretending a complete device/server scan occurred.

## 8. Connection test versus Active Runtime

A connection test is a protected Engineering operation and may create a short-lived protocol session. It must not activate a project revision or silently replace a running Data Source.

A test result may expose only sanitized, non-secret facts such as:

- endpoint/device identity;
- observed controller/server/broker identity;
- effective route/TSAP/security/profile;
- negotiated PDU/APDU/session limits;
- compatibility and certificate/trust issues;
- supported services/profile evidence.

Resolved passwords/private keys/tokens never cross into canonical Engineering, diagnostics or browser-visible result models.

## 9. Security convergence

Across MQTT TLS/mTLS, OPC UA certificates/user tokens, BACnet/SC certificates, CIP Security and future secure modules, the same rules apply:

- Engineering stores secret/certificate references, not secret material;
- unknown/changed secure endpoint identity fails closed unless an explicit protected trust/reconciliation action is performed;
- no permanent “accept any certificate” production mode;
- no silent downgrade from a configured secure protocol/profile to an insecure one;
- trust/configuration changes are auditable administrative/Engineering actions;
- protocol diagnostics remain sanitized.

The exact secret resolver/trust-store runtime API is a future security-host contract. It must be host-owned so plugin code receives only the minimum resolved credential material required for its connection and cannot enumerate unrelated project secrets.

## 10. TAG timestamps and quality

The protocol research exposed a cross-protocol timestamp requirement.

`TagValue.Timestamp` remains the local EliteSCADA observation/publication timestamp. The common value contract now also permits:

- `SourceTimestamp` — measurement/origin time supplied by the device/application;
- `ServerTimestamp` — intermediary/server time when a protocol exposes it separately, notably OPC UA.

Protocols without these timestamps leave them null. They must never fabricate source time from local receipt time merely to populate a field.

Quality remains an EliteSCADA semantic, not a transport flag:

- MQTT QoS is not TAG quality;
- TCP/session connected does not make all TAGs Good;
- BACnet Status_Flags/Reliability/Out_Of_Service contribute to mapping but do not replace EliteSCADA quality;
- OPC UA StatusCode must be mapped deliberately;
- one invalid address/type can remain a point-level fault without poisoning independent good points when protocol semantics permit.

## 11. Read/write convergence

All writable drivers participate through the normal owning-provider write boundary used by Runtime and Gateway.

Protocol-specific write semantics remain adapter-owned and explicit:

- MQTT publishes to configured write topics with deliberate retain/QoS policy;
- BACnet command priority/relinquish is Engineering configuration, never an invisible stack default;
- OPC UA writeability uses both configured intent and live effective access evidence;
- S7 classic writes require compatible absolute address/type and CPU permissions;
- Allen-Bradley writes fail closed on External Access/constant/safety/type ambiguity.

The Gateway never calls a concrete driver and never needs pairwise protocol code.

## 12. Diagnostics convergence

`CommunicationDriverDiagnosticSnapshot` remains the common authority for external communication Data Sources.

Protocol details may extend sanitized metadata, but drivers must not invent metrics that do not exist:

- MQTT event-driven sources may have no configured scan or scan duration;
- subscription protocols should expose requested/effective subscription behavior through protocol details where useful;
- BACnet may expose COV lease/renewal or BBMD/FDR state;
- S7 may expose effective TSAP/negotiated PDU;
- Allen-Bradley may expose route/messaging mode;
- OPC UA may expose endpoint/security/session/subscription state without leaking certificates/private material.

Shared transport failures may affect multiple Data Sources, but each Data Source still reports its own health/quality/counters.

## 13. Installable module implications

Future module loading must discover a descriptor/factory pair rather than infer capabilities from arbitrary reflection or library types.

A module must eventually expose:

- stable module and Driver type identity;
- Driver SDK contract compatibility range;
- `CommunicationDriverTypeDescriptor` for every Driver type it provides;
- versioned configuration schema;
- runtime factory/adapter registration;
- optional Engineering adapter registration;
- configuration migration hooks;
- integrity/publisher metadata.

Missing or incompatible modules do not destroy canonical Engineering. They produce explicit validation/activation diagnostics.

## 14. Cross-check with Python and visual research

The Python and visual research reinforce the same authority boundary:

- scripts never receive direct driver APIs;
- Client Visual Python uses a narrow versioned EliteSCADA facade and normal backend authorization;
- graphical editor state remains a projection of canonical Engineering, not renderer JSON;
- future driver browsers/importers should feed reusable Engineering workspace primitives, not protocol-specific private stores in React;
- visual bindings refer to canonical TAG identity, not device addresses.

This means Driver SDK work, scripting work and visual-editor work can evolve independently as long as all three meet at canonical Engineering/TAG/runtime contracts.

## 15. Deliberately deferred after this convergence

This alignment does **not** authorize:

- MQTT/OPC UA/BACnet/S7/Allen-Bradley production runtime;
- final rich canonical protocol TAG-binding DTO/schema migration;
- secret resolver/trust-store implementation;
- Driver Module loader/package/signing implementation;
- final runtime driver factory/DI/module isolation model;
- protocol library selection;
- Python runtime/editor or graphical Screen/Popup/Dynamo editor.

Those remain scheduled product slices. The purpose of this convergence is that, when they begin, their contracts already point in the same direction.
