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
    DateTimeOffset? LastDisconnectedAt,
    S7IsoFailureKind? LastFailureKind);

internal sealed record S7IsoReadCollectionResult(
    IReadOnlyList<S7IsoReadItemResult> Items,
    IReadOnlyDictionary<S7IsoPoint, string> ConfigurationFailures,
    IReadOnlyDictionary<S7IsoPoint, string> CommunicationFailures);

internal sealed class S7IsoTransport : IAsyncDisposable
{
    private const int Rfc1006AndCotpHeaderLength = 7;

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
    private S7IsoFailureKind? _lastFailureKind;
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
        var detailed = await ReadDetailedAsync(points, cancellationToken);
        if (detailed.CommunicationFailures.Count > 0)
        {
            throw new IOException(string.Join(
                " ",
                detailed.CommunicationFailures.Select(failure => failure.Value).Distinct(StringComparer.Ordinal)));
        }
        if (detailed.ConfigurationFailures.Count > 0)
        {
            throw new S7IsoConfigurationException(string.Join(
                " ",
                detailed.ConfigurationFailures.Select(failure => failure.Value)));
        }
        return detailed.Items;
    }

    public async Task<S7IsoReadCollectionResult> ReadDetailedAsync(
        IReadOnlyList<S7IsoPoint> points,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(points);
        if (points.Count == 0)
            return new S7IsoReadCollectionResult(
                Array.Empty<S7IsoReadItemResult>(),
                new Dictionary<S7IsoPoint, string>(),
                new Dictionary<S7IsoPoint, string>());

        await _ioGate.WaitAsync(cancellationToken);
        try
        {
            var communicationFailures = new Dictionary<S7IsoPoint, string>();
            try
            {
                await EnsureConnectedUnsafeAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                foreach (var point in points) communicationFailures[point] = ex.Message;
                return new S7IsoReadCollectionResult(
                    Array.Empty<S7IsoReadItemResult>(),
                    new Dictionary<S7IsoPoint, string>(),
                    communicationFailures);
            }

            var pduSize = _negotiatedPduSize ?? _options.RequestedPduSize;
            var validPoints = new List<S7IsoPoint>(points.Count);
            var configurationFailures = new Dictionary<S7IsoPoint, string>();

            foreach (var point in points)
            {
                try
                {
                    _ = S7IsoBatchPlanner.PlanReads(new[] { point }, pduSize);
                    validPoints.Add(point);
                }
                catch (ArgumentException ex)
                {
                    configurationFailures[point] = ex.Message;
                }
            }

            IReadOnlyList<IReadOnlyList<S7IsoPoint>> batches;
            try
            {
                batches = validPoints.Count == 0
                    ? Array.Empty<IReadOnlyList<S7IsoPoint>>()
                    : S7IsoBatchPlanner.PlanReads(validPoints, pduSize);
            }
            catch (ArgumentException ex)
            {
                throw new S7IsoConfigurationException(ex.Message);
            }

            var results = new List<S7IsoReadItemResult>(validPoints.Count);
            for (var batchIndex = 0; batchIndex < batches.Count; batchIndex++)
            {
                var batch = batches[batchIndex];
                var reference = NextPduReference();
                var request = S7IsoProtocol.BuildReadRequest(reference, batch);
                IncrementRequestAttempts();

                try
                {
                    var response = await ExchangeUnsafeAsync(request, _options.RequestTimeout, cancellationToken);
                    var parsed = S7IsoProtocol.ParseReadResponse(response, reference, batch);
                    foreach (var item in parsed)
                    {
                        if (!item.Succeeded)
                            RecordFailure(S7IsoFailureClassifier.ClassifyReturnCode(item.ReturnCode, writeOperation: false));
                    }
                    results.AddRange(parsed);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    DisconnectUnsafe();
                    throw;
                }
                catch (Exception ex)
                {
                    RecordFailure(S7IsoFailureClassifier.Classify(ex, S7IsoFailurePhase.Read));
                    DisconnectUnsafe();
                    for (var failedBatchIndex = batchIndex; failedBatchIndex < batches.Count; failedBatchIndex++)
                    {
                        foreach (var failedPoint in batches[failedBatchIndex])
                            communicationFailures[failedPoint] = ex.Message;
                    }
                    break;
                }
            }

            return new S7IsoReadCollectionResult(results, configurationFailures, communicationFailures);
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
            var pduSize = _negotiatedPduSize ?? _options.RequestedPduSize;
            var requestPduLength = request.Length - Rfc1006AndCotpHeaderLength;
            if (requestPduLength > pduSize)
            {
                throw new S7IsoConfigurationException(
                    $"S7 write for '{point.Tag.Path}' requires a {requestPduLength}-byte PDU, " +
                    $"but the peer negotiated {pduSize} bytes.");
            }

            IncrementRequestAttempts();
            try
            {
                var response = await ExchangeUnsafeAsync(request, _options.RequestTimeout, cancellationToken);
                S7IsoProtocol.ParseWriteResponse(response, reference);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                DisconnectUnsafe();
                throw;
            }
            catch (S7IsoProtocolException ex) when (ex.ReturnCode.HasValue)
            {
                RecordFailure(S7IsoFailureClassifier.Classify(ex, S7IsoFailurePhase.Write));
                throw;
            }
            catch (Exception ex)
            {
                RecordFailure(S7IsoFailureClassifier.Classify(ex, S7IsoFailurePhase.Write));
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
                _lastDisconnectedAt,
                _lastFailureKind);
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
                    RecordFailure(S7IsoFailureKind.Timeout);
                    throw new TimeoutException(
                        $"Timed out connecting to S7 ISO endpoint {_options.SanitizedEndpoint}.",
                        ex);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    RecordFailure(S7IsoFailureClassifier.Classify(ex, S7IsoFailurePhase.ConnectTransport));
                    throw;
                }
            }

            var stream = client.GetStream();
            try
            {
                var connectionRequest = S7IsoProtocol.BuildConnectionRequest(_options);
                var connectionConfirm = await ExchangeOnStreamAsync(
                    stream,
                    connectionRequest,
                    _options.RequestTimeout,
                    cancellationToken);
                S7IsoProtocol.ValidateConnectionConfirm(connectionConfirm);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                RecordFailure(S7IsoFailureClassifier.Classify(ex, S7IsoFailurePhase.CotpConnect));
                throw;
            }

            ushort negotiatedPdu;
            try
            {
                var setupReference = NextPduReference();
                var setupRequest = S7IsoProtocol.BuildSetupCommunication(setupReference, _options.RequestedPduSize);
                var setupResponse = await ExchangeOnStreamAsync(
                    stream,
                    setupRequest,
                    _options.RequestTimeout,
                    cancellationToken);
                negotiatedPdu = S7IsoProtocol.ParseSetupCommunicationResponse(setupResponse, setupReference);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                RecordFailure(S7IsoFailureClassifier.Classify(ex, S7IsoFailurePhase.SetupCommunication));
                throw;
            }

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
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            client.Dispose();
            throw;
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

    private void RecordFailure(S7IsoFailureKind kind)
    {
        lock (_diagnosticsGate) _lastFailureKind = kind;
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