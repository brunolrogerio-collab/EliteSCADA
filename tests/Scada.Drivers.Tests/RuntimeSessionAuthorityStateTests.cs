using Npgsql;
using Scada.Persistence.PostgreSql;
using Scada.Security.Authorization;

namespace Scada.Drivers.Tests;

public sealed class RuntimeSessionAuthorityStateTests
{
    [Fact]
    public async Task InMemoryAuthorityTransition_CommitsFencesAndRequiresCoherentCompletion()
    {
        var now = DateTimeOffset.Parse("2026-09-17T19:00:00Z");
        await using var store = new InMemoryRuntimeSessionLeaseStore(() => now);
        var runtime = new RuntimeSessionRuntimeIdentity("engineering", "project-a", 7, now);
        var lease = await store.AdmitAsync(new RuntimeSessionLeaseAdmission(
            "operator", "client-a", "interactive", runtime, TimeSpan.FromMinutes(1)));

        Assert.Equal(1, lease.AuthorityRevision);
        var initial = await store.GetAuthorityStateAsync();
        Assert.Equal(1, initial.AuthorityRevision);
        Assert.False(initial.TransitionPending);
        Assert.False(initial.SupportsDurableRecovery);

        var transition = await store.BeginAuthorityTransitionAsync("replace", now);
        Assert.Equal(1, transition.BaseAuthorityRevision);
        Assert.True((await store.GetAuthorityStateAsync()).TransitionPending);

        var committed = await store.CommitAuthorityChangeAsync(
            transition.TransitionId,
            transition.BaseAuthorityRevision,
            now.AddSeconds(1),
            demoStartedAtUtc: null);
        Assert.Equal(2, committed.AuthorityRevision);
        Assert.True(committed.TransitionPending);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.CompleteAuthorityTransitionAsync(transition.TransitionId, 2));

        var fenced = await store.FenceLeasesBeforeAuthorityRevisionAsync(transition.TransitionId, 2);
        Assert.Equal(1, fenced);
        var stale = await store.ValidateAsync(lease.SessionId, "operator", runtime, "client-a");
        Assert.False(stale.IsValid);

        await store.CompleteAuthorityTransitionAsync(transition.TransitionId, 2);
        var completed = await store.GetAuthorityStateAsync();
        Assert.Equal(2, completed.AuthorityRevision);
        Assert.False(completed.TransitionPending);
    }

    [Fact]
    public async Task InMemoryAuthorityTransition_AbortPreservesRevision()
    {
        await using var store = new InMemoryRuntimeSessionLeaseStore();
        var transition = await store.BeginAuthorityTransitionAsync("replace", DateTimeOffset.UtcNow);
        Assert.True(await store.AbortAuthorityTransitionAsync(
            transition.TransitionId,
            transition.BaseAuthorityRevision));
        var state = await store.GetAuthorityStateAsync();
        Assert.Equal(1, state.AuthorityRevision);
        Assert.False(state.TransitionPending);
    }

    [Fact]
    public async Task PostgreSqlMigration_InitializesAuthoritySingletonAndLeaseRevision()
    {
        var connectionString = Environment.GetEnvironmentVariable("ELITESCADA_C25_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        await using var store = new PostgreSqlRuntimeSessionLeaseStore(connectionString);
        await store.InitializeAsync();

        var state = await store.GetAuthorityStateAsync();
        Assert.True(state.AuthorityRevision >= 1);
        Assert.True(state.SupportsDurableRecovery);

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var command = dataSource.CreateCommand("""
            SELECT count(*)
            FROM information_schema.columns
            WHERE table_schema = 'elitescada'
              AND table_name = 'runtime_session_leases'
              AND column_name = 'authority_revision';
            """);
        Assert.Equal(1L, Convert.ToInt64(await command.ExecuteScalarAsync()));
    }
}
