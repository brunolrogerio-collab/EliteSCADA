# CI USAGE POLICY — EliteSCADA

Status: **PERMANENT EXECUTION RULE WITH TEMPORARY BUDGET MODES**

This document governs how `COORDENADOR - EliteSCADA`, `DEV 1 - EliteSCADA`, `DEV 2 - EliteSCADA` and `DEV 3 - EliteSCADA` consume GitHub Actions while preserving the project's quality gates.

The objective is not to reduce validation quality. It is to avoid spending full CI matrices on intermediate states that do not need them.

## Current budget mode

**Mode:** `NORMAL`  
**Owner report:** on 2026-08-28 the product owner reported a GitHub configuration change expected to provide approximately **1000 additional Actions minutes**.  
**Operational verification:** run #488, attempt 2, reran only the historical Wave 06 `Web build` job (`98990802140`). A hosted runner was allocated and Checkout, Node setup, dependency installation and `npm run build` all completed successfully.  
**Billing note:** repository tooling verifies execution availability, not the account billing balance itself; the exact remaining-minute number remains owner/account evidence.

The former temporary Wave 07 no-Actions freeze is therefore **lifted**. Normal CI execution may resume, while the permanent efficiency and exact-final-head rules below remain mandatory.

The probe is availability evidence only. It ran against historical Wave 06 merge head `cc79713434c1d7b5988158b843b137eaf488d923` and does **not** validate the current Wave 07 integration head.

## Non-negotiable quality rule

CI economy must **never** be achieved by:

- weakening or deleting tests;
- relaxing security, CAS, lifecycle, persistence, Runtime or Python sandbox guards;
- ignoring a failing exact-head validation;
- merging known-failing work;
- declaring a worker or wave complete without the validation required by its Definition of Done;
- replacing required final integrated CI with reasoning alone.

The optimization target is **when and how often CI runs**, not what the final product must prove.

## Normal-mode execution rules

### Workers — DEV 1 / DEV 2 / DEV 3

1. Run the focused validation required by the active assignment.
2. Batch coherent implementation changes rather than triggering full matrices on every trivial commit.
3. If CI fails with a localized cause, diagnose and fix the cause before another expensive full run.
4. Do not rerun unchanged heads merely hoping for green.
5. Never merge worker work directly to `main`.
6. Never broaden an assignment merely because CI capacity exists.

### Coordinator

1. Review contracts/diffs before spending full integration matrices.
2. Reuse valid exact-head evidence when the head has not changed semantically.
3. Reconcile integration with real `main` before final wave validation.
4. Reserve the full matrix for meaningful integration/final checkpoints.
5. When a run fails, inspect the failed job/log first and apply a targeted correction.
6. Documentation-only movement in `main` does not require revalidating an unchanged product head.
7. Before merging any functional wave, the required integrated matrix must be green on the exact final product head.

## CI evidence hierarchy

Use the cheapest evidence sufficient for the current decision:

1. code/diff/contract inspection;
2. focused unit/component validation;
3. focused backend/frontend build/test;
4. worker exact-head CI where required;
5. full integration matrix at meaningful integration/final gates.

Higher-cost evidence is not automatically better if lower-cost evidence already answers the current question. Conversely, lower-cost evidence does not replace a required final wave matrix.

## Rerun policy

A rerun is justified when:

- the previous failure was demonstrably transient/infrastructure-only;
- the relevant product head did not change and evidence supports flakiness;
- a required job did not execute because of external runner/service failure.

A rerun is **not** justified merely because a product/test failure is inconvenient. Fix the cause first.

## Pull request policy

Normal Draft/integration PR review is restored. Opening a PR to `main` triggers the repository workflow, so do it only when the branch is at a meaningful validation checkpoint.

For Wave 07 specifically, do not open the integration PR merely to prove Actions availability. First finish the coordinator-owned readiness work, reconcile with current `main`, prepare the candidate exact head, then use the PR/full matrix as product evidence.

## Final wave policy

Before a wave merge to `main`, the coordinator requires the final integrated validation matrix defined by the wave, normally:

- Web build;
- backend Release build;
- full relevant automated tests;
- Runtime smoke;
- Chromium E2E;
- wave-specific acceptance checks.

If a future budget constraint again prevents that matrix, the correct state is `IMPLEMENTED / CI_DEFERRED` or `BLOCKED_BY_CI_BUDGET`, not `MERGED`.

## Wave 06 evidence note

Wave 06 final integration head `d665dc13b0922938a15252d9775ef6604e41bff4` passed CI #487 fully and was merged through PR #83 as `cc79713434c1d7b5988158b843b137eaf488d923`.

Original run #488 attempt 1 allocated no runners/product steps and remained infrastructure evidence rather than a product regression. On 2026-08-28, after the owner's Actions configuration change, attempt 2 reran only its `Web build` job as an availability probe; that job completed successfully. The overall historical run can still display failure because the backend job was intentionally not rerun and Chromium therefore remained skipped.

## Budget-mode fallback

If Actions capacity becomes constrained again, the coordinator may explicitly switch this document back to `CONSTRAINED`. Such a switch changes validation frequency only, never the final quality gate.

CI minutes are a project resource. Use them to buy evidence, not ceremony.
