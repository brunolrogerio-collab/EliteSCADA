# Wave 15 — FND-06 Main Coordinator Control Plane

> GitHub live is the sole authority. Revalidate refs, exact SHAs, PRs and CI before any action.

`CONTROL_BRANCH: coord/w15-fnd06-control`

`MAIN_ORDER_REV: 0005`

`STATE: ACTIVE / FND06-CODEX-MOUNTED-LEGACY-CLOSE-V3`

`EXECUTOR_LANE: SAME SEQUENTIAL CODEX EXECUTOR USED IN PRIOR FOUNDATION WORK INCLUDING FND-04`

`PREVIOUS_CONTROL_ROUTE: coord/w15-fnd04-dev-aud-control:docs/WAVE15-FND04-DEV-AUD-CONTROL.md -> ROUTE-SEQUENTIAL-CODEX-TO-FND06-12`

`PRODUCT_BASE_CANDIDATE: 6c810647c9773a19b212d9c33694780141786ac7`

`PRODUCT_BASE_TREE: 1221ff55963052be4e924dd644efbaa65763f546`

`COORDINATION_BASE: f7da7694ec68222c40559fb77df2e2cb99d15557`

`FND04_POST_MERGE_CI: 35913456486 / SUCCESS`

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
4. assign the bounded FND-06 implementation to the **same sequential CODEX lane/chat that executed prior Foundation stages including FND-04**. This lane is active; FND-04's frozen state must not be interpreted as executor WAIT.

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

The exact accepted product base also persists `status` in `src/Scada.Api/Runtime/EngineeringWorkspace.cs` (the seeded pump Popup `fault` element), alongside `tank`, `value` and `dynamo`.

Main classification is now frozen:
- `tank`, `value`, `dynamo` and `status` are **known persisted legacy identifiers** that must be handled by the centralized compatibility boundary;
- `status` is **not** a current canonical built-in: the live `BUILTIN_VISUAL_OBJECT_TYPES` registry contains only the current `core.*` identifiers and has no bare `status`;
- do **not** guess that bare `status` means `instrument.status`, `core.valueDisplay`, or any other canonical built-in;
- until an explicit lossless migration is proven, preserve its authored id/key/bindings/properties and expose a bounded compatibility schema/model plus actionable diagnostic;
- arbitrary unknown identifiers remain outside this known-legacy set and fail closed/contained.

Modern strict consumers such as Property Inspector call the canonical built-in schema registry directly and return `not registered` for unsupported legacy type keys.

The canonical renderer currently contains unknown types in a `LegacyCompatibilityElement`, but that renderer fallback does not establish a centralized compatibility contract for strict editor/schema consumers.

Required rule:
- known persisted legacy identifiers must pass through one explicit compatibility/migration adapter **before strict schema consumers**;
- do not invent semantic aliases by guess;
- where a known legacy type cannot be losslessly mapped to a canonical built-in, expose an explicit compatibility schema/model with contained diagnostics rather than crashing;
- truly unknown identifiers remain fail-closed and must not be silently accepted;
- compatibility must be shared by Screen and Popup selection/property paths.


### 3.6 Wave 15 validation-profile correction

The active T1 router vocabulary is repository-authoritative. For FND-06 the correct declaration is:

`VALIDATION_PROFILE: UI_EDITOR, RUNTIME_RENDERER`

The former prepared names `VISUAL_ENGINEERING, RUNTIME_VISUAL` are not valid router profiles and must not be used in the PR body or manual validation evidence.

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
2. Initial mandatory known-legacy fixtures: `tank`, `value`, `dynamo`, `status`. Bare `status` is explicitly classified as a known persisted seed identifier, not a canonical built-in and not an inferred alias.
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
7. live-seed `status` is handled as known legacy compatibility without guessed built-in aliasing, preserving authored identity/bindings/properties and containing diagnostics;
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

## 7. CURRENT EXECUTOR ORDER

`ORDER_ID: FND06-CODEX-MOUNTED-LEGACY-CLOSE-V3`

`ORDER_STATE: ACTIVE`

`EXECUTOR_MODE: BOUNDED_TEST_EVIDENCE_AND_MINIMAL_FIX_IF_NEEDED`

`EXACT_PRODUCT_BASE_SHA: 6c810647c9773a19b212d9c33694780141786ac7`

`CURRENT_CANDIDATE_SHA: 923543705378016090e7067b35954795a9591a57`

`CURRENT_CANDIDATE_TREE: 5657cee7169a4e77370d416add4efcf07184d7c0`

`PR: #337`

`WORK_BRANCH: work/w15-fnd-06-visual-stability-foundation`

`TARGET_BRANCH: wave15/corrections-integration`

`VALIDATION_PROFILE: UI_EDITOR, RUNTIME_RENDERER`

`EXECUTOR_IDENTITY: SAME_SEQUENTIAL_CODEX_FROM_PRIOR_FOUNDATION/FND-04`

Main review disposition on `92354370...`:

- architecture/scope: acceptable;
- centralized known-legacy compatibility: acceptable;
- arbitrary unknown fail-closed: acceptable;
- Runtime Popup persistence + Active-identity reset evidence: acceptable;
- natural T1 `35931139983`: SUCCESS;
- **candidate is NOT merge-approved yet** because the original Wave 14 A7 acceptance requires mounted Screen + Popup selection regression, while the changed legacy selection evidence is currently model-level.

### Mandatory closeout evidence

Against exact current candidate `923543705378016090e7067b35954795a9591a57`, add mounted browser regression that proves the original A7 failure surface is actually closed.

Required matrix:

1. **Mounted Screen editor**
   - load a Screen containing persisted legacy `tank`, `value`, `dynamo`, and `status` fixtures (one test may cover multiple objects);
   - select each object through the real mounted editor path (canvas and/or outliner; cover both interaction routes across the matrix);
   - assert Engineering SPA remains mounted;
   - assert Property Inspector remains mounted and shows the bounded compatibility diagnostic;
   - assert Binding/Dynamic dependent panels do not throw/poison the page where they are part of the selected-state composition;
   - perform one safe shared-surface property edit on at least one known-legacy object and prove authored legacy-specific fields remain preserved.

2. **Mounted Popup editor**
   - same requirement for representative persisted legacy objects, including at minimum `value` and live-seed `status`;
   - select via the real mounted Popup editor path;
   - assert SPA/editor continuity and contained compatibility diagnostic.

3. **Unknown negative**
   - mounted or nearest truthful selected-state path for `vendor.unknown-x` remains contained/fail-closed;
   - it must not become accepted as a known legacy/built-in merely because compatibility exists.

4. **No regression of existing candidate evidence**
   - existing model-level compatibility tests remain green;
   - Popup retry/recovery remains green;
   - Active identity reset remains green;
   - Web build remains green.

### RED requirement

Do not manufacture RED if the current candidate already passes mounted A7 scenarios. Record:
- `GREEN_EXISTING_ON_92354370` if mounted tests pass without product change; or
- a discriminating mounted RED if a remaining selected-state component still fails, then make only the minimal allowlisted correction.

### Scope

Preferred change is tests only under `web/scada-web/tests-e2e/**`.

If a remaining mounted defect is exposed, production fix stays inside the existing FND-06 allowlist:
- `web/scada-web/src/visual-runtime/**`;
- `web/scada-web/src/engineering/visual-editor/**`;
- Runtime application/navigation only if directly causal.

No architecture expansion. No single-canvas DEV-EDITOR work. No Security/Authority/Licensing/Driver/Historian/lifecycle/schema/workflow changes.

### Return

Return exactly:

`FND-06 CODEX EXECUTOR -> MAIN COORDINATOR — MOUNTED LEGACY CLOSEOUT HANDOFF`

Include:
- `92354370...` -> final exact head/tree;
- whether production changed;
- exact changed files;
- mounted Screen matrix;
- mounted Popup matrix;
- unknown negative;
- preservation of legacy-specific authored data;
- local commands/results;
- fresh natural T1 if head changes;
- explicit non-actions.

No self-merge/freeze authority.

## 8. FC0-A effect — FND-06 is necessary but no longer sufficient

FND-06 completion plus FND-04 freeze clears the remaining implementation prerequisite, but **does not by itself close/release FC0-A**.

After FND-06 is integrated, exact post-merge validation is green and Main marks FND-06 VERIFIED/FROZEN, Main must activate and complete:

`FC0A-POST-FND06-W15-FOUNDATION-AUDIT-01`

Prepared audit control:

`coord/w15-fnd06-control:docs/WAVE15-FC0A-POST-FND06-AUDIT-CONTROL.md`

The audit must correlate:
- Wave 15 product premises and roadmap;
- `docs/WAVE15-CORRECTION-BACKLOG-FINAL.md`;
- final Wave 14 diagnostic/PO audit comments and handoffs, including #286 comments `5628159172`, `5628311338`, `5628760255`, `5634503355`;
- all frozen Foundation contracts FND-01/02/03/04/06/08 + INFRA-CI-01A;
- exact integrated product behavior/evidence after FND-06;
- remaining product gaps/residuals/deferred items;
- prepared FND-05 and FND-07 contracts and their impact on contracts consumed by FC0-A DEVs.

FC0-A release is allowed only if the audit concludes:
1. no unresolved P0/P1 Foundation blocker required before the four DEV lanes;
2. Wave 14 findings are mapped to CLOSED / DOWNSTREAM-DEV / DEFERRED-WITH-EVIDENCE / BLOCKED;
3. FND-05 and FND-07 can proceed **without breaking or redefining** frozen contracts consumed by the four FC0-A DEVs;
4. any additive future contract is isolated and does not invalidate the exact FC0-A base;
5. exact audit checkpoint SHA/tree and residual ledger are recorded.

If FND-05 or FND-07 would require a breaking Foundation-contract change, audit result is `BLOCKED-CONTRACT`; the Foundation delta must happen **before** FC0-A DEV release.

Only after audit `ACCEPTABLE / FC0A_RELEASE_APPROVED` may Main record the exact FC0-A integration checkpoint and activate:
- DEV-EDITOR;
- DEV-SCRIPT-ENGINEERING;
- DEV-AUTHORITY-UX;
- DEV-LICENSING-UX.

At that same approved checkpoint, FND-05 and FND-07 may also be activated in parallel with the four FC0-A DEV lanes, subject to their own isolated branches/orders. EliteGO, Installation UX and HA downstream remain blocked until their respective FND-05/FND-07 contracts freeze.
