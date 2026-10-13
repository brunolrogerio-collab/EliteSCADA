# ADR-004 — Engineering Import/Export as a core capability

## Status
Accepted and active.

## Decision
Engineering configuration is serializable, versioned and exchangeable independently of the graphical editor.

The canonical technical format is JSON with schema `scada.engineering` and an explicit schema version (currently v23). CSV and XLSX are focused bulk formats for flat Engineering collections. They are projections over the canonical package and must use its existing Preview/Apply, validation, merge-mode, authorization, audit and Workspace-version lifecycle; they must not add a second mutation or persistence authority.

Tabular exchange is one entity collection per file (and one worksheet per XLSX). Current flat scope: TAGs, Alarms, Data Sources, Gateway routes and Engineering Operational Event definitions. Runtime event occurrences/history and audit records are excluded. Screens, popups, templates, Dynamos, scripts, nested dependencies and reusable resources remain in canonical JSON / Engineering Fragments so flattening cannot lose graph structure.

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
- CSV and XLSX import/export for TAGs, Alarms, Data Sources, Gateway routes and Operational Event definitions.
- Stable IDs plus portable path/key reconciliation; invariant-culture numbers; row-numbered parse diagnostics; bounded input; CSV spreadsheet-formula protection; XLSX formulas rejected.
- Plaintext credential-like Data Source settings and TAG communication-binding settings fail closed on tabular export; secret references remain portable references only.
- Tabular imports produce a partial canonical package containing only the selected collection and then run through canonical Preview/Apply for all three merge modes. Existing current TAGs may satisfy a tabular Operational Event reference without being redundantly included in the import.
- XLSX is implemented with the platform ZIP/XML APIs only; no Office automation or third-party spreadsheet runtime is required.
- Automated JSON export -> import preview round-trip in Chromium as part of CI.

Backward compatibility is maintained for older package versions. Missing entity collections in older schemas are normalized to empty collections during parsing.

## Mandatory gate before persistence/real drivers

Before PostgreSQL/TimescaleDB becomes the canonical persistence layer and before real Modbus TCP configuration is promoted into the main runtime, the Engineering contract must close the remaining baseline gaps:

1. Explicit TAG access/permission policy in the engineering contract and runtime model.
2. Full fidelity audit of CSV fields, especially historian maximum-period and metadata round-trip.
3. Project/package manifest suitable for complete engineering backup/restore.
4. Schema migration tests for supported historical package versions.

The persistence model must follow the public engineering contract rather than silently becoming the only authoritative representation.

## Remaining scope using the same infrastructure

- Full project package and backup/restore.
- SQL mappings/connectors.
- Plugin-owned versioned configuration schemas.
- Engineering audit/diff and change history.
- Authentication/authorization enforcement based on the explicit engineering permission model.
