namespace Scada.Api.Runtime;

public enum RuntimeHaTransferHandoffPhase
{
    Break,
    Grant
}

public sealed record RuntimeHaPeerObservationEnvelope(
    string Schema,
    int SchemaVersion,
    string ClusterId,
    long TopologyVersion,
    string SourceNodeId,
    Guid SourceObservationInstanceId,
    long ObservationSequence,
    Guid SourceAuthorityInstanceId,
    long AuthorityEpoch,
    string? EffectiveActiveNodeId,
    bool AmbiguousAuthority,
    RuntimeHaNodeReadinessEvidence Readiness,
    DateTimeOffset ObservedAtUtc)
{
    public const string SchemaName = "elitescada.runtime-ha-peer-observation";
    public const int CurrentSchemaVersion = 1;
}

public sealed record RuntimeHaTransferHandoffEnvelope(
    string Schema,
    int SchemaVersion,
    string ClusterId,
    long TopologyVersion,
    Guid HandoffId,
    Guid SourceObservationInstanceId,
    long HandoffSequence,
    Guid SourceAuthorityInstanceId,
    RuntimeHaTransferHandoffPhase Phase,
    Guid TransferId,
    string SourceNodeId,
    string TargetNodeId,
    long BreakEpoch,
    DateTimeOffset IssuedAtUtc)
{
    public const string SchemaName = "elitescada.runtime-ha-transfer-handoff";
    public const int CurrentSchemaVersion = 1;
}

public sealed record RuntimeHaPeerApplyResult(
    bool Accepted,
    string ReasonCode,
    RuntimeHaTopologySnapshot Snapshot);

public sealed record RuntimeHaPeerTransferResult(
    RuntimeHaTransitionResult Transition,
    RuntimeHaTransferHandoffEnvelope? Handoff);

public sealed record RuntimeSessionContinuityImportResult(
    bool Accepted,
    string ReasonCode,
    RuntimeSessionLeaseContinuityEnvelope? Lease);

public sealed partial class RuntimeHaAuthorityCoordinator
{
    internal RuntimeHaTransitionResult ApplyPeerTransferBreak(
        RuntimeHaTransferHandoffEnvelope handoff)
    {
        ArgumentNullException.ThrowIfNull(handoff);

        lock (_gate)
        {
            if (!_topology.Enabled)
                return TransitionDeniedLocked("ha-disabled");
            if (_ambiguousAuthority)
                return TransitionDeniedLocked("ambiguous-authority");
            if (_pendingTransfer is not null)
                return TransitionDeniedLocked("transfer-already-pending");
            if (_authorityEpoch == long.MaxValue ||
                handoff.BreakEpoch != _authorityEpoch + 1)
            {
                return TransitionDeniedLocked("handoff-epoch-invalid");
            }
            if (_effectiveActiveNodeId is null ||
                !_effectiveActiveNodeId.Equals(
                    handoff.SourceNodeId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return TransitionDeniedLocked("source-not-effective-active");
            }

            var source = ResolveNodeLocked(handoff.SourceNodeId);
            var target = ResolveNodeLocked(handoff.TargetNodeId);
            if (!IsStandbyReadyAgainst(target.Evidence, source.Evidence))
                return TransitionDeniedLocked("target-not-ready-standby");

            _authorityEpoch = handoff.BreakEpoch;
            _effectiveActiveNodeId = null;
            source.State = RuntimeHaState.Demoting;
            target.State = RuntimeHaState.Promoting;
            _pendingTransfer = new RuntimeHaTransferOperation(
                handoff.TransferId,
                source.Definition.NodeId,
                target.Definition.NodeId,
                handoff.BreakEpoch,
                handoff.IssuedAtUtc);
            _stateVersion = checked(_stateVersion + 1);

            return new RuntimeHaTransitionResult(
                true,
                "peer-break-applied",
                _pendingTransfer,
                SnapshotLocked());
        }
    }

    internal RuntimeHaTransitionResult ApplyPeerTransferGrant(
        RuntimeHaTransferHandoffEnvelope handoff)
    {
        ArgumentNullException.ThrowIfNull(handoff);

        lock (_gate)
        {
            if (!_topology.Enabled)
                return TransitionDeniedLocked("ha-disabled");
            if (_ambiguousAuthority)
                return TransitionDeniedLocked("ambiguous-authority");
            if (_pendingTransfer is null ||
                _pendingTransfer.TransferId != handoff.TransferId)
            {
                return TransitionDeniedLocked("transfer-not-found");
            }
            if (_pendingTransfer.BreakEpoch != handoff.BreakEpoch ||
                _authorityEpoch != handoff.BreakEpoch)
            {
                return TransitionDeniedLocked("stale-epoch");
            }
            if (!_pendingTransfer.SourceNodeId.Equals(
                    handoff.SourceNodeId,
                    StringComparison.OrdinalIgnoreCase) ||
                !_pendingTransfer.TargetNodeId.Equals(
                    handoff.TargetNodeId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return TransitionDeniedLocked("handoff-transfer-mismatch");
            }

            var source = ResolveNodeLocked(handoff.SourceNodeId);
            var target = ResolveNodeLocked(handoff.TargetNodeId);
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
                "peer-grant-applied",
                completed,
                SnapshotLocked());
        }
    }

    internal RuntimeHaTopologySnapshot MarkAmbiguousPeerAuthority(
        long observedEpoch)
    {
        lock (_gate)
        {
            _ambiguousAuthority = true;
            _effectiveActiveNodeId = null;
            _pendingTransfer = null;
            _authorityEpoch = checked(Math.Max(_authorityEpoch, observedEpoch) + 1);
            foreach (var node in _nodes.Values)
            {
                if (node.State != RuntimeHaState.Faulted)
                    node.State = RuntimeHaState.Isolated;
            }

            _stateVersion = checked(_stateVersion + 1);
            return SnapshotLocked();
        }
    }
}

public sealed partial class RuntimeSessionLeaseContinuityRegistry
{
    private readonly Dictionary<string, PeerLeaseTombstone> _peerTombstones =
        new(StringComparer.Ordinal);

    public RuntimeSessionContinuityImportResult ImportPeerEnvelope(
        RuntimeSessionLeaseContinuityEnvelope envelope,
        string expectedClusterId,
        string expectedPeerNodeId)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedClusterId);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedPeerNodeId);

        if (!string.Equals(envelope.Schema, Schema, StringComparison.Ordinal) ||
            envelope.SchemaVersion != SchemaVersion)
        {
            return new RuntimeSessionContinuityImportResult(
                false,
                "session-schema-mismatch",
                null);
        }
        if (!envelope.ClusterId.Equals(
                expectedClusterId.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            return new RuntimeSessionContinuityImportResult(
                false,
                "session-cluster-mismatch",
                null);
        }
        if (!envelope.SourceNodeId.Equals(
                expectedPeerNodeId.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            return new RuntimeSessionContinuityImportResult(
                false,
                "session-source-node-mismatch",
                null);
        }

        var now = _utcNow();
        if (envelope.ExpiresAtUtc <= now)
        {
            return new RuntimeSessionContinuityImportResult(
                false,
                "session-expired",
                null);
        }

        lock (_gate)
        {
            ExpireLocked(now);
            var key = LogicalKey(
                envelope.ClusterId,
                envelope.UserId,
                envelope.ClientInstanceId);

            if (_peerTombstones.TryGetValue(key, out var tombstone) &&
                envelope.Generation <= tombstone.Generation)
            {
                return new RuntimeSessionContinuityImportResult(
                    false,
                    "session-terminated-or-expired-generation",
                    null);
            }

            if (_leases.TryGetValue(key, out var current))
            {
                if (current.SessionId != envelope.SessionId)
                {
                    return new RuntimeSessionContinuityImportResult(
                        false,
                        "logical-lease-conflict",
                        current);
                }
                if (envelope.Generation < current.Generation)
                {
                    return new RuntimeSessionContinuityImportResult(
                        false,
                        "session-generation-stale",
                        current);
                }
                if (envelope.Generation == current.Generation &&
                    envelope.AuthorityRevision < current.AuthorityRevision)
                {
                    return new RuntimeSessionContinuityImportResult(
                        false,
                        "session-authority-revision-stale",
                        current);
                }
                if (envelope.ConnectionClass != current.ConnectionClass)
                {
                    return new RuntimeSessionContinuityImportResult(
                        false,
                        "session-class-mismatch",
                        current);
                }
                if (!envelope.Runtime.CompatibleWith(current.Runtime))
                {
                    return new RuntimeSessionContinuityImportResult(
                        false,
                        "session-runtime-mismatch",
                        current);
                }
                if (envelope.Generation == current.Generation &&
                    envelope.AuthorityRevision == current.AuthorityRevision &&
                    envelope.LastHeartbeatUtc <= current.LastHeartbeatUtc &&
                    envelope.ExpiresAtUtc <= current.ExpiresAtUtc)
                {
                    return new RuntimeSessionContinuityImportResult(
                        false,
                        "session-state-not-newer",
                        current);
                }
            }

            var imported = envelope with { ReplicatedAtUtc = now };
            _leases[key] = imported;
            return new RuntimeSessionContinuityImportResult(
                true,
                "peer-session-applied",
                imported);
        }
    }

    private void RecordPeerTombstoneLocked(
        string key,
        RuntimeSessionLeaseContinuityEnvelope lease,
        string reasonCode)
    {
        if (_peerTombstones.TryGetValue(key, out var current) &&
            current.Generation > lease.Generation)
        {
            return;
        }

        _peerTombstones[key] = new PeerLeaseTombstone(
            lease.SessionId,
            lease.Generation,
            lease.AuthorityRevision,
            _utcNow(),
            reasonCode);
    }

    private void ClearPeerTombstoneForLocalAdmissionLocked(string key) =>
        _peerTombstones.Remove(key);

    private sealed record PeerLeaseTombstone(
        Guid SessionId,
        long Generation,
        long AuthorityRevision,
        DateTimeOffset RecordedAtUtc,
        string ReasonCode);
}

public sealed partial class RuntimeHighAvailabilityService
{
    private readonly object _peerGate = new();
    private readonly Guid _peerObservationInstanceId = Guid.NewGuid();
    private readonly Dictionary<string, PeerObservationCursor> _peerObservations =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<Guid, PeerTransferCursor> _peerTransfers = new();
    private long _peerObservationSequence;
    private long _peerHandoffSequence;

    public RuntimeHaTopologySnapshot ObserveLocalReadiness(
        RuntimeHaNodeReadinessEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        if (!Enabled)
            return _authority.Snapshot();

        _authority.UpdateNodeReadiness(
            _authority.Definition.LocalNodeId,
            evidence);
        return _authority.Snapshot();
    }

    public RuntimeHaIndustrialAuthorityDecision TryAcquireLocalIndustrialAuthority()
    {
        if (!Enabled)
            return _authority.TryAcquireIndustrialAuthority(
                _authority.Definition.LocalNodeId);

        return _authority.TryAcquireIndustrialAuthority(
            _authority.Definition.LocalNodeId);
    }

    public bool ValidateLocalIndustrialAuthority(RuntimeHaFencingToken token)
    {
        ArgumentNullException.ThrowIfNull(token);
        return _authority.ValidateIndustrialAuthority(token);
    }

    public RuntimeHaPeerObservationEnvelope CreatePeerObservation()
    {
        if (!Enabled || ClusterId is null || LocalNodeId is null)
            throw new InvalidOperationException(
                "Peer observation is available only when HA is enabled.");

        var readiness = _authority.GetNodeReadiness(LocalNodeId)
            ?? throw new InvalidOperationException(
                "Local HA readiness must be observed before publishing peer evidence.");
        var snapshot = _authority.Snapshot();
        long sequence;

        lock (_peerGate)
        {
            sequence = checked(++_peerObservationSequence);
        }

        return new RuntimeHaPeerObservationEnvelope(
            RuntimeHaPeerObservationEnvelope.SchemaName,
            RuntimeHaPeerObservationEnvelope.CurrentSchemaVersion,
            ClusterId,
            _authority.Definition.TopologyVersion,
            LocalNodeId,
            _peerObservationInstanceId,
            sequence,
            snapshot.AuthorityInstanceId,
            snapshot.AuthorityEpoch,
            snapshot.EffectiveActiveNodeId,
            snapshot.AmbiguousAuthority,
            readiness,
            _utcNow());
    }

    public RuntimeHaPeerApplyResult ApplyPeerObservation(
        RuntimeHaPeerObservationEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        var headerFailure = ValidatePeerHeader(
            envelope.Schema,
            RuntimeHaPeerObservationEnvelope.SchemaName,
            envelope.SchemaVersion,
            RuntimeHaPeerObservationEnvelope.CurrentSchemaVersion,
            envelope.ClusterId,
            envelope.TopologyVersion,
            envelope.SourceNodeId,
            envelope.ObservedAtUtc);
        if (headerFailure is not null)
        {
            return new RuntimeHaPeerApplyResult(
                false,
                headerFailure,
                _authority.Snapshot());
        }

        if (!IsFreshPeerTime(envelope.Readiness.ObservedAtUtc))
        {
            return new RuntimeHaPeerApplyResult(
                false,
                "peer-readiness-stale",
                _authority.Snapshot());
        }

        lock (_peerGate)
        {
            if (_peerObservations.TryGetValue(
                    envelope.SourceNodeId,
                    out var cursor))
            {
                if (cursor.ObservationInstanceId ==
                        envelope.SourceObservationInstanceId &&
                    envelope.ObservationSequence <= cursor.Sequence)
                {
                    return new RuntimeHaPeerApplyResult(
                        false,
                        "peer-observation-stale",
                        _authority.Snapshot());
                }

                if (cursor.ObservationInstanceId ==
                        envelope.SourceObservationInstanceId &&
                    cursor.SourceAuthorityInstanceId !=
                        envelope.SourceAuthorityInstanceId)
                {
                    var ambiguous = _authority.MarkAmbiguousPeerAuthority(
                        envelope.AuthorityEpoch);
                    return new RuntimeHaPeerApplyResult(
                        false,
                        "peer-authority-instance-conflict",
                        ambiguous);
                }
            }

            var local = _authority.Snapshot();
            if (envelope.AuthorityEpoch < local.AuthorityEpoch)
            {
                return new RuntimeHaPeerApplyResult(
                    false,
                    "peer-authority-epoch-stale",
                    local);
            }

            _peerObservations[envelope.SourceNodeId] =
                new PeerObservationCursor(
                    envelope.SourceObservationInstanceId,
                    envelope.ObservationSequence,
                    envelope.SourceAuthorityInstanceId,
                    envelope.AuthorityEpoch,
                    envelope.ObservedAtUtc);

            if (envelope.AmbiguousAuthority)
            {
                var ambiguous = _authority.MarkAmbiguousPeerAuthority(
                    envelope.AuthorityEpoch);
                return new RuntimeHaPeerApplyResult(
                    false,
                    "peer-reports-ambiguous-authority",
                    ambiguous);
            }

            if (envelope.AuthorityEpoch > local.AuthorityEpoch)
            {
                if (envelope.EffectiveActiveNodeId is not null)
                {
                    var ambiguous = _authority.MarkAmbiguousPeerAuthority(
                        envelope.AuthorityEpoch);
                    return new RuntimeHaPeerApplyResult(
                        false,
                        "peer-authority-ahead-with-claim",
                        ambiguous);
                }

                return new RuntimeHaPeerApplyResult(
                    false,
                    "peer-authority-ahead-requires-handoff",
                    local);
            }

            if (!string.Equals(
                    local.EffectiveActiveNodeId,
                    envelope.EffectiveActiveNodeId,
                    StringComparison.OrdinalIgnoreCase))
            {
                var ambiguous = _authority.MarkAmbiguousPeerAuthority(
                    envelope.AuthorityEpoch);
                return new RuntimeHaPeerApplyResult(
                    false,
                    "peer-effective-active-conflict",
                    ambiguous);
            }

            if (envelope.EffectiveActiveNodeId is not null &&
                envelope.EffectiveActiveNodeId.Equals(
                    envelope.SourceNodeId,
                    StringComparison.OrdinalIgnoreCase))
            {
                var claimed = _authority.ObserveAuthorityClaim(
                    envelope.SourceNodeId,
                    envelope.AuthorityEpoch,
                    claimsActive: true);
                if (claimed.AmbiguousAuthority)
                {
                    return new RuntimeHaPeerApplyResult(
                        false,
                        "peer-active-claim-conflict",
                        claimed);
                }
            }

            _authority.UpdateNodeReadiness(
                envelope.SourceNodeId,
                envelope.Readiness);
            return new RuntimeHaPeerApplyResult(
                true,
                "peer-observation-applied",
                _authority.Snapshot());
        }
    }

    public RuntimeHaPeerTransferResult BeginManualTransfer(
        string targetNodeId,
        long expectedEpoch)
    {
        if (!Enabled || LocalNodeId is null)
        {
            var transition = _authority.BeginManualTransfer(
                _authority.Definition.LocalNodeId,
                targetNodeId,
                expectedEpoch);
            return new RuntimeHaPeerTransferResult(transition, null);
        }

        var transitionResult = _authority.BeginManualTransfer(
            LocalNodeId,
            targetNodeId,
            expectedEpoch);
        return BuildTransferResult(
            transitionResult,
            RuntimeHaTransferHandoffPhase.Break);
    }

    public RuntimeHaPeerTransferResult CompleteManualTransfer(
        Guid transferId,
        string targetNodeId,
        long expectedBreakEpoch)
    {
        var transitionResult = _authority.CompleteManualTransfer(
            transferId,
            targetNodeId,
            expectedBreakEpoch);
        return BuildTransferResult(
            transitionResult,
            RuntimeHaTransferHandoffPhase.Grant);
    }

    public RuntimeHaPeerApplyResult ApplyPeerTransferHandoff(
        RuntimeHaTransferHandoffEnvelope handoff)
    {
        ArgumentNullException.ThrowIfNull(handoff);

        var headerFailure = ValidatePeerHeader(
            handoff.Schema,
            RuntimeHaTransferHandoffEnvelope.SchemaName,
            handoff.SchemaVersion,
            RuntimeHaTransferHandoffEnvelope.CurrentSchemaVersion,
            handoff.ClusterId,
            handoff.TopologyVersion,
            handoff.SourceNodeId,
            handoff.IssuedAtUtc);
        if (headerFailure is not null)
        {
            return new RuntimeHaPeerApplyResult(
                false,
                headerFailure,
                _authority.Snapshot());
        }

        if (LocalNodeId is null ||
            !handoff.TargetNodeId.Equals(
                LocalNodeId,
                StringComparison.OrdinalIgnoreCase))
        {
            return new RuntimeHaPeerApplyResult(
                false,
                "handoff-target-node-mismatch",
                _authority.Snapshot());
        }

        lock (_peerGate)
        {
            if (!_peerObservations.TryGetValue(
                    handoff.SourceNodeId,
                    out var peer))
            {
                return new RuntimeHaPeerApplyResult(
                    false,
                    "peer-observation-required",
                    _authority.Snapshot());
            }
            if (peer.ObservationInstanceId !=
                    handoff.SourceObservationInstanceId ||
                peer.SourceAuthorityInstanceId !=
                    handoff.SourceAuthorityInstanceId)
            {
                return new RuntimeHaPeerApplyResult(
                    false,
                    "handoff-peer-instance-mismatch",
                    _authority.Snapshot());
            }
            if (handoff.BreakEpoch <= peer.AuthorityEpoch)
            {
                return new RuntimeHaPeerApplyResult(
                    false,
                    "handoff-epoch-not-newer-than-peer-observation",
                    _authority.Snapshot());
            }

            if (handoff.Phase == RuntimeHaTransferHandoffPhase.Break)
            {
                if (_peerTransfers.ContainsKey(handoff.TransferId))
                {
                    return new RuntimeHaPeerApplyResult(
                        false,
                        "handoff-break-duplicate",
                        _authority.Snapshot());
                }

                var transition = _authority.ApplyPeerTransferBreak(handoff);
                if (!transition.Succeeded)
                {
                    return new RuntimeHaPeerApplyResult(
                        false,
                        transition.ReasonCode,
                        transition.Snapshot);
                }

                _peerTransfers[handoff.TransferId] = new PeerTransferCursor(
                    handoff.SourceObservationInstanceId,
                    handoff.SourceAuthorityInstanceId,
                    handoff.BreakEpoch,
                    handoff.HandoffSequence,
                    RuntimeHaTransferHandoffPhase.Break);

                return new RuntimeHaPeerApplyResult(
                    true,
                    transition.ReasonCode,
                    transition.Snapshot);
            }

            if (!_peerTransfers.TryGetValue(
                    handoff.TransferId,
                    out var transfer))
            {
                return new RuntimeHaPeerApplyResult(
                    false,
                    "handoff-break-required",
                    _authority.Snapshot());
            }
            if (transfer.SourceObservationInstanceId !=
                    handoff.SourceObservationInstanceId ||
                transfer.SourceAuthorityInstanceId !=
                    handoff.SourceAuthorityInstanceId ||
                transfer.BreakEpoch != handoff.BreakEpoch)
            {
                return new RuntimeHaPeerApplyResult(
                    false,
                    "handoff-grant-mismatch",
                    _authority.Snapshot());
            }
            if (handoff.HandoffSequence <= transfer.HandoffSequence)
            {
                return new RuntimeHaPeerApplyResult(
                    false,
                    "handoff-grant-stale",
                    _authority.Snapshot());
            }

            var grant = _authority.ApplyPeerTransferGrant(handoff);
            if (!grant.Succeeded)
            {
                return new RuntimeHaPeerApplyResult(
                    false,
                    grant.ReasonCode,
                    grant.Snapshot);
            }

            _peerTransfers[handoff.TransferId] = transfer with
            {
                HandoffSequence = handoff.HandoffSequence,
                HighestPhase = RuntimeHaTransferHandoffPhase.Grant
            };

            return new RuntimeHaPeerApplyResult(
                true,
                grant.ReasonCode,
                grant.Snapshot);
        }
    }

    public RuntimeHaTopologySnapshot ReportPeerUnavailable(string peerNodeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(peerNodeId);
        if (LocalNodeId is not null &&
            LocalNodeId.Equals(peerNodeId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The local node cannot be reported as an unavailable peer.");
        }

        return _authority.ReportNodeUnavailable(peerNodeId);
    }

    public RuntimeSessionContinuityImportResult ApplyPeerSessionContinuity(
        RuntimeSessionLeaseContinuityEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        if (!Enabled || ClusterId is null || LocalNodeId is null)
        {
            return new RuntimeSessionContinuityImportResult(
                false,
                "ha-disabled",
                null);
        }

        if (envelope.SourceNodeId.Equals(
                LocalNodeId,
                StringComparison.OrdinalIgnoreCase))
        {
            return new RuntimeSessionContinuityImportResult(
                false,
                "session-source-is-local-node",
                null);
        }
        if (!_authority.Definition.Nodes.Any(node =>
                node.NodeId.Equals(
                    envelope.SourceNodeId,
                    StringComparison.OrdinalIgnoreCase)))
        {
            return new RuntimeSessionContinuityImportResult(
                false,
                "session-source-node-unknown",
                null);
        }

        return SessionContinuity.ImportPeerEnvelope(
            envelope,
            ClusterId,
            envelope.SourceNodeId);
    }

    private RuntimeHaPeerTransferResult BuildTransferResult(
        RuntimeHaTransitionResult transition,
        RuntimeHaTransferHandoffPhase phase)
    {
        if (!transition.Succeeded ||
            transition.Transfer is null ||
            ClusterId is null ||
            LocalNodeId is null)
        {
            return new RuntimeHaPeerTransferResult(transition, null);
        }

        long sequence;
        lock (_peerGate)
        {
            sequence = checked(++_peerHandoffSequence);
        }

        var handoff = new RuntimeHaTransferHandoffEnvelope(
            RuntimeHaTransferHandoffEnvelope.SchemaName,
            RuntimeHaTransferHandoffEnvelope.CurrentSchemaVersion,
            ClusterId,
            _authority.Definition.TopologyVersion,
            Guid.NewGuid(),
            _peerObservationInstanceId,
            sequence,
            transition.Snapshot.AuthorityInstanceId,
            phase,
            transition.Transfer.TransferId,
            transition.Transfer.SourceNodeId,
            transition.Transfer.TargetNodeId,
            transition.Transfer.BreakEpoch,
            _utcNow());

        return new RuntimeHaPeerTransferResult(transition, handoff);
    }

    private string? ValidatePeerHeader(
        string schema,
        string expectedSchema,
        int schemaVersion,
        int expectedSchemaVersion,
        string clusterId,
        long topologyVersion,
        string sourceNodeId,
        DateTimeOffset observedAtUtc)
    {
        if (!Enabled || ClusterId is null || LocalNodeId is null)
            return "ha-disabled";
        if (!string.Equals(schema, expectedSchema, StringComparison.Ordinal) ||
            schemaVersion != expectedSchemaVersion)
            return "peer-schema-mismatch";
        if (!ClusterId.Equals(clusterId, StringComparison.OrdinalIgnoreCase))
            return "peer-cluster-mismatch";
        if (_authority.Definition.TopologyVersion != topologyVersion)
            return "peer-topology-version-mismatch";
        if (LocalNodeId.Equals(sourceNodeId, StringComparison.OrdinalIgnoreCase))
            return "peer-source-is-local-node";
        if (!_authority.Definition.Nodes.Any(node =>
                node.NodeId.Equals(
                    sourceNodeId,
                    StringComparison.OrdinalIgnoreCase)))
            return "peer-source-node-unknown";
        if (!IsFreshPeerTime(observedAtUtc))
            return "peer-evidence-stale";
        return null;
    }

    private bool IsFreshPeerTime(DateTimeOffset observedAtUtc)
    {
        var now = _utcNow();
        var delta = now - observedAtUtc;
        return delta <= _authority.Definition.FreshnessWindow &&
            delta >= -_authority.Definition.FreshnessWindow;
    }

    private sealed record PeerObservationCursor(
        Guid ObservationInstanceId,
        long Sequence,
        Guid SourceAuthorityInstanceId,
        long AuthorityEpoch,
        DateTimeOffset ObservedAtUtc);

    private sealed record PeerTransferCursor(
        Guid SourceObservationInstanceId,
        Guid SourceAuthorityInstanceId,
        long BreakEpoch,
        long HandoffSequence,
        RuntimeHaTransferHandoffPhase HighestPhase);
}
