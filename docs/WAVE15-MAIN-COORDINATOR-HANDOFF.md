# Wave 15 — Main order and execution contract

Snapshot: 2026-10-20 23:19 America/Sao_Paulo. This is the live Main order. Replace this current order rather than stacking historical CURRENT/LATEST sections. Use [the short pointer](CURRENT-COORDINATOR-HANDOFF.md) for the accepted product checkpoint and evidence.

## Current status — simulation-validated experimental 4F implementation authorized; field validation remains open

| Field | Order |
| --- | --- |
| Owner / executor | Product Owner directed on 2026-10-20 to use simulation to validate and close engineering gaps now; future product tests may collect real data and improve Intelligence. Main owns implementation; CODEX is not assigned |
| Parent / target | Intelligence #588 / `wave15/corrections-integration` |
| Completed package | [#680](https://github.com/brunolrogerio-collab/EliteSCADA/issues/680) / [PR #682](https://github.com/brunolrogerio-collab/EliteSCADA/pull/682); merge `51a6e72c5a57605a2647aa891b5dff2e06e46eaa`, candidate `8456c470cecd7b83b71b0126ff818c4f3a52e4ce`; exact-head T1 #39489609295 passed |
| Research result | None of six required classes passed the combined synthetic gate. Point FP 13/10,920=0.119%; false episodes 1.582/24 scored synthetic hours (>1 target); current-only timely 87.5% (<90%); dispersion x2/x0.5 recall 2.5%/0.83%. This is generator-conditional negative evidence only; no score, threshold or production method is adopted |
| Execution and containment | The protocol, configuration, cross-platform comparison and one-use Windows holdout are frozen. Main independently reran 19 focused UNIT tests on Windows/Python 3.12.14 (19/19 pass) and checked frozen hashes/source/receipt. Do not replay or tune the consumed holdout; no old artifact was changed, and no product/service/Preview/HA/driver/license action occurred |
| Completed design | #678 read-only return accepted as a research direction. Main's binding #680 supplement resolves raw-vs-residual confounding, constant-speed reference identifiability, three-context-TAG budget, 200 rows/page semantics and finite calibration/warm-up/episode rules. This is not adoption of a replacement product API/model/ADR |
| Completed temporal study | #675 / PR #677 accepted at `7065d4cf7b6a9a281206ad3f1f78e551b77b2b39`, final head `2e8e7b02219c129fbcf13df338dd55a483a5af41`; T1 #39479275345 passed. CODEX 16/16 Windows/Linux and exact fit/calibration comparison; Main 16/16, 32,768-sequence oracle and independent metadata/hash/56-scenario arithmetic checks. No joint candidate met all targets; no method winner. No product T2 applies |
| Completed bench | #671 / PR #673 accepted at `ebe9b3abbbaffb8054b79273728f383e3bd17086`, final head `6561568a4604cb6d2ec188a78f8551cd2e0c8b46`; T1 #39475543766 passed. CODEX 18/18 Windows/Linux, Main 18/18 plus 32,768-sequence oracle with zero mismatches; eight frozen blobs/provenance preserved and historical-performance erratum accepted. No product T2 applies |
| Preparation package completed | [#667](https://github.com/brunolrogerio-collab/EliteSCADA/issues/667), local CSV input preflight; PR #669 merged at `4ebb8b08005271c4eb3143bd647f992870dd86c4`; final head `6821c55c6fb84d05ebfd4a2b9ab85382c99081b9`; issue closed for the utility scope |
| Preparation evidence | Exact-head DOCS_I18N_HELP T1 #39452237823 passed classifier, Common sanity and final gate. CODEX passed 19/19 synthetic tests on Windows and isolated Linux; Main independently passed the Windows suite and Unicode rejection probes. No product T2 applies |
| Completed bounded mission | `W15-INTELLIGENCE-4F-OFFLINE-METHOD-STUDY-01`, [#662](https://github.com/brunolrogerio-collab/EliteSCADA/issues/662), closed as a research deliverable; [PR #664](https://github.com/brunolrogerio-collab/EliteSCADA/pull/664) merged at `6e6bd345dd730b4ba7dee1522dbb58ef12a42231`; final head `a55f4a0859fdb7f95ab58ee31b3cdd29bf971040` |
| Accepted product checkpoint | Stage 4E PR #654 merge `793b8303547491e02b98bf73732d3ab5ab76567a`; exact T1 #39331697007 attempt 2 and exact-merge T2 #39336277009 passed. Research/preparation PRs #664 and #669 do not advance this product-code checkpoint |
| Exact research gate | DOCS_I18N_HELP exact-head T1 [#39439718537](https://github.com/brunolrogerio-collab/EliteSCADA/actions/runs/39439718537) passed on `a55f4a0859fdb7f95ab58ee31b3cdd29bf971040`: classifier, Common T1 sanity and Wave 15 T1 gate succeeded; product checks were skipped as expected. No product T2 applies |
| Owner Preview | Preserve `http://localhost:18080`, product `0054d587fe855bbea4b1af81726af016208643da`, data/volumes/ports. Owner evaluation and #639 visual acceptance remain separate. Do not reset or redeploy Preview |
| Prior UCI outcome | **INSUFFICIENT EVIDENCE** for product method, threshold, field performance or deployment. The study compared a covariance-aware candidate and marginal control over 10 nominal+stable hydraulic cycles; it selected no winner/method/threshold |
| New experimental candidate | [PR #683](https://github.com/brunolrogerio-collab/EliteSCADA/pull/683) remains open/draft at `60c50f17dc13897690185287f9a343b33c7e0a37`. It reports a distinct synthetic candidate; Main's exact-head review and local test results are [recorded here](https://github.com/brunolrogerio-collab/EliteSCADA/pull/683#issuecomment-6303024402). It is not a selected product method, was not independently replayed, and is not merged/activated |
| Prior UCI evidence | 18/18 synthetic harness tests passed on Windows/Python 3.14 and isolated Docker/Linux/Python 3.12.15; all 1,138 fit/calibration numeric values matched across runtimes. Frozen config/results were unchanged during guard hardening; real holdout cycles were not reopened. Full counts, conditioning and caveats are in `docs/research/intelligence-stage4f/results.md` and #662 closeout |
| Evidence boundary | Public-source qualification is complete in [#659](https://github.com/brunolrogerio-collab/EliteSCADA/issues/659#issuecomment-6282568180). No eligible representative owner-Historian cohort is available, so field metrics and operational thresholds remain unestablished. This does not block a versioned experimental score implementation validated with synthetic data |
| Defined acceptance plan | [Main's plan](WAVE15-INTELLIGENCE-STAGE4F-ACCEPTANCE-PLAN.md) remains binding: motor-driven pumping sets first, compressors second; targets include ≤1% point false positives, ≤1 false episode per 24 eligible operating hours, ≥90% recall per defined sustained-deviation class, and cadence-qualified 10-minute latency. These targets are not measured. Do not ask the owner to define them again |
| Accepted tool scope | Standalone local CSV structural/declared-metadata preflight under `docs/research/intelligence-stage4f-input-preflight/`; seven-digit/100 ns timestamp precision, invariant numeric/date grammar, duplicate counts, explicit limits and aggregate diagnostics. Collection guidance points to accepted criteria; analytical eligibility/readiness stay NOT_ASSESSED. No operational cohort was used |
| Current execution | Main has implemented the experimental Core scorer, typed 4F DTO/catalog, dedicated query route, read-only Analytics service and deterministic/service security tests in isolated worktree `work/w15-intelligence-stage4f-simulation-v1`; draft [PR #687](https://github.com/brunolrogerio-collab/EliteSCADA/pull/687) targets exact base `63182e021730ad4fb35532481e28d7a16fc04be3`. Its initial implementation candidate head `6f9557316cf9c9a3ce2a2ac4e5a1aa74f5b59297` passed exact-head T1 #39531733156: 1,684 .NET tests passed, 1 skipped, none failed; common sanity and gate passed. Current increment exposes per-signal acceptance evidence and counts ambiguous source-time groups only inside each requested window. The owner is independently editing local Preview and will supply that separate delta later; do not access or alter Preview |
| Local evidence | Windows SDK 10.0.402: `Scada.Core.Tests` 698/698; all Intelligence-filtered Drivers 267/267; focused 4F joint-deviation tests 13/13; Release API build 0 warnings / 0 errors. Linux SDK 10.0.400 isolated container: Core 698/698 and Intelligence-filtered Drivers 267/267. `git diff --check` passes after this handoff update. Full Drivers is not green: a mandatory DNP3 lab test lacks `ELITESCADA_L3_MQTT_HOST`; ServerScript rich-command test times out even alone; Modbus/Shelly transport tests failed during a broad run but passed on immediate isolated rerun. These full-suite failures are outside the 4F change surface. CI remains outstanding |
| Next owner/action | Main: complete review of PR #687 and require green T1 for its current head, then integrate only after gates and verify exact-merge T2. Product Owner: continue local Preview edits independently and supply that delta later for a separate integration review. Preserve 4A–4E and Preview. Future product-test data may improve/requalify Intelligence under applicable permissions; only eligible representative evidence supports field thresholds/claims. Do not repeat #659 or replay #680/#683 holdouts; 4G/5B/6 remain gated |
| Forbidden | Claiming method/threshold or product acceptance from synthetic research; treating synthetic seconds as field exposure; using a consumed holdout for tuning; altering frozen 4A–4E, HA/drivers/licensing/Preview, runtime, API/schema/UI/SQL or releasing 4G/5B/6 without their gates |

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
- ADR-021 was initially a scope proposal (PR #643/T1 #38992226246); the owner authorized sequential development on 2026-10-19, [recorded in #588](https://github.com/brunolrogerio-collab/EliteSCADA/issues/588#issuecomment-6267755298). Stage 4D #649 and 4E #652 are accepted within bounded contracts. #655's NOT READY assessment accurately described the evidence then available; the owner's later direction now authorizes a simulation-validated experimental 4F implementation. Representative product data remains necessary for field claims and 4G. Preserve caller-selected reference and frozen 4A–4E; broader Stage 4 remains partial.
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

## Completed CODEX return — #680

CODEX returned the exact preregistration/configuration/receipt chain and one-use holdout result in PR #682. Main completed independent review, exact-head T1 and integration; the finite research issue is closed. The result failed the combined synthetic gate and does not select a product method. No follow-on study or product implementation is currently released. Do not replay or retune the holdout. Persist later decisions only after Main identifies an eligible evidence basis and issues a new bounded order. End visible interactions with fresh `Hora: HH:MM`.

The owner approved continued Intelligence research/design on 2026-10-20, then clarified to use simulation to validate/close engineering gaps now and use future product tests to collect real data and improve Intelligence. Main accepted #678 as design evidence and separately authorized finite #680; its negative result remains historical and does not relax field targets or permit replay. #667/#671/#675/#678/#680 remain complete within their scopes; prior studies and acceptance targets stay frozen. The current simulation-validated experimental product increment proceeds on a fresh exact base; no field-validity or Stage 4 closure is inferred.

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
