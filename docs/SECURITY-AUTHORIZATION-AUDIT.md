# Security, Authorization and Audit Baseline

Status: capability/audit foundation established; security policy engineering serialization added in schema v6. API authentication and enforcement remain pending.

This document defines the security boundary that will be used as authentication, user management and audit persistence are added to EliteSCADA. The existence of security contracts does not by itself make the current runtime API authenticated.

## Principles

1. Role names are application configuration, not hard-coded security semantics.
2. Capabilities describe protected operations. A role only has the capabilities explicitly granted to it.
3. Authorization is enforced by the backend. Hiding or disabling a UI element is presentation behavior, never the security boundary.
4. Grants may be scoped to area, equipment, screen, TAG or command.
5. Once enforcement is enabled, an authenticated identity that has no applicable grant is denied by default.
6. Authentication secrets, password hashes, tokens and private keys are never Engineering Import/Export payloads.
7. Operational and engineering changes that affect the process or security model must produce audit events.

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

The capability evaluator accepts an authenticated principal containing a stable subject id and assigned role keys. It does not authenticate credentials itself.

A future API authentication adapter will be responsible for establishing that principal from a trusted identity mechanism. Until that adapter exists and endpoints are migrated, EliteSCADA must not describe the current API as access-controlled merely because the capability evaluator and serializable policies exist.

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

1. Add a trusted authenticated-principal provider to the API.
2. Enforce capabilities on TAG reads/writes, commands, alarm actions and engineering mutations.
3. Persist audit events in PostgreSQL with append-only semantics.
4. Audit successful, denied and failed process/security mutations.
5. Add user/role administration with explicit `UserRoleAdmin` authorization.
6. Add browser tests proving UI visibility and backend enforcement are independent.
