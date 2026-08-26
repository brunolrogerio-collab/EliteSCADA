using Scada.Core.Abstractions;
using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Engineering.Assets;
using Scada.Engineering.Contracts;
using Scada.Engineering.DataSources;
using Scada.Engineering.ImportExport;
using Scada.Engineering.Views;

namespace Scada.Core.Tests;

public sealed class EngineeringViewExchangeTests
{
    [Fact]
    public void CurrentSchema_RoundTripsScreenAndPopup()
    {
        var tags = new InMemoryTagRegistry();
        var bus = new InMemoryScadaEventBus();
        using var alarms = new InMemoryAlarmEngine(bus);
        var dataSources = new InMemoryDataSourceEngineeringRegistry();
        var assets = new InMemoryEngineeringAssetRegistry();
        var views = new InMemoryEngineeringViewRegistry();

        assets.UpsertTemplate(new EquipmentTemplateEngineeringDto(null, "pump.standard", "Standard Pump"));
        views.UpsertScreen(new ScreenEngineeringDto(
            null,
            "plant.overview",
            "Plant Overview",
            "/plant",
            Elements: new[]
            {
                new VisualElementEngineeringDto(
                    "pressure",
                    "value",
                    Bindings: new[] { new EngineeringBindingDto("value", EngineeringBindingKind.Tag, "Plant.Pressure", "read") },
                    Properties: new() { ["label"] = "Pressure" },
                    Context: new() { ["area"] = "Plant" })
            },
            Properties: new() { ["canvasWidth"] = "1366" }));
        views.UpsertPopup(new PopupEngineeringDto(
            null,
            "popup.pump.standard",
            "Standard Pump Popup",
            "pump.standard",
            Elements: new[]
            {
                new VisualElementEngineeringDto(
                    "current",
                    "value",
                    Bindings: new[] { new EngineeringBindingDto("value", EngineeringBindingKind.Tag, "{equipmentPath}.Current", "read") })
            },
            Properties: new() { ["width"] = "640" },
            Context: new() { ["role"] = "equipment-details" }));

        var service = new EngineeringExchangeService(tags, alarms, dataSources, assets, views);
        var package = service.ParseJson(service.ExportJson());

        Assert.Equal(EngineeringExchangeService.CurrentSchemaVersion, package.SchemaVersion);
        var screen = Assert.Single(package.Screens!);
        var popup = Assert.Single(package.Popups!);
        Assert.Equal("/plant", screen.Route);
        Assert.Equal("Pressure", screen.Elements!.Single().Properties!["label"]);
        Assert.Equal("pump.standard", popup.TemplateKey);
        Assert.Equal("{equipmentPath}.Current", popup.Elements!.Single().Bindings!.Single().Target);
        Assert.Equal("equipment-details", popup.Context!["role"]);
    }

    [Fact]
    public void Preview_RejectsScreenWithMissingDynamoAndEquipment()
    {
        var service = CreateService();
        var package = new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            Array.Empty<TagEngineeringDto>(),
            Array.Empty<AlarmEngineeringDto>(),
            Screens: new[]
            {
                new ScreenEngineeringDto(
                    null,
                    "plant.overview",
                    "Plant Overview",
                    "/plant",
                    Elements: new[]
                    {
                        new VisualElementEngineeringDto(
                            "pump01",
                            "dynamo",
                            DynamoKey: "dynamo.missing",
                            EquipmentPath: "Plant.P99")
                    })
            });

        var preview = service.Preview(package, ImportMode.CreateAndUpdate);
        var issues = preview.Items.SelectMany(x => x.Issues).ToArray();

        Assert.False(preview.CanApply);
        Assert.Contains(issues, x => x.Code == "VISUAL_DYNAMO_NOT_FOUND");
        Assert.Contains(issues, x => x.Code == "VISUAL_EQUIPMENT_NOT_FOUND");
    }

    [Fact]
    public void Preview_RejectsScreenBindingWhenTagDoesNotExist()
    {
        var service = CreateService();
        var package = new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            Array.Empty<TagEngineeringDto>(),
            Array.Empty<AlarmEngineeringDto>(),
            Screens: new[]
            {
                new ScreenEngineeringDto(
                    null,
                    "plant.overview",
                    "Plant Overview",
                    "/plant",
                    Elements: new[]
                    {
                        new VisualElementEngineeringDto(
                            "pressure",
                            "value",
                            Bindings: new[] { new EngineeringBindingDto("value", EngineeringBindingKind.Tag, "Plant.Pressure", "read") })
                    })
            });

        var preview = service.Preview(package, ImportMode.CreateAndUpdate);

        Assert.False(preview.CanApply);
        Assert.Contains(preview.Items.SelectMany(x => x.Issues), x => x.Code == "BINDING_TAG_NOT_FOUND");
    }

    [Fact]
    public void Preview_AcceptsScreenWhenReferencesAreInSamePackage()
    {
        var service = CreateService();
        var package = new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            new[] { new TagEngineeringDto(null, "Running", "Plant.P01.Running", TagDataType.Boolean) },
            Array.Empty<AlarmEngineeringDto>(),
            Templates: new[] { new EquipmentTemplateEngineeringDto(null, "pump.standard", "Standard Pump") },
            Equipment: new[] { new EquipmentEngineeringDto(null, "Plant.P01", "Pump P01", "pump.standard") },
            Dynamos: new[] { new DynamoEngineeringDto(null, "dynamo.pump.standard", "Pump Dynamo", "pump.standard") },
            Screens: new[]
            {
                new ScreenEngineeringDto(
                    null,
                    "plant.overview",
                    "Plant Overview",
                    "/plant",
                    Elements: new[]
                    {
                        new VisualElementEngineeringDto(
                            "pump01",
                            "dynamo",
                            DynamoKey: "dynamo.pump.standard",
                            EquipmentPath: "Plant.P01",
                            Bindings: new[] { new EngineeringBindingDto("running", EngineeringBindingKind.Tag, "Plant.P01.Running", "read") })
                    })
            });

        var preview = service.Preview(package, ImportMode.CreateAndUpdate);

        Assert.True(preview.CanApply);
        Assert.Equal(5, preview.CreateCount);
    }

    [Fact]
    public void Preview_AcceptsPopupPlaceholderBindingsWhenTemplateExists()
    {
        var service = CreateService();
        var package = new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            Array.Empty<TagEngineeringDto>(),
            Array.Empty<AlarmEngineeringDto>(),
            Templates: new[] { new EquipmentTemplateEngineeringDto(null, "pump.standard", "Standard Pump") },
            Popups: new[]
            {
                new PopupEngineeringDto(
                    null,
                    "popup.pump.standard",
                    "Standard Pump Popup",
                    "pump.standard",
                    Elements: new[]
                    {
                        new VisualElementEngineeringDto(
                            "current",
                            "value",
                            EquipmentPath: "{equipmentPath}",
                            Bindings: new[] { new EngineeringBindingDto("value", EngineeringBindingKind.Tag, "{equipmentPath}.Current", "read") })
                    })
            });

        var preview = service.Preview(package, ImportMode.CreateAndUpdate);

        Assert.True(preview.CanApply);
        Assert.Equal(2, preview.CreateCount);
    }

    private static EngineeringExchangeService CreateService()
    {
        var tags = new InMemoryTagRegistry();
        var bus = new InMemoryScadaEventBus();
        var alarms = new InMemoryAlarmEngine(bus);
        return new EngineeringExchangeService(
            tags,
            alarms,
            new InMemoryDataSourceEngineeringRegistry(),
            new InMemoryEngineeringAssetRegistry(),
            new InMemoryEngineeringViewRegistry());
    }
}
