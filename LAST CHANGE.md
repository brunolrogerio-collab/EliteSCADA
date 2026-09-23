# LAST CHANGE — EliteSCADA

**Date:** 2026-09-23 BRT  
**Operational state:** **WAVE 15 ACTIVE / FND-03 VERIFIED+FROZEN / FND-04 ACTIVE / FND-06 NOT STARTED / INFRA-CI-01 PENDING / FC0-A BLOCKED**

> GitHub live is the official memory.
>
> Canonical operational handoff: `docs/WAVE15-MAIN-COORDINATOR-HANDOFF.md`.

## Latest verified product checkpoint

PR #334 merged and post-merge verified:

`a3eb86f8e1022675f84f0a76129a64d8e9d5faa6`

tree:

`e48c8b9918f4d3a5ae4dee1df6211393c95b6513`

## Verification

Candidate CI #1558 / `35813975645`:
- Backend/Web/Chromium SUCCESS.

Exact post-merge CI #1559 / `35815261288`, attempt 2:
- Web `107058812138` — SUCCESS;
- Backend `107058810300` — SUCCESS;
- Chromium `107059153655` — SUCCESS / 624 passed.

Attempt 1 had one isolated PostgreSQL advisory-lock test failure outside the FND-03 product delta. Main used one diagnosed failed-backend-job rerun; the same exact SHA then completed green without product/workflow mutation.

Therefore **FND-03 global = VERIFIED/FROZEN**.

## Current active work

FND-04 Script TAG Reference Resolution is **ACTIVE**.

Plan:

`FND04-TAGREF-V1`

Exact product base:

`a3eb86f8e1022675f84f0a76129a64d8e9d5faa6`

Work branch:

`work/w15-fnd-04-script-tag-reference-resolution`

Dedicated control-plane activation commit:

`cea43de5141824057822657d35ed2fb32b3d03f4`

DEV order:

`FND04-DEV-TAGREF-V1-01`

Key execution shape:
- mandatory RED-1/RED-2/RED-3 before production correction;
- additive path-readable Script TAG binding with expected stable TagId;
- resolver states found/notFound/ambiguous/stale/identityDrift;
- one read/write semantic;
- legacy GUID compatibility;
- closed production/test allowlists;
- exact-head focused tests + natural CI;
- no self-merge.

FND-04 AUD remains WAIT_CANDIDATE / READ_ONLY until Main supplies an immutable DEV SHA/tree.

## Remaining FC0-A blockers

- FND-04 — ACTIVE.
- FND-06 — NOT STARTED.
- INFRA-CI-01A — ACTIVE in CODEX on `work/w15-infra-ci-01-profile-orchestration`.

CI efficiency audit: the universal Chromium gate currently runs 624 tests serially on one Playwright worker and dominates ~13.5–14.5 minute full-CI wall time. INFRA-CI-01 should preserve assertions while moving Wave 15 leaf PRs to profile-aware T1 evidence and proving isolated browser sharding for broader checkpoints.


## Parallel CI infrastructure work

CODEX order:

`INFRA-CI-01A-REVIEW-CLOSE-02`

Exact branch base:

`084d48f833415797f62ec525d0192def1a380592`

Work branch:

`work/w15-infra-ci-01-profile-orchestration`

Mission: add `wave15-pr.yml` profile-aware T1 routing, deterministic profile classifier/tests, and return the universal `dotnet-ci.yml` Wave 15 role to broad integrated push/checkpoint validation instead of every leaf PR. Existing heavy workflow assertions remain untouched. No merge authority.


## INFRA-CI-01A Main review correction

PR #335 head `2069f4cb1ea398da615286001102fa44cbc35e67` has natural T1 CI green, but Main review found four pre-integration defects:

- real FND-04 Server Script runtime files under `src/Scada.Api/Runtime/**` were not inferred as `SCRIPT_RUNTIME`;
- real `web/scada-web/src/engineering/scripts/**` files were not inferred as `SCRIPT_ENGINEERING`;
- manual dispatch compared only the last commit instead of the full branch delta to Wave 15 integration;
- `AUTHORITY_UX`, `LICENSING_UX` and `ELITEGO_RUNTIME` did not independently request their owning backend evidence.

CODEX order is now `INFRA-CI-01A-REVIEW-CLOSE-02`; correction remains inside the original six-file allowlist. PR #335 is not approved for merge yet.


## FND-04 environment disposition

The original FND-04 DEV chat reported `BLOCKED-ENV` before any RED test or product/test mutation because its local runtime cannot reach GitHub and the connector cannot execute dotnet/Node/Playwright.

Main accepted the blocker and preserved the branch untouched at:

`a3eb86f8e1022675f84f0a76129a64d8e9d5faa6`

Execution is now delegated to a dedicated functional-runtime lane:

`FND04-CODEX-TAGREF-V1-01`

Control commit:

`e871d51238881824e57778cf884b246ba8163bec`

The normal DEV chat is `BLOCKED_ENV / WATCH_ONLY`; FND-04 AUD remains `WAIT_CANDIDATE`. The Codex executor must use the exact same section 3B plan, RED-before-production sequence, allowlists, branch and no-merge boundary.
