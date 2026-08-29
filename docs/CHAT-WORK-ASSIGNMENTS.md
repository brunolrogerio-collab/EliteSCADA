# CHAT WORK ASSIGNMENTS — EliteSCADA

> Authoritative live execution board. GitHub branch/PR/head/CI state is operational truth. Permanent rules: `docs/DEVELOPMENT-WAVES.md`, `docs/PARALLEL-WORK.md`, `docs/CI-USAGE-POLICY.md`.

**Last coordinator synchronization:** 2026-08-28 — Wave 08 remains ACTIVE. Coordinator Visual Asset/revision/package and central Screen editor foundation are validated on exact product head `b48b489660ae953029fd2416aa18b149eaa18258` by CI #515 / run `33225051402` SUCCESS. Draft PR #90 remains open. Actions is authorized with conservative usage. DEV 1/2/3 are now deliberately **ACTIVE / AUTHORIZED** on their fixed parallel slices; each worker branch contains only the coordinator-seeded shared intent contract on top of the frozen logical base before worker implementation begins.

## Mandatory `siga`

Every fixed EliteSCADA chat first rereads current `main`: `PROJECT GOAL.md`, `LAST CHANGE.md`, `docs/ROADMAP.md`, `docs/PARALLEL-WORK.md`, `docs/DEVELOPMENT-WAVES.md`, this board, `docs/CI-USAGE-POLICY.md`, `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`, `docs/COORDINATOR-HANDOFF.md`, and current MustReadSpecific documents. Then verify real branch/head/PR/CI and execute only the current authorized assignment.

For Wave 08 workers, the coordinator-seeded `web/scada-web/src/engineering/visual-editor/visualEditorContracts.ts` is read from the worker's own branch after the mandatory current-main coordination documents. It is intentionally not merged product state in `main` yet.

## Current product gate

`GRAPHICAL-EDITOR-WAVE-08` is **ACTIVE** and the three interaction worker slices are **ACTIVE / parallel-safe**.

Wave 07 closure:
- final integration product head: `6d869109af23b25d1ae95cd35610e1930a16791c`
- final CI #508 / `33217787482`: SUCCESS
- merge PR #89
- main merge: `8de706882ba20afedd666532ac41ae11115d06b3`
- post-merge CI #510 / `33218282760`: SUCCESS

Wave 08:
- Logical WaveBaseSHA: `8de706882ba20afedd666532ac41ae11115d06b3`
- ContractSHA / logical branch base: `7a445d3dd94cabd09807291a0ee94276559fcb0e`
- Integration branch: `integration/graphical-editor-wave-08`
- main reconciliation merge on integration: `011d3c649bb868bfcf8d13bfafe896bb0863a153`
- Draft integration PR: **#90**
- exact validated central-foundation product head: **`b48b489660ae953029fd2416aa18b149eaa18258`**
- CI #515 / run `33225051402`: **SUCCESS**
- Web build: SUCCESS
- backend build/full tests/Runtime smoke: SUCCESS
- Chromium E2E: SUCCESS
- `visual-editor-workspace.spec.ts`: SUCCESS
- later documentation-only successors use `[skip ci]`; they do not invalidate unchanged product evidence
- DEV 1 branch seed head: `57521312914e21e303976a81bc81c84ad5aa9cbb`
- DEV 2 branch seed head: `3e942ed641d96afe848966f123fb10eaeaa99ed7`
- DEV 3 branch seed head: `df3cd6c332a19bb3011373c95d010f33754c0c12`
- all three worker branches are exactly one coordinator dependency-seed commit ahead of ContractSHA before worker implementation
- CI mode: NORMAL; Actions authorized but conservative
- Wave 09/10: NOT ACTIVE

Wave 08 execution contract: `docs/GRAPHICAL-EDITOR-WAVE-08-IMPLEMENTATION-DECISION.md`.  
Detailed coordinator checkpoint: `docs/COORDINATOR-HANDOFF.md`.

### Wave 08 Actions discipline

- workers use focused tests within owned scope and batch coherent changes;
- Draft PRs may be opened when enough structure exists for contract/integration review;
- do not spend a full matrix on trivial intermediate worker commits;
- no unchanged-head reassurance reruns;
- diagnose/fix localized failures before another expensive run;
- coordinator reserves the next complete matrix for a meaningful integrated product checkpoint;
- never weaken the final Wave gate to save minutes.

---

# COORDENADOR - EliteSCADA

**Role:** `COORDINATOR`  
**Wave:** `GRAPHICAL-EDITOR-WAVE-08`  
**Status:** `ACTIVE — CENTRAL FOUNDATION VALIDATED / WORKERS ACTIVE`  
**IntegrationBranch:** `integration/graphical-editor-wave-08`  
**DraftPR:** `#90`  
**ValidatedProductHead:** `b48b489660ae953029fd2416aa18b149eaa18258`  
**ValidatedCI:** `#515 / 33225051402 — SUCCESS`  
**LogicalWaveBaseSHA:** `8de706882ba20afedd666532ac41ae11115d06b3`  
**ContractSHA:** `7a445d3dd94cabd09807291a0ee94276559fcb0e`

**CurrentTask:** preserve the validated coordinator foundation; review worker contracts/deliveries; implement coordinator-owned canonical intent application/composition seams that do not absorb worker UI scope; integrate the three fixed slices; complete the Wave 08 interaction/product gate; validate final integrated head; merge only when fully green.

**MustReadSpecific:**
- `docs/COORDINATOR-HANDOFF.md`
- `docs/GRAPHICAL-EDITOR-WAVE-08-IMPLEMENTATION-DECISION.md`
- `docs/VISUAL-ASSET-STORAGE-WAVE-08.md`
- `docs/VISUAL-CANONICAL-CONVERGENCE-07-TO-08.md`
- `docs/VISUAL-RUNTIME-WAVE-07-IMPLEMENTATION-DECISION.md`
- `docs/PYTHON-SCRIPTING-AND-VISUAL-RUNTIME.md`
- `docs/VISUAL-ASSETS-AND-IMAGES.md`
- canonical Engineering Screen/View contracts/import-export/Preview/Apply/persistence/package code
- `web/scada-web/src/visual-runtime/**`
- `web/scada-web/src/engineering/visualEngineeringRuntimeAdapter.ts`
- `web/scada-web/src/engineering/visual-editor/**`

**AllowedScope:** coordinator-owned canonical Engineering/API/persistence/assets, integration branch, central editor workspace/routing/composition/renderer/localization, shared mutation helpers/exports, cross-slice tests, worker integration, final PR/CI/merge.

**ForbiddenScope:** Wave 09 Screen navigation/Popup/Dynamo product semantics; Wave 10 Python event editor/production tween/visual Preview; new protocols; Server Python.

**CurrentStateBoundary:** Schema v13 Visual Assets/revision/package foundation plus central Screen editor composition have exact-head CI evidence. Canonical Engineering Preview/Apply/CAS remains mutation authority. `visualEditorContracts.ts` is the shared worker/coordinator intent seam. This does not imply Canvas/Inspector/Palette/Binding are implemented until their worker deliveries are reviewed and integrated.

**CompletionCriteria:** exact integrated Wave 08 head proves create Screen -> add/move/resize/rotate -> edit properties -> canonical binding -> image asset by stable `assetRef` -> save/reopen -> export/import, with transient Canvas state absent from persisted authority; full final required CI green; merged main post-merge healthy; docs synchronized.

**NextActions:**
1. monitor DEV 1/2/3 branches and Draft PRs without duplicating their scopes;
2. review contracts/diffs at Early Contract Review and Integration Review checkpoints;
3. reuse CI #515 while central validated product code is unchanged;
4. implement only coordinator-owned canonical mutation/composition hooks required to consume shared intents;
5. integrate delivered worker heads after Delivery Review;
6. use focused validation while composing slices;
7. run the next full matrix only at a meaningful integrated product checkpoint;
8. merge PR #90 only after the complete Wave 08 gate is green and then verify post-merge main.

---

# DEV 1 - EliteSCADA

**Role:** `WORKER`  
**Wave:** `GRAPHICAL-EDITOR-WAVE-08`  
**Status:** `ACTIVE — AUTHORIZED`  
**Branch:** `feature/graphical-editor-wave-08-canvas`  
**HeadAtAuthorization:** `57521312914e21e303976a81bc81c84ad5aa9cbb` — coordinator dependency seed only  
**BaseSHA:** `7a445d3dd94cabd09807291a0ee94276559fcb0e`  
**StartCondition:** `SATISFIED — central Wave 08 foundation CI #515 green and shared intent contract seeded`  
**ParallelSafeWith:** `DEV 2`, `DEV 3`

**CurrentTask:** Canvas / Selection.

**MustReadSpecific:** `docs/GRAPHICAL-EDITOR-WAVE-08-IMPLEMENTATION-DECISION.md`, its DEV 1 section, and seeded `web/scada-web/src/engineering/visual-editor/visualEditorContracts.ts` from this worker branch.

**AllowedScope:**
- `web/scada-web/src/engineering/visual-editor/canvas/**`
- `web/scada-web/tests-e2e/visual-editor-canvas*.spec.ts`

**Deliver:** renderer-independent Canvas interaction model/component; viewport zoom/pan/grid/snap; selection/multiselect; move/resize/rotate interaction intents; duplicate/delete/z-order intents; deterministic UI-only selection/adornment state; focused tests proving supplied canonical definitions are not mutated directly; use only the shared coordinator intent contracts.

**ForbiddenScope:** ReservedFiles; canonical schema/API/persistence; central workspace/route; Property Inspector; Object Palette/binding; asset backend/storage; public Visual Runtime/Python semantics; another worker branch; competing private intent types for already-defined shared operations.

**ValidationMatrix:** focused Canvas tests in owned files; Web build/typecheck at a coherent delivery checkpoint; no full integration matrix required on every intermediate worker commit.

**CompletionCriteria:** implementation/tests committed on own branch; focused required validation green on exact delivery head; Draft PR/body records IMPLEMENTED IN PR, INTEGRATION REQUIRED and SPECIFIED / NOT IMPLEMENTED; delivery head reported; then STOP.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`.

**Authorization state:** AUTHORIZED TO EXECUTE immediately on `siga`, only within this assignment.

---

# DEV 2 - EliteSCADA

**Role:** `WORKER`  
**Wave:** `GRAPHICAL-EDITOR-WAVE-08`  
**Status:** `ACTIVE — AUTHORIZED`  
**Branch:** `feature/graphical-editor-wave-08-property-inspector`  
**HeadAtAuthorization:** `3e942ed641d96afe848966f123fb10eaeaa99ed7` — coordinator dependency seed only  
**BaseSHA:** `7a445d3dd94cabd09807291a0ee94276559fcb0e`  
**StartCondition:** `SATISFIED — central Wave 08 foundation CI #515 green and shared intent contract seeded`  
**ParallelSafeWith:** `DEV 1`, `DEV 3`

**CurrentTask:** Property Inspector.

**MustReadSpecific:** `docs/GRAPHICAL-EDITOR-WAVE-08-IMPLEMENTATION-DECISION.md`, its DEV 2 section, and seeded `web/scada-web/src/engineering/visual-editor/visualEditorContracts.ts` from this worker branch.

**AllowedScope:**
- `web/scada-web/src/engineering/visual-editor/property-inspector/**`
- `web/scada-web/tests-e2e/visual-editor-property-inspector*.spec.ts`

**Deliver:** inspector driven only by shared object/property schema; typed controls for registered Wave 08 property family; explicit Default vs Engineering behavior; validation/error presentation; deterministic supported multiselect behavior; change/remove intents without persistence calls; use only the shared coordinator intent contracts.

**ForbiddenScope:** duplicate property registries/defaults; ReservedFiles; canonical schema/API/persistence; central workspace/route; Canvas; Object Palette/binding; asset backend/storage; Runtime/Python writes; another worker branch; competing private property-mutation intent types.

**ValidationMatrix:** focused Property Inspector tests in owned files; Web build/typecheck at a coherent delivery checkpoint; no full integration matrix required on every intermediate worker commit.

**CompletionCriteria:** implementation/tests committed on own branch; focused required validation green on exact delivery head; no competing property authority; Draft PR/body records IMPLEMENTED IN PR, INTEGRATION REQUIRED and SPECIFIED / NOT IMPLEMENTED; delivery head reported; then STOP.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`.

**Authorization state:** AUTHORIZED TO EXECUTE immediately on `siga`, only within this assignment.

---

# DEV 3 - EliteSCADA

**Role:** `WORKER`  
**Wave:** `GRAPHICAL-EDITOR-WAVE-08`  
**Status:** `ACTIVE — AUTHORIZED`  
**Branch:** `feature/graphical-editor-wave-08-palette-bindings`  
**HeadAtAuthorization:** `df3cd6c332a19bb3011373c95d010f33754c0c12` — coordinator dependency seed only  
**BaseSHA:** `7a445d3dd94cabd09807291a0ee94276559fcb0e`  
**StartCondition:** `SATISFIED — central Wave 08 foundation CI #515 green and shared intent contract seeded`  
**ParallelSafeWith:** `DEV 1`, `DEV 2`

**CurrentTask:** Object Palette / Binding Foundation.

**MustReadSpecific:** `docs/GRAPHICAL-EDITOR-WAVE-08-IMPLEMENTATION-DECISION.md`, its DEV 3 section, and seeded `web/scada-web/src/engineering/visual-editor/visualEditorContracts.ts` from this worker branch.

**AllowedScope:**
- `web/scada-web/src/engineering/visual-editor/object-palette/**`
- `web/scada-web/src/engineering/visual-editor/binding-editor/**`
- `web/scada-web/tests-e2e/visual-editor-palette*.spec.ts`
- `web/scada-web/tests-e2e/visual-editor-binding*.spec.ts`

**Deliver:** registered `core.*` palette; object-add intents without private persistence; binding editor/model using canonical destination/source semantics; rejection of unsupported/unregistered destinations; coordinator-provided source catalog boundary; Image palette entry using existing `assetRef`; focused tests; use only the shared coordinator intent contracts.

**ForbiddenScope:** ReservedFiles; canonical schema/API/persistence; central workspace/route; Canvas; Property Inspector; asset backend/storage/import authority; Wave 07 property/Python contract changes; another worker branch; competing private add/binding intent types.

**ValidationMatrix:** focused Palette/Binding tests in owned files; Web build/typecheck at a coherent delivery checkpoint; no full integration matrix required on every intermediate worker commit.

**CompletionCriteria:** implementation/tests committed on own branch; focused required validation green on exact delivery head; no private persistence or direct driver source catalog; Draft PR/body records IMPLEMENTED IN PR, INTEGRATION REQUIRED and SPECIFIED / NOT IMPLEMENTED; delivery head reported; then STOP.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`.

**Authorization state:** AUTHORIZED TO EXECUTE immediately on `siga`, only within this assignment.

## Coordinator note

The three workers were deliberately reactivated only after the coordinator foundation passed CI #515 and each branch received the shared intent-contract seed. A coordinator-seeded dependency commit is not worker implementation. Workers never alter `main`, merge their own PR, broaden scope, work on sibling branches or edit around coordinator-owned dependencies; each stops after delivery.