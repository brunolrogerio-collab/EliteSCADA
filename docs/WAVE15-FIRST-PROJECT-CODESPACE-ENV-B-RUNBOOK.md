# Wave 15 — First Project Preview — ENV_B Codespace Runbook

**Owner:** MAIN COORDINATOR  
**Purpose:** prepare and validate the Product Owner human-audit Codespace independently from CODEX ENV_A work.  
**Product authority:** `1f14a57491805a5d976bc9d0bf51393cf1b3ebcd` / tree `5e5fce8ce87f31dfc11b83bb68ff86c67f9f0112`.  
**Harness branch:** `preview/w15-first-project-env-harness`.  
**Current status:** PREPARED STATICALLY / WAIT EXACT SHARED HARNESS ACCEPTANCE / REAL CODESPACE NOT YET CREATED.

GitHub live remains the authority. Before creating the real Codespace, revalidate the harness branch and exact accepted SHA.

## 1. Separation from ENV_A

CODEX owns local ENV_A implementation/readiness work.

MAIN owns ENV_B preparation and acceptance in parallel.

MAIN must not edit the shared harness files while CODEX is actively correcting ENV_A. ENV_B-specific findings are first recorded here/control-side. Any shared-harness change is applied only after the active CODEX correction handoff is reviewed, avoiding concurrent writes to the same implementation branch.

## 2. Current lifecycle design accepted statically

The current harness direction uses:

- Compose-backed devcontainer;
- normal EliteSCADA API + Web startup from the same exact product bytes;
- independent TimescaleDB state inside the Codespace;
- Web port `5173` as the only browser-facing port;
- API `5080` internal;
- DB `5432` internal;
- `postStartCommand` for Codespace start/resume policy;
- repository script `scripts/preview/codespaces-public-web-port.sh` to re-assert and verify `5173:public`;
- product authentication remains authoritative;
- no precreated Administrator, project, package, EEE Demo or hidden Working state.

This deliberately differs from the old Wave 14 reliance on attach-time behavior. The current Dev Container specification defines `postStartCommand` as a command run each time the container successfully starts, including environment resume. GitHub Codespaces documentation also states that a public forwarded port reverts to private after Codespace restart, so public visibility must be re-applied on each start/resume.

## 3. ENV_B real acceptance gate

ENV_B is not READY from static review.

After one exact shared harness SHA is accepted, MAIN will require a **fresh Codespace** from that harness and prove:

1. `git rev-parse HEAD` equals the exact accepted harness SHA;
2. product base provenance remains `1f14a574...`;
3. TimescaleDB becomes healthy without host setup;
4. API and Web start automatically with no terminal command;
5. fresh product state still requires first Administrator setup;
6. no project/application/import/Demo is pre-seeded;
7. 5173 is automatically forwarded and made PUBLIC;
8. 5080 and 5432 are not public;
9. the public forwarded browser URL opens the real EliteSCADA surface;
10. stop the Codespace;
11. resume the same Codespace;
12. product API/Web recover automatically;
13. `postStartCommand` re-applies and verifies 5173 PUBLIC;
14. ordinary browser access works again without Product Owner changing the Ports panel;
15. product state continuity is preserved across the stop/resume cycle;
16. no product authentication/licensing/security bypass was introduced.

Only after all items pass may MAIN mark:
`ENV_B_READY / HUMAN_PREVIEW_RELEASED`.

## 4. Product Owner browser experience target

Once ENV_B is READY, normal use should be:

1. Open/resume the Codespace.
2. Wait for repository-controlled startup to finish.
3. Open the public 5173 forwarded URL in a normal browser.
4. Use EliteSCADA normally.
5. No manual terminal startup.
6. No manual port-visibility change.
7. No access to API/database ports.

Public 5173 intentionally removes GitHub's forwarded-port authentication. EliteSCADA's own login/authentication remains required and must not be weakened.

## 5. Recovery behavior

A 5173 forwarding entry alone is not readiness. If the browser returns 502, treat it as environment failure until the application listener is proven healthy.

Before restart/rebuild, preserve:
- exact harness SHA;
- Codespace name;
- port list/visibility;
- API/Web service health;
- relevant environment startup logs.

Do not repair the product or seed project state to make the environment appear ready.

## 6. Current execution boundary

MAIN can complete repository/static preparation now.

A **real Codespace lifecycle proof cannot be executed through the currently connected GitHub automation**, because the available GitHub connector does not expose Codespace create/start/stop actions.

Therefore, after exact harness acceptance, the only Product Owner interaction needed should be the minimal act of creating/opening the fresh Codespace. Repository automation must handle product startup and public-port configuration after that.

Do not ask the Product Owner to perform manual environment configuration that can be repository-controlled.


## 7. Current exact shared harness candidate

Current exact shared candidate selected by Main:
- branch: `preview/w15-first-project-env-harness`;
- SHA: `ec050e9bfda121805b1165860a4aeda0eb2582e8`;
- tree: `0d6221540c3678a8b042c51082f7a2ed0a466fa2`;
- product parent/base: `1f14a57491805a5d976bc9d0bf51393cf1b3ebcd`.

The replacement relative to the prior static-reviewed harness changes only `scripts/preview/local-audit.ps1`, so ENV_B devcontainer/Compose/public-port mechanics are unchanged from the reviewed design.

Do not create the real Product Owner Codespace from a later moving branch head without revalidation. At creation time, record `git rev-parse HEAD`; it must equal the exact harness SHA Main has accepted for ENV_B or the environment is not under acceptance.

Real Codespace execution remains pending because the connected GitHub connector has no Codespaces lifecycle actions.


## 8. Exact Product Owner Codespace branch released for lifecycle proof

Main has accepted ENV_A and created a dedicated exact branch for the Product Owner Codespace lifecycle proof:

`preview/w15-first-project-env-b-codespace`

Creation point:
`bb451fa6e07982ac12384895f6097d5833761d16`

tree:
`650d30089596021cb1a564ee1d2f1abfc7d2b509`.

This branch was created directly from the accepted Preview harness candidate. Use this branch, not the moving `preview/w15-first-project-env-harness`, when creating the real Product Owner Codespace.

### Product Owner action boundary

The Product Owner should only need to:
1. create/open a **new Codespace** for repository `brunolrogerio-collab/EliteSCADA` on branch `preview/w15-first-project-env-b-codespace`;
2. wait for the devcontainer lifecycle to complete;
3. open the forwarded Web 5173 URL in a normal browser when available.

Do not manually start API/Web/database unless Main is diagnosing an environment failure. Do not manually make ports public unless the repository-controlled policy fails and Main first captures the failure evidence.

### First-open acceptance evidence

Before any human product exploration beyond confirming the fresh first-run surface, capture:
- `git rev-parse HEAD`;
- Codespace name;
- devcontainer startup outcome;
- live Codespaces port list/visibility;
- Web/API/database container/service health;
- public 5173 browser result;
- visible product first-run state.

Expected:
- HEAD = `bb451fa6e07982ac12384895f6097d5833761d16`;
- 5173 = PUBLIC;
- 5080/5432 != PUBLIC;
- EliteSCADA visible in browser;
- initial Administrator setup available;
- no persisted project/application/import/Demo state.

Do not create the real audit project yet. This first open is ENV_B readiness proof only.

### Resume acceptance evidence

After first-open readiness is captured:
1. stop the Codespace through normal GitHub Codespaces lifecycle;
2. reopen/resume the same Codespace;
3. do not run terminal recovery commands;
4. verify repository-controlled startup restores API/Web/database;
5. verify 5173 becomes PUBLIC again automatically;
6. verify 5080/5432 remain non-public;
7. verify the product-owned state used for continuity proof remains present;
8. verify browser access works again.

Only then may Main record `ENV_B_READY` and release the Product Owner human Preview together with CODEX Stage 1.
