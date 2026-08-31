# EliteSCADA documentation authority map

This directory contains architecture decisions, historical assignments, laboratory evidence, product policy and live coordination notes. They do **not** have the same authority.

## Stable product authority

### Root `PROJECT GOAL.md`

**Persistent product north and locked product intent.** Read it before planning new EliteSCADA work. It now includes the final Demo/hardware-bound licensing contract at product-goal level.

Current repository code/`main` still wins for what is actually implemented. `PROJECT GOAL.md` wins for explicitly locked future product intent.

## Operational source of truth

### `CURRENT-COORDINATOR-HANDOFF.md`

**Single operational handoff.** Use it for current branch/PR, last accepted exact-head CI, Driver convergence stage, current-vs-final Preview capacity distinction, blockers and immediate next action.

Static SHAs are snapshots. Live GitHub refs and exact-head Actions evidence always win after a fresh read.

### Root `LAST CHANGE.md`

Short operational resume point. It must clearly distinguish **MERGED**, **IMPLEMENTED IN PR** and **SPECIFIED / NOT IMPLEMENTED**.

### `COORDINATOR-TRANSFER-2026-08-31.md`

Concise replacement-coordinator checkpoint created after the Demo/licensing product decision. Use it together with `PROJECT GOAL.md`, `LAST CHANGE.md` and the canonical handoff when changing coordinator/chat.

### `COORDINATOR-HANDOFF.md`

**Legacy path / superseded.** Retained only for old links and redirected to `CURRENT-COORDINATOR-HANDOFF.md`.

## Preview / licensing product policy

### `PREVIEW-CAPACITY-POLICY.md`

Owns the distinction between the **current validated transitional code** and the **final desired Demo behavior**.

Current validated code at `6d340e8...` / CI #982 uses a static 200-TAG project ceiling and rejects creation/import of the 201st TAG.

That is transitional behavior, not the final licensing contract.

### `LICENSING-AND-DEMO-MODE.md`

Owns the detailed final Demo/licensing specification:

- no license => Demo;
- Engineering may exceed 200 TAGs;
- Demo Run gate at 200 TAGs;
- 300 continuous minutes per explicit Demo Run session;
- hardware-derived copyable request code;
- asymmetrically signed machine-bound license;
- 500 / 1000 / 1500 / 3000 / 5000 / Unlimited tiers;
- valid licensed/evaluation entitlement removes Demo time limit;
- installed invalid/wrong-hardware license blocks Run;
- private signing key never enters GitHub/normal product distribution.

Status: **SPECIFIED / NOT IMPLEMENTED**. Tracking issue: **#183**.

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
- post-main integrated L3 definition;
- distinction between peer health and actual Driver product path.

It does not own coordinator implementation progress.

Issue **#180** owns the integrated seven-Driver post-main L3 campaign.

## Assignment / historical records

### `PARALLEL-DRIVER-WORK-ASSIGNMENTS.md`

Historical worker authorization, ownership and isolation boundaries from the parallel-development phase. Not current Driver status.

### `CHAT-WORK-ASSIGNMENTS.md`

Coordination/assignment record. Treat assignment text and embedded SHAs as snapshots, not as the current product state or merge authority.

## Roadmap

### `ROADMAP.md`

Product sequencing and planned scope. It does not override a live convergence gate or exact-head CI result.

## GitHub coordination surfaces

- Issue **#174**: shared Driver convergence/mainline/L3 stage tracking.
- Issue **#180**: integrated seven-Driver post-main L3 acceptance.
- Issue **#183**: Demo/hardware licensing implementation track.
- Draft PR **#175**: actual long-lived coordinator integration line until controlled merge.
- Worker PRs: protocol implementation/evidence snapshots; descriptions may lag live branch heads.

## Conflict resolution

When two sources disagree:

1. re-read the live branch/PR ref;
2. inspect Actions for that exact SHA;
3. use root `PROJECT GOAL.md` for locked product intent;
4. use `CURRENT-COORDINATOR-HANDOFF.md` + `LAST CHANGE.md` for operational interpretation;
5. use `PREVIEW-CAPACITY-POLICY.md` and `LICENSING-AND-DEMO-MODE.md` for current-vs-final Preview/licensing semantics;
6. use ADRs / `DRIVER-CONVERGENCE-COORDINATION-V1.md` for architecture semantics;
7. use `DRIVER-AND-INTEROP-LAB-STATUS.md` for lab evidence;
8. treat older status, assignment and worker-PR prose as historical evidence.

Never inherit green CI from another SHA, never turn peer-lab health into a Driver L2 acceptance claim, and never report a specified licensing feature as implemented without code + exact-head CI evidence.
