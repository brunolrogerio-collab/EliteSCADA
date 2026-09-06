# Wave 14 C25.6 — Reusable Resource Libraries — Implementation Status

**Status:** ACTIVE / PARTIAL IMPLEMENTATION EXACT-SHA GREEN / NOT COMPLETE / NOT ACCEPTED / NOT INTEGRATED  
**Coordinator package:** C25  
**Checkpoint:** C25.6  
**Tracking issue:** #282  
**Implementation PR:** #283  
**Branch:** `wave14/c25-post-demo`  
**Integration target:** `wave14/corrections-integration`

> GitHub live state remains the sole project authority. This document records the latest exact-SHA validated C25.6 implementation checkpoint and supplements the older execution-ledger C25.6 opening text. It does not authorize merge, integration, C11 synchronization or any `main` mutation.

## 1. Exact validated checkpoint

Validated product/test SHA:

`a0468f3857abe73165c1746a92ee55d44ccea861`

Exact-SHA CI:

- Wave 14 C25 Post-Demo #215 / run `34045685446` — **SUCCESS**;
- Wave 14 C03 DNP3 Adapter #250 / run `34045685434` — **SUCCESS**, including managed validation, Linux/Windows native host, real OpenDNP3↔dnp3py L3 interoperability and Windows commercial publish dependency gate.

This is a C25.6 checkpoint only. It is not final C25 acceptance.

## 2. Package kernel — implemented and green

The reusable-library artifact is implemented as `.escadalib`, distinct from `.escadapkg`.

Current package kernel provides:

- versioned `elitescada.resource-library` manifest/container contract;
- stable library/resource identities;
- explicit resource kinds;
- deterministic payload paths;
- SHA-256 and byte-length validation;
- visual-asset sidecars;
- archive traversal/absolute-path rejection;
- undeclared/orphan payload rejection;
- canonical Engineering schema compatibility validation;
- inspection of untrusted library content before mutation.

Current export-enabled resource kinds are deliberately limited to:

- `equipment-template`;
- `dynamo`;
- `visual-asset`.

`screen`, `popup` and `script` are recognized manifest kinds but remain export/incorporation disabled until their full dependency contracts are implemented.

## 3. Association/catalog — implemented and green

Association is an Engineering-time, process-local catalog operation and remains semantically separate from importing project content.

Current behavior:

- `association != import`;
- associating a library validates and stores only the catalog/library content required for Engineering reuse;
- association does not mutate canonical Working and reports `workingChanged=false`;
- catalogs are partitioned by Engineering project/session scope;
- no source path, network share or Runtime reference becomes canonical project authority;
- same library ID + identical content is idempotent;
- same library ID + different content fails explicitly;
- disassociation removes the catalog relationship without deleting incorporated project content;
- catalog loss/restart cannot invalidate resources already incorporated into canonical Working.

Backend API is capability- and Engineering-Lock-protected and auditable.

## 4. Selective incorporation — implemented for current safe set

Selective incorporation is implemented for:

- `equipment-template`;
- `visual-asset`;
- canonical Dynamo composition v1.

The incorporation flow:

1. re-inspects the `.escadalib`;
2. resolves the selected resource dependency closure;
3. detects dependency cycles/missing members;
4. re-verifies payloads;
5. performs stable ID/key/content collision checks before mutation;
6. builds a canonical `EngineeringPackage` subset;
7. performs canonical Engineering Preview;
8. requires optimistic `x-elitescada-workspace-version` concurrency;
9. re-previews under the Workspace mutation lease;
10. applies through canonical `EngineeringExchangeService` CreateOnly behavior;
11. relies on canonical Working dirty/changeVersion callbacks;
12. audits success, denial, conflict and failure.

Identical existing content is deduplicated. Same identity/key with different canonical content fails rather than silently overwriting project content.

## 5. Dynamo v1 dependency authority — centralized and green

`ReusableDynamoDependencyAnalyzer` is the single reusable-Dynamo dependency/portability authority used by both export and incorporation validation.

Canonical v1 reusable dependency closure currently supports:

- Dynamo -> Equipment Template through `TemplateKey`;
- Dynamo -> Visual Asset through canonical `core.image` `assetRef`.

The analyzer fails closed for project-bound or unsupported composition including:

- concrete TAG / Client Memory bindings;
- concrete TAG default/parameter references;
- concrete equipment paths;
- project-bound dynamic sources;
- expression dependencies bound to project data;
- navigation/command actions;
- nested Dynamo composition.

Canonical Dynamo composition version 1 does not support nested Dynamos. C25.6 deliberately preserves `DYNAMO_NESTING_NOT_SUPPORTED`; reusable libraries do not weaken the product contract to manufacture nesting support.

Exported Dynamo dependency declarations must exactly match dependencies derived from canonical Dynamo content.

## 6. Self-contained project/package proof — implemented for current safe set

Tests prove that incorporated Template, VisualAsset and Dynamo v1 content becomes normal project-owned canonical content and survives `.escadapkg` export/import without an associated `.escadalib` or reusable-library catalog.

Therefore, for the currently incorporation-enabled resource set:

- final application content does not depend on `.escadalib` availability;
- Runtime/Active does not resolve reusable-library files;
- disassociation/catalog absence cannot break the incorporated resource;
- `.escadapkg` remains the self-contained application portability boundary.

## 7. Security/lifecycle authority — preserved

Reusable-library endpoints:

- require backend `EngineeringModify` authority;
- remain protected by Engineering Lock;
- are not added to canonical Import/Export/Recovery Lock exemptions;
- preserve optimistic Workspace concurrency for mutations;
- mutate only Working through canonical Engineering mechanisms;
- do not mutate Published/Active directly;
- use Audit for attributable operations and never Alarm or Operational Event.

Existing C25 Engineering Lock, Restore-first and Runtime session contracts remain green on the exact validated checkpoint.

## 8. Explicitly incomplete C25.6 work

C25.6 remains **IN PROGRESS**. The following are not yet accepted:

### 8.1 Durable informational provenance

Incorporated resources do not yet carry a standardized durable source-library provenance marker.

The next bounded product slice should add deterministic, namespaced informational origin metadata to project-owned resources without storing a source path, library bytes or Runtime dependency.

Provenance must remain stable across `.escadapkg` roundtrip and must not cause repeat incorporation of the same source resource to conflict with its own already-incorporated copy.

### 8.2 Screen/Popup reusable dependency authority

Screen/Popup export/incorporation remains disabled until a dedicated analyzer handles or rejects canonical references including:

- `DynamoKey`;
- visual `assetRef`;
- Popup `TemplateKey`;
- `NavigateScreen` / `OpenPopup` targets;
- `ExecuteCommand` command IDs;
- TAG/equipment/binding/dynamic references;
- any Script/HMI reference that participates in canonical behavior.

Application-owned dependencies intentionally excluded from reusable libraries must be classified explicitly as project-external or unsupported; they must never be silently copied or omitted.

### 8.3 Script reusable dependency authority

Script export/incorporation remains disabled.

`ScriptEngineeringDefinition` has canonical stable identity/path/source/scope/dependencies, but dependencies may target:

- Script;
- VisualDefinition;
- VisualObject;
- Tag;
- ClientMemoryTag;
- ServerMemoryTag;
- Resource.

A reusable Script analyzer must distinguish portable library dependencies from project-external/unsupported references and retain the existing Script validator, scope rules, dependency-cycle checks and Python preflight authority.

### 8.4 Engineering Libraries UI/browser contract

No final Libraries management UI/browser contract is accepted yet.

The UI still needs coherent operations for:

- association;
- catalog browse/search/preview;
- create/export library;
- selective `Usar` / `Importar`;
- dependency visibility;
- disassociation;
- provenance indication;
- locked-state hiding/direct-route denial;
- current Engineering locale authority without creating another independent locale state.

## 9. Next execution order

From this exact checkpoint:

1. add deterministic informational provenance for the already-supported Template/VisualAsset/Dynamo v1 incorporation set;
2. prove repeat incorporation/deduplication and `.escadapkg` roundtrip with provenance;
3. revalidate exact-SHA C25 + C03;
4. implement explicit dependency analyzers for the next resource family before enabling its export/incorporation;
5. add Engineering Libraries UI only after backend/resource behavior is stable enough that the UI does not become a second authority;
6. close C25.6 only after required backend, package, lifecycle, disassociation, UI/browser and exact-SHA regressions are green;
7. start C25.7 Help/manual only after C25.6 behavior is stable.

## 10. Permanent governance

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
