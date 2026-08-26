using Scada.Core.Abstractions;
using Scada.Core.Alarms;
using Scada.Core.Tags;

namespace Scada.Api.Runtime;

public sealed class DemoRuntimeServices : IDisposable
{
    public DemoRuntimeServices(IScadaEventBus eventBus)
    {
        Registry = new InMemoryTagRegistry();
        Cache = new CurrentTagCache(eventBus);
        Alarms = new InMemoryAlarmEngine(eventBus);
    }

    public InMemoryTagRegistry Registry { get; }
    public CurrentTagCache Cache { get; }
    public InMemoryAlarmEngine Alarms { get; }

    public void Dispose() => Alarms.Dispose();
}
