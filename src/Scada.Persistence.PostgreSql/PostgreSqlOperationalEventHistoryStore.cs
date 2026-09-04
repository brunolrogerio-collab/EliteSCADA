using System.Text;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using Scada.Core.Events;
using Scada.Core.HistoricalQueries;

namespace Scada.Persistence.PostgreSql;

public sealed class PostgreSqlOperationalEventHistoryStore : IHistoricalDatasetProvider, IAsyncDisposable
{
    private const long InfrastructureLockKey = 4993446713136202562;
    private const string InfrastructureSql = """
        CREATE SCHEMA IF NOT EXISTS elitescada;

        CREATE TABLE IF NOT EXISTS elitescada.operational_event_history (
            event_id uuid PRIMARY KEY,
            definition_id uuid NOT NULL,
            definition_key text NOT NULL,
            event_type text NOT NULL,
            category text NOT NULL,
            source text NOT NULL,
            area text NULL,
            equipment_path text NULL,
            tag_id uuid NULL,
            tag_path text NULL,
            operator_name text NULL,
            operation text NULL,
            command_id uuid NULL,
            command_key text NULL,
            message text NULL,
            context_json jsonb NOT NULL DEFAULT '{}'::jsonb,
            timestamp_utc timestamptz NOT NULL
        );

        CREATE INDEX IF NOT EXISTS ix_operational_event_history_timestamp_event_desc
            ON elitescada.operational_event_history (timestamp_utc DESC, event_id DESC);
        CREATE INDEX IF NOT EXISTS ix_operational_event_history_definition_timestamp
            ON elitescada.operational_event_history (definition_id, timestamp_utc DESC, event_id DESC);
        CREATE INDEX IF NOT EXISTS ix_operational_event_history_type_timestamp
            ON elitescada.operational_event_history (event_type, timestamp_utc DESC, event_id DESC);
        CREATE INDEX IF NOT EXISTS ix_operational_event_history_category_timestamp
            ON elitescada.operational_event_history (category, timestamp_utc DESC, event_id DESC);
        CREATE INDEX IF NOT EXISTS ix_operational_event_history_source_timestamp
            ON elitescada.operational_event_history (source, timestamp_utc DESC, event_id DESC);
        CREATE INDEX IF NOT EXISTS ix_operational_event_history_area_timestamp
            ON elitescada.operational_event_history (area, timestamp_utc DESC, event_id DESC);
        CREATE INDEX IF NOT EXISTS ix_operational_event_history_tag_timestamp
            ON elitescada.operational_event_history (tag_id, timestamp_utc DESC, event_id DESC);

        CREATE OR REPLACE FUNCTION elitescada.reject_operational_event_history_mutation()
        RETURNS trigger
        LANGUAGE plpgsql
        AS $$
        BEGIN
            RAISE EXCEPTION 'EliteSCADA operational event history is append-only';
        END;
        $$;

        DROP TRIGGER IF EXISTS trg_operational_event_history_append_only ON elitescada.operational_event_history;
        CREATE TRIGGER trg_operational_event_history_append_only
            BEFORE UPDATE OR DELETE ON elitescada.operational_event_history
            FOR EACH ROW EXECUTE FUNCTION elitescada.reject_operational_event_history_mutation();

        DROP TRIGGER IF EXISTS trg_operational_event_history_no_truncate ON elitescada.operational_event_history;
        CREATE TRIGGER trg_operational_event_history_no_truncate
            BEFORE TRUNCATE ON elitescada.operational_event_history
            FOR EACH STATEMENT EXECUTE FUNCTION elitescada.reject_operational_event_history_mutation();
        """;

    private readonly NpgsqlDataSource _dataSource;
    private readonly Task _initializeTask;

    public PostgreSqlOperationalEventHistoryStore(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("PostgreSQL connection string is required.", nameof(connectionString));

        _dataSource = NpgsqlDataSource.Create(connectionString);
        _initializeTask = InitializeAsync();
    }

    public string Dataset => HistoricalDatasets.OperationalEvents;

    public async Task AppendAsync(
        OperationalEventOccurred occurrence,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(occurrence);
        if (occurrence.EventId == Guid.Empty || occurrence.DefinitionId == Guid.Empty)
            throw new ArgumentException("Operational Event history requires stable event and definition identities.", nameof(occurrence));

        await _initializeTask.WaitAsync(cancellationToken);
        const string sql = """
            INSERT INTO elitescada.operational_event_history (
                event_id, definition_id, definition_key, event_type, category, source,
                area, equipment_path, tag_id, tag_path, operator_name, operation,
                command_id, command_key, message, context_json, timestamp_utc)
            VALUES (
                @event_id, @definition_id, @definition_key, @event_type, @category, @source,
                @area, @equipment_path, @tag_id, @tag_path, @operator_name, @operation,
                @command_id, @command_key, @message, @context_json, @timestamp_utc);
            """;

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("event_id", NpgsqlDbType.Uuid, occurrence.EventId);
        command.Parameters.AddWithValue("definition_id", NpgsqlDbType.Uuid, occurrence.DefinitionId);
        command.Parameters.AddWithValue("definition_key", NpgsqlDbType.Text, occurrence.DefinitionKey);
        command.Parameters.AddWithValue("event_type", NpgsqlDbType.Text, occurrence.Type);
        command.Parameters.AddWithValue("category", NpgsqlDbType.Text, occurrence.Category);
        command.Parameters.AddWithValue("source", NpgsqlDbType.Text, occurrence.Source);
        AddNullable(command, "area", NpgsqlDbType.Text, occurrence.Area);
        AddNullable(command, "equipment_path", NpgsqlDbType.Text, occurrence.EquipmentPath);
        AddNullable(command, "tag_id", NpgsqlDbType.Uuid, occurrence.TagId);
        AddNullable(command, "tag_path", NpgsqlDbType.Text, occurrence.TagPath);
        AddNullable(command, "operator_name", NpgsqlDbType.Text, occurrence.Operator);
        AddNullable(command, "operation", NpgsqlDbType.Text, occurrence.Operation);
        AddNullable(command, "command_id", NpgsqlDbType.Uuid, occurrence.CommandId);
        AddNullable(command, "command_key", NpgsqlDbType.Text, occurrence.CommandKey);
        AddNullable(command, "message", NpgsqlDbType.Text, occurrence.Message);
        command.Parameters.AddWithValue(
            "context_json",
            NpgsqlDbType.Jsonb,
            JsonSerializer.Serialize(occurrence.Context));
        command.Parameters.AddWithValue("timestamp_utc", NpgsqlDbType.TimestampTz, occurrence.OccurredAt.ToUniversalTime());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<HistoricalProviderPage> QueryAsync(
        HistoricalQueryExecution query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (!string.Equals(query.Dataset.Id, Dataset, StringComparison.Ordinal))
            throw new ArgumentException("Operational Event historical provider received the wrong dataset.", nameof(query));
        if (!string.Equals(query.Sort.Field, "timestamp", StringComparison.Ordinal))
            throw new ArgumentException("Operational Event history currently supports timestamp ordering only.", nameof(query));

        await _initializeTask.WaitAsync(cancellationToken);
        var sql = new StringBuilder("""
            SELECT event_id, definition_id, definition_key, event_type, category, source,
                   area, equipment_path, tag_id, tag_path, operator_name, operation,
                   command_id, command_key, message, context_json::text, timestamp_utc
            FROM elitescada.operational_event_history
            WHERE timestamp_utc >= @from_utc AND timestamp_utc <= @to_utc
            """);
        await using var command = _dataSource.CreateCommand();
        command.Parameters.AddWithValue("from_utc", NpgsqlDbType.TimestampTz, query.Range.FromUtc);
        command.Parameters.AddWithValue("to_utc", NpgsqlDbType.TimestampTz, query.Range.ToUtc);

        var parameterIndex = 0;
        foreach (var filter in query.Filters)
            sql.Append(" AND ").Append(BuildFilter(filter, command, ref parameterIndex));

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var name = $"p{parameterIndex++}";
            command.Parameters.AddWithValue(name, NpgsqlDbType.Text, query.Search);
            sql.Append($" AND (position(lower(@{name}) in lower(definition_key)) > 0");
            sql.Append($" OR position(lower(@{name}) in lower(event_type)) > 0");
            sql.Append($" OR position(lower(@{name}) in lower(category)) > 0");
            sql.Append($" OR position(lower(@{name}) in lower(source)) > 0");
            sql.Append($" OR position(lower(@{name}) in lower(COALESCE(area, ''))) > 0");
            sql.Append($" OR position(lower(@{name}) in lower(COALESCE(equipment_path, ''))) > 0");
            sql.Append($" OR position(lower(@{name}) in lower(COALESCE(tag_path, ''))) > 0");
            sql.Append($" OR position(lower(@{name}) in lower(COALESCE(operator_name, ''))) > 0");
            sql.Append($" OR position(lower(@{name}) in lower(COALESCE(operation, ''))) > 0");
            sql.Append($" OR position(lower(@{name}) in lower(COALESCE(command_key, ''))) > 0");
            sql.Append($" OR position(lower(@{name}) in lower(COALESCE(message, ''))) > 0");
            sql.Append($" OR position(lower(@{name}) in lower(context_json::text)) > 0)");
        }

        var direction = query.Sort.Direction == HistoricalSortDirection.Descending ? "DESC" : "ASC";
        var relation = query.Sort.Direction == HistoricalSortDirection.Descending ? "<" : ">";
        if (query.After is not null)
        {
            if (!Guid.TryParse(query.After.TieBreaker, out var afterEventId))
                throw new HistoricalQueryCursorException("Operational Event cursor tie-breaker is invalid.");
            command.Parameters.AddWithValue("after_event_id", NpgsqlDbType.Uuid, afterEventId);
            command.Parameters.AddWithValue("after_ts", NpgsqlDbType.TimestampTz, query.After.TimestampUtc);
            sql.Append($" AND (timestamp_utc {relation} @after_ts OR (timestamp_utc = @after_ts AND event_id {relation} @after_event_id))");
        }

        sql.Append($" ORDER BY timestamp_utc {direction}, event_id {direction} LIMIT @fetch_limit;");
        command.Parameters.AddWithValue("fetch_limit", NpgsqlDbType.Integer, query.PageSize + 1);
        command.CommandText = sql.ToString();

        var materialized = new List<OperationalEventHistoryRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            materialized.Add(new OperationalEventHistoryRow(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                ReadNullableString(reader, 6),
                ReadNullableString(reader, 7),
                reader.IsDBNull(8) ? null : reader.GetGuid(8),
                ReadNullableString(reader, 9),
                ReadNullableString(reader, 10),
                ReadNullableString(reader, 11),
                reader.IsDBNull(12) ? null : reader.GetGuid(12),
                ReadNullableString(reader, 13),
                ReadNullableString(reader, 14),
                reader.GetString(15),
                ReadTimestamp(reader, 16)));
        }

        var hasMore = materialized.Count > query.PageSize;
        if (hasMore) materialized.RemoveAt(materialized.Count - 1);
        var rows = materialized.Select(ToRow).ToArray();
        var next = hasMore && materialized.Count > 0
            ? new HistoricalQueryPosition(
                HistoricalQueryValue.FromDateTime(materialized[^1].Timestamp),
                materialized[^1].Timestamp,
                materialized[^1].EventId.ToString("D"))
            : null;
        return new HistoricalProviderPage(rows, next);
    }

    private static string BuildFilter(HistoricalFilter filter, NpgsqlCommand command, ref int parameterIndex) =>
        filter.Field switch
        {
            "event.id" => BuildGuidFilter("event_id", filter, command, ref parameterIndex),
            "definition.id" => BuildGuidFilter("definition_id", filter, command, ref parameterIndex),
            "definition.key" => BuildStringFilter("definition_key", filter, command, ref parameterIndex),
            "type" => BuildStringFilter("event_type", filter, command, ref parameterIndex),
            "category" => BuildStringFilter("category", filter, command, ref parameterIndex),
            "source" => BuildStringFilter("source", filter, command, ref parameterIndex),
            "area" => BuildStringFilter("COALESCE(area, '')", filter, command, ref parameterIndex),
            "equipment.path" => BuildStringFilter("COALESCE(equipment_path, '')", filter, command, ref parameterIndex),
            "tag.id" => BuildGuidFilter("tag_id", filter, command, ref parameterIndex),
            "tag.path" => BuildStringFilter("COALESCE(tag_path, '')", filter, command, ref parameterIndex),
            "operator" => BuildStringFilter("COALESCE(operator_name, '')", filter, command, ref parameterIndex),
            "operation" => BuildStringFilter("COALESCE(operation, '')", filter, command, ref parameterIndex),
            "command.id" => BuildGuidFilter("command_id", filter, command, ref parameterIndex),
            "command.key" => BuildStringFilter("COALESCE(command_key, '')", filter, command, ref parameterIndex),
            "message" => BuildStringFilter("COALESCE(message, '')", filter, command, ref parameterIndex),
            "context" => BuildStringFilter("context_json::text", filter, command, ref parameterIndex),
            "timestamp" => BuildTimestampFilter("timestamp_utc", filter, command, ref parameterIndex),
            _ => throw new ArgumentException($"Operational Event filter field '{filter.Field}' is not supported.", nameof(filter))
        };

    private static string BuildGuidFilter(string column, HistoricalFilter filter, NpgsqlCommand command, ref int parameterIndex)
    {
        var values = filter.Values.Select(static value => value.AsGuid()).ToArray();
        if (filter.Operator == HistoricalFilterOperator.In)
        {
            var name = $"p{parameterIndex++}";
            command.Parameters.AddWithValue(name, NpgsqlDbType.Array | NpgsqlDbType.Uuid, values);
            return $"{column} = ANY(@{name})";
        }

        var parameter = $"p{parameterIndex++}";
        command.Parameters.AddWithValue(parameter, NpgsqlDbType.Uuid, values[0]);
        return filter.Operator switch
        {
            HistoricalFilterOperator.Eq => $"{column} = @{parameter}",
            HistoricalFilterOperator.NotEq => $"{column} <> @{parameter}",
            _ => throw new ArgumentException("Unsupported GUID filter operator.", nameof(filter))
        };
    }

    private static string BuildStringFilter(string column, HistoricalFilter filter, NpgsqlCommand command, ref int parameterIndex)
    {
        if (filter.Operator == HistoricalFilterOperator.In)
        {
            var name = $"p{parameterIndex++}";
            command.Parameters.AddWithValue(name, NpgsqlDbType.Array | NpgsqlDbType.Text, filter.Values.Select(static value => value.Value!).ToArray());
            return $"{column} = ANY(@{name})";
        }

        var parameter = $"p{parameterIndex++}";
        command.Parameters.AddWithValue(parameter, NpgsqlDbType.Text, filter.Values[0].Value!);
        return filter.Operator switch
        {
            HistoricalFilterOperator.Eq => $"{column} = @{parameter}",
            HistoricalFilterOperator.NotEq => $"{column} <> @{parameter}",
            HistoricalFilterOperator.Contains => $"position(lower(@{parameter}) in lower({column})) > 0",
            HistoricalFilterOperator.StartsWith => $"left(lower({column}), length(@{parameter})) = lower(@{parameter})",
            _ => throw new ArgumentException("Unsupported string filter operator.", nameof(filter))
        };
    }

    private static string BuildTimestampFilter(string column, HistoricalFilter filter, NpgsqlCommand command, ref int parameterIndex)
    {
        if (filter.Operator == HistoricalFilterOperator.In)
        {
            var name = $"p{parameterIndex++}";
            command.Parameters.AddWithValue(name, NpgsqlDbType.Array | NpgsqlDbType.TimestampTz, filter.Values.Select(static value => value.AsDateTime()).ToArray());
            return $"{column} = ANY(@{name})";
        }

        var parameter = $"p{parameterIndex++}";
        command.Parameters.AddWithValue(parameter, NpgsqlDbType.TimestampTz, filter.Values[0].AsDateTime());
        var op = filter.Operator switch
        {
            HistoricalFilterOperator.Eq => "=",
            HistoricalFilterOperator.NotEq => "<>",
            HistoricalFilterOperator.GreaterThan => ">",
            HistoricalFilterOperator.GreaterThanOrEqual => ">=",
            HistoricalFilterOperator.LessThan => "<",
            HistoricalFilterOperator.LessThanOrEqual => "<=",
            _ => throw new ArgumentException("Unsupported timestamp filter operator.", nameof(filter))
        };
        return $"{column} {op} @{parameter}";
    }

    private static HistoricalQueryRow ToRow(OperationalEventHistoryRow row) =>
        new(new Dictionary<string, HistoricalQueryValue>(StringComparer.Ordinal)
        {
            ["event.id"] = HistoricalQueryValue.FromGuid(row.EventId),
            ["definition.id"] = HistoricalQueryValue.FromGuid(row.DefinitionId),
            ["definition.key"] = HistoricalQueryValue.FromString(row.DefinitionKey),
            ["type"] = HistoricalQueryValue.FromString(row.Type),
            ["category"] = HistoricalQueryValue.FromString(row.Category),
            ["source"] = HistoricalQueryValue.FromString(row.Source),
            ["area"] = Value(row.Area),
            ["equipment.path"] = Value(row.EquipmentPath),
            ["tag.id"] = row.TagId.HasValue ? HistoricalQueryValue.FromGuid(row.TagId.Value) : HistoricalQueryValue.Null(),
            ["tag.path"] = Value(row.TagPath),
            ["operator"] = Value(row.Operator),
            ["operation"] = Value(row.Operation),
            ["command.id"] = row.CommandId.HasValue ? HistoricalQueryValue.FromGuid(row.CommandId.Value) : HistoricalQueryValue.Null(),
            ["command.key"] = Value(row.CommandKey),
            ["message"] = Value(row.Message),
            ["context"] = HistoricalQueryValue.FromString(row.ContextJson),
            ["timestamp"] = HistoricalQueryValue.FromDateTime(row.Timestamp)
        });

    private static HistoricalQueryValue Value(string? value) =>
        value is null ? HistoricalQueryValue.Null() : HistoricalQueryValue.FromString(value);

    private static void AddNullable(NpgsqlCommand command, string name, NpgsqlDbType type, object? value) =>
        command.Parameters.AddWithValue(name, type, value ?? DBNull.Value);

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

    private static string? ReadNullableString(NpgsqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    private static DateTimeOffset ReadTimestamp(NpgsqlDataReader reader, int ordinal)
    {
        try
        {
            return reader.GetFieldValue<DateTimeOffset>(ordinal).ToUniversalTime();
        }
        catch (InvalidCastException)
        {
            var value = reader.GetDateTime(ordinal);
            if (value.Kind != DateTimeKind.Utc)
                value = DateTime.SpecifyKind(value, DateTimeKind.Utc);
            return new DateTimeOffset(value);
        }
    }

    public async ValueTask DisposeAsync() => await _dataSource.DisposeAsync();

    private sealed record OperationalEventHistoryRow(
        Guid EventId,
        Guid DefinitionId,
        string DefinitionKey,
        string Type,
        string Category,
        string Source,
        string? Area,
        string? EquipmentPath,
        Guid? TagId,
        string? TagPath,
        string? Operator,
        string? Operation,
        Guid? CommandId,
        string? CommandKey,
        string? Message,
        string ContextJson,
        DateTimeOffset Timestamp);
}