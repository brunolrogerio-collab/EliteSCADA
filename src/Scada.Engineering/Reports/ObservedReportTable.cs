using System.Globalization;
using Scada.Core.HistoricalQueries;

namespace Scada.Engineering.Reports;

public static class ObservedReportTable
{
    public static string Label(ReportVariableEngineeringDto variable) =>
        string.IsNullOrWhiteSpace(variable.DisplayLabel) ? variable.Name : variable.DisplayLabel;

    public static string ExcelFormat(ReportVariableEngineeringDto variable)
    {
        var places = variable.DecimalPlaces;
        var format = places is null ? "0.###############" : places == 0 ? "0" : "0." + new string('0', places.Value);
        var unit = ReportV3ExecutionService.DisplayUnit(variable);
        return string.IsNullOrEmpty(unit) ? format : format + "\\ " + string.Concat(unit.Select(ch => "\\" + ch));
    }

    internal static ReportQueryExecutionResult Format(ReportEngineeringDto report, ReportQueryExecutionResult wide,
        IReadOnlyList<ReportVariableEngineeringDto> variables)
    {
        var visible = variables.Where(v => v.Visible).ToArray();
        var culture = CultureInfo.GetCultureInfo(report.Metadata?.GetValueOrDefault("numberCulture") ?? "");
        var missingQuality = report.Metadata?.GetValueOrDefault("invalidQualityLabel") ?? "Invalid quality";
        var columns = wide.Columns.Take(1).Concat(visible.Select(v => new HistoricalColumn(
            Label(v), HistoricalFieldType.String, [], false, false, false))).ToArray();
        var rows = new List<HistoricalQueryRow>(wide.Rows.Count);
        var numeric = new List<HistoricalQueryRow>(wide.Rows.Count);
        foreach (var row in wide.Rows)
        {
            var cells = new Dictionary<string, HistoricalQueryValue> { ["timestamp"] = row.Cells["timestamp"] };
            var numbers = new Dictionary<string, HistoricalQueryValue>();
            foreach (var variable in visible)
            {
                var field = Label(variable);
                var value = row.Cells[$"v:{variable.TagId:D}"];
                var quality = row.Cells[$"q:{variable.TagId:D}"].Value;
                if (quality is null or "Gap")
                    cells[field] = HistoricalQueryValue.Null();
                else if (quality != "Good" || value.Value is null)
                    cells[field] = HistoricalQueryValue.FromString(missingQuality);
                else if (value.Kind == HistoricalValueKind.Boolean)
                    cells[field] = HistoricalQueryValue.FromString(value.AsBoolean()
                        ? variable.BooleanTrueLabel ?? "True" : variable.BooleanFalseLabel ?? "False");
                else if (value.Kind is HistoricalValueKind.Int16 or HistoricalValueKind.Int32 or HistoricalValueKind.Int64
                    or HistoricalValueKind.Float or HistoricalValueKind.Double or HistoricalValueKind.Number)
                {
                    numbers[field] = value;
                    var format = variable.DecimalPlaces is int places ? "F" + places : "G";
                    var display = value.Kind == HistoricalValueKind.Int64
                        ? value.AsInt64().ToString(format, culture) : value.AsNumber().ToString(format, culture);
                    if (ReportV3ExecutionService.DisplayUnit(variable) is { } unit) display += " " + unit;
                    cells[field] = HistoricalQueryValue.FromString(display);
                }
                else cells[field] = value;
            }
            rows.Add(new(cells));
            numeric.Add(new(numbers));
        }
        return wide with { Columns = columns, Rows = rows, NumericRows = numeric, Provenance = null };
    }
}
