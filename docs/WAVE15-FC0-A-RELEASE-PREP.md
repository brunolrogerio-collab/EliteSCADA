# Wave 15 — FC0-A Release Preparation

> PREPARED ONLY. This document does not release any DEV lane. GitHub live and Main's later exact FC0-A checkpoint are authoritative.

`STATE: PREPARED / NOT RELEASED / BLOCKED_ON_FND06_AND_POST_FND06_FOUNDATION_AUDIT`

`CONTROL_BRANCH: coord/w15-fnd06-control`

## 1. Frozen dependencies already established

As of preparation:
- FND-01 — VERIFIED/FROZEN;
- FND-02 incl. AUTH-04 — VERIFIED/FROZEN;
- FND-08 — VERIFIED/FROZEN;
- FND-03 global — VERIFIED/FROZEN;
- INFRA-CI-01A — VERIFIED/FROZEN;
- FND-04 — VERIFIED/FROZEN at `6c810647c9773a19b212d9c33694780141786ac7` / tree `1221ff55963052be4e924dd644efbaa65763f546`; exact post-merge CI `35913456486` SUCCESS;
- FND-06 — ACTIVE on exact product base `6c810647c9773a19b212d9c33694780141786ac7`, order `FND06-CODEX-VISUAL-STABILITY-V2`, not yet integrated/frozen.

FC0-A remains blocked by **FND-06 VERIFIED/FROZEN + mandatory post-FND06 Foundation Closure Audit ACCEPTABLE**.

## 1A. Mandatory post-FND06 audit gate

After FND-06 becomes VERIFIED/FROZEN, Main must **not** immediately release FC0-A.

Required audit:
`coord/w15-fnd06-control:docs/WAVE15-FC0A-POST-FND06-AUDIT-CONTROL.md`

Required final classification:
`FC0-A FOUNDATION AUDIT -> MAIN COORDINATOR — ACCEPTABLE / FC0A_RELEASE_APPROVED`

The audit must:
- reconcile Wave 15 product premises against Wave 14 final diagnostic comments/findings;
- map every Wave 15 correction-backlog P0/P1/P2/uncertain item to exact evidence/residual ownership;
- verify no frozen Foundation contracts contradict each other;
- prove prepared FND-05 can remain additive/non-breaking to FND-03/FND-04/FND-06 contracts consumed by FC0-A DEVs;
- prove prepared FND-07 can remain compositional/non-breaking to FND-01/FND-02/FND-03 contracts consumed by FC0-A DEVs;
- block FC0-A if either future Foundation would require redefining a frozen downstream contract.

Until that audit PASS, the four DEV lanes **and FND-05/FND-07 product implementation remain blocked**.

## 2. Exact FC0-A checkpoint fields Main must fill

Do not release lanes until Main records:
- `FC0_A_INTEGRATION_SHA`;
- `FC0_A_TREE`;
- exact post-merge CI/specialized gate evidence;
- final FND-04 frozen SHA/contract;
- final FND-06 frozen SHA/contract;
- known residuals/deferred items;
- branch divergence classification.

All DEV branches must start from the exact recorded FC0-A SHA, not from a remembered or earlier Foundation checkpoint.

## 3. First parallel batch — maximum four active implementation lanes

### DEV-EDITOR

Parent: #303  
Hard dependency: FND-06 VERIFIED/FROZEN.

Owns downstream editor maturity only:
- single primary WYSIWYG Screen/Popup canvas;
- direct manipulation and Outliner synchronization;
- Property Inspector density/usability;
- move/resize/group/lock/alignment/distribution/z-order;
- clipboard/history;
- bindings/property UX;
- grid/snap/zoom/pan/workspace density;
- removal of permanently stacked second preview as normal workflow.

Must consume, not redefine:
- canonical renderer/public model;
- legacy compatibility contract;
- Working-design vs Active Runtime boundary;
- Runtime navigation authority.

Prepared branch naming:
`work/w15-dev-editor-single-canvas`

### DEV-SCRIPT-ENGINEERING

Parent: #297 / Script Engineering downstream work  
Hard dependency: FND-04 VERIFIED/FROZEN.

Owns:
- event-aware authoring;
- timer/tagChanged required configuration;
- stale hidden-field clearing/migration;
- cursor-aware syntactically safe snippet insertion;
- API signatures/parameters/property addressing/examples;
- compare-two-TAG / visual-state recipe;
- mounted UI regression.

Must consume, not redefine:
- FND-04 readable TAG binding/resolution contract;
- Server Script runtime/sandbox/HA ownership boundaries.

Prepared branch naming:
`work/w15-dev-script-engineering`

### DEV-AUTHORITY-UX

Parent: #302  
Hard dependency: FND-02/AUTH-04 VERIFIED/FROZEN.

Owns:
- Users + role/profile assignment;
- role CRUD under protected invariants;
- capability grouping;
- hierarchy/scope selector;
- effective-permission preview;
- truthful multi-role/inheritance UX.

Must consume, not redefine backend Authority/capability/scope semantics.

Prepared branch naming:
`work/w15-dev-authority-ux`

### DEV-LICENSING-UX

Parent: #301  
Hard dependency: FND-03 VERIFIED/FROZEN.

Owns:
- License Generator v2 UI fields;
- licensing status/usage UX;
- Web Runtime View Only request/fallback messaging where frontend-bounded;
- backward/new-schema UX and deterministic tests.

Must preserve:
- machine-license/signing-key trust boundary;
- shared Runtime Lease/seat accounting;
- Authority intersection and server admission.

Prepared branch naming:
`work/w15-dev-licensing-ux`

## 3A. Prepared lane orders — still blocked, do not execute yet

These identifiers are reserved so Main can activate the four lanes immediately after recording the exact FC0-A checkpoint. They are **not authorization to code** until each order is rewritten with the final exact `FC0_A_INTEGRATION_SHA` and state `ACTIVE`.

### `DEV-EDITOR-FC0A-01`

- parent: #303
- status now: `PREPARED / BLOCKED_FND06`
- branch to create only after FC0-A: `work/w15-dev-editor-single-canvas`
- target: `wave15/corrections-integration`
- validation profile: `UI_EDITOR, RUNTIME_RENDERER`
- consumes frozen FND-06 canonical renderer/legacy-compatibility/Working-vs-Active/navigation contracts
- owns downstream single-primary-canvas UX, direct manipulation, Outliner synchronization, property/binding UX, layout density, grid/snap/zoom/pan and removal of the permanent stacked second-preview workflow
- forbidden: new renderer, Working->Active collapse, process writes from ordinary design mode, Foundation legacy-compatibility redesign
- mandatory mounted acceptance is the 12-scenario matrix in #303 plus regression against the frozen FND-06 compatibility contract
- handoff prefix: `DEV-EDITOR -> MAIN COORDINATOR`

### `DEV-SCRIPT-ENGINEERING-FC0A-01`

- parent: #297 / W15-P1-05 downstream Script Engineering
- status now: `PREPARED / BLOCKED_FC0A`
- branch to create only after FC0-A: `work/w15-dev-script-engineering`
- target: `wave15/corrections-integration`
- validation profile: `SCRIPT_ENGINEERING, SCRIPT_RUNTIME`
- consumes frozen FND-04 readable TAG binding/resolution contract
- owns event-aware fields, timer/tagChanged configuration, hidden-field clearing/migration, cursor-safe insertion, API signatures/examples/property addressing, compare-two-TAG/change-visual-state recipe and mounted UI regression
- forbidden: Server Script sandbox/runtime ownership redesign, new TAG resolver, Authority/HA contract mutation
- handoff prefix: `DEV-SCRIPT-ENGINEERING -> MAIN COORDINATOR`

### `DEV-AUTHORITY-UX-FC0A-01`

- parent: #302
- status now: `PREPARED / BLOCKED_FC0A`
- branch to create only after FC0-A: `work/w15-dev-authority-ux`
- target: `wave15/corrections-integration`
- validation profile: `AUTHORITY_UX`
- consumes frozen FND-02/AUTH-04 capability/scope/effective-permission semantics
- owns Users + role/profile assignment, role CRUD under protected invariants, capability grouping, hierarchy/scope selector, effective-permission preview and truthful multi-role UX
- forbidden: hidden role-name privileges, backend Authority evaluator redesign, Runtime Session Class becoming a role, secret/password exposure
- must regress #302's backend-authoritative denial scenarios and direct API tampering behavior
- handoff prefix: `DEV-AUTHORITY-UX -> MAIN COORDINATOR`

### `DEV-LICENSING-UX-FC0A-01`

- parent: #301
- status now: `PREPARED / BLOCKED_FC0A`
- branch to create only after FC0-A: `work/w15-dev-licensing-ux`
- target: `wave15/corrections-integration`
- validation profile: `LICENSING_UX, SESSION_LICENSING`
- consumes frozen FND-03 machine-license v2 / Runtime Session Lease / shared Web+EliteGO quota / admission semantics
- owns License Generator v2 user-facing fields, licensing status/usage UX, Web Runtime View Only request/fallback/granted-class messaging, backward/new-schema UX and deterministic tests
- forbidden: private signing-key boundary changes, independent client entitlement pools, session-admission authority redesign, HA fencing contract invention
- handoff prefix: `DEV-LICENSING-UX -> MAIN COORDINATOR`

Release-time rule for all four: Main may replace the blocked state with `ACTIVE` only after FND-06 VERIFIED/FROZEN **and** audit `FC0A-POST-FND06-W15-FOUNDATION-AUDIT-01` returns `ACCEPTABLE / FC0A_RELEASE_APPROVED`. Then Main writes the same exact audited FC0-A SHA/tree into each order, creates each work branch from that SHA, and records the release in #305. No lane may infer activation from this preparation file.

---

## 3B. Parallel release after audit PASS

When the post-FND06 audit passes, Main may activate in parallel:
- DEV-EDITOR;
- DEV-SCRIPT-ENGINEERING;
- DEV-AUTHORITY-UX;
- DEV-LICENSING-UX;
- FND-05 on its isolated Foundation branch/order;
- FND-07 on its isolated Foundation branch/order.

FND-05/FND-07 activation at that point is permitted only because the audit has confirmed their prepared contracts are non-breaking to the exact FC0-A frozen-consumer contracts. If later implementation exposes a contradictory requirement, the relevant lane must stop with `BLOCKED-CONTRACT`.

## 4. Release guard

At FC0-A Main may activate no more than these four implementation lanes initially.

Each lane's final issue/order must contain:
1. DEV-ID and parent;
2. exact FC0-A base SHA;
3. UNBLOCKED/ACTIVE state;
4. hard/soft dependencies;
5. owned files/components/functional boundary;
6. forbidden Foundation/shared-authority surfaces;
7. frozen contracts consumed;
8. allowed outputs;
9. deterministic acceptance tests;
10. required branch and target `wave15/corrections-integration`;
11. handoff prefix;
12. exact SHA/tests/CI/non-actions.

Any DEV requiring a frozen-contract change must stop:
`DEV -> BLOCKED-CONTRACT -> MAIN/CODEX Foundation delta`.

## 5. Not released by FC0-A

Remain blocked on additional dependencies:
- DEV-ELITEGO — requires FND-03 + FND-05 + FND-06;
- DEV-INSTALLATION-UX — requires FND-01 + FND-02 + FND-03 + FND-07;
- DEV-HA-DOWNSTREAM — requires FND-05 + replication primitives;
- P2 recovery lanes — wait for relevant upstream corrections.

No lane may infer release merely because this preparation document exists.


## 6. Current FND-06 activation metadata

- active FND-06 order: `FND06-CODEX-VISUAL-STABILITY-V2`
- FND-06 validation profile: `UI_EDITOR, RUNTIME_RENDERER`
- FND-06 control commit: `35e1ae631b8471a66eb0c4042295d5b5628d61ec`
- FC0-A remains blocked until that Foundation slice is integrated, post-merge green and explicitly frozen by Main.


## 7. Parallel-lane collision guard

This ownership map is PREPARED and becomes binding only when FC0-A releases the lanes from one exact common SHA.

### DEV-EDITOR primary ownership

Preferred feature surface:
- `web/scada-web/src/engineering/visual-editor/**`;
- focused `visual-editor*` E2E/mounted tests.

FND-06 may modify part of this same surface first. Therefore DEV-EDITOR **must** branch only after FND-06 is integrated/frozen and consume its compatibility/renderer contract.

Frozen/shared surfaces DEV-EDITOR should consume rather than redesign:
- `web/scada-web/src/visual-runtime/**`;
- `CanonicalVisualRenderer.tsx`;
- Runtime navigation/application authority modules.

A required Foundation change -> `DEV-EDITOR -> BLOCKED-CONTRACT -> MAIN`.

### DEV-SCRIPT-ENGINEERING primary ownership

Preferred feature surface:
- `web/scada-web/src/engineering/scripts/**`;
- `web/scada-web/src/engineering/python-editor/**`;
- Script Engineering focused E2E/mounted tests.

Frozen/shared surfaces to consume:
- FND-04 readable TAG binding/resolution;
- `web/scada-web/src/python-runtime/**` runtime authority/bridge contracts;
- server Script runtime/sandbox.

Do not alter FND-04 resolver semantics or sandbox ownership merely to improve authoring UX.

### DEV-AUTHORITY-UX primary ownership

Preferred feature surface:
- `web/scada-web/src/engineering/UserAdministration.tsx`;
- `web/scada-web/src/engineering/userAdministrationApi.ts`;
- `web/scada-web/src/engineering/userAdministration.css`;
- Authority administration focused tests.

Frozen/shared surfaces to consume:
- FND-02/AUTH-04 backend evaluator/capability/scope contracts;
- Identity/session authority.

No frontend role-name shortcut may become authorization authority.

### DEV-LICENSING-UX primary ownership

Preferred feature surface:
- `web/scada-web/src/licensing/**`;
- `src/Scada.LicenseGenerator/**`;
- licensing/generator focused tests;
- explicitly delegated Web Runtime View Only/granted-class UX.

Frozen/shared surfaces to consume:
- FND-03 signed schema, Runtime Session Lease admission and shared quota authority;
- Security/Authority intersection.

No private key, entitlement authority or independent Web/EliteGO seat pool may move client-side.

### Shared hotspots — Main-coordinated only

The following are likely cross-lane integration hotspots and are **not free-for-all ownership**:
- `web/scada-web/src/engineering/EngineeringApp.tsx`;
- common Engineering/product `types.ts`;
- shared App/router/shell files;
- common localization resource/index files;
- common CSS/layout shells;
- CI/workflow files.

Rule:
1. a DEV avoids these files when lane-local composition can achieve the feature;
2. if a shared hotspot is truly required, the handoff names the exact need and smallest diff;
3. Main decides integration order or assigns a bounded shared-wiring follow-up;
4. no lane broad-refactors a shared hotspot while parallel PRs are active.

This guard is intended to keep the first four FC0-A lanes actually parallelizable rather than creating avoidable merge/review coupling.
