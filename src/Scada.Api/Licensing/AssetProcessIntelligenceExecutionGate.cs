using Scada.Engineering.Contracts;

namespace Scada.Api.Licensing;

public sealed record AssetProcessIntelligenceExecutionPermitSnapshot(
    long Revision,
    long AuthorityPolicyVersion,
    long HaEpoch);

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

    public AssetProcessIntelligenceExecutionPermitSnapshot? Describe(
        string projectKey,
        AssetProcessIntelligenceScopeKind kind,
        Guid scopeId)
    {
        ScopeKey key;
        try { key = ValidateScopeKey(projectKey, kind, scopeId); }
        catch (ArgumentException) { return null; }

        lock (_sync)
        {
            if (!_permits.TryGetValue(key, out var permit) || permit.Cancellation.IsCancellationRequested)
                return null;
            return new AssetProcessIntelligenceExecutionPermitSnapshot(
                permit.Revision,
                permit.AuthorityPolicyVersion,
                permit.HaEpoch);
        }
    }

    public void Activate(
        string projectKey,
        long activeRevision,
        AssetProcessIntelligenceScopeKind kind,
        Guid scopeId,
        long authorityPolicyVersion = 0,
        long haEpoch = 0)
    {
        ValidateFence(authorityPolicyVersion, haEpoch);
        var key = ValidateKey(projectKey, activeRevision, kind, scopeId);
        lock (_sync)
        {
            RemoveAndCancel(key);
            _permits[key] = new Permit(activeRevision, authorityPolicyVersion, haEpoch, new CancellationTokenSource());
        }
    }

    public bool TryGetToken(
        string projectKey,
        long activeRevision,
        AssetProcessIntelligenceScopeKind kind,
        Guid scopeId,
        out CancellationToken cancellationToken,
        long authorityPolicyVersion = 0,
        long haEpoch = 0)
    {
        cancellationToken = new CancellationToken(canceled: true);
        try { ValidateFence(authorityPolicyVersion, haEpoch); }
        catch (ArgumentOutOfRangeException) { return false; }
        ScopeKey key;
        try { key = ValidateScopeKey(projectKey, kind, scopeId); }
        catch (ArgumentException) { return false; }

        lock (_sync)
        {
            if (!_permits.TryGetValue(key, out var permit) ||
                permit.Revision != activeRevision ||
                permit.AuthorityPolicyVersion != authorityPolicyVersion ||
                permit.HaEpoch != haEpoch ||
                permit.Cancellation.IsCancellationRequested)
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
        if (activeRevision <= 0) throw new ArgumentOutOfRangeException(nameof(activeRevision));
        return ValidateScopeKey(projectKey, kind, scopeId);
    }

    private static ScopeKey ValidateScopeKey(
        string projectKey,
        AssetProcessIntelligenceScopeKind kind,
        Guid scopeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectKey);
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        if (scopeId == Guid.Empty) throw new ArgumentException("Intelligence scope identity is required.", nameof(scopeId));
        return new ScopeKey(projectKey.Trim().ToUpperInvariant(), kind, scopeId);
    }

    private static void ValidateFence(long authorityPolicyVersion, long haEpoch)
    {
        if (authorityPolicyVersion < 0) throw new ArgumentOutOfRangeException(nameof(authorityPolicyVersion));
        if (haEpoch < 0) throw new ArgumentOutOfRangeException(nameof(haEpoch));
    }

    private readonly record struct ScopeKey(
        string ProjectKey,
        AssetProcessIntelligenceScopeKind Kind,
        Guid ScopeId);

    private sealed record Permit(
        long Revision,
        long AuthorityPolicyVersion,
        long HaEpoch,
        CancellationTokenSource Cancellation);
}
