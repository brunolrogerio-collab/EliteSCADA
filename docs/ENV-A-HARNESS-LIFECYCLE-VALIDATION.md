# Environment A harness lifecycle validation

This is infrastructure-only evidence for coordinator route `ROUTE-SEQUENTIAL-CODEX-ENV-A-REPAIR-FINAL-LIFECYCLE-77`. Attempt 1 remains sealed/inconclusive. This validation does not start a new Stage 1 journey and records no product findings.

## Runtime identity

- Product base: `1f14a57491805a5d976bc9d0bf51393cf1b3ebcd`
- Accepted repair candidate: `preview/w15-first-project-env-harness-repair`
- Candidate SHA/tree: `50a4451aa122f7f9fd0af98173c184f6623a147b` / `5efeafb08725a90ce0c3df689b8d613f165bb569`
- Dependency key: `5982e0109dde9eb43dfcb9734d004671c2e8d0bfdc8557bce51a03be0347aa1e`
- Disposable session: `aec97c73-173e-4339-a41d-e77a223ae958`

## Completed checks at evidence-worktree checkpoint

- Fresh disposable start reached healthy Web and TimescaleDB services; the Web root returned HTTP 200 with `text/html`.
- Two consecutive status checks reported `PREPARATION=READY` and the same dependency key.
- Harness pause reported `PAUSED_RESUMABLE`; resume returned `RUNNING` with the same session ID and key.
- A second clean pause was performed before restarting Docker Desktop. The engine stopped with zero running containers, restarted, and reported version `29.8.0`.
- After engine recovery, status still reported `PAUSED_RESUMABLE` / `PREPARATION=READY`; resume returned the same session to `RUNNING` with the same key.
- No project was created, no product workflow was explored, and no Stage 1 attempt was started.

This file is committed from the separate evidence worktree while the runtime worktree remains at the exact candidate SHA. Runtime status is checked immediately after that evidence commit.

## Remaining lifecycle checks

Reset the disposable session while preserving preparation, then perform one second fresh start without package installation/restore, perform the final reset, and verify the final `NOT_STARTED` / `PREPARATION=READY` state plus static/diff/worktree cleanliness.
