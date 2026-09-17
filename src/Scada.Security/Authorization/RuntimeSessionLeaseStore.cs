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
    bool IsActive);

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

/// <summary>
/// Durable boundary for logical Runtime leases. Lease identity is subject plus ClientInstanceId;
/// transports are deliberately not represented here.
/// </summary>
public interface IRuntimeSessionLeaseStore : IAsyncDisposable
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<RuntimeSessionLeaseState> AdmitAsync(
        RuntimeSessionLeaseAdmission admission,
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

    public InMemoryRuntimeSessionLeaseStore(Func<DateTimeOffset>? utcNow = null) =>
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);

    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public async Task<RuntimeSessionLeaseState> AdmitAsync(
        RuntimeSessionLeaseAdmission admission,
        CancellationToken cancellationToken = default)
    {
        ValidateAdmission(admission);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var now = _utcNow();
            var subjectId = admission.SubjectId.Trim();
            var clientInstanceId = admission.ClientInstanceId.Trim();
            var active = _leases.Values.FirstOrDefault(lease =>
                lease.IsActive &&
                lease.SubjectId.Equals(subjectId, StringComparison.Ordinal) &&
                lease.ClientInstanceId.Equals(clientInstanceId, StringComparison.Ordinal));

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
                NormalizeOptional(admission.ServerNode), NormalizeOptional(admission.ClusterId), true);
            _leases.Add(lease.SessionId, lease);
            return lease;
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
        if (sessionId == Guid.Empty) return RuntimeSessionLeaseStoreResult.Invalid("invalid-session-id");
        if (string.IsNullOrWhiteSpace(subjectId)) return RuntimeSessionLeaseStoreResult.Invalid("missing-user");
        if (!_leases.TryGetValue(sessionId, out var lease) || !lease.IsActive)
            return RuntimeSessionLeaseStoreResult.Invalid("session-not-found");

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
