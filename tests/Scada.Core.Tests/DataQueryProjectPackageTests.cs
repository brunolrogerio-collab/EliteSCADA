using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.HistoricalQueries;
using Scada.Core.Tags;
using Scada.Engineering.Contracts;
using Scada.Engineering.DataQueries;
using Scada.Engineering.ImportExport;
using Scada.Engineering.ProjectPackages;

namespace Scada.Core.Tests;

public sealed class DataQueryProjectPackageTests
{
    [Fact]
    public void ProjectPackage_PreservesCanonicalDataQueryAndAlarmViewIdentity()
    {
        var queries = new InMemoryDataQueryEngineeringRegistry();
        var views = new InMemoryAlarmViewEngineeringRegistry();
        queries.Upsert(new DataQueryEngineeringDto(
            null,
            "package-query",
            "Package query",
            DataQueryExecutionService.HistoricalProviderKey,
            new HistoricalQueryRequest(
                HistoricalDatasets.HistorianSamples,
                HistoricalTimeRange.Relative(3600)),
            HistorianRetrieval: new HistorianRetrievalEngineeringDto(HistorianRetrievalMode.Raw)));
        views.Upsert(new AlarmViewEngineeringDto(
            null,
            "package-view",
            "Package view",
            new AlarmViewFilterEngineeringDto(
                Priorities: [AlarmPriority.High, AlarmPriority.Critical])));

        using var sourceAlarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        IEngineeringExchangeService sourceExchange = new EngineeringExchangeService(
            new InMemoryTagRegistry(),
            sourceAlarms);
        sourceExchange = new DataQueryEngineeringExchangeDecorator(sourceExchange, queries, views);
        var sourcePackages = new ProjectPackageService(sourceExchange);

        var expectedQueryId = queries.Snapshot().Single().Id;
        var expectedViewId = views.Snapshot().Single().Id;
        var bytes = sourcePackages.Export("plant-a", "Plant A");
        var inspection = sourcePackages.Inspect(bytes);
        Assert.Equal(expectedQueryId, inspection.Engineering.DataQueries!.Single().Id);
        Assert.Equal(expectedViewId, inspection.Engineering.AlarmViews!.Single().Id);

        var targetQueries = new InMemoryDataQueryEngineeringRegistry();
        var targetViews = new InMemoryAlarmViewEngineeringRegistry();
        using var targetAlarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        IEngineeringExchangeService targetExchange = new EngineeringExchangeService(
            new InMemoryTagRegistry(),
            targetAlarms);
        targetExchange = new DataQueryEngineeringExchangeDecorator(
            targetExchange,
            targetQueries,
            targetViews);
        var targetPackages = new ProjectPackageService(targetExchange);

        var preview = targetPackages.Preview(bytes, ImportMode.CreateAndUpdate);
        Assert.True(preview.CanApply);
        var applied = targetPackages.Apply(bytes, ImportMode.CreateAndUpdate);
        Assert.DoesNotContain(applied.Issues, x => x.IsError);
        Assert.Equal(expectedQueryId, targetQueries.Snapshot().Single().Id);
        Assert.Equal(expectedViewId, targetViews.Snapshot().Single().Id);
    }
}
