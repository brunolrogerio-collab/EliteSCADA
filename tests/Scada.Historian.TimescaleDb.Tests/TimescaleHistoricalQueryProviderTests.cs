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
