using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Scada.Api.Reports;
using Scada.Core.HistoricalQueries;
using Scada.Engineering.Contracts;
using Scada.Engineering.DataQueries;
using Scada.Engineering.Reports;

namespace Scada.Drivers.Tests;

public sealed class ReportingV3Tests
{
    [Fact]
    public void Validation_RejectsWideRawAndNonUtcCalendarAlignment()
    {
        var report = Report() with
        {
            TableLayout = ReportTableLayout.Wide,
            Resolution = new ReportDataResolutionEngineeringDto(
                ReportDataResolutionMode.Raw,
                BucketAlignment: "localCalendar")
        };

        var problems = ReportEngineeringValidation.Validate(report);

        Assert.Contains(problems, p => p.Code == "REPORT_WIDE_RAW_UNSUPPORTED");
        Assert.Contains(problems, p => p.Code == "REPORT_BUCKET_ALIGNMENT_UNSUPPORTED");
    }

    [Fact]
    public void UnitLabelOverride_IsPresentationOnlyAndStableTagIdentityPersists()
    {
        var id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var report = Report() with
        {
            Variables =
            [
                new ReportVariableEngineeringDto(
                    id,
                    "Area.Pressure",
                    "Pressure",
                    "Double",
                    "kPa",
                    "driver.modbus",
                    "Process pressure",
                    UnitMode: ReportUnitMode.LabelOverride,
                    UnitLabel: "Pressão")
            ]
        };

        var variable = Assert.Single(report.Variables!);
        Assert.Equal(id, variable.TagId);
        Assert.Equal("kPa", variable.EngineeringUnit);
        Assert.Equal("Pressão", variable.UnitLabel);
        Assert.Equal(ReportUnitMode.LabelOverride, variable.UnitMode);
        Assert.Empty(ReportEngineeringValidation.Validate(report));
    }

    [Fact]
    public void ArtifactRenderer_ProducesPdfTypedXlsxAndUtf8CsvFromOneSnapshot()
    {
        var at = new DateTimeOffset(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);
        var columns = new[]
        {
            Column("number", HistoricalFieldType.Number),
            Column("when", HistoricalFieldType.DateTime),
            Column("enabled", HistoricalFieldType.Boolean),
            Column("message", HistoricalFieldType.String)
        };
        var row = new HistoricalQueryRow(new Dictionary<string, HistoricalQueryValue>(StringComparer.Ordinal)
        {
            ["number"] = HistoricalQueryValue.FromDouble(12.5),
            ["when"] = HistoricalQueryValue.FromDateTime(at),
            ["enabled"] = HistoricalQueryValue.FromBoolean(true),
            ["message"] = HistoricalQueryValue.FromString("quoted, \"value\"")
        });
        var result = new ReportExecutionResult(
            Guid.NewGuid(),
            "v3",
            new Dictionary<string, ReportParameterValue>(),
            [new ReportQueryExecutionResult("main", HistoricalDatasets.HistorianSamples, columns, [row], at.AddHours(-1), at)],
            at);
        var snapshot = new ReportGeneratedSnapshot(Guid.NewGuid(), null, Report(), null, result, at);
        var renderer = new ReportArtifactRenderer();

        var pdf = renderer.Render(snapshot, "pdf")!;
        var xlsx = renderer.Render(snapshot, "xlsx")!;
        var csv = renderer.Render(snapshot, "csv")!;

        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(pdf.Content, 0, 5));
        Assert.Equal("application/pdf", pdf.ContentType);

        using var archive = new ZipArchive(new MemoryStream(xlsx.Content), ZipArchiveMode.Read);
        var sheet = archive.GetEntry("xl/worksheets/sheet1.xml");
        Assert.NotNull(sheet);
        using var reader = new StreamReader(sheet!.Open(), Encoding.UTF8);
        var xml = reader.ReadToEnd();
        Assert.Contains("<v>12.5</v>", xml);
        Assert.Contains("t=\"b\"><v>1</v>", xml);
        Assert.Contains("s=\"1\"><v>", xml);
        Assert.Contains("quoted, &quot;value&quot;", xml);

        var csvText = Encoding.UTF8.GetString(csv.Content);
        Assert.StartsWith("\uFEFF", csvText);
        Assert.Contains("12.5", csvText);
        Assert.Contains("\"quoted, \"\"value\"\"\"", csvText);
    }

    [Fact]
    public void GeneratedStore_IsOwnerBound()
    {
        var at = DateTimeOffset.UtcNow;
        var result = new ReportExecutionResult(
            null, "v3", new Dictionary<string, ReportParameterValue>(),
            Array.Empty<ReportQueryExecutionResult>(), at);
        var store = new ReportGeneratedExecutionStore();
        var snapshot = store.Add("operator-a", Report(), null, result);

        Assert.True(store.TryGet(snapshot.ExecutionId, "operator-a", out _));
        Assert.False(store.TryGet(snapshot.ExecutionId, "operator-b", out _));
    }

    [Fact]
    public void ResolvedDataQueries_AreServerOnlyAndNeverAcceptedFromWireJson()
    {
        var definition = SavedQuery(
            Guid.Parse("22222222-3333-4444-5555-666666666666"),
            "active.saved",
            HistoricalDatasets.HistorianSamples,
            HistoricalTimeRange.Relative(7200));
        var request = new ReportExecutionRequest(
            Report(),
            TimeRange: new ReportRuntimeTimeRange(HistoricalTimeRangeKind.Relative, DurationSeconds: 3600),
            ResolvedDataQueries: [definition]);

        var json = JsonSerializer.Serialize(request, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.DoesNotContain("resolvedDataQueries", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("active.saved", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReportTimeRangeDefault_IsAppliedServerSideWithoutRuntimeOverride()
    {
        var historical = new RecordingHistoricalQueryService();
        var legacy = new ReportExecutionService(historical);
        var service = new ReportV3ExecutionService(
            legacy,
            new InMemoryDataQueryEngineeringRegistry(),
            new RecordingTransientExecutionService());

        var report = Report() with
        {
            TimeRange = new ReportTimeRangeEngineeringDto(
                HistoricalTimeRangeKind.Relative,
                DefaultRelativeDurationSeconds: 8 * 60 * 60)
        };

        await service.ExecuteAsync(new ReportExecutionRequest(report));

        var request = Assert.Single(historical.Requests);
        Assert.Equal(HistoricalTimeRangeKind.Relative, request.Range.Kind);
        Assert.Equal(8 * 60 * 60, request.Range.DurationSeconds);
    }

    [Fact]
    public async Task SavedDataQuery_UsesServerResolvedActiveDefinitionAndExplicitRuntimeRange()
    {
        var id = Guid.Parse("22222222-3333-4444-5555-666666666666");
        var workingDefinition = SavedQuery(
            id,
            "active.saved",
            HistoricalDatasets.AlarmEvents,
            HistoricalTimeRange.Relative(60));
        var activeDefinition = SavedQuery(
            id,
            "active.saved",
            HistoricalDatasets.HistorianSamples,
            HistoricalTimeRange.Relative(7200));

        var workingRegistry = new InMemoryDataQueryEngineeringRegistry();
        workingRegistry.Upsert(workingDefinition);
        var transient = new RecordingTransientExecutionService();
        var legacy = new ReportExecutionService(new RejectHistoricalQueryService());
        var service = new ReportV3ExecutionService(legacy, workingRegistry, transient);

        var from = new DateTimeOffset(2026, 10, 3, 10, 0, 0, TimeSpan.Zero);
        var to = from.AddHours(2);
        var report = Report() with
        {
            Queries =
            [
                new ReportQueryEngineeringDto(
                    "main",
                    new HistoricalQueryRequest(
                        HistoricalDatasets.HistorianSamples,
                        HistoricalTimeRange.Relative(300),
                        Page: new HistoricalPageRequest(100)),
                    DataQueryId: id,
                    DataQueryKey: "active.saved")
            ]
        };

        await service.ExecuteAsync(new ReportExecutionRequest(
            report,
            TimeRange: new ReportRuntimeTimeRange(
                HistoricalTimeRangeKind.Absolute,
                FromUtc: from,
                ToUtc: to),
            ResolvedDataQueries: [activeDefinition]));

        var executed = Assert.IsType<DataQueryEngineeringDto>(transient.Definition);
        Assert.Equal(HistoricalDatasets.HistorianSamples, executed.Query.Dataset);
        Assert.Equal(HistoricalTimeRangeKind.Absolute, executed.Query.Range.Kind);
        Assert.Equal(from, executed.Query.Range.FromUtc);
        Assert.Equal(to, executed.Query.Range.ToUtc);
        Assert.NotEqual(workingDefinition.Query.Dataset, executed.Query.Dataset);
    }

    private static DataQueryEngineeringDto SavedQuery(
        Guid id,
        string key,
        string dataset,
        HistoricalTimeRange range) =>
        new(
            id,
            key,
            key,
            DataQueryExecutionService.HistoricalProviderKey,
            new HistoricalQueryRequest(
                dataset,
                range,
                Page: new HistoricalPageRequest(100)));

    private static ReportEngineeringDto Report() =>
        new(
            Guid.Parse("11111111-2222-3333-4444-555555555555"),
            "process.summary",
            "Process Summary",
            Queries:
            [
                new ReportQueryEngineeringDto(
                    "main",
                    new HistoricalQueryRequest(
                        HistoricalDatasets.HistorianSamples,
                        HistoricalTimeRange.Relative(3600),
                        Page: new HistoricalPageRequest(100)))
            ],
            Sections:
            [
                new ReportSectionEngineeringDto(
                    Guid.NewGuid(),
                    "detail",
                    ReportSectionKind.Detail,
                    8,
                    "main",
                    Controls:
                    [
                        new ReportControlEngineeringDto(
                            Guid.NewGuid(), "value", ReportControlKind.DataField,
                            0, 0, 40, 6, QueryKey: "main", Field: "value")
                    ])
            ]);

    private static HistoricalColumn Column(string field, HistoricalFieldType type) =>
        new(field, type, Array.Empty<HistoricalFilterOperator>(), false, false, false);

    private sealed class RecordingHistoricalQueryService : IHistoricalQueryService
    {
        public List<HistoricalQueryRequest> Requests { get; } = [];

        public Task<HistoricalQueryResponse> QueryAsync(
            HistoricalQueryRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            var from = new DateTimeOffset(2026, 10, 3, 10, 0, 0, TimeSpan.Zero);
            var to = from.AddHours(1);
            return Task.FromResult(new HistoricalQueryResponse(
                HistoricalQueryContract.Version,
                request.Dataset,
                HistoricalQueryCatalog.Require(request.Dataset).Columns,
                Array.Empty<HistoricalQueryRow>(),
                from,
                to,
                null,
                request.Page?.Size ?? 100));
        }
    }

    private sealed class RejectHistoricalQueryService : IHistoricalQueryService
    {
        public Task<HistoricalQueryResponse> QueryAsync(
            HistoricalQueryRequest request,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Legacy Historical Query path must not execute for a saved Data Query.");
    }

    private sealed class RecordingTransientExecutionService : ITransientDataQueryExecutionService
    {
        public DataQueryEngineeringDto? Definition { get; private set; }

        public Task<DataQueryExecutionResponse> ExecuteTransientAsync(
            DataQueryEngineeringDto definition,
            DataQueryExecutionRequest? request = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Definition = definition;
            var from = definition.Query.Range.FromUtc ?? new DateTimeOffset(2026, 10, 3, 10, 0, 0, TimeSpan.Zero);
            var to = definition.Query.Range.ToUtc ?? from.AddHours(1);
            return Task.FromResult(new DataQueryExecutionResponse(
                1,
                definition.Id ?? Guid.NewGuid(),
                definition.Key,
                definition.Query.Dataset,
                definition.HistorianRetrieval?.Mode ?? HistorianRetrievalMode.Raw,
                HistoricalQueryCatalog.Require(definition.Query.Dataset).Columns,
                Array.Empty<DataQueryExecutionRow>(),
                from,
                to,
                null,
                100));
        }
    }
}
