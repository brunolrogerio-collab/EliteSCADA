# Wave 14 C25.6 — Reusable Resource Libraries — Implementation Status

**Status:** COMPLETE / EXACT-SHA GREEN / NOT YET INTEGRATED / C25 NOT YET ACCEPTED  
**Coordinator package:** C25  
**Checkpoint:** C25.6  
**Tracking issue:** #282  
**Implementation PR:** #283  
**Branch:** `wave14/c25-post-demo`  
**Integration target:** `wave14/corrections-integration`

> GitHub live state remains the sole project authority. This document records the exact C25.6 closure candidate and does not authorize merge, C11 synchronization or any `main` mutation.

## 1. Exact closing authority

Validated product/test SHA:

`1f17367defa03f903e68f585d068b4f23f82bef9`

Exact-SHA CI:

- Wave 14 C25 Post-Demo #275 / run `34052101709` — **SUCCESS**;
- Wave 14 C03 DNP3 Adapter #280 / run `34052101702` — **SUCCESS**.

The C25 run includes backend/package/lifecycle validation, React/Vite build and the combined Chromium Engineering Lock + Restore-first + Runtime session + reusable-library browser contract.

The C03 run includes Managed validation, Linux and Windows native OpenDNP3 hosts, real OpenDNP3↔dnp3py L3 interoperability and the Windows commercial publish dependency gate.

No blind rerun or validation weakening was used for this closure.

## 2. Shipped `.escadalib` contract

`.escadalib` is a versioned reusable Engineering resource artifact distinct from `.escadapkg`.

Enabled reusable resource kinds are:

- `equipment-template`;
- `dynamo`;
- `visual-asset`;
- `script`;
- `screen`;
- `popup`.

The package kernel validates stable identities, deterministic payload locations, schema compatibility, declared length and SHA-256, archive traversal/path safety, visual-asset sidecars, undeclared/orphan payloads and supported dependency closure before Working mutation.

## 3. Association is not import

Association remains backend Engineering-time catalog state only:

- associating a library does not copy resources into canonical Working;
- association does not mark Working dirty or advance ChangeVersion;
- the catalog is process-local and scoped to the current Engineering project/session;
- no source path, network share, repository, catalog handle or library byte stream becomes project/Runtime authority;
- exact same-ID/content association is idempotent;
- same library ID with different content fails explicitly;
- disassociation cannot delete or invalidate project-owned resources already incorporated.

## 4. Selective incorporation and dependency closure

Using a resource follows the canonical flow:

1. inspect and verify the `.escadalib`;
2. resolve the selected transitive dependency closure;
3. reject missing, cyclic or unsupported edges;
4. resolve stable ID/key/path/content collisions before mutation;
5. build a canonical `EngineeringPackage` subset;
6. run canonical Engineering Preview;
7. require `x-elitescada-workspace-version` optimistic concurrency;
8. acquire one Workspace mutation lease;
9. re-preview under the lease;
10. apply with canonical `EngineeringExchangeService` CreateOnly behavior;
11. let canonical registries mark Working dirty/advance ChangeVersion;
12. audit success, denial, collision, concurrency and failure.

Identical project-owned content deduplicates. Unrelated differing content is never silently overwritten.

## 5. Resource-specific portability authorities

### Dynamo v1

`ReusableDynamoDependencyAnalyzer` supports Dynamo -> EquipmentTemplate and Dynamo -> VisualAsset closure. It rejects concrete TAG/ClientMemory state, concrete EquipmentPath, project-bound dynamics/expressions, navigation/commands and nested Dynamos.

### Script v1

`ReusableScriptDependencyAnalyzer` supports same-scope Script -> Script closure. It rejects missing/self/cyclic/cross-scope closure and project-owned dependency kinds, concrete TagChanged/ClientMemoryChanged coupling and existing Script/HMI visual-event association.

### Screen/Popup v1

`ReusableViewDependencyAnalyzer` is the single Screen/Popup portability authority.

Supported closure:

- Screen -> Dynamo / VisualAsset;
- Popup -> EquipmentTemplate / Dynamo / VisualAsset.

Parameterized authoring such as `{equipmentPath}` remains portable. Concrete TAG/ClientMemory identities, concrete EquipmentPath, concrete Dynamo TAG-reference parameters, project-bound dynamic/expression dependencies, navigation/command actions and Script/HMI coupling fail closed rather than being silently copied or omitted.

## 6. Provenance and self-contained ownership

Incorporated resources become ordinary project-owned canonical content and may retain informational metadata under:

`elitescada.reusable.origin.*`

Recorded origin includes source library/resource identity, library version, resource kind and source payload SHA-256. It contains no source path or live library dependency.

Validated behavior proves:

- provenance survives `.escadapkg` roundtrip;
- repeat incorporation remains idempotent;
- re-export into a new `.escadalib` strips previous origin genealogy while retaining ordinary authored metadata;
- disassociation preserves all incorporated content;
- final `.escadapkg` is self-contained with no `.escadalib` present;
- Runtime/Active never opens or resolves `.escadalib`.

## 7. Engineering Libraries UI/browser closure

The Engineering shell now exposes a coherent `/engineering/libraries` surface inside the existing `AuthGate -> effectiveCapabilities -> EngineeringLockGate -> EngineeringApp` authority chain.

The UI provides:

- `.escadalib` association;
- associated-library catalog selection;
- resource browse/search;
- resource kind, stable identity and dependency visibility;
- selective `Usar` using the current Workspace ChangeVersion;
- `.escadalib` creation/export from selected canonical Working resources;
- informational provenance visibility;
- safe library disassociation;
- pt-BR/en/es presentation using the current Engineering locale state;
- no localStorage/browser-side catalog authority.

Browser proof on exact SHA `1f17367d...` demonstrates:

1. association returns `workingChanged=false` and Working remains clean at the same ChangeVersion;
2. the selected Screen exposes its Dynamo dependency before use;
3. `Usar` sends the exact current `x-elitescada-workspace-version` and advances Working only after successful incorporation;
4. the reloaded project shows dirty state, new ChangeVersion and origin provenance;
5. the UI exports a new `.escadalib` from the selected canonical Screen identity;
6. disassociation removes the external catalog while project-owned provenance/content remains visible;
7. direct `/engineering/libraries` access while Engineering Lock is active never mounts the Libraries workspace and makes zero library API calls.

The architecture requires visual preview only where a safe canonical preview exists. C25.6 does not invent a shadow renderer for reusable resources; compatibility and closure remain backend canonical authority.

## 8. Security/lifecycle authority preserved

C25.6 preserves:

- backend `EngineeringModify` authority;
- fail-closed Engineering Lock;
- Audit distinct from Alarm and Operational Event;
- Workspace optimistic concurrency;
- Working-only mutation;
- Save/Publish/Activate authority;
- Script safety/preflight;
- package/archive safety;
- existing Engineering Lock, Restore-first and Runtime session regressions.

## 9. C25.6 closure decision

All minimum closure items in the binding C25.6 architecture are now represented by product behavior and exact-SHA tests. No unresolved C25.6 audit gap remains.

**C25.6 is COMPLETE.**

This is checkpoint closure only. It does not mean C25 is accepted or integrated.

## 10. Next checkpoint — C25.7

C25.7 contextual multilingual Help/manual is next.

Binding requirements remain:

- stable language-neutral Help IDs;
- pt-BR/en/es content for shipped UI languages;
- Help follows the active application UI locale authority;
- local/offline availability where practical;
- Driver and Script documentation derived from actual shipped registries/APIs;
- product-facing content contains no internal Wave/handoff prose;
- reusable-library creation/export, association, browse/use, dependency closure, provenance and safe disassociation are documented from the now-stable product behavior.

A live C25.7 locale audit corrected an earlier assumption: the product already has one canonical persisted locale owner. `engineering/i18n.ts` owns `elitescada.engineering.locale`; `appShellI18n.ts` explicitly delegates to that same owner and subscribes to the same key/document language. C25.6 Libraries introduced no additional locale state. C25.7 Help must reuse this existing authority and must not create another persisted language state.

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
