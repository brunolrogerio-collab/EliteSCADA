# Current coordinator pointer — Wave 15

Snapshot: 2026-10-16. Replace this snapshot when state changes; keep detailed history in the owning issue/PR. Live refs, published code, and exact-SHA Actions evidence govern implementation state.

## Current position

| Field | Current disposition |
| --- | --- |
| Integration target | `wave15/corrections-integration` |
| Current product checkpoint | `7f09c9f88af85e0b5c92a3cacbca867d9a67ee79` — PR #633 merge; exact T2 #38975551802 passed |
| Product front | Asset & Process Intelligence, issue #588; Stage 4C implementation is authorized under #635; #588 remains open |
| Completed slices | Stage 3 S3-01–S3-08; Stage 4A #630 / PR #631; Stage 4B #632 / PR #633 — accepted within bounded scopes |
| Verified prerequisite | PERFORMANCE-V1A #621 / PR #622 — INTEGRATED / VERIFIED at `257a4ab3`, exact T2 #38943570688 |
| Integrated slices | PERFORMANCE-V1B #623 / PR #626 at `ab38bfe`; S3-07 follow-up PR #628 at `b58fd48`; Stage 4A/4B in #630/#632 |
| Current acceptance state | **Stage 3 COMPLETE / VERIFIED; Stage 4A and 4B INTEGRATED / VERIFIED; Stage 4 PARTIAL** |
| Product executor | Chat CODEX; single implementation executor for Stage 4C |
| Immediate direction | Add only the ADR-019 two-context deviation-vector v2 contract; preserve the Stage 4A/4B v1 endpoints and meanings |
| Next owner/action | After ADR/handoff docs T1 is integrated, CODEX revalidates the live coordination ref, implements #635, runs focused local validation and exact T1, then waits for Main review |
| Allowed changes | Scoped Stage 4C API/catalog/calculation/tests plus ADR-019 only; no persistence, UI, schema, HA or other stage/product front |

Coordination-only commits after the product checkpoint do not invalidate its product evidence. Discover the current coordination HEAD from the live integration ref.

Workflow adjustment #618 is complete. [ADR-015](ADR-015-INTELLIGENCE-CURRENT-FREQUENCY-COMPARISON.md) records accepted #619; [ADR-016](ADR-016-INTELLIGENCE-PERFORMANCE-V1.md) fixes the physical-input/formula scope; [ADR-019](ADR-019-INTELLIGENCE-CONTEXTUAL-DEVIATION-VECTOR-V2.md) records the current Stage 4C decision. Stage 3 is accepted on `b58fd485`; Stage 4A #630 is verified at `51a4204`, and Stage 4B #632 at `7f09c9f`. Stage 4C is authorized, not yet implemented; Stage 4 remains partial and #588 remains open.

## Evidence

- #619/#620: local Analytics 78/78, T1 #38832922847 attempt2 and post-merge #38834562474; accepted `current-at-recorded-frequency.compare.v1` remains frozen within its meaning.
- #621 / PR #622: A integrated at `a96b49b`; its broad post-merge CI initially failed in two HA-D2 separate-database timing cases. Test-only correction PR #625 is integrated at `257a4ab3`; [T1 #38942922868](https://github.com/brunolrogerio-collab/EliteSCADA/actions/runs/38942922868) and exact-SHA [T2 #38943570688](https://github.com/brunolrogerio-collab/EliteSCADA/actions/runs/38943570688) are green. T2 totals: Core674, PostgreSQL127, Timescale41, Drivers1,519 passed/1 skipped, Security42; Web build and Chromium integration smoke13/13 passed.
- #625 changed only the test fixture wait/request timeout. It does not prove a root cause in HA and does not replace the Product Owner's outstanding local HA Preview/regression acceptance. No HA runtime/product code was touched.
- PR #626/#623: B code is integrated at `ab38bfe`; T1 #38956508442 and exact T2 #38961262161 are green, with full-browser E2E skipped. That intermediate checkpoint still had the S3-07 coverage gap; test-only follow-up PR #628 below resolved it.
- PR #628/#623: deterministic S3-07 tests merged at `b58fd4857ee2c6ed84e40405148294cb910fddbd`. Exact candidate T1 #38963151780 attempt 2 and exact-merge T2 #38964817316 attempt 2 are green. T2 backend: Core674, PostgreSQL127, Historian41, Drivers1,578 passed/1 skipped/0 failed, Security42 (2,462 passed/1 skipped overall); Web build passed; Chromium integration smoke 13/13 passed; full-browser E2E skipped. T2's first attempt had one unrelated HA-D2 separate-db failure; a single failed-backend-job retry passed without code changes.
- PR #631/#630: Stage 4A merged at `51a4204de926ec2a81bdcbebacba8dbb578dbb71`; exact T1 #38970785957 and T2 #38971337239 passed. It adds a single-target caller-conditioned descriptive deviation; #630 closed for this scope.
- PR #633/#632: Stage 4B merged at `7f09c9f88af85e0b5c92a3cacbca867d9a67ee79`; exact T1 #38974826487 and exact T2 #38975551802 passed. T2 backend 2,500 passed/1 skipped, Web passed, Chromium integration smoke 13/13 passed; full-browser E2E skipped. Local focused analytics/API tests 198/198 and Release API build passed; #632 closed for its bounded scope.
- The residual Stage 4 audit is complete and recorded in #588. Main authorized Stage 4C only through #635 / ADR-019: a new v2 two-context query, additive to the verified product code checkpoint. Its exact implementation base must be revalidated after the ADR/handoff documentation PR; current product code remains `7f09c9f`.
- Stage 4 remains partial. Stage 5/6 and full Intelligence/Wave 15 closure remain unclaimed.

## Operating documents

1. [Global collaboration protocol](CHAT-COLLABORATION-PROTOCOL.md), including the incremental-read and handoff cycle.
2. [Main's current order and return contract](WAVE15-MAIN-COORDINATOR-HANDOFF.md).
3. [CI policy](CI-USAGE-POLICY.md) and the active mission's required profiles.
4. [State-independent bootstrap](NEXT-COORDINATOR-CHAT-HANDOFF.md) for recovery; [document authority map](README.md) for conflicting sources.

Read #297 first, #302 next when establishing the Wave/Authority baseline; then #305 and the active mission's latest comments. For an unchanged ongoing mission, check only changed material and the live decision inputs.

## Standing limits and pending owner validation

- Windows/Linux and the product's other declared platforms remain architectural scope.
- Intelligence remains licensed, selective, read-only analytics. Stage 3 is verified; Stage 4A/4B are verified only within their contracts; Stage 4 overall, Stage 5 visuals, and Stage 6 hardening remain separate/incomplete.
- HA acceptance raised by the Product Owner remains for the local Preview with the owner. The prior unsupervised HA-related work is a regression risk requiring that verification; a targeted code reversion may be necessary if owner/CODEX checks find problems. CI is not a substitute for that owner acceptance. Do not switch from Intelligence to HA without a new product-direction decision.
- Wave 15 and #588 remain open; bounded Stage 3/4A/4B acceptance is not whole-Intelligence or Wave 15 closure and makes no FND/FC0 claim.
- Stage 5 visuals, Stage 6 qualification, and the Product Owner's local HA Preview/regression acceptance remain separate and pending.

Historical snapshots: [previous current pointer](CURRENT-COORDINATOR-HANDOFF-ARCHIVE-2026-10-15.md), [previous Main orders](WAVE15-MAIN-COORDINATOR-HANDOFF-ARCHIVE-2026-10-15.md). They preserve evidence and are not active instructions.
