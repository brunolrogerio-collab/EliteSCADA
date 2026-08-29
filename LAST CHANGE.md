# LAST CHANGE — EliteSCADA

> Operational handoff. Resume from GitHub, not chat history.

**Handoff date:** 2026-08-28  
**Development state:** **WAVE 08 ACTIVE — CENTRAL FOUNDATION CI GREEN / INTERACTIVE WORKER SLICES STILL PENDING**  
**CI mode:** **NORMAL — Actions authorized with conservative usage**  
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

Then verify live GitHub branch/PR/head/CI. GitHub is operational truth.

## Wave 07 — CLOSED

- final integration product head: `6d869109af23b25d1ae95cd35610e1930a16791c`
- exact-head CI #508 / run `33217787482`: **SUCCESS**
- merge PR #89
- main merge: `8de706882ba20afedd666532ac41ae11115d06b3`
- post-merge CI #510 / run `33218282760`: **SUCCESS**

## Wave 08 — ACTIVE

Frozen identity:

- Logical WaveBaseSHA: `8de706882ba20afedd666532ac41ae11115d06b3`
- ContractSHA / branch base: `7a445d3dd94cabd09807291a0ee94276559fcb0e`
- Integration: `integration/graphical-editor-wave-08`
- integration reconciliation merge with current `main`: `011d3c649bb868bfcf8d13bfafe896bb0863a153`
- Draft integration PR: **#90 — `Wave 08: graphical editor foundation integration`**
- exact validated product head: **`b48b489660ae953029fd2416aa18b149eaa18258`**
- validation: **CI #515 / run `33225051402` — SUCCESS**

CI #515 proved on that exact product head:

- Web React/Vite/TypeScript build: **SUCCESS**;
- backend Release build: **SUCCESS**;
- full backend tests including PostgreSQL/Timescale coverage: **SUCCESS**;
- Runtime smoke: **SUCCESS**;
- Chromium E2E: **SUCCESS**;
- new `visual-editor-workspace.spec.ts`: **SUCCESS**;
- prior Wave 06/07 Python/visual regressions executed inside the browser matrix remained green.

Documentation-only successors after `b48b489...` do **not** invalidate that unchanged product evidence. They use `[skip ci]` deliberately to avoid spending Actions on handoff synchronization.

## CI defects found and corrected before #515

The conservative checkpoint found real deterministic defects instead of wasting reruns:

1. TypeScript readonly/mutable mismatch in `visualEditorCanonicalModel.ts`; fixed by cloning a mutable elements array.
2. C# records `EngineeringRevisionAssetPayload` and `VisualAssetPayload` declared a forbidden `Clone` method; helpers renamed to `Copy` and call sites updated.
3. broad Runtime E2E still expected `.escadapkg` format v1, while Wave 08 intentionally emits current format v2; expectation updated to v2. Historical v1 compatibility remains separately tested.

No unchanged-head reassurance reruns were used.

## Coordinator foundation now validated

The integration branch contains and CI #515 validates the current central Wave 08 foundation:

- Engineering Schema v13 first-class Visual Asset metadata;
- stable Visual Asset `Id` reference identity separated from developer `Key` and content SHA-256;
- bounded PNG/JPEG/BMP validation;
- Working asset registry shared through canonical Engineering authority;
- PostgreSQL asset blobs + immutable revision links;
- asset-aware revision save/load/checkout/rollback;
- `.escadapkg` v2 asset sidecars with v1 compatibility;
- protected Visual Asset API with CAS/authorization/Audit;
- frontend asset catalog/import/content seams;
- `Engineering -> Telas` central `VisualEditorWorkspace`;
- Screen draft Preview/Apply through public canonical Engineering + Workspace CAS;
- canonical snapshot reload after Apply;
- renderer consuming the shared `core.*` Visual Property Registry and stable `assetRef`;
- explicit non-authoritative compatibility placeholders for historical demo visual types;
- `visualEditorCanonicalModel.ts` mutation helpers;
- shared `visualEditorContracts.ts` separating transient UI state from canonical object/property/binding mutation intents;
- PT-BR/EN/ES editor composition;
- Screen Preview -> Apply -> canonical export -> reopen E2E.

This validation does **not** close Wave 08. PR #90 remains Draft and must not be merged yet.

## Worker state

Owner explicitly stopped the DEV chats. Keep them stopped unless deliberately restarted.

Last verified worker branches remain at contract base unless GitHub proves otherwise:

- DEV 1 `feature/graphical-editor-wave-08-canvas` — Canvas / Selection;
- DEV 2 `feature/graphical-editor-wave-08-property-inspector` — Property Inspector;
- DEV 3 `feature/graphical-editor-wave-08-palette-bindings` — Object Palette / Binding.

When restarted, all three must consume coordinator-owned `web/scada-web/src/engineering/visual-editor/visualEditorContracts.ts`; they must not invent competing shared intent contracts.

## Actions rule

Actions is explicitly authorized, but use remains conservative:

- static/diff/contract review first when sufficient;
- focused evidence before full matrix when practical;
- batch coherent changes;
- never rerun unchanged heads for reassurance;
- diagnose/fix a localized failure before another expensive run;
- use full PR/integration matrices at meaningful checkpoints;
- never weaken final assertions or the Wave Definition of Done to save minutes.

## Still pending for Wave 08

- DEV 1 Canvas/selection, zoom/pan/grid/snap and move/resize/rotate/duplicate/delete/z-order intents;
- DEV 2 schema-driven typed Property Inspector;
- DEV 3 registered Object Palette + canonical Binding authoring;
- coordinator integration of those intents into canonical Screen drafts;
- practical Image selection/import workflow through stable `assetRef` in the integrated editor;
- complete Wave 08 product gate;
- final exact-head CI after worker integration;
- PR #90 readiness/merge only after the full wave is green;
- post-merge `main` health confirmation.

Wave 09/10 remain NOT ACTIVE.

## Next coordinator execution

1. verify live `main`, PR #90, integration and worker heads before changes;
2. reuse CI #515 evidence for unchanged central foundation rather than rerunning it;
3. preserve workers as STOPPED until deliberately restarted;
4. when restarting them, preserve fixed scopes and require `visualEditorContracts.ts` integration;
5. review worker deliveries before integrating;
6. integrate through `integration/graphical-editor-wave-08` only;
7. use focused validation during integration and reserve the next complete matrix for a meaningful product checkpoint;
8. prove the full Wave 08 gate;
9. merge only after final exact integrated head is green, then confirm post-merge `main` before activating Wave 09.

## Wave 08 product gate

`Create Screen -> add objects -> move/resize/rotate -> edit registered properties -> canonical binding -> image asset by stable assetRef -> save -> reopen -> export/import`

Transient Canvas selection/viewport/adornment state must never become persisted project authority.
