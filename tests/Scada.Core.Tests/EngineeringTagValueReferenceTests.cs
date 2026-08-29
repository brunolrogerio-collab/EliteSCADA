using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Engineering.Assets;
using Scada.Engineering.Contracts;
using Scada.Engineering.DataSources;
using Scada.Engineering.ImportExport;

namespace Scada.Core.Tests;

public sealed class EngineeringTagValueReferenceTests
{
    [Fact]
    public void ExportAndParseJson_RoundTripsStableBitBindingReference()
    {
        var tags = new InMemoryTagRegistry();
        var tag = TagDefinition.Create("Status", "Plant.Status", TagDataType.Int16);
        tags.Register(tag);
        var assets = new InMemoryEngineeringAssetRegistry();
        assets.UpsertEquipment(new EquipmentEngineeringDto(
            null,
            "Plant.Equipment01",
            "Equipment 01",
            Bindings:
            [
                new EngineeringBindingDto(
                    "running",
                    EngineeringBindingKind.Tag,
                    "Plant.Status.03",
                    TagReference: new TagValueReference(
                        tag.Id,
                        new TagValueSelector(TagValueSelectorKind.Bit, 3)))
            ]));

        using var alarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var service = new EngineeringExchangeService(
            tags,
            alarms,
            new InMemoryDataSourceEngineeringRegistry(),
            assets);

        var package = service.ParseJson(service.ExportJson());
        var binding = Assert.Single(Assert.Single(package.Equipment!).Bindings!);

        Assert.Equal("Plant.Status.03", binding.Target);
        Assert.NotNull(binding.TagReference);
        Assert.Equal(tag.Id, binding.TagReference!.TagId);
        Assert.Equal(TagValueSelectorKind.Bit, binding.TagReference.Selector!.Kind);
        Assert.Equal(3, binding.TagReference.Selector.Index);
    }

    [Fact]
    public void Preview_UsesStableTagIdWhenFriendlyTargetPathIsStale()
    {
        var tags = new InMemoryTagRegistry();
        var tag = TagDefinition.Create("Status", "Plant.RenamedStatus", TagDataType.Int16);
        tags.Register(tag);
        using var alarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var service = new EngineeringExchangeService(
            tags,
            alarms,
            new InMemoryDataSourceEngineeringRegistry(),
            new InMemoryEngineeringAssetRegistry());
        var package = new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            Array.Empty<TagEngineeringDto>(),
            Array.Empty<AlarmEngineeringDto>(),
            Equipment:
            [
                new EquipmentEngineeringDto(
                    null,
                    "Plant.Equipment01",
                    "Equipment 01",
                    Bindings:
                    [
                        new EngineeringBindingDto(
                            "running",
                            EngineeringBindingKind.Tag,
                            "Plant.OldStatus.03",
                            TagReference: new TagValueReference(
                                tag.Id,
                                new TagValueSelector(TagValueSelectorKind.Bit, 3)))
                    ])
            ]);

        var preview = service.Preview(package, ImportMode.CreateAndUpdate);

        Assert.True(preview.CanApply);
        Assert.DoesNotContain(preview.Items.SelectMany(x => x.Issues), issue => issue.Code == "BINDING_TAG_NOT_FOUND");
    }

    [Fact]
    public void Preview_RejectsBitSelectorOutsideAuthoritativeTagWidth()
    {
        var tags = new InMemoryTagRegistry();
        var tag = TagDefinition.Create("Status", "Plant.Status", TagDataType.Int16);
        tags.Register(tag);
        using var alarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var service = new EngineeringExchangeService(
            tags,
            alarms,
            new InMemoryDataSourceEngineeringRegistry(),
            new InMemoryEngineeringAssetRegistry());
        var package = new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            Array.Empty<TagEngineeringDto>(),
            Array.Empty<AlarmEngineeringDto>(),
            Equipment:
            [
                new EquipmentEngineeringDto(
                    null,
                    "Plant.Equipment01",
                    "Equipment 01",
                    Bindings:
                    [
                        new EngineeringBindingDto(
                            "running",
                            EngineeringBindingKind.Tag,
                            "Plant.Status.16",
                            TagReference: new TagValueReference(
                                tag.Id,
                                new TagValueSelector(TagValueSelectorKind.Bit, 16)))
                    ])
            ]);

        var preview = service.Preview(package, ImportMode.CreateAndUpdate);

        Assert.False(preview.CanApply);
        Assert.Contains(preview.Items.SelectMany(x => x.Issues), issue => issue.Code == "BINDING_TAG_SELECTOR_INVALID");
    }

    [Fact]
    public void Preview_RejectsStableTagReferenceOnNonTagBindingKind()
    {
        var tags = new InMemoryTagRegistry();
        var tag = TagDefinition.Create("Status", "Plant.Status", TagDataType.Int16);
        tags.Register(tag);
        using var alarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var service = new EngineeringExchangeService(
            tags,
            alarms,
            new InMemoryDataSourceEngineeringRegistry(),
            new InMemoryEngineeringAssetRegistry());
        var package = new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            Array.Empty<TagEngineeringDto>(),
            Array.Empty<AlarmEngineeringDto>(),
            Equipment:
            [
                new EquipmentEngineeringDto(
                    null,
                    "Plant.Equipment01",
                    "Equipment 01",
                    Bindings:
                    [
                        new EngineeringBindingDto(
                            "caption",
                            EngineeringBindingKind.Property,
                            "some.property",
                            TagReference: new TagValueReference(tag.Id))
                    ])
            ]);

        var preview = service.Preview(package, ImportMode.CreateAndUpdate);

        Assert.False(preview.CanApply);
        Assert.Contains(preview.Items.SelectMany(x => x.Issues), issue => issue.Code == "BINDING_TAG_REFERENCE_KIND_INVALID");
    }
}
