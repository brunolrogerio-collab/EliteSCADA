using Scada.Api.Runtime;
using Scada.Core.Abstractions;
using Scada.Core.Events;
using Scada.DriverHost.Runtime;

namespace Scada.Drivers.Tests;

public sealed class RuntimeHighAvailabilityTests
{
    [Fact]
    public void Topology_ExposesStableDistinctNodesAndLocalRemoteEndpoints()
    {
        var coordinator = CreateCoordinator(out _);

        var snapshot = coordinator.Snapshot();

        Assert.True(snapshot.Enabled);
        Assert.Equal("cluster-a", snapshot.ClusterId);
        Assert.Equal("node-a", snapshot.LocalNodeId);
        Assert.Equal("node-a", snapshot.EffectiveActiveNodeId);
        Assert.Equal(2, snapshot.Nodes.Count);
        Assert.Equal(2, snapshot.Nodes.Select(node => node.NodeId).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(snapshot.Nodes, node =>
        {
            Assert.Contains(node.Endpoints, endpoint => endpoint.Kind == RuntimeHaEndpointKind.Local);
            Assert.Contains(node.Endpoints, endpoint => endpoint.Kind == RuntimeHaEndpointKind.Remote);
        });
    }

    [Fact]
    public void ManualTransfer_IsBreakBeforeMake_AndStaleFencingTokenIsRejected()
    {
        var coordinator = CreateReadyPair(out _);
        var before = coordinator.Snapshot();
        var activeToken = coordinator.TryAcquireIndustrialAuthority("node-a");

        Assert.True(activeToken.Allowed);
        Assert.NotNull(activeToken.Token);
        Assert.Equal(RuntimeHaState.ReadyStandby, Node(before, "node-b").State);

        var begin = coordinator.BeginManualTransfer(
            "node-a",
            "node-b",
            before.AuthorityEpoch);

        Assert.True(begin.Succeeded);
        Assert.Equal("break-established", begin.ReasonCode);
        Assert.Null(begin.Snapshot.EffectiveActiveNodeId);
        Assert.Equal(RuntimeHaState.Demoting, Node(begin.Snapshot, "node-a").State);
        Assert.Equal(RuntimeHaState.Promoting, Node(begin.Snapshot, "node-b").State);
        Assert.False(coordinator.ValidateIndustrialAuthority(activeToken.Token!));
        Assert.False(coordinator.TryAcquireIndustrialAuthority("node-a").Allowed);
        Assert.False(coordinator.TryAcquireIndustrialAuthority("node-b").Allowed);

        var complete = coordinator.CompleteManualTransfer(
            begin.Transfer!.TransferId,
            "node-b",
            begin.Transfer.BreakEpoch);

        Assert.True(complete.Succeeded);
        Assert.Equal("node-b", complete.Snapshot.EffectiveActiveNodeId);
        Assert.Equal(RuntimeHaState.Active, Node(complete.Snapshot, "node-b").State);
        Assert.False(coordinator.TryAcquireIndustrialAuthority("node-a").Allowed);
        Assert.True(coordinator.TryAcquireIndustrialAuthority("node-b").Allowed);
    }

    [Fact]
    public void PeerLoss_DoesNotPromoteStandby()
    {
        var coordinator = CreateReadyPair(out _);

        var afterLoss = coordinator.ReportNodeUnavailable("node-a");

        Assert.Null(afterLoss.EffectiveActiveNodeId);
        Assert.Equal(RuntimeHaState.Faulted, Node(afterLoss, "node-a").State);
        Assert.Equal(RuntimeHaState.Isolated, Node(afterLoss, "node-b").State);
        Assert.False(coordinator.TryAcquireIndustrialAuthority("node-b").Allowed);
    }

    [Fact]
    public void ConflictingActiveClaim_FailsClosedAsAmbiguousAuthority()
    {
        var coordinator = CreateReadyPair(out _);
        var epoch = coordinator.Snapshot().AuthorityEpoch;

        var ambiguous = coordinator.ObserveAuthorityClaim(
            "node-b",
            epoch,
            claimsActive: true);

        Assert.True(ambiguous.AmbiguousAuthority);
        Assert.Null(ambiguous.EffectiveActiveNodeId);
        Assert.All(ambiguous.Nodes, node =>
            Assert.True(node.State is RuntimeHaState.Isolated or RuntimeHaState.Faulted));
        Assert.False(coordinator.TryAcquireIndustrialAuthority("node-a").Allowed);
        Assert.False(coordinator.TryAcquireIndustrialAuthority("node-b").Allowed);
    }

    [Theory]
    [InlineData(false, 7)]
    [InlineData(true, 8)]
    public void ReadyStandby_RequiresHaEntitlementAndCompatibleRuntime(
        bool haEntitled,
        long standbyRevision)
    {
        var coordinator = CreateCoordinator(out var now);
        coordinator.UpdateNodeReadiness(
            "node-a",
            Evidence(now, haEntitled: true, revision: 7, synchronized: true));
        coordinator.UpdateNodeReadiness(
            "node-b",
            Evidence(now, haEntitled, standbyRevision, synchronized: true));

        var snapshot = coordinator.Snapshot();
        var standby = Node(snapshot, "node-b");

        Assert.NotEqual(RuntimeHaState.ReadyStandby, standby.State);
        Assert.False(standby.Ready);

        var transfer = coordinator.BeginManualTransfer(
            "node-a",
            "node-b",
            snapshot.AuthorityEpoch);
        Assert.False(transfer.Succeeded);
        Assert.Equal("target-not-ready-standby", transfer.ReasonCode);
    }

    [Fact]
    public void TopologyFreshness_IsTruthfulWhenEvidenceAges()
    {
        var coordinator = CreateReadyPair(out var clock);
        Assert.All(coordinator.Snapshot().Nodes, node => Assert.True(node.Fresh));

        clock.Advance(TimeSpan.FromSeconds(20));

        var stale = coordinator.Snapshot();
        Assert.All(stale.Nodes, node => Assert.False(node.Fresh));
    }

    [Fact]
    public void RuntimeSessionContinuity_RetainsOneLogicalLeaseAndNeverResurrectsExpiredState()
    {
        var now = DateTimeOffset.Parse("2026-09-24T18:00:00Z");
        var registry = new RuntimeSessionLeaseContinuityRegistry(() => now);
        var first = Lease(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            RuntimeConnectionClass.ViewOnly,
            now,
            now.AddMinutes(1));
        var duplicateTransportLease = Lease(
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            RuntimeConnectionClass.Interactive,
            now,
            now.AddMinutes(1));

        var captured = registry.CaptureOrRetain(first, "cluster-a", "node-a");
        var retained = registry.CaptureOrRetain(
            duplicateTransportLease,
            "cluster-a",
            "node-b");

        Assert.Equal(captured.SessionId, retained.SessionId);
        Assert.Equal(RuntimeConnectionClass.ViewOnly, retained.ConnectionClass);
        Assert.Equal(1, registry.ActiveLogicalLeaseCount("cluster-a"));

        var resume = registry.TryResume(
            "cluster-a",
            "operator",
            "elitego-1",
            new RuntimeHaRuntimeIdentity("engineering", "project-a", 7));
        Assert.True(resume.Resumable);
        Assert.Equal(RuntimeConnectionClass.ViewOnly, resume.Lease!.ConnectionClass);

        now = now.AddMinutes(2);
        var expired = registry.TryResume(
            "cluster-a",
            "operator",
            "elitego-1",
            new RuntimeHaRuntimeIdentity("engineering", "project-a", 7));

        Assert.False(expired.Resumable);
        Assert.Equal(0, registry.ActiveLogicalLeaseCount("cluster-a"));
    }

    [Fact]
    public async Task RuntimeEventGate_StopsAlarmHistorianScriptEventFanoutWhenAuthorityIsLost()
    {
        var external = new InMemoryScadaEventBus();
        var allowed = true;
        var gate = new RuntimeEventGate(
            external,
            forwardingEnabled: true,
            effectAuthority: () => allowed);
        var localCount = 0;
        var externalCount = 0;
        using var local = gate.Subscribe<TestEvent>(_ =>
        {
            localCount++;
            return ValueTask.CompletedTask;
        });
        using var remote = external.Subscribe<TestEvent>(_ =>
        {
            externalCount++;
            return ValueTask.CompletedTask;
        });

        await gate.PublishAsync(new TestEvent(DateTimeOffset.UtcNow));
        Assert.Equal(1, localCount);
        Assert.Equal(1, externalCount);

        allowed = false;
        await gate.PublishAsync(new TestEvent(DateTimeOffset.UtcNow));

        Assert.Equal(1, localCount);
        Assert.Equal(1, externalCount);
    }

    private static RuntimeHaAuthorityCoordinator CreateReadyPair(out MutableClock clock)
    {
        var coordinator = CreateCoordinator(out clock);
        coordinator.UpdateNodeReadiness(
            "node-a",
            Evidence(clock.UtcNow, haEntitled: true, revision: 7, synchronized: true));
        coordinator.UpdateNodeReadiness(
            "node-b",
            Evidence(clock.UtcNow, haEntitled: true, revision: 7, synchronized: true));
        return coordinator;
    }

    private static RuntimeHaAuthorityCoordinator CreateCoordinator(out MutableClock clock)
    {
        clock = new MutableClock(DateTimeOffset.Parse("2026-09-24T18:00:00Z"));
        return new RuntimeHaAuthorityCoordinator(
            new RuntimeHaTopologyDefinition(
                Enabled: true,
                ClusterId: "cluster-a",
                LocalNodeId: "node-a",
                InitialActiveNodeId: "node-a",
                TopologyVersion: 3,
                FreshnessWindow: TimeSpan.FromSeconds(15),
                Nodes: new[]
                {
                    new RuntimeHaNodeDefinition(
                        "node-a",
                        new[]
                        {
                            new RuntimeHaEndpoint(RuntimeHaEndpointKind.Local, "https://10.0.0.1:5001", 0),
                            new RuntimeHaEndpoint(RuntimeHaEndpointKind.Remote, "https://a.example.test", 1)
                        }),
                    new RuntimeHaNodeDefinition(
                        "node-b",
                        new[]
                        {
                            new RuntimeHaEndpoint(RuntimeHaEndpointKind.Local, "https://10.0.0.2:5001", 0),
                            new RuntimeHaEndpoint(RuntimeHaEndpointKind.Remote, "https://b.example.test", 1)
                        })
                }),
            () => clock.UtcNow,
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
    }

    private static RuntimeHaNodeReadinessEvidence Evidence(
        DateTimeOffset observedAtUtc,
        bool haEntitled,
        long revision,
        bool synchronized) =>
        new(
            Healthy: true,
            SynchronizationComplete: synchronized,
            HaLicenseEntitled: haEntitled,
            Runtime: new RuntimeHaRuntimeIdentity("engineering", "project-a", revision),
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
        DateTimeOffset expiresAtUtc) =>
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
            Generation: 1,
            AuthorityRevision: 1);

    private sealed record TestEvent(DateTimeOffset OccurredAt) : IScadaEvent;

    private sealed class MutableClock(DateTimeOffset utcNow)
    {
        public DateTimeOffset UtcNow { get; private set; } = utcNow;
        public void Advance(TimeSpan duration) => UtcNow = UtcNow.Add(duration);
    }
}
