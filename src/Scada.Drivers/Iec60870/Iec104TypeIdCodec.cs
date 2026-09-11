using System.Globalization;

namespace Scada.Drivers.Iec60870;

/// <summary>
/// Shared text boundary for IEC 60870-5-104 Type IDs. Runtime binding names are
/// the established enum-style contract used by activation. Standard IEC names
/// with underscores and numeric IDs remain accepted at import/protocol edges.
/// </summary>
public static class Iec104TypeIdCodec
{
    private static readonly IReadOnlyDictionary<string, Iec104TypeId> StandardByName =
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

    public static IReadOnlyCollection<string> MonitoredBindingNames { get; } = new[]
    {
        nameof(Iec104TypeId.MSpNa1),
        nameof(Iec104TypeId.MDpNa1),
        nameof(Iec104TypeId.MBoNa1),
        nameof(Iec104TypeId.MMeNa1),
        nameof(Iec104TypeId.MMeNb1),
        nameof(Iec104TypeId.MMeNc1),
        nameof(Iec104TypeId.MSpTb1),
        nameof(Iec104TypeId.MDpTb1),
        nameof(Iec104TypeId.MBoTb1),
        nameof(Iec104TypeId.MMeTd1),
        nameof(Iec104TypeId.MMeTe1),
        nameof(Iec104TypeId.MMeTf1)
    };

    public static IReadOnlyCollection<string> CommandBindingNames { get; } = new[]
    {
        nameof(Iec104TypeId.CScNa1),
        nameof(Iec104TypeId.CDcNa1),
        nameof(Iec104TypeId.CSeNa1),
        nameof(Iec104TypeId.CSeNb1),
        nameof(Iec104TypeId.CSeNc1)
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

        if (StandardByName.TryGetValue(value, out typeId))
            return true;

        return Enum.TryParse(value, ignoreCase: true, out typeId) && Enum.IsDefined(typeId);
    }

    public static string FormatStandard(Iec104TypeId typeId) => typeId switch
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
