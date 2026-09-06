# Wave 14 C25 — Post-Demo Consolidated Corrections — Execution Log

**Status:** ACTIVE / NOT ACCEPTED / NOT INTEGRATED  
**Coordinator package:** C25  
**Tracking issue:** #282  
**Implementation PR:** #283  
**Implementation branch:** `wave14/c25-post-demo`  
**Integration target:** `wave14/corrections-integration`  
**Binding product contract:** `docs/WAVE14-C25-CONSOLIDATED-POST-DEMO-CONTRACT.md`  
**Full code audit:** `docs/WAVE14-C25-FULL-CODE-AUDIT.md`  
**Restore-first architecture:** `docs/WAVE14-C25-RESTORE-FIRST-ARCHITECTURE.md`  
**Coordinator handoff:** `docs/WAVE14-C25-COORDINATOR-HANDOFF-2026-09-06.md`  
**Historical Product Owner provenance:** #280, especially comment `5554918787`

> GitHub is the sole project authority. Revalidate live refs, PR state and exact-SHA CI before every decision or mutation. Older detailed ledger versions remain preserved in Git history; this revision is the current resumable authority.

## 1. Permanent governance

- #212 remains OPEN/DRAFT and must not merge to `main` without later explicit Product Owner authorization.
- Never alter `main` directly.
- C25 may integrate only into `wave14/corrections-integration` after exact-SHA acceptance.
- C11 #263 remains preserved until C25 is accepted, integrated and post-merge revalidated.
- #266 remains validation-only and MUST NEVER MERGE.
- No force-push, destructive rebase, branch deletion or unrelated cleanup.
- Diagnose every CI red before rerun; no blind reruns.
- Never weaken tests, validation, authentication, authorization, licensing, lifecycle, package or Runtime authority for green CI.
- Backend Active revision remains Runtime application authority.
- Alarm / Operational Event / Audit remain distinct.
- No EEE-specific workaround for a generic product gap.
- Wave13 #205/#207 remains paused.

## 2. Accepted baseline beneath C25

C25 branch base:

`c2fc96eacc168ea092c2e4d4dcbc79b00faa3155`

Accepted C24 product authority beneath that documentation state:

`40a491c2de2403f2934b8bae647c35072d5c2496`

C24 is formally:

**ACCEPTED / INTEGRATED / POST-MERGE REVALIDATED**

C11 remains intentionally preserved at:

`41d24d89c3b9d2b881215255e44023fabde262f3`

Do not synchronize C25 into C11 yet.

## 3. Preparation convergence

The asynchronous post-DEMO preparation was centralized into C25 before product mutation:

- #273 System Recovery design: CLOSED WITHOUT MERGE; branch preserved.
- #274 Runtime session/manual design: CLOSED WITHOUT MERGE; branch preserved.
- #280 Product Owner decision issue: CLOSED/COMPLETED as decision record only.

Durable preparation commits:

- `b4b53350a447d48d747c2512297ea0809f8bf61b` — establish execution ledger;
- `b32ee0778d646bc65972ac16401c67f78ad8bc2b` — consolidated post-DEMO product contract;
- `3657826b01717553024d0115c10305cf548f5433` — preparation convergence record.

## 4. Checkpoint matrix

### C25.0 — Durable bootstrap + full architecture/code audit

**Status: COMPLETE**

Exact audited product starting SHA:

`3657826b01717553024d0115c10305cf548f5433`

Audit commit:

`063a826f551e840f21ccd1c96d2d4ff2960322c3`

Durable audit:

`docs/WAVE14-C25-FULL-CODE-AUDIT.md`

Key decisions:

- Engineering Lock belongs to canonical application Engineering state, not Authority.
- `.escadapkg` remains application portability, not whole-system encryption/DRM.
- Lock secret, Authority login credentials and Authority backup password are separate secret domains.
- Lock enforcement is an additional backend application-content policy, not a rewrite of Authority capabilities.
- Runtime remains Active-revision authoritative.
- Restore-first must reuse true-empty first-run boundaries rather than create disposable users/projects.
- Runtime session UX reuses existing auth/session primitives.
- contextual Help requires stable language-neutral Help IDs and multilingual installed content.

### C25.1 — Engineering Lock domain/package/security contract

**Status: COMPLETE / FOCUSED VALIDATION GREEN / FINAL C25 MATRIX STILL PENDING**

Principal commits:

- `bc9d1de154fa40ba855847c6e6cc0ac9c145b0cb` — canonical Engineering Lock package contract;
- `f6aa2df9d12301bd0a2c41eb670521620f542520` — verifier/registry service;
- `2140921e7f12f609aa5799eb97d8a59eadcf443f` — verifier/security tests;
- `8ad05422ab68fd5bcc30d67e0e26c791cc2c9b77` — canonical exchange persistence;
- `14bceb8c64569d9479494044d2205dfb55aae436` — package/lifecycle/legacy/partial-import roundtrip tests.

Implemented contract:

- optional lock;
- verifier presence independent from `Locked`;
- PBKDF2-SHA256 one-way verifier with random salt and constant-time verification;
- no plaintext/reversible Lock secret;
- canonical JSON and `.escadapkg` round-trip Lock state;
- package remains unencrypted;
- Import/Export never require Lock secret;
- legacy payload without Lock metadata normalizes safely;
- Working/Published/Active revision persistence carries immutable canonical lock metadata.

### C25.2 — Engineering Lock backend enforcement and restricted authority

**Status: COMPLETE / FOCUSED VALIDATION GREEN / NOT FINAL C25 ACCEPTANCE**

Validated SHA:

`493acedc12079b73c02312a2d8355291664226b4`

Validation:

- Wave 14 C25 Post-Demo run `34005944316` — SUCCESS.

Implemented behavior:

- Authority authentication/capability remains first admission boundary;
- Lock is separate application-content policy and does not strip/rewrite `EngineeringModify`;
- protected Workspace Engineering reads/mutations fail closed while locked;
- Save/Publish/Activate and protected lifecycle content blocked while locked;
- Authority administration, Licensing, Audit, Diagnostics, Import/Export, solution replacement/restore and explicit Lock management retain their own authorities;
- management API exposes only safe Lock state, never verifier material;
- wrong unlock secret remains locked and is audited without secret leakage;
- Active activation/restart rehydrates Lock from durable Active authority.

Diagnosed historical red:

- C03 run `34005773590` failed because an unintended whole-file `Program.cs` replacement referenced symbols absent from the canonical base.
- No blind rerun occurred.
- `Program.cs` was restored by exact original blob in `992d6fb0e9a83d3ddbd30f7d549958412280e48f`.
- No net C25 `Program.cs` product diff remains from that incident.

### C25.3 — Engineering Lock UI/lifecycle/package roundtrip

**Status: COMPLETE / FOCUSED BACKEND + WEB VALIDATION GREEN / NOT FINAL C25 ACCEPTANCE**

Exact validated product SHA:

`a5fb9959fa15c060f51417de7bea84a5eec11e5b`

Principal commits:

- `c12e16e3b1295bcc7e2fd8f08c474ce60578b07e` — serialize Lock changes as Working mutations;
- `9f301bfb6fae2943df5b73a8e63b16bbe31781e4` — lifecycle/context regressions;
- `fdf601b01864d583b06a6868343a3c15ffc54ee9` — locked Import/Export concurrency context;
- `040c9cccbc3d8ff000b6450b031f14f0ad467703` — frontend Lock client;
- `a19418309cb6e1c2e2dcd30848024838b122d907` — restricted Engineering surface;
- `5cdb944c51c9383d91781c0f0caee37155d24504` — restricted presentation;
- `302e5242f66aabedffbf3caa927ff4dad2363f56` — backend-state Lock gate in application routing;
- `46b8ae9c495ad39f44ee70702e4bd8b7b6786bef` — focused Chromium tests;
- `4f1e64f8e2fa0f76e4fb13a5b379a12a589b80de` — C25 workflow web validation;
- `a5fb9959fa15c060f51417de7bea84a5eec11e5b` — scope ambiguous Playwright assertion.

Exact C25.3 validation:

- Wave 14 C25 Post-Demo run `34007585252` — SUCCESS;
- Wave 14 C03 DNP3 Adapter run `34007585312` — SUCCESS.

### C25.4 — Restore-first bootstrap / System Recovery

**Status: IN PROGRESS / BACKEND IMPLEMENTED AND FOCUSED GREEN / FRONTEND FLOW NOT YET WIRED OR CI-PROVEN**

Binding architecture:

`docs/WAVE14-C25-RESTORE-FIRST-ARCHITECTURE.md`

Product HEAD revalidated before coordinator handoff documentation:

`a7ac6a8a008749e8224e24a59d7f7b1752d02fe2`

Exact-head workflow evidence:

- Wave 14 C25 Post-Demo #89 / run `34013048034` — SUCCESS;
- Wave 14 C03 DNP3 Adapter #187 / run `34013048107` — SUCCESS.

#### Implemented Authority backup domain

`AuthorityBackupService` now implements:

- format `elitescada.authority-backup`, version 1;
- payload schema `elitescada.authority`, version 1;
- PBKDF2-SHA256 backup KDF at 310,000 iterations;
- AES-256-GCM authenticated encryption;
- random salt/nonce;
- user-supplied Authority backup password, minimum 12 characters;
- password verifier metadata only for restored login credentials;
- no plaintext passwords, sessions, JWTs, cookies, realtime grants, license/trust or application content;
- wrong password/corruption/malformed envelope fails before mutation.

Authority backup/restore files include:

- `src/Scada.Security/Authentication/AuthorityBackupService.cs`;
- `src/Scada.Security/Authentication/AuthorityRestoreSecurity.cs`;
- `src/Scada.Api/Security/AuthorityBackupApi.cs`.

#### Atomic Authority replacement

Store contract now contains complete replacement primitives:

- `ReplaceAllAsync`;
- `TryReplaceAllIfEmptyAsync`.

Implementations:

- in-memory replacement validates/builds complete replacement before state swap;
- PostgreSQL replacement uses one transactional/serialization boundary;
- secure true-empty restore uses `InitialInstallationGate` and rechecks first-run eligibility.

Relevant files:

- `src/Scada.Security/Authentication/LocalIdentityStore.cs`;
- `src/Scada.Persistence.PostgreSql/PostgreSqlLocalIdentityStore.cs`;
- `src/Scada.Api/Security/InitialInstallationGate.cs`;
- `src/Scada.Api/Security/LocalIdentityConfiguration.cs`;
- `src/Scada.Api/Security/LocalIdentityApi.cs`.

#### Prospective Authority admission

`src/Scada.Api/ProjectPackages/SystemRecoveryAuthorityAdmission.cs` evaluates the restored Administrator against the package being recovered.

Admission requires:

- enabled identity;
- bootstrap Authority role anchor (`LocalIdentityBootstrapService.InitialAdministratorRole`, currently `developer`);
- `EngineeringModify` granted by prospective package `SecurityRoles`;
- `UserRoleAdmin` or `SystemAdmin` granted by prospective package `SecurityRoles`.

Do not regress this to role-name-only authorization.

#### Application recovery coordinator

Files:

- `src/Scada.Api/ProjectPackages/SystemRecoveryApplicationService.cs`;
- `src/Scada.Api/ProjectPackages/SystemRecoveryApplicationEndpoints.cs`;
- mapping through `ProjectPackageEndpoints.cs`;
- `tests/Scada.Drivers.Tests/SystemRecoveryApplicationServiceTests.cs`.

Implemented sequence:

1. canonical package Inspect/Preview reuse;
2. require empty persisted project catalog for first-project recovery;
3. require configured `EngineeringRuntime:ProjectKey` and package-key match before recovery can be declared complete;
4. prospectively validate restored/current Administrator against package policy;
5. acquire installation/workspace mutation gates;
6. snapshot current Working and visual asset payloads;
7. clear Working and re-preview as true replacement so references cannot resolve through old/demo state;
8. Apply canonical package;
9. before durable Save, any failure restores previous Working state;
10. Save restored package as root Engineering revision;
11. Publish through normal validation;
12. Activate through existing published-runtime authority;
13. after durable Save, later failure is reported as explicit partial state instead of pretending rollback to an empty installation.

#### Restore-first component and intended browser contract

Frontend component exists:

`web/scada-web/src/auth/RestoreFirstPanel.tsx`

It supports:

- bootstrap Authority + application package validation;
- Authority restore first;
- application-only recovery after real login;
- optional license file and existing `/api/licensing/install` endpoint;
- non-blocking optional-license failure;
- pt-BR/en/es;
- explicit DB/Historian separation.

Playwright spec exists:

`web/scada-web/tests-e2e/wave-14-c25-restore-first.spec.ts`

It specifies:

- Restore backup before disposable Administrator creation;
- no anonymous application Apply after Authority restore;
- restored real Administrator login before application recovery;
- no disposable project required;
- optional license failure does not invalidate core recovery.

#### Critical C25.4 gap discovered during coordinator handoff audit

C25.4 MUST NOT be marked complete yet.

Exact `a7ac6a8a...` static revalidation found:

1. `web/scada-web/src/auth/AuthGate.tsx` does not currently import/render `RestoreFirstPanel`, does not expose `Restaurar backup` on true-empty first-run, and still forces the old first-project form for authenticated users with no persisted project.
2. `.github/workflows/wave14-c25-post-demo.yml` builds React/Vite but runs only `wave-14-c25-engineering-lock.spec.ts`; it does not execute `wave-14-c25-restore-first.spec.ts`.

Therefore:

- current green run `34013048034` proves backend System Recovery focused tests and frontend build compatibility;
- it does **not** prove the Restore-first browser UX;
- the existing Restore-first Playwright file would not be considered current CI evidence until wired into the workflow;
- do not weaken/skip the browser test to obtain green.

#### Exact next C25.4 work

1. revalidate live #283 HEAD before mutation;
2. wire `RestoreFirstPanel` into the real `AuthGate` state machine;
3. true-empty first-run must offer `Restaurar backup` OR `Criar Administrador`;
4. after Authority restore, require real restored login before application Apply;
5. preserve selected application/license files across Authority restore -> login -> application step without persisting backup password or other secret material;
6. authenticated local Administrator with empty catalog must see `Restaurar backup` OR `Criar novo projeto`;
7. extend C25 workflow to run `wave-14-c25-restore-first.spec.ts` in Chromium in addition to Engineering Lock web tests;
8. inspect exact-SHA C25 + C03 results, diagnose any red before rerun;
9. only then close C25.4 in ledger/#282/#283.

### C25.5 — Runtime session UX

**Status: NOT STARTED**

Do not start until C25.4 is genuinely browser-wired and checkpointed.

### C25.6 — Contextual/manual product integration

**Status: NOT STARTED**

### C25.7 — Integrated regression/audit pass

**Status: NOT STARTED**

### C25.8 — Exact final candidate matrix and acceptance

**Status: NOT STARTED**

Final acceptance requires one exact final candidate SHA, the required product matrix, diagnosed reds before rerun, then merge only into `wave14/corrections-integration` and post-merge exact-SHA revalidation.

## 5. Current governance revalidation at coordinator handoff

Revalidated immediately before the handoff-only documentation commit:

- #282: OPEN;
- #283: OPEN / DRAFT / merged=false / base `wave14/corrections-integration` / product head `a7ac6a8a008749e8224e24a59d7f7b1752d02fe2`;
- #212: OPEN / DRAFT / merged=false / no authorization to merge into `main`;
- #263: OPEN / DRAFT / C11 head `41d24d89c3b9d2b881215255e44023fabde262f3`;
- #266: OPEN / DRAFT / validation-only / NEVER MERGE / C11 head `41d24d89...`.

No C25 integration, `main` mutation, C11 synchronization or #266 merge occurred during this handoff.

## 6. Resume protocol

On a new coordinator/chat session:

1. fetch issue #282 and PR #283;
2. revalidate #212, #263 and #266;
3. fetch current C25 branch HEAD;
4. read `docs/WAVE14-C25-COORDINATOR-HANDOFF-2026-09-06.md`;
5. read `docs/WAVE14-C25-CONSOLIDATED-POST-DEMO-CONTRACT.md`;
6. read `docs/WAVE14-C25-FULL-CODE-AUDIT.md`;
7. read `docs/WAVE14-C25-RESTORE-FIRST-ARCHITECTURE.md`;
8. read this execution ledger;
9. inspect current exact-SHA workflow evidence;
10. continue C25.4 from the AuthGate/workflow wiring gap unless a later durable checkpoint supersedes this record.

Never reconstruct project state from chat memory when live GitHub can be checked directly.