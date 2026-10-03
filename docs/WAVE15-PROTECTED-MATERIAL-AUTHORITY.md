# Wave 15 Protected Material Authority

Contract: `C-PROTECTED-MATERIAL-AUTHORITY-01`  
Order: `W15-FND-PROTECTED-MATERIAL-01`

## Decision

EliteSCADA now has one canonical **host-owned protected-material authority** for new
server-side product resources. The first required consumer is Media Source (#495).
The authority is infrastructure, not a browser-visible vault.

The accepted flow is:

`feature-owned admin write -> IProtectedMaterialAuthority -> encrypted host store -> opaque reference -> trusted server resolve`

A browser/client may receive only sanitized state such as
`credentialsConfigured=true`. It must never receive resolved material.

This lane deliberately does **not** migrate the existing Driver, HA or Remote Database
stores. Their current contracts remain compatible while new product resources converge
on this authority. Follow-up migration may use adapters without changing existing
references.

## Existing authorities audited

### Communication Drivers

`ICommunicationDriverProtectedMaterialResolver` is already strongly scoped by
Project/Data Source/Driver/Purpose/Reference and returns a short-lived zeroing lease.
Its production provider is environment-backed and its reference namespace is specific
to Driver/Data Source execution. It remains unchanged. It is not the Media Source
authority.

### High Availability

HA currently uses `IRuntimeHaDeploymentSecretStore` and
`RuntimeHaEncryptedFileDeploymentSecretStore`. It already protects the peer shared
secret with AES-256-GCM and keeps `ha-secret-v1:` compatibility. HA remains unchanged
in this lane; no HA reference migration is required.

### Remote Database

Remote Database currently uses `IDeploymentDatabaseSecretStore` and its own
AES-256-GCM file store. That implementation is also left intact to avoid destabilizing
the validated cutover/migration path. A future convergence seam is:

`resourceKind=RemoteDatabase / purpose=ConnectionCredential -> IProtectedMaterialAuthority`

No DB-A/DB-B feature behavior is changed here.

## Reference, scope and purpose

Canonical new references have the form:

`protected-material-v1:<32 lower-case hex characters>`

The identifier is random and opaque. It contains no username, password, key, resource
identity or purpose.

Resolution requires the complete expected scope:

- `ScopeOwnerKey`: e.g. `project:<project-key>` or another stable application owner;
- `ResourceKind`: bounded token, initially `MediaSource`;
- `ResourceId`: stable resource identity;
- `Purpose`: bounded token, e.g. `Password`, `BearerToken`, `ClientSecret`.

The envelope stores only a SHA-256 scope hash. The same scope hash is authenticated as
AES-GCM associated data. A valid reference presented for another resource or purpose
fails with `SCOPE_MISMATCH`.

One resource may own multiple protected fields because Purpose is part of the scope.

### Username policy

Username is ordinary non-secret connection metadata by default when the consuming
protocol/product treats it that way. A consumer may instead store it as separate
protected material with purpose `Username` when its deployment policy considers the
identity sensitive. Passwords, bearer tokens, client secrets and shared secrets are
protected material.

## Persistence and cryptography

Default ciphertext root:

`<application-base>/data/protected-material`

It is configurable with:

`ProtectedMaterial:Store:Path`

Each reference is stored as a separate versioned envelope:

- schema `elitescada.protected-material`;
- envelope version `1`;
- algorithm `AES-256-GCM`;
- opaque reference;
- scope hash;
- creation timestamp;
- random 96-bit nonce;
- ciphertext;
- 128-bit authentication tag.

The encryption key is exactly 32 bytes and is supplied as Base64 by deployment. It is
never generated silently and never persisted beside ciphertext.

Default environment variable:

`ELITESCADA_PROTECTED_MATERIAL_KEY`

Alternative deployment configuration may point at a mounted key file:

`ProtectedMaterial:Store:ProtectionKeyFile`

The configured key file is rejected if it is inside the protected-material ciphertext
directory.

The authority has no production in-memory fallback.

## Windows, Linux and containers

### Windows service

The service reads the deployment key from the service environment or a separately
protected key file. The design does not depend on an interactive user profile, Windows
Credential Manager or DPAPI, because the same authority must operate on Linux and in
containers. Deployment/install tooling must grant the EliteSCADA service identity
exclusive filesystem access to the configured store/key locations.

### Linux/headless service

The authority does not require a desktop keyring. Newly created ciphertext directories
are restricted to owner read/write/execute and files to owner read/write
(`0700` / `0600`). The service account must own the paths. The key may come from the
service environment or a separate mounted secret file.

### Container/OCI

Persist the ciphertext directory on a durable host volume. Inject the key through the
container secret mechanism/environment or mount it as a secret file outside that
volume. Replacing the application container must preserve both the ciphertext volume
and the deployment key. The key must not be baked into an image.

If a host has protected material but the key is absent, operations fail with
`KEY_UNAVAILABLE`. A wrong key does not delete, overwrite or regenerate anything;
authenticated decryption fails with `PROTECTED_MATERIAL_UNREADABLE`.

Automatic deployment-key rotation/re-encryption is intentionally not implemented in
this Wave 15 slice. Until explicit rotation tooling exists, deployments must preserve
the active key.

## Lifecycle

### Store

An authorized feature-owned admin flow passes bytes and complete resource scope.
`StoreAsync` returns only the opaque reference/configured state.

### Resolve

Only trusted server code receives a short-lived `IProtectedMaterialLease`.
The material buffer is cleared when the lease is disposed. Callers must not cache,
persist or log it.

Managed .NET cannot promise perfect zeroization of every temporary/runtime copy.
The authority therefore minimizes copies and clears the buffers it owns rather than
claiming impossible heap-wide zeroization.

### Replace/rotate

Replacement creates a **new** reference after first proving that the existing
reference still resolves for the expected scope. The old reference is deliberately
retained.

The consumer owns this transaction:

1. store replacement;
2. atomically persist/accept the new reference in its own configuration;
3. switch the active/session setup to that accepted configuration;
4. delete the superseded reference when rollback/running-lifecycle rules permit.

This prevents a failed configuration update from destroying credentials still used by
the running resource and also makes cleanup ownership explicit.

### Delete

Delete requires the same authorized mutation context and exact scope. The authority
first validates/decrypts the target; a wrong key or wrong scope therefore cannot delete
material. Resource-scoped material is non-shared by default.

## Authorization and audit

The core has no public `/api/secrets` endpoint.

Feature-owned APIs must perform their normal administrative authorization and pass the
result into `ProtectedMaterialMutationContext`. Store/Replace/Delete fail closed for
an unauthenticated or denied decision.

Audit records only:

- actor/roles;
- operation (store/replace/delete);
- resource kind/id;
- purpose;
- authorizing capability.

Audit never contains material, ciphertext, key, or opaque reference.

Resolve is an internal runtime operation and is not separately audited per frame/read;
consumers should resolve during connection/session establishment, not a continuous hot
path.

## Error and health model

Sanitized error codes include:

- `NOT_CONFIGURED`;
- `INVALID_REFERENCE`;
- `REFERENCE_NOT_FOUND`;
- `SCOPE_MISMATCH`;
- `KEY_UNAVAILABLE`;
- `PROTECTED_MATERIAL_UNREADABLE`;
- `STORE_UNAVAILABLE`;
- `UNAUTHORIZED`;
- `CREDENTIAL_REQUIRED`.

Health reports only `Ready`, `KeyUnavailable`, `StoreUnavailable`, or
`CorruptConfiguration` plus a sanitized code. No reference or material is exposed.

## Project export, .escadapkg and cross-host import

Protected material is host-owned, not canonical Engineering truth.

A project/package/export must not copy:

- plaintext material;
- encrypted envelope bytes;
- deployment key;
- host-specific opaque reference.

For a protected field it exports a dependency descriptor
(ResourceKind/ResourceId/Purpose + credential-required state). Importing on another
host therefore produces `CREDENTIAL_REQUIRED` until an administrator supplies new
material on that host.

A **full deployment backup** is operationally different from project export. It may
back up the encrypted store together with the deployment key only through an
independently secured backup process. This lane does not build a protected backup
transport.

## Media Source seam

A Media Source consumer should use:

- owner: `project:<project-key>`;
- resource kind: `MediaSource`;
- resource id: stable Media Source ID, e.g. `CAMERA-01`;
- purpose: `Password` (and optionally a separate `Username`) or `BearerToken`.

Engineering/admin submits new credentials write-only. The server stores material and
persists only the opaque reference. Public Media Source DTOs expose only configured
state. The server-side relay resolves the credential when establishing the upstream
camera connection. Browser URLs/tokens must not contain upstream username/password.

This foundation does **not** implement Media Source, RTSP relay, FFmpeg/GStreamer,
Video Player or PDF Viewer.

## Compatibility and future convergence

- HA `ha-secret-v1:` references remain valid and unchanged.
- Communication Driver protected-material behavior remains unchanged.
- Existing Data Source `secretReferences` schema remains unchanged.
- Remote DB credential behavior remains unchanged.
- New server-side product resources should use this authority rather than introducing
  Camera/Database/Connector-specific stores.
- Future adapters may converge HA/Driver/Remote DB behind this primitive after dedicated
  migration/compatibility review.

## Known limitations

- no enterprise Vault/KMS integration;
- no automatic protection-key rotation/re-encryption;
- no migration of existing HA/Driver/Remote DB references;
- Windows ACL provisioning remains deployment/installer responsibility;
- full deployment backup/restore remains an operational concern;
- managed runtime zeroization is best-effort for buffers owned by this authority.
