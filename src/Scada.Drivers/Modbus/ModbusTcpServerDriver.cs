using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;

namespace Scada.Drivers.Modbus;

public sealed class ModbusTcpServerDriver :
    ICommunicationDriver,
    ICommunicationDiagnosticsSource,
    ICommunicationDriverReadinessSource,
    ICommunicationDriverResourceClaimSource
{
    private readonly ICurrentTagCache _cache;
    private readonly ITagRegistry _registry;
    private readonly IReadOnlyList<ModbusServerPoint> _points;
    private readonly Dictionary<Guid, ModbusServerPoint> _byTagId;
    private readonly ModbusServerRegisterMap _map;
    private readonly ModbusServerProtocolHandler _handler;
    private readonly IPAddress _bindAddress;
    private readonly int _port;
    private readonly int _maxClients;
    private readonly TimeSpan _clientIdleTimeout;
    private readonly SemaphoreSlim _clientSlots;
    private readonly Func<bool> _externalEffectAuthority;
    private readonly ConcurrentDictionary<long, Task> _clients = new();
    private readonly object _diagnosticsGate = new();
    private readonly string _runtimeInstanceId = Guid.NewGuid().ToString("N");
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _acceptLoop;
    private long _clientSequence;
    private int _connectedClients;
    private long _readRequests;
    private long _writeRequests;
    private long _rejectedWrites;
    private long _outOfRangeRequests;
    private long _protocolExceptions;
    private long _updatesPublished;
    private long _connections;
    private long _disconnections;
    private long _operationCount;
    private long _failedOperations;
    private long _lastOperationDurationTicks;
    private long _totalOperationDurationTicks;
    private string? _lastError;
    private DateTimeOffset? _lastSuccessfulCommunicationAt;
    private DateTimeOffset? _lastFailedCommunicationAt;
    private CommunicationDriverOperationalState _communicationState = CommunicationDriverOperationalState.Stopped;
    private CommunicationDriverReadinessState _readinessState = CommunicationDriverReadinessState.NotStarted;
    private DateTimeOffset _stateChangedAt = DateTimeOffset.UtcNow;

    public ModbusTcpServerDriver(
        string driverId,
        string name,
        string bindAddress,
        int port,
        byte unitId,
        IEnumerable<ModbusHoldingRegisterRange> ranges,
        IEnumerable<ModbusServerPoint> points,
        ICurrentTagCache cache,
        ITagRegistry registry,
        int maxClients = 16,
        TimeSpan? clientIdleTimeout = null,
        Func<bool>? externalEffectAuthority = null)
    {
        if (string.IsNullOrWhiteSpace(driverId)) throw new ArgumentException("Driver ID is required.", nameof(driverId));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Driver name is required.", nameof(name));
        if (!TryParseBindAddress(bindAddress, out _bindAddress))
            throw new ArgumentException($"Bind address '{bindAddress}' must be a local IP literal or wildcard.", nameof(bindAddress));
        if (port is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(port));
        if (unitId > 247) throw new ArgumentOutOfRangeException(nameof(unitId));
        if (maxClients is < 1 or > 128) throw new ArgumentOutOfRangeException(nameof(maxClients));

        DriverId = driverId.Trim();
        Name = name.Trim();
        _port = port;
        _maxClients = maxClients;
        _clientIdleTimeout = clientIdleTimeout ?? TimeSpan.FromSeconds(30);
        if (_clientIdleTimeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(clientIdleTimeout));
        _externalEffectAuthority = externalEffectAuthority ?? (() => true);
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _points = (points ?? throw new ArgumentNullException(nameof(points))).ToArray();
        if (_points.Count == 0) throw new ArgumentException("At least one Modbus Server TAG is required.", nameof(points));
        foreach (var point in _points)
        {
            point.Validate();
            if (point.Point.UnitId != unitId)
                throw new ArgumentException($"TAG '{point.Point.Tag.Path}' Unit ID must match server Unit ID {unitId}.", nameof(points));
        }
        _byTagId = _points.ToDictionary(point => point.Point.Tag.Id);
        _map = new ModbusServerRegisterMap(ranges, _points);
        _handler = new ModbusServerProtocolHandler(unitId, _map, PublishExternalUpdatesAsync);
        _clientSlots = new SemaphoreSlim(_maxClients, _maxClients);
        Status = new DriverStatus(DriverId, Name, DriverState.Stopped, DateTimeOffset.UtcNow);
    }

    public string DriverId { get; }
    public string Name { get; }
    public DriverCapabilities Capabilities => DriverCapabilities.Read | DriverCapabilities.Write | DriverCapabilities.Diagnostics;
    public DriverStatus Status { get; private set; }
    public IReadOnlyCollection<TagDefinition> Tags => _points.Select(point => point.Point.Tag).ToArray();
    public string BindEndpoint => $"{FormatAddress(_bindAddress)}:{_port}";
    public IReadOnlyCollection<CommunicationDriverResourceClaim> ResourceClaims => new[]
    {
        new CommunicationDriverResourceClaim(
            "tcp-listener",
            _port.ToString(CultureInfo.InvariantCulture),
            _bindAddress.ToString(),
            DriverId,
            Exclusive: true,
            WildcardIdentity: _bindAddress.Equals(IPAddress.Any) || _bindAddress.Equals(IPAddress.IPv6Any))
    };

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_acceptLoop is { IsCompleted: false }) return Task.CompletedTask;
        Status = new DriverStatus(DriverId, Name, DriverState.Starting, DateTimeOffset.UtcNow);
        SetState(CommunicationDriverOperationalState.Starting, CommunicationDriverReadinessState.Starting);

        foreach (var point in _points)
            if (!_registry.TryGet(point.Point.Tag.Id, out _))
                _registry.Register(point.Point.Tag);

        var listener = new TcpListener(_bindAddress, _port);
        if (_bindAddress.Equals(IPAddress.IPv6Any))
            listener.Server.DualMode = true;
        try
        {
            listener.Start(_maxClients);
        }
        catch
        {
            listener.Stop();
            SetState(CommunicationDriverOperationalState.Faulted, CommunicationDriverReadinessState.Faulted);
            Status = new DriverStatus(DriverId, Name, DriverState.Faulted, DateTimeOffset.UtcNow, $"Could not bind Modbus TCP Server endpoint {BindEndpoint}.");
            throw;
        }

        _listener = listener;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _acceptLoop = AcceptLoopAsync(_cts.Token);
        SetState(CommunicationDriverOperationalState.Healthy, CommunicationDriverReadinessState.Ready);
        Status = new DriverStatus(DriverId, Name, DriverState.Running, DateTimeOffset.UtcNow);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_cts is null && _listener is null) return;
        Status = new DriverStatus(DriverId, Name, DriverState.Stopping, DateTimeOffset.UtcNow, UpdatesPublished: _updatesPublished);
        SetState(CommunicationDriverOperationalState.Stopping, CommunicationDriverReadinessState.Stopped);
        if (_cts is not null) await _cts.CancelAsync();
        try { _listener?.Stop(); } catch { }
        if (_acceptLoop is not null)
        {
            try { await _acceptLoop.WaitAsync(cancellationToken); }
            catch (OperationCanceledException) when (_cts?.IsCancellationRequested == true) { }
            catch (SocketException) when (_cts?.IsCancellationRequested == true) { }
        }

        var clients = _clients.Values.ToArray();
        if (clients.Length > 0)
        {
            try { await Task.WhenAll(clients).WaitAsync(cancellationToken); }
            catch (OperationCanceledException) when (_cts?.IsCancellationRequested == true) { }
        }

        _listener = null;
        Status = new DriverStatus(DriverId, Name, DriverState.Stopped, DateTimeOffset.UtcNow, UpdatesPublished: _updatesPublished);
        SetState(CommunicationDriverOperationalState.Stopped, CommunicationDriverReadinessState.Stopped);
    }

    public ValueTask<TagValue?> ReadAsync(Guid tagId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_byTagId.ContainsKey(tagId))
            throw new KeyNotFoundException($"Modbus Server TAG '{tagId}' was not found.");
        _cache.TryGet(tagId, out var value);
        return ValueTask.FromResult(value);
    }

    public async ValueTask WriteAsync(Guid tagId, object? value, CancellationToken cancellationToken = default)
    {
        if (!_byTagId.TryGetValue(tagId, out var point))
            throw new KeyNotFoundException($"Modbus Server TAG '{tagId}' was not found.");
        if (point.Point.Tag.ReadOnly)
            throw new InvalidOperationException($"Modbus Server TAG '{point.Point.Tag.Path}' is read-only to internal writes.");

        var update = await _map.WriteInternalAsync(tagId, value, cancellationToken);
        await PublishAsync(update, cancellationToken);
    }

    public CommunicationDriverReadinessSnapshot GetCommunicationReadiness() =>
        new(
            DriverId,
            ModbusTcpServerDriverDescriptorProvider.DriverTypeId,
            _readinessState,
            DateTimeOffset.UtcNow,
            _readinessState == CommunicationDriverReadinessState.Ready ? $"Listening on {BindEndpoint}." : _lastError,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["bindEndpoint"] = BindEndpoint,
                ["unitId"] = _handler.UnitId.ToString(CultureInfo.InvariantCulture)
            });

    public CommunicationDriverDiagnosticSnapshot GetCommunicationDiagnostics()
    {
        var captured = DateTimeOffset.UtcNow;
        lock (_diagnosticsGate)
        {
            var average = _operationCount == 0 ? (TimeSpan?)null : TimeSpan.FromTicks(_totalOperationDurationTicks / _operationCount);
            return new CommunicationDriverDiagnosticSnapshot(
                DriverId,
                Name,
                ModbusTcpServerDriverDescriptorProvider.DriverTypeId,
                _runtimeInstanceId,
                BindEndpoint,
                _communicationState,
                _stateChangedAt,
                captured,
                _lastSuccessfulCommunicationAt,
                _lastFailedCommunicationAt,
                _lastError,
                null,
                null,
                _operationCount == 0 ? null : TimeSpan.FromTicks(_lastOperationDurationTicks),
                average,
                null,
                _operationCount == 0 ? 0d : _failedOperations / (double)_operationCount,
                _points.Count,
                BuildQualitySummary(),
                new CommunicationDriverCounters(
                    Cycles: 0,
                    Requests: _readRequests + _writeRequests + _protocolExceptions,
                    SuccessfulOperations: _operationCount - _failedOperations,
                    FailedOperations: _failedOperations,
                    ConsecutiveFailures: 0,
                    Timeouts: 0,
                    Connections: _connections,
                    Disconnections: _disconnections,
                    Reconnects: 0,
                    ReadOperations: _readRequests,
                    WriteOperations: _writeRequests,
                    UpdatesPublished: _updatesPublished),
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["bindEndpoint"] = BindEndpoint,
                    ["listening"] = (_listener is not null).ToString(),
                    ["connectedClients"] = Volatile.Read(ref _connectedClients).ToString(CultureInfo.InvariantCulture),
                    ["maxClients"] = _maxClients.ToString(CultureInfo.InvariantCulture),
                    ["readRequests"] = _readRequests.ToString(CultureInfo.InvariantCulture),
                    ["writeRequests"] = _writeRequests.ToString(CultureInfo.InvariantCulture),
                    ["rejectedWrites"] = _rejectedWrites.ToString(CultureInfo.InvariantCulture),
                    ["outOfRangeRequests"] = _outOfRangeRequests.ToString(CultureInfo.InvariantCulture),
                    ["protocolExceptions"] = _protocolExceptions.ToString(CultureInfo.InvariantCulture),
                    ["registerCount"] = _map.RegisterCount.ToString(CultureInfo.InvariantCulture),
                    ["tagCount"] = _map.TagCount.ToString(CultureInfo.InvariantCulture)
                });
        }
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        var listener = _listener ?? throw new InvalidOperationException("Listener is not initialized.");
        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
            catch (SocketException) when (cancellationToken.IsCancellationRequested) { break; }

            if (!await _clientSlots.WaitAsync(0, cancellationToken))
            {
                client.Dispose();
                continue;
            }

            var id = Interlocked.Increment(ref _clientSequence);
            var task = HandleClientAsync(client, cancellationToken);
            _clients[id] = task;
            _ = task.ContinueWith(
                completed =>
                {
                    _clients.TryRemove(id, out var _);
                    _clientSlots.Release();
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken serverCancellation)
    {
        using (client)
        {
            client.NoDelay = true;
            Interlocked.Increment(ref _connectedClients);
            lock (_diagnosticsGate) _connections++;
            try
            {
                var stream = client.GetStream();
                if (!_externalEffectAuthority())
                    return;

                while (!serverCancellation.IsCancellationRequested)
                {
                    if (!_externalEffectAuthority())
                        break;

                    using var requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(serverCancellation);
                    requestTimeout.CancelAfter(_clientIdleTimeout);

                    var header = new byte[7];
                    var gotHeader = await TryReadExactlyAsync(stream, header, requestTimeout.Token);
                    if (!gotHeader) break;

                    var transaction = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(0, 2));
                    var protocol = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(2, 2));
                    var length = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(4, 2));
                    var unit = header[6];
                    if (protocol != 0 || length is < 2 or > 254)
                    {
                        RecordFailure("Invalid Modbus TCP MBAP header.");
                        break;
                    }

                    var pdu = new byte[length - 1];
                    await stream.ReadExactlyAsync(pdu, requestTimeout.Token);
                    if (!_externalEffectAuthority())
                        break;

                    var started = Stopwatch.GetTimestamp();
                    var result = await _handler.HandleAsync(unit, pdu, requestTimeout.Token);
                    RecordProtocolResult(result, Stopwatch.GetElapsedTime(started));
                    if (!_externalEffectAuthority())
                        break;

                    var response = new byte[7 + result.ResponsePdu.Length];
                    BinaryPrimitives.WriteUInt16BigEndian(response.AsSpan(0, 2), transaction);
                    BinaryPrimitives.WriteUInt16BigEndian(response.AsSpan(2, 2), 0);
                    BinaryPrimitives.WriteUInt16BigEndian(response.AsSpan(4, 2), checked((ushort)(result.ResponsePdu.Length + 1)));
                    response[6] = unit;
                    result.ResponsePdu.CopyTo(response, 7);
                    await stream.WriteAsync(response, requestTimeout.Token);
                }
            }
            catch (OperationCanceledException) when (serverCancellation.IsCancellationRequested) { }
            catch (OperationCanceledException) { }
            catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
            {
                RecordFailure(ex.Message);
            }
            finally
            {
                Interlocked.Decrement(ref _connectedClients);
                lock (_diagnosticsGate) _disconnections++;
            }
        }
    }

    private static async Task<bool> TryReadExactlyAsync(NetworkStream stream, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer[offset..], cancellationToken);
            if (read == 0) return false;
            offset += read;
        }
        return true;
    }

    private void RecordProtocolResult(ModbusServerProtocolResult result, TimeSpan duration)
    {
        lock (_diagnosticsGate)
        {
            _operationCount++;
            _lastOperationDurationTicks = duration.Ticks;
            _totalOperationDurationTicks += duration.Ticks;
            if (result.Kind == ModbusServerOperationKind.Read) _readRequests++;
            else if (result.Kind == ModbusServerOperationKind.Write) _writeRequests++;
            else
            {
                _failedOperations++;
                _protocolExceptions++;
                _lastFailedCommunicationAt = DateTimeOffset.UtcNow;
                if (result.Failure == ModbusServerRequestFailure.ReadOnly) _rejectedWrites++;
                if (result.Failure == ModbusServerRequestFailure.IllegalAddress) _outOfRangeRequests++;
            }

            if (result.Kind != ModbusServerOperationKind.Rejected)
                _lastSuccessfulCommunicationAt = DateTimeOffset.UtcNow;
        }
    }

    private void RecordFailure(string message)
    {
        lock (_diagnosticsGate)
        {
            _failedOperations++;
            _lastFailedCommunicationAt = DateTimeOffset.UtcNow;
            _lastError = Sanitize(message);
        }
    }

    private async ValueTask PublishExternalUpdatesAsync(
        IReadOnlyCollection<ModbusServerTagUpdate> updates,
        CancellationToken cancellationToken)
    {
        foreach (var update in updates)
            await PublishAsync(update, cancellationToken);
    }

    private async ValueTask PublishAsync(ModbusServerTagUpdate update, CancellationToken cancellationToken)
    {
        var sample = new TagValue(update.Tag.Id, update.EngineeringValue, DateTimeOffset.UtcNow, TagQuality.Good, DriverId);
        await _cache.UpdateAsync(update.Tag, sample, cancellationToken);
        Interlocked.Increment(ref _updatesPublished);
    }

    private CommunicationTagQualitySummary BuildQualitySummary()
    {
        var good = 0;
        var badCommunication = 0;
        var uncertain = 0;
        var bad = 0;
        var badConfiguration = 0;
        var badDevice = 0;
        var stale = 0;
        var disabled = 0;
        var noSample = 0;
        foreach (var point in _points)
        {
            if (!_cache.TryGet(point.Point.Tag.Id, out var sample) || sample is null) { noSample++; continue; }
            switch (sample.Quality)
            {
                case TagQuality.Good: good++; break;
                case TagQuality.BadCommunication: badCommunication++; break;
                case TagQuality.Uncertain: uncertain++; break;
                case TagQuality.Bad: bad++; break;
                case TagQuality.BadConfiguration: badConfiguration++; break;
                case TagQuality.BadDevice: badDevice++; break;
                case TagQuality.Stale: stale++; break;
                case TagQuality.Disabled: disabled++; break;
                default: bad++; break;
            }
        }
        return new CommunicationTagQualitySummary(good, badCommunication, uncertain, bad, badConfiguration, badDevice, stale, disabled, noSample);
    }

    private void SetState(CommunicationDriverOperationalState state, CommunicationDriverReadinessState readiness)
    {
        lock (_diagnosticsGate)
        {
            _communicationState = state;
            _readinessState = readiness;
            _stateChangedAt = DateTimeOffset.UtcNow;
        }
    }

    private static bool TryParseBindAddress(string raw, out IPAddress address)
    {
        var value = raw?.Trim();
        if (string.IsNullOrWhiteSpace(value) || value == "*" || value.Equals("any", StringComparison.OrdinalIgnoreCase))
        {
            address = IPAddress.Any;
            return true;
        }
        return IPAddress.TryParse(value, out address!);
    }

    public static string NormalizeBindAddress(string raw)
    {
        if (!TryParseBindAddress(raw, out var address))
            throw new ArgumentException($"Bind address '{raw}' must be a local IP literal or wildcard.");
        return address.ToString();
    }

    public static bool EndpointsConflict(string leftAddress, int leftPort, string rightAddress, int rightPort)
    {
        if (leftPort != rightPort) return false;
        if (!TryParseBindAddress(leftAddress, out var left) || !TryParseBindAddress(rightAddress, out var right))
            return true;
        return left.Equals(right) ||
               left.Equals(IPAddress.Any) || right.Equals(IPAddress.Any) ||
               left.Equals(IPAddress.IPv6Any) || right.Equals(IPAddress.IPv6Any);
    }

    private static string FormatAddress(IPAddress address) =>
        address.AddressFamily == AddressFamily.InterNetworkV6 ? $"[{address}]" : address.ToString();

    private static string Sanitize(string message)
    {
        var clean = message.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return clean.Length <= 512 ? clean : clean[..512];
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _cts?.Dispose();
        _clientSlots.Dispose();
    }
}
