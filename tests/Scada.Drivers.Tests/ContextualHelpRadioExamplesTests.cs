using System.Text;
using System.Text.Json;
using Scada.Api.Runtime;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.DriverHost.Sidecars;
using Scada.Drivers.Abstractions;
using Scada.Drivers.Zigbee2Mqtt;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;

namespace Scada.Drivers.Tests;

public sealed class ContextualHelpRadioExamplesTests
{
    [Theory]
    [InlineData("pt-BR")]
    [InlineData("en")]
    [InlineData("es")]
    public void RadioGuidesCoverEveryDeclaredSourceAndTagBindingField(string locale)
    {
        var catalog = ContextualHelpCatalog.Build(locale);
        (string Id, CommunicationDriverTypeDescriptor Descriptor)[] drivers = [
            ("drivers.zigbee-native-setup", new ZigbeeNativeDriverDescriptorProvider().Descriptor),
            ("drivers.zwave-setup", new ZWaveJsDriverDescriptorProvider().Descriptor),
            ("drivers.zigbee2mqtt-setup", new Zigbee2MqttDriverDescriptorProvider().Descriptor)
        ];
        foreach (var (id, descriptor) in drivers)
        {
            var guide = catalog.Topics.Single(t => t.Id == id);
            var keys = guide.Sections.SelectMany(s => s.Fields ?? []).Select(f => f.Key).ToHashSet();
            foreach (var field in descriptor.ConfigurationSchema.DataSourceFields.Concat(descriptor.ConfigurationSchema.TagBindingFields))
                Assert.Contains(field.Key, keys);
        }
    }

    [Theory]
    [InlineData("pt-BR")]
    [InlineData("en")]
    [InlineData("es")]
    public void PublishedRadioIdentitiesPassActualPlannersWhileInvalidAssistantShapesRemainRejected(string locale)
    {
        var catalog = ContextualHelpCatalog.Build(locale);
        var guide = catalog.Topics.Single(t => t.Id == "drivers.zigbee-native-setup");
        var settings = guide.Sections.SelectMany(s => s.Fields ?? []).Where(f =>
                new[] { "hostResourceId", "networkIdentity", "panId", "extendedPanId", "channel", "baudRate" }.Contains(f.Key))
            .ToDictionary(f => f.Key, f => f.Example);
        using var example = JsonDocument.Parse(Assert.Single(guide.Sections, s => s.Code is not null).Code!);
        var address = example.RootElement.GetProperty("address").GetString()!;
        var source = new DataSourceEngineeringDto(Guid.NewGuid(), "zigbee.demo", "Zigbee example", ZigbeeNativeDriverDescriptorProvider.DriverType,
            Settings: settings, SecretReferences: new Dictionary<string, string> { ["networkKey"] = HostMaterialSlotReference.Marker });
        var binding = new CommunicationTagBinding(1, ZigbeeNativeDriverDescriptorProvider.TagBindingSchemaId, 1, address);
        var tag = new TagEngineeringDto(Guid.NewGuid(), "Switch", "Home.Switch", Enum.Parse<TagDataType>(example.RootElement.GetProperty("dataType").GetString()!),
            Source: source.Key, Address: address, ReadOnly: true, CommunicationBinding: binding, DataSourceId: source.Id);
        var package = Package(source, tag);
        var planner = new ZigbeeNativeCommunicationRuntimePlanner();
        var plan = planner.Plan(package, source);
        Assert.True(plan.CanActivate, string.Join(";", plan.Issues.Select(i => i.Message)));
        var point = Assert.Single(Assert.IsType<ZigbeeNativeCommunicationRuntimePlan>(plan.Plan).Points);
        Assert.Equal(example.RootElement.GetProperty("ieeeAddress").GetString(), point.IeeeAddress);
        Assert.Equal(example.RootElement.GetProperty("endpoint").GetInt32(), point.Endpoint);
        var extra = new Dictionary<string, string> { ["ieeeAddress"] = point.IeeeAddress, ["endpoint"] = "1", ["cluster"] = "genOnOff", ["attribute"] = "onOff" };
        Assert.Contains(planner.Plan(package with { Tags = [tag with { CommunicationBinding = binding with { Settings = extra } }] }, source).Issues,
            i => i.Code == "ZIGBEE_NATIVE_TAG_BINDING_SETTINGS_UNSUPPORTED" && i.IsError);

        var zwave = catalog.Topics.Single(t => t.Id == "drivers.zwave-setup");
        using var zexample = JsonDocument.Parse(Assert.Single(zwave.Sections, s => s.Code is not null).Code!);
        var e = zexample.RootElement;
        var zsource = new DataSourceEngineeringDto(Guid.NewGuid(), "zwave.demo", "Z-Wave example", ZWaveJsDriverDescriptorProvider.DriverType,
            Settings: new Dictionary<string, string> { ["hostResourceId"] = "radio.zwave1" });
        var zaddress = e.GetProperty("address").GetString()!;
        var ztag = new TagEngineeringDto(Guid.NewGuid(), "Switch", "Home.ZWaveSwitch", Enum.Parse<TagDataType>(e.GetProperty("dataType").GetString()!),
            Source: zsource.Key, Address: zaddress, ReadOnly: e.GetProperty("readOnly").GetBoolean(),
            CommunicationBinding: new CommunicationTagBinding(1, ZWaveJsDriverDescriptorProvider.TagBindingSchemaId, 1, zaddress), DataSourceId: zsource.Id);
        var zp = new ZWaveJsCommunicationRuntimePlanner();
        var zplan = zp.Plan(Package(zsource, ztag), zsource);
        Assert.True(zplan.CanActivate, string.Join(";", zplan.Issues.Select(i => i.Message)));
        var zpoint = Assert.Single(Assert.IsType<ZWaveJsCommunicationRuntimePlan>(zplan.Plan).Points);
        Assert.Equal(e.GetProperty("nodeId").GetInt32(), zpoint.NodeId);
        Assert.Equal(e.GetProperty("commandClass").GetInt32(), zpoint.CommandClass);
        Assert.Equal(e.GetProperty("property").GetString(), zpoint.Property);
        Assert.False(zpoint.Writable);
        Assert.Contains(zp.Plan(Package(zsource, ztag with { CommunicationBinding = null }), zsource).Issues,
            i => i.Code == "ZWAVE_JS_TAG_BINDING_REQUIRED" && i.IsError);
    }

    [Theory]
    [InlineData("pt-BR")]
    [InlineData("en")]
    [InlineData("es")]
    public void PublishedInventoryDocumentsCurrentFormatLimitationAndInternalMaterializerKeepsSourceOwnedIds(string locale)
    {
        var guide = ContextualHelpCatalog.Build(locale).Topics.Single(t => t.Id == "drivers.zigbee2mqtt-setup");
        var code = guide.Sections.Where(s => s.Code is not null).Select(s => s.Code!).Single(s => s.TrimStart().StartsWith('['));
        var sourceId = Guid.NewGuid();
        var fields = guide.Sections.SelectMany(s => s.Fields ?? []).ToDictionary(f => f.Key, f => f.Example);
        var settings = Zigbee2MqttConnectionSettings.Parse("z2m.demo", sourceId, new Dictionary<string, string> { ["host"] = fields["host"], ["baseTopic"] = fields["baseTopic"] });
        var inventory = Zigbee2MqttExposeMapper.ParseInventory(Encoding.UTF8.GetBytes(code), settings, "online");
        var device = Assert.Single(inventory.Devices);
        // Documentation characterization of #787, not an acceptance test for interoperability.
        // Fixing the decoder must update both this assertion and the marked Help limitation.
        Assert.Empty(device.Exposes);
        Assert.Empty(Zigbee2MqttExposeMapper.BuildMaterialization(sourceId, "z2m.demo", device).Tags!);
        // Isolate the existing materializer from the decoder defect only inside this component test.
        // This flattened diagnostic input is not an operational workaround or a published example.
        var diagnosticInput = System.Text.Json.Nodes.JsonNode.Parse(code)!.AsArray();
        diagnosticInput[0]!["exposes"] = diagnosticInput[0]!["definition"]!["exposes"]!.DeepClone();
        var internalDevice = Assert.Single(Zigbee2MqttExposeMapper.ParseInventory(
            Encoding.UTF8.GetBytes(diagnosticInput.ToJsonString()), settings, "online").Devices);
        var materialized = Zigbee2MqttExposeMapper.BuildMaterialization(sourceId, "z2m.demo", internalDevice);
        Assert.Equal(2, materialized.Tags!.Count);
        foreach (var generated in materialized.Tags)
        {
            Assert.True(Zigbee2MqttIdentity.TryParsePortableAddress(generated.PortableAddress, out var ieee, out var endpoint, out var property));
            Assert.Equal(device.IeeeAddress, ieee);
            Assert.Equal(sourceId, Guid.Parse(generated.Metadata!["z2m.dataSourceId"]));
            Assert.Equal(Zigbee2MqttIdentity.StablePointIdentity(sourceId, ieee, endpoint, property), generated.Metadata["z2m.identity"]);
            Assert.NotEqual(Zigbee2MqttIdentity.StablePointIdentity(Guid.NewGuid(), ieee, endpoint, property), generated.Metadata["z2m.identity"]);
        }
        var temperature = Assert.Single(materialized.Tags, t => t.Metadata!["z2m.property"] == "temperature");
        Assert.Equal(TagDataType.Double, temperature.DataType);
        Assert.Equal("°C", temperature.EngineeringUnit);
        Assert.True(temperature.ReadOnly);
        var state = Assert.Single(materialized.Tags, t => t.Metadata!["z2m.property"] == "state");
        Assert.Equal(TagDataType.Boolean, state.DataType);
        Assert.False(state.ReadOnly);
        Assert.Equal(Zigbee2MqttContract.ReadbackConfirmation, state.Metadata!["z2m.reportConfirmation"]);
    }

    private static EngineeringPackage Package(DataSourceEngineeringDto source, TagEngineeringDto tag) => new(
        EngineeringExchangeService.CurrentSchema, EngineeringExchangeService.CurrentSchemaVersion, DateTimeOffset.UtcNow, [tag], [], DataSources: [source]);
}
