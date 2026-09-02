# EliteSCADA — Temporary Browser Test Preview

**Status:** IMPLEMENTING / REAL CODESPACE VALIDATION PENDING  
**Tracking issue:** #208  
**Implementation branch:** `preview/codespaces-test-preview`  
**Coordination:** Preview is independent of Wave 13; Wave 13 #205 / PR #207 is released for separate parallel coordination.

## Purpose

Provide a temporary development/homologation environment where the real EliteSCADA can be started remotely and used from a browser without requiring a local product installation.

Target operator flow:

`Open Codespace -> devcontainer starts Preview automatically -> temporary authenticated Web URL -> use the real EliteSCADA`

The VS Code task **Launch Test Preview** remains available as a manual recovery/restart entry point, but normal Codespaces use must not depend on an interactive terminal.

This environment is not a production deployment, permanent public hosting model, SLA-backed service or supported customer deployment target.

## Implemented architecture

The Preview implementation uses GitHub Codespaces/devcontainers with repository-controlled automation for:

- the real EliteSCADA .NET backend;
- the real React/Vite frontend and pinned Pyodide static assets;
- TimescaleDB/PostgreSQL as a private Compose service;
- PostgreSQL-backed Engineering persistence and local identities;
- TimescaleDB historian mode;
- idempotent application initialization;
- automatic reconstruction and SHA-256 validation of the accepted Wave 11 Demo `.escadapkg`;
- normal package Import -> Save -> Publish -> Activate lifecycle;
- persisted Active Engineering as the source of HMI Runtime truth;
- automatic Preview launch through the devcontainer `postAttachCommand`;
- automatic Web-port forwarding/opening only after the application starts listening on `5173`;
- API port `5080` kept inside the app container and explicitly ignored for auto-forwarding;
- database port `5432` kept on the private Compose network.

The implementation does not create a parallel product host or Preview-only authorization bypass.

## Validated Demo fixture

The Preview preserves the exact accepted Wave 11 owner-test package bytes in Base64 form plus provenance metadata:

- artifact name: `EliteSCADA-Wave11-Demo`;
- source workflow run: `33552016447`;
- artifact ID: `9817878392`;
- project key: `e2e-wave11`;
- project name: `EliteSCADA Wave 11 Demo`;
- package size: `5394` bytes;
- SHA-256: `13261af59b8707df7d9ef3bbea307cb0c85d945ea8f47315fb693c92c885efa1`.

Repository files:

- `preview/fixtures/EliteSCADA-Wave11-Demo.json`;
- `preview/fixtures/EliteSCADA-Wave11-Demo.escadapkg.base64`.

`Launch Test Preview` reconstructs the `.escadapkg` only into ignored temporary state and fails closed if either its size or SHA-256 differs from the recorded provenance.

## Product behavior preserved

The Preview runs the actual product, not a mock implementation. The accepted authority remains:

`Working -> saved Revision -> Published -> Active -> HMI Runtime`

The bootstrap uses existing product endpoints and security rules. In particular:

- local authentication uses the existing `LocalIdentityConfiguration` / `LocalIdentityApi` implementation;
- identities are persisted with `PostgreSqlLocalIdentityStore`;
- the initial workspace already contains the real `developer` role with the currently defined capabilities, so package import does not require a Preview-only authorization exception;
- project packages use the existing `/api/project-package/...` endpoints;
- Engineering persistence uses the existing `/api/engineering/persistence/...` lifecycle;
- runtime activation uses the normal persisted Published -> Active path;
- absence of an installed product license intentionally produces the existing official `Demo` license state;
- the browser uses the existing `AuthGate` login UI and HttpOnly JWT cookie behavior.

Representative validation should cover the product surfaces available at the selected source SHA, including:

- local login and Administration;
- Engineering;
- Active HMI Runtime;
- simulated / Server Memory TAG behavior from the Demo fixture;
- alarms;
- trends;
- screens, popups, Dynamos and static assets;
- Python/Pyodide-dependent browser functionality;
- Demo/licensing behavior;
- temporary configuration and persistence expected inside the Codespace lifecycle.

## Administrative test account

The dedicated Preview administrative account is:

`EliteSCADA`

Its password is never stored in this public repository. The required Codespaces development secret is:

`ELITESCADA_PREVIEW_ADMIN_PASSWORD`

The devcontainer declares this as a recommended Codespaces secret. Preview startup fails clearly when it is absent; there is no repository-embedded fallback password.

On first startup, the password is supplied only to a short-lived API bootstrap process so the normal local identity store can create the account and hash the password. The API is then restarted without the bootstrap password or the original Codespaces secret variable in its long-lived process environment.

If an existing disposable Codespace database already contains the `EliteSCADA` identity with a different password, startup fails rather than silently replacing credentials. Rebuild/reset the temporary Codespace database in that situation.

## Exposure and security constraints

- Codespaces forwarded ports are private by default and require GitHub authentication; keep port `5173` private for normal Preview use.
- Only `5173` is intentionally forwarded by the devcontainer configuration.
- API `5080` is proxied by Vite inside the app container and is configured with `onAutoForward: ignore`.
- TimescaleDB/PostgreSQL is not published as a host/Codespaces port.
- The committed PostgreSQL password is explicitly a disposable container-network-only development credential, not a product or deployment secret.
- The requested Preview administrator password is not committed to source, workflow YAML, devcontainer configuration, logs, artifacts, Docker image layers or `.escadapkg` fixtures.
- The Preview creates an ephemeral JWT signing key for each launcher execution and does not persist it.
- No Authenticode or licensing private signing material is used by this environment.
- Backend authentication/authorization is not weakened for Preview convenience.
- No production security, durability, uptime or recoverability claim is made.
- Stopping/deleting the Codespace is an acceptable cleanup boundary.

## Operator procedure — automatic Codespaces Preview

1. Create a Codespace from the branch/revision being validated. For first-time setup, associate the recommended secret `ELITESCADA_PREVIEW_ADMIN_PASSWORD` with the repository.
2. Wait for the devcontainer `postCreateCommand` to restore .NET packages and install frontend dependencies.
3. When the VS Code Web client attaches, the devcontainer `postAttachCommand` automatically runs `scripts/preview/launch-test-preview.sh`. No interactive terminal step is required.
4. The launcher will:
   - wait for the private TimescaleDB service;
   - validate the preserved Wave 11 Demo package bytes;
   - start a short-lived identity bootstrap API;
   - restart the API without bootstrap credentials in its process environment;
   - authenticate the `EliteSCADA` account through `/api/auth/login`;
   - verify its `developer` role;
   - preserve an already-consistent Active Preview revision if one exists;
   - otherwise import the Demo package, Save, Publish and Activate it through the normal product APIs;
   - verify Active Runtime consistency;
   - verify official Demo licensing state;
   - start the Vite Web host on port `5173`.
5. Codespaces forwards port `5173` and opens the entry labeled **EliteSCADA Web — Test Preview** once the Web process is listening.
6. Sign in with username `EliteSCADA` and the protected Preview password.
7. If an operator intentionally needs to restart the Preview, **Tasks: Run Task -> Launch Test Preview** remains available as the explicit fallback.

A forwarded `5173` URL returning HTTP 502 means the Codespaces proxy exists but no Web process is listening. This is not an accepted ready state.

Temporary launcher state and logs live under ignored `.preview/` and are not repository artifacts.

## Automated validation

`.github/workflows/test-preview.yml` provides a specialized smoke complementary to the universal EliteSCADA CI. It:

- starts the same TimescaleDB version used by the devcontainer;
- validates the Compose file;
- verifies that the exact .NET SDK required by `global.json` exists in the Codespaces app image;
- verifies the automatic Codespaces launch contract (`postAttachCommand`, Web `5173`, internal API `5080`);
- restores the real backend/frontend dependencies;
- generates a fresh random CI-only administrator password at runtime;
- runs the exact `scripts/preview/launch-test-preview.sh` used by Codespaces;
- verifies the browser entry point and Pyodide static asset.

The workflow does not contain the Development Lead's Preview password and does not establish a fixed test-password fallback.

A successful Actions smoke is implementation evidence, but final acceptance still requires creating/rebuilding a real Codespace and opening its forwarded browser URL with the actual Web process running.

## Acceptance direction

From a known exact source SHA, this work may be considered ready when:

1. a fresh Codespace can be created using repository configuration;
2. required dependencies and TimescaleDB initialize without local-machine setup;
3. attaching the Codespaces Web client automatically launches the actual EliteSCADA backend and Web frontend without requiring an interactive terminal;
4. database/internal service ports remain private;
5. the `EliteSCADA` administrative test account authenticates using the protected injected password;
6. the validated Wave 11 Demo package is reconstructed and checksum-verified automatically;
7. the Demo application is imported and activated through the normal persisted Engineering lifecycle;
8. the provided temporary Web URL opens the actual EliteSCADA UI without HTTP 502;
9. representative Engineering, Runtime, TAG, alarm and trend behavior is usable from the browser;
10. Pyodide/static browser assets load correctly;
11. startup is repeatable automatically and through the explicit **Launch Test Preview** recovery task;
12. universal EliteSCADA CI and the specialized Test Preview smoke are green on the exact accepted SHA.

## Relationship to release waves

Wave 12 remains COMPLETE / ACCEPTED / CLOSED and is not reopened by this requirement.

Wave 13 #205 / PR #207 has been explicitly released by the Development Lead for separate parallel coordination. The Preview coordinator does not implement or coordinate Wave 13. Neither workstream may assume the other branch has reached `main`; each coordinator must re-audit live GitHub state before merge/release decisions.

## Explicit non-goals

- permanent or public EliteSCADA hosting;
- production deployment through GitHub Codespaces;
- production database persistence/SLA;
- replacing the Windows release/package track;
- reopening Wave 12 architecture without a demonstrated defect;
- unrelated HMI/Driver feature expansion;
- physical Driver L4 validation.
