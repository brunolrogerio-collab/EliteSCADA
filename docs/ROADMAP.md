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
14. Capability-based authorization and durable append-only Audit foundation.
15. Engineering Schema v7 with Security Roles and first-class Operational Commands.
16. Trusted JWT validation and backend protection of critical process/Engineering operations.
17. Protected runtime read, historian, alarm, Engineering/diagnostic and `/ws/tags` realtime surfaces.
18. Engineering UI foundation at `/engineering` with Runtime↔Engineering navigation and `pt-BR` / `en` / `es` localization.
19. Structured TAG, Data Source and Alarm editors.
20. Local identity/browser-login foundation with PBKDF2-SHA256 credentials, PostgreSQL user persistence, JWT issuance, HttpOnly browser cookie and bootstrap-first-user workflow.
21. Protected local-user administration with safe DTOs, Engineering role-key assignment, `UserRoleAdmin` / `SystemAdmin`, last-admin protection, JWT security-version invalidation and active WebSocket session revocation.
22. Protocol-neutral Internal Memory / Source Provider foundation with `builtin.memory.server` and `builtin.memory.client`, typed defaults, stable-ID retention semantics and per-client isolation.
23. Python Scripting + Visual Property foundation with typed public visual properties, runtime presentation overrides, script scopes, sandbox boundaries, tween contracts, runtime instances, event queues and diagnostics.
24. Secured backend-authoritative Engineering Apply/Delete/Bulk mutation workflows with workspace version/CAS protection, dependency-aware delete, authorization, audit and UI confirmation/preview gates.
25. Historian retention/downsampling foundation with typed policies, 1m/5m/15m/1h aggregates, quality-aware aggregation, Timescale continuous aggregates and explicit destructive-retention approval.
26. Audit durability/query/retention foundation with stable keyset pagination, bounded query policy, sanitization, controlled retention and bounded asynchronous outage buffer.
27. Audit runtime integration with configured query/retention/buffer policies, `BufferedAuditSink`, protected Audit diagnostics, keyset cursor headers and periodic retention hosted service.

Important merged PR checkpoints:

- PR #35 Commands: `2fd568976fc6277d0b069adeeb560f6ea3d8205f`.
- PR #36 Runtime read/realtime protection: `10b0320149c1ef2109e9517539717a8800b200c2`.
- PR #37 Engineering UI foundation: `4553aa7ab5ba7e05a209a7c8462286d1a34a1ad6`.
- PR #38 Local identity/browser login: `2a581d279a428cb605429d5939c333ff7ad8d1b4`.
- PR #39 Local user administration: `6de8f06a443ad829ccc95c6dfcd9511e906adeff`.
- PR #40 Internal Memory foundation: `bb38617c9c27cb5c379973a6f65d66006f24eadc`.
- PR #41 Python/Visual foundation: `fc0731309d5b92d302f019d06d3511d3a247b607`.
- PR #43 Historian retention/downsampling foundation: `0c5f2aefdd5a7286c0c9367569067e2d12091c81`.
- PR #42 Secured Engineering mutations: `6d49b99181fce6dabce838822ce972332e2f77f0`.
- PR #44 Audit durability/query/retention foundation: `9406fb2d66c682bd6bde08a0facde0622aa86ff2`.
- PR #45 Audit runtime integration: `889c989fdce26d8593e86e430e76417412846400`.

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

Future Internal Memory public configuration, Gateway, Scripts, visual assets/property schemas, trends, shell regions and plugin-owned configuration must use the same public/versioned Engineering principles.

## Completed hardening wave

The previous coordinator/worker wave is now merged:

1. **Secured Engineering mutation workflows** — merged through PR #42.
2. **Audit durability/query/retention foundation** — merged through PR #44.
3. **Audit runtime integration** — merged through PR #45.
4. **Historian retention/downsampling foundation** — merged through PR #43.
5. **Internal Memory Source Provider foundation** — merged through PR #40.
6. **Python/Visual public-contract foundation** — merged through PR #41.

These merged foundations do not imply that every associated product integration item below is complete. Product status is determined by the detailed roadmap block, not by the existence of a foundation class or contract.

## Locked source/protocol foundation

The order below is mandatory before adding another external protocol family.

### 1. Internal Memory TAG sources — complete product integration

**NEXT LOCKED SOURCE/PROTOCOL BLOCK.**

The PR #40 foundation is merged. Remaining product integration includes:

- public/versioned Engineering representation for `builtin.memory.client` and `builtin.memory.server`;
- typed initial/default value in canonical Engineering;
- schema migration/import/export/preview/apply;
- runtime compilation/composition from Engineering Data Sources into memory Source Providers;
- shared TAG cache/Event Bus/realtime integration for Server Memory;
- explicit Client Memory session ownership and prohibition from global historian/alarm semantics;
- historian/alarm rules for Server Memory;
- durable production Server Memory retention implementation;
- stable-ID retention across rename/revision transitions;
- explicit incompatible-type reset/migration behavior;
- authorization/audit for external Server Memory writes and future script APIs;
- practical Engineering UI configuration as appropriate.

See `docs/INTERNAL-MEMORY-TAGS.md`.

### 2. Protocol-independent TAG Gateway

Only after the Internal Memory product integration above is complete:

- TAG→TAG routes;
- OnChange/Periodic;
- deadband/minimum interval/coalescing;
- `Good` quality default;
- gain/offset transform;
- cycle and multi-writer rejection;
- independent route diagnostics;
- transactional activation;
- public/versioned Engineering and Import/Export.

See `docs/TAG-GATEWAY.md`.

### 3. Common multi-driver/Data-Source diagnostics

Only after the Gateway foundation is integrated:

- per-Data-Source state/health;
- success/failure/timeout/reconnect counters;
- latency/failure-rate/data-age metrics where meaningful;
- TAG quality aggregation;
- isolated failure behavior;
- protected diagnostic APIs and Engineering UI.

See `docs/COMMUNICATION-DRIVER-DIAGNOSTICS.md`.

### 4. USER INTERFACE VALIDATION PREVIEW

Mandatory product-owner test build before the next external-protocol wave:

- primary initial target: Windows x64;
- practical startup path;
- local login/bootstrap;
- demo project;
- visible version;
- short validation checklist;
- package/startup smoke tested separately from repository-only development execution;
- product-owner feedback reviewed before heavy next-protocol investment.

See `docs/INTERFACE-VALIDATION-MILESTONE.md`.

Locked sequence:

`internal memory -> TAG Gateway -> common multi-driver diagnostics -> USER INTERFACE VALIDATION PREVIEW -> additional external protocols`

## External protocol/module wave after preview feedback

5. MQTT through the common Data Source/Source Provider/TAG/Gateway architecture.
6. OPC UA through the same common model.
7. BACnet through the same common model.
8. Installable/versioned Driver Module framework and public Driver SDK compatibility boundary.
9. Siemens S7 ISO Connection as the first intended installable module target after the framework is ready.
10. Later Allen-Bradley research based on public documentation/libraries, licensing, testability and representative hardware/simulator access.

Projects referencing missing/disabled/incompatible modules must preserve Engineering configuration and surface diagnostics instead of silently deleting configuration.

## Historian and trend evolution

The PR #43 storage foundation is merged, but these product integrations remain:

- public/versioned Engineering representation for retention/downsampling storage policy;
- canonical validation/import-export/schema migration;
- central Historian configuration/DI application from approved Engineering/runtime configuration;
- explicit handling/migration policy for legacy raw rows whose data type is unknown;
- history/trend resolution selection between raw and aggregate data;
- engineered, ad-hoc and saved multi-Pen trends;
- historical + live trend integration;
- expressions where appropriate.

TAG definition deletion remains distinct from historian data purge.

## Audit evolution

PRs #44 and #45 provide the current merged Audit durability/runtime baseline:

- append-only PostgreSQL storage;
- bounded keyset query and filters;
- protected diagnostics;
- configurable retention;
- bounded asynchronous in-memory outage buffering;
- storage-boundary sensitive-metadata sanitization.

Still not implemented/claimed:

- persistent disk/database outbox capable of surviving process crash while events are buffered;
- manual purge-all endpoint;
- Audit UI;
- weaker/general AuditRead capability separate from `SystemAdmin`.

A persistent outbox requires a later explicit reliability design; it must not be implied by the current bounded in-memory buffer.

## Python scripting and visual-runtime prerequisite chain

The PR #41 foundation is merged. The graphical screen/popup/Dynamo editor must still follow the locked chain and may not invent a private object/property model.

### 11. Public Script Engineering integration

Complete the versioned product integration around the merged foundation:

- first-class Script Engineering entities;
- explicit Client Visual vs Server Script scopes;
- stable visual object IDs/types/property keys;
- Script/visual references in canonical Engineering;
- import/export, revision persistence, `.escadapkg`, preview/apply and dependency validation;
- mapping existing Screen/Popup/Dynamo definitions into runtime-instance contracts.

### 12. Python script editor + sandbox

- concrete client Python engine technical selection;
- syntax highlighting and line numbers;
- engine-backed syntax/compile diagnostics with line/column;
- EliteSCADA API autocomplete where practical;
- event/entry-point association;
- sandboxed test/preview;
- execution budgets, cancellation and error isolation.

### 13. Visual runtime object instances/property API integration

- browser/client runtime composition;
- Engineering base values separate from runtime presentation overrides;
- TAG/Client Memory/event adapters;
- deterministic lifecycle/disposal;
- renderer implementation behind tween scheduler;
- authorization-aware backend operation adapters;
- deterministic base/binding/script/animation precedence.

### 14. Graphical screen/popup/Dynamo editor

- object palette/canvas;
- property inspector consuming the same public property schema as Python;
- TAG/expression bindings;
- images/resources;
- links/navigation/popups;
- script/event association;
- reusable Dynamo/template composition.

### 15. Advanced reusable visual libraries

- nested Dynamos/components;
- version-aware library update/migration;
- reusable scripted behavior;
- controlled instance overrides.

Full locked contract: `docs/PYTHON-SCRIPTING-AND-VISUAL-RUNTIME.md`.

The interface-validation preview in the source/protocol chain may occur before the visual-editor chain is complete. Public Script integration, script editor/sandbox and visual runtime integration are mandatory before the final graphical editor.

## Additional future Engineering/product slices

These remain locked product goals and may be scheduled according to dependencies and file-ownership safety:

- Engineering XLSX workbook import/export;
- Engineering Fragments and dependency-aware cross-project copy/paste;
- configurable application shell with header/footer/navigation/alarm summary/optional side regions;
- visual asset/resource package management;
- reusable Equipment/Template/Dynamo class-instance libraries;
- runtime-HMI multilingual content separate from Engineering UI localization;
- complete `pt-BR` / `en` / `es` coverage of Engineering/development UI;
- public SDK/module lifecycle, trust/integrity and diagnostics;
- later sandboxed Server Python scripting for shared calculations/automation using Server Memory/shared TAGs.

## Parallel scheduling rule

The coordinator may run independent workstreams in parallel only when doing so does not violate the locked dependency order or create avoidable ownership conflicts in central Engineering contracts, central DI/composition or shared frontend shell files.

Keeping every DEV busy is not a product requirement. Dependency-safe idle time is preferable to parallel conflicts that later require semantic reconstruction.

## Development quality rules

- Prefer small coherent slices with automated validation.
- Never merge a known-failing change into `main`.
- Use GitHub CI as the external .NET validation environment when local .NET execution is unavailable.
- Validate backend build/tests/runtime smoke, Web build and Chromium E2E for affected surfaces.
- Fix root causes rather than weakening tests/security/concurrency.
- Preserve supported Engineering schema compatibility or explicit migrations/tests.
- Keep runtime safety ahead of convenience.
- New drivers, scripts and UI editors must not bypass Engineering, TAG quality, security, audit or lifecycle boundaries.
- Parallel worker branches never self-merge and coordinator-owned shared files remain centrally controlled unless an assignment grants a narrow explicit exception.