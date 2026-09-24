using Scada.Api.Runtime;

namespace Scada.Drivers.Tests;

public sealed class RuntimeHighAvailabilityPeerBoundaryTests
{
    [Fact]
    public void TwoIndependentServices_ConvergeReadinessOnlyThroughPeerObservations()
    {
        var pair = CreateReadyPair();

        Assert.Equal("node-a", pair.NodeA.Snapshot().EffectiveActiveNodeId);
        Assert.Equal("node-a", pair.NodeB.Snapshot().EffectiveActiveNodeId);
        Assert.Equal(RuntimeHaState.ReadyStandby, Node(pair.NodeA.Snapshot(), "node-b").State);
        Assert.Equal(RuntimeHaState.ReadyStandby, Node(pair.NodeB.Snapshot(), "node-b").State);
        Assert.True(pair.NodeA.TryAcquireLocalIndustrialAuthority().Allowed);
        Assert.False(pair.NodeB.TryAcquireLocalIndustrialAuthority().Allowed);

        var stale = pair.NodeB.ApplyPeerObservation(pair.LastObservationFromA);
        Assert.False(stale.Accepted);
        Assert.Equal("peer-observation-stale", stale.ReasonCode);
    }

    [Fact]
    public void ManualTransferAcrossIndependentServices_IsBreakBeforeMake()
    {
        var pair = CreateReadyPair();
        var tokenA = pair.NodeA.TryAcquireLocalIndustrialAuthority();

        Assert.True(tokenA.Allowed);
        Assert.NotNull(tokenA.Token);

        var begin = pair.NodeA.BeginManualTransfer(
            "node-b",
            pair.NodeA.Snapshot().AuthorityEpoch);

        Assert.True(begin.Transition.Succeeded);
        Assert.NotNull(begin.Handoff);
        Assert.Equal(RuntimeHaTransferHandoffPhase.Break, begin.Handoff!.Phase);
        Assert.Null(begin.Transition.Snapshot.EffectiveActiveNodeId);
        Assert.False(pair.NodeA.ValidateLocalIndustrialAuthority(tokenA.Token!));

        var invalidCluster = pair.NodeB.ApplyPeerTransferHandoff(
            begin.Handoff with { ClusterId = "other-cluster" });
        Assert.False(invalidCluster.Accepted);
        Assert.False(pair.NodeB.TryAcquireLocalIndustrialAuthority().Allowed);

        var appliedBreak = pair.NodeB.ApplyPeerTransferHandoff(begin.Handoff);
        Assert.True(appliedBreak.Accepted);
        Assert.Equal("peer-break-applied", appliedBreak.ReasonCode);
        Assert.Null(appliedBreak.Snapshot.EffectiveActiveNodeId);
        Assert.False(pair.NodeB.TryAcquireLocalIndustrialAuthority().Allowed);

        var complete = pair.NodeA.CompleteManualTransfer(
            begin.Transition.Transfer!.TransferId,
            "node-b",
            begin.Transition.Transfer.BreakEpoch);

        Assert.True(complete.Transition.Succeeded);
        Assert.NotNull(complete.Handoff);
        Assert.Equal(RuntimeHaTransferHandoffPhase.Grant, complete.Handoff!.Phase);
        Assert.False(pair.NodeA.TryAcquireLocalIndustrialAuthority().Allowed);

        var appliedGrant = pair.NodeB.ApplyPeerTransferHandoff(complete.Handoff);
        Assert.True(appliedGrant.Accepted);
        Assert.Equal("peer-grant-applied", appliedGrant.ReasonCode);
        Assert.Equal("node-b", appliedGrant.Snapshot.EffectiveActiveNodeId);
        Assert.True(pair.NodeB.TryAcquireLocalIndustrialAuthority().Allowed);
        Assert.False(pair.NodeA.ValidateLocalIndustrialAuthority(tokenA.Token!));
    }

    [Fact]
    public void GrantWithoutAcceptedBreak_IsRejected()
    {
        var pair = CreateReadyPair();
        var begin = pair.NodeA.BeginManualTransfer(
            "node-b",
            pair.NodeA.Snapshot().AuthorityEpoch);
        var complete = pair.NodeA.CompleteManualTransfer(
            begin.Transition.Transfer!.TransferId,
            "node-b",
            begin.Transition.Transfer.BreakEpoch);

        var grantWithoutBreak = pair.NodeB.ApplyPeerTransferHandoff(
            complete.Handoff!);

        Assert.False(grantWithoutBreak.Accepted);
        Assert.Equal("handoff-break-required", grantWithoutBreak.ReasonCode);
        Assert.False(pair.NodeB.TryAcquireLocalIndustrialAuthority().Allowed);
    }

    [Fact]
    public void ConflictingPeerActiveClaim_FailsClosedAcrossIndependentServices()
    {
        var pair = CreateReadyPair();
        var conflicting = pair.NodeB.CreatePeerObservation() with
        {
            EffectiveActiveNodeId = "node-b"
        };

        var applied = pair.NodeA.ApplyPeerObservation(conflicting);

        Assert.False(applied.Accepted);
        Assert.Equal("peer-effective-active-conflict", applied.ReasonCode);
        Assert.True(applied.Snapshot.AmbiguousAuthority);
        Assert.Null(applied.Snapshot.EffectiveActiveNodeId);
        Assert.False(pair.NodeA.TryAcquireLocalIndustrialAuthority().Allowed);
    }

    [Fact]
    public void PeerAuthorityInstanceChangeWithoutHandoff_FailsClosed()
    {
        var pair = CreateReadyPair();
        var restartedPeer = pair.NodeA.CreatePeerObservation() with
        {
            SourceObservationInstanceId =
                Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            SourceAuthorityInstanceId =
                Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd")
        };

        var applied = pair.NodeB.ApplyPeerObservation(restartedPeer);

        Assert.False(applied.Accepted);
        Assert.Equal("peer-authority-instance-conflict", applied.ReasonCode);
        Assert.True(applied.Snapshot.AmbiguousAuthority);
        Assert.Null(applied.Snapshot.EffectiveActiveNodeId);
        Assert.False(pair.NodeB.TryAcquireLocalIndustrialAuthority().Allowed);
    }

    [Fact]
    public void PeerLoss_DoesNotPromoteIndependentStandby()
    {
        var pair = CreateReadyPair();

        var snapshot = pair.NodeB.ReportPeerUnavailable("node-a");

        Assert.Null(snapshot.EffectiveActiveNodeId);
        Assert.Equal(RuntimeHaState.Faulted, Node(snapshot, "node-a").State);
        Assert.Equal(RuntimeHaState.Isolated, Node(snapshot, "node-b").State);
        Assert.False(pair.NodeB.TryAcquireLocalIndustrialAuthority().Allowed);
    }

    [Theory]
    [InlineData("wrong-cluster", 3, "peer-cluster-mismatch")]
    [InlineData("cluster-a", 99, "peer-topology-version-mismatch")]
    public void PeerObservation_WithMismatchedTopologyIdentity_FailsClosed(
        string clusterId,
        long topologyVersion,
        string expectedReason)
    {
        var pair = CreateReadyPair();
        var observation = pair.NodeA.CreatePeerObservation() with
        {
            ClusterId = clusterId,
            TopologyVersion = topologyVersion
        };

        var applied = pair.NodeB.ApplyPeerObservation(observation);

        Assert.False(applied.Accepted);
        Assert.Equal(expectedReason, applied.ReasonCode);
        Assert.False(pair.NodeB.TryAcquireLocalIndustrialAuthority().Allowed);
    }

    [Fact]
    public void PeerObservation_WithAuthorityAheadButNoHandoff_IsRejected()
    {
        var pair = CreateReadyPair();
        var observation = pair.NodeA.CreatePeerObservation() with
        {
            AuthorityEpoch = pair.NodeB.Snapshot().AuthorityEpoch + 1,
            EffectiveActiveNodeId = null
        };

        var applied = pair.NodeB.ApplyPeerObservation(observation);

        Assert.False(applied.Accepted);
        Assert.Equal("peer-authority-ahead-requires-handoff", applied.ReasonCode);
        Assert.False(pair.NodeB.TryAcquireLocalIndustrialAuthority().Allowed);
    }

    [Fact]
    public void SessionContinuityImport_PreservesOneLogicalLeaseAndRejectsStaleOrResurrectedState()
    {
        var pair = CreateReadyPair();
        var now = pair.Clock.UtcNow;
        var lease = Lease(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            RuntimeConnectionClass.ViewOnly,
            now,
            now.AddMinutes(2),
            generation: 1,
            authorityRevision: 10);

        var exported = pair.NodeA.SessionContinuity.CaptureOrRetain(
            lease,
            "cluster-a",
            "node-a");
        var imported = pair.NodeB.ApplyPeerSessionContinuity(exported);

        Assert.True(imported.Accepted);
        Assert.Equal(1, pair.NodeB.SessionContinuity.ActiveLogicalLeaseCount("cluster-a"));

        var duplicate = pair.NodeB.ApplyPeerSessionContinuity(exported);
        Assert.False(duplicate.Accepted);
        Assert.Equal("session-state-not-newer", duplicate.ReasonCode);
        Assert.Equal(1, pair.NodeB.SessionContinuity.ActiveLogicalLeaseCount("cluster-a"));

        var newer = exported with
        {
            Generation = 2,
            LastHeartbeatUtc = now.AddSeconds(10),
            ExpiresAtUtc = now.AddMinutes(3)
        };
        var newerApplied = pair.NodeB.ApplyPeerSessionContinuity(newer);
        Assert.True(newerApplied.Accepted);

        var lowerGeneration = exported with
        {
            LastHeartbeatUtc = now.AddSeconds(20),
            ExpiresAtUtc = now.AddMinutes(4)
        };
        var lowerRejected = pair.NodeB.ApplyPeerSessionContinuity(lowerGeneration);
        Assert.False(lowerRejected.Accepted);
        Assert.Equal("session-generation-stale", lowerRejected.ReasonCode);

        Assert.True(pair.NodeB.SessionContinuity.Terminate(
            "cluster-a",
            "operator",
            "elitego-1",
            lease.SessionId));

        var resurrect = pair.NodeB.ApplyPeerSessionContinuity(newer with
        {
            LastHeartbeatUtc = now.AddSeconds(30),
            ExpiresAtUtc = now.AddMinutes(5)
        });
        Assert.False(resurrect.Accepted);
        Assert.Equal("session-terminated-or-expired-generation", resurrect.ReasonCode);
        Assert.Equal(0, pair.NodeB.SessionContinuity.ActiveLogicalLeaseCount("cluster-a"));
    }

    [Fact]
    public void SessionContinuityImport_RejectsExpiredEnvelope()
    {
        var pair = CreateReadyPair();
        var now = pair.Clock.UtcNow;
        var expired = new RuntimeSessionLeaseContinuityEnvelope(
            RuntimeSessionLeaseContinuityRegistry.Schema,
            RuntimeSessionLeaseContinuityRegistry.SchemaVersion,
            "cluster-a",
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            "operator",
            "elitego-2",
            RuntimeConnectionClass.Interactive,
            now.AddMinutes(-5),
            now.AddMinutes(-2),
            now.AddSeconds(-1),
            "node-a",
            new RuntimeHaRuntimeIdentity("engineering", "project-a", 7),
            Generation: 1,
            AuthorityRevision: 10,
            ReplicatedAtUtc: now.AddMinutes(-2));

        var result = pair.NodeB.ApplyPeerSessionContinuity(expired);

        Assert.False(result.Accepted);
        Assert.Equal("session-expired", result.ReasonCode);
        Assert.Equal(0, pair.NodeB.SessionContinuity.ActiveLogicalLeaseCount("cluster-a"));
    }

    private static ReadyPair CreateReadyPair()
    {
        var clock = new MutableClock(
            DateTimeOffset.Parse("2026-09-24T18:00:00Z"));
        var nodeA = new RuntimeHighAvailabilityService(
            Topology("node-a"),
            () => clock.UtcNow,
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
        var nodeB = new RuntimeHighAvailabilityService(
            Topology("node-b"),
            () => clock.UtcNow,
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));

        nodeA.ObserveLocalReadiness(Evidence(clock.UtcNow));
        nodeB.ObserveLocalReadiness(Evidence(clock.UtcNow));

        var fromA = nodeA.CreatePeerObservation();
        var fromB = nodeB.CreatePeerObservation();

        Assert.True(nodeA.ApplyPeerObservation(fromB).Accepted);
        Assert.True(nodeB.ApplyPeerObservation(fromA).Accepted);

        return new ReadyPair(nodeA, nodeB, clock, fromA, fromB);
    }

    private static RuntimeHaTopologyDefinition Topology(string localNodeId) =>
        new(
            Enabled: true,
            ClusterId: "cluster-a",
            LocalNodeId: localNodeId,
            InitialActiveNodeId: "node-a",
            TopologyVersion: 3,
            FreshnessWindow: TimeSpan.FromSeconds(15),
            Nodes: new[]
            {
                new RuntimeHaNodeDefinition(
                    "node-a",
                    new[]
                    {
                        new RuntimeHaEndpoint(
                            RuntimeHaEndpointKind.Local,
                            "https://10.0.0.1:5001",
                            0),
                        new RuntimeHaEndpoint(
                            RuntimeHaEndpointKind.Remote,
                            "https://a.example.test",
                            1)
                    }),
                new RuntimeHaNodeDefinition(
                    "node-b",
                    new[]
                    {
                        new RuntimeHaEndpoint(
                            RuntimeHaEndpointKind.Local,
                            "https://10.0.0.2:5001",
                            0),
                        new RuntimeHaEndpoint(
                            RuntimeHaEndpointKind.Remote,
                            "https://b.example.test",
                            1)
                    })
            });

    private static RuntimeHaNodeReadinessEvidence Evidence(
        DateTimeOffset observedAtUtc) =>
        new(
            Healthy: true,
            SynchronizationComplete: true,
            HaLicenseEntitled: true,
            Runtime: new RuntimeHaRuntimeIdentity(
                "engineering",
                "project-a",
                7),
            ObservedAtUtc: observedAtUtc);

    private static RuntimeHaNodeSnapshot Node(
        RuntimeHaTopologySnapshot snapshot,
        string nodeId) =>
        Assert.Single(snapshot.Nodes, node =>
            node.NodeId.Equals(nodeId, StringComparison.OrdinalIgnoreCase));

    private static RuntimeSessionLease Lease(
        Guid sessionId,
        RuntimeConnectionClass connectionClass,
        DateTimeOffset issuedAtUtc,
        DateTimeOffset expiresAtUtc,
        long generation,
        long authorityRevision) =>
        new(
            sessionId,
            "operator",
            "elitego-1",
            connectionClass,
            issuedAtUtc,
            issuedAtUtc,
            expiresAtUtc,
            "node-a",
            "cluster-a",
            "engineering",
            "project-a",
            7,
            issuedAtUtc,
            generation,
            authorityRevision);

    private sealed record ReadyPair(
        RuntimeHighAvailabilityService NodeA,
        RuntimeHighAvailabilityService NodeB,
        MutableClock Clock,
        RuntimeHaPeerObservationEnvelope LastObservationFromA,
        RuntimeHaPeerObservationEnvelope LastObservationFromB);

    private sealed class MutableClock(DateTimeOffset utcNow)
    {
        public DateTimeOffset UtcNow { get; private set; } = utcNow;
        public void Advance(TimeSpan duration) => UtcNow = UtcNow.Add(duration);
    }
}
