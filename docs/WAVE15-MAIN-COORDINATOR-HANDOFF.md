# Wave 15 — Main order and execution contract

Snapshot: 2026-10-16. This is the live Main-to-CODEX order. Replace this current order rather than stacking historical CURRENT/LATEST sections. Use [the short pointer](CURRENT-COORDINATOR-HANDOFF.md) for the accepted product checkpoint and evidence.

## Current order — Stage 5A generic measurement widgets (#639)

| Field | Order |
| --- | --- |
| Owner | Main Coordinator |
| Executor | Chat CODEX; one bounded implementation package, dispatched after this handoff PR merges |
| Product front | Asset & Process Intelligence #588 |
| Integration target | `wave15/corrections-integration` |
| Product evidence checkpoint | Code merge `81ac4193194e1471e812e8164f9b9e15e63512ea`, PR #637, exact T2 #38981435925 attempt 2 passed; documentation-only follow-up PRs #638 and #640 do not alter product files. Main must revalidate the live integration ref after this handoff update merges and post the exact active base in #639 before dispatch. |
| Stage 4 disposition | Stage 4 V1 COMPLETE / VERIFIED / FROZEN only within the Product Owner-approved limits recorded in #588 and ADR-020 |
| Objective | Add six generic display-only measurement widgets to the canonical Engineering screen toolbox, preview, serialization and Runtime rendering |
| Scope | Semicircular dial; circular ring; horizontal graduated bar; vertical ruler/thermometer; segmented range bands; actual-value versus target. Reuse canonical screen model/binding/render lifecycle. Keep generic widgets usable without Intelligence license. Show unit, limits/target, quality and freshness accessibly; color is not the sole cue. No process writes or controls. |
| Start condition | This documentation/order PR merges. Main then verifies the live integration SHA, publishes one activation comment on #639 with that exact base, and sends the single CODEX dispatch. CODEX does not mutate before that dispatch. |
| Required validation | Local focused component/accessibility/round-trip and preview/runtime parity checks; PR declares `VALIDATION_PROFILE: UI_EDITOR`; exact-head T1 passes; Main reviews; after authorized merge, exact-merge T2 passes. Product Owner performs manual visual Preview after integration and before Stage 5A acceptance/freeze. |
| Forbidden work | No dashboard generation (Stage 5B), Stage 6, Analytics/history changes, model training, license changes, second screen model/renderer, frozen Stage 4 contract changes, HA, drivers, or unrelated work |
| Completion | CODEX returns one PR with exact base/head, symbols, local evidence, T1 links and caveats. Main reviews/integrates; post-merge T2 and Product Owner Preview are separate gates. Close only #639 after both pass; CODEX then waits for Main. #588 and Wave 15 remain open |

### Context and frozen boundaries

- Stage 3 S3-01–S3-08 is complete/verified. Stage 4A adds a descriptive single-target deviation against a caller-selected reference and context. Stage 4B adds independent per-target deviations over complete synchronized co-observations. Stage 4C adds the separate additive `contextual-deviation-vector.v2` with exactly two caller-declared numeric context intervals and 2–8 target TAGs. The Product Owner approved the V1 acceptance boundary after the read-only audit: the caller selects and is responsible for the known-good reference period; the system does not certify health, representativeness or global regime equivalence; the vector means independent per-TAG scores, not a joint multivariate model. Main recorded Stage 4 V1 COMPLETE / VERIFIED / FROZEN only under these limits in #588/#305 and ADR-020.
- Stage 4C / ADR-019 / child #635 is integrated, verified and frozen only within its bounded scope at PR #637 merge `81ac4193194e1471e812e8164f9b9e15e63512ea`. Exact candidate T1 #38978898568 attempt 3 passed; exact post-merge T2 #38981435925 attempt 2 passed at the merge SHA. The backend-only retry passed after local Linux reproduction of its unchanged timing-sensitive Z-Wave test. Build/runtime smoke, Web build and Chromium integration smoke passed; full-browser E2E was skipped and is not claimed. Detailed counts and caveats are in #588/#635/#305 and PR #637.
- ADR-019 and ADR-020 preserve non-claims: no automatic reference health, no global regime equivalence, no joint multivariate anomaly detection, diagnosis or RUL. Do not alter these limits in Stage 5A.
- Stage 5A is only generic measurement widgets. Guided dashboard generation from profiles/processes is future Stage 5B; Stage 6 hardening remains later. The Product Owner's local HA Preview/regression acceptance remains separate; do not switch to HA.

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

For Stage 5A, return one implementation report in this form:

```text
CODEX -> MAIN COORDINATOR
Package / owning issue: Stage 5A generic measurement widgets / #639
Status / next owner:
Exact base SHA and product tree; PR head SHA:
Files/symbols changed and canonical screen/toolbox/render path reused:
Six layout, theme, value/unit/bounds, quality/freshness and accessibility coverage:
Serialization round-trip and preview/Runtime parity evidence:
Focused local commands/results:
VALIDATION_PROFILE and exact T1 run/job links:
Risks/limitations and explicit non-claims:
Confirmation: no Stage 5B, Stage 6, contract expansion, or self-merge
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
