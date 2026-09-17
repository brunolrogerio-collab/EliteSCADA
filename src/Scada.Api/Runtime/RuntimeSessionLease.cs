using Scada.Security.Authorization;

namespace Scada.Api.Runtime;

public enum RuntimeConnectionClass
{
    // Viewer is retained as the persisted v1 compatibility value. ViewOnly is the public
    // Wave 15 term and intentionally has the same restrictive semantics.
    ViewOnly,
    Viewer = ViewOnly,
    Interactive
}

public sealed record RuntimeSessionAdmissionRequest(
    string ClientInstanceId,
    string ConnectionClass);

public sealed record RuntimeSessionHeartbeatRequest(string ClientInstanceId);

public sealed record RuntimeSessionTerminationRequest(string ClientInstanceId);

/// <summary>
/// Logical Runtime session admission. The lease is server-side deployment/runtime state and
/// must never be serialized into the canonical Engineering application or .escadapkg.
/// A lease is deliberately independent from any single HTTP request or WebSocket connection.
/// </summary>
public sealed record RuntimeSessionLease(
    Guid SessionId,
    string UserId,
    string ClientInstanceId,
    RuntimeConnectionClass ConnectionClass,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset LastHeartbeatUtc,
    DateTimeOffset ExpiresAtUtc,
    string? ServerNode,
    string? ClusterId,
    string RuntimeMode,
    string? RuntimeProjectKey,
    long? RuntimeRevision,
    DateTimeOffset? RuntimeActivatedAtUtc,
    long Generation,
    long AuthorityRevision);

public sealed record RuntimeSessionLeaseValidation(
    bool IsValid,
    RuntimeSessionLease? Lease,
    string? FailureCode)
{
    public static RuntimeSessionLeaseValidation Valid(RuntimeSessionLease lease) =>
        new(true, lease, null);

    public static RuntimeSessionLeaseValidation Invalid(string failureCode) =>
        new(false, null, failureCode);
}

public sealed record RuntimeSessionSeatAdmission(
    bool IsAdmitted,
    RuntimeSessionLease? Lease,
    RuntimeSessionSeatReservationReasonCode ReasonCode)
{
    public static RuntimeSessionSeatAdmission Rejected(RuntimeSessionSeatReservationReasonCode reasonCode) => new(false, null, reasonCode);
}

public sealed class RuntimeSessionLeaseRegistry
{
    public static readonly TimeSpan DefaultLeaseDuration = TimeSpan.FromSeconds(60);
    public const int MaximumClientInstanceIdLength = 128;

    private readonly IRuntimeSessionLeaseStore _store;
    private readonly TimeSpan _leaseDuration;

    public RuntimeSessionLeaseRegistry(
        TimeSpan? leaseDuration = null,
        Func<DateTimeOffset>? utcNow = null)
        : this(new InMemoryRuntimeSessionLeaseStore(utcNow), leaseDuration)
    {
    }

    public RuntimeSessionLeaseRegistry(
        IRuntimeSessionLeaseStore store,
        TimeSpan? leaseDuration = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _leaseDuration = leaseDuration ?? DefaultLeaseDuration;
        if (_leaseDuration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(leaseDuration));
    }

    public TimeSpan LeaseDuration => _leaseDuration;

    public Task<RuntimeAuthorityState> GetAuthorityStateAsync(CancellationToken cancellationToken = default) =>
        _store.GetAuthorityStateAsync(cancellationToken);

    public async Task<RuntimeSessionLease> AdmitAsync(
        string userId,
        string clientInstanceId,
        RuntimeConnectionClass connectionClass,
        ScadaRuntimeDescriptor runtime,
        string? serverNode = null,
        string? clusterId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientInstanceId);
        ArgumentNullException.ThrowIfNull(runtime);

        var normalizedUserId = userId.Trim();
        var normalizedClientInstanceId = clientInstanceId.Trim();
        if (normalizedClientInstanceId.Length > MaximumClientInstanceIdLength)
            throw new ArgumentOutOfRangeException(
                nameof(clientInstanceId),
                $"Client instance id must not exceed {MaximumClientInstanceIdLength} characters.");

        var lease = await _store.AdmitAsync(
            new RuntimeSessionLeaseAdmission(
                normalizedUserId,
                normalizedClientInstanceId,
                ToPersistedConnectionClass(connectionClass),
                ToRuntimeIdentity(runtime),
                _leaseDuration,
                serverNode,
                clusterId),
            cancellationToken);
        return FromStore(lease);
    }

    public async Task<RuntimeSessionSeatAdmission> AdmitWithCapacityAsync(
        string userId,
        string clientInstanceId,
        RuntimeConnectionClass connectionClass,
        ScadaRuntimeDescriptor runtime,
        RuntimeSessionSeatCapacity capacity,
        long expectedAuthorityRevision,
        string? serverNode = null,
        string? clusterId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientInstanceId);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(capacity);
        if (expectedAuthorityRevision < 1)
            throw new ArgumentOutOfRangeException(nameof(expectedAuthorityRevision));
        var normalizedClientInstanceId = clientInstanceId.Trim();
        if (normalizedClientInstanceId.Length > MaximumClientInstanceIdLength)
            throw new ArgumentOutOfRangeException(nameof(clientInstanceId));

        var result = await _store.AdmitWithCapacityAsync(
            new RuntimeSessionLeaseCapacityAdmission(
                new RuntimeSessionLeaseAdmission(
                    userId.Trim(),
                    normalizedClientInstanceId,
                    ToPersistedConnectionClass(connectionClass),
                    ToRuntimeIdentity(runtime),
                    _leaseDuration,
                    serverNode,
                    clusterId),
                capacity,
                expectedAuthorityRevision),
            cancellationToken);
        return result.IsAdmitted && result.Lease is not null
            ? new RuntimeSessionSeatAdmission(true, FromStore(result.Lease), result.ReasonCode)
            : RuntimeSessionSeatAdmission.Rejected(result.ReasonCode);
    }

    public async Task<RuntimeSessionLeaseValidation> ValidateAsync(
        Guid sessionId,
        string userId,
        ScadaRuntimeDescriptor runtime,
        string? clientInstanceId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        var result = await _store.ValidateAsync(
            sessionId,
            userId,
            ToRuntimeIdentity(runtime),
            clientInstanceId,
            cancellationToken);
        return FromStore(result);
    }

    public async Task<RuntimeSessionLeaseValidation> HeartbeatAsync(
        Guid sessionId,
        string userId,
        string clientInstanceId,
        ScadaRuntimeDescriptor runtime,
        CancellationToken cancellationToken = default)
    {
        var result = await _store.HeartbeatAsync(
            sessionId,
            userId,
            clientInstanceId,
            ToRuntimeIdentity(runtime),
            cancellationToken);
        return FromStore(result);
    }

    public async Task<RuntimeSessionLeaseValidation> TerminateAsync(
        Guid sessionId,
        string userId,
        string clientInstanceId,
        ScadaRuntimeDescriptor runtime,
        CancellationToken cancellationToken = default)
    {
        var result = await _store.TerminateAsync(
            sessionId,
            userId,
            clientInstanceId,
            ToRuntimeIdentity(runtime),
            cancellationToken);
        return FromStore(result);
    }

    public static bool TryParseConnectionClass(
        string? value,
        out RuntimeConnectionClass connectionClass)
    {
        connectionClass = default;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var normalized = value.Trim();
        if (normalized.Equals("viewer", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("viewonly", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("view-only", StringComparison.OrdinalIgnoreCase))
        {
            connectionClass = RuntimeConnectionClass.ViewOnly;
            return true;
        }

        if (normalized.Equals("interactive", StringComparison.OrdinalIgnoreCase))
        {
            connectionClass = RuntimeConnectionClass.Interactive;
            return true;
        }

        return false;
    }

    private static RuntimeSessionRuntimeIdentity ToRuntimeIdentity(ScadaRuntimeDescriptor runtime) => new(
        runtime.Mode,
        runtime.ProjectKey,
        runtime.Revision,
        runtime.ActivatedAtUtc);

    private static RuntimeSessionLeaseValidation FromStore(RuntimeSessionLeaseStoreResult result) =>
        result.IsValid && result.Lease is not null
            ? RuntimeSessionLeaseValidation.Valid(FromStore(result.Lease))
            : RuntimeSessionLeaseValidation.Invalid(result.FailureCode ?? "unknown");

    private static RuntimeSessionLease FromStore(RuntimeSessionLeaseState lease)
    {
        if (!TryParseConnectionClass(lease.GrantedConnectionClass, out var connectionClass))
            throw new InvalidDataException("Persisted Runtime session connection class is incompatible.");
        return new RuntimeSessionLease(
            lease.SessionId,
            lease.SubjectId,
            lease.ClientInstanceId,
            connectionClass,
            lease.IssuedAtUtc,
            lease.LastHeartbeatUtc,
            lease.ExpiresAtUtc,
            lease.ServerNode,
            lease.ClusterId,
            lease.Runtime.Mode,
            lease.Runtime.ProjectKey,
            lease.Runtime.Revision,
            lease.Runtime.ActivatedAtUtc,
            lease.Generation,
            lease.AuthorityRevision);
    }

    // Keep v1's "viewer" database value even though the public Wave 15 wire term is ViewOnly.
    private static string ToPersistedConnectionClass(RuntimeConnectionClass connectionClass) =>
        connectionClass == RuntimeConnectionClass.Interactive ? "interactive" : "viewer";
}

public static class RuntimeSessionCapabilityProjection
{
    private static readonly HashSet<SecurityCapability> ViewerCeiling =
    [
        SecurityCapability.View,
        SecurityCapability.TagRead,
        SecurityCapability.TrendUse
    ];

    /// <summary>
    /// Intersects an already-computed Authority decision with the requested Runtime connection
    /// class. It can only preserve or reduce authority; it can never grant a denied capability.
    /// </summary>
    public static AuthorizationDecision Apply(
        RuntimeConnectionClass connectionClass,
        AuthorizationDecision baseline)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        if (!baseline.Allowed) return baseline;
        if (connectionClass == RuntimeConnectionClass.Interactive) return baseline;

        return ViewerCeiling.Contains(baseline.Capability)
            ? baseline
            : AuthorizationDecision.Denied(
                baseline.Capability,
                "The Runtime Viewer session is deliberately downscoped to read-only operation.");
    }
}

public static class RuntimeSessionAccessEvaluator
{
    /// <summary>
    /// Applies the logical lease to a baseline backend Authority decision. This is the common
    /// server-side path used for direct HTTP calls as well as tests that model a modified client.
    /// </summary>
    public static async Task<AuthorizationDecision> ApplyAsync(
        RuntimeSessionLeaseRegistry sessions,
        Guid sessionId,
        string userId,
        ScadaRuntimeDescriptor runtime,
        AuthorizationDecision baseline,
        string? clientInstanceId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(baseline);

        if (!baseline.Allowed) return baseline;

        var validation = await sessions.ValidateAsync(
            sessionId,
            userId,
            runtime,
            clientInstanceId,
            cancellationToken);
        if (!validation.IsValid || validation.Lease is null)
        {
            return AuthorizationDecision.Denied(
                baseline.Capability,
                $"The Runtime session lease is not valid ({validation.FailureCode ?? "unknown"}).");
        }

        return RuntimeSessionCapabilityProjection.Apply(
            validation.Lease.ConnectionClass,
            baseline);
    }
}
