# Wave 14 C25 — Post-Demo Consolidated Corrections — Execution Log

**Status:** ACTIVE / NOT ACCEPTED / NOT INTEGRATED  
**Coordinator package:** C25  
**Tracking issue:** #282  
**Current consolidated product contract:** `docs/WAVE14-C25-CONSOLIDATED-POST-DEMO-CONTRACT.md`  
**Historical Product Owner decision provenance:** #280, especially comment `5554918787`  
**Implementation branch:** `wave14/c25-post-demo`  
**Integration target:** `wave14/corrections-integration`

> GitHub is the sole project authority. Revalidate live refs, PR state and exact-SHA CI before every decision or mutation. This file is a durable execution ledger so coordination can resume safely from another chat/session without relying on conversational memory.

## 1. C25 start authority

C25 was opened after C24 was formally:

**ACCEPTED / INTEGRATED / POST-MERGE REVALIDATED**.

Exact C25 branch base:

`c2fc96eacc168ea092c2e4d4dcbc79b00faa3155`

That base is documentation-only coordination state above accepted product authority:

`40a491c2de2403f2934b8bae647c35072d5c2496`

Accepted C24 candidate beneath it:

`ff5eac6ad12865b174b6ca602a13d01165e57b36`

No C25 product bytes existed at package creation.

## 2. Why C25 is one consolidated package

The Product Owner explicitly moved post-DEMO compatibility-affecting work before final C11 canonicalization. These changes can affect application package, clean bootstrap, Engineering access, Runtime session and contextual documentation contracts.

Rather than splitting the work into C25/C26, the coordinator is deliberately keeping the complete post-DEMO correction set under one C25 branch/PR with internal checkpoints.

C11 remains intentionally preserved at:

`41d24d89c3b9d2b881215255e44023fabde262f3`

Do not sync C25/C24 into C11 until C25 is accepted, integrated and post-merge revalidated.

## 3. C25 binding scope

Detailed current product semantics are consolidated in:

`docs/WAVE14-C25-CONSOLIDATED-POST-DEMO-CONTRACT.md`

Historical design/decision provenance remains #273, #274 and #280, but those items are no longer active execution routes.

### 3.1 Application Engineering Lock

Required semantics:

- optional application/IP lock;
- no password configured means no password-based Engineering Lock exists;
- configured-secret presence and `locked` state are independent;
- Import never requires the Engineering Lock password;
- Export never requires the Engineering Lock password;
- secret is not an Authority/login credential;
- secret is not the Authority backup/restore credential;
- unlocked application exposes normal/full Engineering according to Authority permissions;
- locked application hides application engineering/view/edit capability;
- locked Engineering retains installation administration: Authority users/passwords/profiles, licensing, Import/Export, solution replacement/restore and explicit unlock;
- correct secret unlocks the currently running application;
- wrong secret remains locked and leaks no protected Engineering content;
- package preserves lock metadata/state;
- whole `.escadapkg` is not encrypted.

### 3.2 Engineering Lock cryptographic/security architecture

Architecture is intentionally not frozen at C25 start.

Required gate:

- do not implement a fixed/compiled symmetric key and call it secure;
- define actual verifier/secret/key/storage behavior;
- review it against the declared IP-barrier/deterrence threat model;
- do not claim package confidentiality or DRM-grade resistance;
- preserve secret-domain separation from Authority authentication and Authority backup encryption.

C25 cannot be accepted until this is explicitly documented and tested.

### 3.3 Restore-first bootstrap / System Recovery

Fresh/clean system must allow `Restaurar backup` before disposable user/project creation.

Required restore surface:

- application restore/import;
- Authority restore/import using the Authority backup's own password/credential mechanism;
- optional license file import/attachment;
- license is not required to perform restore;
- Engineering Lock password is not Authority restore password;
- canonical import validation and Authority authorization must not be weakened.

Historical PR #273 was audited and closed without merge after its surviving requirements were transferred into the consolidated C25 contract. Its provisional-Recovery-Administrator/project-first bootstrap is superseded by restore-first sequencing. Its separation of application, Authority and Database/Historian recovery authorities remains preserved for C25 revalidation/implementation.

### 3.4 Runtime session UX

Historical PR #274 was audited and closed without merge after its surviving requirements were transferred into the consolidated C25 contract.

Binding direction includes:

- current user remains visible;
- `Trocar usuário`;
- `Sair`;
- switching invalidates old session first;
- stale privileged UI cannot remain authoritative during switch;
- identity/capabilities are reloaded from backend Authority;
- Runtime-only user never gains Engineering;
- session affordance remains system-owned, outside authored `.escadapkg` HMI content and available in fullscreen;
- post-switch displayed identity, authorization and audit attribution must agree.

### 3.5 Contextual/manual integration

Historical PR #274 and Product Owner refinement comment `5553626108` were audited and transferred into the consolidated C25 contract.

Binding direction includes:

- stable language-neutral Help IDs;
- contextual help for major Engineering surfaces/complex fields;
- detailed coverage especially Drivers/Sources/TAG addressing/Scripts/Reports/HMI;
- manual is version-compatible and local/offline where practical;
- manual is **mandatorily multilingual for shipped UI languages** and follows the active UI locale while preserving semantic Help-ID identity;
- shipped help documents actual product contracts, not internal Wave/ADR/handoff process.

## 4. Execution checkpoint protocol

Every material C25 checkpoint records:

1. exact starting SHA;
2. live refs/PRs revalidated;
3. product contracts/files inspected;
4. derived invariant/decision;
5. files changed;
6. tests added/changed;
7. exact resulting SHA;
8. CI evidence or diagnosed blocker;
9. remaining work.

Focused/local green does not mean package acceptance. Final acceptance is exact-SHA and matrix-based.

## 5. Planned checkpoints

### C25.0 — Durable bootstrap + live architecture audit

Status: **IN PROGRESS**

Completed within C25.0:

- issue/branch/PR/execution ledger established;
- live open issue/PR convergence inventory completed;
- #273/#274/#280 preparation audited and consolidated;
- durable consolidated product contract created;
- obsolete duplicate execution surfaces closed without merge.

Remaining C25.0 goals:

- inspect current Engineering package/persistence/bootstrap contracts;
- inspect Authority authentication/backup APIs and capability projection;
- inspect frontend Engineering/Runtime routing/session authority;
- inspect help/localization infrastructure;
- derive concrete implementation slices before product mutation.

### C25.1 — Engineering Lock domain/package/security contract

Status: NOT STARTED

### C25.2 — Engineering Lock backend enforcement and restricted authority

Status: NOT STARTED

### C25.3 — Engineering Lock UI/lifecycle/package roundtrip

Status: NOT STARTED

### C25.4 — Restore-first bootstrap / recovery

Status: NOT STARTED

### C25.5 — Runtime session UX

Status: NOT STARTED

### C25.6 — Contextual/manual product integration

Status: NOT STARTED

### C25.7 — Integrated regression/audit pass

Status: NOT STARTED

### C25.8 — Exact final candidate matrix and acceptance

Status: NOT STARTED

## 6. Permanent governance during C25

- #212 remains OPEN/DRAFT and must not merge to `main` without later explicit Product Owner authorization;
- never alter `main` directly;
- C25 may integrate only into `wave14/corrections-integration` after exact-SHA acceptance;
- C11 #263 remains preserved until C25 completes;
- #266 remains validation-only and MUST NEVER MERGE;
- #273 is CLOSED WITHOUT MERGE after C25 convergence;
- #274 is CLOSED WITHOUT MERGE after C25 convergence;
- #280 is CLOSED/COMPLETED as a decision record after transfer into C25;
- Wave13 #205/#207 remains paused;
- Preview #208/#210 remains available for later Product Owner homologation;
- Wave14 owner-validation #211 remains open;
- no force-push, destructive rebase, branch deletion or unrelated cleanup;
- diagnose every red before rerun;
- never weaken tests, validation, security, identity, lifecycle, licensing, package or Runtime contracts for green CI;
- no EEE-specific workaround for generic product behavior;
- backend Active revision remains Runtime application authority;
- Alarm / Operational Event / Audit remain distinct.

## 7. Resume protocol after coordinator/chat loss

On any new coordination session:

1. fetch issue #282;
2. fetch the live C25 PR #283;
3. fetch `docs/WAVE14-C25-CONSOLIDATED-POST-DEMO-CONTRACT.md` from live C25 HEAD;
4. fetch this execution ledger from live C25 HEAD;
5. revalidate `wave14/c25-post-demo`, `wave14/corrections-integration`, #212, #263 and #266;
6. inspect the most recent checkpoint and exact SHA;
7. revalidate any CI referenced by that checkpoint;
8. continue only from live GitHub state.

Never infer unfinished C25 work from chat history when GitHub can be checked directly.

## 8. C25.0 preparation convergence checkpoint — 2026-09-05

### Starting exact C25 SHA

`b4b53350a447d48d747c2512297ea0809f8bf61b`

### Live inventory reviewed

Open issues before cleanup:

- #282 C25 active;
- #280 post-DEMO decision input;
- #211 Wave14 Product Owner validation;
- #208 Preview harness;
- #205 Wave13 signing/release pause;
- #178 deferred Siemens L4 validation.

Open PRs before cleanup:

- #283 C25 active;
- #212 Wave14 integration;
- #266 C11 validation-only;
- #263 C11 implementation;
- #274 Runtime session/manual design-only;
- #273 System Recovery design-only;
- #210 Preview harness;
- #207 Wave13 release/signing checkpoint.

### Convergence decision

Close only items whose active purpose is fully absorbed by C25 without losing required execution authority:

- #273 -> CLOSED WITHOUT MERGE;
- #274 -> CLOSED WITHOUT MERGE;
- #280 -> CLOSED/COMPLETED as decision record.

Keep open because they still have distinct future or current operational purpose:

- #282/#283 C25;
- #211 Wave14 validation;
- #212 integration;
- #263 C11 implementation;
- #266 C11 validation-only / NEVER MERGE;
- #208/#210 Preview harness;
- #205/#207 Wave13 paused release/signing;
- #178 deferred external L4 validation.

### Durable consolidation

Created:

`docs/WAVE14-C25-CONSOLIDATED-POST-DEMO-CONTRACT.md`

Consolidation commit:

`b32ee0778d646bc65972ac16401c67f78ad8bc2b`

The contract records explicit preserve/supersede treatment for #273, preserves/adapts #274 including mandatory multilingual Help, and moves #280 from active authority surface to historical Product Owner provenance.

### Closure evidence

- #273 closed without merge; branch preserved;
- #274 closed without merge; branch preserved;
- #280 closed with state reason `completed`; implementation explicitly remains active in #282/#283;
- no `main` mutation;
- no C11 mutation;
- no branch deletion;
- no force push/rebase;
- no product bytes changed by this convergence checkpoint.

### Remaining C25.0 work

Continue the live architecture audit before functional implementation. The next code-bearing checkpoint remains C25.1 only after package/security/bootstrap/session/help authority surfaces are mapped against the current accepted integration product.
