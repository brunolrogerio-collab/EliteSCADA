# Stage 4F — controlled synthetic motor/pump bench protocol v3

**State: pre-fit and frozen at the protocol-v3 commit.** This version supersedes v2 before any fitting, calibration, scoring, or examination of experimental outcomes. The correction explicitly models and tests a fail-closed synthetic-timebase synchronization flag; it does not claim physical sensor synchronization. No fitted parameter, selected candidate, threshold, score, or result is included. Any later amendment requires a separately versioned protocol and must not reinterpret the reserved holdout.

## 1. Question and evidence boundary

Test the mechanics and synthetic-conditional behavior of a small, standard-library-only research evaluator for three target signals from a deliberately transparent motor-driven pump model: motor current (A), discharge pressure (bar), and flow (m³/h). Declared context is running state, speed (rpm), and load fraction (unitless). The model is a controlled stimulus generator, **not a digital twin, measured pump, validated physical relation, equipment-health label, or product implementation**. In particular, current alone will not be converted into efficiency.

The narrow method comparison is a covariance-aware squared Mahalanobis distance against an independent-vector control (`max(abs(z_j))`). Only the predeclared shrinkage grid `{0.05, 0.10, 0.25, 0.50}` is admissible. No product/API/database/Preview/HA/driver/licensing code or data is accessed or changed. No network/service calls, raw UCI/public archives, consumed UCI holdout, owner data, or real-data outputs are part of this package. All generated traces are marked SYNTHETIC and remain in temporary scratch; only tiny deterministic mechanism fixtures and compact aggregate evidence may be committed.

## 2. Generator, units, context, and dependence

Each independent source block is one synthetic 20-minute trace at an exactly declared 10-second cadence: 120 samples at relative times `0, 10, ..., 1190` seconds. Every sample has the full three-target vector, context, quality, source-time metadata and a synthetic-timebase synchronization flag. All generated rows set `source_time_synchronized=true` to describe this controlled generator clock only; this is not evidence that real sensors are synchronized. Each run is treated as a distinct synthetic time origin; equal relative times in different runs are never joined. `running=true`, `speed_rpm`, and `load_fraction` are explicit context. Load changes every 5 minutes while remaining inside the declared comparable operating profile; the same load and speed trajectory is reused for every paired counterpart. Context changes are not faults. The expected target relationships are fitted only on fit-partition nonfault traces by ordinary least squares with a fixed intercept/load/speed design; deviations are residuals from that fit.

The frozen generator equations (before scoring) are:

| Signal | Expected contextual mean | Innovation SD |
| --- | --- | ---: |
| Current | `4 + 20·load_fraction + 0.012·(speed_rpm − 1450)` A | 0.70 A |
| Flow | `3 + 30·load_fraction + 0.020·(speed_rpm − 1450)` m³/h | 1.00 m³/h |
| Pressure | `1.2 + 3·load_fraction − 0.0015·(speed_rpm − 1450)` bar | 0.10 bar |

The signal equations are stimulus conveniences only; their coefficients are not engineering limits. Within each run, context load cycles through the fixed offsets `[-0.05, +0.05, 0.00, +0.07]` around a run-level baseline `0.30 + 0.05·(group_id mod 9)`, clipped to `[0.25, 0.80]`, one offset per 5-minute segment. Run speed is `1430 + 5·(group_id mod 9)` rpm. These values span the fit-only context design without leaving the declared running regime.

Residual innovations are generated from a private, deterministic SplitMix64 stream seeded by the first 64 bits of SHA-256 over the literal protocol namespace plus the numeric group ID. Twelve open-interval uniforms minus six form each unit-variance Irwin–Hall approximation to a normal variate; no Python `random` state, external PRNG, or hidden seed is used. A fixed lower-Cholesky transform gives baseline innovation correlation
`R0 = [[1, .65, .25], [.65, 1, .20], [.25, .20, 1]]`.
An AR(1) filter with `phi=0.65` is applied per residual channel; no state crosses a run boundary. Baseline and injected counterparts for a group share the same seed, exogenous context, and underlying innovation stream, so they are paired experimental variants, **not independent replications**. Distinct group IDs have distinct streams. Float comparison tolerance across platforms is declared in §9.

## 3. Frozen source groups, split, purge, and bounds

There are exactly 600 independent synthetic source groups, ordered by group ID as the synthetic acquisition chronology. Assignment is contiguous and never shuffled:

| Partition | Group IDs | Groups | Independent unit |
| --- | --- | ---: | --- |
| Fit | 000–359 | 360 | One complete seeded run |
| Calibration | 360–479 | 120 | One complete seeded run |
| Final holdout | 480–599 | 120 | One complete seeded run |

This retains the plan's 60/20/20 blocked split. A group, its nonfault trace, and all injected counterparts stay in one partition. Adjacent samples are not independent observations. The maximum lookback is four prior eligible decisions for 3-of-5 confirmation; the run boundary resets episode state, and no run crosses a partition, so no temporal rows are shared and no additional inter-partition purge is needed. The generator emits no rolling feature. An ineligible point, missing expected point, context mismatch, duplicate/misaligned time, or gap greater than twice cadence interrupts an episode as unknown, never as a normal vote.

At 120 samples per group, each OS fit/calibration execution has a hard maximum of 144,000 vector samples (57,600 baseline fit/calibration samples plus 86,400 calibration counterpart samples). The designated Windows holdout has a hard maximum of 100,800 vector samples (14,400 baseline plus 86,400 counterparts). The full single-platform experiment is at most 244,800 vector samples / 734,400 scalar target values. No generated row-level file is written; traces are streamed by group, and only bounded fit arrays, calibration score lists, one group's holdout trace, and aggregate metrics may be resident. Checked-in outputs are limited to the protocol, manifest, source/tests, frozen configuration, receipt, and compact aggregate reports; no raw traces or per-sample scores. Total committed research directory must remain under 1 MiB.

The bench's independent-group counts are evidence units only and do not model the product query path. For a future proposed three-signal on-demand query at 10-second cadence, one fit reference of at most 24 hours is 25,920 aggregate raw signal rows; a 4-hour calibration slice is 4,320; and a 15-minute score window is 270. Their sum is 30,510, below the existing 50,000 aggregate-row limit if requested together. Those illustrative windows are not approved product parameters, and this experiment does not change the limit or prove those windows statistically sufficient.

## 4. Scenarios and labels fixed independently of scores

Event onset is fixed at sample index 12 (120 simulated seconds). All strong shifts persist from that index through the final sample: 108 samples, or 18 simulated minutes of exposure at the declared cadence, exceeding the plan's 15-minute floor. Labels and onsets come only from this scenario manifest, never from detector output.

| Scenario | Stimulus from index 12 | Role |
| --- | --- | --- |
| `nonfault-context` | No added deviation; ordinary load/speed context and correlated autocorrelated noise continue | Known synthetic nonfault denominator and point/episode false-positive control |
| `single-current-strong` | Add `+3.5 × fit-only current residual robust scale` A to current only | Required strong single-target class |
| `multi-target-strong` | Add `+3.5 × each corresponding fit-only residual robust scale` to all three targets | Required strong multi-target class |
| `relationship-strong` | Preserve marginal residual means/variances; change current/flow innovation correlation from `+0.65` to `−0.65`, leaving the other declared correlations fixed, using the same stream and AR filter | Required cross-signal relationship class; the pre-score covariance change is `Δcov(current, flow) = (−0.65 − 0.65) × SD_current × SD_flow` in A·m³/h, where each SD is the frozen raw innovation SD in §2. No detector threshold defines the stimulus |
| `single-current-1scale` | Add `+1.0 × fit-only current residual robust scale` A | Exploratory smaller deviation |
| `single-current-2scale` | Add `+2.0 × fit-only current residual robust scale` A | Exploratory smaller deviation |
| `single-current-ramp` | Linear current residual ramp from 0 at onset to `+3.5 × fit-only scale` over 10 simulated minutes, then hold | Exploratory gradual deviation; report ramp latency separately |

Every counterpart preserves its source block's context and quality. The correlation matrices and all generator bounds are validated as finite positive-definite matrices before generation. A change in the measured synthetic context is not itself a fault. No optional target is silently fabricated as an observed signal.

## 5. Fit, candidate grid, threshold, and calibration-only selection

Fit the three expected-signal linear models on the fit partition's nonfault rows only. Compute residual median and `1.4826 × MAD` independently per target using only fit residuals; zero/nonfinite scales are ineligible, never replaced by epsilon. Robust-transform the same fit residual rows. Compute sample covariance centered by the transformed fit mean; report its rank and condition. For each predeclared lambda, compute `(1−lambda)C + lambda·I`, diagnostics, and Cholesky factor; reject nonfinite, non-positive-definite, or condition number above `1e6`. Score joint distance as `zᵀCλ⁻¹z`; the control is `max(abs(z_j))`. Reuse only the identified pure numerical routines from the prior harness; do not import its data readers, experiment configuration, output writer, or consumed-holdout guard.

For each joint lambda and the independent control, freeze a threshold as the nearest-rank 99th percentile of the calibration **nonfault** score list: sorted index `ceil(0.99·n)−1`. It is a nominal threshold, not an observed false-positive guarantee. Apply the same frozen fit transform, candidate and threshold to calibration counterpart scenarios and later to holdout. A candidate's calibration-only feasibility check uses the existing synthetic analogues of all four point/episode/detection targets (≤1% point FPs, ≤1 false episode per 24 simulated eligible hours, ≥90% recall per strong class, and ≥90% of all strong episodes detected within 10 simulated minutes) plus numerical conditioning. Among feasible joint candidates, choose highest minimum recall across the three strong classes, then fewer false episodes, then smallest lambda. If none is feasible, choose by highest minimum strong-class recall, then fewer false episodes, then smallest lambda and explicitly freeze it as calibration-infeasible. This deterministic fallback is a way to conduct the predeclared holdout comparison, not permission to claim success. The independent control is always reported and is never selected as a joint winner. No other candidate, threshold, split, context, or effect size may be searched.

An eligible decision requires all three finite targets and all declared context values; source time present, unique, exactly cadence-aligned, verified and marked synchronized by the synthetic generator; `quality=Good`; matching context/profile and configuration version; and known cadence. `source_time_synchronized=false` yields `NO-SCORE`; the true value applies only to the generated common timebase. No imputation, interpolation, nearest-time match, epsilon scale, or implicit synchronization is permitted. Missing/unknown/bad quality, missing/unverified/unsynchronized/misaligned/duplicate time, cadence unknown, gap, context/version mismatch, constant target, nonfinite input, insufficient independent groups, singular design/covariance, failed Cholesky, or ill-conditioning yields a named NO-SCORE/INSUFFICIENT state. It is never counted as a normal vote or hidden in the denominator.

An episode confirms at 3 of 5 consecutive eligible decisions above threshold, within 5 minutes. Five consecutive eligible below-threshold decisions terminate it. Any gap, ineligible row, or context/version change interrupts it as unknown. At 10-second cadence, event latency is the simulated time from manifest onset to first confirmation; misses remain in the recall denominator. Never sleep to represent simulated time.

## 6. Holdout identity and one-use rule

The only final experimental holdout identity is `W15-INTEL-4F-SYNTH-BENCH-HOLDOUT-2026-10-20-01`, on **Windows / CPython 3.14.6**. No holdout is run on Linux. Before generation, atomically create a unique one-use receipt under external scratch storage, binding the protocol file digest, experiment-manifest digest, generator source digest, frozen configuration digest, partition digest, holdout identity and designated platform. Text-file SHA-256 inputs are UTF-8 with CRLF/CR normalized to LF; JSON identities use sorted-key, compact UTF-8 serialization with non-finite numbers rejected. An existing receipt for that identity is a hard abort. The receipt is reserved only after the frozen-configuration commit and before any holdout generator or scorer executes. Tests use separate identities and fixtures; they never claim, read, or consume this receipt. No second run, replacement seed, retune, relabel, unchanged-head repeat, or holdout-guided repair is permitted. On failure, preserve the failure and stop.

## 7. Metrics, denominators, uncertainty, and result language

Report for each candidate and scenario: eligible/ineligible denominators and reasons; point exceedance numerator/rate on manifest-known synthetic nonfault only; confirmed episode count; eligible simulated hours; false episodes per 24 eligible simulated hours; per-class recall (120 source blocks per strong holdout class), missed/late count, latency distribution, and comparison with the marginal control. The four event variants from a source run are clustered under that run and never counted as four independent source groups. Smaller shifts and ramp are characterization only.

Use a fixed 2,000-replicate nonparametric source-block bootstrap, percentile 95% intervals, resampling whole group IDs and carrying all variants together, for point rate, class recall and detected-event latency. For false episodes, report that same cluster-bootstrap interval plus a Wilson 95% interval for the fraction of independent normal groups with one or more false episodes; the latter is the explicit safeguard against an all-zero bootstrap appearing precise. No interval treats adjacent points as independent. Report raw counts alongside every interval. Mark the class estimates INSUFFICIENT if fewer than 100 fresh independent held-out source groups survive eligibility.

The synthetic analogue may be described as meeting a numerical target only if the point estimate meets it, the predeclared denominator is adequate, and uncertainty is shown; it remains conditional on this generator regardless. The real 168 eligible normal/known-nonfault operating hours across at least 14 calendar days, field false-episode rate, product authorization/persistence/database behavior, synchronization provenance, equipment health, diagnosis, prediction, RUL and standards certification all remain **UNMEASURED / NOT PROVEN**. Simulated minutes are never physical exposure.

## 8. Mechanism tests, resource limits, and platform comparison

Before fit/calibration, focused small unit fixtures must cover deterministic seeded generation; exact grouped assignment and no seed/partition leakage; independent scenario labels; fit-only contextual regression/robust scales/covariance; fixed candidate grid and tie-break; configuration/protocol/source hash verification; no-score/ineligibility cases in §5; constant/zero-scale/singular/ill-conditioned/nonfinite failure; exact 3-of-5 confirmation, 5-below termination, cadence/gap/context interruption and simulated latency; cluster aggregation/denominators; atomic one-use-receipt rejection in a separate disposable test directory; and bounded failures. The tests may use hand-authored tiny synthetic rows only. They must not instantiate the final holdout identity or read prior Stage 4F result artifacts.

Run focused tests and fit/calibration with identical declared seeds on Windows and an isolated Linux CPython 3.14.6 image. Compare fitted regression coefficients/scales/covariance/factors within absolute `1e−10`, frozen candidate/thresholds and aggregate metrics within relative `1e−8` (exact identities, counts, labels and partition hashes must match). Any candidate choice, count, label, or identity mismatch is a CROSS-PLATFORM DIVERGENCE and is not repaired by changing tolerances after observing the difference. Record OS, Python, image digest, CPU time, wall time and peak RSS per run.

Resource ceiling per full OS fit/calibration run, and separately for the one designated-platform holdout run: one CPU process, ≤512 MiB peak RSS, ≤15 minutes wall time, no sleep, no external numerical dependency. Exceedance is reported as FAIL/INSUFFICIENT, not permission to reduce the holdout, alter product limits, or enlarge resources post hoc. Row/file bounds are in §3. The numerical code remains standard-library-only.

## 9. Finite completion matrix

The final report must separate: (a) generator/evaluator/guard mechanics DEMONSTRATED or failed; (b) synthetic-conditional numerical performance PASS/FAIL/INSUFFICIENT against each unchanged target; and (c) product/field assertions NOT PROVEN. It must state exact base, branch/head, the protocol → harness/tests → frozen config → holdout receipt/results commit chain and hashes, Windows/Linux tests and resource/tolerance comparisons, and one finite next action for Main. No automatic Stage 4G/5B/6, no product method approval, no changes outside this folder, and no CI dispatch or merge by this executor.
