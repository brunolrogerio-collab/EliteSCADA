# Wave 15 — INFRA-CI-01C PostgreSQL Shared-Schema Race Recurrence Control

> GitHub live is the sole authority.
> This is a bounded generic infrastructure correction triggered by the exact FC0-A post-merge gate.
> It does not reopen the accepted FC0-A product package.

`CONTROL_BRANCH: coord/w15-infra-ci-01c-control`

`MAIN_ORDER_REV: 0002`

`STATE: MERGED / POST_MERGE_BACKEND_GREEN / NO_MUTATION`

`ORDER_ID: INFRA-CI-01C-POSTGRES-SCHEMA-RECURRENCE-V1`

`EXECUTOR: SEQUENTIAL CODEX`

`EXACT_BASE_SHA: d975174ae81ff7ed754585097240778a9862d965`

`EXACT_BASE_TREE: 5ac06f47f1bede7a1b0c384c7b1d3c83730015ea`

`WORK_BRANCH: work/w15-infra-ci-01c-postgres-schema-recurrence`

`TARGET_BRANCH: wave15/corrections-integration`

`VALIDATION_PROFILE: FOUNDATION_LIFECYCLE`

## 1. Trigger

PR #340 FC0-A consolidated correction package was accepted on exact candidate head
`ee1764cf31e5eff3d0396d9b5590661fb33e1d63` after natural Wave 15 T1
`36043296814` succeeded and the required Chromium owner suite completed
`54 passed`.

Protected merge produced:

`d975174ae81ff7ed754585097240778a9862d965`

Exact post-merge push gate:

- workflow: `EliteSCADA CI`
- run: `36044280802`
- Web build: SUCCESS
- Backend build: SUCCESS
- Backend Test: FAILURE
- Runtime smoke: skipped downstream
- Chromium: skipped downstream

Single identified failing test:

`Scada.Persistence.PostgreSql.Tests.PostgreSqlEngineeringSchemaV15CommunicationBindingTests.PostgreSqlRevision_SavePreviewApply_RoundTripsCommunicationBinding`

Exact database failure:

`Npgsql.PostgresException 23505: duplicate key value violates unique constraint "pg_namespace_nspname_index"`

Database log:

`Key (nspname)=(elitescada) already exists.`

Failing statement:

`CREATE SCHEMA IF NOT EXISTS elitescada`

Project result:
- Scada.Persistence.PostgreSql.Tests: 124 total / 123 passed / 1 failed.
- Other visible backend projects completed green, including Core 399/399 and Drivers 693/693.

## 2. Classification / non-causality

This failure is classified:

`GENERIC_INFRASTRUCTURE_RECURRENCE / NOT_FC0A_PRODUCT_CAUSAL`

Main verified the following blobs are byte-identical between frozen product checkpoint
`560ac9d80cc7e854f2513559dc6afb28cfb4aee3` and merged FC0-A checkpoint
`d975174ae81ff7ed754585097240778a9862d965`:

- `PostgreSqlEngineeringProjectStore.cs` — blob `a2ba40adb2ce35a4d61ff8c867eb237403277ed4`
- `PostgreSqlSharedSchemaInitialization.cs` — blob `d32a236b1f34bb7796292624e68528f8ce74a0e4`
- `TimescaleHistorianInfrastructure.cs` — blob `330639250f7c642ac6c86ea64727405cc34b16f8`
- failing communication-binding test — blob `7051b8eaa690abb564169ba493018489677dd13a`
- `PostgreSqlConcurrentInitializationTests.cs` — blob `b66a4300e5cd531f36f9a25a0b0d00552e69bba1`

Do not revert or redesign PR #340 product work.

## 3. Relationship to INFRA-CI-01B

INFRA-CI-01B previously corrected the same failure family after CI #1563:
- shared schema key `4993446713136202561`;
- explicit completed lock command before DDL for the known PostgreSQL creators;
- strengthened same-assembly concurrency regression;
- broad Backend gate later became green.

01B explicitly required the invariant to be stronger than blind retry.

The recurrence on exact post-FC0A run `36044280802` proves that the invariant/regression is still incomplete under the current full parallel solution-test load.

Do **not** classify this as a harmless flake merely because an unchanged-SHA rerun might become green.

## 4. Current source audit

Main re-audited the exact merged tree.

Current `src/Scada.Persistence.PostgreSql` shared-schema initializers:
- Engineering Project;
- Audit;
- Local Identity;
- Server Memory;
- Authority Policy;
- Authority Lifecycle;
- Runtime Session Lease;
- Alarm History;
- Operational Event History.

Current Timescale infrastructure:
- `TimescaleHistorianInfrastructure`;
- `TimescaleDbHistorian`;
- `TimescaleDbHistorianRetentionDownsamplingStore`;
- `TimescaleHistoricalQueryProvider`.

At the audited exact SHA, the known production creators use common key
`4993446713136202561` and the known paths acquire their lock before shared-schema DDL.

No direct use of `TimescaleHistorianSchema.RawInfrastructureSql` outside
`TimescaleHistorianInfrastructure` was found.

Therefore the old “one obvious mismatched creator” diagnosis is **not sufficient** for 01C.
The executor must reproduce and identify the actual remaining race boundary.

## 5. Mandatory RED / diagnosis

Do not start by adding retries or changing CI parallelism.

Required RED must exercise a genuinely fresh database/schema and reproduce or deterministically discriminate the cross-project initialization boundary.

Preferred approach:
1. create a disposable fresh PostgreSQL database (or an equivalent isolated fresh database boundary) using the existing CI PostgreSQL service;
2. start the full set of shared-schema initializers concurrently, including both:
   - all nine `Scada.Persistence.PostgreSql` creators; and
   - Timescale historian infrastructure;
3. repeat enough times to make a remaining ordering defect deterministic or strongly reproducible;
4. capture which session/path issues the conflicting schema DDL and what lock state/order it has;
5. preserve the failing natural run `36044280802` as additional RED evidence.

If a fresh-database harness needs a project reference between test assemblies, keep that change test-only and bounded.

Alternative RED is acceptable only if it proves the exact remaining serialization defect rather than manufacturing failure by removing locks.

## 6. Root-cause requirement

Before production mutation, return internally to the same CODEX session with the concrete root cause.

Investigate at least:
- cross-assembly solution-test parallelism;
- transaction advisory lock vs Timescale session advisory lock behavior in the actual connection/database used by tests;
- any initializer launched from constructors/background tasks;
- any current Wave 15 initializer outside the historical 01B inventory;
- first-creation visibility/transaction boundary;
- pooled connection/session-lock lifecycle;
- whether current concurrency regressions accidentally begin after another test has already created the schema.

The final correction must explain why `CREATE SCHEMA IF NOT EXISTS` could collide despite the intended common lock.

## 7. Authorized production scope

Only files required by proven root cause may change.

Expected candidates, not an allowlist obligation:
- `src/Scada.Persistence.PostgreSql/PostgreSqlSharedSchemaInitialization.cs`;
- specific PostgreSQL initializer(s) proven defective;
- `src/Scada.Historian.TimescaleDb/TimescaleHistorianInfrastructure.cs` only if evidence proves the cross-project boundary is defective.

Tests may include:
- `tests/Scada.Persistence.PostgreSql.Tests/PostgreSqlConcurrentInitializationTests.cs`;
- a Timescale concurrency test;
- another directly related integration test project if needed to own the cross-project fresh-database regression.

No product UX, Foundation semantics, Driver behavior, Authority semantics, licensing semantics, renderer contract or Script behavior is authorized.

## 8. Forbidden shortcuts

Do not:
- blindly rerun CI as acceptance;
- change CI/test parallelism merely to serialize around the defect;
- add arbitrary sleeps;
- weaken/remove concurrency tests;
- treat retry as the primary serialization mechanism;
- catch and ignore arbitrary PostgreSQL errors;
- change the shared lock key without a new Main architecture decision;
- touch FC0-A product code;
- self-merge;
- declare FC0A release/freeze.

A narrow defensive retry may exist only if root-cause evidence proves PostgreSQL can still emit a documented benign catalog race after correct serialization and the invariant remains protected by a deterministic lock. That is not currently established.

## 9. Acceptance

Candidate PASS requires:

1. exact base remains `d975174a...`;
2. deterministic/reproducible RED for the remaining race boundary;
3. root cause documented;
4. smallest generic fix;
5. fresh-database cross-project concurrency regression green repeatedly;
6. existing `PostgreSqlConcurrentInitializationTests` green;
7. exact failing communication-binding test green;
8. Timescale concurrency tests green;
9. full local .NET test pass where environment permits;
10. natural Wave 15 T1 green on exact candidate;
11. PR to `wave15/corrections-integration` with bounded diff;
12. Main review before merge;
13. exact post-merge `EliteSCADA CI` green including Backend build/test/smoke and Chromium.

Only then can Main resume the FC0-A release audit.

## 10. Return

Return exactly:

`INFRA-CI-01C CODEX -> MAIN COORDINATOR — CANDIDATE HANDOFF`

Include:
- exact base -> candidate SHA/tree;
- root cause;
- RED evidence;
- changed files;
- before/after synchronization model;
- focused and stress/concurrency results;
- full local test results;
- exact natural T1 run/jobs;
- explicit non-actions;
- no merge/freeze/release.

## 11. Downstream state

Until this lane is closed:
- FC0-A state remains `POST_MERGE_VALIDATION_BLOCKED_BY_INFRA`;
- do not record `FC0A_RELEASE_APPROVED`;
- all six prepared DEV/FND lanes remain WAIT;
- their future exact activation base remains undecided until this generic correction is integrated and the new exact post-merge checkpoint is green.


## 12. Main closure evidence

Candidate:
- PR #341;
- head `8cd0a4efdb1a179678ced4255084a8ad37c8bdf8`;
- tree `80c1ef79602b3d985e04faf4c2db2756c0897130`;
- T1 `36046834536` — SUCCESS.

Protected merge:
- merge SHA `da0e64122f1e4d0293e027f45ef95021cd03c1a1`;
- tree `a724e565f11121a43b97bf0c59683b414c72f48c`.

Exact broad post-merge `EliteSCADA CI 36047274028`:
- Web build — SUCCESS;
- Backend build — SUCCESS;
- Backend Test — SUCCESS;
- Runtime smoke — SUCCESS.

This directly closes the PostgreSQL `23505 / pg_namespace_nspname_index` recurrence that created 01C.

The broad Chromium job failed for a separate deterministic test-load defect in the FC0-A-added `contextual-help-routing.spec.ts`: Node-side Playwright loading of `AppNavigation.tsx` reaches CSS and throws before test execution.

Classification:

`INFRA-CI-01C -> MERGED / POST_MERGE_BACKEND_GREEN / CLOSED_FOR_MUTATION`

No more PostgreSQL/01C mutation is authorized.

Global FC0-A release remains blocked by:
`coord/w15-fnd06-control:docs/WAVE15-FC0A-POSTMERGE-HELP-E2E-LOAD-CONTROL.md`
order `FC0A-POSTMERGE-HELP-E2E-LOAD-V1`.
