using System.Globalization;
using System.Text;
using Scada.Drivers.Abstractions;

namespace Scada.Drivers.Iec60870;

/// <summary>
/// Driver-private CSV exporter paired with <see cref="Iec104PointListImporter"/>.
/// The common Driver SDK currently exposes file import but no file-export interface, so this utility
/// deliberately stays protocol-local until a shared export contract is coordinated.
/// </summary>
public static class Iec104PointListExporter
{
    public static async Task ExportAsync(
        IEnumerable<DriverImportCandidate> candidates,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(destination);
        if (!destination.CanWrite)
            throw new ArgumentException("IEC-104 point-list export stream must be writable.", nameof(destination));

        var rows = new List<ExportRow>();
        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(candidate);

            var address = Iec104PortablePointAddress.Parse(candidate.PortableAddress);
            var typeIds = ReadTypeIds(candidate);
            foreach (var typeId in typeIds)
            {
                if (!Iec104InformationObjectDecoder.IsSupported(typeId))
                {
                    throw new InvalidDataException(
                        $"IEC-104 export candidate '{candidate.PortableAddress}' contains unsupported monitored Type ID {(byte)typeId} ({typeId}).");
                }

                rows.Add(new ExportRow(address, typeId, candidate.DisplayName));
            }
        }

        rows.Sort(static (left, right) =>
        {
            var ca = left.Address.CommonAddress.CompareTo(right.Address.CommonAddress);
            if (ca != 0) return ca;
            var ioa = left.Address.InformationObjectAddress.CompareTo(right.Address.InformationObjectAddress);
            if (ioa != 0) return ioa;
            return ((byte)left.TypeId).CompareTo((byte)right.TypeId);
        });

        using var writer = new StreamWriter(
            destination,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            bufferSize: 4096,
            leaveOpen: true);

        await writer.WriteLineAsync("commonAddress,informationObjectAddress,typeId,displayName").ConfigureAwait(false);
        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await writer.WriteLineAsync(string.Join(",",
                row.Address.CommonAddress.ToString(CultureInfo.InvariantCulture),
                row.Address.InformationObjectAddress.ToString(CultureInfo.InvariantCulture),
                FormatStandardTypeName(row.TypeId),
                EscapeCsv(row.DisplayName))).ConfigureAwait(false);
        }

        await writer.FlushAsync().ConfigureAwait(false);
    }

    private static Iec104TypeId[] ReadTypeIds(DriverImportCandidate candidate)
    {
        if (candidate.Metadata is null)
        {
            throw new InvalidDataException(
                $"IEC-104 export candidate '{candidate.PortableAddress}' does not contain Type ID metadata.");
        }

        string? raw = null;
        if (candidate.Metadata.TryGetValue("declaredTypeIds", out var declared) && !string.IsNullOrWhiteSpace(declared))
            raw = declared;
        else if (candidate.Metadata.TryGetValue("observedTypeIds", out var observed) && !string.IsNullOrWhiteSpace(observed))
            raw = observed;

        if (raw is null)
        {
            throw new InvalidDataException(
                $"IEC-104 export candidate '{candidate.PortableAddress}' requires 'declaredTypeIds' or 'observedTypeIds' metadata.");
        }

        var result = new SortedSet<Iec104TypeId>();
        foreach (var item in raw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (!byte.TryParse(item, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric))
            {
                throw new InvalidDataException(
                    $"IEC-104 export candidate '{candidate.PortableAddress}' contains invalid Type ID metadata '{item}'.");
            }

            var typeId = (Iec104TypeId)numeric;
            if (!Iec104InformationObjectDecoder.IsSupported(typeId))
            {
                throw new InvalidDataException(
                    $"IEC-104 export candidate '{candidate.PortableAddress}' contains unsupported monitored Type ID {numeric}.");
            }

            result.Add(typeId);
        }

        if (result.Count == 0)
        {
            throw new InvalidDataException(
                $"IEC-104 export candidate '{candidate.PortableAddress}' contains no monitored Type IDs.");
        }

        return result.ToArray();
    }

    private static string FormatStandardTypeName(Iec104TypeId typeId) => typeId switch
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
        _ => throw new ArgumentOutOfRangeException(nameof(typeId), typeId, "Unsupported IEC-104 monitored Type ID.")
    };

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var requiresQuotes = value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0;
        if (!requiresQuotes)
            return value;

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private sealed record ExportRow(
        Iec104PortablePointAddress Address,
        Iec104TypeId TypeId,
        string DisplayName);
}
