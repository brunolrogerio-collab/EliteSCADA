# EliteSCADA documentation authority map

This directory contains architecture decisions, historical assignments, laboratory evidence and live coordination notes. These documents do **not** all have the same authority, so the hierarchy below is explicit.

## Operational source of truth

### `CURRENT-COORDINATOR-HANDOFF.md`

**Single operational handoff.** Use it for:

- current coordinator branch/PR;
- last accepted exact-head CI;
- current Driver convergence stage;
- Driver L2 pass/fail/pending classification;
- live/audited worker heads;
- current blockers;
- immediate next action;
- new-Coordinator resume procedure.

Static SHAs are still snapshots. Live GitHub refs and exact-head Actions evidence always win after a fresh re-read.

## Architectural authority

### `DRIVER-CONVERGENCE-COORDINATION-V1.md`

Shared Driver architecture and convergence semantics. Use it for contract intent such as registry composition, readiness, protected material, rich binding, operation boundaries and timestamp policy.

**Do not use its observed worker SHAs or per-Driver “next milestone” prose as current operational status.** Those are historical coordination snapshots.

### ADRs / locked architecture documents

Architecture semantics take precedence over old handoff prose when they explicitly lock a decision. They do not replace live implementation evidence.

## Laboratory evidence

### `DRIVER-AND-INTEROP-LAB-STATUS.md`

Current laboratory evidence and terminology:

- common peer lab health;
- independent-software L2 product acceptance;
- distinction between peer health and actual Driver product path.

It does not own coordinator implementation progress.

## Historical assignment records

### `PARALLEL-DRIVER-WORK-ASSIGNMENTS.md`

Historical worker authorization, ownership and isolation boundaries from the parallel-development phase.

**Not current Driver status.** “AUTHORIZED”, “RESEARCH FIRST” and old SHAs in that file describe the phase when they were written.

## GitHub coordination surfaces

- Issue **#174**: shared Driver convergence scope and current mirrored summary.
- Draft PR **#175**: actual long-lived coordinator integration line.
- Worker PRs: protocol implementation/evidence snapshots. Their descriptions may lag live branch heads.

## Conflict resolution

When two sources disagree:

1. re-read the live branch/PR ref;
2. inspect Actions for that exact SHA;
3. use `CURRENT-COORDINATOR-HANDOFF.md` for operational interpretation;
4. use ADRs / `DRIVER-CONVERGENCE-COORDINATION-V1.md` for architecture semantics;
5. treat older status, assignment and worker-PR prose as historical evidence.

Never inherit green CI from another SHA and never turn peer-lab health into a Driver L2 acceptance claim.
