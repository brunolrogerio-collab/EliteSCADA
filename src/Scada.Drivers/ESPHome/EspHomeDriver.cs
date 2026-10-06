using System.Globalization;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;

namespace Scada.Drivers.ESPHome;

public sealed class EspHomeDriver : ICommunicationDriver
{
    private sealed record PendingWrite(object? ExpectedValue, TaskCompletionSource<TagValue> Completion);

    private readonly EspHomeConnectionSettings _settings;
    private readonly ICurrentTagCache _cache;
    private readonly ITagRegistry _registry;
    private readonly IReadOnlyCollection<EspHomePoint> _points;
    private readonly IReadOnlyDictionary<Guid, EspHomePoint> _pointsById;
    private readonly IReadOnlyDictionary<EspHomeEntityAddress, EspHomePoint> _pointsByAddress;
    private readonly IEspHomeNativeClient _client;
    private readonly Dictionary<Guid, PendingWrite> _pendingWrites = new();
    private readonly object _gate = new();
    private CancellationTokenSource? _runCts;
    private Task? _stateTask;
    private bool _disposed;

    public EspHomeDriver(
        string driverId,
        string name,
        EspHomeConnectionSettings settings,
        ICurrentTagCache cache,
        ITagRegistry registry,
        IReadOnlyCollection<EspHomePoint> points,
        IEspHomeNativeClient client)
    {
        DriverId = driverId;
        Name = name;
        _settings = settings;
        _cache = cache;
        _registry = registry;
        _points = points;
        _pointsById = points.ToDictionary(x => x.Tag.Id);
        _pointsByAddress = points.ToDictionary(x => x.Address);
        _client = client;
        Tags = points.Select(x => x.Tag).ToArray();
        Status = new DriverStatus(driverId, name, DriverState.Stopped, DateTimeOffset.UtcNow);
    }

    public string DriverId { get; }
    public string Name { get; }
    public DriverCapabilities Capabilities =>
        DriverCapabilities.Read | DriverCapabilities.Write | DriverCapabilities.Subscribe;
    public DriverStatus Status { get; private set; }
    public IReadOnlyCollection<TagDefinition> Tags { get; }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (_runCts is not null) return;
        _settings.Validate();
        foreach (var point in _points) _registry.Upsert(point.Tag);

        Status = new DriverStatus(DriverId, Name, DriverState.Starting, DateTimeOffset.UtcNow);
        _runCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            _ = await _client.ConnectAsync(_runCts.Token).ConfigureAwait(false);
            await _client.SubscribeStatesAsync(_runCts.Token).ConfigureAwait(false);
            _stateTask = RunStateSubscriptionAsync(_runCts.Token);
            Status = new DriverStatus(DriverId, Name, DriverState.Running, DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            Status = new DriverStatus(DriverId, Name, DriverState.Faulted, DateTimeOffset.UtcNow, Sanitize(ex.Message));
            _runCts.Dispose();
            _runCts = null;
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        var cts = _runCts;
        if (cts is null) return;
        cts.Cancel();

        lock (_gate)
        {
            foreach (var pending in _pendingWrites.Values)
                pending.Completion.TrySetCanceled();
            _pendingWrites.Clear();
        }

        await _client.DisconnectAsync(cancellationToken).ConfigureAwait(false);
        if (_stateTask is not null)
        {
            try { await _stateTask.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
            catch (EndOfStreamException) { }
            catch (IOException) { }
        }

        _stateTask = null;
        _runCts = null;
        cts.Dispose();
        Status = new DriverStatus(DriverId, Name, DriverState.Stopped, DateTimeOffset.UtcNow);
    }

    public ValueTask<TagValue?> ReadAsync(Guid tagId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_pointsById.ContainsKey(tagId))
            throw new KeyNotFoundException($"ESPHome TAG '{tagId}' is not owned by this driver.");
        return ValueTask.FromResult(_cache.TryGet(tagId, out var value) ? value : null);
    }

    public async ValueTask WriteAsync(Guid tagId, object? value, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (!_pointsById.TryGetValue(tagId, out var point))
            throw new KeyNotFoundException($"ESPHome TAG '{tagId}' is not owned by this driver.");
        if (!point.CanWrite)
            throw new InvalidOperationException($"ESPHome TAG '{point.Tag.Path}' is read-only.");

        var expected = ConvertWriteValue(point.Tag.DataType, value);
        var completion = new TaskCompletionSource<TagValue>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate)
        {
            if (_pendingWrites.ContainsKey(tagId))
                throw new InvalidOperationException($"ESPHome TAG '{point.Tag.Path}' already has a pending authoritative write reconciliation.");
            _pendingWrites[tagId] = new PendingWrite(expected, completion);
        }

        try
        {
            await _client.SendCommandAsync(
                new EspHomeCommand(point.Address, point.WriteKind, expected),
                cancellationToken).ConfigureAwait(false);

            try
            {
                _ = await completion.Task
                    .WaitAsync(_settings.EffectiveWriteReconcileTimeout, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                await MarkWriteUncertainAsync(point, cancellationToken).ConfigureAwait(false);
                throw new TimeoutException(
                    $"ESPHome command for TAG '{point.Tag.Path}' was sent but no matching authoritative state arrived within {_settings.EffectiveWriteReconcileTimeout.TotalMilliseconds:0} ms.");
            }
        }
        finally
        {
            lock (_gate) _pendingWrites.Remove(tagId);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        await StopAsync(CancellationToken.None).ConfigureAwait(false);
        _disposed = true;
        await _client.DisposeAsync().ConfigureAwait(false);
    }

    private async Task RunStateSubscriptionAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            EspHomeStateUpdate update;
            try
            {
                update = await _client.ReceiveStateAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            if (!_pointsByAddress.TryGetValue(update.Address, out var point))
                continue;

            _cache.TryGet(point.Tag.Id, out var previous);
            var quality = update.MissingState ? TagQuality.Uncertain : TagQuality.Good;
            var value = update.MissingState ? previous?.Value : ConvertStateValue(point.Tag.DataType, update.Value);
            var sample = new TagValue(point.Tag.Id, value, DateTimeOffset.UtcNow, quality, DriverId)
            {
                SourceTimestamp = update.SourceTimestamp
            };
            await _cache.UpdateAsync(point.Tag, sample, cancellationToken).ConfigureAwait(false);

            if (quality == TagQuality.Good)
            {
                PendingWrite? pending;
                lock (_gate) _pendingWrites.TryGetValue(point.Tag.Id, out pending);
                if (pending is not null && ValuesEquivalent(point.Tag.DataType, pending.ExpectedValue, sample.Value))
                    pending.Completion.TrySetResult(sample);
            }
        }
    }

    private async Task MarkWriteUncertainAsync(EspHomePoint point, CancellationToken cancellationToken)
    {
        _cache.TryGet(point.Tag.Id, out var previous);
        var sample = new TagValue(
            point.Tag.Id,
            previous?.Value,
            DateTimeOffset.UtcNow,
            TagQuality.Uncertain,
            DriverId)
        {
            SourceTimestamp = previous?.SourceTimestamp
        };
        await _cache.UpdateAsync(point.Tag, sample, cancellationToken).ConfigureAwait(false);
    }

    private static object? ConvertStateValue(TagDataType type, object? value) => type switch
    {
        TagDataType.Boolean => Convert.ToBoolean(value, CultureInfo.InvariantCulture),
        TagDataType.Int16 => Convert.ToInt16(value, CultureInfo.InvariantCulture),
        TagDataType.Int32 => Convert.ToInt32(value, CultureInfo.InvariantCulture),
        TagDataType.Int64 => Convert.ToInt64(value, CultureInfo.InvariantCulture),
        TagDataType.Float => Convert.ToSingle(value, CultureInfo.InvariantCulture),
        TagDataType.Double => Convert.ToDouble(value, CultureInfo.InvariantCulture),
        TagDataType.String or TagDataType.Enum => Convert.ToString(value, CultureInfo.InvariantCulture),
        _ => value
    };

    private static object? ConvertWriteValue(TagDataType type, object? value) =>
        ConvertStateValue(type, value);

    private static bool ValuesEquivalent(TagDataType type, object? expected, object? observed)
    {
        if (expected is null || observed is null) return expected is null && observed is null;
        return type switch
        {
            TagDataType.Float or TagDataType.Double =>
                Math.Abs(Convert.ToDouble(expected, CultureInfo.InvariantCulture) -
                         Convert.ToDouble(observed, CultureInfo.InvariantCulture)) <= 0.001d,
            TagDataType.Boolean =>
                Convert.ToBoolean(expected, CultureInfo.InvariantCulture) ==
                Convert.ToBoolean(observed, CultureInfo.InvariantCulture),
            _ => string.Equals(
                Convert.ToString(expected, CultureInfo.InvariantCulture),
                Convert.ToString(observed, CultureInfo.InvariantCulture),
                StringComparison.Ordinal)
        };
    }

    private static string Sanitize(string message)
    {
        var clean = message.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return clean.Length <= 512 ? clean : clean[..512];
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
