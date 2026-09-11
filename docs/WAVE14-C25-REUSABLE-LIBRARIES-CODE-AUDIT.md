# Wave 14 C25.6 — Reusable Resource Libraries — Binding Code Audit

**Status:** C25.6 CODE AUDIT COMPLETE / FIRST IMPLEMENTATION SLICE AUTHORIZED  
**Tracking issue:** #282  
**Implementation PR:** #283  
**Branch:** `wave14/c25-post-demo`  
**Audited exact SHA:** `8794262ccd8ccf3a1b14c407878888855e85e0c5`

> GitHub live state remains the sole project authority. This audit applies the binding contracts in `docs/WAVE14-C25-REUSABLE-LIBRARIES-ARCHITECTURE.md` and `docs/WAVE14-C25-REUSABLE-LIBRARIES-ENGINEERING-LOCK-DECISION.md` to the actual shipped code at the audited SHA. It does not authorize a parallel project authority, a Runtime library dependency, or an Engineering Lock exemption.

## 1. Audit conclusion

EliteSCADA already has the canonical primitives required to build reusable libraries without inventing shadow resource models:

- canonical Engineering resource DTOs and registries;
- canonical JSON exchange through `EngineeringExchangeService`;
- a hardened ZIP/package vocabulary in `ProjectPackageService`;
- visual-asset metadata plus content-hash sidecars;
- an Engineering Workspace mutation gate with dirty/change-version authority;
- backend capability checks and audit conventions;
- a fail-closed Engineering Lock guard.

The `.escadalib` implementation must compose these primitives rather than calling full-project import or creating a second runtime/project model.

The first functional implementation slice is deliberately non-mutating:

1. define a versioned `.escadalib` manifest/container contract;
2. export a selected set of supported canonical resources into that container;
3. inspect and validate an untrusted `.escadalib` without mutating Working;
4. enforce backend Engineering capability and Engineering Lock on the corresponding API surface;
5. test archive/path/hash/schema/resource-identity behavior.

Association, dependency-closure incorporation and provenance mutation follow only after this package kernel is exact-SHA green.

## 2. Canonical application package boundary

### Audited code

- `src/Scada.Engineering/ProjectPackages/ProjectPackageService.cs`
- `src/Scada.Engineering/ImportExport/IEngineeringExchangeService.cs`
- `src/Scada.Engineering/ImportExport/EngineeringExchangeService.cs`

### Findings

`ProjectPackageService` is the canonical `.escadapkg` container boundary.

Current package facts:

- file extension: `.escadapkg`;
- package schema version: `3`;
- canonical Engineering entry: `engineering.json`;
- package metadata: `metadata.json`;
- package manifest: `manifest.json`;
- legacy/checksum entry: `checksum.sha256`;
- visual-asset sidecars under `assets/`;
- SHA-256 content validation;
- normalized archive member paths;
- inspect before mutation;
- expected ProjectKey validation before apply;
- visual-asset metadata/payload consistency validation.

`ProjectPackageService` delegates canonical Engineering JSON semantics to `IEngineeringExchangeService`. It does not itself define the project model.

### Binding implication for `.escadalib`

`.escadalib` should reuse the safe archive vocabulary where practical:

- versioned ZIP container;
- manifest as explicit authority for members;
- normalized relative member paths;
- SHA-256 per payload/member;
- declared byte length/media type where applicable;
- no undeclared or ambiguous payload authority;
- inspect/validate before any future Working mutation.

However, `.escadalib` **must not** call full-project `ImportAsync`/`Apply` as its incorporation mechanism. Full-project import has replace/update semantics across the canonical Engineering model and would violate `Association != Import` and selective dependency closure.

## 3. Canonical Engineering resource authority

### Audited code

- `src/Scada.Engineering/Contracts/EngineeringContracts.cs`
- `src/Scada.Engineering/Contracts/VisualCompositionEngineeringContracts.cs`
- `src/Scada.Engineering/Assets/EngineeringAssetRegistry.cs`
- `src/Scada.Engineering/Views/EngineeringViewRegistry.cs`
- `src/Scada.Engineering/Scripts/*`
- `src/Scada.Engineering/VisualAssets/*`
- `src/Scada.Engineering/ImportExport/EngineeringExchangeService.cs`

### Findings

`EngineeringExchangeService` is currently schema `scada.engineering`, version `16`, and exports/imports the canonical registries directly.

The actual canonical resource families relevant to reusable libraries are:

- `EquipmentTemplateEngineeringDto`;
- `DynamoEngineeringDto`;
- `ScreenEngineeringDto`;
- `PopupEngineeringDto`;
- `ScriptEngineeringDefinition`;
- `ScriptVisualEventReference` as script/HMI reference metadata rather than a standalone authoring resource;
- `VisualAssetEngineeringDto` plus payload bytes addressed by SHA-256.

The existing product also has canonical equipment instances, tags, alarms, data sources, commands, gateways, security roles, reports and operational events. Those are not initial library resource kinds merely because they are serializable. They carry application/deployment/security/process semantics and must not be dragged into reusable libraries without a separate dependency/product decision.

### Initial reusable resource kinds

The `.escadalib` manifest may define the following language-neutral kinds from the first package-kernel slice:

- `equipment-template`;
- `dynamo`;
- `screen`;
- `popup`;
- `script`;
- `visual-asset`.

Export/inspect support does **not** imply that every kind is immediately authorized for incorporation. Future incorporation is enabled per kind only after its complete reference/dependency closure is implemented and tested.

### Explicitly excluded from the initial reusable set

- equipment instances;
- tags;
- alarms;
- data sources/connections;
- commands;
- gateways;
- security roles/Authority data;
- reports;
- operational-event configuration;
- Engineering Lock verifier/state;
- license/trust/session/identity material.

These exclusions prevent a reusable visual/script catalog from quietly becoming a second project-package format.

## 4. Resource identity and collision authority

### Audited code

`InMemoryEngineeringAssetRegistry` and the corresponding view/script/visual-asset registries use stable entity IDs plus canonical keys/paths. Upserts may resolve by ID/key and can replace existing content.

### Binding implication

A library import resolver must never directly call a registry upsert before collision resolution. Registry upsert behavior is a storage primitive, not a user-approved collision policy.

The library manifest therefore needs its own stable source identity:

- stable `libraryId`;
- stable `resourceId` within the library;
- resource kind;
- source canonical ID/key/path where applicable;
- deterministic content hash;
- declared dependencies.

Future incorporation must first build a complete deterministic plan and only then invoke canonical registry mutations under the Workspace mutation lease.

Same-key/different-content must never silently overwrite an unrelated project resource.

## 5. Dependency audit

The current canonical model already exposes references that must participate in closure rather than being treated as flat JSON.

Observed dependency classes include:

- dynamo -> equipment template through `TemplateKey` where present;
- screen/popup visual elements -> dynamo through `DynamoKey`;
- popup -> equipment template through `TemplateKey` where present;
- visual navigation actions -> screen/popup target keys and command IDs;
- visual bindings -> tags/equipment paths;
- scripts -> explicit script Engineering dependencies and HMI/view references;
- visual assets -> content payload addressed by canonical SHA-256 metadata;
- HMI content may carry asset/script/action references that must be resolved by the actual canonical contracts before incorporation is enabled for that resource kind.

Because some references point to application-owned resources that are intentionally outside the reusable-library set, the future resolver must distinguish:

1. **library dependency** — must be present in the selected resource closure;
2. **project external dependency** — must already resolve in prospective Working or fail/require an explicit supported decision;
3. **unsupported dependency** — blocks incorporation before mutation.

The implementation must not solve an unresolved dependency by importing the entire library or silently omitting the reference.

## 6. Working, dirty state and concurrency

### Audited code

- `src/Scada.Api/Runtime/EngineeringWorkspace.cs`
- `src/Scada.Api/Runtime/EngineeringMutationEndpoints.cs`

`EngineeringWorkspace` owns:

- current project/revision descriptor;
- `IsDirty`;
- monotonic `ChangeVersion`;
- a `SemaphoreSlim` mutation gate;
- optimistic expected-version enforcement through `AcquireMutationAsync`;
- canonical registries whose `changed` callbacks invoke `MarkDirty()`.

`EngineeringMutationEndpoints` requires the `x-elitescada-workspace-version` header for mutations and translates version conflicts into explicit `409 Conflict` behavior.

### Binding implication

Future library association/disassociation or resource incorporation that changes durable Engineering state must:

1. require the workspace version contract;
2. acquire exactly one Workspace mutation lease for the complete logical operation;
3. validate the full resource/dependency/collision plan before mutation;
4. mutate canonical registries only inside that lease;
5. let canonical changed callbacks advance dirty/change-version;
6. return the resulting change version;
7. never mutate Published or Active directly.

The first package-kernel slice is read-only and therefore does not acquire or advance Workspace mutation state.

## 7. Association state decision for the first implementation

Association is not implemented in the first package-kernel slice.

When association is added, it must be a distinct Engineering-only catalog state and must not be represented by copied project resources.

The code audit does **not** authorize adding library associations to `RuntimeApplicationProjection`.

Before durable association is implemented, its exact persistence must be proven against Save/checkout/package roundtrip behavior. A library source path must never become Runtime authority. Persisting arbitrary absolute workstation/network paths in canonical application content is not approved by this audit.

Therefore the sequencing is:

1. package export/inspect kernel;
2. explicit Engineering-only association model with proven lifecycle semantics;
3. browse/preview from associated library;
4. dependency/collision plan;
5. atomic selective incorporation;
6. provenance persistence;
7. UI flow and browser proof.

## 8. Runtime boundary

### Audited code

`RuntimeApplicationProjection` is a separate projection from the Engineering package/Working representation.

The existing projection includes application runtime resources such as screens, popups, dynamos, scripts and visual assets. It does not expose Engineering Lock metadata as Runtime application authority.

### Binding implication

Library catalog/association/provenance source metadata must remain Engineering-only.

Only resources deliberately incorporated into canonical project content can later reach Runtime through the normal Save -> Publish -> Activate projection.

Runtime must never:

- open `.escadalib`;
- resolve a library path;
- monitor a source library;
- fetch missing library dependencies;
- treat a library version/hash as runtime authority.

## 9. Engineering Lock and backend capability authority

### Audited code

- `src/Scada.Api/Security/EngineeringLockAccess.cs`
- `src/Scada.Api/Runtime/EngineeringMutationEndpoints.cs`
- binding decision `docs/WAVE14-C25-REUSABLE-LIBRARIES-ENGINEERING-LOCK-DECISION.md`

`EngineeringLockAccess.ProtectedEngineeringFailure(...)` is the fail-closed backend guard for protected Engineering content.

The existing workspace-read exemptions are deliberately limited to canonical project-package/import/export/recovery/lock paths. Reusable libraries are semantically Engineering authoring and **must not** inherit those exemptions.

Equivalent protected mutations first check backend `SecurityCapability.EngineeringModify`, then Engineering Lock, then workspace-version/mutation authority, and audit denied/succeeded outcomes.

### Binding library API rule

Every reusable-library API must use its own `/api/engineering/libraries...` surface and must:

- require an authenticated backend Engineering capability appropriate to the action;
- reject while Engineering Lock is locked;
- never be added to `IsWorkspaceReadExempt` merely because `.escadalib` internally uses archive/import/export primitives;
- audit material operations using Audit authority, never Alarm or Operational Event.

For the initial package-kernel slice:

- create/export requires `EngineeringModify` because it reads protected authored resources for reuse outside the application;
- inspect of a supplied `.escadalib` through the Engineering library surface also requires Engineering access and remains Lock-protected;
- no frontend state is authorization authority.

## 10. Package-kernel contract authorized by this audit

The first code slice may introduce a dedicated `Scada.Engineering.Libraries` namespace with a service responsible only for `.escadalib` container construction and inspection.

The v1 container should use:

- extension `.escadalib`;
- schema identifier distinct from `scada.engineering` and `.escadapkg`;
- container schema version `1`;
- `manifest.json` as the authoritative resource/member index;
- payload members under deterministic kind/resource paths;
- SHA-256 hashes and declared lengths;
- stable language-neutral library/resource IDs;
- exact supported resource kind values;
- visual-asset binary payloads as sidecars rather than base64-expanded project JSON where the canonical asset model already separates metadata/payload;
- explicit compatibility with the canonical Engineering schema version used to serialize resource DTO payloads.

Inspection must reject before any mutation:

- unsupported future container schema version;
- duplicate stable resource identities;
- duplicate/ambiguous member paths;
- absolute/traversal archive paths;
- missing declared members;
- undeclared payload members except explicitly reserved metadata entries;
- byte-length mismatch;
- SHA-256 mismatch;
- unsupported resource kind;
- malformed canonical resource payload;
- invalid/missing dependency identity;
- manifest references outside the library.

Resource export must be deterministic for the same canonical content except for explicitly informational timestamps. Resource hashes must be calculated over canonical payload bytes, not display labels.

## 11. First implementation acceptance tests

Before the package kernel is considered complete, exact code tests must prove at least:

1. a valid `.escadalib` can be exported from selected supported canonical resources;
2. manifest resource IDs/kinds/member paths/hashes are stable and inspectable;
3. visual-asset payload hash/length/media type are validated;
4. path traversal and absolute archive members are rejected;
5. duplicate resource IDs/member paths are rejected;
6. missing and hash-mismatched payloads are rejected;
7. unsupported resource kinds/schema versions fail explicitly;
8. inspection/export does not mutate Working or advance `ChangeVersion`;
9. backend capability denial is enforced;
10. Engineering Lock denies export and inspect even when endpoint is invoked directly;
11. canonical application Import/Export and recovery exemptions remain unchanged;
12. existing C25 Engineering Lock, Restore-first and Runtime session browser contracts remain green;
13. C03/native/product compatibility gates remain green on the exact candidate SHA.

## 12. Implementation sequence from this checkpoint

1. **Code audit** — COMPLETE at audited SHA `8794262ccd8ccf3a1b14c407878888855e85e0c5`.
2. **Docs-only checkpoint** — this document; exact-SHA C25/C03 validation required before product mutation.
3. **Package kernel** — `.escadalib` manifest/container export + inspect, backend capability/Lock guard, unit/API tests.
4. **Association/catalog** — Engineering-only association state and browse/preview; no incorporation side effects.
5. **Selective incorporation** — deterministic dependency closure, collision plan, one atomic Working mutation.
6. **Provenance/disassociation** — project-owned incorporated content remains self-contained.
7. **Engineering UI** — Libraries surface, association, browse/preview/use/disassociate with locked-state hiding/deep-link denial.
8. **Browser + exact-SHA regression matrix** — existing C25 and C03 gates plus new library coverage.

No later step is accepted merely because an earlier serialization test is green.