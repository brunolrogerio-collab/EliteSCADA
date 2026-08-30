using Scada.Core.Abstractions;
using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Engineering.Contracts;
using Scada.Engineering.DataSources;
using Scada.Engineering.ImportExport;

namespace Scada.Core.Tests;

public sealed class CommunicationTagBindingEngineeringTests
{
    [Fact]
    public void Json_RoundTripsCanonicalCommunicationBindingAndTransform()
    {
        var tags = new InMemoryTagRegistry();
        var bus = new InMemoryScadaEventBus();
        using var alarms = new InMemoryAlarmEngine(bus);
        var binding = CreateBinding(byteSwap: true, wordSwap: true);
        var tag = TagDefinition.Create(
            "Pressure",
            "Plant.P01.Pressure",
            TagDataType.Int32,
            source: "plant.driver",
            metadata: new Dictionary<string, string> { ["address"] = binding.PortableAddress },
            communicationBinding: binding);
        tags.Register(tag);
        var service = new EngineeringExchangeService(tags, alarms);

        var json = service.ExportJson();
        Assert.DoesNotContain("\"effectiveSettings\"", json);
        Assert.DoesNotContain("\"isIdentity\"", json);
        var package = service.ParseJson(json);

        Assert.Equal(14, package.SchemaVersion);
        var parsed = Assert.Single(package.Tags).CommunicationBinding;
        Assert.NotNull(parsed);
        Assert.Equal("test.binding", parsed!.SchemaId);
        Assert.Equal(2, parsed.SchemaVersion);
        Assert.Equal("test:v2;address=40001", parsed.PortableAddress);
        Assert.Equal("int32", parsed.EffectiveSettings["nativeType"]);
        Assert.True(parsed.ValueTransform!.ByteSwap);
        Assert.True(parsed.ValueTransform.WordSwap);
    }

    [Fact]
    public void Csv_RoundTripsCanonicalCommunicationBindingAndTransform()
    {
        var tags = new InMemoryTagRegistry();
        var bus = new InMemoryScadaEventBus();
        using var alarms = new InMemoryAlarmEngine(bus);
        var binding = CreateBinding(byteSwap: true, wordSwap: false);
        tags.Register(TagDefinition.Create(
            "Flow",
            "Plant.F01.Flow",
            TagDataType.Float,
            source: "plant.driver",
            metadata: new Dictionary<string, string> { ["address"] = binding.PortableAddress },
            communicationBinding: binding));
        var service = new EngineeringExchangeService(tags, alarms);

        var parsedPackage = service.ParseTagsCsv(service.ExportTagsCsv());

        var parsed = Assert.Single(parsedPackage.Tags).CommunicationBinding;
        Assert.NotNull(parsed);
        Assert.Equal(binding.PortableAddress, parsed!.PortableAddress);
        Assert.Equal("int32", parsed.EffectiveSettings["nativeType"]);
        Assert.True(parsed.ValueTransform!.ByteSwap);
        Assert.False(parsed.ValueTransform.WordSwap);
    }

    [Fact]
    public void Apply_PreservesCanonicalCommunicationBindingOnTagDefinition()
    {
        var tags = new InMemoryTagRegistry();
        var bus = new InMemoryScadaEventBus();
        using var alarms = new InMemoryAlarmEngine(bus);
        var dataSources = new InMemoryDataSourceEngineeringRegistry();
        var service = new EngineeringExchangeService(tags, alarms, dataSources);
        var binding = CreateBinding(byteSwap: false, wordSwap: true);
        var package = Package(new TagEngineeringDto(
            null,
            "Counter",
            "Plant.Counter",
            TagDataType.Int32,
            Source: "plant.driver",
            Address: binding.PortableAddress,
            CommunicationBinding: binding));

        var result = service.Apply(package, ImportMode.CreateAndUpdate);

        Assert.Empty(result.Issues);
        Assert.True(tags.TryGetByPath("Plant.Counter", out var applied));
        Assert.NotNull(applied!.CommunicationBinding);
        Assert.True(applied.CommunicationBinding!.ValueTransform!.WordSwap);
    }

    [Fact]
    public void ParseJson_AcceptsSchema13WithoutCommunicationBinding()
    {
        var tags = new InMemoryTagRegistry();
        var bus = new InMemoryScadaEventBus();
        using var alarms = new InMemoryAlarmEngine(bus);
        tags.Register(TagDefinition.Create("Legacy", "Plant.Legacy", TagDataType.Int32));
        var service = new EngineeringExchangeService(tags, alarms);
        var legacyJson = service.ExportJson().Replace("\"schemaVersion\": 14", "\"schemaVersion\": 13", StringComparison.Ordinal);

        var package = service.ParseJson(legacyJson);

        Assert.Equal(13, package.SchemaVersion);
        Assert.Null(Assert.Single(package.Tags).CommunicationBinding);
    }

    [Fact]
    public void Preview_RejectsLegacyAddressThatDiffersFromCanonicalPortableAddress()
    {
        var service = CreateService(out _, out var alarms);
        using (alarms)
        {
            var binding = CreateBinding();
            var package = Package(new TagEngineeringDto(
                null,
                "Bad",
                "Plant.Bad",
                TagDataType.Int32,
                Source: "plant.driver",
                Address: "different-address",
                CommunicationBinding: binding));

            var preview = service.Preview(package, ImportMode.CreateAndUpdate);

            Assert.False(preview.CanApply);
            Assert.Contains(preview.Items.SelectMany(x => x.Issues), x => x.Code == "TAG_COMMUNICATION_BINDING_ADDRESS_MISMATCH");
        }
    }

    [Fact]
    public void Preview_RejectsWordSwapOnSingleWordInt16()
    {
        var service = CreateService(out _, out var alarms);
        using (alarms)
        {
            var binding = CreateBinding(wordSwap: true);
            var package = Package(new TagEngineeringDto(
                null,
                "Word",
                "Plant.Word",
                TagDataType.Int16,
                Source: "plant.driver",
                Address: binding.PortableAddress,
                CommunicationBinding: binding));

            var preview = service.Preview(package, ImportMode.CreateAndUpdate);

            Assert.False(preview.CanApply);
            Assert.Contains(preview.Items.SelectMany(x => x.Issues), x => x.Code == "TAG_BINDING_WORD_SWAP_WIDTH_INVALID");
        }
    }

    [Fact]
    public void Preview_RejectsPlaintextSecretInBindingSettings()
    {
        var service = CreateService(out _, out var alarms);
        using (alarms)
        {
            var binding = CreateBinding(settings: new Dictionary<string, string> { ["password"] = "plaintext" });
            var package = Package(new TagEngineeringDto(
                null,
                "Secret",
                "Plant.Secret",
                TagDataType.Int32,
                Source: "plant.driver",
                Address: binding.PortableAddress,
                CommunicationBinding: binding));

            var preview = service.Preview(package, ImportMode.CreateAndUpdate);

            Assert.False(preview.CanApply);
            Assert.Contains(preview.Items.SelectMany(x => x.Issues), x => x.Code == "TAG_BINDING_PLAINTEXT_SECRET");
        }
    }

    [Fact]
    public void Preview_AllowsPhysicalTransformForBooleanBitSelector()
    {
        var service = CreateService(out _, out var alarms);
        using (alarms)
        {
            var binding = CreateBinding(byteSwap: true, wordSwap: true);
            var package = Package(new TagEngineeringDto(
                null,
                "Bit",
                "Plant.Word.Bit3",
                TagDataType.Boolean,
                Source: "plant.driver",
                Address: binding.PortableAddress,
                AddressSelector: new TagValueSelector(TagValueSelectorKind.Bit, 3),
                CommunicationBinding: binding));

            var preview = service.Preview(package, ImportMode.CreateAndUpdate);

            Assert.True(preview.CanApply);
        }
    }

    private static EngineeringExchangeService CreateService(
        out InMemoryTagRegistry tags,
        out InMemoryAlarmEngine alarms)
    {
        tags = new InMemoryTagRegistry();
        var bus = new InMemoryScadaEventBus();
        alarms = new InMemoryAlarmEngine(bus);
        return new EngineeringExchangeService(tags, alarms, new InMemoryDataSourceEngineeringRegistry());
    }

    private static EngineeringPackage Package(TagEngineeringDto tag) =>
        new(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            new[] { tag },
            Array.Empty<AlarmEngineeringDto>(),
            new[] { new DataSourceEngineeringDto(null, "plant.driver", "Plant Driver", "modbus.tcp") });

    private static CommunicationTagBinding CreateBinding(
        bool byteSwap = false,
        bool wordSwap = false,
        IReadOnlyDictionary<string, string>? settings = null) =>
        new(
            CommunicationTagBinding.CurrentContractVersion,
            "test.binding",
            2,
            "test:v2;address=40001",
            settings ?? new Dictionary<string, string> { ["nativeType"] = "int32" },
            new TagPhysicalValueTransform(
                TagPhysicalValueTransform.CurrentContractVersion,
                byteSwap,
                wordSwap));
}
