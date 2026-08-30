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
    private const int DefaultMaximumRows = 100_000;
    private const int HardMaximumRows = 1_000_000;
    private const int DefaultMaximumLineLength = 65_536;
    private const int HardMaximumLineLength = 1_048_576;
    private const long DefaultMaximumFileBytes = 16L * 1024 * 1024;
    private const long HardMaximumFileBytes = 256L * 1024 * 1024;
    private const int MaximumSourceLinesMetadata = 64;

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
            Description = "IEC 60870-5-104 Engineering provider with bounded GI browse and resource-bounded monitored point-list CSV import. Import candidates remain read-only until canonical Engineering validates/applies a binding."
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

        var limits = ParseLimits(request.Parameters);
        if (content.CanSeek && content.Length - content.Position > limits.MaximumFileBytes)
        {
            throw new InvalidDataException(
                $"IEC-104 point-list CSV exceeds maximumFileBytes={limits.MaximumFileBytes.ToString(CultureInfo.InvariantCulture)}.");
        }

        using var boundedContent = new BoundedReadStream(content, limits.MaximumFileBytes);
        using var reader = new StreamReader(
            boundedContent,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 4096,
            leaveOpen: true);

        var headerLine = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        if (headerLine is null)
            throw new InvalidDataException("IEC-104 point-list CSV is empty.");
        ValidateLine(headerLine, lineNumber: 1, limits.MaximumLineLength);

        var header = ParseCsvLine(headerLine, lineNumber: 1);
        var columns = BuildColumnMap(header);
        RequireColumn(columns, "commonAddress");
        RequireColumn(columns, "informationObjectAddress");
        RequireColumn(columns, "typeId");

        var aggregates = new Dictionary<Iec104PortablePointAddress, ImportedAggregate>();
        var lineNumber = 1;
        var dataRowCount = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null)
                break;

            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
                continue;

            dataRowCount++;
            if (dataRowCount > limits.MaximumRows)
            {
                throw new InvalidDataException(
                    $"IEC-104 point-list CSV exceeds maximumRows={limits.MaximumRows.ToString(CultureInfo.InvariantCulture)} data rows.");
            }

            ValidateLine(line, lineNumber, limits.MaximumLineLength);
            var fields = ParseCsvLine(line, lineNumber);
            if (fields.Count != header.Count)
            {
                throw new InvalidDataException(
                    $"IEC-104 point-list CSV line {lineNumber} has {fields.Count} field(s); expected {header.Count} from the header.");
            }

            var row = ParseRow(fields, columns, lineNumber);
            if (!aggregates.TryGetValue(row.Address, out var aggregate))
            {
                aggregate = new ImportedAggregate(row.Address);
                aggregates.Add(row.Address, aggregate);
            }

            aggregate.Observe(row);
        }

        foreach (var aggregate in aggregates.Values
                     .OrderBy(static candidate => candidate.Address.CommonAddress)
                     .ThenBy(static candidate => candidate.Address.InformationObjectAddress))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var declaredTypes = aggregate.TypeIds.OrderBy(static typeId => (byte)typeId).ToArray();
            var suggestedTypes = declaredTypes.Select(MapSuggestedDataType).Distinct().ToArray();
            var suggestedDataType = suggestedTypes.Length == 1 ? suggestedTypes[0] : (TagDataType?)null;
            var issues = new List<DriverEngineeringIssue>();

            if (aggregate.RowCount > 1)
            {
                issues.Add(new DriverEngineeringIssue(
                    "iec104.import.duplicate",
                    DriverEngineeringIssueSeverity.Warning,
                    $"IEC-104 point {aggregate.Address} appears on {aggregate.RowCount} CSV rows; the rows were collapsed into one transient import candidate."));
            }

            if (declaredTypes.Length > 1)
            {
                issues.Add(new DriverEngineeringIssue(
                    "iec104.import.typeConflict",
                    DriverEngineeringIssueSeverity.Warning,
                    $"IEC-104 point {aggregate.Address} declares multiple Type IDs ({string.Join(",", declaredTypes.Select(static typeId => (byte)typeId))}); binding requires explicit Engineering review."));
            }

            if (aggregate.SourceLinesTruncated)
            {
                issues.Add(new DriverEngineeringIssue(
                    "iec104.import.sourceLinesTruncated",
                    DriverEngineeringIssueSeverity.Information,
                    $"IEC-104 point {aggregate.Address} has more than {MaximumSourceLinesMetadata} source rows; metadata keeps only the first {MaximumSourceLinesMetadata} line numbers."));
            }

            var displayName = aggregate.DisplayName
                ?? $"CA {aggregate.Address.CommonAddress} / IOA {aggregate.Address.InformationObjectAddress}";
            var portableAddress = aggregate.Address.ToString();
            var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["commonAddress"] = aggregate.Address.CommonAddress.ToString(CultureInfo.InvariantCulture),
                ["informationObjectAddress"] = aggregate.Address.InformationObjectAddress.ToString(CultureInfo.InvariantCulture),
                ["declaredTypeIds"] = string.Join(",", declaredTypes.Select(static typeId => ((byte)typeId).ToString(CultureInfo.InvariantCulture))),
                ["declaredTypeNames"] = string.Join(",", declaredTypes),
                ["sourceLines"] = string.Join(",", aggregate.SourceLines.Select(static value => value.ToString(CultureInfo.InvariantCulture))),
                ["sourceRowCount"] = aggregate.RowCount.ToString(CultureInfo.InvariantCulture)
            };
            if (aggregate.SourceLinesTruncated)
                metadata["sourceLinesTruncated"] = "true";

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

    private static ImportLimits ParseLimits(IReadOnlyDictionary<string, string>? parameters) =>
        new(
            ReadIntParameter(parameters, "maximumRows", DefaultMaximumRows, 1, HardMaximumRows),
            ReadIntParameter(parameters, "maximumLineLength", DefaultMaximumLineLength, 1, HardMaximumLineLength),
            ReadLongParameter(parameters, "maximumFileBytes", DefaultMaximumFileBytes, 1, HardMaximumFileBytes));

    private static int ReadIntParameter(
        IReadOnlyDictionary<string, string>? parameters,
        string key,
        int defaultValue,
        int minimum,
        int maximum)
    {
        if (parameters is null || !parameters.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
            return defaultValue;
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value < minimum || value > maximum)
        {
            throw new InvalidDataException(
                $"IEC-104 import parameter '{key}' must be an integer in the range {minimum.ToString(CultureInfo.InvariantCulture)}..{maximum.ToString(CultureInfo.InvariantCulture)}.");
        }

        return value;
    }

    private static long ReadLongParameter(
        IReadOnlyDictionary<string, string>? parameters,
        string key,
        long defaultValue,
        long minimum,
        long maximum)
    {
        if (parameters is null || !parameters.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
            return defaultValue;
        if (!long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value < minimum || value > maximum)
        {
            throw new InvalidDataException(
                $"IEC-104 import parameter '{key}' must be an integer in the range {minimum.ToString(CultureInfo.InvariantCulture)}..{maximum.ToString(CultureInfo.InvariantCulture)}.");
        }

        return value;
    }

    private static void ValidateLine(string line, int lineNumber, int maximumLineLength)
    {
        if (line.Length > maximumLineLength)
        {
            throw new InvalidDataException(
                $"IEC-104 point-list CSV line {lineNumber} exceeds maximumLineLength={maximumLineLength.ToString(CultureInfo.InvariantCulture)} characters.");
        }
        if (line.IndexOf('\0') >= 0)
            throw new InvalidDataException($"IEC-104 point-list CSV line {lineNumber} contains a NUL character.");
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

    private sealed record ImportLimits(int MaximumRows, int MaximumLineLength, long MaximumFileBytes);

    private sealed record ImportedRow(
        Iec104PortablePointAddress Address,
        Iec104TypeId TypeId,
        string? DisplayName,
        int LineNumber);

    private sealed class ImportedAggregate
    {
        private readonly HashSet<Iec104TypeId> _typeIds = new();
        private readonly List<int> _sourceLines = new();

        public ImportedAggregate(Iec104PortablePointAddress address)
        {
            Address = address;
        }

        public Iec104PortablePointAddress Address { get; }
        public IReadOnlyCollection<Iec104TypeId> TypeIds => _typeIds;
        public IReadOnlyList<int> SourceLines => _sourceLines;
        public int RowCount { get; private set; }
        public string? DisplayName { get; private set; }
        public bool SourceLinesTruncated => RowCount > _sourceLines.Count;

        public void Observe(ImportedRow row)
        {
            RowCount++;
            _typeIds.Add(row.TypeId);
            if (_sourceLines.Count < MaximumSourceLinesMetadata)
                _sourceLines.Add(row.LineNumber);
            if (DisplayName is null && !string.IsNullOrWhiteSpace(row.DisplayName))
                DisplayName = row.DisplayName;
        }
    }

    private sealed class BoundedReadStream : Stream
    {
        private readonly Stream _inner;
        private readonly long _maximumBytes;
        private long _bytesRead;

        public BoundedReadStream(Stream inner, long maximumBytes)
        {
            _inner = inner;
            _maximumBytes = maximumBytes;
        }

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => _bytesRead;
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = _inner.Read(buffer, offset, GetAllowedCount(count));
            RecordRead(read);
            return read;
        }

        public override int Read(Span<byte> buffer)
        {
            var read = _inner.Read(buffer[..GetAllowedCount(buffer.Length)]);
            RecordRead(read);
            return read;
        }

        public override async Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
        {
            var read = await _inner.ReadAsync(buffer, offset, GetAllowedCount(count), cancellationToken).ConfigureAwait(false);
            RecordRead(read);
            return read;
        }

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            var read = await _inner.ReadAsync(buffer[..GetAllowedCount(buffer.Length)], cancellationToken).ConfigureAwait(false);
            RecordRead(read);
            return read;
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        private int GetAllowedCount(int requested)
        {
            if (requested <= 0)
                return 0;

            var remaining = _maximumBytes - _bytesRead;
            if (remaining < 0)
                return 1;
            return checked((int)Math.Min(requested, Math.Min(int.MaxValue, remaining + 1)));
        }

        private void RecordRead(int read)
        {
            _bytesRead += read;
            if (_bytesRead > _maximumBytes)
            {
                throw new InvalidDataException(
                    $"IEC-104 point-list CSV exceeds maximumFileBytes={_maximumBytes.ToString(CultureInfo.InvariantCulture)}.");
            }
        }
    }
}