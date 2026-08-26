# ADR-004 — Engineering Import/Export as a core capability

## Status
Accepted and active.

## Decision
Engineering configuration is serializable, versioned and exchangeable independently of the graphical editor.

The canonical technical format is JSON with schema `scada.engineering` and an explicit schema version. CSV is supported for focused bulk engineering where a tabular representation is appropriate. XLSX remains planned through the same service boundary without replacing the canonical JSON model.

Import is never applied directly. The mandatory flow is:

`parse -> validate -> preview -> choose merge mode -> apply`

Merge modes:

- `CreateOnly`
- `UpdateExisting`
- `CreateAndUpdate`

Tags use a stable internal GUID plus `Path` as the logical engineering key. An import that matches an existing Path preserves the existing stable ID unless a deliberate migration rule says otherwise.

Secrets, passwords, private keys and tokens must never be serialized in clear text by this subsystem. Data Sources expose technical settings separately from `secretReferences`. Accepted secret references are explicit references such as `secret://`, `env://`, `vault://` and `keyvault://`.

## Implemented scope

The canonical package is currently at schema version 4 and includes:

- Tags, including datasource/source, address, scaling, historian settings and metadata.
- Alarms, including TAG reference, type, priority, setpoint, class, area, message, delay, acknowledgement and shelving policy.
- Data Sources and driver configuration, with plaintext-secret rejection and separate secret references.
- Equipment Templates with bindings, properties and context.
- Equipment instances with template references, bindings, properties and context.
- Dynamos with reusable bindings, properties and context.
- Screens with routes, recursive visual elements, bindings, properties and context.
- Popups with reusable visual definitions, bindings, properties and context.
- Validation of cross-references between TAGs, Data Sources, Templates, Equipment and Dynamos before apply.
- REST endpoints for canonical JSON export, preview and apply.
- CSV import/export for Tags, Alarms and Data Sources.
- Automated JSON export -> import preview round-trip in Chromium as part of CI.

Backward compatibility is maintained for older package versions. Missing entity collections in older schemas are normalized to empty collections during parsing.

## Mandatory gate before persistence/real drivers

Before PostgreSQL/TimescaleDB becomes the canonical persistence layer and before real Modbus TCP configuration is promoted into the main runtime, the Engineering contract must close the remaining baseline gaps:

1. Explicit TAG access/permission policy in the engineering contract and runtime model.
2. Full fidelity audit of CSV fields, especially historian maximum-period and metadata round-trip.
3. Project/package manifest suitable for complete engineering backup/restore.
4. Schema migration tests for supported historical package versions.

The persistence model must follow the public engineering contract rather than silently becoming the only authoritative representation.

## Next scope using the same infrastructure

- XLSX engineering workbooks.
- Full project package and backup/restore.
- SQL mappings/connectors.
- Plugin-owned versioned configuration schemas.
- Engineering audit/diff and change history.
- Authentication/authorization enforcement based on the explicit engineering permission model.
