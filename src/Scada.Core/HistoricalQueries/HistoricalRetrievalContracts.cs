using Scada.Core.Tags;

namespace Scada.Core.HistoricalQueries;

/// <summary>
/// Runtime/provider provenance for historical retrieval. This is not an Engineering
/// persistence wire contract; the frozen F0 DataQuery contract remains authoritative.
/// </summary>
public enum HistoricalRetrievalProvenanceKind
{
    Measured,
    Interpolated,
    Held,
    Gap,
    Aggregate
}

/// <summary>
/// Provider-internal aggregate operation mapped one-to-one from the frozen Engineering
/// DataQueryAggregateFunction by the Data Query execution service. It is never persisted.
/// </summary>
public enum HistoricalAggregateOperation
{
    Count,
    Sum,
    Average,
    Minimum,
    Maximum,
    First,
    Last
}

public sealed record HistoricalRetrievalProvenance(
    HistoricalRetrievalProvenanceKind Kind,
    IReadOnlyList<DateTimeOffset> SourceTimestampsUtc,
    DateTimeOffset? BucketStartUtc = null,
    DateTimeOffset? BucketEndUtc = null,
    long? SampleCount = null,
    long? GoodCount = null,
    long? UncertainCount = null,
    long? BadCount = null,
    TagQuality? DerivedQuality = null,
    string? Reason = null);

public sealed record HistoricalRetrievalPoint(
    Guid TagId,
    string? TagPath,
    DateTimeOffset TimestampUtc,
    HistoricalQueryValue Value,
    TagQuality? Quality,
    TagDataType? DataType,
    HistoricalRetrievalProvenance Provenance);

public sealed record HistoricalRetrievalResult(
    HistoricalResolvedRange Range,
    IReadOnlyList<HistoricalRetrievalPoint> Points);

/// <summary>
/// Server-side historian retrieval authority consumed by reusable Data Query execution.
/// Implementations must remain bounded and read-only.
/// </summary>
public interface IHistoricalRetrievalProvider
{
    string Dataset { get; }

    Task<HistoricalRetrievalResult> QueryLastAsync(
        HistoricalQueryExecution query,
        CancellationToken cancellationToken = default);

    Task<HistoricalRetrievalResult> QueryAtOrBeforeAsync(
        HistoricalQueryExecution query,
        DateTimeOffset targetUtc,
        CancellationToken cancellationToken = default);

    Task<HistoricalRetrievalResult> QueryAtOrAfterAsync(
        HistoricalQueryExecution query,
        DateTimeOffset targetUtc,
        CancellationToken cancellationToken = default);

    Task<HistoricalRetrievalResult> QueryExactAsync(
        HistoricalQueryExecution query,
        DateTimeOffset targetUtc,
        CancellationToken cancellationToken = default);

    Task<HistoricalRetrievalResult> QueryInterpolatedAsync(
        HistoricalQueryExecution query,
        DateTimeOffset targetUtc,
        int maximumGapMilliseconds,
        CancellationToken cancellationToken = default);

    Task<HistoricalRetrievalResult> QuerySampledFixedStepAsync(
        HistoricalQueryExecution query,
        int stepMilliseconds,
        int maximumGapMilliseconds,
        CancellationToken cancellationToken = default);

    Task<HistoricalRetrievalResult> QueryAggregateAsync(
        HistoricalQueryExecution query,
        int bucketMilliseconds,
        HistoricalAggregateOperation operation,
        CancellationToken cancellationToken = default);
}
