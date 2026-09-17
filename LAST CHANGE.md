# LAST CHANGE — EliteSCADA

**Date:** 2026-09-17 BRT  
**Operational state:** **WAVE 15 ACTIVE / FND-03 ACTIVE / RUNTIME ADMISSION VERIFIED+FROZEN / SHARED RUNTIME SEAT ACCOUNTING PR_READY WITH EXACT-HEAD CI GREEN / FND-04 QUEUED / FC0-A BLOCKED**

> **GitHub live is the official project memory.** Revalidate refs, exact SHA/tree, branches, PRs, issues and Actions before every material decision.

> **Canonical operational handoff:** `docs/WAVE15-MAIN-COORDINATOR-HANDOFF.md`.
>
> **Generic successor/bootstrap prompt:** `docs/NEXT-COORDINATOR-CHAT-HANDOFF.md`.
>
> `docs/CURRENT-COORDINATOR-HANDOFF.md` is only the short bridge.

## Latest verified product checkpoint

Repository: `brunolrogerio-collab/EliteSCADA`  
Integration branch: `wave15/corrections-integration`

Latest verified product-code checkpoint:

`6f02b9e3c1327b34ff33bab22e90aaf24dfc4628`

Tree:

`53ffaf05ecd492d06ecf7852bd6e77b48431a1eb`

This checkpoint contains PR #330 / FND-03 Runtime Admission. Exact post-merge CI #1543 validated Backend, Web and Chromium.

The live integration HEAD may be ahead because coordinator documentation was updated. Do not confuse docs-only `COORDINATION HEAD` with `PRODUCT CHECKPOINT`.

## Foundation state

- FND-01 — **VERIFIED/FROZEN**
- FND-02 incl. AUTH-04 — **VERIFIED/FROZEN**
- FND-08 — **VERIFIED/FROZEN**
- FND-03 durable Runtime Session Lease v1 — **VERIFIED/FROZEN**
- FND-03 machine-license v2 + hardening — **VERIFIED/FROZEN**
- FND-03 Runtime Admission — **VERIFIED/FROZEN**
- FND-03 Shared Runtime Seat Accounting — **PR_READY / UNDER COORDINATOR VALIDATION**
- FND-03 global — **ACTIVE / NOT FROZEN**
- FND-04 Script TAG Reference Resolution — **QUEUED / CONTRACT DEFINED / NOT ACTIVE**
- FC0-A — **BLOCKED**

## Current candidate — PR #331

PR: `#331 — FND-03: enforce shared runtime seat accounting`

- authorized product base: `6f02b9e3c1327b34ff33bab22e90aaf24dfc4628`
- branch: `work/w15-fnd-03-shared-runtime-seat-accounting-v1`
- exact candidate head: `09f81e97369089def481ceb25629779a5aba8aff`
- target: `wave15/corrections-integration`
- state: OPEN / not merged

EliteSCADA CI #1544 / run `35255337014`, latest attempt on the same exact candidate:

- Chromium end-to-end `105348050154` — **SUCCESS**
- Web build `105348051287` — **SUCCESS**
- Backend build, test and smoke `105348092685` — **SUCCESS**

The first attempt had a historical C04 browser-test failure; the coordinator-authorized controlled rerun on the unchanged candidate completed green.

## Current Codex order

Codex must read `docs/WAVE15-MAIN-COORDINATOR-HANDOFF.md` live.

Current order is **final handoff only** for Shared Runtime Seat Accounting:

`CODEX -> MAIN COORDINATOR — FND-03 SHARED RUNTIME SEAT ACCOUNTING HANDOFF`

No further product change, no further rerun, no merge, no new FND-03 slice, no FND-04 and no FC0-A release before Main review.

## Main Coordinator permanent CI authority

Product Owner has permanently authorized the Main Coordinator to operate CI/GitHub Actions for **pre-merge and post-merge validation**, without requesting authorization for every execution.

Allowed, under the canonical handoff guards:

- inspect runs/jobs/steps/logs/artifacts;
- trigger/rerun exact-candidate CI when justified;
- rerun a failed job or failed jobs after diagnosis;
- validate exact merge/integration SHA after merge;
- operate checkpoint/release CI using existing workflows and correct refs.

Required guards:

- diagnose red CI before rerun;
- preserve exact SHA/ref;
- use the smallest sufficient rerun;
- record run/attempt/job;
- no blind rerun loops.

This authority does **not** authorize merge, workflow weakening, test weakening, artificial commits, retarget/rebase tricks, force push or writing to `main`.

**CI green never equals merge authorization.**

## FND-03 remaining after seat accounting

Even after PR #331 is integrated/verified, FND-03 global still requires bounded completion of:

- license inspect/verify/install/replace/remove lifecycle;
- entitlement reevaluation/fencing with Runtime active;
- integration with Installation switching #304;
- final observability/rejection reasons where missing;
- remaining negative/concurrency regressions from #301.

Product Owner decision: product has not launched; no installed customer base requires commercial quota compatibility for ESLIC1. ESLIC2 is the current commercial session-entitlement contract; ESLIC1 does not receive inferred/unlimited remote-session capacity.

## Immediate resume sequence

1. Read `docs/WAVE15-MAIN-COORDINATOR-HANDOFF.md` fully.
2. Revalidate integration HEAD/tree, PR #331 and current Actions live.
3. Distinguish product checkpoint from docs-only coordination head.
4. If Codex final handoff is present, Main independently reviews diff/evidence and decides integration.
5. If integration is authorized/performed, capture merge SHA/parents/tree and validate exact post-merge CI; Main may operate that CI directly under its permanent authority.
6. Promote the bounded slice only after exact integrated evidence.
7. FND-04 remains queued until Main explicitly activates it with an exact product base.

## Permanent guards

- no direct `main` mutation without protected authorization;
- no direct feature-code write to integration;
- no destructive history operation;
- diagnose CI before rerun;
- no contract/security weakening to get green;
- no claim of PASS/VERIFIED/FROZEN without exact evidence;
- canonical orders live in `docs/WAVE15-MAIN-COORDINATOR-HANDOFF.md`.
