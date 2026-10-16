# Wave 15 — Main order and execution contract

Snapshot: 2026-10-16. This is the live Main-to-CODEX order. Replace its current order rather than stacking historical CURRENT/LATEST sections. Use [the short pointer](CURRENT-COORDINATOR-HANDOFF.md) for the accepted product checkpoint and evidence.

## Current order — #623 PERFORMANCE-V1B observed physical indicators

| Field | Order |
| --- | --- |
| Owner | Main Coordinator |
| Executor | Chat CODEX for implementation and validation; Main for review/integration |
| Product front | Intelligence #588; no change of front |
| Integration target | `wave15/corrections-integration` |
| Verified product checkpoint / B base | `257a4ab3c40a023beea91a027e5c3d692c22f72c` — PR #625 exact T2 green |
| Main control/start | `coord/w15-intelligence-performance-v1b-activation-20261016`; exact immutable commit is recorded in #623 |
| Objective | Deliver the three fixed, read-only recorded physical indicator rules with reproducible evidence |
| Allowed changes | Add versioned performance catalog/calculator/query beneath the existing analytics route; reuse A canonical metadata, historian.samples.v2, existing query/security/scope guards and owning tests; see #623/ADR-016 |
| Validation | Focused local calculator/service/API tests and API build; exact T1 SESSION_LICENSING plus inferred floor; Main source review and exact broad post-merge CI |
| Completion | #623 finite acceptance implemented, integrated and verified; Main audits Stage3 S3-01–S3-08; no auto-close of #588/Wave15 |
| Implementation branch | `work/w15-intelligence-performance-v1b-recorded-physical-indicators` |

Workflow adjustment #618 is complete at coordination HEAD `120d93a477924b34994e2a1c2167b873c93c512d`; CODEX acknowledged the new process. Old documentation proposals #569/#591/#610 are closed with branches/evidence preserved. PERFORMANCE-V1A #621 is verified after the test-only PR #625 and exact post-merge T2 #38943570688.

Start at the exact Main control commit recorded in [#623](https://github.com/brunolrogerio-collab/EliteSCADA/issues/623). It descends from verified product base `257a4ab3c40a023beea91a027e5c3d692c22f72c` and adds only this order, resume pointers, ADR status and the finite Stage3 matrix. CODEX uses a clean isolated implementation branch from that exact control commit; Main-authored docs join its draft PR. The owning issue records direct dispatch and actual executor availability separately; a credits failure is not execution and must not trigger repeated wakes.

The owning issue #623 and ADR-016 fix B's three physical formula families, raw Historian-v2 evidence, exact synchronized source-time pairing, 50,000 combined-row cap, authorization/revision guards and no-partial-result behavior. A's schema27/unit/catalog/import/editor contracts and all #619 v1 meanings are frozen and must be reused, not reopened. No Authority/HA, licensing behavior, Historian storage/provider, driver, workflow, dependency, UI/dashboard, baseline, free-form formula, or Stage4–6 change is authorized. Do not infer total efficiency, global regime equivalence, integrated energy/production, or physical synchrony beyond the explicit evidence contract.

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

The [finite acceptance matrix](WAVE15-INTELLIGENCE-STAGE3-ACCEPTANCE.md) fixes the remaining scope: A #621 is verified, B #623 is active, then Main performs the exact-composition S3-01–S3-08 audit. The compatibility split is justified by the lower-version writer/import risk, not an indefinite investigation. Stage 4/5/6 and a renewed HA lane are not active orders. The Product Owner's HA Preview remains separate and pending.

## Persistent authority

- Product code/exact-SHA evidence: live GitHub; durable goals and frozen architecture: `PROJECT GOAL.md` and accepted ADRs.
- Execution cadence: [global protocol](CHAT-COLLABORATION-PROTOCOL.md); current order: this file; short resume state: [current pointer](CURRENT-COORDINATOR-HANDOFF.md).
- Cross-mission ledger: #305; product scope/evidence: the owning issue/PR. Update `LAST CHANGE.md` when the resume point changes.
- Documentation-only movement creates a coordination HEAD, not a new verified product checkpoint. Product divergence must be assessed before a dependent implementation begins.
- `INTEGRATED`, `VERIFIED`, and `FROZEN` require their respective evidence/decision. Whole Wave acceptance remains separate, and final merge to `main` retains its applicable Product Owner gate.

[Earlier Main orders and evidence](WAVE15-MAIN-COORDINATOR-HANDOFF-ARCHIVE-2026-10-15.md) are archived. Read their relevant sections only when a current dependency or contract requires them.
