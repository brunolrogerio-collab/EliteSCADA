using System.Globalization;
using Scada.Core.Tags;
using Scada.Drivers.Mqtt;
using Scada.Engineering.Contracts;

namespace Scada.DriverHost.Engineering;

public sealed record MqttRuntimePlan(
    string DataSourceKey,
    string DriverId,
    string Name,
    MqttConnectionSettings Connection,
    string? Username,
    string? PasswordSecretReference,
    IReadOnlyCollection<MqttPoint> Points);

public sealed record MqttEngineeringCompilation(
    IReadOnlyCollection<MqttRuntimePlan> Plans,
    IReadOnlyCollection<EngineeringDriverIssue> Issues)
{
    public bool CanActivate => Issues.All(issue => !issue.IsError);
}

/// <summary>
/// Compiles canonical Engineering DTOs into MQTT runtime plans without inventing
/// protocol-private persistence. Data Source Settings/SecretReferences and TAG
/// Address/Metadata remain the public project representation.
/// </summary>
public sealed class MqttEngineeringCompiler
{
    public MqttEngineeringCompilation Compile(EngineeringPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);

        var plans = new List<MqttRuntimePlan>();
        var issues = new List<EngineeringDriverIssue>();
        var dataSources = package.DataSources ?? Array.Empty<DataSourceEngineeringDto>();

        foreach (var dataSource in dataSources
                     .Where(item => item.Enabled && item.Driver.Equals(MqttDriverDescriptorProvider.DriverType, StringComparison.OrdinalIgnoreCase))
                     .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
        {
            CompileDataSource(package, dataSource, plans, issues);
        }

        return new MqttEngineeringCompilation(plans, issues);
    }

    private static void CompileDataSource(
        EngineeringPackage package,
        DataSourceEngineeringDto dataSource,
        List<MqttRuntimePlan> plans,
        List<EngineeringDriverIssue> issues)
    {
        var settings = CaseInsensitive(dataSource.Settings);
        var secrets = CaseInsensitive(dataSource.SecretReferences);
        var errorsBefore = ErrorCount(issues, dataSource.Key);

        if (settings.ContainsKey("password"))
        {
            issues.Add(Error(
                "MQTT_PLAINTEXT_SECRET_REJECTED",
                "MQTT password must be stored as DataSource.SecretReferences['password']; plaintext Settings['password'] is forbidden.",
                dataSource.Key));
        }

        var host = Get(settings, "host");
        if (string.IsNullOrWhiteSpace(host))
            issues.Add(Error("MQTT_HOST_REQUIRED", "MQTT setting 'host' is required.", dataSource.Key));

        var useTls = ParseBool(settings, "tls", true, dataSource.Key, issues);
        var defaultPort = useTls ? 8883 : 1883;
        var port = ParseInt(settings, "port", defaultPort, 1, 65535, dataSource.Key, issues);
        var clientId = Get(settings, "clientId");
        if (string.IsNullOrWhiteSpace(clientId))
            issues.Add(Error("MQTT_CLIENT_ID_REQUIRED", "MQTT setting 'clientId' is required.", dataSource.Key));

        var protocol = ParseProtocol(settings, dataSource.Key, issues);
        var username = NullIfWhiteSpace(Get(settings, "username"));
        var passwordSecretReference = NullIfWhiteSpace(Get(secrets, "password"));
        if (passwordSecretReference is not null && username is null)
        {
            issues.Add(Error(
                "MQTT_PASSWORD_USERNAME_REQUIRED",
                "MQTT password secret reference requires a non-empty 'username' setting.",
                dataSource.Key));
        }

        var keepAliveSeconds = ParseInt(settings, "keepAliveSeconds", 30, 1, 65535, dataSource.Key, issues);
        var connectTimeoutMs = ParseInt(settings, "connectTimeoutMilliseconds", 10_000, 100, 300_000, dataSource.Key, issues);
        var reconnectMinimumMs = ParseInt(settings, "reconnectMinimumMilliseconds", 1_000, 100, 300_000, dataSource.Key, issues);
        var reconnectMaximumMs = ParseInt(settings, "reconnectMaximumMilliseconds", 30_000, 100, 3_600_000, dataSource.Key, issues);
        var maximumInboundPayloadBytes = ParseInt(settings, "maximumInboundPayloadBytes", 1_048_576, 1, 67_108_864, dataSource.Key, issues);
        var maximumConsecutiveConnectFailures = ParseInt(settings, "maximumConsecutiveConnectFailures", 5, 1, 1000, dataSource.Key, issues);
        var maximumBufferedMessages = ParseInt(settings, "maximumBufferedMessages", 4_096, 1, 1_000_000, dataSource.Key, issues);

        var cleanSession = false;
        var cleanStart = false;
        uint? sessionExpirySeconds = null;
        if (protocol == MqttProtocolMode.Mqtt311)
        {
            cleanSession = ParseBool(settings, "mqtt311.cleanSession", false, dataSource.Key, issues);
            RejectSetting(settings, "mqtt5.cleanStart", "MQTT 5 Clean Start", dataSource.Key, issues);
            RejectSetting(settings, "mqtt5.sessionExpirySeconds", "MQTT 5 Session Expiry", dataSource.Key, issues);
        }
        else
        {
            cleanStart = ParseBool(settings, "mqtt5.cleanStart", false, dataSource.Key, issues);
            sessionExpirySeconds = ParseUInt(settings, "mqtt5.sessionExpirySeconds", 3600U, dataSource.Key, issues);
            RejectSetting(settings, "mqtt311.cleanSession", "MQTT 3.1.1 Clean Session", dataSource.Key, issues);
        }

        var connection = new MqttConnectionSettings(
            host ?? string.Empty,
            port,
            useTls,
            clientId ?? string.Empty,
            protocol,
            TimeSpan.FromSeconds(keepAliveSeconds),
            TimeSpan.FromMilliseconds(connectTimeoutMs),
            TimeSpan.FromMilliseconds(reconnectMinimumMs),
            TimeSpan.FromMilliseconds(reconnectMaximumMs),
            cleanSession,
            cleanStart,
            sessionExpirySeconds,
            maximumInboundPayloadBytes,
            maximumConsecutiveConnectFailures,
            maximumBufferedMessages);

        try
        {
            connection.Validate();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            issues.Add(Error("MQTT_DATASOURCE_CONFIGURATION_INVALID", ex.Message, dataSource.Key));
        }

        var sourceTags = package.Tags
            .Where(tag => string.Equals(tag.Source, dataSource.Key, StringComparison.OrdinalIgnoreCase))
            .OrderBy(tag => tag.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (sourceTags.Length == 0)
        {
            issues.Add(new EngineeringDriverIssue(
                "MQTT_DATASOURCE_NO_TAGS",
                $"Enabled MQTT data source '{dataSource.Key}' has no associated TAGs.",
                dataSource.Key,
                IsError: false));
        }

        var points = new List<MqttPoint>();
        foreach (var tag in sourceTags)
        {
            var point = CompilePoint(dataSource.Key, tag, issues);
            if (point is not null) points.Add(point);
        }

        if (ErrorCount(issues, dataSource.Key) != errorsBefore) return;

        plans.Add(new MqttRuntimePlan(
            dataSource.Key,
            $"{MqttDriverDescriptorProvider.DriverType}:{dataSource.Key}",
            dataSource.Name,
            connection,
            username,
            passwordSecretReference,
            points));
    }

    private static MqttPoint? CompilePoint(
        string dataSourceKey,
        TagEngineeringDto dto,
        List<EngineeringDriverIssue> issues)
    {
        if (!IsSupportedDataType(dto.DataType))
        {
            issues.Add(Error(
                "MQTT_TAG_DATA_TYPE_UNSUPPORTED",
                $"MQTT TAG '{dto.Path}' uses unsupported data type '{dto.DataType}'.",
                dataSourceKey,
                dto.Path));
            return null;
        }

        if (dto.AddressSelector is not null)
        {
            issues.Add(Error(
                "MQTT_TAG_SELECTOR_UNSUPPORTED",
                $"MQTT TAG '{dto.Path}' cannot use AddressSelector in this driver slice. Map a typed payload field directly instead.",
                dataSourceKey,
                dto.Path));
            return null;
        }

        if (string.IsNullOrWhiteSpace(dto.Address))
        {
            issues.Add(Error(
                "MQTT_TAG_TOPIC_REQUIRED",
                $"MQTT TAG '{dto.Path}' requires an exact subscribe topic in Address.",
                dataSourceKey,
                dto.Path));
            return null;
        }

        var metadata = CaseInsensitive(dto.Metadata);
        var errorsBefore = ErrorCount(issues, dataSourceKey);
        var payloadFormat = ParsePayloadFormat(metadata, dataSourceKey, dto.Path, issues);
        var jsonPointer = NullIfEmpty(Get(metadata, "mqtt.jsonPointer"));
        var sourceTimestampJsonPointer = NullIfEmpty(Get(metadata, "mqtt.sourceTimestampJsonPointer"));
        var sourceTimestampRequired = ParseBool(metadata, "mqtt.sourceTimestampRequired", false, dataSourceKey, issues, dto.Path);
        var freshnessTimeoutMs = ParseOptionalInt(metadata, "mqtt.freshnessTimeoutMilliseconds", 1, int.MaxValue, dataSourceKey, issues, dto.Path);
        var retainedPolicy = ParseRetainedPolicy(metadata, dataSourceKey, dto.Path, issues);
        var qos = ParseQos(metadata, "mqtt.qos", MqttQosLevel.AtLeastOnce, dataSourceKey, dto.Path, issues);
        var publishTopic = NullIfWhiteSpace(Get(metadata, "mqtt.publishTopic"));
        var publishQos = ParseQos(metadata, "mqtt.publishQos", MqttQosLevel.AtLeastOnce, dataSourceKey, dto.Path, issues);
        var publishRetain = ParseBool(metadata, "mqtt.publishRetain", false, dataSourceKey, issues, dto.Path);

        if (ErrorCount(issues, dataSourceKey) != errorsBefore) return null;

        var tag = BuildTagDefinition(dto);
        try
        {
            var point = new MqttPoint(
                tag,
                dto.Address,
                payloadFormat,
                jsonPointer,
                sourceTimestampJsonPointer,
                sourceTimestampRequired,
                retainedPolicy,
                qos,
                Writable: !dto.ReadOnly,
                PublishTopic: publishTopic,
                PublishQos: publishQos,
                PublishRetain: publishRetain,
                FreshnessTimeout: freshnessTimeoutMs.HasValue
                    ? TimeSpan.FromMilliseconds(freshnessTimeoutMs.Value)
                    : null);
            point.Validate();
            return point;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            issues.Add(Error("MQTT_TAG_CONFIGURATION_INVALID", ex.Message, dataSourceKey, dto.Path));
            return null;
        }
    }

    private static TagDefinition BuildTagDefinition(TagEngineeringDto dto)
    {
        var metadata = CaseInsensitive(dto.Metadata);
        if (!string.IsNullOrWhiteSpace(dto.Address)) metadata["address"] = dto.Address;
        if (dto.ScaleMinimum.HasValue) metadata["scale.minimum"] = dto.ScaleMinimum.Value.ToString(CultureInfo.InvariantCulture);
        if (dto.ScaleMaximum.HasValue) metadata["scale.maximum"] = dto.ScaleMaximum.Value.ToString(CultureInfo.InvariantCulture);
        if (dto.Historian is not null)
        {
            metadata["historian.enabled"] = dto.Historian.Enabled.ToString(CultureInfo.InvariantCulture);
            metadata["historian.strategy"] = dto.Historian.Strategy;
            Set(metadata, "historian.deadband", dto.Historian.Deadband);
            Set(metadata, "historian.periodMs", dto.Historian.PeriodMilliseconds);
            Set(metadata, "historian.maxPeriodMs", dto.Historian.MaximumPeriodMilliseconds);
        }

        var access = dto.AccessPolicy is null
            ? null
            : new TagAccessPolicy(
                dto.AccessPolicy.ReadRoles?.ToArray(),
                dto.AccessPolicy.WriteRoles?.ToArray(),
                dto.AccessPolicy.ConfigureRoles?.ToArray());

        return new TagDefinition(
            dto.Id ?? Guid.NewGuid(),
            dto.Name,
            dto.Path,
            dto.DataType,
            dto.Source,
            dto.EngineeringUnit,
            dto.Description,
            dto.ReadOnly,
            metadata,
            access,
            dto.AddressSelector);
    }

    private static bool IsSupportedDataType(TagDataType dataType) => dataType is
        TagDataType.Boolean or
        TagDataType.Int16 or
        TagDataType.Int32 or
        TagDataType.Int64 or
        TagDataType.Float or
        TagDataType.Double or
        TagDataType.String or
        TagDataType.Enum or
        TagDataType.DateTime;

    private static MqttProtocolMode ParseProtocol(
        IReadOnlyDictionary<string, string> settings,
        string dataSourceKey,
        List<EngineeringDriverIssue> issues)
    {
        var raw = Get(settings, "protocolVersion");
        if (string.IsNullOrWhiteSpace(raw) || raw.Equals("mqtt5", StringComparison.OrdinalIgnoreCase))
            return MqttProtocolMode.Mqtt5;
        if (raw.Equals("mqtt311", StringComparison.OrdinalIgnoreCase))
            return MqttProtocolMode.Mqtt311;

        issues.Add(Error(
            "MQTT_PROTOCOL_VERSION_INVALID",
            $"MQTT setting 'protocolVersion' must be 'mqtt5' or 'mqtt311'; received '{raw}'.",
            dataSourceKey));
        return MqttProtocolMode.Mqtt5;
    }

    private static MqttPayloadFormat ParsePayloadFormat(
        IReadOnlyDictionary<string, string> metadata,
        string dataSourceKey,
        string tagPath,
        List<EngineeringDriverIssue> issues)
    {
        var raw = Get(metadata, "mqtt.payloadFormat");
        if (string.IsNullOrWhiteSpace(raw) || raw.Equals("utf8Scalar", StringComparison.OrdinalIgnoreCase))
            return MqttPayloadFormat.Utf8Scalar;
        if (raw.Equals("json", StringComparison.OrdinalIgnoreCase)) return MqttPayloadFormat.Json;

        issues.Add(Error(
            "MQTT_PAYLOAD_FORMAT_INVALID",
            $"TAG '{tagPath}' metadata 'mqtt.payloadFormat' must be 'utf8Scalar' or 'json'; received '{raw}'.",
            dataSourceKey,
            tagPath));
        return MqttPayloadFormat.Utf8Scalar;
    }

    private static MqttRetainedValuePolicy ParseRetainedPolicy(
        IReadOnlyDictionary<string, string> metadata,
        string dataSourceKey,
        string tagPath,
        List<EngineeringDriverIssue> issues)
    {
        var raw = Get(metadata, "mqtt.retainedValuePolicy");
        if (string.IsNullOrWhiteSpace(raw) || raw.Equals("staleWithoutSourceTimestamp", StringComparison.OrdinalIgnoreCase))
            return MqttRetainedValuePolicy.MarkStaleWithoutSourceTimestamp;
        if (raw.Equals("acceptAsCurrent", StringComparison.OrdinalIgnoreCase))
            return MqttRetainedValuePolicy.AcceptAsCurrent;

        issues.Add(Error(
            "MQTT_RETAINED_POLICY_INVALID",
            $"TAG '{tagPath}' has unsupported mqtt.retainedValuePolicy '{raw}'.",
            dataSourceKey,
            tagPath));
        return MqttRetainedValuePolicy.MarkStaleWithoutSourceTimestamp;
    }

    private static MqttQosLevel ParseQos(
        IReadOnlyDictionary<string, string> metadata,
        string key,
        MqttQosLevel fallback,
        string dataSourceKey,
        string tagPath,
        List<EngineeringDriverIssue> issues)
    {
        var raw = Get(metadata, key);
        if (string.IsNullOrWhiteSpace(raw)) return fallback;
        if (int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value is >= 0 and <= 2)
            return (MqttQosLevel)value;

        issues.Add(Error(
            "MQTT_QOS_INVALID",
            $"TAG '{tagPath}' metadata '{key}' must be 0, 1 or 2; received '{raw}'.",
            dataSourceKey,
            tagPath));
        return fallback;
    }

    private static int ParseInt(
        IReadOnlyDictionary<string, string> map,
        string key,
        int fallback,
        int minimum,
        int maximum,
        string dataSourceKey,
        List<EngineeringDriverIssue> issues,
        string? tagPath = null)
    {
        var raw = Get(map, key);
        if (string.IsNullOrWhiteSpace(raw)) return fallback;
        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value >= minimum && value <= maximum)
            return value;

        issues.Add(Error(
            "MQTT_SETTING_INVALID",
            $"Setting '{key}' must be an integer from {minimum} to {maximum}; received '{raw}'.",
            dataSourceKey,
            tagPath));
        return fallback;
    }

    private static int? ParseOptionalInt(
        IReadOnlyDictionary<string, string> map,
        string key,
        int minimum,
        int maximum,
        string dataSourceKey,
        List<EngineeringDriverIssue> issues,
        string? tagPath = null)
    {
        var raw = Get(map, key);
        if (string.IsNullOrWhiteSpace(raw)) return null;
        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value >= minimum && value <= maximum)
            return value;

        issues.Add(Error(
            "MQTT_SETTING_INVALID",
            $"Setting '{key}' must be an integer from {minimum} to {maximum}; received '{raw}'.",
            dataSourceKey,
            tagPath));
        return null;
    }

    private static uint ParseUInt(
        IReadOnlyDictionary<string, string> map,
        string key,
        uint fallback,
        string dataSourceKey,
        List<EngineeringDriverIssue> issues)
    {
        var raw = Get(map, key);
        if (string.IsNullOrWhiteSpace(raw)) return fallback;
        if (uint.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var value)) return value;

        issues.Add(Error(
            "MQTT_SETTING_INVALID",
            $"Setting '{key}' must be an unsigned integer from 0 to {uint.MaxValue}; received '{raw}'.",
            dataSourceKey));
        return fallback;
    }

    private static bool ParseBool(
        IReadOnlyDictionary<string, string> map,
        string key,
        bool fallback,
        string dataSourceKey,
        List<EngineeringDriverIssue> issues,
        string? tagPath = null)
    {
        var raw = Get(map, key);
        if (string.IsNullOrWhiteSpace(raw)) return fallback;
        if (bool.TryParse(raw, out var value)) return value;

        issues.Add(Error(
            "MQTT_SETTING_INVALID",
            $"Setting '{key}' must be 'true' or 'false'; received '{raw}'.",
            dataSourceKey,
            tagPath));
        return fallback;
    }

    private static void RejectSetting(
        IReadOnlyDictionary<string, string> settings,
        string key,
        string description,
        string dataSourceKey,
        List<EngineeringDriverIssue> issues)
    {
        if (!settings.ContainsKey(key)) return;
        issues.Add(Error(
            "MQTT_PROTOCOL_SETTING_MISMATCH",
            $"{description} setting '{key}' is not valid for the configured MQTT protocol version.",
            dataSourceKey));
    }

    private static Dictionary<string, string> CaseInsensitive(IReadOnlyDictionary<string, string>? source) =>
        source is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(source, StringComparer.OrdinalIgnoreCase);

    private static string? Get(IReadOnlyDictionary<string, string> map, string key) =>
        map.TryGetValue(key, out var value) ? value : null;

    private static string? NullIfWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string? NullIfEmpty(string? value) => value is null || value.Length == 0 ? null : value;

    private static int ErrorCount(IEnumerable<EngineeringDriverIssue> issues, string dataSourceKey) =>
        issues.Count(issue => issue.IsError && issue.DataSourceKey.Equals(dataSourceKey, StringComparison.OrdinalIgnoreCase));

    private static EngineeringDriverIssue Error(
        string code,
        string message,
        string dataSourceKey,
        string? tagPath = null) =>
        new(code, message, dataSourceKey, tagPath, IsError: true);

    private static void Set(Dictionary<string, string> metadata, string key, double? value)
    {
        if (value.HasValue) metadata[key] = value.Value.ToString(CultureInfo.InvariantCulture);
    }

    private static void Set(Dictionary<string, string> metadata, string key, int? value)
    {
        if (value.HasValue) metadata[key] = value.Value.ToString(CultureInfo.InvariantCulture);
    }
}
