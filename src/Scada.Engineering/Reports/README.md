# Reporting core boundary

Wave 09 DEV 1 first milestone keeps Reporting behind canonical Engineering and the accepted Historical Query v1 authority.

- `ReportEngineeringDto` is canonical/versioned Engineering.
- `ReportQueryEngineeringDto.Query` is the shared `HistoricalQueryRequest`; Reporting does not define another historical query language.
- persisted report definitions never contain an opaque Historical Query cursor.
- runtime parameter bindings may replace only predeclared range/search/filter-value slots; they cannot change dataset, field, operator or sort identity.
- `ReportExecutionService` delegates every data page to `IHistoricalQueryService` and never resolves relative time or opens PostgreSQL/TimescaleDB directly.
- page/row limits and cancellation are enforced by Reporting in addition to Historical Query bounds.
- report layout uses renderer-independent millimeter geometry; DOM/CSS/editor selection state is not Engineering.
- PDF/XLSX/print output is derived runtime output and is not implemented by this core milestone.
