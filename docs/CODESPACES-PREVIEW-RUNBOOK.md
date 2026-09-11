# EliteSCADA — Codespaces Preview Operational Runbook

**Purpose:** reproducible procedure for creating, rebuilding, diagnosing and accepting the EliteSCADA browser Preview in GitHub Codespaces.  
**Historical harness:** issue #208 / draft PR #210 / branch `preview/codespaces-test-preview`.  
**Wave 14 use:** final clean-browser homologation after C09/C10 convergence, C11 pass-2 gap disposition and construction of the new canonical EEE DEMO.

> This file is copied into the active integration branch because the original proven runbook lived only on the Preview branch. Future coordinators must not need an old chat to remember how the Codespace was made to work.

## 1. Core rule

GitHub is the source of truth. Before creating or rebuilding a Codespace, confirm:

- exact product SHA to be tested;
- exact Preview/harness branch containing that product plus Preview infrastructure;
- current PR/issue state;
- exact CI state.

A Codespace is disposable homologation infrastructure. Never change product security, authentication, licensing or Runtime architecture merely to keep a broken Codespace alive.

The Preview must exercise the normal product lifecycle:

`Working -> saved Revision -> Published -> Active -> HMI Runtime`

There is no Preview-only auth/licensing/runtime bypass.

## 2. Historical Preview baseline

Historical Preview PR #210 is open/draft and uses:

`preview/codespaces-test-preview`

Historical current PR head at the 2026-09-03 synchronization:

`a08171ebe62ce20427a22aaf028b764a9c114184`

That branch proved the environment and real-browser login path, but its product content is old relative to the active Wave 14 integration. Do not use that SHA as the final corrected product baseline.

Historical environment contracts:

- exact .NET SDK 10.0.400 required by `global.json` at that time;
- Node 24;
- TimescaleDB/PostgreSQL in Compose;
- Web port 5173;
- internal API 5080;
- private DB service 5432;
- protected Codespaces secret `ELITESCADA_PREVIEW_ADMIN_PASSWORD`;
- automatic launcher `scripts/preview/launch-test-preview.sh`;
- launcher starts through devcontainer `postAttachCommand`;
- Web 5173 normally remains **Private**;
- API and database remain internal;
- disposable per-Codespace machine identity mounted read-only at `/etc/machine-id` so normal licensing can remain fail-closed.

Always re-check live repository configuration because these values are operational history, not permission to ignore changed code.

## 3. Wave 14 final Preview strategy

The historical Preview harness is infrastructure, not product scope.

For final Wave 14 acceptance:

1. first establish a converged exact product SHA after C09/C10;
2. execute C11 pass 2 and disposition required product gaps;
3. rerun C10 after any approved corrections;
4. explicitly release and build the new canonical EEE DEMO;
5. only then prepare a Preview branch that contains the **current product + Preview infrastructure + new canonical DEMO**.

Do not resurrect the historical Wave 11 DEMO merely because the old launcher already knows how to reconstruct it.

The Preview branch may be the updated existing `preview/codespaces-test-preview` or a coordinator-approved successor. Decide from live GitHub. Whichever branch is used, record exact product SHA and Preview-harness SHA/provenance.

## 4. Before creating a Codespace

1. Read `PROJECT GOAL.md`, `LAST CHANGE.md`, `docs/CURRENT-COORDINATOR-HANDOFF.md`, issue #211 and the active Preview PR.
2. Record the exact intended source SHA.
3. Confirm universal `EliteSCADA CI` and all impact-specific workflows required for that product SHA are green, unless the Codespace is intentionally being used to investigate an already-documented failure.
4. Confirm repository Codespaces secret exists:

   `ELITESCADA_PREVIEW_ADMIN_PASSWORD`

5. Never print, echo, commit or place the secret in an issue/PR/log/artifact.
6. The password must satisfy the current backend-authoritative Local Identity policy. Wave 14 changed the minimum to 8 characters. Historical evidence that an earlier Preview failed under the former 12-character minimum is valid history, not current policy.
7. Verify `.devcontainer` configuration and `global.json` agree on SDK requirements.
8. Verify the Preview branch contains the intended canonical DEMO and no stale launcher-only assumption points back to the historical DEMO.

## 5. Golden path — create a fresh Codespace

For final acceptance prefer a fresh Codespace rather than a heavily repaired old one.

1. Create the Codespace from the exact active Preview branch/revision, not casually from `main`.
2. Select the repository devcontainer configuration **EliteSCADA Test Preview** if still named that way.
3. Associate `ELITESCADA_PREVIEW_ADMIN_PASSWORD` with the Codespace/repository.
4. Wait for devcontainer creation and `postCreateCommand` to finish.
5. When VS Code Web attaches, `postAttachCommand` should automatically execute:

```bash
bash scripts/preview/launch-test-preview.sh
```

6. Normal accepted startup must not require a human to type that command manually.
7. Wait for forwarded port **5173**, historically labeled `EliteSCADA Web — Test Preview`.
8. Keep 5173 **Private**.
9. Do not expose API 5080 or database 5432.
10. Open the forwarded 5173 URL.
11. Sign in through the real EliteSCADA login UI. Historical Preview used username `EliteSCADA`; verify the current launcher/first-run contract before assuming the same bootstrap path.
12. Continue through the real browser acceptance checklist below.

## 6. What the automatic launcher must achieve

The repository launcher is the canonical startup mechanism. It should, without hand-edited container state:

1. verify required commands/tooling;
2. require the protected administrator secret where the current first-run strategy requires it;
3. reach private `timescaledb:5432`;
4. provide a stable disposable machine identity to the normal licensing path;
5. initialize the product through normal Local Identity/first-run behavior;
6. avoid leaving bootstrap credentials in the long-lived API process environment;
7. authenticate through normal product APIs;
8. create/import the intended canonical project through normal Engineering flows;
9. execute Save -> Publish -> Activate where required;
10. verify Active Runtime consistency;
11. verify expected Demo-license state when no product license is installed;
12. start Vite/Web on `0.0.0.0:5173` while API remains internal.

### Wave 14 change to the launcher

The historical launcher reconstructed an accepted Wave 11 Demo `.escadapkg`. After C11 implementation, that behavior is obsolete for Wave 14 acceptance.

Update the launcher so it uses the new canonical EEE DEMO and current C01 first-run/project behavior. Do not add a special bypass to avoid exercising the real current product flow.

## 7. Recovery levels

Use the smallest recovery action appropriate to the change.

### Level A — browser reload / HMR

Use for a frontend-only change when Vite HMR has loaded the new code and API/environment are healthy.

### Level B — restart Preview launcher

Use when:

- backend source changed;
- launcher changed;
- DEMO/fixture content changed;
- API/Web process died;
- HMR is no longer trustworthy.

Preferred UI path:

`Tasks -> Run Task -> Launch Test Preview`

Equivalent shell command:

```bash
bash scripts/preview/launch-test-preview.sh
```

A Level B restart is recovery, not permission to hide broken devcontainer setup.

### Level C — Rebuild Container

Use when changing:

- `.devcontainer/devcontainer.json`;
- `.devcontainer/docker-compose.yml`;
- machine-id initialization/mounting;
- devcontainer image/features;
- SDK/Node/environment dependencies;
- `postCreateCommand` / `postAttachCommand` behavior.

After rebuild, re-check exact Git SHA before testing. A rebuilt environment and moving branch are an excellent way to test a version nobody intended.

### Level D — fresh Codespace

Use when:

- disposable DB/bootstrap state is ambiguous;
- identity was created with another Preview password;
- old container generations had materially different config;
- repeated rebuilds make reproducibility uncertain;
- final acceptance needs clean-start proof.

**Final Wave 14 owner acceptance should include Level D.**

## 8. Proven failure modes

### 8.1 Required .NET SDK not found

Historical symptom:

`Nenhuma versão obrigatória de um SDK do .NET foi encontrada`

Meaning: devcontainer SDK did not satisfy `global.json`.

Correct action:

- fix repository devcontainer SDK contract;
- validate it;
- Level C rebuild or Level D fresh Codespace.

Do not relax `global.json` merely for Preview convenience unless product SDK policy is intentionally changing.

### 8.2 Licensing fails before API readiness because `/etc/machine-id` is missing

Historical cause: `DefaultMachineIdentityProvider` correctly failed closed.

Correct action:

- generate disposable machine identity as environment state;
- mount it read-only at `/etc/machine-id`;
- keep product licensing unchanged;
- rebuild/fresh Codespace.

Never add a Preview licensing bypass.

### 8.3 Local Identity bootstrap password rejected

The Preview secret may violate the current product password policy.

Correct action:

- fix the protected secret;
- keep backend policy authoritative;
- restart/rebuild so the new secret is injected;
- if DB identity state is now ambiguous, use a fresh Codespace.

Never expose the password while diagnosing it.

### 8.4 Forwarded 5173 returns HTTP 502

502 means Codespaces forwarding exists but no Web process is listening. It is not a ready Preview.

Collect:

```bash
git rev-parse HEAD
dotnet --version
docker compose -f .devcontainer/docker-compose.yml ps
echo "===== API LOG ====="
tail -n 160 .preview/api.log
echo "===== WEB LOG ====="
tail -n 160 .preview/web.log
curl -I http://127.0.0.1:5173/
curl -I http://127.0.0.1:5080/health
```

Interpret the first causal failure. Do not patch the last stack-frame line by superstition.

### 8.5 Integrated terminal blank/unusable

This occurred in real Codespaces. The terminal UI can fail while the container is healthy.

Fallback: use the Codespace's built-in AI/agent to run exact **read-only** diagnostics and return complete output.

Safe examples:

```bash
git rev-parse HEAD
dotnet --version
docker compose -f .devcontainer/docker-compose.yml ps
tail -n 160 .preview/api.log
tail -n 160 .preview/web.log
```

Never ask it to print secret environment variables.

### 8.6 Application opens but is unusable

Real Preview testing exposed a Script Engineering contrast defect even though startup/login succeeded.

Rule: browser availability is not product acceptance. Unreadable text, invisible controls, wrong layout, broken scaling and bad interaction are real defects.

## 9. Updating an existing Codespace

Before update:

```bash
git rev-parse HEAD
git status --short
```

If there are unexpected local changes, diagnose them. Do not use routine `git reset --hard` to bulldoze evidence.

Then:

```bash
git fetch origin
git checkout <active-preview-branch>
git pull --ff-only
git rev-parse HEAD
```

Verify the final SHA matches the intended homologation SHA, then deliberately choose Level A/B/C/D.

## 10. Security checklist

- never commit/print `ELITESCADA_PREVIEW_ADMIN_PASSWORD`;
- keep Web 5173 Private for accepted state;
- do not expose API 5080;
- do not expose DB 5432;
- no credentials in `.escadapkg`, artifacts or logs;
- do not weaken auth, authorization, password policy or licensing;
- generated machine identity is disposable environment state, not a license/signing secret;
- no Authenticode/private signing keys in Codespaces/normal CI.

## 11. Wave 14 real-browser acceptance checklist

For the exact SHA under acceptance, record evidence that:

1. clean Codespace was created from repository-controlled configuration;
2. `git rev-parse HEAD` matches the intended Preview/product composition;
3. `dotnet --version` matches `global.json`;
4. TimescaleDB is healthy privately;
5. automatic startup succeeds without mandatory manual terminal intervention;
6. 5173 opens actual EliteSCADA without 502;
7. 5173 is Private;
8. real login/first-run flow succeeds;
9. first Administrator/project workflow behaves correctly when fresh-state testing is intended;
10. Engineering is readable and usable in Dark/Light;
11. `pt-BR`, `en`, `es` switching works on representative changed surfaces;
12. current Driver/Data Source forms work;
13. TAG Source/address assistants work, including representative Modbus and OPC UA discovery/browse;
14. Property Inspector exposes current canonical properties;
15. Screen authoring/Popups/Dynamos work;
16. Script Assistant/Project Object Browser works;
17. Python/Pyodide loads and security boundaries remain intact;
18. Save -> Publish -> Activate produces the real Active Runtime;
19. operator-only identity sees the intended Runtime shell/capabilities;
20. Runtime scaling is checked at representative 1280×720, 1920×1080, 2560×1440 and 3840×2160 plus a mismatched aspect ratio;
21. there is no document scroll/reflow of HMI composition;
22. equipment hit targets remain aligned under scaling;
23. Screen navigation works;
24. Popups render correctly under the same logical transform;
25. alarms/events/trends/history used by the canonical EEE DEMO behave naturally;
26. DEMO Simulation is visibly alive: well level, pumps, analog values, faults/quality scenarios;
27. launcher restart is repeatable;
28. exact-head universal and specialized CI remain green.

CI alone is insufficient. Real-browser homologation is part of Wave 14 acceptance.

## 12. Investigation loop when Codespace finds a defect

1. record exact running SHA;
2. capture smallest useful evidence;
3. classify failure: product, launcher, devcontainer, secret/state, forwarding or Codespaces UI;
4. fix the narrowest responsible layer;
5. add regression test where practical;
6. push new head;
7. wait for required CI;
8. update Codespace;
9. choose recovery level deliberately;
10. reproduce the original failure in the real browser;
11. continue acceptance where it stopped.

Any manual workaround necessary for success must be converted into repository-controlled automation before final acceptance.

## 13. Coordinator evidence to record

For each final Preview/Codespace pass record in GitHub:

- active Preview branch;
- exact product SHA;
- exact harness/combined SHA;
- workflow run IDs/results;
- whether Codespace was fresh or rebuilt;
- whether automatic startup succeeded;
- whether 5173 opened;
- whether login/first-run succeeded;
- last product surface validated;
- next pending surface;
- known failure and diagnostics;
- accepted port visibility state.

The repository record must be enough for a future coordinator to continue without conversation history.
