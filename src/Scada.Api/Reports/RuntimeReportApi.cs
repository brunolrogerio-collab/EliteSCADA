using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using Scada.Api.Runtime;
using Scada.Api.Security;
using Scada.Core.HistoricalQueries;
using Scada.Engineering.Contracts;
using Scada.Engineering.DataQueries;
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
    DateTimeOffset GeneratedAtUtc)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyDictionary<Guid, ReportImage> Images { get; init; } = new Dictionary<Guid, ReportImage>();
}

public sealed record ReportImage(byte[] Content, string MediaType, int Width, int Height);

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
        ReportExecutionResult result,
        IReadOnlyDictionary<Guid, ReportImage>? images = null)
    {
        var now = DateTimeOffset.UtcNow;
        lock (_gate)
        {
            Prune(now);
            var resultRows = result.Queries.Sum(q => q.Rows.Count);
            while (_items.Count > 0 && (_items.Count >= MaximumSnapshots || _items.Values.Sum(s => s.Result.Queries.Sum(q => q.Rows.Count)) + resultRows > 100000))
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
                result.GeneratedAtUtc ?? now) { Images = images ?? new Dictionary<Guid, ReportImage>() };
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

public sealed record AuthorizedRuntimeReport(
    ReportEngineeringDto Report,
    IReadOnlyCollection<DataQueryEngineeringDto> ResolvedDataQueries)
{
    public IReadOnlyDictionary<Guid, ReportImage>? Images { get; init; }
}

public sealed class RuntimeReportCatalog(
    IEngineeringProjectPersistenceService persistence,
    IEngineeringExchangeService exchange,
    ScadaRuntimeFacade runtime,
    IHistoricalQueryAuthorizer historicalAuthorization,
    IEngineeringProjectStore? assetStore = null)
{
    public async Task<IReadOnlyList<ReportEngineeringDto>> ListAuthorizedAsync(
        CancellationToken cancellationToken)
    {
        var package = await ActivePackageAsync(cancellationToken);
        if (package is null) return Array.Empty<ReportEngineeringDto>();

        var reports = new List<ReportEngineeringDto>();
        foreach (var report in package.Reports ?? Array.Empty<ReportEngineeringDto>())
        {
            var authority = await AuthorizeReportAsync(package, report, cancellationToken);
            if (authority is not null) reports.Add(authority.Report);
        }
        return reports.OrderBy(x => x.Category).ThenBy(x => x.Name).ToArray();
    }

    public async Task<AuthorizedRuntimeReport?> FindAuthorityAsync(
        string key,
        CancellationToken cancellationToken,
        bool includeImages = false)
    {
        var descriptor = runtime.Describe();
        var package = await ActivePackageAsync(cancellationToken);
        if (package is null) return null;
        var report = (package.Reports ?? Array.Empty<ReportEngineeringDto>())
            .FirstOrDefault(candidate => candidate.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        var authority = report is null ? null : await AuthorizeReportAsync(package, report, cancellationToken);
        if (authority is null || !includeImages) return authority;
        var assetIds = (report!.Sections ?? []).SelectMany(section => section.Controls ?? [])
            .Where(control => control.Kind == ReportControlKind.Image && control.AssetId.HasValue)
            .Select(control => control.AssetId!.Value).Distinct().ToArray();
        if (assetIds.Length == 0) return authority;
        var active = runtime.Describe();
        if (assetStore is null || descriptor.ProjectKey is null || descriptor.Revision is null ||
            active.ProjectKey != descriptor.ProjectKey || active.Revision != descriptor.Revision)
            throw new ReportExecutionLimitException("The active image revision is unavailable. Reopen the report.");
        var payloads = await assetStore.LoadRevisionAssetsAsync(descriptor.ProjectKey, descriptor.Revision.Value, cancellationToken);
        var images = new Dictionary<Guid, ReportImage>();
        long totalPixels = 0;
        foreach (var id in assetIds)
        {
            var metadata = package.VisualAssets?.FirstOrDefault(asset => asset.Id == id);
            var payload = payloads.FirstOrDefault(asset => asset.AssetId == id);
            if (metadata is null || payload is null || metadata.MediaType is not ("image/png" or "image/jpeg") ||
                !string.Equals(metadata.Sha256, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(payload.Content)), StringComparison.OrdinalIgnoreCase))
                throw new ReportExecutionLimitException("A referenced report image is missing or is not an approved PNG or JPEG in this revision.");
            using var input = new MemoryStream(payload.Content, false);
            var info = StbImageSharp.ImageInfo.FromStream(input);
            if (info is null || info.Value.Width <= 0 || info.Value.Height <= 0 ||
                (totalPixels += (long)info.Value.Width * info.Value.Height) > 16000000)
                throw new ReportExecutionLimitException("Invalid report image or report images exceed 16 megapixels in total.");
            images[id] = new ReportImage(payload.Content, metadata.MediaType, info.Value.Width, info.Value.Height);
        }
        return authority with { Images = images };
    }

    public async Task<ReportEngineeringDto?> FindAuthorizedAsync(
        string key,
        CancellationToken cancellationToken) =>
        (await FindAuthorityAsync(key, cancellationToken))?.Report;

    public async Task<HistoricalAuthorizationOutcome> AuthorizeResultAsync(
        ReportExecutionResult result,
        CancellationToken cancellationToken)
    {
        foreach (var dataset in result.Queries
                     .Select(query => query.Dataset)
                     .Distinct(StringComparer.Ordinal))
        {
            var decision = await historicalAuthorization.AuthorizeAsync(dataset, cancellationToken);
            if (decision.Outcome != HistoricalAuthorizationOutcome.Allowed)
                return decision.Outcome;
        }
        return HistoricalAuthorizationOutcome.Allowed;
    }

    private async Task<AuthorizedRuntimeReport?> AuthorizeReportAsync(
        EngineeringPackage package,
        ReportEngineeringDto report,
        CancellationToken cancellationToken)
    {
        if (!TryResolveDataQueries(package, report, out var resolved, out var datasets))
            return null;

        foreach (var dataset in datasets)
        {
            var decision = await historicalAuthorization.AuthorizeAsync(dataset, cancellationToken);
            if (decision.Outcome != HistoricalAuthorizationOutcome.Allowed)
                return null;
        }

        return new AuthorizedRuntimeReport(report, resolved);
    }

    private static bool TryResolveDataQueries(
        EngineeringPackage package,
        ReportEngineeringDto report,
        out IReadOnlyCollection<DataQueryEngineeringDto> resolved,
        out IReadOnlyCollection<string> datasets)
    {
        var available = package.DataQueries ?? Array.Empty<DataQueryEngineeringDto>();
        var definitions = new List<DataQueryEngineeringDto>();
        var effectiveDatasets = new HashSet<string>(StringComparer.Ordinal);

        foreach (var reportQuery in report.Queries ?? Array.Empty<ReportQueryEngineeringDto>())
        {
            var hasSavedReference =
                reportQuery.DataQueryId.HasValue ||
                !string.IsNullOrWhiteSpace(reportQuery.DataQueryKey);
            if (!hasSavedReference)
            {
                effectiveDatasets.Add(reportQuery.Query.Dataset);
                continue;
            }

            DataQueryEngineeringDto? saved = null;
            if (reportQuery.DataQueryId.HasValue)
                saved = available.FirstOrDefault(definition => definition.Id == reportQuery.DataQueryId.Value);
            if (saved is null && !string.IsNullOrWhiteSpace(reportQuery.DataQueryKey))
                saved = available.FirstOrDefault(definition =>
                    definition.Key.Equals(reportQuery.DataQueryKey, StringComparison.OrdinalIgnoreCase));
            if (saved is null)
            {
                resolved = Array.Empty<DataQueryEngineeringDto>();
                datasets = Array.Empty<string>();
                return false;
            }

            if (!definitions.Any(definition => definition.Id == saved.Id))
                definitions.Add(saved);
            effectiveDatasets.Add(saved.Query.Dataset);
        }

        resolved = definitions;
        datasets = effectiveDatasets;
        return true;
    }

    internal async Task<EngineeringPackage?> ActivePackageAsync(CancellationToken cancellationToken)
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
            AuthorizedRuntimeReport? authority;
            try { authority = await catalog.FindAuthorityAsync(key, ct, includeImages: true); }
            catch (ReportExecutionLimitException ex) { return Results.BadRequest(new ReportExecutionApiError("report_image", ex.Message)); }
            if (authority is null)
                return Results.NotFound(new ReportExecutionApiError("report_not_found", "Report was not found or is not authorized."));
            var report = authority.Report;

            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(TimeSpan.FromSeconds(120));
                using var generationLease = await generationGate.EnterAsync(timeout.Token);
                var result = await execution.ExecuteAsync(
                    new ReportExecutionRequest(
                        report,
                        request.Parameters,
                        request.TimeRange,
                        authority.ResolvedDataQueries),
                    timeout.Token);
                var owner = security.AuthenticationEnabled ? security.GetPrincipal(context).SubjectId : null;
                var snapshot = store.Add(owner, report, request.TimeRange, result, authority.Images);
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
            var dataAccess = await catalog.AuthorizeResultAsync(snapshot.Result, ct);
            if (dataAccess == HistoricalAuthorizationOutcome.Unauthenticated) return Results.Unauthorized();
            if (dataAccess != HistoricalAuthorizationOutcome.Allowed)
                return Results.Json(new ReportExecutionApiError("forbidden", "Forbidden."), statusCode: StatusCodes.Status403Forbidden);
            return Results.Ok(snapshot);
        });

        app.MapGet(ExecutionRoute + "/images/{assetId:guid}", async (
            Guid executionId, Guid assetId,
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
            var dataAccess = await catalog.AuthorizeResultAsync(snapshot.Result, ct);
            if (dataAccess == HistoricalAuthorizationOutcome.Unauthenticated) return Results.Unauthorized();
            if (dataAccess != HistoricalAuthorizationOutcome.Allowed)
                return Results.Json(new ReportExecutionApiError("forbidden", "Forbidden."), statusCode: StatusCodes.Status403Forbidden);
            return snapshot.Images.TryGetValue(assetId, out var image) ? Results.File(image.Content, image.MediaType) : Results.NotFound();
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
            var dataAccess = await catalog.AuthorizeResultAsync(snapshot.Result, ct);
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
            var dataAccess = await catalog.AuthorizeResultAsync(snapshot.Result, ct);
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
    public const int MaximumOutputBytes = 64 * 1024 * 1024;
    public const int MaximumCells = 1000000;
    public const int MaximumPdfPages = 50000;

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
            [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(sb.ToString())],
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
            WriteZip(archive, "[Content_Types].xml", ContentTypes(snapshot.Result.Queries.Count, snapshot.Images.Count > 0));
            WriteZip(archive, "_rels/.rels",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
            WriteZip(archive, "xl/workbook.xml", Workbook(snapshot.Result.Queries));
            WriteZip(archive, "xl/_rels/workbook.xml.rels", WorkbookRelationships(snapshot.Result.Queries.Count));
            var formats = snapshot.Report.Resolution?.LatestObserved == true
                ? (snapshot.Report.Variables ?? []).Where(v => v.Visible).ToDictionary(
                    ObservedReportTable.Label, ObservedReportTable.ExcelFormat)
                : new Dictionary<string, string>();
            var uniqueFormats = formats.Values.Distinct().ToList();
            WriteZip(archive, "xl/styles.xml", BuildStyles(uniqueFormats));
            var styles = formats.ToDictionary(pair => pair.Key, pair => uniqueFormats.IndexOf(pair.Value) + 2);
            if (snapshot.Images.Count > 0) WriteWorkbookImages(archive, snapshot.Images, snapshot.Result.Queries.Count);
            var index = 1;
            foreach (var query in snapshot.Result.Queries)
                WriteZip(archive, $"xl/worksheets/sheet{index++}.xml", Worksheet(query, styles, snapshot.Report.Page ?? new(), snapshot.Images.Count));
        }
        return new ReportArtifact(
            stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            SafeName(snapshot.Report.Name) + ".xlsx");
    }

    private static ReportArtifact Pdf(ReportGeneratedSnapshot snapshot)
    {
        var page = PdfPageGeometry(snapshot.Report.Page);
        var pages = BuildPdfPages(snapshot, page);
        if (pages.Count > MaximumPdfPages)
            throw new ReportExecutionLimitException($"PDF export exceeds the maximum page count ({MaximumPdfPages}).");

        var bytes = SimplePdf(pages, page.WidthPoints, page.HeightPoints, snapshot.Images);
        return new ReportArtifact(bytes, "application/pdf", SafeName(snapshot.Report.Name) + ".pdf");
    }

    private sealed record PdfPageSpec(IReadOnlyList<string> Commands);
    private sealed record PdfGeometry(double WidthPoints, double HeightPoints, double ContentLeftPoints, double ContentTopPoints, double ContentWidthPoints, double ContentHeightPoints);

    private static PdfGeometry PdfPageGeometry(ReportPageEngineeringDto? page)
    {
        var key = (page?.PaperSizeKey ?? "A4").Trim().ToUpperInvariant();
        var portraitMillimeters = key switch
        {
            "A3" => (Width: 297d, Height: 420d),
            "LETTER" => (Width: 215.9d, Height: 279.4d),
            _ => (Width: 210d, Height: 297d)
        };
        var size = page?.Orientation == ReportPageOrientation.Landscape
            ? (Width: portraitMillimeters.Height, Height: portraitMillimeters.Width)
            : portraitMillimeters;
        var left = Mm(page?.MarginLeftMillimeters ?? 10);
        var right = Mm(page?.MarginRightMillimeters ?? 10);
        var numberBand = page?.ShowPageNumbers != false ? (page?.PageNumberFontSizePoints ?? 8) + Mm(3) : 0;
        var top = Math.Max(Mm(page?.MarginTopMillimeters ?? 10), page?.PageNumberPosition == "header" ? numberBand : 0);
        var bottom = Math.Max(Mm(page?.MarginBottomMillimeters ?? 10), page?.PageNumberPosition != "header" ? numberBand : 0);
        return new PdfGeometry(
            Mm(size.Width),
            Mm(size.Height),
            left,
            top,
            Math.Max(Mm(20), Mm(size.Width) - left - right),
            Math.Max(Mm(20), Mm(size.Height) - top - bottom));
    }

    private static IReadOnlyList<PdfPageSpec> BuildPdfPages(ReportGeneratedSnapshot snapshot, PdfGeometry page)
    {
        var report = snapshot.Report;
        var sections = report.Sections ?? Array.Empty<ReportSectionEngineeringDto>();
        var reportHeader = sections.Where(section => section.Kind == ReportSectionKind.ReportHeader).ToArray();
        var pageHeader = sections.Where(section => section.Kind == ReportSectionKind.PageHeader).ToArray();
        var details = sections.Where(section => section.Kind == ReportSectionKind.Detail).ToArray();
        var pageFooter = sections.Where(section => section.Kind == ReportSectionKind.PageFooter).ToArray();
        var reportFooter = sections.Where(section => section.Kind == ReportSectionKind.ReportFooter).ToArray();
        var groupHeaders = sections.Where(section => section.Kind == ReportSectionKind.GroupHeader).ToArray();
        var groupFooters = sections.Where(section => section.Kind == ReportSectionKind.GroupFooter).ToArray();

        if (details.Length == 0)
            return BuildFallbackPdfPages(snapshot, page);

        var primary = snapshot.Result.Queries.FirstOrDefault();
        var rows = primary?.Rows ?? Array.Empty<HistoricalQueryRow>();
        var detailHeight = Math.Max(Mm(1), details.Sum(section => Mm(section.HeightMillimeters)));
        var fixedHeight = pageHeader.Sum(section => Mm(section.HeightMillimeters)) +
                          pageFooter.Sum(section => Mm(section.HeightMillimeters));
        var firstExtra = reportHeader.Sum(section => Mm(section.HeightMillimeters));
        var lastExtra = reportFooter.Sum(section => Mm(section.HeightMillimeters));
        var rowsPerPage = Math.Max(1, (int)Math.Floor((page.ContentHeightPoints - fixedHeight - Math.Max(firstExtra, lastExtra)) / detailHeight));
        var totalPages = Math.Max(1, (int)Math.Ceiling(rows.Count / (double)rowsPerPage));
        var output = new List<PdfPageSpec>(totalPages);

        for (var pageIndex = 0; pageIndex < totalPages; pageIndex++)
        {
            var commands = new List<string>();
            var y = page.ContentTopPoints;
            if (pageIndex == 0)
                foreach (var section in reportHeader)
                    y = RenderPdfSection(commands, snapshot, section, null, page, y);

            foreach (var section in pageHeader)
                y = RenderPdfSection(commands, snapshot, section, null, page, y);

            var pageRows = rows.Skip(pageIndex * rowsPerPage).Take(rowsPerPage).ToArray();
            HistoricalQueryRow? previous = pageIndex == 0 || pageIndex * rowsPerPage == 0 ? null : rows[pageIndex * rowsPerPage - 1];

            foreach (var row in pageRows)
            {
                foreach (var group in report.Groups ?? Array.Empty<ReportGroupEngineeringDto>())
                {
                    var currentValue = PdfCell(row, group.Field);
                    var previousValue = previous is null ? null : PdfCell(previous, group.Field);
                    if (previous is null || !string.Equals(currentValue, previousValue, StringComparison.Ordinal))
                    {
                        foreach (var section in groupHeaders.Where(section => string.Equals(section.GroupKey, group.Key, StringComparison.OrdinalIgnoreCase)))
                            y = RenderPdfSection(commands, snapshot, section, row, page, y);
                    }
                }

                foreach (var section in details)
                    y = RenderPdfSection(commands, snapshot, section, row, page, y);

                previous = row;
            }

            if (pageRows.Length > 0)
            {
                var last = pageRows[^1];
                foreach (var group in report.Groups ?? Array.Empty<ReportGroupEngineeringDto>())
                {
                    var globalIndex = pageIndex * rowsPerPage + pageRows.Length;
                    var next = globalIndex < rows.Count ? rows[globalIndex] : null;
                    if (next is null || !string.Equals(PdfCell(last, group.Field), PdfCell(next, group.Field), StringComparison.Ordinal))
                    {
                        foreach (var section in groupFooters.Where(section => string.Equals(section.GroupKey, group.Key, StringComparison.OrdinalIgnoreCase)))
                            y = RenderPdfSection(commands, snapshot, section, last, page, y);
                    }
                }
            }

            foreach (var section in pageFooter)
                RenderPdfSection(commands, snapshot, section, null, page, page.ContentTopPoints + page.ContentHeightPoints - pageFooter.Sum(item => Mm(item.HeightMillimeters)));

            if (pageIndex == totalPages - 1)
            {
                var footerY = page.ContentTopPoints + page.ContentHeightPoints - pageFooter.Sum(item => Mm(item.HeightMillimeters)) - reportFooter.Sum(item => Mm(item.HeightMillimeters));
                foreach (var section in reportFooter)
                    footerY = RenderPdfSection(commands, snapshot, section, null, page, footerY);
            }

            if (report.Page?.ShowPageNumbers != false)
                AddPageNumber(commands, report.Page ?? new(), page, pageIndex + 1, totalPages);

            output.Add(new PdfPageSpec(commands));
        }

        return output;
    }

    private static void AddPageNumber(List<string> commands, ReportPageEngineeringDto settings, PdfGeometry page, int current, int total)
    {
        var alignment = settings.PageNumberAlignment;
        var x = page.ContentLeftPoints + (alignment switch { "right" => page.ContentWidthPoints, "center" => page.ContentWidthPoints / 2, _ => 0 });
        var y = settings.PageNumberPosition == "header" ? Mm(2) + settings.PageNumberFontSizePoints : page.HeightPoints - Mm(2);
        DrawPdfText(commands, settings.ShowPageCount ? $"{current} / {total}" : current.ToString(CultureInfo.InvariantCulture),
            x, y, settings.PageNumberFontSizePoints, alignment, settings.PageNumberFontFamily);
    }

    private static IReadOnlyList<PdfPageSpec> BuildFallbackPdfPages(ReportGeneratedSnapshot snapshot, PdfGeometry page)
    {
        var pages = new List<PdfPageSpec>();
        var settings = snapshot.Report.Page ?? new();
        foreach (var query in snapshot.Result.Queries)
        {
            var fields = query.Columns.Select(column => column.Field).ToArray();
            var lineWidth = (int)(page.ContentWidthPoints / 4);
            var perColumn = Math.Max(1, lineWidth / Math.Max(1, fields.Length) - 2);
            var perPage = Math.Max(1, (int)((page.ContentHeightPoints - 48) / 10));
            for (var offset = 0; offset < Math.Max(1, query.Rows.Count); offset += perPage)
            {
                var y = page.ContentTopPoints + 14;
                var commands = new List<string>();
                DrawPdfText(commands, snapshot.Report.Name, page.ContentLeftPoints, y, 14, "left");
                DrawPdfText(commands, $"{query.QueryKey} | {query.FromUtc:O} - {query.ToUtc:O}", page.ContentLeftPoints, y += 14, 8, "left");
                DrawPdfText(commands, string.Join(" | ", fields.Select(f => Truncate(f, perColumn))), page.ContentLeftPoints, y += 10, 7, "left");
                foreach (var row in query.Rows.Skip(offset).Take(perPage))
                    DrawPdfText(commands, string.Join(" | ", fields.Select(f => Truncate(PdfCell(row, f), perColumn))), page.ContentLeftPoints, y += 10, 7, "left");
                pages.Add(new PdfPageSpec(commands));
            }
        }
        if (pages.Count == 0) pages.Add(new PdfPageSpec([ ]));
        if (settings.ShowPageNumbers)
            for (int i = 0; i < pages.Count; i++)
            {
                var numbered = pages[i].Commands.ToList();
                AddPageNumber(numbered, settings, page, i + 1, pages.Count);
                pages[i] = new PdfPageSpec(numbered);
            }
        return pages;
    }

    private static double RenderPdfSection(
        List<string> commands,
        ReportGeneratedSnapshot snapshot,
        ReportSectionEngineeringDto section,
        HistoricalQueryRow? row,
        PdfGeometry page,
        double top)
    {
        var sectionHeight = Mm(section.HeightMillimeters);
        foreach (var control in section.Controls ?? Array.Empty<ReportControlEngineeringDto>())
        {
            if (control.Kind == ReportControlKind.PageBreak) continue;
            var x = page.ContentLeftPoints + Mm(control.XMillimeters);
            var y = top + Mm(control.YMillimeters);
            var width = Mm(control.WidthMillimeters);
            var height = Mm(control.HeightMillimeters);
            var border = Math.Max(0, control.Style?.BorderWidth ?? 0);

            switch (control.Kind)
            {
                case ReportControlKind.Line:
                    DrawPdfLine(commands, x, y + height / 2, x + width, y + height / 2, Math.Max(0.5, border));
                    break;
                case ReportControlKind.Rectangle:
                case ReportControlKind.RoundedRectangle:
                    DrawPdfRectangle(commands, x, y, width, height, Math.Max(0.5, border));
                    break;
                case ReportControlKind.Ellipse:
                    DrawPdfEllipse(commands, x, y, width, height, Math.Max(0.5, border));
                    break;
                case ReportControlKind.Chart:
                    DrawPdfChart(commands, snapshot, control, x, y, width, height);
                    break;
                case ReportControlKind.Image:
                    if (control.AssetId is { } id && snapshot.Images.TryGetValue(id, out var image))
                    {
                        var scale = Math.Min(width / image.Width, height / image.Height);
                        var w = image.Width * scale;
                        var h = image.Height * scale;
                        commands.Add(FormattableString.Invariant($"q {w:0.###} 0 0 {-h:0.###} {x + (width-w)/2:0.###} {y + (height+h)/2:0.###} cm /Im{id:N} Do Q"));
                    }
                    break;
                default:
                    var text = control.Kind is ReportControlKind.DataField or ReportControlKind.BooleanState or ReportControlKind.Barcode
                        ? PdfCell(row, control.Field)
                        : control.Text ?? control.Key;
                    var size = Math.Clamp(control.Style?.FontSizePoints ?? 9, 5, 72);
                    DrawPdfText(commands, Truncate(text, Math.Max(8, (int)(width / Math.Max(3, size * 0.45)))), x + (control.Style?.TextAlignment switch { ReportTextAlignment.Center => width/2, ReportTextAlignment.Right => width, _ => 0 }), y + Math.Min(height - 1, size + 1), size, PdfAlignment(control.Style?.TextAlignment), control.Style?.FontFamily);
                    if (border > 0)
                        DrawPdfRectangle(commands, x, y, width, height, border);
                    break;
            }
        }
        return top + sectionHeight;
    }

    private static void DrawPdfChart(
        List<string> commands,
        ReportGeneratedSnapshot snapshot,
        ReportControlEngineeringDto control,
        double x,
        double y,
        double width,
        double height)
    {
        var query = snapshot.Result.Queries.FirstOrDefault(item =>
                        string.Equals(item.QueryKey, control.QueryKey, StringComparison.OrdinalIgnoreCase))
                    ?? snapshot.Result.Queries.FirstOrDefault();
        if (query is null || string.IsNullOrWhiteSpace(control.Field)) return;
        var values = query.Rows
            .Select((row, index) => (Index: index, Text: PdfCell(row, control.Field)))
            .Select(point => (point.Index, Value: double.TryParse(point.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : double.NaN))
            .Where(point => double.IsFinite(point.Value))
            .ToArray();
        if (values.Length < 2) return;
        var min = values.Min(point => point.Value);
        var max = values.Max(point => point.Value);
        var span = max == min ? 1 : max - min;
        var sb = new StringBuilder("q 0.8 w ");
        for (var i = 0; i < values.Length; i++)
        {
            var px = x + width * i / Math.Max(1, values.Length - 1);
            var py = y + height - (values[i].Value - min) / span * height;
            sb.Append(FormattableString.Invariant($"{px:0.###} {py:0.###} {(i == 0 ? "m" : "l")} "));
        }
        sb.Append("S Q");
        commands.Add(sb.ToString());
    }

    private static void DrawPdfText(List<string> commands, string? text, double x, double yTop, double size, string alignment, string? family = null)
    {
        var font = family switch { "Times New Roman" => "F2", "Courier New" => "F3", _ => "F1" };
        var safe = PdfEscape(text);
        var estimatedWidth = (text?.Length ?? 0) * size * 0.5;
        var drawX = alignment switch
        {
            "right" => x - estimatedWidth,
            "center" => x - estimatedWidth / 2,
            _ => x
        };
        commands.Add(FormattableString.Invariant($"BT /{font} {size:0.###} Tf 1 0 0 -1 {drawX:0.###} {yTop:0.###} Tm ({safe}) Tj ET"));
    }

    private static void DrawPdfLine(List<string> commands, double x1, double y1Top, double x2, double y2Top, double width) =>
        commands.Add(FormattableString.Invariant($"q {width:0.###} w {x1:0.###} {y1Top:0.###} m {x2:0.###} {y2Top:0.###} l S Q"));

    private static void DrawPdfRectangle(List<string> commands, double x, double yTop, double width, double height, double lineWidth) =>
        commands.Add(FormattableString.Invariant($"q {lineWidth:0.###} w {x:0.###} {yTop:0.###} {width:0.###} {height:0.###} re S Q"));

    private static void DrawPdfEllipse(List<string> commands, double x, double yTop, double width, double height, double lineWidth)
    {
        var k = 0.5522847498;
        var rx = width / 2;
        var ry = height / 2;
        var cx = x + rx;
        var cy = yTop + ry;
        var command =
            FormattableString.Invariant($"q {lineWidth:0.###} w {cx + rx:0.###} {cy:0.###} m ") +
            FormattableString.Invariant($"{cx + rx:0.###} {cy + k * ry:0.###} {cx + k * rx:0.###} {cy + ry:0.###} {cx:0.###} {cy + ry:0.###} c ") +
            FormattableString.Invariant($"{cx - k * rx:0.###} {cy + ry:0.###} {cx - rx:0.###} {cy + k * ry:0.###} {cx - rx:0.###} {cy:0.###} c ") +
            FormattableString.Invariant($"{cx - rx:0.###} {cy - k * ry:0.###} {cx - k * rx:0.###} {cy - ry:0.###} {cx:0.###} {cy - ry:0.###} c ") +
            FormattableString.Invariant($"{cx + k * rx:0.###} {cy - ry:0.###} {cx + rx:0.###} {cy - k * ry:0.###} {cx + rx:0.###} {cy:0.###} c S Q");
        commands.Add(command);
    }

    private static string PdfAlignment(ReportTextAlignment? alignment) => alignment switch
    {
        ReportTextAlignment.Center => "center",
        ReportTextAlignment.Right => "right",
        _ => "left"
    };

    private static string PdfCell(HistoricalQueryRow? row, string? field)
    {
        if (row is null || string.IsNullOrWhiteSpace(field) || !row.Cells.TryGetValue(field, out var value))
            return string.Empty;
        return value.Value ?? string.Empty;
    }

    private static byte[] CompressedStream(string attributes, byte[] bytes)
    {
        using var output = new MemoryStream();
        using (var compressed = new ZLibStream(output, CompressionLevel.Fastest, true)) compressed.Write(bytes);
        return [.. Ascii($"<< {attributes} /Filter /FlateDecode /Length {output.Length} >>\nstream\n"), .. output.ToArray(), .. Ascii("\nendstream")];
    }

    private static double Mm(double value) => value * 72d / 25.4d;

    private static byte[] SimplePdf(IReadOnlyList<PdfPageSpec> pages, double width, double height, IReadOnlyDictionary<Guid, ReportImage> images)
    {
        var objects = new List<byte[]>();
        var pageObjects = new List<int>();
        const int fontObject = 3;
        objects.Add(Array.Empty<byte>());
        objects.Add(Array.Empty<byte>());
        objects.Add(Ascii("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>"));

        objects.Add(Ascii("<< /Type /Font /Subtype /Type1 /BaseFont /Times-Roman /Encoding /WinAnsiEncoding >>"));
        objects.Add(Ascii("<< /Type /Font /Subtype /Type1 /BaseFont /Courier /Encoding /WinAnsiEncoding >>"));
        var imageResources = new StringBuilder();
        foreach (var (id, image) in images)
        {
            var rgba = StbImageSharp.ImageResult.FromMemory(image.Content, StbImageSharp.ColorComponents.RedGreenBlueAlpha).Data;
            var rgb = new byte[checked(image.Width * image.Height * 3)];
            for (int i = 0, j = 0; i < rgba.Length; i += 4)
                for (int channel = 0; channel < 3; channel++)
                    rgb[j++] = (byte)((rgba[i + channel] * rgba[i + 3] + 255 * (255 - rgba[i + 3]) + 127) / 255);
            imageResources.Append($" /Im{id:N} {objects.Count + 1} 0 R");
            objects.Add(CompressedStream($"/Type /XObject /Subtype /Image /Width {image.Width} /Height {image.Height} /ColorSpace /DeviceRGB /BitsPerComponent 8", rgb));
        }

        foreach (var page in pages)
        {
            var transformed = $"q 1 0 0 -1 0 {height.ToString("0.###", CultureInfo.InvariantCulture)} cm\n" +
                              string.Join("\n", page.Commands) +
                              "\nQ";
            var contentBytes = Ascii(transformed);
            var contentNumber = objects.Count + 1;
            objects.Add(contentBytes.Length < 2048 ? Ascii($"<< /Length {contentBytes.Length} >>\nstream\n{Encoding.ASCII.GetString(contentBytes)}\nendstream") : CompressedStream("", contentBytes));
            var pageNumber = objects.Count + 1;
            objects.Add(Ascii(FormattableString.Invariant(
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {width:0.###} {height:0.###}] /Resources << /Font << /F1 {fontObject} 0 R /F2 4 0 R /F3 5 0 R >> /XObject <<{imageResources} >> >> /Contents {contentNumber} 0 R >>")));
            pageObjects.Add(pageNumber);
        }

        objects[1] = Ascii($"<< /Type /Pages /Kids [{string.Join(" ", pageObjects.Select(number => $"{number} 0 R"))}] /Count {pageObjects.Count} >>");
        objects[0] = Ascii("<< /Type /Catalog /Pages 2 0 R >>");

        using var output = new MemoryStream();
        WriteAscii(output, "%PDF-1.4\n%");
        output.Write([0xE2, 0xE3, 0xCF, 0xD3]);
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

    private static string BuildStyles(IReadOnlyList<string> formats)
    {
        var root = System.Xml.Linq.XElement.Parse(StylesXml);
        var ns = root.Name.Namespace;
        var cellFormats = root.Element(ns + "cellXfs")!;
        var numbers = new System.Xml.Linq.XElement(ns + "numFmts", new System.Xml.Linq.XAttribute("count", formats.Count + 1));
        root.AddFirst(numbers);
        numbers.Add(new System.Xml.Linq.XElement(ns + "numFmt", new System.Xml.Linq.XAttribute("numFmtId", 164),
            new System.Xml.Linq.XAttribute("formatCode", "yyyy-mm-dd hh:mm:ss.000")));
        cellFormats.Elements().ElementAt(1).SetAttributeValue("numFmtId", 164);
        for (var i = 0; i < formats.Count; i++)
        {
            numbers.Add(new System.Xml.Linq.XElement(ns + "numFmt", new System.Xml.Linq.XAttribute("numFmtId", 165 + i),
                new System.Xml.Linq.XAttribute("formatCode", formats[i])));
            cellFormats.Add(new System.Xml.Linq.XElement(ns + "xf", new System.Xml.Linq.XAttribute("numFmtId", 165 + i),
                new System.Xml.Linq.XAttribute("applyNumberFormat", 1)));
        }
        cellFormats.SetAttributeValue("count", formats.Count + 2);
        root.Add(new System.Xml.Linq.XElement(ns + "cellStyles", new System.Xml.Linq.XAttribute("count", 1),
            new System.Xml.Linq.XElement(ns + "cellStyle", new System.Xml.Linq.XAttribute("name", "Normal"),
                new System.Xml.Linq.XAttribute("xfId", 0), new System.Xml.Linq.XAttribute("builtinId", 0))));
        return root.ToString();
    }

    private static void WriteWorkbookImages(ZipArchive book, IReadOnlyDictionary<Guid, ReportImage> images, int sheetCount)
    {
        const string spreadsheet = "http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing";
        const string drawing = "http://schemas.openxmlformats.org/drawingml/2006/main";
        const string relations = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        const string package = "http://schemas.openxmlformats.org/package/2006/relationships";
        var anchors = new StringBuilder($"<xdr:wsDr xmlns:xdr=\"{spreadsheet}\" xmlns:a=\"{drawing}\" xmlns:r=\"{relations}\">");
        var links = new StringBuilder($"<Relationships xmlns=\"{package}\">");
        var row = 0;
        foreach (var (id, image) in images)
        {
            var name = id.ToString("N") + (image.MediaType == "image/png" ? ".png" : ".jpeg");
            using (var stream = book.CreateEntry("xl/media/" + name, CompressionLevel.NoCompression).Open()) stream.Write(image.Content);
            var scale = Math.Min(200d / image.Width, 60d / image.Height);
            var width = (long)(image.Width * scale * 9525);
            var height = (long)(image.Height * scale * 9525);
            var index = row + 1;
            links.Append($"<Relationship Id=\"rId{index}\" Type=\"{relations}/image\" Target=\"../media/{name}\"/>");
            anchors.Append($"""
                <xdr:oneCellAnchor><xdr:from><xdr:col>0</xdr:col><xdr:colOff>0</xdr:colOff><xdr:row>{row}</xdr:row><xdr:rowOff>0</xdr:rowOff></xdr:from><xdr:ext cx="{width}" cy="{height}"/>
                <xdr:pic><xdr:nvPicPr><xdr:cNvPr id="{index}" name="Image {index}"/><xdr:cNvPicPr/></xdr:nvPicPr>
                <xdr:blipFill><a:blip r:embed="rId{index}"/><a:stretch><a:fillRect/></a:stretch></xdr:blipFill>
                <xdr:spPr><a:prstGeom prst="rect"><a:avLst/></a:prstGeom></xdr:spPr></xdr:pic><xdr:clientData/></xdr:oneCellAnchor>
                """);
            row++;
        }
        WriteZip(book, "xl/drawings/report.xml", anchors.Append("</xdr:wsDr>").ToString());
        WriteZip(book, "xl/drawings/_rels/report.xml.rels", links.Append("</Relationships>").ToString());
        for (var i = 1; i <= sheetCount; i++)
            WriteZip(book, $"xl/worksheets/_rels/sheet{i}.xml.rels", $"<Relationships xmlns=\"{package}\"><Relationship Id=\"rId1\" Type=\"{relations}/drawing\" Target=\"../drawings/report.xml\"/></Relationships>");
    }

    private static string Worksheet(ReportQueryExecutionResult query, IReadOnlyDictionary<string, int> styles, ReportPageEngineeringDto page, int imageCount)
    {
        var sb = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
        if (query.Columns.Count > 0)
        {
            sb.Append("<cols>");
            for (var i = 0; i < query.Columns.Count; i++)
            {
                var field = query.Columns[i].Field;
                var width = field switch { "timestamp" => 24, "message" => 65, "tag.path" => 36, _ => Math.Clamp(field.Length + 4, 16, 45) };
                sb.Append($"<col min=\"{i + 1}\" max=\"{i + 1}\" width=\"{width}\" customWidth=\"1\"/>");
            }
            sb.Append("</cols>");
        }
        sb.Append("<sheetData>");
        var rowNumber = 1;
        for (var i = 0; i < imageCount; i++) sb.Append($"<row r=\"{rowNumber++}\" ht=\"48\" customHeight=\"1\"/>");
        sb.Append($"<row r=\"{rowNumber++}\">");
        foreach (var column in query.Columns)
            sb.Append(InlineCell(column.Field));
        sb.Append("</row>");
        for (var i = 0; i < query.Rows.Count; i++)
        {
            var row = query.Rows[i];
            sb.Append($"<row r=\"{rowNumber++}\">");
            foreach (var column in query.Columns)
            {
                if (query.NumericRows is { } numeric && numeric[i].Cells.TryGetValue(column.Field, out var native)
                    && styles.TryGetValue(column.Field, out var style))
                    sb.Append($"<c s=\"{style}\"><v>{XmlEscape(native.Value!)}</v></c>");
                else sb.Append(Cell(row.Cells.GetValueOrDefault(column.Field) ?? HistoricalQueryValue.Null()));
            }
            sb.Append("</row>");
        }
        sb.Append("</sheetData>");
        sb.Append($"<pageSetup orientation=\"{(page.Orientation == ReportPageOrientation.Landscape ? "landscape" : "portrait")}\"/>");
        if (page.ShowPageNumbers)
        {
            var tag = page.PageNumberPosition == "header" ? "oddHeader" : "oddFooter";
            var align = page.PageNumberAlignment switch { "left" => "&L", "right" => "&R", _ => "&C" };
            var number = align + "&\"" + page.PageNumberFontFamily + ",Regular\"&" + Math.Round(page.PageNumberFontSizePoints).ToString(CultureInfo.InvariantCulture) + " &P" + (page.ShowPageCount ? " / &N" : "");
            sb.Append($"<headerFooter><{tag}>{XmlEscape(number)}</{tag}></headerFooter>");
        }
        if (imageCount > 0) sb.Append("<drawing xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\" r:id=\"rId1\"/>");
        sb.Append("</worksheet>");
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

    private static string ContentTypes(int count, bool hasImages)
    {
        var actual = Math.Max(1, count);
        var sheets = string.Concat(Enumerable.Range(1, actual)
            .Select(i => $"<Override PartName=\"/xl/worksheets/sheet{i}.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>"));
        return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>" +
               sheets + (hasImages ? "<Default Extension=\"png\" ContentType=\"image/png\"/><Default Extension=\"jpeg\" ContentType=\"image/jpeg\"/><Override PartName=\"/xl/drawings/report.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.drawing+xml\"/>" : "") + "</Types>";
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

    private static readonly Encoding PdfTextEncoding = CodePagesEncodingProvider.Instance.GetEncoding(1252)!;

    private static string PdfEscape(string? value)
    {
        var escaped = new StringBuilder();
        foreach (var b in PdfTextEncoding.GetBytes(value ?? ""))
        {
            if (b is < 32 or > 126) escaped.Append('\\').Append(Convert.ToString(b, 8).PadLeft(3, '0'));
            else
            {
                if (b is (byte)'(' or (byte)')' or (byte)'\\') escaped.Append('\\');
                escaped.Append((char)b);
            }
        }
        return escaped.ToString();
    }

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
