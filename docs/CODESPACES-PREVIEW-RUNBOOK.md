# EliteSCADA — Codespaces Preview Operational Runbook

**Purpose:** reproducible operating procedure for creating, rebuilding, diagnosing and accepting the temporary EliteSCADA browser Preview in GitHub Codespaces.  
**Primary tracking:** issue #208 / PR #210 while that work remains open.  
**Concept and architecture:** `docs/TEMPORARY-BROWSER-TEST-PREVIEW.md`.

This document is intentionally operational. It records the failures and successful recovery patterns discovered during real Codespace homologation so that a future coordinator does not need the previous ChatGPT conversation to reproduce the environment.

## 1. Operating principle

The GitHub repository is the source of truth. Before creating or rebuilding a Codespace, confirm the live issue/PR, current branch, exact head SHA and CI state in GitHub.

A Codespace is disposable homologation infrastructure. Do not preserve a broken Codespace at the cost of changing product security, licensing, authentication or runtime architecture.

The Preview must run the real EliteSCADA lifecycle:

`Working -> saved Revision -> Published -> Active -> HMI Runtime`

There is no Preview-only authorization or licensing bypass.

## 2. Golden path — create a fresh Preview

### 2.1 Before creating the Codespace

1. Open issue #208 and the active Preview PR and confirm the branch/revision currently under validation.
2. Record the exact expected head SHA.
3. Confirm the exact-head validation required by the change is green or intentionally still under investigation. At minimum, use the universal `EliteSCADA CI` and the specialized `Test Preview` workflow. If a Web/product change also triggers a dedicated HMI workflow, treat it as an additional gate.
4. Confirm the repository Codespaces secret exists:

   `ELITESCADA_PREVIEW_ADMIN_PASSWORD`

5. The protected Preview password must satisfy the product's existing Local Identity password policy. The real homologation exposed the current minimum of 12 characters. Do not weaken that policy for Preview convenience.
6. Never place the password in a GitHub comment, commit, workflow, command transcript or diagnostic output.

### 2.2 Create the Codespace

1. Create the Codespace from the exact Preview branch/revision being validated, not casually from `main`.
2. Use the repository devcontainer configuration named **EliteSCADA Test Preview**.
3. Associate the recommended `ELITESCADA_PREVIEW_ADMIN_PASSWORD` Codespaces secret with the repository.
4. Wait for the container creation and `postCreateCommand` to finish.
5. When VS Code Web attaches, `postAttachCommand` automatically runs:

   `bash scripts/preview/launch-test-preview.sh`

6. Normal operation must not require an interactive terminal command after creation.
7. Wait for port `5173`, labeled **EliteSCADA Web — Test Preview**, to be forwarded.
8. Keep port `5173` **Private** for normal use. API `5080` and database `5432` are internal and must not be exposed.
9. Open the forwarded `5173` URL and sign in through the actual EliteSCADA login UI with username:

   `EliteSCADA`

10. A successful login is only the start of homologation. Continue through the acceptance checklist in section 8.

## 3. What the automatic launcher must do

The repository launcher is the canonical Preview startup mechanism. It must, without hand-edited container state:

1. verify required commands;
2. require the protected Codespaces administrator secret;
3. reach the private `timescaledb:5432` service;
4. reconstruct the accepted Wave 11 Demo `.escadapkg` only under ignored `.preview/` state;
5. validate the package size and SHA-256;
6. start a short-lived API bootstrap with the protected password;
7. persist the Local Identity through the normal product implementation;
8. restart the long-lived API without the bootstrap password in its process environment;
9. authenticate the `EliteSCADA` account through `/api/auth/login`;
10. verify the `developer` role;
11. preserve a consistent already-Active Preview revision when possible, otherwise Import -> Save -> Publish -> Activate the Demo through normal product APIs;
12. verify Active Runtime consistency;
13. verify the official Demo license state when no license is installed;
14. start Vite on `0.0.0.0:5173` with the API remaining internal on `127.0.0.1:5080`.

If a real Codespace requires a manual workaround that is not represented in repository configuration or the launcher, the environment is not yet reproducible. Convert the workaround into repository-controlled automation before acceptance.

## 4. Decide the recovery level before rebuilding

Do not automatically rebuild the entire Codespace for every change. Use the smallest recovery level that actually matches the change.

### Level A — browser reload / no process restart

Use when:

- only a frontend file changed and the running Vite dev server successfully hot-reloads it;
- the Web process and API remain healthy;
- no dependency, devcontainer, launcher, environment or backend binary changed.

Typical action: update the branch, then reload the browser and verify the actual screen.

### Level B — restart the Preview launcher

Use when:

- backend product source changed;
- `scripts/preview/launch-test-preview.sh` changed;
- Preview fixture or launcher-managed configuration changed;
- the API/Web process died while the container itself remains valid;
- frontend HMR did not pick up a source change cleanly.

Preferred manual recovery entry point:

**Tasks -> Run Task -> Launch Test Preview**

Equivalent command when an interactive shell is usable:

```bash
bash scripts/preview/launch-test-preview.sh
```

A Level B restart must not be used to conceal a devcontainer/configuration defect.

### Level C — Rebuild Container

Use when the environment definition itself changed, including:

- `.devcontainer/devcontainer.json`;
- `.devcontainer/docker-compose.yml`;
- `.devcontainer/initialize-preview-machine-id.sh`;
- devcontainer base image or Features;
- container mounts such as `/etc/machine-id`;
- `postCreateCommand` or `postAttachCommand` semantics;
- exact SDK compatibility with `global.json`;
- Node/.NET/container dependencies that must exist before the launcher starts.

After a Level C rebuild, verify the exact SHA again. Rebuilds are allowed to change the running environment while the branch may also be moving, which is precisely how humans end up testing the wrong thing with great confidence.

### Level D — create a new Codespace

Prefer a fresh Codespace when:

- the disposable database contains a Local Identity created with a different Preview password;
- stale container/database state makes it unclear whether the current result is reproducible;
- a prior devcontainer generation had fundamentally different mounts/images/services;
- repeated rebuilds produce ambiguous state;
- final acceptance needs proof that a clean environment starts from repository configuration alone.

A new Codespace is also the safest final confirmation after infrastructure changes.

## 5. Proven failure modes and what they mean

### 5.1 Required .NET SDK not found

Observed symptom:

`Nenhuma versão obrigatória de um SDK do .NET foi encontrada`

Meaning: the devcontainer image did not satisfy the exact SDK required by `global.json`.

Correct response:

- fix the devcontainer image/SDK contract in the repository;
- validate the exact SDK in CI;
- perform a Level C rebuild or create a fresh Codespace.

Do not relax `global.json` merely to make the Preview start unless the product itself intentionally changes SDK policy.

### 5.2 Licensing fails before API readiness because machine identity is absent

Observed cause: the app container did not have a usable `/etc/machine-id`, so the existing `DefaultMachineIdentityProvider` correctly failed closed.

Correct response:

- provide a disposable per-Codespace machine identity as environment state;
- mount it through the devcontainer/Compose configuration;
- keep the existing product licensing implementation unchanged;
- validate the mount in CI;
- perform a Level C rebuild or create a fresh Codespace.

Do not add a Preview-only licensing bypass.

### 5.3 Local Identity bootstrap fails because the protected password violates policy

Observed cause: a Preview password shorter than the existing product minimum caused bootstrap to fail closed.

Correct response:

- correct the protected Codespaces secret;
- do not weaken the product password rule;
- restart/rebuild the environment so the new secret is injected;
- if the disposable database already contains the `EliteSCADA` identity under another password, create/reset disposable state, preferably with a fresh Codespace.

### 5.4 Forwarded `5173` returns HTTP 502

Meaning: Codespaces has created the forwarding proxy, but no Web process is listening behind it. **502 is not a ready Preview.**

First checks:

```bash
git rev-parse HEAD
dotnet --version
docker compose -f .devcontainer/docker-compose.yml ps
echo "===== API LOG ====="
tail -n 160 .preview/api.log
echo "===== WEB LOG ====="
tail -n 160 .preview/web.log
```

Also check from inside the container:

```bash
curl -I http://127.0.0.1:5173/
curl -I http://127.0.0.1:5080/health
```

Interpret the first real exception in the logs. Do not patch the product from the last stack-frame line alone.

### 5.5 Integrated terminal appears blank or unusable

This was observed in real Codespaces. The browser terminal UI itself may be the problem while the container remains usable.

Fallback used successfully during homologation: ask the Codespace's built-in AI/agent to execute read-only diagnostic commands and return their complete output.

When using this fallback:

- explicitly instruct it not to alter files unless that is the intended operation;
- never ask it to print the protected password or secret environment variable;
- prefer exact commands such as `git rev-parse HEAD`, `dotnet --version`, `docker compose ... ps`, and `tail` of `.preview` logs.

Do not accept “the terminal was broken” as proof that the application was broken.

### 5.6 UI opens, but content is visually unusable

A real Codespace exposed a pre-existing Script Engineering contrast defect: dark Engineering text tokens were combined with light fallback surfaces. The application was technically running, but homologation correctly rejected the screen as unusable.

Rule: browser availability is not product usability. Treat contrast, invisible controls, broken layouts and unreadable content as real defects discovered by Preview testing.

If the same faulty code exists in `main`, document it as a pre-existing product defect rather than blaming Codespaces.

## 6. Updating an existing Codespace to a newer Preview head

Before updating, record the currently running SHA:

```bash
git rev-parse HEAD
```

Then update without destroying uncommitted work:

```bash
git status --short
git fetch origin
git checkout preview/codespaces-test-preview
git pull --ff-only
```

If `git status --short` shows unexpected local changes, stop and diagnose them. Do not use `git reset --hard` as a routine update mechanism.

After the update:

1. run `git rev-parse HEAD` again and compare it with the exact SHA intended for homologation;
2. choose Level A, B, C or D from section 4;
3. wait for startup before opening the forwarded URL;
4. do not claim validation for a SHA different from the one actually running.

If the active Preview branch changes in the future, use the branch recorded in the live issue/PR instead of blindly copying the branch name from this document.

## 7. Security and exposure checklist during troubleshooting

- Never commit or print `ELITESCADA_PREVIEW_ADMIN_PASSWORD`.
- Do not echo the protected secret for diagnostics.
- Keep `5173` Private for normal operation.
- If port visibility is temporarily changed to Public for troubleshooting, return it to Private before acceptance.
- Do not intentionally forward API `5080`.
- Do not publish database `5432`.
- Do not put credentials in `.escadapkg`, workflow artifacts or logs.
- Do not weaken backend authentication/authorization, password policy or licensing fail-closed behavior to fix Preview startup.
- Generated machine identity is disposable environment state, not a license or signing secret.

## 8. Real browser acceptance checklist

For the exact source SHA under acceptance, record evidence that:

1. the Codespace was created/rebuilt from repository configuration;
2. `git rev-parse HEAD` matches the intended SHA;
3. `dotnet --version` matches the repository SDK contract;
4. TimescaleDB is healthy on the private Compose network;
5. automatic `postAttachCommand` startup succeeds without a required manual terminal step;
6. port `5173` opens the actual EliteSCADA UI without HTTP 502;
7. port `5173` is Private for the accepted state;
8. login with `EliteSCADA` succeeds through the real login UI;
9. Engineering is readable and usable, including the screens touched by the current change;
10. Active HMI Runtime opens from the real persisted Active revision;
11. representative Demo TAG behavior is visible/usable;
12. alarm behavior is usable;
13. trend/historian behavior is usable;
14. representative screens, popups and Dynamos render;
15. Python/Pyodide-dependent browser functionality loads when present in the selected baseline;
16. licensing reports the expected official Demo state when no product license is installed;
17. a launcher restart is repeatable;
18. exact-head `Test Preview` is green;
19. exact-head universal `EliteSCADA CI` is green;
20. any other workflow required by the changed product surface is green.

Do not mark #208 accepted from CI alone. Real Codespace/browser use is part of the acceptance criterion.

## 9. Rebuild loop for active development

When a real Codespace exposes a defect, use this loop:

1. capture the exact running SHA;
2. capture the smallest useful diagnostic evidence;
3. classify the failure as product, launcher, devcontainer, secret/state, browser forwarding or Codespaces UI;
4. fix the narrowest responsible repository layer;
5. add regression coverage when practical;
6. push the new Preview head;
7. wait for the exact-head required CI gates;
8. update the Codespace;
9. choose Level A/B/C/D deliberately;
10. reproduce the original failure in the real browser;
11. continue the acceptance checklist from the point where it stopped.

Multiple rebuilds during implementation are acceptable. What is not acceptable is depending on undocumented manual container surgery that a future coordinator cannot reproduce.

## 10. Coordinator handoff evidence

When handing this work to another coordinator or another chat, the GitHub record should be enough to continue. Record in issue #208 / PR #210 or their successors:

- current Preview branch;
- exact head SHA;
- exact workflow run IDs and conclusions;
- whether a real Codespace was created or rebuilt from that SHA;
- whether `5173` opened successfully;
- whether login succeeded;
- last product surface validated;
- next surface/check still pending;
- any currently known failure and the diagnostic evidence already collected;
- whether the accepted `5173` port state is Private.

A new coordinator should begin by reading, in order:

1. `PROJECT GOAL.md`;
2. `LAST CHANGE.md`;
3. `docs/CURRENT-COORDINATOR-HANDOFF.md`;
4. `docs/ROADMAP.md`;
5. issue #208 and the active Preview PR;
6. `docs/TEMPORARY-BROWSER-TEST-PREVIEW.md`;
7. this runbook.

Then re-check live GitHub state before changing code. Conversation history is supplementary, not authoritative.

## 11. Current repository-controlled Preview contracts

At the time this runbook was introduced, the implementation used:

- app image: `mcr.microsoft.com/devcontainers/dotnet:2-10.0-noble`;
- Node devcontainer Feature: major version 24;
- TimescaleDB image: `timescale/timescaledb:2.29.2-pg18`;
- Web port: `5173`;
- internal API: `5080`;
- private database service: `timescaledb:5432`;
- project key: `e2e-wave11`;
- administrative username: `EliteSCADA`;
- protected secret name: `ELITESCADA_PREVIEW_ADMIN_PASSWORD`.

These values are a snapshot for troubleshooting orientation, not an excuse to ignore live repository configuration. If the code changes, the code wins and this document must be updated.
