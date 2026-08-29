namespace Scada.Drivers.Iec60870;

public sealed record Iec104ReconnectPolicy
{
    public IReadOnlyList<TimeSpan> Delays { get; init; } = new[]
    {
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(30)
    };

    public TimeSpan StableSessionThreshold { get; init; } = TimeSpan.FromSeconds(30);

    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Delays);
        if (Delays.Count == 0)
            throw new ArgumentException("IEC-104 reconnect policy requires at least one delay.", nameof(Delays));
        if (Delays.Any(static delay => delay <= TimeSpan.Zero))
            throw new ArgumentOutOfRangeException(nameof(Delays), "IEC-104 reconnect delays must all be greater than zero.");
        if (StableSessionThreshold <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(StableSessionThreshold), "IEC-104 stable-session threshold must be greater than zero.");
    }
}

public sealed class Iec104ReconnectBackoff
{
    private readonly TimeSpan[] _delays;
    private int _nextIndex;

    public Iec104ReconnectBackoff(Iec104ReconnectPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        policy.Validate();
        _delays = policy.Delays.ToArray();
    }

    public TimeSpan NextDelay()
    {
        var index = Math.Min(_nextIndex, _delays.Length - 1);
        var delay = _delays[index];
        if (_nextIndex < _delays.Length - 1)
            _nextIndex++;
        return delay;
    }

    public void Reset() => _nextIndex = 0;
}

public sealed record Iec104ReconnectFailure(
    int Attempt,
    Exception Failure,
    TimeSpan SessionDuration,
    TimeSpan NextDelay,
    bool BackoffWasReset);

/// <summary>
/// Compatibility facade over the managed IEC-104 client. Reconnect policy and command safety live in one
/// implementation so bootstrap may repeat while operational commands are never queued for replay.
/// </summary>
public sealed class Iec104ReconnectingSessionRunner
{
    private readonly Iec104ManagedClient _client;

    public Iec104ReconnectingSessionRunner(
        Func<IIec104ClientAdapter> adapterFactory,
        string host,
        int port,
        Iec104SessionOptions sessionOptions,
        TimeZoneInfo stationTimeZone,
        IEnumerable<ushort> commonAddresses,
        Iec104ReconnectPolicy? reconnectPolicy = null,
        byte originatorAddress = 0,
        Func<TimeSpan, CancellationToken, Task>? delayAsync = null)
    {
        _client = new Iec104ManagedClient(
            adapterFactory,
            host,
            port,
            sessionOptions,
            stationTimeZone,
            commonAddresses,
            reconnectPolicy,
            commandOptions: null,
            originatorAddress,
            delayAsync);
    }

    public Iec104SessionState SessionState => _client.SessionState;

    public int InFlightCommandCount => _client.InFlightCommandCount;

    public Task<Iec104CommandResult> ExecuteCommandAsync(
        Iec104CommandTransaction transaction,
        CancellationToken cancellationToken = default) =>
        _client.ExecuteCommandAsync(transaction, cancellationToken);

    public Task RunAsync(
        Func<Iec104DecodedPoint, CancellationToken, ValueTask> onObservedPoint,
        Func<Iec104ReconnectFailure, CancellationToken, ValueTask>? onReconnectFailure = null,
        CancellationToken cancellationToken = default) =>
        _client.RunAsync(onObservedPoint, onReconnectFailure, cancellationToken);
}
