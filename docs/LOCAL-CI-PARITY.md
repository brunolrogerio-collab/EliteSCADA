# Local CI parity harness

This harness reproduces the universal CI dependency shape locally without making Docker state product state.

Run from the repository root in PowerShell:

```powershell
./scripts/ci/local-parity.ps1 up
./scripts/ci/local-parity.ps1 backend
./scripts/ci/local-parity.ps1 web
./scripts/ci/local-parity.ps1 e2e
./scripts/ci/local-parity.ps1 all
./scripts/ci/local-parity.ps1 reset
./scripts/ci/local-parity.ps1 down
```

`up` reuses the pinned `timescale/timescaledb:2.29.2-pg18` container on CI-compatible local port `5432`; override with `-DbPort` if a developer-owned service occupies it. `reset` is deliberately destructive only to this compose project's disposable volume, then recreates `postgres`, `elitescada_test`, and `elitescada_e2e` exactly for a clean pass. `all` always begins from that same clean volume. Logs and smoke payloads are retained under `ci/local/artifacts/` (ignored by Git).

The local harness uses `global.json` for the .NET 10.0.400 contract and checks the host-installed effective SDK through `versions`. On this Windows workstation the installed 10.0.401 SDK is a documented `latestFeature` roll-forward equivalent; GitHub Actions remains the independent exact-10.0.400 confirmation. Node must be 24.19.0. The runtime smoke first validates the anonymous demo Runtime, then starts an isolated secure first-run session to create the initial Administrator and authority-owned first project; it retains diagnostics under the local artifact directory.
