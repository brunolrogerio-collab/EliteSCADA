# LAST CHANGE — EliteSCADA

**Date:** 2026-09-17 BRT  
**Operational state:** **WAVE 15 ACTIVE / FND-03 SHARED SEAT ACCOUNTING VERIFIED+FROZEN / LIFECYCLE+FENCING ARCHITECTURE CORRECTION ACTIVE / CODEX WAIT / FND-04 WAIT / FC0-A BLOCKED**

> GitHub live is the official memory. Revalidate refs, SHA/tree, PRs, issues and Actions before any material decision.
>
> Canonical operational handoff: `docs/WAVE15-MAIN-COORDINATOR-HANDOFF.md`.
>
> Generic succession prompt: `docs/NEXT-COORDINATOR-CHAT-HANDOFF.md`.
>
> Short bridge: `docs/CURRENT-COORDINATOR-HANDOFF.md`.

## Latest verified product checkpoint

`wave15/corrections-integration@a7067ac99f9f88fcd17f740b915d8c4f57c556fc`

tree `eed22a377fea2778d3e78143d706e4de0ef9ce38`.

This checkpoint is PR #331 — FND-03 Shared Runtime Seat Accounting — merged and verified by exact post-merge EliteSCADA CI #1546 / run `35269829080`:

- Backend `105366045111` — SUCCESS
- Web `105366045298` — SUCCESS
- Chromium `105366583742` — SUCCESS

Shared Runtime Seat Accounting is therefore VERIFIED/FROZEN. Later coordination-only documentation commits do not change the product checkpoint.

## Foundation state

- FND-01 — VERIFIED/FROZEN
- FND-02 incl. AUTH-04 — VERIFIED/FROZEN
- FND-08 — VERIFIED/FROZEN
- FND-03 durable Runtime Session Lease v1 — VERIFIED/FROZEN
- FND-03 machine-license v2 + hardening — VERIFIED/FROZEN
- FND-03 Runtime Admission — VERIFIED/FROZEN
- FND-03 Shared Runtime Seat Accounting — VERIFIED/FROZEN
- FND-03 License Lifecycle + Runtime Authority Re-evaluation/Fencing — **ARCHITECTURE REVIEW / CORRECTION REQUIRED / IMPLEMENTATION NOT AUTHORIZED**
- FND-03 global — ACTIVE / NOT FROZEN
- FND-04 Script TAG Reference Resolution — QUEUED / CONTRACT DEFINED / WAIT
- FC0-A — BLOCKED

## Current execution strategy

CODEX is `WAIT` to preserve scarce usage. It must not implement, commit, open a PR or run implementation CI unless Main later gives a bounded ACTIVE order.

A normal FND-03 DEV/ARCH lane is ACTIVE in `ARCH_ONLY_CORRECTION`. Its first architecture handoff was reviewed against exact product checkpoint `a7067ac9...`. Main found the overall direction useful but blocked implementation pending six corrections:

1. use exact repository paths/symbols at the checkpoint;
2. candidate verification returns canonical `LicenseVerificationResult` and reuses the existing verifier;
3. lifecycle uses durable `transition_pending` + short PostgreSQL transactions under existing transaction-scoped `LeaseMutationAdvisoryLock`, rather than pretending one DB transaction spans filesystem/Runtime work;
4. shared-ledger multi-instance support in this slice assumes the same installation/machine-license authority; cross-machine HA authority convergence remains out of scope;
5. install/replace/remove require `EngineeringModify` but do not depend on current Application Engineering Lock;
6. Demo authority transition/restart must be mapped against the real `PersistedRuntimeRecoveryService` so the same Active Runtime cannot obtain a permissive timer reset.

Required next evidence in #301 begins:

`FND-03 DEV -> MAIN COORDINATOR — LICENSE LIFECYCLE ARCHITECTURE AMENDMENT`

Only after Main reviews and freezes that amendment may an implementation order be issued to a normal DEV or, if materially necessary, Codex.

## FND-03 acceptance remains binding

The lifecycle/fencing implementation must ultimately prove the 18 acceptance criteria recorded in the canonical handoff: non-mutating valid/invalid candidate verification, safe install/replace/remove, no-op invalid replacement, Demo semantics, package boundary, complete old-lease fencing, admission-vs-downgrade race safety, new-quota enforcement, local Runtime re-evaluation, EngineeringModify, safe audit, regressions, package exclusion and exact-head CI.

## FND-04

FND-04 DEV and AUD remain `WAIT`. They begin only when Main changes their canonical CURRENT ORDER to ACTIVE with exact product base/candidate and acceptance package.

## Main permanent CI authority

Product Owner permanently authorized Main to inspect/trigger/rerun exact-SHA pre-merge and post-merge CI under diagnosis/minimal-rerun guards. CI authority and merge authority are separate. `main` remains protected.

## Permanent guards

No direct feature write to integration; no direct `main` mutation without protected authorization; no destructive history operation; diagnose red CI before rerun; required unexecuted test is PENDING; no PASS/VERIFIED/FROZEN without exact evidence; no license/session/secrets in `.escadapkg`; agent evidence comments are append-only and Main reviews by new comment plus canonical-order update.
