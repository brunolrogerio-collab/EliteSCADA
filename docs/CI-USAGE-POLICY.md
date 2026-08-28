# CI USAGE POLICY — EliteSCADA

Status: **PERMANENT EXECUTION RULE WITH TEMPORARY BUDGET MODES**

This document governs how `COORDENADOR - EliteSCADA`, `DEV 1 - EliteSCADA`, `DEV 2 - EliteSCADA` and `DEV 3 - EliteSCADA` consume GitHub Actions while preserving the project's quality gates.

The objective is not to reduce validation quality. It is to avoid spending full CI matrices on intermediate states that do not need them.

## Current budget mode

**Mode:** `CONSTRAINED`  
**Reason:** GitHub Actions monthly included usage is near exhaustion.  
**Current remaining allowance reported by the product owner on 2026-08-28:** approximately 50 included minutes.  
**Expected reset:** 2026-09-01.

Until the reset, all EliteSCADA chats must treat GitHub Actions minutes as a scarce operational resource.

Wave 06 has priority over the remaining allowance because it still requires its exact-final-head integration gate before merge. Do not spend the remaining allowance on Wave 07 CI while Wave 06 is open.

After Wave 06 is merged, Wave 07 implementation may proceed if explicitly promoted by the coordinator, but GitHub Actions execution for Wave 07 is temporarily deferred until the product owner explicitly reports that the Actions allowance has reset. Wave 07 code may be implemented, reviewed and supported by static/focused evidence that does not consume Actions, but it must be labeled `IMPLEMENTED / CI_DEFERRED` or an equivalent explicit non-final state. It must not be called validated, complete or merge-ready on the basis of deferred CI.

After the monthly reset, the coordinator may return the project to `NORMAL` mode by updating this document or the current handoff/assignment board. The permanent efficiency principles below remain valid even in NORMAL mode.

## Non-negotiable quality rule

CI economy must **never** be achieved by:

- weakening or deleting tests;
- relaxing security, CAS, lifecycle, persistence or Runtime guards;
- ignoring a failing exact-head validation;
- merging known-failing work;
- declaring a worker or wave complete without the validation required by its Definition of Done;
- replacing required final integrated CI with reasoning alone.

The optimization target is **when and how often CI runs**, not what the final product must prove.

## Constrained-mode execution rules

### Workers — DEV 1 / DEV 2 / DEV 3

During `CONSTRAINED` mode:

1. Prefer local/static reasoning and focused tests during implementation.
2. Batch related edits before pushing instead of pushing every tiny correction separately.
3. Do not intentionally trigger a full Actions run merely to inspect an intermediate Draft state.
4. Draft PRs remain useful for review, but a green full matrix is not required after every intermediate commit unless the assignment explicitly says otherwise.
5. Use the smallest validation that proves the changed domain when tooling permits.
6. If CI fails with a localized and understood cause, fix that cause first. Do not rerun unchanged heads simply hoping for green.
7. Avoid cosmetic/documentation-only commits on worker branches while Actions usage is constrained unless required for delivery correctness.
8. Before declaring `READY_FOR_COORDINATOR_REVIEW`, provide focused evidence and identify whether a full exact-head workflow has already run. Do not manufacture another run if equivalent evidence already exists and the coordinator can safely defer full validation to integration.
9. Never merge the worker PR. Return to `WAIT_FOR_COORDINATOR` after delivery as usual.
10. During the temporary Wave 07 CI deferral, do not trigger Actions merely to validate Wave 07 worker branches. Preserve tests in code and record validation as deferred until the owner reports the allowance reset.

### Coordinator

During `CONSTRAINED` mode:

1. Reuse valid exact-head CI evidence whenever the head has not changed semantically.
2. Do not rerun successful matrices for reassurance alone.
3. Review worker diffs/contracts before requesting expensive full CI.
4. Prefer Early Contract Review and focused validation before integrating obviously incomplete work.
5. Integrate accepted slices deliberately so the full matrix is spent on meaningful composition checkpoints, not every merge step.
6. When a full CI fails, inspect the exact failed job/log first; apply a targeted correction before another full run.
7. Documentation-only coordination movement in `main` does not justify revalidating unchanged product heads.
8. Preserve the final wave gate: before merging the wave integration PR to `main`, the required integrated matrix must still be green on the exact final product head.
9. If remaining Actions minutes become insufficient for the required final matrix, pause the final merge rather than weakening validation. Development/review may continue with focused evidence until the allowance resets or billing policy deliberately changes.
10. Until the owner reports the allowance reset, reserve the remaining approximately 50 minutes for Wave 06 final validation. Do not run Wave 07 Actions.

## CI evidence hierarchy

Use the cheapest evidence that is sufficient for the current decision:

1. code/diff/contract inspection;
2. focused unit/component test for the changed domain;
3. focused build or backend/frontend validation;
4. worker exact-head CI when needed for delivery confidence;
5. full integration matrix at meaningful integration/final gates.

Higher-cost evidence is not automatically better if lower-cost evidence already answers the current question. Conversely, lower-cost evidence does not replace a required final wave matrix.

## Rerun policy

A rerun is justified when:

- the previous failure was demonstrably transient/infrastructure-only;
- the relevant product head did not change and evidence supports flakiness;
- a required job did not execute because of external runner/service failure.

A rerun is **not** justified merely because a product/test failure is inconvenient. Fix the cause first.

## Draft PR policy

Opening a Draft PR early remains required for event-driven review. Draft status does not mean every commit deserves a complete CI expenditure.

Workers should group implementation into coherent checkpoints. Coordinator reviews may happen against Draft PRs even when only focused validation exists, provided no one mislabels that state as final delivery evidence.

## Final wave policy

`CONSTRAINED` mode does not change the Wave Definition of Done.

Before a wave merge to `main`, the coordinator still requires the final integrated validation matrix defined by the wave, normally:

- Web build;
- backend Release build;
- full relevant automated tests;
- Runtime smoke;
- Chromium E2E;
- wave-specific acceptance checks.

If the budget cannot support that matrix, the correct state is `BLOCKED_BY_CI_BUDGET`, not `MERGED`.

The temporary Wave 07 development-only period does not waive this rule. Deferred Wave 07 CI remains a debt to be executed after the owner confirms the Actions reset and before Wave 07 can satisfy its final gate.

## Normal mode

When Actions allowance is healthy, the project returns to `NORMAL` mode. Even then:

- avoid redundant unchanged-head reruns;
- batch trivial commits where practical;
- keep event-based reviews;
- reserve complete integration matrices for meaningful checkpoints;
- preserve exact-head final CI before merge.

CI minutes are a project resource. Use them to buy evidence, not ceremony.