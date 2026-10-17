# Current coordinator pointer — Wave 15

Snapshot: 2026-10-17. Live refs, accepted ADRs, owning issues/PRs, and exact-SHA Actions evidence govern implementation state. Replace this snapshot when state changes; keep detailed history in the owning issue/PR.

## Current position

| Field | Current disposition |
| --- | --- |
| Integration target | `wave15/corrections-integration` |
| Product code checkpoint | `81ac4193194e1471e812e8164f9b9e15e63512ea` — PR #637 merge; exact post-merge T2 #38981435925 attempt 2 passed |
| Stage 5A implementation base | Exact dispatch base `9ad4756c07f3a848750ea10cd2a14af7b63f8e6d`, recorded in #639 after docs PR #641; product code checkpoint remains `81ac4193194e1471e812e8164f9b9e15e63512ea` |
| Product front | Asset & Process Intelligence, parent issue #588; next bounded child #639 |
| Completed slices | Stage 3 S3-01–S3-08; PERFORMANCE-V1A/B; Stage 4A #630 / PR #631; Stage 4B #632 / PR #633; Stage 4C #635 / PR #637 |
| Current acceptance state | **Stage 3 and Stage 4 V1 COMPLETE / VERIFIED / FROZEN within accepted scopes; broader Stage 4 remains PARTIAL**; Stage 5A active, 5B and 6 not released |
| Product executor | Chat CODEX; one active product package at a time |
| Immediate direction | CODEX completes only active Stage 5A #639 on its authorized exact base; Main then reviews and integrates subject to its stated gates |
| Next owner/action | CODEX returns one Stage 5A PR; Main reviews, integrates and verifies exact T2, then Product Owner performs manual Preview. Main is preparing the Stage 4 residual proposal concurrently in ADR-021; it does not activate Stage 4 code or change #639. After #639 acceptance Main may issue bounded 4D; Stage 5B/6 remain blocked until residual disposition |
| Package boundary | Six generic display-only measurement widgets in the canonical editor toolbox. No Stage 5B dashboard generation, Stage 6, HA, drivers, or unrelated front |

The last verified product code tree is at `81ac4193194e1471e812e8164f9b9e15e63512ea`. Documentation-only movement creates a coordination HEAD, not a new product checkpoint. The live integration ref was revalidated at exact Stage 5A dispatch base `9ad4756c07f3a848750ea10cd2a14af7b63f8e6d` and recorded in #639; revalidate again before any later product package.

## Accepted evidence

- Workflow adjustment #618 and PERFORMANCE-V1A #621/#625 remain verified; PERFORMANCE-V1B #623/#626 remains integrated. Detailed run IDs and prior totals are retained in #588 and #305.
- Stage 3 S3-01–S3-08 is complete/verified after PR #628 at `b58fd4857ee2c6ed84e40405148294cb910fddbd`.
- Stage 4A #630 / PR #631 is integrated/verified at `51a4204de926ec2a81bdcbebacba8dbb578dbb71`; exact T1 #38970785957 and T2 #38971337239 passed.
- Stage 4B #632 / PR #633 is integrated/verified at `7f09c9f88af85e0b5c92a3cacbca867d9a67ee79`; exact T1 #38974826487 and T2 #38975551802 passed. Its v1 contract remains frozen.
- Stage 4C / ADR-019 / child #635 is integrated/verified/frozen within scope at PR #637 merge `81ac4193194e1471e812e8164f9b9e15e63512ea`. T1 #38978898568 attempt 3 passed on candidate head `fb848907362af97762180b70be9a7bd5b12fc020`; local focused analytics/API tests 206/206 and Release API build passed. Exact T2 #38981435925 attempt 2 passed at the merge SHA: backend tests and Runtime smoke, Web build, and Chromium integration smoke passed. Full-browser E2E was skipped and is not claimed.
- The first T2 attempt had one timeout in an unchanged Z-Wave fake-sidecar test. Docker Linux/SDK 10.0.400 reproductions passed the isolated test 3/3 and full Drivers suite 1,628 passed / 1 skipped; backend-only T2 retry passed without code/test changes. Root cause remains unestablished.
- The read-only residual audit is recorded in #588; it found reference health/representativeness and global regime equivalence are not measured, while v2 computes independent per-TAG scores over complete synchronized co-observations. Product Owner approved Main's bounded acceptance interpretation: users select and take responsibility for the reference/context; the system makes no automatic health/equivalence claim; the vector is not a joint multivariate model. Main recorded **Stage 4 V1** COMPLETE / VERIFIED / FROZEN within this boundary in #588 and #305; ADR-020 formalizes that limited decision. **The broader Stage 4 remains PARTIAL.**

## Current limits and pending acceptance

- EliteSCADA remains multiplatform, including Windows and Linux. Do not narrow architecture to one operating system.
- Intelligence remains licensed, selective, and read-only. Stage 4 V1 is accepted only within the explicit caller-selected reference/context and per-TAG descriptive-vector boundaries; #588 remains open.
- Stage 5A #639 is the active bounded package: six generic reusable measurement displays in the canonical editor toolbox, independent of Intelligence license, display-only, accessible, and persisted/rendered through existing screen contracts. It does not close the broader Stage 4. Guided dashboard generation is Stage 5B and is not included. Stage 6 remains pending.
- [ADR-021](ADR-021-INTELLIGENCE-STAGE4-RESIDUAL-CLOSURE-SCOPE.md) is a coordination-only proposal for Stage 4 residuals: reference evidence, explicit operating profiles, a separately validated joint score, and a closure evidence gate. It was prepared while #639 remains active; it is not yet a PO-approved model/threshold and does not authorize implementation. After #639 is accepted, Main must revalidate the exact base and activate the bounded next package. Do not alter frozen 4A–4C contracts. Do not claim whole Stage 4 complete until accepted residual criteria are met or explicitly deferred by the Product Owner.
- Product Owner manual visual Preview is required after Stage 5A integration and before that slice is marked accepted/frozen. CI does not substitute for this Preview.
- The Product Owner's local HA Preview/regression acceptance remains separate and pending. Do not switch product front to HA without a new product-direction decision.
- No FND/FC0 or whole-Intelligence/Wave 15 completion is inferred from these bounded slices. Full-browser E2E was skipped in T2 and is not claimed.

## Operating documents

1. [Global collaboration protocol](CHAT-COLLABORATION-PROTOCOL.md), including incremental reads and handoff cadence.
2. [Main's current order and return contract](WAVE15-MAIN-COORDINATOR-HANDOFF.md).
3. [CI policy](CI-USAGE-POLICY.md) and any future mission's required profile.
4. [State-independent bootstrap](NEXT-COORDINATOR-CHAT-HANDOFF.md) and [document authority map](README.md).

Read #297 first and #302 next when establishing the Wave/Authority baseline, then #305 and the active mission's latest comments. For continuation, check changed material and live refs rather than replaying unchanged history.

Historical snapshots: [previous current pointer](CURRENT-COORDINATOR-HANDOFF-ARCHIVE-2026-10-15.md), [previous Main orders](WAVE15-MAIN-COORDINATOR-HANDOFF-ARCHIVE-2026-10-15.md). They preserve evidence and are not active instructions.
