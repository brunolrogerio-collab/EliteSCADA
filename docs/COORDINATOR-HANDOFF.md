# COORDINATOR HANDOFF — EliteSCADA

> Persistent coordinator resume checkpoint. Read this with the mandatory current `main` documents, then verify live GitHub branch/PR/CI state before acting.

**Handoff date:** 2026-08-28  
**Current wave:** `GRAPHICAL-EDITOR-WAVE-08`  
**Wave status:** **ACTIVE — CENTRAL FOUNDATION VALIDATED / INTERACTIVE WORKER SLICES PENDING**  
**CI policy:** `NORMAL`; Actions authorized with conservative usage  
**Workers:** **STOPPED / WAIT_FOR_COORDINATOR; dependency-prepared only**

## Exact validated checkpoint

- `main` used for reconciliation: `6399464cadda96aaaa075aff83bed2ce5b67da89`
- logical WaveBaseSHA: `8de706882ba20afedd666532ac41ae11115d06b3`
- contract/base: `7a445d3dd94cabd09807291a0ee94276559fcb0e`
- integration branch: `integration/graphical-editor-wave-08`
- reconciliation merge: `011d3c649bb868bfcf8d13bfafe896bb0863a153`
- Draft integration PR: **#90**
- exact validated product head: **`b48b489660ae953029fd2416aa18b149eaa18258`**
- CI: **#515 / run `33225051402` — SUCCESS**

CI #515 proved on that exact head:

- Web React/Vite/TypeScript build: SUCCESS;
- backend Release build: SUCCESS;
- backend full tests including PostgreSQL/Timescale coverage: SUCCESS;
- Runtime smoke: SUCCESS;
- Chromium E2E: SUCCESS;
- `visual-editor-workspace.spec.ts`: SUCCESS;
- existing Wave 06/07 Python/visual browser regressions remained green.

Documentation-only successors after `b48b489...` use `[skip ci]` and do not invalidate unchanged product evidence. Always distinguish the validated product head from later handoff-only commits.

PR #90 remains **Draft**. Do not merge it until DEV 1/2/3 interaction slices are integrated and the complete Wave 08 gate passes on a later exact final product head.

## Actions discipline

Owner explicitly authorized Actions, conservatively:

- use code/diff/contract review first when sufficient;
- prefer focused evidence during iteration;
- batch coherent changes;
- no unchanged-head reassurance reruns;
- inspect and fix a localized failure before another expensive run;
- reserve complete matrices for meaningful product/integration checkpoints;
- never weaken tests, security, CAS, persistence, Runtime or final acceptance to save minutes.

The coordinator followed that policy in this checkpoint: each new run was caused by a concrete corrective commit, never an unchanged rerun.

## CI defects found and corrected

1. **TypeScript readonly/mutable mismatch** in `visualEditorCanonicalModel.ts` when replacing `Screen.elements`; fixed by cloning to a mutable array.
2. **C# record reserved method name**: `EngineeringRevisionAssetPayload.Clone()` and `VisualAssetPayload.Clone()` caused CS8859; helpers renamed to `Copy()` and call sites updated.
3. **Stale broad Runtime E2E contract** expected `.escadapkg` format v1. Wave 08 intentionally emits v2 for asset sidecars, so the current-package expectation was updated to v2. Dedicated compatibility coverage still proves historical v1 input remains accepted.

After those fixes, CI #515 was fully green.

## Validated coordinator foundation

### Visual Assets / Schema v13

- first-class `VisualAssetEngineeringDto` metadata;
- stable project/reference `Id`, developer `Key`, immutable SHA-256 content identity kept distinct;
- `assetRef = null | { assetId }` remains canonical reference shape;
- bounded PNG/JPEG/BMP structural validation;
- 16 MiB maximum raster payload and 16384 maximum pixel dimension;
- unknown/dangling v13 asset references fail closed;
- same-Key/different-Id substitution fails closed;
- Working asset authority belongs to `EngineeringWorkspace`;
- PostgreSQL content-addressed blobs + immutable revision links;
- asset-aware save/load/Preview/Apply/checkout/rollback;
- `.escadapkg` v2 sidecars with v1 asset-free compatibility;
- protected Visual Asset API through Engineering CAS/authorization/Audit;
- frontend asset catalog/import/content seams.

Identity rule remains locked:

`Id = stable project/reference identity`  
`Key = developer-facing key`  
`Sha256 = immutable content identity`

Never change references to key-based authority.

### Central Screen editor

Coordinator-owned central files include:

- `web/scada-web/src/engineering/visual-editor/VisualEditorWorkspace.tsx`
- `web/scada-web/src/engineering/visual-editor/CanonicalVisualRenderer.tsx`
- `web/scada-web/src/engineering/visual-editor/visualEditorCanonicalModel.ts`
- `web/scada-web/src/engineering/visual-editor/visualEditorContracts.ts`
- `web/scada-web/src/engineering/visual-editor/VisualEditorWorkspace.css`
- central `EngineeringApp.tsx` composition

Validated behavior:

- `Engineering -> Telas` enters the graphical editor foundation;
- existing Screen selection + new Screen draft surface;
- central slots for Object Palette / Canvas / Property Inspector;
- Screen name/key/route edit canonical `ScreenEngineering`, never private Canvas persistence;
- candidate generation clones public canonical Engineering and replaces the selected Screen only;
- Preview uses public Engineering Preview and checks Workspace `changeVersion` stability;
- Apply uses protected public Engineering Apply with expected Workspace version/CAS;
- successful Apply reloads canonical Engineering;
- `visualEditorContracts.ts` freezes one shared vocabulary separating transient selection/viewport state from canonical object/property/binding mutation intents;
- registered `core.*` rendering consumes the shared Visual Property Registry;
- `core.image` resolves stable project `assetRef`;
- historical seeded visual types such as `tank`, `dynamo`, `value`, `status` render only as non-authoritative compatibility placeholders; frontend does not silently migrate/persist them;
- PT-BR/EN/ES editor copy exists.

### Screen save/reopen E2E

`web/scada-web/tests-e2e/visual-editor-workspace.spec.ts` has real CI evidence from #515. It proves canonical Screen load, error-free seeded preview, route edit, Preview-before-Apply, canonical Apply/export, UI reopen, Workspace version advance and test cleanup/restore.

## Worker branches — prepared dependency, still STOPPED

Logical worker Base remains `7a445d3dd94cabd09807291a0ee94276559fcb0e`.

The coordinator seeded **only** the shared `visualEditorContracts.ts` into each worker branch. No worker implementation has started and no integration-branch rebase was performed:

- DEV 1 `feature/graphical-editor-wave-08-canvas` head **`57521312914e21e303976a81bc81c84ad5aa9cbb`** — dependency seed only;
- DEV 2 `feature/graphical-editor-wave-08-property-inspector` head **`3e942ed641d96afe848966f123fb10eaeaa99ed7`** — dependency seed only;
- DEV 3 `feature/graphical-editor-wave-08-palette-bindings` head **`df3cd6c332a19bb3011373c95d010f33754c0c12`** — dependency seed only.

All three remain **STOPPED / WAIT_FOR_COORDINATOR**. These coordinator commits are not worker delivery and do not authorize execution.

When restarted, each must consume the seeded coordinator-owned contract rather than inventing competing intents. Reserved central files/domains remain those frozen by `docs/GRAPHICAL-EDITOR-WAVE-08-IMPLEMENTATION-DECISION.md`.

## Remaining Wave 08 work

The central foundation is validated, but Wave 08 is **not complete and not merge-ready**.

Still required:

- DEV 1: viewport, zoom/pan/grid/snap, selection/multiselect, move/resize/rotate, duplicate/delete/z-order intents;
- DEV 2: shared-schema-driven typed Property Inspector with Default vs Engineering semantics;
- DEV 3: registered `core.*` Object Palette + canonical binding authoring foundation;
- coordinator integration of worker intents into canonical Screen drafts;
- practical Image object selection/import/reference flow using stable `assetRef`;
- full create/add/move/resize/rotate/property/binding/image/save/reopen/export/import acceptance;
- final exact-head full CI after worker integration;
- PR #90 readiness and merge only after full green;
- post-merge `main` health confirmation.

Wave 09/10 remain NOT ACTIVE.

## Resume procedure

1. read mandatory current `main` docs plus this handoff and Wave 08 MustReadSpecific docs;
2. verify live `main`, PR #90, integration and all worker heads/CI;
3. reuse CI #515 evidence while the central product code is unchanged;
4. keep workers STOPPED unless deliberately restarted;
5. when restarting, start from the dependency-prepared heads above, preserve fixed scopes and require the shared `visualEditorContracts.ts` contract;
6. review worker deliveries before integration;
7. integrate only through `integration/graphical-editor-wave-08`;
8. use focused validation while composing slices;
9. run the next full matrix only at a meaningful integrated product checkpoint;
10. merge only after complete Wave 08 gate is green and then verify post-merge `main` before activating Wave 09.

## Final Wave 08 gate

`Create Screen -> add objects -> move/resize/rotate -> edit registered properties -> canonical binding -> image asset by stable assetRef -> save -> reopen -> export/import`

Transient selection/viewport/adornment state must never become canonical persisted Engineering authority.
