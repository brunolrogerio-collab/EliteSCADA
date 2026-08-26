# Roadmap baseline

The approved development north is preserved, with Engineering Import/Export acting as a mandatory cross-cutting capability rather than a later utility.

## Runtime foundation established

Implemented and validated on `main` before the current engineering-lifecycle security branch:

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

## Engineering Import/Export status

Every engineering entity introduced must define a stable serialization contract and participate in the common validation/preview/apply pipeline.

Canonical JSON schema v6 adds application security roles to the schema-v5 foundation.

Implemented engineering entities:

- Tags, including explicit read/write/configure access-policy role lists.
- Alarms.
- Data Sources / driver configuration with protected secret references.
- Equipment Templates.
- Equipment instances.
- Dynamos.
- Screens.
- Popups.
- Security Roles with explicit capability grants and optional scopes by area, equipment, screen, TAG and command.
- Cross-reference and entity validation.
- Backward-compatible parsing of historical package versions.
- Explicit migration tests from historical schemas.
- Export -> import preview round-trip testing in CI.

Security-role engineering carries authorization policy only. Passwords, password hashes, authentication tokens, private keys and other secret authentication material are excluded and rejected when represented as suspicious security metadata.

Bulk CSV engineering is implemented for Tags, Alarms and Data Sources. TAG CSV preserves historian maximum-period, metadata and access policy; alarm CSV preserves metadata.

A versioned `.escadapkg` project-package format is implemented for engineering backup/restore. Package v1 contains a manifest plus canonical `engineering.json`, with byte-length and SHA-256 integrity validation. Because the package carries the canonical engineering JSON, schema-v6 security roles participate in backup/restore without adding credential material. The package remains an engineering-project backup, not a historian/runtime database image.

The former Engineering Exchange monolith has been split into smaller entity handlers so new engineering domains do not have to accumulate inside one service.

Future plugin-owned driver/Data Source configuration must expose versioned public configuration schemas and participate in the same Engineering validation/import/export/backup/migration flow. Missing or incompatible driver modules must be diagnosable without discarding project configuration.

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

## Current execution slice

The strong runtime foundation is now being turned into a secure and engineer-friendly product without weakening the public engineering model.

Completed in the security track:

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

Next:

17. Introduce a first-class operational command domain, then enforce/audit `CommandExecute`; extend protection to sensitive read/realtime/WebSocket surfaces.
18. Add a real login/token-issuance or external identity-provider workflow and user lifecycle administration.
19. Add audit retention/query policy and durable buffering/outbox behavior for temporary storage outages.
20. Add historian retention/downsampling policies on TimescaleDB.
21. Add MQTT driver integration through the same Data Source/driver model.
22. Add OPC UA integration through the same Data Source/driver model.
23. Add BACnet communication-driver integration through the same Data Source/driver model.
24. Introduce the installable/versioned Driver Module framework and public Driver SDK compatibility boundary, including module lifecycle, diagnostics, trust/integrity and Engineering configuration preservation; use Siemens S7 ISO Connection as the first intended installable-module target after the framework is ready.
25. Add Portuguese (`pt-BR`), English (`en`) and Spanish (`es`) localization across the Engineering/development interface with a developer-selectable language preference and language-neutral Engineering contracts.
26. Add Engineering XLSX workbook import/export.
27. Expand runtime diagnostics, driver health, offline behavior and operational hardening.
28. Stabilize frontend package versions/lockfile and continue CI performance/hygiene improvements.

## Locked future product requirements

These requirements remain part of the EliteSCADA product north and must be implemented through the public engineering model.

### Industrial protocols and installable driver modules

- Modbus TCP remains the currently implemented real industrial protocol baseline.
- MQTT is a locked future protocol/integration target.
- OPC UA is a locked future industrial interoperability target.
- BACnet is a locked future communication-driver target, particularly relevant to building automation/BMS and BACnet-capable controllers/devices.
- EliteSCADA must support additional first-party and third-party communication drivers through installable modules rather than requiring every protocol to be compiled into the core product.
- The first intended installable module target is Siemens S7 communication compatible with S7 ISO Connection; its future implementation research must include relevant public/open-source work such as Node-RED S7 projects where licensing and industrial suitability permit reuse.
- Allen-Bradley PLC communication is a later explicit research/module target; protocol/library/family scope remains intentionally undecided until public documentation, open-source options, licensing, simulator/hardware access and practical interoperability can be evaluated.
- The combined Modbus TCP, MQTT, OPC UA, BACnet, Siemens S7 and future Allen-Bradley direction is intended to provide broad practical compatibility across mainstream industrial/building-automation controllers. The user's planning hypothesis that this set can cover more than 90% of practical PLC/controller needs must be validated before being used externally as a measured market statistic.
- Driver modules use the common Driver SDK/DriverHost boundary and must not bypass TAG/quality, historian, alarms, security, audit or Engineering semantics.
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
- The selected language applies consistently to Data Sources/drivers, TAGs, database/historian configuration, alarms, templates/equipment/Dynamos, screens/popups, trends, project lifecycle, security/user administration, module administration, diagnostics, menus, property editors, validation messages and product-owned engineering help text.
- Language selection is a presentation/user preference and must not change stable Engineering IDs, TAG paths, communication addresses, enum/storage values, schema keys, revision identity or runtime semantics.
- Product-owned UI text should use localization/resource keys instead of persisting translated labels as authoritative engineering values.
- The language preference should be persistable per user/profile once that subsystem exists.
- Localization of the Engineering UI is distinct from multilingual runtime HMI/application content; runtime screen-language engineering is a separate capability.
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

The runtime, engineering contract and persistence foundation are strong enough that editor work can begin incrementally after the core backend authentication/enforcement and audit path are established, rather than waiting for every future driver to exist.

The editor must consume the same public engineering model rather than own a private representation of project configuration. Reusable libraries, Engineering Fragments/cross-project copy-paste, trends, access-aware visibility, localization and configurable shell regions are core workflows, not late add-ons.

The Engineering/development UI must share one Portuguese/English/Spanish localization infrastructure across editor and administrative/engineering surfaces rather than implementing translations independently per screen.

The initial reusable dynamo/object catalog, common state model, faceplates, trend, shell widgets and reference-research procedure are defined in `docs/VISUAL-COMPONENT-LIBRARY.md`.
