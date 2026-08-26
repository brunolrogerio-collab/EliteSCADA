using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using Scada.Security.Audit;

namespace Scada.Persistence.PostgreSql;

public sealed class PostgreSqlAuditStore : IAuditStore, IAsyncDisposable
{
    private const string InitializeSql = """
        SELECT pg_advisory_xact_lock(4993446713136202561);

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
            correlation_id varchar(200) NULL,
            area varchar(300) NULL,
            project_key varchar(300) NULL,
            revision_number bigint NULL,
            roles text[] NULL,
            source varchar(300) NULL
        );

        ALTER TABLE elitescada.audit_events ADD COLUMN IF NOT EXISTS area varchar(300) NULL;
        ALTER TABLE elitescada.audit_events ADD COLUMN IF NOT EXISTS project_key varchar(300) NULL;
        ALTER TABLE elitescada.audit_events ADD COLUMN IF NOT EXISTS revision_number bigint NULL;
        ALTER TABLE elitescada.audit_events ADD COLUMN IF NOT EXISTS roles text[] NULL;
        ALTER TABLE elitescada.audit_events ADD COLUMN IF NOT EXISTS source varchar(300) NULL;

        CREATE INDEX IF NOT EXISTS ix_audit_events_timestamp_id_desc
            ON elitescada.audit_events (timestamp_utc DESC, id DESC);

        CREATE INDEX IF NOT EXISTS ix_audit_events_subject_timestamp
            ON elitescada.audit_events (subject_id, timestamp_utc DESC, id DESC);

        CREATE INDEX IF NOT EXISTS ix_audit_events_action_timestamp
            ON elitescada.audit_events (action, timestamp_utc DESC, id DESC);

        CREATE INDEX IF NOT EXISTS ix_audit_events_outcome_timestamp
            ON elitescada.audit_events (outcome, timestamp_utc DESC, id DESC);

        CREATE INDEX IF NOT EXISTS ix_audit_events_target_timestamp
            ON elitescada.audit_events (target_kind, target_id, timestamp_utc DESC, id DESC);

        CREATE INDEX IF NOT EXISTS ix_audit_events_area_timestamp
            ON elitescada.audit_events (area, timestamp_utc DESC, id DESC)
            WHERE area IS NOT NULL;

        CREATE INDEX IF NOT EXISTS ix_audit_events_correlation
            ON elitescada.audit_events (correlation_id)
            WHERE correlation_id IS NOT NULL;

        CREATE OR REPLACE FUNCTION elitescada.reject_audit_event_mutation()
        RETURNS trigger
        LANGUAGE plpgsql
        AS $$
        BEGIN
            IF TG_OP = 'DELETE'
               AND current_setting('elitescada.audit_retention_delete', true) = 'on' THEN
                RETURN OLD;
            END IF;

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

        INSERT INTO elitescada.schema_migrations (migration_key)
        VALUES ('007_audit_retention_query_foundation')
        ON CONFLICT (migration_key) DO NOTHING;
        """;

    private readonly NpgsqlDataSource _dataSource;
    private readonly AuditQueryPolicy _queryPolicy;
    private long _persistedCount;
    private long _appendFailureCount;
    private long _lastPersistedUtcTicks;
    private long _lastAppendFailureUtcTicks;
    private long _lastRetentionRunUtcTicks;
    private int _lastRetentionDeletedCount;

    public PostgreSqlAuditStore(string connectionString, AuditQueryPolicy? queryPolicy = null)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("PostgreSQL connection string is required.", nameof(connectionString));

        _queryPolicy = queryPolicy ?? new AuditQueryPolicy();
        _queryPolicy.Validate();
        _dataSource = NpgsqlDataSource.Create(connectionString);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(InitializeSql);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async ValueTask WriteAsync(
        AuditEvent auditEvent,
        CancellationToken cancellationToken = default)
    {
        var normalized = AuditSanitizer.Normalize(auditEvent);
        Validate(normalized);

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
                correlation_id,
                area,
                project_key,
                revision_number,
                roles,
                source)
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
                @correlation_id,
                @area,
                @project_key,
                @revision_number,
                @roles,
                @source);
            """;

        try
        {
            await using var command = _dataSource.CreateCommand(sql);
            command.Parameters.AddWithValue("id", normalized.Id);
            command.Parameters.AddWithValue("timestamp_utc", normalized.TimestampUtc);
            command.Parameters.AddWithValue("subject_id", normalized.SubjectId);
            command.Parameters.AddWithValue("display_name", NpgsqlDbType.Varchar, (object?)normalized.DisplayName ?? DBNull.Value);
            command.Parameters.AddWithValue("action", normalized.Action);
            command.Parameters.AddWithValue("outcome", (short)normalized.Outcome);
            command.Parameters.AddWithValue("target_kind", normalized.TargetKind);
            command.Parameters.AddWithValue("target_id", normalized.TargetId);
            command.Parameters.AddWithValue(
                "details",
                NpgsqlDbType.Jsonb,
                normalized.Details is null
                    ? DBNull.Value
                    : JsonSerializer.Serialize(normalized.Details));
            command.Parameters.AddWithValue("correlation_id", NpgsqlDbType.Varchar, (object?)normalized.CorrelationId ?? DBNull.Value);
            command.Parameters.AddWithValue("area", NpgsqlDbType.Varchar, (object?)normalized.Area ?? DBNull.Value);
            command.Parameters.AddWithValue("project_key", NpgsqlDbType.Varchar, (object?)normalized.ProjectKey ?? DBNull.Value);
            command.Parameters.AddWithValue("revision_number", NpgsqlDbType.Bigint, normalized.Revision.HasValue ? normalized.Revision.Value : DBNull.Value);
            command.Parameters.AddWithValue(
                "roles",
                NpgsqlDbType.Array | NpgsqlDbType.Text,
                normalized.Roles is null ? DBNull.Value : normalized.Roles.ToArray());
            command.Parameters.AddWithValue("source", NpgsqlDbType.Varchar, (object?)normalized.Source ?? DBNull.Value);

            await command.ExecuteNonQueryAsync(cancellationToken);
            Interlocked.Increment(ref _persistedCount);
            Interlocked.Exchange(ref _lastPersistedUtcTicks, DateTimeOffset.UtcNow.UtcTicks);
        }
        catch
        {
            Interlocked.Increment(ref _appendFailureCount);
            Interlocked.Exchange(ref _lastAppendFailureUtcTicks, DateTimeOffset.UtcNow.UtcTicks);
            throw;
        }
    }

    public async Task<AuditPage> QueryPageAsync(
        AuditQuery query,
        CancellationToken cancellationToken = default)
    {
        var normalized = AuditQueryValidator.ValidateAndNormalize(query, _queryPolicy);

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
                correlation_id,
                area,
                project_key,
                revision_number,
                roles,
                source
            FROM elitescada.audit_events
            WHERE (@subject_id IS NULL OR subject_id = @subject_id)
              AND (@action IS NULL OR action = @action)
              AND (@outcome IS NULL OR outcome = @outcome)
              AND (@target_kind IS NULL OR target_kind = @target_kind)
              AND (@target_id IS NULL OR target_id = @target_id)
              AND (@area IS NULL OR area = @area)
              AND (@correlation_id IS NULL OR correlation_id = @correlation_id)
              AND (@from_utc IS NULL OR timestamp_utc >= @from_utc)
              AND (@to_utc IS NULL OR timestamp_utc <= @to_utc)
              AND (
                    @after_timestamp IS NULL
                    OR timestamp_utc < @after_timestamp
                    OR (timestamp_utc = @after_timestamp AND id < @after_id))
            ORDER BY timestamp_utc DESC, id DESC
            LIMIT @fetch_limit;
            """;

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("subject_id", NpgsqlDbType.Varchar, (object?)normalized.SubjectId ?? DBNull.Value);
        command.Parameters.AddWithValue("action", NpgsqlDbType.Varchar, (object?)normalized.Action ?? DBNull.Value);
        command.Parameters.AddWithValue("outcome", NpgsqlDbType.Smallint, normalized.Outcome.HasValue ? (short)normalized.Outcome.Value : DBNull.Value);
        command.Parameters.AddWithValue("target_kind", NpgsqlDbType.Varchar, (object?)normalized.TargetKind ?? DBNull.Value);
        command.Parameters.AddWithValue("target_id", NpgsqlDbType.Varchar, (object?)normalized.TargetId ?? DBNull.Value);
        command.Parameters.AddWithValue("area", NpgsqlDbType.Varchar, (object?)normalized.Area ?? DBNull.Value);
        command.Parameters.AddWithValue("correlation_id", NpgsqlDbType.Varchar, (object?)normalized.CorrelationId ?? DBNull.Value);
        command.Parameters.AddWithValue("from_utc", NpgsqlDbType.TimestampTz, (object?)normalized.FromUtc ?? DBNull.Value);
        command.Parameters.AddWithValue("to_utc", NpgsqlDbType.TimestampTz, (object?)normalized.ToUtc ?? DBNull.Value);
        command.Parameters.AddWithValue("after_timestamp", NpgsqlDbType.TimestampTz, (object?)normalized.After?.TimestampUtc ?? DBNull.Value);
        command.Parameters.AddWithValue("after_id", NpgsqlDbType.Uuid, normalized.After is null ? DBNull.Value : normalized.After.Id);
        command.Parameters.AddWithValue("fetch_limit", normalized.PageSize + 1);

        var events = new List<AuditEvent>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            events.Add(Read(reader));

        var hasMore = events.Count > normalized.PageSize;
        if (hasMore) events.RemoveAt(events.Count - 1);
        var nextCursor = hasMore && events.Count > 0
            ? new AuditCursor(events[^1].TimestampUtc, events[^1].Id)
            : null;

        return new AuditPage(events, nextCursor);
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
        var page = await QueryPageAsync(
            new AuditQuery(
                PageSize: limit,
                FromUtc: fromUtc,
                ToUtc: toUtc,
                SubjectId: subjectId,
                Action: action,
                Outcome: outcome),
            cancellationToken);
        return page.Events;
    }

    public async Task<int> ApplyRetentionBatchAsync(
        DateTimeOffset cutoffUtc,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        if (batchSize is < 1 or > 100000)
            throw new ArgumentOutOfRangeException(nameof(batchSize), "Audit retention batch size must be between 1 and 100000.");

        const string sql = """
            SET LOCAL elitescada.audit_retention_delete = 'on';

            WITH candidates AS (
                SELECT id
                FROM elitescada.audit_events
                WHERE timestamp_utc < @cutoff_utc
                ORDER BY timestamp_utc, id
                LIMIT @batch_size
                FOR UPDATE SKIP LOCKED
            )
            DELETE FROM elitescada.audit_events AS audit
            USING candidates
            WHERE audit.id = candidates.id;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("cutoff_utc", cutoffUtc.ToUniversalTime());
        command.Parameters.AddWithValue("batch_size", batchSize);
        var deleted = await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        Interlocked.Exchange(ref _lastRetentionRunUtcTicks, DateTimeOffset.UtcNow.UtcTicks);
        Volatile.Write(ref _lastRetentionDeletedCount, deleted);
        return deleted;
    }

    public AuditStoreHealthSnapshot GetHealthSnapshot() => new(
        Interlocked.Read(ref _persistedCount),
        Interlocked.Read(ref _appendFailureCount),
        ReadTimestamp(ref _lastPersistedUtcTicks),
        ReadTimestamp(ref _lastAppendFailureUtcTicks),
        ReadTimestamp(ref _lastRetentionRunUtcTicks),
        Volatile.Read(ref _lastRetentionDeletedCount));

    private static AuditEvent Read(NpgsqlDataReader reader)
    {
        IReadOnlyDictionary<string, string>? details = null;
        if (!reader.IsDBNull(8))
        {
            details = JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(8))
                ?? new Dictionary<string, string>();
        }

        return new AuditEvent(
            reader.GetGuid(0),
            reader.GetFieldValue<DateTimeOffset>(1),
            reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.GetString(4),
            (AuditOutcome)reader.GetInt16(5),
            reader.GetString(6),
            reader.GetString(7),
            details,
            reader.IsDBNull(9) ? null : reader.GetString(9),
            reader.IsDBNull(10) ? null : reader.GetString(10),
            reader.IsDBNull(11) ? null : reader.GetString(11),
            reader.IsDBNull(12) ? null : reader.GetInt64(12),
            reader.IsDBNull(13) ? null : reader.GetFieldValue<string[]>(13),
            reader.IsDBNull(14) ? null : reader.GetString(14));
    }

    private static void Validate(AuditEvent auditEvent)
    {
        if (auditEvent.Id == Guid.Empty)
            throw new ArgumentException("Audit event ID is required.", nameof(auditEvent));
        ValidateRequired(auditEvent.SubjectId, 300, "Audit subject ID");
        ValidateOptional(auditEvent.DisplayName, 300, "Audit display name");
        ValidateRequired(auditEvent.Action, 200, "Audit action");
        ValidateRequired(auditEvent.TargetKind, 200, "Audit target kind");
        ValidateRequired(auditEvent.TargetId, 600, "Audit target ID");
        ValidateOptional(auditEvent.CorrelationId, 200, "Audit correlation ID");
        ValidateOptional(auditEvent.Area, 300, "Audit area");
        ValidateOptional(auditEvent.ProjectKey, 300, "Audit project key");
        ValidateOptional(auditEvent.Source, 300, "Audit source");
        if (!Enum.IsDefined(auditEvent.Outcome))
            throw new ArgumentOutOfRangeException(nameof(auditEvent), "Audit outcome is invalid.");
        if (auditEvent.Revision.HasValue && auditEvent.Revision.Value < 1)
            throw new ArgumentOutOfRangeException(nameof(auditEvent), "Audit revision must be positive when present.");
        if (auditEvent.Roles is not null && auditEvent.Roles.Any(role => role.Length > 200))
            throw new ArgumentException("Audit role keys cannot exceed 200 characters.", nameof(auditEvent));
    }

    private static void ValidateRequired(string value, int maximumLength, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{label} is required.");
        if (value.Length > maximumLength)
            throw new ArgumentException($"{label} cannot exceed {maximumLength} characters.");
    }

    private static void ValidateOptional(string? value, int maximumLength, string label)
    {
        if (value is not null && value.Length > maximumLength)
            throw new ArgumentException($"{label} cannot exceed {maximumLength} characters.");
    }

    private static DateTimeOffset? ReadTimestamp(ref long ticks)
    {
        var value = Interlocked.Read(ref ticks);
        return value == 0 ? null : new DateTimeOffset(value, TimeSpan.Zero);
    }

    public ValueTask DisposeAsync() => _dataSource.DisposeAsync();
}
