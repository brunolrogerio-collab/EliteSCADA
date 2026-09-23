# Wave 15 — FC0-A Release Preparation

> PREPARED ONLY. This document does not release any DEV lane. GitHub live and Main's later exact FC0-A checkpoint are authoritative.

`STATE: PREPARED / NOT RELEASED / BLOCKED_ONLY_ON_FND06`

`CONTROL_BRANCH: coord/w15-fnd06-control`

## 1. Frozen dependencies already established

As of preparation:
- FND-01 — VERIFIED/FROZEN;
- FND-02 incl. AUTH-04 — VERIFIED/FROZEN;
- FND-08 — VERIFIED/FROZEN;
- FND-03 global — VERIFIED/FROZEN;
- INFRA-CI-01A — VERIFIED/FROZEN;
- FND-04 — VERIFIED/FROZEN at `6c810647c9773a19b212d9c33694780141786ac7` / tree `1221ff55963052be4e924dd644efbaa65763f546`; exact post-merge CI `35913456486` SUCCESS;
- FND-06 — ACTIVE on exact product base `6c810647c9773a19b212d9c33694780141786ac7`, order `FND06-CODEX-VISUAL-STABILITY-V1`, not yet integrated/frozen.

FC0-A remains blocked **only by FND-06 VERIFIED/FROZEN**.

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

Release-time rule for all four: Main must first replace the blocked state with `ACTIVE`, write the same exact FC0-A SHA/tree into each order, create each work branch from that SHA, and record the release in #305. No lane may infer activation from this preparation file.

---

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
