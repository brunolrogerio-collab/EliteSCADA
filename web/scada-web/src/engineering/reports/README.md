# Report Designer Web / Preview

This folder is a web projection of the canonical `Scada.Engineering.Reports.ReportEngineeringDto` contract.

Authority rules for this Wave 09 slice:

- layout coordinates and dimensions remain canonical millimeters;
- report persistence uses the existing Engineering package Preview/Apply/CAS lifecycle;
- Preview calls the server-side `IReportExecutionService` seam and never opens a database or interprets Historical Query cursors in the browser;
- Historical Query v1 remains the only historical query descriptor stored by reports;
- runtime parameter values affect Preview execution only and do not mutate Report Engineering;
- image controls resolve existing canonical Visual Asset IDs only;
- PDF/XLSX/print output is derived output and is intentionally not persisted here.
