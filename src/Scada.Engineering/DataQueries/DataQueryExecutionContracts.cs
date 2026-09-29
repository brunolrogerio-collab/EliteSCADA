using System.Globalization;
using System.Text.Json.Serialization;
using Scada.Core.HistoricalQueries;
using Scada.Engineering.Contracts;

namespace Scada.Engineering.DataQueries;

[JsonConverter(typeof(JsonStringEnumConverter<DataQueryValueProvenance>))]
public enum DataQueryValueProvenance
{
    [JsonStringEnumMemberName("measured")] Measured,
    [JsonStringEnumMemberName("interpolated")] Interpolated,
    [JsonStringEnumMemberName("held")] Held,
    [JsonStringEnumMemberName("gap")] Gap,
    [JsonStringEnumMemberName("aggregate")] Aggregate
}

public sealed record DataQueryValueResult(
    Guid TagId,
    string? TagPath,
    DateTimeOffset TimestampUtc,
    HistoricalQueryValue? Value,
    string? Quality,
    DataQueryValueProvenance Provenance,
    DateTimeOffset? SourceTimestampUtc = null,
    DateTimeOffset? SecondarySourceTimestampUtc = null,
    DateTimeOffset? BucketEndExclusiveUtc = null,
    long? SampleCount = null,
    long? GoodCount = null,
    long? UncertainCount = null,
    long? BadCount = null);

public sealed record DataQueryHistorianExecution(
    DataQueryEngineeringDto Definition,
    HistoricalResolvedRange Range,
    IReadOnlyList<HistoricalFilter> Filters,
    string? Search,
    HistorianRetrievalEngineeringDto Retrieval,
    int MaximumResultPoints,
    int MaximumSourceSamples);

public sealed record DataQueryHistorianResult(
    HistorianRetrievalMode Mode,
    HistoricalResolvedRange Range,
    IReadOnlyList<DataQueryValueResult> Results);

public interface IDataQueryHistorianRetrievalProvider
{
    string ProviderKey { get; }

    Task<DataQueryHistorianResult> ExecuteAsync(
        DataQueryHistorianExecution execution,
        CancellationToken cancellationToken = default);
}

public sealed record BoundDataQuery(
    HistoricalQueryRequest Query,
    HistorianRetrievalEngineeringDto Retrieval);

public static class DataQueryParameterBinder
{
    public static BoundDataQuery Bind(
        DataQueryEngineeringDto definition,
        IReadOnlyDictionary<string, DataQueryParameterValue>? supplied)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var problems = DataQueryEngineeringValidation.Validate(definition);
        if (problems.Count > 0)
            throw new ArgumentException(string.Join(" ", problems.Select(static problem => problem.Message)), nameof(definition));

        var suppliedValues = supplied ?? new Dictionary<string, DataQueryParameterValue>(StringComparer.OrdinalIgnoreCase);
        var parameters = (definition.Parameters ?? Array.Empty<DataQueryParameterEngineeringDto>())
            .ToDictionary(static parameter => parameter.Key, StringComparer.OrdinalIgnoreCase);
        var effective = new Dictionary<string, DataQueryParameterValue>(StringComparer.OrdinalIgnoreCase);

        foreach (var parameter in parameters.Values)
        {
            suppliedValues.TryGetValue(parameter.Key, out var value);
            value ??= parameter.DefaultValue;
            if (value is null) continue;
            if (value.Type != parameter.Type)
                throw new ArgumentException(
                    $"Parameter '{parameter.Key}' requires type '{parameter.Type}', received '{value.Type}'.",
                    nameof(supplied));
            _ = ToHistoricalValue(value);
            if (parameter.AllowedValues is { Count: > 0 } &&
                !parameter.AllowedValues.Any(allowed =>
                    allowed.Type == value.Type &&
                    string.Equals(allowed.Value, value.Value, StringComparison.Ordinal)))
                throw new ArgumentException(
                    $"Parameter '{parameter.Key}' value is not in its allowed-value set.",
                    nameof(supplied));
            effective[parameter.Key] = value;
        }

        foreach (var key in suppliedValues.Keys)
        {
            if (!parameters.ContainsKey(key))
                throw new ArgumentException($"Unknown Data Query parameter '{key}'.", nameof(supplied));
        }

        var query = definition.Query with
        {
            Filters = (definition.Query.Filters ?? Array.Empty<HistoricalFilter>())
                .Select(static filter => filter with { Values = filter.Values.ToArray() })
                .ToArray(),
            OrderBy = definition.Query.OrderBy?.ToArray(),
            Page = definition.Query.Page is null
                ? null
                : definition.Query.Page with { Cursor = null }
        };
        var retrieval = definition.HistorianRetrieval ?? new HistorianRetrievalEngineeringDto();
        var filters = (query.Filters ?? Array.Empty<HistoricalFilter>()).ToArray();

        foreach (var binding in definition.ParameterBindings ?? Array.Empty<DataQueryParameterBindingEngineeringDto>())
        {
            if (!effective.TryGetValue(binding.ParameterKey, out var value))
                throw new ArgumentException(
                    $"Parameter '{binding.ParameterKey}' requires a value for bound target '{binding.Target}'.",
                    nameof(supplied));

            switch (binding.Target)
            {
                case DataQueryParameterTarget.AbsoluteFromUtc:
                {
                    if (query.Range.Kind != HistoricalTimeRangeKind.Absolute)
                        throw new ArgumentException("absoluteFromUtc binding requires an absolute Historical Query range.", nameof(definition));
                    query = query with
                    {
                        Range = query.Range with { FromUtc = ParseUtc(value, binding.ParameterKey) }
                    };
                    break;
                }
                case DataQueryParameterTarget.AbsoluteToUtc:
                {
                    if (query.Range.Kind != HistoricalTimeRangeKind.Absolute)
                        throw new ArgumentException("absoluteToUtc binding requires an absolute Historical Query range.", nameof(definition));
                    query = query with
                    {
                        Range = query.Range with { ToUtc = ParseUtc(value, binding.ParameterKey) }
                    };
                    break;
                }
                case DataQueryParameterTarget.RelativeDurationSeconds:
                {
                    if (query.Range.Kind != HistoricalTimeRangeKind.Relative)
                        throw new ArgumentException("relativeDurationSeconds binding requires a relative Historical Query range.", nameof(definition));
                    var seconds = ParsePositiveInt32(value, binding.ParameterKey);
                    query = query with { Range = HistoricalTimeRange.Relative(seconds) };
                    break;
                }
                case DataQueryParameterTarget.HistorianTargetUtc:
                    retrieval = retrieval with { TargetUtc = ParseUtc(value, binding.ParameterKey) };
                    break;
                case DataQueryParameterTarget.Search:
                    query = query with { Search = value.Value };
                    break;
                case DataQueryParameterTarget.FilterValue:
                {
                    var filterIndex = binding.FilterIndex!.Value;
                    var valueIndex = binding.ValueIndex!.Value;
                    if (filterIndex < 0 || filterIndex >= filters.Length)
                        throw new ArgumentOutOfRangeException(nameof(definition), $"FilterIndex {filterIndex} is outside the saved filter list.");
                    var values = filters[filterIndex].Values.ToArray();
                    if (valueIndex < 0 || valueIndex >= values.Length)
                        throw new ArgumentOutOfRangeException(nameof(definition), $"ValueIndex {valueIndex} is outside filter {filterIndex}.");
                    values[valueIndex] = ToHistoricalValue(value);
                    filters[filterIndex] = filters[filterIndex] with { Values = values };
                    query = query with { Filters = filters };
                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException(nameof(binding.Target));
            }
        }

        HistoricalQueryValidator.Validate(query);
        return new BoundDataQuery(query, retrieval);
    }

    private static DateTimeOffset ParseUtc(DataQueryParameterValue value, string key)
    {
        if (value.Type != DataQueryParameterType.DateTime ||
            !DateTimeOffset.TryParse(
                value.Value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var parsed) ||
            parsed.Offset != TimeSpan.Zero)
            throw new ArgumentException($"Parameter '{key}' must be an ISO-8601 UTC DateTime.");
        return parsed;
    }

    private static int ParsePositiveInt32(DataQueryParameterValue value, string key)
    {
        if (value.Type != DataQueryParameterType.DurationSeconds ||
            !int.TryParse(value.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ||
            parsed <= 0)
            throw new ArgumentException($"Parameter '{key}' must be a positive whole-second duration.");
        return parsed;
    }

    public static HistoricalQueryValue ToHistoricalValue(DataQueryParameterValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.Type switch
        {
            DataQueryParameterType.String => HistoricalQueryValue.FromString(value.Value),
            DataQueryParameterType.Enum => HistoricalQueryValue.FromEnum(value.Value),
            DataQueryParameterType.Boolean when bool.TryParse(value.Value, out var boolean) =>
                HistoricalQueryValue.FromBoolean(boolean),
            DataQueryParameterType.Number
                when double.TryParse(value.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) &&
                     double.IsFinite(number) =>
                HistoricalQueryValue.FromNumber(number),
            DataQueryParameterType.Int64
                when long.TryParse(value.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer) =>
                HistoricalQueryValue.FromInt64(integer),
            DataQueryParameterType.Guid when Guid.TryParse(value.Value, out var guid) =>
                HistoricalQueryValue.FromGuid(guid),
            DataQueryParameterType.DateTime =>
                HistoricalQueryValue.FromDateTime(ParseUtc(value, "value")),
            DataQueryParameterType.DurationSeconds
                when int.TryParse(value.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds) &&
                     seconds > 0 =>
                HistoricalQueryValue.FromInt32(seconds),
            _ => throw new ArgumentException(
                $"Data Query parameter value '{value.Value}' is invalid for type '{value.Type}'.",
                nameof(value))
        };
    }
}
