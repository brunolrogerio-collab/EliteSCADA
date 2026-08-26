# Security, Authorization and Audit Baseline

Status: capability/audit foundation established; security policy engineering serialization is available in schema v6; JWT authentication and the first backend enforcement slice are implemented on the current security branch.

This document defines the security boundary used as authentication, user management and audit persistence are added to EliteSCADA. Security remains incremental: the presence of a trusted identity adapter and protected mutations does not mean every API surface or product workflow is already access-controlled.

## Principles

1. Role names are application configuration, not hard-coded security semantics.
2. Capabilities describe protected operations. A role only has the capabilities explicitly granted to it.
3. Authorization is enforced by the backend. Hiding or disabling a UI element is presentation behavior, never the security boundary.
4. Grants may be scoped to area, equipment, screen, TAG or command.
5. An authenticated identity that has no applicable grant is denied by default on protected operations.
6. Authentication secrets, password hashes, tokens and private keys are never Engineering Import/Export payloads.
7. Operational and engineering changes that affect the process or security model must produce audit events.
8. Runtime authorization policy must correspond to the active engineering revision, not an unsaved or draft workspace state.

## Initial capability vocabulary

The first stable capability vocabulary is:

- `View`
- `TagRead`
- `CommandExecute`
- `ProcessValueWrite`
- `AlarmAcknowledge`
- `AlarmShelve`
- `TrendUse`
- `TrendSave`
- `EngineeringModify`
- `UserRoleAdmin`
- `SystemAdmin`

These are capabilities, not roles. Applications remain free to define roles such as `operator`, `maintenance`, `night-shift`, `process-engineer` or any customer-specific hierarchy and assign grants to them.

`SystemAdmin` is not implicitly granted by a role merely because its name contains `admin`. Likewise, possessing `SystemAdmin` does not silently imply every other capability in the evaluator. Any hierarchy or inheritance must be explicit and reviewable.

## Engineering Schema v6

Schema v6 makes application authorization policy a first-class engineering entity through `securityRoles`.

Each security role contains:

- a stable optional ID;
- configurable key and display name;
- optional description;
- explicit capability grants;
- optional authorization scope per grant;
- non-secret metadata.

Each grant can be scoped by area, equipment path, screen key, TAG path and/or command key. The same role definition participates in normal Engineering Import/Export validation, preview, apply, project backup/restore, PostgreSQL revision persistence, checkout and revision lineage.

Role definitions contain authorization policy only. User credentials, passwords, password hashes, tokens, private keys and other authentication secret material are explicitly outside this engineering contract. Validation rejects metadata keys that appear to represent such secret material.

The demo application intentionally illustrates the distinction between command and setpoint authority:

- `operator`: view, TAG read, command execution, alarm acknowledgement and trend use;
- `developer`: every currently defined capability, each granted explicitly.

The demo `operator` role does not receive `ProcessValueWrite`. This demonstrates the intended product behavior where a user can be permitted to start/stop equipment while remaining unable to modify a process setpoint.

## Scoped grants

A capability grant may constrain any combination of:

- area;
- equipment path;
- screen key;
- TAG path;
- command key.

The baseline matcher supports exact case-insensitive values, `*` for any value and a trailing `*` for prefix scopes such as `Plant.Area1.*`.

Runtime authorization consumes the public, serializable policy model rather than editor-private state.

## TAG access policy interaction

TAG access policy remains a local override for TAG-specific operations:

- `ReadRoles = null`: inherit/fall back to the general `TagRead` capability policy.
- `WriteRoles = null`: inherit/fall back to the general `ProcessValueWrite` capability policy.
- `ConfigureRoles = null`: inherit/fall back to the general `EngineeringModify` capability policy.
- an empty role list means explicit deny for that TAG operation;
- a non-empty role list means at least one assigned role must appear in that list.

This preserves the schema-v5 distinction between `null` and `[]` and avoids an import/export round trip accidentally converting an explicit deny into inherited access.

## Authentication boundary

The API now has a first trusted identity adapter using ASP.NET JWT Bearer authentication. When `Authentication:Enabled=true`, the runtime validates a signed JWT before mapping it to the domain `SecurityPrincipal`.

The initial JWT configuration requires:

- `Authentication:Jwt:Issuer`;
- `Authentication:Jwt:Audience`;
- `Authentication:Jwt:SigningKey`, with at least 32 UTF-8 bytes for the current symmetric-key adapter.

Validation requires issuer, audience, cryptographic signature, signed tokens, lifetime and expiration. Role claims are mapped into configurable EliteSCADA role keys. The API distinguishes:

- `401 Unauthorized`: no trusted authenticated identity, invalid token or authenticated identity without a stable subject id;
- `403 Forbidden`: trusted identity exists, but none of its configured roles grants the requested capability/scope.

The signing key is deployment configuration and must be supplied through a protected configuration mechanism. Product signing secrets are never committed to the repository or placed in engineering packages. The Playwright suite uses an explicitly test-only key generated/configured for the test process.

This adapter is intentionally not a user database or token issuer. Login UI, local/external identity-provider integration, user lifecycle and credential management remain separate future work.

## Runtime policy resolution

When the simulation/demo fallback runtime is active, protected operations use the current demo/workspace policy registry.

When a persisted engineering runtime is active, authorization resolves security roles from the exact PostgreSQL Active Revision corresponding to the live runtime project and revision. The compiled policy is cached by project/revision. If persistence is unavailable, the active snapshot cannot be loaded, the revision does not match or the live runtime changes while policy is being resolved, authorization fails closed.

This prevents a draft role edit in the Engineering Workspace from silently changing authority over an already active process runtime.

## Phase-one backend enforcement

The first enforcement slice protects mutations with direct process or engineering impact:

- runtime TAG writes require the TAG access-policy write rule or inherited `ProcessValueWrite` capability;
- alarm acknowledgement requires `AlarmAcknowledge`, with the alarm area supplied as authorization scope;
- Engineering JSON apply requires `EngineeringModify`;
- Engineering TAG/Alarm/Data Source CSV apply requires `EngineeringModify`;
- `.escadapkg` project-package restore/apply requires `EngineeringModify`.

Alarm acknowledgement no longer trusts a caller-supplied user name as identity. The authenticated JWT principal becomes the actor; the old request-body field is retained temporarily only for compatibility and is ignored.

Read endpoints, Engineering preview/export, persistence save/publish/activate/checkout flows, WebSocket subscriptions and other operations are not all authorization-enforced yet. They must not be described as protected merely because phase-one mutation enforcement exists.

The browser E2E security suite proves separate behavior for a valid `developer`, valid but underprivileged `operator`, no credential and invalid Bearer credential.

## Audit baseline

The security project defines append-oriented audit events with:

- event id;
- UTC timestamp;
- stable subject id and optional display name;
- action;
- outcome (`Succeeded`, `Denied`, `Failed`);
- target kind and target id;
- optional non-secret details;
- optional correlation id.

Initial action keys include:

- `tag.write`
- `command.execute`
- `alarm.acknowledge`
- `alarm.shelve`
- `engineering.save`
- `engineering.publish`
- `engineering.activate`
- `user-role.manage`

The in-memory sink exists for development and tests. Product deployment requires append-only durable persistence, retention/query rules and authorization for viewing the audit trail.

Audit details must never contain passwords, authentication tokens, private keys or Data Source secrets.

## Next implementation slices

1. Extend backend capability enforcement to persistence save/publish/activate/checkout, command endpoints, alarm shelving, sensitive reads and WebSocket/runtime subscriptions where appropriate.
2. Persist audit events in PostgreSQL with append-only semantics.
3. Audit successful, denied and failed process/security mutations using the authenticated principal and correlation information.
4. Add user/role administration with explicit `UserRoleAdmin` authorization.
5. Add a real login/token-issuance or external identity-provider workflow without coupling credentials to Engineering Import/Export.
6. Add access-aware UI presentation while keeping backend authorization authoritative.
