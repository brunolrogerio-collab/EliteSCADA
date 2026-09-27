# First-project preview environment harness

This branch is infrastructure-only for the Wave 15 first-project preview. Its product checkpoint is `1f14a57491805a5d976bc9d0bf51393cf1b3ebcd`; the harness must not change product, identity, licensing, Authority, or project-seeding semantics.

## Local Environment A

The local audit runs in a Linux container based on the repository's pinned Playwright/.NET/Node image. The preview Compose project has its own TimescaleDB database and named volumes; it does not use `ci/local/docker-compose.yml`, expose PostgreSQL, or expose the API to the host. Only Web is bound to host loopback at `127.0.0.1:5173`.

The repository pins the preview shell scripts to LF so Windows Git checkouts remain executable by Bash inside Docker.

After Main posts an exact live acceptance SHA for Environment A, prepare the pinned tool image and dependency cache once, then run in PowerShell from the repository checkout:

```powershell
./scripts/preview/local-audit.ps1 prepare
./scripts/preview/local-audit.ps1 status
./scripts/preview/local-audit.ps1 start -AcceptedHarnessSha <accepted-harness-sha>
```

`prepare` resolves the locked npm and NuGet dependencies into external, hash-keyed Docker volumes. Their provenance is tied to the exact harness SHA/tree, product checkpoint, dependency manifests, and tool-image labels. The preparation manifest and volumes live outside the audit-session directory. `start` and `resume` require that exact provenance and use the already-built local image (`--no-build --pull never`); they do not install or restore packages. Missing or mismatched prepared state fails explicitly and requires `prepare` rather than silently downloading or reusing stale artifacts. A TLS-chain failure during preparation is reported as `ENVIRONMENT_PREP_BLOCKED_TLS`; certificate verification must not be weakened.

Prepared mode is enabled only by `docker-compose.preview.local.yml` for Environment A. Other Compose consumers retain the existing startup restore behavior unless their own reviewed configuration opts into prepared dependencies.

Open `http://localhost:5173` in a normal browser. The host binding remains loopback-only, and the `localhost` origin preserves the product's default Secure-cookie behavior. The harness enables the supported secure first-run local identity flow but supplies no bootstrap username/password, project key, package, fixture, or Demo state. JWT and query-cursor signing keys are generated randomly into the private persistent runtime volume and are not committed.

Use these commands to preserve/continue a session:

```powershell
./scripts/preview/local-audit.ps1 pause -Checkpoint "Last user-visible area and coarse completion state"
./scripts/preview/local-audit.ps1 status
./scripts/preview/local-audit.ps1 resume
```

`pause` stops the application and database containers without deleting the database volume, account/project state, evidence directory, or session provenance. `resume` requires the exact same accepted harness commit/tree and does not reset or seed product data. Detailed CODEX notes/screenshots/traces belong under `ci/local/artifacts/preview-audit-a/`; they are ignored local evidence, not a Product Owner status channel.

`reset` is the only destructive operation:

```powershell
./scripts/preview/local-audit.ps1 reset
```

It removes only containers, network, and product/audit volumes labelled with Compose project `elitescada-preview-a`; the provenance-bound dependency volumes and local tool image are preserved. It archives the local evidence folder instead of deleting it. Starting again always requires a fresh exact acceptance SHA and creates a new random session ID. If the manifest is absent but dedicated product/audit resources remain, `start` refuses to reuse them and requires explicit inspection/reset.

## Product Owner Environment B

The devcontainer uses the same Compose application/database definition, but Codespaces owns its separate VM, session, and Compose volumes. It forwards only port `5173`; `5080` and `5432` are marked ignore and are not published by Docker Compose. The Web container proxies same-origin API traffic internally.

On every Codespace start/reopen, `postStartCommand` runs `scripts/preview/codespaces-public-web-port.sh`. It derives the current identity from `CODESPACE_NAME`, uses the authenticated GitHub CLI to set only `5173:public`, then reads the live port list to verify `5173=public` and that `5080`/`5432` are not public. Missing authentication, organization policy, unavailable forwarding, or an unverified visibility result is a readiness failure; no secret or product-authentication bypass is used. Public forwarding makes the URL reachable without GitHub port authentication, so the product's own first-run local Administrator flow remains enabled and unchanged.

Environment B is not READY until the coordinator verifies a fresh Codespace, direct public browser access, successful recovery after stop/resume, and no public API/database port. A local Docker/Compose test cannot certify that remote Codespaces lifecycle behavior.

## Harness boundaries

- Do not start the black-box audit until Main accepts the exact harness commit, resets Environment A, and explicitly releases its gate.
- Do not pre-create the first Administrator/application or import the EEE package during environment setup.
- During the later first-project audit, do not use source, controls, database inspection, direct internal API calls, fixtures, or implementation logs to choose the user route.
- Keep detailed CODEX findings local and embargoed until the independent Product Owner journey is complete.
