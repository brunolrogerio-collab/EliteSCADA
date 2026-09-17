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
    public bool Matches(RuntimeSessionRuntimeIdentity other) =>
        Revision == other.Revision &&
        ActivatedAtUtc == other.ActivatedAtUtc &&
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

public sealed record RuntimeAuthorityState(
    long AuthorityRevision,
    bool TransitionPending,
    Guid? TransitionId,
    string? TransitionKind,
    DateTimeOffset? TransitionStartedAtUtc,
    DateTimeOffset? AuthorityChangedAtUtc,
    DateTimeOffset? DemoStartedAtUtc,
    bool SupportsDurableRecovery);

public sealed record RuntimeAuthorityTransition(
    Guid TransitionId,
    long BaseAuthorityRevision,
    string Kind,
    DateTimeOffset StartedAtUtc);

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
    long ExpectedAuthorityRevision = 1);

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
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Func<DateTimeOffset> _utcNow;
    private long _authorityRevision = 1;
    private bool _transitionPending;
    private Guid? _transitionId;
    private string? _transitionKind;
    private DateTimeOffset? _transitionStartedAtUtc;
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
            RequireTransitionLocked(transitionId, expectedBaseAuthorityRevision);
            _authorityRevision = checked(_authorityRevision + 1);
            _authorityChangedAtUtc = authorityChangedAtUtc;
            _demoStartedAtUtc = demoStartedAtUtc;
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
            _authorityChangedAtUtc,
            _demoStartedAtUtc,
            SupportsDurableRecovery: false);

    private void RequireTransitionLocked(
        Guid transitionId,
        long expectedRevision,
        bool revisionAlreadyAdvanced = false)
    {
        if (!_transitionPending || _transitionId != transitionId)
            throw new InvalidOperationException("Runtime authority transition does not match the pending transition.");

        var expected = revisionAlreadyAdvanced ? expectedRevision : expectedRevision;
        if (_authorityRevision != expected)
            throw new InvalidOperationException("Runtime authority revision changed unexpectedly.");
    }

    private void ClearTransitionLocked()
    {
        _transitionPending = false;
        _transitionId = null;
        _transitionKind = null;
        _transitionStartedAtUtc = null;
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
