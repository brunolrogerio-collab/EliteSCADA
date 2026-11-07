using System.Text.Json;
using Scada.Api.Runtime;
using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;

namespace Scada.Drivers.Tests;

public sealed class RuntimeStationActiveProjectionTests
{
    [Fact]
    public void AuthenticatedRuntimeHmiProjection_PreservesTypedStationProfilesFromSavedJson()
    {
        using var alarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var exchange = new EngineeringExchangeService(new InMemoryTagRegistry(), alarms);
        var source = exchange.ExportPackage() with
        {
            Screens =
            [
                new ScreenEngineeringDto(Guid.NewGuid(), "overview", "Overview"),
                new ScreenEngineeringDto(Guid.NewGuid(), "pumps", "Pumps")
            ],
            RuntimePresentation = new RuntimePresentationEngineeringDto(
                MobileOrientation: "portrait",
                StationProfiles:
                [
                    new RuntimeStationProfileEngineeringDto("control", "Control room",
                    [
                        new RuntimeStationWindowEngineeringDto("overview", "navigation"),
                        new RuntimeStationWindowEngineeringDto("pumps", "fixed")
                    ])
                ])
        };
        Assert.True(exchange.Preview(source, ImportMode.CreateAndUpdate).CanApply);
        Assert.Empty(exchange.Apply(source, ImportMode.CreateAndUpdate).Issues);

        // The endpoint supplies JSON from IEngineeringProjectPersistenceService.LoadActiveAsync;
        // this is its exact authenticated HMI projection of that revision payload.
        using var document = JsonDocument.Parse(exchange.ExportJson(indented: false));
        var projected = JsonSerializer.SerializeToElement(
            RuntimeEngineeringPackageApi.ProjectHmiPackage(document.RootElement));
        var presentation = projected.GetProperty("runtimePresentation");
        var station = presentation.GetProperty("stationProfiles")[0];
        Assert.Equal("control", station.GetProperty("id").GetString());
        Assert.Equal("Control room", station.GetProperty("name").GetString());
        Assert.Equal(2, station.GetProperty("windows").GetArrayLength());
        Assert.Equal("overview", station.GetProperty("windows")[0].GetProperty("screenKey").GetString());
        Assert.Equal("navigation", station.GetProperty("windows")[0].GetProperty("mode").GetString());
        Assert.Equal("pumps", station.GetProperty("windows")[1].GetProperty("screenKey").GetString());
        Assert.Equal("fixed", station.GetProperty("windows")[1].GetProperty("mode").GetString());
        Assert.Equal("portrait", presentation.GetProperty("mobileOrientation").GetString());
    }
}
