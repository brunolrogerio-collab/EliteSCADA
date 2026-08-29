# Wave 09 — Historical Data Browser, Alarm History and Historian Context

**Status:** PRODUCT CONTEXT LOCKED FOR WAVE 09  
**Context date:** 2026-08-29

This document records the EliteSCADA product requirements for historical alarm browsing, historian queries and reusable tabular data views. It is architecture/product input for **Wave 09** and does not authorize Wave 09 before Wave 08 and the mandatory 08-FOLLOW work are closed.

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

A historical row never becomes a back door for changing current alarm state, acknowledgement or shelving.

# 3. Typed query model

Historical browsing must use a public typed query descriptor rather than arbitrary SQL from the browser.

Conceptually the query descriptor contains:

- dataset/provider identity;
- selected fields/columns;
- time range;
- typed filters;
- sort order;
- page/limit/cursor;
- optional presentation metadata when the view itself is canonical Engineering.

Rules:

- browser never connects directly to PostgreSQL/TimescaleDB;
- browser never receives database credentials;
- normal product API never accepts arbitrary SQL from the browser;
- server validates dataset, field and operator combinations;
- server converts validated descriptors into parameterized queries;
- authorization is enforced server-side;
- query limits are server-enforced;
- invalid filters fail explicitly;
- cancellation/timeouts prevent abandoned queries from consuming resources indefinitely.

# 4. Shared time-range model

Historical Data Browser and compatible Trend/history consumers should share one time-range contract.

## Relative period

At minimum support useful presets and configurable relative periods such as:

- last 15 minutes;
- last 1 hour;
- last 8 hours;
- last 24 hours;
- last 7 days;
- configurable amount + supported unit where appropriate.

## Absolute interval

Support:

- start date/time;
- end date/time;
- explicit boundary validation before database execution.

## Timestamp semantics

- persisted/query timestamp identity remains canonical UTC;
- locale/timezone affect display and interpretation of user-entered local times, not stored event identity;
- UI makes the effective interval visible;
- refresh/requery preserves the active filter configuration.

# 5. Historical alarm filters

Wave 09 must support combining at least:

- relative or absolute period;
- alarm/event type;
- alarm category/type where available;
- subcondition such as HI/HIHI/LO/LOLO/digital where applicable;
- area with hierarchical/prefix discovery;
- source/name/path search;
- message search;
- severity/priority;
- active/inactive state when represented by persisted facts;
- acknowledged/unacknowledged;
- acknowledgement-required state;
- shelved/not-shelved state when persisted.

The first UI must provide normal controls for these filters. v0.1 must not require an operator to write a filter expression language.

# 6. Historian sample filters

At minimum:

- one or more canonical TAG references;
- relative or absolute period;
- optional quality filter;
- deterministic timestamp order;
- selected columns including timestamp, TAG/path/name, value, quality and engineering unit when available.

The result path preserves exact typed values. Int64, Boolean and timestamps are not lossily converted merely because the result table is heterogeneous.

# 7. Table/browser ergonomics

Minimum behavior:

- configurable visible columns;
- sortable columns;
- deterministic default sort;
- server-side filtering;
- bounded result window with pagination/cursor as appropriate;
- explicit loading, empty, error and unauthorized states;
- row presentation based on typed alarm severity/state facts;
- visible active-filter summary;
- clear/reset filters;
- refresh/requery;
- large result sets must not be loaded unbounded into browser memory.

Useful later extensions, not mandatory for the first Wave 09 slice:

- saved personal Runtime views;
- CSV export;
- multi-level sort UI;
- advanced filter builder;
- aggregation/downsampling controls;
- reusable user query templates.

# 8. Historian recording and query relationship

Historian recording policy and historian query period are separate concepts.

EliteSCADA already carries historian Engineering settings such as strategy, deadband, period and maximum period. Wave 09 consumes and validates the existing historian authority rather than creating another storage model.

Relevant product requirements:

- periodic and change/event-oriented recording policies remain explicit;
- deadband may reduce unnecessary samples;
- maximum recording interval avoids indefinite gaps when configured;
- quality transitions remain meaningful data;
- query time range does not mutate historian recording policy.

# 9. Trend relationship

Historical table browsing and Trends should use compatible time-range semantics.

Historical and realtime data remain distinct source semantics even when presented together in a future Trend/view.

Do not create separate, incompatible date rules for each component.

# 10. Engineering persistence

If a Historical Data Browser is configured on a Screen/Popup, its engineered configuration participates in:

- canonical JSON Import/Export;
- Preview/Apply/CAS;
- Working;
- immutable revisions;
- PostgreSQL project persistence;
- `.escadapkg`;
- Screen/Popup/Dynamo dependency analysis where applicable.

Runtime-selected temporary dates and ad-hoc filters are session/presentation state unless a deliberate save action exists.

Selecting `last 8 hours` at Runtime must not silently dirty Engineering.

# 11. Database/performance requirements

Use existing PostgreSQL/TimescaleDB authority.

Required principles:

- historian predicates are optimized around time + TAG identity where applicable;
- alarm-history predicates are optimized around event time and supported alarm dimensions;
- indexes/hypertable strategy supports common time windows;
- maximum page/result sizes are server-enforced;
- stable cursor/keyset pagination is preferred for large historical lists where appropriate;
- all user-controlled query values are parameterized;
- query diagnostics do not expose credentials or sensitive database internals;
- TimescaleDB aggregation/downsampling may be used when useful, but a sophisticated aggregation designer is not required for the first Wave 09 slice.

# 12. Wave 09 product gate addition

Wave 09 keeps its original visual-navigation gate and additionally proves:

`Runtime/Data Browser -> historian dataset -> TAG(s) -> relative period -> typed historical rows`

`Runtime/Data Browser -> absolute start/end -> deterministic historical rows`

`Historical Alarm Browser -> period + area + source/name + type/severity -> correct persisted alarm events`

`change filter -> requery -> no Engineering mutation`

`configured Data Browser on Screen/Popup -> save -> revision -> publish/activate -> reopen -> same engineered query/view configuration`

Correctness checks include:

- deterministic UTC interval boundaries;
- invalid ranges rejected before database execution;
- server-side authorization;
- explicit bad/uncertain historical quality;
- exact typed serialization;
- historical view cannot perform current Alarm Center operations;
- bounded large result sets.

# 13. Explicit non-goals

Do not expand the first Wave 09 slice into:

- arbitrary SQL console;
- unrestricted database table explorer;
- direct browser-to-database access;
- multi-database compatibility beyond the locked v0.1 PostgreSQL/TimescaleDB platform;
- report designer;
- full historian analytics language;
- a second historian engine;
- alarm safety/interlock logic;
- changing current alarm state from historical rows;
- playback subsystem;
- advanced data-science analytics.

# 14. Product architecture direction

The locked composition is:

`PostgreSQL/Timescale domain stores -> typed protected query providers -> shared time/filter model -> Historical Data Browser / Trend / alarm-history presentation`

Database details remain behind public APIs, filters remain reusable, and Screens/Popups/Dynamos can host historical information without making React or SQL the project authority.
