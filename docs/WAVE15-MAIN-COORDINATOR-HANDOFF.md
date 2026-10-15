# Wave 15 — Main order and execution contract

Snapshot: 2026-10-15. This is the live Main-to-CODEX order. Replace its current order rather than stacking historical CURRENT/LATEST sections. Use [the short pointer](CURRENT-COORDINATOR-HANDOFF.md) for the accepted product checkpoint and evidence.

## Current order — #619 current/frequency observed comparison v1

| Field | Order |
| --- | --- |
| Owner | Main Coordinator |
| Executor | Chat CODEX for implementation and validation; Main for review/integration |
| Product front | Intelligence #588; no change of front |
| Integration target | `wave15/corrections-integration` |
| Verified product checkpoint | `54c7c9942b57328cabcef740c99f4ab3207cc040` |
| Objective | Reproducible current comparison inside an explicit recorded measured-frequency range |
| Allowed changes | New versioned Intelligence DTO/catalog/calculation; additive service/route; owning tests; Main control docs |
| Validation | Focused local calculator/service/API cases and build; exact published T1 SESSION_LICENSING plus inference; Main review; required post-merge CI |
| Completion | #619 acceptance passes, reviewed implementation integrated and verified; CODEX then waits for Main |
| Implementation branch | `work/w15-intelligence-current-frequency-comparison-v1` |

Workflow adjustment #618 is complete at coordination HEAD `120d93a477924b34994e2a1c2167b873c93c512d`; CODEX acknowledged the new process. Old documentation proposals #569/#591/#610 are closed with branches/evidence preserved.

Start at the exact published Main control commit recorded in [#619](https://github.com/brunolrogerio-collab/EliteSCADA/issues/619)'s activation comment. It descends from `120d93a477924b34994e2a1c2167b873c93c512d` and adds only this order, resume pointers, [ADR-015](ADR-015-INTELLIGENCE-CURRENT-FREQUENCY-COMPARISON.md), and the [finite Stage 3 acceptance matrix](WAVE15-INTELLIGENCE-STAGE3-ACCEPTANCE.md). CODEX uses a fresh isolated implementation branch at that commit; Main-authored docs join its draft PR. This avoids a separate documentation integration/CI cycle merely to release the package. Verify the live integration remains that start base or differs only by coordinated documentation; product divergence requires Main disposition.

The owning issue and ADR fix the formula, source-time join, semantic qualifiers, no-partial/security fences, aggregate 50k-row budget, and test cases. Main's narrow shared-file exception covers additive analytics service/route registration only. No source changes to existing v1 contracts, units/roles, Historian provider/storage, HA/security behavior, licensing, UI, or workflows are authorized.

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

PR #617's `net-observed-rate.v1` is integrated and verified at the checkpoint above. Stage 3A descriptive KPIs, the bounded Stage 3B coverage work, controlled semantics, and ADR-014 are existing foundations; they do not establish all of Stage 3 as complete.

The [finite acceptance matrix](WAVE15-INTELLIGENCE-STAGE3-ACCEPTANCE.md) now fixes the remaining scope: #619 first, then Main's PERFORMANCE-V1 prerequisite/formula package, then audit the exact composition against the named rows. Main next reviews the delivered #619 candidate. Stage 4/5/6 and a renewed HA lane are not active orders in this snapshot.

## Persistent authority

- Product code/exact-SHA evidence: live GitHub; durable goals and frozen architecture: `PROJECT GOAL.md` and accepted ADRs.
- Execution cadence: [global protocol](CHAT-COLLABORATION-PROTOCOL.md); current order: this file; short resume state: [current pointer](CURRENT-COORDINATOR-HANDOFF.md).
- Cross-mission ledger: #305; product scope/evidence: the owning issue/PR. Update `LAST CHANGE.md` when the resume point changes.
- Documentation-only movement creates a coordination HEAD, not a new verified product checkpoint. Product divergence must be assessed before a dependent implementation begins.
- `INTEGRATED`, `VERIFIED`, and `FROZEN` require their respective evidence/decision. Whole Wave acceptance remains separate, and final merge to `main` retains its applicable Product Owner gate.

[Earlier Main orders and evidence](WAVE15-MAIN-COORDINATOR-HANDOFF-ARCHIVE-2026-10-15.md) are archived. Read their relevant sections only when a current dependency or contract requires them.
