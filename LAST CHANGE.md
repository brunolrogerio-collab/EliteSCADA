# LAST CHANGE — EliteSCADA

> Operational handoff. Resume from GitHub, not chat history.

**Handoff date:** 2026-08-28  
**Development state:** **WAVE 07 COMPLETE / WAVE 08 GRAPHICAL EDITOR FOUNDATION ACTIVE**  
**CI mode:** **NORMAL**

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

PR #88 was the original Draft PR. It was closed unmerged only because the available connector failed to transition Draft -> Ready after validation. PR #89 replaced it on the exact same product head. No product byte changed between final CI #508 and the replacement PR.

### Important Wave 07 results now in `main`

- Engineering Schema **v12** with JSON-native typed built-in visual properties;
- explicit v10/v11 visual string-property compatibility/migration;
- stable nested visual object IDs;
- typed public Visual Property Registry and aligned `core.*` object schemas;
- canonical visual binding destination/source semantics;
- deterministic `Animation > Script > Binding/Expression > Engineering Base > Default` precedence;
- per-client Runtime Visual Instance isolation and disposal;
- canonical `assetRef = null | { assetId }` with path/URL rejection;
- Client Visual Python property read/write/explicit-clear without DOM/renderer authority;
- canonical nested `VisualObject` Script dependency/event integrity;
- fail-closed malformed visual/Script Preview paths;
- reproducible .NET SDK/Node/npm CI baseline;
- Timescale historian DDL coordinated with the shared EliteSCADA PostgreSQL schema advisory lock;
- duplicate definition bindings and duplicate Script event references fail closed.

Historical/final contract: `docs/VISUAL-RUNTIME-WAVE-07-IMPLEMENTATION-DECISION.md`.

## Wave 08 — ACTIVE

Wave 08 Definition of Ready is satisfied and the execution contract is frozen.

- Logical WaveBaseSHA: `8de706882ba20afedd666532ac41ae11115d06b3`
- Wave 08 ContractSHA / branch base: `7a445d3dd94cabd09807291a0ee94276559fcb0e`
- Integration: `integration/graphical-editor-wave-08`
- DEV 1: `feature/graphical-editor-wave-08-canvas`
- DEV 2: `feature/graphical-editor-wave-08-property-inspector`
- DEV 3: `feature/graphical-editor-wave-08-palette-bindings`

Contract: `docs/GRAPHICAL-EDITOR-WAVE-08-IMPLEMENTATION-DECISION.md`.

### Active assignments

**DEV 1 — Canvas / Selection**
- owns only `web/scada-web/src/engineering/visual-editor/canvas/**` and focused Canvas tests;
- delivers viewport, zoom/pan/grid/snap, selection/multiselect, move/resize/rotate and duplicate/delete/z-order intents;
- no persistence or central workspace changes.

**DEV 2 — Property Inspector**
- owns only `web/scada-web/src/engineering/visual-editor/property-inspector/**` and focused Inspector tests;
- consumes the shared Visual Property Registry;
- no competing property model or persistence calls.

**DEV 3 — Object Palette / Binding Foundation**
- owns `object-palette/**`, `binding-editor/**` and focused tests;
- consumes existing `core.*` schemas and canonical binding semantics;
- Image uses existing `assetRef` contract but DEV 3 does not own asset persistence/import authority.

**Coordinator**
- owns canonical Screen mutation/save/reopen integration;
- first-class project image asset entity/API/persistence/package authority;
- central visual editor workspace/routing/composition/renderer/localization;
- all reserved/shared files;
- integration, cross-slice acceptance, final PR/CI/merge.

Workers must read `docs/CHAT-WORK-ASSIGNMENTS.md` and the Wave 08 contract before acting, use only their branch/scope, report delivery head and STOP.

## Wave 08 product gate

The integrated wave must prove:

`Create Screen -> add objects -> move/resize/rotate -> edit registered properties -> canonical binding -> image asset by stable assetRef -> save -> reopen -> export/import`

Transient Canvas selection/viewport state must not become persisted project authority.

Final Wave 08 CI must preserve all Wave 07 visual/Python and Wave 06 sandbox/native-escape/cancellation regressions.

## Forbidden forward expansion

Wave 09/10 are not active. Do not implement production Popup/Dynamo/navigation semantics, Python event editor, production tween/animation preview, Server Python or new industrial protocols during Wave 08.

## Next coordinator execution

1. re-read mandatory `main` documents and verify all Wave 08 branch heads;
2. inspect canonical Screen/View and project-package persistence seams plus existing asset concepts;
3. establish coordinator-owned first-class project asset authority and Screen graphical mutation/save seam on `integration/graphical-editor-wave-08` without changing Wave 07 public semantics;
4. monitor worker deliveries only after they are explicitly executed in their fixed DEV chats;
5. integrate worker heads through the Wave 08 integration branch;
6. run focused static/cross-contract review before opening final integration PR;
7. execute the full exact-head CI matrix and fix root causes only;
8. merge only fully green and confirm healthy post-merge `main`;
9. activate Wave 09 only after Wave 08 is formally closed.
