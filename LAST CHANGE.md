# LAST CHANGE — EliteSCADA

**Date:** 2026-09-23 BRT  
**Operational state:** **WAVE 15 ACTIVE / FND-04 VERIFIED+FROZEN / FND-06 ACTIVE / FC0-A BLOCKED ONLY ON FND-06**

## Latest verified product checkpoint

Exact frozen Foundation product checkpoint:

`6c810647c9773a19b212d9c33694780141786ac7`

tree:

`1221ff55963052be4e924dd644efbaa65763f546`

This checkpoint includes FND-03, INFRA-CI-01A and FND-04. Exact FND-04 post-merge EliteSCADA CI #1562 / run `35913456486` completed SUCCESS:
- Web `107358858133` — SUCCESS;
- Backend build/test/smoke `107358858405` — SUCCESS;
- Chromium E2E `107359503423` — SUCCESS.

FND-04 is **VERIFIED/FROZEN**. Its downstream Script TAG reference contract may be consumed but not redefined.

## Current active work

FND-06 is the only active FC0-A blocker.

- order: `FND06-CODEX-MOUNTED-LEGACY-CLOSE-V3`
- exact product base: `6c810647c9773a19b212d9c33694780141786ac7`
- work branch: `work/w15-fnd-06-visual-stability-foundation`
- dedicated control: `coord/w15-fnd06-control:docs/WAVE15-FND06-CONTROL.md`
- active control commit: `35e1ae631b8471a66eb0c4042295d5b5628d61ec`
- work branch remains untouched at the exact base and no candidate/PR exists yet.

The frozen FND-06 plan covers centralized known-legacy visual compatibility, Screen/Popup selection stability, canonical renderer/public-model authority, Runtime navigation persistence and Working-vs-Active separation. Full single-canvas WYSIWYG remains downstream DEV-EDITOR scope.

## FC0-A preparation

FC0-A remains **BLOCKED only on FND-06 VERIFIED/FROZEN**.

Prepared downstream release file:
`coord/w15-fnd06-control:docs/WAVE15-FC0-A-RELEASE-PREP.md`

Latest prep commit:
`dfae2377f0c6802da726650697040181c7b0f453`

Reserved, not-yet-active orders:
- `DEV-EDITOR-FC0A-01`
- `DEV-SCRIPT-ENGINEERING-FC0A-01`
- `DEV-AUTHORITY-UX-FC0A-01`
- `DEV-LICENSING-UX-FC0A-01`

No downstream product branch is created before Main records the final exact FC0-A SHA/tree.

## Later Foundation preparation

Read-only/source-audit preparation was completed without product mutation:

- FND-05 HA authority control:
  - branch `coord/w15-fnd05-control`
  - file `docs/WAVE15-FND05-CONTROL.md`
  - prep commit `66d0f0e53a9842c719cfe28ef40da3b7fbdd1f6f`
  - state `PREPARED / NOT ACTIVE`
- FND-07 installation detach control:
  - branch `coord/w15-fnd07-control`
  - file `docs/WAVE15-FND07-CONTROL.md`
  - prep commit `760e1cb57a1bdb1e146a6326bd55f713c7790add`
  - state `PREPARED / NOT ACTIVE`

Neither later Foundation is authorized to mutate product while FND-06 owns the sequential high-risk Foundation executor.

## Foundation current Main gate

- FND-04: **VERIFIED/FROZEN**
  - exact product checkpoint: `6c810647c9773a19b212d9c33694780141786ac7`
  - tree: `1221ff55963052be4e924dd644efbaa65763f546`
  - post-merge EliteSCADA CI `35913456486`: SUCCESS
  - Web / Backend+tests+smoke / Chromium: all SUCCESS
  - control freeze commit: `0453428521b28e951aa8e9d01742ee58b09f6690`
- FND-06: **ACTIVE / NOT INTEGRATED**
  - exact product base: `6c810647c9773a19b212d9c33694780141786ac7`
  - work branch: `work/w15-fnd-06-visual-stability-foundation`
  - control branch/file: `coord/w15-fnd06-control:docs/WAVE15-FND06-CONTROL.md`
  - active control commit: `35e1ae631b8471a66eb0c4042295d5b5628d61ec`
  - order: `FND06-CODEX-MOUNTED-LEGACY-CLOSE-V3`
- FC0-A: **BLOCKED only on FND-06**
- downstream release package is prepared but not released at `docs/WAVE15-FC0-A-RELEASE-PREP.md` on the FND-06 control branch.



## FND-06 execution metadata correction

Before first product mutation, Main corrected the prepared FND-06 validation profile to the actual Wave 15 router vocabulary: `UI_EDITOR, RUNTIME_RENDERER`. The exact product seed also proves bare `status` is a persisted legacy identifier alongside `tank`, `value` and `dynamo`; it is not a current canonical `core.*` built-in and no guessed alias is authorized.

Active FND-06 order: `FND06-CODEX-MOUNTED-LEGACY-CLOSE-V3`.
Control commit: `35e1ae631b8471a66eb0c4042295d5b5628d61ec`.


## FC0-A collision guard

The prepared downstream release package now includes a Main-owned parallel-file collision map. Primary ownership is separated across Editor (`engineering/visual-editor/**`), Script Engineering (`engineering/scripts/**` + `python-editor/**`), Authority UX (`UserAdministration*`) and Licensing UX (`web/licensing/**` + `Scada.LicenseGenerator/**`). Shared shell/router/types/i18n/CI files are Main-coordinated hotspots, not free-for-all lane ownership.

Latest FC0-A prep commit: `dfae2377f0c6802da726650697040181c7b0f453`.


## Coordinator correction — shared CODEX routing and FC0-A audit

A stale FND-04 CODEX `WAIT` order could cause the reused sequential CODEX chat to stop even though FND-06 was active.

Main corrected this at the source:
- FND-04 control rev 0016: `ROUTE-SEQUENTIAL-CODEX-TO-FND06-12`
- commit: `cf19a9b0ce7efbf25d3d85acc2076a8c48148a9d`
- same sequential CODEX chat/lane is explicitly the FND-06 executor
- FND-06 control rev 0004: `FND06-CODEX-MOUNTED-LEGACY-CLOSE-V3`
- commit: `1a1488388fd67bf89380879b07437e1460170f18`

FC0-A sequencing was also tightened:
- FND-06 freeze no longer releases FC0-A directly;
- mandatory gate: `FC0A-POST-FND06-W15-FOUNDATION-AUDIT-01`;
- audit control created at `coord/w15-fnd06-control:docs/WAVE15-FC0A-POST-FND06-AUDIT-CONTROL.md`, commit `0689a53b6cdcb569dd6c65a8326009dfcf2da9de`;
- FC0-A release prep updated at `fee82e7dd8eed8cd237e067317fedbe48d64cb97`;
- FND-05 control rev 0002 / `b995435594f9031a33df5674a0207016405a41d7`;
- FND-07 control rev 0002 / `503a89d2985db64c1d6666e40d1de06251027687`.

After FND-06, audit must relate Wave 15 implementation to final Wave 14 diagnostics, product premises/gaps and frozen contracts. It must also prove FND-05/FND-07 are non-breaking to FC0-A DEV-consumed contracts.

Only audit PASS releases the four FC0-A DEVs and permits FND-05/FND-07 to activate in parallel.


## FND-06 Main review — mounted legacy closeout

Current PR #337 candidate:
- head `923543705378016090e7067b35954795a9591a57`
- tree `5657cee7169a4e77370d416add4efcf07184d7c0`
- natural T1 `35931139983` — SUCCESS
- 9 changed files, all inside FND-06 allowlist
- architecture/scope accepted by Main.

Remaining gate before merge:
- original Wave 14 A7 was a mounted Screen/Popup selection crash that blanked/poisoned Engineering;
- candidate currently proves model/helper compatibility but lacks the required mounted Screen + Popup persisted-legacy selection regression;
- active order: `FND06-CODEX-MOUNTED-LEGACY-CLOSE-V3`;
- control revision `0005`;
- control commit `48778387c2fde6ffb645ced17669bd0606d30142`;
- preferred delta: tests only; minimal production fix only if the mounted scenario exposes a remaining defect.

The same sequential CODEX lane remains the executor. No merge/freeze yet.
