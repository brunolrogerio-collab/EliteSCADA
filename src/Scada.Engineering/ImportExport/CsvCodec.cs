using System.Globalization;
using System.Text;

namespace Scada.Engineering.ImportExport;

internal static class CsvCodec
{
    private const int MaximumRows = 100_000;
    private const int MaximumColumns = 128;
    private const int MaximumCellCharacters = 64 * 1024;
    private const int MaximumOutputBytes = 16 * 1024 * 1024;

    public static string Write(IEnumerable<IReadOnlyList<string?>> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var sb = new StringBuilder();
        var outputBytes = 0;
        var rowCount = 0;

        void AppendBounded(string value)
        {
            var valueBytes = Encoding.UTF8.GetByteCount(value);
            if (valueBytes > MaximumOutputBytes - outputBytes)
                throw new InvalidDataException("CSV output exceeds the supported 16 MiB size.");
            sb.Append(value);
            outputBytes += valueBytes;
        }

        foreach (var row in rows)
        {
            if (++rowCount > MaximumRows + 1)
                throw new InvalidDataException("CSV output exceeds the supported row limit.");
            if (row is null || row.Count > MaximumColumns)
                throw new InvalidDataException("CSV output exceeds the supported column limit.");

            for (var index = 0; index < row.Count; index++)
            {
                var value = row[index] ?? string.Empty;
                if (value.Length > MaximumCellCharacters)
                    throw new InvalidDataException("CSV output contains a cell that exceeds the supported size.");
                if (index > 0) AppendBounded(",");
                AppendBounded(Escape(value));
            }
            AppendBounded("\n");
        }
        return sb.ToString();
    }

    public static IReadOnlyList<string[]> Read(string csv)
    {
        var table = ReadTable(csv);
        return table is null
            ? Array.Empty<string[]>()
            : new[] { table.Headers.ToArray() }.Concat(table.Rows.Select(row => row.Cells.ToArray())).ToArray();
    }

    public static CsvTable? ReadTable(string csv)
    {
        var records = ParseRecords(csv ?? string.Empty);
        if (records.Count == 0) return null;

        var headers = records[0].Cells.Select(header => header.Trim()).ToArray();
        if (headers.Length == 0 || headers.Length > MaximumColumns || headers.Any(string.IsNullOrWhiteSpace))
            throw new InvalidDataException("CSV header is empty or exceeds the supported column bounds.");
        if (headers.Distinct(StringComparer.OrdinalIgnoreCase).Count() != headers.Length)
            throw new InvalidDataException("CSV contains duplicate column keys.");

        var rows = new List<CsvTableRow>(records.Count - 1);
        foreach (var record in records.Skip(1))
        {
            if (record.Cells.Count > headers.Length)
                throw new InvalidDataException($"CSV row {record.LineNumber} contains more cells than the header.");
            if (record.Cells.All(string.IsNullOrWhiteSpace)) continue;

            var cells = new string[headers.Length];
            for (var index = 0; index < cells.Length; index++)
                cells[index] = index < record.Cells.Count ? UnprotectFormula(record.Cells[index]) : string.Empty;
            rows.Add(new CsvTableRow(record.LineNumber, cells));
        }

        return new CsvTable(headers, rows);
    }

    public static string Number(double? value) => value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

    private static string Escape(string? value)
    {
        value = ProtectFormula(value ?? string.Empty);
        if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\n') && !value.Contains('\r')) return value;
        return '"' + value.Replace("\"", "\"\"") + '"';
    }

    private static string ProtectFormula(string value)
    {
        var first = value.AsSpan().TrimStart([' ', '\t', '\r', '\n']);
        if (first.IsEmpty || first[0] is not ('=' or '+' or '-' or '@'))
            return value;

        // A plain negative numeric literal is not a formula. Other formula-leading
        // text is prefixed for spreadsheet safety.
        if (value.Length > 1 && value[0] == '-' && IsInvariantNegativeNumber(value))
            return value;
        return "'" + value;
    }

    private static bool IsInvariantNegativeNumber(string value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) &&
        number < 0 &&
        !value.Contains('=') && !value.Contains('+') && !value.Contains('@');

    private static string UnprotectFormula(string value)
    {
        if (!value.StartsWith("'", StringComparison.Ordinal)) return value;
        var afterOne = value.AsSpan(1).TrimStart([' ', '\t', '\r', '\n']);
        if (afterOne.IsEmpty || afterOne[0] is not ('=' or '+' or '-' or '@')) return value;
        return value[1..];
    }

    private static List<CsvRecord> ParseRecords(string csv)
    {
        if (csv.Length > 16 * 1024 * 1024)
            throw new InvalidDataException("CSV exceeds the supported import size.");
        if (csv.Length > 0 && csv[0] == '\uFEFF') csv = csv[1..];

        var records = new List<CsvRecord>();
        var cells = new List<string>();
        var cell = new StringBuilder();
        var inQuotes = false;
        var afterQuote = false;
        var line = 1;
        var recordLine = 1;

        void EndCell()
        {
            if (cells.Count >= MaximumColumns)
                throw new InvalidDataException($"CSV row {recordLine} exceeds the supported column limit.");
            if (cell.Length > MaximumCellCharacters)
                throw new InvalidDataException($"CSV cell in row {recordLine} exceeds the supported size.");
            cells.Add(cell.ToString());
            cell.Clear();
            afterQuote = false;
        }

        void EndRecord()
        {
            EndCell();
            if (cells.Any(value => !string.IsNullOrWhiteSpace(value)))
            {
                if (records.Count >= MaximumRows + 1)
                    throw new InvalidDataException("CSV exceeds the supported row limit.");
                records.Add(new CsvRecord(recordLine, cells.ToArray()));
            }
            cells.Clear();
            recordLine = line + 1;
        }

        for (var index = 0; index < csv.Length; index++)
        {
            var current = csv[index];
            if (inQuotes)
            {
                if (current == '"')
                {
                    if (index + 1 < csv.Length && csv[index + 1] == '"')
                    {
                        cell.Append('"');
                        index++;
                    }
                    else
                    {
                        inQuotes = false;
                        afterQuote = true;
                    }
                }
                else if (current == '\r' || current == '\n')
                {
                    if (current == '\r' && index + 1 < csv.Length && csv[index + 1] == '\n') index++;
                    cell.Append('\n');
                    line++;
                }
                else
                {
                    cell.Append(current);
                    if (cell.Length > MaximumCellCharacters)
                        throw new InvalidDataException($"CSV cell in row {recordLine} exceeds the supported size.");
                }
                continue;
            }

            if (afterQuote)
            {
                if (current == ',') { EndCell(); continue; }
                if (current is '\r' or '\n')
                {
                    if (current == '\r' && index + 1 < csv.Length && csv[index + 1] == '\n') index++;
                    EndRecord();
                    line++;
                    recordLine = line;
                    continue;
                }
                if (char.IsWhiteSpace(current)) continue;
                throw new InvalidDataException($"CSV row {recordLine} contains text after a closing quote.");
            }

            if (current == '"')
            {
                if (cell.Length != 0)
                    throw new InvalidDataException($"CSV row {recordLine} contains a quote inside an unquoted cell.");
                inQuotes = true;
            }
            else if (current == ',') EndCell();
            else if (current is '\r' or '\n')
            {
                if (current == '\r' && index + 1 < csv.Length && csv[index + 1] == '\n') index++;
                EndRecord();
                line++;
                recordLine = line;
            }
            else
            {
                cell.Append(current);
                if (cell.Length > MaximumCellCharacters)
                    throw new InvalidDataException($"CSV cell in row {recordLine} exceeds the supported size.");
            }
        }

        if (inQuotes)
            throw new InvalidDataException($"CSV row {recordLine} contains an unterminated quoted cell.");
        if (cells.Count > 0 || cell.Length > 0 || afterQuote)
            EndRecord();
        return records;
    }

    private sealed record CsvRecord(int LineNumber, IReadOnlyList<string> Cells);
}

internal sealed record CsvTable(string[] Headers, IReadOnlyCollection<CsvTableRow> Rows);
internal sealed record CsvTableRow(int LineNumber, string[] Cells);
