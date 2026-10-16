# Current coordinator pointer — Wave 15

Snapshot: 2026-10-16. Live refs, accepted ADRs, owning issues/PRs, and exact-SHA Actions evidence govern implementation state. Replace this snapshot when state changes; keep detailed history in the owning issue/PR.

## Current position

| Field | Current disposition |
| --- | --- |
| Integration target | `wave15/corrections-integration` |
| Current product checkpoint | `81ac4193194e1471e812e8164f9b9e15e63512ea` — PR #637 merge; exact post-merge T2 #38981435925 attempt 2 passed |
| Product front | Asset & Process Intelligence, issue #588; Stage 4C child #635 is closed for its bounded scope |
| Completed slices | Stage 3 S3-01–S3-08; PERFORMANCE-V1A/B; Stage 4A #630 / PR #631; Stage 4B #632 / PR #633; Stage 4C #635 / PR #637 |
| Current acceptance state | **Stage 3 COMPLETE / VERIFIED; Stage 4A, 4B, 4C INTEGRATED / VERIFIED / FROZEN within their scopes; Stage 4 PARTIAL** |
| Product executor | Chat CODEX; one read-only audit executor for the remaining Stage 4 gaps |
| Immediate direction | Audit remaining Stage 4 requirements in #588 against the current code and frozen contracts; return evidence and one bounded recommendation to Main |
| Next owner/action | CODEX inspects and reports; Main reviews the recommendation and decides whether to release a new implementation package |
| Audit boundary | Read-only. No product edits, contract changes, CI dispatch, Stage 5/6 implementation, HA, drivers, or other product front |

The current product tree is at `81ac4193194e1471e812e8164f9b9e15e63512ea`. Documentation-only movement creates a coordination HEAD, not a new product checkpoint. Revalidate live refs before any later mutation.

## Accepted evidence

- Workflow adjustment #618 and PERFORMANCE-V1A #621/#625 remain verified; PERFORMANCE-V1B #623/#626 remains integrated. Detailed run IDs and prior totals are retained in #588 and #305.
- Stage 3 S3-01–S3-08 is complete/verified after PR #628 at `b58fd4857ee2c6ed84e40405148294cb910fddbd`.
- Stage 4A #630 / PR #631 is integrated/verified at `51a4204de926ec2a81bdcbebacba8dbb578dbb71`; exact T1 #38970785957 and T2 #38971337239 passed.
- Stage 4B #632 / PR #633 is integrated/verified at `7f09c9f88af85e0b5c92a3cacbca867d9a67ee79`; exact T1 #38974826487 and T2 #38975551802 passed. Its v1 contract remains frozen.
- Stage 4C / ADR-019 / child #635 is integrated/verified/frozen within scope at PR #637 merge `81ac4193194e1471e812e8164f9b9e15e63512ea`. T1 #38978898568 attempt 3 passed on candidate head `fb848907362af97762180b70be9a7bd5b12fc020`; local focused analytics/API tests 206/206 and Release API build passed. Exact T2 #38981435925 attempt 2 passed at the merge SHA: backend tests and Runtime smoke, Web build, and Chromium integration smoke passed. Full-browser E2E was skipped and is not claimed.
- The first T2 attempt had one timeout in an unchanged Z-Wave fake-sidecar test. Docker Linux/SDK 10.0.400 reproductions passed the isolated test 3/3 and full Drivers suite 1,628 passed / 1 skipped; backend-only T2 retry passed without code/test changes. Root cause remains unestablished.
- Stage 4C adds only the bounded, read-only, caller-declared two-context vector v2 under ADR-019. It does not establish a healthy/representative reference, global regime equivalence, multivariate anomaly detection, diagnosis, or RUL. Stage 4 remains partial.

## Current limits and pending acceptance

- EliteSCADA remains multiplatform, including Windows and Linux. Do not narrow architecture to one operating system.
- Intelligence remains licensed, selective, and read-only. Stage 4A/4B/4C are accepted only within their written contracts; #588 remains open.
- Stage 5 visuals and Stage 6 hardening are separate and not part of the current read-only audit order. No next product implementation is released until Main reviews the audit and publishes a bounded package.
- The Product Owner's local HA Preview/regression acceptance remains separate and pending. Do not switch product front to HA without a new product-direction decision.
- No FND/FC0 or whole-Intelligence/Wave 15 completion is inferred from these bounded slices. Full-browser E2E was skipped in T2 and is not claimed.

## Operating documents

1. [Global collaboration protocol](CHAT-COLLABORATION-PROTOCOL.md), including incremental reads and handoff cadence.
2. [Main's current order and return contract](WAVE15-MAIN-COORDINATOR-HANDOFF.md).
3. [CI policy](CI-USAGE-POLICY.md) and any future mission's required profile.
4. [State-independent bootstrap](NEXT-COORDINATOR-CHAT-HANDOFF.md) and [document authority map](README.md).

Read #297 first and #302 next when establishing the Wave/Authority baseline, then #305 and the active mission's latest comments. For continuation, check changed material and live refs rather than replaying unchanged history.

Historical snapshots: [previous current pointer](CURRENT-COORDINATOR-HANDOFF-ARCHIVE-2026-10-15.md), [previous Main orders](WAVE15-MAIN-COORDINATOR-HANDOFF-ARCHIVE-2026-10-15.md). They preserve evidence and are not active instructions.
