# Wave 15 — Parallel DEV Coordination Control

> GitHub live is the sole authority. This file prepares the post-FC0A parallel implementation model. It does **not** activate any lane by itself.

`CONTROL_BRANCH: coord/w15-parallel-dev-control`

`MAIN_ORDER_REV: 0001`

`STATE: PREPARED / ALL_LANES_BLOCKED_ON_FC0A_RELEASE_APPROVED`

`ACTIVATION_RULE: Main must revalidate GitHub live, record the exact FC0-A product SHA/tree, create each work branch from that exact checkpoint, and switch the lane state to ACTIVE before product mutation.`

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

`STATE: PREPARED / BLOCKED_ON_FC0A_RELEASE_APPROVED`

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

`STATE: PREPARED / BLOCKED_ON_FC0A_RELEASE_APPROVED`

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

`STATE: PREPARED / BLOCKED_ON_FC0A_RELEASE_APPROVED`

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

`STATE: PREPARED / BLOCKED_ON_FC0A_RELEASE_APPROVED`

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

`STATE: PREPARED / BLOCKED_ON_FC0A_RELEASE_APPROVED`

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

`STATE: PREPARED / BLOCKED_ON_FC0A_RELEASE_APPROVED`

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
