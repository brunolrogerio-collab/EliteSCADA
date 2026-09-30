using Scada.Security.Authorization;

namespace Scada.Drivers.Tests;

public sealed class RuntimeSessionLeaseReplicationAdoptionTests
{
    [Fact]
    public async Task InMemoryAdoption_PreservesLogicalLeaseAndConsumesOneCanonicalSeat()
    {
        var now = DateTimeOffset.Parse("2026-09-30T18:30:00Z");
        await using var store = new InMemoryRuntimeSessionLeaseStore(() => now);
        var authority = await store.GetAuthorityStateAsync();
        var runtime = new RuntimeSessionRuntimeIdentity(
            "engineering",
            "project-a",
            7,
            now.AddMinutes(-5));
        var sessionId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var replicated = new RuntimeSessionLeaseState(
            sessionId,
            "operator",
            "runtime-client",
            "interactive",
            4,
            now.AddMinutes(-2),
            now.AddSeconds(-5),
            now.AddMinutes(2),
            runtime,
            "node-a",
            "cluster-a",
            IsActive: true,
            authority.AuthorityRevision);

        var adopted = await store.AdoptReplicatedAsync(
            replicated,
            "cluster-a",
            "node-a",
            runtime,
            authority.AuthorityRevision);

        Assert.True(adopted.Accepted, adopted.ReasonCode);
        Assert.Equal(sessionId, adopted.Lease!.SessionId);
        Assert.Equal(4, adopted.Lease.Generation);
        Assert.Equal("operator", adopted.Lease.SubjectId);
        Assert.Equal("runtime-client", adopted.Lease.ClientInstanceId);
        Assert.Equal("node-a", adopted.Lease.ServerNode);

        var capacity = new RuntimeSessionSeatCapacity(
            InteractiveSeats: 1,
            ViewOnlySeats: 0);
        var resumed = await store.AdmitWithCapacityAsync(
            new RuntimeSessionLeaseCapacityAdmission(
                new RuntimeSessionLeaseAdmission(
                    "operator",
                    "runtime-client",
                    "interactive",
                    runtime,
                    TimeSpan.FromMinutes(1),
                    ServerNode: "node-b",
                    ClusterId: "cluster-a"),
                capacity,
                authority.AuthorityRevision));

        Assert.True(resumed.IsAdmitted, resumed.ReasonCode);
        Assert.Equal(sessionId, resumed.Lease!.SessionId);

        var secondLogicalSession = await store.AdmitWithCapacityAsync(
            new RuntimeSessionLeaseCapacityAdmission(
                new RuntimeSessionLeaseAdmission(
                    "operator-2",
                    "runtime-client-2",
                    "interactive",
                    runtime,
                    TimeSpan.FromMinutes(1),
                    ServerNode: "node-b",
                    ClusterId: "cluster-a"),
                capacity,
                authority.AuthorityRevision));

        Assert.False(secondLogicalSession.IsAdmitted);
        Assert.Equal(
            RuntimeSessionSeatReservationReasonCode.InteractiveQuotaExhaustedNoEligibleViewOnly,
            secondLogicalSession.ReasonCode);
    }

    [Fact]
    public async Task InMemoryAdoption_TombstonePreventsResurrection_AndTransitionFailsClosed()
    {
        var now = DateTimeOffset.Parse("2026-09-30T18:30:00Z");
        await using var store = new InMemoryRuntimeSessionLeaseStore(() => now);
        var authority = await store.GetAuthorityStateAsync();
        var runtime = new RuntimeSessionRuntimeIdentity(
            "engineering",
            "project-a",
            7,
            now.AddMinutes(-5));
        var sessionId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var replicated = new RuntimeSessionLeaseState(
            sessionId,
            "operator",
            "runtime-client",
            "viewer",
            2,
            now.AddMinutes(-1),
            now.AddSeconds(-5),
            now.AddMinutes(2),
            runtime,
            "node-a",
            "cluster-a",
            IsActive: true,
            authority.AuthorityRevision);

        Assert.True((await store.AdoptReplicatedAsync(
            replicated,
            "cluster-a",
            "node-a",
            runtime,
            authority.AuthorityRevision)).Accepted);

        var tombstone = new RuntimeSessionLeaseTombstoneState(
            "cluster-a",
            "operator",
            "runtime-client",
            sessionId,
            Generation: 3,
            authority.AuthorityRevision,
            replicated.IssuedAtUtc,
            "node-a",
            now,
            "terminated");
        var applied = await store.ApplyReplicatedTombstoneAsync(
            tombstone,
            "cluster-a",
            "node-a");
        Assert.True(applied.Accepted, applied.ReasonCode);

        var resurrected = await store.AdoptReplicatedAsync(
            replicated with
            {
                Generation = 4,
                LastHeartbeatUtc = now,
                ExpiresAtUtc = now.AddMinutes(3)
            },
            "cluster-a",
            "node-a",
            runtime,
            authority.AuthorityRevision);
        Assert.False(resurrected.Accepted);
        Assert.Equal("session-resurrection-rejected", resurrected.ReasonCode);

        var transition = await store.BeginAuthorityTransitionAsync("replace", now);
        var duringTransition = await store.AdoptReplicatedAsync(
            replicated with
            {
                SessionId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                ClientInstanceId = "runtime-client-transition",
                IssuedAtUtc = now,
                LastHeartbeatUtc = now,
                ExpiresAtUtc = now.AddMinutes(2)
            },
            "cluster-a",
            "node-a",
            runtime,
            transition.BaseAuthorityRevision);

        Assert.False(duringTransition.Accepted);
        Assert.Equal("authority-transition-pending", duringTransition.ReasonCode);
        Assert.True(await store.AbortAuthorityTransitionAsync(
            transition.TransitionId,
            transition.BaseAuthorityRevision));
    }

    [Fact]
    public async Task InMemoryAdoption_RejectsWrongRuntimeProvenanceExpiryAndAuthorityRevision()
    {
        var now = DateTimeOffset.Parse("2026-09-30T18:30:00Z");
        await using var store = new InMemoryRuntimeSessionLeaseStore(() => now);
        var authority = await store.GetAuthorityStateAsync();
        var runtime = new RuntimeSessionRuntimeIdentity(
            "engineering",
            "project-a",
            7,
            now.AddMinutes(-5));
        var lease = new RuntimeSessionLeaseState(
            Guid.NewGuid(),
            "operator",
            "runtime-client",
            "viewer",
            1,
            now.AddMinutes(-1),
            now.AddSeconds(-10),
            now.AddMinutes(1),
            runtime,
            "node-a",
            "cluster-a",
            IsActive: true,
            authority.AuthorityRevision);

        var wrongRuntime = await store.AdoptReplicatedAsync(
            lease,
            "cluster-a",
            "node-a",
            runtime with { Revision = 8 },
            authority.AuthorityRevision);
        Assert.Equal("replicated-session-runtime-mismatch", wrongRuntime.ReasonCode);

        var wrongSource = await store.AdoptReplicatedAsync(
            lease,
            "cluster-a",
            "node-b",
            runtime,
            authority.AuthorityRevision);
        Assert.Equal("replicated-session-source-mismatch", wrongSource.ReasonCode);

        var expired = await store.AdoptReplicatedAsync(
            lease with
            {
                LastHeartbeatUtc = now.AddMinutes(-2),
                ExpiresAtUtc = now.AddSeconds(-1)
            },
            "cluster-a",
            "node-a",
            runtime,
            authority.AuthorityRevision);
        Assert.Equal("replicated-session-expired", expired.ReasonCode);

        var wrongRevision = await store.AdoptReplicatedAsync(
            lease with { AuthorityRevision = authority.AuthorityRevision + 1 },
            "cluster-a",
            "node-a",
            runtime,
            authority.AuthorityRevision);
        Assert.Equal("authority-revision-changed", wrongRevision.ReasonCode);
    }
}
