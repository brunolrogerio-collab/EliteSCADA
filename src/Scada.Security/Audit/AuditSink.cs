using System.Collections.Concurrent;

namespace Scada.Security.Audit;

public interface IAuditSink
{
    ValueTask WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default);
}

public sealed class InMemoryAuditSink : IAuditSink
{
    private readonly ConcurrentQueue<AuditEvent> _events = new();

    public ValueTask WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        cancellationToken.ThrowIfCancellationRequested();
        _events.Enqueue(auditEvent);
        return ValueTask.CompletedTask;
    }

    public IReadOnlyCollection<AuditEvent> Snapshot() =>
        _events.OrderBy(x => x.TimestampUtc).ThenBy(x => x.Id).ToArray();

    public void Clear()
    {
        while (_events.TryDequeue(out _))
        {
        }
    }
}
