using System.Collections.Concurrent;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;

namespace Scada.Drivers.Simulation;

public sealed class SimulationDriver : ICommunicationDriver
{
    private readonly ICurrentTagCache _cache;
    private readonly ITagRegistry _registry;
    private readonly IReadOnlyList<SimulationPoint> _points;
    private readonly ConcurrentDictionary<Guid, object?> _manualValues = new();
    private CancellationTokenSource? _cts;
    private Task? _loop;
    private long _updatesPublished;
    private DateTimeOffset _startedAt;

    public SimulationDriver(
        ICurrentTagCache cache,
        ITagRegistry registry,
        IEnumerable<SimulationPoint> points,
        TimeSpan? scanRate = null)
    {
        _cache = cache;
        _registry = registry;
        _points = points.ToArray();
        ScanRate = scanRate ?? TimeSpan.FromMilliseconds(500);
        Status = new(DriverId, Name, DriverState.Stopped, DateTimeOffset.UtcNow);
    }

    public string DriverId => "builtin.simulation";
    public string Name => "Simulation Driver";
    public DriverCapabilities Capabilities =>
        DriverCapabilities.Read | DriverCapabilities.Write | DriverCapabilities.Subscribe | DriverCapabilities.Diagnostics;
    public DriverStatus Status { get; private set; }
    public IReadOnlyCollection<TagDefinition> Tags => _points.Select(x => x.Tag).ToArray();
    public TimeSpan ScanRate { get; }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_loop is { IsCompleted: false }) return Task.CompletedTask;

        Status = new(DriverId, Name, DriverState.Starting, DateTimeOffset.UtcNow);
        foreach (var point in _points)
        {
            if (_registry.TryGet(point.Tag.Id, out var existing) && existing is not null)
            {
                if (!existing.Path.Equals(point.Tag.Path, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        $"Simulation tag '{point.Tag.Id}' is already registered with path '{existing.Path}', expected '{point.Tag.Path}'.");
                continue;
            }

            _registry.Register(point.Tag);
        }

        _cts?.Dispose();
        _startedAt = DateTimeOffset.UtcNow;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _loop = RunAsync(_cts.Token);
        Status = new(DriverId, Name, DriverState.Running, DateTimeOffset.UtcNow);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        var cts = _cts;
        if (cts is null) return;

        Status = new(DriverId, Name, DriverState.Stopping, DateTimeOffset.UtcNow);
        await cts.CancelAsync();
        if (_loop is not null)
        {
            try { await _loop.WaitAsync(cancellationToken); }
            catch (OperationCanceledException) { }
        }

        Status = new(DriverId, Name, DriverState.Stopped, DateTimeOffset.UtcNow, UpdatesPublished: _updatesPublished);
        cts.Dispose();
        if (ReferenceEquals(_cts, cts)) _cts = null;
        _loop = null;
    }

    public ValueTask<TagValue?> ReadAsync(Guid tagId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _cache.TryGet(tagId, out var value);
        return ValueTask.FromResult(value);
    }

    public async ValueTask WriteAsync(Guid tagId, object? value, CancellationToken cancellationToken = default)
    {
        var point = _points.FirstOrDefault(x => x.Tag.Id == tagId)
            ?? throw new KeyNotFoundException($"Simulation tag '{tagId}' was not found.");

        _manualValues[tagId] = value;
        await _cache.UpdateAsync(point.Tag, TagValue.Good(tagId, value, DriverId), cancellationToken);
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(ScanRate);
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                var elapsed = (DateTimeOffset.UtcNow - _startedAt).TotalSeconds;
                foreach (var point in _points)
                {
                    var value = Calculate(point, elapsed);
                    await _cache.UpdateAsync(point.Tag, TagValue.Good(point.Tag.Id, value, DriverId), cancellationToken);
                    Interlocked.Increment(ref _updatesPublished);
                }
                Status = Status with { Timestamp = DateTimeOffset.UtcNow, UpdatesPublished = _updatesPublished };
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception ex)
        {
            Status = new(DriverId, Name, DriverState.Faulted, DateTimeOffset.UtcNow, ex.Message, _updatesPublished);
            throw;
        }
    }

    private object? Calculate(SimulationPoint point, double elapsedSeconds)
    {
        if (_manualValues.TryGetValue(point.Tag.Id, out var manual)) return manual;

        var range = point.Maximum - point.Minimum;
        var period = Math.Max(point.PeriodSeconds, 0.001);
        var numeric = point.SignalType switch
        {
            SimulationSignalType.Constant => point.ConstantValue,
            SimulationSignalType.Ramp => point.Minimum + range * ((elapsedSeconds % period) / period),
            SimulationSignalType.Sine => point.Minimum + range / 2d + (range / 2d) * Math.Sin(2d * Math.PI * elapsedSeconds / period),
            SimulationSignalType.Random => point.Minimum + Random.Shared.NextDouble() * range,
            SimulationSignalType.Counter => point.Minimum + ((Math.Floor(elapsedSeconds / Math.Max(ScanRate.TotalSeconds, 0.001)) * point.Step) % Math.Max(range, 1)),
            SimulationSignalType.BooleanToggle => Math.Floor(elapsedSeconds / period) % 2 == 0 ? 1d : 0d,
            SimulationSignalType.Manual => point.ConstantValue,
            _ => point.ConstantValue
        };

        return point.Tag.DataType switch
        {
            TagDataType.Boolean => numeric >= 0.5,
            TagDataType.Int16 => Convert.ToInt16(Math.Round(numeric)),
            TagDataType.Int32 => Convert.ToInt32(Math.Round(numeric)),
            TagDataType.Int64 => Convert.ToInt64(Math.Round(numeric)),
            TagDataType.Float => Convert.ToSingle(numeric),
            TagDataType.Double => numeric,
            TagDataType.String => numeric.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
            _ => numeric
        };
    }

    public ValueTask DisposeAsync() => new(StopAsync());
}
