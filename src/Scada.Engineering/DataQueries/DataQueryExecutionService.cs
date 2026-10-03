using System.Globalization;
using Scada.Core.HistoricalQueries;
using Scada.Core.Tags;
using Scada.Engineering.Contracts;

namespace Scada.Engineering.DataQueries;

public sealed record DataQueryExecutionRequest(
    IReadOnlyDictionary<string, DataQueryParameterValue>? Parameters = null,
    HistoricalPageRequest? Page = null);

public sealed record DataQueryExecutionProvenance(
    string Kind,
    IReadOnlyList<DateTimeOffset> SourceTimestampsUtc,
    DateTimeOffset? BucketStartUtc = null,
    DateTimeOffset? BucketEndUtc = null,
    long? SampleCount = null,
    long? GoodCount = null,
    long? UncertainCount = null,
    long? BadCount = null,
    string? DerivedQuality = null,
    string? Reason = null);

public sealed record DataQueryExecutionRow(
    HistoricalQueryRow Data,
    DataQueryExecutionProvenance? Provenance = null);

public sealed record DataQueryExecutionResponse(
    int Version,
    Guid QueryId,
    string QueryKey,
    string DatasetKey,
    HistorianRetrievalMode RetrievalMode,
    IReadOnlyList<HistoricalColumn> Columns,
    IReadOnlyList<DataQueryExecutionRow> Rows,
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    string? NextCursor,
    int ResultLimit);

public interface IDataQueryExecutionService
{
    Task<DataQueryExecutionResponse> ExecuteAsync(
        string queryKey,
        DataQueryExecutionRequest? request = null,
        CancellationToken cancellationToken = default);

    Task<DataQueryExecutionResponse> ExecuteAsync(
        Guid queryId,
        DataQueryExecutionRequest? request = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Runtime-only execution envelope for a Data Query definition that is not persisted
/// into Working/Active Engineering. Historical Playback uses this path so arbitrary
/// visual TAG bindings reuse the canonical Data Query/Historian authority.
/// </summary>
public sealed record TransientDataQueryExecutionRequest(
    DataQueryEngineeringDto Definition,
    DataQueryExecutionRequest? Execution = null);

public interface ITransientDataQueryExecutionService
{
    Task<DataQueryExecutionResponse> ExecuteTransientAsync(
        DataQueryEngineeringDto definition,
        DataQueryExecutionRequest? request = null,
        CancellationToken cancellationToken = default);
}

public sealed class DataQueryDefinitionNotFoundException(string message) : KeyNotFoundException(message);

public sealed class DataQueryExecutionService : IDataQueryExecutionService, ITransientDataQueryExecutionService
{
    public const string HistoricalProviderKey = "historical";

    private readonly IDataQueryEngineeringRegistry _registry;
    private readonly IHistoricalQueryService _historicalQuery;
    private readonly IHistoricalQueryAuthorizer _authorizer;
    private readonly IReadOnlyDictionary<string, IHistoricalRetrievalProvider> _retrievalProviders;
    private readonly Func<DateTimeOffset> _utcNow;

    public DataQueryExecutionService(
        IDataQueryEngineeringRegistry registry,
        IHistoricalQueryService historicalQuery,
        IHistoricalQueryAuthorizer authorizer,
        IEnumerable<IHistoricalRetrievalProvider> retrievalProviders,
        Func<DateTimeOffset>? utcNow = null)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _historicalQuery = historicalQuery ?? throw new ArgumentNullException(nameof(historicalQuery));
        _authorizer = authorizer ?? throw new ArgumentNullException(nameof(authorizer));
        ArgumentNullException.ThrowIfNull(retrievalProviders);
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);

        var providers = retrievalProviders.ToArray();
        if (providers.Any(x => x is null || string.IsNullOrWhiteSpace(x.Dataset)))
            throw new ArgumentException("Historical retrieval providers must declare a dataset.", nameof(retrievalProviders));
        if (providers.GroupBy(x => x.Dataset, StringComparer.Ordinal).Any(g => g.Count() > 1))
            throw new ArgumentException("Historical retrieval provider dataset IDs must be unique.", nameof(retrievalProviders));
        _retrievalProviders = providers.ToDictionary(x => x.Dataset, StringComparer.Ordinal);
    }

    public Task<DataQueryExecutionResponse> ExecuteAsync(
        string queryKey,
        DataQueryExecutionRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(queryKey))
            throw new HistoricalQueryValidationException("Data Query key is required.");
        var definition = _registry.FindByKey(queryKey.Trim())
            ?? throw new DataQueryDefinitionNotFoundException($"Data Query '{queryKey}' was not found.");
        return ExecuteDefinitionAsync(definition, request ?? new DataQueryExecutionRequest(), cancellationToken);
    }

    public Task<DataQueryExecutionResponse> ExecuteAsync(
        Guid queryId,
        DataQueryExecutionRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        if (queryId == Guid.Empty)
            throw new HistoricalQueryValidationException("Data Query Id cannot be empty.");
        var definition = _registry.Find(queryId)
            ?? throw new DataQueryDefinitionNotFoundException($"Data Query '{queryId:D}' was not found.");
        return ExecuteDefinitionAsync(definition, request ?? new DataQueryExecutionRequest(), cancellationToken);
    }

    public Task<DataQueryExecutionResponse> ExecuteTransientAsync(
        DataQueryEngineeringDto definition,
        DataQueryExecutionRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return ExecuteDefinitionAsync(
            definition,
            request ?? new DataQueryExecutionRequest(),
            cancellationToken);
    }

    private async Task<DataQueryExecutionResponse> ExecuteDefinitionAsync(
        DataQueryEngineeringDto definition,
        DataQueryExecutionRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var definitionProblems = DataQueryEngineeringValidation.Validate(definition);
        if (definitionProblems.Count > 0)
            throw new HistoricalQueryValidationException(
                string.Join(" ", definitionProblems.Select(x => x.Message)));

        if (!string.Equals(definition.ProviderKey, HistoricalProviderKey, StringComparison.OrdinalIgnoreCase))
            throw new HistoricalQueryValidationException(
                $"Data Query provider '{definition.ProviderKey}' is not executable by the historical provider authority.");

        var (effectiveRequest, retrieval) = Bind(definition, request);
        var mode = retrieval.Mode;

        if (mode != HistorianRetrievalMode.Aggregate &&
            ((definition.Groups?.Count ?? 0) > 0 || (definition.Aggregates?.Count ?? 0) > 0))
            throw new HistoricalQueryValidationException(
                "Data Query grouping/aggregate descriptors require Aggregate historian retrieval in the current provider.");

        if (mode == HistorianRetrievalMode.Aggregate)
            ValidateAggregateDefinition(definition, retrieval);

        if (mode == HistorianRetrievalMode.Raw)
        {
            var raw = await _historicalQuery.QueryAsync(effectiveRequest, cancellationToken);
            return ProjectRaw(definition, raw, mode);
        }

        if (!string.Equals(effectiveRequest.Dataset, HistoricalDatasets.HistorianSamples, StringComparison.Ordinal))
            throw new HistoricalQueryValidationException(
                "Non-Raw historian retrieval modes require dataset 'historian.samples'.");
        if (effectiveRequest.Page?.Cursor is not null)
            throw new HistoricalQueryValidationException(
                "Non-Raw Data Query retrieval does not accept Historical Query cursors; use bounded step/bucket/result settings.");

        HistoricalValidatedRequest validated;
        HistoricalResolvedRange range;
        try
        {
            validated = HistoricalQueryValidator.Validate(effectiveRequest);
            range = HistoricalQueryValidator.ResolveRange(validated.RequestedRange, _utcNow());
        }
        catch (HistoricalQueryValidationException)
        {
            throw;
        }
        catch (ArgumentException ex)
        {
            throw new HistoricalQueryValidationException(FirstLine(ex.Message), ex);
        }

        await AuthorizeAsync(validated.Dataset.Id, cancellationToken);

        if (!_retrievalProviders.TryGetValue(validated.Dataset.Id, out var provider))
            throw new HistoricalQueryProviderException(
                $"Historical retrieval provider for dataset '{validated.Dataset.Id}' is unavailable.");

        var execution = new HistoricalQueryExecution(
            validated.Dataset,
            range,
            validated.Filters,
            validated.Search,
            validated.Sort,
            validated.PageSize,
            null);

        HistoricalRetrievalResult result;
        try
        {
            result = mode switch
            {
                HistorianRetrievalMode.Last =>
                    await provider.QueryLastAsync(execution, cancellationToken),
                HistorianRetrievalMode.AtOrBefore =>
                    await provider.QueryAtOrBeforeAsync(
                        execution,
                        RequireTarget(retrieval, range),
                        cancellationToken),
                HistorianRetrievalMode.AtOrAfter =>
                    await provider.QueryAtOrAfterAsync(
                        execution,
                        RequireTarget(retrieval, range),
                        cancellationToken),
                HistorianRetrievalMode.Exact =>
                    await provider.QueryExactAsync(
                        execution,
                        RequireTarget(retrieval, range),
                        cancellationToken),
                HistorianRetrievalMode.Interpolated =>
                    await provider.QueryInterpolatedAsync(
                        execution,
                        RequireTarget(retrieval, range),
                        RequirePositive(retrieval.MaximumGapMilliseconds, "MaximumGapMilliseconds"),
                        cancellationToken),
                HistorianRetrievalMode.SampledFixedStep =>
                    await provider.QuerySampledFixedStepAsync(
                        execution,
                        RequirePositive(retrieval.StepMilliseconds, "StepMilliseconds"),
                        RequirePositive(retrieval.MaximumGapMilliseconds, "MaximumGapMilliseconds"),
                        cancellationToken),
                HistorianRetrievalMode.Aggregate =>
                    await provider.QueryAggregateAsync(
                        execution,
                        RequirePositive(retrieval.BucketMilliseconds, "BucketMilliseconds"),
                        MapAggregate(retrieval.AggregateFunction
                            ?? throw new HistoricalQueryValidationException("AggregateFunction is required.")),
                        cancellationToken),
                _ => throw new HistoricalQueryValidationException(
                    $"Historian retrieval mode '{mode}' is unsupported.")
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (HistoricalQueryValidationException)
        {
            throw;
        }
        catch (HistoricalQueryProviderException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new HistoricalQueryProviderException(
                $"Historical retrieval provider failed for mode '{mode}'.",
                ex);
        }

        if (result.Points.Count > validated.PageSize)
            throw new HistoricalQueryProviderException(
                "Historical retrieval provider exceeded the validated result limit.");

        var columns = ProjectColumns(definition, validated.Dataset.Columns);
        var rows = result.Points.Select(point =>
            new DataQueryExecutionRow(
                ProjectRow(definition, PointRow(point)),
                MapProvenance(point.Provenance)))
            .ToArray();

        return new DataQueryExecutionResponse(
            definition.Version,
            definition.Id!.Value,
            definition.Key,
            validated.Dataset.Id,
            mode,
            columns,
            rows,
            result.Range.FromUtc,
            result.Range.ToUtc,
            null,
            validated.PageSize);
    }

    private static void ValidateAggregateDefinition(
        DataQueryEngineeringDto definition,
        HistorianRetrievalEngineeringDto retrieval)
    {
        var groups = definition.Groups ?? Array.Empty<DataQueryGroupEngineeringDto>();
        if (groups.Count > 1 || (groups.Count == 1 && !string.Equals(groups.Single().Field, "tag.id", StringComparison.Ordinal)))
            throw new HistoricalQueryValidationException(
                "Historian Aggregate currently supports only implicit per-TAG grouping or an explicit 'tag.id' group.");

        var aggregates = definition.Aggregates ?? Array.Empty<DataQueryAggregateEngineeringDto>();
        if (aggregates.Count == 0) return;
        if (aggregates.Count != 1 ||
            !string.Equals(aggregates.Single().Field, "value", StringComparison.Ordinal) ||
            aggregates.Single().Function != retrieval.AggregateFunction)
            throw new HistoricalQueryValidationException(
                "Historian Aggregate supports one 'value' aggregate matching HistorianRetrieval.AggregateFunction.");
    }

    private static (HistoricalQueryRequest Query, HistorianRetrievalEngineeringDto Retrieval) Bind(
        DataQueryEngineeringDto definition,
        DataQueryExecutionRequest request)
    {
        var parameterDefinitions = (definition.Parameters ?? Array.Empty<DataQueryParameterEngineeringDto>())
            .ToDictionary(x => x.Key.Trim(), StringComparer.OrdinalIgnoreCase);
        var supplied = (request.Parameters ?? new Dictionary<string, DataQueryParameterValue>())
            .ToDictionary(x => x.Key.Trim(), x => x.Value, StringComparer.OrdinalIgnoreCase);

        foreach (var key in supplied.Keys)
        {
            if (!parameterDefinitions.ContainsKey(key))
                throw new HistoricalQueryValidationException($"Unknown Data Query parameter '{key}'.");
        }

        var values = new Dictionary<string, DataQueryParameterValue>(StringComparer.OrdinalIgnoreCase);
        foreach (var parameter in parameterDefinitions.Values)
        {
            var value = supplied.TryGetValue(parameter.Key, out var runtime)
                ? runtime
                : parameter.DefaultValue;
            if (value is null) continue;
            if (value.Type != parameter.Type)
                throw new HistoricalQueryValidationException(
                    $"Data Query parameter '{parameter.Key}' expects type '{parameter.Type}'.");

            if (!DataQueryEngineeringValidation.TryNormalizeParameterValue(value, out var normalized))
                throw new HistoricalQueryValidationException(
                    $"Data Query parameter '{parameter.Key}' is invalid.");
            var normalizedValue = value with { Value = normalized };

            if (parameter.AllowedValues is { Count: > 0 })
            {
                var allowed = parameter.AllowedValues.Any(candidate =>
                    candidate.Type == parameter.Type &&
                    DataQueryEngineeringValidation.TryNormalizeParameterValue(candidate, out var normalizedAllowed) &&
                    string.Equals(normalizedAllowed, normalized, StringComparison.Ordinal));
                if (!allowed)
                    throw new HistoricalQueryValidationException(
                        $"Data Query parameter '{parameter.Key}' is outside its allowed values.");
            }
            values[parameter.Key] = normalizedValue;
        }

        var query = definition.Query;
        var range = query.Range;
        var filters = (query.Filters ?? Array.Empty<HistoricalFilter>())
            .Select(x => new HistoricalFilter(x.Field, x.Operator, x.Values.ToArray()))
            .ToArray();
        var search = query.Search;
        var retrieval = definition.HistorianRetrieval ?? new HistorianRetrievalEngineeringDto();

        foreach (var binding in definition.ParameterBindings ?? Array.Empty<DataQueryParameterBindingEngineeringDto>())
        {
            if (!values.TryGetValue(binding.ParameterKey, out var value))
                throw new HistoricalQueryValidationException(
                    $"Data Query parameter '{binding.ParameterKey}' requires a runtime or default value.");

            switch (binding.Target)
            {
                case DataQueryParameterTarget.AbsoluteFromUtc:
                    if (range.Kind != HistoricalTimeRangeKind.Absolute)
                        throw new HistoricalQueryValidationException("absoluteFromUtc binding requires an absolute Historical Query range.");
                    range = range with { FromUtc = ParseUtc(value) };
                    break;
                case DataQueryParameterTarget.AbsoluteToUtc:
                    if (range.Kind != HistoricalTimeRangeKind.Absolute)
                        throw new HistoricalQueryValidationException("absoluteToUtc binding requires an absolute Historical Query range.");
                    range = range with { ToUtc = ParseUtc(value) };
                    break;
                case DataQueryParameterTarget.RelativeDurationSeconds:
                    if (range.Kind != HistoricalTimeRangeKind.Relative)
                        throw new HistoricalQueryValidationException("relativeDurationSeconds binding requires a relative Historical Query range.");
                    range = range with { DurationSeconds = int.Parse(value.Value, CultureInfo.InvariantCulture) };
                    break;
                case DataQueryParameterTarget.HistorianTargetUtc:
                    retrieval = retrieval with { TargetUtc = ParseUtc(value) };
                    break;
                case DataQueryParameterTarget.Search:
                    search = value.Value;
                    break;
                case DataQueryParameterTarget.FilterValue:
                    if (!binding.FilterIndex.HasValue || !binding.ValueIndex.HasValue ||
                        binding.FilterIndex < 0 || binding.FilterIndex >= filters.Length ||
                        binding.ValueIndex < 0 || binding.ValueIndex >= filters[binding.FilterIndex.Value].Values.Count)
                        throw new HistoricalQueryValidationException("filterValue binding indexes are invalid.");
                    var filter = filters[binding.FilterIndex.Value];
                    var filterValues = filter.Values.ToArray();
                    filterValues[binding.ValueIndex.Value] = ToHistoricalValue(value);
                    filters[binding.FilterIndex.Value] = filter with { Values = filterValues };
                    break;
                default:
                    throw new HistoricalQueryValidationException(
                        $"Data Query parameter target '{binding.Target}' is unsupported.");
            }
        }

        var page = request.Page ?? query.Page;
        return (
            query with { Range = range, Filters = filters, Search = search, Page = page },
            retrieval);
    }

    private async Task AuthorizeAsync(string dataset, CancellationToken cancellationToken)
    {
        var decision = await _authorizer.AuthorizeAsync(dataset, cancellationToken);
        switch (decision.Outcome)
        {
            case HistoricalAuthorizationOutcome.Allowed:
                return;
            case HistoricalAuthorizationOutcome.Unauthenticated:
                throw new HistoricalQueryUnauthorizedException(decision.Reason);
            case HistoricalAuthorizationOutcome.Forbidden:
                throw new HistoricalQueryForbiddenException(decision.Reason);
            default:
                throw new HistoricalQueryForbiddenException(
                    "Historical authorization returned an unknown decision and failed closed.");
        }
    }

    private static DateTimeOffset RequireTarget(
        HistorianRetrievalEngineeringDto retrieval,
        HistoricalResolvedRange range)
    {
        var target = retrieval.TargetUtc
            ?? throw new HistoricalQueryValidationException(
                $"Historian retrieval mode '{retrieval.Mode}' requires effective TargetUtc.");
        if (target.Offset != TimeSpan.Zero)
            throw new HistoricalQueryValidationException("Historian TargetUtc must use UTC offset +00:00.");
        if (target < range.FromUtc || target > range.ToUtc)
            throw new HistoricalQueryValidationException(
                "Historian TargetUtc must be inside the resolved Historical Query range, inclusive.");
        return target;
    }

    private static int RequirePositive(int? value, string name) =>
        value is > 0
            ? value.Value
            : throw new HistoricalQueryValidationException($"{name} must be positive.");

    private static HistoricalAggregateOperation MapAggregate(DataQueryAggregateFunction operation) => operation switch
    {
        DataQueryAggregateFunction.Count => HistoricalAggregateOperation.Count,
        DataQueryAggregateFunction.Sum => HistoricalAggregateOperation.Sum,
        DataQueryAggregateFunction.Average => HistoricalAggregateOperation.Average,
        DataQueryAggregateFunction.Minimum => HistoricalAggregateOperation.Minimum,
        DataQueryAggregateFunction.Maximum => HistoricalAggregateOperation.Maximum,
        DataQueryAggregateFunction.First => HistoricalAggregateOperation.First,
        DataQueryAggregateFunction.Last => HistoricalAggregateOperation.Last,
        _ => throw new HistoricalQueryValidationException($"Aggregate function '{operation}' is unsupported.")
    };

    private static DataQueryExecutionResponse ProjectRaw(
        DataQueryEngineeringDto definition,
        HistoricalQueryResponse response,
        HistorianRetrievalMode mode)
    {
        var columns = ProjectColumns(definition, response.Columns);
        var rows = response.Rows
            .Select(row => new DataQueryExecutionRow(ProjectRow(definition, row)))
            .ToArray();
        return new DataQueryExecutionResponse(
            definition.Version,
            definition.Id!.Value,
            definition.Key,
            response.Dataset,
            mode,
            columns,
            rows,
            response.FromUtc,
            response.ToUtc,
            response.NextCursor,
            response.PageSize);
    }

    private static IReadOnlyList<HistoricalColumn> ProjectColumns(
        DataQueryEngineeringDto definition,
        IReadOnlyList<HistoricalColumn> columns)
    {
        var selected = definition.SelectedFields;
        if (selected is null || selected.Count == 0) return columns;
        var wanted = selected.ToHashSet(StringComparer.Ordinal);
        return columns.Where(x => wanted.Contains(x.Field)).ToArray();
    }

    private static HistoricalQueryRow ProjectRow(
        DataQueryEngineeringDto definition,
        HistoricalQueryRow row)
    {
        var selected = definition.SelectedFields;
        if (selected is null || selected.Count == 0) return row;
        var wanted = selected.ToHashSet(StringComparer.Ordinal);
        return new HistoricalQueryRow(
            row.Cells
                .Where(x => wanted.Contains(x.Key))
                .ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal));
    }

    private static HistoricalQueryRow PointRow(HistoricalRetrievalPoint point) =>
        new(new Dictionary<string, HistoricalQueryValue>(StringComparer.Ordinal)
        {
            ["tag.id"] = HistoricalQueryValue.FromGuid(point.TagId),
            ["tag.path"] = point.TagPath is null
                ? HistoricalQueryValue.Null()
                : HistoricalQueryValue.FromString(point.TagPath),
            ["quality"] = point.Quality.HasValue
                ? HistoricalQueryValue.FromEnum(point.Quality.Value.ToString())
                : HistoricalQueryValue.Null(),
            ["value"] = point.Value,
            ["timestamp"] = HistoricalQueryValue.FromDateTime(point.TimestampUtc)
        });

    private static DataQueryExecutionProvenance MapProvenance(HistoricalRetrievalProvenance provenance) =>
        new(
            provenance.Kind switch
            {
                HistoricalRetrievalProvenanceKind.Measured => "measured",
                HistoricalRetrievalProvenanceKind.Interpolated => "interpolated",
                HistoricalRetrievalProvenanceKind.Held => "held",
                HistoricalRetrievalProvenanceKind.Gap => "gap",
                HistoricalRetrievalProvenanceKind.Aggregate => "aggregate",
                _ => "unknown"
            },
            provenance.SourceTimestampsUtc,
            provenance.BucketStartUtc,
            provenance.BucketEndUtc,
            provenance.SampleCount,
            provenance.GoodCount,
            provenance.UncertainCount,
            provenance.BadCount,
            provenance.DerivedQuality?.ToString(),
            provenance.Reason);

    private static HistoricalQueryValue ToHistoricalValue(DataQueryParameterValue value) => value.Type switch
    {
        DataQueryParameterType.String => HistoricalQueryValue.FromString(value.Value),
        DataQueryParameterType.Enum => HistoricalQueryValue.FromEnum(value.Value),
        DataQueryParameterType.Boolean => HistoricalQueryValue.FromBoolean(bool.Parse(value.Value)),
        DataQueryParameterType.Number => HistoricalQueryValue.FromNumber(
            double.Parse(value.Value, CultureInfo.InvariantCulture)),
        DataQueryParameterType.Int64 => HistoricalQueryValue.FromInt64(
            long.Parse(value.Value, CultureInfo.InvariantCulture)),
        DataQueryParameterType.DurationSeconds => HistoricalQueryValue.FromInt64(
            long.Parse(value.Value, CultureInfo.InvariantCulture)),
        DataQueryParameterType.Guid => HistoricalQueryValue.FromGuid(Guid.Parse(value.Value)),
        DataQueryParameterType.DateTime => HistoricalQueryValue.FromDateTime(ParseUtc(value)),
        _ => throw new HistoricalQueryValidationException(
            $"Data Query parameter type '{value.Type}' cannot bind a Historical Query filter.")
    };

    private static DateTimeOffset ParseUtc(DataQueryParameterValue value)
    {
        if (value.Type != DataQueryParameterType.DateTime ||
            !DateTimeOffset.TryParse(
                value.Value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var timestamp) ||
            timestamp.Offset != TimeSpan.Zero)
            throw new HistoricalQueryValidationException("Data Query dateTime parameter must be canonical UTC.");
        return timestamp.ToUniversalTime();
    }

    private static string FirstLine(string message)
    {
        var end = message.IndexOfAny(['\r', '\n']);
        return end < 0 ? message : message[..end];
    }
}
