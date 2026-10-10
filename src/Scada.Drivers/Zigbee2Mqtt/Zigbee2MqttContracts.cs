using Scada.Core.Tags;
using Scada.Drivers.Abstractions;
using Scada.Drivers.Mqtt;

namespace Scada.Drivers.Zigbee2Mqtt;

public static class Zigbee2MqttContract
{
    public const string DriverType = "zigbee2mqtt.bridge";
    public const string SchemaId = "elitescada.driver.zigbee2mqtt.bridge";
    public const string TagBindingSchemaId = "elitescada.tag.zigbee2mqtt.bridge";
    public const int SchemaVersion = 1;
    public const int TagBindingSchemaVersion = 1;
    public const string PasswordSecretReferenceKey = "password";
    public const string PasswordPurpose = "zigbee2mqtt.password";
    public const string ReadbackConfirmation = "non-retained-state-match";
}

public sealed class Zigbee2MqttDriverDescriptorProvider : ICommunicationDriverDescriptorProvider
{
    public CommunicationDriverTypeDescriptor Descriptor { get; } = new(
        Zigbee2MqttContract.DriverType,
        "Zigbee2MQTT Bridge",
        DriverContractVersion: 1,
        RuntimeCapabilities: DriverCapabilities.Read | DriverCapabilities.Write |
                            DriverCapabilities.Subscribe | DriverCapabilities.Diagnostics,
        EngineeringCapabilities: DriverEngineeringCapabilities.ConnectionTest |
                                 DriverEngineeringCapabilities.Discover |
                                 DriverEngineeringCapabilities.PointReadTest,
        AcquisitionModes: [DriverAcquisitionMode.Subscription],
        ConfigurationSchema: new DriverConfigurationSchemaDescriptor(
            Zigbee2MqttContract.SchemaId,
            Zigbee2MqttContract.SchemaVersion,
            DataSourceFields:
            [
                new("host", DriverConfigurationValueKind.Host, Required: true, DisplayName: "MQTT broker host"),
                new("port", DriverConfigurationValueKind.Port, DefaultValue: "1883", Minimum: 1, Maximum: 65535),
                new("tls", DriverConfigurationValueKind.Boolean, DefaultValue: "false"),
                new("baseTopic", DriverConfigurationValueKind.String, DefaultValue: "zigbee2mqtt"),
                new("clientId", DriverConfigurationValueKind.Identifier),
                new("protocolVersion", DriverConfigurationValueKind.Enum, DefaultValue: "mqtt5", AllowedValues: ["mqtt5", "mqtt311"]),
                new("username", DriverConfigurationValueKind.String),
                new("password", DriverConfigurationValueKind.SecretReference, DisplayName: "Password secret reference"),
                new("keepAliveSeconds", DriverConfigurationValueKind.Integer, DefaultValue: "30", Minimum: 1, Maximum: 65535, Advanced: true),
                new("connectTimeoutMilliseconds", DriverConfigurationValueKind.Integer, DefaultValue: "10000", Minimum: 100, Maximum: 300000, Advanced: true),
                new("requestTimeoutMilliseconds", DriverConfigurationValueKind.Integer, DefaultValue: "5000", Minimum: 100, Maximum: 30000, Advanced: true),
                new("writeConfirmationTimeoutMilliseconds", DriverConfigurationValueKind.Integer, DefaultValue: "10000", Minimum: 100, Maximum: 60000),
                new("reconnectMinimumMilliseconds", DriverConfigurationValueKind.Integer, DefaultValue: "1000", Minimum: 100, Maximum: 300000, Advanced: true),
                new("reconnectMaximumMilliseconds", DriverConfigurationValueKind.Integer, DefaultValue: "30000", Minimum: 100, Maximum: 3600000, Advanced: true),
                new("maximumInboundPayloadBytes", DriverConfigurationValueKind.Integer, DefaultValue: "1048576", Minimum: 1, Maximum: MqttConnectionSettings.MaximumAllowedInboundPayloadBytes, Advanced: true),
                new("maximumConsecutiveConnectFailures", DriverConfigurationValueKind.Integer, DefaultValue: "10", Minimum: 1, Maximum: 1000, Advanced: true),
                new("maximumBufferedMessages", DriverConfigurationValueKind.Integer, DefaultValue: "2048", Minimum: 1, Maximum: 65535, Advanced: true),
                new("maximumDevices", DriverConfigurationValueKind.Integer, DefaultValue: "256", Minimum: 1, Maximum: 4096, Advanced: true),
                new("maximumExposesPerDevice", DriverConfigurationValueKind.Integer, DefaultValue: "256", Minimum: 1, Maximum: 2048, Advanced: true)
            ],
            TagBindingFields:
            [
                new("address", DriverConfigurationValueKind.String, Required: true, DisplayName: "Stable IEEE/property binding"),
                new("z2m.dataSourceId", DriverConfigurationValueKind.Identifier, Required: true),
                new("z2m.dataSourceKey", DriverConfigurationValueKind.String, Required: true),
                new("z2m.ieeeAddress", DriverConfigurationValueKind.String, Required: true),
                new("z2m.endpoint", DriverConfigurationValueKind.String),
                new("z2m.property", DriverConfigurationValueKind.String, Required: true),
                new("z2m.exposeType", DriverConfigurationValueKind.Enum, Required: true, AllowedValues: ["binary", "numeric"]),
                new("z2m.access", DriverConfigurationValueKind.Integer, Required: true),
                new("z2m.unit", DriverConfigurationValueKind.String),
                new("z2m.minimum", DriverConfigurationValueKind.Number),
                new("z2m.maximum", DriverConfigurationValueKind.Number),
                new("z2m.step", DriverConfigurationValueKind.Number),
                new("z2m.valueOn", DriverConfigurationValueKind.String),
                new("z2m.valueOff", DriverConfigurationValueKind.String),
                new("z2m.identity", DriverConfigurationValueKind.String, Required: true),
                new("z2m.capability", DriverConfigurationValueKind.String),
                new("z2m.reportConfirmation", DriverConfigurationValueKind.Enum, AllowedValues: [Zigbee2MqttContract.ReadbackConfirmation])
            ]),
        Description: "Bounded state/read/write connector for a user-managed external Zigbee2MQTT installation over MQTT.",
        TagBindingSchemaId: Zigbee2MqttContract.TagBindingSchemaId,
        TagBindingSchemaVersion: Zigbee2MqttContract.TagBindingSchemaVersion,
        IntegrationDomains: [IntegrationDomain.Building, IntegrationDomain.Residential, IntegrationDomain.IoT],
        ConnectionModel: DriverConnectionModel.LocalBridge,
        ExternalDependencies: [new DriverExternalDependencyDescriptor(DriverExternalDependencyKind.ExternalGateway, "user-managed Zigbee2MQTT and MQTT broker")]);
}

public sealed record Zigbee2MqttConnectionSettings(
    MqttConnectionSettings Mqtt,
    string BaseTopic,
    TimeSpan RequestTimeout,
    TimeSpan WriteConfirmationTimeout,
    int MaximumDevices,
    int MaximumExposesPerDevice)
{
    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Mqtt);
        Mqtt.Validate();
        if (string.IsNullOrWhiteSpace(BaseTopic) || BaseTopic != BaseTopic.Trim() ||
            BaseTopic.StartsWith("/", StringComparison.Ordinal) || BaseTopic.EndsWith("/", StringComparison.Ordinal))
            throw new ArgumentException("Zigbee2MQTT base topic must be a non-empty topic prefix without surrounding whitespace or slashes.", nameof(BaseTopic));
        MqttPoint.ValidateExactTopic($"{BaseTopic}/bridge/devices", nameof(BaseTopic));
        MqttPoint.ValidateExactTopic($"{BaseTopic}/bridge/state", nameof(BaseTopic));
        if (RequestTimeout < TimeSpan.FromMilliseconds(100) || RequestTimeout > TimeSpan.FromSeconds(30))
            throw new ArgumentOutOfRangeException(nameof(RequestTimeout));
        if (WriteConfirmationTimeout < TimeSpan.FromMilliseconds(100) || WriteConfirmationTimeout > TimeSpan.FromSeconds(60))
            throw new ArgumentOutOfRangeException(nameof(WriteConfirmationTimeout));
        if (MaximumDevices is < 1 or > 4096) throw new ArgumentOutOfRangeException(nameof(MaximumDevices));
        if (MaximumExposesPerDevice is < 1 or > 2048) throw new ArgumentOutOfRangeException(nameof(MaximumExposesPerDevice));
    }

    public string BridgeStateTopic => $"{BaseTopic}/bridge/state";
    public string BridgeDevicesTopic => $"{BaseTopic}/bridge/devices";
    public string BridgeInfoTopic => $"{BaseTopic}/bridge/info";

    public static Zigbee2MqttConnectionSettings Parse(
        string dataSourceKey,
        Guid? dataSourceId,
        IReadOnlyDictionary<string, string>? values)
    {
        if (string.IsNullOrWhiteSpace(dataSourceKey) || dataSourceKey != dataSourceKey.Trim())
            throw new ArgumentException("Zigbee2MQTT requires a canonical Data Source key.", nameof(dataSourceKey));
        if (dataSourceId is null || dataSourceId == Guid.Empty)
            throw new ArgumentException("Zigbee2MQTT requires a canonical DataSourceId for stable device identity.", nameof(dataSourceId));
        var settings = values ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var host = Required(settings, "host");
        if (host.Any(char.IsWhiteSpace) || host.Any(char.IsControl) || host.Contains('@') || host.Contains('/') || host.Contains('?') || host.Contains('#'))
            throw new ArgumentException("MQTT broker host must be a hostname or IP address without user info, path, or whitespace.", nameof(values));
        var tls = GetBool(settings, "tls", false);
        var port = GetInt(settings, "port", tls ? 8883 : 1883, 1, 65535);
        var protocol = Get(settings, "protocolVersion")?.Trim().ToLowerInvariant() switch
        {
            null or "" or "mqtt5" => MqttProtocolMode.Mqtt5,
            "mqtt311" => MqttProtocolMode.Mqtt311,
            _ => throw new ArgumentException("MQTT protocolVersion must be mqtt5 or mqtt311.")
        };
        var clientId = Get(settings, "clientId");
        if (string.IsNullOrWhiteSpace(clientId))
            clientId = $"elitescada-z2m-{dataSourceId.Value:N}";

        var mqtt = new MqttConnectionSettings(
            host,
            port,
            tls,
            clientId,
            protocol,
            KeepAlive: TimeSpan.FromSeconds(GetInt(settings, "keepAliveSeconds", 30, 1, 65535)),
            ConnectTimeout: TimeSpan.FromMilliseconds(GetInt(settings, "connectTimeoutMilliseconds", 10000, 100, 300000)),
            ReconnectMinimumDelay: TimeSpan.FromMilliseconds(GetInt(settings, "reconnectMinimumMilliseconds", 1000, 100, 300000)),
            ReconnectMaximumDelay: TimeSpan.FromMilliseconds(GetInt(settings, "reconnectMaximumMilliseconds", 30000, 100, 3600000)),
            CleanSession: protocol == MqttProtocolMode.Mqtt311,
            CleanStart: protocol == MqttProtocolMode.Mqtt5,
            SessionExpirySeconds: protocol == MqttProtocolMode.Mqtt5 ? 0U : null,
            MaximumInboundPayloadBytes: GetInt(settings, "maximumInboundPayloadBytes", 1_048_576, 1, MqttConnectionSettings.MaximumAllowedInboundPayloadBytes),
            MaximumConsecutiveConnectFailures: GetInt(settings, "maximumConsecutiveConnectFailures", 10, 1, 1000),
            MaximumBufferedMessages: GetInt(settings, "maximumBufferedMessages", 2048, 1, 65535));

        var result = new Zigbee2MqttConnectionSettings(
            mqtt,
            Get(settings, "baseTopic") ?? "zigbee2mqtt",
            TimeSpan.FromMilliseconds(GetInt(settings, "requestTimeoutMilliseconds", 5000, 100, 30000)),
            TimeSpan.FromMilliseconds(GetInt(settings, "writeConfirmationTimeoutMilliseconds", 10000, 100, 60000)),
            GetInt(settings, "maximumDevices", 256, 1, 4096),
            GetInt(settings, "maximumExposesPerDevice", 256, 1, 2048));
        result.Validate();
        return result;
    }

    private static string Required(IReadOnlyDictionary<string, string> values, string key) =>
        Get(values, key) is { Length: > 0 } value ? value : throw new ArgumentException($"Zigbee2MQTT setting '{key}' is required.");

    private static string? Get(IReadOnlyDictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) ? value :
        values.FirstOrDefault(pair => pair.Key.Equals(key, StringComparison.OrdinalIgnoreCase)).Value;

    private static bool GetBool(IReadOnlyDictionary<string, string> values, string key, bool fallback) =>
        Get(values, key) is { } text
            ? bool.TryParse(text, out var parsed) ? parsed : throw new ArgumentException($"Zigbee2MQTT setting '{key}' must be true or false.")
            : fallback;

    private static int GetInt(IReadOnlyDictionary<string, string> values, string key, int fallback, int minimum, int maximum)
    {
        if (Get(values, key) is not { } text) return fallback;
        if (!int.TryParse(text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsed) || parsed < minimum || parsed > maximum)
            throw new ArgumentException($"Zigbee2MQTT setting '{key}' must be an integer from {minimum} to {maximum}.");
        return parsed;
    }
}

public enum Zigbee2MqttValueKind
{
    Boolean,
    Double
}

public sealed record Zigbee2MqttExpose(
    string Property,
    string? Endpoint,
    Zigbee2MqttValueKind ValueKind,
    int Access,
    string? Unit,
    double? Minimum,
    double? Maximum,
    double? Step,
    string? ValueOnJson,
    string? ValueOffJson,
    string CapabilityKind)
{
    public bool Readable => (Access & 1) != 0;
    public bool Settable => (Access & 2) != 0;
    public bool Gettable => (Access & 4) != 0;
}

public sealed record Zigbee2MqttDevice(
    string IeeeAddress,
    string FriendlyName,
    bool Supported,
    string? Model,
    string? Vendor,
    IReadOnlyCollection<Zigbee2MqttExpose> Exposes,
    IReadOnlyCollection<DriverEngineeringIssue>? Issues = null);

public sealed record Zigbee2MqttInventory(
    IReadOnlyCollection<Zigbee2MqttDevice> Devices,
    string? BridgeState,
    string? Version,
    DateTimeOffset ObservedAtUtc);

public sealed record Zigbee2MqttPoint(
    TagDefinition Tag,
    string IeeeAddress,
    string? Endpoint,
    string Property,
    Zigbee2MqttValueKind ValueKind,
    int Access,
    string? Unit,
    double? Minimum,
    double? Maximum,
    double? Step,
    string? ValueOnJson,
    string? ValueOffJson,
    string CapabilityKind)
{
    public bool Writable => !Tag.ReadOnly && (Access & 2) != 0;
    public bool Gettable => (Access & 4) != 0;

    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Tag);
        if (Tag.Id == Guid.Empty || Tag.DataType is not (TagDataType.Boolean or TagDataType.Double))
            throw new ArgumentException("Zigbee2MQTT points require a canonical Boolean or Double TAG.", nameof(Tag));
        if (!Enum.IsDefined(ValueKind) ||
            (ValueKind == Zigbee2MqttValueKind.Boolean) != (Tag.DataType == TagDataType.Boolean))
            throw new ArgumentException("Zigbee2MQTT expose kind must match the canonical TAG data type.", nameof(ValueKind));
        if (!string.Equals(Tag.EngineeringUnit ?? string.Empty, Unit ?? string.Empty, StringComparison.Ordinal))
            throw new ArgumentException("Zigbee2MQTT expose unit must match the canonical TAG engineering unit.", nameof(Unit));
        if (!Zigbee2MqttIdentity.TryNormalizeIeee(IeeeAddress, out var normalized) || normalized != IeeeAddress)
            throw new ArgumentException("Zigbee2MQTT point IEEE address is not canonical.", nameof(IeeeAddress));
        if (string.IsNullOrWhiteSpace(Property) || Property.Any(char.IsControl))
            throw new ArgumentException("Zigbee2MQTT expose property is invalid.", nameof(Property));
        if ((Access & 1) == 0 || (Access & ~7) != 0)
            throw new ArgumentException("Zigbee2MQTT point access must include state/read and use only the documented access bits.", nameof(Access));
        if (ValueKind == Zigbee2MqttValueKind.Boolean && (ValueOnJson is null || ValueOffJson is null || ValueOnJson == ValueOffJson))
            throw new ArgumentException("Boolean exposes require distinct explicit value_on and value_off values.");
        if (ValueKind == Zigbee2MqttValueKind.Double &&
            (Minimum.HasValue && !double.IsFinite(Minimum.Value) || Maximum.HasValue && !double.IsFinite(Maximum.Value) ||
             Step.HasValue && (!double.IsFinite(Step.Value) || Step.Value <= 0) ||
             Minimum.HasValue && Maximum.HasValue && Minimum > Maximum))
            throw new ArgumentException("Numeric expose range and step are invalid.");
        if (Writable && string.IsNullOrWhiteSpace(CapabilityKind))
            throw new ArgumentException("Writable exposes require an explicit supported state capability.");
    }
}

public static class Zigbee2MqttIdentity
{
    public static bool TryNormalizeIeee(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value) || value != value.Trim()) return false;
        var token = value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? value[2..] : value;
        if (token.Length != 16 || token.Any(character => !Uri.IsHexDigit(character))) return false;
        normalized = "0x" + token.ToLowerInvariant();
        return true;
    }

    public static string StableDeviceIdentity(Guid dataSourceId, string ieeeAddress)
    {
        if (dataSourceId == Guid.Empty) throw new ArgumentException("DataSourceId is required.", nameof(dataSourceId));
        if (!TryNormalizeIeee(ieeeAddress, out var ieee)) throw new ArgumentException("IEEE address is invalid.", nameof(ieeeAddress));
        return $"zigbee2mqtt:datasource:{dataSourceId:N}:ieee:{ieee}";
    }

    public static string StablePointIdentity(Guid dataSourceId, string ieeeAddress, string? endpoint, string property)
    {
        if (string.IsNullOrWhiteSpace(property) || property.Any(char.IsControl))
            throw new ArgumentException("Property is invalid.", nameof(property));
        if (endpoint?.Any(char.IsControl) == true)
            throw new ArgumentException("Endpoint is invalid.", nameof(endpoint));
        return $"{StableDeviceIdentity(dataSourceId, ieeeAddress)}:endpoint:{Uri.EscapeDataString(endpoint ?? string.Empty)}:property:{Uri.EscapeDataString(property)}";
    }

    public static string PortableAddress(string ieeeAddress, string? endpoint, string property)
    {
        if (!TryNormalizeIeee(ieeeAddress, out var ieee)) throw new ArgumentException("IEEE address is invalid.", nameof(ieeeAddress));
        if (string.IsNullOrWhiteSpace(property) || property.Any(char.IsControl)) throw new ArgumentException("Property is invalid.", nameof(property));
        if (endpoint?.Any(char.IsControl) == true) throw new ArgumentException("Endpoint is invalid.", nameof(endpoint));
        return $"ieee:{ieee}/endpoint:{Uri.EscapeDataString(endpoint ?? string.Empty)}/property:{Uri.EscapeDataString(property)}";
    }

    public static bool TryParsePortableAddress(string? value, out string ieeeAddress, out string? endpoint, out string property)
    {
        ieeeAddress = string.Empty;
        endpoint = null;
        property = string.Empty;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var parts = value.Split('/', StringSplitOptions.None);
        if (parts.Length != 3 || !parts[0].StartsWith("ieee:", StringComparison.Ordinal) ||
            !parts[1].StartsWith("endpoint:", StringComparison.Ordinal) ||
            !parts[2].StartsWith("property:", StringComparison.Ordinal)) return false;
        if (!TryNormalizeIeee(parts[0][5..], out ieeeAddress)) return false;
        try
        {
            var decodedEndpoint = Uri.UnescapeDataString(parts[1][9..]);
            endpoint = decodedEndpoint.Length == 0 ? null : decodedEndpoint;
            property = Uri.UnescapeDataString(parts[2][9..]);
        }
        catch (UriFormatException) { return false; }
        return !string.IsNullOrWhiteSpace(property) && !property.Any(char.IsControl) &&
               endpoint?.Any(char.IsControl) != true &&
               string.Equals(value, PortableAddress(ieeeAddress, endpoint, property), StringComparison.Ordinal);
    }
}
