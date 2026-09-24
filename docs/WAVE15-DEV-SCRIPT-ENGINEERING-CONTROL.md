# Wave 15 — DEV-SCRIPT-ENGINEERING Control

> GitHub live is the sole authority.

`LANE: DEV-SCRIPT-ENGINEERING`
`MAIN_ORDER_REV: 0001`
`ORDER_ID: DEV-SCRIPT-ENGINEERING-FC0A-01`
`ORDER_STATE: PREPARED / WAIT_FC0A_RELEASE`
`PLANNED_BRANCH: work/w15-dev-script-engineering`
`TARGET: wave15/corrections-integration`
`VALIDATION_PROFILE: SCRIPT_ENGINEERING, SCRIPT_RUNTIME`

## Activation

Do not mutate product until Main records `FC0A_RELEASE_APPROVED`, writes exact base SHA/tree here, creates the work branch and changes ORDER_STATE to ACTIVE.

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
