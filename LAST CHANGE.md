# LAST CHANGE — EliteSCADA

**Date:** 2026-09-18 BRT  
**Operational state:** **WAVE 15 ACTIVE / FND-03 LIFECYCLE PHASE A INTEGRATED / POST-MERGE CI #1551 RUNNING / NOT YET VERIFIED / CODEX WAIT / FND-04 WAIT / FC0-A BLOCKED**

> GitHub live is the official memory.
>
> Canonical operational handoff: `docs/WAVE15-MAIN-COORDINATOR-HANDOFF.md`.

## Latest integrated product checkpoint

PR #332 — FND-03 License Lifecycle / Runtime Authority Fencing Phase A — merged.

Exact product merge:

`20b934f23d8798ffb65cca203b62f8b5c3d8f111`

tree:

`4e227fdde1d8475c23852e142c51946c7a2e1859`

merge parents:
- integration coordination head `a3555b3422e0f86ee89d21588e550a33931e71b2`
- reviewed Phase A candidate `a07568ea072bf6a095f800dc5443b76b6a6d3a94`

The candidate was merged only after exact-head CI #1550 had Backend/Web green and the single controlled Chromium rerun `105460986304` green.

## Phase A content

Integrated Phase A includes:
- canonical non-mutating `VerifyCandidate`;
- Runtime AuthorityRevision / durable transition state;
- PostgreSQL migration `023_runtime_session_authority_fencing_v1`;
- lease AuthorityRevision stamping;
- ExpectedAuthorityRevision capacity binding;
- pending/stale fail-closed admission/use checks;
- bulk lease fencing with exactly-once Generation mutation;
- deterministic PostgreSQL concurrency/migration/fence evidence;
- corrected canonical PostgreSQL CI environment wiring.

## Post-merge gate

EliteSCADA CI #1551 / run `35341475101` is a `push` run on exact merge SHA `20b934f23d8798ffb65cca203b62f8b5c3d8f111`.

Latest observed:
- Backend `105588126265` — SUCCESS;
- Web `105588126462` — SUCCESS;
- Chromium `105588538108` — RUNNING.

Until Chromium is green:
- Phase A = INTEGRATED;
- not VERIFIED/FROZEN;
- FND-03 DEV = WAIT;
- Phase B = not active.

## Next bounded phase after green

Main has already source-mapped Phase B around:
- `ProductLicensedRuntimeCoordinator`;
- `PersistedRuntimeRecoveryService`;
- local Runtime re-evaluation after authority change;
- durable Demo authority-change anchor and remaining-duration semantics;
- fail-closed persisted Runtime recovery while transition is pending / when Demo lacks a durable anchor.

A new clean Phase B branch should start from exact product checkpoint `20b934f23d8798ffb65cca203b62f8b5c3d8f111`.

CODEX remains reserve.
FND-04 remains WAIT.
FC0-A remains blocked.
