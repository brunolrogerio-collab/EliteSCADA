using System.Globalization;
using Scada.Core.HistoricalQueries;

namespace Scada.Engineering.Reports;

public sealed record ReportEngineeringProblem(string Code, string Message);

public static class ReportEngineeringValidation
{
    public const int MaximumQueries = 8;
    public const int MaximumParameters = 32;
    public const int MaximumSections = 64;
    public const int MaximumControls = 512;
    public const int MaximumGroups = 16;
    public const int MaximumAggregates = 64;

    public static IReadOnlyList<ReportEngineeringProblem> Validate(ReportEngineeringDto report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var problems = new List<ReportEngineeringProblem>();

        Required(report.Key, "REPORT_KEY_REQUIRED", "Report key is required.", problems);
        Required(report.Name, "REPORT_NAME_REQUIRED", "Report name is required.", problems);
        if (report.Id == Guid.Empty)
            problems.Add(new("REPORT_ID_EMPTY", "Report ID cannot be empty."));

        ValidatePage(report.Page ?? new ReportPageEngineeringDto(), problems);

        var parameters = report.Parameters ?? Array.Empty<ReportParameterEngineeringDto>();
        if (parameters.Count > MaximumParameters)
            problems.Add(new("REPORT_PARAMETER_LIMIT", $"Report cannot contain more than {MaximumParameters} parameters."));
        AddDuplicateProblems(parameters.Where(x => x is not null).Select(x => x.Key), "REPORT_PARAMETER_DUPLICATE", "parameter", problems);
        foreach (var parameter in parameters)
            ValidateParameter(parameter, problems);

        var queries = report.Queries ?? Array.Empty<ReportQueryEngineeringDto>();
        if (queries.Count is < 1 or > MaximumQueries)
            problems.Add(new("REPORT_QUERY_LIMIT", $"Report must contain between 1 and {MaximumQueries} queries."));
        AddDuplicateProblems(queries.Where(x => x is not null).Select(x => x.Key), "REPORT_QUERY_DUPLICATE", "query", problems);

        var parameterMap = parameters
            .Where(x => x is not null && !string.IsNullOrWhiteSpace(x.Key))
            .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
        var queryMap = queries
            .Where(x => x is not null && !string.IsNullOrWhiteSpace(x.Key))
            .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var query in queries)
            ValidateQuery(query, parameterMap, problems);

        var groups = report.Groups ?? Array.Empty<ReportGroupEngineeringDto>();
        if (groups.Count > MaximumGroups)
            problems.Add(new("REPORT_GROUP_LIMIT", $"Report cannot contain more than {MaximumGroups} groups."));
        AddDuplicateProblems(groups.Where(x => x is not null).Select(x => x.Key), "REPORT_GROUP_DUPLICATE", "group", problems);
        var groupMap = groups
            .Where(x => x is not null && !string.IsNullOrWhiteSpace(x.Key))
            .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
        foreach (var group in groups)
            ValidateGroup(group, queryMap, problems);

        var sections = report.Sections ?? Array.Empty<ReportSectionEngineeringDto>();
        if (sections.Count is < 1 or > MaximumSections)
            problems.Add(new("REPORT_SECTION_LIMIT", $"Report must contain between 1 and {MaximumSections} sections."));
        AddDuplicateProblems(sections.Where(x => x is not null).Select(x => x.Key), "REPORT_SECTION_DUPLICATE", "section", problems);
        var controlCount = sections.Where(x => x is not null).Sum(x => x.Controls?.Count ?? 0);
        if (controlCount > MaximumControls)
            problems.Add(new("REPORT_CONTROL_LIMIT", $"Report cannot contain more than {MaximumControls} controls."));
        foreach (var section in sections)
            ValidateSection(section, queryMap, groupMap, problems);

        var aggregates = report.Aggregates ?? Array.Empty<ReportAggregateEngineeringDto>();
        if (aggregates.Count > MaximumAggregates)
            problems.Add(new("REPORT_AGGREGATE_LIMIT", $"Report cannot contain more than {MaximumAggregates} aggregates."));
        AddDuplicateProblems(aggregates.Where(x => x is not null).Select(x => x.Key), "REPORT_AGGREGATE_DUPLICATE", "aggregate", problems);
        foreach (var aggregate in aggregates)
            ValidateAggregate(aggregate, queryMap, groupMap, problems);

        return problems;
    }

    public static bool TryNormalizeParameterValue(
        ReportParameterValue value,
        ReportParameterType expectedType,
        out ReportParameterValue normalized,
        out string? error)
    {
        normalized = value;
        error = null;
        if (value is null)
        {
            error = "Parameter value is required.";
            return false;
        }
        if (value.Type != expectedType)
        {
            error = $"Parameter value declares {value.Type} but {expectedType} is required.";
            return false;
        }
        if (value.Value is null || value.Value.Length > 2000)
        {
            error = "Parameter value is missing or exceeds 2000 characters.";
            return false;
        }

        try
        {
            normalized = expectedType switch
            {
                ReportParameterType.String => ReportParameterValue.FromString(value.Value),
                ReportParameterType.Boolean => ReportParameterValue.FromBoolean(bool.Parse(value.Value)),
                ReportParameterType.Number => NormalizeNumber(value.Value),
                ReportParameterType.Int64 => ReportParameterValue.FromInt64(long.Parse(value.Value, NumberStyles.Integer, CultureInfo.InvariantCulture)),
                ReportParameterType.DateTime => NormalizeDateTime(value.Value),
                ReportParameterType.DurationSeconds => ReportParameterValue.FromDurationSeconds(int.Parse(value.Value, NumberStyles.Integer, CultureInfo.InvariantCulture)),
                ReportParameterType.Guid => ReportParameterValue.FromGuid(Guid.Parse(value.Value)),
                ReportParameterType.Enum when !string.IsNullOrWhiteSpace(value.Value) => ReportParameterValue.FromEnum(value.Value.Trim()),
                _ => throw new FormatException("Report parameter value is invalid.")
            };
            return true;
        }
        catch (Exception ex) when (ex is FormatException or OverflowException or ArgumentOutOfRangeException)
        {
            error = ex.Message;
            return false;
        }
    }

    private static void ValidatePage(ReportPageEngineeringDto page, List<ReportEngineeringProblem> problems)
    {
        Required(page.PaperSizeKey, "REPORT_PAGE_SIZE_REQUIRED", "Report paper size key is required.", problems);
        ValidateFiniteRange(page.MarginTopMillimeters, 0, 100, "REPORT_MARGIN_TOP_INVALID", "Top margin", problems);
        ValidateFiniteRange(page.MarginRightMillimeters, 0, 100, "REPORT_MARGIN_RIGHT_INVALID", "Right margin", problems);
        ValidateFiniteRange(page.MarginBottomMillimeters, 0, 100, "REPORT_MARGIN_BOTTOM_INVALID", "Bottom margin", problems);
        ValidateFiniteRange(page.MarginLeftMillimeters, 0, 100, "REPORT_MARGIN_LEFT_INVALID", "Left margin", problems);
    }

    private static void ValidateParameter(ReportParameterEngineeringDto? parameter, List<ReportEngineeringProblem> problems)
    {
        if (parameter is null)
        {
            problems.Add(new("REPORT_PARAMETER_NULL", "Report parameter cannot be null."));
            return;
        }
        Required(parameter.Key, "REPORT_PARAMETER_KEY_REQUIRED", "Report parameter key is required.", problems);
        Required(parameter.Name, "REPORT_PARAMETER_NAME_REQUIRED", $"Report parameter '{parameter.Key}' requires a name.", problems);
        if (!TryNormalizeParameterValue(parameter.DefaultValue, parameter.Type, out var normalizedDefault, out var defaultError))
            problems.Add(new("REPORT_PARAMETER_DEFAULT_INVALID", $"Report parameter '{parameter.Key}' has an invalid default: {defaultError}"));

        var allowed = parameter.AllowedValues ?? Array.Empty<ReportParameterValue>();
        foreach (var value in allowed)
        {
            if (!TryNormalizeParameterValue(value, parameter.Type, out _, out var allowedError))
                problems.Add(new("REPORT_PARAMETER_ALLOWED_INVALID", $"Report parameter '{parameter.Key}' has an invalid allowed value: {allowedError}"));
        }
        if (allowed.Count > 0 && TryNormalizeParameterValue(parameter.DefaultValue, parameter.Type, out normalizedDefault, out _) &&
            !allowed.Any(value => TryNormalizeParameterValue(value, parameter.Type, out var normalized, out _) && normalized == normalizedDefault))
            problems.Add(new("REPORT_PARAMETER_DEFAULT_NOT_ALLOWED", $"Report parameter '{parameter.Key}' default must be one of its allowed values."));
    }

    private static void ValidateQuery(
        ReportQueryEngineeringDto? reportQuery,
        IReadOnlyDictionary<string, ReportParameterEngineeringDto> parameters,
        List<ReportEngineeringProblem> problems)
    {
        if (reportQuery is null)
        {
            problems.Add(new("REPORT_QUERY_NULL", "Report query cannot be null."));
            return;
        }
        Required(reportQuery.Key, "REPORT_QUERY_KEY_REQUIRED", "Report query key is required.", problems);
        if (reportQuery.Query is null)
        {
            problems.Add(new("REPORT_QUERY_REQUIRED", $"Report query '{reportQuery.Key}' requires a Historical Query v1 descriptor."));
            return;
        }
        if (!string.IsNullOrWhiteSpace(reportQuery.Query.Page?.Cursor))
            problems.Add(new("REPORT_QUERY_CURSOR_PERSISTED", $"Report query '{reportQuery.Key}' must not persist a Historical Query cursor."));

        try
        {
            _ = HistoricalQueryValidator.Validate(reportQuery.Query);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            problems.Add(new("REPORT_QUERY_INVALID", $"Report query '{reportQuery.Key}' is invalid: {ex.Message}"));
        }

        var bindings = reportQuery.ParameterBindings ?? Array.Empty<ReportQueryParameterBindingEngineeringDto>();
        var bindingSlots = new HashSet<string>(StringComparer.Ordinal);
        foreach (var binding in bindings)
        {
            if (binding is null)
            {
                problems.Add(new("REPORT_QUERY_BINDING_NULL", $"Report query '{reportQuery.Key}' contains a null parameter binding."));
                continue;
            }
            if (!parameters.TryGetValue(binding.ParameterKey ?? string.Empty, out var parameter))
            {
                problems.Add(new("REPORT_QUERY_BINDING_PARAMETER_NOT_FOUND", $"Report query '{reportQuery.Key}' binding references missing parameter '{binding.ParameterKey}'."));
                continue;
            }

            var slot = $"{binding.Target}:{binding.FilterIndex}:{binding.ValueIndex}";
            if (!bindingSlots.Add(slot))
                problems.Add(new("REPORT_QUERY_BINDING_DUPLICATE", $"Report query '{reportQuery.Key}' binds more than one parameter to '{slot}'."));

            ValidateBinding(reportQuery, binding, parameter, problems);
        }
    }

    private static void ValidateBinding(
        ReportQueryEngineeringDto reportQuery,
        ReportQueryParameterBindingEngineeringDto binding,
        ReportParameterEngineeringDto parameter,
        List<ReportEngineeringProblem> problems)
    {
        var query = reportQuery.Query;
        switch (binding.Target)
        {
            case ReportQueryParameterTarget.AbsoluteFromUtc:
            case ReportQueryParameterTarget.AbsoluteToUtc:
                if (parameter.Type != ReportParameterType.DateTime || query.Range.Kind != HistoricalTimeRangeKind.Absolute)
                    problems.Add(new("REPORT_QUERY_BINDING_TYPE_MISMATCH", $"Report query '{reportQuery.Key}' absolute time binding requires a DateTime parameter and absolute Historical Query range."));
                break;
            case ReportQueryParameterTarget.RelativeDurationSeconds:
                if (parameter.Type != ReportParameterType.DurationSeconds || query.Range.Kind != HistoricalTimeRangeKind.Relative)
                    problems.Add(new("REPORT_QUERY_BINDING_TYPE_MISMATCH", $"Report query '{reportQuery.Key}' relative duration binding requires a DurationSeconds parameter and relative Historical Query range."));
                break;
            case ReportQueryParameterTarget.Search:
                if (parameter.Type != ReportParameterType.String)
                    problems.Add(new("REPORT_QUERY_BINDING_TYPE_MISMATCH", $"Report query '{reportQuery.Key}' search binding requires a String parameter."));
                break;
            case ReportQueryParameterTarget.FilterValue:
                ValidateFilterBinding(reportQuery, binding, parameter, problems);
                break;
            default:
                problems.Add(new("REPORT_QUERY_BINDING_TARGET_INVALID", $"Report query '{reportQuery.Key}' contains an unsupported parameter binding target."));
                break;
        }
    }

    private static void ValidateFilterBinding(
        ReportQueryEngineeringDto reportQuery,
        ReportQueryParameterBindingEngineeringDto binding,
        ReportParameterEngineeringDto parameter,
        List<ReportEngineeringProblem> problems)
    {
        var filters = reportQuery.Query.Filters ?? Array.Empty<HistoricalFilter>();
        if (!binding.FilterIndex.HasValue || binding.FilterIndex < 0 || binding.FilterIndex >= filters.Count)
        {
            problems.Add(new("REPORT_QUERY_BINDING_FILTER_INDEX_INVALID", $"Report query '{reportQuery.Key}' filter binding has an invalid FilterIndex."));
            return;
        }
        var values = filters[binding.FilterIndex.Value].Values;
        if (!binding.ValueIndex.HasValue || binding.ValueIndex < 0 || binding.ValueIndex >= values.Count)
        {
            problems.Add(new("REPORT_QUERY_BINDING_VALUE_INDEX_INVALID", $"Report query '{reportQuery.Key}' filter binding has an invalid ValueIndex."));
            return;
        }
        if (!CanBind(parameter.Type, values[binding.ValueIndex.Value].Kind))
            problems.Add(new("REPORT_QUERY_BINDING_TYPE_MISMATCH", $"Report parameter '{parameter.Key}' type {parameter.Type} cannot bind Historical Query value kind {values[binding.ValueIndex.Value].Kind}."));
    }

    private static void ValidateGroup(
        ReportGroupEngineeringDto? group,
        IReadOnlyDictionary<string, ReportQueryEngineeringDto> queries,
        List<ReportEngineeringProblem> problems)
    {
        if (group is null)
        {
            problems.Add(new("REPORT_GROUP_NULL", "Report group cannot be null."));
            return;
        }
        Required(group.Key, "REPORT_GROUP_KEY_REQUIRED", "Report group key is required.", problems);
        if (!queries.TryGetValue(group.QueryKey ?? string.Empty, out var query))
        {
            problems.Add(new("REPORT_GROUP_QUERY_NOT_FOUND", $"Report group '{group.Key}' references missing query '{group.QueryKey}'."));
            return;
        }
        if (!FieldExists(query.Query.Dataset, group.Field))
            problems.Add(new("REPORT_GROUP_FIELD_NOT_FOUND", $"Report group '{group.Key}' references field '{group.Field}' not exposed by dataset '{query.Query.Dataset}'."));
    }

    private static void ValidateSection(
        ReportSectionEngineeringDto? section,
        IReadOnlyDictionary<string, ReportQueryEngineeringDto> queries,
        IReadOnlyDictionary<string, ReportGroupEngineeringDto> groups,
        List<ReportEngineeringProblem> problems)
    {
        if (section is null)
        {
            problems.Add(new("REPORT_SECTION_NULL", "Report section cannot be null."));
            return;
        }
        Required(section.Key, "REPORT_SECTION_KEY_REQUIRED", "Report section key is required.", problems);
        if (section.Id == Guid.Empty)
            problems.Add(new("REPORT_SECTION_ID_EMPTY", $"Report section '{section.Key}' ID cannot be empty."));
        ValidateFiniteRange(section.HeightMillimeters, 0.1, 1000, "REPORT_SECTION_HEIGHT_INVALID", $"Section '{section.Key}' height", problems);
        if (!string.IsNullOrWhiteSpace(section.QueryKey) && !queries.ContainsKey(section.QueryKey))
            problems.Add(new("REPORT_SECTION_QUERY_NOT_FOUND", $"Report section '{section.Key}' references missing query '{section.QueryKey}'."));
        if (!string.IsNullOrWhiteSpace(section.GroupKey) && !groups.ContainsKey(section.GroupKey))
            problems.Add(new("REPORT_SECTION_GROUP_NOT_FOUND", $"Report section '{section.Key}' references missing group '{section.GroupKey}'."));
        if (section.Kind is ReportSectionKind.GroupHeader or ReportSectionKind.GroupFooter && string.IsNullOrWhiteSpace(section.GroupKey))
            problems.Add(new("REPORT_SECTION_GROUP_REQUIRED", $"Report section '{section.Key}' is a group section and requires GroupKey."));

        var controls = section.Controls ?? Array.Empty<ReportControlEngineeringDto>();
        AddDuplicateProblems(controls.Where(x => x is not null).Select(x => x.Key), "REPORT_CONTROL_DUPLICATE", $"control in section '{section.Key}'", problems);
        foreach (var control in controls)
            ValidateControl(section, control, queries, problems);
    }

    private static void ValidateControl(
        ReportSectionEngineeringDto section,
        ReportControlEngineeringDto? control,
        IReadOnlyDictionary<string, ReportQueryEngineeringDto> queries,
        List<ReportEngineeringProblem> problems)
    {
        if (control is null)
        {
            problems.Add(new("REPORT_CONTROL_NULL", $"Report section '{section.Key}' contains a null control."));
            return;
        }
        Required(control.Key, "REPORT_CONTROL_KEY_REQUIRED", $"Report section '{section.Key}' contains a control without a key.", problems);
        if (control.Id == Guid.Empty)
            problems.Add(new("REPORT_CONTROL_ID_EMPTY", $"Report control '{control.Key}' ID cannot be empty."));
        ValidateFiniteRange(control.XMillimeters, 0, 2000, "REPORT_CONTROL_X_INVALID", $"Control '{control.Key}' X", problems);
        ValidateFiniteRange(control.YMillimeters, 0, 2000, "REPORT_CONTROL_Y_INVALID", $"Control '{control.Key}' Y", problems);
        if (control.Kind != ReportControlKind.PageBreak)
        {
            ValidateFiniteRange(control.WidthMillimeters, 0.1, 2000, "REPORT_CONTROL_WIDTH_INVALID", $"Control '{control.Key}' width", problems);
            ValidateFiniteRange(control.HeightMillimeters, 0.1, 2000, "REPORT_CONTROL_HEIGHT_INVALID", $"Control '{control.Key}' height", problems);
        }

        if (control.Kind is ReportControlKind.DataField or ReportControlKind.BooleanState or ReportControlKind.Chart)
        {
            if (string.IsNullOrWhiteSpace(control.QueryKey) || !queries.TryGetValue(control.QueryKey, out var query))
            {
                problems.Add(new("REPORT_CONTROL_QUERY_NOT_FOUND", $"Report control '{control.Key}' requires a valid QueryKey."));
                return;
            }
            if (control.Kind is ReportControlKind.DataField or ReportControlKind.BooleanState)
            {
                if (string.IsNullOrWhiteSpace(control.Field) || !FieldExists(query.Query.Dataset, control.Field))
                    problems.Add(new("REPORT_CONTROL_FIELD_NOT_FOUND", $"Report control '{control.Key}' references field '{control.Field}' not exposed by dataset '{query.Query.Dataset}'."));
            }
        }

        if (control.Kind == ReportControlKind.Image && !control.AssetId.HasValue)
            problems.Add(new("REPORT_CONTROL_ASSET_REQUIRED", $"Report image control '{control.Key}' requires AssetId."));
    }

    private static void ValidateAggregate(
        ReportAggregateEngineeringDto? aggregate,
        IReadOnlyDictionary<string, ReportQueryEngineeringDto> queries,
        IReadOnlyDictionary<string, ReportGroupEngineeringDto> groups,
        List<ReportEngineeringProblem> problems)
    {
        if (aggregate is null)
        {
            problems.Add(new("REPORT_AGGREGATE_NULL", "Report aggregate cannot be null."));
            return;
        }
        Required(aggregate.Key, "REPORT_AGGREGATE_KEY_REQUIRED", "Report aggregate key is required.", problems);
        if (!queries.TryGetValue(aggregate.QueryKey ?? string.Empty, out var query))
        {
            problems.Add(new("REPORT_AGGREGATE_QUERY_NOT_FOUND", $"Report aggregate '{aggregate.Key}' references missing query '{aggregate.QueryKey}'."));
            return;
        }
        if (aggregate.Function != ReportAggregateFunction.Count &&
            (string.IsNullOrWhiteSpace(aggregate.Field) || !FieldExists(query.Query.Dataset, aggregate.Field)))
            problems.Add(new("REPORT_AGGREGATE_FIELD_NOT_FOUND", $"Report aggregate '{aggregate.Key}' requires a valid dataset field."));
        if (!string.IsNullOrWhiteSpace(aggregate.GroupKey) && !groups.ContainsKey(aggregate.GroupKey))
            problems.Add(new("REPORT_AGGREGATE_GROUP_NOT_FOUND", $"Report aggregate '{aggregate.Key}' references missing group '{aggregate.GroupKey}'."));
    }

    private static bool FieldExists(string dataset, string? field)
    {
        if (string.IsNullOrWhiteSpace(field)) return false;
        try { return HistoricalQueryCatalog.Require(dataset).Fields.ContainsKey(field); }
        catch (ArgumentException) { return false; }
    }

    private static bool CanBind(ReportParameterType type, HistoricalValueKind kind) => kind switch
    {
        HistoricalValueKind.Guid => type == ReportParameterType.Guid,
        HistoricalValueKind.String => type == ReportParameterType.String,
        HistoricalValueKind.Enum => type == ReportParameterType.Enum,
        HistoricalValueKind.Boolean => type == ReportParameterType.Boolean,
        HistoricalValueKind.DateTime => type == ReportParameterType.DateTime,
        HistoricalValueKind.Int64 => type == ReportParameterType.Int64,
        HistoricalValueKind.Int16 or HistoricalValueKind.Int32 or HistoricalValueKind.Float or HistoricalValueKind.Double or HistoricalValueKind.Number =>
            type == ReportParameterType.Number,
        _ => false
    };

    private static ReportParameterValue NormalizeNumber(string value)
    {
        var parsed = double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
        if (!double.IsFinite(parsed)) throw new FormatException("Report number must be finite.");
        return ReportParameterValue.FromNumber(parsed);
    }

    private static ReportParameterValue NormalizeDateTime(string value)
    {
        var parsed = DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        if (parsed.Offset != TimeSpan.Zero) throw new FormatException("Report DateTime must use UTC offset +00:00.");
        return ReportParameterValue.FromDateTime(parsed);
    }

    private static void Required(string? value, string code, string message, List<ReportEngineeringProblem> problems)
    {
        if (string.IsNullOrWhiteSpace(value)) problems.Add(new(code, message));
    }

    private static void AddDuplicateProblems(
        IEnumerable<string> keys,
        string code,
        string label,
        List<ReportEngineeringProblem> problems)
    {
        foreach (var duplicate in keys
                     .Where(x => !string.IsNullOrWhiteSpace(x))
                     .GroupBy(x => x, StringComparer.OrdinalIgnoreCase)
                     .Where(x => x.Count() > 1)
                     .Select(x => x.Key))
            problems.Add(new(code, $"Report {label} key '{duplicate}' appears more than once."));
    }

    private static void ValidateFiniteRange(
        double value,
        double minimum,
        double maximum,
        string code,
        string label,
        List<ReportEngineeringProblem> problems)
    {
        if (!double.IsFinite(value) || value < minimum || value > maximum)
            problems.Add(new(code, $"{label} must be finite and between {minimum} and {maximum}."));
    }
}
