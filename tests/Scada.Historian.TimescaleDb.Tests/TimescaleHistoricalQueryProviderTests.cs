using Scada.Core.Events;
using Scada.Core.HistoricalQueries;
using Scada.Core.Tags;
using Scada.Historian.Policies;
using Scada.Historian.TimescaleDb;
using Npgsql;
using NpgsqlTypes;
using System.Diagnostics;
using System.Text.Json;
using Xunit.Abstractions;

namespace Scada.Historian.TimescaleDb.Tests;

public sealed class TimescaleHistoricalQueryProviderTests
{
    private readonly ITestOutputHelper _output;

    public TimescaleHistoricalQueryProviderTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task Provider_V2RoundTripsEvidencePolicyAndKeepsV1ProjectionFrozen()
    {
        var connectionString = Environment.GetEnvironmentVariable("ELITESCADA_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var eventBus = new InMemoryScadaEventBus();
        var registry = new InMemoryTagRegistry();
        var profileId = Guid.NewGuid();
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["historian.captureProfileId"] = profileId.ToString("D"),
            [HistorianCapturePolicy.EnabledMetadataKey] = "true",
            [HistorianCapturePolicy.StrategyMetadataKey] = "periodic",
            [HistorianCapturePolicy.PeriodMetadataKey] = "250"
        };
        var profiledTag = registry.Register(TagDefinition.Create(
            "Profiled",
            $"Integration.Historical.Provenance.Profiled.{Guid.NewGuid():N}",
            TagDataType.Double,
            metadata: metadata));
        var noTimestampTag = registry.Register(TagDefinition.Create(
            "NoTimestamp",
            $"Integration.Historical.Provenance.NoTimestamp.{Guid.NewGuid():N}",
            TagDataType.Int32));
        var inlinePolicyTag = registry.Register(TagDefinition.Create(
            "InlinePolicy",
            $"Integration.Historical.Provenance.InlinePolicy.{Guid.NewGuid():N}",
            TagDataType.Int32,
            metadata: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [HistorianCapturePolicy.EnabledMetadataKey] = "true",
                [HistorianCapturePolicy.StrategyMetadataKey] = "onChange"
            }));
        var legacyTag = registry.Register(TagDefinition.Create(
            "LegacyWriter",
            $"Integration.Historical.Provenance.Legacy.{Guid.NewGuid():N}",
            TagDataType.Int32));

        var observedAt = DateTimeOffset.Parse("2026-10-14T12:00:00.123456Z");
        var migrationClock = Stopwatch.StartNew();
        await using var provider = new TimescaleHistoricalQueryProvider(connectionString, registry);
        _ = await provider.QueryVersion2Async(new HistoricalQueryExecution(
            HistoricalQueryCatalog.Require(HistoricalDatasets.HistorianSamplesV2),
            new HistoricalResolvedRange(
                observedAt.AddMinutes(-1),
                DateTimeOffset.UtcNow.AddSeconds(1)),
            Array.Empty<HistoricalFilter>(),
            null,
            new HistoricalSort(),
            1,
            null));
        migrationClock.Stop();
        long outstandingInfrastructureLocks;
        await using (var dataSource = NpgsqlDataSource.Create(connectionString))
        await using (var connection = await dataSource.OpenConnectionAsync())
        await using (var command = new NpgsqlCommand("""
            SELECT count(*)
            FROM pg_locks
            WHERE locktype = 'advisory'
              AND database = (SELECT oid FROM pg_database WHERE datname = current_database())
              AND classid = ((@lock_key >> 32) & 4294967295)::oid
              AND objid = (@lock_key & 4294967295)::oid
              AND objsubid = 1;
            """, connection))
        {
            command.Parameters.AddWithValue("lock_key", NpgsqlDbType.Bigint, 4993446713136202561L);
            outstandingInfrastructureLocks = (long)(await command.ExecuteScalarAsync() ?? 0L);
        }
        Assert.Equal(0, outstandingInfrastructureLocks);
        _output.WriteLine(
            "Legacy tag_history forward migration + provider initialization + empty first query: " +
            $"{migrationClock.Elapsed.TotalMilliseconds:F2} ms; infrastructure locks still held after initialization={outstandingInfrastructureLocks}. " +
            "Elapsed time includes DDL/index initialization and query startup, not an isolated lock duration.");
        await using var historian = new TimescaleDbHistorian(eventBus, connectionString, batchSize: 10);
        var sourceAt = DateTimeOffset.Parse("2024-04-05T06:07:08-03:00");
        var serverAt = DateTimeOffset.Parse("2024-04-05T06:07:09+02:00");
        var profiledValue = new TagValue(
            profiledTag.Id,
            17.25d,
            observedAt,
            TagQuality.Good,
            "dnp3")
        {
            SourceTimestamp = sourceAt,
            SourceTimestampEvidenceState = TimestampEvidenceState.Synchronized,
            ServerTimestamp = serverAt
        };
        var absentValue = new TagValue(
            noTimestampTag.Id,
            22,
            observedAt.AddMilliseconds(10),
            TagQuality.Good,
            "modbus");
        var inlinePolicyValue = new TagValue(
            inlinePolicyTag.Id,
            23,
            observedAt.AddMilliseconds(20),
            TagQuality.Good,
            "modbus");
        await eventBus.PublishAsync(new TagValueChanged(
            profiledTag, null, profiledValue, observedAt));
        await eventBus.PublishAsync(new TagValueChanged(
            noTimestampTag, null, absentValue, absentValue.Timestamp));
        await eventBus.PublishAsync(new TagValueChanged(
            inlinePolicyTag, null, inlinePolicyValue, inlinePolicyValue.Timestamp));
        await WaitForWritesAsync(historian, 3);

        var range = new HistoricalResolvedRange(observedAt.AddMinutes(-1), DateTimeOffset.UtcNow.AddSeconds(1));
        var v1 = await provider.QueryAsync(Execution(
            HistoricalQueryCatalog.Require(HistoricalDatasets.HistorianSamples),
            profiledTag.Id,
            range));
        var v1Row = Assert.Single(v1.Rows);
        Assert.Equal(
            new[] { "tag.id", "tag.path", "quality", "value", "timestamp" },
            v1Row.Cells.Keys.ToArray());
        Assert.Equal(HistoricalQueryValue.FromDateTime(observedAt).Value, v1Row.Cells["timestamp"].Value);

        var v2 = await provider.QueryVersion2Async(Execution(
            HistoricalQueryCatalog.Require(HistoricalDatasets.HistorianSamplesV2),
            profiledTag.Id,
            range));
        var v2Row = Assert.Single(v2.Rows);
        Assert.Equal(HistoricalValueKind.DateTime, v2Row.Cells["source.timestamp"].Kind);
        Assert.Equal(sourceAt.ToUniversalTime().ToString("O"), v2Row.Cells["source.timestamp"].Value);
        Assert.Equal("Synchronized", v2Row.Cells["source.timestamp.status"].Value);
        Assert.Equal(serverAt.ToUniversalTime().ToString("O"), v2Row.Cells["server.timestamp"].Value);
        Assert.Equal("PresentUnverified", v2Row.Cells["server.timestamp.status"].Value);
        Assert.Equal(profileId.ToString("D"), v2Row.Cells["capture.policy.profileId"].Value);
        Assert.Equal(
            HistorianCapturePolicy.Resolve(profiledTag).StableFingerprint,
            v2Row.Cells["capture.policy.fingerprint"].Value);
        Assert.Equal("Explicit", v2Row.Cells["capture.policy.status"].Value);
        Assert.Equal(observedAt, v2Row.Cells["timestamp"].AsDateTime());

        var absentV2 = await provider.QueryVersion2Async(Execution(
            HistoricalQueryCatalog.Require(HistoricalDatasets.HistorianSamplesV2),
            noTimestampTag.Id,
            range));
        var absentRow = Assert.Single(absentV2.Rows);
        Assert.Equal(HistoricalValueKind.Null, absentRow.Cells["source.timestamp"].Kind);
        Assert.Equal("NotProvided", absentRow.Cells["source.timestamp.status"].Value);
        Assert.Equal(HistoricalValueKind.Null, absentRow.Cells["server.timestamp"].Kind);
        Assert.Equal("NotProvided", absentRow.Cells["server.timestamp.status"].Value);
        Assert.Equal(HistoricalValueKind.Null, absentRow.Cells["capture.policy.profileId"].Kind);
        Assert.Equal(HistoricalValueKind.Null, absentRow.Cells["capture.policy.fingerprint"].Kind);
        Assert.Equal("LegacyDefault", absentRow.Cells["capture.policy.status"].Value);

        var inlinePolicyV2 = await provider.QueryVersion2Async(Execution(
            HistoricalQueryCatalog.Require(HistoricalDatasets.HistorianSamplesV2),
            inlinePolicyTag.Id,
            range));
        var inlinePolicyRow = Assert.Single(inlinePolicyV2.Rows);
        Assert.Equal(
            HistorianCapturePolicy.Resolve(inlinePolicyTag).StableFingerprint,
            inlinePolicyRow.Cells["capture.policy.fingerprint"].Value);
        Assert.Equal(HistoricalValueKind.Null, inlinePolicyRow.Cells["capture.policy.profileId"].Kind);
        Assert.Equal("Explicit", inlinePolicyRow.Cells["capture.policy.status"].Value);

        await using (var dataSource = NpgsqlDataSource.Create(connectionString))
        await using (var connection = await dataSource.OpenConnectionAsync())
        await using (var command = new NpgsqlCommand("""
            SELECT octet_length(capture_policy_fingerprint)
            FROM elitescada.tag_history
            WHERE tag_id = @tag_id AND ts = @ts;
            """, connection))
        {
            command.Parameters.AddWithValue("tag_id", NpgsqlDbType.Uuid, inlinePolicyTag.Id);
            command.Parameters.AddWithValue("ts", NpgsqlDbType.TimestampTz, inlinePolicyValue.Timestamp);
            Assert.Equal(16, await command.ExecuteScalarAsync());
        }

        await using (var dataSource = NpgsqlDataSource.Create(connectionString))
        await using (var connection = await dataSource.OpenConnectionAsync())
        await using (var command = new NpgsqlCommand("""
            INSERT INTO elitescada.tag_history (tag_id, ts, quality, source, value, data_type)
            VALUES (@tag_id, @ts, @quality, @source, @value, @data_type);
            """, connection))
        {
            command.Parameters.AddWithValue("tag_id", NpgsqlDbType.Uuid, legacyTag.Id);
            command.Parameters.AddWithValue("ts", NpgsqlDbType.TimestampTz, observedAt);
            command.Parameters.AddWithValue("quality", NpgsqlDbType.Integer, (int)TagQuality.Good);
            command.Parameters.AddWithValue("source", NpgsqlDbType.Text, "pre-v2-writer");
            command.Parameters.AddWithValue("value", NpgsqlDbType.Jsonb, "31");
            command.Parameters.AddWithValue("data_type", NpgsqlDbType.Smallint, (short)TagDataType.Int32);
            await command.ExecuteNonQueryAsync();

            await InsertSampleAsync(
                connection,
                legacyTag.Id,
                observedAt.AddMilliseconds(1),
                32,
                sourceTimestamp: sourceAt,
                sourceEvidence: null,
                serverTimestamp: null,
                serverEvidence: null);
            await InsertSampleAsync(
                connection,
                legacyTag.Id,
                observedAt.AddMilliseconds(2),
                33,
                sourceTimestamp: null,
                sourceEvidence: (short)TimestampEvidenceState.Synchronized,
                serverTimestamp: null,
                serverEvidence: (short)TimestampEvidenceState.NotProvided);
            await InsertSampleAsync(
                connection,
                legacyTag.Id,
                observedAt.AddMilliseconds(3),
                34,
                sourceTimestamp: sourceAt,
                sourceEvidence: (short)TimestampEvidenceState.NotProvided,
                serverTimestamp: null,
                serverEvidence: (short)TimestampEvidenceState.NotProvided);
            await InsertSampleAsync(
                connection,
                legacyTag.Id,
                observedAt.AddMilliseconds(4),
                35,
                sourceTimestamp: null,
                sourceEvidence: (short)TimestampEvidenceState.NotProvided,
                serverTimestamp: serverAt,
                serverEvidence: (short)TimestampEvidenceState.Synchronized);
            await InsertSampleAsync(
                connection,
                legacyTag.Id,
                observedAt.AddMilliseconds(5),
                36,
                sourceTimestamp: null,
                sourceEvidence: (short)TimestampEvidenceState.PresentUnverified,
                serverTimestamp: null,
                serverEvidence: null);
            await using var inconsistentPolicy = new NpgsqlCommand("""
                INSERT INTO elitescada.tag_history (
                    tag_id, ts, quality, source, value, data_type,
                    capture_profile_id, capture_policy_state)
                VALUES (
                    @tag_id, @ts, @quality, @source, @value, @data_type,
                    @profile_id, @policy_state);
                """, connection);
            inconsistentPolicy.Parameters.AddWithValue("tag_id", NpgsqlDbType.Uuid, legacyTag.Id);
            inconsistentPolicy.Parameters.AddWithValue("ts", NpgsqlDbType.TimestampTz, observedAt.AddMilliseconds(6));
            inconsistentPolicy.Parameters.AddWithValue("quality", NpgsqlDbType.Integer, (int)TagQuality.Good);
            inconsistentPolicy.Parameters.AddWithValue("source", NpgsqlDbType.Text, "inconsistent-policy-evidence");
            inconsistentPolicy.Parameters.AddWithValue("value", NpgsqlDbType.Jsonb, "37");
            inconsistentPolicy.Parameters.AddWithValue("data_type", NpgsqlDbType.Smallint, (short)TagDataType.Int32);
            inconsistentPolicy.Parameters.AddWithValue("profile_id", NpgsqlDbType.Uuid, profileId);
            inconsistentPolicy.Parameters.AddWithValue("policy_state", NpgsqlDbType.Smallint, (short)HistorianCapturePolicyEvidenceState.LegacyDefault);
            await inconsistentPolicy.ExecuteNonQueryAsync();

            await using var incompleteExplicitPolicy = new NpgsqlCommand("""
                INSERT INTO elitescada.tag_history (
                    tag_id, ts, quality, source, value, data_type,
                    capture_profile_id, capture_policy_state)
                VALUES (
                    @tag_id, @ts, @quality, @source, @value, @data_type,
                    @profile_id, @policy_state);
                """, connection);
            incompleteExplicitPolicy.Parameters.AddWithValue("tag_id", NpgsqlDbType.Uuid, legacyTag.Id);
            incompleteExplicitPolicy.Parameters.AddWithValue("ts", NpgsqlDbType.TimestampTz, observedAt.AddMilliseconds(7));
            incompleteExplicitPolicy.Parameters.AddWithValue("quality", NpgsqlDbType.Integer, (int)TagQuality.Good);
            incompleteExplicitPolicy.Parameters.AddWithValue("source", NpgsqlDbType.Text, "incomplete-explicit-policy-evidence");
            incompleteExplicitPolicy.Parameters.AddWithValue("value", NpgsqlDbType.Jsonb, "38");
            incompleteExplicitPolicy.Parameters.AddWithValue("data_type", NpgsqlDbType.Smallint, (short)TagDataType.Int32);
            incompleteExplicitPolicy.Parameters.AddWithValue("profile_id", NpgsqlDbType.Uuid, profileId);
            incompleteExplicitPolicy.Parameters.AddWithValue("policy_state", NpgsqlDbType.Smallint, (short)HistorianCapturePolicyEvidenceState.Explicit);
            await incompleteExplicitPolicy.ExecuteNonQueryAsync();
        }

        var legacyV2 = await provider.QueryVersion2Async(Execution(
            HistoricalQueryCatalog.Require(HistoricalDatasets.HistorianSamplesV2),
            legacyTag.Id,
            range));
        var legacyRow = Assert.Single(
            legacyV2.Rows,
            row => row.Cells["value"].Value == "31");
        Assert.Equal("NotRecorded", legacyRow.Cells["source.timestamp.status"].Value);
        Assert.Equal("NotRecorded", legacyRow.Cells["server.timestamp.status"].Value);
        Assert.Equal(HistoricalValueKind.Null, legacyRow.Cells["capture.policy.profileId"].Kind);
        Assert.Equal(HistoricalValueKind.Null, legacyRow.Cells["capture.policy.fingerprint"].Kind);
        Assert.Equal("NotRecorded", legacyRow.Cells["capture.policy.status"].Value);

        var inconsistentPolicyRow = Assert.Single(
            legacyV2.Rows,
            row => row.Cells["value"].Value == "37");
        Assert.Equal("NotRecorded", inconsistentPolicyRow.Cells["capture.policy.status"].Value);
        Assert.Equal(HistoricalValueKind.Null, inconsistentPolicyRow.Cells["capture.policy.profileId"].Kind);
        Assert.Equal(HistoricalValueKind.Null, inconsistentPolicyRow.Cells["capture.policy.fingerprint"].Kind);

        var incompleteExplicitPolicyRow = Assert.Single(
            legacyV2.Rows,
            row => row.Cells["value"].Value == "38");
        Assert.Equal("NotRecorded", incompleteExplicitPolicyRow.Cells["capture.policy.status"].Value);
        Assert.Equal(HistoricalValueKind.Null, incompleteExplicitPolicyRow.Cells["capture.policy.profileId"].Kind);
        Assert.Equal(HistoricalValueKind.Null, incompleteExplicitPolicyRow.Cells["capture.policy.fingerprint"].Kind);

        AssertEvidence(legacyV2.Rows, 32, "PresentUnverified", "NotRecorded");
        AssertEvidence(legacyV2.Rows, 33, "NotRecorded", "NotProvided");
        AssertEvidence(legacyV2.Rows, 34, "PresentUnverified", "NotProvided");
        AssertEvidence(legacyV2.Rows, 35, "NotProvided", "PresentUnverified");
        AssertEvidence(legacyV2.Rows, 36, "NotRecorded", "NotRecorded");

        var durableValue = Assert.Single(historian.Query(
            profiledTag.Id,
            observedAt.AddMinutes(-1),
            observedAt.AddMinutes(1)));
        Assert.Equal(sourceAt.ToUniversalTime(), durableValue.SourceTimestamp);
        Assert.Equal(TimestampEvidenceState.Synchronized, durableValue.SourceTimestampEvidenceState);
        Assert.Equal(serverAt.ToUniversalTime(), durableValue.ServerTimestamp);
        Assert.Equal(TimestampEvidenceState.PresentUnverified, durableValue.ServerTimestampEvidenceState);
    }

    [Fact]
    public async Task Provider_V2BoundsA50kObservedWindowToPage200AndReportsStorageAndWireCost()
    {
        var connectionString = Environment.GetEnvironmentVariable("ELITESCADA_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var registry = new InMemoryTagRegistry();
        var tag = registry.Register(TagDefinition.Create(
            "BoundedWindow",
            $"Integration.Historical.BoundedWindow.{Guid.NewGuid():N}",
            TagDataType.Int32));
        var defaultTag = registry.Register(TagDefinition.Create(
            "ImplicitDefaultPolicy",
            $"Integration.Historical.ImplicitDefault.{Guid.NewGuid():N}",
            TagDataType.Int32));
        await using var provider = new TimescaleHistoricalQueryProvider(connectionString, registry);
        var now = DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        var firstObserved = now.AddMinutes(-45);
        var staleSourceTimestamp = now.AddDays(-90);
        var serverTimestamp = staleSourceTimestamp.AddSeconds(2);
        var profileId = Guid.NewGuid();
        _ = await provider.QueryVersion2Async(new HistoricalQueryExecution(
            HistoricalQueryCatalog.Require(HistoricalDatasets.HistorianSamplesV2),
            new HistoricalResolvedRange(firstObserved.AddSeconds(-1), now),
            Array.Empty<HistoricalFilter>(),
            null,
            new HistoricalSort(),
            1,
            null));

        await using (var dataSource = NpgsqlDataSource.Create(connectionString))
        await using (var connection = await dataSource.OpenConnectionAsync())
        await using (var command = new NpgsqlCommand("""
            INSERT INTO elitescada.tag_history (
                tag_id, ts, quality, source, value, data_type,
                source_ts, source_ts_evidence, server_ts, server_ts_evidence,
                capture_profile_id, capture_policy_state, capture_policy_fingerprint)
            SELECT
                @tag_id,
                @first_observed + ((sample_index - 1) * INTERVAL '50 milliseconds'),
                @quality,
                @source,
                to_jsonb(sample_index),
                @data_type,
                @source_ts,
                @source_evidence,
                @server_ts,
                @server_evidence,
                @profile_id,
                @policy_state,
                @fingerprint
            FROM generate_series(1, 50000) AS sample_index;
            """, connection))
        {
            command.Parameters.AddWithValue("tag_id", NpgsqlDbType.Uuid, tag.Id);
            command.Parameters.AddWithValue("first_observed", NpgsqlDbType.TimestampTz, firstObserved);
            command.Parameters.AddWithValue("quality", NpgsqlDbType.Integer, (int)TagQuality.Good);
            command.Parameters.AddWithValue("source", NpgsqlDbType.Text, "bounded-window-test");
            command.Parameters.AddWithValue("data_type", NpgsqlDbType.Smallint, (short)TagDataType.Int32);
            command.Parameters.AddWithValue("source_ts", NpgsqlDbType.TimestampTz, staleSourceTimestamp);
            command.Parameters.AddWithValue("source_evidence", NpgsqlDbType.Smallint, (short)TimestampEvidenceState.PresentUnverified);
            command.Parameters.AddWithValue("server_ts", NpgsqlDbType.TimestampTz, serverTimestamp);
            command.Parameters.AddWithValue("server_evidence", NpgsqlDbType.Smallint, (short)TimestampEvidenceState.PresentUnverified);
            command.Parameters.AddWithValue("profile_id", NpgsqlDbType.Uuid, profileId);
            command.Parameters.AddWithValue("policy_state", NpgsqlDbType.Smallint, (short)HistorianCapturePolicyEvidenceState.Explicit);
            command.Parameters.AddWithValue("fingerprint", NpgsqlDbType.Bytea, Convert.FromHexString(new string('a', 32)));
            Assert.Equal(50_000, await command.ExecuteNonQueryAsync());
        }

        await using (var dataSource = NpgsqlDataSource.Create(connectionString))
        await using (var connection = await dataSource.OpenConnectionAsync())
        await using (var command = new NpgsqlCommand("""
            INSERT INTO elitescada.tag_history (
                tag_id, ts, quality, source, value, data_type,
                source_ts_evidence, server_ts_evidence, capture_policy_state)
            VALUES (
                @tag_id, @ts, @quality, @source, @value, @data_type,
                @source_evidence, @server_evidence, @policy_state);
            """, connection))
        {
            command.Parameters.AddWithValue("tag_id", NpgsqlDbType.Uuid, defaultTag.Id);
            command.Parameters.AddWithValue("ts", NpgsqlDbType.TimestampTz, now);
            command.Parameters.AddWithValue("quality", NpgsqlDbType.Integer, (int)TagQuality.Good);
            command.Parameters.AddWithValue("source", NpgsqlDbType.Text, "legacy-default-benchmark");
            command.Parameters.AddWithValue("value", NpgsqlDbType.Jsonb, "1");
            command.Parameters.AddWithValue("data_type", NpgsqlDbType.Smallint, (short)TagDataType.Int32);
            command.Parameters.AddWithValue("source_evidence", NpgsqlDbType.Smallint, (short)TimestampEvidenceState.NotProvided);
            command.Parameters.AddWithValue("server_evidence", NpgsqlDbType.Smallint, (short)TimestampEvidenceState.NotProvided);
            command.Parameters.AddWithValue("policy_state", NpgsqlDbType.Smallint, (short)HistorianCapturePolicyEvidenceState.LegacyDefault);
            await command.ExecuteNonQueryAsync();
        }

        var execution = Execution(
            HistoricalQueryCatalog.Require(HistoricalDatasets.HistorianSamplesV2),
            tag.Id,
            new HistoricalResolvedRange(firstObserved.AddSeconds(-1), now),
            pageSize: 200);
        var queryClock = Stopwatch.StartNew();
        var first = await provider.QueryVersion2Async(execution);
        queryClock.Stop();
        Assert.Equal(200, first.Rows.Count);
        Assert.NotNull(first.NextPosition);
        var lastObserved = firstObserved.AddMilliseconds(49_999 * 50);
        Assert.Equal(lastObserved, first.Rows[0].Cells["timestamp"].AsDateTime());
        Assert.Equal(staleSourceTimestamp, first.Rows[0].Cells["source.timestamp"].AsDateTime());
        Assert.Equal(serverTimestamp, first.Rows[0].Cells["server.timestamp"].AsDateTime());
        Assert.Equal(profileId.ToString("D"), first.Rows[0].Cells["capture.policy.profileId"].Value);
        Assert.Equal("Explicit", first.Rows[0].Cells["capture.policy.status"].Value);

        var second = await provider.QueryVersion2Async(execution with { After = first.NextPosition });
        Assert.Equal(200, second.Rows.Count);
        Assert.NotEqual(first.Rows[0].Cells["timestamp"].Value, second.Rows[0].Cells["timestamp"].Value);

        var v1 = await provider.QueryAsync(execution with
        {
            Dataset = HistoricalQueryCatalog.Require(HistoricalDatasets.HistorianSamples)
        });
        Assert.Equal(200, v1.Rows.Count);
        Assert.Equal(lastObserved, v1.Rows[0].Cells["timestamp"].AsDateTime());
        var v1WireBytes = JsonSerializer.SerializeToUtf8Bytes(v1.Rows).Length;
        var v2WireBytes = JsonSerializer.SerializeToUtf8Bytes(first.Rows).Length;
        var priorV2Projection = first.Rows.Select(row =>
        {
            var cells = row.Cells
                .Where(static cell => !string.Equals(cell.Key, "capture.policy.status", StringComparison.Ordinal))
                .ToDictionary(static cell => cell.Key, static cell => cell.Value, StringComparer.Ordinal);
            cells["capture.policy.fingerprint"] = HistoricalQueryValue.FromString(
                "sha256:" + new string('a', 64));
            return new HistoricalQueryRow(cells);
        }).ToArray();
        var priorV2WireBytes = JsonSerializer.SerializeToUtf8Bytes(priorV2Projection).Length;
        var defaultV2 = await provider.QueryVersion2Async(Execution(
            HistoricalQueryCatalog.Require(HistoricalDatasets.HistorianSamplesV2),
            defaultTag.Id,
            new HistoricalResolvedRange(firstObserved.AddSeconds(-1), now),
            pageSize: 200));
        var defaultV1 = await provider.QueryAsync(Execution(
            HistoricalQueryCatalog.Require(HistoricalDatasets.HistorianSamples),
            defaultTag.Id,
            new HistoricalResolvedRange(firstObserved.AddSeconds(-1), now),
            pageSize: 200));
        var defaultV2Row = Assert.Single(defaultV2.Rows);
        Assert.Equal("LegacyDefault", defaultV2Row.Cells["capture.policy.status"].Value);
        Assert.Equal(HistoricalValueKind.Null, defaultV2Row.Cells["capture.policy.fingerprint"].Kind);
        var defaultV2WireBytes = JsonSerializer.SerializeToUtf8Bytes(defaultV2.Rows).Length;
        var defaultV1WireBytes = JsonSerializer.SerializeToUtf8Bytes(defaultV1.Rows).Length;
        long rowBytes;
        long legacyProjectionBytes;
        long defaultRowBytes;
        long defaultLegacyProjectionBytes;
        long priorFormatTupleBytes;
        long relationBytes;
        long indexBytes;
        await using (var dataSource = NpgsqlDataSource.Create(connectionString))
        await using (var connection = await dataSource.OpenConnectionAsync())
        {
            await using (var baselineCommand = new NpgsqlCommand("""
                CREATE TEMP TABLE temporal_policy_baseline (
                    tag_id uuid NOT NULL,
                    ts timestamptz NOT NULL,
                    quality integer NOT NULL,
                    source text NULL,
                    value jsonb NOT NULL,
                    data_type smallint NULL,
                    source_ts timestamptz NULL,
                    source_ts_evidence smallint NULL,
                    server_ts timestamptz NULL,
                    server_ts_evidence smallint NULL,
                    capture_profile_id uuid NULL,
                    capture_policy_fingerprint text NULL,
                    sample_id bigint NOT NULL);
                """, connection))
                await baselineCommand.ExecuteNonQueryAsync();

            await using (var baselineInsert = new NpgsqlCommand("""
                INSERT INTO temporal_policy_baseline (
                    tag_id, ts, quality, source, value, data_type,
                    source_ts, source_ts_evidence, server_ts, server_ts_evidence,
                    capture_profile_id, capture_policy_fingerprint, sample_id)
                SELECT
                    tag_id, ts, quality, source, value, data_type,
                    source_ts, source_ts_evidence, server_ts, server_ts_evidence,
                    capture_profile_id, @fingerprint, sample_id
                FROM elitescada.tag_history
                WHERE tag_id = @tag_id;
                """, connection))
            {
                baselineInsert.Parameters.AddWithValue("tag_id", NpgsqlDbType.Uuid, tag.Id);
                baselineInsert.Parameters.AddWithValue("fingerprint", NpgsqlDbType.Text, "sha256:" + new string('a', 64));
                Assert.Equal(50_000, await baselineInsert.ExecuteNonQueryAsync());
            }

        await using (var command = new NpgsqlCommand("""
            SELECT
                pg_column_size(sample),
                pg_column_size(ROW(
                    sample.tag_id, sample.ts, sample.quality, sample.source,
                    sample.value, sample.data_type)),
                (SELECT pg_column_size(baseline)
                 FROM temporal_policy_baseline AS baseline
                 ORDER BY baseline.ts
                 LIMIT 1),
                COALESCE((
                    SELECT sum(pg_relation_size(format('%I.%I', chunk_schema, chunk_name)::regclass))
                    FROM timescaledb_information.chunks
                    WHERE hypertable_schema = 'elitescada'
                      AND hypertable_name = 'tag_history'), 0)::bigint,
                COALESCE((
                    SELECT sum(pg_indexes_size(format('%I.%I', chunk_schema, chunk_name)::regclass))
                    FROM timescaledb_information.chunks
                    WHERE hypertable_schema = 'elitescada'
                      AND hypertable_name = 'tag_history'), 0)::bigint
            FROM elitescada.tag_history AS sample
            WHERE sample.tag_id = @tag_id
            ORDER BY sample.ts
            LIMIT 1;
            """, connection))
        {
            command.Parameters.AddWithValue("tag_id", NpgsqlDbType.Uuid, tag.Id);
            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            rowBytes = reader.GetInt32(0);
            legacyProjectionBytes = reader.GetInt32(1);
            priorFormatTupleBytes = reader.GetInt32(2);
            relationBytes = reader.GetInt64(3);
            indexBytes = reader.GetInt64(4);
        }
        }

        await using (var dataSource = NpgsqlDataSource.Create(connectionString))
        await using (var connection = await dataSource.OpenConnectionAsync())
        await using (var command = new NpgsqlCommand("""
            SELECT
                pg_column_size(sample),
                pg_column_size(ROW(
                    sample.tag_id, sample.ts, sample.quality, sample.source,
                    sample.value, sample.data_type))
            FROM elitescada.tag_history AS sample
            WHERE sample.tag_id = @tag_id
            ORDER BY sample.ts
            LIMIT 1;
            """, connection))
        {
            command.Parameters.AddWithValue("tag_id", NpgsqlDbType.Uuid, defaultTag.Id);
            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            defaultRowBytes = reader.GetInt32(0);
            defaultLegacyProjectionBytes = reader.GetInt32(1);
        }

        _output.WriteLine(
            $"V2 50k window/page=200: query={queryClock.Elapsed.TotalMilliseconds:F2} ms, rows={first.Rows.Count}, " +
            $"JSON rows payload v1={v1WireBytes} bytes/v2={v2WireBytes} bytes (delta={v2WireBytes - v1WireBytes}), " +
            $"same-row prior v2 shape (full text hash/no policy-state cell)={priorV2WireBytes} bytes " +
            $"(current-minus-prior={v2WireBytes - priorV2WireBytes}), " +
            $"max-provenance tuple={rowBytes} bytes vs legacy-column projection={legacyProjectionBytes} bytes " +
            $"(delta={rowBytes - legacyProjectionBytes}; prior text-hash tuple={priorFormatTupleBytes}, " +
            $"current-minus-prior={rowBytes - priorFormatTupleBytes}), implicit-default tuple={defaultRowBytes} bytes " +
            $"vs legacy-column projection={defaultLegacyProjectionBytes} bytes " +
            $"(delta={defaultRowBytes - defaultLegacyProjectionBytes}); one-row default JSON v1={defaultV1WireBytes} bytes/v2={defaultV2WireBytes} bytes, " +
            $"hypertable chunks={relationBytes} bytes, chunk indexes={indexBytes} bytes. " +
            "Source timestamp was 90 days older than observed and did not affect the observed-time range/page; server time remained a separate unverified field.");
    }

    [Fact]
    public async Task Provider_PagesEqualTimestampSamplesAndPreservesInt64Exactly()
    {
        var connectionString = Environment.GetEnvironmentVariable("ELITESCADA_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var eventBus = new InMemoryScadaEventBus();
        var registry = new InMemoryTagRegistry();
        var tag = registry.Register(TagDefinition.Create(
            "Counter",
            $"Integration.Historical.Counter.{Guid.NewGuid():N}",
            TagDataType.Int64));
        await using var historian = new TimescaleDbHistorian(
            eventBus,
            connectionString,
            batchSize: 10);
        await using var provider = new TimescaleHistoricalQueryProvider(
            connectionString,
            registry);
        var timestamp = DateTimeOffset.UtcNow.AddSeconds(-1);
        var values = new[] { long.MaxValue - 1, long.MaxValue };

        foreach (var value in values)
        {
            var sample = new TagValue(
                tag.Id,
                value,
                timestamp,
                TagQuality.Good,
                "historical-query-test");
            await eventBus.PublishAsync(new TagValueChanged(tag, null, sample, timestamp));
        }

        await WaitForWritesAsync(historian, values.Length);

        var dataset = HistoricalQueryCatalog.Require(HistoricalDatasets.HistorianSamples);
        var range = new HistoricalResolvedRange(
            timestamp.AddMinutes(-1),
            DateTimeOffset.UtcNow.AddSeconds(1));
        var filters = new[]
        {
            new HistoricalFilter(
                "tag.id",
                HistoricalFilterOperator.Eq,
                [HistoricalQueryValue.FromGuid(tag.Id)])
        };
        var first = await provider.QueryAsync(new HistoricalQueryExecution(
            dataset,
            range,
            filters,
            null,
            new HistoricalSort(),
            1,
            null));

        Assert.Single(first.Rows);
        Assert.NotNull(first.NextPosition);
        Assert.Equal(HistoricalValueKind.Int64, first.Rows[0].Cells["value"].Kind);
        Assert.Contains(
            first.Rows[0].Cells["value"].Value,
            values.Select(static value => value.ToString()));

        var second = await provider.QueryAsync(new HistoricalQueryExecution(
            dataset,
            range,
            filters,
            null,
            new HistoricalSort(),
            1,
            first.NextPosition));

        Assert.Single(second.Rows);
        Assert.Null(second.NextPosition);
        Assert.Equal(HistoricalValueKind.Int64, second.Rows[0].Cells["value"].Kind);
        Assert.NotEqual(
            first.Rows[0].Cells["value"].Value,
            second.Rows[0].Cells["value"].Value);
        Assert.Equal(tag.Path, first.Rows[0].Cells["tag.path"].Value);
        Assert.Equal(tag.Path, second.Rows[0].Cells["tag.path"].Value);
    }

    [Fact]
    public async Task Provider_PreservesDeclaredHistorianScalarKinds()
    {
        var connectionString = Environment.GetEnvironmentVariable("ELITESCADA_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var eventBus = new InMemoryScadaEventBus();
        var registry = new InMemoryTagRegistry();
        var timestamp = DateTimeOffset.UtcNow.AddSeconds(-1);
        var dateTimeValue = new DateTimeOffset(2026, 8, 29, 18, 30, 0, TimeSpan.Zero);
        var cases = new (TagDefinition Tag, object Value, HistoricalValueKind Kind, string Expected)[]
        {
            (Register(registry, "Int16", TagDataType.Int16), (short)123, HistoricalValueKind.Int16, "123"),
            (Register(registry, "Int32", TagDataType.Int32), 123456, HistoricalValueKind.Int32, "123456"),
            (Register(registry, "Int64", TagDataType.Int64), long.MaxValue, HistoricalValueKind.Int64, long.MaxValue.ToString()),
            (Register(registry, "Float", TagDataType.Float), 1.25f, HistoricalValueKind.Float, "1.25"),
            (Register(registry, "Double", TagDataType.Double), 2.5d, HistoricalValueKind.Double, "2.5"),
            (Register(registry, "Boolean", TagDataType.Boolean), true, HistoricalValueKind.Boolean, "true"),
            (Register(registry, "String", TagDataType.String), "AUTO", HistoricalValueKind.String, "AUTO"),
            (Register(registry, "DateTime", TagDataType.DateTime), dateTimeValue, HistoricalValueKind.DateTime, dateTimeValue.ToString("O"))
        };

        await using var historian = new TimescaleDbHistorian(
            eventBus,
            connectionString,
            batchSize: 20);
        await using var provider = new TimescaleHistoricalQueryProvider(
            connectionString,
            registry);

        foreach (var item in cases)
        {
            var sample = new TagValue(
                item.Tag.Id,
                item.Value,
                timestamp,
                TagQuality.Good,
                "historical-query-type-test");
            await eventBus.PublishAsync(
                new TagValueChanged(item.Tag, null, sample, timestamp));
        }

        await WaitForWritesAsync(historian, cases.Length);

        var dataset = HistoricalQueryCatalog.Require(HistoricalDatasets.HistorianSamples);
        var range = new HistoricalResolvedRange(
            timestamp.AddMinutes(-1),
            DateTimeOffset.UtcNow.AddSeconds(1));
        foreach (var item in cases)
        {
            var page = await provider.QueryAsync(new HistoricalQueryExecution(
                dataset,
                range,
                [
                    new HistoricalFilter(
                        "tag.id",
                        HistoricalFilterOperator.Eq,
                        [HistoricalQueryValue.FromGuid(item.Tag.Id)])
                ],
                null,
                new HistoricalSort(),
                10,
                null));

            var row = Assert.Single(page.Rows);
            Assert.Equal(item.Kind, row.Cells["value"].Kind);
            Assert.Equal(item.Expected, row.Cells["value"].Value);
        }
    }

    [Fact]
    public async Task Provider_UsesRegistryForPathSearchAndRejectsUnsupportedSort()
    {
        var connectionString = Environment.GetEnvironmentVariable("ELITESCADA_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var registry = new InMemoryTagRegistry();
        await using var provider = new TimescaleHistoricalQueryProvider(
            connectionString,
            registry);
        var dataset = HistoricalQueryCatalog.Require(HistoricalDatasets.HistorianSamples);
        var range = new HistoricalResolvedRange(
            DateTimeOffset.UtcNow.AddMinutes(-1),
            DateTimeOffset.UtcNow);
        await Assert.ThrowsAsync<ArgumentException>(() => provider.QueryAsync(
            new HistoricalQueryExecution(
                dataset,
                range,
                Array.Empty<HistoricalFilter>(),
                null,
                new HistoricalSort("tag.path", HistoricalSortDirection.Ascending),
                10,
                null)));
    }

    private static TagDefinition Register(
        InMemoryTagRegistry registry,
        string name,
        TagDataType dataType) =>
        registry.Register(TagDefinition.Create(
            name,
            $"Integration.Historical.{name}.{Guid.NewGuid():N}",
            dataType));

    private static HistoricalQueryExecution Execution(
        HistoricalDatasetDefinition dataset,
        Guid tagId,
        HistoricalResolvedRange range,
        int pageSize = 10) => new(
        dataset,
        range,
        [new HistoricalFilter(
            "tag.id",
            HistoricalFilterOperator.Eq,
            [HistoricalQueryValue.FromGuid(tagId)])],
        null,
        new HistoricalSort(),
        pageSize,
        null);

    private static async Task InsertSampleAsync(
        NpgsqlConnection connection,
        Guid tagId,
        DateTimeOffset observedAt,
        int value,
        DateTimeOffset? sourceTimestamp,
        short? sourceEvidence,
        DateTimeOffset? serverTimestamp,
        short? serverEvidence)
    {
        await using var command = new NpgsqlCommand("""
            INSERT INTO elitescada.tag_history (
                tag_id, ts, quality, source, value, data_type,
                source_ts, source_ts_evidence, server_ts, server_ts_evidence)
            VALUES (
                @tag_id, @ts, @quality, @source, @value, @data_type,
                @source_ts, @source_ts_evidence, @server_ts, @server_ts_evidence);
            """, connection);
        command.Parameters.AddWithValue("tag_id", NpgsqlDbType.Uuid, tagId);
        command.Parameters.AddWithValue("ts", NpgsqlDbType.TimestampTz, observedAt);
        command.Parameters.AddWithValue("quality", NpgsqlDbType.Integer, (int)TagQuality.Good);
        command.Parameters.AddWithValue("source", NpgsqlDbType.Text, "legacy-compatibility-test");
        command.Parameters.AddWithValue("value", NpgsqlDbType.Jsonb, value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("data_type", NpgsqlDbType.Smallint, (short)TagDataType.Int32);
        command.Parameters.AddWithValue("source_ts", NpgsqlDbType.TimestampTz, sourceTimestamp?.UtcDateTime ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("source_ts_evidence", NpgsqlDbType.Smallint, sourceEvidence is { } se ? se : DBNull.Value);
        command.Parameters.AddWithValue("server_ts", NpgsqlDbType.TimestampTz, serverTimestamp?.UtcDateTime ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("server_ts_evidence", NpgsqlDbType.Smallint, serverEvidence is { } re ? re : DBNull.Value);
        await command.ExecuteNonQueryAsync();
    }

    private static void AssertEvidence(
        IReadOnlyCollection<HistoricalQueryRow> rows,
        int value,
        string source,
        string server)
    {
        var row = Assert.Single(
            rows,
            candidate => candidate.Cells["value"].Value ==
                         value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(source, row.Cells["source.timestamp.status"].Value);
        Assert.Equal(server, row.Cells["server.timestamp.status"].Value);
    }

    private static async Task WaitForWritesAsync(
        TimescaleDbHistorian historian,
        int expected)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (historian.WrittenSamples < expected && DateTimeOffset.UtcNow < deadline)
            await Task.Delay(50);
        Assert.Equal(expected, historian.WrittenSamples);
        Assert.Equal(0, historian.DroppedSamples);
        Assert.Null(historian.LastWriteError);
    }
}
