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
}
