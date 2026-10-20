# Stage 4F windowed relationship study — protocol v1

**State: preregistered before any fit, calibration, or study generation.** This is the one finite, synthetic-only experiment authorized by issue #680. It follows the read-only design in #678 and tests one fixed 30-cohort statistic; it does not select or release a product method. No result is included here.

## 1. Scope and evidence boundary

The question is whether one fixed rolling-window score can jointly expose contextualized level, dispersion, and linear relationship changes in three synthetic pump signals. All values, contexts, quality flags, timestamps, source identities, labels, and exposure are generated locally from this protocol. This is not measured equipment, a digital twin, field validation, equipment health, diagnosis, RUL, a standards claim, or evidence that an EliteSCADA product API or method is ready.

Only this directory may change. Do not read or mutate the owner Preview at `http://localhost:18080`, its data, volumes, ports, product code, API, database, UI, schema, SQL, HA, drivers, licensing, previous research artifacts, or 4A–4E checkpoint. Do not use raw or owner data, archives, downloads, external services, GPU, new dependencies, or background services. The implementation is independently written here; it must not import or invoke previous study generators, readers, fit/calibration routines, outputs, holdout guards, or consumed identities. Raw rows and per-cohort scores stay in memory and are never persisted.

## 2. Frozen experiment identity and partitions

| Item | Fixed value |
|---|---|
| Product checkpoint preserved | `793b8303547491e02b98bf73732d3ab5ab76567a` |
| Owner Preview preserved | `http://localhost:18080` at `0054d587fe855bbea4b1af81726af016208643da` |
| Inspected source base | `wave15/corrections-integration@2da9cfa76a9d21b4d6b31097ed0921edaba2c01d` |
| Branch | `work/w15-intelligence-stage4f-windowed-study` |
| Seed namespace | `ELITESCADA-W15-4F-WINDOWED-v1` |
| Experiment identity | `W15-INTEL-4F-WINDOWED-2026-10-20-01` |
| One-use holdout identity | `W15-INTEL-4F-WINDOWED-HOLDOUT-2026-10-20-01` |
| Independent source groups | 240; IDs are unique and never shared across partitions |
| Fit groups | 0–59, baseline only (60 groups × 120 cohorts = 7,200 vectors) |
| Calibration groups | 60–119 (60 groups; every declared scenario) |
| Holdout groups | 120–239 (120 groups; every declared scenario) |
| Cohorts per group | 120 at exactly 10 s cadence, indices 0–119 |
| Injection onset | index 12, source time 120 s; event continues through index 119 |
| UNIT namespace | `ELITESCADA-W15-4F-WINDOWED-UNIT-v1`; never aliases a study identity |

Group seed is the first eight bytes, big-endian, of SHA-256 over UTF-8 `namespace|group-NNN`. Audit all 240 seeds for uniqueness and all group IDs for exact, disjoint assignment. Implement portable SplitMix64 locally: add `0x9E3779B97F4A7C15` modulo 2^64; xor-shift/multiply by `0xBF58476D1CE4E5B9`, xor-shift/multiply by `0x94D049BB133111EB`, final xor-shift; open uniform is `((next_u64 >> 11)+0.5)/2^53`; one unit-normal approximation is the sum of twelve open uniforms minus six. No random stream crosses groups. Each scenario variant for a group uses the same independent innovations and context sequence.

## 3. Fixed synthetic context and scenarios

Target order is motor current (A), flow (m³/h), discharge pressure (bar). For group `g`, `speed=1430+5*(g mod 9)` rpm. For cohort `i`, `load=clip(0.30+0.05*(g mod 9)+offset[floor(i/30)], 0.25, 0.80)`, offsets `[-0.05,+0.05,0,+0.07]`. `running=true`; the synthetic input context is finite, `quality=Good`, synchronized, cadence-aligned, and carries one stable source/profile/version identity for the run.

Contextual expected targets are frozen as:

| Target | Expected value | Innovation SD |
|---|---|---:|
| Current | `4 + 20*load + 0.012*(speed-1450)` | 0.70 |
| Flow | `3 + 30*load + 0.020*(speed-1450)` | 1.00 |
| Pressure | `1.2 + 3*load - 0.0015*(speed-1450)` | 0.10 |

The baseline innovation correlation matrix in target order is `R0=[[1,.65,.25],[.65,1,.20],[.25,.20,1]]`. Generate independent unit-normal approximations, apply the lower Cholesky factor of `R0`, then AR(1) with `phi=.65` and innovation multiplier `sqrt(1-phi^2)`. The relationship-change matrix changes only current/flow to −.65: `R1=[[1,-.65,.25],[-.65,1,.20],[.25,.20,1]]`. The matrices and factors must be finite and positive definite; otherwise fail closed.

Exactly these ten scenarios are generated, using shared group noise and fixed onset:

| Scenario | Post-onset change | Evidence role |
|---|---|---|
| `nonfault-context` | No injection | Required nonfault reference/false-alarm class |
| `current-plus-3-fit-scales` | Add `3*s_current` to current | Required strong class |
| `all-targets-plus-3-fit-scales` | Add `3*s_j` to each target `j` | Required strong class |
| `relationship-current-flow-flip` | Use `R1`; preserve marginal innovation SDs and other pair correlations | Required strong class |
| `residual-dispersion-2x` | Multiply each residual about its unchanged contextual mean by 2 | Required strong class |
| `residual-dispersion-0.5x` | Multiply each residual about its unchanged contextual mean by 0.5 | Required strong class |
| `mixed-relationship-and-current-plus-3` | Use `R1` and add `3*s_current` to current | Required strong class |
| `current-plus-1-fit-scale` | Add `s_current` to current | Descriptive sensitivity only |
| `current-plus-2-fit-scales` | Add `2*s_current` to current | Descriptive sensitivity only |
| `current-linear-ramp-to-3` | Current shift ramps from zero at onset to `3*s_current` across 60 samples, then holds | Descriptive sensitivity only |

Here `s_j` is the target's frozen fit-only residual scale below. Dispersion multipliers apply to post-onset AR residuals, not to the contextual expected mean; their correlation construction stays unchanged. No scenario, seed, effect, context, duration, noise, or dimension may be changed after outcomes are seen. The six required strong classes are the six rows explicitly labeled required; the three sensitivity cases do not gain strong-class pass status.

## 4. Fit-only reference and the sole score

Fit a three-target ordinary least-squares contextual baseline from the 7,200 fit-partition nonfault cohorts only, with design columns `(1, load, (speed-1450)/20)`. Diagnose finite values, design rank exactly 3, and 2-norm design condition `<=1e6`; any failure stops the study. For each target, `e_j=x_j-predicted_j`. Freeze `m_j=median_fit(e_j)` and `s_j=1.4826*median_fit(abs(e_j-m_j))`. Every scale must be positive finite. Pearson reference correlations are computed across the same fit-only residual cohorts; every target variance must be positive finite.

For each *complete* 30-consecutive-eligible-cohort residual window `W`, updated every cohort at 10 s cadence and never spanning a group/run boundary, compute exactly:

* `L(W)=max_j(abs(median_W(e_j)-m_j)/s_j)`;
* `D(W)=max_j |log(MAD_W(e_j)/MAD_R(e_j))|`, where `MAD_R(e_j)=s_j/1.4826`;
* `C(W)=2*sum_(j<k)(rho_jk,W-rho_jk,R)^2`.

`MAD_W` is the median absolute deviation about that window's median. Pearson correlations use finite, positive-variance residual columns. Fit-only component centers and scales are `center_k=median(component_k)` and `scale_k=1.4826*MAD(component_k)` over every complete full-window output from the 60 fit nonfault groups, processed with a fresh window per group. Require each component scale positive finite. Standardize with signed upper-tail values `Z_k=(component_k-center_k)/scale_k`; the one score is `T=max(Z_L,Z_D,Z_C)`. Do not take absolute values of `Z`, tune components separately, drop channels, clip, impute, add epsilons, substitute identity covariance, or treat T as a probability.

The only threshold is the nearest-rank `q=0.999` of `T` from complete nonfault calibration windows in groups 60–119: sort ascending and choose zero-based index `ceil(q*n)-1`. No threshold sweep, tuning, alternate score, or injected calibration rows. Required outputs are the three component contributions and this single T decision; component values are descriptive, not independent alarms.

## 5. Eligibility, windows and episode state

A cohort may enter a window only when all three targets and declared context are finite; `running=true`; `quality=Good`; synchronization/source evidence is verified; source time and index are unique, cadence-aligned at 10 s; and source/profile/model version match within the run. Duplicate-time keys invalidate every row with that key. Missing, ineligible, nonfinite, unsynchronized, unknown-cadence, duplicate, gap, or context/profile/version boundary clears the window and episode decision history. A valid cohort after a reset starts a new ring. Do not score partial windows. A full window with nonpositive/nonfinite target/window MAD or variance, nonfinite correlation/component, or other numerical degeneracy produces no score and clears ring/episode state; no fallback value is allowed. No-score breaks episode history.

Warm-up is the 29 eligible cohorts before the first complete window. Do not count those as scored normal decisions, but do not exclude their elapsed source time from event latency. Report raw eligible exposure, scored eligible exposure, and availability separately. Raw eligible time is valid cohort count × 10 s; scored eligible time is T output count × 10 s; availability is scored/raw eligible time, with zero when raw exposure is zero. The false-episode rate denominator is scored eligible time.

Apply the fixed rolling five-*scored-decision* 3-of-5 entry convention and five-consecutive-below termination/rearm. A decision is above only when `T > threshold` (strict). Require a complete five-score history to enter. Confirmation time is the current fifth-decision cohort; episode history is retained on termination. Five consecutive below-threshold decisions terminate/rearm. A no-score/reset clears decision history and active state. At an event run, count a timely detection only for a new episode confirmed at or after source onset and within 600 simulated seconds; a pre-onset episode is not retroactively credited. Latency is confirmation source time minus true injection source time, including warm-up. Misses/late cases remain in the 120-group denominator.

## 6. Calibration, metrics and uncertainty

Calibration uses only independent groups 60–119; the threshold uses only their `nonfault-context` scores. Then report calibration aggregates for all ten scenarios without refitting. No result from calibration changes the protocol or score. Holdout uses the one frozen fit/reference/threshold configuration and groups 120–239 exactly once.

For nonfault and each scenario, report exact group and cohort denominators; reason counts; eligible/ineligible counts; warm-up/no-score counts; raw and scored exposure; availability; point exceedances (`T>threshold`) and rate over scored nonfault decisions; episodes; false episodes per 24 scored eligible synthetic hours; event recall per source group for each required strong class; timely fraction within 600 s; misses/late detections; and detected source-time latency p50/p95/max. Report calibration and holdout separately. Never treat overlapping windows or AR cohorts as independent trials.

Holdout uncertainty uses 2,000 deterministic percentile 95% cluster-bootstrap replicates: resample whole independent group IDs, retaining all scenario variants for each selected group. Use the same 2,000 resample index vectors for every scenario and metric so the source-group pairing is retained. Replicate seed derives from the first eight big-endian SHA-256 bytes of `experiment|bootstrap|holdout|replicate-NNNN`; bounded draws use rejection sampling before modulo so group indices are uniform. Bootstrap nonfault point-FP and false-episode rates, group recall, timely fraction, and detected latency p50/p95. Bootstrap and latency percentiles use the same nearest-rank order statistic `sorted[ceil(q*n)-1]` (95% interval endpoints q=.025/.975). If no nonfault source group has an episode, also report a Wilson 95% interval for group occurrence so an all-zero bootstrap is not represented as precise. Fewer than 100 independently eligible holdout groups in a required class is `INSUFFICIENT`, never PASS.

For each of the six sustained required classes, report PASS/FAIL/INSUFFICIENT separately against the unchanged synthetic point targets: point FP `<=1%`; false episodes `<=1 per 24 scored eligible synthetic hours`; event recall `>=90%`; and timely detections within 600 s `>=90%`. Show numerators, denominators, intervals, and group count. A passing point estimate with wide intervals is not a reliable rate guarantee. The exact tests are synthetic-conditional only. Real 168 eligible normal hours/14 calendar days, field false-episode performance, physical synchronization/provenance, equipment health, diagnosis, RUL, standards, product integration, product authorization, persistence, and deployment readiness remain `UNMEASURED / NOT PROVEN`, regardless of results.

## 7. Bounded request model, mechanics and resources

For a bounded reference-plus-evaluation request, count the union of every distinct target/context TAG, including Boolean running, before filtering or deduplication. Exact `p=3` row envelope: `(7200+120)*(3 targets+3 context tags)=43,920`, below 50,000 raw rows. With `p=8`, `(7200+120)*(8+3)=80,520`, so reject before a request. Invalid/duplicate rows still consume the raw-row budget. `pageSize=200` means rows per page, not total pages; windows are at most 31 days and outputs at most 500. These request bounds are independent of the study's cumulative synthetic rows. UNIT p=2/p=8 tests validate mechanics only, not p=8 empirical detection.

The straightforward robust window routine must honestly account for `O(p*w*log(w))` median/MAD work and correlation `O(p^2*w)` when recomputed, for `p<=8,w=30`. Tests must cover hand-computed formulas, fit-only leakage, provenance/group split, order and positive affine unit invariance including intercept, p=2/p=8 finite mechanics and degeneracy, correlation-sign vs scale-only distinction, ineligible/gap/duplicate reset, warm-up/latency denominators, full 3-of-5 and termination/rearm, budget boundaries, outlier/nonlinear limitations, interrupted atomic one-use claims, receipt replay refusal, and no-overwrite. Tests use tiny `UNIT` fixtures only and must never instantiate the study holdout identity.

Use standard library only, 1 CPU, 512 MiB memory ceiling, no network in the Linux container; use an immutable verified Python 3.14.6 Linux image digest. Keep each fit/calibration and holdout process within 512 MiB peak RSS, 15 CPU minutes, and 20 wall minutes; tests are excluded from the study-runtime budget. Stop on any limit; do not expand it, omit cases, or weaken gates. Keep committed artifacts collectively below 1 MiB. Report actual Python/OS/image, CPU/wall/RSS, group/cohort processing, and artifact bytes. Run Windows and isolated Linux tests and fit/calibration. Compare all non-environment numeric fit/calibration values with absolute and relative tolerance no looser than `1e-9`; exact partition/scenario/threshold selection and statuses must match. Never run Linux holdout.

## 8. One-use holdout protocol, provenance and delivery

First commit this protocol and its manifest before any fit/calibration/study generation. Implement and commit harness/tests next. Run focused UNIT tests only. Fit/calibration runs on Windows and isolated Linux; compare results and freeze configuration only after the comparison. The frozen config must bind protocol, manifest, harness, tests, output artifacts, runtime, and exact pre-holdout source commit. Commit the frozen configuration before holdout.

Immediately before holdout generation, atomically claim the exact holdout identity in canonical per-user durable state under `%LOCALAPPDATA%\EliteSCADA\research-holdout-claims\<sha256(identity)>.json`, using exclusive create and durable flush. Check both that claim and this directory's receipt before generation. The claim binds all protocol/manifest/harness/tests/frozen-config hashes and pre-holdout commit. Any existing claim or receipt is a hard stop, including an interrupted claim, across scratch directories for this user. Persist aggregate and a receipt with exclusive create only after successful completion; never overwrite. The committed receipt prevents replay from clean repository clones. Do not delete/reset claims, re-seed, re-fit, re-calibrate, retune, change groups, or retry after a claim. If interrupted or failed after claiming, preserve that state and stop for Main's separate decision.

Final delivery is one coherent draft research PR with `VALIDATION_PROFILE: DOCS_I18N_HELP` in its body at creation, exact base/head, protocol → harness/tests → fit/calibration → frozen config → holdout receipt/results commit chain, hashes, Windows/Linux tests and numerical/resource comparison, compact aggregates, per-target status, limitations, and one finite recommendation. Main owns T1, review, and integration. No manual CI, self-merge, follow-on experiment/search, product patch, 4G/5B/6, or changes outside this directory. A negative result is valid completion; do not force a winner. #588 remains PARTIAL and the product gate NOT READY.

Primary-source rationale supports concepts, not these unvalidated parameters: [NIST multivariate mean/dispersion monitoring](https://www.itl.nist.gov/div898/handbook/pmc/section3/pmc34.htm), [NIST MAD and scale limitations](https://www.itl.nist.gov/div898/handbook/eda/section3/eda356.htm), and [NIST Pearson formula and linear-association meaning](https://www.itl.nist.gov/div898/software/dataplot/refman2/auxillar/correlat.htm). No iid/normal chi-square limit is assumed.
