# CHAT WORK ASSIGNMENTS — EliteSCADA

> Authoritative live execution board. GitHub branch/PR/head/CI state is operational truth. Permanent rules: `docs/DEVELOPMENT-WAVES.md`, `docs/PARALLEL-WORK.md`, `docs/CI-USAGE-POLICY.md`.

**Last coordinator synchronization:** 2026-08-28 — Wave 08 remains ACTIVE. Coordinator Visual Asset/revision/package and central Screen editor foundation are validated on exact product head `b48b489660ae953029fd2416aa18b149eaa18258` by CI #515 / run `33225051402` SUCCESS. Draft PR #90 remains open. Actions is authorized with conservative usage. DEV 1/2/3 remain explicitly STOPPED; each branch has only the coordinator-seeded shared intent contract on top of the frozen logical base.

## Mandatory `siga`

Every fixed EliteSCADA chat first rereads current `main`: `PROJECT GOAL.md`, `LAST CHANGE.md`, `docs/ROADMAP.md`, `docs/PARALLEL-WORK.md`, `docs/DEVELOPMENT-WAVES.md`, this board, `docs/CI-USAGE-POLICY.md`, `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`, `docs/COORDINATOR-HANDOFF.md`, and current MustReadSpecific documents. Then verify real branch/head/PR/CI and execute only the current authorized assignment.

## Current product gate

`GRAPHICAL-EDITOR-WAVE-08` is **ACTIVE**. Worker execution is currently stopped.

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
- main reconciliation merge: `011d3c649bb868bfcf8d13bfafe896bb0863a153`
- Draft integration PR: **#90**
- exact validated central-foundation product head: **`b48b489660ae953029fd2416aa18b149eaa18258`**
- CI #515 / run `33225051402`: **SUCCESS**
- Web build: SUCCESS
- backend build/full tests/Runtime smoke: SUCCESS
- Chromium E2E: SUCCESS
- `visual-editor-workspace.spec.ts`: SUCCESS
- later documentation-only successors use `[skip ci]`; they do not invalidate unchanged product evidence
- DEV 1 branch head: `57521312914e21e303976a81bc81c84ad5aa9cbb` — coordinator dependency seed only / STOPPED
- DEV 2 branch head: `3e942ed641d96afe848966f123fb10eaeaa99ed7` — coordinator dependency seed only / STOPPED
- DEV 3 branch head: `df3cd6c332a19bb3011373c95d010f33754c0c12` — coordinator dependency seed only / STOPPED
- CI mode: NORMAL; Actions authorized but conservative
- Wave 09/10: NOT ACTIVE

Wave 08 execution contract: `docs/GRAPHICAL-EDITOR-WAVE-08-IMPLEMENTATION-DECISION.md`.  
Detailed coordinator checkpoint: `docs/COORDINATOR-HANDOFF.md`.

### Wave 08 Actions discipline

- prefer static/focused evidence before a complete matrix where sufficient;
- batch coherent changes;
- no unchanged-head reassurance reruns;
- diagnose/fix localized failures before another expensive run;
- reserve complete matrices for meaningful integrated product checkpoints;
- never weaken the final Wave gate to save minutes.

---

# COORDENADOR - EliteSCADA

**Role:** `COORDINATOR`  
**Wave:** `GRAPHICAL-EDITOR-WAVE-08`  
**Status:** `ACTIVE — CENTRAL FOUNDATION VALIDATED / WORKER INTERACTION SLICES PENDING`  
**IntegrationBranch:** `integration/graphical-editor-wave-08`  
**DraftPR:** `#90`  
**ValidatedProductHead:** `b48b489660ae953029fd2416aa18b149eaa18258`  
**ValidatedCI:** `#515 / 33225051402 — SUCCESS`  
**LogicalWaveBaseSHA:** `8de706882ba20afedd666532ac41ae11115d06b3`  
**ContractSHA:** `7a445d3dd94cabd09807291a0ee94276559fcb0e`

**CurrentTask:** preserve the validated coordinator foundation; deliberately restart and integrate the three fixed worker slices when authorized/useful; complete the Wave 08 interaction/product gate; validate the final integrated head; merge only when fully green.

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

**CurrentStateBoundary:** Schema v13 Visual Assets/revision/package foundation plus central Screen editor composition have exact-head CI evidence. Canonical Engineering Preview/Apply/CAS remains mutation authority. `visualEditorContracts.ts` is the shared worker/coordinator intent seam. This does not imply Canvas/Inspector/Palette/Binding are implemented.

**CompletionCriteria:** exact integrated Wave 08 head proves create Screen -> add/move/resize/rotate -> edit properties -> canonical binding -> image asset by stable `assetRef` -> save/reopen -> export/import, with transient Canvas state absent from persisted authority; full final required CI green; merged main post-merge healthy; docs synchronized.

**NextActions:**
1. verify live PR #90/integration/worker heads before changes;
2. reuse CI #515 while central product code is unchanged;
3. restart DEV 1/2/3 only deliberately;
4. require `visualEditorContracts.ts` in all worker slices;
5. review delivery diffs/contracts before integration;
6. integrate into canonical Screen drafts without private persistence;
7. use focused validation while composing slices;
8. run the next full matrix only at a meaningful integrated product checkpoint;
9. merge PR #90 only after the complete Wave 08 gate is green and then verify post-merge main.

---

# DEV 1 - EliteSCADA

**Role:** `WORKER`  
**Wave:** `GRAPHICAL-EDITOR-WAVE-08`  
**Status:** `STOPPED — WAIT_FOR_COORDINATOR`  
**Branch:** `feature/graphical-editor-wave-08-canvas`  
**Head:** `57521312914e21e303976a81bc81c84ad5aa9cbb` — coordinator dependency seed only  
**Base:** `7a445d3dd94cabd09807291a0ee94276559fcb0e`

**CurrentTask when explicitly restarted:** Canvas / Selection.

**MustReadSpecific:** `docs/GRAPHICAL-EDITOR-WAVE-08-IMPLEMENTATION-DECISION.md`, its DEV 1 section, and seeded `web/scada-web/src/engineering/visual-editor/visualEditorContracts.ts`.

**AllowedScope:**
- `web/scada-web/src/engineering/visual-editor/canvas/**`
- `web/scada-web/tests-e2e/visual-editor-canvas*.spec.ts`

**Deliver:** viewport zoom/pan/grid/snap, selection/multiselect, move/resize/rotate interaction intents, duplicate/delete/z-order intents, deterministic UI-only selection/adornment state and focused tests, using the shared coordinator intent contracts.

**ForbiddenScope:** ReservedFiles; canonical schema/API/persistence; central workspace/route; Property Inspector; Object Palette/binding; asset backend/storage; public Visual Runtime/Python semantics; another worker branch; competing private intent types for already-defined shared operations.

**CompletionCriteria:** focused implementation/tests committed on own branch; supplied canonical definitions are not mutated directly; delivery head reported; then STOP.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`.

**Authorization state:** NOT AUTHORIZED TO EXECUTE until owner/coordinator explicitly restarts this worker. The current head contains no worker implementation.

---

# DEV 2 - EliteSCADA

**Role:** `WORKER`  
**Wave:** `GRAPHICAL-EDITOR-WAVE-08`  
**Status:** `STOPPED — WAIT_FOR_COORDINATOR`  
**Branch:** `feature/graphical-editor-wave-08-property-inspector`  
**Head:** `3e942ed641d96afe848966f123fb10eaeaa99ed7` — coordinator dependency seed only  
**Base:** `7a445d3dd94cabd09807291a0ee94276559fcb0e`

**CurrentTask when explicitly restarted:** Property Inspector.

**MustReadSpecific:** `docs/GRAPHICAL-EDITOR-WAVE-08-IMPLEMENTATION-DECISION.md`, its DEV 2 section, and seeded `web/scada-web/src/engineering/visual-editor/visualEditorContracts.ts`.

**AllowedScope:**
- `web/scada-web/src/engineering/visual-editor/property-inspector/**`
- `web/scada-web/tests-e2e/visual-editor-property-inspector*.spec.ts`

**Deliver:** inspector driven only by shared schema, typed property controls, explicit Default vs Engineering behavior, validation/error contract, deterministic supported multiselect behavior, change/remove intents and focused tests, using the shared coordinator intent contracts.

**ForbiddenScope:** duplicate property registries/defaults; ReservedFiles; canonical schema/API/persistence; central workspace/route; Canvas; Object Palette/binding; asset backend/storage; Runtime/Python writes; another worker branch; competing private property-mutation intent types for already-defined shared operations.

**CompletionCriteria:** focused implementation/tests committed on own branch; no competing property authority; delivery head reported; then STOP.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`.

**Authorization state:** NOT AUTHORIZED TO EXECUTE until owner/coordinator explicitly restarts this worker. The current head contains no worker implementation.

---

# DEV 3 - EliteSCADA

**Role:** `WORKER`  
**Wave:** `GRAPHICAL-EDITOR-WAVE-08`  
**Status:** `STOPPED — WAIT_FOR_COORDINATOR`  
**Branch:** `feature/graphical-editor-wave-08-palette-bindings`  
**Head:** `df3cd6c332a19bb3011373c95d010f33754c0c12` — coordinator dependency seed only  
**Base:** `7a445d3dd94cabd09807291a0ee94276559fcb0e`

**CurrentTask when explicitly restarted:** Object Palette / Binding Foundation.

**MustReadSpecific:** `docs/GRAPHICAL-EDITOR-WAVE-08-IMPLEMENTATION-DECISION.md`, its DEV 3 section, and seeded `web/scada-web/src/engineering/visual-editor/visualEditorContracts.ts`.

**AllowedScope:**
- `web/scada-web/src/engineering/visual-editor/object-palette/**`
- `web/scada-web/src/engineering/visual-editor/binding-editor/**`
- `web/scada-web/tests-e2e/visual-editor-palette*.spec.ts`
- `web/scada-web/tests-e2e/visual-editor-binding*.spec.ts`

**Deliver:** registered `core.*` palette, object-add intents, canonical binding authoring foundation, coordinator-provided source catalog boundary, validation of binding-capable destinations, Image palette entry using existing `assetRef`, focused tests, using the shared coordinator intent contracts.

**ForbiddenScope:** ReservedFiles; canonical schema/API/persistence; central workspace/route; Canvas; Property Inspector; asset backend/storage/import authority; Wave 07 property/Python contract changes; another worker branch; competing private add/binding intent types for already-defined shared operations.

**CompletionCriteria:** focused implementation/tests committed on own branch; no private persistence or direct driver source catalog; delivery head reported; then STOP.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`.

**Authorization state:** NOT AUTHORIZED TO EXECUTE until owner/coordinator explicitly restarts this worker. The current head contains no worker implementation.

## Coordinator note

Workers are intentionally stopped. The coordinator-seeded dependency commit is preparation only, not task execution. Preserved task text is not execution authorization. Workers never alter main, merge their own PR, broaden scope, or work around coordinator-owned dependencies; they stop after delivery.
