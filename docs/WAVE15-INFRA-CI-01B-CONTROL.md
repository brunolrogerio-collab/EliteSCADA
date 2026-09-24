# Wave 15 — INFRA-CI-01B Shared PostgreSQL Schema Initialization Control

> GitHub live is the sole authority.
> This is a bounded generic infrastructure correction discovered by the exact FND-06 post-merge gate.

`CONTROL_BRANCH: coord/w15-infra-ci-01b-control`

`MAIN_ORDER_REV: 0001`

`STATE: ACTIVE`

`ORDER_ID: INFRA-CI-01B-POSTGRES-SCHEMA-LOCK-V1`

`EXECUTOR_IDENTITY: SAME SEQUENTIAL CODEX LANE USED FOR PRIOR FOUNDATION/FND-06`

`EXACT_BASE_SHA: 624f2eca456310a2c6156538b3616a06e3be075f`

`EXACT_BASE_TREE: fb864fb954b0123e69db379cd6b3120349b43600`

`WORK_BRANCH: work/w15-infra-ci-01b-postgresql-schema-init`

`TARGET_BRANCH: wave15/corrections-integration`

`VALIDATION_PROFILE: FOUNDATION_LIFECYCLE`

## 1. Trigger / blocking evidence

FND-06 PR #337 merged cleanly at exact SHA:

`624f2eca456310a2c6156538b3616a06e3be075f`

Candidate T1 `35939646387` was SUCCESS.

Exact natural broad post-merge CI:
- run `35940661531` / EliteSCADA CI #1563;
- Web job `107447605542` — SUCCESS;
- Backend build/test/smoke `107447605830` — FAILURE in Test;
- Chromium `107447946636` — skipped downstream.

Only identified failing test:
`Scada.Persistence.PostgreSql.Tests.PostgreSqlVisualDynamicPersistenceTests.RevisionPersistence_PreservesVisualExpressionConditionAndAnalogFill`

Exact error:
`Npgsql.PostgresException 23505: duplicate key value violates unique constraint pg_namespace_nspname_index`

Stack:
`PostgreSqlEngineeringProjectStore.InitializeAsync`
while executing shared-schema initialization.

Database log confirms concurrent:
`CREATE SCHEMA IF NOT EXISTS elitescada`

## 2. Non-causality to FND-06

The failing production/test files are byte-identical between the pre-FND06 product checkpoint and the FND-06 merge:

- `src/Scada.Persistence.PostgreSql/PostgreSqlEngineeringProjectStore.cs`
  blob `739d4bf252df6fee0f09c332eeefa42bc9707f84`
- `tests/Scada.Persistence.PostgreSql.Tests/PostgreSqlVisualDynamicPersistenceTests.cs`
  blob `69b8573f60b26416bba0ecb3d6a9eebc6839f981`

PR #337 changed only visual/editor/runtime files.

Classification:
`GENERIC_INFRASTRUCTURE_BLOCKER / NOT_FND06_CAUSAL`

Do not reopen FND-06 product architecture.

## 3. Historical evidence

Wave 14 already observed the same PostgreSQL catalog signature:
- EliteSCADA CI #1470;
- `23505 / pg_namespace_nspname_index`;
- concurrent `CREATE SCHEMA IF NOT EXISTS elitescada`.

Wave 14 fixed one real mismatch:
`PostgreSqlOperationalEventHistoryStore` moved from lock `4993446713136202562` to the shared infrastructure lock `4993446713136202561`.

That correction was valid and must remain.

The recurrence at run `35940661531` proves the shared-schema initialization guarantee is still not robust enough under the current full parallel test load.

## 4. Exact source diagnosis

All known production creators of schema `elitescada` were re-audited.

Already explicit separate lock-before-DDL flows:
- `TimescaleHistorianInfrastructure` — session advisory lock before historian schema DDL;
- `PostgreSqlAlarmHistoryStore` — separate `pg_advisory_xact_lock` command before DDL;
- `PostgreSqlOperationalEventHistoryStore` — separate `pg_advisory_xact_lock` command before DDL.

Residual inline batch pattern:
- `PostgreSqlEngineeringProjectStore`;
- `PostgreSqlAuditStore`;
- `PostgreSqlLocalIdentityStore`;
- `PostgreSqlServerMemoryRetentionStore`.

Those stores currently include:
`SELECT pg_advisory_xact_lock(4993446713136202561);`
inside the same multi-statement SQL command as:
`CREATE SCHEMA IF NOT EXISTS elitescada;`
and subsequent DDL.

`PostgreSqlServerMemoryRetentionStore` additionally retries PostgreSQL `23505 | 42P06 | 42P07`, confirming that residual concurrent DDL collisions are already treated as possible by current code.

The desired Foundation invariant is stronger:
**acquire the shared advisory lock as an explicit completed database command before executing shared-schema DDL; do not depend on retry as the primary serialization mechanism.**

## 5. CURRENT EXECUTOR ORDER

`ORDER_ID: INFRA-CI-01B-POSTGRES-SCHEMA-LOCK-V1`

`ORDER_STATE: ACTIVE`

`EXECUTOR_MODE: BOUNDED_INFRA_CORRECTION`

Mission:

1. On exact base `624f2eca456310a2c6156538b3616a06e3be075f`, prove the residual initialization weakness with a discriminating concurrency regression.
2. Refactor the inline-batch stores so shared advisory lock acquisition is an explicit separate command on the same transaction/connection **before** any shared-schema DDL command.
3. Preserve lock key `4993446713136202561`.
4. Do not change schema, migrations, business semantics, retention semantics, Authority, licensing, FND-06 visual behavior or test thresholds.
5. Prefer one shared internal helper only if it reduces duplication without widening public API; otherwise bounded per-store lock command is acceptable.
6. Do not solve by adding blind sleep/retry loops to every store.
7. Existing ServerMemory retry may remain as defensive fallback, but correctness must not rely on it.
8. Expand the shared-schema concurrency regression to cover the relevant production creators, including at minimum:
   - Engineering project;
   - Audit;
   - Local Identity;
   - Server Memory;
   - Operational Event;
   - Alarm History;
   - Timescale/historian path where feasible in the existing test project/dependency boundary.
9. Run the focused PostgreSQL concurrency/persistence tests repeatedly enough to exercise parallel initialization.
10. Obtain a fresh natural Wave 15 T1 and, after merge, a fresh broad post-merge EliteSCADA CI on the exact integration SHA.

## 6. RED requirement

Use exact base `624f2eca...`.

Do not manufacture a failure by removing locks.

Acceptable RED evidence is one of:
- a deterministic test that forces a waiter to acquire the shared advisory lock before schema DDL and demonstrates the current same-batch path can still expose the catalog race; or
- a bounded stress/concurrency test on the exact base that reproduces the `23505/42P06/42P07` collision; or
- if deterministic reproduction is impractical, the exact natural CI failure `35940661531` plus source-structural proof of same-batch lock+DDL may serve as RED, provided the new regression discriminates the corrected sequencing by implementation structure/behavior.

Do not weaken this requirement into "rerun became green".

## 7. Expected allowlist

Production, only as needed:
- `src/Scada.Persistence.PostgreSql/PostgreSqlEngineeringProjectStore.cs`
- `src/Scada.Persistence.PostgreSql/PostgreSqlAuditStore.cs`
- `src/Scada.Persistence.PostgreSql/PostgreSqlLocalIdentityStore.cs`
- `src/Scada.Persistence.PostgreSql/PostgreSqlServerMemoryRetentionStore.cs`
- optional one new internal shared initialization helper under `src/Scada.Persistence.PostgreSql/`

Tests:
- `tests/Scada.Persistence.PostgreSql.Tests/PostgreSqlConcurrentInitializationTests.cs`
- other directly related PostgreSQL initialization tests only if needed for deterministic proof.

Historian/Alarm/Operational production files should not change unless exact source proof identifies an independent mismatch.

No workflow changes are expected.

## 8. Acceptance

PASS requires:
1. explicit lock command completes before shared-schema DDL in every corrected inline initializer;
2. common key remains `4993446713136202561`;
3. no schema/business behavior changes;
4. no new permissive retries masking arbitrary failures;
5. focused concurrency regression green;
6. failing visual-dynamic persistence test green;
7. existing shared-schema concurrency regression green;
8. full .NET tests green locally/environment permitting;
9. natural T1 green on exact candidate;
10. PR scope bounded;
11. normal merge only after Main review;
12. exact broad post-merge EliteSCADA CI green;
13. only then may Main complete the pending FND-06 freeze and activate the post-FND06 FC0-A audit.

## 9. Return

Return exactly:

`INFRA-CI-01B CODEX EXECUTOR -> MAIN COORDINATOR — CANDIDATE HANDOFF`

Include:
- exact base -> candidate SHA/tree;
- exact changed files;
- RED evidence;
- before/after lock sequencing;
- regression matrix;
- local commands/results;
- natural T1 run/jobs;
- explicit non-actions;
- no self-merge/freeze.

## 10. Forbidden

- blind rerun as substitute for diagnosis;
- modifying FND-06 visual contract;
- changing PostgreSQL schema semantics;
- changing lock key without a new architecture decision;
- broad database refactor;
- weakening/removing concurrency tests;
- changing CI timing/parallelism merely to hide the race;
- self-merge/freeze.
