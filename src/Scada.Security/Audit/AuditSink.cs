namespace Scada.Security.Audit;

public interface IAuditSink
{
    ValueTask WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default);
}

public interface IAuditStore : IAuditSink, IAuditRetentionStore, IAuditStoreDiagnostics
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<AuditPage> QueryPageAsync(
        AuditQuery query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<AuditEvent>> QueryAsync(
        int limit = 100,
        string? subjectId = null,
        string? action = null,
        AuditOutcome? outcome = null,
        DateTimeOffset? fromUtc = null,
        DateTimeOffset? toUtc = null,
        CancellationToken cancellationToken = default);
}

public sealed class InMemoryAuditSink : IAuditStore
{
    private readonly object _gate = new();
    private readonly List<AuditEvent> _events = new();
    private readonly AuditQueryPolicy _queryPolicy;
    private long _persistedCount;
    private DateTimeOffset? _lastPersistedAtUtc;
    private DateTimeOffset? _lastRetentionRunAtUtc;
    private int _lastRetentionDeletedCount;

    public InMemoryAuditSink(AuditQueryPolicy? queryPolicy = null)
    {
        _queryPolicy = queryPolicy ?? new AuditQueryPolicy();
        _queryPolicy.Validate();
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public ValueTask WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalized = AuditSanitizer.Normalize(auditEvent);
        Validate(normalized);

        lock (_gate)
        {
            if (_events.Any(existing => existing.Id == normalized.Id))
                throw new InvalidOperationException($"Audit event '{normalized.Id}' already exists.");

            _events.Add(normalized);
            _persistedCount++;
            _lastPersistedAtUtc = DateTimeOffset.UtcNow;
        }

        return ValueTask.CompletedTask;
    }

    public Task<AuditPage> QueryPageAsync(
        AuditQuery query,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalized = AuditQueryValidator.ValidateAndNormalize(query, _queryPolicy);

        AuditEvent[] snapshot;
        lock (_gate) snapshot = _events.ToArray();

        IEnumerable<AuditEvent> filtered = snapshot;
        if (normalized.SubjectId is not null)
            filtered = filtered.Where(x => x.SubjectId.Equals(normalized.SubjectId, StringComparison.Ordinal));
        if (normalized.Action is not null)
            filtered = filtered.Where(x => x.Action.Equals(normalized.Action, StringComparison.Ordinal));
        if (normalized.Outcome.HasValue)
            filtered = filtered.Where(x => x.Outcome == normalized.Outcome.Value);
        if (normalized.TargetKind is not null)
            filtered = filtered.Where(x => x.TargetKind.Equals(normalized.TargetKind, StringComparison.Ordinal));
        if (normalized.TargetId is not null)
            filtered = filtered.Where(x => x.TargetId.Equals(normalized.TargetId, StringComparison.Ordinal));
        if (normalized.Area is not null)
            filtered = filtered.Where(x => string.Equals(x.Area, normalized.Area, StringComparison.Ordinal));
        if (normalized.CorrelationId is not null)
            filtered = filtered.Where(x => string.Equals(x.CorrelationId, normalized.CorrelationId, StringComparison.Ordinal));
        if (normalized.FromUtc.HasValue)
            filtered = filtered.Where(x => x.TimestampUtc >= normalized.FromUtc.Value);
        if (normalized.ToUtc.HasValue)
            filtered = filtered.Where(x => x.TimestampUtc <= normalized.ToUtc.Value);
        if (normalized.After is not null)
        {
            filtered = filtered.Where(x =>
                x.TimestampUtc < normalized.After.TimestampUtc ||
                (x.TimestampUtc == normalized.After.TimestampUtc && x.Id.CompareTo(normalized.After.Id) < 0));
        }

        var ordered = filtered
            .OrderByDescending(x => x.TimestampUtc)
            .ThenByDescending(x => x.Id)
            .Take(normalized.PageSize + 1)
            .ToArray();
        var hasMore = ordered.Length > normalized.PageSize;
        var events = ordered.Take(normalized.PageSize).ToArray();
        var nextCursor = hasMore && events.Length > 0
            ? new AuditCursor(events[^1].TimestampUtc, events[^1].Id)
            : null;

        return Task.FromResult(new AuditPage(events, nextCursor));
    }

    public async Task<IReadOnlyCollection<AuditEvent>> QueryAsync(
        int limit = 100,
        string? subjectId = null,
        string? action = null,
        AuditOutcome? outcome = null,
        DateTimeOffset? fromUtc = null,
        DateTimeOffset? toUtc = null,
        CancellationToken cancellationToken = default)
    {
        var page = await QueryPageAsync(
            new AuditQuery(
                PageSize: limit,
                FromUtc: fromUtc,
                ToUtc: toUtc,
                SubjectId: subjectId,
                Action: action,
                Outcome: outcome),
            cancellationToken);
        return page.Events;
    }

    public Task<int> ApplyRetentionBatchAsync(
        DateTimeOffset cutoffUtc,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (batchSize is < 1 or > 100000)
            throw new ArgumentOutOfRangeException(nameof(batchSize));

        var cutoff = cutoffUtc.ToUniversalTime();
        int deleted;
        lock (_gate)
        {
            var ids = _events
                .Where(x => x.TimestampUtc < cutoff)
                .OrderBy(x => x.TimestampUtc)
                .ThenBy(x => x.Id)
                .Take(batchSize)
                .Select(x => x.Id)
                .ToHashSet();
            deleted = _events.RemoveAll(x => ids.Contains(x.Id));
            _lastRetentionRunAtUtc = DateTimeOffset.UtcNow;
            _lastRetentionDeletedCount = deleted;
        }

        return Task.FromResult(deleted);
    }

    public AuditStoreHealthSnapshot GetHealthSnapshot()
    {
        lock (_gate)
        {
            return new AuditStoreHealthSnapshot(
                _persistedCount,
                0,
                _lastPersistedAtUtc,
                null,
                _lastRetentionRunAtUtc,
                _lastRetentionDeletedCount);
        }
    }

    public IReadOnlyCollection<AuditEvent> Snapshot()
    {
        lock (_gate) return _events.ToArray();
    }

    public void Clear()
    {
        lock (_gate) _events.Clear();
    }

    private static void Validate(AuditEvent auditEvent)
    {
        if (auditEvent.Id == Guid.Empty)
            throw new ArgumentException("Audit event ID is required.", nameof(auditEvent));
        if (string.IsNullOrWhiteSpace(auditEvent.SubjectId))
            throw new ArgumentException("Audit subject ID is required.", nameof(auditEvent));
        if (string.IsNullOrWhiteSpace(auditEvent.Action))
            throw new ArgumentException("Audit action is required.", nameof(auditEvent));
        if (string.IsNullOrWhiteSpace(auditEvent.TargetKind))
            throw new ArgumentException("Audit target kind is required.", nameof(auditEvent));
        if (string.IsNullOrWhiteSpace(auditEvent.TargetId))
            throw new ArgumentException("Audit target ID is required.", nameof(auditEvent));
        if (!Enum.IsDefined(auditEvent.Outcome))
            throw new ArgumentOutOfRangeException(nameof(auditEvent), "Audit outcome is invalid.");
    }
}
