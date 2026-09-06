# Wave 14 C25 — Post-Demo Consolidated Corrections — Execution Log

**Status:** ACTIVE / NOT ACCEPTED / NOT INTEGRATED  
**Coordinator package:** C25  
**Tracking issue:** #282  
**Implementation PR:** #283  
**Implementation branch:** `wave14/c25-post-demo`  
**Integration target:** `wave14/corrections-integration`  
**Binding product contract:** `docs/WAVE14-C25-CONSOLIDATED-POST-DEMO-CONTRACT.md`  
**Full code audit:** `docs/WAVE14-C25-FULL-CODE-AUDIT.md`  
**Historical Product Owner provenance:** #280, especially comment `5554918787`

> GitHub is the sole project authority. Revalidate live refs, PR state and exact-SHA CI before every decision or mutation. This ledger records the current resumable checkpoint; earlier preparation detail remains preserved in Git history.

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

No product bytes were changed by those preparation commits.

## 4. Checkpoint matrix

### C25.0 — Durable bootstrap + full architecture/code audit

**Status: COMPLETE**

Exact audited product starting SHA:

`3657826b01717553024d0115c10305cf548f5433`

Audit commit:

`063a826f551e840f21ccd1c96d2d4ff2960322c3`

Durable audit:

`docs/WAVE14-C25-FULL-CODE-AUDIT.md`

The audit maps all C25 correction domains to current code and records REUSE / MODIFY / ADD / DO NOT TOUCH boundaries, security decisions, required tests, dependency order and risk register.

Key architecture decisions:

- Engineering Lock rides in canonical Engineering JSON and therefore follows revision/package lifecycle naturally.
- `.escadapkg` remains a portable application package, not whole-system encrypted DRM.
- Engineering Lock is a dedicated secret domain, separate from Authority login and Authority backup credentials.
- Existing Authority password hashing is the reviewed cryptographic precedent: PBKDF2-SHA256, random salt, deliberate iteration count and constant-time comparison.
- Backend Authority remains primary identity/capability authority; Engineering Lock is an additional application-content admission boundary.
- Runtime remains Active-revision authoritative.
- clean-install recovery must become restore-first rather than force a disposable project first.
- existing Runtime session UI is reusable rather than replaced.
- contextual Help requires a new centralized language-neutral Help-ID resolver.

### C25.1 — Engineering Lock domain/package/security contract

**Status: CODE IMPLEMENTED / VALIDATION EVIDENCE PENDING**

Code/test commits:

1. `bc9d1de154fa40ba855847c6e6cc0ac9c145b0cb` — add canonical Engineering Lock package contract.
2. `f6aa2df9d12301bd0a2c41eb670521620f542520` — add dedicated Engineering Lock verifier/registry service.
3. `2140921e7f12f609aa5799eb97d8a59eadcf443f` — focused verifier/security tests.
4. `8ad05422ab68fd5bcc30d67e0e26c791cc2c9b77` — persist Engineering Lock through canonical Engineering exchange.
5. `14bceb8c64569d9479494044d2205dfb55aae436` — package/lifecycle/legacy/partial-import roundtrip tests.

Implemented contract:

- `EngineeringLockVerifierDto` contains algorithm/version/iterations/salt/hash only.
- `EngineeringLockEngineeringDto` keeps `Locked` independent from verifier presence.
- optional `EngineeringLock` field added to `EngineeringPackage` without increasing schema v16 merely for an additive optional field.
- verifier v1 uses PBKDF2-SHA256, 210,000 iterations, 16-byte random salt and 32-byte hash.
- verification uses `CryptographicOperations.FixedTimeEquals`.
- no plaintext or reversibly encrypted Engineering Lock secret is persisted.
- malformed/unsupported/weakened verifier metadata fails closed.
- legacy v16 payload without lock metadata normalizes to unlocked/unconfigured.
- canonical JSON export/import carries lock state.
- `.escadapkg` carries lock state through the canonical payload without encrypting the whole package.
- partial CSV package operations preserve current Engineering Lock state rather than implicitly clearing it.
- full application package replacement may replace/clear lock state without asking for the existing Engineering Lock password, consistent with the Product Owner contract that Import is an allowed locked-state recovery/administrative operation.
- Working/Published/Active revision persistence stores immutable canonical lock metadata with each revision.

Focused tests added cover:

- configured-but-unlocked state;
- correct/wrong secret;
- invalid verifier rejection;
- absence of plaintext secret in serialized state;
- legacy v16 package compatibility;
- canonical JSON roundtrip;
- partial CSV preservation;
- `.escadapkg` Export -> Inspect -> Preview -> Apply roundtrip;
- malformed lock fail-before-mutation;
- Save -> Publish -> Activate -> LoadActive immutable lock preservation.

Validation caveat:

The normal EliteSCADA CI workflow in `.github/workflows/dotnet-ci.yml` is scoped to pushes/PRs targeting `main`. PR #283 targets `wave14/corrections-integration`, so GitHub associated no normal EliteSCADA CI run with the above C25.1 commits. **Do not infer green from absence of a run.** C25.1 is implemented but not yet accepted/validated by the final required matrix.

### C25.2 — Engineering Lock backend enforcement and restricted authority

**Status: IN PROGRESS**

Live backend inspection at exact C25.1 head `14bceb8c64569d9479494044d2205dfb55aae436` confirmed:

- `src/Scada.Api/Security/EngineeringReadSecurityExtensions.cs` already centralizes Authority admission for Engineering reads.
- `src/Scada.Api/Security/ApiAuthorizationService.cs` remains the backend identity/capability authority.
- `src/Scada.Api/Security/ApiMutationAuditAdmissionMiddleware.cs` durably admits protected POST/PUT/PATCH/DELETE operations through Audit before execution.
- `src/Scada.Api/Security/ApiAuditService.cs` sanitizes password/secret/token fields from audit details.
- Licensing/Audit/Diagnostics have explicit capability boundaries and are not to be folded behind Engineering Lock.

Implementation direction:

- register one shared `IEngineeringLockRegistry` in API composition so package exchange, filters and lock endpoints observe the same application lock authority;
- extend centralized backend admission rather than sprinkling route-local lock checks;
- Authority authentication/capability decision occurs first; Engineering Lock is additional admission afterward;
- locked protected Engineering reads/mutations must fail without protected payload leakage;
- FullAccess does not bypass Engineering Lock;
- locked-state exemptions remain Authority administration, Licensing, Import, Export, application replacement/restore/recovery and explicit unlock;
- explicit lock/configure/unlock/clear operations remain Authority-authenticated and auditable;
- no client-side state is security authority.

### C25.3 — Engineering Lock UI/lifecycle/package roundtrip

**Status: NOT STARTED**

### C25.4 — Restore-first bootstrap / recovery

**Status: NOT STARTED**

### C25.5 — Runtime session UX

**Status: NOT STARTED**

### C25.6 — Contextual/manual product integration

**Status: NOT STARTED**

### C25.7 — Integrated regression/audit pass

**Status: NOT STARTED**

### C25.8 — Exact final candidate matrix and acceptance

**Status: NOT STARTED**

Final acceptance requires one exact candidate SHA, the required Wave14 product matrix on that SHA, diagnosed reds before any rerun, then merge only into `wave14/corrections-integration` and exact post-merge revalidation. Only after that may C11 synchronization begin.

## 5. Current live governance revalidation before C25.2

Revalidated immediately before this ledger update:

- #283: OPEN / DRAFT / merged=false / base `wave14/corrections-integration` / head `14bceb8c64569d9479494044d2205dfb55aae436` before this documentation commit.
- #212: OPEN / DRAFT / merged=false / head `wave14/corrections-integration` at `c2fc96eacc168ea092c2e4d4dcbc79b00faa3155`.
- #263: OPEN / DRAFT / merged=false / C11 head still `41d24d89c3b9d2b881215255e44023fabde262f3`.
- #266: OPEN / DRAFT / merged=false / validation-only / C11 head still `41d24d89c3b9d2b881215255e44023fabde262f3` / MUST NEVER MERGE.

No integration, main or C11 mutation occurred during C25.0/C25.1.

## 6. Resume protocol

On a new coordinator/chat session:

1. fetch issue #282 and PR #283;
2. revalidate #212, #263 and #266;
3. fetch the current C25 branch HEAD;
4. read `docs/WAVE14-C25-CONSOLIDATED-POST-DEMO-CONTRACT.md`;
5. read `docs/WAVE14-C25-FULL-CODE-AUDIT.md`;
6. read this execution ledger;
7. inspect the latest checkpoint exact SHA and any workflow evidence;
8. continue C25.2 from live GitHub state unless a later durable checkpoint supersedes it.

Never infer unfinished C25 work from chat history when GitHub can be checked directly.
