using Scada.Core.Tags;
using Scada.Drivers.Abstractions;

namespace Scada.Drivers.Modbus;

public sealed class ModbusTcpDriver : ICommunicationDriver
{
    private readonly ICurrentTagCache _cache;
    private readonly ITagRegistry _registry;
    private readonly IReadOnlyList<ModbusPoint> _points;
    private readonly IReadOnlyList<ModbusPollBlock> _pollBlocks;
    private readonly ModbusTcpTransport _transport;
    private readonly Dictionary<Guid, ModbusPoint> _pointsByTagId;
    private CancellationTokenSource? _cts;
    private Task? _loop;
    private long _updatesPublished;

    public ModbusTcpDriver(
        string driverId,
        string name,
        string host,
        ICurrentTagCache cache,
        ITagRegistry registry,
        IEnumerable<ModbusPoint> points,
        int port = 502,
        TimeSpan? scanRate = null,
        TimeSpan? requestTimeout = null,
        int maxGapElements = 8)
    {
        if (string.IsNullOrWhiteSpace(driverId)) throw new ArgumentException("Driver ID is required.", nameof(driverId));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Driver name is required.", nameof(name));
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(points);
        if (maxGapElements < 0) throw new ArgumentOutOfRangeException(nameof(maxGapElements));

        DriverId = driverId.Trim();
        Name = name.Trim();
        _cache = cache;
        _registry = registry;
        _points = points.ToArray();
        if (_points.Count == 0) throw new ArgumentException("At least one Modbus point is required.", nameof(points));
        foreach (var point in _points) point.Validate();
        if (_points.Select(x => x.Tag.Id).Distinct().Count() != _points.Count)
            throw new ArgumentException("Each Modbus point must reference a unique TAG ID.", nameof(points));

        _pointsByTagId = _points.ToDictionary(x => x.Tag.Id);
        _pollBlocks = BuildPollBlocks(_points, maxGapElements);
        _transport = new ModbusTcpTransport(host, port, requestTimeout);
        ScanRate = scanRate ?? TimeSpan.FromSeconds(1);
        if (ScanRate <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(scanRate));
        Status = new DriverStatus(DriverId, Name, DriverState.Stopped, DateTimeOffset.UtcNow);
    }

    public string DriverId { get; }
    public string Name { get; }
    public DriverCapabilities Capabilities => DriverCapabilities.Read | DriverCapabilities.Write | DriverCapabilities.Diagnostics;
    public DriverStatus Status { get; private set; }
    public IReadOnlyCollection<TagDefinition> Tags => _points.Select(x => x.Tag).ToArray();
    public TimeSpan ScanRate { get; }
    public IReadOnlyCollection<ModbusPollBlockInfo> PollBlocks => _pollBlocks
        .Select(x => new ModbusPollBlockInfo(x.UnitId, x.Area, x.StartAddress, x.Quantity, x.Points.Count))
        .ToArray();

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_loop is { IsCompleted: false }) return Task.CompletedTask;

        Status = new DriverStatus(DriverId, Name, DriverState.Starting, DateTimeOffset.UtcNow);
        foreach (var point in _points) _registry.Register(point.Tag);
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _loop = RunAsync(_cts.Token);
        Status = new DriverStatus(DriverId, Name, DriverState.Running, DateTimeOffset.UtcNow);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_cts is null) return;
        Status = new DriverStatus(DriverId, Name, DriverState.Stopping, DateTimeOffset.UtcNow, UpdatesPublished: _updatesPublished);
        await _cts.CancelAsync();
        if (_loop is not null)
        {
            try { await _loop.WaitAsync(cancellationToken); }
            catch (OperationCanceledException) when (_cts.IsCancellationRequested) { }
        }
        await _transport.DisconnectAsync();
        Status = new DriverStatus(DriverId, Name, DriverState.Stopped, DateTimeOffset.UtcNow, UpdatesPublished: _updatesPublished);
    }

    public ValueTask<TagValue?> ReadAsync(Guid tagId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_pointsByTagId.ContainsKey(tagId))
            throw new KeyNotFoundException($"Modbus TAG '{tagId}' was not found in driver '{DriverId}'.");
        _cache.TryGet(tagId, out var value);
        return ValueTask.FromResult(value);
    }

    public async ValueTask WriteAsync(Guid tagId, object? value, CancellationToken cancellationToken = default)
    {
        if (!_pointsByTagId.TryGetValue(tagId, out var point))
            throw new KeyNotFoundException($"Modbus TAG '{tagId}' was not found in driver '{DriverId}'.");
        if (!point.Writable)
            throw new InvalidOperationException($"Modbus TAG '{point.Tag.Path}' is not writable.");

        if (point.Area == ModbusDataArea.Coil)
        {
            await _transport.WriteSingleCoilAsync(point.UnitId, point.Address, ModbusValueCodec.EncodeBit(point, value), cancellationToken);
        }
        else if (point.Area == ModbusDataArea.HoldingRegister)
        {
            var registers = ModbusValueCodec.EncodeRegisters(point, value);
            if (registers.Length == 1)
                await _transport.WriteSingleRegisterAsync(point.UnitId, point.Address, registers[0], cancellationToken);
            else
                await _transport.WriteMultipleRegistersAsync(point.UnitId, point.Address, registers, cancellationToken);
        }
        else
        {
            throw new InvalidOperationException($"Modbus area '{point.Area}' is read-only.");
        }

        await PublishAsync(point, value, TagQuality.Good, cancellationToken);
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(ScanRate);
            while (!cancellationToken.IsCancellationRequested)
            {
                await PollOnceAsync(cancellationToken);
                if (!await timer.WaitForNextTickAsync(cancellationToken)) break;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception ex)
        {
            Status = new DriverStatus(DriverId, Name, DriverState.Faulted, DateTimeOffset.UtcNow, ex.Message, _updatesPublished);
        }
    }

    private async Task PollOnceAsync(CancellationToken cancellationToken)
    {
        var failedBlocks = 0;
        string? lastError = null;

        foreach (var block in _pollBlocks)
        {
            try
            {
                if (block.Area is ModbusDataArea.Coil or ModbusDataArea.DiscreteInput)
                    await PollBitsAsync(block, cancellationToken);
                else
                    await PollRegistersAsync(block, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (ex is IOException or TimeoutException)
            {
                failedBlocks++;
                lastError = ex.Message;
                foreach (var point in block.Points)
                    await PublishCommunicationFailureAsync(point, cancellationToken);
            }
        }

        var message = failedBlocks == 0
            ? null
            : $"{failedBlocks} of {_pollBlocks.Count} Modbus poll block(s) failed. Last error: {lastError}";
        Status = new DriverStatus(DriverId, Name, DriverState.Running, DateTimeOffset.UtcNow, message, _updatesPublished);
    }

    private async Task PollBitsAsync(ModbusPollBlock block, CancellationToken cancellationToken)
    {
        var values = await _transport.ReadBitsAsync(
            block.UnitId,
            block.Area,
            block.StartAddress,
            block.Quantity,
            cancellationToken);

        foreach (var point in block.Points)
        {
            var index = point.Address - block.StartAddress;
            await PublishAsync(point, ModbusValueCodec.DecodeBit(point, values[index]), TagQuality.Good, cancellationToken);
        }
    }

    private async Task PollRegistersAsync(ModbusPollBlock block, CancellationToken cancellationToken)
    {
        var registers = await _transport.ReadRegistersAsync(
            block.UnitId,
            block.Area,
            block.StartAddress,
            block.Quantity,
            cancellationToken);

        foreach (var point in block.Points)
        {
            var offset = point.Address - block.StartAddress;
            var value = ModbusValueCodec.DecodeRegisters(point, registers.AsSpan(offset, point.RegisterCount));
            await PublishAsync(point, value, TagQuality.Good, cancellationToken);
        }
    }

    private async Task PublishCommunicationFailureAsync(ModbusPoint point, CancellationToken cancellationToken)
    {
        _cache.TryGet(point.Tag.Id, out var previous);
        await PublishAsync(point, previous?.Value, TagQuality.BadCommunication, cancellationToken);
    }

    private async Task PublishAsync(ModbusPoint point, object? value, TagQuality quality, CancellationToken cancellationToken)
    {
        var sample = new TagValue(point.Tag.Id, value, DateTimeOffset.UtcNow, quality, DriverId);
        await _cache.UpdateAsync(point.Tag, sample, cancellationToken);
        Interlocked.Increment(ref _updatesPublished);
    }

    private static IReadOnlyList<ModbusPollBlock> BuildPollBlocks(IReadOnlyList<ModbusPoint> points, int maxGapElements)
    {
        var result = new List<ModbusPollBlock>();
        foreach (var group in points.GroupBy(x => new { x.UnitId, x.Area }))
        {
            var ordered = group.OrderBy(x => x.Address).ThenBy(x => x.EndAddressExclusive).ToArray();
            var current = new List<ModbusPoint>();
            var start = 0;
            var end = 0;
            var maxQuantity = group.Key.Area is ModbusDataArea.Coil or ModbusDataArea.DiscreteInput ? 2000 : 125;

            foreach (var point in ordered)
            {
                if (current.Count == 0)
                {
                    current.Add(point);
                    start = point.Address;
                    end = point.EndAddressExclusive;
                    continue;
                }

                var gap = Math.Max(0, point.Address - end);
                var proposedEnd = Math.Max(end, point.EndAddressExclusive);
                var proposedQuantity = proposedEnd - start;
                if (gap <= maxGapElements && proposedQuantity <= maxQuantity)
                {
                    current.Add(point);
                    end = proposedEnd;
                }
                else
                {
                    result.Add(CreateBlock(group.Key.UnitId, group.Key.Area, start, end, current));
                    current = new List<ModbusPoint> { point };
                    start = point.Address;
                    end = point.EndAddressExclusive;
                }
            }

            if (current.Count > 0)
                result.Add(CreateBlock(group.Key.UnitId, group.Key.Area, start, end, current));
        }

        return result;
    }

    private static ModbusPollBlock CreateBlock(byte unitId, ModbusDataArea area, int start, int end, IReadOnlyList<ModbusPoint> points) =>
        new(unitId, area, checked((ushort)start), checked((ushort)(end - start)), points.ToArray());

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _cts?.Dispose();
        await _transport.DisposeAsync();
    }

    private sealed record ModbusPollBlock(
        byte UnitId,
        ModbusDataArea Area,
        ushort StartAddress,
        ushort Quantity,
        IReadOnlyList<ModbusPoint> Points);
}

public sealed record ModbusPollBlockInfo(
    byte UnitId,
    ModbusDataArea Area,
    ushort StartAddress,
    ushort Quantity,
    int PointCount);
