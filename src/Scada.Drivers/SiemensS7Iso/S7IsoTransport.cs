using System.Buffers.Binary;
using System.Net.Sockets;

namespace Scada.Drivers.SiemensS7Iso;

internal sealed record S7IsoTransportDiagnosticSnapshot(
    string Host,
    int Port,
    ushort SourceTsap,
    ushort DestinationTsap,
    ushort RequestedPduSize,
    ushort? NegotiatedPduSize,
    bool Connected,
    long RequestAttempts,
    long TimeoutCount,
    long ConnectionCount,
    long DisconnectionCount,
    long ReconnectCount,
    DateTimeOffset? LastConnectedAt,
    DateTimeOffset? LastDisconnectedAt);

internal sealed class S7IsoTransport : IAsyncDisposable
{
    private readonly S7IsoConnectionOptions _options;
    private readonly SemaphoreSlim _ioGate = new(1, 1);
    private readonly object _diagnosticsGate = new();
    private TcpClient? _client;
    private NetworkStream? _stream;
    private ushort _pduReference;
    private ushort? _negotiatedPduSize;
    private long _requestAttempts;
    private long _timeoutCount;
    private long _connectionCount;
    private long _disconnectionCount;
    private long _reconnectCount;
    private DateTimeOffset? _lastConnectedAt;
    private DateTimeOffset? _lastDisconnectedAt;
    private bool _disposed;

    public S7IsoTransport(S7IsoConnectionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _ioGate.WaitAsync(cancellationToken);
        try
        {
            await EnsureConnectedUnsafeAsync(cancellationToken);
        }
        finally
        {
            _ioGate.Release();
        }
    }

    public async Task<IReadOnlyList<S7IsoReadItemResult>> ReadAsync(
        IReadOnlyList<S7IsoPoint> points,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(points);
        if (points.Count == 0) return Array.Empty<S7IsoReadItemResult>();

        await _ioGate.WaitAsync(cancellationToken);
        try
        {
            await EnsureConnectedUnsafeAsync(cancellationToken);
            var pduSize = _negotiatedPduSize ?? _options.RequestedPduSize;
            var batches = S7IsoBatchPlanner.PlanReads(points, pduSize);
            var results = new List<S7IsoReadItemResult>(points.Count);

            foreach (var batch in batches)
            {
                var reference = NextPduReference();
                var request = S7IsoProtocol.BuildReadRequest(reference, batch);
                IncrementRequestAttempts();

                try
                {
                    var response = await ExchangeUnsafeAsync(request, _options.RequestTimeout, cancellationToken);
                    results.AddRange(S7IsoProtocol.ParseReadResponse(response, reference, batch));
                }
                catch (S7IsoProtocolException ex) when (ex.ReturnCode.HasValue)
                {
                    throw;
                }
                catch
                {
                    DisconnectUnsafe();
                    throw;
                }
            }

            return results;
        }
        finally
        {
            _ioGate.Release();
        }
    }

    public async Task WriteAsync(
        S7IsoPoint point,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(point);

        await _ioGate.WaitAsync(cancellationToken);
        try
        {
            await EnsureConnectedUnsafeAsync(cancellationToken);
            var reference = NextPduReference();
            var request = S7IsoProtocol.BuildWriteRequest(reference, point, data.Span);
            IncrementRequestAttempts();

            try
            {
                var response = await ExchangeUnsafeAsync(request, _options.RequestTimeout, cancellationToken);
                S7IsoProtocol.ParseWriteResponse(response, reference);
            }
            catch (S7IsoProtocolException ex) when (ex.ReturnCode.HasValue)
            {
                throw;
            }
            catch
            {
                DisconnectUnsafe();
                throw;
            }
        }
        finally
        {
            _ioGate.Release();
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed) return;
        await _ioGate.WaitAsync(cancellationToken);
        try
        {
            DisconnectUnsafe();
        }
        finally
        {
            _ioGate.Release();
        }
    }

    public S7IsoTransportDiagnosticSnapshot GetDiagnostics()
    {
        lock (_diagnosticsGate)
        {
            return new S7IsoTransportDiagnosticSnapshot(
                _options.Host,
                _options.Port,
                _options.EffectiveSourceTsap,
                _options.EffectiveDestinationTsap,
                _options.RequestedPduSize,
                _negotiatedPduSize,
                _stream is not null,
                _requestAttempts,
                _timeoutCount,
                _connectionCount,
                _disconnectionCount,
                _reconnectCount,
                _lastConnectedAt,
                _lastDisconnectedAt);
        }
    }

    private async Task EnsureConnectedUnsafeAsync(CancellationToken cancellationToken)
    {
        if (_stream is not null) return;

        DateTimeOffset? lastDisconnected;
        lock (_diagnosticsGate) lastDisconnected = _lastDisconnectedAt;
        if (lastDisconnected.HasValue && _options.ReconnectDelay > TimeSpan.Zero)
        {
            var remaining = lastDisconnected.Value + _options.ReconnectDelay - DateTimeOffset.UtcNow;
            if (remaining > TimeSpan.Zero)
                await Task.Delay(remaining, cancellationToken);
        }

        var client = new TcpClient { NoDelay = true };
        try
        {
            using (var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                connectCts.CancelAfter(_options.ConnectTimeout);
                try
                {
                    await client.ConnectAsync(_options.Host, _options.Port, connectCts.Token);
                }
                catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
                {
                    IncrementTimeout();
                    throw new TimeoutException(
                        $"Timed out connecting to S7 ISO endpoint {_options.SanitizedEndpoint}.",
                        ex);
                }
            }

            var stream = client.GetStream();
            var connectionRequest = S7IsoProtocol.BuildConnectionRequest(_options);
            var connectionConfirm = await ExchangeOnStreamAsync(
                stream,
                connectionRequest,
                _options.RequestTimeout,
                cancellationToken);
            S7IsoProtocol.ValidateConnectionConfirm(connectionConfirm);

            var setupReference = NextPduReference();
            var setupRequest = S7IsoProtocol.BuildSetupCommunication(setupReference, _options.RequestedPduSize);
            var setupResponse = await ExchangeOnStreamAsync(
                stream,
                setupRequest,
                _options.RequestTimeout,
                cancellationToken);
            var negotiatedPdu = S7IsoProtocol.ParseSetupCommunicationResponse(setupResponse, setupReference);

            _client = client;
            _stream = stream;
            lock (_diagnosticsGate)
            {
                if (_connectionCount > 0) _reconnectCount++;
                _connectionCount++;
                _lastConnectedAt = DateTimeOffset.UtcNow;
                _negotiatedPduSize = negotiatedPdu;
            }
        }
        catch
        {
            client.Dispose();
            lock (_diagnosticsGate)
            {
                _lastDisconnectedAt = DateTimeOffset.UtcNow;
                _negotiatedPduSize = null;
            }
            throw;
        }
    }

    private async Task<byte[]> ExchangeUnsafeAsync(
        byte[] request,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var stream = _stream ?? throw new IOException("S7 ISO transport is not connected.");
        return await ExchangeOnStreamAsync(stream, request, timeout, cancellationToken);
    }

    private async Task<byte[]> ExchangeOnStreamAsync(
        NetworkStream stream,
        byte[] request,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var requestCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        requestCts.CancelAfter(timeout);
        try
        {
            await stream.WriteAsync(request, requestCts.Token);
            await stream.FlushAsync(requestCts.Token);
            return await ReadTpktPacketAsync(stream, requestCts.Token);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            IncrementTimeout();
            throw new TimeoutException(
                $"S7 ISO request to {_options.SanitizedEndpoint} exceeded {timeout.TotalMilliseconds:0.###} ms.",
                ex);
        }
    }

    private static async Task<byte[]> ReadTpktPacketAsync(
        NetworkStream stream,
        CancellationToken cancellationToken)
    {
        var header = new byte[4];
        await ReadExactAsync(stream, header, cancellationToken);
        if (header[0] != 0x03 || header[1] != 0x00)
            throw new S7IsoProtocolException("Invalid RFC1006 TPKT header received from S7 peer.");

        var length = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(2, 2));
        if (length < 4)
            throw new S7IsoProtocolException($"Invalid TPKT length {length}.");

        var packet = new byte[length];
        header.CopyTo(packet, 0);
        if (length > 4)
            await ReadExactAsync(stream, packet.AsMemory(4, length - 4), cancellationToken);
        return packet;
    }

    private static async Task ReadExactAsync(
        NetworkStream stream,
        Memory<byte> destination,
        CancellationToken cancellationToken)
    {
        var total = 0;
        while (total < destination.Length)
        {
            var read = await stream.ReadAsync(destination[total..], cancellationToken);
            if (read == 0) throw new EndOfStreamException("S7 ISO peer closed the connection.");
            total += read;
        }
    }

    private ushort NextPduReference()
    {
        _pduReference++;
        if (_pduReference == 0) _pduReference = 1;
        return _pduReference;
    }

    private void IncrementRequestAttempts()
    {
        lock (_diagnosticsGate) _requestAttempts++;
    }

    private void IncrementTimeout()
    {
        lock (_diagnosticsGate) _timeoutCount++;
    }

    private void DisconnectUnsafe()
    {
        var wasConnected = _stream is not null || _client is not null;
        try { _stream?.Dispose(); } catch { }
        try { _client?.Dispose(); } catch { }
        _stream = null;
        _client = null;

        if (!wasConnected) return;
        lock (_diagnosticsGate)
        {
            _disconnectionCount++;
            _lastDisconnectedAt = DateTimeOffset.UtcNow;
            _negotiatedPduSize = null;
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(S7IsoTransport));
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        await DisconnectAsync();
        _disposed = true;
        _ioGate.Dispose();
    }
}
