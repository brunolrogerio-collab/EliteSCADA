# Wave 14 C25 — Restore-first / System Recovery Architecture

**Status:** BINDING C25.4 IMPLEMENTATION ARCHITECTURE  
**Tracking:** #282 / PR #283  
**Checkpoint:** C25.4  
**Audit product SHA:** `a5fb9959fa15c060f51417de7bea84a5eec11e5b`  
**C25.3 ledger close:** `db73782083cd8cea9cd0318a653b175027cefa86`

This document converts the binding Restore-first contract into an implementation architecture grounded in the live C25 code. It does not mark C25.4 complete and does not authorize integration.

## 1. Binding outcome

A genuinely fresh/clean installation must expose **Restore backup** before forcing creation of a disposable local user or empty project.

Recovery must preserve three distinct authorities:

1. **Application authority** — canonical EliteSCADA `.escadapkg` plus normal Engineering persistence/lifecycle;
2. **Security Authority** — a dedicated protected Authority backup containing portable local identity state;
3. **Database/Historian authority** — separate database-native recovery, not absorbed into `.escadapkg` or Authority backup.

Licensing is a fourth, optional installation concern. A license may be supplied during recovery, but it is not required to restore Authority or application state and is not embedded into either backup format.

Engineering Lock, Authority login passwords and the Authority backup password remain separate secret domains.

## 2. Current code facts confirmed on `a5fb9959...`

### 2.1 Empty-install admission already exists

`LocalIdentityApi` and `LocalIdentityConfiguration` already fail closed around anonymous first-run:

- local identity storage must be durable;
- local identity count must be zero;
- persisted Engineering project catalog must be empty;
- persisted projects with no identities block anonymous bootstrap rather than reopening an anonymous administration path.

C25.4 will reuse this exact security boundary for anonymous Restore-first admission. It must not create a generic unauthenticated recovery endpoint on populated installations.

### 2.2 Current first-use UI is project-first

`AuthGate` currently creates the first Administrator and then, when the project catalog is empty, forces **Create New Project**.

C25.4 must replace/refine that sequencing:

- true empty first-use offers Restore backup before forcing initial Administrator creation;
- an authenticated Administrator with an empty project catalog is offered application restore before Create New Project;
- Create Administrator / Create New Project remain valid new-install paths, but are no longer mandatory detours before recovery.

### 2.3 Application package machinery is reusable

`ProjectPackageEndpoints` / `ProjectPackageService` already provide bounded package reading, integrity validation, Inspect, Preview and Apply with optimistic Workspace version checks.

C25.4 must reuse those package contracts. It must not create a second `.escadapkg` parser or weaken normal package validation.

Applying a package only mutates Working. A completed recovered application must additionally be durably saved, published and, when the configured Runtime binding matches, activated through the existing lifecycle services.

`SaveFirstProjectAsync` is **not** suitable after package restore because it intentionally clears Working and seeds a new empty first project. Recovery instead uses the normal `SaveCurrentDerivedAsync` root-revision path after the restored package has been applied.

### 2.4 Authority backup does not exist yet

`ILocalIdentityStore` currently supports initialization, mutation serialization, count/list/find/create/update. No backup/export/preview/replace contract exists.

Portable local identity state currently consists of:

- stable account ID;
- username + normalized username;
- display name;
- enabled state;
- role-key assignments;
- password verifier salt/hash/iteration metadata;
- creation/update timestamps.

Plaintext passwords are neither required nor permitted in the Authority backup.

### 2.5 Existing PostgreSQL mutation lease is insufficient for restore

`PostgreSqlLocalIdentityStore.AcquireMutationLeaseAsync` takes a PostgreSQL advisory transaction lock. Existing `CreateAsync` and `UpdateAsync`, however, create their own commands/connections and therefore do not form one replace-all transaction under that lease.

C25.4 must add a dedicated atomic replace/apply primitive. Sequential delete/create operations across independent commands are not an acceptable restore architecture.

### 2.6 Sessions are deliberately non-portable

Local JWT validation re-resolves the restored user and validates enabled state plus account `UpdatedAtUtc` version. Cookies, JWTs, realtime connections and other transient grants are not Authority backup data and must never be restored.

### 2.7 Licensing is separate

`ProductLicensingApi` owns license status/request/install/remove. Recovery may call the existing licensing service for an optional supplied license after core recovery validation, but license failure must not silently corrupt or masquerade as failure of an otherwise valid Authority/application restore.

## 3. Authority backup v1

### 3.1 Envelope

Introduce a versioned JSON envelope with explicit cryptographic metadata. Proposed logical contract:

- format: `elitescada.authority-backup`;
- formatVersion: `1`;
- createdAtUtc;
- KDF metadata:
  - algorithm `PBKDF2-SHA256`;
  - version `1`;
  - deliberate iteration count at or above the product's reviewed password-hardening floor;
  - random 32-byte salt;
- encryption metadata:
  - algorithm `AES-256-GCM`;
  - version `1`;
  - random 12-byte nonce;
  - 16-byte authentication tag;
- ciphertext.

All binary fields are Base64 in the JSON envelope.

The user-supplied Authority backup password derives only the Authority-backup encryption key. It is never stored, never treated as an Authority login password, never treated as an Engineering Lock password and never used as a vendor recovery secret.

No fixed/compiled encryption key or universal fallback credential is permitted.

### 3.2 Encrypted payload

The encrypted payload is separately versioned and contains:

- payload schema/version;
- export timestamp;
- local user accounts;
- role-key assignments attached to each account;
- password verifier metadata required to preserve the users' existing login passwords.

Each password credential is labeled with its supported algorithm contract and contains salt/hash/iteration metadata only.

The payload excludes:

- plaintext passwords;
- JWTs/access tokens/refresh tokens;
- cookies;
- active sessions;
- websocket/realtime grants;
- runtime authorization caches;
- license/trust material;
- `.escadapkg` application content;
- Historian/process data.

## 4. Authority backup validation and apply

### 4.1 Decode / Inspect / Preview

Wrong password, malformed envelope, unsupported version/algorithm, invalid Base64, AES-GCM authentication failure or malformed decrypted payload must fail before Authority mutation.

Preview performs all structural validation without mutating the store, including:

- unique non-empty account IDs;
- unique normalized usernames;
- normalization matches username;
- valid display names/timestamps;
- enabled-state validity;
- normalized role assignments;
- supported password verifier algorithm and minimum hardening parameters;
- valid salt/hash lengths;
- at least one enabled account assigned the product's initial administrative role key `developer`.

Preview returns only safe summary data. It never returns password hashes, salts, derived backup keys or decrypted secret material.

### 4.2 Atomic Authority replace

Add a dedicated store contract, e.g. `ReplaceAllAsync`, whose implementation guarantee is atomic replacement after full validation.

- **In-memory:** build and validate a complete replacement dictionary first, then swap state under the store lock.
- **PostgreSQL:** open one connection + transaction, take the existing Authority mutation advisory lock, validate/prepare, delete/insert all local users through that same transaction, then commit once. Any failure rolls back the complete replacement.

Existing per-user create/update APIs remain unchanged for normal administration.

## 5. Administrative identity invariant

Authority owns identities and role-key assignments; the Engineering application owns role definitions/capabilities. C25.4 must not duplicate application security-role definitions into the Authority backup.

The bootstrap convention establishes `developer` as the initial administrative role key and First Project grants it all currently defined capabilities. Authority Preview therefore requires at least one enabled restored identity assigned `developer`.

Final System Recovery validation additionally cross-checks the restored application security model so an enabled restored identity's assigned role resolves to the administrative capabilities required by the product. If the restored application removes/weakens the expected administrative role, recovery is not declared complete.

## 6. Restore-first admission modes

### 6.1 Anonymous clean-install recovery

Anonymous recovery is permitted only while the server can prove the same true-empty condition used by secure first-run:

- Authentication/local restore support enabled and durable;
- identity count = 0;
- persisted project catalog empty.

The condition is rechecked immediately before Apply under the relevant mutation gates. A race that creates either an identity or project closes anonymous recovery and returns conflict/forbidden rather than continuing.

Anonymous recovery is a narrowly scoped bootstrap execution context, not a user identity and not a reusable bearer/session credential.

### 6.2 Authenticated recovery

Once Authority exists, recovery uses normal authenticated backend Authority and capability checks. This supports application replacement/restore and future Authority restore by a legitimate administrator without opening anonymous access.

## 7. Multi-authority recovery sequence

A full clean-install recovery may select:

1. Authority backup + Authority backup password;
2. application `.escadapkg`;
3. optional license.

All selected inputs are inspected/previewed before any mutation.

Because Authority and Engineering project persistence are separate authorities/stores, C25.4 does not pretend they can be made one database transaction in every deployment. Instead it uses deterministic staged recovery with atomicity inside each authority and explicit stage results.

### 7.1 Apply order

For a local-auth clean install:

1. revalidate true-empty bootstrap admission;
2. atomically replace Authority from the already validated encrypted backup;
3. apply the already validated `.escadapkg` into Working through the canonical package service;
4. persist the restored Working package as a root Engineering revision using the package manifest project key/name;
5. publish that revision through normal publication validation;
6. require `EngineeringRuntime:ProjectKey` to be configured and to match the restored project before declaring Runtime restoration complete;
7. activate through the existing published-runtime activation service;
8. optionally install the supplied license through the existing Licensing service;
9. run final recovery invariants.

Authority is applied before project persistence because the inverse failure mode could create a project while identities remain empty, intentionally closing anonymous bootstrap and leaving the installation stranded. If a later application stage fails after Authority commit, the response reports an explicit partial recovery state and the restored Administrator can authenticate to continue/remediate. Such partial state must never be reported as successful recovery.

### 7.2 Application-only authenticated restore

If a legitimate Administrator already exists but no project exists, the UI may Restore application instead of forcing Create New Project. The normal authenticated package/persistence/publish/activate contracts are used; no Authority backup is required in this mode.

## 8. Runtime binding and completion

Recovery must not claim normal Runtime restoration merely because a package was parsed.

Completion requires at minimum:

- Authority readable and internally consistent;
- at least one enabled usable administrative identity;
- application root revision durably saved;
- revision published successfully;
- configured Runtime project key present and matching restored project key;
- activation successful and durable Active revision recorded;
- Runtime descriptor consistent with durable Active;
- package/Authority compatibility failures explicit;
- optional license result explicit;
- DB/Historian recovery status explicitly outside this application/Authority transaction unless separately performed.

A missing/mismatched `EngineeringRuntime:ProjectKey` is a blocking recovery-completion condition, not something C25.4 silently rewrites in deployment configuration.

## 9. UI sequencing

### True empty install

Initial first-use surface exposes two explicit choices without forcing either first:

- **Restore backup**;
- **Create Administrator** for a new installation.

Restore UI supports Authority backup + password, application package and optional license. It presents Preview before Apply and stage-specific failures.

### Administrator exists / no persisted project

The current forced Create New Project screen becomes a choice between:

- **Restore application**;
- **Create New Project**.

No throwaway project is required to reach Import/Restore.

## 10. Security boundaries

C25.4 must not:

- expose anonymous recovery after identities or persisted projects exist;
- authenticate a restored user using the Authority backup password;
- automatically create a disposable/provisional product user;
- use Engineering Lock secret for Authority recovery;
- bypass canonical `.escadapkg` Inspect/Preview/Apply validation;
- bypass publication validation or Runtime activation checks;
- restore sessions/tokens/cookies;
- copy machine-bound license/trust state into Authority/application backup;
- invent a universal recovery key;
- silently partially succeed.

## 11. Implementation slices

### C25.4-A — Authority backup domain + crypto

- versioned envelope/payload DTOs;
- PBKDF2-SHA256 key derivation;
- AES-256-GCM encryption/authentication;
- safe Inspect/Preview;
- malformed/wrong-password fail-before-mutation tests.

### C25.4-B — atomic Authority store replace

- store-level atomic replace contract;
- in-memory implementation;
- PostgreSQL single-transaction implementation;
- duplicate/invalid/admin-invariant tests;
- rollback tests.

### C25.4-C — Authority backup API

- authenticated Export/Preview/Apply;
- clean-install bootstrap Preview/Apply guarded by the exact empty-install predicate;
- audit without password/credential leakage.

### C25.4-D — application recovery coordinator

- package Inspect/Preview reuse;
- Apply -> root Save -> Publish -> Activate;
- runtime binding checks;
- final recovery invariants;
- explicit partial-stage result if a later authority fails.

### C25.4-E — Restore-first UI + browser tests

- Restore backup on initial bootstrap surface;
- Restore application vs Create New Project after Administrator exists;
- optional license field/step;
- no disposable user/project prerequisite;
- fresh-install and authenticated-empty-project E2E coverage.

## 12. Validation policy

Focused C25.4 validation must include:

- Authority crypto/unit tests;
- in-memory atomic replace tests;
- PostgreSQL transactional replace/rollback tests;
- anonymous admission tests for empty vs populated identity/project combinations;
- full clean-install recovery browser/API path;
- authenticated application-only restore path;
- wrong backup password/corrupt backup/malformed package fail-before-relevant-authority mutation;
- no session/token portability;
- restored application lifecycle reaches Active only when runtime binding matches;
- no License requirement for core recovery.

Final C25 acceptance still belongs to C25.8 and requires the complete exact-SHA product matrix. This C25.4 architecture does not authorize #283 integration and does not alter C11.