using Scada.Core.Tags;
using Scada.Historian.Policies;

namespace Scada.Historian.Aggregation;

public enum HistorianQualityClass
{
    Good,
    Uncertain,
    Bad
}

public static class HistorianQualityClassifier
{
    public static HistorianQualityClass Classify(TagQuality quality) => quality switch
    {
        TagQuality.Good => HistorianQualityClass.Good,
        TagQuality.Uncertain => HistorianQualityClass.Uncertain,
        _ => HistorianQualityClass.Bad
    };
}

public sealed record HistorianAggregateBucket(
    Guid TagId,
    HistorianBucketWidth Bucket,
    DateTimeOffset BucketStart,
    DateTimeOffset BucketEndExclusive,
    long SampleCount,
    long GoodCount,
    long UncertainCount,
    long BadCount,
    long NumericGoodCount,
    double? Minimum,
    double? Maximum,
    double? Average,
    object? FirstValue,
    TagQuality FirstQuality,
    object? LastValue,
    TagQuality LastQuality,
    TagDataType? DataType = null,
    bool DataTypeConsistent = true);

public static class HistorianBucketCalculator
{
    public static DateTimeOffset GetBucketStart(DateTimeOffset timestamp, HistorianBucketWidth bucket)
    {
        var width = bucket.ToTimeSpan();
        var utcTicks = timestamp.UtcDateTime.Ticks;
        var startTicks = utcTicks - (utcTicks % width.Ticks);
        return new DateTimeOffset(startTicks, TimeSpan.Zero);
    }

    public static DateTimeOffset GetBucketEndExclusive(DateTimeOffset timestamp, HistorianBucketWidth bucket) =>
        GetBucketStart(timestamp, bucket) + bucket.ToTimeSpan();
}

public static class HistorianBucketAggregator
{
    public static HistorianAggregateBucket? Aggregate(
        Guid tagId,
        TagDataType dataType,
        HistorianBucketWidth bucket,
        DateTimeOffset bucketStart,
        IEnumerable<TagValue> samples)
    {
        if (tagId == Guid.Empty)
            throw new ArgumentException("Historian aggregate TAG ID cannot be empty.", nameof(tagId));
        ArgumentNullException.ThrowIfNull(samples);

        var normalizedStart = HistorianBucketCalculator.GetBucketStart(bucketStart, bucket);
        if (normalizedStart != bucketStart.ToUniversalTime())
            throw new ArgumentException("Aggregate bucket start must be aligned to the requested bucket width.", nameof(bucketStart));

        var endExclusive = normalizedStart + bucket.ToTimeSpan();
        var ordered = samples.OrderBy(x => x.Timestamp).ToArray();
        if (ordered.Length == 0) return null;

        foreach (var sample in ordered)
        {
            if (sample.TagId != tagId)
                throw new ArgumentException("All samples in a bucket must belong to the requested TAG.", nameof(samples));
            if (sample.Timestamp < normalizedStart || sample.Timestamp >= endExclusive)
                throw new ArgumentException("Historian sample falls outside the requested bucket boundaries.", nameof(samples));
        }

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
                    if (TryGetNumericGoodValue(dataType, sample.Value, out var numeric))
                        numericGood.Add(numeric);
                    break;
                case HistorianQualityClass.Uncertain:
                    uncertain++;
                    ValidateValueTypeWhenPresent(dataType, sample.Value);
                    break;
                case HistorianQualityClass.Bad:
                    bad++;
                    ValidateValueTypeWhenPresent(dataType, sample.Value);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        return new HistorianAggregateBucket(
            tagId,
            bucket,
            normalizedStart,
            endExclusive,
            ordered.LongLength,
            good,
            uncertain,
            bad,
            numericGood.Count,
            numericGood.Count == 0 ? null : numericGood.Min(),
            numericGood.Count == 0 ? null : numericGood.Max(),
            numericGood.Count == 0 ? null : numericGood.Average(),
            ordered[0].Value,
            ordered[0].Quality,
            ordered[^1].Value,
            ordered[^1].Quality,
            dataType,
            DataTypeConsistent: true);
    }

    public static bool IsNumeric(TagDataType dataType) => dataType is
        TagDataType.Int16 or
        TagDataType.Int32 or
        TagDataType.Int64 or
        TagDataType.Float or
        TagDataType.Double;

    private static bool TryGetNumericGoodValue(TagDataType dataType, object? value, out double numeric)
    {
        ValidateValueTypeWhenPresent(dataType, value);
        numeric = 0;
        if (value is null || !IsNumeric(dataType)) return false;

        numeric = dataType switch
        {
            TagDataType.Int16 => (short)value,
            TagDataType.Int32 => (int)value,
            TagDataType.Int64 => (long)value,
            TagDataType.Float => (float)value,
            TagDataType.Double => (double)value,
            _ => throw new InvalidOperationException($"TAG data type '{dataType}' is not numeric.")
        };
        return true;
    }

    private static void ValidateValueTypeWhenPresent(TagDataType dataType, object? value)
    {
        if (value is null) return;

        var valid = dataType switch
        {
            TagDataType.Boolean => value is bool,
            TagDataType.Int16 => value is short,
            TagDataType.Int32 => value is int,
            TagDataType.Int64 => value is long,
            TagDataType.Float => value is float,
            TagDataType.Double => value is double,
            TagDataType.String => value is string,
            TagDataType.DateTime => value is DateTimeOffset or DateTime,
            TagDataType.Enum => value is int or string,
            _ => false
        };

        if (!valid)
            throw new ArgumentException(
                $"Historian sample runtime type '{value.GetType().FullName}' is incompatible with TAG data type '{dataType}'. No silent coercion is allowed.");
    }
}
