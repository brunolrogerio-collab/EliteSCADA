using System.Globalization;
using System.Text;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using Scada.Core.HistoricalQueries;
using Scada.Core.Tags;
using Scada.Historian.Policies;

namespace Scada.Historian.TimescaleDb;

public sealed partial class TimescaleHistoricalQueryProvider : IHistoricalDatasetProvider, IAsyncDisposable
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

    public Task<HistoricalProviderPage> QueryAsync(
        HistoricalQueryExecution query,
        CancellationToken cancellationToken = default) =>
        QueryCoreAsync(query, includeProvenance: false, cancellationToken);

    public Task<HistoricalProviderPage> QueryVersion2Async(
        HistoricalQueryExecution query,
        CancellationToken cancellationToken = default) =>
        QueryCoreAsync(query, includeProvenance: true, cancellationToken);

    private async Task<HistoricalProviderPage> QueryCoreAsync(
        HistoricalQueryExecution query,
        bool includeProvenance,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var expectedDataset = includeProvenance
            ? HistoricalDatasets.HistorianSamplesV2
            : Dataset;
        if (!string.Equals(query.Dataset.Id, expectedDataset, StringComparison.Ordinal))
            throw new ArgumentException(
                "Timescale historical provider received the wrong dataset.",
                nameof(query));
        if (!string.Equals(query.Sort.Field, "timestamp", StringComparison.Ordinal))
            throw new ArgumentException(
                "Historian samples currently allow only timestamp sorting.",
                nameof(query));

        await _initializeTask.WaitAsync(cancellationToken);
        var tags = _tagRegistry.Snapshot();
        var sql = new StringBuilder(includeProvenance
            ? """
                SELECT sample_id, tag_id, ts, quality, value::text, data_type,
                       source_ts, source_ts_evidence, server_ts, server_ts_evidence,
                       capture_profile_id, capture_policy_state, capture_policy_fingerprint
                FROM elitescada.tag_history
                WHERE ts >= @from_utc AND ts <= @to_utc
                """
            : """
                SELECT sample_id, tag_id, ts, quality, value::text, data_type
                FROM elitescada.tag_history
                WHERE ts >= @from_utc AND ts <= @to_utc
                """);
        await using var command = _dataSource.CreateCommand();
        command.Parameters.AddWithValue("from_utc", NpgsqlDbType.TimestampTz, query.Range.FromUtc);
        command.Parameters.AddWithValue("to_utc", NpgsqlDbType.TimestampTz, query.Range.ToUtc);

        var parameterIndex = 0;
        foreach (var filter in query.Filters)
            sql.Append(" AND ").Append(BuildFilter(filter, tags, command, ref parameterIndex));

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var ids = tags
                .Where(tag => tag.Path.Contains(query.Search, StringComparison.OrdinalIgnoreCase))
                .Select(static tag => tag.Id)
                .Distinct()
                .ToArray();
            sql.Append(" AND tag_id = ANY(@search_tag_ids)");
            command.Parameters.AddWithValue(
                "search_tag_ids",
                NpgsqlDbType.Array | NpgsqlDbType.Uuid,
                ids);
        }

        if (query.After is not null)
        {
            if (!long.TryParse(
                    query.After.TieBreaker,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var sampleId))
                throw new HistoricalQueryCursorException(
                    "Historian cursor tie-breaker is invalid.");

            var relation = query.Sort.Direction == HistoricalSortDirection.Descending ? "<" : ">";
            sql.Append(
                $" AND (ts {relation} @after_ts OR (ts = @after_ts AND sample_id {relation} @after_sample_id))");
            command.Parameters.AddWithValue(
                "after_ts",
                NpgsqlDbType.TimestampTz,
                query.After.TimestampUtc);
            command.Parameters.AddWithValue(
                "after_sample_id",
                NpgsqlDbType.Bigint,
                sampleId);
        }

        var direction = query.Sort.Direction == HistoricalSortDirection.Descending ? "DESC" : "ASC";
        sql.Append(
            $" ORDER BY ts {direction}, sample_id {direction} LIMIT @fetch_limit;");
        command.Parameters.AddWithValue(
            "fetch_limit",
            NpgsqlDbType.Integer,
            query.PageSize + 1);
        command.CommandText = sql.ToString();

        var materialized = new List<SampleRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var sample = new SampleRow(
                reader.GetInt64(0),
                reader.GetGuid(1),
                ReadTimestamp(reader, 2),
                (TagQuality)reader.GetInt32(3),
                reader.GetString(4),
                reader.IsDBNull(5) ? null : (TagDataType?)reader.GetInt16(5));
            if (includeProvenance)
            {
                var policyEvidence = ReadCapturePolicyEvidence(
                    reader,
                    11,
                    reader.IsDBNull(10) ? null : reader.GetGuid(10),
                    ReadFingerprint(reader, 12));
                sample = sample with
                {
                    SourceTimestamp = reader.IsDBNull(6) ? null : ReadTimestamp(reader, 6),
                    SourceTimestampEvidence = ReadEvidence(
                        reader,
                        7,
                        timestampPresent: !reader.IsDBNull(6)),
                    ServerTimestamp = reader.IsDBNull(8) ? null : ReadTimestamp(reader, 8),
                    ServerTimestampEvidence = ReadEvidence(
                        reader,
                        9,
                        timestampPresent: !reader.IsDBNull(8),
                        server: true),
                    CapturePolicyState = policyEvidence.State,
                    CaptureProfileId = policyEvidence.ProfileId,
                    CapturePolicyFingerprint = policyEvidence.Fingerprint
                };
            }
            materialized.Add(sample);
        }

        var hasMore = materialized.Count > query.PageSize;
        if (hasMore) materialized.RemoveAt(materialized.Count - 1);
        var rows = materialized.Select(sample => ToRow(sample, tags, includeProvenance)).ToArray();
        var next = hasMore && materialized.Count > 0
            ? Position(materialized[^1])
            : null;
        return new HistoricalProviderPage(rows, next);
    }

    private static string BuildFilter(
        HistoricalFilter filter,
        IReadOnlyCollection<TagDefinition> tags,
        NpgsqlCommand command,
        ref int parameterIndex) => filter.Field switch
        {
            "tag.id" => BuildGuidFilter("tag_id", filter, command, ref parameterIndex),
            "tag.path" => BuildPathFilter(filter, tags, command, ref parameterIndex),
            "quality" => BuildQualityFilter(filter, command, ref parameterIndex),
            "value" => BuildValueFilter(filter, command, ref parameterIndex),
            "timestamp" => BuildTimestampFilter("ts", filter, command, ref parameterIndex),
            _ => throw new ArgumentException(
                $"Historian filter field '{filter.Field}' is not supported.",
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
            command.Parameters.AddWithValue(
                name,
                NpgsqlDbType.Array | NpgsqlDbType.Uuid,
                values);
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
                HistoricalFilterOperator.Eq =>
                    candidate.Equals(filter.Values[0].Value, StringComparison.OrdinalIgnoreCase),
                HistoricalFilterOperator.NotEq =>
                    !candidate.Equals(filter.Values[0].Value, StringComparison.OrdinalIgnoreCase),
                HistoricalFilterOperator.In =>
                    filter.Values.Any(value =>
                        candidate.Equals(value.Value, StringComparison.OrdinalIgnoreCase)),
                HistoricalFilterOperator.Contains =>
                    candidate.Contains(filter.Values[0].Value!, StringComparison.OrdinalIgnoreCase),
                HistoricalFilterOperator.StartsWith =>
                    candidate.StartsWith(filter.Values[0].Value!, StringComparison.OrdinalIgnoreCase),
                _ => throw new ArgumentException("Unsupported path filter operator.", nameof(filter))
            };
        }

        var ids = tags
            .Where(Matches)
            .Select(static tag => tag.Id)
            .Distinct()
            .ToArray();
        var name = $"p{parameterIndex++}";
        command.Parameters.AddWithValue(
            name,
            NpgsqlDbType.Array | NpgsqlDbType.Uuid,
            ids);
        return $"tag_id = ANY(@{name})";
    }

    private static string BuildQualityFilter(
        HistoricalFilter filter,
        NpgsqlCommand command,
        ref int parameterIndex)
    {
        var values = filter.Values.Select(value =>
        {
            if (!Enum.TryParse<TagQuality>(value.Value, ignoreCase: true, out var parsed) ||
                !Enum.IsDefined(parsed))
                throw new ArgumentException(
                    $"Unknown TAG quality '{value.Value}'.",
                    nameof(filter));
            return (int)parsed;
        }).ToArray();

        if (filter.Operator == HistoricalFilterOperator.In)
        {
            var name = $"p{parameterIndex++}";
            command.Parameters.AddWithValue(
                name,
                NpgsqlDbType.Array | NpgsqlDbType.Integer,
                values);
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
        static string Json(HistoricalQueryValue value) => value.Kind switch
        {
            HistoricalValueKind.String or HistoricalValueKind.Enum =>
                JsonSerializer.Serialize(value.Value),
            HistoricalValueKind.Int16 or HistoricalValueKind.Int32 or HistoricalValueKind.Int64 =>
                value.Value!,
            HistoricalValueKind.Float or HistoricalValueKind.Double or HistoricalValueKind.Number =>
                value.Value!,
            HistoricalValueKind.Boolean => value.AsBoolean() ? "true" : "false",
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
        command.Parameters.AddWithValue(
            parameter,
            NpgsqlDbType.Jsonb,
            Json(filter.Values[0]));
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
            command.Parameters.AddWithValue(
                name,
                NpgsqlDbType.Array | NpgsqlDbType.TimestampTz,
                values);
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

    private static HistoricalQueryRow ToRow(
        SampleRow sample,
        IReadOnlyCollection<TagDefinition> tags,
        bool includeProvenance)
    {
        var tag = tags.FirstOrDefault(candidate => candidate.Id == sample.TagId);
        var cells = new Dictionary<string, HistoricalQueryValue>(StringComparer.Ordinal)
        {
            ["tag.id"] = HistoricalQueryValue.FromGuid(sample.TagId),
            ["tag.path"] = tag is null
                ? HistoricalQueryValue.Null()
                : HistoricalQueryValue.FromString(tag.Path),
            ["quality"] = HistoricalQueryValue.FromEnum(sample.Quality.ToString()),
            ["value"] = ReadValue(sample.ValueJson, sample.DataType),
            ["timestamp"] = HistoricalQueryValue.FromDateTime(sample.Timestamp)
        };
        if (includeProvenance)
        {
            cells["source.timestamp"] = sample.SourceTimestamp is { } sourceTimestamp
                ? HistoricalQueryValue.FromDateTime(sourceTimestamp)
                : HistoricalQueryValue.Null();
            cells["source.timestamp.status"] = HistoricalQueryValue.FromEnum(sample.SourceTimestampEvidence.ToString());
            cells["server.timestamp"] = sample.ServerTimestamp is { } serverTimestamp
                ? HistoricalQueryValue.FromDateTime(serverTimestamp)
                : HistoricalQueryValue.Null();
            cells["server.timestamp.status"] = HistoricalQueryValue.FromEnum(sample.ServerTimestampEvidence.ToString());
            cells["capture.policy.profileId"] = sample.CaptureProfileId is { } profileId
                ? HistoricalQueryValue.FromGuid(profileId)
                : HistoricalQueryValue.Null();
            cells["capture.policy.fingerprint"] = sample.CapturePolicyFingerprint is { } fingerprint
                ? HistoricalQueryValue.FromString(
                    HistorianCapturePolicySettings.FingerprintTextPrefix +
                    Convert.ToHexString(fingerprint).ToLowerInvariant())
                : HistoricalQueryValue.Null();
            cells["capture.policy.status"] = HistoricalQueryValue.FromEnum(sample.CapturePolicyState.ToString());
        }
        return new HistoricalQueryRow(cells);
    }

    private static TimestampEvidenceState ReadEvidence(
        NpgsqlDataReader reader,
        int ordinal,
        bool timestampPresent,
        bool server = false)
    {
        if (reader.IsDBNull(ordinal))
            return timestampPresent
                ? TimestampEvidenceState.PresentUnverified
                : TimestampEvidenceState.NotRecorded;
        var raw = reader.GetInt16(ordinal);
        if (!Enum.IsDefined(typeof(TimestampEvidenceState), raw))
            return timestampPresent
                ? TimestampEvidenceState.PresentUnverified
                : TimestampEvidenceState.NotRecorded;
        var state = (TimestampEvidenceState)raw;
        if (state == TimestampEvidenceState.NotRecorded)
            return state;
        if (state == TimestampEvidenceState.NotProvided)
            return timestampPresent ? TimestampEvidenceState.PresentUnverified : state;
        if (!timestampPresent)
            return TimestampEvidenceState.NotRecorded;
        return server && state is TimestampEvidenceState.Synchronized or TimestampEvidenceState.Unsynchronized
            ? TimestampEvidenceState.PresentUnverified
            : state;
    }

    private static byte[]? ReadFingerprint(NpgsqlDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
            return null;

        var fingerprint = reader.GetFieldValue<byte[]>(ordinal);
        return fingerprint.Length == 16 ? fingerprint : null;
    }

    private static CapturePolicyEvidence ReadCapturePolicyEvidence(
        NpgsqlDataReader reader,
        int ordinal,
        Guid? profileId,
        byte[]? fingerprint)
    {
        if (reader.IsDBNull(ordinal))
            return CapturePolicyEvidence.NotRecorded;

        var raw = reader.GetInt16(ordinal);
        if (!Enum.IsDefined(typeof(HistorianCapturePolicyEvidenceState), raw))
            return CapturePolicyEvidence.NotRecorded;

        var state = (HistorianCapturePolicyEvidenceState)raw;
        return state switch
        {
            HistorianCapturePolicyEvidenceState.NotRecorded => CapturePolicyEvidence.NotRecorded,
            HistorianCapturePolicyEvidenceState.LegacyDefault when profileId is null && fingerprint is null =>
                new(HistorianCapturePolicyEvidenceState.LegacyDefault, null, null),
            HistorianCapturePolicyEvidenceState.Explicit when
                fingerprint is not null && (!profileId.HasValue || profileId.Value != Guid.Empty) =>
                new(HistorianCapturePolicyEvidenceState.Explicit, profileId, fingerprint),
            _ => CapturePolicyEvidence.NotRecorded
        };
    }

    private static HistoricalQueryValue ReadValue(string json, TagDataType? dataType)
    {
        using var document = JsonDocument.Parse(json);
        var value = document.RootElement;
        if (value.ValueKind == JsonValueKind.Null)
            return HistoricalQueryValue.Null();

        if (dataType.HasValue)
            return ReadTypedValue(value, dataType.Value);

        return value.ValueKind switch
        {
            JsonValueKind.True => HistoricalQueryValue.FromBoolean(true),
            JsonValueKind.False => HistoricalQueryValue.FromBoolean(false),
            JsonValueKind.Number when value.TryGetInt64(out var integer) =>
                HistoricalQueryValue.FromInt64(integer),
            JsonValueKind.Number => HistoricalQueryValue.FromNumber(value.GetDouble()),
            JsonValueKind.String => HistoricalQueryValue.FromString(value.GetString() ?? string.Empty),
            _ => HistoricalQueryValue.FromString(value.GetRawText())
        };
    }

    private static HistoricalQueryValue ReadTypedValue(
        JsonElement value,
        TagDataType dataType) => dataType switch
    {
        TagDataType.Boolean when value.ValueKind is JsonValueKind.True or JsonValueKind.False =>
            HistoricalQueryValue.FromBoolean(value.GetBoolean()),

        TagDataType.Int16 when value.TryGetInt32(out var int16) &&
                               int16 is >= short.MinValue and <= short.MaxValue =>
            HistoricalQueryValue.FromInt16((short)int16),

        TagDataType.Int32 when value.TryGetInt32(out var int32) =>
            HistoricalQueryValue.FromInt32(int32),

        TagDataType.Int64 when value.TryGetInt64(out var int64) =>
            HistoricalQueryValue.FromInt64(int64),

        TagDataType.Float when value.ValueKind == JsonValueKind.Number =>
            HistoricalQueryValue.FromFloat(value.GetSingle()),

        TagDataType.Double when value.ValueKind == JsonValueKind.Number =>
            HistoricalQueryValue.FromDouble(value.GetDouble()),

        TagDataType.String when value.ValueKind == JsonValueKind.String =>
            HistoricalQueryValue.FromString(value.GetString() ?? string.Empty),

        TagDataType.DateTime when value.ValueKind == JsonValueKind.String &&
                                  DateTimeOffset.TryParse(
                                      value.GetString(),
                                      CultureInfo.InvariantCulture,
                                      DateTimeStyles.RoundtripKind,
                                      out var timestamp) =>
            HistoricalQueryValue.FromDateTime(timestamp),

        TagDataType.Enum when value.ValueKind == JsonValueKind.String =>
            HistoricalQueryValue.FromEnum(value.GetString() ?? string.Empty),

        TagDataType.Enum when value.ValueKind == JsonValueKind.Number =>
            HistoricalQueryValue.FromEnum(value.GetRawText()),

        _ => throw new InvalidOperationException(
            $"Persisted historian value does not match declared TAG data type '{dataType}'.")
    };

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

    private sealed record SampleRow(
        long SampleId,
        Guid TagId,
        DateTimeOffset Timestamp,
        TagQuality Quality,
        string ValueJson,
        TagDataType? DataType,
        DateTimeOffset? SourceTimestamp = null,
        TimestampEvidenceState SourceTimestampEvidence = TimestampEvidenceState.NotRecorded,
        DateTimeOffset? ServerTimestamp = null,
        TimestampEvidenceState ServerTimestampEvidence = TimestampEvidenceState.NotRecorded,
        HistorianCapturePolicyEvidenceState CapturePolicyState = HistorianCapturePolicyEvidenceState.NotRecorded,
        Guid? CaptureProfileId = null,
        byte[]? CapturePolicyFingerprint = null);

    private sealed record CapturePolicyEvidence(
        HistorianCapturePolicyEvidenceState State,
        Guid? ProfileId,
        byte[]? Fingerprint)
    {
        public static CapturePolicyEvidence NotRecorded { get; } =
            new(HistorianCapturePolicyEvidenceState.NotRecorded, null, null);
    }
}
