# CHAT WORK ASSIGNMENTS — EliteSCADA

> Authoritative live execution board. GitHub branch/PR/head/CI state is operational truth. Permanent rules: `docs/DEVELOPMENT-WAVES.md`, `docs/PARALLEL-WORK.md`, `docs/CI-USAGE-POLICY.md`.

**Last coordinator synchronization:** 2026-08-28 — Wave 08 remains ACTIVE; coordinator-only Visual Asset/revision/package foundation exists on integration and is NOT CI validated; owner explicitly stopped DEV 1/2/3; all worker branches remain untouched at the Wave 08 contract base.

## Mandatory `siga`

Every fixed EliteSCADA chat first rereads current `main`: `PROJECT GOAL.md`, `LAST CHANGE.md`, `docs/ROADMAP.md`, `docs/PARALLEL-WORK.md`, `docs/DEVELOPMENT-WAVES.md`, this board, `docs/CI-USAGE-POLICY.md`, `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`, `docs/COORDINATOR-HANDOFF.md`, and current MustReadSpecific documents. Then verify real branch/head/CI and execute only the current authorized assignment.

## Current product gate

`GRAPHICAL-EDITOR-WAVE-08` is **ACTIVE**, but worker execution is currently stopped.

Wave 07 closure:
- final integration product head: `6d869109af23b25d1ae95cd35610e1930a16791c`
- final CI #508 / `33217787482`: SUCCESS
- merge PR #89
- main merge: `8de706882ba20afedd666532ac41ae11115d06b3`
- post-merge CI #510 / `33218282760`: SUCCESS

Wave 08:
- Logical WaveBaseSHA: `8de706882ba20afedd666532ac41ae11115d06b3`
- ContractSHA / branch base: `7a445d3dd94cabd09807291a0ee94276559fcb0e`
- Integration branch: `integration/graphical-editor-wave-08`
- exact coordinator integration head at this synchronization: `4f8a71f59c5d033265c38c1c0beca2add9bb117b`
- DEV 1 branch: `feature/graphical-editor-wave-08-canvas` @ `7a445d3dd94cabd09807291a0ee94276559fcb0e`
- DEV 2 branch: `feature/graphical-editor-wave-08-property-inspector` @ `7a445d3dd94cabd09807291a0ee94276559fcb0e`
- DEV 3 branch: `feature/graphical-editor-wave-08-palette-bindings` @ `7a445d3dd94cabd09807291a0ee94276559fcb0e`
- integration Actions runs: none yet
- CI mode: NORMAL
- Wave 09/10: NOT ACTIVE

Wave 08 execution contract: `docs/GRAPHICAL-EDITOR-WAVE-08-IMPLEMENTATION-DECISION.md`.
Current coordinator implementation checkpoint: `docs/COORDINATOR-HANDOFF.md`.

---

# COORDENADOR - EliteSCADA

**Role:** `COORDINATOR`  
**Wave:** `GRAPHICAL-EDITOR-WAVE-08`  
**Status:** `ACTIVE — PARTIAL CENTRAL FOUNDATION / NOT CI VALIDATED`  
**IntegrationBranch:** `integration/graphical-editor-wave-08`  
**CurrentIntegrationHead:** `4f8a71f59c5d033265c38c1c0beca2add9bb117b`  
**LogicalWaveBaseSHA:** `8de706882ba20afedd666532ac41ae11115d06b3`  
**ContractSHA:** `7a445d3dd94cabd09807291a0ee94276559fcb0e`

**CurrentTask:** resume coordinator-only Wave 08 integration from the persistent handoff, preserve the completed Visual Asset/revision/package foundation, finish canonical Screen graphical mutation/save/reopen plus central editor composition, then deliberately restart/integrate worker slices only when authorized.

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

**AllowedScope:** coordinator-owned canonical Engineering/API/persistence/assets, integration branch, central editor workspace/routing/composition/renderer/localization, shared exports, cross-slice tests, worker integration, final PR/CI/merge.

**ForbiddenScope:** do not absorb Wave 09 Screen navigation/Popup/Dynamo product semantics; do not implement Wave 10 Python event editor/production tween/visual Preview; no new protocols; no Server Python.

**ReservedFiles:** all files listed under `ReservedFiles` in the Wave 08 implementation decision remain coordinator-only unless explicitly reassigned.

**CurrentStateBoundary:** Visual Asset Schema v13, Working asset authority, raster validation, PostgreSQL revision blobs/links, `.escadapkg` v2, protected asset API/frontend seam and focused source tests are implemented on integration but have no exact-head CI evidence yet. Do not call this validated or merge-ready.

**CompletionCriteria:** exact integrated Wave 08 head proves create Screen -> add/move/resize/rotate -> edit properties -> canonical binding -> image asset by stable `assetRef` -> save/reopen -> export/import, with transient Canvas state absent from persisted authority; full required CI green; merged main post-merge healthy; docs synchronized.

---

# DEV 1 - EliteSCADA

**Role:** `WORKER`  
**Wave:** `GRAPHICAL-EDITOR-WAVE-08`  
**Status:** `STOPPED — WAIT_FOR_COORDINATOR`  
**Branch:** `feature/graphical-editor-wave-08-canvas`  
**Head:** `7a445d3dd94cabd09807291a0ee94276559fcb0e`  
**Base:** `7a445d3dd94cabd09807291a0ee94276559fcb0e`

**CurrentTask when explicitly restarted:** Canvas / Selection.

**MustReadSpecific:** `docs/GRAPHICAL-EDITOR-WAVE-08-IMPLEMENTATION-DECISION.md` and its DEV 1 section.

**AllowedScope:**
- `web/scada-web/src/engineering/visual-editor/canvas/**`
- `web/scada-web/tests-e2e/visual-editor-canvas*.spec.ts`

**Deliver:** viewport zoom/pan/grid/snap, selection/multiselect, move/resize/rotate interaction intents, duplicate/delete/z-order intents, deterministic UI-only selection/adornment state and focused tests.

**ForbiddenScope:** ReservedFiles; canonical schema/API/persistence; central workspace/route; Property Inspector; Object Palette/binding; asset backend/storage; public Visual Runtime/Python semantics; another worker branch.

**CompletionCriteria:** focused implementation/tests committed on own branch; supplied canonical definitions are not mutated directly; delivery head reported; then STOP.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`.

**Authorization state:** NOT AUTHORIZED TO EXECUTE until owner/coordinator explicitly restarts this worker.

---

# DEV 2 - EliteSCADA

**Role:** `WORKER`  
**Wave:** `GRAPHICAL-EDITOR-WAVE-08`  
**Status:** `STOPPED — WAIT_FOR_COORDINATOR`  
**Branch:** `feature/graphical-editor-wave-08-property-inspector`  
**Head:** `7a445d3dd94cabd09807291a0ee94276559fcb0e`  
**Base:** `7a445d3dd94cabd09807291a0ee94276559fcb0e`

**CurrentTask when explicitly restarted:** Property Inspector.

**MustReadSpecific:** `docs/GRAPHICAL-EDITOR-WAVE-08-IMPLEMENTATION-DECISION.md` and its DEV 2 section.

**AllowedScope:**
- `web/scada-web/src/engineering/visual-editor/property-inspector/**`
- `web/scada-web/tests-e2e/visual-editor-property-inspector*.spec.ts`

**Deliver:** inspector driven only by shared schema, typed property controls, explicit Default vs Engineering behavior, validation/error contract, deterministic supported multiselect behavior, change/remove intents and focused tests.

**ForbiddenScope:** duplicate property registries/defaults; ReservedFiles; canonical schema/API/persistence; central workspace/route; Canvas; Object Palette/binding; asset backend/storage; Runtime/Python writes; another worker branch.

**CompletionCriteria:** focused implementation/tests committed on own branch; no competing property authority; delivery head reported; then STOP.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`.

**Authorization state:** NOT AUTHORIZED TO EXECUTE until owner/coordinator explicitly restarts this worker.

---

# DEV 3 - EliteSCADA

**Role:** `WORKER`  
**Wave:** `GRAPHICAL-EDITOR-WAVE-08`  
**Status:** `STOPPED — WAIT_FOR_COORDINATOR`  
**Branch:** `feature/graphical-editor-wave-08-palette-bindings`  
**Head:** `7a445d3dd94cabd09807291a0ee94276559fcb0e`  
**Base:** `7a445d3dd94cabd09807291a0ee94276559fcb0e`

**CurrentTask when explicitly restarted:** Object Palette / Binding Foundation.

**MustReadSpecific:** `docs/GRAPHICAL-EDITOR-WAVE-08-IMPLEMENTATION-DECISION.md` and its DEV 3 section.

**AllowedScope:**
- `web/scada-web/src/engineering/visual-editor/object-palette/**`
- `web/scada-web/src/engineering/visual-editor/binding-editor/**`
- `web/scada-web/tests-e2e/visual-editor-palette*.spec.ts`
- `web/scada-web/tests-e2e/visual-editor-binding*.spec.ts`

**Deliver:** registered `core.*` palette, object-add intents, canonical binding authoring foundation, coordinator-provided source catalog boundary, validation of binding-capable destinations, Image palette entry using existing `assetRef`, focused tests.

**ForbiddenScope:** ReservedFiles; canonical schema/API/persistence; central workspace/route; Canvas; Property Inspector; asset backend/storage/import authority; Wave 07 property/Python contract changes; another worker branch.

**CompletionCriteria:** focused implementation/tests committed on own branch; no private persistence or direct driver source catalog; delivery head reported; then STOP.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`.

**Authorization state:** NOT AUTHORIZED TO EXECUTE until owner/coordinator explicitly restarts this worker.

## Coordinator note

Workers are intentionally stopped by owner instruction. Queued scope is preserved only so a future coordinator can restart each fixed chat without reconstructing the assignment. A stopped worker is not authorized to act merely because its task text remains present. Workers do not open/merge their own main PR, do not edit `main`, do not expand scope to solve coordinator dependencies and stop after delivery.
