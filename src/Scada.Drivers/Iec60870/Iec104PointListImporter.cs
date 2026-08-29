using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;

namespace Scada.Drivers.Iec60870;

/// <summary>
/// Engineering-only import for explicit IEC-104 monitored point lists.
/// Imported rows are transient candidates and never apply TAGs directly.
/// </summary>
public sealed class Iec104PointListImporter : ICommunicationDriverFileImporter
{
    private static readonly IReadOnlyDictionary<string, Iec104TypeId> StandardTypeNames =
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
            ["M_ME_TF_1"] = Iec104TypeId.MMeTf1
        };

    public Iec104PointListImporter()
    {
        var engineering = new Iec104EngineeringProvider();
        Descriptor = engineering.Descriptor with
        {
            EngineeringCapabilities = engineering.Descriptor.EngineeringCapabilities | DriverEngineeringCapabilities.FileImport,
            Description = "IEC 60870-5-104 Engineering provider with bounded GI browse and monitored point-list CSV import. Import candidates remain read-only until canonical Engineering validates/applies a binding."
        };
    }

    public CommunicationDriverTypeDescriptor Descriptor { get; }

    public async IAsyncEnumerable<DriverImportCandidate> ImportAsync(
        DriverImportRequest request,
        Stream content,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(content);
        cancellationToken.ThrowIfCancellationRequested();

        ValidateRequest(request);
        if (!content.CanRead)
            throw new ArgumentException("IEC-104 point-list import stream must be readable.", nameof(content));

        using var reader = new StreamReader(
            content,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 4096,
            leaveOpen: true);

        var headerLine = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        if (headerLine is null)
            throw new InvalidDataException("IEC-104 point-list CSV is empty.");

        var header = ParseCsvLine(headerLine, lineNumber: 1);
        var columns = BuildColumnMap(header);
        RequireColumn(columns, "commonAddress");
        RequireColumn(columns, "informationObjectAddress");
        RequireColumn(columns, "typeId");

        var rows = new List<ImportedRow>();
        var lineNumber = 1;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null)
                break;

            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var fields = ParseCsvLine(line, lineNumber);
            if (fields.Count != header.Count)
            {
                throw new InvalidDataException(
                    $"IEC-104 point-list CSV line {lineNumber} has {fields.Count} field(s); expected {header.Count} from the header.");
            }

            rows.Add(ParseRow(fields, columns, lineNumber));
        }

        foreach (var group in rows
                     .GroupBy(static row => row.Address)
                     .OrderBy(static group => group.Key.CommonAddress)
                     .ThenBy(static group => group.Key.InformationObjectAddress))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var groupedRows = group.ToArray();
            var declaredTypes = groupedRows.Select(static row => row.TypeId).Distinct().OrderBy(static typeId => (byte)typeId).ToArray();
            var suggestedTypes = declaredTypes.Select(MapSuggestedDataType).Distinct().ToArray();
            var suggestedDataType = suggestedTypes.Length == 1 ? suggestedTypes[0] : (TagDataType?)null;
            var issues = new List<DriverEngineeringIssue>();

            if (groupedRows.Length > 1)
            {
                issues.Add(new DriverEngineeringIssue(
                    "iec104.import.duplicate",
                    DriverEngineeringIssueSeverity.Warning,
                    $"IEC-104 point {group.Key} appears on {groupedRows.Length} CSV rows; the rows were collapsed into one transient import candidate."));
            }

            if (declaredTypes.Length > 1)
            {
                issues.Add(new DriverEngineeringIssue(
                    "iec104.import.typeConflict",
                    DriverEngineeringIssueSeverity.Warning,
                    $"IEC-104 point {group.Key} declares multiple Type IDs ({string.Join(",", declaredTypes.Select(static typeId => (byte)typeId))}); binding requires explicit Engineering review."));
            }

            var displayName = groupedRows
                .Select(static row => row.DisplayName)
                .FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value))
                ?? $"CA {group.Key.CommonAddress} / IOA {group.Key.InformationObjectAddress}";
            var portableAddress = group.Key.ToString();
            var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["commonAddress"] = group.Key.CommonAddress.ToString(CultureInfo.InvariantCulture),
                ["informationObjectAddress"] = group.Key.InformationObjectAddress.ToString(CultureInfo.InvariantCulture),
                ["declaredTypeIds"] = string.Join(",", declaredTypes.Select(static typeId => ((byte)typeId).ToString(CultureInfo.InvariantCulture))),
                ["declaredTypeNames"] = string.Join(",", declaredTypes),
                ["sourceLines"] = string.Join(",", groupedRows.Select(static row => row.LineNumber.ToString(CultureInfo.InvariantCulture)))
            };

            yield return new DriverImportCandidate(
                CandidateId: portableAddress,
                StableIdentity: portableAddress,
                DisplayName: displayName,
                PortableAddress: portableAddress,
                IsReadable: true,
                IsWritable: false,
                SuggestedDataType: suggestedDataType,
                Metadata: metadata,
                Issues: issues.Count == 0 ? null : issues);
        }
    }

    private static void ValidateRequest(DriverImportRequest request)
    {
        if (request.Context is not null &&
            !string.Equals(request.Context.DriverType, Iec104EngineeringConnectionTester.DriverType, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"IEC-104 point-list import context driver type must be '{Iec104EngineeringConnectionTester.DriverType}'.");
        }

        var contentType = request.ContentType?.Split(';', 2)[0].Trim();
        var acceptedContentType = string.IsNullOrWhiteSpace(contentType) ||
                                  contentType.Equals("text/csv", StringComparison.OrdinalIgnoreCase) ||
                                  contentType.Equals("application/csv", StringComparison.OrdinalIgnoreCase) ||
                                  contentType.Equals("text/plain", StringComparison.OrdinalIgnoreCase);
        var acceptedName = request.SourceName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase);
        if (!acceptedContentType || (!acceptedName && string.IsNullOrWhiteSpace(contentType)))
        {
            throw new NotSupportedException(
                "IEC-104 point-list importer accepts CSV input (text/csv, application/csv, text/plain, or a .csv source name)." );
        }
    }

    private static ImportedRow ParseRow(
        IReadOnlyList<string> fields,
        IReadOnlyDictionary<string, int> columns,
        int lineNumber)
    {
        var caRaw = fields[columns["commonAddress"]].Trim();
        var ioaRaw = fields[columns["informationObjectAddress"]].Trim();
        var typeRaw = fields[columns["typeId"]].Trim();

        if (!ushort.TryParse(caRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var commonAddress))
            throw new InvalidDataException($"IEC-104 point-list CSV line {lineNumber} has invalid Common Address '{caRaw}'.");
        if (!int.TryParse(ioaRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ioa) ||
            ioa is < 0 or > Iec104PortablePointAddress.MaximumInformationObjectAddress)
        {
            throw new InvalidDataException(
                $"IEC-104 point-list CSV line {lineNumber} has invalid IOA '{ioaRaw}'; expected 0..{Iec104PortablePointAddress.MaximumInformationObjectAddress}.");
        }

        if (!TryParseMonitoredTypeId(typeRaw, out var typeId))
            throw new InvalidDataException($"IEC-104 point-list CSV line {lineNumber} has unsupported monitored Type ID '{typeRaw}'.");

        var displayName = columns.TryGetValue("displayName", out var displayNameIndex)
            ? fields[displayNameIndex].Trim()
            : null;

        return new ImportedRow(
            new Iec104PortablePointAddress(commonAddress, ioa),
            typeId,
            string.IsNullOrWhiteSpace(displayName) ? null : displayName,
            lineNumber);
    }

    private static bool TryParseMonitoredTypeId(string raw, out Iec104TypeId typeId)
    {
        if (byte.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric))
            typeId = (Iec104TypeId)numeric;
        else if (StandardTypeNames.TryGetValue(raw, out var standardType))
            typeId = standardType;
        else if (!Enum.TryParse(raw, ignoreCase: true, out typeId))
            return false;

        return Iec104InformationObjectDecoder.IsSupported(typeId);
    }

    private static TagDataType MapSuggestedDataType(Iec104TypeId typeId) => typeId switch
    {
        Iec104TypeId.MSpNa1 or Iec104TypeId.MSpTb1 => TagDataType.Boolean,
        Iec104TypeId.MDpNa1 or Iec104TypeId.MDpTb1 => TagDataType.Enum,
        Iec104TypeId.MBoNa1 or Iec104TypeId.MBoTb1 => TagDataType.Int32,
        Iec104TypeId.MMeNa1 or Iec104TypeId.MMeTd1 => TagDataType.Float,
        Iec104TypeId.MMeNb1 or Iec104TypeId.MMeTe1 => TagDataType.Int16,
        Iec104TypeId.MMeNc1 or Iec104TypeId.MMeTf1 => TagDataType.Float,
        _ => throw new ArgumentOutOfRangeException(nameof(typeId), typeId, "Unsupported IEC-104 monitored Type ID.")
    };

    private static Dictionary<string, int> BuildColumnMap(IReadOnlyList<string> header)
    {
        var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < header.Count; index++)
        {
            var name = header[index].Trim();
            if (string.IsNullOrWhiteSpace(name))
                throw new InvalidDataException($"IEC-104 point-list CSV header column {index + 1} is empty.");
            if (!columns.TryAdd(name, index))
                throw new InvalidDataException($"IEC-104 point-list CSV header contains duplicate column '{name}'.");
        }

        return columns;
    }

    private static void RequireColumn(IReadOnlyDictionary<string, int> columns, string name)
    {
        if (!columns.ContainsKey(name))
            throw new InvalidDataException($"IEC-104 point-list CSV requires header column '{name}'.");
    }

    private static IReadOnlyList<string> ParseCsvLine(string line, int lineNumber)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var quoted = false;

        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (quoted)
            {
                if (character == '"')
                {
                    if (index + 1 < line.Length && line[index + 1] == '"')
                    {
                        current.Append('"');
                        index++;
                    }
                    else
                    {
                        quoted = false;
                    }
                }
                else
                {
                    current.Append(character);
                }

                continue;
            }

            if (character == '"')
            {
                if (current.Length != 0)
                    throw new InvalidDataException($"IEC-104 point-list CSV line {lineNumber} contains an unexpected quote.");
                quoted = true;
                continue;
            }

            if (character == ',')
            {
                fields.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(character);
        }

        if (quoted)
            throw new InvalidDataException($"IEC-104 point-list CSV line {lineNumber} contains an unterminated quoted field.");

        fields.Add(current.ToString());
        return fields;
    }

    private sealed record ImportedRow(
        Iec104PortablePointAddress Address,
        Iec104TypeId TypeId,
        string? DisplayName,
        int LineNumber);
}
