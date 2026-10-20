# Wave 15 — Main order and execution contract

Snapshot: 2026-10-20. This is the live Main-to-CODEX order. Replace this current order rather than stacking historical CURRENT/LATEST sections. Use [the short pointer](CURRENT-COORDINATOR-HANDOFF.md) for the accepted product checkpoint and evidence.

## Current status — Stage 4F public-source qualification #659; readiness remains NOT READY

| Field | Order |
| --- | --- |
| Owner / executor | CODEX owns supplemental source qualification #659; Main owns review and, under owner delegation on 2026-10-20, has defined the initial scope/acceptance/data-handling decisions |
| Parent / target | Intelligence #588 / `wave15/corrections-integration` |
| Mission | `W15-INTELLIGENCE-4F-PUBLIC-DATA-QUALIFICATION-01`, [#659](https://github.com/brunolrogerio-collab/EliteSCADA/issues/659): bounded inspection/isolated descriptive analysis of UCI MetroPT-3 and hydraulic-system sources. Completed #655 remains NOT READY; [ADR-021 §4F–4G](ADR-021-INTELLIGENCE-STAGE4-RESIDUAL-CLOSURE-SCOPE.md) remains binding |
| Accepted product checkpoint | Stage 4E PR #654 merge `793b8303547491e02b98bf73732d3ab5ab76567a`; exact T1 #39331697007 attempt 2 and exact-merge T2 #39336277009 passed |
| Inspected integration base | `wave15/corrections-integration@2803135cdd68e204b2afde03d5e6f675d495c3fa` includes docs-only PR #658 over the unchanged accepted product. Revalidate live ref and checkout cleanliness; no product mutation is assigned |
| Owner Preview | Preserve `http://localhost:18080`, product `0054d587fe855bbea4b1af81726af016208643da`, data/volumes/ports. Owner evaluation and #639 visual acceptance remain separate. Do not reset or redeploy Preview |
| Gate result | #655 assessed NOT READY at audit time. Main's later delegated acceptance plan resolves scope/bounds/research-use choices; representative-data eligibility and actual score performance remain unproved. Demo config contains no history; existing analytics tests use synthetic inline vectors |
| Objective | Determine whether either public source supports an offline method study or the existing synchronized-cohort contract, and identify missing evidence. Separate source assertions, measured facts and unknowns. Joint score/calibration remains NOT DELIVERED / NOT VALIDATED |
| Defined scope/targets | Apply [Main's acceptance plan](WAVE15-INTELLIGENCE-STAGE4F-ACCEPTANCE-PLAN.md): motor-driven pumping sets first, compressors as second validation case; conservative advisory objectives, exposure/uncertainty, grouped temporal evaluation and local public-source retention. The blank collection package remains a reusable form; the owner need not fill statistical targets again |
| Stop/continue | Research only. Pin source identity/license; inspect actual timestamps/cycles, rates, gaps, labels and quality evidence; map to real product symbols. Do not invent `Good`, source/server timestamps, capture-policy provenance or `Synchronized`. Check feasibility of the defined plan and identify concrete data gaps, leaving final method/sample sufficiency/threshold for evidence-based review. Source/resource limits remain in #659. 4G remains separate |
| Forbidden | Changes to product code/API/schema/UI/SQL/workflows, frozen 4A–4E, HA, drivers, licensing, AI/learning, Stage 4G closure, 5B/6, or Preview. No inferred healthy baseline, diagnosis, fault probability, RUL, or synthetic-as-field-validation claim |
| CODEX return | One coherent report in #659 with source/file fingerprints where downloaded, contract fit, actual descriptive evidence, gaps, resource paths and recommended next owner/action. No product PR or CI is required |
| Main next step | Review #659 against ADR-021 and the defined acceptance plan; then release an implementation only when evidence supports it. Request owner-held data only for a specific remaining gap. Source discovery alone does not qualify a product profile. Stage 4 and #588 remain PARTIAL |

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
- ADR-021 was initially a scope proposal (PR #643/T1 #38992226246); the owner authorized sequential development on 2026-10-19, [recorded in #588](https://github.com/brunolrogerio-collab/EliteSCADA/issues/588#issuecomment-6267755298). Stage 4D #649 and 4E #652 are accepted within bounded contracts. The 4F method/data gate #655 is closed with result NOT READY; joint score implementation and 4G validation require representative data and explicit acceptance bounds. Preserve caller-selected reference and frozen 4A–4E contracts; broader Stage 4 remains partial until residuals pass or are explicitly deferred.
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

For supplemental Stage 4F public-source qualification, return one report in this form:

```text
CODEX -> MAIN COORDINATOR
Package / owning issue: Stage 4F public-source qualification / #659
Status / next owner:
Exact inspected product checkpoint and live integration ref:
Exact source records/files, retrieved license notices, attribution and checksums where downloaded:
Observed schema/units, chronology/rates/gaps, clock/quality evidence and label scope:
Mapping to real Historian/Stage 4E symbols; unsupported source-time/quality mappings:
Separate suitability for offline research, canonical synchronized cohorts and field claims:
Feasibility against Main's acceptance plan; leakage controls and precise remaining data gaps:
Local resources and recommendation to Main; no product code/method/threshold finalized:
Confirmation: owner Preview, HA, drivers and frozen Stage 4A–4E untouched:
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
