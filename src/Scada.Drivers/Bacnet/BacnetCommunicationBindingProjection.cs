using System.Globalization;

namespace Scada.Drivers.Bacnet;

/// <summary>
/// Protocol-owned projection between the stable BACnet object/property identity
/// and the shared Engineering CommunicationBinding envelope. PortableAddress
/// carries identity only; COV and command-priority behavior live in Settings.
/// </summary>
public static class BacnetCommunicationBindingProjection
{
    public const string SchemaId = BacnetBinding.BindingSchemaId;
    public const int SchemaVersion = BacnetBinding.BindingSchemaVersion;

    private static readonly HashSet<string> CanonicalSettingKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "deviceInstance",
        "objectType",
        "objectInstance",
        "propertyIdentifier",
        "arrayIndex",
        "useCov",
        "writePriority"
    };

    public static string ToCanonicalPortableAddress(BacnetBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        binding.Validate();
        return binding.PortableAddress;
    }

    public static IReadOnlyDictionary<string, string> ToCanonicalSettings(BacnetBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        binding.Validate();

        var settings = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["deviceInstance"] = binding.DeviceInstance.ToString(CultureInfo.InvariantCulture),
            ["objectType"] = binding.ObjectType.ToString(CultureInfo.InvariantCulture),
            ["objectInstance"] = binding.ObjectInstance.ToString(CultureInfo.InvariantCulture),
            ["propertyIdentifier"] = binding.PropertyIdentifier.ToString(CultureInfo.InvariantCulture),
            ["useCov"] = binding.UseCov ? "true" : "false"
        };
        if (binding.ArrayIndex.HasValue)
            settings["arrayIndex"] = binding.ArrayIndex.Value.ToString(CultureInfo.InvariantCulture);
        if (binding.WritePriority.HasValue)
            settings["writePriority"] = binding.WritePriority.Value.ToString(CultureInfo.InvariantCulture);
        return settings;
    }

    public static bool TryMaterializeCanonical(
        string? portableAddress,
        IReadOnlyDictionary<string, string>? settings,
        out BacnetBinding? binding,
        out string? error)
    {
        binding = null;
        error = null;

        if (!BacnetBinding.TryParse(portableAddress, out var parsed, out error) || parsed is null)
            return false;
        if (settings is null || settings.Count == 0)
        {
            error = "Canonical BACnet CommunicationBinding.Settings is required.";
            return false;
        }

        foreach (var item in settings)
        {
            if (!CanonicalSettingKeys.Contains(item.Key))
            {
                error = $"Canonical BACnet binding setting '{item.Key}' is not supported.";
                return false;
            }
        }

        if (!Matches(settings, "deviceInstance", parsed.DeviceInstance, out error) ||
            !Matches(settings, "objectType", parsed.ObjectType, out error) ||
            !Matches(settings, "objectInstance", parsed.ObjectInstance, out error) ||
            !Matches(settings, "propertyIdentifier", parsed.PropertyIdentifier, out error))
            return false;

        if (parsed.ArrayIndex.HasValue)
        {
            if (!Matches(settings, "arrayIndex", parsed.ArrayIndex.Value, out error)) return false;
        }
        else if (TryGet(settings, "arrayIndex", out _))
        {
            error = "Canonical BACnet setting 'arrayIndex' must be absent when PortableAddress has no array index.";
            return false;
        }

        if (!TryGet(settings, "useCov", out var useCovText) || !bool.TryParse(useCovText, out var useCov))
        {
            error = "Canonical BACnet setting 'useCov' is required and must be true or false.";
            return false;
        }

        byte? writePriority = null;
        if (TryGet(settings, "writePriority", out var priorityText))
        {
            if (!byte.TryParse(priorityText, NumberStyles.None, CultureInfo.InvariantCulture, out var priority) || priority is < 1 or > 16)
            {
                error = "Canonical BACnet setting 'writePriority' must be from 1 to 16.";
                return false;
            }
            writePriority = priority;
        }

        var materialized = parsed with { UseCov = useCov, WritePriority = writePriority };
        try
        {
            materialized.Validate();
            binding = materialized;
            return true;
        }
        catch (ArgumentOutOfRangeException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static bool Matches(
        IReadOnlyDictionary<string, string> settings,
        string key,
        uint expected,
        out string? error)
    {
        error = null;
        if (!TryGet(settings, key, out var actual) ||
            !uint.TryParse(actual, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) ||
            parsed != expected)
        {
            error = $"Canonical BACnet setting '{key}' must match PortableAddress value '{expected.ToString(CultureInfo.InvariantCulture)}'.";
            return false;
        }
        return true;
    }

    private static bool TryGet(IReadOnlyDictionary<string, string> settings, string key, out string value)
    {
        foreach (var item in settings)
        {
            if (!string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase)) continue;
            value = item.Value;
            return true;
        }
        value = string.Empty;
        return false;
    }
}
