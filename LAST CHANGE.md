# LAST CHANGE — EliteSCADA

> Operational handoff between tasks and ChatGPT conversations.
>
> **Mandatory:** read this file together with `PROJECT GOAL.md` before starting any EliteSCADA task. Update this file immediately before the final user-facing response of every EliteSCADA task.

**Handoff date:** 2026-08-26
**Development state:** PAUSED until the user explicitly resumes development in another ChatGPT conversation.

## Latest user instruction — cross-chat resume

The user explicitly stated that he will open **another ChatGPT chat** and there issue the command to continue EliteSCADA development.

This instruction is now part of the operational handoff:

- do **not** resume product implementation in the current chat merely because the project state is known;
- the current development pause remains in effect until the user gives an explicit resume/continue command in the new chat;
- when that new chat begins an EliteSCADA task, first read `PROJECT GOAL.md` and this `LAST CHANGE.md` before planning or changing code;
- after reading the continuity files, fetch the live `main` HEAD and `docs/ROADMAP.md`, then continue from repository truth rather than from remembered chat state;
- the expected immediate technical sequence on resume remains the operational command/security slice described below unless the user gives a different instruction.

The purpose of this note is specifically to make the transition to a new conversation safe. A new chat should not ask the user to reconstruct the project history.

## Repository state observed for this handoff

Live `main` HEAD observed immediately before writing this update:

`709347a5809cf16542ad19997bda1ee7bc86bc43` — project progress assessment handoff.

This `LAST CHANGE.md` update creates a newer commit. Always fetch live `main` HEAD again in the next task.

## Current project progress assessment

Use percentages only as weighted planning estimates, not contractual metrics:

- **Core/backend/platform foundation:** approximately **75–80% complete**.
- **Industrial MVP/pilot foundation with current Modbus path:** approximately **65–70% complete**.
- **Full currently locked product scope:** approximately **40–45% complete**.
- Therefore approximately **55–60% of the full defined product effort remains**.

The project is beyond proof-of-concept. The backend/runtime/engineering foundation is strong; the next major phase is productization through Engineering UI/editor, reusable visual components/Dynamos, trends, real user lifecycle, broader protocols, installable Driver Modules, localization and operational hardening.

## Strongly implemented foundation

Current `main` already contains/records:

- TAG Engine, quality model, current cache and Event Bus;
- REST API and WebSocket realtime path;
- simulation runtime;
- real Modbus TCP driver with FC01/02/03/04/05/06/16, polling, writes, reconnect and communication quality;
- Data Source compiler to executable runtime plans;
- PostgreSQL Engineering revision persistence;
- isolated Engineering Workspace;
- Working/Published/Active lifecycle;
- transactional candidate activation and rollback;
- fail-closed recovery of Active Revision;
- TimescaleDB historian baseline;
- canonical Engineering JSON schema v6;
- Import/Export for TAGs, alarms, Data Sources, Equipment Templates/Equipment, Dynamos, Screens, Popups and security roles;
- CSV engineering for TAGs/alarms/Data Sources;
- `.escadapkg` engineering backup/restore;
- schema migration/backward-compatibility tests;
- capability/scoped authorization model;
- trusted JWT validation adapter;
- durable append-only PostgreSQL audit trail;
- secured TAG writes, alarm ACK, alarm shelving/unshelving and Engineering/persistence lifecycle mutations;
- automated .NET tests, runtime smoke and Chromium E2E infrastructure.

## Immediate next product slice when the user resumes

Unless the user changes priority in the new chat, continue with:

1. introduce a **first-class operational command domain**;
2. enforce and audit `CommandExecute` against real command objects;
3. extend authorization to sensitive read/realtime/WebSocket surfaces;
4. then continue user/login lifecycle, audit durability/retention, historian retention/downsampling and subsequent product/editor/protocol slices according to `docs/ROADMAP.md`.

Important architectural rule: do not create a fake/placeholder command endpoint merely to exercise `CommandExecute`; the command domain must exist first.

## Major remaining product blocks

Still substantially outstanding:

- real login/token issuance or external IdP workflow and user lifecycle administration;
- audit buffering/outbox/retention;
- historian retention/downsampling;
- full Engineering/development UI for Data Sources/drivers, TAGs, historian, alarms, security, revisions and administration;
- graphical SVG screen/popup editor consuming the public Engineering model;
- reusable Dínamo/component library, bindings, faceplates, commands and setpoints;
- full multi-Pen engineered/ad-hoc/saved trends;
- persistent configurable application shell;
- reusable libraries with controlled migration and instance overrides;
- Engineering Fragments and dependency-aware cross-project copy/paste;
- MQTT;
- OPC UA;
- BACnet;
- installable/versioned Driver Module framework;
- Siemens S7 ISO Connection as first intended installable driver module, including later Node-RED/open-source/license research;
- Allen-Bradley as later research/module target;
- Portuguese (`pt-BR`), English (`en`) and Spanish (`es`) Engineering UI localization;
- XLSX Engineering import/export;
- runtime diagnostics, driver health, offline behavior and industrial hardening;
- later sandboxed Python/public SDK expansion where applicable.

## Current industrial communication north

- Modbus TCP: implemented real-driver baseline.
- MQTT: locked future target.
- OPC UA: locked future target.
- BACnet: locked future target.
- Installable/versioned Driver Module framework: locked requirement.
- First intended installable module: Siemens S7 ISO Connection.
- Allen-Bradley: later research/module target.
- The >90% controller-market-coverage idea is a planning hypothesis and must be validated before external use as a statistic.

For protocol/module work, read `docs/ADR-007-DRIVER-MODULES-AND-PROTOCOLS.md` before designing or coding.

## Current Engineering UI north

The developer-facing Engineering interface must eventually cover Data Sources/drivers, TAGs, historian/database, alarms, Equipment/Templates/Dynamos, screens/popups, trends, security/users, revision lifecycle, modules and diagnostics.

The developer must be able to select the Engineering UI language among Portuguese (`pt-BR`), English (`en`) and Spanish (`es`). Localization is presentation only and must not change Engineering identifiers/contracts/runtime semantics.

For localization work, read `docs/ADR-008-ENGINEERING-UI-LOCALIZATION.md`.

## Current functional milestone

The latest functional/product milestone before the documentation/continuity work remains:

`fdaa093f8ba735e447cb871beaf515f4417e7559` — `Secure alarm shelving lifecycle`.

Later commits primarily updated persistent project goals, ADRs, roadmap and continuity documentation.

## Resume checklist for the NEW CHAT

Before doing anything else on EliteSCADA:

1. Read `PROJECT GOAL.md` completely.
2. Read this `LAST CHANGE.md` completely.
3. Fetch live `main` HEAD and recent commits.
4. Read `docs/ROADMAP.md` for ordered implementation status.
5. Read the relevant ADR/security/Engineering documents for the affected slice.
6. For visual/editor work, read `docs/VISUAL-COMPONENT-LIBRARY.md`.
7. For protocol/module work, read ADR-007.
8. For localization work, read ADR-008.
9. Do not rely on old branch/chat assumptions.
10. Validate implementation through GitHub CI when .NET cannot be executed locally in the ChatGPT environment.
11. Immediately before every final user-facing response, update this file again.

## Permanent continuity rule

- `PROJECT GOAL.md` = persistent global project memory/product north.
- `LAST CHANGE.md` = exact stopping/resume checkpoint.

This rule remains in force until the user explicitly changes it.