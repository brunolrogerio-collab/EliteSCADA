using System.Globalization;

namespace Scada.Drivers.Dnp3;

/// <summary>
/// Deterministic Engineering-facing point address inside one DNP3 Data Source.
/// The Data Source itself owns the outstation association, so endpoint/link
/// identity is intentionally not duplicated in every point address.
/// </summary>
public readonly record struct Dnp3PortableAddress(Dnp3PointKind PointKind, ushort Index)
{
    public override string ToString() => $"dnp3:{Dnp3VariationRules.GetPointKindToken(PointKind)}:{Index.ToString(CultureInfo.InvariantCulture)}";

    public static Dnp3PortableAddress Parse(string text)
    {
        if (!TryParse(text, out var address))
            throw new FormatException($"'{text}' is not a canonical DNP3 portable address.");
        return address;
    }

    public static bool TryParse(string? text, out Dnp3PortableAddress address)
    {
        address = default;
        if (string.IsNullOrEmpty(text)) return false;
        if (!text.Equals(text.Trim(), StringComparison.Ordinal)) return false;

        var parts = text.Split(':');
        if (parts.Length != 3 || !parts[0].Equals("dnp3", StringComparison.Ordinal))
            return false;
        if (!TryParsePointKind(parts[1], out var pointKind))
            return false;
        if (!ushort.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var index))
            return false;

        var candidate = new Dnp3PortableAddress(pointKind, index);
        if (!candidate.ToString().Equals(text, StringComparison.Ordinal))
            return false;

        address = candidate;
        return true;
    }

    private static bool TryParsePointKind(string token, out Dnp3PointKind pointKind)
    {
        pointKind = token switch
        {
            "binaryInput" => Dnp3PointKind.BinaryInput,
            "doubleBitBinaryInput" => Dnp3PointKind.DoubleBitBinaryInput,
            "analogInput" => Dnp3PointKind.AnalogInput,
            "counter" => Dnp3PointKind.Counter,
            "frozenCounter" => Dnp3PointKind.FrozenCounter,
            "binaryOutputStatus" => Dnp3PointKind.BinaryOutputStatus,
            "analogOutputStatus" => Dnp3PointKind.AnalogOutputStatus,
            _ => default
        };

        return token is "binaryInput" or
            "doubleBitBinaryInput" or
            "analogInput" or
            "counter" or
            "frozenCounter" or
            "binaryOutputStatus" or
            "analogOutputStatus";
    }
}
