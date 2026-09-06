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

**Status: COMPLETE / FOCUSED VALIDATION GREEN / FINAL C25 MATRIX STILL PENDING**

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
- verifier v1 uses PBKDF2-SHA256, 210,000 iterations, random salt and constant-time verification.
- no plaintext or reversibly encrypted Engineering Lock secret is persisted.
- malformed/unsupported/weakened verifier metadata fails closed.
- legacy v16 payload without lock metadata normalizes to unlocked/unconfigured.
- canonical JSON and `.escadapkg` round-trip lock state without encrypting the whole package.
- partial CSV operations preserve current Engineering Lock state.
- full application replacement may replace/clear lock state without asking for the old Lock password, per binding Import contract.
- Working/Published/Active revision persistence stores immutable canonical lock metadata.

### C25.2 — Engineering Lock backend enforcement and restricted authority

**Status: COMPLETE / FOCUSED VALIDATION GREEN / NOT FINAL C25 ACCEPTANCE**

Validated code SHA:

`493acedc12079b73c02312a2d8355291664226b4`

Focused validation:

- Wave 14 C25 Post-Demo #2 / run `34005944316` — **SUCCESS**;
- job `Engineering Lock backend contract` / `101413008823` — **SUCCESS**.

Backend implementation establishes:

- Authority authentication/capability remains the first admission boundary.
- Engineering Lock is a separate application-content policy; `EngineeringModify` is not stripped or rewritten.
- protected Workspace Engineering reads and mutations fail closed while locked.
- Delete, Bulk Apply, Save/Publish/Activate and protected lifecycle content are blocked while locked.
- Import/Export, `.escadapkg` Inspect/Preview/Apply, solution replacement/recovery paths and explicit Lock management remain reachable subject to normal Authority/validation/audit boundaries.
- Authority administration, Licensing, Audit and Diagnostics retain their independent authorities.
- management API exposes only `configured` + `locked`, never verifier salt/hash.
- wrong unlock secret remains locked and is audited without secret disclosure.
- malformed lock metadata fails closed.
- Published activation rehydrates Lock only after successful committed activation.
- restart recovery rehydrates Lock from durable Active, not a newer Published revision.
- failed activation does not replace the current Lock state.
- Bulk partial packages preserve the current verifier instead of silently clearing it.

Important diagnosed CI red:

- C03 #140 / `34005773590` failed during API compilation because an unintended whole-file `Program.cs` replacement introduced three symbols not present in the canonical base.
- No blind rerun occurred.
- `Program.cs` was restored by exact original blob in `992d6fb0e9a83d3ddbd30f7d549958412280e48f`.
- There is no net C25 `Program.cs` product diff from that incident.

### C25.3 — Engineering Lock UI/lifecycle/package roundtrip

**Status: COMPLETE / FOCUSED BACKEND + WEB VALIDATION GREEN / NOT FINAL C25 ACCEPTANCE**

Exact validated product SHA:

`a5fb9959fa15c060f51417de7bea84a5eec11e5b`

Principal C25.3 commits:

- `c12e16e3b1295bcc7e2fd8f08c474ce60578b07e` — serialize Lock changes as Working mutations and expose restricted administration context;
- `9f301bfb6fae2943df5b73a8e63b16bbe31781e4` — Lock lifecycle/context regression tests;
- `fdf601b01864d583b06a6868343a3c15ffc54ee9` — decouple locked Import/Export concurrency context from protected Workspace endpoint;
- `040c9cccbc3d8ff000b6450b031f14f0ad467703` — frontend Lock API client;
- `a19418309cb6e1c2e2dcd30848024838b122d907` — restricted Engineering surface;
- `5cdb944c51c9383d91781c0f0caee37155d24504` — restricted Lock presentation;
- `302e5242f66aabedffbf3caa927ff4dad2363f56` — backend-state Engineering Lock gate in application routing;
- `46b8ae9c495ad39f44ee70702e4bd8b7b6786bef` — focused Chromium contract tests;
- `4f1e64f8e2fa0f76e4fb13a5b379a12a589b80de` — C25 workflow extended with React/Vite + focused Chromium validation;
- `211d0bf072318585474069c27ed4a1e575c0bf9a` — compile-safe correction found by static review;
- `a5fb9959fa15c060f51417de7bea84a5eec11e5b` — scope one ambiguous Playwright selector; no product behavior changed.

Implemented behavior:

- Authority remains the route admission authority; Lock does not fabricate or strip frontend capabilities.
- `/engineering` first resolves backend Lock state.
- while locked, the protected `EngineeringApp` is not mounted, so TAGs, Screens, Scripts, Alarms and other protected application-development content are not fetched/rendered.
- restricted mode retains explicit unlock, Import/Export/Inspect/Preview/Apply/Restore, local Authority user administration and Licensing navigation.
- wrong secret remains restricted and clears the password field without exposing protected content.
- correct secret transitions to full Engineering and only then mounts protected content.
- configure-and-lock transitions immediately from full Engineering to restricted mode.
- Lock configure/lock/unlock/clear are serialized through the Working mutation gate and mark Working dirty/increment `changeVersion` only on a real state change.
- Active activation/restart rehydration remains outside Working dirty tracking.
- locked project portability uses a minimal administration context containing only project identity/revision/dirty/version and canonical schema identity; it does not reopen the protected Workspace descriptor.
- restricted Lock UI follows the existing `pt-BR`, `en` and `es` Engineering locale contract.

Exact C25.3 validation:

- Wave 14 C25 Post-Demo #26 / run `34007585252` — **SUCCESS**;
- backend job `101417526710` — **SUCCESS**;
- web job `101417526603` — **SUCCESS**;
- React/Vite build — SUCCESS;
- Engineering Lock package/crypto tests — SUCCESS;
- Engineering Lock backend/lifecycle tests — SUCCESS;
- focused Chromium Engineering Lock tests — SUCCESS.

Diagnosed predecessor red, preserved for provenance:

- Wave 14 C25 Post-Demo #24 / run `34006485293` — overall FAILURE;
- backend job was SUCCESS and React/Vite build was SUCCESS;
- Chromium produced 3 passed / 1 failed;
- the single failure was Playwright strict-mode ambiguity because the assertion for heading `Administração` matched both the restricted-section heading and the nested `UserAdministration` heading;
- no product contract failed and no blind rerun occurred;
- commit `a5fb9959...` scoped the assertion to `data-testid="user-administration"` with exact heading matching;
- the newly generated exact-SHA run #26 then passed completely.

Additional exact-SHA compatibility evidence on `a5fb9959...`:

- Wave 14 C03 DNP3 Adapter #155 / run `34007585312` — **SUCCESS**;
- Windows native host — SUCCESS;
- Linux native host — SUCCESS;
- managed adapter build/tests — SUCCESS;
- OpenDNP3 ↔ dnp3py L3 interop — SUCCESS;
- Windows commercial publish dependency gate — SUCCESS.

### C25.4 — Restore-first bootstrap / System Recovery

**Status: IN PROGRESS — LIVE READ-ONLY ARCHITECTURE AUDIT STARTED / NO C25.4 PRODUCT MUTATION YET**

C25.4 audit began against exact validated C25.3 SHA `a5fb9959fa15c060f51417de7bea84a5eec11e5b` before any recovery implementation.

Confirmed current architecture:

- anonymous local first-run is already fail-closed: it is available only when the durable local identity store is empty **and** the persisted Engineering project catalog is empty;
- if persisted projects exist while the identity store is empty, anonymous bootstrap is explicitly blocked;
- current `AuthGate` forces `Criar novo projeto` after local authentication when no persisted project exists; this is the UI behavior C25.4 must replace/refine so `Restaurar backup` is first-class before any disposable project is required;
- current first-project creation remains authenticated, serialized and conflict-checked; C25.4 must not weaken it;
- `.escadapkg` application Inspect/Preview/Apply already has size, package validation, Workspace optimistic-version and Authority/audit boundaries; recovery should reuse these contracts rather than invent a second package parser;
- Licensing is a separate service/API and remains an optional independent recovery step, never a prerequisite for Authority/application restore;
- `ILocalIdentityStore` currently supports list/find/create/update plus a mutation lease, but there is no Authority backup/restore contract;
- `PostgreSqlLocalIdentityStore` stores user identity, roles and password credential material (`salt`, `hash`, `iterations`) and has an advisory mutation lease;
- that existing lease serializes mutations but does **not** provide a transaction spanning existing `CreateAsync`/`UpdateAsync`, because those methods create their own data-source commands/connections;
- therefore Authority restore must introduce a dedicated transactional replace/apply primitive; a sequential delete/create loop is not acceptable;
- local password credentials already preserve PBKDF2-SHA256-compatible verifier material and no plaintext password needs to enter the backup payload;
- local JWT validation is tied to account existence/enabled state and `UpdatedAtUtc` version; active sessions/tokens are not portable backup content and restored state must not attempt to restore them.

Open C25.4 audit items before first recovery code commit:

1. pin the exact host composition/initialization path for local identity endpoints and startup initialization;
2. define the versioned encrypted/authenticated Authority backup envelope and payload DTO;
3. define a dedicated atomic Authority replace/apply interface with PostgreSQL transaction semantics and in-memory test implementation;
4. define Preview validation including duplicate IDs/usernames, credential metadata, role normalization and at least one usable administrative identity;
5. define the true-empty anonymous Restore-first admission predicate by reusing the existing identity-empty + project-empty durable-store boundary;
6. define staged recovery sequencing for Authority + application + optional license without conflating backup password, login password and Engineering Lock secret;
7. add a dedicated C25.4 architecture record before functional mutation.

### C25.5 — Runtime session UX

**Status: NOT STARTED**

### C25.6 — Contextual/manual product integration

**Status: NOT STARTED**

### C25.7 — Integrated regression/audit pass

**Status: NOT STARTED**

### C25.8 — Exact final candidate matrix and acceptance

**Status: NOT STARTED**

Final acceptance requires one exact candidate SHA, the required Wave14 product matrix on that SHA, diagnosed reds before any rerun, then merge only into `wave14/corrections-integration` and exact post-merge revalidation. Only after that may C11 synchronization begin.

## 5. Current governance revalidation at C25.3 close

Revalidated immediately before this checkpoint record:

- #283: OPEN / DRAFT / merged=false / base `wave14/corrections-integration` / exact validated product head `a5fb9959fa15c060f51417de7bea84a5eec11e5b` before this documentation commit.
- #212 remains OPEN / DRAFT / no authorization to merge into `main`.
- C11 remains preserved at `41d24d89c3b9d2b881215255e44023fabde262f3`.
- #263 remains OPEN/DRAFT and must not be synchronized yet.
- #266 remains validation-only / MUST NEVER MERGE.

No integration, `main` or C11 mutation occurred during C25.0 through C25.3.

## 6. Resume protocol

On a new coordinator/chat session:

1. fetch issue #282 and PR #283;
2. revalidate #212, #263 and #266;
3. fetch the current C25 branch HEAD;
4. read `docs/WAVE14-C25-CONSOLIDATED-POST-DEMO-CONTRACT.md`;
5. read `docs/WAVE14-C25-FULL-CODE-AUDIT.md`;
6. read this execution ledger;
7. inspect the latest checkpoint exact SHA and any workflow evidence;
8. continue C25.4 from live GitHub state unless a later durable checkpoint supersedes it.

Never infer unfinished C25 work from chat history when GitHub can be checked directly.
