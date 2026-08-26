# PARALLEL WORK — EliteSCADA

This file defines the permanent rules for concurrent ChatGPT workstreams on the EliteSCADA repository.

Dynamic assignments are maintained separately in `docs/CHAT-WORK-ASSIGNMENTS.md`.

## 1. Core ownership rule

Each development chat owns exactly one assigned workstream/branch at a time.

Worker chats:

- work only in the branch and scope currently assigned to their fixed chat name;
- never merge their own pull requests;
- never alter `main`;
- never select their own next roadmap task after finishing an assignment;
- never modify another workstream's branch or reserved domain unless the coordinator explicitly reassigns that work in `docs/CHAT-WORK-ASSIGNMENTS.md`.

The primary integration chat is `COORDENADOR - EliteSCADA` and owns cross-PR integration, merge ordering, shared-file changes and assignment updates.

## 2. Mandatory read protocol

Before any EliteSCADA work, every chat must read from the current GitHub `main`:

1. `PROJECT GOAL.md`
2. `LAST CHANGE.md`
3. `docs/ROADMAP.md`
4. `docs/PARALLEL-WORK.md`
5. `docs/CHAT-WORK-ASSIGNMENTS.md`
6. every workstream-specific document listed under that chat's `MustReadSpecific` assignment field

The repository must be inspected before editing. Branch, PR, head commit and CI state in GitHub are operational truth; documentation is coordination/handoff state and may briefly lag a just-completed GitHub action.

## 3. Permanent `siga` / `continue` rule

The canonical short command used by the user is:

`Siga`

For backward compatibility, `continue` has exactly the same meaning.

When the user sends only `siga` or `continue` in an EliteSCADA chat, the chat must:

1. identify itself by its fixed workstream/chat name;
2. reread the mandatory repository documents from current `main`;
3. locate its exact current assignment in `docs/CHAT-WORK-ASSIGNMENTS.md`;
4. verify the real assigned branch, PR/head and CI state in GitHub;
5. continue only the explicitly authorized task without asking the user to repeat the previous prompt.

Recognized fixed names currently include:

- `COORDENADOR - EliteSCADA`
- `DEV 1 - EliteSCADA`
- `DEV 2 - EliteSCADA`
- `DEV 3 - EliteSCADA`
- future names explicitly added to the assignment board by the coordinator

### Assigned or active work

If its assignment is `ASSIGNED`, `IN_PROGRESS`, `PR_OPEN` or `CI_FAILED`, the chat must verify the real GitHub state and automatically start/continue that exact task.

### Completed/delivered work

If the current task is delivered, waiting, completed or ready for coordinator review and `AfterCompletion` is `WAIT_FOR_COORDINATOR`, the worker must not create new work.

It should report substantially:

> Tarefa atual concluída/entregue. `WAIT_FOR_COORDINATOR` está ativo e não há nova tarefa autorizada para este DEV.

It must not:

- create a new branch;
- choose a new roadmap item;
- resume an older delivered PR as new work;
- modify `main`;
- modify another branch;
- expand its own mission.

If no new assignment exists, the worker should also make the operational handoff obvious to the user: the next coordination action is to open `COORDENADOR - EliteSCADA` and send `siga`. After the coordinator records a new assignment, the user may return to the DEV chat and send only `siga`.

### Explicit next assignment

If the assignment board contains a new current task, `TAKE_NEXT_ASSIGNED_TASK`, or `NEXT_TASK: <task>` with branch/scope information, `siga` means start or continue exactly that explicitly assigned work.

### Missing identity

If the current fixed chat name is not present in `docs/CHAT-WORK-ASSIGNMENTS.md`, the chat must not infer an assignment from the roadmap, conversation history, old branches or nearby PRs. It must report that no authorized assignment exists and wait for coordinator action.

## 4. Assignment authority

Only `COORDENADOR - EliteSCADA` may change assignments of DEV chats in `docs/CHAT-WORK-ASSIGNMENTS.md`.

Workers may update their own PR body with:

- `IMPLEMENTED IN PR` scope;
- tests/CI evidence;
- remaining integration hooks;
- `INTEGRATION REQUIRED` items;
- blockers found inside the assigned scope.

A worker must not modify the assignment board to create, broaden or replace its own mission.

## 5. Shared files reserved to coordinator

Unless an assignment explicitly grants a specific exception, worker chats must not modify:

- `PROJECT GOAL.md`
- `LAST CHANGE.md`
- `docs/ROADMAP.md`
- `docs/PARALLEL-WORK.md`
- `docs/CHAT-WORK-ASSIGNMENTS.md`
- `.github/workflows/**`
- top-level solution/project orchestration files when avoidable
- `src/Scada.Api/Program.cs`
- central application composition/DI files
- central frontend routing/application-shell files
- lockfiles
- `src/Scada.Engineering/Contracts/EngineeringContracts.cs`

Workers should prefer isolated files/types inside their assigned domain and record required central changes under `INTEGRATION REQUIRED` in the PR body.

The coordinator may explicitly grant a narrow exception to one of these paths in an assignment when a product-integration task genuinely requires it. Such an exception must name the files/domain and does not grant general ownership of other shared surfaces.

## 6. Conflict-avoidance rules

1. Prefer new isolated files over editing shared central files.
2. A worker may refactor only inside its assigned domain.
3. If a required change crosses into coordinator-owned scope and no explicit exception exists, implement the isolated side first and document the integration requirement.
4. Do not rename or move files owned by another active workstream.
5. Do not force/reset another workstream branch.
6. Do not merge/rebase another worker's branch without explicit coordinator reconciliation.
7. Worker PRs remain Draft until isolated implementation and focused validation are complete unless the coordinator deliberately changes that policy.
8. Worker PRs must identify changed domains, tests, remaining hooks and coordinator-owned integration requirements.
9. Final reconciled CI must be green before merge.
10. Worker chats never merge their own PRs.
11. An open feature branch/PR is **IMPLEMENTED IN PR**, never official product state.
12. If two PRs touch the same shared contract, the coordinator decides integration order and reconciles semantics deliberately.
13. A merged historical branch is not automatically a current assignment.

## 7. Integration rules

The coordinator must:

- inspect real PR diffs, tests and CI before integration;
- reconcile worker branches with then-current `main` without discarding valid worker commits;
- add central/shared integration hooks after reviewing worker `INTEGRATION REQUIRED` notes;
- resolve cross-PR contract duplication explicitly;
- run relevant final CI on reconciled heads;
- merge only green, reviewed work;
- update `docs/CHAT-WORK-ASSIGNMENTS.md` whenever a worker's task/status/next assignment materially changes;
- keep `LAST CHANGE.md` and `docs/ROADMAP.md` consistent with actual merged state;
- keep completed workers idle until a new assignment is explicitly recorded.

Roadmap dependency ordering remains governed by `PROJECT GOAL.md` and `docs/ROADMAP.md`; the assignment board does not rewrite product architecture.

## 8. Low-friction user coordination loop

The intended operational loop is deliberately small:

1. a DEV works until it reports a result/completion;
2. the user sends `siga` in `COORDENADOR - EliteSCADA`;
3. the coordinator verifies GitHub, integrates/reviews as needed and updates the DEV's assignment in `docs/CHAT-WORK-ASSIGNMENTS.md`;
4. the user returns to that DEV chat and sends only `siga`;
5. the DEV rereads the repository and starts the newly assigned work automatically.

The user should not need to copy technical task descriptions between chats after the bootstrap instruction has been installed once in each fixed DEV chat.

## 9. Document responsibilities

- `PROJECT GOAL.md` = stable product north and locked architecture.
- `docs/ROADMAP.md` = macro implementation sequence and dependencies.
- `docs/PARALLEL-WORK.md` = permanent concurrency/ownership/integration rules.
- `docs/CHAT-WORK-ASSIGNMENTS.md` = live assignment board: who is doing what **now**, branch/scope/status, and what `siga` means for each chat.
- `LAST CHANGE.md` = technical operational handoff and exact resume point.
- PR bodies = branch-local implementation evidence and `INTEGRATION REQUIRED` details.

No one of these documents replaces the others.

## 10. Status vocabulary

Repository/product state:

- **MERGED** — official `main` state;
- **IMPLEMENTED IN PR** — implementation exists only in an open feature branch/PR;
- **SPECIFIED / NOT IMPLEMENTED** — architecture/product intent is documented but functionality does not yet exist.

Assignment states include `ASSIGNED`, `IN_PROGRESS`, `PR_OPEN`, `CI_FAILED`, `READY_FOR_COORDINATOR_REVIEW`, `INTEGRATION_REQUIRED`, `MERGED`, `BLOCKED`, `WAITING` and `COMPLETED`.

Never describe an open branch as merged product state.