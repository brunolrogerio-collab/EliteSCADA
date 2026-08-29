# LAST CHANGE — EliteSCADA

> Operational handoff. Resume from GitHub, not chat history.

**Handoff date:** 2026-08-28  
**Development state:** **WAVE 08 ACTIVE — PARTIAL COORDINATOR FOUNDATION / NOT CI VALIDATED**  
**CI mode:** **NORMAL**  
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

Post-merge #510 proved Web build, backend Release/full PostgreSQL+Timescale tests, Runtime smoke and Chromium all green on the actual merge commit.

Historical/final contract: `docs/VISUAL-RUNTIME-WAVE-07-IMPLEMENTATION-DECISION.md`.

## Wave 08 — ACTIVE

Wave 08 contract remains frozen:

- Logical WaveBaseSHA: `8de706882ba20afedd666532ac41ae11115d06b3`
- ContractSHA / branch base: `7a445d3dd94cabd09807291a0ee94276559fcb0e`
- Integration: `integration/graphical-editor-wave-08`
- Current exact integration head at this handoff: `4f8a71f59c5d033265c38c1c0beca2add9bb117b`
- No Actions run yet for the Wave 08 integration branch.
- No Wave 08 integration PR yet.

Contract: `docs/GRAPHICAL-EDITOR-WAVE-08-IMPLEMENTATION-DECISION.md`.
Asset persistence contract: `docs/VISUAL-ASSET-STORAGE-WAVE-08.md`.
Detailed current coordinator checkpoint: `docs/COORDINATOR-HANDOFF.md`.

## Worker state

Owner explicitly stopped the DEV chats.

All three worker branches remain untouched at the Wave 08 contract base `7a445d3dd94cabd09807291a0ee94276559fcb0e`:

- DEV 1 `feature/graphical-editor-wave-08-canvas` — STOPPED / no implementation commits;
- DEV 2 `feature/graphical-editor-wave-08-property-inspector` — STOPPED / no implementation commits;
- DEV 3 `feature/graphical-editor-wave-08-palette-bindings` — STOPPED / no implementation commits.

Do not assume any background worker activity. Restart them only on explicit owner/coordinator authorization.

## Coordinator work already implemented on integration

The coordinator-only Wave 08 foundation now includes:

- Engineering Schema v13 first-class Visual Asset metadata;
- stable Visual Asset `Id` as canonical project/reference identity;
- content-addressed SHA-256 Working payload registry;
- shared Working registry composition through Engineering Exchange, persistence, package, View validation and API;
- PNG/JPEG/BMP structural validation and bounded import safety;
- v13 `core.image.assetRef` prospective reference integrity;
- same-Key/different-Id identity conflict rejection;
- PostgreSQL asset blobs + immutable revision links;
- asset-aware revision save/load/Preview/Apply/checkout/rollback;
- `.escadapkg` v2 asset sidecars with v1 asset-free compatibility;
- project-controlled Visual Asset API with Engineering CAS/authorization/Audit;
- frontend Visual Asset metadata/catalog/import/content seams;
- focused Core/PostgreSQL tests for raster, package, identity and revision behavior;
- compatibility adjustments proving v11/v12 remain historical input while current Engineering is v13.

During static review, several concrete defects were found and corrected before CI, including an invalid C# `EndsWith` overload, checkout asset-state leakage/missing payload restore, package-side malformed-raster bypass and Visual Asset key/identity substitution risk.

See `docs/COORDINATOR-HANDOFF.md` for the detailed list and exact commits.

## Important status boundary

Wave 08 is **not validated, not merge-ready and not complete**.

Still pending:

- exact-head build/test evidence for current integration;
- reconciliation of integration with current `main` before final CI/PR;
- canonical Screen graphical mutation/save/reopen seam;
- central graphical editor workspace/route/renderer/localization composition;
- all three worker UI slices, because workers are currently stopped and branches untouched;
- integrated Wave 08 product gate;
- final exact-head CI matrix, merge and post-merge health confirmation.

Wave 09/10 remain NOT ACTIVE.

## Next coordinator execution

1. reread all mandatory `main` documents including `docs/COORDINATOR-HANDOFF.md`;
2. verify real `main`, integration, worker heads, PRs and Actions before changing code;
3. treat all DEV workers as STOPPED unless explicitly restarted;
4. resume from exact integration head `4f8a71f59c5d033265c38c1c0beca2add9bb117b` or its verified successor;
5. review build-risk and reconcile integration with current `main` before final CI/PR;
6. finish coordinator-owned Screen mutation/save/reopen and central editor composition seams;
7. deliberately restart worker chats only when useful, preserving their fixed scopes;
8. integrate delivered worker heads through the Wave 08 integration branch;
9. run focused tests, then the full exact-head CI matrix;
10. merge only fully green, confirm post-merge `main`, then close Wave 08 and only then activate Wave 09.

## Wave 08 product gate

The integrated wave must prove:

`Create Screen -> add objects -> move/resize/rotate -> edit registered properties -> canonical binding -> image asset by stable assetRef -> save -> reopen -> export/import`

Transient Canvas selection/viewport/adornment state must never become persisted project authority.
