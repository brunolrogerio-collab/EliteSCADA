# Wave 14 C25 — Full code audit and implementation map

**Status:** C25.0 architecture/code audit complete at the baseline below; C25 product implementation remains active.  
**Tracking:** #282 / #283  
**Binding product contract:** `docs/WAVE14-C25-CONSOLIDATED-POST-DEMO-CONTRACT.md`  
**Audited exact starting SHA:** `3657826b01717553024d0115c10305cf548f5433`  
**Integration target:** `wave14/corrections-integration` only.

This document is the durable implementation map for the complete C25 post-DEMO correction package. It records the live product surfaces that were inspected, the code that must be reused or changed, new boundaries that must be added, protected surfaces that must not be weakened, expected regression evidence, and the dependency order between C25.1 through C25.8.

GitHub live state still prevails over this file if later commits change any path or contract. Every implementation slice must revalidate the live C25 head before mutation.

## 1. Non-negotiable authorities

- `main` is not a C25 work target.
- #212 remains OPEN/DRAFT and is not merge authority for `main`.
- #263 remains preserved until C25 is fully accepted, integrated and post-merge revalidated.
- #266 is validation-only and MUST NEVER MERGE.
- Backend Active revision remains Runtime application authority.
- Authority authentication/authorization remains distinct from Engineering Lock.
- Authority backup/restore credential remains distinct from Engineering Lock.
- Licensing remains its own product authority.
- Alarm, Operational Event and Audit remain distinct domains.
- `.escadapkg` remains an application/project portability artifact; C25 does not turn it into whole-system backup or encrypted DRM.

## 2. Exact live surfaces inspected

### Engineering canonical model / import-export

Verified at `3657826...`:

- `src/Scada.Engineering/Contracts/EngineeringContracts.cs`
- `src/Scada.Engineering/ImportExport/EngineeringExchangeService.cs`
- `src/Scada.Engineering/ProjectPackages/ProjectPackageService.cs`
- `src/Scada.Api/ProjectPackages/ProjectPackageEndpoints.cs`

Current facts:

- canonical Engineering schema is `scada.engineering`, current schema version `16`;
- `EngineeringPackage` is the canonical application payload and currently ends with `StartupScreenId`;
- `EngineeringExchangeService.ExportPackage()` builds the canonical payload from live registries;
- `ParseJson()` already accepts older supported schema versions and normalizes nullable collections;
- `.escadapkg` is a ZIP with `manifest.json`, `payload/project.json` and media payloads;
- package integrity is SHA-256 based and media is hash-validated;
- package format is not encrypted;
- package endpoints are Authority-authenticated and Import/Apply require Engineering project modification authority.

### Engineering lifecycle / revision persistence

Verified:

- `src/Scada.Engineering/Persistence/IEngineeringProjectStore.cs`
- `src/Scada.Engineering/Persistence/EngineeringProjectPersistenceService.cs`

Current facts:

- each saved revision contains complete canonical Engineering JSON;
- Working/latest, Published and Active are separate lifecycle pointers/states;
- Published and Active resolve immutable stored revisions;
- Runtime Active authority therefore remains compatible with persisting Engineering Lock metadata inside canonical Engineering JSON;
- there is no need for a second lock database merely to preserve lifecycle/package state.

### Existing credential security pattern

Verified:

- `src/Scada.Security/Authentication/LocalIdentityStore.cs`
- `src/Scada.Security/Authentication/LocalIdentityModels.cs`

Current reviewed password-verifier pattern:

- random 16-byte salt;
- PBKDF2-SHA256;
- 210,000 iterations;
- 32-byte derived key;
- Base64 persisted salt/hash;
- `CryptographicOperations.FixedTimeEquals` verification;
- no plaintext password persistence.

C25 decision: Engineering Lock must use a dedicated application-lock verifier domain, but reuse this reviewed cryptographic pattern rather than introduce reversible encryption or a compiled/static key. Authority identity records and Engineering Lock records remain separate.

### API composition / enforcement surfaces

Verified:

- `src/Scada.Api/Program.cs`
- `src/Scada.Api/ProjectPackages/ProjectPackageEndpoints.cs`

Current facts:

- Engineering reads and mutations are mapped as backend endpoints and already pass through Authority capability requirements;
- product Licensing is mapped independently;
- package Import/Export is mapped independently;
- C25.2 therefore needs an additional backend Engineering-Lock admission boundary layered after Authority, not a replacement for Authority;
- Import/Export, Authority administration, Licensing, solution replacement/restore and explicit unlock must be explicit lock exemptions.

### Frontend authentication / navigation / Runtime session

Verified:

- `web/scada-web/src/auth/AuthGate.tsx`
- `web/scada-web/src/auth/UserSessionMenu.tsx`
- `web/scada-web/src/auth/UserSessionMenuView.tsx`
- `web/scada-web/src/AppNavigation.tsx`
- `web/scada-web/src/runtime/application/RuntimeApplicationMount.tsx`

Current facts:

- AuthGate owns current authenticated profile and local login flow;
- AuthGate currently forces first-project creation after local authentication when persistence is empty;
- current logout calls `/api/auth/logout` then clears local profile without checking a non-success response, so C25.5 must make logout fail-closed and visibly fail when server invalidation fails;
- an existing system-owned `UserSessionMenu` already shows identity and roles and already has logout-failure UI handling around the supplied callback;
- the session menu already appears in the global shell and is explicitly mounted inside Runtime fullscreen;
- AppNavigation already projects surfaces from backend-effective capabilities;
- Runtime application mounting already reloads the backend Active application projection and must remain Active-revision authoritative;
- C25.5 should extend the existing session menu/auth context, not build a parallel session system.

### Localization / Help discovery

Verified by exact C25 tree and inspected frontend files:

- active UI locale is already persisted under `elitescada.engineering.locale` and supports `pt-BR`, `en`, `es` across current auth/session surfaces;
- localization is currently distributed among app-shell/Engineering/domain modules;
- there is no established centralized product Help-ID -> installed-topic resolver in the audited baseline;
- no existing Authority backup/restore implementation was found by the C25 repository-wide backup/restore discovery search. C25.4 must treat Authority backup/restore as an ADD boundary unless later live code discovery proves otherwise.

## 3. C25.1 — Engineering Lock domain/package/security contract

### REUSE

- canonical `EngineeringPackage` serialization/deserialization;
- revision JSON persistence;
- `.escadapkg` manifest/payload/hash pipeline;
- existing LocalIdentity PBKDF2/fixed-time comparison pattern as reviewed security precedent.

### MODIFY

- `src/Scada.Engineering/Contracts/EngineeringContracts.cs`
  - add optional versioned Engineering Lock metadata to `EngineeringPackage`;
  - keep old schema payloads readable by defaulting missing lock metadata to no configured lock/unlocked.
- `src/Scada.Engineering/ImportExport/EngineeringExchangeService.cs`
  - export/import the application lock state through the canonical package;
  - do not make lock metadata an import obstacle merely because the imported application is locked.

### ADD

Planned dedicated domain under `src/Scada.Engineering/Security/`:

- application lock state registry/service;
- versioned verifier contract;
- password configure/verify/clear transitions;
- explicit lock/unlock state transition rules.

Security format v1:

- algorithm identifier: `PBKDF2-SHA256`;
- verifier version: `1`;
- random salt: 16 bytes;
- iterations: 210,000;
- derived hash: 32 bytes;
- persisted Base64 salt/hash only;
- compare using `CryptographicOperations.FixedTimeEquals`;
- reject malformed/unknown verifier versions fail-closed;
- no plaintext secret, reversible encrypted secret, fixed key, Authority-password reuse or backup-password reuse.

Recommended canonical shape:

- `EngineeringLockVerifierDto` carries algorithm/version/iterations/salt/hash;
- `EngineeringLockEngineeringDto` carries `Locked` and optional `Verifier`;
- configured state is `Verifier != null`, independently from `Locked`.

Schema rule: this is an additive optional field. C25.1 must prove old v16 payloads remain readable. A schema-version bump is required only if validation/import semantics become incompatible; do not bump merely because an optional field exists.

### DO NOT TOUCH / DO NOT WEAKEN

- Authority local user password records;
- JWT/session semantics;
- package SHA/media validation;
- Active revision authority;
- licensing trust material.

### Required tests

- no verifier => no password lock configured;
- verifier configured + `Locked=false` is valid;
- verifier configured + `Locked=true` is valid;
- correct password verifies;
- wrong password fails;
- malformed/unknown verifier fails closed;
- serialized canonical JSON contains no plaintext secret;
- legacy v16 package without lock metadata parses as unlocked/unconfigured;
- canonical JSON roundtrip preserves verifier and locked flag;
- `.escadapkg` export/inspect/import/apply roundtrip preserves lock metadata/state;
- revision save/load/publish/activate preserves lock metadata/state.

## 4. C25.2 — backend enforcement and restricted authority

### REUSE

- existing Authority capability requirements/extensions;
- existing mutation/read endpoint grouping;
- API audit service for denied/successful security-sensitive operations.

### MODIFY

- backend Engineering read admission paths in `src/Scada.Api/Program.cs` and the existing Engineering security extensions;
- backend Engineering mutation endpoints;
- package endpoints only to declare/verify lock exemptions, never to require Engineering Lock password for Import/Export;
- project persistence/lifecycle endpoints where protected application content would otherwise leak while locked.

### ADD

- server-authoritative current-application lock projection;
- explicit unlock endpoint requiring normal Authority admission plus Engineering Lock secret verification;
- lock/configure/clear endpoints with suitable Authority capability;
- centralized endpoint policy/helper so a new Engineering endpoint cannot casually bypass lock admission.

### Explicit lock exemptions

While the application is locked, retain Authority-authenticated access to:

- Authority user/role/profile/password administration;
- Licensing;
- package Import;
- package Export;
- application replacement/import/restore;
- explicit unlock;
- recovery surfaces required to replace the current application.

The exemption permits the operation, not protected Engineering-content disclosure beyond what that operation inherently needs.

### Required tests

- locked protected GET => denied without protected payload leakage;
- locked protected mutation => denied;
- correct unlock => protected surface becomes available;
- wrong unlock => still locked;
- Authority permission denial still wins regardless of lock state;
- Import/Export remain Authority-authorized but do not ask for Engineering Lock password;
- Licensing/Authority administration remain reachable while locked;
- audit differentiates Authority denial from Engineering Lock denial.

## 5. C25.3 — Engineering Lock UI/lifecycle/package roundtrip

### MODIFY

- Engineering shell/navigation to project backend lock state;
- Engineering project management/portability UI for configure, lock, unlock, clear and package behavior;
- lifecycle UI so saved/published/active revision state is not misrepresented.

### ADD

- locked Engineering restricted shell that exposes only allowed administrative/recovery/product surfaces;
- unlock dialog that does not render protected application data before success.

### DO NOT

- rely on React hiding as the security boundary;
- cache unlocked state as client authority;
- expose lock secret/verifier in ordinary UI diagnostics.

### Required evidence

Frontend contract/e2e tests for locked routing, unlock, wrong password, fullscreen/navigation behavior, package roundtrip, lifecycle persistence and backend denial even when protected routes are manually requested.

## 6. C25.4 — Restore-first bootstrap / System Recovery

### Current incompatibility found

`web/scada-web/src/auth/AuthGate.tsx` currently drives empty persisted systems into first-project creation after authentication. The historical #273 project-first/provisional-user concept is therefore not the C25 target.

### MODIFY

- `AuthGate.tsx` first-run state machine so a clean installation presents Restore before disposable project creation;
- backend bootstrap/config projection so frontend can distinguish recoverable clean-install state safely;
- persistence/package application path for application restore.

### ADD

- Recovery landing/orchestrator;
- Authority backup Export / Inspect-or-Preview / Import-or-Restore service and endpoints;
- versioned Authority backup schema;
- password-derived authenticated encryption for Authority backup;
- non-mutating Preview;
- fail-before-mutation validation;
- atomic/transactional Apply where practical;
- post-restore validation.

### Separate recovery authorities

1. `.escadapkg` application/project portability;
2. Authority identities/roles protected backup;
3. Database/Historian native supported recovery;
4. optional Licensing import/recovery under Licensing's own rules.

### Security rules

- Authority backup password never stored plaintext;
- Engineering Lock secret is never the Authority backup password;
- no universal vendor recovery/backdoor secret;
- do not restore sessions/cookies/access tokens/refresh tokens/runtime auth caches;
- machine-bound licensing trust excluded unless Licensing explicitly supports portability;
- corrupt/wrong-password/incompatible backup fails before Authority mutation.

### Required tests

Clean install restore-first UI; no throwaway project requirement; application-only recovery; Authority Preview/Apply; wrong password; corrupt/incompatible backup; no sessions restored; admin viability after restore; package Active application validation; optional license path; DB/Historian separation.

## 7. C25.5 — Runtime session UX

### REUSE

- `AuthGate` profile context;
- `UserSessionMenu` / `UserSessionMenuView`;
- existing session presentation/localization model;
- AppNavigation effective-capability projection;
- Runtime fullscreen mounting already present.

### MODIFY

- `AuthGate.logout()` must inspect server response and clear client state only after server invalidation succeeds;
- Auth context needs an explicit switch-user transition/state;
- effective capabilities must be invalidated/reloaded after new authentication;
- Runtime must become non-interactive while identity is unresolved during switch;
- session menu adds `Trocar usuário` while retaining `Sair`.

### Required sequence for switch

1. invalidate old server session;
2. remove old client authority immediately;
3. keep Runtime non-interactive;
4. authenticate new user normally;
5. reload backend profile and effective capabilities;
6. render identity/capability-authorized surface only after reload.

No dismissed overlay may resurrect the invalidated old identity.

### Required tests

- identity display preference: displayName then username;
- logout success invalidates server and client;
- logout failure remains visibly failed and does not pretend logout;
- switch invalidates old identity first;
- old privileged surface cannot operate during switch;
- new capabilities reload from backend;
- Runtime-only identity never gets Engineering/Audit/Licensing/Diagnostics navigation;
- audit attribution follows the new identity;
- session control remains reachable in Runtime fullscreen.

## 8. C25.6 — contextual multilingual Help/manual

### REUSE

- current locale key and `pt-BR` / `en` / `es` language selection;
- existing app/domain localization modules;
- production Driver registry/contracts and shipped Script APIs as documentation sources.

### ADD

Recommended frontend product boundary:

- `web/scada-web/src/help/helpRegistry.ts`
- `web/scada-web/src/help/helpResolver.ts`
- installed versioned/local topic content grouped by locale;
- contextual Help component/link that receives stable language-neutral Help ID;
- build/test validation that every registered Help ID resolves for every shipped locale.

### MODIFY

Major Engineering/Runtime/admin surfaces to attach stable Help IDs without embedding localized topic paths in components.

### Rules

- locale switch preserves semantic Help ID;
- broken Help ID fails gracefully and is test-detectable;
- no fallback to unrelated manual home simply because locale changed;
- ordinary product Help is local/offline where practical;
- no internal Wave/ADR/handoff prose in user manual;
- Driver documentation is derived from actual production drivers/contracts;
- Script documentation names only shipped APIs;
- Alarm / Operational Event / Audit remain distinct terminology.

## 9. C25.7 — integrated regression/audit pass

Cross-domain regressions must cover:

- Engineering Lock × package import/export;
- Engineering Lock × Working/Published/Active lifecycle;
- Engineering Lock × Authority roles/capabilities;
- Engineering Lock × Licensing exemption;
- recovery × locked imported application;
- recovery × Authority backup password separation;
- session switch × capability reload × audit attribution;
- Help × locale change × exact semantic topic;
- Runtime remains driven by backend Active revision throughout.

No green result is valid if obtained by weakening existing assertions or bypassing existing security/lifecycle contracts.

## 10. C25.8 — final candidate acceptance

Before C25 acceptance:

- freeze one exact final C25 candidate SHA;
- run the required Wave14 product matrix on that exact SHA;
- inspect every red before rerun;
- record workflow/run/job evidence in the execution ledger;
- only after acceptance merge product bytes into `wave14/corrections-integration`;
- perform post-merge exact-SHA revalidation;
- only then may C11 synchronization begin.

## 11. Implementation order and dependency graph

1. **C25.1** canonical lock metadata + verifier + roundtrip primitives.
2. **C25.2** backend lock admission/enforcement using C25.1 state.
3. **C25.3** UI/lifecycle/package behavior relying on backend authority.
4. **C25.4** restore-first, able to restore applications that may already carry lock metadata.
5. **C25.5** session UX, preserving backend Authority and lock separation.
6. **C25.6** Help/manual against the now-final product surfaces.
7. **C25.7** cross-domain regression and security audit.
8. **C25.8** exact candidate matrix / acceptance / integration / post-merge revalidation.

Do not reorder C25.4 before the package/lock contract is stable; doing so would force Recovery to guess the application security payload. Do not make C25.3 a client-only security implementation. Do not defer session capability reload until after UI resume.

## 12. Risk register

### Highest risk

- accidentally conflating Engineering Lock with Authority authorization;
- leaking protected Engineering through a read endpoint not covered by lock admission;
- treating package encryption as a requirement and thereby breaking portability;
- restore mutating Authority before password/integrity/schema validation completes;
- switch-user retaining stale frontend authority;
- CI concurrency hiding exact candidate provenance.

### Medium risk

- legacy package compatibility after additive lock metadata;
- lifecycle UI confusing current Working lock state with the immutable Active revision state;
- Help locale fragmentation due existing decentralized strings;
- incomplete Driver/manual coverage if maintained by a handwritten stale list.

## 13. C25.0 conclusion

C25.0 is complete from the code-architecture perspective at starting SHA `3657826...`.

There is no architectural blocker to C25.1. The first code-bearing slice should add the canonical Engineering Lock metadata/verifier domain and focused tests before any API/UI enforcement is introduced.

The audit deliberately does not claim C25 is implemented or accepted. It defines where the implementation must occur and the proof required at each checkpoint.