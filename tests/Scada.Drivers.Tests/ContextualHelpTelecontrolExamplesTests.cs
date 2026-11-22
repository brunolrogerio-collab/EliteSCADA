using System.Globalization;
using System.Text.Json;
using Scada.Api.Runtime;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.Drivers.Abstractions;
using Scada.Drivers.Dnp3;
using Scada.Drivers.Iec60870;
using Scada.Drivers.Modbus;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;

namespace Scada.Drivers.Tests;

public sealed class ContextualHelpTelecontrolExamplesTests
{
    [Theory]
    [InlineData("pt-BR")]
    [InlineData("en")]
    [InlineData("es")]
    public void GuidesExplainEveryDeclaredSourceAndPointField(string locale)
    {
        (string Topic, CommunicationDriverTypeDescriptor Descriptor)[] drivers = [
            ("drivers.dnp3-setup", Dnp3DriverDescriptorProvider.SharedDescriptor),
            ("drivers.iec104-setup", Iec104DriverDescriptorProvider.SharedDescriptor),
            ("drivers.modbus-server-setup", ModbusTcpServerDriverDescriptorProvider.SharedDescriptor),
            ("drivers.modbus-server-setup", ModbusRtuServerDriverDescriptorProvider.SharedDescriptor)
        ];
        foreach (var (id, descriptor) in drivers)
        {
            var keys = ContextualHelpCatalog.Build(locale).Topics.Single(t => t.Id == id).Sections
                .SelectMany(s => s.Fields ?? []).Select(f => f.Key).ToHashSet();
            foreach (var field in descriptor.ConfigurationSchema.DataSourceFields.Concat(descriptor.ConfigurationSchema.TagBindingFields))
                Assert.Contains(field.Key, keys);
        }
    }

    [Theory]
    [InlineData("pt-BR")]
    [InlineData("en")]
    [InlineData("es")]
    public void ExactPublishedReadAndCommandExamplesPassActualRuntimePlanners(string locale)
    {
        (string Topic, CommunicationDriverTypeDescriptor Descriptor, ICommunicationDriverRuntimePlanner Planner)[] drivers = [
            ("drivers.dnp3-setup", Dnp3DriverDescriptorProvider.SharedDescriptor, new Dnp3CommunicationRuntimePlanner()),
            ("drivers.iec104-setup", Iec104DriverDescriptorProvider.SharedDescriptor, new Iec104CommunicationRuntimePlanner()),
            ("drivers.modbus-server-setup", ModbusTcpServerDriverDescriptorProvider.SharedDescriptor, new ModbusTcpServerCommunicationRuntimePlanner())
        ];
        foreach (var (topic, descriptor, planner) in drivers)
        {
            foreach (var (source, tag) in Examples(locale, topic, descriptor))
            {
                var result = planner.Plan(Package(source, tag), source);
                Assert.True(result.CanActivate, string.Join("; ", result.Issues.Select(i => i.Message)));
                Assert.Equal(tag.Id, Assert.Single(result.Plan!.Tags).Id);
            }
        }

        var (tcp, serialTag) = Assert.Single(Examples(locale, "drivers.modbus-server-setup", ModbusTcpServerDriverDescriptorProvider.SharedDescriptor));
        var fields = ContextualHelpCatalog.Build(locale).Topics.Single(t => t.Id == "drivers.modbus-server-setup").Sections
            .SelectMany(s => s.Fields ?? []).ToDictionary(f => f.Key, f => f.Example);
        var serial = tcp with { Driver = ModbusRtuServerDriverDescriptorProvider.DriverTypeId, Settings = new Dictionary<string, string> {
            ["serialPort"] = fields["serialPort"], ["baudRate"] = fields["baudRate"], ["dataBits"] = fields["dataBits"],
            ["parity"] = fields["parity"], ["stopBits"] = fields["stopBits"], ["frameTimeoutMilliseconds"] = fields["frameTimeoutMilliseconds"],
            ["unitId"] = fields["unitId"], ["holdingRanges"] = fields["holdingRanges"] } };
        var serialResult = new ModbusRtuServerCommunicationRuntimePlanner().Plan(Package(serial, serialTag), serial);
        Assert.True(serialResult.CanActivate, string.Join("; ", serialResult.Issues.Select(i => i.Message)));
    }

    [Theory]
    [InlineData("pt-BR")]
    [InlineData("en")]
    [InlineData("es")]
    public void InvalidPointIdentityTimersAndInputCommandsAreRejected(string locale)
    {
        var (source, tag) = Examples(locale, "drivers.dnp3-setup", Dnp3DriverDescriptorProvider.SharedDescriptor)[0];
        Assert.True(Dnp3DataSourceSettingsParser.Parse(source.Settings).Succeeded);
        var badSettings = new Dictionary<string, string>(source.Settings!) { ["responseTimeout"] = "00:00:00" };
        Assert.False(Dnp3DataSourceSettingsParser.Parse(badSettings).Succeeded);
        var inputWrite = tag with { ReadOnly = false, CommunicationBinding = tag.CommunicationBinding! with {
            Settings = new Dictionary<string, string>(tag.CommunicationBinding.Settings!) { ["writable"] = "true" } } };
        Assert.False(new Dnp3CommunicationRuntimePlanner().Plan(Package(source, inputWrite), source).CanActivate);
        Assert.True(Dnp3PortableAddress.TryParse(tag.Address, out var address));
        Assert.Equal((ushort)7, address.Index);

        var (iec, iecTag) = Examples(locale, "drivers.iec104-setup", Iec104DriverDescriptorProvider.SharedDescriptor)[0];
        Assert.True(Iec104PortablePointAddress.TryParse(iecTag.Address, out var point));
        Assert.Equal((ushort)1, point.CommonAddress);
        Assert.Equal(100, point.InformationObjectAddress);
        var invalidTimers = iec with { Settings = new Dictionary<string, string>(iec.Settings!) { ["t1Seconds"] = "10", ["t2Seconds"] = "10" } };
        Assert.False(new Iec104CommunicationRuntimePlanner().Plan(Package(invalidTimers, iecTag), invalidTimers).CanActivate);
        var wrongStation = iec with { Settings = new Dictionary<string, string>(iec.Settings!) { ["commonAddresses"] = "2" } };
        Assert.False(new Iec104CommunicationRuntimePlanner().Plan(Package(wrongStation, iecTag), wrongStation).CanActivate);
    }

    [Theory]
    [InlineData("pt-BR")]
    [InlineData("en")]
    [InlineData("es")]
    public async Task PublishedServerMapStartsAtZeroThenPublishesActualInternalValueAndRejectsExternalWrites(string locale)
    {
        var guide = ContextualHelpCatalog.Build(locale).Topics.Single(t => t.Id == "drivers.modbus-server-setup");
        using var doc = JsonDocument.Parse(Assert.Single(guide.Sections, s => s.Code is not null).Code!);
        var root = doc.RootElement;
        var (source, dto) = Assert.Single(Examples(locale, "drivers.modbus-server-setup", ModbusTcpServerDriverDescriptorProvider.SharedDescriptor));
        var result = new ModbusTcpServerCommunicationRuntimePlanner().Plan(Package(source, dto), source);
        Assert.True(result.CanActivate, string.Join("; ", result.Issues.Select(i => i.Message)));
        var plan = Assert.IsType<ModbusTcpServerCommunicationRuntimePlan>(result.Plan);
        var point = Assert.Single(plan.Points);
        var map = new ModbusServerRegisterMap(plan.Ranges, plan.Points);
        Assert.Equal(new ushort[] { 0 }, await map.ReadAsync(point.Point.Address, 1));
        await map.WriteInternalAsync(point.Point.Tag.Id, (short)root.GetProperty("internalValue").GetInt32());
        Assert.Equal(new ushort[] { 123 }, await map.ReadAsync(point.Point.Address, 1));
        var rejected = await Assert.ThrowsAsync<ModbusServerRequestException>(async () => await map.WriteClientAsync(point.Point.Address, new ushort[] { 99 }));
        Assert.Equal(ModbusServerRequestFailure.ReadOnly, rejected.Failure);
        Assert.Equal(new ushort[] { 123 }, await map.ReadAsync(point.Point.Address, 1));
        var reopened = new ModbusServerRegisterMap(plan.Ranges, plan.Points);
        Assert.Equal(new ushort[] { 0 }, await reopened.ReadAsync(point.Point.Address, 1));
        Assert.False(ModbusServerRangeCodec.TryParse("v1:0-9;5-20", out _, out _));
        var twoWords = point.Point with { Tag = TagDefinition.Create("Wide", "Plant.Wide", TagDataType.Int32, readOnly: false), ValueType = ModbusValueType.Int32 };
        var wideMap = new ModbusServerRegisterMap(plan.Ranges, [new ModbusServerPoint(twoWords, ModbusServerClientAccess.ReadWrite)]);
        await Assert.ThrowsAsync<ModbusServerRequestException>(async () => await wideMap.WriteClientAsync(twoWords.Address, new ushort[] { 1 }));
    }

    private static List<(DataSourceEngineeringDto Source, TagEngineeringDto Tag)> Examples(string locale, string topic, CommunicationDriverTypeDescriptor descriptor)
    {
        var result = new List<(DataSourceEngineeringDto, TagEngineeringDto)>();
        foreach (var section in ContextualHelpCatalog.Build(locale).Topics.Single(t => t.Id == topic).Sections.Where(s => s.Code is not null))
        {
            using var json = JsonDocument.Parse(section.Code!);
            var root = json.RootElement;
            static Dictionary<string, string> Settings(JsonElement v) => v.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()!);
            var source = new DataSourceEngineeringDto(Guid.NewGuid(), "protocol.example", "Protocol example", descriptor.DriverType, Settings: Settings(root.GetProperty("source")));
            var address = root.GetProperty("address").GetString()!;
            var binding = root.TryGetProperty("binding", out var b) ? new CommunicationTagBinding(1,
                descriptor.TagBindingSchemaId ?? descriptor.ConfigurationSchema.SchemaId,
                descriptor.TagBindingSchemaVersion ?? descriptor.ConfigurationSchema.SchemaVersion, address, Settings: Settings(b)) : null;
            var metadata = root.TryGetProperty("metadata", out var m) ? Settings(m) : null;
            var tag = new TagEngineeringDto(Guid.NewGuid(), "Point", "Plant.Protocol.Point", Enum.Parse<TagDataType>(root.GetProperty("tagDataType").GetString()!),
                Source: source.Key, Address: address, ReadOnly: root.GetProperty("readOnly").GetBoolean(), Metadata: metadata, CommunicationBinding: binding, DataSourceId: source.Id);
            result.Add((source, tag));
        }
        return result;
    }

    private static EngineeringPackage Package(DataSourceEngineeringDto source, TagEngineeringDto tag) => new(
        EngineeringExchangeService.CurrentSchema, EngineeringExchangeService.CurrentSchemaVersion, DateTimeOffset.UtcNow, [tag], [], DataSources: [source]);
}
