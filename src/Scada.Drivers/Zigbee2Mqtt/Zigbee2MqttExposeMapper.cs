using System.Globalization;
using System.Text.Json;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;
using Scada.Drivers.Mqtt;

namespace Scada.Drivers.Zigbee2Mqtt;

public static class Zigbee2MqttExposeMapper
{
    public const int MaximumTotalExposes = 65_536;

    private static readonly IReadOnlyDictionary<string, string> BooleanCapabilities =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["state"] = "OnOff",
            ["contact"] = "BinaryInput",
            ["occupancy"] = "BinaryInput"
        };

    private static readonly IReadOnlyDictionary<string, string> NumericCapabilities =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["temperature"] = "Temperature",
            ["humidity"] = "Humidity",
            ["illuminance"] = "Illuminance",
            ["power"] = "Power",
            ["energy"] = "Energy"
        };

    public static Zigbee2MqttInventory ParseInventory(
        ReadOnlyMemory<byte> payload,
        Zigbee2MqttConnectionSettings settings,
        string? bridgeState = null,
        string? bridgeInfoPayload = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (payload.Length == 0 || payload.Length > settings.Mqtt.MaximumInboundPayloadBytes)
            throw new FormatException("Zigbee2MQTT bridge inventory is empty or exceeds the configured payload limit.");

        using var document = JsonDocument.Parse(payload, new JsonDocumentOptions { MaxDepth = 24, CommentHandling = JsonCommentHandling.Disallow });
        if (document.RootElement.ValueKind != JsonValueKind.Array)
            throw new FormatException("Zigbee2MQTT bridge/devices payload must be a JSON array.");
        if (document.RootElement.GetArrayLength() > settings.MaximumDevices)
            throw new FormatException($"Zigbee2MQTT inventory exceeds the configured limit of {settings.MaximumDevices} devices.");

        var devices = new List<Zigbee2MqttDevice>();
        var ieeeAddresses = new HashSet<string>(StringComparer.Ordinal);
        var friendlyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var totalExposes = 0;
        foreach (var item in document.RootElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                throw new FormatException("Zigbee2MQTT inventory contains a non-object device entry.");

            var rawIeee = String(item, "ieee_address");
            if (!Zigbee2MqttIdentity.TryNormalizeIeee(rawIeee, out var ieee))
                throw new FormatException("Zigbee2MQTT inventory contains a missing or invalid IEEE address.");
            var friendlyName = String(item, "friendly_name");
            if (string.IsNullOrWhiteSpace(friendlyName) || friendlyName.Length > 255 || friendlyName != friendlyName.Trim() || friendlyName.Any(char.IsControl))
                throw new FormatException($"Zigbee2MQTT device '{ieee}' has an invalid friendly_name used for MQTT routing.");
            if (!ieeeAddresses.Add(ieee))
                throw new FormatException($"Zigbee2MQTT inventory contains duplicate IEEE identity '{ieee}'.");
            if (!friendlyNames.Add(friendlyName))
                throw new FormatException($"Zigbee2MQTT inventory contains ambiguous duplicate friendly_name '{friendlyName}'.");
            ValidateTopicSegment(friendlyName, ieee);

            var issues = new List<DriverEngineeringIssue>();
            var exposes = new List<Zigbee2MqttExpose>();
            var supported = Bool(item, "supported") ?? false;
            if (!supported)
                issues.Add(Warning("Z2M_DEVICE_UNSUPPORTED", "Zigbee2MQTT does not declare this device supported; its exposes are shown for inventory review but cannot be materialized into Runtime TAGs."));
            var count = 0;
            if (item.TryGetProperty("exposes", out var exposesElement))
            {
                if (exposesElement.ValueKind != JsonValueKind.Array)
                    throw new FormatException($"Zigbee2MQTT device '{ieee}' exposes must be an array.");
                foreach (var expose in exposesElement.EnumerateArray())
                    VisitExpose(expose, null, null, settings.MaximumExposesPerDevice, ref count, exposes, issues, ieee);
            }
            totalExposes += count;
            if (totalExposes > MaximumTotalExposes)
                throw new FormatException($"Zigbee2MQTT inventory exceeds the global expose limit of {MaximumTotalExposes}.");

            if (exposes.GroupBy(expose => Zigbee2MqttIdentity.PortableAddress(ieee, expose.Endpoint, expose.Property), StringComparer.Ordinal)
                .Any(group => group.Count() > 1))
            {
                throw new FormatException($"Zigbee2MQTT device '{ieee}' exposes an ambiguous duplicate endpoint/property identity.");
            }

            devices.Add(new Zigbee2MqttDevice(
                ieee,
                friendlyName,
                supported,
                SafeString(item, "model_id"),
                SafeNestedString(item, "definition", "vendor"),
                exposes,
                issues));
        }

        var version = ParseVersion(bridgeInfoPayload);
        return new Zigbee2MqttInventory(devices, NormalizeBridgeState(bridgeState), version, DateTimeOffset.UtcNow);
    }

    public static DriverMaterializationCandidate BuildMaterialization(Guid dataSourceId, string dataSourceKey, Zigbee2MqttDevice device)
    {
        if (dataSourceId == Guid.Empty) throw new ArgumentException("DataSourceId is required.", nameof(dataSourceId));
        if (string.IsNullOrWhiteSpace(dataSourceKey)) throw new ArgumentException("Data Source key is required.", nameof(dataSourceKey));
        ArgumentNullException.ThrowIfNull(device);

        var tags = new List<DriverMaterializationTagCandidate>();
        var capabilities = new List<DriverMaterializationCapabilityCandidate>();
        var sourceIdentity = dataSourceId.ToString("N", CultureInfo.InvariantCulture);
        foreach (var expose in device.Exposes)
        {
            var writable = expose.Settable && expose.Readable && !string.IsNullOrWhiteSpace(expose.CapabilityKind);
            var address = Zigbee2MqttIdentity.PortableAddress(device.IeeeAddress, expose.Endpoint, expose.Property);
            var stableChild = Zigbee2MqttIdentity.StablePointIdentity(dataSourceId, device.IeeeAddress, expose.Endpoint, expose.Property);
            var candidateId = $"z2m-tag-{sourceIdentity}-{SafeSegment(device.IeeeAddress)}-{SafeSegment(expose.Endpoint ?? "root")}-{SafeSegment(expose.Property)}";
            var path = $"Zigbee2Mqtt/{sourceIdentity}/{device.IeeeAddress}/{SafeSegment(expose.Endpoint ?? "root")}/{SafeSegment(expose.Property)}";
            var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["z2m.dataSourceId"] = dataSourceId.ToString("D", CultureInfo.InvariantCulture),
                ["z2m.dataSourceKey"] = dataSourceKey,
                ["z2m.ieeeAddress"] = device.IeeeAddress,
                ["z2m.friendlyName"] = device.FriendlyName,
                ["z2m.property"] = expose.Property,
                ["z2m.endpoint"] = expose.Endpoint ?? string.Empty,
                ["z2m.exposeType"] = expose.ValueKind == Zigbee2MqttValueKind.Boolean ? "binary" : "numeric",
                ["z2m.access"] = expose.Access.ToString(CultureInfo.InvariantCulture),
                ["z2m.identity"] = stableChild,
                ["z2m.capability"] = expose.CapabilityKind
            };
            if (expose.Unit is not null) metadata["z2m.unit"] = expose.Unit;
            if (expose.Minimum.HasValue) metadata["z2m.minimum"] = expose.Minimum.Value.ToString("R", CultureInfo.InvariantCulture);
            if (expose.Maximum.HasValue) metadata["z2m.maximum"] = expose.Maximum.Value.ToString("R", CultureInfo.InvariantCulture);
            if (expose.Step.HasValue) metadata["z2m.step"] = expose.Step.Value.ToString("R", CultureInfo.InvariantCulture);
            if (expose.ValueOnJson is not null) metadata["z2m.valueOn"] = expose.ValueOnJson;
            if (expose.ValueOffJson is not null) metadata["z2m.valueOff"] = expose.ValueOffJson;
            if (writable) metadata["z2m.reportConfirmation"] = Zigbee2MqttContract.ReadbackConfirmation;

            var tag = new DriverMaterializationTagCandidate(
                candidateId,
                $"{device.FriendlyName} {expose.Property}",
                path,
                expose.ValueKind == Zigbee2MqttValueKind.Boolean ? TagDataType.Boolean : TagDataType.Double,
                address,
                ReadOnly: !writable,
                EngineeringUnit: expose.Unit,
                Metadata: metadata);
            tags.Add(tag);

            if (string.IsNullOrWhiteSpace(expose.CapabilityKind)) continue;
            capabilities.Add(new DriverMaterializationCapabilityCandidate(
                $"{Zigbee2MqttIdentity.StableDeviceIdentity(dataSourceId, device.IeeeAddress)}:{expose.CapabilityKind}:{SafeSegment(expose.Endpoint ?? "root")}:{SafeSegment(expose.Property)}",
                expose.CapabilityKind,
                [new DriverMaterializationRoleBinding("state", TagCandidateId: tag.CandidateId)],
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["property"] = expose.Property,
                    ["unit"] = expose.Unit ?? string.Empty,
                    ["identity"] = stableChild
                }));
        }

        var equipmentMetadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["source"] = dataSourceKey,
            ["dataSourceId"] = dataSourceId.ToString("D", CultureInfo.InvariantCulture),
            ["physicalIdentity"] = "zigbee-ieee",
            ["ieeeAddress"] = device.IeeeAddress,
            ["friendlyName"] = device.FriendlyName,
            ["supported"] = device.Supported.ToString(CultureInfo.InvariantCulture).ToLowerInvariant(),
            ["identityStability"] = "datasource_id_plus_ieee"
        };
        if (device.Model is not null) equipmentMetadata["model"] = device.Model;
        if (device.Vendor is not null) equipmentMetadata["vendor"] = device.Vendor;

        return new DriverMaterializationCandidate(
            new DriverMaterializationEquipmentCandidate(
                $"z2m-equipment-{sourceIdentity}-{SafeSegment(device.IeeeAddress)}",
                $"Zigbee2Mqtt/{sourceIdentity}/{device.IeeeAddress}",
                device.FriendlyName,
                Zigbee2MqttIdentity.StableDeviceIdentity(dataSourceId, device.IeeeAddress),
                SourceRole: "primary",
                Capabilities: capabilities,
                Metadata: equipmentMetadata),
            tags);
    }

    public static bool TryDecodeValue(Zigbee2MqttPoint point, JsonElement payload, out object? value, out string? error)
    {
        ArgumentNullException.ThrowIfNull(point);
        value = null;
        error = null;
        var current = payload;
        foreach (var segment in point.Property.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
            {
                error = $"State report has no property '{point.Property}'.";
                return false;
            }
        }

        if (point.ValueKind == Zigbee2MqttValueKind.Boolean)
        {
            if (JsonEquals(current, point.ValueOnJson)) { value = true; return true; }
            if (JsonEquals(current, point.ValueOffJson)) { value = false; return true; }
            error = $"Boolean state '{point.Property}' is outside its explicit value_on/value_off mapping.";
            return false;
        }

        if (current.ValueKind != JsonValueKind.Number || !current.TryGetDouble(out var numeric) || !double.IsFinite(numeric))
        {
            error = $"Numeric state '{point.Property}' is not a finite number.";
            return false;
        }
        if (point.Minimum.HasValue && numeric < point.Minimum.Value || point.Maximum.HasValue && numeric > point.Maximum.Value)
        {
            error = $"Numeric state '{point.Property}' is outside the declared expose range.";
            return false;
        }
        if (point.Step.HasValue)
        {
            var steps = (numeric - (point.Minimum ?? 0d)) / point.Step.Value;
            if (!double.IsFinite(steps) || Math.Abs(steps - Math.Round(steps)) > 1e-7 * Math.Max(1d, Math.Abs(steps)))
            {
                error = $"Numeric state '{point.Property}' is outside the declared expose step.";
                return false;
            }
        }
        value = numeric;
        return true;
    }

    public static string EncodeSetValue(Zigbee2MqttPoint point, object? value)
    {
        if (!point.Writable) throw new InvalidOperationException($"TAG '{point.Tag.Path}' is not writable by its verified expose access.");
        if (point.ValueKind == Zigbee2MqttValueKind.Boolean)
        {
            if (value is not bool boolean) throw new ArgumentException("Boolean Zigbee2MQTT TAG writes require a Boolean value.", nameof(value));
            return boolean ? point.ValueOnJson! : point.ValueOffJson!;
        }
        var number = value switch
        {
            double d => d,
            float f => f,
            decimal m => (double)m,
            byte b => b,
            sbyte b => b,
            short s => s,
            ushort s => s,
            int i => i,
            uint i => i,
            long l => l,
            ulong l => l,
            _ => throw new ArgumentException("Numeric Zigbee2MQTT TAG writes require a numeric value.", nameof(value))
        };
        if (!double.IsFinite(number) || point.Minimum.HasValue && number < point.Minimum.Value || point.Maximum.HasValue && number > point.Maximum.Value)
            throw new ArgumentOutOfRangeException(nameof(value), "Numeric write is non-finite or outside the declared expose range.");
        if (point.Step.HasValue)
        {
            var steps = (number - (point.Minimum ?? 0d)) / point.Step.Value;
            if (!double.IsFinite(steps) || Math.Abs(steps - Math.Round(steps)) > 1e-7 * Math.Max(1d, Math.Abs(steps)))
                throw new ArgumentOutOfRangeException(nameof(value), "Numeric write is outside the declared expose step.");
        }
        return JsonSerializer.Serialize(number);
    }

    private static void VisitExpose(
        JsonElement element,
        string? inheritedEndpoint,
        string? inheritedCategory,
        int maximumExposes,
        ref int count,
        List<Zigbee2MqttExpose> result,
        List<DriverEngineeringIssue> issues,
        string ieee)
    {
        if (++count > maximumExposes)
            throw new FormatException($"Zigbee2MQTT device '{ieee}' exceeds the configured expose limit of {maximumExposes}.");
        if (element.ValueKind != JsonValueKind.Object)
            throw new FormatException($"Zigbee2MQTT device '{ieee}' contains a malformed expose.");

        var type = String(element, "type")?.Trim().ToLowerInvariant();
        var category = (String(element, "category") ?? inheritedCategory)?.Trim().ToLowerInvariant();
        var endpoint = String(element, "endpoint") ?? inheritedEndpoint;
        var property = String(element, "property");
        if (endpoint is { Length: > 64 } || endpoint?.Any(char.IsControl) == true)
            throw new FormatException($"Zigbee2MQTT device '{ieee}' contains an invalid expose endpoint identity.");
        if (category is "config" or "diagnostic")
        {
            if (property is not null)
                issues.Add(Warning("Z2M_METADATA_EXPOSE_EXCLUDED", $"Expose '{property}' is category '{category}' and is excluded from process TAGs."));
            return;
        }
        if (type is "action" or "button" || string.Equals(property, "action", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(property, "action_group", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(Warning("Z2M_TRANSIENT_EVENT_EXCLUDED", "Action and button exposes are transient events and are never materialized as persistent TAGs."));
            return;
        }

        if (element.TryGetProperty("features", out var features))
        {
            if (features.ValueKind != JsonValueKind.Array)
                throw new FormatException($"Zigbee2MQTT device '{ieee}' expose features must be an array.");
            foreach (var feature in features.EnumerateArray())
                VisitExpose(feature, endpoint, category, maximumExposes, ref count, result, issues, ieee);
        }

        if (property is null || type is not ("binary" or "numeric")) return;
        if (property.Length > 256 || property.Any(char.IsControl))
        {
            issues.Add(Warning("Z2M_EXPOSE_PROPERTY_INVALID", "An expose has an invalid or oversized property identity and was excluded."));
            return;
        }
        if (!TryGetInt(element, "access", out var access) || access < 0 || (access & ~7) != 0)
        {
            issues.Add(Warning("Z2M_EXPOSE_ACCESS_INVALID", $"Expose '{property}' has unsupported access metadata and was excluded."));
            return;
        }
        if ((access & 1) == 0) return;

        if (type == "binary")
        {
            if (!BooleanCapabilities.TryGetValue(property, out var capability))
            {
                issues.Add(Warning("Z2M_EXPOSE_UNSUPPORTED", $"Binary expose '{property}' is outside the curated v1 state capability set."));
                return;
            }
            string? valueOn = null;
            string? valueOff = null;
            var hasValueOn = TryRawScalar(element, "value_on", out valueOn);
            var hasValueOff = TryRawScalar(element, "value_off", out valueOff);
            if (!hasValueOn || !hasValueOff || JsonEqualText(valueOn, valueOff))
            {
                issues.Add(Warning("Z2M_BINARY_MAPPING_INVALID", $"Binary expose '{property}' requires distinct explicit scalar value_on and value_off metadata."));
                return;
            }
            result.Add(new Zigbee2MqttExpose(property, endpoint, Zigbee2MqttValueKind.Boolean, access, SafeUnit(element), null, null, null, valueOn, valueOff, capability));
            return;
        }

        if (!NumericCapabilities.TryGetValue(property, out var numericCapability))
        {
            issues.Add(Warning("Z2M_EXPOSE_UNSUPPORTED", $"Numeric expose '{property}' is outside the curated v1 state capability set."));
            return;
        }
        var issueCount = issues.Count;
        var minimum = OptionalFinite(element, "value_min", issues, property);
        var maximum = OptionalFinite(element, "value_max", issues, property);
        var step = OptionalFinite(element, "value_step", issues, property);
        if (issues.Count != issueCount) return;
        if (minimum.HasValue && maximum.HasValue && minimum > maximum || step.HasValue && step <= 0)
        {
            issues.Add(Warning("Z2M_NUMERIC_RANGE_INVALID", $"Numeric expose '{property}' has an invalid min/max/step range."));
            return;
        }
        result.Add(new Zigbee2MqttExpose(property, endpoint, Zigbee2MqttValueKind.Double, access, SafeUnit(element), minimum, maximum, step, null, null, numericCapability));
    }

    private static bool JsonEquals(JsonElement actual, string? expectedJson)
    {
        if (expectedJson is null) return false;
        try
        {
            using var expectedDocument = JsonDocument.Parse(expectedJson);
            var expected = expectedDocument.RootElement;
            if (actual.ValueKind != expected.ValueKind) return false;
            return actual.ValueKind switch
            {
                JsonValueKind.String => string.Equals(actual.GetString(), expected.GetString(), StringComparison.Ordinal),
                JsonValueKind.True => true,
                JsonValueKind.False => true,
                JsonValueKind.Number => actual.TryGetDecimal(out var left) && expected.TryGetDecimal(out var right)
                    ? left == right
                    : actual.TryGetDouble(out var ld) && expected.TryGetDouble(out var rd) && ld == rd,
                JsonValueKind.Null => true,
                _ => false
            };
        }
        catch (JsonException) { return false; }
    }

    private static bool JsonEqualText(string? leftJson, string? rightJson)
    {
        if (leftJson is null || rightJson is null) return false;
        try
        {
            using var leftDocument = JsonDocument.Parse(leftJson);
            return JsonEquals(leftDocument.RootElement, rightJson);
        }
        catch (JsonException) { return false; }
    }

    private static bool TryRawScalar(JsonElement element, string name, out string? raw)
    {
        raw = null;
        if (!element.TryGetProperty(name, out var value) || value.ValueKind is JsonValueKind.Object or JsonValueKind.Array or JsonValueKind.Undefined)
            return false;
        raw = value.GetRawText();
        return raw.Length <= 256;
    }

    private static double? OptionalFinite(JsonElement element, string property, List<DriverEngineeringIssue> issues, string expose)
    {
        if (!element.TryGetProperty(property, out var value) || value.ValueKind == JsonValueKind.Null) return null;
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out var number) || !double.IsFinite(number))
        {
            issues.Add(Warning("Z2M_NUMERIC_RANGE_INVALID", $"Numeric expose '{expose}' has a non-finite '{property}' value."));
            return null;
        }
        return number;
    }

    private static string? SafeUnit(JsonElement element)
    {
        var unit = String(element, "unit");
        return unit is null || unit.Length > 64 || unit.Any(char.IsControl) ? null : unit;
    }

    private static void ValidateTopicSegment(string friendlyName, string ieee)
    {
        try { MqttPoint.ValidateExactTopic($"z2m/{friendlyName}/state", nameof(friendlyName)); }
        catch (ArgumentException ex) { throw new FormatException($"Zigbee2MQTT device '{ieee}' friendly_name is not a valid topic segment.", ex); }
    }

    private static string? ParseVersion(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload) || payload.Length > 64 * 1024) return null;
        try
        {
            using var document = JsonDocument.Parse(payload, new JsonDocumentOptions { MaxDepth = 8 });
            return SafeString(document.RootElement, "version");
        }
        catch (JsonException) { return null; }
    }

    private static string? NormalizeBridgeState(string? state)
    {
        if (string.IsNullOrWhiteSpace(state) || state.Length > 32) return null;
        try
        {
            using var document = JsonDocument.Parse(state);
            if (document.RootElement.ValueKind == JsonValueKind.Object)
                state = String(document.RootElement, "state");
        }
        catch (JsonException) { }
        return state?.Trim().ToLowerInvariant() is "online" or "offline" ? state.Trim().ToLowerInvariant() : null;
    }

    private static bool? Bool(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : null;

    private static bool TryGetInt(JsonElement element, string name, out int value)
    {
        if (element.TryGetProperty(name, out var json) && json.ValueKind == JsonValueKind.Number && json.TryGetInt32(out value))
            return true;
        value = 0;
        return false;
    }

    private static string? String(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string? SafeString(JsonElement element, string name)
    {
        var value = String(element, name);
        return value is not null && value.Length <= 256 && !value.Any(char.IsControl) ? value : null;
    }

    private static string? SafeNestedString(JsonElement element, string parent, string name) =>
        element.TryGetProperty(parent, out var nested) ? SafeString(nested, name) : null;

    private static string? String(JsonElement element, string name, bool trim) =>
        String(element, name) is { } value ? trim ? value.Trim() : value : null;

    private static DriverEngineeringIssue Warning(string code, string message) =>
        new(code, DriverEngineeringIssueSeverity.Warning, message);

    private static string SafeSegment(string value) => Uri.EscapeDataString(value);
}
