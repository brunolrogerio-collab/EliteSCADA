using System.Globalization;
using Scada.Core.HistoricalQueries;

namespace Scada.Engineering.Reports;

public sealed record ReportExecutionPolicy(
    int MaximumRowsPerQuery = 5000,
    int MaximumTotalRows = 10000,
    int MaximumPagesPerQuery = 50)
{
    public void Validate()
    {
        if (MaximumRowsPerQuery is < 1 or > 100000)
            throw new ArgumentOutOfRangeException(nameof(MaximumRowsPerQuery));
        if (MaximumTotalRows < MaximumRowsPerQuery || MaximumTotalRows > 250000)
            throw new ArgumentOutOfRangeException(nameof(MaximumTotalRows));
        if (MaximumPagesPerQuery is < 1 or > 1000)
            throw new ArgumentOutOfRangeException(nameof(MaximumPagesPerQuery));
    }
}

public sealed record ReportExecutionRequest(
    ReportEngineeringDto Report,
    IReadOnlyDictionary<string, ReportParameterValue>? Parameters = null);

public sealed record ReportQueryExecutionResult(
    string QueryKey,
    string Dataset,
    IReadOnlyList<HistoricalColumn> Columns,
    IReadOnlyList<HistoricalQueryRow> Rows,
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc);

public sealed record ReportExecutionResult(
    Guid? ReportId,
    string ReportKey,
    IReadOnlyDictionary<string, ReportParameterValue> Parameters,
    IReadOnlyList<ReportQueryExecutionResult> Queries);

public sealed class ReportExecutionValidationException : InvalidOperationException
{
    public ReportExecutionValidationException(IReadOnlyList<ReportEngineeringProblem> problems)
        : base("Report Engineering is invalid and cannot execute.")
    {
        Problems = problems;
    }

    public IReadOnlyList<ReportEngineeringProblem> Problems { get; }
}

public sealed class ReportExecutionLimitException(string message) : InvalidOperationException(message);

public interface IReportExecutionService
{
    Task<ReportExecutionResult> ExecuteAsync(
        ReportExecutionRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Executes canonical Report Engineering by delegating every historical data page to
/// the accepted Historical Query v1 service. Reporting never resolves relative time,
/// opens a database, interprets a cursor or creates a second provider/query language.
/// </summary>
public sealed class ReportExecutionService : IReportExecutionService
{
    private readonly IHistoricalQueryService _historicalQueries;
    private readonly ReportExecutionPolicy _policy;

    public ReportExecutionService(
        IHistoricalQueryService historicalQueries,
        ReportExecutionPolicy? policy = null)
    {
        _historicalQueries = historicalQueries ?? throw new ArgumentNullException(nameof(historicalQueries));
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

        var problems = ReportEngineeringValidation.Validate(request.Report);
        if (problems.Count != 0)
            throw new ReportExecutionValidationException(problems);

        var parameters = ResolveParameters(request.Report, request.Parameters);
        var results = new List<ReportQueryExecutionResult>();
        var totalRows = 0;

        foreach (var queryDefinition in request.Report.Queries ?? Array.Empty<ReportQueryEngineeringDto>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var query = ApplyBindings(queryDefinition, parameters);
            _ = HistoricalQueryValidator.Validate(query);

            var rows = new List<HistoricalQueryRow>();
            IReadOnlyList<HistoricalColumn>? columns = null;
            DateTimeOffset? resolvedFromUtc = null;
            DateTimeOffset? resolvedToUtc = null;
            string? cursor = null;
            var pages = 0;
            var pageSize = query.Page?.Size ?? HistoricalQueryValidator.DefaultPageSize;

            do
            {
                cancellationToken.ThrowIfCancellationRequested();
                pages++;
                if (pages > _policy.MaximumPagesPerQuery)
                    throw new ReportExecutionLimitException(
                        $"Report query '{queryDefinition.Key}' exceeded the maximum page count {_policy.MaximumPagesPerQuery}.");

                var pageRequest = query with
                {
                    Page = new HistoricalPageRequest(pageSize, cursor)
                };
                var response = await _historicalQueries.QueryAsync(pageRequest, cancellationToken);

                if (!string.Equals(response.Dataset, query.Dataset, StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        $"Historical Query returned dataset '{response.Dataset}' for report query '{queryDefinition.Key}', expected '{query.Dataset}'.");

                if (resolvedFromUtc is null)
                {
                    resolvedFromUtc = response.FromUtc;
                    resolvedToUtc = response.ToUtc;
                    columns = response.Columns;
                }
                else if (response.FromUtc != resolvedFromUtc.Value || response.ToUtc != resolvedToUtc!.Value)
                {
                    throw new InvalidOperationException(
                        $"Historical Query changed the resolved time window while paging report query '{queryDefinition.Key}'.");
                }

                if (rows.Count + response.Rows.Count > _policy.MaximumRowsPerQuery)
                    throw new ReportExecutionLimitException(
                        $"Report query '{queryDefinition.Key}' exceeded the maximum row count {_policy.MaximumRowsPerQuery}.");
                if (totalRows + response.Rows.Count > _policy.MaximumTotalRows)
                    throw new ReportExecutionLimitException(
                        $"Report execution exceeded the maximum total row count {_policy.MaximumTotalRows}.");

                rows.AddRange(response.Rows);
                totalRows += response.Rows.Count;
                cursor = response.NextCursor;
            }
            while (!string.IsNullOrWhiteSpace(cursor));

            results.Add(new ReportQueryExecutionResult(
                queryDefinition.Key,
                query.Dataset,
                columns ?? Array.Empty<HistoricalColumn>(),
                rows,
                resolvedFromUtc ?? throw new InvalidOperationException("Historical Query returned no resolved FromUtc."),
                resolvedToUtc ?? throw new InvalidOperationException("Historical Query returned no resolved ToUtc.")));
        }

        return new ReportExecutionResult(
            request.Report.Id,
            request.Report.Key,
            parameters,
            results);
    }

    private static IReadOnlyDictionary<string, ReportParameterValue> ResolveParameters(
        ReportEngineeringDto report,
        IReadOnlyDictionary<string, ReportParameterValue>? overrides)
    {
        var definitions = (report.Parameters ?? Array.Empty<ReportParameterEngineeringDto>())
            .ToDictionary(x => x.Key, StringComparer.OrdinalIgnoreCase);
        var supplied = overrides ?? new Dictionary<string, ReportParameterValue>();

        foreach (var key in supplied.Keys)
        {
            if (!definitions.ContainsKey(key))
                throw new ArgumentException($"Unknown report runtime parameter '{key}'.", nameof(overrides));
        }

        var resolved = new Dictionary<string, ReportParameterValue>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in definitions.Values)
        {
            var candidate = supplied.TryGetValue(definition.Key, out var runtime)
                ? runtime
                : definition.DefaultValue;
            if (!ReportEngineeringValidation.TryNormalizeParameterValue(
                    candidate,
                    definition.Type,
                    out var normalized,
                    out var error))
                throw new ArgumentException(
                    $"Report runtime parameter '{definition.Key}' is invalid: {error}",
                    nameof(overrides));

            var allowed = definition.AllowedValues ?? Array.Empty<ReportParameterValue>();
            if (allowed.Count > 0 && !allowed.Any(value =>
                    ReportEngineeringValidation.TryNormalizeParameterValue(
                        value,
                        definition.Type,
                        out var normalizedAllowed,
                        out _) && normalizedAllowed == normalized))
                throw new ArgumentException(
                    $"Report runtime parameter '{definition.Key}' is not one of the allowed values.",
                    nameof(overrides));

            resolved[definition.Key] = normalized;
        }

        return resolved;
    }

    private static HistoricalQueryRequest ApplyBindings(
        ReportQueryEngineeringDto definition,
        IReadOnlyDictionary<string, ReportParameterValue> parameters)
    {
        var query = definition.Query with
        {
            Filters = (definition.Query.Filters ?? Array.Empty<HistoricalFilter>()).
                Select(filter => filter with { Values = filter.Values.ToArray() }).ToArray(),
            OrderBy = definition.Query.OrderBy?.ToArray(),
            Page = new HistoricalPageRequest(
                definition.Query.Page?.Size ?? HistoricalQueryValidator.DefaultPageSize,
                null)
        };

        foreach (var binding in definition.ParameterBindings ?? Array.Empty<ReportQueryParameterBindingEngineeringDto>())
        {
            if (!parameters.TryGetValue(binding.ParameterKey, out var parameter))
                throw new InvalidOperationException(
                    $"Report query '{definition.Key}' references unresolved parameter '{binding.ParameterKey}'.");

            query = binding.Target switch
            {
                ReportQueryParameterTarget.AbsoluteFromUtc => query with
                {
                    Range = query.Range with { FromUtc = parameter.AsDateTimeUtc() }
                },
                ReportQueryParameterTarget.AbsoluteToUtc => query with
                {
                    Range = query.Range with { ToUtc = parameter.AsDateTimeUtc() }
                },
                ReportQueryParameterTarget.RelativeDurationSeconds => query with
                {
                    Range = query.Range with { DurationSeconds = parameter.AsDurationSeconds() }
                },
                ReportQueryParameterTarget.Search => query with { Search = parameter.Value },
                ReportQueryParameterTarget.FilterValue => BindFilterValue(definition.Key, query, binding, parameter),
                _ => throw new InvalidOperationException(
                    $"Report query '{definition.Key}' contains an unsupported parameter binding target.")
            };
        }

        return query;
    }

    private static HistoricalQueryRequest BindFilterValue(
        string queryKey,
        HistoricalQueryRequest query,
        ReportQueryParameterBindingEngineeringDto binding,
        ReportParameterValue parameter)
    {
        var filters = (query.Filters ?? Array.Empty<HistoricalFilter>()).ToArray();
        if (!binding.FilterIndex.HasValue || binding.FilterIndex < 0 || binding.FilterIndex >= filters.Length)
            throw new InvalidOperationException(
                $"Report query '{queryKey}' parameter binding has an invalid FilterIndex.");

        var filterIndex = binding.FilterIndex.Value;
        var values = filters[filterIndex].Values.ToArray();
        if (!binding.ValueIndex.HasValue || binding.ValueIndex < 0 || binding.ValueIndex >= values.Length)
            throw new InvalidOperationException(
                $"Report query '{queryKey}' parameter binding has an invalid ValueIndex.");

        var valueIndex = binding.ValueIndex.Value;
        values[valueIndex] = ToHistoricalValue(values[valueIndex].Kind, parameter);
        filters[filterIndex] = filters[filterIndex] with { Values = values };
        return query with { Filters = filters };
    }

    private static HistoricalQueryValue ToHistoricalValue(
        HistoricalValueKind kind,
        ReportParameterValue parameter) => kind switch
    {
        HistoricalValueKind.Guid => HistoricalQueryValue.FromGuid(parameter.AsGuid()),
        HistoricalValueKind.String => HistoricalQueryValue.FromString(parameter.Value),
        HistoricalValueKind.Enum => HistoricalQueryValue.FromEnum(parameter.Value),
        HistoricalValueKind.Boolean => HistoricalQueryValue.FromBoolean(parameter.AsBoolean()),
        HistoricalValueKind.DateTime => HistoricalQueryValue.FromDateTime(parameter.AsDateTimeUtc()),
        HistoricalValueKind.Int64 => HistoricalQueryValue.FromInt64(parameter.AsInt64()),
        HistoricalValueKind.Int16 => HistoricalQueryValue.FromInt16(ToInt16(parameter.AsNumber())),
        HistoricalValueKind.Int32 => HistoricalQueryValue.FromInt32(ToInt32(parameter.AsNumber())),
        HistoricalValueKind.Float => HistoricalQueryValue.FromFloat(ToFloat(parameter.AsNumber())),
        HistoricalValueKind.Double => HistoricalQueryValue.FromDouble(parameter.AsNumber()),
        HistoricalValueKind.Number => HistoricalQueryValue.FromNumber(parameter.AsNumber()),
        _ => throw new InvalidOperationException(
            $"Historical Query value kind '{kind}' cannot be parameter-bound by Reporting.")
    };

    private static short ToInt16(double value)
    {
        if (!double.IsFinite(value) || Math.Truncate(value) != value || value < short.MinValue || value > short.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(value), "Report numeric parameter cannot be represented as Int16.");
        return (short)value;
    }

    private static int ToInt32(double value)
    {
        if (!double.IsFinite(value) || Math.Truncate(value) != value || value < int.MinValue || value > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(value), "Report numeric parameter cannot be represented as Int32.");
        return (int)value;
    }

    private static float ToFloat(double value)
    {
        if (!double.IsFinite(value) || value < -float.MaxValue || value > float.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(value), "Report numeric parameter cannot be represented as Float.");
        return (float)value;
    }
}
