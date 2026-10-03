using System.IO.Ports;

namespace Scada.Drivers.Serial;

public enum HostSerialParity
{
    None,
    Odd,
    Even,
    Mark,
    Space
}

public enum HostSerialStopBits
{
    One,
    OnePointFive,
    Two
}

public sealed record HostSerialLineSettings(
    string PortName,
    int BaudRate = 9600,
    int DataBits = 8,
    HostSerialParity Parity = HostSerialParity.None,
    HostSerialStopBits StopBits = HostSerialStopBits.One)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PortName))
            throw new ArgumentException("Server serial port is required.", nameof(PortName));
        if (!string.Equals(PortName, PortName.Trim(), StringComparison.Ordinal))
            throw new ArgumentException("Server serial port must not contain leading or trailing whitespace.", nameof(PortName));
        if (PortName.IndexOfAny(['\r', '\n', '\0']) >= 0)
            throw new ArgumentException("Server serial port contains invalid control characters.", nameof(PortName));
        if (BaudRate is < 300 or > 4_000_000)
            throw new ArgumentOutOfRangeException(nameof(BaudRate), "Serial baud rate must be from 300 to 4000000.");
        if (DataBits is < 5 or > 8)
            throw new ArgumentOutOfRangeException(nameof(DataBits), "Serial data bits must be from 5 to 8.");
        if (!Enum.IsDefined(Parity)) throw new ArgumentOutOfRangeException(nameof(Parity));
        if (!Enum.IsDefined(StopBits)) throw new ArgumentOutOfRangeException(nameof(StopBits));
    }

    public string PhysicalPortKey =>
        OperatingSystem.IsWindows() ? PortName.ToUpperInvariant() : PortName;
}

public sealed record HostSerialPortInfo(string DeviceName);

public interface IHostSerialConnection : IAsyncDisposable
{
    HostSerialLineSettings Settings { get; }
    bool IsOpen { get; }
    ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default);
    ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default);
    ValueTask FlushAsync(CancellationToken cancellationToken = default);
    void DiscardInput();
}

public interface IHostSerialPortProvider
{
    IReadOnlyCollection<HostSerialPortInfo> ListVisiblePorts();
    ValueTask<IHostSerialConnection> OpenAsync(
        HostSerialLineSettings settings,
        CancellationToken cancellationToken = default);
}

public sealed class SystemHostSerialPortProvider : IHostSerialPortProvider
{
    public IReadOnlyCollection<HostSerialPortInfo> ListVisiblePorts() =>
        SerialPort.GetPortNames()
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)
            .OrderBy(name => name, OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)
            .Select(name => new HostSerialPortInfo(name))
            .ToArray();

    public ValueTask<IHostSerialConnection> OpenAsync(
        HostSerialLineSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();
        cancellationToken.ThrowIfCancellationRequested();

        var port = new SerialPort(
            settings.PortName,
            settings.BaudRate,
            Map(settings.Parity),
            settings.DataBits,
            Map(settings.StopBits))
        {
            Handshake = Handshake.None,
            ReadTimeout = SerialPort.InfiniteTimeout,
            WriteTimeout = SerialPort.InfiniteTimeout
        };

        try
        {
            port.Open();
            return ValueTask.FromResult<IHostSerialConnection>(new SystemHostSerialConnection(settings, port));
        }
        catch
        {
            port.Dispose();
            throw;
        }
    }

    private static Parity Map(HostSerialParity value) => value switch
    {
        HostSerialParity.None => Parity.None,
        HostSerialParity.Odd => Parity.Odd,
        HostSerialParity.Even => Parity.Even,
        HostSerialParity.Mark => Parity.Mark,
        HostSerialParity.Space => Parity.Space,
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    private static StopBits Map(HostSerialStopBits value) => value switch
    {
        HostSerialStopBits.One => StopBits.One,
        HostSerialStopBits.OnePointFive => StopBits.OnePointFive,
        HostSerialStopBits.Two => StopBits.Two,
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    private sealed class SystemHostSerialConnection : IHostSerialConnection
    {
        private readonly SerialPort _port;

        public SystemHostSerialConnection(HostSerialLineSettings settings, SerialPort port)
        {
            Settings = settings;
            _port = port;
        }

        public HostSerialLineSettings Settings { get; }
        public bool IsOpen => _port.IsOpen;

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            _port.BaseStream.ReadAsync(buffer, cancellationToken);

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
            _port.BaseStream.WriteAsync(buffer, cancellationToken);

        public async ValueTask FlushAsync(CancellationToken cancellationToken = default) =>
            await _port.BaseStream.FlushAsync(cancellationToken);

        public void DiscardInput()
        {
            if (_port.IsOpen) _port.DiscardInBuffer();
        }

        public ValueTask DisposeAsync()
        {
            try
            {
                if (_port.IsOpen) _port.Close();
            }
            finally
            {
                _port.Dispose();
            }
            return ValueTask.CompletedTask;
        }
    }
}

public enum HostSerialBusUsage
{
    SharedMaster,
    ExclusiveServer
}

public sealed class HostSerialBusCoordinator : IAsyncDisposable
{
    private readonly IHostSerialPortProvider _provider;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, BusState> _buses = new(
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

    public HostSerialBusCoordinator(IHostSerialPortProvider provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    public ValueTask<HostSerialBusLease> AcquireMasterAsync(
        string ownerId,
        HostSerialLineSettings settings,
        IReadOnlyCollection<byte> unitIds,
        CancellationToken cancellationToken = default) =>
        AcquireAsync(ownerId, settings, HostSerialBusUsage.SharedMaster, unitIds, cancellationToken);

    public ValueTask<HostSerialBusLease> AcquireServerAsync(
        string ownerId,
        HostSerialLineSettings settings,
        CancellationToken cancellationToken = default) =>
        AcquireAsync(ownerId, settings, HostSerialBusUsage.ExclusiveServer, Array.Empty<byte>(), cancellationToken);

    public ValueTask<HostSerialBusLease> AcquireEngineeringMasterAsync(
        string ownerId,
        HostSerialLineSettings settings,
        CancellationToken cancellationToken = default) =>
        AcquireAsync(ownerId, settings, HostSerialBusUsage.SharedMaster, Array.Empty<byte>(), cancellationToken);

    private async ValueTask<HostSerialBusLease> AcquireAsync(
        string ownerId,
        HostSerialLineSettings settings,
        HostSerialBusUsage usage,
        IReadOnlyCollection<byte> unitIds,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(ownerId)) throw new ArgumentException("Serial bus owner ID is required.", nameof(ownerId));
        settings.Validate();
        ArgumentNullException.ThrowIfNull(unitIds);
        var normalizedOwner = ownerId.Trim();
        var normalizedUnits = unitIds.Distinct().OrderBy(x => x).ToArray();

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var key = settings.PhysicalPortKey;
            if (_buses.TryGetValue(key, out var existing))
            {
                if (existing.Usage != HostSerialBusUsage.SharedMaster || usage != HostSerialBusUsage.SharedMaster)
                    throw new InvalidOperationException($"Serial port '{settings.PortName}' is already owned exclusively by '{existing.Owners.Keys.First()}'.");
                if (!LineCompatible(existing.Settings, settings))
                    throw new InvalidOperationException($"Serial port '{settings.PortName}' is already active with incompatible line settings.");
                if (existing.Owners.ContainsKey(normalizedOwner))
                    throw new InvalidOperationException($"Serial bus owner '{normalizedOwner}' already holds port '{settings.PortName}'.");

                var conflict = existing.Owners
                    .SelectMany(pair => pair.Value.Select(unit => (pair.Key, Unit: unit)))
                    .FirstOrDefault(entry => normalizedUnits.Contains(entry.Unit));
                if (conflict != default)
                    throw new InvalidOperationException(
                        $"Serial port '{settings.PortName}' Unit ID {conflict.Unit} is already owned by Data Source '{conflict.Key}'.");

                existing.Owners.Add(normalizedOwner, normalizedUnits);
                return new HostSerialBusLease(this, existing, normalizedOwner);
            }

            var connection = await _provider.OpenAsync(settings, cancellationToken);
            var state = new BusState(settings, usage, connection);
            state.Owners.Add(normalizedOwner, normalizedUnits);
            _buses.Add(key, state);
            return new HostSerialBusLease(this, state, normalizedOwner);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async ValueTask ReleaseAsync(BusState state, string ownerId)
    {
        await _gate.WaitAsync();
        try
        {
            if (!state.Owners.Remove(ownerId)) return;
            if (state.Owners.Count != 0) return;
            _buses.Remove(state.Settings.PhysicalPortKey);
            await state.Connection.DisposeAsync();
            state.TransactionGate.Dispose();
        }
        finally
        {
            _gate.Release();
        }
    }

    private static bool LineCompatible(HostSerialLineSettings left, HostSerialLineSettings right) =>
        left.BaudRate == right.BaudRate &&
        left.DataBits == right.DataBits &&
        left.Parity == right.Parity &&
        left.StopBits == right.StopBits;

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync();
        try
        {
            foreach (var state in _buses.Values)
            {
                await state.Connection.DisposeAsync();
                state.TransactionGate.Dispose();
            }
            _buses.Clear();
        }
        finally
        {
            _gate.Release();
            _gate.Dispose();
        }
    }

    internal sealed class BusState
    {
        public BusState(HostSerialLineSettings settings, HostSerialBusUsage usage, IHostSerialConnection connection)
        {
            Settings = settings;
            Usage = usage;
            Connection = connection;
        }

        public HostSerialLineSettings Settings { get; }
        public HostSerialBusUsage Usage { get; }
        public IHostSerialConnection Connection { get; }
        public Dictionary<string, IReadOnlyCollection<byte>> Owners { get; } = new(StringComparer.OrdinalIgnoreCase);
        public SemaphoreSlim TransactionGate { get; } = new(1, 1);
        public DateTimeOffset? LastTransactionCompletedUtc { get; set; }
    }

    public sealed class HostSerialBusLease : IAsyncDisposable
    {
        private readonly HostSerialBusCoordinator _coordinator;
        private readonly BusState _state;
        private readonly string _ownerId;
        private int _disposed;

        internal HostSerialBusLease(HostSerialBusCoordinator coordinator, BusState state, string ownerId)
        {
            _coordinator = coordinator;
            _state = state;
            _ownerId = ownerId;
        }

        public string BusIdentity => _state.Settings.PhysicalPortKey;
        public HostSerialLineSettings Settings => _state.Settings;
        public HostSerialBusUsage Usage => _state.Usage;

        public async ValueTask<T> ExecuteSerializedAsync<T>(
            Func<IHostSerialConnection, CancellationToken, ValueTask<T>> operation,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(operation);
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

            await _state.TransactionGate.WaitAsync(cancellationToken);
            try
            {
                if (_state.LastTransactionCompletedUtc is { } completed)
                {
                    var minimum = MinimumInterFrameDelay(_state.Settings.BaudRate);
                    var remaining = minimum - (DateTimeOffset.UtcNow - completed);
                    if (remaining > TimeSpan.Zero)
                        await Task.Delay(remaining, cancellationToken);
                }

                var result = await operation(_state.Connection, cancellationToken);
                _state.LastTransactionCompletedUtc = DateTimeOffset.UtcNow;
                return result;
            }
            finally
            {
                _state.TransactionGate.Release();
            }
        }

        public async ValueTask ExecuteSerializedAsync(
            Func<IHostSerialConnection, CancellationToken, ValueTask> operation,
            CancellationToken cancellationToken = default)
        {
            await ExecuteSerializedAsync<object?>(
                async (connection, token) =>
                {
                    await operation(connection, token);
                    return null;
                },
                cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            await _coordinator.ReleaseAsync(_state, _ownerId);
        }

        private static TimeSpan MinimumInterFrameDelay(int baudRate) =>
            TimeSpan.FromSeconds(38.5d / baudRate);
    }
}
