using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Engineering.Assets;
using Scada.Engineering.Commands;
using Scada.Engineering.Contracts;
using Scada.Engineering.DataSources;
using Scada.Engineering.Gateways;
using Scada.Engineering.ImportExport;
using Scada.Engineering.ProjectPackages;
using Scada.Engineering.Security;
using Scada.Engineering.Views;

namespace Scada.Core.Tests;

public sealed class GatewayProjectPackageTests
{
    [Fact]
    public void ProjectPackage_RoundTripsAndRestoresGatewayRoutes()
    {
        var sourceTags = new InMemoryTagRegistry();
        var source = TagDefinition.Create("Source", "Plant.Source", TagDataType.Double);
        var destination = TagDefinition.Create("Destination", "Plant.Destination", TagDataType.Double);
        sourceTags.Register(source);
        sourceTags.Register(destination);
        using var sourceAlarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var sourceGateways = new InMemoryGatewayEngineeringRegistry();
        var routeId = Guid.NewGuid();
        sourceGateways.Upsert(new GatewayRouteEngineeringDto(
            routeId,
            "plant.gateway",
            "Plant gateway",
            source.Id,
            source.Path,
            destination.Id,
            destination.Path,
            MinimumIntervalMilliseconds: 250));
        var sourceExchange = CreateExchange(sourceTags, sourceAlarms, sourceGateways);
        var sourcePackages = new ProjectPackageService(sourceExchange);

        var bytes = sourcePackages.Export("plant-a", "Plant A");
        var inspection = sourcePackages.Inspect(bytes);

        Assert.Equal(EngineeringExchangeService.CurrentSchemaVersion, inspection.Manifest.EngineeringSchemaVersion);
        var inspectedRoute = Assert.Single(inspection.Engineering.Gateways!);
        Assert.Equal(routeId, inspectedRoute.Id);
        Assert.Equal(source.Id, inspectedRoute.SourceTagId);
        Assert.Equal(destination.Id, inspectedRoute.DestinationTagId);

        var targetTags = new InMemoryTagRegistry();
        using var targetAlarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var targetGateways = new InMemoryGatewayEngineeringRegistry();
        var targetPackages = new ProjectPackageService(CreateExchange(targetTags, targetAlarms, targetGateways));

        var preview = targetPackages.Preview(bytes, ImportMode.CreateAndUpdate);
        var result = targetPackages.Apply(bytes, ImportMode.CreateAndUpdate);

        Assert.True(preview.CanApply);
        Assert.Empty(result.Issues);
        var restored = Assert.Single(targetGateways.Snapshot());
        Assert.Equal(routeId, restored.Id);
        Assert.Equal("plant.gateway", restored.Key);
    }

    private static EngineeringExchangeService CreateExchange(
        ITagRegistry tags,
        IAlarmEngine alarms,
        IGatewayEngineeringRegistry gateways) =>
        new(
            tags,
            alarms,
            new InMemoryDataSourceEngineeringRegistry(),
            new InMemoryEngineeringAssetRegistry(),
            new InMemoryEngineeringViewRegistry(),
            new InMemorySecurityPolicyEngineeringRegistry(),
            new InMemoryCommandEngineeringRegistry(),
            gateways);
}