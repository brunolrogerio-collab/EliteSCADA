# First-project preview environment harness

This branch is infrastructure-only for the Wave 15 first-project preview. Its product checkpoint is `1f14a57491805a5d976bc9d0bf51393cf1b3ebcd`; the harness must not change product, identity, licensing, Authority, or project-seeding semantics.

See [LOCAL-ELITESCADA-OPERATIONS-EVIDENCE.md](LOCAL-ELITESCADA-OPERATIONS-EVIDENCE.md) for the evidence labels, actual local lifecycle results, regressions, and remaining questions. Local ENV_A evidence is not a substitute for Codespaces or Product Owner Preview acceptance.

## Local Environment A

### Visual Studio local development profile

For ordinary local use, launch the separate developer workbench with:

```powershell
./scripts/preview/elite-local.ps1 launch
```

This one command checks the `development` profile, prepares pinned dependencies if the exact Git-blob provenance is not ready, starts or resumes its own Compose project/database/session/evidence, waits for DB/API/Web health, and prints the local URL. The profile uses `elitescada-preview-dev` and `ci/local/artifacts/preview-dev/`; it is not Main-accepted ENV_A evidence. Product source edits may remain uncommitted, while dependency-input changes must be committed before preparation. The separate development and audit profiles share host port `5173`; the operator refuses to start either while the other has running containers.

Stop local development without deleting data:

```powershell
./scripts/preview/elite-local.ps1 stop -Profile development
```

Resume with `launch`, or use `restart -Profile development` to recreate the app/database containers on the same volumes. Destructive reset is separate and must only follow an explicit fresh-install request: `reset -Profile development -Force`.

The official audit profile below remains gated by Main's exact acceptance SHA. Do not treat a development-profile launch as audit readiness or Product Owner Preview evidence.

The local audit runs in a Linux container based on the repository's pinned Playwright/.NET/Node image. The preview Compose project has its own TimescaleDB database and named volumes; it does not use `ci/local/docker-compose.yml`, expose PostgreSQL, or expose the API to the host. Only Web is bound to host loopback at `127.0.0.1:5173`.

The repository pins the preview shell scripts to LF so Windows Git checkouts remain executable by Bash inside Docker.

After Main posts an exact live acceptance SHA for Environment A, prepare the pinned tool image and dependency cache once, then use the repository operator in PowerShell from the repository checkout:

```powershell
./scripts/preview/elite-local.ps1 prepare
./scripts/preview/elite-local.ps1 status
./scripts/preview/elite-local.ps1 start -AcceptedHarnessSha <accepted-harness-sha>
```

`prepare` resolves the locked npm and NuGet dependencies into external, hash-keyed Docker volumes. Their provenance is tied to the exact harness SHA/tree, product checkpoint, dependency manifests, and tool-image labels. Dependency inputs are identified from committed Git blob IDs, then aggregated with SHA-256; this keeps the key stable when Windows checkouts differ only in LF/CRLF working-tree bytes while still changing it when a committed dependency input changes. The preparation bootstrap also normalizes embedded shell text to LF before invoking Bash, so Windows checkout line endings cannot alter shell options. The scripts/preview/test-dependency-identity.ps1 regression test exercises both cases using disposable Git repositories. The preparation manifest and volumes live outside the audit-session directory. `start` and `resume` require that exact provenance and use the already-built local image (`--no-build --pull never`); they do not install or restore packages. Missing or mismatched prepared state fails explicitly and requires `prepare` rather than silently downloading or reusing stale artifacts. A TLS-chain failure during preparation is reported as `ENVIRONMENT_PREP_BLOCKED_TLS`; certificate verification must not be weakened.

Dependency input paths use one ordinal, case-insensitive order in Windows PowerShell 5.1 and PowerShell 7. Both hosts derive the same provenance key for the same committed Git tree, so the local operator does not require a separate PowerShell 7 installation.

On the development profile's `launch` path only, a failing npm TLS chain may trigger one retry after the host validates `registry.npmjs.org` through its normal Windows trust store. The matching root is mounted read-only only into the preparation container, removed afterward, and recorded by thumbprint/hash. No certificate is imported and TLS verification remains enabled. If the Windows chain cannot be validated, launch stops with a TLS blocker.

If a local HTTPS-inspection product substitutes certificates, an operator may explicitly select a root **already trusted by Windows** for this one preparation container: `prepare -TrustedRootThumbprint <40-hex-thumbprint>`. The script refuses a missing or non-root certificate, mounts only its public PEM read-only during `npm ci`/`dotnet restore`, keeps TLS verification enabled, deletes the temporary PEM, and records the root thumbprint and SHA-256 in the local preparation manifest. It does not change the Windows/Docker trust stores or carry that root into product `start`/`resume`. Do not use this option with an unknown or untrusted certificate.

Prepared mode is enabled only by `docker-compose.preview.local.yml` for Environment A. Other Compose consumers retain the existing startup restore behavior unless their own reviewed configuration opts into prepared dependencies.

Open `http://localhost:5173` in a normal browser. The host binding remains loopback-only, and the `localhost` origin preserves the product's default Secure-cookie behavior. The harness enables the supported secure first-run local identity flow but supplies no bootstrap username/password, project key, package, fixture, or Demo state. JWT and query-cursor signing keys are generated randomly into the private persistent runtime volume and are not committed.

Use these commands to preserve/continue a session:

```powershell
./scripts/preview/elite-local.ps1 pause -Checkpoint "Last user-visible area and coarse completion state"
./scripts/preview/elite-local.ps1 status
./scripts/preview/elite-local.ps1 resume
```

`pause` stops the application and database containers without deleting the database volume, account/project state, evidence directory, or session provenance. `resume` requires the exact same accepted harness commit/tree and does not reset or seed product data. Detailed CODEX notes/screenshots/traces belong under `ci/local/artifacts/preview-audit-a/`; they are ignored local evidence, not a Product Owner status channel.

`stop` is a friendly alias for `pause`. `restart` recreates the application and database containers against the same named product volumes and verifies Web/API health before returning. `status` reports the current Git branch/HEAD/tree, whether the checkout differs from the saved session, dependency preparation identity, Docker containers, TimescaleDB/API/Web health, resumability, and local URLs. `diagnose` writes a local report with recent service logs after common secret/token patterns are redacted; review it before sharing.

Run the consolidated local regression suite from the repository root. It checks PowerShell syntax and command contracts, the exact PR #362 scope allowlist (and rejection of product paths), reset guardrails, development-profile isolation, Docker inspect JSON parsing, diagnostic redaction, and dependency provenance/line-ending/path-order stability. It does not start or reset the product environment. Run it in both Windows PowerShell 5.1 (Visual Studio's default) and PowerShell 7 when available:

```powershell
./scripts/preview/test-local.ps1
```

`reset` is the only destructive operation:

```powershell
./scripts/preview/elite-local.ps1 reset -Force
```

`-Force` is mandatory and must only be supplied after the Product Owner explicitly requests a fresh install. Reset removes only containers, network, and product/audit volumes labelled with Compose project `elitescada-preview-a`; the provenance-bound dependency volumes and local tool image are preserved. It archives the local evidence folder instead of deleting it. Starting again always requires a fresh exact acceptance SHA and creates a new random session ID. If the manifest is absent but dedicated product/audit resources remain, `start` refuses to reuse them and requires explicit inspection/reset.

## Product Owner Environment B

The devcontainer uses the same Compose application/database definition, but Codespaces owns its separate VM, session, and Compose volumes. It forwards only port `5173`; `5080` and `5432` are marked ignore and are not published by Docker Compose. The Web container proxies same-origin API traffic internally.

On every Codespace start/reopen, `postStartCommand` runs `scripts/preview/codespaces-public-web-port.sh`. It derives the current identity from `CODESPACE_NAME`, uses the authenticated GitHub CLI to set only `5173:public`, then reads the live port list to verify `5173=public` and that `5080`/`5432` are not public. Missing authentication, organization policy, unavailable forwarding, or an unverified visibility result is a readiness failure; no secret or product-authentication bypass is used. Public forwarding makes the URL reachable without GitHub port authentication, so the product's own first-run local Administrator flow remains enabled and unchanged.

Environment B is not READY until the coordinator verifies a fresh Codespace, direct public browser access, successful recovery after stop/resume, and no public API/database port. A local Docker/Compose test cannot certify that remote Codespaces lifecycle behavior.

## Harness boundaries

- Do not start the black-box audit until Main accepts the exact harness commit, resets Environment A, and explicitly releases its gate.
- Do not pre-create the first Administrator/application or import the EEE package during environment setup.
- During the later first-project audit, do not use source, controls, database inspection, direct internal API calls, fixtures, or implementation logs to choose the user route.
- Keep detailed CODEX findings local and embargoed until the independent Product Owner journey is complete.
