# Wave 15 Reporting V3 public contract

Owner: #494 / C-REPORTING-V3-01

Consumer: #496 W15-VISUAL-UTILITY-OBJECTS

This document freezes the public seam that future visual launchers may consume. A consumer must not depend on SQL, PostgreSQL, Historical Query internals, Data Query implementation details, report renderer internals, or server filesystem paths.

## Stable report identity

A report is identified by its persisted `ReportEngineeringDto.Id` when available and its stable `Key`. Runtime navigation uses the key; the server resolves the Active revision and remains authoritative.

## Open Report X

Public browser entry point:

`/runtime/reports?report=<url-encoded-report-key>`

The Runtime Report Center resolves the key against the authorized Active-revision catalog. Unknown or unauthorized reports do not reveal a report definition.

This is the preferred #496 integration seam for an `Open Report X` visual action.

## Catalog and parameter metadata

- `GET /api/runtime/reports` lists authorized reports only.
- `GET /api/runtime/reports/{key}` returns public Runtime metadata only:
  - stable id/key;
  - name/category/description;
  - runtime parameter definitions;
  - time-range policy/defaults;
  - resolution metadata;
  - display-variable metadata;
  - table layout.

The metadata endpoint intentionally does not return embedded Historical Query or saved Data Query definitions.

## Generate

`POST /api/runtime/reports/{key}/generate`

Body:

- `parameters`: runtime values for this generation only;
- `timeRange`: relative or absolute UTC query authority.

Runtime overrides do not mutate Working Engineering. If `timeRange` is omitted, the server applies the Report Engineering default (relative or absolute) when configured; existing Wave 09 reports without V3 time metadata continue using their embedded query range.

Saved Data Query references are resolved by stable ID/key from the **same Active project revision** as the Report. The resolved definition is server-only execution state and is never accepted from browser JSON. Working Data Query state cannot override Runtime generation.

Successful generation returns an `executionId`, effective generation timestamp and the bounded generated result. The server keeps a short-lived owner-bound snapshot. Subsequent viewer/export actions use that same snapshot and do not run a second query.

Generation is cancellation-aware, has a 30-second bound and a per-process concurrency gate of 2 jobs. Canonical Report/Data Query row/page limits remain in force.

## Viewer

The normal viewer is the Runtime Report Center. A generated execution can also be read through:

`GET /api/runtime/reports/executions/{executionId}`

The endpoint rechecks Runtime access, snapshot ownership and authorization against the datasets actually present in the generated execution result.

## Export and print

Derived from the same execution snapshot:

- `GET /api/runtime/reports/executions/{executionId}/export/pdf`
- `GET /api/runtime/reports/executions/{executionId}/export/xlsx`
- `GET /api/runtime/reports/executions/{executionId}/export/csv`
- `GET /api/runtime/reports/executions/{executionId}/print`

All routes reauthorize against the datasets actually executed. No route accepts an arbitrary server path.

## Failure semantics

- `401`: authentication is required.
- `403`: current principal is not authorized.
- `404 report_not_found`: report does not exist in the Active revision or is not visible to the caller.
- `404 execution_not_found`: execution snapshot is missing, expired or owned by another subject.
- `400 invalid_report / invalid_query`: configured or supplied values are invalid.
- `400 report_limit`: bounded execution/export limit exceeded.
- `408 report_timeout`: generation exceeded its execution bound.

Consumers must fail closed and must not translate an authorization failure into a direct data/export request.

## Time aggregation contract

Raw, SampledFixedStep and Aggregate delegate to the canonical Data Query/Historian authority. Reporting does not aggregate raw samples in the browser.

The current shared authority aligns duration buckets in UTC. Reporting V3 therefore stores `bucketAlignment = utcDuration`. A local calendar day/week with timezone/DST semantics is deliberately **not** emulated in Reporting. It requires a separately versioned shared Data Query contract before Reporting may expose it as calendar Day/Week.

## Units

- automatic: display the canonical engineering unit;
- hidden: omit it;
- labelOverride: replace display text only.

A label override never numerically converts a value.

## Compatibility

All V3 fields are additive. Existing Wave 09 reports with embedded `ReportQueryEngineeringDto.Query`, existing package round-trip, revision persistence and Designer layout remain valid.
