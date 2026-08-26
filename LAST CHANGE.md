# LAST CHANGE — EliteSCADA

> Operational handoff. Read with `PROJECT GOAL.md` before every EliteSCADA task and update before every final response.

**Handoff date:** 2026-08-26
**Development state:** ACTIVE

## Repository state

- `main` HEAD last observed: `78a1656160c4317680ed54f0167537f806e104fc`.
- PR #35 `Add first-class operational command domain`: open, base `main`, head `fc15adb507db172233ed2893f65d30cdad311963`.
- PR #36 `Protect runtime read and realtime surfaces`: open, stacked on #35, head `1df64077b235321f0c3318b994f7b89632261cee`.
- PR #37 `Add Engineering UI foundation and localization`: open, Draft, base `main`, branch `feature/engineering-ui-foundation`.
- #37 head before this checkpoint commit: `ded2557fe549c08824d719825095d7e72e5da1a1`.
- Do not merge #35/#36/#37 without relevant green CI.

## GitHub Actions

- PR #35 run #133 (`32985066021`) was last verified `queued` with zero jobs allocated.
- Do not create duplicate runs while that run is waiting.
- Last useful executed run remains #129: backend built with 0 warnings/0 errors; its only test failure was the stale schema-v6 expectation, already fixed on current #35 head.

## New locked requirement: multi-driver/device communication

User clarified and this is now recorded in `PROJECT GOAL.md`:

- EliteSCADA must support multiple communication instances active simultaneously.
- Multiple Data Sources may use the same Driver type to communicate with different PLCs/devices.
- Different Driver types may run simultaneously in the same application.
- **Driver type** = protocol/runtime implementation, e.g. `modbus.tcp`, future S7/OPC UA/BACnet/module type.
- **Data Source** = one concrete configured runtime instance/connection/device context.
- **TAG** = belongs to exactly one Data Source for communication ownership in a revision and carries its protocol-specific address/binding.
- Connection, scan, timeout, reconnect and diagnostic state are independent per Data Source instance.

### Current code verification

Current Modbus architecture already supports multiple active Modbus Data Sources:

- `EngineeringDriverCompiler` iterates every enabled Data Source and creates one `ModbusTcpRuntimePlan` per valid `modbus.tcp` source.
- `EngineeringRuntimeCoordinator.BuildCandidate` creates one `ModbusTcpDriver` for each plan.
- Active runtime stores a collection of `ICommunicationDriver` instances.
- `RuntimeState.DriverByTagId` routes writes to the TAG's owning driver.
- TAGs are grouped to drivers through their Data Source `Source` reference.

Mixed real protocols are not implemented yet because the current compiler only produces executable plans for Modbus TCP.

## New locked requirement: communication diagnostics window

Created `docs/COMMUNICATION-DRIVER-DIAGNOSTICS.md` and updated `PROJECT GOAL.md`.

Communication diagnostics are first-class runtime/Engineering data, not just log text.

Required protected Engineering view per active Data Source/driver should expose, where meaningful:

- Data Source key/name and Driver type;
- non-secret endpoint/device identity;
- runtime health/state and last state change;
- last successful and failed communication timestamps;
- sanitized last error;
- cycles/requests, successes and failures;
- consecutive failures;
- timeout and reconnect/disconnect counts;
- recent failure rate;
- response/round-trip timing where meaningful;
- configured scan interval and observed data age;
- associated TAG count and TAG quality counts such as Good and BadCommunication.

Important semantics:

- TAG quality remains authoritative per point.
- Driver health is an aggregate summary and must not mark all TAGs good merely because a connection/session exists.
- Detailed diagnostics are protected; `/health` remains a minimal service probe.
- Diagnostic outputs must not expose resolved confidential values.

Current generic `DriverStatus` is insufficient: it only exposes DriverId, Name, State, Timestamp, Message and UpdatesPublished.

Recommended implementation after backend CI is reliable:

1. evolve common protocol-independent driver diagnostic contract;
2. instrument Modbus TCP counters/timestamps/failure/latency metrics;
3. expose protected diagnostics per active Data Source;
4. test independent behavior of multiple simultaneous drivers, including one failing while another stays healthy;
5. build the Engineering communication diagnostics window;
6. extend same contract to future protocols/modules.

Do not build a misleading frontend-only health dashboard using only the current minimal `DriverStatus`.

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

The latest communication requirement task added documentation/product-contract changes only, not backend driver metrics or communication UI code.

## Immediate continuation

1. Recheck #35 run #133 when Actions is usable; merge #35 only if green.
2. Retarget/validate #36 after #35 merges; merge only if green.
3. Validate #37 TypeScript/Vite + Chromium; keep Draft until green.
4. Do not add Apply to Engineering editors before validation and #36 security integration.
5. Do not build communication diagnostics UI before common backend driver diagnostic contract exists.
6. When backend CI is reliable, prioritize common diagnostics + Modbus instrumentation + multi-driver isolation tests, then the Engineering communication window.
7. After #35 reaches main, expose Commands in Engineering UI.
8. Identity/login/user lifecycle remains the next major backend security slice after #35/#36.

## Continuity rule

`PROJECT GOAL.md` is the persistent product north. `LAST CHANGE.md` is the exact resume point. Repository code determines what is actually implemented.
