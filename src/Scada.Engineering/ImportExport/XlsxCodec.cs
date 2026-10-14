using System.Globalization;
using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;

namespace Scada.Engineering.ImportExport;

internal static class XlsxCodec
{
    private const int MaximumInputBytes = 16 * 1024 * 1024;
    private const int MaximumOutputBytes = 16 * 1024 * 1024;
    private const long MaximumExpandedBytes = 32 * 1024 * 1024;
    private const int MaximumEntries = 128;
    private const int MaximumRows = 100_001;
    private const int MaximumColumns = 128;
    private const int MaximumCellCharacters = 64 * 1024;
    private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace OfficeRelationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace PackageRelationships = "http://schemas.openxmlformats.org/package/2006/relationships";
    private static readonly XNamespace ContentTypes = "http://schemas.openxmlformats.org/package/2006/content-types";

    public static byte[] Write(IReadOnlyList<string[]> rows, string sheetName)
    {
        ArgumentNullException.ThrowIfNull(rows);
        if (rows.Count > MaximumRows || rows.Any(row => row is null || row.Length > MaximumColumns))
            throw new InvalidDataException("XLSX export exceeds the supported row or column bounds.");
        if (rows.SelectMany(row => row).Any(value => value is not null && value.Length > MaximumCellCharacters))
            throw new InvalidDataException("XLSX export contains a cell that exceeds the supported size.");

        using var output = new MemoryStream();
        using (var boundedOutput = new BoundedWriteStream(output, MaximumOutputBytes,
                   "XLSX output exceeds the supported 16 MiB size.", leaveOpen: true))
        using (var archive = new ZipArchive(boundedOutput, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddXml(archive, "[Content_Types].xml", new XDocument(
                new XElement(ContentTypes + "Types",
                    new XElement(ContentTypes + "Default", new XAttribute("Extension", "rels"), new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
                    new XElement(ContentTypes + "Default", new XAttribute("Extension", "xml"), new XAttribute("ContentType", "application/xml")),
                    new XElement(ContentTypes + "Override", new XAttribute("PartName", "/xl/workbook.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml")),
                    new XElement(ContentTypes + "Override", new XAttribute("PartName", "/xl/worksheets/sheet1.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml")))));
            AddXml(archive, "_rels/.rels", new XDocument(
                new XElement(PackageRelationships + "Relationships",
                    new XElement(PackageRelationships + "Relationship",
                        new XAttribute("Id", "rId1"),
                        new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"),
                        new XAttribute("Target", "xl/workbook.xml")))));
            AddXml(archive, "xl/workbook.xml", new XDocument(
                new XElement(Main + "workbook",
                    new XAttribute(XNamespace.Xmlns + "r", OfficeRelationships),
                    new XElement(Main + "sheets",
                        new XElement(Main + "sheet", new XAttribute("name", sheetName), new XAttribute("sheetId", "1"), new XAttribute(OfficeRelationships + "id", "rId1"))))));
            AddXml(archive, "xl/_rels/workbook.xml.rels", new XDocument(
                new XElement(PackageRelationships + "Relationships",
                    new XElement(PackageRelationships + "Relationship",
                        new XAttribute("Id", "rId1"),
                        new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"),
                        new XAttribute("Target", "worksheets/sheet1.xml")))));

            AddWorksheet(archive, rows);
        }
        return output.ToArray();
    }

    public static IReadOnlyList<string[]> Read(ReadOnlyMemory<byte> bytes, string expectedSheetName)
    {
        try
        {
            return ReadCore(bytes, expectedSheetName);
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception exception) when (exception is XmlException or InvalidOperationException or ArgumentException or OverflowException or IOException)
        {
            throw new InvalidDataException("XLSX workbook structure is invalid or unsupported.", exception);
        }
    }

    private static IReadOnlyList<string[]> ReadCore(ReadOnlyMemory<byte> bytes, string expectedSheetName)
    {
        if (bytes.Length == 0 || bytes.Length > MaximumInputBytes)
            throw new InvalidDataException("XLSX file is empty or exceeds the supported input size.");

        using var stream = new MemoryStream(bytes.ToArray(), writable: false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
        if (archive.Entries.Count == 0 || archive.Entries.Count > MaximumEntries)
            throw new InvalidDataException("XLSX archive has an unsupported number of entries.");
        if (archive.Entries.Sum(entry => entry.Length) > MaximumExpandedBytes)
            throw new InvalidDataException("XLSX expanded contents exceed the supported safety limit.");

        var workbook = LoadXml(RequiredEntry(archive, "xl/workbook.xml"));
        var sheets = workbook.Root?.Element(Main + "sheets")?.Elements(Main + "sheet").ToArray() ?? [];
        if (sheets.Length != 1)
            throw new InvalidDataException("Import requires a single-sheet XLSX workbook; export one Engineering table per workbook.");
        var sheet = sheets[0];
        var sheetName = (string?)sheet.Attribute("name");
        if (!string.Equals(sheetName, expectedSheetName, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"The XLSX worksheet must be named '{expectedSheetName}'.");
        var relationId = (string?)sheet.Attribute(OfficeRelationships + "id");
        if (string.IsNullOrWhiteSpace(relationId))
            throw new InvalidDataException("XLSX worksheet relationship is missing.");

        var relationships = LoadXml(RequiredEntry(archive, "xl/_rels/workbook.xml.rels"));
        var relationship = relationships.Root?.Elements(PackageRelationships + "Relationship")
            .SingleOrDefault(item => string.Equals((string?)item.Attribute("Id"), relationId, StringComparison.Ordinal));
        var target = (string?)relationship?.Attribute("Target");
        if (string.IsNullOrWhiteSpace(target))
            throw new InvalidDataException("XLSX worksheet relationship target is missing.");
        var worksheetPath = ResolveWorksheetPath(target);
        var worksheet = LoadXml(RequiredEntry(archive, worksheetPath));

        var sharedStrings = ReadSharedStrings(archive);
        var data = worksheet.Root?.Element(Main + "sheetData")
            ?? throw new InvalidDataException("XLSX worksheet has no sheet data.");
        var rows = new SortedDictionary<int, string[]>();
        var seenRows = new HashSet<int>();
        var nextRow = 1;
        foreach (var row in data.Elements(Main + "row"))
        {
            var rowIndex = ParseIndex((string?)row.Attribute("r"), nextRow, MaximumRows, "row");
            if (!seenRows.Add(rowIndex))
                throw new InvalidDataException($"XLSX worksheet contains duplicate row index {rowIndex}.");
            nextRow = rowIndex + 1;
            var cells = new string[MaximumColumns];
            var nextColumn = 1;
            var seenColumns = new HashSet<int>();
            foreach (var cell in row.Elements(Main + "c"))
            {
                if (cell.Element(Main + "f") is not null)
                    throw new InvalidDataException($"XLSX row {rowIndex} contains a formula; formulas are not accepted for Engineering import.");
                var reference = (string?)cell.Attribute("r");
                var columnIndex = reference is null ? nextColumn : ParseCellReference(reference, rowIndex);
                nextColumn = columnIndex + 1;
                if (columnIndex > MaximumColumns)
                    throw new InvalidDataException($"XLSX row {rowIndex} exceeds the supported column limit.");
                if (!seenColumns.Add(columnIndex))
                    throw new InvalidDataException($"XLSX row {rowIndex} contains duplicate cell references.");
                cells[columnIndex - 1] = ReadCell(cell, sharedStrings);
            }
            var last = Array.FindLastIndex(cells, value => value is not null);
            if (last >= 0)
            {
                var trimmed = cells.Take(last + 1).Select(value => value ?? string.Empty).ToArray();
                if (trimmed.Any(value => value.Length > MaximumCellCharacters))
                    throw new InvalidDataException($"XLSX row {rowIndex} contains a cell that exceeds the supported size.");
                rows[rowIndex] = trimmed;
            }
        }

        if (rows.Count == 0) return [];
        var width = rows.Values.Max(row => row.Length);
        return rows.Values.Select(row => row.Concat(Enumerable.Repeat(string.Empty, width - row.Length)).ToArray()).ToArray();
    }

    private static string ReadCell(XElement cell, IReadOnlyList<string> sharedStrings)
    {
        var type = (string?)cell.Attribute("t");
        if (type == "inlineStr") return string.Concat(cell.Element(Main + "is")?.Descendants(Main + "t").Select(text => text.Value) ?? []);
        var value = cell.Element(Main + "v")?.Value ?? string.Empty;
        if (type == "s")
        {
            if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var index) || index < 0 || index >= sharedStrings.Count)
                throw new InvalidDataException("XLSX shared-string reference is invalid.");
            return sharedStrings[index];
        }
        if (type == "e") throw new InvalidDataException("XLSX contains an error-valued cell.");
        if (type == "b") return value == "1" ? "true" : value == "0" ? "false" : throw new InvalidDataException("XLSX boolean cell is invalid.");
        return value;
    }

    private static IReadOnlyList<string> ReadSharedStrings(ZipArchive archive)
    {
        var entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry is null) return [];
        var document = LoadXml(entry);
        return document.Root?.Elements(Main + "si")
            .Select(item => string.Concat(item.Descendants(Main + "t").Select(text => text.Value)))
            .ToArray() ?? [];
    }

    private static string ResolveWorksheetPath(string target)
    {
        var normalized = target.Replace('\\', '/').TrimStart('/');
        if (normalized.Split('/').Any(segment => segment is "" or "." or ".."))
            throw new InvalidDataException("XLSX worksheet path is invalid.");
        if (!normalized.StartsWith("xl/", StringComparison.OrdinalIgnoreCase)) normalized = "xl/" + normalized;
        return normalized;
    }

    private static int ParseCellReference(string reference, int expectedRow)
    {
        if (string.IsNullOrEmpty(reference))
            throw new InvalidDataException("XLSX cell reference is invalid.");

        var column = 0;
        var position = 0;
        while (position < reference.Length && reference[position] is >= 'a' and <= 'z' or >= 'A' and <= 'Z')
        {
            column = checked(column * 26 + (char.ToUpperInvariant(reference[position]) - 'A' + 1));
            if (column > MaximumColumns)
                throw new InvalidDataException("XLSX cell reference exceeds the supported column range.");
            position++;
        }

        if (position == 0)
            throw new InvalidDataException("XLSX cell reference is invalid.");

        var rowStart = position;
        while (position < reference.Length && reference[position] is >= '0' and <= '9') position++;
        if (position == rowStart || position != reference.Length ||
            !int.TryParse(reference.AsSpan(rowStart), NumberStyles.None, CultureInfo.InvariantCulture, out var referencedRow) ||
            referencedRow <= 0 || referencedRow > MaximumRows)
            throw new InvalidDataException("XLSX cell reference must contain a complete column and row index.");
        if (referencedRow != expectedRow)
            throw new InvalidDataException($"XLSX cell reference row {referencedRow} does not match worksheet row {expectedRow}.");

        return column;
    }

    private static int ParseIndex(string? value, int fallback, int maximum, string label)
    {
        if (value is null) return fallback;
        if (int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) && parsed > 0 && parsed <= maximum)
            return parsed;
        throw new InvalidDataException($"XLSX {label} index is outside the supported range.");
    }

    private static string ColumnName(int column)
    {
        var result = string.Empty;
        while (column > 0)
        {
            column--;
            result = (char)('A' + column % 26) + result;
            column /= 26;
        }
        return result;
    }

    private static ZipArchiveEntry RequiredEntry(ZipArchive archive, string name) =>
        archive.GetEntry(name) ?? throw new InvalidDataException($"XLSX file is missing required part '{name}'.");

    private static XDocument LoadXml(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = MaximumExpandedBytes,
            MaxCharactersFromEntities = 0
        });
        return XDocument.Load(reader, LoadOptions.None);
    }

    private static void AddXml(ZipArchive archive, string path, XDocument document)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = XmlWriter.Create(stream, new XmlWriterSettings
        {
            Encoding = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            Indent = false,
            CloseOutput = false
        });
        document.Save(writer);
    }

    private static void AddWorksheet(ZipArchive archive, IReadOnlyList<string[]> rows)
    {
        var entry = archive.CreateEntry("xl/worksheets/sheet1.xml", CompressionLevel.Optimal);
        using var entryStream = entry.Open();
        using var boundedEntryStream = new BoundedWriteStream(entryStream, MaximumExpandedBytes,
            "XLSX worksheet output exceeds the supported expanded size.", leaveOpen: true);
        using var writer = XmlWriter.Create(boundedEntryStream, new XmlWriterSettings
        {
            Encoding = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            Indent = false,
            CloseOutput = false
        });

        writer.WriteStartDocument();
        writer.WriteStartElement("worksheet", Main.NamespaceName);
        writer.WriteStartElement("sheetData", Main.NamespaceName);
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            writer.WriteStartElement("row", Main.NamespaceName);
            writer.WriteAttributeString("r", (rowIndex + 1).ToString(CultureInfo.InvariantCulture));
            for (var columnIndex = 0; columnIndex < rows[rowIndex].Length; columnIndex++)
            {
                var value = rows[rowIndex][columnIndex] ?? string.Empty;
                writer.WriteStartElement("c", Main.NamespaceName);
                writer.WriteAttributeString("r", ColumnName(columnIndex + 1) + (rowIndex + 1).ToString(CultureInfo.InvariantCulture));
                writer.WriteAttributeString("t", "inlineStr");
                writer.WriteStartElement("is", Main.NamespaceName);
                writer.WriteStartElement("t", Main.NamespaceName);
                writer.WriteAttributeString("xml", "space", XNamespace.Xml.NamespaceName, "preserve");
                writer.WriteString(value);
                writer.WriteEndElement();
                writer.WriteEndElement();
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    private sealed class BoundedWriteStream(
        Stream inner,
        long maximumBytes,
        string error,
        bool leaveOpen) : Stream
    {
        public override bool CanRead => false;
        public override bool CanSeek => inner.CanSeek;
        public override bool CanWrite => inner.CanWrite;
        public override long Length => inner.Length;
        public override long Position
        {
            get => inner.Position;
            set => inner.Position = value;
        }

        public override void Flush() => inner.Flush();

        public override void Write(byte[] buffer, int offset, int count)
        {
            EnsureWithinLimit(count);
            inner.Write(buffer, offset, count);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            EnsureWithinLimit(buffer.Length);
            inner.Write(buffer);
        }

        public override void WriteByte(byte value)
        {
            EnsureWithinLimit(1);
            inner.WriteByte(value);
        }

        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);

        public override void SetLength(long value)
        {
            if (value > maximumBytes) throw new InvalidDataException(error);
            inner.SetLength(value);
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing && !leaveOpen) inner.Dispose();
            base.Dispose(disposing);
        }

        private void EnsureWithinLimit(int writeLength)
        {
            if (writeLength < 0 || writeLength > maximumBytes - inner.Position)
                throw new InvalidDataException(error);
        }
    }
}
