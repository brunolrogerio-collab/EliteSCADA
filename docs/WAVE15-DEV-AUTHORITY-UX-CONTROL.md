# Wave 15 — DEV-AUTHORITY-UX Control

> GitHub live is the sole authority.

`LANE: DEV-AUTHORITY-UX`
`MAIN_ORDER_REV: 0001`
`ORDER_ID: DEV-AUTHORITY-UX-FC0A-01`
`ORDER_STATE: PREPARED / WAIT_FC0A_RELEASE`
`PLANNED_BRANCH: work/w15-dev-authority-ux`
`TARGET: wave15/corrections-integration`
`VALIDATION_PROFILE: AUTHORITY_UX`

## Activation

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
