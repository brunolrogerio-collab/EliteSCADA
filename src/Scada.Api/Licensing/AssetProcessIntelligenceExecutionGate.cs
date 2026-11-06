using Scada.Engineering.Contracts;
using Scada.Security.Authentication;

namespace Scada.Api.Licensing;

public sealed record AssetProcessIntelligenceExecutionPermitSnapshot(
    long Revision,
    long AuthorityPolicyVersion,
    long HaEpoch,
    long Generation = 0);

/// <summary>
/// Process-local, fail-closed permits for future Intelligence workers. No worker is
/// registered in Stage 1. An Engineering active flag alone never creates a permit;
/// restart recovery requires an exact durable Authority receipt and current fences.
/// </summary>
public sealed class AssetProcessIntelligenceExecutionGate
{
    private readonly object _sync = new();
    private readonly Dictionary<ScopeKey, Permit> _permits = new();
    private bool _startupRecoveryClosed;
    private bool _startupRecoveryPending;
    private Action? _persistInvalidation;
    private Action<string, long>? _persistRevisionInvalidation;

    /// <summary>
    /// Startup-only recovery after the caller atomically verifies the durable receipt.
    /// Any local invalidation or explicit transition closes recovery for this process.
    /// </summary>
    public bool TryRecoverAtStartup(
        AssetProcessIntelligenceActivationEvidence evidence,
        AuthorityLifecycleSnapshot authority,
        string projectKey, long revision, AssetProcessIntelligenceScopeKind kind, Guid scopeId,
        long policyVersion, string policyDigest, long haEpoch, long generation)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentNullException.ThrowIfNull(authority);
        if (!evidence.Matches(authority, projectKey, revision, kind, scopeId,
                policyVersion, policyDigest, haEpoch, generation)) return false;
        var key = ValidateKey(projectKey, revision, kind, scopeId);
        lock (_sync)
        {
            if (_startupRecoveryClosed || _permits.ContainsKey(key)) return false;
            _permits[key] = new Permit(revision, policyVersion, haEpoch, new CancellationTokenSource(), generation);
            return true;
        }
    }

    /// <summary>Install once after durable storage initialization, before accepting requests.</summary>
    public void ConfigureDurableInvalidation(Action persistInvalidation, Action<string, long>? persistRevisionInvalidation = null)
    {
        ArgumentNullException.ThrowIfNull(persistInvalidation);
        lock (_sync)
        {
            if (_persistInvalidation is not null)
                throw new InvalidOperationException("Durable Intelligence invalidation is already configured.");
            _persistInvalidation = persistInvalidation;
            _persistRevisionInvalidation = persistRevisionInvalidation;
        }
    }
    public void CompleteStartupRecovery()
    {
        lock (_sync)
        {
            _startupRecoveryClosed = true;
            _startupRecoveryPending = false;
        }
    }

    public void BeginStartupRecovery()
    {
        lock (_sync)
        {
            if (!_startupRecoveryClosed)
                _startupRecoveryPending = true;
        }
    }

    public bool StartupRecoveryPending
    {
        get { lock (_sync) return _startupRecoveryPending && !_startupRecoveryClosed; }
    }

    /// <summary>
    /// Closes current permits when Authority/HA readiness is temporarily unknown.
    /// During initial host startup there are no local permits to revoke, so keep
    /// the durable activation receipt available for the first exact, fenced
    /// recovery attempt. Once a permit exists, loss of readiness is a durable
    /// invalidation and cannot be undone by a later reconnect.
    /// </summary>
    public void InvalidateForAuthorityReadinessFailure()
    {
        Action? persist = null;
        lock (_sync)
        {
            var hadPermits = _permits.Count > 0;
            foreach (var key in _permits.Keys.ToArray())
                RemoveAndCancel(key);
            if (hadPermits)
            {
                _startupRecoveryClosed = true;
                _startupRecoveryPending = false;
                persist = _persistInvalidation;
            }
        }

        // Local tokens close before durable I/O. Before this process has ever
        // armed a permit, a readiness miss is only a startup ordering condition;
        // the recovery routine still requires the exact persisted Authority,
        // policy, revision and HA fence before it can create a new permit.
        persist?.Invoke();
    }
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
                permit.HaEpoch, permit.Generation);
        }
    }

    public void Activate(
        string projectKey,
        long activeRevision,
        AssetProcessIntelligenceScopeKind kind,
        Guid scopeId,
        long authorityPolicyVersion = 0,
        long haEpoch = 0, long generation = 0)
    {
        ValidateFence(authorityPolicyVersion, haEpoch);
        var key = ValidateKey(projectKey, activeRevision, kind, scopeId);
        lock (_sync)
        {
            _startupRecoveryClosed = true;
            _startupRecoveryPending = false;
            RemoveAndCancel(key);
            _permits[key] = new Permit(activeRevision, authorityPolicyVersion, haEpoch, new CancellationTokenSource(), generation);
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
        {
            _startupRecoveryClosed = true;
            _startupRecoveryPending = false;
            RemoveAndCancel(new ScopeKey(projectKey.Trim().ToUpperInvariant(), kind, scopeId));
        }
    }

    public void InvalidateProject(string projectKey)
    {
        if (string.IsNullOrWhiteSpace(projectKey)) return;
        lock (_sync)
        {
            _startupRecoveryClosed = true;
            _startupRecoveryPending = false;
            foreach (var key in _permits.Keys.Where(key =>
                         key.ProjectKey.Equals(projectKey.Trim(), StringComparison.OrdinalIgnoreCase)).ToArray())
                RemoveAndCancel(key);
        }
    }

    public void InvalidateForRevision(string projectKey, long revision)
    {
        Action<string, long>? revisionInvalidation;
        Action? allInvalidation;
        lock (_sync)
        {
            _startupRecoveryClosed = true;
            _startupRecoveryPending = false;
            foreach (var key in _permits.Keys.ToArray()) RemoveAndCancel(key);
            revisionInvalidation = _persistRevisionInvalidation;
            allInvalidation = _persistInvalidation;
        }
        if (revisionInvalidation is not null) revisionInvalidation(projectKey, revision);
        else allInvalidation?.Invoke();
    }
    public void InvalidateAll()
    {
        Action? persist;
        lock (_sync)
        {
            _startupRecoveryClosed = true;
            _startupRecoveryPending = false;
            foreach (var key in _permits.Keys.ToArray())
                RemoveAndCancel(key);
            persist = _persistInvalidation;
        }
        // Local tokens close before durable I/O; a write failure must propagate.
        // Do not hold the gate lock while acquiring the canonical DB lock.
        persist?.Invoke();
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
        CancellationTokenSource Cancellation,
        long Generation = 0);
}
