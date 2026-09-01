# ADR-006 — Versioned EliteSCADA project package

## Status
Accepted.

The package is also the initial user-facing portable **application file**. See `APPLICATION-PROJECT-STORAGE.md`. Server Working/Revisions remain distinct lifecycle persistence.

## Context

Engineering configuration can already be exported as canonical JSON, but a practical project backup needs an outer container that identifies the project, records the engineering schema version and verifies that the payload was not silently truncated or altered.

A backup format must also remain independent of the graphical editor and must not bypass the normal engineering validation/preview/apply pipeline during restore.

## Decision

EliteSCADA project engineering backups use the extension `.escadapkg` and media type `application/vnd.elitescada.project-package`.

Package format v1 is a ZIP container with exactly two root entries:

- `manifest.json`
- `engineering.json`

`engineering.json` is the normal canonical `scada.engineering` payload. The project-package subsystem does not create a second private engineering representation.

`manifest.json` records:

- package format and version;
- package UUID;
- UTC creation timestamp;
- product identifier (`EliteSCADA`);
- project key and project name;
- engineering schema and schema version;
- packaged file path, media type, byte length and SHA-256 checksum.

## Restore flow

A project package is never blindly restored. The required flow is:

`read package -> validate container -> validate manifest -> verify checksum -> parse engineering -> preview -> apply`

The normal engineering merge modes remain authoritative:

- `CreateOnly`
- `UpdateExisting`
- `CreateAndUpdate`

The API exposes separate export, inspect, preview and apply operations.

## Integrity and safety

Package v1 validation rejects:

- unexpected or duplicate archive entries;
- unsafe archive paths;
- unsupported format/product/version;
- invalid project identity or manifest fields;
- payloads beyond configured safety limits;
- length mismatch;
- SHA-256 mismatch;
- invalid UTF-8 engineering payloads;
- disagreement between manifest engineering schema/version and the contained engineering document.

This integrity check detects accidental or unsophisticated modification. It is not a cryptographic signature or proof of publisher identity. Package signing can be added as a future format capability if deployment requirements justify it.

## Scope of package v1

Package v1 is an **engineering project backup**, not a full operational database image.

It contains the serializable project engineering model, including the entities supported by the current engineering schema. It intentionally does not contain:

- plaintext passwords, tokens, private keys or other secrets;
- historian time-series samples;
- transient runtime values;
- active sessions;
- authentication credentials/password hashes;
- operating-system or deployment secrets.

Secret references may be present through the normal engineering model, but the referenced secret material remains outside the package.

Historian-data backup, deployment-state backup and signed/release packages may be introduced later as separate, explicit capabilities rather than silently expanding the semantics of this format.

## Evolution

Additional package files require a future project-package format version with explicit compatibility rules. Existing v1 readers require exactly `manifest.json` and `engineering.json`, preventing unknown contents from being silently trusted.

The engineering schema version and project-package format version are intentionally independent. A future engineering schema can still use package format v1 as long as the reader supports that engineering schema and the package structure itself has not changed.
