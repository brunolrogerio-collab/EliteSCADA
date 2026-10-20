# Intelligence innovation repair — preregistration v1

## Authorization and containment

The Product Owner directly instructed this executor on 2026-10-20: "Se o problema é o produto, então arrume e melhore ele". This is a separate follow-up to the completed #680 study, whose files, identities and holdout remain immutable. Base: `wave15/corrections-integration@1a131cd28b1f5a20d94bc88f9b4bba950949548f`. The owner Preview remains untouched.

The failed combined statistic was never deployed. This package develops and tests a new **internal experimental calculation core**, without registering an API, background worker, alarm, process command, or replacement for the frozen descriptive contracts. Product adoption and representative field validation are distinct later gates. Main must review this owner-directed follow-up and its API/context/model contract before activation.

## Diagnosis and one fixed candidate

The previous study standardized the maximum of three component types by their fit median/MAD and used one point-score quantile for episode decisions. Its calibration artifact already has a nonfault episode-rate failure, current-only timely detection below 90%, and very weak scale-change sensitivity. It cannot identify a software defect in the existing descriptive product. The concrete algebraic limitation is that a dispersion change contributes only `abs(log(scale ratio))`; at the old center 0.361068, scale 0.181031, threshold 10.959599 it needs a ratio outside approximately [0.096, 10.43] to cross the shared threshold by itself. A 2x or 0.5x dispersion is therefore not directly sufficient. This is a consequence of the declared formula, not a reason to replay old data.

One new candidate, chosen before generating its data, uses fit-only contextual residuals and an estimated per-target AR(1) model. Learn residual center, AR coefficient and full innovation covariance from fit runs only, never joining across run boundaries. Reject nonfinite, nonstationary (`abs(phi)>=0.98`), zero-scale, rank-deficient or ill-conditioned references. No epsilon, clipping, identity substitution or data repair.

Whiten innovations with the fitted positive-definite covariance. On exactly **40 innovations** use the multivariate Gaussian likelihood contrast

`G = n * (mean' mean + trace(S) - p - log(det(S)))`,

where S is the centered maximum-likelihood covariance of whitened innovations. Return separate level and covariance terms for explanation. This jointly includes increases/decreases in dispersion and changed cross-signal relationships. It is Gaussian-motivated, non-robust, and an empirical deviation statistic, not a chi-square probability or fault classifier. AR(1) does not prove arbitrary field dependence removed. Window 40 (400 seconds plus initialization) leaves recovery margin inside the existing 600-second target. No window/model/quantile grid or alternative candidate.

Episode entry is complete rolling 3-of-5 strict exceedances; rearm requires five consecutive below-threshold scores. Missing/nonfinite/bad/unsynchronized evidence, cadence gaps, duplicates or provenance changes reset innovation/window/episode histories. Warm-up counts against latency and is excluded from scored normal exposure.

Calibrate against **normal run episode-entry statistics**, rather than marginal point scores: for each complete five-score sequence take its third largest score; take the maximum over that run; set the sole threshold to nearest-rank q=0.999 over 2,400 independent calibration runs. This is an empirical calibration, not a field rate guarantee. No injected calibration rows and no post-result parameter change.

## New independent finite experiment

- Namespace/identity: `ELITESCADA-INTELLIGENCE-INNOVATION-REPAIR-20261020-v1`; no old generator or entry point is imported or called.
- Fresh fit groups 0..59; calibration groups 10000..12399; final holdout groups 20000..20999; distinct SHA256-derived SplitMix64 seeds. UNIT fixtures have their own namespace.
- Same three signals/context/noise equations as #680: current `4+20*load+.012*(speed-1450)`, flow `3+30*load+.020*(speed-1450)`, pressure `1.2+3*load-.0015*(speed-1450)`; marginal residual SDs .7,1,.1; AR phi .65; innovation correlation [[1,.65,.25],[.65,1,.2],[.25,.2,1]]. Normal innovations use sum-of-12 independent uniforms minus6, not a changed/easier noise distribution.
- Speed `1430+5*(group%9)`, load base `.30+.05*(group%9)` plus offsets [-.05,.05,0,.07] in 30-sample blocks, limited [.25,.80]. 120 cohorts/run, 10-second cadence, event onset index12, same sustained six classes: current +3 fit residual scales; all +3; current/flow innovation correlation sign flip; dispersion2x; dispersion0.5x; mixed sign flip/current+3. Every variant reuses its own group's innovation stream and stays in that partition.
- Fit contextual OLS intercept/load/scaled-speed on all 7,200 fit cohorts, with scaled Gaussian elimination singularity guard. Fit residual scales by 1.4826*MAD for unchanged injection magnitude. The product core consumes explicit already-residualized vectors; this experiment does not select a universal field regression/minimum or complete the product API.
- A new local harness links the actual new C# core directly. Tests include independent formula oracles; p=2/8; affine-unit/signal-order behavior; invalid/gap/duplicate/provenance resets; zero/collinear/nonstationary/overflow refusal; fit-only model ownership; full 3-of-5/rearm.
- Commit this protocol before generation; commit source and fit/calibration artifact before final holdout. Freeze exact protocol/core/harness hashes, model and threshold. Run Windows/Linux unit and fit/calibration numerical parity <=1e-9 before final holdout.
- One Windows final holdout only, atomic per-user claim before generation, no overwrite/replay/retry after claim. Any failure is preserved. Aggregates only; no raw traces persisted. Independent groups are the uncertainty unit; 2,000 paired reproducible bootstrap resamples plus Wilson group-occurrence intervals. Report availability and denominator explicitly.
- Common point FP <=1%, false episodes <=1 per24 scored eligible hours; each required class recall >=90% and timely <=600s >=90%, with >=100 eligible groups. Confidence intervals are reported and do not turn a point pass into a guarantee. All1,000 groups/class remain the denominator; no omitted cases.
- CPU<=15 minutes, wall<=20 minutes, RSS<=512MiB per process; synthetic totals are multiple offline runs, not one product request. Artifacts<=1MiB. Real historian query budgeting, authorization, permit/revocation, persistence, source-synchronization and field validation remain unmeasured by this pure core.

Stop after this one finite candidate. Publish its evidence honestly, including FAIL/INSUFFICIENT. No threshold adjustment, additional search or activation of an unvalidated runtime method follows from a synthetic pass.

## Conceptual sources

[NIST multivariate mean and dispersion monitoring](https://www.itl.nist.gov/div898/handbook/pmc/section3/pmc34.htm) distinguishes mean-vector and covariance behavior. [NIST residual model validation](https://www.itl.nist.gov/div898/handbook/pmc/section6/pmc624.htm) checks remaining serial dependence. These support the concepts, not this candidate's selected parameters, acceptance or field performance.
