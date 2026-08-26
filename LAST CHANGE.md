# LAST CHANGE — EliteSCADA

> Operational handoff. Read with `PROJECT GOAL.md` before every EliteSCADA task and update before every final response.

**Handoff date:** 2026-08-26
**Development state:** ACTIVE

## Repository state

- `main` HEAD verified live: `78a1656160c4317680ed54f0167537f806e104fc`.
- PR #35 `Add first-class operational command domain`: open, base `main`, head `fc15adb507db172233ed2893f65d30cdad311963`.
- PR #36 `Protect runtime read and realtime surfaces`: open, stacked on #35, head `1df64077b235321f0c3318b994f7b89632261cee`.
- PR #37 `Add Engineering UI foundation and localization`: open, Draft, base `main`, branch `feature/engineering-ui-foundation`.
- #37 head immediately before this checkpoint commit: `d8bd99d97b8de0f7728ac424f74258c3528e8e22`.
- Do not merge #35/#36/#37 without relevant green CI.

## GitHub Actions

- PR #35 run #133 (`32985066021`) was last verified `queued` with zero jobs allocated.
- Do not create duplicate runs while that run is waiting.
- Last useful executed run remains #129: backend built with 0 warnings/0 errors; its only test failure was the stale schema-v6 expectation, already fixed on current #35 head.

## Locked requirement: multi-driver/device communication

Already recorded in `PROJECT GOAL.md` and `docs/COMMUNICATION-DRIVER-DIAGNOSTICS.md`:

- multiple communication instances may run simultaneously;
- many Data Sources may use the same Driver type for different PLCs/devices;
- different Driver types may run in parallel;
- TAG communication ownership is through one Data Source plus its protocol-specific address/binding;
- current Modbus compiler/runtime already creates one Modbus driver instance per valid enabled Modbus Data Source;
- communication diagnostics must become a protected common per-Data-Source capability with counters, timestamps, quality aggregation and independent-failure tests.

Current generic `DriverStatus` is insufficient for the future diagnostics window.

## NEW locked requirement: internal memory TAG sources

User clarified that internal memory sources are required **before additional external communication drivers/protocols**.

Created `docs/INTERNAL-MEMORY-TAGS.md`, updated `PROJECT GOAL.md`, `docs/ROADMAP.md` and communication-diagnostics semantics.

### Built-in source identities

- `builtin.memory.client` = Client Memory scoped to one opened Runtime Client instance/session.
- `builtin.memory.server` = shared Server Memory owned by the EliteSCADA server runtime and retentive by design.

These are internal TAG sources, not fake PLC/network drivers.

### Client Memory semantics

- Different Runtime Clients may have different values for the same engineered Client Memory TAG.
- Intended for popup/screen transition variables, selected equipment/context, navigation state, temporary filters, UI flags, local demo controls and future client-side scripts.
- New client/session starts from an engineered typed initial/default value.
- No server retention in the initial implementation.
- Client-side scripts may access their own Client Memory.
- Server-side scripts cannot treat Client Memory as one global authoritative scalar value.
- Client Memory must never be authentication/authorization state, safety/interlock state, authoritative command permissive, server sequencing truth or audit identity.
- Client Memory must not drive the global server historian/alarm engine in the initial implementation because there is no single global value.

### Server Retentive Memory semantics

- One authoritative shared value per TAG for the active server runtime.
- All authorized Runtime Clients observe the same value.
- Intended for simulation variables/parameters, internal sequence state, intermediate values, retained parameters and future server-side scripts.
- Participates in normal server TAG cache/event/realtime/security semantics and may participate in historian/alarms when engineered.
- External writes use normal TAG/process-write authorization.
- Normally has `Good` quality because there is no external transport; do not invent `BadCommunication` for an internal source.
- Retained mutable values are stored separately from immutable Engineering revisions/packages.
- Retention identity is the stable TAG ID, not path; path rename with same ID preserves value.
- Restart and compatible revision reactivation restore the retained value.
- If no retained value exists, use engineered typed initial/default value or deterministic type default.
- Incompatible retained-value/data-type changes must not be silently coerced; explicit validation/reset/migration behavior is required.
- Deleted TAGs must not be resurrected by stale retained state.

### Engineering/schema consequence

- The public versioned TAG Engineering contract needs a typed initial/default value for memory TAGs.
- Memory TAGs do not require protocol addresses.
- Mutable retained Server Memory values themselves must NOT be serialized into ordinary Engineering exports/revisions.
- The implementation may require a new Engineering schema version after the current pending command schema-v7 work; do not hard-code the next version until branch integration is stable.

### Architecture consequence

The Data Source abstraction is broader than a physical communication channel: it identifies the provider/owner of TAG values.

- external protocol Data Sources compile into communication drivers;
- `builtin.memory.server` compiles into an internal server source/provider;
- `builtin.memory.client` remains client-owned and must not be forced into one server-global `ICommunicationDriver`, otherwise per-client semantics would be destroyed;
- a clearer source-provider abstraction may be introduced when implementing memory rather than pretending every source is a network driver.

### Diagnostics consequence

Internal memory sources are excluded from network communication metrics.

- no fake timeout/reconnect/request-latency statistics;
- Server Memory may later expose internal/retention health in a broader Data Source status view;
- Client Memory may expose only client-local availability/state;
- dedicated communication-health UI remains focused on external protocol Data Sources.

### Required validation later

Must test at minimum:

1. two clients hold independent Client Memory values;
2. new client initializes from engineered default;
3. Server Memory write is visible to all clients;
4. Server Memory survives restart;
5. stable-ID path rename preserves retained value;
6. incompatible data-type migration is surfaced, never silently coerced;
7. Server Memory participates in realtime and optionally historian/alarms;
8. Client Memory is rejected as global historian/alarm source;
9. memory source definitions round-trip through Engineering import/export/backup without embedding mutable retained values.

## PR #35

Command domain implemented: schema v7 commands, registries/import-export, runtime compilation/execution through owning driver, scoped `CommandExecute`, audit, demo commands and tests. Await CI.

## PR #36

Read/realtime security implemented: TagRead filtering, protected history/alarm reads, authenticated WebSocket with per-event checks and expiry, protected Engineering/diagnostic/package reads, minimal public health and expanded E2E. Await #35 + CI.

## PR #37

Engineering UI currently includes:

- `/engineering` workspace and Runtime ↔ Engineering navigation;
- `pt-BR` / `en` / `es` localization;
- read views for project domains;
- preview-only editors for TAGs, Data Sources and Alarms;
- existing/new transient drafts;
- backend canonical package preview only, no Apply;
- stale-preview invalidation and unsaved-draft guards;
- Chromium tests for locale, previews, invalid references and proof previews do not mutate Workspace/export.

Latest Alarm editor supports TAG binding, type, priority, setpoint/digital value, class, area, message, delay, enabled, ACK and shelving. Changing alarm TAG path clears old `tagId` in the draft so backend validates the new path.

Latest documentation additions on #37 now also lock multi-driver diagnostics and internal-memory source architecture. No memory runtime implementation exists yet.

## Immediate continuation

1. Recheck #35 run #133 when Actions is usable; merge #35 only if green.
2. Retarget/validate #36 after #35 merges; merge only if green.
3. Validate #37 TypeScript/Vite + Chromium; keep Draft until green.
4. Do not add Apply to Engineering editors before validation and #36 security integration.
5. Before MQTT/OPC UA/BACnet/S7 or another external protocol, implement internal memory foundation from `docs/INTERNAL-MEMORY-TAGS.md`.
6. Memory implementation order: versioned typed initial-value Engineering contract -> retentive Server Memory -> per-client Client Memory -> security/realtime/historian-alarm restrictions -> UI -> scripting APIs -> restart/multi-client tests.
7. Then implement common external communication diagnostics + Modbus instrumentation + multi-driver isolation tests + Engineering communication window.
8. After #35 reaches main, expose Commands in Engineering UI.
9. Identity/login/user lifecycle remains the next major backend security slice after #35/#36.

## Continuity rule

`PROJECT GOAL.md` is the persistent product north. `LAST CHANGE.md` is the exact resume point. Repository code determines what is actually implemented.
