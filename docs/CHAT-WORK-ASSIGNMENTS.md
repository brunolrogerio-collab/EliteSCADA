# CHAT WORK ASSIGNMENTS — EliteSCADA

> Authoritative live execution board. GitHub branch/PR/head/CI state is operational truth. Permanent rules: `docs/DEVELOPMENT-WAVES.md`, `docs/PARALLEL-WORK.md`, `docs/CI-USAGE-POLICY.md`.

**Last coordinator synchronization:** 2026-08-28 — Wave 07 merged and post-merge green; Wave 08 Definition of Ready satisfied; graphical editor branches created from the frozen Wave 08 contract; DEV 1/2/3 assignments are now ACTIVE.

## Mandatory `siga`

Every fixed EliteSCADA chat first rereads current `main`: `PROJECT GOAL.md`, `LAST CHANGE.md`, `docs/ROADMAP.md`, `docs/PARALLEL-WORK.md`, `docs/DEVELOPMENT-WAVES.md`, this board, `docs/CI-USAGE-POLICY.md`, `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`, and current MustReadSpecific documents. Then verify real branch/head/CI and execute only the current authorized assignment.

## Current product gate

`GRAPHICAL-EDITOR-WAVE-08` is **ACTIVE**.

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
- DEV 1 branch: `feature/graphical-editor-wave-08-canvas`
- DEV 2 branch: `feature/graphical-editor-wave-08-property-inspector`
- DEV 3 branch: `feature/graphical-editor-wave-08-palette-bindings`
- CI mode: NORMAL
- Wave 09/10: NOT ACTIVE

Wave 08 execution contract: `docs/GRAPHICAL-EDITOR-WAVE-08-IMPLEMENTATION-DECISION.md`.

---

# COORDENADOR - EliteSCADA

**Role:** `COORDINATOR`  
**Wave:** `GRAPHICAL-EDITOR-WAVE-08`  
**Status:** `ACTIVE — CENTRAL CANONICAL INTEGRATION`  
**IntegrationBranch:** `integration/graphical-editor-wave-08`  
**LogicalWaveBaseSHA:** `8de706882ba20afedd666532ac41ae11115d06b3`  
**ContractSHA:** `7a445d3dd94cabd09807291a0ee94276559fcb0e`

**CurrentTask:** establish and integrate the coordinator-owned canonical Screen editor seam, first-class project image asset authority, central Engineering workspace/route/renderer composition, then integrate worker deliveries without weakening the Wave 07 contracts.

**MustReadSpecific:**
- `docs/GRAPHICAL-EDITOR-WAVE-08-IMPLEMENTATION-DECISION.md`
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

**CompletionCriteria:** exact integrated Wave 08 head proves create Screen -> add/move/resize/rotate -> edit properties -> canonical binding -> image asset by stable `assetRef` -> save/reopen -> export/import, with transient Canvas state absent from persisted authority; full required CI green; merged main post-merge healthy; docs synchronized.

---

# DEV 1 - EliteSCADA

**Role:** `WORKER`  
**Wave:** `GRAPHICAL-EDITOR-WAVE-08`  
**Status:** `ACTIVE`  
**Branch:** `feature/graphical-editor-wave-08-canvas`  
**Base:** `7a445d3dd94cabd09807291a0ee94276559fcb0e`

**CurrentTask:** Canvas / Selection.

**MustReadSpecific:** `docs/GRAPHICAL-EDITOR-WAVE-08-IMPLEMENTATION-DECISION.md` and its DEV 1 section.

**AllowedScope:**
- `web/scada-web/src/engineering/visual-editor/canvas/**`
- `web/scada-web/tests-e2e/visual-editor-canvas*.spec.ts`

**Deliver:** viewport zoom/pan/grid/snap, selection/multiselect, move/resize/rotate interaction intents, duplicate/delete/z-order intents, deterministic UI-only selection/adornment state and focused tests.

**ForbiddenScope:** ReservedFiles; canonical schema/API/persistence; central workspace/route; Property Inspector; Object Palette/binding; asset backend/storage; public Visual Runtime/Python semantics; another worker branch.

**CompletionCriteria:** focused implementation/tests committed on own branch; supplied canonical definitions are not mutated directly; delivery head reported; then STOP.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`.

---

# DEV 2 - EliteSCADA

**Role:** `WORKER`  
**Wave:** `GRAPHICAL-EDITOR-WAVE-08`  
**Status:** `ACTIVE`  
**Branch:** `feature/graphical-editor-wave-08-property-inspector`  
**Base:** `7a445d3dd94cabd09807291a0ee94276559fcb0e`

**CurrentTask:** Property Inspector.

**MustReadSpecific:** `docs/GRAPHICAL-EDITOR-WAVE-08-IMPLEMENTATION-DECISION.md` and its DEV 2 section.

**AllowedScope:**
- `web/scada-web/src/engineering/visual-editor/property-inspector/**`
- `web/scada-web/tests-e2e/visual-editor-property-inspector*.spec.ts`

**Deliver:** inspector driven only by shared schema, typed property controls, explicit Default vs Engineering behavior, validation/error contract, deterministic supported multiselect behavior, change/remove intents and focused tests.

**ForbiddenScope:** duplicate property registries/defaults; ReservedFiles; canonical schema/API/persistence; central workspace/route; Canvas; Object Palette/binding; asset backend/storage; Runtime/Python writes; another worker branch.

**CompletionCriteria:** focused implementation/tests committed on own branch; no competing property authority; delivery head reported; then STOP.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`.

---

# DEV 3 - EliteSCADA

**Role:** `WORKER`  
**Wave:** `GRAPHICAL-EDITOR-WAVE-08`  
**Status:** `ACTIVE`  
**Branch:** `feature/graphical-editor-wave-08-palette-bindings`  
**Base:** `7a445d3dd94cabd09807291a0ee94276559fcb0e`

**CurrentTask:** Object Palette / Binding Foundation.

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

## Coordinator note

Exactly one ACTIVE assignment exists per worker. Queued future work is not authorization. Workers do not open/merge their own main PR, do not edit `main`, do not expand scope to solve coordinator dependencies and stop after delivery.
