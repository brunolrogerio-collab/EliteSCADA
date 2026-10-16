# Current coordinator pointer — Wave 15

Snapshot: 2026-10-16. Replace this snapshot when state changes; keep detailed history in the owning issue/PR. Live refs, published code, and exact-SHA Actions evidence govern implementation state.

## Current position

| Field | Current disposition |
| --- | --- |
| Integration target | `wave15/corrections-integration` |
| Current product checkpoint | `b58fd4857ee2c6ed84e40405148294cb910fddbd` — #628 merge; exact T2 green on attempt 2 |
| Product front | Asset & Process Intelligence, issue #588; Stage 3 complete, #588 remains open |
| Completed slice | `current-at-recorded-frequency.compare.v1` — INTEGRATED / VERIFIED |
| Verified prerequisite | PERFORMANCE-V1A #621 / PR #622 — INTEGRATED / VERIFIED at `257a4ab3`, exact T2 #38943570688 |
| Integrated slice | PERFORMANCE-V1B #623 / PR #626 at `ab38bfe`; S3-07 test follow-up PR #628 at `b58fd48` |
| Current acceptance state | **STAGE 3 COMPLETE / VERIFIED**; S3-01–S3-08 accepted on exact composition `b58fd485` |
| Product executor | Chat CODEX; next task is a read-only Stage 4 architecture audit |
| Immediate direction | Bound the Stage 4 baseline/deviation design against existing Stage 3 evidence and contracts |
| Next owner/action | CODEX returns findings to #588; Main then records a separate implementation order. No Stage 4 source changes in the audit |
| Allowed changes | None for the audit; source review and evidence only |

Coordination-only commits after the product checkpoint do not invalidate its product evidence. Discover the current coordination HEAD from the live integration ref.

Workflow adjustment #618 is complete. [ADR-015](ADR-015-INTELLIGENCE-CURRENT-FREQUENCY-COMPARISON.md) records accepted #619; [ADR-016](ADR-016-INTELLIGENCE-PERFORMANCE-V1.md) fixes the physical-input/formula scope. A (#621) and B (#623) are verified. PR #628 closes S3-07 with deterministic per-input/two-vs-three-input revocation and context-invalidation coverage. Main's final audit accepts S3-01–S3-08. #588 remains open for later stages; the active Stage 4 task is read-only planning, not implementation.

## Evidence

- #619/#620: local Analytics 78/78, T1 #38832922847 attempt2 and post-merge #38834562474; accepted `current-at-recorded-frequency.compare.v1` remains frozen within its meaning.
- #621 / PR #622: A integrated at `a96b49b`; its broad post-merge CI initially failed in two HA-D2 separate-database timing cases. Test-only correction PR #625 is integrated at `257a4ab3`; [T1 #38942922868](https://github.com/brunolrogerio-collab/EliteSCADA/actions/runs/38942922868) and exact-SHA [T2 #38943570688](https://github.com/brunolrogerio-collab/EliteSCADA/actions/runs/38943570688) are green. T2 totals: Core674, PostgreSQL127, Timescale41, Drivers1,519 passed/1 skipped, Security42; Web build and Chromium integration smoke13/13 passed.
- #625 changed only the test fixture wait/request timeout. It does not prove a root cause in HA and does not replace the Product Owner's outstanding local HA Preview/regression acceptance. No HA runtime/product code was touched.
- PR #626/#623: B code is integrated at `ab38bfe`; T1 #38956508442 and exact T2 #38961262161 are green, with full-browser E2E skipped. Main's post-merge audit found the stated S3-07 coverage gap; only the test-only follow-up is active. Stage3 remains partial; #588 and Wave15 remain open.
- PR #628/#623: deterministic S3-07 tests merged at `b58fd4857ee2c6ed84e40405148294cb910fddbd`. Exact candidate T1 #38963151780 attempt 2 and exact-merge T2 #38964817316 attempt 2 are green. T2 backend: Core674, PostgreSQL127, Historian41, Drivers1,578 passed/1 skipped/0 failed, Security42 (2,462 passed/1 skipped overall); Web build passed; Chromium integration smoke 13/13 passed; full-browser E2E skipped. T2's first attempt had one unrelated HA-D2 separate-db failure; a single failed-backend-job retry passed without code changes.
- Main re-audited S3-01–S3-08 on `b58fd485`; Stage 3 is complete/verified. Next active work is a read-only Stage 4 design audit; no baselines/anomaly implementation is authorized until Main records the bounded package.

## Operating documents

1. [Global collaboration protocol](CHAT-COLLABORATION-PROTOCOL.md), including the incremental-read and handoff cycle.
2. [Main's current order and return contract](WAVE15-MAIN-COORDINATOR-HANDOFF.md).
3. [CI policy](CI-USAGE-POLICY.md) and the active mission's required profiles.
4. [State-independent bootstrap](NEXT-COORDINATOR-CHAT-HANDOFF.md) for recovery; [document authority map](README.md) for conflicting sources.

Read #297 first, #302 next when establishing the Wave/Authority baseline; then #305 and the active mission's latest comments. For an unchanged ongoing mission, check only changed material and the live decision inputs.

## Standing limits and pending owner validation

- Windows/Linux and the product's other declared platforms remain architectural scope.
- Intelligence remains licensed, selective, read-only analytics. Existing contracts and the boundaries between Stage 3 KPIs, Stage 4 baselines, Stage 5 visuals, and Stage 6 hardening still apply.
- HA acceptance raised by the Product Owner remains for the local Preview with the owner. The prior unsupervised HA-related work is a regression risk requiring that verification; a targeted code reversion may be necessary if owner/CODEX checks find problems. CI is not a substitute for that owner acceptance. Do not switch from Intelligence to HA without a new product-direction decision.
- Wave 15 and #588 remain open; Stage 3 acceptance is not whole-Intelligence or Wave 15 closure and makes no FND/FC0 claim.
- Stage 5 visuals, Stage 6 qualification, and the Product Owner's local HA Preview/regression acceptance remain separate and pending.

Historical snapshots: [previous current pointer](CURRENT-COORDINATOR-HANDOFF-ARCHIVE-2026-10-15.md), [previous Main orders](WAVE15-MAIN-COORDINATOR-HANDOFF-ARCHIVE-2026-10-15.md). They preserve evidence and are not active instructions.
