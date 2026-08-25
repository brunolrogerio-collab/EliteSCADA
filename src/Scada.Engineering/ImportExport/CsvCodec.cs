using System.Globalization;
using System.Text;

namespace Scada.Engineering.ImportExport;

internal static class CsvCodec
{
    public static string Write(IEnumerable<IReadOnlyList<string?>> rows)
    {
        var sb = new StringBuilder();
        foreach (var row in rows)
            sb.AppendLine(string.Join(',', row.Select(Escape)));
        return sb.ToString();
    }

    public static IReadOnlyList<string[]> Read(string csv)
    {
        var rows = new List<string[]>();
        using var reader = new StringReader(csv ?? string.Empty);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            rows.Add(ParseLine(line).ToArray());
        }
        return rows;
    }

    public static string Number(double? value) => value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

    private static string Escape(string? value)
    {
        value ??= string.Empty;
        if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\n') && !value.Contains('\r')) return value;
        return '"' + value.Replace("\"", "\"\"") + '"';
    }

    private static IEnumerable<string> ParseLine(string line)
    {
        var sb = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                if (quoted && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                else quoted = !quoted;
            }
            else if (c == ',' && !quoted)
            {
                yield return sb.ToString();
                sb.Clear();
            }
            else sb.Append(c);
        }
        yield return sb.ToString();
    }
}
