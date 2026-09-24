# Wave 15 — DEV-LICENSING-UX Control

> GitHub live is the sole authority.

`LANE: DEV-LICENSING-UX`
`MAIN_ORDER_REV: 0004`
`ORDER_ID: DEV-LICENSING-UX-CODEX-VALIDATION-V1`
`ORDER_STATE: MAIN_ACCEPTED_FOR_CODEX / QUEUED / DEV_WAIT`
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


### Additional exact-head T1 evidence — unrelated IEC-104 observation race

Natural T1 after Main repaired PR metadata:

`36067628680`

Results on exact old head `cdf572d644417fe83aee3003a3da3fe171d7ada3`:
- classifier: SUCCESS;
- Common sanity: SUCCESS;
- Web semantic build: SUCCESS;
- focused Chromium: SUCCESS;
- focused .NET: **692/693**, one failure.

Only failure:

`Iec104TcpFaultInjectionTests.Adapter_OutOfOrderIFrameFaultsBeforePublishingAsdu`

at the immediate assertion:
`Assert.False(diagnostics.IsConnected)`.

Main proved unchanged lineage between FC0-A release base and this Licensing head:
- test blob `b9afd137678d225182de2288e879afbacef2579f`;
- `Iec104TcpClientAdapter.cs` blob `18c836aa7a6935121104020aa63c50966475b2b8`;
- `Iec104SequenceState.cs` blob `7c0f66374e679faf181f98504f12946e81a6e388`.

Root cause is in the existing test observation boundary:
1. the test waits only until `ProtocolErrors >= 1`;
2. adapter catch increments `_protocolErrors`;
3. only **after that** it calls `SignalSessionFailure`;
4. `SignalSessionFailure` increments session failures and writes `_connected = 0`;
5. the test can therefore legally wake between steps 2 and 4 and observe `ProtocolErrors=1` while `IsConnected=true`.

Classification:

`IEC104_TEST_OBSERVATION_RACE / NOT_LICENSING_CAUSAL / SHARED_TEST_INFRA_DEFECT`

Do not mutate IEC-104 code/test in this Licensing lane and do not use an unchanged-head rerun to hide it.

The Licensing product correction remains exactly `DEV-LICENSING-UX-STATUS-ENTITLEMENTS-02`.

A separate shared test-infrastructure closeout owns the IEC-104 assertion synchronization.


## Successor Main review — Licensing status-entitlement correction accepted

Exact corrected candidate:
- head `f5d3212b9c114d3ad6e2239460172db0b3d568f8`;
- tree `a3e87f2ef7beb15bc66d6980e12710eddcc30525`;
- correction delta from `cdf572d6...`: 6 commits / 5 bounded files;
- natural T1 `36071779912`: SUCCESS across classifier, Common, Web, focused .NET, focused Chromium and final gate.

Main contract review confirms the prior material residual is closed:
- server status projection now exposes actual license `SchemaVersion`;
- signed ESLIC2 `ViewOnlySeats`, `InteractiveSeats` and `HaRuntime` are projected from verified `SessionEntitlements`;
- ESLIC1/legacy values remain nullable/not-specified rather than fabricated as zero/false;
- Licensing UI displays schema + signed entitlement totals/HA truth in pt-BR/en/es;
- no seat-usage pool, quota/admission authority, signing-key boundary or HA fencing authority moved client-side.

Disposition:
`DEV-LICENSING-UX -> MAIN_ACCEPTED_FOR_CODEX / QUEUED / DEV_WAIT`

`ACCEPTED_CANDIDATE_SHA: f5d3212b9c114d3ad6e2239460172db0b3d568f8`

`ACCEPTED_CANDIDATE_TREE: a3e87f2ef7beb15bc66d6980e12710eddcc30525`

`EXPECTED_CODEX_ORDER: DEV-LICENSING-UX-CODEX-VALIDATION-V1`

CODEX remains sequential and is **not routed here yet**. Until Main publishes an explicit shared route, this DEV must wait and make no further candidate mutation.

CODEX later owns the remaining old/new-license, ViewOnly fail-closed, requested/granted/fallback, logical lease/quota and mounted UX negatives against this exact accepted candidate.

No merge/T2 authorization yet.


## Replacement Main follow-up — corrected candidate accepted for CODEX

Exact corrected candidate:
- head `f5d3212b9c114d3ad6e2239460172db0b3d568f8`;
- tree `a3e87f2ef7beb15bc66d6980e12710eddcc30525`;
- natural T1 `36071779912`: SUCCESS.

Main accepts closure of `DEV-LICENSING-UX-STATUS-ENTITLEMENTS-02`:
- status projection exposes license schema plus nullable signed ESLIC2 Interactive/ViewOnly/HA entitlements;
- ESLIC1 remains truthful legacy/null and is never fabricated as zero/false;
- Licensing UI exposes that truth in pt-BR/en/es;
- no client-side admission/quota authority was introduced.

`CANDIDATE_PR: #345`

`CANDIDATE_HEAD: f5d3212b9c114d3ad6e2239460172db0b3d568f8`

`CANDIDATE_TREE: a3e87f2ef7beb15bc66d6980e12710eddcc30525`

`CANDIDATE_T1: 36071779912 / SUCCESS`

`STATE: MAIN_ACCEPTED_FOR_CODEX / QUEUED / DEV_WAIT`

The shared sequential CODEX route remains Script Engineering #344. This lane must not mutate while queued unless Main returns a material defect. No merge/T2 authorization exists yet.
