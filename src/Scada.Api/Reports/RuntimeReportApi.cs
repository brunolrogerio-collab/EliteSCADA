using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using Scada.Api.Runtime;
using Scada.Api.Security;
using Scada.Core.HistoricalQueries;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;
using Scada.Engineering.Persistence;
using Scada.Engineering.Reports;
using Scada.Security.Authorization;

namespace Scada.Api.Reports;

public sealed record RuntimeReportListItem(
    Guid Id,
    string Key,
    string Name,
    string? Category,
    string? Description);

public sealed record RuntimeReportDefinition(
    Guid Id,
    string Key,
    string Name,
    string? Category,
    string? Description,
    IReadOnlyCollection<ReportParameterEngineeringDto> Parameters,
    ReportTimeRangeEngineeringDto? TimeRange,
    ReportDataResolutionEngineeringDto? Resolution,
    IReadOnlyCollection<ReportVariableEngineeringDto> Variables,
    ReportTableLayout TableLayout,
    ReportPageEngineeringDto? Page,
    IReadOnlyCollection<ReportSectionEngineeringDto> Sections,
    IReadOnlyCollection<ReportGroupEngineeringDto> Groups,
    IReadOnlyCollection<ReportAggregateEngineeringDto> Aggregates);

public sealed record RuntimeReportGenerateRequest(
    IReadOnlyDictionary<string, ReportParameterValue>? Parameters = null,
    ReportRuntimeTimeRange? TimeRange = null);

public sealed record RuntimeReportGenerationResponse(
    Guid ExecutionId,
    string ReportKey,
    string ReportName,
    DateTimeOffset GeneratedAtUtc,
    ReportRuntimeTimeRange? TimeRange,
    ReportExecutionResult Result);

public sealed record ReportGeneratedSnapshot(
    Guid ExecutionId,
    string? OwnerSubjectId,
    ReportEngineeringDto Report,
    ReportRuntimeTimeRange? TimeRange,
    ReportExecutionResult Result,
    DateTimeOffset GeneratedAtUtc);

public sealed class ReportGenerationGate
{
    public const int MaximumConcurrentJobs = 2;
    private readonly SemaphoreSlim _slots = new(MaximumConcurrentJobs, MaximumConcurrentJobs);

    public async Task<IDisposable> EnterAsync(CancellationToken cancellationToken)
    {
        await _slots.WaitAsync(cancellationToken);
        return new Lease(_slots);
    }

    private sealed class Lease(SemaphoreSlim slots) : IDisposable
    {
        private SemaphoreSlim? _slots = slots;
        public void Dispose() => Interlocked.Exchange(ref _slots, null)?.Release();
    }
}

public sealed class ReportGeneratedExecutionStore
{
    public const int MaximumSnapshots = 64;
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(30);
    private readonly object _gate = new();
    private readonly Dictionary<Guid, ReportGeneratedSnapshot> _items = new();

    public ReportGeneratedSnapshot Add(
        string? ownerSubjectId,
        ReportEngineeringDto report,
        ReportRuntimeTimeRange? timeRange,
        ReportExecutionResult result)
    {
        var now = DateTimeOffset.UtcNow;
        lock (_gate)
        {
            Prune(now);
            if (_items.Count >= MaximumSnapshots)
            {
                var oldest = _items.Values.OrderBy(x => x.GeneratedAtUtc).First();
                _items.Remove(oldest.ExecutionId);
            }
            var snapshot = new ReportGeneratedSnapshot(
                Guid.NewGuid(),
                ownerSubjectId,
                report,
                timeRange,
                result,
                result.GeneratedAtUtc ?? now);
            _items[snapshot.ExecutionId] = snapshot;
            return snapshot;
        }
    }

    public bool TryGet(Guid id, string? ownerSubjectId, out ReportGeneratedSnapshot? snapshot)
    {
        lock (_gate)
        {
            Prune(DateTimeOffset.UtcNow);
            if (!_items.TryGetValue(id, out snapshot)) return false;
            if (!string.Equals(snapshot.OwnerSubjectId, ownerSubjectId, StringComparison.Ordinal))
            {
                snapshot = null;
                return false;
            }
            return true;
        }
    }

    private void Prune(DateTimeOffset now)
    {
        foreach (var id in _items
                     .Where(pair => now - pair.Value.GeneratedAtUtc > Lifetime)
                     .Select(pair => pair.Key)
                     .ToArray())
            _items.Remove(id);
    }
}

public sealed class RuntimeReportCatalog(
    IEngineeringProjectPersistenceService persistence,
    IEngineeringExchangeService exchange,
    ScadaRuntimeFacade runtime,
    IHistoricalQueryAuthorizer historicalAuthorization)
{
    public async Task<IReadOnlyList<ReportEngineeringDto>> ListAuthorizedAsync(
        CancellationToken cancellationToken)
    {
        var package = await ActivePackageAsync(cancellationToken);
        if (package is null) return Array.Empty<ReportEngineeringDto>();
        var reports = new List<ReportEngineeringDto>();
        foreach (var report in package.Reports ?? Array.Empty<ReportEngineeringDto>())
        {
            var allowed = true;
            foreach (var dataset in (report.Queries ?? Array.Empty<ReportQueryEngineeringDto>())
                         .Select(q => q.Query.Dataset)
                         .Distinct(StringComparer.Ordinal))
            {
                var decision = await historicalAuthorization.AuthorizeAsync(dataset, cancellationToken);
                if (decision.Outcome != HistoricalAuthorizationOutcome.Allowed)
                {
                    allowed = false;
                    break;
                }
            }
            if (allowed) reports.Add(report);
        }
        return reports.OrderBy(x => x.Category).ThenBy(x => x.Name).ToArray();
    }

    public async Task<ReportEngineeringDto?> FindAuthorizedAsync(
        string key,
        CancellationToken cancellationToken) =>
        (await ListAuthorizedAsync(cancellationToken))
        .FirstOrDefault(report => report.Key.Equals(key, StringComparison.OrdinalIgnoreCase));

    public async Task<HistoricalAuthorizationOutcome> AuthorizeSnapshotAsync(
        ReportEngineeringDto report,
        CancellationToken cancellationToken)
    {
        foreach (var dataset in (report.Queries ?? Array.Empty<ReportQueryEngineeringDto>())
                     .Select(q => q.Query.Dataset)
                     .Distinct(StringComparer.Ordinal))
        {
            var decision = await historicalAuthorization.AuthorizeAsync(dataset, cancellationToken);
            if (decision.Outcome != HistoricalAuthorizationOutcome.Allowed)
                return decision.Outcome;
        }
        return HistoricalAuthorizationOutcome.Allowed;
    }

    private async Task<EngineeringPackage?> ActivePackageAsync(CancellationToken cancellationToken)
    {
        var descriptor = runtime.Describe();
        if (string.IsNullOrWhiteSpace(descriptor.ProjectKey) || !descriptor.Revision.HasValue) return null;
        var snapshot = await persistence.LoadActiveAsync(descriptor.ProjectKey, cancellationToken);
        if (snapshot is null || snapshot.Revision != descriptor.Revision) return null;
        var package = exchange.ParseJson(snapshot.EngineeringJson);
        var after = runtime.Describe();
        return after.Revision == descriptor.Revision &&
               string.Equals(after.ProjectKey, descriptor.ProjectKey, StringComparison.OrdinalIgnoreCase)
            ? package
            : null;
    }
}

public static class RuntimeReportApi
{
    public const string CatalogRoute = "/api/runtime/reports";
    public const string GenerateRoute = "/api/runtime/reports/{key}/generate";
    public const string ExecutionRoute = "/api/runtime/reports/executions/{executionId:guid}";
    public const string ExportRoute = "/api/runtime/reports/executions/{executionId:guid}/export/{format}";
    public const string PrintRoute = "/api/runtime/reports/executions/{executionId:guid}/print";

    public static void MapRuntimeReportEndpoints(this WebApplication app)
    {
        app.MapGet(CatalogRoute, async (
            HttpContext context,
            RuntimeReportCatalog catalog,
            ScadaRuntimeFacade runtime,
            ApiAuthorizationService security,
            CancellationToken ct) =>
        {
            var access = await RuntimeViewAsync(context, runtime, security, ct);
            if (access is not null) return access;
            var reports = await catalog.ListAuthorizedAsync(ct);
            return Results.Ok(reports.Select(report => new RuntimeReportListItem(
                report.Id ?? Guid.Empty, report.Key, report.Name, report.Category, report.Description)));
        });

        app.MapGet("/api/runtime/reports/{key}", async (
            string key,
            HttpContext context,
            RuntimeReportCatalog catalog,
            ScadaRuntimeFacade runtime,
            ApiAuthorizationService security,
            CancellationToken ct) =>
        {
            var access = await RuntimeViewAsync(context, runtime, security, ct);
            if (access is not null) return access;
            var report = await catalog.FindAuthorizedAsync(key, ct);
            return report is null
                ? Results.NotFound(new ReportExecutionApiError("report_not_found", "Report was not found or is not authorized."))
                : Results.Ok(ToDefinition(report));
        });

        app.MapPost(GenerateRoute, async (
            string key,
            RuntimeReportGenerateRequest request,
            HttpContext context,
            RuntimeReportCatalog catalog,
            IReportExecutionService execution,
            ReportGeneratedExecutionStore store,
            ReportGenerationGate generationGate,
            ScadaRuntimeFacade runtime,
            ApiAuthorizationService security,
            CancellationToken ct) =>
        {
            var access = await RuntimeViewAsync(context, runtime, security, ct);
            if (access is not null) return access;
            var report = await catalog.FindAuthorizedAsync(key, ct);
            if (report is null)
                return Results.NotFound(new ReportExecutionApiError("report_not_found", "Report was not found or is not authorized."));

            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(TimeSpan.FromSeconds(30));
                using var generationLease = await generationGate.EnterAsync(timeout.Token);
                var result = await execution.ExecuteAsync(
                    new ReportExecutionRequest(report, request.Parameters, request.TimeRange),
                    timeout.Token);
                var owner = security.AuthenticationEnabled ? security.GetPrincipal(context).SubjectId : null;
                var snapshot = store.Add(owner, report, request.TimeRange, result);
                return Results.Ok(new RuntimeReportGenerationResponse(
                    snapshot.ExecutionId,
                    report.Key,
                    report.Name,
                    snapshot.GeneratedAtUtc,
                    request.TimeRange,
                    result));
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                return Results.Json(
                    new ReportExecutionApiError("report_timeout", "Report generation timed out. Reduce the period or increase the interval."),
                    statusCode: StatusCodes.Status408RequestTimeout);
            }
            catch (ReportExecutionValidationException ex)
            {
                return Results.BadRequest(new ReportExecutionValidationApiError("invalid_report", ex.Message, ex.Problems));
            }
            catch (ReportExecutionLimitException ex)
            {
                return Results.BadRequest(new ReportExecutionApiError("report_limit", ex.Message));
            }
            catch (HistoricalQueryValidationException ex)
            {
                return Results.BadRequest(new ReportExecutionApiError("invalid_query", ex.Message));
            }
            catch (HistoricalQueryUnauthorizedException)
            {
                return Results.Unauthorized();
            }
            catch (HistoricalQueryForbiddenException)
            {
                return Results.Json(new ReportExecutionApiError("forbidden", "Forbidden."), statusCode: StatusCodes.Status403Forbidden);
            }
        });

        app.MapGet(ExecutionRoute, async (
            Guid executionId,
            HttpContext context,
            ReportGeneratedExecutionStore store,
            RuntimeReportCatalog catalog,
            ScadaRuntimeFacade runtime,
            ApiAuthorizationService security,
            CancellationToken ct) =>
        {
            var access = await RuntimeViewAsync(context, runtime, security, ct);
            if (access is not null) return access;
            var owner = security.AuthenticationEnabled ? security.GetPrincipal(context).SubjectId : null;
            if (!store.TryGet(executionId, owner, out var snapshot) || snapshot is null)
                return Results.NotFound(new ReportExecutionApiError("execution_not_found", "Generated report snapshot was not found or expired."));
            var dataAccess = await catalog.AuthorizeSnapshotAsync(snapshot.Report, ct);
            if (dataAccess == HistoricalAuthorizationOutcome.Unauthenticated) return Results.Unauthorized();
            if (dataAccess != HistoricalAuthorizationOutcome.Allowed)
                return Results.Json(new ReportExecutionApiError("forbidden", "Forbidden."), statusCode: StatusCodes.Status403Forbidden);
            return Results.Ok(snapshot);
        });

        app.MapGet(ExportRoute, async (
            Guid executionId,
            string format,
            HttpContext context,
            ReportGeneratedExecutionStore store,
            RuntimeReportCatalog catalog,
            ReportArtifactRenderer renderer,
            ScadaRuntimeFacade runtime,
            ApiAuthorizationService security,
            CancellationToken ct) =>
        {
            var access = await RuntimeViewAsync(context, runtime, security, ct);
            if (access is not null) return access;
            var owner = security.AuthenticationEnabled ? security.GetPrincipal(context).SubjectId : null;
            if (!store.TryGet(executionId, owner, out var snapshot) || snapshot is null)
                return Results.NotFound(new ReportExecutionApiError("execution_not_found", "Generated report snapshot was not found or expired."));
            var dataAccess = await catalog.AuthorizeSnapshotAsync(snapshot.Report, ct);
            if (dataAccess == HistoricalAuthorizationOutcome.Unauthenticated) return Results.Unauthorized();
            if (dataAccess != HistoricalAuthorizationOutcome.Allowed)
                return Results.Json(new ReportExecutionApiError("forbidden", "Forbidden."), statusCode: StatusCodes.Status403Forbidden);

            try
            {
                ct.ThrowIfCancellationRequested();
                var artifact = renderer.Render(snapshot, format);
                return artifact is null
                    ? Results.BadRequest(new ReportExecutionApiError("export_format", "Supported formats are pdf, xlsx and csv."))
                    : Results.File(artifact.Content, artifact.ContentType, artifact.FileName);
            }
            catch (ReportExecutionLimitException ex)
            {
                return Results.BadRequest(new ReportExecutionApiError("report_limit", ex.Message));
            }
        });

        app.MapGet(PrintRoute, async (
            Guid executionId,
            HttpContext context,
            ReportGeneratedExecutionStore store,
            RuntimeReportCatalog catalog,
            ReportArtifactRenderer renderer,
            ScadaRuntimeFacade runtime,
            ApiAuthorizationService security,
            CancellationToken ct) =>
        {
            var access = await RuntimeViewAsync(context, runtime, security, ct);
            if (access is not null) return access;
            var owner = security.AuthenticationEnabled ? security.GetPrincipal(context).SubjectId : null;
            if (!store.TryGet(executionId, owner, out var snapshot) || snapshot is null)
                return Results.NotFound(new ReportExecutionApiError("execution_not_found", "Generated report snapshot was not found or expired."));
            var dataAccess = await catalog.AuthorizeSnapshotAsync(snapshot.Report, ct);
            if (dataAccess == HistoricalAuthorizationOutcome.Unauthenticated) return Results.Unauthorized();
            if (dataAccess != HistoricalAuthorizationOutcome.Allowed)
                return Results.Json(new ReportExecutionApiError("forbidden", "Forbidden."), statusCode: StatusCodes.Status403Forbidden);
            try
            {
                ct.ThrowIfCancellationRequested();
                var artifact = renderer.Render(snapshot, "pdf")!;
                return Results.File(artifact.Content, artifact.ContentType);
            }
            catch (ReportExecutionLimitException ex)
            {
                return Results.BadRequest(new ReportExecutionApiError("report_limit", ex.Message));
            }
        });
    }

    private static RuntimeReportDefinition ToDefinition(ReportEngineeringDto report) =>
        new(
            report.Id ?? Guid.Empty,
            report.Key,
            report.Name,
            report.Category,
            report.Description,
            report.Parameters ?? Array.Empty<ReportParameterEngineeringDto>(),
            report.TimeRange,
            report.Resolution,
            report.Variables ?? Array.Empty<ReportVariableEngineeringDto>(),
            report.TableLayout,
            report.Page,
            report.Sections ?? Array.Empty<ReportSectionEngineeringDto>(),
            report.Groups ?? Array.Empty<ReportGroupEngineeringDto>(),
            report.Aggregates ?? Array.Empty<ReportAggregateEngineeringDto>());

    private static async Task<IResult?> RuntimeViewAsync(
        HttpContext context,
        ScadaRuntimeFacade runtime,
        ApiAuthorizationService security,
        CancellationToken cancellationToken)
    {
        if (!security.AuthenticationEnabled) return null;
        var access = await security.CheckRuntimeAsync(
            context,
            runtime,
            SecurityCapability.View,
            cancellationToken: cancellationToken);
        return access.FailureResult();
    }
}

public sealed record ReportArtifact(byte[] Content, string ContentType, string FileName);

public sealed class ReportArtifactRenderer
{
    public const int MaximumOutputBytes = 16 * 1024 * 1024;
    public const int MaximumCells = 250000;
    public const int MaximumPdfPages = 500;

    public ReportArtifact? Render(ReportGeneratedSnapshot snapshot, string format)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var normalized = format.Trim().ToLowerInvariant();
        var artifact = normalized switch
        {
            "csv" => Csv(snapshot),
            "xlsx" => Xlsx(snapshot),
            "pdf" => Pdf(snapshot),
            _ => null
        };
        if (artifact is not null && artifact.Content.Length > MaximumOutputBytes)
            throw new ReportExecutionLimitException(
                $"Generated export exceeds the maximum output size ({MaximumOutputBytes} bytes). Reduce the period or increase the interval.");
        return artifact;
    }

    private static ReportArtifact Csv(ReportGeneratedSnapshot snapshot)
    {
        var query = snapshot.Result.Queries.FirstOrDefault();
        var sb = new StringBuilder();
        if (query is not null)
        {
            var fields = query.Columns.Select(c => c.Field).ToArray();
            sb.AppendLine(string.Join(",", fields.Select(EscapeCsv)));
            foreach (var row in query.Rows)
                sb.AppendLine(string.Join(",", fields.Select(field =>
                    EscapeCsv(row.Cells.TryGetValue(field, out var value) ? value.Value ?? string.Empty : string.Empty))));
        }
        return new ReportArtifact(
            new UTF8Encoding(true).GetBytes(sb.ToString()),
            "text/csv; charset=utf-8",
            SafeName(snapshot.Report.Name) + ".csv");
    }

    private static ReportArtifact Xlsx(ReportGeneratedSnapshot snapshot)
    {
        var cellCount = snapshot.Result.Queries.Sum(q => (long)q.Rows.Count * Math.Max(1, q.Columns.Count));
        if (cellCount > MaximumCells)
            throw new ReportExecutionLimitException($"Excel export exceeds the maximum cell count ({MaximumCells}).");

        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            WriteZip(archive, "[Content_Types].xml", ContentTypes(snapshot.Result.Queries.Count));
            WriteZip(archive, "_rels/.rels",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
            WriteZip(archive, "xl/workbook.xml", Workbook(snapshot.Result.Queries));
            WriteZip(archive, "xl/_rels/workbook.xml.rels", WorkbookRelationships(snapshot.Result.Queries.Count));
            WriteZip(archive, "xl/styles.xml", StylesXml);
            var index = 1;
            foreach (var query in snapshot.Result.Queries)
                WriteZip(archive, $"xl/worksheets/sheet{index++}.xml", Worksheet(query));
        }
        return new ReportArtifact(
            stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            SafeName(snapshot.Report.Name) + ".xlsx");
    }

    private static ReportArtifact Pdf(ReportGeneratedSnapshot snapshot)
    {
        const int rowsPerPage = 42;
        var lines = new List<string>
        {
            snapshot.Report.Name,
            $"Generated At: {snapshot.GeneratedAtUtc:O}"
        };
        foreach (var query in snapshot.Result.Queries)
        {
            lines.Add($"{query.QueryKey} | {query.FromUtc:O} - {query.ToUtc:O} | {query.RetrievalMode}");
            var fields = query.Columns.Take(8).Select(c => c.Field).ToArray();
            lines.Add(string.Join(" | ", fields));
            foreach (var row in query.Rows)
                lines.Add(string.Join(" | ", fields.Select(f => Truncate(row.Cells.TryGetValue(f, out var v) ? v.Value : null, 18))));
        }
        var pages = Math.Max(1, (int)Math.Ceiling(lines.Count / (double)rowsPerPage));
        if (pages > MaximumPdfPages)
            throw new ReportExecutionLimitException($"PDF export exceeds the maximum page count ({MaximumPdfPages}).");

        var bytes = SimplePdf(lines, rowsPerPage, snapshot.Report.Page?.Orientation == ReportPageOrientation.Landscape);
        return new ReportArtifact(bytes, "application/pdf", SafeName(snapshot.Report.Name) + ".pdf");
    }

    private static byte[] SimplePdf(IReadOnlyList<string> lines, int rowsPerPage, bool landscape)
    {
        var width = landscape ? 842 : 595;
        var height = landscape ? 595 : 842;
        var objects = new List<byte[]>();
        var pages = new List<int>();
        var fontObject = 3;
        objects.Add(Array.Empty<byte>()); // catalog
        objects.Add(Array.Empty<byte>()); // pages root
        objects.Add(Ascii("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"));

        for (var pageIndex = 0; pageIndex * rowsPerPage < Math.Max(lines.Count, 1); pageIndex++)
        {
            var slice = lines.Skip(pageIndex * rowsPerPage).Take(rowsPerPage).ToArray();
            var content = new StringBuilder("BT /F1 9 Tf 36 " + (height - 42) + " Td 11 TL\n");
            if (slice.Length == 0) slice = [string.Empty];
            foreach (var line in slice)
                content.Append("(").Append(PdfEscape(line)).Append(") Tj T*\n");
            content.Append("ET");
            var contentBytes = Ascii(content.ToString());
            var contentNumber = objects.Count + 1;
            objects.Add(Ascii($"<< /Length {contentBytes.Length} >>\nstream\n{Encoding.ASCII.GetString(contentBytes)}\nendstream"));
            var pageNumber = objects.Count + 1;
            objects.Add(Ascii($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {width} {height}] /Resources << /Font << /F1 {fontObject} 0 R >> >> /Contents {contentNumber} 0 R >>"));
            pages.Add(pageNumber);
        }

        objects[1] = Ascii($"<< /Type /Pages /Kids [{string.Join(" ", pages.Select(p => $"{p} 0 R"))}] /Count {pages.Count} >>");
        objects[0] = Ascii("<< /Type /Catalog /Pages 2 0 R >>");

        using var output = new MemoryStream();
        WriteAscii(output, "%PDF-1.4\n%");
        output.Write([0xE2,0xE3,0xCF,0xD3]);
        WriteAscii(output, "\n");
        var offsets = new List<long> { 0 };
        for (var i = 0; i < objects.Count; i++)
        {
            offsets.Add(output.Position);
            WriteAscii(output, $"{i + 1} 0 obj\n");
            output.Write(objects[i]);
            WriteAscii(output, "\nendobj\n");
        }
        var xref = output.Position;
        WriteAscii(output, $"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1))
            WriteAscii(output, $"{offset:0000000000} 00000 n \n");
        WriteAscii(output, $"trailer << /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF");
        return output.ToArray();
    }

    private static string Worksheet(ReportQueryExecutionResult query)
    {
        var sb = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>");
        var rowNumber = 1;
        sb.Append($"<row r=\"{rowNumber++}\">");
        foreach (var column in query.Columns)
            sb.Append(InlineCell(column.Field));
        sb.Append("</row>");
        foreach (var row in query.Rows)
        {
            sb.Append($"<row r=\"{rowNumber++}\">");
            foreach (var column in query.Columns)
                sb.Append(Cell(row.Cells.TryGetValue(column.Field, out var value) ? value : HistoricalQueryValue.Null()));
            sb.Append("</row>");
        }
        sb.Append("</sheetData></worksheet>");
        return sb.ToString();
    }

    private static string Cell(HistoricalQueryValue value)
    {
        if (value.Value is null) return "<c/>";
        return value.Kind switch
        {
            HistoricalValueKind.Boolean => $"<c t=\"b\"><v>{(value.AsBoolean() ? 1 : 0)}</v></c>",
            HistoricalValueKind.Int16 or HistoricalValueKind.Int32 or HistoricalValueKind.Int64 or
            HistoricalValueKind.Float or HistoricalValueKind.Double or HistoricalValueKind.Number =>
                $"<c><v>{XmlEscape(value.Value)}</v></c>",
            HistoricalValueKind.DateTime =>
                $"<c s=\"1\"><v>{value.AsDateTime().UtcDateTime.ToOADate().ToString("R", CultureInfo.InvariantCulture)}</v></c>",
            _ => InlineCell(value.Value)
        };
    }

    private static string InlineCell(string text) =>
        $"<c t=\"inlineStr\"><is><t xml:space=\"preserve\">{XmlEscape(text)}</t></is></c>";

    private static string Workbook(IReadOnlyList<ReportQueryExecutionResult> queries)
    {
        var names = queries.Count == 0 ? new[] { "Report" } : queries.Select(q => q.QueryKey).ToArray();
        return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets>" +
               string.Concat(names.Select((name, i) => $"<sheet name=\"{XmlEscape(SheetName(name, i + 1))}\" sheetId=\"{i + 1}\" r:id=\"rId{i + 1}\"/>")) +
               "</sheets></workbook>";
    }

    private static string WorkbookRelationships(int count)
    {
        var actual = Math.Max(1, count);
        var rels = string.Concat(Enumerable.Range(1, actual)
            .Select(i => $"<Relationship Id=\"rId{i}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet{i}.xml\"/>"));
        return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
               rels + $"<Relationship Id=\"rId{actual + 1}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/></Relationships>";
    }

    private static string ContentTypes(int count)
    {
        var actual = Math.Max(1, count);
        var sheets = string.Concat(Enumerable.Range(1, actual)
            .Select(i => $"<Override PartName=\"/xl/worksheets/sheet{i}.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>"));
        return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>" +
               sheets + "</Types>";
    }

    private const string StylesXml = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><fonts count=\"1\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts><fills count=\"1\"><fill><patternFill patternType=\"none\"/></fill></fills><borders count=\"1\"><border/></borders><cellStyleXfs count=\"1\"><xf/></cellStyleXfs><cellXfs count=\"2\"><xf numFmtId=\"0\"/><xf numFmtId=\"14\" applyNumberFormat=\"1\"/></cellXfs></styleSheet>";

    private static void WriteZip(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Fastest);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    private static string EscapeCsv(string? value)
    {
        var text = value ?? string.Empty;
        return text.IndexOfAny([',','\"','\r','\n']) >= 0
            ? "\"" + text.Replace("\"", "\"\"") + "\""
            : text;
    }

    private static string PdfEscape(string? value) =>
        (value ?? string.Empty).Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)")
            .Select(ch => ch <= 127 ? ch : '?').Aggregate(new StringBuilder(), (sb, ch) => sb.Append(ch)).ToString();

    private static string XmlEscape(string value) =>
        System.Security.SecurityElement.Escape(value) ?? string.Empty;

    private static string SafeName(string name)
    {
        var safe = new string(name.Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '_').ToArray()).Trim('_');
        return string.IsNullOrWhiteSpace(safe) ? "report" : safe;
    }

    private static string SheetName(string name, int index)
    {
        var invalid = new[] { ':', '\\', '/', '?', '*', '[', ']' };
        var safe = new string(name.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
        if (string.IsNullOrWhiteSpace(safe)) safe = $"Sheet{index}";
        return safe.Length <= 31 ? safe : safe[..31];
    }

    private static string Truncate(string? value, int length)
    {
        var text = value ?? string.Empty;
        return text.Length <= length ? text : text[..Math.Max(0, length - 1)] + "…";
    }

    private static byte[] Ascii(string value) => Encoding.ASCII.GetBytes(value);
    private static void WriteAscii(Stream stream, string value) => stream.Write(Ascii(value));
}
