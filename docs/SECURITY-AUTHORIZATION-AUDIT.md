# Security, Authorization and Audit Baseline

## Status

The current `main` security stack includes:

- capability-based authorization;
- Engineering Schema v7 Security Roles and Operational Commands;
- trusted JWT authentication;
- local identity/browser login;
- protected local user administration;
- protected engineering/runtime mutations;
- trusted persistence-lifecycle actors;
- secured alarm shelving;
- secured first-class command execution;
- sensitive read/realtime authorization;
- durable append-only Audit storage;
- bounded Audit query/keyset pagination;
- configurable Audit retention;
- bounded asynchronous in-memory Audit outage buffering;
- protected Audit diagnostics.

Relevant merged checkpoints include PRs #35–#39 and, for the latest hardening, PR #42 secured Engineering mutations, PR #44 Audit durability/query/retention foundation and PR #45 Audit runtime integration.

## Principles

1. Role names are application configuration, not hard-coded security semantics.
2. Capabilities describe protected operations. A role only has capabilities explicitly granted to it.
3. Authorization is enforced by the backend. UI hiding/disabling is presentation only.
4. Grants may be scoped to area, equipment, screen, TAG or command.
5. Authenticated identity with no applicable grant is denied by default.
6. Authentication secrets, password hashes, tokens and private keys are never Engineering Import/Export payloads.
7. Operational/Engineering changes that affect process or security state are auditable.
8. Runtime authorization policy corresponds to the Active Engineering revision, not an unsaved draft.
9. Durable Audit history is append-only at the storage boundary, not merely by API convention.
10. Operational Commands are engineered objects; command authority never implies arbitrary process-value write authority.
11. Long-lived realtime sessions must not outlive trusted credentials or bypass current policy evaluation.
12. Public health probes reveal service availability only, not plant/project/driver/historian topology.
13. Audit metadata is structural and must not become a secondary store for process values, secrets or request payloads.

## Capability vocabulary

Current stable capability vocabulary includes:

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

These are capabilities, not roles. Customer/application roles remain configurable.

No capability hierarchy is implicit merely from a role/capability name. `SystemAdmin` does not silently imply unrelated capabilities unless the policy explicitly grants them.

## Engineering Schema v7 security and command model

Engineering Security Roles contain stable identity/key/display information, explicit capability grants, optional scopes and non-secret metadata.

Operational Commands contain stable identity/key, display name, command kind, target TAG reference, engineered command value, optional area/equipment context, enabled state and non-secret metadata.

The initial command kind `WriteTagValue` represents a pre-engineered operation rather than caller-supplied arbitrary process data.

Engineering validation rejects invalid/missing command targets, incompatible command values and secret-like policy metadata.

Local identities remain separate from Engineering policy. Users reference role keys; the active Engineering revision remains authoritative for runtime capabilities/scopes.

## Scoped grants and TAG access policy

A capability grant may constrain any combination of:

- area;
- equipment path;
- screen key;
- TAG path;
- command key.

TAG access-policy semantics remain:

- `ReadRoles = null`: inherit/fall back to general `TagRead`;
- `WriteRoles = null`: inherit/fall back to general `ProcessValueWrite`;
- `ConfigureRoles = null`: inherit/fall back to general `EngineeringModify`;
- empty role list: explicit deny;
- non-empty list: at least one current role must match.

Operational command execution remains separate from TAG arbitrary-write semantics.

## Authentication boundary

When authentication is enabled, EliteSCADA validates trusted signed JWTs before mapping them to the domain security principal.

Current browser/local identity flow includes:

- PostgreSQL-backed local users;
- PBKDF2-SHA256 password storage;
- first-user/bootstrap flow;
- JWT issuance;
- HttpOnly browser cookie support;
- token security-version invalidation after sensitive user changes;
- active WebSocket revocation/termination behavior where applicable.

Authentication secrets and signing keys are deployment configuration and never Engineering payloads.

The API distinguishes:

- `401 Unauthorized`: no valid trusted authenticated identity;
- `403 Forbidden`: trusted identity exists but lacks the required capability/scope.

Native browser WebSocket bearer handling may use the explicitly supported `/ws/tags` access-token path. Deployments must use TLS and avoid retaining query-string credentials in logs.

## Runtime policy resolution

When a persisted Engineering runtime is active, authorization resolves security roles from the exact Active Revision corresponding to the live runtime project/revision and fails closed if that trustworthy snapshot cannot be resolved.

Operational commands are compiled from the same Engineering revision as runtime TAGs/drivers/alarms. Command execution checks that runtime revision identity has not changed between authorization and execution.

Realtime TAG authorization is evaluated against current runtime policy rather than becoming a permanent authorization snapshot at socket-open time.

## Backend enforcement

### Process/runtime mutation

- TAG writes require TAG write policy or inherited `ProcessValueWrite`;
- Operational Commands require `CommandExecute` against canonical scopes;
- Alarm acknowledgement requires `AlarmAcknowledge`;
- Alarm shelving/unshelving requires `AlarmShelve`;
- Engineering Apply/Delete/Bulk and lifecycle mutations require appropriate Engineering authority, principally `EngineeringModify` for current mutation surfaces.

`POST /api/commands/{id}/execute` never accepts an arbitrary caller process value. It resolves the active engineered command and executes only its configured operation after authorization.

### Engineering mutations

Current merged mutation safety includes:

- backend-authoritative Preview/Apply;
- Workspace change-version/CAS preconditions;
- mutation serialization;
- explicit dependency-aware Delete;
- no implicit delete-by-omission;
- no cascade deletion;
- safe selected-entity Bulk Preview/Apply;
- structural Audit for success/denial/failure.

### Runtime read/realtime

When authentication is enabled:

- TAG reads/history require read authority;
- alarm visibility is filtered through TAG/area visibility;
- `/ws/tags` requires trusted authentication and current TAG read authority;
- technical runtime/driver diagnostics require protected Engineering/system authority according to current endpoint rules;
- `/health` remains intentionally minimal/public.

### Engineering/project data

Protected Engineering configuration/lifecycle surfaces include canonical Engineering reads/exports, previews/applies, project packages, persistence/revision lifecycle operations and secured mutation endpoints.

Caller-supplied lifecycle actor names are not trusted when authentication is enabled; authoritative actor identity comes from the authenticated principal.

### User administration

Protected local-user administration is implemented and uses explicit administrative authority, including current last-admin safety and token/session invalidation semantics.

## Durable Audit model

Audit events include:

- stable event ID;
- UTC timestamp;
- stable subject ID and optional display name;
- action;
- outcome (`Succeeded`, `Denied`, `Failed`);
- target kind/ID;
- optional non-secret structural details;
- request correlation ID;
- optional trusted structural context such as area/project/revision/roles/source where available.

Current action vocabulary includes, among others:

- `tag.write`
- `command.execute`
- `alarm.acknowledge`
- `alarm.shelve`
- `engineering.import.apply`
- `engineering.delete`
- `engineering.bulk.apply`
- `engineering.package.restore`
- `engineering.checkout`
- `engineering.save`
- `engineering.publish`
- `engineering.activate`
- `audit.read`
- user/security administration action keys.

The official Engineering bulk action key is `engineering.bulk.apply`; competing `engineering.bulk-edit` vocabulary was removed during integration.

## Audit storage and sanitization

When PostgreSQL is configured, events are stored in `elitescada.audit_events`.

Database triggers enforce ordinary append-only behavior by rejecting unauthorized `UPDATE`, `DELETE` and `TRUNCATE` operations.

Storage-boundary sanitization is defense-in-depth and removes/redacts sensitive metadata patterns, including password/token/secret/private-key/API-key/authorization/credential/cookie/hash/salt-like material and bearer/JWT-like values.

Metadata counts/key/value lengths and similar collections are bounded.

Audit deliberately does not record spontaneous process-value changes and protected command/TAG mutation audit does not copy process values into metadata.

## Audit query

`GET /api/audit` remains protected by `SystemAdmin` in the current baseline.

The endpoint preserves its established array response contract while internally using bounded keyset pagination.

Supported query semantics include bounded page size and filters for relevant fields such as:

- UTC interval;
- subject;
- action;
- outcome;
- target kind/id;
- area;
- correlation ID.

Newest-first deterministic ordering uses `(timestampUtc, eventId)` keyset semantics.

When another page exists, the opaque cursor is returned through:

`X-EliteSCADA-Audit-Next-Cursor`

Invalid cursors fail explicitly rather than silently changing query meaning.

Audit reads are themselves auditable.

## Audit retention

Retention is configurable and remains disabled by default.

Semantics:

- disabled means no retention deletion;
- enabled with no `MaximumAge` means explicit indefinite retention;
- only events strictly older than the UTC cutoff are eligible;
- deletion runs oldest-first in bounded batches;
- each run has a bounded maximum batch count;
- finite configured retention runs through the server hosted-service integration;
- no manual purge-all endpoint is provided.

Controlled retention uses the narrow storage mechanism designed for that purpose and does not disable the ordinary append-only database protection generally.

## Audit outage buffering

Current writes use a bounded asynchronous `BufferedAuditSink` around the underlying store.

The buffer provides:

- bounded capacity;
- temporary-failure retry;
- explicit overflow rejection;
- bounded shutdown flush;
- health counters/diagnostics.

This is **in-memory outage protection**, not a persistent outbox.

A process crash while events remain buffered can still create an Audit gap. No claim of crash-durable buffering should be made until a separate persistent disk/database outbox design is implemented and validated.

Audit persistence failure does not cause an already executed physical process command to be blindly retried.

## Protected Audit diagnostics

`GET /api/audit/diagnostics` is currently protected by `SystemAdmin` and exposes bounded operational health for the Audit store, buffer and retention configuration/state without weakening Audit read authority.

Diagnostics/logging must not leak arbitrary storage exception messages or environment secrets.

## Browser/integration security coverage

Current CI/browser coverage exercises, among other things:

- developer/operator/no-grant/anonymous/invalid credential distinctions;
- runtime read filtering;
- historian/alarm protection;
- arbitrary TAG-write denial;
- Operational Command separation;
- alarm shelving authority;
- Engineering read/mutation protection;
- lifecycle actor anti-spoofing;
- Audit-read protection;
- authenticated WebSocket behavior;
- secured Engineering Apply/Delete/Bulk behavior;
- Audit buffered-write visibility;
- keyset cursor pagination;
- Audit target filtering;
- invalid cursor rejection;
- protected Audit diagnostics.

## Still not implemented / future security work

The current merged baseline does not claim:

- persistent crash-durable Audit outbox;
- manual Audit purge-all;
- general Audit UI;
- a weaker dedicated `AuditRead` capability replacing `SystemAdmin`;
- complete enterprise/external identity-provider integration;
- every deployment hardening concern around reverse proxies/TLS/key management.

Any future persistent Audit outbox or fail-closed process/Audit coupling requires explicit reliability design because blindly retrying physical operations is unsafe.
