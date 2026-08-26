# Roadmap baseline

The approved development north is preserved, with Engineering Import/Export acting as a mandatory cross-cutting capability rather than a later utility.

This roadmap distinguishes what is already on `main`, what is implemented in an open PR, and what is only specified. The current development pause permits documentation/continuity maintenance only; functional implementation resumes only by explicit user instruction.

## Runtime foundation established

Implemented and validated on `main`:

1. Repository, architecture and CI/CD foundation.
2. Tag Engine, quality, current-value cache and internal Event Bus.
3. Simulation Driver and Driver SDK contract.
4. REST + WebSocket runtime.
5. First React runtime screen and equipment modal.
6. Automated .NET build/test/runtime smoke validation.
7. Automated Chromium end-to-end validation.
8. PostgreSQL engineering revision persistence.
9. Working, Published and Active revision lifecycle.
10. Transactional activation with candidate runtime isolation and rollback.
11. Fail-closed recovery of the persisted Active Revision after restart.
12. TimescaleDB historian implementation used by the runtime.
13. Real Modbus TCP transport/driver with FC01/02/03/04/05/06/16, grouped polling, writes, reconnect and communication quality.
14. Engineering Data Source compiler that converts `modbus.tcp` configuration and TAG bindings into executable runtime plans.
15. Isolated Engineering Workspace, independent from the simulation/active process runtime.
16. Transactional checkout of persisted revisions into the Engineering Workspace.
17. Workspace dirty tracking, change-version-safe saves and immutable revision lineage through `BasedOnRevision`.
18. Capability-based authorization contracts and audit event/sink foundation.
19. Engineering Schema v6 authorization roles, explicit capability grants and scoped policies.
20. Trusted JWT Bearer principal adapter with issuer/audience/signature/lifetime validation.
21. Phase-one backend enforcement for process-value TAG writes, alarm acknowledgement, Engineering import apply and project-package restore apply.
22. Active-runtime authorization policy resolution from the exact persisted Active Revision with fail-closed mismatch behavior.
23. Browser coverage distinguishing valid developer, underprivileged operator, missing credentials and invalid credentials.
24. First-class operational command domain merged through PR #35, including Engineering Schema v7 command definitions/registries, runtime compilation/execution through the target TAG's owning driver, scoped `CommandExecute`, audit and command tests. ✓

## Engineering Import/Export status

Every engineering entity introduced must define a stable serialization contract and participate in the common validation/preview/apply pipeline.

Canonical JSON is now at **Engineering Schema v7** on `main`; v7 extends the schema-v6 security foundation with first-class operational Commands.

Implemented engineering entities on `main`:

- Tags, including explicit read/write/configure access-policy role lists.
- Alarms.
- Data Sources / driver configuration with protected secret references.
- Equipment Templates.
- Equipment instances.
- Dynamos.
- Screens.
- Popups.
- Security Roles with explicit capability grants and optional scopes by area, equipment, screen, TAG and command.
- Operational Commands with target TAG references and runtime/security semantics.
- Cross-reference and entity validation.
- Backward-compatible parsing of historical package versions.
- Explicit migration tests from historical schemas.
- Export -> import preview round-trip testing in CI.

Security-role engineering carries authorization policy only. Passwords, password hashes, authentication tokens, private keys and other secret authentication material are excluded and rejected when represented as suspicious security metadata.

Bulk CSV engineering is implemented for Tags, Alarms and Data Sources. TAG CSV preserves historian maximum-period, metadata and access policy; alarm CSV preserves metadata.

A versioned `.escadapkg` project-package format is implemented for engineering backup/restore. Package v1 contains a manifest plus canonical `engineering.json`, with byte-length and SHA-256 integrity validation. Because the package carries canonical engineering JSON, current schema domains participate in backup/restore without adding credential material. The package remains an engineering-project backup, not a historian/runtime database image.

The former Engineering Exchange monolith has been split into smaller entity handlers so new engineering domains do not have to accumulate inside one service.

Future internal-source, Gateway and plugin-owned driver/Data Source configuration must expose versioned public configuration schemas and participate in the same Engineering validation/import/export/backup/migration flow. Missing or incompatible driver modules must be diagnosable without discarding project configuration.

## Foundation gate completed

The original gate before promoting persistence and real industrial communication is complete:

1. Explicit TAG permission/access-policy serialization. ✓
2. CSV round-trip fidelity for current bulk-engineering entities. ✓
3. Versioned project-package manifest for engineering backup/restore. ✓
4. Historical engineering schema migration/compatibility tests. ✓
5. Engineering Exchange handler refactor. ✓
6. PostgreSQL engineering persistence. ✓
7. TimescaleDB historian baseline. ✓
8. Real Modbus TCP Data Source/driver baseline. ✓

## Current execution status

The strong runtime foundation is being turned into a secure and engineer-friendly product without weakening the public Engineering model.

Completed on `main` in the security/command track:

1. Capability-based authorization evaluator with configurable role names and explicit scoped grants. ✓
2. TAG access-policy evaluator preserving `null` versus empty-list semantics. ✓
3. Audit event and sink contracts. ✓
4. Versioned Engineering Schema v6 security-role/grant/scope serialization. ✓
5. Trusted JWT Bearer principal adapter. ✓
6. Phase-one backend capability enforcement for critical TAG/alarm/Engineering restore mutations. ✓
7. Active-runtime policy resolution from the exact persisted Active Revision. ✓
8. Browser authentication/authorization coverage for developer/operator/anonymous/invalid-token cases. ✓
9. PostgreSQL append-only audit event storage with database-enforced rejection of `UPDATE`, `DELETE` and `TRUNCATE`. ✓
10. Queryable audit trail protected by `SystemAdmin`. ✓
11. Succeeded/denied/failed audit recording for protected TAG writes, alarm ACK, Engineering import apply and project-package restore apply. ✓
12. Browser coverage validating trusted/anonymous audit subjects, authorization outcomes and audit-read protection. ✓
13. Persistence save/publish/activate/checkout/apply protection with `EngineeringModify`, authenticated lifecycle actors and succeeded/denied/failed audit records. ✓
14. PostgreSQL-backed Chromium lifecycle coverage proving anonymous/operator denial and preventing caller-supplied save/publish actor spoofing. ✓
15. Alarm shelving/unshelving runtime behavior with `AlarmShelve` authorization, area scoping, trusted actor metadata and succeeded/denied/failed audit coverage. ✓
16. Browser coverage for developer/operator/anonymous alarm shelving and audit outcomes. ✓
17. First-class operational command domain, Engineering Schema v7, scoped `CommandExecute`, runtime command routing and command audit through PR #35. ✓

## Implemented in open PRs, not yet on `main`

### PR #36 — read/realtime security

`Protect runtime read and realtime surfaces` is open and retargeted to `main` after PR #35 merged.

It implements protection for sensitive TAG/historian/alarm reads, authenticated realtime/WebSocket behavior, protected Engineering/runtime diagnostic reads and a minimal public `/health` boundary. It is **not considered integrated** until its current head receives independent successful CI and the PR is merged.

### PR #37 — Engineering UI foundation

`Add Engineering UI foundation and localization` remains Draft. Its current tested head has a successful CI run #143.

Implemented in that PR:

- `/engineering` developer workspace while preserving `/` as Runtime HMI;
- Runtime <-> Engineering navigation;
- shared `pt-BR`, `en` and `es` localization foundation;
- structured editors for TAGs, Data Sources and Alarms;
- existing-entity and new-entity local drafts;
- canonical backend preview through the Engineering import preview pipeline;
- preservation of metadata/non-exposed fields;
- alarm `tagPath` change clears stale `tagId` before preview validation;
- stale-preview invalidation;
- changed-draft navigation protection and `beforeunload` protection;
- Chromium coverage for navigation, localization, preview validity and proof that preview does not mutate Workspace/export state.

The PR intentionally has **no Apply, Delete or bulk-edit workflow**. Drafts and creates are preview-only and do not modify the Engineering Workspace.

## Ordered next implementation slices when development resumes

The architectural order below is locked. Do not jump to another external protocol merely because protocol code looks more entertaining than foundations.

1. Independently validate and integrate PR #36 read/realtime security into `main`.
2. Reconcile PR #37 against current `main`/security boundaries and integrate the validated Engineering UI foundation without weakening its preview-only safety boundary.
3. Add real login/token issuance or an external identity-provider workflow plus user lifecycle/administration.
4. Add audit retention/query policy and durable buffering/outbox behavior for temporary storage outages.
5. Add historian retention/downsampling policies on TimescaleDB.
6. Implement built-in internal memory TAG sources: `builtin.memory.client` and `builtin.memory.server`, typed initial values, Server Memory retention by stable TAG ID, migration/reset behavior, scripting scope and Engineering Import/Export. See `docs/INTERNAL-MEMORY-TAGS.md`.
7. Implement the protocol-independent Gateway / TAG Bridge as a first-class Engineering/runtime domain: TAG-to-TAG routes, OnChange/Periodic transfer, deadband/minimum interval/coalescing, `Good` quality default, linear gain/offset transform, loop and multi-writer rejection, diagnostics and transactional activation. See `docs/TAG-GATEWAY.md`.
8. Expand common multi-driver communication diagnostics: per-Data-Source health/state, successes/failures/timeouts/reconnects, rates/latency/data age, TAG quality aggregation, protected APIs, independent failure isolation and Engineering diagnostics UI. See `docs/COMMUNICATION-DRIVER-DIAGNOSTICS.md`.
9. Only after internal memory -> Gateway -> common diagnostics, add MQTT through the same Data Source/Source Provider/TAG Gateway model.
10. Add OPC UA through the same common model.
11. Add BACnet through the same common model.
12. Introduce the installable/versioned Driver Module framework and public Driver SDK compatibility boundary, including lifecycle, diagnostics, trust/integrity and Engineering configuration preservation; Siemens S7 ISO Connection remains the first intended installable-module target after the framework is ready.
13. Expand the `pt-BR` / `en` / `es` localization foundation from PR #37 across the complete Engineering/development interface.
14. Add Engineering XLSX workbook import/export.
15. Continue graphical screen/popup editor, reusable Dynamos/components, bindings/faceplates, configurable shell and Engineering Fragments/cross-project copy-paste through the public Engineering model.
16. Implement engineered/ad-hoc/saved multi-Pen trends with historical/realtime semantics and expressions where appropriate.
17. Stabilize frontend package versions/lockfile and continue CI performance/hygiene improvements.

## Locked future product requirements

These requirements remain part of the EliteSCADA product north and must be implemented through the public Engineering model.

### Internal memory TAG sources

- Internal memory is a built-in TAG source family and must be implemented before MQTT, OPC UA, BACnet, Siemens S7 or other new external protocol work.
- `builtin.memory.client` is scoped to one opened Runtime Client instance/session. Different clients may hold different values for the same engineered TAG definition.
- Client Memory is non-retentive server-side in the initial implementation and is intended for popup/screen transition variables, navigation/context state, temporary filters, local demo controls and future client-side scripts.
- Client Memory is not trusted backend state and must not be used as an authorization source, interlock, server command permissive, global process-sequencing variable or audit identity.
- Client Memory must not drive global server historian/alarm semantics because there is no single global value.
- `builtin.memory.server` is one server-owned shared value per TAG, visible consistently to all authorized Runtime Clients.
- Server Memory is retentive by design and is suitable for shared simulation variables, internal sequence state, intermediate values, retained parameters and future server-side scripts.
- Server Memory participates in the normal shared TAG runtime path: cache/events/realtime/security and historian/alarm behavior when configured.
- Retained Server Memory values are runtime state stored separately from immutable Engineering revisions/packages and keyed primarily by stable TAG ID so a path rename does not lose the value.
- Incompatible retained-value/data-type changes must never be silently coerced; explicit validation/reset/migration behavior is required.
- Memory TAG Engineering requires a typed initial/default value in the public versioned contract. New Client sessions start from it; Server Memory uses it when no compatible retained value exists.
- Internal memory sources require no protocol address and must not fabricate network diagnostics such as reconnect, timeout or latency counters.
- Client/server script scope must remain explicit: client scripts may access their Client Memory; server scripts may access Server Memory; server logic must never treat one client's local value as global truth.
- Full semantics and required validation scenarios are defined in `docs/INTERNAL-MEMORY-TAGS.md`.

### Protocol-independent TAG Gateway

- EliteSCADA must provide a server-side Gateway/TAG Bridge before additional external protocol families are introduced.
- The authoritative mapping is `Source TAG -> Destination TAG`; Data Sources/protocols are resolved from each TAG's owning source/provider.
- Concrete communication drivers must never call one another directly for gateway transfer.
- Gateway routes are first-class versioned Engineering entities and participate in import/export, preview/apply, revisioning, backup/restore and activation.
- `builtin.memory.server` may be a source or destination; `builtin.memory.client` is rejected as a server Gateway endpoint.
- Destination TAGs must be active, writable and type-compatible.
- Initial routing is unidirectional; direct/indirect cycles are rejected.
- Initial model rejects multiple active Gateway writers to the same destination unless a future explicit arbitration model is defined.
- Fan-out from one source to several destinations is supported through independent routes.
- Initial transfer modes include OnChange and Periodic with bounded rate/deadband/coalescing behavior.
- Safe default quality policy transfers only `Good` source values and does not push stale values when source communication becomes bad.
- Unsafe implicit type coercion is forbidden; simple explicit deterministic conversions and linear `destination = source × gain + offset` transformation may be supported without scripts.
- Gateway execution uses a trusted internal runtime/service authority and common TAG/write/provider boundaries; it must not borrow browser identity or expose a generic authorization bypass.
- Engineering changes/enablement are auditable; cyclic runtime transfers use route diagnostics rather than flooding the human audit trail.
- Route diagnostics are distinct from driver network diagnostics and include transfer state/counters, quality skips, throttling/coalescing and destination write failures.
- Full semantics and validation scenarios are defined in `docs/TAG-GATEWAY.md`.

### Industrial protocols and installable driver modules

- Modbus TCP remains the currently implemented real industrial protocol baseline.
- Multiple Data Sources/communication instances must be active simultaneously; several instances may use the same Driver type for different PLCs/devices, and different Driver types may run in parallel.
- TAG communication ownership is through one Data Source per revision plus the protocol-specific address/binding.
- Failure of one Data Source must remain isolated and must not contaminate another Data Source's health, counters or TAG quality.
- Driver/Data Source diagnostics are a first-class protected Engineering/runtime capability and must use a common diagnostic contract rather than protocol-private log parsing. See `docs/COMMUNICATION-DRIVER-DIAGNOSTICS.md`.
- MQTT is a locked future protocol/integration target after the internal-memory, TAG-Gateway and common-diagnostics foundations.
- OPC UA is a locked future industrial interoperability target after the internal-memory, TAG-Gateway and common-diagnostics foundations.
- BACnet is a locked future communication-driver target, particularly relevant to building automation/BMS and BACnet-capable controllers/devices, after the internal-memory, TAG-Gateway and common-diagnostics foundations.
- EliteSCADA must support additional first-party and third-party communication drivers through installable modules rather than requiring every protocol to be compiled into the core product.
- The first intended installable module target is Siemens S7 communication compatible with S7 ISO Connection; its future implementation research must include relevant public/open-source work such as Node-RED S7 projects where licensing and industrial suitability permit reuse.
- Allen-Bradley PLC communication is a later explicit research/module target; protocol/library/family scope remains intentionally undecided until public documentation, open-source options, licensing, simulator/hardware access and practical interoperability can be evaluated.
- The combined Modbus TCP, MQTT, OPC UA, BACnet, Siemens S7 and future Allen-Bradley direction is intended to provide broad practical compatibility across mainstream industrial/building-automation controllers. The planning hypothesis that this set can cover more than 90% of practical PLC/controller needs must be validated before being used externally as a measured market statistic.
- Driver modules use the common Driver SDK/DriverHost boundary and must not bypass TAG/quality, historian, alarms, security, audit, Gateway routing or Engineering semantics.
- A driver module declares stable identity/version, EliteSCADA compatibility, provided driver/Data Source types and a public versioned Engineering configuration schema.
- The module lifecycle must support installation, discovery/catalog registration, enable/disable, upgrade and removal with compatibility validation before runtime activation.
- Projects that reference missing, disabled or incompatible modules must preserve their Engineering configuration and expose explicit diagnostics rather than silently dropping data.
- Module package integrity/publisher trust must be evaluated before executable code is enabled; arbitrary untrusted modules must not be silently loaded into an industrial runtime.
- Module administration is security-sensitive and must be permission-controlled and auditable.
- The exact package format, signing policy, distribution/catalog UX and isolation strategy are deferred to the dedicated module-framework implementation slice.
- The accepted architectural decision is recorded in `docs/ADR-007-DRIVER-MODULES-AND-PROTOCOLS.md`.

### Reusable libraries across applications

- Evolve Equipment Templates/Equipment and Dynamos into a version-aware reusable library experience.
- Preserve a class/instance model conceptually similar in responsibility to Elipse E3 XObject/XControl while using EliteSCADA's own contracts and implementation.
- Allow reusable definitions to expose properties/bindings and be instantiated with application-specific context.
- Support nested reusable components with deterministic dependency validation.
- Make library definitions importable/exportable independently of the graphical editor.
- Support controlled library update/migration while preserving safe instance overrides.

### Cross-project copy/paste

- Copy/paste screens, popups, equipment, Dynamos and engineering structures between projects.
- Represent clipboard payloads as canonical Engineering Fragments rather than browser-private data.
- Support dependency-aware paste with preview for create/update/conflict/missing dependency and rebinding cases.
- Offer both selected-only and selected-with-required-dependencies workflows.

### Trend charts

- Provide engineering-configurable trends with multiple Pens.
- Allow each Pen to use historical TAG data, a live/runtime binding or an expression.
- Support project-defined trends placed on screens/popups.
- Permit ad-hoc and saved runtime trends where access policy allows.
- Keep historical query and realtime subscription semantics distinct even when displayed together.
- Expose suitable aggregation/downsampling for TimescaleDB without leaking storage-specific concepts into the engineering contract.

### Users, roles and hierarchy

- Roles and access hierarchy are configurable per application, not hard-coded globally.
- Distinguish at minimum: view, TAG read, operational command, setpoint/process-value write, alarm acknowledgement, alarm shelving, trend use/save, engineering modification, user/role administration and system administration.
- Allow access scope by area, equipment, screen, TAG or command where required by the application.
- Permit UI objects/screens/menus to be hidden or disabled based on policy.
- Never rely on visibility as the security boundary; backend/API authorization must enforce protected operations.
- Engineering packages may contain role/policy definitions but never authentication secrets or password hashes.
- Commands, process writes, ACK/shelving, engineering publication/activation and security administration must be auditable.

### Engineering/development interface localization

- The developer/engineering user must be able to choose the Engineering UI language among Portuguese (Brazil / `pt-BR`), English (`en`) and Spanish (`es`).
- The selected language applies consistently to Data Sources/drivers, TAGs, database/historian configuration, alarms, templates/equipment/Dynamos, screens/popups, trends, project lifecycle, security/user administration, module administration, diagnostics, Gateway/TAG Bridge engineering, menus, property editors, validation messages and product-owned engineering help text.
- Language selection is a presentation/user preference and must not change stable Engineering IDs, TAG paths, communication addresses, enum/storage values, schema keys, revision identity or runtime semantics.
- Product-owned UI text should use localization/resource keys instead of persisting translated labels as authoritative engineering values.
- The language preference should be persistable per user/profile once that subsystem exists.
- Localization of the Engineering UI is distinct from multilingual runtime HMI/application content; runtime screen-language engineering is a separate capability.
- PR #37 contains the initial shared `pt-BR` / `en` / `es` implementation foundation, but the full Engineering environment still needs to consume it as later surfaces are built.
- The accepted architectural decision is recorded in `docs/ADR-008-ENGINEERING-UI-LOCALIZATION.md`.

### Configurable persistent application shell

- Support reusable/configurable header, footer, navigation, alarm banner/summary and optional side regions that remain fixed while process screens change.
- These shell regions are engineering objects and may be globally defined with controlled application/screen overrides.
- Common widgets include application identity, logged-in user, navigation, alarm summary and date/time.

### Date/time binding and device clock integration

- Date/time widgets support date+time, date-only and time-only formats.
- Required time sources: EliteSCADA server clock or a TAG containing time from a PLC/RTU/other source.
- Timezone/formatting is presentation configuration and must not change timestamp semantics.
- Future active device-clock synchronization through a communication driver is a separate explicit command, permission-controlled and auditable. Binding a display to a PLC time TAG must never silently synchronize the PLC clock.

## Editor phase

The runtime, Engineering contract and persistence foundation are strong enough that editor work can proceed incrementally after the required backend security integration rather than waiting for every future driver to exist.

PR #37 has already started this phase with a validated Draft implementation of `/engineering`, localized navigation and preview-only structured editors for TAGs, Data Sources and Alarms. This is **implemented in PR, not merged** and does not yet provide Apply, Delete or bulk edit.

The editor must consume the same public Engineering model rather than own a private representation of project configuration. Reusable libraries, Engineering Fragments/cross-project copy-paste, trends, access-aware visibility, localization, Gateway/TAG Bridge engineering and configurable shell regions are core workflows, not late add-ons.

The Engineering/development UI must share one Portuguese/English/Spanish localization infrastructure across editor and administrative/engineering surfaces rather than implementing translations independently per screen.

The initial reusable dynamo/object catalog, common state model, faceplates, trend, shell widgets and reference-research procedure are defined in `docs/VISUAL-COMPONENT-LIBRARY.md`.
