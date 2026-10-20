# Stage 4F — offline method experiment protocol v1

**Protocol state:** frozen for the pre-fit specification commit. This document and its source/split/environment manifests must be committed before running any fitting or calibration operation. No fitted parameters, thresholds, scores, or final method decision are included here.

**Research-only boundary:** UCI data is not admitted to Historian and is not canonical `Good`/`Synchronized` product evidence. This experiment does not add or call a runtime score, API, store, entity, worker, notification, or process action. Results can support offline method feasibility only, not field validity, a fault probability, diagnosis, RUL, or a production threshold.

## Question and hypothesis

On the small UCI 447 hydraulic test-rig reference slice, compare a covariance-aware distance with an independent per-signal control. The bounded hypothesis is that the joint score can express the observed dependency structure among motor power and pressure signals differently from a score that considers each channel independently. This is a feasibility question, not a claim that either score detects field faults.

## Pinned source and cohort

Use only the already downloaded UCI Condition Monitoring of Hydraulic Systems archive pinned in `source-manifest.json`. Read the archive directly; do not extract raw files to the repository. The selected cohort is the ten **one-based** cycle IDs 1788–1797 from `profile.txt`, each with cooler condition 100, valve condition 100, pump leakage 0, accumulator pressure 130, and `stable` flag 0. The source's `stable=0` means the test-rig conditions were stable; it is not a certified healthy/no-fault label or independent normal operating exposure.

The required three-signal pilot is `EPS1` (motor power, W), `PS1` (pressure, bar), and `PS2` (pressure, bar). A predeclared dimension/conditioning check adds same-rate pressure signals: d=5 adds `PS3`, `PS4`; d=7 also adds `PS5`, `PS6`. All selected series have the published 100 Hz rate and 6,000 positions per cycle. A vector is paired by its original cycle row and identical within-cycle column in the source matrices. This is a documented offline pairing assumption using the source's cycle/sample coordinate—not a claim of explicit UTC timestamps, physical simultaneity, or `Synchronized` evidence. No cross-rate signal is used.

Retain the one-based cycle ID and zero-based within-cycle position 0–5999. Do not concatenate cycles into a continuous clock, join/interpolate rows, resample, repeat cycles, or infer a wall-clock duration from adjacent cycle numbers.

## Locked grouped split, purges, and holdout isolation

| Partition | Whole cycle groups | Cycle count | Per-dimension vector rows |
| --- | --- | ---: | ---: |
| Fit | 1788–1793 | 6 | 36,000 |
| Calibration | 1794–1795 | 2 | 12,000 |
| Final holdout | 1796–1797 | 2 | 12,000 |

`cycle-split-manifest.csv` is the exact assignment. A cycle is indivisible and may appear in exactly one partition. Scoring is pointwise: no rolling features, lookback window, smoothing, or window overlap is defined, so the feature/lookback purge is zero sample positions by construction. The group-level purge is enforced as disjoint complete cycles. No source-time gap can be asserted between cycles because absolute cycle timestamps/elapsed gaps are not supplied; possible dependence across adjacent cycles remains an explicit limitation, not a claim of independence. Do not replace this limitation by a time-based purge or by dropping the prescribed 6/2/2 groups without a new Main order.

The harness must reject duplicate/overlapping cycle assignments and must not read final-holdout matrix rows during fitting or calibration. Fit only on cycles 1788–1793. Calibration-only empirical quantiles are frozen before any holdout read. The final holdout is run once against the frozen configuration; no post-holdout selection, tuning, or threshold change.

## Finite predeclared method and conditioning grid

1. **Fit-only robust per-signal transform:** for each selected signal, fit the median and `1.4826 × MAD` on the fit partition. A nonfinite or zero scale is a degeneracy and stops that candidate; do not substitute epsilon. Apply the frozen transform to fit/calibration/holdout.
2. **Joint candidate:** estimate sample covariance of the robust-scaled fit vectors (centered by the fit-vector mean). For the finite shrinkage values `lambda ∈ {0.05, 0.10, 0.25, 0.50}`, form `C_lambda = (1 - lambda) C + lambda I`; report unregularized rank/condition and each regularized condition. Reject nonfinite, non-positive-definite, or failed-solve candidates. Score as squared Mahalanobis distance `zᵀ C_lambda⁻¹ z`; do not publish signed feature contributions or fault attribution.
3. **Independent control:** score each vector as `max_j |z_j|`, where each signal uses the same fit-only robust center/scale but no covariance terms. This is a marginal control, not a joint score.
4. **Calibration:** for each dimension/method/shrinkage candidate, freeze its empirical 99th-percentile score threshold using the calibration partition only, with nearest-rank rule `sorted[ceil(0.99*n)-1]`. The 99th percentile is a nominal study threshold, not an achieved 1% false-positive rate. Retain the full finite grid; because there are only two calibration cycle groups, do not select or announce a winning method/regularizer.
5. **Sample-size/conditioning series:** on fit data only, report each prefix of 1–6 complete fit cycles for dimensions 3/5/7. For every prefix report source-group count, vector-row count, vector-row/dimension ratio, empirical covariance rank and condition. These ratios describe raw within-cycle positions only; the 100 Hz positions are temporally dependent and do not constitute that many independent source groups.

The eigen/linear-solve implementation and its numerical tolerances must be deterministic, standard-library-only, and covered by tests. The finite grid is the complete experiment; no model zoo or post-hoc grid expansion is allowed.

## Metrics and explicit non-measurements

After the frozen holdout run, report per candidate/dimension and separately for each of the two holdout cycles: raw point-score counts, calibration threshold, reference-exceedance count/rate, matrix rank/condition, degeneracy, elapsed time, and process peak resident memory. Call holdout exceedances **reference exceedances**, not false positives: the profile label is not independent ground truth that the asset was healthy.

Do not report acceptance PASS from this slice. The following are structurally unavailable and must remain `NOT MEASURED / INSUFFICIENT EVIDENCE`: verified point false-positive rate; false episodes per 24 eligible operating hours; 168 eligible normal/known-nonfault hours across 14 calendar days; 95% block/cluster intervals with adequate independent day/cycle groups; ≥100 independent held-out source blocks per injection class; event recall; and 10-minute latency. The selected stable nominal cohort is ten cycles and only two holdout groups. Do not stretch the 60-second cycles into 15-minute events or 168 hours. No natural event labels exist in this selected slice. Synthetic unit-test stimuli, if used, are reported separately as harness-mechanics checks, not source-based sensitivity or field evidence.

Apply no 3-of-5 episode convention: there is no absolute source-time axis and cycle continuity is unknown. Do not turn cycle/column coordinates into UTC or product timestamps. Do not treat the source's missing-value statement as quality. Do not create `Good`, `Synchronized`, capture-policy provenance, or a canonical source cohort.

## Aggregate-row budget comparison

The accepted product contract keeps `MaximumRawRowsAcrossQuery = 50_000` across selected signals/windows (`AssetProcessIntelligenceContextualDeviationVectorV1Catalog`). At this source's 6,000 positions per cycle, the equivalent raw signal-row counts are:

| Dimension | Fit (6 cycles) | Calibration (2) | Holdout (2) | All partitions |
| ---: | ---: | ---: | ---: | ---: |
| 3 | 108,000 | 36,000 | 36,000 | 180,000 |
| 5 | 180,000 | 60,000 | 60,000 | 300,000 |
| 7 | 252,000 | 84,000 | 84,000 | 420,000 |

These counts are `signals × original within-cycle positions`, for a transparent comparison only. The harness reads UCI files directly; it does not call the product API, import records, change the product limit, or claim equivalent query semantics. Under that conservative aggregate-row accounting, only the d=3 calibration and holdout partitions individually fit within 50,000; no complete fitting partition or complete experiment does.

## Reproducibility and privacy

Record exact base/head, source hashes, local source paths, Python/environment/image versions, split, configuration digest, test results, elapsed time, and peak RSS. No randomized sampling or algorithmic randomness is used; seed is `not applicable` (any synthetic unit test uses fixed literal vectors). Use a single CPU worker, no GPU, external AI, or network access in the numerical run. The code uses only the Python standard library; no product dependency changes.

Keep raw archives and all record-level scores outside Git/GitHub. The report may contain aggregate counts and summary statistics only. Retain the downloaded raw ZIPs in the existing isolated scratch location under the #659 90-day policy, with retention review due 2027-01-11 and maximum expiry 2027-01-18; no extension is assumed. Do not request or access private plant data in this package.

## Commit/holdout sequence

1. Commit this protocol, source/split manifests, and environment pin before fitting.
2. Commit the research harness and checks before fitting/calibration.
3. Run fit/calibration only; commit a hash-verified frozen configuration before any final-holdout read.
4. Run the final holdout once from the frozen configuration; preserve aggregate results only and state limitations.
5. Submit one draft research PR with `VALIDATION_PROFILE: DOCS_I18N_HELP`; Main owns review and T1. Do not self-merge, dispatch T1/T2, or alter workflow/classifier behavior.
