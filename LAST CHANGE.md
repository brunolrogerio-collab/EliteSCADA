# LAST CHANGE — EliteSCADA

> Operational handoff for the Wave 08 integration train. Always read current `main` coordination first; live GitHub state wins.

**Handoff date:** 2026-08-28  
**Wave:** **WAVE 08 ACTIVE — CENTRAL FOUNDATION CI GREEN / WORKERS ACTIVE / CANONICAL INTENT REDUCER IMPLEMENTED, CI DEFERRED**  
**CI mode:** **NORMAL — Actions authorized with conservative usage**

## Current operational state

- official merged product remains Wave 07; Wave 08 is **IMPLEMENTED IN DRAFT PR / NOT MERGED**;
- integration branch: `integration/graphical-editor-wave-08`;
- Draft PR: **#90**;
- exact central-foundation product head validated: **`b48b489660ae953029fd2416aa18b149eaa18258`**;
- CI #515 / run `33225051402`: **SUCCESS**;
- current coordinator code/test checkpoint before this documentation successor: **`9c4cecf55f55a9e55d049646ce854872467c02bc`**;
- commits after `b48b489...` that change product/test code are intentionally **not covered by CI #515**;
- no full matrix was triggered for the new intent reducer because DEV 1/2/3 were just activated and a complete run would immediately become stale during worker integration.

Current `main` coordination authorizes all three worker slices. Do not use the older STOPPED text from historical integration commits.

## Mandatory resume

Read current `main` first:
- `PROJECT GOAL.md`
- `LAST CHANGE.md`
- `docs/ROADMAP.md`
- `docs/PARALLEL-WORK.md`
- `docs/DEVELOPMENT-WAVES.md`
- `docs/CHAT-WORK-ASSIGNMENTS.md`
- `docs/CI-USAGE-POLICY.md`
- `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`
- `docs/COORDINATOR-HANDOFF.md`
- current MustReadSpecific.

Then verify PR #90, integration/worker heads and CI.

## Wave 07 — CLOSED

- main merge `8de706882ba20afedd666532ac41ae11115d06b3`;
- final integration CI #508 green;
- post-merge CI #510 green.

## Wave 08 frozen identity

- Logical WaveBaseSHA: `8de706882ba20afedd666532ac41ae11115d06b3`;
- ContractSHA / logical worker base: `7a445d3dd94cabd09807291a0ee94276559fcb0e`;
- integration: `integration/graphical-editor-wave-08`;
- Draft PR #90 remains the integration train;
- Wave 09/10 remain NOT ACTIVE.

## Validated central foundation — CI #515

CI #515 on exact product head `b48b489...` proved:

- Web React/Vite/TypeScript build;
- backend Release build;
- full backend tests including PostgreSQL/Timescale;
- Runtime smoke;
- Chromium E2E;
- Screen editor Preview -> Apply -> canonical export -> reopen coverage;
- prior Wave 06/07 Python/visual regressions.

Validated central capabilities in that checkpoint include:

- Engineering Schema v13 first-class Visual Assets;
- stable Asset ID / SHA-256 payload identity;
- bounded PNG/JPEG/BMP validation;
- canonical Working asset authority;
- PostgreSQL asset revision blobs/links;
- `.escadapkg` v2 sidecars with v1 compatibility;
- protected Visual Asset CAS/authorization/Audit API;
- central `Engineering -> Telas` `VisualEditorWorkspace`;
- canonical Screen Preview/Apply/CAS/reload;
- shared `core.*` renderer and stable `assetRef`;
- legacy visual compatibility placeholders;
- shared `visualEditorContracts.ts` worker/coordinator vocabulary.

## New coordinator-owned integration seam after CI #515

Checkpoint `9c4cecf55f55a9e55d049646ce854872467c02bc` adds the coordinator-owned canonical consumer for the shared worker intents plus focused pure tests.

`applyVisualEditorMutationIntent(...)` now handles:

- `object.add` with coordinator-minted stable ID/key and registered built-in type validation;
- `object.move` using registry defaults only as a starting value when interaction makes x/y explicit;
- `object.resize` and `object.rotate` with registry validation;
- hierarchy-aware `object.duplicate` with new IDs/keys and no duplicate child action when an ancestor is selected;
- `object.delete`;
- `object.zOrder` through explicit canonical `zIndex` interaction results;
- `property.set` / `property.remove` only through the shared schema and Engineering editability;
- `binding.set` / `binding.remove` only for registered binding-capable destinations;
- fail-closed missing IDs, unknown types, invalid values and invalid binding destinations.

Important authority rule: renderer/schema defaults are not materialized merely by viewing/selection. They become explicit Engineering values only when an edit operation needs to change that property.

Focused test file:

- `web/scada-web/tests-e2e/visual-editor-canonical-model.spec.ts`

It covers add/default separation, geometry mutation, property/binding authority, hierarchy duplicate/delete and z-order behavior. These new reducer tests are **written but not yet executed by Actions**.

## Workers — ACTIVE / AUTHORIZED

Current `main` execution board deliberately reactivated all three workers after CI #515 and after the shared contract seed was placed in each branch.

- DEV 1 `feature/graphical-editor-wave-08-canvas` authorization/seed head `57521312914e21e303976a81bc81c84ad5aa9cbb` — Canvas / Selection;
- DEV 2 `feature/graphical-editor-wave-08-property-inspector` authorization/seed head `3e942ed641d96afe848966f123fb10eaeaa99ed7` — Property Inspector;
- DEV 3 `feature/graphical-editor-wave-08-palette-bindings` authorization/seed head `df3cd6c332a19bb3011373c95d010f33754c0c12` — Object Palette / Binding Foundation.

At authorization each branch was exactly one coordinator dependency-seed commit ahead of ContractSHA and contained no worker implementation. Verify live heads because worker execution may have begun after this checkpoint.

Workers must consume their seeded `visualEditorContracts.ts`, remain inside fixed scopes, use focused validation and stop at `WAIT_FOR_COORDINATOR` after delivery.

## Actions discipline

- CI #515 remains valid evidence only for unchanged code at/before `b48b489...`;
- new reducer code/test is currently `CI_DEFERRED`, not validated;
- do not run a full matrix solely for this intermediate seam while active worker slices are expected to change the composition;
- static review and focused worker evidence come first;
- diagnose failures before rerun;
- next full matrix should be triggered at a meaningful integrated checkpoint after one or more worker slices are composed;
- final exact Wave 08 head still requires the complete matrix before merge.

## Still pending

- DEV 1 Canvas slice;
- DEV 2 Property Inspector slice;
- DEV 3 Palette/Binding slice;
- wire delivered worker components to the central `VisualEditorWorkspace` using the reducer and transient UI-state boundary;
- coordinator-provided canonical binding source catalog;
- practical Image asset selection/import flow through stable `assetRef`;
- integrated create/add/move/resize/rotate/property/binding/image/save/reopen/export/import gate;
- proof selection/viewport/hover/adornment/drag preview never persist;
- full exact-head CI;
- PR #90 merge and post-merge main health.

## Next coordinator actions

1. reread/verify current-main coordination and live GitHub state;
2. inspect each worker branch/PR for Early Contract or Delivery Review evidence;
3. do not duplicate worker UI scope;
4. review the reducer diff/tests before any semantic expansion;
5. integrate reviewed worker deliveries into this branch;
6. connect transient UI intents in central composition only after worker contracts are concrete;
7. use focused validation while composing;
8. run the next complete CI matrix at a meaningful integrated checkpoint;
9. merge only fully green and confirm post-merge `main` before Wave 09.

## Final Wave 08 gate

`Create Screen -> add objects -> move/resize/rotate -> edit registered properties -> canonical binding -> image asset by stable assetRef -> save -> reopen -> export/import`

Transient Canvas selection/viewport/adornment state must never become persisted project authority.
