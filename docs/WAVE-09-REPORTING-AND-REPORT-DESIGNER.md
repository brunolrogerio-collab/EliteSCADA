# Wave 09 — Reporting and Report Designer

**Status:** LOCKED PRODUCT CONTEXT FOR WAVE 09 / SPECIFIED / NOT IMPLEMENTED  
**Context date:** 2026-08-29

This document defines the first EliteSCADA Reporting capability. It is product/architecture input for **Wave 09** and does not authorize Wave 09 before the mandatory Wave 08 follow-ups are closed.

Reporting is canonical Engineering. A report definition is not a private browser layout, generated PDF artifact, raw SQL file or renderer-owned JSON.

# 1. Product objective

Wave 09 must add a practical industrial report workflow alongside Screens, Popups, Dynamos, navigation and the Historical Data Browser:

`Engineer Report -> choose typed data source/query -> design sections/layout -> Preview -> Save/Revision -> Publish/Activate -> Runtime parameters -> Preview/Print/Export`

The product owner must be able to create common operational reports without writing SQL or application code.

Initial report use cases include:

- historian/process-value reports;
- alarm/event reports;
- production/operation summaries;
- interval summaries and grouped totals;
- current-value snapshots where explicitly supported;
- mixed text/table/chart reports built from protected typed data providers.

# 2. Canonical report entity

Introduce a first-class canonical Report definition with stable identity and normal Engineering lifecycle participation.

A report definition conceptually contains:

- stable report ID/key/name/path;
- description and optional category/folder metadata;
- page/layout configuration;
- one or more typed data/query definitions;
- report parameters/variables;
- ordered sections;
- controls inside sections;
- grouping/sort/aggregation rules;
- export/print defaults where appropriate;
- dependency/reference metadata;
- versioned migration information.

The exact DTO names are implementation details, but the public schema must be deterministic and versioned.

# 3. Section-based layout model

The initial Report Designer must support these section roles:

- **Report Header** — emitted once at the beginning;
- **Report Footer** — emitted once at the end;
- **Page Header** — repeated at the top of each page;
- **Page Footer** — repeated at the bottom of each page;
- **Detail** — repeated for each row/item from the active dataset;
- **Group Header** / **Group Footer** — emitted around grouped rows.

Requirements:

- multiple nested groups are supported;
- groups have deterministic order and may be reordered in Engineering;
- Detail remains the repeatable row body;
- sections may be hidden conditionally through the normal bounded report expression/visibility contract;
- page breaks may be inserted deliberately;
- long report headers/sections may continue across pages where the renderer can do so deterministically.

# 4. Report Designer editing experience

The designer should reuse established EliteSCADA graphical editor interaction conventions where practical instead of creating a second alien editor.

Minimum editing capabilities:

- select/multiselect;
- move/resize controls;
- copy/paste/duplicate/delete;
- z-order / bring-to-front / send-to-back where overlap is meaningful;
- align left/right/top/bottom;
- distribute horizontally/vertically;
- grid and snap;
- rulers/dimensions where useful;
- pan and zoom;
- property inspector;
- border/background/font/text alignment configuration;
- deterministic section/group ordering.

Transient selection, viewport and drag-preview state never becomes canonical Report Engineering.

# 5. Initial report controls

The first useful report-control palette should contain at least:

- static text/label;
- typed data field;
- Boolean checkbox/state field;
- image/resource;
- barcode;
- chart;
- line;
- rectangle;
- rounded rectangle;
- ellipse;
- page break.

Controls must use public typed properties and stable data references. Report image/resource references must reuse the project asset/resource authority rather than embedding arbitrary private browser URLs.

Barcode support is presentation only; it does not grant device/file/network access.

# 6. Typed report data providers and queries

Reports consume public protected data providers, not physical database tables.

Initial provider families should include:

- `historian.samples`;
- `alarm.events`;
- approved current-value snapshot/reference provider;
- future protected datasets through the same provider contract.

A report query definition must support:

- provider/dataset identity;
- selected fields;
- relative or absolute time range;
- typed filters;
- typed parameter/variable bindings;
- sort order;
- grouping inputs;
- bounded row/result limits;
- optional aggregation/downsampling where the provider declares support.

The user-facing designer may provide a graphical query builder and a readable query summary/plan. Normal Reporting must not require or expose arbitrary SQL authority.

All database execution remains server-side, validated and parameterized.

# 7. Parameters, variables and requery

Reports must support runtime parameters without modifying Engineering.

Initial parameter types should cover:

- date/time;
- duration/relative period;
- number;
- Boolean;
- string/text search;
- canonical TAG/reference selection where relevant;
- enum/list choice where a provider exposes one.

Examples:

- start date / end date;
- last N minutes/hours/days;
- area;
- alarm severity/type;
- TAG selection;
- equipment/context identifier.

Changing a runtime parameter causes a validated requery/render. Ad-hoc runtime values are session state unless deliberately saved as Engineering defaults.

# 8. Grouping, summaries and calculations

Reports must support common declarative summary behavior without requiring scripting.

Minimum aggregation functions where compatible with the source type:

- count;
- sum;
- average;
- minimum;
- maximum;
- first/last where deterministic ordering is defined.

Aggregations may operate at:

- group scope;
- page scope where meaningful;
- report/grand-total scope.

Time-bucket grouping must support common industrial summaries such as values grouped every N minutes/hours when backed by historian/query capability.

The engine must preserve source type/quality semantics. Bad/uncertain data must not silently become zero in an average or total unless an explicit aggregation policy says how it is handled.

# 9. Report expressions and formatting

Wave 09 reporting may use a bounded declarative report expression/format layer for:

- text/value formatting;
- simple calculated fields;
- conditional visibility/style;
- page/date/time labels;
- report/group/page counters;
- aggregation presentation.

It must not execute arbitrary JavaScript, Python, filesystem, network, driver or database operations.

Where practical, reuse the typed expression principles already established for visual Engineering rather than inventing an unbounded report scripting language.

Full user-authored report lifecycle scripting is **not required in the first Wave 09 slice**. If later added, it must use the normal sandboxed Script Engineering model and is sequenced with/after the Wave 10 scripting/event boundary.

# 10. Page, print and preview configuration

A report must provide practical page configuration, including:

- paper/page size;
- portrait/landscape orientation;
- margins;
- page header/footer behavior;
- page numbering including `N of M` capability;
- page breaks;
- print/preview defaults that do not override operating-system security/print policy.

Engineering and Runtime must provide a real print preview before printing/exporting.

Preview must render from the canonical report definition plus validated runtime parameters. Preview must not mutate Active Engineering.

# 11. Output and export

Initial output targets:

- print preview;
- printer through the supported host/platform print path;
- PDF;
- XLSX;
- HTML;
- RTF;
- plain text;
- CSV.

Requirements:

- output is generated from the same validated report definition/query result;
- file names/paths are handled through product/platform-safe download/save flows;
- export does not reveal database credentials or raw internal query authority;
- large exports are bounded and cancellable;
- authorization applies to the underlying report and data, regardless of output format;
- CSV/Text exports preserve deterministic encoding and column ordering;
- spreadsheet export preserves typed cells where practical instead of formatting every value as text.

# 12. Charts and graphical content

A report chart must consume the same protected report/query data path.

Initial chart capability should be sufficient for process/history summaries and grouped data. It must not create a second historian or independent query authority.

Images and charts must render consistently in preview, PDF and print within the practical limits of each export target.

# 13. Runtime integration

Runtime must provide a report surface that can:

- list/open reports permitted to the current user;
- collect runtime parameters;
- execute/requery;
- display preview;
- print;
- export;
- show loading/empty/error/unauthorized states;
- cancel abandoned long-running report generation.

Screens/Popups may later launch a report by stable report identity and optionally pass validated context/parameters. A visual button must not pass raw SQL or arbitrary server commands.

# 14. Security and resource bounds

Reporting is a potentially expensive server operation and requires explicit bounds.

Required protections:

- server-side authorization for report access and every provider query;
- parameterized database operations;
- maximum rows/data window/output size;
- timeout/cancellation;
- bounded expression complexity;
- bounded image/resource sizes;
- no arbitrary filesystem paths from report definitions;
- no direct browser-to-database connectivity;
- no credentials/secrets in exported report definitions;
- useful Audit for report generation/export/print where product policy requires it.

# 15. Engineering persistence and portability

Report definitions and their dependencies must round-trip through:

- canonical JSON Import/Export;
- Preview/Apply/CAS;
- Working state;
- immutable revisions;
- PostgreSQL project persistence;
- `.escadapkg`;
- dependency analysis;
- future Engineering Fragments/copy-paste where applicable.

Runtime-generated report outputs are artifacts, not canonical Engineering truth.

# 16. Relationship with Historical Data Browser and Trends

Reporting, Historical Data Browser and Trends must reuse compatible provider/time-range/filter semantics.

Do not create three incompatible definitions for:

- relative period;
- absolute interval;
- timezone display;
- quality filtering;
- TAG identity;
- alarm dimensions;
- server-side query bounds.

The Data Browser is interactive tabular exploration. Reporting is engineered paginated presentation/export. Trends are chart-focused time-series visualization. They share data authority but serve different product jobs.

# 17. Wave 09 acceptance gate

In addition to the Screen/Popup/Dynamo/navigation and Historical Data Browser gates, Wave 09 must prove at least:

`create Report -> choose historian/alarm typed query -> add sections/fields -> Preview -> save/reopen -> publish/activate -> Runtime preview`

`runtime date/period parameter -> requery -> report rows change -> no Engineering mutation`

`Detail + Group Header/Footer -> grouped rows -> average/sum/count -> deterministic totals`

`Page Header/Footer + page numbering -> multi-page report -> correct preview`

`report with text + field + image + chart -> PDF export`

`same report -> XLSX/CSV export with deterministic columns and typed values where supported`

`print preview -> print path invoked without bypassing authorization`

`report definition -> JSON export/import + revision/package round-trip -> same canonical layout/query`

Correctness requirements:

- invalid filters/ranges fail before database execution;
- no arbitrary SQL reaches the normal report API;
- source quality remains visible/meaningful;
- authorization survives every preview/export/print path;
- cancellation/limits protect the server from unbounded reports;
- runtime parameter changes do not dirty Engineering;
- outputs are derived artifacts, not a second source of project truth.

# 18. Explicit non-goals for the first Wave 09 slice

Do not expand the first reporting slice into:

- arbitrary SQL console;
- unrestricted generic database explorer;
- direct browser-to-database access;
- unrestricted report scripting language;
- office-suite document editor;
- full business-intelligence/data-science environment;
- email scheduler/distribution server;
- arbitrary filesystem export authority;
- safety/interlock logic.

Scheduled/distributed reports may be a later product extension after the synchronous report definition/generation path is proven.

# 19. Architecture summary

Wave 09 Reporting follows this composition:

`canonical Report Engineering -> typed protected provider/query + runtime parameters -> bounded report renderer -> preview/print/export`

This keeps data authority behind public APIs, lets report definitions survive project lifecycle/versioning and avoids turning SQL, browser state or generated PDF files into hidden project truth.
