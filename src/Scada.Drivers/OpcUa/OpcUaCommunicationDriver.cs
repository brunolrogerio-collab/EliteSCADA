using System.Collections.Concurrent;
using System.Threading.Channels;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;

namespace Scada.Drivers.OpcUa;

public sealed class OpcUaCommunicationDriver : ICommunicationDriver
{
    private readonly OpcUaRuntimeSessionSupervisor _sessions;
    private readonly ConcurrentDictionary<Guid, TagValue> _cache = new();
    private readonly Channel<TagValue> _updates = Channel.CreateBounded<TagValue>(
        new BoundedChannelOptions(4096) { FullMode = BoundedChannelFullMode.DropOldest });
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private IReadOnlyDictionary<Guid, OpcUaRuntimeBinding> _bindings = new Dictionary<Guid, OpcUaRuntimeBinding>();
    private CancellationTokenSource? _runtimeCts;
    private Task? _subscriptionLoop;
    private long _updatesPublished;
    private int _disposed;
    private DriverStatus _status;

    public OpcUaCommunicationDriver(
        string driverId,
        string name,
        IOpcUaRuntimeSessionFactory sessionFactory,
        IReadOnlyList<TimeSpan>? reconnectDelays = null)
    {
        if (string.IsNullOrWhiteSpace(driverId)) throw new ArgumentException("Driver ID is required.", nameof(driverId));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Driver name is required.", nameof(name));
        DriverId = driverId.Trim();
        Name = name.Trim();
        _sessions = new OpcUaRuntimeSessionSupervisor(sessionFactory, reconnectDelays);
        _status = NewStatus(DriverState.Stopped);
    }

    public string DriverId { get; }
    public string Name { get; }
    public DriverCapabilities Capabilities => DriverCapabilities.Read |
        DriverCapabilities.Write |
        DriverCapabilities.Subscribe |
        DriverCapabilities.Diagnostics |
        DriverCapabilities.SourceTimestamp |
        DriverCapabilities.ServerTimestamp;
    public DriverStatus Status => _status;

    public async Task StartAsync(IReadOnlyCollection<TagDefinition> tags, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(tags);
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_runtimeCts is { IsCancellationRequested: false }) return;
            _status = NewStatus(DriverState.Starting);
            var bindings = BuildBindings(tags);

            try
            {
                await _sessions.ConnectAsync(bindings.Values.ToArray(), cancellationToken).ConfigureAwait(false);
                _bindings = bindings;
                _cache.Clear();
                _runtimeCts = new CancellationTokenSource();
                _status = NewStatus(DriverState.Running);
                _subscriptionLoop = PumpSubscriptionsAsync(_runtimeCts.Token);
            }
            catch
            {
                _status = NewStatus(DriverState.Faulted, "OPC UA runtime session could not be started.");
                throw;
            }
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var cts = _runtimeCts;
            if (cts is null)
            {
                await _sessions.DisconnectAsync().ConfigureAwait(false);
                _status = NewStatus(DriverState.Stopped);
                return;
            }

            _status = NewStatus(DriverState.Stopping);
            await cts.CancelAsync().ConfigureAwait(false);
            if (_subscriptionLoop is not null)
            {
                try { await _subscriptionLoop.WaitAsync(cancellationToken).ConfigureAwait(false); }
                catch (OperationCanceledException) when (cts.IsCancellationRequested) { }
            }

            cts.Dispose();
            _runtimeCts = null;
            _subscriptionLoop = null;
            await _sessions.DisconnectAsync().ConfigureAwait(false);
            _status = NewStatus(DriverState.Stopped);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async Task<TagValue> ReadAsync(TagDefinition tag, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        var binding = GetBinding(tag);
        if (_cache.TryGetValue(tag.Id, out var cached)) return cached;
        var session = _sessions.CurrentSession
            ?? throw new InvalidOperationException("OPC UA runtime session is not available.");
        var observed = await session.ReadAsync(binding, cancellationToken).ConfigureAwait(false);
        if (observed.TagId != tag.Id)
            throw new InvalidOperationException($"OPC UA read returned TAG '{observed.TagId}' instead of '{tag.Id}'.");
        return Publish(observed);
    }

    public async Task<IReadOnlyDictionary<Guid, TagValue>> ReadManyAsync(
        IReadOnlyCollection<TagDefinition> tags,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(tags);
        var result = new Dictionary<Guid, TagValue>(tags.Count);
        foreach (var tag in tags)
        {
            cancellationToken.ThrowIfCancellationRequested();
            result[tag.Id] = await ReadAsync(tag, cancellationToken).ConfigureAwait(false);
        }
        return result;
    }

    public async Task WriteAsync(TagDefinition tag, object? value, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        var binding = GetBinding(tag);
        OpcUaRuntimeValueValidator.ValidateWrite(binding.Tag, value);
        var session = _sessions.CurrentSession
            ?? throw new InvalidOperationException("OPC UA runtime session is not available.");
        await session.WriteAsync(binding, value!, cancellationToken).ConfigureAwait(false);
    }

    public IAsyncEnumerable<TagValue> SubscribeAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        return _updates.Reader.ReadAllAsync(cancellationToken);
    }

    private async Task PumpSubscriptionsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var session = _sessions.CurrentSession;
            if (session is null)
            {
                session = await _sessions.ReconnectUntilAvailableAsync(
                    _bindings.Values.ToArray(),
                    attempt => _status = NewStatus(DriverState.Faulted, $"OPC UA reconnect attempt {attempt} failed."),
                    cancellationToken).ConfigureAwait(false);
                _status = NewStatus(DriverState.Running, "OPC UA runtime session reconnected.");
            }

            try
            {
                await foreach (var observed in session.SubscribeAsync(cancellationToken).WithCancellation(cancellationToken))
                {
                    if (_bindings.ContainsKey(observed.TagId)) Publish(observed);
                }
                if (!cancellationToken.IsCancellationRequested)
                    throw new InvalidOperationException("OPC UA subscription ended unexpectedly.");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                _status = NewStatus(DriverState.Faulted, "OPC UA subscription interrupted. Reconnecting.");
                PublishCommunicationFailure();
                await _sessions.InvalidateAsync(session).ConfigureAwait(false);
            }
        }
    }

    private void PublishCommunicationFailure()
    {
        foreach (var binding in _bindings.Values)
        {
            _cache.TryGetValue(binding.Tag.Id, out var previous);
            Publish(new TagValue(binding.Tag.Id, previous?.Value, DateTimeOffset.UtcNow, TagQuality.BadCommunication, DriverId)
            {
                SourceTimestamp = previous?.SourceTimestamp,
                ServerTimestamp = previous?.ServerTimestamp
            });
        }
    }

    private TagValue Publish(OpcUaRuntimeDataValue observed) => Publish(new TagValue(
        observed.TagId, observed.Value, DateTimeOffset.UtcNow, observed.Quality, DriverId)
    {
        SourceTimestamp = observed.SourceTimestamp,
        ServerTimestamp = observed.ServerTimestamp
    });

    private TagValue Publish(TagValue value)
    {
        _cache[value.TagId] = value;
        var count = Interlocked.Increment(ref _updatesPublished);
        _updates.Writer.TryWrite(value);
        _status = _status with { Timestamp = DateTimeOffset.UtcNow, UpdatesPublished = count };
        return value;
    }

    private OpcUaRuntimeBinding GetBinding(TagDefinition tag)
    {
        ArgumentNullException.ThrowIfNull(tag);
        return _bindings.TryGetValue(tag.Id, out var binding)
            ? binding
            : throw new KeyNotFoundException($"OPC UA TAG '{tag.Id}' is not registered in driver '{DriverId}'.");
    }

    private static IReadOnlyDictionary<Guid, OpcUaRuntimeBinding> BuildBindings(IReadOnlyCollection<TagDefinition> tags)
    {
        var result = new Dictionary<Guid, OpcUaRuntimeBinding>(tags.Count);
        foreach (var tag in tags)
        {
            if (!result.TryAdd(tag.Id, OpcUaRuntimeBinding.FromTag(tag)))
                throw new InvalidOperationException($"OPC UA TAG '{tag.Id}' is registered more than once.");
        }
        return result;
    }

    private DriverStatus NewStatus(DriverState state, string? message = null) =>
        new(DriverId, Name, state, DateTimeOffset.UtcNow, message, _updatesPublished);

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

    public async ValueTask DisposeAsync()
    {
        if (Volatile.Read(ref _disposed) != 0) return;
        await StopAsync(CancellationToken.None).ConfigureAwait(false);
        await _sessions.DisposeAsync().ConfigureAwait(false);
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _updates.Writer.TryComplete();
        _lifecycleGate.Dispose();
    }
}
