# CHAT WORK ASSIGNMENTS — EliteSCADA

Date: 2026-08-29
Stage: **Wave 09-A — SHARED HISTORICAL / NAVIGATION FOUNDATION — ACTIVE**
Integration owner: **Coordinator**
Integration branch: `integration/wave-09-historical-navigation-foundation`
Product BaseSHA: `dededaca980fdb72b5d4955685ab1161aca441fd`

All workers start from the coordinator-created Wave 09 activation baseline. They must not edit `main` or the integration branch directly.

## DEV 1 — Shared Historical Query Core

Branch: `dev1/wave-09-historical-query-core`
Status: **ACTIVE**

Ownership:

- implement the public/versioned Historical Query v1 contract from `docs/WAVE-09-HISTORICAL-QUERY-CONTRACT.md`;
- shared typed dataset/query/result model;
- relative and absolute time-range resolution with one deterministic server anchor per query;
- bounded allowlisted filter/order/page semantics and opaque cursor contract;
- protected backend query surface and authorization/cancellation boundaries;
- initial provider adapters for `historian.samples` and `alarm.events`;
- parameterized PostgreSQL/TimescaleDB access where persistence is queried;
- exact Int64 transport semantics, quality and timestamp fidelity;
- focused core/API/persistence tests, including invalid/boundary queries and cancellation.

Relevant existing seams include `src/Scada.Historian/Abstractions/IHistorian.cs`, `src/Scada.Historian.TimescaleDb/TimescaleDbHistorian.cs`, existing alarm domain/persistence/API surfaces and their tests.

Must not:

- implement Browser or Report Designer UX;
- accept arbitrary SQL, arbitrary field names or unbounded result sets;
- invent dataset-specific time/filter DTOs outside the shared contract;
- weaken current Alarm Center authorization/operational semantics.

## DEV 2 — Popup / Dynamo / Navigation Engineering

Branch: `dev2/wave-09-popup-dynamo-navigation`
Status: **ACTIVE**

Ownership:

- canonical first-class Popup and Dynamo Engineering representation around the existing Screen/visual object model;
- deterministic navigation/action references between Screens/Popups where required by the Wave 09 contract;
- Dynamo reusable-instance identity and parameter/reference semantics without copying renderer-private state into Engineering;
- validation, Preview/Apply/CAS, revision, PostgreSQL and `.escadapkg` fidelity for new canonical entities;
- runtime composition using the existing visual registry/runtime/property precedence;
- compatibility/migration and focused Engineering/runtime tests.

Must not:

- create a second visual property registry/runtime;
- duplicate TAG-bit, expression or Client Memory identity rules;
- own the Historical Query contract;
- persist DOM/React/CSS/renderer handles or undocumented metadata as canonical state.

## DEV 3 — Historical Data Browser

Branch: `dev3/wave-09-historical-browser`
Status: **ACTIVE**

Ownership:

- interactive Historical Data Browser UX/runtime;
- dataset selection using canonical keys, initially `historian.samples` and `alarm.events`;
- relative/absolute time presets and typed filter builder projected from the shared Historical Query contract;
- bounded sortable/paged result table with explicit loading/empty/error/authorization states;
- exact typed value presentation, quality/state and timestamps, including Int64 without precision loss;
- read-only historical alarm event context/drill-down that remains distinct from current Alarm Center commands;
- representative browser E2E and focused frontend contract tests.

Integration rule:

- consume `docs/WAVE-09-HISTORICAL-QUERY-CONTRACT.md` and the DEV 1 public API/types once integrated;
- before that seam lands, UI scaffolding may use view-local form state, but **must not** establish an alternative persisted/public query DTO or backend endpoint.

Must not:

- implement arbitrary SQL/query text;
- duplicate provider/time/filter semantics in frontend-only persistence;
- turn historical alarm rows into acknowledge/shelve/command authority;
- take ownership of Report Designer in this first substage.

## Reporting / Report Designer sequencing

Reporting remains **IN WAVE 09**, but implementation is intentionally sequenced after DEV 1's shared Historical Query contract is accepted into integration. The next Wave 09 assignment will reuse the same dataset/query/time/filter/result semantics for canonical Report Engineering and the Report Designer.

This is not permission to create an independent reporting query model in parallel.

## Shared integration constraints

- Historical Query authority: `docs/WAVE-09-HISTORICAL-QUERY-CONTRACT.md`.
- Historical Browser/alarm context: `docs/WAVE-09-HISTORICAL-DATA-BROWSER-ALARM-HISTORIAN-CONTEXT.md`.
- Reporting direction: `docs/WAVE-09-REPORTING-AND-REPORT-DESIGNER.md`.
- Existing visual precedence remains `Animation > Script > Binding/Expression > Engineering Base > Default`.
- Engineering Import/Export, Preview/Apply/CAS, revisions and project-package fidelity remain mandatory for new canonical Engineering entities.
- Coordinate shared DTO/API edits before touching the same file from multiple worker branches.
- Keep commits narrow and attributable.
- CI policy is **NORMAL**; do not run reassurance CI after every small commit.
- Parallel Driver work remains lower priority and parked from `main`.

## Required worker handoff

Each worker handoff must report:

1. exact branch and head SHA;
2. concise delivered scope;
3. exact changed-file list;
4. tests executed and results;
5. known limitations/risks;
6. confirmation that no unassigned files were changed;
7. any shared contract decision requiring Coordinator reconciliation.