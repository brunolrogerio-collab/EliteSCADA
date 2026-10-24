VALIDATION_PROFILE: FOUNDATION_LIFECYCLE

# Intelligence innovation repair — implemented core and finite validation

**Disposition: experimental core implemented; all six synthetic point acceptance gates PASS; product integration and field validation remain pending.** This owner-directed follow-up addresses the failed #680 candidate with a new calculation, new preregistration, fresh data identities and a single new holdout. It does not reinterpret or replace the negative evidence in PR #682.

## Concrete change and result

The old `max(Z_L,Z_D,Z_C)` score left a stand-alone dispersion 2x/0.5x contribution well below its common threshold: its dispersion component would need an approximately 10.43x or 0.096x ratio to cross by itself. The new internal core estimates temporal dependence from fit-only residual segments, whitens their innovations with a fitted full covariance, and computes the joint mean/covariance Gaussian likelihood contrast. It retains separate level/covariance terms. The new threshold is calibrated on rolling 3-of-5 **episode-entry run maxima**, rather than a marginal point quantile. No normal-theory chi-square probability is returned.

`src/Scada.Core/Intelligence/Experimental/InnovationWindowContrast.cs` is the actual calculation source linked into the validation executable. It owns its fitted arrays, accepts 2..8 dimensions, uses fixed 40-innovation windows, rejects nonstationary/nonfinite/degenerate/ill-conditioned evidence, requires exact UTC cadence/context binding and clears all temporal state after rejected evidence. A complete five-decision 3-of-5 entry and five-below rearm are tested. No API, UI, classifier, alarm or worker is registered; this leaves the experimental source available for Main's product-contract review.

## Independent holdout evidence

All 1,000 independent source groups per scenario remained eligible, with zero invalid windows. Each scenario had 120,000 eligible input cohorts, 40,000 warm-up cohorts, 80,000 scored decisions and 800,000 scored eligible seconds (222.222 hours); availability 66.667%. Event latency includes model/window initialization and uses true injected source time. The six injections retain the same equations, magnitudes and noise family as #680; group identities/seeds are fresh and disjoint. This is not a paired rerun of the old study, so no causal effect size or field claim is inferred from the before/after numbers.

| Common normal gate | Observed | Target | Result |
| --- | --- | --- | --- |
| Point false positives | 7/80,000 = 0.00875%; 95% group-bootstrap 0–0.02375% | <=1% | PASS_POINT |
| False episodes | 1 episode in 1/1,000 groups; 0.108 per 24 scored hours; 95% bootstrap 0–0.324 | <=1 per 24 scored hours | PASS_POINT |

The Wilson 95% interval for normal group occurrence is 0.000176546–0.005642559. Group occurrence, point exceedances and episodes/time are different quantities. No field reliability guarantee follows from a bootstrap point pass.

| Required class | Recall | Timely<=600s | Latency p50/p95/max(s) | Combined gate |
| --- | --- | --- | --- | --- |
| Current+3 fit residual scales | 1,000/1,000 | 1,000/1,000 | 320/320/320 | PASS_POINT |
| All three+3 scales | 1,000/1,000 | 1,000/1,000 | 320/320/320 | PASS_POINT |
| Current/flow correlation sign flip | 1,000/1,000 | 1,000/1,000 | 320/320/380 | PASS_POINT |
| Residual dispersion2x | 1,000/1,000 | 1,000/1,000 | 320/320/320 | PASS_POINT |
| Residual dispersion0.5x | 1,000/1,000 | 1,000/1,000 | 320/350/400 | PASS_POINT |
| Mixed correlation/current+3 | 1,000/1,000 | 1,000/1,000 | 320/320/320 | PASS_POINT |

All six empirical group-bootstrap recall/timely intervals are [1,1], because every sampled group succeeded. That is a resampling limitation, not proof of certainty. For 1,000/1,000 independent Bernoulli group successes, the corresponding Wilson 95% interval is [0.996173,1]; this additional reporting calculation uses existing counts and does not consume or regenerate observations. Latency 320s is the earliest possible entry for this onset/cadence/window/complete-five setup. The worst observed class latency was 400s, below 600s.

## Verification, provenance and resources

- Preregistered protocol commit `faa417d5`; final source/UNIT commit `bafc0f11`; Windows fit/calibration commit `aff9b50e`; comparison/configuration freeze commit `e1d5349d`.
- UNIT harness:33/33 Windows .NET10.0.13;33/33 Linux .NET10.0.11. It includes independent Fourier mean/dispersion/correlation oracles, affine-unit and order invariance, invalid/gap/duplicate resets, fit ownership, episode/rearm and atomic no-overwrite. Outlier and nonlinear fixtures explicitly demonstrate the method's limitations.
- Product suite:13/13 new focused xUnit cases passed on Windows and Linux. Entire `Scada.Core.Tests` suite passed698/698 on Windows. Product Core and the exact-source study executable built without warnings.
- Linux SDK image `mcr.microsoft.com/dotnet/sdk@sha256:4beef5b8919dcaa2dc924233bd069257e883cc7a061e09088a97d152d6a48510`,1CPU/512MiB, network disabled, root read-only. Only new candidate/study sources or new test binaries were mounted. Host-attested clean source commit is recorded. Disposable containers used `--rm` and were removed on exit.
- Fit/calibration numerical difference between Windows/Linux was exactly0. The one nearest-rank run-entry q=.999 threshold is35.57963109418079 from2,400 independent normal calibration runs (2 runs strictly above). No injected calibration observations or threshold search.
- Windows fit/calibration:14.647s wall,13.969s CPU,55,447,552 bytes peak RSS. Linux:2.961s wall,2.999s CPU,68,608,000 bytes peak RSS. Final Windows holdout:8.057s wall,7.750s CPU,61,272,064 bytes peak RSS. All declared resource bounds passed.
- One atomic per-user claim before holdout generation, source `e1d5349de2a89686e417a28a812479fcee4d5d79`; frozen-config SHA256 `32B6845E3DA180A62A078E11DBCB56374CAAECCE4DC2B052FB088C4079D96AFB`; aggregate SHA256 `62388DCBCABCB82EEB06AE5F3C2DF8FD1D6EAF576E4C6B29F697C0511988A0FB`. Receipt has consumptionCount1/replayPermittedfalse. No retry, retuning or regenerated holdout.

## Reporting erratum and limits

The generic aggregator leaves `latencySeconds:870` in the nonfault scenario. That field is the time of the **false episode** relative to index12, not latency of a true event; nonfault has no injected event and its event latency is **not applicable**. Its recall/timely fields are correctlynull. This cosmetic reporting erratum is preserved explicitly; no evidence or frozen source was edited to hide it. The fault-class latency and common counts/gates are unaffected.

This candidate is Gaussian-motivated and non-robust. AR(1) prewhitening is not a universal time-series model; heavy tails, outliers, nonlinearity, unmodeled dynamics and drifting context need representative checks. Synthetic elapsed exposure does not satisfy real168 eligible normal hours/14 calendar days. All field health, physical synchronization, diagnosis, fault probability and RUL claims remain unmeasured.

## Handoff to Main

The Product Owner requested a concrete improvement after the negative study. This package supplies a tested candidate in product source with positive independent synthetic evidence. Main should review the calculation and the new experimental boundary, then define the additive model/reference/calibration/context API contract and representative Historian validation before runtime activation. Preserve the canonical entitlement/permit/TagRead/revocation/HA fences and query bounds when integrating; the pure core does not prove them. The frozen4A–4E APIs, owner Preview/data and previous research remain unchanged.

Stage4F is not declared delivered as a user-facing feature; #588 and Stage4G/5B/6 remain open. Main owns T1, review and merge on the exact PR head. No manual CI, self-merge, Preview rebuild or further model search was performed.
