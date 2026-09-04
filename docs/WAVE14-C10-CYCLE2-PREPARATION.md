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

C18 was released from this exact product base. At the latest live revalidation during this preparation, `wave14/c18-hmi-alarm-event-browsers` still pointed exactly to `568e93...` and had no C18 product commit on top.

The preparation branch is intentionally isolated so C18 continues from a stable base while the Coordinator audits C01-C10 impacts.

## 2. Rules for this preparation lane

1. Do not advance, rebase or modify the C18 branch from this lane.
2. Do not merge this preparation branch into `wave14/corrections-integration` while C18 is active merely to record analysis.
3. Do not declare a new product freeze before C18 acceptance and integration.
4. Documentation-only preparation does not require duplicate CI on the already validated `568e93...` product.
5. If a real product defect is found, fix it here only when it is clearly independent of C18-owned HMI browser surfaces and after ownership is explicitly assigned.
6. Shared HMI visual-schema/palette/renderer/Property-Inspector defects are held for final post-C18 convergence unless they are critical blockers.
7. Any product-code correction requires exact-head validation under `docs/CI-VALIDATION-POLICY.md`.
8. Backend authority, authorization, licensing, identity and `Working -> Revision -> Published -> Active -> Runtime` remain binding.

## 3. Delta hotspots from `97eefd8...` to `568e93...`

### A. Source / TAG / Engineering identity

Representative changed files include:

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

Representative changed files include:

- `src/Scada.Api/Runtime/ServerScriptRuntimeManager.cs`;
- `src/Scada.Api/Runtime/IsolatedPythonScriptHandlerExecutor.cs`;
- `src/Scada.Engineering/Scripts/ScriptEngineeringRegistry.cs`;
- `src/Scada.Engineering/VisualScripting/ScriptRuntimeExecutionCoordinator.cs`;
- Operational Event model/registry/store/query files introduced by C14.

These changes principally affect C08 and cross-check script/runtime lifecycle assumptions from earlier Engineering contracts.

### C. Visual HMI / Runtime composition

Representative changed files include:

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

These changes principally affect C05/C07/C09. They are also the largest overlap with C18, which adds Alarm Browser and Event Browser through the same canonical visual-object pipeline.

## 4. C01-C10 impact matrix

| Package | Post-C10 impact | Current preparation disposition | Final post-C18 requirement |
|---|---|---|---|
| C01 Identity/bootstrap/password | **LOW direct** | No identity/auth/password product files appear in the `97eefd8... -> 568e93...` delta. Existing focused tests prove the password boundary and race-safe one-time bootstrap. No product churn. | Re-run universal security/E2E coverage and perform clean Preview first-run/bootstrap validation on the final exact product SHA. |
| C02 Driver catalog + Source configuration | **HIGH** | Source contracts, DTOs/types and Data Source editor changed. Current focused tests cover backend-driven catalog behavior, stable Source identity and incompatible-setting removal; C17 covers atomic new-Source transition. | Re-run universal CI + Wave11 on final SHA and verify catalog/forms, stable identity, type change cleanup and normal persistence/import/export. |
| C03 DNP3 commercial adapter | **LOW direct / MATERIAL RELEASE DEPENDENCY** | No DNP3 adapter product files changed in this delta. Existing DNP3 managed/native/interop coverage remains intact. No speculative adapter change. | Manually run `Wave 14 C03 DNP3 Adapter` on the final exact product SHA as explicit carry-forward commercial-adapter evidence, plus L3 and Interop. Do not rely only on path auto-routing. |
| C04 TAG Source/address/discovery assistants | **HIGH** | `TagAddressEditor`, address policy and Data Source lifecycle changed. Existing E2E coverage is broad and the C17 deterministic regression protects the exposed Source-creation race. | Re-run universal CI + Wave11 and recheck Source selector, unresolved Source, protocol-aware addressing and OPC UA discovery/browse/import. |
| C05 Canonical visual properties / Property Inspector | **VERY HIGH + DIRECT C18 OVERLAP** | Strong existing schema/Property Inspector/codec/Python-parity E2E coverage. Audit only while C18 is active; do not create a competing visual-schema line. | Mandatory post-C18 rerun. Old visual objects + Trend + Alarm Browser + Event Browser must coexist under one canonical schema/property/renderer lifecycle. |
| C06 Engineering Diagnostics / TAG Monitor boundary | **LOW/MEDIUM** | Focused browser test proves TAG Monitor remains Engineering-only, reads Active Runtime facts, exposes no write action and denies operator-only direct access. No corrective code indicated. | Re-run universal CI on final SHA and verify shell capability boundary after C18 Runtime composition. |
| C07 Screen Engineering + Dynamo maturity | **VERY HIGH + DIRECT C18 OVERLAP** | Strong visual/Dynamo authoring and Runtime test inventory exists. Audit only while C18 owns the same schema/palette/renderer/Runtime chain. | Mandatory post-C18 authored Screen/Popup/Dynamo regression including Trend + Alarm Browser + Event Browser coexistence. |
| C08 Python Script Assistant / project browser | **HIGH** | Existing tests cover official capability exposure, Script Preview/Apply CAS, backend authorization, sandbox boundaries and mediated TAG write. Server Script/Operational Event extensions do not justify a second scripting authority. | Re-run universal CI and verify generic discoverability/references for new HMI object types without object-specific scripting bypasses. |
| C09 Application shell + operator Runtime | **HIGH** | Existing tests cover capability-derived app surfaces, shell theme separation, fixed logical viewport at required resolutions, Startup/Home, Popup X/Y and canonical Runtime Commands. | Mandatory post-C18 rerun of capability-pruned shell, fixed canvas, Screen navigation, Popup stacking/position and embedded browser behavior in Active Runtime. |
| C10 Coordinator convergence | **GATE / NOT YET EXECUTED** | This branch is preparation only. No freeze and no integration authority. | Execute definitive Cycle 2 only after C18 is accepted/integrated: focused C01-C09 revalidation + universal/specialized gates + real Preview evidence -> new exact product freeze. |

## 5. Preparation findings

### 5.1 No immediate independent product correction is justified

The audit found no evidence requiring a new C01-C09 product correction before C18. In particular:

- C01 has no direct post-C10 product change requiring identity/password churn;
- C03 adapter/product bytes were not directly changed by C12-C17;
- C05/C07 changes share C18-owned visual surfaces and must not be patched speculatively in parallel;
- C02/C04/C06/C08/C09 have focused coverage matching the contracts affected by C12-C17.

A new code branch merely to make the preparation lane look industrious would increase integration risk without fixing a demonstrated defect.

### 5.2 C02/C04 require directed revalidation, not a new architecture

The current Data Source contract preserves the correct split:

- a new Source draft has no persisted stable id;
- editing an existing Source preserves stable identity;
- changing type removes incompatible settings/secret references and derives defaults from the backend-driven type schema;
- normal UI does not hardcode Driver identities;
- the accepted C17 correction establishes a fresh new-Source draft atomically before user interaction can reuse stale entity state;
- backend identity semantics remain authoritative.

Final Cycle 2 must exercise the broader catalog/configuration/discovery contract after C18 composition.

### 5.3 C08 remains mediated and fail-closed

The Script workspace continues to use canonical Preview/Apply with Workspace CAS and backend authorization. Client Visual Python advertises only official product capabilities; TAG write is mediated through an injected Runtime writer and direct shared TAG mutation, arbitrary network and industrial Driver authority remain denied boundaries.

Final C08 validation must confirm C18's new first-class HMI objects appear through generic project/object/reference contracts rather than acquiring browser-specific scripting APIs.

### 5.4 C09 changes are intentional contract extensions

The accepted product keeps Runtime separate from Engineering chrome, derives application surface access from effective backend capabilities, preserves a fixed logical visual canvas and implements canonical command dispatch plus persisted Popup positioning. Wave11 #275 passed on `568e93...`.

Final C09 validation must ensure C18 browser rendering remains inside this same Runtime composition rather than creating a new operator route/shell authority.

### 5.5 C05/C07 stay audit-only until C18 candidate

C18 necessarily consumes the same visual schema, palette, Property Inspector, canonical renderer, Popup and Runtime chain touched by C15/C16. Non-critical cleanup now would manufacture avoidable merge conflict and move the visual contract under an active DEV.

Therefore C05/C07 remain audit-only until the C18 candidate arrives.

## 6. Focused test coverage inventory at exact `568e93...`

The universal `EliteSCADA CI` performs:

- full `ScadaPlatform.sln` restore/build;
- `dotnet test ScadaPlatform.sln`, covering backend/Core/Drivers/Security suites;
- Runtime/API/persistence smoke;
- frontend Vite build;
- full Chromium `npm run test:e2e`.

The separate `Wave 11 Active HMI Runtime` workflow executes `playwright.wave11.config.ts` and therefore the complete Active Runtime lifecycle suite.

### C01 — STRONG focused coverage

Representative direct tests:

- `tests/Scada.Security.Tests/LocalIdentityTests.cs`
  - rejects 7-character password;
  - accepts 8-character password;
  - one-time first Administrator bootstrap;
  - concurrent bootstrap is race-safe;
  - duplicate normalized identities and mutation serialization are protected.
- `web/scada-web/tests-e2e/local-auth.spec.ts`
- `web/scada-web/tests-e2e/security.spec.ts`
- `web/scada-web/tests-e2e/engineering-mutation-security.spec.ts`
- user/administration E2E contracts.

Residual final action: clean installed/Preview first-run validation remains necessary because normal CI fixtures are not a substitute for the real first-run product experience.

### C02 — STRONG focused coverage

Representative direct tests:

- `EngineeringDataSourceTypeCatalogTests.cs`;
- `EngineeringDriverStableSourceIdentityTests.cs`;
- `DriverEngineeringContractsTests.cs`;
- `data-source-catalog-api-boundary.spec.ts`;
- `data-source-catalog-editor-contract.spec.ts`;
- `data-source-catalog-editor-mounted.spec.ts`;
- `driver-catalog-i18n-contract.spec.ts`;
- Wave11 `c17-datasource-new-transition.spec.ts` and `c17-memory-lifecycle.spec.ts`.

The frontend contract test explicitly proves stable identity for existing Source edits, removal of incompatible configuration, backend-driven Driver catalog use and no hardcoded normal-flow Driver ids.

Residual final action: rerun universal + Wave11 on post-C18 integrated SHA.

### C03 — STRONG focused + dedicated specialized coverage

Representative direct tests:

- `Dnp3CoordinatorConvergenceTests.cs`;
- `OpenDnp3HostProtocolTests.cs`;
- `OpenDnp3L3InteropTests.cs`;
- shared Driver convergence and L3 seven-Driver runtime/write/fault-recovery tests.

Dedicated workflow `.github/workflows/wave14-c03-dnp3.yml` additionally proves:

- managed OpenDNP3 tests;
- pinned native helper build on Linux;
- real OpenDNP3/dnp3py L3 interop;
- native Windows x64 build;
- native dependency inspection;
- exact third-party notice staging;
- Windows commercial publish dependency gate proving the restricted Step Function graph/bytes are absent.

Residual final action: invoke this workflow manually on the final Cycle 2 exact SHA, even if path filters do not auto-select it, then retain L3 + Interop combined evidence.

### C04 — STRONG focused coverage

Representative direct E2E coverage:

- `c04-tag-source-browser.spec.ts`;
- `tag-source-selector-contract.spec.ts`;
- `tag-address-assistant-contract.spec.ts`;
- `c04-manual-address-contract.spec.ts`;
- `c04-opcua-source-discovery-browser.spec.ts`;
- `c04-opcua-tooling-browser.spec.ts`;
- `c04-opcua-bulk-browser.spec.ts`;
- `opcua-tag-binding-contract.spec.ts`;
- `wave-14-c17-tag-address-policy.spec.ts`.

Backend/source identity coverage is shared with C02 and communication reality is additionally exercised by L3.

Residual final action: rerun after C18 composition because Source/TAG identity is cross-cutting product state.

### C05 — STRONG existing coverage, POST-C18 REEXECUTION REQUIRED

Representative direct E2E coverage:

- `visual-property-registry-contract.spec.ts`;
- `visual-property-inspector-editors.spec.ts`;
- `visual-property-inspector-schema-drift.spec.ts`;
- `visual-property-cross-stack-parity.spec.ts`;
- `visual-editor-property-inspector-contract.spec.ts`;
- canonical/legacy property codec tests;
- `visual-property-python-schema-parity.spec.ts`;
- visual Python provider/override/runtime acceptance tests.

Residual final action: C18 changes precisely the same visual-object/property/renderer chain, so the existing coverage is not final evidence until it reruns on the integrated C18 product.

### C06 — STRONG focused boundary coverage

Representative direct tests:

- `engineering-tag-monitor.spec.ts`;
- `runtime-tag-inspector-contract.spec.ts`;
- `communication-diagnostics.spec.ts`;
- backend `EngineeringRuntimeCommunicationDiagnosticsTests.cs`.

The mounted browser test proves TAG Monitor is an Engineering diagnostic, displays Active Runtime facts, exposes no TAG write control and cannot be obtained by operator-only direct URL/API authority.

Residual final action: rerun universal CI after C18 modifies Runtime composition/shell-adjacent HMI surfaces.

### C07 — STRONG existing coverage, POST-C18 REEXECUTION REQUIRED

Representative direct coverage includes:

- `visual-editor-*` authoring/canvas/layout/selection/z-order/workspace tests;
- `visual-dynamic-*` authoring/runtime tests;
- `BuiltinDynamoLibraryTests.cs`;
- `wave-14-dynamo-instance-authoring.spec.ts`;
- `wave-14-dynamo-public-interface.spec.ts`;
- `wave-14-dynamo-runtime-binding-projection.spec.ts`;
- `wave-14-dynamo-runtime-state-resolution.spec.ts`;
- Popup visual authoring/workspace tests;
- Runtime Dynamo/visual surface contracts.

Residual final action: mandatory integrated regression after C18 adds two more first-class authored Screen/Popup object types.

### C08 — STRONG focused coverage

Representative direct tests:

- `python-script-assistant-integration.spec.ts`;
- `script-assistant-model.spec.ts`;
- `script-assistant-reference-validation.spec.ts`;
- `script-engineering-workspace-contract.spec.ts`;
- `script-engineering-workspace-roundtrip.spec.ts`;
- `python-tag-write-capability.spec.ts`;
- Python sandbox/host/native-escape tests;
- visual Python capability/provider/runtime tests;
- backend `ServerScriptQualifiedQualityIntegrationTests.cs`;
- backend `ServerScriptRuntimeAutomationIntegrationTests.cs`.

Residual final action: verify C18 object discoverability through generic reference/object contracts, then rely on full universal E2E/backend execution on the final exact SHA.

### C09 — STRONG focused + Active Runtime coverage

Representative direct tests:

- `effective-capabilities-api.spec.ts`;
- `effective-capabilities-contract.spec.ts`;
- `app-shell.spec.ts`;
- `runtime-logical-canvas.spec.ts`;
- `runtime-operations-model.spec.ts`;
- `wave-09-popup-dynamo-runtime-web.spec.ts`;
- C16 Startup/Popup/Command E2E contracts;
- Wave11 `c16-startup-bootstrap.spec.ts`;
- Wave11 `c16-operational-runtime.spec.ts`;
- backend `EngineeringRuntimeCommandTests.cs` and persisted/published Runtime activation/recovery tests.

`runtime-logical-canvas.spec.ts` covers uniform 16:9 scaling at 1280x720, 1920x1080, 2560x1440 and 3840x2160 plus deterministic letterboxing and pointer-coordinate inversion. Wave11 C16 proves Screen/Dynamo/Popup Commands execute through the canonical backend command endpoint in the Active HMI Runtime and persisted Popup X/Y survives activation.

Residual final action: mandatory rerun after C18 because Alarm/Event Browser objects must embed in this exact Runtime/Popup coordinate and authority model.

## 7. Workflow ownership and Cycle 2 gate routing

Repository policy is binding:

`universal core CI + affected specialized CI + manual conservative override + explicit release/full-integration validation`.

For the final C10 Cycle 2 product candidate, the planned exact-head evidence set is:

1. **EliteSCADA CI** — universal backend/full solution + frontend build + full Chromium E2E;
2. **Wave 11 Active HMI Runtime** — Active Revision browser lifecycle and operational HMI composition;
3. **Preview Licensing CI** — manual/conservative release evidence, including Windows License Generator/product capacity contracts;
4. **L3 Seven-Driver Lab** — manual/conservative full integration communication evidence;
5. **Interop Lab Smoke** — common peer-stack evidence;
6. **Wave 14 C03 DNP3 Adapter** — manual exact-head carry-forward evidence for the commercial DNP3 adapter/distribution contract;
7. **real Preview/browser product validation** — clean first-run/auth plus final operator/Engineering HMI behavior that CI fixtures cannot fully substitute.

Path filters are routing aids, not architectural truth. A missing auto-trigger does not waive a specialized gate when the final release/freeze depends on that subsystem.

No workflow above is run merely because this documentation preparation commit exists. Product bytes remain `568e93...` until C18 supplies new product code.

## 8. Trigger for final C10 Convergence Cycle 2

The preparation lane becomes actionable final convergence only after all of the following:

1. C18 DEV delivers an exact candidate based on `568e93...`;
2. Coordinator reviews C18 diff/architecture/tests;
3. C18 exact candidate passes its required five gates plus package-specific authored Screen/Popup Alarm/Event Browser Save/Publish/Activate/Active Runtime coverage;
4. C18 is composed into `wave14/corrections-integration` preserving history;
5. the resulting integrated C12-C18 product passes all five combined gates.

Only then does the Coordinator execute definitive C10 Cycle 2 against that exact integrated product.

## 9. Definitive Cycle 2 validation procedure

At final convergence:

1. freeze the exact integrated C12-C18 product SHA under review without calling it accepted yet;
2. revalidate focused C01-C09 contracts against that one SHA;
3. pay special attention to C02/C04 Source identity/configuration/address/discovery flows;
4. prove C05/C07 visual schema/Property Inspector/Screen/Popup/Dynamo coexistence with Trend + Alarm Browser + Event Browser;
5. prove C08 generic script/object discoverability without browser-object-specific scripting bypasses;
6. prove C09 capability shell, fixed logical viewport, Startup/Home, Popup X/Y, command navigation and normal Runtime presentation;
7. run the exact-head workflow evidence set in Section 7, including manually dispatched C03 DNP3;
8. perform real Preview/browser product validation;
9. diagnose every red before any rerun and never weaken a contract to obtain green;
10. only after the evidence is coherent, declare one new exact product-code freeze.

That new freeze, not `97eefd8...`, not `568e93...`, and not a documentation HEAD, becomes the authority for affected C11 finding revalidation.

## 10. Non-goals

This preparation lane does not:

- implement C18;
- alter C18 base;
- release C11;
- build the EEE DEMO;
- merge PR #212 to `main`;
- resume Wave13 packaging/signing;
- create a new product freeze before C18 convergence.

## 11. Current decision

**Parallel C10 Cycle 2 preparation is ACTIVE and its C01-C09 coverage inventory is COMPLETE for the pre-C18 baseline.**

**C18 remains independently ACTIVE from exact base `568e93eb4dc4ba1fdc41455cfd6935e8831f09a4`.**

**No independent C01-C09 product correction is currently justified by this audit.**

**Final C10 Cycle 2 freeze remains BLOCKED on accepted/integrated C18.**

**C11 remains IMPLEMENTATION LOCKED.**