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

Only after the runtime, engineering contract and persistence layer are stable should the SVG/editor experience become the primary development focus. The editor must consume the same public engineering model rather than own a private representation of project configuration.
