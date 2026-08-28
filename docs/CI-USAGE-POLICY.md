# CI USAGE POLICY — EliteSCADA

Status: **PERMANENT EXECUTION RULE WITH TEMPORARY BUDGET MODES**

This document governs how `COORDENADOR - EliteSCADA`, `DEV 1 - EliteSCADA`, `DEV 2 - EliteSCADA` and `DEV 3 - EliteSCADA` consume GitHub Actions while preserving the project's quality gates.

The objective is not to reduce validation quality. It is to avoid spending full CI matrices on intermediate states that do not need them.

## Current budget mode

**Mode:** `CONSTRAINED`  
**Reason:** GitHub Actions monthly included usage is near exhaustion.  
**Current remaining allowance reported by the product owner on 2026-08-28:** approximately **19 included minutes**.  
**Expected reset:** 2026-09-01, but return to normal mode only after the owner explicitly reports the reset.

Wave 06 is merged after exact-final-head CI #487 fully green. Wave 07 is now **development active with CI deferred**.

Until the owner reports reset, all EliteSCADA chats must treat GitHub Actions minutes as unavailable for Wave 07 validation. Code and tests may be written and committed, but no Wave 07 PR or workflow run should be intentionally created.

After the monthly reset, the coordinator may return the project to `NORMAL` mode by updating this document or the current handoff/assignment board. The permanent efficiency principles below remain valid even in NORMAL mode.

## Non-negotiable quality rule

CI economy must **never** be achieved by:

- weakening or deleting tests;
- relaxing security, CAS, lifecycle, persistence, Runtime or Python sandbox guards;
- ignoring a failing exact-head validation;
- merging known-failing work;
- declaring a worker or wave complete without the validation required by its Definition of Done;
- replacing required final integrated CI with reasoning alone.

The optimization target is **when and how often CI runs**, not what the final product must prove.

## Temporary Wave 07 no-Actions rule

Until the product owner explicitly reports that the Actions allowance has reset:

1. DEV 1, DEV 2 and DEV 3 may implement their assigned Wave 07 slices on the branches recorded in `docs/CHAT-WORK-ASSIGNMENTS.md`.
2. Required tests must still be written and committed.
3. **Do not open Wave 07 pull requests.** This repository's `pull_request` trigger runs the full CI workflow against `main`.
4. **Do not use `workflow_dispatch`, rerun jobs or otherwise trigger Wave 07 Actions.**
5. Do not push Wave 07 product code to `main`.
6. Source/static review and other evidence that does not consume GitHub Actions may be used during implementation.
7. Worker delivery is labeled `IMPLEMENTED / CI_DEFERRED` and returns to `WAIT_FOR_COORDINATOR`.
8. `IMPLEMENTED / CI_DEFERRED` is not equivalent to validated, complete, Ready or mergeable.
9. The coordinator may review and compose work on `integration/visual-runtime-wave-07`, but no final Wave 07 merge occurs before deferred CI debt is paid.
10. When the owner reports reset, normal worker/integration PR and CI validation resumes.

## Constrained-mode execution rules

### Workers — DEV 1 / DEV 2 / DEV 3

During `CONSTRAINED` mode:

1. Prefer local/static reasoning and focused tests during implementation.
2. Batch related edits before pushing instead of pushing every tiny correction separately.
3. Do not intentionally trigger a full Actions run merely to inspect an intermediate state.
4. Use the smallest validation that proves the changed domain when tooling permits and does not violate the active no-Actions rule.
5. If a later CI run fails with a localized and understood cause, fix that cause first. Do not rerun unchanged heads merely hoping for green.
6. Avoid cosmetic commits on worker branches unless required for delivery correctness.
7. Never merge worker work directly to `main`.
8. Never broaden the task to compensate for deferred CI.

### Coordinator

During `CONSTRAINED` mode:

1. Reuse valid exact-head CI evidence whenever the head has not changed semantically.
2. Do not rerun successful matrices for reassurance alone.
3. Review worker diffs/contracts before requesting expensive CI after reset.
4. Integrate accepted slices deliberately so the full matrix is spent on meaningful composition checkpoints.
5. When a full CI fails, inspect the exact failed job/log first; apply a targeted correction before another run.
6. Documentation-only coordination movement in `main` does not justify revalidating unchanged product heads.
7. Preserve the final wave gate: before merging any functional wave, the required integrated matrix must be green on the exact final product head.
8. If remaining Actions minutes become insufficient, pause validation/merge rather than weakening quality.
9. Do not spend the reported ~19 remaining minutes on Wave 07 while the temporary development-only rule is active.

## CI evidence hierarchy

Use the cheapest evidence sufficient for the current decision:

1. code/diff/contract inspection;
2. focused unit/component test when available without Actions;
3. focused build/backend/frontend validation;
4. worker exact-head CI after reset when needed;
5. full integration matrix at meaningful integration/final gates.

Higher-cost evidence is not automatically better if lower-cost evidence already answers the current question. Conversely, lower-cost evidence does not replace a required final wave matrix.

## Rerun policy

A rerun is justified when:

- the previous failure was demonstrably transient/infrastructure-only;
- the relevant product head did not change and evidence supports flakiness;
- a required job did not execute because of external runner/service failure.

A rerun is **not** justified merely because a product/test failure is inconvenient. Fix the cause first.

## Pull request policy

Normally Draft PRs are opened early for event-driven review.

**Temporary exception:** while Wave 07 CI is explicitly deferred, Wave 07 PRs must not be opened because the repository workflow runs on every `pull_request` to `main`. Branch-based development/review temporarily replaces Draft PRs until the owner reports reset.

After reset, restore the normal Draft PR review process and capture the deferred validation evidence.

## Final wave policy

`CONSTRAINED` mode does not change the Wave Definition of Done.

Before a wave merge to `main`, the coordinator still requires the final integrated validation matrix defined by the wave, normally:

- Web build;
- backend Release build;
- full relevant automated tests;
- Runtime smoke;
- Chromium E2E;
- wave-specific acceptance checks.

If the budget cannot support that matrix, the correct state is `IMPLEMENTED / CI_DEFERRED` or `BLOCKED_BY_CI_BUDGET`, not `MERGED`.

Wave 07's deferred CI remains debt to execute after owner confirmation of reset and before Wave 07 can satisfy its final gate.

## Wave 06 evidence note

Wave 06 final integration head `d665dc13b0922938a15252d9775ef6604e41bff4` passed CI #487 fully and was merged through PR #83 as `cc79713434c1d7b5988158b843b137eaf488d923`.

The automatic post-merge main push run #488 did not execute product steps: Web/Backend had empty step lists and no allocated runner; Chromium was skipped. The merge commit differs from the exact green product head only by Markdown documentation movement, so this does not represent a product regression.

## Normal mode

When Actions allowance is healthy and owner-confirmed, the project returns to `NORMAL` mode. Even then:

- avoid redundant unchanged-head reruns;
- batch trivial commits where practical;
- keep event-based reviews;
- reserve complete integration matrices for meaningful checkpoints;
- preserve exact-head final CI before merge.

CI minutes are a project resource. Use them to buy evidence, not ceremony.
