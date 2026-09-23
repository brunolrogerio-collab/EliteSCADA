# Current Coordinator Handoff — Wave 15

> GitHub live is the authority. Canonical operational handoff: `docs/WAVE15-MAIN-COORDINATOR-HANDOFF.md`.

## Stable Foundation state

- Wave 15 — ACTIVE.
- FND-01 — VERIFIED/FROZEN.
- FND-02 incl. AUTH-04 — VERIFIED/FROZEN.
- FND-08 common timing/WAN contract — VERIFIED/FROZEN.
- FND-03 global — VERIFIED/FROZEN.
- INFRA-CI-01A — VERIFIED/FROZEN.
- FND-04 Script TAG Reference Resolution — VERIFIED/FROZEN.
  - exact product checkpoint: `6c810647c9773a19b212d9c33694780141786ac7`
  - tree: `1221ff55963052be4e924dd644efbaa65763f546`
  - exact post-merge EliteSCADA CI `35913456486` — SUCCESS
  - Web `107358858133`, Backend/test/smoke `107358858405`, Chromium `107359503423` — SUCCESS
  - frozen control commit: `0453428521b28e951aa8e9d01742ee58b09f6690`

## Current active mission

FND-06 is **ACTIVE / NOT INTEGRATED** and is the only remaining FC0-A blocker.

- order: `FND06-CODEX-VISUAL-STABILITY-V2`
- exact product base: `6c810647c9773a19b212d9c33694780141786ac7`
- base tree: `1221ff55963052be4e924dd644efbaa65763f546`
- work branch: `work/w15-fnd-06-visual-stability-foundation`
- target: `wave15/corrections-integration`
- control branch/file: `coord/w15-fnd06-control:docs/WAVE15-FND06-CONTROL.md`
- active control commit: `35e1ae631b8471a66eb0c4042295d5b5628d61ec`
- validation profile: `UI_EDITOR, RUNTIME_RENDERER`

Latest revalidation: work branch is still identical to the exact product base; no FND-06 PR/candidate/handoff exists yet.

Frozen FND-06 scope:
- centralized known-legacy visual compatibility before strict schema consumers;
- Screen/Popup selection stability;
- canonical renderer/public-model single authority;
- selected Screen + Popup-stack persistence across retryable projection failure;
- deliberate reset on real Active identity change;
- Working-design vs Active Runtime authority separation.

Full single-canvas WYSIWYG remains downstream DEV-EDITOR scope.

## FC0-A preparation

FC0-A state: **PREPARED / NOT RELEASED / BLOCKED ONLY ON FND-06**.

Prepared release file:
`coord/w15-fnd06-control:docs/WAVE15-FC0-A-RELEASE-PREP.md`

Latest prep commit:
`dfae2377f0c6802da726650697040181c7b0f453`

Reserved downstream orders:
- `DEV-EDITOR-FC0A-01`
- `DEV-SCRIPT-ENGINEERING-FC0A-01`
- `DEV-AUTHORITY-UX-FC0A-01`
- `DEV-LICENSING-UX-FC0A-01`

No downstream work branch is created until Main records the final exact `FC0_A_INTEGRATION_SHA` and tree after FND-06 freeze.

## Later Foundation controls — prepared only

### FND-05

- state: PREPARED / NOT ACTIVE
- reserved order: `FND05-CODEX-HA-AUTHORITY-V1`
- control: `coord/w15-fnd05-control:docs/WAVE15-FND05-CONTROL.md`
- prep commit: `66d0f0e53a9842c719cfe28ef40da3b7fbdd1f6f`

Prepared around one server-owned Cluster/Node/effective-Active/fencing contract, manual break-before-make transfer first, Runtime Session Lease continuity and no client/Driver election.

### FND-07

- state: PREPARED / NOT ACTIVE
- reserved order: `FND07-CODEX-DETACH-NEUTRAL-V1`
- control: `coord/w15-fnd07-control:docs/WAVE15-FND07-CONTROL.md`
- prep commit: `760e1cb57a1bdb1e146a6326bd55f713c7790add`

Prepared around secure populated-install Application+Authority detach, Runtime/process-effect fencing, old-session invalidation, neutral bootstrap, explicit license keep/remove/replace and no silent Historian deletion.

Neither later Foundation is authorized to mutate product while FND-06 owns the sequential high-risk Foundation executor.

## Current guards

- product changes only through isolated branch/PR; no direct feature writes to integration/main;
- exact-SHA evidence and natural Wave 15 T1 required;
- red CI diagnosed before rerun;
- downstream lanes consume frozen Foundation contracts and stop with `BLOCKED-CONTRACT` if a contract change is required;
- no force push/destructive rebase/evidence deletion;
- no Product Owner message relay between agents; agents read GitHub live control/ledger.

Primary ledger: Issue #305.


## FND-06 corrected execution metadata

- active order: `FND06-CODEX-VISUAL-STABILITY-V2`
- validation profile: `UI_EDITOR, RUNTIME_RENDERER`
- known legacy set: `tank | value | dynamo | status`
- `status` is compatibility-only unless a lossless migration is separately proven; no alias guessing
- control commit: `35e1ae631b8471a66eb0c4042295d5b5628d61ec`


## FC0-A collision guard

The prepared downstream release package now includes a Main-owned parallel-file collision map. Primary ownership is separated across Editor (`engineering/visual-editor/**`), Script Engineering (`engineering/scripts/**` + `python-editor/**`), Authority UX (`UserAdministration*`) and Licensing UX (`web/licensing/**` + `Scada.LicenseGenerator/**`). Shared shell/router/types/i18n/CI files are Main-coordinated hotspots, not free-for-all lane ownership.

Latest FC0-A prep commit: `dfae2377f0c6802da726650697040181c7b0f453`.
