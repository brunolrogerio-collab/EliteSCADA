# LAST CHANGE — EliteSCADA

**Date:** 2026-09-01 (BRT)  
**Operational state:** **WAVE 11 ACTIVE / RESUMED — repository/CI hygiene complete**

> Mutable Coordinator resume point. Stable product intent remains in `PROJECT GOAL.md`. Live GitHub refs and exact-SHA Actions override copied prose. Documentation-only `[skip ci]` commits may advance `main` beyond the latest validated product-code SHA.

## 1. Last accepted mainline checkpoint

The pre-Wave-11 repository/CI hygiene gate is **COMPLETE / ACCEPTED / INTEGRATED**.

### Hygiene integration

- PR #196 — specialized CI routing + stale PR/issue sanitation: **MERGED** at `95abfad5c35f2715e15422f726d93013fd7290fd`.
- PR #197 — focused Preview Licensing ownership + synchronization fixes exposed by universal CI: **MERGED**.
- exact #197 implementation head: `01fc71df4162c3728073ae4fc056d04429349e4f`.
- validated main product-code merge: `e117849827f1409ad6dd383dbdc2ed936ce62567`.

Exact-head pre-merge #197 evidence:

- EliteSCADA CI #1057 / `33539711101`: **SUCCESS**;
- Preview Licensing CI #115 / `33539711114`: **SUCCESS**;
- L3 Seven-Driver Lab #62 / `33539711124`: **SUCCESS**.

Exact post-main `e1178498...` evidence:

- EliteSCADA CI #1058 / `33543916943`: **SUCCESS** including backend build/tests/runtime smoke, Web build and Chromium E2E;
- Preview Licensing CI #116 / `33543916958`: **SUCCESS**;
- L3 Seven-Driver Lab #63 / `33543916942`: **SUCCESS** including heterogeneous Gateway, seven-Driver acquisition, supported writes, serial recovery and Gateway source/destination recovery.

### CI policy now in force

`EliteSCADA CI` is the universal Coordinator acceptance gate for PRs to `main`.

`Preview Licensing CI` is automatically selected only by licensing, License Generator, product-capacity and known licensing-sensitive paths. Its focused test ownership now covers licensing/capacity and `ProductLicensedRuntimeCoordinatorTests` while retaining full solution build, License Generator build, host licensing smoke and Windows artifact validation.

`L3 Seven-Driver Lab` is automatically selected by Driver/DriverHost/communication/Gateway/TAG-event/Driver-test/interop-lab paths.

Both specialized workflows remain manually executable for cross-cutting/release validation. Path filters never replace engineering judgment.

Repository fact: `main` currently has no branch protection / required status checks configured. The universal EliteSCADA CI rule is therefore a Coordinator/Development Lead operating rule, not a GitHub-enforced merge block.

### Synchronization reliability fixes integrated with #197

- Siemens S7 publishes `Ready` only after the initial acquisition outcome is visible in the canonical current TAG cache;
- the S7→Modbus Gateway integration test waits on authoritative Gateway transfer completion instead of using peer-side register mutation as the internal completion barrier;
- the Modbus diagnostics isolation test keeps failing driver B at 80 ms with 250 ms injected delay while unaffected control A uses a normal 500 ms timeout, preserving isolation assertions under hosted-runner scheduling.

No assertion/security/product acceptance boundary was weakened.

## 2. Repository hygiene state

Historical/superseded PRs #108, #109, #111, #128, #135, #146, #167, #169, #176, #177, #179, #181 and #182 are closed with integration lineage retained in their conversations.

Completed Driver coordination issues #120-#123 are closed. Issue #178 remains intentionally open only as **deferred L4 Siemens hardware/vendor-simulator evidence** and is not a Wave 11 blocker.

Historical branch refs remain numerous. The connected GitHub action set does not provide safe delete-ref, so old refs must not be repointed merely for cosmetics.

Durable routing/hygiene policy: `docs/CI-VALIDATION-POLICY.md`.

## 3. Wave 11 — current implementation

Wave 11 is **ACTIVE / RESUMED**.

- issue #194: **OPEN / ACTIVE**;
- draft PR #195: **OPEN / DRAFT**;
- branch: `coordination/wave11-hmi-runtime`;
- current pre-reconciliation branch head: `b2c93cbfca3c4794796a0b251ab612b32ee08fed`;
- Wave 12: **BLOCKED until #194 is accepted/closed**.

Current Wave 11 implementation includes:

- protected `/api/runtime/application` projection derived from persisted **Active** Engineering, never mutable Working;
- project/revision consistency checks and fail-closed behavior when persistence/identity/package authority is inconsistent;
- canonical Screen/Popup/Dynamo Runtime mount through the existing renderer/navigation stack;
- explicit Simulation fallback only when no Engineering Runtime is active;
- revision-stable mount behavior so polling does not reset navigation/popups while the Active identity is unchanged;
- protected Runtime `View` authorization for the HMI without granting operator access to Working Engineering;
- active revision visual-asset endpoint with SHA-256/media-type/length integrity checks;
- explicit Active asset authority propagated through Runtime mount -> Navigator -> DefinitionRenderer -> CanonicalRenderer;
- deterministic lifecycle test using `builtin.memory.server` because `builtin.simulation` is intentionally not an activatable Engineering source.

Exact current pre-reconciliation Wave 11 evidence on `b2c93cbf...`:

- Wave 11 Active HMI Runtime #8 / `33544807476`: **SUCCESS**;
- the dedicated browser proof covers Simulation -> Active A -> Working isolation -> Active B, operator Runtime View vs Working denial, fail-closed projection, and a real imported PNG served from `/api/runtime/visual-assets/{id}/content` rather than the Working asset route;
- EliteSCADA CI #1061 / `33544807512`: Web build **SUCCESS**, backend build/tests/runtime smoke **SUCCESS**, Chromium E2E **IN PROGRESS** at this checkpoint.

CI routing proof: Wave-11-only heads automatically selected **EliteSCADA CI + the dedicated Wave 11 workflow only**. Preview Licensing CI and L3 Seven-Driver Lab did not auto-run because no affected specialized paths were changed.

## 4. Owner-test Demo deliverable

When the demonstration application is finalized for owner testing with EliteSCADA Preview, deliver an actual portable application package, preferably the canonical `.escadapkg`, not merely source code or CI evidence. The deliverable must be usable through the normal Save/Open application contract. Issue #194 carries this owner-test delivery requirement; an existing condition watch will surface the artifact when it is actually ready.

## 5. Exact next action

1. let EliteSCADA CI #1061 finish Chromium on exact Wave 11 head `b2c93cbf...`; diagnose any failure instead of blindly rerunning;
2. reconcile `coordination/wave11-hmi-runtime` with the final live post-hygiene `main` while preserving Wave 11 history and all `main` commits;
3. remove the temporary hygiene pause language from PR #195 and record the reconciled head in issue #194;
4. require exact reconciled-head Wave 11 dedicated CI + universal EliteSCADA CI green; specialized CI should remain unselected unless the reconciled diff/risk actually touches those subsystems;
5. complete any remaining #194 acceptance gaps, mark #195 ready only when the gate is satisfied, merge normally, then require exact post-main CI;
6. accept/close #194 and synchronize continuity docs only after post-main validation;
7. Wave 12 remains blocked until that acceptance.

`PROJECT GOAL.md` was reviewed during this coordination cycle; no stable product architecture changed. Wave 13 remains the mandatory Authenticode + trusted timestamp Windows release-signing stage. Physical L4 remains later Preview/device-specific validation.
