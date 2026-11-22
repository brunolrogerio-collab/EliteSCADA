using Npgsql;
using NpgsqlTypes;
using Scada.Core.HistoricalQueries;
using Scada.Core.Tags;
using Scada.Historian.TimescaleDb;

namespace Scada.Historian.TimescaleDb.Tests;

public sealed class TimescaleHistoricalRetrievalProviderTests
{
    [Fact]
    public async Task Provider_PointModes_UseCanonicalPersistedSamplesAndExplicitGaps()
    {
        var connectionString = Environment.GetEnvironmentVariable("ELITESCADA_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var registry = new InMemoryTagRegistry();
        var analog = Register(registry, "Analog", TagDataType.Double);
        var digital = Register(registry, "Digital", TagDataType.Boolean);
        await using var provider = new TimescaleHistoricalQueryProvider(connectionString, registry);

        var start = DateTimeOffset.UtcNow.AddMinutes(-20);
        start = new DateTimeOffset(
            start.Year, start.Month, start.Day, start.Hour, start.Minute, 0, TimeSpan.Zero);
        var range = new HistoricalResolvedRange(start, start.AddMinutes(8));

        await EnsureProviderReadyAsync(provider, range, analog.Id);
        await InsertAsync(
            connectionString,
            (analog.Id, start.AddMinutes(1), TagQuality.Good, "10.0", TagDataType.Double),
            (analog.Id, start.AddMinutes(3), TagQuality.Good, "30.0", TagDataType.Double),
            (analog.Id, start.AddMinutes(5), TagQuality.Bad, "50.0", TagDataType.Double),
            (digital.Id, start.AddMinutes(1), TagQuality.Good, "false", TagDataType.Boolean),
            (digital.Id, start.AddMinutes(4), TagQuality.Good, "true", TagDataType.Boolean));

        var both = Execution(range, [analog.Id, digital.Id], pageSize: 20);

        var last = await provider.QueryLastAsync(both);
        Assert.Equal(2, last.Points.Count);
        Assert.Equal(start.AddMinutes(5), last.Points.Single(x => x.TagId == analog.Id).TimestampUtc);
        Assert.Equal(TagQuality.Bad, last.Points.Single(x => x.TagId == analog.Id).Quality);
        Assert.Equal(start.AddMinutes(4), last.Points.Single(x => x.TagId == digital.Id).TimestampUtc);

        var before = await provider.QueryAtOrBeforeAsync(both, start.AddMinutes(2));
        Assert.All(before.Points, x => Assert.Equal(start.AddMinutes(1), x.TimestampUtc));

        var after = await provider.QueryAtOrAfterAsync(both, start.AddMinutes(2));
        Assert.Equal(start.AddMinutes(3), after.Points.Single(x => x.TagId == analog.Id).TimestampUtc);
        Assert.Equal(start.AddMinutes(4), after.Points.Single(x => x.TagId == digital.Id).TimestampUtc);

        var exact = await provider.QueryExactAsync(both, start.AddMinutes(5));
        var exactAnalog = exact.Points.Single(x => x.TagId == analog.Id);
        Assert.Equal(HistoricalRetrievalProvenanceKind.Measured, exactAnalog.Provenance.Kind);
        Assert.Equal(TagQuality.Bad, exactAnalog.Quality);
        var exactDigital = exact.Points.Single(x => x.TagId == digital.Id);
        Assert.Equal(HistoricalRetrievalProvenanceKind.Gap, exactDigital.Provenance.Kind);
        Assert.Null(exactDigital.Quality);

        var interpolated = await provider.QueryInterpolatedAsync(
            both,
            start.AddMinutes(2),
            maximumGapMilliseconds: 120_000);
        var analogInterpolated = interpolated.Points.Single(x => x.TagId == analog.Id);
        Assert.Equal(HistoricalRetrievalProvenanceKind.Interpolated, analogInterpolated.Provenance.Kind);
        Assert.Equal("20", analogInterpolated.Value.Value);
        Assert.Equal(TagQuality.Good, analogInterpolated.Quality);
        Assert.Equal(
            [start.AddMinutes(1), start.AddMinutes(3)],
            analogInterpolated.Provenance.SourceTimestampsUtc);
        Assert.Equal(
            HistoricalRetrievalProvenanceKind.Gap,
            interpolated.Points.Single(x => x.TagId == digital.Id).Provenance.Kind);
    }

    [Fact]
    public async Task Provider_SampledFixedStep_UsesRangeOriginAndPreservesBadExactObservation()
    {
        var connectionString = Environment.GetEnvironmentVariable("ELITESCADA_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var registry = new InMemoryTagRegistry();
        var tag = Register(registry, "Sampled", TagDataType.Double);
        await using var provider = new TimescaleHistoricalQueryProvider(connectionString, registry);

        var from = DateTimeOffset.UtcNow.AddMinutes(-15);
        from = new DateTimeOffset(from.Year, from.Month, from.Day, from.Hour, from.Minute, 0, TimeSpan.Zero);
        var range = new HistoricalResolvedRange(from, from.AddMinutes(4).AddSeconds(30));
        await EnsureProviderReadyAsync(provider, range, tag.Id);
        await InsertAsync(
            connectionString,
            (tag.Id, from, TagQuality.Good, "10.0", TagDataType.Double),
            (tag.Id, from.AddMinutes(2), TagQuality.Good, "30.0", TagDataType.Double),
            (tag.Id, from.AddMinutes(4), TagQuality.Bad, "50.0", TagDataType.Double));

        var result = await provider.QuerySampledFixedStepAsync(
            Execution(range, [tag.Id], pageSize: 10),
            stepMilliseconds: 60_000,
            maximumGapMilliseconds: 120_000);

        Assert.Equal(5, result.Points.Count);
        Assert.Equal(
            Enumerable.Range(0, 5).Select(i => from.AddMinutes(i)),
            result.Points.Select(x => x.TimestampUtc));
        Assert.Equal(HistoricalRetrievalProvenanceKind.Measured, result.Points[0].Provenance.Kind);
        Assert.Equal(HistoricalRetrievalProvenanceKind.Interpolated, result.Points[1].Provenance.Kind);
        Assert.Equal(HistoricalRetrievalProvenanceKind.Measured, result.Points[2].Provenance.Kind);
        Assert.Equal(HistoricalRetrievalProvenanceKind.Gap, result.Points[3].Provenance.Kind);
        Assert.Equal(HistoricalRetrievalProvenanceKind.Measured, result.Points[4].Provenance.Kind);
        Assert.Equal(TagQuality.Bad, result.Points[4].Quality);
        Assert.DoesNotContain(result.Points, x => x.TimestampUtc == range.ToUtc);
    }

    [Fact]
    public async Task Provider_Aggregate_IsUtcAlignedQualityExplicitAndDoesNotFabricateNumericGood()
    {
        var connectionString = Environment.GetEnvironmentVariable("ELITESCADA_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var registry = new InMemoryTagRegistry();
        var tag = Register(registry, "Aggregate", TagDataType.Double);
        await using var provider = new TimescaleHistoricalQueryProvider(connectionString, registry);

        var baseTime = DateTimeOffset.UtcNow.AddMinutes(-30);
        baseTime = new DateTimeOffset(baseTime.Year, baseTime.Month, baseTime.Day, baseTime.Hour, 0, 0, TimeSpan.Zero);
        var range = new HistoricalResolvedRange(baseTime, baseTime.AddMinutes(6));
        await EnsureProviderReadyAsync(provider, range, tag.Id);
        await InsertAsync(
            connectionString,
            (tag.Id, baseTime.AddMinutes(1), TagQuality.Good, "10.0", TagDataType.Double),
            (tag.Id, baseTime.AddMinutes(3), TagQuality.Good, "30.0", TagDataType.Double),
            (tag.Id, baseTime.AddMinutes(5), TagQuality.Bad, "50.0", TagDataType.Double));

        var execution = Execution(range, [tag.Id], pageSize: 10);
        var average = await provider.QueryAggregateAsync(
            execution,
            bucketMilliseconds: 120_000,
            HistoricalAggregateOperation.Average);

        Assert.Equal(3, average.Points.Count);
        Assert.Equal(
            [baseTime, baseTime.AddMinutes(2), baseTime.AddMinutes(4)],
            average.Points.Select(x => x.TimestampUtc));
        Assert.Equal("10", average.Points[0].Value.Value);
        Assert.Equal("30", average.Points[1].Value.Value);
        Assert.Equal(HistoricalValueKind.Null, average.Points[2].Value.Kind);
        Assert.Equal(TagQuality.Bad, average.Points[2].Quality);
        Assert.Equal(1, average.Points[2].Provenance.SampleCount);
        Assert.Equal(0, average.Points[2].Provenance.GoodCount);
        Assert.Equal(1, average.Points[2].Provenance.BadCount);
        Assert.Equal(baseTime.AddMinutes(6), average.Points[2].Provenance.BucketEndUtc);

        var count = await provider.QueryAggregateAsync(
            execution,
            bucketMilliseconds: 120_000,
            HistoricalAggregateOperation.Count);
        Assert.All(count.Points, point => Assert.Equal("1", point.Value.Value));
    }

    [Fact]
    public async Task Provider_RetrievalHonorsCancellation()
    {
        var connectionString = Environment.GetEnvironmentVariable("ELITESCADA_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var registry = new InMemoryTagRegistry();
        var tag = Register(registry, "Cancel", TagDataType.Double);
        await using var provider = new TimescaleHistoricalQueryProvider(connectionString, registry);
        var range = new HistoricalResolvedRange(DateTimeOffset.UtcNow.AddMinutes(-2), DateTimeOffset.UtcNow);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            provider.QueryLastAsync(
                Execution(range, [tag.Id], pageSize: 10),
                cancellation.Token));
    }

    private static HistoricalQueryExecution Execution(
        HistoricalResolvedRange range,
        Guid[] tagIds,
        int pageSize)
    {
        var filter = new HistoricalFilter(
            "tag.id",
            tagIds.Length == 1 ? HistoricalFilterOperator.Eq : HistoricalFilterOperator.In,
            tagIds.Select(HistoricalQueryValue.FromGuid).ToArray());
        return new HistoricalQueryExecution(
            HistoricalQueryCatalog.Require(HistoricalDatasets.HistorianSamples),
            range,
            [filter],
            null,
            new HistoricalSort(),
            pageSize,
            null);
    }

    private static TagDefinition Register(
        InMemoryTagRegistry registry,
        string name,
        TagDataType dataType) =>
        registry.Register(TagDefinition.Create(
            name,
            $"Integration.DataQuery.{name}.{Guid.NewGuid():N}",
            dataType));

    private static async Task EnsureProviderReadyAsync(
        TimescaleHistoricalQueryProvider provider,
        HistoricalResolvedRange range,
        Guid tagId)
    {
        _ = await provider.QueryExactAsync(
            Execution(range, [tagId], pageSize: 10),
            range.FromUtc);
    }

    private static async Task InsertAsync(
        string connectionString,
        params (Guid TagId, DateTimeOffset Timestamp, TagQuality Quality, string JsonValue, TagDataType DataType)[] samples)
    {
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var connection = await dataSource.OpenConnectionAsync();
        foreach (var sample in samples)
        {
            await using var command = new NpgsqlCommand(
                """
                INSERT INTO elitescada.tag_history(tag_id, ts, quality, source, value, data_type)
                VALUES (@tag_id, @ts, @quality, @source, @value, @data_type);
                """,
                connection);
            command.Parameters.AddWithValue("tag_id", NpgsqlDbType.Uuid, sample.TagId);
            command.Parameters.AddWithValue("ts", NpgsqlDbType.TimestampTz, sample.Timestamp);
            command.Parameters.AddWithValue("quality", NpgsqlDbType.Integer, (int)sample.Quality);
            command.Parameters.AddWithValue("source", NpgsqlDbType.Text, "data-query-provider-test");
            command.Parameters.AddWithValue("value", NpgsqlDbType.Jsonb, sample.JsonValue);
            command.Parameters.AddWithValue("data_type", NpgsqlDbType.Smallint, (short)sample.DataType);
            await command.ExecuteNonQueryAsync();
        }
    }
}
