using Scada.Core.Tags;
using Scada.Drivers.Abstractions;

namespace Scada.Drivers.OpcUa;

public sealed class OpcUaCommunicationDriver : ICommunicationDriver
{
    private readonly ICurrentTagCache _cache;
    private readonly ITagRegistry _registry;
    private readonly IReadOnlyList<OpcUaRuntimeBinding> _bindings;
    private readonly IReadOnlyDictionary<Guid, OpcUaRuntimeBinding> _bindingsByTagId;
    private readonly OpcUaRuntimeSessionSupervisor _sessions;
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private CancellationTokenSource? _runtimeCts;
    private Task? _subscriptionLoop;
    private long _updatesPublished;
    private int _disposed;

    public OpcUaCommunicationDriver(
        string driverId,
        string name,
        ICurrentTagCache cache,
        ITagRegistry registry,
        IEnumerable<TagDefinition> tags,
        IOpcUaRuntimeSessionFactory sessionFactory,
        IReadOnlyList<TimeSpan>? reconnectDelays = null)
    {
        if (string.IsNullOrWhiteSpace(driverId)) throw new ArgumentException("Driver ID is required.", nameof(driverId));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Driver name is required.", nameof(name));
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(tags);
        ArgumentNullException.ThrowIfNull(sessionFactory);

        DriverId = driverId.Trim();
        Name = name.Trim();
        _cache = cache;
        _registry = registry;
        _bindings = tags.Select(OpcUaRuntimeBinding.FromTag).ToArray();
        if (_bindings.Count == 0) throw new ArgumentException("At least one OPC UA TAG is required.", nameof(tags));
        if (_bindings.Select(x => x.Tag.Id).Distinct().Count() != _bindings.Count)
            throw new ArgumentException("Each OPC UA binding must reference a unique TAG ID.", nameof(tags));

        _bindingsByTagId = _bindings.ToDictionary(x => x.Tag.Id);
        _sessions = new OpcUaRuntimeSessionSupervisor(sessionFactory, reconnectDelays);
        Status = NewStatus(DriverState.Stopped);
    }

    public string DriverId { get; }
    public string Name { get; }
    public DriverCapabilities Capabilities =>
        DriverCapabilities.Read |
        DriverCapabilities.Write |
        DriverCapabilities.Subscribe |
        DriverCapabilities.SourceTimestamp |
        DriverCapabilities.ServerTimestamp;
    public DriverStatus Status { get; private set; }
    public IReadOnlyCollection<TagDefinition> Tags => _bindings.Select(x => x.Tag).ToArray();

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_runtimeCts is { IsCancellationRequested: false }) return;
            Status = NewStatus(DriverState.Starting);
            RegisterTags();

            try
            {
                await _sessions.ConnectAsync(_bindings, cancellationToken).ConfigureAwait(false);
                _runtimeCts = new CancellationTokenSource();
                Status = NewStatus(DriverState.Running);
                _subscriptionLoop = PumpSubscriptionsAsync(_runtimeCts.Token);
            }
            catch (Exception ex)
            {
                await _sessions.DisconnectAsync().ConfigureAwait(false);
                Status = NewStatus(DriverState.Faulted, ex.Message);
                throw;
            }
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var cts = _runtimeCts;
            if (cts is null)
            {
                await _sessions.DisconnectAsync().ConfigureAwait(false);
                Status = NewStatus(DriverState.Stopped);
                return;
            }

            Status = NewStatus(DriverState.Stopping);
            await cts.CancelAsync().ConfigureAwait(false);
            if (_subscriptionLoop is not null)
            {
                try
                {
                    await _subscriptionLoop.WaitAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cts.IsCancellationRequested)
                {
                }
            }

            await _sessions.DisconnectAsync().ConfigureAwait(false);
            cts.Dispose();
            _runtimeCts = null;
            _subscriptionLoop = null;
            Status = NewStatus(DriverState.Stopped);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public ValueTask<TagValue?> ReadAsync(Guid tagId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        GetBinding(tagId);
        _cache.TryGet(tagId, out var value);
        return ValueTask.FromResult(value);
    }

    public async ValueTask WriteAsync(Guid tagId, object? value, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        var binding = GetBinding(tagId);
        OpcUaRuntimeValueValidator.ValidateWrite(binding.Tag, value);
        var session = _sessions.CurrentSession
            ?? throw new InvalidOperationException("OPC UA runtime session is not available.");

        await session.WriteAsync(binding, value!, cancellationToken).ConfigureAwait(false);
        await PublishAsync(new OpcUaRuntimeDataValue(tagId, value, TagQuality.Good), cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task PumpSubscriptionsAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var session = _sessions.CurrentSession;
                if (session is null)
                {
                    session = await _sessions.ReconnectUntilAvailableAsync(
                        _bindings,
                        attempt => Status = NewStatus(
                            DriverState.Faulted,
                            $"OPC UA reconnect attempt {attempt} failed."),
                        cancellationToken).ConfigureAwait(false);
                    Status = NewStatus(DriverState.Running, "OPC UA runtime session reconnected.");
                }

                try
                {
                    await foreach (var observed in session.SubscribeAsync(cancellationToken).WithCancellation(cancellationToken))
                    {
                        if (_bindingsByTagId.ContainsKey(observed.TagId))
                            await PublishAsync(observed, cancellationToken).ConfigureAwait(false);
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
                    Status = NewStatus(DriverState.Faulted, "OPC UA subscription interrupted. Reconnecting.");
                    await PublishCommunicationFailureAsync(cancellationToken).ConfigureAwait(false);
                    await _sessions.InvalidateAsync(session).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            Status = NewStatus(DriverState.Faulted, ex.Message);
        }
    }

    private async Task PublishCommunicationFailureAsync(CancellationToken cancellationToken)
    {
        foreach (var binding in _bindings)
        {
            _cache.TryGet(binding.Tag.Id, out var previous);
            var failed = new TagValue(
                binding.Tag.Id,
                previous?.Value,
                DateTimeOffset.UtcNow,
                TagQuality.BadCommunication,
                DriverId)
            {
                SourceTimestamp = previous?.SourceTimestamp,
                ServerTimestamp = previous?.ServerTimestamp
            };
            await UpdateCacheAsync(binding.Tag, failed, cancellationToken).ConfigureAwait(false);
        }
    }

    private Task PublishAsync(OpcUaRuntimeDataValue observed, CancellationToken cancellationToken)
    {
        var binding = GetBinding(observed.TagId);
        var value = new TagValue(
            observed.TagId,
            observed.Value,
            DateTimeOffset.UtcNow,
            observed.Quality,
            DriverId)
        {
            SourceTimestamp = observed.SourceTimestamp,
            ServerTimestamp = observed.ServerTimestamp
        };
        return UpdateCacheAsync(binding.Tag, value, cancellationToken);
    }

    private async Task UpdateCacheAsync(TagDefinition tag, TagValue value, CancellationToken cancellationToken)
    {
        await _cache.UpdateAsync(tag, value, cancellationToken).ConfigureAwait(false);
        var count = Interlocked.Increment(ref _updatesPublished);
        Status = Status with { Timestamp = DateTimeOffset.UtcNow, UpdatesPublished = count };
    }

    private OpcUaRuntimeBinding GetBinding(Guid tagId) =>
        _bindingsByTagId.TryGetValue(tagId, out var binding)
            ? binding
            : throw new KeyNotFoundException($"OPC UA TAG '{tagId}' was not found in driver '{DriverId}'.");

    private void RegisterTags()
    {
        foreach (var binding in _bindings)
        {
            if (_registry.TryGet(binding.Tag.Id, out var existing) && existing is not null)
            {
                if (!existing.Path.Equals(binding.Tag.Path, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        $"OPC UA TAG '{binding.Tag.Id}' is already registered with path '{existing.Path}', expected '{binding.Tag.Path}'.");
                continue;
            }
            _registry.Register(binding.Tag);
        }
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
        if (Interlocked.Exchange(ref _disposed, 1) == 0) _lifecycleGate.Dispose();
    }
}
