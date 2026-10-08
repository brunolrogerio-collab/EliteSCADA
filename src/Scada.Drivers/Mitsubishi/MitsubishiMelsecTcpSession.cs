using System.Buffers.Binary;
using System.Diagnostics;
using System.Net.Sockets;

namespace Scada.Drivers.Mitsubishi;

/// <summary>Persistent TCP session with a single in-flight 3E transaction.</summary>
public sealed class MitsubishiMelsecTcpSession : IAsyncDisposable
{
    private readonly MitsubishiMelsecConnectionOptions _options;
    private readonly SemaphoreSlim _requestGate = new(1, 1);
    private readonly object _stateGate = new();
    private TcpClient? _client;
    private NetworkStream? _stream;
    private long _requestCount;
    private long _connectionCount;
    private long _timeoutCount;
    private long _disconnectCount;
    private long _reconnectCount;
    private long _lastRoundTripTicks;
    private ushort? _lastEndCode;
    private string? _lastFailureKind;
    private bool _everConnected;

    public MitsubishiMelsecTcpSession(MitsubishiMelsecConnectionOptions options) =>
        _options = options ?? throw new ArgumentNullException(nameof(options));

    public string Endpoint => _options.SanitizedEndpoint;
    public long RequestCount => Interlocked.Read(ref _requestCount);
    public long ConnectionCount => Interlocked.Read(ref _connectionCount);
    public long TimeoutCount => Interlocked.Read(ref _timeoutCount);
    public long DisconnectCount => Interlocked.Read(ref _disconnectCount);
    public long ReconnectCount => Interlocked.Read(ref _reconnectCount);
    public TimeSpan? LastRoundTripTime
    {
        get
        {
            var ticks = Interlocked.Read(ref _lastRoundTripTicks);
            return ticks > 0 ? TimeSpan.FromTicks(ticks) : null;
        }
    }
    public ushort? LastEndCode { get { lock (_stateGate) return _lastEndCode; } }
    public string? LastFailureKind { get { lock (_stateGate) return _lastFailureKind; } }
    public bool IsConnected => _client?.Connected == true && _stream is not null;

    public async Task<MitsubishiMelsecDecodedResponse> ExecuteAsync(
        ushort command,
        ushort subcommand,
        ReadOnlyMemory<byte> commandData,
        bool writeCommand,
        int? expectedDataLength,
        CancellationToken cancellationToken)
    {
        await _requestGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var timeoutCts = new CancellationTokenSource(_options.RequestTimeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            var token = linkedCts.Token;
            await EnsureConnectedAsync(token).ConfigureAwait(false);
            var request = MitsubishiMelsecProtocolCodec.BuildRequest(
                _options.Route, _options.MonitoringTimerUnits, command, subcommand, commandData.Span);
            var started = Stopwatch.GetTimestamp();
            var dispatchMayHaveOccurred = false;
            Interlocked.Increment(ref _requestCount);
            try
            {
                dispatchMayHaveOccurred = true;
                await _stream!.WriteAsync(request, token).ConfigureAwait(false);
                var responseFrame = await ReadFrameAsync(_stream, token).ConfigureAwait(false);
                var decoded = MitsubishiMelsecProtocolCodec.DecodeResponse(responseFrame, _options.Route,
                    expectedDataLength.HasValue ? checked((ushort)expectedDataLength.Value) : null);
                Interlocked.Exchange(ref _lastRoundTripTicks, Stopwatch.GetElapsedTime(started).Ticks);
                lock (_stateGate)
                {
                    _lastEndCode = decoded.EndCode;
                    _lastFailureKind = null;
                }
                return decoded;
            }
            catch (MitsubishiMelsecProtocolException ex) when (ex.FailureKind == "plc_end_code")
            {
                lock (_stateGate) { _lastEndCode = ex.EndCode; _lastFailureKind = ex.FailureKind; }
                throw;
            }
            catch (OperationCanceledException ex)
            {
                var timedOut = timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested;
                if (timedOut) Interlocked.Increment(ref _timeoutCount);
                lock (_stateGate) _lastFailureKind = timedOut ? "request_timeout" : "cancelled";
                await DropConnectionAsync().ConfigureAwait(false);
                if (writeCommand && dispatchMayHaveOccurred)
                    throw new MitsubishiMelsecProtocolException(
                        timedOut ? "SLMP write timed out after dispatch may have begun." : "SLMP write was interrupted after dispatch may have begun.",
                        timedOut ? "request_timeout" : "transport_cancelled", dispatchMayHaveOccurred: true, innerException: ex);
                if (timedOut)
                    throw new MitsubishiMelsecProtocolException("SLMP request timed out.", "request_timeout", dispatchMayHaveOccurred: dispatchMayHaveOccurred, innerException: ex);
                throw;
            }
            catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
            {
                lock (_stateGate) _lastFailureKind = ex is SocketException ? "socket_error" : "transport_error";
                await DropConnectionAsync().ConfigureAwait(false);
                throw new MitsubishiMelsecProtocolException(
                    writeCommand && dispatchMayHaveOccurred
                        ? "SLMP write transport failed after dispatch may have begun."
                        : "SLMP transport failed.",
                    ex is SocketException ? "socket_error" : "transport_error",
                    dispatchMayHaveOccurred: writeCommand && dispatchMayHaveOccurred,
                    innerException: ex);
            }
        }
        finally
        {
            _requestGate.Release();
        }
    }

    public async Task DisconnectAsync()
    {
        await _requestGate.WaitAsync().ConfigureAwait(false);
        try { await DropConnectionAsync().ConfigureAwait(false); }
        finally { _requestGate.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync().ConfigureAwait(false);
        _requestGate.Dispose();
    }

    private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (IsConnected) return;
        var client = new TcpClient { NoDelay = true };
        using var connectTimeout = new CancellationTokenSource(_options.ConnectTimeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, connectTimeout.Token);
        try
        {
            await client.ConnectAsync(_options.Host, _options.Port, linked.Token).ConfigureAwait(false);
            _client = client;
            _stream = client.GetStream();
            if (_everConnected) Interlocked.Increment(ref _reconnectCount);
            _everConnected = true;
            Interlocked.Increment(ref _connectionCount);
        }
        catch (OperationCanceledException ex) when (connectTimeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            client.Dispose();
            Interlocked.Increment(ref _timeoutCount);
            lock (_stateGate) _lastFailureKind = "connect_timeout";
            throw new MitsubishiMelsecProtocolException("MELSEC TCP connection timed out.", "connect_timeout", innerException: ex);
        }
        catch (Exception ex) when (ex is SocketException or OperationCanceledException)
        {
            client.Dispose();
            lock (_stateGate) _lastFailureKind = ex is SocketException ? "connect_error" : "cancelled";
            throw;
        }
    }

    private static async Task<byte[]> ReadFrameAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var header = new byte[MitsubishiMelsecProtocolCodec.HeaderLength];
        await ReadExactlyAsync(stream, header, cancellationToken).ConfigureAwait(false);
        var length = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(7));
        if (length < 2)
            throw new MitsubishiMelsecProtocolException("SLMP response advertises an invalid length.", "invalid_length");
        var frame = new byte[header.Length + length];
        header.CopyTo(frame, 0);
        await ReadExactlyAsync(stream, frame.AsMemory(header.Length, length), cancellationToken).ConfigureAwait(false);
        return frame;
    }

    private static async Task ReadExactlyAsync(Stream stream, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer[offset..], cancellationToken).ConfigureAwait(false);
            if (read == 0) throw new EndOfStreamException("MELSEC closed the TCP stream during an SLMP frame.");
            offset += read;
        }
    }

    private async Task DropConnectionAsync()
    {
        var stream = Interlocked.Exchange(ref _stream, null);
        var client = Interlocked.Exchange(ref _client, null);
        if (stream is not null || client is not null) Interlocked.Increment(ref _disconnectCount);
        try { if (stream is not null) await stream.DisposeAsync().ConfigureAwait(false); }
        catch (IOException) { }
        client?.Dispose();
    }
}
