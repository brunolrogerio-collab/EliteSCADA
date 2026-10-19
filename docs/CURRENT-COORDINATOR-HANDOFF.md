# Current coordinator pointer — Wave 15

Snapshot: 2026-10-19. Live refs, accepted ADRs, owning issues/PRs, and exact-SHA Actions evidence govern implementation state. Replace this snapshot when state changes; keep detailed history in the owning issue/PR.

## Current position

| Field | Current disposition |
| --- | --- |
| Integration target | `wave15/corrections-integration` |
| Product code checkpoint | `0054d587fe855bbea4b1af81726af016208643da` — PR #647 merge; exact post-merge T2 #39259192178 attempt 2 passed |
| Stage 5A source evidence | Dispatch base `9ad4756c07f3a848750ea10cd2a14af7b63f8e6d`; final PR head `6c8991a53f22dbc3f509e83dcfcf722257dd60ae`; exact T1 #39258159241 passed |
| Product front | Asset & Process Intelligence, parent issue #588; next bounded child #639 |
| Completed slices | Stage 3 S3-01–S3-08; PERFORMANCE-V1A/B; Stage 4A #630 / PR #631; Stage 4B #632 / PR #633; Stage 4C #635 / PR #637 |
| Current acceptance state | **Stage 3 and Stage 4 V1 COMPLETE / VERIFIED / FROZEN within accepted scopes; broader Stage 4 remains PARTIAL**; Stage 5A integrated/technically verified, owner visual Preview pending; 5B and 6 not released |
| Product executor | Chat CODEX; one active product package at a time |
| Immediate direction | Main accepted/merged PR #647 and verified T2. CODEX completed the isolated failed-test diagnostic without HA changes and now waits; no duplicate CI or implementation is ordered |
| Next owner/action | Product Owner visual Preview remains before #639 acceptance/freeze. Next development remains ADR-021: Main dispatches bounded 4D after the existing #639 gates and exact-base revalidation, then 4E–4G before Stage 5B/6 |
| Package boundary | Six generic display-only measurement widgets in the canonical editor toolbox. No Stage 5B dashboard generation, Stage 6, HA, drivers, or unrelated front |

The last technically verified product code checkpoint is `0054d587fe855bbea4b1af81726af016208643da`. Documentation-only movement creates a coordination HEAD, not a new product checkpoint. Revalidate the live integration ref before any later product package. [The issue audit](WAVE15-ISSUE-AUDIT-2026-10-19.md) records 27 owner-authorized closures and the remaining acceptance/functionality split; physical driver L4 is no longer GitHub-controlled tracking.

## Accepted evidence

- Stage 5A #639 / PR #647 is integrated at `0054d587fe855bbea4b1af81726af016208643da`. Main accepted the six types, canonical persistence/loaded-model Preview/Runtime, applied themes, accessible band limits and complete unit labels. Local evidence: Web build, 50/50 tests across seven routed browser specs, focused domain 7/7 and PostgreSQL schema 3/3. Exact-head T1 #39258159241 passed. Exact-merge T2 #39259192178 attempt 2 passed: backend build/test/Runtime smoke, Web build and Chromium integration smoke; full-browser E2E was skipped, not passed. Manual visual acceptance is still pending.
- T2 attempt 1 timed out waiting for the initial two-API Authority policy commit with separate databases. CODEX reproduced the exact case locally: merge 3/3 passed, base `65479ec5c92fec7c6d9c49f1e9001689b3743fe0` 1/1 passed. Relevant HA/Authority/test files are unchanged; this establishes no direct delta, not absence of all runner interactions. The initial policy remained pending even after standby readiness. Non-2xx peer response diagnostics are incomplete, so the cause is still unknown. Main requested one backend-job retry, which passed without code/test changes. No HA fix, timeout relaxation or HA acceptance is claimed. Detailed diagnostic is in [#639](https://github.com/brunolrogerio-collab/EliteSCADA/issues/639#issuecomment-6268543313).
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
- Stage 5A #639 is integrated/technically verified: six generic reusable measurement displays in the canonical editor toolbox, independent of Intelligence license, display-only, accessible, and persisted/rendered through existing screen contracts. Keep #639 open for owner visual Preview. It does not close the broader Stage 4. Guided dashboard generation is Stage 5B and is not included. Stage 6 remains pending.
- [ADR-021](ADR-021-INTELLIGENCE-STAGE4-RESIDUAL-CLOSURE-SCOPE.md) is the owner-authorized next development scope as of 2026-10-19: reference evidence, explicit operating profiles, a separately validated joint score, and a closure evidence gate. [Owner order in #588](https://github.com/brunolrogerio-collab/EliteSCADA/issues/588#issuecomment-6267755298). After #639 acceptance Main must revalidate the exact base and activate bounded 4D, then 4E–4G, before Stage 5B/6. Method/threshold validity still requires the data gate. Do not alter frozen 4A–4C contracts or claim whole Stage 4 complete before its accepted criteria pass.
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
