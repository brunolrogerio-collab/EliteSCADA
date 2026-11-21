using System.Collections.Concurrent;
using System.Collections.Immutable;
using Scada.Core.Abstractions;

namespace Scada.Core.Events;

public sealed class InMemoryScadaEventBus : IScadaEventBus
{
    private readonly ConcurrentDictionary<Type, ImmutableArray<Func<IScadaEvent, ValueTask>>> _handlers = new();

    public IDisposable Subscribe<TEvent>(Func<TEvent, ValueTask> handler)
        where TEvent : IScadaEvent
    {
        var eventType = typeof(TEvent);
        Func<IScadaEvent, ValueTask> callback = e => handler((TEvent)e);
        _handlers.AddOrUpdate(eventType,
            static (_, item) => ImmutableArray.Create(item),
            static (_, current, item) => current.Add(item), callback);

        return new Subscription(() => _handlers.AddOrUpdate(eventType,
            static (_, _) => ImmutableArray<Func<IScadaEvent, ValueTask>>.Empty,
            static (_, current, item) => current.Remove(item), callback));
    }

    public async ValueTask PublishAsync<TEvent>(TEvent scadaEvent, CancellationToken cancellationToken = default)
        where TEvent : IScadaEvent
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_handlers.TryGetValue(typeof(TEvent), out var handlers)) return;

        foreach (var handler in handlers)
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
