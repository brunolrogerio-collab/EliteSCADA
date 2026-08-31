namespace Scada.Drivers.OpcUa;

public sealed class OpcUaRuntimeSessionSupervisor : IAsyncDisposable
{
    private static readonly TimeSpan[] DefaultReconnectDelays =
    [
        TimeSpan.FromMilliseconds(250),
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5)
    ];

    private readonly IOpcUaRuntimeSessionFactory _factory;
    private readonly IReadOnlyList<TimeSpan> _reconnectDelays;
    private readonly object _gate = new();
    private IOpcUaRuntimeSession? _session;
    private int _disposed;

    public OpcUaRuntimeSessionSupervisor(
        IOpcUaRuntimeSessionFactory factory,
        IReadOnlyList<TimeSpan>? reconnectDelays = null)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _factory = factory;
        _reconnectDelays = ValidateReconnectDelays(reconnectDelays ?? DefaultReconnectDelays);
    }

    public IOpcUaRuntimeSession? CurrentSession
    {
        get
        {
            lock (_gate) return _session;
        }
    }

    public async Task<IOpcUaRuntimeSession> ConnectAsync(
        IReadOnlyCollection<OpcUaRuntimeBinding> bindings,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        var session = await _factory.ConnectAsync(bindings, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("OPC UA runtime session factory returned no session.");

        IOpcUaRuntimeSession? previous;
        lock (_gate)
        {
            previous = _session;
            _session = session;
        }

        if (previous is not null && !ReferenceEquals(previous, session))
            await previous.DisposeAsync().ConfigureAwait(false);

        return session;
    }

    public async Task<IOpcUaRuntimeSession> ReconnectUntilAvailableAsync(
        IReadOnlyCollection<OpcUaRuntimeBinding> bindings,
        Action<int>? onFailure,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        var attempt = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return await ConnectAsync(bindings, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                attempt++;
                onFailure?.Invoke(attempt);
                var delay = _reconnectDelays[Math.Min(attempt - 1, _reconnectDelays.Count - 1)];
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public async Task InvalidateAsync(IOpcUaRuntimeSession expected)
    {
        IOpcUaRuntimeSession? detached = null;
        lock (_gate)
        {
            if (ReferenceEquals(_session, expected))
            {
                detached = _session;
                _session = null;
            }
        }
        if (detached is not null) await detached.DisposeAsync().ConfigureAwait(false);
    }

    public async Task DisconnectAsync()
    {
        IOpcUaRuntimeSession? session;
        lock (_gate)
        {
            session = _session;
            _session = null;
        }
        if (session is not null) await session.DisposeAsync().ConfigureAwait(false);
    }

    private static IReadOnlyList<TimeSpan> ValidateReconnectDelays(IReadOnlyList<TimeSpan> delays)
    {
        if (delays.Count == 0)
            throw new ArgumentException("At least one OPC UA reconnect delay is required.", nameof(delays));
        if (delays.Any(x => x < TimeSpan.Zero))
            throw new ArgumentOutOfRangeException(nameof(delays), "OPC UA reconnect delays cannot be negative.");
        return delays.ToArray();
    }

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        await DisconnectAsync().ConfigureAwait(false);
    }
}
