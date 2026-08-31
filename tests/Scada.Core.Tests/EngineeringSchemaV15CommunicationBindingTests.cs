using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Engineering.Contracts;
using Scada.Engineering.DataSources;
using Scada.Engineering.ImportExport;

namespace Scada.Core.Tests;

public sealed class EngineeringSchemaV15CommunicationBindingTests
{
    [Fact]
    public void JsonPreviewApplyExport_RoundTripsCommunicationBinding()
    {
        var tags = new InMemoryTagRegistry();
        var bus = new InMemoryScadaEventBus();
        using var alarms = new InMemoryAlarmEngine(bus);
        var dataSources = new InMemoryDataSourceEngineeringRegistry();
        var service = new EngineeringExchangeService(tags, alarms, dataSources);
        var binding = new CommunicationTagBinding(
            CommunicationTagBinding.CurrentContractVersion,
            "modbus.tcp.tag",
            1,
            "40001",
            new Dictionary<string, string> { ["function"] = "holding-registers" },
            new TagPhysicalValueTransform(ByteSwap: true, WordSwap: true));
        var package = Package(
            EngineeringExchangeService.CurrentSchemaVersion,
            new TagEngineeringDto(
                null,
                "Pressure",
                "Plant.P01.Pressure",
                TagDataType.Double,
                Source: "plant.modbus01",
                Address: binding.PortableAddress,
                ReadOnly: false,
                CommunicationBinding: binding));

        var preview = service.Preview(package, ImportMode.CreateAndUpdate);
        var result = service.Apply(package, ImportMode.CreateAndUpdate);

        Assert.True(preview.CanApply);
        Assert.Empty(result.Issues);
        Assert.True(tags.TryGetByPath("Plant.P01.Pressure", out var applied));
        Assert.NotNull(applied!.CommunicationBinding);
        Assert.Equal("modbus.tcp.tag", applied.CommunicationBinding!.SchemaId);
        Assert.Equal("40001", applied.CommunicationBinding.PortableAddress);
        Assert.Equal("holding-registers", applied.CommunicationBinding.EffectiveSettings["function"]);
        Assert.True(applied.CommunicationBinding.ValueTransform!.ByteSwap);
        Assert.True(applied.CommunicationBinding.ValueTransform.WordSwap);

        var exported = service.ParseJson(service.ExportJson());
        var roundTripped = Assert.Single(exported.Tags);
        Assert.Equal(EngineeringExchangeService.CurrentSchemaVersion, exported.SchemaVersion);
        Assert.Equal("40001", roundTripped.Address);
        Assert.NotNull(roundTripped.CommunicationBinding);
        Assert.Equal("modbus.tcp.tag", roundTripped.CommunicationBinding!.SchemaId);
        Assert.Equal("40001", roundTripped.CommunicationBinding.PortableAddress);
        Assert.Equal("holding-registers", roundTripped.CommunicationBinding.EffectiveSettings["function"]);
        Assert.True(roundTripped.CommunicationBinding.ValueTransform!.ByteSwap);
        Assert.True(roundTripped.CommunicationBinding.ValueTransform.WordSwap);
    }

    [Fact]
    public void CsvPreviewApplyExport_RoundTripsCommunicationBinding()
    {
        var binding = new CommunicationTagBinding(
            CommunicationTagBinding.CurrentContractVersion,
            "modbus.tcp.tag",
            1,
            "40001",
            new Dictionary<string, string> { ["function"] = "holding-registers" },
            new TagPhysicalValueTransform(ByteSwap: true, WordSwap: true));

        var sourceTags = new InMemoryTagRegistry();
        var sourceBus = new InMemoryScadaEventBus();
        using var sourceAlarms = new InMemoryAlarmEngine(sourceBus);
        var sourceDataSources = new InMemoryDataSourceEngineeringRegistry();
        sourceDataSources.Upsert(DataSource());
        sourceTags.Register(TagDefinition.Create(
            "Pressure",
            "Plant.P01.Pressure",
            TagDataType.Double,
            source: "plant.modbus01",
            metadata: new Dictionary<string, string> { ["address"] = binding.PortableAddress },
            communicationBinding: binding));
        var sourceService = new EngineeringExchangeService(sourceTags, sourceAlarms, sourceDataSources);

        var targetTags = new InMemoryTagRegistry();
        var targetBus = new InMemoryScadaEventBus();
        using var targetAlarms = new InMemoryAlarmEngine(targetBus);
        var targetDataSources = new InMemoryDataSourceEngineeringRegistry();
        targetDataSources.Upsert(DataSource());
        var targetService = new EngineeringExchangeService(targetTags, targetAlarms, targetDataSources);

        var csv = sourceService.ExportTagsCsv();
        var package = targetService.ParseTagsCsv(csv);
        var preview = targetService.Preview(package, ImportMode.CreateAndUpdate);
        var result = targetService.Apply(package, ImportMode.CreateAndUpdate);
        var reExported = targetService.ParseTagsCsv(targetService.ExportTagsCsv());

        Assert.Contains("CommunicationBindingJson", csv, StringComparison.Ordinal);
        Assert.True(preview.CanApply);
        Assert.Empty(result.Issues);
        var roundTripped = Assert.Single(reExported.Tags);
        Assert.Equal("40001", roundTripped.Address);
        Assert.NotNull(roundTripped.CommunicationBinding);
        Assert.Equal("modbus.tcp.tag", roundTripped.CommunicationBinding!.SchemaId);
        Assert.Equal("holding-registers", roundTripped.CommunicationBinding.EffectiveSettings["function"]);
        Assert.True(roundTripped.CommunicationBinding.ValueTransform!.ByteSwap);
        Assert.True(roundTripped.CommunicationBinding.ValueTransform.WordSwap);
    }

    [Fact]
    public void ParseTagsCsv_AcceptsLegacyColumnsWithoutCommunicationBinding()
    {
        var tags = new InMemoryTagRegistry();
        var bus = new InMemoryScadaEventBus();
        using var alarms = new InMemoryAlarmEngine(bus);
        tags.Register(TagDefinition.Create("Legacy", "Plant.Legacy", TagDataType.Double));
        var service = new EngineeringExchangeService(tags, alarms);
        var currentCsv = service.ExportTagsCsv();
        var legacyCsv = currentCsv.Replace(",CommunicationBindingJson", string.Empty, StringComparison.Ordinal);

        var package = service.ParseTagsCsv(legacyCsv);

        var tag = Assert.Single(package.Tags);
        Assert.Equal("Plant.Legacy", tag.Path);
        Assert.Null(tag.CommunicationBinding);
    }

    [Fact]
    public void Preview_RejectsCommunicationBindingInSchemaV14()
    {
        var service = CreateService(out _);
        var binding = ValidBinding();
        var package = Package(
            14,
            new TagEngineeringDto(
                null,
                "Pressure",
                "Plant.P01.Pressure",
                TagDataType.Double,
                Source: "plant.modbus01",
                Address: binding.PortableAddress,
                CommunicationBinding: binding));

        var preview = service.Preview(package, ImportMode.CreateAndUpdate);

        Assert.False(preview.CanApply);
        Assert.Contains(
            preview.Items.SelectMany(x => x.Issues),
            x => x.Code == "TAG_COMMUNICATION_BINDING_SCHEMA_VERSION");
    }

    [Fact]
    public void Preview_RejectsPlaintextSecretInCommunicationBindingSettings()
    {
        var service = CreateService(out _);
        var binding = new CommunicationTagBinding(
            CommunicationTagBinding.CurrentContractVersion,
            "modbus.tcp.tag",
            1,
            "40001",
            new Dictionary<string, string> { ["password"] = "do-not-store-this" });
        var package = Package(
            EngineeringExchangeService.CurrentSchemaVersion,
            new TagEngineeringDto(
                null,
                "Pressure",
                "Plant.P01.Pressure",
                TagDataType.Double,
                Source: "plant.modbus01",
                Address: binding.PortableAddress,
                CommunicationBinding: binding));

        var preview = service.Preview(package, ImportMode.CreateAndUpdate);

        Assert.False(preview.CanApply);
        Assert.Contains(
            preview.Items.SelectMany(x => x.Issues),
            x => x.Code == "TAG_COMMUNICATION_BINDING_PLAINTEXT_SECRET");
    }

    [Fact]
    public void Preview_RejectsAddressPortableAddressMismatch()
    {
        var service = CreateService(out _);
        var binding = ValidBinding();
        var package = Package(
            EngineeringExchangeService.CurrentSchemaVersion,
            new TagEngineeringDto(
                null,
                "Pressure",
                "Plant.P01.Pressure",
                TagDataType.Double,
                Source: "plant.modbus01",
                Address: "40002",
                CommunicationBinding: binding));

        var preview = service.Preview(package, ImportMode.CreateAndUpdate);

        Assert.False(preview.CanApply);
        Assert.Contains(
            preview.Items.SelectMany(x => x.Issues),
            x => x.Code == "TAG_COMMUNICATION_BINDING_ADDRESS_MISMATCH");
    }

    private static EngineeringExchangeService CreateService(out InMemoryTagRegistry tags)
    {
        tags = new InMemoryTagRegistry();
        var bus = new InMemoryScadaEventBus();
        var alarms = new InMemoryAlarmEngine(bus);
        return new EngineeringExchangeService(tags, alarms, new InMemoryDataSourceEngineeringRegistry());
    }

    private static CommunicationTagBinding ValidBinding() =>
        new(
            CommunicationTagBinding.CurrentContractVersion,
            "modbus.tcp.tag",
            1,
            "40001",
            new Dictionary<string, string> { ["function"] = "holding-registers" });

    private static DataSourceEngineeringDto DataSource() =>
        new(
            null,
            "plant.modbus01",
            "PLC principal",
            "modbus.tcp",
            Settings: new Dictionary<string, string> { ["host"] = "10.10.0.10" });

    private static EngineeringPackage Package(int schemaVersion, TagEngineeringDto tag) =>
        new(
            EngineeringExchangeService.CurrentSchema,
            schemaVersion,
            DateTimeOffset.UtcNow,
            new[] { tag },
            Array.Empty<AlarmEngineeringDto>(),
            new[] { DataSource() });
}
