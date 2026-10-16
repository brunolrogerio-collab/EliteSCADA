# Wave 15 — Main order and execution contract

Snapshot: 2026-10-16. This is the live Main-to-CODEX order. Replace its current order rather than stacking historical CURRENT/LATEST sections. Use [the short pointer](CURRENT-COORDINATOR-HANDOFF.md) for the accepted product checkpoint and evidence.

## Current order — #623 S3-07 security-test acceptance follow-up

| Field | Order |
| --- | --- |
| Owner | Main Coordinator |
| Executor | Chat CODEX for implementation and validation; Main for review/integration |
| Product front | Intelligence #588; no change of front |
| Integration target | `wave15/corrections-integration` |
| Current product checkpoint / exact follow-up base | `ab38bfe879d6ca8074aa5c38b821c7da62634768` — PR #626 merged; T2 #38961262161 green |
| Previously verified prerequisite | PERFORMANCE-V1A #621 at `257a4ab3`; exact T2 #38943570688 |
| Objective | Close the explicit #623/S3-07 deterministic security-test matrix missing from post-merge review; do not change product behavior |
| Allowed changes | `tests/Scada.Drivers.Tests/AssetProcessIntelligenceAnalyticsTests.cs` only |
| Test matrix | Both 2-input rules and the 3-input ratio rule; revoke each selected TAG while another input page is deterministically in flight; permit cancellation plus workspace/revision/entitlement/scope invalidation in 2- and 3-input flows; no partial metric or unauthorized next read |
| Validation | Focused PerformanceV1B and full Analytics class locally; one exact candidate T1; Main source/test review; integrate only after Main acceptance and verify exact post-merge T2 |
| Completion | Main accepts the finite S3-07 tests and exact T1; after integration exact T2 passes, Main re-audits S3-01–S3-08. No auto-close of #588/Wave15 |
| Implementation branch | `work/w15-intelligence-performance-v1b-security-matrix` |

Workflow adjustment #618 is complete at coordination HEAD `120d93a477924b34994e2a1c2167b873c93c512d`; CODEX acknowledged the new process. Old documentation proposals #569/#591/#610 are closed with branches/evidence preserved. PERFORMANCE-V1A #621 is verified after the test-only PR #625 and exact post-merge T2 #38943570688.

The original B package on #623/ADR-016 is integrated by PR #626 at `ab38bfe879d6ca8074aa5c38b821c7da62634768`. Exact-head T1 #38956508442 and exact-merge T2 #38961262161 passed. During the required Main audit, source review of `PerformanceV1B_RevocationOrContextChangeBetweenPagesDiscardsTheWholeQuery` found only the two-input differential-pressure rule and only `request.SignalIds[0]` revoked; the explicit #623 requirement for both two-/three-input flows and every selected TAG is therefore not yet proven. The correction is intentionally tests-only and does not reopen the accepted formulas or frozen A/#619 contracts.

Remote validation before dispatch: `wave15/corrections-integration` was `ab38bfe879d6ca8074aa5c38b821c7da62634768`; follow-up branch `work/w15-intelligence-performance-v1b-security-matrix` was absent. CODEX must revalidate both once before mutation, then use a clean isolated worktree from that exact base. The owning issue records dispatch and executor availability separately; do not confuse a published comment with a delivered task or repeat an unchanged wake.

The owning issue #623 and ADR-016 fix B's three physical formula families, raw Historian-v2 evidence, exact synchronized source-time pairing, 50,000 combined-row cap, authorization/revision guards and no-partial-result behavior. A's schema27/unit/catalog/import/editor contracts and all #619 v1 meanings are frozen and must be reused, not reopened. This follow-up cannot modify product source, Authority/HA, licensing, Historian storage/provider, drivers, workflow, dependencies, UI/dashboard, baselines or formula semantics. Do not infer total efficiency, global regime equivalence, integrated energy/production, or physical synchrony beyond the explicit evidence contract. The prior Modbus result is treated as recurring timing per Product Owner and is outside this work.

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
