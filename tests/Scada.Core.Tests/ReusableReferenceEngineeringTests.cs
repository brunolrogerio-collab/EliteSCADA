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

public sealed class ReusableReferenceEngineeringTests
{
    [Fact]
    public void LegacyAliasReferences_NormalizeToStableIds_OnApplyAndExport()
    {
        using var harness = CreateHarness();
        var templateId = Guid.NewGuid();
        var equipmentId = Guid.NewGuid();
        var dynamoId = Guid.NewGuid();
        var screenId = Guid.NewGuid();
        var popupId = Guid.NewGuid();

        var package = Package(
            templates:
            [
                new EquipmentTemplateEngineeringDto(templateId, "pump.standard", "Pump Template")
            ],
            equipment:
            [
                new EquipmentEngineeringDto(
                    equipmentId,
                    "Plant.P01",
                    "Pump P01",
                    TemplateKey: "pump.standard")
            ],
            dynamos:
            [
                new DynamoEngineeringDto(
                    dynamoId,
                    "dynamo.pump",
                    "Pump Dynamo",
                    TemplateKey: "pump.standard")
            ],
            screens:
            [
                new ScreenEngineeringDto(
                    screenId,
                    "screen.main",
                    "Main",
                    Elements:
                    [
                        new VisualElementEngineeringDto(
                            "pump01",
                            "core.group",
                            DynamoKey: "dynamo.pump",
                            EquipmentPath: "Plant.P01",
                            Id: Guid.NewGuid())
                    ])
            ],
            popups:
            [
                new PopupEngineeringDto(
                    popupId,
                    "popup.pump",
                    "Pump",
                    TemplateKey: "pump.standard",
                    Elements:
                    [
                        new VisualElementEngineeringDto(
                            "pump01",
                            "core.group",
                            DynamoKey: "dynamo.pump",
                            EquipmentPath: "Plant.P01",
                            Id: Guid.NewGuid())
                    ])
            ]);

        var preview = harness.Exchange.Preview(package, ImportMode.CreateOnly);
        Assert.True(preview.CanApply);

        var result = harness.Exchange.Apply(package, ImportMode.CreateOnly);
        Assert.DoesNotContain(result.Issues, issue => issue.IsError);

        var exported = harness.Exchange.ExportPackage();
        var equipment = Assert.Single(exported.Equipment!);
        var dynamo = Assert.Single(exported.Dynamos!);
        var screenElement = Assert.Single(Assert.Single(exported.Screens!).Elements!);
        var popup = Assert.Single(exported.Popups!);
        var popupElement = Assert.Single(popup.Elements!);

        Assert.Equal(templateId, equipment.TemplateId);
        Assert.Equal("pump.standard", equipment.TemplateKey);
        Assert.Equal(templateId, dynamo.TemplateId);
        Assert.Equal("pump.standard", dynamo.TemplateKey);
        Assert.Equal(templateId, popup.TemplateId);
        Assert.Equal("pump.standard", popup.TemplateKey);
        Assert.Equal(dynamoId, screenElement.DynamoDefinitionId);
        Assert.Equal("dynamo.pump", screenElement.DynamoKey);
        Assert.Equal(equipmentId, screenElement.EquipmentId);
        Assert.Equal("Plant.P01", screenElement.EquipmentPath);
        Assert.Equal(dynamoId, popupElement.DynamoDefinitionId);
        Assert.Equal(equipmentId, popupElement.EquipmentId);
    }

    [Fact]
    public void StableOnlyReferences_AreAccepted_AndCompatibilityAliasesAreRestored()
    {
        using var harness = CreateHarness();
        var templateId = Guid.NewGuid();
        var equipmentId = Guid.NewGuid();
        var dynamoId = Guid.NewGuid();

        var package = Package(
            templates:
            [
                new EquipmentTemplateEngineeringDto(templateId, "motor.standard", "Motor Template")
            ],
            equipment:
            [
                new EquipmentEngineeringDto(
                    equipmentId,
                    "Plant.M01",
                    "Motor M01",
                    TemplateId: templateId)
            ],
            dynamos:
            [
                new DynamoEngineeringDto(
                    dynamoId,
                    "dynamo.motor",
                    "Motor Dynamo",
                    TemplateId: templateId)
            ],
            screens:
            [
                new ScreenEngineeringDto(
                    Guid.NewGuid(),
                    "screen.motor",
                    "Motor",
                    Elements:
                    [
                        new VisualElementEngineeringDto(
                            "motor01",
                            "core.group",
                            Id: Guid.NewGuid(),
                            DynamoDefinitionId: dynamoId,
                            EquipmentId: equipmentId)
                    ])
            ],
            popups:
            [
                new PopupEngineeringDto(
                    Guid.NewGuid(),
                    "popup.motor",
                    "Motor",
                    Elements:
                    [
                        new VisualElementEngineeringDto(
                            "motor01",
                            "core.group",
                            Id: Guid.NewGuid(),
                            DynamoDefinitionId: dynamoId,
                            EquipmentId: equipmentId)
                    ],
                    TemplateId: templateId)
            ]);

        var preview = harness.Exchange.Preview(package, ImportMode.CreateOnly);
        Assert.True(preview.CanApply);

        var result = harness.Exchange.Apply(package, ImportMode.CreateOnly);
        Assert.DoesNotContain(result.Issues, issue => issue.IsError);

        var exported = harness.Exchange.ExportPackage();
        Assert.Equal("motor.standard", Assert.Single(exported.Equipment!).TemplateKey);
        Assert.Equal("motor.standard", Assert.Single(exported.Dynamos!).TemplateKey);
        Assert.Equal("motor.standard", Assert.Single(exported.Popups!).TemplateKey);

        var screenElement = Assert.Single(Assert.Single(exported.Screens!).Elements!);
        Assert.Equal("dynamo.motor", screenElement.DynamoKey);
        Assert.Equal("Plant.M01", screenElement.EquipmentPath);
    }

    [Fact]
    public void StableIdentity_WithStaleLegacyAlias_NormalizesToCurrentAlias()
    {
        using var harness = CreateHarness();
        var template = new EquipmentTemplateEngineeringDto(
            Guid.NewGuid(),
            "template.current",
            "Current Template");
        var dynamo = new DynamoEngineeringDto(
            Guid.NewGuid(),
            "dynamo.current",
            "Current Dynamo",
            TemplateId: template.Id,
            TemplateKey: template.Key);
        harness.Assets.UpsertTemplate(template);
        harness.Assets.UpsertDynamo(dynamo);

        var equipmentId = Guid.NewGuid();
        var package = Package(
            equipment:
            [
                new EquipmentEngineeringDto(
                    equipmentId,
                    "Plant.P01",
                    "Pump P01",
                    TemplateKey: "template.before-rename",
                    TemplateId: template.Id)
            ],
            screens:
            [
                new ScreenEngineeringDto(
                    Guid.NewGuid(),
                    "screen.rename",
                    "Rename",
                    Elements:
                    [
                        new VisualElementEngineeringDto(
                            "pump01",
                            "core.group",
                            DynamoKey: "dynamo.before-rename",
                            EquipmentPath: "Plant.BeforeRename",
                            Id: Guid.NewGuid(),
                            DynamoDefinitionId: dynamo.Id,
                            EquipmentId: equipmentId)
                    ])
            ]);

        var preview = harness.Exchange.Preview(package, ImportMode.CreateOnly);
        Assert.True(preview.CanApply);

        var result = harness.Exchange.Apply(package, ImportMode.CreateOnly);
        Assert.DoesNotContain(result.Issues, issue => issue.IsError);

        var exported = harness.Exchange.ExportPackage();
        var equipment = Assert.Single(exported.Equipment!);
        var element = Assert.Single(Assert.Single(exported.Screens!).Elements!);
        Assert.Equal(template.Id, equipment.TemplateId);
        Assert.Equal(template.Key, equipment.TemplateKey);
        Assert.Equal(dynamo.Id, element.DynamoDefinitionId);
        Assert.Equal(dynamo.Key, element.DynamoKey);
        Assert.Equal(equipmentId, element.EquipmentId);
        Assert.Equal("Plant.P01", element.EquipmentPath);
    }

    [Fact]
    public void StableIdAndAliasMismatch_FailsPreviewBeforeMutation()
    {
        using var harness = CreateHarness();
        var templateA = new EquipmentTemplateEngineeringDto(Guid.NewGuid(), "template.a", "Template A");
        var templateB = new EquipmentTemplateEngineeringDto(Guid.NewGuid(), "template.b", "Template B");
        harness.Assets.UpsertTemplate(templateA);
        harness.Assets.UpsertTemplate(templateB);

        var package = Package(
            equipment:
            [
                new EquipmentEngineeringDto(
                    Guid.NewGuid(),
                    "Plant.P01",
                    "Pump P01",
                    TemplateKey: templateB.Key,
                    TemplateId: templateA.Id)
            ]);

        var preview = harness.Exchange.Preview(package, ImportMode.CreateOnly);
        Assert.False(preview.CanApply);
        Assert.Contains(
            preview.Items.SelectMany(item => item.Issues),
            issue => issue.Code == "EQUIPMENT_TEMPLATE_REFERENCE_MISMATCH");

        var result = harness.Exchange.Apply(package, ImportMode.CreateOnly);
        Assert.Contains(result.Issues, issue => issue.Code == "EQUIPMENT_TEMPLATE_REFERENCE_MISMATCH");
        Assert.Empty(harness.Assets.SnapshotEquipment());
    }

    [Fact]
    public void ExplicitStableIdCannotClaimAnAliasOwnedByAnotherEntity()
    {
        using var harness = CreateHarness();
        var existingId = Guid.NewGuid();
        harness.Assets.UpsertDynamo(new DynamoEngineeringDto(
            existingId,
            "dynamo.shared",
            "Existing"));

        var incomingId = Guid.NewGuid();
        var package = Package(
            dynamos:
            [
                new DynamoEngineeringDto(
                    incomingId,
                    "dynamo.shared",
                    "Incoming")
            ]);

        var preview = harness.Exchange.Preview(package, ImportMode.CreateOnly);
        Assert.False(preview.CanApply);
        Assert.Contains(
            preview.Items.SelectMany(item => item.Issues),
            issue => issue.Code == "DYNAMO_STABLE_ID_KEY_COLLISION");

        var result = harness.Exchange.Apply(package, ImportMode.CreateOnly);
        Assert.Contains(result.Issues, issue => issue.Code == "DYNAMO_STABLE_ID_KEY_COLLISION");
        Assert.Equal(existingId, harness.Assets.FindDynamoByKey("dynamo.shared")!.Id);
        Assert.Null(harness.Assets.FindDynamo(incomingId));
    }

    [Fact]
    public void ProjectPackage_RoundTripPreservesNormalizedStableReuseReferences()
    {
        using var source = CreateHarness();
        var templateId = Guid.NewGuid();
        var equipmentId = Guid.NewGuid();
        var dynamoId = Guid.NewGuid();

        var package = Package(
            templates:
            [
                new EquipmentTemplateEngineeringDto(templateId, "pump.standard", "Pump Template")
            ],
            equipment:
            [
                new EquipmentEngineeringDto(
                    equipmentId,
                    "Plant.P01",
                    "Pump P01",
                    TemplateKey: "pump.standard")
            ],
            dynamos:
            [
                new DynamoEngineeringDto(
                    dynamoId,
                    "dynamo.pump",
                    "Pump Dynamo",
                    TemplateKey: "pump.standard")
            ],
            screens:
            [
                new ScreenEngineeringDto(
                    Guid.NewGuid(),
                    "screen.main",
                    "Main",
                    Elements:
                    [
                        new VisualElementEngineeringDto(
                            "pump01",
                            "core.group",
                            DynamoKey: "dynamo.pump",
                            EquipmentPath: "Plant.P01",
                            Id: Guid.NewGuid())
                    ])
            ]);

        Assert.DoesNotContain(
            source.Exchange.Apply(package, ImportMode.CreateOnly).Issues,
            issue => issue.IsError);

        var projectPackages = new ProjectPackageService(source.Exchange);
        var bytes = projectPackages.Export("reuse-r1", "Reuse R1");
        var inspection = projectPackages.Inspect(bytes);
        var exportedElement = Assert.Single(Assert.Single(inspection.Engineering.Screens!).Elements!);
        Assert.Equal(dynamoId, exportedElement.DynamoDefinitionId);
        Assert.Equal(equipmentId, exportedElement.EquipmentId);
        Assert.Equal(templateId, Assert.Single(inspection.Engineering.Equipment!).TemplateId);
        Assert.Equal(templateId, Assert.Single(inspection.Engineering.Dynamos!).TemplateId);

        using var target = CreateHarness();
        var targetPackages = new ProjectPackageService(target.Exchange);
        var preview = targetPackages.Preview(bytes, ImportMode.CreateOnly);
        Assert.True(preview.CanApply);
        var result = targetPackages.Apply(bytes, ImportMode.CreateOnly);
        Assert.DoesNotContain(result.Issues, issue => issue.IsError);

        var restored = target.Exchange.ExportPackage();
        var restoredElement = Assert.Single(Assert.Single(restored.Screens!).Elements!);
        Assert.Equal(dynamoId, restoredElement.DynamoDefinitionId);
        Assert.Equal(equipmentId, restoredElement.EquipmentId);
        Assert.Equal(templateId, Assert.Single(restored.Equipment!).TemplateId);
        Assert.Equal(templateId, Assert.Single(restored.Dynamos!).TemplateId);
    }

    [Fact]
    public void DynamoRuntimeComposer_AcceptsStableDefinitionReferenceWithoutAlias()
    {
        var definitionId = Guid.NewGuid();
        var definition = new DynamoEngineeringDto(
            definitionId,
            "dynamo.stable",
            "Stable Dynamo");
        var instance = new VisualElementEngineeringDto(
            "instance",
            "core.group",
            Id: Guid.NewGuid(),
            DynamoDefinitionId: definitionId);

        var composition = DynamoRuntimeComposer.Compose(instance, definition);

        Assert.Equal(definitionId, composition.DefinitionId);
        Assert.Equal("dynamo.stable", composition.DefinitionKey);
    }

    private static EngineeringPackage Package(
        IReadOnlyCollection<EquipmentTemplateEngineeringDto>? templates = null,
        IReadOnlyCollection<EquipmentEngineeringDto>? equipment = null,
        IReadOnlyCollection<DynamoEngineeringDto>? dynamos = null,
        IReadOnlyCollection<ScreenEngineeringDto>? screens = null,
        IReadOnlyCollection<PopupEngineeringDto>? popups = null) =>
        new(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            Array.Empty<TagEngineeringDto>(),
            Array.Empty<AlarmEngineeringDto>(),
            Templates: templates,
            Equipment: equipment,
            Dynamos: dynamos,
            Screens: screens,
            Popups: popups);

    private static Harness CreateHarness()
    {
        var eventBus = new InMemoryScadaEventBus();
        var tags = new InMemoryTagRegistry();
        var alarms = new InMemoryAlarmEngine(eventBus);
        var assets = new InMemoryEngineeringAssetRegistry();
        var views = new InMemoryEngineeringViewRegistry();
        var exchange = new EngineeringExchangeService(
            tags,
            alarms,
            new InMemoryDataSourceEngineeringRegistry(),
            assets,
            views);
        return new Harness(alarms, assets, exchange);
    }

    private sealed class Harness(
        InMemoryAlarmEngine alarms,
        InMemoryEngineeringAssetRegistry assets,
        EngineeringExchangeService exchange) : IDisposable
    {
        public InMemoryEngineeringAssetRegistry Assets { get; } = assets;
        public EngineeringExchangeService Exchange { get; } = exchange;
        public void Dispose() => alarms.Dispose();
    }
}
