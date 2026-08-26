# EliteSCADA Local Identity

## Purpose

The local identity subsystem supplies trusted browser/API identity for deployments that do not use an external identity provider.

It is deliberately separate from the public Engineering model.

- **Local user account** = authentication identity, password credential, enabled state and assigned role keys.
- **Engineering Security Role** = versioned authorization policy containing capabilities and scopes.
- The user account references role keys, but credentials/password hashes are never serialized through Engineering Import/Export or `.escadapkg`.

This preserves the existing rule that project Engineering can be exported, revised and restored without carrying authentication secrets.

## Password storage

Local passwords are never stored in plaintext.

Current baseline:

- PBKDF2-HMAC-SHA256;
- random 32-byte salt per credential;
- 32-byte derived hash;
- default 210,000 iterations;
- constant-time verification;
- minimum password length of 12 characters.

The hashing contract is implementation-owned authentication state, not Engineering configuration. A future migration to another password-hashing algorithm must preserve explicit upgrade/migration behavior rather than reinterpret stored hashes silently.

## Persistence

When `ConnectionStrings:EliteScada` is configured, local accounts are stored in PostgreSQL table:

`elitescada.local_users`

The table stores identity/profile fields, role keys and password salt/hash/iteration metadata.

Without the PostgreSQL connection string, the local identity store is in-memory and is suitable only for local/development operation.

## First-user bootstrap

Local authentication is opt-in:

```text
Authentication:Enabled=true
Authentication:Local:Enabled=true
```

An empty local identity store requires a bootstrap user through deployment configuration:

```text
Authentication:Local:Bootstrap:Username
Authentication:Local:Bootstrap:DisplayName
Authentication:Local:Bootstrap:Password
Authentication:Local:Bootstrap:Roles:0
```

The bootstrap credential is used only to create the first persistent user when the store is empty. Deployment configuration containing the bootstrap password must be removed after successful initialization.

The bootstrap password must never be committed to the repository or included in a project package.

## Browser session

Successful local login creates the same signed JWT format already trusted by the backend authorization layer.

For same-origin browser use, the JWT is stored in an **HttpOnly** cookie. Product JavaScript cannot read the token value.

Default cookie name:

`elitescada_access`

Properties:

- HttpOnly;
- SameSite=Strict;
- Path=/;
- Secure by default;
- expiration aligned to the signed JWT expiration.

`SecureCookie=false` exists only for explicit non-TLS local/test environments. Production-style deployment must use TLS and secure cookies.

Normal `Authorization: Bearer` API tokens remain supported. Browser WebSocket query tokens also remain supported only on `/ws/tags` for clients that cannot use the cookie. An explicit Bearer header or explicit realtime query token takes precedence over the local cookie.

## Endpoints

Public configuration discovery:

- `GET /api/auth/config`

Local-authentication endpoints when local login is enabled:

- `POST /api/auth/login`
- `POST /api/auth/logout`

Authenticated identity introspection remains:

- `GET /api/auth/me`

Login failures return a generic unauthorized result so username existence is not disclosed through the response shape.

Login attempts are bounded per remote-address/process window to reduce trivial brute-force attempts. This is a baseline application guard, not a replacement for deployment-level reverse-proxy/firewall/rate-control policy.

## Audit

Successful and denied login attempts are written as authentication audit events without password/token material.

Logout is also audited.

The existing audit sanitizer continues to remove detail fields whose keys imply passwords, tokens, authorization headers, signing keys or secrets.

## Authorization

Authentication does not grant capability by itself.

After JWT validation, the normal EliteSCADA authorization layer evaluates the token's role keys against the exact active Engineering role policies when runtime authority is required. Unknown/removed role keys therefore do not create implicit permissions.

Frontend visibility remains presentation only; protected API/runtime operations remain backend-authoritative.

## Future user administration

The next identity slice adds permission-controlled user lifecycle operations such as:

- list/create users;
- enable/disable accounts;
- change display profile;
- assign configured role keys;
- password reset/change workflows;
- audit all administrative changes;
- Engineering UI administration guarded by `UserRoleAdmin`.

Those operations must continue to keep credential state outside Engineering packages.
