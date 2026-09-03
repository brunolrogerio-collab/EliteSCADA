using System.Globalization;

namespace Scada.Drivers.Iec60870;

/// <summary>
/// Canonical text boundary for IEC 60870-5-104 Type IDs used by Engineering
/// contracts. Runtime enum names remain accepted as a legacy compatibility path,
/// while newly emitted bindings use the standard IEC names with underscores.
/// </summary>
public static class Iec104TypeIdCodec
{
    private static readonly IReadOnlyDictionary<string, Iec104TypeId> CanonicalByName =
        new Dictionary<string, Iec104TypeId>(StringComparer.OrdinalIgnoreCase)
        {
            ["M_SP_NA_1"] = Iec104TypeId.MSpNa1,
            ["M_DP_NA_1"] = Iec104TypeId.MDpNa1,
            ["M_BO_NA_1"] = Iec104TypeId.MBoNa1,
            ["M_ME_NA_1"] = Iec104TypeId.MMeNa1,
            ["M_ME_NB_1"] = Iec104TypeId.MMeNb1,
            ["M_ME_NC_1"] = Iec104TypeId.MMeNc1,
            ["M_SP_TB_1"] = Iec104TypeId.MSpTb1,
            ["M_DP_TB_1"] = Iec104TypeId.MDpTb1,
            ["M_BO_TB_1"] = Iec104TypeId.MBoTb1,
            ["M_ME_TD_1"] = Iec104TypeId.MMeTd1,
            ["M_ME_TE_1"] = Iec104TypeId.MMeTe1,
            ["M_ME_TF_1"] = Iec104TypeId.MMeTf1,
            ["C_SC_NA_1"] = Iec104TypeId.CScNa1,
            ["C_DC_NA_1"] = Iec104TypeId.CDcNa1,
            ["C_SE_NA_1"] = Iec104TypeId.CSeNa1,
            ["C_SE_NB_1"] = Iec104TypeId.CSeNb1,
            ["C_SE_NC_1"] = Iec104TypeId.CSeNc1,
            ["C_IC_NA_1"] = Iec104TypeId.CIcNa1
        };

    public static IReadOnlyCollection<string> MonitoredCanonicalNames { get; } = new[]
    {
        "M_SP_NA_1",
        "M_DP_NA_1",
        "M_BO_NA_1",
        "M_ME_NA_1",
        "M_ME_NB_1",
        "M_ME_NC_1",
        "M_SP_TB_1",
        "M_DP_TB_1",
        "M_BO_TB_1",
        "M_ME_TD_1",
        "M_ME_TE_1",
        "M_ME_TF_1"
    };

    public static IReadOnlyCollection<string> CommandCanonicalNames { get; } = new[]
    {
        "C_SC_NA_1",
        "C_DC_NA_1",
        "C_SE_NA_1",
        "C_SE_NB_1",
        "C_SE_NC_1"
    };

    public static bool TryParse(string? raw, out Iec104TypeId typeId)
    {
        typeId = default;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var value = raw.Trim();
        if (byte.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric))
        {
            typeId = (Iec104TypeId)numeric;
            return Enum.IsDefined(typeId);
        }

        if (CanonicalByName.TryGetValue(value, out typeId))
            return true;

        return Enum.TryParse(value, ignoreCase: true, out typeId) && Enum.IsDefined(typeId);
    }

    public static string FormatCanonical(Iec104TypeId typeId) => typeId switch
    {
        Iec104TypeId.MSpNa1 => "M_SP_NA_1",
        Iec104TypeId.MDpNa1 => "M_DP_NA_1",
        Iec104TypeId.MBoNa1 => "M_BO_NA_1",
        Iec104TypeId.MMeNa1 => "M_ME_NA_1",
        Iec104TypeId.MMeNb1 => "M_ME_NB_1",
        Iec104TypeId.MMeNc1 => "M_ME_NC_1",
        Iec104TypeId.MSpTb1 => "M_SP_TB_1",
        Iec104TypeId.MDpTb1 => "M_DP_TB_1",
        Iec104TypeId.MBoTb1 => "M_BO_TB_1",
        Iec104TypeId.MMeTd1 => "M_ME_TD_1",
        Iec104TypeId.MMeTe1 => "M_ME_TE_1",
        Iec104TypeId.MMeTf1 => "M_ME_TF_1",
        Iec104TypeId.CScNa1 => "C_SC_NA_1",
        Iec104TypeId.CDcNa1 => "C_DC_NA_1",
        Iec104TypeId.CSeNa1 => "C_SE_NA_1",
        Iec104TypeId.CSeNb1 => "C_SE_NB_1",
        Iec104TypeId.CSeNc1 => "C_SE_NC_1",
        Iec104TypeId.CIcNa1 => "C_IC_NA_1",
        _ => throw new ArgumentOutOfRangeException(nameof(typeId), typeId, "Unsupported IEC-104 Type ID.")
    };
}
