# ADR-011 — Asset & Process Intelligence Stage 3A: Read-only descriptive KPIs

- Status: implementation candidate for Main review under [issue #604](https://github.com/brunolrogerio-collab/EliteSCADA/issues/604), parent [#588](https://github.com/brunolrogerio-collab/EliteSCADA/issues/588).
- Base required by the issue: `wave15/corrections-integration@49764e5dfd23be2f57bfd8ff87081064a776a182`.
- Stage: 3A only. This is not Stage 3B, Stage 5, or Wave 15 completion.

## Decision

Provide a fixed, versioned, read-only KPI catalog (`catalogVersion = 1`, `ruleVersion = 1.0.0`) and an on-demand single-TAG query endpoint at `/api/engineering/asset-process-intelligence/analytics/query`. The catalog endpoint is `/api/engineering/asset-process-intelligence/analytics/catalog`. The route is mounted only when the existing Historical Query feature is enabled. The route surface contains no write, activation, worker, schedule, or result-store operation.

The request selects one Equipment or Location scope, one stable TAG ID, one absolute UTC window, and optionally one comparison window. A comparison is accepted only for the same TAG and equal-duration windows. It reports differences but always identifies regime equivalence as `not_assessed`.

The built-in rules report raw Historian observation count separately from derived indicators: Good finite-numeric sample count, minimum, maximum, arithmetic sample mean, first/last accepted observations, delta, and transparent time-bucket count/min/max/sample-mean trend. The sample mean is explicitly not time-weighted. First/last are chosen by ascending Historian timestamp; equal timestamps retain the provider's stable cursor order. Every rule has a stable ID and version. The response carries the exact window, active canonical TAG ID/path/type/unit, dataset and dataset-contract versions, quality/accepted/rejected counts, scope assignment provenance, and catalog/rule versions.

Only finite numeric values with `TagQuality.Good` are accepted. Other qualities, missing/unrecognized quality, missing/nonnumeric values, and non-finite numeric encodings are rejected and counted. Empty windows report a valid sample count of zero and null value metrics. Completeness remains `unknown` and expected count remains null: this product path has no canonical expected-cadence contract.

## Coherence and authorization gates

Before resolving Intelligence profiles, assignments, equipment, locations, or the source TAG, the endpoint requires a clean `EngineeringWorkspace` and exact active project/revision agreement with `ScadaRuntimeFacade`. This is checked before the workspace snapshot and again under the workspace mutation lease. A dirty workspace, absent runtime, or mismatch returns conflict and issues zero Historian queries. Reads use the shared canonical Engineering registries only after this gate; the endpoint does not query an independent persisted or mutable copy.

The requested scope must be explicitly configured active in the existing v2 profile, be licensed for Asset & Process Intelligence, and have the Stage 1 process-local permit for the exact active project/revision. HA must confirm this node can own industrial effects. The requested numeric TAG must be referenced by a v2 profile assigned to equipment in the exact Equipment scope or in the canonical Location ancestry. Location ancestry follows stable IDs; a broken/cyclic ancestry fails closed. `SignalRole` is not interpreted.

`TrendUse` is checked before resolving the source and revalidated before and after every page/period. Each referenced runtime TAG is checked with `ApiAuthorizationService.CheckRuntimeTagAsync(..., TagAccessOperation.Read)` before the first and every subsequent historical page, preserving both TAG policy and Runtime Session downscope. The canonical `IHistoricalQueryService` independently authorizes `historian.samples` (`TrendUse`) on each query. If any required authorization fails, no later query runs and the response contains no partial KPI or unauthorized TAG identity. Pause cancels the permit token linked to in-flight query cancellation. Workspace/runtime revision changes, loss of effective-active HA authority, permit replacement/cancellation, or entitlement loss discard the whole result.

## Historical query bounds

The implementation reuses only the existing `historian.samples` v1 dataset through `IHistoricalQueryService`; it adds no SQL, provider, Historian contract, schema, cursor, persistence, cache, or background worker. Each absolute window is UTC and at most 31 days. Pages use the existing cursor contract, with a request page size of 200 and a 50,000-observation per-window ceiling. If the ceiling is reached while `nextCursor` remains non-null, the request returns a bounded-window error and no partial KPI; exactly 50,000 rows is accepted only when pagination has actually ended. The bounded page count also fails closed if a provider returns an endless cursor sequence.

Trend buckets are a closed catalog of 900, 1,800, 3,600, 21,600, and 86,400 seconds. Empty buckets remain visible. The 31-day window and minimum 900-second bucket remain within 3,000 buckets. No relative-window anchoring, arbitrary filter/formula, arbitrary bucket size, or multi-TAG formula is exposed.

## Explicit exclusions and limitations

No interpretation or selection by free-text `SignalRole`; no efficiency, energy-per-production, pump-efficiency, cause/diagnosis, calibration, freshness, capture-rate, equivalent-regime, baseline/anomaly, forecasting, or causal claim. Historian observations provide timestamp/value/quality but not a canonical expected cadence or source timestamp. Those claims require a separately reviewed Stage 3B semantic/capture contract.

No UI/dashboard, process write, command, worker, scheduled query, cache, backfill, result store, generic analytics/formula language, Authority capability change, licensing-schema change, Stage 1 permit/lifecycle change, Runtime/HA authority change, or Engineering v2/schema-25 change is included. In particular, this ADR consumes the accepted Stage 1 and Stage 2 contracts without reopening them.
