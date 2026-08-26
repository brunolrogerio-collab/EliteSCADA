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

Mandatory runtime flow:

`Device/Source -> Driver -> TAG Engine / Current Cache -> Event Bus -> Historian / Alarm Engine / Realtime / Scripts`

The frontend never accesses industrial drivers directly, and drivers never depend on screens/UI.

## Technology baseline

Current technology north:

- Backend/Core: .NET 10 LTS.
- Frontend: React + TypeScript.
- Configuration/persistence: PostgreSQL.
- Historian: PostgreSQL + TimescaleDB.
- Realtime client transport: WebSocket.
- Public integration: REST API.
- Industrial protocol/messaging expansion: MQTT and OPC UA.
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

1. **TAGs**: stable ID, path, name, data type, unit, description, Data Source, address, scaling, deadband, historian policy, permissions/access policy and metadata.
2. **Alarms**: associated TAG, type, limits/setpoint, priority, class, area, message, delay, ACK behavior, shelving policy and related metadata.
3. **Data Sources / Drivers**: technical communication configuration and bindings.
4. **Equipment Templates and Equipment instances**: reusable class/instance engineering structures, bindings, properties and context.
5. **Dynamos / reusable visual definitions**: bindings, properties, context and dependencies.
6. **Screens and Popups**: routes, visual definitions, bindings, context and reusable dependencies.
7. **Security Roles / Policies**: capabilities and scopes used by the application.
8. Future engineering domains such as trends, shell regions, commands, libraries and plugins must join the same versioned public model when introduced.
9. Plugin-owned driver/Data Source configuration must expose a public versioned schema so it participates in Engineering validation, import/export, backup/restore and migration without becoming opaque private state.

### Secrets rule

Passwords, authentication tokens, private keys and other secrets must never be serialized in plaintext inside Engineering packages.

Technical configuration uses protected secret references such as environment/vault/key-vault style references. Engineering packages may carry authorization policies, but never user passwords/password hashes or equivalent authentication secrets.

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

## Industrial communication

Drivers are accessed through common contracts and Data Source engineering definitions.

Current baseline includes real Modbus TCP runtime support with polling, writes, reconnect and communication-quality behavior. New protocols must follow the same separation between Engineering configuration, compiled runtime plan and driver execution.

The locked protocol direction includes:

- **Modbus TCP** as the currently implemented first real industrial driver;
- **MQTT** as a planned first-class communication/messaging integration;
- **OPC UA** as a planned first-class industrial interoperability protocol;
- additional protocols supplied by first-party or third-party **installable driver modules** through the same public Driver SDK boundary.

MQTT, OPC UA and future modules must use the same Data Source / TAG / Engineering model rather than create protocol-specific configuration islands.

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

EliteSCADA must evolve Equipment Templates/Equipment and Dynamos into version-aware reusable libraries, conceptually similar in responsibility to class/instance systems such as Elipse E3 XObject/XControl while maintaining EliteSCADA's own contracts.

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

## Editor direction

The editor must consume the same public Engineering model rather than maintain a private project representation.

Editor development may proceed incrementally on top of the established runtime/security/persistence foundation. Core workflows include reusable objects, Engineering Fragments, trends, access-aware visibility and configurable shell regions.

The graphical editor is an engineering client of the platform, not the platform's authority.

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

## Relationship to other repository documents

- `PROJECT GOAL.md`: persistent product memory, principles and locked requirements.
- `LAST CHANGE.md`: exact handoff point between tasks/conversations.
- `docs/ROADMAP.md`: ordered implementation status and next development slices.
- `docs/ARCHITECTURE.md`: current architectural boundaries and data flow.
- `docs/ADR-*.md`: specific accepted architectural decisions.
- `docs/SECURITY-AUTHORIZATION-AUDIT.md`: security implementation boundary/details.
- `docs/VISUAL-COMPONENT-LIBRARY.md`: visual/reusable component direction.

When these documents evolve, they should remain consistent. `PROJECT GOAL.md` wins for explicitly locked product intent; repository code and current `main` win for what is actually implemented; `LAST CHANGE.md` records where work stopped and how to resume.