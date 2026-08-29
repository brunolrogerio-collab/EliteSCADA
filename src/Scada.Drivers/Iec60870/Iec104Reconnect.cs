using System.Diagnostics;

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
/// Recreates complete IEC-104 sessions after failures. Each attempt gets a fresh adapter and therefore
/// fresh TCP/APCI sequence state. Only session bootstrap (STARTDT/GI) is repeated. Operational commands
/// are intentionally not queued here and can never be replayed by reconnect logic.
/// </summary>
public sealed class Iec104ReconnectingSessionRunner
{
    private readonly Func<IIec104ClientAdapter> _adapterFactory;
    private readonly string _host;
    private readonly int _port;
    private readonly Iec104SessionOptions _sessionOptions;
    private readonly TimeZoneInfo _stationTimeZone;
    private readonly ushort[] _commonAddresses;
    private readonly byte _originatorAddress;
    private readonly Iec104ReconnectPolicy _reconnectPolicy;
    private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync;

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
        ArgumentNullException.ThrowIfNull(adapterFactory);
        ArgumentNullException.ThrowIfNull(sessionOptions);
        ArgumentNullException.ThrowIfNull(stationTimeZone);
        ArgumentNullException.ThrowIfNull(commonAddresses);
        if (string.IsNullOrWhiteSpace(host)) throw new ArgumentException("IEC-104 host is required.", nameof(host));
        if (port is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(port));

        sessionOptions.Validate();
        var addresses = commonAddresses.Distinct().OrderBy(static value => value).ToArray();
        if (addresses.Length == 0)
            throw new ArgumentException("IEC-104 reconnect runner requires at least one Common Address.", nameof(commonAddresses));

        var policy = reconnectPolicy ?? new Iec104ReconnectPolicy();
        policy.Validate();

        _adapterFactory = adapterFactory;
        _host = host.Trim();
        _port = port;
        _sessionOptions = sessionOptions;
        _stationTimeZone = stationTimeZone;
        _commonAddresses = addresses;
        _originatorAddress = originatorAddress;
        _reconnectPolicy = policy;
        _delayAsync = delayAsync ?? static (delay, cancellationToken) => Task.Delay(delay, cancellationToken);
    }

    public async Task RunAsync(
        Func<Iec104DecodedPoint, CancellationToken, ValueTask> onObservedPoint,
        Func<Iec104ReconnectFailure, CancellationToken, ValueTask>? onReconnectFailure = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onObservedPoint);

        var backoff = new Iec104ReconnectBackoff(_reconnectPolicy);
        var attempt = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            attempt++;
            var startedTimestamp = Stopwatch.GetTimestamp();

            await using var adapter = _adapterFactory()
                ?? throw new InvalidOperationException("IEC-104 adapter factory returned null.");
            var runner = new Iec104ClientSessionRunner(
                adapter,
                _host,
                _port,
                _sessionOptions,
                _stationTimeZone,
                _commonAddresses,
                _originatorAddress);

            try
            {
                await runner.RunAsync(onObservedPoint, cancellationToken).ConfigureAwait(false);
                if (!cancellationToken.IsCancellationRequested)
                    throw new IOException("IEC-104 session ended without cancellation; reconnect is required.");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception failure)
            {
                var sessionDuration = Stopwatch.GetElapsedTime(startedTimestamp);
                var reset = sessionDuration >= _reconnectPolicy.StableSessionThreshold;
                if (reset)
                    backoff.Reset();

                var delay = backoff.NextDelay();
                if (onReconnectFailure is not null)
                {
                    await onReconnectFailure(
                        new Iec104ReconnectFailure(attempt, failure, sessionDuration, delay, reset),
                        cancellationToken).ConfigureAwait(false);
                }

                await _delayAsync(delay, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
