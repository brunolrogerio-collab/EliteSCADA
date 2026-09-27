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

The checkpoint above was committed from this separate evidence worktree as `a49527f4d2c15a2e2af245750f3d56f2cf570a27` and pushed. Immediately after the evidence commit, the runtime worktree remained at the candidate SHA and status remained `RUNNING` / `PREPARATION=READY` with the same dependency key.

## Post-checkpoint lifecycle checks

- First disposable reset completed as `NOT_STARTED` / `PREPARATION=READY`; the dependency image, volumes, and manifest remained valid.
- Second fresh start created session `12905718-9cfb-4174-b6df-243ce42bfa29`, reached healthy services, and served the Web root as HTTP 200. Its start output contained no package installation/restore; it reused the already-prepared image and volumes.
- A status check after the second start retained `PREPARATION=READY` and the exact same dependency key.
- Final reset completed. Final status is `NOT_STARTED` / `PREPARATION=READY` at candidate SHA `50a4451aa122f7f9fd0af98173c184f6623a147b`.
- No project was created, no product workflow was explored, and no Stage 1 attempt was started.
