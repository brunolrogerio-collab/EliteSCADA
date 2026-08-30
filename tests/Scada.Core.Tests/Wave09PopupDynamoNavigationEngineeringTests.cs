using System.Text.Json;
using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Engineering.Assets;
using Scada.Engineering.Contracts;
using Scada.Engineering.DataSources;
using Scada.Engineering.ImportExport;
using Scada.Engineering.ProjectPackages;
using Scada.Engineering.Views;

namespace Scada.Core.Tests;

public sealed class Wave09PopupDynamoNavigationEngineeringTests
{
    [Fact]
    public void PreviewApplyExport_RoundTripsDynamoCompositionAndNavigation()
    {
        var tags = new InMemoryTagRegistry();
        using var alarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var assets = new InMemoryEngineeringAssetRegistry();
        var views = new InMemoryEngineeringViewRegistry();
        var exchange = CreateExchange(tags, alarms, assets, views);
        var statusId = Guid.NewGuid();

        var package = new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            [new TagEngineeringDto(statusId, "Status", "Plant.P01.Status", TagDataType.Int16)],
            Array.Empty<AlarmEngineeringDto>(),
            Dynamos:
            [
                new DynamoEngineeringDto(
                    null,
                    "dynamo.pump",
                    "Pump",
                    Parameters:
                    [
                        new DynamoParameterDefinitionEngineeringDto("equipment", DynamoParameterKind.EquipmentPath, Required: true),
                        new DynamoParameterDefinitionEngineeringDto("running", DynamoParameterKind.TagReference, Required: true)
                    ],
                    Elements: [new VisualElementEngineeringDto("body", "core.rectangle")])
            ],
            Screens:
            [
                new ScreenEngineeringDto(
                    null,
                    "plant.overview",
                    "Overview",
                    "/overview",
                    Elements:
                    [
                        new VisualElementEngineeringDto(
                            "pump01",
                            "dynamo",
                            DynamoKey: "dynamo.pump",
                            DynamoParameters:
                            [
                                new DynamoParameterValueEngineeringDto(
                                    "equipment",
                                    DynamoParameterKind.EquipmentPath,
                                    JsonSerializer.SerializeToElement("Plant.P01")),
                                new DynamoParameterValueEngineeringDto(
                                    "running",
                                    DynamoParameterKind.TagReference,
                                    TagReference: new TagValueReference(
                                        statusId,
                                        new TagValueSelector(TagValueSelectorKind.Bit, 3)))
                            ],
                            Actions:
                            [
                                new VisualNavigationActionEngineeringDto(
                                    "click",
                                    VisualNavigationActionKind.OpenPopup,
                                    "popup.pump",
                                    new Dictionary<string, JsonElement>
                                    {
                                        ["equipmentPath"] = JsonSerializer.SerializeToElement("Plant.P01")
                                    })
                            ])
                    ])
            ],
            Popups:
            [
                new PopupEngineeringDto(
                    null,
                    "popup.pump",
                    "Pump Details",
                    Elements:
                    [
                        new VisualElementEngineeringDto(
                            "close",
                            "core.button",
                            Actions:
                            [new VisualNavigationActionEngineeringDto("click", VisualNavigationActionKind.ClosePopup)])
                    ])
            ]);

        var preview = exchange.Preview(package, ImportMode.CreateAndUpdate);
        Assert.True(preview.CanApply);

        var result = exchange.Apply(package, ImportMode.CreateAndUpdate);
        Assert.Equal(4, result.Created);

        var exported = exchange.ParseJson(exchange.ExportJson(indented: false));
        var dynamo = Assert.Single(exported.Dynamos!);
        var definitionElement = Assert.Single(dynamo.Elements!);
        Assert.NotNull(dynamo.Id);
        Assert.NotNull(definitionElement.Id);
        Assert.Equal(2, dynamo.Parameters!.Count);

        var screenElement = Assert.Single(Assert.Single(exported.Screens!).Elements!);
        Assert.NotNull(screenElement.Id);
        var running = screenElement.DynamoParameters!.Single(x => x.Key == "running");
        Assert.Equal(statusId, running.TagReference!.TagId);
        Assert.Equal(3, running.TagReference.Selector!.Index);
        var action = Assert.Single(screenElement.Actions!);
        Assert.Equal(VisualNavigationActionKind.OpenPopup, action.Kind);
        Assert.Equal("popup.pump", action.TargetKey);
        Assert.Equal("Plant.P01", action.Parameters!["equipmentPath"].GetString());
    }

    [Fact]
    public void DynamoRegistry_PreservesDefinitionAndChildIdentityAcrossUpdates()
    {
        var registry = new InMemoryEngineeringAssetRegistry();
        registry.UpsertDynamo(new DynamoEngineeringDto(
            null,
            "dynamo.valve",
            "Valve",
            Elements: [new VisualElementEngineeringDto("body", "core.rectangle")]));

        var first = registry.FindDynamoByKey("dynamo.valve")!;
        var childId = Assert.Single(first.Elements!).Id;
        Assert.NotNull(childId);

        registry.UpsertDynamo(new DynamoEngineeringDto(
            first.Id,
            "dynamo.valve.renamed",
            "Valve v2",
            Elements: [new VisualElementEngineeringDto("body", "core.ellipse")]));

        var second = registry.FindDynamo(first.Id!.Value)!;
        Assert.Equal(first.Id, second.Id);
        Assert.Equal(childId, Assert.Single(second.Elements!).Id);
        Assert.Equal("dynamo.valve.renamed", second.Key);
    }

    [Fact]
    public void Preview_RejectsMissingNavigationTargetAndInvalidDynamoArguments()
    {
        var tags = new InMemoryTagRegistry();
        using var alarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var assets = new InMemoryEngineeringAssetRegistry();
        var views = new InMemoryEngineeringViewRegistry();
        var exchange = CreateExchange(tags, alarms, assets, views);

        var package = new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            Array.Empty<TagEngineeringDto>(),
            Array.Empty<AlarmEngineeringDto>(),
            Dynamos:
            [
                new DynamoEngineeringDto(
                    null,
                    "dynamo.motor",
                    "Motor",
                    Parameters:
                    [new DynamoParameterDefinitionEngineeringDto("equipment", DynamoParameterKind.EquipmentPath, Required: true)],
                    Elements: [new VisualElementEngineeringDto("body", "core.rectangle")])
            ],
            Screens:
            [
                new ScreenEngineeringDto(
                    null,
                    "plant.overview",
                    "Overview",
                    "/overview",
                    Elements:
                    [
                        new VisualElementEngineeringDto(
                            "motor01",
                            "dynamo",
                            DynamoKey: "dynamo.motor",
                            DynamoParameters:
                            [new DynamoParameterValueEngineeringDto("unexpected", DynamoParameterKind.String, JsonSerializer.SerializeToElement("x"))],
                            Actions:
                            [new VisualNavigationActionEngineeringDto("click", VisualNavigationActionKind.OpenPopup, "popup.missing")])
                    ])
            ]);

        var preview = exchange.Preview(package, ImportMode.CreateAndUpdate);
        var issues = preview.Items.SelectMany(x => x.Issues).ToArray();

        Assert.False(preview.CanApply);
        Assert.Contains(issues, x => x.Code == "VISUAL_DYNAMO_PARAMETER_UNKNOWN");
        Assert.Contains(issues, x => x.Code == "VISUAL_DYNAMO_PARAMETER_REQUIRED");
        Assert.Contains(issues, x => x.Code == "VISUAL_ACTION_POPUP_NOT_FOUND");
    }

    [Fact]
    public void RuntimeComposer_UsesStableCompositeIdentityAndCanonicalDefaults()
    {
        var definitionId = Guid.NewGuid();
        var elementId = Guid.NewGuid();
        var instanceId = Guid.NewGuid();
        var definition = new DynamoEngineeringDto(
            definitionId,
            "dynamo.level",
            "Level",
            Parameters:
            [
                new DynamoParameterDefinitionEngineeringDto(
                    "caption",
                    DynamoParameterKind.String,
                    DefaultValue: JsonSerializer.SerializeToElement("Tank"))
            ],
            Elements: [new VisualElementEngineeringDto("body", "core.rectangle", Id: elementId)]);
        var instance = new VisualElementEngineeringDto(
            "level01",
            "dynamo",
            DynamoKey: definition.Key,
            Id: instanceId);

        var composition = DynamoRuntimeComposer.Compose(instance, definition);

        Assert.Equal(instanceId, composition.InstanceId);
        Assert.Equal(definitionId, composition.DefinitionId);
        Assert.Equal("Tank", composition.Parameters["caption"].Value!.Value.GetString());
        Assert.Same(definition.Elements, composition.Elements);
        Assert.Equal($"{instanceId:D}/{elementId:D}", DynamoRuntimeComposer.RuntimeElementIdentity(instanceId, elementId));
    }

    [Fact]
    public void ProjectPackage_RoundTripsWave09CompositionWithoutRendererState()
    {
        var tags = new InMemoryTagRegistry();
        using var alarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var assets = new InMemoryEngineeringAssetRegistry();
        var views = new InMemoryEngineeringViewRegistry();
        assets.UpsertDynamo(new DynamoEngineeringDto(
            null,
            "dynamo.simple",
            "Simple",
            Parameters: [new DynamoParameterDefinitionEngineeringDto("caption", DynamoParameterKind.String)],
            Elements: [new VisualElementEngineeringDto("label", "core.text")]));
        views.UpsertScreen(new ScreenEngineeringDto(
            null,
            "screen.home",
            "Home",
            "/home",
            Elements:
            [
                new VisualElementEngineeringDto(
                    "instance",
                    "dynamo",
                    DynamoKey: "dynamo.simple",
                    DynamoParameters:
                    [new DynamoParameterValueEngineeringDto("caption", DynamoParameterKind.String, JsonSerializer.SerializeToElement("Hello"))])
            ]));

        var exchange = CreateExchange(tags, alarms, assets, views);
        var packageService = new ProjectPackageService(exchange);
        var inspection = packageService.Inspect(packageService.Export("wave09", "Wave 09"));

        var dynamo = Assert.Single(inspection.Engineering.Dynamos!);
        Assert.NotNull(Assert.Single(dynamo.Elements!).Id);
        var instance = Assert.Single(Assert.Single(inspection.Engineering.Screens!).Elements!);
        Assert.Equal("Hello", Assert.Single(instance.DynamoParameters!).Value!.Value.GetString());
    }

    private static EngineeringExchangeService CreateExchange(
        InMemoryTagRegistry tags,
        InMemoryAlarmEngine alarms,
        InMemoryEngineeringAssetRegistry assets,
        InMemoryEngineeringViewRegistry views) =>
        new(
            tags,
            alarms,
            new InMemoryDataSourceEngineeringRegistry(),
            assets,
            views);
}
