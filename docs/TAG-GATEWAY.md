# EliteSCADA protocol-independent TAG Gateway

## Status

Locked product/architecture requirement recorded on 2026-08-26.

The EliteSCADA runtime must support a protocol-independent gateway capability that transfers values between server-owned runtime TAGs. This feature allows the SCADA server to act as a multiprotocol data gateway without coupling one communication driver directly to another.

The user-facing concept may be presented as **Gateway**, **TAG Bridge** or **Data Bridge**. The stable public Engineering naming should be finalized when the schema slice is implemented, but the semantic contract in this document is locked.

## Core rule

The gateway operates at the TAG layer:

`Source TAG -> Gateway route -> Destination TAG`

The gateway never implements protocol-to-protocol code such as Modbus-to-S7 or OPC-UA-to-MQTT mappings.

Instead:

1. the source TAG receives its authoritative value through its owning Data Source/source provider;
2. the gateway observes the source TAG through the normal runtime TAG/event path;
3. the gateway validates route policy, quality and conversion rules;
4. the destination TAG is written through its owning runtime Data Source/driver/source provider;
5. the destination driver handles the actual protocol/device write.

This makes the gateway protocol-independent. Any future source/provider that participates correctly in the common TAG runtime can participate without adding pairwise protocol adapters.

Examples:

- Modbus TCP PLC -> Siemens S7 PLC;
- Siemens S7 PLC -> Modbus TCP PLC;
- OPC UA TAG -> Modbus holding register;
- PLC TAG -> MQTT writable/publish TAG when that driver model exists;
- PLC TAG -> `builtin.memory.server`;
- `builtin.memory.server` -> PLC TAG;
- one source TAG -> several destination TAGs using several independent routes.

## Relationship to Data Sources

The Engineering UI may help the engineer browse/filter TAGs by Data Source and may visually show source/destination Data Sources for a route, but the authoritative route endpoints are TAG references.

A route must not merely say "copy Data Source A to Data Source B" because protocols expose different address spaces and one Data Source can own hundreds or thousands of TAGs.

The runtime resolves each TAG to its owning Data Source/provider from the Active Revision.

## Client Memory exclusion

The server gateway operates on server-owned authoritative TAG values.

`builtin.memory.client` is per Runtime Client/session and therefore does not have one global server value. It must not be accepted as a source or destination of the server TAG Gateway.

Client-local transfers belong to client-side bindings/scripts and must preserve per-client semantics.

`builtin.memory.server` is valid as either a gateway source or destination because it has one server-authoritative shared value.

## Engineering entity

Gateway configuration must become a first-class public versioned Engineering domain rather than hidden script code or browser-only configuration.

Each route requires a stable ID and should carry, at minimum:

- stable route ID;
- key/name;
- enabled state;
- source TAG stable ID and portable path reference;
- destination TAG stable ID and portable path reference;
- transfer mode;
- quality policy;
- type/conversion policy;
- optional rate/deadband controls;
- startup/initial-transfer policy;
- metadata/description where useful.

The exact DTO shape and schema version are implementation decisions. Do not hard-code a schema number before the currently pending schema-v7 command branch is integrated.

Gateway routes must participate in:

- canonical `scada.engineering` JSON;
- validation;
- preview/apply;
- revisions;
- project package backup/restore;
- migration/version compatibility;
- future XLSX/bulk Engineering when appropriate.

## Endpoint references

Stable TAG IDs are the primary runtime identity.

Portable TAG paths should also be carried where the Engineering conventions use ID+path reconciliation, allowing import/preview to detect renamed, missing or mismatched endpoints deterministically.

Activation must fail or disable the affected route with an explicit issue if either endpoint cannot be resolved consistently.

## Destination writability

The destination TAG must be writable by its source/provider.

A route targeting:

- a read-only TAG;
- a protocol area that is physically read-only;
- a missing/disabled Data Source;
- an incompatible or unavailable driver/module;
- Client Memory;

must be rejected during Engineering preview/activation rather than waiting for repetitive runtime failures.

Runtime write failures remain possible and must be diagnosed independently if the external device later becomes unavailable or rejects a write.

## Transfer modes

The initial design should support two deterministic modes.

### On change

A good source value change schedules a destination write.

Optional controls should include:

- deadband/change threshold where meaningful;
- minimum write interval;
- coalescing so rapid source changes retain the newest pending value rather than creating an unbounded write queue.

### Periodic

The latest acceptable source value is written at a configured interval.

Periodic mode is useful when a destination device expects refresh/keepalive behavior or when deterministic transfer cadence is preferable to event-driven updates.

The implementation must define safe minimum intervals and reject pathological configurations that could overwhelm the destination PLC/network.

## Startup behavior

A route should not blindly write an uninitialized value during runtime activation.

Default startup behavior:

- wait until the source TAG has an acceptable current quality/value;
- wait until the destination provider is active/writable;
- then perform an initial synchronization when the route's initial-transfer policy enables it.

The exact activation ordering must remain compatible with transactional Active Revision staging.

## Quality policy

Source TAG quality is authoritative.

The safe initial default is:

- transfer only when source quality is `Good`;
- do not write stale/previous data merely because the source becomes `BadCommunication`;
- retain destination device state when source quality is unacceptable unless a future explicit fallback policy is engineered.

A bad source quality must place the route into a visible waiting/degraded diagnostic state rather than continuously producing failed writes.

Future policies may intentionally support fallback constants or uncertain-quality handling, but they must be explicit Engineering behavior.

## Type compatibility and transformation

Simple gateway use must not require scripts.

The implementation must validate source/destination data types before activation.

Initial direction:

- identical compatible types copy directly;
- safe numeric conversions may be supported only through explicit deterministic conversion rules;
- unsafe/narrowing conversion must not happen silently;
- Boolean/string/enum-like mappings require explicit rules when types are not directly compatible;
- optional simple linear transform such as gain/offset may be supported because unit/engineering-range conversion is a common gateway need.

Complex calculations, sequencing or conditional business logic belong to the future scripting/expression subsystem rather than turning the gateway into a programming language.

## Determinism, cycles and arbitration

The initial gateway implementation is **unidirectional**.

Engineering validation must reject active route graphs that create feedback cycles such as:

`TAG_A -> TAG_B -> TAG_A`

or longer indirect cycles.

This prevents echo storms and oscillating PLC writes.

Likewise, the initial implementation should reject more than one active gateway route writing the same destination TAG. Multi-writer arbitration, priority or explicit bidirectional synchronization can be added later only with clearly defined conflict-resolution semantics.

Fan-out is valid: one source may feed several destinations through separate routes.

## Runtime architecture

The gateway is a server runtime service built above the common TAG/event/write APIs.

It must not:

- import protocol-specific transport libraries;
- call a concrete Modbus/S7/OPC/MQTT driver implementation directly;
- bypass the TAG Engine/current cache/event model;
- mutate Engineering configuration at runtime;
- use browser state as routing authority.

The destination write path resolves the active TAG and delegates to its owning runtime driver/source provider, preserving the same write semantics used elsewhere in the platform.

Gateway execution should integrate naturally with the future general source-provider abstraction introduced for internal memory sources.

## Security and audit

Gateway configuration changes are Engineering-sensitive operations and must use backend authorization/audit according to the project security model.

Continuous gateway execution is automated runtime behavior, not a human issuing thousands of interactive process writes. Therefore:

- route execution uses a trusted internal runtime/service authority rather than borrowing the identity of a browser session;
- destination writability/type/route validation is always enforced;
- configuration enable/disable/change and other sensitive administrative operations are auditable;
- every cyclic transfer should not create a full human-style audit event, because that would create unusable audit volume;
- runtime failures, state changes and counters belong in gateway diagnostics/events, with optional alarm integration later.

The internal service identity must never be caller-supplied and must not become a generic authorization bypass API.

## Diagnostics

Each active gateway route should expose a diagnostic snapshot including, where meaningful:

- route key/name;
- enabled/runtime state;
- source/destination TAG identity and Data Source context;
- last source update time;
- last successful transfer time;
- last failed transfer time;
- transfer count;
- skipped transfer count due to bad/unacceptable quality;
- coalesced/throttled update count;
- write failure count;
- consecutive failures;
- sanitized last error;
- current pending/coalesced state;
- effective transfer mode/interval.

The Engineering UI should provide an overview plus drill-down. Gateway diagnostics are distinct from driver communication diagnostics: a healthy source Modbus driver can coexist with a failed destination write route, and vice versa.

## Relationship to historian and alarms

The gateway itself should not duplicate historian storage. Source and destination TAGs can be historized independently according to their normal historian policies.

Gateway runtime failures may later produce Engineering-configurable system/communication events or alarms. Alarm flood protection and delay/debounce remain necessary.

## Relationship to scripting

The gateway is the preferred mechanism for simple deterministic TAG-to-TAG transfer.

Future scripts can still read and write TAGs for more complex logic, but engineers should not need to write a script merely to relay one PLC value to another protocol.

Scripts and gateways must use the same runtime TAG/write boundaries rather than parallel private driver APIs.

## Engineering UI

The Engineering environment should provide a Gateway/Tag Bridge tool that makes cross-protocol mapping easy.

A useful initial workflow is:

1. create a gateway route;
2. choose source Data Source optionally as a filter;
3. choose source TAG;
4. choose destination Data Source optionally as a filter;
5. choose destination TAG;
6. choose OnChange or Periodic;
7. configure quality/type/rate rules;
8. preview/validate;
9. apply through the normal Engineering lifecycle;
10. inspect runtime route status/diagnostics after activation.

The UI may visually emphasize Data Sources/protocols, but persisted semantics remain TAG-to-TAG.

## Required validation scenarios

Automated validation must eventually include at least:

1. source and destination TAGs on two different Data Sources using the same protocol;
2. source/destination using different protocol/source-provider types once a second protocol exists;
3. Modbus source -> Server Memory destination;
4. Server Memory source -> Modbus destination;
5. Client Memory rejected as a server gateway endpoint;
6. bad source quality suppresses destination writes by default;
7. destination write failure does not corrupt source TAG quality;
8. route recovers when destination communication recovers;
9. OnChange deadband/rate limit/coalescing behavior is deterministic;
10. Periodic transfer cadence is bounded and deterministic;
11. incompatible types are rejected or require explicit conversion;
12. direct and indirect cycles are rejected;
13. multiple active writers to the same destination are rejected in the initial implementation;
14. one source feeding multiple destinations works independently;
15. activation/revision switch replaces gateway routes transactionally with the active runtime;
16. import/export/project-package round trips preserve routes and references;
17. gateway diagnostics are isolated per route and do not masquerade as driver network health.

## Implementation priority

The TAG Gateway is a prerequisite before adding more external protocol families so the second external protocol immediately participates in a useful multiprotocol architecture rather than existing as an isolated driver.

Recommended sequence after the currently pending CI/security integration is stable:

1. implement internal memory source foundation from `docs/INTERNAL-MEMORY-TAGS.md`;
2. finalize public versioned Gateway/Tag Bridge Engineering contract;
3. implement protocol-independent server gateway runtime engine using TAG events and owning-provider writes;
4. implement cycle/multi-writer/type/quality/rate validation;
5. add route diagnostics and Engineering configuration/diagnostic UI;
6. validate Modbus <-> Server Memory routes as the first cross-source proof;
7. then implement additional external protocol drivers; each new writable/readable TAG provider automatically becomes eligible for gateway routes through the common runtime contract.
