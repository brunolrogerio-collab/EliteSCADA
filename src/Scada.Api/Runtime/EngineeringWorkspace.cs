using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.Tags;

namespace Scada.Api.Runtime;

public sealed class EngineeringWorkspace : IDisposable
{
    private readonly InMemoryScadaEventBus _eventBus = new();

    public EngineeringWorkspace()
    {
        Tags = new InMemoryTagRegistry();
        Alarms = new InMemoryAlarmEngine(_eventBus);

        foreach (var tag in DemoProcessModel.CreateTagDefinitions())
            Tags.Register(tag);

        foreach (var alarm in DemoProcessModel.CreateAlarmDefinitions())
            Alarms.Register(alarm);
    }

    public InMemoryTagRegistry Tags { get; }
    public InMemoryAlarmEngine Alarms { get; }

    public void Dispose() => Alarms.Dispose();
}
