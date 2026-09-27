# Wave 15 — ENV_B Product Owner Codespace Launch

**State:** READY_FOR_PRODUCT_OWNER_CONFIRMATION  
**Owner:** MAIN COORDINATOR  
**Purpose:** one-click creation entry for the real Product Owner ENV_B lifecycle proof.

## Exact source

Repository:

`brunolrogerio-collab/EliteSCADA`

Dedicated immutable audit branch at release:

`preview/w15-first-project-env-b-codespace`

Exact branch point:

`bb451fa6e07982ac12384895f6097d5833761d16`

Tree:

`650d30089596021cb1a564ee1d2f1abfc7d2b509`

The branch contains the reviewed Wave 15 Preview harness and the repository-owned `.devcontainer/devcontainer.json`.

Do not create ENV_B from `main`, `wave15/corrections-integration` or the moving harness branch.

## Official one-click creation URL

`https://codespaces.new/brunolrogerio-collab/EliteSCADA/tree/preview/w15-first-project-env-b-codespace`

This is the GitHub-supported Codespaces creation deep link for a specific repository branch.

Expected Product Owner interaction:

1. Open the URL above while authenticated to GitHub.
2. Verify the page shows repository `brunolrogerio-collab/EliteSCADA` and branch `preview/w15-first-project-env-b-codespace`.
3. Leave the repository-provided devcontainer configuration selected.
4. Confirm **Create codespace**.

No manual terminal/bootstrap/port setup is part of the normal path.

## Repository-controlled expected behavior after confirmation

The devcontainer must automatically:

1. create the Compose-backed environment;
2. start TimescaleDB;
3. start EliteSCADA API + Web;
4. expose only Web port 5173 to browser;
5. run the repository `postStartCommand`;
6. make 5173 PUBLIC automatically;
7. verify 5080 and 5432 are not public;
8. preserve the product's own first-run Administrator flow;
9. create no project/import/Demo/EEE state.

Expected browser entry is the Codespaces forwarded 5173 URL.

## First-open boundary

Do not begin the actual human product audit immediately.

First open is ENV_B readiness proof. Before creating Administrator/project state beyond the minimum continuity marker authorized by Main, capture:

- exact checkout HEAD;
- Codespace name;
- devcontainer startup result;
- container/service health;
- live port visibility;
- public 5173 browser result;
- initial product first-run state.

Required exact HEAD:

`bb451fa6e07982ac12384895f6097d5833761d16`.

## Resume proof

After the first-open evidence:

1. stop the Codespace normally;
2. resume the same Codespace;
3. do not manually run startup commands;
4. verify API/Web/database recover;
5. verify 5173 becomes PUBLIC again automatically;
6. verify 5080/5432 remain non-public;
7. verify the product-owned continuity marker persists;
8. verify browser access returns normally.

Only Main may then mark:

`ENV_B = READY`.

## Current release state

- ENV_A: `READY / CLEAN / RESUMABLE`.
- ENV_B: `READY_FOR_PRODUCT_OWNER_CONFIRMATION / WAIT_REAL_CODESPACE_PROOF`.
- CODEX Stage 1: HOLD.
- Human Preview: HOLD.
- CODEX Stage 2: PREPARED / NOT ACTIVE.
