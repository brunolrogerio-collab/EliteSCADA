using System.Globalization;

namespace Scada.Drivers.Modbus;

public enum ModbusAddressReferenceBase
{
    ZeroBased,
    OneBased
}

/// <summary>
/// Canonical, Driver-owned Modbus TAG address parser/builder. Runtime, Engineering
/// validation and UI assistants must converge through this codec instead of
/// maintaining independent address grammars. Canonical persistence is always
/// zero-based: coil:0, discrete:0, holding:0 or input:0.
/// </summary>
public static class ModbusTagAddressCodec
{
    public static bool TryParse(
        string? raw,
        IReadOnlyDictionary<string, string>? metadata,
        out ModbusDataArea area,
        out ushort address,
        out string? error)
    {
        area = default;
        address = default;
        error = null;
        if (string.IsNullOrWhiteSpace(raw))
        {
            error = "Modbus TAG address is required. Use canonical 0-based syntax such as 'holding:0'.";
            return false;
        }

        var value = raw.Trim();
        var separator = value.IndexOf(':');
        string addressPart;
        if (separator > 0)
        {
            var areaPart = value[..separator].Trim();
            addressPart = value[(separator + 1)..].Trim();
            if (!TryParseArea(areaPart, out area))
            {
                error = $"Unknown Modbus area '{areaPart}'. Use coil, discrete, holding or input.";
                return false;
            }
        }
        else
        {
            addressPart = value;
            var areaText = Meta(metadata, "modbus.area");
            if (string.IsNullOrWhiteSpace(areaText) || !TryParseArea(areaText, out area))
            {
                error = "Numeric Modbus addresses require metadata 'modbus.area' with coil, discrete, holding or input.";
                return false;
            }
        }

        if (!ushort.TryParse(addressPart, NumberStyles.None, CultureInfo.InvariantCulture, out address))
        {
            error = $"Modbus address '{addressPart}' must be a decimal 0-based value from 0 to 65535.";
            return false;
        }

        return true;
    }

    public static string Format(ModbusDataArea area, ushort address) =>
        $"{AreaToken(area)}:{address.ToString(CultureInfo.InvariantCulture)}";

    public static bool TryBuild(
        ModbusDataArea area,
        int reference,
        ModbusAddressReferenceBase referenceBase,
        out string? canonicalAddress,
        out string? error)
    {
        canonicalAddress = null;
        error = null;

        var zeroBased = referenceBase switch
        {
            ModbusAddressReferenceBase.ZeroBased => reference,
            ModbusAddressReferenceBase.OneBased => reference - 1,
            _ => throw new ArgumentOutOfRangeException(nameof(referenceBase))
        };

        if (zeroBased is < 0 or > ushort.MaxValue)
        {
            var lower = referenceBase == ModbusAddressReferenceBase.OneBased ? 1 : 0;
            var upper = referenceBase == ModbusAddressReferenceBase.OneBased ? ushort.MaxValue + 1 : ushort.MaxValue;
            error = $"Modbus {referenceBase} reference must be from {lower} to {upper}.";
            return false;
        }

        canonicalAddress = Format(area, checked((ushort)zeroBased));
        return true;
    }

    public static bool TryParseArea(string raw, out ModbusDataArea area)
    {
        switch (raw.Trim().Replace("_", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal).ToLowerInvariant())
        {
            case "coil":
            case "coils": area = ModbusDataArea.Coil; return true;
            case "discrete":
            case "discreteinput":
            case "di": area = ModbusDataArea.DiscreteInput; return true;
            case "holding":
            case "holdingregister":
            case "hr": area = ModbusDataArea.HoldingRegister; return true;
            case "input":
            case "inputregister":
            case "ir": area = ModbusDataArea.InputRegister; return true;
            default: area = default; return false;
        }
    }

    private static string AreaToken(ModbusDataArea area) => area switch
    {
        ModbusDataArea.Coil => "coil",
        ModbusDataArea.DiscreteInput => "discrete",
        ModbusDataArea.HoldingRegister => "holding",
        ModbusDataArea.InputRegister => "input",
        _ => throw new ArgumentOutOfRangeException(nameof(area))
    };

    private static string? Meta(IReadOnlyDictionary<string, string>? map, string key) =>
        map is not null && map.TryGetValue(key, out var value) ? value : null;
}
