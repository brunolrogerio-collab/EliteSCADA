# LAST CHANGE — EliteSCADA

> Operational handoff. Read with `PROJECT GOAL.md`, `docs/ROADMAP.md`, `docs/PARALLEL-WORK.md` and `docs/CHAT-WORK-ASSIGNMENTS.md` before every EliteSCADA task.

**Handoff date:** 2026-08-26
**Development state:** **ACTIVE — THREE WORKER PRs OPEN / NO NEW FUNCTIONAL MERGE YET**

Repository truth is separated into **MERGED**, **IMPLEMENTED IN PR** and **SPECIFIED / NOT IMPLEMENTED**.

## CURRENT CHECKPOINT

The latest official functional/product merge on `main` remains PR #45:

`889c989fdce26d8593e86e430e76417412846400`

Subsequent coordinator commits are documentation/coordination changes only. The latest assignment synchronization before this handoff is:

`b2c58b2397fe2b9f4d678f3bc59c5d4f9f68a198` — synchronize the live assignment board with open PRs #46–#48 and their observed CI state.

The commit containing this `LAST CHANGE.md` is newer than that SHA and must be obtained from current GitHub `main` when resuming.

## MERGED PRODUCT STATE

The established merged baseline remains:

- PR #35 — Engineering Schema v7 + first-class Operational Commands — merge `2fd568976fc6277d0b069adeeb560f6ea3d8205f`;
- PR #36 — protected runtime read/realtime surfaces — merge `10b0320149c1ef2109e9517539717a8800b200c2`;
- PR #37 — Engineering UI foundation/localization — merge `4553aa7ab5ba7e05a209a7c8462286d1a34a1ad6`;
- PR #38 — local identity/browser login — merge `2a581d279a428cb605429d5939c333ff7ad8d1b4`;
- PR #39 — protected local user administration — merge `6de8f06a443ad829ccc95c6dfcd9511e906adeff`;
- PR #40 — Internal Memory / Source Provider foundation — merge `bb38617c9c27cb5c379973a6f65d66006f24eadc`;
- PR #41 — Python Scripting + Visual Property foundation — merge `fc0731309d5b92d302f019d06d3511d3a247b607`;
- PR #42 — secured Engineering Apply/Delete/Bulk — merge `6d49b99181fce6dabce838822ce972332e2f77f0`;
- PR #43 — Historian retention/downsampling foundation — merge `0c5f2aefdd5a7286c0c9367569067e2d12091c81`;
- PR #44 — Audit durability/query/retention foundation — merge `9406fb2d66c682bd6bde08a0facde0622aa86ff2`;
- PR #45 — Audit runtime integration — merge `889c989fdce26d8593e86e430e76417412846400`.

PR #45 CI #241 was green across Web build, backend build/tests, PostgreSQL/Timescale coverage, runtime smoke and Chromium E2E.

No functionality from PRs #46, #47 or #48 is official until merged.

## IMPLEMENTED IN PR / ACTIVE WORK

### PR #46 — DEV 1 — Audit UI and diagnostics client foundation

Branch: `feature/audit-ui`

Observed head: `0659647be5c127a0555f585005a20597255fa990`

State: **OPEN DRAFT / IMPLEMENTED IN PR / NOT MERGED**

Observed scope is isolated to `web/scada-web/src/audit/**` plus focused Audit UI contract testing. It does not alter central routing/application shell or backend Audit semantics.

Implemented in the PR body/diff:

- Audit query UI using only merged backend-supported filters;
- opaque `X-EliteSCADA-Audit-Next-Cursor` handling;
- explicit loading/empty/auth/forbidden/query/server error states;
- protected Audit diagnostics presentation;
- sanitized metadata presentation;
- feature-local localization;
- focused contract test.

`INTEGRATION REQUIRED` remains coordinator-owned:

- central `/audit` route registration in `web/scada-web/src/main.tsx`;
- central Runtime/Engineering navigation entry;
- cross-origin CORS exposure for `X-EliteSCADA-Audit-Next-Cursor` if deployment uses a distinct API origin;
- optional later shared-localization consolidation.

CI observation:

- CI #243 started but was replaced/cancelled;
- CI #244 is running for the same current head;
- Web build in #244 is already **SUCCESS**;
- backend build/tests/runtime smoke and subsequent Chromium completion were still pending at the last coordinator observation.

DEV 1 remains `PR_OPEN`; it must not start a new task. If #244 fails, it fixes only attributable branch failures. If it passes and the PR remains complete, it updates evidence and waits for coordinator.

### PR #47 — DEV 3 — Public Script Engineering integration foundation

Branch: `feature/script-engineering-integration`

Observed head: `da6dd4914741a6fa9ece4c758d245899fb20af92`

State: **OPEN DRAFT / IMPLEMENTED IN PR / NOT MERGED / WORK IN PROGRESS**

Observed diff is isolated to new Script Engineering files and focused tests. No `EngineeringContracts.cs`, `Program.cs`, workflow, coordinator documentation or central frontend routing changes are present.

Implemented direction includes:

- stable Script ID/path/name;
- explicit Client Visual vs Server scope;
- Python language/version/source/enabled state;
- entry points/events/dependencies/metadata;
- deterministic Script/reference validation;
- adapters to the merged PR #41 scripting/visual runtime contracts.

Coordinator integration remains required later for canonical first-class Scripts in Engineering schema, migration/import-export, preview/apply, revisions, PostgreSQL Engineering persistence, `.escadapkg` and Screen/Popup/Dynamo stable Script references.

At the last observation, no PR-triggered workflow run was present for the current head and the PR body explicitly states final CI evidence is still pending. DEV 3 remains `PR_OPEN` and continues only the assigned validation/testing/handoff work.

### PR #48 — DEV 2 — Internal Memory Engineering + durable Server Memory product integration

Branch: `feature/internal-memory-product-integration`

Observed head: `8ea8f7770322de0c1244b70c26027ab0ba2b5a2a`

State: **OPEN DRAFT / IMPLEMENTED IN PR / NOT MERGED / WORK IN PROGRESS**

Observed branch has 13 worker commits from the assignment baseline. Changes remain inside the authorized Internal Memory/Core/Engineering ImportExport/Validation/PostgreSQL/test domains, including the explicit narrow exception for Internal Memory changes to `EngineeringContracts.cs`.

Observed implementation includes:

- Internal Memory core/provider evolution;
- public Engineering typed memory initial-value representation;
- strict Engineering/Core typed-value codec;
- Internal Memory validation/import-export changes;
- PostgreSQL Server Memory retention storage;
- stable-ID retention/reset focused tests.

The PR body still declares remaining product integration/validation work and final CI evidence pending. No PR-triggered workflow run was present for the current observed head.

DEV 2 remains `PR_OPEN` and continues the assigned completion criteria. Final `Program.cs`/DI/runtime/API/shared frontend composition remains coordinator-owned `INTEGRATION REQUIRED` work.

## SPECIFIED / NOT IMPLEMENTED

The locked source/protocol order remains:

`Internal Memory complete product integration -> TAG Gateway -> common multi-driver/Data Source diagnostics -> USER INTERFACE VALIDATION PREVIEW -> additional external protocols`

Therefore TAG Gateway remains **SPECIFIED / NOT IMPLEMENTED** and is still blocked until Internal Memory is complete and official on `main`.

The visual/Python order remains:

`public Script/visual Engineering integration -> Python editor/sandbox -> visual runtime object/property integration -> graphical Screens/Popups/Dynamos editor -> advanced libraries`

PR #47 advances only the first foundation/integration stage. No concrete Python engine/editor or graphical editor is authorized yet.

## CURRENT CHAT ASSIGNMENTS

The exact live board is `docs/CHAT-WORK-ASSIGNMENTS.md`.

Current worker state:

1. **DEV 1 - EliteSCADA** — PR #46 — `PR_OPEN` — CI #244 running.
2. **DEV 2 - EliteSCADA** — PR #48 — `PR_OPEN` — implementation/validation still in progress.
3. **DEV 3 - EliteSCADA** — PR #47 — `PR_OPEN` — focused tests/CI handoff still in progress.

All three have `AfterCompletion: WAIT_FOR_COORDINATOR`.

## COORDINATOR RESUME POINT

On the next coordinator `siga`:

1. reread current `main` mandatory documents;
2. re-check PR #46/#47/#48 heads, diffs, bodies, mergeability and CI;
3. if PR #46 is green and complete, review its diff and perform only the required central integration before final CI/merge;
4. do not merge PR #47 before its focused validation/CI is complete and its isolated semantics are reviewed;
5. prioritize PR #48 because Internal Memory gates the source/protocol chain;
6. after PR #48 is accepted, reconcile its central Engineering changes before adding coordinator-owned Script canonical-schema hooks from PR #47;
7. merge only reviewed green work;
8. update `docs/ROADMAP.md`, this handoff and the assignment board after official merges;
9. only assign TAG Gateway after Internal Memory completion criteria are satisfied on `main`.

## Permanent continuity rules

- GitHub branch/PR/head/CI state is operational truth.
- Feature branches and open PRs are **IMPLEMENTED IN PR**, never **MERGED**.
- Workers never choose their own next task or merge their own PR.
- Shared central integration belongs to the coordinator unless an assignment grants a narrow explicit exception.
- Known-failing work is never merged.
- `siga` is the canonical short command; `continue` is equivalent.
