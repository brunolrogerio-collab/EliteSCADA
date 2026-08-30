using Scada.Drivers.Abstractions;

namespace Scada.Drivers.Mqtt;

public sealed class MqttDriverDescriptorProvider : ICommunicationDriverDescriptorProvider
{
    public const string DriverType = "mqtt.raw";
    public const string SchemaId = "elitescada.driver.mqtt.raw";

    public CommunicationDriverTypeDescriptor Descriptor { get; } = new(
        DriverType,
        "MQTT Raw",
        DriverContractVersion: 1,
        RuntimeCapabilities: DriverCapabilities.Read |
                             DriverCapabilities.Write |
                             DriverCapabilities.Subscribe |
                             DriverCapabilities.Diagnostics |
                             DriverCapabilities.SourceTimestamp,
        EngineeringCapabilities: DriverEngineeringCapabilities.None,
        AcquisitionModes: [DriverAcquisitionMode.EventDriven],
        ConfigurationSchema: new DriverConfigurationSchemaDescriptor(
            SchemaId,
            SchemaVersion: 1,
            DataSourceFields:
            [
                new("host", DriverConfigurationValueKind.Host, Required: true, DisplayName: "Broker host"),
                new("port", DriverConfigurationValueKind.Port, DefaultValue: "8883", Minimum: 1, Maximum: 65535),
                new("tls", DriverConfigurationValueKind.Boolean, DefaultValue: "true"),
                new("clientId", DriverConfigurationValueKind.Identifier, Required: true, DisplayName: "Client ID"),
                new("protocolVersion", DriverConfigurationValueKind.Enum, DefaultValue: "mqtt5", AllowedValues: ["mqtt5", "mqtt311"]),
                new("username", DriverConfigurationValueKind.String),
                new("password", DriverConfigurationValueKind.SecretReference, DisplayName: "Password secret reference"),
                new("keepAliveSeconds", DriverConfigurationValueKind.Integer, DefaultValue: "30", Minimum: 1, Maximum: 65535),
                new("connectTimeoutMilliseconds", DriverConfigurationValueKind.Integer, DefaultValue: "10000", Minimum: 100, Maximum: 300000),
                new("reconnectMinimumMilliseconds", DriverConfigurationValueKind.Integer, DefaultValue: "1000", Minimum: 100, Maximum: 300000),
                new("reconnectMaximumMilliseconds", DriverConfigurationValueKind.Integer, DefaultValue: "30000", Minimum: 100, Maximum: 3600000),
                new("mqtt311.cleanSession", DriverConfigurationValueKind.Boolean, DefaultValue: "false", Advanced: true),
                new("mqtt5.cleanStart", DriverConfigurationValueKind.Boolean, DefaultValue: "false", Advanced: true),
                new("mqtt5.sessionExpirySeconds", DriverConfigurationValueKind.Integer, DefaultValue: "3600", Minimum: 0, Maximum: uint.MaxValue, Advanced: true),
                new("maximumInboundPayloadBytes", DriverConfigurationValueKind.Integer, DefaultValue: "1048576", Minimum: 1, Maximum: MqttConnectionSettings.MaximumAllowedInboundPayloadBytes, Advanced: true),
                new("maximumConsecutiveConnectFailures", DriverConfigurationValueKind.Integer, DefaultValue: "5", Minimum: 1, Maximum: 1000, Advanced: true),
                new("maximumBufferedMessages", DriverConfigurationValueKind.Integer, DefaultValue: "4096", Minimum: 1, Maximum: 1000000, Advanced: true)
            ],
            TagBindingFields:
            [
                new("address", DriverConfigurationValueKind.String, Required: true, DisplayName: "Subscribe topic", Description: "Exact MQTT topic stored in TAG Address. Wildcards are not authoritative TAG identity."),
                new("mqtt.payloadFormat", DriverConfigurationValueKind.Enum, DefaultValue: "utf8Scalar", AllowedValues: ["utf8Scalar", "json"]),
                new("mqtt.jsonPointer", DriverConfigurationValueKind.String),
                new("mqtt.sourceTimestampJsonPointer", DriverConfigurationValueKind.String),
                new("mqtt.sourceTimestampRequired", DriverConfigurationValueKind.Boolean, DefaultValue: "false"),
                new("mqtt.freshnessTimeoutMilliseconds", DriverConfigurationValueKind.Integer, Minimum: 1, Maximum: int.MaxValue, Description: "Optional TAG freshness timeout. A valid sample becomes Stale when no fresher sample arrives before this interval."),
                new("mqtt.retainedValuePolicy", DriverConfigurationValueKind.Enum, DefaultValue: "staleWithoutSourceTimestamp", AllowedValues: ["staleWithoutSourceTimestamp", "acceptAsCurrent"]),
                new("mqtt.qos", DriverConfigurationValueKind.Enum, DefaultValue: "1", AllowedValues: ["0", "1", "2"]),
                new("mqtt.publishTopic", DriverConfigurationValueKind.String),
                new("mqtt.publishQos", DriverConfigurationValueKind.Enum, DefaultValue: "1", AllowedValues: ["0", "1", "2"]),
                new("mqtt.publishRetain", DriverConfigurationValueKind.Boolean, DefaultValue: "false")
            ]),
        Description: "Event-driven raw MQTT 5.0/3.1.1 industrial data source with exact Topic-to-TAG mappings.");
}
