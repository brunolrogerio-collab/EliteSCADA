using Scada.Core.Alarms;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.Drivers.Abstractions;
using Scada.Drivers.Mqtt;
using Scada.Engineering.Contracts;

namespace Scada.Drivers.Tests;

public sealed class MqttEngineeringCompilerTests
{
    [Fact]
    public void Compile_ProducesEventDrivenRuntimePlanFromCanonicalEngineering()
    {
        var dataSource = new DataSourceEngineeringDto(
            Id: Guid.NewGuid(),
            Key: "mqtt.plant",
            Name: "Plant MQTT",
            Driver: MqttDriverDescriptorProvider.DriverType,
            Settings: new Dictionary<string, string>
            {
                ["host"] = "broker.example.internal",
                ["port"] = "1883",
                ["tls"] = "false",
                ["clientId"] = "elite-plant-01",
                ["protocolVersion"] = "mqtt311",
                ["mqtt311.cleanSession"] = "false",
                ["keepAliveSeconds"] = "45"
            });

        var level = Tag(
            "Level",
            "Plant.Tank.Level",
            TagDataType.Double,
            dataSource.Key,
            "plant/tank/state",
            metadata: new Dictionary<string, string>
            {
                ["mqtt.payloadFormat"] = "json",
                ["mqtt.jsonPointer"] = "/level",
                ["mqtt.sourceTimestampJsonPointer"] = "/timestamp",
                ["mqtt.sourceTimestampRequired"] = "true",
                ["mqtt.qos"] = "2"
            });
        var setpoint = Tag(
            "Setpoint",
            "Plant.Tank.Setpoint",
            TagDataType.Int32,
            dataSource.Key,
            "plant/tank/setpoint/readback",
            readOnly: false,
            metadata: new Dictionary<string, string>
            {
                ["mqtt.publishTopic"] = "plant/tank/setpoint/command",
                ["mqtt.publishQos"] = "1"
            });

        var result = new MqttEngineeringCompiler().Compile(Package([level, setpoint], [dataSource]));

        Assert.True(result.CanActivate);
        Assert.DoesNotContain(result.Issues, issue => issue.IsError);
        var plan = Assert.Single(result.Plans);
        Assert.Equal(dataSource.Key, plan.DataSourceKey);
        Assert.Equal("mqtt.raw:mqtt.plant", plan.DriverId);
        Assert.Equal("broker.example.internal", plan.Connection.Host);
        Assert.Equal(1883, plan.Connection.Port);
        Assert.False(plan.Connection.UseTls);
        Assert.Equal(MqttProtocolMode.Mqtt311, plan.Connection.ProtocolMode);
        Assert.Equal(TimeSpan.FromSeconds(45), plan.Connection.EffectiveKeepAlive);
        Assert.False(plan.Connection.CleanSession);
        Assert.Null(plan.PasswordSecretReference);

        var levelPoint = Assert.Single(plan.Points, point => point.Tag.Path == level.Path);
        Assert.Equal(MqttPayloadFormat.Json, levelPoint.PayloadFormat);
        Assert.Equal("/level", levelPoint.JsonPointer);
        Assert.Equal("/timestamp", levelPoint.SourceTimestampJsonPointer);
        Assert.True(levelPoint.SourceTimestampRequired);
        Assert.Equal(MqttQosLevel.ExactlyOnce, levelPoint.Qos);

        var setpointPoint = Assert.Single(plan.Points, point => point.Tag.Path == setpoint.Path);
        Assert.True(setpointPoint.Writable);
        Assert.Equal("plant/tank/setpoint/command", setpointPoint.PublishTopic);
        Assert.Equal(MqttQosLevel.AtLeastOnce, setpointPoint.PublishQos);
    }

    [Fact]
    public void Compile_PreservesSecretReferenceAndRejectsPlaintextPassword()
    {
        var safe = new DataSourceEngineeringDto(
            Id: null,
            Key: "mqtt.safe",
            Name: "Safe MQTT",
            Driver: MqttDriverDescriptorProvider.DriverType,
            Settings: new Dictionary<string, string>
            {
                ["host"] = "broker.local",
                ["clientId"] = "elite-safe",
                ["username"] = "operator"
            },
            SecretReferences: new Dictionary<string, string>
            {
                ["password"] = "secret://mqtt/plant/operator"
            });
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
                ["password"] = "plaintext-is-not-a-secret"
            });

        var result = new MqttEngineeringCompiler().Compile(Package(
            [
                Tag("Safe", "Plant.Safe", TagDataType.Boolean, safe.Key, "plant/safe"),
                Tag("Unsafe", "Plant.Unsafe", TagDataType.Boolean, unsafeSource.Key, "plant/unsafe")
            ],
            [safe, unsafeSource]));

        Assert.False(result.CanActivate);
        var safePlan = Assert.Single(result.Plans);
        Assert.Equal("secret://mqtt/plant/operator", safePlan.PasswordSecretReference);
        Assert.Equal("operator", safePlan.Username);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "MQTT_PLAINTEXT_SECRET_REJECTED" &&
            issue.DataSourceKey == unsafeSource.Key &&
            issue.IsError);
        Assert.DoesNotContain(result.Plans, plan => plan.DataSourceKey == unsafeSource.Key);
    }

    [Fact]
    public void Compile_RejectsProtocolSpecificSettingsAndUnsafePointMappings()
    {
        var dataSource = new DataSourceEngineeringDto(
            Id: null,
            Key: "mqtt.invalid",
            Name: "Invalid MQTT",
            Driver: MqttDriverDescriptorProvider.DriverType,
            Settings: new Dictionary<string, string>
            {
                ["host"] = "broker.local",
                ["clientId"] = "elite-invalid",
                ["protocolVersion"] = "mqtt311",
                ["mqtt5.cleanStart"] = "true"
            });

        var wildcard = Tag(
            "Wildcard",
            "Plant.Wildcard",
            TagDataType.Double,
            dataSource.Key,
            "plant/+/value");
        var writableWithoutPublishTopic = Tag(
            "Command",
            "Plant.Command",
            TagDataType.Int32,
            dataSource.Key,
            "plant/command/readback",
            readOnly: false);
        var invalidJsonPointer = Tag(
            "Json",
            "Plant.Json",
            TagDataType.Double,
            dataSource.Key,
            "plant/json",
            metadata: new Dictionary<string, string>
            {
                ["mqtt.payloadFormat"] = "json",
                ["mqtt.jsonPointer"] = "value"
            });

        var result = new MqttEngineeringCompiler().Compile(Package(
            [wildcard, writableWithoutPublishTopic, invalidJsonPointer],
            [dataSource]));

        Assert.False(result.CanActivate);
        Assert.Empty(result.Plans);
        Assert.Contains(result.Issues, issue => issue.Code == "MQTT_PROTOCOL_SETTING_MISMATCH");
        Assert.Contains(result.Issues, issue => issue.Code == "MQTT_TAG_CONFIGURATION_INVALID" && issue.TagPath == wildcard.Path);
        Assert.Contains(result.Issues, issue => issue.Code == "MQTT_TAG_CONFIGURATION_INVALID" && issue.TagPath == writableWithoutPublishTopic.Path);
        Assert.Contains(result.Issues, issue => issue.Code == "MQTT_TAG_CONFIGURATION_INVALID" && issue.TagPath == invalidJsonPointer.Path);
    }

    [Fact]
    public void Compile_OnlyConsumesEnabledMqttDataSources()
    {
        var disabledMqtt = new DataSourceEngineeringDto(
            null,
            "mqtt.disabled",
            "Disabled MQTT",
            MqttDriverDescriptorProvider.DriverType,
            Enabled: false,
            Settings: new Dictionary<string, string>());
        var modbus = new DataSourceEngineeringDto(
            null,
            "plc.main",
            "PLC",
            "modbus.tcp",
            Settings: new Dictionary<string, string>());

        var result = new MqttEngineeringCompiler().Compile(Package([], [disabledMqtt, modbus]));

        Assert.True(result.CanActivate);
        Assert.Empty(result.Plans);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void Descriptor_DeclaresOnlyImplementedCapabilitiesAndCanonicalFields()
    {
        var descriptor = new MqttDriverDescriptorProvider().Descriptor;

        Assert.Equal("mqtt.raw", descriptor.DriverType);
        Assert.Equal("elitescada.driver.mqtt.raw", descriptor.ConfigurationSchema.SchemaId);
        Assert.Equal(DriverEngineeringCapabilities.None, descriptor.EngineeringCapabilities);
        Assert.Single(descriptor.AcquisitionModes);
        Assert.Equal(DriverAcquisitionMode.EventDriven, descriptor.AcquisitionModes.Single());
        Assert.True(descriptor.RuntimeCapabilities.HasFlag(DriverCapabilities.Read));
        Assert.True(descriptor.RuntimeCapabilities.HasFlag(DriverCapabilities.Write));
        Assert.True(descriptor.RuntimeCapabilities.HasFlag(DriverCapabilities.Subscribe));
        Assert.True(descriptor.RuntimeCapabilities.HasFlag(DriverCapabilities.Diagnostics));
        Assert.True(descriptor.RuntimeCapabilities.HasFlag(DriverCapabilities.SourceTimestamp));

        var password = Assert.Single(descriptor.ConfigurationSchema.DataSourceFields, field => field.Key == "password");
        Assert.Equal(DriverConfigurationValueKind.SecretReference, password.ValueKind);
        var address = Assert.Single(descriptor.ConfigurationSchema.TagBindingFields, field => field.Key == "address");
        Assert.True(address.Required);
        Assert.NotNull(address.Description);
        Assert.Contains("Exact MQTT topic", address.Description!, StringComparison.Ordinal);
    }

    private static TagEngineeringDto Tag(
        string name,
        string path,
        TagDataType type,
        string source,
        string address,
        bool readOnly = true,
        Dictionary<string, string>? metadata = null) => new(
            Id: Guid.NewGuid(),
            Name: name,
            Path: path,
            DataType: type,
            Source: source,
            Address: address,
            ReadOnly: readOnly,
            Metadata: metadata);

    private static EngineeringPackage Package(
        IReadOnlyCollection<TagEngineeringDto> tags,
        IReadOnlyCollection<DataSourceEngineeringDto> dataSources) => new(
            Schema: "scada.engineering",
            SchemaVersion: 5,
            ExportedAt: DateTimeOffset.UtcNow,
            Tags: tags,
            Alarms: Array.Empty<AlarmEngineeringDto>(),
            DataSources: dataSources);
}
