using System.Collections.ObjectModel;
using System.Globalization;
using Scada.Core.HistoricalQueries;
using Scada.Core.Tags;

namespace Scada.Api.Licensing;

public static class AssetProcessIntelligenceKpiV1
{
    public static AssetProcessIntelligenceKpiSummary Calculate(
        IReadOnlyList<AssetProcessIntelligenceRawObservation> observations,
        AssetProcessIntelligenceAnalyticsWindow window,
        int bucketSeconds)
    {
        ArgumentNullException.ThrowIfNull(observations);
        ArgumentNullException.ThrowIfNull(window);
        if (window.ToUtc <= window.FromUtc)
            throw new ArgumentException("Analytics window must have positive duration.", nameof(window));
        if (!AssetProcessIntelligenceKpiCatalogV1.AllowedBucketSizesSeconds.Contains(bucketSeconds))
            throw new ArgumentOutOfRangeException(nameof(bucketSeconds));

        var duration = window.ToUtc - window.FromUtc;
        var bucketTicks = TimeSpan.FromSeconds(bucketSeconds).Ticks;
        var bucketCountLong = checked((duration.Ticks + bucketTicks - 1) / bucketTicks);
        if (bucketCountLong < 1 || bucketCountLong > AssetProcessIntelligenceKpiCatalogV1.MaximumBucketCount)
            throw new ArgumentOutOfRangeException(nameof(window), "Analytics window exceeds the fixed trend bucket limit.");

        var buckets = Enumerable.Range(0, (int)bucketCountLong)
            .Select(index => new BucketAccumulator(
                window.FromUtc.AddTicks(Math.Min(duration.Ticks, (long)index * bucketTicks)),
                window.FromUtc.AddTicks(Math.Min(duration.Ticks, (long)(index + 1) * bucketTicks))))
            .ToArray();
        var qualityCounts = Enum.GetValues<TagQuality>()
            .ToDictionary(static quality => quality.ToString(), static _ => 0, StringComparer.Ordinal);
        qualityCounts.Add("Missing", 0);
        qualityCounts.Add("Unrecognized", 0);

        var accepted = 0;
        double minimum = double.PositiveInfinity;
        double maximum = double.NegativeInfinity;
        double mean = 0;
        AssetProcessIntelligenceObservedValue? first = null;
        AssetProcessIntelligenceObservedValue? last = null;

        var ordered = observations
            .Select((observation, index) => (observation, index))
            .OrderBy(item => item.observation.TimestampUtc)
            .ThenBy(item => item.index)
            .Select(item => item.observation);

        foreach (var observation in ordered)
        {
            if (observation.TimestampUtc < window.FromUtc || observation.TimestampUtc > window.ToUtc)
                throw new InvalidDataException("Historian returned an observation outside the requested window.");

            var quality = NormalizeQuality(observation.Quality);
            qualityCounts[quality]++;
            var bucketIndex = (int)Math.Min(
                buckets.Length - 1,
                Math.Max(0, (observation.TimestampUtc - window.FromUtc).Ticks / bucketTicks));
            var bucket = buckets[bucketIndex];
            bucket.RawObservationCount++;

            if (!TryGetAcceptedGoodNumericValue(observation, out var value))
            {
                bucket.RejectedSampleCount++;
                continue;
            }

            accepted++;
            bucket.Accept(value);
            minimum = Math.Min(minimum, value);
            maximum = Math.Max(maximum, value);
            // Incremental arithmetic mean avoids overflowing a finite sample sum.
            mean = mean * ((accepted - 1d) / accepted) + value / accepted;
            first ??= new AssetProcessIntelligenceObservedValue(
                observation.TimestampUtc, value, "sample.first.good.v1");
            last = new AssetProcessIntelligenceObservedValue(
                observation.TimestampUtc, value, "sample.last.good.v1");
        }

        var trend = buckets.Select(bucket => bucket.ToResult()).ToArray();
        var rawCount = observations.Count;
        return new AssetProcessIntelligenceKpiSummary(
            rawCount,
            accepted,
            rawCount - accepted,
            new ReadOnlyDictionary<string, int>(qualityCounts),
            accepted,
            accepted == 0 ? null : minimum,
            accepted == 0 ? null : maximum,
            accepted == 0 ? null : mean,
            first,
            last,
            accepted < 2 ? null : FiniteOrNull(last!.Value - first!.Value),
            Array.AsReadOnly(trend),
            "unknown",
            null);
    }

    public static double? Difference(double? current, double? comparison) =>
        current.HasValue && comparison.HasValue
            ? FiniteOrNull(current.Value - comparison.Value)
            : null;

    public static bool TryGetAcceptedGoodNumericValue(
        AssetProcessIntelligenceRawObservation observation,
        out double value)
    {
        ArgumentNullException.ThrowIfNull(observation);
        value = 0;
        return NormalizeQuality(observation.Quality) == TagQuality.Good.ToString() &&
               TryNumber(observation.Value, out value);
    }

    private static string NormalizeQuality(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "Missing";
        return Enum.TryParse<TagQuality>(text.Trim(), ignoreCase: true, out var value) && Enum.IsDefined(value)
            ? value.ToString()
            : "Unrecognized";
    }

    private static bool TryNumber(HistoricalQueryValue? value, out double number)
    {
        number = 0;
        if (value?.Kind is not (HistoricalValueKind.Int16 or HistoricalValueKind.Int32 or HistoricalValueKind.Int64 or
            HistoricalValueKind.Float or HistoricalValueKind.Double or HistoricalValueKind.Number) ||
            string.IsNullOrWhiteSpace(value.Value))
            return false;

        return double.TryParse(
            value.Value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out number) && double.IsFinite(number);
    }

    private static double? FiniteOrNull(double value) => double.IsFinite(value) ? value : null;
    private sealed class BucketAccumulator(DateTimeOffset fromUtc, DateTimeOffset toUtc)
    {
        private double _minimum = double.PositiveInfinity;
        private double _maximum = double.NegativeInfinity;
        private double _mean;

        public DateTimeOffset FromUtc { get; } = fromUtc;
        public DateTimeOffset ToUtc { get; } = toUtc;
        public int RawObservationCount { get; set; }
        public int AcceptedSampleCount { get; private set; }
        public int RejectedSampleCount { get; set; }

        public void Accept(double value)
        {
            AcceptedSampleCount++;
            _minimum = Math.Min(_minimum, value);
            _maximum = Math.Max(_maximum, value);
            _mean = _mean * ((AcceptedSampleCount - 1d) / AcceptedSampleCount) + value / AcceptedSampleCount;
        }

        public AssetProcessIntelligenceKpiBucket ToResult() => new(
            FromUtc,
            ToUtc,
            RawObservationCount,
            AcceptedSampleCount,
            RejectedSampleCount,
            AcceptedSampleCount == 0 ? null : _minimum,
            AcceptedSampleCount == 0 ? null : _maximum,
            AcceptedSampleCount == 0 ? null : _mean);
    }
}
