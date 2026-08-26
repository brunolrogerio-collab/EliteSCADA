# CHAT WORK ASSIGNMENTS — EliteSCADA

> Authoritative coordination board for EliteSCADA ChatGPT workstreams.
>
> Purpose: answer **who is doing what now, on which branch, under which boundaries, and what that chat must do when the user says only `siga`**.

**Coordination protocol introduced:** 2026-08-26
**Latest completed functional integration wave baseline:** `main` at `889c989fdce26d8593e86e430e76417412846400` after PR #45.

This file is coordination state, not implementation truth. Before working, every chat must verify the real GitHub branch, PR, head commit and CI state. If this file and GitHub disagree about operational state, GitHub wins and the discrepancy must be reconciled by the coordinator. Stable product/architecture intent remains governed by `PROJECT GOAL.md`.

## 1. Permanent `siga` protocol

The user's canonical short command is:

`siga`

`continue` is accepted as a backward-compatible alias with identical meaning.

When the user sends only `siga` or `continue` in an EliteSCADA chat, the chat must:

1. identify itself by its fixed workstream/chat name;
2. read, from current GitHub `main`:
   - `PROJECT GOAL.md`;
   - `LAST CHANGE.md`;
   - `docs/ROADMAP.md`;
   - `docs/PARALLEL-WORK.md`;
   - `docs/CHAT-WORK-ASSIGNMENTS.md`;
   - every document listed in its own `MustReadSpecific` field;
3. locate its exact assignment section in this file;
4. verify the real GitHub state of its assigned branch, PR, head commit and relevant CI before editing anything;
5. obey `Status`, `AllowedScope`, `ForbiddenScope`, `Dependencies`, `NextActions`, `CompletionCriteria` and `AfterCompletion`;
6. continue automatically without asking the user to repeat the original task prompt.

### Decision table

- `ASSIGNED` or `IN_PROGRESS`: start or continue the assigned task.
- `PR_OPEN`: continue the assigned PR task until its completion criteria are met.
- `CI_FAILED`: inspect the failed CI, fix the assigned branch only, and revalidate.
- `READY_FOR_COORDINATOR_REVIEW`, `WAITING` or `COMPLETED` + `AfterCompletion: WAIT_FOR_COORDINATOR`: do **not** start new work.
- `MERGED` or `COMPLETED` + `AfterCompletion: TAKE_NEXT_ASSIGNED_TASK`: act only if a separate explicit next task is already recorded here.
- `MERGED` or `COMPLETED` + `AfterCompletion: NEXT_TASK: <task>`: start exactly that named task using its recorded branch/scope rules.
- assignment not found: do not infer work from roadmap/history/old branches; report no assignment and wait for coordinator action.

A worker must never reinterpret `siga` as permission to choose its own next roadmap item.

## 2. Authority to change assignments

Only **COORDENADOR - EliteSCADA** may add, remove or change work assignments for DEV chats in this file.

Workers may:

- read this file;
- verify their branch/PR/CI against GitHub;
- update code and tests inside their authorized branch/scope;
- update their own PR body with implementation status, CI evidence and `INTEGRATION REQUIRED` notes.

Workers must not:

- edit this file to give themselves new work;
- change another chat's assignment;
- create a new task/branch because the current task is complete;
- alter `main`;
- merge their own PR;
- resume an older merged branch as if it were a new assignment;
- work in another chat's branch or reserved domain unless explicitly reassigned here.

## 3. Status vocabulary

Assignment status values:

- `ASSIGNED`
- `IN_PROGRESS`
- `PR_OPEN`
- `CI_FAILED`
- `READY_FOR_COORDINATOR_REVIEW`
- `INTEGRATION_REQUIRED`
- `MERGED`
- `BLOCKED`
- `WAITING`
- `COMPLETED`

Repository/product terminology:

- **MERGED** — official `main` state;
- **IMPLEMENTED IN PR** — exists only in a feature branch/open PR;
- **SPECIFIED / NOT IMPLEMENTED** — architecture/product intent exists but implementation does not.

---

# COORDENADOR - EliteSCADA

**Role:** `COORDINATOR`

**CurrentTask:** Post-integration coordination checkpoint after PRs #40–#45

**Branch:** `main`

**Status:** `WAITING`

**PullRequest:** none active for this checkpoint

**ObservedFunctionalHead:** `889c989fdce26d8593e86e430e76417412846400` — merge of PR #45 before the documentation synchronization commits that follow it.

**Objective:**

Keep repository/documentation/worker assignments synchronized after the completed integration wave, then schedule the next development wave only after the permanent bootstrap instruction has been installed in the fixed DEV chats.

**Responsibilities:**

- own shared integration and merge ordering;
- maintain this assignment board and coordinator-owned documentation;
- reconcile worker PRs without discarding valid worker commits;
- implement coordinator-owned cross-domain hooks when required;
- run relevant CI before merge;
- preserve `MERGED` vs `IMPLEMENTED IN PR` distinction;
- choose the next worker tasks from actual roadmap dependencies, not conversational momentum;
- keep workers idle until their next assignment is explicitly recorded here.

**AllowedScope:**

Coordinator may modify shared/central files and coordination documents when required by integration or scheduling.

**ForbiddenScope:**

Do not silently rewrite worker history, force-reset worker branches, merge known-failing work, invent product state, or schedule work that violates locked roadmap dependency order.

**MustReadSpecific:**

- task-specific architecture document(s) for the next assignment wave;
- worker PR bodies and `INTEGRATION REQUIRED` sections when reviewing a new delivery.

**Dependencies:**

The previous integration wave is complete. Before scheduling new worker work, the fixed DEV chats must receive their permanent bootstrap instruction so future coordination can rely on repository assignments instead of copied prompts.

**NextActions:**

1. wait for confirmation that the permanent DEV bootstrap text has been installed in `DEV 1`, `DEV 2` and `DEV 3` chats;
2. on the next coordinator `siga`, re-read current GitHub state and choose the next safe assignment wave from `PROJECT GOAL.md` and `docs/ROADMAP.md`;
3. record each new task/branch/scope here before the user sends `siga` in the corresponding DEV chat;
4. prefer dependency-safe parallelism rather than keeping all three workers busy at the cost of shared-file conflicts.

**CompletionCriteria:**

- documentation matches merged repository state;
- workers have no stale open-task assignment;
- `siga` is the permanent command in repository coordination rules;
- next product tasks are not started until explicit assignments are written here.

**AfterCompletion:** `CONTINUE_COORDINATION`

---

# DEV 1 - EliteSCADA

**Role:** `WORKER`

**CurrentTask:** Previous Audit Durability + Retention + Query Foundation is complete

**Branch:** none active — previous branch `feature/audit-durability-retention-query` is historical

**Status:** `COMPLETED`

**PullRequest:** `#44` — **MERGED**

**MergeCommit:** `9406fb2d66c682bd6bde08a0facde0622aa86ff2`

**RelatedCoordinatorIntegration:** PR `#45` — **MERGED** as `889c989fdce26d8593e86e430e76417412846400`

**Objective:**

No current implementation objective. Wait for a new explicit assignment.

**AllowedScope:** none until reassigned.

**ForbiddenScope:**

- creating a new branch/task;
- resuming PR #44 as active work;
- modifying `main`;
- selecting Audit, Gateway, Historian, Python, UI or any other roadmap item independently.

**MustReadSpecific:** none while waiting; next assignment will declare its required documents.

**Dependencies:** new work depends on coordinator reassignment.

**CompletionCriteria:** previous assigned work is merged and coordinator integration is complete.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`

**ContinueBehaviorNow:**

On `siga`, verify GitHub and report that the previous Audit task is merged and there is no new authorized DEV 1 task. The next operational action is for the user to send `siga` in `COORDENADOR - EliteSCADA`; after the coordinator records a new DEV 1 assignment, a later `siga` here starts it automatically.

---

# DEV 2 - EliteSCADA

**Role:** `WORKER`

**CurrentTask:** Previous Internal Memory foundation and Historian Retention/Downsampling foundation are complete

**Branch:** none active — previous branches are historical

**Status:** `COMPLETED`

**PullRequests:**

- `#40` Internal Memory / Source Provider Foundation — **MERGED** as `bb38617c9c27cb5c379973a6f65d66006f24eadc`;
- `#43` Historian Retention + Downsampling Foundation — **MERGED** as `0c5f2aefdd5a7286c0c9367569067e2d12091c81`.

**Objective:**

No current implementation objective. Wait for a new explicit assignment.

**AllowedScope:** none until reassigned.

**ForbiddenScope:**

- creating a new branch/task;
- resuming PR #40 or #43 as active work;
- modifying `main`;
- starting Internal Memory product integration, Gateway, Historian UI or another roadmap item without a recorded coordinator assignment.

**MustReadSpecific:** none while waiting; next assignment will declare its required documents.

**Dependencies:** new work depends on coordinator reassignment.

**CompletionCriteria:** previous assigned foundations are merged.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`

**ContinueBehaviorNow:**

On `siga`, verify GitHub and report that PRs #40 and #43 are merged and there is no new authorized DEV 2 task. The next operational action is for the user to send `siga` in `COORDENADOR - EliteSCADA`; after the coordinator records a new DEV 2 assignment, a later `siga` here starts it automatically.

---

# DEV 3 - EliteSCADA

**Role:** `WORKER`

**CurrentTask:** Previous Python Scripting + Visual Property Foundation is complete

**Branch:** none active — previous branch `feature/python-scripting-foundation` is historical

**Status:** `COMPLETED`

**PullRequest:** `#41` — **MERGED**

**MergeCommit:** `fc0731309d5b92d302f019d06d3511d3a247b607`

**Objective:**

No current implementation objective. Wait for a new explicit assignment.

**AllowedScope:** none until reassigned.

**ForbiddenScope:**

- creating a new branch/task;
- resuming PR #41 as active work;
- modifying `main`;
- starting script editor, visual runtime integration, graphical editor or another roadmap item without a recorded coordinator assignment.

**MustReadSpecific:** none while waiting; next assignment will declare its required documents.

**Dependencies:** new work depends on coordinator reassignment.

**CompletionCriteria:** previous assigned foundation is merged.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`

**ContinueBehaviorNow:**

On `siga`, verify GitHub and report that PR #41 is merged and there is no new authorized DEV 3 task. The next operational action is for the user to send `siga` in `COORDENADOR - EliteSCADA`; after the coordinator records a new DEV 3 assignment, a later `siga` here starts it automatically.

---

## 4. Adding future chats/workstreams

When a new fixed EliteSCADA chat is created, the coordinator must add a section before that chat is expected to work from `siga` alone. At minimum every assignment must contain:

- `Role`
- `CurrentTask`
- `Branch`
- `Status`
- `PullRequest` when one exists
- `Objective`
- `AllowedScope`
- `ForbiddenScope`
- `MustReadSpecific`
- `Dependencies`
- `IntegrationRequired` when applicable
- `NextActions` when useful
- `CompletionCriteria`
- `AfterCompletion`

Every chat must still verify GitHub before acting, regardless of how current this board appears.