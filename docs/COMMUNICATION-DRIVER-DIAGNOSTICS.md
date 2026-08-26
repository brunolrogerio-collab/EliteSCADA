# EliteSCADA communication and driver diagnostics baseline

## Status

Locked product/architecture requirement recorded on 2026-08-26.

This document defines the intended multi-driver communication topology and the minimum common diagnostics expected from communication drivers. It complements `PROJECT GOAL.md`, `docs/ROADMAP.md`, the Driver SDK contracts and protocol-specific implementations.

## Communication topology

EliteSCADA must support multiple communication instances at the same time.

A project may contain:

- several PLCs using the same protocol;
- several instruments/RTUs using the same protocol;
- multiple different protocol families in parallel;
- independent scan rates, timeouts, reconnect behavior and endpoints per communication instance.

The model intentionally distinguishes three concepts.

### Driver type

A Driver type is the protocol/runtime implementation, for example:

- `modbus.tcp`;
- future `siemens.s7`;
- future `opc.ua`;
- future `bacnet`;
- another type supplied by an installable Driver Module.

A Driver type is not a singleton connection for the whole SCADA application.

### Data Source

A Data Source is one configured instance of a Driver type.

Examples:

| Data Source | Driver type | Endpoint/device |
| --- | --- | --- |
| `PLC_EAB5` | `modbus.tcp` | `10.10.1.15:502` |
| `PLC_EAB6` | `modbus.tcp` | `10.10.1.16:502` |
| `PLC_FILTERS` | future `siemens.s7` | `10.10.2.20` |
| `ENERGY_METER_01` | `modbus.tcp` | `10.10.3.31:502` |

Two or more Data Sources may use the same Driver type. Different Driver types may also be active simultaneously.

The Data Source remains an Engineering entity and therefore participates in canonical JSON Import/Export, validation, revisions, backup/restore and activation.

### TAG communication ownership

A communication TAG belongs to exactly one Data Source in a given Engineering revision through its `source` reference.

The TAG also carries the protocol-specific address/binding required by the selected Data Source. Examples include a Modbus register/coil address, an S7 address, an OPC UA node identifier or another module-defined address form.

The runtime compiler/DriverHost is responsible for grouping TAGs by Data Source and constructing the appropriate active driver instances.

The frontend never communicates directly with a PLC/device.

## Internal source boundary

The public Data Source concept also supports non-network internal sources such as `builtin.memory.client` and `builtin.memory.server`, defined in `docs/INTERNAL-MEMORY-TAGS.md`.

Those sources are **not communication drivers** for diagnostic purposes:

- they do not have a PLC/device transport;
- they do not expose fake reconnect, timeout, request-latency or network-failure counters;
- `builtin.memory.server` may expose simple internal-provider/retention health in a broader Data Source status view;
- `builtin.memory.client` may expose client-local availability only within that client;
- neither should be marked `BadCommunication` merely because it is a memory source.

The dedicated communication-health window is primarily for external/protocol communication instances. A future broader Data Source administration view may include both communication and internal sources while preserving this distinction.

## Current implementation alignment

The existing Modbus compiler already iterates through every enabled Data Source and creates a separate `ModbusTcpRuntimePlan` for each valid `modbus.tcp` Data Source.

The runtime coordinator then creates one `ModbusTcpDriver` instance per compiled plan. The active runtime stores a collection of communication drivers and builds a TAG-to-owning-driver lookup for writes.

This means the current architecture already supports multiple active Modbus TCP Data Sources/devices. The common architecture is also suitable for multiple protocol types, but additional protocol compilers/modules are not implemented yet.

The current generic `DriverStatus` is intentionally considered only a baseline. It currently exposes driver ID/name, coarse runtime state, timestamp, message and published-update count. That is not sufficient for the required Engineering communication-diagnostics experience.

## Diagnostic principles

Communication diagnostics are runtime facts surfaced through a common public contract.

They must not be stored as browser-only state and must not depend on parsing log text.

Three different concepts must remain distinct:

1. **Driver/Data Source state**: whether the communication instance is running, degraded, reconnecting, stopped or faulted.
2. **Transport/protocol statistics**: requests, failures, timeouts, reconnects, latency and similar metrics.
3. **TAG quality**: point-level data quality such as `Good` and `BadCommunication`.

A connected TCP socket/session does not prove every TAG has good data. Likewise, one bad TAG or unsupported address must not necessarily imply every other point on the same device is invalid. Driver-level health is an aggregate summary; TAG quality remains authoritative per point.

## Common driver diagnostic contract

All communication drivers should expose a common diagnostic snapshot. Protocols may add protocol-specific detail, but the common surface should support the fields below where meaningful.

### Identity and configuration context

- Data Source stable key;
- Data Source display name;
- Driver type;
- runtime driver instance ID;
- non-secret endpoint/device identity suitable for diagnostics;
- configured scan/publish interval where applicable;
- associated TAG count.

Secrets must never appear in diagnostics.

### State and timestamps

- current state;
- last state-change timestamp;
- last successful communication timestamp;
- last failed communication timestamp;
- sanitized last error/message;
- data age or time since last successful update where applicable.

The driver-state model should be able to represent at least the operational distinction among healthy running, degraded/reconnecting and faulted conditions, even if enum evolution is staged.

### Counters

Where meaningful for the protocol:

- total communication cycles;
- total protocol requests/operations;
- successful operations;
- failed operations;
- consecutive failures;
- timeout count;
- connection/disconnection count;
- reconnect count;
- read operations;
- write operations;
- published TAG updates.

Counters should be monotonic for the lifetime of a runtime driver instance unless explicitly documented otherwise.

### Rates and latency

Where meaningful:

- recent failure rate over a defined window;
- last request/round-trip duration;
- rolling average response time;
- optional maximum/recent percentile metrics when justified;
- effective/observed scan duration versus configured scan interval.

The implementation should avoid expensive high-cardinality telemetry merely to make a dashboard look impressive. Metrics must help diagnose real communication behavior.

### TAG quality summary

Per Data Source/driver instance, expose aggregate counts of associated TAGs by current quality, including at minimum:

- Good;
- BadCommunication;
- other supported quality states as the quality model evolves;
- TAGs without a current sample if distinct from a quality state.

The diagnostic contract may also expose the worst/current aggregate health, but it must preserve the point-level qualities.

## Modbus TCP expectations

For the current Modbus TCP driver, the future diagnostic implementation should be able to expose useful details such as:

- host and port, excluding any secret material;
- scan interval;
- request timeout;
- number of poll blocks;
- configured Unit IDs represented by the active points;
- successful/failed poll blocks;
- failed poll cycles;
- consecutive failed cycles;
- timeout count;
- reconnect count;
- last successful poll time;
- last communication error;
- request/poll latency where measurable;
- TAG count and TAG-quality summary.

The existing poll-block grouping already creates a useful unit for several of these statistics.

## Engineering UI communication window

The Engineering/development environment must contain a dedicated communication/driver diagnostics view.

The initial view should present a compact table/card list of active communication Data Sources with at least:

- Data Source name/key;
- Driver type;
- endpoint/device;
- current health/state;
- last successful communication/data age;
- recent/total failures;
- reconnect/timeout indication;
- associated TAG count;
- Good/BadCommunication TAG counts.

Healthy communication should be visually quiet. Strong warning/error colors are reserved for degraded or failed communication in line with the high-performance-HMI direction.

Selecting a communication Data Source should open a drill-down panel containing:

- detailed counters and timestamps;
- configured non-secret communication parameters;
- latency/failure information;
- TAG-quality breakdown;
- current sanitized diagnostic message;
- protocol-specific diagnostic fields where useful.

The UI may refresh diagnostics periodically or use an appropriate realtime mechanism later, but the backend diagnostic snapshot remains authoritative.

## Security

Communication diagnostics can reveal plant topology, endpoints and device health and therefore are not treated as an unrestricted public health check.

Detailed driver/Data Source diagnostics should require an Engineering/system diagnostic capability through backend authorization. PR #36 already moves detailed runtime diagnostics behind the Engineering authorization boundary while keeping `/health` minimal for service probes.

Secret references may be shown only according to Engineering rules; resolved secret values must never be returned by diagnostics.

## Health check separation

`/health` is intended for basic service liveness/readiness and should remain small.

Detailed industrial communication state belongs in a protected diagnostics endpoint rather than expanding the public health payload.

## Events, alarms and retained history

Initial diagnostics may be snapshot-based.

Later hardening may add:

- retained communication diagnostic samples;
- communication state-change events;
- rolling rate windows;
- project-configurable communication alarms;
- fleet/site overview summaries.

Communication alarms should be implemented through the common alarm/event model rather than hard-coded UI warnings. Temporary network noise should not create uncontrolled alarm floods; delay/debounce/hysteresis policies may be required.

## Implementation sequence

When backend CI is reliable, the recommended implementation order is:

1. complete the internal memory-source foundation separately according to `docs/INTERNAL-MEMORY-TAGS.md`;
2. evolve the common external Driver diagnostics contract without coupling it to Modbus;
3. instrument Modbus TCP with counters/timestamps/failure/latency statistics;
4. expose protected runtime diagnostic snapshots per active communication Data Source;
5. add unit/integration tests for independent metrics across multiple simultaneous driver instances;
6. add an Engineering UI communication diagnostics view;
7. prove TAG-quality aggregation and per-TAG quality remain consistent;
8. extend the same common diagnostics contract to each future external driver/module;
9. later add retained diagnostics/events/communication alarms if operationally justified.

## Required multi-driver validation scenarios

Automated validation should eventually include at least:

- two active Modbus TCP Data Sources pointing at different endpoints/simulators;
- independent TAG sets assigned to each Data Source;
- failure of one Data Source without corrupting healthy TAG quality on another;
- recovery/reconnect of one Data Source while another remains continuously healthy;
- write routing to the correct owning driver;
- diagnostic counters isolated per driver/Data Source;
- internal memory sources excluded from network-failure metrics;
- simultaneous different Driver types once a second real protocol exists;
- activation/revision switching correctly stops old instances and starts the candidate set transactionally.
