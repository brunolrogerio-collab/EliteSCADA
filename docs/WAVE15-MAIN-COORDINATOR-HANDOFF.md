# Wave 15 — Main order and execution contract

Snapshot: 2026-10-16. This is the live Main-to-CODEX order. Replace its current order rather than stacking historical CURRENT/LATEST sections. Use [the short pointer](CURRENT-COORDINATOR-HANDOFF.md) for the accepted product checkpoint and evidence.

## Current order — Intelligence Stage 4C two-context vector v2

| Field | Order |
| --- | --- |
| Owner | Main Coordinator |
| Executor | Chat CODEX for source audit and findings only; Main owns ADR/design and any later implementation order |
| Product front | Intelligence #588; no change of front |
| Integration target | `wave15/corrections-integration` |
| Current product checkpoint | `7f09c9f88af85e0b5c92a3cacbca867d9a67ee79` — #633 merge; exact T2 #38975551802 passed |
| Completed prerequisite | Stage 3 S3-01–S3-08 and bounded Stage 4A/4B accepted; children #630/#632 closed; residual audit recorded in #588 |
| Objective | Implement additive `contextual-deviation-vector.v2` with 2–8 targets and exactly two caller-declared numeric context intervals per ADR-019/#635 |
| Allowed changes | New v2 catalog/query/calculator/response and focused tests; include the exact Main-authored ADR-019. Keep Stage 4A/4B v1 routes and contracts unchanged |
| Acceptance | Focused local analytics/API tests and Release API build; one exact-head T1 with effective `SESSION_LICENSING`; Main source review; exact post-merge T2 before #635 closes |
| Boundaries | No combined/multivariate anomaly, regime detection, representative/healthy-reference claim, diagnosis/RUL, persistence/cache/worker, UI, schema/shared Engineering semantics, HA or other product front |
| Completion | CODEX publishes one draft PR and returns to Main; Main reviews/integrates and owns exact T2. Stage 4 remains partial after this bounded slice |
| Product code basis | `7f09c9f88af85e0b5c92a3cacbca867d9a67ee79`; current coordination ancestry is docs-only. Main records the exact post-ADR live integration ref in #635 before CODEX source mutation |

Verified Stage 4 delivery evidence: Stage 4A PR #631 merged at `51a4204de926ec2a81bdcbebacba8dbb578dbb71`; exact T1 #38970785957 and T2 #38971337239 passed. Stage 4B PR #633 merged at `7f09c9f88af85e0b5c92a3cacbca867d9a67ee79`; local focused analytics/API tests 198/198, Release API build 0 warnings/errors; exact candidate T1 #38974826487 passed (SESSION_LICENSING, 1,615 passed/1 skipped); exact T2 #38975551802 passed at the merge SHA (backend 2,500 passed/1 skipped, Web passed, Chromium integration smoke 13/13 passed, full-browser E2E skipped). The residual audit and Stage 4C decision are in #588/#635 and [ADR-019](ADR-019-INTELLIGENCE-CONTEXTUAL-DEVIATION-VECTOR-V2.md).

Workflow adjustment #618 and PERFORMANCE-V1A #621/#625 remain verified. Accepted Stage 3 contracts and Stage 4A/4B endpoints are frozen within their stated meanings. Main has authorized only the new Stage 4C v2 contract in #635. Stage 4 remains partial; Stages 5/6, #588 and Wave 15 remain open.

## Responsibilities

- Product Owner sets product intent, scope, and owner acceptance. Previously granted authorization remains applicable to its stated scope and conditions.
- Main designs bounded packages, resolves cross-contract decisions, publishes assignments, reviews delivered code/evidence, integrates approved work, and maintains the current state.
- CODEX implements the assigned slice, tests it locally, publishes a draft PR and exact evidence, then returns control to Main. Main does not implement the same slice in parallel.
- Additional DEV/AUD chats receive explicit owners and boundaries only when Main opens parallel work. The current operating mode is one product executor.

An order requiring a separate **Main** decision does not require the Product Owner to repeat authorization already granted for the product front. Main may prepare and release the next bounded package within that authorization after reviewing dependencies. Scope expansion, changed frozen contracts, or changed acceptance expectations must receive the applicable decision before implementation.

## One execution cycle

1. Main records one package: issue, exact product base, branch/target, expected result, allowed/forbidden scope, consumed frozen contracts, acceptance checks, evidence destination, and next owner.
2. Main publishes it here and in the owning issue, verifies readback, and dispatches it once to the authorized executor. A GitHub comment alone is not proof that another chat received a message.
3. CODEX checks its local branch/cleanliness and the live remote once before mutations, implements, and runs the relevant focused local checks. Existing unrelated user changes remain untouched.
4. CODEX publishes a coherent candidate/draft PR with `VALIDATION_PROFILE`, head SHA and tests. One party owns CI dispatch and observation; both parties reuse the same run IDs.
5. Main reviews the diff and evidence. Corrections name the unmet acceptance item; CODEX corrects that item under the same package. A failed gate requires diagnosis before rerun.
6. Main applies the existing integration authorization once its conditions pass, verifies the required post-merge result, records the disposition, and closes the slice. CODEX waits until Main releases the next package.

Required local evidence, CI, and owner Preview are distinct acceptance sources. A passing CI does not replace local/human evidence requested by the Product Owner. A local pass does not turn a required failing CI gate into a pass. Apply [the CI policy](CI-USAGE-POLICY.md) without changing assertions or workflow requirements.

## Return from CODEX

Use one delivery containing:

```text
CODEX -> MAIN COORDINATOR
Package / owning issue:
Status / next owner:
Base / branch / exact published head / PR:
Implemented result and changed files:
Acceptance evidence: local checks; CI run/attempt/job; counts and skips:
Open review requests, limitations, or exact blocker:
Next executable action / prerequisite:
```

Main records review separately from the original evidence. An acknowledgement is not a second delivery, and unchanged completion does not require repeated handoffs.

## Blocking, recovery, and observation

- Reuse a known accessible checkout. A failed shell/UI operation proves failure of that operation, not failure of Visual Studio, the repository, or every available access route.
- For an unchanged failure, allow one documented alternate route or targeted retry supported by evidence. Repetition across later `siga` turns counts as the same retry budget.
- If that fails, record the exact command/operation, error, base/head, affected action, next owner, and observable change needed to resume. Continue useful independent work within the same authorized front.
- CI retries follow the CI policy: inspect logs and the changed files, reproduce the failing test locally where available, and distinguish a demonstrated defect from a suspected transient condition. A non-reproduction alone does not establish root cause.
- Use the chat wait mechanism with a cursor for executor progress. Use an existing workflow completion signal or bounded status check for CI. Unchanged observations do not create new issue comments, new runs, or new task prompts.

## Current product facts and next Main planning task

Stage 3 S3-01–S3-08 is complete/verified. Stage 4A is integrated/verified at `51a4204de926ec2a81bdcbebacba8dbb578dbb71`; Stage 4B is integrated/verified at `7f09c9f88af85e0b5c92a3cacbca867d9a67ee79`. Stage 4B adds independent per-target contextual differences/scores over one complete synchronized cohort; it does not classify combined anomalies, diagnose faults, claim reference health or establish global regime equivalence. Exact tests and validation runs are recorded above and in #588/#632.

The residual Stage 4 audit found that a second caller-declared numeric context can be added safely as a separate vector v2 contract, but does not establish representative references, global regime equivalence or a valid multivariate anomaly statistic. ADR-019 freezes this bounded decision. CODEX implements only #635 after Main records the exact post-documentation integration ref; Main then reviews, integrates and verifies T2. Stage 4 remains partial after this slice; Stage 5 dashboards/visuals and Stage 6 broad hardening are not current orders. The Product Owner's local HA Preview remains separate and pending.

## Persistent authority

- Product code/exact-SHA evidence: live GitHub; durable goals and frozen architecture: `PROJECT GOAL.md` and accepted ADRs.
- Execution cadence: [global protocol](CHAT-COLLABORATION-PROTOCOL.md); current order: this file; short resume state: [current pointer](CURRENT-COORDINATOR-HANDOFF.md).
- Cross-mission ledger: #305; product scope/evidence: the owning issue/PR. Update `LAST CHANGE.md` when the resume point changes.
- Documentation-only movement creates a coordination HEAD, not a new verified product checkpoint. Product divergence must be assessed before a dependent implementation begins.
- `INTEGRATED`, `VERIFIED`, and `FROZEN` require their respective evidence/decision. Whole Wave acceptance remains separate, and final merge to `main` retains its applicable Product Owner gate.

[Earlier Main orders and evidence](WAVE15-MAIN-COORDINATOR-HANDOFF-ARCHIVE-2026-10-15.md) are archived. Read their relevant sections only when a current dependency or contract requires them.
