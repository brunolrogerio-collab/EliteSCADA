# LAST CHANGE — EliteSCADA

**Date:** 2026-09-16 BRT  
**Operational state:** **WAVE 15 ACTIVE / FND-02 VERIFIED+FROZEN / FND-03 ACTIVE / SLICE 1 INTEGRATED+VERIFIED / MACHINE-LICENSE V2 SLICE AUTHORIZED+STARTED IN CODEX SESSION / NO PUBLISHED HANDOFF YET / FC0-A BLOCKED**

> **GitHub live is the official project memory.** Revalidate refs, exact SHA/tree, branches, PRs, issues and Actions before every material decision. Chat-local work that has not been committed/pushed is not repository evidence.

> **Operational relay rule:** `docs/CURRENT-COORDINATOR-HANDOFF.md` is the single current Main Coordinator <-> Codex/Work handoff/combinator. Do not create a competing `*-CURRENT*` handoff file for the same live state.

## Latest verified product-code checkpoint

- Repository: `brunolrogerio-collab/EliteSCADA`
- Integration branch: `wave15/corrections-integration`
- Latest verified product-code checkpoint: `456c66f4966ab5302831f642a39690ae3a3402a5`
- Tree at that checkpoint: `f42d442933ded9bcf4290ae437193f2d1bb3d492`
- Merge content: **FND-03 Slice 1 — durable Runtime Session Leases**
- Exact post-merge CI: Actions run `35110143733` / run #1531 — backend build/test/smoke PASS, Web build PASS, Chromium end-to-end PASS.

The live integration branch may be ahead of `456c66f...` because of coordination/documentation-only merges. Do not treat a later docs-only SHA as a newer product-code validation checkpoint, and do not assume `456c66f...` is still the live branch HEAD without revalidation.

## Foundation state

- FND-01 Working/lifecycle/bootstrap — **VERIFIED/FROZEN**.
- FND-02 Security Authority, including AUTH-04 — **VERIFIED/FROZEN** at the predecessor checkpoint and consumed by FND-03.
- FND-08 common timing — **VERIFIED/FROZEN**.
- FND-03 Runtime Session Lease / Licensing v2 — **ACTIVE / NOT FROZEN**.
- FND-03 Slice 1 durable Runtime Session Lease persistence/identity — **INTEGRATED / VERIFIED** at `456c66f...`.
- FND-04 remains a required FC0-A foundation and now has the binding Script TAG reference-resolution exit criterion recorded in #305 comment `5701881550`.
- FND-05/FND-07 remain later FC0-B foundations.
- Parallel feature DEVs remain blocked until FC0-A is explicitly recorded.

## Current Codex mission — FND-03 machine-license v2 schema/codec

The latest Main Coordinator order to Codex is #301 comment `5699620231` / #305 summary: implement the **machine-license v2 schema/codec** from exact product base `456c66f4966ab5302831f642a39690ae3a3402a5`.

Authorized target branch: `work/w15-fnd-03-machine-license-v2` -> `wave15/corrections-integration`.

Required slice:

- preserve exact read/validation compatibility for existing signed machine-bound `ESLIC1` licenses;
- add the signed machine-bound v2 representation (referred to by the coordinator as `ESLIC2`) with explicit `viewOnlySeats`, `interactiveSeats` and `haRuntime` entitlements;
- preserve existing signature algorithm, hardware fingerprint verification, expiry and Demo behavior unless a narrow version adapter is required;
- do **not** infer an Interactive entitlement from ESLIC1;
- keep this slice schema/codec-only: no Runtime admission enforcement, active quota accounting, requested->granted class calculation, install/replace/remove flow, License Generator UX, Installation UX, EliteGO UX or HA election/fencing.

Required deterministic proof includes v1 compatibility, valid v2 verification/decode, tamper/wrong-key/wrong-machine/expiry rejection, malformed seat values, signature coverage of v2 fields and package-boundary regression.

### Important resume note

The Product Owner reports that Codex **started this authorized slice and then stopped only because the interaction limit was reached**. No completed handoff was produced.

GitHub currently shows **no published branch `work/w15-fnd-03-machine-license-v2`, no PR and no new committed evidence for this slice**. Therefore a resumed Codex/Work session must first inspect its existing local/worktree state before recreating work. Do not assume the absence of a GitHub branch means no local progress exists, and do not duplicate implementation blindly.

The older branch `work/w15-fnd-03-runtime-session-lease` points to the already-consumed Slice 1 work and is **not** the authorized branch for the current license-v2 slice.

## Queued Foundation delta — FND-04 Script TAG references

The readable-Python requirement for W15-P1-05 is not UI-only. Before FND-04 may freeze for DEV-SCRIPT-ENGINEERING, the shared runtime/script contract must ensure:

- normal generated Python uses canonical readable TAG paths rather than GUIDs;
- `tag_read` and `tag_write` share one resolver semantic;
- stable `TagId` remains the internal identity authority;
- source/binding metadata detects rename/path-reuse identity drift;
- missing/ambiguous/stale references fail closed;
- rename or reuse of an old path can never silently retarget a script to another TAG.

Detailed binding criteria and regressions are in #305 comment `5701881550`. This work is **queued behind the currently active FND-03 slice** and must not interrupt it.

## Immediate resume sequence

1. Revalidate the live `wave15/corrections-integration` HEAD/tree and distinguish docs-only advances from product-code changes.
2. Read `docs/CURRENT-COORDINATOR-HANDOFF.md` as the single current Main <-> Codex relay, then read latest #301 and #305 comments.
3. Resume the existing Codex session/worktree for the authorized machine-license-v2 schema/codec slice; inspect local changes before creating/recreating a branch.
4. Publish only when the slice has a reviewable branch/PR and exact-head evidence; handoff prefix must be `CODEX -> MAIN COORDINATOR — FND-03 LICENSE V2 SCHEMA HANDOFF`.
5. Do not self-freeze FND-03 and do not release FC0-A.
6. After this slice is reviewed/integrated/verified, continue the remaining FND-03 slices under explicit coordinator authorization.
7. Keep the new FND-04 Script TAG reference-resolution criterion queued for FND-04 before FC0-A freeze.

## Current documentation pointers

- **single current Main Coordinator <-> Codex/Work handoff/combinator:** `docs/CURRENT-COORDINATOR-HANDOFF.md`
- historical prior Wave 15 handoff: `docs/WAVE15-MAIN-COORDINATOR-HANDOFF.md`
- generic next-Main protocol: `docs/NEXT-COORDINATOR-CHAT-HANDOFF.md`
- sequencing: `docs/ROADMAP.md`

Permanent guards remain: no direct `main` mutation, no direct feature write to integration, no destructive history operation, no blind CI rerun, no weakening Security/Authority/Licensing/lifecycle/Runtime/Historian/Driver contracts, no EEE-only workaround for generic defects, and no claim of PASS/FROZEN without exact evidence.