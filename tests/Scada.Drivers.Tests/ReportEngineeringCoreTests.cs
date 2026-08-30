using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.HistoricalQueries;
using Scada.Core.Tags;
using Scada.Engineering.Assets;
using Scada.Engineering.Commands;
using Scada.Engineering.Contracts;
using Scada.Engineering.DataSources;
using Scada.Engineering.Gateways;
using Scada.Engineering.ImportExport;
using Scada.Engineering.Reports;
using Scada.Engineering.Scripts;
using Scada.Engineering.Security;
using Scada.Engineering.Views;
using Scada.Engineering.VisualAssets;

namespace Scada.Drivers.Tests;

public sealed class ReportEngineeringCoreTests
{
    [Fact]
    public void ReportValidation_UsesHistoricalQueryAndRejectsPersistedCursor()
    {
        var report = ValidReport();
        Assert.Empty(ReportEngineeringValidation.Validate(report));

        var query = report.Queries!.Single();
        var invalid = report with
        {
            Queries =
            [
                query with
                {
                    Query = query.Query with
                    {
                        Page = new HistoricalPageRequest(100, "client-owned-cursor")
                    }
                }
            ]
        };

        var problems = ReportEngineeringValidation.Validate(invalid);
        Assert.Contains(problems, x => x.Code == "REPORT_QUERY_CURSOR_PERSISTED");
    }

    [Fact]
    public async Task ReportExecution_PagesThroughHistoricalServiceAndPreservesInt64()
    {
        var from = new DateTimeOffset(2026, 8, 29, 20, 0, 0, TimeSpan.Zero);
        var to = from.AddHours(1);
        var fake = new RecordingHistoricalQueryService(
        [
            Response(
                [Row(("value", HistoricalQueryValue.FromInt64(long.MaxValue)))],
                from,
                to,
                "opaque-next"),
            Response(
                [Row(("value", HistoricalQueryValue.FromInt64(long.MinValue)))],
                from,
                to,
                null)
        ]);
        var service = new ReportExecutionService(fake);

        var result = await service.ExecuteAsync(new ReportExecutionRequest(ValidReport()));

        Assert.Equal(2, fake.Requests.Count);
        Assert.Null(fake.Requests[0].Page?.Cursor);
        Assert.Equal("opaque-next", fake.Requests[1].Page?.Cursor);
        Assert.Equal(HistoricalTimeRangeKind.Relative, fake.Requests[0].Range.Kind);
        Assert.Equal(HistoricalTimeRangeKind.Relative, fake.Requests[1].Range.Kind);

        var values = result.Queries.Single().Rows.Select(x => x.Cells["value"]).ToArray();
        Assert.Equal(HistoricalValueKind.Int64, values[0].Kind);
        Assert.Equal("9223372036854775807", values[0].Value);
        Assert.Equal("-9223372036854775808", values[1].Value);
        Assert.Equal(from, result.Queries.Single().FromUtc);
        Assert.Equal(to, result.Queries.Single().ToUtc);
    }

    [Fact]
    public async Task ReportExecution_BindsRuntimeParametersWithoutMutatingEngineering()
    {
        var originalFrom = new DateTimeOffset(2026, 8, 20, 0, 0, 0, TimeSpan.Zero);
        var originalTo = originalFrom.AddHours(1);
        var query = new HistoricalQueryRequest(
            HistoricalDatasets.AlarmEvents,
            HistoricalTimeRange.Absolute(originalFrom, originalTo),
            Filters:
            [
                new HistoricalFilter(
                    "priority",
                    HistoricalFilterOperator.Eq,
                    [HistoricalQueryValue.FromNumber(1)])
            ],
            Page: new HistoricalPageRequest(100));
        var report = new ReportEngineeringDto(
            Guid.NewGuid(),
            "alarms.by-period",
            "Alarm report",
            Parameters:
            [
                new("from", "From", ReportParameterType.DateTime, ReportParameterValue.FromDateTime(originalFrom)),
                new("to", "To", ReportParameterType.DateTime, ReportParameterValue.FromDateTime(originalTo)),
                new("priority", "Priority", ReportParameterType.Number, ReportParameterValue.FromNumber(1))
            ],
            Queries:
            [
                new(
                    "alarms",
                    query,
                    [
                        new("from", ReportQueryParameterTarget.AbsoluteFromUtc),
                        new("to", ReportQueryParameterTarget.AbsoluteToUtc),
                        new("priority", ReportQueryParameterTarget.FilterValue, 0, 0)
                    ])
            ],
            Sections:
            [
                new(Guid.NewGuid(), "detail", ReportSectionKind.Detail, 8, "alarms",
                    Controls:
                    [
                        new(Guid.NewGuid(), "message", ReportControlKind.DataField, 0, 0, 80, 6, QueryKey: "alarms", Field: "message")
                    ])
            ]);
        Assert.Empty(ReportEngineeringValidation.Validate(report));

        var runtimeFrom = new DateTimeOffset(2026, 8, 28, 10, 0, 0, TimeSpan.Zero);
        var runtimeTo = runtimeFrom.AddHours(2);
        var fake = new RecordingHistoricalQueryService(
        [
            new HistoricalQueryResponse(
                HistoricalQueryContract.Version,
                HistoricalDatasets.AlarmEvents,
                HistoricalQueryCatalog.Require(HistoricalDatasets.AlarmEvents).Columns,
                Array.Empty<HistoricalQueryRow>(),
                runtimeFrom,
                runtimeTo,
                null,
                100)
        ]);
        var service = new ReportExecutionService(fake);

        await service.ExecuteAsync(new ReportExecutionRequest(
            report,
            new Dictionary<string, ReportParameterValue>
            {
                ["from"] = ReportParameterValue.FromDateTime(runtimeFrom),
                ["to"] = ReportParameterValue.FromDateTime(runtimeTo),
                ["priority"] = ReportParameterValue.FromNumber(4)
            }));

        var sent = Assert.Single(fake.Requests);
        Assert.Equal(runtimeFrom, sent.Range.FromUtc);
        Assert.Equal(runtimeTo, sent.Range.ToUtc);
        Assert.Equal("4", sent.Filters![0].Values[0].Value);

        Assert.Equal(originalFrom, query.Range.FromUtc);
        Assert.Equal(originalTo, query.Range.ToUtc);
        Assert.Equal("1", query.Filters![0].Values[0].Value);
    }

    [Fact]
    public async Task ReportExecution_RejectsBoundedRowOverflow()
    {
        var from = new DateTimeOffset(2026, 8, 29, 20, 0, 0, TimeSpan.Zero);
        var to = from.AddHours(1);
        var row = Row(("value", HistoricalQueryValue.FromDouble(1)));
        var fake = new RecordingHistoricalQueryService(
        [
            Response([row, row], from, to, "next"),
            Response([row], from, to, null)
        ]);
        var service = new ReportExecutionService(
            fake,
            new ReportExecutionPolicy(MaximumRowsPerQuery: 2, MaximumTotalRows: 10, MaximumPagesPerQuery: 5));

        await Assert.ThrowsAsync<ReportExecutionLimitException>(() =>
            service.ExecuteAsync(new ReportExecutionRequest(ValidReport())));
    }

    [Fact]
    public void EngineeringExchange_RoundTripsReportsThroughPreviewApply()
    {
        var eventBus = new InMemoryScadaEventBus();
        using var alarms = new InMemoryAlarmEngine(eventBus);
        var tags = new InMemoryTagRegistry();
        var dataSources = new InMemoryDataSourceEngineeringRegistry();
        var assets = new InMemoryEngineeringAssetRegistry();
        var views = new InMemoryEngineeringViewRegistry();
        var security = new InMemorySecurityPolicyEngineeringRegistry();
        var commands = new InMemoryCommandEngineeringRegistry();
        var gateways = new InMemoryGatewayEngineeringRegistry();
        var scripts = new InMemoryScriptEngineeringRegistry();
        var visualAssets = new InMemoryVisualAssetEngineeringRegistry();
        var reports = new InMemoryReportEngineeringRegistry();
        var exchange = new EngineeringExchangeService(
            tags,
            alarms,
            dataSources,
            assets,
            views,
            security,
            commands,
            gateways,
            scripts,
            visualAssets,
            reports);

        reports.Upsert(ValidReport() with { Id = null });
        var exported = exchange.ExportJson(indented: false);
        var parsed = exchange.ParseJson(exported);

        Assert.Equal(14, parsed.SchemaVersion);
        var parsedReport = Assert.Single(parsed.Reports!);
        Assert.NotNull(parsedReport.Id);
        var parsedSection = Assert.Single(parsedReport.Sections!);
        Assert.NotNull(parsedSection.Id);
        Assert.NotNull(Assert.Single(parsedSection.Controls!).Id);

        reports.Clear();
        var preview = exchange.Preview(parsed, ImportMode.CreateAndUpdate);
        Assert.True(preview.CanApply);
        Assert.Contains(preview.Items, x => x.EntityKind == ImportEntityKind.Report && x.Operation == ImportOperation.Create);

        var applied = exchange.Apply(parsed, ImportMode.CreateAndUpdate);
        Assert.Empty(applied.Issues);
        var restored = Assert.Single(reports.SnapshotReports());
        Assert.Equal(parsedReport.Id, restored.Id);
        Assert.Equal(parsedReport.Queries!.Single().Query, restored.Queries!.Single().Query);
    }

    private static ReportEngineeringDto ValidReport()
    {
        var query = new HistoricalQueryRequest(
            HistoricalDatasets.HistorianSamples,
            HistoricalTimeRange.Relative(3600),
            Page: new HistoricalPageRequest(100));

        return new ReportEngineeringDto(
            Guid.NewGuid(),
            "process.history",
            "Process history",
            Queries: [new ReportQueryEngineeringDto("history", query)],
            Sections:
            [
                new(
                    Guid.NewGuid(),
                    "detail",
                    ReportSectionKind.Detail,
                    8,
                    "history",
                    Controls:
                    [
                        new(
                            Guid.NewGuid(),
                            "value",
                            ReportControlKind.DataField,
                            0,
                            0,
                            50,
                            6,
                            QueryKey: "history",
                            Field: "value")
                    ])
            ]);
    }

    private static HistoricalQueryResponse Response(
        IReadOnlyList<HistoricalQueryRow> rows,
        DateTimeOffset from,
        DateTimeOffset to,
        string? cursor) =>
        new(
            HistoricalQueryContract.Version,
            HistoricalDatasets.HistorianSamples,
            HistoricalQueryCatalog.Require(HistoricalDatasets.HistorianSamples).Columns,
            rows,
            from,
            to,
            cursor,
            100);

    private static HistoricalQueryRow Row(params (string Field, HistoricalQueryValue Value)[] cells) =>
        new(cells.ToDictionary(x => x.Field, x => x.Value, StringComparer.Ordinal));

    private sealed class RecordingHistoricalQueryService : IHistoricalQueryService
    {
        private readonly Queue<HistoricalQueryResponse> _responses;

        public RecordingHistoricalQueryService(IEnumerable<HistoricalQueryResponse> responses)
        {
            _responses = new Queue<HistoricalQueryResponse>(responses);
        }

        public List<HistoricalQueryRequest> Requests { get; } = [];

        public Task<HistoricalQueryResponse> QueryAsync(
            HistoricalQueryRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            if (_responses.Count == 0)
                throw new InvalidOperationException("No historical response was configured for the test.");
            return Task.FromResult(_responses.Dequeue());
        }
    }
}
