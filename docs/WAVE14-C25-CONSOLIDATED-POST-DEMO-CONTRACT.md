# Wave 14 C25 — Consolidated Post-Demo Product Contract

**Status:** BINDING C25 PRODUCT CONTRACT / IMPLEMENTATION IN PROGRESS  
**Coordinator package:** C25  
**Tracking issue:** #282  
**Implementation PR:** #283  
**Branch:** `wave14/c25-post-demo`

This document consolidates the Wave 14 post-DEMO preparation that was previously distributed across issue #280, Product Owner comment `5554918787`, design-only PR #273 and design-only PR #274.

It exists so C25 can be resumed from GitHub alone without reconstructing product decisions from chat history or leaving active design PRs open merely as memory storage.

## 1. Authority and supersession order

For C25 implementation, read product intent in this order:

1. this consolidated contract on the live C25 branch;
2. issue #282 and the C25 execution ledger;
3. Product Owner clarification `#280 / comment 5554918787` as historical decision provenance;
4. PR #273 and PR #274 only as historical design provenance.

Where an older design conflicts with this document, this document wins for C25.

The older items are intentionally not merge routes.

## 2. Application Engineering Lock — binding contract

The application password concept is an optional **Application Engineering Lock**, not an Import/Export password and not an Authority credential.

### 2.1 Semantics

- Engineering Lock is optional.
- No configured secret means there is no password-based application lock to authenticate against.
- Configured-secret presence and current `locked` state are independent.
- A package may therefore contain protected lock verifier/metadata while `locked=false`.
- Import must never require the Engineering Lock password.
- Export must never require the Engineering Lock password.
- Import/export must preserve the lock metadata/state defined by the package contract.
- The Engineering Lock secret is separate from Authority login credentials.
- The Engineering Lock secret is separate from the Authority backup/restore password or credential.
- The whole `.escadapkg` must not be encrypted merely to implement Engineering Lock.

### 2.2 Unlocked application

When the application is unlocked:

- normal/full Engineering is visible according to the authenticated user's backend-effective Authority permissions;
- Engineering exposes the controls required to configure/manage the lock secret and enable/disable Lock;
- no frontend-only capability fabrication is permitted.

### 2.3 Locked application

When the application is locked, protected application-development/view/edit surfaces must not be exposed.

The restricted Engineering/System Administration surface must still allow legitimate installation administration, including at minimum:

- Authority user/login/password/profile/role administration;
- licensing administration;
- application Import;
- application Export;
- solution replacement/import/restore;
- explicit unlock of the currently running application using that application's Engineering Lock secret.

Correct secret unlocks the currently running application. Wrong secret leaves it locked and must not leak protected application Engineering content.

Backend/server-side enforcement remains authoritative. Hiding React navigation alone is not an acceptable lock implementation.

### 2.4 Security architecture gate

The concrete verifier/secret/storage/key architecture is a required C25 design and security-review gate.

Binding boundaries:

- do not store plaintext lock secrets;
- do not accept a fixed/compiled/static symmetric key as the final architecture merely because it makes package roundtrip convenient;
- define a concrete verifier/secret/key/storage mechanism before acceptance;
- review the mechanism against the actual threat model: Engineering/IP access barrier and deterrence, not cryptographic DRM;
- do not claim whole-package confidentiality or resistance to a determined reverse-engineering attacker unless a future architecture actually provides it;
- maintain strict separation from Authority authentication credentials and Authority backup encryption.

## 3. System Recovery / Restore-first bootstrap — consolidated contract

### 3.1 Restore-first is binding and supersedes the provisional-user-first design

A fresh or cleaned installation must expose a first-class **`Restaurar backup`** path on the initial bootstrap surface **before forcing creation of a disposable local user or disposable empty project**.

This explicitly supersedes the older #273 design requirement that first created a provisional Recovery Administrator and provisional/bootstrap project before restore.

C25 must not require throwaway user/project creation simply to reach recovery.

If implementation requires an internal bootstrap execution context to perform authenticated/authorized recovery, that mechanism must be designed inside C25 without turning it into a disposable product user/project and without weakening Authority security.

Consequently, the #273 provisional-Recovery-Administrator collision rule is historical design material, not a binding C25 requirement in its original form.

### 3.2 Restore surface

The first-use recovery surface must support:

1. selecting/importing/restoring the application package;
2. selecting/importing/restoring the Authority backup with the Authority backup's own password/credential mechanism;
3. optionally selecting/importing a license file.

A license is **not mandatory** to execute recovery.

Engineering Lock secret and Authority restore secret remain completely separate flows.

### 3.3 Separate recovery authorities are preserved from #273

The following #273 architecture survives the sequencing refinement and remains C25 input unless live architecture audit finds a required compatibility adaptation:

- `.escadapkg` remains application/project portability, not a monolithic whole-machine backup;
- application recovery uses the canonical package Inspect/Preview/Apply lifecycle and normal Save/Publish/Activate rules;
- Security Authority backup/restore is a separate security authority from `.escadapkg`;
- Database/Historian backup/recovery remains separate from `.escadapkg` and should use supported native database facilities rather than inventing a duplicate proprietary dump format;
- licensing/trust material is not silently absorbed into application or Authority backup formats.

### 3.4 Authority backup security properties preserved from #273

The Authority backup mechanism must be deliberately designed and security-reviewed. The #273 preparation establishes these required properties for revalidation/implementation:

- dedicated Authority Export / Inspect-or-Preview / Import-or-Restore flow;
- encrypted/authenticated backup protected by a user-supplied backup/master password or equivalent deliberate credential mechanism;
- password must not be stored as reusable plaintext;
- use a modern password-based KDF plus authenticated encryption, or a security-reviewed equivalent;
- wrong password, corrupted data, authentication failure or incompatible format must fail before Authority mutation;
- no universal vendor recovery password, hidden backdoor or embedded fallback secret;
- version the portable Authority backup schema;
- portable content may include local identities, password hashes plus algorithm metadata, roles, memberships/assignments and supported portable Authority configuration;
- never export plaintext user passwords;
- do not restore active sessions, cookies, access tokens, refresh tokens, runtime auth caches or other transient authentication grants;
- machine-bound license/trust material remains excluded unless a separate licensing contract explicitly permits portability;
- Preview/Inspect is non-mutating;
- Apply should be atomic/transactional where practical and must not silently partially succeed.

### 3.5 Recovery completion invariants preserved from #273

A restored installation must not be declared successfully recovered while obviously unusable.

C25 must define/validate at least the applicable equivalents of:

- Authority state is readable and internally consistent;
- at least one usable administrative identity exists after Authority restore;
- an Active production application exists before normal Runtime operation is considered restored;
- Runtime can mount the accepted Active application;
- blocking package/Authority compatibility failures remain explicit;
- database/Historian recovery state is explicit where such restore is part of the supported recovery procedure.

The exact finalization UX may differ from the old #273 Recovery Mode design because restore-first sequencing has changed.

## 4. Runtime session UX — preserved/adapted from #274

This is generic product UI. It is never authored HMI content and never EEE-specific.

### 4.1 System-owned operator session affordance

Runtime must provide a discreet, always-reachable system session control.

It must:

- show the current authenticated identity, preferring display name and falling back to username;
- expose a compact system-owned popup/overlay;
- remain outside authored Screen/Popup/Dynamo content and outside `.escadapkg` authority;
- remain reachable in normal Runtime and product fullscreen;
- be keyboard-accessible;
- avoid becoming dominant HMI chrome.

The popup exposes at least:

- current identity;
- `Trocar usuário`;
- `Sair`.

### 4.2 `Sair`

`Sair` must invalidate the server session, clear client-authenticated state and return to the supported authentication surface.

A failed logout must fail visibly. The UI must never claim logout succeeded while an old server session remains valid.

### 4.3 `Trocar usuário`

Switch-user is fail-closed:

1. invalidate the current session first;
2. immediately prevent Runtime interaction under the previous identity;
3. authenticate the next user through the supported authentication mechanism;
4. reload authenticated profile and effective capabilities from backend Authority;
5. update the visible current identity;
6. resume only surfaces authorized to the new identity.

During switch:

- old operator has zero interactive authority;
- project commands/buttons/navigation behind the auth surface are not actionable;
- cancelling/dismissing authentication must not resurrect the invalidated old session;
- cached frontend roles/capabilities are not authority.

After switch, authorization and audit attribution must agree with the newly displayed identity.

A Runtime-only user must never acquire Engineering, Diagnostics, Licensing, Audit or other privileged product surfaces merely through client navigation.

## 5. Contextual multilingual product manual — preserved/refined from #274

The contextual manual is a product surface, not a dump of internal Wave/ADR/handoff documents.

### 5.1 Binding delivery properties

- version-compatible with the installed EliteSCADA build;
- available locally/offline for normal contextual use where practical for industrial isolated networks;
- may also have an online mirror, but normal help must not require Internet access;
- stable language-neutral Help IDs;
- centralized resolver/registry from Help ID to installed manual route/topic;
- contextual links at section level and, where useful, field level;
- broken/missing Help IDs fail gracefully and should be detectable by automated validation.

### 5.2 Multilingual requirement is mandatory

The later Product Owner refinement on #274 is binding:

- the contextual manual is **mandatorily multilingual** for languages the shipped UI claims to support;
- it follows the active EliteSCADA UI locale automatically;
- the same language-neutral Help ID resolves to the semantically equivalent localized topic;
- locale changes preserve topic identity, including field-level and Driver-specific help;
- do not fall back to an unrelated manual home page merely because localization changed.

### 5.3 Required content depth

The manual must be derived from actual shipped product contracts and should cover, at minimum:

- Getting Started / first startup / authentication;
- Runtime operator guide and session behavior;
- Working -> Save -> Revision -> Publish -> Activate lifecycle;
- application package Import/Export/Inspect/Preview/Apply;
- Data Sources;
- TAGs, quality, timestamps, writeability, addressing, scaling vs presentation formatting;
- every production Driver and its real parameters/address syntax/limitations;
- Internal Memory and TAG Gateway;
- Scripts, actual available APIs, triggers/lifecycle, failure/safety semantics and validated examples;
- Alarms;
- Operational Events;
- Audit, kept semantically distinct from Alarm and Operational Event;
- Historian and Trends;
- Reports;
- Screens, Popups, Dynamos, bindings, commands and visual Runtime behavior;
- users/roles/capabilities/security;
- licensing;
- Backup/System Recovery once implemented;
- diagnostics and troubleshooting.

### 5.4 Driver documentation contract

Each production Driver should follow a consistent user-facing template covering the applicable items:

- purpose/use case;
- supported protocol/profile;
- Data Source fields;
- endpoint/device/station configuration;
- exact TAG addressing syntax;
- datatype mapping;
- bit access;
- byte/word ordering where relevant;
- read/write support and restrictions;
- quality/timestamp semantics;
- polling/subscription behavior;
- reconnect/timeouts;
- security/certificates where applicable;
- valid and invalid examples;
- diagnostics/troubleshooting;
- interoperability limits.

Driver inventory must come from the actual production registry/contracts, not a stale handwritten list.

### 5.5 Script documentation safety

Public Script examples must use only APIs actually shipped in the corresponding product version.

The manual must not invent convenience functions or document planned/historical APIs as implemented behavior.

## 6. Convergence disposition

After this contract is committed and cross-linked from #282/#283:

- PR #273 may close **without merge** as historical design preparation absorbed/revised by C25;
- PR #274 may close **without merge** as historical design preparation absorbed by C25, including its multilingual refinement;
- issue #280 may close as a completed **decision record**, while implementation remains active under #282/#283;
- their branches are not deleted by this cleanup;
- closing them does not mean the product features are complete.

## 7. Items that remain deliberately open

This convergence does **not** close:

- #282 / #283: active C25 implementation;
- #211: Wave 14 Product Owner validation umbrella;
- #212: Wave 14 integration PR, OPEN/DRAFT, no merge to `main` without explicit Product Owner authorization;
- #263: C11 implementation, preserved until C25 acceptance/integration;
- #266: C11 validation-only, NEVER MERGE;
- #208 / #210: Preview harness still needed for later exact-product visual homologation;
- #205 / #207: paused Wave 13 signing/release checkpoint;
- #178: deferred Siemens L4 external interoperability validation.

## 8. Permanent C25 boundaries

- GitHub live state is the sole project authority.
- Never modify `main` directly.
- #212 remains DRAFT and must not merge to `main` without later explicit Product Owner authorization.
- C25 integrates only into `wave14/corrections-integration` after exact-SHA acceptance.
- Do not sync C11 until C25 is accepted, integrated and post-merge revalidated.
- Never merge #266.
- No force push, destructive rebase or branch deletion.
- Diagnose red CI before rerun.
- Never weaken security, identity, lifecycle, package validation, licensing or Runtime authority for green CI.
- Backend Active revision remains Runtime application authority.
- Alarm / Operational Event / Audit remain distinct.
