using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using Scada.Drivers.Serial;

namespace Scada.Drivers.Panasonic;

public interface IPanasonicMewtocolSession : IAsyncDisposable
{
    bool IsConnected { get; }
    long RequestCount { get; }
    long TimeoutCount { get; }
    long ConnectionCount { get; }
    long DisconnectCount { get; }
    long ReconnectCount { get; }
    TimeSpan? LastRoundTripTime { get; }
    string? LastFailureKind { get; }
    Task ConnectAsync(CancellationToken cancellationToken = default);
    Task<PanasonicMewtocolDecodedResponse> ExecuteAsync(
        ReadOnlyMemory<byte> request,
        string expectedResponseCode,
        int? expectedDataLength,
        bool writeCommand,
        CancellationToken cancellationToken = default);
    Task DisconnectAsync();
}

/// <summary>Persistent, single-flight TCP session for MEWTOCOL-COM.</summary>
public sealed class PanasonicMewtocolTcpSession : IPanasonicMewtocolSession
{
    private readonly PanasonicMewtocolConnectionOptions _options;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private TcpClient? _client;
    private NetworkStream? _stream;
    private bool _everConnected;
    private long _requests;
    private long _timeouts;
    private long _connections;
    private long _disconnects;
    private long _reconnects;
    private long _lastRoundTripTicks;
    private string? _lastFailureKind;

    public PanasonicMewtocolTcpSession(PanasonicMewtocolConnectionOptions options) =>
        _options = options ?? throw new ArgumentNullException(nameof(options));

    public bool IsConnected => _client?.Connected == true && _stream is not null;
    public long RequestCount => Interlocked.Read(ref _requests);
    public long TimeoutCount => Interlocked.Read(ref _timeouts);
    public long ConnectionCount => Interlocked.Read(ref _connections);
    public long DisconnectCount => Interlocked.Read(ref _disconnects);
    public long ReconnectCount => Interlocked.Read(ref _reconnects);
    public TimeSpan? LastRoundTripTime { get { var ticks = Interlocked.Read(ref _lastRoundTripTicks); return ticks > 0 ? TimeSpan.FromTicks(ticks) : null; } }
    public string? LastFailureKind => Volatile.Read(ref _lastFailureKind);

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var connectTimeout = new CancellationTokenSource(_options.ConnectTimeout);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, connectTimeout.Token);
            await EnsureConnectedAsync(linked.Token, connectTimeout, cancellationToken).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    public async Task<PanasonicMewtocolDecodedResponse> ExecuteAsync(
        ReadOnlyMemory<byte> request,
        string expectedResponseCode,
        int? expectedDataLength,
        bool writeCommand,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var timeout = new CancellationTokenSource(_options.RequestTimeout);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
            await EnsureConnectedAsync(linked.Token, null, cancellationToken).ConfigureAwait(false);
            Interlocked.Increment(ref _requests);
            var started = Stopwatch.GetTimestamp();
            var dispatchMayHaveOccurred = false;
            try
            {
                dispatchMayHaveOccurred = true;
                await _stream!.WriteAsync(request, linked.Token).ConfigureAwait(false);
                var responseFrame = await ReadFrameAsync(_stream, _options.Capabilities.MaximumFrameCharacters, linked.Token).ConfigureAwait(false);
                var decoded = PanasonicMewtocolProtocolCodec.DecodeResponse(responseFrame, _options.Station, _options.FrameMode, expectedResponseCode);
                if (expectedDataLength.HasValue && decoded.Data.Length != expectedDataLength.Value)
                    throw new PanasonicMewtocolProtocolException("MEWTOCOL-COM response data length is inconsistent with the requested operation.", "unexpected_data_length");
                Interlocked.Exchange(ref _lastRoundTripTicks, Stopwatch.GetElapsedTime(started).Ticks);
                Volatile.Write(ref _lastFailureKind, null);
                return decoded;
            }
            catch (PanasonicMewtocolProtocolException ex) when (ex.FailureKind == "plc_error")
            {
                Volatile.Write(ref _lastFailureKind, ex.FailureKind);
                throw;
            }
            catch (OperationCanceledException ex)
            {
                var timedOut = timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested;
                if (timedOut) Interlocked.Increment(ref _timeouts);
                Volatile.Write(ref _lastFailureKind, timedOut ? "request_timeout" : "cancelled");
                await DropConnectionAsync().ConfigureAwait(false);
                if (cancellationToken.IsCancellationRequested && (!writeCommand || !dispatchMayHaveOccurred)) throw;
                throw new PanasonicMewtocolProtocolException(
                    timedOut ? "MEWTOCOL-COM request timed out." : "MEWTOCOL-COM request was cancelled.",
                    timedOut ? "request_timeout" : "cancelled", dispatchMayHaveOccurred: writeCommand && dispatchMayHaveOccurred, innerException: ex);
            }
            catch (PanasonicMewtocolProtocolException ex)
            {
                Volatile.Write(ref _lastFailureKind, ex.FailureKind);
                await DropConnectionAsync().ConfigureAwait(false);
                if (writeCommand && ex.DispatchMayHaveOccurred is false)
                    throw new PanasonicMewtocolProtocolException(ex.Message, ex.FailureKind, ex.ErrorCode, true, ex);
                throw;
            }
            catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
            {
                Volatile.Write(ref _lastFailureKind, ex is SocketException ? "socket_error" : "transport_error");
                await DropConnectionAsync().ConfigureAwait(false);
                throw new PanasonicMewtocolProtocolException(
                    "MEWTOCOL-COM TCP transport failed.", ex is SocketException ? "socket_error" : "transport_error",
                    dispatchMayHaveOccurred: writeCommand && dispatchMayHaveOccurred, innerException: ex);
            }
        }
        finally { _gate.Release(); }
    }

    public async Task DisconnectAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try { await DropConnectionAsync().ConfigureAwait(false); }
        finally { _gate.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync().ConfigureAwait(false);
        _gate.Dispose();
    }

    private async Task EnsureConnectedAsync(CancellationToken token, CancellationTokenSource? connectTimeout, CancellationToken callerToken)
    {
        if (IsConnected) return;
        var client = new TcpClient { NoDelay = true };
        try
        {
            using var localTimeout = connectTimeout is null ? new CancellationTokenSource(_options.ConnectTimeout) : null;
            using var linked = connectTimeout is null
                ? CancellationTokenSource.CreateLinkedTokenSource(token, localTimeout!.Token)
                : null;
            var connectToken = linked?.Token ?? token;
            await client.ConnectAsync(_options.Host!, _options.Port!.Value, connectToken).ConfigureAwait(false);
            _client = client;
            _stream = client.GetStream();
            if (_everConnected) Interlocked.Increment(ref _reconnects);
            _everConnected = true;
            Interlocked.Increment(ref _connections);
            Volatile.Write(ref _lastFailureKind, null);
        }
        catch (OperationCanceledException ex)
        {
            client.Dispose();
            if (callerToken.IsCancellationRequested) throw;
            if (!callerToken.IsCancellationRequested) Interlocked.Increment(ref _timeouts);
            Volatile.Write(ref _lastFailureKind, callerToken.IsCancellationRequested ? "cancelled" : "connect_timeout");
            throw new PanasonicMewtocolProtocolException(
                callerToken.IsCancellationRequested ? "MEWTOCOL-COM connection was cancelled." : "MEWTOCOL-COM TCP connection timed out.",
                callerToken.IsCancellationRequested ? "cancelled" : "connect_timeout", innerException: ex);
        }
        catch (SocketException ex)
        {
            client.Dispose();
            Volatile.Write(ref _lastFailureKind, "connect_error");
            throw new PanasonicMewtocolProtocolException("MEWTOCOL-COM TCP connection failed.", "connect_error", innerException: ex);
        }
    }

    private static async Task<byte[]> ReadFrameAsync(NetworkStream stream, int maximumCharacters, CancellationToken token)
    {
        using var buffer = new MemoryStream();
        var one = new byte[1];
        while (buffer.Length < maximumCharacters)
        {
            var read = await stream.ReadAsync(one, token).ConfigureAwait(false);
            if (read == 0) throw new EndOfStreamException("Peer closed TCP during a MEWTOCOL-COM frame.");
            buffer.WriteByte(one[0]);
            if (one[0] == PanasonicMewtocolProtocolCodec.CarriageReturn) return buffer.ToArray();
        }
        throw new PanasonicMewtocolProtocolException("MEWTOCOL-COM response exceeds the selected frame limit.", "frame_too_large");
    }

    private async Task DropConnectionAsync()
    {
        var stream = Interlocked.Exchange(ref _stream, null);
        var client = Interlocked.Exchange(ref _client, null);
        if (stream is not null || client is not null) Interlocked.Increment(ref _disconnects);
        if (stream is not null)
        {
            try { await stream.DisposeAsync().ConfigureAwait(false); }
            catch (IOException) { }
        }
        client?.Dispose();
    }
}

/// <summary>Host Serial transport session. Port ownership and transaction serialization remain with #469.</summary>
public sealed class PanasonicMewtocolSerialSession : IPanasonicMewtocolSession
{
    private readonly PanasonicMewtocolConnectionOptions _options;
    private readonly HostSerialBusCoordinator _coordinator;
    private readonly string _ownerId;
    private readonly bool _engineeringReadOnly;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private HostSerialBusCoordinator.HostSerialBusLease? _lease;
    private long _requests;
    private long _timeouts;
    private long _connections;
    private long _disconnects;
    private long _reconnects;
    private long _lastRoundTripTicks;
    private bool _everConnected;
    private string? _lastFailureKind;

    public PanasonicMewtocolSerialSession(
        PanasonicMewtocolConnectionOptions options,
        HostSerialBusCoordinator coordinator,
        string ownerId,
        bool engineeringReadOnly = false)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        if (string.IsNullOrWhiteSpace(ownerId)) throw new ArgumentException("Data Source owner ID is required for Host Serial.", nameof(ownerId));
        _ownerId = ownerId.Trim();
        _engineeringReadOnly = engineeringReadOnly;
    }

    public bool IsConnected => _lease is not null;
    public long RequestCount => Interlocked.Read(ref _requests);
    public long TimeoutCount => Interlocked.Read(ref _timeouts);
    public long ConnectionCount => Interlocked.Read(ref _connections);
    public long DisconnectCount => Interlocked.Read(ref _disconnects);
    public long ReconnectCount => Interlocked.Read(ref _reconnects);
    public TimeSpan? LastRoundTripTime { get { var ticks = Interlocked.Read(ref _lastRoundTripTicks); return ticks > 0 ? TimeSpan.FromTicks(ticks) : null; } }
    public string? LastFailureKind => Volatile.Read(ref _lastFailureKind);

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { await EnsureLeaseAsync(cancellationToken).ConfigureAwait(false); }
        finally { _gate.Release(); }
    }

    public async Task<PanasonicMewtocolDecodedResponse> ExecuteAsync(
        ReadOnlyMemory<byte> request,
        string expectedResponseCode,
        int? expectedDataLength,
        bool writeCommand,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureLeaseAsync(cancellationToken).ConfigureAwait(false);
            using var timeout = new CancellationTokenSource(_options.RequestTimeout);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
            Interlocked.Increment(ref _requests);
            var started = Stopwatch.GetTimestamp();
            var dispatchMayHaveOccurred = false;
            try
            {
                return await _lease!.ExecuteSerializedAsync(async (connection, token) =>
                {
                    connection.DiscardInput();
                    try
                    {
                        dispatchMayHaveOccurred = true;
                        await connection.WriteAsync(request, token).ConfigureAwait(false);
                        await connection.FlushAsync(token).ConfigureAwait(false);
                        if (_options.TurnaroundDelay > TimeSpan.Zero)
                            await Task.Delay(_options.TurnaroundDelay, token).ConfigureAwait(false);
                        var responseFrame = await ReadFrameAsync(connection, _options.Capabilities.MaximumFrameCharacters, token).ConfigureAwait(false);
                        var decoded = PanasonicMewtocolProtocolCodec.DecodeResponse(responseFrame, _options.Station, _options.FrameMode, expectedResponseCode);
                        if (expectedDataLength.HasValue && decoded.Data.Length != expectedDataLength.Value)
                            throw new PanasonicMewtocolProtocolException("MEWTOCOL-COM response data length is inconsistent with the requested operation.", "unexpected_data_length");
                        Volatile.Write(ref _lastFailureKind, null);
                        return decoded;
                    }
                    catch
                    {
                        connection.DiscardInput();
                        throw;
                    }
                }, linked.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException ex)
            {
                var timedOut = timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested;
                if (timedOut) Interlocked.Increment(ref _timeouts);
                Volatile.Write(ref _lastFailureKind, timedOut ? "request_timeout" : "cancelled");
                await CleanupLeaseAfterFailureAsync(ex, writeCommand && dispatchMayHaveOccurred).ConfigureAwait(false);
                if (cancellationToken.IsCancellationRequested && (!writeCommand || !dispatchMayHaveOccurred)) throw;
                throw new PanasonicMewtocolProtocolException(
                    timedOut ? "MEWTOCOL-COM serial request timed out." : "MEWTOCOL-COM serial request was cancelled.",
                    timedOut ? "request_timeout" : "cancelled", dispatchMayHaveOccurred: writeCommand && dispatchMayHaveOccurred, innerException: ex);
            }
            catch (PanasonicMewtocolProtocolException ex) when (ex.FailureKind == "plc_error")
            {
                Volatile.Write(ref _lastFailureKind, ex.FailureKind);
                throw;
            }
            catch (PanasonicMewtocolProtocolException ex)
            {
                Volatile.Write(ref _lastFailureKind, ex.FailureKind);
                await CleanupLeaseAfterFailureAsync(ex, writeCommand && dispatchMayHaveOccurred).ConfigureAwait(false);
                if (writeCommand && dispatchMayHaveOccurred)
                    throw new PanasonicMewtocolProtocolException(ex.Message, ex.FailureKind, ex.ErrorCode, true, ex);
                throw;
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException)
            {
                Volatile.Write(ref _lastFailureKind, "transport_error");
                await CleanupLeaseAfterFailureAsync(ex, writeCommand && dispatchMayHaveOccurred).ConfigureAwait(false);
                throw new PanasonicMewtocolProtocolException("MEWTOCOL-COM Host Serial transport failed.", "transport_error",
                    dispatchMayHaveOccurred: writeCommand && dispatchMayHaveOccurred, innerException: ex);
            }
            finally
            {
                if (_lastFailureKind is null)
                {
                    Interlocked.Exchange(ref _lastRoundTripTicks, Stopwatch.GetElapsedTime(started).Ticks);
                }
            }
        }
        finally { _gate.Release(); }
    }

    public async Task DisconnectAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try { await DropLeaseAsync().ConfigureAwait(false); }
        finally { _gate.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync().ConfigureAwait(false);
        _gate.Dispose();
    }

    private async Task EnsureLeaseAsync(CancellationToken cancellationToken)
    {
        if (_lease is not null) return;
        var acquired = _engineeringReadOnly
            ? await _coordinator.AcquireEngineeringMasterAsync(_ownerId, _options.SerialSettings!, cancellationToken).ConfigureAwait(false)
            : await _coordinator.AcquireMasterAsync(
                _ownerId, _options.SerialSettings!, new[] { checked((byte)_options.Station) }, cancellationToken).ConfigureAwait(false);
        _lease = acquired;
        if (_everConnected) Interlocked.Increment(ref _reconnects);
        _everConnected = true;
        Interlocked.Increment(ref _connections);
        Volatile.Write(ref _lastFailureKind, null);
    }

    private async Task DropLeaseAsync()
    {
        var lease = Interlocked.Exchange(ref _lease, null);
        if (lease is null) return;
        Interlocked.Increment(ref _disconnects);
        await lease.DisposeAsync().ConfigureAwait(false);
    }

    private async Task CleanupLeaseAfterFailureAsync(Exception operationFailure, bool dispatchMayHaveOccurred)
    {
        try { await DropLeaseAsync().ConfigureAwait(false); }
        catch (Exception cleanupFailure)
        {
            throw new PanasonicMewtocolProtocolException(
                "MEWTOCOL-COM Host Serial operation failed and its Host Serial lease cleanup also failed.",
                "transport_cleanup_error", dispatchMayHaveOccurred: dispatchMayHaveOccurred,
                innerException: new AggregateException(operationFailure, cleanupFailure));
        }
    }

    private static async Task<byte[]> ReadFrameAsync(IHostSerialConnection connection, int maximumCharacters, CancellationToken token)
    {
        using var buffer = new MemoryStream();
        var one = new byte[1];
        while (buffer.Length < maximumCharacters)
        {
            var read = await connection.ReadAsync(one, token).ConfigureAwait(false);
            if (read == 0) throw new EndOfStreamException("Host Serial peer closed during a MEWTOCOL-COM frame.");
            buffer.WriteByte(one[0]);
            if (one[0] == PanasonicMewtocolProtocolCodec.CarriageReturn) return buffer.ToArray();
        }
        throw new PanasonicMewtocolProtocolException("MEWTOCOL-COM response exceeds the selected frame limit.", "frame_too_large");
    }

}
