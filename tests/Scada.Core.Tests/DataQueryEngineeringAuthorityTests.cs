using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.HistoricalQueries;
using Scada.Core.Tags;
using Scada.Engineering.Contracts;
using Scada.Engineering.DataQueries;
using Scada.Engineering.ImportExport;

namespace Scada.Core.Tests;

public sealed class DataQueryEngineeringAuthorityTests
{
    private static readonly DateTimeOffset From = new(2026, 9, 29, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset To = From.AddHours(1);
    private static readonly DateTimeOffset Target = From.AddMinutes(20);

    [Fact]
    public void Registry_ValidatesFrozenRetrievalContractAndPreservesStableIdentity()
    {
        var registry = new InMemoryDataQueryEngineeringRegistry();
        var missingTarget = Definition("point", new HistorianRetrievalEngineeringDto(HistorianRetrievalMode.Exact));

        var ex = Assert.Throws<ArgumentException>(() => registry.Upsert(missingTarget));
        Assert.Contains("requires TargetUtc", ex.Message, StringComparison.OrdinalIgnoreCase);

        var first = missingTarget with
        {
            HistorianRetrieval = new HistorianRetrievalEngineeringDto(
                HistorianRetrievalMode.Exact,
                TargetUtc: Target)
        };
        registry.Upsert(first);
        var saved = Assert.IsType<DataQueryEngineeringDto>(registry.FindByKey("point"));
        Assert.NotNull(saved.Id);

        registry.Upsert(first with { Name = "Point query renamed" });
        var updated = Assert.IsType<DataQueryEngineeringDto>(registry.FindByKey("point"));
        Assert.Equal(saved.Id, updated.Id);
        Assert.Equal("Point query renamed", updated.Name);
    }

    [Fact]
    public void ExchangeDecorator_RoundTripsDataQueryAndAlarmViewThroughPreviewApply()
    {
        var sourceQueries = new InMemoryDataQueryEngineeringRegistry();
        var sourceViews = new InMemoryAlarmViewEngineeringRegistry();
        sourceQueries.Upsert(Definition(
            "raw-history",
            new HistorianRetrievalEngineeringDto(HistorianRetrievalMode.Raw)));
        sourceViews.Upsert(new AlarmViewEngineeringDto(
            null,
            "critical-active",
            "Critical active alarms",
            new AlarmViewFilterEngineeringDto(
                Priorities: [AlarmPriority.Critical],
                Active: AlarmViewMatchState.Yes)));

        using var sourceAlarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        IEngineeringExchangeService source = new EngineeringExchangeService(
            new InMemoryTagRegistry(),
            sourceAlarms);
        source = new DataQueryEngineeringExchangeDecorator(source, sourceQueries, sourceViews);

        var json = source.ExportJson(indented: false);
        var package = source.ParseJson(json);
        Assert.Single(package.DataQueries!);
        Assert.Single(package.AlarmViews!);

        var targetQueries = new InMemoryDataQueryEngineeringRegistry();
        var targetViews = new InMemoryAlarmViewEngineeringRegistry();
        using var targetAlarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        IEngineeringExchangeService target = new EngineeringExchangeService(
            new InMemoryTagRegistry(),
            targetAlarms);
        target = new DataQueryEngineeringExchangeDecorator(target, targetQueries, targetViews);

        var preview = target.Preview(package, ImportMode.CreateAndUpdate);
        Assert.True(preview.CanApply);
        Assert.Contains(preview.Items, x =>
            x.EntityKind == ImportEntityKind.DataQuery && x.Operation == ImportOperation.Create);
        Assert.Contains(preview.Items, x =>
            x.EntityKind == ImportEntityKind.AlarmView && x.Operation == ImportOperation.Create);

        var result = target.Apply(package, ImportMode.CreateAndUpdate);
        Assert.DoesNotContain(result.Issues, x => x.IsError);
        Assert.Single(targetQueries.Snapshot());
        Assert.Single(targetViews.Snapshot());
        Assert.Equal(sourceQueries.Snapshot().Single().Id, targetQueries.Snapshot().Single().Id);
        Assert.Equal(sourceViews.Snapshot().Single().Id, targetViews.Snapshot().Single().Id);
    }

    [Fact]
    public async Task Execution_BindsHistorianTargetUtcAndUsesProtectedRetrievalProvider()
    {
        var tagId = Guid.NewGuid();
        var definition = new DataQueryEngineeringDto(
            null,
            "point-before",
            "Point before",
            DataQueryExecutionService.HistoricalProviderKey,
            new HistoricalQueryRequest(
                HistoricalDatasets.HistorianSamples,
                HistoricalTimeRange.Absolute(From, To),
                Filters:
                [
                    new HistoricalFilter(
                        "tag.id",
                        HistoricalFilterOperator.Eq,
                        [HistoricalQueryValue.FromGuid(tagId)])
                ]),
            Parameters:
            [
                new DataQueryParameterEngineeringDto(
                    "point",
                    "Point",
                    DataQueryParameterType.DateTime)
            ],
            ParameterBindings:
            [
                new DataQueryParameterBindingEngineeringDto(
                    "point",
                    DataQueryParameterTarget.HistorianTargetUtc)
            ],
            HistorianRetrieval: new HistorianRetrievalEngineeringDto(
                HistorianRetrievalMode.AtOrBefore));

        var registry = new InMemoryDataQueryEngineeringRegistry();
        registry.Upsert(definition);
        var provider = new RecordingRetrievalProvider(tagId);
        var service = new DataQueryExecutionService(
            registry,
            new NeverRawHistoricalQueryService(),
            new AllowAuthorizer(),
            [provider],
            () => To);

        var response = await service.ExecuteAsync(
            "point-before",
            new DataQueryExecutionRequest(
                new Dictionary<string, DataQueryParameterValue>
                {
                    ["point"] = new(DataQueryParameterType.DateTime, Target.ToString("O"))
                }));

        Assert.Equal(Target, provider.TargetUtc);
        Assert.Equal(HistorianRetrievalMode.AtOrBefore, response.RetrievalMode);
        var row = Assert.Single(response.Rows);
        Assert.Equal("measured", row.Provenance!.Kind);
        Assert.Equal(tagId.ToString("D"), row.Data.Cells["tag.id"].Value);
    }

    [Fact]
    public void AlarmViewMatcher_AppliesTypedStateAndFactsWithoutCommandAuthority()
    {
        var alarmId = Guid.NewGuid();
        var tagId = Guid.NewGuid();
        var filter = new AlarmViewFilterEngineeringDto(
            Areas: ["Plant.A"],
            Priorities: [AlarmPriority.Critical],
            Types: [AlarmType.HighHigh],
            AlarmClasses: ["process"],
            AlarmIds: [alarmId],
            TagIds: [tagId],
            Active: AlarmViewMatchState.Yes,
            Acknowledged: AlarmViewMatchState.No,
            Shelved: AlarmViewMatchState.No,
            Search: "pressure");

        var facts = new AlarmViewFacts(
            alarmId,
            tagId,
            AlarmPriority.Critical,
            AlarmType.HighHigh,
            AlarmState.Active,
            "Plant.A",
            "process",
            Message: "High discharge pressure");

        Assert.True(AlarmViewMatcher.Matches(filter, facts));
        Assert.False(AlarmViewMatcher.Matches(filter, facts with { State = AlarmState.Shelved }));
    }

    private static DataQueryEngineeringDto Definition(
        string key,
        HistorianRetrievalEngineeringDto retrieval) =>
        new(
            null,
            key,
            key,
            DataQueryExecutionService.HistoricalProviderKey,
            new HistoricalQueryRequest(
                HistoricalDatasets.HistorianSamples,
                HistoricalTimeRange.Absolute(From, To)),
            HistorianRetrieval: retrieval);

    private sealed class AllowAuthorizer : IHistoricalQueryAuthorizer
    {
        public ValueTask<HistoricalAuthorizationDecision> AuthorizeAsync(
            string dataset,
            CancellationToken cancellationToken = default)
        {
            Assert.Equal(HistoricalDatasets.HistorianSamples, dataset);
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(HistoricalAuthorizationDecision.Allow());
        }
    }

    private sealed class NeverRawHistoricalQueryService : IHistoricalQueryService
    {
        public Task<HistoricalQueryResponse> QueryAsync(
            HistoricalQueryRequest request,
            CancellationToken cancellationToken = default) =>
            throw new Xunit.Sdk.XunitException("Raw Historical Query service must not execute for point retrieval.");
    }

    private sealed class RecordingRetrievalProvider(Guid tagId) : IHistoricalRetrievalProvider
    {
        public string Dataset => HistoricalDatasets.HistorianSamples;
        public DateTimeOffset? TargetUtc { get; private set; }

        public Task<HistoricalRetrievalResult> QueryAtOrBeforeAsync(
            HistoricalQueryExecution query,
            DateTimeOffset targetUtc,
            CancellationToken cancellationToken = default)
        {
            TargetUtc = targetUtc;
            return Task.FromResult(new HistoricalRetrievalResult(
                query.Range,
                [
                    new HistoricalRetrievalPoint(
                        tagId,
                        "Plant.Tag",
                        targetUtc.AddSeconds(-1),
                        HistoricalQueryValue.FromDouble(42d),
                        TagQuality.Good,
                        TagDataType.Double,
                        new HistoricalRetrievalProvenance(
                            HistoricalRetrievalProvenanceKind.Measured,
                            [targetUtc.AddSeconds(-1)]))
                ]));
        }

        public Task<HistoricalRetrievalResult> QueryLastAsync(HistoricalQueryExecution query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<HistoricalRetrievalResult> QueryAtOrAfterAsync(HistoricalQueryExecution query, DateTimeOffset targetUtc, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<HistoricalRetrievalResult> QueryExactAsync(HistoricalQueryExecution query, DateTimeOffset targetUtc, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<HistoricalRetrievalResult> QueryInterpolatedAsync(HistoricalQueryExecution query, DateTimeOffset targetUtc, int maximumGapMilliseconds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<HistoricalRetrievalResult> QuerySampledFixedStepAsync(HistoricalQueryExecution query, int stepMilliseconds, int maximumGapMilliseconds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<HistoricalRetrievalResult> QueryAggregateAsync(HistoricalQueryExecution query, int bucketMilliseconds, HistoricalAggregateOperation operation, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
