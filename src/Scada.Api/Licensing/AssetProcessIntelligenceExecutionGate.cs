using Scada.Engineering.Contracts;

namespace Scada.Api.Licensing;

/// <summary>
/// Process-local, fail-closed permits for future Intelligence workers. No worker is
/// registered in Stage 1. Persisted Engineering activation alone never recreates a
/// permit after restart; a manager must explicitly activate it against the live revision.
/// </summary>
public sealed class AssetProcessIntelligenceExecutionGate
{
    private readonly object _sync = new();
    private readonly Dictionary<ScopeKey, Permit> _permits = new();

    public int ActivePermitCount
    {
        get { lock (_sync) return _permits.Count; }
    }

    public void Activate(
        string projectKey,
        long activeRevision,
        AssetProcessIntelligenceScopeKind kind,
        Guid scopeId)
    {
        var key = ValidateKey(projectKey, activeRevision, kind, scopeId);
        lock (_sync)
        {
            RemoveAndCancel(key);
            _permits[key] = new Permit(activeRevision, new CancellationTokenSource());
        }
    }

    public bool TryGetToken(
        string projectKey,
        long activeRevision,
        AssetProcessIntelligenceScopeKind kind,
        Guid scopeId,
        out CancellationToken cancellationToken)
    {
        cancellationToken = new CancellationToken(canceled: true);
        ScopeKey key;
        try { key = ValidateKey(projectKey, activeRevision, kind, scopeId); }
        catch (ArgumentException) { return false; }

        lock (_sync)
        {
            if (!_permits.TryGetValue(key, out var permit) ||
                permit.Revision != activeRevision || permit.Cancellation.IsCancellationRequested)
                return false;
            cancellationToken = permit.Cancellation.Token;
            return true;
        }
    }

    public void Pause(
        string projectKey,
        AssetProcessIntelligenceScopeKind kind,
        Guid scopeId)
    {
        if (string.IsNullOrWhiteSpace(projectKey) || scopeId == Guid.Empty || !Enum.IsDefined(kind))
            return;
        lock (_sync)
            RemoveAndCancel(new ScopeKey(projectKey.Trim().ToUpperInvariant(), kind, scopeId));
    }

    public void InvalidateProject(string projectKey)
    {
        if (string.IsNullOrWhiteSpace(projectKey)) return;
        lock (_sync)
        {
            foreach (var key in _permits.Keys.Where(key =>
                         key.ProjectKey.Equals(projectKey.Trim(), StringComparison.OrdinalIgnoreCase)).ToArray())
                RemoveAndCancel(key);
        }
    }

    public void InvalidateAll()
    {
        lock (_sync)
            foreach (var key in _permits.Keys.ToArray())
                RemoveAndCancel(key);
    }

    private void RemoveAndCancel(ScopeKey key)
    {
        if (!_permits.Remove(key, out var permit)) return;
        try { permit.Cancellation.Cancel(); }
        finally { permit.Cancellation.Dispose(); }
    }

    private static ScopeKey ValidateKey(
        string projectKey,
        long activeRevision,
        AssetProcessIntelligenceScopeKind kind,
        Guid scopeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectKey);
        if (activeRevision <= 0) throw new ArgumentOutOfRangeException(nameof(activeRevision));
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        if (scopeId == Guid.Empty) throw new ArgumentException("Intelligence scope identity is required.", nameof(scopeId));
        return new ScopeKey(projectKey.Trim().ToUpperInvariant(), kind, scopeId);
    }

    private readonly record struct ScopeKey(
        string ProjectKey,
        AssetProcessIntelligenceScopeKind Kind,
        Guid ScopeId);

    private sealed record Permit(long Revision, CancellationTokenSource Cancellation);
}
