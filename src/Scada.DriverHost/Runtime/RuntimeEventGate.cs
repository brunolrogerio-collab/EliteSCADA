using Scada.Core.Abstractions;
using Scada.Core.Events;

namespace Scada.DriverHost.Runtime;

public sealed class RuntimeEventGate : IScadaEventBus
{
    private readonly InMemoryScadaEventBus _local = new();
    private readonly IScadaEventBus _external;
    private int _forwardingEnabled;

    public RuntimeEventGate(IScadaEventBus external, bool forwardingEnabled = false)
    {
        _external = external ?? throw new ArgumentNullException(nameof(external));
        _forwardingEnabled = forwardingEnabled ? 1 : 0;
    }

    public bool ForwardingEnabled => Volatile.Read(ref _forwardingEnabled) == 1;

    public void EnableForwarding() => Volatile.Write(ref _forwardingEnabled, 1);

    public void DisableForwarding() => Volatile.Write(ref _forwardingEnabled, 0);

    public IDisposable Subscribe<TEvent>(Func<TEvent, ValueTask> handler)
        where TEvent : IScadaEvent =>
        _local.Subscribe(handler);

    public async ValueTask PublishAsync<TEvent>(
        TEvent scadaEvent,
        CancellationToken cancellationToken = default)
        where TEvent : IScadaEvent
    {
        await _local.PublishAsync(scadaEvent, cancellationToken);
        if (ForwardingEnabled)
            await _external.PublishAsync(scadaEvent, cancellationToken);
    }
}
