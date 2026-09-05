# Wave 14 C25 — Post-Demo Consolidated Corrections — Execution Log

**Status:** ACTIVE / NOT ACCEPTED / NOT INTEGRATED  
**Coordinator package:** C25  
**Tracking issue:** #282  
**Product-direction authority:** #280, especially comment `5554918787`  
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

### 3.1 Application Engineering Lock

Required semantics from #280:

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

PR #273 is historical design input only and must be revalidated/adapted.

### 3.4 Runtime session UX

Review/adapt #274 against live product state:

- current user remains visible;
- `Trocar usuário`;
- `Sair`;
- switching invalidates old session first;
- stale privileged UI cannot remain authoritative during switch;
- identity/capabilities are reloaded from backend Authority;
- Runtime-only user never gains Engineering.

### 3.5 Contextual/manual integration

Review/adapt #274 against live product state:

- stable language-neutral Help IDs;
- contextual help for major Engineering surfaces/complex fields;
- detailed coverage especially Drivers/Sources/TAG addressing/Scripts/Reports/HMI;
- help follows active UI language;
- prefer local/offline versioned manual;
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

Goals:

- establish issue/branch/PR/execution ledger;
- inspect current Engineering package/persistence/bootstrap contracts;
- inspect Authority authentication/backup APIs and capability projection;
- inspect frontend Engineering/Runtime routing/session authority;
- inspect help/localization infrastructure;
- review #273/#274 diffs against current accepted baseline;
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
- #273/#274 remain design-only inputs and are not merge shortcuts;
- Wave13 #205/#207 remains paused;
- no force-push, destructive rebase, branch deletion or unrelated cleanup;
- diagnose every red before rerun;
- never weaken tests, validation, security, identity, lifecycle, licensing, package or Runtime contracts for green CI;
- no EEE-specific workaround for generic product behavior;
- backend Active revision remains Runtime application authority;
- Alarm / Operational Event / Audit remain distinct.

## 7. Resume protocol after coordinator/chat loss

On any new coordination session:

1. fetch issue #282;
2. fetch the live C25 PR;
3. fetch this file from live C25 HEAD;
4. revalidate `wave14/c25-post-demo`, `wave14/corrections-integration`, #212, #263 and #266;
5. inspect the most recent checkpoint and exact SHA;
6. revalidate any CI referenced by that checkpoint;
7. continue only from live GitHub state.

Never infer unfinished C25 work from chat history when GitHub can be checked directly.
