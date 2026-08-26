# EliteSCADA Roadmap

Engineering Import/Export remains a mandatory cross-cutting capability throughout this roadmap. Every new Engineering domain must join the public versioned model, validation/preview/apply workflow, revision lifecycle and backup/restore path.

**Status date:** 2026-08-26
**Functional development:** ACTIVE

## Established `main` foundation

The following major capabilities are already integrated and validated:

1. Repository architecture and CI/CD foundation.
2. TAG Engine, quality model, current-value cache and Event Bus.
3. Simulation driver and common Driver SDK/DriverHost boundary.
4. REST API + WebSocket realtime runtime.
5. React runtime client baseline.
6. .NET build/test/runtime-smoke CI and Chromium E2E.
7. PostgreSQL Engineering persistence.
8. Working / immutable Revision / Published / Active project lifecycle.
9. Transactional activation and rollback/fail-closed restart recovery.
10. TimescaleDB historian baseline.
11. Real Modbus TCP runtime with grouped polling, writes, reconnect and communication quality.
12. Engineering Data Source compilation into runtime plans.
13. Isolated Engineering Workspace and transactional checkout/save lineage.
14. Capability-based authorization and append-oriented audit foundation.
15. Engineering Schema v7 with Security Roles and first-class Operational Commands.
16. Trusted JWT validation and backend protection of critical process/Engineering operations.
17. Protected runtime read, historian, alarm, Engineering/diagnostic and `/ws/tags` realtime surfaces.
18. Engineering UI foundation at `/engineering` with Runtime↔Engineering navigation and `pt-BR` / `en` / `es` localization.
19. Structured TAG, Data Source and Alarm editors with safe preview-oriented draft behavior.
20. Local identity/browser-login foundation with PBKDF2-SHA256 credentials, PostgreSQL user persistence, JWT issuance, HttpOnly browser cookie and bootstrap-first-user workflow.

Important merged PR checkpoints:

- PR #35 Commands: `2fd568976fc6277d0b069adeeb560f6ea3d8205f`.
- PR #36 Runtime read/realtime protection: `10b0320149c1ef2109e9517539717a8800b200c2`.
- PR #37 Engineering UI foundation: `4553aa7ab5ba7e05a209a7c8462286d1a34a1ad6`.
- PR #38 Local identity/browser login: `2a581d279a428cb605429d5939c333ff7ad8d1b4`.

## Engineering Import/Export baseline

Canonical Engineering JSON is at Schema v7.

Current public domains include:

- TAGs;
- Alarms;
- Data Sources/driver configuration;
- Equipment Templates;
- Equipment instances;
- Dynamos;
- Screens;
- Popups;
- Security Roles/policies;
- Operational Commands.

Current bulk/project exchange includes:

- CSV support for appropriate TAG/Alarm/Data Source workflows;
- historical-schema compatibility/migration testing;
- `.escadapkg` Engineering project package with integrity validation.

Future internal memory, Gateway, scripts, visual assets/property schemas, trends, shell regions and plugin-owned configuration must use the same public/versioned Engineering principles.

## Immediate ordered product-hardening slices

These are the next product-unblocking slices before the locked source/protocol milestone.

1. **User lifecycle and administration**
   - user list/profile/enable-disable/password reset/change as appropriate;
   - role-key assignment without moving role capability definitions out of Engineering;
   - `UserRoleAdmin`/`SystemAdmin` enforcement;
   - never expose password hash/salt through administrative APIs or Engineering export.

2. **Secured Engineering mutation workflows**
   - Apply for validated TAG/Data Source/Alarm drafts;
   - Delete lifecycle;
   - bulk-edit workflow where appropriate;
   - authorization/audit and dirty/revision semantics;
   - preserve the public preview/apply boundary.

3. **Audit durability and retention hardening**
   - retention/query policy;
   - bounded buffering/outbox behavior for temporary database/storage outages;
   - preserve append-only semantics and actor integrity.

4. **Historian retention/downsampling**
   - TimescaleDB retention policies;
   - aggregation/downsampling baseline;
   - clear separation between storage policy and public trend semantics.

## Locked source/protocol foundation

The order below is mandatory before adding another external protocol family:

5. **Internal memory TAG sources**
   - `builtin.memory.client`;
   - retentive `builtin.memory.server`;
   - typed initial/default values;
   - stable-ID retention and explicit incompatible-type reset/migration;
   - client/server scripting scope boundaries;
   - Engineering Import/Export.
   - See `docs/INTERNAL-MEMORY-TAGS.md`.

6. **Protocol-independent TAG Gateway**
   - TAG→TAG routes;
   - OnChange/Periodic;
   - deadband/minimum interval/coalescing;
   - `Good` quality default;
   - gain/offset transform;
   - cycle and multi-writer rejection;
   - independent route diagnostics;
   - transactional activation.
   - See `docs/TAG-GATEWAY.md`.

7. **Common multi-driver/Data-Source diagnostics**
   - per-Data-Source state/health;
   - success/failure/timeout/reconnect counters;
   - latency/failure-rate/data-age metrics where meaningful;
   - TAG quality aggregation;
   - isolated failure behavior;
   - protected diagnostic APIs and Engineering UI.
   - See `docs/COMMUNICATION-DRIVER-DIAGNOSTICS.md`.

8. **USER INTERFACE VALIDATION PREVIEW**
   - mandatory product-owner test build before the next external-protocol wave;
   - primary initial delivery target: Windows x64;
   - practical startup path, local login/bootstrap, demo project, visible version and short test checklist;
   - package/startup path must be smoke-tested separately from repository-only development execution;
   - feedback reviewed before heavy next-protocol investment.
   - See `docs/INTERFACE-VALIDATION-MILESTONE.md`.

Locked sequence:

`internal memory -> TAG Gateway -> common multi-driver diagnostics -> USER INTERFACE VALIDATION PREVIEW -> additional external protocols`

## External protocol/module wave after preview feedback

9. MQTT through the common Data Source/Source Provider/TAG/Gateway architecture.
10. OPC UA through the same common model.
11. BACnet through the same common model.
12. Installable/versioned Driver Module framework and public Driver SDK compatibility boundary.
13. Siemens S7 ISO Connection as the first intended installable module target after the framework is ready.
14. Later Allen-Bradley research based on public documentation/libraries, licensing, testability and representative hardware/simulator access.

Projects referencing missing/disabled/incompatible modules must preserve their Engineering configuration and surface diagnostics instead of silently deleting configuration.

## Python scripting and visual-runtime prerequisite

The graphical screen/popup/Dynamo editor has a separate mandatory prerequisite chain. It **must not** invent its own private object/property model first and retrofit scripting later.

Before full graphical visual engineering begins, implement:

15. **Python scripting public contract + visual property schema**
   - first-class versioned Script Engineering entities;
   - explicit Client Visual vs Server Script scopes;
   - stable visual object IDs/types/property keys;
   - typed property metadata defining Engineering editability, runtime readability/writability, binding support and animatability;
   - common properties including x/y, width/height, line thickness, fill/background color, line color, opacity, visibility, rotation, z-order, text/font and image/resource properties where applicable.

16. **Python script editor + sandbox**
   - syntax highlighting and line numbers;
   - syntax/compile diagnostics with line/column;
   - EliteSCADA API autocomplete where practical;
   - event/entry-point association;
   - sandboxed test/preview;
   - execution budgets, cancellation and error isolation.

17. **Visual runtime object instances/property API**
   - design-time Engineering base values separate from runtime presentation overrides;
   - scripts can change declared runtime-writable object properties without silently changing saved Engineering revisions;
   - deterministic lifecycle/disposal of subscriptions and timers;
   - event model for load/unload, object interaction, TAG/Client-Memory changes and timers;
   - renderer-native tween/animation primitives callable from Python for smooth procedural animation;
   - deterministic, diagnosable precedence among base values, TAG/expression bindings, script writes and animations.

18. **Graphical screen/popup/Dynamo editor**
   - object palette/canvas;
   - property inspector consuming the same public property schema as Python;
   - TAG/expression bindings;
   - images/resources;
   - links/navigation/popups;
   - script/event association;
   - reusable Dynamo/template composition.

19. **Advanced reusable visual libraries**
   - nested Dynamos/components;
   - version-aware library update/migration;
   - reusable scripted behavior;
   - controlled instance overrides.

Full locked scripting/visual contract: `docs/PYTHON-SCRIPTING-AND-VISUAL-RUNTIME.md`.

The interface-validation preview in item 8 may occur before items 15–19. The requirement is that items 15–17 occur before item 18.

## Additional future Engineering/product slices

These remain locked product goals and may be scheduled around the major gates according to dependencies:

- Engineering XLSX workbook import/export;
- Engineering Fragments and dependency-aware cross-project copy/paste;
- configurable application shell with header/footer/navigation/alarm summary/optional side regions;
- multi-Pen engineered, ad-hoc and saved trends;
- historical/realtime trend integration and expressions where appropriate;
- visual asset/resource package management;
- reusable Equipment/Template/Dynamo class-instance libraries;
- runtime-HMI multilingual content as a separate capability from Engineering UI localization;
- complete `pt-BR` / `en` / `es` coverage of Engineering/development UI;
- public SDK/module lifecycle, trust/integrity and diagnostics;
- later sandboxed Server Python scripting for shared calculations/automation using Server Memory/shared TAGs.

## Locked scripting/visual architecture summary

Client Visual Scripts:

- run in the Runtime Client;
- may read permitted shared TAGs and read/write that client's Client Memory;
- may change explicitly runtime-writable properties of current screen/popup/Dynamo object instances;
- operate through a stable EliteSCADA object API, not React/DOM internals;
- may request protected backend writes/commands only through normal authorization boundaries;
- cannot access arbitrary OS/filesystem/network/secrets/drivers/database.

Server Scripts:

- are a separate server-owned capability;
- may use shared TAGs/Server Memory under an explicit security model;
- never manipulate one browser's visual instances or use one client's Client Memory as global truth.

Runtime visual property overrides never silently mutate immutable/saved Engineering configuration.

## Development quality rules

- Prefer small coherent slices with automated validation.
- Never merge a known-failing change into `main`.
- Use GitHub CI as the external .NET validation environment when local .NET execution is unavailable.
- Validate backend build/tests/runtime smoke, web build and Chromium E2E for affected surfaces.
- Fix root causes rather than weakening tests/security/concurrency.
- Preserve supported Engineering schema compatibility or explicit migrations/tests.
- Keep runtime safety ahead of convenience.
- New drivers, scripts and UI editors must not bypass Engineering, TAG quality, security, audit or lifecycle boundaries.
- Documentation changes must preserve locked future requirements.
