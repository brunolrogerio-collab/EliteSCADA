using System.Globalization;
using System.Text.Json;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;

namespace Scada.Drivers.HomeAssistant;

public sealed record HomeAssistantMaterializationSet(
    IReadOnlyCollection<DriverMaterializationCandidate> Candidates,
    IReadOnlyCollection<DriverEngineeringIssue> Issues);

public static class HomeAssistantEntityMapper
{
    private const int CoverOpen = 1;
    private const int CoverClose = 2;
    private const int CoverSetPosition = 4;

    private static readonly IReadOnlyDictionary<string, string> SensorCapabilities =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["temperature"] = "Temperature",
            ["humidity"] = "Humidity",
            ["illuminance"] = "Illuminance",
            ["power"] = "Power",
            ["energy"] = "Energy",
            ["voltage"] = "Voltage",
            ["current"] = "Current",
            ["battery"] = "Battery"
        };

    private static readonly IReadOnlyDictionary<string, string> BinarySensorCapabilities =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["door"] = "Contact",
            ["garage_door"] = "Contact",
            ["opening"] = "Contact",
            ["window"] = "Contact",
            ["motion"] = "Motion",
            ["occupancy"] = "Occupancy",
            ["presence"] = "Occupancy",
            ["moisture"] = "Leak",
            ["smoke"] = "Smoke"
        };

    private static readonly HashSet<string> ExplicitlyDeferredDomains = new(StringComparer.Ordinal)
    {
        "fan", "climate"
    };

    private static readonly HashSet<string> ExplicitlyExcludedDomains = new(StringComparer.Ordinal)
    {
        "lock",
        "input_boolean", "input_number", "input_select", "input_text",
        "automation", "script", "scene", "update", "button", "select", "number"
    };

    public static HomeAssistantMaterializationSet Build(
        string dataSourceKey,
        HomeAssistantSelectedInventory inventory)
    {
        if (string.IsNullOrWhiteSpace(dataSourceKey))
            throw new ArgumentException("Home Assistant Data Source key is required.", nameof(dataSourceKey));
        ArgumentNullException.ThrowIfNull(inventory);

        var issues = new List<DriverEngineeringIssue>(inventory.Issues);
        var candidates = new List<DriverMaterializationCandidate>();
        var mappedAny = false;

        foreach (var group in inventory.Entities.GroupBy(
                     entity => string.IsNullOrWhiteSpace(entity.DeviceId)
                         ? "logical"
                         : $"device:{entity.DeviceId}",
                     StringComparer.Ordinal))
        {
            var tags = new List<DriverMaterializationTagCandidate>();
            var commands = new List<DriverMaterializationCommandCandidate>();
            var capabilities = new List<DriverMaterializationCapabilityCandidate>();
            var groupEntities = group.ToArray();

            foreach (var entity in groupEntities)
            {
                if (entity.Registry?.EntityCategory is "config" or "diagnostic")
                {
                    issues.Add(Issue(
                        "HA_ENTITY_CATEGORY_EXCLUDED",
                        entity.EntityId,
                        $"Selected Home Assistant entity '{entity.EntityId}' is {entity.Registry.EntityCategory} metadata/configuration and is not projected as process state."));
                    continue;
                }

                if (ExplicitlyDeferredDomains.Contains(entity.Domain))
                {
                    issues.Add(Issue(
                        "DEFERRED_ENTITY_SCOPE",
                        entity.EntityId,
                        $"Home Assistant domain '{entity.Domain}' is deferred from the bounded first release."));
                    continue;
                }

                if (ExplicitlyExcludedDomains.Contains(entity.Domain))
                {
                    issues.Add(Issue(
                        entity.Domain == "lock" ? "HA_LOCK_OUT_OF_SCOPE" : "HA_NON_PROCESS_ENTITY_EXCLUDED",
                        entity.EntityId,
                        $"Home Assistant domain '{entity.Domain}' is not projected as a persistent process TAG in this release."));
                    continue;
                }

                var mapped = entity.Domain switch
                {
                    "switch" => MapSwitch(entity, tags, commands, capabilities),
                    "light" => MapLight(entity, tags, commands, capabilities),
                    "sensor" => MapSensor(entity, tags, capabilities),
                    "binary_sensor" => MapBinarySensor(entity, tags, capabilities),
                    "cover" => MapCover(entity, tags, commands, capabilities),
                    _ => false
                };

                if (!mapped)
                {
                    issues.Add(Issue(
                        "HA_DOMAIN_UNMAPPED",
                        entity.EntityId,
                        $"Home Assistant entity '{entity.EntityId}' uses unsupported domain '{entity.Domain}'."));
                }
                else
                {
                    mappedAny = true;
                }
            }

            if (tags.Count == 0 && commands.Count == 0)
                continue;

            var deviceId = groupEntities.Select(x => x.DeviceId).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
            var areaIds = groupEntities
                .Select(x => x.AreaId)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.Ordinal)
                .Cast<string>()
                .ToArray();
            var safeSource = SanitizeSegment(dataSourceKey);
            var groupId = deviceId is null ? "logical" : $"device-{SanitizeSegment(deviceId)}";
            var stableEquipmentIdentity = deviceId is null
                ? $"homeassistant:{dataSourceKey}:logical"
                : $"homeassistant:{dataSourceKey}:device:{deviceId}";
            var equipmentMetadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["source"] = dataSourceKey,
                ["physicalIdentity"] = deviceId is null ? "false" : "ha-device-registry-id",
                ["entityIdentity"] = "entity_id_mutable"
            };
            if (deviceId is not null) equipmentMetadata["haDeviceId"] = deviceId;
            if (areaIds.Length == 1) equipmentMetadata["haAreaId"] = areaIds[0];

            var equipmentName = deviceId is null
                ? "Home Assistant logical entities"
                : $"Home Assistant device {Short(deviceId)}";

            candidates.Add(new DriverMaterializationCandidate(
                new DriverMaterializationEquipmentCandidate(
                    $"ha-{safeSource}-{groupId}",
                    $"HomeAssistant/{safeSource}/{groupId}",
                    equipmentName,
                    stableEquipmentIdentity,
                    SourceRole: deviceId is null ? "logical" : "primary",
                    LocationPlaceholder: areaIds.Length == 1 ? $"HA Area:{areaIds[0]}" : null,
                    Capabilities: capabilities,
                    Metadata: equipmentMetadata),
                tags,
                commands));
        }

        if (mappedAny)
        {
            issues.Add(new DriverEngineeringIssue(
                "HA_STABLE_ENTITY_IDENTITY_UNAVAILABLE",
                DriverEngineeringIssueSeverity.Warning,
                "The authorized Home Assistant list_for_display contract does not expose registry unique_id; entity_id bindings are explicitly rename-unstable until Main approves a richer supported registry contract."));
        }

        return new HomeAssistantMaterializationSet(candidates, issues);
    }

    private static bool MapSwitch(
        HomeAssistantSelectedEntity entity,
        List<DriverMaterializationTagCandidate> tags,
        List<DriverMaterializationCommandCandidate> commands,
        List<DriverMaterializationCapabilityCandidate> capabilities)
    {
        var state = AddTag(entity, "state", TagDataType.Boolean, readOnly: false, null, tags,
            ("writeServiceDomain", "switch"),
            ("writeOnService", "turn_on"),
            ("writeOffService", "turn_off"));
        var on = AddCommand(entity, "on", "Turn on", "true", state.CandidateId, "switch", "turn_on", commands);
        var off = AddCommand(entity, "off", "Turn off", "false", state.CandidateId, "switch", "turn_off", commands);
        capabilities.Add(new DriverMaterializationCapabilityCandidate(
            $"{EntityKey(entity)}.onoff",
            "OnOff",
            [new("state", state.CandidateId), new("turnOn", CommandCandidateId: on.CandidateId), new("turnOff", CommandCandidateId: off.CandidateId)]));
        return true;
    }

    private static bool MapLight(
        HomeAssistantSelectedEntity entity,
        List<DriverMaterializationTagCandidate> tags,
        List<DriverMaterializationCommandCandidate> commands,
        List<DriverMaterializationCapabilityCandidate> capabilities)
    {
        var state = AddTag(entity, "state", TagDataType.Boolean, readOnly: false, null, tags,
            ("writeServiceDomain", "light"),
            ("writeOnService", "turn_on"),
            ("writeOffService", "turn_off"));
        var on = AddCommand(entity, "on", "Turn on", "true", state.CandidateId, "light", "turn_on", commands);
        var off = AddCommand(entity, "off", "Turn off", "false", state.CandidateId, "light", "turn_off", commands);
        capabilities.Add(new DriverMaterializationCapabilityCandidate(
            $"{EntityKey(entity)}.light",
            "Light",
            [new("state", state.CandidateId), new("turnOn", CommandCandidateId: on.CandidateId), new("turnOff", CommandCandidateId: off.CandidateId)]));

        var colorModes = StringArray(entity.State.Attributes, "supported_color_modes");
        var supportsBrightness = colorModes.Any(mode => !string.Equals(mode, "onoff", StringComparison.OrdinalIgnoreCase)) ||
                                 Has(entity.State.Attributes, "brightness");
        if (supportsBrightness)
        {
            var brightness = AddTag(entity, "brightness", TagDataType.Double, readOnly: false, "%", tags,
                ("haAttribute", "brightness"),
                ("sourceScale", "1..255"),
                ("engineeringScale", "0..100"),
                ("writeServiceDomain", "light"),
                ("writeService", "turn_on"),
                ("writeParameter", "brightness_pct"));
            capabilities.Add(new DriverMaterializationCapabilityCandidate(
                $"{EntityKey(entity)}.dimmer", "Dimmer", [new("level", brightness.CandidateId)]));
        }

        var colorCapable = colorModes.Any(mode => mode is "hs" or "rgb" or "rgbw" or "rgbww" or "xy") ||
                           Has(entity.State.Attributes, "rgb_color");
        if (colorCapable)
        {
            var color = AddTag(entity, "rgb_color", TagDataType.String, readOnly: false, null, tags,
                ("haAttribute", "rgb_color"),
                ("valueEncoding", "rgb-0-255-triplet"),
                ("writeServiceDomain", "light"),
                ("writeService", "turn_on"),
                ("writeParameter", "rgb_color"),
                ("supportedColorModes", string.Join(",", colorModes)));
            capabilities.Add(new DriverMaterializationCapabilityCandidate(
                $"{EntityKey(entity)}.color", "ColorLight",
                [new("state", state.CandidateId), new("color", color.CandidateId)]));
        }

        return true;
    }

    private static bool MapSensor(
        HomeAssistantSelectedEntity entity,
        List<DriverMaterializationTagCandidate> tags,
        List<DriverMaterializationCapabilityCandidate> capabilities)
    {
        var deviceClass = GetString(entity.State.Attributes, "device_class");
        var unit = GetString(entity.State.Attributes, "unit_of_measurement");
        var hasSemanticNumericClass = deviceClass is not null && SensorCapabilities.ContainsKey(deviceClass);
        var type = hasSemanticNumericClass || double.TryParse(entity.State.State, NumberStyles.Float, CultureInfo.InvariantCulture, out _)
            ? TagDataType.Double
            : TagDataType.String;
        var tag = AddTag(entity, "state", type, readOnly: true, unit, tags,
            ("deviceClass", deviceClass ?? string.Empty));

        if (deviceClass is not null && SensorCapabilities.TryGetValue(deviceClass, out var capability))
        {
            capabilities.Add(new DriverMaterializationCapabilityCandidate(
                $"{EntityKey(entity)}.{deviceClass}", capability, [new("value", tag.CandidateId)]));
        }
        return true;
    }

    private static bool MapBinarySensor(
        HomeAssistantSelectedEntity entity,
        List<DriverMaterializationTagCandidate> tags,
        List<DriverMaterializationCapabilityCandidate> capabilities)
    {
        var deviceClass = GetString(entity.State.Attributes, "device_class");
        var tag = AddTag(entity, "state", TagDataType.Boolean, readOnly: true, null, tags,
            ("deviceClass", deviceClass ?? string.Empty));
        if (deviceClass is not null && BinarySensorCapabilities.TryGetValue(deviceClass, out var capability))
        {
            capabilities.Add(new DriverMaterializationCapabilityCandidate(
                $"{EntityKey(entity)}.{deviceClass}", capability, [new("state", tag.CandidateId)]));
        }
        return true;
    }

    private static bool MapCover(
        HomeAssistantSelectedEntity entity,
        List<DriverMaterializationTagCandidate> tags,
        List<DriverMaterializationCommandCandidate> commands,
        List<DriverMaterializationCapabilityCandidate> capabilities)
    {
        var features = GetInt(entity.State.Attributes, "supported_features");
        var bindings = new List<DriverMaterializationRoleBinding>();
        var state = AddTag(entity, "state", TagDataType.String, readOnly: true, null, tags,
            ("deviceClass", GetString(entity.State.Attributes, "device_class") ?? string.Empty));
        bindings.Add(new DriverMaterializationRoleBinding("state", state.CandidateId));

        if (Has(entity.State.Attributes, "current_position"))
        {
            var writable = (features & CoverSetPosition) != 0;
            var position = AddTag(entity, "current_position", TagDataType.Double, readOnly: !writable, "%", tags,
                ("haAttribute", "current_position"),
                ("writeServiceDomain", writable ? "cover" : string.Empty),
                ("writeService", writable ? "set_cover_position" : string.Empty),
                ("writeParameter", writable ? "position" : string.Empty));
            bindings.Add(new DriverMaterializationRoleBinding("position", position.CandidateId));
        }

        if ((features & CoverOpen) != 0)
        {
            var open = AddCommand(entity, "open", "Open", "open", state.CandidateId, "cover", "open_cover", commands);
            bindings.Add(new DriverMaterializationRoleBinding("open", CommandCandidateId: open.CandidateId));
        }
        if ((features & CoverClose) != 0)
        {
            var close = AddCommand(entity, "close", "Close", "closed", state.CandidateId, "cover", "close_cover", commands);
            bindings.Add(new DriverMaterializationRoleBinding("close", CommandCandidateId: close.CandidateId));
        }

        capabilities.Add(new DriverMaterializationCapabilityCandidate(
            $"{EntityKey(entity)}.cover", "Cover", bindings,
            Metadata: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["haSupportedFeatures"] = features.ToString(CultureInfo.InvariantCulture)
            }));
        return true;
    }

    private static DriverMaterializationTagCandidate AddTag(
        HomeAssistantSelectedEntity entity,
        string field,
        TagDataType dataType,
        bool readOnly,
        string? unit,
        List<DriverMaterializationTagCandidate> tags,
        params (string Key, string Value)[] metadataValues)
    {
        var entityKey = EntityKey(entity);
        var metadata = BaseMetadata(entity);
        metadata["haField"] = field;
        foreach (var pair in metadataValues)
            if (!string.IsNullOrEmpty(pair.Value))
                metadata[pair.Key] = pair.Value;

        var tag = new DriverMaterializationTagCandidate(
            $"{entityKey}.{field}",
            $"{entity.DisplayName} {field}",
            $"HomeAssistant/{entityKey}/{SanitizeSegment(field)}",
            dataType,
            PortableAddress: field == "state"
                ? $"entity:{entity.EntityId}:state"
                : $"entity:{entity.EntityId}:attribute:{field}",
            ReadOnly: readOnly,
            EngineeringUnit: unit,
            Metadata: metadata);
        tags.Add(tag);
        return tag;
    }

    private static DriverMaterializationCommandCandidate AddCommand(
        HomeAssistantSelectedEntity entity,
        string suffix,
        string name,
        string value,
        string targetTagCandidateId,
        string serviceDomain,
        string service,
        List<DriverMaterializationCommandCandidate> commands)
    {
        var command = new DriverMaterializationCommandCandidate(
            $"{EntityKey(entity)}.command.{suffix}",
            $"{entity.EntityId}.{suffix}",
            name,
            value,
            targetTagCandidateId,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["haEntityId"] = entity.EntityId,
                ["serviceDomain"] = serviceDomain,
                ["service"] = service,
                ["identityStability"] = "entity_id_mutable"
            });
        commands.Add(command);
        return command;
    }

    private static Dictionary<string, string> BaseMetadata(HomeAssistantSelectedEntity entity)
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["haEntityId"] = entity.EntityId,
            ["haDomain"] = entity.Domain,
            ["identityStability"] = "entity_id_mutable"
        };
        if (!string.IsNullOrWhiteSpace(entity.Platform)) metadata["haPlatform"] = entity.Platform!;
        if (!string.IsNullOrWhiteSpace(entity.DeviceId)) metadata["haDeviceId"] = entity.DeviceId!;
        if (!string.IsNullOrWhiteSpace(entity.AreaId)) metadata["haAreaId"] = entity.AreaId!;
        return metadata;
    }

    private static DriverEngineeringIssue Issue(string code, string entityId, string message) =>
        new(code, DriverEngineeringIssueSeverity.Warning, message, entityId);

    private static bool Has(JsonElement attributes, string name) =>
        attributes.ValueKind == JsonValueKind.Object &&
        attributes.TryGetProperty(name, out var value) &&
        value.ValueKind != JsonValueKind.Null;

    private static string? GetString(JsonElement attributes, string name) =>
        attributes.ValueKind == JsonValueKind.Object &&
        attributes.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int GetInt(JsonElement attributes, string name) =>
        attributes.ValueKind == JsonValueKind.Object &&
        attributes.TryGetProperty(name, out var value) &&
        value.TryGetInt32(out var parsed)
            ? parsed
            : 0;

    private static IReadOnlyList<string> StringArray(JsonElement attributes, string name)
    {
        if (attributes.ValueKind != JsonValueKind.Object ||
            !attributes.TryGetProperty(name, out var value) ||
            value.ValueKind != JsonValueKind.Array)
            return Array.Empty<string>();

        return value.EnumerateArray()
            .Where(x => x.ValueKind == JsonValueKind.String && x.GetString() is not null)
            .Select(x => x.GetString()!)
            .ToArray();
    }

    private static string EntityKey(HomeAssistantSelectedEntity entity) =>
        SanitizeSegment(entity.EntityId.Replace('.', '-'));

    private static string SanitizeSegment(string value)
    {
        var chars = value.Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '-').ToArray();
        return new string(chars);
    }

    private static string Short(string value) => value.Length <= 8 ? value : value[..8];
}