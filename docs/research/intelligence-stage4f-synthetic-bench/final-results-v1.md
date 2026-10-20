# Stage 4F synthetic motor/pump bench — final result v1

## Main review erratum — post-holdout hardening

**Main has not accepted the original mechanical demonstration or any PASS cell below.** The original 13/13 suite missed three demonstrated defects: three/four decisions could confirm an episode without a complete five-decision 3-of-5 window; a duplicate source-time pair left its first row eligible for scoring; and a caller-chosen scratch folder allowed a second reservation after interruption. Those original tests passed, but did not establish the prescribed mechanics.

The same PR now hardens those mechanics and adds small-fixture regressions: confirmation requires five consecutive eligible decisions; episode termination preserves the still-consecutive five-decision window, including trailing below-threshold decisions already present in the confirmation window; every row in a duplicate source-time key is ineligible; and an atomic per-user reservation keyed by holdout identity is independent of scratch and remains fail-closed after interruption, with the committed consumption receipt checked as well. The guard follows the stable per-user exclusive-claim pattern already used by the prior Stage 4F harness, in a separate synthetic-bench namespace. Current code validates historical source/artifact bindings at the receipt's frozen commit, then refuses replay when current bench/test hashes differ. A read-only verification confirmed the existing record against `35f187d9304ec1c31ed9859d07a4a812f13cfcab` and produced the expected current-source-mismatch refusal.

A further Main re-review caught one remaining episode-boundary defect on the corrected head: ending an episode must reset only the active/below-run state, not discard the rolling history of consecutive eligible decisions. The implementation now retains that window and counts below-threshold values already inside the confirming window toward five-below termination. Exact regressions produce `[4,12]` for `TTTTT FFFFF TTT` and `[4,10]` for `TTT FFFFF TTT`; an independent direct-window oracle agrees for all 16,383 Boolean sequences of lengths 0 through 13. This is mechanism hardening only, not new numerical-performance evidence.

This is **post-execution hardening only**: no fit/calibration, score recalculation, generator run, or holdout replay was performed. The old aggregate and receipt remain the historical output of the code frozen at `35f187d9304ec1c31ed9859d07a4a812f13cfcab`; their episode-dependent values are not accepted measurements of the now-corrected convention. The old aggregate's original candidate-level PASS labels are historical labels only, **not Main acceptance**. The original holdout identity remains consumed; a fresh experiment needs Main's separate predeclared protocol and identity.

Mechanism regressions now pass **18/18 on Windows and 18/18 on isolated Linux**. The following raw-file SHA-256 values were recorded before hardening and verified unchanged against the pre-hardening PR head `162637c40f81867a2ad172d9263691bfb61d5b7a`:

| Preserved evidence | Raw-file SHA-256 |
| --- | --- |
| `protocol-v1.md` | `436DA377FBDF45FB93064BC042096B088392768400A7128AF0F85AFB0445A385` |
| `protocol-v2.md` | `FABACE0AA305888E36BA5DBC0B4C04E1ABF3F5752428D087BD6A4D9BFA195288` |
| `protocol-v3.md` | `2457E2E685287E19E35CAA7CCBE88B01F02DE3374565EBC6C85CDB73555F3DE4` |
| `experiment-manifest-v1.json` | `E53F706815FA6B61B7ED1F3497BD75BBDA23C202D9F31E1AEED6B3AAEB29F019` |
| `frozen-config-v1.json` | `B79EE826D5C7DC02149C7B9FDAF3CC89C6F078262FA3554961E79656F56FDE70` |
| `fit-calibration-summary-v1.json` | `7323DE28AA7628C9AFC7DB2D52CF1C103BF79698B507BD320E4A9B12326BB24D` |
| `holdout-aggregate-v1.json` | `D8976874B61ADAEDFA3D870232BAC3964649D00C42C37116854C18CC3C6BB6E0` |
| `holdout-receipt-v1.json` | `75C78CE6D87C9136A3B686E195BC2E4118F4CAB685DAC279E4577237D55C62CF` |

## Disposition

**Historical original result:** bench mechanics were initially classified DEMONSTRATED; Main review overturned that classification pending the hardening above. Historical synthetic outputs remain FAIL / INSUFFICIENT, and product/field validity is NOT PROVEN. The covariance-aware score's reported original outputs remain unchanged and are not reinterpreted as results under the corrected episode convention. The one-use holdout remains consumed. Actual ≥168-hour / 14-calendar-day exposure remains UNMEASURED.

## Frozen execution record

- Repository: `brunolrogerio-collab/EliteSCADA`; assigned branch `work/w15-intelligence-stage4f-synthetic-bench`.
- Exact start base: `wave15/corrections-integration@26ecd07f06dfe48d286ce77704fa79ac4d92bd92`; accepted product checkpoint `793b8303547491e02b98bf73732d3ab5ab76567a`.
- Live integration later advanced to `b7869dd86cf00623c2b1a93e715fd667ac93ec10` through the coordination-only #672 merge, outside this folder. Per #671, this does not require rebasing the research branch; divergence is recorded.
- Protocol history before any fit: v1 + manifest commit `02ae483b6deb037fc8b9a9853d5aa9cc542740eb`; pre-fit relationship-unit clarification v2 `9b8f8e50`; pre-fit synthetic synchronization-gate clarification v3 `f0573a7f`. Active protocol: `protocol-v3.md`, SHA-256 `2457E2E685287E19E35CAA7CCBE88B01F02DE3374565EBC6C85CDB73555F3DE4`.
- Generator/evaluator/tests committed before fit/calibration: `641276dface25225f303e9609b9928f660f52c63`; Windows RSS collection correction and test: `f18a309ea9a77dd1a5a1483a627fcf75c662edaf`.
- Windows/Linux fit-calibration matched within the frozen tolerances and produced the same configuration hash: `138778C4FBDB29F43D70C19D1AC85A5831B76DCBE3822CC65D6546305CB0712C`. Configuration, calibration summary and fallback rationale were committed at `35f187d9304ec1c31ed9859d07a4a812f13cfcab` **before** the holdout.
- The only final holdout was `W15-INTEL-4F-SYNTH-BENCH-HOLDOUT-2026-10-20-01`, Windows / CPython 3.14.6, using 120 fresh whole-run source groups per strong class. Frozen head: `35f187d9304ec1c31ed9859d07a4a812f13cfcab`. Receipt state: `completed`; aggregate SHA-256: `2E6580D80A369BE4F2B763EF54E41192594D615604C53C409D7DD6A8B6B1A94A`.
- Holdout processed 100,800 vector samples (302,400 synthetic scalar target values); Windows CPU 14.00 s, wall 14.04 s, peak RSS 43,806,720 bytes. Fit/calibration processed 144,000 vectors per OS: Windows 7.86 s / 52,215,808 bytes RSS; Linux 7.30 s / 46,727,168 bytes RSS. All were within one-worker, 15-minute and 512-MiB limits.
- Focused mechanism tests: **13/13 passed on Windows and 13/13 on isolated Linux**. Cross-platform fit parameters and thresholds were identical in this execution; identities, counts, labels and partition digests matched exactly. No holdout was run on Linux.
- Only these pure numerical helpers were selectively compiled from the older harness: `median`, `fit_robust_scaling`, `transform_rows`, `covariance_matrix`, `jacobi_eigenvalues`, `rank_and_condition`, `shrink_covariance`, `cholesky`, `mahalanobis_squared`, `independent_max_abs`, and `nearest_rank_quantile`. No prior data reader, frozen configuration, output writer, or consumed-holdout guard was imported or used.

## Criterion matrix

| Area / unchanged criterion | Holdout evidence | Disposition |
| --- | --- | --- |
| Deterministic generation, blocked 360/120/120 grouping, fit-only contextual regression/scaling/covariance, finite shrinkage grid, frozen hashes, one-use guard, invalid-data gates and episode mechanics | Original 13/13 suite passed but missed Main-reviewed cases; corrected 18/18 fixture/oracle suite now passes on each platform | Original **DEMONSTRATED** label is overturned; corrected mechanics demonstrated for this research harness only |
| Point false positives ≤1% on known synthetic nonfault | Selected λ=0.05: 122/14,400 = 0.847%; source-block bootstrap 95% interval 0.688–1.014%. Independent control: 117/14,400 = 0.813%, interval 0.632–0.993% | λ=0.05 point estimate is below target, but uncertainty crosses 1%; conditional synthetic evidence only |
| False episodes ≤1 per 24 eligible operating hours | λ=0.05: 5 episodes / 40 simulated hours = 3.0 per 24 h; cluster bootstrap 0.6–6.0 per 24 h; Wilson group-occurrence upper bound 6.76 per 24 h | **FAIL** at point estimate; the formal 168-hour/14-day exposure floor is also **INSUFFICIENT / UNMEASURED** |
| Strong single-target shift, ≥90% recall and ≥90% detected within 10 minutes | Historical λ=0.05 output: 120/120 detected, 120/120 within 10 minutes; latency p95 50 s, maximum 90 s | Original candidate-level **PASS** label only; not Main acceptance or a result under corrected mechanics |
| Strong multi-target shift, ≥90% recall and ≥90% within 10 minutes | Historical λ=0.05 output: 120/120 detected and within 10 minutes; latency p95 40 s, maximum 60 s | Original candidate-level **PASS** label only; not Main acceptance or a result under corrected mechanics |
| Strong cross-signal relationship change, ≥90% recall and ≥90% within 10 minutes | λ=0.05: 104/120 detected (86.7%, block-bootstrap 80.0–92.5%); 89/120 within 10 minutes (74.2%, interval 65.8–81.7%); latency p95 820 s, maximum 1,060 s. Independent control: 1/120 detected (0.83%) | **FAIL**. Joint score adds information over the marginal control but misses both targets. Other joint shrinkage values also failed this class. |
| Smaller shifts and ramp | λ=0.05: 1-scale current shift 59/120 (49.2%) detected, 43/120 within 10 minutes; 2-scale 120/120 detected, 119/120 within 10 minutes; ramp 120/120 detected and within 10 minutes | Characterization only; no sensitivity guarantee or product claim |
| Real normal exposure, source provenance, product integration and field validity | No real plant/public archive used. The 40 simulated normal hours do not represent elapsed operating time or synchronized sensors | **NOT MEASURED / NOT PROVEN** |

The calibration fallback was frozen as λ=0.05 before holdout because it had the highest minimum recall over the three strong classes; the calibration screen had already shown that no candidate met every target. No candidate was substituted or retuned after holdout. All results depend on the stated generator and do not validate physical pump behavior, field false-alarm rates, fault diagnosis, RUL, runtime authorization, persistence, API/database behavior, or standards compliance.

## Execution exception and containment

The first Windows fit/calibration process completed its deterministic calculations but failed only at the terminal RSS query because `GetProcessMemoryInfo` lacked a typed Windows process-handle signature. It persisted no configuration or aggregate and no in-memory score/parameter was inspected. The signature was corrected and covered by a new test in `f18a309e`; the exact same seeds, equations, split and finite candidate grid were then rerun. The successful Windows/Linux configurations matched. This exception did not touch or consume the final holdout.

All generated traces and per-sample scores remained ephemeral. The external holdout scratch contains only the compact receipt. The committed aggregate report and receipt hashes were verified against each other and the frozen configuration. No product files, product services, Preview, HA, drivers, licensing, UCI archive/holdout, owner data or raw records were accessed or changed. No CI was manually dispatched and no PR was merged.

## Next action for Main

No new experiment is authorized by this correction return. Main decides whether the corrected mechanics and containment are acceptable, then may issue a separate finite, predeclared research action. Keep the product gate NOT READY; do not promote this synthetic evaluator or threshold into product behavior.
