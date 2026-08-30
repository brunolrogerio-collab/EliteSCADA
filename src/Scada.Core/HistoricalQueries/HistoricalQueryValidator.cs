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
    public static readonly TimeSpan MaximumAbsoluteRange = TimeSpan.FromDays(31);

    private static readonly IReadOnlyDictionary<string, TimeSpan> RelativeRanges =
        new Dictionary<string, TimeSpan>(StringComparer.OrdinalIgnoreCase)
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
        ValidateRangeShape(request.Range);
        var filters = NormalizeFilters(dataset, request.Filters ?? Array.Empty<HistoricalFilter>());
        var search = NormalizeSearch(dataset, request.Search);
        var sort = NormalizeSort(dataset, request.Sort);
        var pageSize = request.Page?.Size ?? DefaultPageSize;
        if (pageSize is < 1 or > MaximumPageSize)
            throw new ArgumentOutOfRangeException(nameof(request), $"Historical page size must be between 1 and {MaximumPageSize}.");

        var fingerprint = ComputeFingerprint(dataset.Id, request.Range, filters, search, sort, pageSize);
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

    public static void ValidateResolvedRange(
        HistoricalResolvedRange range,
        DateTimeOffset nowUtc)
    {
        EnsureUtc(range.FromUtc, "Historical cursor FromUtc");
        EnsureUtc(range.ToUtc, "Historical cursor ToUtc");
        EnsureUtc(nowUtc, "Historical query clock");
        if (range.FromUtc >= range.ToUtc ||
            range.ToUtc > nowUtc ||
            range.ToUtc - range.FromUtc > MaximumAbsoluteRange)
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
        if (hasAbsolute)
        {
            EnsureUtc(range.FromUtc!.Value, "Historical FromUtc");
            EnsureUtc(range.ToUtc!.Value, "Historical ToUtc");
        }
    }

    private static IReadOnlyList<HistoricalFilter> NormalizeFilters(
        HistoricalDatasetDefinition dataset,
        IReadOnlyList<HistoricalFilter> filters)
    {
        if (filters.Count > MaximumFilterCount)
            throw new ArgumentException($"Historical query cannot contain more than {MaximumFilterCount} filters.", nameof(filters));

        var normalized = new List<HistoricalFilter>(filters.Count);
        foreach (var filter in filters)
        {
            if (filter is null)
                throw new ArgumentException("Historical filter cannot be null.", nameof(filters));
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

    private static string? NormalizeSearch(
        HistoricalDatasetDefinition dataset,
        string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return null;
        if (!dataset.Fields.Values.Any(static field => field.Searchable))
            throw new ArgumentException($"Historical dataset '{dataset.Id}' does not support search.", nameof(search));
        var normalized = search.Trim();
        if (normalized.Length > MaximumSearchLength)
            throw new ArgumentException($"Historical search cannot exceed {MaximumSearchLength} characters.", nameof(search));
        return normalized;
    }

    private static HistoricalSort NormalizeSort(
        HistoricalDatasetDefinition dataset,
        HistoricalSort? sort)
    {
        var candidate = sort ?? new HistoricalSort(dataset.DefaultSortField, dataset.DefaultSortDirection);
        var field = candidate.Field?.Trim() ?? string.Empty;
        if (!dataset.Fields.TryGetValue(field, out var definition) || !definition.Sortable)
            throw new ArgumentException($"Historical sort field '{field}' is not allowlisted for dataset '{dataset.Id}'.", nameof(sort));
        return new HistoricalSort(field, candidate.Direction);
    }

    private static HistoricalQueryValue NormalizeValue(
        HistoricalFieldType fieldType,
        HistoricalQueryValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Kind == HistoricalValueKind.Null)
            throw new ArgumentException("Historical filter values cannot be null.", nameof(value));
        var text = value.Value ?? throw new ArgumentException("Historical filter value text is required.", nameof(value));
        if (text.Length > 1000)
            throw new ArgumentException("Historical filter value is too long.", nameof(value));

        return fieldType switch
        {
            HistoricalFieldType.Guid when value.Kind == HistoricalValueKind.Guid && Guid.TryParse(text, out var guid) =>
                HistoricalQueryValue.FromGuid(guid),
            HistoricalFieldType.String when value.Kind == HistoricalValueKind.String =>
                HistoricalQueryValue.FromString(text),
            HistoricalFieldType.Enum when value.Kind is HistoricalValueKind.Enum or HistoricalValueKind.String =>
                HistoricalQueryValue.FromEnum(text.Trim()),
            HistoricalFieldType.Number when value.Kind == HistoricalValueKind.Number &&
                double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number) =>
                HistoricalQueryValue.FromNumber(number),
            HistoricalFieldType.Boolean when value.Kind == HistoricalValueKind.Boolean && bool.TryParse(text, out var boolean) =>
                HistoricalQueryValue.FromBoolean(boolean),
            HistoricalFieldType.DateTime when value.Kind == HistoricalValueKind.DateTime &&
                DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var timestamp) && timestamp.Offset == TimeSpan.Zero =>
                HistoricalQueryValue.FromDateTime(timestamp),
            HistoricalFieldType.Int64 when value.Kind == HistoricalValueKind.Int64 &&
                long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer) =>
                HistoricalQueryValue.FromInt64(integer),
            HistoricalFieldType.Scalar when value.Kind is HistoricalValueKind.String or HistoricalValueKind.Number or HistoricalValueKind.Boolean or HistoricalValueKind.Int64 or HistoricalValueKind.Enum =>
                NormalizeScalar(value),
            _ => throw new ArgumentException($"Historical filter value kind '{value.Kind}' does not match field type '{fieldType}'.", nameof(value))
        };
    }

    private static HistoricalQueryValue NormalizeScalar(HistoricalQueryValue value) => value.Kind switch
    {
        HistoricalValueKind.String => HistoricalQueryValue.FromString(value.Value!),
        HistoricalValueKind.Enum => HistoricalQueryValue.FromEnum(value.Value!.Trim()),
        HistoricalValueKind.Number when double.TryParse(value.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number) =>
            HistoricalQueryValue.FromNumber(number),
        HistoricalValueKind.Boolean when bool.TryParse(value.Value, out var boolean) =>
            HistoricalQueryValue.FromBoolean(boolean),
        HistoricalValueKind.Int64 when long.TryParse(value.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer) =>
            HistoricalQueryValue.FromInt64(integer),
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
