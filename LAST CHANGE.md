# LAST CHANGE — EliteSCADA

> Operational handoff. Read with `PROJECT GOAL.md` before every EliteSCADA task and update before every final response.

**Handoff date:** 2026-08-26
**Development state:** ACTIVE

## Repository state

- `main` HEAD verified live: `78a1656160c4317680ed54f0167537f806e104fc`.
- PR #35 `Add first-class operational command domain`: open, base `main`, head `fc15adb507db172233ed2893f65d30cdad311963`.
- PR #36 `Protect runtime read and realtime surfaces`: open, stacked on #35, head `1df64077b235321f0c3318b994f7b89632261cee`.
- PR #37 `Add Engineering UI foundation and localization`: open, Draft, base `main`, branch `feature/engineering-ui-foundation`.
- #37 head immediately before this checkpoint update: `3cde4d9302676c854bc880fe9c3190ced35c2c59`.
- This checkpoint commit creates the next #37 head; fetch live PR metadata before continuing.
- Do not merge #35/#36/#37 without relevant green CI.

## GitHub Actions

- PR #35 run #133 (`32985066021`) was last verified `queued` with zero jobs allocated.
- Do not create duplicate runs while that run is waiting.
- Last useful executed run remains #129: backend built with 0 warnings/0 errors; its only test failure was the stale schema-v6 expectation, already fixed on current #35 head.

## Locked requirement: multi-driver/device communication

Recorded in `PROJECT GOAL.md` and `docs/COMMUNICATION-DRIVER-DIAGNOSTICS.md`:

- multiple communication instances may run simultaneously;
- many Data Sources may use the same Driver type for different PLCs/devices;
- different Driver types may run in parallel;
- TAG communication ownership is through one Data Source plus its protocol-specific address/binding;
- current Modbus compiler/runtime already creates one Modbus driver instance per valid enabled Modbus Data Source;
- communication diagnostics must become a protected common per-Data-Source capability with counters, timestamps, quality aggregation and independent-failure tests.

## Locked requirement: internal memory TAG sources

Defined in `docs/INTERNAL-MEMORY-TAGS.md` and the product north. Required before additional external protocols.

### `builtin.memory.client`

- one independent value store per opened Runtime Client/session;
- non-retentive server-side initially;
- intended for popup/screen transition variables, selected context, navigation/filter/UI state, local demos and future client scripts;
- cannot be trusted backend/security/interlock/global sequencing state;
- cannot drive global server historian/alarms;
- server-side scripts cannot treat it as one authoritative scalar value.

### `builtin.memory.server`

- one server-authoritative shared value per TAG;
- visible to all authorized clients;
- retentive by design;
- suitable for simulation, internal sequences, intermediate/shared variables, parameters and future server scripts;
- participates in normal cache/event/realtime/security and optional historian/alarm semantics;
- retained values are runtime state separate from Engineering revisions and keyed primarily by stable TAG ID;
- path rename with same ID preserves value;
- incompatible retained-value/data-type changes require explicit reset/migration behavior;
- public Engineering contract needs a typed initial/default value.

Internal memory does not fabricate network timeout/reconnect/latency diagnostics.

## NEW locked requirement: protocol-independent TAG Gateway

User requires a tool that can connect information across different Data Sources/protocols so EliteSCADA can act as a multiprotocol gateway simply by reading/writing TAGs.

Created `docs/TAG-GATEWAY.md`, updated `PROJECT GOAL.md` and reordered `docs/ROADMAP.md`.

### Architectural rule

Authoritative mapping is:

`Source TAG -> Gateway route -> Destination TAG`

- Data Sources/protocols are resolved from each TAG's owning source/provider.
- Concrete drivers must never call one another directly.
- The Gateway observes the source through common TAG/event semantics and writes the destination through its owning active driver/source provider.
- This prevents N×N protocol adapters; every future compliant readable/writable TAG provider automatically becomes Gateway-compatible.

Examples:

- Modbus -> future S7;
- future OPC UA -> Modbus;
- future PLC TAG -> MQTT writable/publish TAG;
- Modbus -> Server Memory;
- Server Memory -> Modbus;
- later any supported protocol -> any other through TAGs.

### Engineering model

Gateway/Tag Bridge routes are first-class versioned Engineering entities, not hidden scripts/browser state.

Each route will have stable identity and source/destination TAG references plus transfer/quality/type/rate/startup policy. Routes participate in canonical JSON, validation, preview/apply, revisions and project package backup/restore.

Data Sources may be selected/filterable in the UI, but persisted mapping remains TAG-to-TAG.

Do not hard-code the next Engineering schema version until pending schema-v7 command work is integrated.

### Memory interaction

- `builtin.memory.server` is valid as source or destination.
- `builtin.memory.client` is rejected by the server Gateway because there is no one global Client Memory value.

### Initial deterministic/safety behavior

- destination must be active, writable and type-compatible;
- initial routing is unidirectional;
- direct/indirect route cycles are rejected;
- more than one active Gateway writer to the same destination is rejected initially unless future explicit arbitration is designed;
- one source may fan out to several destinations through independent routes;
- initial transfer modes: OnChange and Periodic;
- rate/deadband/minimum interval/coalescing prevent unbounded PLC writes;
- startup waits for acceptable source quality/value and writable destination before initial synchronization;
- safe default transfers only source quality `Good` and does not push stale values when source quality is bad;
- unsafe implicit type conversion is prohibited; simple explicit deterministic conversion/linear scaling may be supported;
- complex logic remains for scripts/expressions rather than turning Gateway into a scripting language.

### Security/diagnostics

- Gateway is a trusted internal server runtime service, not a browser-user session.
- Engineering configuration/enablement is security-sensitive and auditable.
- Cyclic sample transfers should not flood the human audit trail; use route runtime diagnostics/events.
- Route diagnostics include state, last source/transfer/failure times, transfer/failure/quality-skip/throttle/coalesce counters and sanitized last error.
- Gateway diagnostics are distinct from communication-driver health. Destination write failure must not corrupt source TAG quality.

### Implementation priority

Gateway is now a prerequisite before additional external protocol families.

Recommended order after pending CI/security integration:

1. internal memory Engineering/runtime foundation;
2. public Gateway/Tag Bridge Engineering contract;
3. protocol-independent server Gateway runtime engine;
4. cycle/multi-writer/type/quality/rate validation;
5. route diagnostics + Engineering configuration/diagnostic UI;
6. prove Modbus <-> Server Memory routing;
7. common external communication diagnostics/Modbus instrumentation;
8. then add MQTT/OPC UA/BACnet/S7 and other drivers, which automatically participate through TAGs.

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

Latest documentation/product-contract additions on #37 lock multi-driver diagnostics, internal-memory source architecture and the protocol-independent TAG Gateway. No memory/Gateway backend runtime implementation exists yet.

## Immediate continuation

1. Recheck #35 run #133 when Actions is usable; merge #35 only if green.
2. Retarget/validate #36 after #35 merges; merge only if green.
3. Validate #37 TypeScript/Vite + Chromium; keep Draft until green.
4. Do not add Apply to Engineering editors before validation and #36 security integration.
5. Before additional external protocol work, implement internal memory foundation from `docs/INTERNAL-MEMORY-TAGS.md`.
6. Then implement Gateway foundation from `docs/TAG-GATEWAY.md` and prove Modbus <-> Server Memory routes.
7. Then common external communication diagnostics + Modbus instrumentation + multi-driver isolation tests + Engineering communication window.
8. Only after those foundations proceed with MQTT/OPC UA/BACnet/S7 or another external protocol.
9. After #35 reaches main, expose Commands in Engineering UI.
10. Identity/login/user lifecycle remains the next major backend security slice after #35/#36.

## Continuity rule

`PROJECT GOAL.md` is the persistent product north. `LAST CHANGE.md` is the exact resume point. Repository code determines what is actually implemented.
