# LAST CHANGE — EliteSCADA

**Date:** 2026-09-15 BRT
**Operational override:** **AUTH-04 PR OPEN / REBASED ON CURRENT INTEGRATION / EXACT-HEAD CI PENDING / NOT INTEGRATED / NOT VERIFIED / NOT FROZEN**

This entry supersedes the historical snapshot below for AUTH-04; it does not rewrite its evidence.

- Frozen AUTH-03 / FND-02 baseline: `wave15/corrections-integration@e7b9b83dbc71764e3eee5b5afe2daf07c45a9377`.
- Active AUTH-04 PR branch: `work/w15-auth-04-authority-detach`, rebased onto `wave15/corrections-integration@b2eec2dc95da905dab9908246e30bf0f96d05aa3` to preserve the global collaboration protocol.
- The prior candidate CI must not be reused after this rebase; exact-head CI is pending for the rebased branch.
- Delivered boundary: persistent Authority binding/session epoch, serialized atomic detach/switch,
  fail-closed cross-node invalidation and secret-safe audit. Policy, local identities and credentials
  remain Authority-owned; `.escadapkg` remains prohibited from carrying them.
- Role boundary: the separate privileged Codex/Visual Studio Work chat owns heavy implementation.
  Main Coordinator validates evidence, records handoffs and enforces the dependency graph.
- Next safe action: read #297, then #302; revalidate exact live branch/CI and review the Work handoff.

---

# Historical snapshot — 2026-09-13

**Date:** 2026-09-13 BRT  
**Operational state:** **WAVE 15 ACTIVE / FOUNDATION-FIRST / AUTH-01+AUTH-02 VERIFIED+FROZEN / AUTH-03 ACTIVE IN PR #314 / INFRA-CI-01 REQUIRED BEFORE FC0-A PARALLEL DEVS / WAVE13 PAUSED**

> **GitHub live is the official and sole project memory.** Revalidate refs, PR/issue state, exact SHA/tree and exact-head Actions evidence before every decision, diagnosis, code/documentation write, rerun or merge. If this file differs from GitHub live, GitHub wins.

## Current execution boundary

- Repository: `brunolrogerio-collab/EliteSCADA`
- Wave: **15 — complete product delivery**
- Integration branch: `wave15/corrections-integration`
- Snapshot integration SHA: `bc68bf450f6efd42b90898ad0656bea9b7543f57`
- Global Wave 15 issue: #297
- Foundation/dependency/CI orchestration: #305
- Active Authority foundation issue: #302
- Active implementation PR: #314 — `W15 AUTH-03: persist canonical Security Authority`
- PR #314 base: `wave15/corrections-integration@bc68bf450f6efd42b90898ad0656bea9b7543f57`
- PR #314 snapshot head: `f8d56f8cb87ba0b51d33c58c7597a1466b4bd9c1`
- AUTH-04: queued/not active; blocked by AUTH-03 plus installation/FND-07 dependencies
- Wave13 #205/#207: paused

This documentation update lives on a coordination branch above the integration snapshot. Never use a documentation commit as product validation evidence.

## Current frozen boundaries

### FND-01 — VERIFIED/FROZEN

Persisted deterministic Working/bootstrap/lifecycle selection and fail-closed recovery boundary.

### FND-08 common timing — VERIFIED/FROZEN

`elitescada.timing-policy/v1`: no global timeout inflation, bounded retry only where safe, no blind write retry, timeout may be unknown outcome and stale responses cannot overwrite newer truth.

### FND-02 / AUTH-01 — VERIFIED/FROZEN

Versioned stable capability vocabulary; old ordinals preserved; `EngineeringView=11`, HA Observe/Transfer/Admin = 12/13/14; role names never grant privilege; TAG authorization is capability-first; `CommandExecute` remains distinct from `ProcessValueWrite`.

### FND-02 / AUTH-02 — VERIFIED/FROZEN

Stable Guid hierarchy/scope identities and explicit ancestry; malformed/stale/ambiguous scopes fail closed; TAG/screen/command/equipment bindings use stable identity; display rename does not alter authority.

## Current ACTIVE mission — AUTH-03

PR #314 owns durable canonical Security Authority persistence/admin/portable backup v2 and the `.escadapkg` v3 / Engineering schema19 ownership transition.

Earlier review blockers around persisted bootstrap migration, package Authority reference/versioning and strict wire capability vocabulary were corrected before the latest CI-specific commits. Any new head delta must still be reviewed before acceptance.

### Current exact-head evidence

GitHub Actions run `34766682415` on exact PR head `f8d56f8...`:

- Web build — PASS;
- backend restore/build — PASS;
- full .NET test stage — PASS;
- real PostgreSQL Authority persistence/CAS test — PASS;
- backend Runtime smoke — **FAIL**;
- rerun of the existing backend job reproduced the same smoke failure.

The reproduced failure happens immediately after Runtime diagnostics report TimescaleDB `writtenSamples=7` and `Runtime exposed 7 TAGs`. The smoke resolves `Demo.Tank01.Level`, calls `/api/history/{id}?limit=100` and fails `assert len(history) >= 1`. Later historical-query, Security Role and lifecycle assertions are never reached.

Therefore the current blocker is a **narrow direct-Historian smoke diagnosis**, not an AUTH PostgreSQL persistence failure and not the previously suspected lifecycle `ChangesPending` transition. Do not weaken Historian or Authority contracts to force green.

## Immediate next safe action

1. Revalidate `wave15/corrections-integration`, PR #314 head and newest #302/#314 comments.
2. Diagnose the reproducible direct `/api/history/{id}` empty-result smoke on the exact current AUTH-03 head.
3. If it is a stale/non-causal CI fixture, apply only the smallest evidence-backed correction and validate the new exact head.
4. Review the final AUTH-03 delta and required evidence.
5. Only then integrate PR #314 into `wave15/corrections-integration`, verify merge parent/tree and record `INTEGRATED -> VERIFIED -> FROZEN` in #302/#297.
6. Do **not** activate AUTH-04 early.
7. Continue FC0-A foundations plus INFRA-CI-01 before releasing parallel feature DEVs.

## Coordination / CI rule

All current `.github/workflows` support `workflow_dispatch`, but a given ChatGPT connector may not expose creation of a new manual dispatch. That is a tool limitation, not a repository limitation. Existing runs/jobs may be rerunnable through the connector. For a new dispatch, use Work/Codex/CLI `gh workflow run` when available. Never create an empty commit, retarget a PR to `main` or mutate workflow code merely to make CI start.

Most automatic triggers still reflect `main` or Wave 14 branches. Do not spray `wave15/corrections-integration` into every old workflow. Implement INFRA-CI-01 with profile-aware T1/T2/T3/T4 gates.

## Management model

- Main Coordinator owns dependency graph, exact bases, review, integration, freezes and escalation.
- Separate Work chat owns one active Foundation/high-risk implementation mission at a time.
- Work continues independent coding while Actions runs; CI is parallel evidence.
- Required but unexecuted validation is `PENDING`, never `PASS`.
- Parallel feature DEVs remain blocked until required shared contracts are `VERIFIED + FROZEN` and FC0-A is satisfied.
- State machine: `NOT_STARTED -> ACTIVE -> PR_READY -> INTEGRATED -> VERIFIED -> FROZEN`.
- `siga` means continue safe coordination autonomously; it never authorizes protected `main` merge.

## Canonical current handoffs

- full persistent Wave 15 coordinator state: `docs/WAVE15-MAIN-COORDINATOR-HANDOFF.md`
- concise live pointer: `docs/CURRENT-COORDINATOR-HANDOFF.md`
- copy-ready next-chat prompt: `docs/NEXT-COORDINATOR-CHAT-HANDOFF.md`

Historical Wave 14 documents remain valid as historical evidence only. They are no longer the active coordination authority.
