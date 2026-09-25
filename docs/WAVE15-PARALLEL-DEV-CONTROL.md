# Wave 15 — Parallel DEV Coordination Control

> GitHub live is the sole authority. This file prepares the post-FC0A parallel implementation model. It does **not** activate any lane by itself.

`CONTROL_BRANCH: coord/w15-parallel-dev-control`

`MAIN_ORDER_REV: 0024`

`STATE: POST_FC0A_PARALLEL_EXECUTION / MIXED_REVIEW_VALIDATION_CORRECTION`

`ACTIVATION_RULE: Main must revalidate GitHub live, record the exact FC0-A product SHA/tree, create each work branch from that exact checkpoint, and switch the lane state to ACTIVE before product mutation.`

## 0A. FC0-A release activation — six lanes ACTIVE

`FC0A_RELEASE_APPROVED: YES`

`FC0_A_BASE_SHA: e3ed5138369c576549cb58a7aff9783792f322d3`

`FC0_A_BASE_TREE: 4e7627774fbfc111344e3d80fcb9d921eed8377e`

`EXACT_BROAD_GATE: EliteSCADA CI #1569 / 36060017969 / SUCCESS`

`CHROMIUM_FULL_SUITE: 655 passed / 0 failed`

All six work branches were created directly from this exact checkpoint:
- `work/w15-dev-editor-single-canvas`
- `work/w15-dev-script-engineering`
- `work/w15-dev-authority-ux`
- `work/w15-dev-licensing-ux`
- `work/w15-fnd-05-ha-authority`
- `work/w15-fnd-07-detach-neutral`

Activation rule:
- each chat may now mutate only its own branch and owned scope;
- on `SIGA`, it must re-read its dedicated live control before coding;
- no lane may infer authority from this summary alone if its dedicated control disagrees;
- shared-hotspot policy remains binding;
- no lane self-merges;
- CODEX remains sequential validation owner after Main accepts each candidate.

## 0. Coordination decision

The Wave 15 post-FC0A implementation model is now:

`NORMAL DEV CHAT implements code -> MAIN audits candidate -> same DEV corrects material defects -> CODEX validates accepted candidate locally and with T1 -> MAIN finalizes integration`

Important consequences:

- the normal DEV chats are primarily **code implementers**, not full validation engineers;
- DEV should still keep its candidate buildable and run cheap/focused evidence when practical;
- Main owns architecture/contract/scope review before scarce CODEX validation time is spent;
- CODEX is primarily the sequential validation owner for accepted candidates: focused/adversarial tests, local execution, exact-head T1, and small validation-driven corrections;
- a material product/design defect found by Main or CODEX returns to the owning DEV rather than turning CODEX into the feature implementer;
- a frozen-contract deficiency stops as `BLOCKED-CONTRACT -> MAIN`;
- CODEX remains scarce/sequential; parallel coding is not capped at four lanes by policy.

The old wording `normally no more than four active coding DEVs` was a historical operational throttle requested for a different context. It is not a permanent Wave 15 architecture constraint.

## 1. Common lane state machine

Feature DEV lanes:

`PREPARED -> ACTIVE_CODING -> DEV_CANDIDATE -> MAIN_REVIEW -> DEV_CORRECTION(if needed) -> MAIN_ACCEPTED_FOR_CODEX -> CODEX_VALIDATION -> T1_GREEN -> READY_TO_INTEGRATE -> INTEGRATED_PENDING_T2 -> T2_VERIFIED`

Foundation DEV lanes:

`PREPARED -> ACTIVE_CODING -> DEV_CANDIDATE -> MAIN_CONTRACT_REVIEW -> DEV_CORRECTION(if needed) -> MAIN_ACCEPTED_FOR_CODEX -> CODEX_ADVERSARIAL_VALIDATION -> T1_GREEN -> READY_TO_INTEGRATE -> INTEGRATED -> POST_MERGE_VALIDATED -> VERIFIED/FROZEN`

FND-05 additionally requires `CODEX_HA_ADVERSARIAL_GREEN` before Main integration approval.

No lane self-merges or self-freezes.

## 2. Common delivery contract for normal DEV chats

Each DEV works in one isolated branch and one PR to `wave15/corrections-integration`.

At activation Main supplies:
- exact common FC0-A base SHA/tree;
- branch name;
- active order ID;
- owned scope;
- shared/frozen surfaces to consume;
- forbidden surfaces;
- current integration hotspots.

DEV responsibilities:
1. implement the owned product scope;
2. preserve all frozen Foundation contracts;
3. avoid opportunistic edits in shared hotspots;
4. keep code buildable;
5. run cheap/focused local checks when practical, but do not spend the mission rebuilding the full validation harness unless implementation itself requires it;
6. commit coherent work and open/update the lane PR;
7. return exact head/tree, changed files, implemented acceptance items, local evidence, known gaps and explicit non-actions.

DEV must not claim a broad validation PASS merely because it did not run a test. Unexecuted required validation is `PENDING_FOR_CODEX`.

### Main review rule

Main reviews code/architecture/contract before CODEX validation.

If Main finds a material implementation gap:
`MAIN -> DEV_CORRECTION`

If Main accepts the implementation direction:
`MAIN_ACCEPTED_FOR_CODEX`

Only then does the candidate enter the scarce sequential CODEX queue.

### CODEX validation rule

CODEX validates the exact accepted candidate/PR.

CODEX may:
- write/extend focused tests;
- run local build/test/browser/adversarial evidence;
- make small validation-driven fixes that do not redesign the feature;
- rerun the natural Wave 15 T1 on the exact final head;
- update the PR with exact evidence.

CODEX must return a material product/design defect to Main for routing back to the owning DEV.

CODEX must not silently redefine a frozen Foundation contract to make a test pass.

## 3. Integration and package strategy

### 3.1 Do not combine raw DEV implementations into one monolithic PR

Each DEV/FND keeps its own branch and PR.

Reasons:
- preserve ownership and causality;
- make review and rollback bounded;
- make regressions attributable;
- avoid one failing validation obscuring which lane introduced the defect.

### 3.2 Feature lanes consolidate at integrated validation, not source ownership

After individually reviewed/CODEX-validated/T1-green feature PRs are merged in controlled order, Main runs the broader integrated **T2** on the exact integration head.

Possible integration waves:
- Editor + Script Engineering;
- Authority UX + Licensing UX;
- or all four if readiness is close and collision risk is low.

Main chooses merge order from live file overlap and contract risk.

### 3.3 Foundations remain individually frozen

FND-05 and FND-07 are not bundled into the four-feature T2 package as raw development.

Each Foundation follows its own:
`Main contract review -> CODEX validation -> exact-head T1 -> merge -> post-merge validation -> VERIFIED/FROZEN`

FC0-B is reached only after FND-05 and FND-07 are both independently frozen.

If both Foundation candidates are ready near the same time, implementation may remain parallel while validation/integration is serialized. Default preference when risk is otherwise equal: validate/integrate FND-07 first, then FND-05, because FND-05 introduces the broader HA/topology authority surface. Main may reverse this if live evidence makes that safer.

## 4. Shared hotspot policy

Primary parallel ownership from the existing FC0-A collision audit remains binding.

Main-coordinated hotspots:
- `web/scada-web/src/engineering/EngineeringApp.tsx`;
- common Engineering/product `types.ts`;
- shared App/router/shell;
- common localization resource/index files;
- common CSS/layout shells;
- CI/workflow files;
- any frozen Foundation authority/registry/evaluator used by multiple lanes.

Rules:
1. prefer lane-local composition;
2. if a hotspot is necessary, DEV identifies the exact minimal need in its handoff/PR;
3. Main serializes that wiring or assigns a bounded follow-up;
4. no broad refactor of shared hotspots while parallel PRs are active.

## 5. Prepared lane — DEV-EDITOR

`ORDER_ID: DEV-EDITOR-FC0A-01`

`STATE: ACTIVE_CODING / EXACT_FC0A_BASE_RECORDED`

`PLANNED_BRANCH: work/w15-dev-editor-single-canvas`

`TARGET: wave15/corrections-integration`

`VALIDATION_PROFILE: UI_EDITOR, RUNTIME_RENDERER`

Parent/authority:
- Issue #303;
- frozen FND-06 canonical renderer, centralized known-legacy Engineering compatibility, Working-vs-Active separation and navigation/projection authority.

Primary ownership:
- `web/scada-web/src/engineering/visual-editor/**`;
- focused Editor authoring/UI surfaces that are lane-local.

Mission:
- converge to one primary WYSIWYG canvas instead of a permanently stacked second-preview workflow;
- direct manipulation and deterministic selection;
- Outliner <-> canvas synchronization;
- Properties/binding authoring UX;
- move/resize/group/lock/z-order/alignment/distribution/size flows;
- grid/snap/zoom/pan and canvas density/usability;
- Screen/Popup authoring parity where the canonical model supports it;
- preserve known persisted legacy compatibility by consuming FND-06 rather than duplicating it.

Activation-time revalidation:
- subtract any bounded Editor fixes already integrated by the FC0-A consolidated package;
- do not reimplement those merely because they appeared in the historical mission.

Forbidden:
- second renderer;
- second legacy compatibility registry;
- collapse Working into Active;
- process writes from ordinary design mode;
- redesign of FND-06 public visual authority.

DEV delivery:
- product candidate/PR;
- coherent UI behavior;
- cheap focused checks where practical;
- acceptance mapping with unexecuted broad scenarios marked `PENDING_FOR_CODEX`.

CODEX validation emphasis:
- mounted 12-scenario Editor matrix from #303;
- legacy known-type/unknown containment;
- renderer parity;
- interaction/history/persistence regressions;
- exact-head T1.

Handoff prefix:
`DEV-EDITOR -> MAIN COORDINATOR — CANDIDATE HANDOFF`

## 6. Prepared lane — DEV-SCRIPT-ENGINEERING

`ORDER_ID: DEV-SCRIPT-ENGINEERING-FC0A-01`

`STATE: ACTIVE_CODING / EXACT_FC0A_BASE_RECORDED`

`PLANNED_BRANCH: work/w15-dev-script-engineering`

`TARGET: wave15/corrections-integration`

`VALIDATION_PROFILE: SCRIPT_ENGINEERING, SCRIPT_RUNTIME`

Parent/authority:
- Wave 15 W15-P1-05;
- frozen FND-04 readable TAG reference <-> stable TagId binding/resolution;
- frozen FND-06 visual property compatibility surface.

Primary ownership:
- `web/scada-web/src/engineering/scripts/**`;
- `web/scada-web/src/engineering/python-editor/**`;
- lane-local Script Engineering UX.

Mission:
- complete practical event-aware authoring for supported events;
- event-specific fields and deterministic stale/hidden-field clearing;
- Object/TAG/property discovery and usable autocomplete/assistant flows;
- cursor-safe/composable snippet insertion where residual gaps remain;
- useful validation/diagnostics;
- property targeting against the canonical/FND-06 Engineering schema;
- representative UI-only multi-step Script Engineering workflows.

Activation-time revalidation:
- FC0-A consolidated work has already brought forward parts of Script Assistant compatibility and structured API Help/recipe;
- inspect the exact FC0-A base and continue from what is actually present;
- do not recreate already-good cursor insertion, timer/tagChanged controls, API Help or recipe unless live evidence shows a residual defect.

Forbidden:
- new TAG resolver;
- FND-04 identity-binding reinterpretation;
- Server Script sandbox/runtime ownership redesign;
- Authority/HA contract mutation.

DEV delivery:
- product authoring candidate;
- UX/diagnostic behavior;
- focused local sanity where practical;
- remaining test obligations explicitly marked for CODEX.

CODEX validation emphasis:
- readable TAG binding regressions;
- identity-drift/path-reuse negatives;
- mounted event authoring;
- Python syntactic composition;
- Script Assistant/object-property compatibility;
- Script runtime boundary regressions;
- exact-head T1.

Handoff prefix:
`DEV-SCRIPT-ENGINEERING -> MAIN COORDINATOR — CANDIDATE HANDOFF`

## 7. Prepared lane — DEV-AUTHORITY-UX

`ORDER_ID: DEV-AUTHORITY-UX-FC0A-01`

`STATE: ACTIVE_CODING / EXACT_FC0A_BASE_RECORDED`

`PLANNED_BRANCH: work/w15-dev-authority-ux`

`TARGET: wave15/corrections-integration`

`VALIDATION_PROFILE: AUTHORITY_UX`

Parent/authority:
- Issue #302;
- frozen FND-02/AUTH-04 capability/scope/effective-permission semantics.

Primary ownership:
- `web/scada-web/src/engineering/UserAdministration.tsx`;
- `web/scada-web/src/engineering/userAdministrationApi.ts`;
- `web/scada-web/src/engineering/userAdministration.css`;
- lane-local administration components.

Mission:
- Users + role/profile assignment UX;
- protected role CRUD;
- capability grouping;
- hierarchy/scope selector;
- truthful effective-permission preview;
- truthful multi-role/inheritance UX;
- clear backend-denial feedback without implying frontend authority.

Forbidden:
- role-name-based privilege;
- backend evaluator/capability/scope redesign;
- Runtime Session Class becoming an Authority role;
- secret/password exposure;
- client-side authorization authority.

DEV delivery:
- administration UX candidate;
- error/denial handling;
- focused local sanity;
- explicit list of backend-authoritative cases left for CODEX validation.

CODEX validation emphasis:
- direct API tampering;
- capability/scope denial;
- multi-role/effective permission truth;
- unauthorized mutation negatives;
- mounted pt-BR/en/es behavior as applicable;
- exact-head T1.

Handoff prefix:
`DEV-AUTHORITY-UX -> MAIN COORDINATOR — CANDIDATE HANDOFF`

## 8. Prepared lane — DEV-LICENSING-UX

`ORDER_ID: DEV-LICENSING-UX-FC0A-01`

`STATE: ACTIVE_CODING / EXACT_FC0A_BASE_RECORDED`

`PLANNED_BRANCH: work/w15-dev-licensing-ux`

`TARGET: wave15/corrections-integration`

`VALIDATION_PROFILE: LICENSING_UX, SESSION_LICENSING`

Parent/authority:
- Issue #301;
- frozen FND-03 machine-license v2, Runtime Session Lease, requested/granted class, Authority ceiling and shared Web+EliteGO quota semantics.

Primary ownership:
- `web/scada-web/src/licensing/**`;
- `src/Scada.LicenseGenerator/**`;
- explicitly delegated Runtime Session Class UX.

Mission:
- License Generator v2 user-facing fields/workflow;
- licensing status and entitlement UX;
- explicit ViewOnly request;
- truthful requested vs server-granted Runtime class;
- Interactive quota -> ViewOnly fallback/reason UX;
- backward/new-schema user experience;
- coherent diagnostics around install/replace/remove without changing backend authority.

Activation-time revalidation:
- the FC0-A consolidated package has already brought forward part of requested/granted/ViewOnly/fallback UX and fallback-lease lifecycle;
- inspect the exact FC0-A base and complete only residual product maturity.

Forbidden:
- private signing-key boundary changes;
- client-owned entitlement/quota pool;
- session admission authority redesign;
- HA fencing invention;
- treating Session Class as Authority.

DEV delivery:
- UI/generator candidate;
- no secret material;
- focused local sanity;
- exact residual validation matrix for CODEX.

CODEX validation emphasis:
- old/new license compatibility;
- hardware/signature negative paths already owned by backend;
- requested/granted/fallback truth;
- ViewOnly fail-closed mutation behavior;
- logical lease accounting;
- exact-head T1.

Handoff prefix:
`DEV-LICENSING-UX -> MAIN COORDINATOR — CANDIDATE HANDOFF`

## 9. Prepared Foundation lane — FND-07 DEV

`ORDER_ID: FND07-DEV-DETACH-NEUTRAL-V1`

`STATE: ACTIVE_CODING / EXACT_FC0A_BASE_RECORDED`

`PLANNED_BRANCH: work/w15-fnd-07-detach-neutral`

`TARGET: wave15/corrections-integration`

`EXECUTOR: NORMAL CHAT DEV`

`VALIDATION_OWNER: SEQUENTIAL CODEX AFTER MAIN CONTRACT REVIEW`

Authority:
- dedicated FND-07 control remains canonical for technical contract;
- frozen FND-01 lifecycle/bootstrap;
- frozen FND-02 Authority;
- frozen FND-03 licensing/session/fencing primitives.

DEV mission:
- implement the protected detach/preflight state machine and API;
- explicit consequence acknowledgement;
- fence current Runtime/process effects before authority removal;
- application detach;
- Authority detach/replace orchestration using existing protected primitives;
- invalidate old Authority-A sessions/tokens;
- finish in truthful neutral bootstrap supporting Create / Import / Restore;
- license keep/remove/replace only through frozen FND-03 authority;
- preserve Historian by default;
- reconcile Engineering Lock × detach/switch/neutral-bootstrap exactly as required by the dedicated control.

Forbidden:
- anonymous populated-install takeover;
- direct DB deletion/hacks;
- silent Historian deletion;
- license or Authority credentials in package;
- lifecycle/Authority/licensing semantic rewrite;
- hidden Demo project.

DEV delivery:
- implementation candidate and focused sanity;
- acceptance matrix with robust tests allowed to remain `PENDING_FOR_CODEX`;
- exact list of orchestration/fencing assumptions.

Main contract review:
- lifecycle separation;
- Authority atomicity/session invalidation;
- neutral bootstrap truth;
- license independence;
- Engineering Lock interaction.

CODEX validation emphasis:
- unauthorized detach;
- unsaved Working preflight;
- fence-before-removal;
- stale Project-A write rejection;
- A->B->A switching;
- license keep/remove/invalid replacement;
- Historian preservation;
- Engineering Lock locked/unlocked matrix;
- local focused validation then natural exact-head T1.

Handoff prefix:
`FND-07 DEV -> MAIN COORDINATOR — CANDIDATE HANDOFF`

## 10. Prepared Foundation lane — FND-05 DEV

`ORDER_ID: FND05-DEV-HA-AUTHORITY-V1`

`STATE: ACTIVE_CODING / EXACT_FC0A_BASE_RECORDED`

`PLANNED_BRANCH: work/w15-fnd-05-ha-authority`

`TARGET: wave15/corrections-integration`

`EXECUTOR: NORMAL CHAT DEV`

`VALIDATION_OWNER: SEQUENTIAL CODEX AFTER MAIN CONTRACT REVIEW`

Authority:
- dedicated FND-05 control remains canonical for technical contract;
- frozen FND-03 logical Runtime Session Lease/licensing;
- frozen FND-04 Script TAG identity contract;
- frozen FND-06 renderer/visual authority.

DEV mission first slice:
- versioned Cluster/Node/topology contract;
- HA state/readiness;
- effective-Active industrial ownership authority;
- break-before-make manual transfer skeleton + Audit;
- epoch/fencing contract sufficient to deny ambiguous/stale ownership;
- Runtime Session continuity/replication contract boundary;
- deterministic two-node implementation/harness support;
- no automatic failover breadth beyond the dedicated order unless Main widens later.

Forbidden:
- client-side election;
- Driver-owned HA authority;
- topology/session/fencing state in `.escadapkg`;
- peer-loss-implies-promotion;
- duplicate industrial effects;
- licensing/session/FND-04/FND-06 semantic rewrite;
- unsafe automatic failover.

DEV delivery:
- implementation candidate;
- compile/focused sanity;
- exact topology/fencing/state-machine explanation;
- acceptance rows not fully exercised locally marked `PENDING_FOR_CODEX`.

Main contract review:
- single effective Active;
- break-before-make transfer;
- fencing/epoch semantics;
- additive licensing compatibility;
- lease identity continuity;
- no duplicate side-effect authority.

CODEX adversarial validation is mandatory and owns:
- deterministic two-node test harness completion if needed;
- stale epoch/fencing negative;
- split-brain/ambiguous fail-closed;
- peer-loss no-promotion;
- incompatible revision/license readiness rejection;
- one logical lease across A/B reconnect;
- expired lease no resurrection;
- no duplicate Alarm/Historian/Script/Event/process effects;
- package topology neutrality;
- natural T1 plus any broader HA validation required by live diff.

Handoff prefix:
`FND-05 DEV -> MAIN COORDINATOR — CANDIDATE HANDOFF`

## 11. Activation and bootstrap rule

All six lanes are intentionally prepared now so the Product Owner can organize parallel ChatGPT chats before release.

A prepared chat may:
- read its control;
- inspect GitHub read-only;
- understand its future mission;
- remain ready.

It may **not** mutate product until Main changes its lane state to ACTIVE and supplies the exact FC0-A base SHA/tree.

The Product Owner may request a bootstrap text for any lane later. Bootstrap text is an onboarding convenience only; GitHub live control remains authoritative.

At activation Main must:
1. revalidate final FC0-A release approval;
2. record exact FC0-A SHA/tree;
3. revalidate what the consolidated FC0-A package already closed so lanes do not duplicate work;
4. create the lane branch from that exact SHA;
5. set the lane ACTIVE in the appropriate control;
6. publish the activation in #305;
7. identify any current shared-hotspot collision with other active lanes.

## 12. CODEX queue policy

CODEX remains a sequential scarce resource.

Priority is based on candidate readiness + risk, not on who started coding first.

Normal pattern:
- while CODEX validates one candidate, other DEV chats continue implementation/corrections;
- Main does not send an obviously incomplete candidate to CODEX;
- feature validation can queue behind the current FC0-A consolidation;
- Foundation candidates receive adversarial validation before freeze;
- FND-05 receives the strongest concurrency/fencing validation.

This control prepares coordination only. It does not supersede the active FC0-A consolidated CODEX order.


## 13. Dedicated chat control files

Feature DEV chats should use these dedicated controls:

- DEV-EDITOR -> `docs/WAVE15-DEV-EDITOR-CONTROL.md`
- DEV-SCRIPT-ENGINEERING -> `docs/WAVE15-DEV-SCRIPT-ENGINEERING-CONTROL.md`
- DEV-AUTHORITY-UX -> `docs/WAVE15-DEV-AUTHORITY-UX-CONTROL.md`
- DEV-LICENSING-UX -> `docs/WAVE15-DEV-LICENSING-UX-CONTROL.md`

Foundation chats use their dedicated existing controls:

- FND-05 DEV -> `coord/w15-fnd05-control:docs/WAVE15-FND05-CONTROL.md`
- FND-07 DEV -> `coord/w15-fnd07-control:docs/WAVE15-FND07-CONTROL.md`

A future bootstrap prompt should point the chat to its dedicated control first and to this cross-lane control second.

While the lane is PREPARED/WAIT, the chat may initialize, read GitHub live and understand the mission, but must not create/mutate the product branch until Main activates it with an exact base SHA/tree.


## 14. Six-lane phase exit -> first-project fresh-install partial preview

Completion of the six prepared implementation lanes does not go directly to a prebuilt EEE-only audit.

After:
- the four feature lanes are integrated and T2-verified; and
- FND-05/FND-07 are independently VERIFIED/FROZEN;

Main prepares the exact checkpoint for:

`coord/w15-fresh-install-preview-control:docs/WAVE15-FIRST-PROJECT-FRESH-INSTALL-PREVIEW-CONTROL.md`

That preview has two independent user-like moments:
1. CODEX black-box fresh-install / first-project audit;
2. Product Owner human fresh-install / first-project audit on a separate reset environment.

Detailed CODEX findings are intentionally not shown to the Product Owner before the human journey completes.

This audit is meant to reveal end-to-end product birth/discoverability gaps before later Installation UX, EliteGO, EEE v15 and final complete-product acceptance.


## 15. Live queue — Script Engineering candidate in CODEX validation

Main accepted the first post-FC0A feature candidate for sequential CODEX validation.

`LANE: DEV-SCRIPT-ENGINEERING`

`STATE: MAIN_ACCEPTED_FOR_CODEX`

`PR: #344`

`CANDIDATE_SHA: cf0ae2d1dcd2d63668b5b1c2c3590a5b6bb9bdaa`

`CANDIDATE_TREE: 0820a2a00f36bcc13a05659d85cc43ee7f266d24`

`VALIDATION_PROFILE: SCRIPT_ENGINEERING, SCRIPT_RUNTIME`

Main review found the candidate bounded to five lane-owned Script Engineering files and compatible with frozen FND-04/FND-06/backend event rules.

The live integration branch has advanced from the common FC0-A release base only by coordination/documentation files, so there is no product/infra overlap requiring a DEV rebase.

Initial T1 `36063109199` is metadata-invalid only: the PR profile declaration was missing, the classifier failed before product evidence, and Main corrected the PR body without source mutation.

Shared CODEX route:
- route rev 0037;
- `ROUTE-SEQUENTIAL-CODEX-TO-SCRIPT-ENGINEERING-25`;
- route commit `d5f6d4c6aa682db1f633f61396d46a1fe0eb427d`.

While CODEX validates this lane:
- DEV-SCRIPT-ENGINEERING is `DEV_WAIT`;
- the other five lanes remain ACTIVE_CODING unless their own dedicated controls say otherwise;
- no lane is blocked merely because CODEX is occupied;
- next CODEX priority remains Main-owned based on candidate readiness + risk.


## 16. Live six-lane board after first Main review pass

This board is a coordination summary only; each dedicated control remains authoritative for its lane.

### DEV-SCRIPT-ENGINEERING
- PR #344
- exact accepted head `cf0ae2d1dcd2d63668b5b1c2c3590a5b6bb9bdaa`
- state: `MAIN_ACCEPTED_FOR_CODEX / DEV_WAIT`
- shared CODEX route: `ROUTE-SEQUENTIAL-CODEX-TO-SCRIPT-ENGINEERING-25`
- CODEX is currently active here.
- original T1 `36063109199` was metadata-invalid before product evidence.

### DEV-EDITOR
- PR #349
- exact head `06eed31d99ddb34d99e0287e96e38bed3bf7dab5`
- T1 `36066874097`: SUCCESS
- state: `MAIN_ACCEPTED_FOR_CODEX / QUEUED / DEV_WAIT`
- dedicated control rev 0003 / commit `57832a87eef65215845e46d3d55b1e2f82faaebc`.
- must not preempt the active Script CODEX route.

### DEV-AUTHORITY-UX
- PR #346
- reviewed head `3986475b20e4a72969159bfaed003ad5d72626d4`
- state: `DEV_CORRECTION / STABLE_ROLE_KEY_IDENTITY`
- order `DEV-AUTHORITY-UX-STABLE-ROLE-KEY-02`
- control rev 0003 / commit `93dd6d66c78d5ec13133f581fe3c695e6dee8552`.
- baseline role keys must remain stable because local user assignments persist by role key.

### DEV-LICENSING-UX
- PR #345
- reviewed head `cdf572d644417fe83aee3003a3da3fe171d7ada3`
- state: `DEV_CORRECTION / LICENSE_STATUS_ENTITLEMENT_TRUTH`
- order `DEV-LICENSING-UX-STATUS-ENTITLEMENTS-02`
- control rev 0003 / commit `a791f3950a83d6f94f5bee6cbd1bb3b573177ac3`.
- Licensing status must expose truthful ESLIC1/ESLIC2 schema and signed ESLIC2 Interactive/ViewOnly/HA entitlements.

### FND-05 DEV
- PR #347
- reviewed head `8c2bd2724b7f17711d76c59919f68ed7037483a3`
- T1 `36064607662`: SUCCESS
- state: `DEV_CORRECTION / PEER_HANDOFF_BOUNDARY_REQUIRED`
- order `FND05-DEV-PEER-HANDOFF-BOUNDARY-V2-01`
- dedicated control rev 0006 / commit `e6de4aa2dee0323db95135b08d90c3633f9a954b`.
- correction is transport-neutral two-independent-node readiness/authority/lease handoff; no network/consensus/automatic-failover scope.

### FND-07 DEV
- PR #348 (draft)
- reviewed head `ae11e42e8ad5e39b1e2c5a0068f81e4ec31653c6`
- state: `DEV_CORRECTION / FRESH_INSTALL_NO_DEMO_TEST_CONTRACT`
- order `FND07-DEV-FRESH-INSTALL-NO-DEMO-E2E-01`
- dedicated control rev 0005 / commit `bc1f74056725219ed04225335bbc7695cb70b646`.
- T1 `36066422907` proved a stale Demo-dependent E2E fixture; product must keep clean no-Demo fresh-install/neutral semantics.

### Integration / target truth

Current feature/Foundation branches remain independently owned.

Do not merge raw candidates merely because a lane T1 is green.

Sequential CODEX remains one-at-a-time:
1. active Script Engineering validation;
2. Editor is the first currently Main-accepted queued candidate;
3. later priority may be reassessed when corrected Authority/Licensing/FND candidates return.

FND-05 and FND-07 still require Foundation-specific Main contract review, CODEX validation and post-merge VERIFIED/FROZEN before the six-lane phase exit gate.


## 17. Shared CI diagnosis / CODEX queue update

### Authority UX old-head T1

PR #346 old reviewed head:
`3986475b20e4a72969159bfaed003ad5d72626d4`

Natural T1:
`36067636970`

Evidence:
- classifier SUCCESS;
- Common sanity SUCCESS;
- focused .NET SUCCESS;
- focused Chromium SUCCESS;
- Web build FAILURE.

Exact candidate-causal TypeScript failure:
- `AuthorityPolicyAdministration.logic.ts(16,63) TS2345`;
- `AuthorityPolicyAdministration.logic.ts(18,99) TS2345`;
- generic `number` lookup against a Map inferred with literal capability keys.

This compile defect is folded into the already-open Authority DEV correction:
`DEV-AUTHORITY-UX-STABLE-ROLE-KEY-02`.

Do not rerun the unchanged old head.

### Licensing UX old-head T1

PR #345 old reviewed head:
`cdf572d644417fe83aee3003a3da3fe171d7ada3`

Natural T1:
`36067628680`

Evidence:
- classifier SUCCESS;
- Common sanity SUCCESS;
- Web SUCCESS;
- Chromium SUCCESS;
- focused .NET 692/693.

Only failure:
`Iec104TcpFaultInjectionTests.Adapter_OutOfOrderIFrameFaultsBeforePublishingAsdu`.

Main proved the exact test, adapter and sequence-state blobs are unchanged from FC0-A release.

The test waits only for `ProtocolErrors >= 1`; production increments that counter before `SignalSessionFailure` writes disconnected/session-failure state. The test can therefore observe the intermediate state `ProtocolErrors=1 / IsConnected=true`.

Classification:
`IEC104_TEST_OBSERVATION_RACE / NOT_LICENSING_CAUSAL / SHARED_TEST_INFRA_DEFECT`.

Licensing stays on its own product correction:
`DEV-LICENSING-UX-STATUS-ENTITLEMENTS-02`.

Separate prepared closeout:
- `INFRA-CI-01D-IEC104-FAULT-OBSERVATION-RACE-V1`;
- control commit `b23cefeaf78a58ea17eaba8cf9a1566f3095ada4`;
- state `PREPARED / NO MUTATION`.

### Sequential CODEX planning

Active route remains Script Engineering PR #344.

Planned queue after Script handoff:
1. INFRA-CI-01D test-only shared-gate stabilization;
2. Editor PR #349 exact Main-accepted candidate.

This is queue planning, not an activation order. Main must publish a new explicit route before CODEX changes mission.


## 18. Replacement Main takeover — live board after post-transfer returns

GitHub live revalidation supersedes the transfer snapshot for any lane whose head advanced.

Integration target:
- `wave15/corrections-integration` live head at takeover: `3480ed03a6719aef38e4c1ced2f466aaa4fa10b3`;
- exact FC0-A product release base remains `e3ed5138369c576549cb58a7aff9783792f322d3`;
- compare FC0-A -> takeover integration head: 14 commits, changes only the five canonical coordination/documentation files;
- therefore no post-FC0A product/infra candidate has been integrated and no lane rebase is required solely by that documentation advance.

### DEV-SCRIPT-ENGINEERING
- PR #344;
- accepted head `cf0ae2d1dcd2d63668b5b1c2c3590a5b6bb9bdaa`;
- state `MAIN_ACCEPTED_FOR_CODEX / ACTIVE_SHARED_CODEX_ROUTE / DEV_WAIT`;
- shared route remains `ROUTE-SEQUENTIAL-CODEX-TO-SCRIPT-ENGINEERING-25`;
- no post-transfer CODEX validation handoff has been published yet.

### DEV-EDITOR
- PR #349;
- head `06eed31d99ddb34d99e0287e96e38bed3bf7dab5`;
- T1 `36066874097`: SUCCESS;
- state `MAIN_ACCEPTED_FOR_CODEX / QUEUED / DEV_WAIT`.

### DEV-LICENSING-UX
- PR #345;
- corrected head `f5d3212b9c114d3ad6e2239460172db0b3d568f8`;
- tree `a3e87f2ef7beb15bc66d6980e12710eddcc30525`;
- T1 `36071779912`: SUCCESS;
- Main accepted the corrected schema/signed-entitlement truth;
- state `MAIN_ACCEPTED_FOR_CODEX / QUEUED / DEV_WAIT`;
- dedicated control rev 0004 / commit `a4a4098f7c65863bb5cc14a9b566f67aee9f7884`.

### DEV-AUTHORITY-UX
- PR #346;
- corrected head `9fd2462f43c74b085e58be91dbec9ebdf18514c5`;
- tree `bdd7ac76c3566aef81e249a9db91810674b7dfe8`;
- stable-role-key direction accepted;
- T1 `36071729747`: FAILURE only at Web semantic build on candidate-causal nullable-baseline TS2345;
- state remains `DEV_CORRECTION` under `DEV-AUTHORITY-UX-STABLE-ROLE-KEY-02`;
- dedicated control rev 0004 / commit `5e2804963ea919d9224ace341f279660804437ad`;
- unchanged-head rerun is forbidden.

### FND-05
- PR #347;
- corrected head `be9cf0f3f02aa1ba49cdb6b589abd5e1e723c845`;
- tree `c9e94e84c078928ad690217484171fafa6390b46`;
- T1 `36072325579`: SUCCESS;
- transport-neutral two-independent-service peer readiness/authority/lease handoff boundary accepted directionally;
- state `MAIN_ACCEPTED_FOR_CODEX_HA_ADVERSARIAL / QUEUED / DEV_WAIT`;
- dedicated control rev 0007 / commit `a3f69a286a62028b49b0809025c45b585ff698f8`;
- `CODEX_HA_ADVERSARIAL_GREEN` remains mandatory before integration.

### FND-07
- PR #348 remains DRAFT;
- head remains `ae11e42e8ad5e39b1e2c5a0068f81e4ec31653c6`;
- state `DEV_CORRECTION / FRESH_INSTALL_NO_DEMO_TEST_CONTRACT`;
- order `FND07-DEV-FRESH-INSTALL-NO-DEMO-E2E-01`;
- no corrected post-transfer head exists yet.

### Sequential CODEX queue

Binding active mission remains Script Engineering #344. Do not infer a reroute from candidate readiness.

Prepared/accepted waiting work now includes:
- INFRA-CI-01D — PREPARED ONLY / NO MUTATION;
- Editor #349 — Main accepted;
- Licensing #345 — Main accepted;
- FND-05 #347 — Main accepted but requires HA adversarial validation.

Main will select the next route only after Script returns, after revalidating live integration/candidates and explicit risk/priority. A new shared route is mandatory before the CODEX executor changes mission.

Fresh-install first-project preview remains NOT ACTIVE.


## 19. FND-07 post-transfer correction return

FND-07 advanced after the rev 0005 takeover board:
- PR #348 remains DRAFT;
- corrected head `af7bf1ae51539975b3b3e8b40ea36472249ade2e`;
- tree `0947177b314dbcbaaebb3ae8ba65eb2fa3498c49`;
- correction is test-only in `local-auth.spec.ts`;
- no-Demo pre-project and genuinely-empty first-project assertions now execute successfully;
- natural T1 `36072685058` is still red only because the same historical fixture expects `developer + operator` while the truthful clean bootstrap endpoint returns one `developer` role.

State remains:
`DEV_CORRECTION / FRESH_INSTALL_NO_DEMO_TEST_CONTRACT`

Binding order remains:
`FND07-DEV-FRESH-INSTALL-NO-DEMO-E2E-01`

Dedicated control rev 0006:
`bc7e3f810b7534d1180196c1e768f4c92a1c60a5`.

No unchanged-head rerun, CODEX route, merge or freeze is authorized.


## 20. Authority control normalization after coordinator inconsistency

Main found and corrected a coordination defect in the dedicated Authority control: two sections were simultaneously labeled as current after the coordinator takeover.

Canonical Authority state is now unambiguous:

- PR #346;
- head `d96e685daf7ddb190cefc67c6ba975d71a779a52`;
- tree `184f8a49bdf15a34ce7ba96bc99168f5152a4fef`;
- natural T1 `36076143618`: SUCCESS;
- state `MAIN_ACCEPTED_FOR_CODEX / QUEUED / DEV_WAIT`;
- current order `DEV-AUTHORITY-UX-CODEX-QUEUE-04`;
- dedicated control rev 0005 / commit `28f152607e01ac5964b2f4ccc2abd37f5d4769d9`.

All older Authority correction-order sections are historical/superseded only.

The shared sequential CODEX route remains Script Engineering #344. Authority acceptance does not reroute CODEX or authorize merge/T2.


## 21. Script integrated / INFRA-CI-01D activated

Script Engineering PR #344 completed CODEX validation and was merged.

Final Script candidate:
- head `79d43f1994acb509628b08e703c20820bf8a9c72`;
- tree `c5e85db0c176d9dc070f20ff79ce81d7894f4ccc`;
- T1 `36078781412`: SUCCESS.

Integration:
- merge SHA `3b1511799a73c3c4fee1c2265005d6724bcaa235`;
- tree `bb9b1fb4b9a7ffe47d9eb56eaee6dd8ba0e300fe`;
- state `SCRIPT_ENGINEERING -> INTEGRATED_PENDING_T2`.

No broader post-merge T2 verification is claimed yet.

Sequential CODEX is now explicitly routed to INFRA-CI-01D:
- route `ROUTE-SEQUENTIAL-CODEX-TO-INFRA-CI-01D-26`;
- exact activation base `3b1511799a73c3c4fee1c2265005d6724bcaa235`;
- work branch `work/w15-infra-ci-01d-iec104-fault-observation-race`;
- control rev 0002 / commit `47437359cb33a47faa3db6df5758d4340843e4ea`;
- shared route control rev 0040 / commit `4fbd0c2aef92ae2362376fbcaed624e5d4e8f074`.

Editor, Authority, Licensing and FND-05 remain queued/waiting. FND-07 remains the only normal DEV still authorized to continue its bounded correction independently.


## 22. INFRA-CI-01D merged / Editor CODEX active / FND-07 continues correction

INFRA-CI-01D:
- PR #350;
- final head `925070a1fb0c35a28266115f57856791f403b425`;
- T1 `36079753043`: SUCCESS;
- full local Drivers 693/693;
- merged at integration SHA `66694aac1408218a41d1251459c20465d990cfef`;
- state `INTEGRATED / CLOSED`.

Sequential CODEX is now explicitly routed to Editor:
- route `ROUTE-SEQUENTIAL-CODEX-TO-EDITOR-27`;
- Editor candidate `06eed31d99ddb34d99e0287e96e38bed3bf7dab5`;
- Editor control rev 0004 / `513912326b32683d60b8b65007361ca9b275839c`;
- shared route control rev 0041 / `9d2968122f8afb48b359ef51a9b362413358bb2a`.

FND-07 remains independently authorized on normal DEV correction:
- current head `3f90cc6d62d1ea9df0101e228cd28332d422fa08`;
- T1 `36079596118` red only on stale historical Authority roleIds cardinality;
- dedicated control rev 0007 / `f8f9a0a46961c92ad70573380415c83f74e988bb`;
- no product mutation is authorized by that failure.

Authority, Licensing and FND-05 remain queued/waiting.


## 23. Editor integrated / FND-05 HA adversarial CODEX active

Editor PR #349:
- final candidate `06eed31d99ddb34d99e0287e96e38bed3bf7dab5`;
- CODEX source delta: none;
- T1 `36066874097`: SUCCESS;
- merged at integration SHA `cfaafa4b29e1462bf9d304af995cc8638578a3c2`;
- state `INTEGRATED_PENDING_T2`.

Sequential CODEX now targets FND-05:
- route `ROUTE-SEQUENTIAL-CODEX-TO-FND05-HA-28`;
- candidate `be9cf0f3f02aa1ba49cdb6b589abd5e1e723c845`;
- mandatory `CODEX_HA_ADVERSARIAL_GREEN`;
- dedicated control rev 0008 / `ddfd84ab4f2522c255fa9a155f9ec14dfbbc8734`.

FND-07 remains independently authorized in DEV correction. Authority and Licensing remain queued/waiting. Script and Editor are both integrated pending broader T2.


## 24. FND-07 clean bootstrap passed farther / downstream E2E fixture gap

FND-07 advanced to:
- PR #348 head `72c22e0ea751222e9929af2fb86305f0da95204c`;
- tree `a318df054072aaffe5f7aae8efe0cdf7ab2eebea`;
- T1 `36084614406`.

Fresh-install assertions now progress past the prior Engineering-role and Authority-reference expectations. The remaining focused Chromium failure is downstream `runtime.spec.ts`, which still assumes the old shared Demo baseline with 7 TAGs.

State:
`DEV_CORRECTION / FRESH_INSTALL_NO_DEMO_TEST_CONTRACT`

Binding correction remains:
`FND07-DEV-FRESH-INSTALL-NO-DEMO-E2E-01`

Required direction is test-harness-only:
- preserve clean fresh-install proof;
- afterwards create/load an explicit test-owned populated baseline through supported APIs for legacy downstream Runtime/Security specs;
- never restore hidden product Demo bootstrap.

Dedicated control rev 0008.

FND-05 remains active in CODEX HA adversarial validation. Authority and Licensing remain queued. Script and Editor remain integrated pending T2.


## 25. FND-05 merged pending post-merge validation / FND-07 one fixture assertion remains

FND-05:
- final CODEX candidate `19c65423880719afc0163bf7fd741d2cd2d0377d`;
- T1 `36085565091`: SUCCESS;
- `CODEX_HA_ADVERSARIAL_GREEN`: SATISFIED;
- PR #347 merged at integration SHA `b2874a00c7f7b35ca8223defd7e3b6bbdd89ecf8`;
- state `INTEGRATED_PENDING_POSTMERGE_VALIDATION`;
- CODEX remains on FND-05 under route `ROUTE-SEQUENTIAL-CODEX-TO-FND05-POSTMERGE-29`.

FND-07:
- current head `da1f00a2db4dbd1125fa342aa5a41c5ec57edbdb`;
- explicit populated fixture direction accepted;
- T1 red only because the test conflated Authority role count with Engineering Workspace role count;
- state remains bounded DEV correction;
- dedicated control rev 0010.

Authority and Licensing remain Main-accepted/queued. Script and Editor remain integrated pending T2.


## 26. FND-07 exact-head T1 environment blocker / DEV WAIT_MAIN

FND-07 exact candidate remains:
- PR #348;
- head `a8fcfe8c855a692670e9c74a95f4450caf612b2f`;
- tree `6913879ae0a1d7cf1a69da7fe8f58b1eb0dfdab3`;
- source review ACCEPTED;
- no source mutation after the one-line role-count fix.

Exact-head T1 execution is environment-blocked:
- DEV has no workflow_dispatch capability and no `gh`;
- Main close/reopen and parent->head ref pulse preserved the candidate but did not create a new T1.

Canonical state:
`FND-07 -> WAIT_MAIN / NO_SOURCE_MUTATION`

Dedicated control rev 0012 / `7f7db7eb4e1e7a878a7624de2ec2de61e9d227bf`.

No further DEV action is expected until Main resolves CI execution.


## 27. FND-05 VERIFIED/FROZEN / FND-07 CODEX active / Editor test closeout separated

FND-05:
- integrated checkpoint `b2874a00c7f7b35ca8223defd7e3b6bbdd89ecf8`;
- HA post-merge 52/52 PASS;
- Drivers 715/715 PASS;
- isolated PostgreSQL advisory-lock 124/124 PASS;
- targeted Backend rerun on CI #1574: SUCCESS;
- remaining Chromium red classified as stale Editor contract test, non-causal to HA;
- state `VERIFIED / FROZEN`;
- control rev 0011 / `82d75e12c3e0a1e0c4f36e3a0c4e7b375cda932c`.

Separated infrastructure closeout:
- PR #351;
- branch `work/w15-infra-ci-01e-editor-surface-contract`;
- head `b83ca41cfde8b1424c0a8666d499e53bae179960`;
- test-only correction aligning legacy hardcoded 600/400 expectation with parameterized Editor CSS variables;
- natural T1 `36087895502` currently running.

Sequential CODEX is now routed to FND-07:
- route `ROUTE-SEQUENTIAL-CODEX-TO-FND07-30`;
- exact candidate `a8fcfe8c855a692670e9c74a95f4450caf612b2f`;
- FND-07 DEV remains WAIT_MAIN / no source mutation;
- CODEX owns exact-head T1 execution and focused/adversarial validation.

Authority and Licensing remain Main-accepted/queued. Script and Editor remain integrated pending broader T2.


## 28. Editor test closeout merged / FND-07 CODEX lifecycle fixture correction

Editor stale-contract closeout:
- PR #351;
- head `b83ca41cfde8b1424c0a8666d499e53bae179960`;
- T1 `36087895502`: SUCCESS;
- one test file only;
- merged at integration SHA `9895a01a662851505198b965fae2335e55fba6fa`;
- no product mutation.

FND-07 exact-head validation:
- candidate `a8fcfe8c855a692670e9c74a95f4450caf612b2f`;
- exact workflow-dispatch T1 `36088026405`;
- Common/Web/.NET green;
- Chromium red only because the explicit downstream fixture imports+saves but does not publish+activate the saved revision;
- product fresh-install/runtime authority semantics remain correct.

CODEX remains on FND-07 under `ROUTE-SEQUENTIAL-CODEX-TO-FND07-30`, authorized for a bounded test-only lifecycle completion:
`save -> publish exact revision -> activate published revision`.

FND-07 DEV remains WAIT_MAIN / no source mutation.
Authority and Licensing remain Main-accepted/queued.


## 29. FND-07 material restart-order defect returned to DEV

Main independently confirmed the CODEX investigation finding on exact candidate:
- PR #348;
- head `a8fcfe8c855a692670e9c74a95f4450caf612b2f`;
- tree `6913879ae0a1d7cf1a69da7fe8f58b1eb0dfdab3`.

The previous test-only lifecycle-gap classification is revoked.

Confirmed startup defect:
- Engineering persistence/bootstrap currently runs before canonical Authority policy hydration;
- persisted Working checkout can reach live `EngineeringExchangeService.Apply`;
- live apply validates immutable `AuthorityPolicyReference` against the current Authority snapshot;
- PostgreSQL Authority policy snapshot remains empty in memory until `AuthorityPolicyStore.InitializeAsync` is reached later through Authority bootstrap;
- valid persisted project restart can therefore fail before persisted Authority is loaded.

Canonical state:
`FND-07 -> DEV_CORRECTION / AUTHORIZED`

Binding order:
`FND07-DEV-AUTHORITY-BEFORE-ENGINEERING-RESTART-05`

Dedicated control rev 0015 / `86249242be5dc0d3e412bf472bc3fc9634ab4438`.

CODEX:
- material defect returned to DEV;
- shared route rev 0046 / `a213a78834b697a4a85df1d844fc51939519a4a2`;
- state `PAUSED / MATERIAL_DEFECT_RETURNED_TO_DEV / CODEX_USAGE_BLOCKED`.

FND-05 remains VERIFIED/FROZEN.
Authority and Licensing remain Main-accepted/queued.
Script and Editor remain integrated pending broader T2.


## 30. FND-07 candidate ed552418 rejected / DEV correction continues

FND-07 DEV advanced from `a8fcfe8c...` to:
- head `ed552418a95d8b7b086eb81ed42ac12d29efaefa`;
- tree `5329588c4e6bfeabd524d190a48ff0cfca95a8d0`.

Direction accepted:
- split Engineering storage initialization from Working checkout;
- hydrate Authority before persisted checkout;
- preserve fail-closed Authority reference validation.

Candidate rejected due deterministic startup defect:
- `InitializeEngineeringPersistenceStorageAsync` recursively calls itself instead of initializing the underlying persistence service.

Regression coverage is also incomplete for real startup ordering, fresh install and detached/neutral semantics.

State:
`FND-07 -> DEV_CORRECTION / AUTHORIZED`

Binding order remains:
`FND07-DEV-AUTHORITY-BEFORE-ENGINEERING-RESTART-05`

Dedicated control rev 0016.

CODEX remains paused / no action.
FND-05 remains VERIFIED/FROZEN.
Authority and Licensing remain Main-accepted/queued.
Script and Editor remain integrated pending broader T2.


## 31. FND-07 mandatory cumulative deep-audit gate

Product Owner requested elevated scrutiny before FND-07 can return to CODEX or integration.

Reason:
the lane accumulated several rapid correction cycles and later validation exposed gaps that were not visible from narrow incremental review.

Canonical rule:
- review the **entire cumulative FND-07 PR**, not only the latest patch;
- T1 green is necessary but insufficient;
- no CODEX route and no merge until Main completes the deep audit and closes all BLOCKER/MAJOR findings.

Audit domains:
- cumulative diff/code quality;
- startup ordering and initialization dependencies;
- durable restart matrix;
- Authority boundary/fail-closed semantics;
- Engineering Working/Published/Active lifecycle;
- fresh install/no hidden Demo;
- detach/neutral/recovery states;
- persistence idempotence/failure recovery;
- adequacy of real startup/integration/E2E tests;
- compatibility with current integration and frozen Foundation contracts.

Dedicated FND-07 control rev 0017 contains the binding audit gate.

Current DEV correction remains bounded to:
`FND07-DEV-AUTHORITY-BEFORE-ENGINEERING-RESTART-05`.

CODEX remains paused.


## 32. FND-07 cumulative deep audit — blockers returned to DEV

Main audited PR #348 cumulatively at:
- head `f2f1fed37552133e30567faa0c944d605b048125`;
- tree `74d2186d69e77c8252f0ec8740794e3485072e5f`;
- 14 commits / 21 changed files.

Open BLOCKERs:
1. `AttachInProgress` restart recovery can be unreachable before first durable revision;
2. interrupted installation detach can complete Application Neutral while leaving Authority attached.

Open MAJORs:
3. license Keep/Remove/Replace intent/result is not restart-durable/transactionally honest;
4. transition/restart/System-Recovery test coverage is insufficient.

Cross-lane reconciliation is also required after DEV correction because FND-07 overlaps frozen FND-05 HA surfaces in `ProductLicensedRuntimeCoordinator.cs`, `Program.cs` and `ScadaRuntimeFacade.cs`.

State:
`FND-07 -> DEV_CORRECTION / DEEP_AUDIT_BLOCKERS_OPEN`

Binding order:
`FND07-DEV-DEEP-AUDIT-BLOCKERS-06`.

CODEX remains paused.


## 33. Independent cross-lane delivery audit while FND-07 correction runs

Live audit checkpoint:
- integration `9895a01a662851505198b965fae2335e55fba6fa`;
- FND-07 continues independently under deep-audit correction order and has advanced beyond the audited `f2f1fed3...` checkpoint; Main will re-audit its eventual handoff separately.

### Script Engineering #344
Independent cumulative audit:
`NO_NEW_BLOCKER_OR_MAJOR`.
- backend/frontend scope-event rules align;
- stable TagId / client-memory identity semantics preserved;
- stale event fields cleared deterministically;
- no second resolver/runtime authority.
Control rev 0005.

### Editor #349
Independent cumulative audit:
`NO_NEW_BLOCKER_OR_MAJOR`.
- single-canvas canonical-renderer architecture remains coherent;
- interaction layer remains non-authoritative;
- Popup/Screen paths share the canonical canvas;
- the prior stale hardcoded surface test was already closed test-only by #351.
Control rev 0006.

### Authority UX #346
Independent candidate audit:
`NO_NEW_BLOCKER_OR_MAJOR`.
- user role choices come from canonical `IAuthorityPolicyStore`;
- persisted stable role IDs/keys remain protected;
- orphan assignment, self-lockout and expected-version concurrency remain backend-authoritative.
State remains `MAIN_ACCEPTED_FOR_CODEX / QUEUED / DEV_WAIT`.
Control rev 0006.

### Licensing UX #345
Independent audit found:
`LICENSING_UX_SESSION_REPLACEMENT_STALE_STATE / MAJOR_TRUTH_GAP`.

When changing Runtime Session class, the UI terminates the old lease before admitting the replacement but retains old local `lease.current` / `outcome` until the replacement succeeds. A replacement admission failure can therefore display an already-terminated old session as active.

State changed to:
`DEV_CORRECTION / AUTHORIZED`

Binding order:
`DEV-LICENSING-UX-SESSION-REPLACEMENT-TRUTH-03`

Control rev 0005.

### FND-05
Independent post-freeze source audit:
`NO_NEW_BLOCKER_OR_MAJOR_WITHIN_FROZEN_FND05_SCOPE`.
State remains `VERIFIED / FROZEN`.
The control header was also normalized to the already-established freeze disposition.
Control rev 0012.

CODEX remains paused while FND-07 deep-audit blockers are being corrected. It must not self-select Authority/Licensing.


## 34. Durable inter-agent handoff protocol

Product Owner must not relay lane handoffs between executor chats and Main.

Effective immediately for all Wave 15 DEV/CODEX lanes:

`CHAT_ONLY_HANDOFF: NOT_DELIVERED`

A handoff is considered delivered only when persisted in GitHub:
1. preferred: comment on the lane PR;
2. if no PR exists: comment on the coordinator Issue/control ledger;
3. if comment tooling is unavailable: write the complete handoff into the lane's dedicated control and report the tooling blocker.

Minimum durable handoff fields:
- order ID/revision;
- exact branch/head/tree;
- changed-file map;
- tests/workflow IDs and conclusions;
- explicit blockers/environment limitations;
- requested Main disposition.

After a durable handoff, the executor defaults to `DEV_WAIT` / no mutation until Main responds unless the current order explicitly says otherwise.

The Product Owner should never need to copy executor output into the Main chat for coordination continuity.

Current FND-07 handoff at `0189b7f79bf9772c1cf7ece8f4774ba9ea207f6b` was chat-only and has been mirrored by Main to PR #348 as a one-time recovery action.

FND-07 is now:
`MAIN_DEEP_AUDIT / DEV_WAIT`

Licensing remains on its own DEV correction/T1 path.


## 35. FND-07 handoff received durably / deep re-audit disposition

FND-07 exact candidate:
- head `0189b7f79bf9772c1cf7ece8f4774ba9ea207f6b`;
- tree `bd6971f73348ec7b4f4682d18cabe36f6ef32a47`;
- DEV chat handoff mirrored by Main to PR #348 because it was not originally persisted;
- future chat-only handoffs are not considered delivered.

Deep re-audit:
- Attach restart blocker: source-closed;
- interrupted detach/Authority blocker: source-closed;
- license truth/restart major: architecture-closed via independent FND-03 staging before destructive installation journal;
- test-gap major: source coverage added, but execution evidence still pending;
- cross-lane reconciliation remains Main-owned/open.

FND-07 overlaps current integration in exactly:
- `ProductLicensedRuntimeCoordinator.cs`;
- `Program.cs`;
- `ScadaRuntimeFacade.cs`.

State:
`FND-07 -> MAIN_OWNED_RECONCILIATION / DEV_WAIT`

No further FND-07 DEV mutation.

Main must reconcile frozen FND-05 HA fencing with FND-07 Neutral/Detach fencing, then obtain executable exact-head evidence before any CODEX merge/freeze decision.


## 36. Licensing audit correction accepted / queued again

DEV-LICENSING-UX exact corrected candidate:
- PR #345;
- head `d97598fb16ba0eb6d43e78b25c848149a1ec547a`;
- natural exact-head T1 `36092087487`: SUCCESS across classifier/Common/Web/.NET/Chromium/final gate.

The prior session-replacement stale-state MAJOR is closed.

State:
`DEV-LICENSING-UX -> MAIN_ACCEPTED_FOR_CODEX / QUEUED / DEV_WAIT`

Dedicated control rev 0006.

Shared CODEX remains reserved/paused for the Main-owned FND-07 reconciliation/validation path; Licensing must not preempt it.


## 37. FND-07 Main reconciliation complete / final CODEX active

Main reconciled FND-07 against current integration using an explicit two-parent merge commit:

- exact head `2bacddbc558bba0dd1a316d98b9895fbb847683c`;
- tree `06ddb27f8f50c495653c1b8e57a2142769ca29e0`;
- parent 1: integration `9895a01a662851505198b965fae2335e55fba6fa`;
- parent 2: audited FND-07 DEV candidate `0189b7f79bf9772c1cf7ece8f4774ba9ea207f6b`;
- PR #348 is mergeable.

Hotspot resolution preserves both:
- frozen FND-05 HA industrial authority;
- FND-07 installation Neutral/Detach authority.

Main also closed one reconciliation finding:
`FND07-AUD-NEUTRAL-RUNTIME-METADATA-07`.
Neutral now hides stale Operational Event / Client Memory / Driver / Server Memory metadata, with a focused regression.

Natural exact-head T1:
`36093062819` — active on the reconciled SHA.

Sequential CODEX route:
`ROUTE-SEQUENTIAL-CODEX-TO-FND07-RECONCILED-33`

Expected order:
`FND07-CODEX-RECONCILED-ADVERSARIAL-09`

FND-07 DEV remains WAIT. Authority and Licensing remain queued behind this Foundation closeout.


## 38. Coordinator transfer — FND-07 test-audit / CODEX unavailable

Live transfer state:
- integration: `9895a01a662851505198b965fae2335e55fba6fa`;
- FND-07 PR #348 exact head: `3ecc4a78080685b0556402d50190e09236d6d8fa`;
- T1 `36094394912`: classifier/Common/Web/.NET green, Chromium red;
- remaining failure is HTTP 422 from real persisted Published -> Active activation after publish succeeds;
- activation mapping's prior nested-Task 500 was found and corrected during Main test audit.

Main also removed legacy Demo dependency from Playwright, made INSTALLATION own fresh-install browser evidence, corrected stale Authority-checkout test expectations, and made Runtime E2E require the actual persisted Active revision rather than a fallback Demo runtime.

State:
`FND-07 -> MAIN_DIAGNOSTIC / DEV_WAIT / NO_MERGE`

Shared CODEX:
`PAUSED / CODEX_UNAVAILABLE`.

Authority and Licensing must not self-select the shared CODEX resource. The replacement Main Coordinator must revalidate live GitHub, diagnose the exact-head 422, and publish the next explicit route.
## 39. FND-07 activation 422 diagnosed / bounded DEV fixture correction

Main revalidated PR #348 exact head `3ecc4a78080685b0556402d50190e09236d6d8fa` and exact-head T1 `36094394912`.

Diagnosis:
`FND07_TEST_FIXTURE_SOURCE_INCOMPATIBILITY / BUILTIN_SIMULATION_NOT_ACTIVE_RUNTIME_SOURCE`.

Evidence:
- fresh install / first Administrator / first project remain valid;
- explicit fixture save succeeds;
- revision 2 publish succeeds;
- real awaited Published -> Active handler returns 422;
- reproduced activation outcome reports sole runtime issue `RUNTIME_NO_ACTIVE_SOURCES`;
- the fixture uses only `builtin.simulation`;
- the runtime compiler intentionally skips `builtin.simulation`;
- no communication driver, Server Memory or Client Memory source therefore exists to activate.

This is not an Authority, Licensing/admission or endpoint-transport defect. The prior nested-Task 500 remains closed.

Current FND-07 state:
`DEV_CORRECTION / AUTHORIZED / TEST_HARNESS_ONLY`

Binding order:
`FND07-DEV-ACTIVE-RUNTIME-FIXTURE-SOURCE-11`

Dedicated control rev 0023:
`e7964661e06ce70f3cca20659a3bd7d05a79065b`.

Authorized correction is limited to the explicit downstream E2E fixture and stale runtime assertions: move the test-owned active source to canonical Server Memory (`builtin.memory.server`) and preserve fresh-install no-Demo truth plus Working != Published != Active sequencing.

No product source mutation, merge or freeze is authorized.

Shared CODEX remains paused/unavailable. Authority and Licensing remain unable to self-select the shared CODEX resource.
## 40. FND-07 new T1 failure classified / CODEX correction active

FND-07 DEV delivered exact head `95fd68468c7ce26b94e8c60a513625e917f4c4a2`, tree `beca8b125258e812680573558a1c382acffe87ff`.

T1 `36136242327`:
- Classify SUCCESS;
- Common SUCCESS;
- Web SUCCESS;
- focused .NET SUCCESS;
- focused Chromium FAILURE.

The Server Memory fixture correction closed the prior no-active-source blocker. The new failure occurs after successful Runtime activation commit when `EngineeringLockAccess.Replace` constructs a lock-only Engineering package without the current Authority reference.

Classification:
`GENERIC_PRODUCT_DEFECT / ENGINEERING_LOCK_PARTIAL_PACKAGE_DROPS_AUTHORITY_REFERENCE`.

The affected production file is byte-identical to FC0-A and current integration, so the defect is pre-existing rather than FND-07 DEV causal.

Shared CODEX is now ACTIVE on:
`FND07-CODEX-ENGINEERING-LOCK-AUTHORITY-CORRECTION-12`.

CODEX may perform the bounded production correction + tests/adversarial validation. Authority validation must remain exact/fail-closed; no bypass is permitted.

FND-07 DEV remains WAIT. Authority UX and Licensing UX remain queued and must not preempt the shared CODEX route.
## 41. FND-07 merged / post-merge CI pending

PR #348 has been merged after Main final adversarial acceptance.

Integrated SHA:
`1b186c48ba5d2e3012be2c58f0efc36170101fe9`.

Pre-merge exact-head T1 `36189002703` was fully green.

Post-merge exact integrated CI:
`36191702355` / run #1576 — queued at this revision.

State:
`FND-07 -> INTEGRATED_PENDING_POSTMERGE_CI`.

No freeze yet. FND-07 DEV and CODEX remain WAIT. Shared CODEX stays held until Main resolves the post-merge CI and publishes the next sequential route.
## 42. FND-07 post-merge CI blocker / CODEX correction active

Post-merge CI `36191702355` on integrated SHA `1b186c48ba5d2e3012be2c58f0efc36170101fe9` failed in Backend smoke startup.

Exact failure:
`AuthorityDetachService` cannot be constructed because `IAuthorityLifecycleStore` is absent when local authentication is disabled.

Classification:
`FND07_POSTMERGE_COMPOSITION_BLOCKER / LOCAL_AUTHORITY_SERVICES_RESOLVED_WHEN_LOCAL_AUTH_DISABLED`.

This is a real composition defect, not a transient. No rerun is authorized before correction.

Dedicated correction branch:
`work/w15-fnd07-postmerge-local-authority-gate`.

Shared CODEX route is active on:
`FND07-CODEX-POSTMERGE-LOCAL-AUTHORITY-GATE-15`.

FND-07 remains integrated but NOT VERIFIED/FROZEN until the correction is integrated and post-merge evidence is green.
## 43. FND-07 final post-merge CI: stale first-project smoke / CODEX active

Final post-merge CI `36194762603` on `e49155e4a17acb6cd35683500f4ef211eb8f6e56` failed only after the API had started and backend tests/runtime/historian smoke had succeeded.

Failure:
legacy CI uses `POST /api/engineering/persistence/ci-demo/save` to create the first Application while the installation binding is correctly `Neutral`.

Classification:
`CI_HARNESS_STALE_FIRST_PROJECT_BOOTSTRAP / GENERIC_SAVE_ON_NEUTRAL_BINDING`.

FND-07 product behavior is correct. The canonical first-Application transaction is `POST /api/engineering/persistence/projects/first`.

Shared CODEX is active on:
`FND07-CODEX-CI-FIRST-PROJECT-SMOKE-18`

Branch:
`work/w15-fnd07-postmerge-ci-first-project-smoke`.

Scope is CI/harness only. No product rule may be weakened.

FND-07 remains NOT VERIFIED/FROZEN until corrected full post-merge evidence is green.
## 44. FND-07 CODEX local full-CI parity battery required

The active FND-07 CI-harness correction now includes a mandatory persistent local parity pass before additional hosted-CI debugging.

CODEX must reproduce the complete current `EliteSCADA CI` surface in one reusable environment:
- Release backend build;
- full solution tests;
- complete Runtime smoke;
- frontend build;
- complete Chromium E2E.

Expensive dependencies/tooling should be installed/restored once and reused while inputs remain unchanged. Databases/test state must be reset between passes as needed.

The goal is to discover sequential failures locally rather than requiring one GitHub Actions run per blocker.

Binding order remains:
`FND07-CODEX-CI-FIRST-PROJECT-SMOKE-18`

Amended FND-07 control:
rev 0032 / `25877bfb5c1154d63c21f001f2190c9b28dc6bc6`.

Production mutation remains prohibited.

