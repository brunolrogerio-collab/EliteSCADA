# Wave 15 — Main order and execution contract

Snapshot: 2026-10-16. This is the live Main-to-CODEX order. Replace this current order rather than stacking historical CURRENT/LATEST sections. Use [the short pointer](CURRENT-COORDINATOR-HANDOFF.md) for the accepted product checkpoint and evidence.

## Current order — remaining Stage 4 gap audit (read-only)

| Field | Order |
| --- | --- |
| Owner | Main Coordinator |
| Executor | Chat CODEX; one source/contract audit only |
| Product front | Asset & Process Intelligence #588; no front switch |
| Integration target | `wave15/corrections-integration` |
| Current product checkpoint | `81ac4193194e1471e812e8164f9b9e15e63512ea` — PR #637 merge; exact T2 #38981435925 attempt 2 passed |
| Completed Stage 4 slices | 4A #630 / PR #631; 4B #632 / PR #633; 4C #635 / PR #637; each accepted only within its bounded contract |
| Objective | Identify what remains in Stage 4 after the accepted 4A/4B/4C contracts and recommend the next safe bounded increment, or report that no safe increment is ready |
| Allowed work | Read current #588/ADR/docs and exact implementation at the checkpoint; trace source symbols and tests; produce a concise gap/evidence/options report for Main |
| Forbidden work | No source/test/docs edits, branches/PRs, CI dispatch, contract changes, statistical or diagnostic claims, Stage 5/6 implementation, HA, driver work, or unrelated product front |
| Acceptance | Report exact base SHA/tree, file/symbol references, evidence for each accepted Stage 4 contract, exact residual gaps, risks/dependencies, and one bounded recommendation; then wait for Main |
| Completion | Main reviews the report and issues a separate bounded implementation order only if justified. Stage 4 remains partial until its acceptance is explicitly resolved; #588 and Wave 15 remain open |

### Context the audit must preserve

- Stage 3 S3-01–S3-08 is complete/verified. Stage 4A adds a descriptive single-target deviation against a caller-selected reference and context. Stage 4B adds independent per-target deviations over complete synchronized co-observations. Stage 4C adds the separate additive `contextual-deviation-vector.v2` with exactly two caller-declared numeric context intervals and 2–8 target TAGs.
- Stage 4C / ADR-019 / child #635 is integrated, verified and frozen only within its bounded scope at PR #637 merge `81ac4193194e1471e812e8164f9b9e15e63512ea`. Exact candidate T1 #38978898568 attempt 3 passed; exact post-merge T2 #38981435925 attempt 2 passed at the merge SHA. The backend-only retry passed after local Linux reproduction of its unchanged timing-sensitive Z-Wave test. Build/runtime smoke, Web build and Chromium integration smoke passed; full-browser E2E was skipped and is not claimed. Detailed counts and caveats are in #588/#635/#305 and PR #637.
- ADR-019 does not claim representative/healthy references, global regime equivalence, multivariate anomaly detection, diagnosis or RUL. Do not relax those boundaries in an audit or infer that Stage 4 is complete just because 4C was integrated.
- Stage 5 dashboards/gauges and Stage 6 hardening remain pending and are outside this read-only order. The Product Owner's local HA Preview/regression acceptance remains separate; do not switch to HA without direction.

## Responsibilities

- Product Owner sets product intent, scope, and owner acceptance. Previously granted authorization persists only within its stated scope and conditions.
- Main designs bounded packages, resolves cross-contract decisions, publishes assignments, reviews evidence, integrates approved work, and maintains coordination state.
- CODEX performs only the currently assigned task, records its evidence in the owning issue, then returns control to Main. It does not self-merge or select its next package.
- Additional DEV/AUD chats receive explicit owners/boundaries only when Main opens parallel work. Current operating mode is one executor.

An order requiring a separate **Main** decision does not require the Product Owner to repeat existing authorization for Intelligence. Main may release another bounded package within that authorization after dependency review. Escalate a materially new product front, changed frozen contract, or changed acceptance expectation to its applicable decision authority.

## One execution cycle

1. Main records one package: issue, exact base, objective, scope, frozen contracts, acceptance evidence, persistence surface, and next owner.
2. Main publishes the package in this file and the owning issue, verifies readback, and dispatches once to the authorized executor. An issue comment alone does not prove another chat received the order.
3. CODEX checks branch/cleanliness/live ref once before mutation, implements only its assignment, and runs focused local validation. Existing unrelated changes remain untouched.
4. CODEX publishes one coherent draft PR with `VALIDATION_PROFILE`, exact head and evidence. One party owns CI dispatch/observation; reuse run IDs.
5. Main reviews the diff and evidence. Corrections name the unmet acceptance item. Failed gates require diagnosis before rerun.
6. Main applies integration authorization when its conditions pass, verifies the required post-merge gate, records the disposition, and closes only that bounded slice. CODEX waits until Main releases the next package.

Required local evidence, CI, and owner Preview are distinct acceptance sources. CI economy must not weaken required evidence; see [CI policy](CI-USAGE-POLICY.md). A broad CI pass does not substitute for Product Owner human/Preview evidence.

## Return from CODEX

For the current read-only audit, return one report in this form:

```text
CODEX -> MAIN COORDINATOR
Package / owning issue: Stage 4 residual audit / #588
Status / next owner:
Exact integration base SHA and tree:
Files/symbols inspected:
Accepted 4A/4B/4C evidence and contract boundaries:
Residual requirements/gaps, with exact evidence:
Options and risks; one bounded recommendation or no-safe-increment finding:
Product Owner decision needed (if any):
Confirmation: no source/tests/docs/CI changes made
```

Main records its review separately. An acknowledgement is not a second delivery.

## Blocking, recovery, and observation

- Reuse an accessible checkout; a failed shell/UI operation proves only that operation failed.
- For an unchanged failure, allow at most one evidenced alternate route or retry under the global collaboration and CI policies. Repeated `siga` turns do not reset that budget.
- Record blockers with exact error, base/head, affected action, next owner, and observable condition permitting resumption. Continue only safe, useful work within the same authorized front.
- Use the chat wait mechanism/cursor for executor progress and bounded CI status checks. Unchanged observations do not create issue comments, runs or prompts.

## Persistent authority

- Live GitHub refs, PRs and exact-SHA checks govern implementation state. ADRs and `PROJECT GOAL.md` govern frozen architecture and durable product goals.
- The global [collaboration protocol](CHAT-COLLABORATION-PROTOCOL.md) governs persistence and cadence; this file is the current Main order; [the current pointer](CURRENT-COORDINATOR-HANDOFF.md) is the short resume state; #305 is the cross-mission ledger; #588 is the Intelligence ledger.
- Documentation-only movement creates a coordination HEAD, not a new product checkpoint. Revalidate divergence before dependent product mutation.
- `INTEGRATED`, `VERIFIED`, and `FROZEN` each require their stated evidence/decision. Whole Wave acceptance remains separate; final merge to `main` retains its Product Owner gate.

[Earlier Main orders and evidence](WAVE15-MAIN-COORDINATOR-HANDOFF-ARCHIVE-2026-10-15.md) remain archived and are not active instructions unless a current dependency requires them.
