# Wave 15 — DEV-LICENSING-UX Control

> GitHub live is the sole authority.

`LANE: DEV-LICENSING-UX`
`MAIN_ORDER_REV: 0002`
`ORDER_ID: DEV-LICENSING-UX-FC0A-01`
`ORDER_STATE: ACTIVE_CODING / AUTHORIZED`
`PLANNED_BRANCH: work/w15-dev-licensing-ux`
`WORK_BRANCH: work/w15-dev-licensing-ux`
`FC0A_RELEASE_APPROVED: YES`
`EXACT_BASE_SHA: e3ed5138369c576549cb58a7aff9783792f322d3`
`EXACT_BASE_TREE: 4e7627774fbfc111344e3d80fcb9d921eed8377e`
`ACTIVATION_GATE: EliteSCADA CI #1569 / 36060017969 / SUCCESS / Chromium 655 passed`
`TARGET: wave15/corrections-integration`
`VALIDATION_PROFILE: LICENSING_UX, SESSION_LICENSING`

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

Historical prerequisite — now satisfied: Main recorded `FC0A_RELEASE_APPROVED`, wrote the exact base SHA/tree, created the branch and changed the order to ACTIVE.

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
