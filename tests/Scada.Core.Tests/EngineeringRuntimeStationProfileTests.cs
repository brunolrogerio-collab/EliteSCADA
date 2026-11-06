using System.Text.Json;
using System.Text.Json.Nodes;
using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;

namespace Scada.Core.Tests;

public sealed class EngineeringRuntimeStationProfileTests
{
    [Fact]
    public void WorkingApply_ExportAndImport_PreserveStationWindowsAndNeighboringPresentation()
    {
        using var sourceAlarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var source = new EngineeringExchangeService(new InMemoryTagRegistry(), sourceAlarms);
        var package = StationPackage(source);
        Assert.True(source.Preview(package, ImportMode.CreateAndUpdate).CanApply);
        Assert.Empty(source.Apply(package, ImportMode.CreateAndUpdate).Issues);

        using var document = JsonDocument.Parse(source.ExportJson());
        var presentationJson = document.RootElement.GetProperty("runtimePresentation");
        var windows = presentationJson.GetProperty("stationProfiles")[0].GetProperty("windows");
        Assert.Equal(2, windows.GetArrayLength());
        Assert.Equal("overview", windows[0].GetProperty("screenKey").GetString());
        Assert.Equal("navigation", windows[0].GetProperty("mode").GetString());
        Assert.Equal("pumps", windows[1].GetProperty("screenKey").GetString());
        Assert.Equal("fixed", windows[1].GetProperty("mode").GetString());
        Assert.Equal("portrait", presentationJson.GetProperty("mobileOrientation").GetString());
        Assert.Equal("overview", presentationJson.GetProperty("mobileScreens").GetProperty("phone").GetString());
        Assert.Equal(64, presentationJson.GetProperty("header").GetProperty("height").GetInt32());

        using var targetAlarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var target = new EngineeringExchangeService(new InMemoryTagRegistry(), targetAlarms);
        var imported = target.ParseJson(source.ExportJson(indented: false));
        Assert.True(target.Preview(imported, ImportMode.CreateAndUpdate).CanApply);
        Assert.Empty(target.Apply(imported, ImportMode.CreateAndUpdate).Issues);
        var roundTrip = target.ExportPackage().RuntimePresentation!;
        var profile = Assert.Single(roundTrip.StationProfiles!);
        Assert.Equal("control-room", profile.Id);
        Assert.Equal("Sala de controle", profile.Name);
        Assert.Collection(profile.Windows,
            first => { Assert.Equal("overview", first.ScreenKey); Assert.Equal("navigation", first.Mode); },
            second => { Assert.Equal("pumps", second.ScreenKey); Assert.Equal("fixed", second.Mode); });
        Assert.Equal("portrait", roundTrip.MobileOrientation);
        Assert.Equal("overview", roundTrip.MobileScreens!["phone"]);
        Assert.Equal(64, roundTrip.Header!.Height);
    }

    [Fact]
    public void LegacyPresentationWithoutStationProfiles_DefaultsWithoutErasingAdjacentFields()
    {
        using var alarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var exchange = new EngineeringExchangeService(new InMemoryTagRegistry(), alarms);
        var root = JsonNode.Parse(exchange.ExportJson())!.AsObject();
        root["runtimePresentation"] = new JsonObject
        {
            ["version"] = 1,
            ["historicalPlaybackEnabled"] = true,
            ["mobileOrientation"] = "portrait",
            ["mobileScreens"] = new JsonObject { ["phone"] = "overview" },
            ["header"] = new JsonObject { ["height"] = 72 }
        };
        var package = exchange.ParseJson(root.ToJsonString());
        Assert.Null(package.RuntimePresentation!.StationProfiles);
        Assert.True(package.RuntimePresentation.HistoricalPlaybackEnabled);
        Assert.Equal("portrait", package.RuntimePresentation.MobileOrientation);
        Assert.Equal("overview", package.RuntimePresentation.MobileScreens!["phone"]);
        Assert.Equal(72, package.RuntimePresentation.Header!.Height);
        // The default case must also be safe when the entire presentation is absent.
        root.Remove("runtimePresentation");
        var legacy = exchange.ParseJson(root.ToJsonString());
        Assert.Null(legacy.RuntimePresentation!.StationProfiles);
        Assert.Equal("landscape", legacy.RuntimePresentation.MobileOrientation);
        Assert.Equal(1, legacy.RuntimePresentation.Version);
    }

    [Theory]
    [InlineData("empty-name")]
    [InlineData("duplicate-id")]
    [InlineData("missing-screen")]
    [InlineData("bad-mode")]
    [InlineData("zero-windows")]
    [InlineData("five-windows")]
    public void InvalidStationProfile_FailsPreviewAndApplyWithDiagnostic(string violation)
    {
        using var alarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var exchange = new EngineeringExchangeService(new InMemoryTagRegistry(), alarms);
        var package = StationPackage(exchange);
        var station = Assert.Single(package.RuntimePresentation!.StationProfiles!);
        var windows = station.Windows.ToArray();
        var invalid = violation switch
        {
            "empty-name" => station with { Name = " " },
            "missing-screen" => station with { Windows = [windows[0], windows[1] with { ScreenKey = "missing" }] },
            "bad-mode" => station with { Windows = [windows[0], windows[1] with { Mode = "unsupported" }] },
            "zero-windows" => station with { Windows = [] },
            "five-windows" => station with { Windows = [windows[0], windows[1], windows[0], windows[1], windows[0]] },
            _ => station
        };
        var profiles = violation == "duplicate-id" ? new[] { station, station } : new[] { invalid };
        package = package with { RuntimePresentation = package.RuntimePresentation with { StationProfiles = profiles } };
        var preview = exchange.Preview(package, ImportMode.CreateAndUpdate);
        Assert.False(preview.CanApply);
        Assert.Contains(preview.Items.SelectMany(item => item.Issues),
            issue => issue.Code == "RUNTIME_STATION_PROFILE_INVALID" && issue.IsError);
        var applied = exchange.Apply(package, ImportMode.CreateAndUpdate);
        Assert.Contains(applied.Issues, issue => issue.Code == "RUNTIME_STATION_PROFILE_INVALID");
        Assert.Null(exchange.ExportPackage().RuntimePresentation!.StationProfiles);
    }

    [Fact]
    public void StationProfilesWithWrongJsonType_IsRejectedRatherThanSilentlySkipped()
    {
        using var alarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var exchange = new EngineeringExchangeService(new InMemoryTagRegistry(), alarms);
        var root = JsonNode.Parse(exchange.ExportJson())!.AsObject();
        root["runtimePresentation"]!["stationProfiles"] = "not-an-array";
        var error = Assert.Throws<JsonException>(() => exchange.ParseJson(root.ToJsonString()));
        Assert.Contains("stationProfiles", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static EngineeringPackage StationPackage(EngineeringExchangeService exchange) =>
        exchange.ExportPackage() with
        {
            Screens =
            [
                new ScreenEngineeringDto(Guid.NewGuid(), "overview", "Visão geral"),
                new ScreenEngineeringDto(Guid.NewGuid(), "pumps", "Bombas")
            ],
            RuntimePresentation = new RuntimePresentationEngineeringDto(
                HistoricalPlaybackEnabled: true,
                MobileOrientation: "portrait",
                Header: new RuntimeHeaderEngineeringDto(Height: 64),
                MobileScreens: new Dictionary<string, string> { ["phone"] = "overview" },
                StationProfiles:
                [
                    new RuntimeStationProfileEngineeringDto(
                        "control-room", "Sala de controle",
                        [
                            new RuntimeStationWindowEngineeringDto("overview", "navigation"),
                            new RuntimeStationWindowEngineeringDto("pumps", "fixed")
                        ])
                ])
        };
}
