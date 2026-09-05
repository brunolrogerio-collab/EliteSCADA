# Wave 14 — Post-DEMO System Recovery / Backup & Restore

**Status:** PRODUCT OWNER DESIGN LOCKED — NOT IMPLEMENTED  
**Recorded:** 2026-09-05 BRT  
**Authority:** Product Owner decision during Wave 14 C11 canonical EEE DEMO closure  
**Implementation timing:** post-DEMO product work; do not fold this into the current C11 Simulation DEMO package  

## 1. Purpose

Define how a new or replacement EliteSCADA installation can be recovered from available backups without turning `.escadapkg` into a monolithic machine backup.

The recovery problem is not only “can each subsystem export a file?”. The required product answer is:

> Given a fresh EliteSCADA installation and valid backup material, what controlled procedure restores an operational system with application, authority and persistent data?

The locked design is a **System Recovery Mode** that orchestrates separate backup authorities.

## 2. Recovery authorities remain separate

EliteSCADA must preserve the separation between these authorities:

### 2.1 Application / Engineering

Use the existing normal project package mechanism:

- `.escadapkg` remains the application/project package;
- normal Export / Inspect / Preview / Import surfaces remain authoritative;
- Save -> Publish -> Activate remains the normal lifecycle after restoration;
- `.escadapkg` must not silently absorb local user credentials, Historian samples, host secrets or licensing material.

No new proprietary “whole system project package” is required.

### 2.2 Database / Historian and other database-backed persistent data

Use the database platform’s supported native backup/restore mechanisms.

For the current PostgreSQL/TimescaleDB-based deployments this means the database backup/restore facilities already provided by that platform, subject to a documented and supported EliteSCADA recovery procedure.

EliteSCADA may later provide Administration guidance/orchestration around these operations, but it must not invent a second proprietary database dump format merely to wrap functionality the database already provides.

Recovery validation must still verify that the restored database is compatible with the current EliteSCADA installation/project identity and must report failures instead of silently accepting partial or cross-project data.

### 2.3 Security Authority / Center of Authority

Provide a dedicated **encrypted Export / Preview / Import** capability for the authority/security state.

This is the new backup format required by this design.

It is independent from `.escadapkg` and from the native database/Historian backup.

## 3. Encrypted Authority Backup

### 3.1 Export password

Export requires the administrator to enter an **export/master password**.

The password is used to derive the encryption key for the Authority Backup. It must not be stored as a reusable plaintext secret by EliteSCADA.

Implementation must use a modern password-based key derivation mechanism and authenticated encryption. A memory-hard KDF such as Argon2id plus an AEAD construction such as AES-256-GCM, or an equivalent security-reviewed mechanism, is the expected baseline.

The password itself must not be used directly as raw encryption key material.

### 3.2 Import password

Import requires the same master password.

Wrong password, corrupted ciphertext, failed authentication tag or incompatible format must fail **before any authority mutation occurs**.

There is no universal EliteSCADA recovery password, vendor backdoor or hidden fallback key.

If the master password is lost, the encrypted Authority Backup cannot be recovered by design.

### 3.3 Authority Backup content

The encrypted payload is expected to include the portable authority configuration necessary to recreate access control, including as applicable:

- local users/identities;
- password hashes and their algorithm/parameter metadata, never plaintext user passwords;
- roles;
- role memberships / user-role assignments;
- configurable capabilities/permissions belonging to the authority;
- local authentication configuration required for normal operation;
- portable provider configuration only where it is explicitly safe and supported.

The exact schema must be versioned.

### 3.4 Explicit exclusions

Do not export transient or machine-bound security state as normal portable authority data, including:

- active sessions;
- cookies;
- access tokens;
- refresh tokens or equivalent active authentication grants;
- runtime authentication caches;
- ephemeral cryptographic material;
- machine-bound licensing/trust material unless a separate licensing contract explicitly permits portability;
- plaintext user passwords.

After recovery, users authenticate again normally.

## 4. Fresh-install Recovery Bootstrap

### 4.1 Entry condition

On a fresh/uninitialized EliteSCADA installation, before a normal operational authority/application exists, the product must provide a controlled **Recovery Bootstrap** path.

The first-use flow creates:

1. a **provisional Recovery Administrator**;
2. a **provisional/bootstrap project or workspace** sufficient for the normal Administration and Engineering surfaces to operate.

The bootstrap project is temporary recovery scaffolding, not a production application.

### 4.2 Recovery Administrator

The Recovery Administrator is created locally during first use with a new username/password chosen on the recovered installation.

During an active Recovery Session this account:

- has the administrative capability required to perform recovery;
- cannot be removed by an imported Authority Backup;
- cannot have its password/hash replaced by imported authority data;
- cannot lose the administrative access required to finish or safely abort recovery;
- remains locally authoritative while Recovery Mode is active.

## 5. Authority import collision rule — bootstrap user has priority

This rule is PRODUCT OWNER LOCKED.

If the Authority Backup contains a user that resolves to the same recovery identity/username as the newly created Recovery Administrator:

- the imported user entity **is not imported over the local Recovery Administrator**;
- the newly created Recovery Administrator has priority;
- its newly created credential/password remains authoritative;
- no imported password hash may replace it;
- the collision must be shown explicitly in Authority Import Preview;
- imported data must not silently reduce or remove the bootstrap administrator’s access.

The later implementation may map non-destructive role/membership information to the surviving local identity only if that behavior is explicit in Preview and cannot remove the required recovery administration capability. The imported user record itself never replaces the bootstrap user.

If there is no collision, imported users are restored normally subject to validation.

## 6. Authority Restore Preview

Authority Import must have a Preview stage before Apply.

At minimum Preview should report:

- backup format/version;
- number of users;
- number of roles;
- number of memberships/assignments;
- relevant authentication/provider configuration entries;
- compatibility errors/warnings;
- bootstrap-user collisions;
- users that will be created;
- users that will be skipped because the local Recovery Administrator has priority;
- whether at least one usable administrative identity will remain after recovery.

Example semantic result:

- 18 users found;
- 1 collision with Recovery Administrator;
- `bruno` -> imported user skipped; local recovery identity preserved;
- 7 roles;
- 42 memberships;
- 0 blocking errors.

No mutation occurs during Preview.

## 7. Fresh-machine recovery procedure

The supported recovery path is:

### Step 1 — Install EliteSCADA

Install the product and required database/runtime dependencies on the target machine.

### Step 2 — Enter first-use / Recovery Bootstrap

Because the installation has no operational authority/application, EliteSCADA starts the controlled bootstrap flow.

### Step 3 — Create provisional Recovery Administrator

Create the new local administrative identity and password.

This account is protected for the duration of the Recovery Session.

### Step 4 — Create/open provisional bootstrap project/workspace

Provide the minimum valid workspace required for normal Administration and Engineering recovery surfaces.

### Step 5 — Restore Security Authority

- select the encrypted Authority Backup;
- enter the backup master password;
- decrypt and authenticate the package;
- Inspect/Preview the authority restore;
- show collisions and exclusions;
- Apply only after validation succeeds.

The Recovery Administrator retains priority on collision.

### Step 6 — Restore application/project

Use the normal `.escadapkg` surface:

- Inspect;
- Import Preview;
- Apply;
- Save;
- Publish;
- Activate.

The restored production project replaces the need for the provisional bootstrap project.

### Step 7 — Restore database/Historian persistent data when required

Use the database platform’s supported native restore mechanism according to the EliteSCADA recovery procedure.

This can include Historian and other persistent database-backed operational records that are intentionally outside `.escadapkg`.

The implementation/documentation must define safe service stop/start, compatibility checks and validation for the actual supported deployment topology.

### Step 8 — System recovery validation

Before finalization, EliteSCADA must verify the restored installation sufficiently to prevent an obviously unusable system from being declared recovered.

Minimum checks should include, as applicable:

- authority is readable and internally consistent;
- at least one verified usable administrator exists;
- Recovery Administrator still has required access until finalization;
- an Active production project exists;
- Startup Screen / startup application identity is valid;
- required Data Sources/TAG definitions exist;
- Runtime can mount the Active project;
- database/Historian connectivity is healthy where configured;
- no blocking package/authority/database compatibility error remains.

### Step 9 — Finalize System Recovery

Provide an explicit **Finalize System Recovery** action.

Finalization:

- exits Recovery Mode;
- removes the special bootstrap protection semantics;
- removes/discards the provisional bootstrap project when it is no longer needed;
- leaves the restored production project as the normal application authority;
- leaves the restored Security Authority as the normal access-control authority;
- requires at least one verified administrative identity to remain usable.

The product must never finalize recovery into a state with zero usable administrators.

## 8. Disposition of the provisional Recovery Administrator

After successful restoration:

### 8.1 Collision existed

If the bootstrap username/identity collided with a user in the Authority Backup, the **newly created local Recovery Administrator remains the surviving authoritative user for that identity**.

The imported duplicate user is not restored over it.

### 8.2 No collision existed

If the Recovery Administrator does not correspond to a restored authority user, finalization may allow either:

- keeping it as an additional administrator; or
- removing it after another restored administrator has been explicitly verified usable.

Removal must be blocked if it would leave the system without a usable administrator.

## 9. Recovery Session safety rules

The later implementation must preserve these invariants:

1. wrong backup password never partially mutates authority state;
2. corrupted/tampered Authority Backup never partially mutates authority state;
3. Preview is non-mutating;
4. bootstrap Recovery Administrator cannot be overwritten by import;
5. recovery cannot finalize with zero usable administrators;
6. `.escadapkg` remains application-only;
7. database restore remains an explicit separate authority/procedure;
8. licensing/trust material is not made portable implicitly;
9. transient sessions/tokens are not restored;
10. every restore failure is explicit; no silent partial success.

Where practical, Apply should be atomic or provide a transactional rollback boundary.

## 10. Administration UX direction

A future **Administration -> Backup & Recovery** area may present the system coherently while preserving separate formats underneath.

Conceptually it may expose:

- Application Export / Import (`.escadapkg`);
- Security Authority encrypted Export / Preview / Import;
- Database/Historian backup/restore guidance or supported orchestration;
- Recovery status and compatibility checks;
- first-use / System Recovery Mode when the installation is uninitialized.

The UI must not imply that any one of these files alone is a complete system backup.

## 11. What “System Backup” means after this decision

Wave 14 should no longer treat the post-DEMO gap as “invent a single complete EliteSCADA backup file”.

The intended architecture is:

**Application authority**  
-> existing `.escadapkg`

**Security authority**  
-> new encrypted Authority Backup protected by a user-supplied master export password

**Historian/database authority**  
-> supported native database backup/restore

**System Recovery Mode**  
-> controlled bootstrap and orchestration that joins those authorities again on a fresh installation

This is the definition of the future **Backup/Restore** capability.

## 12. Implementation boundary

This document is a product/design lock for later implementation.

It does **not** authorize changing the current C11 Simulation DEMO, does not alter the accepted `.escadapkg` application responsibility and does not authorize any merge to `main`.

When implementation begins it must be treated as generic EliteSCADA product work with focused tests for:

- encrypted Authority Backup round-trip;
- wrong-password/tamper rejection;
- version compatibility;
- Preview non-mutation;
- bootstrap-user collision priority;
- fresh-install recovery flow;
- administrator-survival invariant;
- application package restore in Recovery Mode;
- database/Historian recovery validation boundaries;
- finalization and cleanup of provisional bootstrap state.
