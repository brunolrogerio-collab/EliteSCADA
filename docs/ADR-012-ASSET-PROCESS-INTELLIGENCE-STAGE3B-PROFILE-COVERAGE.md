# ADR-012 — Asset & Process Intelligence Stage 3B: profile signal coverage

- Status: implementation candidate for Main review under [issue #588](https://github.com/brunolrogerio-collab/EliteSCADA/issues/588); this bounded slice does not complete Stage 3B or Wave 15.
- Implementation base: `wave15/corrections-integration@141089d999ea6ad13da34b2f0e1a8f9ed7c5ec81`.
- Related decision: [ADR-011 — Stage 3A descriptive KPIs](ADR-011-ASSET-PROCESS-INTELLIGENCE-STAGE3A.md).

## Decision

Add a read-only, on-demand profile coverage/trend bundle at `POST /api/engineering/asset-process-intelligence/analytics/profile-coverage`, with a discoverable fixed rule description at `GET /api/engineering/asset-process-intelligence/analytics/profile-coverage/catalog`.

The request selects one active Equipment/Location scope, one existing assigned v2 profile, one absolute UTC window of at most 31 days, and one of the existing fixed trend bucket sizes. For each profile signal it returns canonical TAG identity/unit, opaque `SignalRole` metadata, assignment provenance, and—when the TAG is numeric—the existing Stage 3A descriptive per-TAG quality counts and trend summary. Unsupported nonnumeric signals are identified and do not cause a Historian query.

For required numeric signals, the only new cross-signal result is the count of distinct timestamps in the intersection of finite-numeric `Good` observations whose **Historian event timestamps are exactly equal**. The result labels this basis explicitly. It does not establish source acquisition simultaneity, expected cadence, completeness, or operating-regime equivalence. Per-signal completeness remains `unknown`; source acquisition alignment remains `unverified`.

## Semantics and limits

- `SignalRole` remains free-text metadata and is not interpreted. No physical or derived process KPI is produced by this slice; the response explicitly reports that such a KPI is unsupported without a canonical semantic-role and unit-dimension contract.
- Units are carried from the canonical TAG; no unit conversion or cross-unit arithmetic occurs.
- Poor-quality, missing, unrecognized, or non-finite values cannot contribute to the exact timestamp intersection. Per-TAG rejection accounting continues to use the Stage 3A v1 KPI rules.
- The fixed profile bounds are 16 signals and 50,000 raw Historian rows total per requested window. Existing page/cursor behavior, 200-row maximum page size, and no-partial-result rule are retained. Duplicate TAG references are rejected because they would make an exact profile intersection ambiguous.
- An empty required signal set, a required signal without good numeric samples, or no exact shared timestamp is surfaced as an explicit insufficient status—not as a zero-valued process KPI.

## Reused fences and exclusions

The endpoint reuses the clean-workspace/active-revision check, active scope, exact process-local permit, effective-active HA check, current Asset & Process Intelligence entitlement, `TrendUse`, per-TAG `TagRead`, canonical `historian.samples` v1 query, cancellation, and final revalidation. All profile TAGs are preauthorized before the first Historian query. A later authorization, revision, permit, or HA failure discards the entire response. There is no SQL, provider, persistence, cache, worker, schedule, dashboard, process write/command, licensing or Authority change, schema migration, Stage 1/2/HA fence change, or Stage 4–6 work.

## Verification target

Focused coverage includes deterministic exact timestamp intersection, differing timestamps reported as insufficient alignment, poor-quality required signals, nonnumeric signal handling, licensing/TrendUse/TagRead/scope gates before Historian access, all-or-nothing cancellation, response/catalog versioning, and the absence of write routes. Local focused tests and exact-head T1/CI are recorded on the review PR; unrun checks are not claimed.
