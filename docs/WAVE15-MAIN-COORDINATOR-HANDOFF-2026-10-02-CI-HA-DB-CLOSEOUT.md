# WAVE 15 — MAIN COORDINATOR HANDOFF — 2026-10-02 — CI SLIMMED / HA+DB FINAL GATE

> **GitHub live is the only authority. Revalidate every SHA/run before acting.**
>
> This document supersedes older current-state handoffs whenever they conflict.

## 1. Current live product integration

At handoff creation:

- branch: `wave15/corrections-integration`;
- HEAD: `9b3e43b36efd2b58247d692270f97dc6d3b5278a`;
- this includes:
  - PR #461 Engineering Lifecycle UX;
  - PR #462 Simulation TAG UX convergence;
  - PR #460 Runtime History UX;
  - PR #464 Runtime Shell UX;
  - PR #465 slim post-merge integration CI;
  - PR #466 slim T1 routing.

Do not rely on older accepted base `015b0d5a...`, `a213477b...`, `ce8bd48e...`, `64ca1d87...` or `fd694ab...` as current product authority.

## 2. CI cleanup already integrated

### PR #465 — post-merge gate

Merged:
`fd694ab77db9f717c30325a68e15b58c6370ed7f`

Wave 15 integration push now:
- keeps full backend build/tests/runtime smoke;
- keeps Web build;
- runs compact Chromium integrated composition smoke only:
  - `wave-03-integrated-composition.spec.ts`;
  - `interface-wave-03-readiness.spec.ts`;
  - `runtime.spec.ts`;
- Chromium runs in parallel rather than waiting for backend + Web;
- full browser catalog remains for main/manual/final acceptance.

### PR #466 — T1 PR routing

Merged:
`9b3e43b36efd2b58247d692270f97dc6d3b5278a`

Validated by T1 #582 / `37076270800`: SUCCESS.

Key changes:
- new `APP_SHELL` and `DATABASE_TOPOLOGY` profiles;
- browser specs route by owning paths rather than broad directory ancestry;
- HA UI no longer pulls entire visual-editor/tag/z-order suite;
- HA backend no longer implies `RUNTIME_RENDERER` merely from `Scada.Api/Runtime`;
- Database Topology no longer inherits INSTALLATION/local-auth from a filename containing `Installation`;
- ordinary product `i18n.ts` no longer implies DOCS_I18N_HELP;
- HA two-process is reserved for HA core/protocol paths;
- .NET restores only owning selected projects;
- Chromium restores only `Scada.Api.csproj`;
- npm + Playwright browser caches added;
- bounded timeouts added.

Do not re-expand either gate without a demonstrated coverage gap.

## 3. HA + Remote DB final convergence — PR #463

PR:
`#463 — W15: converge HA and Remote DB on current integration`

Branch:
`coord/w15-ha-db-convergence`

Current exact candidate after T1 #584 cleanup fix:
`a015741cc0d1c9f49f5a30aee5560db53fa16fcc`

Current PR profile:
`VALIDATION_PROFILE: HA_DISTRIBUTED, DATABASE_TOPOLOGY`

The candidate carries:
- accepted HA-D2A core;
- accepted HA-D2B admin UI;
- accepted DB-A topology/quiesce/migration core;
- accepted DB-B admin UI;
- Main-owned HA Engineering mount;
- Main-owned `/admin/database` SystemAdmin mount;
- Runtime Shell already integrated.

Critical combined rule:
`PostgreSqlRuntimeSessionLeaseStore.RebindActiveClusterLeasesAsync`
must acquire:
`IDurableWriteAdmission("runtime-session")`

This prevents HA lease rebind from bypassing DB migration/cutover quiesce.

### T1 history

- #574: failed stale broad evidence/harness assumptions;
- #577: failed browser evidence; no identified HA/DB core product defect;
- #584 / `37076463027`: focused routing worked; .NET/Web/HA two-process/Common were GREEN; Chromium failed 2 tests.

#584 exact failures:
1. Simulation TAG test cleanup expected JSON import-by-omission to remove a temporary TAG.
   - That assumption was wrong: Engineering JSON import is additive/upsert.
2. following `runtime.spec.ts` then saw polluted Active Runtime and missed `E2E Explicit Demo Fixture`.

Correction on exact current candidate:
- removed fake JSON-import restore;
- cleanup now uses canonical CAS-protected DELETE:
  - delete temporary Simulation TAG;
  - delete temporary Simulation Data Source;
  - save/publish/activate clean Working;
- no product behavior weakened.

Replacement T1:
- #585 / `37077218173`;
- exact HEAD: `a015741cc0d1c9f49f5a30aee5560db53fa16fcc`;
- at handoff observation:
  - classifier SUCCESS;
  - focused jobs queued/running;
- Main stopped watching per standing CI rule.


## 3A. Known post-merge smoke correction — PR #467

Last slim post-merge run on `fd694ab77db9f717c30325a68e15b58c6370ed7f`:
- EliteSCADA CI #1653 / `37075517710`;
- Backend: SUCCESS;
- Web: SUCCESS;
- Chromium integration gate: FAILURE.

Failure was stale test shape, not a product defect:
- `interface-wave-03-readiness.spec.ts` still searched:
  `Runtime views -> link Overview -> aria-current=page`;
- accepted Runtime Shell #464 now uses:
  `.runtime-operator-toolbar -> button Overview -> aria-pressed=true`.

Correction:
- PR #467;
- branch `coord/w15-postmerge-runtime-shell-smoke-fix`;
- exact head `30eb68a78822d7f2c93512012d12b67bb5e0dd7a`;
- T1 #586 / `37077454052` queued/running at bounded observation.

Preferred safe order:
1. integrate #467 after its exact-head T1 is green;
2. revalidate/rebase #463 if the base moved;
3. require #585 or a replacement exact-head T1 green;
4. merge #463;
5. observe the resulting slim post-merge CI.

## 4. Immediate successor action

FIRST action:
1. fetch PR #463 live;
2. fetch T1 #585 final result;
3. verify PR exact head remains `a015741cc0d1c9f49f5a30aee5560db53fa16fcc` or explain any movement.

If #585 is GREEN:
1. revalidate `mergeable=true`;
2. mark #463 ready if still draft;
3. merge exact validated head into `wave15/corrections-integration`;
4. do not alter candidate during merge;
5. identify the resulting merge SHA;
6. observe post-merge `EliteSCADA CI` at most 2 minutes;
7. stop watching and later revalidate final result;
8. require green post-merge before calling HA+DB integrated.

If #585 is RED:
- inspect only failed job(s);
- do not rerun unchanged head hoping for green;
- distinguish test/harness from product;
- do not weaken:
  - HA fencing/election;
  - DB durability/quiesce;
  - Auth/SystemAdmin;
  - Runtime session lease rules.

## 5. Historical Playback next

PR #452 remains:
- exact accepted core head: `408b49a558d9a9fbbbb67797b5939194d5822603`;
- old T1 #462: SUCCESS;
- current PR is stale/non-mergeable against live integration.

The original HOLD reason was waiting for Runtime History UX.
That prerequisite is now integrated through #460, and Runtime Shell is integrated through #464.

Next coordinator should, **after HA+DB is stable**, recompose Playback onto the then-current integration:
- preserve accepted playback safety/query/renderer semantics;
- reconcile entry/controls with the new compact Runtime History + Runtime Shell;
- do not restore old dense History UX;
- do not create a second Historian/query engine/renderer;
- run exact-head focused T1;
- no blind merge of stale #452.

## 6. Remaining Wave 15 closeout after HA+DB + Playback

Order:
1. #379 final pt-BR/en/es product sweep;
2. #425 complete Help/Manual finalization after stable surfaces;
3. #306 productization:
   - EEE Simulation v15;
   - real Modbus variant;
   - final .escadapkg/checksum/provenance/PREVIEW-READY;
4. #300 fresh Preview:
   - CODEX first-time-user pass if capacity/tooling available;
   - Product Owner human pass;
5. final integrated validation / release-candidate decision.

Dynamo R2 artwork remains rejected by Product Owner.
Do not revive rejected PR #451 artwork. Future professional redraw, if pursued, belongs to CODEX/new accepted artwork work from the good baseline.

## 7. DEV / CODEX action matrix at transfer

- Lifecycle UX: merged; no action.
- Simulation TAG UX: merged; no action.
- Runtime History UX: merged; no action.
- Runtime Shell UX: merged; no action.
- HA-D2A / HA-D2B chats: no SIGA; Main owns #463 convergence.
- DB-A / DB-B chats: no SIGA; Main owns #463 convergence.
- Playback DEV: no SIGA until Main recomposes/re-releases it on current base.
- Shared CODEX: no action now.

## 8. Coordination rules to preserve

- GitHub live is authority.
- Product Owner is not a messenger between agents.
- DEV never self-merges.
- exact-head evidence matters.
- CI:
  `LAUNCH -> OBSERVE MAX 2 MIN -> STOP WATCHING -> REVALIDATE FINAL RESULT LATER`.
- do not stare at long tests.
- do not rerun unchanged failures.
- every user-facing status should clearly state which chat needs `SIGA` and which does not.

State:
`CI_SLIMMED / HA_DB_T1_585_ACTIVE / PLAYBACK_NEXT_AFTER_HA_DB / W15_CLOSEOUT_SEQUENCE_FROZEN`.
