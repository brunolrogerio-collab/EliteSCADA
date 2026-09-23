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
        var pending = await store.GetAuthorityStateAsync();
        Assert.True(pending.TransitionPending);
        Assert.Equal(transition.BaseAuthorityRevision, pending.TransitionBaseAuthorityRevision);

        var committed = await store.CommitAuthorityChangeAsync(
            transition.TransitionId,
            transition.BaseAuthorityRevision,
            now.AddSeconds(1),
            demoStartedAtUtc: null);
        Assert.Equal(2, committed.AuthorityRevision);
        Assert.True(committed.TransitionPending);
        Assert.Equal(transition.BaseAuthorityRevision, committed.TransitionBaseAuthorityRevision);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.CompleteAuthorityTransitionAsync(transition.TransitionId, 2));

        var fenced = await store.FenceLeasesBeforeAuthorityRevisionAsync(transition.TransitionId, 2);
        Assert.Equal(1, fenced);
        var stale = await store.ValidateAsync(lease.SessionId, "operator", runtime, "client-a");
        Assert.False(stale.IsValid);
        Assert.Equal(0, await store.FenceLeasesBeforeAuthorityRevisionAsync(transition.TransitionId, 2));

        await store.CompleteAuthorityTransitionAsync(transition.TransitionId, 2);
        var completed = await store.GetAuthorityStateAsync();
        Assert.Equal(2, completed.AuthorityRevision);
        Assert.False(completed.TransitionPending);
        Assert.Null(completed.TransitionBaseAuthorityRevision);
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
        Assert.Null(state.TransitionBaseAuthorityRevision);
    }

    [Fact]
    public async Task PostgreSqlMigration_InitializesAuthoritySingletonAndLeaseRevision()
    {
        var connectionString = Environment.GetEnvironmentVariable("ELITESCADA_TEST_POSTGRES") ??
            Environment.GetEnvironmentVariable("ELITESCADA_C25_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        await using var store = new PostgreSqlRuntimeSessionLeaseStore(connectionString);
        await store.InitializeAsync();

        var state = await store.GetAuthorityStateAsync();
        Assert.True(state.AuthorityRevision >= 1);
        Assert.True(state.SupportsDurableRecovery);
        Assert.Null(state.TransitionBaseAuthorityRevision);

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

        await using var transitionBaseColumn = dataSource.CreateCommand("""
            SELECT count(*)
            FROM information_schema.columns
            WHERE table_schema = 'elitescada'
              AND table_name = 'runtime_session_authority_state'
              AND column_name = 'transition_base_authority_revision';
            """);
        Assert.Equal(1L, Convert.ToInt64(await transitionBaseColumn.ExecuteScalarAsync()));

        await using var migration = dataSource.CreateCommand("""
            SELECT count(*) FROM elitescada.schema_migrations
            WHERE migration_key = '024_runtime_session_authority_transition_base_v1';
            """);
        Assert.Equal(1L, Convert.ToInt64(await migration.ExecuteScalarAsync()));
    }

    [Fact]
    public async Task PostgreSqlAuthorityTransition_FailedCommitRollsBackAndAbortPreservesBaseRevision()
    {
        var connectionString = Environment.GetEnvironmentVariable("ELITESCADA_TEST_POSTGRES") ??
            Environment.GetEnvironmentVariable("ELITESCADA_C25_POSTGRES");
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
            Assert.Equal(transition.BaseAuthorityRevision, afterFailedCommit.TransitionBaseAuthorityRevision);

            Assert.True(await store.AbortAuthorityTransitionAsync(
                transition.TransitionId,
                transition.BaseAuthorityRevision));

            var afterAbort = await store.GetAuthorityStateAsync();
            Assert.False(afterAbort.TransitionPending);
            Assert.Equal(transition.BaseAuthorityRevision, afterAbort.AuthorityRevision);
            Assert.Null(afterAbort.TransitionBaseAuthorityRevision);
        }
        finally
        {
            var current = await store.GetAuthorityStateAsync();
            if (current.TransitionPending && current.TransitionId == transition.TransitionId)
                _ = await store.AbortAuthorityTransitionAsync(transition.TransitionId, transition.BaseAuthorityRevision);
        }
    }
    [Fact]
    public async Task PostgreSqlBulkFence_IncrementsGenerationOnce_IsIdempotent_AndGuardsCompletion()
    {
        var connectionString = Environment.GetEnvironmentVariable("ELITESCADA_TEST_POSTGRES") ??
            Environment.GetEnvironmentVariable("ELITESCADA_C25_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var subject = $"phase-a-fence-generation-{Guid.NewGuid():N}";
        var client = "phase-a-fence-client";
        var now = DateTimeOffset.UtcNow;
        var runtime = new RuntimeSessionRuntimeIdentity("engineering", "project-a", 7, now);

        await using var store = new PostgreSqlRuntimeSessionLeaseStore(connectionString);
        await store.InitializeAsync();
        var initial = await store.GetAuthorityStateAsync();
        if (initial.TransitionPending) return;

        RuntimeAuthorityTransition? transition = null;
        try
        {
            var lease = await store.AdmitAsync(new RuntimeSessionLeaseAdmission(
                subject,
                client,
                "interactive",
                runtime,
                TimeSpan.FromMinutes(5)));
            Assert.Equal(initial.AuthorityRevision, lease.AuthorityRevision);

            var beforeFence = await ReadPersistedLeaseStateAsync(connectionString, lease.SessionId);
            Assert.True(beforeFence.IsActive);
            Assert.Equal(lease.Generation, beforeFence.Generation);
            Assert.Equal(initial.AuthorityRevision, beforeFence.AuthorityRevision);

            transition = await store.BeginAuthorityTransitionAsync("replace", now);
            var committed = await store.CommitAuthorityChangeAsync(
                transition.TransitionId,
                transition.BaseAuthorityRevision,
                now.AddSeconds(1),
                demoStartedAtUtc: null);
            var nextRevision = committed.AuthorityRevision;
            Assert.Equal(initial.AuthorityRevision + 1, nextRevision);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                store.CompleteAuthorityTransitionAsync(transition.TransitionId, nextRevision));

            var changed = await store.FenceLeasesBeforeAuthorityRevisionAsync(
                transition.TransitionId,
                nextRevision);
            Assert.True(changed >= 1);

            var afterFence = await ReadPersistedLeaseStateAsync(connectionString, lease.SessionId);
            Assert.False(afterFence.IsActive);
            Assert.Equal(beforeFence.Generation + 1, afterFence.Generation);
            Assert.Equal(initial.AuthorityRevision, afterFence.AuthorityRevision);

            var changedAgain = await store.FenceLeasesBeforeAuthorityRevisionAsync(
                transition.TransitionId,
                nextRevision);
            Assert.Equal(0, changedAgain);

            var afterSecondFence = await ReadPersistedLeaseStateAsync(connectionString, lease.SessionId);
            Assert.False(afterSecondFence.IsActive);
            Assert.Equal(afterFence.Generation, afterSecondFence.Generation);
            Assert.Equal(afterFence.AuthorityRevision, afterSecondFence.AuthorityRevision);

            await store.CompleteAuthorityTransitionAsync(transition.TransitionId, nextRevision);
            Assert.False((await store.GetAuthorityStateAsync()).TransitionPending);
        }
        finally
        {
            var current = await store.GetAuthorityStateAsync();
            if (transition is not null &&
                current.TransitionPending &&
                current.TransitionId == transition.TransitionId)
            {
                if (current.AuthorityRevision == transition.BaseAuthorityRevision)
                {
                    _ = await store.AbortAuthorityTransitionAsync(
                        transition.TransitionId,
                        transition.BaseAuthorityRevision);
                }
                else
                {
                    _ = await store.FenceLeasesBeforeAuthorityRevisionAsync(
                        transition.TransitionId,
                        current.AuthorityRevision);
                    await store.CompleteAuthorityTransitionAsync(
                        transition.TransitionId,
                        current.AuthorityRevision);
                }
            }

            await DeleteLeaseSubjectAsync(connectionString, subject);
        }
    }

    [Fact]
    public async Task PostgreSqlSecondTransition_PersistsCurrentBaseAndFencesRevisionTwoLease()
    {
        var connectionString = Environment.GetEnvironmentVariable("ELITESCADA_TEST_POSTGRES") ??
            Environment.GetEnvironmentVariable("ELITESCADA_C25_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        await using var store = new PostgreSqlRuntimeSessionLeaseStore(connectionString);
        await store.InitializeAsync();
        var initial = await store.GetAuthorityStateAsync();
        if (initial.TransitionPending) return;

        var now = DateTimeOffset.UtcNow;
        var subject = $"phase-c-second-transition-{Guid.NewGuid():N}";
        var runtime = new RuntimeSessionRuntimeIdentity("engineering", "project-a", 7, now);
        RuntimeAuthorityTransition? active = null;
        try
        {
            active = await store.BeginAuthorityTransitionAsync("install", now);
            var first = await store.CommitAuthorityChangeAsync(
                active.TransitionId, active.BaseAuthorityRevision, now, null);
            await store.FenceLeasesBeforeAuthorityRevisionAsync(active.TransitionId, first.AuthorityRevision);
            await store.CompleteAuthorityTransitionAsync(active.TransitionId, first.AuthorityRevision);

            var lease = await store.AdmitAsync(new RuntimeSessionLeaseAdmission(
                subject, "client-a", "interactive", runtime, TimeSpan.FromMinutes(5)));
            Assert.Equal(first.AuthorityRevision, lease.AuthorityRevision);

            active = await store.BeginAuthorityTransitionAsync("replace", now.AddSeconds(1));
            var pending = await store.GetAuthorityStateAsync();
            Assert.Equal(first.AuthorityRevision, pending.AuthorityRevision);
            Assert.Equal(first.AuthorityRevision, pending.TransitionBaseAuthorityRevision);
            Assert.Equal(first.AuthorityChangedAtUtc, pending.AuthorityChangedAtUtc); // Prior timestamp is not proof of commit 2.
            Assert.False((await store.ValidateAsync(
                lease.SessionId, subject, runtime, "client-a")).IsValid);

            var second = await store.CommitAuthorityChangeAsync(
                active.TransitionId, active.BaseAuthorityRevision, now.AddSeconds(2), null);
            Assert.Equal(first.AuthorityRevision + 1, second.AuthorityRevision);
            await store.FenceLeasesBeforeAuthorityRevisionAsync(active.TransitionId, second.AuthorityRevision);
            await store.CompleteAuthorityTransitionAsync(active.TransitionId, second.AuthorityRevision);
            Assert.False((await store.ValidateAsync(
                lease.SessionId, subject, runtime, "client-a")).IsValid);
        }
        finally
        {
            var current = await store.GetAuthorityStateAsync();
            if (active is not null && current.TransitionPending && current.TransitionId == active.TransitionId)
            {
                if (current.AuthorityRevision == active.BaseAuthorityRevision)
                    _ = await store.AbortAuthorityTransitionAsync(active.TransitionId, active.BaseAuthorityRevision);
                else
                {
                    _ = await store.FenceLeasesBeforeAuthorityRevisionAsync(active.TransitionId, current.AuthorityRevision);
                    await store.CompleteAuthorityTransitionAsync(active.TransitionId, current.AuthorityRevision);
                }
            }
            await DeleteLeaseSubjectAsync(connectionString, subject);
        }
    }

    private static async Task<(long Generation, bool IsActive, long AuthorityRevision)> ReadPersistedLeaseStateAsync(
        string connectionString,
        Guid sessionId)
    {
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var command = dataSource.CreateCommand("""
            SELECT generation, is_active, authority_revision
            FROM elitescada.runtime_session_leases
            WHERE session_id = @session_id;
            """);
        command.Parameters.AddWithValue("session_id", sessionId);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return (reader.GetInt64(0), reader.GetBoolean(1), reader.GetInt64(2));
    }

    private static async Task DeleteLeaseSubjectAsync(string connectionString, string subject)
    {
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var command = dataSource.CreateCommand(
            "DELETE FROM elitescada.runtime_session_leases WHERE subject_id = @subject_id;");
        command.Parameters.AddWithValue("subject_id", subject);
        await command.ExecuteNonQueryAsync();
    }


}
