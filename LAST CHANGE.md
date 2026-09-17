# LAST CHANGE — EliteSCADA

**Date:** 2026-09-17 BRT  
**Operational state:** **WAVE 15 ACTIVE / FND-03 SHARED RUNTIME SEAT ACCOUNTING VERIFIED+FROZEN / LICENSE LIFECYCLE+RUNTIME FENCING ACTIVE / FND-04 WAIT / FC0-A BLOCKED**

> GitHub live é a memória oficial. Revalidar refs, SHA/tree, PRs, issues e Actions antes de decisão material.
>
> Handoff operacional canônico: `docs/WAVE15-MAIN-COORDINATOR-HANDOFF.md`.
>
> Prompt genérico de sucessão: `docs/NEXT-COORDINATOR-CHAT-HANDOFF.md`.
>
> Ponte curta: `docs/CURRENT-COORDINATOR-HANDOFF.md`.

## Latest verified product checkpoint

`wave15/corrections-integration@a7067ac99f9f88fcd17f740b915d8c4f57c556fc`

tree `eed22a377fea2778d3e78143d706e4de0ef9ce38`.

Esse checkpoint é o merge do PR #331 — FND-03 Shared Runtime Seat Accounting.

Post-merge EliteSCADA CI #1546 / run `35269829080` no exact merge SHA:

- Backend `105366045111` — SUCCESS
- Web `105366045298` — SUCCESS
- Chromium `105366583742` — SUCCESS

Portanto Shared Runtime Seat Accounting está **VERIFIED/FROZEN**.

Commits posteriores somente documentais de coordenação não mudam automaticamente o product checkpoint.

## Foundation state

- FND-01 — VERIFIED/FROZEN
- FND-02 incl. AUTH-04 — VERIFIED/FROZEN
- FND-08 — VERIFIED/FROZEN
- FND-03 durable Runtime Session Lease v1 — VERIFIED/FROZEN
- FND-03 machine-license v2 + hardening — VERIFIED/FROZEN
- FND-03 Runtime Admission — VERIFIED/FROZEN
- FND-03 Shared Runtime Seat Accounting — VERIFIED/FROZEN
- FND-03 License Lifecycle + Runtime Authority Re-evaluation/Fencing v1 — **ACTIVE**
- FND-03 global — ACTIVE / NOT FROZEN
- FND-04 Script TAG Reference Resolution — QUEUED / CONTRACT DEFINED / WAIT
- FC0-A — BLOCKED

## Current Codex mission

Canonical work package: `docs/WAVE15-MAIN-COORDINATOR-HANDOFF.md`.

Mission:

`FND-03 License Lifecycle + Runtime Authority Re-evaluation/Fencing v1`

Exact authorized product base:

`a7067ac99f9f88fcd17f740b915d8c4f57c556fc`

tree `eed22a377fea2778d3e78143d706e4de0ef9ce38`.

Work branch:

`work/w15-fnd-03-license-lifecycle-fencing-v1`

Target:

`wave15/corrections-integration`

The Main Coordinator already performed the architectural/source audit. Codex must implement the bounded package rather than repeat an open-ended audit.

Binding objectives include:

- candidate license verification without mutation using canonical verifier;
- transactional valid install/replace; invalid candidate preserves current valid license;
- deliberate remove -> Demo, not Invalid;
- successful license-authority changes fence all existing remote Runtime logical leases;
- active local Runtime is re-evaluated/fenced against new authority without a temporary entitlement loophole;
- Demo timing starts from successful transition when applicable;
- install/replace/remove require canonical EngineeringModify minimum;
- audit evidence excludes raw license/signing material;
- concurrency and stale-client regressions required;
- no second licensing/session/quota/Authority authority.

Product-base rule: product work is anchored to exact verified checkpoint `a7067ac9...`; moving documentation HEADs are not product bases. Any intervening product/infra delta requires `BLOCKED-BASE-DIVERGENCE` before implementation.

## FND-04

FND-04 DEV and AUD remain `WAIT`. They begin only when Main changes their `CURRENT ORDER` to ACTIVE with exact product base/candidate and acceptance package.

## Main permanent CI authority

Product Owner permanently authorized Main to inspect/trigger/rerun pre-merge and post-merge CI under exact-SHA and diagnosis guards. CI authority remains separate from merge authority. `main` remains protected.

## FND-03 remaining

After lifecycle/fencing is integrated and verified, Main re-evaluates #301 for global close or one final bounded closeout. Expected review areas: final observability/rejection reasons/counters, heartbeat/reuse residuals, #304 contract sufficiency, and remaining negative/concurrency proof.

FND-03 global becomes VERIFIED/FROZEN only when every binding #301 criterion is proven on one exact integration checkpoint.

Permanent guards: no direct feature write to integration; no direct `main` mutation without protected authorization; no destructive history operation; diagnose CI before rerun; no PASS/VERIFIED/FROZEN without exact evidence; no license/session/secrets in `.escadapkg`.
