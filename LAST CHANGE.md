# LAST CHANGE — EliteSCADA

> Operational handoff. Resume from GitHub, not chat history.

**Handoff date:** 2026-08-28  
**Development state:** **WAVE 08 ACTIVE — CENTRAL SCREEN EDITOR FOUNDATION IMPLEMENTED / NOT CI VALIDATED**  
**CI mode:** **NORMAL, but do not spend Actions until owner access/quota is available**  
**DEV workers:** **STOPPED / WAIT_FOR_COORDINATOR**

## Mandatory resume reading

Before any action read current `main`:
- `PROJECT GOAL.md`
- `LAST CHANGE.md`
- `docs/ROADMAP.md`
- `docs/PARALLEL-WORK.md`
- `docs/DEVELOPMENT-WAVES.md`
- `docs/CHAT-WORK-ASSIGNMENTS.md`
- `docs/CI-USAGE-POLICY.md`
- `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`
- `docs/COORDINATOR-HANDOFF.md`
- current assignment `MustReadSpecific`.

GitHub branch/PR/head/CI is operational truth.

## Wave 07 — CLOSED

Wave 07 Visual Runtime Object Model is fully merged and validated.

- Logical WaveBaseSHA: `cc79713434c1d7b5988158b843b137eaf488d923`
- Final integration product head: `6d869109af23b25d1ae95cd35610e1930a16791c`
- Final exact-head CI: #508 / run `33217787482` — **SUCCESS**
- Merge PR: #89
- Main merge: `8de706882ba20afedd666532ac41ae11115d06b3`
- Post-merge CI: #510 / run `33218282760` — **SUCCESS**

Historical/final contract: `docs/VISUAL-RUNTIME-WAVE-07-IMPLEMENTATION-DECISION.md`.

## Wave 08 — ACTIVE

Frozen contract:

- Logical WaveBaseSHA: `8de706882ba20afedd666532ac41ae11115d06b3`
- ContractSHA / branch base: `7a445d3dd94cabd09807291a0ee94276559fcb0e`
- Integration: `integration/graphical-editor-wave-08`
- Latest coordinator implementation-contract checkpoint before this documentation sync: `d49182474e3275e798b2a1434c48466d1b846ac1`
- Integration has been reconciled with `main` and was verified **0 commits behind** after merge commit `011d3c649bb868bfcf8d13bfafe896bb0863a153`.
- No Wave 08 Actions validation yet.
- No Wave 08 integration PR yet.

Contract: `docs/GRAPHICAL-EDITOR-WAVE-08-IMPLEMENTATION-DECISION.md`.  
Asset persistence contract: `docs/VISUAL-ASSET-STORAGE-WAVE-08.md`.  
Detailed checkpoint: `docs/COORDINATOR-HANDOFF.md`.

## Worker state

Owner explicitly stopped the DEV chats. Do not infer background work.

Worker branches remain at the Wave 08 contract base unless GitHub proves otherwise:

- DEV 1 `feature/graphical-editor-wave-08-canvas` — STOPPED / Canvas + selection slice;
- DEV 2 `feature/graphical-editor-wave-08-property-inspector` — STOPPED / Property Inspector slice;
- DEV 3 `feature/graphical-editor-wave-08-palette-bindings` — STOPPED / Object Palette + binding slice.

Restart workers only deliberately. Their preserved assignments are not authorization to execute.

## Coordinator work implemented on integration

### First-class Visual Assets / Schema v13

Implemented before this checkpoint:

- Engineering Schema v13 first-class Visual Asset metadata;
- stable Visual Asset `Id` as canonical project/reference identity;
- content-addressed SHA-256 Working payload registry;
- shared Working registry through Engineering Exchange, persistence, package, View validation and API;
- PNG/JPEG/BMP bounded structural validation;
- v13 `core.image.assetRef` reference integrity;
- same-Key/different-Id identity conflict rejection;
- PostgreSQL asset blobs + immutable revision links;
- asset-aware revision save/load/Preview/Apply/checkout/rollback;
- `.escadapkg` v2 asset sidecars with v1 asset-free compatibility;
- protected Visual Asset API with Engineering CAS/authorization/Audit;
- frontend asset catalog/import/content seams;
- focused Core/PostgreSQL source tests and v11/v12 compatibility coverage.

### Central Screen editor foundation

Now also implemented on the integration branch:

- `EngineeringApp` routes `Telas` into a dedicated `VisualEditorWorkspace` instead of a read-only table;
- Screen name/key/route drafts are edits of canonical `ScreenEngineering`, not private Canvas persistence;
- candidate generation clones the public Engineering package and replaces only the selected Screen draft;
- Preview uses the public Engineering Preview path and verifies that Workspace `changeVersion` stayed stable while validating;
- Apply uses the already protected public Engineering Apply path with expected Workspace version/CAS and therefore retains backend authorization/Audit semantics;
- successful Apply reloads the canonical Engineering snapshot;
- central 3-surface editor composition exists for Object Palette / Canvas / Property Inspector integration without absorbing DEV 1/2/3 scopes;
- canonical renderer consumes the shared `core.*` Visual Property Registry/defaults plus explicit Engineering values;
- `core.image` resolves project image content through stable `assetRef`;
- old demo visual types (`tank`, `dynamo`, `value`, etc.) render as non-authoritative compatibility placeholders rather than being silently converted into a competing model;
- Screen mutation helpers include stable Screen identity, canonical package replacement, visual-tree update-by-object-ID and recursive object counting;
- `visualEditorContracts.ts` now defines the shared worker/coordinator boundary: UI-only selection/viewport intents are distinct from canonical object/property/binding mutation intents, and Canvas/Inspector/Palette/Binding component prop contracts are explicit;
- PT-BR/EN/ES editor copy is present in the central composition;
- new Playwright coverage `visual-editor-workspace.spec.ts` exercises Screen route edit -> Preview -> Apply -> canonical export verification -> UI reopen -> restore original package and asserts the seeded visual preview opens without renderer errors.

Important: Canvas gestures, Property Inspector typed editing and Object Palette/binding authoring are still intentionally worker-owned and not implemented by this coordinator pass.

## Review defects corrected during this pass

In addition to earlier asset defects, this pass found and corrected:

1. integration was 8 commits behind `main`; all were coordination/documentation-only and were merged into the integration branch without product-code conflict;
2. a newly applied Screen can receive a backend GUID, so selection matching now accepts canonical ID or key identity during refresh;
3. the demo seed still contains historical visual object types while the new registry uses `core.*`; the editor now renders explicit compatibility placeholders instead of presenting them as malformed new-registry objects;
4. worker deliverables referenced generic “intents” but had no single typed integration boundary; `visualEditorContracts.ts` now freezes that coordinator-owned seam before worker restart.

## Validation boundary

Wave 08 is **not CI validated, not merge-ready and not complete**.

Evidence currently available:

- GitHub static/source review of the current integration changes;
- integration reconciled to current `main` at the checkpoint above;
- focused source tests have been added but not executed by GitHub Actions;
- no Wave 08 PR has been opened, intentionally avoiding an automatic Actions run while owner Actions access/quota is unavailable.

Still pending:

- exact-head Web TypeScript/Vite build;
- backend Release build/full tests and Runtime smoke;
- execution of the new Screen editor Playwright test and existing browser regression suite;
- DEV 1 Canvas/selection implementation;
- DEV 2 Property Inspector implementation;
- DEV 3 Object Palette/binding implementation;
- cross-slice integration and full Wave 08 product gate;
- final PR, exact-head CI, merge and post-merge health confirmation.

Wave 09/10 remain NOT ACTIVE.

## Next coordinator execution

1. reread mandatory current `main` documents and this handoff;
2. verify real `main`, integration, worker heads, PRs and Actions;
3. preserve workers as STOPPED unless deliberately restarted;
4. continue from `integration/graphical-editor-wave-08` and verify checkpoint `d49182474e3275e798b2a1434c48466d1b846ac1` or its documented successor;
5. make every restarted worker consume `web/scada-web/src/engineering/visual-editor/visualEditorContracts.ts` rather than inventing a competing intent format;
6. when Actions access is available, run the cheapest meaningful exact-head validation first; opening the integration PR is acceptable once the branch is ready because PR-to-main triggers CI;
7. fix build/test failures before expanding scope;
8. restart the three worker slices only when useful, without changing their fixed ownership boundaries;
9. integrate worker deliveries through the Wave 08 integration branch, run focused tests and then the full exact-head CI matrix;
10. merge only fully green, confirm post-merge `main`, close Wave 08, then and only then activate Wave 09.

## Wave 08 product gate

The integrated wave must prove:

`Create Screen -> add objects -> move/resize/rotate -> edit registered properties -> canonical binding -> image asset by stable assetRef -> save -> reopen -> export/import`

Transient Canvas selection/viewport/adornment state must never become persisted project authority.
