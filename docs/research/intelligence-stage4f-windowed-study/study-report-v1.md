VALIDATION_PROFILE: DOCS_I18N_HELP

# Stage 4F windowed relationship study — final report v1

**Disposition: COMPLETE / SYNTHETIC PERFORMANCE FAIL / NO PRODUCT ADOPTION.** The one predeclared holdout was consumed once on Windows. No score, threshold, seed, partition, or scenario was changed after calibration. The result is valid negative research evidence for this synthetic generator, not evidence about real equipment or an EliteSCADA product.

## Provenance and scope

- Issue #680; branch `work/w15-intelligence-stage4f-windowed-study`.
- Assigned product/source base: `wave15/corrections-integration@2da9cfa76a9d21b4d6b31097ed0921edaba2c01d`. Before edits the live branch was rechecked at `1a131cd28b1f5a20d94bc88f9b4bba950949548f`; the intervening delta was four coordination documents only, so the exact assigned product base was retained.
- Preserved product checkpoint `793b8303547491e02b98bf73732d3ab5ab76567a` and owner Preview `http://localhost:18080` at `0054d587fe855bbea4b1af81726af016208643da`. No product code, Preview, database, API, UI, HA, driver, license, owner data, or 4A–4E artifact was changed or mounted.
- All study artifacts are confined to `docs/research/intelligence-stage4f-windowed-study/`. The harness independently implements the declared synthetic generator and pure calculations; it imports or invokes no previous study generator, data reader, fit/calibration routine, or holdout entry point.
- The first Windows-only fit/calibration preflight is preserved in `fit-calibration-windows-v1.json`, but it predates the Linux provenance note and is **superseded and excluded from the frozen comparison/configuration**. The frozen evidence uses only the matched `v2` Windows/Linux artifacts and their comparison.

## Frozen method

The new namespace is `ELITESCADA-W15-4F-WINDOWED-v1`; experiment `W15-INTEL-4F-WINDOWED-2026-10-20-01`; holdout `W15-INTEL-4F-WINDOWED-HOLDOUT-2026-10-20-01`. There are 240 independent source groups: fit 0–59 (7,200 nonfault cohorts), calibration 60–119, and holdout 120–239. Each group has 120 samples at 10-second cadence. Variants share the group innovation stream and stay within the same partition.

The sole score uses 30 consecutive eligible cohorts and combines contextual-residual level `L`, dispersion `D`, and pairwise Pearson relationship contrast `C`; fit-only full-window robust center/scales standardize the three components, with `T=max(Z_L,Z_D,Z_C)`. The one threshold is nearest-rank `q=.999` on 5,460 nonfault calibration windows: index 5,454, `10.959598773199875`. Entry remains complete rolling 3-of-5 with strict `score > threshold`; termination remains five consecutive below-threshold decisions. Warm-up is included in source-time latency, not scored normal exposure.

Fit regression diagnostics were rank 3 and design condition 27.6148 (limit 1e6). Fit-only robust residual scales were current 0.7066405 A, flow 1.0215794 m³/h, and pressure 0.1019090 bar. All target, window and fit-component scales were positive and finite. The one reference/analysis request envelope is 43,920 raw rows at p=3, below 50,000; the p=8 case is 80,520 and fails closed. UNIT tests, not this p=3 experiment, cover p=2/p=8 mechanics.

The ten frozen scenarios are unchanged context, six required sustained cases (current +3 fit scales, all targets +3, current/flow relationship flip, residual dispersion ×2, residual dispersion ×0.5, and mixed relationship/current +3), plus current +1, +2, and ramp sensitivity cases. Sensitivity cases are descriptive only.

## Execution and cross-platform evidence

| Stage | Runtime | Wall | CPU | Peak RSS | Result |
|---|---|---:|---:|---:|---|
| UNIT tests, Windows | CPython 3.14.6 | 0.05 s | — | — | 19/19 passed |
| UNIT tests, Linux | CPython 3.14.6, pinned digest below; network off | 0.05 s | — | — | 19/19 passed |
| Fit/calibration, Windows | CPython 3.14.6 | 12.58 s | 11.64 s | 85.3 MiB | complete |
| Fit/calibration, Linux | CPython 3.14.6, pinned digest below; network off | 6.88 s | 6.88 s | 74.1 MiB | complete |
| One-use holdout, Windows | CPython 3.14.6 | 32.54 s | 29.11 s | 48.2 MiB | consumed once |

Linux image: `python:3.14.6-slim-bookworm@sha256:4c92ffcde4dd6f1ff72a24518f49fd4990b27134987dfa31a733badde66df9f8`. It has no Git executable; before the Linux run the native host verified the clean worktree and passed exact commit `d57e32a2be6125323d57fbb8559ab22724f633b2` as a host attestation. Only this new research directory was mounted, with container root filesystem read-only and network disabled; the sole intended write was the new Linux aggregate input file.

All compared numerical values matched exactly across Windows/Linux (maximum absolute and relative difference 0.0; allowed tolerances 1e-9). The one final holdout claim was created before generation at source commit `27d7a543479e1cdf0b47fa2a11115bdb1a0a934d`; the claim and receipt forbid replay. Holdout processed 144,000 unique scenario cohorts (432,000 target scalars) over the 120 assigned groups. Fit/calibration processed 79,200 unique cohorts per platform; repeated fit and decision passes are identified in the JSON resource records. No raw observations or per-cohort scores were persisted. All runtime limits were met; committed artifacts remain below 1 MiB.

## Holdout result

All 120 nonfault groups and every required class were independently eligible; there were no ineligible cohorts or numerical no-score windows. The 120 nonfault runs contain 14,400 eligible input cohorts, 3,480 warm-up cohorts, and 10,920 scored decisions. Raw eligible exposure was 144,000 seconds; scored eligible exposure was 109,200 seconds (30.33 hours), so availability was 75.83%.

| Gate | Observed | 95% group-bootstrap interval | Target | Result |
|---|---:|---:|---:|---|
| Nonfault point false-positive rate | 13/10,920 = 0.119% | 0–0.330% | ≤1% | PASS (point) |
| Nonfault false episodes | 2 episodes in 2/120 groups; 1.582 per 24 scored hours | 0–3.956 per 24 hours | ≤1 per 24 scored hours | **FAIL** |

The two nonfault episodes mean the all-zero Wilson group-occurrence interval rule does not apply. The episode-rate interval is wide; a passing point estimate alone would not establish a reliable rate guarantee.

| Required sustained class | Recall | Recall 95% CI | Timely ≤600 s | Timely 95% CI | Detected latency p50 / p95 / max (s) | Overall |
|---|---:|---:|---:|---:|---:|---|
| All targets +3 fit scales | 120/120 (100%) | 100–100% | 118/120 (98.33%) | 95.83–100% | 220 / 390 / 710 | **FAIL** — shared false-episode gate |
| Current +3 fit scales | 120/120 (100%) | 100–100% | 105/120 (87.50%) | 81.67–93.33% | 260 / 750 / 980 | **FAIL** — shared gate and timely target |
| Current/flow relationship flip | 120/120 (100%) | 100–100% | 120/120 (100%) | 100–100% | 260 / 380 / 580 | **FAIL** — shared false-episode gate |
| Residual dispersion ×2 | 3/120 (2.50%) | 0–5.83% | 2/120 (1.67%) | 0–4.17% | 470 / 780 / 780 | **FAIL** |
| Residual dispersion ×0.5 | 1/120 (0.83%) | 0–2.50% | 1/120 (0.83%) | 0–2.50% | 470 / 470 / 470 | **FAIL** |
| Mixed relationship + current +3 | 120/120 (100%) | 100–100% | 120/120 (100%) | 100–100% | 230 / 320 / 500 | **FAIL** — shared false-episode gate |

The method recognizes the declared relationship-flip and strong location-change cases in this generator, but it does not meet the common false-episode target; the current-only case misses the timely target, and both dispersion-only cases are largely undetected. Consequently **none of the six required classes passes the combined gate**. The +1/+2/ramp results remain sensitivity-only and do not change this disposition.

## Commit/provenance chain and next action

- Preregistration savepoints: `a16d372a`, `4c7abc33`, `87efd432`, `28ff01ff`, `8a9f841b` (method and execution-provenance clarifications all precede final fit/calibration v2 and holdout).
- Harness/UNIT tests: `3ea337ec`; Linux host-attestation implementation: `424142a9`.
- Matched Windows fit/calibration v2: `d57e32a2`; Linux fit/calibration v2 plus exact comparison: `af7009f6`.
- Frozen configuration commit: `27d7a543`; its parent is the exact frozen source/artifact commit `af7009f6`.
- Final immutable holdout receipt: `holdout-receipt-v1.json`; claim has `consumptionCount=1`, `replayPermitted=false`. The aggregate binds the same configuration and source commit.

**Recommendation to Main:** do not adopt or port this score to product. The finite study is complete and failed its combined target. Any different threshold, feature treatment, or method requires a new explicit Main decision and a separately preregistered study; no follow-on search is authorized here. Main owns review and exact-head T1. No manual CI, merge, or product change was performed by this executor.

Real 168 eligible normal hours/14 calendar days, field false-episode rates, physical synchronization/provenance, equipment health, diagnosis, RUL, standards, product integration, authorization/persistence, and deployment readiness remain `UNMEASURED / NOT PROVEN`. #588 remains PARTIAL and the product gate NOT READY.
