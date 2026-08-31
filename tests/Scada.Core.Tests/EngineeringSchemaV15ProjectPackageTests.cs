using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Engineering.Contracts;
using Scada.Engineering.DataSources;
using Scada.Engineering.ImportExport;
using Scada.Engineering.ProjectPackages;

namespace Scada.Core.Tests;

public sealed class EngineeringSchemaV15ProjectPackageTests
{
    [Fact]
    public void ProjectPackage_InspectAndApply_RoundTripsCommunicationBinding()
    {
        var binding = new CommunicationTagBinding(
            CommunicationTagBinding.CurrentContractVersion,
            "modbus.tcp.tag",
            1,
            "40001",
            new Dictionary<string, string> { ["function"] = "holding-registers" },
            new TagPhysicalValueTransform(ByteSwap: true, WordSwap: true));

        var sourceTags = new InMemoryTagRegistry();
        using var sourceAlarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var sourceDataSources = new InMemoryDataSourceEngineeringRegistry();
        sourceDataSources.Upsert(new DataSourceEngineeringDto(
            null,
            "plant.modbus01",
            "PLC principal",
            "modbus.tcp",
            Settings: new Dictionary<string, string> { ["host"] = "10.10.0.10" }));
        sourceTags.Register(TagDefinition.Create(
            "Pressure",
            "Plant.P01.Pressure",
            TagDataType.Double,
            source: "plant.modbus01",
            metadata: new Dictionary<string, string> { ["address"] = binding.PortableAddress },
            communicationBinding: binding));
        var source = new ProjectPackageService(
            new EngineeringExchangeService(sourceTags, sourceAlarms, sourceDataSources));

        var bytes = source.Export("plant-v15", "Plant v15");
        var inspection = source.Inspect(bytes);

        Assert.Equal(15, inspection.Manifest.EngineeringSchemaVersion);
        var inspectedTag = Assert.Single(inspection.Engineering.Tags);
        Assert.NotNull(inspectedTag.CommunicationBinding);
        Assert.Equal("40001", inspectedTag.Address);
        Assert.Equal("modbus.tcp.tag", inspectedTag.CommunicationBinding!.SchemaId);
        Assert.True(inspectedTag.CommunicationBinding.ValueTransform!.ByteSwap);
        Assert.True(inspectedTag.CommunicationBinding.ValueTransform.WordSwap);

        var targetTags = new InMemoryTagRegistry();
        using var targetAlarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var target = new ProjectPackageService(
            new EngineeringExchangeService(
                targetTags,
                targetAlarms,
                new InMemoryDataSourceEngineeringRegistry()));

        var preview = target.Preview(bytes, ImportMode.CreateAndUpdate);
        var result = target.Apply(bytes, ImportMode.CreateAndUpdate);

        Assert.True(preview.CanApply);
        Assert.Empty(result.Issues);
        Assert.True(targetTags.TryGetByPath("Plant.P01.Pressure", out var restored));
        Assert.NotNull(restored!.CommunicationBinding);
        Assert.Equal("modbus.tcp.tag", restored.CommunicationBinding!.SchemaId);
        Assert.Equal("40001", restored.CommunicationBinding.PortableAddress);
        Assert.Equal("holding-registers", restored.CommunicationBinding.EffectiveSettings["function"]);
        Assert.True(restored.CommunicationBinding.ValueTransform!.ByteSwap);
        Assert.True(restored.CommunicationBinding.ValueTransform.WordSwap);
    }
}
