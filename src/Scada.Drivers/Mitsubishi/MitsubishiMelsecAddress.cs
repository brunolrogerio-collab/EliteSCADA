using System.Globalization;

namespace Scada.Drivers.Mitsubishi;

/// <summary>A validated Mitsubishi device address with its wire device code.</summary>
public sealed record MitsubishiMelsecAddress(
    MitsubishiMelsecDeviceArea Area,
    int Number,
    byte DeviceCode,
    bool IsBitDevice,
    int MaximumAddress)
{
    public const int MaximumWireAddress = 0xFFFFFF;

    public string PortableAddress => string.Create(
        CultureInfo.InvariantCulture,
        $"{Area}{Number.ToString(IsHexArea(Area) ? "X" : "D", CultureInfo.InvariantCulture)}");

    public static bool TryParse(
        string? value,
        MitsubishiMelsecFamilyProfile profile,
        int? rMaximumAddress,
        out MitsubishiMelsecAddress? address,
        out string? error)
    {
        address = null;
        error = null;
        if (string.IsNullOrWhiteSpace(value) || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            error = "Address must be a device mnemonic followed by a non-negative address.";
            return false;
        }

        var areaChar = char.ToUpperInvariant(value[0]);
        var area = areaChar switch
        {
            'X' => MitsubishiMelsecDeviceArea.X,
            'Y' => MitsubishiMelsecDeviceArea.Y,
            'M' => MitsubishiMelsecDeviceArea.M,
            'L' => MitsubishiMelsecDeviceArea.L,
            'B' => MitsubishiMelsecDeviceArea.B,
            'D' => MitsubishiMelsecDeviceArea.D,
            'W' => MitsubishiMelsecDeviceArea.W,
            'R' => MitsubishiMelsecDeviceArea.R,
            _ => (MitsubishiMelsecDeviceArea?)null
        };
        if (!area.HasValue || value.Length < 2)
        {
            error = "Address must use one of X, Y, M, L, B, D, W or profile-enabled R.";
            return false;
        }

        var radix = IsHexArea(area.Value) ? 16 : 10;
        var digits = value.AsSpan(1);
        var style = radix == 16 ? NumberStyles.AllowHexSpecifier : NumberStyles.None;
        if (!int.TryParse(digits, style, CultureInfo.InvariantCulture, out var number) || number < 0)
        {
            error = $"{area.Value} addresses must contain only {(radix == 16 ? "hexadecimal" : "decimal")} digits.";
            return false;
        }

        var maximum = GetMaximumAddress(area.Value, profile, rMaximumAddress);
        if (maximum < 0)
        {
            error = "R devices are disabled unless rMaximumAddress is explicitly configured for the selected profile.";
            return false;
        }
        if (number > MaximumWireAddress || number > maximum)
        {
            error = $"Address {value} exceeds the supported {area.Value} range 0..{FormatNumber(maximum, radix)} for {profile}.";
            return false;
        }

        var deviceCode = area.Value switch
        {
            MitsubishiMelsecDeviceArea.X => 0x9C,
            MitsubishiMelsecDeviceArea.Y => 0x9D,
            MitsubishiMelsecDeviceArea.M => 0x90,
            MitsubishiMelsecDeviceArea.L => 0x92,
            MitsubishiMelsecDeviceArea.B => 0xA0,
            MitsubishiMelsecDeviceArea.D => 0xA8,
            MitsubishiMelsecDeviceArea.W => 0xB4,
            MitsubishiMelsecDeviceArea.R => 0xAF,
            _ => throw new ArgumentOutOfRangeException()
        };
        var isBit = area.Value is MitsubishiMelsecDeviceArea.X or MitsubishiMelsecDeviceArea.Y or
            MitsubishiMelsecDeviceArea.M or MitsubishiMelsecDeviceArea.L or MitsubishiMelsecDeviceArea.B;
        address = new MitsubishiMelsecAddress(area.Value, number, checked((byte)deviceCode), isBit, maximum);
        return true;
    }

    public static int GetMaximumAddress(
        MitsubishiMelsecDeviceArea area,
        MitsubishiMelsecFamilyProfile profile,
        int? rMaximumAddress = null)
    {
        if (area == MitsubishiMelsecDeviceArea.R)
            return rMaximumAddress is >= 0 ? Math.Min(rMaximumAddress.Value, 32767) : -1;

        return profile switch
        {
            MitsubishiMelsecFamilyProfile.Fx5U32MtDs => area switch
            {
                MitsubishiMelsecDeviceArea.X or MitsubishiMelsecDeviceArea.Y => 0x3FF, // documented 1777 octal
                MitsubishiMelsecDeviceArea.M or MitsubishiMelsecDeviceArea.L => 32767,
                MitsubishiMelsecDeviceArea.B or MitsubishiMelsecDeviceArea.W => 0x7FFF,
                MitsubishiMelsecDeviceArea.D => 7999,
                _ => -1
            },
            MitsubishiMelsecFamilyProfile.IqR04EnCpu => area switch
            {
                MitsubishiMelsecDeviceArea.X or MitsubishiMelsecDeviceArea.Y or
                    MitsubishiMelsecDeviceArea.B or MitsubishiMelsecDeviceArea.W => 0x1FFF,
                MitsubishiMelsecDeviceArea.M or MitsubishiMelsecDeviceArea.L => 8191,
                MitsubishiMelsecDeviceArea.D => 143359,
                _ => -1
            },
            _ => -1
        };
    }

    public static bool IsHexArea(MitsubishiMelsecDeviceArea area) =>
        area is MitsubishiMelsecDeviceArea.X or MitsubishiMelsecDeviceArea.Y or
            MitsubishiMelsecDeviceArea.B or MitsubishiMelsecDeviceArea.W;

    public static string FormatNumber(int value, int radix) =>
        value.ToString(radix == 16 ? "X" : "D", CultureInfo.InvariantCulture);
}
