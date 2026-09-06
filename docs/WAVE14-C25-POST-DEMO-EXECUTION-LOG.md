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

**Status: IMPLEMENTED / FOCUSED VALIDATION GREEN / FINAL C25 MATRIX STILL PENDING**

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
- canonical JSON and `.escadapkg` round-trip lock state without encrypting the whole package.
- partial CSV operations preserve current Engineering Lock state.
- full application replacement may replace/clear lock state without asking for the old Lock password, per binding Import contract.
- Working/Published/Active revision persistence stores immutable canonical lock metadata.

Focused Core tests now run durably in the dedicated C25 workflow described under C25.2.

### C25.2 — Engineering Lock backend enforcement and restricted authority

**Status: COMPLETE / FOCUSED VALIDATION GREEN / NOT FINAL C25 ACCEPTANCE**

Validated code SHA:

`493acedc12079b73c02312a2d8355291664226b4`

Focused C25 workflow introduced by that SHA:

`.github/workflows/wave14-c25-post-demo.yml`

Exact focused validation:

- Wave 14 C25 Post-Demo #2 / run `34005944316` — **SUCCESS**;
- job `Engineering Lock backend contract` / `101413008823` — **SUCCESS**;
- Core restore/build — SUCCESS;
- API/Driver restore/build — SUCCESS;
- `EngineeringLock*` package/crypto tests — SUCCESS;
- `EngineeringLockAccessTests`, `PublishedRuntimeActivationServiceTests`, `PersistedRuntimeRecoveryServiceTests` — SUCCESS.

Backend implementation now establishes:

- Authority authentication/capability remains the first admission boundary.
- Engineering Lock is a separate application-content policy; `EngineeringModify` is not stripped or rewritten.
- FullAccess therefore does not bypass a locked application.
- protected Workspace Engineering reads return fail-closed 403 while locked.
- Engineering Delete and Bulk Apply are blocked and audited while locked.
- persistence Save/Publish/Activate and protected lifecycle content are blocked while locked.
- application Import/Export, `.escadapkg` Inspect/Preview/Apply, solution replacement/recovery paths and explicit Lock management remain reachable subject to normal Authority/validation/audit boundaries.
- Authority administration, Licensing, Audit and Diagnostics retain their independent capability/security authorities and are not folded into Engineering Lock.
- management API exposes only `configured` + `locked`, never verifier salt/hash.
- wrong unlock secret remains locked and is audited without secret disclosure.
- malformed lock metadata fails closed.
- one canonical lock authority is observed through the singleton `IEngineeringExchangeService`; no second global lock truth is introduced.
- Published activation rehydrates Lock from the revision only after successful committed activation.
- restart recovery rehydrates Lock from the durable **Active** revision, not a newer Published revision.
- failed activation does not replace the currently running application's Lock state.

Important defect discovered and fixed during C25.2:

- Bulk partial packages initially omitted `EngineeringLock`; because canonical Apply replaces lock state from the incoming package, a configured-but-unlocked verifier could have been silently cleared by Bulk Apply. `PartialPackage` now carries `source.EngineeringLock` explicitly and the lock boundary tests protect the invariant.

Important CI diagnostic preserved:

- Wave 14 C03 DNP3 Adapter #140 / run `34005773590` failed at `Managed adapter build and tests` before tests.
- Exact errors were unrelated API-host symbol references in `Program.cs`: `RuntimeTagValueCoercion`, `TagWriteContext`, and `ScadaRuntimeFacade.WriteTagAsync`.
- The failure was traced to an unintended whole-file `Program.cs` replacement while attempting to register the Lock API, not to the Lock domain itself.
- No blind rerun occurred.
- `Program.cs` was restored by exact original blob `1496bcf7af7e5203f2260508bf0986e74a2d13a9` in commit `992d6fb0e9a83d3ddbd30f7d549958412280e48f`.
- The Lock API remains correctly mapped through `MapEngineeringMutationEndpoints()` and uses its stateless `EngineeringLockSecretService` locally, so `Program.cs` has **no net C25 diff**.
- Subsequent C25 focused workflow on `493acedc...` compiled the API/Driver graph and passed the Lock tests.

C25.2 supporting commits after the initial backend-policy work include:

- `e1b1a6ddb57a618941136b3ad22f8794e2e70449` — Active activation Lock rehydration;
- `aae5a3e5ac1eec72daf9acfde6bbe6b7e5078dd1` — persisted Active restart Lock recovery;
- `a4913c1b84811509241a074dfc731fe41d7f4f36` — Bulk enforcement + verifier preservation;
- `e2735c515e0c2e78aaf7d0e8736029d0ad10f435` — activation Lock tests;
- `9a720118868cf69b19dff162de64876627dc43a5` — Active/restart Lock tests;
- `62096fe8d65511d175fd6fae5573855af8a87532` — backend boundary tests;
- `d923d97841a6a11c1dca6d7a1e2c6d9beee67964` — preserve independent Diagnostics authority;
- `992d6fb0e9a83d3ddbd30f7d549958412280e48f` — restore canonical API host after diagnosed CI red;
- `493acedc12079b73c02312a2d8355291664226b4` — dedicated C25 focused validation workflow.

### C25.3 — Engineering Lock UI/lifecycle/package roundtrip

**Status: NEXT / NOT YET COMPLETE**

Next implementation must preserve the now-validated backend authority and add product lifecycle/UI behavior without making React security authority. At minimum revalidate:

- Lock status/manage UX in Engineering/System Administration;
- protected Engineering navigation/content hidden while locked;
- restricted Authority/Licensing/Import/Export/recovery/unlock surfaces remain reachable;
- lock configuration/state changes participate correctly in Working dirty/version/save lifecycle;
- package Export/Import remains password-free and preserves metadata/state;
- unlock of the current application does not mutate Authority identity or credential state;
- browser tests exercise correct/wrong unlock and navigation projection against backend state.

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

## 5. Current governance revalidation after C25.2 focused validation

Revalidated before this checkpoint record:

- #283: OPEN / DRAFT / merged=false / base `wave14/corrections-integration` / exact validated code head `493acedc12079b73c02312a2d8355291664226b4` before this documentation commit.
- #212: OPEN / DRAFT / merged=false / integration head still `c2fc96eacc168ea092c2e4d4dcbc79b00faa3155`.
- #263: OPEN / DRAFT / merged=false / C11 head still `41d24d89c3b9d2b881215255e44023fabde262f3`.
- #266: OPEN / DRAFT / merged=false / validation-only / C11 head still `41d24d89c3b9d2b881215255e44023fabde262f3` / MUST NEVER MERGE.

No integration, `main` or C11 mutation occurred during C25.0 through C25.2.

## 6. Resume protocol

On a new coordinator/chat session:

1. fetch issue #282 and PR #283;
2. revalidate #212, #263 and #266;
3. fetch the current C25 branch HEAD;
4. read `docs/WAVE14-C25-CONSOLIDATED-POST-DEMO-CONTRACT.md`;
5. read `docs/WAVE14-C25-FULL-CODE-AUDIT.md`;
6. read this execution ledger;
7. inspect the latest checkpoint exact SHA and any workflow evidence;
8. continue C25.3 from live GitHub state unless a later durable checkpoint supersedes it.

Never infer unfinished C25 work from chat history when GitHub can be checked directly.
