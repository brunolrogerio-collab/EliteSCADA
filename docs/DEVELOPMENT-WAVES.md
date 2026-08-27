# Development Waves — EliteSCADA

Status: **PERMANENT COORDINATION RULE**  
Approved by product owner: 2026-08-27.

This document defines how `COORDENADOR - EliteSCADA`, `DEV 1 - EliteSCADA`, `DEV 2 - EliteSCADA` and `DEV 3 - EliteSCADA` organize parallel product development. It complements `docs/PARALLEL-WORK.md`; if older coordination text conflicts with this model, this document and the latest `main` state govern after deliberate reconciliation by the coordinator.

## Goal

The purpose of parallel work is not to keep three workers busy. It is to keep up to three independent streams producing work that can actually be integrated with minimal duplication, branch churn and CI waste.

The permanent safety model remains:

- GitHub branch/PR/head/CI state is operational truth;
- workers never modify `main` or merge their own PR;
- each worker has exactly one ACTIVE task;
- shared/central composition belongs to the coordinator unless explicitly delegated;
- workers do not choose their own next task;
- known-failing work is never merged;
- canonical Engineering, backend authorization, TAG quality, Audit and lifecycle boundaries may not be bypassed for parallelism.

## Development Wave

Every coordinated set of parallel product tasks belongs to an explicit Development Wave.

Each wave records:

- `Wave` identifier, e.g. `INTERFACE-WAVE-03`;
- product checkpoint/objective;
- immutable logical `WaveBaseSHA`;
- `IntegrationBranch`, normally `integration/<wave-name>`;
- participating tasks/workers;
- dependency map;
- reserved files/domains;
- coordinator-owned integration work;
- validation matrix;
- objective completion/gate;
- next product gate.

A wave is one product result divided into parallel slices, not three unrelated features grouped for convenience.

## Wave base rule

At wave start, the coordinator selects the exact `WaveBaseSHA` after closing the previous wave and confirming a healthy `main`.

Workers validate their slices against that logical base. The base does not move merely because administrative documentation in `main` changes.

During an active wave, avoid merging unrelated product changes into `main`, including:

- parallel research;
- non-urgent architecture documentation/code hardening;
- unrelated refactors;
- future protocol preparation;
- unrelated functional slices.

Such work may be prepared on branches but normally waits for the integration boundary.

Exceptions are limited to:

- critical defects;
- security fixes;
- CI/infrastructure defects blocking the wave;
- indispensable dependency discovered by the wave itself.

A documentation-only coordination update does not by itself invalidate a worker's `WaveBaseSHA` or force worker reconciliation.

## Integration Train

Preferred shape:

```text
main
  |
  +-- integration/<wave>          <- COORDENADOR
        +-- worker slice DEV 1
        +-- worker slice DEV 2
        +-- worker slice DEV 3
```

Workers remain on their own branches and PRs. Their job is to prove `WaveBase + worker slice`.

The coordinator integrates accepted worker work into the wave integration branch, performs shared hooks/composition there, reconciles the integration branch with real `main` when needed and proves the final composition once.

### Worker CI

A delivered worker must have the validation required by its assignment green on the exact worker head built from the official wave base.

A worker is **not** automatically required to merge every unrelated newer `main` commit into its branch merely because `main` advanced.

### Coordinator CI

Before the wave can merge, the final integrated composition must pass the complete required matrix, normally:

- Web build;
- backend Release build;
- full relevant automated tests;
- runtime smoke;
- Chromium end-to-end;
- additional wave-specific acceptance checks.

If integration reveals a semantic conflict, only the affected worker is returned for a targeted correction when that is safer than coordinator integration.

## Definition of Ready

A task may be promoted to READY/ACTIVE only when the coordinator can answer:

1. What product problem does it solve?
2. Which current gate does it advance?
3. Are required dependencies already available at the WaveBaseSHA?
4. What branch will be used?
5. What exact BaseSHA applies?
6. What files/domains are allowed?
7. What files/domains are forbidden/reserved?
8. Is it genuinely parallel-safe with the other active slices?
9. What central integration remains coordinator-owned?
10. How will the slice be validated?
11. What objective condition defines completion?
12. What happens after delivery?

If these answers are materially unknown, the task remains QUEUED and is not executable.

## Assignment fields

`docs/CHAT-WORK-ASSIGNMENTS.md` should use, where relevant:

- `Wave`
- `CurrentTask`
- `Branch`
- `Status`
- `BaseSHA`
- `StartCondition`
- `DependsOn`
- `ParallelSafeWith`
- `Objective`
- `AllowedScope`
- `ForbiddenScope`
- `ReservedFiles`
- `IntegrationRequired`
- `IntegrationTarget`
- `ValidationMatrix`
- `CompletionCriteria`
- `AfterCompletion`
- `NextQueuedTask`
- `NextStartCondition`

The assignment board is an execution mechanism, not merely historical documentation, and should track GitHub state promptly.

## Queue semantics

Future work may be prepared one wave ahead, but queue presence is not authorization to start.

Use three states:

- `QUEUED` — selected as likely future work but not authorized;
- `READY` — Definition of Ready satisfied and start condition satisfied;
- `ACTIVE` — explicitly assigned current work.

A worker receiving `siga` starts only its current authorized assignment. A `NextQueuedTask` never permits speculative execution unless the coordinator promotes it according to the board and start condition.

## Preferred specialization

Specialization is a preference, never rigid ownership.

### DEV 1

Preferred context: Engineering, configuration, lifecycle, editors, import/export.

### DEV 2

Preferred context: Runtime, TAGs, historian, source/runtime behavior, operations.

### DEV 3

Preferred context: cross-product acceptance, security/session, Audit, UX quality and validation.

### Coordinator

Preferred ownership:

- central contracts;
- `Program.cs` and DI/composition;
- canonical Engineering schema/model changes;
- `EngineeringApp.tsx`;
- `main.tsx`;
- routing/shell;
- cross-domain integration;
- integration branches;
- merges;
- official roadmap/assignment/handoff documentation.

The coordinator may redistribute work whenever dependency or parallel-safety analysis warrants it.

## Wave lifecycle

### A. Synchronize

Coordinator checks actual `main`, PRs, branches, CI, assignment board, roadmap and handoff. GitHub wins over stale documentation for operational state.

### B. Close Previous Wave

Before opening another functional wave:

- worker PRs are merged, rejected or explicitly abandoned;
- coordinator integration is complete;
- integrated CI is green;
- merged product state is understood;
- official documentation is synchronized.

Do not open three new functional branches while central integration of the previous wave remains incomplete.

### C. Select Product Checkpoint

Choose one user/product outcome, then derive up to three independent slices from it.

### D. Dependency Map

Classify candidate tasks as:

- executable now;
- parallel-safe now;
- waiting on dependency;
- research only.

Prefer work that advances the current product gate, unlocks downstream work, gives validation value, reuses existing contracts and remains isolated.

Penalize tasks that unnecessarily touch shared central files or invent new architecture during a product wave.

## Review checkpoints

Do not use artificial percentage accounting. Use event-driven checkpoints:

### Early Contract Review

As soon as a Draft PR has enough structure to reveal architecture, inspect changed files, contracts/APIs used, scope and authority boundaries.

### Integration Review

When the slice's interfaces and required hooks are known, inspect overlap, central integration requirements and possible semantic conflicts with sibling work.

### Delivery Review

At delivery, inspect exact diff, tests/CI, PR body, `INTEGRATION REQUIRED`, changed domains and objective completion.

Workers should open Draft PRs early enough for these reviews to be meaningful.

## Worker Definition of Done

A worker delivery requires:

- assigned functional scope complete;
- focused tests;
- assignment validation matrix green on exact worker head;
- exact head recorded;
- changed files/domains described;
- no change outside AllowedScope;
- PR body distinguishes `IMPLEMENTED IN PR`, `INTEGRATION REQUIRED` and `SPECIFIED / NOT IMPLEMENTED`;
- no permanent architectural decision exists only in the worker branch;
- worker returns to `WAIT_FOR_COORDINATOR`.

## Wave Definition of Done

A Development Wave is complete only when:

1. all approved slices are in the integration composition;
2. coordinator-owned central hooks are implemented;
3. obsolete duplicate paths are removed;
4. required cross-product tests are present;
5. final Web build passes;
6. final backend/tests pass;
7. final runtime smoke passes;
8. final Chromium acceptance passes;
9. integration branch is merged to `main`;
10. post-merge `main` is healthy;
11. roadmap, assignment board and handoff match reality;
12. the next wave/gate is selected or explicitly paused.

## Permanent `siga` behavior

### Worker

On `siga`:

1. reread mandatory current-main documents;
2. locate its exact board assignment;
3. check StartCondition/status;
4. verify real branch/PR/head/CI;
5. execute only ACTIVE/authorized work;
6. deliver and stop at `WAIT_FOR_COORDINATOR`.

### Coordinator

On `siga`:

1. reread mandatory documents;
2. inspect real GitHub state;
3. reconcile stale documentation mentally and then in the repository;
4. identify current wave phase;
5. review/integrate delivered work;
6. close the wave before opening the next functional wave;
7. update assignments and continuity documents promptly.

## Anti-patterns

Avoid:

- assigning arbitrary work merely because a DEV is idle;
- moving `main` repeatedly with unrelated product changes during a worker wave;
- requiring all three workers to reconcile individually after unrelated `main` movement;
- allowing multiple workers to edit central composition files without deliberate exception;
- beginning production protocols merely because research is complete;
- leaving the assignment board one execution cycle behind GitHub;
- treating a worker CI as proof of the final integrated product.

The coordinator's role is scheduler + architect + integrator. The workers are slices of one small team, not independent roadmaps.