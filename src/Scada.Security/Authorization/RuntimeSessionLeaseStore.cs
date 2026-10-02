namespace Scada.Security.Authorization;

/// <summary>
/// Server-owned Runtime identity captured by a logical session lease. This state is deployment
/// data and must never be exported as Engineering/package content.
/// </summary>
public sealed record RuntimeSessionRuntimeIdentity(
    string Mode,
    string? ProjectKey,
    long? Revision,
    DateTimeOffset? ActivatedAtUtc)
{
    // PostgreSQL timestamptz persists microseconds. Runtime activation is part of
    // the durable lease identity, so compare it at that storage precision rather
    // than treating a harmless sub-microsecond truncation as a runtime change.
    public RuntimeSessionRuntimeIdentity NormalizeStoragePrecision() =>
        ActivatedAtUtc is { } activatedAtUtc
            ? this with { ActivatedAtUtc = RuntimeDemoSessionAnchor.Normalize(activatedAtUtc) }
            : this;

    public bool Matches(RuntimeSessionRuntimeIdentity other) =>
        Revision == other.Revision &&
        NormalizeStoragePrecision().ActivatedAtUtc == other.NormalizeStoragePrecision().ActivatedAtUtc &&
        string.Equals(Mode, other.Mode, StringComparison.Ordinal) &&
        string.Equals(ProjectKey, other.ProjectKey, StringComparison.OrdinalIgnoreCase);
}

public sealed record RuntimeSessionLeaseAdmission(
    string SubjectId,
    string ClientInstanceId,
    string GrantedConnectionClass,
    RuntimeSessionRuntimeIdentity Runtime,
    TimeSpan LeaseDuration,
    string? ServerNode = null,
    string? ClusterId = null);

public sealed record RuntimeSessionLeaseState(
    Guid SessionId,
    string SubjectId,
    string ClientInstanceId,
    string GrantedConnectionClass,
    long Generation,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset LastHeartbeatUtc,
    DateTimeOffset ExpiresAtUtc,
    RuntimeSessionRuntimeIdentity Runtime,
    string? ServerNode,
    string? ClusterId,
    bool IsActive,
    long AuthorityRevision = 1);

/// <summary>
/// Durable termination/expiry evidence imported from an authenticated HA peer. Tombstones
/// live in the same canonical session ledger boundary so a replicated lease can never be
/// resurrected through a separate HA-only quota store.
/// </summary>
public sealed record RuntimeSessionLeaseTombstoneState(
    string ClusterId,
    string SubjectId,
    string ClientInstanceId,
    Guid SessionId,
    long Generation,
    long AuthorityRevision,
    DateTimeOffset IssuedAtUtc,
    string SourceNode,
    DateTimeOffset RecordedAtUtc,
    string ReasonCode);

public sealed record RuntimeSessionReplicationMutationResult(
    bool Accepted,
    string ReasonCode,
    RuntimeSessionLeaseState? Lease = null)
{
    public static RuntimeSessionReplicationMutationResult Accept(
        string reasonCode,
        RuntimeSessionLeaseState? lease = null) =>
        new(true, reasonCode, lease);

    public static RuntimeSessionReplicationMutationResult Reject(string reasonCode) =>
        new(false, reasonCode, null);
}

public sealed record RuntimeAuthorityState(
    long AuthorityRevision,
    bool TransitionPending,
    Guid? TransitionId,
    string? TransitionKind,
    DateTimeOffset? TransitionStartedAtUtc,
    long? TransitionBaseAuthorityRevision,
    DateTimeOffset? AuthorityChangedAtUtc,
    DateTimeOffset? DemoStartedAtUtc,
    bool SupportsDurableRecovery);

public sealed record RuntimeAuthorityTransition(
    Guid TransitionId,
    long BaseAuthorityRevision,
    string Kind,
    DateTimeOffset StartedAtUtc);

/// <summary>
/// Result of the durable Demo-session anchor compare-and-set operation. This is
/// deliberately independent from license authority revision changes: starting
/// a Demo Runtime must not masquerade as a license transition.
/// </summary>
public sealed record RuntimeDemoSessionAnchorResult(
    DateTimeOffset DemoStartedAtUtc,
    bool WasEstablished);

public static class RuntimeDemoSessionAnchor
{
    // PostgreSQL timestamptz is microsecond-precise. Normalizing at the shared
    // boundary keeps the result of a successful CAS byte-for-byte equal to a
    // later read, including when another process wins the next comparison.
    public static DateTimeOffset Normalize(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        return new DateTimeOffset(utc.Ticks - utc.Ticks % 10, TimeSpan.Zero);
    }
}

public sealed record RuntimeSessionLeaseStoreResult(
    bool IsValid,
    RuntimeSessionLeaseState? Lease,
    string? FailureCode)
{
    public static RuntimeSessionLeaseStoreResult Valid(RuntimeSessionLeaseState lease) =>
        new(true, lease, null);

    public static RuntimeSessionLeaseStoreResult Invalid(string failureCode) =>
        new(false, null, failureCode);
}

/// <summary>Signed/effective remote-client totals supplied by the host licensing boundary.</summary>
public sealed record RuntimeSessionSeatCapacity(int InteractiveSeats, int ViewOnlySeats)
{
    public void Validate()
    {
        if (InteractiveSeats < 0) throw new ArgumentOutOfRangeException(nameof(InteractiveSeats));
        if (ViewOnlySeats < 0) throw new ArgumentOutOfRangeException(nameof(ViewOnlySeats));
    }
}

public sealed record RuntimeSessionLeaseCapacityAdmission(
    RuntimeSessionLeaseAdmission Lease,
    RuntimeSessionSeatCapacity Capacity,
    long ExpectedAuthorityRevision);

/// <summary>
/// Stable, host-visible outcome codes for the atomic shared-seat reservation.  Authority
/// eligibility is reported separately by the Runtime admission policy; these codes only
/// describe the final capacity mutation performed by the single logical lease ledger.
/// </summary>
public enum RuntimeSessionSeatReservationReasonCode
{
    ExistingLeaseRetained,
    InteractiveReserved,
    AuthorityDownscopeViewOnlyReserved,
    ViewOnlyReserved,
    InteractiveQuotaFallbackViewOnly,
    ViewOnlyQuotaExhausted,
    InteractiveQuotaExhaustedNoEligibleViewOnly,
    EligiblePoolsExhausted,
    AuthorityTransitionPending,
    AuthorityRevisionChanged
}

public sealed record RuntimeSessionLeaseCapacityAdmissionResult(
    bool IsAdmitted,
    RuntimeSessionLeaseState? Lease,
    RuntimeSessionSeatReservationReasonCode ReasonCode)
{
    public static RuntimeSessionLeaseCapacityAdmissionResult Admitted(RuntimeSessionLeaseState lease, RuntimeSessionSeatReservationReasonCode reasonCode) =>
        new(true, lease, reasonCode);

    public static RuntimeSessionLeaseCapacityAdmissionResult Rejected(RuntimeSessionSeatReservationReasonCode reasonCode) =>
        new(false, null, reasonCode);
}

/// <summary>
/// Durable boundary for logical Runtime leases. Lease identity is subject plus ClientInstanceId;
/// transports are deliberately not represented here.
/// </summary>
public interface IRuntimeSessionLeaseStore : IAsyncDisposable
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<RuntimeAuthorityState> GetAuthorityStateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Establishes or replaces the durable Demo-session start only when the
    /// current value still matches <paramref name="expectedExistingStartedAtUtc"/>.
    /// A concurrent writer is returned unchanged so callers can re-evaluate the
    /// authoritative remaining allowance without resetting it.
    /// </summary>
    Task<RuntimeDemoSessionAnchorResult> EstablishDemoSessionAnchorAsync(
        DateTimeOffset requestedStartedAtUtc,
        DateTimeOffset? expectedExistingStartedAtUtc,
        CancellationToken cancellationToken = default);

    Task<RuntimeAuthorityTransition> BeginAuthorityTransitionAsync(
        string kind,
        DateTimeOffset startedAtUtc,
        CancellationToken cancellationToken = default);

    Task<bool> AbortAuthorityTransitionAsync(
        Guid transitionId,
        long expectedBaseAuthorityRevision,
        CancellationToken cancellationToken = default);

    Task<RuntimeAuthorityState> CommitAuthorityChangeAsync(
        Guid transitionId,
        long expectedBaseAuthorityRevision,
        DateTimeOffset authorityChangedAtUtc,
        DateTimeOffset? demoStartedAtUtc,
        CancellationToken cancellationToken = default);

    Task<int> FenceLeasesBeforeAuthorityRevisionAsync(
        Guid transitionId,
        long authorityRevision,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rebinds already-admitted HA logical leases to a newly committed Runtime authority
    /// revision without creating a second seat or changing SessionId/ClientInstanceId.
    /// </summary>
    Task<int> RebindActiveClusterLeasesAsync(
        Guid transitionId,
        long authorityRevision,
        string clusterId,
        string serverNode,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(0);

    Task CompleteAuthorityTransitionAsync(
        Guid transitionId,
        long authorityRevision,
        CancellationToken cancellationToken = default);

    Task<RuntimeSessionLeaseState> AdmitAsync(
        RuntimeSessionLeaseAdmission admission,
        CancellationToken cancellationToken = default);

    Task<RuntimeSessionLeaseCapacityAdmissionResult> AdmitWithCapacityAsync(
        RuntimeSessionLeaseCapacityAdmission admission,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically adopts an authenticated peer lease as the exact same logical lease.
    /// This does not perform admission or reserve a second seat.
    /// </summary>
    Task<RuntimeSessionReplicationMutationResult> AdoptReplicatedAsync(
        RuntimeSessionLeaseState lease,
        string expectedClusterId,
        string expectedSourceNode,
        RuntimeSessionRuntimeIdentity expectedRuntime,
        long expectedAuthorityRevision,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(RuntimeSessionReplicationMutationResult.Reject(
            "replicated-session-adoption-not-supported"));

    /// <summary>
    /// Applies peer termination/expiry evidence under the same canonical mutation boundary.
    /// </summary>
    Task<RuntimeSessionReplicationMutationResult> ApplyReplicatedTombstoneAsync(
        RuntimeSessionLeaseTombstoneState tombstone,
        string expectedClusterId,
        string expectedSourceNode,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(RuntimeSessionReplicationMutationResult.Reject(
            "replicated-session-tombstone-not-supported"));

    Task<RuntimeSessionLeaseStoreResult> ValidateAsync(
        Guid sessionId,
        string subjectId,
        RuntimeSessionRuntimeIdentity runtime,
        string? clientInstanceId = null,
        CancellationToken cancellationToken = default);

    Task<RuntimeSessionLeaseStoreResult> HeartbeatAsync(
        Guid sessionId,
        string subjectId,
        string clientInstanceId,
        RuntimeSessionRuntimeIdentity runtime,
        CancellationToken cancellationToken = default,
        long? expectedGeneration = null);

    Task<RuntimeSessionLeaseStoreResult> TerminateAsync(
        Guid sessionId,
        string subjectId,
        string clientInstanceId,
        RuntimeSessionRuntimeIdentity runtime,
        CancellationToken cancellationToken = default,
        long? expectedGeneration = null);
}

/// <summary>Development fallback with the same lease identity and CAS behavior as the durable store.</summary>
public sealed class InMemoryRuntimeSessionLeaseStore : IRuntimeSessionLeaseStore
{
    private readonly Dictionary<Guid, RuntimeSessionLeaseState> _leases = new();
    private readonly Dictionary<string, RuntimeSessionLeaseTombstoneState> _replicationTombstones =
        new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Func<DateTimeOffset> _utcNow;
    private long _authorityRevision = 1;
    private bool _transitionPending;
    private Guid? _transitionId;
    private string? _transitionKind;
    private DateTimeOffset? _transitionStartedAtUtc;
    private long? _transitionBaseAuthorityRevision;
    private DateTimeOffset? _authorityChangedAtUtc;
    private DateTimeOffset? _demoStartedAtUtc;

    public InMemoryRuntimeSessionLeaseStore(Func<DateTimeOffset>? utcNow = null) =>
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);

    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public async Task<RuntimeAuthorityState> GetAuthorityStateAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try { return AuthorityStateLocked(); }
        finally { _gate.Release(); }
    }

    public async Task<RuntimeDemoSessionAnchorResult> EstablishDemoSessionAnchorAsync(
        DateTimeOffset requestedStartedAtUtc,
        DateTimeOffset? expectedExistingStartedAtUtc,
        CancellationToken cancellationToken = default)
    {
        requestedStartedAtUtc = RuntimeDemoSessionAnchor.Normalize(requestedStartedAtUtc);
        expectedExistingStartedAtUtc = expectedExistingStartedAtUtc is { } expectedAnchor
            ? RuntimeDemoSessionAnchor.Normalize(expectedAnchor)
            : null;
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_transitionPending)
                throw new InvalidOperationException("A Runtime authority transition is pending.");

            if (_demoStartedAtUtc == expectedExistingStartedAtUtc)
            {
                _demoStartedAtUtc = requestedStartedAtUtc;
                return new RuntimeDemoSessionAnchorResult(requestedStartedAtUtc, WasEstablished: true);
            }

            if (_demoStartedAtUtc is { } concurrentAnchor)
                return new RuntimeDemoSessionAnchorResult(concurrentAnchor, WasEstablished: false);

            throw new InvalidOperationException("Runtime Demo session anchor changed concurrently.");
        }
        finally { _gate.Release(); }
    }

    public async Task<RuntimeAuthorityTransition> BeginAuthorityTransitionAsync(
        string kind,
        DateTimeOffset startedAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_transitionPending)
                throw new InvalidOperationException("A Runtime authority transition is already pending.");

            var id = Guid.NewGuid();
            _transitionPending = true;
            _transitionId = id;
            _transitionKind = kind.Trim();
            _transitionStartedAtUtc = startedAtUtc;
            _transitionBaseAuthorityRevision = _authorityRevision;
            return new RuntimeAuthorityTransition(id, _authorityRevision, _transitionKind, startedAtUtc);
        }
        finally { _gate.Release(); }
    }

    public async Task<bool> AbortAuthorityTransitionAsync(
        Guid transitionId,
        long expectedBaseAuthorityRevision,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!_transitionPending ||
                _transitionId != transitionId ||
                _transitionBaseAuthorityRevision != expectedBaseAuthorityRevision ||
                _authorityRevision != expectedBaseAuthorityRevision)
                return false;

            ClearTransitionLocked();
            return true;
        }
        finally { _gate.Release(); }
    }

    public async Task<RuntimeAuthorityState> CommitAuthorityChangeAsync(
        Guid transitionId,
        long expectedBaseAuthorityRevision,
        DateTimeOffset authorityChangedAtUtc,
        DateTimeOffset? demoStartedAtUtc,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            RequireTransitionForCommitLocked(transitionId, expectedBaseAuthorityRevision);
            _authorityRevision = checked(expectedBaseAuthorityRevision + 1);
            _authorityChangedAtUtc = authorityChangedAtUtc;
            _demoStartedAtUtc = demoStartedAtUtc is { } anchor
                ? RuntimeDemoSessionAnchor.Normalize(anchor)
                : null;
            return AuthorityStateLocked();
        }
        finally { _gate.Release(); }
    }

    public async Task<int> FenceLeasesBeforeAuthorityRevisionAsync(
        Guid transitionId,
        long authorityRevision,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            RequireTransitionLocked(transitionId, authorityRevision, revisionAlreadyAdvanced: true);
            var stale = _leases.Values
                .Where(lease => lease.IsActive && lease.AuthorityRevision < authorityRevision)
                .ToArray();
            foreach (var lease in stale)
            {
                _leases[lease.SessionId] = lease with
                {
                    Generation = checked(lease.Generation + 1),
                    IsActive = false
                };
            }
            return stale.Length;
        }
        finally { _gate.Release(); }
    }

    public async Task<int> RebindActiveClusterLeasesAsync(
        Guid transitionId,
        long authorityRevision,
        string clusterId,
        string serverNode,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clusterId);
        ArgumentException.ThrowIfNullOrWhiteSpace(serverNode);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            RequireTransitionLocked(transitionId, authorityRevision, revisionAlreadyAdvanced: true);
            var candidates = _leases.Values
                .Where(lease =>
                    lease.IsActive &&
                    lease.AuthorityRevision < authorityRevision &&
                    string.Equals(lease.ClusterId, clusterId.Trim(), StringComparison.OrdinalIgnoreCase))
                .ToArray();
            foreach (var lease in candidates)
            {
                _leases[lease.SessionId] = lease with
                {
                    Generation = checked(lease.Generation + 1),
                    ServerNode = serverNode.Trim(),
                    AuthorityRevision = authorityRevision
                };
            }
            return candidates.Length;
        }
        finally { _gate.Release(); }
    }

    public async Task CompleteAuthorityTransitionAsync(
        Guid transitionId,
        long authorityRevision,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            RequireTransitionLocked(transitionId, authorityRevision, revisionAlreadyAdvanced: true);
            if (_leases.Values.Any(lease => lease.IsActive && lease.AuthorityRevision < authorityRevision))
                throw new InvalidOperationException("Cannot complete Runtime authority transition while stale active leases remain.");
            ClearTransitionLocked();
        }
        finally { _gate.Release(); }
    }

    public async Task<RuntimeSessionLeaseState> AdmitAsync(
        RuntimeSessionLeaseAdmission admission,
        CancellationToken cancellationToken = default)
    {
        ValidateAdmission(admission);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_transitionPending)
                throw new InvalidOperationException("runtime-authority-transition-pending");

            var now = _utcNow();
            var subjectId = admission.SubjectId.Trim();
            var clientInstanceId = admission.ClientInstanceId.Trim();
            var active = _leases.Values.FirstOrDefault(lease =>
                lease.IsActive &&
                lease.SubjectId.Equals(subjectId, StringComparison.Ordinal) &&
                lease.ClientInstanceId.Equals(clientInstanceId, StringComparison.Ordinal));

            if (active is not null && active.AuthorityRevision != _authorityRevision)
            {
                _leases[active.SessionId] = active with
                {
                    Generation = checked(active.Generation + 1),
                    IsActive = false
                };
                active = null;
            }

            if (active is not null && active.ExpiresAtUtc <= now)
            {
                _leases[active.SessionId] = active with { IsActive = false };
                active = null;
            }

            if (active is not null && active.Runtime.Matches(admission.Runtime))
            {
                var retainedClass = MostRestrictiveConnectionClass(
                    active.GrantedConnectionClass,
                    admission.GrantedConnectionClass);
                if (!string.Equals(retainedClass, active.GrantedConnectionClass, StringComparison.OrdinalIgnoreCase))
                {
                    // The logical identity remains stable, but an explicit ViewOnly request or
                    // Authority downscope must invalidate stale generation users immediately.
                    active = active with
                    {
                        GrantedConnectionClass = retainedClass,
                        Generation = checked(active.Generation + 1)
                    };
                    _leases[active.SessionId] = active;
                }
                return active;
            }
            if (active is not null) _leases[active.SessionId] = active with { IsActive = false };

            var lease = new RuntimeSessionLeaseState(
                Guid.NewGuid(), subjectId, clientInstanceId, admission.GrantedConnectionClass.Trim(), 1,
                now, now, now.Add(admission.LeaseDuration), admission.Runtime,
                NormalizeOptional(admission.ServerNode), NormalizeOptional(admission.ClusterId), true,
                _authorityRevision);
            _leases.Add(lease.SessionId, lease);
            return lease;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<RuntimeSessionLeaseCapacityAdmissionResult> AdmitWithCapacityAsync(
        RuntimeSessionLeaseCapacityAdmission capacityAdmission,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(capacityAdmission);
        var admission = capacityAdmission.Lease;
        ValidateAdmission(admission);
        capacityAdmission.Capacity.Validate();
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_transitionPending)
                return RuntimeSessionLeaseCapacityAdmissionResult.Rejected(
                    RuntimeSessionSeatReservationReasonCode.AuthorityTransitionPending);
            if (capacityAdmission.ExpectedAuthorityRevision != _authorityRevision)
                return RuntimeSessionLeaseCapacityAdmissionResult.Rejected(
                    RuntimeSessionSeatReservationReasonCode.AuthorityRevisionChanged);

            var now = _utcNow();
            var subjectId = admission.SubjectId.Trim();
            var clientInstanceId = admission.ClientInstanceId.Trim();
            ExpireLocked(now);
            var active = _leases.Values.FirstOrDefault(lease =>
                lease.IsActive &&
                lease.SubjectId.Equals(subjectId, StringComparison.Ordinal) &&
                lease.ClientInstanceId.Equals(clientInstanceId, StringComparison.Ordinal));

            if (active is not null && active.AuthorityRevision != _authorityRevision)
            {
                _leases[active.SessionId] = active with
                {
                    Generation = checked(active.Generation + 1),
                    IsActive = false
                };
                active = null;
            }

            if (active is not null && active.Runtime.Matches(admission.Runtime))
            {
                if (IsViewOnlyConnectionClass(active.GrantedConnectionClass))
                    return RuntimeSessionLeaseCapacityAdmissionResult.Admitted(active, RuntimeSessionSeatReservationReasonCode.ExistingLeaseRetained);

                if (!IsViewOnlyConnectionClass(admission.GrantedConnectionClass))
                    return RuntimeSessionLeaseCapacityAdmissionResult.Admitted(active, RuntimeSessionSeatReservationReasonCode.InteractiveReserved);

                // Security/explicit ViewOnly downscope replaces the existing Interactive seat.
                if (CountActiveLocked(admission.Runtime, "viewer") >= capacityAdmission.Capacity.ViewOnlySeats)
                {
                    _leases[active.SessionId] = active with { Generation = checked(active.Generation + 1), IsActive = false };
                    return RuntimeSessionLeaseCapacityAdmissionResult.Rejected(RuntimeSessionSeatReservationReasonCode.ViewOnlyQuotaExhausted);
                }

                var downscoped = active with
                {
                    GrantedConnectionClass = "viewer",
                    Generation = checked(active.Generation + 1)
                };
                _leases[active.SessionId] = downscoped;
                return RuntimeSessionLeaseCapacityAdmissionResult.Admitted(downscoped, RuntimeSessionSeatReservationReasonCode.AuthorityDownscopeViewOnlyReserved);
            }

            if (active is not null)
                _leases[active.SessionId] = active with { Generation = checked(active.Generation + 1), IsActive = false };

            var desiredViewOnly = IsViewOnlyConnectionClass(admission.GrantedConnectionClass);
            var interactiveInUse = CountActiveLocked(admission.Runtime, "interactive");
            var viewOnlyInUse = CountActiveLocked(admission.Runtime, "viewer");
            string grantedClass;
            RuntimeSessionSeatReservationReasonCode reason;
            if (desiredViewOnly)
            {
                if (viewOnlyInUse >= capacityAdmission.Capacity.ViewOnlySeats)
                    return RuntimeSessionLeaseCapacityAdmissionResult.Rejected(RuntimeSessionSeatReservationReasonCode.ViewOnlyQuotaExhausted);
                grantedClass = "viewer";
                reason = RuntimeSessionSeatReservationReasonCode.ViewOnlyReserved;
            }
            else if (interactiveInUse < capacityAdmission.Capacity.InteractiveSeats)
            {
                grantedClass = "interactive";
                reason = RuntimeSessionSeatReservationReasonCode.InteractiveReserved;
            }
            else if (viewOnlyInUse < capacityAdmission.Capacity.ViewOnlySeats)
            {
                grantedClass = "viewer";
                reason = RuntimeSessionSeatReservationReasonCode.InteractiveQuotaFallbackViewOnly;
            }
            else if (capacityAdmission.Capacity.ViewOnlySeats == 0)
            {
                return RuntimeSessionLeaseCapacityAdmissionResult.Rejected(RuntimeSessionSeatReservationReasonCode.InteractiveQuotaExhaustedNoEligibleViewOnly);
            }
            else
            {
                return RuntimeSessionLeaseCapacityAdmissionResult.Rejected(RuntimeSessionSeatReservationReasonCode.EligiblePoolsExhausted);
            }

            var lease = new RuntimeSessionLeaseState(
                Guid.NewGuid(), subjectId, clientInstanceId, grantedClass, 1,
                now, now, now.Add(admission.LeaseDuration), admission.Runtime,
                NormalizeOptional(admission.ServerNode), NormalizeOptional(admission.ClusterId), true,
                _authorityRevision);
            _leases.Add(lease.SessionId, lease);
            return RuntimeSessionLeaseCapacityAdmissionResult.Admitted(lease, reason);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<RuntimeSessionReplicationMutationResult> AdoptReplicatedAsync(
        RuntimeSessionLeaseState lease,
        string expectedClusterId,
        string expectedSourceNode,
        RuntimeSessionRuntimeIdentity expectedRuntime,
        long expectedAuthorityRevision,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedClusterId);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedSourceNode);
        ArgumentNullException.ThrowIfNull(expectedRuntime);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_transitionPending)
                return RuntimeSessionReplicationMutationResult.Reject("authority-transition-pending");
            if (_authorityRevision != expectedAuthorityRevision ||
                lease.AuthorityRevision != expectedAuthorityRevision)
            {
                return RuntimeSessionReplicationMutationResult.Reject("authority-revision-changed");
            }

            var normalized = NormalizeReplicatedLease(lease);
            var validationFailure = ValidateReplicatedLease(
                normalized,
                expectedClusterId,
                expectedSourceNode,
                expectedRuntime,
                _utcNow());
            if (validationFailure is not null)
                return RuntimeSessionReplicationMutationResult.Reject(validationFailure);

            var tombstoneKey = ReplicationTombstoneKey(
                normalized.ClusterId!,
                normalized.SubjectId,
                normalized.ClientInstanceId);
            if (_replicationTombstones.TryGetValue(tombstoneKey, out var tombstone))
            {
                if (tombstone.SessionId == normalized.SessionId)
                    return RuntimeSessionReplicationMutationResult.Reject("session-resurrection-rejected");
                if (normalized.IssuedAtUtc <= tombstone.IssuedAtUtc)
                    return RuntimeSessionReplicationMutationResult.Reject("replicated-session-older-than-tombstone");
            }

            if (_leases.TryGetValue(normalized.SessionId, out var sameSession))
            {
                if (!sameSession.IsActive)
                    return RuntimeSessionReplicationMutationResult.Reject("session-resurrection-rejected");
                if (!ReplicatedIdentityMatches(sameSession, normalized))
                    return RuntimeSessionReplicationMutationResult.Reject("replicated-session-conflict");
                if (normalized.Generation < sameSession.Generation)
                    return RuntimeSessionReplicationMutationResult.Reject("replicated-session-stale-generation");
                if (normalized.Generation == sameSession.Generation &&
                    normalized.LastHeartbeatUtc <= sameSession.LastHeartbeatUtc &&
                    normalized.ExpiresAtUtc <= sameSession.ExpiresAtUtc)
                {
                    return RuntimeSessionReplicationMutationResult.Accept(
                        "replicated-session-already-current",
                        sameSession);
                }

                _leases[normalized.SessionId] = normalized;
                return RuntimeSessionReplicationMutationResult.Accept(
                    "replicated-session-updated",
                    normalized);
            }

            var logicalConflict = _leases.Values.FirstOrDefault(candidate =>
                candidate.IsActive &&
                candidate.SubjectId.Equals(normalized.SubjectId, StringComparison.Ordinal) &&
                candidate.ClientInstanceId.Equals(normalized.ClientInstanceId, StringComparison.Ordinal));
            if (logicalConflict is not null)
                return RuntimeSessionReplicationMutationResult.Reject("replicated-logical-session-conflict");

            _leases.Add(normalized.SessionId, normalized);
            return RuntimeSessionReplicationMutationResult.Accept(
                "replicated-session-adopted",
                normalized);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<RuntimeSessionReplicationMutationResult> ApplyReplicatedTombstoneAsync(
        RuntimeSessionLeaseTombstoneState tombstone,
        string expectedClusterId,
        string expectedSourceNode,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tombstone);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedClusterId);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedSourceNode);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_transitionPending)
                return RuntimeSessionReplicationMutationResult.Reject("authority-transition-pending");

            var normalized = NormalizeReplicatedTombstone(tombstone);
            var failure = ValidateReplicatedTombstone(
                normalized,
                expectedClusterId,
                expectedSourceNode);
            if (failure is not null)
                return RuntimeSessionReplicationMutationResult.Reject(failure);
            if (normalized.AuthorityRevision > _authorityRevision)
                return RuntimeSessionReplicationMutationResult.Reject("authority-revision-changed");

            var key = ReplicationTombstoneKey(
                normalized.ClusterId,
                normalized.SubjectId,
                normalized.ClientInstanceId);
            if (_replicationTombstones.TryGetValue(key, out var current))
            {
                if (normalized.AuthorityRevision < current.AuthorityRevision ||
                    (normalized.AuthorityRevision == current.AuthorityRevision &&
                     normalized.Generation < current.Generation) ||
                    (normalized.AuthorityRevision == current.AuthorityRevision &&
                     normalized.Generation == current.Generation &&
                     normalized.RecordedAtUtc <= current.RecordedAtUtc))
                {
                    return RuntimeSessionReplicationMutationResult.Accept(
                        "replicated-tombstone-already-current");
                }
            }

            if (_leases.TryGetValue(normalized.SessionId, out var lease) &&
                lease.IsActive)
            {
                if (lease.AuthorityRevision > normalized.AuthorityRevision ||
                    lease.Generation > normalized.Generation)
                {
                    return RuntimeSessionReplicationMutationResult.Reject(
                        "replicated-tombstone-stale");
                }

                _leases[lease.SessionId] = lease with
                {
                    Generation = Math.Max(
                        checked(lease.Generation + 1),
                        normalized.Generation),
                    IsActive = false
                };
            }

            _replicationTombstones[key] = normalized;
            return RuntimeSessionReplicationMutationResult.Accept(
                "replicated-tombstone-applied");
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<RuntimeSessionLeaseStoreResult> ValidateAsync(
        Guid sessionId,
        string subjectId,
        RuntimeSessionRuntimeIdentity runtime,
        string? clientInstanceId = null,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try { return ValidateLocked(sessionId, subjectId, runtime, clientInstanceId); }
        finally { _gate.Release(); }
    }

    public async Task<RuntimeSessionLeaseStoreResult> HeartbeatAsync(
        Guid sessionId,
        string subjectId,
        string clientInstanceId,
        RuntimeSessionRuntimeIdentity runtime,
        CancellationToken cancellationToken = default,
        long? expectedGeneration = null)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var validation = ValidateLocked(sessionId, subjectId, runtime, clientInstanceId);
            if (!validation.IsValid || validation.Lease is null) return validation;
            if (expectedGeneration.HasValue && validation.Lease.Generation != expectedGeneration.Value)
                return RuntimeSessionLeaseStoreResult.Invalid("session-changed");
            var now = _utcNow();
            var current = validation.Lease;
            var renewal = current with
            {
                Generation = checked(current.Generation + 1),
                LastHeartbeatUtc = now,
                ExpiresAtUtc = now.Add(current.ExpiresAtUtc - current.LastHeartbeatUtc)
            };
            _leases[sessionId] = renewal;
            return RuntimeSessionLeaseStoreResult.Valid(renewal);
        }
        finally { _gate.Release(); }
    }

    public async Task<RuntimeSessionLeaseStoreResult> TerminateAsync(
        Guid sessionId,
        string subjectId,
        string clientInstanceId,
        RuntimeSessionRuntimeIdentity runtime,
        CancellationToken cancellationToken = default,
        long? expectedGeneration = null)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var validation = ValidateLocked(sessionId, subjectId, runtime, clientInstanceId);
            if (!validation.IsValid || validation.Lease is null) return validation;
            if (expectedGeneration.HasValue && validation.Lease.Generation != expectedGeneration.Value)
                return RuntimeSessionLeaseStoreResult.Invalid("session-changed");
            _leases[sessionId] = validation.Lease with
            {
                Generation = checked(validation.Lease.Generation + 1),
                IsActive = false
            };
            return validation;
        }
        finally { _gate.Release(); }
    }

    private RuntimeSessionLeaseStoreResult ValidateLocked(
        Guid sessionId,
        string subjectId,
        RuntimeSessionRuntimeIdentity runtime,
        string? clientInstanceId)
    {
        if (_transitionPending)
            return RuntimeSessionLeaseStoreResult.Invalid("authority-transition-pending");
        if (sessionId == Guid.Empty) return RuntimeSessionLeaseStoreResult.Invalid("invalid-session-id");
        if (string.IsNullOrWhiteSpace(subjectId)) return RuntimeSessionLeaseStoreResult.Invalid("missing-user");
        if (!_leases.TryGetValue(sessionId, out var lease) || !lease.IsActive)
            return RuntimeSessionLeaseStoreResult.Invalid("session-not-found");
        if (lease.AuthorityRevision != _authorityRevision)
            return RuntimeSessionLeaseStoreResult.Invalid("authority-revision-stale");

        if (lease.ExpiresAtUtc <= _utcNow())
        {
            _leases[sessionId] = lease with { IsActive = false };
            return RuntimeSessionLeaseStoreResult.Invalid("session-expired");
        }
        if (!lease.SubjectId.Equals(subjectId.Trim(), StringComparison.Ordinal))
            return RuntimeSessionLeaseStoreResult.Invalid("session-user-mismatch");
        if (clientInstanceId is not null &&
            !lease.ClientInstanceId.Equals(clientInstanceId.Trim(), StringComparison.Ordinal))
            return RuntimeSessionLeaseStoreResult.Invalid("session-client-mismatch");
        if (!lease.Runtime.Matches(runtime))
        {
            _leases[sessionId] = lease with { IsActive = false };
            return RuntimeSessionLeaseStoreResult.Invalid("runtime-changed");
        }
        return RuntimeSessionLeaseStoreResult.Valid(lease);
    }

    private RuntimeAuthorityState AuthorityStateLocked() =>
        new(
            _authorityRevision,
            _transitionPending,
            _transitionId,
            _transitionKind,
            _transitionStartedAtUtc,
            _transitionBaseAuthorityRevision,
            _authorityChangedAtUtc,
            _demoStartedAtUtc,
            SupportsDurableRecovery: false);

    private void RequireTransitionForCommitLocked(
        Guid transitionId,
        long expectedBaseAuthorityRevision)
    {
        if (!_transitionPending ||
            _transitionId != transitionId ||
            _transitionBaseAuthorityRevision != expectedBaseAuthorityRevision)
            throw new InvalidOperationException("Runtime authority transition does not match the pending transition.");
        if (_authorityRevision != expectedBaseAuthorityRevision)
            throw new InvalidOperationException("Runtime authority revision changed unexpectedly.");
    }

    private void RequireTransitionLocked(Guid transitionId, long authorityRevision, bool revisionAlreadyAdvanced = false)
    {
        if (!_transitionPending ||
            _transitionId != transitionId ||
            _transitionBaseAuthorityRevision is not { } transitionBase ||
            _authorityRevision != authorityRevision ||
            (revisionAlreadyAdvanced && authorityRevision != checked(transitionBase + 1)))
        {
            throw new InvalidOperationException("Runtime authority transition does not match the pending transition.");
        }
    }

    private void ClearTransitionLocked()
    {
        _transitionPending = false;
        _transitionId = null;
        _transitionKind = null;
        _transitionStartedAtUtc = null;
        _transitionBaseAuthorityRevision = null;
    }

    private void ExpireLocked(DateTimeOffset now)
    {
        foreach (var expired in _leases.Values.Where(lease => lease.IsActive && lease.ExpiresAtUtc <= now).ToArray())
            _leases[expired.SessionId] = expired with { Generation = checked(expired.Generation + 1), IsActive = false };
    }

    private int CountActiveLocked(RuntimeSessionRuntimeIdentity runtime, string connectionClass) =>
        _leases.Values.Count(lease => lease.IsActive && lease.Runtime.Matches(runtime) &&
            (IsViewOnlyConnectionClass(connectionClass)
                ? IsViewOnlyConnectionClass(lease.GrantedConnectionClass)
                : lease.GrantedConnectionClass.Equals("interactive", StringComparison.OrdinalIgnoreCase)));

    private static void ValidateAdmission(RuntimeSessionLeaseAdmission admission)
    {
        ArgumentNullException.ThrowIfNull(admission);
        ArgumentException.ThrowIfNullOrWhiteSpace(admission.SubjectId);
        ArgumentException.ThrowIfNullOrWhiteSpace(admission.ClientInstanceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(admission.GrantedConnectionClass);
        ArgumentNullException.ThrowIfNull(admission.Runtime);
        if (admission.LeaseDuration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(admission), "Lease duration must be positive.");
    }

    private static RuntimeSessionLeaseState NormalizeReplicatedLease(
        RuntimeSessionLeaseState lease) =>
        lease with
        {
            SubjectId = lease.SubjectId.Trim(),
            ClientInstanceId = lease.ClientInstanceId.Trim(),
            GrantedConnectionClass = IsViewOnlyConnectionClass(lease.GrantedConnectionClass)
                ? "viewer"
                : "interactive",
            IssuedAtUtc = RuntimeDemoSessionAnchor.Normalize(lease.IssuedAtUtc),
            LastHeartbeatUtc = RuntimeDemoSessionAnchor.Normalize(lease.LastHeartbeatUtc),
            ExpiresAtUtc = RuntimeDemoSessionAnchor.Normalize(lease.ExpiresAtUtc),
            Runtime = lease.Runtime.NormalizeStoragePrecision(),
            ServerNode = NormalizeOptional(lease.ServerNode),
            ClusterId = NormalizeOptional(lease.ClusterId),
            IsActive = true
        };

    private static RuntimeSessionLeaseTombstoneState NormalizeReplicatedTombstone(
        RuntimeSessionLeaseTombstoneState tombstone) =>
        tombstone with
        {
            ClusterId = tombstone.ClusterId.Trim(),
            SubjectId = tombstone.SubjectId.Trim(),
            ClientInstanceId = tombstone.ClientInstanceId.Trim(),
            SourceNode = tombstone.SourceNode.Trim(),
            IssuedAtUtc = RuntimeDemoSessionAnchor.Normalize(tombstone.IssuedAtUtc),
            RecordedAtUtc = RuntimeDemoSessionAnchor.Normalize(tombstone.RecordedAtUtc),
            ReasonCode = tombstone.ReasonCode.Trim()
        };

    private static string? ValidateReplicatedLease(
        RuntimeSessionLeaseState lease,
        string expectedClusterId,
        string expectedSourceNode,
        RuntimeSessionRuntimeIdentity expectedRuntime,
        DateTimeOffset now)
    {
        if (lease.SessionId == Guid.Empty) return "replicated-session-id-invalid";
        if (string.IsNullOrWhiteSpace(lease.SubjectId)) return "replicated-session-subject-missing";
        if (string.IsNullOrWhiteSpace(lease.ClientInstanceId) ||
            lease.ClientInstanceId.Length > 128)
            return "replicated-session-client-invalid";
        if (lease.Generation < 1) return "replicated-session-generation-invalid";
        if (lease.AuthorityRevision < 1) return "replicated-session-authority-revision-invalid";
        if (lease.ExpiresAtUtc <= lease.LastHeartbeatUtc ||
            lease.LastHeartbeatUtc < lease.IssuedAtUtc)
            return "replicated-session-time-invalid";
        if (lease.ExpiresAtUtc <= now) return "replicated-session-expired";
        if (!string.Equals(lease.ClusterId, expectedClusterId.Trim(), StringComparison.OrdinalIgnoreCase))
            return "replicated-session-cluster-mismatch";
        if (!string.Equals(lease.ServerNode, expectedSourceNode.Trim(), StringComparison.OrdinalIgnoreCase))
            return "replicated-session-source-mismatch";
        if (!lease.Runtime.Matches(expectedRuntime))
            return "replicated-session-runtime-mismatch";
        return null;
    }

    private static string? ValidateReplicatedTombstone(
        RuntimeSessionLeaseTombstoneState tombstone,
        string expectedClusterId,
        string expectedSourceNode)
    {
        if (tombstone.SessionId == Guid.Empty) return "replicated-tombstone-session-id-invalid";
        if (string.IsNullOrWhiteSpace(tombstone.SubjectId) ||
            string.IsNullOrWhiteSpace(tombstone.ClientInstanceId))
            return "replicated-tombstone-identity-invalid";
        if (tombstone.Generation < 1 || tombstone.AuthorityRevision < 1)
            return "replicated-tombstone-version-invalid";
        if (string.IsNullOrWhiteSpace(tombstone.ReasonCode))
            return "replicated-tombstone-reason-missing";
        if (!tombstone.ClusterId.Equals(expectedClusterId.Trim(), StringComparison.OrdinalIgnoreCase))
            return "replicated-tombstone-cluster-mismatch";
        if (!tombstone.SourceNode.Equals(expectedSourceNode.Trim(), StringComparison.OrdinalIgnoreCase))
            return "replicated-tombstone-source-mismatch";
        return null;
    }

    private static bool ReplicatedIdentityMatches(
        RuntimeSessionLeaseState left,
        RuntimeSessionLeaseState right) =>
        left.SubjectId.Equals(right.SubjectId, StringComparison.Ordinal) &&
        left.ClientInstanceId.Equals(right.ClientInstanceId, StringComparison.Ordinal) &&
        string.Equals(left.GrantedConnectionClass, right.GrantedConnectionClass, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(left.ServerNode, right.ServerNode, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(left.ClusterId, right.ClusterId, StringComparison.OrdinalIgnoreCase) &&
        left.AuthorityRevision == right.AuthorityRevision &&
        left.IssuedAtUtc == right.IssuedAtUtc &&
        left.Runtime.Matches(right.Runtime);

    private static string ReplicationTombstoneKey(
        string clusterId,
        string subjectId,
        string clientInstanceId) =>
        string.Concat(
            clusterId.Trim().ToUpperInvariant(), "\n",
            subjectId.Trim(), "\n",
            clientInstanceId.Trim());

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string MostRestrictiveConnectionClass(string existing, string requested) =>
        IsViewOnlyConnectionClass(existing) || IsViewOnlyConnectionClass(requested)
            ? "viewer"
            : "interactive";

    private static bool IsViewOnlyConnectionClass(string value) =>
        value.Equals("viewer", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("viewonly", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("view-only", StringComparison.OrdinalIgnoreCase);

    public ValueTask DisposeAsync()
    {
        _gate.Dispose();
        return ValueTask.CompletedTask;
    }
}
