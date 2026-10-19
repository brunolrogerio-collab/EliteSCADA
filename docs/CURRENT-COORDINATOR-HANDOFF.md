# Current coordinator pointer — Wave 15

Snapshot: 2026-10-19. Live refs, accepted ADRs, owning issues/PRs, and exact-SHA Actions evidence govern implementation state. Replace this snapshot when state changes; keep detailed history in the owning issue/PR.

## Current position

| Field | Current disposition |
| --- | --- |
| Integration target | `wave15/corrections-integration` |
| Product code checkpoint | `dd67bac5c4ca937e028b0fb9890b00dfeedd03a4` — Stage 4D PR #651 merge; exact post-merge T2 #39321693285 passed |
| Stage 5A source evidence | Dispatch base `9ad4756c07f3a848750ea10cd2a14af7b63f8e6d`; final PR head `6c8991a53f22dbc3f509e83dcfcf722257dd60ae`; exact T1 #39258159241 passed |
| Product front | Asset & Process Intelligence #588; Stage 4D #649 closed after verification; next bounded child Stage 4E #652; #639 owner Preview continues in parallel |
| Completed slices | Stage 3 S3-01–S3-08; PERFORMANCE-V1A/B; Stage 4A #630 / PR #631; Stage 4B #632 / PR #633; Stage 4C #635 / PR #637; Stage 4D #649 / PR #651 |
| Current acceptance state | **Stage 3 and Stage 4A–4D COMPLETE / VERIFIED / FROZEN within accepted scopes; broader Stage 4 remains PARTIAL**; Stage 5A integrated/technically verified, owner visual Preview pending; 5B and 6 not released |
| Product executor | Chat CODEX; one active product package at a time |
| Immediate direction | 4D is integrated and verified at `dd67bac5`; Main has prepared Stage 4E ADR-022/issue #652. Publish the exact-base order, then activate CODEX on the revalidated documentation merge base |
| Next owner/action | Main publishes and activates #652; CODEX implements 4E only in isolated resources. Owner evaluates #639/Preview independently; the Full ESLIC3 license is installed and verified, with Runtime Idle and Intelligence scopes/permits not activated. Main then releases 4F method/data gate and 4G before 5B/6 |
| Package boundary | Config v3 and `.escadapkg` schema 28 context profiles, localized authoring UI, versioned typed predicates and bounded read-only eligibility report. No 4F score, frozen 4A–4D changes, 4G, 5B/6, HA, drivers, licensing changes or stable-Preview redeployment |

The last technically verified product code checkpoint is `dd67bac5c4ca937e028b0fb9890b00dfeedd03a4`. Documentation-only movement creates a coordination HEAD, not a new product checkpoint. Revalidate the live integration ref before dispatching 4E. [The issue audit](WAVE15-ISSUE-AUDIT-2026-10-19.md) records 27 owner-authorized closures and the remaining acceptance/functionality split; physical driver L4 is no longer GitHub-controlled tracking.

## Accepted evidence

- Stage 5A #639 / PR #647 is integrated at `0054d587fe855bbea4b1af81726af016208643da`. Main accepted the six types, canonical persistence/loaded-model Preview/Runtime, applied themes, accessible band limits and complete unit labels. Local evidence: Web build, 50/50 tests across seven routed browser specs, focused domain 7/7 and PostgreSQL schema 3/3. Exact-head T1 #39258159241 passed. Exact-merge T2 #39259192178 attempt 2 passed: backend build/test/Runtime smoke, Web build and Chromium integration smoke; full-browser E2E was skipped, not passed. Manual visual acceptance is still pending.
- Stage 4D #649 / PR #651 is integrated and verified at `dd67bac5c4ca937e028b0fb9890b00dfeedd03a4`. Main caught and required a 50,001st-row budget fix before merge. Local boundary tests passed 2/2, Intelligence tests 231/231, Release API build 0 warnings/errors. Exact final candidate T1 #39321066819 passed on `3bbd2bda507516ae2b61423d3c7eb6aa0ac9d36e` (1,648 .NET passed / 1 skipped / 0 failed; classifier/sanity/gate passed; other profiles skipped by SESSION_LICENSING). Exact-merge T2 #39321693285 passed: Backend build/test/smoke, Web build and Chromium integration gate. #649 closed; broader #588 remains PARTIAL.
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
- [ADR-021](ADR-021-INTELLIGENCE-STAGE4-RESIDUAL-CLOSURE-SCOPE.md) remains the owner-authorized scope: reference evidence, operating profiles, joint score and closure evidence. [Original sequence](https://github.com/brunolrogerio-collab/EliteSCADA/issues/588#issuecomment-6267755298). Owner clarified the same day that Main activates bounded 4D #649 after technical verification and stable Preview delivery, without waiting for #639 visual acceptance; then 4E–4G before 5B/6. Preserve frozen 4A–4C; method/threshold validation and broader Stage 4 acceptance remain required.
- ADR-022 and child #652 now define the next bounded 4E package: persisted operator-authored context profiles, typed predicates and context eligibility. Main publishes the design/scope before dispatch; 4F and 4G remain separate and sequential.
- Product Owner manual visual Preview is required after Stage 5A integration and before that slice is marked accepted/frozen. CI does not substitute for this Preview.
- That visual gate is not a development blocker per the owner's 2026-10-19 instruction. Keep Preview `elitescada-stable` at `0054d587fe855bbea4b1af81726af016208643da`, URL http://localhost:18080, with delivered data/volumes/ports intact during evaluation. Development/tests use isolated resources. The owner directly authorized the initial test-stack reset, not continuing resets/redeploys. Fresh first access creates the administrator; no default password. Checkout: `C:/Users/Bruno Rogerio/Documents/Codex/EliteSCADA-stable-preview-20261019`.
- The requested Full ESLIC3 license is installed and valid: Unlimited TAGs, 2 Interactive + 2 View Only seats, HA Runtime, and Asset & Process Intelligence; expires 2026-11-18. The Runtime remains Idle and no Intelligence scope/permit or HA configuration was activated. Non-secret installation evidence is in [#639](https://github.com/brunolrogerio-collab/EliteSCADA/issues/639#issuecomment-6269312804); never publish the signed payload or private key.
- The Product Owner's local HA Preview/regression acceptance remains separate and pending. Do not switch product front to HA without a new product-direction decision.
- No FND/FC0 or whole-Intelligence/Wave 15 completion is inferred from these bounded slices. Full-browser E2E was skipped in T2 and is not claimed.

## Operating documents

1. [Global collaboration protocol](CHAT-COLLABORATION-PROTOCOL.md), including incremental reads and handoff cadence.
2. [Main's current order and return contract](WAVE15-MAIN-COORDINATOR-HANDOFF.md).
3. [CI policy](CI-USAGE-POLICY.md) and any future mission's required profile.
4. [State-independent bootstrap](NEXT-COORDINATOR-CHAT-HANDOFF.md) and [document authority map](README.md).

Read #297 first and #302 next when establishing the Wave/Authority baseline, then #305 and the active mission's latest comments. For continuation, check changed material and live refs rather than replaying unchanged history.

Historical snapshots: [previous current pointer](CURRENT-COORDINATOR-HANDOFF-ARCHIVE-2026-10-15.md), [previous Main orders](WAVE15-MAIN-COORDINATOR-HANDOFF-ARCHIVE-2026-10-15.md). They preserve evidence and are not active instructions.
