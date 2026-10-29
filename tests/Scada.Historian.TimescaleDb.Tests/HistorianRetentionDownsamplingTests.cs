using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Historian.Aggregation;
using Scada.Historian.Policies;
using Scada.Historian.TimescaleDb;
using Npgsql;

namespace Scada.Historian.TimescaleDb.Tests;

public sealed class HistorianRetentionDownsamplingTests
{
    [Fact]
    public void Storage_policy_validates_and_round_trips_json()
    {
        var policy = new HistorianStoragePolicy(
            "industrial-default",
            new HistorianRetentionRule(true, TimeSpan.FromDays(30)),
            [
                new HistorianDownsamplingRule(
                    HistorianBucketWidth.OneMinute,
                    Enabled: true,
                    RefreshInterval: TimeSpan.FromMinutes(1),
                    RefreshLookback: TimeSpan.FromHours(2),
                    Retention: new HistorianRetentionRule(true, TimeSpan.FromDays(365)))
            ],
            RawSampleStorageLimitBytes: 20L * 1024 * 1024 * 1024,
            RawSampleStorageLimitAction: HistorianRawStorageLimitAction.WarnOnly);

        policy.Validate();
        var json = HistorianPolicyJson.Serialize(policy);
        var restored = HistorianPolicyJson.Deserialize(json);

        Assert.Equal("industrial-default", restored.Key);
        Assert.Equal(TimeSpan.FromDays(30), restored.RawRetention.Duration);
        Assert.Equal(20L * 1024 * 1024 * 1024, restored.RawSampleStorageLimitBytes);
        Assert.Equal(HistorianRawStorageLimitAction.WarnOnly, restored.RawSampleStorageLimitAction);
        var tier = Assert.Single(restored.EffectiveDownsampling);
        Assert.Equal(HistorianBucketWidth.OneMinute, tier.Bucket);
        Assert.Equal(TimeSpan.FromDays(365), tier.EffectiveRetention.Duration);
    }

    [Fact]
    public void Invalid_storage_policy_is_rejected()
    {
        Assert.Throws<ArgumentException>(() =>
            new HistorianStoragePolicy(
                "invalid",
                new HistorianRetentionRule(true),
                Array.Empty<HistorianDownsamplingRule>()).Validate());

        Assert.Throws<ArgumentException>(() =>
            new HistorianStoragePolicy(
                "duplicates",
                new HistorianRetentionRule(),
                [
                    EnabledTier(HistorianBucketWidth.FiveMinutes),
                    EnabledTier(HistorianBucketWidth.FiveMinutes)
                ]).Validate());

        Assert.Throws<ArgumentException>(() =>
            new HistorianStoragePolicy(
                "disabled-refresh",
                new HistorianRetentionRule(),
                [
                    new HistorianDownsamplingRule(
                        HistorianBucketWidth.OneMinute,
                        Enabled: false,
                        RefreshInterval: TimeSpan.FromMinutes(1))
                ]).Validate());
    }

    [Fact]
    public void Retention_reduction_requires_explicit_data_expiration_approval()
    {
        var current = new HistorianStoragePolicy(
            "policy",
            new HistorianRetentionRule(true, TimeSpan.FromDays(30)));
        var shorter = current with
        {
            RawRetention = new HistorianRetentionRule(true, TimeSpan.FromDays(7))
        };
        var longer = current with
        {
            RawRetention = new HistorianRetentionRule(true, TimeSpan.FromDays(60))
        };

        Assert.True(HistorianPolicySafety.RequiresExplicitDataExpirationApproval(current, shorter));
        Assert.False(HistorianPolicySafety.RequiresExplicitDataExpirationApproval(current, longer));
    }

    [Fact]
    public void Enabling_or_lowering_destructive_size_limit_requires_explicit_approval()
    {
        var warning = new HistorianStoragePolicy(
            "policy",
            new HistorianRetentionRule(),
            RawSampleStorageLimitBytes: 10L * 1024 * 1024 * 1024);
        var destructive = warning with
        {
            RawSampleStorageLimitAction = HistorianRawStorageLimitAction.DeleteOldestCompleteChunks
        };
        var lowerLimit = destructive with { RawSampleStorageLimitBytes = 2L * 1024 * 1024 * 1024 };
        var higherLimit = destructive with { RawSampleStorageLimitBytes = 20L * 1024 * 1024 * 1024 };
        var warningOnly = destructive with { RawSampleStorageLimitAction = HistorianRawStorageLimitAction.WarnOnly };

        Assert.True(HistorianPolicySafety.RequiresExplicitDataExpirationApproval(warning, destructive));
        Assert.True(HistorianPolicySafety.RequiresExplicitDataExpirationApproval(destructive, lowerLimit));
        Assert.False(HistorianPolicySafety.RequiresExplicitDataExpirationApproval(destructive, higherLimit));
        Assert.False(HistorianPolicySafety.RequiresExplicitDataExpirationApproval(destructive, warningOnly));
        Assert.False(HistorianPolicySafety.RequiresExplicitDataExpirationApproval(null, warning));
    }

    [Fact]
    public async Task Timescale_size_limit_warns_without_deletion_and_prunes_only_oldest_completed_chunks_when_authorized()
    {
        var connectionString = TestConnectionString();
        if (connectionString is null) return;

        await using var store = new TimescaleDbHistorianRetentionDownsamplingStore(connectionString);
        await store.EnsureInfrastructureAsync();
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var tagId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var sampleTimes = new[] { now.AddDays(-60), now.AddDays(-30), now.AddMinutes(-1) };
        var originalPolicy = await store.GetAppliedPolicyAsync();

        try
        {
            await InsertRawSamplesAsync(dataSource, tagId, sampleTimes);
            Assert.True(await CountDistinctChunksForTagAsync(dataSource, tagId) >= 2);

            var warningPolicy = new HistorianStoragePolicy(
                $"size-warning-{Guid.NewGuid():N}",
                new HistorianRetentionRule(),
                RawSampleStorageLimitBytes: 1,
                RawSampleStorageLimitAction: HistorianRawStorageLimitAction.WarnOnly);
            await store.ApplyPolicyAsync(warningPolicy);
            var warningResult = await store.EnforceRawSampleStorageLimitAsync();
            Assert.Equal(HistorianStorageLimitEnforcementStatus.WarningOnly, warningResult.Status);
            Assert.Equal(0, warningResult.DeletedChunkCount);
            Assert.Equal(3, await CountSamplesForTagAsync(dataSource, tagId));

            var deletePolicy = warningPolicy with
            {
                RawSampleStorageLimitAction = HistorianRawStorageLimitAction.DeleteOldestCompleteChunks
            };
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.ApplyPolicyAsync(deletePolicy));
            await store.ApplyPolicyAsync(
                deletePolicy,
                new HistorianPolicyApplyOptions(AllowPotentialDataExpiration: true));

            await using (var lockingConnection = await dataSource.OpenConnectionAsync())
            await using (var lockingTransaction = await lockingConnection.BeginTransactionAsync())
            {
                await using var lockPolicy = new NpgsqlCommand(
                    "SELECT singleton_id FROM elitescada.historian_storage_policy_state WHERE singleton_id = 1 FOR UPDATE;",
                    lockingConnection,
                    lockingTransaction);
                Assert.Equal((short)1, Convert.ToInt16(await lockPolicy.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture));
                var skipped = await store.EnforceRawSampleStorageLimitAsync();
                Assert.Equal(HistorianStorageLimitEnforcementStatus.SkippedBusy, skipped.Status);
                Assert.Equal(3, await CountSamplesForTagAsync(dataSource, tagId));
                await lockingTransaction.CommitAsync();
            }

            var result = await store.EnforceRawSampleStorageLimitAsync();
            Assert.Equal(HistorianStorageLimitEnforcementStatus.ChunksRemoved, result.Status);
            Assert.True(result.DeletedChunkCount > 0);
            Assert.True(result.IsOverLimit);
            Assert.Equal(1, await CountSamplesForTagAsync(dataSource, tagId));
            Assert.True(await SampleExistsAsync(dataSource, tagId, sampleTimes[^1]));
        }
        finally
        {
            await DeleteSamplesForTagAsync(dataSource, tagId);
            if (originalPolicy is not null)
            {
                await store.ApplyPolicyAsync(
                    originalPolicy,
                    new HistorianPolicyApplyOptions(AllowPotentialDataExpiration: true));
            }
            else
            {
                var reset = new HistorianStoragePolicy(
                    $"test-reset-{Guid.NewGuid():N}",
                    new HistorianRetentionRule(),
                    [
                        new HistorianDownsamplingRule(HistorianBucketWidth.OneMinute, Enabled: false),
                        new HistorianDownsamplingRule(HistorianBucketWidth.FiveMinutes, Enabled: false),
                        new HistorianDownsamplingRule(HistorianBucketWidth.FifteenMinutes, Enabled: false),
                        new HistorianDownsamplingRule(HistorianBucketWidth.OneHour, Enabled: false)
                    ]);
                await store.ApplyPolicyAsync(reset);
                await using var cleanup = await dataSource.OpenConnectionAsync();
                await using var command = new NpgsqlCommand(
                    "DELETE FROM elitescada.historian_storage_policy_state WHERE singleton_id = 1;",
                    cleanup);
                await command.ExecuteNonQueryAsync();
            }
        }
    }

    [Fact]
    public void Bucket_calculation_uses_half_open_utc_boundaries()
    {
        var beforeBoundary = new DateTimeOffset(2026, 8, 26, 12, 34, 59, TimeSpan.Zero).AddMilliseconds(999);
        var boundary = new DateTimeOffset(2026, 8, 26, 12, 35, 0, TimeSpan.Zero);

        Assert.Equal(
            new DateTimeOffset(2026, 8, 26, 12, 34, 0, TimeSpan.Zero),
            HistorianBucketCalculator.GetBucketStart(beforeBoundary, HistorianBucketWidth.OneMinute));
        Assert.Equal(
            boundary,
            HistorianBucketCalculator.GetBucketStart(boundary, HistorianBucketWidth.OneMinute));
        Assert.Equal(
            new DateTimeOffset(2026, 8, 26, 12, 30, 0, TimeSpan.Zero),
            HistorianBucketCalculator.GetBucketStart(boundary, HistorianBucketWidth.FifteenMinutes));
    }

    [Fact]
    public void Numeric_aggregation_excludes_uncertain_and_bad_from_numeric_statistics()
    {
        var tagId = Guid.NewGuid();
        var start = new DateTimeOffset(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);
        var samples = new[]
        {
            new TagValue(tagId, 10d, start.AddSeconds(1), TagQuality.Good),
            new TagValue(tagId, 100d, start.AddSeconds(2), TagQuality.Uncertain),
            new TagValue(tagId, 20d, start.AddSeconds(3), TagQuality.Good),
            new TagValue(tagId, 1000d, start.AddSeconds(4), TagQuality.BadCommunication)
        };

        var aggregate = Assert.IsType<HistorianAggregateBucket>(
            HistorianBucketAggregator.Aggregate(
                tagId,
                TagDataType.Double,
                HistorianBucketWidth.OneMinute,
                start,
                samples));

        Assert.Equal(4, aggregate.SampleCount);
        Assert.Equal(2, aggregate.GoodCount);
        Assert.Equal(1, aggregate.UncertainCount);
        Assert.Equal(1, aggregate.BadCount);
        Assert.Equal(2, aggregate.NumericGoodCount);
        Assert.Equal(10d, aggregate.Minimum);
        Assert.Equal(20d, aggregate.Maximum);
        Assert.Equal(15d, aggregate.Average);
        Assert.Equal(10d, aggregate.FirstValue);
        Assert.Equal(TagQuality.Good, aggregate.FirstQuality);
        Assert.Equal(1000d, aggregate.LastValue);
        Assert.Equal(TagQuality.BadCommunication, aggregate.LastQuality);
    }

    [Theory]
    [InlineData(TagDataType.Boolean)]
    [InlineData(TagDataType.String)]
    [InlineData(TagDataType.Enum)]
    [InlineData(TagDataType.DateTime)]
    public void Non_numeric_types_never_receive_numeric_aggregates(TagDataType dataType)
    {
        var tagId = Guid.NewGuid();
        var start = new DateTimeOffset(2026, 8, 26, 13, 0, 0, TimeSpan.Zero);
        var first = ValueFor(dataType, first: true);
        var last = ValueFor(dataType, first: false);

        var aggregate = Assert.IsType<HistorianAggregateBucket>(
            HistorianBucketAggregator.Aggregate(
                tagId,
                dataType,
                HistorianBucketWidth.OneMinute,
                start,
                [
                    new TagValue(tagId, first, start.AddSeconds(1), TagQuality.Good),
                    new TagValue(tagId, last, start.AddSeconds(2), TagQuality.Good)
                ]));

        Assert.Equal(2, aggregate.SampleCount);
        Assert.Equal(0, aggregate.NumericGoodCount);
        Assert.Null(aggregate.Minimum);
        Assert.Null(aggregate.Maximum);
        Assert.Null(aggregate.Average);
        Assert.Equal(first, aggregate.FirstValue);
        Assert.Equal(last, aggregate.LastValue);
    }

    [Fact]
    public void Empty_bucket_has_no_aggregate_row()
    {
        var start = new DateTimeOffset(2026, 8, 26, 14, 0, 0, TimeSpan.Zero);
        Assert.Null(HistorianBucketAggregator.Aggregate(
            Guid.NewGuid(),
            TagDataType.Double,
            HistorianBucketWidth.OneMinute,
            start,
            Array.Empty<TagValue>()));
    }

    [Fact]
    public void Aggregation_rejects_silent_runtime_type_coercion()
    {
        var tagId = Guid.NewGuid();
        var start = new DateTimeOffset(2026, 8, 26, 15, 0, 0, TimeSpan.Zero);

        Assert.Throws<ArgumentException>(() =>
            HistorianBucketAggregator.Aggregate(
                tagId,
                TagDataType.Int32,
                HistorianBucketWidth.OneMinute,
                start,
                [new TagValue(tagId, 7L, start.AddSeconds(1), TagQuality.Good)]));
    }

    [Fact]
    public async Task Timescale_infrastructure_is_idempotent_and_policy_application_is_explicit()
    {
        var connectionString = TestConnectionString();
        if (connectionString is null) return;

        await using var store = new TimescaleDbHistorianRetentionDownsamplingStore(connectionString);
        await store.EnsureInfrastructureAsync();
        await store.EnsureInfrastructureAsync();

        var disabled = new HistorianStoragePolicy(
            $"ci-{Guid.NewGuid():N}",
            new HistorianRetentionRule(),
            [new HistorianDownsamplingRule(HistorianBucketWidth.OneMinute, Enabled: false)]);
        await store.ApplyPolicyAsync(disabled);

        var applied = Assert.IsType<HistorianStoragePolicy>(await store.GetAppliedPolicyAsync());
        Assert.Equal(disabled.Key, applied.Key);
        Assert.False(applied.RawRetention.Enabled);

        var enabledRetention = disabled with
        {
            RawRetention = new HistorianRetentionRule(true, TimeSpan.FromDays(36500))
        };
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.ApplyPolicyAsync(enabledRetention));
        await store.ApplyPolicyAsync(
            enabledRetention,
            new HistorianPolicyApplyOptions(AllowPotentialDataExpiration: true));

        var restored = Assert.IsType<HistorianStoragePolicy>(await store.GetAppliedPolicyAsync());
        Assert.True(restored.RawRetention.Enabled);
        Assert.Equal(TimeSpan.FromDays(36500), restored.RawRetention.Duration);

        await store.ApplyPolicyAsync(disabled);
    }

    [Fact]
    public async Task Timescale_storage_usage_reports_database_and_historian_owned_bytes()
    {
        var connectionString = TestConnectionString();
        if (connectionString is null) return;

        await using var store = new TimescaleDbHistorianRetentionDownsamplingStore(connectionString);
        await store.EnsureInfrastructureAsync();

        var usage = await store.GetStorageUsageAsync();

        Assert.True(usage.DatabaseBytes > 0);
        Assert.True(usage.RawSamplesBytes.HasValue && usage.RawSamplesBytes.Value >= 0);
        Assert.True(usage.AggregateBytes.HasValue && usage.AggregateBytes.Value >= 0);
        Assert.True(usage.HistorianBytes.HasValue && usage.HistorianBytes.Value >= 0);
        Assert.True(usage.DatabaseBytes >= usage.HistorianBytes!.Value);
        Assert.Equal(TimeSpan.Zero, usage.MeasuredAtUtc.Offset);
    }

    [Fact]
    public async Task Timescale_downsampling_preserves_stable_tag_id_quality_and_tag_isolation()
    {
        var connectionString = TestConnectionString();
        if (connectionString is null) return;

        var eventBus = new InMemoryScadaEventBus();
        await using var historian = new TimescaleDbHistorian(eventBus, connectionString, batchSize: 20);
        await using var store = new TimescaleDbHistorianRetentionDownsamplingStore(connectionString);
        await store.EnsureInfrastructureAsync();

        var bucketStart = HistorianBucketCalculator.GetBucketStart(
            DateTimeOffset.UtcNow.AddMinutes(-10),
            HistorianBucketWidth.OneMinute);
        var tagId = Guid.NewGuid();
        var original = CreateTag(tagId, "Plant.Area.Pressure", TagDataType.Double);
        var renamed = CreateTag(tagId, "Plant.Area.PressureRenamed", TagDataType.Double);
        var other = CreateTag(Guid.NewGuid(), "Plant.Area.Other", TagDataType.Double);

        var samples = new[]
        {
            (original, new TagValue(tagId, 10d, bucketStart.AddSeconds(1), TagQuality.Good)),
            (renamed, new TagValue(tagId, 100d, bucketStart.AddSeconds(2), TagQuality.Uncertain)),
            (renamed, new TagValue(tagId, 20d, bucketStart.AddSeconds(3), TagQuality.Good)),
            (renamed, new TagValue(tagId, 1000d, bucketStart.AddSeconds(4), TagQuality.BadDevice)),
            (other, new TagValue(other.Id, 999d, bucketStart.AddSeconds(5), TagQuality.Good))
        };

        foreach (var (tag, value) in samples)
            await eventBus.PublishAsync(new TagValueChanged(tag, null, value, value.Timestamp));
        await WaitForWritesAsync(historian, samples.Length);

        await store.RefreshAggregateAsync(
            HistorianBucketWidth.OneMinute,
            bucketStart,
            bucketStart.AddMinutes(1));

        var aggregate = Assert.Single(await store.QueryAggregatesAsync(
            tagId,
            HistorianBucketWidth.OneMinute,
            bucketStart,
            bucketStart.AddMinutes(2)));

        Assert.Equal(tagId, aggregate.TagId);
        Assert.Equal(4, aggregate.SampleCount);
        Assert.Equal(2, aggregate.GoodCount);
        Assert.Equal(1, aggregate.UncertainCount);
        Assert.Equal(1, aggregate.BadCount);
        Assert.Equal(10d, aggregate.Minimum);
        Assert.Equal(20d, aggregate.Maximum);
        Assert.Equal(15d, aggregate.Average);
        Assert.Equal(TagDataType.Double, aggregate.DataType);
        Assert.True(aggregate.DataTypeConsistent);

        var otherAggregate = Assert.Single(await store.QueryAggregatesAsync(
            other.Id,
            HistorianBucketWidth.OneMinute,
            bucketStart,
            bucketStart.AddMinutes(2)));
        Assert.Equal(1, otherAggregate.SampleCount);
        Assert.Equal(999d, otherAggregate.Minimum);
    }

    [Fact]
    public async Task Timescale_non_numeric_and_removed_tags_are_not_coerced_or_purged()
    {
        var connectionString = TestConnectionString();
        if (connectionString is null) return;

        var eventBus = new InMemoryScadaEventBus();
        await using var historian = new TimescaleDbHistorian(eventBus, connectionString, batchSize: 20);
        await using var store = new TimescaleDbHistorianRetentionDownsamplingStore(connectionString);
        await store.EnsureInfrastructureAsync();

        var bucketStart = HistorianBucketCalculator.GetBucketStart(
            DateTimeOffset.UtcNow.AddMinutes(-20),
            HistorianBucketWidth.OneMinute);
        var enumTag = CreateTag(Guid.NewGuid(), "Plant.Mode", TagDataType.Enum);

        await eventBus.PublishAsync(new TagValueChanged(
            enumTag,
            null,
            new TagValue(enumTag.Id, 1, bucketStart.AddSeconds(1), TagQuality.Good),
            bucketStart.AddSeconds(1)));
        await eventBus.PublishAsync(new TagValueChanged(
            enumTag,
            null,
            new TagValue(enumTag.Id, 2, bucketStart.AddSeconds(2), TagQuality.Good),
            bucketStart.AddSeconds(2)));
        await WaitForWritesAsync(historian, 2);

        // The TAG now simply stops producing samples. No Engineering delete/purge call is
        // made because historian retention is deliberately independent from active TAG definitions.
        await store.RefreshAggregateAsync(
            HistorianBucketWidth.OneMinute,
            bucketStart,
            bucketStart.AddMinutes(1));

        var aggregate = Assert.Single(await store.QueryAggregatesAsync(
            enumTag.Id,
            HistorianBucketWidth.OneMinute,
            bucketStart,
            bucketStart.AddMinutes(2)));
        Assert.Equal(2, aggregate.SampleCount);
        Assert.Equal(0, aggregate.NumericGoodCount);
        Assert.Null(aggregate.Minimum);
        Assert.Null(aggregate.Maximum);
        Assert.Null(aggregate.Average);
        Assert.Equal(TagDataType.Enum, aggregate.DataType);

        var empty = await store.QueryAggregatesAsync(
            Guid.NewGuid(),
            HistorianBucketWidth.OneMinute,
            bucketStart,
            bucketStart.AddMinutes(2));
        Assert.Empty(empty);
    }

    private static HistorianDownsamplingRule EnabledTier(HistorianBucketWidth bucket) =>
        new(
            bucket,
            Enabled: true,
            RefreshInterval: bucket.ToTimeSpan(),
            RefreshLookback: bucket.ToTimeSpan() * 4,
            Retention: new HistorianRetentionRule());

    private static object ValueFor(TagDataType dataType, bool first) => dataType switch
    {
        TagDataType.Boolean => first,
        TagDataType.String => first ? "A" : "B",
        TagDataType.Enum => first ? 1 : 2,
        TagDataType.DateTime => new DateTimeOffset(2026, 8, 26, first ? 1 : 2, 0, 0, TimeSpan.Zero),
        _ => throw new ArgumentOutOfRangeException(nameof(dataType))
    };

    private static TagDefinition CreateTag(Guid id, string path, TagDataType dataType) =>
        new(
            id,
            path.Split('.').Last(),
            path,
            dataType,
            Source: "integration-test",
            EngineeringUnit: null,
            Description: null,
            ReadOnly: false);

    private static string? TestConnectionString() =>
        Environment.GetEnvironmentVariable("ELITESCADA_TEST_POSTGRES");

    private static async Task InsertRawSamplesAsync(
        NpgsqlDataSource dataSource,
        Guid tagId,
        IReadOnlyList<DateTimeOffset> timestamps)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        foreach (var timestamp in timestamps)
        {
            await using var command = new NpgsqlCommand("""
                INSERT INTO elitescada.tag_history (tag_id, ts, quality, source, value)
                VALUES (@tag_id, @timestamp, @quality, 'size-limit-test', '1'::jsonb);
                """, connection);
            command.Parameters.AddWithValue("tag_id", tagId);
            command.Parameters.AddWithValue("timestamp", timestamp.UtcDateTime);
            command.Parameters.AddWithValue("quality", (int)TagQuality.Good);
            await command.ExecuteNonQueryAsync();
        }
    }

    private static async Task<long> CountDistinctChunksForTagAsync(NpgsqlDataSource dataSource, Guid tagId)
    {
        const string sql = """
            SELECT COUNT(DISTINCT c.chunk_name)
            FROM timescaledb_information.chunks c
            WHERE c.hypertable_schema = 'elitescada'
              AND c.hypertable_name = 'tag_history'
              AND EXISTS (
                  SELECT 1 FROM elitescada.tag_history h
                  WHERE h.tag_id = @tag_id
                    AND h.ts >= c.range_start
                    AND h.ts < c.range_end);
            """;
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("tag_id", tagId);
        return Convert.ToInt64(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task<long> CountSamplesForTagAsync(NpgsqlDataSource dataSource, Guid tagId)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            "SELECT COUNT(*) FROM elitescada.tag_history WHERE tag_id = @tag_id;",
            connection);
        command.Parameters.AddWithValue("tag_id", tagId);
        return Convert.ToInt64(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task<bool> SampleExistsAsync(
        NpgsqlDataSource dataSource,
        Guid tagId,
        DateTimeOffset timestamp)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            "SELECT EXISTS(SELECT 1 FROM elitescada.tag_history WHERE tag_id = @tag_id AND ts = @timestamp);",
            connection);
        command.Parameters.AddWithValue("tag_id", tagId);
        command.Parameters.AddWithValue("timestamp", timestamp.UtcDateTime);
        return (bool)(await command.ExecuteScalarAsync() ?? false);
    }

    private static async Task DeleteSamplesForTagAsync(NpgsqlDataSource dataSource, Guid tagId)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            "DELETE FROM elitescada.tag_history WHERE tag_id = @tag_id;",
            connection);
        command.Parameters.AddWithValue("tag_id", tagId);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task WaitForWritesAsync(TimescaleDbHistorian historian, int expected)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (historian.WrittenSamples < expected && DateTimeOffset.UtcNow < deadline)
            await Task.Delay(50);

        Assert.True(historian.WrittenSamples >= expected);
        Assert.Null(historian.LastWriteError);
    }
}
