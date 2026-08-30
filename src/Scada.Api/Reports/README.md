# Report Preview API seam

`ReportExecutionApi` exposes the first Report Designer Preview endpoint without changing central `Program.cs` composition.

Coordinator integration must:

1. register the accepted Historical Query API/core dependencies;
2. call `AddReportExecutionApiCore()` during service composition;
3. call `MapReportExecutionEndpoints()` during endpoint composition.

The endpoint is protected by the existing workspace Engineering authorization filter and delegates all report data access to `IReportExecutionService`, which in turn delegates historical data access to Historical Query v1.
