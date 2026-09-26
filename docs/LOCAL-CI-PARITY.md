# Local CI parity harness

This harness reproduces the universal CI dependency shape locally without making Docker state product state.

Run from the repository root in PowerShell:

```powershell
./scripts/ci/local-parity.ps1 up
./scripts/ci/local-parity.ps1 backend
./scripts/ci/local-parity.ps1 web
./scripts/ci/local-parity.ps1 e2e
./scripts/ci/local-parity.ps1 all
./scripts/ci/local-parity.ps1 linux-e2e
./scripts/ci/local-parity.ps1 linux-all
./scripts/ci/local-parity.ps1 reset
./scripts/ci/local-parity.ps1 down
```

`up` reuses the pinned `timescale/timescaledb:2.29.2-pg18` container on CI-compatible local port `5432`; override with `-DbPort` if a developer-owned service occupies it. `reset` is deliberately destructive only to this compose project's disposable volume, then recreates `postgres`, `elitescada_test`, and `elitescada_e2e` exactly for a clean pass. `all` always begins from that same clean volume. Logs and smoke payloads are retained under `ci/local/artifacts/` (ignored by Git).

`linux-e2e` resets only disposable databases and then runs the dependent local-auth and Chromium projects in a repository-controlled Ubuntu runner. `linux-all` performs the complete clean battery in that same runner: backend build/tests, Runtime smoke, web build and Chromium. Chromium is explicitly switched back to its clean `elitescada_e2e` database after Runtime smoke, so smoke state cannot affect the first-run E2E fixture. It pins Playwright 1.62.1, Node 24.19.0 and .NET 10.0.400, while sharing the same TimescaleDB service. Named npm/NuGet caches are reused, but the Windows `node_modules` tree is never used by the Linux runner. Failure traces, screenshots, videos and the HTML report remain under `web/scada-web/test-results` and `web/scada-web/playwright-report`.

For a Wave 15 pull request, declare the applicable `VALIDATION_PROFILE: <profile[,profile]>` line in its description so the PR router can select the focused checks. The full EliteSCADA CI is a separate independent gate after local parity.

The local harness uses `global.json` for the .NET 10.0.400 contract and checks the host-installed effective SDK through `versions`. On this Windows workstation the installed 10.0.401 SDK is a documented `latestFeature` roll-forward equivalent; GitHub Actions remains the independent exact-10.0.400 confirmation. Node must be 24.19.0. The runtime smoke first validates the anonymous demo Runtime, then starts an isolated secure first-run session to create the initial Administrator and authority-owned first project; it retains diagnostics under the local artifact directory.
