# Roadmap baseline

The approved development north is preserved, with Engineering Import/Export acting as a mandatory cross-cutting capability rather than a later utility.

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

## Engineering Import/Export status

Every engineering entity introduced must define a stable serialization contract and participate in the common validation/preview/apply pipeline.

Implemented in canonical JSON schema v5:

- Tags, including explicit read/write/configure access-policy role lists.
- Alarms.
- Data Sources / driver configuration with protected secret references.
- Equipment Templates.
- Equipment instances.
- Dynamos.
- Screens.
- Popups.
- Cross-reference validation.
- Backward-compatible parsing of historical package versions.
- Explicit migration tests from schema v1 through v5.
- Export -> import preview round-trip testing in CI.

Bulk CSV engineering is implemented for Tags, Alarms and Data Sources. TAG CSV preserves historian maximum-period, metadata and access policy; alarm CSV preserves metadata.

A versioned `.escadapkg` project-package format is implemented for engineering backup/restore. Package v1 contains a manifest plus canonical `engineering.json`, with byte-length and SHA-256 integrity validation. Restore reuses the normal engineering preview/apply flow. It is intentionally an engineering-project backup, not a historian/runtime database image.

The former Engineering Exchange monolith has been split into smaller entity handlers so new engineering domains do not have to accumulate inside one service.

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

The next work should turn the strong runtime foundation into a secure and engineer-friendly product without weakening the public engineering model.

In progress:

1. Capability-based authorization and audit contracts. Role names remain application-configurable; capability semantics are explicit and backend-oriented. See `docs/SECURITY-AUTHORIZATION-AUDIT.md`.

Next:

2. Serialize application role/grant/scope definitions as versioned engineering entities.
3. Add a trusted authenticated-principal provider and enforce backend authorization on protected API operations.
4. Persist append-only audit events in PostgreSQL and audit successful, denied and failed process/security mutations.
5. Add historian retention/downsampling policies on TimescaleDB.
6. Add MQTT driver integration through the same Data Source/driver model.
7. Add Engineering XLSX workbook import/export.
8. Expand runtime diagnostics, driver health, offline behavior and operational hardening.
9. Stabilize frontend package versions/lockfile and continue CI performance/hygiene improvements.

## Locked future product requirements

These requirements remain part of the EliteSCADA product north and must be implemented through the public engineering model.

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

The runtime, engineering contract and persistence foundation are now strong enough that editor work can begin incrementally after the current security baseline is established, rather than waiting for every future driver to exist.

The editor must consume the same public engineering model rather than own a private representation of project configuration. Reusable libraries, Engineering Fragments/cross-project copy-paste, trends, access-aware visibility and configurable shell regions are core workflows, not late add-ons.

The initial reusable dynamo/object catalog, common state model, faceplates, trend, shell widgets and reference-research procedure are defined in `docs/VISUAL-COMPONENT-LIBRARY.md`.
