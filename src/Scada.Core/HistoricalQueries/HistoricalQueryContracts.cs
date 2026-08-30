using System.Globalization;

namespace Scada.Core.HistoricalQueries;

public static class HistoricalDatasets
{
    public const string HistorianSamples = "historian.samples";
    public const string AlarmEvents = "alarm.events";
}

public enum HistoricalFieldType
{
    Guid,
    String,
    Enum,
    Number,
    Boolean,
    DateTime,
    Int64,
    Scalar
}

public enum HistoricalValueKind
{
    Guid,
    String,
    Enum,
    Number,
    Boolean,
    DateTime,
    Int64,
    Null
}

public enum HistoricalFilterOperator
{
    Eq,
    NotEq,
    In,
    Contains,
    StartsWith,
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual
}

public enum HistoricalSortDirection
{
    Ascending,
    Descending
}

public sealed record HistoricalQueryValue(HistoricalValueKind Kind, string? Value)
{
    public static HistoricalQueryValue FromGuid(Guid value) => new(HistoricalValueKind.Guid, value.ToString("D"));
    public static HistoricalQueryValue FromString(string value) => new(HistoricalValueKind.String, value);
    public static HistoricalQueryValue FromEnum(string value) => new(HistoricalValueKind.Enum, value);

    public static HistoricalQueryValue FromNumber(double value)
    {
        if (!double.IsFinite(value))
            throw new ArgumentOutOfRangeException(nameof(value), "Historical numeric values must be finite.");
        return new(HistoricalValueKind.Number, value.ToString("R", CultureInfo.InvariantCulture));
    }

    public static HistoricalQueryValue FromBoolean(bool value) => new(HistoricalValueKind.Boolean, value ? "true" : "false");
    public static HistoricalQueryValue FromDateTime(DateTimeOffset value) =>
        new(HistoricalValueKind.DateTime, value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
    public static HistoricalQueryValue FromInt64(long value) =>
        new(HistoricalValueKind.Int64, value.ToString(CultureInfo.InvariantCulture));
    public static HistoricalQueryValue Null() => new(HistoricalValueKind.Null, null);

    public Guid AsGuid() => Guid.Parse(Value ?? throw new InvalidOperationException("Historical value is null."));
    public double AsNumber() => double.Parse(Value ?? throw new InvalidOperationException("Historical value is null."), CultureInfo.InvariantCulture);
    public bool AsBoolean() => bool.Parse(Value ?? throw new InvalidOperationException("Historical value is null."));
    public DateTimeOffset AsDateTime() => DateTimeOffset.Parse(
        Value ?? throw new InvalidOperationException("Historical value is null."),
        CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind);
    public long AsInt64() => long.Parse(Value ?? throw new InvalidOperationException("Historical value is null."), CultureInfo.InvariantCulture);

    internal string CanonicalText() => $"{Kind}:{Value ?? "<null>"}";
}

public sealed record HistoricalTimeRange(
    DateTimeOffset? FromUtc = null,
    DateTimeOffset? ToUtc = null,
    string? RelativePreset = null);

public sealed record HistoricalFilter(
    string Field,
    HistoricalFilterOperator Operator,
    IReadOnlyList<HistoricalQueryValue> Values);

public sealed record HistoricalSort(
    string Field = "timestamp",
    HistoricalSortDirection Direction = HistoricalSortDirection.Descending);

public sealed record HistoricalPageRequest(int Size = 100, string? Cursor = null);

public sealed record HistoricalQueryRequest(
    string Dataset,
    HistoricalTimeRange Range,
    IReadOnlyList<HistoricalFilter>? Filters = null,
    string? Search = null,
    HistoricalSort? Sort = null,
    HistoricalPageRequest? Page = null);

public sealed record HistoricalColumn(
    string Field,
    HistoricalFieldType Type,
    bool Filterable,
    bool Sortable,
    bool Searchable);

public sealed record HistoricalQueryRow(IReadOnlyDictionary<string, HistoricalQueryValue> Cells);

public sealed record HistoricalQueryResponse(
    string Dataset,
    IReadOnlyList<HistoricalColumn> Columns,
    IReadOnlyList<HistoricalQueryRow> Rows,
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    string? NextCursor,
    int PageSize);

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
    public HistoricalColumn ToColumn() => new(Field, Type, Operators.Count > 0, Sortable, Searchable);
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
