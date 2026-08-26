using Scada.Core.Abstractions;
using Scada.Core.Alarms;
using Scada.Core.Commands;
using Scada.Core.Tags;

namespace Scada.Api.Runtime;

public sealed class DemoRuntimeServices : IDisposable
{
    public DemoRuntimeServices(IScadaEventBus eventBus)
    {
        Registry = new InMemoryTagRegistry();
        Cache = new CurrentTagCache(eventBus);
        Alarms = new InMemoryAlarmEngine(eventBus);
        Commands = new InMemoryCommandRegistry();
        foreach (var command in DemoProcessModel.CreateCommandDefinitions())
            Commands.Register(command);
    }

    public InMemoryTagRegistry Registry { get; }
    public CurrentTagCache Cache { get; }
    public InMemoryAlarmEngine Alarms { get; }
    public InMemoryCommandRegistry Commands { get; }

    public void Dispose() => Alarms.Dispose();
}
