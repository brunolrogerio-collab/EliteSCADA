# EliteSCADA Roadmap

Engineering Import/Export remains a mandatory cross-cutting capability throughout this roadmap. Every new Engineering domain must join the public versioned model, validation/preview/apply workflow, revision lifecycle and backup/restore path.

**Status date:** 2026-08-26
**Functional development:** ACTIVE — DEPENDENCY-SAFE PARALLEL WORK

## Established `main` foundation

The following major capabilities are integrated:

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
18. Engineering UI at `/engineering`, Runtime↔Engineering navigation and `pt-BR` / `en` / `es` localization.
19. Structured TAG, Data Source and Alarm editors.
20. Local identity/browser-login foundation with PostgreSQL users, PBKDF2-SHA256 credentials, JWT, HttpOnly browser cookie and first-user bootstrap.
21. Protected local-user administration, Engineering role-key assignment, last-admin protection, JWT security-version invalidation and active WebSocket revocation.
22. Protocol-neutral Internal Memory / Source Provider foundation with `builtin.memory.server` and `builtin.memory.client`, typed defaults, stable-ID retention semantics and per-client isolation.
23. Python Scripting + Visual Property foundation with typed public visual properties, runtime overrides, script scopes, sandbox boundaries, tween contracts, runtime instances, event queues and diagnostics.
24. Secured backend-authoritative Engineering Apply/Delete/Bulk workflows with workspace CAS/version protection, dependency-aware delete, authorization, Audit and confirmation/preview gates.
25. Historian retention/downsampling foundation with typed policies, 1m/5m/15m/1h aggregates, quality-aware aggregation, Timescale continuous aggregates and explicit destructive-retention approval.
26. Audit durability/query/retention foundation with stable keyset pagination, bounded query policy, sanitization, controlled retention and bounded asynchronous outage buffering.
27. Audit runtime integration with configured query/retention/buffer policies, protected diagnostics, cursor header and periodic retention service.
28. Audit UI and diagnostics client at `/audit`, with opaque keyset cursor handling, supported filters, safe error/auth states and central Runtime/Engineering/Audit navigation.
29. Isolated Public Script Engineering domain with stable identities, ClientVisual/Server scopes, Python metadata/source, events, dependencies, deterministic validation and adapters to the public visual scripting runtime contracts.

Important merged PR checkpoints:

- PR #35 Commands: `2fd568976fc6277d0b069adeeb560f6ea3d8205f`.
- PR #36 Runtime read/realtime protection: `10b0320149c1ef2109e9517539717a8800b200c2`.
- PR #37 Engineering UI foundation: `4553aa7ab5ba7e05a209a7c8462286d1a34a1ad6`.
- PR #38 Local identity/browser login: `2a581d279a428cb605429d5939c333ff7ad8d1b4`.
- PR #39 Local user administration: `6de8f06a443ad829ccc95c6dfcd9511e906adeff`.
- PR #40 Internal Memory foundation: `bb38617c9c27cb5c379973a6f65d66006f24eadc`.
- PR #41 Python/Visual foundation: `fc0731309d5b92d302f019d06d3511d3a247b607`.
- PR #42 Secured Engineering mutations: `6d49b99181fce6dabce838822ce972332e2f77f0`.
- PR #43 Historian retention/downsampling: `0c5f2aefdd5a7286c0c9367569067e2d12091c81`.
- PR #44 Audit durability/query/retention: `9406fb2d66c682bd6bde08a0facde0622aa86ff2`.
- PR #45 Audit runtime integration: `889c989fdce26d8593e86e430e76417412846400`.
- PR #46 Audit UI: `5629f55699d68d70d11d7058c26033d54306b570`.
- PR #47 isolated Script Engineering foundation: `8a8a52e9e1725fca5b9e06ff9f560f583dab5bbb`.

## Engineering Import/Export baseline

Canonical Engineering JSON on official `main` remains **Schema v7** until the active Internal Memory product-integration work is merged. Schema v8 currently exists only in PR #48 and must not be described as official product state yet.

Current canonical public domains include TAGs, Alarms, Data Sources/driver configuration, Equipment Templates, Equipment instances, Dynamos, Screens, Popups, Security Roles/policies and Operational Commands.

Current exchange includes canonical JSON, CSV for appropriate TAG/Alarm/Data Source workflows, historical-schema compatibility/migration testing and `.escadapkg` Engineering packages with integrity validation.

Internal Memory public configuration is being integrated through PR #48. The isolated Script Engineering model from PR #47 is merged, but its first-class canonical package/schema integration remains pending. Gateway, visual assets/property schema integration, trends, shell regions and plugin-owned configuration must use the same public/versioned Engineering infrastructure.

## Current source/protocol chain

The order below is mandatory before another external protocol family.

### 1. Internal Memory TAG sources — complete product integration

**ACTIVE LOCKED SOURCE/PROTOCOL BLOCK — PR #48 OPEN / NOT MERGED.**

PR #40 provided the source/provider foundation. PR #48 currently implements in its worker branch:

- public/versioned `builtin.memory.client` and `builtin.memory.server` Engineering semantics;
- typed initial/default values;
- proposed Engineering Schema v8 with v7 compatibility;
- JSON/CSV round-trip and preview validation;
- rejection of fake network configuration for memory sources;
- Client Memory prohibition from global Historian/Alarm semantics;
- durable PostgreSQL Server Memory retention keyed by stable TAG ID;
- restart and path-rename retention;
- fail-closed incompatible retained type behavior and explicit reset coverage.

Still required before this roadmap block becomes complete on `main`:

- final worker handoff and green reconciled CI;
- coordinator review/merge of PR #48;
- coordinator-owned runtime/DI/API composition from Engineering Data Sources into the correct memory Source Providers;
- shared TAG cache/Event Bus/realtime integration for authoritative Server Memory as required by the final composition;
- explicit Client Memory session ownership in the actual Runtime Client integration;
- authorization/Audit boundary for external Server Memory writes;
- practical Engineering UI hooks where appropriate;
- final validation that complete product semantics match `docs/INTERNAL-MEMORY-TAGS.md`.

The coordinator-owned CI was updated to validate persisted schema consistency dynamically instead of hard-coding schema v7.

### 2. Protocol-independent TAG Gateway

**SPECIFIED / NOT IMPLEMENTED — BLOCKED BY INTERNAL MEMORY.**

Only after Internal Memory product integration is complete on `main`:

- TAG→TAG routes;
- OnChange/Periodic modes;
- deadband/minimum interval/coalescing;
- `Good` quality default;
- gain/offset transform;
- cycle and multiple-writer rejection;
- independent route diagnostics;
- transactional activation;
- public/versioned Engineering and Import/Export.

See `docs/TAG-GATEWAY.md`.

### 3. Common multi-driver/Data-Source diagnostics

**SPECIFIED / NOT IMPLEMENTED — BLOCKED BY GATEWAY FOUNDATION.**

Required direction:

- per-Data-Source health/state;
- success/failure/timeout/reconnect counters;
- latency/failure-rate/data-age metrics where meaningful;
- TAG quality aggregation;
- independent failure isolation;
- protected diagnostic APIs and Engineering UI;
- no fabricated network metrics for internal memory sources.

See `docs/COMMUNICATION-DRIVER-DIAGNOSTICS.md`.

### 4. USER INTERFACE VALIDATION PREVIEW

Mandatory product-owner test build before the next external-protocol wave:

- Windows x64 primary target;
- practical startup path;
- local login/bootstrap;
- demo project;
- visible version identification;
- short validation checklist;
- package/startup smoke separate from repository-only development execution;
- product-owner feedback before heavy next-protocol investment.

See `docs/INTERFACE-VALIDATION-MILESTONE.md`.

Locked sequence:

`Internal Memory -> TAG Gateway -> common multi-driver diagnostics -> USER INTERFACE VALIDATION PREVIEW -> additional external protocols`

## External protocol/module wave after preview feedback

5. MQTT through the common Data Source/Source Provider/TAG/Gateway architecture.
6. OPC UA through the same common model.
7. BACnet through the same common model.
8. Installable/versioned Driver Module framework and public Driver SDK compatibility boundary.
9. Siemens S7 ISO Connection as the first intended installable module target after the framework is ready.
10. Later Allen-Bradley research based on public documentation/libraries, licensing and testability.

Projects referencing missing/disabled/incompatible modules must preserve Engineering configuration and surface diagnostics instead of silently deleting configuration.

## Historian and trend evolution

PR #43 storage foundations are merged. Still required:

- public/versioned Engineering representation for retention/downsampling storage policy;
- canonical validation/import-export/schema migration;
- central Historian configuration/DI application from approved Engineering/runtime configuration;
- explicit legacy raw-type migration policy;
- raw vs aggregate query/resolution selection;
- engineered, ad-hoc and saved multi-Pen trends;
- historical + live trend integration;
- expressions where appropriate.

TAG definition deletion remains separate from historian data purge.

## Audit evolution

PRs #44, #45 and #46 plus coordinator routing now provide:

- append-only PostgreSQL Audit storage;
- bounded keyset query and filters;
- protected diagnostics;
- configurable retention;
- bounded asynchronous in-memory outage buffering;
- storage-boundary sensitive-metadata sanitization;
- Audit UI at `/audit` with opaque cursor pagination and diagnostic presentation;
- central navigation to Runtime, Engineering and Audit.

Still not implemented/claimed:

- persistent disk/database outbox surviving process crash while events are buffered;
- manual purge-all endpoint;
- weaker/general AuditRead capability separate from `SystemAdmin`.

Cross-origin browser access may require exposing `X-EliteSCADA-Audit-Next-Cursor` through the deployment CORS policy; same-origin/Vite-proxy use is unaffected.

## Python scripting and visual-runtime prerequisite chain

PR #41 provides the scripting/visual runtime contract foundation. PR #47 now provides a merged isolated Script Engineering domain. The graphical editor must still consume these public contracts rather than invent a private object/property model.

### 11. Public Script Engineering integration

**PARTIALLY MERGED — ISOLATED DOMAIN COMPLETE / CANONICAL PACKAGE INTEGRATION PENDING.**

Merged through PR #47:

- stable typed Script Engineering definitions outside the central shared contract;
- ClientVisual/Server scope separation;
- Python source/language/version/enabled state;
- typed event/entry-point and dependency model;
- deterministic identity, scope, dependency-cycle and visual-reference validation;
- adapters to PR #41 public runtime contracts.

Coordinator-owned work still required after Internal Memory central Engineering reconciliation:

- first-class Scripts collection/entity kind in canonical Engineering;
- stable visual Script references in Screen/Popup/Dynamo/visual definitions;
- schema migration and canonical JSON round-trip;
- import/export preview/apply integration;
- Engineering revision and PostgreSQL persistence;
- `.escadapkg` backup/restore;
- authoritative TAG/Client Memory/Server Memory/resource reference catalog wiring.

Until those items are complete, stage 12 must not begin as production implementation.

### 12. Python script editor + sandbox

**SPECIFIED / NOT IMPLEMENTED.**

- client Python engine technical selection;
- syntax highlighting and line numbers;
- engine-backed diagnostics with line/column;
- EliteSCADA API autocomplete where practical;
- event/entry-point association;
- sandboxed test/preview;
- execution budgets, cancellation and fault isolation.

### 13. Visual runtime object instances/property API integration

**SPECIFIED / NOT IMPLEMENTED AS PRODUCT INTEGRATION.**

- browser/client runtime composition;
- Engineering base values separated from runtime presentation overrides;
- TAG/Client Memory/event adapters;
- deterministic lifecycle/disposal;
- renderer implementation behind tween scheduler;
- authorization-aware backend operation adapters;
- deterministic base/binding/script/animation precedence.

### 14. Graphical screen/popup/Dynamo editor

**SPECIFIED / NOT IMPLEMENTED.**

- object palette/canvas;
- property inspector consuming the same public property schema as Python;
- TAG/expression bindings;
- images/resources;
- navigation/popups;
- script/event association;
- reusable Dynamo/template composition.

### 15. Advanced reusable visual libraries

**SPECIFIED / NOT IMPLEMENTED.**

- nested Dynamos/components;
- version-aware library update/migration;
- reusable scripted behavior;
- controlled instance overrides.

Full locked contract: `docs/PYTHON-SCRIPTING-AND-VISUAL-RUNTIME.md`.

The source/protocol interface-validation preview may occur before this visual-editor chain is complete. Once graphical screen/Dynamo development starts, the Script/property prerequisites are mandatory.

## Additional future Engineering/product slices

These remain locked product goals and may be scheduled according to dependencies and file-ownership safety:

- Engineering XLSX workbook import/export;
- Engineering Fragments and dependency-aware cross-project copy/paste;
- configurable application shell with header/footer/navigation/alarm summary/optional side regions;
- visual asset/resource package management;
- reusable Equipment/Template/Dynamo class-instance libraries;
- runtime-HMI multilingual content separate from Engineering UI localization;
- complete `pt-BR` / `en` / `es` Engineering/development UI coverage;
- public SDK/module lifecycle, trust/integrity and diagnostics;
- later sandboxed Server Python scripting for shared calculations/automation using Server Memory/shared TAGs.

## Parallel scheduling rule

The coordinator may run independent workstreams in parallel only when doing so does not violate locked dependencies or create avoidable ownership conflicts in central Engineering contracts, DI/composition or frontend shell files.

DEV 1 and DEV 3 are intentionally waiting after their current-wave merges while DEV 2 finishes the central Engineering-sensitive Internal Memory work. Safe idle time is preferable to creating artificial parallel work that later requires semantic reconstruction.

## Development quality rules

- Prefer small coherent slices with automated validation.
- Never merge a known-failing change into `main`.
- Use GitHub CI as the external .NET validation environment when local .NET execution is unavailable.
- Validate backend build/tests/runtime smoke, Web build and Chromium E2E for affected surfaces.
- Fix root causes rather than weakening tests/security/concurrency.
- Preserve supported Engineering schema compatibility or explicit migrations/tests.
- Keep runtime safety ahead of convenience.
- New drivers, scripts and UI editors must not bypass Engineering, TAG quality, security, Audit or lifecycle boundaries.
- Parallel worker branches never self-merge and coordinator-owned shared files remain centrally controlled unless an assignment grants a narrow explicit exception.
