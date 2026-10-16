# Current coordinator pointer — Wave 15

Snapshot: 2026-10-16. Replace this snapshot when state changes; keep detailed history in the owning issue/PR. Live refs, published code, and exact-SHA Actions evidence govern implementation state.

## Current position

| Field | Current disposition |
| --- | --- |
| Integration target | `wave15/corrections-integration` |
| Current product checkpoint | `ab38bfe879d6ca8074aa5c38b821c7da62634768` — #626 merge; exact T2 green |
| Product front | Asset & Process Intelligence, issue #588; Stage 3 partially delivered |
| Completed slice | `current-at-recorded-frequency.compare.v1` — INTEGRATED / VERIFIED |
| Verified prerequisite | PERFORMANCE-V1A #621 / PR #622 — INTEGRATED / VERIFIED at `257a4ab3`, exact T2 #38943570688 |
| Integrated slice | PERFORMANCE-V1B #623 / PR #626 at `ab38bfe`; T1 #38956508442 and exact post-merge T2 #38961262161 green |
| Current acceptance state | **INTEGRATED / CI GREEN / ACCEPTANCE FOLLOW-UP REQUIRED**; post-merge audit found missing S3-07 per-input/two-vs-three-input revocation coverage |
| Product executor | Chat CODEX; one test-only follow-up dispatched from #623 |
| Immediate direction | Close the finite S3-07 deterministic security-test matrix; do not change frozen product behavior |
| Next owner/action | CODEX publishes the single draft test-only PR from exact base `ab38bfe879d6ca8074aa5c38b821c7da62634768`; Main reviews and requires exact T1 before integration and exact T2 afterward |
| Allowed path | `tests/Scada.Drivers.Tests/AssetProcessIntelligenceAnalyticsTests.cs` only |

Coordination-only commits after the product checkpoint do not invalidate its product evidence. Discover the current coordination HEAD from the live integration ref.

Workflow adjustment #618 is complete. [ADR-015](ADR-015-INTELLIGENCE-CURRENT-FREQUENCY-COMPARISON.md) records accepted #619; [ADR-016](ADR-016-INTELLIGENCE-PERFORMANCE-V1.md) fixes the physical-input/formula scope. A (#621) is verified; B's three fixed formulas are integrated, but the exact S3-07 acceptance matrix is still open. See #623, parent #588, and cross-mission ledger #305. This order does not activate HA or Stage 4–6.

## Evidence

- #619/#620: local Analytics 78/78, T1 #38832922847 attempt2 and post-merge #38834562474; accepted `current-at-recorded-frequency.compare.v1` remains frozen within its meaning.
- #621 / PR #622: A integrated at `a96b49b`; its broad post-merge CI initially failed in two HA-D2 separate-database timing cases. Test-only correction PR #625 is integrated at `257a4ab3`; [T1 #38942922868](https://github.com/brunolrogerio-collab/EliteSCADA/actions/runs/38942922868) and exact-SHA [T2 #38943570688](https://github.com/brunolrogerio-collab/EliteSCADA/actions/runs/38943570688) are green. T2 totals: Core674, PostgreSQL127, Timescale41, Drivers1,519 passed/1 skipped, Security42; Web build and Chromium integration smoke13/13 passed.
- #625 changed only the test fixture wait/request timeout. It does not prove a root cause in HA and does not replace the Product Owner's outstanding local HA Preview/regression acceptance. No HA runtime/product code was touched.
- PR #626/#623: B code is integrated at `ab38bfe`; T1 #38956508442 and exact T2 #38961262161 are green, with full-browser E2E skipped. Main's post-merge audit found the stated S3-07 coverage gap; only the test-only follow-up is active. Stage3 remains partial; #588 and Wave15 remain open.

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
- Wave 15 and #588 remain open; this workflow update makes no FND/FC0 or whole-stage completion claim.
- Modbus is not part of the active order; the Product Owner classifies the previous isolated failure as recurring timing, and no driver change was made.

Historical snapshots: [previous current pointer](CURRENT-COORDINATOR-HANDOFF-ARCHIVE-2026-10-15.md), [previous Main orders](WAVE15-MAIN-COORDINATOR-HANDOFF-ARCHIVE-2026-10-15.md). They preserve evidence and are not active instructions.
