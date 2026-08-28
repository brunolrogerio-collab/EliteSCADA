# PARALLEL WORK — EliteSCADA

This file defines the permanent concurrent-work safety rules. The detailed Development Wave execution model is authoritative in `docs/DEVELOPMENT-WAVES.md` and must be read with this file. GitHub Actions consumption rules are defined in `docs/CI-USAGE-POLICY.md`.

## 1. Core ownership

Each worker chat owns exactly one ACTIVE assignment/branch at a time.

Workers:

- never alter `main`;
- never merge their own PR;
- never choose or broaden their own mission;
- never work in another DEV branch;
- obey AllowedScope/ForbiddenScope/ReservedFiles;
- stop at `WAIT_FOR_COORDINATOR` after delivery.

`COORDENADOR - EliteSCADA` owns assignments, cross-domain architecture, central composition, integration branches, merge ordering, official documentation and final integration CI.

## 2. Mandatory read protocol

Before any EliteSCADA action, every fixed chat reads current `main`:

1. `PROJECT GOAL.md`;
2. `LAST CHANGE.md`;
3. `docs/ROADMAP.md`;
4. `docs/PARALLEL-WORK.md`;
5. `docs/DEVELOPMENT-WAVES.md`;
6. `docs/CHAT-WORK-ASSIGNMENTS.md`;
7. `docs/CI-USAGE-POLICY.md`;
8. every current `MustReadSpecific` document.

For product planning through first owner validation, coordinator and relevant workers also read `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`.

GitHub branch/PR/head/CI state is operational truth. Documentation is coordination truth and must be synchronized promptly when it lags.

## 3. Permanent `siga` / `continue`

When the user sends only `siga` or `continue`, the chat:

1. identifies its fixed role;
2. performs the mandatory read protocol;
3. locates its exact assignment;
4. verifies branch/PR/head/CI and wave state;
5. checks StartCondition/status;
6. continues only explicitly authorized work without asking the user to repeat old prompts.

Workers with delivered work and `WAIT_FOR_COORDINATOR` do not create new branches or start queued work. A `NextQueuedTask` is not authorization until promoted according to the board and `docs/DEVELOPMENT-WAVES.md`.

## 4. Development Waves

Parallel product development is organized into explicit waves with:

- one product checkpoint;
- immutable logical `WaveBaseSHA`;
- up to three parallel-safe worker slices;
- coordinator integration branch;
- reserved/shared ownership;
- validation matrix;
- objective wave gate.

The wave base is not invalidated merely by coordination/documentation-only commits.

During an active wave, avoid merging unrelated product/research/refactor work into `main`. Critical/security/CI-blocking/indispensable dependency fixes are exceptions.

Detailed rules, Definition of Ready/Done, queue semantics and review checkpoints are in `docs/DEVELOPMENT-WAVES.md`.

## 5. Integration Train

Workers prove `WaveBaseSHA + worker slice` on their own Draft PRs. They are not automatically required to reconcile individually with every unrelated newer `main` commit.

The coordinator integrates accepted slices into `integration/<wave>`, implements central hooks, reconciles the integrated composition with real `main` where needed and runs final complete CI there.

If a semantic conflict is isolated to one worker, the coordinator returns only that worker for targeted correction when appropriate.

Final wave quality remains strict: no wave merge without green integrated validation required by its matrix, normally including Web build, backend build/tests, runtime smoke and Chromium E2E.

## 6. GitHub Actions budget discipline

`docs/CI-USAGE-POLICY.md` governs CI frequency for both workers and coordinator.

When its mode is `CONSTRAINED`:

- workers batch coherent implementation changes and prefer focused validation during iteration;
- opening/updating a Draft PR does not mean every intermediate commit requires a full workflow matrix;
- unchanged-head reruns for reassurance are not allowed;
- a localized CI failure must be diagnosed and corrected before another expensive full run, except for demonstrably transient infrastructure failures;
- coordinator reuses valid exact-head evidence and reserves complete matrices for meaningful integration/final checkpoints;
- documentation-only `main` changes do not invalidate unchanged product CI evidence;
- final integrated Wave Definition of Done remains unchanged;
- if available Actions minutes cannot support the required final matrix, the correct state is `BLOCKED_BY_CI_BUDGET`, never a weakened merge.

CI economy changes **frequency**, not assertions, security, CAS, lifecycle, persistence, Runtime guards or final acceptance requirements.

## 7. Shared files reserved to coordinator

Unless an assignment grants a narrow explicit exception, workers do not modify:

- `PROJECT GOAL.md`;
- `LAST CHANGE.md`;
- `docs/ROADMAP.md`;
- `docs/PARALLEL-WORK.md`;
- `docs/DEVELOPMENT-WAVES.md`;
- `docs/CI-USAGE-POLICY.md`;
- `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`;
- `docs/CHAT-WORK-ASSIGNMENTS.md`;
- `.github/workflows/**`;
- central solution/orchestration/DI files;
- `src/Scada.Api/Program.cs`;
- central frontend routing/shell/composition files;
- lockfiles;
- canonical Engineering contract/schema files such as `src/Scada.Engineering/Contracts/EngineeringContracts.cs`.

Workers prefer isolated files/types and record required central changes in PR `INTEGRATION REQUIRED` notes.

## 8. Worker PR requirements

Draft PRs are opened early enough for event-driven reviews:

- Early Contract Review;
- Integration Review;
- Delivery Review.

Worker delivery requires focused tests/assigned CI, exact head evidence appropriate to the active CI budget mode, changed-domain description, no scope violation and a PR body separating:

- `IMPLEMENTED IN PR`;
- `INTEGRATION REQUIRED`;
- `SPECIFIED / NOT IMPLEMENTED`.

No permanent architectural decision may live only in a worker branch.

Under `CONSTRAINED` CI mode, a worker may reach coordinator review with focused evidence when the board/policy permits deferring the complete matrix to integration. This never authorizes merging a known-failing worker head or bypassing a specifically required worker validation.

## 9. Assignment authority and queue

Only the coordinator changes worker missions in `docs/CHAT-WORK-ASSIGNMENTS.md`.

Future work may be preplanned, using:

- `QUEUED`;
- `READY`;
- `ACTIVE`.

A worker starts only ACTIVE/explicitly authorized work whose StartCondition is satisfied. Queue preparation exists to reduce idle coordination, not to grant autonomy over roadmap selection.

## 10. Preferred specialization

Preferences, not rigid ownership:

- DEV 1: Engineering/configuration/lifecycle/editors/import-export;
- DEV 2: Runtime/TAGs/historian/source-runtime/operations;
- DEV 3: cross-product acceptance/security/session/Audit/UX quality;
- Coordinator: central contracts/schema/DI/routing/shell/composition/integration/merges/official docs.

Coordinator may redistribute work when dependency or parallel-safety analysis requires it.

## 11. Status vocabulary

Product/repository:

- `MERGED` — official `main` state;
- `IMPLEMENTED IN PR` — exists only in an open branch/PR;
- `RESEARCH MERGED / PRODUCTION NOT IMPLEMENTED` — architecture/evidence is official but no production capability is implied;
- `SPECIFIED / NOT IMPLEMENTED` — locked intent with no merged implementation.

Execution states may include `QUEUED`, `READY`, `ACTIVE`, `IN_PROGRESS`, `PR_OPEN`, `CI_FAILED`, `READY_FOR_COORDINATOR_REVIEW`, `INTEGRATION_REQUIRED`, `WAIT_FOR_COORDINATOR`, `BLOCKED`, `BLOCKED_BY_CI_BUDGET`, `MERGED` and `COMPLETED`.

Never describe an open branch as merged product state.

## 12. Document responsibilities

- `PROJECT GOAL.md` = long-lived architecture/product north;
- `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md` = locked first owner-validation product scope and ordered waves;
- `docs/ROADMAP.md` = macro current implementation order/status;
- `docs/DEVELOPMENT-WAVES.md` = permanent scheduling/integration model;
- `docs/PARALLEL-WORK.md` = concurrent safety/ownership rules;
- `docs/CI-USAGE-POLICY.md` = CI budget modes, evidence hierarchy and rerun discipline;
- `docs/CHAT-WORK-ASSIGNMENTS.md` = live execution board;
- `LAST CHANGE.md` = exact operational handoff;
- PR bodies = branch-local delivery evidence.

No one document replaces the others.