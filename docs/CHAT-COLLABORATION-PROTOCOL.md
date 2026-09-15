# EliteSCADA — Global Chat Collaboration Protocol

This protocol applies to **every ChatGPT/Codex/Work/DEV/audit/coordinator chat participating in EliteSCADA**, not only to the Main Coordinator.

Repository: `brunolrogerio-collab/EliteSCADA`

GitHub live remains the authority for current implementation state. This protocol defines how all project chats communicate and persist material work.

## 1. End-of-interaction time stamp — mandatory for every chat

Every user-visible response/interation related to EliteSCADA must end with the current local time in `America/Sao_Paulo`, using exactly:

`Hora: HH:MM`

This applies to:

- Main Coordinator;
- Foundation Work / technical reviewer;
- parallel DEV chats;
- CI/infrastructure chats;
- audit/review chats;
- documentation/UX chats;
- Codex or other project agents when they produce a user-visible handoff/status response.

Do not reuse a remembered time. Obtain or calculate the current local time at the end of the interaction using the best available trusted time source.

## 2. Persist material project steps in the repository

A material step must not exist only in chat history.

Whenever a chat completes or discovers a **material project step**, update the relevant persistent repository surfaces before treating that step as durable project state, provided that chat has authorized write capability.

Material steps include, at minimum:

- mission activation, completion, blocking or scope change;
- exact base/head/tree change that affects coordination;
- important architecture or public-contract decision;
- newly discovered blocker or corrected diagnosis;
- creation of a significant branch or PR;
- material implementation checkpoint;
- CI evidence that changes disposition;
- required validation changing from PENDING to PASS/FAIL;
- integration/merge;
- post-merge verification;
- transition to `INTEGRATED`, `VERIFIED` or `FROZEN`;
- dependency being released or newly blocked;
- roadmap/checkpoint sequencing change;
- Product Owner decision that changes project behavior or development process.

Do **not** create repository noise for every shell command, file read, polling request or trivial intermediate observation. Persist state-changing steps, not telemetry about the assistant itself.

## 3. Update the right repository surface

Choose the smallest authoritative set of files/issues needed for the material change.

### `LAST CHANGE.md`

Update when the mutable project resume point changes materially, such as current mission, principal blocker, accepted integration state or immediate next action.

### `docs/CURRENT-COORDINATOR-HANDOFF.md`

Update when coordinator-visible topology, current gate, active mission, integration state or next safe action changes materially.

### `docs/ROADMAP.md`

Update only when sequencing, checkpoints, Wave scope or dependency ordering changes. Do not use it as a per-commit log.

### `PROJECT GOAL.md`

Update only for durable product goals, architectural principles or permanent process rules. Do not fill it with transient SHA/CI status.

### Relevant issue

Use the owning issue as the durable ledger for mission activation, blocker, decision, evidence, integration, verification and freeze.

### Relevant PR

Use the PR for implementation-specific review, correction requests, exact candidate evidence and final disposition.

### Handoff documents

Update when a coordinator/chat rotation would otherwise receive stale or misleading instructions.

## 4. All chats share this persistence duty

The Main Coordinator is responsible for global consistency, but **persistence is not exclusively the coordinator's job**.

A Work or DEV chat that owns an authorized mission must persist its own material checkpoints in the appropriate branch/PR/issue/handoff surface when it has write access and the mission contract allows it.

Examples:

- DEV publishes a PR: record exact base/head, scope and validation in the PR/handoff;
- Work discovers a contract blocker: record it in the owning issue/PR instead of leaving it only in chat;
- CI chat proves a required runtime gate: attach the exact run/job/SHA evidence to the relevant coordination surface;
- auditor corrects a material diagnosis: persist the corrected diagnosis in the owning issue or review thread.

The Main Coordinator then reviews and, when necessary, propagates the state into global handoff/roadmap documents.

## 5. Chats without repository write capability

If a chat cannot write to GitHub or another required persistent surface:

1. do not claim that the repository was updated;
2. produce a concise, copy-ready persistence note/handoff containing the exact material change and evidence;
3. notify the Main Coordinator or Product Owner that persistence remains `PENDING`;
4. continue non-blocked work when safe.

A tool limitation must not silently turn a material decision into chat-only memory.

## 6. No documentation-only fiction

Repository updates must reflect verified reality.

Do not update status to PASS, VERIFIED, FROZEN or COMPLETE merely because the implementation chat believes the work is done.

Use exact evidence and the project's state model. Required test not executed remains `PENDING`.

## 7. Avoid conflicting writes from parallel chats

Parallel chats may work simultaneously under Main Coordinator supervision.

Before editing a shared coordination file, a chat should re-read the live file/ref and avoid overwriting newer work from another chat.

Mission-specific evidence normally belongs first in the owning issue/PR. The Main Coordinator owns cross-mission consolidation when concurrent edits to global documents would create unnecessary conflicts.

## 8. Coordinator handoff rule

A new Main Coordinator must read this protocol before supervising Work/DEV chats and must propagate these requirements into every new mission prompt:

- every chat ends each user-visible interaction with `Hora: HH:MM`;
- every material project step is persisted to the appropriate repository surface;
- no chat may rely on conversation history as the sole durable record of project state.

These requirements are global EliteSCADA collaboration rules, not coordinator-specific preferences.