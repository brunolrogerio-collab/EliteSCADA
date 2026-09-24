using Npgsql;
using NpgsqlTypes;
using Scada.Security.Authentication;

namespace Scada.Persistence.PostgreSql;

/// <summary>PostgreSQL-backed, cross-instance Authority lifecycle and session epoch state.</summary>
public sealed class PostgreSqlAuthorityLifecycleStore : IAuthorityLifecycleStore, IAsyncDisposable
{
    private const long IdentityMutationAdvisoryLock = 4993446713136202562;
    private const long PolicyMutationAdvisoryLock = 4993446713136202564;
    private const long AuthorityOperationAdvisoryLock = 4993446713136202565;
    private const string StateKey = "authority-lifecycle-v1";
    private readonly NpgsqlDataSource _dataSource;

    public PostgreSqlAuthorityLifecycleStore(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("PostgreSQL connection string is required.", nameof(connectionString));
        _dataSource = NpgsqlDataSource.Create(connectionString);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            CREATE SCHEMA IF NOT EXISTS elitescada;
            CREATE TABLE IF NOT EXISTS elitescada.schema_migrations (
                migration_key text PRIMARY KEY,
                applied_at_utc timestamptz NOT NULL DEFAULT clock_timestamp());
            CREATE TABLE IF NOT EXISTS elitescada.authority_lifecycle_state (
                state_key text PRIMARY KEY,
                state text NOT NULL CONSTRAINT authority_lifecycle_state_allowed CHECK (state IN ('InitialInstallation', 'AuthorityPresent', 'DetachInProgress', 'DeliberatelyDetached', 'AttachInProgress', 'Invalid')),
                epoch bigint NOT NULL CHECK (epoch > 0),
                updated_at_utc timestamptz NOT NULL DEFAULT clock_timestamp());
            WITH migration AS (
                INSERT INTO elitescada.schema_migrations (migration_key)
                VALUES ('020_authority_lifecycle_epoch')
                ON CONFLICT (migration_key) DO NOTHING
                RETURNING migration_key)
            INSERT INTO elitescada.authority_lifecycle_state (state_key, state, epoch)
            SELECT 'authority-lifecycle-v1', 'InitialInstallation', 1
            WHERE EXISTS (SELECT 1 FROM migration)
            ON CONFLICT (state_key) DO NOTHING;
            DO $migration$
            BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM elitescada.schema_migrations
                    WHERE migration_key = '021_authority_lifecycle_attach') THEN
                    ALTER TABLE elitescada.authority_lifecycle_state
                        DROP CONSTRAINT IF EXISTS authority_lifecycle_state_state_check;
                    ALTER TABLE elitescada.authority_lifecycle_state
                        DROP CONSTRAINT IF EXISTS authority_lifecycle_state_allowed;
                    ALTER TABLE elitescada.authority_lifecycle_state
                        ADD CONSTRAINT authority_lifecycle_state_allowed
                        CHECK (state IN ('InitialInstallation', 'AuthorityPresent', 'DetachInProgress', 'DeliberatelyDetached', 'AttachInProgress', 'Invalid'));
                    INSERT INTO elitescada.schema_migrations (migration_key)
                    VALUES ('021_authority_lifecycle_attach');
                END IF;
            END
            $migration$;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await PostgreSqlSharedSchemaInitialization.AcquireLockAsync(connection, transaction, cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<AuthorityLifecycleSnapshot> GetAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        return await LoadAsync(connection, null, cancellationToken);
    }

    public async ValueTask<IAsyncDisposable> AcquireOperationLeaseAsync(CancellationToken cancellationToken = default)
    {
        var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        NpgsqlTransaction? transaction = null;
        try
        {
            transaction = await connection.BeginTransactionAsync(cancellationToken);
            await using var command = new NpgsqlCommand(
                "SELECT pg_advisory_xact_lock(@operation_key);",
                connection,
                transaction);
            command.Parameters.AddWithValue("operation_key", NpgsqlDbType.Bigint, AuthorityOperationAdvisoryLock);
            await command.ExecuteNonQueryAsync(cancellationToken);
            return new OperationLease(connection, transaction);
        }
        catch
        {
            if (transaction is not null) await transaction.DisposeAsync();
            await connection.DisposeAsync();
            throw;
        }
    }

    public Task<AuthorityLifecycleSnapshot> MarkAuthorityPresentAsync(CancellationToken cancellationToken = default) =>
        TransitionAsync(
            AuthorityLifecycleState.InitialInstallation,
            AuthorityLifecycleState.AuthorityPresent,
            advanceEpoch: false,
            idempotentState: AuthorityLifecycleState.AuthorityPresent,
            cancellationToken);

    public async Task<AuthorityLifecycleSnapshot> MarkInvalidAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await AcquireAuthorityMutationLocksAsync(connection, transaction, cancellationToken);
        var current = await LoadAsync(connection, transaction, cancellationToken);
        if (current.State == AuthorityLifecycleState.Invalid)
        {
            await transaction.CommitAsync(cancellationToken);
            return current;
        }

        var invalid = current with { State = AuthorityLifecycleState.Invalid };
        await SaveAsync(connection, transaction, invalid, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return invalid;
    }

    public Task<AuthorityLifecycleSnapshot> BeginDetachAsync(CancellationToken cancellationToken = default) =>
        TransitionAsync(
            AuthorityLifecycleState.AuthorityPresent,
            AuthorityLifecycleState.DetachInProgress,
            advanceEpoch: false,
            idempotentState: AuthorityLifecycleState.DetachInProgress,
            cancellationToken);

    public Task<AuthorityLifecycleSnapshot> CompleteDetachAsync(CancellationToken cancellationToken = default) =>
        TransitionAsync(
            AuthorityLifecycleState.DetachInProgress,
            AuthorityLifecycleState.DeliberatelyDetached,
            advanceEpoch: true,
            idempotentState: AuthorityLifecycleState.DeliberatelyDetached,
            cancellationToken);

    public Task<AuthorityLifecycleSnapshot> BeginAttachAsync(CancellationToken cancellationToken = default) =>
        TransitionAsync(
            AuthorityLifecycleState.DeliberatelyDetached,
            AuthorityLifecycleState.AttachInProgress,
            advanceEpoch: false,
            idempotentState: AuthorityLifecycleState.AttachInProgress,
            cancellationToken);

    public Task<AuthorityLifecycleSnapshot> CompleteAttachAsync(CancellationToken cancellationToken = default) =>
        TransitionAsync(
            AuthorityLifecycleState.AttachInProgress,
            AuthorityLifecycleState.AuthorityPresent,
            advanceEpoch: true,
            idempotentState: AuthorityLifecycleState.AuthorityPresent,
            cancellationToken);

    public Task<AuthorityLifecycleSnapshot> AbortAttachAsync(CancellationToken cancellationToken = default) =>
        TransitionAsync(
            AuthorityLifecycleState.AttachInProgress,
            AuthorityLifecycleState.DeliberatelyDetached,
            advanceEpoch: true,
            idempotentState: AuthorityLifecycleState.DeliberatelyDetached,
            cancellationToken);

    private async Task<AuthorityLifecycleSnapshot> TransitionAsync(
        AuthorityLifecycleState expected,
        AuthorityLifecycleState next,
        bool advanceEpoch,
        AuthorityLifecycleState idempotentState,
        CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await AcquireAuthorityMutationLocksAsync(connection, transaction, cancellationToken);
        var current = await LoadAsync(connection, transaction, cancellationToken);
        if (current.State == idempotentState)
        {
            await transaction.CommitAsync(cancellationToken);
            return current;
        }
        if (current.State != expected)
            throw new InvalidOperationException($"Authority lifecycle cannot transition from '{current.State}' to '{next}'.");

        var updated = new AuthorityLifecycleSnapshot(next, advanceEpoch ? checked(current.Epoch + 1) : current.Epoch);
        await SaveAsync(connection, transaction, updated, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return updated;
    }

    private static async Task AcquireAuthorityMutationLocksAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT pg_advisory_xact_lock(@identity_key); SELECT pg_advisory_xact_lock(@policy_key);",
            connection,
            transaction);
        command.Parameters.AddWithValue("identity_key", NpgsqlDbType.Bigint, IdentityMutationAdvisoryLock);
        command.Parameters.AddWithValue("policy_key", NpgsqlDbType.Bigint, PolicyMutationAdvisoryLock);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task SaveAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, AuthorityLifecycleSnapshot snapshot, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "UPDATE elitescada.authority_lifecycle_state SET state = @state, epoch = @epoch, updated_at_utc = clock_timestamp() WHERE state_key = @key;",
            connection,
            transaction);
        command.Parameters.AddWithValue("state", snapshot.State.ToString());
        command.Parameters.AddWithValue("epoch", NpgsqlDbType.Bigint, snapshot.Epoch);
        command.Parameters.AddWithValue("key", StateKey);
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new InvalidOperationException("Authority lifecycle state is missing during transition.");
    }

    private static async Task<AuthorityLifecycleSnapshot> LoadAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT state, epoch FROM elitescada.authority_lifecycle_state WHERE state_key = @key;",
            connection,
            transaction);
        command.Parameters.AddWithValue("key", StateKey);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new InvalidOperationException("Authority lifecycle state is missing; authorization is fail-closed.");

        var stateText = reader.GetString(0);
        var epoch = reader.GetInt64(1);
        if (!Enum.TryParse<AuthorityLifecycleState>(stateText, ignoreCase: false, out var state) || epoch <= 0)
            throw new InvalidOperationException("Authority lifecycle state is incompatible; authorization is fail-closed.");
        return new AuthorityLifecycleSnapshot(state, epoch);
    }

    private sealed class OperationLease(NpgsqlConnection connection, NpgsqlTransaction transaction) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                await transaction.CommitAsync(CancellationToken.None);
            }
            finally
            {
                await transaction.DisposeAsync();
                await connection.DisposeAsync();
            }
        }
    }

    public ValueTask DisposeAsync() => _dataSource.DisposeAsync();
}
