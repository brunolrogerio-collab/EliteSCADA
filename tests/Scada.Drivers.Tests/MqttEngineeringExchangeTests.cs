using System.Text.Json;
using System.Text.Json.Serialization;
using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Drivers.Mqtt;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;

namespace Scada.Drivers.Tests;

public sealed class MqttEngineeringExchangeTests
{
    [Fact]
    public void PublicExchange_RoundTripsMqttConfigurationAcrossJsonAndCsv()
    {
        var bus = new InMemoryScadaEventBus();
        using var alarms = new InMemoryAlarmEngine(bus);
        var exchange = new EngineeringExchangeService(new InMemoryTagRegistry(), alarms);
        var dataSource = new DataSourceEngineeringDto(
            Id: Guid.NewGuid(),
            Key: "mqtt.plant",
            Name: "Plant MQTT",
            Driver: MqttDriverDescriptorProvider.DriverType,
            Settings: new Dictionary<string, string>
            {
                ["host"] = "broker.example.internal",
                ["port"] = "8883",
                ["tls"] = "true",
                ["clientId"] = "elite-plant-01",
                ["protocolVersion"] = "mqtt5",
                ["username"] = "operator",
                ["mqtt5.cleanStart"] = "false",
                ["mqtt5.sessionExpirySeconds"] = "3600"
            },
            SecretReferences: new Dictionary<string, string>
            {
                ["password"] = "secret://mqtt/plant/operator"
            });
        var tag = new TagEngineeringDto(
            Id: Guid.NewGuid(),
            Name: "Tank Level",
            Path: "Plant.Tank.Level",
            DataType: TagDataType.Double,
            Source: dataSource.Key,
            Address: "plant/tank/state",
            ReadOnly: false,
            Metadata: new Dictionary<string, string>
            {
                ["mqtt.payloadFormat"] = "json",
                ["mqtt.jsonPointer"] = "/level",
                ["mqtt.sourceTimestampJsonPointer"] = "/sourceTime",
                ["mqtt.sourceTimestampRequired"] = "true",
                ["mqtt.qos"] = "1",
                ["mqtt.publishTopic"] = "plant/tank/command",
                ["mqtt.publishQos"] = "2",
                ["mqtt.publishRetain"] = "false"
            });
        var package = new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            [tag],
            Array.Empty<AlarmEngineeringDto>(),
            [dataSource]);

        var serializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };
        var inputJson = JsonSerializer.Serialize(package, serializerOptions);
        var parsed = exchange.ParseJson(inputJson);
        var preview = exchange.Preview(parsed, ImportMode.CreateAndUpdate);

        Assert.True(preview.CanApply);
        Assert.Equal(0, preview.ErrorCount);
        var applied = exchange.Apply(parsed, ImportMode.CreateAndUpdate);
        Assert.Empty(applied.Issues);

        var exportedJson = exchange.ExportJson(indented: false);
        Assert.DoesNotContain("clear-text-password", exportedJson, StringComparison.Ordinal);
        var jsonRoundTrip = exchange.ParseJson(exportedJson);
        AssertMqttDataSource(Assert.Single(jsonRoundTrip.DataSources!));
        AssertMqttTag(Assert.Single(jsonRoundTrip.Tags));

        var dataSourcesCsv = exchange.ExportDataSourcesCsv();
        var dataSourceCsvRoundTrip = exchange.ParseDataSourcesCsv(dataSourcesCsv);
        AssertMqttDataSource(Assert.Single(dataSourceCsvRoundTrip.DataSources!));

        var tagsCsv = exchange.ExportTagsCsv();
        var tagCsvRoundTrip = exchange.ParseTagsCsv(tagsCsv);
        AssertMqttTag(Assert.Single(tagCsvRoundTrip.Tags));
    }

    [Fact]
    public void PublicExchange_PreviewRejectsPlaintextMqttSecret()
    {
        var bus = new InMemoryScadaEventBus();
        using var alarms = new InMemoryAlarmEngine(bus);
        var exchange = new EngineeringExchangeService(new InMemoryTagRegistry(), alarms);
        var unsafeSource = new DataSourceEngineeringDto(
            Id: null,
            Key: "mqtt.unsafe",
            Name: "Unsafe MQTT",
            Driver: MqttDriverDescriptorProvider.DriverType,
            Settings: new Dictionary<string, string>
            {
                ["host"] = "broker.local",
                ["clientId"] = "elite-unsafe",
                ["username"] = "operator",
                ["password"] = "clear-text-password"
            });
        var package = new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            Array.Empty<TagEngineeringDto>(),
            Array.Empty<AlarmEngineeringDto>(),
            [unsafeSource]);

        var preview = exchange.Preview(package, ImportMode.CreateAndUpdate);

        Assert.False(preview.CanApply);
        Assert.Contains(preview.Items.SelectMany(item => item.Issues), issue =>
            issue.Code == "DATASOURCE_PLAINTEXT_SECRET" && issue.IsError);
    }

    private static void AssertMqttDataSource(DataSourceEngineeringDto dataSource)
    {
        Assert.Equal("mqtt.plant", dataSource.Key);
        Assert.Equal(MqttDriverDescriptorProvider.DriverType, dataSource.Driver);
        Assert.NotNull(dataSource.Settings);
        Assert.Equal("broker.example.internal", dataSource.Settings!["host"]);
        Assert.Equal("mqtt5", dataSource.Settings["protocolVersion"]);
        Assert.Equal("operator", dataSource.Settings["username"]);
        Assert.False(dataSource.Settings.ContainsKey("password"));
        Assert.NotNull(dataSource.SecretReferences);
        Assert.Equal("secret://mqtt/plant/operator", dataSource.SecretReferences!["password"]);
    }

    private static void AssertMqttTag(TagEngineeringDto tag)
    {
        Assert.Equal("Plant.Tank.Level", tag.Path);
        Assert.Equal("mqtt.plant", tag.Source);
        Assert.Equal("plant/tank/state", tag.Address);
        Assert.False(tag.ReadOnly);
        Assert.NotNull(tag.Metadata);
        Assert.Equal("json", tag.Metadata!["mqtt.payloadFormat"]);
        Assert.Equal("/level", tag.Metadata["mqtt.jsonPointer"]);
        Assert.Equal("/sourceTime", tag.Metadata["mqtt.sourceTimestampJsonPointer"]);
        Assert.Equal("plant/tank/command", tag.Metadata["mqtt.publishTopic"]);
        Assert.Equal("2", tag.Metadata["mqtt.publishQos"]);
    }
}
