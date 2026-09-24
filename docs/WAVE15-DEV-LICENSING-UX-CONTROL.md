# Wave 15 — DEV-LICENSING-UX Control

> GitHub live is the sole authority.

`LANE: DEV-LICENSING-UX`
`MAIN_ORDER_REV: 0003`
`ORDER_ID: DEV-LICENSING-UX-FC0A-01`
`ORDER_STATE: DEV_CORRECTION / LICENSE_STATUS_ENTITLEMENT_TRUTH`
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


## Main candidate review — CHANGES_REQUIRED / ESLIC2 status-entitlement truth

Exact reviewed candidate:
- PR `#345`;
- head `cdf572d644417fe83aee3003a3da3fe171d7ada3`;
- tree `dc8f2b5df3dcc87df4b5fa1cfaeffcbce854810f`.

Main accepts the implemented direction:
- License Generator now emits signed ESLIC2 using the frozen FND-03 codec;
- CLI/GUI author explicit Interactive/ViewOnly totals and HA Runtime entitlement;
- private signing key remains external;
- Runtime Session panel explicitly requests ViewOnly or Interactive and displays requested/granted/fallback truth;
- capacity rejection reason remains server-owned;
- no client quota/admission authority was introduced.

One mission residual is material and remains DEV-owned before CODEX:

`LICENSING_STATUS_ENTITLEMENT_TRUTH_MISSING`

Live exact-head evidence:
- `ProductLicensingApi.DescribeLicense` exposes state/tier/TAG capacity/ID/expiry/key/diagnostic but not license schema or `SessionEntitlements`;
- `LicensingApp.tsx` therefore cannot tell the user whether the installed valid license is ESLIC1 or ESLIC2 and cannot show signed Interactive seats, ViewOnly seats or HA Runtime entitlement;
- the lane mission explicitly requires user-facing licensing status/entitlement truth and old/new-schema experience.

Disposition:

`DEV-LICENSING-UX -> MAIN COORDINATOR — CHANGES_REQUIRED`

### CURRENT CORRECTION ORDER

`ORDER_ID: DEV-LICENSING-UX-STATUS-ENTITLEMENTS-02`

`ORDER_STATE: DEV_CORRECTION / AUTHORIZED`

`CORRECTION_BASE_HEAD: cdf572d644417fe83aee3003a3da3fe171d7ada3`

Authorized bounded shared API delegation:
- `src/Scada.Api/Licensing/ProductLicensingApi.cs` may be changed only to add a truthful read projection of already-frozen license data;
- do not change verification, signing, seat admission, quota reservation or lifecycle authority.

Required behavior:

1. `GET /api/licensing/status` and existing install/remove license projections must expose:
   - installed license schema/version when a license exists;
   - ESLIC2 signed `viewOnlySeats`;
   - ESLIC2 signed `interactiveSeats`;
   - ESLIC2 signed `haRuntime`.

2. Preserve truthful ESLIC1 compatibility:
   - ESLIC1 must not invent zero seats or `haRuntime=false` as if those were signed entitlements;
   - represent “legacy / session-class entitlements not specified” explicitly/nullably.

3. Update `LicensingApp.tsx` to show this information in pt-BR/en/es:
   - schema/generation;
   - Interactive total;
   - ViewOnly total;
   - HA Runtime entitlement;
   - clear legacy/not-specified state.

4. Do not claim current seat **usage** unless a canonical backend read model already exists. Signed totals are required now; usage may remain deferred rather than inferred client-side.

5. Retain all current ESLIC2 generator/session-class behavior and tests.

6. Add bounded regression evidence for:
   - ESLIC2 status projection;
   - ESLIC1 nullable/legacy projection;
   - mounted/display parsing where practical.

The first T1 `36063210159` was metadata-invalid only. Main has corrected the PR body and re-opened the same exact SHA to trigger a natural PR validation; any resulting run on this pre-correction head is evidence only for that head, not acceptance of the missing entitlement UX.

After correction return:

`DEV-LICENSING-UX -> MAIN COORDINATOR — CANDIDATE HANDOFF`

with new exact SHA/tree, changed files and focused evidence.

No CODEX routing, merge or T2 authorization yet.
