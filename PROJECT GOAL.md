# PROJECT GOAL — EliteSCADA

> Persistent project memory and product north.
>
> This file exists specifically to preserve continuity across ChatGPT conversations, session limits, developers and tooling. It is not a replacement for `docs/ROADMAP.md`, ADRs or architecture documentation. It defines the stable goal, locked principles and the context that must be known before changing the project.

**Last reviewed:** 2026-08-26

## Mandatory continuity protocol

This protocol is part of the project and is not optional.

1. At the **beginning of every task related to EliteSCADA**, regardless of which ChatGPT conversation/session is being used, read both:
   - `PROJECT GOAL.md`
   - `LAST CHANGE.md`
2. Only after reading those two files should implementation planning or repository changes begin.
3. If the user changes, adds, removes or clarifies a stable project goal, product principle, architectural constraint or locked requirement in ChatGPT, update `PROJECT GOAL.md` in the same task.
4. **Before sending the final user-facing message for any EliteSCADA task**, update `LAST CHANGE.md` with the actual repository state and what was done.
5. `LAST CHANGE.md` must record enough context for a fresh ChatGPT conversation to resume without reconstructing the previous chat.
6. Never treat ChatGPT conversation history alone as the source of truth for current project position. Session limits and chat changes are expected.
7. If chat memory, roadmap and repository disagree, inspect the repository and these two continuity files before acting; do not guess.
8. Permanent architectural decisions must not remain only in a feature branch. Record them in the official `PROJECT GOAL.md` on `main` even before implementation. `LAST CHANGE.md` must state explicitly whether relevant work is **MERGED**, **IMPLEMENTED IN PR** or **SPECIFIED / NOT IMPLEMENTED**.

## Product mission

EliteSCADA is a modern industrial SCADA / Supervisory platform intended to become a serious engineering and runtime product, not merely a monitoring dashboard.

The product must support the complete lifecycle of an industrial application:

- engineering configuration;
- reusable industrial objects and screens;
- communication drivers;
- live runtime;
- alarms and events;
- historical data and trends;
- security and audit;
- project revisioning, publication and activation;
- backup, restore, import and export;
- future extensibility through public contracts/SDKs and installable modules.

The long-term direction is comparable in responsibility to established industrial supervisory platforms while keeping an independent EliteSCADA architecture, data model and implementation.

## Core architectural principle

The **public, versioned Engineering model is authoritative**.

Runtime, editor, persistence, import/export, reusable libraries, administrative tools and future extensions must consume the same engineering model. No graphical editor, database schema, browser state or driver-specific representation may silently become the only source of project truth.

The SCADA Core must remain independent of:

- a specific PLC/protocol;
- a specific database engine;
- a specific UI technology.

Mandatory shared/server runtime flow:

`Device/Server Source -> Driver / Source Provider -> TAG Engine / Current Cache -> Event Bus -> Historian / Alarm Engine / Realtime / Gateway / Scripts`

Client-local presentation/session state may use the explicit Client Memory source defined below and must not be mistaken for one globally authoritative server TAG value.

The frontend never accesses industrial drivers directly, and drivers never depend on screens/UI. Protocol-independent data transfer between devices must happen through TAG-level runtime services, never by making one concrete driver call another concrete driver.

## Technology baseline

Current technology north:

- Backend/Core: .NET 10 LTS.
- Frontend: React + TypeScript.
- Configuration/persistence: PostgreSQL.
- Historian: PostgreSQL + TimescaleDB.
- Realtime client transport: WebSocket.
- Public integration: REST API.
- Industrial protocol/messaging expansion: MQTT, OPC UA and BACnet.
- Scripting/analytics: sandboxed Python in a later phase.
- Extension direction: public SDK plus installable/versioned driver modules in a later phase.

Technology choices may evolve deliberately, but product contracts must not be coupled unnecessarily to one implementation technology.

## Mandatory Engineering Import/Export principle

Engineering Import/Export is a **cross-cutting core capability**, not a utility to be added after the editor is finished.

Every relevant engineering entity introduced into EliteSCADA must be:

- serializable;
- versioned;
- importable/exportable through a public interface;
- usable without depending on the graphical editor;
- validated before application;
- compatible with the common preview/apply workflow.

Canonical technical engineering representation is versioned JSON (`scada.engineering`). Tabular bulk engineering may use CSV and, later, XLSX, but those formats do not replace the canonical model.

Mandatory import flow:

`parse -> validate -> preview -> choose merge mode -> apply`

Supported merge semantics include create-only, update-existing and create-and-update.

### Required engineering domains

At minimum, the public Engineering model covers and must continue to cover:

1. **TAGs**: stable ID, path, name, data type, unit, description, Data Source, address/binding where applicable, scaling, deadband, historian policy, permissions/access policy, memory initial/default value where applicable and metadata.
2. **Alarms**: associated TAG, type, limits/setpoint, priority, class, area, message, delay, ACK behavior, shelving policy and related metadata.
3. **Data Sources / Drivers / Internal Sources**: technical communication configuration, built-in memory-source configuration and bindings.
4. **Equipment Templates and Equipment instances**: reusable class/instance engineering structures, bindings, properties and context.
5. **Dynamos / reusable visual definitions**: bindings, properties, context and dependencies.
6. **Screens and Popups**: routes, visual definitions, bindings, context and reusable dependencies.
7. **Security Roles / Policies**: capabilities and scopes used by the application.
8. **Gateway / TAG Bridge routes**: protocol-independent source-TAG to destination-TAG transfer definitions, quality/type/rate policies and stable endpoint references.
9. **Operational Commands**: first-class command definitions with stable identity, target TAG references, driver-routed execution and security/audit semantics. Commands are already part of Engineering Schema v7.
10. Future engineering domains such as trends, shell regions, libraries and plugins must join the same versioned public model when introduced.
11. Plugin-owned driver/Data Source configuration must expose a public versioned schema so it participates in Engineering validation, import/export, backup/restore and migration without becoming opaque private state.

### Secrets rule

Passwords, authentication tokens, private keys and other secrets must never be serialized in plaintext inside Engineering packages.

Technical configuration uses protected secret references such as environment/vault/key-vault style references. Engineering packages may carry authorization policies, but never user passwords/password hashes or equivalent authentication secrets.

## Current command-domain baseline

The first-class operational command domain is implemented and merged into `main` through PR #35 `Add first-class operational command domain`.

Locked current facts:

- Engineering Schema is at **v7** for the command-domain baseline;
- command definitions/registries participate in canonical Engineering Import/Export;
- commands compile into the active runtime and execute through the target TAG's owning driver;
- `CommandExecute` is enforced with area/equipment/TAG/command scopes;
- command success, denial and failure are audited without persisting commanded values as configuration;
- PR #35 merged as commit `2fd568976fc6277d0b069adeeb560f6ea3d8205f`.

Sensitive read/realtime protection remains a separate security slice represented by PR #36 until it is independently validated and merged.

## Project lifecycle and persistence

Engineering work is distinct from the operational runtime.

The project lifecycle explicitly distinguishes:

- **Working** engineering state;
- immutable saved **Revisions**;
- **Published** revision;
- **Active** revision actually driving the runtime.

Required behavior:

- Engineering Workspace is isolated from the active runtime.
- Checkout restores a persisted revision into an isolated editable workspace.
- Saves preserve revision lineage (`BasedOnRevision`).
- Publication does not automatically mean activation.
- Activation is transactional: candidate runtime is staged and validated before replacing the active runtime.
- Failed activation keeps the previously active runtime intact.
- Restart recovery uses the persisted Active Revision and fails closed if the industrial runtime cannot be recovered safely.
- Persistence follows the public Engineering contract, not the reverse.

Project engineering backup/restore uses the versioned `.escadapkg` concept containing canonical engineering data plus integrity metadata. It is an engineering-project package, not a full historian/database image.

## Internal memory TAG sources

Before additional external communication protocols are implemented, EliteSCADA must provide two explicit built-in internal memory source types through the public Engineering/Data Source model:

- **`builtin.memory.client`**: Client Memory scoped to one opened Runtime Client instance/session;
- **`builtin.memory.server`**: shared Server Memory owned by the EliteSCADA server runtime and retentive by design.

These are internal TAG value sources, not fake PLC/network connections. They do not require protocol addresses and must not expose invented network timeout/reconnect/latency statistics.

### Client Memory

Client Memory is local to each Runtime Client. Different clients may hold different values for the same engineered Client Memory TAG definition.

Required semantics:

- per-client/session value store rather than one server-global scalar value;
- initialized from a typed engineered initial/default value when the client/session starts;
- no server retention in the initial implementation;
- suitable for popup/screen transition variables, selected equipment/context, navigation state, temporary filters, UI flags, local demo controls and future client-side scripts;
- client-side scripts may read/write Client Memory for their own client;
- server-side scripts cannot treat Client Memory as one authoritative global value;
- Client Memory must not be used as authentication/authorization state, safety interlock state, authoritative command permissive, server process sequencing truth or audit identity;
- Client Memory does not drive the global server historian or server alarm engine in the initial implementation because its value is not globally unique.

The exact browser/session persistence mechanism is an implementation detail, but browser storage must never silently redefine Client Memory as trusted server or user-profile state.

### Server Retentive Memory

Server Memory has one authoritative value per TAG in the active server runtime. All authorized clients observe the same value.

Required semantics:

- shared across Runtime Clients;
- suitable for simulation variables/parameters, internal sequence state, intermediate values, operator-adjustable internal parameters and future server-side scripts;
- participates in the normal Current TAG Cache, Event Bus, realtime/WebSocket distribution, authorization, historian/alarm semantics where configured and future server scripting;
- external writes use the normal backend TAG/process-write security boundary;
- normally carries `Good` quality because there is no external transport; bad/uncertain quality must represent a real modeled internal failure rather than fake `BadCommunication`.

Server Memory is **retentive by design**. Its mutable runtime value is persisted separately from immutable Engineering revisions/packages.

Retention rules:

- stable TAG ID is the primary identity for retained values;
- renaming a TAG path while preserving its stable ID preserves its retained value;
- server restart and compatible runtime/revision reactivation restore the retained value;
- when no retained value exists, use the engineered typed initial/default value or a deterministic type default;
- incompatible retained-value/data-type changes must never be silently coerced; validation/activation must surface the incompatibility and use an explicit reset/migration policy;
- deleting a TAG removes it from active runtime and stale retained state must never resurrect it automatically.

Memory TAG Engineering therefore needs a typed initial/default value in the public versioned contract. Mutable retained runtime values themselves must not be serialized into normal Engineering export/revision data as if they were configuration.

### Relationship to source/driver architecture and scripting

The public Data Source concept is broader than a physical device connection: it identifies the owner/provider of TAG values.

External protocol Data Sources compile into communication drivers. `builtin.memory.server` compiles into an internal server source/provider. `builtin.memory.client` remains a client-owned source definition and must not be forced into one global server `ICommunicationDriver` value store because that would destroy its per-client semantics.

The common runtime architecture may introduce a clearer **Source Provider** abstraction where useful rather than pretending every source is a network driver.

Future scripting must keep the scope explicit:

- client-side scripts may access Client Memory plus permitted shared runtime TAGs;
- server-side scripts may access Server Memory plus permitted shared runtime TAGs;
- no server-side API may accidentally choose one client's Client Memory as global truth.

Full semantics and validation scenarios are locked in `docs/INTERNAL-MEMORY-TAGS.md`.

## Protocol-independent TAG Gateway

Before adding additional external protocol families, EliteSCADA must implement a server-side **Gateway / TAG Bridge** capability that transfers data between server-owned runtime TAGs without coupling communication drivers directly.

Core route semantics:

`Source TAG -> Gateway route -> Destination TAG`

The source and destination may belong to different Data Sources and different source/driver types. The runtime reads the source through the common TAG path and writes the destination through its owning active driver/source provider. No pairwise Modbus-to-S7, S7-to-OPC-UA or similar adapter logic is allowed in the core design.

Required behavior:

- Gateway routes are first-class, serializable, versioned Engineering entities with stable IDs and source/destination TAG references.
- Data Sources may be used by the UI to filter/select endpoints, but the authoritative mapping is TAG-to-TAG rather than Data-Source-to-Data-Source.
- `builtin.memory.server` is a valid source or destination.
- `builtin.memory.client` is not a valid server Gateway endpoint because there is no one global Client Memory value.
- destination TAG must be active, type-compatible and writable through its owning provider;
- initial implementation is unidirectional and must reject direct or indirect route cycles;
- initial implementation should reject multiple active Gateway writers targeting the same destination TAG unless a future explicit arbitration model is introduced;
- fan-out from one source TAG to several destination TAGs is allowed through separate routes;
- transfer modes should include OnChange and Periodic, with bounded intervals, optional deadband/minimum write interval and coalescing to avoid unbounded device writes;
- startup must wait for an acceptable source value/quality and a writable destination before initial synchronization;
- default quality policy transfers only `Good` source values and does not push stale values when source communication becomes bad;
- source/destination type compatibility must be validated before activation, with no unsafe implicit coercion;
- simple explicit linear transformation may use `destination = source × gain + offset`;
- complex calculations/sequencing remain the responsibility of future scripts/expressions rather than turning the Gateway into a programming language;
- Gateway execution is a trusted internal runtime service, not a borrowed browser/user session. Engineering configuration changes are security-sensitive/auditable; cyclic sample transfers should use route diagnostics rather than flooding the human audit trail;
- route diagnostics must expose state, last successful/failed transfer, counters, quality skips, throttling/coalescing and sanitized write errors independently from communication-driver health.

A Gateway failure must not corrupt source TAG quality. A destination communication failure affects the route/destination write path independently while the source remains authoritative.

The Gateway is a prerequisite before new external protocol families so the next protocol immediately participates in multiprotocol TAG routing through the common runtime model.

Full semantics and validation scenarios are locked in `docs/TAG-GATEWAY.md`.

## Industrial communication

Drivers are accessed through common contracts and Data Source engineering definitions.

Current baseline includes real Modbus TCP runtime support with polling, writes, reconnect and communication-quality behavior. New protocols must follow the same separation between Engineering configuration, compiled runtime plan and driver execution.

### Multiple communication instances and device topology

EliteSCADA must support **multiple communication drivers/Data Sources active at the same time**, including:

- multiple instances of the same protocol communicating with different PLCs, RTUs, instruments or other devices;
- different protocol families active simultaneously in the same application;
- independent connection, scan, timeout, reconnect and diagnostic state per Data Source/communication instance.

The model distinguishes:

- **Driver type**: the protocol/implementation type, for example `modbus.tcp`, future `siemens.s7`, `opc.ua`, `bacnet` or another module-provided type;
- **Data Source**: one concrete configured runtime instance of a driver type, normally representing one connection/device/channel or another protocol-appropriate communication context;
- **TAG**: an engineering point associated with exactly one Data Source for its communication ownership in a revision, plus its protocol-specific address/binding.

A project may therefore contain many Data Sources using the same Driver type and many Data Sources using different Driver types. Driver implementations must not assume they are unique/singleton communication channels for the entire application.

A communication failure in one Data Source must remain isolated: it must not contaminate the runtime health, counters or TAG quality of another independent Data Source.

### Communication quality and driver diagnostics

Communication diagnostics are a **first-class operational and Engineering capability**, not merely log text.

The Engineering/development interface must provide a communication-diagnostics view where each active Data Source/driver instance can be inspected individually and summarized collectively.

At minimum, the diagnostic model should expose, where meaningful for the protocol:

- Data Source key/name and driver type;
- configured non-secret endpoint/device identity suitable for diagnostics;
- runtime state such as healthy/running, degraded, reconnecting or faulted/failed as the driver model evolves;
- last state-change time;
- last successful communication/sample time;
- last communication failure time and sanitized last error;
- total communication cycles/requests or equivalent protocol operations;
- successful and failed operation counts;
- consecutive failure count;
- timeout count;
- reconnect/disconnect count;
- current and/or recent failure rate;
- last and representative response/round-trip time where the protocol provides a meaningful measurement;
- configured scan/publish interval and observed data age where applicable;
- number of associated TAGs and counts by current TAG quality such as Good, BadCommunication and other supported quality states.

The exact metrics may vary by protocol, but all drivers must map their diagnostics into a common public diagnostic contract instead of exposing only protocol-private log strings.

TAG quality remains authoritative per point. A driver-level health summary may aggregate TAG and transport behavior, but it must not erase per-TAG quality or falsely mark every point good merely because the socket/session is connected.

Communication diagnostics must never expose passwords, tokens, private keys or other protected secret values. Diagnostic reads are subject to backend authorization appropriate to Engineering/system diagnostics.

The diagnostic UI should support quick identification of healthy, degraded and failed communication instances and drill-down into an individual Data Source. Longer-term operational hardening may add retained diagnostic history, rate windows and communication events/alarms without making the UI the source of truth.

The locked protocol direction includes:

- **Modbus TCP** as the currently implemented first real industrial driver;
- **MQTT** as a planned first-class communication/messaging integration after the internal memory sources, TAG Gateway and common diagnostics foundations;
- **OPC UA** as a planned first-class industrial interoperability protocol after the internal memory sources, TAG Gateway and common diagnostics foundations;
- **BACnet** as a planned communication-driver protocol, especially relevant to building automation/BMS and devices/controllers that expose BACnet interoperability, after the internal memory sources, TAG Gateway and common diagnostics foundations;
- additional protocols supplied by first-party or third-party **installable driver modules** through the same public Driver SDK boundary.

Together with Siemens S7 and future Allen-Bradley support, these protocol families are intended to give EliteSCADA broad practical compatibility across mainstream PLC, industrial automation and building-automation environments. The user's planning assumption is that this protocol set should cover more than 90% of practical PLC/controller needs encountered in the target market; that percentage is a product-planning hypothesis and must be validated before being presented externally as a measured market statistic.

MQTT, OPC UA, BACnet and future modules must use the same Data Source / TAG / Engineering model rather than create protocol-specific configuration islands. Writable/readable TAGs from those providers should become eligible for protocol-independent Gateway routes through the same runtime contract.

### First installable driver target: Siemens S7

The **first intended installable communication-driver module** is a Siemens S7 driver compatible with **S7 ISO Connection** for Siemens PLC communication.

This is a future implementation target, not active development at the time this requirement was recorded.

When the module-framework and S7 implementation slice is reached:

- research existing public/open-source implementations, including relevant Node-RED S7 communication work and libraries;
- evaluate whether architecture, protocol handling or code can be reused safely;
- reuse source code only when its license is compatible with EliteSCADA and all attribution/distribution obligations are understood;
- independently validate protocol behavior, reconnect handling, address model, data types, PLC-family compatibility, read/write semantics, diagnostics and industrial reliability rather than assuming a Node-RED implementation is production-ready for a SCADA runtime;
- keep the S7-specific address/configuration model inside the common versioned Data Source/Engineering contract and Driver SDK boundary.

The exact Siemens PLC families, supported address/data types, connection modes and write capabilities will be defined after technical research at the appropriate implementation stage.

### Future Allen-Bradley driver target

EliteSCADA should also pursue a future communication module for **Allen-Bradley PLCs**.

This is intentionally recorded as a research target rather than a prematurely fixed protocol implementation. When its turn arrives:

- research public protocol documentation, open-source projects, existing libraries and legally reusable implementations available at that time;
- determine which Allen-Bradley PLC families and communication protocols can be supported reliably without depending on unavailable proprietary information;
- evaluate licensing, interoperability, testability and access to representative equipment/simulators before committing to a production scope;
- use manufacturer documentation or cooperation when available, but do not make the entire architectural goal depend on obtaining direct manufacturer support;
- keep any resulting driver behind the same installable module, Driver SDK, Data Source, TAG, security and Engineering boundaries.

No Allen-Bradley protocol/library choice is considered locked yet. The implementation decision must be based on evidence gathered during that future research slice.

## Installable driver modules and Driver SDK

EliteSCADA must support adding communication drivers without rebuilding the core product for every protocol.

The target is a controlled module/plugin system for first-party and third-party drivers. At minimum, the module model must provide:

- an explicit module/package identity and version;
- declared compatibility with the EliteSCADA Driver SDK/platform contract;
- declared driver/Data Source types provided by the module;
- a public versioned configuration schema for Engineering Import/Export;
- installation and removal lifecycle;
- enable/disable lifecycle without deleting Engineering configuration;
- upgrade/version migration rules;
- dependency and compatibility validation before activation;
- publisher/trust/integrity metadata so arbitrary untrusted code is not silently loaded into an industrial runtime;
- clear diagnostics when a project references a missing, disabled or incompatible module;
- preservation of project Engineering data even when the corresponding driver module is temporarily unavailable;
- no plaintext secrets embedded in module packages or exported Engineering configuration.

Driver execution should remain behind the DriverHost/driver boundary so a module does not gain direct access to frontend concerns or become the authority over TAGs, alarms, historian or project configuration.

The exact package format, sandbox/isolation mechanism, signing policy and distribution/catalog UX are implementation decisions to be finalized in a dedicated architectural slice, but the ability to install additional driver modules is a **locked product requirement**.

## Historian and trends

Historical data is a first-class runtime concern, with TimescaleDB as the current intended backend.

The product must evolve to support:

- retention policies;
- aggregation/downsampling;
- multiple-Pen trends;
- historical TAG sources;
- live/runtime bindings;
- expressions where appropriate;
- engineered trends on screens/popups;
- ad-hoc and saved runtime trends subject to access policy.

Historical queries and realtime subscriptions remain distinct concepts even if displayed in one trend component. Engineering trend definitions must not expose TimescaleDB-specific storage details as product concepts.

## Security model

Security is enforced by the backend/API, never merely by hiding UI elements.

The authorization model is capability based and configurable per application. Roles do not imply one globally hard-coded hierarchy.

Capabilities must distinguish, as the product grows, areas such as:

- view/read access;
- TAG/process-value read;
- operational command execution;
- setpoint/process-value write;
- alarm acknowledgement;
- alarm shelving;
- trend use/save;
- engineering modification;
- user/role administration;
- system administration.

Scopes may restrict access by area, equipment, screen, TAG or command.

Authenticated identity used for protected actions must come from a trusted authentication principal/token, not from caller-supplied `...By` request fields.

Sensitive mutations and administrative actions are auditable. Audit history is durable, append-only and protected against ordinary update/delete/truncate mutation at the database boundary.

Module installation, removal, enable/disable and upgrade are security-sensitive administrative operations and must be permission-controlled and auditable when the module system is implemented.

## Alarm philosophy

Alarms are runtime objects backed by Engineering definitions.

Alarm behavior includes state, priority, class/area/message, acknowledgement and shelving policy. ACK and shelving are security-controlled and auditable operations. Shelving must respect whether a specific alarm permits shelving.

Future alarm UX should support persistent alarm summaries/banner regions in the application shell without making the UI the source of alarm truth.

## Reusable engineering libraries

Reusable industrial structures are a product requirement, not a convenience feature.

EliteSCADA must evolve Equipment Templates/Equipment and Dynamos into a version-aware reusable library experience, conceptually similar in responsibility to class/instance systems such as Elipse E3 XObject/XControl while maintaining EliteSCADA's own contracts.

Required direction:

- reusable definitions with properties and bindings;
- application-specific instance context;
- nested reusable components;
- deterministic dependency validation;
- independent import/export of library definitions;
- controlled version update/migration;
- preservation of safe instance overrides.

## Cross-project copy/paste

Copy/paste between projects must use canonical **Engineering Fragments**, not opaque browser-only clipboard state.

The workflow must support dependency-aware preview for:

- create/update conflicts;
- missing dependencies;
- rebinding;
- selected-only copy;
- selected-with-required-dependencies copy.

Target domains include screens, popups, equipment, Dynamos and other engineering structures.

## Configurable application shell

Applications must support reusable/configurable persistent regions such as:

- header;
- footer;
- navigation;
- alarm banner/summary;
- optional side regions.

These are Engineering objects with controlled global/application/screen overrides. Common widgets include application identity, logged-in user, navigation, alarm summary and date/time.

## Date/time and device clocks

Date/time widgets support date+time, date-only and time-only presentation.

Required time sources:

- EliteSCADA server clock;
- TAG-provided time from PLC/RTU/other source.

Timezone and formatting are presentation concerns and must not alter timestamp semantics.

Future active synchronization of PLC/RTU clocks is a distinct industrial command, permission-controlled and auditable. Displaying a PLC clock TAG must never silently synchronize the device.

## Engineering/development interface localization

The **Engineering/development interface** must allow the developer/engineering user to choose the application UI language among:

- **Português** (Brazilian Portuguese / `pt-BR`);
- **English** (`en`);
- **Español** (`es`).

This language choice applies across the complete engineering/development environment, including at minimum:

- Data Sources and communication-driver configuration;
- TAG engineering;
- database/historian configuration and diagnostics;
- alarm engineering;
- Equipment Templates, Equipment and Dynamos;
- screen and popup creation/editing;
- trends;
- project save/revision/publish/activate workflows;
- users, roles and security administration;
- driver/module administration and diagnostics;
- Gateway/TAG Bridge configuration and diagnostics;
- validation messages, menus, property editors, dialogs and engineering help text provided by the product.

The selected interface language is a **presentation/user preference**. Changing it must never alter stable Engineering identifiers, TAG paths, addresses, internal enum values, public JSON/CSV/XLSX schema keys, revision identity or runtime semantics. Product code should use localization/resource keys rather than persist translated UI labels as authoritative configuration values.

This requirement concerns the EliteSCADA engineering/development UI. Multilingual text inside the **runtime HMI application being engineered** is a separate product capability and must not be assumed to be solved merely because the editor itself is localized.

The language preference should be persistable per user/profile when the user-lifecycle/profile subsystem exists, while the exact fallback/detection behavior is an implementation detail to define when localization is built.

## Editor direction

The editor must consume the same public Engineering model rather than maintain a private project representation.

Editor development may proceed incrementally on top of the established runtime/security/persistence foundation. Core workflows include reusable objects, Engineering Fragments, trends, access-aware visibility, Gateway/TAG Bridge engineering and configurable shell regions.

The graphical editor is an engineering client of the platform, not the platform's authority.

The editor and all other developer-facing Engineering surfaces must share the same localization infrastructure so the Portuguese/English/Spanish choice is consistent across the product instead of being implemented separately by each screen.

The current Engineering UI foundation implemented in PR #37 uses this localization model for `pt-BR`, `en` and `es`. Its structured TAG, Data Source and Alarm editors remain preview-only until a later secured Apply workflow is integrated.

## Development quality rules

- Prefer small coherent slices with automated validation.
- Do not merge known-failing changes into `main`.
- Use CI as the external .NET validation environment when the current ChatGPT runtime cannot execute .NET locally.
- Validate backend build/tests/runtime smoke, frontend build and browser E2E when the changed surface requires them.
- Fix root causes instead of disabling tests or weakening concurrency/security merely to obtain a green pipeline.
- Keep operational runtime safety ahead of convenience.
- Do not create placeholder security endpoints without an actual domain model behind them.
- Preserve backward compatibility of supported Engineering schema versions or introduce explicit migration behavior/tests.
- Documentation and roadmap updates must not accidentally erase locked future product requirements.
- Additional driver modules must not bypass Engineering validation, security, audit, TAG quality semantics or the DriverHost boundary.
- Protocol-independent Gateway routes must use common TAG/read/write/source-provider boundaries and must never couple concrete communication drivers directly.
- Client Memory must never be treated as trusted backend/global process state.
- Server-retained memory values must remain runtime state separate from immutable Engineering revision contents.
- Engineering UI localization must not leak translated presentation strings into stable public Engineering contracts or identifiers.
- Architecture order before adding new external protocols is locked as: **internal memory -> TAG-to-TAG Gateway -> common multi-driver diagnostics -> new external drivers/protocols**.
- Permanent architectural decisions must be consolidated into this official `main` document even before implementation; feature branches may elaborate them but must not be their sole durable home.

## Relationship to other repository documents

- `PROJECT GOAL.md`: persistent product memory, principles and locked requirements.
- `LAST CHANGE.md`: exact handoff point between tasks/conversations and explicit MERGED / IMPLEMENTED IN PR / SPECIFIED status.
- `docs/ROADMAP.md`: ordered implementation status and next development slices.
- `docs/ARCHITECTURE.md`: current architectural boundaries and data flow.
- `docs/ADR-*.md`: specific accepted architectural decisions.
- `docs/SECURITY-AUTHORIZATION-AUDIT.md`: security implementation boundary/details.
- `docs/VISUAL-COMPONENT-LIBRARY.md`: visual/reusable component direction.
- `docs/COMMUNICATION-DRIVER-DIAGNOSTICS.md`: multi-driver communication topology and diagnostic contract direction.
- `docs/INTERNAL-MEMORY-TAGS.md`: Client Memory and retentive Server Memory semantics.
- `docs/TAG-GATEWAY.md`: protocol-independent TAG-to-TAG gateway/bridge semantics.

When these documents evolve, they should remain consistent. `PROJECT GOAL.md` wins for explicitly locked product intent; repository code and current `main` win for what is actually implemented; `LAST CHANGE.md` records where work stopped and how to resume.
