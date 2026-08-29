# COORDINATOR HANDOFF — EliteSCADA

> Persistent coordinator resume checkpoint. New coordinator chats must use this document together with the mandatory current `main` documents and then verify real GitHub branch/PR/CI state before acting.

**Handoff date:** 2026-08-28  
**Current wave:** `GRAPHICAL-EDITOR-WAVE-08`  
**Wave status:** **ACTIVE — CENTRAL SCREEN EDITOR FOUNDATION IMPLEMENTED / NOT CI VALIDATED**  
**CI policy:** `NORMAL`; Actions are authorized, but usage must remain conservative  
**Workers:** **STOPPED / WAIT_FOR_COORDINATOR**

## Exact GitHub checkpoint

- `main` reconciled commit at this pass: `6399464cadda96aaaa075aff83bed2ce5b67da89`.
- Wave 08 logical base: `8de706882ba20afedd666532ac41ae11115d06b3`.
- Wave 08 contract/base: `7a445d3dd94cabd09807291a0ee94276559fcb0e`.
- Integration branch: `integration/graphical-editor-wave-08`.
- Main reconciliation merge on integration: `011d3c649bb868bfcf8d13bfafe896bb0863a153`.
- Latest coordinator implementation-contract checkpoint before the CI-policy documentation update: `f3968eba629a97c5b85936ae9b908732e78a4e3a`.
- After reconciliation, GitHub compare reported integration **0 behind `main`**.
- No Wave 08 GitHub Actions validation exists yet.
- No Wave 08 integration PR exists yet.

Documentation-only commits after the code checkpoint are valid successors; always verify the live branch head before acting.

## Actions budget rule

The product owner explicitly authorized GitHub Actions use for Wave 08, with conservative consumption.

Operational rule:

- use static/diff/contract review first when it answers the question;
- prefer focused build/test evidence before a full workflow matrix;
- batch coherent changes rather than trigger CI for every small commit;
- do not rerun unchanged heads merely for reassurance;
- inspect and fix the cause of a localized failure before spending another full run;
- use PR-triggered/full matrix validation only at meaningful integration/final checkpoints;
- never weaken tests, security, CAS, persistence, Runtime or final acceptance to save minutes.

The final integrated Wave 08 head still requires the complete validation matrix before merge.

Worker branches remain preserved at the contract base unless GitHub proves otherwise:

- DEV 1 `feature/graphical-editor-wave-08-canvas`: `7a445d3dd94cabd09807291a0ee94276559fcb0e`.
- DEV 2 `feature/graphical-editor-wave-08-property-inspector`: `7a445d3dd94cabd09807291a0ee94276559fcb0e`.
- DEV 3 `feature/graphical-editor-wave-08-palette-bindings`: `7a445d3dd94cabd09807291a0ee94276559fcb0e`.

Owner explicitly stopped the DEV chats. Preserved assignments do not authorize execution.

## Coordinator implementation present on integration

### 1. Engineering Schema v13 / first-class Visual Assets

Implemented and source-reviewed, but not yet exact-head CI validated:

- `VisualAssetEngineeringDto` canonical metadata entity;
- stable project/reference identity via `VisualAssetEngineeringDto.Id`;
- developer-facing `Key` remains separate from canonical identity;
- content-addressed SHA-256 Working payload registry;
- canonical `assetRef = null | { assetId }` unchanged;
- Schema v13 `core.image.assetRef.assetId` resolves by stable Visual Asset `Id`;
- unknown/dangling asset references fail closed;
- same-Key/different-Id imports fail closed;
- Schema v12 remains readable as asset-free historical Engineering.

Identity rule remains locked:

`Id = stable project/reference identity`  
`Key = developer-facing key`  
`Sha256 = immutable content identity`

Do not convert references to key-based resolution.

### 2. Raster safety / project package / revision persistence

Implemented:

- bounded PNG/JPEG/BMP structural inspection;
- 16 MiB maximum raster payload;
- 16384 maximum pixel dimension;
- Working Visual Asset authority owned by `EngineeringWorkspace`;
- PostgreSQL content-addressed asset blobs plus immutable revision links;
- revision save/load/Preview/Apply/checkout/rollback includes exact asset payload state;
- `.escadapkg` v2 contains `manifest.json`, `engineering.json`, and `assets/<lowercase-sha256>` sidecars;
- v1 asset-free package compatibility remains;
- malformed, unexpected, missing or metadata-inconsistent sidecars fail closed;
- protected Visual Asset API uses Engineering CAS/authorization/Audit;
- frontend has asset catalog/import/content seams using project-controlled stable identities.

Earlier source review corrected several concrete defects: invalid C# `EndsWith` overload, asset registry authority outside Workspace, checkout payload omission, rollback payload omission, malformed raster package bypass, and key/ID substitution risk.

### 3. Central graphical Screen editor foundation

Coordinator-owned central composition now exists under:

- `web/scada-web/src/engineering/visual-editor/VisualEditorWorkspace.tsx`;
- `web/scada-web/src/engineering/visual-editor/CanonicalVisualRenderer.tsx`;
- `web/scada-web/src/engineering/visual-editor/visualEditorCanonicalModel.ts`;
- `web/scada-web/src/engineering/visual-editor/visualEditorContracts.ts`;
- `web/scada-web/src/engineering/visual-editor/VisualEditorWorkspace.css`;
- `web/scada-web/src/engineering/EngineeringApp.tsx` integration.

Current behavior:

- Engineering `Telas` opens the graphical editor foundation instead of the prior read-only table;
- the left surface selects existing Screens or starts a new Screen draft;
- central composition reserves explicit Object Palette / Canvas / Property Inspector boundaries for worker delivery;
- Screen name/key/route are edited on a canonical `ScreenEngineering` draft;
- no private Canvas document/project persistence exists;
- helper functions clone canonical Engineering, replace only the selected Screen candidate, update visual tree nodes by stable object ID and recursively count objects;
- Preview builds the candidate from the public Engineering package and uses the existing `/api/engineering/import/json/preview` authority;
- Workspace `changeVersion` is checked before and after Preview so validation fails if canonical Working changed during the preview window;
- Apply uses the existing protected public Engineering Apply path with the retained expected Workspace version;
- backend mutation lock/CAS/authorization/Audit therefore remain the mutation authority;
- successful Apply reloads canonical Engineering;
- `visualEditorContracts.ts` separates UI-only selection/viewport state from canonical object/property/binding intents so worker modules share one integration vocabulary;
- central copy is present in PT-BR, EN and ES.

### 4. Renderer behavior and legacy compatibility

For Wave 07/08 registered object types:

- renderer uses the shared built-in `core.*` schema registry;
- registry defaults are used only as runtime/editor projection defaults and are not persisted merely by rendering;
- explicit canonical Engineering values are decoded through the shared property codec;
- `core.image` content resolves through stable project `assetRef` via the existing asset content endpoint;
- groups recurse through canonical `children`.

Important compatibility boundary discovered during this pass:

The current demo seed still contains historical visual types such as `tank`, `dynamo`, `value` and `status`. These are not silently migrated by the frontend and are not treated as new registered `core.*` objects. The central renderer shows them as non-authoritative compatibility placeholders using only their existing canonical label/x/y when available. This prevents an ugly error-only demo while avoiding a second property/type authority.

Do not turn this compatibility projection into persisted migration logic. Backend/schema migration remains the proper authority for any future formal conversion.

### 5. Screen Preview/Apply/reopen test

Added:

`web/scada-web/tests-e2e/visual-editor-workspace.spec.ts`

The test:

1. exports the original Engineering package;
2. opens `Engineering -> Telas`;
3. selects the seeded Screen;
4. verifies the seeded visual preview opens without renderer-error cards;
5. edits its canonical route;
6. proves Apply is disabled before Preview;
7. runs Preview and requires a valid candidate;
8. applies through the UI;
9. confirms the canonical exported Screen changed;
10. confirms the editor reopens with the persisted route;
11. verifies Workspace version advanced;
12. restores the original package in `finally`.

This test is committed but **has not been executed by GitHub Actions yet**.

## Commits from this coordinator continuation

Important implementation checkpoints include:

- `40575736afa88d022135d337047e3d7e751b93ce` — canonical Screen editor mutation helpers;
- `b60b1e8773c8f6dce4ccec728951176c9cde37f6` — canonical visual preview renderer;
- `f24ca883688655c0b4939982546fa483a270fcde` — central Screen editor workspace;
- `82d1a5d3be3347cfd182022eeb8b3611d08be71f` — editor composition styling;
- `14614ba1ef6ce6ea1cfc84fc066843de34706c64` — connect Screen editor into Engineering;
- `280c5509dc1253587ba1886a6364e9ea5660917e` — tolerate backend-assigned Screen ID when matching selection;
- `011d3c649bb868bfcf8d13bfafe896bb0863a153` — merge current `main` coordination state into integration;
- `2c5dbbf80346b61350ee344dd01ca8da2fc218cb` — Screen Preview/Apply/reopen E2E;
- `6b089c3190b594b0a8c71ab9aaffa69090df4670` — legacy visual compatibility placeholders;
- `a2cdc39a9d7a8dcf425aab6a253d6bb0694faab1` — placeholder styling;
- `8a7da6cd6208c19314fbf33281402c7df6b9d588` — assert Screen preview has no renderer errors;
- `d49182474e3275e798b2a1434c48466d1b846ac1` — shared Wave 08 editor integration contracts;
- `f3968eba629a97c5b85936ae9b908732e78a4e3a` — record the shared integration contract in the operational checkpoint.

## Current validation evidence and limits

Available:

- static/source review against actual GitHub branch contents;
- backend Preview/Apply CAS/Audit path inspected and reused rather than duplicated;
- Screen validator and View handler inspected;
- existing secured TAG/Data Source/Alarm editors confirmed as precedent for full-package candidate Preview/Apply;
- integration reconciled with current `main` and verified 0 behind at the checkpoint;
- new focused E2E test committed;
- shared typed worker/coordinator intent boundary committed.

Not available yet:

- authoritative `npm run build` on the exact integration head;
- .NET Release build/full tests;
- PostgreSQL/Timescale integration tests;
- Runtime smoke;
- Playwright Chromium execution;
- full exact-head GitHub Actions matrix.

Do not call Wave 08 validated or merge-ready.

## Remaining Wave 08 work

Coordinator foundation is advanced, but the actual interactive graphical editor still needs the preserved worker slices:

- DEV 1: Canvas/selection, zoom/pan/grid/snap, move/resize/rotate, duplicate/delete/z-order intents;
- DEV 2: Property Inspector driven only by the shared Visual Property Registry;
- DEV 3: registered Object Palette + canonical binding authoring foundation.

After worker delivery, coordinator must integrate their intents into canonical Screen draft mutation without letting transient selection/viewport/adornment state become persisted Engineering.

Still pending globally:

- exact-head build/test evidence for coordinator code;
- worker implementation/integration;
- object creation and geometry editing through the integrated UI;
- registered property editing;
- canonical binding authoring;
- image object workflow using stable `assetRef`;
- complete save/reopen/export/import gate;
- final Wave 08 PR/CI/merge/post-merge health.

Wave 09/10 remain forbidden/not active.

## Resume procedure

On the next `COORDENADOR - EliteSCADA` execution:

1. read current `main`: `PROJECT GOAL.md`, `LAST CHANGE.md`, `docs/ROADMAP.md`, `docs/PARALLEL-WORK.md`, `docs/DEVELOPMENT-WAVES.md`, `docs/CHAT-WORK-ASSIGNMENTS.md`, `docs/CI-USAGE-POLICY.md`, `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`, this handoff and Wave 08 MustReadSpecific docs;
2. verify real main/integration/worker heads, PRs and Actions;
3. treat all workers as STOPPED unless deliberately restarted;
4. continue from `integration/graphical-editor-wave-08` and verify its live head;
5. use Actions conservatively, selecting the cheapest validation that answers the current decision; do not rerun unchanged heads for reassurance;
6. fix any build/test failure before broadening scope;
7. restart worker chats only deliberately and preserve their existing ownership boundaries plus the shared `visualEditorContracts.ts` seam;
8. integrate worker heads only after explicit delivery;
9. exercise the complete Wave 08 product gate and full exact-head CI at the meaningful final integration checkpoint;
10. merge only green, verify post-merge `main`, synchronize docs, close Wave 08, then activate Wave 09.

## Wave 08 final product gate

The integrated wave must prove:

`Create Screen -> add objects -> move/resize/rotate -> edit registered properties -> canonical binding -> image asset by stable assetRef -> save -> reopen -> export/import`

Transient selection/viewport/adornment state must never become canonical persisted Engineering authority.
