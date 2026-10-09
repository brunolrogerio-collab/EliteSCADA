# EliteSCADA — Global Chat Collaboration Protocol

This protocol applies to **every ChatGPT/Codex/Work/DEV/audit/coordinator chat participating in EliteSCADA**, not only to the Main Coordinator.

Repository: `brunolrogerio-collab/EliteSCADA`

GitHub live remains the authority for current implementation state. This protocol defines how all project chats communicate and persist material work.

## 1. End-of-interaction time stamp — mandatory for every chat

Every user-visible response/interation related to EliteSCADA must end with the current local time in `America/Sao_Paulo`, using exactly:

`DD/MM/YYYY — Hora: HH:MM BRT`

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

- every chat ends each user-visible interaction with `DD/MM/YYYY — Hora: HH:MM BRT`;
- every material project step is persisted to the appropriate repository surface;
- no chat may rely on conversation history as the sole durable record of project state.

These requirements are global EliteSCADA collaboration rules, not coordinator-specific preferences.

## 9. Chat-local wake and per-chat next action

A `SIGA` / `continue` wakes only the chat that received that message. It never wakes a different Main or DEV chat. GitHub comments, issues, branches, PRs and Actions runs are durable records but are not wake signals for separate conversations.

At the end of each Main response, enumerate the active DEV chats and tell the Product Owner the action for each exact chat: send `SIGA` there, `WAIT`, or `NO ACTION`. At the end of each DEV response, state whether that same DEV chat needs `SIGA`, is waiting for Main, or is done. Do not ask the Product Owner to relay routine technical messages between Main and DEV; persist the checkpoint in the owning GitHub issue or PR.

`SIGA` resumes only the existing authorized lane after the live GitHub state has been re-read. It does not authorize merge, a new lane, an architecture change, scope expansion, or a reserved/shared-contract change.

## 10. Required DEV Bootstrap and GitHub evidence

Every new DEV assignment must carry the reusable operating block in `docs/PARALLEL-WORK.md` §3.2, with the exact lane issue, branch, base SHA, allowed/forbidden scope, required validation profile and report destination filled in. A Bootstrap must say that local tests are T0 only, GitHub live is the authority, a normal exact-head T1 on the published SHA is required, failures must be classified before correction, and no merge occurs without separate explicit Product Owner authorization. If the owner has already given conditional authorization tied to named tests and audit gates, Main revalidates those gates live and merges once they pass; do not ask the owner to repeat it.

If ordinary HTTPS push or the `gh` CLI is unavailable, do not ask the Product Owner to create credentials or share a token. Use the authorized GitHub connector/API to publish Git blobs/tree/commit/ref when available, verify the published tree, or report `BLOCKED_GIT_AUTH` to Main. Never claim repository publication or T1 based only on a local checkout. Physical L4 scheduled after Wave 16 and partner disclosure must be recorded as `DEFERRED / NOT RUN` until then; do not substitute simulation or claim compatibility. Continue the other authorized available gates.


## 11. Human L4 and code merge readiness

Physical L4 is a human validation that happens only after Wave 16 and partner disclosure, after a stable EliteSCADA release has been installed on a computer. Until that time, record L4 as `DEFERRED / NOT RUN`; do not substitute a simulator. L4 is not a pre-merge blocker for completed driver code. A driver may be integrated when its implementation is complete, applicable intermediate/focused tests and exact-head T1 pass, Main's audit passes, and the Product Owner's applicable merge authorization is present. The coordinator revalidates those facts in live GitHub and executes an already-authorized conditional merge without asking the owner to repeat it. Do not claim hardware compatibility until a human records L4 evidence.

S4 Server Script functionality is not a physical protocol compatibility claim; its code/test/audit gates stand on their own. The physical L4 rule applies to driver hardware validation.

## 12. Outstanding Main audit requests

On every SIGA, a DEV must inspect the current owning issue, PR discussion/review and latest Main disposition in #305, including the exact audit permalink when supplied. Initial release/bootstrap status and a green T1 do not close later Main correction requests.

If Main has requested source or coverage changes, the next authorized action is to implement those named corrections, run the minimum owning focused checks, publish a coherent new candidate, obtain normal exact-head T1 and report each request with evidence. Do not wait for a second Main audit or repeated Product Owner permission to begin already-authorized corrections.

A later DEV completion handoff cannot supersede an outstanding Main review. Repeating the old green HEAD while required source/coverage changes remain is not completion. A request closes only when the required correction/evidence exists and Main accepts its disposition. Report a real tool/contract blocker precisely instead of restating completion.

Every new DEV Bootstrap must include this check and name the outstanding request IDs, required next step and owning report destination. Main's summary must still give the owner an exact chat-local SIGA or WAIT action.

Shared RED ownership must also be distinguished from root cause: SHARED_HOTSPOT identifies a boundary; inspect logs/source before labeling the product defective. A timing-sensitive fixture that crashes before the phase it intends to test is corrected at the harness with explicit phase synchronization, preserving valid product rejection and assertions. Any exception to worker shared-file scope must be explicit, bounded and recorded by Main.
