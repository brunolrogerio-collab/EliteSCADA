using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using Scada.Security.Audit;

namespace Scada.Persistence.PostgreSql;

public sealed class PostgreSqlAuditStore : IAuditStore, IAsyncDisposable
{
    private const long SchemaInitializationLockKey = 4993446713136202561L;

    private const string InitializeSql = """
        CREATE SCHEMA IF NOT EXISTS elitescada;

        CREATE TABLE IF NOT EXISTS elitescada.schema_migrations (
            migration_key text PRIMARY KEY,
            applied_at_utc timestamptz NOT NULL DEFAULT clock_timestamp()
        );

        CREATE TABLE IF NOT EXISTS elitescada.audit_events (
            id uuid PRIMARY KEY,
            timestamp_utc timestamptz NOT NULL,
            subject_id varchar(300) NOT NULL,
            display_name varchar(300) NULL,
            action varchar(200) NOT NULL,
            outcome smallint NOT NULL CHECK (outcome BETWEEN 0 AND 2),
            target_kind varchar(200) NOT NULL,
            target_id varchar(600) NOT NULL,
            details jsonb NULL,
            correlation_id varchar(200) NULL
        );

        CREATE INDEX IF NOT EXISTS ix_audit_events_timestamp
            ON elitescada.audit_events (timestamp_utc DESC, id);

        CREATE INDEX IF NOT EXISTS ix_audit_events_subject_timestamp
            ON elitescada.audit_events (subject_id, timestamp_utc DESC);

        CREATE INDEX IF NOT EXISTS ix_audit_events_action_timestamp
            ON elitescada.audit_events (action, timestamp_utc DESC);

        CREATE OR REPLACE FUNCTION elitescada.reject_audit_event_mutation()
        RETURNS trigger
        LANGUAGE plpgsql
        AS $$
        BEGIN
            RAISE EXCEPTION 'EliteSCADA audit events are append-only';
        END;
        $$;

        DROP TRIGGER IF EXISTS trg_audit_events_append_only ON elitescada.audit_events;
        CREATE TRIGGER trg_audit_events_append_only
            BEFORE UPDATE OR DELETE ON elitescada.audit_events
            FOR EACH ROW EXECUTE FUNCTION elitescada.reject_audit_event_mutation();

        DROP TRIGGER IF EXISTS trg_audit_events_no_truncate ON elitescada.audit_events;
        CREATE TRIGGER trg_audit_events_no_truncate
            BEFORE TRUNCATE ON elitescada.audit_events
            FOR EACH STATEMENT EXECUTE FUNCTION elitescada.reject_audit_event_mutation();

        INSERT INTO elitescada.schema_migrations (migration_key)
        VALUES ('005_append_only_audit_events')
        ON CONFLICT (migration_key) DO NOTHING;
        """;

    private readonly NpgsqlDataSource _dataSource;

    public PostgreSqlAuditStore(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("PostgreSQL connection string is required.", nameof(connectionString));

        _dataSource = NpgsqlDataSource.Create(connectionString);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var lockCommand = new NpgsqlCommand(
                         "SELECT pg_advisory_xact_lock(@lock_key);",
                         connection,
                         transaction))
        {
            lockCommand.Parameters.AddWithValue("lock_key", SchemaInitializationLockKey);
            await lockCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var command = new NpgsqlCommand(InitializeSql, connection, transaction))
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async ValueTask WriteAsync(
        AuditEvent auditEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        Validate(auditEvent);

        const string sql = """
            INSERT INTO elitescada.audit_events (
                id,
                timestamp_utc,
                subject_id,
                display_name,
                action,
                outcome,
                target_kind,
                target_id,
                details,
                correlation_id)
            VALUES (
                @id,
                @timestamp_utc,
                @subject_id,
                @display_name,
                @action,
                @outcome,
                @target_kind,
                @target_id,
                @details,
                @correlation_id);
            """;

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("id", auditEvent.Id);
        command.Parameters.AddWithValue("timestamp_utc", auditEvent.TimestampUtc);
        command.Parameters.AddWithValue("subject_id", auditEvent.SubjectId.Trim());
        command.Parameters.AddWithValue("display_name", NpgsqlDbType.Varchar, (object?)Normalize(auditEvent.DisplayName) ?? DBNull.Value);
        command.Parameters.AddWithValue("action", auditEvent.Action.Trim());
        command.Parameters.AddWithValue("outcome", (short)auditEvent.Outcome);
        command.Parameters.AddWithValue("target_kind", auditEvent.TargetKind.Trim());
        command.Parameters.AddWithValue("target_id", auditEvent.TargetId.Trim());
        command.Parameters.AddWithValue(
            "details",
            NpgsqlDbType.Jsonb,
            auditEvent.Details is null
                ? DBNull.Value
                : JsonSerializer.Serialize(auditEvent.Details));
        command.Parameters.AddWithValue("correlation_id", NpgsqlDbType.Varchar, (object?)Normalize(auditEvent.CorrelationId) ?? DBNull.Value);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<AuditEvent>> QueryAsync(
        int limit = 100,
        string? subjectId = null,
        string? action = null,
        AuditOutcome? outcome = null,
        DateTimeOffset? fromUtc = null,
        DateTimeOffset? toUtc = null,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 1000)
            throw new ArgumentOutOfRangeException(nameof(limit), "Audit query limit must be between 1 and 1000.");
        if (fromUtc.HasValue && toUtc.HasValue && fromUtc > toUtc)
            throw new ArgumentException("Audit query fromUtc must not be later than toUtc.");

        const string sql = """
            SELECT
                id,
                timestamp_utc,
                subject_id,
                display_name,
                action,
                outcome,
                target_kind,
                target_id,
                details,
                correlation_id
            FROM elitescada.audit_events
            WHERE (@subject_id IS NULL OR subject_id = @subject_id)
              AND (@action IS NULL OR action = @action)
              AND (@outcome IS NULL OR outcome = @outcome)
              AND (@from_utc IS NULL OR timestamp_utc >= @from_utc)
              AND (@to_utc IS NULL OR timestamp_utc <= @to_utc)
            ORDER BY timestamp_utc DESC, id DESC
            LIMIT @limit;
            """;

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("subject_id", NpgsqlDbType.Varchar, (object?)Normalize(subjectId) ?? DBNull.Value);
        command.Parameters.AddWithValue("action", NpgsqlDbType.Varchar, (object?)Normalize(action) ?? DBNull.Value);
        command.Parameters.AddWithValue("outcome", NpgsqlDbType.Smallint, outcome.HasValue ? (short)outcome.Value : DBNull.Value);
        command.Parameters.AddWithValue("from_utc", NpgsqlDbType.TimestampTz, (object?)fromUtc ?? DBNull.Value);
        command.Parameters.AddWithValue("to_utc", NpgsqlDbType.TimestampTz, (object?)toUtc ?? DBNull.Value);
        command.Parameters.AddWithValue("limit", limit);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var events = new List<AuditEvent>();
        while (await reader.ReadAsync(cancellationToken))
        {
            events.Add(new AuditEvent(
                reader.GetGuid(0),
                reader.GetFieldValue<DateTimeOffset>(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.GetString(4),
                (AuditOutcome)reader.GetInt16(5),
                reader.GetString(6),
                reader.GetString(7),
                ReadDetails(reader, 8),
                reader.IsDBNull(9) ? null : reader.GetString(9)));
        }

        return events;
    }

    public async ValueTask DisposeAsync() => await _dataSource.DisposeAsync();

    private static IReadOnlyDictionary<string, string>? ReadDetails(NpgsqlDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal)) return null;
        return JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(ordinal));
    }

    private static void Validate(AuditEvent auditEvent)
    {
        if (auditEvent.Id == Guid.Empty) throw new ArgumentException("Audit event id is required.", nameof(auditEvent));
        if (string.IsNullOrWhiteSpace(auditEvent.SubjectId)) throw new ArgumentException("Audit subject id is required.", nameof(auditEvent));
        if (string.IsNullOrWhiteSpace(auditEvent.Action)) throw new ArgumentException("Audit action is required.", nameof(auditEvent));
        if (string.IsNullOrWhiteSpace(auditEvent.TargetKind)) throw new ArgumentException("Audit target kind is required.", nameof(auditEvent));
        if (string.IsNullOrWhiteSpace(auditEvent.TargetId)) throw new ArgumentException("Audit target id is required.", nameof(auditEvent));
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
