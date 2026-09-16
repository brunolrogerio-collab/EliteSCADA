# EliteSCADA documentation authority map

The repository contains stable architecture, product policy, current coordination records, historical Wave evidence and worker assignments. They do **not** have the same operational authority.

## 1. Live repository and CI

**Highest authority for what is actually implemented now.**

Before a decision, write, rerun or merge, inspect live refs, exact SHA/tree, latest issue/PR comments and exact-head Actions evidence. A static SHA in documentation is always a snapshot.

## 2. Global collaboration protocol — mandatory for every project chat

### `CHAT-COLLABORATION-PROTOCOL.md`

This protocol applies to **all EliteSCADA chats/agents**, including Main Coordinator, Foundation Work, parallel DEV chats, CI/infrastructure, audit, documentation, UX and Codex.

Mandatory global rules include:

- every user-visible EliteSCADA interaction ends with current `America/Sao_Paulo` time in the format `Hora: HH:MM`;
- every material project step is persisted in the appropriate repository file, issue or PR instead of existing only in chat history;
- chats with no write capability must report persistence as `PENDING` and produce a copy-ready handoff rather than falsely claiming an update;
- parallel chats must re-read shared coordination surfaces before writing and avoid overwriting newer concurrent state.

Every new Work/DEV/coordinator mission must inherit these rules.

## 3. Stable product/architecture authority

### Root `PROJECT GOAL.md`

Persistent product north and locked architectural intent. Use it for durable product principles, not as the sole source of mutable Wave execution state.

If an old release-sequencing sentence inside `PROJECT GOAL.md` conflicts with the current live handoff/GitHub, use the live Wave handoff/GitHub for execution sequencing while preserving the stable architecture/product rules from `PROJECT GOAL.md`.

### ADRs and locked contract documents

Versioned public-model, licensing, TAG/bit binding, Driver, lifecycle and related architecture decisions take precedence over old worker/handoff prose when they explicitly lock a contract.

## 4. Current operational authority

When taking over coordination during Wave 15, read in this order:

1. root `PROJECT GOAL.md` — durable product/architecture north;
2. `CHAT-COLLABORATION-PROTOCOL.md` — mandatory behavior for every project chat;
3. root `LAST CHANGE.md` — short mutable resume point;
4. `CURRENT-COORDINATOR-HANDOFF.md` — short current combinator/pointer;
5. `WAVE15-MAIN-COORDINATOR-HANDOFF.md` — **live canonical operational handoff for Main Coordinator <-> Codex/Foundation Work while Wave 15 is active**;
6. `NEXT-COORDINATOR-CHAT-HANDOFF.md` — permanent, state-independent protocol for replacing the Main Coordinator chat;
7. `ROADMAP.md` — current sequencing/checkpoints;
8. current coordinator/Foundation issues and every active mission issue/PR with their latest comments.

GitHub live wins over every snapshot.

`WAVE15-MAIN-COORDINATOR-HANDOFF.md` is the detailed living handoff for Wave 15. It must not be classified as historical while Wave 15 is active.

`CURRENT-COORDINATOR-HANDOFF.md` is deliberately smaller: it combines/pivots the current exchange by pointing to the active order, the Wave 15 handoff and live ledgers without maintaining a second full copy of the state.

`NEXT-COORDINATOR-CHAT-HANDOFF.md` intentionally does **not** carry a current SHA/PR snapshot. A replacement coordinator must discover the live state from the sources above rather than inherit stale execution data from the transfer prompt.

## 5. Coordination surfaces

Current issue numbers and Wave-specific coordination surfaces can change over the lifetime of the project. Discover them from `LAST CHANGE.md`, `CURRENT-COORDINATOR-HANDOFF.md`, `WAVE15-MAIN-COORDINATOR-HANDOFF.md`, `ROADMAP.md` and live GitHub instead of treating an old issue list as permanent.

The detailed current Main -> Codex order and expected Codex -> Main return contract belong in `WAVE15-MAIN-COORDINATOR-HANDOFF.md`. `CURRENT-COORDINATOR-HANDOFF.md` is the short combinator/pointer. Issues remain the durable ledger for binding decisions, scope, evidence, blockers, integration and freeze records.

Read recent comments, not only issue bodies. Creation-time sequencing can be superseded by later binding comments.

## 6. CI authority

Repository workflow capability and a particular chat connector's tool capability are different things.

Use rerun only after diagnosis. If a new dispatch is required and the current chat lacks the operation, delegate through an authorized Work/Codex/CLI/session when available. Do not create empty commits or artificial PR retargeting merely to wake CI.

Use the current CI policy/profile documents and live workflow definitions for the active development stage. Historical trigger topology from an older Wave is not a reason to run every heavy suite on every PR.

## 7. Historical records

Old completed-Wave handoffs, Preview evidence, Driver convergence assignments, execution logs and prior coordinator transfers remain valuable historical evidence.

They are **not current execution authority** unless the active Wave handoff or a current binding issue explicitly imports one of their contracts/evidence.

Do not delete historical files simply because they are old. Version control preserves history; this authority map prevents history from pretending to be the present.

## 8. Conflict resolution

When sources disagree:

1. inspect the live branch/PR and exact Actions evidence;
2. use `PROJECT GOAL.md` and locked ADR/contracts for durable product intent;
3. use `CHAT-COLLABORATION-PROTOCOL.md` for project-wide chat/process behavior;
4. use `LAST CHANGE.md` + `CURRENT-COORDINATOR-HANDOFF.md` + the active Wave handoff for operational interpretation;
5. use current coordinator/Foundation issues and active issue latest comments for dependency/mission state;
6. treat older completed-Wave/assignment/status prose as historical evidence.

Never inherit green CI from another SHA, never call an unexecuted required test PASS, never infer security from role display names and never report a specified feature as implemented without code plus exact-head evidence.
