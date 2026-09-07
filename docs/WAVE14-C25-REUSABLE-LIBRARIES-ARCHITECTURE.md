# Wave 14 C25.6 — Reusable Resource Libraries — Binding Architecture

**Status:** BINDING C25.6 ARCHITECTURE / IMPLEMENTATION NEXT  
**Coordinator package:** C25  
**Checkpoint:** C25.6  
**Tracking issue:** #282  
**Implementation PR:** #283  
**Branch:** `wave14/c25-post-demo`  
**Integration target:** `wave14/corrections-integration`

> GitHub live state is the sole project authority. This document records the Product Owner-approved reusable-library product contract before implementation. It does not redefine Runtime authority, package authority, security, licensing, Engineering Lock, Restore-first, lifecycle, Alarm, Operational Event or Audit contracts.

## 1. Product intent

EliteSCADA must support reusable engineering libraries so resources created in one application can be intentionally reused in another application without forcing projects to depend permanently on the source library.

The feature must let an engineer:

1. create/export a reusable library from supported canonical EliteSCADA resources;
2. associate one or more library files with the project being engineered;
3. browse/search/preview resources made available by associated libraries;
4. use/import only selected resources;
5. incorporate only the selected resource and the transitive dependencies actually required by it;
6. continue editing the incorporated copy as project-owned canonical content;
7. disassociate the source library later without breaking anything already incorporated into the project;
8. produce a final application package that is self-contained and has no Runtime dependency on the library.

This is generic product behavior. No EEE-specific workaround or project-specific library behavior is permitted.

## 2. Fundamental semantic distinction

### 2.1 Associating a library is not importing its contents

**Association != Import.**

Associating a library must:

- register it as an Engineering-time catalog/source of reusable resources;
- make its compatible resources discoverable to the engineer;
- retain only the minimum association metadata required to identify/reopen the catalog while the association exists;
- avoid mutating canonical project resources merely because the library was associated;
- avoid marking Working dirty solely because resource bytes were enumerated/read for preview, unless the association itself is intentionally persisted as project Engineering configuration;
- never copy the whole library into the application merely for convenience.

A library containing hundreds of objects, screens, images or scripts therefore adds zero of those resources to the application until they are explicitly used/imported.

### 2.2 Using/importing a resource incorporates it

When the engineer intentionally uses/imports a library resource, EliteSCADA must:

1. resolve the selected stable library resource identity;
2. validate type/schema/version compatibility;
3. resolve the complete transitive dependency closure required by that resource;
4. validate the complete closure before project mutation;
5. resolve deterministic resource-ID/name collisions before mutation;
6. incorporate only the selected resource plus required dependency closure into canonical Working project content;
7. record provenance metadata without retaining an operational dependency on the library;
8. pass through the normal Working dirty/change-version/lifecycle authority.

The incorporated resources become project-owned canonical content.

## 3. File/product boundary

The reusable library is a product artifact distinct from the application package.

The intended file extension is:

`.escadalib`

The exact binary/container implementation must reuse existing safe package/archive primitives where practical, but `.escadalib` and `.escadapkg` have different semantics:

- `.escadapkg` = portable application/project package and canonical application exchange boundary;
- `.escadalib` = reusable Engineering-time resource catalog/source;
- `.escadalib` is never Runtime application authority;
- `.escadalib` is never an Authority backup;
- `.escadalib` is never a license container;
- `.escadalib` does not replace project Save/Publish/Activate lifecycle.

The final `.escadapkg` must contain every resource actually required by the application regardless of whether any source library remains available.

## 4. Initial supported resource classes

C25.6 should support reusable resource classes that already have canonical product representations and can be safely transported without inventing shadow models.

The baseline target inventory is:

- reusable HMI/graphic objects and object templates;
- images;
- SVG/vector assets;
- screens and supported screen templates;
- scripts;
- other resources that the current EliteSCADA product already exposes as reusable/exportable artifacts, after live architecture audit confirms their canonical representation and dependency semantics.

Implementation must derive the actual first supported set from live code. A resource class must not be advertised merely because the concept sounds useful if the shipped product has no canonical import/export or dependency-safe representation yet.

Unsupported resource types must fail explicitly rather than being silently flattened or partially copied.

## 5. Canonical library manifest

A library must have a versioned manifest with stable, language-neutral identities.

At minimum the manifest model must be able to represent:

### Library identity

- schema/format version;
- stable `libraryId` independent from display name and file path;
- human-readable display name;
- library version;
- optional description/author/vendor metadata;
- product compatibility metadata required by actual shipped schemas.

### Resource identity

Each resource entry must include or be able to derive:

- stable resource ID within the library;
- resource type/kind;
- display name;
- resource/schema version where applicable;
- content hash or equivalent deterministic content identity;
- dependency identities;
- compatibility metadata where required;
- payload location/reference inside the library container.

Display names and localized descriptions are not resource authority. Stable IDs remain language-neutral.

## 6. Dependency closure

Resource reuse must be dependency-aware.

Example:

- a reusable screen may reference reusable graphic objects;
- those objects may reference SVG/images;
- a reusable object or screen may reference scripts or other supported resource types.

Importing the screen must therefore incorporate the screen plus only the dependency closure actually needed by it.

The dependency resolver must:

- resolve dependencies transitively;
- detect missing dependencies;
- detect cycles without infinite traversal;
- validate all dependency types before mutation;
- deduplicate identical dependency resources;
- fail before partial project mutation if the closure is invalid;
- produce deterministic results for the same project/library inputs.

The implementation must not solve dependency complexity by importing the entire library.

## 7. Project ownership and provenance

After successful incorporation, the project is the operational owner of the resource.

The project may retain provenance such as:

- source `libraryId`;
- source library version;
- source resource ID;
- source resource version;
- source content hash;
- imported timestamp/version metadata where useful and deterministic.

Provenance is informational and may support future explicit update/comparison workflows. It must not imply a live dependency on the source file.

Project-owned IDs must remain valid after the source library disappears.

## 8. Disassociation contract

Disassociating a library must be safe and unsurprising.

After disassociation:

- resources that were merely visible in the external catalog disappear from that catalog source;
- resources never incorporated into the project are not copied as a side effect of disassociation;
- every resource previously incorporated remains in the project;
- screens continue to resolve incorporated objects/assets/scripts;
- scripts and assets already incorporated remain available;
- Runtime remains unaffected;
- `.escadapkg` export remains complete;
- historical provenance may remain visible as informational metadata, including that the source library is no longer associated.

No incorporated resource may become broken merely because the associated `.escadalib` file was moved, deleted, unavailable or deliberately detached.

## 9. Runtime and Active authority

Reusable libraries are Engineering-time inputs only.

Binding invariants:

- backend Active revision remains Runtime application authority;
- Runtime must never open, resolve, monitor, mount or depend on `.escadalib` files;
- Runtime must never depend on the original library path, network share or external repository;
- Publish/Activate must operate on canonical project content, not library references;
- a missing library after Publish/Activate cannot affect the running Active application;
- final application behavior must be reproducible from the canonical project/package content alone.

## 10. Updates and version drift

C25.6 must not automatically replace incorporated project content when a source library changes.

If a newer library version or newer source-resource hash is detected, C25.6 may expose informative version/provenance status only if this can be done without expanding risk/scope.

Any future update mechanism must be explicit and user-authorized and should support comparison/impact review.

Out of scope for the initial C25.6 implementation unless required by existing product architecture:

- automatic updates;
- background replacement of incorporated resources;
- automatic project migration to a newer library;
- remote corporate library repositories;
- cloud marketplace/catalog service;
- package signing/trust distribution for libraries;
- centralized version approval workflow;
- visual diff/merge of library resource revisions.

Those may be future product increments.

## 11. Identity and collision handling

Stable resource identity and collision behavior must be deliberate.

Import/use must safely handle at least:

- same resource name with different identity/content;
- same source library resource imported more than once;
- identical dependency reached through multiple selected resources;
- source resource whose preferred project ID is already occupied;
- dependency graph containing a resource already incorporated from the same source/hash;
- name/path constraints imposed by existing project resource stores.

The resolver must not silently overwrite unrelated project content.

Where an identical source resource is already incorporated, reuse/deduplication should be preferred when the canonical representation safely permits it.

Where content differs, the product must require deterministic distinct identity or an explicit supported conflict decision; never silently replace existing project content.

## 12. Lifecycle and concurrency

All project mutations produced by library use/import must compose with canonical Engineering lifecycle behavior.

Required properties:

- use/import operates against Working, never directly against Published/Active;
- successful incorporation marks Working dirty/change-version through the existing mutation model;
- Save creates normal revision authority;
- Publish and Activate retain existing guards;
- package export/import round-trips incorporated resources as normal application content;
- library import/use must respect existing mutation/concurrency gates;
- validation must occur before mutation where practical;
- failure must not leave a silently half-imported dependency graph.

Association metadata, if persisted, must have explicit lifecycle semantics and must never be confused with incorporated application content.

## 13. Security, capabilities and Engineering Lock

C25.6 must preserve all existing C25 security boundaries.

- Backend Authority remains the first capability authority.
- Library creation/export, association and resource incorporation require the appropriate existing Engineering/import/export authority as determined by live code audit; no frontend-only authorization.
- Engineering Lock remains an additional application-content policy and must not be bypassed through library preview/import/use.
- A locked application must not leak protected Engineering content through library export or catalog operations beyond the already-approved locked Import/Export/recovery exemptions.
- Library parsing must treat input as untrusted data and reuse existing package/archive validation limits against malformed paths, oversized payloads and invalid schemas where applicable.
- `.escadalib` must not carry or restore Authority credentials, active sessions, JWT/cookies, license trust material or Engineering Lock plaintext secrets.
- Script resources are code-bearing content and must follow the same capability/lifecycle/safety contracts as scripts imported by existing canonical mechanisms.

## 14. Audit and semantic separation

Library operations that materially change project Engineering state should be attributable through the existing audit authority where equivalent Engineering/package mutations are audited.

Potential auditable actions include:

- library associated/disassociated, if association is durable product state;
- library created/exported;
- resource incorporated/imported;
- conflict/update decision if later supported.

Do not misuse Alarm or Operational Event as the audit channel.

Alarm / Operational Event / Audit remain semantically distinct.

## 15. UX contract

The Engineering UI should expose a coherent Libraries surface rather than scattering raw file-picker behavior across unrelated editors.

Minimum conceptual operations:

- `Bibliotecas` management surface;
- `Associar biblioteca`;
- `Criar/Exportar biblioteca`;
- browse/search supported resources by type;
- preview where a safe canonical preview exists;
- `Usar` / `Importar` selected resource;
- clear indication of selected resource dependencies before/while incorporating where practical;
- `Desassociar` without deleting incorporated resources;
- provenance indication on incorporated resources where useful.

Exact navigation placement must reuse current Engineering shell/navigation patterns identified by live frontend audit.

The UI must communicate the distinction between an associated external resource and an incorporated project-owned resource.

## 16. Required validation before C25.6 closure

C25.6 cannot be declared complete only because serialization tests pass.

At minimum the implementation must prove:

1. `.escadalib` creation/export and inspection for supported resource types;
2. association alone does not incorporate library resources into canonical project content;
3. selecting/using one resource incorporates only it plus required transitive dependencies;
4. duplicate/transitive dependency resolution is deterministic;
5. invalid/missing dependency fails before partial project mutation;
6. ID/name collision cannot silently overwrite unrelated project content;
7. imported content becomes normal Working project content and participates in dirty/change-version lifecycle;
8. incorporated resources survive Save/package roundtrip as application-owned content;
9. disassociation preserves every incorporated resource and leaves final package valid;
10. missing/deleted source `.escadalib` after incorporation cannot break Runtime/Active;
11. capability/Engineering Lock boundaries remain backend enforced;
12. script/code-bearing resource behavior does not bypass existing Script safety contracts;
13. browser coverage proves the principal Engineering association -> browse -> use -> disassociate UX;
14. existing C25 Engineering Lock, Restore-first and Runtime session browser contracts remain green;
15. required C03/native/product compatibility gates remain green on the exact candidate SHA.

## 17. C25 sequencing after Product Owner decision

The Product Owner explicitly moved reusable libraries ahead of Help/manual so the shipped contextual manual can document the completed library behavior rather than immediately becoming stale.

C25 checkpoints are therefore:

- C25.0 — durable bootstrap + architecture/code audit — COMPLETE;
- C25.1 — Engineering Lock domain/package/security — COMPLETE;
- C25.2 — Engineering Lock backend enforcement — COMPLETE;
- C25.3 — Engineering Lock UI/lifecycle/package — COMPLETE;
- C25.4 — Restore-first / System Recovery — COMPLETE;
- C25.5 — Runtime session UX — COMPLETE after exact-SHA closure evidence is recorded;
- **C25.6 — Reusable Resource Libraries — IN PROGRESS;**
- C25.7 — Contextual multilingual Help/manual;
- C25.8 — integrated regression/audit pass;
- C25.9 — exact final candidate matrix and acceptance.

Checkpoint renumbering affects only not-yet-started work. C25.0 through C25.5 retain their historical identities.

## 18. Implementation discipline

Before product mutation, C25.6 must audit live source for:

- canonical `.escadapkg` inspect/preview/apply/export implementations;
- canonical Engineering Working resource representation;
- screen/popup/dynamo/object persistence;
- image/vector/static asset stores;
- Script persistence and public API boundaries;
- existing export/import/resource-copy utilities;
- resource references/dependency handling;
- project dirty/change-version/lifecycle gates;
- capability, Engineering Lock and audit gates around equivalent operations;
- current browser/UI import/export patterns.

The implementation must extend/reuse those canonical mechanisms rather than inventing a parallel project/resource authority.

No C25.6 product behavior is accepted until exact-SHA tests/CI are inspected and durably recorded.