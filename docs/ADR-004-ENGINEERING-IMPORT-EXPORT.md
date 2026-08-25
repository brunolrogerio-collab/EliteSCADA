# ADR-004 — Engineering Import/Export as a core capability

## Status
Accepted.

## Decision
Engineering configuration is serializable, versioned and exchangeable independently of the graphical editor.

The canonical technical format is JSON with schema `scada.engineering` and an explicit schema version. CSV is supported for bulk human engineering of tags and alarms. XLSX will be added through the same service boundary without changing the canonical model.

Import is never applied directly. The mandatory flow is:

`parse -> validate -> preview -> choose merge mode -> apply`

Merge modes:

- `CreateOnly`
- `UpdateExisting`
- `CreateAndUpdate`

Tags use a stable internal GUID plus `Path` as the logical engineering key. An import that matches an existing Path preserves the existing stable ID unless a deliberate migration rule says otherwise.

Secrets, passwords, private keys and tokens must never be serialized in clear text by this subsystem. Future driver/data-source exporters expose secret references only.

## Initial scope

- Tags: JSON and CSV import/export.
- Alarms: JSON and CSV import/export.
- Validation and preview with create/update/skip/error classification.
- Historian policy, address, scaling and engineering metadata included in tag engineering DTOs.
- Alarm class, delay, acknowledgement policy and shelving policy included in alarm engineering DTOs.
- REST endpoints expose export, preview and apply operations.

## Future scope using the same infrastructure

- XLSX engineering workbooks.
- Data sources and driver configuration.
- Screens and SVG engineering documents.
- Dynamos, equipment templates and popups.
- SQL mappings/connectors.
- Full project package and backup/restore.
- Plugin-owned versioned configuration schemas.
