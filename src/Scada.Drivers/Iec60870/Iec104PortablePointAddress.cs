using System.Globalization;

namespace Scada.Drivers.Iec60870;

/// <summary>
/// Canonical library-independent IEC-104 point identity used by transient Engineering evidence.
/// It intentionally contains only Common Address + IOA. Expected semantic family and command profile
/// remain separate coordinated binding concerns and must not be smuggled into this address string.
/// </summary>
public readonly record struct Iec104PortablePointAddress
{
    public const int MaximumInformationObjectAddress = 0xFFFFFF;

    public Iec104PortablePointAddress(ushort commonAddress, int informationObjectAddress)
    {
        if (informationObjectAddress is < 0 or > MaximumInformationObjectAddress)
        {
            throw new ArgumentOutOfRangeException(
                nameof(informationObjectAddress),
                informationObjectAddress,
                $"IEC-104 Information Object Address must be in the range 0..{MaximumInformationObjectAddress}.");
        }

        CommonAddress = commonAddress;
        InformationObjectAddress = informationObjectAddress;
    }

    public ushort CommonAddress { get; }
    public int InformationObjectAddress { get; }

    public override string ToString() =>
        $"ca={CommonAddress.ToString(CultureInfo.InvariantCulture)};ioa={InformationObjectAddress.ToString(CultureInfo.InvariantCulture)}";

    public static Iec104PortablePointAddress Parse(string value)
    {
        if (!TryParse(value, out var address))
            throw new FormatException("IEC-104 portable point address must use canonical fields 'ca=<0..65535>;ioa=<0..16777215>'.");
        return address;
    }

    public static bool TryParse(string? value, out Iec104PortablePointAddress address)
    {
        address = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        ushort? commonAddress = null;
        int? informationObjectAddress = null;

        foreach (var segment in value.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = segment.IndexOf('=');
            if (separator <= 0 || separator == segment.Length - 1)
                return false;

            var key = segment[..separator].Trim();
            var rawValue = segment[(separator + 1)..].Trim();

            if (key.Equals("ca", StringComparison.OrdinalIgnoreCase))
            {
                if (commonAddress.HasValue ||
                    !ushort.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedCommonAddress))
                {
                    return false;
                }

                commonAddress = parsedCommonAddress;
                continue;
            }

            if (key.Equals("ioa", StringComparison.OrdinalIgnoreCase))
            {
                if (informationObjectAddress.HasValue ||
                    !int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedInformationObjectAddress) ||
                    parsedInformationObjectAddress is < 0 or > MaximumInformationObjectAddress)
                {
                    return false;
                }

                informationObjectAddress = parsedInformationObjectAddress;
                continue;
            }

            return false;
        }

        if (!commonAddress.HasValue || !informationObjectAddress.HasValue)
            return false;

        address = new Iec104PortablePointAddress(commonAddress.Value, informationObjectAddress.Value);
        return true;
    }
}
