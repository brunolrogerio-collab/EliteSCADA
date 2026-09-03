using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.InternalMemory;
using Scada.Core.Sources;
using Scada.Core.Tags;
using Scada.Historian.Memory;

namespace Scada.Drivers.Tests;

public sealed class CanonicalSimulationQualityContractTests
{
    [Theory]
    [InlineData(TagQuality.Bad)]
    [InlineData(TagQuality.Stale)]
    [InlineData(TagQuality.Unavailable)]
    public async Task ServerPublisher_PropagatesExplicitQuality_ToCacheAlarmAndHistorian(TagQuality quality)
    {
        var eventBus = new InMemoryScadaEventBus();
        var cache = new CurrentTagCache(eventBus);
        using var alarms = new InMemoryAlarmEngine(eventBus);
        await using var historian = new BufferedInMemoryHistorian(eventBus);

        var tag = TagDefinition.Create(
            "ProcessValue",
            "Simulation.ProcessValue",
            TagDataType.Int32,
            source: "memory.server");
        var source = new ServerMemorySourceProvider(
            "memory.server",
            new InMemoryServerMemoryRetentionStore());
        await source.ActivateAsync(new[]
        {
            new MemoryTagDefinition(tag, new TypedTagValue(TagDataType.Int32, 0))
        });

        var communicationAlarm = AlarmDefinition.Create(
            "ProcessValue communication",
            tag.Id,
            AlarmType.Communication,
            AlarmPriority.High);
        alarms.Register(communicationAlarm);

        var publisher = new ServerAuthoritativeSamplePublisher(source, cache);
        var sourceTimestamp = DateTimeOffset.UtcNow.AddMilliseconds(-10);
        var current = await publisher.PublishAsync(
            tag,
            new QualifiedSourceSample(41, quality, sourceTimestamp));

        Assert.Equal(quality, current.Quality);
        Assert.Equal(sourceTimestamp, current.SourceTimestamp);
        Assert.True(cache.TryGet(tag.Id, out var cached));
        Assert.Equal(quality, cached!.Quality);

        var alarm = Assert.Single(alarms.Snapshot(activeOnly: true));
        Assert.Equal(AlarmState.Active, alarm.State);
        Assert.Equal(AlarmType.Communication, alarm.Type);

        await WaitForHistorianAsync(historian);
        var samples = historian.Query(
            tag.Id,
            DateTimeOffset.UtcNow.AddMinutes(-1),
            DateTimeOffset.UtcNow.AddMinutes(1));
        Assert.Contains(samples, sample => sample.Quality == quality && Equals(sample.Value, 41));
    }

    [Fact]
    public async Task OrdinaryServerMemoryWrite_RemainsGood()
    {
        var tag = TagDefinition.Create(
            "Setpoint",
            "Simulation.Setpoint",
            TagDataType.Double,
            source: "memory.server");
        var source = new ServerMemorySourceProvider(
            "memory.server",
            new InMemoryServerMemoryRetentionStore());
        await source.ActivateAsync(new[]
        {
            new MemoryTagDefinition(tag, new TypedTagValue(TagDataType.Double, 1D))
        });

        await source.WriteAsync(tag.Id, 2D);
        var current = await source.ReadAsync(tag.Id);

        Assert.NotNull(current);
        Assert.Equal(TagQuality.Good, current!.Quality);
        Assert.Equal(2D, current.Value);
    }

    [Fact]
    public async Task ServerPublisher_RejectsTagNotOwnedByItsSource()
    {
        var owned = TagDefinition.Create("Owned", "Simulation.Owned", TagDataType.Int32, source: "memory.server");
        var foreign = TagDefinition.Create("Physical", "PLC.Physical", TagDataType.Int32, source: "modbus.line1");
        var source = new ServerMemorySourceProvider(
            "memory.server",
            new InMemoryServerMemoryRetentionStore());
        await source.ActivateAsync(new[]
        {
            new MemoryTagDefinition(owned, new TypedTagValue(TagDataType.Int32, 0))
        });

        var publisher = new ServerAuthoritativeSamplePublisher(
            source,
            new CurrentTagCache(new InMemoryScadaEventBus()));

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await publisher.PublishAsync(
                foreign,
                new QualifiedSourceSample(1, TagQuality.Bad)));
    }

    private static async Task WaitForHistorianAsync(BufferedInMemoryHistorian historian)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(2);
        while (historian.WrittenSamples == 0 && DateTimeOffset.UtcNow < deadline)
            await Task.Delay(10);

        Assert.True(historian.WrittenSamples > 0);
    }
}
