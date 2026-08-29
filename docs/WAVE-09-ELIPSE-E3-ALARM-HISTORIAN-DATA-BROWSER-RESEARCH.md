# Wave 09 research — Elipse E3 alarms, database, E3Browser and historian

**Status:** RESEARCH COMPLETE / PRODUCT CONTEXT LOCKED FOR WAVE 09  
**Research date:** 2026-08-29  
**Purpose:** translate proven Elipse E3 alarm/historian/query workflows into EliteSCADA product requirements without copying ActiveX, raw database coupling or proprietary implementation details.

This document is architecture/product input for **Wave 09**. It does not authorize Wave 09 before Wave 08 and mandatory 08-FOLLOW work are closed.

## Research question

Research Elipse E3 documentation, manuals, knowledge base and support material for:

- alarms and alarm history;
- database-backed process/history data;
- E3Browser;
- E3Query/query configuration;
- Historian/Histórico and Storage concepts;
- date/time-range selection;
- filtering by type, area, source/name and related operational fields;
- useful presentation/sorting behavior.

The goal is to retain the useful operator/engineer workflows while implementing them with EliteSCADA's own public typed Engineering/API model.

# 1. What Elipse E3 does

## 1.1 Database as persistence behind product objects

Elipse E3 uses database objects to persist Histories, Alarms, Formulas and Storage data. Current E3 documentation advertises support for commercial databases including SQL Server, PostgreSQL, MySQL, Access and Oracle.

Important product lesson:

- operator/engineering surfaces do not need to expose physical database implementation directly;
- Alarm, History, Query and Browser are separate product concepts layered over persisted data;
- the same browser/query concept can visualize more than one persisted dataset.

EliteSCADA already intentionally standardizes v0.1 on PostgreSQL + TimescaleDB. Multi-database compatibility is therefore **not** a Wave 09 goal.

Official references:

- https://docs.elipse.com.br/documents/pt-br/e3/latest/manual/e3/manual_database.html
- https://www.elipse.com.br/produto/elipse-e3/

## 1.2 E3Browser

E3Browser is a tabular viewer for data stored in a database. Elipse documents it as usable for Histories, alarms or other tables. It uses a Query object to define what must be retrieved.

Useful observed behavior:

- configurable query;
- selected/displayed columns;
- filtering;
- sorting by clicking a column;
- configurable visual appearance, including column/row presentation;
- one table component can represent different persisted datasets.

The useful pattern is a **data-view component + query definition**, not ActiveX itself.

Official references:

- https://docs.elipse.com.br/documents/pt-br/e3/latest/manual/e3/manual_e3browser.html
- https://docs.elipse.com.br/documents/pt-br/e3/latest/manual/e3/manual_e3browser_query.html

## 1.3 E3Query

E3Query provides a graphical query builder and exposes the resulting SQL. It supports temporal filters such as:

- last N days/hours/months;
- initial date;
- final date;
- initial + final date interval.

Query filter values may be variables. At Runtime the application can change variables and request a new query. Elipse's documented `SetVariableValue` + `Requery` flow is essentially a parameterized runtime query workflow from the user's point of view.

Useful product lesson:

- query configuration can be engineered once;
- runtime users can change safe parameters without rebuilding the query;
- period selection belongs to the query model, not ad-hoc SQL assembled in each Screen.

EliteSCADA should keep the parameterized-query workflow while **not exposing arbitrary SQL as the normal v0.1 browser contract**.

Official references:

- https://docs.elipse.com.br/documents/pt-br/e3/latest/manual/e3/manual_query_e3query.html
- https://docs.elipse.com.br/documents/pt-br/e3/latest/manual/e3/manual_query_e3query_filter_script.html
- https://kb.elipse.com.br/kb29001-criando-filtros-por-valores-no-e3browser/

## 1.4 Historian / Histórico

Elipse Historian objects store process data for later analysis. A History may contain multiple Tags/expressions and can use time-based or event-based recording. The table configuration includes a record interval.

Elipse also documents compressed history/deadband behavior. Its Storage module goes further with change-oriented recording, deadband, minimum record time, maximum record time and quality-aware persistence.

Useful product lessons:

- historian recording policy and historian query period are separate concerns;
- periodic and event/change-oriented storage are both useful;
- deadband can reduce useless samples;
- a maximum recording period avoids indefinite gaps even when values remain stable;
- quality transitions are meaningful historian data and should not be flattened away.

These concepts align with EliteSCADA's existing historian Engineering fields such as strategy, deadband, period and maximum period. Wave 09 should consume/stress those existing contracts rather than inventing an E3-style parallel historian model.

Official references:

- https://docs.elipse.com.br/documents/pt-br/e3/latest/manual/e3/manual_historic.html
- https://docs.elipse.com.br/documents/pt-br/e3/latest/manual/e3/manual_historic_config_table_config.html
- https://docs.elipse.com.br/documents/pt-br/e3/latest/manual/script/e3script_ihist_deadband.htm
- https://docs.elipse.com.br/documents/pt-br/e3/latest/manual/e3/manual_storage.html
- https://docs.elipse.com.br/documents/pt-br/e3/latest/manual/e3/manual_storage_table_config.html

## 1.5 Historical chart/time navigation

E3Chart supports live TAGs, historical data and combined historical + realtime pens. Historical axes can be configured for a fixed start/end interval, while examples show querying the last hour using a time filter.

Useful product lesson:

- the same time-range model should serve tabular historical browsing and trend/chart navigation;
- historical and realtime are distinct data semantics even if displayed together.

EliteSCADA already has a basic Trend path. Wave 09 should reuse one shared typed time-range/query contract rather than letting Data Browser and Trends invent incompatible date rules.

Official references:

- https://docs.elipse.com.br/documents/pt-br/e3/latest/manual/e3/manual_e3chart.html
- https://docs.elipse.com.br/documents/pt-br/e3/latest/manual/e3/manual_e3chart_usage_example_update_historic.html
- https://docs.elipse.com.br/documents/pt-br/e3/v6.6.292/manual/e3/manual_e3chart_config_axis.html

# 2. Alarm model and filtering found in E3

## 2.1 Current alarm surface is distinct from historical alarm browsing

E3Alarm is an operational alarm surface for current alarm state and acknowledgement. Historical alarms are persisted and can be queried through E3Browser/database queries.

This separation is important for EliteSCADA:

- **Current Alarm Center** remains the operational ACK/shelving/current-state surface.
- **Historical Alarm Browser** is a read-only query over persisted alarm events.
- A historical row must never become a back door for acknowledging a current alarm.

Support material explicitly recommends E3Browser when retrieving alarms stored in a database by date range.

References:

- https://docs.elipse.com.br/documents/pt-br/e3/latest/manual/e3/manual_alarm_alarmfilter.html
- https://forum.elipse.com.br/t/como-filtrar-alarmes-pela-data/1533

## 2.2 Alarm filters

The E3 Alarm Filter / E3Alarm filter model exposes several independent filters that are combined:

- **type:** alarms only, events only, or alarms + events;
- **severity:** critical, high, medium, low;
- **area:** prefix/simple-area semantics or wildcard-capable area matching;
- **custom filter:** fields such as alarm source, area, state, acknowledgement, times, category, subcondition, message-related information, shelving and other event facts.

Documented custom-filter fields include, among others:

- `AlarmSourceName` / `FullAlarmSourceName`;
- `Area`;
- `ConditionActive`;
- `ConditionName`;
- `EventCategory`;
- `EventType`;
- `EventTime` / UTC equivalent;
- `InTime`;
- `OutTime`;
- `Acked`, `AckRequired`, `AckTime`, `ActorID`;
- `Message`;
- `Severity`;
- `CurrentValue` / formatted value;
- `Quality`;
- `Shelved` and shelving metadata;
- `SubConditionName`;
- custom user fields.

Elipse examples include filtering source names by prefix and combining this with HIHI/LOLO subconditions.

Official references:

- https://docs.elipse.com.br/documents/pt-br/e3/latest/manual/e3/manual_alarm_alarmfilter_config_filter.html
- https://docs.elipse.com.br/documents/pt-br/e3/latest/manual/script/e3script_ialarmfilter_customfilter.htm
- https://docs.elipse.com.br/documents/pt-br/e3/latest/manual/script/e3script__de3alarm_customfilter.htm
- https://docs.elipse.com.br/documents/pt-br/e3/latest/manual/script/e3script__de3alarm_areafilter.htm

## 2.3 Alarm organization and presentation

E3 treats severity as both filter and presentation information. Alarm lists can expose configurable columns and sorting; historical alarm examples use fields such as active state, entry time, exit time and acknowledgement time to distinguish lifecycle events visually.

Useful product lesson:

- filtering and visual emphasis should come from typed alarm facts;
- do not encode critical/high/ack state only as CSS or row color;
- default sorting should be deterministic, with optional secondary criteria.

References:

- https://docs.elipse.com.br/documents/pt-br/e3/latest/manual/e3/manual_e3alarm_config_column.html
- https://docs.elipse.com.br/documents/pt-br/e3/v6.7.236/tutorial/adv/tutorial_alarme_adv_exercicio_e3browser.html

# 3. Locked EliteSCADA translation for Wave 09

Wave 09 must adopt the useful behavior above using EliteSCADA-native contracts.

## 3.1 Add a web-native Historical Data Browser capability

Wave 09 scope expands from only Screens/Popups/Dynamos/navigation to also include a **Historical Data Browser** capable of consuming at least two logical datasets:

1. `historian.samples`
2. `alarm.events`

The user-facing component may be used:

- as a dedicated Runtime workspace/view;
- as an embeddable visual object on a Screen or Popup when the graphical contract is stable.

Do not create a separate database browser implementation for each dataset.

## 3.2 Typed query contract, not raw SQL

Introduce or stabilize a public typed query descriptor. Exact naming is an implementation decision, but conceptually it contains:

- dataset/provider identity;
- selected fields/columns;
- time range;
- typed filters;
- sort order;
- page/limit/cursor;
- optional presentation metadata where it belongs to an engineered view.

Rules:

- browser frontend never connects directly to PostgreSQL/TimescaleDB;
- browser frontend never submits arbitrary SQL as the normal product path;
- server converts validated query descriptors into parameterized database queries;
- dataset providers expose only supported fields/operators;
- authorization is applied before/while executing the query;
- query limits are server-enforced;
- invalid filters fail explicitly.

A future administrative/raw-query tool, if ever desired, is a separate security/product decision and is not Wave 09.

## 3.3 Shared time-range model

Wave 09 must provide one reusable time-range contract for Data Browser and compatible Trend/history consumers.

Minimum modes:

### Relative period

Examples:

- last 15 minutes;
- last 1 hour;
- last 8 hours;
- last 24 hours;
- last 7 days;
- configurable amount + unit where allowed.

### Absolute interval

- start date/time;
- end date/time;
- start must be before or equal to end according to the chosen inclusive/exclusive rule;
- invalid range is rejected before database execution.

### Timestamp semantics

- canonical storage/query timestamps remain UTC;
- locale/timezone affect display and user input interpretation, not persisted event identity;
- UI must make the effective interval visible;
- refresh/requery must preserve the active filter configuration.

## 3.4 Historical alarm filters required in Wave 09

At minimum:

- period: relative or absolute;
- alarm/event type;
- alarm category/type where available;
- area, supporting hierarchical/prefix discovery;
- source/name/path text search;
- message text search;
- severity/priority;
- active/inactive state where represented by persisted event facts;
- acknowledged/unacknowledged;
- acknowledgement required;
- shelved/not shelved where persisted;
- subcondition such as HI/HIHI/LO/LOLO/digital where applicable.

Filter UI should support combining independent criteria without requiring the operator to write an expression language.

A later advanced filter expression builder may be added, but v0.1 must not depend on users writing filter code.

## 3.5 Historian sample filters required in Wave 09

At minimum:

- one or more canonical TAG references;
- period: relative or absolute;
- optional quality filter;
- deterministic timestamp order;
- selected columns including timestamp, TAG/path/name, value, quality and engineering unit when available.

The query/result path must preserve exact typed values. `Int64`, booleans and timestamps must not be lossy-stringified merely because the table is heterogeneous.

## 3.6 Table/browser ergonomics

Minimum product behavior inspired by E3Browser but implemented natively:

- configurable visible columns;
- sortable columns;
- deterministic default sort;
- server-side filtering;
- pagination/cursor or bounded result window;
- explicit loading/empty/error/unauthorized states;
- row presentation based on typed facts, especially alarm severity/state;
- filter summary showing what is active;
- clear/reset filters;
- requery/refresh;
- responsive handling of large result sets without loading an unbounded table into browser memory.

Useful later extensions, not mandatory for first Wave 09 slice:

- saved personal Runtime views;
- CSV export;
- multi-level sort UI;
- advanced expression filter builder;
- aggregation/downsampling controls;
- reusable user-defined query templates beyond the engineered project definition.

## 3.7 Engineering persistence

If a Historical Data Browser is placed/configured in a Screen/Popup, the configuration that defines the engineered view must be canonical Engineering and participate in:

- JSON Import/Export;
- Preview/Apply/CAS;
- Working;
- immutable revisions;
- PostgreSQL project persistence;
- `.escadapkg`;
- Screen/Popup/Dynamo dependency analysis where applicable.

Runtime-selected temporary date ranges and ad-hoc filters are presentation/session state unless the user deliberately saves an engineered or permitted personal view.

A Runtime user's temporary choice of `last 8 hours`, for example, must not silently dirty the Engineering Workspace.

# 4. Database/performance requirements

Wave 09 implementation must use the existing PostgreSQL/TimescaleDB authority.

Required design principles:

- historian query predicates start from time + TAG identity where applicable;
- alarm history query predicates start from event time plus supported alarm dimensions;
- indexes/hypertable strategy must support the common time-window filters;
- server enforces maximum page size/result bounds;
- prefer stable cursor/keyset pagination for large historical lists instead of unbounded OFFSET scans;
- parameterize all user-controlled values;
- never expose database credentials/table authority to the browser;
- query cancellation/timeouts must prevent abandoned browser requests from consuming database work indefinitely;
- diagnostics should expose query failures without leaking credentials or sensitive SQL internals.

TimescaleDB-native aggregation/downsampling may be used where already available or when needed for performance, but a sophisticated aggregation designer is not required for the first Wave 09 Data Browser.

# 5. Relationship to existing EliteSCADA features

## Current Alarm Center

Keep as operational/current-state authority for:

- active alarms;
- current state;
- protected acknowledgement;
- future protected shelving operations.

Historical Alarm Browser remains query/read-only.

## Existing historian + Basic Trends

Reuse historian storage/query authority and the same time-range semantics. Do not create a second historian database path for Data Browser.

## Screens / Popups / Dynamos

Wave 09's original navigation/reuse objective remains. Historical Data Browser becomes an important practical visual/application object because real supervisory applications commonly need alarm history and process history in the same navigation model as normal process Screens.

# 6. Proposed Wave 09 product gate

Wave 09 must still prove:

`Screen containing normal objects + Dynamo -> navigation -> Popup -> Runtime lifecycle`

and now additionally prove:

`Runtime/Data Browser -> select historian dataset -> choose TAG(s) -> relative period -> query -> typed historical rows`

`Runtime/Data Browser -> absolute start/end -> query -> deterministic filtered rows`

`Historical Alarm Browser -> period + area + source/name + type/severity filter -> correct persisted alarm events`

`change filter -> requery -> no Engineering mutation`

`configured Data Browser on Screen/Popup -> save -> revision -> publish/activate -> reopen -> same engineered query/view configuration`

Required correctness checks:

- UTC interval boundaries are deterministic;
- invalid date ranges fail before DB query;
- no arbitrary SQL reaches the browser API;
- authorization is enforced server-side;
- bad/uncertain historian quality remains visible;
- exact typed historical values survive query/serialization;
- current Alarm Center ACK semantics are not duplicated by the historical browser;
- large result sets remain bounded.

# 7. Wave 09 explicit non-goals

Do not expand the first Wave 09 slice into:

- arbitrary SQL console/query editor;
- generic unrestricted database table explorer;
- direct browser-to-database access;
- support for Oracle/SQL Server/MySQL/Access merely because E3 supports them;
- report designer;
- full historian aggregation/analytics language;
- historian replacement or new parallel historian engine;
- alarm safety/interlock logic;
- changing active alarm state from a historical-record row;
- full E3 Playback clone;
- advanced data-science analytics.

# 8. Main design takeaway

The strongest E3 pattern for EliteSCADA is not any individual ActiveX control. It is this composition:

`persisted domain data -> reusable query definition -> safe runtime parameters -> browser/chart/report presentation`

For EliteSCADA v0.1 this becomes:

`PostgreSQL/Timescale domain stores -> typed protected query providers -> shared time/filter model -> Data Browser / Trend / historical alarm presentation`

This keeps database details behind public APIs, makes date/period filters reusable, and allows Screens/Popups/Dynamos to host useful historical information without making React or SQL the project authority.
