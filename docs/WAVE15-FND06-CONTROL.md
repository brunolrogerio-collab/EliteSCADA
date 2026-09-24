# Wave 15 — FND-06 Main Coordinator Control Plane

> GitHub live is the sole authority. Revalidate refs, exact SHAs, PRs and CI before any action.

`CONTROL_BRANCH: coord/w15-fnd06-control`

`MAIN_ORDER_REV: 0010`

`STATE: INTEGRATED / PRODUCT_ACCEPTED / FREEZE_BLOCKED_FND06_E2E_FIXTURE_ISOLATION`

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

`ORDER_ID: FND06-CODEX-E2E-FIXTURE-ISOLATION-V6`

`ORDER_STATE: ACTIVE`

`EXECUTOR_MODE: TEST_ONLY_CLOSEOUT`

`EXACT_BASE_SHA: eb4563cf0060449b479c4335ef30a19ed65e35ab`

`EXACT_BASE_TREE: 0158aa1b6082a8f9e514f6e6a059f49312b07f65`

`WORK_BRANCH: work/w15-fnd06-e2e-fixture-isolation`

`TARGET_BRANCH: wave15/corrections-integration`

`FAILED_BROAD_CI_RUN: 35944510920 / EliteSCADA CI #1564`

`FAILED_JOB: Chromium end-to-end 107459723706`

`VALIDATION_PROFILE: UI_EDITOR, RUNTIME_RENDERER`

`EXECUTOR_IDENTITY: SAME_SEQUENTIAL_CODEX_FROM_PRIOR_FOUNDATION/FND-04/FND-06/INFRA-CI-01B`

### Main diagnosis

Broad run #1564 proves:
- Web build — SUCCESS;
- Backend build/test/smoke — SUCCESS;
- Chromium — FAILURE;
- 636 passed / 1 failed;
- only failed spec: `tests-e2e/runtime.spec.ts`;
- exact failure: expected one canonical Screen but observed the temporary `fnd06-legacy-screen-*` fixture left by `fnd06-mounted-legacy-selection.spec.ts`.

This is **not** an INFRA-CI-01B failure:
- PostgreSQL backend/test gate is green;
- shared-schema correction is not causal to Chromium state pollution.

This is a **test-isolation defect introduced by the FND-06 mounted closeout spec**.

Exact source proof:
- `playwright.config.ts` uses `workers: 1`, so this is not cross-worker concurrency;
- the FND-06 mounted spec calls `/api/engineering/import/json/apply`;
- `ViewEngineeringHandler.Apply` is upsert-only for Screens/Popups and does not delete entities absent from a later package;
- therefore the current `finally { applyPackage(original) }` cannot remove a newly-created temporary Screen/Popup;
- `runtime.spec.ts` correctly detects the leaked extra Screen and must **not** be weakened.

### Required correction

Change **only**:

`web/scada-web/tests-e2e/fnd06-mounted-legacy-selection.spec.ts`

Preferred strategy:

1. Do **not** create a new Screen or Popup entity for mounted compatibility testing.
2. Export the canonical package.
3. Reuse an existing canonical Screen (normally `demo.overview`) by cloning that exact Screen and temporarily appending the FND-06 legacy elements to its `elements`.
4. Apply that modified existing Screen by the normal import/apply path.
5. Exercise the same mounted Screen selection matrix:
   - `tank | value | dynamo | status`;
   - canvas/outliner coverage;
   - Inspector/Dynamic/Binding continuity;
   - compatibility diagnostics;
   - unknown `vendor.unknown-x` remains contained;
   - one safe shared property edit preserves legacy-specific authored data.
6. In `finally`, reapply the exact original package. Because the same existing Screen identity/key is being updated rather than a new Screen created, the original upsert restores it truthfully.
7. For Popup, do the same using an existing canonical Popup (normally `popup.pump.standard`) rather than creating a new Popup entity.
8. After each cleanup, re-export and assert:
   - no `fnd06-*` fixture Screen/Popup remains;
   - canonical Screen/Popup counts/keys match the original exported package;
   - original canonical entity content is restored.
9. Do not weaken `runtime.spec.ts`.
10. Do not change product code, API semantics, import semantics, Playwright workers, CI ordering or retries.

If reusing the canonical Screen/Popup cannot exercise the mounted path without violating a product invariant, stop with a precise blocker instead of adding a delete bypass.

### Mandatory RED/GREEN evidence

RED is already the exact broad run:
`35944510920` — Chromium 636 pass / 1 fail because `fnd06-legacy-screen-*` leaked into `runtime.spec.ts`.

Required local/focused GREEN:
- run `fnd06-mounted-legacy-selection.spec.ts` followed by `runtime.spec.ts` under the same normal Playwright server/database lifecycle;
- both must pass in the same command/process;
- run the pair more than once if environment permits to prove cleanup determinism.

Required candidate gate:
- natural Wave 15 T1 on exact candidate under `UI_EDITOR, RUNTIME_RENDERER`;
- no product files changed;
- PR scope is test-only.

Required post-merge gate:
- full broad EliteSCADA CI on exact integration SHA must be green:
  - Web;
  - Backend build/test/smoke;
  - Chromium.

Only that exact green broad run may:
1. close INFRA-CI-01B;
2. close this FND-06 test-isolation defect;
3. mark FND-06 VERIFIED/FROZEN;
4. activate the independent post-FND06 FC0-A audit.

### Return

Return exactly:

`FND-06 CODEX EXECUTOR -> MAIN COORDINATOR — E2E FIXTURE ISOLATION HANDOFF`

Include:
- exact base -> candidate SHA/tree;
- exact changed files;
- RED evidence from #1564;
- cleanup strategy;
- proof original Screen/Popup identities are restored;
- paired mounted+runtime command/results;
- natural T1 run/jobs;
- explicit non-actions;
- no self-merge/freeze.

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


### INFRA-CI-01B merged gate

- PR #338 merged at `eb4563cf0060449b479c4335ef30a19ed65e35ab`
- tree `0158aa1b6082a8f9e514f6e6a059f49312b07f65`
- broad post-merge CI `35944510920` / #1564 pending
- FND-06 freeze remains HOLD until that exact broad run is green.


### Broad CI #1564 fixture-isolation diagnosis

Exact integration SHA:
`eb4563cf0060449b479c4335ef30a19ed65e35ab`

Run:
`35944510920`

Results:
- Web `107459384660` — SUCCESS;
- Backend `107459384855` — SUCCESS;
- Chromium `107459723706` — FAILURE;
- 636 passed / 1 failed.

The only failed spec was `runtime.spec.ts`, which saw two Screens because the FND-06 mounted test created `fnd06-legacy-screen-*` and its upsert-based restore did not delete that entity.

Classification:
`FND06_TEST_FIXTURE_ISOLATION_DEFECT / PRODUCT_NON_CAUSAL / INFRA_CI_01B_NON_CAUSAL`

The Runtime assertion remains valid and must not be weakened.
