# COORDINATOR HANDOFF — EliteSCADA

> Persistent coordinator resume checkpoint. New coordinator chats must use this document together with the mandatory current `main` documents and then verify the real GitHub branch/PR/CI state before acting.

**Handoff date:** 2026-08-28  
**Current wave:** `GRAPHICAL-EDITOR-WAVE-08`  
**Wave status:** **ACTIVE — PARTIAL COORDINATOR FOUNDATION / NOT CI VALIDATED**  
**CI mode:** `NORMAL`  
**Workers:** **STOPPED / WAIT_FOR_COORDINATOR**

## Exact GitHub state at handoff

- `main`: `17fd61a8aca7be09550999120a5347b21b0e58c5` before this handoff-doc synchronization.
- Wave 08 contract/base: `7a445d3dd94cabd09807291a0ee94276559fcb0e`.
- Integration branch: `integration/graphical-editor-wave-08`.
- Exact integration head: `4f8a71f59c5d033265c38c1c0beca2add9bb117b`.
- Integration vs `main`: diverged; 33 commits ahead / 4 behind; merge-base `7a445d3dd94cabd09807291a0ee94276559fcb0e` at this checkpoint.
- No GitHub Actions workflow run exists yet for `integration/graphical-editor-wave-08`.
- No Wave 08 integration PR is open from this coordinator work.

Worker branch heads are all still exactly at the Wave 08 contract base and contain no worker implementation:

- DEV 1 `feature/graphical-editor-wave-08-canvas`: `7a445d3dd94cabd09807291a0ee94276559fcb0e`.
- DEV 2 `feature/graphical-editor-wave-08-property-inspector`: `7a445d3dd94cabd09807291a0ee94276559fcb0e`.
- DEV 3 `feature/graphical-editor-wave-08-palette-bindings`: `7a445d3dd94cabd09807291a0ee94276559fcb0e`.

Owner explicitly instructed that the DEV chats are stopped. Do not assume background or pending worker deliveries.

## Coordinator implementation already present on integration

### Engineering Schema v13 / first-class Visual Assets

Wave 08 advances the integration branch to Engineering Schema v13 for first-class raster asset metadata while preserving historical schema compatibility.

Implemented:

- `VisualAssetEngineeringDto` canonical metadata entity;
- stable project-reference identity via `VisualAssetEngineeringDto.Id`;
- `Key` remains developer-facing and is not canonical reference identity;
- asset payload registry with content-addressed SHA-256 bytes;
- canonical `assetRef = null | { assetId }` remains unchanged;
- for Schema v13 `core.image.assetRef.assetId` resolves against stable Visual Asset `Id`, not `Key`;
- unknown/dangling v13 asset references fail closed;
- same-Key/different-Id import conflicts fail closed to prevent identity substitution;
- Schema v12 remains readable as asset-free historical Engineering.

Important identity rule:

`VisualAssetEngineeringDto.Id` = stable project/reference identity.  
`VisualAssetEngineeringDto.Key` = developer-facing key only.  
`Sha256` = immutable content identity.

Do not change this into key-based reference resolution.

### Raster validation / safety

Implemented backend validation for Wave 08 supported families:

- PNG;
- JPEG/JPG;
- BMP.

Limits remain locked by `docs/VISUAL-ASSET-STORAGE-WAVE-08.md`:

- 16 MiB maximum per raster payload;
- 16384 maximum pixel dimension.

`RasterImageInspector` now validates bounded structural shape rather than trusting filename/MIME alone:

- PNG requires signature, canonical IHDR, chunk traversal, IDAT and terminal IEND;
- JPEG requires SOF/SOS and terminal EOI with bounded segment traversal;
- BMP validates header/DIB/pixel offset/declared size coherence;
- malformed/truncated payloads fail closed.

`VisualAssetEngineeringValidator` also calls the raster inspector when bytes are available, so `.escadapkg`, revision restore and direct upload use the same structural validation rather than leaving a package-side bypass.

### Working asset authority / composition

A single canonical Working Visual Asset registry is owned by `EngineeringWorkspace` and shared through DI with:

- `EngineeringExchangeService`;
- project persistence;
- project package service;
- Visual Asset API;
- view/reference validation.

This corrected an earlier integration defect where asset state could exist outside `EngineeringWorkspace` and survive checkout/clear incorrectly.

### Persistence / revision semantics

Implemented asset-aware revision persistence:

- PostgreSQL content-addressed blob table;
- immutable revision-to-asset links;
- save revision JSON + required asset payload links transactionally;
- load exact revision asset payload set;
- Preview/Apply validates metadata/hash/length/media/structure;
- checkout now loads revision payloads before Preview/Apply;
- checkout rollback preserves both Engineering JSON and asset payload state;
- same logical Asset ID may point to a new hash in a later revision without mutating the older revision.

This fixed a concrete lifecycle bug discovered during review: checkout originally loaded only `engineering.json`, so a revision containing an image would report missing payload and rollback could lose Working asset bytes.

### `.escadapkg` v2

Implemented Project Package format v2 foundation:

- `manifest.json`;
- `engineering.json`;
- `assets/<lowercase-sha256>` sidecars;
- sidecar/hash/length/media checks;
- unexpected/missing sidecars fail closed;
- Inspect/Preview do not mutate Working;
- Apply receives payloads only through the validated import context;
- v1 asset-free package compatibility retained.

A deterministic compile blocker in the initial package implementation was found and fixed before CI (`EndsWith(char, StringComparison)` invalid overload).

Package request/service total size handling was aligned to a common bounded policy in the integration work. Recheck the exact current constants before changing them.

### Visual Asset API / frontend seam

Coordinator-owned API foundation exists for project-controlled Visual Assets, including:

- asset catalog/read seam;
- binary image import under Engineering Workspace CAS;
- `EngineeringModify` authorization;
- Audit recording;
- validated content serving by stable asset identity;
- canonical response/reference data without source filesystem authority.

Frontend Engineering contracts now include Visual Asset metadata plus API helpers for:

- loading asset catalog;
- binary asset import with expected Workspace version;
- project-controlled asset content URL;
- `visualAssets` in the Engineering snapshot.

This is infrastructure only. No Canvas/Property Inspector/Object Palette functional implementation was added by the coordinator.

## Source-level defects found and corrected during this pass

Do not regress these fixes:

1. invalid C# `EndsWith(char, StringComparison)` overload in package path validation;
2. Visual Asset registry originally outside `EngineeringWorkspace`, allowing stale/ghost asset state across checkout;
3. checkout/revision restore originally omitted revision asset payloads;
4. checkout rollback originally backed up only JSON, not Working asset bytes;
5. `.escadapkg` could otherwise carry hash-correct but structurally invalid raster bytes if only upload performed deep validation;
6. same developer `Key` with a different stable asset `Id` could otherwise cause Apply to preserve old ID while screens referenced the imported new ID;
7. existing Wave 07 Schema-v12/current-version test expectations needed adjustment after current Engineering advanced to v13.

The last three compatibility commits on integration are:

- `a1ac34e3e31fa1635a637e0696c424169fe98e85` — keep Schema v12 visual tests compatible with current v13;
- `941e73a2f049286ce4b76a5b7106deb932f9cf21` — preserve visual identity compatibility through Schema v13;
- `4f8a71f59c5d033265c38c1c0beca2add9bb117b` — prove Schema v12 asset-free compatibility with v13.

## Tests added on integration but NOT YET EXECUTED IN CI

Focused source tests now cover:

- supported raster family inspection;
- structurally truncated raster rejection;
- validator rejection of hash-correct malformed raster;
- Schema v13 Visual Asset metadata round-trip;
- prospective `core.image.assetRef` stable-ID resolution and unknown-ID rejection;
- `.escadapkg` v2 asset round-trip;
- Inspect/Preview non-mutation of Working;
- missing sidecar rejection;
- `.escadapkg` v1 asset-free compatibility;
- same-Key/different-Id conflict rejection;
- PostgreSQL two-revision same-Asset-ID/different-hash immutability.

Files include:

- `tests/Scada.Core.Tests/EngineeringSchemaV13CompatibilityTests.cs`;
- `tests/Scada.Core.Tests/VisualAssetWave08Tests.cs`;
- `tests/Scada.Core.Tests/VisualAssetIdentityConflictTests.cs`;
- `tests/Scada.Persistence.PostgreSql.Tests/PostgreSqlVisualAssetRevisionTests.cs`;
- updated Wave 07 Schema v11/v12 compatibility tests.

These are implementation evidence only until build/test/CI succeeds on the exact integration head.

## Explicitly NOT complete

Wave 08 is not closeable yet.

Not completed/validated at this checkpoint:

- no Actions run for the Wave 08 integration branch;
- no exact-head build/test evidence yet for the coordinator asset changes;
- no Wave 08 integration PR;
- integration branch is behind current `main` coordination/doc commits and must be reconciled before final exact-head CI;
- coordinator-owned canonical Screen graphical mutation/save/reopen seam is not yet finished;
- central graphical editor workspace/route/renderer/localization composition is not yet finished;
- DEV 1 Canvas/Selection implementation has not started;
- DEV 2 Property Inspector implementation has not started;
- DEV 3 Object Palette/Binding implementation has not started;
- full integrated product gate has not been exercised;
- Wave 09/10 remain forbidden/not active.

Do not label Wave 08 validated, merge-ready or complete.

## Resume procedure for the next Coordinator chat

On the next `COORDENADOR - EliteSCADA` chat, before changing code:

1. read current `main` mandatory documents:
   - `PROJECT GOAL.md`;
   - `LAST CHANGE.md`;
   - `docs/ROADMAP.md`;
   - `docs/PARALLEL-WORK.md`;
   - `docs/DEVELOPMENT-WAVES.md`;
   - `docs/CHAT-WORK-ASSIGNMENTS.md`;
   - `docs/CI-USAGE-POLICY.md`;
   - `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`;
   - this `docs/COORDINATOR-HANDOFF.md`;
   - Wave 08 MustReadSpecific documents;
2. verify real `main`, integration and worker branch heads plus open PR/Actions state;
3. treat workers as STOPPED unless the owner explicitly restarts them;
4. continue from `integration/graphical-editor-wave-08`, not from chat memory;
5. first perform a static/build-risk review of exact integration head `4f8a71f59c5d033265c38c1c0beca2add9bb117b` and reconcile it with current `main` before any final CI/PR;
6. finish coordinator-owned Screen mutation/save/reopen and central editor composition seams;
7. only restart/authorize worker chats deliberately, with their scopes unchanged unless the coordinator updates the board;
8. integrate worker heads only after explicit deliveries;
9. run focused tests, then full exact-head Wave 08 CI;
10. merge only after the complete product gate is green and post-merge `main` is healthy.

## Wave 08 final product gate remains unchanged

The integrated wave must prove:

`Create Screen -> add objects -> move/resize/rotate -> edit registered properties -> canonical binding -> image asset by stable assetRef -> save -> reopen -> export/import`

Transient selection/viewport/adornment state must never become canonical persisted Engineering authority.

Wave 09/10 functionality remains out of scope until Wave 08 is formally closed.
