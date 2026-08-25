using System.Collections.Concurrent;
using Scada.Core.Abstractions;

namespace Scada.Core.Events;

public sealed class InMemoryScadaEventBus : IScadaEventBus
{
    private readonly ConcurrentDictionary<Type, ConcurrentDictionary<Guid, Func<IScadaEvent, ValueTask>>> _handlers = new();

    public IDisposable Subscribe<TEvent>(Func<TEvent, ValueTask> handler)
        where TEvent : IScadaEvent
    {
        var id = Guid.NewGuid();
        var handlers = _handlers.GetOrAdd(typeof(TEvent), _ => new());
        handlers[id] = e => handler((TEvent)e);
        return new Subscription(() => handlers.TryRemove(id, out _));
    }

    public async ValueTask PublishAsync<TEvent>(TEvent scadaEvent, CancellationToken cancellationToken = default)
        where TEvent : IScadaEvent
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_handlers.TryGetValue(typeof(TEvent), out var handlers)) return;

        foreach (var handler in handlers.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await handler(scadaEvent);
        }
    }

    private sealed class Subscription(Action unsubscribe) : IDisposable
    {
        private Action? _unsubscribe = unsubscribe;
        public void Dispose() => Interlocked.Exchange(ref _unsubscribe, null)?.Invoke();
    }
}
