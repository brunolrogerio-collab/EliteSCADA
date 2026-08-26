# LAST CHANGE — EliteSCADA

> Operational handoff. Read with `PROJECT GOAL.md`, `docs/ROADMAP.md` and `docs/PARALLEL-WORK.md` before every EliteSCADA task and update before every final response.

**Handoff date:** 2026-08-26
**Development state:** **ACTIVE — PARALLEL WORK ENABLED**

Repository truth is separated into **MERGED**, **IMPLEMENTED IN PR**, and **SPECIFIED / NOT IMPLEMENTED**.

## MERGED

### PR #35 — Add first-class operational command domain

- merge commit `2fd568976fc6277d0b069adeeb560f6ea3d8205f`;
- Engineering Schema v7 and first-class Operational Commands are official state.

### PR #36 — Protect runtime read and realtime surfaces

- merge commit `10b0320149c1ef2109e9517539717a8800b200c2`;
- protected TAG/historian/alarm/Engineering/diagnostic reads, authenticated `/ws/tags`, per-event authorization, JWT expiration and minimal public `/health` are official state.

### PR #37 — Engineering UI foundation and localization

- merge commit `4553aa7ab5ba7e05a209a7c8462286d1a34a1ad6`;
- `/engineering` is official platform UI while `/` remains Runtime HMI;
- Runtime↔Engineering navigation and `pt-BR` / `en` / `es` localization are on `main`;
- structured TAG, Data Source and Alarm editors are on `main`;
- those editors still use preview-oriented mutation behavior until the secured Apply/Delete/bulk slice is merged.

### PR #38 — Local identity and browser login foundation

- merge commit `2a581d279a428cb605429d5939c333ff7ad8d1b4`;
- tested head `bdd2003230570387328f5a4083c9c47b9817af50`;
- CI #167 fully green.

Merged identity behavior includes PBKDF2-SHA256 local credentials, PostgreSQL user persistence, JWT issuance, HttpOnly browser cookie, bootstrap-first-user, login/logout/profile and shared Runtime/Engineering browser authentication. Credentials remain outside Engineering Import/Export; users reference role keys while active Engineering security remains authoritative for capabilities/scopes.

### PR #39 — Protected local user administration

- final integrated head `95462b40cbb2910f291ca5522cb16dcfabac5729`;
- final CI #183 fully green:
  - Web build: success;
  - Backend build/test/runtime smoke: success;
  - Chromium end-to-end: success;
- merged into `main` as `6de8f06a443ad829ccc95c6dfcd9511e906adeff`.

Official merged administration behavior now includes:

- protected local-user list/create/update/password-reset endpoints;
- Engineering role catalog for user assignment;
- role keys validated against Engineering security roles;
- administration through active Runtime policy using `UserRoleAdmin` or `SystemAdmin`;
- safe user DTOs that never expose hash/salt/credential material;
- protection against removing/disabling the last enabled local administrator, evaluated against active Runtime policy rather than an unpublished Workspace draft;
- local JWT security-version validation so profile, enabled state, role or password changes invalidate older local sessions;
- active realtime sessions for a changed user are revoked with WebSocket `1008 PolicyViolation`;
- Runtime treats explicit identity revocation as a session-revalidation event rather than reconnecting indefinitely;
- Engineering > Security user administration UI with `pt-BR`, `en`, `es`;
- Chromium coverage for lifecycle, safe DTOs, JWT/session invalidation, realtime revocation and password reset.

### Permanent architecture/documentation consolidated on `main`

Official locked specifications include:

- `docs/INTERNAL-MEMORY-TAGS.md`;
- `docs/TAG-GATEWAY.md`;
- `docs/COMMUNICATION-DRIVER-DIAGNOSTICS.md`;
- `docs/INTERFACE-VALIDATION-MILESTONE.md`;
- `docs/PYTHON-SCRIPTING-AND-VISUAL-RUNTIME.md`;
- `docs/PARALLEL-WORK.md`.

Permanent gates remain:

`internal memory -> TAG Gateway -> common multi-driver diagnostics -> USER INTERFACE VALIDATION PREVIEW -> additional external protocols`

and, before the final graphical editor:

`Python scripting/property contracts -> script editor/sandbox -> visual runtime object/property API -> graphical Screens/Popups/Dynamos editor`.

## IMPLEMENTED IN PR

### PR #40 — Internal Memory / Source Provider foundation

- state: **OPEN / DRAFT / NOT MERGED**;
- branch: `feature/internal-memory-foundation`;
- head at this handoff: `77990fd161580f2e70de941632e5398dfac5c6bd`;
- PR was created from the parallel-work checkpoint before PR #39 merged and therefore must be reconciled with current `main` before integration;
- CI #184 is running at this handoff.

Implemented in PR includes:

- protocol-neutral `ISourceProvider` boundary;
- `builtin.memory.server` and `builtin.memory.client` provider descriptors;
- strict typed memory values/defaults for current TAG data types;
- stable TAG-ID retention semantics;
- `IServerMemoryRetentionStore` plus deterministic in-memory store;
- server-memory restoration across compatible restart/revision and stable-ID path rename;
- incompatible retained type fails closed without silent coercion;
- stale retention cannot enumerate/resurrect deleted TAGs;
- normal memory quality is `Good` without fabricated network diagnostics;
- per-Runtime-Client Client Memory isolation;
- focused automated tests.

Coordinator integration still required for public Engineering schema/import-export, runtime composition/cache/event wiring, durable production retention, historian/alarm rules and authorization/audit boundaries.

### PR #41 — Python scripting + visual property foundation

- state: **OPEN / DRAFT / NOT MERGED**;
- branch: `feature/python-scripting-foundation`;
- head at this handoff: `49ac730d1e43d60fc3fddc852f985a46b3113a28`;
- base already includes PR #39 merge (`6de8f06a443ad829ccc95c6dfcd9511e906adeff`);
- CI #186 is running at this handoff.

Implemented in PR includes:

- typed public visual-property definitions and per-object schemas;
- common geometry/transform/visibility/fill/stroke/text/image property groups;
- immutable Engineering base values separated from Runtime presentation overrides;
- deterministic precedence `Engineering base -> binding/expression -> script -> animation`;
- renderer-facing tween contracts with duration/easing/repeat/ping-pong/replace/cancel/completion semantics;
- explicit Client Visual vs Server Python script scopes and capability surfaces;
- sandbox boundary contracts denying arbitrary filesystem/OS/shell/network/database/driver/secrets/DOM/storage access;
- bounded execution policy/cancellation/queue/error-isolation contracts;
- Python validation/editor diagnostic contracts with line/column information;
- focused automated tests.

Coordinator integration still required for canonical Engineering schema/import-export/revisions/packages and later runtime/editor wiring. The final graphical editor and concrete Python engine remain **SPECIFIED / NOT IMPLEMENTED**.

## SPECIFIED / NOT IMPLEMENTED

### Immediate coordinator-owned product hardening

- secured Engineering Apply/Delete/bulk-edit lifecycle for TAG/Data Source/Alarm editors;
- audit durability/retention/query policy and bounded buffering/outbox for temporary storage failures;
- historian retention/downsampling.

### Source/protocol milestone

After Internal Memory foundation is integrated:

- complete `builtin.memory.client` / retentive `builtin.memory.server` product integration;
- protocol-independent TAG→TAG Gateway;
- common isolated per-Data-Source driver diagnostics and Engineering diagnostics UI;
- product-owner **USER INTERFACE VALIDATION PREVIEW** before additional external protocols.

The preview must provide a practical Windows x64 test path/package, local login, demo project, visible version and startup/test instructions. It is a development preview, not a production-certified release.

### Python scripting and visual runtime

The final graphical screen/popup/Dynamo editor must not start until the scripting/property prerequisite is integrated. Locked rules include typed script-accessible object properties, Engineering base vs Runtime overrides, deterministic precedence, Client vs Server script scope separation, sandboxing, bounded execution and renderer-native animation/tween primitives rather than Python busy loops.

Full specification: `docs/PYTHON-SCRIPTING-AND-VISUAL-RUNTIME.md`.

### Later product blocks

- MQTT;
- OPC UA;
- BACnet;
- installable/versioned Driver Module framework;
- Siemens S7 ISO Connection first intended installable module target;
- later Allen-Bradley research;
- graphical screens/popups and reusable Dynamos/components after scripting/property prerequisites;
- Engineering Fragments/cross-project copy-paste;
- multi-Pen trends;
- configurable application shell;
- Engineering XLSX;
- visual asset/resource management;
- advanced reusable Equipment/Template/Dynamo libraries;
- later Server Python scripting expansion.

## PARALLEL WORK COORDINATION

Parallel development is governed by `docs/PARALLEL-WORK.md`.

Current workstreams:

1. **Coordinator/integration chat** — PR #39 is complete; next isolated coordinator slice is secured Engineering Apply/Delete/bulk mutation while also reviewing/reconciling worker PRs when their CI completes.
2. **Worker A** — PR #40 / `feature/internal-memory-foundation`.
3. **Worker B** — PR #41 / `feature/python-scripting-foundation`.

Worker chats must not merge their own PRs and must not modify coordinator-owned shared files. Do not force/reset worker branches while work is active.

## Immediate continuation

Coordinator:

1. keep PR #40 and PR #41 untouched while their current CI runs complete;
2. review both worker diffs/tests after CI and reconcile each with then-current `main` before merge;
3. in parallel, implement secured Engineering mutation workflows on a separate coordinator branch, avoiding Worker A/B domains;
4. after secured Engineering mutation: audit durability/retention, then historian retention/downsampling;
5. integrate Internal Memory before TAG Gateway;
6. continue through Gateway -> multi-driver diagnostics -> USER INTERFACE VALIDATION PREVIEW.

Worker A and Worker B continue according to their PR scope and `docs/PARALLEL-WORK.md` and never self-merge.

## Permanent continuity rule

- `PROJECT GOAL.md` = official permanent product north and locked architecture.
- `LAST CHANGE.md` = exact operational resume point with explicit MERGED / IMPLEMENTED IN PR / SPECIFIED state.
- `docs/ROADMAP.md` = ordered implementation plan/status.
- `docs/PARALLEL-WORK.md` = concurrent work ownership, conflict-avoidance and integration rules.
- Feature branches must never be the sole durable home of permanent architecture decisions.
