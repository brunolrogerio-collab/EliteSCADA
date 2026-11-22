namespace Scada.Historian.Policies;

/// <summary>
/// Measured storage usage reported by the configured Historian database.
/// Filesystem capacity is intentionally not inferred from database size.
/// </summary>
public sealed record HistorianStorageUsage(
    long DatabaseBytes,
    long? RawSamplesBytes,
    long? AggregateBytes,
    DateTimeOffset MeasuredAtUtc)
{
    public long? HistorianBytes => RawSamplesBytes.HasValue && AggregateBytes.HasValue
        ? checked(RawSamplesBytes.Value + AggregateBytes.Value)
        : null;
}
