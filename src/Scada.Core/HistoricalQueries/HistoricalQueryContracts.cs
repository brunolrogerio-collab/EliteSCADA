using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

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
        if (!double.IsFinite(value)) throw new ArgumentOutOfRangeException(nameof(value), "Historical numeric values must be finite.");
        return new(HistoricalValueKind.Number, value.ToString("R", CultureInfo.InvariantCulture));
    }
    public static HistoricalQueryValue FromBoolean(bool value) => new(HistoricalValueKind.Boolean, value ? "true" : "false");
    public static HistoricalQueryValue FromDateTime(DateTimeOffset value) =>
        new(HistoricalValueKind.DateTime, value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
    public static HistoricalQueryValue FromInt64(long value) => new(HistoricalValueKind.Int64, value.ToString(CultureInfo.InvariantCulture));
    public static HistoricalQueryValue Null() => new(HistoricalValueKind.Null, null);

    public Guid AsGuid() => Guid.Parse(Value ?? throw new InvalidOperationException("Historical value is null."));
    public double AsNumber() => double.Parse(Value ?? throw new InvalidOperationException("Historical value is null."), CultureInfo.InvariantCulture);
    public bool AsBoolean() => bool.Parse(Value ?? throw new InvalidOperationException("Historical value is null."));
    public DateTimeOffset AsDateTime() => DateTimeOffset.Parse(Value ?? throw new InvalidOperationException("Historical value is null."), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
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

public sealed record HistoricalPageRequest(
    int Size = 100,
    string? Cursor = null);

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
    Task<HistoricalProviderPage> QueryAsync(HistoricalQueryExecution query, CancellationToken cancellationToken = default);
}

public enum HistoricalAuthorizationOutcome
{
    Allowed,
    Unauthenticated,
    Forbidden
}

public sealed record HistoricalAuthorizationDecision(HistoricalAuthorizationOutcome Outcome, string Reason)
{
    public static HistoricalAuthorizationDecision Allow(string reason = "allowed") => new(HistoricalAuthorizationOutcome.Allowed, reason);
    public static HistoricalAuthorizationDecision Unauthenticated(string reason = "authentication required") => new(HistoricalAuthorizationOutcome.Unauthenticated, reason);
    public static HistoricalAuthorizationDecision Forbid(string reason = "historical dataset access denied") => new(HistoricalAuthorizationOutcome.Forbidden, reason);
}

public interface IHistoricalQueryAuthorizer
{
    ValueTask<HistoricalAuthorizationDecision> AuthorizeAsync(string dataset, CancellationToken cancellationToken = default);
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
    public IReadOnlyList<HistoricalColumn> Columns => Fields.Values.Select(static field => field.ToColumn()).ToArray();
}

public static class HistoricalQueryCatalog
{
    private static readonly IReadOnlySet<HistoricalFilterOperator> IdentityOperators = Set(HistoricalFilterOperator.Eq, HistoricalFilterOperator.NotEq, HistoricalFilterOperator.In);
    private static readonly IReadOnlySet<HistoricalFilterOperator> StringOperators = Set(HistoricalFilterOperator.Eq, HistoricalFilterOperator.NotEq, HistoricalFilterOperator.In, HistoricalFilterOperator.Contains, HistoricalFilterOperator.StartsWith);
    private static readonly IReadOnlySet<HistoricalFilterOperator> OrderedOperators = Set(HistoricalFilterOperator.Eq, HistoricalFilterOperator.NotEq, HistoricalFilterOperator.In, HistoricalFilterOperator.GreaterThan, HistoricalFilterOperator.GreaterThanOrEqual, HistoricalFilterOperator.LessThan, HistoricalFilterOperator.LessThanOrEqual);

    private static readonly IReadOnlyDictionary<string, HistoricalDatasetDefinition> Definitions =
        new Dictionary<string, HistoricalDatasetDefinition>(StringComparer.Ordinal)
        {
            [HistoricalDatasets.HistorianSamples] = Dataset(
                HistoricalDatasets.HistorianSamples,
                new HistoricalFieldDefinition("tag.id", HistoricalFieldType.Guid, IdentityOperators),
                new HistoricalFieldDefinition("tag.path", HistoricalFieldType.String, StringOperators, sortable: true, searchable: true),
                new HistoricalFieldDefinition("quality", HistoricalFieldType.Enum, IdentityOperators, sortable: true),
                new HistoricalFieldDefinition("value", HistoricalFieldType.Scalar, IdentityOperators),
                new HistoricalFieldDefinition("timestamp", HistoricalFieldType.DateTime, OrderedOperators, sortable: true)),
            [HistoricalDatasets.AlarmEvents] = Dataset(
                HistoricalDatasets.AlarmEvents,
                new HistoricalFieldDefinition("alarm.id", HistoricalFieldType.Guid, IdentityOperators),
                new HistoricalFieldDefinition("tag.id", HistoricalFieldType.Guid, IdentityOperators),
                new HistoricalFieldDefinition("tag.path", HistoricalFieldType.String, StringOperators, sortable: true, searchable: true),
                new HistoricalFieldDefinition("state", HistoricalFieldType.Enum, IdentityOperators, sortable: true),
                new HistoricalFieldDefinition("priority", HistoricalFieldType.Number, OrderedOperators, sortable: true),
                new HistoricalFieldDefinition("message", HistoricalFieldType.String, StringOperators, searchable: true),
                new HistoricalFieldDefinition("timestamp", HistoricalFieldType.DateTime, OrderedOperators, sortable: true))
        };

    public static HistoricalDatasetDefinition Require(string dataset)
    {
        if (string.IsNullOrWhiteSpace(dataset) || !Definitions.TryGetValue(dataset.Trim(), out var definition))
            throw new ArgumentException("Historical dataset is not allowlisted.", nameof(dataset));
        return definition;
    }

    private static HistoricalDatasetDefinition Dataset(string id, params HistoricalFieldDefinition[] fields) =>
        new(id, fields.ToDictionary(static field => field.Field, StringComparer.Ordinal), "timestamp", HistoricalSortDirection.Descending);

    private static IReadOnlySet<HistoricalFilterOperator> Set(params HistoricalFilterOperator[] operators) => operators.ToHashSet();
}

public static class HistoricalQueryValidator
{
    public const int DefaultPageSize = 100;
    public const int MaximumPageSize = 200;
    public const int MaximumFilterCount = 32;
    public const int MaximumFilterValues = 64;
    public const int MaximumSearchLength = 200;
    public static readonly TimeSpan MaximumAbsoluteRange = TimeSpan.FromDays(31);

    private static readonly IReadOnlyDictionary<string, TimeSpan> RelativeRanges = new Dictionary<string, TimeSpan>(StringComparer.OrdinalIgnoreCase)
    {
        ["15m"] = TimeSpan.FromMinutes(15),
        ["1h"] = TimeSpan.FromHours(1),
        ["8h"] = TimeSpan.FromHours(8),
        ["24h"] = TimeSpan.FromHours(24),
        ["7d"] = TimeSpan.FromDays(7),
        ["30d"] = TimeSpan.FromDays(30)
    };

    public static HistoricalValidatedRequest Validate(HistoricalQueryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Range);

        var dataset = HistoricalQueryCatalog.Require(request.Dataset);
        var filters = NormalizeFilters(dataset, request.Filters ?? Array.Empty<HistoricalFilter>());
        var search = NormalizeSearch(dataset, request.Search);
        var sort = NormalizeSort(dataset, request.Sort);
        var pageSize = request.Page?.Size ?? DefaultPageSize;
        if (pageSize is < 1 or > MaximumPageSize)
            throw new ArgumentOutOfRangeException(nameof(request), $"Historical page size must be between 1 and {MaximumPageSize}.");

        ValidateRangeShape(request.Range);
        var fingerprint = ComputeFingerprint(dataset.Id, request.Range, filters, search, sort, pageSize);
        return new HistoricalValidatedRequest(dataset, request.Range, filters, search, sort, pageSize, request.Page?.Cursor, fingerprint);
    }

    public static HistoricalResolvedRange ResolveRange(HistoricalTimeRange range, DateTimeOffset nowUtc)
    {
        EnsureUtc(nowUtc, "Historical query clock");
        var now = nowUtc.ToUniversalTime();

        if (!string.IsNullOrWhiteSpace(range.RelativePreset))
        {
            if (!RelativeRanges.TryGetValue(range.RelativePreset.Trim(), out var duration))
                throw new ArgumentException("Historical relative range preset is not supported.", nameof(range));
            return new HistoricalResolvedRange(now - duration, now);
        }

        var from = range.FromUtc!.Value;
        var to = range.ToUtc!.Value;
        EnsureUtc(from, "Historical FromUtc");
        EnsureUtc(to, "Historical ToUtc");
        if (from >= to)
            throw new ArgumentException("Historical FromUtc must be earlier than ToUtc.", nameof(range));
        if (to > now)
            throw new ArgumentException("Historical ToUtc cannot be in the future.", nameof(range));
        if (to - from > MaximumAbsoluteRange)
            throw new ArgumentException($"Historical absolute range cannot exceed {MaximumAbsoluteRange.TotalDays:0} days.", nameof(range));
        return new HistoricalResolvedRange(from, to);
    }

    public static void ValidateResolvedRange(HistoricalResolvedRange range, DateTimeOffset nowUtc)
    {
        EnsureUtc(range.FromUtc, "Historical cursor FromUtc");
        EnsureUtc(range.ToUtc, "Historical cursor ToUtc");
        EnsureUtc(nowUtc, "Historical query clock");
        if (range.FromUtc >= range.ToUtc || range.ToUtc > nowUtc || range.ToUtc - range.FromUtc > MaximumAbsoluteRange)
            throw new HistoricalQueryCursorException("Historical cursor contains an invalid or stale time range.");
    }

    private static void ValidateRangeShape(HistoricalTimeRange range)
    {
        var hasRelative = !string.IsNullOrWhiteSpace(range.RelativePreset);
        var hasAbsolute = range.FromUtc.HasValue || range.ToUtc.HasValue;
        if (hasRelative == hasAbsolute)
            throw new ArgumentException("Historical range must use exactly one relative preset or an absolute FromUtc/ToUtc pair.", nameof(range));
        if (hasAbsolute && (!range.FromUtc.HasValue || !range.ToUtc.HasValue))
            throw new ArgumentException("Historical absolute range requires both FromUtc and ToUtc.", nameof(range));
        if (hasRelative && !RelativeRanges.ContainsKey(range.RelativePreset!.Trim()))
            throw new ArgumentException("Historical relative range preset is not supported.", nameof(range));
    }

    private static IReadOnlyList<HistoricalFilter> NormalizeFilters(HistoricalDatasetDefinition dataset, IReadOnlyList<HistoricalFilter> filters)
    {
        if (filters.Count > MaximumFilterCount)
            throw new ArgumentException($"Historical query cannot contain more than {MaximumFilterCount} filters.", nameof(filters));

        var normalized = new List<HistoricalFilter>(filters.Count);
        foreach (var filter in filters)
        {
            if (filter is null) throw new ArgumentException("Historical filter cannot be null.", nameof(filters));
            var field = filter.Field?.Trim() ?? string.Empty;
            if (!dataset.Fields.TryGetValue(field, out var definition))
                throw new ArgumentException($"Historical field '{field}' is not allowlisted for dataset '{dataset.Id}'.", nameof(filters));
            if (!definition.Operators.Contains(filter.Operator))
                throw new ArgumentException($"Operator '{filter.Operator}' is not allowed for historical field '{field}'.", nameof(filters));
            if (filter.Values is null || filter.Values.Count == 0 || filter.Values.Count > MaximumFilterValues)
                throw new ArgumentException($"Historical filter '{field}' must contain between 1 and {MaximumFilterValues} values.", nameof(filters));
            if (filter.Operator != HistoricalFilterOperator.In && filter.Values.Count != 1)
                throw new ArgumentException($"Historical operator '{filter.Operator}' requires exactly one value.", nameof(filters));

            var values = filter.Values.Select(value => NormalizeValue(definition.Type, value)).ToArray();
            normalized.Add(new HistoricalFilter(field, filter.Operator, values));
        }
        return normalized;
    }

    private static string? NormalizeSearch(HistoricalDatasetDefinition dataset, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return null;
        if (!dataset.Fields.Values.Any(static field => field.Searchable))
            throw new ArgumentException($"Historical dataset '{dataset.Id}' does not support search.", nameof(search));
        var normalized = search.Trim();
        if (normalized.Length > MaximumSearchLength)
            throw new ArgumentException($"Historical search cannot exceed {MaximumSearchLength} characters.", nameof(search));
        return normalized;
    }

    private static HistoricalSort NormalizeSort(HistoricalDatasetDefinition dataset, HistoricalSort? sort)
    {
        var candidate = sort ?? new HistoricalSort(dataset.DefaultSortField, dataset.DefaultSortDirection);
        var field = candidate.Field?.Trim() ?? string.Empty;
        if (!dataset.Fields.TryGetValue(field, out var definition) || !definition.Sortable)
            throw new ArgumentException($"Historical sort field '{field}' is not allowlisted for dataset '{dataset.Id}'.", nameof(sort));
        return new HistoricalSort(field, candidate.Direction);
    }

    private static HistoricalQueryValue NormalizeValue(HistoricalFieldType fieldType, HistoricalQueryValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Kind == HistoricalValueKind.Null)
            throw new ArgumentException("Historical filter values cannot be null.", nameof(value));
        var text = value.Value ?? throw new ArgumentException("Historical filter value text is required.", nameof(value));
        if (text.Length > 1000) throw new ArgumentException("Historical filter value is too long.", nameof(value));

        return fieldType switch
        {
            HistoricalFieldType.Guid when value.Kind == HistoricalValueKind.Guid && Guid.TryParse(text, out var guid) => HistoricalQueryValue.FromGuid(guid),
            HistoricalFieldType.String when value.Kind == HistoricalValueKind.String => HistoricalQueryValue.FromString(text),
            HistoricalFieldType.Enum when value.Kind is HistoricalValueKind.Enum or HistoricalValueKind.String => HistoricalQueryValue.FromEnum(text.Trim()),
            HistoricalFieldType.Number when value.Kind == HistoricalValueKind.Number && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number) => HistoricalQueryValue.FromNumber(number),
            HistoricalFieldType.Boolean when value.Kind == HistoricalValueKind.Boolean && bool.TryParse(text, out var boolean) => HistoricalQueryValue.FromBoolean(boolean),
            HistoricalFieldType.DateTime when value.Kind == HistoricalValueKind.DateTime && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var timestamp) && timestamp.Offset == TimeSpan.Zero => HistoricalQueryValue.FromDateTime(timestamp),
            HistoricalFieldType.Int64 when value.Kind == HistoricalValueKind.Int64 && long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer) => HistoricalQueryValue.FromInt64(integer),
            HistoricalFieldType.Scalar when value.Kind is HistoricalValueKind.String or HistoricalValueKind.Number or HistoricalValueKind.Boolean or HistoricalValueKind.Int64 or HistoricalValueKind.Enum => NormalizeScalar(value),
            _ => throw new ArgumentException($"Historical filter value kind '{value.Kind}' does not match field type '{fieldType}'.", nameof(value))
        };
    }

    private static HistoricalQueryValue NormalizeScalar(HistoricalQueryValue value) => value.Kind switch
    {
        HistoricalValueKind.String => HistoricalQueryValue.FromString(value.Value!),
        HistoricalValueKind.Enum => HistoricalQueryValue.FromEnum(value.Value!.Trim()),
        HistoricalValueKind.Number when double.TryParse(value.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number) => HistoricalQueryValue.FromNumber(number),
        HistoricalValueKind.Boolean when bool.TryParse(value.Value, out var boolean) => HistoricalQueryValue.FromBoolean(boolean),
        HistoricalValueKind.Int64 when long.TryParse(value.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer) => HistoricalQueryValue.FromInt64(integer),
        _ => throw new ArgumentException("Historical scalar filter value is invalid.", nameof(value))
    };

    private static string ComputeFingerprint(
        string dataset,
        HistoricalTimeRange range,
        IReadOnlyList<HistoricalFilter> filters,
        string? search,
        HistoricalSort sort,
        int pageSize)
    {
        var builder = new StringBuilder();
        builder.Append("v1|").Append(dataset).Append('|');
        if (!string.IsNullOrWhiteSpace(range.RelativePreset))
            builder.Append("relative:").Append(range.RelativePreset!.Trim().ToLowerInvariant());
        else
            builder.Append("absolute:").Append(range.FromUtc!.Value.UtcTicks).Append(':').Append(range.ToUtc!.Value.UtcTicks);
        builder.Append("|search:").Append(search ?? string.Empty)
            .Append("|sort:").Append(sort.Field).Append(':').Append((int)sort.Direction)
            .Append("|page:").Append(pageSize);

        foreach (var filter in filters
                     .OrderBy(static filter => filter.Field, StringComparer.Ordinal)
                     .ThenBy(static filter => filter.Operator))
        {
            builder.Append("|f:").Append(filter.Field).Append(':').Append((int)filter.Operator).Append(':');
            foreach (var value in filter.Values.Select(static value => value.CanonicalText()).OrderBy(static value => value, StringComparer.Ordinal))
                builder.Append(value).Append(',');
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexString(hash);
    }

    private static void EnsureUtc(DateTimeOffset value, string label)
    {
        if (value.Offset != TimeSpan.Zero)
            throw new ArgumentException($"{label} must use UTC offset +00:00.");
    }
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

public sealed class HistoricalQueryCursorCodec
{
    private const int Version = 1;
    private const int MaximumCursorLength = 4096;
    private readonly byte[] _key;

    public HistoricalQueryCursorCodec(ReadOnlySpan<byte> key)
    {
        if (key.Length < 32) throw new ArgumentException("Historical cursor key must contain at least 32 bytes.", nameof(key));
        _key = key.ToArray();
    }

    public string Encode(string dataset, string fingerprint, HistoricalResolvedRange range, HistoricalSort sort, HistoricalQueryPosition position)
    {
        var payload = new CursorPayload(
            Version,
            dataset,
            fingerprint,
            range.FromUtc.UtcTicks,
            range.ToUtc.UtcTicks,
            sort.Field,
            sort.Direction,
            position.Primary.Kind,
            position.Primary.Value,
            position.TimestampUtc.UtcTicks,
            position.TieBreaker);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload);
        var signature = HMACSHA256.HashData(_key, bytes);
        return $"{Base64Url(bytes)}.{Base64Url(signature)}";
    }

    public HistoricalDecodedCursor Decode(string cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor) || cursor.Length > MaximumCursorLength)
            throw new HistoricalQueryCursorException("Historical cursor is missing or exceeds the supported size.");
        var parts = cursor.Split('.', StringSplitOptions.None);
        if (parts.Length != 2)
            throw new HistoricalQueryCursorException("Historical cursor format is invalid.");

        byte[] payloadBytes;
        byte[] suppliedSignature;
        try
        {
            payloadBytes = FromBase64Url(parts[0]);
            suppliedSignature = FromBase64Url(parts[1]);
        }
        catch (FormatException ex)
        {
            throw new HistoricalQueryCursorException($"Historical cursor encoding is invalid: {ex.Message}");
        }

        var expectedSignature = HMACSHA256.HashData(_key, payloadBytes);
        if (suppliedSignature.Length != expectedSignature.Length || !CryptographicOperations.FixedTimeEquals(suppliedSignature, expectedSignature))
            throw new HistoricalQueryCursorException("Historical cursor signature is invalid.");

        CursorPayload payload;
        try
        {
            payload = JsonSerializer.Deserialize<CursorPayload>(payloadBytes)
                ?? throw new JsonException("Cursor payload is empty.");
        }
        catch (JsonException ex)
        {
            throw new HistoricalQueryCursorException($"Historical cursor payload is invalid: {ex.Message}");
        }

        if (payload.Version != Version || string.IsNullOrWhiteSpace(payload.Dataset) || string.IsNullOrWhiteSpace(payload.Fingerprint) ||
            string.IsNullOrWhiteSpace(payload.SortField) || string.IsNullOrWhiteSpace(payload.TieBreaker))
            throw new HistoricalQueryCursorException("Historical cursor payload is incomplete or unsupported.");

        DateTimeOffset from;
        DateTimeOffset to;
        DateTimeOffset timestamp;
        try
        {
            from = new DateTimeOffset(payload.FromUtcTicks, TimeSpan.Zero);
            to = new DateTimeOffset(payload.ToUtcTicks, TimeSpan.Zero);
            timestamp = new DateTimeOffset(payload.TimestampUtcTicks, TimeSpan.Zero);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            throw new HistoricalQueryCursorException($"Historical cursor time value is invalid: {ex.Message}");
        }

        return new HistoricalDecodedCursor(
            payload.Dataset,
            payload.Fingerprint,
            new HistoricalResolvedRange(from, to),
            new HistoricalSort(payload.SortField, payload.Direction),
            new HistoricalQueryPosition(new HistoricalQueryValue(payload.PrimaryKind, payload.PrimaryValue), timestamp, payload.TieBreaker));
    }

    private static string Base64Url(ReadOnlySpan<byte> bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromBase64Url(string value)
    {
        var normalized = value.Replace('-', '+').Replace('_', '/');
        normalized += normalized.Length % 4 switch { 2 => "==", 3 => "=", 0 => string.Empty, _ => throw new FormatException("Invalid base64url length.") };
        return Convert.FromBase64String(normalized);
    }

    private sealed record CursorPayload(
        int Version,
        string Dataset,
        string Fingerprint,
        long FromUtcTicks,
        long ToUtcTicks,
        string SortField,
        HistoricalSortDirection Direction,
        HistoricalValueKind PrimaryKind,
        string? PrimaryValue,
        long TimestampUtcTicks,
        string TieBreaker);
}

public sealed record HistoricalDecodedCursor(
    string Dataset,
    string Fingerprint,
    HistoricalResolvedRange Range,
    HistoricalSort Sort,
    HistoricalQueryPosition Position);

public sealed class HistoricalQueryService
{
    private readonly IReadOnlyDictionary<string, IHistoricalDatasetProvider> _providers;
    private readonly IHistoricalQueryAuthorizer _authorizer;
    private readonly HistoricalQueryCursorCodec _cursorCodec;
    private readonly Func<DateTimeOffset> _utcNow;

    public HistoricalQueryService(
        IEnumerable<IHistoricalDatasetProvider> providers,
        IHistoricalQueryAuthorizer authorizer,
        HistoricalQueryCursorCodec cursorCodec,
        Func<DateTimeOffset>? utcNow = null)
    {
        ArgumentNullException.ThrowIfNull(providers);
        _authorizer = authorizer ?? throw new ArgumentNullException(nameof(authorizer));
        _cursorCodec = cursorCodec ?? throw new ArgumentNullException(nameof(cursorCodec));
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);

        var materialized = providers.ToArray();
        if (materialized.Any(static provider => provider is null))
            throw new ArgumentException("Historical dataset provider collection cannot contain null entries.", nameof(providers));
        _providers = materialized.ToDictionary(static provider => provider.Dataset, StringComparer.Ordinal);
    }

    public async Task<HistoricalQueryResponse> QueryAsync(HistoricalQueryRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var validated = HistoricalQueryValidator.Validate(request);
        if (!_providers.TryGetValue(validated.Dataset.Id, out var provider))
            throw new InvalidOperationException($"Historical dataset provider '{validated.Dataset.Id}' is not configured.");

        var decision = await _authorizer.AuthorizeAsync(validated.Dataset.Id, cancellationToken);
        switch (decision.Outcome)
        {
            case HistoricalAuthorizationOutcome.Unauthenticated:
                throw new HistoricalQueryUnauthorizedException(decision.Reason);
            case HistoricalAuthorizationOutcome.Forbidden:
                throw new HistoricalQueryForbiddenException(decision.Reason);
            case HistoricalAuthorizationOutcome.Allowed:
                break;
            default:
                throw new HistoricalQueryForbiddenException("Historical authorization returned an unknown decision and failed closed.");
        }

        var now = _utcNow();
        HistoricalResolvedRange range;
        HistoricalQueryPosition? after = null;
        if (string.IsNullOrWhiteSpace(validated.Cursor))
        {
            range = HistoricalQueryValidator.ResolveRange(validated.RequestedRange, now);
        }
        else
        {
            var cursor = _cursorCodec.Decode(validated.Cursor);
            if (!string.Equals(cursor.Dataset, validated.Dataset.Id, StringComparison.Ordinal) ||
                !string.Equals(cursor.Fingerprint, validated.Fingerprint, StringComparison.Ordinal) ||
                cursor.Sort != validated.Sort)
                throw new HistoricalQueryCursorException("Historical cursor does not belong to this dataset/query/sort.");
            HistoricalQueryValidator.ValidateResolvedRange(cursor.Range, now);
            range = cursor.Range;
            after = cursor.Position;
        }

        var execution = new HistoricalQueryExecution(
            validated.Dataset,
            range,
            validated.Filters,
            validated.Search,
            validated.Sort,
            validated.PageSize,
            after);
        var page = await provider.QueryAsync(execution, cancellationToken);
        if (page.Rows.Count > validated.PageSize)
            throw new InvalidOperationException("Historical dataset provider exceeded the validated page size.");

        var nextCursor = page.NextPosition is null
            ? null
            : _cursorCodec.Encode(validated.Dataset.Id, validated.Fingerprint, range, validated.Sort, page.NextPosition);

        return new HistoricalQueryResponse(
            validated.Dataset.Id,
            validated.Dataset.Columns,
            page.Rows,
            range.FromUtc,
            range.ToUtc,
            nextCursor,
            validated.PageSize);
    }
}
