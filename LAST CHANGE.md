# LAST CHANGE — EliteSCADA

> Operational handoff between tasks and ChatGPT conversations.
>
> **Mandatory:** read this file together with `PROJECT GOAL.md` before starting any EliteSCADA task. Update this file immediately before the final user-facing response of every EliteSCADA task.

**Handoff date:** 2026-08-26
**Development state:** PAUSED by explicit user request.

## Latest task

The user explicitly instructed the project goal to be updated so that MQTT, OPC UA and installable modules for additional communication drivers are formal product requirements.

No runtime/product code was developed. This task changed project documentation/architecture only, and the development pause remains in effect.

### Changes made on `main`

1. `PROJECT GOAL.md` was updated to lock the following requirements:
   - MQTT as a planned first-class industrial communication/messaging integration;
   - OPC UA as a planned first-class industrial interoperability protocol;
   - ability to add first-party and third-party communication drivers through installable/versioned modules using the common Driver SDK/DriverHost boundary;
   - plugin-owned Data Source/driver configuration must expose a public versioned Engineering schema and participate in validation, import/export, backup/restore and migration;
   - module lifecycle must include installation, removal, enable/disable, upgrade, compatibility validation and explicit diagnostics;
   - project Engineering configuration must be preserved if a required module is missing, disabled or incompatible;
   - module trust/integrity must be checked before executable code is enabled;
   - module administration is security-sensitive and must be permission-controlled/auditable when implemented.

2. Created `docs/ADR-007-DRIVER-MODULES-AND-PROTOCOLS.md`, an accepted architectural decision that defines:
   - Modbus TCP as current implemented baseline;
   - MQTT and OPC UA as explicit future protocol targets;
   - installable driver modules as a locked product capability;
   - common Data Source/TAG/quality/Engineering boundaries for all protocols;
   - minimum module manifest/lifecycle/compatibility/trust expectations;
   - preservation of configuration for unavailable modules;
   - deferred details such as physical package format, exact signing policy, distribution/catalog UX and isolation strategy.

3. `docs/ROADMAP.md` was updated:
   - MQTT remains an explicit future slice;
   - OPC UA now has its own explicit future slice;
   - a dedicated installable/versioned Driver Module framework slice was added;
   - a new locked future-requirement section documents the protocol/module rules and points to ADR-007.

### Repository state before this LAST CHANGE update

Live `main` HEAD observed immediately before updating this file:

`7cfdef1fbbf2054e9f262479d50727851f914c96` — `Add OPC UA and installable driver modules to roadmap`

This `LAST CHANGE.md` update creates a newer commit. Always fetch live `main` HEAD on the next task.

## Current product direction for industrial communication

Locked direction now is:

- Modbus TCP: implemented real-driver baseline;
- MQTT: planned first-class integration;
- OPC UA: planned first-class integration;
- additional protocols: installable first-party/third-party driver modules;
- all of them must use the common EliteSCADA Engineering/Data Source/TAG/runtime model;
- no driver module may bypass TAG quality semantics, alarms, historian, security, audit or project persistence.

The exact implementation details of the module packaging/catalog/signing/isolation mechanism remain intentionally deferred to the dedicated implementation slice. Do not invent them prematurely as if already decided.

## Previous functional milestone

The latest functional/product milestone before the continuity/documentation work remains:

`fdaa093f8ba735e447cb871beaf515f4417e7559` — `Secure alarm shelving lifecycle`

Alarm shelving is already integrated into `main`.

## Current development pause

The user previously requested development to stop after a ChatGPT/platform error.

**Do not continue product implementation automatically.**

Documentation/goal maintenance is allowed when explicitly requested. New product development resumes only after an explicit instruction to continue.

## Next product slice when development is explicitly resumed

According to the current roadmap, the immediate next technical slice remains:

1. introduce a first-class operational command domain;
2. enforce/audit `CommandExecute` against real command objects;
3. extend authorization to sensitive read/realtime/WebSocket surfaces.

Later roadmap slices now explicitly include historian retention/downsampling, MQTT, OPC UA, the installable Driver Module framework, XLSX Engineering, diagnostics and frontend hardening.

## Resume checklist

Before any EliteSCADA task:

1. Read `PROJECT GOAL.md` completely.
2. Read this `LAST CHANGE.md` completely.
3. Fetch live `main` HEAD/recent commits when repository state matters.
4. Read `docs/ROADMAP.md` when planning implementation.
5. For protocol/module work, read `docs/ADR-007-DRIVER-MODULES-AND-PROTOCOLS.md` before designing or coding.
6. Read other relevant ADR/security/Engineering documents for the affected domain.
7. Do not rely on old chat/branch assumptions.
8. Validate implementation through GitHub CI when .NET cannot be executed locally in the ChatGPT environment.
9. Immediately before the final user-facing response, update this file again.

## Permanent continuity rule

- `PROJECT GOAL.md` = persistent global project memory/product north.
- `LAST CHANGE.md` = exact stopping/resume checkpoint.

This rule remains in force until the user explicitly changes it.