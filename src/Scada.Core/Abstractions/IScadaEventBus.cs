using Scada.Core.Events;

namespace Scada.Core.Abstractions;

public interface IScadaEventBus
{
    IDisposable Subscribe<TEvent>(Func<TEvent, ValueTask> handler)
        where TEvent : IScadaEvent;

    ValueTask PublishAsync<TEvent>(TEvent scadaEvent, CancellationToken cancellationToken = default)
        where TEvent : IScadaEvent;
}
