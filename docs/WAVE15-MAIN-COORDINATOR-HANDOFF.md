# Wave 15 — Main order and execution contract

Snapshot: 2026-10-19. This is the live Main-to-CODEX order. Replace this current order rather than stacking historical CURRENT/LATEST sections. Use [the short pointer](CURRENT-COORDINATOR-HANDOFF.md) for the accepted product checkpoint and evidence.

## Current order — Stage 4D reference evidence (#649); owner Preview in parallel

| Field | Order |
| --- | --- |
| Owner / executor | Main Coordinator / existing chat CODEX, one product implementation package |
| Parent / target | Intelligence #588 / `wave15/corrections-integration` |
| Mission | `W15-INTELLIGENCE-4D-REFERENCE-EVIDENCE-01`, issue #649; [repository scope](WAVE15-INTELLIGENCE-STAGE4D-REFERENCE-EVIDENCE.md) |
| Product checkpoint | PR #647 merge `0054d587fe855bbea4b1af81726af016208643da`; T1 #39258159241 / T2 #39259192178 attempt 2 passed |
| Dispatch base | Main revalidated coordination ref `15697b30e2633510198fca0285df633156f7f338`; the exact activated base after this docs-only order merge is recorded in #649. Do not self-rebase to an unreviewed product tree |
| Preview / start condition | CODEX delivered healthy stable Docker Preview http://localhost:18080 on product `0054d587`. Owner explicitly ordered development during evaluation; #639 visual acceptance remains pending, not a 4D blocker. Begin after Main publishes/activates this exact-base order |
| Planned branch | `work/w15-intelligence-stage4d-reference-evidence`, clean isolated checkout; preserve unrelated changes |
| Objective | Add a bounded versioned read-only reference-evidence/data-sufficiency report using canonical active assignment/profile and Historian v2 |
| Scope | Server-derived reference-submission actor/time; exact project/revision/scope/assignment/profile/TAG/window/query provenance; per-signal quality/rejection/missing evidence; exact synchronized source-cohort counts; explicit Required/Optional insufficiency and unknown cadence/completeness. No claim of healthy reference or model validity |
| Canonical reuse | Analytics API/service, profile coverage and v2 observation/revalidation helpers; existing execution permit, entitlement, TrendUse and TAG read guards. No duplicate query/identity/store authority |
| Resource limits | <=16 configured profile signals; <=31-day reference and optional later comparison window; 200-row pages; one 50,000-row aggregate cap across selected signals/windows; no partial/unauthorized result |
| Forbidden | Frozen 4A–4C/ADR-020 behavior changes, new persisted context profiles (4E), joint score (4F), broad Stage4 closure (4G), dashboards5B, Stage6, HA, drivers, licensing changes, SQL/store/schema/worker, imputation, process writes or automatic Preview deployment |
| Local/T1 evidence owner | CODEX: focused calculator/service/API/security/cursor/limit/frozen-regression tests and Release API build, isolated test resources; one draft PR with `VALIDATION_PROFILE: SESSION_LICENSING`, exact-head T1 and limitations |
| Main acceptance | Main reviews requirements/diff/local/T1; conditionally integrates under existing owner authorization and verifies exact-merge T2. 4D closes only after its named criteria pass; no automatic next-stage/self-merge by worker |
| Next package | Main releases bounded 4E after accepted 4D, then 4F method/data gate and 4G. Stage5B/6 remain unreleased until Stage4 residual disposition |

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

For Stage 4D, return one implementation report in this form:

```text
CODEX -> MAIN COORDINATOR
Package / owning issue: Stage 4D reference evidence / #649
Status / next owner:
Exact base SHA and product tree; PR head SHA:
Files/symbols changed and canonical analytics/query/guard path reused:
Authenticated provenance, per-signal evidence, required/optional and cohort semantics:
Bounds, cancellation/authorization/no-partial and frozen 4A–4C regression evidence:
Confirmation: stable owner Preview revision/data/resources untouched:
Focused local commands/results:
VALIDATION_PROFILE and exact T1 run/job links:
Risks/limitations and explicit non-claims:
Confirmation: no 4E/4F/4G, Stage 5B/6, HA/drivers, store/schema or self-merge
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
