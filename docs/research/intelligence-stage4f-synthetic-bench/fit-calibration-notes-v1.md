# Stage 4F synthetic bench — fit/calibration savepoint

**State:** fit/calibration complete on Windows and isolated Linux; frozen configuration is ready to commit. The one-use final holdout has **not** been claimed, generated, or scored. This is synthetic-only method evidence, not a product-method selection or field result.

## Frozen inputs and execution

- Exact research base: `wave15/corrections-integration@26ecd07f06dfe48d286ce77704fa79ac4d92bd92`; accepted product checkpoint: `793b8303547491e02b98bf73732d3ab5ab76567a`.
- Branch: `work/w15-intelligence-stage4f-synthetic-bench`.
- Active pre-fit contract: `protocol-v3.md`, SHA-256 `2457E2E685287E19E35CAA7CCBE88B01F02DE3374565EBC6C85CDB73555F3DE4`.
- Experiment manifest: `experiment-manifest-v1.json`; 600 whole-run groups split 360/120/120; each calibration event class has 120 distinct source groups; each nonfault calibration run is 20 simulated minutes.
- Fit/calibration work per platform: 43,200 fit baseline vectors and 100,800 calibration vectors; no per-sample trace or score was written.
- Runtime: Windows 11 x64 / CPython 3.14.6; isolated Linux/amd64 CPython 3.14.6 from `python:3.14.6-slim-bookworm@sha256:ff83a535339812dd72e69c93b3c48ddf7c85a324d6330af5797c82a255dbeef4`.
- Mechanism suite after the resource-collector fix: **13/13 passed on both platforms**. The same configuration digest was produced on both: `138778C4FBDB29F43D70C19D1AC85A5831B76DCBE3822CC65D6546305CB0712C` (also stored as `configurationSha256` in the frozen configuration).
- Resource measurements: Windows 7.86 s wall / 7.58 s CPU / 52,215,808-byte peak RSS; Linux 7.30 s wall / 7.28 s CPU / 46,727,168-byte peak RSS. Both are below the predeclared 15-minute and 512-MiB ceilings.
- Reused numerical helpers, compiled selectively from the prior harness source without importing its archive readers or holdout guards: `median`, `fit_robust_scaling`, `transform_rows`, `covariance_matrix`, `jacobi_eigenvalues`, `rank_and_condition`, `shrink_covariance`, `cholesky`, `mahalanobis_squared`, `independent_max_abs`, and `nearest_rank_quantile`.

## Calibration-only comparison

Every candidate used the fit-only transform and a calibration-only nearest-rank 99th-percentile threshold. Point FP rate is 144/14,400 = 1.00% for each by construction of the calibration quantile; it is not an independent demonstrated operating rate. The 120 normal calibration groups contain 40 simulated eligible hours.

| Candidate | Threshold | False episodes / 40 simulated h | Per 24 simulated h | Single strong recall | Multi strong recall | Relationship recall | Relationship within 10 min |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Independent control | 2.89894 | 3 | 1.80 | 100.0% | 100.0% | 5.0% | 1.7% |
| Joint λ=0.05 | 11.03309 | 5 | 3.00 | 100.0% | 100.0% | 79.2% | 58.3% |
| Joint λ=0.10 | 10.92626 | 5 | 3.00 | 100.0% | 100.0% | 77.5% | 54.2% |
| Joint λ=0.25 | 10.59870 | 3 | 1.80 | 100.0% | 100.0% | 65.8% | 39.2% |
| Joint λ=0.50 | 10.77444 | 2 | 1.20 | 100.0% | 100.0% | 38.3% | 15.8% |

The deterministic calibration rule selects joint λ=0.05 only as the predeclared fallback: it has the highest minimum recall across the three strong classes, but **no candidate met every calibration target**. For λ=0.05, the relationship class is below the 90% recall and 90%-within-10-minute targets, and the observed 3.00 false episodes/24 simulated hours exceeds the numeric target of 1.00. λ=0.50 also exceeds that numeric false-episode target. The formal ≥168-hour/14-day exposure criterion is **INSUFFICIENT / UNMEASURED** because these are only 40 simulated hours; simulated time does not satisfy real operating exposure. The joint score improved synthetic relationship recall over the independent control (79.2% versus 5.0%) but did not reach the target. Smaller-shift and ramp characterization remains in the aggregate calibration report; it is not an acceptance claim.

**Freeze decision:** preserve λ=0.05 and all calibration thresholds exactly as computed. Do not tune or substitute another candidate after observing holdout. The holdout comparison will report the same finite grid and the independent control, with no selection update.

## Execution note

The first Windows fit/calibration process executed the fixed deterministic calculation but failed at the final RSS collection because the Windows `GetProcessMemoryInfo` call lacked a correctly declared native handle signature. It wrote no summary or frozen configuration, and no in-memory candidate/parameter output was inspected. The collector was corrected and a focused measurement test added in commit `f18a309ea9a77dd1a5a1483a627fcf75c662edaf`; the full Windows fit/calibration was then rerun with the identical protocol, groups, seeds, equations, and candidate grid. Linux used that same committed source. No holdout identity has been reserved or consumed.
