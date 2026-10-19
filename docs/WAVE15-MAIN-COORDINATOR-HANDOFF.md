# Wave 15 — Main order and execution contract

Snapshot: 2026-10-19. This is the live Main-to-CODEX order. Replace this current order rather than stacking historical CURRENT/LATEST sections. Use [the short pointer](CURRENT-COORDINATOR-HANDOFF.md) for the accepted product checkpoint and evidence.

## Current order — Stage 4E operating contexts (#652); owner Preview in parallel

| Field | Order |
| --- | --- |
| Owner / executor | Main Coordinator publishes and activates; existing chat CODEX implements one package after activation |
| Parent / target | Intelligence #588 / `wave15/corrections-integration` |
| Mission | `W15-INTELLIGENCE-4E-OPERATING-CONTEXT-01`, issue #652; [ADR-022](ADR-022-INTELLIGENCE-OPERATING-CONTEXT-PROFILES.md) and [repository scope](WAVE15-INTELLIGENCE-STAGE4E-OPERATING-CONTEXT.md) |
| Product checkpoint | PR #651 / Stage 4D merge `dd67bac5c4ca937e028b0fb9890b00dfeedd03a4`; exact T1 #39321066819 and T2 #39321693285 passed |
| Dispatch base | Candidate integration `dd67bac5c4ca937e028b0fb9890b00dfeedd03a4`; documentation order is under publication. Main records and revalidates the exact activated base in #652 after that merge; do not self-rebase to another product tree |
| Preview / start condition | Stable Preview remains http://localhost:18080 at `0054d587`. Owner ordered development during evaluation; #639 visual acceptance and the requested Full license are separate. License installation still awaits an authenticated Preview administrator session; do not reset/redeploy Preview. CODEX starts 4E only after Main's exact-base activation |
| Planned branch | `work/w15-intelligence-stage4e-operating-context`, clean isolated checkout; preserve unrelated changes |
| Objective | Add versioned operator-authored operating-context profiles and read-only context eligibility for caller-selected windows |
| Scope | Engineering config v3, `.escadapkg` schema 28 compatibility, localized authoring UI, stable profile version, typed required predicates and `context-eligibility.v1`. Only observations matching every declared predicate at the same exact synchronized UTC source timestamp are eligible. No score or inferred/physical regime claim |
| Canonical reuse | Existing measurement profiles, Equipment assignments, TAG identities, Engineering validator/API/import-export, authoring workspace, Historian v2 evidence reader, and canonical entitlement/scope/TrendUse/TagRead/permit/Authority/HA fences |
| Resource limits | <=16 signals/predicates; <=31-day reference and optional later comparison windows; 200-row pages; 50,000 raw Historian rows aggregate; fail closed with no partial response |
| Forbidden | Changes to frozen 4A–4D contracts, 4F joint score/calibration, 4G closure, 5B/6, HA, drivers, licensing, AI/learning, SQL/worker, imputation, process writes or automatic Preview deployment |
| Local/T1 evidence owner | CODEX: focused domain/API/UI/package/security/version/cursor/limit/frozen-regression tests and Release API/Web builds on isolated resources; one draft PR, `VALIDATION_PROFILE: UI_EDITOR` plus inferred backend profiles, exact-head T1 and explicit skipped evidence |
| Main acceptance | Main reviews semantics, persistence/import compatibility, diff and local/T1 evidence; conditionally merges under existing owner authorization and verifies exact-merge T2. Close only 4E after named criteria pass; no self-merge or automatic 4F |
| Next package | Main releases the separate 4F method/data gate only after 4E acceptance, then 4G. Stage5B/6 remain unreleased until Stage4 residual disposition |

### Stable Preview isolation

- The owner authorized recreating only the old stable test stack/data for this initial deployment. CODEX delivered fresh bootstrap; no default password. URL http://localhost:18080; product `0054d587fe855bbea4b1af81726af016208643da`; Compose project `elitescada-stable`; checkout `C:/Users/Bruno Rogerio/Documents/Codex/EliteSCADA-stable-preview-20261019`.
- After delivery, do not reset, migrate or redeploy that evaluation revision/data automatically. New implementation/tests use a separate checkout, DB/Compose project, volumes/ports and process resources. Preserve `local-timescaledb-1` and other user projects. Existing stable secrets remain local and unpublished.
- #639 remains INTEGRATED / TECHNICALLY VERIFIED / OWNER VISUAL PREVIEW PENDING. Parallel development is not implicit visual acceptance/freeze. User findings become bounded reviewed corrections; do not stop development merely waiting for feedback.
- The initial T2 Authority-policy timeout and successful unchanged-SHA retry remain recorded in #639. No HA fix or HA acceptance is inferred. HA manual regression/possible rollback review stays separate.

Owner-authorized issue hygiene closed 27 fulfilled/retired trackers; see [the audit](WAVE15-ISSUE-AUDIT-2026-10-19.md). Physical driver L4 is human testing outside GitHub control. Do not reopen accepted drivers/visual foundations from stale bootstraps.

### Context and frozen boundaries

- Stage 3 S3-01–S3-08 is complete/verified. Stage 4A adds a descriptive single-target deviation against a caller-selected reference and context. Stage 4B adds independent per-target deviations over complete synchronized co-observations. Stage 4C adds the separate additive `contextual-deviation-vector.v2` with exactly two caller-declared numeric context intervals and 2–8 target TAGs. The Product Owner approved the V1 acceptance boundary after the read-only audit: the caller selects and is responsible for the known-good reference period; the system does not certify health, representativeness or global regime equivalence; the vector means independent per-TAG scores, not a joint multivariate model. **This accepts and freezes Stage 4 V1 only; broader Stage 4 in #588 remains PARTIAL.** The V1 boundary is not evidence that the original Stage 4 residuals were implemented or permanently waived.
- Stage 4C / ADR-019 / child #635 is integrated, verified and frozen only within its bounded scope at PR #637 merge `81ac4193194e1471e812e8164f9b9e15e63512ea`. Exact candidate T1 #38978898568 attempt 3 passed; exact post-merge T2 #38981435925 attempt 2 passed at the merge SHA. The backend-only retry passed after local Linux reproduction of its unchanged timing-sensitive Z-Wave test. Build/runtime smoke, Web build and Chromium integration smoke passed; full-browser E2E was skipped and is not claimed. Detailed counts and caveats are in #588/#635/#305 and PR #637.
- ADR-019 and ADR-020 preserve non-claims: no automatic reference health, no global regime equivalence, no joint multivariate anomaly detection, diagnosis or RUL. Do not alter these limits in Stage 5A.
- ADR-021 was initially a scope proposal (PR #643/T1 #38992226246); the owner authorized sequential development on 2026-10-19, [recorded in #588](https://github.com/brunolrogerio-collab/EliteSCADA/issues/588#issuecomment-6267755298). The same-day clarification permits Stage 4D #649 after Stage5A technical verification and stable Preview delivery, without waiting for manual visual acceptance. Then Main releases bounded 4E–4G before 5B/6. Statistical method/threshold claims still require representative-data validation. Preserve caller-selected reference and frozen 4A–4C contracts; broader Stage4 is not complete until all residuals pass or are explicitly deferred.
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

For Stage 4E, return one implementation report in this form:

```text
CODEX -> MAIN COORDINATOR
Package / owning issue: Stage 4E operating contexts / #652
Status / next owner:
Exact base SHA and product tree; PR head SHA:
Files/symbols changed and canonical Engineering/package/UI/analytics/guard paths reused:
Configuration v3/schema 28, profile versioning, typed predicates and comparison eligibility:
Bounds, cancellation/authorization/no-partial and frozen 4A–4D regression evidence:
Confirmation: stable owner Preview revision/data/resources untouched:
Focused local commands/results:
VALIDATION_PROFILE and exact T1 run/job links:
Risks/limitations and explicit non-claims:
Confirmation: no 4F/4G, Stage 5B/6, HA/drivers, AI, SQL/worker or self-merge
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
