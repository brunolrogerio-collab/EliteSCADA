using Npgsql;
using NpgsqlTypes;
using Scada.Security.Authorization;

namespace Scada.Persistence.PostgreSql;

/// <summary>
/// Durable, cross-instance store for logical Runtime session leases. It deliberately stores no
/// Authority decision, entitlement, credential, transport socket, or Engineering package state.
/// </summary>
public sealed class PostgreSqlRuntimeSessionLeaseStore : IRuntimeSessionLeaseStore
{
    private const long LeaseMutationAdvisoryLock = 4993446713136202566;
    private readonly NpgsqlDataSource _dataSource;

    public PostgreSqlRuntimeSessionLeaseStore(string connectionString)
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
            CREATE TABLE IF NOT EXISTS elitescada.runtime_session_leases (
                session_id uuid PRIMARY KEY,
                subject_id text NOT NULL,
                client_instance_id varchar(128) NOT NULL,
                requested_connection_class text NOT NULL
                    CONSTRAINT runtime_session_leases_connection_class_allowed
                    CHECK (requested_connection_class IN ('viewer', 'interactive')),
                generation bigint NOT NULL CHECK (generation > 0),
                issued_at_utc timestamptz NOT NULL,
                last_heartbeat_utc timestamptz NOT NULL,
                expires_at_utc timestamptz NOT NULL,
                lease_duration interval NOT NULL CHECK (lease_duration > interval '0'),
                runtime_mode text NOT NULL,
                runtime_project_key text NULL,
                runtime_revision bigint NULL,
                runtime_activated_at_utc timestamptz NULL,
                server_node text NULL,
                cluster_id text NULL,
                is_active boolean NOT NULL DEFAULT true,
                authority_revision bigint NOT NULL DEFAULT 1 CHECK (authority_revision > 0),
                CHECK (expires_at_utc >= last_heartbeat_utc));
            CREATE TABLE IF NOT EXISTS elitescada.runtime_session_lease_tombstones (
                cluster_id text NOT NULL,
                subject_id text NOT NULL,
                client_instance_id varchar(128) NOT NULL,
                session_id uuid NOT NULL,
                generation bigint NOT NULL CHECK (generation > 0),
                authority_revision bigint NOT NULL CHECK (authority_revision > 0),
                issued_at_utc timestamptz NOT NULL,
                source_node text NOT NULL,
                recorded_at_utc timestamptz NOT NULL,
                reason_code text NOT NULL,
                PRIMARY KEY (cluster_id, subject_id, client_instance_id));
            CREATE INDEX IF NOT EXISTS ix_runtime_session_lease_tombstones_session
                ON elitescada.runtime_session_lease_tombstones (session_id);
            CREATE TABLE IF NOT EXISTS elitescada.runtime_session_authority_state (
                singleton_id smallint PRIMARY KEY CHECK (singleton_id = 1),
                authority_revision bigint NOT NULL CHECK (authority_revision > 0),
                transition_pending boolean NOT NULL DEFAULT false,
                transition_id uuid NULL,
                transition_kind text NULL,
                transition_started_at_utc timestamptz NULL,
                transition_base_authority_revision bigint NULL CHECK (transition_base_authority_revision > 0),
                authority_changed_at_utc timestamptz NULL,
                demo_started_at_utc timestamptz NULL,
                CHECK (
                    (transition_pending AND transition_id IS NOT NULL AND transition_kind IS NOT NULL AND transition_started_at_utc IS NOT NULL)
                    OR
                    (NOT transition_pending AND transition_id IS NULL AND transition_kind IS NULL AND transition_started_at_utc IS NULL)));
            INSERT INTO elitescada.runtime_session_authority_state (singleton_id, authority_revision, transition_pending)
            VALUES (1, 1, false)
            ON CONFLICT (singleton_id) DO NOTHING;
            ALTER TABLE elitescada.runtime_session_leases
                ADD COLUMN IF NOT EXISTS authority_revision bigint NOT NULL DEFAULT 1 CHECK (authority_revision > 0);
            ALTER TABLE elitescada.runtime_session_authority_state
                ADD COLUMN IF NOT EXISTS transition_base_authority_revision bigint NULL;
            DO $migration$
            BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM pg_constraint
                    WHERE conname = 'runtime_authority_transition_base_coherent'
                      AND conrelid = 'elitescada.runtime_session_authority_state'::regclass)
                THEN
                    ALTER TABLE elitescada.runtime_session_authority_state
                        ADD CONSTRAINT runtime_authority_transition_base_coherent CHECK (
                            (transition_pending AND transition_base_authority_revision IS NOT NULL AND transition_base_authority_revision > 0)
                            OR (NOT transition_pending AND transition_base_authority_revision IS NULL)) NOT VALID;
                END IF;
            END
            $migration$;
            CREATE UNIQUE INDEX IF NOT EXISTS ux_runtime_session_leases_active_logical_identity
                ON elitescada.runtime_session_leases (subject_id, client_instance_id)
                WHERE is_active;
            CREATE INDEX IF NOT EXISTS ix_runtime_session_leases_active_expiry
                ON elitescada.runtime_session_leases (expires_at_utc)
                WHERE is_active;
            CREATE INDEX IF NOT EXISTS ix_runtime_session_leases_active_authority_revision
                ON elitescada.runtime_session_leases (authority_revision)
                WHERE is_active;
            INSERT INTO elitescada.schema_migrations (migration_key)
            VALUES ('022_runtime_session_lease_v1')
            ON CONFLICT (migration_key) DO NOTHING;
            INSERT INTO elitescada.schema_migrations (migration_key)
            VALUES ('023_runtime_session_authority_fencing_v1')
            ON CONFLICT (migration_key) DO NOTHING;
            INSERT INTO elitescada.schema_migrations (migration_key)
            VALUES ('024_runtime_session_authority_transition_base_v1')
            ON CONFLICT (migration_key) DO NOTHING;
            INSERT INTO elitescada.schema_migrations (migration_key)
            VALUES ('025_runtime_session_replication_adoption_v1')
            ON CONFLICT (migration_key) DO NOTHING;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await PostgreSqlSharedSchemaInitialization.AcquireLockAsync(connection, transaction, cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<RuntimeAuthorityState> GetAuthorityStateAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await AcquireLeaseMutationLockAsync(connection, transaction, cancellationToken);
            var state = await LoadAuthorityStateForUpdateAsync(connection, transaction, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return state;
        }
        catch
        {
            await RollbackQuietlyAsync(transaction);
            throw;
        }
    }

    public async Task<RuntimeDemoSessionAnchorResult> EstablishDemoSessionAnchorAsync(
        DateTimeOffset requestedStartedAtUtc,
        DateTimeOffset? expectedExistingStartedAtUtc,
        CancellationToken cancellationToken = default)
    {
        requestedStartedAtUtc = RuntimeDemoSessionAnchor.Normalize(requestedStartedAtUtc);
        expectedExistingStartedAtUtc = expectedExistingStartedAtUtc is { } expectedAnchor
            ? RuntimeDemoSessionAnchor.Normalize(expectedAnchor)
            : null;
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await AcquireLeaseMutationLockAsync(connection, transaction, cancellationToken);
            var state = await LoadAuthorityStateForUpdateAsync(connection, transaction, cancellationToken);
            if (state.TransitionPending)
                throw new InvalidOperationException("A Runtime authority transition is pending.");

            if (state.DemoStartedAtUtc == expectedExistingStartedAtUtc)
            {
                const string sql = """
                    UPDATE elitescada.runtime_session_authority_state
                    SET demo_started_at_utc = @requested_started_at
                    WHERE singleton_id = 1
                      AND NOT transition_pending
                      AND demo_started_at_utc IS NOT DISTINCT FROM @expected_started_at;
                    """;
                await using var command = new NpgsqlCommand(sql, connection, transaction);
                command.Parameters.AddWithValue("requested_started_at", NpgsqlDbType.TimestampTz, requestedStartedAtUtc);
                command.Parameters.AddWithValue("expected_started_at", NpgsqlDbType.TimestampTz, (object?)expectedExistingStartedAtUtc ?? DBNull.Value);
                if (await command.ExecuteNonQueryAsync(cancellationToken) == 1)
                {
                    await transaction.CommitAsync(cancellationToken);
                    return new RuntimeDemoSessionAnchorResult(requestedStartedAtUtc, WasEstablished: true);
                }
            }

            var current = await LoadAuthorityStateForUpdateAsync(connection, transaction, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            if (current.DemoStartedAtUtc is { } concurrentAnchor)
                return new RuntimeDemoSessionAnchorResult(concurrentAnchor, WasEstablished: false);

            throw new InvalidOperationException("Runtime Demo session anchor changed concurrently.");
        }
        catch
        {
            await RollbackQuietlyAsync(transaction);
            throw;
        }
    }

    public async Task<RuntimeAuthorityTransition> BeginAuthorityTransitionAsync(
        string kind,
        DateTimeOffset startedAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await AcquireLeaseMutationLockAsync(connection, transaction, cancellationToken);
            var state = await LoadAuthorityStateForUpdateAsync(connection, transaction, cancellationToken);
            if (state.TransitionPending)
                throw new InvalidOperationException("A Runtime authority transition is already pending.");

            var transitionId = Guid.NewGuid();
            const string sql = """
                UPDATE elitescada.runtime_session_authority_state
                SET transition_pending = true,
                    transition_id = @transition_id,
                    transition_kind = @transition_kind,
                    transition_started_at_utc = @started_at,
                    transition_base_authority_revision = @authority_revision
                WHERE singleton_id = 1 AND NOT transition_pending AND authority_revision = @authority_revision;
                """;
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("transition_id", transitionId);
            command.Parameters.AddWithValue("transition_kind", kind.Trim());
            command.Parameters.AddWithValue("started_at", NpgsqlDbType.TimestampTz, startedAtUtc);
            command.Parameters.AddWithValue("authority_revision", NpgsqlDbType.Bigint, state.AuthorityRevision);
            if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
                throw new InvalidOperationException("Runtime authority transition changed concurrently.");

            await transaction.CommitAsync(cancellationToken);
            return new RuntimeAuthorityTransition(transitionId, state.AuthorityRevision, kind.Trim(), startedAtUtc);
        }
        catch
        {
            await RollbackQuietlyAsync(transaction);
            throw;
        }
    }

    public async Task<bool> AbortAuthorityTransitionAsync(
        Guid transitionId,
        long expectedBaseAuthorityRevision,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await AcquireLeaseMutationLockAsync(connection, transaction, cancellationToken);
            const string sql = """
                UPDATE elitescada.runtime_session_authority_state
                SET transition_pending = false,
                    transition_id = NULL,
                    transition_kind = NULL,
                    transition_started_at_utc = NULL,
                    transition_base_authority_revision = NULL
                WHERE singleton_id = 1
                  AND transition_pending
                  AND transition_id = @transition_id
                  AND transition_base_authority_revision = @authority_revision
                  AND authority_revision = @authority_revision;
                """;
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("transition_id", transitionId);
            command.Parameters.AddWithValue("authority_revision", NpgsqlDbType.Bigint, expectedBaseAuthorityRevision);
            var changed = await command.ExecuteNonQueryAsync(cancellationToken) == 1;
            await transaction.CommitAsync(cancellationToken);
            return changed;
        }
        catch
        {
            await RollbackQuietlyAsync(transaction);
            throw;
        }
    }

    public async Task<RuntimeAuthorityState> CommitAuthorityChangeAsync(
        Guid transitionId,
        long expectedBaseAuthorityRevision,
        DateTimeOffset authorityChangedAtUtc,
        DateTimeOffset? demoStartedAtUtc,
        CancellationToken cancellationToken = default)
    {
        demoStartedAtUtc = demoStartedAtUtc is { } anchor
            ? RuntimeDemoSessionAnchor.Normalize(anchor)
            : null;
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await AcquireLeaseMutationLockAsync(connection, transaction, cancellationToken);
            var nextRevision = checked(expectedBaseAuthorityRevision + 1);
            const string sql = """
                UPDATE elitescada.runtime_session_authority_state
                SET authority_revision = @next_revision,
                    authority_changed_at_utc = @changed_at,
                    demo_started_at_utc = @demo_started_at
                WHERE singleton_id = 1
                  AND transition_pending
                  AND transition_id = @transition_id
                  AND transition_base_authority_revision = @expected_revision
                  AND authority_revision = @expected_revision;
                """;
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("next_revision", NpgsqlDbType.Bigint, nextRevision);
            command.Parameters.AddWithValue("changed_at", NpgsqlDbType.TimestampTz, authorityChangedAtUtc);
            command.Parameters.AddWithValue("demo_started_at", NpgsqlDbType.TimestampTz, (object?)demoStartedAtUtc ?? DBNull.Value);
            command.Parameters.AddWithValue("transition_id", transitionId);
            command.Parameters.AddWithValue("expected_revision", NpgsqlDbType.Bigint, expectedBaseAuthorityRevision);
            if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
                throw new InvalidOperationException("Runtime authority transition changed before revision commit.");

            var state = await LoadAuthorityStateForUpdateAsync(connection, transaction, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return state;
        }
        catch
        {
            await RollbackQuietlyAsync(transaction);
            throw;
        }
    }

    public async Task<int> FenceLeasesBeforeAuthorityRevisionAsync(
        Guid transitionId,
        long authorityRevision,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await AcquireLeaseMutationLockAsync(connection, transaction, cancellationToken);
            var state = await LoadAuthorityStateForUpdateAsync(connection, transaction, cancellationToken);
            RequirePendingTransition(state, transitionId, authorityRevision);
            const string sql = """
                UPDATE elitescada.runtime_session_leases
                SET is_active = false, generation = generation + 1
                WHERE is_active AND authority_revision < @authority_revision;
                """;
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("authority_revision", NpgsqlDbType.Bigint, authorityRevision);
            var changed = await command.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return changed;
        }
        catch
        {
            await RollbackQuietlyAsync(transaction);
            throw;
        }
    }

    public async Task<int> RebindActiveClusterLeasesAsync(
        Guid transitionId,
        long authorityRevision,
        string clusterId,
        string serverNode,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clusterId);
        ArgumentException.ThrowIfNullOrWhiteSpace(serverNode);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await AcquireLeaseMutationLockAsync(connection, transaction, cancellationToken);
            var state = await LoadAuthorityStateForUpdateAsync(connection, transaction, cancellationToken);
            RequirePendingTransition(state, transitionId, authorityRevision);

            const string sql = """
                UPDATE elitescada.runtime_session_leases
                SET authority_revision = @authority_revision,
                    server_node = @server_node,
                    generation = generation + 1
                WHERE is_active
                  AND authority_revision < @authority_revision
                  AND cluster_id = @cluster_id;
                """;
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("authority_revision", NpgsqlDbType.Bigint, authorityRevision);
            command.Parameters.AddWithValue("server_node", NpgsqlDbType.Text, serverNode.Trim());
            command.Parameters.AddWithValue("cluster_id", NpgsqlDbType.Text, clusterId.Trim());
            var changed = await command.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return changed;
        }
        catch
        {
            await RollbackQuietlyAsync(transaction);
            throw;
        }
    }

    public async Task CompleteAuthorityTransitionAsync(
        Guid transitionId,
        long authorityRevision,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await AcquireLeaseMutationLockAsync(connection, transaction, cancellationToken);
            var state = await LoadAuthorityStateForUpdateAsync(connection, transaction, cancellationToken);
            RequirePendingTransition(state, transitionId, authorityRevision);

            const string staleSql = """
                SELECT count(*)
                FROM elitescada.runtime_session_leases
                WHERE is_active AND authority_revision < @authority_revision;
                """;
            await using (var stale = new NpgsqlCommand(staleSql, connection, transaction))
            {
                stale.Parameters.AddWithValue("authority_revision", NpgsqlDbType.Bigint, authorityRevision);
                var staleCount = Convert.ToInt64(await stale.ExecuteScalarAsync(cancellationToken));
                if (staleCount != 0)
                    throw new InvalidOperationException("Cannot complete Runtime authority transition while stale active leases remain.");
            }

            const string completeSql = """
                UPDATE elitescada.runtime_session_authority_state
                SET transition_pending = false,
                    transition_id = NULL,
                    transition_kind = NULL,
                    transition_started_at_utc = NULL,
                    transition_base_authority_revision = NULL
                WHERE singleton_id = 1
                  AND transition_pending
                  AND transition_id = @transition_id
                  AND transition_base_authority_revision = @transition_base_revision
                  AND authority_revision = @authority_revision;
                """;
            await using var complete = new NpgsqlCommand(completeSql, connection, transaction);
            complete.Parameters.AddWithValue("transition_id", transitionId);
            complete.Parameters.AddWithValue("authority_revision", NpgsqlDbType.Bigint, authorityRevision);
            complete.Parameters.AddWithValue("transition_base_revision", NpgsqlDbType.Bigint, checked(authorityRevision - 1));
            if (await complete.ExecuteNonQueryAsync(cancellationToken) != 1)
                throw new InvalidOperationException("Runtime authority transition changed before completion.");
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await RollbackQuietlyAsync(transaction);
            throw;
        }
    }

    public async Task<RuntimeSessionReplicationMutationResult> AdoptReplicatedAsync(
        RuntimeSessionLeaseState lease,
        string expectedClusterId,
        string expectedSourceNode,
        RuntimeSessionRuntimeIdentity expectedRuntime,
        long expectedAuthorityRevision,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedClusterId);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedSourceNode);
        ArgumentNullException.ThrowIfNull(expectedRuntime);

        var normalized = NormalizeReplicatedLease(lease);
        var validationFailure = ValidateReplicatedLease(
            normalized,
            expectedClusterId,
            expectedSourceNode,
            expectedRuntime,
            DateTimeOffset.UtcNow);
        if (validationFailure is not null)
            return RuntimeSessionReplicationMutationResult.Reject(validationFailure);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await AcquireLeaseMutationLockAsync(connection, transaction, cancellationToken);
            var authority = await LoadAuthorityStateForUpdateAsync(connection, transaction, cancellationToken);
            if (authority.TransitionPending)
            {
                await transaction.CommitAsync(cancellationToken);
                return RuntimeSessionReplicationMutationResult.Reject("authority-transition-pending");
            }
            if (authority.AuthorityRevision != expectedAuthorityRevision ||
                normalized.AuthorityRevision != expectedAuthorityRevision)
            {
                await transaction.CommitAsync(cancellationToken);
                return RuntimeSessionReplicationMutationResult.Reject("authority-revision-changed");
            }

            var tombstone = await LoadReplicationTombstoneAsync(
                connection,
                transaction,
                normalized.ClusterId!,
                normalized.SubjectId,
                normalized.ClientInstanceId,
                cancellationToken);
            if (tombstone is not null)
            {
                if (tombstone.SessionId == normalized.SessionId)
                {
                    await transaction.CommitAsync(cancellationToken);
                    return RuntimeSessionReplicationMutationResult.Reject("session-resurrection-rejected");
                }
                if (normalized.IssuedAtUtc <= tombstone.IssuedAtUtc)
                {
                    await transaction.CommitAsync(cancellationToken);
                    return RuntimeSessionReplicationMutationResult.Reject("replicated-session-older-than-tombstone");
                }
            }

            var sameSession = await LoadByIdForUpdateAsync(
                connection,
                transaction,
                normalized.SessionId,
                cancellationToken);
            if (sameSession is not null)
            {
                if (!sameSession.IsActive)
                {
                    await transaction.CommitAsync(cancellationToken);
                    return RuntimeSessionReplicationMutationResult.Reject("session-resurrection-rejected");
                }
                if (!ReplicatedIdentityMatches(sameSession, normalized))
                {
                    await transaction.CommitAsync(cancellationToken);
                    return RuntimeSessionReplicationMutationResult.Reject("replicated-session-conflict");
                }
                if (normalized.Generation < sameSession.Generation)
                {
                    await transaction.CommitAsync(cancellationToken);
                    return RuntimeSessionReplicationMutationResult.Reject("replicated-session-stale-generation");
                }
                if (normalized.Generation == sameSession.Generation &&
                    normalized.LastHeartbeatUtc <= sameSession.LastHeartbeatUtc &&
                    normalized.ExpiresAtUtc <= sameSession.ExpiresAtUtc)
                {
                    await transaction.CommitAsync(cancellationToken);
                    return RuntimeSessionReplicationMutationResult.Accept(
                        "replicated-session-already-current",
                        sameSession);
                }

                if (!await UpdateAdoptedAsync(
                        connection,
                        transaction,
                        normalized,
                        sameSession.Generation,
                        cancellationToken))
                {
                    throw new InvalidOperationException(
                        "Replicated Runtime session changed during adoption.");
                }

                await transaction.CommitAsync(cancellationToken);
                return RuntimeSessionReplicationMutationResult.Accept(
                    "replicated-session-updated",
                    normalized);
            }

            var logicalConflict = await LoadActiveByIdentityAsync(
                connection,
                transaction,
                normalized.SubjectId,
                normalized.ClientInstanceId,
                cancellationToken);
            if (logicalConflict is not null)
            {
                await transaction.CommitAsync(cancellationToken);
                return RuntimeSessionReplicationMutationResult.Reject(
                    "replicated-logical-session-conflict");
            }

            await InsertAsync(
                connection,
                transaction,
                normalized,
                normalized.ExpiresAtUtc - normalized.LastHeartbeatUtc,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return RuntimeSessionReplicationMutationResult.Accept(
                "replicated-session-adopted",
                normalized);
        }
        catch
        {
            await RollbackQuietlyAsync(transaction);
            throw;
        }
    }

    public async Task<RuntimeSessionReplicationMutationResult> ApplyReplicatedTombstoneAsync(
        RuntimeSessionLeaseTombstoneState tombstone,
        string expectedClusterId,
        string expectedSourceNode,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tombstone);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedClusterId);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedSourceNode);

        var normalized = NormalizeReplicatedTombstone(tombstone);
        var validationFailure = ValidateReplicatedTombstone(
            normalized,
            expectedClusterId,
            expectedSourceNode);
        if (validationFailure is not null)
            return RuntimeSessionReplicationMutationResult.Reject(validationFailure);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await AcquireLeaseMutationLockAsync(connection, transaction, cancellationToken);
            var authority = await LoadAuthorityStateForUpdateAsync(connection, transaction, cancellationToken);
            if (authority.TransitionPending)
            {
                await transaction.CommitAsync(cancellationToken);
                return RuntimeSessionReplicationMutationResult.Reject("authority-transition-pending");
            }
            if (normalized.AuthorityRevision > authority.AuthorityRevision)
            {
                await transaction.CommitAsync(cancellationToken);
                return RuntimeSessionReplicationMutationResult.Reject("authority-revision-changed");
            }

            var current = await LoadReplicationTombstoneAsync(
                connection,
                transaction,
                normalized.ClusterId,
                normalized.SubjectId,
                normalized.ClientInstanceId,
                cancellationToken);
            if (current is not null &&
                (normalized.AuthorityRevision < current.AuthorityRevision ||
                 (normalized.AuthorityRevision == current.AuthorityRevision &&
                  normalized.Generation < current.Generation) ||
                 (normalized.AuthorityRevision == current.AuthorityRevision &&
                  normalized.Generation == current.Generation &&
                  normalized.RecordedAtUtc <= current.RecordedAtUtc)))
            {
                await transaction.CommitAsync(cancellationToken);
                return RuntimeSessionReplicationMutationResult.Accept(
                    "replicated-tombstone-already-current");
            }

            var lease = await LoadByIdForUpdateAsync(
                connection,
                transaction,
                normalized.SessionId,
                cancellationToken);
            if (lease is { IsActive: true })
            {
                if (lease.AuthorityRevision > normalized.AuthorityRevision ||
                    lease.Generation > normalized.Generation)
                {
                    await transaction.CommitAsync(cancellationToken);
                    return RuntimeSessionReplicationMutationResult.Reject(
                        "replicated-tombstone-stale");
                }

                var terminalGeneration = Math.Max(
                    checked(lease.Generation + 1),
                    normalized.Generation);
                if (!await DeactivateForTombstoneAsync(
                        connection,
                        transaction,
                        lease.SessionId,
                        lease.Generation,
                        terminalGeneration,
                        cancellationToken))
                {
                    throw new InvalidOperationException(
                        "Runtime session changed while applying replicated tombstone.");
                }
            }

            await UpsertReplicationTombstoneAsync(
                connection,
                transaction,
                normalized,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return RuntimeSessionReplicationMutationResult.Accept(
                "replicated-tombstone-applied");
        }
        catch
        {
            await RollbackQuietlyAsync(transaction);
            throw;
        }
    }

    public async Task<RuntimeSessionLeaseState> AdmitAsync(
        RuntimeSessionLeaseAdmission admission,
        CancellationToken cancellationToken = default)
    {
        ValidateAdmission(admission);
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await AcquireLeaseMutationLockAsync(connection, transaction, cancellationToken);
            var authorityState = await LoadAuthorityStateForUpdateAsync(connection, transaction, cancellationToken);
            if (authorityState.TransitionPending)
                throw new InvalidOperationException("runtime-authority-transition-pending");
            var subjectId = admission.SubjectId.Trim();
            var clientInstanceId = admission.ClientInstanceId.Trim();
            var now = DateTimeOffset.UtcNow;
            await DeactivateExpiredForIdentityAsync(connection, transaction, subjectId, clientInstanceId, now, cancellationToken);
            var active = await LoadActiveByIdentityAsync(connection, transaction, subjectId, clientInstanceId, cancellationToken);
            if (active is not null && active.AuthorityRevision != authorityState.AuthorityRevision)
            {
                if (!await DeactivateAsync(connection, transaction, active.SessionId, active.Generation, cancellationToken))
                    throw new InvalidOperationException("Runtime session admission changed concurrently.");
                active = null;
            }
            if (active is not null && active.Runtime.Matches(admission.Runtime))
            {
                var retainedClass = MostRestrictiveConnectionClass(
                    active.GrantedConnectionClass,
                    admission.GrantedConnectionClass);
                if (!string.Equals(retainedClass, active.GrantedConnectionClass, StringComparison.OrdinalIgnoreCase))
                {
                    var downscoped = active with
                    {
                        GrantedConnectionClass = retainedClass,
                        Generation = checked(active.Generation + 1)
                    };
                    if (!await DownscopeAsync(connection, transaction, downscoped, active.Generation, cancellationToken))
                        throw new InvalidOperationException("Runtime session admission changed concurrently.");
                    active = downscoped;
                }
                await transaction.CommitAsync(cancellationToken);
                return active;
            }
            if (active is not null)
                await DeactivateAsync(connection, transaction, active.SessionId, active.Generation, cancellationToken);

            var lease = new RuntimeSessionLeaseState(
                Guid.NewGuid(),
                subjectId,
                clientInstanceId,
                admission.GrantedConnectionClass.Trim().ToLowerInvariant(),
                1,
                now,
                now,
                now.Add(admission.LeaseDuration),
                admission.Runtime,
                NormalizeOptional(admission.ServerNode),
                NormalizeOptional(admission.ClusterId),
                true,
                authorityState.AuthorityRevision);
            await InsertAsync(connection, transaction, lease, admission.LeaseDuration, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return lease;
        }
        catch
        {
            await RollbackQuietlyAsync(transaction);
            throw;
        }
    }

    public async Task<RuntimeSessionLeaseCapacityAdmissionResult> AdmitWithCapacityAsync(
        RuntimeSessionLeaseCapacityAdmission capacityAdmission,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(capacityAdmission);
        var admission = capacityAdmission.Lease;
        ValidateAdmission(admission);
        capacityAdmission.Capacity.Validate();
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await AcquireLeaseMutationLockAsync(connection, transaction, cancellationToken);
            var authorityState = await LoadAuthorityStateForUpdateAsync(connection, transaction, cancellationToken);
            if (authorityState.TransitionPending)
            {
                await transaction.CommitAsync(cancellationToken);
                return RuntimeSessionLeaseCapacityAdmissionResult.Rejected(
                    RuntimeSessionSeatReservationReasonCode.AuthorityTransitionPending);
            }
            if (capacityAdmission.ExpectedAuthorityRevision != authorityState.AuthorityRevision)
            {
                await transaction.CommitAsync(cancellationToken);
                return RuntimeSessionLeaseCapacityAdmissionResult.Rejected(
                    RuntimeSessionSeatReservationReasonCode.AuthorityRevisionChanged);
            }
            var subjectId = admission.SubjectId.Trim();
            var clientInstanceId = admission.ClientInstanceId.Trim();
            var now = DateTimeOffset.UtcNow;
            await DeactivateExpiredAsync(connection, transaction, now, cancellationToken);
            var active = await LoadActiveByIdentityAsync(connection, transaction, subjectId, clientInstanceId, cancellationToken);
            if (active is not null && active.AuthorityRevision != authorityState.AuthorityRevision)
            {
                if (!await DeactivateAsync(connection, transaction, active.SessionId, active.Generation, cancellationToken))
                    throw new InvalidOperationException("Runtime session admission changed concurrently.");
                active = null;
            }

            if (active is not null && active.Runtime.Matches(admission.Runtime))
            {
                if (IsViewOnlyConnectionClass(active.GrantedConnectionClass))
                {
                    await transaction.CommitAsync(cancellationToken);
                    return RuntimeSessionLeaseCapacityAdmissionResult.Admitted(active, RuntimeSessionSeatReservationReasonCode.ExistingLeaseRetained);
                }

                if (!IsViewOnlyConnectionClass(admission.GrantedConnectionClass))
                {
                    await transaction.CommitAsync(cancellationToken);
                    return RuntimeSessionLeaseCapacityAdmissionResult.Admitted(active, RuntimeSessionSeatReservationReasonCode.InteractiveReserved);
                }

                if (await CountActiveByClassAsync(connection, transaction, admission.Runtime, "viewer", now, cancellationToken) >= capacityAdmission.Capacity.ViewOnlySeats)
                {
                    if (!await DeactivateAsync(connection, transaction, active.SessionId, active.Generation, cancellationToken))
                        throw new InvalidOperationException("Runtime session admission changed concurrently.");
                    await transaction.CommitAsync(cancellationToken);
                    return RuntimeSessionLeaseCapacityAdmissionResult.Rejected(RuntimeSessionSeatReservationReasonCode.ViewOnlyQuotaExhausted);
                }

                var downscoped = active with { GrantedConnectionClass = "viewer", Generation = checked(active.Generation + 1) };
                if (!await DownscopeAsync(connection, transaction, downscoped, active.Generation, cancellationToken))
                    throw new InvalidOperationException("Runtime session admission changed concurrently.");
                await transaction.CommitAsync(cancellationToken);
                return RuntimeSessionLeaseCapacityAdmissionResult.Admitted(downscoped, RuntimeSessionSeatReservationReasonCode.AuthorityDownscopeViewOnlyReserved);
            }

            if (active is not null && !await DeactivateAsync(connection, transaction, active.SessionId, active.Generation, cancellationToken))
                throw new InvalidOperationException("Runtime session admission changed concurrently.");

            var interactiveInUse = await CountActiveByClassAsync(connection, transaction, admission.Runtime, "interactive", now, cancellationToken);
            var viewOnlyInUse = await CountActiveByClassAsync(connection, transaction, admission.Runtime, "viewer", now, cancellationToken);
            var desiredViewOnly = IsViewOnlyConnectionClass(admission.GrantedConnectionClass);
            string grantedClass;
            RuntimeSessionSeatReservationReasonCode reason;
            if (desiredViewOnly)
            {
                if (viewOnlyInUse >= capacityAdmission.Capacity.ViewOnlySeats)
                {
                    await transaction.CommitAsync(cancellationToken);
                    return RuntimeSessionLeaseCapacityAdmissionResult.Rejected(RuntimeSessionSeatReservationReasonCode.ViewOnlyQuotaExhausted);
                }
                grantedClass = "viewer";
                reason = RuntimeSessionSeatReservationReasonCode.ViewOnlyReserved;
            }
            else if (interactiveInUse < capacityAdmission.Capacity.InteractiveSeats)
            {
                grantedClass = "interactive";
                reason = RuntimeSessionSeatReservationReasonCode.InteractiveReserved;
            }
            else if (viewOnlyInUse < capacityAdmission.Capacity.ViewOnlySeats)
            {
                grantedClass = "viewer";
                reason = RuntimeSessionSeatReservationReasonCode.InteractiveQuotaFallbackViewOnly;
            }
            else if (capacityAdmission.Capacity.ViewOnlySeats == 0)
            {
                await transaction.CommitAsync(cancellationToken);
                return RuntimeSessionLeaseCapacityAdmissionResult.Rejected(RuntimeSessionSeatReservationReasonCode.InteractiveQuotaExhaustedNoEligibleViewOnly);
            }
            else
            {
                await transaction.CommitAsync(cancellationToken);
                return RuntimeSessionLeaseCapacityAdmissionResult.Rejected(RuntimeSessionSeatReservationReasonCode.EligiblePoolsExhausted);
            }

            var lease = new RuntimeSessionLeaseState(
                Guid.NewGuid(), subjectId, clientInstanceId, grantedClass, 1, now, now,
                now.Add(admission.LeaseDuration), admission.Runtime,
                NormalizeOptional(admission.ServerNode), NormalizeOptional(admission.ClusterId), true,
                authorityState.AuthorityRevision);
            await InsertAsync(connection, transaction, lease, admission.LeaseDuration, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return RuntimeSessionLeaseCapacityAdmissionResult.Admitted(lease, reason);
        }
        catch
        {
            await RollbackQuietlyAsync(transaction);
            throw;
        }
    }

    public async Task<RuntimeSessionLeaseStoreResult> ValidateAsync(
        Guid sessionId,
        string subjectId,
        RuntimeSessionRuntimeIdentity runtime,
        string? clientInstanceId = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await AcquireLeaseMutationLockAsync(connection, transaction, cancellationToken);
            var authorityState = await LoadAuthorityStateForUpdateAsync(connection, transaction, cancellationToken);
            var result = await ValidateForMutationAsync(connection, transaction, authorityState, sessionId, subjectId, runtime, clientInstanceId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await RollbackQuietlyAsync(transaction);
            throw;
        }
    }

    public async Task<RuntimeSessionLeaseStoreResult> HeartbeatAsync(
        Guid sessionId,
        string subjectId,
        string clientInstanceId,
        RuntimeSessionRuntimeIdentity runtime,
        CancellationToken cancellationToken = default,
        long? expectedGeneration = null)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await AcquireLeaseMutationLockAsync(connection, transaction, cancellationToken);
            var authorityState = await LoadAuthorityStateForUpdateAsync(connection, transaction, cancellationToken);
            var validation = await ValidateForMutationAsync(connection, transaction, authorityState, sessionId, subjectId, runtime, clientInstanceId, cancellationToken);
            if (!validation.IsValid || validation.Lease is null)
            {
                await transaction.CommitAsync(cancellationToken);
                return validation;
            }
            if (expectedGeneration.HasValue && validation.Lease.Generation != expectedGeneration.Value)
            {
                await transaction.CommitAsync(cancellationToken);
                return RuntimeSessionLeaseStoreResult.Invalid("session-changed");
            }

            var current = validation.Lease;
            var now = DateTimeOffset.UtcNow;
            var renewed = current with
            {
                Generation = checked(current.Generation + 1),
                LastHeartbeatUtc = now,
                ExpiresAtUtc = now.Add(current.ExpiresAtUtc - current.LastHeartbeatUtc)
            };
            if (!await RenewAsync(connection, transaction, renewed, current.Generation, cancellationToken))
            {
                await transaction.CommitAsync(cancellationToken);
                return RuntimeSessionLeaseStoreResult.Invalid("session-changed");
            }
            await transaction.CommitAsync(cancellationToken);
            return RuntimeSessionLeaseStoreResult.Valid(renewed);
        }
        catch
        {
            await RollbackQuietlyAsync(transaction);
            throw;
        }
    }

    public async Task<RuntimeSessionLeaseStoreResult> TerminateAsync(
        Guid sessionId,
        string subjectId,
        string clientInstanceId,
        RuntimeSessionRuntimeIdentity runtime,
        CancellationToken cancellationToken = default,
        long? expectedGeneration = null)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await AcquireLeaseMutationLockAsync(connection, transaction, cancellationToken);
            var authorityState = await LoadAuthorityStateForUpdateAsync(connection, transaction, cancellationToken);
            var validation = await ValidateForMutationAsync(connection, transaction, authorityState, sessionId, subjectId, runtime, clientInstanceId, cancellationToken);
            if (!validation.IsValid || validation.Lease is null)
            {
                await transaction.CommitAsync(cancellationToken);
                return validation;
            }
            if (expectedGeneration.HasValue && validation.Lease.Generation != expectedGeneration.Value)
            {
                await transaction.CommitAsync(cancellationToken);
                return RuntimeSessionLeaseStoreResult.Invalid("session-changed");
            }
            if (!await DeactivateAsync(connection, transaction, sessionId, validation.Lease.Generation, cancellationToken))
            {
                await transaction.CommitAsync(cancellationToken);
                return RuntimeSessionLeaseStoreResult.Invalid("session-changed");
            }
            await transaction.CommitAsync(cancellationToken);
            return validation;
        }
        catch
        {
            await RollbackQuietlyAsync(transaction);
            throw;
        }
    }

    private static async Task<RuntimeSessionLeaseStoreResult> ValidateForMutationAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        RuntimeAuthorityState authorityState,
        Guid sessionId,
        string subjectId,
        RuntimeSessionRuntimeIdentity runtime,
        string? clientInstanceId,
        CancellationToken cancellationToken)
    {
        if (authorityState.TransitionPending)
            return RuntimeSessionLeaseStoreResult.Invalid("authority-transition-pending");
        if (sessionId == Guid.Empty) return RuntimeSessionLeaseStoreResult.Invalid("invalid-session-id");
        if (string.IsNullOrWhiteSpace(subjectId)) return RuntimeSessionLeaseStoreResult.Invalid("missing-user");
        ArgumentNullException.ThrowIfNull(runtime);

        var lease = await LoadByIdForUpdateAsync(connection, transaction, sessionId, cancellationToken);
        if (lease is null || !lease.IsActive) return RuntimeSessionLeaseStoreResult.Invalid("session-not-found");
        if (lease.AuthorityRevision != authorityState.AuthorityRevision)
            return RuntimeSessionLeaseStoreResult.Invalid("authority-revision-stale");

        if (lease.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            await DeactivateAsync(connection, transaction, lease.SessionId, lease.Generation, cancellationToken);
            return RuntimeSessionLeaseStoreResult.Invalid("session-expired");
        }
        if (!lease.SubjectId.Equals(subjectId.Trim(), StringComparison.Ordinal))
            return RuntimeSessionLeaseStoreResult.Invalid("session-user-mismatch");
        if (clientInstanceId is not null &&
            !lease.ClientInstanceId.Equals(clientInstanceId.Trim(), StringComparison.Ordinal))
            return RuntimeSessionLeaseStoreResult.Invalid("session-client-mismatch");
        if (!lease.Runtime.Matches(runtime))
        {
            await DeactivateAsync(connection, transaction, lease.SessionId, lease.Generation, cancellationToken);
            return RuntimeSessionLeaseStoreResult.Invalid("runtime-changed");
        }
        return RuntimeSessionLeaseStoreResult.Valid(lease);
    }

    private static async Task<RuntimeAuthorityState> LoadAuthorityStateForUpdateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT authority_revision, transition_pending, transition_id, transition_kind,
                   transition_started_at_utc, transition_base_authority_revision,
                   authority_changed_at_utc, demo_started_at_utc
            FROM elitescada.runtime_session_authority_state
            WHERE singleton_id = 1
            FOR UPDATE;
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new InvalidOperationException("Runtime authority singleton state is missing.");
        return new RuntimeAuthorityState(
            reader.GetInt64(0),
            reader.GetBoolean(1),
            reader.IsDBNull(2) ? null : reader.GetGuid(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetFieldValue<DateTimeOffset>(4),
            reader.IsDBNull(5) ? null : reader.GetInt64(5),
            reader.IsDBNull(6) ? null : reader.GetFieldValue<DateTimeOffset>(6),
            reader.IsDBNull(7) ? null : reader.GetFieldValue<DateTimeOffset>(7),
            SupportsDurableRecovery: true);
    }

    private static void RequirePendingTransition(
        RuntimeAuthorityState state,
        Guid transitionId,
        long authorityRevision)
    {
        if (!state.TransitionPending ||
            state.TransitionId != transitionId ||
            state.TransitionBaseAuthorityRevision is not { } transitionBase ||
            state.AuthorityRevision != authorityRevision ||
            authorityRevision != checked(transitionBase + 1))
        {
            throw new InvalidOperationException("Runtime authority transition does not match the pending transition.");
        }
    }

    private static async Task AcquireLeaseMutationLockAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("SELECT pg_advisory_xact_lock(@lock_key);", connection, transaction);
        command.Parameters.AddWithValue("lock_key", NpgsqlDbType.Bigint, LeaseMutationAdvisoryLock);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task DeactivateExpiredForIdentityAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string subjectId, string clientInstanceId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE elitescada.runtime_session_leases
            SET is_active = false, generation = generation + 1
            WHERE subject_id = @subject_id AND client_instance_id = @client_instance_id
              AND is_active AND expires_at_utc <= @now;
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("subject_id", subjectId);
        command.Parameters.AddWithValue("client_instance_id", clientInstanceId);
        command.Parameters.AddWithValue("now", NpgsqlDbType.TimestampTz, now);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task DeactivateExpiredAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, DateTimeOffset now, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE elitescada.runtime_session_leases
            SET is_active = false, generation = generation + 1
            WHERE is_active AND expires_at_utc <= @now;
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("now", NpgsqlDbType.TimestampTz, now);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<int> CountActiveByClassAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        RuntimeSessionRuntimeIdentity runtime,
        string connectionClass,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        runtime = runtime.NormalizeStoragePrecision();
        const string sql = """
            SELECT count(*)
            FROM elitescada.runtime_session_leases
            WHERE is_active AND expires_at_utc > @now
              AND runtime_mode = @runtime_mode
              AND runtime_project_key IS NOT DISTINCT FROM @runtime_project_key
              AND runtime_revision IS NOT DISTINCT FROM @runtime_revision
              AND runtime_activated_at_utc IS NOT DISTINCT FROM @runtime_activated_at_utc
              AND requested_connection_class = @connection_class;
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("now", NpgsqlDbType.TimestampTz, now);
        command.Parameters.AddWithValue("runtime_mode", runtime.Mode);
        command.Parameters.AddWithValue("runtime_project_key", NpgsqlDbType.Text, (object?)runtime.ProjectKey ?? DBNull.Value);
        command.Parameters.AddWithValue("runtime_revision", NpgsqlDbType.Bigint, (object?)runtime.Revision ?? DBNull.Value);
        command.Parameters.AddWithValue("runtime_activated_at_utc", NpgsqlDbType.TimestampTz, (object?)runtime.ActivatedAtUtc ?? DBNull.Value);
        command.Parameters.AddWithValue("connection_class", connectionClass);
        return checked(Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)));
    }

    private static async Task<RuntimeSessionLeaseState?> LoadActiveByIdentityAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string subjectId, string clientInstanceId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT session_id, subject_id, client_instance_id, requested_connection_class, generation,
                   issued_at_utc, last_heartbeat_utc, expires_at_utc, runtime_mode, runtime_project_key,
                   runtime_revision, runtime_activated_at_utc, server_node, cluster_id, is_active, authority_revision
            FROM elitescada.runtime_session_leases
            WHERE subject_id = @subject_id AND client_instance_id = @client_instance_id AND is_active
            FOR UPDATE;
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("subject_id", subjectId);
        command.Parameters.AddWithValue("client_instance_id", clientInstanceId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Read(reader) : null;
    }

    private static async Task<RuntimeSessionLeaseState?> LoadByIdForUpdateAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid sessionId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT session_id, subject_id, client_instance_id, requested_connection_class, generation,
                   issued_at_utc, last_heartbeat_utc, expires_at_utc, runtime_mode, runtime_project_key,
                   runtime_revision, runtime_activated_at_utc, server_node, cluster_id, is_active, authority_revision
            FROM elitescada.runtime_session_leases WHERE session_id = @session_id FOR UPDATE;
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("session_id", sessionId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Read(reader) : null;
    }

    private static async Task<RuntimeSessionLeaseTombstoneState?> LoadReplicationTombstoneAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string clusterId,
        string subjectId,
        string clientInstanceId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT cluster_id, subject_id, client_instance_id, session_id, generation,
                   authority_revision, issued_at_utc, source_node, recorded_at_utc, reason_code
            FROM elitescada.runtime_session_lease_tombstones
            WHERE cluster_id = @cluster_id
              AND subject_id = @subject_id
              AND client_instance_id = @client_instance_id
            FOR UPDATE;
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("cluster_id", clusterId);
        command.Parameters.AddWithValue("subject_id", subjectId);
        command.Parameters.AddWithValue("client_instance_id", clientInstanceId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new RuntimeSessionLeaseTombstoneState(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetGuid(3),
            reader.GetInt64(4),
            reader.GetInt64(5),
            reader.GetFieldValue<DateTimeOffset>(6),
            reader.GetString(7),
            reader.GetFieldValue<DateTimeOffset>(8),
            reader.GetString(9));
    }

    private static async Task UpsertReplicationTombstoneAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        RuntimeSessionLeaseTombstoneState tombstone,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO elitescada.runtime_session_lease_tombstones (
                cluster_id, subject_id, client_instance_id, session_id, generation,
                authority_revision, issued_at_utc, source_node, recorded_at_utc, reason_code)
            VALUES (
                @cluster_id, @subject_id, @client_instance_id, @session_id, @generation,
                @authority_revision, @issued_at_utc, @source_node, @recorded_at_utc, @reason_code)
            ON CONFLICT (cluster_id, subject_id, client_instance_id)
            DO UPDATE SET
                session_id = EXCLUDED.session_id,
                generation = EXCLUDED.generation,
                authority_revision = EXCLUDED.authority_revision,
                issued_at_utc = EXCLUDED.issued_at_utc,
                source_node = EXCLUDED.source_node,
                recorded_at_utc = EXCLUDED.recorded_at_utc,
                reason_code = EXCLUDED.reason_code;
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("cluster_id", tombstone.ClusterId);
        command.Parameters.AddWithValue("subject_id", tombstone.SubjectId);
        command.Parameters.AddWithValue("client_instance_id", tombstone.ClientInstanceId);
        command.Parameters.AddWithValue("session_id", tombstone.SessionId);
        command.Parameters.AddWithValue("generation", NpgsqlDbType.Bigint, tombstone.Generation);
        command.Parameters.AddWithValue("authority_revision", NpgsqlDbType.Bigint, tombstone.AuthorityRevision);
        command.Parameters.AddWithValue("issued_at_utc", NpgsqlDbType.TimestampTz, tombstone.IssuedAtUtc);
        command.Parameters.AddWithValue("source_node", tombstone.SourceNode);
        command.Parameters.AddWithValue("recorded_at_utc", NpgsqlDbType.TimestampTz, tombstone.RecordedAtUtc);
        command.Parameters.AddWithValue("reason_code", tombstone.ReasonCode);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, RuntimeSessionLeaseState lease, TimeSpan duration, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO elitescada.runtime_session_leases (
                session_id, subject_id, client_instance_id, requested_connection_class, generation,
                issued_at_utc, last_heartbeat_utc, expires_at_utc, lease_duration, runtime_mode,
                runtime_project_key, runtime_revision, runtime_activated_at_utc, server_node, cluster_id, is_active, authority_revision)
            VALUES (
                @session_id, @subject_id, @client_instance_id, @requested_connection_class, @generation,
                @issued_at_utc, @last_heartbeat_utc, @expires_at_utc, @lease_duration, @runtime_mode,
                @runtime_project_key, @runtime_revision, @runtime_activated_at_utc, @server_node, @cluster_id, true, @authority_revision);
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        BindLease(command, lease);
        command.Parameters.AddWithValue("lease_duration", NpgsqlDbType.Interval, duration);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<bool> UpdateAdoptedAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        RuntimeSessionLeaseState lease,
        long expectedGeneration,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE elitescada.runtime_session_leases
            SET generation = @generation,
                last_heartbeat_utc = @last_heartbeat_utc,
                expires_at_utc = @expires_at_utc
            WHERE session_id = @session_id
              AND is_active
              AND generation = @expected_generation;
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("generation", NpgsqlDbType.Bigint, lease.Generation);
        command.Parameters.AddWithValue("last_heartbeat_utc", NpgsqlDbType.TimestampTz, lease.LastHeartbeatUtc);
        command.Parameters.AddWithValue("expires_at_utc", NpgsqlDbType.TimestampTz, lease.ExpiresAtUtc);
        command.Parameters.AddWithValue("session_id", lease.SessionId);
        command.Parameters.AddWithValue("expected_generation", NpgsqlDbType.Bigint, expectedGeneration);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    private static async Task<bool> RenewAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, RuntimeSessionLeaseState lease, long expectedGeneration, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE elitescada.runtime_session_leases
            SET generation = @generation, last_heartbeat_utc = @last_heartbeat_utc, expires_at_utc = @expires_at_utc
            WHERE session_id = @session_id AND is_active AND generation = @expected_generation;
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("generation", NpgsqlDbType.Bigint, lease.Generation);
        command.Parameters.AddWithValue("last_heartbeat_utc", NpgsqlDbType.TimestampTz, lease.LastHeartbeatUtc);
        command.Parameters.AddWithValue("expires_at_utc", NpgsqlDbType.TimestampTz, lease.ExpiresAtUtc);
        command.Parameters.AddWithValue("session_id", lease.SessionId);
        command.Parameters.AddWithValue("expected_generation", NpgsqlDbType.Bigint, expectedGeneration);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    private static async Task<bool> DownscopeAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, RuntimeSessionLeaseState lease, long expectedGeneration, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE elitescada.runtime_session_leases
            SET requested_connection_class = @requested_connection_class, generation = @generation
            WHERE session_id = @session_id AND is_active AND generation = @expected_generation;
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("requested_connection_class", lease.GrantedConnectionClass);
        command.Parameters.AddWithValue("generation", NpgsqlDbType.Bigint, lease.Generation);
        command.Parameters.AddWithValue("session_id", lease.SessionId);
        command.Parameters.AddWithValue("expected_generation", NpgsqlDbType.Bigint, expectedGeneration);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    private static async Task<bool> DeactivateAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid sessionId, long expectedGeneration, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE elitescada.runtime_session_leases
            SET is_active = false, generation = generation + 1
            WHERE session_id = @session_id AND is_active AND generation = @expected_generation;
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("session_id", sessionId);
        command.Parameters.AddWithValue("expected_generation", NpgsqlDbType.Bigint, expectedGeneration);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    private static async Task<bool> DeactivateForTombstoneAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid sessionId,
        long expectedGeneration,
        long terminalGeneration,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE elitescada.runtime_session_leases
            SET is_active = false, generation = @terminal_generation
            WHERE session_id = @session_id AND is_active AND generation = @expected_generation;
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("session_id", sessionId);
        command.Parameters.AddWithValue("expected_generation", NpgsqlDbType.Bigint, expectedGeneration);
        command.Parameters.AddWithValue("terminal_generation", NpgsqlDbType.Bigint, terminalGeneration);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    private static void BindLease(NpgsqlCommand command, RuntimeSessionLeaseState lease)
    {
        command.Parameters.AddWithValue("session_id", lease.SessionId);
        command.Parameters.AddWithValue("subject_id", lease.SubjectId);
        command.Parameters.AddWithValue("client_instance_id", lease.ClientInstanceId);
        // The v1 database column name is retained for migration compatibility; its value is the
        // server-granted class after Runtime Admission, never untrusted client input.
        command.Parameters.AddWithValue("requested_connection_class", lease.GrantedConnectionClass);
        command.Parameters.AddWithValue("generation", NpgsqlDbType.Bigint, lease.Generation);
        command.Parameters.AddWithValue("issued_at_utc", NpgsqlDbType.TimestampTz, lease.IssuedAtUtc);
        command.Parameters.AddWithValue("last_heartbeat_utc", NpgsqlDbType.TimestampTz, lease.LastHeartbeatUtc);
        command.Parameters.AddWithValue("expires_at_utc", NpgsqlDbType.TimestampTz, lease.ExpiresAtUtc);
        command.Parameters.AddWithValue("runtime_mode", lease.Runtime.Mode);
        command.Parameters.AddWithValue("runtime_project_key", NpgsqlDbType.Text, (object?)lease.Runtime.ProjectKey ?? DBNull.Value);
        command.Parameters.AddWithValue("runtime_revision", NpgsqlDbType.Bigint, (object?)lease.Runtime.Revision ?? DBNull.Value);
        command.Parameters.AddWithValue("runtime_activated_at_utc", NpgsqlDbType.TimestampTz, (object?)lease.Runtime.ActivatedAtUtc ?? DBNull.Value);
        command.Parameters.AddWithValue("server_node", NpgsqlDbType.Text, (object?)lease.ServerNode ?? DBNull.Value);
        command.Parameters.AddWithValue("cluster_id", NpgsqlDbType.Text, (object?)lease.ClusterId ?? DBNull.Value);
        command.Parameters.AddWithValue("authority_revision", NpgsqlDbType.Bigint, lease.AuthorityRevision);
    }

    private static RuntimeSessionLeaseState Read(NpgsqlDataReader reader) => new(
        reader.GetGuid(0),
        reader.GetString(1),
        reader.GetString(2),
        reader.GetString(3),
        reader.GetInt64(4),
        reader.GetFieldValue<DateTimeOffset>(5),
        reader.GetFieldValue<DateTimeOffset>(6),
        reader.GetFieldValue<DateTimeOffset>(7),
        new RuntimeSessionRuntimeIdentity(
            reader.GetString(8),
            reader.IsDBNull(9) ? null : reader.GetString(9),
            reader.IsDBNull(10) ? null : reader.GetInt64(10),
            reader.IsDBNull(11) ? null : reader.GetFieldValue<DateTimeOffset>(11)),
        reader.IsDBNull(12) ? null : reader.GetString(12),
        reader.IsDBNull(13) ? null : reader.GetString(13),
        reader.GetBoolean(14),
        reader.GetInt64(15));

    private static void ValidateAdmission(RuntimeSessionLeaseAdmission admission)
    {
        ArgumentNullException.ThrowIfNull(admission);
        ArgumentException.ThrowIfNullOrWhiteSpace(admission.SubjectId);
        ArgumentException.ThrowIfNullOrWhiteSpace(admission.ClientInstanceId);
        if (admission.ClientInstanceId.Trim().Length > 128)
            throw new ArgumentOutOfRangeException(nameof(admission), "Client instance id must not exceed 128 characters.");
        if (!string.Equals(admission.GrantedConnectionClass, "viewer", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(admission.GrantedConnectionClass, "viewonly", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(admission.GrantedConnectionClass, "interactive", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Runtime connection class is invalid.", nameof(admission));
        ArgumentNullException.ThrowIfNull(admission.Runtime);
        ArgumentException.ThrowIfNullOrWhiteSpace(admission.Runtime.Mode);
        if (admission.LeaseDuration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(admission), "Lease duration must be positive.");
    }

    private static RuntimeSessionLeaseState NormalizeReplicatedLease(
        RuntimeSessionLeaseState lease) =>
        lease with
        {
            SubjectId = lease.SubjectId.Trim(),
            ClientInstanceId = lease.ClientInstanceId.Trim(),
            GrantedConnectionClass = IsViewOnlyConnectionClass(lease.GrantedConnectionClass)
                ? "viewer"
                : "interactive",
            IssuedAtUtc = RuntimeDemoSessionAnchor.Normalize(lease.IssuedAtUtc),
            LastHeartbeatUtc = RuntimeDemoSessionAnchor.Normalize(lease.LastHeartbeatUtc),
            ExpiresAtUtc = RuntimeDemoSessionAnchor.Normalize(lease.ExpiresAtUtc),
            Runtime = lease.Runtime.NormalizeStoragePrecision(),
            ServerNode = NormalizeOptional(lease.ServerNode),
            ClusterId = NormalizeOptional(lease.ClusterId),
            IsActive = true
        };

    private static RuntimeSessionLeaseTombstoneState NormalizeReplicatedTombstone(
        RuntimeSessionLeaseTombstoneState tombstone) =>
        tombstone with
        {
            ClusterId = tombstone.ClusterId.Trim(),
            SubjectId = tombstone.SubjectId.Trim(),
            ClientInstanceId = tombstone.ClientInstanceId.Trim(),
            SourceNode = tombstone.SourceNode.Trim(),
            IssuedAtUtc = RuntimeDemoSessionAnchor.Normalize(tombstone.IssuedAtUtc),
            RecordedAtUtc = RuntimeDemoSessionAnchor.Normalize(tombstone.RecordedAtUtc),
            ReasonCode = tombstone.ReasonCode.Trim()
        };

    private static string? ValidateReplicatedLease(
        RuntimeSessionLeaseState lease,
        string expectedClusterId,
        string expectedSourceNode,
        RuntimeSessionRuntimeIdentity expectedRuntime,
        DateTimeOffset now)
    {
        if (lease.SessionId == Guid.Empty) return "replicated-session-id-invalid";
        if (string.IsNullOrWhiteSpace(lease.SubjectId)) return "replicated-session-subject-missing";
        if (string.IsNullOrWhiteSpace(lease.ClientInstanceId) ||
            lease.ClientInstanceId.Length > 128)
            return "replicated-session-client-invalid";
        if (lease.Generation < 1) return "replicated-session-generation-invalid";
        if (lease.AuthorityRevision < 1) return "replicated-session-authority-revision-invalid";
        if (lease.ExpiresAtUtc <= lease.LastHeartbeatUtc ||
            lease.LastHeartbeatUtc < lease.IssuedAtUtc)
            return "replicated-session-time-invalid";
        if (lease.ExpiresAtUtc <= now) return "replicated-session-expired";
        if (!string.Equals(lease.ClusterId, expectedClusterId.Trim(), StringComparison.OrdinalIgnoreCase))
            return "replicated-session-cluster-mismatch";
        if (!string.Equals(lease.ServerNode, expectedSourceNode.Trim(), StringComparison.OrdinalIgnoreCase))
            return "replicated-session-source-mismatch";
        if (!lease.Runtime.Matches(expectedRuntime))
            return "replicated-session-runtime-mismatch";
        return null;
    }

    private static string? ValidateReplicatedTombstone(
        RuntimeSessionLeaseTombstoneState tombstone,
        string expectedClusterId,
        string expectedSourceNode)
    {
        if (tombstone.SessionId == Guid.Empty) return "replicated-tombstone-session-id-invalid";
        if (string.IsNullOrWhiteSpace(tombstone.SubjectId) ||
            string.IsNullOrWhiteSpace(tombstone.ClientInstanceId) ||
            tombstone.ClientInstanceId.Length > 128)
            return "replicated-tombstone-identity-invalid";
        if (tombstone.Generation < 1 || tombstone.AuthorityRevision < 1)
            return "replicated-tombstone-version-invalid";
        if (string.IsNullOrWhiteSpace(tombstone.ReasonCode))
            return "replicated-tombstone-reason-missing";
        if (!tombstone.ClusterId.Equals(expectedClusterId.Trim(), StringComparison.OrdinalIgnoreCase))
            return "replicated-tombstone-cluster-mismatch";
        if (!tombstone.SourceNode.Equals(expectedSourceNode.Trim(), StringComparison.OrdinalIgnoreCase))
            return "replicated-tombstone-source-mismatch";
        return null;
    }

    private static bool ReplicatedIdentityMatches(
        RuntimeSessionLeaseState left,
        RuntimeSessionLeaseState right) =>
        left.SubjectId.Equals(right.SubjectId, StringComparison.Ordinal) &&
        left.ClientInstanceId.Equals(right.ClientInstanceId, StringComparison.Ordinal) &&
        string.Equals(left.GrantedConnectionClass, right.GrantedConnectionClass, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(left.ServerNode, right.ServerNode, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(left.ClusterId, right.ClusterId, StringComparison.OrdinalIgnoreCase) &&
        left.AuthorityRevision == right.AuthorityRevision &&
        left.IssuedAtUtc == right.IssuedAtUtc &&
        left.Runtime.Matches(right.Runtime);

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string MostRestrictiveConnectionClass(string existing, string requested) =>
        IsViewOnlyConnectionClass(existing) || IsViewOnlyConnectionClass(requested)
            ? "viewer"
            : "interactive";

    private static bool IsViewOnlyConnectionClass(string value) =>
        value.Equals("viewer", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("viewonly", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("view-only", StringComparison.OrdinalIgnoreCase);

    private static async Task RollbackQuietlyAsync(NpgsqlTransaction transaction)
    {
        try { await transaction.RollbackAsync(CancellationToken.None); }
        catch { }
    }

    public ValueTask DisposeAsync() => _dataSource.DisposeAsync();
}
