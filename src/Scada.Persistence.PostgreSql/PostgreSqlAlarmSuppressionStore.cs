using Npgsql;
using NpgsqlTypes;
using Scada.Core.Alarms;
using Scada.Core.Persistence;

namespace Scada.Persistence.PostgreSql;

/// <summary>Durable, project-scoped active alarm-suppression targets.</summary>
public sealed class PostgreSqlAlarmSuppressionStore : IAlarmSuppressionStore, IAsyncDisposable
{
    // Shared with the other elitescada infrastructure schema initializers.
    private const long InfrastructureLockKey = 4993446713136202561;
    private const string InfrastructureSql = """
        CREATE SCHEMA IF NOT EXISTS elitescada;

        CREATE TABLE IF NOT EXISTS elitescada.alarm_suppression_project_state (
            project_key text PRIMARY KEY,
            revision bigint NOT NULL DEFAULT 0,
            CHECK (length(project_key) BETWEEN 1 AND 200),
            CHECK (revision >= 0)
        );

        CREATE TABLE IF NOT EXISTS elitescada.alarm_suppression_state (
            project_key text NOT NULL REFERENCES elitescada.alarm_suppression_project_state(project_key) ON DELETE CASCADE,
            target_kind text NOT NULL CHECK (target_kind IN ('alarm', 'location')),
            target_id uuid NOT NULL CHECK (target_id <> '00000000-0000-0000-0000-000000000000'::uuid),
            reason_code text NOT NULL CHECK (reason_code IN ('maintenance', 'startup', 'testing', 'other')),
            actor text NOT NULL CHECK (length(actor) BETWEEN 1 AND 200),
            applied_at_utc timestamptz NOT NULL,
            expires_at_utc timestamptz NULL,
            version bigint NOT NULL CHECK (version > 0),
            PRIMARY KEY (project_key, target_kind, target_id),
            CHECK (expires_at_utc IS NULL OR expires_at_utc > applied_at_utc)
        );

        CREATE INDEX IF NOT EXISTS ix_alarm_suppression_expiry
            ON elitescada.alarm_suppression_state (project_key, expires_at_utc);
        """;

    private readonly NpgsqlDataSource _dataSource;
    private readonly IDurableWriteAdmission? _writeAdmission;
    private readonly Task _initializeTask;

    public PostgreSqlAlarmSuppressionStore(
        string connectionString,
        IDurableWriteAdmission? writeAdmission = null)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("PostgreSQL connection string is required.", nameof(connectionString));

        _dataSource = NpgsqlDataSource.Create(connectionString);
        _writeAdmission = writeAdmission;
        _initializeTask = InitializeAsync();
    }

    public Task EnsureInitializedAsync(CancellationToken cancellationToken = default) =>
        _initializeTask.WaitAsync(cancellationToken);

    public async Task<IReadOnlyCollection<AlarmSuppressionRecord>> ReadActiveAsync(
        string projectKey,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default) =>
        (await ReadSnapshotAsync(projectKey, nowUtc, cancellationToken)).Records;

    public async Task<AlarmSuppressionStoreSnapshot> ReadSnapshotAsync(
        string projectKey,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        ValidateProjectKey(projectKey);
        await _initializeTask.WaitAsync(cancellationToken);
        const string revisionSql = """
            SELECT revision FROM elitescada.alarm_suppression_project_state
            WHERE project_key = @project_key;
            """;
        const string recordsSql = """
            SELECT project_key, target_kind, target_id, reason_code, actor,
                   applied_at_utc, expires_at_utc, version
            FROM elitescada.alarm_suppression_state
            WHERE project_key = @project_key
              AND (expires_at_utc IS NULL OR expires_at_utc > @now_utc)
            ORDER BY target_kind, target_id;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, cancellationToken);
        long revision;
        await using (var revisionCommand = new NpgsqlCommand(revisionSql, connection, transaction))
        {
            revisionCommand.Parameters.AddWithValue("project_key", NpgsqlDbType.Text, projectKey);
            var value = await revisionCommand.ExecuteScalarAsync(cancellationToken);
            revision = value is null or DBNull ? 0 : Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture);
        }

        await using var command = new NpgsqlCommand(recordsSql, connection, transaction);
        command.Parameters.AddWithValue("project_key", NpgsqlDbType.Text, projectKey);
        command.Parameters.AddWithValue("now_utc", NpgsqlDbType.TimestampTz, nowUtc.ToUniversalTime());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var records = new List<AlarmSuppressionRecord>();
        while (await reader.ReadAsync(cancellationToken)) records.Add(ReadRecord(reader));
        await reader.DisposeAsync();
        await transaction.CommitAsync(cancellationToken);
        return new(projectKey, revision, records);
    }

    public async Task<AlarmSuppressionMutationResult> SetAsync(
        AlarmSuppressionRecord record,
        CancellationToken cancellationToken = default)
    {
        ValidateRecord(record);
        await using var durableWrite = await _writeAdmission.AcquireOptionalAsync("alarm-suppression", cancellationToken);
        await _initializeTask.WaitAsync(cancellationToken);
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await LockProjectAsync(connection, transaction, record.ProjectKey, cancellationToken);
        var previous = await ReadTargetAsync(connection, transaction, record.ProjectKey,
            record.TargetKind, record.TargetId, cancellationToken);
        var next = record with { AppliedAtUtc = record.AppliedAtUtc.ToUniversalTime(), Version = (previous?.Version ?? 0) + 1 };

        const string sql = """
            INSERT INTO elitescada.alarm_suppression_state (
                project_key, target_kind, target_id, reason_code, actor,
                applied_at_utc, expires_at_utc, version)
            VALUES (@project_key, @target_kind, @target_id, @reason_code, @actor,
                    @applied_at_utc, @expires_at_utc, @version)
            ON CONFLICT (project_key, target_kind, target_id) DO UPDATE SET
                reason_code = EXCLUDED.reason_code,
                actor = EXCLUDED.actor,
                applied_at_utc = EXCLUDED.applied_at_utc,
                expires_at_utc = EXCLUDED.expires_at_utc,
                version = EXCLUDED.version;
            UPDATE elitescada.alarm_suppression_project_state
            SET revision = revision + 1 WHERE project_key = @project_key;
            """;
        await using (var command = new NpgsqlCommand(sql, connection, transaction))
        {
            AddRecordParameters(command, next);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new(true, previous, next);
    }

    public async Task<AlarmSuppressionMutationResult> ReleaseAsync(
        string projectKey,
        AlarmSuppressionTargetKind targetKind,
        Guid targetId,
        CancellationToken cancellationToken = default)
    {
        ValidateProjectKey(projectKey);
        if (!Enum.IsDefined(targetKind) || targetId == Guid.Empty)
            throw new ArgumentException("Alarm suppression target is invalid.");
        await using var durableWrite = await _writeAdmission.AcquireOptionalAsync("alarm-suppression", cancellationToken);
        await _initializeTask.WaitAsync(cancellationToken);
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await LockProjectAsync(connection, transaction, projectKey, cancellationToken);
        var previous = await ReadTargetAsync(connection, transaction, projectKey, targetKind, targetId, cancellationToken);
        if (previous is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return new(false, null, null);
        }

        const string sql = """
            DELETE FROM elitescada.alarm_suppression_state
            WHERE project_key = @project_key AND target_kind = @target_kind AND target_id = @target_id;
            UPDATE elitescada.alarm_suppression_project_state
            SET revision = revision + 1 WHERE project_key = @project_key;
            """;
        await using (var command = new NpgsqlCommand(sql, connection, transaction))
        {
            command.Parameters.AddWithValue("project_key", NpgsqlDbType.Text, projectKey);
            command.Parameters.AddWithValue("target_kind", NpgsqlDbType.Text, ToDatabase(targetKind));
            command.Parameters.AddWithValue("target_id", NpgsqlDbType.Uuid, targetId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new(true, previous, null);
    }

    public async Task ReplaceProjectSnapshotAsync(
        string projectKey,
        long revision,
        IReadOnlyCollection<AlarmSuppressionRecord> records,
        CancellationToken cancellationToken = default)
    {
        ValidateProjectKey(projectKey);
        if (revision < 0) throw new ArgumentOutOfRangeException(nameof(revision));
        ArgumentNullException.ThrowIfNull(records);
        foreach (var record in records)
        {
            ValidateRecord(record);
            if (!record.ProjectKey.Equals(projectKey, StringComparison.Ordinal))
                throw new ArgumentException("Suppression snapshot contains a different project key.", nameof(records));
        }

        await using var durableWrite = await _writeAdmission.AcquireOptionalAsync("alarm-suppression-replication", cancellationToken);
        await _initializeTask.WaitAsync(cancellationToken);
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await LockProjectAsync(connection, transaction, projectKey, cancellationToken);

        await using (var currentRevisionCommand = new NpgsqlCommand(
                         "SELECT revision FROM elitescada.alarm_suppression_project_state WHERE project_key = @project_key;",
                         connection, transaction))
        {
            currentRevisionCommand.Parameters.AddWithValue("project_key", NpgsqlDbType.Text, projectKey);
            var currentRevision = Convert.ToInt64(
                await currentRevisionCommand.ExecuteScalarAsync(cancellationToken),
                System.Globalization.CultureInfo.InvariantCulture);
            if (revision < currentRevision)
                throw new InvalidOperationException("Alarm suppression snapshot is older than the durable local state.");
        }

        await using (var delete = new NpgsqlCommand(
                         "DELETE FROM elitescada.alarm_suppression_state WHERE project_key = @project_key;",
                         connection, transaction))
        {
            delete.Parameters.AddWithValue("project_key", NpgsqlDbType.Text, projectKey);
            await delete.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var record in records)
        {
            const string insertSql = """
                INSERT INTO elitescada.alarm_suppression_state (
                    project_key, target_kind, target_id, reason_code, actor,
                    applied_at_utc, expires_at_utc, version)
                VALUES (@project_key, @target_kind, @target_id, @reason_code, @actor,
                        @applied_at_utc, @expires_at_utc, @version);
                """;
            await using var insert = new NpgsqlCommand(insertSql, connection, transaction);
            AddRecordParameters(insert, record);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var updateRevision = new NpgsqlCommand("""
                         UPDATE elitescada.alarm_suppression_project_state
                         SET revision = @revision WHERE project_key = @project_key;
                         """, connection, transaction))
        {
            updateRevision.Parameters.AddWithValue("project_key", NpgsqlDbType.Text, projectKey);
            updateRevision.Parameters.AddWithValue("revision", NpgsqlDbType.Bigint, revision);
            await updateRevision.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private async Task InitializeAsync()
    {
        await using var connection = await _dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using (var lockCommand = new NpgsqlCommand("SELECT pg_advisory_xact_lock(@lock_key);", connection, transaction))
        {
            lockCommand.Parameters.AddWithValue("lock_key", NpgsqlDbType.Bigint, InfrastructureLockKey);
            await lockCommand.ExecuteNonQueryAsync();
        }
        await using (var command = new NpgsqlCommand(InfrastructureSql, connection, transaction))
            await command.ExecuteNonQueryAsync();
        await transaction.CommitAsync();
    }

    private static async Task LockProjectAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string projectKey,
        CancellationToken cancellationToken)
    {
        await using var insert = new NpgsqlCommand("""
            INSERT INTO elitescada.alarm_suppression_project_state (project_key, revision)
            VALUES (@project_key, 0) ON CONFLICT (project_key) DO NOTHING;
            """, connection, transaction);
        insert.Parameters.AddWithValue("project_key", NpgsqlDbType.Text, projectKey);
        await insert.ExecuteNonQueryAsync(cancellationToken);

        await using var lockRow = new NpgsqlCommand("""
            SELECT revision FROM elitescada.alarm_suppression_project_state
            WHERE project_key = @project_key FOR UPDATE;
            """, connection, transaction);
        lockRow.Parameters.AddWithValue("project_key", NpgsqlDbType.Text, projectKey);
        _ = await lockRow.ExecuteScalarAsync(cancellationToken);
    }

    private static async Task<AlarmSuppressionRecord?> ReadTargetAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string projectKey,
        AlarmSuppressionTargetKind targetKind,
        Guid targetId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT project_key, target_kind, target_id, reason_code, actor,
                   applied_at_utc, expires_at_utc, version
            FROM elitescada.alarm_suppression_state
            WHERE project_key = @project_key AND target_kind = @target_kind AND target_id = @target_id;
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("project_key", NpgsqlDbType.Text, projectKey);
        command.Parameters.AddWithValue("target_kind", NpgsqlDbType.Text, ToDatabase(targetKind));
        command.Parameters.AddWithValue("target_id", NpgsqlDbType.Uuid, targetId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadRecord(reader) : null;
    }

    private static AlarmSuppressionRecord ReadRecord(NpgsqlDataReader reader) => new(
        reader.GetString(0),
        FromDatabase(reader.GetString(1)),
        reader.GetGuid(2),
        reader.GetString(3),
        reader.GetString(4),
        reader.GetFieldValue<DateTimeOffset>(5),
        reader.IsDBNull(6) ? null : reader.GetFieldValue<DateTimeOffset>(6),
        reader.GetInt64(7));

    private static void AddRecordParameters(NpgsqlCommand command, AlarmSuppressionRecord record)
    {
        command.Parameters.AddWithValue("project_key", NpgsqlDbType.Text, record.ProjectKey);
        command.Parameters.AddWithValue("target_kind", NpgsqlDbType.Text, ToDatabase(record.TargetKind));
        command.Parameters.AddWithValue("target_id", NpgsqlDbType.Uuid, record.TargetId);
        command.Parameters.AddWithValue("reason_code", NpgsqlDbType.Text, record.ReasonCode);
        command.Parameters.AddWithValue("actor", NpgsqlDbType.Text, record.Actor);
        command.Parameters.AddWithValue("applied_at_utc", NpgsqlDbType.TimestampTz, record.AppliedAtUtc.ToUniversalTime());
        command.Parameters.AddWithValue("expires_at_utc", NpgsqlDbType.TimestampTz, (object?)record.ExpiresAtUtc?.ToUniversalTime() ?? DBNull.Value);
        command.Parameters.AddWithValue("version", NpgsqlDbType.Bigint, Math.Max(1, record.Version));
    }

    private static string ToDatabase(AlarmSuppressionTargetKind targetKind) => targetKind switch
    {
        AlarmSuppressionTargetKind.Alarm => "alarm",
        AlarmSuppressionTargetKind.Location => "location",
        _ => throw new ArgumentOutOfRangeException(nameof(targetKind))
    };

    private static AlarmSuppressionTargetKind FromDatabase(string value) => value switch
    {
        "alarm" => AlarmSuppressionTargetKind.Alarm,
        "location" => AlarmSuppressionTargetKind.Location,
        _ => throw new InvalidOperationException("Stored alarm suppression target kind is invalid.")
    };

    private static void ValidateRecord(AlarmSuppressionRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        ValidateProjectKey(record.ProjectKey);
        if (!Enum.IsDefined(record.TargetKind) || record.TargetId == Guid.Empty)
            throw new ArgumentException("Alarm suppression target is invalid.", nameof(record));
        if (!AlarmSuppressionReasonCodes.IsValid(record.ReasonCode))
            throw new ArgumentException("Alarm suppression reason code is invalid.", nameof(record));
        if (string.IsNullOrWhiteSpace(record.Actor) || record.Actor.Trim().Length > 200)
            throw new ArgumentException("Alarm suppression actor is invalid.", nameof(record));
        if (record.ExpiresAtUtc.HasValue && record.ExpiresAtUtc.Value.ToUniversalTime() <= record.AppliedAtUtc.ToUniversalTime())
            throw new ArgumentException("Alarm suppression expiry must be later than its activation.", nameof(record));
    }

    private static void ValidateProjectKey(string projectKey)
    {
        if (string.IsNullOrWhiteSpace(projectKey) || projectKey.Trim().Length > 200)
            throw new ArgumentException("Alarm suppression project key is invalid.", nameof(projectKey));
    }

    public ValueTask DisposeAsync() => _dataSource.DisposeAsync();
}
