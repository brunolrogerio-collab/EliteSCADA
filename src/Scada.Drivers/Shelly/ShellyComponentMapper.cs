using System.Text.Json;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;

namespace Scada.Drivers.Shelly;

public static class ShellyComponentMapper
{
    public static DriverMaterializationCandidate BuildMaterialization(
        ShellyDeviceInfo device,
        JsonElement componentsResult,
        JsonElement fullStatus)
    {
        var tags = new List<DriverMaterializationTagCandidate>();
        var commands = new List<DriverMaterializationCommandCandidate>();
        var capabilities = new List<DriverMaterializationCapabilityCandidate>();
        var unmapped = new List<string>();

        foreach (var component in EnumerateComponentKeys(componentsResult, fullStatus))
        {
            if (!fullStatus.TryGetProperty(component, out var status) || status.ValueKind != JsonValueKind.Object)
            {
                unmapped.Add(component);
                continue;
            }

            if (component.StartsWith("switch:", StringComparison.Ordinal))
                MapSwitch(component, status, tags, commands, capabilities);
            else if (component.StartsWith("light:", StringComparison.Ordinal))
                MapLight(component, status, tags, commands, capabilities);
            else if (component.StartsWith("cover:", StringComparison.Ordinal))
                MapCover(component, status, tags, commands, capabilities);
            else if (component.StartsWith("temperature:", StringComparison.Ordinal))
                MapScalar(component, status, "tC", TagDataType.Double, "°C", "Temperature", "value", tags, capabilities);
            else if (component.StartsWith("humidity:", StringComparison.Ordinal))
                MapScalar(component, status, "rh", TagDataType.Double, "%", "Humidity", "value", tags, capabilities);
            else if (component.StartsWith("input:", StringComparison.Ordinal) &&
                     status.TryGetProperty("state", out var inputState) &&
                     inputState.ValueKind is JsonValueKind.True or JsonValueKind.False)
                MapScalar(component, status, "state", TagDataType.Boolean, null, "BinaryInput", "state", tags, capabilities);
            else if (IsFunctionalComponent(component))
                unmapped.Add(component);
        }

        var safeId = SanitizeSegment(device.StableDeviceIdentity);
        var equipmentMetadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["manufacturer"] = "Shelly",
            ["model"] = device.Model,
            ["firmware"] = device.Firmware ?? string.Empty,
            ["mac"] = device.Mac ?? string.Empty
        };
        if (unmapped.Count > 0)
            equipmentMetadata["unmappedComponents"] = string.Join(",", unmapped.OrderBy(x => x, StringComparer.Ordinal));

        return new DriverMaterializationCandidate(
            new DriverMaterializationEquipmentCandidate(
                $"shelly-{safeId}",
                $"Home/Shelly/{safeId}",
                device.Name ?? device.StableDeviceIdentity,
                device.StableDeviceIdentity,
                SourceRole: "primary",
                LocationPlaceholder: "Home",
                Capabilities: capabilities,
                Metadata: equipmentMetadata),
            tags,
            commands);
    }

    private static void MapSwitch(
        string key,
        JsonElement status,
        List<DriverMaterializationTagCandidate> tags,
        List<DriverMaterializationCommandCandidate> commands,
        List<DriverMaterializationCapabilityCandidate> capabilities)
    {
        var bindings = new List<DriverMaterializationRoleBinding>();
        if (Has(status, "output"))
        {
            var tag = AddTag(key, "output", TagDataType.Boolean, readOnly: false, null, "Switch.Set", "on", tags);
            var on = AddCommand(key, "on", "Turn on", "true", tag.CandidateId, commands);
            var off = AddCommand(key, "off", "Turn off", "false", tag.CandidateId, commands);
            bindings.Add(new("state", tag.CandidateId));
            bindings.Add(new("turnOn", CommandCandidateId: on.CandidateId));
            bindings.Add(new("turnOff", CommandCandidateId: off.CandidateId));
            capabilities.Add(new DriverMaterializationCapabilityCandidate($"{key}.onoff", "OnOff", bindings));
        }

        MapMeasurement(key, status, "apower", "Power", "W", tags, capabilities);
        MapMeasurement(key, status, "voltage", "Voltage", "V", tags, capabilities);
        MapMeasurement(key, status, "current", "Current", "A", tags, capabilities);
        if (TryPath(status, "aenergy.total"))
        {
            var energy = AddTag(key, "aenergy.total", TagDataType.Double, true, "Wh", null, null, tags);
            capabilities.Add(new DriverMaterializationCapabilityCandidate(
                $"{key}.energy", "Energy", [new("value", energy.CandidateId)]));
        }
    }

    private static void MapLight(
        string key,
        JsonElement status,
        List<DriverMaterializationTagCandidate> tags,
        List<DriverMaterializationCommandCandidate> commands,
        List<DriverMaterializationCapabilityCandidate> capabilities)
    {
        if (Has(status, "output"))
        {
            var output = AddTag(key, "output", TagDataType.Boolean, false, null, "Light.Set", "on", tags);
            var on = AddCommand(key, "on", "Turn on", "true", output.CandidateId, commands);
            var off = AddCommand(key, "off", "Turn off", "false", output.CandidateId, commands);
            capabilities.Add(new DriverMaterializationCapabilityCandidate(
                $"{key}.light", "Light",
                [new("state", output.CandidateId), new("turnOn", CommandCandidateId: on.CandidateId), new("turnOff", CommandCandidateId: off.CandidateId)]));
        }

        if (Has(status, "brightness"))
        {
            var brightness = AddTag(key, "brightness", TagDataType.Double, false, "%", "Light.Set", "brightness", tags);
            capabilities.Add(new DriverMaterializationCapabilityCandidate(
                $"{key}.dimmer", "Dimmer", [new("level", brightness.CandidateId)]));
        }

        MapMeasurement(key, status, "apower", "Power", "W", tags, capabilities);
        if (TryPath(status, "aenergy.total"))
        {
            var energy = AddTag(key, "aenergy.total", TagDataType.Double, true, "Wh", null, null, tags);
            capabilities.Add(new DriverMaterializationCapabilityCandidate(
                $"{key}.energy", "Energy", [new("value", energy.CandidateId)]));
        }
    }

    private static void MapCover(
        string key,
        JsonElement status,
        List<DriverMaterializationTagCandidate> tags,
        List<DriverMaterializationCommandCandidate> commands,
        List<DriverMaterializationCapabilityCandidate> capabilities)
    {
        var bindings = new List<DriverMaterializationRoleBinding>();
        if (Has(status, "current_pos"))
        {
            var position = AddTag(key, "current_pos", TagDataType.Double, false, "%", "Cover.GoToPosition", "pos", tags);
            bindings.Add(new("position", position.CandidateId));
        }
        if (Has(status, "state"))
        {
            var state = AddTag(key, "state", TagDataType.String, true, null, null, null, tags);
            bindings.Add(new("state", state.CandidateId));
        }
        if (bindings.Count > 0)
            capabilities.Add(new DriverMaterializationCapabilityCandidate($"{key}.cover", "Cover", bindings));
    }

    private static void MapScalar(
        string key,
        JsonElement status,
        string field,
        TagDataType type,
        string? unit,
        string capability,
        string role,
        List<DriverMaterializationTagCandidate> tags,
        List<DriverMaterializationCapabilityCandidate> capabilities)
    {
        if (!Has(status, field)) return;
        var tag = AddTag(key, field, type, true, unit, null, null, tags);
        capabilities.Add(new DriverMaterializationCapabilityCandidate(
            $"{key}.{capability.ToLowerInvariant()}", capability, [new(role, tag.CandidateId)]));
    }

    private static void MapMeasurement(
        string key,
        JsonElement status,
        string field,
        string capability,
        string unit,
        List<DriverMaterializationTagCandidate> tags,
        List<DriverMaterializationCapabilityCandidate> capabilities)
    {
        if (!Has(status, field)) return;
        var tag = AddTag(key, field, TagDataType.Double, true, unit, null, null, tags);
        capabilities.Add(new DriverMaterializationCapabilityCandidate(
            $"{key}.{field}", capability, [new("value", tag.CandidateId)]));
    }

    private static DriverMaterializationTagCandidate AddTag(
        string key,
        string field,
        TagDataType type,
        bool readOnly,
        string? unit,
        string? writeMethod,
        string? writeParameter,
        List<DriverMaterializationTagCandidate> tags)
    {
        var candidateId = $"{key}.{field}";
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal);
        if (writeMethod is not null) metadata["writeMethod"] = writeMethod;
        if (writeParameter is not null) metadata["writeParameter"] = writeParameter;
        var tag = new DriverMaterializationTagCandidate(
            candidateId,
            $"{key} {field}",
            $"Shelly/{key}/{field.Replace('.', '/')}",
            type,
            PortableAddress: candidateId,
            ReadOnly: readOnly,
            EngineeringUnit: unit,
            Metadata: metadata);
        tags.Add(tag);
        return tag;
    }

    private static DriverMaterializationCommandCandidate AddCommand(
        string key,
        string suffix,
        string name,
        string value,
        string targetTagCandidateId,
        List<DriverMaterializationCommandCandidate> commands)
    {
        var command = new DriverMaterializationCommandCandidate(
            $"{key}.command.{suffix}",
            $"{key}.{suffix}",
            name,
            value,
            targetTagCandidateId);
        commands.Add(command);
        return command;
    }

    private static IEnumerable<string> EnumerateComponentKeys(JsonElement componentsResult, JsonElement status)
    {
        var found = new HashSet<string>(StringComparer.Ordinal);
        if (componentsResult.TryGetProperty("components", out var components) && components.ValueKind == JsonValueKind.Array)
        {
            foreach (var component in components.EnumerateArray())
                if (component.TryGetProperty("key", out var key) && key.ValueKind == JsonValueKind.String && key.GetString() is { } value)
                    found.Add(value);
        }
        foreach (var property in status.EnumerateObject())
            found.Add(property.Name);
        return found.OrderBy(x => x, StringComparer.Ordinal);
    }

    private static bool IsFunctionalComponent(string key) =>
        !key.Equals("sys", StringComparison.Ordinal) &&
        !key.Equals("wifi", StringComparison.Ordinal) &&
        !key.Equals("eth", StringComparison.Ordinal) &&
        !key.Equals("cloud", StringComparison.Ordinal) &&
        !key.Equals("mqtt", StringComparison.Ordinal) &&
        !key.Equals("ws", StringComparison.Ordinal) &&
        !key.Equals("ble", StringComparison.Ordinal);

    private static bool Has(JsonElement status, string field) =>
        status.ValueKind == JsonValueKind.Object && status.TryGetProperty(field, out var value) && value.ValueKind != JsonValueKind.Null;

    private static bool TryPath(JsonElement status, string path)
    {
        var current = status;
        foreach (var segment in path.Split('.'))
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out var next) || next.ValueKind == JsonValueKind.Null)
                return false;
            current = next;
        }
        return true;
    }

    private static string SanitizeSegment(string value)
    {
        var chars = value.Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '-').ToArray();
        return new string(chars);
    }
}
