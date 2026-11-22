# ADR-021 — Intelligence Stage 4 residual closure scope

| Field | Value |
| --- | --- |
| Status | **STAGE 4 ENGINEERING IMPLEMENTATION COMPLETE / VERIFIED / FROZEN within the experimental scope; operational field-validation residual explicitly deferred to a separate future wave in #701** |
| Decision date | 2026-10-21 |
| Owner direction | Close Stage 4 with what can be built and verified now; defer work requiring field data until after product testing, in a separate future-wave round. Recorded in [#588](https://github.com/brunolrogerio-collab/EliteSCADA/issues/588) and tracked separately in [#701](https://github.com/brunolrogerio-collab/EliteSCADA/issues/701) |
| Parent | [#588 — Asset & Process Intelligence](https://github.com/brunolrogerio-collab/EliteSCADA/issues/588) |
| Frozen boundary | Stage 4A–4C and ADR-020 remain unchanged |
| Scheduling | Stage 4 engineering closure is separate from Stage 5A visual acceptance, later Intelligence stages and future field validation. Stage 5A owner visual review remains separate; #588 stays open for later product scope. Do not access or alter the owner's Preview |

## Purpose and current truth

Main's initial scope, operational acceptance targets and public-research data policy are defined in [the Stage 4F acceptance plan](WAVE15-INTELLIGENCE-STAGE4F-ACCEPTANCE-PLAN.md) under the Product Owner's 2026-10-20 delegation. On 2026-10-20 the Product Owner authorized simulation to close software/mechanics gaps while real operational data would be collected after future product tests. On 2026-10-21 the Product Owner explicitly directed that Stage 4 be closed using what can be delivered and verified now, with field-dependent work moved to a separate round in another wave. Accordingly, this ADR accepts Stage 4's engineering implementation within its experimental contract and defers only the named operational/field evidence; it does not claim field performance, adopt a field-calibrated threshold, or certify a detector.

Stage 4A–4C remain integrated, verified and frozen within the descriptive Stage 4 V1 acceptance boundary in [ADR-020](ADR-020-INTELLIGENCE-STAGE4-V1-ACCEPTANCE.md). The additive 4F experimental score/API is integrated and verified. The preserved limitations still apply: the caller chooses the reference and context; the product does not certify baseline health or global regime equivalence; the experimental joint score is not a fault classifier, diagnosis, efficiency result or field-performance claim.

The Product Owner authorized this residual development sequence on 2026-10-19: complete Stage 5A, then finish Stage 4 using this scope before Stage 5B/6. On 2026-10-21, the owner clarified that field collection and field-dependent acceptance belong to a separate future-wave round, after product testing. This decision closes current Stage 4 engineering only; it does not change frozen 4A–4C contracts, assert field validity, or pre-approve a future model/threshold.

## Authorized bounded packages

Owner clarification, 2026-10-19: continue development after delivery of the stable Docker Preview while the owner evaluates/reports corrections later. Stage 5A visual acceptance is still required before its freeze but is not a Stage 4 development blocker. Preserve Preview revision/data; isolate later development/tests. Main's bounded package is [#649](https://github.com/brunolrogerio-collab/EliteSCADA/issues/649), with [the repository order](WAVE15-INTELLIGENCE-STAGE4D-REFERENCE-EVIDENCE.md).

### 4D — Reference evidence and data sufficiency

- Preserve explicit operator selection of the reference window. Record that provenance (selected by whom/when, exact time window, source query and contract version) without asserting that the period is healthy or representative.
- Return transparent evidence per selected signal: observed/accepted/rejected sample counts, quality distribution, synchronized-cohort count, window boundaries, and coverage/completeness only when a declared expected cadence or completeness contract exists. Otherwise return `unknown`; do not infer a cadence.
- Show why a reference or comparison is insufficient (missing required signals, poor quality, sparse or unsynchronized observations, incompatible source/context). Never repair missing observations with interpolation, imputation, nearest-time joins, or carry-forward.
- Optional signals that are not configured/available do not block unrelated analyses; a missing signal explicitly required by the selected profile yields an insufficient-data result.

### 4E — Explicit operating profiles and comparison eligibility

- Add a versioned, operator-authored context profile using stable identities of existing TAGs and explicit typed predicates/intervals. Human-readable regime labels are configurable; no hard-coded `off/startup/stable` taxonomy is imposed.
- A query may compare observations only when the selected profile identity/version and required predicates match. Report **declared-context match**, not inferred or certified physical-process equivalence. A mismatch, unknown predicate, stale profile version or incomplete context fails closed as non-comparable/insufficient.
- Keep `contextual-deviation-vector.v2` behavior exactly as frozen. Any broader context query uses a new additive contract/version; it does not reinterpret the two numeric context intervals in 4C.

### 4F — Joint multivariate deviation score

- Add a separate, versioned read-only analysis over a complete synchronized cohort and one explicit reference/context profile. It must capture cross-signal relationships; re-labeling the existing independent per-TAG vector as “multivariate” is not acceptable.
- Build a new versioned, read-only joint score using a bounded, explicit method evaluated first with deterministic synthetic simulations. Record the method, windowing, numeric limits and insufficiency behavior in the contract. Results released on this evidence are **EXPERIMENTAL / SIMULATION-VALIDATED ONLY**: no false-alarm, sensitivity, fault-detection, efficiency, diagnostic, or field-performance claim is made. The existing per-TAG `n >= 30` rule is not automatically sufficient for joint analysis.
- Simulation is sufficient to close software/mathematical mechanics: deterministic fit/score, cross-signal relationship changes, injected shifts, warm-up and source-gap behavior, invalid/singular inputs, bounded resource use, read-only authorization and Windows/Linux execution. A simulated threshold may only be explicitly labeled experimental and generator-conditional; it cannot satisfy the operational targets in the acceptance plan.
- Future product tests may collect real operational evidence only under the product owner's applicable data/retention permissions. Representative held-out cohorts are still required before freezing a field threshold or claiming the quantitative false-alarm/recall/latency targets. Field evidence should improve and requalify the experimental Intelligence method; it is not a prerequisite to start this simulation-validated increment.
- Return the versioned score, threshold, context/reference provenance and interpretable per-signal contribution/residual information. If the cohort is too small, covariance is ill-conditioned, required signals are missing, or calibration is unavailable, return a stable insufficient-data state and no score.
- The result means “deviation from this selected reference under this declared context”; it does not identify a fault, cause, fault probability, remaining useful life or safe operating state.

### 4G — Engineering verification complete; field validation deferred

- The current-wave engineering/mechanics gate is satisfied by deterministic simulation and tests for the experimental implementation: score determinism and signal contributions; synchronized complete cohorts and context matching; warm-up, gaps and invalid/singular/insufficient inputs; bounded query/resource behavior; entitlement, Authority, per-TAG `TagRead`, permits, cancellation/revocation, inactive-scope no-work, read-only behavior; HTTP binding/early rejection; and Windows/Linux execution evidence. Exact-head T1, exact-merge T2 and local test evidence are listed below and in the current handoff.
- Persistence/restart of a fitted model is **not applicable** to this increment: fitting/scoring is on-demand from the selected reference, no model artifact or parallel persistence store is created, and there is no background learner. Persisted Intelligence profiles continue to use existing canonical configuration contracts; this score adds no new persistence requirement.
- Do not treat synthetic data as operating exposure, healthy labels or field-performance evidence. Point false-positive rate, false episodes per operating hour, field sensitivity/recall, latency, final threshold selection and repeatability on representative product-test cohorts remain explicitly unproven.
- The Product Owner's 2026-10-21 instruction explicitly defers those data-dependent items to [#701](https://github.com/brunolrogerio-collab/EliteSCADA/issues/701), a separately planned future-wave field-validation round after product testing. Thus Stage 4 is **COMPLETE / VERIFIED / FROZEN for the engineering scope deliverable now**, while the experimental status and non-claims remain visible. This is not field validation and does not close the parent #588 or release Stage 5B/6.

## Invariants and explicit exclusions

- No change to ADR-020, 4A/4B APIs, the 4C `contextual-deviation-vector.v2` contract, existing persisted meanings, or Stage 4 V1 results.
- Analytics remain on-demand, read-only, bounded, and under the canonical Intelligence entitlement, Authority capabilities and TAG-level `TagRead`; no parallel authoritative store, SQL, background learner, or process write/control is added.
- No automatic “healthy/normal” certification of a reference. Unknown cadence/completeness remains unknown unless explicitly configured.
- High-frequency vibration waveform acquisition and feature extraction require a separate acquisition/capability package. Existing historian vibration TAGs may be analyzed under the same availability/quality rules; their absence does not make other signals unusable.
- Fault diagnosis, root-cause attribution, alarms, autonomous action, failure prediction and remaining-useful-life estimates remain out of scope without a separately approved evidence package.
- Stage 5A widgets, Stage 5B guided dashboards, Stage 6 hardening, and the future field-validation round #701 are separate from this Stage 4 engineering closeout. Do not fold field data collection into the current Wave 15 acceptance.

## Implementation status and remaining gates

1. Stage 4D #649 and 4E #652 are integrated and verified within their bounded contracts; they do not establish reference health or physical-regime equivalence.
2. Stage 4F's additive experimental joint-score implementation is integrated in PR #687 at `48cfe002ac437f5248d0127aaecce28c82dc9ba9`. Exact candidate T1 #39532394830 and exact-merge T2 #39532910787 passed. It is read-only, simulation-validated, unclassified, and has no field-calibrated threshold. Do not alter frozen 4A–4E or replay a consumed holdout.
3. The non-field 4G engineering criteria are verified by the tests and exact-SHA gates above. Field-validation targets are **deferred**, not waived or achieved; #701 requires future eligible, representative, owner-authorized product-test data. Public UCI sources and finite synthetic studies do not establish field validity. Use the accepted data-request model/preflight and assess provenance, independent labels, cadence/exposure, coverage and operating context before future claims.
4. Stage 4 engineering is now closed within its experimental scope. Parent #588 remains open for distinct product scope; this closeout does not release Stage 5B/6. Stage 5A owner visual Preview and the user's independent local Preview changes remain separate; do not access or alter them without handoff.
