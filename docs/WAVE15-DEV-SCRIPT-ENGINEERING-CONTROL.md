# Wave 15 — DEV-SCRIPT-ENGINEERING Control

> GitHub live is the sole authority.

`LANE: DEV-SCRIPT-ENGINEERING`
`MAIN_ORDER_REV: 0002`
`ORDER_ID: DEV-SCRIPT-ENGINEERING-FC0A-01`
`ORDER_STATE: ACTIVE_CODING / AUTHORIZED`
`PLANNED_BRANCH: work/w15-dev-script-engineering`
`WORK_BRANCH: work/w15-dev-script-engineering`
`FC0A_RELEASE_APPROVED: YES`
`EXACT_BASE_SHA: e3ed5138369c576549cb58a7aff9783792f322d3`
`EXACT_BASE_TREE: 4e7627774fbfc111344e3d80fcb9d921eed8377e`
`ACTIVATION_GATE: EliteSCADA CI #1569 / 36060017969 / SUCCESS / Chromium 655 passed`
`TARGET: wave15/corrections-integration`
`VALIDATION_PROFILE: SCRIPT_ENGINEERING, SCRIPT_RUNTIME`

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
