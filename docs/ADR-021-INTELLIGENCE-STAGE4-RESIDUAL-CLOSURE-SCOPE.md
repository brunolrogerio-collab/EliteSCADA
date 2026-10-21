# ADR-021 — Intelligence Stage 4 residual closure scope

| Field | Value |
| --- | --- |
| Status | **SCOPE AUTHORIZED FOR SEQUENTIAL DEVELOPMENT — bounded Main assignments and validation gates apply** |
| Date | 2026-10-16 |
| Owner direction | Reiterated 2026-10-19: finish Stage 5A, then return to finish Stage 4 against this scope; [record in #588](https://github.com/brunolrogerio-collab/EliteSCADA/issues/588#issuecomment-6267755298) |
| Parent | [#588 — Asset & Process Intelligence](https://github.com/brunolrogerio-collab/EliteSCADA/issues/588) |
| Frozen boundary | Stage 4A–4C and ADR-020 remain unchanged |
| Scheduling | Stage 5A technical gates and stable Preview delivery passed; owner chose continued development during evaluation on 2026-10-19. #639 visual acceptance remains pending, not a blocker to Main's exact-base Stage 4D #649 assignment |

## Purpose and current truth

Main's initial scope, operational acceptance targets and public-research data policy are defined in [the Stage 4F acceptance plan](WAVE15-INTELLIGENCE-STAGE4F-ACCEPTANCE-PLAN.md) under the Product Owner's 2026-10-20 delegation. On 2026-10-20 the Product Owner further directed Main to use simulation to validate and close engineering gaps now, then use future product tests to collect real data and improve Intelligence. This authorizes a bounded simulation-validated experimental implementation; it does not establish real-data eligibility, field performance, final operational thresholds, or waive Stage 4G.

Stage 4A, 4B and 4C are integrated, verified and frozen within the descriptive Stage 4 V1 acceptance boundary in [ADR-020](ADR-020-INTELLIGENCE-STAGE4-V1-ACCEPTANCE.md). That boundary intentionally leaves the broader objective in #588 **PARTIAL**: the caller chooses the reference and context; the product does not certify baseline health or global regime equivalence; the multi-signal response contains independent per-TAG scores, not a joint multivariate score.

The Product Owner authorized this residual development sequence on 2026-10-19: complete Stage 5A, then finish Stage 4 using this scope before Stage 5B/6. Main releases the bounded packages on revalidated exact bases. The authorization does not change frozen 4A–4C contracts, establish that residual capabilities exist, or approve a model/threshold without its stated data-validation gate.

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

### 4G — Validation and closure gate

- Establish a representative, version-controlled validation dataset for each supported profile/context, including normal operating variability, known non-fault process variation, sensor loss/quality degradation, synchronized and misaligned data, correlated-tag behavior, and deterministic injected single-/multi-signal deviations.
- Before calling the score validated, report threshold-selection method, false-alarm behavior on held-out reference data, sensitivity on the defined deviation scenarios, and repeatability across supported OS/database configurations. Synthetic perturbations validate detector behavior, not field fault-prediction accuracy.
- Prove fail-closed authorization, read-only behavior, deterministic outputs, query bounds, persistence/restart behavior, and no work for disabled Intelligence scopes. Validate on Windows and Linux; no Windows-only dependency, GPU, external service or generative model is introduced.
- Main may declare the broader Stage 4 complete only after each accepted 4D–4G criterion is implemented and verified on exact candidate/merge SHAs, or the Product Owner explicitly defers a named residual. A missing representative dataset blocks field/performance claims and 4G validation; it does not block simulation-validated software implementation. Green CI alone is not field evidence.

## Invariants and explicit exclusions

- No change to ADR-020, 4A/4B APIs, the 4C `contextual-deviation-vector.v2` contract, existing persisted meanings, or Stage 4 V1 results.
- Analytics remain on-demand, read-only, bounded, and under the canonical Intelligence entitlement, Authority capabilities and TAG-level `TagRead`; no parallel authoritative store, SQL, background learner, or process write/control is added.
- No automatic “healthy/normal” certification of a reference. Unknown cadence/completeness remains unknown unless explicitly configured.
- High-frequency vibration waveform acquisition and feature extraction require a separate acquisition/capability package. Existing historian vibration TAGs may be analyzed under the same availability/quality rules; their absence does not make other signals unusable.
- Fault diagnosis, root-cause attribution, alarms, autonomous action, failure prediction and remaining-useful-life estimates remain out of scope without a separately approved evidence package.
- Stage 5A widgets, Stage 5B guided dashboards, and Stage 6 hardening are separate product packages. Finish Stage 5A, then these Stage 4 residuals, before releasing Stage 5B/6.

## Implementation order after Stage 5A

1. Main revalidates the exact integration/product checkpoint after Stage 5A technical verification and stable Preview delivery, then activates bounded 4D #649. #639 visual acceptance proceeds independently per the owner's 2026-10-19 clarification. Existing authorization persists; each package starts through a bounded exact-base Main assignment without repeated owner permission.
2. Implement and verify 4D's additive evidence contract and insufficiency semantics.
3. Implement 4E's explicit versioned profiles/comparability only after its persisted schema and UX-facing operator semantics are accepted.
4. Implement 4F as a new versioned, read-only experimental contract using deterministic simulated cohorts. Keep its experimental status explicit, test integration/security/query bounds on isolated Windows and Linux resources, and never use the Preview database or replay a consumed holdout.
5. Keep field targets and operational threshold qualification open until future authorized product tests produce eligible representative evidence. Use that evidence to tune and revalidate; do not call synthetic exposure real.
6. Complete 4G exact-SHA local/CI evidence and Main's review. Only then resolve the parent #588 Stage 4 disposition. Stage 5B/6 remain unreleased until that decision.
