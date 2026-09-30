using System.Globalization;
using System.Text;
using Npgsql;
using NpgsqlTypes;
using Scada.Core.HistoricalQueries;
using Scada.Core.Tags;
using Scada.Historian.Aggregation;

namespace Scada.Historian.TimescaleDb;

public sealed partial class TimescaleHistoricalQueryProvider : IHistoricalRetrievalProvider
{
    private const int MaximumRetrievalSourceSamples = 50_000;

    public async Task<HistoricalRetrievalResult> QueryLastAsync(
        HistoricalQueryExecution query,
        CancellationToken cancellationToken = default)
    {
        ValidateRetrievalQuery(query);
        var rows = await LoadPointRowsAsync(query, null, null, descending: true, cancellationToken);
        var points = AddPointGaps(
            query,
            rows,
            query.Range.ToUtc,
            rows.Select(ToMeasuredPoint).ToArray(),
            "no-source-observation");
        return Result(query, points);
    }

    public async Task<HistoricalRetrievalResult> QueryAtOrBeforeAsync(
        HistoricalQueryExecution query,
        DateTimeOffset targetUtc,
        CancellationToken cancellationToken = default)
    {
        ValidateTarget(query, targetUtc);
        var rows = await LoadPointRowsAsync(query, "ts <= @target_utc", targetUtc, descending: true, cancellationToken);
        var points = AddPointGaps(
            query,
            rows,
            targetUtc,
            rows.Select(ToMeasuredPoint).ToArray(),
            "no-observation-at-or-before-target");
        return Result(query, points);
    }

    public async Task<HistoricalRetrievalResult> QueryAtOrAfterAsync(
        HistoricalQueryExecution query,
        DateTimeOffset targetUtc,
        CancellationToken cancellationToken = default)
    {
        ValidateTarget(query, targetUtc);
        var rows = await LoadPointRowsAsync(query, "ts >= @target_utc", targetUtc, descending: false, cancellationToken);
        var points = AddPointGaps(
            query,
            rows,
            targetUtc,
            rows.Select(ToMeasuredPoint).ToArray(),
            "no-observation-at-or-after-target");
        return Result(query, points);
    }

    public async Task<HistoricalRetrievalResult> QueryExactAsync(
        HistoricalQueryExecution query,
        DateTimeOffset targetUtc,
        CancellationToken cancellationToken = default)
    {
        ValidateTarget(query, targetUtc);
        var rows = await LoadPointRowsAsync(query, "ts = @target_utc", targetUtc, descending: true, cancellationToken);
        var points = AddPointGaps(
            query,
            rows,
            targetUtc,
            rows.Select(ToMeasuredPoint).ToArray(),
            "no-exact-observation");
        return Result(query, points);
    }

    public async Task<HistoricalRetrievalResult> QueryInterpolatedAsync(
        HistoricalQueryExecution query,
        DateTimeOffset targetUtc,
        int maximumGapMilliseconds,
        CancellationToken cancellationToken = default)
    {
        ValidateTarget(query, targetUtc);
        ValidatePositive(maximumGapMilliseconds, nameof(maximumGapMilliseconds));

        var exact = await LoadPointRowsAsync(query, "ts = @target_utc", targetUtc, descending: true, cancellationToken);
        var before = await LoadPointRowsAsync(query, "ts < @target_utc", targetUtc, descending: true, cancellationToken);
        var after = await LoadPointRowsAsync(query, "ts > @target_utc", targetUtc, descending: false, cancellationToken);

        var exactByTag = exact.ToDictionary(x => x.TagId);
        var beforeByTag = before.ToDictionary(x => x.TagId);
        var afterByTag = after.ToDictionary(x => x.TagId);
        var candidates = ResolveCandidateTagIds(query, exact.Concat(before).Concat(after)).ToArray();
        EnsureResultBound(query, candidates.Length);

        var maxGap = TimeSpan.FromMilliseconds(maximumGapMilliseconds);
        var points = new List<HistoricalRetrievalPoint>(candidates.Length);
        foreach (var tagId in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (exactByTag.TryGetValue(tagId, out var measured))
            {
                points.Add(ToMeasuredPoint(measured));
                continue;
            }

            beforeByTag.TryGetValue(tagId, out var left);
            afterByTag.TryGetValue(tagId, out var right);
            points.Add(InterpolateOrGap(tagId, targetUtc, left, right, maxGap));
        }

        return Result(query, points);
    }

    public async Task<HistoricalRetrievalResult> QuerySampledFixedStepAsync(
        HistoricalQueryExecution query,
        int stepMilliseconds,
        int maximumGapMilliseconds,
        CancellationToken cancellationToken = default)
    {
        ValidateRetrievalQuery(query);
        ValidatePositive(stepMilliseconds, nameof(stepMilliseconds));
        ValidatePositive(maximumGapMilliseconds, nameof(maximumGapMilliseconds));

        var rows = await LoadSourceRowsAsync(query, cancellationToken);
        var tagIds = ResolveCandidateTagIds(query, rows).ToArray();
        var step = TimeSpan.FromMilliseconds(stepMilliseconds);
        var maxGap = TimeSpan.FromMilliseconds(maximumGapMilliseconds);
        var gridCount = checked(((query.Range.ToUtc.UtcTicks - query.Range.FromUtc.UtcTicks) / step.Ticks) + 1L);
        if (gridCount <= 0 || gridCount > int.MaxValue)
            throw new HistoricalQueryValidationException("SampledFixedStep grid is outside the supported bounded range.");
        if (checked(gridCount * Math.Max(1, tagIds.LongLength)) > query.PageSize)
            throw new HistoricalQueryValidationException(
                $"SampledFixedStep would return more than the validated result limit {query.PageSize}; increase StepMilliseconds or narrow the range.");

        var byTag = rows
            .GroupBy(x => x.TagId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(x => x.Timestamp).ThenBy(x => x.SampleId).ToArray());

        var points = new List<HistoricalRetrievalPoint>(checked((int)(gridCount * tagIds.LongLength)));
        foreach (var tagId in tagIds)
        {
            byTag.TryGetValue(tagId, out var samples);
            samples ??= Array.Empty<SampleRow>();
            var seriesType = ResolveSeriesType(samples);

            for (long n = 0; n < gridCount; n++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var timestamp = query.Range.FromUtc + TimeSpan.FromTicks(checked(n * step.Ticks));
                var exact = samples
                    .Where(x => x.Timestamp == timestamp)
                    .OrderByDescending(x => x.SampleId)
                    .FirstOrDefault();
                if (exact is not null)
                {
                    points.Add(ToMeasuredPoint(exact));
                    continue;
                }

                var previous = samples
                    .Where(x => x.Timestamp < timestamp)
                    .OrderByDescending(x => x.Timestamp)
                    .ThenByDescending(x => x.SampleId)
                    .FirstOrDefault();

                if (seriesType.HasValue && IsNumeric(seriesType.Value))
                {
                    var next = samples
                        .Where(x => x.Timestamp > timestamp)
                        .OrderBy(x => x.Timestamp)
                        .ThenByDescending(x => x.SampleId)
                        .FirstOrDefault();
                    points.Add(InterpolateOrGap(tagId, timestamp, previous, next, maxGap));
                    continue;
                }

                if (seriesType.HasValue)
                {
                    points.Add(HoldOrGap(tagId, timestamp, previous, seriesType.Value, maxGap));
                    continue;
                }

                points.Add(GapPoint(tagId, timestamp, null, "unknown-or-inconsistent-data-type"));
            }
        }

        return Result(query, points);
    }

    public async Task<HistoricalRetrievalResult> QueryAggregateAsync(
        HistoricalQueryExecution query,
        int bucketMilliseconds,
        HistoricalAggregateOperation operation,
        CancellationToken cancellationToken = default)
    {
        ValidateRetrievalQuery(query);
        ValidatePositive(bucketMilliseconds, nameof(bucketMilliseconds));
        if (!Enum.IsDefined(operation))
            throw new HistoricalQueryValidationException("Aggregate operation is invalid.");

        var rows = await LoadSourceRowsAsync(query, cancellationToken);
        var width = TimeSpan.FromMilliseconds(bucketMilliseconds);
        var groups = rows
            .GroupBy(x => (TagId: x.TagId, BucketStart: BucketStart(x.Timestamp, width)))
            .OrderBy(g => g.Key.BucketStart)
            .ThenBy(g => g.Key.TagId)
            .ToArray();

        if (groups.Length > query.PageSize)
            throw new HistoricalQueryValidationException(
                $"Aggregate would return {groups.Length} buckets, exceeding the validated result limit {query.PageSize}; increase BucketMilliseconds or narrow the range.");

        var points = new List<HistoricalRetrievalPoint>(groups.Length);
        foreach (var group in groups)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var ordered = group.OrderBy(x => x.Timestamp).ThenBy(x => x.SampleId).ToArray();
            var sampleCount = ordered.LongLength;
            long good = 0;
            long uncertain = 0;
            long bad = 0;
            var numericGood = new List<double>();
            foreach (var sample in ordered)
            {
                switch (HistorianQualityClassifier.Classify(sample.Quality))
                {
                    case HistorianQualityClass.Good:
                        good++;
                        if (TryNumeric(sample, out var numeric)) numericGood.Add(numeric);
                        break;
                    case HistorianQualityClass.Uncertain:
                        uncertain++;
                        break;
                    case HistorianQualityClass.Bad:
                        bad++;
                        break;
                }
            }

            var derived = bad > 0
                ? TagQuality.Bad
                : uncertain > 0
                    ? TagQuality.Uncertain
                    : TagQuality.Good;
            var first = ordered[0];
            var last = ordered[^1];
            HistoricalQueryValue value;
            TagQuality quality;
            IReadOnlyList<DateTimeOffset> sourceTimestamps;
            TagDataType? dataType;

            switch (operation)
            {
                case HistoricalAggregateOperation.Count:
                    value = HistoricalQueryValue.FromInt64(sampleCount);
                    quality = derived;
                    sourceTimestamps = Array.Empty<DateTimeOffset>();
                    dataType = null;
                    break;
                case HistoricalAggregateOperation.Sum:
                    value = NumericAggregate(numericGood, static values => values.Sum());
                    quality = derived;
                    sourceTimestamps = Array.Empty<DateTimeOffset>();
                    dataType = TagDataType.Double;
                    break;
                case HistoricalAggregateOperation.Average:
                    value = NumericAggregate(numericGood, static values => values.Average());
                    quality = derived;
                    sourceTimestamps = Array.Empty<DateTimeOffset>();
                    dataType = TagDataType.Double;
                    break;
                case HistoricalAggregateOperation.Minimum:
                    value = NumericAggregate(numericGood, static values => values.Min());
                    quality = derived;
                    sourceTimestamps = Array.Empty<DateTimeOffset>();
                    dataType = TagDataType.Double;
                    break;
                case HistoricalAggregateOperation.Maximum:
                    value = NumericAggregate(numericGood, static values => values.Max());
                    quality = derived;
                    sourceTimestamps = Array.Empty<DateTimeOffset>();
                    dataType = TagDataType.Double;
                    break;
                case HistoricalAggregateOperation.First:
                    value = ReadValue(first.ValueJson, first.DataType);
                    quality = first.Quality;
                    sourceTimestamps = [first.Timestamp];
                    dataType = first.DataType;
                    break;
                case HistoricalAggregateOperation.Last:
                    value = ReadValue(last.ValueJson, last.DataType);
                    quality = last.Quality;
                    sourceTimestamps = [last.Timestamp];
                    dataType = last.DataType;
                    break;
                default:
                    throw new HistoricalQueryValidationException("Aggregate operation is invalid.");
            }

            var bucketStart = group.Key.BucketStart;
            points.Add(new HistoricalRetrievalPoint(
                group.Key.TagId,
                TagPath(group.Key.TagId),
                bucketStart,
                value,
                quality,
                dataType,
                new HistoricalRetrievalProvenance(
                    HistoricalRetrievalProvenanceKind.Aggregate,
                    sourceTimestamps,
                    bucketStart,
                    bucketStart + width,
                    sampleCount,
                    good,
                    uncertain,
                    bad,
                    derived)));
        }

        return Result(query, points);
    }

    private async Task<IReadOnlyList<SampleRow>> LoadPointRowsAsync(
        HistoricalQueryExecution query,
        string? targetPredicate,
        DateTimeOffset? targetUtc,
        bool descending,
        CancellationToken cancellationToken)
    {
        ValidateRetrievalQuery(query);
        await _initializeTask.WaitAsync(cancellationToken);
        var tags = _tagRegistry.Snapshot();
        var order = descending ? "DESC" : "ASC";
        var sql = new StringBuilder($"""
            WITH ranked AS (
                SELECT sample_id, tag_id, ts, quality, value::text, data_type,
                       row_number() OVER (PARTITION BY tag_id ORDER BY ts {order}, sample_id DESC) AS rn
                FROM elitescada.tag_history
                WHERE ts >= @from_utc AND ts <= @to_utc
            """);
        await using var command = _dataSource.CreateCommand();
        command.Parameters.AddWithValue("from_utc", NpgsqlDbType.TimestampTz, query.Range.FromUtc);
        command.Parameters.AddWithValue("to_utc", NpgsqlDbType.TimestampTz, query.Range.ToUtc);

        var parameterIndex = 0;
        foreach (var filter in query.Filters)
            sql.Append(" AND ").Append(BuildFilter(filter, tags, command, ref parameterIndex));

        AppendSearch(query, tags, sql, command);
        if (!string.IsNullOrWhiteSpace(targetPredicate))
        {
            sql.Append(" AND ").Append(targetPredicate);
            command.Parameters.AddWithValue(
                "target_utc",
                NpgsqlDbType.TimestampTz,
                targetUtc ?? throw new ArgumentNullException(nameof(targetUtc)));
        }

        sql.Append(
            """
            )
            SELECT sample_id, tag_id, ts, quality, value, data_type
            FROM ranked
            WHERE rn = 1
            ORDER BY tag_id
            LIMIT @fetch_limit;
            """);
        command.Parameters.AddWithValue("fetch_limit", NpgsqlDbType.Integer, query.PageSize + 1);
        command.CommandText = sql.ToString();

        var rows = await ReadRowsAsync(command, cancellationToken);
        if (rows.Count > query.PageSize)
            throw new HistoricalQueryValidationException(
                $"Point retrieval exceeds the validated result limit {query.PageSize}; narrow the TAG selection.");
        return rows;
    }

    private async Task<IReadOnlyList<SampleRow>> LoadSourceRowsAsync(
        HistoricalQueryExecution query,
        CancellationToken cancellationToken)
    {
        ValidateRetrievalQuery(query);
        await _initializeTask.WaitAsync(cancellationToken);
        var tags = _tagRegistry.Snapshot();
        var sql = new StringBuilder("""
            SELECT sample_id, tag_id, ts, quality, value::text, data_type
            FROM elitescada.tag_history
            WHERE ts >= @from_utc AND ts <= @to_utc
            """);
        await using var command = _dataSource.CreateCommand();
        command.Parameters.AddWithValue("from_utc", NpgsqlDbType.TimestampTz, query.Range.FromUtc);
        command.Parameters.AddWithValue("to_utc", NpgsqlDbType.TimestampTz, query.Range.ToUtc);

        var parameterIndex = 0;
        foreach (var filter in query.Filters)
            sql.Append(" AND ").Append(BuildFilter(filter, tags, command, ref parameterIndex));

        AppendSearch(query, tags, sql, command);
        sql.Append(" ORDER BY tag_id ASC, ts ASC, sample_id ASC LIMIT @source_limit;");
        command.Parameters.AddWithValue("source_limit", NpgsqlDbType.Integer, MaximumRetrievalSourceSamples + 1);
        command.CommandText = sql.ToString();

        var rows = await ReadRowsAsync(command, cancellationToken);
        if (rows.Count > MaximumRetrievalSourceSamples)
            throw new HistoricalQueryValidationException(
                $"Historical retrieval source selection exceeds {MaximumRetrievalSourceSamples} persisted samples; narrow the range/TAG selection or increase sampling/aggregation resolution.");
        return rows;
    }

    private static void AppendSearch(
        HistoricalQueryExecution query,
        IReadOnlyCollection<TagDefinition> tags,
        StringBuilder sql,
        NpgsqlCommand command)
    {
        if (string.IsNullOrWhiteSpace(query.Search)) return;
        var ids = tags
            .Where(tag => tag.Path.Contains(query.Search, StringComparison.OrdinalIgnoreCase))
            .Select(tag => tag.Id)
            .Distinct()
            .ToArray();
        sql.Append(" AND tag_id = ANY(@retrieval_search_tag_ids)");
        command.Parameters.AddWithValue(
            "retrieval_search_tag_ids",
            NpgsqlDbType.Array | NpgsqlDbType.Uuid,
            ids);
    }

    private static async Task<List<SampleRow>> ReadRowsAsync(
        NpgsqlCommand command,
        CancellationToken cancellationToken)
    {
        var rows = new List<SampleRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new SampleRow(
                reader.GetInt64(0),
                reader.GetGuid(1),
                ReadTimestamp(reader, 2),
                (TagQuality)reader.GetInt32(3),
                reader.GetString(4),
                reader.IsDBNull(5) ? null : (TagDataType?)reader.GetInt16(5)));
        }
        return rows;
    }

    private HistoricalRetrievalPoint InterpolateOrGap(
        Guid tagId,
        DateTimeOffset targetUtc,
        SampleRow? left,
        SampleRow? right,
        TimeSpan maximumGap)
    {
        if (left is null || right is null)
            return GapPoint(tagId, targetUtc, left?.DataType ?? right?.DataType, "missing-bracketing-observation");
        if (left.Quality != TagQuality.Good || right.Quality != TagQuality.Good)
            return GapPoint(tagId, targetUtc, ResolvePairType(left, right), "bracketing-quality-not-good");
        if (right.Timestamp - left.Timestamp > maximumGap)
            return GapPoint(tagId, targetUtc, ResolvePairType(left, right), "maximum-gap-exceeded");

        var dataType = ResolvePairType(left, right);
        if (!dataType.HasValue || !IsNumeric(dataType.Value))
            return GapPoint(tagId, targetUtc, dataType, "unsupported-interpolation-data-type");
        if (!TryNumeric(left, out var leftValue) || !TryNumeric(right, out var rightValue))
            return GapPoint(tagId, targetUtc, dataType, "non-numeric-persisted-value");

        var totalTicks = right.Timestamp.UtcTicks - left.Timestamp.UtcTicks;
        if (totalTicks <= 0)
            return GapPoint(tagId, targetUtc, dataType, "invalid-bracketing-order");
        var fraction = (double)(targetUtc.UtcTicks - left.Timestamp.UtcTicks) / totalTicks;
        var value = leftValue + ((rightValue - leftValue) * fraction);
        if (!double.IsFinite(value))
            return GapPoint(tagId, targetUtc, dataType, "non-finite-interpolation-result");

        return new HistoricalRetrievalPoint(
            tagId,
            TagPath(tagId),
            targetUtc,
            HistoricalQueryValue.FromNumber(value),
            TagQuality.Good,
            dataType,
            new HistoricalRetrievalProvenance(
                HistoricalRetrievalProvenanceKind.Interpolated,
                [left.Timestamp, right.Timestamp]));
    }

    private HistoricalRetrievalPoint HoldOrGap(
        Guid tagId,
        DateTimeOffset targetUtc,
        SampleRow? previous,
        TagDataType dataType,
        TimeSpan maximumGap)
    {
        if (previous is null)
            return GapPoint(tagId, targetUtc, dataType, "missing-held-observation");
        if (previous.Quality != TagQuality.Good)
            return GapPoint(tagId, targetUtc, dataType, "held-observation-quality-not-good");
        if (targetUtc - previous.Timestamp > maximumGap)
            return GapPoint(tagId, targetUtc, dataType, "maximum-gap-exceeded");

        return new HistoricalRetrievalPoint(
            tagId,
            TagPath(tagId),
            targetUtc,
            ReadValue(previous.ValueJson, previous.DataType),
            TagQuality.Good,
            dataType,
            new HistoricalRetrievalProvenance(
                HistoricalRetrievalProvenanceKind.Held,
                [previous.Timestamp]));
    }

    private HistoricalRetrievalPoint ToMeasuredPoint(SampleRow sample) =>
        new(
            sample.TagId,
            TagPath(sample.TagId),
            sample.Timestamp,
            ReadValue(sample.ValueJson, sample.DataType),
            sample.Quality,
            sample.DataType,
            new HistoricalRetrievalProvenance(
                HistoricalRetrievalProvenanceKind.Measured,
                [sample.Timestamp],
                DerivedQuality: sample.Quality));

    private HistoricalRetrievalPoint GapPoint(
        Guid tagId,
        DateTimeOffset timestampUtc,
        TagDataType? dataType,
        string reason) =>
        new(
            tagId,
            TagPath(tagId),
            timestampUtc,
            HistoricalQueryValue.Null(),
            null,
            dataType,
            new HistoricalRetrievalProvenance(
                HistoricalRetrievalProvenanceKind.Gap,
                Array.Empty<DateTimeOffset>(),
                Reason: reason));

    private IReadOnlyList<HistoricalRetrievalPoint> AddPointGaps(
        HistoricalQueryExecution query,
        IReadOnlyCollection<SampleRow> observed,
        DateTimeOffset gapTimestamp,
        IReadOnlyCollection<HistoricalRetrievalPoint> measured,
        string reason)
    {
        var candidates = ResolveCandidateTagIds(query, observed).ToArray();
        EnsureResultBound(query, candidates.Length);
        var byTag = measured.ToDictionary(x => x.TagId);
        return candidates
            .Select(tagId => byTag.TryGetValue(tagId, out var point)
                ? point
                : GapPoint(tagId, gapTimestamp, null, reason))
            .ToArray();
    }

    private IEnumerable<Guid> ResolveCandidateTagIds(
        HistoricalQueryExecution query,
        IEnumerable<SampleRow> observed)
    {
        var observedIds = observed.Select(x => x.TagId).Distinct().ToHashSet();
        var tagSelectors = query.Filters
            .Where(x => x.Field is "tag.id" or "tag.path")
            .ToArray();
        var hasIdentitySelector = tagSelectors.Length > 0 || !string.IsNullOrWhiteSpace(query.Search);
        if (!hasIdentitySelector)
            return observedIds.Order();

        var selected = _tagRegistry.Snapshot()
            .Where(tag => tagSelectors.All(filter => MatchesTagFilter(tag, filter)))
            .Where(tag => string.IsNullOrWhiteSpace(query.Search) ||
                          tag.Path.Contains(query.Search, StringComparison.OrdinalIgnoreCase))
            .Select(tag => tag.Id)
            .Concat(observedIds)
            .Distinct()
            .Order()
            .ToArray();
        return selected;
    }

    private static bool MatchesTagFilter(TagDefinition tag, HistoricalFilter filter)
    {
        if (filter.Field == "tag.id")
        {
            var values = filter.Values.Select(x => x.AsGuid()).ToArray();
            return filter.Operator switch
            {
                HistoricalFilterOperator.Eq => tag.Id == values[0],
                HistoricalFilterOperator.NotEq => tag.Id != values[0],
                HistoricalFilterOperator.In => values.Contains(tag.Id),
                _ => true
            };
        }

        if (filter.Field == "tag.path")
        {
            var values = filter.Values.Select(x => x.Value ?? string.Empty).ToArray();
            return filter.Operator switch
            {
                HistoricalFilterOperator.Eq => tag.Path.Equals(values[0], StringComparison.OrdinalIgnoreCase),
                HistoricalFilterOperator.NotEq => !tag.Path.Equals(values[0], StringComparison.OrdinalIgnoreCase),
                HistoricalFilterOperator.In => values.Any(x => tag.Path.Equals(x, StringComparison.OrdinalIgnoreCase)),
                HistoricalFilterOperator.Contains => tag.Path.Contains(values[0], StringComparison.OrdinalIgnoreCase),
                HistoricalFilterOperator.StartsWith => tag.Path.StartsWith(values[0], StringComparison.OrdinalIgnoreCase),
                _ => true
            };
        }

        return true;
    }

    private static TagDataType? ResolveSeriesType(IReadOnlyCollection<SampleRow> samples)
    {
        var types = samples.Where(x => x.DataType.HasValue).Select(x => x.DataType!.Value).Distinct().ToArray();
        return types.Length == 1 ? types[0] : null;
    }

    private static TagDataType? ResolvePairType(SampleRow left, SampleRow right) =>
        left.DataType.HasValue && right.DataType.HasValue && left.DataType == right.DataType
            ? left.DataType
            : null;

    private static bool IsNumeric(TagDataType dataType) =>
        dataType is TagDataType.Int16 or TagDataType.Int32 or TagDataType.Int64 or TagDataType.Float or TagDataType.Double;

    private static bool TryNumeric(SampleRow sample, out double numeric)
    {
        numeric = 0d;
        if (!sample.DataType.HasValue || !IsNumeric(sample.DataType.Value)) return false;
        var value = ReadValue(sample.ValueJson, sample.DataType);
        if (value.Kind is not (HistoricalValueKind.Int16 or HistoricalValueKind.Int32 or HistoricalValueKind.Int64 or HistoricalValueKind.Float or HistoricalValueKind.Double or HistoricalValueKind.Number))
            return false;
        return double.TryParse(
            value.Value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out numeric) && double.IsFinite(numeric);
    }

    private static HistoricalQueryValue NumericAggregate(
        IReadOnlyCollection<double> values,
        Func<IEnumerable<double>, double> aggregate)
    {
        if (values.Count == 0) return HistoricalQueryValue.Null();
        var result = aggregate(values);
        return double.IsFinite(result)
            ? HistoricalQueryValue.FromNumber(result)
            : HistoricalQueryValue.Null();
    }

    private static DateTimeOffset BucketStart(DateTimeOffset timestamp, TimeSpan width)
    {
        var ticks = timestamp.UtcDateTime.Ticks;
        var start = ticks - (ticks % width.Ticks);
        return new DateTimeOffset(start, TimeSpan.Zero);
    }

    private string? TagPath(Guid tagId) =>
        _tagRegistry.TryGet(tagId, out var tag) && tag is not null ? tag.Path : null;

    private static HistoricalRetrievalResult Result(
        HistoricalQueryExecution query,
        IReadOnlyList<HistoricalRetrievalPoint> points) =>
        new(query.Range, points);

    private static void ValidateRetrievalQuery(HistoricalQueryExecution query)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (!string.Equals(query.Dataset.Id, HistoricalDatasets.HistorianSamples, StringComparison.Ordinal))
            throw new ArgumentException("Timescale historian retrieval received the wrong dataset.", nameof(query));
        if (query.After is not null)
            throw new HistoricalQueryValidationException("Historian retrieval modes do not accept raw-query cursors.");
        if (query.PageSize < 1 || query.PageSize > HistoricalQueryValidator.MaximumPageSize)
            throw new HistoricalQueryValidationException("Historian retrieval result limit is invalid.");
    }

    private static void ValidateTarget(HistoricalQueryExecution query, DateTimeOffset targetUtc)
    {
        ValidateRetrievalQuery(query);
        if (targetUtc.Offset != TimeSpan.Zero)
            throw new HistoricalQueryValidationException("Historian TargetUtc must use UTC offset +00:00.");
        if (targetUtc < query.Range.FromUtc || targetUtc > query.Range.ToUtc)
            throw new HistoricalQueryValidationException("Historian TargetUtc must be inside the resolved range, inclusive.");
    }

    private static void ValidatePositive(int value, string name)
    {
        if (value <= 0)
            throw new HistoricalQueryValidationException($"{name} must be positive.");
    }

    private static void EnsureResultBound(HistoricalQueryExecution query, int count)
    {
        if (count > query.PageSize)
            throw new HistoricalQueryValidationException(
                $"Historical retrieval would return {count} series, exceeding the validated result limit {query.PageSize}.");
    }
}
