# LAST CHANGE — EliteSCADA

> Operational handoff. Read with `PROJECT GOAL.md`, `docs/ROADMAP.md`, `docs/PARALLEL-WORK.md` and `docs/CHAT-WORK-ASSIGNMENTS.md` before every EliteSCADA task.

**Handoff date:** 2026-08-26
**Development state:** **ACTIVE — TAG GATEWAY ENGINEERING + OPC UA DISCOVERY/IMPORT RESEARCH**

Repository truth is separated into **MERGED**, **IMPLEMENTED IN PR** and **SPECIFIED / NOT IMPLEMENTED**.

## CURRENT CHECKPOINT

Internal Memory complete product integration remains official `main` state.

Coordinator PR #49, **Integrate Internal Memory into engineering runtime**, was merged as:

`18e0c5f55bf33958d040ed3a2f2b948e34d3cc5f`

Its reviewed final head was:

`f12e4dc7b65f4ab41f47f26aca779dcd9aa0fde9`

Validation is complete at both required levels:

- PR CI #296: Web build **SUCCESS**, backend build/tests **SUCCESS**, runtime smoke **SUCCESS**, Chromium E2E **SUCCESS**;
- post-merge `main` CI #297 on `18e0c5f55bf33958d040ed3a2f2b948e34d3cc5f`: Web build **SUCCESS**, backend build/tests **SUCCESS**, runtime smoke **SUCCESS**, Chromium E2E **SUCCESS**.

Therefore Internal Memory complete product integration is **MERGED / COMPLETE** and no longer blocks TAG Gateway.

## MERGED PRODUCT STATE ADDED BY PR #49

PR #48 remains the public Engineering/durable-retention foundation. PR #49 completes its coordinator-owned product/runtime integration.

Official `main` includes:

- Engineered `builtin.memory.server` Data Sources/TAGs composed into the shared Server Memory runtime provider;
- Server Memory registration in normal TAG registry/current-cache/Event-Bus paths;
- shared realtime and Alarm Engine participation through normal TAG events;
- Historian capture honoring explicit `historian.enabled` while preserving legacy TAG behavior when the metadata is absent;
- generic TAG writes and Operational Commands routed to Server Memory without pretending it is a communication driver;
- PostgreSQL Server Memory retention wired into actual API/runtime startup before persisted runtime recovery;
- stable-ID retention across restart/path rename and fail-closed incompatible-type semantics;
- explicit retained-value reset requiring confirmation, authorization and Audit action `server-memory.retention.reset`;
- transactional/serialized PostgreSQL retention-schema initialization with narrow retry for known concurrent DDL collisions;
- Client Memory definitions exposed only to authorized runtime clients while mutable values remain page/tab-local and never server-global;
- exact Int64 browser handling through decimal strings and signed-range validation;
- practical Internal Memory initial/default-value Engineering UI using the canonical Preview/Apply + Workspace CAS path;
- Client Memory rejection from server-side Operational Commands in Engineering Preview;
- no fake Internal Memory network diagnostics.

Mutable Server Memory retained values remain separate from immutable/versioned Engineering revisions/packages.

## DEFECTS CAUGHT DURING FINAL INTEGRATION

Two real defects were discovered by final CI and fixed rather than hidden:

1. CI #294 exposed a React lifecycle regression where the optional Internal Memory TAG panel could throw `ReferenceError: Cannot access 'invalidate' before initialization` in ordinary projects with no memory TAGs. The panel is now mounted only when actual memory TAGs exist, preserving the existing structured TAG editor.
2. CI #295 exposed a PostgreSQL schema-initialization race. `pg_advisory_xact_lock` had been executed without one encompassing explicit transaction, so the lock could be released before DDL. Initialization now executes under an explicit transaction with narrowly bounded retry for known concurrent DDL collisions.

CI #296 and post-merge CI #297 validate both corrections.

## SOURCE/PROTOCOL GATE STATUS

The locked functional sequence remains:

`Internal Memory -> TAG Gateway -> common multi-driver diagnostics -> USER INTERFACE VALIDATION PREVIEW -> additional external protocols`

Current functional state:

- Internal Memory: **MERGED / COMPLETE**;
- TAG Gateway: **ACTIVE LOCKED BLOCK — FIRST ENGINEERING SLICE ASSIGNED TO DEV 2**;
- common multi-driver diagnostics: **SPECIFIED / NOT IMPLEMENTED — BLOCKED BY GATEWAY**;
- interface validation preview: **SPECIFIED / NOT IMPLEMENTED — BLOCKED BY DIAGNOSTICS**;
- production MQTT/OPC UA/BACnet and later external protocols remain blocked by the preceding gates.

A non-production research/specification spike for a later protocol is permitted when it does not register runtime protocol behavior or alter the active implementation order.

## OPC UA PRODUCT DIRECTION ADDED

The product owner requested that the future OPC UA driver include an Engineering experience comparable to and improved from the Elipse E3 OPC UA workflow rather than requiring manual NodeId entry.

`docs/OPC-UA.md` now locks the intended direction, including:

- manual endpoint plus standard OPC UA server/endpoint discovery;
- an opt-in, bounded and cancellable **Scan network for OPC UA devices/servers** workflow;
- endpoint/security/authentication inspection;
- explicit certificate trust and fail-closed unexpected server identity changes;
- connection test before import;
- lazy address-space tree browser;
- search/filter for large servers;
- multiple selection and subtree import candidates;
- canonical Engineering Preview/Apply for TAG imports;
- native subscription/monitored-item update profiles;
- NodeId plus namespace-aware BrowsePath/namespace URI identity so imported nodes can be safely re-resolved;
- Refresh/Re-resolve Node IDs with preview rather than blind rebinding;
- Rescan/diff for new/missing/changed nodes without silent deletion of EliteSCADA TAG Engineering or historian data;
- explicit unsupported/lossy data-type handling;
- official OPC Foundation UA .NET Standard client stack as the primary technical candidate to evaluate.

This direction was informed by the current Elipse E3 manuals for OPC UA server selection/endpoints, multiple TAG import, subscriptions and NodeId refresh/re-resolution, while deliberately choosing stronger certificate-trust behavior for EliteSCADA.

## ACTIVE WORKER ASSIGNMENTS

### DEV 2 - EliteSCADA

Continues to own the first functional TAG Gateway slice:

- CurrentTask: `Public TAG Gateway Engineering contract and deterministic validation foundation`;
- Branch: `feature/tag-gateway-engineering`;
- Status: `ASSIGNED`;
- exclusive narrow permission for Gateway-related changes in `src/Scada.Engineering/Contracts/EngineeringContracts.cs` plus required Gateway import/export integration;
- no Gateway runtime engine, API/DI, frontend, diagnostics UI or protocol-specific driver work in this slice;
- AfterCompletion: `WAIT_FOR_COORDINATOR`.

### DEV 1 - EliteSCADA

Now owns a parallel non-production research spike:

- CurrentTask: `OPC UA discovery, address-space browse and TAG-import Engineering research spike`;
- Branch: `research/opc-ua-discovery-import`;
- Status: `ASSIGNED`;
- MustRead includes `docs/OPC-UA.md`, common diagnostics, interface-validation milestone, Engineering import/export ADR and TAG Gateway architecture;
- deliverable is research/specification only, with sourced Elipse E3/OPC Foundation analysis and concrete EliteSCADA recommendations;
- no production OPC UA networking/runtime, Data Source registration, OPC UA production dependency, central DI/API, DriverHost runtime, central Engineering contracts or production frontend UI;
- AfterCompletion: `WAIT_FOR_COORDINATOR`.

### DEV 3 - EliteSCADA

Remains `MERGED / WAITING`. Canonical Script package/schema integration is intentionally deferred while DEV 2 owns overlapping central Engineering contract files.

## COORDINATOR RESUME POINT

On the next coordinator `siga`:

1. reread mandatory docs from current `main`;
2. inspect the real `feature/tag-gateway-engineering` branch/PR/head/diff/CI for DEV 2;
3. inspect the real `research/opc-ua-discovery-import` branch/PR/head and research deliverable for DEV 1;
4. enforce that DEV 1 remains research-only and does not introduce production OPC UA runtime work;
5. enforce DEV 2 ownership of Gateway central Engineering contract changes;
6. merge functional Gateway Engineering only after public/versioned contract validation/compatibility tests and relevant full CI are green;
7. review DEV 1 OPC UA findings and fold approved decisions into the future OPC UA implementation assignment after the external-protocol gate opens;
8. only after Gateway Engineering is official on `main`, assign the protocol-independent Gateway runtime engine slice;
9. keep DEV 3 idle unless a clearly non-conflicting new assignment is explicitly recorded.

## Permanent continuity rules

- GitHub branch/PR/head/CI state is operational truth.
- Open feature branches/PRs are **IMPLEMENTED IN PR** or **RESEARCH IN PR**, never **MERGED**.
- Workers never choose their own next task or merge their own PR.
- Shared central integration belongs to the coordinator unless a narrow exception is explicitly assigned.
- Known-failing work is never merged.
- `siga` is the canonical short command; `continue` is equivalent.