using System.Globalization;
using System.Text;
using Npgsql;
using NpgsqlTypes;
using Scada.Core.Alarms;
using Scada.Core.HistoricalQueries;

namespace Scada.Persistence.PostgreSql;

public sealed class PostgreSqlAlarmHistoryStore : IHistoricalDatasetProvider, IAsyncDisposable
{
    private const long InfrastructureLockKey = 4993446713136202561;
    private const string InfrastructureSql = """
        CREATE SCHEMA IF NOT EXISTS elitescada;

        CREATE TABLE IF NOT EXISTS elitescada.alarm_history (
            event_id uuid PRIMARY KEY,
            alarm_id uuid NOT NULL,
            tag_id uuid NOT NULL,
            tag_path text NOT NULL,
            previous_state smallint NOT NULL,
            state smallint NOT NULL,
            priority smallint NOT NULL,
            message text NULL,
            timestamp_utc timestamptz NOT NULL
        );

        CREATE INDEX IF NOT EXISTS ix_alarm_history_timestamp_event_desc
            ON elitescada.alarm_history (timestamp_utc DESC, event_id DESC);
        CREATE INDEX IF NOT EXISTS ix_alarm_history_alarm_timestamp
            ON elitescada.alarm_history (alarm_id, timestamp_utc DESC, event_id DESC);
        CREATE INDEX IF NOT EXISTS ix_alarm_history_tag_timestamp
            ON elitescada.alarm_history (tag_id, timestamp_utc DESC, event_id DESC);
        CREATE INDEX IF NOT EXISTS ix_alarm_history_priority_timestamp
            ON elitescada.alarm_history (priority DESC, timestamp_utc DESC, event_id DESC);

        CREATE OR REPLACE FUNCTION elitescada.reject_alarm_history_mutation()
        RETURNS trigger
        LANGUAGE plpgsql
        AS $$
        BEGIN
            RAISE EXCEPTION 'EliteSCADA alarm history is append-only';
        END;
        $$;

        DROP TRIGGER IF EXISTS trg_alarm_history_append_only ON elitescada.alarm_history;
        CREATE TRIGGER trg_alarm_history_append_only
            BEFORE UPDATE OR DELETE ON elitescada.alarm_history
            FOR EACH ROW EXECUTE FUNCTION elitescada.reject_alarm_history_mutation();

        DROP TRIGGER IF EXISTS trg_alarm_history_no_truncate ON elitescada.alarm_history;
        CREATE TRIGGER trg_alarm_history_no_truncate
            BEFORE TRUNCATE ON elitescada.alarm_history
            FOR EACH STATEMENT EXECUTE FUNCTION elitescada.reject_alarm_history_mutation();
        """;

    private readonly NpgsqlDataSource _dataSource;
    private readonly Task _initializeTask;

    public PostgreSqlAlarmHistoryStore(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("PostgreSQL connection string is required.", nameof(connectionString));

        _dataSource = NpgsqlDataSource.Create(connectionString);
        _initializeTask = InitializeAsync();
    }

    public string Dataset => HistoricalDatasets.AlarmEvents;

    public async Task AppendAsync(
        AlarmStateChanged stateChanged,
        string tagPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stateChanged);
        if (string.IsNullOrWhiteSpace(tagPath))
            throw new ArgumentException("Canonical TAG path is required for alarm history.", nameof(tagPath));
        if (stateChanged.Current.DefinitionId == Guid.Empty || stateChanged.Current.TagId == Guid.Empty)
            throw new ArgumentException("Alarm history requires stable alarm and TAG identities.", nameof(stateChanged));

        await _initializeTask.WaitAsync(cancellationToken);
        const string sql = """
            INSERT INTO elitescada.alarm_history (
                event_id, alarm_id, tag_id, tag_path, previous_state, state,
                priority, message, timestamp_utc)
            VALUES (
                @event_id, @alarm_id, @tag_id, @tag_path, @previous_state, @state,
                @priority, @message, @timestamp_utc);
            """;

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("event_id", NpgsqlDbType.Uuid, Guid.NewGuid());
        command.Parameters.AddWithValue("alarm_id", NpgsqlDbType.Uuid, stateChanged.Current.DefinitionId);
        command.Parameters.AddWithValue("tag_id", NpgsqlDbType.Uuid, stateChanged.Current.TagId);
        command.Parameters.AddWithValue("tag_path", NpgsqlDbType.Text, tagPath.Trim());
        command.Parameters.AddWithValue("previous_state", NpgsqlDbType.Smallint, (short)stateChanged.Previous.State);
        command.Parameters.AddWithValue("state", NpgsqlDbType.Smallint, (short)stateChanged.Current.State);
        command.Parameters.AddWithValue("priority", NpgsqlDbType.Smallint, (short)stateChanged.Current.Priority);
        command.Parameters.AddWithValue("message", NpgsqlDbType.Text, (object?)stateChanged.Current.Message ?? DBNull.Value);
        command.Parameters.AddWithValue("timestamp_utc", NpgsqlDbType.TimestampTz, stateChanged.OccurredAt.ToUniversalTime());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<HistoricalProviderPage> QueryAsync(
        HistoricalQueryExecution query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (!string.Equals(query.Dataset.Id, Dataset, StringComparison.Ordinal))
            throw new ArgumentException("Alarm historical provider received the wrong dataset.", nameof(query));

        await _initializeTask.WaitAsync(cancellationToken);
        var sql = new StringBuilder("""
            SELECT event_id, alarm_id, tag_id, tag_path, state, priority, message, timestamp_utc
            FROM elitescada.alarm_history
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
            sql.Append(
                $" AND (position(lower(@{name}) in lower(tag_path)) > 0 OR position(lower(@{name}) in lower(COALESCE(message, ''))) > 0)");
        }

        var sortColumn = query.Sort.Field switch
        {
            "timestamp" => "timestamp_utc",
            "priority" => "priority",
            "state" => "state",
            "tag.path" => "tag_path",
            _ => throw new ArgumentException(
                $"Alarm sort field '{query.Sort.Field}' is not supported.",
                nameof(query))
        };
        var direction = query.Sort.Direction == HistoricalSortDirection.Descending ? "DESC" : "ASC";
        var relation = query.Sort.Direction == HistoricalSortDirection.Descending ? "<" : ">";

        if (query.After is not null)
            AppendCursorPredicate(sql, query, sortColumn, relation, command);

        sql.Append(
            $" ORDER BY {sortColumn} {direction}, timestamp_utc {direction}, event_id {direction} LIMIT @fetch_limit;");
        command.Parameters.AddWithValue("fetch_limit", NpgsqlDbType.Integer, query.PageSize + 1);
        command.CommandText = sql.ToString();

        var materialized = new List<AlarmHistoryRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            materialized.Add(new AlarmHistoryRow(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetGuid(2),
                reader.GetString(3),
                (AlarmState)reader.GetInt16(4),
                (AlarmPriority)reader.GetInt16(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                ReadTimestamp(reader, 7)));
        }

        var hasMore = materialized.Count > query.PageSize;
        if (hasMore) materialized.RemoveAt(materialized.Count - 1);
        var rows = materialized.Select(ToRow).ToArray();
        var next = hasMore && materialized.Count > 0
            ? Position(materialized[^1], query.Sort.Field)
            : null;
        return new HistoricalProviderPage(rows, next);
    }

    private static void AppendCursorPredicate(
        StringBuilder sql,
        HistoricalQueryExecution query,
        string sortColumn,
        string relation,
        NpgsqlCommand command)
    {
        var after = query.After!;
        if (!Guid.TryParse(after.TieBreaker, out var eventId))
            throw new HistoricalQueryCursorException("Alarm cursor tie-breaker is invalid.");

        command.Parameters.AddWithValue("after_event_id", NpgsqlDbType.Uuid, eventId);
        command.Parameters.AddWithValue("after_ts", NpgsqlDbType.TimestampTz, after.TimestampUtc);
        if (query.Sort.Field == "timestamp")
        {
            sql.Append(
                $" AND (timestamp_utc {relation} @after_ts OR (timestamp_utc = @after_ts AND event_id {relation} @after_event_id))");
            return;
        }

        AddAfterPrimary(query.Sort.Field, after.Primary, command);
        sql.Append(
            $" AND ({sortColumn} {relation} @after_primary OR ({sortColumn} = @after_primary AND (timestamp_utc {relation} @after_ts OR (timestamp_utc = @after_ts AND event_id {relation} @after_event_id))))");
    }

    private static string BuildFilter(
        HistoricalFilter filter,
        NpgsqlCommand command,
        ref int parameterIndex) => filter.Field switch
        {
            "alarm.id" => BuildGuidFilter("alarm_id", filter, command, ref parameterIndex),
            "tag.id" => BuildGuidFilter("tag_id", filter, command, ref parameterIndex),
            "tag.path" => BuildStringFilter("tag_path", filter, command, ref parameterIndex),
            "state" => BuildStateFilter(filter, command, ref parameterIndex),
            "priority" => BuildPriorityFilter(filter, command, ref parameterIndex),
            "message" => BuildStringFilter("COALESCE(message, '')", filter, command, ref parameterIndex),
            "timestamp" => BuildTimestampFilter("timestamp_utc", filter, command, ref parameterIndex),
            _ => throw new ArgumentException(
                $"Alarm filter field '{filter.Field}' is not supported.",
                nameof(filter))
        };

    private static string BuildGuidFilter(
        string column,
        HistoricalFilter filter,
        NpgsqlCommand command,
        ref int parameterIndex)
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

    private static string BuildStringFilter(
        string column,
        HistoricalFilter filter,
        NpgsqlCommand command,
        ref int parameterIndex)
    {
        if (filter.Operator == HistoricalFilterOperator.In)
        {
            var name = $"p{parameterIndex++}";
            command.Parameters.AddWithValue(
                name,
                NpgsqlDbType.Array | NpgsqlDbType.Text,
                filter.Values.Select(static value => value.Value!).ToArray());
            return $"{column} = ANY(@{name})";
        }

        var parameter = $"p{parameterIndex++}";
        command.Parameters.AddWithValue(parameter, NpgsqlDbType.Text, filter.Values[0].Value!);
        return filter.Operator switch
        {
            HistoricalFilterOperator.Eq => $"{column} = @{parameter}",
            HistoricalFilterOperator.NotEq => $"{column} <> @{parameter}",
            HistoricalFilterOperator.Contains =>
                $"position(lower(@{parameter}) in lower({column})) > 0",
            HistoricalFilterOperator.StartsWith =>
                $"left(lower({column}), length(@{parameter})) = lower(@{parameter})",
            _ => throw new ArgumentException("Unsupported string filter operator.", nameof(filter))
        };
    }

    private static string BuildStateFilter(
        HistoricalFilter filter,
        NpgsqlCommand command,
        ref int parameterIndex)
    {
        var values = filter.Values.Select(value =>
        {
            if (!Enum.TryParse<AlarmState>(value.Value, ignoreCase: true, out var parsed) ||
                !Enum.IsDefined(parsed))
                throw new ArgumentException(
                    $"Unknown alarm state '{value.Value}'.",
                    nameof(filter));
            return (short)parsed;
        }).ToArray();
        return BuildSmallIntFilter(
            "state",
            filter.Operator,
            values,
            command,
            ref parameterIndex);
    }

    private static string BuildPriorityFilter(
        HistoricalFilter filter,
        NpgsqlCommand command,
        ref int parameterIndex)
    {
        var values = filter.Values.Select(value =>
        {
            var numeric = value.AsNumber();
            if (numeric != Math.Truncate(numeric) ||
                numeric < (int)AlarmPriority.Low ||
                numeric > (int)AlarmPriority.Critical)
                throw new ArgumentException(
                    $"Alarm priority '{numeric}' is outside the canonical range.",
                    nameof(filter));
            return (short)numeric;
        }).ToArray();

        if (filter.Operator == HistoricalFilterOperator.In)
            return BuildSmallIntFilter(
                "priority",
                filter.Operator,
                values,
                command,
                ref parameterIndex);

        var parameter = $"p{parameterIndex++}";
        command.Parameters.AddWithValue(parameter, NpgsqlDbType.Smallint, values[0]);
        var op = filter.Operator switch
        {
            HistoricalFilterOperator.Eq => "=",
            HistoricalFilterOperator.NotEq => "<>",
            HistoricalFilterOperator.GreaterThan => ">",
            HistoricalFilterOperator.GreaterThanOrEqual => ">=",
            HistoricalFilterOperator.LessThan => "<",
            HistoricalFilterOperator.LessThanOrEqual => "<=",
            _ => throw new ArgumentException("Unsupported priority filter operator.", nameof(filter))
        };
        return $"priority {op} @{parameter}";
    }

    private static string BuildSmallIntFilter(
        string column,
        HistoricalFilterOperator filterOperator,
        short[] values,
        NpgsqlCommand command,
        ref int parameterIndex)
    {
        if (filterOperator == HistoricalFilterOperator.In)
        {
            var name = $"p{parameterIndex++}";
            command.Parameters.AddWithValue(
                name,
                NpgsqlDbType.Array | NpgsqlDbType.Smallint,
                values);
            return $"{column} = ANY(@{name})";
        }

        var parameter = $"p{parameterIndex++}";
        command.Parameters.AddWithValue(parameter, NpgsqlDbType.Smallint, values[0]);
        return filterOperator switch
        {
            HistoricalFilterOperator.Eq => $"{column} = @{parameter}",
            HistoricalFilterOperator.NotEq => $"{column} <> @{parameter}",
            _ => throw new ArgumentException("Unsupported enum filter operator.")
        };
    }

    private static string BuildTimestampFilter(
        string column,
        HistoricalFilter filter,
        NpgsqlCommand command,
        ref int parameterIndex)
    {
        if (filter.Operator == HistoricalFilterOperator.In)
        {
            var name = $"p{parameterIndex++}";
            command.Parameters.AddWithValue(
                name,
                NpgsqlDbType.Array | NpgsqlDbType.TimestampTz,
                filter.Values.Select(static value => value.AsDateTime()).ToArray());
            return $"{column} = ANY(@{name})";
        }

        var parameter = $"p{parameterIndex++}";
        command.Parameters.AddWithValue(
            parameter,
            NpgsqlDbType.TimestampTz,
            filter.Values[0].AsDateTime());
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

    private static void AddAfterPrimary(
        string field,
        HistoricalQueryValue value,
        NpgsqlCommand command)
    {
        switch (field)
        {
            case "priority":
            {
                var priority = value.AsNumber();
                if (priority != Math.Truncate(priority) ||
                    priority < (int)AlarmPriority.Low ||
                    priority > (int)AlarmPriority.Critical)
                    throw new HistoricalQueryCursorException(
                        "Alarm priority cursor value is invalid.");
                command.Parameters.AddWithValue(
                    "after_primary",
                    NpgsqlDbType.Smallint,
                    (short)priority);
                break;
            }
            case "state":
                if (!Enum.TryParse<AlarmState>(value.Value, true, out var state) ||
                    !Enum.IsDefined(state))
                    throw new HistoricalQueryCursorException(
                        "Alarm state cursor value is invalid.");
                command.Parameters.AddWithValue(
                    "after_primary",
                    NpgsqlDbType.Smallint,
                    (short)state);
                break;
            case "tag.path":
                command.Parameters.AddWithValue(
                    "after_primary",
                    NpgsqlDbType.Text,
                    value.Value
                    ?? throw new HistoricalQueryCursorException(
                        "Alarm TAG path cursor value is invalid."));
                break;
            default:
                throw new HistoricalQueryCursorException(
                    "Alarm cursor sort field is invalid.");
        }
    }

    private static HistoricalQueryRow ToRow(AlarmHistoryRow row) =>
        new(new Dictionary<string, HistoricalQueryValue>(StringComparer.Ordinal)
        {
            ["alarm.id"] = HistoricalQueryValue.FromGuid(row.AlarmId),
            ["tag.id"] = HistoricalQueryValue.FromGuid(row.TagId),
            ["tag.path"] = HistoricalQueryValue.FromString(row.TagPath),
            ["state"] = HistoricalQueryValue.FromEnum(row.State.ToString()),
            ["priority"] = HistoricalQueryValue.FromNumber((int)row.Priority),
            ["message"] = row.Message is null
                ? HistoricalQueryValue.Null()
                : HistoricalQueryValue.FromString(row.Message),
            ["timestamp"] = HistoricalQueryValue.FromDateTime(row.Timestamp)
        });

    private static HistoricalQueryPosition Position(
        AlarmHistoryRow row,
        string sortField)
    {
        var primary = sortField switch
        {
            "timestamp" => HistoricalQueryValue.FromDateTime(row.Timestamp),
            "priority" => HistoricalQueryValue.FromNumber((int)row.Priority),
            "state" => HistoricalQueryValue.FromEnum(row.State.ToString()),
            "tag.path" => HistoricalQueryValue.FromString(row.TagPath),
            _ => throw new ArgumentOutOfRangeException(nameof(sortField))
        };
        return new HistoricalQueryPosition(
            primary,
            row.Timestamp,
            row.EventId.ToString("D"));
    }

    private async Task InitializeAsync()
    {
        await using var connection = await _dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using (var lockCommand = new NpgsqlCommand(
                         "SELECT pg_advisory_xact_lock(@lock_key);",
                         connection,
                         transaction))
        {
            lockCommand.Parameters.AddWithValue(
                "lock_key",
                NpgsqlDbType.Bigint,
                InfrastructureLockKey);
            await lockCommand.ExecuteNonQueryAsync();
        }

        await using (var command = new NpgsqlCommand(
                         InfrastructureSql,
                         connection,
                         transaction))
            await command.ExecuteNonQueryAsync();
        await transaction.CommitAsync();
    }

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

    private sealed record AlarmHistoryRow(
        Guid EventId,
        Guid AlarmId,
        Guid TagId,
        string TagPath,
        AlarmState State,
        AlarmPriority Priority,
        string? Message,
        DateTimeOffset Timestamp);
}
