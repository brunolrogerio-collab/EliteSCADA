# Current Coordinator Handoff — Wave 15

> **GitHub live is the sole operational authority.** This file is the concise current pointer. Revalidate exact refs, latest issue comments, PR heads/trees and Actions before acting.

**Status date:** 2026-09-16 BRT  
**Current integration checkpoint:** `wave15/corrections-integration@456c66f4966ab5302831f642a39690ae3a3402a5`  
**Current tree:** `f42d442933ded9bcf4290ae437193f2d1bb3d492`

## Current state

- Wave 15 complete-product delivery remains active.
- FND-01 — VERIFIED/FROZEN.
- FND-02 Security Authority, including AUTH-04 — VERIFIED/FROZEN.
- FND-08 common timing — VERIFIED/FROZEN.
- FND-03 Runtime Session Lease / Licensing v2 — **ACTIVE / NOT FROZEN**.
- FND-03 Slice 1 durable Runtime Session Leases — **INTEGRATED / VERIFIED** at `456c66f...`.
- Exact post-merge run `35110143733` is green for backend build/test/smoke, Web build and Chromium end-to-end.
- FC0-A remains **BLOCKED**.
- Parallel feature DEV lanes remain blocked.

## Active Codex mission

The latest Main Coordinator order is #301 comment `5699620231`:

**FND-03 machine-license v2 schema/codec** from exact base `456c66f4966ab5302831f642a39690ae3a3402a5`.

Authorized branch when work is published:

`work/w15-fnd-03-machine-license-v2` -> `wave15/corrections-integration`

Scope is deliberately narrow:

- retain signed machine-bound ESLIC1 read/validation compatibility;
- add the signed machine-bound v2/ESLIC2 representation with explicit `viewOnlySeats`, `interactiveSeats`, `haRuntime`;
- preserve the existing signature/hardware-binding/expiry trust path;
- do not infer Interactive entitlement from ESLIC1;
- no admission enforcement, active quotas, requested/granted-class policy, license lifecycle UX, License Generator UX, Installation UX, EliteGO UX or HA election/fencing in this slice.

Required proof: v1 compatibility, v2 valid decode/verification, tamper/wrong-key/wrong-machine/expiry rejection, invalid seat values, signed-field coverage and `.escadapkg` boundary regression.

### Interaction-limit resume condition

The Product Owner reports that Codex **started this slice and was interrupted only by the interaction limit**. There is not yet a completed Codex handoff.

GitHub currently exposes no `work/w15-fnd-03-machine-license-v2` branch/PR. Therefore the next Codex/Work continuation must **inspect the existing local/worktree/session state first**. Do not start a parallel reimplementation merely because the work is not yet pushed.

The existing `work/w15-fnd-03-runtime-session-lease` branch belongs to the already integrated Slice 1 and must not be mistaken for the current license-v2 branch.

## New queued FND-04 requirement

#305 comment `5701881550` adds a binding FND-04 exit criterion for W15-P1-05 readable Python TAG references.

Before FND-04 can freeze for DEV-SCRIPT-ENGINEERING:

- generated literal `tag_read` / `tag_write` should use readable canonical TAG paths;
- read and write must share one stable resolution semantic;
- `TagId` remains the actual internal identity;
- enough stable binding evidence must exist to detect path rename/reuse identity drift;
- missing/ambiguous/stale references fail closed;
- rename/path reuse must never silently retarget a script to another TAG.

This is **queued behind the active FND-03 work**. Do not interrupt the current license-v2 slice to implement it.

## Immediate resume

1. Read `LAST CHANGE.md`.
2. Revalidate integration head/tree and newest #301/#305 comments.
3. Resume the existing Codex license-v2 work by inspecting the prior local/worktree/session state.
4. Continue only the authorized schema/codec slice.
5. When ready, publish one reviewable PR with exact-head validation and handoff prefix `CODEX -> MAIN COORDINATOR — FND-03 LICENSE V2 SCHEMA HANDOFF`.
6. Do not self-freeze FND-03 or release FC0-A.
7. After FND-03 progresses, preserve the queued FND-04 readable-reference freeze criterion before DEV-SCRIPT-ENGINEERING is released.

## Coordination pointers

- detailed current handoff: `docs/WAVE15-MAIN-COORDINATOR-HANDOFF-CURRENT.md`
- prior detailed handoff: `docs/WAVE15-MAIN-COORDINATOR-HANDOFF.md` — **historical snapshot only; do not use its AUTH-03/AUTH-04 active-state text as current**
- execution/dependency ledger: #305
- FND-03/licensing ledger: #301
- global Wave 15 ledger: #297
- generic rotation protocol: `docs/NEXT-COORDINATOR-CHAT-HANDOFF.md`

Permanent guards: no direct `main`, no destructive history operations, no blind reruns, no weakened tests/security/lifecycle/licensing/Runtime/Historian/Driver contracts, no downstream silent redesign of frozen authorities, and no treatment of unpushed local work as repository-verified evidence.

For Product Owner control, coordinator messages end with America/Sao_Paulo time as `Hora: HH:MM`.
