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

- order: `FND06-CODEX-WAIT-POSTMERGE-V4`
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
  - order: `FND06-CODEX-WAIT-POSTMERGE-V4`
- FC0-A: **BLOCKED only on FND-06**
- downstream release package is prepared but not released at `docs/WAVE15-FC0-A-RELEASE-PREP.md` on the FND-06 control branch.



## FND-06 execution metadata correction

Before first product mutation, Main corrected the prepared FND-06 validation profile to the actual Wave 15 router vocabulary: `UI_EDITOR, RUNTIME_RENDERER`. The exact product seed also proves bare `status` is a persisted legacy identifier alongside `tank`, `value` and `dynamo`; it is not a current canonical `core.*` built-in and no guessed alias is authorized.

Active FND-06 order: `FND06-CODEX-WAIT-POSTMERGE-V4`.
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
- FND-06 control rev 0004: `FND06-CODEX-WAIT-POSTMERGE-V4`
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


## Post-FND06 broad CI exposed generic PostgreSQL blocker

PR #337 merged at `624f2eca456310a2c6156538b3616a06e3be075f`.

Exact broad EliteSCADA CI #1563 / `35940661531`:
- Web — SUCCESS;
- Backend build/test/smoke — FAILURE in test;
- Chromium — skipped downstream.

Only identified failure:
`PostgreSqlVisualDynamicPersistenceTests.RevisionPersistence_PreservesVisualExpressionConditionAndAnalogFill`

Error:
`23505: duplicate key value violates unique constraint pg_namespace_nspname_index`
during `PostgreSqlEngineeringProjectStore.InitializeAsync`.

The store and failing test are byte-identical to the pre-FND06 checkpoint. PR #337 did not touch Persistence/PostgreSQL. Same PostgreSQL catalog-race signature was diagnosed in Wave 14.

Main did not rerun blindly.

Active generic correction:
- `INFRA-CI-01B-POSTGRES-SCHEMA-LOCK-V1`
- control `coord/w15-infra-ci-01b-control:docs/WAVE15-INFRA-CI-01B-CONTROL.md`
- control commit `358b067d9af6501b2945f311b5d2cd32cab64efa`
- exact base `624f2eca456310a2c6156538b3616a06e3be075f`
- work branch `work/w15-infra-ci-01b-postgresql-schema-init`.

FND-06 = INTEGRATED / MAIN-ACCEPTED / FREEZE BLOCKED BY GENERIC INFRA.
FC0-A audit remains PREPARED.


## Broad CI #1564 — PostgreSQL fixed; FND-06 E2E fixture leak remains

Exact run `35944510920` on `eb4563cf0060449b479c4335ef30a19ed65e35ab`:
- Web SUCCESS;
- Backend build/test/smoke SUCCESS;
- Chromium FAILURE: 636 passed / 1 failed.

The PostgreSQL failure that triggered INFRA-CI-01B is closed at the backend gate.

The sole Chromium failure is `runtime.spec.ts`, which saw an extra `fnd06-legacy-screen-*` created by the FND-06 mounted closeout test.

Root cause: JSON import/apply for Screens/Popups is upsert-only. Reapplying the pre-test export cannot delete a new fixture entity.

Active test-only correction:
- `FND06-CODEX-E2E-FIXTURE-ISOLATION-V6`
- work `work/w15-fnd06-e2e-fixture-isolation`
- FND-06 control rev 0010 / `b7b3ab914464a2da87d7b2175eb95a24d8a7db9b`.

No product semantic change is authorized.


## Pre-audit finding — W15-P1-01 Server Script recovery

While preparing the mandatory post-FND06 FC0-A audit, Main revalidated the current Server Script runtime against the final Wave 14 backlog contract.

Current source still shows:
- `ScriptRuntimeExecutionCoordinator.ProcessNextAsync` returns `Throttled` while diagnostics `IsThrottled` is true;
- the only coordinator exit is explicit `ResetThrottle()`;
- repository search found no production automatic Server Script caller of `ResetThrottle()`;
- existing tests prove timeout -> throttled behavior, not bounded cooldown/half-open/probe recovery.

Wave 14/W15-P1-01 explicitly required bounded automatic recovery and rejected a permanent silent throttle latch.

Preliminary disposition:
`W15-P1-01 = PRELIMINARY BLOCKED_FOUNDATION`

This finding is independent of FND-06 and does not prevent FND-06 from becoming VERIFIED/FROZEN if exact broad CI #1565 is green. It **does** prevent immediate FC0-A release if the independent audit confirms it.

Audit evidence:
- `coord/w15-fnd06-control:docs/WAVE15-FC0A-POST-FND06-AUDIT-EVIDENCE.md`
- preliminary matrix commit `3483faf3aab4940791807b07b4453865629d9077`
- audit control rev 0008 / commit `18a0fcda2b354779cdf0f1ba4d829a838714b3d2`.

DEV-SCRIPT-ENGINEERING may not absorb this runtime/Foundation correction silently.


## Independent AUD routing prepared

The former FND-04 independent AUD lane is now pre-routed, but not activated, for the mandatory post-FND06 closure audit.

- current AUD order: `FC0A-AUD-WAIT-FND06-FINAL-BROAD-0012`
- state: `WAIT_FND06_FINAL_BROAD`
- final broad: `35953557122`
- provisional exact product checkpoint: `560ac9d80cc7e854f2513559dc6afb28cfb4aee3`
- FND-04 AUD control rev 0023 / `1be8ef8cbdd555f3fa554e905faafab186c21dd3`
- next audit: `FC0A-POST-FND06-W15-FOUNDATION-AUDIT-01`

AUD must independently confirm/disprove Main's preliminary P1-01 Server Script recovery blocker after activation.


## Pre-audit finding — W15-P1-06 truthful Engineering fallback

Exact source inspected at the final FND-06 product checkpoint `560ac9d80cc7e854f2513559dc6afb28cfb4aee3`:

`web/scada-web/src/engineering/EngineeringApp.tsx`

Observed:
- `snapshot` initializes as null while loading;
- failed load sets/keeps `snapshot=null` and renders an error/retry state;
- the sidebar project chip nevertheless renders `snapshot?.workspace.projectName ?? snapshot?.workspace.projectKey ?? 'Demo Project'`;
- therefore an absent/unloaded public model can still be displayed as `Demo Project`;
- no-model WorkspaceBar fallbacks can also present `unsaved` / `clean` despite no authoritative Working model.

This matches the Wave14 A6 / W15-P1-06 class that required truthful loading/unavailable/error identity rather than a fictitious Working project.

Preliminary disposition:
`W15-P1-06 = PRELIMINARY BLOCKED_PRODUCT / SHARED_ENGINEERING_SHELL`

This is not causal to FND-06 and does not block its freeze if broad #1565 is green. If independent audit confirms it, FC0-A remains blocked until a bounded shared-shell correction is integrated/revalidated.

Audit control rev 0009:
`888473cbf27e003d659ec3dd87de2f0f89240474`

Evidence matrix:
`a5260ca309742894ee48e8cce17d9175d67cda08`.


## Audit blocker correction preparation

Coordination-only preparation exists for the two preliminary blockers. It does **not** authorize product mutation:

`coord/w15-fnd06-control:docs/WAVE15-FC0A-AUDIT-BLOCKER-CORRECTION-PREP.md`

prep commit:
`5904924faf7dcc13ec42c495ff54fe6ef82ad005`

Prepared, inactive orders:
- `FC0A-BLOCKER-P101-SERVER-SCRIPT-RECOVERY-V1`
- `FC0A-BLOCKER-P106-ENGINEERING-FALLBACK-V1`

Activation is forbidden until independent audit confirms the corresponding finding on the exact frozen post-FND06 checkpoint.

P1-01 plan preserves FND-04 Script TAG semantics, sandbox isolation, bounded queue/coalescing and Active revision safety while replacing a permanent latch only if confirmed.

P1-06 plan preserves lifecycle/Authority/FND-06 contracts while removing fictitious no-snapshot project/status state and distinguishing transport rejection from actual HTTP response only if confirmed.


## FND-06 final freeze / FC0-A audit active

FND-06 is now **VERIFIED/FROZEN**.

Exact product checkpoint:
- SHA `560ac9d80cc7e854f2513559dc6afb28cfb4aee3`;
- tree `674019fbbc21001a2d68deb853c2c0b293e0a5cb`;
- broad `35953557122` / EliteSCADA CI #1565 — **SUCCESS**;
- Web SUCCESS;
- Backend build/test/smoke SUCCESS;
- Chromium end-to-end SUCCESS.

Main verified that divergence above the product checkpoint was coordination-doc-only before freeze.

FND-06 control:
- rev 0012;
- commit `44c372d8316733398d25f72f3331b12022ab6594`;
- CODEX order `FND06-CODEX-FROZEN-FINAL-08 / NO_MUTATION`.

Mandatory independent post-FND06 audit is now **ACTIVE**:
- audit `FC0A-POST-FND06-W15-FOUNDATION-AUDIT-01`;
- audit rev 0010 / `72421abee84ac09415a045b7c86d554dba7dd187`;
- AUD lane rev 0024 / `0048a198a2c7d2ac40dd1a055c5bcc6346f30fe8`;
- order `FC0A-AUD-ACTIVE-POST-FND06-0013`.

Main preliminary P1-01 and P1-06 findings remain hypotheses until independent AUD disposition.

No FC0-A DEV, FND-05 or FND-07 release yet.


## FC0-A audit completed by Main — corrections required

The Main Coordinator completed the mandatory post-FND06 Wave14->Wave15 audit on frozen product SHA `560ac9d80cc7e854f2513559dc6afb28cfb4aee3`.

Result:
`CHANGES_REQUIRED`

Confirmed blockers:
- W15-P1-01 Server Script throttle recovery;
- W15-P1-06 truthful Engineering fallback.

FND-05 remains additive/compatible and FND-07 compositional/compatible; neither currently forces a breaking change to FC0-A-consumed frozen contracts.

Audit report commit:
`463d357f8a9c11f774f5da59480e4a17a19569f5`.

First correction activated:
- `FC0A-BLOCKER-P101-SERVER-SCRIPT-RECOVERY-V1`;
- branch `work/w15-fc0a-p101-server-script-recovery`;
- exact base `560ac9d80cc7e854f2513559dc6afb28cfb4aee3`;
- profile `SCRIPT_RUNTIME`;
- control `b8858d08a8516588db4be40d2da48ea266fe793e`;
- routing `a088c884d90bd0c2d86b844f74332306a80b7a7c`.

P1-06 remains queued. No FC0-A lane has been released.


## Post-FND06 audit — second-pass deep review

Main completed a distinct second-pass audit on the frozen baseline:
`560ac9d80cc7e854f2513559dc6afb28cfb4aee3` / tree `674019fbbc21001a2d68deb853c2c0b293e0a5cb`.

Report:
`coord/w15-fnd06-control:docs/WAVE15-FC0A-POST-FND06-AUDIT-SECOND-PASS.md`

report commit:
`43c266949236af377ba859c9b8f09fa2d3a3a31a`

audit control rev 0012:
`62a731cc4291b751e9cb2e91e440fcf0d5ffb3bd`

release-prep refinement:
`c78895b218608b511e61b90206189d2b641a1e71`

Second-pass conclusion:
`NO NEW PRE-FC0A BLOCKER IDENTIFIED`.

The two confirmed release blockers remain:
- W15-P1-01 Server Script bounded recovery;
- W15-P1-06 truthful Engineering no-model/loading/error state.

New/refined downstream findings:
- DEV-EDITOR must consume FND-06 compatibility for known-legacy marquee/geometry/z-order/multi-object authoring paths that still use strict built-in lookup;
- DEV-SCRIPT must make Script Assistant consume the FND-06 compatibility seam for known-legacy property discovery;
- Script Engineering already has cursor-aware insertion and timer/tagChanged authoring, so those are regression/refinement scope, not greenfield;
- Python API Help still lacks a formal signature/parameter/return/example contract;
- DEV-LICENSING must surface requested vs granted Runtime class, explicit ViewOnly request and Interactive-quota fallback/reason UX; backend Authority/capacity contract is already sound;
- one minor Runtime API message still says `viewer` where canonical public vocabulary is `viewOnly`;
- W15-P2-01 Trends still lacks explicit realtime/reconnect/last-request/last-success/freshness observability;
- W15-P2-02 shared Popup/live-value path correctly handles numeric zero/quality but still lacks explicit freshness-age/reason telemetry.

Contract result strengthened:
- FND-05 remains ADDITIVE / COMPATIBLE;
- FND-07 remains COMPOSITIONAL / COMPATIBLE;
- production host uses `EngineeringWorkspace(seedDemo:false)`, so a truthful neutral no-Demo workspace is already representable;
- existing Authority detach/attach/switch primitives further reduce FND-07 contract risk.

No DEV/FND-05/FND-07 release occurs until P1-01 and P1-06 close and Main reruns the affected release rows.


## Main Coordinator takeover — third post-FND06 audit pass (2026-09-24)

Live takeover revalidated against GitHub after coordinator-chat rotation.

- frozen product checkpoint remains `560ac9d80cc7e854f2513559dc6afb28cfb4aee3` / tree `674019fbbc21001a2d68deb853c2c0b293e0a5cb`;
- post-FND06 FC0-A audit remains `CHANGES_REQUIRED`;
- third Main pass outcome: `NO NEW PRE-FC0A BLOCKER IDENTIFIED`;
- authoritative third-pass report: `coord/w15-fnd06-control:docs/WAVE15-FC0A-POST-FND06-AUDIT-THIRD-PASS.md`, commit `b144de748337045bf447a5142d825fa0beac1ff1`;
- audit control rev 0013: `0d27fe59d154c161418c996a4ea2df6fe59066e6`;
- confirmed release blockers remain only W15-P1-01 and W15-P1-06;
- active order remains `FC0A-BLOCKER-P101-SERVER-SCRIPT-RECOVERY-V1` on `work/w15-fc0a-p101-server-script-recovery`;
- work branch revalidated identical to exact base: 0 ahead / 0 behind / no changed files / no PR;
- P1-06 remains queued separately; it must not be mixed into P1-01;
- DEV-EDITOR, DEV-SCRIPT-ENGINEERING, DEV-AUTHORITY-UX, DEV-LICENSING-UX, FND-05 and FND-07 remain HOLD.

Third-pass refinements are downstream/pre-activation only: P2-03 shell responsiveness remains open; P2-04 account accessibility is substantially implemented but needs focused keyboard regression; P2-05/P2-06 remain Engineering layout-density residuals; P2-07 is partially implemented and should not rebuild the existing Dynamo insertion preview; Help routing is surface-level rather than Engineering-section-contextual; FND-07 gained an explicit Engineering Lock × detach/switch/neutral-bootstrap acceptance guard. FND-05 remains additive/compatible; FND-07 remains compositional/compatible.

FND control refreshes:
- FND-05 hold/activation metadata: `0c69dd307880b6afcd89f352cf76fef183087410`;
- FND-07 hold metadata + Lock/detach acceptance guard: `d0aba9e375a8330b7720b32f4defcdabf0ec12ae`.

No product code, product branch or release state was changed by this audit pass.


## Main decision — consolidated FC0-A correction package (2026-09-24)

Product Owner requested that all known FC0-A findings be corrected now where safely possible, with one larger CODEX delivery before the next Main review.

Active order:
- `FC0A-CONSOLIDATED-CORRECTION-PACKAGE-V2`
- control: `coord/w15-fnd06-control:docs/WAVE15-FC0A-CONSOLIDATED-CORRECTION-PACKAGE.md`
- control commit: `0ab4a2f9226a1f3710aa516dff9534d880023218`
- branch: `work/w15-fc0a-consolidated-corrections`
- exact base: `560ac9d80cc7e854f2513559dc6afb28cfb4aee3`
- exact base tree: `674019fbbc21001a2d68deb853c2c0b293e0a5cb`
- one consolidated PR only after the package is complete and exact-head validation is green.

The former P1-01-only order/branch is superseded unused; it was identical to base with no PR at supersession.

The package includes the two mandatory blockers plus confirmed shared/downstream residuals that can be safely brought forward: shell responsiveness, account keyboard regression, Engineering scroll composition, Engineering Lock density, Trends/live-value freshness observability, contextual Help routing, canonical viewOnly wording, frozen FND-06 compatibility consumption in Editor/Script Assistant, structured Script API Help/representative recipe, Runtime Session/Licensing requested/granted UX, and Template/Equipment/Library inspection.

Evidence-bounded/speculative items and future Foundations remain outside the package. Frozen FND-01/02/03/04/06/08 semantics may not be redefined.

Sequential CODEX route updated to rev 0027 / commit `93d79a773d7571677a9f43e6f3a80ad21aa25612`.
No intermediate Main review is required; CODEX returns one final candidate handoff unless a real contract/base/environment blocker prevents safe completion.


## Main decision — six prepared parallel DEV chats / CODEX as sequential validator (2026-09-24)

Product Owner clarified that the previous `normally no more than four active coding DEVs` rule was a historical operational throttle for a different context and is **not** a permanent Wave 15 concurrency limit.

Prepared post-FC0A implementation chats:
- DEV-EDITOR;
- DEV-SCRIPT-ENGINEERING;
- DEV-AUTHORITY-UX;
- DEV-LICENSING-UX;
- FND-05 DEV;
- FND-07 DEV.

Canonical cross-lane coordination:
- branch: `coord/w15-parallel-dev-control`
- file: `docs/WAVE15-PARALLEL-DEV-CONTROL.md`
- prepared control commit: `87479d0bc3042435935ac1dbaa119d3a7ed72bd6`

All six are **PREPARED / BLOCKED** until Main records `FC0A_RELEASE_APPROVED` on an exact integrated SHA/tree. No work branch is created before that exact activation base is known.

Execution pipeline:
`normal DEV chat implements code -> Main reviews -> same DEV corrects material defects -> Main accepts exact candidate for CODEX -> sequential scarce CODEX writes/extends tests, runs focused/adversarial local validation and exact-head T1 -> Main finalizes integration`.

CODEX may make small validation-driven corrections that do not redesign the feature. Material product/design defects return to the owning DEV. Frozen-contract insufficiency returns to Main.

Feature DEV results stay in **separate PRs**; they are not combined into a raw monolithic implementation package. After individually accepted/T1-green merges, Main runs broader integrated T2 on the exact integration head.

FND-05/FND-07 also move to normal-chat DEV implementation:
- FND-05 prepared order: `FND05-DEV-HA-AUTHORITY-V1`; dedicated control rev 0004 / commit `f3f685cff16ebfef53db4aa69b061d7bac773829`.
- FND-07 prepared order: `FND07-DEV-DETACH-NEUTRAL-V1`; dedicated control rev 0003 / commit `ae92f5f1ec55464eea7f231c178d0a05dc04df13`.

Foundation validation remains stricter:
`DEV -> Main contract review -> CODEX adversarial/focused validation -> exact-head T1 -> Main merge -> post-merge validation -> VERIFIED/FROZEN`.

FND-05 additionally requires `CODEX_HA_ADVERSARIAL_GREEN`.

Release preparation was updated:
- `coord/w15-fnd06-control:docs/WAVE15-FC0-A-RELEASE-PREP.md`
- commit `349ed55e74b237c77dec64971a7f8dac3ee97c7e`.

ROADMAP current coordination update:
- commit `08b11987b78adee134de77906fd57b2af199e5fb`.

Current FC0-A product work remains PR #340 / V2 IN PROGRESS. This coordination preparation does not activate any downstream lane and does not alter the current CODEX mission.

Bootstrap texts for each lane will be generated on Product Owner request; the bootstrap is only onboarding convenience and GitHub live controls remain authoritative.


## Main decision — two-stage fresh-install partial preview (2026-09-24)

After the four feature DEV lanes are integrated/T2-verified and FND-05/FND-07 are independently VERIFIED/FROZEN, Main will run a partial first-project product audit before later EEE/complete-product acceptance.

Prepared control:
- branch: `coord/w15-fresh-install-preview-control`
- file: `docs/WAVE15-FIRST-PROJECT-FRESH-INSTALL-PREVIEW-CONTROL.md`
- prepared commit: `8d209c8f0cacf5c1feb050632645c9537e1f09dc`

Prepared gates:
- `W15-FIRST-PROJECT-CODEX-BLACKBOX-PREVIEW-01`
- `W15-FIRST-PROJECT-HUMAN-PREVIEW-01`

The two first-project journeys are independent.

CODEX moment:
- fresh installation / no project / no EEE / no hidden Demo project;
- user-like browser exploration;
- high-level objective only: create a first SCADA application from zero and reach a functional truthful Runtime;
- no source/control/database/internal API lookup during the black-box journey;
- no code correction during exploration;
- source/log/API diagnosis is allowed only after the journey completes or is blocked;
- detailed CODEX findings are persisted but not surfaced to Product Owner before the human preview.

Human moment:
- second independent clean environment;
- Product Owner repeats the same high-level first-project mission as a real user;
- no CODEX-created project;
- no detailed CODEX report/checklist before the unaided human journey completes;
- needing help or becoming blocked is itself audit evidence.

After both journeys:
- Main unseals and compares findings as BOTH / CODEX_ONLY / HUMAN_ONLY / PATH_DIVERGENCE / NOT_REPRODUCED;
- a directed follow-up may then cover restart/persistence, Authority/ViewOnly, FND-07 detach -> neutral bootstrap -> Project B -> B/A switching, and a separate HA user-surface/manual transfer check;
- material blockers are corrected/owned; non-blocking onboarding/usability gaps may feed later Installation UX/product convergence.

T1/T2/T3/T4 green evidence does not replace these audits.

Possible partial-preview dispositions:
- `ACCEPTABLE_FOR_NEXT_CONVERGENCE`
- `CHANGES_REQUIRED`

Neither is final Wave 15 acceptance. EEE v15, later T3/T4 and final fresh complete-product Preview remain mandatory.

Roadmap update: `57729b9db0ebcac2c748a9f6f0101eb48c5b625b`.
Six-lane control link update: `e130b5353c320e9c22f8e1f8f3a7e42a2dd6946c`.

This preparation does not activate the preview now and does not alter current PR #340 / CODEX V2 execution.


## Main review — PR #340 V2 complete / narrow V3 closeout active (2026-09-24)

Main reviewed final V2 candidate:
- PR #340;
- head `150b808140a5fdf80afd0c88d46ea80f83f630b2`;
- tree `c2bfbbfe705be64e5c1aac314f96bf8851a913b0`;
- 15 commits / 41 changed files;
- natural Wave 15 T1 `36033152318`: SUCCESS across classification, Common T1 sanity, Focused .NET, Focused Chromium, Web semantic build and final gate.

Disposition:
`FC0-A CONSOLIDATED V2 -> MAIN COORDINATOR — CHANGES_REQUIRED / NARROW V3`

Accepted V2 closures remain preserved. Two bounded groups remain before merge:
1. actual user-facing Runtime Session Class request/status surface for ViewOnly/Interactive requested-vs-granted/reason truth without FND-03 redesign;
2. missing R6 mounted evidence for shell no-overflow, Engineering independent scroll, compact Engineering Lock lifecycle and known-legacy advanced-authoring/unknown containment.

Binding control:
- `coord/w15-fnd06-control:docs/WAVE15-FC0A-CONSOLIDATED-CORRECTION-PACKAGE.md`
- order `FC0A-CONSOLIDATED-CORRECTION-PACKAGE-V4`
- section 12
- commit `6a13c88fd112fe94e9c87e1206d9840be75161eb`.

Sequential CODEX route:
- rev 0030
- `ROUTE-SEQUENTIAL-CODEX-TO-FC0A-CONSOLIDATED-V3-18`
- commit `7011f7e2a29f37c105657b1a56ceac62a6ef4d58`.

PR #340 Main review comment: `5819091955`.
Issue #305 ledger comment: `5819092476`.

No merge/freeze/release occurred. All downstream six lanes remain PREPARED/HOLD until FC0-A final acceptance.


## Main review — PR #340 V3 product accepted / final evidence-only closeout active (2026-09-24)

Exact V3:
- head `1efc16994ea7b11857b0a1aac7da1690276e6d07`
- tree `da022f95a0c2342188a34ffe6dc830acf84d873a`
- natural T1 `36036316797`: SUCCESS.

Main accepts the V3 Runtime Session Class product surface and shell compact-width product direction.

Merge remains blocked only on final test/evidence closure:
- explicit Engineering scroll-composition proof;
- compact Engineering Lock lifecycle proof including lock-now + clear;
- known-legacy advanced-authoring + arbitrary-unknown containment proof;
- focused execution of owner specs that natural T1 did not select.

The T1 Chromium job on V3 executed 16 tests but did not select the Runtime Session mounted spec, app-shell, Engineering Lock or legacy owner-model specs, so its green result is not used as proof for those rows.

Binding control:
- `FC0A-CONSOLIDATED-CORRECTION-PACKAGE-V5`
- section 13
- commit `1485bd3d832a57b109d347e0b931b21334d56844`

CODEX route:
- rev 0031
- `ROUTE-SEQUENTIAL-CODEX-TO-FC0A-CONSOLIDATED-V4-19`
- commit `63afda347e798d5e45292161fab382911093ae42`

This is test/validation-only. No new feature scope is authorized.

PR #340 Main comment: `5819317792`.
Issue #305 ledger: `5819318386`.

No merge/freeze/release occurred. Six downstream lanes remain PREPARED/HOLD.


## Main review — PR #340 V4 tests accepted / final execution proof active (2026-09-24)

Exact V4 head `87eafb68e4fea7815a26ccffeb8a070fae6564c8` is test-only and natural T1 `36037962003` is green.

Main accepts the added scroll/Lock/legacy tests directionally, but the T1 Chromium log did not execute those owner specs. It selected only python-runtime-host, runtime, script-engineering-workspace-contract, visual-editor-workspace and local-auth bootstrap.

Merge therefore remains blocked solely on execution proof.

Binding control:
- `FC0A-CONSOLIDATED-CORRECTION-PACKAGE-V6`
- section 14
- commit `9990d84750be61c86322951255a67ff22fb4a29d`.

CODEX route:
- rev 0032
- `ROUTE-SEQUENTIAL-CODEX-TO-FC0A-CONSOLIDATED-V5-20`
- commit `96ccc7d6287db9889940b6a57ed54cee115a74e5`.

Required work is validation-only:
- support multiple profile-owned E2E specs in Wave 15 router;
- make UI_EDITOR execute app-shell, Engineering Lock and visual-editor owner specs in addition to workspace;
- make RUNTIME_RENDERER execute runtime-session owner spec in addition to runtime;
- unit-test router mapping;
- strengthen Lock backend-state transition and arbitrary-unknown containment assertions;
- final exact-head natural T1 must visibly execute these owner specs.

Required return:
`FC0-A CONSOLIDATED CODEX -> MAIN COORDINATOR — FINAL INTEGRATION HANDOFF V5`.

No merge/freeze/release yet. Six downstream lanes remain PREPARED/HOLD.


## FC0-A merged / release blocked by INFRA-CI-01C recurrence (2026-09-24)

FC0-A consolidated PR #340 is merged.

Exact accepted product checkpoint:
- SHA `d975174ae81ff7ed754585097240778a9862d965`
- tree `5ac06f47f1bede7a1b0c384c7b1d3c83730015ea`
- pre-merge Wave 15 T1 `36043296814`: SUCCESS
- required Chromium owner suite: 54 passed.

The exact post-merge push gate `EliteSCADA CI 36044280802` is red:
- Web build SUCCESS;
- Backend build SUCCESS;
- Backend Test FAILURE;
- smoke/Chromium skipped downstream.

Only identified failure:
`PostgreSqlEngineeringSchemaV15CommunicationBindingTests.PostgreSqlRevision_SavePreviewApply_RoundTripsCommunicationBinding`
with PostgreSQL
`23505 / pg_namespace_nspname_index`
on concurrent
`CREATE SCHEMA IF NOT EXISTS elitescada`.

Main proved the affected Engineering store, shared-schema lock helper, Timescale infrastructure, failing test and existing concurrency regression are byte-identical to frozen pre-FC0A `560ac9d...`. Classification:

`GENERIC_INFRASTRUCTURE_RECURRENCE / NOT_FC0A_PRODUCT_CAUSAL`.

Do not reopen accepted PR #340 product work and do not use a blind rerun as release evidence.

Active blocker:
- `coord/w15-infra-ci-01c-control:docs/WAVE15-INFRA-CI-01C-POSTGRES-SCHEMA-RACE-RECURRENCE-CONTROL.md`
- order `INFRA-CI-01C-POSTGRES-SCHEMA-RECURRENCE-V1`
- control commit `8cf15e123823f5aaae2c911f0f113e804c9dad0d`
- work branch `work/w15-infra-ci-01c-postgres-schema-recurrence`
- exact base `d975174ae81ff7ed754585097240778a9862d965`.

Sequential CODEX route:
- rev 0033
- `ROUTE-SEQUENTIAL-CODEX-TO-INFRA-CI-01C-21`
- commit `57dbe4a29ad69483d0a06f1bc0b8dc2f8f908d6b`.

Current state:
`FC0-A -> POST_MERGE_VALIDATION_BLOCKED_BY_INFRA`.

No `FC0A_RELEASE_APPROVED` yet. All six prepared DEV/FND lanes remain WAIT. Their work branches must not be created/activated from a stale checkpoint; after 01C is integrated and the exact new post-merge gate is green, Main will record the final release base and create/activate them.


## FC0-A post-merge Help E2E load blocker (2026-09-24)

INFRA-CI-01C PR #341 merged at `da0e64122f1e4d0293e027f45ef95021cd03c1a1`
(tree `a724e565f11121a43b97bf0c59683b414c72f48c`).

Exact broad `EliteSCADA CI 36047274028 / #1567`:
- Web SUCCESS;
- Backend build/test SUCCESS;
- Runtime smoke SUCCESS;
- Chromium FAILURE before test execution.

The PostgreSQL recurrence is closed.

The remaining deterministic blocker is the FC0-A-added
`contextual-help-routing.spec.ts` importing the React shell `AppNavigation.tsx`;
its transitive CSS import reaches the Node-side Playwright loader and fails with
`src/auth/auth.css: Unexpected token (1:0)`.

Classification:
`FC0A_POSTMERGE_E2E_TEST_LOAD_DEFECT / PR340_TEST_CAUSAL / PRODUCT_BEHAVIOR_NOT_SHOWN_DEFECTIVE`.

Active closeout:
- control: `coord/w15-fnd06-control:docs/WAVE15-FC0A-POSTMERGE-HELP-E2E-LOAD-CONTROL.md`;
- order: `FC0A-POSTMERGE-HELP-E2E-LOAD-V1`;
- branch: `work/w15-fc0a-postmerge-help-e2e-load`;
- exact base: `da0e6412...`;
- CODEX route rev 0034 / `ROUTE-SEQUENTIAL-CODEX-TO-FC0A-HELP-E2E-22`.

FC0-A remains NOT RELEASED. Do not activate the six prepared downstream lanes until Main obtains a globally green exact post-merge broad CI and records `FC0A_RELEASE_APPROVED`.


## FC0-A broad #1568 — stale Canvas source-contract blocker (2026-09-24)

Current exact release candidate:
`1ab3550e1afb258e38caaa6de3f6547f481bbef7`
(tree `701f4591a294885b414284691b1051cf274c9707`).

EliteSCADA CI `36049229264 / #1568`:
- Web SUCCESS;
- Backend build/test SUCCESS and Runtime smoke SUCCESS after the single repository-authorized same-SHA retry of the known IEC-104 T2 timing transient;
- Chromium full suite: 654 passed / 1 failed.

The only Chromium failure is `visual-editor-canvas-source-contract.spec.ts`, whose unchanged historical source assertion still requires `getBuiltinVisualObjectSchema`. The accepted FC0-A product intentionally consumes FND-06's `getVisualSchemaForEngineering` compatibility seam instead. Functional Canvas tests pass.

Classification:
`STALE_SOURCE_CONTRACT_TEST / PRODUCT_BEHAVIOR_NOT_DEFECTIVE`.

Active closeout:
- `FC0A-POSTMERGE-CANVAS-SOURCE-CONTRACT-V1`;
- control `coord/w15-fnd06-control:docs/WAVE15-FC0A-POSTMERGE-CANVAS-SOURCE-CONTRACT-CONTROL.md`;
- branch `work/w15-fc0a-postmerge-canvas-source-contract`;
- CODEX route rev 0035 / `ROUTE-SEQUENTIAL-CODEX-TO-FC0A-CANVAS-CONTRACT-23`.

FC0-A remains NOT RELEASED and the six post-FC0A lanes remain WAIT until a later exact broad gate is globally green.


## FC0-A RELEASE APPROVED / six implementation lanes ACTIVE (2026-09-24)

Main completed the final post-FND06 release audit.

Product release checkpoint:
- SHA `e3ed5138369c576549cb58a7aff9783792f322d3`
- tree `4e7627774fbfc111344e3d80fcb9d921eed8377e`
- exact broad gate `EliteSCADA CI #1569 / 36060017969`: SUCCESS
- Web build: SUCCESS
- Backend build/test: SUCCESS
- Runtime smoke: SUCCESS
- Chromium full suite: **655 passed / 0 failed**

Final audit:
`FC0-A FOUNDATION AUDIT -> MAIN COORDINATOR — ACCEPTABLE / FC0A_RELEASE_APPROVED`
rev 0014.

All six implementation branches were created directly from the exact product release SHA and independently revalidated as identical before coding:
- `work/w15-dev-editor-single-canvas`
- `work/w15-dev-script-engineering`
- `work/w15-dev-authority-ux`
- `work/w15-dev-licensing-ux`
- `work/w15-fnd-05-ha-authority`
- `work/w15-fnd-07-detach-neutral`

All six lanes are now `ACTIVE_CODING / AUTHORIZED`.

Control state:
- central parallel control rev 0002;
- four feature controls rev 0002;
- FND-05 control rev 0005 / order `FND05-DEV-HA-AUTHORITY-V1`;
- FND-07 control rev 0004 / order `FND07-DEV-DETACH-NEUTRAL-V1`.

Sequential CODEX is no longer executing FC0-A closeouts:
- route rev 0036;
- `ROUTE-SEQUENTIAL-CODEX-WAIT-POST-FC0A-24`;
- state `WAIT_FOR_MAIN_ACCEPTED_CANDIDATE`.

Workflow:
`normal DEV implements -> Main reviews -> same DEV corrects if needed -> Main accepts -> sequential CODEX validates/tests/T1 -> Main integrates`.

Foundations:
`normal FND DEV implements -> Main contract review -> sequential CODEX adversarial validation -> T1 -> Main integration -> post-merge -> VERIFIED/FROZEN`.

Product release SHA remains `e3ed5138...` even if `wave15/corrections-integration` advances afterward through coordination-only documentation commits.

Ledger: Issue #305 comment `5822605281`.

After all four feature lanes reach integrated T2 verification and FND-05/FND-07 are independently VERIFIED/FROZEN, proceed to the already-defined two-moment fresh-install first-project partial preview: CODEX black-box first, then Product Owner human journey on a separate reset environment.
