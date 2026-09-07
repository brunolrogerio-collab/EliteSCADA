using System.Collections.Concurrent;
using Scada.Security.Authorization;

namespace Scada.Api.Runtime;

public enum RuntimeConnectionClass
{
    Viewer,
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
    DateTimeOffset? RuntimeActivatedAtUtc);

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

public sealed class RuntimeSessionLeaseRegistry
{
    public static readonly TimeSpan DefaultLeaseDuration = TimeSpan.FromSeconds(60);
    public const int MaximumClientInstanceIdLength = 128;

    private readonly ConcurrentDictionary<Guid, RuntimeSessionLease> _leases = new();
    private readonly TimeSpan _leaseDuration;
    private readonly Func<DateTimeOffset> _utcNow;

    public RuntimeSessionLeaseRegistry(
        TimeSpan? leaseDuration = null,
        Func<DateTimeOffset>? utcNow = null)
    {
        _leaseDuration = leaseDuration ?? DefaultLeaseDuration;
        if (_leaseDuration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(leaseDuration));

        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
    }

    public TimeSpan LeaseDuration => _leaseDuration;

    public RuntimeSessionLease Admit(
        string userId,
        string clientInstanceId,
        RuntimeConnectionClass connectionClass,
        ScadaRuntimeDescriptor runtime,
        string? serverNode = null,
        string? clusterId = null)
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

        var now = _utcNow();
        var lease = new RuntimeSessionLease(
            Guid.NewGuid(),
            normalizedUserId,
            normalizedClientInstanceId,
            connectionClass,
            now,
            now,
            now.Add(_leaseDuration),
            NormalizeOptional(serverNode),
            NormalizeOptional(clusterId),
            runtime.Mode,
            runtime.ProjectKey,
            runtime.Revision,
            runtime.ActivatedAtUtc);

        _leases[lease.SessionId] = lease;
        return lease;
    }

    public RuntimeSessionLeaseValidation Validate(
        Guid sessionId,
        string userId,
        ScadaRuntimeDescriptor runtime,
        string? clientInstanceId = null)
    {
        if (sessionId == Guid.Empty)
            return RuntimeSessionLeaseValidation.Invalid("invalid-session-id");
        if (string.IsNullOrWhiteSpace(userId))
            return RuntimeSessionLeaseValidation.Invalid("missing-user");
        ArgumentNullException.ThrowIfNull(runtime);

        if (!_leases.TryGetValue(sessionId, out var lease))
            return RuntimeSessionLeaseValidation.Invalid("session-not-found");

        var now = _utcNow();
        if (lease.ExpiresAtUtc <= now)
        {
            _leases.TryRemove(sessionId, out _);
            return RuntimeSessionLeaseValidation.Invalid("session-expired");
        }

        if (!lease.UserId.Equals(userId.Trim(), StringComparison.Ordinal))
            return RuntimeSessionLeaseValidation.Invalid("session-user-mismatch");

        if (clientInstanceId is not null &&
            !lease.ClientInstanceId.Equals(clientInstanceId.Trim(), StringComparison.Ordinal))
        {
            return RuntimeSessionLeaseValidation.Invalid("session-client-mismatch");
        }

        if (!SameRuntime(lease, runtime))
        {
            _leases.TryRemove(sessionId, out _);
            return RuntimeSessionLeaseValidation.Invalid("runtime-changed");
        }

        return RuntimeSessionLeaseValidation.Valid(lease);
    }

    public RuntimeSessionLeaseValidation Heartbeat(
        Guid sessionId,
        string userId,
        string clientInstanceId,
        ScadaRuntimeDescriptor runtime)
    {
        var validation = Validate(sessionId, userId, runtime, clientInstanceId);
        if (!validation.IsValid || validation.Lease is null) return validation;

        var now = _utcNow();
        var renewed = validation.Lease with
        {
            LastHeartbeatUtc = now,
            ExpiresAtUtc = now.Add(_leaseDuration)
        };

        return _leases.TryUpdate(sessionId, renewed, validation.Lease)
            ? RuntimeSessionLeaseValidation.Valid(renewed)
            : RuntimeSessionLeaseValidation.Invalid("session-changed");
    }

    public RuntimeSessionLeaseValidation Terminate(
        Guid sessionId,
        string userId,
        string clientInstanceId,
        ScadaRuntimeDescriptor runtime)
    {
        var validation = Validate(sessionId, userId, runtime, clientInstanceId);
        if (!validation.IsValid || validation.Lease is null) return validation;

        return _leases.TryRemove(sessionId, out _)
            ? validation
            : RuntimeSessionLeaseValidation.Invalid("session-changed");
    }

    public static bool TryParseConnectionClass(
        string? value,
        out RuntimeConnectionClass connectionClass)
    {
        connectionClass = default;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var normalized = value.Trim();
        if (normalized.Equals("viewer", StringComparison.OrdinalIgnoreCase))
        {
            connectionClass = RuntimeConnectionClass.Viewer;
            return true;
        }

        if (normalized.Equals("interactive", StringComparison.OrdinalIgnoreCase))
        {
            connectionClass = RuntimeConnectionClass.Interactive;
            return true;
        }

        return false;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool SameRuntime(RuntimeSessionLease lease, ScadaRuntimeDescriptor runtime) =>
        lease.RuntimeRevision == runtime.Revision &&
        lease.RuntimeActivatedAtUtc == runtime.ActivatedAtUtc &&
        lease.RuntimeMode.Equals(runtime.Mode, StringComparison.Ordinal) &&
        string.Equals(lease.RuntimeProjectKey, runtime.ProjectKey, StringComparison.OrdinalIgnoreCase);
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
    public static AuthorizationDecision Apply(
        RuntimeSessionLeaseRegistry sessions,
        Guid sessionId,
        string userId,
        ScadaRuntimeDescriptor runtime,
        AuthorizationDecision baseline,
        string? clientInstanceId = null)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(baseline);

        if (!baseline.Allowed) return baseline;

        var validation = sessions.Validate(sessionId, userId, runtime, clientInstanceId);
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
