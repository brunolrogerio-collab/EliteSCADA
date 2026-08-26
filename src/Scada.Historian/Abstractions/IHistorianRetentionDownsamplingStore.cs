using Scada.Historian.Aggregation;
using Scada.Historian.Policies;

namespace Scada.Historian.Abstractions;

public interface IHistorianRetentionDownsamplingStore : IAsyncDisposable
{
    Task EnsureInfrastructureAsync(CancellationToken cancellationToken = default);

    Task<HistorianStoragePolicy?> GetAppliedPolicyAsync(CancellationToken cancellationToken = default);

    Task ApplyPolicyAsync(
        HistorianStoragePolicy policy,
        HistorianPolicyApplyOptions? options = null,
        CancellationToken cancellationToken = default);

    Task RefreshAggregateAsync(
        HistorianBucketWidth bucket,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HistorianAggregateBucket>> QueryAggregatesAsync(
        Guid tagId,
        HistorianBucketWidth bucket,
        DateTimeOffset from,
        DateTimeOffset to,
        int limit = 5000,
        CancellationToken cancellationToken = default);
}
