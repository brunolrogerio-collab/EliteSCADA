# Wave 14 C25.6 — Reusable Resource Libraries — Implementation Status

**Status:** ACTIVE / BACKEND-PACKAGE-LIFECYCLE EXACT-SHA GREEN / UI PENDING / NOT COMPLETE / NOT ACCEPTED / NOT INTEGRATED  
**Coordinator package:** C25  
**Checkpoint:** C25.6  
**Tracking issue:** #282  
**Implementation PR:** #283  
**Branch:** `wave14/c25-post-demo`  
**Integration target:** `wave14/corrections-integration`

> GitHub live state remains the sole project authority. This document records the latest resumable C25.6 implementation checkpoint. It does not authorize merge, integration, C11 synchronization or any `main` mutation.

## 1. Latest exact validated product checkpoint

Validated product/test SHA:

`b65a433054b971bdd7c1815bf448a4da2dc7a397`

Exact-SHA CI:

- Wave 14 C25 Post-Demo #259 / run `34050831077` — **SUCCESS**;
- Wave 14 C03 DNP3 Adapter #272 / run `34050831076` — **SUCCESS**, including Managed validation, Linux/Windows native host, real OpenDNP3↔dnp3py L3 interoperability and Windows commercial publish dependency gate.

This SHA closes the backend/package/lifecycle Screen/Popup v1 slice on top of the previously green Template, Dynamo, VisualAsset, provenance and Script slices. It is not final C25.6 or C25 acceptance.

## 2. `.escadalib` package kernel and association

`.escadalib` remains distinct from `.escadapkg` and uses `elitescada.resource-library` format version 1.

Package validation includes stable library/resource identities, deterministic payload paths, SHA-256/length validation, visual-asset sidecars, archive traversal/path rejection, undeclared/orphan payload rejection and canonical Engineering schema compatibility.

Current export/incorporation-enabled resource kinds are:

- `equipment-template`;
- `dynamo`;
- `visual-asset`;
- `script`;
- `screen`;
- `popup`.

Association remains catalog availability only:

- `association != import`;
- association does not mutate Working;
- catalog is process-local and project/session scoped;
- no source path, network share, repository or catalog handle becomes Runtime/application authority;
- identical same-ID association is idempotent;
- same library ID with different content fails explicitly;
- disassociation cannot invalidate already-incorporated project-owned content.

## 3. Common selective-incorporation authority

All enabled reusable resources follow the same canonical flow:

1. inspect and verify `.escadalib`;
2. resolve exact transitive dependency closure;
3. reject missing/cyclic/unsupported edges;
4. perform stable ID/key/path/content collision checks;
5. build a canonical `EngineeringPackage` subset;
6. run canonical Engineering Preview;
7. require `x-elitescada-workspace-version` optimistic concurrency;
8. acquire the Workspace mutation lease;
9. re-preview under the lease;
10. apply with canonical `EngineeringExchangeService` CreateOnly behavior;
11. rely on canonical registry dirty/ChangeVersion callbacks;
12. audit success, denial, conflict and failure.

Identical project-owned content deduplicates. Same identity with different canonical content never silently overwrites unrelated project content.

## 4. Reusable Dynamo v1

`ReusableDynamoDependencyAnalyzer` remains the Dynamo portability authority.

Supported closure:

- Dynamo -> EquipmentTemplate through `TemplateKey`;
- Dynamo -> VisualAsset through canonical `core.image` `assetRef`.

It rejects concrete TAG/ClientMemory data, concrete equipment context, project-bound dynamic/expression dependencies, navigation/command actions and nested Dynamos. Reusable libraries do not weaken the canonical no-nested-Dynamo v1 contract.

## 5. Portable Script v1

`ReusableScriptDependencyAnalyzer` remains the Script portability authority.

Supported closure:

- Script -> Script transitively, same scope only.

It rejects missing/self/cyclic/cross-scope Script closure and project-owned dependency kinds including VisualDefinition, VisualObject, Tag, ClientMemoryTag, ServerMemoryTag and Resource. `TagChanged`, `ClientMemoryChanged` and existing `ScriptVisualEventReference` coupling are rejected rather than silently detached.

Canonical Script validation and Python preflight remain authoritative.

## 6. Screen/Popup reusable v1

`ReusableViewDependencyAnalyzer` is now the single Screen/Popup portability authority used by export and incorporation validation.

### Supported closure

Reusable Screen v1 may depend on:

- Dynamo;
- VisualAsset.

Reusable Popup v1 may depend on:

- EquipmentTemplate through `TemplateKey`;
- Dynamo;
- VisualAsset.

Parameterized authoring such as `{equipmentPath}` bindings remains portable.

### Fail-closed project-bound behavior

Screen/Popup v1 rejects rather than silently omits:

- concrete TAG/ClientMemory identities or targets;
- concrete `EquipmentPath`;
- concrete Dynamo TAG-reference parameters;
- project-bound dynamic sources and expression dependencies;
- navigation/command actions, including screen/popup navigation and command execution;
- existing Script/HMI event associations to the view;
- Script dependencies on the view/visual objects.

Navigation/command target closure is deliberately deferred instead of implicitly copying arbitrary project content.

### Validated behavior

Exact-SHA tests prove:

- Screen dependency closure through Dynamo and VisualAsset;
- Popup dependency closure through EquipmentTemplate, Dynamo and VisualAsset;
- declared manifest dependency sets must exactly match canonical payload content;
- selective Screen and Popup incorporation through canonical Preview/Apply;
- explicit stable ID/key collision failure;
- informational provenance on incorporated Screen/Popup;
- repeat incorporation deduplicates;
- `.escadapkg` roundtrip restores Screen, Popup and their incorporated dependencies with no `.escadalib` or catalog present;
- real `EngineeringWorkspace` Screen→Dynamo incorporation marks Working dirty and advances ChangeVersion;
- re-deduplication does not advance ChangeVersion.

## 7. Informational provenance and self-contained ownership

Incorporated reusable resources receive informational origin metadata under:

`elitescada.reusable.origin.*`

It records source library/resource identity/version/kind and source payload SHA-256 only. It stores no source path, catalog handle or Runtime dependency.

Validated semantics:

- provenance survives `.escadapkg` roundtrip;
- functional collision comparison ignores only the reserved origin namespace;
- repeat incorporation remains idempotent;
- re-export into a new `.escadalib` strips old origin metadata while keeping ordinary authored metadata;
- final `.escadapkg` remains self-contained;
- Runtime/Active never opens or resolves `.escadalib`.

## 8. Security and lifecycle boundaries

Reusable-library operations preserve:

- backend `EngineeringModify` authority;
- fail-closed Engineering Lock;
- Audit attribution distinct from Alarm/Operational Event;
- Workspace optimistic concurrency;
- Working-only mutation;
- canonical Save/Publish/Activate boundaries;
- existing Engineering Lock, Restore-first and Runtime session regressions.

## 9. Remaining C25.6 work — Engineering Libraries UI/browser

Backend/package/resource semantics are now stable enough for the Engineering UI projection.

The remaining C25.6 product slice must provide coherent Engineering UI for:

- associate `.escadalib` without dirtying Working;
- browse/search associated libraries and resources;
- inspect resource kind, identity and dependency closure;
- create/export `.escadalib` from selected canonical Working resources;
- selectively `Usar` / incorporate a resource using the current Workspace ChangeVersion;
- show informational provenance for project-owned resources where practical;
- disassociate a library without deleting incorporated project content;
- surface explicit collision/invalid-library/concurrency errors;
- remain unavailable while Engineering Lock is locked;
- inherit existing backend capability authority;
- avoid localStorage or another browser-side catalog authority.

### Locale audit note

The live Engineering shell currently maintains `elitescada.engineering.locale`, while the application shell uses `elitescada.locale`. C25.6 Libraries UI must not introduce a third locale state. C25.7 contextual Help/manual must resolve the existing locale-authority duplication so Help follows the same active application UI locale.

## 10. Next execution order

1. implement the Engineering Libraries UI as a projection of the current backend catalog/export/incorporation APIs;
2. add focused browser contract coverage for association-without-dirty, catalog/resource visibility, selective incorporation with ChangeVersion and disassociation;
3. prove locked Engineering does not mount/expose the Libraries surface;
4. run exact-SHA C25 + C03 regression matrix;
5. record C25.6 backend/package/UI exact authority and close C25.6 only if no unresolved audit gap remains;
6. begin C25.7 contextual multilingual Help/manual only after C25.6 is stable.

## 11. Permanent governance

- #283 remains OPEN/DRAFT and targets only `wave14/corrections-integration`;
- #212 remains OPEN/DRAFT and must not merge to `main` without later explicit Product Owner authorization;
- never modify `main` directly;
- no force push, destructive rebase, branch deletion or unrelated cleanup;
- diagnose every CI red before rerun;
- never weaken tests, validation, authentication, authorization, licensing, lifecycle, package or Runtime authority for green CI;
- C11 remains frozen at `41d24d89c3b9d2b881215255e44023fabde262f3` until C25 is fully accepted, integrated only into the integration branch and post-merge exact-SHA revalidated;
- #266 remains validation-only and MUST NEVER MERGE;
- Backend Active revision remains Runtime application authority;
- Alarm / Operational Event / Audit remain distinct;
- no EEE-specific workaround;
- Wave13 #205/#207 remains paused.
