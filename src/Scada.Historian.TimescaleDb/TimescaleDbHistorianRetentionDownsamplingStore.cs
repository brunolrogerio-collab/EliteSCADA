using System.Text.Json;
using Scada.Core.Persistence;
using Npgsql;
using NpgsqlTypes;
using Scada.Core.Tags;
using Scada.Historian.Abstractions;
using Scada.Historian.Aggregation;
using Scada.Historian.Policies;

namespace Scada.Historian.TimescaleDb;

public sealed class TimescaleDbHistorianRetentionDownsamplingStore : IHistorianRetentionDownsamplingStore
{
    private const int MaximumChunkIntervalsPerEnforcementRun = 4;
    private readonly NpgsqlDataSource _dataSource;
    private readonly IDurableWriteAdmission? _writeAdmission;

    public TimescaleDbHistorianRetentionDownsamplingStore(
        string connectionString,
        IDurableWriteAdmission? writeAdmission = null)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("TimescaleDB connection string is required.", nameof(connectionString));
        _dataSource = NpgsqlDataSource.Create(connectionString);
        _writeAdmission = writeAdmission;
    }

    public Task EnsureInfrastructureAsync(CancellationToken cancellationToken = default) =>
        TimescaleHistorianInfrastructure.EnsureAllAsync(_dataSource, cancellationToken);

    public async Task<HistorianStoragePolicy?> GetAppliedPolicyAsync(CancellationToken cancellationToken = default)
    {
        const string existsSql = "SELECT to_regclass('elitescada.historian_storage_policy_state') IS NOT NULL;";
        await using (var exists = _dataSource.CreateCommand(existsSql))
        {
            if (!Equals(await exists.ExecuteScalarAsync(cancellationToken), true)) return null;
        }

        const string sql = """
            SELECT policy_json::text
            FROM elitescada.historian_storage_policy_state
            WHERE singleton_id = 1;
            """;
        await using var command = _dataSource.CreateCommand(sql);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null or DBNull
            ? null
            : HistorianPolicyJson.Deserialize((string)result);
    }

    public async Task<HistorianStorageUsage> GetStorageUsageAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                pg_database_size(current_database()),
                CASE
                    WHEN to_regclass('elitescada.tag_history') IS NULL THEN NULL
                    ELSE hypertable_size('elitescada.tag_history'::regclass)
                END,
                COALESCE((
                    SELECT SUM(hypertable_size(
                        format('%I.%I', materialization_hypertable_schema, materialization_hypertable_name)::regclass))
                    FROM timescaledb_information.continuous_aggregates
                    WHERE view_schema = 'elitescada'
                      AND view_name = ANY(@aggregate_views)
                )::bigint, 0::bigint),
                clock_timestamp();
            """;
        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue(
            "aggregate_views",
            NpgsqlDbType.Array | NpgsqlDbType.Text,
            TimescaleHistorianSchema.SupportedBuckets
                .Select(bucket => TimescaleHistorianSchema.AggregateViewName(bucket).Split('.').Last())
                .ToArray());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new InvalidOperationException("Historian storage usage query returned no row.");

        return new HistorianStorageUsage(
            reader.GetInt64(0),
            reader.IsDBNull(1) ? null : reader.GetInt64(1),
            reader.IsDBNull(2) ? null : reader.GetInt64(2),
            reader.GetFieldValue<DateTimeOffset>(3).ToUniversalTime());
    }

    public async Task<HistorianStorageLimitEnforcementResult> EnforceRawSampleStorageLimitAsync(
        CancellationToken cancellationToken = default)
    {
        await using var durableWrite = await _writeAdmission.AcquireOptionalAsync(
            "historian-raw-size-limit",
            cancellationToken);

        const string stateExistsSql = "SELECT to_regclass('elitescada.historian_storage_policy_state') IS NOT NULL;";
        await using (var exists = _dataSource.CreateCommand(stateExistsSql))
        {
            if (!Equals(await exists.ExecuteScalarAsync(cancellationToken), true))
                return Result(HistorianStorageLimitEnforcementStatus.NoPolicy);
        }

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string policyExistsSql = "SELECT EXISTS (SELECT 1 FROM elitescada.historian_storage_policy_state WHERE singleton_id = 1);";
        await using (var policyExists = new NpgsqlCommand(policyExistsSql, connection, transaction))
        {
            if (!Equals(await policyExists.ExecuteScalarAsync(cancellationToken), true))
                return Result(HistorianStorageLimitEnforcementStatus.NoPolicy);
        }

        // This row lock is database-scoped. When nodes share the Historian database,
        // only one can enforce the same persisted policy at a time. SKIP LOCKED keeps
        // a second node from waiting behind an admin update or another maintenance pass.
        const string policySql = """
            SELECT policy_json::text
            FROM elitescada.historian_storage_policy_state
            WHERE singleton_id = 1
            FOR UPDATE SKIP LOCKED;
            """;
        string? policyJson;
        await using (var policyCommand = new NpgsqlCommand(policySql, connection, transaction))
            policyJson = (string?)await policyCommand.ExecuteScalarAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(policyJson))
            return Result(HistorianStorageLimitEnforcementStatus.SkippedBusy);

        var policy = HistorianPolicyJson.Deserialize(policyJson);
        var limitBytes = policy.RawSampleStorageLimitBytes;
        if (!limitBytes.HasValue)
            return Result(HistorianStorageLimitEnforcementStatus.NoLimitConfigured);

        var rawBytesBefore = await ReadRawSampleBytesAsync(connection, transaction, cancellationToken);
        if (rawBytesBefore <= limitBytes.Value)
        {
            await transaction.CommitAsync(cancellationToken);
            return new HistorianStorageLimitEnforcementResult(
                HistorianStorageLimitEnforcementStatus.WithinLimit,
                limitBytes,
                rawBytesBefore,
                rawBytesBefore,
                0,
                DateTimeOffset.UtcNow);
        }

        if (policy.RawSampleStorageLimitAction != HistorianRawStorageLimitAction.DeleteOldestCompleteChunks)
        {
            await transaction.CommitAsync(cancellationToken);
            return new HistorianStorageLimitEnforcementResult(
                HistorianStorageLimitEnforcementStatus.WarningOnly,
                limitBytes,
                rawBytesBefore,
                rawBytesBefore,
                0,
                DateTimeOffset.UtcNow);
        }

        var deletedChunks = 0;
        var rawBytesAfter = rawBytesBefore;
        for (var interval = 0; interval < MaximumChunkIntervalsPerEnforcementRun && rawBytesAfter > limitBytes; interval++)
        {
            var cutoff = await ReadOldestCompletedChunkCutoffAsync(connection, transaction, cancellationToken);
            if (!cutoff.HasValue) break;

            var deletedThisInterval = await DropOldestChunkIntervalAsync(
                connection,
                transaction,
                cutoff.Value,
                cancellationToken);
            if (deletedThisInterval == 0) break;
            deletedChunks += deletedThisInterval;

            rawBytesAfter = await ReadRawSampleBytesAsync(connection, transaction, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        var status = deletedChunks > 0
            ? HistorianStorageLimitEnforcementStatus.ChunksRemoved
            : HistorianStorageLimitEnforcementStatus.NoEligibleChunks;
        return new HistorianStorageLimitEnforcementResult(
            status,
            limitBytes,
            rawBytesBefore,
            rawBytesAfter,
            deletedChunks,
            DateTimeOffset.UtcNow);
    }

    private static HistorianStorageLimitEnforcementResult Result(
        HistorianStorageLimitEnforcementStatus status) =>
        new(status, null, null, null, 0, DateTimeOffset.UtcNow);

    public async Task ApplyPolicyAsync(
        HistorianStoragePolicy policy,
        HistorianPolicyApplyOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        await using var durableWrite = await _writeAdmission.AcquireOptionalAsync("historian-policy", cancellationToken);
        ArgumentNullException.ThrowIfNull(policy);
        policy.Validate();
        options ??= new HistorianPolicyApplyOptions();

        await EnsureInfrastructureAsync(cancellationToken);
        var current = await GetAppliedPolicyWithoutInitializationAsync(cancellationToken);
        var nextJson = HistorianPolicyJson.Serialize(policy);
        var currentJson = current is null ? null : HistorianPolicyJson.Serialize(current);
        if (string.Equals(currentJson, nextJson, StringComparison.Ordinal)) return;

        if (HistorianPolicySafety.RequiresExplicitDataExpirationApproval(current, policy) &&
            !options.AllowPotentialDataExpiration)
        {
            throw new InvalidOperationException(
                "Historian retention change may expire existing data. Re-apply with explicit AllowPotentialDataExpiration approval.");
        }

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await ReconcileRetentionPolicyAsync(
            connection,
            transaction,
            TimescaleHistorianSchema.RawTable,
            policy.RawRetention,
            cancellationToken);

        var tiers = policy.EffectiveDownsampling.ToDictionary(x => x.Bucket);
        foreach (var bucket in TimescaleHistorianSchema.SupportedBuckets)
        {
            tiers.TryGetValue(bucket, out var tier);
            tier ??= new HistorianDownsamplingRule(bucket, Enabled: false);
            var view = TimescaleHistorianSchema.AggregateViewName(bucket);

            await ReconcileRefreshPolicyAsync(
                connection,
                transaction,
                view,
                bucket,
                tier,
                cancellationToken);
            await ReconcileRetentionPolicyAsync(
                connection,
                transaction,
                view,
                tier.EffectiveRetention,
                cancellationToken);
        }

        const string saveStateSql = """
            INSERT INTO elitescada.historian_storage_policy_state(singleton_id, policy_json, applied_at)
            VALUES (1, @policy_json::jsonb, now())
            ON CONFLICT (singleton_id) DO UPDATE
            SET policy_json = EXCLUDED.policy_json,
                applied_at = EXCLUDED.applied_at;
            """;
        await using (var save = new NpgsqlCommand(saveStateSql, connection, transaction))
        {
            save.Parameters.AddWithValue("policy_json", NpgsqlDbType.Text, nextJson);
            await save.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task RefreshAggregateAsync(
        HistorianBucketWidth bucket,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        await using var durableWrite = await _writeAdmission.AcquireOptionalAsync("historian-aggregate-refresh", cancellationToken);
        if (to <= from)
            throw new ArgumentException("Aggregate refresh end must be greater than start.");
        await EnsureInfrastructureAsync(cancellationToken);

        var view = TimescaleHistorianSchema.AggregateViewName(bucket);
        var sql = $"CALL refresh_continuous_aggregate('{view}', @from, @to);";
        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("from", NpgsqlDbType.TimestampTz, from.UtcDateTime);
        command.Parameters.AddWithValue("to", NpgsqlDbType.TimestampTz, to.UtcDateTime);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<HistorianAggregateBucket>> QueryAggregatesAsync(
        Guid tagId,
        HistorianBucketWidth bucket,
        DateTimeOffset from,
        DateTimeOffset to,
        int limit = 5000,
        CancellationToken cancellationToken = default)
    {
        if (tagId == Guid.Empty)
            throw new ArgumentException("Historian query TAG ID cannot be empty.", nameof(tagId));
        if (to < from)
            throw new ArgumentException("Historian aggregate query end must be greater than or equal to start.");

        await EnsureInfrastructureAsync(cancellationToken);
        var take = Math.Clamp(limit, 1, 50_000);
        var view = TimescaleHistorianSchema.AggregateViewName(bucket);
        var sql = $"""
            SELECT
                bucket_start,
                sample_count,
                good_count,
                uncertain_count,
                bad_count,
                numeric_good_count,
                numeric_minimum,
                numeric_maximum,
                numeric_average,
                first_value::text,
                first_quality,
                first_data_type,
                last_value::text,
                last_quality,
                last_data_type,
                min_data_type,
                max_data_type
            FROM {view}
            WHERE tag_id = @tag_id
              AND bucket_start >= @from
              AND bucket_start < @to
            ORDER BY bucket_start
            LIMIT @limit;
            """;

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("tag_id", NpgsqlDbType.Uuid, tagId);
        command.Parameters.AddWithValue("from", NpgsqlDbType.TimestampTz, from.UtcDateTime);
        command.Parameters.AddWithValue("to", NpgsqlDbType.TimestampTz, to.UtcDateTime);
        command.Parameters.AddWithValue("limit", NpgsqlDbType.Integer, take);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<HistorianAggregateBucket>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var bucketStart = ReadTimestamp(reader, 0);
            var minType = ReadTagDataType(reader, 15);
            var maxType = ReadTagDataType(reader, 16);
            var consistent = minType.HasValue && maxType.HasValue && minType == maxType;

            result.Add(new HistorianAggregateBucket(
                tagId,
                bucket,
                bucketStart,
                bucketStart + bucket.ToTimeSpan(),
                reader.GetInt64(1),
                reader.GetInt64(2),
                reader.GetInt64(3),
                reader.GetInt64(4),
                reader.GetInt64(5),
                reader.IsDBNull(6) ? null : reader.GetDouble(6),
                reader.IsDBNull(7) ? null : reader.GetDouble(7),
                reader.IsDBNull(8) ? null : reader.GetDouble(8),
                DeserializeValue(reader.IsDBNull(9) ? null : reader.GetString(9)),
                (TagQuality)reader.GetInt32(10),
                DeserializeValue(reader.IsDBNull(12) ? null : reader.GetString(12)),
                (TagQuality)reader.GetInt32(13),
                consistent ? minType : null,
                consistent));
        }

        return result;
    }

    private async Task<HistorianStoragePolicy?> GetAppliedPolicyWithoutInitializationAsync(
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT policy_json::text
            FROM elitescada.historian_storage_policy_state
            WHERE singleton_id = 1;
            """;
        await using var command = _dataSource.CreateCommand(sql);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null or DBNull
            ? null
            : HistorianPolicyJson.Deserialize((string)result);
    }

    private static async Task<long> ReadRawSampleBytesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        const string sql = "SELECT hypertable_size('elitescada.tag_history'::regclass);";
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        if (result is null or DBNull)
            throw new InvalidOperationException("Historian raw sample size is unavailable.");
        return Convert.ToInt64(result, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task<DateTimeOffset?> ReadOldestCompletedChunkCutoffAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT range_end
            FROM timescaledb_information.chunks
            WHERE hypertable_schema = 'elitescada'
              AND hypertable_name = 'tag_history'
              AND range_start IS NOT NULL
              AND range_end IS NOT NULL
              AND range_end <= clock_timestamp()
              AND range_start < (
                  SELECT MAX(range_start)
                  FROM timescaledb_information.chunks
                  WHERE hypertable_schema = 'elitescada'
                    AND hypertable_name = 'tag_history'
                    AND range_start IS NOT NULL)
            ORDER BY range_start, range_end
            LIMIT 1;
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        if (result is null or DBNull) return null;

        var rangeEnd = result switch
        {
            DateTimeOffset offset => offset.ToUniversalTime(),
            DateTime timestamp => new DateTimeOffset(
                timestamp.Kind == DateTimeKind.Utc ? timestamp : timestamp.ToUniversalTime()),
            _ => throw new InvalidOperationException("Historian chunk metadata returned an unsupported time value.")
        };

        // drop_chunks uses a strict end-time comparison. One PostgreSQL timestamp
        // quantum selects the entire oldest completed interval and no later one.
        return rangeEnd.AddTicks(10);
    }

    private static async Task<int> DropOldestChunkIntervalAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        DateTimeOffset cutoff,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT drop_chunks(
                relation => 'elitescada.tag_history'::regclass,
                older_than => @cutoff,
                verbose => FALSE);
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("cutoff", NpgsqlDbType.TimestampTz, cutoff.UtcDateTime);
        var dropped = 0;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            dropped++;
        return dropped;
    }

    private static async Task ReconcileRetentionPolicyAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string relation,
        HistorianRetentionRule retention,
        CancellationToken cancellationToken)
    {
        var removeSql = $"SELECT remove_retention_policy('{relation}', if_exists => TRUE);";
        await using (var remove = new NpgsqlCommand(removeSql, connection, transaction))
            await remove.ExecuteNonQueryAsync(cancellationToken);

        if (!retention.Enabled) return;

        var addSql = $"SELECT add_retention_policy('{relation}', @drop_after, if_not_exists => TRUE);";
        await using var add = new NpgsqlCommand(addSql, connection, transaction);
        add.Parameters.AddWithValue("drop_after", NpgsqlDbType.Interval, retention.Duration!.Value);
        await add.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task ReconcileRefreshPolicyAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string view,
        HistorianBucketWidth bucket,
        HistorianDownsamplingRule tier,
        CancellationToken cancellationToken)
    {
        var removeSql = $"SELECT remove_continuous_aggregate_policy('{view}', if_exists => TRUE);";
        await using (var remove = new NpgsqlCommand(removeSql, connection, transaction))
            await remove.ExecuteNonQueryAsync(cancellationToken);

        if (!tier.Enabled) return;

        var addSql = $"""
            SELECT add_continuous_aggregate_policy(
                '{view}',
                start_offset => @start_offset,
                end_offset => @end_offset,
                schedule_interval => @schedule_interval,
                if_not_exists => TRUE);
            """;
        await using var add = new NpgsqlCommand(addSql, connection, transaction);
        add.Parameters.AddWithValue("start_offset", NpgsqlDbType.Interval, tier.RefreshLookback!.Value);
        add.Parameters.AddWithValue("end_offset", NpgsqlDbType.Interval, bucket.ToTimeSpan());
        add.Parameters.AddWithValue("schedule_interval", NpgsqlDbType.Interval, tier.RefreshInterval!.Value);
        await add.ExecuteNonQueryAsync(cancellationToken);
    }

    private static TagDataType? ReadTagDataType(NpgsqlDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal)) return null;
        var raw = reader.GetInt16(ordinal);
        return Enum.IsDefined(typeof(TagDataType), (int)raw) ? (TagDataType)raw : null;
    }

    private static object? DeserializeValue(string? json)
    {
        if (json is null) return null;
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        return root.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => root.GetString(),
            JsonValueKind.Number when root.TryGetInt64(out var integer) => integer,
            JsonValueKind.Number => root.GetDouble(),
            _ => root.GetRawText()
        };
    }

    private static DateTimeOffset ReadTimestamp(NpgsqlDataReader reader, int ordinal)
    {
        var value = reader.GetFieldValue<DateTime>(ordinal);
        return new DateTimeOffset(value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime());
    }

    public ValueTask DisposeAsync() => _dataSource.DisposeAsync();
}
