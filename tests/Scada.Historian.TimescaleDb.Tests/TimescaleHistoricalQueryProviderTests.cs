using Scada.Core.Events;
using Scada.Core.HistoricalQueries;
using Scada.Core.Tags;
using Scada.Historian.TimescaleDb;

namespace Scada.Historian.TimescaleDb.Tests;

public sealed class TimescaleHistoricalQueryProviderTests
{
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
        await using var historian = new TimescaleDbHistorian(eventBus, connectionString, batchSize: 10);
        await using var provider = new TimescaleHistoricalQueryProvider(connectionString, registry);
        var timestamp = DateTimeOffset.UtcNow.AddSeconds(-1);
        var values = new[] { long.MaxValue - 1, long.MaxValue };

        foreach (var value in values)
        {
            var sample = new TagValue(tag.Id, value, timestamp, TagQuality.Good, "historical-query-test");
            await eventBus.PublishAsync(new TagValueChanged(tag, null, sample, timestamp));
        }

        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (historian.WrittenSamples < values.Length && DateTimeOffset.UtcNow < deadline)
            await Task.Delay(50);
        Assert.Equal(values.Length, historian.WrittenSamples);

        var dataset = HistoricalQueryCatalog.Require(HistoricalDatasets.HistorianSamples);
        var range = new HistoricalResolvedRange(timestamp.AddMinutes(-1), DateTimeOffset.UtcNow.AddSeconds(1));
        var first = await provider.QueryAsync(new HistoricalQueryExecution(
            dataset,
            range,
            [new HistoricalFilter("tag.id", HistoricalFilterOperator.Eq, [HistoricalQueryValue.FromGuid(tag.Id)])],
            null,
            new HistoricalSort(),
            1,
            null));

        Assert.Single(first.Rows);
        Assert.NotNull(first.NextPosition);
        Assert.Equal(HistoricalValueKind.Int64, first.Rows[0].Cells["value"].Kind);
        Assert.Contains(first.Rows[0].Cells["value"].Value, values.Select(static value => value.ToString()));

        var second = await provider.QueryAsync(new HistoricalQueryExecution(
            dataset,
            range,
            [new HistoricalFilter("tag.id", HistoricalFilterOperator.Eq, [HistoricalQueryValue.FromGuid(tag.Id)])],
            null,
            new HistoricalSort(),
            1,
            first.NextPosition));

        Assert.Single(second.Rows);
        Assert.Null(second.NextPosition);
        Assert.Equal(HistoricalValueKind.Int64, second.Rows[0].Cells["value"].Kind);
        Assert.NotEqual(first.Rows[0].Cells["value"].Value, second.Rows[0].Cells["value"].Value);
        Assert.Equal(tag.Path, first.Rows[0].Cells["tag.path"].Value);
        Assert.Equal(tag.Path, second.Rows[0].Cells["tag.path"].Value);
    }

    [Fact]
    public async Task Provider_UsesRegistryForPathSearchAndRejectsUnsupportedSort()
    {
        var connectionString = Environment.GetEnvironmentVariable("ELITESCADA_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var registry = new InMemoryTagRegistry();
        var provider = new TimescaleHistoricalQueryProvider(connectionString, registry);
        await using (provider)
        {
            var dataset = HistoricalQueryCatalog.Require(HistoricalDatasets.HistorianSamples);
            var range = new HistoricalResolvedRange(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow);
            await Assert.ThrowsAsync<ArgumentException>(() => provider.QueryAsync(new HistoricalQueryExecution(
                dataset,
                range,
                Array.Empty<HistoricalFilter>(),
                null,
                new HistoricalSort("tag.path", HistoricalSortDirection.Ascending),
                10,
                null)));
        }
    }
}
