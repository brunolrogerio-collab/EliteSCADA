using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Scada.Core.HistoricalQueries;

public static class HistoricalQueryValidator
{
    public const int DefaultPageSize = 100;
    public const int MaximumPageSize = 200;
    public const int MaximumFilterCount = 32;
    public const int MaximumFilterValues = 64;
    public const int MaximumSearchLength = 200;
    public const int MaximumOrderTerms = 1;
    public static readonly TimeSpan MaximumRange = TimeSpan.FromDays(31);
    public static readonly int MaximumRelativeDurationSeconds = checked((int)MaximumRange.TotalSeconds);

    public static HistoricalValidatedRequest Validate(HistoricalQueryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Range);

        if (request.Version != HistoricalQueryContract.Version)
            throw new ArgumentException(
                $"Historical Query version '{request.Version}' is unsupported. Expected version {HistoricalQueryContract.Version}.",
                nameof(request));

        var dataset = HistoricalQueryCatalog.Require(request.Dataset);
        ValidateRangeShape(request.Range);
        var filters = NormalizeFilters(dataset, request.Filters ?? Array.Empty<HistoricalFilter>());
        var search = NormalizeSearch(dataset, request.Search);
        var sort = NormalizeOrder(dataset, request.OrderBy);
        var pageSize = request.Page?.Size ?? DefaultPageSize;
        if (pageSize is < 1 or > MaximumPageSize)
            throw new ArgumentOutOfRangeException(
                nameof(request),
                $"Historical page size must be between 1 and {MaximumPageSize}.");

        var fingerprint = ComputeFingerprint(
            request.Version,
            dataset.Id,
            request.Range,
            filters,
            search,
            sort,
            pageSize);

        return new HistoricalValidatedRequest(
            dataset,
            request.Range,
            filters,
            search,
            sort,
            pageSize,
            request.Page?.Cursor,
            fingerprint);
    }

    public static HistoricalResolvedRange ResolveRange(
        HistoricalTimeRange range,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(range);
        EnsureUtc(nowUtc, "Historical query clock");
        ValidateRangeShape(range);
        var now = nowUtc.ToUniversalTime();

        if (range.Kind == HistoricalTimeRangeKind.Relative)
        {
            var duration = TimeSpan.FromSeconds(range.DurationSeconds!.Value);
            return new HistoricalResolvedRange(now - duration, now);
        }

        var from = range.FromUtc!.Value;
        var to = range.ToUtc!.Value;
        if (from >= to)
            throw new ArgumentException("Historical FromUtc must be earlier than ToUtc.", nameof(range));
        if (to > now)
            throw new ArgumentException("Historical ToUtc cannot be in the future.", nameof(range));
        if (to - from > MaximumRange)
            throw new ArgumentException(
                $"Historical absolute range cannot exceed {MaximumRange.TotalDays:0} days.",
                nameof(range));
        return new HistoricalResolvedRange(from, to);
    }

    public static void ValidateResolvedRange(
        HistoricalResolvedRange range,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(range);
        EnsureUtc(range.FromUtc, "Historical cursor FromUtc");
        EnsureUtc(range.ToUtc, "Historical cursor ToUtc");
        EnsureUtc(nowUtc, "Historical query clock");
        if (range.FromUtc >= range.ToUtc ||
            range.ToUtc > nowUtc ||
            range.ToUtc - range.FromUtc > MaximumRange)
            throw new HistoricalQueryCursorException(
                "Historical cursor contains an invalid or stale time range.");
    }

    private static void ValidateRangeShape(HistoricalTimeRange range)
    {
        switch (range.Kind)
        {
            case HistoricalTimeRangeKind.Absolute:
                if (!range.FromUtc.HasValue ||
                    !range.ToUtc.HasValue ||
                    range.DurationSeconds.HasValue ||
                    range.Anchor.HasValue)
                    throw new ArgumentException(
                        "Historical absolute range requires only FromUtc and ToUtc.",
                        nameof(range));
                EnsureUtc(range.FromUtc.Value, "Historical FromUtc");
                EnsureUtc(range.ToUtc.Value, "Historical ToUtc");
                break;

            case HistoricalTimeRangeKind.Relative:
                if (range.FromUtc.HasValue ||
                    range.ToUtc.HasValue ||
                    !range.DurationSeconds.HasValue ||
                    range.Anchor != HistoricalTimeAnchor.Now)
                    throw new ArgumentException(
                        "Historical relative range requires DurationSeconds and anchor=now and must not include absolute timestamps.",
                        nameof(range));
                if (range.DurationSeconds.Value is < 1 or > MaximumRelativeDurationSeconds)
                    throw new ArgumentOutOfRangeException(
                        nameof(range),
                        $"Historical relative duration must be between 1 and {MaximumRelativeDurationSeconds} seconds.");
                break;

            default:
                throw new ArgumentException("Historical time-range kind is unsupported.", nameof(range));
        }
    }

    private static IReadOnlyList<HistoricalFilter> NormalizeFilters(
        HistoricalDatasetDefinition dataset,
        IReadOnlyList<HistoricalFilter> filters)
    {
        if (filters.Count > MaximumFilterCount)
            throw new ArgumentException(
                $"Historical query cannot contain more than {MaximumFilterCount} filters.",
                nameof(filters));

        var normalized = new List<HistoricalFilter>(filters.Count);
        foreach (var filter in filters)
        {
            if (filter is null)
                throw new ArgumentException("Historical filter cannot be null.", nameof(filters));

            var field = filter.Field?.Trim() ?? string.Empty;
            if (!dataset.Fields.TryGetValue(field, out var definition))
                throw new ArgumentException(
                    $"Historical field '{field}' is not allowlisted for dataset '{dataset.Id}'.",
                    nameof(filters));
            if (!definition.Operators.Contains(filter.Operator))
                throw new ArgumentException(
                    $"Operator '{filter.Operator}' is not allowed for historical field '{field}'.",
                    nameof(filters));
            if (filter.Values is null || filter.Values.Count == 0 || filter.Values.Count > MaximumFilterValues)
                throw new ArgumentException(
                    $"Historical filter '{field}' must contain between 1 and {MaximumFilterValues} values.",
                    nameof(filters));
            if (filter.Operator != HistoricalFilterOperator.In && filter.Values.Count != 1)
                throw new ArgumentException(
                    $"Historical operator '{filter.Operator}' requires exactly one value.",
                    nameof(filters));

            var values = filter.Values.Select(value => NormalizeValue(definition.Type, value)).ToArray();
            normalized.Add(new HistoricalFilter(field, filter.Operator, values));
        }

        return normalized;
    }

    private static string? NormalizeSearch(
        HistoricalDatasetDefinition dataset,
        string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return null;
        if (!dataset.Fields.Values.Any(static field => field.Searchable))
            throw new ArgumentException(
                $"Historical dataset '{dataset.Id}' does not support search.",
                nameof(search));

        var normalized = search.Trim();
        if (normalized.Length > MaximumSearchLength)
            throw new ArgumentException(
                $"Historical search cannot exceed {MaximumSearchLength} characters.",
                nameof(search));
        return normalized;
    }

    private static HistoricalSort NormalizeOrder(
        HistoricalDatasetDefinition dataset,
        IReadOnlyList<HistoricalSort>? orderBy)
    {
        if (orderBy is null || orderBy.Count == 0)
            return new HistoricalSort(dataset.DefaultSortField, dataset.DefaultSortDirection);
        if (orderBy.Count > MaximumOrderTerms)
            throw new ArgumentException(
                $"Historical Query v1 currently supports at most {MaximumOrderTerms} order term.",
                nameof(orderBy));

        var candidate = orderBy[0]
            ?? throw new ArgumentException("Historical order term cannot be null.", nameof(orderBy));
        var field = candidate.Field?.Trim() ?? string.Empty;
        if (!dataset.Fields.TryGetValue(field, out var definition) || !definition.Sortable)
            throw new ArgumentException(
                $"Historical sort field '{field}' is not allowlisted for dataset '{dataset.Id}'.",
                nameof(orderBy));
        if (!Enum.IsDefined(candidate.Direction))
            throw new ArgumentException("Historical sort direction is invalid.", nameof(orderBy));
        return new HistoricalSort(field, candidate.Direction);
    }

    private static HistoricalQueryValue NormalizeValue(
        HistoricalFieldType fieldType,
        HistoricalQueryValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Kind == HistoricalValueKind.Null)
            throw new ArgumentException("Historical filter values cannot be null.", nameof(value));
        var text = value.Value
            ?? throw new ArgumentException("Historical filter value text is required.", nameof(value));
        if (text.Length > 1000)
            throw new ArgumentException("Historical filter value is too long.", nameof(value));

        return fieldType switch
        {
            HistoricalFieldType.Guid
                when value.Kind == HistoricalValueKind.Guid && Guid.TryParse(text, out var guid) =>
                HistoricalQueryValue.FromGuid(guid),

            HistoricalFieldType.String when value.Kind == HistoricalValueKind.String =>
                HistoricalQueryValue.FromString(text),

            HistoricalFieldType.Enum
                when value.Kind is HistoricalValueKind.Enum or HistoricalValueKind.String =>
                HistoricalQueryValue.FromEnum(text.Trim()),

            HistoricalFieldType.Number
                when IsNumericKind(value.Kind) &&
                     double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) &&
                     double.IsFinite(number) =>
                HistoricalQueryValue.FromNumber(number),

            HistoricalFieldType.Boolean
                when value.Kind == HistoricalValueKind.Boolean && bool.TryParse(text, out var boolean) =>
                HistoricalQueryValue.FromBoolean(boolean),

            HistoricalFieldType.DateTime
                when TryParseUtcDateTime(value, out var timestamp) =>
                HistoricalQueryValue.FromDateTime(timestamp),

            HistoricalFieldType.Int64
                when value.Kind == HistoricalValueKind.Int64 &&
                     long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer) =>
                HistoricalQueryValue.FromInt64(integer),

            HistoricalFieldType.Scalar when IsScalarKind(value.Kind) =>
                NormalizeScalar(value),

            _ => throw new ArgumentException(
                $"Historical filter value kind '{value.Kind}' does not match field type '{fieldType}'.",
                nameof(value))
        };
    }

    private static HistoricalQueryValue NormalizeScalar(HistoricalQueryValue value) => value.Kind switch
    {
        HistoricalValueKind.String => HistoricalQueryValue.FromString(value.Value!),
        HistoricalValueKind.Enum => HistoricalQueryValue.FromEnum(value.Value!.Trim()),
        HistoricalValueKind.Int16
            when short.TryParse(value.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var int16) =>
            HistoricalQueryValue.FromInt16(int16),
        HistoricalValueKind.Int32
            when int.TryParse(value.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var int32) =>
            HistoricalQueryValue.FromInt32(int32),
        HistoricalValueKind.Int64
            when long.TryParse(value.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var int64) =>
            HistoricalQueryValue.FromInt64(int64),
        HistoricalValueKind.Float
            when float.TryParse(value.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var single) &&
                 float.IsFinite(single) =>
            HistoricalQueryValue.FromFloat(single),
        HistoricalValueKind.Double
            when double.TryParse(value.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var dbl) &&
                 double.IsFinite(dbl) =>
            HistoricalQueryValue.FromDouble(dbl),
        HistoricalValueKind.Number
            when double.TryParse(value.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) &&
                 double.IsFinite(number) =>
            HistoricalQueryValue.FromNumber(number),
        HistoricalValueKind.Boolean when bool.TryParse(value.Value, out var boolean) =>
            HistoricalQueryValue.FromBoolean(boolean),
        HistoricalValueKind.DateTime when TryParseUtcDateTime(value, out var timestamp) =>
            HistoricalQueryValue.FromDateTime(timestamp),
        _ => throw new ArgumentException("Historical scalar filter value is invalid.", nameof(value))
    };

    private static bool TryParseUtcDateTime(
        HistoricalQueryValue value,
        out DateTimeOffset timestamp)
    {
        timestamp = default;
        return value.Kind == HistoricalValueKind.DateTime &&
               DateTimeOffset.TryParse(
                   value.Value,
                   CultureInfo.InvariantCulture,
                   DateTimeStyles.RoundtripKind,
                   out timestamp) &&
               timestamp.Offset == TimeSpan.Zero;
    }

    private static bool IsNumericKind(HistoricalValueKind kind) =>
        kind is HistoricalValueKind.Int16 or
            HistoricalValueKind.Int32 or
            HistoricalValueKind.Int64 or
            HistoricalValueKind.Float or
            HistoricalValueKind.Double or
            HistoricalValueKind.Number;

    private static bool IsScalarKind(HistoricalValueKind kind) =>
        kind is HistoricalValueKind.String or
            HistoricalValueKind.Enum or
            HistoricalValueKind.Int16 or
            HistoricalValueKind.Int32 or
            HistoricalValueKind.Int64 or
            HistoricalValueKind.Float or
            HistoricalValueKind.Double or
            HistoricalValueKind.Number or
            HistoricalValueKind.Boolean or
            HistoricalValueKind.DateTime;

    private static string ComputeFingerprint(
        int version,
        string dataset,
        HistoricalTimeRange range,
        IReadOnlyList<HistoricalFilter> filters,
        string? search,
        HistoricalSort sort,
        int pageSize)
    {
        var builder = new StringBuilder();
        builder.Append('v').Append(version).Append('|').Append(dataset).Append('|');
        switch (range.Kind)
        {
            case HistoricalTimeRangeKind.Relative:
                builder.Append("relative:")
                    .Append(range.DurationSeconds!.Value)
                    .Append(':')
                    .Append(range.Anchor);
                break;
            case HistoricalTimeRangeKind.Absolute:
                builder.Append("absolute:")
                    .Append(range.FromUtc!.Value.UtcTicks)
                    .Append(':')
                    .Append(range.ToUtc!.Value.UtcTicks);
                break;
            default:
                throw new ArgumentException("Historical time-range kind is unsupported.", nameof(range));
        }

        builder.Append("|search:").Append(search ?? string.Empty)
            .Append("|sort:").Append(sort.Field).Append(':').Append((int)sort.Direction)
            .Append("|page:").Append(pageSize);

        foreach (var filter in filters
                     .OrderBy(static filter => filter.Field, StringComparer.Ordinal)
                     .ThenBy(static filter => filter.Operator))
        {
            builder.Append("|f:")
                .Append(filter.Field)
                .Append(':')
                .Append((int)filter.Operator)
                .Append(':');
            foreach (var value in filter.Values
                         .Select(static value => value.CanonicalText())
                         .OrderBy(static value => value, StringComparer.Ordinal))
                builder.Append(value).Append(',');
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    private static void EnsureUtc(DateTimeOffset value, string label)
    {
        if (value.Offset != TimeSpan.Zero)
            throw new ArgumentException($"{label} must use UTC offset +00:00.");
    }
}
