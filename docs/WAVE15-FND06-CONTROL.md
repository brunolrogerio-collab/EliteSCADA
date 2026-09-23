# Wave 15 — FND-06 Main Coordinator Control Plane

> GitHub live is the sole authority. Revalidate refs, exact SHAs, PRs and CI before any action.

`CONTROL_BRANCH: coord/w15-fnd06-control`

`MAIN_ORDER_REV: 0001`

`STATE: WAIT_FND04_POST_MERGE_GATE / SOURCE_AUDIT_COMPLETE / ACTIVATION_PREPARED`

`PRODUCT_BASE_CANDIDATE: 6c810647c9773a19b212d9c33694780141786ac7`

`PRODUCT_BASE_TREE: 1221ff55963052be4e924dd644efbaa65763f546`

`COORDINATION_BASE: f7da7694ec68222c40559fb77df2e2cb99d15557`

`FND04_POST_MERGE_CI: 35913456486 / PENDING_CHROMIUM_AT_CREATION`

## 1. Purpose

FND-06 freezes the shared visual foundation needed before DEV-EDITOR and later EliteGO work may safely consume the Runtime/editor visual model.

Sources:
- Issue #305 — FND-06;
- Issue #303 — W15-EDITOR;
- `docs/WAVE15-CORRECTION-BACKLOG-FINAL.md` — W15-P1-02/P1-03/P1-04;
- Wave 14 accepted diagnostic handoff #286 comment `5634503355`.

This is a Foundation slice, not the full Editor UX implementation.

## 2. Activation gate

Do not mutate product until Main has verified all jobs of exact post-merge CI run `35913456486` on exact FND-04 merge SHA `6c810647c9773a19b212d9c33694780141786ac7`.

When that gate is green, Main may:
1. declare FND-04 VERIFIED/FROZEN;
2. revalidate integration divergence above the exact product checkpoint;
3. change this control to ACTIVE on the exact accepted base;
4. assign the bounded FND-06 implementation to the same sequential CODEX lane.

No downstream DEV may infer activation from this prepared document alone.

## 3. Source audit — live findings

### 3.1 Canonical renderer/public boundary is already substantially present

Current live code already establishes:
- `CanonicalVisualRenderer` as the common canonical visual renderer;
- Runtime `RuntimeVisualDefinitionRenderer` delegates process artwork to `CanonicalVisualRenderer`;
- `RuntimeVisualNavigator` owns Screen/Popup navigation/composition;
- `visualEngineeringRuntimeAdapter` projects canonical Engineering data into renderer-independent runtime structures;
- Screen and Popup Runtime surfaces use the same visual surface-property resolver.

FND-06 must freeze/reuse these boundaries, not create a second renderer.

### 3.2 Runtime transient projection resilience is already partly implemented

`RuntimeApplicationMount` retains `lastSuccessfulProjection` for retryable transport/proxy failures and preserves the mounted navigator when Active identity is unchanged.

Existing mounted regressions already prove selected Screen persistence for transient transport/proxy failure and deliberate teardown for authority conflict/genuine package failure.

Remaining Foundation proof gap:
- explicit Popup stack persistence across a retryable projection failure/recovery under unchanged `projectKey + revision + activatedAtUtc`;
- explicit reinitialization of Screen/Popup navigation when any real Active authority identity component changes;
- truthful stale/retrying signal must not be confused with valid Active-state mutation.

### 3.3 Working design and Active Runtime are already separate authorities

Editor workspaces render an Engineering `snapshot.package` / local Working draft/session.

Runtime renders the protected `/api/runtime/application` Active projection.

FND-06 freezes:
`Working design renderer != Active Runtime authority`.

No authoring/preview operation may publish, activate, issue process writes or mutate Active merely because it renders with canonical visual components.

### 3.4 Current editor still has a split authoring + canonical preview composition

`VisualEditorWorkspaceLegacy.tsx` and `PopupVisualEditorWorkspaceImpl.tsx` render:
1. the editable `VisualEditorCanvas`; then
2. a second canonical preview below it.

Issue #303 requires downstream DEV-EDITOR to converge this toward one primary WYSIWYG surface.

FND-06 does **not** implement the whole single-canvas UX. It freezes the renderer/authority contract and ensures selection/legacy compatibility are safe enough for DEV-EDITOR to do that work without redefining Foundation.

### 3.5 Legacy compatibility remains the main concrete Foundation product gap

Historical live audit reproduced deterministic Screen/Popup selection failure for persisted legacy types:
- `tank`;
- `value`;
- `dynamo`.

The current seed still contains those types and also `status`.

Modern strict consumers such as Property Inspector call the canonical built-in schema registry directly and return `not registered` for unsupported legacy type keys.

The canonical renderer currently contains unknown types in a `LegacyCompatibilityElement`, but that renderer fallback does not establish a centralized compatibility contract for strict editor/schema consumers.

Required rule:
- known persisted legacy identifiers must pass through one explicit compatibility/migration adapter **before strict schema consumers**;
- do not invent semantic aliases by guess;
- where a known legacy type cannot be losslessly mapped to a canonical built-in, expose an explicit compatibility schema/model with contained diagnostics rather than crashing;
- truly unknown identifiers remain fail-closed and must not be silently accepted;
- compatibility must be shared by Screen and Popup selection/property paths.

## 4. Frozen FND-06 contract

### 4.1 Renderer authority

1. `CanonicalVisualRenderer` remains the canonical Web visual artwork renderer.
2. Runtime may compose navigation, logical viewport, Popup stage, overlays and protected actions around it.
3. Engineering may layer selection handles/grid/guides/diagnostics around it.
4. Neither DEV-EDITOR nor EliteGO may create a competing visual interpretation/renderer.

### 4.2 Stable visual identity

1. Canonical visual object IDs remain stable identity.
2. Selection/Outliner/canvas state is UI/session state and never project authority.
3. Selecting malformed or compatible-legacy objects must not unmount/poison the Engineering SPA.
4. One object failure is contained to that object/inspector diagnostic.

### 4.3 Legacy compatibility

1. Centralize known legacy type compatibility before strict registry consumers.
2. Initial mandatory historical fixtures: `tank`, `value`, `dynamo`; inspect `status` because it remains in the live seed and either classify it as known compatibility or prove why it is intentionally unsupported.
3. Preserve object IDs, keys, bindings, Dynamo identity/equipment path and authored properties during compatibility projection.
4. Arbitrary unknown type e.g. `vendor.unknown-x` remains rejected/contained.
5. Do not broaden the canonical built-in registry merely to make tests pass.

### 4.4 Runtime projection/navigation persistence

1. Retryable transport/proxy failure with unchanged Active identity retains last successful projection and mounted navigator.
2. Selected Screen and open Popup stack remain intact.
3. Recovery with the same authority resumes without reinitialization.
4. Real `projectKey`, `revision` or `activatedAtUtc` change deliberately creates a fresh Runtime navigation session.
5. Non-retryable invalid Active package/authority conflict fails visibly; stale projection is not hidden as healthy.

### 4.5 Working vs Active

1. Engineering design renders current Working/draft data only.
2. Runtime operator surface renders only Active projection.
3. Canonical renderer reuse does not transfer authority.
4. Design Preview is non-authoritative and must not issue industrial writes unless an explicit protected preview contract separately authorizes a bounded operation.
5. Lifecycle remains `Working -> Save/Revision -> Publish -> Activate -> Runtime Active`.

## 5. FND-06 acceptance matrix

A candidate is acceptable only if all are deterministic:

1. current canonical `core.*` Screen selection from canvas stays mounted;
2. current canonical `core.*` Screen selection from Outliner stays mounted;
3. same two checks for Popup;
4. historical `tank` Screen object selection is contained/compatible, not SPA-fatal;
5. historical `value` Screen/Popup selection is contained/compatible;
6. historical `dynamo` selection preserves Dynamo identity/parameters/equipment path;
7. live-seed `status` behavior is explicitly classified and tested;
8. truly unknown type is rejected with contained actionable diagnostic and Engineering remains mounted;
9. malformed property on one selected object cannot blank Engineering;
10. malformed binding/destination cannot blank Engineering;
11. canonical renderer remains the sole artwork renderer for Runtime;
12. Engineering and Runtime consume the same canonical visual public model/schema contracts;
13. Working draft change becomes visible in design without mutating Active Runtime;
14. unchanged Active projection transient failure preserves selected Screen;
15. unchanged Active projection transient failure preserves open Popup stack;
16. same-authority recovery preserves Screen/Popup state;
17. real project change resets Runtime navigation;
18. real revision/activation identity change resets Runtime navigation;
19. invalid/non-retryable Active projection fails visibly rather than preserving stale healthy state;
20. no Security/Authority/Licensing/Driver/Historian/lifecycle weakening;
21. no second visual renderer or independent visual schema registry;
22. focused .NET/Web/Playwright regression suite green;
23. natural Wave 15 PR validation green on exact candidate;
24. no merge/freeze by executor.

## 6. Expected implementation boundary

Likely product touch is bounded to the visual compatibility/selection/runtime-navigation surfaces already owned by FND-06, for example:
- shared visual compatibility adapter in `web/scada-web/src/visual-runtime/**` or nearest existing canonical visual-contract module;
- Property Inspector / editor integration only where needed to consume that adapter;
- `VisualEditorWorkspaceLegacy.tsx` / `PopupVisualEditorWorkspaceImpl.tsx` only for contained selection diagnostics if the shared adapter is insufficient;
- `RuntimeApplicationMount.tsx` / `RuntimeVisualNavigator.tsx` only if the missing Popup-state/authority-reset regression exposes a real gap;
- focused existing visual/runtime Playwright tests.

Forbidden without new Main order:
- Authority/Security;
- Licensing;
- Drivers;
- Historian semantics;
- Working/Published/Active lifecycle redesign;
- database schema/migrations;
- new renderer;
- broad DEV-EDITOR single-canvas UX implementation;
- EliteGO implementation.

## 7. Prepared executor order

`ORDER_ID: FND06-CODEX-VISUAL-STABILITY-V1`

`ORDER_STATE: WAIT_FND04_POST_MERGE_GATE`

When Main flips this order to ACTIVE, executor must:
1. revalidate exact assigned base and branch divergence;
2. prove the historical strict-consumer legacy failure with test-only RED before changing product;
3. implement the smallest centralized compatibility/selection correction;
4. add the missing Popup projection/navigation persistence and authority-change regressions;
5. prove Working-design vs Active authority separation;
6. preserve all current canonical editor/Runtime behavior;
7. open one isolated PR targeting `wave15/corrections-integration`;
8. return exact head/tree/files/tests/natural CI/non-actions.

Until ACTIVE: **no FND-06 product mutation**.

## 8. FC0-A effect

FND-06 completion plus FND-04 freeze clears the remaining visual/script Foundation blockers for FC0-A.

Main must then record an exact integration checkpoint and explicitly release only the eligible downstream lanes whose other dependencies are frozen:
- DEV-EDITOR;
- DEV-SCRIPT-ENGINEERING;
- DEV-AUTHORITY-UX;
- DEV-LICENSING-UX.

EliteGO, Installation UX and HA downstream remain subject to their additional FND-05/FND-07 dependencies.
