# LAST CHANGE — EliteSCADA

**Date:** 2026-09-01 (BRT)  
**Operational state:** **WAVE 11 ACTIVE — issue #194 / `coordination/wave11-hmi-runtime`**

> This file is the mutable coordinator resume point. Stable product intent remains in `PROJECT GOAL.md`. Always verify live refs/Actions before acting because documentation-only `[skip ci]` commits may advance `main` beyond the latest validated code SHA.

## 1. Last accepted code checkpoint

Pre-Wave-11 owner-usability gate #191 is **COMPLETE / ACCEPTED / INTEGRATED**.

- implementation head: `aeb9b3b5641adee344c4ead166b97cc0adba3dbf`;
- PR #193: **MERGED**;
- validated main code merge: `64ba134f88df61233c492f6c5e2b1ea8f244bf19`;
- pre-merge EliteSCADA CI #1033 / `33525910566`: **SUCCESS**;
- pre-merge Preview Licensing CI #90 / `33525910582`: **SUCCESS**;
- pre-merge L3 Seven-Driver Lab #39 / `33525910552`: **SUCCESS**;
- post-main Preview Licensing CI #92 / `33527294658`: **SUCCESS**;
- post-main EliteSCADA CI #1035 / `33527294657`: **SUCCESS after unchanged rerun of one transient IEC-104 timing failure**; backend build/tests/runtime smoke, Web build and Chromium E2E all passed.

PR #192 was closed unmerged only because the connector failed to transition its draft state; PR #193 used the exact same validated head.

Post-main License Generator artifact:

- artifact `EliteSCADA-LicenseGenerator-win-x64`, id `9808306320`;
- `EliteSCADA.LicenseGenerator.exe`, 116,257,103 bytes;
- PE32+ Windows GUI, x86-64;
- executable SHA-256 `841dea832d67f44e07aa10b2de96ccfffd5d518beeadafb48ed34e16d0317523`;
- GitHub ZIP digest `sha256:888ed224f686918f50e27dd5998e105a5e26900edd87254a1f163d7be9416943`.

Historical issue #184 was closed as completed/superseded so its obsolete red checkpoint no longer appears as a live blocker.

## 2. Current coordination state — Wave 11

- issue: **#194 — Wave 11 — Active Engineering HMI Runtime vertical slice**;
- issue state: **OPEN / ACTIVE**;
- branch: `coordination/wave11-hmi-runtime`;
- branch creation base: documentation-synchronized `main` SHA `4be2cb68225cc4222f768ef34a6ed3c808391400`;
- Wave 12: **BLOCKED until #194 is accepted/closed**.

The branch was created before the follow-up documentation-only Wave 11 activation commits. Because no Wave 11 code existed yet at that instant, it may be fast-forwarded to the latest documentation-synchronized `main` before implementation begins.

## 3. Wave 11 objective

Replace the current hand-authored Demo process surface as Runtime application truth with an owner-testable HMI Runtime derived from the **active persisted canonical Engineering revision**.

Required lifecycle authority:

`Working -> saved Revision -> Published -> Active -> HMI Runtime projection`

Editable Working state must not leak into the Runtime visual application before activation.

### Slice A — protected active-revision Runtime projection

The persistence service already exposes `LoadActiveAsync(projectKey)` internally. Add a protected deterministic read boundary for the active canonical Engineering package that:

- uses Active, never Working export;
- verifies configured/runtime project identity and active revision consistency;
- fails closed for unavailable persistence, absent Active revision, inconsistent project/revision or invalid package content;
- inherits existing persistence authorization/security filtering;
- exposes no secrets/transient Runtime values.

### Slice B — canonical HMI Runtime mount

The current default `RuntimeApp` in `web/scada-web/src/main.tsx` still manually renders `Demo.Tank01`, `Demo.P01`, discharge metrics and a custom modal.

Wave 11 must instead:

- resolve current Runtime project/revision;
- load the matching active Engineering package;
- use existing `createRuntimeVisualCatalog` / navigation state;
- render Screens/Popups/Dynamos through `RuntimeVisualDefinitionRenderer` / `CanonicalVisualRenderer`;
- execute existing NavigateScreen/OpenPopup/ClosePopup actions;
- preserve Client Visual Python/event projection and protected Slider/TAG writes;
- keep alarm/TAG/trend/history operational tools available without making them process-screen truth.

A clearly separate simulation fallback may remain when no Engineering runtime is active.

### Slice C — lifecycle isolation proof

Tests must prove:

1. Active revision A renders;
2. Working edits do not change Runtime;
3. activating revision B changes the Runtime visual application to B;
4. inconsistent/missing active projection fails closed rather than falling back to Working;
5. Screen/Popup/Dynamo composition remains canonical;
6. Slider writes remain protected/audited;
7. no frontend-to-Driver or renderer-private Engineering bypass is introduced.

## 4. Existing foundation to reuse

- canonical Screen/Popup/Dynamo Engineering;
- `RuntimeVisualDefinitionRenderer` and `CanonicalVisualRenderer`;
- runtime visual catalog/navigation/action model;
- Dynamo runtime composition and `{equipmentPath}` substitution;
- Client Visual Python runtime/event dispatcher;
- realtime TAG transport/current values;
- protected Runtime TAG write API;
- alarm center, TAG Inspector, Trends and Historical Data Browser;
- PostgreSQL Working/Revisions/Published/Active lifecycle.

Do not duplicate these as Wave 11-private equivalents.

## 5. Exact next action

1. fast-forward `coordination/wave11-hmi-runtime` to the latest documentation-synchronized `main` if needed;
2. implement the protected Active Engineering package Runtime projection;
3. implement the canonical HMI Runtime application mount and navigation using that projection;
4. add focused backend/frontend/lifecycle-isolation tests;
5. run exact-head normal EliteSCADA CI;
6. fix root causes without weakening assertions/security/lifecycle boundaries;
7. integrate by PR only after green evidence, then require exact post-main validation;
8. synchronize all continuity documents and issue #194 before closure.

Wave 13 remains the mandatory Authenticode + trusted timestamp Windows release-signing stage. Physical L4 remains later Preview/device-specific validation.
