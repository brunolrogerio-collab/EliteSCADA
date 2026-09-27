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
