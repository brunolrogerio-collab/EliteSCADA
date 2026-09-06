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

**Status: COMPLETE / BACKEND + REAL AUTHGATE FLOW + CHROMIUM CONTRACT GREEN / FINAL C25 MATRIX STILL PENDING**

Binding architecture:

`docs/WAVE14-C25-RESTORE-FIRST-ARCHITECTURE.md`

Pre-frontend product authority:

`a7ac6a8a008749e8224e24a59d7f7b1752d02fe2`

Pre-frontend evidence:

- Wave 14 C25 Post-Demo #89 / run `34013048034` — SUCCESS;
- Wave 14 C03 DNP3 Adapter #187 / run `34013048107` — SUCCESS.

#### Authority backup and atomic replacement

Implemented domain remains:

- `elitescada.authority-backup` v1 / `elitescada.authority` v1;
- PBKDF2-SHA256 backup KDF at 310,000 iterations;
- AES-256-GCM authenticated encryption with random salt/nonce;
- user-supplied Authority backup password, separate from login and Engineering Lock;
- no plaintext user passwords;
- preserved password verifier metadata only;
- no sessions/JWTs/cookies/realtime grants/license/trust/application content in Authority backup;
- malformed/corrupt/wrong-password backup fails before Authority mutation;
- `ReplaceAllAsync` / `TryReplaceAllIfEmptyAsync` complete-replacement semantics;
- PostgreSQL replacement under one transaction/serialization boundary;
- anonymous bootstrap restore constrained by `InitialInstallationGate` true-empty admission.

Relevant files include:

- `src/Scada.Security/Authentication/AuthorityBackupService.cs`;
- `src/Scada.Security/Authentication/AuthorityRestoreSecurity.cs`;
- `src/Scada.Api/Security/AuthorityBackupApi.cs`;
- `src/Scada.Security/Authentication/LocalIdentityStore.cs`;
- `src/Scada.Persistence.PostgreSql/PostgreSqlLocalIdentityStore.cs`;
- `src/Scada.Api/Security/InitialInstallationGate.cs`.

#### Prospective restored-Administrator admission

`src/Scada.Api/ProjectPackages/SystemRecoveryAuthorityAdmission.cs` evaluates the restored identity against the SecurityRoles of the application package that will become authoritative.

Admission requires:

- enabled identity;
- bootstrap Authority role anchor (`LocalIdentityBootstrapService.InitialAdministratorRole`, currently `developer`);
- `EngineeringModify` granted by prospective package `SecurityRoles`;
- `UserRoleAdmin` or `SystemAdmin` granted by prospective package `SecurityRoles`.

This remains deliberately stronger than role-name-only admission.

#### Application recovery coordinator

`SystemRecoveryApplicationService` / endpoints retain the canonical sequence:

1. Inspect/Preview canonical `.escadapkg`;
2. require empty persisted project catalog for first-project recovery;
3. validate configured `EngineeringRuntime:ProjectKey` against package key;
4. prospectively validate current/restored Administrator against package policy;
5. acquire mutation gates and snapshot current Working/assets;
6. replace Working and re-preview as true replacement;
7. Apply canonical package;
8. restore previous Working on any pre-durable failure;
9. Save restored package as root revision;
10. Publish;
11. Activate through normal Active authority;
12. report post-Save Publish/Activate failures as explicit partial recovery rather than fake rollback to an empty installation.

#### Restore-first frontend closure

C25.4 frontend commits:

- `e1efb31cad6ef5e0bb4a5e76cff5d4906378bf94` — wire `RestoreFirstPanel` into the real `AuthGate` state machine;
- `a2a32c6227d1231d7b71230076a2b54ecef0f3f6` — add Restore-first Playwright to the existing C25 Chromium gate without removing Engineering Lock coverage;
- `06948450c365009531d584b8b9d1d45e05c1aec8` — correct the Restore-first test to use Playwright's real `postDataBuffer()` request-body API while retaining the package-body assertion.

The real frontend state machine now provides:

- true-empty first-run: `Restaurar backup` OR `Criar Administrador`;
- Authority restore before application mutation;
- no anonymous application Apply after Authority restore;
- application/license `File` selections retained only in React memory across Authority restore -> real login -> application step;
- Authority backup password and Authority file are not retained by the parent AuthGate and are cleared by the Restore panel before handoff;
- restored user must authenticate through the normal login path before application recovery continues;
- authenticated local Administrator with empty persisted catalog: `Restaurar backup` OR `Criar novo projeto`;
- optional license failure remains explicit and non-blocking for an otherwise successful core application recovery;
- DB/Historian recovery remains separate.

#### Diagnosed browser red before closure

First combined browser candidate at product SHA `a2a32c6227d1231d7b71230076a2b54ecef0f3f6` produced:

- Wave 14 C25 Post-Demo #95 / `34016167369`;
- backend job `101440204541` — SUCCESS;
- web job `101440204615` — FAILURE;
- browser result: 6/7 tests passed; only the bootstrap Restore-first test failed.

The failure was diagnosed before any rerun. The route mock used nonexistent Playwright API `route.request().body()`, throwing `TypeError: ...body is not a function` before the mocked Preview response could be fulfilled. This was a test-harness defect, not a product failure. No blind rerun occurred and no product assertion was weakened.

Commit `06948450...` replaced only that invalid body read with a positive-length `postDataBuffer()` assertion.

#### Exact C25.4 closing evidence

Exact validated product SHA:

`06948450c365009531d584b8b9d1d45e05c1aec8`

Wave 14 C25 Post-Demo #97 / run `34016347754` — **SUCCESS**:

- C25 security backend contract job `101440680405` — SUCCESS;
- Engineering Lock web contract job `101440680322` — SUCCESS;
- React/Vite build — SUCCESS;
- combined Chromium Engineering Lock + Restore-first browser tests — SUCCESS;
- Authority backup, System Recovery and atomic Authority replacement focused tests — SUCCESS.

Wave 14 C03 DNP3 Adapter #191 / run `34016347737` — **SUCCESS** across managed tests, Linux/Windows native host, real OpenDNP3↔dnp3py L3 interop and Windows commercial publish dependency gate.

C25.4 is therefore COMPLETE. This is checkpoint evidence, not final C25 acceptance.

### C25.5 — Runtime session UX

**Status: IN PROGRESS / ARCHITECTURE REVALIDATION NEXT**

C25.5 begins only after the exact C25.4 evidence above. No C25.5 product behavior is claimed complete by this ledger update.

Binding requirements remain:

- current authenticated identity visible through the existing system-owned session control;
- `Trocar usuário` and `Sair` remain outside authored `.escadapkg` HMI content;
- logout must invalidate the server session before clearing client authority and must fail visibly if invalidation fails;
- switch-user invalidates the old server session first, removes old client authority immediately and keeps Runtime non-interactive while the next identity is unresolved;
- authentication then reloads backend profile/effective capabilities;
- stale cached frontend authority must not survive switch;
- displayed identity, backend authorization and audit attribution must agree;
- Runtime-only users never gain Engineering/Diagnostics/Licensing/Audit;
- session control remains reachable in Runtime fullscreen.

First C25.5 action is live inspection of `AuthGate`, `UserSessionMenu`, `UserSessionMenuView`, AppNavigation/auth capability projection and Runtime fullscreen integration before mutation.

### C25.6 — Contextual/manual product integration

**Status: NOT STARTED**

### C25.7 — Integrated regression/audit pass

**Status: NOT STARTED**

### C25.8 — Exact final candidate matrix and acceptance

**Status: NOT STARTED**

Final acceptance requires one exact final candidate SHA, the required product matrix, diagnosed reds before rerun, then merge only into `wave14/corrections-integration` and post-merge exact-SHA revalidation.

## 5. Current governance at C25.4 closure

Immediately before this documentation-only checkpoint update:

- #283 remained OPEN / DRAFT / merged=false / base `wave14/corrections-integration`, with exact validated product head `06948450c365009531d584b8b9d1d45e05c1aec8`;
- C25 remains ACTIVE / NOT ACCEPTED / NOT INTEGRATED;
- #212 remains OPEN/DRAFT and has no authorization to merge into `main`;
- C11 remains preserved at `41d24d89c3b9d2b881215255e44023fabde262f3`;
- #266 remains validation-only / NEVER MERGE;
- no `main`, integration or C11 mutation occurred while closing C25.4.

This ledger update is coordination documentation only and does not redefine the exact validated C25.4 product SHA above.

## 6. Resume protocol

On a new coordinator/chat session:

1. fetch issue #282 and PR #283;
2. revalidate #212, #263 and #266;
3. fetch current C25 branch HEAD and distinguish documentation-only HEAD from the latest validated product SHA;
4. read `docs/WAVE14-C25-COORDINATOR-HANDOFF-2026-09-06.md` and this execution ledger;
5. inspect current exact-SHA workflows and any later durable checkpoint;
6. continue C25.5 Runtime session UX unless a later checkpoint supersedes this record;
7. do not sync C11 until C25 is fully accepted, integrated into `wave14/corrections-integration` and post-merge revalidated.

Never reconstruct project state from chat memory when live GitHub can be checked directly.
