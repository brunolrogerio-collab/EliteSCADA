# COORDINATOR HANDOFF — EliteSCADA

Date: 2026-08-29
Status: **WAVE 09 ACTIVE / FOLLOW-A CLOSED / FOLLOW-B CLOSED**

## Operational truth

- Current validated product baseline on `main`: `dededaca980fdb72b5d4955685ab1161aca441fd`.
- FOLLOW-A is closed; post-merge CI #543 is green.
- FOLLOW-B is closed; exact final-head CI #657 and exact post-merge `main` CI #658 are green.
- CI policy remains **NORMAL**.
- GitHub state is the operational source of truth for branches, PRs and CI.
- Wave work has priority over parallel Driver work.
- Parallel Drivers remain isolated/parked and are not authorized for automatic merge to `main`.

## Closed gates: Wave 08 FOLLOW-A / FOLLOW-B

Do not rebuild or re-integrate their prior worker slices.

Permanent downstream contracts include:

- integer TAG-bit identity is stable `TagId + selector`;
- `.NN` remains friendly authoring/display syntax only;
- typed visual expressions are side-effect-free and do not use arbitrary JavaScript/Python evaluation;
- public visual property precedence remains `Animation > Script > Binding/Expression > Engineering Base > Default`;
- Boolean Conditions and Analog Fill persist through canonical Engineering rather than renderer-private state;
- Client Memory expression dependencies preserve the real stable Client Memory definition ID plus friendly path;
- unavailable/bad-quality values fail closed with diagnostics rather than silently becoming `false`/`0`.

Canonical documents:

- `docs/TAG-BIT-ACCESS-AND-BIT-BINDING.md`;
- `docs/VISUAL-BOOLEAN-CONDITIONS-AND-ANALOG-FILL.md`.

## Active gate: Wave 09

Wave 09 covers:

- Screens + Popups + Dynamos + navigation;
- Historical Data Browser;
- Reporting / Report Designer.

Canonical contracts:

- `docs/WAVE-09-HISTORICAL-QUERY-CONTRACT.md`;
- `docs/WAVE-09-HISTORICAL-DATA-BROWSER-ALARM-HISTORIAN-CONTEXT.md`;
- `docs/WAVE-09-REPORTING-AND-REPORT-DESIGNER.md`.

### Initial substage: 09-A shared foundation

The first integration train establishes one protected Historical Query contract before Reporting and Browser are allowed to invent provider/filter/time semantics independently.

Initial datasets:

- `historian.samples`;
- `alarm.events`.

Shared invariants:

- relative and absolute time ranges use one public/versioned model;
- server resolves relative ranges once per query;
- filters/order/paging are typed, bounded and allowlisted per dataset;
- cursors are opaque and server-owned;
- Int64 fidelity is exact end-to-end;
- quality/timestamps remain explicit facts;
- queries are authorized, cancellable and parameterized;
- no arbitrary SQL or unrestricted scripting;
- historical alarm browsing is read-only and never becomes an acknowledgement/shelving command surface;
- Browser, Trends and Reporting must consume the same query/provider/time semantics.

### Initial integration branch

`integration/wave-09-historical-navigation-foundation`

### Initial worker branches

- DEV 1: `dev1/wave-09-historical-query-core`
- DEV 2: `dev2/wave-09-popup-dynamo-navigation`
- DEV 3: `dev3/wave-09-historical-browser`

Detailed ownership is in `docs/CHAT-WORK-ASSIGNMENTS.md`.

## Execution order

1. Workers start only from the coordinator-approved Wave 09 activation baseline.
2. DEV 1 owns the shared Historical Query backend/core contract and initial dataset adapters.
3. DEV 2 owns canonical Popup/Dynamo/navigation Engineering/runtime composition and must not duplicate historical query semantics.
4. DEV 3 owns Historical Data Browser UX/runtime as a consumer of the shared query contract and must not create a frontend-only competing query DTO.
5. Coordinator integrates narrow worker heads into the Wave 09 integration train and reconciles shared files.
6. Reporting/Report Designer implementation begins after the shared Historical Query contract is accepted in integration; it reuses that contract rather than creating a reporting-specific data-query language.
7. Exact final integration CI must be green before merge to `main`.
8. Exact post-merge `main` CI must be green before the next stage transition.

## Worker handoff requirement

Every worker handoff reports exact branch/head SHA, changed files, delivered scope, tests/results, known limitations, shared decisions needing coordinator action and confirmation that no unassigned files were changed.