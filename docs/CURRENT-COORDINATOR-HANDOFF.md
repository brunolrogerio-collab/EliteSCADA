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

- order: `FND06-CODEX-WAIT-POSTMERGE-V4`
- exact product base: `6c810647c9773a19b212d9c33694780141786ac7`
- base tree: `1221ff55963052be4e924dd644efbaa65763f546`
- work branch: `work/w15-fnd-06-visual-stability-foundation`
- target: `wave15/corrections-integration`
- control branch/file: `coord/w15-fnd06-control:docs/WAVE15-FND06-CONTROL.md`
- active control commit: `8d1f415bc16b556e8133e6a1da1ab89881e7f189`
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

FC0-A state: **PREPARED / NOT RELEASED / BLOCKED ON FND-06 + POST-FND06 FOUNDATION AUDIT**.

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

- active order: `FND06-CODEX-WAIT-POSTMERGE-V4`
- validation profile: `UI_EDITOR, RUNTIME_RENDERER`
- known legacy set: `tank | value | dynamo | status`
- `status` is compatibility-only unless a lossless migration is separately proven; no alias guessing
- control commit: `8d1f415bc16b556e8133e6a1da1ab89881e7f189`


## FC0-A collision guard

The prepared downstream release package now includes a Main-owned parallel-file collision map. Primary ownership is separated across Editor (`engineering/visual-editor/**`), Script Engineering (`engineering/scripts/**` + `python-editor/**`), Authority UX (`UserAdministration*`) and Licensing UX (`web/licensing/**` + `Scada.LicenseGenerator/**`). Shared shell/router/types/i18n/CI files are Main-coordinated hotspots, not free-for-all lane ownership.

Latest FC0-A prep commit: `dfae2377f0c6802da726650697040181c7b0f453`.


## Same sequential CODEX routing

The CODEX chat/lane that executed prior Foundation work including FND-04 is the active FND-06 executor.

- FND-04 old control no longer means executor WAIT.
- FND-04 control rev 0016 routes that same CODEX through `ROUTE-SEQUENTIAL-CODEX-TO-FND06-12`.
- routed destination: `coord/w15-fnd06-control:docs/WAVE15-FND06-CONTROL.md`
- active FND-06 order: `FND06-CODEX-WAIT-POSTMERGE-V4`
- FND-06 control commit: `8d1f415bc16b556e8133e6a1da1ab89881e7f189`

On `SIGA`, that CODEX should execute FND-06, not report FND-04 frozen/wait.

## FC0-A release gate update

After FND-06 freeze, FC0-A still waits for `FC0A-POST-FND06-W15-FOUNDATION-AUDIT-01`.

The audit must close the Wave14->Wave15 premise/gap matrix and prove FND-05/FND-07 are non-breaking to frozen contracts consumed by the first four DEVs.

Only after audit `ACCEPTABLE / FC0A_RELEASE_APPROVED` may Main activate:
- DEV-EDITOR;
- DEV-SCRIPT-ENGINEERING;
- DEV-AUTHORITY-UX;
- DEV-LICENSING-UX;
- FND-05;
- FND-07.

FND-05 and FND-07 remain PREPARED / NOT ACTIVE until then.


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
- active order: `FND06-CODEX-WAIT-POSTMERGE-V4`;
- control revision `0005`;
- control commit `8d1f415bc16b556e8133e6a1da1ab89881e7f189`;
- preferred delta: tests only; minimal production fix only if the mounted scenario exposes a remaining defect.

The same sequential CODEX lane remains the executor. No merge/freeze yet.


## PR #337 contract-risk snapshot for post-FND06 FC0-A gate

Post-FND06 audit control:
- audit: `FC0A-POST-FND06-W15-FOUNDATION-AUDIT-01`
- audit rev: `0002`
- latest audit-control commit: `9e75fd836d03dc34619f72ba5056e3aa874cfdbb`

PR #337 pre-freeze evidence:
- candidate `923543705378016090e7067b35954795a9591a57`
- tree `5657cee7169a4e77370d416add4efcf07184d7c0`
- natural T1 `35931139983` SUCCESS
- no Security/Authority/Licensing/Script/lifecycle contract change in its 9-file visual/editor/runtime delta
- FND-06 still HOLD pending mounted A7 closeout; this is an evidence gate, not a known contract break.

Current FC0-A contract-risk classification:
- DEV-EDITOR: `LOW / GUARDED` — must consume frozen FND-06 compatibility/renderer authority; mounted A7 proof still pending.
- DEV-SCRIPT-ENGINEERING: `NONE IDENTIFIED / GUARDED` — FND-04 remains untouched; FND-05 may only fence execution authority around it.
- DEV-AUTHORITY-UX: `NONE IDENTIFIED / GUARDED` — FND-07 must compose FND-02/AUTH-04, not redefine it.
- DEV-LICENSING-UX: `LOW BUT MATERIAL RESIDUAL` — FND-05 redundancy entitlement/readiness must remain additive/backward-compatible to frozen FND-03 semantics.

FND-05 hard guard:
- control rev `0003`
- commit `85502eaaa3a27fc2c050396b3f40c3e08943ae37`
- existing FND-03 license validity/meaning, Interactive/ViewOnly/session quota semantics, machine binding and install/replace/remove behavior may not be reinterpreted by HA.
- if HA needs such a breaking change -> `BLOCKED-CONTRACT` before FC0-A DEV release.

FC0-A release prep snapshot commit:
`c0e4bd69c89c47efc7dc936a6ac9e61cbbbd8f96`.

This is pre-freeze risk assessment only; final release still requires exact FND-06 freeze + independent post-FND06 audit PASS.


## FND-06 merged checkpoint pending freeze

PR #337 is merged.

- candidate: `2257f8f99b5e6deac80d64ed2cc0c43aa8dab1cc`
- candidate tree: `2ebb839a788bb4fad249877689c25ac1b18f6d74`
- merge SHA: `624f2eca456310a2c6156538b3616a06e3be075f`
- merge tree: `fb864fb954b0123e69db379cd6b3120349b43600`
- candidate T1 `35939646387`: SUCCESS
- post-merge broad CI `35940661531` / #1563: PENDING/IN PROGRESS at this record
- FND-06 state: **INTEGRATED / POST-MERGE CI PENDING / NOT YET VERIFIED-FROZEN**
- CODEX: `FND06-CODEX-WAIT-POSTMERGE-V4 / NO_MUTATION`
- FND-06 control rev `0006`, commit `8d1f415bc16b556e8133e6a1da1ab89881e7f189`

Post-FND06 audit remains blocking and is preloaded with this exact checkpoint:
- `FC0A-POST-FND06-W15-FOUNDATION-AUDIT-01`
- audit rev `0003`
- audit-control commit `a9ed4003acf8c71db035bc854a75f87cf97273fb`
- state `PREPARED / WAIT_FND06_POST_MERGE_CI_GREEN`

No FC0-A DEV, FND-05 or FND-07 release until FND-06 freezes and the independent audit returns `ACCEPTABLE / FC0A_RELEASE_APPROVED`.


## Current blocker — INFRA-CI-01B

FND-06 product work is merged and Main-accepted, but not frozen.

- FND-06 merge: `624f2eca456310a2c6156538b3616a06e3be075f`
- post-merge CI `35940661531`: FAILURE
- failure: PostgreSQL `23505 pg_namespace_nspname_index` during shared-schema initialization
- FND-06 causality: not established; failing source/test blobs unchanged by FND-06
- historical same-signature Wave 14 evidence exists
- no blind rerun authorized.

Active sequential CODEX mission:
- `INFRA-CI-01B-POSTGRES-SCHEMA-LOCK-V2`
- control: `coord/w15-infra-ci-01b-control:docs/WAVE15-INFRA-CI-01B-CONTROL.md`
- control commit: `be7a2d875c24e07d023162e64c65acb0aebe9672`
- work: `work/w15-infra-ci-01b-postgresql-schema-init`
- exact base: `624f2eca456310a2c6156538b3616a06e3be075f`.

FND-06 control rev `0007` routes this same CODEX to INFRA-CI-01B.
FND-04 legacy routing control rev `0019` also routes directly to INFRA-CI-01B.

FC0-A and independent audit are not released until:
1. INFRA-CI-01B correction is reviewed/merged;
2. exact broad integration CI is green;
3. FND-06 is declared VERIFIED/FROZEN;
4. post-FND06 audit returns ACCEPTABLE / FC0A_RELEASE_APPROVED.


## INFRA-CI-01B V2 widened only for shared-schema DDL sequencing

Intermediate PR #338 head `97c665c8e4d62268336dfdef400f992c2f9d43cf` remains unmerged.

Full local concurrency RED exposed additional creators:
- PostgreSqlAuthorityPolicyStore
- PostgreSqlAuthorityLifecycleStore
- PostgreSqlRuntimeSessionLeaseStore

Current order is `INFRA-CI-01B-POSTGRES-SCHEMA-LOCK-V2` at control commit `be7a2d875c24e07d023162e64c65acb0aebe9672`.

Only initialization lock sequencing is authorized in those files. No Authority, session, quota, admission, fencing or licensing semantics may change.


## Current active order — FND-06 E2E fixture isolation

Broad run `35944510920` proved INFRA-CI-01B backend correction green but exposed a test-only FND-06 fixture leak.

Active order:
`FND06-CODEX-E2E-FIXTURE-ISOLATION-V6`

Exact base:
`eb4563cf0060449b479c4335ef30a19ed65e35ab`

Work:
`work/w15-fnd06-e2e-fixture-isolation`

Only authorized mutation:
`web/scada-web/tests-e2e/fnd06-mounted-legacy-selection.spec.ts`

Do not weaken `runtime.spec.ts`, change product code, import semantics, Playwright workers/order/retries or CI workflow.

Required strategy: temporarily update existing canonical Screen/Popup identities, then restore those same identities in finally; prove no fnd06 fixture remains.

INFRA-CI-01B is merged/Main-accepted and requires no further mutation.
FC0-A audit remains PREPARED / blocked.
