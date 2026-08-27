# LAST CHANGE — EliteSCADA

> Operational handoff. Read with `PROJECT GOAL.md`, `docs/ROADMAP.md`, `docs/PARALLEL-WORK.md` and `docs/CHAT-WORK-ASSIGNMENTS.md` before every EliteSCADA task.

**Handoff date:** 2026-08-26
**Development state:** **ACTIVE — PARALLEL WORKER WAVE MERGED / INTERNAL MEMORY CENTRAL INTEGRATION NEXT**

Repository truth is separated into **MERGED**, **IMPLEMENTED IN PR** and **SPECIFIED / NOT IMPLEMENTED**.

## CURRENT CHECKPOINT

The three worker deliveries from the current parallel wave are now official `main` state:

- PR #46 — Audit UI and diagnostics client foundation — merge `5629f55699d68d70d11d7058c26033d54306b570`;
- PR #47 — isolated Public Script Engineering integration foundation — merge `8a8a52e9e1725fca5b9e06ff9f560f583dab5bbb`;
- PR #48 — Internal Memory Engineering + durable Server Memory retention — merge `ad4a7aa8d17e4e7370e5801d470a69f1a096bab4`.

PR #48 final reconciled CI #265 passed against pre-merge `main` `35789b3f4910c5ba8130f6de71093e9d2e5fcb14`, and post-merge `main` CI #266 independently passed on merge commit `ad4a7aa8d17e4e7370e5801d470a69f1a096bab4`.

For both decisive validations, the relevant stack is green:

- Web build: **SUCCESS**;
- backend restore/build: **SUCCESS**;
- automated tests: **SUCCESS**;
- runtime smoke: **SUCCESS**;
- Chromium E2E: **SUCCESS**.

Before merge, the coordinator reconciled two stale central assumptions instead of weakening tests: Engineering schema checks no longer hard-code v7, and central Audit navigation no longer makes the legacy Engineering E2E locator ambiguous.

## MERGED PRODUCT STATE ADDED BY PR #48

Canonical Engineering is now **schema v8** on `main` for the Internal Memory evolution.

Merged capabilities include:

- public typed Internal Memory initial/default values through `MemoryInitialValueDto`;
- v7 JSON backward compatibility and re-export through current schema;
- TAG CSV typed initial-value exchange with legacy CSV compatibility;
- explicit Engineering validation for `builtin.memory.server` and `builtin.memory.client`;
- rejection of fabricated protocol/network configuration for memory sources;
- strict typed-value validation without silent numeric/cross-type coercion;
- Client Memory rejection as a global Historian or Alarm source, including unsafe Server→Client transitions;
- PostgreSQL durable Server Memory retained-value storage keyed by stable TAG ID rather than path;
- retained-value restoration across provider/store restart and TAG path rename when ID/type remain compatible;
- fail-closed incompatible retained data types;
- explicit guarded retained-value reset semantics;
- focused Core, schema/import-export and real PostgreSQL retention coverage.

Mutable Server Memory retained runtime values remain separate from immutable/versioned Engineering packages.

## MERGED AUDIT AND SCRIPT STATE

PR #46 remains the merged Audit UI foundation and is centrally reachable at `/audit` through the authenticated application routing/navigation.

PR #47 remains the merged isolated Script Engineering domain with stable identity, Client Visual vs Server scope, source/events/dependencies, deterministic validation and adapters to the PR #41 public scripting/visual runtime foundation.

Scripts are still not first-class members of the canonical Engineering package. Their central schema/migration/import-export/revision/`.escadapkg` integration remains coordinator-owned future work.

## INTERNAL MEMORY PRODUCT STATUS

PR #48 completes the **DEV 2 assigned worker slice**. DEV 2 is now `MERGED / WAITING` and must not start TAG Gateway or reopen its branch for additional scope.

However, do **not** describe Internal Memory as complete product integration yet. The following central/shared work remains coordinator-owned:

- compile engineered `builtin.memory.server` Data Sources/TAGs into the shared Server Memory runtime provider;
- wire `PostgreSqlServerMemoryRetentionStore` into actual runtime composition;
- publish authoritative Server Memory values through the shared TAG cache/Event Bus/realtime path and configured Historian/Alarm semantics;
- compose Client Memory per opened runtime client/session rather than as one global server store;
- preserve capability authorization and Audit for external Server Memory writes;
- provide explicit reset/migration handling when retained type is incompatible;
- expose appropriate central API/Engineering UI configuration hooks;
- avoid fake network diagnostics for Internal Memory.

Therefore the locked source/protocol order remains:

`Internal Memory complete product integration -> TAG Gateway -> common multi-driver/Data Source diagnostics -> USER INTERFACE VALIDATION PREVIEW -> additional external protocols`

**TAG Gateway remains SPECIFIED / NOT IMPLEMENTED and blocked.**

## CURRENT CHAT ASSIGNMENTS

The exact live board is `docs/CHAT-WORK-ASSIGNMENTS.md`.

1. **DEV 1 - EliteSCADA** — PR #46 — `MERGED / WAITING`.
2. **DEV 2 - EliteSCADA** — PR #48 — `MERGED / WAITING`.
3. **DEV 3 - EliteSCADA** — PR #47 — `MERGED / WAITING`.

All three workers retain `AfterCompletion: WAIT_FOR_COORDINATOR` and currently have no new assignment.

## COORDINATOR RESUME POINT

On the next coordinator `siga`:

1. reread current mandatory documents from `main`;
2. complete coordinator-owned Internal Memory runtime/DI/client-session/security/Audit/API/UI integration;
3. validate complete Internal Memory product integration with full CI;
4. only after that milestone may TAG Gateway be assigned;
5. separately, canonical Script schema/package integration may be scheduled after shared Engineering v8/Internal Memory integration is stable;
6. update roadmap/handoff/assignments after every official state change.

## Permanent continuity rules

- GitHub branch/PR/head/CI state is operational truth.
- Open feature branches/PRs are **IMPLEMENTED IN PR**, never **MERGED**.
- Workers never choose their own next task or merge their own PR.
- Shared central integration belongs to the coordinator unless a narrow exception is explicitly assigned.
- Known-failing work is never merged.
- `siga` is the canonical short command; `continue` is equivalent.
