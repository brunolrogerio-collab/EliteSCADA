# Wave 14 — C10 Convergence Cycle 2 — Parallel Preparation

**Date:** 2026-09-04 BRT  
**State:** **PREPARATION ONLY / NON-AUTHORITATIVE / C18 ACTIVE IN PARALLEL**  
**Preparation branch:** `wave14/c10-convergence-cycle2-prep`  
**Exact preparation product base:** `568e93eb4dc4ba1fdc41455cfd6935e8831f09a4`  
**C18 exact authorized base:** `568e93eb4dc4ba1fdc41455cfd6935e8831f09a4`  
**Integration branch:** `wave14/corrections-integration`  
**Integration PR:** `#212` — OPEN / DRAFT / DO NOT MERGE TO `main`  
**C11:** IMPLEMENTATION LOCKED

GitHub is the official development memory. This document prepares the second C10 convergence cycle while C18 is implemented on an immutable accepted product base. It does **not** create a new Wave 14 freeze, does not authorize C11, does not alter the C18 base, and is not itself an integration candidate.

## 1. Why parallel preparation is safe

The prior complete C01-C10 converged product freeze was:

`97eefd8f4377ff583d1ba20bc89203f4a82b584d`

The accepted C12-C17 combined-green checkpoint is:

`568e93eb4dc4ba1fdc41455cfd6935e8831f09a4`

GitHub compare establishes:

- status: `ahead`;
- `ahead_by = 173`;
- `behind_by = 0`;
- merge base = exact old C10 freeze `97eefd8...`.

Therefore the current product is a direct descendant of the C01-C10 freeze. The convergence problem is not divergent history; it is contract revalidation after the C12-C17 product extensions.

`568e93...` already passed the five combined gates:

- EliteSCADA CI #1347 / `33882503111` — SUCCESS;
- Wave11 Active HMI Runtime #275 / `33882503088` — SUCCESS;
- Preview Licensing CI #297 / `33882503272` — SUCCESS;
- L3 Seven-Driver Lab #252 / `33882503050` — SUCCESS;
- Interop Lab Smoke #174 / `33882503053` — SUCCESS.

C18 was released from this exact product base. At the start of this preparation the package branch `wave14/c18-hmi-alarm-event-browsers` had been moved to exact `568e93...` and had no C18 product commit on top yet.

The preparation branch is intentionally isolated so C18 continues from a stable base while the Coordinator audits C01-C10 impacts.

## 2. Rules for this preparation lane

1. Do not advance, rebase or modify the C18 branch from this lane.
2. Do not merge this preparation branch into `wave14/corrections-integration` while C18 is active merely to record analysis.
3. Do not declare a new product freeze before C18 acceptance and integration.
4. Documentation-only preparation does not require duplicate CI on the already validated `568e93...` product.
5. If a real product defect is found, fix it here only when it is clearly independent of C18-owned HMI browser surfaces.
6. Shared HMI visual-schema/palette/renderer/Property-Inspector defects are held for final post-C18 convergence unless they are critical blockers.
7. Any product-code correction requires exact-head validation under `docs/CI-VALIDATION-POLICY.md`.
8. Backend authority, authorization, licensing, identity and `Working -> Revision -> Published -> Active -> Runtime` remain binding.

## 3. Delta hotspots from `97eefd8...` to `568e93...`

The post-C10 correction round changed three broad areas that intersect earlier C01-C10 contracts:

### A. Source / TAG / Engineering identity

Representative changed files:

- `src/Scada.Core/Sources/SourceProviderContracts.cs`;
- `src/Scada.Core/InternalMemory/ServerMemorySourceProvider.cs`;
- `src/Scada.Engineering/Contracts/EngineeringContracts.cs`;
- `src/Scada.Engineering/ImportExport/EngineeringExchangeService.cs`;
- `web/scada-web/src/engineering/DataSourceCatalogEditor.tsx`;
- `web/scada-web/src/engineering/DataSourceCatalogEditor.logic.ts`;
- `web/scada-web/src/engineering/TagAddressEditor.tsx`;
- `web/scada-web/src/engineering/tagAddressPolicy.ts`;
- `web/scada-web/src/engineering/types.ts`.

These changes principally affect C02/C04 and include the accepted C17 atomic new-Data-Source correction.

### B. Script / Runtime / operational model

Representative changed files:

- `src/Scada.Api/Runtime/ServerScriptRuntimeManager.cs`;
- `src/Scada.Api/Runtime/IsolatedPythonScriptHandlerExecutor.cs`;
- `src/Scada.Engineering/Scripts/ScriptEngineeringRegistry.cs`;
- `src/Scada.Engineering/VisualScripting/ScriptRuntimeExecutionCoordinator.cs`;
- Operational Event model/registry/store/query files introduced by C14.

These changes principally affect C08 and cross-check script/runtime lifecycle assumptions from earlier Engineering contracts.

### C. Visual HMI / Runtime composition

Representative changed files:

- `src/Scada.Engineering/VisualScripting/BuiltinVisualObjectSchemas.cs`;
- `src/Scada.Engineering/VisualScripting/VisualEngineeringPropertyCodec.cs`;
- `src/Scada.Engineering/Contracts/VisualCompositionEngineeringContracts.cs`;
- `web/scada-web/src/engineering/visual-editor/CanonicalVisualRenderer.tsx`;
- `web/scada-web/src/engineering/visual-editor/VisualEditorWorkspace.tsx`;
- `web/scada-web/src/engineering/visual-editor/object-palette/*`;
- `web/scada-web/src/engineering/visual-editor/property-inspector/PropertyInspector.tsx`;
- `web/scada-web/src/runtime/application/RuntimeApplicationMount.tsx`;
- `web/scada-web/src/runtime/visual-navigation/RuntimeVisualNavigator.tsx`;
- popup position, startup-screen, command API and Trend object/runtime files.

These changes principally affect C05/C07/C09. They are also the largest overlap with C18, which will add Alarm Browser and Event Browser through the same canonical visual-object pipeline.

## 4. C01-C10 impact matrix

| Package | Post-C10 impact | Current preparation disposition | Final post-C18 requirement |
|---|---|---|---|
| C01 Identity/bootstrap/password | **LOW direct** | No identity/auth/password product files appear in the `97eefd8... -> 568e93...` delta. Treat the accepted C01 contract as provisionally preserved; do not churn product code. | Re-run/confirm auth/security boundary coverage and clean Preview/bootstrap behavior as part of final convergence evidence. |
| C02 Driver catalog + Source configuration | **HIGH** | Revalidate now. Source contracts, Engineering DTOs/types and Data Source editor changed. C17 correction explicitly restored atomic new-Source identity semantics without weakening backend stable identity. | Re-check backend-authoritative catalog/forms, stable Source identity, type change cleanup and normal save/import/export after C18 composition. |
| C03 DNP3 commercial adapter | **LOW direct / MEDIUM indirect quality** | No DNP3 adapter product files changed in this delta. C13 did extend canonical quality/sample contracts; combined L3 and Interop are green. No speculative adapter change. | Preserve prior C03 commercial/licensing evidence; rerun DNP3-specific validation only if final composition touches adapter/dependency paths or if packaging manifest changes. L3/Interop remain mandatory combined gates. |
| C04 TAG Source/address/discovery assistants | **HIGH** | Revalidate now. `TagAddressEditor`, address policy and Data Source lifecycle changed. The C17 deterministic regression directly protects the previously exposed Source-creation corruption. | Recheck normal Source selector, unresolved Source, protocol-aware address policy and discovery/browse/import after C18 integration. |
| C05 Canonical visual properties / Property Inspector | **VERY HIGH + DIRECT C18 OVERLAP** | Static audit only while C18 is active. C15/C16 changed schemas, property codec, inspector, palette and renderer. Do not create a competing visual-schema line. | Full final convergence after C18: old visual objects + Trend + Alarm Browser + Event Browser must coexist under one schema/property/renderer lifecycle. |
| C06 Engineering Diagnostics / TAG Monitor boundary | **LOW/MEDIUM** | No TAG Monitor product file is directly changed by C12-C17, but Runtime application mounting/startup behavior changed. Treat as provisionally preserved; verify shell capability boundary rather than edit it. | Recheck Runtime-only identities do not regain Engineering/Diagnostics and TAG Monitor still observes Active Runtime under backend authorization. |
| C07 Screen Engineering + Dynamo maturity | **VERY HIGH + DIRECT C18 OVERLAP** | Static audit only. Visual editor, canonical renderer, palette, property inspector, Dynamo projection, popup positioning and runtime navigation changed. | Full authored Screen/Popup/Dynamo regression after C18. Confirm multiple first-class Trend/Alarm/Event objects do not regress selection/layout/z-order/public binding/runtime parity. |
| C08 Python Script Assistant / project browser | **HIGH** | Revalidate script registry/workspace lifecycle now. C12 added Server Runtime automation and C14 extended the workspace-owned registry with Operational Events. No separate registry or stale lifecycle was introduced in the inspected implementation. | Final check after C18 that newly supported HMI object types remain discoverable through generic project/object contracts without adding object-specific scripting bypasses. |
| C09 Application shell + operator Runtime | **HIGH** | Revalidate now using accepted C16 behavior. Runtime mount/navigation, Startup/Home, command dispatch and Popup X/Y deliberately extend C09 surfaces. Wave11 #275 provides combined runtime evidence. | Recheck fixed logical viewport, capability-pruned shell, Screen navigation, Popup stacking/position and alarm/browser composition after C18. |
| C10 Coordinator convergence | **GATE / NOT YET EXECUTED** | This branch is preparation only. No freeze and no integration authority. | Execute definitive Cycle 2 only after C18 is accepted/integrated: focused C01-C09 revalidation + five combined gates + required Preview evidence -> new exact product freeze. |

## 5. Preparation findings so far

### 5.1 No immediate C01 or C03 code correction justified

The compare contains no direct C01 identity/password/bootstrap file change and no C03 DNP3 adapter product change. Creating code churn here would add risk without evidence.

C03 still receives indirect coverage from the green L3/Interop combined gates, while final commercial-distribution claims continue to depend on the accepted C03 licensing/dependency evidence.

### 5.2 C02/C04 require directed revalidation, not a new architecture

The current Data Source logic preserves the correct split:

- `newDataSourceDraft()` creates a new entity without stable id;
- existing type switch preserves entity fields where editing an existing Source requires it;
- entering new mode now establishes a fresh draft before user interaction can operate on stale Source state;
- backend identity semantics remain authoritative.

The accepted C17 candidate and combined Wave11 lifecycle provide strong evidence that the C02/C04 Source identity contract survives C12-C17 convergence.

Final Cycle 2 must still exercise the broader C02/C04 catalog/configuration/discovery contract after C18 composition.

### 5.3 C08 workspace lifecycle remains one authority

`InMemoryScriptEngineeringRegistry` now also implements the Operational Event engineering registry, but checkout/save/project-switch clearing remains coordinated through the same workspace-owned object. This avoids creating an independent stale Event registry lifecycle.

Final C08 revalidation must concentrate on Script Assistant discoverability and generic object references, especially after C18 adds two more first-class HMI object types.

### 5.4 C09 changes are intentional contract extensions

`RuntimeVisualNavigator` now keeps the accepted fixed logical visual composition while adding canonical command dispatch and persisted Popup positioning. The post-C17 combined Wave11 #275 passed on `568e93...`.

Final C09 validation must ensure C18 browser rendering is contained inside this same Runtime composition rather than creating a new operator route/shell authority.

### 5.5 C05/C07 should not be patched in parallel with C18 without a concrete blocker

C18 necessarily consumes the same visual schema/palette/Property Inspector/renderer chain touched by C15/C16. Any non-critical C05/C07 cleanup now would manufacture avoidable merge conflict and make C18 rebase against moving visual contracts.

Therefore C05/C07 are audit-only until the C18 candidate arrives.

## 6. What can be completed while C18 develops

The Coordinator may continue, without altering C18:

1. catalogue focused C01-C09 tests and historical accepted evidence;
2. statically re-audit C02/C04 Source identity/configuration contracts;
3. statically re-audit C08 script/workspace lifecycle;
4. statically re-audit C09 shell/runtime contracts against accepted C16 extensions;
5. identify any truly C18-independent product defect and, only then, create a bounded correction on this prep branch;
6. prepare the final Cycle 2 validation checklist and exact workflow plan.

Do not run duplicate five-gate CI merely because this documentation branch exists. Product bytes remain exact `568e93...`, which already passed 5/5.

## 7. Trigger for final C10 Convergence Cycle 2

The preparation lane becomes actionable final convergence only after all of the following:

1. C18 DEV delivers an exact candidate based on `568e93...`;
2. Coordinator reviews C18 diff/architecture/tests;
3. C18 exact candidate passes its required five gates plus package-specific authored Screen/Popup browser lifecycle coverage;
4. C18 is composed into `wave14/corrections-integration` preserving history;
5. the resulting integrated C12-C18 product passes all five combined gates.

Then the Coordinator executes the definitive C10 Cycle 2 against that exact integrated product.

## 8. Definitive Cycle 2 validation plan

At final convergence:

- revalidate focused C01-C09 contracts against one exact C12-C18 product head;
- explicitly cover C02/C04 Source identity/configuration and address/discovery flows;
- cover C05/C07 visual schema/Property Inspector/Screen/Popup/Dynamo coexistence with Trend + Alarm Browser + Event Browser;
- cover C08 generic script/object discoverability without special browser-object APIs;
- cover C09 capability shell, fixed logical viewport, Startup/Home, Popup X/Y, command navigation and normal Runtime presentation;
- run universal EliteSCADA CI;
- run Wave11 Active HMI Runtime;
- run Preview Licensing CI;
- run L3 Seven-Driver Lab;
- run Interop Lab Smoke;
- perform real Preview/browser validation required by the C10 product acceptance boundary;
- diagnose any red before rerun;
- only after all evidence is coherent, declare one new exact product-code freeze.

That new freeze, not `97eefd8...`, not `568e93...`, and not a documentation HEAD, becomes the authority for affected C11 finding revalidation.

## 9. Non-goals

This preparation lane does not:

- implement C18;
- alter C18 base;
- release C11;
- build the EEE DEMO;
- merge PR #212 to `main`;
- resume Wave13 packaging/signing;
- create a new product freeze before C18 convergence.

## 10. Current decision

**Parallel C10 Cycle 2 preparation is ACTIVE.**

**C18 remains independently ACTIVE from exact base `568e93eb4dc4ba1fdc41455cfd6935e8831f09a4`.**

**Final C10 Cycle 2 freeze remains BLOCKED on accepted/integrated C18.**

**C11 remains IMPLEMENTATION LOCKED.**
