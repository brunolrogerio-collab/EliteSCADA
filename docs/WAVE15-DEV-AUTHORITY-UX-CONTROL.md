# Wave 15 — DEV-AUTHORITY-UX Control

> GitHub live is the sole authority.

`LANE: DEV-AUTHORITY-UX`
`MAIN_ORDER_REV: 0002`
`ORDER_ID: DEV-AUTHORITY-UX-FC0A-01`
`ORDER_STATE: ACTIVE_CODING / AUTHORIZED`
`PLANNED_BRANCH: work/w15-dev-authority-ux`
`WORK_BRANCH: work/w15-dev-authority-ux`
`FC0A_RELEASE_APPROVED: YES`
`EXACT_BASE_SHA: e3ed5138369c576549cb58a7aff9783792f322d3`
`EXACT_BASE_TREE: 4e7627774fbfc111344e3d80fcb9d921eed8377e`
`ACTIVATION_GATE: EliteSCADA CI #1569 / 36060017969 / SUCCESS / Chromium 655 passed`
`TARGET: wave15/corrections-integration`
`VALIDATION_PROFILE: AUTHORITY_UX`

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

Do not mutate product until Main records `FC0A_RELEASE_APPROVED`, writes exact base SHA/tree here, creates the branch and changes ORDER_STATE to ACTIVE.

## Mission after activation

Build the user-facing administration UX on top of frozen backend Authority:
- Users + role/profile assignment;
- protected role CRUD;
- capability grouping;
- hierarchy/scope selector;
- truthful effective-permission preview;
- truthful multi-role/inheritance UX;
- clear denial/error feedback without frontend authority shortcuts.

## Primary ownership

- `web/scada-web/src/engineering/UserAdministration.tsx`
- `web/scada-web/src/engineering/userAdministrationApi.ts`
- `web/scada-web/src/engineering/userAdministration.css`
- lane-local administration UI

## Frozen contracts to consume

- FND-02/AUTH-04 capability/scope/effective-permission evaluator;
- Identity/session authority.

## Forbidden

No role-name privilege, no backend evaluator redesign, no Runtime Session Class-as-role, no client authorization authority, no password/secret exposure.

## DEV delivery

Deliver isolated code PR, exact head/tree, changed files, implemented UX matrix, cheap focused evidence, backend-authoritative cases left `PENDING_FOR_CODEX`, known gaps and non-actions.

Main returns material product gaps to this DEV. After Main acceptance, CODEX owns direct-API tampering, denial/adversarial, mounted locale/effective-permission and exact-head T1 validation.

Required prefix:
`DEV-AUTHORITY-UX -> MAIN COORDINATOR — CANDIDATE HANDOFF`

Cross-lane control:
`docs/WAVE15-PARALLEL-DEV-CONTROL.md`.
