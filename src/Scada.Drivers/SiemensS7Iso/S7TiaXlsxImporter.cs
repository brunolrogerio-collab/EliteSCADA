using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using Scada.Drivers.Abstractions;

namespace Scada.Drivers.SiemensS7Iso;

internal static class S7TiaXlsxImporter
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

            var name = Get(values, "Name");
            if (string.IsNullOrWhiteSpace(name)) continue;

            var invalidBooleanFields = new List<string>();
            var record = new S7TiaImportRecord(
                "TiaXlsx",
                sourceName,
                name,
                Get(values, "Path"),
                Get(values, "Data Type"),
                Get(values, "Logical Address"),
                Get(values, "Comment"),
                S7TiaImportCandidateFactory.ParseOptionalBoolean(Get(values, "Hmi Visible"), "hmiVisible", invalidBooleanFields),
                S7TiaImportCandidateFactory.ParseOptionalBoolean(Get(values, "Hmi Accessible"), "hmiAccessible", invalidBooleanFields),
                S7TiaImportCandidateFactory.ParseOptionalBoolean(Get(values, "Hmi Writeable"), "hmiWriteable", invalidBooleanFields),
                InvalidBooleanFields: invalidBooleanFields);

            var candidate = S7TiaImportCandidateFactory.Create(record);
            if (candidate.PortableAddress.StartsWith("tia-export:unsupported:", StringComparison.Ordinal))
                candidate = candidate with { PortableAddress = $"tia-xlsx:unsupported:{candidate.CandidateId}" };
            result.Add(S7TiaImportValidation.ValidateAddressWidth(candidate));
        }

        if (headers is null)
            throw new InvalidDataException("TIA XLSX PLCTags sheet does not contain a header row.");

        return result;
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
}
