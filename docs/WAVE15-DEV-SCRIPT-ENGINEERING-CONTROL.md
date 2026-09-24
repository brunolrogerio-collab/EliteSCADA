# Wave 15 — DEV-SCRIPT-ENGINEERING Control

> GitHub live is the sole authority.

`LANE: DEV-SCRIPT-ENGINEERING`
`MAIN_ORDER_REV: 0003`
`ORDER_ID: DEV-SCRIPT-ENGINEERING-FC0A-01`
`ORDER_STATE: MAIN_ACCEPTED_FOR_CODEX / DEV_WAIT`
`PLANNED_BRANCH: work/w15-dev-script-engineering`
`WORK_BRANCH: work/w15-dev-script-engineering`
`FC0A_RELEASE_APPROVED: YES`
`EXACT_BASE_SHA: e3ed5138369c576549cb58a7aff9783792f322d3`
`EXACT_BASE_TREE: 4e7627774fbfc111344e3d80fcb9d921eed8377e`
`ACTIVATION_GATE: EliteSCADA CI #1569 / 36060017969 / SUCCESS / Chromium 655 passed`
`TARGET: wave15/corrections-integration`
`VALIDATION_PROFILE: SCRIPT_ENGINEERING, SCRIPT_RUNTIME`
`CANDIDATE_PR: #344`
`CANDIDATE_HEAD: cf0ae2d1dcd2d63668b5b1c2c3590a5b6bb9bdaa`
`CANDIDATE_TREE: 0820a2a00f36bcc13a05659d85cc43ee7f266d24`

## Activation — ACTIVE

Main activated this lane after final FC0-A audit rev 0014 returned:
`ACCEPTABLE / FC0A_RELEASE_APPROVED`.

The work branch was created directly from the exact base above.

On `SIGA`:
1. re-read this control live;
2. revalidate the exact work-branch head;
3. inspect the FC0-A base and subtract already-integrated work;
4. begin implementation only inside this lane's owned scope;
5. do not merge or widen frozen contracts.

## Activation history

Historical prerequisite — now satisfied: Main recorded `FC0A_RELEASE_APPROVED`, wrote the exact base SHA/tree, created the work branch and changed the order to ACTIVE.

## Mission after activation

Complete residual Script Engineering product maturity while consuming frozen FND-04/FND-06 contracts:
- practical event-aware authoring;
- event-specific fields and stale/hidden-field clearing;
- TAG/Object/property discovery;
- useful autocomplete/assistant flows;
- cursor-safe/composable insertion only where residual gaps remain;
- useful diagnostics/validation;
- canonical visual property targeting;
- representative UI-only compound authoring flows.

Before coding, inspect exact FC0-A base. PR #340 has already brought forward Script Assistant compatibility and structured API Help/recipe work. Do not reimplement good behavior merely because it was in older scope. Existing cursor insertion and timer/tagChanged work must be treated as regression/refinement unless live evidence shows a gap.

## Primary ownership

- `web/scada-web/src/engineering/scripts/**`
- `web/scada-web/src/engineering/python-editor/**`
- lane-local Script Engineering UI

## Frozen contracts to consume

- FND-04 readable reference <-> stable TagId identity binding;
- canonical backend TAG resolver/Authority;
- FND-06 Engineering visual schema compatibility;
- existing Server Script sandbox/runtime ownership.

## Forbidden

No second TAG resolver, no FND-04 reinterpretation, no Server Script sandbox/runtime redesign, no Authority/HA mutation.

## DEV delivery

Deliver code candidate/PR, exact head/tree, changed files, implemented product matrix, cheap focused evidence, known gaps and `PENDING_FOR_CODEX` validation obligations.

After Main code/contract review, CODEX owns robust readable-binding/identity-drift/mounted authoring/syntax/runtime regressions and exact-head T1.

Required prefix:
`DEV-SCRIPT-ENGINEERING -> MAIN COORDINATOR — CANDIDATE HANDOFF`

Cross-lane control:
`docs/WAVE15-PARALLEL-DEV-CONTROL.md`.


## Main candidate review — ACCEPTED FOR CODEX

Main independently reviewed PR #344 on exact head
`cf0ae2d1dcd2d63668b5b1c2c3590a5b6bb9bdaa`
(tree `0820a2a00f36bcc13a05659d85cc43ee7f266d24`).

Disposition:

`DEV-SCRIPT-ENGINEERING -> MAIN_ACCEPTED_FOR_CODEX`

Review findings:
- candidate is one product commit from exact FC0-A release base `e3ed5138...`;
- changed files are limited to five Script Engineering owned files;
- no shared EngineeringApp/types/router/workflow/Authority/HA/frozen renderer surface changed;
- frontend scope/event compatibility mirrors the frozen backend `ScriptScopeEventRules`;
- Timer uses canonical `timerIntervalMs` and the existing 50 ms minimum;
- TAG Changed persists canonical stable TagId plus optional bit selector and clears `TargetReference`;
- Client Memory Changed uses the existing stable definition ID;
- event-kind changes clear stale target/TAG/timer fields instead of silently carrying hidden state;
- incompatible persisted scope/event combinations remain visible and are blocked by local/backend validation rather than silently rewritten;
- no second TAG resolver or Runtime/Server Script authority was introduced.

Live target reconciliation:
- `wave15/corrections-integration` is currently one commit ahead of the release base only in the five canonical coordination/documentation files;
- no competing product/infra delta overlaps this lane;
- PR #344 is currently mergeable;
- no DEV rebase is required merely for the documentation-only target advance.

Initial natural T1 `36063109199` is **INVALID AS PRODUCT EVIDENCE**:
- router failed before Web/.NET/Chromium because PR body omitted the required standalone `VALIDATION_PROFILE` declaration;
- Main corrected PR metadata to:
  `VALIDATION_PROFILE: SCRIPT_ENGINEERING, SCRIPT_RUNTIME`;
- source SHA was not changed;
- do not classify run `36063109199` as a product/test failure.

### CODEX validation order

CODEX now owns exact-candidate validation on PR #344 / work branch
`work/w15-dev-script-engineering`.

Required validation:
1. mounted event-aware authoring for ClientVisual and Server scopes;
2. persisted incompatible scope/event combinations are surfaced and cannot Preview/Apply until corrected;
3. event-kind switching clears stale hidden fields deterministically;
4. Timer canonical interval, minimum boundary and non-Timer stale timer rejection;
5. TAG Changed discovery/persistence by stable TagId, optional bit-selector bounds and no TargetReference duplication;
6. FND-04 readable-reference/stable-identity regressions, including rename/path reuse/identity-drift fail-closed behavior where relevant to Script authoring/runtime;
7. Client Memory Changed stable definition identity and missing/unavailable discovery behavior;
8. representative Script Assistant object/property/canonical-property regression so this entry-point work does not break the existing assistant path;
9. Python composable/syntactic authoring regression;
10. backend-authoritative tampering/invalid payload negatives;
11. Web build and exact-head natural Wave 15 T1 with the declared profiles.

CODEX may add/strengthen focused tests and may make small validation-driven fixes on the same branch if they do not redesign the feature.

If validation finds a material product/design defect:
`CODEX -> MAIN -> DEV-SCRIPT-ENGINEERING CORRECTION`.

DEV must not mutate while this state remains `MAIN_ACCEPTED_FOR_CODEX / DEV_WAIT`.

CODEX return prefix:

`DEV-SCRIPT-ENGINEERING CODEX -> MAIN COORDINATOR — VALIDATION HANDOFF`
