# LAST CHANGE — EliteSCADA

> Operational handoff. Read with `PROJECT GOAL.md`, `docs/ROADMAP.md`, `docs/PARALLEL-WORK.md` and `docs/CHAT-WORK-ASSIGNMENTS.md` before every EliteSCADA task.

**Handoff date:** 2026-08-26
**Development state:** **ACTIVE — AUDIT UI + SCRIPT FOUNDATION MERGED / INTERNAL MEMORY PR ACTIVE**

Repository truth is separated into **MERGED**, **IMPLEMENTED IN PR** and **SPECIFIED / NOT IMPLEMENTED**.

## CURRENT CHECKPOINT

Two worker deliveries from the current wave are now official `main` state:

- PR #47 — isolated Public Script Engineering integration foundation — merge `8a8a52e9e1725fca5b9e06ff9f560f583dab5bbb`;
- PR #46 — Audit UI and diagnostics client foundation — merge `5629f55699d68d70d11d7058c26033d54306b570`.

Coordinator-owned Audit integration was added after PR #46:

- `04199395fa62037d413781c7e7623bafe857a5e1` — central Runtime / Engineering / Audit navigation component;
- `f1059b89bd41a3b9f9a0af0b602a1a683fef5f14` — navigation styling;
- `838053a85d2382577f15db7da8a431eff92889c0` — central `/audit` application routing inside the existing authentication boundary.

The coordinator also repaired a stale CI assumption in:

- `c77d3096e6a6d225d23e4077b07704a341b1993a` — runtime smoke now verifies the persisted Engineering schema version against the schema actually exported by the running application instead of hard-coding schema v7.

The live assignment board was reconciled in:

- `192983b79245691ad44f8329b55fbff16f867360`.

## MERGED PRODUCT STATE

The established merged baseline now includes:

- PR #35 — Engineering Schema v7 + first-class Operational Commands — `2fd568976fc6277d0b069adeeb560f6ea3d8205f`;
- PR #36 — protected runtime read/realtime surfaces — `10b0320149c1ef2109e9517539717a8800b200c2`;
- PR #37 — Engineering UI foundation/localization — `4553aa7ab5ba7e05a209a7c8462286d1a34a1ad6`;
- PR #38 — local identity/browser login — `2a581d279a428cb605429d5939c333ff7ad8d1b4`;
- PR #39 — protected local user administration — `6de8f06a443ad829ccc95c6dfcd9511e906adeff`;
- PR #40 — Internal Memory / Source Provider foundation — `bb38617c9c27cb5c379973a6f65d66006f24eadc`;
- PR #41 — Python Scripting + Visual Property foundation — `fc0731309d5b92d302f019d06d3511d3a247b607`;
- PR #42 — secured Engineering Apply/Delete/Bulk — `6d49b99181fce6dabce838822ce972332e2f77f0`;
- PR #43 — Historian retention/downsampling foundation — `0c5f2aefdd5a7286c0c9367569067e2d12091c81`;
- PR #44 — Audit durability/query/retention foundation — `9406fb2d66c682bd6bde08a0facde0622aa86ff2`;
- PR #45 — Audit runtime integration — `889c989fdce26d8593e86e430e76417412846400`;
- PR #46 — Audit UI and diagnostics client foundation — `5629f55699d68d70d11d7058c26033d54306b570`;
- PR #47 — isolated Public Script Engineering integration foundation — `8a8a52e9e1725fca5b9e06ff9f560f583dab5bbb`.

### Audit UI now merged

The merged Audit frontend:

- consumes only the protected backend-supported Audit filters;
- preserves the keyset cursor as an opaque value;
- exposes explicit loading/empty/unauthenticated/forbidden/query/server states;
- presents protected Audit storage/buffer/retention diagnostics;
- renders only already-sanitized Audit metadata;
- is reachable at `/audit` through central application routing/navigation.

CI #244 passed Web build, backend build/tests, runtime smoke and Chromium E2E on the DEV 1 delivery head. Cross-origin exposure of `X-EliteSCADA-Audit-Next-Cursor` remains deployment-conditional; same-origin/Vite-proxy operation does not require a CORS exposed-header change.

### Script Engineering foundation now merged

PR #47 merged the isolated `Scada.Engineering.Scripts` domain with:

- stable Script ID/path/name;
- explicit Client Visual vs Server scope;
- Python language/version/source/enabled state;
- typed entry points/events/dependencies/metadata;
- deterministic identity, dependency-cycle, scope and visual-reference validation;
- adapters to the already-merged PR #41 public scripting/visual runtime contracts;
- focused automated tests.

CI #248 passed Web build, backend build/tests, runtime smoke and Chromium E2E on the DEV 3 delivery head.

This does **not** yet mean Scripts are first-class members of the canonical Engineering package. Central schema/migration/import-export/revision/PostgreSQL/`.escadapkg` integration remains coordinator-owned and intentionally waits until Internal Memory's central Engineering changes are reconciled.

## IMPLEMENTED IN PR / ACTIVE WORK

### PR #48 — DEV 2 — Internal Memory Engineering + durable Server Memory product integration

Branch: `feature/internal-memory-product-integration`

Observed current head: `d676cae2c3cee76ecf4238d2e8b6c495aa907d4c`

State: **OPEN DRAFT / IMPLEMENTED IN PR / NOT MERGED**

Observed implementation includes:

- Engineering schema v8 on the worker branch;
- typed `MemoryInitialValueDto` public representation;
- JSON/CSV round-trip and v7 backward compatibility;
- validation for `builtin.memory.client` and `builtin.memory.server`;
- rejection of fake network address/configuration for memory sources;
- rejection of global Historian/Alarm semantics for Client Memory, including unsafe Server→Client transitions with existing consumers;
- durable PostgreSQL Server Memory retention keyed by stable TAG ID;
- restart and path-rename restoration;
- fail-closed incompatible retained type handling and explicit reset coverage.

### PR #48 CI status

CI #251 ran on current observed head `d676cae2c3cee76ecf4238d2e8b6c495aa907d4c`:

- Web build: **SUCCESS**;
- backend restore/build: **SUCCESS**, 0 warnings / 0 errors;
- automated tests: **SUCCESS**, including Internal Memory, PostgreSQL retention, Timescale/Historian, Drivers and Security;
- runtime smoke: **FAILED only at the coordinator-owned literal `engineeringSchemaVersion == 7` assertion**;
- Chromium E2E: skipped because the backend job failed at that assertion.

The stale workflow assumption is now repaired on `main` by `c77d3096e6a6d225d23e4077b07704a341b1993a`. DEV 2 correctly did not modify the reserved workflow. A final green PR CI using the corrected base workflow is still required before coordinator review/merge.

DEV 2 must remain on PR #48 until it updates the PR body to the final actual implementation, records exact `INTEGRATION REQUIRED` hooks, obtains final-head CI and freezes at `WAIT_FOR_COORDINATOR`.

## CURRENT MAIN VALIDATION

Main CI #257 validates the merged Audit/Script work, central Audit routing/navigation, and the dynamic Engineering schema smoke check.

At this handoff observation:

- Web build: **SUCCESS**;
- backend restore/build/tests: **SUCCESS**;
- runtime smoke: **SUCCESS**;
- Chromium E2E: **IN PROGRESS**.

Do not describe CI #257 as fully green until Chromium completes successfully.

## SPECIFIED / NOT IMPLEMENTED

The locked source/protocol order remains:

`Internal Memory complete product integration -> TAG Gateway -> common multi-driver/Data Source diagnostics -> USER INTERFACE VALIDATION PREVIEW -> additional external protocols`

Therefore TAG Gateway remains **SPECIFIED / NOT IMPLEMENTED** and blocked until PR #48 is reviewed, integrated, fully validated and official on `main`.

The visual/Python order remains:

`public Script/visual Engineering integration -> Python editor/sandbox -> visual runtime object/property integration -> graphical Screens/Popups/Dynamos editor -> advanced libraries`

PR #47 completes the isolated Script Engineering domain foundation, but canonical first-class Script package integration is still pending. No concrete Python editor/engine or graphical editor is authorized yet.

## CURRENT CHAT ASSIGNMENTS

The exact live board is `docs/CHAT-WORK-ASSIGNMENTS.md`.

1. **DEV 1 - EliteSCADA** — PR #46 — `MERGED / WAITING`.
2. **DEV 2 - EliteSCADA** — PR #48 — `CI_FAILED / PR_OPEN` — continues the same Internal Memory assignment.
3. **DEV 3 - EliteSCADA** — PR #47 — `MERGED / WAITING`.

All workers retain `AfterCompletion: WAIT_FOR_COORDINATOR`.

## COORDINATOR RESUME POINT

On the next coordinator `siga`:

1. reread current mandatory documents from `main`;
2. check final status of main CI #257;
3. check current PR #48 head/body/diff/mergeability and its newest CI;
4. if DEV 2 is delivered with final green CI, review/reconcile its central Engineering changes and required runtime composition before merge;
5. after Internal Memory is official, integrate the already-merged Script Engineering domain into the canonical Engineering schema/package in coordinator-owned shared files;
6. update roadmap/handoff/assignments after each official state change;
7. do not assign TAG Gateway until Internal Memory completion criteria are actually satisfied on `main`.

## Permanent continuity rules

- GitHub branch/PR/head/CI state is operational truth.
- Open feature branches/PRs are **IMPLEMENTED IN PR**, never **MERGED**.
- Workers never choose their own next task or merge their own PR.
- Shared central integration belongs to the coordinator unless a narrow exception is explicitly assigned.
- Known-failing work is never merged.
- `siga` is the canonical short command; `continue` is equivalent.
