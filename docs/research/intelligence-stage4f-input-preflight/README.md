# Stage 4F local input preflight

This is a bounded, offline structural check for one explicitly supplied copy of the prepared Stage 4F `historian-observations.csv`. It is a standard-library-only Python tool; it has no product/API/database integration and makes no network calls. It is not an import path, analytic evaluator, eligibility decision, or readiness gate.

From the repository root, run it with Python 3.10 or later against an explicitly prepared local copy:

```powershell
python docs/research/intelligence-stage4f-input-preflight/preflight.py "./private-copy/historian-observations.csv"
```

The default report is JSON on stdout. An optional `--report-out <new-path>` writes a report only to an explicitly chosen, new path outside any Git repository; existing paths, the input itself, and Git repositories are refused. Nothing writes a report into the checkout by default. If the input or report destination cannot be safely resolved, the sanitized error report goes to stdout instead; it is never created beside an unresolved input. Exit codes are `0` for structurally parseable content with no row diagnostics (including header-only), `2` for encoding/header/CSV-shape errors, `3` for row-level contract errors, `4` for a configured resource bound, and `5` for local path/argument/output errors. All error text is sanitized.

## Existing product source and 4D/4E evidence (documentation only)

For an authorized collection, the existing source is the paginated [`POST /api/historical/query`](../../../src/Scada.Api/Historian/HistoricalQueryApi.cs) endpoint using `datasetKey: "historian.samples.v2"` and query `version: 2` (as enforced by [`HistoricalQueryValidator`](../../../src/Scada.Core/HistoricalQueries/HistoricalQueryValidator.cs)). Set `page.limit` within the product's 1–200 bound; when the response supplies `nextCursor`, continue the same query with that cursor until it is null. Keep the time range, filters and ordering fixed while paging. The authenticated endpoint applies its existing `TrendUse` authorization. The CSV's `export.id`, `cohort.id` and `value.kind` remain collection-side annotations/flattening, not new product query fields.

This preflight does **not** call that API, create an export endpoint/client, or repeat the product's eligibility checks. Existing aggregate evidence is documented in [Stage 4D reference evidence and data sufficiency](../../WAVE15-INTELLIGENCE-STAGE4D-REFERENCE-EVIDENCE.md) ([issue #649](https://github.com/brunolrogerio-collab/EliteSCADA/issues/649)) and [Stage 4E operating contexts and comparison eligibility](../../WAVE15-INTELLIGENCE-STAGE4E-OPERATING-CONTEXT.md) ([issue #652](https://github.com/brunolrogerio-collab/EliteSCADA/issues/652)). 4D reports bounded Historian sufficiency/provenance counts; 4E reports typed, exact-time context eligibility. Neither upgrades the evidence assessed here or changes its `NOT_ASSESSED` analytic/readiness results.

## Checks and limits

The accepted header fields and meanings are defined by the [Stage 4F data-request README and workbook/CSV model](../../examples/intelligence-stage4f-data-request/README.md), `HistoricalQueryContracts.cs`, `TagValue.cs`, `TagQuality.cs`, `HistorianCapturePolicy.cs`, and the Timescale Historian v2 projection. The verifier requires exactly the expected field-name set (order may vary), strict UTF-8, valid CSV quoting and exact row width. It handles an initial UTF-8 BOM and quoted CSV fields.

The explicit bounds are 256 MiB per input, 1,000,000 data rows, 65,536 characters per CSV field, 2,048 characters per TAG path, 256 characters per collection identifier, 128 characters for typed numeric text, 100 emitted diagnostic items, and a 40-character timestamp text. These are resource and format bounds for this local preflight only. The 1,000,000-row bound is not the Stage 4D/4E product query cap and is not a statistical sufficiency target.

The checks include:

- declared export/cohort IDs; one export annotation per supplied file; canonical GUID TAG IDs and stable ID/path mapping;
- the Historian quality and `value.kind` vocabulary, typed null/empty/zero distinctions, integer ranges, finite numeric values, booleans and GUID/date-time values. Typed numbers and ISO timestamp digits use ASCII invariant syntax, matching the .NET wire/parse contract; Unicode remains supported in ordinary string values and collection/TAG text identifiers;
- UTC observation, source and server timestamps. Up to all seven .NET fractional tick digits are retained for comparisons; timestamp key comparisons do not pass through microsecond-resolution `datetime` values;
- source/server timestamp evidence consistency with the additive `TagValue` contract. Legacy `NotRecorded` is not promoted to `NotProvided`, `PresentUnverified`, or synchronized evidence; server timestamps cannot claim source-clock synchronization;
- capture-policy state/profile/fingerprint consistency and duplicate source-time keys scoped by export, cohort and TAG.

Repeated source-time keys are counted as aggregate groups and rows. Rows with absent source time are counted separately; missing or weak evidence is not silently removed or upgraded. Any syntactically valid, present source timestamp participates in duplicate detection (including `NotRecorded` or an inconsistent evidence row), while its evidence state remains unchanged and errors remain visible.

## Report and interpretation

The report includes aggregate row/cohort/TAG and enumerated-state counts, duplicate-key counts and bounded diagnostics containing only logical CSV row number, column name and stable reason code. It never includes values, collection IDs, TAG IDs/paths, timestamps, credentials, input/output paths, raw parser messages, or tracebacks. The process does not log or copy input data.

`structure_status` and `content_status` describe only file parsing and row contract checks. `analytical_eligibility`, `product_readiness`, and every entry in the report's `assessments` object always remain `NOT_ASSESSED`, including when every structural check passes. This explicitly covers required signals, context, labels, completeness/cadence, physical synchronization, exposure targets, leakage, analytical-use authorization, statistical sufficiency, and query/detector performance. Those require their own evidence and decisions under the [accepted Stage 4F plan](../../WAVE15-INTELLIGENCE-STAGE4F-ACCEPTANCE-PLAN.md); #655 was closed with disposition **NOT READY**, which remains the current product-readiness outcome.

Provide only an explicitly authorized copy. Keep operational exports and generated reports in the approved private location, not in GitHub. The repository fixture remains header-only; tests below use synthetic data only.

## Synthetic tests

Run the standard-library test suite with:

```powershell
python -m unittest discover -s docs/research/intelligence-stage4f-input-preflight -p "test_*.py" -v
```

The suite creates only temporary synthetic rows and local test files. It covers contract and timestamp edge cases, the exact seven-digit key precision, malformed input, deterministic redaction, resource bounds, CLI exit codes, and explicit no-overwrite/no-repository report-output rules. No operational history or private export is needed.
