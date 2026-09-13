# EliteSCADA documentation authority map

The repository contains stable architecture, product policy, current coordination records, historical Wave evidence and worker assignments. They do **not** have the same operational authority.

## 1. Live repository and CI

**Highest authority for what is actually implemented now.**

Before a decision, write, rerun or merge, inspect live refs, exact SHA/tree, latest issue/PR comments and exact-head Actions evidence. A static SHA in documentation is always a snapshot.

## 2. Stable product/architecture authority

### Root `PROJECT GOAL.md`

Persistent product north and locked architectural intent. Use it for durable product principles, not as the sole source of mutable Wave execution state.

If an old release-sequencing sentence inside `PROJECT GOAL.md` conflicts with the live Wave 15 handoffs, use the Wave 15 handoffs/GitHub for current sequencing while preserving the stable architecture/product rules from `PROJECT GOAL.md`.

### ADRs and locked contract documents

Versioned public-model, licensing, TAG/bit binding, Driver, lifecycle and related architecture decisions take precedence over old worker/handoff prose when they explicitly lock a contract.

## 3. Current Wave 15 operational authority

Read in this order when taking over coordination:

1. root `LAST CHANGE.md` — short mutable resume point;
2. `WAVE15-MAIN-COORDINATOR-HANDOFF.md` — detailed persistent Main Coordinator handoff;
3. `CURRENT-COORDINATOR-HANDOFF.md` — concise live pointer;
4. `NEXT-COORDINATOR-CHAT-HANDOFF.md` — copy-ready prompt for a new coordinator chat;
5. `ROADMAP.md` — current Wave 15 sequencing/checkpoints;
6. GitHub issues #297 and #305, then the active Foundation/product issue/PR and their latest comments.

GitHub live wins over every one of these snapshots.

## 4. Wave 15 coordination surfaces

- #297 — complete-product Wave 15 global status;
- #305 — dependency graph, Foundation checkpoints, parallel DEV orchestration and CI rules;
- #302 — Security Authority / FND-02;
- #301 — Runtime Session Lease / Licensing;
- #303 — Editor;
- #304 — installation detach/switch;
- #298 — EliteGO;
- #299 — HA/redundancy;
- #300 — final integration and fresh Preview.

Read recent comments, not only issue bodies. Creation-time sequencing can be superseded by later binding comments.

## 5. CI authority

All current workflows support `workflow_dispatch`, but a particular ChatGPT connector may not expose creation of a new manual dispatch. Repository capability and chat-tool capability are different things.

Use rerun only after diagnosis. If a new dispatch is required and Main lacks the operation, delegate to Work/Codex/CLI when available. Do not create empty commits or artificial PR retargeting to wake CI.

INFRA-CI-01 owns the Wave 15 move to profile-aware T0/T1/T2/T3/T4 validation. Old workflow triggers tied to `main` or Wave 14 are transitional infrastructure, not a reason to run every heavy suite on every DEV PR.

## 6. Historical records

Wave 14 C25/C26 handoffs, post-C26 audit directives, old Preview evidence, Driver convergence assignments, Wave 11/12 execution logs and older coordinator transfers remain valuable historical evidence.

They are **not current execution authority** after Wave 15 starts unless a current Wave 15 issue explicitly imports one of their contracts/evidence.

Do not delete historical files simply because they are old. Version control preserves history; the authority map prevents history from pretending to be the present.

## 7. Conflict resolution

When sources disagree:

1. inspect the live branch/PR and exact Actions evidence;
2. use `PROJECT GOAL.md` and locked ADR/contracts for durable product intent;
3. use `LAST CHANGE.md` + Wave 15 coordinator handoffs for current operational interpretation;
4. use #297/#305 and the active issue's latest binding comments for dependency/mission state;
5. treat older Wave/assignment/status prose as historical evidence.

Never inherit green CI from another SHA, never call an unexecuted required test PASS, never infer security from role display names and never report a specified feature as implemented without code plus exact-head evidence.
