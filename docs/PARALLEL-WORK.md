# PARALLEL WORK — EliteSCADA

This file coordinates concurrent ChatGPT workstreams on the same repository.

## Core rule

Each chat owns exactly one branch/workstream. Worker chats must not merge their own pull requests. Integration and shared-file changes are coordinated by the primary integration chat.

Before any EliteSCADA work, every chat must read:

1. `PROJECT GOAL.md`
2. `LAST CHANGE.md`
3. `docs/ROADMAP.md`
4. this file
5. the specification documents relevant to its workstream

## Active workstreams

### Coordinator / integration chat

PR #39 local-user administration is **MERGED**. The coordinator now owns the next isolated hardening slice plus cross-PR integration.

Current responsibilities:

- implement secured Engineering Apply/Delete/bulk mutation on a separate coordinator branch;
- review Worker A / Worker B PRs after their CI completes;
- reconcile worker branches with then-current `main` without discarding worker commits;
- perform shared Engineering/runtime integration explicitly marked `INTEGRATION REQUIRED` by workers;
- decide merge order and run final integrated CI;
- maintain `PROJECT GOAL.md`, `LAST CHANGE.md`, `docs/ROADMAP.md` and this coordination file.

Coordinator-owned domains currently include:

- shared Engineering mutation lifecycle and public schema integration;
- `src/Scada.Api/Security/**` and authentication/session boundaries;
- `src/Scada.Api/Realtime/TagRealtimeHub.cs`;
- central Engineering/Runtime composition and API wiring;
- shared frontend routing/application-shell integration.

### Worker A — Internal Memory foundation

Branch: `feature/internal-memory-foundation`
Draft PR: #40

Goal: implement the isolated foundation for the locked Internal Memory / Source Provider architecture without broad application integration.

Current Worker A foundation includes:

- common Source Provider abstraction for non-network TAG ownership;
- `builtin.memory.server` and `builtin.memory.client` provider contracts;
- typed initial/default values;
- stable TAG-ID retention identity;
- retention-store abstraction plus deterministic in-memory implementation;
- fail-closed incompatible retained data types;
- deleted TAG non-resurrection;
- normal memory quality `Good` without fabricated network metrics;
- per-Runtime-Client Client Memory isolation;
- focused tests.

Read first: `docs/INTERNAL-MEMORY-TAGS.md`.

Worker A must leave public Engineering schema, runtime application composition, durable production retention, historian/alarm integration and shared authorization/audit hooks as `INTEGRATION REQUIRED` unless coordinator explicitly assigns them.

### Worker B — Python scripting + visual property foundation

Branch: `feature/python-scripting-foundation`
Draft PR: #41

Goal: implement the isolated contracts/foundation required before the graphical Screens/Popups/Dynamos editor.

Current Worker B foundation includes:

- typed public visual-property schema;
- common geometry/transform/visibility/fill/stroke/text/image property groups;
- explicit object-type-specific declaration;
- Engineering base values separated from Runtime presentation overrides;
- deterministic `base -> binding/expression -> script -> animation` precedence;
- animation/tween contracts;
- explicit Client Visual Script vs Server Script scopes;
- sandbox capability contracts;
- execution budget/cancellation/queue/error-isolation contracts;
- Python validation/diagnostic contracts;
- focused tests.

Read first: `docs/PYTHON-SCRIPTING-AND-VISUAL-RUNTIME.md`.

Worker B must not implement the final graphical editor, concrete central Engineering schema wiring, central runtime composition or a private browser-only source of truth.

## Shared files reserved to coordinator

Worker chats must not modify these unless the coordinator explicitly assigns a specific change:

- `PROJECT GOAL.md`
- `LAST CHANGE.md`
- `docs/ROADMAP.md`
- `docs/PARALLEL-WORK.md`
- `.github/workflows/**`
- top-level solution/project orchestration files when avoidable
- `src/Scada.Api/Program.cs`
- central application composition/DI files
- central frontend routing/application-shell files
- lockfiles
- `src/Scada.Engineering/Contracts/EngineeringContracts.cs`

Worker chats should prefer new isolated files/types and record required central changes in the PR body under `INTEGRATION REQUIRED`.

## Conflict-avoidance rules

1. Prefer new files over editing shared central files.
2. A worker may refactor only inside its assigned domain.
3. If a needed change crosses into a coordinator-owned file, implement the isolated side first and record `INTEGRATION REQUIRED` rather than editing the shared file.
4. Do not rename/move files owned by another workstream.
5. Do not force/reset another workstream branch.
6. Do not merge `main` into a worker branch while another change is being committed to that branch unless the worker/coordinator explicitly coordinates the reconciliation.
7. Each worker PR remains Draft until the isolated implementation and focused tests are complete.
8. Each worker PR must list changed domains, tests, remaining integration hooks and coordinator-owned changes still required.
9. CI must be green on the final reconciled head before merge.
10. Worker chats never merge their own PRs.
11. An open worker PR is **IMPLEMENTED IN PR**, never product state.

## Current integration order

1. PR #39 is complete and merged.
2. Let current Worker A / Worker B CI runs finish without branch interference.
3. Coordinator may continue the independent secured Engineering mutation slice in parallel.
4. Review worker PRs and their `INTEGRATION REQUIRED` items.
5. Reconcile the smaller/cleaner ready worker PR with then-current `main` first.
6. Add only the necessary coordinator-owned integration hooks, run full relevant CI and merge when green.
7. Reconcile the remaining worker PR again against updated `main`, integrate hooks, validate and merge.
8. Internal Memory must reach full product integration before TAG Gateway starts.
9. Python/visual foundation must be integrated before the final graphical editor starts, but it does not block the earlier interface-validation preview after driver diagnostics.

Internal Memory remains earlier in the operational roadmap than TAG Gateway. Python/visual-property foundation may progress concurrently because it targets a later graphical-editor dependency and is isolated from Internal Memory when these ownership rules are respected.

## Status vocabulary

Every chat must distinguish:

- **MERGED** — official `main` state;
- **IMPLEMENTED IN PR** — code exists only on an open feature branch/PR;
- **SPECIFIED / NOT IMPLEMENTED** — architecture is locked but functionality does not yet exist.

Never describe an open branch as product state.
