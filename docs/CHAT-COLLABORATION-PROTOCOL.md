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

## 9. Bounded execution and incremental continuation

Product Owner direction, 2026-10-15: adjust the coordination flow before resuming product development. This section governs execution cadence wherever older handoffs require repeating full historical discovery. Product goals, frozen contracts, ownership boundaries, and required validation remain governed by their existing authorities.

### Establish once, then check changes

At the start of a mission or after loss of context, establish the baseline from `PROJECT GOAL.md`, this protocol, the latest `LAST CHANGE.md` entry, `CURRENT-COORDINATOR-HANDOFF.md`, the current Main order, relevant frozen contracts, and live GitHub. During Wave 15, read #297 first and #302 next when establishing the product/Authority baseline, then #305 and the active issue/PR.

For continuation of the same mission, check only changed authoritative documents, current refs, latest binding issue/PR comments, outstanding review requests, and the relevant existing run IDs. An unchanged input does not require another full read, test run, handoff, or authorization request. A new dependency, scope change, contradictory evidence, or context loss expands the read set as needed.

Use authenticated GitHub API/CLI and the current checkout for operational state. If a public page or chat summary conflicts with newer evidence, verify the specific live ref/comment/run directly. Do not restart the investigation from an old cached blocker.

### One owner at each transition

Main owns package design, assignment, review, integration, and coordination records. CODEX/DEV owns its assigned implementation, focused local validation, draft PR, and delivery. Each transition states the next owner and one executable action. Main and the executor must not duplicate implementation or CI dispatch for the same candidate.

Existing Product Owner authorization persists within its stated scope and conditions. Main releases a separate bounded order when the work package requires it; that internal Main decision is not a request for repeated Product Owner permission. Escalate a materially new product front, changed frozen contract, or changed acceptance expectation to its proper decision authority.

After delivery, the worker waits for Main. Main reviews and advances the authorized flow, including an already-authorized conditional integration once its gates pass. A worker never self-merges or chooses its next package.

### Send once and observe the existing execution

When the Product Owner has authorized coordination with an existing executor chat and a messaging tool is available, Main sends the order directly once and verifies delivery. Observe that chat with the wait mechanism and its cursor; use a bounded wait that permits user updates. Do not ask the owner to relay routine technical messages or repeatedly wake an already-running executor.

A GitHub issue comment alone does not wake another chat. If direct dispatch is unavailable, name the exact receiving chat and action needed. Do not claim an order was received based only on repository publication.

### Handle failure without repeated attempts

Record a blocker as: failed operation and exact error; affected prerequisite; current base/head; next owner; observable condition that permits resumption. A failed operation proves only that operation failed. Reuse a verified accessible checkout or authorized alternate route when available, preserving user changes and ordinary branch history.

For the same unresolved environment error, use at most one evidenced retry or alternate route. The budget spans `siga` turns and resets only when a relevant input, environment condition, or diagnosis changes. If blocked, continue independent work within the same authorized front when useful; do not switch product fronts to stay busy.

For a failing test/CI, inspect logs and the candidate diff, then reproduce the failing test locally where available. Classify the cause before selecting a fix; a shared file boundary is not itself a root cause, and a local non-reproduction does not prove flakiness. Correct demonstrated in-scope defects with meaningful checks. Any rerun requires the grounds in `CI-USAGE-POLICY.md`, uses the smallest required job set, and retains the exact SHA/attempt evidence. Do not weaken tests or broaden the package to obtain green.

### Validate and close once

Define the validation profile and acceptance evidence in the package before coding. Use focused local checks during implementation; publish one coherent candidate for required T1, review it, integrate under applicable authorization, and inspect the required post-merge gate. Reuse valid unchanged-product evidence after documentation-only movement. Do not dispatch broad product CI for a process-documentation update unless risk or an existing gate requires it.

Local results, CI, and Product Owner Preview each answer their stated acceptance questions. Required local/human evidence is not replaced by green CI. Required CI failures are not overridden by a local pass. Physical driver L4 remains human validation after Wave 16, partner disclosure, and stable installation; record it as `DEFERRED / NOT RUN` until then, independently from applicable software integration gates.

An outstanding Main correction remains open until its named evidence/correction exists and Main accepts the disposition. Returning the same green candidate does not close a later review request.

### Keep the resume point short

Maintain one current snapshot in `CURRENT-COORDINATOR-HANDOFF.md` and one current order in the active Main handoff. Update them in place. Keep the transfer bootstrap independent from current SHAs and assignments. Detailed evidence belongs in the owning issue/PR; preserve older long snapshots in clearly marked archives rather than stacking new CURRENT headings over them.

Publish updates on meaningful transitions: assignment, delivery, diagnosed blocker, review disposition, integration, verification. Unchanged polling results and acknowledgements do not need new issue comments. Each final status states the result, remaining acceptance item if any, next action, and responsible owner.
