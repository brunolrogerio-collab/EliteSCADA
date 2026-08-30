using System.Globalization;
using System.Text.Json.Serialization;

namespace Scada.Core.HistoricalQueries;

public static class HistoricalQueryContract
{
    public const int Version = 1;
}

public static class HistoricalDatasets
{
    public const string HistorianSamples = "historian.samples";
    public const string AlarmEvents = "alarm.events";
}

[JsonConverter(typeof(JsonStringEnumConverter<HistoricalFieldType>))]
public enum HistoricalFieldType
{
    [JsonStringEnumMemberName("guid")] Guid,
    [JsonStringEnumMemberName("string")] String,
    [JsonStringEnumMemberName("enum")] Enum,
    [JsonStringEnumMemberName("number")] Number,
    [JsonStringEnumMemberName("boolean")] Boolean,
    [JsonStringEnumMemberName("dateTime")] DateTime,
    [JsonStringEnumMemberName("int64")] Int64,
    [JsonStringEnumMemberName("scalar")] Scalar
}

[JsonConverter(typeof(JsonStringEnumConverter<HistoricalValueKind>))]
public enum HistoricalValueKind
{
    [JsonStringEnumMemberName("guid")] Guid,
    [JsonStringEnumMemberName("string")] String,
    [JsonStringEnumMemberName("enum")] Enum,
    [JsonStringEnumMemberName("int16")] Int16,
    [JsonStringEnumMemberName("int32")] Int32,
    [JsonStringEnumMemberName("int64")] Int64,
    [JsonStringEnumMemberName("float")] Float,
    [JsonStringEnumMemberName("double")] Double,
    [JsonStringEnumMemberName("number")] Number,
    [JsonStringEnumMemberName("boolean")] Boolean,
    [JsonStringEnumMemberName("dateTime")] DateTime,
    [JsonStringEnumMemberName("null")] Null
}

[JsonConverter(typeof(JsonStringEnumConverter<HistoricalFilterOperator>))]
public enum HistoricalFilterOperator
{
    [JsonStringEnumMemberName("eq")] Eq,
    [JsonStringEnumMemberName("notEq")] NotEq,
    [JsonStringEnumMemberName("in")] In,
    [JsonStringEnumMemberName("contains")] Contains,
    [JsonStringEnumMemberName("startsWith")] StartsWith,
    [JsonStringEnumMemberName("gt")] GreaterThan,
    [JsonStringEnumMemberName("gte")] GreaterThanOrEqual,
    [JsonStringEnumMemberName("lt")] LessThan,
    [JsonStringEnumMemberName("lte")] LessThanOrEqual
}

[JsonConverter(typeof(JsonStringEnumConverter<HistoricalSortDirection>))]
public enum HistoricalSortDirection
{
    [JsonStringEnumMemberName("ascending")] Ascending,
    [JsonStringEnumMemberName("descending")] Descending
}

[JsonConverter(typeof(JsonStringEnumConverter<HistoricalTimeRangeKind>))]
public enum HistoricalTimeRangeKind
{
    [JsonStringEnumMemberName("absolute")] Absolute,
    [JsonStringEnumMemberName("relative")] Relative
}

[JsonConverter(typeof(JsonStringEnumConverter<HistoricalTimeAnchor>))]
public enum HistoricalTimeAnchor
{
    [JsonStringEnumMemberName("now")] Now
}

public sealed record HistoricalQueryValue(
    [property: JsonPropertyName("kind")] HistoricalValueKind Kind,
    [property: JsonPropertyName("value")] string? Value)
{
    public static HistoricalQueryValue FromGuid(Guid value) => new(HistoricalValueKind.Guid, value.ToString("D"));
    public static HistoricalQueryValue FromString(string value) => new(HistoricalValueKind.String, value);
    public static HistoricalQueryValue FromEnum(string value) => new(HistoricalValueKind.Enum, value);
    public static HistoricalQueryValue FromInt16(short value) =>
        new(HistoricalValueKind.Int16, value.ToString(CultureInfo.InvariantCulture));
    public static HistoricalQueryValue FromInt32(int value) =>
        new(HistoricalValueKind.Int32, value.ToString(CultureInfo.InvariantCulture));
    public static HistoricalQueryValue FromInt64(long value) =>
        new(HistoricalValueKind.Int64, value.ToString(CultureInfo.InvariantCulture));

    public static HistoricalQueryValue FromFloat(float value)
    {
        if (!float.IsFinite(value))
            throw new ArgumentOutOfRangeException(nameof(value), "Historical float values must be finite.");
        return new(HistoricalValueKind.Float, value.ToString("R", CultureInfo.InvariantCulture));
    }

    public static HistoricalQueryValue FromDouble(double value)
    {
        if (!double.IsFinite(value))
            throw new ArgumentOutOfRangeException(nameof(value), "Historical double values must be finite.");
        return new(HistoricalValueKind.Double, value.ToString("R", CultureInfo.InvariantCulture));
    }

    public static HistoricalQueryValue FromNumber(double value)
    {
        if (!double.IsFinite(value))
            throw new ArgumentOutOfRangeException(nameof(value), "Historical numeric values must be finite.");
        return new(HistoricalValueKind.Number, value.ToString("R", CultureInfo.InvariantCulture));
    }

    public static HistoricalQueryValue FromBoolean(bool value) =>
        new(HistoricalValueKind.Boolean, value ? "true" : "false");
    public static HistoricalQueryValue FromDateTime(DateTimeOffset value) =>
        new(HistoricalValueKind.DateTime, value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
    public static HistoricalQueryValue Null() => new(HistoricalValueKind.Null, null);

    public Guid AsGuid() => Guid.Parse(Value ?? throw new InvalidOperationException("Historical value is null."));
    public double AsNumber() => double.Parse(
        Value ?? throw new InvalidOperationException("Historical value is null."),
        CultureInfo.InvariantCulture);
    public bool AsBoolean() => bool.Parse(Value ?? throw new InvalidOperationException("Historical value is null."));
    public DateTimeOffset AsDateTime() => DateTimeOffset.Parse(
        Value ?? throw new InvalidOperationException("Historical value is null."),
        CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind);
    public long AsInt64() => long.Parse(
        Value ?? throw new InvalidOperationException("Historical value is null."),
        CultureInfo.InvariantCulture);

    internal string CanonicalText() => $"{Kind}:{Value ?? "<null>"}";
}

public sealed record HistoricalTimeRange(
    [property: JsonPropertyName("kind")] HistoricalTimeRangeKind Kind,
    [property: JsonPropertyName("fromUtc")] DateTimeOffset? FromUtc = null,
    [property: JsonPropertyName("toUtc")] DateTimeOffset? ToUtc = null,
    [property: JsonPropertyName("durationSeconds")] int? DurationSeconds = null,
    [property: JsonPropertyName("anchor")] HistoricalTimeAnchor? Anchor = null)
{
    public static HistoricalTimeRange Absolute(DateTimeOffset fromUtc, DateTimeOffset toUtc) =>
        new(HistoricalTimeRangeKind.Absolute, fromUtc, toUtc);

    public static HistoricalTimeRange Relative(int durationSeconds) =>
        new(
            HistoricalTimeRangeKind.Relative,
            DurationSeconds: durationSeconds,
            Anchor: HistoricalTimeAnchor.Now);
}

public sealed record HistoricalFilter(
    [property: JsonPropertyName("field")] string Field,
    [property: JsonPropertyName("operator")] HistoricalFilterOperator Operator,
    [property: JsonPropertyName("values")] IReadOnlyList<HistoricalQueryValue> Values);

public sealed record HistoricalSort(
    [property: JsonPropertyName("field")] string Field = "timestamp",
    [property: JsonPropertyName("direction")] HistoricalSortDirection Direction = HistoricalSortDirection.Descending);

public sealed record HistoricalPageRequest(
    [property: JsonPropertyName("limit")] int Size = 100,
    [property: JsonPropertyName("cursor")] string? Cursor = null);

public sealed record HistoricalQueryRequest(
    [property: JsonPropertyName("datasetKey")] string Dataset,
    [property: JsonPropertyName("timeRange")] HistoricalTimeRange Range,
    [property: JsonPropertyName("version")] int Version = HistoricalQueryContract.Version,
    [property: JsonPropertyName("filters")] IReadOnlyList<HistoricalFilter>? Filters = null,
    [property: JsonPropertyName("search")] string? Search = null,
    [property: JsonPropertyName("orderBy")] IReadOnlyList<HistoricalSort>? OrderBy = null,
    [property: JsonPropertyName("page")] HistoricalPageRequest? Page = null);

public sealed record HistoricalColumn(
    [property: JsonPropertyName("field")] string Field,
    [property: JsonPropertyName("type")] HistoricalFieldType Type,
    [property: JsonPropertyName("operators")] IReadOnlyList<HistoricalFilterOperator> Operators,
    [property: JsonPropertyName("filterable")] bool Filterable,
    [property: JsonPropertyName("sortable")] bool Sortable,
    [property: JsonPropertyName("searchable")] bool Searchable);

public sealed record HistoricalQueryRow(
    [property: JsonPropertyName("cells")] IReadOnlyDictionary<string, HistoricalQueryValue> Cells);

public sealed record HistoricalQueryResponse(
    [property: JsonPropertyName("version")] int Version,
    [property: JsonPropertyName("datasetKey")] string Dataset,
    [property: JsonPropertyName("columns")] IReadOnlyList<HistoricalColumn> Columns,
    [property: JsonPropertyName("rows")] IReadOnlyList<HistoricalQueryRow> Rows,
    [property: JsonPropertyName("fromUtc")] DateTimeOffset FromUtc,
    [property: JsonPropertyName("toUtc")] DateTimeOffset ToUtc,
    [property: JsonPropertyName("nextCursor")] string? NextCursor,
    [property: JsonPropertyName("pageSize")] int PageSize);

public sealed record HistoricalResolvedRange(DateTimeOffset FromUtc, DateTimeOffset ToUtc);

public sealed record HistoricalQueryPosition(
    HistoricalQueryValue Primary,
    DateTimeOffset TimestampUtc,
    string TieBreaker);

public sealed record HistoricalQueryExecution(
    HistoricalDatasetDefinition Dataset,
    HistoricalResolvedRange Range,
    IReadOnlyList<HistoricalFilter> Filters,
    string? Search,
    HistoricalSort Sort,
    int PageSize,
    HistoricalQueryPosition? After);

public sealed record HistoricalProviderPage(
    IReadOnlyList<HistoricalQueryRow> Rows,
    HistoricalQueryPosition? NextPosition);

public interface IHistoricalDatasetProvider
{
    string Dataset { get; }
    Task<HistoricalProviderPage> QueryAsync(
        HistoricalQueryExecution query,
        CancellationToken cancellationToken = default);
}

public enum HistoricalAuthorizationOutcome
{
    Allowed,
    Unauthenticated,
    Forbidden
}

public sealed record HistoricalAuthorizationDecision(HistoricalAuthorizationOutcome Outcome, string Reason)
{
    public static HistoricalAuthorizationDecision Allow(string reason = "allowed") =>
        new(HistoricalAuthorizationOutcome.Allowed, reason);
    public static HistoricalAuthorizationDecision Unauthenticated(string reason = "authentication required") =>
        new(HistoricalAuthorizationOutcome.Unauthenticated, reason);
    public static HistoricalAuthorizationDecision Forbid(string reason = "historical dataset access denied") =>
        new(HistoricalAuthorizationOutcome.Forbidden, reason);
}

public interface IHistoricalQueryAuthorizer
{
    ValueTask<HistoricalAuthorizationDecision> AuthorizeAsync(
        string dataset,
        CancellationToken cancellationToken = default);
}

public sealed class HistoricalQueryUnauthorizedException(string message) : InvalidOperationException(message);
public sealed class HistoricalQueryForbiddenException(string message) : InvalidOperationException(message);
public sealed class HistoricalQueryCursorException(string message) : ArgumentException(message);

public sealed record HistoricalFieldDefinition(
    string Field,
    HistoricalFieldType Type,
    IReadOnlySet<HistoricalFilterOperator> Operators,
    bool Sortable = false,
    bool Searchable = false)
{
    public HistoricalColumn ToColumn()
    {
        var operators = Operators.OrderBy(static value => (int)value).ToArray();
        return new(Field, Type, operators, operators.Length > 0, Sortable, Searchable);
    }
}

public sealed record HistoricalDatasetDefinition(
    string Id,
    IReadOnlyDictionary<string, HistoricalFieldDefinition> Fields,
    string DefaultSortField,
    HistoricalSortDirection DefaultSortDirection)
{
    public IReadOnlyList<HistoricalColumn> Columns =>
        Fields.Values.Select(static definition => definition.ToColumn()).ToArray();
}

public static class HistoricalQueryCatalog
{
    private static readonly IReadOnlySet<HistoricalFilterOperator> IdentityOperators = Set(
        HistoricalFilterOperator.Eq,
        HistoricalFilterOperator.NotEq,
        HistoricalFilterOperator.In);

    private static readonly IReadOnlySet<HistoricalFilterOperator> StringOperators = Set(
        HistoricalFilterOperator.Eq,
        HistoricalFilterOperator.NotEq,
        HistoricalFilterOperator.In,
        HistoricalFilterOperator.Contains,
        HistoricalFilterOperator.StartsWith);

    private static readonly IReadOnlySet<HistoricalFilterOperator> OrderedOperators = Set(
        HistoricalFilterOperator.Eq,
        HistoricalFilterOperator.NotEq,
        HistoricalFilterOperator.In,
        HistoricalFilterOperator.GreaterThan,
        HistoricalFilterOperator.GreaterThanOrEqual,
        HistoricalFilterOperator.LessThan,
        HistoricalFilterOperator.LessThanOrEqual);

    private static readonly IReadOnlyDictionary<string, HistoricalDatasetDefinition> Definitions =
        new Dictionary<string, HistoricalDatasetDefinition>(StringComparer.Ordinal)
        {
            [HistoricalDatasets.HistorianSamples] = Dataset(
                HistoricalDatasets.HistorianSamples,
                new("tag.id", HistoricalFieldType.Guid, IdentityOperators),
                new("tag.path", HistoricalFieldType.String, StringOperators, Searchable: true),
                new("quality", HistoricalFieldType.Enum, IdentityOperators),
                new("value", HistoricalFieldType.Scalar, IdentityOperators),
                new("timestamp", HistoricalFieldType.DateTime, OrderedOperators, Sortable: true)),
            [HistoricalDatasets.AlarmEvents] = Dataset(
                HistoricalDatasets.AlarmEvents,
                new("alarm.id", HistoricalFieldType.Guid, IdentityOperators),
                new("tag.id", HistoricalFieldType.Guid, IdentityOperators),
                new("tag.path", HistoricalFieldType.String, StringOperators, Sortable: true, Searchable: true),
                new("state", HistoricalFieldType.Enum, IdentityOperators, Sortable: true),
                new("priority", HistoricalFieldType.Number, OrderedOperators, Sortable: true),
                new("message", HistoricalFieldType.String, StringOperators, Searchable: true),
                new("timestamp", HistoricalFieldType.DateTime, OrderedOperators, Sortable: true))
        };

    public static HistoricalDatasetDefinition Require(string dataset)
    {
        if (string.IsNullOrWhiteSpace(dataset) ||
            !Definitions.TryGetValue(dataset.Trim(), out var definition))
            throw new ArgumentException("Historical dataset is not allowlisted.", nameof(dataset));
        return definition;
    }

    private static HistoricalDatasetDefinition Dataset(
        string id,
        params HistoricalFieldDefinition[] fields) =>
        new(
            id,
            fields.ToDictionary(static definition => definition.Field, StringComparer.Ordinal),
            "timestamp",
            HistoricalSortDirection.Descending);

    private static IReadOnlySet<HistoricalFilterOperator> Set(
        params HistoricalFilterOperator[] operators) => operators.ToHashSet();
}

public sealed record HistoricalValidatedRequest(
    HistoricalDatasetDefinition Dataset,
    HistoricalTimeRange RequestedRange,
    IReadOnlyList<HistoricalFilter> Filters,
    string? Search,
    HistoricalSort Sort,
    int PageSize,
    string? Cursor,
    string Fingerprint);
