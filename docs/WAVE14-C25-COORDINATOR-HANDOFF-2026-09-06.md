# Wave 14 — C25 Coordinator Handoff — 2026-09-06

**Status:** ACTIVE / NOT ACCEPTED / NOT INTEGRATED  
**Repository:** `brunolrogerio-collab/EliteSCADA`  
**C25 branch:** `wave14/c25-post-demo`  
**C25 PR:** #283 -> `wave14/corrections-integration`  
**Tracking issue:** #282  
**Integration PR:** #212 -> `main` (OPEN/DRAFT; no merge authorization)  
**Binding contract:** `docs/WAVE14-C25-CONSOLIDATED-POST-DEMO-CONTRACT.md`  
**Full code audit:** `docs/WAVE14-C25-FULL-CODE-AUDIT.md`  
**Execution ledger:** `docs/WAVE14-C25-POST-DEMO-EXECUTION-LOG.md`  
**Restore-first architecture:** `docs/WAVE14-C25-RESTORE-FIRST-ARCHITECTURE.md`

> GitHub live state is the sole authority. This handoff is navigation and durable coordination state. Revalidate all refs, PR state and exact-SHA CI before any decision or mutation.

## 1. Non-negotiable governance

- PR #212 must remain **OPEN/DRAFT** and MUST NOT merge into `main` without later explicit Product Owner authorization.
- Never alter `main` directly.
- C25 may integrate only into `wave14/corrections-integration` after exact-SHA acceptance.
- Do not synchronize C25 into C11 until C25 is accepted, integrated and post-merge revalidated.
- C11 PR #263 remains OPEN/DRAFT and preserved.
- C11 validation-only PR #266 MUST NEVER MERGE.
- No force push, destructive rebase, branch deletion or unrelated cleanup.
- Diagnose every CI red before rerun. No blind reruns.
- Never weaken validation, authentication, authorization, licensing, lifecycle, package contracts or Runtime authority to obtain green CI.
- Backend Active revision remains Runtime application authority.
- Alarm / Operational Event / Audit remain distinct.
- No EEE-specific workaround for a generic product defect.
- Wave13 #205/#207 remains paused.

## 2. Live state at this handoff

Revalidated immediately before this handoff was written:

- #282: OPEN, C25 umbrella active.
- #283: OPEN / DRAFT / merged=false / base `wave14/corrections-integration`.
- Product HEAD before this handoff-only documentation commit: `a7ac6a8a008749e8224e24a59d7f7b1752d02fe2`.
- #212: OPEN / DRAFT / merged=false / integration HEAD `c2fc96eacc168ea092c2e4d4dcbc79b00faa3155`.
- #263: OPEN / DRAFT / C11 head `41d24d89c3b9d2b881215255e44023fabde262f3`.
- #266: OPEN / DRAFT / validation-only / NEVER MERGE / same C11 head `41d24d89...`.

Exact product HEAD `a7ac6a8a...` had these workflow results:

- Wave 14 C25 Post-Demo #89 / run `34013048034` — **SUCCESS**.
- Wave 14 C03 DNP3 Adapter #187 / run `34013048107` — **SUCCESS**.

Important limitation: the C25 workflow at this SHA builds React/Vite and runs the Engineering Lock Chromium suite, but it does **not** execute `web/scada-web/tests-e2e/wave-14-c25-restore-first.spec.ts`. Therefore the green C25 workflow is not proof of the Restore-first browser flow.

## 3. Accepted baseline beneath C25

C24 is already:

**ACCEPTED / INTEGRATED / POST-MERGE REVALIDATED**

Accepted C24 product merge:

`40a491c2de2403f2934b8bae647c35072d5c2496`

Integration documentation HEAD beneath C25:

`c2fc96eacc168ea092c2e4d4dcbc79b00faa3155`

C11 remains intentionally preserved at `41d24d89...` until C25 is fully accepted and post-merge revalidated.

## 4. C25 checkpoint state

### C25.0 — complete

Full architecture/code audit is durable in:

`docs/WAVE14-C25-FULL-CODE-AUDIT.md`

Audit commit:

`063a826f551e840f21ccd1c96d2d4ff2960322c3`

### C25.1 — complete, focused validation green

Engineering Lock canonical domain/package/security contract implemented.

Key properties:

- application-level lock, separate from Authority and Authority backup credentials;
- configured verifier independent from `locked` state;
- one-way PBKDF2-SHA256 verifier with random salt and constant-time comparison;
- no plaintext/reversible lock secret;
- canonical Engineering JSON and `.escadapkg` preserve lock metadata/state;
- full package remains unencrypted;
- legacy payload without lock metadata normalizes safely;
- Import/Export do not require Engineering Lock password.

Principal commits are recorded in the execution ledger.

### C25.2 — complete, focused validation green

Backend Engineering Lock enforcement implemented and validated.

Validated SHA:

`493acedc12079b73c02312a2d8355291664226b4`

Wave 14 C25 run `34005944316` — SUCCESS.

Authority capabilities remain primary; Lock is an additional application-content boundary and does not strip/rewrite `EngineeringModify`.

### C25.3 — complete, focused backend + web validation green

Exact validated product SHA:

`a5fb9959fa15c060f51417de7bea84a5eec11e5b`

Wave 14 C25 run `34007585252` — SUCCESS.  
Wave 14 C03 run `34007585312` — SUCCESS.

Implemented UI behavior includes backend-authoritative Lock state, restricted Engineering administration surface, unlock/configure/lock/clear lifecycle, package portability under lock, and Chromium coverage for Engineering Lock.

### C25.4 — IN PROGRESS

C25.4 has advanced substantially beyond the older ledger text. The following implementation exists on product HEAD `a7ac6a8a...`:

#### Authority backup / restore

Files include:

- `src/Scada.Security/Authentication/AuthorityBackupService.cs`
- `src/Scada.Security/Authentication/AuthorityRestoreSecurity.cs`
- `src/Scada.Api/Security/AuthorityBackupApi.cs`
- `src/Scada.Api/Security/InitialInstallationGate.cs`
- `src/Scada.Security/Authentication/LocalIdentityStore.cs`
- `src/Scada.Persistence.PostgreSql/PostgreSqlLocalIdentityStore.cs`

Implemented properties:

- versioned `elitescada.authority-backup` v1 envelope;
- PBKDF2-SHA256 backup KDF, 310,000 iterations;
- AES-256-GCM authenticated encryption;
- user-supplied backup password, minimum 12 chars;
- no plaintext user passwords;
- preserved local password verifier salt/hash/iterations;
- no sessions/tokens/cookies/realtime grants or license material in backup;
- malformed/corrupted/wrong-password backup fails before Authority mutation;
- `ReplaceAllAsync` and `TryReplaceAllIfEmptyAsync` provide atomic complete replacement semantics;
- PostgreSQL replacement uses one transaction/serialization boundary;
- bootstrap restore uses the secure true-empty installation gate;
- authenticated Authority Export/Preview/Apply remains separate from anonymous bootstrap restore.

#### Prospective application security admission

Files:

- `src/Scada.Api/ProjectPackages/SystemRecoveryAuthorityAdmission.cs`
- tests in `SystemRecoveryAuthorityAdmissionTests.cs`.

The restored bootstrap administrator must:

1. be enabled;
2. retain the bootstrap Authority role anchor (`developer` / `LocalIdentityBootstrapService.InitialAdministratorRole`);
3. be granted `EngineeringModify` by the **prospective package SecurityRoles**;
4. be granted `UserRoleAdmin` or `SystemAdmin` by that same prospective package policy.

Do not simplify this to role-name-only admission. The package policy evaluation is deliberate.

#### Application recovery coordinator

Files:

- `src/Scada.Api/ProjectPackages/SystemRecoveryApplicationService.cs`
- `src/Scada.Api/ProjectPackages/SystemRecoveryApplicationEndpoints.cs`
- `src/Scada.Api/ProjectPackages/ProjectPackageEndpoints.cs` mapping hook;
- `tests/Scada.Drivers.Tests/SystemRecoveryApplicationServiceTests.cs`.

Implemented sequence:

- canonical package Inspect/Preview reused;
- first-project recovery only while catalog is empty;
- configured `EngineeringRuntime:ProjectKey` must exist and match package project key before recovery can be declared complete;
- current/restored identity is evaluated against prospective package policy;
- Working replacement runs under mutation gate;
- pre-durable failure restores prior in-memory Working using the established checkout rollback pattern;
- restored Working is saved as a root revision;
- then Publish;
- then Activate;
- after durable Save, later Publish/Activate failures are reported as explicit partial recovery and are not falsely rolled back to “empty”.

#### Restore-first frontend component

File:

`web/scada-web/src/auth/RestoreFirstPanel.tsx`

The component already implements:

- bootstrap mode with application package + Authority backup + Authority backup password;
- application-only mode after real Authority login;
- optional license selection;
- application preview/apply;
- optional license install after successful application recovery;
- visible non-blocking license failure with `Continuar sem licença`;
- pt-BR / en / es copy;
- explicit note that DB/Historian recovery remains separate.

A Playwright specification also exists:

`web/scada-web/tests-e2e/wave-14-c25-restore-first.spec.ts`

It expresses the intended UX:

- true-empty bootstrap exposes `Restaurar backup` before Administrator creation;
- Authority restores first, then a real restored user must log in before application Apply;
- restored Administrator can recover application without creating a disposable project;
- optional license failure does not invalidate successful application recovery.

## 5. Critical unresolved C25.4 gap at handoff

**Do not mark C25.4 complete yet.**

Static revalidation of exact HEAD `a7ac6a8a...` found two concrete gaps:

1. `web/scada-web/src/auth/AuthGate.tsx` does **not** currently import/render `RestoreFirstPanel` or expose the required `Restaurar backup` choices on true-empty bootstrap / authenticated-no-project surfaces.
2. `.github/workflows/wave14-c25-post-demo.yml` does **not** run `wave-14-c25-restore-first.spec.ts`; it only runs the Engineering Lock Chromium specification.

Therefore the Restore-first Playwright file exists but is not currently part of CI proof, and the current AuthGate source does not satisfy the behavior asserted by that test.

This discrepancy is the first task for the next coordinator. Do not paper over it by weakening/skipping the E2E test.

## 6. Exact next implementation sequence

1. Revalidate #283 HEAD and current CI before mutation.
2. Re-read:
   - `docs/WAVE14-C25-CONSOLIDATED-POST-DEMO-CONTRACT.md`;
   - `docs/WAVE14-C25-RESTORE-FIRST-ARCHITECTURE.md`;
   - `docs/WAVE14-C25-FULL-CODE-AUDIT.md`;
   - `docs/WAVE14-C25-POST-DEMO-EXECUTION-LOG.md`;
   - this handoff.
3. Inspect current `AuthGate.tsx`, `RestoreFirstPanel.tsx` and `wave-14-c25-restore-first.spec.ts` together.
4. Wire Restore-first into the real AuthGate state machine:
   - true-empty: `Restaurar backup` OR `Criar Administrador`;
   - after Authority restore: require real restored login before application Apply;
   - authenticated local admin with no persisted project: `Restaurar backup` OR `Criar novo projeto`;
   - preserve the selected application/license files across Authority restore -> sign-in -> application step without persisting secret/password material.
5. Extend C25 workflow to execute the Restore-first Chromium specification in addition to Engineering Lock browser coverage.
6. Run/inspect exact-SHA C25 backend + web evidence. Diagnose any red before rerun.
7. Confirm optional license failure remains explicitly non-blocking for core recovery.
8. Confirm Authority/Application recovery APIs remain unavailable outside their intended authenticated/true-empty boundaries.
9. Only after the actual browser flow is wired and green, record C25.4 as COMPLETE in ledger/#282/#283.
10. Then start C25.5 Runtime session UX. Do not jump to C25.5 while C25.4 is only partially wired.

## 7. C25.5 through C25.8

- C25.5 Runtime session UX — NOT STARTED.
- C25.6 contextual multilingual Help/manual — NOT STARTED.
- C25.7 integrated regression/audit — NOT STARTED.
- C25.8 final exact candidate matrix/acceptance — NOT STARTED.

Final C25 acceptance still requires a single exact candidate SHA and the appropriate full Wave14 matrix. Focused green jobs do not equal final acceptance.

## 8. C11 sequence remains frozen

Do not sync/adapt C11 yet.

Required Product Owner sequence remains:

1. C24 accepted — DONE;
2. C25 fully implement + accept;
3. merge accepted C25 only into `wave14/corrections-integration`;
4. post-merge exact-SHA revalidation;
5. only then sync/adapt C11 to the accepted final contracts;
6. export/version/freeze canonical EEE `.escadapkg`;
7. final C11 gates + Preview;
8. Product Owner real visual homologation;
9. only then C11 ACCEPTED/FROZEN/DEMO-READY and #263 may integrate.

## 9. Resume rule

When a new coordinator receives this project, do not trust this document blindly either. First fetch live #282/#283/#212/#263/#266 and current branch HEAD. If live GitHub differs, live GitHub wins.

The fastest safe starting point is:

- issue #282;
- PR #283;
- this handoff;
- execution ledger;
- C25 consolidated contract;
- Restore-first architecture;
- full code audit.

No product integration, `main` mutation, C11 synchronization or #266 merge is authorized by this handoff.