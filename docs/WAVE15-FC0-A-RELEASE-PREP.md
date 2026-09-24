# Wave 15 — FC0-A Release Preparation

> PREPARED ONLY. This document does not release any DEV lane. GitHub live and Main's later exact FC0-A checkpoint are authoritative.

`STATE: PREPARED / NOT RELEASED / CONSOLIDATED_FC0A_CORRECTION_ACTIVE`

`CONTROL_BRANCH: coord/w15-fnd06-control`

## 0A. Current correction strategy — consolidated pre-release package

Main superseded the previous serialized P1-01 -> review -> P1-06 sequence.

Active order:
`FC0A-CONSOLIDATED-CORRECTION-PACKAGE-V2`

Control:
`docs/WAVE15-FC0A-CONSOLIDATED-CORRECTION-PACKAGE.md`

Work branch:
`work/w15-fc0a-consolidated-corrections`

Exact base:
`560ac9d80cc7e854f2513559dc6afb28cfb4aee3`

The package brings forward all confirmed/shared FC0-A findings that can be safely corrected without reopening frozen contracts, while leaving only speculative/evidence-bounded future work outside the package. The same sequential CODEX executor is authorized to complete the full package and present one final consolidated candidate before Main re-review.

No DEV/FND-05/FND-07 release occurs until that candidate is reviewed, integrated if accepted, broad exact-head evidence is green, and Main reruns the FC0-A audit rows.

## 1. Frozen dependencies already established

As of preparation:
- FND-01 — VERIFIED/FROZEN;
- FND-02 incl. AUTH-04 — VERIFIED/FROZEN;
- FND-08 — VERIFIED/FROZEN;
- FND-03 global — VERIFIED/FROZEN;
- INFRA-CI-01A — VERIFIED/FROZEN;
- FND-04 — VERIFIED/FROZEN at `6c810647c9773a19b212d9c33694780141786ac7` / tree `1221ff55963052be4e924dd644efbaa65763f546`; exact post-merge CI `35913456486` SUCCESS;
- FND-06 — VERIFIED/FROZEN at `560ac9d80cc7e854f2513559dc6afb28cfb4aee3` / tree `674019fbbc21001a2d68deb853c2c0b293e0a5cb`; exact broad `35953557122` SUCCESS.

FND-06 prerequisite is closed. FC0-A remains blocked because Main's post-FND06 audit returned **CHANGES_REQUIRED** on W15-P1-01 and W15-P1-06.

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
- consume the frozen FND-06 compatibility seam for known persisted legacy geometry/visibility/z-order/authoring paths;
- prove known legacy `tank | value | dynamo | status` can participate in marquee/topmost selection, move/resize, z-order and multi-object authoring where semantically allowed without a second compatibility table;
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

Owns remaining Script Engineering maturity:
- preserve/regress the already-present cursor-safe Monaco insertion instead of reimplementing it;
- preserve/regress the already-present timer/tagChanged canonical authoring flow instead of treating it as greenfield;
- close any remaining stale event-field switching edge cases;
- API signatures/parameters/return semantics/examples beyond the current title+summary help;
- representative compare-two-TAG / visual-state UI-only recipe;
- make Script Assistant consume the frozen FND-06 `getVisualSchemaForEngineering` compatibility seam for known legacy visual-property discovery while arbitrary unknown remains fail-closed;
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
- explicit Web Runtime View Only request;
- truthful `requestedClass` vs server `grantedClass`;
- Interactive-quota -> ViewOnly fallback/reason messaging using server reason codes;
- minor `viewer` vs canonical `viewOnly` API/UX wording consistency;
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

## 3C. Updated six-lane execution model

Prepared cross-lane control:

`coord/w15-parallel-dev-control:docs/WAVE15-PARALLEL-DEV-CONTROL.md`

Prepared implementation chats after FC0-A:
- DEV-EDITOR;
- DEV-SCRIPT-ENGINEERING;
- DEV-AUTHORITY-UX;
- DEV-LICENSING-UX;
- FND-05 DEV;
- FND-07 DEV.

All six are normal ChatGPT DEV implementation chats.

Pipeline:

`DEV code -> Main review -> same DEV correction if material -> Main accepts for CODEX -> sequential CODEX local/adversarial validation + exact-head T1 -> Main integration`

Feature lanes remain separate PRs and consolidate at integrated T2 after controlled merges.

FND-05/FND-07 remain separate Foundation PRs and each must pass Main contract review, CODEX validation, exact-head T1, post-merge validation and explicit VERIFIED/FROZEN. They are not folded into one raw feature package.

CODEX is a scarce sequential validation resource, not the default implementation owner for these six lanes.

FND-05 prepared implementation order is now `FND05-DEV-HA-AUTHORITY-V1`.

FND-07 prepared implementation order is now `FND07-DEV-DETACH-NEUTRAL-V1`.

No lane is activated by this preparation.

## 4. Release guard

At FC0-A there is **no fixed numeric cap** on coding DEV chats. The previous four-DEV limit was a historical operational throttle for a different context. Main controls concurrency dynamically through ownership isolation, shared-hotspot collision risk, review capacity and CODEX validation queue.

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


## 8. PR #337 pre-freeze contract-risk snapshot

Source candidate:
- PR #337
- head `923543705378016090e7067b35954795a9591a57`
- tree `5657cee7169a4e77370d416add4efcf07184d7c0`
- T1 `35931139983` SUCCESS
- not yet FND-06 frozen because mounted A7 selection closeout remains active.

Current risk:
- DEV-EDITOR: `LOW / GUARDED` — no known contract break; release still blocked by mounted A7 evidence and final FND-06 freeze. DEV must consume the frozen centralized legacy compatibility boundary.
- DEV-SCRIPT-ENGINEERING: `NONE IDENTIFIED / GUARDED` — PR #337 does not touch FND-04; FND-05 may only add execution fencing around existing Script authority.
- DEV-AUTHORITY-UX: `NONE IDENTIFIED / GUARDED` — PR #337 does not touch FND-02; FND-07 must compose existing Authority/session mechanisms.
- DEV-LICENSING-UX: `LOW BUT MATERIAL RESIDUAL` — FND-05 HA redundancy entitlement/readiness must be additive/backward-compatible to FND-03. Breaking license-schema/seat/quota reinterpretation blocks FC0-A.

This snapshot is superseded by the exact post-FND06 independent audit. It does not release any lane.


## 9. Second-pass deep-audit release notes

Authoritative supplement:
`docs/WAVE15-FC0A-POST-FND06-AUDIT-SECOND-PASS.md`
commit `43c266949236af377ba859c9b8f09fa2d3a3a31a`.

No third pre-FC0A blocker was identified.

Additional bounded product gaps that remain tracked but do not serialize FC0-A:
- W15-P2-01 Trends: explicit realtime socket/reconnect/last-request/last-success/freshness observability;
- W15-P2-02 Popup/shared live values: explicit freshness age/reason and request/success telemetry.

Contract confidence:
- FND-05 remains additive/compatible;
- FND-07 remains compositional/compatible;
- production host already starts Engineering with `seedDemo:false`, so neutral no-Demo workspace state does not require breaking the lifecycle descriptor contract;
- Authority detach/attach/switch primitives already exist and remain separate from the future Application/runtime detach orchestrator.

Release is still blocked only by the two confirmed first-audit blockers plus their exact post-merge revalidation.


## Post-merge gate update — 2026-09-24 / CI #1567

FC0-A consolidated PR #340 remains product-accepted and merged.

INFRA-CI-01C PR #341 is merged at exact SHA
`da0e64122f1e4d0293e027f45ef95021cd03c1a1`,
tree `a724e565f11121a43b97bf0c59683b414c72f48c`.

Exact broad `EliteSCADA CI 36047274028 / #1567`:
- Web build — SUCCESS;
- Backend build — SUCCESS;
- Backend Test — SUCCESS;
- Runtime smoke — SUCCESS;
- Chromium — FAILURE before browser test execution.

The PostgreSQL `23505` recurrence is closed by the exact broad backend evidence.

The remaining blocker is a deterministic validation-load defect:
`web/scada-web/tests-e2e/contextual-help-routing.spec.ts` imports
`contextualHelpTopic` through `AppNavigation.tsx`, whose transitive CSS imports are parsed by the Node-side Playwright spec loader and fail at `src/auth/auth.css` with `Unexpected token (1:0)`.

Classification:
`FC0A_POSTMERGE_E2E_TEST_LOAD_DEFECT / PRODUCT_BEHAVIOR_NOT_SHOWN_DEFECTIVE`.

Active closeout:
- order `FC0A-POSTMERGE-HELP-E2E-LOAD-V1`;
- control `docs/WAVE15-FC0A-POSTMERGE-HELP-E2E-LOAD-CONTROL.md`;
- work branch `work/w15-fc0a-postmerge-help-e2e-load`;
- exact base `da0e6412...`.

No `FC0A_RELEASE_APPROVED` yet. The six prepared DEV/FND lanes remain WAIT until the corrected candidate is merged and a new exact broad post-merge CI is globally green.


## Post-merge gate update — CI #1568 Canvas source-contract stale

Current exact release candidate:
- SHA `1ab3550e1afb258e38caaa6de3f6547f481bbef7`;
- tree `701f4591a294885b414284691b1051cf274c9707`.

Exact broad `EliteSCADA CI 36049229264 / #1568`:
- Web — SUCCESS;
- Backend build/test + Runtime smoke — SUCCESS after one diagnosed same-SHA retry of the repository-documented known IEC-104 T2 timing transient;
- Chromium — 654 passed / 1 failed.

The only Chromium failure is a stale source-contract assertion that still requires direct `getBuiltinVisualObjectSchema` in Canvas while the accepted FC0-A/FND-06 contract intentionally uses `getVisualSchemaForEngineering` for built-in + bounded known-legacy Engineering compatibility.

Active closeout:
- `FC0A-POSTMERGE-CANVAS-SOURCE-CONTRACT-V1`;
- control `docs/WAVE15-FC0A-POSTMERGE-CANVAS-SOURCE-CONTRACT-CONTROL.md`;
- work branch `work/w15-fc0a-postmerge-canvas-source-contract`;
- exact base `1ab3550e...`.

No `FC0A_RELEASE_APPROVED` yet. Six prepared DEV/FND lanes remain WAIT until this stale test contract is corrected, merged and a new exact broad post-merge CI is globally green.
