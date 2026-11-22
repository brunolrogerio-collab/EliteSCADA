# Stage 4F temporal-score study — final report v1

**Outcome: no joint candidate meets all preregistered synthetic point-estimate targets. No winner or product method is approved.** This is one finite synthetic study under #675, not product, field, equipment-health, diagnosis, or RUL evidence.

## Frozen design and provenance

- Study `W15-INTELLIGENCE-4F-TEMPORAL-STUDY-01`; experiment `W15-INTEL-4F-TEMPORAL-2026-10-20-01`.
- Exact base: `wave15/corrections-integration@ebe9b3abbbaffb8054b79273728f383e3bd17086`; product checkpoint unchanged.
- Preregistration savepoints: `d6c076652c012ba4e3593628f7bee4d92730117a`, then pre-fit provenance/image correction `cc63a1a04e7c38958f4a21b896157b3f3c5a70ad` and `6c5e8ae5c544f1baac7542c5a332cd8284c7e230`.
- Harness + 16 UNIT tests committed at `3a09a5d189784679b506fb64762ccdda4a92ac51`; Windows/Linux fit-calibration, exact comparison, and frozen configuration committed at `80173fb454af5098df95c53ec571696743f88192` before holdout.
- Fresh groups: fit 0–59; calibration 60–119; one-use Windows holdout 120–239. Holdout identity `W15-INTEL-4F-TEMPORAL-HOLDOUT-2026-10-20-01`; seed namespace `ELITESCADA-W15-4F-TEMPORAL-v1`.
- Both platforms passed all 16 UNIT tests. Fit/calibration cross-platform comparison: **PASS, 0 differences**, same selection and thresholds, within frozen tolerances.
- Calibration found no feasible joint candidate. The protocol-required fallback `joint-instant-q099` was frozen only as an **infeasible reference**, not a winner.
- Linux used isolated, network-disabled Python 3.14.6 image `python:3.14.6-slim-bookworm@sha256:4c92ffcde4dd6f1ff72a24518f49fd4990b27134987dfa31a733badde66df9f8`. The malformed image digest in the continuation notes was rejected before any container ran; the correction was committed before fit/calibration.
- The only reused code was the preregistered AST-allowlisted numerical helper subset and `find_episodes`, with their source digests verified. No old generator, data, or holdout was read or called.

## Final holdout

One Windows CPython 3.14.6 run completed, with the durable identity claim in `consumed-completed` state. It generated 100,800 synthetic vectors (302,400 scalar target values) across 840 scenario runs; it wrote no row-level traces. Resources: one worker, 48.09 CPU seconds, 51.72 wall seconds, 41,418,752-byte peak RSS. All are within the preregistered 900-second / 512-MiB limits.

| Joint candidate | Nonfault point FP | False episodes / 24 simulated eligible h (95% cluster bootstrap) | Strong-class recall current / multi / relationship | Timely ≤600 s current / multi / relationship | Preregistered outcome |
|---|---:|---:|---:|---:|---|
| Instant q0.99 | 1.0347% (0.8264–1.2639%) | 3.0 (0.6–6.0) | 100% / 100% / 90.83% | 100% / 100% / 71.67% | Fail: FP, false episodes, relationship timeliness |
| EWMA q0.99 | 1.0972% (0.7708–1.4861%) | 11.4 (6.6–16.8) | 99.17% / 99.17% / 98.33% | 99.17% / 99.17% / 87.50% | Fail: FP, false episodes, relationship timeliness |
| Instant q0.999 | 0.1597% (0.0903–0.2361%) | 0; Wilson group-occurrence 95% interval 0–3.10% | 100% / 100% / 59.17% | 100% / 100% / 38.33% | Fail: relationship recall and timeliness |
| EWMA q0.999 | 0.0486% (0.0069–0.1181%) | 0.6 (0–1.8) | 100% / 100% / 91.67% | 100% / 100% / 70.00% | Fail: relationship timeliness |

The EWMA result is a tradeoff, not a pass. Paired by source group against instantaneous at the same quantile, EWMA q0.99 improved relationship-class timely detection by 15.83 percentage points (95% paired cluster bootstrap 10.0–23.33 pp) while increasing nonfault false episodes by 8.4/24 h (4.2–13.2). EWMA q0.999 improved relationship timely detection by 31.67 pp (23.33–40.0) and lowered point FP by 0.111 pp, but relationship timely detection remained 70%, below 90%; its false-episode rate was 0.6/24 h (0–1.8). Marginal controls at both quantiles/forms and all remaining scenario metrics are reported in the aggregate; controls cannot be selected as a joint method.

For the frozen fallback `joint-instant-q099`, relationship recall was 90.83% (95% cluster bootstrap 85.0–95.83%), but timely detection was only 71.67% (63.33–79.17%). A 120-group finite synthetic sample does not establish real exposure or field performance; zero observed false episodes is not proof of a zero rate.

## Artifacts, bounds, and recommendation

- The machine-readable holdout aggregate contains every joint and marginal candidate, all seven scenarios, full denominators/reasons, per-class disposition, 2,000-replicate group-cluster bootstrap, paired comparisons, and runtime/resource counts.
- The one-use receipt binds the aggregate normalized SHA-256 `88B4F3ACA95795DF3039ED78E0EE39BC82EA265C18C2EB30B774A82F967992B7`, frozen configuration, protocol/manifest/source hashes and pre-holdout commit. Retry is forbidden.
- The complete new study directory is 444,590 bytes, below 1 MiB. No product or prior-study files were modified.
- Real ≥168 eligible operating hours / ≥14 days, actual device behavior, field false-alarm rates, production thresholds, and product integration remain **UNMEASURED / NOT PROVEN**.

**Finite recommendation to Main:** do not release any candidate as a product method from this study. Keep all four joint candidates as research-only evidence and leave any follow-on feature/model study to a separate explicit authorization; no follow-on is started here.
