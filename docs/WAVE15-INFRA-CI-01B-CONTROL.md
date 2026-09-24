# Wave 15 — INFRA-CI-01B Shared PostgreSQL Schema Initialization Control

> GitHub live is the sole authority.
> This is a bounded generic infrastructure correction discovered by the exact FND-06 post-merge gate.

`CONTROL_BRANCH: coord/w15-infra-ci-01b-control`

`MAIN_ORDER_REV: 0004`

`STATE: MERGED / MAIN_ACCEPTED / BROAD_BACKEND_GREEN / FINAL_GLOBAL_GREEN_BLOCKED_BY_FND06_TEST_FIXTURE`

`ORDER_ID: INFRA-CI-01B-NO-MUTATION-V4`

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

## 4A. Scope amendment after candidate 97c665c8

Intermediate candidate:

`97c665c8e4d62268336dfdef400f992c2f9d43cf`

Natural Wave 15 T1:

`35943407678` — SUCCESS under `FOUNDATION_LIFECYCLE`.

That focused T1 is necessary but not sufficient. The executor correctly ran a fuller local .NET test pass against fresh TimescaleDB and obtained:

- 120 passed;
- 1 failed;
- strengthened `PostgreSqlConcurrentInitializationTests.SharedSchemaStores_InitializeConcurrentlyWithoutDdlCollisions` reproduced PostgreSQL `23505`;
- failing command observed in `PostgreSqlAuditStore.InitializeAsync`.

The strengthened RED exposed that V1's creator inventory was incomplete.

Main re-audited the complete `src/Scada.Persistence.PostgreSql` directory at exact base `624f2eca...`.

All production creators of `CREATE SCHEMA IF NOT EXISTS elitescada` in this assembly:

1. `PostgreSqlAlarmHistoryStore`
2. `PostgreSqlAuditStore`
3. `PostgreSqlAuthorityLifecycleStore`
4. `PostgreSqlAuthorityPolicyStore`
5. `PostgreSqlEngineeringProjectStore`
6. `PostgreSqlLocalIdentityStore`
7. `PostgreSqlOperationalEventHistoryStore`
8. `PostgreSqlRuntimeSessionLeaseStore`
9. `PostgreSqlServerMemoryRetentionStore`

Plus the Timescale historian infrastructure in its separate project.

Initialization sequencing at exact base:

Already explicit lock-before-DDL:
- Alarm History;
- Operational Event;
- Timescale historian.

V1 candidate corrects:
- Audit;
- Engineering Project;
- Local Identity;
- Server Memory.

Additional residual creators discovered by full-test RED:

### PostgreSqlAuthorityPolicyStore

`InitializeAsync` executes `CREATE SCHEMA IF NOT EXISTS elitescada` with **no shared DDL advisory lock**.

Its policy mutation lock is a separate semantic concern and does not satisfy shared schema initialization serialization.

Required bounded correction:
- start an initialization transaction;
- acquire shared DDL advisory lock `4993446713136202561` explicitly as a completed command;
- execute the existing schema/table/migration DDL unchanged;
- load the snapshot consistently;
- do not change policy mutation/concurrency semantics.

### PostgreSqlAuthorityLifecycleStore

Uses shared DDL key `4993446713136202561`, but currently sends:

`SELECT pg_advisory_xact_lock(@ddl_lock);`
and `CREATE SCHEMA ...`

inside the same multi-statement SQL batch.

Required bounded correction:
- remove DDL-lock acquisition from the DDL batch;
- acquire the shared lock explicitly before the existing DDL command;
- do not change Authority lifecycle states, epochs, mutation locks, transitions or fail-closed behavior.

### PostgreSqlRuntimeSessionLeaseStore

Uses shared DDL key `4993446713136202561`, but also retains lock+DDL in the same SQL batch.

Required bounded correction:
- explicit shared DDL lock command before existing DDL;
- no change to lease authority revision, admission, session class, quotas, generations, fencing or licensing semantics.

Classification:

`SAME_GENERIC_SHARED_SCHEMA_INFRASTRUCTURE_INVARIANT / SCOPE_AMENDMENT APPROVED`

This is **not** permission for Authority or Licensing feature changes. Only database-initialization sequencing and directly discriminating concurrency tests are authorized.

Intermediate candidate `97c665c8...` remains useful evidence but is **not candidate-ready**.

## 5. CURRENT EXECUTOR ORDER

`ORDER_ID: INFRA-CI-01B-NO-MUTATION-V4`

`ORDER_STATE: HOLD / NO_MUTATION`

`EXECUTOR_MODE: NO_MUTATION`

`FINAL_CANDIDATE: 6f835bd8a084c0952e93f14c7c90bfffd64a3c71`

`MERGE_SHA: eb4563cf0060449b479c4335ef30a19ed65e35ab`

`MERGE_TREE: 0158aa1b6082a8f9e514f6e6a059f49312b07f65`

`POST_MERGE_CI_RUN: 35944510920 / EliteSCADA CI #1564`

Main disposition:

- INFRA-CI-01B production correction is accepted.
- Exact run #1564 Backend build/test/smoke is SUCCESS, directly closing the PostgreSQL failure that triggered this lane.
- Chromium failed for a separate FND-06 E2E fixture leak: temporary `fnd06-legacy-screen-*` remained after an upsert-only restore.
- No additional PostgreSQL/infra mutation is authorized.
- Final INFRA-CI-01B closure label remains pending only until the next exact broad integration run is globally green after the FND-06 test-only fix.

On `SIGA`, do not mutate INFRA-CI-01B. Follow Main's newer shared-CODEX routing to FND-06 E2E fixture isolation.

## 5A. Broad run #1564 causality split

Exact broad run `35944510920`:
- Web — SUCCESS;
- Backend build/test/smoke — SUCCESS;
- Chromium — FAILURE, 636 passed / 1 failed.

The sole Chromium failure is `runtime.spec.ts` observing an extra FND-06 test fixture Screen. This is unrelated to the PostgreSQL sequencing changes in PR #338.

Therefore:
- PostgreSQL correction evidence = PASS;
- global broad-run gate = still red;
- no infra rerun or infra code change is justified;
- next correction owner = FND-06 test-only fixture isolation.

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
- `src/Scada.Persistence.PostgreSql/PostgreSqlAuthorityPolicyStore.cs`
- `src/Scada.Persistence.PostgreSql/PostgreSqlAuthorityLifecycleStore.cs`
- `src/Scada.Persistence.PostgreSql/PostgreSqlRuntimeSessionLeaseStore.cs`
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
13. all nine `Scada.Persistence.PostgreSql` shared-schema creators are either already explicit lock-before-DDL or corrected by this candidate, with no unlocked/same-batch residual creator;
14. only then may Main complete the pending FND-06 freeze and activate the post-FND06 FC0-A audit.

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
