using System.Globalization;
using Scada.Core.Tags;

namespace Scada.Drivers.SiemensS7Iso;

public sealed record S7IsoBindingIssue(string FieldKey, string Message);

/// <summary>
/// EliteSCADA-owned, library-independent Siemens S7 ISO TAG binding.
/// The binding is intentionally versioned and can round-trip through a portable
/// text form without exposing any concrete S7 client library type.
/// </summary>
public sealed record S7IsoTagBinding(
    int SchemaVersion,
    S7IsoArea Area,
    int ByteOffset,
    S7IsoValueType ValueType,
    ushort DbNumber = 0,
    byte BitOffset = 0,
    bool Writable = false,
    byte StringLength = 0,
    S7IsoValueOrder ValueOrder = S7IsoValueOrder.Normal)
{
    public const string SchemaId = "siemens.s7.iso.binding";
    public const int CurrentSchemaVersion = 1;
    public const string PortablePrefix = "s7iso:v";

    private static readonly HashSet<string> PortableFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "area", "db", "byte", "bit", "type", "string", "writable", "order"
    };

    public S7IsoPoint ToPoint(TagDefinition tag)
    {
        ArgumentNullException.ThrowIfNull(tag);
        if (SchemaVersion != CurrentSchemaVersion)
            throw new NotSupportedException($"S7 ISO binding schema version '{SchemaVersion}' is not supported.");

        var point = new S7IsoPoint(
            tag,
            Area,
            ByteOffset,
            ValueType,
            DbNumber,
            BitOffset,
            Writable,
            StringLength,
            ValueOrder);
        point.Validate();
        return point;
    }

    public string ToPortableAddress() => string.Join(
        ';',
        $"{PortablePrefix}{SchemaVersion}",
        $"area={Area}",
        $"db={DbNumber.ToString(CultureInfo.InvariantCulture)}",
        $"byte={ByteOffset.ToString(CultureInfo.InvariantCulture)}",
        $"bit={BitOffset.ToString(CultureInfo.InvariantCulture)}",
        $"type={ValueType}",
        $"string={StringLength.ToString(CultureInfo.InvariantCulture)}",
        $"writable={(Writable ? "true" : "false")}",
        $"order={ValueOrder}");

    public static bool TryCreateFromSettings(
        IReadOnlyDictionary<string, string> settings,
        out S7IsoTagBinding? binding,
        out IReadOnlyCollection<S7IsoBindingIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var errors = new List<S7IsoBindingIssue>();

        var area = ParseRequiredEnum<S7IsoArea>(settings, "area", errors);
        var byteOffset = ParseRequiredInt(settings, "byteOffset", 0, 2_097_151, errors);
        var valueType = ParseRequiredEnum<S7IsoValueType>(settings, "valueType", errors);
        var dbNumber = checked((ushort)ParseOptionalInt(settings, "dbNumber", 0, 0, ushort.MaxValue, errors));
        var bitOffset = checked((byte)ParseOptionalInt(settings, "bitOffset", 0, 0, 7, errors));
        var stringLength = checked((byte)ParseOptionalInt(settings, "stringLength", 0, 0, 254, errors));
        var writable = ParseOptionalBool(settings, "writable", false, errors);
        var order = ParseOptionalEnum(settings, "valueOrder", S7IsoValueOrder.Normal, errors);

        binding = errors.Count == 0
            ? new S7IsoTagBinding(
                CurrentSchemaVersion,
                area,
                byteOffset,
                valueType,
                dbNumber,
                bitOffset,
                writable,
                stringLength,
                order)
            : null;

        if (binding is not null)
            ValidateShape(binding, errors);

        if (errors.Count > 0) binding = null;
        issues = errors;
        return binding is not null;
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
            error = "S7 ISO portable address is required.";
            return false;
        }

        var parts = portableAddress.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0 || !parts[0].StartsWith(PortablePrefix, StringComparison.OrdinalIgnoreCase))
        {
            error = $"S7 ISO portable address must start with '{PortablePrefix}<version>'.";
            return false;
        }

        if (!int.TryParse(parts[0].AsSpan(PortablePrefix.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out var version))
        {
            error = "S7 ISO portable address contains an invalid schema version.";
            return false;
        }
        if (version != CurrentSchemaVersion)
        {
            error = $"S7 ISO binding schema version '{version}' is not supported.";
            return false;
        }

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 1; i < parts.Length; i++)
        {
            var separator = parts[i].IndexOf('=');
            if (separator <= 0 || separator == parts[i].Length - 1)
            {
                error = $"S7 ISO portable address token '{parts[i]}' is invalid.";
                return false;
            }

            var key = parts[i][..separator].Trim();
            var value = parts[i][(separator + 1)..].Trim();
            if (!PortableFields.Contains(key))
            {
                error = $"S7 ISO portable address field '{key}' is not supported by schema v{CurrentSchemaVersion}.";
                return false;
            }
            if (!values.TryAdd(key, value))
            {
                error = $"S7 ISO portable address field '{key}' appears more than once.";
                return false;
            }
        }

        var normalized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Copy(values, normalized, "area", "area");
        Copy(values, normalized, "db", "dbNumber");
        Copy(values, normalized, "byte", "byteOffset");
        Copy(values, normalized, "bit", "bitOffset");
        Copy(values, normalized, "type", "valueType");
        Copy(values, normalized, "string", "stringLength");
        Copy(values, normalized, "writable", "writable");
        Copy(values, normalized, "order", "valueOrder");

        if (TryCreateFromSettings(normalized, out binding, out var issues)) return true;

        error = string.Join(" ", issues.Select(issue => issue.Message));
        return false;
    }

    private static void ValidateShape(S7IsoTagBinding binding, List<S7IsoBindingIssue> errors)
    {
        if (binding.Area == S7IsoArea.DataBlock && binding.DbNumber == 0)
            errors.Add(new S7IsoBindingIssue("dbNumber", "S7 DB bindings require a non-zero DB number."));
        if (binding.Area != S7IsoArea.DataBlock && binding.DbNumber != 0)
            errors.Add(new S7IsoBindingIssue("dbNumber", "S7 DB number is valid only for DataBlock bindings."));
        if (binding.ValueType != S7IsoValueType.Boolean && binding.BitOffset != 0)
            errors.Add(new S7IsoBindingIssue("bitOffset", "S7 bit offset is valid only for Boolean bindings."));
        if (binding.ValueType == S7IsoValueType.String && binding.StringLength is < 1 or > 254)
            errors.Add(new S7IsoBindingIssue("stringLength", "S7 STRING bindings require a length from 1 to 254."));
        if (binding.ValueType != S7IsoValueType.String && binding.StringLength != 0)
            errors.Add(new S7IsoBindingIssue("stringLength", "S7 string length is valid only for String bindings."));
        if (binding.Writable && binding.Area == S7IsoArea.Input)
            errors.Add(new S7IsoBindingIssue("writable", "S7 input-area bindings are read-only."));

        var byteLength = GetByteLength(binding);
        if ((long)binding.ByteOffset + byteLength - 1 > 2_097_151)
            errors.Add(new S7IsoBindingIssue(
                "byteOffset",
                $"S7 binding payload of {byteLength} byte(s) exceeds the 24-bit S7ANY address range from byte offset {binding.ByteOffset}."));

        var multiByteNumeric = binding.ValueType is
            S7IsoValueType.UInt16 or S7IsoValueType.Int16 or
            S7IsoValueType.UInt32 or S7IsoValueType.Int32 or S7IsoValueType.Float32 or
            S7IsoValueType.Int64 or S7IsoValueType.Float64;

        if (binding.ValueOrder != S7IsoValueOrder.Normal && !multiByteNumeric)
            errors.Add(new S7IsoBindingIssue("valueOrder", "S7 byte/word ordering is valid only for multi-byte numeric bindings."));
        if ((binding.ValueOrder is S7IsoValueOrder.WordSwap or S7IsoValueOrder.ByteAndWordSwap) &&
            binding.ValueType is S7IsoValueType.UInt16 or S7IsoValueType.Int16)
            errors.Add(new S7IsoBindingIssue("valueOrder", "S7 word swap requires a value at least 32 bits wide."));
    }

    private static int GetByteLength(S7IsoTagBinding binding) => binding.ValueType switch
    {
        S7IsoValueType.Boolean or S7IsoValueType.Byte => 1,
        S7IsoValueType.UInt16 or S7IsoValueType.Int16 => 2,
        S7IsoValueType.UInt32 or S7IsoValueType.Int32 or S7IsoValueType.Float32 => 4,
        S7IsoValueType.Int64 or S7IsoValueType.Float64 or S7IsoValueType.DateTime => 8,
        S7IsoValueType.String => binding.StringLength + 2,
        _ => throw new ArgumentOutOfRangeException(nameof(binding.ValueType))
    };

    private static void Copy(
        IReadOnlyDictionary<string, string> source,
        IDictionary<string, string> destination,
        string sourceKey,
        string destinationKey)
    {
        if (source.TryGetValue(sourceKey, out var value)) destination[destinationKey] = value;
    }

    private static int ParseRequiredInt(
        IReadOnlyDictionary<string, string> settings,
        string key,
        int minimum,
        int maximum,
        List<S7IsoBindingIssue> errors)
    {
        if (!settings.TryGetValue(key, out var text) || string.IsNullOrWhiteSpace(text))
        {
            errors.Add(new S7IsoBindingIssue(key, $"S7 binding field '{key}' is required."));
            return minimum;
        }
        return ParseIntValue(text, key, minimum, maximum, errors);
    }

    private static int ParseOptionalInt(
        IReadOnlyDictionary<string, string> settings,
        string key,
        int defaultValue,
        int minimum,
        int maximum,
        List<S7IsoBindingIssue> errors)
    {
        if (!settings.TryGetValue(key, out var text) || string.IsNullOrWhiteSpace(text)) return defaultValue;
        return ParseIntValue(text, key, minimum, maximum, errors);
    }

    private static int ParseIntValue(
        string text,
        string key,
        int minimum,
        int maximum,
        List<S7IsoBindingIssue> errors)
    {
        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value >= minimum && value <= maximum)
            return value;

        errors.Add(new S7IsoBindingIssue(key, $"S7 binding field '{key}' must be an integer from {minimum} to {maximum}."));
        return minimum;
    }

    private static T ParseRequiredEnum<T>(
        IReadOnlyDictionary<string, string> settings,
        string key,
        List<S7IsoBindingIssue> errors)
        where T : struct, Enum
    {
        if (settings.TryGetValue(key, out var text) &&
            Enum.TryParse<T>(text, true, out var value) &&
            Enum.IsDefined(value))
            return value;

        errors.Add(new S7IsoBindingIssue(key, $"S7 binding field '{key}' is required and must use a supported value."));
        return default;
    }

    private static T ParseOptionalEnum<T>(
        IReadOnlyDictionary<string, string> settings,
        string key,
        T defaultValue,
        List<S7IsoBindingIssue> errors)
        where T : struct, Enum
    {
        if (!settings.TryGetValue(key, out var text) || string.IsNullOrWhiteSpace(text)) return defaultValue;
        if (Enum.TryParse<T>(text, true, out var value) && Enum.IsDefined(value)) return value;

        errors.Add(new S7IsoBindingIssue(key, $"S7 binding field '{key}' must use a supported value."));
        return defaultValue;
    }

    private static bool ParseOptionalBool(
        IReadOnlyDictionary<string, string> settings,
        string key,
        bool defaultValue,
        List<S7IsoBindingIssue> errors)
    {
        if (!settings.TryGetValue(key, out var text) || string.IsNullOrWhiteSpace(text)) return defaultValue;
        if (bool.TryParse(text, out var value)) return value;

        errors.Add(new S7IsoBindingIssue(key, $"S7 binding field '{key}' must be true or false."));
        return defaultValue;
    }
}
