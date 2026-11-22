using System.Globalization;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;

namespace Scada.Drivers.ESPHome;

public static class EspHomeEntityMapper
{
    public static DriverMaterializationCandidate BuildMaterialization(EspHomeNativeInventory inventory)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        var tags = new List<DriverMaterializationTagCandidate>();
        var commands = new List<DriverMaterializationCommandCandidate>();
        var capabilities = new List<DriverMaterializationCapabilityCandidate>();
        var unmapped = new List<string>();

        foreach (var entity in inventory.Entities.OrderBy(x => x.DeviceId).ThenBy(x => x.Kind).ThenBy(x => x.Key))
        {
            if (!entity.IsProcessEntity)
            {
                unmapped.Add($"{Identity(entity)}:entity-category-{entity.EntityCategory}");
                continue;
            }

            switch (entity.Kind)
            {
                case EspHomeEntityKind.Switch:
                    MapSwitch(entity, tags, commands, capabilities);
                    break;
                case EspHomeEntityKind.BinarySensor:
                    MapBinarySensor(entity, tags, capabilities);
                    break;
                case EspHomeEntityKind.Sensor:
                    MapSensor(entity, tags, capabilities);
                    break;
                case EspHomeEntityKind.TextSensor:
                    AddTag(entity, "state", TagDataType.String, true, null, EspHomeWriteKind.None, tags);
                    break;
                case EspHomeEntityKind.Light:
                    MapLight(entity, tags, commands, capabilities);
                    break;
                case EspHomeEntityKind.Cover:
                    MapCover(entity, tags, commands, capabilities);
                    break;
                case EspHomeEntityKind.Fan:
                    MapFan(entity, tags, commands, capabilities);
                    break;
                case EspHomeEntityKind.Number:
                    MapNumber(entity, tags);
                    break;
                case EspHomeEntityKind.Select:
                    MapSelect(entity, tags);
                    break;
                case EspHomeEntityKind.Button:
                    unmapped.Add($"{Identity(entity)}:parameterless-command-not-representable-with-current-write-tag-command-contract");
                    break;
                case EspHomeEntityKind.Climate:
                    unmapped.Add($"{Identity(entity)}:climate-traits-deferred");
                    break;
                case EspHomeEntityKind.Lock:
                    unmapped.Add($"{Identity(entity)}:lock-semantics-deferred");
                    break;
                default:
                    unmapped.Add($"{Identity(entity)}:unsupported");
                    break;
            }
        }

        if (inventory.UnsupportedEntityMessageCount > 0)
            unmapped.Add($"unknown-message-types:{inventory.UnsupportedEntityMessageCount}");

        var safeId = SanitizeSegment(inventory.Device.StableDeviceIdentity);
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["manufacturer"] = inventory.Device.Manufacturer,
            ["model"] = inventory.Device.Model,
            ["firmware"] = inventory.Device.ESPHomeVersion,
            ["mac"] = inventory.Device.OriginalMacAddress,
            ["projectName"] = inventory.Device.ProjectName,
            ["projectVersion"] = inventory.Device.ProjectVersion,
            ["apiVersion"] = inventory.NegotiatedVersion.ToString(),
            ["hasDeepSleep"] = inventory.Device.HasDeepSleep.ToString(CultureInfo.InvariantCulture).ToLowerInvariant(),
            ["unmappedEntityCount"] = unmapped.Count.ToString(CultureInfo.InvariantCulture)
        };
        if (unmapped.Count > 0)
            metadata["unmappedEntities"] = string.Join(",", unmapped);

        return new DriverMaterializationCandidate(
            new DriverMaterializationEquipmentCandidate(
                $"esphome-{safeId}",
                $"Home/ESPHome/{safeId}",
                string.IsNullOrWhiteSpace(inventory.Device.FriendlyName)
                    ? inventory.Device.Name
                    : inventory.Device.FriendlyName,
                inventory.Device.StableDeviceIdentity,
                SourceRole: "primary",
                LocationPlaceholder: "Home",
                Capabilities: capabilities,
                Metadata: metadata),
            tags,
            commands);
    }

    public static IReadOnlyCollection<EspHomePoint> BuildRuntimePoints(
        IReadOnlyCollection<TagDefinition> tags,
        IReadOnlyCollection<CommunicationTagBinding> bindings)
    {
        if (tags.Count != bindings.Count)
            throw new ArgumentException("ESPHome tags and bindings must have matching cardinality.");

        var points = new List<EspHomePoint>(tags.Count);
        using var tagEnumerator = tags.GetEnumerator();
        using var bindingEnumerator = bindings.GetEnumerator();
        while (tagEnumerator.MoveNext() && bindingEnumerator.MoveNext())
        {
            var tag = tagEnumerator.Current;
            var binding = bindingEnumerator.Current;
            if (!EspHomeEntityAddress.TryParse(binding.PortableAddress, out var address))
                throw new ArgumentException($"ESPHome address '{binding.PortableAddress}' is invalid.");

            var writeKind = EspHomeWriteKind.None;
            if (!tag.ReadOnly)
            {
                if (!binding.EffectiveSettings.TryGetValue("writeKind", out var raw) ||
                    !Enum.TryParse<EspHomeWriteKind>(raw, true, out writeKind) ||
                    writeKind == EspHomeWriteKind.None)
                    throw new ArgumentException($"Writable ESPHome TAG '{tag.Path}' requires a valid writeKind.");
            }

            points.Add(new EspHomePoint(tag, address, writeKind));
        }

        return points;
    }

    private static void MapSwitch(
        EspHomeEntityDescriptor entity,
        List<DriverMaterializationTagCandidate> tags,
        List<DriverMaterializationCommandCandidate> commands,
        List<DriverMaterializationCapabilityCandidate> capabilities)
    {
        var state = AddTag(entity, "state", TagDataType.Boolean, false, null, EspHomeWriteKind.SwitchState, tags);
        var on = AddCommand(entity, "on", "Turn on", "true", state.CandidateId, commands);
        var off = AddCommand(entity, "off", "Turn off", "false", state.CandidateId, commands);
        capabilities.Add(new DriverMaterializationCapabilityCandidate(
            $"{Identity(entity)}.onoff",
            "OnOff",
            [new("state", state.CandidateId), new("turnOn", CommandCandidateId: on.CandidateId), new("turnOff", CommandCandidateId: off.CandidateId)]));
    }

    private static void MapBinarySensor(
        EspHomeEntityDescriptor entity,
        List<DriverMaterializationTagCandidate> tags,
        List<DriverMaterializationCapabilityCandidate> capabilities)
    {
        var state = AddTag(entity, "state", TagDataType.Boolean, true, null, EspHomeWriteKind.None, tags);
        var capability = BinaryCapability(entity.DeviceClass);
        if (capability is not null)
            capabilities.Add(new DriverMaterializationCapabilityCandidate(
                $"{Identity(entity)}.{capability.ToLowerInvariant()}",
                capability,
                [new("state", state.CandidateId)]));
    }

    private static void MapSensor(
        EspHomeEntityDescriptor entity,
        List<DriverMaterializationTagCandidate> tags,
        List<DriverMaterializationCapabilityCandidate> capabilities)
    {
        var state = AddTag(entity, "state", TagDataType.Double, true, entity.Unit, EspHomeWriteKind.None, tags);
        var capability = SensorCapability(entity.DeviceClass);
        if (capability is not null)
            capabilities.Add(new DriverMaterializationCapabilityCandidate(
                $"{Identity(entity)}.{capability.ToLowerInvariant()}",
                capability,
                [new("value", state.CandidateId)]));
    }

    private static void MapLight(
        EspHomeEntityDescriptor entity,
        List<DriverMaterializationTagCandidate> tags,
        List<DriverMaterializationCommandCandidate> commands,
        List<DriverMaterializationCapabilityCandidate> capabilities)
    {
        var state = AddTag(entity, "state", TagDataType.Boolean, false, null, EspHomeWriteKind.LightState, tags);
        var on = AddCommand(entity, "on", "Turn on", "true", state.CandidateId, commands);
        var off = AddCommand(entity, "off", "Turn off", "false", state.CandidateId, commands);
        capabilities.Add(new DriverMaterializationCapabilityCandidate(
            $"{Identity(entity)}.light",
            "Light",
            [new("state", state.CandidateId), new("turnOn", CommandCandidateId: on.CandidateId), new("turnOff", CommandCandidateId: off.CandidateId)]));

        if (entity.SupportsBrightness)
        {
            var brightness = AddTag(entity, "brightness", TagDataType.Double, false, "%", EspHomeWriteKind.LightBrightness, tags);
            capabilities.Add(new DriverMaterializationCapabilityCandidate(
                $"{Identity(entity)}.dimmer",
                "Dimmer",
                [new("level", brightness.CandidateId)]));
        }

        if (entity.SupportsRgb)
        {
            // RGB channel writes require an atomic three-channel command in the
            // Native API. Until the canonical TAG write contract can carry that
            // compound write without fabricating missing channels, publish the
            // authoritative channels read-only and still expose ColorLight traits.
            var red = AddTag(entity, "red", TagDataType.Double, true, "%", EspHomeWriteKind.None, tags);
            var green = AddTag(entity, "green", TagDataType.Double, true, "%", EspHomeWriteKind.None, tags);
            var blue = AddTag(entity, "blue", TagDataType.Double, true, "%", EspHomeWriteKind.None, tags);
            capabilities.Add(new DriverMaterializationCapabilityCandidate(
                $"{Identity(entity)}.color",
                "ColorLight",
                [new("red", red.CandidateId), new("green", green.CandidateId), new("blue", blue.CandidateId)]));
        }
    }

    private static void MapCover(
        EspHomeEntityDescriptor entity,
        List<DriverMaterializationTagCandidate> tags,
        List<DriverMaterializationCommandCandidate> commands,
        List<DriverMaterializationCapabilityCandidate> capabilities)
    {
        var bindings = new List<DriverMaterializationRoleBinding>();
        if (entity.SupportsPosition)
        {
            var position = AddTag(entity, "position", TagDataType.Double, false, "%", EspHomeWriteKind.CoverPosition, tags);
            bindings.Add(new("position", position.CandidateId));
            var open = AddCommand(entity, "open", "Open", "100", position.CandidateId, commands);
            var close = AddCommand(entity, "close", "Close", "0", position.CandidateId, commands);
            bindings.Add(new("open", CommandCandidateId: open.CandidateId));
            bindings.Add(new("close", CommandCandidateId: close.CandidateId));
        }

        var operation = AddTag(entity, "operation", TagDataType.String, true, null, EspHomeWriteKind.None, tags);
        bindings.Add(new("state", operation.CandidateId));
        capabilities.Add(new DriverMaterializationCapabilityCandidate(
            $"{Identity(entity)}.cover", "Cover", bindings));
    }

    private static void MapFan(
        EspHomeEntityDescriptor entity,
        List<DriverMaterializationTagCandidate> tags,
        List<DriverMaterializationCommandCandidate> commands,
        List<DriverMaterializationCapabilityCandidate> capabilities)
    {
        var bindings = new List<DriverMaterializationRoleBinding>();
        var state = AddTag(entity, "state", TagDataType.Boolean, false, null, EspHomeWriteKind.FanState, tags);
        var on = AddCommand(entity, "on", "Turn on", "true", state.CandidateId, commands);
        var off = AddCommand(entity, "off", "Turn off", "false", state.CandidateId, commands);
        bindings.Add(new("state", state.CandidateId));
        bindings.Add(new("turnOn", CommandCandidateId: on.CandidateId));
        bindings.Add(new("turnOff", CommandCandidateId: off.CandidateId));

        if (entity.SupportsFanSpeed)
        {
            var speed = AddTag(entity, "speedLevel", TagDataType.Int32, false, null, EspHomeWriteKind.FanSpeedLevel, tags,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["minimum"] = "0",
                    ["maximum"] = entity.SupportedFanSpeedCount.ToString(CultureInfo.InvariantCulture)
                });
            bindings.Add(new("speed", speed.CandidateId));
        }

        capabilities.Add(new DriverMaterializationCapabilityCandidate(
            $"{Identity(entity)}.fan", "Fan", bindings));
    }

    private static void MapNumber(
        EspHomeEntityDescriptor entity,
        List<DriverMaterializationTagCandidate> tags)
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal);
        if (entity.MinValue.HasValue) metadata["minimum"] = entity.MinValue.Value.ToString("R", CultureInfo.InvariantCulture);
        if (entity.MaxValue.HasValue) metadata["maximum"] = entity.MaxValue.Value.ToString("R", CultureInfo.InvariantCulture);
        if (entity.Step.HasValue) metadata["step"] = entity.Step.Value.ToString("R", CultureInfo.InvariantCulture);
        AddTag(entity, "state", TagDataType.Double, false, entity.Unit, EspHomeWriteKind.NumberState, tags, metadata);
    }

    private static void MapSelect(
        EspHomeEntityDescriptor entity,
        List<DriverMaterializationTagCandidate> tags)
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal);
        if (entity.Options is { Count: > 0 })
            metadata["options"] = string.Join("|", entity.Options);
        AddTag(entity, "state", TagDataType.String, false, null, EspHomeWriteKind.SelectState, tags, metadata);
    }

    private static DriverMaterializationTagCandidate AddTag(
        EspHomeEntityDescriptor entity,
        string field,
        TagDataType dataType,
        bool readOnly,
        string? unit,
        EspHomeWriteKind writeKind,
        List<DriverMaterializationTagCandidate> tags,
        IReadOnlyDictionary<string, string>? extraMetadata = null)
    {
        var address = new EspHomeEntityAddress(entity.Kind, entity.DeviceId, entity.Key, field);
        var candidateId = address.ToString();
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["entityKind"] = entity.Kind.ToString(),
            ["entityKey"] = entity.Key.ToString("X8", CultureInfo.InvariantCulture),
            ["deviceId"] = entity.DeviceId.ToString(CultureInfo.InvariantCulture),
            ["objectId"] = entity.ObjectId,
            ["deviceClass"] = entity.DeviceClass ?? string.Empty
        };
        if (writeKind != EspHomeWriteKind.None)
            metadata["writeKind"] = writeKind.ToString();
        if (extraMetadata is not null)
            foreach (var item in extraMetadata) metadata[item.Key] = item.Value;

        var tag = new DriverMaterializationTagCandidate(
            candidateId,
            string.IsNullOrWhiteSpace(entity.Name) ? $"{entity.ObjectId} {field}" : $"{entity.Name} {field}",
            $"ESPHome/{SanitizeSegment(entity.Name.Length == 0 ? entity.ObjectId : entity.Name)}/{field}",
            dataType,
            PortableAddress: candidateId,
            ReadOnly: readOnly,
            EngineeringUnit: unit,
            Metadata: metadata);
        tags.Add(tag);
        return tag;
    }

    private static DriverMaterializationCommandCandidate AddCommand(
        EspHomeEntityDescriptor entity,
        string suffix,
        string name,
        string value,
        string targetTagCandidateId,
        List<DriverMaterializationCommandCandidate> commands)
    {
        var id = $"{Identity(entity)}.command.{suffix}";
        var command = new DriverMaterializationCommandCandidate(
            id,
            id,
            $"{entity.Name} {name}".Trim(),
            value,
            targetTagCandidateId);
        commands.Add(command);
        return command;
    }

    private static string? SensorCapability(string? deviceClass) =>
        deviceClass?.Trim().ToLowerInvariant() switch
        {
            "temperature" => "Temperature",
            "humidity" => "Humidity",
            "illuminance" => "Illuminance",
            "power" => "Power",
            "energy" => "Energy",
            "voltage" => "Voltage",
            "current" => "Current",
            "battery" => "Battery",
            _ => null
        };

    private static string? BinaryCapability(string? deviceClass) =>
        deviceClass?.Trim().ToLowerInvariant() switch
        {
            "door" or "window" or "opening" or "garage_door" => "Contact",
            "occupancy" or "presence" => "Occupancy",
            "motion" => "Motion",
            "moisture" => "Leak",
            "smoke" => "Smoke",
            _ => null
        };

    private static string Identity(EspHomeEntityDescriptor entity) =>
        $"{entity.Kind.ToString().ToLowerInvariant()}:{entity.DeviceId}:{entity.Key:X8}";

    private static string SanitizeSegment(string value)
    {
        var chars = value.Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '-').ToArray();
        return new string(chars);
    }
}
