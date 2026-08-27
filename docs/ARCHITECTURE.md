# EliteSCADA — Architecture Baseline v0.1

## Principle
The SCADA Core must not depend on a specific PLC protocol, database engine, protocol library, or UI technology.

The Engineering model is authoritative. Runtime, editor, import/export, reusable libraries, Driver Modules and administrative tools consume the same public, versioned model rather than maintaining private representations of project configuration.

## Main boundaries

- `Scada.Core`: TAG model, quality, current-value cache, domain events and core access semantics.
- `Scada.Api`: REST/WebSocket boundary for clients and integrations; backend authorization is authoritative.
- `Scada.Historian`: persistence of time-series values and retention policies.
- `Scada.Drivers`: runtime communication contracts plus protocol-neutral Driver descriptor/Engineering capability contracts.
- `Scada.DriverHost`: host/compiler/runtime composition for communication Data Sources.
- `Scada.Engineering`: public versioned Engineering contracts, import/export, validation, reusable assets and project composition.
- `scada-web`: browser Runtime and Engineering UX; it never accesses industrial drivers directly.
- future Client/Server Python engines: sandboxed adapters behind public scripting contracts.
- future Driver Module/SDK packaging: controlled extension boundary, not an escape from canonical Engineering.

## Mandatory runtime data flow

`Canonical Engineering -> DriverHost/compiler -> Data Source/source provider -> TAG Current Cache -> Event Bus -> Historian / Alarm Engine / Realtime / Gateway / Server Scripts`

No frontend component or Client Visual Script may access a driver directly. No concrete driver may call another concrete driver. Protocol-independent transfer uses TAG/Gateway boundaries.

## Driver type, Data Source and TAG

These concepts remain distinct:

- **Driver type** identifies one protocol/runtime implementation and its versioned public configuration schema.
- **Data Source** is one concrete configured communication/device/session context. Multiple Data Sources may use the same Driver type simultaneously.
- **TAG** is one canonical point owned by one Data Source/source provider and carries a portable protocol-specific binding.

Failure, counters and quality are isolated per logical Data Source as far as the protocol permits. Protocols may share lower-level transport infrastructure internally, but shared sockets/session managers never erase Data Source identity.

## Driver SDK: Runtime versus Engineering

Active communication and Engineering inspection are intentionally separate surfaces.

Runtime:

- `ICommunicationDriver` owns lifecycle and active read/write behavior;
- acquisition may be polling, subscription, event-driven or hybrid;
- `ICommunicationDiagnosticsSource` provides common protected communication diagnostics where meaningful.

Engineering:

- Driver descriptors expose stable capabilities and versioned configuration schemas;
- connection test, discovery, browse, file import and reconciliation are independent optional interfaces;
- Engineering tooling may use short-lived protected protocol sessions without activating a project Runtime;
- discovery/browse/import output is transient candidate evidence, never project truth.

Canonical mutation always returns to:

`candidate -> validate -> preview -> choose merge mode -> apply`

See `docs/ADR-009-DRIVER-SDK-ENGINEERING-BOUNDARIES.md` and `docs/DRIVER-SDK-RESEARCH-CONVERGENCE.md`.

## Tag state

A runtime TAG value contains:

- value;
- local EliteSCADA observation/publication `Timestamp`;
- `TagQuality`;
- source/provider identity;
- optional protocol/device `SourceTimestamp`;
- optional distinct protocol-server `ServerTimestamp`.

Protocols that do not provide source/server time leave those fields null. They do not fabricate remote timestamps from local receipt time.

Quality is EliteSCADA-owned. Transport/session state, MQTT QoS, OPC UA StatusCode, BACnet reliability/status and similar protocol evidence are mapped deliberately rather than becoming alternate quality models.

Current quality states:
GOOD, UNCERTAIN, BAD, BAD_COMM, BAD_CONFIG, BAD_DEVICE, STALE, DISABLED.

## Protocol-specific identities stay behind portable bindings

Canonical Engineering stores stable protocol meaning, not implementation-library handles.

Examples include namespace-aware OPC UA identity, BACnet object/property identity, Logix symbolic paths, typed S7 absolute addresses and deterministic MQTT topic/payload mappings. Browse/session indexes and library handles are transient caches unless the protocol itself defines a durable portable identity.

Current Schema v9 remains authoritative. Richer protocol-binding representation will be introduced by a deliberate future schema migration rather than by hidden per-driver state.

## Secrets and secure protocol configuration

Passwords, tokens, private keys and other secret material do not belong in canonical Engineering packages.

Driver configuration stores protected references. Future host-owned security infrastructure resolves only the material required by a driver/Engineering session. Secure protocols fail closed on unknown/changed identity or incompatible security policy and never silently downgrade merely for convenience.

## Reusable Engineering and application composition

EliteSCADA is designed around reusable project structures rather than isolated screen copies.

- Equipment templates and equipment instances represent reusable data/process structures.
- Dynamos and visual definitions represent reusable graphical structures.
- Reusable definitions are versioned and portable through Engineering Import/Export.
- Cross-project copy/paste is implemented conceptually as a validated Engineering Fragment with dependency handling, never as browser-only opaque clipboard state.
- Screens may be composed inside a configurable application shell with persistent header, footer, alarm and navigation regions.

Graphical-editor state remains a projection of canonical Engineering. Renderer JSON/object trees are never project authority. Visual bindings refer to canonical TAG identity, not PLC/device addresses.

## Scripting boundary

Client Visual and Server Python remain separate scopes. Scripts consume a narrow versioned EliteSCADA API and never receive direct driver, database, filesystem, secret or renderer-internal access.

Client Visual scripts act under the logged-in user's normal backend authorization and manipulate only permitted Runtime/visual surfaces. Server scripting is a separate future security/runtime design.

## Trends

Trend charts are first-class Engineering/Runtime objects. A trend contains Pens whose sources may be historical TAG data or live/runtime bindings. Historical query semantics remain separate from realtime subscriptions even when both are rendered in one chart.

TimescaleDB is the intended historical backend, but trend Engineering definitions must not depend directly on TimescaleDB-specific storage details.

## Access model

Security is role/capability based and configurable by application. Roles are not restricted to one hard-coded hierarchy.

The model distinguishes capabilities such as view, command execution, setpoint/process-value write, alarm acknowledgement, shelving, trend use, Engineering changes and administrative actions.

UI visibility may reflect access rules, including hiding screens or controls, but backend/API remains the actual security boundary and independently enforces protected operations.

## Time semantics

Visual date/time elements may use the EliteSCADA server clock or a TAG-provided time source such as a PLC clock acquired through a driver. Display formatting and timezone conversion are presentation concerns.

Active synchronization of PLC/RTU clocks is a separate future driver operation that must be explicit, permission controlled and auditable.

## Technology baseline
- Backend/Core: .NET 10 LTS
- Frontend: React 19.2 + TypeScript
- Configuration DB: PostgreSQL
- Historian: PostgreSQL + TimescaleDB
- Realtime UI: WebSocket
- Public integration: REST API
- Industrial messaging target: MQTT
- Scripting: Python through sandboxed Client/Server adapters
