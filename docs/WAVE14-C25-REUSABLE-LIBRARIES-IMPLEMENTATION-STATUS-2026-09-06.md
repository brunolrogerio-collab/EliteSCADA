# Wave 14 C25.6 — Reusable Resource Libraries — Implementation Status

**Status:** ACTIVE / PARTIAL IMPLEMENTATION EXACT-SHA GREEN / NOT COMPLETE / NOT ACCEPTED / NOT INTEGRATED  
**Coordinator package:** C25  
**Checkpoint:** C25.6  
**Tracking issue:** #282  
**Implementation PR:** #283  
**Branch:** `wave14/c25-post-demo`  
**Integration target:** `wave14/corrections-integration`

> GitHub live state remains the sole project authority. This document is the current resumable C25.6 status. Older implementation details and intermediate checkpoints remain preserved in Git history and in #282/#283. Nothing here authorizes merge, C11 synchronization or any `main` mutation.

## 1. Latest exact validated product checkpoint

Validated product/test SHA:

`56854ddd509f0f969fefa68eff032017e5ce7906`

Exact-SHA CI:

- Wave 14 C25 Post-Demo #241 / run `34048685148` — **SUCCESS**;
- Wave 14 C03 DNP3 Adapter #263 / run `34048685143` — **SUCCESS**, including managed validation, Linux/Windows native host, real OpenDNP3↔dnp3py L3 interoperability and Windows commercial publish dependency gate.

This exact SHA closes the portable Script v1 slice and the prior provenance/self-contained ownership slice. It is not final C25.6 or C25 acceptance.

A previous intermediate candidate `357999c0a0cb747b5b7ed106dd64d445a2de6f71` produced C25 #238 red during Core test compilation because the new test attempted to assign the repository xUnit `Assert.NotNull(...)` void return to `var`. The log was inspected before correction. Commit `56854ddd...` corrected only that test assertion. No blind rerun and no product/test weakening occurred.

## 2. Implemented `.escadalib` package kernel

`.escadalib` remains distinct from `.escadapkg` and uses the versioned `elitescada.resource-library` container contract.

Implemented package properties include:

- stable library/resource identities;
- explicit language-neutral resource kinds;
- deterministic payload paths;
- SHA-256 and declared-length verification;
- visual-asset binary sidecars;
- archive path/traversal/absolute-path rejection;
- undeclared/orphan payload rejection;
- canonical Engineering schema compatibility checks;
- inspect/validate before any Working mutation.

Current **export-enabled** resource kinds are:

- `equipment-template`;
- `dynamo`;
- `script`;
- `visual-asset`.

`screen` and `popup` remain recognized manifest kinds but are deliberately not export/incorporation enabled yet.

## 3. Association/catalog behavior

Association remains Engineering-time catalog availability, not content import.

Current behavior:

- `association != import`;
- associating a library does not mutate canonical Working;
- catalog entries are process-local and scoped by current Engineering project/session;
- no source path/network share/repository becomes application or Runtime authority;
- identical same-ID library association is idempotent;
- same library ID with different content fails explicitly;
- disassociation cannot delete or invalidate already-incorporated canonical project resources;
- losing the catalog on process restart cannot break incorporated project-owned content.

Library endpoints remain backend capability-, Engineering-Lock- and Audit-controlled.

## 4. Selective incorporation and project ownership

Selective incorporation is implemented for:

- Equipment Template;
- Visual Asset;
- Dynamo composition v1;
- portable Script v1.

The common incorporation sequence remains:

1. re-inspect the `.escadalib`;
2. resolve selected transitive dependency closure;
3. reject missing/cyclic/unsupported dependencies;
4. re-verify payloads;
5. resolve stable identity/key/path/content collision policy before mutation;
6. build a canonical `EngineeringPackage` subset;
7. run canonical Engineering Preview;
8. require optimistic `x-elitescada-workspace-version` concurrency;
9. acquire one Workspace mutation lease for the logical mutation;
10. re-preview under that lease;
11. apply through `EngineeringExchangeService` CreateOnly behavior;
12. let canonical registries advance Working dirty/changeVersion;
13. audit success/denial/conflict/failure.

Identical already-owned content deduplicates. Same identity with different canonical content never silently overwrites unrelated project content.

## 5. Dynamo v1 dependency authority

`ReusableDynamoDependencyAnalyzer` remains the single dependency/portability authority used by both export and incorporation validation.

Reusable Dynamo v1 supports closure through:

- Dynamo -> Equipment Template via `TemplateKey`;
- Dynamo -> Visual Asset through canonical `core.image` `assetRef`.

It fails closed for concrete project-bound composition including TAG/Client Memory bindings, concrete TAG parameters/defaults, concrete equipment paths, project-bound dynamic sources, expression dependencies, navigation/commands and nested Dynamos.

Canonical Dynamo nesting remains unsupported; the reusable-library feature does not weaken that product contract.

## 6. Portable Script v1 dependency authority

`ReusableScriptDependencyAnalyzer` is the single reusable Script v1 portability authority.

Validated behavior:

- canonical `ScriptEngineeringDefinition` is the reusable Script payload;
- Script-to-Script dependencies are included transitively;
- reusable Script dependencies must remain same-scope;
- missing, self, cyclic and cross-scope Script dependencies fail before packaging/incorporation;
- project-owned dependency kinds (`VisualDefinition`, `VisualObject`, `Tag`, `ClientMemoryTag`, `ServerMemoryTag`, `Resource`) are rejected;
- `TagChanged` and `ClientMemoryChanged` entry points are rejected because their targets are concrete project state;
- any existing `ScriptVisualEventReference` in source Working is rejected rather than silently omitted;
- package inspection verifies declared Script dependency sets against canonical Script content;
- the canonical Script validator/Python preflight remains authoritative;
- incorporation routes through canonical `EngineeringExchangeService` Preview/Apply and Workspace Script registry;
- stable Script ID/path collision handling is explicit;
- identical incorporated Script content deduplicates.

Script/HMI associations remain project composition, not a hidden part of reusable Script v1.

## 7. Informational provenance and re-export semantics

Currently incorporation-enabled resources receive deterministic informational provenance under the reserved namespace:

`elitescada.reusable.origin.*`

Recorded values are limited to source library/resource identity/version/kind and source payload SHA-256. Provenance stores no source path, library bytes, catalog handle or Runtime dependency.

Validated behavior:

- provenance survives normal `.escadapkg` export/import;
- project restore works with zero `.escadalib` files/catalogs present;
- functional collision/deduplication comparison ignores only the reserved origin namespace;
- repeat incorporation therefore remains idempotent;
- when project-owned content is exported into a **new** `.escadalib`, old origin metadata is stripped while ordinary authored metadata remains;
- the new library becomes the immediate source rather than inheriting misleading origin genealogy.

This re-export reset is proven for Equipment Template, Dynamo, Script and Visual Asset payloads.

## 8. Runtime/self-contained boundary

For all currently incorporation-enabled resources, tests prove:

- incorporated content becomes ordinary project-owned canonical Engineering content;
- `.escadapkg` remains self-contained after reusable-library disassociation;
- Runtime/Active never opens `.escadalib`;
- Runtime/Active never resolves source paths/network shares/repositories;
- catalog disappearance cannot break incorporated content;
- library provenance is informational only and is not Runtime authority.

## 9. Security/lifecycle authority preserved

Reusable-library operations continue to preserve:

- backend `EngineeringModify` authority;
- fail-closed Engineering Lock protection;
- Audit attribution distinct from Alarm/Operational Event;
- optimistic Workspace concurrency;
- Working-only mutation;
- canonical dirty/changeVersion callbacks;
- Save/Publish/Activate boundaries;
- existing Engineering Lock, Restore-first and Runtime session regressions.

The exact Script checkpoint also proves Script closure incorporation under a Workspace mutation lease marks Working dirty and advances ChangeVersion, while identical re-planning remains non-mutating.

## 10. Explicitly incomplete C25.6 work

C25.6 remains **IN PROGRESS**.

### 10.1 Screen/Popup dependency authority

Screen/Popup export/incorporation remains disabled.

Live code audit confirms their canonical closure can include or reference:

- `DynamoKey`;
- visual `assetRef`;
- Popup `TemplateKey`;
- concrete TAG bindings;
- Dynamo TAG-reference parameters/defaults;
- dynamic TAG/expression dependencies;
- concrete `EquipmentPath`;
- `NavigateScreen` / `OpenPopup` targets;
- `ExecuteCommand` command IDs;
- separate Script/HMI event associations through `ScriptVisualEventReference`.

A dedicated Screen/Popup analyzer must classify every such edge as a reusable-library dependency, an explicitly supported project-external dependency, or unsupported. No reference may be silently copied, silently omitted or resolved by importing the entire library.

### 10.2 Engineering Libraries UI/browser contract

The final Engineering Libraries surface is not accepted yet.

Required product flow still includes coherent UI for:

- associate library;
- catalog browse/search/preview;
- create/export `.escadalib`;
- selective `Usar` / `Importar`;
- dependency visibility;
- provenance visibility;
- disassociate;
- Engineering-Lock hiding/direct-route denial;
- existing UI locale authority, without a second locale state.

UI must remain a projection/client of backend library authority rather than becoming another source of collision/dependency/security truth.

## 11. Next execution order

1. complete read-only Screen/Popup dependency audit against actual view/import/script contracts;
2. define one dedicated Screen/Popup portability analyzer before enabling either resource kind;
3. implement the smallest safe Screen/Popup slice with explicit closure/rejection tests and canonical Preview/Apply;
4. exact-SHA C25 + C03 revalidation;
5. implement Engineering Libraries UI/browser flow only after backend resource semantics are stable;
6. run final C25.6 package/security/lifecycle/UI/browser regression matrix;
7. close C25.6 only after exact-SHA evidence is durable;
8. start C25.7 contextual Help/manual only after C25.6 behavior is stable.

## 12. Permanent governance

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
