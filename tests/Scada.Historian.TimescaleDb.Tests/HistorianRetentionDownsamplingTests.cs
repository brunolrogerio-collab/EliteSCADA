using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Historian.Aggregation;
using Scada.Historian.Policies;
using Scada.Historian.TimescaleDb;

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
            ]);

        policy.Validate();
        var json = HistorianPolicyJson.Serialize(policy);
        var restored = HistorianPolicyJson.Deserialize(json);

        Assert.Equal("industrial-default", restored.Key);
        Assert.Equal(TimeSpan.FromDays(30), restored.RawRetention.Duration);
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

    private static async Task WaitForWritesAsync(TimescaleDbHistorian historian, int expected)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (historian.WrittenSamples < expected && DateTimeOffset.UtcNow < deadline)
            await Task.Delay(50);

        Assert.True(historian.WrittenSamples >= expected);
        Assert.Null(historian.LastWriteError);
    }
}
