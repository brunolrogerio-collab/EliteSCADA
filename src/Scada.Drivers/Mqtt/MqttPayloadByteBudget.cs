namespace Scada.Drivers.Mqtt;

/// <summary>
/// Async byte budget for EliteSCADA-owned inbound payload copies queued between
/// the MQTTnet callback and the driver receive loop.
/// </summary>
internal sealed class MqttPayloadByteBudget
{
    public const long DefaultCapacityBytes = 67_108_864L;

    private readonly object _gate = new();
    private readonly long _capacity;
    private TaskCompletionSource<bool> _changed = NewSignal();
    private long _used;

    public MqttPayloadByteBudget(long capacity = DefaultCapacityBytes)
    {
        if (capacity < 1 || capacity > DefaultCapacityBytes)
            throw new ArgumentOutOfRangeException(nameof(capacity));

        _capacity = capacity;
    }

    public async ValueTask ReserveAsync(long bytes, CancellationToken cancellationToken)
    {
        if (bytes < 0 || bytes > _capacity)
            throw new ArgumentOutOfRangeException(nameof(bytes));
        if (bytes == 0)
            return;

        while (true)
        {
            Task changed;
            lock (_gate)
            {
                if (_used <= _capacity - bytes)
                {
                    _used += bytes;
                    return;
                }

                changed = _changed.Task;
            }

            await changed.WaitAsync(cancellationToken);
        }
    }

    public void Release(long bytes)
    {
        if (bytes < 0)
            throw new ArgumentOutOfRangeException(nameof(bytes));
        if (bytes == 0)
            return;

        TaskCompletionSource<bool> changed;
        lock (_gate)
        {
            if (bytes > _used)
                throw new InvalidOperationException("MQTT payload byte budget release exceeds the reserved byte count.");

            _used -= bytes;
            changed = _changed;
            _changed = NewSignal();
        }

        changed.TrySetResult(true);
    }

    private static TaskCompletionSource<bool> NewSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
