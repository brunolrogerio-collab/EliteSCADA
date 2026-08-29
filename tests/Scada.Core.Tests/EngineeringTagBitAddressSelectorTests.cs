using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Engineering.Contracts;
using Scada.Engineering.DataSources;
using Scada.Engineering.ImportExport;
using Scada.Engineering.Validation;

namespace Scada.Core.Tests;

public sealed class EngineeringTagBitAddressSelectorTests
{
    [Fact]
    public void JsonAndCsv_RoundTripStructuredAddressSelector()
    {
        var tags = new InMemoryTagRegistry();
        using var alarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var tag = new TagDefinition(
            Guid.NewGuid(),
            "Command bit 7",
            "Plant.Command.Bit7",
            TagDataType.Boolean,
            "plant.modbus01",
            null,
            null,
            false,
            new Dictionary<string, string> { ["address"] = "400001" },
            AddressSelector: new TagValueSelector(TagValueSelectorKind.Bit, 7));
        tags.Register(tag);
        var service = new EngineeringExchangeService(tags, alarms);

        var json = service.ExportJson();
        var jsonTag = Assert.Single(service.ParseJson(json).Tags);
        var csv = service.ExportTagsCsv();
        var csvTag = Assert.Single(service.ParseTagsCsv(csv).Tags);

        Assert.Contains("\"addressSelector\"", json, StringComparison.Ordinal);
        Assert.Contains("\"kind\": \"bit\"", json, StringComparison.Ordinal);
        Assert.Equal(TagValueSelectorKind.Bit, jsonTag.AddressSelector?.Kind);
        Assert.Equal(7, jsonTag.AddressSelector?.Index);
        Assert.Contains("AddressSelectorKind", csv, StringComparison.Ordinal);
        Assert.Contains("AddressSelectorIndex", csv, StringComparison.Ordinal);
        Assert.Equal(TagValueSelectorKind.Bit, csvTag.AddressSelector?.Kind);
        Assert.Equal(7, csvTag.AddressSelector?.Index);
    }

    [Fact]
    public void PreviewApplyExport_PreservesSelectorInTagDefinitionWithoutMetadataAuthority()
    {
        var tags = new InMemoryTagRegistry();
        using var alarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var dataSources = new InMemoryDataSourceEngineeringRegistry();
        dataSources.Upsert(new DataSourceEngineeringDto(
            null,
            "plant.modbus01",
            "PLC principal",
            "modbus.tcp",
            Settings: new() { ["host"] = "127.0.0.1" }));
        var service = new EngineeringExchangeService(tags, alarms, dataSources);
        var package = Package(new TagEngineeringDto(
            null,
            "Command bit 7",
            "Plant.Command.Bit7",
            TagDataType.Boolean,
            Source: "plant.modbus01",
            Address: "400001",
            ReadOnly: false,
            Metadata: new() { ["area"] = "Plant" },
            AddressSelector: new TagValueSelector(TagValueSelectorKind.Bit, 7)));

        var preview = service.Preview(package, ImportMode.CreateAndUpdate);
        var result = service.Apply(package, ImportMode.CreateAndUpdate);

        Assert.True(preview.CanApply);
        Assert.Empty(result.Issues);
        Assert.Equal(1, result.Created);
        Assert.True(tags.TryGetByPath("Plant.Command.Bit7", out var stored));
        Assert.Equal(TagValueSelectorKind.Bit, stored!.AddressSelector?.Kind);
        Assert.Equal(7, stored.AddressSelector?.Index);
        Assert.DoesNotContain(
            stored.Metadata?.Keys ?? Array.Empty<string>(),
            key => key.Contains("selector", StringComparison.OrdinalIgnoreCase) ||
                   key.Contains("bitIndex", StringComparison.OrdinalIgnoreCase));

        var exported = Assert.Single(service.ExportPackage().Tags);
        Assert.Equal(TagValueSelectorKind.Bit, exported.AddressSelector?.Kind);
        Assert.Equal(7, exported.AddressSelector?.Index);
        Assert.Equal("400001", exported.Address);
    }

    [Fact]
    public void GenericValidation_RejectsInvalidSelectorShapeButLeavesProtocolRangeToDriver()
    {
        Assert.Contains(
            EngineeringValidator.ValidateTag(Tag(TagDataType.Int16, "source", "400001", new(TagValueSelectorKind.Bit, 7))),
            issue => issue.Code == "TAG_ADDRESS_SELECTOR_BOOLEAN_REQUIRED");
        Assert.Contains(
            EngineeringValidator.ValidateTag(Tag(TagDataType.Boolean, null, "400001", new(TagValueSelectorKind.Bit, 7))),
            issue => issue.Code == "TAG_ADDRESS_SELECTOR_SOURCE_REQUIRED");
        Assert.Contains(
            EngineeringValidator.ValidateTag(Tag(TagDataType.Boolean, "source", null, new(TagValueSelectorKind.Bit, 7))),
            issue => issue.Code == "TAG_ADDRESS_SELECTOR_ADDRESS_REQUIRED");
        Assert.Contains(
            EngineeringValidator.ValidateTag(Tag(TagDataType.Boolean, "source", "400001", new(TagValueSelectorKind.Bit, -1))),
            issue => issue.Code == "TAG_ADDRESS_SELECTOR_INDEX_INVALID");
        Assert.Contains(
            EngineeringValidator.ValidateTag(Tag(TagDataType.Boolean, "source", "400001", new(TagValueSelectorKind.Bit, 64))),
            issue => issue.Code == "TAG_ADDRESS_SELECTOR_INDEX_INVALID");
        Assert.Contains(
            EngineeringValidator.ValidateTag(Tag(TagDataType.Boolean, "source", "400001", new((TagValueSelectorKind)99, 7))),
            issue => issue.Code == "TAG_ADDRESS_SELECTOR_KIND_UNSUPPORTED");

        var protocolSpecificIndex = EngineeringValidator.ValidateTag(
            Tag(TagDataType.Boolean, "plant.modbus01", "400001", new(TagValueSelectorKind.Bit, 31)));
        Assert.DoesNotContain(protocolSpecificIndex, issue => issue.Code == "TAG_ADDRESS_SELECTOR_INDEX_INVALID");
    }

    [Fact]
    public void SchemaV13WithoutAddressSelector_ParsesPreviewsAndAppliesUnchanged()
    {
        var tags = new InMemoryTagRegistry();
        using var alarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var service = new EngineeringExchangeService(tags, alarms);
        const string legacyV13 = """
        {
          "schema": "scada.engineering",
          "schemaVersion": 13,
          "exportedAt": "2026-08-29T00:00:00Z",
          "tags": [
            {
              "id": null,
              "name": "Legacy flag",
              "path": "Legacy.Flag",
              "dataType": "boolean",
              "readOnly": true
            }
          ],
          "alarms": []
        }
        """;

        var package = service.ParseJson(legacyV13);
        var imported = Assert.Single(package.Tags);
        var preview = service.Preview(package, ImportMode.CreateAndUpdate);
        var result = service.Apply(package, ImportMode.CreateAndUpdate);

        Assert.Null(imported.AddressSelector);
        Assert.True(preview.CanApply);
        Assert.Empty(result.Issues);
        Assert.True(tags.TryGetByPath("Legacy.Flag", out var stored));
        Assert.Null(stored!.AddressSelector);
    }

    [Fact]
    public void LegacyTagCsvWithoutSelectorColumns_RemainsReadable()
    {
        var tags = new InMemoryTagRegistry();
        using var alarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var service = new EngineeringExchangeService(tags, alarms);
        const string legacyCsv = "Id,Path,Name,DataType,ReadOnly\r\n,Legacy.Flag,Legacy flag,Boolean,True\r\n";

        var imported = Assert.Single(service.ParseTagsCsv(legacyCsv).Tags);

        Assert.Equal("Legacy.Flag", imported.Path);
        Assert.Null(imported.AddressSelector);
    }

    [Fact]
    public void CsvWithPartialAddressSelector_FailsClosed()
    {
        var tags = new InMemoryTagRegistry();
        using var alarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var service = new EngineeringExchangeService(tags, alarms);
        const string csv = "Path,Name,DataType,Source,Address,AddressSelectorKind,ReadOnly\r\nPlant.Bit,Bit,Boolean,source,400001,Bit,True\r\n";

        Assert.Throws<InvalidDataException>(() => service.ParseTagsCsv(csv));
    }

    private static TagEngineeringDto Tag(
        TagDataType dataType,
        string? source,
        string? address,
        TagValueSelector selector) =>
        new(
            null,
            "Selector test",
            "Plant.SelectorTest",
            dataType,
            Source: source,
            Address: address,
            AddressSelector: selector);

    private static EngineeringPackage Package(TagEngineeringDto tag) =>
        new(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            new[] { tag },
            Array.Empty<AlarmEngineeringDto>());
}
