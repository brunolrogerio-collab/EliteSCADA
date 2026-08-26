# Security, Authorization and Audit Baseline

Status: capability policy, Engineering Schema v7 security/operational-command engineering, JWT authentication, protected engineering/runtime mutations, trusted persistence-lifecycle actors, secured alarm shelving, secured first-class command execution, sensitive read/realtime authorization and durable append-only audit storage are implemented on the current security stack. User administration and token issuance remain future slices.

This document defines the security boundary used as identity, authorization, audit persistence and future user management are added to EliteSCADA.

## Principles

1. Role names are application configuration, not hard-coded security semantics.
2. Capabilities describe protected operations. A role only has the capabilities explicitly granted to it.
3. Authorization is enforced by the backend. Hiding or disabling a UI element is presentation behavior, never the security boundary.
4. Grants may be scoped to area, equipment, screen, TAG or command.
5. An authenticated identity that has no applicable grant is denied by default on protected operations.
6. Authentication secrets, password hashes, tokens and private keys are never Engineering Import/Export payloads.
7. Operational and engineering changes that affect the process or security model must produce audit events.
8. Runtime authorization policy must correspond to the active engineering revision, not an unsaved or draft workspace state.
9. Durable audit history is append-only at the database boundary, not merely by API convention.
10. Operational commands are engineered objects with stable identity and configured semantics; command permission never implies arbitrary process-value write permission.
11. Long-lived realtime connections must not outlive the trusted credential or bypass later policy evaluation.
12. Public health probes must reveal only service availability, not plant, project, driver or historian topology.

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

## Engineering Schema v7

Schema v6 made application authorization policy a first-class engineering entity through `securityRoles`. Schema v7 adds first-class operational `commands` while preserving backward-compatible parsing of older Engineering packages.

Each security role contains:

- a stable optional ID;
- configurable key and display name;
- optional description;
- explicit capability grants;
- optional authorization scope per grant;
- non-secret metadata.

Each grant can be scoped by area, equipment path, screen key, TAG path and/or command key. The same role definition participates in normal Engineering Import/Export validation, preview, apply, project backup/restore, PostgreSQL revision persistence, checkout and revision lineage.

Each operational command contains:

- a stable ID and stable command key;
- a display name and command kind;
- a target TAG reference by ID/path;
- an engineered command value compatible with the target TAG data type;
- optional area and equipment context used for authorization scoping;
- enabled state and non-secret metadata.

The initial command kind is `WriteTagValue`. It deliberately represents a pre-engineered operation, such as start/stop, rather than accepting an arbitrary value from the runtime caller. Engineering validation rejects missing targets, mismatched TAG references, read-only targets and command values that cannot be converted to the target TAG data type.

Role definitions contain authorization policy only. User credentials, passwords, password hashes, tokens, private keys and other authentication secret material are explicitly outside this engineering contract. Validation rejects metadata keys that appear to represent such secret material.

The demo application intentionally illustrates the distinction between command and setpoint authority:

- `operator`: view, TAG read, command execution, alarm acknowledgement and trend use;
- `developer`: every currently defined capability, each granted explicitly.

The demo `operator` role does not receive `ProcessValueWrite` or `EngineeringModify`. It may observe the runtime and execute engineered `demo.p01.start` / `demo.p01.stop` commands while remaining unable to submit arbitrary process values, modify process setpoints or retrieve Engineering configuration/diagnostics.

## Scoped grants

A capability grant may constrain any combination of:

- area;
- equipment path;
- screen key;
- TAG path;
- command key.

The baseline matcher supports exact case-insensitive values, `*` for any value and a trailing `*` for prefix scopes such as `Plant.Area1.*` or `plant.p01.*`.

Runtime authorization consumes the public, serializable policy model rather than editor-private state.

For command execution, the backend evaluates `CommandExecute` against the command's canonical area, equipment path, target TAG path and command key together. A command grant can therefore permit one equipment command family without granting command authority over another area/equipment/TAG.

## TAG access policy interaction

TAG access policy remains a local override for TAG-specific operations:

- `ReadRoles = null`: inherit/fall back to the general `TagRead` capability policy.
- `WriteRoles = null`: inherit/fall back to the general `ProcessValueWrite` capability policy.
- `ConfigureRoles = null`: inherit/fall back to the general `EngineeringModify` capability policy.
- an empty role list means explicit deny for that TAG operation;
- a non-empty role list means at least one assigned role must appear in that list.

This preserves the schema-v5 distinction between `null` and `[]` and avoids an import/export round trip accidentally converting an explicit deny into inherited access.

Operational command execution is intentionally separate from TAG access-policy write semantics. A caller executing a command does not obtain `ProcessValueWrite`; the command's engineered target/value is resolved from the active runtime command object and executed through the owning communication driver only after `CommandExecute` authorization succeeds.

## Authentication boundary

The API has a trusted identity adapter using ASP.NET JWT Bearer authentication. When `Authentication:Enabled=true`, the runtime validates a signed JWT before mapping it to the domain `SecurityPrincipal`.

The initial JWT configuration requires:

- `Authentication:Jwt:Issuer`;
- `Authentication:Jwt:Audience`;
- `Authentication:Jwt:SigningKey`, with at least 32 UTF-8 bytes for the current symmetric-key adapter.

Validation requires issuer, audience, cryptographic signature, signed tokens, lifetime and expiration. Role claims are mapped into configurable EliteSCADA role keys. The API distinguishes:

- `401 Unauthorized`: no trusted authenticated identity, invalid token or authenticated identity without a stable subject id;
- `403 Forbidden`: trusted identity exists, but none of its configured roles grants the requested capability/scope.

The signing key is deployment configuration and must be supplied through a protected configuration mechanism. Product signing secrets are never committed to the repository or placed in engineering packages. The Playwright suite uses an explicitly test-only key configured for the test process.

This adapter is intentionally not a user database or token issuer. Login UI, local/external identity-provider integration, user lifecycle and credential management remain separate future work.

When authentication is explicitly disabled for local development or CI smoke scenarios, authorization filters preserve the established local/no-auth workflow. This mode is not a trusted-identity deployment mode and caller-supplied lifecycle actor fields must not be interpreted as authenticated identity.

### Browser WebSocket bearer handling

Native browser WebSocket construction does not permit an application to set an arbitrary `Authorization` header. EliteSCADA therefore accepts an `access_token` query parameter only for the `/ws/tags` endpoint. Query-string bearer extraction is not enabled for normal HTTP routes.

A realtime socket is admitted only after normal JWT validation. The validated JWT expiration is then bound to the socket lifetime. The server closes the connection when that credential expires, and TAG authorization is re-evaluated for every outbound event. A long-lived connection therefore cannot become a permanent authorization snapshot.

Deployments must use TLS (`wss://`) for authenticated browser realtime traffic, and reverse-proxy/application access logging must avoid retaining query-string access tokens.

## Runtime policy resolution

When the simulation/demo fallback runtime is active, protected operations use the current demo/workspace policy registry.

When a persisted engineering runtime is active, authorization resolves security roles from the exact PostgreSQL Active Revision corresponding to the live runtime project and revision. The compiled policy is cached by project/revision. If persistence is unavailable, the active snapshot cannot be loaded, the revision does not match or the live runtime changes while policy is being resolved, authorization fails closed.

This prevents a draft role edit in the Engineering Workspace from silently changing authority over an already active process runtime.

Operational commands are compiled into the candidate runtime from the same published Engineering package. The candidate resolves each enabled command to its canonical active TAG and typed configured value before the revision can become active. Commands therefore swap atomically with the same runtime revision as drivers, TAGs and alarms.

The command endpoint performs an additional revision/project check between authorization and physical execution. If the active runtime changes during that window, the request fails with conflict and the operator must retry against the new active runtime rather than allowing an authorization decision from one revision to execute a command from another.

Realtime TAG authorization also resolves against the live runtime policy on each event, so an active-revision transition does not leave an existing socket permanently authorized under a stale policy.

## Backend enforcement

### Process and runtime mutation

- runtime TAG writes require the TAG access-policy write rule or inherited `ProcessValueWrite` capability;
- first-class operational command execution requires `CommandExecute` against area, equipment, target TAG and command-key scope;
- alarm acknowledgement requires `AlarmAcknowledge`, with the alarm area supplied as authorization scope;
- alarm shelving and unshelving require `AlarmShelve`, with the alarm area supplied as authorization scope.

`POST /api/commands/{id}/execute` never accepts a caller-supplied process value. It resolves the active command object by stable ID, authorizes its scopes, verifies that the active runtime did not change after authorization, then executes the command's pre-engineered value through the communication driver that owns the target TAG.

Alarm acknowledgement no longer trusts a caller-supplied user name as identity. The authenticated JWT principal becomes the actor; the old request-body field is retained temporarily only for compatibility and is ignored.

Alarm shelving is a runtime state, not merely a UI flag. While an alarm is shelved, its underlying process condition continues to be evaluated but it is excluded from the active alarm view/count. Unshelving restores the latest underlying alarm state. `ShelvedBy` is derived from the trusted principal, and alarms whose engineering definition has `ShelvingAllowed=false` reject shelving.

### Runtime read and realtime

When authentication is enabled:

- `/api/tags` and `/api/tags/current` return only TAGs allowed by `TagRead` / TAG access policy;
- TAG-by-path and historian reads require read authority for the requested TAG and return normal 401/403 failures rather than leaking the value;
- alarm instances and definitions are filtered to alarms whose associated TAG is readable and whose area is visible through `View`;
- `/ws/tags` requires a trusted JWT and sends an event only if the principal may currently read that TAG;
- realtime authorization failures fail closed and do not publish the event to that client;
- the WebSocket closes when the JWT used for the connection expires;
- `/api/drivers` and `/api/diagnostics/runtime` require runtime `EngineeringModify` authority because they expose technical runtime topology and diagnostics.

`GET /health` remains deliberately public but returns only service availability (`status` and service identity). Driver state, project/revision, TAG/alarm counts and historian information are available only through the protected diagnostics endpoint.

### Engineering and project data

When authentication is enabled, `EngineeringModify` protects Engineering configuration and lifecycle surfaces, including:

- Engineering workspace entities and command/security-role definitions;
- canonical JSON and CSV Engineering exports;
- Engineering import previews and applies;
- `.escadapkg` export, inspection, import preview and restore/apply;
- PostgreSQL persistence status, revision metadata, lifecycle/runtime metadata, previews, checkout, save, publish, activate and apply.

Engineering JSON/TAG/Alarm/Data Source apply and project-package restore/apply remain audited mutations. Ordinary Engineering reads/previews are authorization-enforced but intentionally do not generate one audit event per read; otherwise routine editor activity would turn the operational audit trail into noise.

Persistence save/publish/activate requests retain legacy `SavedBy`/`PublishedBy`/`ActivatedBy` body fields for compatibility, but when authentication is enabled those values are replaced before execution with the stable subject id from the trusted JWT principal. The client therefore cannot select the authoritative lifecycle actor.

### Administrative information

`GET /api/audit` requires `SystemAdmin`. User/role administration will require `UserRoleAdmin` when that subsystem is implemented.

## Browser and integration security coverage

The browser E2E security suite distinguishes:

- a valid `developer`;
- a valid but operationally constrained `operator`;
- an authenticated principal with no grants;
- no credential;
- an invalid Bearer credential.

Coverage proves process-read filtering, historian protection, alarm visibility, arbitrary TAG-write denial, command execution separation, alarm shelving authority, Engineering read/mutation protection, package export/inspect/preview protection, persistence actor anti-spoofing, audit-read protection and JWT-authenticated WebSocket behavior. Realtime tests also prove that a principal without `TagRead` receives no TAG events and that a valid realtime connection terminates after its JWT expires.

PostgreSQL-backed browser coverage exercises persisted save/publish/checkout/apply and verifies that caller-supplied lifecycle actor values cannot override the JWT subject. Command-domain unit/integration coverage verifies command scoping, Engineering validation and real Modbus-driver execution.

## Durable audit trail

Audit events carry:

- event id;
- UTC timestamp;
- stable subject id and optional display name;
- action;
- outcome (`Succeeded`, `Denied`, `Failed`);
- target kind and target id;
- optional non-secret details;
- correlation id derived from the API request trace identifier.

Current action keys include:

- `tag.write`
- `command.execute`
- `alarm.acknowledge`
- `alarm.shelve`
- `engineering.import.apply`
- `engineering.package.restore`
- `engineering.checkout`
- `engineering.save`
- `engineering.publish`
- `engineering.activate`
- `audit.read`
- `user-role.manage`

When `ConnectionStrings:EliteScada` is configured, audit events are stored in PostgreSQL under `elitescada.audit_events`. The database itself enforces append-only behavior through triggers that reject `UPDATE`, `DELETE` and `TRUNCATE`. Integration tests directly attempt all three operations and require PostgreSQL to reject them.

When PostgreSQL is not configured, the same public audit-store contract uses an in-memory implementation for local development and browser tests that do not require durable persistence.

The API records success, denial and operational failure for protected TAG writes, first-class command execution, alarm acknowledgement, alarm shelving/unshelving, Engineering import apply, project-package restore and persisted Engineering lifecycle mutations. Shelving and unshelving share the `alarm.shelve` audit action and identify the operation in non-secret audit details. Anonymous/invalid-token denied attempts use the stable audit subject `anonymous`; authenticated denied attempts retain the trusted JWT subject.

Command audit records identify the stable command key/id, kind and target TAG path but deliberately exclude the engineered process value. This prevents the audit metadata path from becoming a secondary store for sensitive process payloads while still preserving who attempted which engineered action and whether it succeeded, was denied or failed operationally.

`GET /api/audit` supports bounded filtering by subject, action, outcome and UTC time range and requires `SystemAdmin`. Attempts to read the audit trail are themselves audited.

Audit metadata deliberately excludes process values and import/package payloads. Detail keys that look like passwords, tokens, secrets, signing/private keys or authorization material are filtered before persistence.

Audit persistence errors are logged and do not change the result of an already executed process command. A future reliability slice may add a durable queue/outbox so temporary audit-storage outages cannot create gaps without falsely retrying physical operations.

## Next implementation slices

1. Add user/role administration with explicit `UserRoleAdmin` authorization.
2. Add a real login/token-issuance or external identity-provider workflow without coupling credentials to Engineering Import/Export.
3. Add audit retention/query policy and durable buffering/outbox behavior for temporary storage outages.
4. Add access-aware UI presentation while keeping backend authorization authoritative.
5. Continue hardening deployment-facing authentication concerns such as TLS/proxy guidance, token storage and query-string log redaction for browser realtime access.
