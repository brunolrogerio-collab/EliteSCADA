# Stage 4F research environment

## Coordinator and repository state

- Assignment: #662.
- Accepted product checkpoint: `793b8303547491e02b98bf73732d3ab5ab76567a`.
- Inspected integration base before the order PR: `80a5b51bd81854a349d5f1bd77fb9c055c379040`.
- Documentation-only PR #663 merged at effective coordination base `e93c36453fbe731c164da095efc47e052d8d10dc` (parent `80a5b51bd81854a349d5f1bd77fb9c055c379040`, documentation commit `831dee83dbaa1361d650c7aec0f32bb57d2d18f7`). Its complete file list was `LAST CHANGE.md`, `docs/CURRENT-COORDINATOR-HANDOFF.md`, and `docs/WAVE15-MAIN-COORDINATOR-HANDOFF.md`; no `src`, `web`, or `tests` path changed. The assigned branch starts from the merged docs-only coordination tip; accepted product checkpoint remains unchanged.
- Branch: `work/w15-intelligence-stage4f-offline-method-study`.

## CPU runtimes

The numerical harness uses only the Python standard library and one process/thread; there are no third-party runtime dependencies, GPU, product dependency changes, network access, or AI service.

| Resource | Pinned runtime | CPU/OS context |
| --- | --- | --- |
| Windows local | CPython 3.14.6 | Windows 11 build 26200; Intel Core i7-13700HX; 32 GiB installed RAM |
| Linux local | pre-cached `python:3.12.15-slim-trixie`, image ID/digest `sha256:a6e34c598f2467ed0e9a8d349809fcd8b5c603269512df273a0bb1784edc11b1`, `linux/amd64` | Docker Desktop WSL2 Linux kernel `6.18.33.2-microsoft-standard-WSL2`; only available WSL distro is `docker-desktop`, which has no host `python3` |

The Linux run is an ephemeral `docker run --rm --network none` using the existing image with the two source archives mounted read-only; it creates no container/volume/network after completion and does not touch the stable Preview stack. Recheck image ID before execution; do not pull or mutate the image if unavailable. Measure elapsed wall time and process peak RSS in each runtime and report the differing interpreter versions. Use finite tests/tolerances instead of claiming bit-identical floating-point results across versions.

## Measurements to capture

- Exact Python build, OS/kernel and image digest.
- Single-run wall time for fit/calibration and holdout phases separately.
- Peak resident memory for each phase (Windows `GetProcessMemoryInfo`; Linux `getrusage`/`ru_maxrss`).
- Focused unit-test command/results for each OS.
- Raw archives are read directly from the pinned local ZIPs; no raw rows or record-level scores are written to Git.
