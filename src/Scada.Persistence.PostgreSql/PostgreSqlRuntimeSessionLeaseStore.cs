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
    private const long SharedDdlAdvisoryLock = 4993446713136202561;
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
            SELECT pg_advisory_xact_lock(@ddl_lock);
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
                CHECK (expires_at_utc >= last_heartbeat_utc));
            CREATE UNIQUE INDEX IF NOT EXISTS ux_runtime_session_leases_active_logical_identity
                ON elitescada.runtime_session_leases (subject_id, client_instance_id)
                WHERE is_active;
            CREATE INDEX IF NOT EXISTS ix_runtime_session_leases_active_expiry
                ON elitescada.runtime_session_leases (expires_at_utc)
                WHERE is_active;
            INSERT INTO elitescada.schema_migrations (migration_key)
            VALUES ('022_runtime_session_lease_v1')
            ON CONFLICT (migration_key) DO NOTHING;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("ddl_lock", NpgsqlDbType.Bigint, SharedDdlAdvisoryLock);
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
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
            var subjectId = admission.SubjectId.Trim();
            var clientInstanceId = admission.ClientInstanceId.Trim();
            var now = DateTimeOffset.UtcNow;
            await DeactivateExpiredForIdentityAsync(connection, transaction, subjectId, clientInstanceId, now, cancellationToken);
            var active = await LoadActiveByIdentityAsync(connection, transaction, subjectId, clientInstanceId, cancellationToken);
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
                true);
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
            var subjectId = admission.SubjectId.Trim();
            var clientInstanceId = admission.ClientInstanceId.Trim();
            var now = DateTimeOffset.UtcNow;
            await DeactivateExpiredAsync(connection, transaction, now, cancellationToken);
            var active = await LoadActiveByIdentityAsync(connection, transaction, subjectId, clientInstanceId, cancellationToken);

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
                NormalizeOptional(admission.ServerNode), NormalizeOptional(admission.ClusterId), true);
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
            var result = await ValidateForMutationAsync(connection, transaction, sessionId, subjectId, runtime, clientInstanceId, cancellationToken);
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
            var validation = await ValidateForMutationAsync(connection, transaction, sessionId, subjectId, runtime, clientInstanceId, cancellationToken);
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
            var validation = await ValidateForMutationAsync(connection, transaction, sessionId, subjectId, runtime, clientInstanceId, cancellationToken);
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
        Guid sessionId,
        string subjectId,
        RuntimeSessionRuntimeIdentity runtime,
        string? clientInstanceId,
        CancellationToken cancellationToken)
    {
        if (sessionId == Guid.Empty) return RuntimeSessionLeaseStoreResult.Invalid("invalid-session-id");
        if (string.IsNullOrWhiteSpace(subjectId)) return RuntimeSessionLeaseStoreResult.Invalid("missing-user");
        ArgumentNullException.ThrowIfNull(runtime);

        var lease = await LoadByIdForUpdateAsync(connection, transaction, sessionId, cancellationToken);
        if (lease is null || !lease.IsActive) return RuntimeSessionLeaseStoreResult.Invalid("session-not-found");

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
                   runtime_revision, runtime_activated_at_utc, server_node, cluster_id, is_active
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
                   runtime_revision, runtime_activated_at_utc, server_node, cluster_id, is_active
            FROM elitescada.runtime_session_leases WHERE session_id = @session_id FOR UPDATE;
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("session_id", sessionId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Read(reader) : null;
    }

    private static async Task InsertAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, RuntimeSessionLeaseState lease, TimeSpan duration, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO elitescada.runtime_session_leases (
                session_id, subject_id, client_instance_id, requested_connection_class, generation,
                issued_at_utc, last_heartbeat_utc, expires_at_utc, lease_duration, runtime_mode,
                runtime_project_key, runtime_revision, runtime_activated_at_utc, server_node, cluster_id, is_active)
            VALUES (
                @session_id, @subject_id, @client_instance_id, @requested_connection_class, @generation,
                @issued_at_utc, @last_heartbeat_utc, @expires_at_utc, @lease_duration, @runtime_mode,
                @runtime_project_key, @runtime_revision, @runtime_activated_at_utc, @server_node, @cluster_id, true);
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        BindLease(command, lease);
        command.Parameters.AddWithValue("lease_duration", NpgsqlDbType.Interval, duration);
        await command.ExecuteNonQueryAsync(cancellationToken);
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
        reader.GetBoolean(14));

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
