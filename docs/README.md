# EliteSCADA documentation authority map

This directory contains architecture decisions, historical assignments, laboratory evidence and live coordination notes. They do **not** have the same authority.

## Operational source of truth

### `CURRENT-COORDINATOR-HANDOFF.md`

**Single operational handoff.** Use it for current branch/PR, last accepted exact-head CI, Driver convergence stage, blockers and immediate next action.

Static SHAs are snapshots. Live GitHub refs and exact-head Actions evidence always win after a fresh read.

### Root `LAST CHANGE.md`

A short mirror/checkpoint only. It points back to `CURRENT-COORDINATOR-HANDOFF.md` and must not become a second independent status authority.

### `COORDINATOR-HANDOFF.md`

**Legacy path / superseded.** Retained only for old links and redirected to `CURRENT-COORDINATOR-HANDOFF.md`.

## Architectural authority

### `DRIVER-CONVERGENCE-COORDINATION-V1.md`

Shared Driver architecture and convergence semantics: registry composition, readiness, protected material, rich binding, operation boundaries and timestamp policy.

Do not use its observed worker SHAs or old per-Driver milestone prose as current operational status.

### ADRs / locked architecture documents

Architecture semantics take precedence over old handoff prose when they explicitly lock a decision. They do not replace live implementation evidence.

## Laboratory evidence

### `DRIVER-AND-INTEROP-LAB-STATUS.md`

Owns laboratory evidence and terminology:

- common peer lab health;
- independent-software L2 product acceptance;
- distinction between peer health and actual Driver product path.

It does not own coordinator implementation progress.

## Assignment / historical records

### `PARALLEL-DRIVER-WORK-ASSIGNMENTS.md`

Historical worker authorization, ownership and isolation boundaries from the parallel-development phase. Not current Driver status.

### `CHAT-WORK-ASSIGNMENTS.md`

Coordination/assignment record. Treat assignment text and embedded SHAs as snapshots, not as the current product state or merge authority.

## Roadmap

### `ROADMAP.md`

Product sequencing and planned scope. It does not override a live convergence gate or exact-head CI result.

## GitHub coordination surfaces

- Issue **#174**: shared Driver convergence scope and mirrored current summary.
- Draft PR **#175**: actual long-lived coordinator integration line.
- Worker PRs: protocol implementation/evidence snapshots; descriptions may lag live branch heads.

## Conflict resolution

When two sources disagree:

1. re-read the live branch/PR ref;
2. inspect Actions for that exact SHA;
3. use `CURRENT-COORDINATOR-HANDOFF.md` for operational interpretation;
4. use ADRs / `DRIVER-CONVERGENCE-COORDINATION-V1.md` for architecture semantics;
5. use `DRIVER-AND-INTEROP-LAB-STATUS.md` for lab evidence;
6. treat older status, assignment and worker-PR prose as historical evidence.

Never inherit green CI from another SHA and never turn peer-lab health into a Driver L2 acceptance claim.
