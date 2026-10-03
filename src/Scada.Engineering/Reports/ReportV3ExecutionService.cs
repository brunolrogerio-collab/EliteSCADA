using System.Globalization;
using Scada.Core.HistoricalQueries;
using Scada.Engineering.Contracts;
using Scada.Engineering.DataQueries;

namespace Scada.Engineering.Reports;

/// <summary>
/// Additive Reporting V3 execution layer. Legacy reports continue through
/// ReportExecutionService; V3 sampling/aggregation delegates to the canonical
/// Data Query/Historian authority and never opens a second historian path.
/// </summary>
public sealed class ReportV3ExecutionService : IReportExecutionService
{
    private readonly ReportExecutionService _legacy;
    private readonly IDataQueryEngineeringRegistry _dataQueryRegistry;
    private readonly ITransientDataQueryExecutionService _dataQueries;
    private readonly ReportExecutionPolicy _policy;

    public ReportV3ExecutionService(
        ReportExecutionService legacy,
        IDataQueryEngineeringRegistry dataQueryRegistry,
        ITransientDataQueryExecutionService dataQueries,
        ReportExecutionPolicy? policy = null)
    {
        _legacy = legacy ?? throw new ArgumentNullException(nameof(legacy));
        _dataQueryRegistry = dataQueryRegistry ?? throw new ArgumentNullException(nameof(dataQueryRegistry));
        _dataQueries = dataQueries ?? throw new ArgumentNullException(nameof(dataQueries));
        _policy = policy ?? new ReportExecutionPolicy();
        _policy.Validate();
    }

    public async Task<ReportExecutionResult> ExecuteAsync(
        ReportExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Report);
        cancellationToken.ThrowIfCancellationRequested();

        ValidateRuntimeRange(request.TimeRange);
        var report = ApplyRuntimeRangeAndVariables(request.Report, request.TimeRange);

        var usesV3Retrieval =
            report.Resolution is { Mode: not ReportDataResolutionMode.Raw } ||
            (report.Queries ?? Array.Empty<ReportQueryEngineeringDto>())
                .Any(q => q.DataQueryId.HasValue || !string.IsNullOrWhiteSpace(q.DataQueryKey));

        ReportExecutionResult result;
        if (!usesV3Retrieval)
        {
            result = await _legacy.ExecuteAsync(
                new ReportExecutionRequest(report, request.Parameters),
                cancellationToken);
        }
        else
        {
            result = await ExecuteV3Async(report, request.Parameters, cancellationToken);
        }

        var projected = result.Queries
            .Select(query => Project(report, query))
            .ToArray();

        var totalRows = projected.Sum(query => query.Rows.Count);
        if (totalRows > _policy.MaximumTotalRows)
            throw new ReportExecutionLimitException(
                $"Report exceeded the maximum total generated rows ({_policy.MaximumTotalRows}). Reduce the period or increase the interval.");

        return result with
        {
            Queries = projected,
            GeneratedAtUtc = DateTimeOffset.UtcNow
        };
    }

    private async Task<ReportExecutionResult> ExecuteV3Async(
        ReportEngineeringDto report,
        IReadOnlyDictionary<string, ReportParameterValue>? suppliedParameters,
        CancellationToken cancellationToken)
    {
        var parameters = ResolveParameters(report, suppliedParameters);
        var queryResults = new List<ReportQueryExecutionResult>();
        var totalRows = 0;

        foreach (var reportQuery in report.Queries ?? Array.Empty<ReportQueryEngineeringDto>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var definition = ResolveDefinition(report, reportQuery);
            var executionParameters = parameters.ToDictionary(
                pair => pair.Key,
                pair => ToDataQueryValue(pair.Value),
                StringComparer.OrdinalIgnoreCase);

            var response = await _dataQueries.ExecuteTransientAsync(
                definition,
                new DataQueryExecutionRequest(executionParameters),
                cancellationToken);

            if (response.Rows.Count > _policy.MaximumRowsPerQuery)
                throw new ReportExecutionLimitException(
                    $"Report query '{reportQuery.Key}' exceeded the maximum row count ({_policy.MaximumRowsPerQuery}). Reduce the period or increase the interval.");

            totalRows += response.Rows.Count;
            if (totalRows > _policy.MaximumTotalRows)
                throw new ReportExecutionLimitException(
                    $"Report exceeded the maximum total row count ({_policy.MaximumTotalRows}). Reduce the period or increase the interval.");

            queryResults.Add(new ReportQueryExecutionResult(
                reportQuery.Key,
                response.DatasetKey,
                response.Columns,
                response.Rows.Select(row => row.Data).ToArray(),
                response.FromUtc,
                response.ToUtc,
                RetrievalName(response.RetrievalMode),
                response.Rows.Select(row => row.Provenance).ToArray()));
        }

        return new ReportExecutionResult(
            report.Id,
            report.Key,
            parameters,
            queryResults,
            DateTimeOffset.UtcNow);
    }

    private DataQueryEngineeringDto ResolveDefinition(
        ReportEngineeringDto report,
        ReportQueryEngineeringDto reportQuery)
    {
        DataQueryEngineeringDto? saved = null;
        if (reportQuery.DataQueryId.HasValue)
            saved = _dataQueryRegistry.Find(reportQuery.DataQueryId.Value);
        if (saved is null && !string.IsNullOrWhiteSpace(reportQuery.DataQueryKey))
            saved = _dataQueryRegistry.FindByKey(reportQuery.DataQueryKey);

        if ((reportQuery.DataQueryId.HasValue || !string.IsNullOrWhiteSpace(reportQuery.DataQueryKey)) && saved is null)
            throw new ReportExecutionValidationException(
                [new ReportEngineeringProblem(
                    "REPORT_DATA_QUERY_NOT_FOUND",
                    $"Report query '{reportQuery.Key}' references a saved Data Query that was not found.")]);

        var source = saved ?? new DataQueryEngineeringDto(
            StableTransientId(report, reportQuery),
            $"report.{report.Key}.{reportQuery.Key}",
            $"{report.Name} / {reportQuery.Key}",
            DataQueryExecutionService.HistoricalProviderKey,
            reportQuery.Query);

        var query = ApplyTagFilter(source.Query, report.Variables);
        var retrieval = saved?.HistorianRetrieval ?? MapRetrieval(report.Resolution);

        // Report-level resolution is an explicit presentation contract. When it is
        // configured, it overrides only the transient effective execution and never
        // mutates a referenced saved Data Query.
        if (report.Resolution is not null)
            retrieval = MapRetrieval(report.Resolution);

        return source with
        {
            Id = source.Id ?? StableTransientId(report, reportQuery),
            Query = query with { Page = new HistoricalPageRequest(Math.Min(200, query.Page?.Size ?? 200)) },
            HistorianRetrieval = retrieval
        };
    }

    private static ReportEngineeringDto ApplyRuntimeRangeAndVariables(
        ReportEngineeringDto report,
        ReportRuntimeTimeRange? runtime)
    {
        var queries = (report.Queries ?? Array.Empty<ReportQueryEngineeringDto>())
            .Select(query =>
            {
                var effective = query.Query;
                if (runtime is not null)
                    effective = effective with { Range = ToHistoricalRange(runtime) };
                effective = ApplyTagFilter(effective, report.Variables);
                return query with { Query = effective };
            })
            .ToArray();
        return report with { Queries = queries };
    }

    private static HistoricalQueryRequest ApplyTagFilter(
        HistoricalQueryRequest query,
        IReadOnlyCollection<ReportVariableEngineeringDto>? variables)
    {
        if (!string.Equals(query.Dataset, HistoricalDatasets.HistorianSamples, StringComparison.Ordinal) ||
            variables is null || variables.Count == 0)
            return query;

        var values = variables
            .Where(variable => variable.TagId != Guid.Empty)
            .OrderBy(variable => variable.Order)
            .ThenBy(variable => variable.TagId)
            .Select(variable => HistoricalQueryValue.FromGuid(variable.TagId))
            .ToArray();
        if (values.Length == 0) return query;

        var filters = (query.Filters ?? Array.Empty<HistoricalFilter>())
            .Where(filter => !string.Equals(filter.Field, "tag.id", StringComparison.Ordinal))
            .ToList();
        filters.Add(new HistoricalFilter("tag.id", HistoricalFilterOperator.In, values));
        return query with { Filters = filters };
    }

    private static HistorianRetrievalEngineeringDto MapRetrieval(
        ReportDataResolutionEngineeringDto? resolution)
    {
        if (resolution is null || resolution.Mode == ReportDataResolutionMode.Raw)
            return new HistorianRetrievalEngineeringDto(HistorianRetrievalMode.Raw);

        if (resolution.Mode == ReportDataResolutionMode.SampledFixedStep)
            return new HistorianRetrievalEngineeringDto(
                HistorianRetrievalMode.SampledFixedStep,
                StepMilliseconds: RequirePositive(resolution.IntervalMilliseconds, "Report fixed interval"),
                MaximumGapMilliseconds: RequirePositive(resolution.MaximumGapMilliseconds, "Report maximum gap"));

        return new HistorianRetrievalEngineeringDto(
            HistorianRetrievalMode.Aggregate,
            BucketMilliseconds: RequirePositive(resolution.BucketMilliseconds, "Report aggregate bucket"),
            AggregateFunction: MapAggregate(
                resolution.AggregateFunction
                ?? throw new HistoricalQueryValidationException("Report aggregate function is required.")));
    }

    private static DataQueryAggregateFunction MapAggregate(ReportAggregateFunction function) => function switch
    {
        ReportAggregateFunction.Count => DataQueryAggregateFunction.Count,
        ReportAggregateFunction.Sum => DataQueryAggregateFunction.Sum,
        ReportAggregateFunction.Average => DataQueryAggregateFunction.Average,
        ReportAggregateFunction.Minimum => DataQueryAggregateFunction.Minimum,
        ReportAggregateFunction.Maximum => DataQueryAggregateFunction.Maximum,
        ReportAggregateFunction.First => DataQueryAggregateFunction.First,
        ReportAggregateFunction.Last => DataQueryAggregateFunction.Last,
        _ => throw new HistoricalQueryValidationException($"Unsupported Report aggregate function '{function}'.")
    };

    private static int RequirePositive(int? value, string label)
    {
        if (!value.HasValue || value.Value <= 0)
            throw new HistoricalQueryValidationException($"{label} must be a positive duration.");
        return value.Value;
    }

    private static ReportQueryExecutionResult Project(
        ReportEngineeringDto report,
        ReportQueryExecutionResult query)
    {
        var variables = (report.Variables ?? Array.Empty<ReportVariableEngineeringDto>())
            .OrderBy(variable => variable.Order)
            .ThenBy(variable => variable.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (variables.Length == 0 ||
            !string.Equals(query.Dataset, HistoricalDatasets.HistorianSamples, StringComparison.Ordinal))
            return query;

        if (report.TableLayout == ReportTableLayout.Wide)
        {
            if (string.Equals(query.RetrievalMode, "raw", StringComparison.OrdinalIgnoreCase))
                throw new ReportExecutionValidationException(
                    [new ReportEngineeringProblem(
                        "REPORT_WIDE_RAW_UNSUPPORTED",
                        "Wide table projection requires fixed-interval or aggregate retrieval; raw asynchronous samples remain Long.")]);
            return ProjectWide(query, variables);
        }

        return ProjectLong(query, variables);
    }

    private static ReportQueryExecutionResult ProjectLong(
        ReportQueryExecutionResult query,
        IReadOnlyList<ReportVariableEngineeringDto> variables)
    {
        var byId = variables.ToDictionary(variable => variable.TagId);
        var columns = query.Columns.ToList();
        if (!columns.Any(column => column.Field == "variable"))
            columns.Insert(2, TextColumn("variable"));
        if (!columns.Any(column => column.Field == "unit"))
            columns.Add(TextColumn("unit"));

        var rows = query.Rows.Select(row =>
        {
            var cells = new Dictionary<string, HistoricalQueryValue>(row.Cells, StringComparer.Ordinal);
            if (cells.TryGetValue("tag.id", out var idValue) &&
                idValue.Value is not null &&
                Guid.TryParse(idValue.Value, out var id) &&
                byId.TryGetValue(id, out var variable))
            {
                cells["variable"] = HistoricalQueryValue.FromString(
                    string.IsNullOrWhiteSpace(variable.DisplayLabel) ? variable.Name : variable.DisplayLabel);
                var unit = DisplayUnit(variable);
                cells["unit"] = unit is null
                    ? HistoricalQueryValue.Null()
                    : HistoricalQueryValue.FromString(unit);
            }
            else
            {
                cells["variable"] = HistoricalQueryValue.Null();
                cells["unit"] = HistoricalQueryValue.Null();
            }
            return new HistoricalQueryRow(cells);
        }).ToArray();

        return query with { Columns = columns, Rows = rows };
    }

    private static ReportQueryExecutionResult ProjectWide(
        ReportQueryExecutionResult query,
        IReadOnlyList<ReportVariableEngineeringDto> variables)
    {
        var visible = variables.Where(variable => variable.Visible).ToArray();
        var columns = new List<HistoricalColumn> { DateColumn("timestamp") };
        foreach (var variable in visible)
        {
            columns.Add(new HistoricalColumn(
                ValueField(variable.TagId),
                FieldType(variable.DataType),
                Array.Empty<HistoricalFilterOperator>(),
                false,
                false,
                false));
            columns.Add(TextColumn(QualityField(variable.TagId)));
        }

        var byTimestamp = new SortedDictionary<DateTimeOffset, Dictionary<Guid, HistoricalQueryRow>>();
        foreach (var row in query.Rows)
        {
            if (!TryTimestamp(row, out var timestamp) || !TryTagId(row, out var tagId))
                continue;
            if (!byTimestamp.TryGetValue(timestamp, out var bucket))
            {
                bucket = new Dictionary<Guid, HistoricalQueryRow>();
                byTimestamp[timestamp] = bucket;
            }
            bucket[tagId] = row;
        }

        var rows = byTimestamp.Select(pair =>
        {
            var cells = new Dictionary<string, HistoricalQueryValue>(StringComparer.Ordinal)
            {
                ["timestamp"] = HistoricalQueryValue.FromDateTime(pair.Key)
            };
            foreach (var variable in visible)
            {
                if (pair.Value.TryGetValue(variable.TagId, out var source))
                {
                    cells[ValueField(variable.TagId)] = source.Cells.GetValueOrDefault("value") ?? HistoricalQueryValue.Null();
                    cells[QualityField(variable.TagId)] = source.Cells.GetValueOrDefault("quality") ?? HistoricalQueryValue.Null();
                }
                else
                {
                    cells[ValueField(variable.TagId)] = HistoricalQueryValue.Null();
                    cells[QualityField(variable.TagId)] = HistoricalQueryValue.FromEnum("Gap");
                }
            }
            return new HistoricalQueryRow(cells);
        }).ToArray();

        return query with { Columns = columns, Rows = rows };
    }

    private static bool TryTimestamp(HistoricalQueryRow row, out DateTimeOffset timestamp)
    {
        timestamp = default;
        if (!row.Cells.TryGetValue("timestamp", out var value) || value.Value is null) return false;
        return DateTimeOffset.TryParse(
            value.Value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out timestamp);
    }

    private static bool TryTagId(HistoricalQueryRow row, out Guid tagId)
    {
        tagId = default;
        return row.Cells.TryGetValue("tag.id", out var value) &&
               value.Value is not null &&
               Guid.TryParse(value.Value, out tagId);
    }

    private static string? DisplayUnit(ReportVariableEngineeringDto variable) => variable.UnitMode switch
    {
        ReportUnitMode.Hidden => null,
        ReportUnitMode.LabelOverride => string.IsNullOrWhiteSpace(variable.UnitLabel) ? null : variable.UnitLabel,
        _ => string.IsNullOrWhiteSpace(variable.EngineeringUnit) ? null : variable.EngineeringUnit
    };

    private static string ValueField(Guid id) => $"v:{id:D}";
    private static string QualityField(Guid id) => $"q:{id:D}";

    private static HistoricalColumn TextColumn(string field) =>
        new(field, HistoricalFieldType.String, Array.Empty<HistoricalFilterOperator>(), false, false, false);

    private static HistoricalColumn DateColumn(string field) =>
        new(field, HistoricalFieldType.DateTime, Array.Empty<HistoricalFilterOperator>(), false, false, false);

    private static HistoricalFieldType FieldType(string dataType) => dataType.Trim().ToLowerInvariant() switch
    {
        "boolean" => HistoricalFieldType.Boolean,
        "datetime" => HistoricalFieldType.DateTime,
        "int64" => HistoricalFieldType.Int64,
        "int16" or "int32" or "float" or "double" => HistoricalFieldType.Number,
        _ => HistoricalFieldType.Scalar
    };

    private static IReadOnlyDictionary<string, ReportParameterValue> ResolveParameters(
        ReportEngineeringDto report,
        IReadOnlyDictionary<string, ReportParameterValue>? supplied)
    {
        var suppliedValues = supplied ?? new Dictionary<string, ReportParameterValue>();
        var result = new Dictionary<string, ReportParameterValue>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in report.Parameters ?? Array.Empty<ReportParameterEngineeringDto>())
        {
            var value = suppliedValues.TryGetValue(definition.Key, out var explicitValue)
                ? explicitValue
                : definition.DefaultValue;
            if (!ReportEngineeringValidation.TryNormalizeParameterValue(value, definition.Type, out var normalized, out var error))
                throw new ReportExecutionValidationException(
                    [new ReportEngineeringProblem(
                        "REPORT_PARAMETER_INVALID",
                        $"Report parameter '{definition.Key}' is invalid: {error}")]);
            result[definition.Key] = normalized;
        }
        foreach (var suppliedKey in suppliedValues.Keys)
            if (!result.ContainsKey(suppliedKey))
                throw new ReportExecutionValidationException(
                    [new ReportEngineeringProblem(
                        "REPORT_PARAMETER_UNKNOWN",
                        $"Report parameter '{suppliedKey}' is not declared by the report.")]);
        return result;
    }

    private static DataQueryParameterValue ToDataQueryValue(ReportParameterValue value) =>
        new(MapParameterType(value.Type), value.Value);

    private static DataQueryParameterType MapParameterType(ReportParameterType type) => type switch
    {
        ReportParameterType.String => DataQueryParameterType.String,
        ReportParameterType.Boolean => DataQueryParameterType.Boolean,
        ReportParameterType.Number => DataQueryParameterType.Number,
        ReportParameterType.Int64 => DataQueryParameterType.Int64,
        ReportParameterType.DateTime => DataQueryParameterType.DateTime,
        ReportParameterType.DurationSeconds => DataQueryParameterType.DurationSeconds,
        ReportParameterType.Guid => DataQueryParameterType.Guid,
        ReportParameterType.Enum => DataQueryParameterType.Enum,
        _ => throw new HistoricalQueryValidationException($"Unsupported report parameter type '{type}'.")
    };

    private static HistoricalTimeRange ToHistoricalRange(ReportRuntimeTimeRange range) => range.Kind switch
    {
        HistoricalTimeRangeKind.Relative =>
            HistoricalTimeRange.Relative(
                range.DurationSeconds
                ?? throw new HistoricalQueryValidationException("Relative report range requires DurationSeconds.")),
        HistoricalTimeRangeKind.Absolute =>
            HistoricalTimeRange.Absolute(
                range.FromUtc
                ?? throw new HistoricalQueryValidationException("Absolute report range requires FromUtc."),
                range.ToUtc
                ?? throw new HistoricalQueryValidationException("Absolute report range requires ToUtc.")),
        _ => throw new HistoricalQueryValidationException("Unsupported report time-range kind.")
    };

    private static void ValidateRuntimeRange(ReportRuntimeTimeRange? range)
    {
        if (range is null) return;
        if (range.Kind == HistoricalTimeRangeKind.Relative)
        {
            if (!range.DurationSeconds.HasValue || range.DurationSeconds.Value <= 0)
                throw new HistoricalQueryValidationException("Relative report duration must be positive.");
            return;
        }

        if (!range.FromUtc.HasValue || !range.ToUtc.HasValue)
            throw new HistoricalQueryValidationException("Absolute report range requires From and To.");
        if (range.FromUtc.Value.Offset != TimeSpan.Zero || range.ToUtc.Value.Offset != TimeSpan.Zero)
            throw new HistoricalQueryValidationException("Report query authority requires UTC absolute timestamps.");
        if (range.FromUtc.Value >= range.ToUtc.Value)
            throw new HistoricalQueryValidationException("Report From must be earlier than To.");
    }

    private static string RetrievalName(HistorianRetrievalMode mode) => mode switch
    {
        HistorianRetrievalMode.SampledFixedStep => "sampledFixedStep",
        HistorianRetrievalMode.Aggregate => "aggregate",
        HistorianRetrievalMode.Raw => "raw",
        _ => mode.ToString()
    };

    private static Guid StableTransientId(ReportEngineeringDto report, ReportQueryEngineeringDto query)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes($"report:{report.Id}:{report.Key}:{query.Key}"));
        Span<byte> guidBytes = stackalloc byte[16];
        bytes.AsSpan(0, 16).CopyTo(guidBytes);
        return new Guid(guidBytes);
    }
}
