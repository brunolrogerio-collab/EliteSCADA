# Wave 15 — FC0-A Release Preparation

> PREPARED ONLY. This document does not release any DEV lane. GitHub live and Main's later exact FC0-A checkpoint are authoritative.

`STATE: PREPARED / NOT RELEASED`

`CONTROL_BRANCH: coord/w15-fnd06-control`

## 1. Frozen dependencies already established

As of preparation:
- FND-01 — VERIFIED/FROZEN;
- FND-02 incl. AUTH-04 — VERIFIED/FROZEN;
- FND-08 — VERIFIED/FROZEN;
- FND-03 global — VERIFIED/FROZEN;
- INFRA-CI-01A — VERIFIED/FROZEN;
- FND-04 — merged, exact post-merge CI still pending final Chromium at preparation time;
- FND-06 — activation prepared, not yet implemented/frozen.

FC0-A remains blocked only by FND-04 final freeze + FND-06.

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
