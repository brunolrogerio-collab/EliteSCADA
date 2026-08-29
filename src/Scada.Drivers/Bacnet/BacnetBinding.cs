using System.Globalization;
using Scada.Core.Tags;

namespace Scada.Drivers.Bacnet;

/// <summary>
/// Library-independent BACnet object/property identity persisted by EliteSCADA.
/// Network addresses and object names are deliberately not part of the identity.
/// </summary>
public sealed record BacnetBinding(
    uint DeviceInstance,
    uint ObjectType,
    uint ObjectInstance,
    uint PropertyIdentifier,
    uint? ArrayIndex = null,
    bool UseCov = true,
    byte? WritePriority = null)
{
    public const uint MaximumDeviceInstance = 4_194_302;
    public const uint MaximumObjectInstance = 4_194_303;

    public string StableIdentity => ArrayIndex.HasValue
        ? FormattableString.Invariant($"device={DeviceInstance};object={ObjectType}:{ObjectInstance};property={PropertyIdentifier};index={ArrayIndex.Value}")
        : FormattableString.Invariant($"device={DeviceInstance};object={ObjectType}:{ObjectInstance};property={PropertyIdentifier}");

    public string PortableAddress => "bacnet:" + StableIdentity;

    public void Validate()
    {
        if (DeviceInstance > MaximumDeviceInstance)
            throw new ArgumentOutOfRangeException(nameof(DeviceInstance), $"BACnet device instance must be from 0 to {MaximumDeviceInstance}.");
        if (ObjectInstance > MaximumObjectInstance)
            throw new ArgumentOutOfRangeException(nameof(ObjectInstance), $"BACnet object instance must be from 0 to {MaximumObjectInstance}.");
        if (WritePriority is < 1 or > 16)
            throw new ArgumentOutOfRangeException(nameof(WritePriority), "BACnet write priority must be from 1 to 16 when configured.");
    }

    public static bool TryParse(string? raw, out BacnetBinding? binding, out string? error)
    {
        binding = null;
        error = null;
        if (string.IsNullOrWhiteSpace(raw))
        {
            error = "BACnet portable address is required.";
            return false;
        }

        var value = raw.Trim();
        if (value.StartsWith("bacnet:", StringComparison.OrdinalIgnoreCase))
            value = value[7..];

        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var token in value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = token.IndexOf('=');
            if (separator <= 0 || separator == token.Length - 1)
            {
                error = $"Invalid BACnet address token '{token}'.";
                return false;
            }
            fields[token[..separator].Trim()] = token[(separator + 1)..].Trim();
        }

        if (!TryUInt(fields, "device", out var device, out error)) return false;
        if (!TryUInt(fields, "property", out var property, out error)) return false;
        if (!fields.TryGetValue("object", out var objectText))
        {
            error = "BACnet address field 'object' is required as '<objectType>:<objectInstance>'.";
            return false;
        }
        var objectParts = objectText.Split(':', StringSplitOptions.TrimEntries);
        if (objectParts.Length != 2 ||
            !uint.TryParse(objectParts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var objectType) ||
            !uint.TryParse(objectParts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var objectInstance))
        {
            error = $"BACnet object identity '{objectText}' must be '<numericObjectType>:<numericObjectInstance>'.";
            return false;
        }

        uint? arrayIndex = null;
        if (fields.TryGetValue("index", out var indexText))
        {
            if (!uint.TryParse(indexText, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedIndex))
            {
                error = $"BACnet array index '{indexText}' must be an unsigned integer.";
                return false;
            }
            arrayIndex = parsedIndex;
        }

        var candidate = new BacnetBinding(device, objectType, objectInstance, property, arrayIndex);
        try
        {
            candidate.Validate();
            binding = candidate;
            return true;
        }
        catch (ArgumentOutOfRangeException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static bool TryUInt(IReadOnlyDictionary<string, string> fields, string key, out uint value, out string? error)
    {
        value = default;
        error = null;
        if (!fields.TryGetValue(key, out var text))
        {
            error = $"BACnet address field '{key}' is required.";
            return false;
        }
        if (!uint.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value))
        {
            error = $"BACnet address field '{key}' must be an unsigned integer.";
            return false;
        }
        return true;
    }
}

public sealed record BacnetPoint(TagDefinition Tag, BacnetBinding Binding, bool Writable = false)
{
    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Tag);
        ArgumentNullException.ThrowIfNull(Binding);
        Binding.Validate();
        if (Writable && Tag.ReadOnly)
            throw new ArgumentException($"TAG '{Tag.Path}' is read-only but the BACnet point is marked writable.");
    }
}
