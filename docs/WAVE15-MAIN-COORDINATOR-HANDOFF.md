# Wave 15 — Main order and execution contract

Snapshot: 2026-10-20. This is the live Main-to-CODEX order. Replace this current order rather than stacking historical CURRENT/LATEST sections. Use [the short pointer](CURRENT-COORDINATOR-HANDOFF.md) for the accepted product checkpoint and evidence.

## Current status — Stage 4F fresh temporal study #675 released; product gate remains NOT READY

| Field | Order |
| --- | --- |
| Owner / executor | CODEX implements fresh offline study #675; Main owns review, T1 and integration. #671 research/mechanics corrections are accepted/closed |
| Parent / target | Intelligence #588 / `wave15/corrections-integration` |
| Active package | [#675](https://github.com/brunolrogerio-collab/EliteSCADA/issues/675), `W15-INTELLIGENCE-4F-TEMPORAL-STUDY-01`; branch `work/w15-intelligence-stage4f-temporal-study`, inspected integration `ebe9b3abbbaffb8054b79273728f383e3bd17086`, subject to live revalidation. Write only new `docs/research/intelligence-stage4f-temporal-study/`; reuse accepted pure helpers without changing old studies |
| Fresh hypothesis | Compare instantaneous and EWMA versions of the same fixed-shrinkage joint score, with only predeclared alpha/quantile settings and marginal controls. New namespace/240-group 60-fit/60-calibration/120-holdout assignment; 43,560 aggregate scalar rows for illustrative fit+calibration+one analysis block. Exact definitions/selection/limits are in #675, not production settings |
| Execution and containment | Commit protocol/manifest before fitting and configuration before one fresh Windows holdout. Tests/fit-calibration on Windows and isolated Linux; separate UNIT fixtures. Old #671 and UCI identities/seeds/configurations/results remain consumed/frozen. No stimulus/target adjustment, holdout retune, old replay or product-method approval |
| Completed bench | #671 / PR #673 accepted at `ebe9b3abbbaffb8054b79273728f383e3bd17086`, final head `6561568a4604cb6d2ec188a78f8551cd2e0c8b46`; T1 #39475543766 passed. CODEX 18/18 Windows/Linux, Main 18/18 plus 32,768-sequence oracle with zero mismatches; eight frozen blobs/provenance preserved and historical-performance erratum accepted. No product T2 applies |
| Preparation package completed | [#667](https://github.com/brunolrogerio-collab/EliteSCADA/issues/667), local CSV input preflight; PR #669 merged at `4ebb8b08005271c4eb3143bd647f992870dd86c4`; final head `6821c55c6fb84d05ebfd4a2b9ab85382c99081b9`; issue closed for the utility scope |
| Preparation evidence | Exact-head DOCS_I18N_HELP T1 #39452237823 passed classifier, Common sanity and final gate. CODEX passed 19/19 synthetic tests on Windows and isolated Linux; Main independently passed the Windows suite and Unicode rejection probes. No product T2 applies |
| Completed bounded mission | `W15-INTELLIGENCE-4F-OFFLINE-METHOD-STUDY-01`, [#662](https://github.com/brunolrogerio-collab/EliteSCADA/issues/662), closed as a research deliverable; [PR #664](https://github.com/brunolrogerio-collab/EliteSCADA/pull/664) merged at `6e6bd345dd730b4ba7dee1522dbb58ef12a42231`; final head `a55f4a0859fdb7f95ab58ee31b3cdd29bf971040` |
| Accepted product checkpoint | Stage 4E PR #654 merge `793b8303547491e02b98bf73732d3ab5ab76567a`; exact T1 #39331697007 attempt 2 and exact-merge T2 #39336277009 passed. Research/preparation PRs #664 and #669 do not advance this product-code checkpoint |
| Exact research gate | DOCS_I18N_HELP exact-head T1 [#39439718537](https://github.com/brunolrogerio-collab/EliteSCADA/actions/runs/39439718537) passed on `a55f4a0859fdb7f95ab58ee31b3cdd29bf971040`: classifier, Common T1 sanity and Wave 15 T1 gate succeeded; product checks were skipped as expected. No product T2 applies |
| Owner Preview | Preserve `http://localhost:18080`, product `0054d587fe855bbea4b1af81726af016208643da`, data/volumes/ports. Owner evaluation and #639 visual acceptance remain separate. Do not reset or redeploy Preview |
| Prior UCI outcome | **INSUFFICIENT EVIDENCE** for product method, threshold, field performance or deployment. The study compared a covariance-aware candidate and marginal control over 10 nominal+stable hydraulic cycles; it selected no winner/method/threshold |
| Prior UCI evidence | 18/18 synthetic harness tests passed on Windows/Python 3.14 and isolated Docker/Linux/Python 3.12.15; all 1,138 fit/calibration numeric values matched across runtimes. Frozen config/results were unchanged during guard hardening; real holdout cycles were not reopened. Full counts, conditioning and caveats are in `docs/research/intelligence-stage4f/results.md` and #662 closeout |
| Unresolved data gate | Dataset lacks independent healthy/fault labels, UTC per-sample source timestamps, `Good` quality and `Synchronized` provenance. Only two calibration and two holdout cycle groups exist; no reliable confidence interval, operating-hour exposure, validated false-alarm rate/episodes, event recall or cadence-qualified latency can be computed |
| Defined acceptance plan | [Main's plan](WAVE15-INTELLIGENCE-STAGE4F-ACCEPTANCE-PLAN.md) remains binding: motor-driven pumping sets first, compressors second; targets include ≤1% point false positives, ≤1 false episode per 24 eligible operating hours, ≥90% recall per defined sustained-deviation class, and cadence-qualified 10-minute latency. These targets are not measured. Do not ask the owner to define them again |
| Accepted tool scope | Standalone local CSV structural/declared-metadata preflight under `docs/research/intelligence-stage4f-input-preflight/`; seven-digit/100 ns timestamp precision, invariant numeric/date grammar, duplicate counts, explicit limits and aggregate diagnostics. Collection guidance points to accepted criteria; analytical eligibility/readiness stay NOT_ASSESSED. No operational cohort was used |
| Next owner/action | CODEX returns one coherent fresh-study draft PR with protocol/freeze/receipt chain, meaningful Windows/Linux evidence, synthetic metric/uncertainty/resource matrix and a finite next recommendation; Main owns review/T1/integration. No private export is needed, and no measured synthetic result by itself releases a product method. 4G remains separate; 5B/6 are unreleased |
| Forbidden | Claiming method/threshold or product acceptance from the public sample; treating cycle adjacency as elapsed exposure; using holdout exceedances as false positives; altering frozen 4A–4E, HA/drivers/licensing/Preview, runtime, API/schema/UI/SQL or releasing 4G/5B/6 without their gates |

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

For #675, return exact inspected base/head and draft PR; pre-fit protocol/generator/partition hashes and commits; pre-holdout frozen selection/configuration; new identity-scoped receipt/aggregate provenance; focused Windows/Linux tests, numerical comparison/resources; per-criterion synthetic PASS/FAIL/INSUFFICIENT and still-unproved field/product claims. Report negative outcomes without forcing a winner. Put VALIDATION_PROFILE in the PR body at creation. Then wait for Main; no manual CI, self-merge or follow-on study. Persist evidence in #675/PR and end visible interactions with fresh `Hora: HH:MM`.

The owner approved continued controlled research on 2026-10-20 after Main distinguished engineering from field evidence. #675 is Main's separate finite preregistration decision after accepting #671's corrected mechanics; it does not reopen the consumed trial or waive ADR-021. #667 remains complete; all prior studies and acceptance targets remain frozen. Any later study, private-data assessment or product implementation requires its own applicable decision/evidence; no field-validity or Stage 4 closure is inferred.

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
