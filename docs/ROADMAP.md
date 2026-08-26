# Roadmap baseline

The approved development north is preserved, with Engineering Import/Export acting as a mandatory cross-cutting gate rather than a later utility.

## Runtime foundation already established

1. Repository, architecture and CI/CD foundation.
2. Tag Engine + quality + current-value cache + internal Event Bus.
3. Simulation Driver.
4. Driver SDK contract.
5. REST + WebSocket runtime.
6. First runtime screen and equipment modal.
7. Automated .NET build/test/runtime smoke validation.
8. Automated Chromium end-to-end validation.

## Engineering Import/Export status

Every engineering entity introduced must define a stable serialization contract and participate in the common validation/preview/apply pipeline.

Implemented in canonical JSON schema v4:

- Tags.
- Alarms.
- Data Sources / driver configuration with protected secret references.
- Equipment Templates.
- Equipment instances.
- Dynamos.
- Screens.
- Popups.
- Cross-reference validation.
- Backward-compatible parsing of older package versions.
- Export -> import preview round-trip testing in CI.

Bulk CSV engineering is implemented for Tags, Alarms and Data Sources.

## Immediate execution slice

Before PostgreSQL/TimescaleDB persistence and real Modbus TCP are promoted to the main development track:

1. Add explicit TAG permission/access-policy serialization.
2. Close CSV round-trip fidelity gaps, including historian maximum-period and metadata.
3. Add a complete project-package manifest for backup/restore.
4. Add explicit migration tests for historical engineering schema versions.
5. Refactor the Engineering Exchange service into smaller entity handlers before its scope grows further.

After that gate:

6. PostgreSQL persistence for engineering configuration and runtime state.
7. TimescaleDB historian persistence and retention policies.
8. Real Modbus TCP Data Source/driver implementation driven by the same engineering model.
9. MQTT driver integration.
10. Engineering XLSX workbook import/export.
11. Authentication/authorization enforcement and audit trail.

## Locked future product requirements

These requirements are part of the EliteSCADA product north but are intentionally not promoted ahead of the immediate persistence/driver gate.

### Reusable libraries across applications

- Evolve Equipment Templates/Equipment and Dynamos into a version-aware reusable library experience.
- Preserve a class/instance model conceptually similar to the reuse obtained from Elipse E3 XObject/XControl, while using EliteSCADA's own contracts and implementation.
- Allow reusable definitions to expose properties/bindings and be instantiated with application-specific context.
- Support nested reusable components with deterministic dependency validation.
- Make library definitions importable/exportable independently of the graphical editor.

### Cross-project copy/paste

- Copy/paste screens, popups, equipment, Dynamos and engineering structures between projects.
- Represent clipboard payloads as canonical Engineering Fragments rather than browser-private data.
- Support dependency-aware paste with preview for create/update/conflict/missing dependency and rebinding cases.
- Offer both selected-only and selected-with-required-dependencies workflows.

### Trend charts

- Provide engineering-configurable trends with multiple Pens.
- Allow each Pen to use historical TAG data or a live/runtime binding.
- Support project-defined trends placed on screens/popups.
- Permit ad-hoc and saved runtime trends where access policy allows.
- Keep historical query and realtime subscription semantics distinct even when displayed together.
- Later expose suitable aggregation/downsampling for TimescaleDB without leaking storage-specific concepts into the engineering contract.

### Users, roles and hierarchy

- Roles and access hierarchy are configurable per application, not hard-coded globally.
- Distinguish at minimum: view, operational command, setpoint/process-value write, alarm acknowledgement, alarm shelving, trend use/save, engineering modification, user/role administration and system administration.
- Allow access scope by area, equipment, screen, TAG or command where required by the application.
- Permit UI objects/screens/menus to be hidden or disabled based on policy.
- Never rely on visibility as the security boundary; backend/API authorization must enforce protected operations.
- Engineering packages may contain role/policy definitions but never authentication secrets or password hashes.

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

Only after the runtime, engineering contract and persistence layer are stable should the SVG/editor experience become the primary development focus. The editor must consume the same public engineering model rather than own a private representation of project configuration.

When that phase begins, the editor must treat reusable libraries, engineering fragments/cross-project copy-paste, trends, access-aware visibility and configurable shell regions as core workflows rather than late add-ons.

The initial reusable dynamo/object catalog, common state model, faceplates, trend, shell widgets and future reference-research procedure are defined in `docs/VISUAL-COMPONENT-LIBRARY.md`.