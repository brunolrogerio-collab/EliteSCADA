using System.Globalization;

namespace Scada.Drivers.SiemensS7Iso;

/// <summary>
/// Protocol-owned Siemens S7 binding schema prepared for the shared
/// CommunicationTagBinding envelope introduced by Engineering schema v14.
///
/// This type deliberately does not duplicate that shared envelope. It only owns
/// the Siemens schema parts: SchemaId/SchemaVersion, PortableAddress, public
/// protocol settings, and the conversion between the shared ByteSwap/WordSwap
/// flags and the internal S7IsoValueOrder runtime representation.
/// </summary>
public static class S7IsoCommunicationBindingSchemaV2
{
    public const string SchemaId = S7IsoTagBinding.SchemaId;
    public const int SchemaVersion = 2;
    public const string PortablePrefix = "s7iso:v2";

    private static readonly HashSet<string> SettingsKeys = new(StringComparer.Ordinal)
    {
        "area",
        "dbNumber",
        "byteOffset",
        "bitOffset",
        "valueType",
        "stringLength",
        "writable"
    };

    public static string ToPortableAddress(S7IsoTagBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        var settings = ToSettings(binding);

        return string.Join(
            ';',
            PortablePrefix,
            $"area={settings["area"]}",
            $"db={settings["dbNumber"]}",
            $"byte={settings["byteOffset"]}",
            $"bit={settings["bitOffset"]}",
            $"type={settings["valueType"]}",
            $"string={settings["stringLength"]}",
            $"writable={settings["writable"]}");
    }

    public static IReadOnlyDictionary<string, string> ToSettings(S7IsoTagBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);

        // Reuse the v1 validator as the protocol-shape authority. ValueOrder is
        // intentionally removed because schema v14 persists it exclusively in
        // TagPhysicalValueTransform.
        var validated = binding.ToSettings();
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["area"] = validated["area"],
            ["dbNumber"] = validated["dbNumber"],
            ["byteOffset"] = validated["byteOffset"],
            ["bitOffset"] = validated["bitOffset"],
            ["valueType"] = validated["valueType"],
            ["stringLength"] = validated["stringLength"],
            ["writable"] = validated["writable"]
        };
    }

    public static (bool ByteSwap, bool WordSwap) GetPhysicalTransform(S7IsoTagBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        _ = binding.ToSettings();

        return binding.ValueOrder switch
        {
            S7IsoValueOrder.Normal => (false, false),
            S7IsoValueOrder.ByteSwap => (true, false),
            S7IsoValueOrder.WordSwap => (false, true),
            S7IsoValueOrder.ByteAndWordSwap => (true, true),
            _ => throw new ArgumentOutOfRangeException(nameof(binding), "Unsupported S7 value order.")
        };
    }

    public static bool TryMaterialize(
        string? portableAddress,
        IReadOnlyDictionary<string, string>? settings,
        bool byteSwap,
        bool wordSwap,
        out S7IsoTagBinding? binding,
        out string? error)
    {
        binding = null;
        error = null;

        if (!TryParsePortableAddress(portableAddress, out var addressBinding, out error))
            return false;

        var expectedSettings = ToSettings(addressBinding!);
        if (settings is not null && settings.Count > 0)
        {
            foreach (var item in settings)
            {
                if (string.Equals(item.Key, "valueOrder", StringComparison.OrdinalIgnoreCase))
                {
                    error = "S7 binding schema v2 does not persist 'valueOrder'; use the shared physical value transform.";
                    return false;
                }

                if (!SettingsKeys.Contains(item.Key))
                {
                    error = $"S7 binding schema v2 setting '{item.Key}' is not supported.";
                    return false;
                }
            }

            foreach (var expected in expectedSettings)
            {
                if (!settings.TryGetValue(expected.Key, out var actual) ||
                    !string.Equals(actual, expected.Value, StringComparison.OrdinalIgnoreCase))
                {
                    error = $"S7 binding schema v2 setting '{expected.Key}' does not match PortableAddress.";
                    return false;
                }
            }
        }

        var order = (byteSwap, wordSwap) switch
        {
            (false, false) => S7IsoValueOrder.Normal,
            (true, false) => S7IsoValueOrder.ByteSwap,
            (false, true) => S7IsoValueOrder.WordSwap,
            (true, true) => S7IsoValueOrder.ByteAndWordSwap
        };

        var transformed = addressBinding! with { ValueOrder = order };
        try
        {
            // Validation here proves that the shared transform is legal for the
            // selected Siemens value type before a runtime point is activated.
            _ = transformed.ToSettings();
        }
        catch (ArgumentException ex)
        {
            error = ex.Message;
            return false;
        }

        binding = transformed;
        return true;
    }

    public static bool TryParsePortableAddress(
        string? portableAddress,
        out S7IsoTagBinding? binding,
        out string? error)
    {
        binding = null;
        error = null;

        if (string.IsNullOrWhiteSpace(portableAddress))
        {
            error = "S7 binding schema v2 PortableAddress is required.";
            return false;
        }

        var parts = portableAddress.Split(
            ';',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0 || !string.Equals(parts[0], PortablePrefix, StringComparison.OrdinalIgnoreCase))
        {
            error = $"S7 binding schema v2 PortableAddress must start with '{PortablePrefix}'.";
            return false;
        }

        for (var index = 1; index < parts.Length; index++)
        {
            var separator = parts[index].IndexOf('=');
            if (separator <= 0)
            {
                error = $"S7 binding schema v2 token '{parts[index]}' is invalid.";
                return false;
            }

            var key = parts[index][..separator].Trim();
            if (string.Equals(key, "order", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "valueOrder", StringComparison.OrdinalIgnoreCase))
            {
                error = "S7 binding schema v2 PortableAddress cannot contain physical byte/word ordering.";
                return false;
            }
        }

        // The v1 parser already owns all Siemens area/type/address validation.
        // Convert only the envelope syntax, then keep the resulting binding as
        // an internal runtime representation. No v1 persistence is manufactured.
        var legacyPortable = string.Join(
            ';',
            $"{S7IsoTagBinding.PortablePrefix}{S7IsoTagBinding.CurrentSchemaVersion}",
            string.Join(';', parts.Skip(1)),
            $"order={S7IsoValueOrder.Normal}");

        if (!S7IsoTagBinding.TryParsePortableAddress(legacyPortable, out binding, out error))
            return false;

        return true;
    }
}
