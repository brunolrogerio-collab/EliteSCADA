# EliteSCADA internal memory TAG sources

## Status

Locked product/architecture requirement recorded on 2026-08-26.

This document defines the built-in internal memory sources that must be implemented before adding more external communication protocols. It complements `PROJECT GOAL.md`, `docs/ROADMAP.md`, the public Engineering model, runtime TAG semantics and the future scripting subsystem.

## Why memory is a source, not a fake PLC

EliteSCADA needs TAGs whose value does not come from an external PLC/device. These values are useful for runtime UI state, simulations, scripts, intermediate logic and internal application state.

They must still participate in the public Engineering model, but they must not pretend to be network communication channels. In particular:

- they do not have transport reconnect/timeout semantics;
- they do not require a protocol address such as a Modbus register or OPC UA NodeId;
- they must not pollute the communication-health dashboard with invented network metrics;
- their definitions remain importable/exportable/versioned like other Engineering TAG/Data Source definitions.

The initial built-in source types are:

- `builtin.memory.client`;
- `builtin.memory.server`.

These keys are stable intended product identifiers unless a later explicit schema decision changes them with migration support.

## Client Memory

### Identity

`builtin.memory.client` represents memory owned by one opened Runtime Client instance/session.

Each Runtime Client has its own value store. Two clients looking at the same application and the same Engineering TAG definition may therefore see different values for a Client Memory TAG.

Examples of separate clients include:

- two browser tabs;
- two operator workstations;
- two logged-in runtime users;
- another future Runtime Client implementation.

The exact browser/session lifecycle mechanism is an implementation detail, but the semantic contract is that Client Memory is **not globally shared server state**.

### Intended uses

Client Memory is appropriate for presentation/session state such as:

- selected equipment or object context;
- popup-to-screen or popup-to-popup transition variables;
- return-screen/navigation state;
- temporary filters and selections;
- local demo controls;
- UI flags used by future client-side scripts;
- temporary values that intentionally differ between runtime clients.

Example conceptual TAG paths:

- `Client.Popup.SelectedEquipment`;
- `Client.Navigation.ReturnScreen`;
- `Client.Filter.Area`;
- `Client.Demo.LocalSetpoint`.

These paths are examples only; the Data Source determines the memory scope and the product should not depend on a required `Client.` prefix.

### Lifetime and retention

Client Memory is non-retentive server-side.

A new Runtime Client instance initializes its Client Memory values from the engineered initial/default value for each TAG. Closing the client/session discards that client's current values unless a future explicit client-persistence feature is engineered.

Client Memory must not silently become server-retentive or user-profile state merely because browser storage exists.

### Script semantics

Future client-side scripts may read/write Client Memory.

Server-side scripts cannot treat Client Memory as a normal scalar TAG because there is no single authoritative value. If a script must produce one value visible to all clients, it must use Server Memory or another server/runtime TAG source.

A client-side script may read ordinary shared runtime TAGs as permitted and combine them with Client Memory for UI behavior.

### Security and safety boundary

Client Memory is presentation/session state and is **not a trusted backend security or process-control authority**.

It must not be used to establish:

- authentication or authorization state;
- backend role/capability decisions;
- safety interlocks;
- authoritative command permissives;
- server-side process sequencing that assumes one global value;
- audit identity.

Changing browser/client memory cannot grant backend authority.

### Historian and alarms

Client Memory is not a globally meaningful historian/alarm source because every client may hold a different value.

The initial implementation must therefore not allow Client Memory TAGs to drive global server historian samples or server alarm definitions. Client-side presentation logic may react to them locally.

If a future feature needs per-user/session historical UI state, that is a separate product capability and must not be confused with the industrial historian.

## Server Retentive Memory

### Identity

`builtin.memory.server` represents an internal TAG value owned by the active EliteSCADA server runtime.

There is one authoritative current value per Server Memory TAG for the active application. All Runtime Clients with permission to read the TAG observe the same value.

### Intended uses

Server Memory is appropriate for shared internal runtime state such as:

- simulation variables and simulation parameters;
- a value that a future script increments/decrements over time;
- internal sequence/state variables;
- shared intermediate calculation state;
- operator-adjustable internal parameters;
- retained application state that is not stored in a PLC;
- shared flags used by server-side scripts or runtime logic.

Example conceptual TAG paths:

- `Server.Simulation.Level`;
- `Server.Simulation.Direction`;
- `Server.Sequence.State`;
- `Server.Internal.BatchCounter`.

Again, prefixes are examples rather than mandatory semantics.

### Shared runtime behavior

Server Memory participates in normal server TAG runtime behavior:

- Current TAG Cache;
- Event Bus / TAG change events;
- realtime/WebSocket distribution;
- normal TAG read/write authorization;
- future server-side scripting;
- historian when engineered;
- alarm evaluation when engineered;
- expressions/bindings that operate on ordinary runtime TAGs.

External client writes to Server Memory must pass the same backend authorization boundary as other writable server TAGs. Sensitive operational writes remain subject to the project's security/audit policy.

### Retention

Server Memory is retentive by design in the initial product requirement.

The retained **value** is runtime state and must be persisted separately from the immutable Engineering definition/revision. Engineering packages describe what the TAG is; they do not become a continually changing storage location for its current value.

Retention must use the stable TAG ID as the primary identity, not the human-readable path. Therefore:

- renaming a TAG path while preserving its ID must preserve the retained value;
- server restart must restore the last retained compatible value;
- runtime reactivation/revision changes should preserve the retained value when the same stable TAG ID remains valid;
- first initialization with no retained value uses the engineered initial value or a deterministic type default;
- incompatible data-type changes must never silently coerce the old retained value. Preview/activation must surface the incompatibility and use an explicit reset/migration policy;
- deleting a TAG removes it from active runtime. Cleanup policy for orphaned retained records may be deferred, but stale retained data must never resurrect a deleted TAG on its own.

Durability should avoid turning every high-frequency memory update into an unsafe blocking database transaction. The implementation may use batching/coalescing/write-behind as long as the retention contract and crash behavior are explicitly tested and documented.

### Initial value

Memory TAG Engineering needs a typed initial/default value concept.

The exact DTO/schema shape is an implementation decision, but it must be part of the public versioned Engineering contract rather than hidden in frontend-only state. Validation must ensure the initial value is compatible with the TAG data type.

For Server Memory, the initial value is used when no compatible retained value exists. Once a retained value exists, retention takes precedence until an explicit reset/migration occurs.

For Client Memory, each new client/session starts from the engineered initial/default value.

### Quality

Server Memory has no external communication transport. A healthy in-process memory value should normally carry `Good` quality.

Quality may become bad/uncertain only for a real internal reason such as invalid value conversion, failed restoration, script/runtime evaluation failure or another explicitly modeled condition. It must not report fake `BadCommunication` merely because it uses the TAG quality model.

## Engineering model

Both memory source types must participate in the canonical Engineering model.

A project can contain one or more Data Sources of each built-in memory type. TAGs reference the appropriate source through the normal `source` field.

Initial implementation direction:

| Source type | Runtime owner | Shared between clients | Retentive | Network address required |
| --- | --- | --- | --- | --- |
| `builtin.memory.client` | Runtime Client instance | No | No server retention | No |
| `builtin.memory.server` | EliteSCADA server runtime | Yes | Yes | No |

Memory TAGs continue to use the ordinary TAG data types, stable IDs, paths, descriptions, permissions and metadata where meaningful.

The implementation must not require a fake protocol address. Existing generic TAG `address` may remain empty for memory sources.

## Relationship to Data Source and Driver architecture

The public Engineering `Data Source` abstraction is broader than a physical PLC connection. It identifies the provider/owner of TAG values.

External protocol Data Sources compile to communication-driver instances. Internal memory Data Sources compile to the appropriate internal runtime provider:

- `builtin.memory.server` is a server-owned internal source/provider and may use the common runtime driver/provider lifecycle where useful, but it has no network transport;
- `builtin.memory.client` is a client-owned source definition and must not create one global server `ICommunicationDriver` value store, because that would destroy its per-client semantics.

The runtime/compiler architecture may introduce a clearer common source-provider abstraction if needed instead of forcing Client Memory through an inappropriate server communication-driver interface.

## Relationship to communication diagnostics

Internal memory sources are not PLC/device communications.

The communication diagnostics window should therefore focus on external communication Data Sources. If memory sources are shown in a broader Data Source status view:

- Server Memory may show a simple internal/local health state and retention status;
- Client Memory may show only client-local availability/state;
- neither type should display invented latency, reconnect, timeout or network failure counters.

## Relationship to simulation

The existing simulation driver remains useful for generated process signals.

Server Memory complements it by providing shared writable simulation state and parameters that scripts/runtime logic can modify. For example, a future server script may increment `Server.Simulation.Level`, reverse direction at limits and publish the resulting value to every client.

Client Memory can be used when each opened client should have an independent demonstration/control state.

## Future scripting contract

When scripting is implemented, the script APIs must make memory scope explicit.

Expected rule:

- client-side script context: may access Client Memory for its own client plus permitted shared server/runtime TAGs;
- server-side script context: may access Server Memory and permitted shared server/runtime TAGs, but not a nonexistent single global Client Memory value.

Scripts must use stable TAG identity/path lookup through public runtime APIs and must not bypass TAG security, quality, event, historian or alarm semantics.

## Required validation scenarios

Automated validation must eventually cover at least:

1. two Runtime Clients can hold different values for the same Client Memory TAG without leaking between clients;
2. navigation/popup changes within one client preserve that client's Client Memory for the client lifetime;
3. a new client initializes Client Memory from the engineered initial/default value;
4. Server Memory writes are visible to all connected authorized clients;
5. Server Memory survives server restart according to the retention contract;
6. Server Memory survives a TAG path rename when the stable TAG ID is preserved;
7. an incompatible retained value/data-type migration is reported and never silently coerced;
8. Server Memory can participate in realtime updates, historian and alarms where configured;
9. Client Memory is rejected as a global historian/alarm source;
10. server-side scripts cannot accidentally resolve one client's Client Memory as authoritative global state;
11. memory sources do not appear as failed network communications in driver diagnostics;
12. Engineering export/import/backup/revision round trips preserve memory Data Source/TAG definitions and initial values without embedding mutable retained runtime values.

## Implementation priority

These internal memory sources are a prerequisite before implementing additional external protocol drivers such as MQTT, OPC UA, BACnet or Siemens S7.

Recommended implementation order after the currently pending security/CI integration is stable:

1. finalize versioned Engineering representation for memory source types and typed initial values;
2. implement `builtin.memory.server` shared runtime behavior and durable retention;
3. implement `builtin.memory.client` per-client runtime store and binding API;
4. integrate authorization, realtime, historian/alarm restrictions and reset/migration behavior;
5. add Engineering UI configuration for both source types;
6. expose future script APIs against the correct client/server scope;
7. validate the required multi-client and restart scenarios;
8. only then continue with additional external protocol drivers.
