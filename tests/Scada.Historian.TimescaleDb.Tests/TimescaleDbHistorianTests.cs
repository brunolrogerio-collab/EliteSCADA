using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Historian.TimescaleDb;

namespace Scada.Historian.TimescaleDb.Tests;

public sealed class TimescaleDbHistorianTests
{
    [Fact]
    public async Task Historian_PersistsAndQueriesTypedTagValues()
    {
        var connectionString = Environment.GetEnvironmentVariable("ELITESCADA_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var eventBus = new InMemoryScadaEventBus();
        await using var historian = new TimescaleDbHistorian(eventBus, connectionString, batchSize: 50);

        var tag = TagDefinition.Create("Pressure", $"Integration.Pressure.{Guid.NewGuid():N}", TagDataType.Double);
        var start = DateTimeOffset.UtcNow.AddSeconds(-1);
        var values = new[]
        {
            new TagValue(tag.Id, 7.25d, DateTimeOffset.UtcNow, TagQuality.Good, "integration-test"),
            new TagValue(tag.Id, 7.50d, DateTimeOffset.UtcNow.AddMilliseconds(10), TagQuality.Uncertain, "integration-test"),
            new TagValue(tag.Id, 8.00d, DateTimeOffset.UtcNow.AddMilliseconds(20), TagQuality.Good, "integration-test")
        };

        foreach (var value in values)
            await eventBus.PublishAsync(new TagValueChanged(tag, null, value, value.Timestamp));

        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (historian.WrittenSamples < values.Length && DateTimeOffset.UtcNow < deadline)
            await Task.Delay(50);

        Assert.Equal(values.Length, historian.WrittenSamples);
        Assert.Equal(0, historian.DroppedSamples);
        Assert.Null(historian.LastWriteError);

        var result = historian.Query(tag.Id, start, DateTimeOffset.UtcNow.AddSeconds(2), 100);

        Assert.Equal(3, result.Count);
        Assert.Equal(7.25d, Convert.ToDouble(result[0].Value));
        Assert.Equal(7.50d, Convert.ToDouble(result[1].Value));
        Assert.Equal(TagQuality.Uncertain, result[1].Quality);
        Assert.Equal("integration-test", result[2].Source);
    }

    [Fact]
    public async Task Historian_PreservesBooleanStringAndNullValues()
    {
        var connectionString = Environment.GetEnvironmentVariable("ELITESCADA_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var eventBus = new InMemoryScadaEventBus();
        await using var historian = new TimescaleDbHistorian(eventBus, connectionString);
        var tag = TagDefinition.Create("State", $"Integration.State.{Guid.NewGuid():N}", TagDataType.String);
        var now = DateTimeOffset.UtcNow;

        var values = new[]
        {
            new TagValue(tag.Id, true, now, TagQuality.Good),
            new TagValue(tag.Id, "AUTO", now.AddMilliseconds(10), TagQuality.Good),
            new TagValue(tag.Id, null, now.AddMilliseconds(20), TagQuality.BadCommunication)
        };

        foreach (var value in values)
            await eventBus.PublishAsync(new TagValueChanged(tag, null, value, value.Timestamp));

        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (historian.WrittenSamples < values.Length && DateTimeOffset.UtcNow < deadline)
            await Task.Delay(50);

        var result = historian.Query(tag.Id, now.AddSeconds(-1), now.AddSeconds(2), 100);

        Assert.Equal(true, result[0].Value);
        Assert.Equal("AUTO", result[1].Value);
        Assert.Null(result[2].Value);
        Assert.Equal(TagQuality.BadCommunication, result[2].Quality);
    }
}
