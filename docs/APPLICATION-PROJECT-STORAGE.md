# EliteSCADA — Application and project storage

## Status

Locked product contract. Initial implementation uses the existing project-package v2 container and the canonical Engineering lifecycle.

## User model

An application developer must see two explicit operations:

1. **Save Application As…** — choose a name and directory and write one `.escadapkg` file;
2. **Open Application…** — choose an existing `.escadapkg`, inspect it, run Preview and apply it into Working.

When the browser exposes the File System Access save picker, EliteSCADA uses it so the developer chooses the exact file and directory. A normal browser download remains a compatibility fallback.

## Why one file

The Elipse E3 model usefully separates a Domain from one or more Project/Library files and resolves those files relative to the Domain directory. EliteSCADA adopts the useful concepts—explicit application identity, composition and portable relative ownership—without requiring ordinary projects to manage several physical files.

The initial `.escadapkg` is a single structured container with:

- `manifest.json`;
- canonical `engineering.json`;
- content-addressed project visual assets under `assets/`.

The developer moves, copies, versions or archives one application file. Internal entries are implementation details and are validated before use.

## Server lifecycle versus application file

| Concern | Authority |
|---|---|
| Current editable state | Working workspace on the EliteSCADA server |
| Immutable saves | PostgreSQL Engineering Revisions |
| Runtime selection | Published and Active revision lifecycle |
| Portable application | Developer-chosen `.escadapkg` file |
| Images/resources | Embedded content-addressed package assets |
| Secrets/credentials | External protected secret provider |
| Historian samples | Historian storage, not the application file |

`Ctrl+S`/Save Revision and Save Application As are not aliases. A revision records durable server lifecycle state. Save Application As materializes a portable file at a developer-selected location.

## Validation and opening

Opening never replaces Runtime directly:

`select file -> inspect container/integrity -> Preview -> choose merge mode -> Apply to Working -> save revision -> publish -> activate`

Invalid packages, checksum failures, unsupported versions or Engineering validation errors fail closed. Published and Active remain unchanged until their separate authorized lifecycle operations succeed.

## Evolution

Large systems may later use a thin domain/application descriptor that references multiple project or external library packages by paths relative to that descriptor. That is an additive future format and must include dependency/version validation. It must not turn the initial simple case back into a directory full of opaque loose files.
