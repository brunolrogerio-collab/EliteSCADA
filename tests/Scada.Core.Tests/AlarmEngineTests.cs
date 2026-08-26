using Scada.Core.Abstractions;
using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.Tags;
using Xunit;

namespace Scada.Core.Tests;

public sealed class AlarmEngineTests
{
    [Fact]
    public async Task High_alarm_activates_and_can_be_acknowledged()
    {
        IScadaEventBus bus = new InMemoryScadaEventBus();
        var cache = new CurrentTagCache(bus);
        using var alarms = new InMemoryAlarmEngine(bus);
        var tag = TagDefinition.Create("Pressure", "Demo.Pressure", TagDataType.Double);
        var definition = alarms.Register(AlarmDefinition.Create("High Pressure", tag.Id, AlarmType.High, AlarmPriority.High, 10));

        await cache.UpdateAsync(tag, TagValue.Good(tag.Id, 11.2));
        Assert.Equal(AlarmState.Active, alarms.Snapshot().Single().State);

        Assert.True(await alarms.AcknowledgeAsync(definition.Id, "operator"));
        Assert.Equal(AlarmState.Acknowledged, alarms.Snapshot().Single().State);
    }

    [Fact]
    public async Task Shelved_alarm_stays_suppressed_and_unshelve_restores_latest_underlying_state()
    {
        IScadaEventBus bus = new InMemoryScadaEventBus();
        var cache = new CurrentTagCache(bus);
        using var alarms = new InMemoryAlarmEngine(bus);
        var tag = TagDefinition.Create("Pressure", "Demo.Pressure", TagDataType.Double);
        var definition = alarms.Register(AlarmDefinition.Create("High Pressure", tag.Id, AlarmType.High, AlarmPriority.High, 10));

        await cache.UpdateAsync(tag, TagValue.Good(tag.Id, 8.0));
        Assert.True(await alarms.ShelveAsync(definition.Id, "developer"));

        var shelved = alarms.Snapshot().Single();
        Assert.Equal(AlarmState.Shelved, shelved.State);
        Assert.Equal("developer", shelved.ShelvedBy);
        Assert.NotNull(shelved.ShelvedAt);
        Assert.Empty(alarms.Snapshot(activeOnly: true));

        await cache.UpdateAsync(tag, TagValue.Good(tag.Id, 12.0));
        Assert.Equal(AlarmState.Shelved, alarms.Snapshot().Single().State);

        Assert.True(await alarms.UnshelveAsync(definition.Id, "developer"));
        var unshelved = alarms.Snapshot().Single();
        Assert.Equal(AlarmState.Active, unshelved.State);
        Assert.Null(unshelved.ShelvedAt);
        Assert.Null(unshelved.ShelvedBy);
    }

    [Fact]
    public async Task Alarm_that_disallows_shelving_cannot_be_shelved()
    {
        IScadaEventBus bus = new InMemoryScadaEventBus();
        using var alarms = new InMemoryAlarmEngine(bus);
        var tag = TagDefinition.Create("Pressure", "Demo.Pressure", TagDataType.Double);
        var definition = alarms.Register(AlarmDefinition.Create(
            "High Pressure",
            tag.Id,
            AlarmType.High,
            AlarmPriority.High,
            setpoint: 10,
            shelvingAllowed: false));

        Assert.False(await alarms.ShelveAsync(definition.Id, "developer"));
        Assert.Equal(AlarmState.Normal, alarms.Snapshot().Single().State);
    }
}
