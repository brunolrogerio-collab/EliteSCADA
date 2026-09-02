# EliteSCADA — Current Coordinator Handoff

**Last operational synchronization:** 2026-09-02 BRT  
**Status:** **WAVE 12 COMPLETE / ACCEPTED; TEST PREVIEW #208/#210 ACTIVE AS VALIDATION HARNESS; WAVE 14 #211 ACTIVE EARLY; WAVE 13 #205/#207 PAUSED AT GREEN CHECKPOINT**

> GitHub/main/CI is implementation truth. `PROJECT GOAL.md` governs permanent product intent. `LAST CHANGE.md` is the mutable resume point. Never resume from chat alone.

## 1. Current coordination direction

Development Lead direction on 2026-09-02 changed the execution order after real Codespaces owner use exposed product/usability findings before final release signing.

Active fronts are now:

- **Temporary Browser Test Preview:** issue #208 / draft PR #210 / branch `preview/codespaces-test-preview`; this is the reproducible browser test harness;
- **Wave 14 Product-owner validation:** issue #211; this is the active product-validation priority;
- **Wave 13 Windows release/signing:** issue #205 / draft PR #207 / branch `wave13/windows-release-signing`; **paused**, preserved at its already-green repository-side checkpoint.

The key rule is simple: do not sign/release a stale pre-validation product while owner validation is still exposing and correcting material defects.

## 2. Mandatory resume protocol

Before changing code, read in this order:

1. `PROJECT GOAL.md`;
2. `LAST CHANGE.md`;
3. this handoff;
4. `docs/ROADMAP.md`;
5. `docs/CI-VALIDATION-POLICY.md`;
6. live `main`, open PRs/issues and exact Actions state.

Then read the active workstream material:

### Preview / Wave 14 coordinator

7. `docs/TEMPORARY-BROWSER-TEST-PREVIEW.md`;
8. `docs/CODESPACES-PREVIEW-RUNBOOK.md` from the current Preview branch while PR #210 remains unmerged;
9. issue #208;
10. draft PR #210 and branch `preview/codespaces-test-preview`;
11. issue #211 — Wave 14 Product-owner validation.

### Wave 13 resume coordinator

Wave 13 is currently paused. When Development Lead explicitly resumes it, first read:

7. `docs/WAVE-13-WINDOWS-RELEASE-PREPARATION.md`;
8. issue #205;
9. draft PR #207 and branch `wave13/windows-release-signing`;
10. accepted Wave 14 baseline/evidence;
11. compare Wave 13 against then-live `main` before any signing or merge action.

If repository state differs from copied prose, GitHub/main/CI wins.

## 3. Accepted foundation

Wave 11 issue #194 is **CLOSED / COMPLETED**.  
Wave 12 issue #201 is **COMPLETE / ACCEPTED / CLOSED**.

Accepted Wave 12 product-code baseline:

`63bced02426fcb84b26028913f6c68feb3457d80`

Exact post-merge acceptance evidence:

- EliteSCADA CI #1096 / `33576603185`: **SUCCESS**;
- L3 Seven-Driver Lab #92 / `33576603158`: **SUCCESS**.

Accepted lifecycle authority remains:

`Working -> saved Revision -> Published -> Active -> HMI Runtime`

Runtime uses persisted Active Engineering only; Working edits remain isolated until Save/Publish/Activate. Accepted Wave 11/12 architecture must not be reopened without a demonstrated defect.

Owner-test package from Wave 11 remains:

`EliteSCADA-Wave11-Demo.escadapkg`  
SHA-256 `13261af59b8707df7d9ef3bbea307cb0c85d945ea8f47315fb693c92c885efa1`

## 4. Preview harness state

Tracking: issue #208 and draft PR #210.

Real Codespace homologation already established several repository-controlled requirements that Actions alone initially missed:

- exact .NET SDK 10.0.400 in the app devcontainer;
- one disposable machine identity mounted at `/etc/machine-id` so normal product licensing can remain fail-closed;
- protected Codespaces secret `ELITESCADA_PREVIEW_ADMIN_PASSWORD`;
- current product password policy requires at least 12 characters;
- automatic launcher through `postAttachCommand`;
- Web on forwarded port 5173, with API/DB remaining internal;
- successful actual browser login after those conditions were satisfied.

Operational procedure is documented in `docs/CODESPACES-PREVIEW-RUNBOOK.md` on the Preview branch. The runbook defines four recovery levels: browser reload, Preview launcher restart, Rebuild Container and fresh Codespace.

A 5173 HTTP 502 means forwarding exists but no Web process is listening; it is never accepted as ready state.

Administrative username:

`EliteSCADA`

Protected secret name:

`ELITESCADA_PREVIEW_ADMIN_PASSWORD`

Never commit or echo the supplied password.

## 5. Wave 14 active handoff state

Issue #211 is **ACTIVE EARLY / PRODUCT-OWNER VALIDATION STARTED THROUGH PREVIEW**.

The Preview is infrastructure; Wave 14 is the actual product-validation work.

For each owner-validation session:

1. record the exact SHA exercised;
2. validate one product area at a time through the real UI;
3. record concrete observed behavior;
4. classify findings;
5. correct blockers/material defects only when needed to keep validation meaningful;
6. rerun exact-head universal and impact-specific CI after product-code changes;
7. transfer non-blocking feedback/enhancements to Wave 15.

Finding classes defined by #211:

- A — validation blocker;
- B — functional defect;
- C — usability defect;
- D — enhancement/preference.

The first confirmed owner finding is a pre-existing Script Engineering contrast defect. Light panels/inputs inherited light Engineering text, making that surface effectively unreadable. A narrow correction was started in PR #210 because it blocked meaningful validation.

## 6. Wave 13 paused checkpoint

Issue #205 is **PAUSED BY DEVELOPMENT LEAD**. Draft PR #207 remains draft/open.

Preserved fully validated Wave 13 implementation SHA:

`9f26a2bc02ae77017e266c52ff128dc39eece4b4`

Retained validation:

- Wave 13 Windows Release #27 / `33643546191`: **SUCCESS**;
- EliteSCADA CI #1134 / `33643546119`: **SUCCESS**;
- L3 Seven-Driver Lab #102 / `33643546111`: **SUCCESS**;
- Wave 11 Active HMI Runtime #64 / `33643546139`: **SUCCESS**.

Current Wave 13 branch head after docs synchronization:

`fda87ba4445127c174f6ea533a6bcabaabc7bb20`

Do not advance Authenticode, merge or release from this snapshot while Wave 14 is active. When Wave 13 resumes, incorporate accepted Wave 14 corrections and rerun its package/signing validation on the actual post-validation product.

Existing release locks remain intact: protected Authenticode authority, trusted timestamp, deterministic/fail-closed verification, no private signing material in source/GitHub/normal artifacts/logs, and separate DNP3 commercial authorization.

## 7. CI / merge rules

- EliteSCADA CI remains the universal Coordinator gate for PRs to `main` when product code changes;
- specialized validation complements but never replaces universal CI;
- diagnose failures before rerun;
- do not weaken authentication, authorization, licensing, Runtime authority or tests to manufacture green evidence;
- integration uses expected-head protection;
- validate post-merge `main` when product code changes;
- documentation-only coordination changes may use `[skip ci]` according to repository policy.

## 8. Immediate next action

Continue Wave 14 owner validation through the Preview, one surface at a time. Keep #205/#207 paused. Use #211 as the product-finding ledger and #208/#210 as the Preview-infrastructure ledger.

Before every correction, decide whether the finding is a validation blocker/material defect or merely Wave 15 feedback. Do not let the convenience of a live Preview silently expand product scope.
