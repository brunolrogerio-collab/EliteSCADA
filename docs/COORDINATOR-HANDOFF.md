# COORDINATOR HANDOFF — EliteSCADA

> Persistent coordinator resume checkpoint. New coordinator chats must use this document together with the mandatory current `main` documents and then verify live GitHub branch/PR/CI state before acting.

**Handoff date:** 2026-08-28  
**Current wave:** `GRAPHICAL-EDITOR-WAVE-08`  
**Merged product:** Wave 07 closed; Wave 08 remains unmerged  
**Wave status:** **ACTIVE — CENTRAL FOUNDATION VALIDATED IN DRAFT PR #90 / DEV 1-2-3 ACTIVE**  
**CI policy:** `NORMAL`; Actions authorized with conservative usage

## Exact checkpoint

- current coordination `main` includes the worker activation board and this handoff; verify the live main SHA before acting;
- Wave 08 logical base: `8de706882ba20afedd666532ac41ae11115d06b3`;
- Wave 08 ContractSHA / logical worker base: `7a445d3dd94cabd09807291a0ee94276559fcb0e`;
- integration branch: `integration/graphical-editor-wave-08`;
- Draft integration PR: **#90**;
- exact central-foundation product head validated before documentation successors: **`b48b489660ae953029fd2416aa18b149eaa18258`**;
- CI #515 / run `33225051402`: **SUCCESS**;
- later coordination/documentation successors use `[skip ci]` and do not invalidate unchanged product evidence.

PR #90 remains Draft. It is not merge-ready because the three interactive worker slices and final integrated product gate are still pending.

## CI #515 evidence

On exact product head `b48b489...`:

- Web React/Vite/TypeScript build: SUCCESS;
- backend Release build: SUCCESS;
- full backend tests including PostgreSQL/Timescale: SUCCESS;
- Runtime smoke: SUCCESS;
- Chromium E2E: SUCCESS;
- `visual-editor-workspace.spec.ts`: SUCCESS;
- Wave 06/07 Python/visual browser regressions remained green.

The validation checkpoint found and corrected before success:

1. TypeScript readonly/mutable mismatch in canonical Screen clone logic;
2. C# record helpers named `Clone`, which is reserved by records; renamed to `Copy`;
3. broad Runtime E2E still expected `.escadapkg` format v1 while current Wave 08 export is v2; expectation updated while dedicated v1 compatibility remains.

No unchanged-head reassurance reruns were used.

## Coordinator foundation implemented and validated in PR #90

### Visual Assets / Schema v13

- first-class `VisualAssetEngineeringDto` metadata;
- stable project/reference identity by Visual Asset `Id`;
- developer `Key` remains separate from canonical reference identity;
- SHA-256 content-addressed Working payload authority;
- canonical `assetRef = null | { assetId }`;
- unknown/dangling references fail closed;
- same-Key/different-Id identity conflicts fail closed;
- v12 remains readable as asset-free historical Engineering;
- bounded PNG/JPEG/BMP structural validation;
- 16 MiB max raster payload and 16384 max pixel dimension per locked storage contract;
- PostgreSQL content-addressed asset blobs + immutable revision links;
- revision save/load/checkout/rollback restores exact asset payload state;
- `.escadapkg` v2 contains asset sidecars and retains v1 asset-free compatibility;
- protected Visual Asset API uses Engineering CAS/authorization/Audit;
- frontend asset catalog/import/content seams use project-controlled stable identity.

Identity remains locked:

`Id = stable project/reference identity`  
`Key = developer-facing key`  
`Sha256 = immutable content identity`

Do not convert asset references to key/path/URL authority.

### Central Screen editor foundation

Coordinator-owned files include:

- `web/scada-web/src/engineering/visual-editor/VisualEditorWorkspace.tsx`;
- `web/scada-web/src/engineering/visual-editor/CanonicalVisualRenderer.tsx`;
- `web/scada-web/src/engineering/visual-editor/visualEditorCanonicalModel.ts`;
- `web/scada-web/src/engineering/visual-editor/visualEditorContracts.ts`;
- `web/scada-web/src/engineering/visual-editor/VisualEditorWorkspace.css`;
- central `EngineeringApp.tsx` composition.

Current validated behavior:

- `Engineering -> Telas` opens the central editor foundation;
- Screen key/name/route drafts edit canonical `ScreenEngineering`;
- Preview creates a candidate from canonical Engineering and uses public Engineering Preview;
- Workspace `changeVersion` remains the CAS boundary;
- Apply reuses the protected canonical Engineering Apply authority with authorization/Audit;
- successful Apply reloads canonical Engineering;
- `core.*` rendering consumes the shared Visual Property Registry/defaults without persisting renderer defaults merely by viewing;
- `core.image` resolves project content through stable `assetRef`;
- historical demo visual types are shown as non-authoritative compatibility placeholders, not silently migrated;
- `visualEditorContracts.ts` separates UI-only selection/viewport state from canonical mutation intents;
- central composition has explicit Canvas / Property Inspector / Object Palette + Binding integration slots;
- PT-BR/EN/ES copy exists;
- `visual-editor-workspace.spec.ts` proves Screen route edit -> Preview -> Apply -> canonical export -> reopen -> restore original package.

## Worker authorization state

The previous stop is deliberately lifted. All three worker slices are now **ACTIVE / AUTHORIZED** because the coordinator foundation is green and each branch has the shared intent contract seeded.

Logical BaseSHA remains `7a445d3dd94cabd09807291a0ee94276559fcb0e`.

### DEV 1 — Canvas / Selection

- branch: `feature/graphical-editor-wave-08-canvas`;
- authorization/seed head: `57521312914e21e303976a81bc81c84ad5aa9cbb`;
- before worker implementation, exactly one commit ahead of ContractSHA containing only `visualEditorContracts.ts`;
- status: **ACTIVE — AUTHORIZED**;
- owns only `visual-editor/canvas/**` and focused `visual-editor-canvas*.spec.ts` tests;
- deliver zoom/pan/grid/snap, selection/multiselect, move/resize/rotate, duplicate/delete/z-order intents and transient adornment state.

### DEV 2 — Property Inspector

- branch: `feature/graphical-editor-wave-08-property-inspector`;
- authorization/seed head: `3e942ed641d96afe848966f123fb10eaeaa99ed7`;
- before worker implementation, exactly one commit ahead of ContractSHA containing only `visualEditorContracts.ts`;
- status: **ACTIVE — AUTHORIZED**;
- owns only `visual-editor/property-inspector/**` and focused tests;
- deliver schema-driven typed controls, Default vs Engineering behavior, validation/mixed-value behavior and property set/remove intents.

### DEV 3 — Object Palette / Binding Foundation

- branch: `feature/graphical-editor-wave-08-palette-bindings`;
- authorization/seed head: `df3cd6c332a19bb3011373c95d010f33754c0c12`;
- before worker implementation, exactly one commit ahead of ContractSHA containing only `visualEditorContracts.ts`;
- status: **ACTIVE — AUTHORIZED**;
- owns only `visual-editor/object-palette/**`, `visual-editor/binding-editor/**` and focused tests;
- deliver registered `core.*` palette, object-add intents, canonical binding authoring foundation, source-catalog boundary, binding destination validation and Image palette entry consuming existing `assetRef`.

Workers read current-main coordination first, then the seeded `visualEditorContracts.ts` from their own branch. They must not create competing intent types, edit reserved files, broaden scope, merge themselves or work around coordinator-owned dependencies. After delivery each returns to `WAIT_FOR_COORDINATOR`.

## Coordinator work while workers execute

Coordinator may continue only coordinator-owned integration work, especially:

- canonical application of `VisualEditorMutationIntent` into immutable Screen drafts;
- source-catalog composition for DEV 3 without direct driver authority;
- central selection/viewport orchestration boundaries;
- cross-slice composition and tests;
- Early Contract / Integration / Delivery reviews;
- integrating accepted worker heads into `integration/graphical-editor-wave-08`.

Do **not** implement Canvas gestures, Property Inspector controls or Palette/Binding UI inside coordinator scope while workers own them.

## Actions discipline

Actions is available, but minutes remain a project resource:

- workers batch coherent changes and run focused assignment validation;
- open/update Draft PRs when they provide review value, not per trivial commit;
- do not rerun unchanged heads for reassurance;
- diagnose/fix deterministic failures before another expensive run;
- coordinator reuses CI #515 while central validated product code is unchanged;
- full matrices are reserved for meaningful integrated/final checkpoints;
- final Wave 08 head still requires full Web/backend/tests/smoke/Chromium acceptance before merge.

## Remaining Wave 08 product work

- DEV 1 interactive Canvas slice;
- DEV 2 Property Inspector slice;
- DEV 3 Palette/Binding slice;
- coordinator reducer/application of worker mutation intents into canonical Screen definitions;
- practical object creation + geometry editing composition;
- image object flow using stable `assetRef`;
- canonical property/binding composition;
- save/reopen/export/import of integrated authored Screen;
- proof transient Canvas selection/viewport/adornment state is not persisted;
- final exact-head integrated CI;
- PR #90 merge only after full green gate;
- post-merge `main` health and documentation synchronization.

Wave 09/10 remain forbidden/not active.

## Resume procedure

On every coordinator `siga`:

1. read mandatory current-main docs;
2. verify live main, PR #90, integration, worker branches/PRs and Actions;
3. reconcile stale documentation promptly;
4. review any new worker work before duplicating or integrating it;
5. keep worker ownership boundaries intact;
6. continue coordinator-only canonical integration seams;
7. integrate reviewed deliveries into the Wave 08 integration train;
8. use focused CI while assembling and one full matrix at a meaningful final checkpoint;
9. merge only green;
10. verify post-merge main, close Wave 08, then activate Wave 09.

## Wave 08 final product gate

`Create Screen -> add objects -> move/resize/rotate -> edit registered properties -> canonical binding -> image asset by stable assetRef -> save -> reopen -> export/import`

Transient selection/viewport/hover/adornment/drag-preview state must never become canonical persisted Engineering authority.
