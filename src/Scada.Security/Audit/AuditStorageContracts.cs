namespace Scada.Security.Audit;

public sealed record AuditCursor(DateTimeOffset TimestampUtc, Guid Id)
{
    public AuditCursor ToUtc() => new(TimestampUtc.ToUniversalTime(), Id);
}

public sealed record AuditQuery(
    int PageSize = 100,
    DateTimeOffset? FromUtc = null,
    DateTimeOffset? ToUtc = null,
    string? SubjectId = null,
    string? Action = null,
    AuditOutcome? Outcome = null,
    string? TargetKind = null,
    string? TargetId = null,
    string? Area = null,
    string? CorrelationId = null,
    AuditCursor? After = null);

public sealed record AuditPage(
    IReadOnlyCollection<AuditEvent> Events,
    AuditCursor? NextCursor);

public sealed record AuditQueryPolicy(int MaximumPageSize = 1000)
{
    public void Validate()
    {
        if (MaximumPageSize is < 1 or > 10000)
            throw new ArgumentOutOfRangeException(nameof(MaximumPageSize), "Audit maximum page size must be between 1 and 10000.");
    }
}

public sealed record AuditRetentionPolicy(
    bool Enabled = false,
    TimeSpan? MaximumAge = null,
    int BatchSize = 1000,
    TimeSpan? Interval = null,
    int MaximumBatchesPerRun = 100)
{
    public void Validate()
    {
        if (MaximumAge.HasValue && MaximumAge.Value <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(MaximumAge), "Audit maximum age must be positive when configured.");
        if (BatchSize is < 1 or > 100000)
            throw new ArgumentOutOfRangeException(nameof(BatchSize), "Audit retention batch size must be between 1 and 100000.");
        if (Interval.HasValue && Interval.Value <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(Interval), "Audit retention interval must be positive when configured.");
        if (MaximumBatchesPerRun is < 1 or > 10000)
            throw new ArgumentOutOfRangeException(nameof(MaximumBatchesPerRun), "Audit maximum batches per run must be between 1 and 10000.");
    }
}

public sealed record AuditRetentionRunResult(
    bool Executed,
    DateTimeOffset? CutoffUtc,
    int DeletedCount,
    int BatchCount,
    bool BacklogMayRemain);

public sealed record AuditStoreHealthSnapshot(
    long PersistedCount,
    long AppendFailureCount,
    DateTimeOffset? LastPersistedAtUtc,
    DateTimeOffset? LastAppendFailureAtUtc,
    DateTimeOffset? LastRetentionRunAtUtc,
    int LastRetentionDeletedCount);

public interface IAuditRetentionStore
{
    Task<int> ApplyRetentionBatchAsync(
        DateTimeOffset cutoffUtc,
        int batchSize,
        CancellationToken cancellationToken = default);
}

public interface IAuditStoreDiagnostics
{
    AuditStoreHealthSnapshot GetHealthSnapshot();
}

public sealed class AuditRetentionCoordinator
{
    private readonly IAuditRetentionStore _store;
    private readonly AuditRetentionPolicy _policy;

    public AuditRetentionCoordinator(IAuditRetentionStore store, AuditRetentionPolicy policy)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _policy.Validate();
    }

    public async Task<AuditRetentionRunResult> RunOnceAsync(
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_policy.Enabled || !_policy.MaximumAge.HasValue)
            return new AuditRetentionRunResult(false, null, 0, 0, false);

        var cutoffUtc = nowUtc.ToUniversalTime() - _policy.MaximumAge.Value;
        var totalDeleted = 0;
        var batchCount = 0;
        var backlogMayRemain = false;

        while (batchCount < _policy.MaximumBatchesPerRun)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var deleted = await _store.ApplyRetentionBatchAsync(
                cutoffUtc,
                _policy.BatchSize,
                cancellationToken);

            totalDeleted += deleted;
            batchCount++;
            if (deleted < _policy.BatchSize)
            {
                backlogMayRemain = false;
                break;
            }

            backlogMayRemain = true;
        }

        return new AuditRetentionRunResult(
            true,
            cutoffUtc,
            totalDeleted,
            batchCount,
            backlogMayRemain);
    }

    public async Task RunPeriodicAsync(
        Func<DateTimeOffset>? utcNow = null,
        CancellationToken cancellationToken = default)
    {
        if (!_policy.Interval.HasValue)
            throw new InvalidOperationException("Audit retention Interval must be configured for periodic execution.");

        var clock = utcNow ?? (() => DateTimeOffset.UtcNow);
        while (!cancellationToken.IsCancellationRequested)
        {
            await RunOnceAsync(clock(), cancellationToken);
            await Task.Delay(_policy.Interval.Value, cancellationToken);
        }
    }
}

public static class AuditQueryValidator
{
    public static AuditQuery ValidateAndNormalize(AuditQuery query, AuditQueryPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(policy);
        policy.Validate();

        if (query.PageSize < 1 || query.PageSize > policy.MaximumPageSize)
            throw new ArgumentOutOfRangeException(nameof(query.PageSize), $"Audit page size must be between 1 and {policy.MaximumPageSize}.");
        if (query.FromUtc.HasValue && query.ToUtc.HasValue && query.FromUtc.Value > query.ToUtc.Value)
            throw new ArgumentException("Audit query FromUtc must not be later than ToUtc.", nameof(query));
        if (query.After is { Id: var id } && id == Guid.Empty)
            throw new ArgumentException("Audit cursor ID cannot be empty.", nameof(query));

        return query with
        {
            FromUtc = query.FromUtc?.ToUniversalTime(),
            ToUtc = query.ToUtc?.ToUniversalTime(),
            SubjectId = Normalize(query.SubjectId),
            Action = Normalize(query.Action),
            TargetKind = Normalize(query.TargetKind),
            TargetId = Normalize(query.TargetId),
            Area = Normalize(query.Area),
            CorrelationId = Normalize(query.CorrelationId),
            After = query.After?.ToUtc()
        };
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
