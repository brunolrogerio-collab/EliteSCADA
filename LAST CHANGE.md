# LAST CHANGE — EliteSCADA

**Date:** 2026-09-17 BRT  
**Operational state:** **WAVE 15 ACTIVE / FND-03 ACTIVE / RUNTIME ADMISSION VERIFIED+FROZEN / SHARED RUNTIME SEAT ACCOUNTING PR_READY WITH TEST-ONLY ACCEPTANCE CLOSE REQUIRED / FND-04 QUEUED / FC0-A BLOCKED**

> GitHub live is the official project memory. Revalidate refs, exact SHA/tree, branches, PRs, issues and Actions before every material decision.

> Canonical operational handoff: `docs/WAVE15-MAIN-COORDINATOR-HANDOFF.md`.
>
> Generic successor/bootstrap prompt: `docs/NEXT-COORDINATOR-CHAT-HANDOFF.md`.
>
> `docs/CURRENT-COORDINATOR-HANDOFF.md` is only the short bridge.

## Latest verified product checkpoint

Integration branch: `wave15/corrections-integration`

Product checkpoint:

`6f02b9e3c1327b34ff33bab22e90aaf24dfc4628`

Tree:

`53ffaf05ecd492d06ecf7852bd6e77b48431a1eb`

Contains PR #330 / FND-03 Runtime Admission. Post-merge CI #1543 validated Backend, Web and Chromium.

Coordination/documentation commits have advanced integration after this product checkpoint; they do not automatically create a new product base.

## Foundation state

- FND-01 — VERIFIED/FROZEN
- FND-02 incl. AUTH-04 — VERIFIED/FROZEN
- FND-08 — VERIFIED/FROZEN
- FND-03 durable Runtime Session Lease v1 — VERIFIED/FROZEN
- FND-03 machine-license v2 + hardening — VERIFIED/FROZEN
- FND-03 Runtime Admission — VERIFIED/FROZEN
- FND-03 Shared Runtime Seat Accounting — **PR_READY / CORRECTION TEST-ONLY REQUIRED**
- FND-03 global — ACTIVE / NOT FROZEN
- FND-04 Script TAG Reference Resolution — QUEUED / CONTRACT DEFINED / NOT ACTIVE
- FC0-A — BLOCKED

## Current candidate — PR #331

Reviewed candidate:

- base: `6f02b9e3c1327b34ff33bab22e90aaf24dfc4628`
- branch: `work/w15-fnd-03-shared-runtime-seat-accounting-v1`
- head: `09f81e97369089def481ceb25629779a5aba8aff`
- tree: `8a81cb965f754fb4299800333f8a5a5c28d1044e`
- target: `wave15/corrections-integration`
- PR raw state at Main review: OPEN / mergeable / clean / not merged.

Exact-head CI #1544 / run `35255337014` was green after the one controlled rerun:

- Chromium `105348050154` SUCCESS
- Web `105348051287` SUCCESS
- Backend/test/smoke `105348092685` SUCCESS

## Main review result

Architecture is bounded and consistent with the approved shared-seat design. Existing logical lease rows remain the single seat ledger; in-memory uses its existing gate; PostgreSQL uses the existing advisory-lock transaction; no second quota/licensing/Authority/session authority was introduced.

The final Codex handoff, however, explicitly marked this required acceptance as PENDING:

`Explicit named Web-vs-EliteGO mixed-client test and high-concurrency distinct mixed identities`.

Because required unexecuted evidence remains PENDING, Main did not authorize integration yet.

## Current Codex order

Canonical order: `ORDER CODEX-331-TEST-CLOSE-03` in `docs/WAVE15-MAIN-COORDINATOR-HANDOFF.md`.

Codex must perform **tests-only** closure:

- explicit mixed `web-*` + `elitego-*` logical identities proving same shared pools;
- high-concurrency distinct mixed identities proving no per-class/total oversubscription and no duplicate logical leases.

Existing PostgreSQL two-store last-seat race remains valid cross-instance evidence.

No production code change is authorized unless a newly required test exposes a real defect. If that occurs, Codex must STOP before product correction and return the blocked handoff defined in the canonical document.

After tests-only change, normal PR CI must validate the new exact head. If green, Codex returns:

`CODEX -> MAIN COORDINATOR — FND-03 SHARED RUNTIME SEAT ACCOUNTING ACCEPTANCE-CLOSE HANDOFF`

and STOPs for Main integration decision.

## Permanent Main CI authority

Product Owner permanently authorizes Main Coordinator to operate pre-merge and post-merge CI under the guardrails in the canonical handoff. CI green never equals merge authorization.

## Immediate resume sequence

1. Read canonical handoff fully.
2. Revalidate PR #331 new head/tree and Actions.
3. If acceptance-close handoff exists, confirm only tests changed versus `09f81e973...`.
4. Verify former PENDING criterion is PASS with concrete evidence.
5. Verify exact-head CI.
6. Only then decide integration.
7. After any merge, capture merge SHA/parents/tree and validate exact post-merge CI.
8. FND-04 remains WAIT until explicit Main activation.

## Permanent guards

- no direct `main` mutation without protected authorization;
- no direct feature-code write to integration;
- no destructive history operation;
- diagnose CI before rerun;
- no weakening of contracts/security/tests to get green;
- no PASS/VERIFIED/FROZEN without exact evidence;
- canonical orders live in `docs/WAVE15-MAIN-COORDINATOR-HANDOFF.md`.
