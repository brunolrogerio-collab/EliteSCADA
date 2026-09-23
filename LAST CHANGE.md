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

- order: `FND06-CODEX-VISUAL-STABILITY-V2`
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
  - order: `FND06-CODEX-VISUAL-STABILITY-V2`
- FC0-A: **BLOCKED only on FND-06**
- downstream release package is prepared but not released at `docs/WAVE15-FC0-A-RELEASE-PREP.md` on the FND-06 control branch.



## FND-06 execution metadata correction

Before first product mutation, Main corrected the prepared FND-06 validation profile to the actual Wave 15 router vocabulary: `UI_EDITOR, RUNTIME_RENDERER`. The exact product seed also proves bare `status` is a persisted legacy identifier alongside `tank`, `value` and `dynamo`; it is not a current canonical `core.*` built-in and no guessed alias is authorized.

Active FND-06 order: `FND06-CODEX-VISUAL-STABILITY-V2`.
Control commit: `35e1ae631b8471a66eb0c4042295d5b5628d61ec`.


## FC0-A collision guard

The prepared downstream release package now includes a Main-owned parallel-file collision map. Primary ownership is separated across Editor (`engineering/visual-editor/**`), Script Engineering (`engineering/scripts/**` + `python-editor/**`), Authority UX (`UserAdministration*`) and Licensing UX (`web/licensing/**` + `Scada.LicenseGenerator/**`). Shared shell/router/types/i18n/CI files are Main-coordinated hotspots, not free-for-all lane ownership.

Latest FC0-A prep commit: `dfae2377f0c6802da726650697040181c7b0f453`.
