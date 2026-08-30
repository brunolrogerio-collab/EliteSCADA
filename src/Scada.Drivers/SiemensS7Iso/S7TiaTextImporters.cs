using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;

namespace Scada.Drivers.SiemensS7Iso;

internal sealed record S7TiaImportRecord(
    string SourceKind,
    string SourceName,
    string Name,
    string? Path,
    string? DataTypeText,
    string? LogicalAddress,
    string? Comment,
    bool? HmiVisible,
    bool? HmiAccessible,
    bool? HmiWriteable,
    bool? Retain = null,
    bool IsConstant = false,
    string? ConstantValue = null);

internal static partial class S7TiaImportCandidateFactory
{
    public static DriverImportCandidate Create(S7TiaImportRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        var name = string.IsNullOrWhiteSpace(record.Name) ? "Unnamed" : record.Name.Trim();
        var issues = new List<DriverEngineeringIssue>();

        if (string.IsNullOrWhiteSpace(record.Name))
            issues.Add(Issue("S7_TIA_NAME_MISSING", DriverEngineeringIssueSeverity.Error, "TIA PLC tag has no name."));
        if (record.IsConstant)
            issues.Add(Issue(
                "S7_TIA_CONSTANT_UNSUPPORTED",
                DriverEngineeringIssueSeverity.Error,
                "TIA PLC constants remain visible in Preview but are not process TAG bindings in the first S7 ISO runtime slice."));
        if (string.IsNullOrWhiteSpace(record.DataTypeText))
            issues.Add(Issue("S7_TIA_DATATYPE_MISSING", DriverEngineeringIssueSeverity.Error, "TIA PLC tag has no data type."));
        if (!record.IsConstant && string.IsNullOrWhiteSpace(record.LogicalAddress))
            issues.Add(Issue("S7_TIA_ADDRESS_MISSING", DriverEngineeringIssueSeverity.Error, "TIA PLC tag has no logical address."));

        S7TypeMapping? typeMapping = null;
        if (!string.IsNullOrWhiteSpace(record.DataTypeText) &&
            !TryMapDataType(record.DataTypeText, out typeMapping, out var typeError))
        {
            issues.Add(Issue("S7_TIA_DATATYPE_UNSUPPORTED", DriverEngineeringIssueSeverity.Error, typeError!));
        }

        S7AddressMapping? addressMapping = null;
        if (!record.IsConstant &&
            !string.IsNullOrWhiteSpace(record.LogicalAddress) &&
            !TryParseLogicalAddress(record.LogicalAddress, out addressMapping, out var addressError))
        {
            issues.Add(Issue("S7_TIA_ADDRESS_UNSUPPORTED", DriverEngineeringIssueSeverity.Error, addressError!));
        }

        S7IsoTagBinding? binding = null;
        if (!record.IsConstant && typeMapping is not null && addressMapping is not null)
        {
            if (addressMapping.BitOffset.HasValue && typeMapping.ValueType != S7IsoValueType.Boolean)
            {
                issues.Add(Issue(
                    "S7_TIA_BIT_TYPE_MISMATCH",
                    DriverEngineeringIssueSeverity.Error,
                    $"TIA logical address '{record.LogicalAddress}' is bit-oriented but data type '{record.DataTypeText}' is not Boolean."));
            }
            else if (!addressMapping.BitOffset.HasValue && typeMapping.ValueType == S7IsoValueType.Boolean)
            {
                issues.Add(Issue(
                    "S7_TIA_BOOL_ADDRESS_REQUIRED",
                    DriverEngineeringIssueSeverity.Error,
                    $"TIA Boolean tag '{name}' requires a bit logical address such as %M0.0 or %DB1.DBX0.0."));
            }
            else
            {
                var writable = record.HmiWriteable == true && addressMapping.Area != S7IsoArea.Input;
                var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["area"] = addressMapping.Area.ToString(),
                    ["dbNumber"] = addressMapping.DbNumber.ToString(CultureInfo.InvariantCulture),
                    ["byteOffset"] = addressMapping.ByteOffset.ToString(CultureInfo.InvariantCulture),
                    ["bitOffset"] = (addressMapping.BitOffset ?? 0).ToString(CultureInfo.InvariantCulture),
                    ["valueType"] = typeMapping.ValueType.ToString(),
                    ["stringLength"] = typeMapping.StringLength.ToString(CultureInfo.InvariantCulture),
                    ["writable"] = writable ? "true" : "false",
                    ["valueOrder"] = nameof(S7IsoValueOrder.Normal)
                };

                if (!S7IsoTagBinding.TryCreateFromSettings(settings, out binding, out var bindingIssues))
                {
                    foreach (var bindingIssue in bindingIssues)
                        issues.Add(Issue("S7_TIA_BINDING_INVALID", DriverEngineeringIssueSeverity.Error, bindingIssue.Message));
                }
            }
        }

        if (record.HmiAccessible == false)
            issues.Add(Issue(
                "S7_TIA_HMI_NOT_ACCESSIBLE",
                DriverEngineeringIssueSeverity.Warning,
                "TIA marks this PLC tag as not HMI Accessible; runtime classic access must not be assumed."));
        if (record.HmiVisible == false)
            issues.Add(Issue(
                "S7_TIA_HMI_NOT_VISIBLE",
                DriverEngineeringIssueSeverity.Information,
                "TIA marks this PLC tag as not HMI Visible."));
        if (!record.IsConstant && record.HmiWriteable is null)
            issues.Add(Issue(
                "S7_TIA_HMI_WRITEABILITY_UNKNOWN",
                DriverEngineeringIssueSeverity.Information,
                "TIA export does not provide HMI writeability; imported write intent remains disabled until explicitly engineered."));

        var supportStatus = issues.Any(issue => issue.Severity == DriverEngineeringIssueSeverity.Error)
            ? "Unsupported"
            : issues.Any(issue => issue.Severity == DriverEngineeringIssueSeverity.Warning)
                ? "Warning"
                : "Supported";

        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["sourceKind"] = record.SourceKind,
            ["sourceName"] = record.SourceName,
            ["entityKind"] = record.IsConstant ? "Constant" : "Tag",
            ["tiaName"] = name,
            ["tiaPath"] = record.Path ?? string.Empty,
            ["logicalAddress"] = record.LogicalAddress ?? string.Empty,
            ["siemensDataType"] = record.DataTypeText ?? string.Empty,
            ["comment"] = record.Comment ?? string.Empty,
            ["hmiVisible"] = FormatOptionalBoolean(record.HmiVisible),
            ["hmiAccessible"] = FormatOptionalBoolean(record.HmiAccessible),
            ["hmiWriteable"] = FormatOptionalBoolean(record.HmiWriteable),
            ["retain"] = FormatOptionalBoolean(record.Retain),
            ["constantValue"] = record.ConstantValue ?? string.Empty,
            ["supportStatus"] = supportStatus
        };

        var stableIdentity = string.Join("|", record.SourceKind, record.Path ?? string.Empty, name);
        var candidateId = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(stableIdentity)))
            .ToLowerInvariant()[..24];
        var unsupportedPrefix = record.SourceKind switch
        {
            "TiaXlsx" => "tia-xlsx",
            "TiaXml" => "tia-xml",
            "TiaSdf" => "tia-sdf",
            _ => "tia-export"
        };
        var portableAddress = binding?.ToPortableAddress() ?? $"{unsupportedPrefix}:unsupported:{candidateId}";
        var readable = !record.IsConstant && record.HmiAccessible != false && binding is not null;
        var writableCandidate = readable && record.HmiWriteable == true && binding?.Writable == true;

        return S7TiaImportValidation.ValidateAddressWidth(new DriverImportCandidate(
            candidateId,
            stableIdentity,
            name,
            portableAddress,
            readable,
            writableCandidate,
            binding is null ? null : typeMapping?.TagDataType,
            Metadata: metadata,
            Issues: issues));
    }

    public static bool? ParseOptionalBoolean(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var value = raw.Trim();
        if (bool.TryParse(value, out var parsed)) return parsed;
        if (value == "1") return true;
        if (value == "0") return false;
        return null;
    }

    private static bool TryMapDataType(string raw, out S7TypeMapping? mapping, out string? error)
    {
        mapping = null;
        error = null;
        var normalized = raw.Trim();
        var stringMatch = StringTypeRegex().Match(normalized);
        if (stringMatch.Success)
        {
            var length = stringMatch.Groups[1].Success
                ? int.Parse(stringMatch.Groups[1].Value, CultureInfo.InvariantCulture)
                : 254;
            if (length is < 1 or > 254)
            {
                error = $"TIA STRING length {length} is outside the supported classic range 1..254.";
                return false;
            }
            mapping = new S7TypeMapping(TagDataType.String, S7IsoValueType.String, checked((byte)length));
            return true;
        }

        mapping = normalized.ToUpperInvariant() switch
        {
            "BOOL" => new(TagDataType.Boolean, S7IsoValueType.Boolean, 0),
            "BYTE" or "USINT" => new(TagDataType.Int16, S7IsoValueType.Byte, 0),
            "SINT" => new(TagDataType.Int16, S7IsoValueType.SInt, 0),
            "WORD" or "UINT" => new(TagDataType.Int32, S7IsoValueType.UInt16, 0),
            "INT" => new(TagDataType.Int16, S7IsoValueType.Int16, 0),
            "DWORD" or "UDINT" => new(TagDataType.Int64, S7IsoValueType.UInt32, 0),
            "DINT" => new(TagDataType.Int32, S7IsoValueType.Int32, 0),
            "REAL" => new(TagDataType.Float, S7IsoValueType.Float32, 0),
            "LINT" => new(TagDataType.Int64, S7IsoValueType.Int64, 0),
            "LREAL" => new(TagDataType.Double, S7IsoValueType.Float64, 0),
            "DATE" => new(TagDataType.DateTime, S7IsoValueType.Date, 0),
            "DATE_AND_TIME" or "DT" => new(TagDataType.DateTime, S7IsoValueType.DateTime, 0),
            _ => null
        };

        if (mapping is not null) return true;
        error = $"TIA data type '{raw}' is not supported by the first classic S7 ISO runtime mapping.";
        return false;
    }

    private static bool TryParseLogicalAddress(string raw, out S7AddressMapping? mapping, out string? error)
    {
        mapping = null;
        error = null;
        var value = raw.Trim().Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
        if (value.StartsWith('%')) value = value[1..];

        var db = DbAddressRegex().Match(value);
        if (db.Success)
        {
            if (!ushort.TryParse(db.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var dbNumber) ||
                dbNumber == 0 ||
                !int.TryParse(db.Groups[3].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var byteOffset))
            {
                error = $"TIA DB logical address '{raw}' is outside supported numeric ranges.";
                return false;
            }

            var width = db.Groups[2].Value;
            var hasBit = db.Groups[4].Success;
            if (width == "X" && !hasBit)
            {
                error = $"TIA DBX logical address '{raw}' requires an explicit bit index.";
                return false;
            }
            if (width != "X" && hasBit)
            {
                error = $"TIA logical address '{raw}' may use a bit index only with DBX addressing.";
                return false;
            }

            byte? bit = null;
            if (hasBit)
            {
                if (!byte.TryParse(db.Groups[4].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedBit) || parsedBit > 7)
                {
                    error = $"TIA DB bit address '{raw}' requires a bit index from 0 to 7.";
                    return false;
                }
                bit = parsedBit;
            }

            if (byteOffset > 2_097_151)
            {
                error = $"TIA DB logical address '{raw}' exceeds the 24-bit S7ANY address range.";
                return false;
            }

            mapping = new S7AddressMapping(S7IsoArea.DataBlock, dbNumber, byteOffset, bit);
            return true;
        }

        var absolute = AbsoluteAddressRegex().Match(value);
        if (!absolute.Success)
        {
            error = $"TIA logical address '{raw}' is not a supported absolute I/Q/M/DB address for classic S7 ISO.";
            return false;
        }

        var areaToken = absolute.Groups[1].Value;
        var widthToken = absolute.Groups[2].Value;
        var hasAbsoluteBit = absolute.Groups[4].Success;
        if (hasAbsoluteBit && widthToken.Length > 0)
        {
            error = $"TIA bit logical address '{raw}' must not include B/W/D width notation.";
            return false;
        }
        if (!hasAbsoluteBit && widthToken.Length == 0)
        {
            error = $"TIA non-bit logical address '{raw}' requires B, W or D width notation.";
            return false;
        }

        var area = areaToken switch
        {
            "I" or "E" => S7IsoArea.Input,
            "Q" or "A" => S7IsoArea.Output,
            "M" => S7IsoArea.Merker,
            _ => throw new InvalidOperationException("Unexpected S7 area token.")
        };
        if (!int.TryParse(absolute.Groups[3].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var offset) ||
            offset > 2_097_151)
        {
            error = $"TIA logical address '{raw}' exceeds the 24-bit S7ANY address range.";
            return false;
        }

        byte? bitOffset = null;
        if (hasAbsoluteBit)
        {
            if (!byte.TryParse(absolute.Groups[4].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedBit) || parsedBit > 7)
            {
                error = $"TIA bit address '{raw}' requires a bit index from 0 to 7.";
                return false;
            }
            bitOffset = parsedBit;
        }

        mapping = new S7AddressMapping(area, 0, offset, bitOffset);
        return true;
    }

    private static string FormatOptionalBoolean(bool? value) =>
        value.HasValue ? (value.Value ? "true" : "false") : string.Empty;

    private static DriverEngineeringIssue Issue(string code, DriverEngineeringIssueSeverity severity, string message) =>
        new(code, severity, message);

    [GeneratedRegex(@"^STRING(?:\[(\d{1,3})\])?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex StringTypeRegex();

    [GeneratedRegex(@"^DB(\d+)\.DB([XBWD])(\d+)(?:\.(\d+))?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DbAddressRegex();

    [GeneratedRegex(@"^([IQEAM])([BWD]?)(\d+)(?:\.(\d+))?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AbsoluteAddressRegex();

    private sealed record S7TypeMapping(TagDataType TagDataType, S7IsoValueType ValueType, byte StringLength);
    private sealed record S7AddressMapping(S7IsoArea Area, ushort DbNumber, int ByteOffset, byte? BitOffset);
}

internal static class S7TiaXmlImporter
{
    public static IReadOnlyList<DriverImportCandidate> Parse(
        string sourceName,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        ValidateInput(sourceName, content, "XML");
        var document = XDocument.Load(content, LoadOptions.None);
        var root = document.Root;
        if (root is null || !root.Name.LocalName.Equals("Tagtable", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("TIA XML export must have a Tagtable root element.");

        var tableName = Attribute(root, "name") ?? string.Empty;
        var result = new List<DriverImportCandidate>();
        foreach (var element in root.Elements())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var kind = element.Name.LocalName;
            var isTag = kind.Equals("Tag", StringComparison.OrdinalIgnoreCase);
            var isConstant = kind.Equals("Constant", StringComparison.OrdinalIgnoreCase);
            if (!isTag && !isConstant) continue;

            var record = new S7TiaImportRecord(
                "TiaXml",
                sourceName,
                element.Value.Trim(),
                tableName,
                Attribute(element, "type"),
                isConstant ? null : Attribute(element, "addr"),
                Attribute(element, "remark"),
                S7TiaImportCandidateFactory.ParseOptionalBoolean(Attribute(element, "hmiVisible")),
                S7TiaImportCandidateFactory.ParseOptionalBoolean(Attribute(element, "hmiAccessible")),
                S7TiaImportCandidateFactory.ParseOptionalBoolean(Attribute(element, "hmiWriteable")),
                S7TiaImportCandidateFactory.ParseOptionalBoolean(Attribute(element, "retain")),
                isConstant,
                isConstant ? Attribute(element, "value") : null);
            result.Add(S7TiaImportCandidateFactory.Create(record));
        }

        return result;
    }

    private static string? Attribute(XElement element, string name) =>
        element.Attributes()
            .FirstOrDefault(attribute => attribute.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase))?
            .Value;

    private static void ValidateInput(string sourceName, Stream content, string format)
    {
        if (string.IsNullOrWhiteSpace(sourceName))
            throw new ArgumentException("TIA import source name is required.", nameof(sourceName));
        ArgumentNullException.ThrowIfNull(content);
        if (!content.CanRead)
            throw new ArgumentException($"TIA {format} import stream must be readable.", nameof(content));
    }
}

internal static class S7TiaSdfImporter
{
    public static IReadOnlyList<DriverImportCandidate> Parse(
        string sourceName,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceName))
            throw new ArgumentException("TIA import source name is required.", nameof(sourceName));
        ArgumentNullException.ThrowIfNull(content);
        if (!content.CanRead)
            throw new ArgumentException("TIA SDF import stream must be readable.", nameof(content));

        using var reader = new StreamReader(content, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var text = reader.ReadToEnd();
        var rows = ParseCsv(text);
        if (rows.Count == 0) return Array.Empty<DriverImportCandidate>();

        var firstDataRow = IsHeader(rows[0]) ? 1 : 0;
        var result = new List<DriverImportCandidate>();
        for (var index = firstDataRow; index < rows.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var row = rows[index];
            if (row.All(string.IsNullOrWhiteSpace)) continue;
            if (row.Count < 3)
                throw new InvalidDataException($"TIA SDF row {index + 1} has {row.Count} column(s); at least Name, Address and Data type are required.");

            var name = Cell(row, 0);
            var address = Cell(row, 1);
            var isConstant = address.Equals("CONSTANT", StringComparison.OrdinalIgnoreCase);
            var record = new S7TiaImportRecord(
                "TiaSdf",
                sourceName,
                name,
                null,
                NullIfEmpty(Cell(row, 2)),
                isConstant ? null : NullIfEmpty(address),
                NullIfEmpty(Cell(row, 6)),
                S7TiaImportCandidateFactory.ParseOptionalBoolean(NullIfEmpty(Cell(row, 4))),
                S7TiaImportCandidateFactory.ParseOptionalBoolean(NullIfEmpty(Cell(row, 3))),
                S7TiaImportCandidateFactory.ParseOptionalBoolean(NullIfEmpty(Cell(row, 8))),
                S7TiaImportCandidateFactory.ParseOptionalBoolean(NullIfEmpty(Cell(row, 5))),
                isConstant,
                isConstant ? NullIfEmpty(Cell(row, 7)) : null);
            result.Add(S7TiaImportCandidateFactory.Create(record));
        }

        return result;
    }

    private static bool IsHeader(IReadOnlyList<string> row) =>
        row.Count >= 3 &&
        row[0].Trim().Equals("Name", StringComparison.OrdinalIgnoreCase) &&
        row[1].Trim().Equals("Address", StringComparison.OrdinalIgnoreCase);

    private static string Cell(IReadOnlyList<string> row, int index) =>
        index < row.Count ? row[index].Trim() : string.Empty;

    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static IReadOnlyList<IReadOnlyList<string>> ParseCsv(string text)
    {
        var rows = new List<IReadOnlyList<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var quoted = false;

        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (quoted)
            {
                if (character == '"')
                {
                    if (index + 1 < text.Length && text[index + 1] == '"')
                    {
                        field.Append('"');
                        index++;
                    }
                    else
                    {
                        quoted = false;
                    }
                }
                else
                {
                    field.Append(character);
                }
                continue;
            }

            switch (character)
            {
                case '"' when field.Length == 0:
                    quoted = true;
                    break;
                case ',':
                    row.Add(field.ToString());
                    field.Clear();
                    break;
                case '\r':
                case '\n':
                    row.Add(field.ToString());
                    field.Clear();
                    if (row.Any(value => value.Length > 0)) rows.Add(row.ToArray());
                    row = new List<string>();
                    if (character == '\r' && index + 1 < text.Length && text[index + 1] == '\n') index++;
                    break;
                default:
                    field.Append(character);
                    break;
            }
        }

        if (quoted)
            throw new InvalidDataException("TIA SDF contains an unterminated quoted field.");

        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            if (row.Any(value => value.Length > 0)) rows.Add(row.ToArray());
        }

        return rows;
    }
}
