# Wave 15 — Main order and execution contract

Snapshot: 2026-10-16. This is the live Main-to-CODEX order. Replace its current order rather than stacking historical CURRENT/LATEST sections. Use [the short pointer](CURRENT-COORDINATOR-HANDOFF.md) for the accepted product checkpoint and evidence.

## Current order — Intelligence Stage 4 bounded architecture audit (read-only)

| Field | Order |
| --- | --- |
| Owner | Main Coordinator |
| Executor | Chat CODEX for source audit and findings only; Main owns ADR/design and any later implementation order |
| Product front | Intelligence #588; no change of front |
| Integration target | `wave15/corrections-integration` |
| Current product checkpoint | `b58fd4857ee2c6ed84e40405148294cb910fddbd` — #628 merged; exact T2 #38964817316 attempt 2 green |
| Completed prerequisite | Stage 3 S3-01–S3-08 re-audited and complete/verified; #623 follow-up resolved by #628 |
| Objective | Identify the smallest safe, explainable Stage 4 baseline/deviation slice that can reuse existing Intelligence, Historian, profile, entitlement, scope and Authority contracts |
| Allowed changes | None. Read-only repository/source audit and a findings comment in #588 only |
| Audit questions | Existing baseline/anomaly implementations and reusable service/API patterns; exact source symbols; explicit regime/context evidence; minimum data/quality/sufficiency requirements; deterministic unavailable states; cancellation/revocation and inactive-scope behavior; resource and cross-platform constraints; finite local/CI tests |
| Boundaries | No product edits, schema/store/worker, free-form formula or external model; no Stage 5 UI/gauges, Stage 6 broad qualification, HA or other product front |
| Completion | CODEX returns cited file/symbol findings and a finite proposal to #588. Main will publish a separate Stage 4 implementation package/ADR before any code changes |
| Product checkpoint for audit | `b58fd4857ee2c6ed84e40405148294cb910fddbd` |

Stage 3 closeout evidence: #628 changed only `tests/Scada.Drivers.Tests/AssetProcessIntelligenceAnalyticsTests.cs`. Local tests passed 59/59 (PerformanceV1B) and 138/138 (full Analytics class). Exact candidate T1 #38963151780 attempt 2 passed at `791fbf9117fef3474ffcb2f2e2c08a95b99df6ed`: focused .NET 1,577 passed / 0 failed / 1 skipped. Exact post-merge T2 #38964817316 attempt 2 passed at `b58fd4857ee2c6ed84e40405148294cb910fddbd`: Core674, PostgreSQL127, Historian41, Drivers1,578 passed/1 skipped, Security42; 2,462 passed/1 skipped/0 failed overall; backend build/test/runtime smoke and Web build passed; Chromium integration smoke13/13 passed; full-browser E2E skipped. The first T2 attempt had one unrelated HA-D2 separate-database timing failure; one failed-backend-job retry passed without source changes.

Workflow adjustment #618 and PERFORMANCE-V1A #621/#625 remain verified. The accepted #619 meanings and ADR-016's three V1B formula families, raw Historian-v2 evidence, exact synchronized source-time pairing, 50,000 combined-row cap, authorization/revision guards and no-partial-result behavior remain frozen. S3-01–S3-08 are accepted on `b58fd485`; Stage 3 is complete, but #588 and Wave 15 remain open. The audit is planning only; it does not authorize Stage 4 implementation or Stage 5/6 work.

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

PR #620's `current-at-recorded-frequency.compare.v1` is integrated/verified at the checkpoint above, with local 78/78, exact T1 #38832922847 attempt2 and exact post-merge #38834562474. It compares recorded-frequency-conditioned observations, not verified equivalent regimes or efficiency. Earlier descriptive/coverage/temporal/net-rate foundations remain accepted; they do not establish all of Stage 3 as complete.

The [finite acceptance matrix](WAVE15-INTELLIGENCE-STAGE3-ACCEPTANCE.md) records S3-01–S3-06 within their explicit meanings, with the global-regime/physical-synchrony limits preserved; S3-07 is the current test-only blocker. S3-08 must include the corrective candidate's exact T1 and exact post-merge T2. After that, Main re-audits the exact composition before setting the Stage 3 disposition. Stage 4/5/6 and a renewed HA lane are not current orders. The Product Owner's HA Preview remains separate and pending.

## Persistent authority

- Product code/exact-SHA evidence: live GitHub; durable goals and frozen architecture: `PROJECT GOAL.md` and accepted ADRs.
- Execution cadence: [global protocol](CHAT-COLLABORATION-PROTOCOL.md); current order: this file; short resume state: [current pointer](CURRENT-COORDINATOR-HANDOFF.md).
- Cross-mission ledger: #305; product scope/evidence: the owning issue/PR. Update `LAST CHANGE.md` when the resume point changes.
- Documentation-only movement creates a coordination HEAD, not a new verified product checkpoint. Product divergence must be assessed before a dependent implementation begins.
- `INTEGRATED`, `VERIFIED`, and `FROZEN` require their respective evidence/decision. Whole Wave acceptance remains separate, and final merge to `main` retains its applicable Product Owner gate.

[Earlier Main orders and evidence](WAVE15-MAIN-COORDINATOR-HANDOFF-ARCHIVE-2026-10-15.md) are archived. Read their relevant sections only when a current dependency or contract requires them.
