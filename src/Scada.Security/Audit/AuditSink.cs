using System.Collections.Concurrent;

namespace Scada.Security.Audit;

public interface IAuditSink
{
    ValueTask WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default);
}

public interface IAuditStore : IAuditSink
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

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
    private readonly ConcurrentQueue<AuditEvent> _events = new();

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public ValueTask WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        cancellationToken.ThrowIfCancellationRequested();
        _events.Enqueue(auditEvent);
        return ValueTask.CompletedTask;
    }

    public Task<IReadOnlyCollection<AuditEvent>> QueryAsync(
        int limit = 100,
        string? subjectId = null,
        string? action = null,
        AuditOutcome? outcome = null,
        DateTimeOffset? fromUtc = null,
        DateTimeOffset? toUtc = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (limit is < 1 or > 1000)
            throw new ArgumentOutOfRangeException(nameof(limit), "Audit query limit must be between 1 and 1000.");
        if (fromUtc.HasValue && toUtc.HasValue && fromUtc > toUtc)
            throw new ArgumentException("Audit query fromUtc must not be later than toUtc.");

        IEnumerable<AuditEvent> query = _events;
        if (!string.IsNullOrWhiteSpace(subjectId))
            query = query.Where(x => x.SubjectId.Equals(subjectId.Trim(), StringComparison.Ordinal));
        if (!string.IsNullOrWhiteSpace(action))
            query = query.Where(x => x.Action.Equals(action.Trim(), StringComparison.Ordinal));
        if (outcome.HasValue)
            query = query.Where(x => x.Outcome == outcome.Value);
        if (fromUtc.HasValue)
            query = query.Where(x => x.TimestampUtc >= fromUtc.Value);
        if (toUtc.HasValue)
            query = query.Where(x => x.TimestampUtc <= toUtc.Value);

        IReadOnlyCollection<AuditEvent> result = query
            .OrderByDescending(x => x.TimestampUtc)
            .ThenByDescending(x => x.Id)
            .Take(limit)
            .ToArray();
        return Task.FromResult(result);
    }

    public IReadOnlyCollection<AuditEvent> Snapshot() => _events.ToArray();

    public void Clear()
    {
        while (_events.TryDequeue(out _))
        {
        }
    }
}
