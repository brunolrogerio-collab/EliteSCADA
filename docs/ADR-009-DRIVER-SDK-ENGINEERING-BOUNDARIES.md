# ADR-009 — Driver SDK runtime and Engineering boundaries

## Status

Accepted.

## Context

Merged research for MQTT, OPC UA, BACnet/IP + BACnet/SC, Siemens S7 ISO Connection and Allen-Bradley Logix showed that the early Driver SDK abstraction is sufficient for the current Modbus runtime but does not fully express the needs of heterogeneous future protocols.

In particular, protocols differ substantially in how they acquire values and how Engineering obtains configuration evidence:

- Modbus and classic S7 are primarily polling-oriented;
- OPC UA is subscription/browse-oriented;
- MQTT is event-driven and has no standard address-space browser;
- BACnet may combine polling and COV while sharing lower-level network transport among logical devices;
- Allen-Bradley Logix has symbolic online browse and project-file import semantics.

Putting all of those behaviors directly on an active runtime `ICommunicationDriver` would couple Engineering tools to runtime lifetime and would force protocols to implement capabilities they do not actually possess.

## Decision

### 1. Keep the active runtime boundary small

`ICommunicationDriver` remains the active communication-runtime contract for lifecycle, TAG read/write capability and current runtime status.

Runtime acquisition implementation is protocol-owned but values always enter the common EliteSCADA TAG/cache/event pipeline. Protocol libraries and their subscription/session objects never become Core or canonical Engineering types.

### 2. Separate Engineering capabilities from runtime capabilities

Connection testing, discovery, browse, file import and reconciliation are protected Engineering operations, not mandatory active-runtime methods.

The Driver SDK therefore exposes capability-specific Engineering interfaces:

- `ICommunicationDriverConnectionTester`;
- `ICommunicationDriverDiscoverySource`;
- `ICommunicationDriverBrowser`;
- `ICommunicationDriverFileImporter`;
- `ICommunicationDriverReconciler`.

Each inherits the common `ICommunicationDriverDescriptorProvider` descriptor surface.

A Driver type implements only the Engineering interfaces it genuinely supports. MQTT does not need to manufacture a fake OPC-UA-style browse tree. A protocol that supports file import without network discovery can expose exactly that capability.

### 3. Every Driver type declares a versioned public descriptor

`CommunicationDriverTypeDescriptor` describes:

- stable Driver type identity;
- Driver SDK contract version;
- runtime capabilities;
- Engineering capabilities;
- supported acquisition modes;
- a versioned EliteSCADA-owned Data Source/TAG-binding configuration schema;
- whether compatible Data Sources may share lower-level transport infrastructure.

The descriptor is metadata consumed by the host and Engineering. It is not a second persistence database and does not replace canonical Engineering.

### 4. Driver configuration remains library-independent

A protocol module may use MQTTnet, OPC Foundation libraries, a BACnet stack, S7.NetPlus, libplctag or another implementation internally. Its public Data Source and TAG-binding schema must use EliteSCADA-owned stable fields and identities.

Library handles, subscription objects, browse instance IDs and transient session indexes are runtime/import caches only unless the protocol itself defines them as portable durable identity.

### 5. Discovery/browse/import results are transient candidates

Engineering adapters return bounded, sanitized discovery/browse/import evidence. Results do not modify Working Engineering directly.

The mutation path remains:

`candidate -> validate -> preview -> choose merge mode -> apply`

Partial discovery/browse operations must identify themselves as partial. Secret values are never returned in candidate models.

### 6. Protocol acquisition mode does not change the TAG pipeline

The SDK recognizes high-level acquisition modes:

- Polling;
- Subscription;
- EventDriven;
- Hybrid.

These are capability/diagnostic descriptors, not alternate data paths. All accepted process values still become normal EliteSCADA TAG values.

### 7. Preserve distinct timestamps when protocols provide them

`TagValue.Timestamp` remains the local EliteSCADA observation/publication time.

The public value contract may additionally preserve:

- `SourceTimestamp`, when the originating device/application supplies measurement time;
- `ServerTimestamp`, when an intermediary protocol server supplies a distinct server time.

Protocols without such timestamps leave them null. Drivers must not fabricate source/server timestamps from local receipt time.

### 8. Quality remains EliteSCADA-owned

Protocol delivery/connection status is evidence used to derive quality, not the quality model itself.

Examples:

- MQTT QoS does not equal TAG quality;
- an open TCP/session does not make all points Good;
- OPC UA StatusCode and BACnet reliability/status semantics require deliberate mapping;
- address/type faults may remain point-level when the protocol permits independent healthy points.

### 9. Writes remain through the owning-provider boundary

Runtime writes, operational commands and TAG Gateway destinations resolve the TAG's active owning provider/driver and delegate through the common write boundary.

There is no protocol-pair Gateway API and no frontend/script direct driver API.

### 10. Detailed research convergence is normative architecture input

`docs/DRIVER-SDK-RESEARCH-CONVERGENCE.md` records the protocol-by-protocol consequences of this ADR and must be read before implementing a new external Driver type or Driver Module.

## Consequences

- Future Driver implementations can differ honestly without fragmenting the platform model.
- Engineering tooling no longer needs an Active Runtime driver just to inspect a device/server/broker.
- Installable Driver Modules have a concrete descriptor/schema direction before the module loader is implemented.
- Canonical Engineering Schema v9 is not changed by this ADR; a future deliberate migration will introduce richer protocol-owned TAG bindings when scheduled.
- Existing Modbus runtime behavior remains compatible.
- No new protocol is implemented or activated by this ADR.
