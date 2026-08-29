using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;

namespace Scada.Drivers.SiemensS7Iso;

internal static partial class S7TiaXlsxImporter
{
    private const string SourceKind = "TiaXlsx";

    public static IReadOnlyList<DriverImportCandidate> Parse(
        string sourceName,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceName))
            throw new ArgumentException("TIA import source name is required.", nameof(sourceName));
        ArgumentNullException.ThrowIfNull(content);
        if (!content.CanRead)
            throw new ArgumentException("TIA XLSX import stream must be readable.", nameof(content));

        using var archive = new ZipArchive(content, ZipArchiveMode.Read, leaveOpen: true);
        var sharedStrings = ReadSharedStrings(archive, cancellationToken);
        var sheetPath = ResolvePlcTagsSheetPath(archive, cancellationToken);
        var sheet = GetRequiredEntry(archive, sheetPath);

        using var sheetStream = sheet.Open();
        var document = XDocument.Load(sheetStream, LoadOptions.None);
        var rows = document.Descendants().Where(element => element.Name.LocalName == "row").ToArray();
        if (rows.Length == 0) return Array.Empty<DriverImportCandidate>();

        Dictionary<int, string>? headers = null;
        var result = new List<DriverImportCandidate>();
        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var cells = ReadRow(row, sharedStrings);
            if (cells.Count == 0) continue;

            if (headers is null)
            {
                headers = cells.ToDictionary(pair => pair.Key, pair => pair.Value.Trim());
                ValidateHeaders(headers);
                continue;
            }

            var values = headers
                .Where(header => !string.IsNullOrWhiteSpace(header.Value))
                .ToDictionary(
                    header => header.Value,
                    header => cells.TryGetValue(header.Key, out var value) ? value.Trim() : string.Empty,
                    StringComparer.OrdinalIgnoreCase);

            if (!values.TryGetValue("Name", out var name) || string.IsNullOrWhiteSpace(name))
                continue;

            result.Add(BuildCandidate(sourceName, values));
        }

        if (headers is null)
            throw new InvalidDataException("TIA XLSX PLCTags sheet does not contain a header row.");

        return result;
    }

    private static DriverImportCandidate BuildCandidate(
        string sourceName,
        IReadOnlyDictionary<string, string> values)
    {
        var name = Get(values, "Name") ?? "Unnamed";
        var path = Get(values, "Path");
        var dataTypeText = Get(values, "Data Type");
        var logicalAddress = Get(values, "Logical Address");
        var comment = Get(values, "Comment");
        var hmiVisible = ParseOptionalBoolean(Get(values, "Hmi Visible"));
        var hmiAccessible = ParseOptionalBoolean(Get(values, "Hmi Accessible"));
        var hmiWriteable = ParseOptionalBoolean(Get(values, "Hmi Writeable"));
        var issues = new List<DriverEngineeringIssue>();

        if (string.IsNullOrWhiteSpace(dataTypeText))
            issues.Add(Issue("S7_TIA_DATATYPE_MISSING", DriverEngineeringIssueSeverity.Error, "TIA PLC tag has no Data Type."));
        if (string.IsNullOrWhiteSpace(logicalAddress))
            issues.Add(Issue("S7_TIA_ADDRESS_MISSING", DriverEngineeringIssueSeverity.Error, "TIA PLC tag has no Logical Address."));

        S7TypeMapping? typeMapping = null;
        if (!string.IsNullOrWhiteSpace(dataTypeText) && !TryMapDataType(dataTypeText, out typeMapping, out var typeError))
            issues.Add(Issue("S7_TIA_DATATYPE_UNSUPPORTED", DriverEngineeringIssueSeverity.Error, typeError!));

        S7AddressMapping? addressMapping = null;
        if (!string.IsNullOrWhiteSpace(logicalAddress) && !TryParseLogicalAddress(logicalAddress, out addressMapping, out var addressError))
            issues.Add(Issue("S7_TIA_ADDRESS_UNSUPPORTED", DriverEngineeringIssueSeverity.Error, addressError!));

        S7IsoTagBinding? binding = null;
        if (typeMapping is not null && addressMapping is not null)
        {
            if (addressMapping.BitOffset.HasValue && typeMapping.ValueType != S7IsoValueType.Boolean)
            {
                issues.Add(Issue(
                    "S7_TIA_BIT_TYPE_MISMATCH",
                    DriverEngineeringIssueSeverity.Error,
                    $"TIA logical address '{logicalAddress}' is bit-oriented but data type '{dataTypeText}' is not Boolean."));
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
                var writable = hmiWriteable == true && addressMapping.Area != S7IsoArea.Input;
                var candidateBinding = new S7IsoTagBinding(
                    S7IsoTagBinding.CurrentSchemaVersion,
                    addressMapping.Area,
                    addressMapping.ByteOffset,
                    typeMapping.ValueType,
                    addressMapping.DbNumber,
                    addressMapping.BitOffset ?? 0,
                    writable,
                    typeMapping.StringLength,
                    S7IsoValueOrder.Normal);

                var bindingSettings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["area"] = candidateBinding.Area.ToString(),
                    ["dbNumber"] = candidateBinding.DbNumber.ToString(CultureInfo.InvariantCulture),
                    ["byteOffset"] = candidateBinding.ByteOffset.ToString(CultureInfo.InvariantCulture),
                    ["bitOffset"] = candidateBinding.BitOffset.ToString(CultureInfo.InvariantCulture),
                    ["valueType"] = candidateBinding.ValueType.ToString(),
                    ["stringLength"] = candidateBinding.StringLength.ToString(CultureInfo.InvariantCulture),
                    ["writable"] = candidateBinding.Writable ? "true" : "false",
                    ["valueOrder"] = candidateBinding.ValueOrder.ToString()
                };

                if (!S7IsoTagBinding.TryCreateFromSettings(bindingSettings, out binding, out var bindingIssues))
                {
                    foreach (var bindingIssue in bindingIssues)
                        issues.Add(Issue("S7_TIA_BINDING_INVALID", DriverEngineeringIssueSeverity.Error, bindingIssue.Message));
                }
            }
        }

        if (hmiAccessible == false)
            issues.Add(Issue(
                "S7_TIA_HMI_NOT_ACCESSIBLE",
                DriverEngineeringIssueSeverity.Warning,
                "TIA marks this PLC tag as not HMI Accessible; runtime classic access must not be assumed."));
        if (hmiVisible == false)
            issues.Add(Issue(
                "S7_TIA_HMI_NOT_VISIBLE",
                DriverEngineeringIssueSeverity.Information,
                "TIA marks this PLC tag as not HMI Visible."));
        if (hmiWriteable is null)
            issues.Add(Issue(
                "S7_TIA_HMI_WRITEABILITY_UNKNOWN",
                DriverEngineeringIssueSeverity.Information,
                "TIA export does not provide Hmi Writeable; imported write intent remains disabled until explicitly engineered."));

        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["sourceKind"] = SourceKind,
            ["sourceName"] = sourceName,
            ["tiaName"] = name,
            ["tiaPath"] = path ?? string.Empty,
            ["logicalAddress"] = logicalAddress ?? string.Empty,
            ["siemensDataType"] = dataTypeText ?? string.Empty,
            ["comment"] = comment ?? string.Empty,
            ["hmiVisible"] = FormatOptionalBoolean(hmiVisible),
            ["hmiAccessible"] = FormatOptionalBoolean(hmiAccessible),
            ["hmiWriteable"] = FormatOptionalBoolean(hmiWriteable),
            ["supportStatus"] = issues.Any(issue => issue.Severity == DriverEngineeringIssueSeverity.Error)
                ? "Unsupported"
                : issues.Any(issue => issue.Severity == DriverEngineeringIssueSeverity.Warning)
                    ? "Warning"
                    : "Supported"
        };

        var stableIdentity = string.Join("|", SourceKind, path ?? string.Empty, name, logicalAddress ?? string.Empty);
        var candidateId = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(stableIdentity))).ToLowerInvariant()[..24];
        var portableAddress = binding?.ToPortableAddress()
            ?? $"tia-xlsx:unsupported:{candidateId}";
        var readable = hmiAccessible != false && binding is not null;
        var writableCandidate = readable && hmiWriteable == true && binding?.Writable == true;

        return new DriverImportCandidate(
            candidateId,
            stableIdentity,
            name,
            portableAddress,
            readable,
            writableCandidate,
            binding is null ? null : typeMapping?.TagDataType,
            Metadata: metadata,
            Issues: issues);
    }

    private static bool TryMapDataType(
        string raw,
        out S7TypeMapping? mapping,
        out string? error)
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
            "WORD" or "UINT" => new(TagDataType.Int32, S7IsoValueType.UInt16, 0),
            "INT" => new(TagDataType.Int16, S7IsoValueType.Int16, 0),
            "DWORD" or "UDINT" => new(TagDataType.Int64, S7IsoValueType.UInt32, 0),
            "DINT" => new(TagDataType.Int32, S7IsoValueType.Int32, 0),
            "REAL" => new(TagDataType.Float, S7IsoValueType.Float32, 0),
            "LINT" => new(TagDataType.Int64, S7IsoValueType.Int64, 0),
            "LREAL" => new(TagDataType.Double, S7IsoValueType.Float64, 0),
            "DATE_AND_TIME" or "DT" => new(TagDataType.DateTime, S7IsoValueType.DateTime, 0),
            _ => null
        };

        if (mapping is not null) return true;
        error = $"TIA data type '{raw}' is not supported by the first classic S7 ISO runtime mapping.";
        return false;
    }

    private static bool TryParseLogicalAddress(
        string raw,
        out S7AddressMapping? mapping,
        out string? error)
    {
        mapping = null;
        error = null;
        var value = raw.Trim().Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
        if (value.StartsWith('%')) value = value[1..];

        var db = DbAddressRegex().Match(value);
        if (db.Success)
        {
            if (!ushort.TryParse(db.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var dbNumber) || dbNumber == 0 ||
                !int.TryParse(db.Groups[3].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var byteOffset))
            {
                error = $"TIA DB logical address '{raw}' is outside supported numeric ranges.";
                return false;
            }

            var format = db.Groups[2].Value;
            var hasBit = db.Groups[4].Success;
            if (format == "X" && !hasBit)
            {
                error = $"TIA DBX logical address '{raw}' requires an explicit bit index.";
                return false;
            }
            if (format != "X" && hasBit)
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

        var areaText = absolute.Groups[1].Value;
        var width = absolute.Groups[2].Value;
        var hasAbsoluteBit = absolute.Groups[4].Success;
        if (hasAbsoluteBit && width.Length > 0)
        {
            error = $"TIA bit logical address '{raw}' must not include B/W/D width notation.";
            return false;
        }
        if (!hasAbsoluteBit && width.Length == 0)
        {
            error = $"TIA non-bit logical address '{raw}' requires B, W or D width notation.";
            return false;
        }

        var area = areaText switch
        {
            "I" or "E" => S7IsoArea.Input,
            "Q" or "A" => S7IsoArea.Output,
            "M" => S7IsoArea.Merker,
            _ => throw new InvalidOperationException("Unexpected S7 area token.")
        };
        if (!int.TryParse(absolute.Groups[3].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var offset) || offset > 2_097_151)
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

    private static IReadOnlyList<string> ReadSharedStrings(
        ZipArchive archive,
        CancellationToken cancellationToken)
    {
        var entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry is null) return Array.Empty<string>();

        using var stream = entry.Open();
        var document = XDocument.Load(stream, LoadOptions.None);
        return document.Descendants()
            .Where(element => element.Name.LocalName == "si")
            .Select(item =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return string.Concat(item.Descendants().Where(element => element.Name.LocalName == "t").Select(element => element.Value));
            })
            .ToArray();
    }

    private static string ResolvePlcTagsSheetPath(
        ZipArchive archive,
        CancellationToken cancellationToken)
    {
        var workbookEntry = GetRequiredEntry(archive, "xl/workbook.xml");
        using var workbookStream = workbookEntry.Open();
        var workbook = XDocument.Load(workbookStream, LoadOptions.None);
        var sheet = workbook.Descendants()
            .FirstOrDefault(element =>
                element.Name.LocalName == "sheet" &&
                string.Equals((string?)element.Attribute("name"), "PLCTags", StringComparison.OrdinalIgnoreCase));
        if (sheet is null)
            throw new InvalidDataException("TIA XLSX file does not contain the required PLCTags sheet.");

        cancellationToken.ThrowIfCancellationRequested();
        var relationshipId = sheet.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == "id")?.Value;
        if (string.IsNullOrWhiteSpace(relationshipId))
            throw new InvalidDataException("TIA XLSX PLCTags sheet has no workbook relationship ID.");

        var relationshipsEntry = GetRequiredEntry(archive, "xl/_rels/workbook.xml.rels");
        using var relationshipsStream = relationshipsEntry.Open();
        var relationships = XDocument.Load(relationshipsStream, LoadOptions.None);
        var relationship = relationships.Descendants()
            .FirstOrDefault(element =>
                element.Name.LocalName == "Relationship" &&
                string.Equals((string?)element.Attribute("Id"), relationshipId, StringComparison.Ordinal));
        var target = (string?)relationship?.Attribute("Target");
        if (string.IsNullOrWhiteSpace(target))
            throw new InvalidDataException("TIA XLSX PLCTags worksheet relationship target is missing.");

        return NormalizeWorkbookTarget(target);
    }

    private static string NormalizeWorkbookTarget(string target)
    {
        var normalized = target.Replace('\\', '/').Trim();
        if (normalized.StartsWith('/')) normalized = normalized[1..];
        if (!normalized.StartsWith("xl/", StringComparison.OrdinalIgnoreCase))
            normalized = "xl/" + normalized;

        var segments = new List<string>();
        foreach (var segment in normalized.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".") continue;
            if (segment == "..")
            {
                if (segments.Count == 0)
                    throw new InvalidDataException("TIA XLSX worksheet relationship escapes the archive root.");
                segments.RemoveAt(segments.Count - 1);
                continue;
            }
            segments.Add(segment);
        }
        return string.Join('/', segments);
    }

    private static Dictionary<int, string> ReadRow(
        XElement row,
        IReadOnlyList<string> sharedStrings)
    {
        var result = new Dictionary<int, string>();
        var fallbackColumn = 0;
        foreach (var cell in row.Elements().Where(element => element.Name.LocalName == "c"))
        {
            var reference = (string?)cell.Attribute("r");
            var column = string.IsNullOrWhiteSpace(reference) ? fallbackColumn : GetColumnIndex(reference);
            fallbackColumn = column + 1;
            result[column] = ReadCellValue(cell, sharedStrings);
        }
        return result;
    }

    private static string ReadCellValue(XElement cell, IReadOnlyList<string> sharedStrings)
    {
        var type = (string?)cell.Attribute("t");
        if (string.Equals(type, "inlineStr", StringComparison.OrdinalIgnoreCase))
            return string.Concat(cell.Descendants().Where(element => element.Name.LocalName == "t").Select(element => element.Value));

        var raw = cell.Elements().FirstOrDefault(element => element.Name.LocalName == "v")?.Value ?? string.Empty;
        if (string.Equals(type, "s", StringComparison.OrdinalIgnoreCase))
        {
            if (!int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var index) || index < 0 || index >= sharedStrings.Count)
                throw new InvalidDataException($"TIA XLSX shared-string index '{raw}' is invalid.");
            return sharedStrings[index];
        }

        return raw;
    }

    private static int GetColumnIndex(string cellReference)
    {
        var column = 0;
        var found = false;
        foreach (var character in cellReference)
        {
            if (!char.IsLetter(character)) break;
            found = true;
            column = checked(column * 26 + (char.ToUpperInvariant(character) - 'A' + 1));
        }
        if (!found) throw new InvalidDataException($"TIA XLSX cell reference '{cellReference}' is invalid.");
        return column - 1;
    }

    private static void ValidateHeaders(IReadOnlyDictionary<int, string> headers)
    {
        var names = headers.Values.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var required in new[] { "Name", "Data Type", "Logical Address" })
        {
            if (!names.Contains(required))
                throw new InvalidDataException($"TIA XLSX PLCTags sheet is missing required column '{required}'.");
        }
    }

    private static ZipArchiveEntry GetRequiredEntry(ZipArchive archive, string path) =>
        archive.GetEntry(path) ?? throw new InvalidDataException($"TIA XLSX archive entry '{path}' is missing.");

    private static string? Get(IReadOnlyDictionary<string, string> values, string key)
    {
        if (!values.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.Equals("<no value>", StringComparison.OrdinalIgnoreCase) ? null : trimmed;
    }

    private static bool? ParseOptionalBoolean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (bool.TryParse(value, out var parsed)) return parsed;
        if (value == "1") return true;
        if (value == "0") return false;
        return null;
    }

    private static string FormatOptionalBoolean(bool? value) => value.HasValue ? (value.Value ? "true" : "false") : string.Empty;

    private static DriverEngineeringIssue Issue(
        string code,
        DriverEngineeringIssueSeverity severity,
        string message) =>
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
