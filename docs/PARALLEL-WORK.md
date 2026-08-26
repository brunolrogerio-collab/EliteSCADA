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

Branch/PR: existing `feature/local-user-administration` / PR #39 until merged, then integration work from `main`.

Current responsibility:

- finish and validate local user administration;
- own integration decisions between concurrent workstreams;
- reconcile/rebase worker branches after other merges;
- perform final cross-feature CI validation and merge order;
- maintain `PROJECT GOAL.md`, `LAST CHANGE.md`, `docs/ROADMAP.md` and this coordination file.

Primary ownership while PR #39 is active:

- `src/Scada.Api/Security/**`
- `src/Scada.Security/**`
- authentication/session/revocation logic
- `src/Scada.Api/Realtime/TagRealtimeHub.cs`
- user administration UI files

### Worker A — Internal Memory foundation

Branch: `feature/internal-memory-foundation`

Goal: implement the isolated foundation for the locked Internal Memory / Source Provider architecture without wiring broad application integration yet.

Required scope:

- common Source Provider abstraction where needed for non-network TAG ownership;
- `builtin.memory.server` runtime provider foundation;
- typed initial/default values;
- stable TAG-ID retention identity semantics;
- retention-store abstraction plus deterministic in-memory implementation suitable for tests;
- incompatible data-type changes fail closed instead of silent coercion;
- deleted TAGs are not resurrected from stale retained state;
- normal memory quality is `Good`, with no fabricated communication/reconnect/network metrics;
- design/test contract for `builtin.memory.client` scope and ownership, without pretending it is one global server scalar;
- focused automated tests for the above.

Read first: `docs/INTERNAL-MEMORY-TAGS.md`.

Prefer adding new isolated files/classes. Do not perform broad API/UI wiring in this workstream.

### Worker B — Python scripting + visual property foundation

Branch: `feature/python-scripting-foundation`

Goal: implement the isolated contracts/foundation that must exist before the graphical Screens/Popups/Dynamos editor.

Required scope:

- typed public visual-property schema;
- common properties such as x/y, width/height, rotation, visibility, opacity, z-order, fill/background, stroke/line color, stroke/line width, text/font and image/resource properties where applicable;
- explicit object-type-specific property declaration;
- separation of Engineering base values from Runtime presentation overrides;
- deterministic property-layer/precedence model for base value, binding/expression, script override and animation override;
- animation/tween request contracts including duration/easing/repeat/ping-pong/cancel semantics;
- script entity/runtime contracts for Client Visual Script vs Server Script scopes;
- sandbox capability surface contracts that do not expose arbitrary OS/filesystem/network/database/driver/secrets access;
- execution budget/cancellation/error-isolation contracts;
- syntax validation/diagnostic contracts needed by a later Python editor;
- focused automated tests for property precedence, scope boundaries and validation.

Read first: `docs/PYTHON-SCRIPTING-AND-VISUAL-RUNTIME.md`.

This workstream must NOT implement the final graphical editor yet and must NOT create a private browser-only source of truth.

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

`src/Scada.Engineering/Contracts/EngineeringContracts.cs` is also coordinator-owned during parallel work because multiple features may eventually need schema expansion. Worker chats should prefer new contract files/types and document any required integration change instead of editing this central file directly.

## Conflict-avoidance rules

1. Prefer new files over editing shared central files.
2. A worker may refactor only inside its assigned domain.
3. If a needed change crosses into a coordinator-owned file, implement the isolated side first and record an `INTEGRATION REQUIRED` note in the PR body rather than editing the shared file.
4. Do not rename/move files owned by another workstream.
5. Do not merge `main` into a worker branch while another feature is mid-integration unless the coordinator directs it.
6. Each worker opens a Draft PR after the first meaningful, buildable slice.
7. Each worker PR must list changed domains, tests, remaining integration hooks, and any coordinator-owned file changes still required.
8. CI must be green on the final reconciled head before merge.
9. Worker chats never merge their own PRs.

## Integration order

Current preferred order:

1. finish and merge PR #39 user administration;
2. rebase/reconcile Worker A and Worker B onto the new `main`;
3. integrate the smaller/cleaner worker PR first;
4. rebase the remaining worker PR again onto updated `main`;
5. perform full relevant CI and integration smoke tests;
6. merge only after shared-file hooks are reviewed by the coordinator.

Internal Memory remains earlier in the operational roadmap than TAG Gateway. Python/visual-property foundation can progress concurrently because it is a prerequisite for the later graphical editor and is architecturally isolated from Internal Memory when the shared-file rules above are respected.

## Status vocabulary

Every chat must distinguish:

- **MERGED** — official `main` state;
- **IMPLEMENTED IN PR** — code exists only on an open feature branch/PR;
- **SPECIFIED / NOT IMPLEMENTED** — architecture is locked but functionality does not yet exist.

Never describe an open branch as product state.
