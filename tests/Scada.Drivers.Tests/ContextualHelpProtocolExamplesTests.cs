using System.Globalization;
using System.Text;
using System.Text.Json;
using Scada.Api.Runtime;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;
using Scada.Drivers.Modbus;
using Scada.Drivers.Mqtt;
using Scada.Drivers.OpcUa;

namespace Scada.Drivers.Tests;

public sealed class ContextualHelpProtocolExamplesTests
{
    [Theory]
    [InlineData("pt-BR")]
    [InlineData("en")]
    [InlineData("es")]
    public void ProtocolGuidesExplainEveryDeclaredConnectionAndBindingField(string locale)
    {
        var catalog = ContextualHelpCatalog.Build(locale);
        (string Topic, CommunicationDriverTypeDescriptor Descriptor)[] drivers = [
            ("drivers.modbus-setup", new ModbusTcpDriverDescriptorProvider().Descriptor),
            ("drivers.modbus-setup", new ModbusRtuDriverDescriptorProvider().Descriptor),
            ("drivers.mqtt-setup", new MqttDriverDescriptorProvider().Descriptor),
            ("drivers.opcua-setup", new OpcUaDriverDescriptorProvider().Descriptor)
        ];
        foreach (var (id, descriptor) in drivers)
        {
            var topic = catalog.Topics.Single(t => t.Id == id);
            var keys = topic.Sections.SelectMany(s => s.Fields ?? []).Select(f => f.Key).ToHashSet();
            foreach (var field in descriptor.ConfigurationSchema.DataSourceFields.Concat(descriptor.ConfigurationSchema.TagBindingFields))
                Assert.Contains(field.Key, keys);
        }
    }

    [Theory]
    [InlineData("pt-BR")]
    [InlineData("en")]
    [InlineData("es")]
    public void PublishedMqttPayloadDecodesAndDocumentedReadOnlyJsonBoundaryIsEnforced(string locale)
    {
        var guide = ContextualHelpCatalog.Build(locale).Topics.Single(t => t.Id == "drivers.mqtt-setup");
        string Example(string key) => guide.Sections.SelectMany(s => s.Fields ?? []).Single(f => f.Key == key).Example;
        var payload = Assert.Single(guide.Sections, s => s.Code is not null).Code!;
        var tag = TagDefinition.Create("Current", "Plant.Pump.Current", TagDataType.Double, readOnly: false);
        var point = new MqttPoint(tag, Example("address"), MqttPayloadFormat.Json,
            Example("mqtt.jsonPointer"), Example("mqtt.sourceTimestampJsonPointer"),
            SourceTimestampRequired: bool.Parse(Example("mqtt.sourceTimestampRequired")),
            FreshnessTimeout: TimeSpan.FromMilliseconds(double.Parse(Example("mqtt.freshnessTimeoutMilliseconds"), CultureInfo.InvariantCulture)));
        var receivedAt = DateTimeOffset.Parse("2026-10-25T12:00:00Z", CultureInfo.InvariantCulture);
        var decoded = MqttPayloadCodec.Decode(point, Encoding.UTF8.GetBytes(payload), true, receivedAt);
        Assert.Equal(12.5, Assert.IsType<double>(decoded.Value));
        Assert.Equal(receivedAt, decoded.SourceTimestamp);
        Assert.Equal(TagQuality.Good, decoded.Quality);
        var noTimestamp = point with { SourceTimestampJsonPointer = null, SourceTimestampRequired = false };
        Assert.Equal(TagQuality.Stale, MqttPayloadCodec.Decode(noTimestamp, Encoding.UTF8.GetBytes(payload), true, receivedAt).Quality);
        Assert.Throws<MqttPayloadException>(() => MqttPayloadCodec.Decode(point, Encoding.UTF8.GetBytes("{\"current\":12.5}"), false, receivedAt));
        Assert.Throws<InvalidOperationException>(() => MqttPayloadCodec.Encode(point with { Writable = true, PublishTopic = "plant/pump01/set" }, 12.5));
        Assert.Throws<ArgumentException>(() => (point with { SubscribeTopic = "plant/+/state" }).Validate());
    }

    [Theory]
    [InlineData("pt-BR")]
    [InlineData("en")]
    [InlineData("es")]
    public void PublishedModbusConversionAndOpcUaNamespaceExampleUseActualCodecs(string locale)
    {
        var catalog = ContextualHelpCatalog.Build(locale);
        JsonDocument Example(string id) => JsonDocument.Parse(Assert.Single(catalog.Topics.Single(t => t.Id == id).Sections, s => s.Code is not null).Code!);
        using var modbus = Example("drivers.modbus-setup");
        var m = modbus.RootElement;
        Assert.True(ModbusTagAddressCodec.TryParse(m.GetProperty("address").GetString(), null, out var area, out var address, out var error), error);
        var point = new ModbusPoint(TagDefinition.Create("Pressure", "Plant.Pressure", TagDataType.Double),
            1, area, address, Enum.Parse<ModbusValueType>(m.GetProperty("wireType").GetString()!),
            Scale: m.GetProperty("scale").GetDouble(), Offset: m.GetProperty("offset").GetDouble());
        Assert.Equal(m.GetProperty("expected").GetDouble(), Assert.IsType<double>(ModbusValueCodec.DecodeRegisters(point, [m.GetProperty("raw").GetUInt16()])), 8);
        using var opcua = Example("drivers.opcua-setup");
        var u = opcua.RootElement;
        var identity = new OpcUaNodeIdentity(u.GetProperty("nodeId").GetString()!, u.GetProperty("namespaceUri").GetString());
        Assert.Equal(identity, OpcUaNodeIdentity.ParsePortableAddress(identity.PortableAddress));
        Assert.Equal(identity.StableIdentity, new OpcUaNodeIdentity(identity.NodeId.Replace("ns=2;", "ns=4;"), identity.NamespaceUri).StableIdentity);
        Assert.NotEqual(identity.StableIdentity, new OpcUaNodeIdentity(identity.NodeId, "urn:another:plant").StableIdentity);
    }
}
