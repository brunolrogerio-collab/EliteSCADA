# Current Coordinator Handoff — Wave 15

> **GitHub live is the sole operational authority.** This file is a concise pointer. The detailed persistent state is in `docs/WAVE15-MAIN-COORDINATOR-HANDOFF.md`. Revalidate live refs, latest issue comments, PR heads/trees and exact-head Actions evidence before acting.

> **2026-09-15 AUTH-04 override:** the historical snapshot below is superseded for AUTH-04.
> Frozen baseline: `wave15/corrections-integration@e7b9b83dbc71764e3eee5b5afe2daf07c45a9377`.
> Integrated checkpoint: `wave15/corrections-integration@b534f71ec45f1f93b15a93f8626cfcfe652b56ca` (tree `0c1e30885b7d341ad17fdc1bf200f893df315757`).
> Exact post-merge CI: [run 35036947168](https://github.com/brunolrogerio-collab/EliteSCADA/actions/runs/35036947168) / #1527 passed backend build/test/smoke, Web build and Chromium E2E on the merge SHA.
> AUTH-04 is **INTEGRATED / VERIFIED / FROZEN**; as its remaining gate, FND-02 is **VERIFIED / FROZEN**.
> The separate privileged Codex/Visual Studio Work chat owns heavy implementation; Main only coordinates,
> reviews evidence and records the authoritative handoff in #302.
> FC0-A remains blocked pending FND-03, FND-04 and FND-06 VERIFIED+FROZEN evidence on one checkpoint.

## Current topology snapshot

- Repository: `brunolrogerio-collab/EliteSCADA`
- Wave: **15 — complete product delivery**
- Integration: `wave15/corrections-integration`
- Snapshot integration SHA: `bc68bf450f6efd42b90898ad0656bea9b7543f57`
- Global status: #297
- Foundation/dependency/parallel-DEV/CI orchestration: #305
- Active foundation issue: #302 — Security Authority / FND-02
- Active PR: #314 — `W15 AUTH-03: persist canonical Security Authority`
- Snapshot AUTH-03 head: `f8d56f8cb87ba0b51d33c58c7597a1466b4bd9c1`
- AUTH-04: queued/not active
- Wave13 #205/#207: paused

## State machine

`NOT_STARTED -> ACTIVE -> PR_READY -> INTEGRATED -> VERIFIED -> FROZEN`

A shared contract may be consumed downstream only after the required slice is `VERIFIED + FROZEN`.

## Frozen foundation snapshot

- FND-01 Working/lifecycle/bootstrap — VERIFIED/FROZEN.
- FND-08 common timing contract — VERIFIED/FROZEN.
- FND-02 AUTH-01 capability vocabulary/enforcement — VERIFIED/FROZEN.
- FND-02 AUTH-02 stable hierarchy/scope identities — VERIFIED/FROZEN.
- FND-02 overall — NOT FROZEN while AUTH-03/remaining required slices remain.

## Active mission

AUTH-03 establishes the single durable canonical Security Authority owner: PostgreSQL version/CAS, deterministic bootstrap/migration, protected administration, self-lockout/orphan protection, Authority backup v2, Engineering/package ownership transition, `.escadapkg` v3 / Engineering schema19 and stable audit IDs without secrets.

Earlier contract blockers A/B/C were corrected. Revalidate any current/new head delta before acceptance.

## Current CI evidence

Run `34766682415` on snapshot head `f8d56f8...`:

- Web build PASS;
- backend build PASS;
- full .NET test stage PASS;
- real PostgreSQL Authority persistence/CAS PASS;
- Runtime smoke FAIL;
- rerun reproduced the same failure.

The actual failure occurs before lifecycle/security assertions. Runtime diagnostics show historian `writtenSamples=7` and seven TAGs; the smoke then asks `/api/history/{id}` for `Demo.Tank01.Level`, receives no sample and fails `assert len(history) >= 1`.

Immediate job is to diagnose that narrow historian-smoke discrepancy without weakening Historian/AUTH contracts. AUTH-03 is not yet merge/freeze-ready merely because its PostgreSQL test passed.

## Main / Work / DEV coordination

- Main owns dependency graph, exact base SHA, mission activation, review, merge order, CI disposition and freeze records.
- Work owns one active Foundation/high-risk implementation mission at a time and continues independent work while Actions runs.
- CI unavailable locally may be delegated to Actions; unexecuted required validation remains PENDING.
- Parallel feature DEVs remain blocked until FC0 gates/frozen shared contracts permit them.
- Every DEV works on an isolated branch/PR to `wave15/corrections-integration`; no DEV merges its own PR or silently redesigns frozen shared contracts.

## Actions capability note

All current workflows support `workflow_dispatch`. A ChatGPT connector may still lack the action to create a new manual dispatch. Existing run/job rerun can be separately available. New dispatch may be delegated to Work/Codex/CLI `gh workflow run` when necessary. Never manufacture an empty commit or retarget a PR simply to wake CI.

INFRA-CI-01 remains the intended Wave 15 fix for profile-aware T1/T2/T3/T4 validation. Do not attach every old heavy workflow automatically to every Wave 15 PR.

## Immediate resume

1. Read `docs/WAVE15-MAIN-COORDINATOR-HANDOFF.md` completely.
2. Revalidate integration and PR #314 exact heads.
3. Read newest #302 and #314 comments.
4. Continue narrow historian-smoke diagnosis on exact AUTH-03 candidate.
5. Review and validate any correction at its exact resulting SHA.
6. Integrate/freeze AUTH-03 only when evidence is sufficient.
7. Do not activate AUTH-04 prematurely.
8. Continue remaining FC0-A Foundation work + INFRA-CI-01 before releasing parallel DEVs.

Permanent guards: no direct `main`, no destructive history operations, no blind reruns, no weakening tests/security/lifecycle/package/Runtime/Historian/Driver contracts, no EEE-only workaround for generic defects, and Runtime/Active remains independent of `.escadalib`.

For Product Owner control, coordinator messages end with America/Sao_Paulo local time as `Hora: HH:MM`.
