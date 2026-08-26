# EliteSCADA Roadmap

Engineering Import/Export remains a mandatory cross-cutting capability throughout this roadmap. Every new Engineering domain must join the public versioned model, validation/preview/apply workflow, revision lifecycle and backup/restore path.

**Status date:** 2026-08-26
**Functional development:** ACTIVE — PARALLEL WORK ENABLED

## Established `main` foundation

The following major capabilities are integrated and validated:

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
21. Protected local-user administration with safe DTOs, Engineering role-key assignment, `UserRoleAdmin` / `SystemAdmin`, last-admin protection, JWT security-version invalidation and active WebSocket session revocation.

Important merged PR checkpoints:

- PR #35 Commands: `2fd568976fc6277d0b069adeeb560f6ea3d8205f`.
- PR #36 Runtime read/realtime protection: `10b0320149c1ef2109e9517539717a8800b200c2`.
- PR #37 Engineering UI foundation: `4553aa7ab5ba7e05a209a7c8462286d1a34a1ad6`.
- PR #38 Local identity/browser login: `2a581d279a428cb605429d5939c333ff7ad8d1b4`.
- PR #39 Local user administration: `6de8f06a443ad829ccc95c6dfcd9511e906adeff`.

## Engineering Import/Export baseline

Canonical Engineering JSON is currently Schema v7.

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

Current bulk/project exchange includes CSV for appropriate TAG/Alarm/Data Source workflows, historical-schema compatibility/migration testing, and `.escadapkg` Engineering project packages with integrity validation.

Future Internal Memory, Gateway, Scripts, visual assets/property schemas, trends, shell regions and plugin-owned configuration must use the same public/versioned Engineering principles.

## Immediate coordinator-owned hardening

These slices can progress independently of the worker branches when file ownership is respected.

1. **Secured Engineering mutation workflows**
   - Apply for validated TAG/Data Source/Alarm drafts;
   - Delete lifecycle;
   - bulk-edit workflow where appropriate;
   - `EngineeringModify` authorization and audit;
   - dirty/revision semantics;
   - preserve the public `parse -> validate -> preview -> apply` boundary;
   - never create a browser-private source of Engineering truth.

2. **Audit durability and retention hardening**
   - retention/query policy;
   - bounded buffering/outbox behavior for temporary database/storage outages;
   - preserve append-only semantics and actor integrity.

3. **Historian retention/downsampling**
   - TimescaleDB retention policies;
   - aggregation/downsampling baseline;
   - clear separation between storage policy and public trend semantics.

## Parallel foundations currently implemented in Draft PRs

### PR #40 — Internal Memory / Source Provider foundation

**IMPLEMENTED IN PR / NOT MERGED**.

The isolated Worker A slice covers Source Provider contracts, strict typed memory defaults, stable-ID server retention semantics, deterministic in-memory retention, `Good` quality, fail-closed incompatible types, deleted-TAG non-resurrection and per-client Client Memory isolation.

Before merge it must be reconciled with then-current `main`, pass final integrated CI and receive coordinator-owned Engineering/runtime integration where appropriate.

### PR #41 — Python scripting + visual property foundation

**IMPLEMENTED IN PR / NOT MERGED**.

The isolated Worker B slice covers typed visual-property schemas, base/binding/script/animation precedence, runtime presentation overrides, tween contracts, Client Visual vs Server script scopes, sandbox capability boundaries, bounded execution contracts and Python editor-validation diagnostics.

Central Engineering schema/import-export and concrete Python/renderer/editor integration remain coordinator/later slices.

## Locked source/protocol foundation

The order below is mandatory before adding another external protocol family:

4. **Internal memory TAG sources — complete product integration**
   - integrate the PR #40 foundation after review;
   - `builtin.memory.client`;
   - retentive `builtin.memory.server`;
   - typed initial/default values in public Engineering;
   - stable-ID retention and explicit incompatible-type reset/migration;
   - runtime cache/Event Bus/realtime integration;
   - historian/alarm rules;
   - durable retention implementation;
   - client/server scripting scope boundaries;
   - Engineering Import/Export.
   - See `docs/INTERNAL-MEMORY-TAGS.md`.

5. **Protocol-independent TAG Gateway**
   - TAG→TAG routes;
   - OnChange/Periodic;
   - deadband/minimum interval/coalescing;
   - `Good` quality default;
   - gain/offset transform;
   - cycle and multi-writer rejection;
   - independent route diagnostics;
   - transactional activation.
   - See `docs/TAG-GATEWAY.md`.

6. **Common multi-driver/Data-Source diagnostics**
   - per-Data-Source state/health;
   - success/failure/timeout/reconnect counters;
   - latency/failure-rate/data-age metrics where meaningful;
   - TAG quality aggregation;
   - isolated failure behavior;
   - protected diagnostic APIs and Engineering UI.
   - See `docs/COMMUNICATION-DRIVER-DIAGNOSTICS.md`.

7. **USER INTERFACE VALIDATION PREVIEW**
   - mandatory product-owner test build before the next external-protocol wave;
   - primary initial target: Windows x64;
   - practical startup path, local login/bootstrap, demo project, visible version and short test checklist;
   - package/startup path smoke-tested separately from repository-only development execution;
   - product-owner feedback reviewed before heavy next-protocol investment.
   - See `docs/INTERFACE-VALIDATION-MILESTONE.md`.

Locked sequence:

`internal memory -> TAG Gateway -> common multi-driver diagnostics -> USER INTERFACE VALIDATION PREVIEW -> additional external protocols`

## External protocol/module wave after preview feedback

8. MQTT through the common Data Source/Source Provider/TAG/Gateway architecture.
9. OPC UA through the same common model.
10. BACnet through the same common model.
11. Installable/versioned Driver Module framework and public Driver SDK compatibility boundary.
12. Siemens S7 ISO Connection as the first intended installable module target after the framework is ready.
13. Later Allen-Bradley research based on public documentation/libraries, licensing, testability and representative hardware/simulator access.

Projects referencing missing/disabled/incompatible modules must preserve their Engineering configuration and surface diagnostics instead of silently deleting configuration.

## Python scripting and visual-runtime prerequisite

The graphical screen/popup/Dynamo editor has a separate mandatory prerequisite chain. It must not invent a private object/property model first and retrofit scripting later.

14. **Python scripting public contract + visual property schema**
   - integrate/review PR #41 foundation;
   - first-class versioned Script Engineering entities;
   - explicit Client Visual vs Server Script scopes;
   - stable visual object IDs/types/property keys;
   - typed property metadata defining Engineering editability, runtime readability/writability, binding support and animatability;
   - common geometry, line, fill/background, opacity, visibility, transform, text/font and image/resource properties.

15. **Python script editor + sandbox**
   - syntax highlighting and line numbers;
   - engine-backed syntax/compile diagnostics with line/column;
   - EliteSCADA API autocomplete where practical;
   - event/entry-point association;
   - sandboxed test/preview;
   - execution budgets, cancellation and error isolation.

16. **Visual runtime object instances/property API**
   - Engineering base values separate from Runtime presentation overrides;
   - scripts can change declared runtime-writable properties without mutating saved revisions;
   - deterministic lifecycle/disposal of subscriptions and timers;
   - load/unload, interaction, TAG/Client-Memory and timer event model;
   - renderer-native tween/animation primitives callable from Python;
   - deterministic, diagnosable precedence among base, binding/expression, script and animation layers.

17. **Graphical screen/popup/Dynamo editor**
   - object palette/canvas;
   - property inspector consuming the same public property schema as Python;
   - TAG/expression bindings;
   - images/resources;
   - links/navigation/popups;
   - script/event association;
   - reusable Dynamo/template composition.

18. **Advanced reusable visual libraries**
   - nested Dynamos/components;
   - version-aware library update/migration;
   - reusable scripted behavior;
   - controlled instance overrides.

Full locked scripting/visual contract: `docs/PYTHON-SCRIPTING-AND-VISUAL-RUNTIME.md`.

The interface-validation preview in item 7 may occur before items 14–18. Items 14–16 are mandatory before item 17.

## Additional future Engineering/product slices

These remain locked product goals and may be scheduled around major gates according to dependencies:

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

## Development quality rules

- Prefer small coherent slices with automated validation.
- Never merge a known-failing change into `main`.
- Use GitHub CI as the external .NET validation environment when local .NET execution is unavailable.
- Validate backend build/tests/runtime smoke, web build and Chromium E2E for affected surfaces.
- Fix root causes rather than weakening tests/security/concurrency.
- Preserve supported Engineering schema compatibility or explicit migrations/tests.
- Keep runtime safety ahead of convenience.
- New drivers, scripts and UI editors must not bypass Engineering, TAG quality, security, audit or lifecycle boundaries.
- Parallel worker branches never self-merge and coordinator-owned shared files remain centrally controlled.
