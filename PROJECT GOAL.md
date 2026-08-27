# PROJECT GOAL — EliteSCADA

> Persistent product north and continuity contract.
>
> This file preserves stable product goals and locked architecture across ChatGPT conversations, developers and tooling. It defines intent, not merely current implementation state.

**Last reviewed:** 2026-08-26

## Mandatory continuity protocol

1. At the beginning of every EliteSCADA task, read `PROJECT GOAL.md` and `LAST CHANGE.md` before planning or changing code.
2. If the user adds, removes or clarifies a stable product goal or architectural rule, update this file in the same task.
3. Before the final response of an EliteSCADA task, update `LAST CHANGE.md` with the actual repository state.
4. `LAST CHANGE.md` must distinguish **MERGED**, **IMPLEMENTED IN PR** and **SPECIFIED / NOT IMPLEMENTED**.
5. `docs/ROADMAP.md` must remain consistent with this product north.
6. Permanent architectural decisions must not exist only in a feature branch or chat history.
7. If conversation memory, documentation and repository disagree, inspect current `main`; repository state wins for what is implemented and this file wins for explicitly locked future product intent.

## Product mission

EliteSCADA is intended to become a serious industrial SCADA/supervisory platform, not merely a monitoring dashboard.

The product must support the complete application lifecycle:

- Engineering configuration;
- reusable equipment/classes, Dynamos and visual libraries;
- screens and popups;
- communication drivers and internal value sources;
- live runtime;
- commands, alarms and events;
- historian and trends;
- Python scripting;
- security, identity and audit;
- project revisioning, publication and activation;
- backup, restore, import/export and cross-project reuse;
- public contracts/SDKs and installable driver modules.

The long-term responsibility is comparable to established industrial supervisory platforms while retaining EliteSCADA's own architecture, data model and implementation.

## Authoritative Engineering model

The **public, versioned Engineering model is authoritative**.

Runtime, editor, persistence, import/export, reusable libraries, scripting references, administrative tools and future extensions consume the same model. No graphical editor, database schema, browser state, script runtime or driver-specific representation may silently become the only project truth.

The Core remains independent of a specific PLC/protocol, database implementation and frontend/rendering technology.

Shared/server runtime flow:

`Device/Server Source -> Driver / Source Provider -> TAG Engine / Current Cache -> Event Bus -> Historian / Alarm Engine / Realtime / Gateway / Server Scripts`

Client-local presentation flow may include Client Memory, visual-object runtime instances and Client Visual Scripts, but client-local state is never global process truth.

The frontend never accesses industrial drivers directly. Concrete drivers never call one another directly. Protocol-independent transfers happen through TAG/runtime boundaries.

## Technology baseline

Current direction:

- Backend/Core: .NET 10 LTS.
- Frontend: React + TypeScript.
- Engineering/persistence: PostgreSQL.
- Historian: PostgreSQL + TimescaleDB.
- Realtime client transport: WebSocket.
- Public integration: REST API.
- Scripting language: Python.
- Client visual scripting: sandboxed Python runtime, with exact browser/WASM implementation selected by technical spike.
- Server scripting: separately sandboxed Python host/runtime in a later server-scripting slice.
- External protocol expansion: MQTT, OPC UA, BACnet and installable modules including Siemens S7; later Allen-Bradley research.
- Extension direction: public SDK plus installable/versioned driver modules.

Implementation technologies may evolve deliberately, but public product contracts should remain decoupled from incidental frameworks.

## Mandatory Engineering Import/Export principle

Engineering Import/Export is a cross-cutting core capability, not a utility added after the GUI is complete.

Every relevant Engineering entity must be:

- serializable;
- versioned;
- importable/exportable through a public interface;
- usable without depending on the graphical editor;
- validated before application;
- compatible with the common preview/apply workflow.

Canonical technical representation is versioned JSON (`scada.engineering`). CSV is supported for appropriate bulk entities and XLSX is a future Engineering surface. Tabular formats do not replace the canonical model.

Mandatory import flow:

`parse -> validate -> preview -> choose merge mode -> apply`

Merge semantics include create-only, update-existing and create-and-update.

### Required Engineering domains

The public model covers or must evolve to cover:

1. **TAGs**: stable ID, path/name, type, unit, description, source/Data Source, address/binding where applicable, scaling, deadband, historian policy, access policy, typed memory initial value and metadata.
2. **Alarms**: TAG reference, type, limits/setpoint, priority, class, area, message, delay, ACK, shelving and metadata.
3. **Data Sources / Drivers / Internal Sources**: technical source configuration and protected secret references.
4. **Equipment Templates and Equipment instances**.
5. **Dynamos / reusable visual definitions** with typed public properties, bindings, script/event references and dependencies.
6. **Screens and Popups** with routes, visual-object trees, properties, bindings, assets and script/event references.
7. **Visual assets/resources** such as project images through stable IDs/references, not arbitrary filesystem paths.
8. **Python Scripts**: stable ID/path, scope, language/version, source, event/entry-point references, dependencies and metadata.
9. **Security Roles / Policies**.
10. **Gateway / TAG Bridge routes**.
11. **Operational Commands**.
12. Future trends, shell regions, libraries, Engineering Fragments and plugin-owned configuration.

Plugin-owned driver/Data Source configuration must expose a public versioned schema so it can participate in validation, import/export, backup/restore and migration without becoming opaque private state.

### Secrets rule

Passwords, tokens, private keys and equivalent secrets never appear in plaintext Engineering packages. Credentials/password hashes are not Engineering configuration. Technical configuration uses protected secret references.

## Current merged platform baseline

The following important slices are already official `main` state:

- real Modbus TCP runtime and common driver boundary;
- PostgreSQL Engineering persistence and revision lifecycle;
- TimescaleDB historian baseline;
- Engineering Schema v7 first-class operational commands through PR #35, merge `2fd568976fc6277d0b069adeeb560f6ea3d8205f`;
- protected sensitive read/realtime/WebSocket surfaces through PR #36, merge `10b0320149c1ef2109e9517539717a8800b200c2`;
- Engineering UI foundation/localization through PR #37, merge `4553aa7ab5ba7e05a209a7c8462286d1a34a1ad6`;
- trusted local identity/browser login foundation through PR #38, merge `2a581d279a428cb605429d5939c333ff7ad8d1b4`.

The current Engineering UI includes `/engineering`, Runtime↔Engineering navigation, `pt-BR`/`en`/`es`, and structured TAG/Data Source/Alarm editors whose current mutation behavior remains intentionally preview-oriented until secured Apply/Delete/bulk workflows are added.

Local identities remain separate from Engineering roles/policies. Local users reference role keys; the active Engineering revision remains authoritative for capabilities/scopes. Browser authentication uses the same trusted JWT boundary and HttpOnly cookie support without replacing normal Bearer-token integration.

## Project lifecycle and persistence

Engineering work is distinct from operational runtime.

The lifecycle explicitly distinguishes:

- editable **Working** state;
- immutable saved **Revisions**;
- **Published** revision;
- **Active** revision driving runtime.

Required behavior:

- Engineering Workspace is isolated from Active Runtime.
- Checkout restores a persisted revision into an isolated editable workspace.
- Saves preserve revision lineage through `BasedOnRevision`.
- Publication does not automatically imply activation.
- Activation is transactional: candidate runtime is staged/validated before replacing the active runtime.
- Failed activation leaves the previous active runtime intact.
- Restart recovery uses the persisted Active Revision and fails closed if industrial runtime recovery cannot be performed safely.
- Persistence follows public Engineering contracts, not the reverse.

Project engineering backup/restore uses versioned `.escadapkg` data containing canonical Engineering content plus integrity metadata. It is not a historian/database image.

## Internal memory TAG sources

Before new external protocol families, EliteSCADA must implement two explicit built-in memory sources:

- `builtin.memory.client`: one value set per opened Runtime Client/session, non-retentive server-side initially;
- `builtin.memory.server`: one server-owned shared value per TAG, retentive by design.

### Client Memory

- initialized from a typed Engineering initial/default value;
- different clients may hold different values for the same engineered definition;
- intended for popup/navigation state, selected equipment/context, local UI flags, temporary filters, demo controls and Client Visual Scripts;
- may be read/written by the owning client's scripts;
- is never an authentication source, safety interlock, global permissive, server sequence truth or audit identity;
- does not drive global historian/alarm semantics because it has no single global value.

### Server Memory

- shared consistently across authorized clients;
- suitable for simulation/internal variables, retained parameters, intermediate/server sequence state and future Server Scripts;
- participates in shared cache/events/realtime/security and historian/alarm behavior when configured;
- retentive runtime values are persisted separately from immutable Engineering revisions;
- stable TAG ID is the primary retention identity so path renames preserve values;
- incompatible type changes require explicit validation/reset/migration, never silent coercion;
- engineered typed initial/default value is used when no compatible retained value exists.

Internal memory sources do not fabricate network timeout/reconnect/latency metrics.

Full locked semantics: `docs/INTERNAL-MEMORY-TAGS.md`.

## Protocol-independent TAG Gateway

Before additional external protocols, EliteSCADA must implement a server-side first-class Gateway/TAG Bridge:

`Source TAG -> Gateway route -> Destination TAG`

Locked rules:

- routes are versioned Engineering entities with stable IDs;
- concrete drivers never call each other directly;
- `builtin.memory.server` is valid as source/destination; `builtin.memory.client` is not a server Gateway endpoint;
- destination must be active, writable and type-compatible;
- first version is unidirectional;
- direct/indirect cycles are rejected;
- multiple active Gateway writers to one destination are rejected unless a future arbitration policy explicitly allows them;
- fan-out is allowed through separate routes;
- OnChange and Periodic modes, bounded intervals, deadband/minimum interval/coalescing;
- default transfer requires source quality `Good`;
- unsafe implicit coercion is forbidden;
- simple transform may use `destination = source × gain + offset`;
- route diagnostics are independent from driver transport diagnostics;
- route failures do not corrupt source TAG quality.

Full locked semantics: `docs/TAG-GATEWAY.md`.

## Multi-Data-Source communication and diagnostics

EliteSCADA supports the architecture of multiple simultaneous Data Sources, including multiple instances of the same driver type and different protocol families.

The model distinguishes:

- **Driver type** = protocol/implementation;
- **Data Source** = concrete configured runtime source/connection/device context;
- **TAG** = point owned by one Data Source/source provider per revision plus protocol binding/address where applicable.

Failure of one Data Source must not contaminate another independent Data Source.

Common protected diagnostics must expose where meaningful:

- Data Source identity/type and sanitized endpoint identity;
- healthy/degraded/reconnecting/faulted state;
- state-change time;
- last success and last failure;
- request/cycle, success, failure and timeout counters;
- consecutive failures;
- reconnect/disconnect count;
- failure rate;
- response/round-trip latency;
- configured scan and observed data age;
- associated TAG count and quality aggregation such as Good/BadCommunication;
- sanitized last error.

TAG quality remains authoritative per point. Driver health is a summary, not a replacement for TAG quality.

Full locked semantics: `docs/COMMUNICATION-DRIVER-DIAGNOSTICS.md`.

## Locked prerequisite order before new external protocols

The architectural sequence is:

**internal memory -> TAG-to-TAG Gateway -> common multi-driver diagnostics -> USER INTERFACE VALIDATION PREVIEW -> new external drivers/protocols**

The interface preview is an explicit product gate. It must provide a practical Windows x64 test path with local login, demo project, required services/startup automation or reliable instructions, visible version identification and a short validation checklist. Feedback is reviewed before investing heavily in the next protocol wave.

Research/specification spikes for future protocols may run earlier only when they do not register a production Data Source, alter active runtime composition or bypass the locked gate. Their purpose is to reduce uncertainty, not to smuggle protocol implementation ahead of the product sequence.

Full milestone: `docs/INTERFACE-VALIDATION-MILESTONE.md`.

## Python scripting and visual runtime foundation

Before the full graphical screen/popup/Dynamo editor is created, EliteSCADA must first establish the scripting and visual-property contracts described in `docs/PYTHON-SCRIPTING-AND-VISUAL-RUNTIME.md`.

### Two script scopes

**Client Visual Scripts** execute in the Runtime Client and may:

- read permitted shared TAGs;
- read/write that client's Client Memory;
- react to screen/object/TAG/timer events;
- read/change explicitly runtime-writable visual properties of the current screen/popup/Dynamo instance;
- request normal authorized backend operations through explicit APIs.

They cannot directly access drivers, database, filesystem, OS/shell, arbitrary network/DOM internals, secrets or a stronger principal than the logged-in user.

**Server Scripts** are a separate server-owned capability for shared calculations/automation using shared TAGs and Server Memory. They never manipulate one browser's visual-object instances or choose one client's Client Memory as global truth.

### Visual-object property contract

Every visual object type exposes one typed public property schema consumed by both the graphical property inspector and the script API.

Properties declare stable key, type/default/constraints, Engineering editability, runtime readability/writability, binding support and animatability.

Common properties include, as applicable:

- x/y;
- width/height;
- rotation and scale;
- z-order;
- visible;
- opacity;
- background/fill color;
- line/stroke color;
- line/stroke width/thickness and style;
- corner radius/effects;
- text, text color, font size/style/alignment;
- image/resource reference and image presentation options.

Type-specific objects may add explicit schema properties.

### Engineering value versus runtime visual state

This separation is mandatory:

- Engineering stores design-time/base property values.
- TAG bindings, animations and scripts may create runtime presentation overrides for one visual-object instance.
- script changes to width, position, colors, thickness, opacity, rotation, visibility etc. do **not** silently mutate saved Engineering revisions.
- different clients may have different runtime presentation state.
- closing/disposal of a visual instance also disposes its subscriptions/timers and re-establishes deterministic state on the next instance.

### Script editor and animation

Before the graphical editor, Engineering must provide a practical Python code editor with syntax highlighting, line/column diagnostics, validation, script scope/event association, API autocomplete where practical and a sandboxed test/preview workflow.

Scripts are first-class versioned Engineering entities and participate in import/export, revisions, `.escadapkg`, dependency validation and Engineering Fragments.

Client scripting is primarily event driven. Required event direction includes load/unload, object interaction, TAG/Client-Memory change, timers and an optional bounded frame/tick callback.

Normal smooth animation should use renderer-native animation/tween primitives invoked from Python, with duration/easing/repeat/cancel behavior, instead of requiring high-frequency Python busy loops. Binding/script/animation precedence must be deterministic and diagnosable.

A faulty script must be isolated with time budgets, cancellation, bounded event queues, diagnostics and no ability to freeze the backend or unrelated clients indefinitely.

### Required sequence before graphical visual engineering

**Python scripting contract + visual property schema -> script editor/sandbox -> visual runtime object instances/property API -> graphical screen/popup/Dynamo editor -> advanced reusable visual libraries**

The interface-validation preview after driver diagnostics may occur before this visual-editor sequence. The final screen/Dynamo editor may not bypass this scripting/property foundation.

## Reusable visual/engineering libraries

Equipment Templates/Equipment and Dynamos evolve into version-aware reusable class/instance systems with:

- reusable definitions and typed properties/bindings;
- application-specific instance context;
- nested reusable components;
- script/event behavior through stable references;
- deterministic dependency validation;
- independent import/export;
- controlled version migration;
- preservation of safe instance overrides.

Cross-project copy/paste uses canonical **Engineering Fragments**, with dependency-aware preview, conflict handling, rebinding and selected-only/selected-with-dependencies modes. Browser clipboard state is not authoritative Engineering data.

## Historian and trends

TimescaleDB remains the historian direction. Required evolution includes retention, aggregation/downsampling, multiple-Pen trends, historical/live sources, engineered and ad-hoc/saved trends and expressions where appropriate.

Historian storage implementation details must not leak into public Engineering concepts.

## Security and audit

Security is backend-enforced, never merely hidden UI.

Capabilities distinguish, as the product grows:

- view/read;
- TAG read;
- operational command execution;
- process/setpoint write;
- alarm ACK;
- alarm shelving;
- trend use/save;
- Engineering modification;
- user/role administration;
- system/module administration.

Scopes may restrict by area, equipment, screen, TAG or command. Protected-action identity comes from trusted authentication, not caller-supplied actor fields.

Sensitive mutations and administrative operations are auditable. Audit is durable/append-only and requires retention/outage-buffering policy hardening.

Python scripts never bypass these capabilities. Editing/applying/publishing scripts is itself a protected Engineering operation.

## Alarm philosophy

Alarms are runtime objects backed by Engineering definitions. State, priority/class/area/message, ACK and shelving are backend runtime/security concerns. ACK/shelving remain permission-controlled and auditable. Future shell UI may expose persistent alarm regions without becoming alarm truth.

## Configurable application shell

Applications must support configurable persistent regions such as header, footer, navigation, alarm summary and optional side regions, with controlled global/application/screen overrides.

Common widgets include application identity, logged-in user, navigation, alarm summary and date/time.

Date/time presentation may use the EliteSCADA server clock or TAG-provided PLC/RTU time. Displaying a device clock never silently synchronizes it; active clock synchronization is a separate future protected industrial command.

## Engineering UI localization

The complete Engineering/development interface supports:

- Português (`pt-BR`);
- English (`en`);
- Español (`es`).

Localization includes Data Sources, TAGs, historian/diagnostics, alarms, equipment/Dynamos, scripts, screen/popup editing, trends, lifecycle, users/security, modules, Gateway, property editors, dialogs and validation/help text.

Language is a presentation/user preference. It never changes stable Engineering IDs, paths, enum values, public schema keys, script API identifiers or runtime semantics.

Runtime-HMI multilingual application content is a separate capability.

## External protocols and installable driver modules

Modbus TCP remains the current real protocol baseline.

After the prerequisite foundation and interface-preview gate:

1. MQTT;
2. OPC UA;
3. BACnet;
4. installable/versioned Driver Module framework;
5. Siemens S7 ISO Connection as the first intended installable module target;
6. later Allen-Bradley research based on public documentation/libraries, licensing, testability and representative hardware/simulator access.

Driver modules declare stable identity/version, EliteSCADA compatibility, provided driver/Data Source types and public versioned Engineering configuration schema. Missing/disabled/incompatible modules preserve project configuration and expose explicit diagnostics. Module installation/upgrade/removal is security-sensitive and auditable. Package integrity/trust must be evaluated before executable code is enabled.

### OPC UA Engineering/discovery experience

When OPC UA reaches production implementation, EliteSCADA must provide more than manual endpoint/NodeId entry.

Locked OPC UA product direction:

- manual endpoint configuration plus standard server/endpoint discovery;
- an opt-in, bounded and cancellable **Scan network for OPC UA devices/servers** tool using standard OPC UA discovery mechanisms where available and controlled host/port probing only as fallback;
- endpoint inspection covering transport, security mode/policy, supported authentication/user-token types and server-certificate identity;
- explicit certificate trust with fail-closed handling for unexpected server identity changes rather than silently trusting arbitrary servers;
- connection test before importing TAGs;
- lazy, searchable/filterable address-space tree browser;
- multiple selection and optional subtree candidate collection;
- import preview mapping OPC UA variables into canonical EliteSCADA TAG Engineering before Apply;
- subscription/update profiles so imported TAGs use native OPC UA monitored-item/subscription semantics;
- imported bindings preserve NodeId plus namespace-aware portable BrowsePath/namespace URI information so nodes can be safely re-resolved after server/namespace changes;
- a Refresh/Re-resolve Node IDs workflow with preview and deterministic mismatch/type-change handling;
- Rescan/diff workflow for new, missing and changed server nodes without silently deleting EliteSCADA Engineering or historian data;
- unsupported/lossy data types are reported explicitly, never silently coerced;
- production runtime continues to use normal TAG/security/Audit/Gateway/diagnostic boundaries and never creates a private protocol bypass.

The official OPC Foundation UA .NET Standard client stack is the primary implementation candidate and must be evaluated during the technical spike/implementation slice rather than reimplementing OPC UA privately without cause.

Full locked semantics and the permitted early non-production spike: `docs/OPC-UA.md`.

## Development quality rules

- Prefer small coherent slices with automated validation.
- Do not merge known-failing changes into `main`.
- Use CI as the external .NET validation environment when local .NET execution is unavailable.
- Validate backend build/tests/runtime smoke, frontend build and Chromium E2E when affected.
- Fix root causes instead of weakening tests, security or concurrency for a green pipeline.
- Preserve Engineering schema compatibility or explicit migration behavior/tests.
- Keep industrial runtime safety ahead of UI convenience.
- Do not create placeholder security endpoints without a real domain model.
- Do not let scripts or visual editors bypass public Engineering/security/runtime boundaries.
- Documentation updates must not erase locked future requirements.

## Relationship to other repository documents

- `PROJECT GOAL.md`: stable product intent and locked architecture.
- `LAST CHANGE.md`: exact operational resume point and MERGED / IMPLEMENTED IN PR / SPECIFIED state.
- `docs/ROADMAP.md`: ordered implementation status.
- `docs/ARCHITECTURE.md`: current architecture/data flow.
- `docs/ADR-*.md`: accepted focused decisions.
- `docs/SECURITY-AUTHORIZATION-AUDIT.md`: security implementation boundary.
- `docs/VISUAL-COMPONENT-LIBRARY.md`: reusable visual-component direction.
- `docs/INTERNAL-MEMORY-TAGS.md`: Client/Server Memory semantics.
- `docs/TAG-GATEWAY.md`: TAG Gateway semantics.
- `docs/COMMUNICATION-DRIVER-DIAGNOSTICS.md`: multi-driver diagnostic contract.
- `docs/INTERFACE-VALIDATION-MILESTONE.md`: mandatory product-owner preview gate.
- `docs/OPC-UA.md`: OPC UA discovery, browse, import, security and future driver Engineering experience.
- `docs/PYTHON-SCRIPTING-AND-VISUAL-RUNTIME.md`: Python scripting, script editor, visual property schema and runtime visual-state contract.

These documents must remain consistent. `PROJECT GOAL.md` wins for locked product intent; current repository code/`main` wins for implementation truth; `LAST CHANGE.md` records the exact handoff.