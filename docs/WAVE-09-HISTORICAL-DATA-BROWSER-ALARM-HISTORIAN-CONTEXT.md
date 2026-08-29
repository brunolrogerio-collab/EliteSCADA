# Wave 09 — Historical Data Browser, Alarm History and Historian Context

**Status:** PRODUCT CONTEXT LOCKED FOR WAVE 09  
**Context date:** 2026-08-29

This document records the EliteSCADA product requirements for historical alarm browsing, historian queries and reusable tabular data views. It is architecture/product input for **Wave 09** and does not authorize Wave 09 before Wave 08 and the mandatory 08-FOLLOW work are closed.

Wave 09 Reporting is defined separately in `docs/WAVE-09-REPORTING-AND-REPORT-DESIGNER.md`. Historical Data Browser, Trends and Reporting must share compatible protected provider, time-range, filter, quality and TAG-identity semantics rather than grow three unrelated query systems.

# 1. Product objective

Wave 09 expands the existing Screens/Popups/Dynamos/navigation objective with a web-native **Historical Data Browser** for persisted supervisory data.

The first logical datasets are:

1. `historian.samples`
2. `alarm.events`

The same query/view architecture should be reusable for future protected datasets without exposing physical database tables directly.

The browser may appear as:

- a dedicated Runtime workspace/view;
- an embeddable visual object on a Screen or Popup when the graphical contract is stable.

# 2. Domain separation

EliteSCADA must keep current operational alarm state separate from historical alarm browsing.

## Current Alarm Center

Remains the operational authority for:

- active alarms;
- current alarm state;
- protected acknowledgement;
- future protected shelving operations.

## Historical Alarm Browser

Is a read-only query over persisted alarm/event records.

A historical row must never become a back door for acknowledging, shelving or otherwise changing current alarm state.

Historian recording policy is also distinct from historian query period. Recording configuration controls what is persisted; browser filters control what portion of persisted data is requested for presentation.

# 3. Typed query contract

Introduce or stabilize a public typed query descriptor. Exact implementation names are not locked, but the contract conceptually contains:

- dataset/provider identity;
- selected fields/columns;
- time range;
- typed filters;
- sort order;
- page/limit/cursor;
- optional presentation metadata when the view itself is engineered.

Rules:

- the browser never connects directly to PostgreSQL/TimescaleDB;
- the browser never submits arbitrary SQL as the normal product path;
- the backend translates validated descriptors into parameterized database queries;
- dataset providers expose only supported fields and operators;
- authorization is enforced server-side;
- server-side result bounds are mandatory;
- invalid filters fail explicitly;
- abandoned queries must support cancellation/timeout behavior.

A future administrative raw-query tool, if ever created, is a separate security/product decision and is not part of Wave 09.

# 4. Shared time-range model

Wave 09 must provide one reusable time-range contract for Data Browser, Reporting and compatible Trend/history consumers.

## Relative period

Required examples:

- last 15 minutes;
- last 1 hour;
- last 8 hours;
- last 24 hours;
- last 7 days;
- configurable amount + unit where permitted.

## Absolute interval

Required inputs:

- start date/time;
- end date/time.

Rules:

- invalid ranges are rejected before database execution;
- canonical storage/query timestamps remain UTC;
- locale/timezone affect user input interpretation and display, not event identity;
- the effective interval must be visible to the user;
- refresh/requery preserves the active filter configuration.

Temporary Runtime date choices are presentation/session state and must not silently dirty Engineering.

# 5. Historical alarm filters

The first Wave 09 browser must support combining independent filters without requiring filter code.

Minimum filters:

- relative or absolute period;
- alarm/event type;
- alarm category/type where available;
- subcondition such as HI, HIHI, LO, LOLO or digital where applicable;
- area, including hierarchical/prefix discovery where the project uses area hierarchy;
- source/name/path search;
- message search;
- severity/priority;
- active/inactive state where represented by persisted event facts;
- acknowledged/unacknowledged;
- acknowledgement-required state;
- shelved/not-shelved where persisted.

Useful persisted alarm facts include event/entry/exit/ack timestamps, source identity, area, category, subcondition, message, severity, current/formatted value, quality, acknowledgement actor and shelving information when available.

Filtering and visual emphasis must derive from typed alarm facts rather than being encoded only as row color or CSS state.

# 6. Historian sample filters

Minimum historian query behavior:

- select one or more canonical TAG references;
- choose relative or absolute period;
- optional quality filter;
- deterministic timestamp ordering;
- selectable columns including timestamp, TAG/path/name, value, quality and engineering unit when available.

The query/result path must preserve exact typed values. `Int64`, Boolean and timestamp values must not be lossy-converted merely because the result table is heterogeneous.

Historian recording policies remain owned by the existing historian Engineering model, including strategy, deadband, period and maximum period. Wave 09 consumes and validates those persisted samples rather than creating a second historian engine.

Quality transitions remain meaningful historical data and must not be flattened into normal values.

# 7. Table/browser ergonomics

Minimum product behavior:

- configurable visible columns;
- sortable columns;
- deterministic default sort;
- server-side filtering;
- pagination/cursor or another bounded result window;
- explicit loading, empty, error and unauthorized states;
- typed row presentation, especially for alarm severity/state and historian quality;
- visible filter summary;
- clear/reset filters;
- refresh/requery;
- responsive handling of large result sets without loading an unbounded table into browser memory.

Useful later extensions, not mandatory for the first Data Browser slice:

- saved personal Runtime views;
- direct CSV export from the browser surface;
- multi-level sort UI;
- advanced filter-expression builder;
- aggregation/downsampling controls;
- reusable user-defined query templates beyond engineered project definitions.

Wave 09 Reporting provides the engineered paginated/export path and should reuse the same protected query authority rather than duplicating it here.

# 8. Engineering persistence

If a Historical Data Browser is placed/configured in a Screen or Popup, its engineered configuration must be canonical Engineering and participate in:

- JSON Import/Export;
- Preview/Apply/CAS;
- Working;
- immutable revisions;
- PostgreSQL project persistence;
- `.escadapkg`;
- Screen/Popup/Dynamo dependency analysis where applicable.

The persisted configuration may include dataset identity, allowed/default fields, initial filter configuration, initial time-range mode, sort order and presentation settings.

Runtime-selected temporary dates and ad-hoc filters remain presentation/session state unless the user deliberately saves an engineered or explicitly supported personal view.

# 9. Database and performance requirements

Wave 09 uses the existing PostgreSQL/TimescaleDB authority.

Required design principles:

- historian queries are optimized around time + TAG identity where applicable;
- alarm history queries are optimized around event time plus supported alarm dimensions;
- indexes/hypertable strategy supports common time-window filters;
- maximum page/result size is server-enforced;
- prefer stable cursor/keyset pagination for large histories where practical;
- all user-controlled values are parameterized;
- database credentials and physical table authority are never exposed to the browser;
- query cancellation/timeouts prevent abandoned requests from consuming database work indefinitely;
- diagnostics expose useful query failures without leaking credentials or sensitive SQL internals.

TimescaleDB-native aggregation/downsampling may be used where needed for performance, but a sophisticated aggregation designer is not required for the first Data Browser slice. Report grouping/summary requirements are governed by the dedicated Reporting contract and should call through protected provider/query capabilities.

# 10. Relationship with Trends and Reporting

Historical Data Browser, Reporting and Trends should reuse the same typed time-range semantics and historian authority.

Historical and realtime values remain conceptually distinct even when a Trend or Report presents them together.

Do not create separate incompatible rules for relative periods, absolute intervals or quality handling between tabular history, paginated reports and charts.

The product roles remain distinct:

- Historical Data Browser: interactive tabular exploration;
- Reporting: engineered paginated presentation, print and export;
- Trends: chart-oriented time-series exploration/presentation.

# 11. Wave 09 product gate

Wave 09 must still prove:

`Screen containing normal objects + Dynamo -> navigation -> Popup -> Runtime lifecycle`

and additionally:

`Runtime/Data Browser -> historian.samples -> choose TAG(s) -> relative period -> typed historical rows`

`Runtime/Data Browser -> absolute start/end -> deterministic filtered rows`

`Historical Alarm Browser -> period + area + source/name + type/severity -> correct persisted alarm events`

`change Runtime filter -> requery -> no Engineering mutation`

`configured Data Browser on Screen/Popup -> save -> revision -> publish/activate -> reopen -> same engineered view configuration`

Reporting has its additional Wave 09 acceptance gate in `docs/WAVE-09-REPORTING-AND-REPORT-DESIGNER.md`.

Required correctness checks:

- UTC interval boundaries are deterministic;
- invalid ranges fail before database execution;
- no arbitrary SQL reaches the normal browser API;
- authorization is enforced server-side;
- bad/uncertain historian quality remains visible;
- exact typed values survive query/serialization;
- current Alarm Center command/ACK semantics are not duplicated by historical browsing;
- large result sets remain bounded.

# 12. Explicit non-goals

Do not expand the first Historical Data Browser slice into:

- arbitrary SQL console/query editor;
- unrestricted generic database table explorer;
- direct browser-to-database access;
- multi-database compatibility beyond the project's PostgreSQL/TimescaleDB direction;
- full analytics/data-science environment;
- replacement historian engine;
- alarm safety/interlock logic;
- changing current alarm state from a historical record;
- advanced playback/replay subsystem.

Reporting/Report Designer is deliberately part of Wave 09 but is governed by its own bounded product contract rather than being hidden inside the Data Browser implementation.

# 13. Architecture summary

Wave 09 historical browsing follows this product composition:

`PostgreSQL/Timescale domain stores -> typed protected query providers -> shared time/filter model -> Historical Data Browser / Reporting / Trend presentation`

This keeps database details behind public APIs, makes period/date/filter behavior reusable and allows Screens/Popups/Dynamos and Reports to consume useful historical information without making React or SQL the project authority.
