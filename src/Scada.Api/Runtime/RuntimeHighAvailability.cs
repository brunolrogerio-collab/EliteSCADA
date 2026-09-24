using Microsoft.Extensions.Configuration;
using Scada.Core.Product.Licensing;

namespace Scada.Api.Runtime;

public enum RuntimeHaState
{
    Standalone,
    Synchronizing,
    Standby,
    ReadyStandby,
    Promoting,
    Active,
    Demoting,
    Isolated,
    Maintenance,
    Faulted
}

public enum RuntimeHaEndpointKind
{
    Local,
    Remote
}

public sealed record RuntimeHaEndpoint(
    RuntimeHaEndpointKind Kind,
    string Address,
    int Priority);

public sealed record RuntimeHaNodeDefinition(
    string NodeId,
    IReadOnlyCollection<RuntimeHaEndpoint> Endpoints);

public sealed record RuntimeHaTopologyDefinition(
    bool Enabled,
    string? ClusterId,
    string LocalNodeId,
    string? InitialActiveNodeId,
    long TopologyVersion,
    TimeSpan FreshnessWindow,
    IReadOnlyCollection<RuntimeHaNodeDefinition> Nodes)
{
    public const string Schema = "elitescada.runtime-ha-topology";
    public const int SchemaVersion = 1;

    public static RuntimeHaTopologyDefinition FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection("HighAvailability");
        var enabled = section.GetValue<bool?>("Enabled") ?? false;
        var localNodeId = NormalizeId(section["NodeId"]) ?? "standalone";

        if (!enabled)
        {
            return new RuntimeHaTopologyDefinition(
                false,
                null,
                localNodeId,
                localNodeId,
                1,
                TimeSpan.FromSeconds(15),
                new[]
                {
                    new RuntimeHaNodeDefinition(localNodeId, Array.Empty<RuntimeHaEndpoint>())
                });
        }

        var clusterId = NormalizeId(section["ClusterId"])
            ?? throw new InvalidOperationException("HighAvailability:ClusterId is required when HA is enabled.");
        var initialActiveNodeId = NormalizeId(section["InitialActiveNodeId"])
            ?? throw new InvalidOperationException("HighAvailability:InitialActiveNodeId is required when HA is enabled.");
        var topologyVersion = Math.Max(1, section.GetValue<long?>("TopologyVersion") ?? 1);
        var freshnessSeconds = Math.Max(1, section.GetValue<int?>("FreshnessSeconds") ?? 15);

        var nodes = new List<RuntimeHaNodeDefinition>();
        foreach (var child in section.GetSection("Nodes").GetChildren())
        {
            var nodeId = NormalizeId(child["NodeId"])
                ?? throw new InvalidOperationException("Every HighAvailability node requires NodeId.");
            var endpoints = new List<RuntimeHaEndpoint>();

            var local = NormalizeAddress(child["LocalEndpoint"], nodeId, "LocalEndpoint");
            var remote = NormalizeAddress(child["RemoteEndpoint"], nodeId, "RemoteEndpoint");
            endpoints.Add(new RuntimeHaEndpoint(RuntimeHaEndpointKind.Local, local, 0));
            endpoints.Add(new RuntimeHaEndpoint(RuntimeHaEndpointKind.Remote, remote, 1));

            nodes.Add(new RuntimeHaNodeDefinition(nodeId, endpoints));
        }

        if (nodes.Count != 2)
            throw new InvalidOperationException("The Wave 15 HA topology requires exactly two configured nodes.");
        if (nodes.Select(node => node.NodeId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != nodes.Count)
            throw new InvalidOperationException("HighAvailability NodeId values must be unique.");
        if (!nodes.Any(node => node.NodeId.Equals(localNodeId, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("HighAvailability:NodeId must identify one configured node.");
        if (!nodes.Any(node => node.NodeId.Equals(initialActiveNodeId, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("HighAvailability:InitialActiveNodeId must identify one configured node.");

        return new RuntimeHaTopologyDefinition(
            true,
            clusterId,
            localNodeId,
            initialActiveNodeId,
            topologyVersion,
            TimeSpan.FromSeconds(freshnessSeconds),
            nodes);
    }

    private static string? NormalizeId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim();
        if (normalized.Length > 128)
            throw new InvalidOperationException("HA identity values must not exceed 128 characters.");
        return normalized;
    }

    private static string NormalizeAddress(string? value, string nodeId, string setting)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"HighAvailability node '{nodeId}' requires {setting}.");

        var normalized = value.Trim();
        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException(
                $"HighAvailability node '{nodeId}' {setting} must be an absolute HTTP(S) endpoint.");
        }

        return uri.ToString().TrimEnd('/');
    }
}

public sealed record RuntimeHaRuntimeIdentity(
    string Mode,
    string? ProjectKey,
    long? Revision)
{
    public static RuntimeHaRuntimeIdentity From(ScadaRuntimeDescriptor runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        return new RuntimeHaRuntimeIdentity(runtime.Mode, runtime.ProjectKey, runtime.Revision);
    }

    public bool CompatibleWith(RuntimeHaRuntimeIdentity other) =>
        Revision.HasValue &&
        other.Revision.HasValue &&
        Revision == other.Revision &&
        string.Equals(Mode, other.Mode, StringComparison.Ordinal) &&
        string.Equals(ProjectKey, other.ProjectKey, StringComparison.OrdinalIgnoreCase);
}

public sealed record RuntimeHaNodeReadinessEvidence(
    bool Healthy,
    bool SynchronizationComplete,
    bool HaLicenseEntitled,
    RuntimeHaRuntimeIdentity Runtime,
    DateTimeOffset ObservedAtUtc,
    string? Diagnostic = null);

public sealed record RuntimeHaNodeSnapshot(
    string NodeId,
    string Role,
    RuntimeHaState State,
    bool Ready,
    bool Healthy,
    bool SynchronizationComplete,
    bool HaLicenseEntitled,
    RuntimeHaRuntimeIdentity Runtime,
    DateTimeOffset? LastObservedAtUtc,
    bool Fresh,
    string? ReadinessReason,
    IReadOnlyCollection<RuntimeHaEndpoint> Endpoints);

public sealed record RuntimeHaTransferOperation(
    Guid TransferId,
    string SourceNodeId,
    string TargetNodeId,
    long BreakEpoch,
    DateTimeOffset StartedAtUtc);

public sealed record RuntimeHaTopologySnapshot(
    string Schema,
    int SchemaVersion,
    bool Enabled,
    string? ClusterId,
    long TopologyVersion,
    long StateVersion,
    long AuthorityEpoch,
    Guid AuthorityInstanceId,
    string LocalNodeId,
    string? EffectiveActiveNodeId,
    bool AmbiguousAuthority,
    RuntimeHaTransferOperation? PendingTransfer,
    DateTimeOffset GeneratedAtUtc,
    IReadOnlyCollection<RuntimeHaNodeSnapshot> Nodes);

public sealed record RuntimeHaFencingToken(
    string ClusterId,
    string NodeId,
    long Epoch,
    Guid AuthorityInstanceId,
    Guid TokenId,
    DateTimeOffset IssuedAtUtc);

public sealed record RuntimeHaIndustrialAuthorityDecision(
    bool Allowed,
    string ReasonCode,
    RuntimeHaFencingToken? Token)
{
    public static RuntimeHaIndustrialAuthorityDecision Denied(string reasonCode) =>
        new(false, reasonCode, null);
}

public sealed record RuntimeHaTransitionResult(
    bool Succeeded,
    string ReasonCode,
    RuntimeHaTransferOperation? Transfer,
    RuntimeHaTopologySnapshot Snapshot);

/// <summary>
/// Server-owned HA authority state machine. It intentionally contains no peer transport and
/// performs no automatic promotion. A future replication/fencing transport can publish the
/// same state contract without moving authority into Drivers or clients.
/// </summary>
public sealed class RuntimeHaAuthorityCoordinator
{
    private readonly object _gate = new();
    private readonly RuntimeHaTopologyDefinition _topology;
    private readonly Func<DateTimeOffset> _utcNow;
    private readonly Guid _authorityInstanceId;
    private readonly Dictionary<string, NodeState> _nodes;
    private long _authorityEpoch = 1;
    private long _stateVersion = 1;
    private string? _effectiveActiveNodeId;
    private RuntimeHaTransferOperation? _pendingTransfer;
    private bool _ambiguousAuthority;

    public RuntimeHaAuthorityCoordinator(
        RuntimeHaTopologyDefinition topology,
        Func<DateTimeOffset>? utcNow = null,
        Guid? authorityInstanceId = null)
    {
        _topology = topology ?? throw new ArgumentNullException(nameof(topology));
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
        _authorityInstanceId = authorityInstanceId ?? Guid.NewGuid();
        _nodes = topology.Nodes.ToDictionary(
            node => node.NodeId,
            node => new NodeState(node),
            StringComparer.OrdinalIgnoreCase);

        if (!_nodes.ContainsKey(topology.LocalNodeId))
            throw new InvalidOperationException("The local HA node is absent from the topology.");

        if (!topology.Enabled)
        {
            _effectiveActiveNodeId = topology.LocalNodeId;
            _nodes[topology.LocalNodeId].State = RuntimeHaState.Standalone;
            return;
        }

        if (topology.InitialActiveNodeId is not null)
        {
            _effectiveActiveNodeId = topology.InitialActiveNodeId;
            foreach (var node in _nodes.Values)
            {
                node.State = node.Definition.NodeId.Equals(
                    topology.InitialActiveNodeId,
                    StringComparison.OrdinalIgnoreCase)
                    ? RuntimeHaState.Active
                    : RuntimeHaState.Synchronizing;
            }
        }
    }

    public RuntimeHaTopologyDefinition Definition => _topology;

    public RuntimeHaNodeReadinessEvidence? GetNodeReadiness(string nodeId)
    {
        lock (_gate)
        {
            return ResolveNodeLocked(nodeId).Evidence;
        }
    }

    public void UpdateNodeReadiness(string nodeId, RuntimeHaNodeReadinessEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        lock (_gate)
        {
            var node = ResolveNodeLocked(nodeId);
            node.Evidence = evidence;
            ReconcileReadinessLocked();
            _stateVersion = checked(_stateVersion + 1);
        }
    }

    public RuntimeHaIndustrialAuthorityDecision TryAcquireIndustrialAuthority(string nodeId)
    {
        lock (_gate)
        {
            if (!_topology.Enabled)
            {
                if (!nodeId.Equals(_topology.LocalNodeId, StringComparison.OrdinalIgnoreCase))
                    return RuntimeHaIndustrialAuthorityDecision.Denied("not-local-node");

                return new RuntimeHaIndustrialAuthorityDecision(
                    true,
                    "standalone-authority",
                    new RuntimeHaFencingToken(
                        _topology.ClusterId ?? "standalone",
                        _topology.LocalNodeId,
                        _authorityEpoch,
                        _authorityInstanceId,
                        Guid.NewGuid(),
                        _utcNow()));
            }

            if (_ambiguousAuthority)
                return RuntimeHaIndustrialAuthorityDecision.Denied("ambiguous-authority");
            if (_pendingTransfer is not null)
                return RuntimeHaIndustrialAuthorityDecision.Denied("transfer-in-progress");
            if (_effectiveActiveNodeId is null ||
                !_effectiveActiveNodeId.Equals(nodeId, StringComparison.OrdinalIgnoreCase))
                return RuntimeHaIndustrialAuthorityDecision.Denied("not-effective-active");

            var node = ResolveNodeLocked(nodeId);
            if (node.State != RuntimeHaState.Active || !IsActiveReady(node.Evidence))
                return RuntimeHaIndustrialAuthorityDecision.Denied("active-not-ready");

            return new RuntimeHaIndustrialAuthorityDecision(
                true,
                "effective-active",
                new RuntimeHaFencingToken(
                    _topology.ClusterId!,
                    node.Definition.NodeId,
                    _authorityEpoch,
                    _authorityInstanceId,
                    Guid.NewGuid(),
                    _utcNow()));
        }
    }

    public bool ValidateIndustrialAuthority(RuntimeHaFencingToken token)
    {
        ArgumentNullException.ThrowIfNull(token);
        lock (_gate)
        {
            if (_ambiguousAuthority || _pendingTransfer is not null || _effectiveActiveNodeId is null)
                return false;
            if (token.AuthorityInstanceId != _authorityInstanceId || token.Epoch != _authorityEpoch)
                return false;
            if (!string.Equals(token.ClusterId, _topology.ClusterId ?? "standalone", StringComparison.OrdinalIgnoreCase))
                return false;
            if (!_effectiveActiveNodeId.Equals(token.NodeId, StringComparison.OrdinalIgnoreCase))
                return false;

            var node = ResolveNodeLocked(token.NodeId);
            return !_topology.Enabled ||
                (node.State == RuntimeHaState.Active && IsActiveReady(node.Evidence));
        }
    }

    public RuntimeHaTransitionResult BeginManualTransfer(
        string sourceNodeId,
        string targetNodeId,
        long expectedEpoch)
    {
        lock (_gate)
        {
            if (!_topology.Enabled)
                return TransitionDeniedLocked("ha-disabled");
            if (_ambiguousAuthority)
                return TransitionDeniedLocked("ambiguous-authority");
            if (_pendingTransfer is not null)
                return TransitionDeniedLocked("transfer-already-pending");
            if (_authorityEpoch != expectedEpoch)
                return TransitionDeniedLocked("stale-epoch");
            if (_effectiveActiveNodeId is null ||
                !_effectiveActiveNodeId.Equals(sourceNodeId, StringComparison.OrdinalIgnoreCase))
                return TransitionDeniedLocked("source-not-effective-active");
            if (sourceNodeId.Equals(targetNodeId, StringComparison.OrdinalIgnoreCase))
                return TransitionDeniedLocked("target-equals-source");

            var source = ResolveNodeLocked(sourceNodeId);
            var target = ResolveNodeLocked(targetNodeId);
            if (!IsStandbyReadyAgainst(target.Evidence, source.Evidence))
                return TransitionDeniedLocked("target-not-ready-standby");

            // Break before make: the old effective Active is removed and the epoch advances
            // before the target can receive authority in CompleteManualTransfer.
            _authorityEpoch = checked(_authorityEpoch + 1);
            _effectiveActiveNodeId = null;
            source.State = RuntimeHaState.Demoting;
            target.State = RuntimeHaState.Promoting;
            _pendingTransfer = new RuntimeHaTransferOperation(
                Guid.NewGuid(),
                source.Definition.NodeId,
                target.Definition.NodeId,
                _authorityEpoch,
                _utcNow());
            _stateVersion = checked(_stateVersion + 1);

            return new RuntimeHaTransitionResult(
                true,
                "break-established",
                _pendingTransfer,
                SnapshotLocked());
        }
    }

    public RuntimeHaTransitionResult CompleteManualTransfer(
        Guid transferId,
        string targetNodeId,
        long expectedBreakEpoch)
    {
        lock (_gate)
        {
            if (!_topology.Enabled)
                return TransitionDeniedLocked("ha-disabled");
            if (_ambiguousAuthority)
                return TransitionDeniedLocked("ambiguous-authority");
            if (_pendingTransfer is null || _pendingTransfer.TransferId != transferId)
                return TransitionDeniedLocked("transfer-not-found");
            if (_pendingTransfer.BreakEpoch != expectedBreakEpoch || _authorityEpoch != expectedBreakEpoch)
                return TransitionDeniedLocked("stale-epoch");
            if (!_pendingTransfer.TargetNodeId.Equals(targetNodeId, StringComparison.OrdinalIgnoreCase))
                return TransitionDeniedLocked("target-mismatch");

            var source = ResolveNodeLocked(_pendingTransfer.SourceNodeId);
            var target = ResolveNodeLocked(targetNodeId);
            if (!IsStandbyReadyAgainst(target.Evidence, source.Evidence))
                return TransitionDeniedLocked("target-no-longer-ready");

            target.State = RuntimeHaState.Active;
            source.State = IsStandbyReadyAgainst(source.Evidence, target.Evidence)
                ? RuntimeHaState.ReadyStandby
                : RuntimeHaState.Standby;
            _effectiveActiveNodeId = target.Definition.NodeId;
            var completed = _pendingTransfer;
            _pendingTransfer = null;
            _stateVersion = checked(_stateVersion + 1);

            return new RuntimeHaTransitionResult(
                true,
                "target-authority-granted",
                completed,
                SnapshotLocked());
        }
    }

    public RuntimeHaTopologySnapshot ReportNodeUnavailable(string nodeId)
    {
        lock (_gate)
        {
            var node = ResolveNodeLocked(nodeId);
            var now = _utcNow();
            var previous = node.Evidence;
            node.Evidence = new RuntimeHaNodeReadinessEvidence(
                false,
                false,
                previous?.HaLicenseEntitled ?? false,
                previous?.Runtime ?? new RuntimeHaRuntimeIdentity("unknown", null, null),
                now,
                "peer-unavailable");
            node.State = RuntimeHaState.Faulted;

            if (_effectiveActiveNodeId is not null &&
                _effectiveActiveNodeId.Equals(node.Definition.NodeId, StringComparison.OrdinalIgnoreCase))
            {
                // Peer loss never promotes another node. This observer instead loses positive
                // evidence of an effective Active and fails closed.
                _effectiveActiveNodeId = null;
                _authorityEpoch = checked(_authorityEpoch + 1);
                _pendingTransfer = null;
                foreach (var candidate in _nodes.Values.Where(candidate => !ReferenceEquals(candidate, node)))
                {
                    if (candidate.State != RuntimeHaState.Faulted)
                        candidate.State = RuntimeHaState.Isolated;
                }
            }

            _stateVersion = checked(_stateVersion + 1);
            return SnapshotLocked();
        }
    }

    public RuntimeHaTopologySnapshot ObserveAuthorityClaim(
        string nodeId,
        long claimedEpoch,
        bool claimsActive)
    {
        lock (_gate)
        {
            ResolveNodeLocked(nodeId);
            if (!claimsActive)
                return SnapshotLocked();

            var matchesCurrent =
                !_ambiguousAuthority &&
                _pendingTransfer is null &&
                _effectiveActiveNodeId is not null &&
                _effectiveActiveNodeId.Equals(nodeId, StringComparison.OrdinalIgnoreCase) &&
                claimedEpoch == _authorityEpoch;
            if (matchesCurrent)
                return SnapshotLocked();

            _ambiguousAuthority = true;
            _effectiveActiveNodeId = null;
            _pendingTransfer = null;
            _authorityEpoch = checked(Math.Max(_authorityEpoch, claimedEpoch) + 1);
            foreach (var node in _nodes.Values)
            {
                if (node.State != RuntimeHaState.Faulted)
                    node.State = RuntimeHaState.Isolated;
            }
            _stateVersion = checked(_stateVersion + 1);
            return SnapshotLocked();
        }
    }

    public RuntimeHaTopologySnapshot Snapshot()
    {
        lock (_gate)
        {
            return SnapshotLocked();
        }
    }

    private RuntimeHaTransitionResult TransitionDeniedLocked(string reasonCode) =>
        new(false, reasonCode, _pendingTransfer, SnapshotLocked());

    private RuntimeHaTopologySnapshot SnapshotLocked()
    {
        var now = _utcNow();
        var nodes = _nodes.Values
            .OrderBy(node => node.Definition.NodeId, StringComparer.OrdinalIgnoreCase)
            .Select(node => ProjectNodeLocked(node, now))
            .ToArray();

        return new RuntimeHaTopologySnapshot(
            RuntimeHaTopologyDefinition.Schema,
            RuntimeHaTopologyDefinition.SchemaVersion,
            _topology.Enabled,
            _topology.ClusterId,
            _topology.TopologyVersion,
            _stateVersion,
            _authorityEpoch,
            _authorityInstanceId,
            _topology.LocalNodeId,
            _effectiveActiveNodeId,
            _ambiguousAuthority,
            _pendingTransfer,
            now,
            nodes);
    }

    private RuntimeHaNodeSnapshot ProjectNodeLocked(NodeState node, DateTimeOffset now)
    {
        var evidence = node.Evidence;
        var fresh = evidence is not null && now - evidence.ObservedAtUtc <= _topology.FreshnessWindow;
        var ready = node.State == RuntimeHaState.Active
            ? IsActiveReady(evidence)
            : _effectiveActiveNodeId is not null &&
              _nodes.TryGetValue(_effectiveActiveNodeId, out var active) &&
              IsStandbyReadyAgainst(evidence, active.Evidence);

        var role = _effectiveActiveNodeId is not null &&
                   _effectiveActiveNodeId.Equals(node.Definition.NodeId, StringComparison.OrdinalIgnoreCase)
            ? "active"
            : "standby";

        var reason = ReadyReason(node, ready, fresh);
        return new RuntimeHaNodeSnapshot(
            node.Definition.NodeId,
            role,
            node.State,
            ready,
            evidence?.Healthy ?? false,
            evidence?.SynchronizationComplete ?? false,
            evidence?.HaLicenseEntitled ?? false,
            evidence?.Runtime ?? new RuntimeHaRuntimeIdentity("unknown", null, null),
            evidence?.ObservedAtUtc,
            fresh,
            reason,
            node.Definition.Endpoints);
    }

    private string? ReadyReason(NodeState node, bool ready, bool fresh)
    {
        if (!_topology.Enabled) return null;
        if (_ambiguousAuthority) return "ambiguous-authority";
        if (node.Evidence is null) return "readiness-not-observed";
        if (!fresh) return "readiness-stale";
        if (!node.Evidence.Healthy) return "node-unhealthy";
        if (!node.Evidence.HaLicenseEntitled) return "ha-license-not-entitled";
        if (!node.Evidence.Runtime.Revision.HasValue) return "active-runtime-unavailable";
        if (node.State != RuntimeHaState.Active && !node.Evidence.SynchronizationComplete)
            return "synchronization-incomplete";
        return ready ? null : node.Evidence.Diagnostic ?? "runtime-not-compatible";
    }

    private void ReconcileReadinessLocked()
    {
        if (!_topology.Enabled)
        {
            _nodes[_topology.LocalNodeId].State = RuntimeHaState.Standalone;
            return;
        }

        if (_ambiguousAuthority)
        {
            foreach (var node in _nodes.Values)
            {
                if (node.State != RuntimeHaState.Faulted)
                    node.State = RuntimeHaState.Isolated;
            }
            return;
        }

        if (_pendingTransfer is not null)
            return;

        if (_effectiveActiveNodeId is null)
        {
            foreach (var node in _nodes.Values)
            {
                if (node.State != RuntimeHaState.Faulted)
                    node.State = RuntimeHaState.Isolated;
            }
            return;
        }

        var active = ResolveNodeLocked(_effectiveActiveNodeId);
        if (!IsActiveReady(active.Evidence))
        {
            active.State = RuntimeHaState.Faulted;
            _effectiveActiveNodeId = null;
            _authorityEpoch = checked(_authorityEpoch + 1);
            foreach (var node in _nodes.Values.Where(node => !ReferenceEquals(node, active)))
            {
                if (node.State != RuntimeHaState.Faulted)
                    node.State = RuntimeHaState.Isolated;
            }
            return;
        }

        active.State = RuntimeHaState.Active;
        foreach (var node in _nodes.Values.Where(node => !ReferenceEquals(node, active)))
        {
            if (node.Evidence is not null && !node.Evidence.Healthy)
            {
                node.State = RuntimeHaState.Faulted;
                continue;
            }

            node.State = IsStandbyReadyAgainst(node.Evidence, active.Evidence)
                ? RuntimeHaState.ReadyStandby
                : RuntimeHaState.Synchronizing;
        }
    }

    private static bool IsActiveReady(RuntimeHaNodeReadinessEvidence? evidence) =>
        evidence is not null &&
        evidence.Healthy &&
        evidence.HaLicenseEntitled &&
        evidence.Runtime.Revision.HasValue;

    private static bool IsStandbyReadyAgainst(
        RuntimeHaNodeReadinessEvidence? candidate,
        RuntimeHaNodeReadinessEvidence? active) =>
        candidate is not null &&
        active is not null &&
        candidate.Healthy &&
        candidate.SynchronizationComplete &&
        candidate.HaLicenseEntitled &&
        active.Healthy &&
        active.HaLicenseEntitled &&
        candidate.Runtime.CompatibleWith(active.Runtime);

    private NodeState ResolveNodeLocked(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId) || !_nodes.TryGetValue(nodeId.Trim(), out var node))
            throw new KeyNotFoundException($"HA node '{nodeId}' is not present in the configured topology.");
        return node;
    }

    private sealed class NodeState(RuntimeHaNodeDefinition definition)
    {
        public RuntimeHaNodeDefinition Definition { get; } = definition;
        public RuntimeHaNodeReadinessEvidence? Evidence { get; set; }
        public RuntimeHaState State { get; set; } = RuntimeHaState.Synchronizing;
    }
}

public sealed record RuntimeSessionLeaseContinuityEnvelope(
    string Schema,
    int SchemaVersion,
    string ClusterId,
    Guid SessionId,
    string UserId,
    string ClientInstanceId,
    RuntimeConnectionClass ConnectionClass,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset LastHeartbeatUtc,
    DateTimeOffset ExpiresAtUtc,
    string SourceNodeId,
    RuntimeHaRuntimeIdentity Runtime,
    long Generation,
    long AuthorityRevision,
    DateTimeOffset ReplicatedAtUtc);

public sealed record RuntimeSessionContinuityResumeResult(
    bool Resumable,
    string ReasonCode,
    RuntimeSessionLeaseContinuityEnvelope? Lease)
{
    public static RuntimeSessionContinuityResumeResult Denied(string reasonCode) =>
        new(false, reasonCode, null);
}

/// <summary>
/// Transport-neutral Runtime Session continuity boundary. It mirrors logical lease identity
/// without becoming a second licensing/quota authority. The frozen FND-03 ledger remains the
/// source of seat admission; future peer transport/store adoption can consume these envelopes.
/// </summary>
public sealed class RuntimeSessionLeaseContinuityRegistry
{
    public const string Schema = "elitescada.runtime-session-continuity";
    public const int SchemaVersion = 1;

    private readonly object _gate = new();
    private readonly Func<DateTimeOffset> _utcNow;
    private readonly Dictionary<string, RuntimeSessionLeaseContinuityEnvelope> _leases =
        new(StringComparer.Ordinal);

    public RuntimeSessionLeaseContinuityRegistry(Func<DateTimeOffset>? utcNow = null) =>
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);

    public RuntimeSessionLeaseContinuityEnvelope CaptureOrRetain(
        RuntimeSessionLease lease,
        string clusterId,
        string sourceNodeId)
    {
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentException.ThrowIfNullOrWhiteSpace(clusterId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceNodeId);

        var now = _utcNow();
        var candidate = new RuntimeSessionLeaseContinuityEnvelope(
            Schema,
            SchemaVersion,
            clusterId.Trim(),
            lease.SessionId,
            lease.UserId,
            lease.ClientInstanceId,
            lease.ConnectionClass,
            lease.IssuedAtUtc,
            lease.LastHeartbeatUtc,
            lease.ExpiresAtUtc,
            sourceNodeId.Trim(),
            new RuntimeHaRuntimeIdentity(
                lease.RuntimeMode,
                lease.RuntimeProjectKey,
                lease.RuntimeRevision),
            lease.Generation,
            lease.AuthorityRevision,
            now);

        lock (_gate)
        {
            ExpireLocked(now);
            var key = LogicalKey(candidate.ClusterId, candidate.UserId, candidate.ClientInstanceId);
            if (_leases.TryGetValue(key, out var current) &&
                current.ExpiresAtUtc > now &&
                current.SessionId != candidate.SessionId)
            {
                // Never let peer replication manufacture a second logical seat identity.
                return current;
            }

            _leases[key] = candidate;
            return candidate;
        }
    }

    public RuntimeSessionContinuityResumeResult TryResume(
        string clusterId,
        string userId,
        string clientInstanceId,
        RuntimeHaRuntimeIdentity runtime)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clusterId);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientInstanceId);
        ArgumentNullException.ThrowIfNull(runtime);

        lock (_gate)
        {
            var now = _utcNow();
            ExpireLocked(now);
            var key = LogicalKey(clusterId, userId, clientInstanceId);
            if (!_leases.TryGetValue(key, out var lease))
                return RuntimeSessionContinuityResumeResult.Denied("session-not-replicated");
            if (lease.ExpiresAtUtc <= now)
                return RuntimeSessionContinuityResumeResult.Denied("session-expired");
            if (!lease.Runtime.CompatibleWith(runtime))
                return RuntimeSessionContinuityResumeResult.Denied("runtime-changed");

            return new RuntimeSessionContinuityResumeResult(true, "logical-lease-resumable", lease);
        }
    }

    public int ActiveLogicalLeaseCount(string clusterId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clusterId);
        lock (_gate)
        {
            ExpireLocked(_utcNow());
            return _leases.Values.Count(lease =>
                lease.ClusterId.Equals(clusterId.Trim(), StringComparison.OrdinalIgnoreCase));
        }
    }

    public IReadOnlyCollection<RuntimeSessionLeaseContinuityEnvelope> Snapshot()
    {
        lock (_gate)
        {
            ExpireLocked(_utcNow());
            return _leases.Values
                .OrderBy(lease => lease.UserId, StringComparer.Ordinal)
                .ThenBy(lease => lease.ClientInstanceId, StringComparer.Ordinal)
                .ToArray();
        }
    }

    private void ExpireLocked(DateTimeOffset now)
    {
        foreach (var key in _leases
                     .Where(pair => pair.Value.ExpiresAtUtc <= now)
                     .Select(pair => pair.Key)
                     .ToArray())
        {
            _leases.Remove(key);
        }
    }

    private static string LogicalKey(string clusterId, string userId, string clientInstanceId) =>
        string.Concat(
            clusterId.Trim().ToUpperInvariant(), "\n",
            userId.Trim(), "\n",
            clientInstanceId.Trim());
}

public sealed class RuntimeHighAvailabilityService
{
    private readonly RuntimeHaAuthorityCoordinator _authority;
    private readonly Func<DateTimeOffset> _utcNow;

    public RuntimeHighAvailabilityService(IConfiguration configuration)
        : this(RuntimeHaTopologyDefinition.FromConfiguration(configuration))
    {
    }

    public RuntimeHighAvailabilityService(
        RuntimeHaTopologyDefinition topology,
        Func<DateTimeOffset>? utcNow = null,
        Guid? authorityInstanceId = null)
    {
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
        _authority = new RuntimeHaAuthorityCoordinator(topology, _utcNow, authorityInstanceId);
        SessionContinuity = new RuntimeSessionLeaseContinuityRegistry(_utcNow);
    }

    public bool Enabled => _authority.Definition.Enabled;
    public string? ClusterId => Enabled ? _authority.Definition.ClusterId : null;
    public string? LocalNodeId => Enabled ? _authority.Definition.LocalNodeId : null;
    public RuntimeHaAuthorityCoordinator Authority => _authority;
    public RuntimeSessionLeaseContinuityRegistry SessionContinuity { get; }

    public void RefreshLocalReadiness(
        ScadaRuntimeDescriptor runtime,
        LicenseVerificationResult verification)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(verification);
        if (!Enabled) return;

        var nodeId = _authority.Definition.LocalNodeId;
        var previous = _authority.GetNodeReadiness(nodeId);
        var snapshot = _authority.Snapshot();
        var isEffectiveActive = snapshot.EffectiveActiveNodeId is not null &&
            snapshot.EffectiveActiveNodeId.Equals(nodeId, StringComparison.OrdinalIgnoreCase);
        var haEntitled =
            verification.State == LicenseState.Valid &&
            verification.SessionEntitlements?.HaRuntime == true;

        _authority.UpdateNodeReadiness(
            nodeId,
            new RuntimeHaNodeReadinessEvidence(
                Healthy: true,
                SynchronizationComplete: previous?.SynchronizationComplete ?? isEffectiveActive,
                HaLicenseEntitled: haEntitled,
                Runtime: RuntimeHaRuntimeIdentity.From(runtime),
                ObservedAtUtc: _utcNow(),
                Diagnostic: haEntitled ? null : "local-license-not-ha-entitled"));
    }

    public void ReportLocalSynchronization(bool synchronizationComplete)
    {
        if (!Enabled) return;
        var nodeId = _authority.Definition.LocalNodeId;
        var previous = _authority.GetNodeReadiness(nodeId);
        if (previous is null)
            throw new InvalidOperationException("Local HA readiness must be observed before synchronization can be updated.");

        _authority.UpdateNodeReadiness(
            nodeId,
            previous with
            {
                SynchronizationComplete = synchronizationComplete,
                ObservedAtUtc = _utcNow()
            });
    }
}
