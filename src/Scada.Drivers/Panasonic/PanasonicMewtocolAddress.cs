using System.Globalization;

namespace Scada.Drivers.Panasonic;

/// <summary>A parsed classic MEWTOCOL-COM memory address.</summary>
public sealed record PanasonicMewtocolAddress(
    PanasonicMewtocolArea Area,
    int Number,
    int MaximumAddress,
    bool IsContact,
    bool IsWritable,
    char WireAreaCode)
{
    public string PortableAddress => string.Create(CultureInfo.InvariantCulture, $"{Area}{FormatNumber(Number, IsHexContact(Area))}");

    public static bool TryParse(
        string? text,
        PanasonicMewtocolFamilyProfile profile,
        out PanasonicMewtocolAddress? address,
        out string? error)
    {
        address = null;
        error = null;
        if (!Enum.IsDefined(profile))
        {
            error = "Address profile is not one of the bounded Panasonic v1 profiles.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(text) || !string.Equals(text, text.Trim(), StringComparison.Ordinal))
        {
            error = "Address must contain a supported Panasonic area and a non-negative address, without surrounding whitespace.";
            return false;
        }

        var normalized = text.ToUpperInvariant();
        var area = normalized.StartsWith("WX", StringComparison.Ordinal) ? PanasonicMewtocolArea.WX
            : normalized.StartsWith("WY", StringComparison.Ordinal) ? PanasonicMewtocolArea.WY
            : normalized.StartsWith("WR", StringComparison.Ordinal) ? PanasonicMewtocolArea.WR
            : normalized.StartsWith("WL", StringComparison.Ordinal) ? PanasonicMewtocolArea.WL
            : normalized.StartsWith("DT", StringComparison.Ordinal) ? PanasonicMewtocolArea.DT
            : normalized.StartsWith("LD", StringComparison.Ordinal) ? PanasonicMewtocolArea.LD
            : normalized.Length > 1 ? normalized[0] switch
            {
                'X' => PanasonicMewtocolArea.X,
                'Y' => PanasonicMewtocolArea.Y,
                'R' => PanasonicMewtocolArea.R,
                'L' => PanasonicMewtocolArea.L,
                'T' => PanasonicMewtocolArea.T,
                'C' => PanasonicMewtocolArea.C,
                _ => (PanasonicMewtocolArea?)null
            }
            : null;
        if (!area.HasValue)
        {
            error = "Address must use X, Y, R, L, T, C, WX, WY, WR, WL, DT or LD.";
            return false;
        }

        var prefixLength = area.Value is PanasonicMewtocolArea.WX or PanasonicMewtocolArea.WY or
            PanasonicMewtocolArea.WR or PanasonicMewtocolArea.WL or PanasonicMewtocolArea.DT or PanasonicMewtocolArea.LD ? 2 : 1;
        var suffix = normalized.AsSpan(prefixLength);
        var hex = IsHexContact(area.Value);
        int number;
        if (suffix.IsEmpty)
        {
            error = $"{area} address must contain only {(hex ? "decimal digits followed by a hexadecimal final digit" : "decimal digits")}.";
            return false;
        }
        if (hex)
        {
            var finalDigit = suffix[^1];
            var nibble = finalDigit is >= '0' and <= '9' ? finalDigit - '0'
                : finalDigit is >= 'A' and <= 'F' ? finalDigit - 'A' + 10
                : -1;
            var decimalPrefix = suffix[..^1];
            var prefixValue = 0;
            if (nibble < 0 || (!decimalPrefix.IsEmpty &&
                !int.TryParse(decimalPrefix, NumberStyles.None, CultureInfo.InvariantCulture, out prefixValue)))
            {
                error = $"{area} address must contain decimal digits followed by a hexadecimal final digit.";
                return false;
            }
            try { number = checked(prefixValue * 16 + nibble); }
            catch (OverflowException)
            {
                error = $"{area} address exceeds the supported numeric range.";
                return false;
            }
        }
        else if (!int.TryParse(suffix, NumberStyles.None, CultureInfo.InvariantCulture, out number) || number < 0)
        {
            error = $"{area} address must contain only decimal digits.";
            return false;
        }

        var capabilities = PanasonicMewtocolFamilyCapabilities.For(profile, PanasonicMewtocolFrameMode.Standard);
        if (!capabilities.TryGetArea(area.Value, out var limits) || number < limits.MinimumAddress || number > limits.MaximumAddress)
        {
            var maximum = limits?.MaximumAddress ?? -1;
            error = $"Address {text} exceeds the supported {area} range 0..{FormatNumber(maximum, hex)} for {profile}.";
            return false;
        }

        var wireCode = area.Value switch
        {
            PanasonicMewtocolArea.X or PanasonicMewtocolArea.WX => 'X',
            PanasonicMewtocolArea.Y or PanasonicMewtocolArea.WY => 'Y',
            PanasonicMewtocolArea.R or PanasonicMewtocolArea.WR => 'R',
            PanasonicMewtocolArea.L or PanasonicMewtocolArea.WL => 'L',
            PanasonicMewtocolArea.DT => 'D',
            PanasonicMewtocolArea.LD => 'L',
            PanasonicMewtocolArea.T => 'T',
            PanasonicMewtocolArea.C => 'C',
            _ => throw new ArgumentOutOfRangeException()
        };
        address = new PanasonicMewtocolAddress(area.Value, number, limits!.MaximumAddress, limits.IsContact, limits.IsWritable, wireCode);
        return true;
    }

    public static bool IsHexContact(PanasonicMewtocolArea area) =>
        area is PanasonicMewtocolArea.X or PanasonicMewtocolArea.Y or PanasonicMewtocolArea.R or PanasonicMewtocolArea.L;

    private static string FormatNumber(int number, bool hex)
    {
        if (!hex) return number.ToString(CultureInfo.InvariantCulture);
        return (number / 16).ToString(CultureInfo.InvariantCulture) +
            (number & 0xF).ToString("X", CultureInfo.InvariantCulture);
    }
}
