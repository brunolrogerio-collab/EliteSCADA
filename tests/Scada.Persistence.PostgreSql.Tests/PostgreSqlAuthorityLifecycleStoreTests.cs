using Npgsql;
using Scada.Persistence.PostgreSql;
using Scada.Security.Authentication;

namespace Scada.Persistence.PostgreSql.Tests;

public sealed class PostgreSqlAuthorityLifecycleStoreTests
{
    // Must match the database-wide shared-schema DDL lock used by production initializers.
    private const string SharedSchemaLockSql = "SELECT pg_advisory_xact_lock(4993446713136202561);";
    private static string? ConnectionString => Environment.GetEnvironmentVariable("ELITESCADA_TEST_POSTGRES");

    [Fact]
    public async Task PersistsDetachIntentAcrossRestart_AndFencesOldEpoch()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString)) return;
        await ResetAsync();

        long initialEpoch;
        await using (var first = new PostgreSqlAuthorityLifecycleStore(ConnectionString))
        {
            await first.InitializeAsync();
            var initial = await first.GetAsync();
            initialEpoch = initial.Epoch;
            Assert.Equal(AuthorityLifecycleState.InitialInstallation, initial.State);
            await first.MarkAuthorityPresentAsync();
            await first.BeginDetachAsync();
        }

        await using (var restarted = new PostgreSqlAuthorityLifecycleStore(ConnectionString))
        {
            await restarted.InitializeAsync();
            var inProgress = await restarted.GetAsync();
            Assert.Equal(AuthorityLifecycleState.DetachInProgress, inProgress.State);
            Assert.False(AuthorityLifecycleSessionFence.IsCurrent(inProgress, initialEpoch));

            var detached = await restarted.CompleteDetachAsync();
            Assert.Equal(AuthorityLifecycleState.DeliberatelyDetached, detached.State);
            Assert.Equal(initialEpoch + 1, detached.Epoch);
            Assert.False(AuthorityLifecycleSessionFence.IsCurrent(detached, initialEpoch));
        }

        await ResetAsync();
    }

    [Fact]
    public async Task CrossInstanceDetachIntent_IsSerializedAndIdempotent()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString)) return;
        await ResetAsync();

        await using var first = new PostgreSqlAuthorityLifecycleStore(ConnectionString);
        await using var second = new PostgreSqlAuthorityLifecycleStore(ConnectionString);
        await first.InitializeAsync();
        await second.InitializeAsync();
        await first.MarkAuthorityPresentAsync();

        var transitions = await Task.WhenAll(first.BeginDetachAsync(), second.BeginDetachAsync());
        Assert.All(transitions, state => Assert.Equal(AuthorityLifecycleState.DetachInProgress, state.State));
        Assert.Equal(transitions[0].Epoch, transitions[1].Epoch);
        Assert.Equal(AuthorityLifecycleState.DetachInProgress, (await first.GetAsync()).State);

        await ResetAsync();
    }

    [Fact]
    public async Task OperationLease_SerializesAcrossInstances()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString)) return;

        await using var first = new PostgreSqlAuthorityLifecycleStore(ConnectionString);
        await using var second = new PostgreSqlAuthorityLifecycleStore(ConnectionString);
        await first.InitializeAsync();
        await second.InitializeAsync();

        Task<IAsyncDisposable> waiting;
        await using (var firstLease = await first.AcquireOperationLeaseAsync())
        {
            waiting = second.AcquireOperationLeaseAsync().AsTask();
            await Task.Delay(TimeSpan.FromMilliseconds(100));
            Assert.False(waiting.IsCompleted);
        }

        await using var secondLease = await waiting.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task PersistsAttachIntentAcrossRestart_AndAdvancesEpochOnAbort()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString)) return;
        await ResetAsync();

        long detachedEpoch;
        await using (var first = new PostgreSqlAuthorityLifecycleStore(ConnectionString))
        {
            await first.InitializeAsync();
            await first.MarkAuthorityPresentAsync();
            await first.BeginDetachAsync();
            detachedEpoch = (await first.CompleteDetachAsync()).Epoch;
            var attaching = await first.BeginAttachAsync();
            Assert.Equal(AuthorityLifecycleState.AttachInProgress, attaching.State);
            Assert.False(AuthorityLifecycleSessionFence.IsCurrent(attaching, detachedEpoch));
        }

        await using (var restarted = new PostgreSqlAuthorityLifecycleStore(ConnectionString))
        {
            await restarted.InitializeAsync();
            Assert.Equal(AuthorityLifecycleState.AttachInProgress, (await restarted.GetAsync()).State);

            var aborted = await restarted.AbortAttachAsync();
            Assert.Equal(AuthorityLifecycleState.DeliberatelyDetached, aborted.State);
            Assert.Equal(detachedEpoch + 1, aborted.Epoch);
        }

        await ResetAsync();
    }

    [Fact]
    public async Task Initialize_CreatesLifecycleState_WhenAuditMigration007AlreadyExists()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString)) return;

        await using (var connection = new NpgsqlConnection(ConnectionString))
        {
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            await AcquireSharedSchemaLockAsync(connection, transaction);
            await using var command = new NpgsqlCommand(
                """
                CREATE SCHEMA IF NOT EXISTS elitescada;
                CREATE TABLE IF NOT EXISTS elitescada.schema_migrations (
                    migration_key text PRIMARY KEY,
                    applied_at_utc timestamptz NOT NULL DEFAULT clock_timestamp());
                CREATE TABLE IF NOT EXISTS elitescada.authority_lifecycle_state (
                    state_key text PRIMARY KEY,
                    state text NOT NULL CHECK (state IN ('InitialInstallation', 'AuthorityPresent', 'DetachInProgress', 'DeliberatelyDetached', 'Invalid')),
                    epoch bigint NOT NULL CHECK (epoch > 0),
                    updated_at_utc timestamptz NOT NULL DEFAULT clock_timestamp());
                DELETE FROM elitescada.authority_lifecycle_state WHERE state_key = 'authority-lifecycle-v1';
                DELETE FROM elitescada.schema_migrations WHERE migration_key = '020_authority_lifecycle_epoch';
                INSERT INTO elitescada.schema_migrations (migration_key)
                VALUES ('007_audit_retention_query_foundation')
                ON CONFLICT (migration_key) DO NOTHING;
                """,
                connection,
                transaction);
            await command.ExecuteNonQueryAsync();
            await transaction.CommitAsync();
        }

        await using var store = new PostgreSqlAuthorityLifecycleStore(ConnectionString);
        await store.InitializeAsync();
        var state = await store.GetAsync();

        Assert.Equal(AuthorityLifecycleState.InitialInstallation, state.State);
        Assert.Equal(1, state.Epoch);
        await ResetAsync();
    }

    private static async Task ResetAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await AcquireSharedSchemaLockAsync(connection, transaction);
        await using var command = new NpgsqlCommand(
            """
            CREATE SCHEMA IF NOT EXISTS elitescada;
            CREATE TABLE IF NOT EXISTS elitescada.authority_lifecycle_state (
                state_key text PRIMARY KEY,
                state text NOT NULL,
                epoch bigint NOT NULL,
                updated_at_utc timestamptz NOT NULL DEFAULT clock_timestamp());
            INSERT INTO elitescada.authority_lifecycle_state (state_key, state, epoch)
            VALUES ('authority-lifecycle-v1', 'InitialInstallation', 1)
            ON CONFLICT (state_key) DO UPDATE
                SET state = EXCLUDED.state, epoch = EXCLUDED.epoch, updated_at_utc = clock_timestamp();
            """,
            connection,
            transaction);
        await command.ExecuteNonQueryAsync();
        await transaction.CommitAsync();
    }

    private static async Task AcquireSharedSchemaLockAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction)
    {
        await using var command = new NpgsqlCommand(SharedSchemaLockSql, connection, transaction);
        await command.ExecuteNonQueryAsync();
    }
}
