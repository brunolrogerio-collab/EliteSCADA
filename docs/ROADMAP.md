# EliteSCADA Roadmap

Engineering Import/Export remains a mandatory cross-cutting capability throughout this roadmap. Every new Engineering domain must join the public versioned model, validation/preview/apply workflow, revision lifecycle and backup/restore path.

**Status date:** 2026-08-27  
**Functional development:** **ACTIVE — COMMON MULTI-DRIVER / DATA SOURCE DIAGNOSTICS**

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
15. Trusted JWT validation and backend protection of critical process/Engineering operations.
16. Protected runtime read, historian, alarm, Engineering/diagnostic and `/ws/tags` realtime surfaces.
17. Engineering UI at `/engineering`, Runtime/Engineering/Audit navigation and `pt-BR` / `en` / `es` localization.
18. Structured TAG, Data Source and Alarm editors.
19. Local identity/browser-login and protected local-user administration.
20. Protocol-neutral Internal Memory foundation and complete product integration for `builtin.memory.server` / `builtin.memory.client`.
21. Python Scripting + Visual Property public contract foundation.
22. Secured backend-authoritative Engineering Apply/Delete/Bulk workflows with Workspace CAS.
23. Historian retention/downsampling foundation and quality-aware aggregates.
24. Audit durability/query/retention, runtime integration and `/audit` UI.
25. Isolated Public Script Engineering domain with stable identities/scopes/events/dependencies.
26. Engineering Schema v9 with first-class TAG Gateway routes, deterministic validation and package/revision persistence.
27. Complete protocol-independent TAG Gateway runtime/product integration, including Engineering UI and route diagnostics.

Important merged checkpoints:

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
- PR #48 Internal Memory Engineering + durable retention: `ad4a7aa8d17e4e7370e5801d470a69f1a096bab4`.
- PR #49 complete Internal Memory product integration: `18e0c5f55bf33958d040ed3a2f2b948e34d3cc5f`.
- PR #50 TAG Gateway Engineering / Schema v9: `7a039c0eda8802a8ed2851fe9223fd831859fc61`.
- PR #51 OPC UA research: `aa7735fcc15e00aea5bf19a543f53b2735ef48e3`.
- PR #52 Siemens S7 research: `bd825682ae0ccfdbdb938fab638a27f6961510bf`.
- PR #55 complete TAG Gateway runtime/product integration: `41bc437ba64f60fba26754794a9dc5a4e9a034f7`.

Gateway post-merge test stabilization is on `main` through `cb4d2c423c31cf7a52ea6ebe6de494c281901f3f`; CI #336 passed Web build, backend build/tests, runtime smoke and Chromium E2E.

## Engineering Import/Export baseline

Canonical Engineering JSON on official `main` is **Schema v9**.

Current first-class domains include TAGs, Alarms, Data Sources/driver configuration, Equipment Templates, Equipment instances, Dynamos, Screens, Popups, Security Roles/policies, Operational Commands and TAG Gateway routes. Internal Memory TAGs carry typed initial/default values.

Canonical exchange includes JSON, CSV where appropriate and `.escadapkg` packages with integrity validation plus historical schema compatibility/migration testing. Preview/Apply remains the public mutation path for imported Engineering candidates.

The isolated Script Engineering model from PR #47 is merged but is not yet a first-class canonical package collection. Visual assets/property schema integration, trends, shell regions and future module-owned configuration must use the same public/versioned Engineering authority rather than private parallel truth.

## Locked source/protocol chain

`Internal Memory -> TAG Gateway -> common multi-driver diagnostics -> USER INTERFACE VALIDATION PREVIEW -> additional external protocols`

### 1. Internal Memory TAG sources

**COMPLETE / MERGED.**

PRs #40, #48 and #49 provide public typed Engineering defaults, stable-ID durable Server Memory retention, per-runtime-client Client Memory isolation, shared Server Memory runtime composition, authorized/Audited retained reset, Historian opt-in behavior and Engineering/runtime UI/API hooks without fake network diagnostics.

### 2. Protocol-independent TAG Gateway

**COMPLETE / MERGED.**

PRs #50 and #55 provide:

- canonical Schema v9 Gateway routes using stable TAG IDs/paths;
- Preview/Apply/revision/package persistence and deterministic validation;
- direct/indirect cycle rejection and multiple-writer rejection;
- fan-out;
- Server Memory support and Client Memory rejection;
- protocol-independent runtime transfer over common TAG/Event Bus/write ownership;
- OnChange/Periodic, quality policy, deadband/rate/coalescing and startup synchronization;
- Exact/CheckedNumeric conversion with optional gain/offset;
- transactional Active Revision replacement;
- route-local diagnostics;
- protected diagnostics API and Engineering Gateway configuration/diagnostic UI;
- automated Modbus<->Server Memory and independent Modbus<->Modbus proof.

See `docs/TAG-GATEWAY.md`.

### 3. Common multi-driver/Data Source diagnostics

**ACTIVE PRODUCT BLOCK.**

The architecture must preserve `Driver type != Data Source`. Multiple simultaneous Data Sources using the same or different Driver types remain first-class.

Required current implementation:

- evolve the common external communication diagnostics contract without Modbus-specific leakage;
- expose Data Source identity, Driver type/instance identity and safe endpoint/configuration context;
- operational state with healthy/degraded/reconnecting/faulted distinction;
- last success/failure/state-change timestamps and sanitized message;
- monotonic cycle/request/success/failure/consecutive-failure/timeout/connect/reconnect/read/write/update counters where meaningful;
- recent failure/latency/data-age/effective-scan information where meaningful and bounded;
- TAG-quality summary by active Data Source;
- instrument current Modbus TCP runtime using poll blocks/Unit IDs as useful protocol detail;
- prove two simultaneous independent Modbus Data Sources, isolated failure/recovery/counters/quality and correct write ownership;
- protected backend diagnostic snapshots and Engineering diagnostics UI;
- do not fabricate transport/network metrics for Internal Memory or built-in simulation.

DEV 2 owns the isolated common-contract + Modbus instrumentation slice. Coordinator owns DriverHost/API/DI/UI composition and final product integration.

See `docs/COMMUNICATION-DRIVER-DIAGNOSTICS.md`.

### 4. USER INTERFACE VALIDATION PREVIEW

**SPECIFIED / NOT IMPLEMENTED — BLOCKED BY COMMON DIAGNOSTICS.**

Before the next external-protocol wave, produce a practical Windows x64 product-owner validation build with:

- practical startup path;
- local login/bootstrap;
- demo project;
- visible version identification;
- short validation checklist;
- package/startup smoke separate from repository-only execution;
- product-owner feedback before heavy protocol investment.

See `docs/INTERFACE-VALIDATION-MILESTONE.md`.

## External protocol/module wave after preview feedback

5. MQTT through the common Data Source/Source Provider/TAG/Gateway architecture.
6. OPC UA through the same common model.
7. BACnet through the same common model.
8. Installable/versioned Driver Module framework and public Driver SDK compatibility boundary.
9. Siemens S7 ISO Connection as the first intended installable-module target after the framework is ready.
10. Later Allen-Bradley research based on public documentation/libraries, licensing and testability.

Projects referencing missing/disabled/incompatible modules must preserve Engineering configuration and surface diagnostics instead of silently deleting configuration.

OPC UA and Siemens S7 research are merged architecture inputs, not authorization to start production protocol runtimes before the preview gate.

## Historian and trend evolution

PR #43 storage foundations are merged. Still required:

- public/versioned Engineering representation for retention/downsampling policy;
- canonical validation/import-export/schema migration;
- central Historian configuration from approved Engineering/runtime configuration;
- explicit legacy raw-type migration policy;
- raw vs aggregate query/resolution selection;
- engineered, ad-hoc and saved multi-Pen trends;
- historical + live trend integration;
- expressions where appropriate.

TAG definition deletion remains separate from historian data purge.

## Audit evolution

PRs #44, #45 and #46 plus coordinator routing provide append-only PostgreSQL Audit storage, bounded keyset query/filtering, protected diagnostics, configurable retention, bounded asynchronous in-memory outage buffering, storage-boundary sanitization and `/audit` UI.

Still not implemented/claimed:

- persistent disk/database outbox surviving process crash while events are buffered;
- manual purge-all endpoint;
- weaker/general AuditRead capability separate from `SystemAdmin`.

## Python scripting and visual-runtime prerequisite chain

`canonical Script integration -> script editor/sandbox -> visual runtime object/property integration -> graphical Screen/Popup/Dynamo editor -> advanced visual libraries`

PR #41 provides public scripting/visual runtime contracts. PR #47 provides an isolated Script Engineering domain. Neither authorizes the graphical editor to create a private model.

### 11. Public Script Engineering integration

**PARTIALLY MERGED — ISOLATED DOMAIN COMPLETE / CANONICAL PACKAGE INTEGRATION PENDING.**

Still required:

- first-class Scripts collection/entity kind in canonical Engineering;
- stable visual Script references;
- schema migration and canonical JSON round-trip;
- Preview/Apply integration;
- Revision/PostgreSQL persistence and `.escadapkg` backup/restore;
- authoritative TAG/Client Memory/Server Memory/resource-reference catalogs.

No production Python editor/sandbox may precede this integration.

### 12. Python script editor + sandbox

**RESEARCH IN PR #54 / PRODUCTION NOT IMPLEMENTED.**

DEV 3 research recommends Pyodide as the first laboratory candidate and Monaco as the first desktop Engineering editor candidate, with per-runtime-instance Worker isolation, restricted JS globals/RPC and hard Worker termination fallback. Those are research recommendations, not selected production dependencies.

### 13. Visual runtime object instances/property API integration

**SPECIFIED / NOT IMPLEMENTED AS PRODUCT INTEGRATION.**

Runtime property precedence remains:

`Animation > Script > BindingOrExpression > EngineeringBase`

The browser runtime must use canonical visual/property contracts and deterministic lifecycle/disposal.

### 14. Graphical Screen/Popup/Dynamo editor

**RESEARCH IN PR #53 / PRODUCTION NOT IMPLEMENTED.**

DEV 1 research recommends canonical Engineering authority with SVG/DOM-first authoring and renderer-independent geometry/interaction services. Production work remains blocked by Script/editor/runtime prerequisites.

### 15. Advanced reusable visual libraries

**SPECIFIED / NOT IMPLEMENTED.**

Includes nested Dynamos/components, version-aware library migration, reusable scripted behavior and controlled instance overrides.

Full locked contract: `docs/PYTHON-SCRIPTING-AND-VISUAL-RUNTIME.md`.

## Additional future Engineering/product slices

These remain locked goals and may be scheduled according to dependencies and ownership safety:

- Engineering XLSX import/export;
- Engineering Fragments and dependency-aware cross-project copy/paste;
- configurable application shell;
- visual asset/resource package management;
- reusable Equipment/Template/Dynamo class-instance libraries;
- runtime-HMI multilingual content separate from Engineering UI localization;
- complete `pt-BR` / `en` / `es` Engineering/development UI coverage;
- public SDK/module lifecycle, trust/integrity and diagnostics;
- later sandboxed Server Python for shared calculations/automation using Server Memory/shared TAGs.

## Parallel scheduling rule

Parallel work is allowed only where dependency order and central ownership remain clean.

Current split:

- **COORDENADOR:** common diagnostics product integration, central DriverHost/API/DI/UI, acceptance, CI, merges and docs;
- **DEV 2:** isolated protocol-neutral diagnostics contract + Modbus TCP instrumentation/tests on `feature/communication-driver-diagnostics`;
- **DEV 1:** research PR #53 delivered, waiting;
- **DEV 3:** research PR #54 delivered, waiting.

Do not start canonical Script schema integration or new protocol runtime work merely because workers are idle if that creates avoidable central Engineering/API/DI conflicts.

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
