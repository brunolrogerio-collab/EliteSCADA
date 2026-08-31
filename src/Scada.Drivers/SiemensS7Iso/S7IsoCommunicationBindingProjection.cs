namespace Scada.Drivers.SiemensS7Iso;

/// <summary>
/// Protocol-owned projection of the existing Siemens S7 binding into the
/// Coordinator-validated Engineering v14 communication-binding shape.
///
/// This adapter deliberately keeps <see cref="S7IsoTagBinding.SchemaId"/> and
/// schema version 1. It does not duplicate the shared CommunicationTagBinding or
/// TagPhysicalValueTransform contracts. Instead it exposes the Siemens-owned
/// pieces that the shared envelope consumes and maps the legacy internal
/// S7IsoValueOrder representation to the shared byte/word transform flags.
/// </summary>
public static class S7IsoCommunicationBindingProjection
{
    public const string SchemaId = S7IsoTagBinding.SchemaId;
    public const int SchemaVersion = S7IsoTagBinding.CurrentSchemaVersion;

    private static readonly HashSet<string> CanonicalSettingKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "area",
        "dbNumber",
        "byteOffset",
        "bitOffset",
        "valueType",
        "stringLength",
        "writable"
    };

    public static string ToCanonicalPortableAddress(S7IsoTagBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        var settings = ToCanonicalSettings(binding);

        return string.Join(
            ';',
            $"{S7IsoTagBinding.PortablePrefix}{SchemaVersion}",
            $"area={settings["area"]}",
            $"db={settings["dbNumber"]}",
            $"byte={settings["byteOffset"]}",
            $"bit={settings["bitOffset"]}",
            $"type={settings["valueType"]}",
            $"string={settings["stringLength"]}",
            $"writable={settings["writable"]}");
    }

    public static IReadOnlyDictionary<string, string> ToCanonicalSettings(S7IsoTagBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);

        // The existing binding validator remains the Siemens protocol-shape
        // authority. Ordering is deliberately omitted from the v14 projection;
        // the shared TagPhysicalValueTransform is its only persisted authority.
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

    public static (bool ByteSwap, bool WordSwap) GetPhysicalValueTransform(S7IsoTagBinding binding)
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

    public static bool TryMaterializeCanonical(
        string? portableAddress,
        IReadOnlyDictionary<string, string>? settings,
        bool byteSwap,
        bool wordSwap,
        out S7IsoTagBinding? binding,
        out string? error)
    {
        binding = null;
        error = null;

        if (ContainsOrderingToken(portableAddress))
        {
            error = "Canonical S7 v14 projection cannot persist byte/word ordering in PortableAddress; use the shared physical value transform.";
            return false;
        }

        if (!S7IsoTagBinding.TryParsePortableAddress(portableAddress, out var parsed, out error))
            return false;

        if (parsed!.SchemaVersion != SchemaVersion)
        {
            error = $"S7 binding schema version '{parsed.SchemaVersion}' is not supported by the canonical projection.";
            return false;
        }

        var expectedSettings = ToCanonicalSettings(parsed);
        if (!ValidateCanonicalSettings(settings, expectedSettings, out error))
            return false;

        var order = (byteSwap, wordSwap) switch
        {
            (false, false) => S7IsoValueOrder.Normal,
            (true, false) => S7IsoValueOrder.ByteSwap,
            (false, true) => S7IsoValueOrder.WordSwap,
            (true, true) => S7IsoValueOrder.ByteAndWordSwap
        };

        var materialized = parsed with { ValueOrder = order };
        try
        {
            _ = materialized.ToSettings();
        }
        catch (ArgumentException ex)
        {
            error = ex.Message;
            return false;
        }

        binding = materialized;
        return true;
    }

    private static bool ContainsOrderingToken(string? portableAddress)
    {
        if (string.IsNullOrWhiteSpace(portableAddress)) return false;

        var parts = portableAddress.Split(
            ';',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var index = 1; index < parts.Length; index++)
        {
            var separator = parts[index].IndexOf('=');
            if (separator <= 0) continue;
            var key = parts[index][..separator].Trim();
            if (string.Equals(key, "order", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "valueOrder", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool ValidateCanonicalSettings(
        IReadOnlyDictionary<string, string>? settings,
        IReadOnlyDictionary<string, string> expected,
        out string? error)
    {
        error = null;
        if (settings is null || settings.Count == 0) return true;

        foreach (var item in settings)
        {
            if (string.Equals(item.Key, "valueOrder", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.Key, "order", StringComparison.OrdinalIgnoreCase))
            {
                error = "Canonical S7 v14 projection cannot persist byte/word ordering in Settings; use the shared physical value transform.";
                return false;
            }

            if (!CanonicalSettingKeys.Contains(item.Key))
            {
                error = $"Canonical S7 binding setting '{item.Key}' is not supported.";
                return false;
            }
        }

        foreach (var item in expected)
        {
            if (!settings.TryGetValue(item.Key, out var actual) ||
                !string.Equals(actual, item.Value, StringComparison.Ordinal))
            {
                error = $"Canonical S7 binding setting '{item.Key}' does not match PortableAddress.";
                return false;
            }
        }

        return true;
    }
}
