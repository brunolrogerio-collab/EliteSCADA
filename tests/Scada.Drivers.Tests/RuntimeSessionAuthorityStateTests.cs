using Npgsql;
using Scada.Persistence.PostgreSql;
using Scada.Security.Authorization;

namespace Scada.Drivers.Tests;

[Collection("RuntimeSessionPostgreSql")]
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

        await using var invalidRows = dataSource.CreateCommand("""
            SELECT count(*)
            FROM elitescada.runtime_session_leases
            WHERE authority_revision IS NULL OR authority_revision < 1;
            """);
        Assert.Equal(0L, Convert.ToInt64(await invalidRows.ExecuteScalarAsync()));

        await using var columnDefault = dataSource.CreateCommand("""
            SELECT column_default
            FROM information_schema.columns
            WHERE table_schema = 'elitescada'
              AND table_name = 'runtime_session_leases'
              AND column_name = 'authority_revision';
            """);
        Assert.Contains("1", Convert.ToString(await columnDefault.ExecuteScalarAsync()) ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PostgreSqlAuthorityTransition_FailedCommitRollsBackAndAbortPreservesBaseRevision()
    {
        var connectionString = Environment.GetEnvironmentVariable("ELITESCADA_C25_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        await using var store = new PostgreSqlRuntimeSessionLeaseStore(connectionString);
        await store.InitializeAsync();
        var before = await store.GetAuthorityStateAsync();
        if (before.TransitionPending) return;

        var transition = await store.BeginAuthorityTransitionAsync("replace", DateTimeOffset.UtcNow);
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                store.CommitAuthorityChangeAsync(
                    Guid.NewGuid(),
                    transition.BaseAuthorityRevision,
                    DateTimeOffset.UtcNow,
                    null));

            var afterFailedCommit = await store.GetAuthorityStateAsync();
            Assert.True(afterFailedCommit.TransitionPending);
            Assert.Equal(transition.TransitionId, afterFailedCommit.TransitionId);
            Assert.Equal(transition.BaseAuthorityRevision, afterFailedCommit.AuthorityRevision);

            Assert.True(await store.AbortAuthorityTransitionAsync(
                transition.TransitionId,
                transition.BaseAuthorityRevision));

            var afterAbort = await store.GetAuthorityStateAsync();
            Assert.False(afterAbort.TransitionPending);
            Assert.Equal(transition.BaseAuthorityRevision, afterAbort.AuthorityRevision);
        }
        finally
        {
            var current = await store.GetAuthorityStateAsync();
            if (current.TransitionPending && current.TransitionId == transition.TransitionId)
                _ = await store.AbortAuthorityTransitionAsync(transition.TransitionId, transition.BaseAuthorityRevision);
        }
    }
}
