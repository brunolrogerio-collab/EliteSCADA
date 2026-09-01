# EliteSCADA — Current Coordinator Handoff

Last operational audit: **2026-09-01 BRT**  
Operational status: **PRE-WAVE-11 REPOSITORY/CI HYGIENE GATE ACTIVE — PR #196; Wave 11 #194/#195 temporarily paused**

> **THIS FILE IS THE SINGLE OPERATIONAL HANDOFF FOR COORDINATOR CONTINUITY.**
>
> Live GitHub refs and exact-head Actions evidence override SHAs copied into prose. Stable product intent is governed by `PROJECT GOAL.md`. Mutable exact state belongs in `LAST CHANGE.md`.

## 1. Mandatory resume protocol

A replacement Coordinator must read, in order:

1. `PROJECT GOAL.md`;
2. `LAST CHANGE.md`;
3. this file;
4. `docs/ROADMAP.md`;
5. `docs/CI-VALIDATION-POLICY.md`;
6. live `main`, PR #196 and its Actions while the hygiene gate is open;
7. issue #194, draft PR #195 and `coordination/wave11-hmi-runtime` after the hygiene gate integrates;
8. `docs/DRIVER-AND-INTEROP-LAB-STATUS.md` for completed Driver evidence;
9. issues #174, #180, #183 and #191 as historical acceptance authority.

Repository state, not old chat messages, is the continuity source.

## 2. Last accepted mainline product checkpoint

Pre-Wave-11 issue #191 is **COMPLETE / ACCEPTED / INTEGRATED**.

- PR #193 — merged;
- validated main code SHA: `64ba134f88df61233c492f6c5e2b1ea8f244bf19`;
- implementation head: `aeb9b3b5641adee344c4ead166b97cc0adba3dbf`;
- Preview Licensing CI #92 / `33527294658`: **SUCCESS**;
- EliteSCADA CI #1035 / `33527294657`: **SUCCESS after unchanged rerun of a transient IEC-104 timing assertion**;
- backend build/tests/runtime smoke, Web build and Chromium E2E all passed.

Seven-Driver convergence is also no longer an active workstream:

- convergence integrated through PR #187;
- integrated seven-Driver L3 issue #180 is closed **COMPLETE / ACCEPTED / INTEGRATED**;
- old worker/lab PRs are historical evidence, not pending merge surfaces.

## 3. Why the hygiene gate exists

The Development Lead observed that Wave 11 PRs were automatically starting three pipelines: EliteSCADA CI, Preview Licensing CI and L3 Seven-Driver Lab, even when the change did not affect licensing or communication.

The requested rule is now the coordination policy:

- EliteSCADA CI = universal acceptance gate for every PR to `main`;
- Preview Licensing CI = automatic only for licensing/License Generator/product-capacity and known licensing-sensitive paths, plus manual/release execution;
- L3 Seven-Driver Lab = automatic only for Driver/DriverHost/communication/Gateway/TAG-event/Driver-test/interop-lab paths, plus manual/cross-cutting/release execution;
- structural changes outside the path matrices that can affect a specialized subsystem require manual specialized validation;
- no specialized workflow test body is weakened.

Durable authority: `docs/CI-VALIDATION-POLICY.md`.

Repository fact: `main` currently has **no branch protection / required status checks configured**. Therefore `EliteSCADA CI required` is currently an operational Coordinator/Development Lead rule rather than a GitHub-enforced merge block.

## 4. Active hygiene branch and PR

- branch: `coordination/ci-hygiene-pre-wave11`;
- branch base: `main` `ab787c98350861566d06d1de07ade0b08c82ce3e`;
- PR: **#196 — Repository hygiene: scope specialized CI before Wave 11 resumes**;
- PR state: **OPEN / NON-DRAFT**;
- exact live head must be re-read because documentation commits advance it;
- changed product behavior: none;
- changed operational behavior: specialized workflow automatic routing only.

Because PR #196 changes both specialized workflow definitions, this PR is intentionally expected to run all three workflows once. Require:

1. EliteSCADA CI green;
2. Preview Licensing CI green;
3. L3 Seven-Driver Lab green;
4. no workflow syntax/routing regression.

Only then merge #196.

## 5. Repository sanitation already performed

Stale/superseded open PRs closed with lineage comments:

- #108 DNP3 worker handoff;
- #109 BACnet worker handoff;
- #111 CIP/EtherNet-IP worker handoff;
- #128 MQTT worker handoff;
- #135 Siemens S7 worker handoff;
- #146 IEC-104 worker handoff;
- #167 DNP3 L2 validation;
- #169 OPC UA worker handoff;
- #176 OPC UA L2 validation;
- #177 BACnet L2 validation;
- #179 secure OPC UA L2 validation;
- #181 obsolete licensing workstream;
- #182 obsolete licensing validation-only PR.

After that cleanup, the active development PR is #195; #196 is the temporary hygiene PR.

Completed convergence issues closed:

- #120 IEC-104 coordination checkpoint;
- #121 Siemens S7 coordination checkpoint;
- #122 OPC UA coordination checkpoint;
- #123 MQTT coordination checkpoint.

Issue #178 remains intentionally open but is retitled:

`[Deferred L4] Driver 8: Siemens hardware/vendor-simulator interoperability validation matrix`

It is future physical/vendor evidence only. It does not block Wave 11 and must not reopen completed Driver convergence.

Historical branches remain numerous. The current connected GitHub action set has no branch delete-ref operation. Do not fake cleanup by moving/repointing old refs. Mechanical deletion of clearly obsolete closed-PR branches is a later repository-maintenance action when a safe delete-ref path is available.

## 6. Wave 11 current state

Wave 11 is **active but paused behind PR #196**, not cancelled and not to be restarted from scratch.

- issue: #194 — Active Engineering HMI Runtime vertical slice;
- branch: `coordination/wave11-hmi-runtime`;
- draft PR: #195;
- pre-hygiene Wave 11 head: `2d85b910f7bcca4239bde54f71e8e81cd883ffe2`;
- Wave 12 remains blocked until #194 closes.

Existing Wave 11 work already includes:

- protected Active Engineering Runtime projection foundation;
- canonical Runtime application mount with explicit simulation fallback;
- mount stability while Active project/revision is unchanged;
- dedicated Active A -> Working isolation -> Active B lifecycle test infrastructure;
- test fixture correction from non-activatable `builtin.simulation` to deterministic `builtin.memory.server` after the first dedicated run exposed `RUNTIME_NO_ACTIVE_SOURCES`;
- first renderer asset-authority seam so active Runtime images can stop resolving from mutable Working state.

Do not discard or recreate this work.

## 7. Exact next action

1. read live PR #196 head and its three workflow runs;
2. if any run fails, diagnose/fix the actual workflow or policy defect without weakening validation;
3. merge #196 normally only after all three are green;
4. verify live `main` contains the narrowed workflow triggers and policy document;
5. synchronize continuity docs if merge/post-main state changes exact refs;
6. reconcile `coordination/wave11-hmi-runtime` with the new `main` while preserving its existing commits;
7. verify a Wave-11-only change no longer automatically starts Preview Licensing CI or L3 Seven-Driver Lab unless its paths/impact require them;
8. remove the temporary pause note from #195 and resume #194 implementation from the existing branch.

## 8. Wave 11 acceptance gate after resume

Before #194 closes:

1. protected active-revision projection is complete and fail-closed;
2. canonical Screen/Popup/Dynamo Runtime mount uses the active persisted revision;
3. active visual assets come from the active persisted revision, not Working;
4. lifecycle proof establishes Active A -> Working isolation -> Active B;
5. protected Slider/TAG writes and Client Visual behavior stay on established boundaries;
6. exact implementation head EliteSCADA CI is green;
7. specialized CI runs only when automatic path/impact policy requires it or Coordinator invokes it manually;
8. normal PR integration to `main` and exact post-main validation succeed;
9. issue #194 and continuity documents are synchronized.

## 9. Non-negotiable rules

- Repository/CI state overrides stale chat/prose for implementation truth.
- Stable product rules belong in `PROJECT GOAL.md`; mutable exact state belongs in `LAST CHANGE.md`.
- `docs/CI-VALIDATION-POLICY.md` governs CI routing and repository hygiene.
- No red core CI into `main`.
- Specialized path filters never excuse a manual run when architectural impact requires it.
- Do not weaken tests to manufacture green evidence.
- No Driver-to-Driver calls or canonical TAG/cache/event bypass.
- No plaintext protected material.
- `CommunicationBinding` remains canonical in schema v15.
- Licensing remains host-owned; Drivers never inspect license files/hardware IDs directly.
- Private license-signing material never enters GitHub, CI or distributed product binaries.
- L2 does not imply L3; L3 does not imply physical L4.
- Every material coordination transition must be persisted before reporting completion.
