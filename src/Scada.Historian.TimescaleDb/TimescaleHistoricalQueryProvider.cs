using System.Globalization;
using System.Text;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using Scada.Core.HistoricalQueries;
using Scada.Core.Tags;

namespace Scada.Historian.TimescaleDb;

public sealed class TimescaleHistoricalQueryProvider : IHistoricalDatasetProvider, IAsyncDisposable
{
    private const long InfrastructureLockKey = 4993446713136202561;
    private const string InfrastructureSql = """
        CREATE SEQUENCE IF NOT EXISTS elitescada.tag_history_sample_id_seq;

        ALTER TABLE elitescada.tag_history
            ADD COLUMN IF NOT EXISTS sample_id bigint NULL;

        ALTER TABLE elitescada.tag_history
            ALTER COLUMN sample_id SET DEFAULT nextval('elitescada.tag_history_sample_id_seq');

        UPDATE elitescada.tag_history
        SET sample_id = nextval('elitescada.tag_history_sample_id_seq')
        WHERE sample_id IS NULL;

        ALTER TABLE elitescada.tag_history
            ALTER COLUMN sample_id SET NOT NULL;

        ALTER SEQUENCE elitescada.tag_history_sample_id_seq
            OWNED BY elitescada.tag_history.sample_id;

        CREATE INDEX IF NOT EXISTS ix_tag_history_time_sample_id_desc
            ON elitescada.tag_history (ts DESC, sample_id DESC);
        """;

    private readonly NpgsqlDataSource _dataSource;
    private readonly ITagRegistry _tagRegistry;
    private readonly Task _initializeTask;

    public TimescaleHistoricalQueryProvider(string connectionString, ITagRegistry tagRegistry)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("TimescaleDB connection string is required.", nameof(connectionString));
        _tagRegistry = tagRegistry ?? throw new ArgumentNullException(nameof(tagRegistry));
        _dataSource = NpgsqlDataSource.Create(connectionString);
        _initializeTask = InitializeAsync();
    }

    public string Dataset => HistoricalDatasets.HistorianSamples;

    public async Task<HistoricalProviderPage> QueryAsync(
        HistoricalQueryExecution query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (!string.Equals(query.Dataset.Id, Dataset, StringComparison.Ordinal))
            throw new ArgumentException("Timescale historical provider received the wrong dataset.", nameof(query));
        if (!string.Equals(query.Sort.Field, "timestamp", StringComparison.Ordinal))
            throw new ArgumentException("Historian samples currently allow only timestamp sorting.", nameof(query));

        await _initializeTask.WaitAsync(cancellationToken);
        var tags = _tagRegistry.Snapshot();
        var sql = new StringBuilder("""
            SELECT sample_id, tag_id, ts, quality, value::text, data_type
            FROM elitescada.tag_history
            WHERE ts >= @from_utc AND ts <= @to_utc
            """);
        await using var command = _dataSource.CreateCommand();
        command.Parameters.AddWithValue("from_utc", NpgsqlDbType.TimestampTz, query.Range.FromUtc);
        command.Parameters.AddWithValue("to_utc", NpgsqlDbType.TimestampTz, query.Range.ToUtc);

        var parameterIndex = 0;
        foreach (var group in query.Filters.GroupBy(static filter => filter.Field, StringComparer.Ordinal))
        {
            var clauses = new List<string>();
            foreach (var filter in group)
                clauses.Add(BuildFilter(filter, tags, command, ref parameterIndex));
            sql.Append(" AND (").AppendJoin(" OR ", clauses).Append(')');
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var ids = tags
                .Where(tag => tag.Path.Contains(query.Search, StringComparison.OrdinalIgnoreCase))
                .Select(static tag => tag.Id)
                .Distinct()
                .ToArray();
            sql.Append(" AND tag_id = ANY(@search_tag_ids)");
            command.Parameters.AddWithValue("search_tag_ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid, ids);
        }

        if (query.After is not null)
        {
            if (!long.TryParse(query.After.TieBreaker, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sampleId))
                throw new HistoricalQueryCursorException("Historian cursor tie-breaker is invalid.");
            var relation = query.Sort.Direction == HistoricalSortDirection.Descending ? "<" : ">";
            sql.Append($" AND (ts {relation} @after_ts OR (ts = @after_ts AND sample_id {relation} @after_sample_id))");
            command.Parameters.AddWithValue("after_ts", NpgsqlDbType.TimestampTz, query.After.TimestampUtc);
            command.Parameters.AddWithValue("after_sample_id", NpgsqlDbType.Bigint, sampleId);
        }

        var direction = query.Sort.Direction == HistoricalSortDirection.Descending ? "DESC" : "ASC";
        sql.Append($" ORDER BY ts {direction}, sample_id {direction} LIMIT @fetch_limit;");
        command.Parameters.AddWithValue("fetch_limit", NpgsqlDbType.Integer, query.PageSize + 1);
        command.CommandText = sql.ToString();

        var materialized = new List<SampleRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            materialized.Add(new SampleRow(
                reader.GetInt64(0),
                reader.GetGuid(1),
                ReadTimestamp(reader, 2),
                (TagQuality)reader.GetInt32(3),
                reader.GetString(4),
                reader.IsDBNull(5) ? null : (TagDataType?)reader.GetInt16(5)));
        }

        var hasMore = materialized.Count > query.PageSize;
        if (hasMore) materialized.RemoveAt(materialized.Count - 1);
        var rows = materialized.Select(sample => ToRow(sample, tags)).ToArray();
        var next = hasMore && materialized.Count > 0
            ? Position(materialized[^1])
            : null;
        return new HistoricalProviderPage(rows, next);
    }

    private static string BuildFilter(
        HistoricalFilter filter,
        IReadOnlyCollection<TagDefinition> tags,
        NpgsqlCommand command,
        ref int parameterIndex)
    {
        return filter.Field switch
        {
            "tag.id" => BuildGuidFilter("tag_id", filter, command, ref parameterIndex),
            "tag.path" => BuildPathFilter(filter, tags, command, ref parameterIndex),
            "quality" => BuildQualityFilter(filter, command, ref parameterIndex),
            "value" => BuildValueFilter(filter, command, ref parameterIndex),
            "timestamp" => BuildTimestampFilter("ts", filter, command, ref parameterIndex),
            _ => throw new ArgumentException($"Historian filter field '{filter.Field}' is not supported.", nameof(filter))
        };
    }

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

    private static string BuildPathFilter(
        HistoricalFilter filter,
        IReadOnlyCollection<TagDefinition> tags,
        NpgsqlCommand command,
        ref int parameterIndex)
    {
        bool Matches(TagDefinition tag)
        {
            var candidate = tag.Path;
            return filter.Operator switch
            {
                HistoricalFilterOperator.Eq => candidate.Equals(filter.Values[0].Value, StringComparison.OrdinalIgnoreCase),
                HistoricalFilterOperator.NotEq => !candidate.Equals(filter.Values[0].Value, StringComparison.OrdinalIgnoreCase),
                HistoricalFilterOperator.In => filter.Values.Any(value => candidate.Equals(value.Value, StringComparison.OrdinalIgnoreCase)),
                HistoricalFilterOperator.Contains => candidate.Contains(filter.Values[0].Value!, StringComparison.OrdinalIgnoreCase),
                HistoricalFilterOperator.StartsWith => candidate.StartsWith(filter.Values[0].Value!, StringComparison.OrdinalIgnoreCase),
                _ => throw new ArgumentException("Unsupported path filter operator.", nameof(filter))
            };
        }

        var ids = tags.Where(Matches).Select(static tag => tag.Id).Distinct().ToArray();
        var name = $"p{parameterIndex++}";
        command.Parameters.AddWithValue(name, NpgsqlDbType.Array | NpgsqlDbType.Uuid, ids);
        return $"tag_id = ANY(@{name})";
    }

    private static string BuildQualityFilter(
        HistoricalFilter filter,
        NpgsqlCommand command,
        ref int parameterIndex)
    {
        var values = filter.Values.Select(value =>
        {
            if (!Enum.TryParse<TagQuality>(value.Value, ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
                throw new ArgumentException($"Unknown TAG quality '{value.Value}'.", nameof(filter));
            return (int)parsed;
        }).ToArray();

        if (filter.Operator == HistoricalFilterOperator.In)
        {
            var name = $"p{parameterIndex++}";
            command.Parameters.AddWithValue(name, NpgsqlDbType.Array | NpgsqlDbType.Integer, values);
            return $"quality = ANY(@{name})";
        }
        var parameter = $"p{parameterIndex++}";
        command.Parameters.AddWithValue(parameter, NpgsqlDbType.Integer, values[0]);
        return filter.Operator switch
        {
            HistoricalFilterOperator.Eq => $"quality = @{parameter}",
            HistoricalFilterOperator.NotEq => $"quality <> @{parameter}",
            _ => throw new ArgumentException("Unsupported quality filter operator.", nameof(filter))
        };
    }

    private static string BuildValueFilter(
        HistoricalFilter filter,
        NpgsqlCommand command,
        ref int parameterIndex)
    {
        string Json(HistoricalQueryValue value) => value.Kind switch
        {
            HistoricalValueKind.String or HistoricalValueKind.Enum => JsonSerializer.Serialize(value.Value),
            HistoricalValueKind.Number => value.AsNumber().ToString("R", CultureInfo.InvariantCulture),
            HistoricalValueKind.Boolean => value.AsBoolean() ? "true" : "false",
            HistoricalValueKind.Int64 => value.AsInt64().ToString(CultureInfo.InvariantCulture),
            _ => throw new ArgumentException("Unsupported historian scalar filter value.", nameof(filter))
        };

        if (filter.Operator == HistoricalFilterOperator.In)
        {
            var pieces = new List<string>();
            foreach (var value in filter.Values)
            {
                var name = $"p{parameterIndex++}";
                command.Parameters.AddWithValue(name, NpgsqlDbType.Jsonb, Json(value));
                pieces.Add($"value = @{name}");
            }
            return $"({string.Join(" OR ", pieces)})";
        }

        var parameter = $"p{parameterIndex++}";
        command.Parameters.AddWithValue(parameter, NpgsqlDbType.Jsonb, Json(filter.Values[0]));
        return filter.Operator switch
        {
            HistoricalFilterOperator.Eq => $"value = @{parameter}",
            HistoricalFilterOperator.NotEq => $"value <> @{parameter}",
            _ => throw new ArgumentException("Unsupported historian value filter operator.", nameof(filter))
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
            var values = filter.Values.Select(static value => value.AsDateTime()).ToArray();
            var name = $"p{parameterIndex++}";
            command.Parameters.AddWithValue(name, NpgsqlDbType.Array | NpgsqlDbType.TimestampTz, values);
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

    private static HistoricalQueryRow ToRow(SampleRow sample, IReadOnlyCollection<TagDefinition> tags)
    {
        var tag = tags.FirstOrDefault(candidate => candidate.Id == sample.TagId);
        return new HistoricalQueryRow(new Dictionary<string, HistoricalQueryValue>(StringComparer.Ordinal)
        {
            ["tag.id"] = HistoricalQueryValue.FromGuid(sample.TagId),
            ["tag.path"] = tag is null ? HistoricalQueryValue.Null() : HistoricalQueryValue.FromString(tag.Path),
            ["quality"] = HistoricalQueryValue.FromEnum(sample.Quality.ToString()),
            ["value"] = ReadValue(sample.ValueJson, sample.DataType),
            ["timestamp"] = HistoricalQueryValue.FromDateTime(sample.Timestamp)
        });
    }

    private static HistoricalQueryValue ReadValue(string json, TagDataType? dataType)
    {
        using var document = JsonDocument.Parse(json);
        var value = document.RootElement;
        if (value.ValueKind == JsonValueKind.Null) return HistoricalQueryValue.Null();
        if (dataType == TagDataType.Int64 || value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _))
        {
            if (value.TryGetInt64(out var integer)) return HistoricalQueryValue.FromInt64(integer);
        }
        return value.ValueKind switch
        {
            JsonValueKind.True => HistoricalQueryValue.FromBoolean(true),
            JsonValueKind.False => HistoricalQueryValue.FromBoolean(false),
            JsonValueKind.Number => HistoricalQueryValue.FromNumber(value.GetDouble()),
            JsonValueKind.String when dataType == TagDataType.Enum => HistoricalQueryValue.FromEnum(value.GetString() ?? string.Empty),
            JsonValueKind.String when dataType == TagDataType.DateTime && DateTimeOffset.TryParse(value.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var timestamp) => HistoricalQueryValue.FromDateTime(timestamp),
            JsonValueKind.String => HistoricalQueryValue.FromString(value.GetString() ?? string.Empty),
            _ => HistoricalQueryValue.FromString(value.GetRawText())
        };
    }

    private static HistoricalQueryPosition Position(SampleRow sample) =>
        new(
            HistoricalQueryValue.FromDateTime(sample.Timestamp),
            sample.Timestamp,
            sample.SampleId.ToString(CultureInfo.InvariantCulture));

    private async Task InitializeAsync()
    {
        await TimescaleHistorianInfrastructure.EnsureRawAsync(_dataSource);
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

    private static DateTimeOffset ReadTimestamp(NpgsqlDataReader reader, int ordinal)
    {
        try { return reader.GetFieldValue<DateTimeOffset>(ordinal).ToUniversalTime(); }
        catch (InvalidCastException)
        {
            var value = reader.GetDateTime(ordinal);
            if (value.Kind != DateTimeKind.Utc) value = DateTime.SpecifyKind(value, DateTimeKind.Utc);
            return new DateTimeOffset(value);
        }
    }

    public async ValueTask DisposeAsync() => await _dataSource.DisposeAsync();

    private sealed record SampleRow(
        long SampleId,
        Guid TagId,
        DateTimeOffset Timestamp,
        TagQuality Quality,
        string ValueJson,
        TagDataType? DataType);
}
