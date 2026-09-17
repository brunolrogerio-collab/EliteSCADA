using Scada.Persistence.PostgreSql;
using Scada.Security.Authorization;

namespace Scada.Drivers.Tests;

public sealed class RuntimeSessionAuthorityEnforcementTests
{
    [Fact]
    public async Task InMemoryCapacityAdmission_RejectsPendingAndStaleExpectedRevisionWithoutConsumingSeat()
    {
        var now = DateTimeOffset.Parse("2026-09-17T20:00:00Z");
        await using var store = new InMemoryRuntimeSessionLeaseStore(() => now);
        var runtime = Runtime(now);
        var capacity = new RuntimeSessionSeatCapacity(InteractiveSeats: 1, ViewOnlySeats: 0);

        var transition = await store.BeginAuthorityTransitionAsync("replace", now);
        var pending = await store.AdmitWithCapacityAsync(CapacityAdmission("user-a", "client-a", runtime, capacity, 1));
        Assert.False(pending.IsAdmitted);
        Assert.Equal(RuntimeSessionSeatReservationReasonCode.AuthorityTransitionPending, pending.ReasonCode);

        _ = await store.CommitAuthorityChangeAsync(transition.TransitionId, 1, now.AddSeconds(1), null);
        _ = await store.FenceLeasesBeforeAuthorityRevisionAsync(transition.TransitionId, 2);
        await store.CompleteAuthorityTransitionAsync(transition.TransitionId, 2);

        var stale = await store.AdmitWithCapacityAsync(CapacityAdmission("user-a", "client-a", runtime, capacity, 1));
        Assert.False(stale.IsAdmitted);
        Assert.Equal(RuntimeSessionSeatReservationReasonCode.AuthorityRevisionChanged, stale.ReasonCode);

        var current = await store.AdmitWithCapacityAsync(CapacityAdmission("user-a", "client-a", runtime, capacity, 2));
        Assert.True(current.IsAdmitted);
        Assert.Equal(2, current.Lease!.AuthorityRevision);
    }

    [Fact]
    public async Task InMemoryUsePath_FailsClosedWhilePendingAndForStaleAuthorityRevision()
    {
        var now = DateTimeOffset.Parse("2026-09-17T20:00:00Z");
        await using var store = new InMemoryRuntimeSessionLeaseStore(() => now);
        var runtime = Runtime(now);
        var lease = await store.AdmitAsync(Admission("user-a", "client-a", runtime));

        var transition = await store.BeginAuthorityTransitionAsync("remove", now);
        var pendingValidation = await store.ValidateAsync(lease.SessionId, "user-a", runtime, "client-a");
        var pendingHeartbeat = await store.HeartbeatAsync(lease.SessionId, "user-a", "client-a", runtime);
        var pendingTerminate = await store.TerminateAsync(lease.SessionId, "user-a", "client-a", runtime);
        Assert.Equal("authority-transition-pending", pendingValidation.FailureCode);
        Assert.Equal("authority-transition-pending", pendingHeartbeat.FailureCode);
        Assert.Equal("authority-transition-pending", pendingTerminate.FailureCode);

        _ = await store.CommitAuthorityChangeAsync(transition.TransitionId, 1, now.AddSeconds(1), now.AddSeconds(1));
        Assert.Equal(1, await store.FenceLeasesBeforeAuthorityRevisionAsync(transition.TransitionId, 2));
        await store.CompleteAuthorityTransitionAsync(transition.TransitionId, 2);

        var stale = await store.ValidateAsync(lease.SessionId, "user-a", runtime, "client-a");
        Assert.False(stale.IsValid);
    }

    [Fact]
    public async Task PostgreSqlTwoStores_TransitionWinsAgainstStaleExpectedRevision()
    {
        var connectionString = Environment.GetEnvironmentVariable("ELITESCADA_C25_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        await using var first = new PostgreSqlRuntimeSessionLeaseStore(connectionString);
        await using var second = new PostgreSqlRuntimeSessionLeaseStore(connectionString);
        await first.InitializeAsync();
        await second.InitializeAsync();

        var state = await first.GetAuthorityStateAsync();
        if (state.TransitionPending) return;

        var now = DateTimeOffset.UtcNow;
        var transition = await first.BeginAuthorityTransitionAsync("replace", now);
        try
        {
            var pending = await second.AdmitWithCapacityAsync(
                CapacityAdmission($"phase-a-{Guid.NewGuid():N}", "client", Runtime(now), new RuntimeSessionSeatCapacity(1, 0), state.AuthorityRevision));
            Assert.False(pending.IsAdmitted);
            Assert.Equal(RuntimeSessionSeatReservationReasonCode.AuthorityTransitionPending, pending.ReasonCode);

            var committed = await first.CommitAuthorityChangeAsync(
                transition.TransitionId,
                transition.BaseAuthorityRevision,
                now,
                null);
            _ = await first.FenceLeasesBeforeAuthorityRevisionAsync(transition.TransitionId, committed.AuthorityRevision);
            await first.CompleteAuthorityTransitionAsync(transition.TransitionId, committed.AuthorityRevision);

            var stale = await second.AdmitWithCapacityAsync(
                CapacityAdmission($"phase-a-{Guid.NewGuid():N}", "client", Runtime(now), new RuntimeSessionSeatCapacity(1, 0), state.AuthorityRevision));
            Assert.False(stale.IsAdmitted);
            Assert.Equal(RuntimeSessionSeatReservationReasonCode.AuthorityRevisionChanged, stale.ReasonCode);
        }
        finally
        {
            var current = await first.GetAuthorityStateAsync();
            if (current.TransitionPending && current.TransitionId == transition.TransitionId)
            {
                if (current.AuthorityRevision == transition.BaseAuthorityRevision)
                    _ = await first.AbortAuthorityTransitionAsync(transition.TransitionId, transition.BaseAuthorityRevision);
                else
                {
                    _ = await first.FenceLeasesBeforeAuthorityRevisionAsync(transition.TransitionId, current.AuthorityRevision);
                    await first.CompleteAuthorityTransitionAsync(transition.TransitionId, current.AuthorityRevision);
                }
            }
        }
    }

    private static RuntimeSessionLeaseCapacityAdmission CapacityAdmission(
        string subject,
        string client,
        RuntimeSessionRuntimeIdentity runtime,
        RuntimeSessionSeatCapacity capacity,
        long expectedRevision) =>
        new(Admission(subject, client, runtime), capacity, expectedRevision);

    private static RuntimeSessionLeaseAdmission Admission(
        string subject,
        string client,
        RuntimeSessionRuntimeIdentity runtime) =>
        new(subject, client, "interactive", runtime, TimeSpan.FromMinutes(1));

    private static RuntimeSessionRuntimeIdentity Runtime(DateTimeOffset activatedAt) =>
        new("engineering", "project-a", 7, activatedAt);
}
