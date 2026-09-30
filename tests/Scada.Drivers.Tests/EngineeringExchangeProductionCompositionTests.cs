using Microsoft.Extensions.DependencyInjection;
using Scada.Api.Runtime;
using Scada.Core.Alarms;
using Scada.Core.HistoricalQueries;
using Scada.Engineering.Contracts;
using Scada.Engineering.DataQueries;
using Scada.Engineering.Historian;
using Scada.Engineering.ImportExport;

namespace Scada.Drivers.Tests;

public sealed class EngineeringExchangeProductionCompositionTests
{
    [Fact]
    public void ResolvedProductionExchange_ExportsPreviewsAndAppliesCanonicalPortableAuthorities()
    {
        using var sourceWorkspace = new EngineeringWorkspace(seedDemo: false);
        using var sourceServices = BuildProductionExchangeServices(sourceWorkspace);
        var source = sourceServices.GetRequiredService<IEngineeringExchangeService>();

        var profileId = Guid.NewGuid();
        var queryId = Guid.NewGuid();
        var alarmViewId = Guid.NewGuid();

        sourceWorkspace.HistorianCaptureProfiles.Upsert(new HistorianCaptureProfileEngineeringDto(
            profileId,
            "capture.production.fast",
            "Production fast capture",
            HistorianCaptureStrategy.Periodic,
            PeriodMilliseconds: 1_000));
        sourceWorkspace.DataQueries.Upsert(new DataQueryEngineeringDto(
            queryId,
            "query.production.history",
            "Production history",
            DataQueryExecutionService.HistoricalProviderKey,
            new HistoricalQueryRequest(
                HistoricalDatasets.HistorianSamples,
                HistoricalTimeRange.Relative(3_600)),
            HistorianRetrieval: new HistorianRetrievalEngineeringDto(
                HistorianRetrievalMode.Raw)));
        sourceWorkspace.AlarmViews.Upsert(new AlarmViewEngineeringDto(
            alarmViewId,
            "alarmview.production.active",
            "Production active alarms",
            new AlarmViewFilterEngineeringDto(
                Priorities: [AlarmPriority.High, AlarmPriority.Critical],
                Active: AlarmViewMatchState.Yes)));

        var package = source.ExportPackage();

        Assert.Equal(profileId, Assert.Single(package.HistorianCaptureProfiles!).Id);
        Assert.Equal(queryId, Assert.Single(package.DataQueries!).Id);
        Assert.Equal(alarmViewId, Assert.Single(package.AlarmViews!).Id);

        using var targetWorkspace = new EngineeringWorkspace(seedDemo: false);
        using var targetServices = BuildProductionExchangeServices(targetWorkspace);
        var target = targetServices.GetRequiredService<IEngineeringExchangeService>();

        var preview = target.Preview(package, ImportMode.CreateAndUpdate);

        Assert.True(preview.CanApply);
        Assert.Contains(preview.Items, item =>
            item.EntityKind == ImportEntityKind.HistorianCaptureProfile &&
            item.Operation == ImportOperation.Create);
        Assert.Contains(preview.Items, item =>
            item.EntityKind == ImportEntityKind.DataQuery &&
            item.Operation == ImportOperation.Create);
        Assert.Contains(preview.Items, item =>
            item.EntityKind == ImportEntityKind.AlarmView &&
            item.Operation == ImportOperation.Create);

        var result = target.Apply(package, ImportMode.CreateAndUpdate);

        Assert.DoesNotContain(result.Issues, issue => issue.IsError);
        Assert.Equal(profileId, Assert.Single(targetWorkspace.HistorianCaptureProfiles.Snapshot()).Id);
        Assert.Equal(queryId, Assert.Single(targetWorkspace.DataQueries.Snapshot()).Id);
        Assert.Equal(alarmViewId, Assert.Single(targetWorkspace.AlarmViews.Snapshot()).Id);

        var roundTrip = target.ExportPackage();
        Assert.Equal(profileId, Assert.Single(roundTrip.HistorianCaptureProfiles!).Id);
        Assert.Equal(queryId, Assert.Single(roundTrip.DataQueries!).Id);
        Assert.Equal(alarmViewId, Assert.Single(roundTrip.AlarmViews!).Id);
    }

    private static ServiceProvider BuildProductionExchangeServices(
        EngineeringWorkspace workspace)
    {
        var services = new ServiceCollection();
        services.AddSingleton(new EngineeringExchangeService(
            workspace.Tags,
            workspace.Alarms,
            workspace.DataSources,
            workspace.Assets,
            workspace.Views));
        services.AddSingleton<IHistorianCaptureProfileEngineeringRegistry>(
            workspace.HistorianCaptureProfiles);
        services.AddSingleton<IDataQueryEngineeringRegistry>(
            workspace.DataQueries);
        services.AddSingleton<IAlarmViewEngineeringRegistry>(
            workspace.AlarmViews);
        services.AddSingleton<IEngineeringExchangeService>(
            EngineeringExchangeProductionComposition.Create);
        return services.BuildServiceProvider();
    }
}
