# Wave 09 — Historical Query Contract v1

Date: 2026-08-29
Status: **ACTIVE / AUTHORITATIVE FOR WAVE 09 SHARED DATA QUERY SEMANTICS**

This contract is shared by Historical Data Browser, Trends and Reporting. Consumers may project it into language-specific DTOs, but must not invent incompatible provider/time/filter/paging semantics.

## 1. Dataset identity

Initial protected dataset keys are exactly:

- `historian.samples`
- `alarm.events`

Dataset keys are stable public identifiers. A dataset exposes an allowlisted schema describing queryable/filterable/sortable fields and result columns. Clients never submit table names, SQL fragments or arbitrary column identifiers.

## 2. Query descriptor

Historical Query v1 consists conceptually of:

- `version = 1`
- `datasetKey`
- `timeRange`
- zero or more typed `filters`
- zero or more allowlisted `orderBy` terms
- bounded `page.limit`
- optional opaque `page.cursor`

Unknown versions, dataset keys, fields, operators or malformed values fail closed with explicit validation diagnostics.

## 3. Time range

One public model supports both forms.

### Absolute

- `kind = absolute`
- `fromUtc`
- `toUtc`

Both timestamps are explicit UTC instants and `fromUtc < toUtc`.

### Relative

- `kind = relative`
- positive bounded `durationSeconds`
- `anchor = now` for v1

The server resolves `now` once at query admission and derives one immutable `[fromUtc,toUtc]` window for the complete query/page operation. Individual providers or rows must not resolve separate clocks.

Server-side dataset policy defines maximum admissible range/duration.

## 4. Filters

Each filter contains:

- allowlisted field key;
- allowlisted operator compatible with that field type;
- typed value or bounded typed value list where the operator requires it.

Initial operator family may include only what providers implement safely, such as equality/inequality, ordered comparisons, bounded interval and finite membership. String search, when supported, is explicit and bounded.

No implicit string-to-number/boolean/date coercion is query authority.

## 5. Ordering and paging

Ordering uses allowlisted sortable field keys plus explicit ascending/descending direction.

Every query is bounded. `limit` has a server-owned maximum even if the client requests more.

Paging uses an opaque server-generated cursor suitable for deterministic keyset continuation. Clients may transport a cursor but must not parse, synthesize or mutate its internal state.

Offset-only unbounded scans are not the canonical Wave 09 paging contract.

## 6. Typed result model

A result returns:

- dataset identity and contract version;
- resolved absolute time window used by the server;
- public column/schema facts needed for deterministic presentation;
- ordered rows;
- optional opaque `nextCursor`;
- bounded query diagnostics/metadata that do not expose SQL or secrets.

Values preserve canonical scalar type. Supported v1 scalar families include Boolean, Int16, Int32, Int64, Float, Double, String and DateTime where the dataset declares them.

### Int64

Int64 must never lose precision through JSON/JavaScript transport. Canonical wire representation for an Int64 result value is an exact base-10 decimal string; clients may project to `bigint` or retain the string for display, but must not round through an unsafe JavaScript `number`.

## 7. Historian sample facts

`historian.samples` preserves at minimum:

- stable TAG identity;
- sample timestamp;
- typed value;
- quality/state facts available from the authoritative historian path.

TAG friendly names/paths may be included as presentation facts, but are not replacements for stable identity.

## 8. Alarm event facts

`alarm.events` is historical/read-only context. It may expose stable alarm identity, event timestamps, transition/state facts, priority/severity/message/context and other persisted authoritative facts when available.

Historical Query does **not** authorize acknowledge, shelve, unshelve, reset or other current Alarm Center commands. Operational alarm actions remain on their existing protected command surface.

## 9. Security and execution

- Query endpoints are protected by normal product authentication/authorization.
- Cancellation propagates through API, provider and database operations.
- SQL/database access is parameterized and provider-owned.
- Dataset, field and sort mappings are server allowlists.
- Result count, time range, filter count/value-list size and other resource-sensitive dimensions are bounded.
- No arbitrary SQL, JavaScript or Python expression is accepted as a Historical Query.
- Diagnostics must not disclose credentials, connection strings or raw SQL with sensitive data.

## 10. Provider composition

Providers implement the shared descriptor/result contract. Dataset-specific code translates approved logical fields/operators into the authoritative persistence/runtime model.

The existing historian abstraction may be adapted or extended, but Wave 09 should not break existing trend/runtime consumers merely to fit the new contract. Prefer an additive shared query service/provider seam when that keeps ownership clearer.

## 11. Consumer rules

Historical Data Browser, Trends and Reporting:

- share dataset keys, time-range semantics, filters, ordering, paging and typed result rules;
- may maintain transient UI form state without dirtying Engineering;
- must not persist a private competing query language;
- show unavailable/authorization/validation states explicitly rather than fabricating empty healthy data.

A canonical Screen/Popup/Report may persist a Historical Query configuration only where its own Engineering contract explicitly declares such a field and participates in normal validation/import-export/revision/package fidelity.

## 12. Change control

Any semantic change to dataset identity, time range, filter operators, paging/cursor behavior or typed wire values requires a versioned public contract change coordinated before downstream Browser/Reporting implementations diverge.