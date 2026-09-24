# Wave 15 — DEV-LICENSING-UX Control

> GitHub live is the sole authority.

`LANE: DEV-LICENSING-UX`
`MAIN_ORDER_REV: 0001`
`ORDER_ID: DEV-LICENSING-UX-FC0A-01`
`ORDER_STATE: PREPARED / WAIT_FC0A_RELEASE`
`PLANNED_BRANCH: work/w15-dev-licensing-ux`
`TARGET: wave15/corrections-integration`
`VALIDATION_PROFILE: LICENSING_UX, SESSION_LICENSING`

## Activation

Do not mutate product until Main records `FC0A_RELEASE_APPROVED`, writes exact base SHA/tree here, creates the branch and changes ORDER_STATE to ACTIVE.

## Mission after activation

Complete residual licensing/session UX on top of frozen FND-03:
- License Generator v2 user-facing fields/workflow;
- licensing status/entitlement UX;
- explicit ViewOnly request;
- truthful requested vs server-granted Runtime class;
- Interactive-quota -> ViewOnly fallback/reason UX;
- backward/new-schema UX;
- deterministic diagnostics for lifecycle operations.

Before coding, inspect exact FC0-A base. The consolidated FC0-A candidate already brought forward part of requested/granted/ViewOnly/fallback UX and fallback-lease lifecycle. Continue from live residuals instead of rebuilding them.

## Primary ownership

- `web/scada-web/src/licensing/**`
- `src/Scada.LicenseGenerator/**`
- explicitly delegated Runtime Session Class UX

## Frozen contracts to consume

- FND-03 machine-license trust;
- logical Runtime Session Lease;
- requested/granted class;
- shared Web+EliteGO quota;
- server Authority/session admission.

## Forbidden

No private-key boundary change, no client entitlement pool, no independent seat authority, no session admission redesign, no HA fencing invention, no treating Session Class as Authority.

## DEV delivery

Deliver isolated code PR, exact head/tree, changed files, implemented UX matrix, cheap focused evidence, residual validation marked `PENDING_FOR_CODEX`, known gaps and non-actions.

After Main acceptance, CODEX owns old/new license/session negatives, ViewOnly fail-closed behavior, lease/quota regressions and exact-head T1.

Required prefix:
`DEV-LICENSING-UX -> MAIN COORDINATOR — CANDIDATE HANDOFF`

Cross-lane control:
`docs/WAVE15-PARALLEL-DEV-CONTROL.md`.
