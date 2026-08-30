using System.Buffers.Binary;
using System.Net.Sockets;

namespace Scada.Drivers.AllenBradley;

public sealed class LogixEtherNetIpClient : ILogixProtocolClient
{
    private const int EncapsulationHeaderLength = 24;
    private readonly SemaphoreSlim _ioGate = new(1, 1);
    private readonly object _diagnosticsGate = new();
    private TcpClient? _tcpClient;
    private NetworkStream? _stream;
    private AllenBradleyLogixOptions? _options;
    private uint _sessionHandle;
    private ulong _senderContext;
    private bool _hasConnectedBefore;
    private long _requestAttempts;
    private long _successfulRequests;
    private long _failedRequests;
    private long _timeouts;
    private long _connections;
    private long _disconnections;
    private long _reconnects;
    private DateTimeOffset? _lastConnectedAt;
    private DateTimeOffset? _lastDisconnectedAt;
    private string? _lastError;
    private bool _disposed;

    public bool IsConnected => _tcpClient?.Connected == true && _stream is not null && _sessionHandle != 0;

    public async ValueTask ConnectAsync(AllenBradleyLogixOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        ThrowIfDisposed();
        if (options.SecurityMode == LogixSecurityMode.CipSecurityRequired)
            throw new NotSupportedException("CIP Security is required by Engineering, but the first-cut raw EtherNet/IP client does not implement CIP Security. Insecure fallback is intentionally disabled.");

        await _ioGate.WaitAsync(cancellationToken);
        try
        {
            if (IsConnected) return;
            await DisconnectCoreAsync(sendUnregister: false, cancellationToken);
            _options = options;
            var client = new TcpClient { NoDelay = true };
            try
            {
                using var timeout = CreateTimeoutToken(options.EffectiveRequestTimeout, cancellationToken);
                await client.ConnectAsync(options.Host.Trim(), options.Port, timeout.Token);
                _tcpClient = client;
                _stream = client.GetStream();
                var payload = LogixCipCodec.BuildRegisterSessionPayload();
                var response = await SendEncapsulationCoreAsync(LogixCipCodec.RegisterSessionCommand, 0, payload, timeout.Token);
                if (response.Status != 0 || response.SessionHandle == 0)
                    throw new IOException($"EtherNet/IP RegisterSession failed with status 0x{response.Status:X8}.");
                _sessionHandle = response.SessionHandle;
                lock (_diagnosticsGate)
                {
                    _connections++;
                    if (_hasConnectedBefore) _reconnects++;
                    _hasConnectedBefore = true;
                    _lastConnectedAt = DateTimeOffset.UtcNow;
                    _lastError = null;
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                RecordTimeout("EtherNet/IP connection timed out.");
                client.Dispose();
                _tcpClient = null;
                _stream = null;
                throw new TimeoutException("EtherNet/IP connection timed out.");
            }
            catch
            {
                client.Dispose();
                _tcpClient = null;
                _stream = null;
                _sessionHandle = 0;
                throw;
            }
        }
        finally
        {
            _ioGate.Release();
        }
    }

    public async ValueTask DisconnectAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _ioGate.WaitAsync(cancellationToken);
        try
        {
            await DisconnectCoreAsync(sendUnregister: true, cancellationToken);
        }
        finally
        {
            _ioGate.Release();
        }
    }

    public async ValueTask<LogixControllerIdentity> GetIdentityAsync(CancellationToken cancellationToken = default)
    {
        await _ioGate.WaitAsync(cancellationToken);
        try
        {
            var response = await ExecuteCipCoreAsync(LogixCipCodec.BuildIdentityRequest(), cancellationToken);
            return LogixCipCodec.ParseIdentity(response);
        }
        finally
        {
            _ioGate.Release();
        }
    }

    public async ValueTask<IReadOnlyList<LogixReadResult>> ReadManyAsync(
        IReadOnlyList<LogixSymbolReference> references,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(references);
        if (references.Count == 0) return Array.Empty<LogixReadResult>();

        var results = new LogixReadResult?[references.Count];
        var readable = new List<IndexedReference>(references.Count);
        for (var index = 0; index < references.Count; index++)
        {
            var reference = references[index];
            reference.Validate();
            if (!LogixValueCodec.IsFirstCutRuntimeReadable(reference.NativeType))
            {
                results[index] = new LogixReadResult(
                    reference,
                    false,
                    Error: LogixProtocolError.TypeMismatch,
                    Message: $"Logix native type '{reference.NativeType}' is not enabled by the first-cut runtime codec.");
            }
            else
            {
                readable.Add(new IndexedReference(index, reference));
            }
        }

        await _ioGate.WaitAsync(cancellationToken);
        try
        {
            var maxBatchSize = (_options ?? throw new InvalidOperationException("EtherNet/IP client has no active options.")).MaxBatchSize;
            for (var offset = 0; offset < readable.Count; offset += maxBatchSize)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var count = Math.Min(maxBatchSize, readable.Count - offset);
                await ReadBatchCoreAsync(readable.GetRange(offset, count), results, cancellationToken);
            }
        }
        finally
        {
            _ioGate.Release();
        }

        return results.Select(static result => result ?? throw new InvalidDataException("Logix read result was not populated.")).ToArray();
    }

    private async ValueTask ReadBatchCoreAsync(
        IReadOnlyList<IndexedReference> batch,
        LogixReadResult?[] results,
        CancellationToken cancellationToken)
    {
        if (batch.Count == 0) return;
        if (batch.Count == 1)
        {
            await ReadSingleCoreAsync(batch[0], results, cancellationToken);
            return;
        }

        try
        {
            var raw = LogixMultipleServicePacket.BuildReadRequest(batch.Select(static item => item.Reference).ToArray());
            var response = await ExecuteCipCoreAsync(raw, cancellationToken);
            var replies = LogixMultipleServicePacket.ParseResponse(response);
            if (replies.Count != batch.Count)
                throw new InvalidDataException($"Multiple Service Packet returned {replies.Count} replies for {batch.Count} requests.");

            for (var index = 0; index < batch.Count; index++)
            {
                var item = batch[index];
                var reply = replies[index];
                if (!reply.Succeeded)
                {
                    results[item.Index] = new LogixReadResult(
                        item.Reference,
                        false,
                        Error: LogixCipCodec.MapGeneralStatus(reply.GeneralStatus),
                        Message: $"Read Tag '{item.Reference.StableIdentity}' failed inside Multiple Service Packet with CIP status 0x{reply.GeneralStatus:X2}.");
                    continue;
                }

                try
                {
                    var native = LogixCipCodec.ParseReadTagValue(item.Reference, reply);
                    results[item.Index] = new LogixReadResult(item.Reference, true, native);
                }
                catch (LogixCipException ex)
                {
                    results[item.Index] = new LogixReadResult(item.Reference, false, Error: ex.Error, Message: Sanitize(ex.Message));
                }
            }
        }
        catch (ArgumentOutOfRangeException)
        {
            await SplitAndReadCoreAsync(batch, results, cancellationToken);
        }
        catch (LogixCipException ex) when (IsBatchFallbackCandidate(ex))
        {
            await SplitAndReadCoreAsync(batch, results, cancellationToken);
        }
    }

    private async ValueTask SplitAndReadCoreAsync(
        IReadOnlyList<IndexedReference> batch,
        LogixReadResult?[] results,
        CancellationToken cancellationToken)
    {
        if (batch.Count <= 1)
        {
            await ReadSingleCoreAsync(batch[0], results, cancellationToken);
            return;
        }

        var midpoint = batch.Count / 2;
        await ReadBatchCoreAsync(batch.Take(midpoint).ToArray(), results, cancellationToken);
        await ReadBatchCoreAsync(batch.Skip(midpoint).ToArray(), results, cancellationToken);
    }

    private async ValueTask ReadSingleCoreAsync(
        IndexedReference item,
        LogixReadResult?[] results,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await ExecuteCipCoreAsync(LogixCipCodec.BuildReadTagRequest(item.Reference), cancellationToken);
            var native = LogixCipCodec.ParseReadTagValue(item.Reference, response);
            results[item.Index] = new LogixReadResult(item.Reference, true, native);
        }
        catch (LogixCipException ex)
        {
            results[item.Index] = new LogixReadResult(item.Reference, false, Error: ex.Error, Message: Sanitize(ex.Message));
        }
    }

    private static bool IsBatchFallbackCandidate(LogixCipException error) =>
        error.Error is LogixProtocolError.PacketTooLarge or LogixProtocolError.ControllerResourceUnavailable ||
        error.GeneralStatus == 0x08;

    public async ValueTask<LogixSymbolBrowsePage> BrowseControllerSymbolsAsync(uint startInstance = 0, CancellationToken cancellationToken = default)
    {
        await _ioGate.WaitAsync(cancellationToken);
        try
        {
            var response = await ExecuteCipCoreAsync(LogixCipCodec.BuildControllerSymbolBrowseRequest(startInstance), cancellationToken, allowPartialTransfer: true);
            return LogixCipCodec.ParseControllerSymbolBrowseResponse(response);
        }
        finally
        {
            _ioGate.Release();
        }
    }

    public async ValueTask WriteAsync(LogixSymbolReference reference, object? nativeValue, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        reference.Validate();
        if (!LogixValueCodec.IsFirstCutRuntimeWritable(reference.NativeType))
            throw new NotSupportedException($"Direct Logix writes for native type '{reference.NativeType}' are not enabled by the first-cut runtime codec.");
        await _ioGate.WaitAsync(cancellationToken);
        try
        {
            var response = await ExecuteCipCoreAsync(LogixCipCodec.BuildWriteTagRequest(reference, nativeValue!), cancellationToken);
            LogixCipCodec.ThrowIfFailed(response, $"Write Tag '{reference.StableIdentity}'");
        }
        finally
        {
            _ioGate.Release();
        }
    }

    public LogixTransportDiagnosticSnapshot GetDiagnostics()
    {
        lock (_diagnosticsGate)
        {
            return new LogixTransportDiagnosticSnapshot(
                IsConnected,
                _requestAttempts,
                _successfulRequests,
                _failedRequests,
                _timeouts,
                _connections,
                _disconnections,
                _reconnects,
                _lastConnectedAt,
                _lastDisconnectedAt,
                _lastError);
        }
    }

    private async ValueTask<LogixCipResponse> ExecuteCipCoreAsync(byte[] rawCipRequest, CancellationToken cancellationToken, bool allowPartialTransfer = false)
    {
        EnsureConnected();
        if (rawCipRequest.Length == 0)
            throw new ArgumentException("CIP request must contain a service byte.", nameof(rawCipRequest));
        var options = _options ?? throw new InvalidOperationException("EtherNet/IP client has no active options.");
        var routed = options.EffectiveRoute.Count > 0;
        var request = LogixCipCodec.BuildUnconnectedSend(rawCipRequest, options.EffectiveRoute);
        var cpf = LogixCipCodec.BuildSendRrDataPayload(request);
        try
        {
            using var timeout = CreateTimeoutToken(options.EffectiveRequestTimeout, cancellationToken);
            var response = await SendEncapsulationCoreAsync(LogixCipCodec.SendRrDataCommand, _sessionHandle, cpf, timeout.Token);
            if (response.Status != 0)
                throw new IOException($"EtherNet/IP SendRRData failed with status 0x{response.Status:X8}.");
            var cip = LogixCipCodec.ExtractCipFromSendRrData(response.Payload);
            var parsed = LogixCipCodec.ParsePossiblyRoutedResponse(cip, routed, allowPartialTransfer);
            var expectedReplyService = (byte)(rawCipRequest[0] | 0x80);
            if (parsed.Service != expectedReplyService)
                throw new InvalidDataException($"CIP response service 0x{parsed.Service:X2} does not match request service 0x{rawCipRequest[0]:X2}; expected reply 0x{expectedReplyService:X2}.");
            lock (_diagnosticsGate)
            {
                _successfulRequests++;
                _lastError = null;
            }
            return parsed;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            RecordTimeout("EtherNet/IP request timed out.");
            await DropConnectionAfterFailureAsync();
            throw new TimeoutException("EtherNet/IP request timed out.");
        }
        catch (LogixCipException ex)
        {
            RecordFailure(ex);
            throw;
        }
        catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException or InvalidDataException)
        {
            RecordFailure(ex);
            await DropConnectionAfterFailureAsync();
            throw;
        }
    }

    private async ValueTask<EncapsulationResponse> SendEncapsulationCoreAsync(
        ushort command,
        uint sessionHandle,
        byte[] payload,
        CancellationToken cancellationToken)
    {
        EnsureStreamAvailable(command == LogixCipCodec.RegisterSessionCommand);
        if (payload.Length > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(payload));
        var stream = _stream ?? throw new IOException("EtherNet/IP stream is unavailable.");
        var context = unchecked(++_senderContext);
        var header = new byte[EncapsulationHeaderLength];
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(0, 2), command);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(2, 2), checked((ushort)payload.Length));
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(4, 4), sessionHandle);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(8, 4), 0);
        BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(12, 8), context);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(20, 4), 0);

        lock (_diagnosticsGate) _requestAttempts++;
        await stream.WriteAsync(header, cancellationToken);
        if (payload.Length > 0) await stream.WriteAsync(payload, cancellationToken);
        await stream.FlushAsync(cancellationToken);

        var responseHeader = new byte[EncapsulationHeaderLength];
        await stream.ReadExactlyAsync(responseHeader, cancellationToken);
        var responseCommand = BinaryPrimitives.ReadUInt16LittleEndian(responseHeader.AsSpan(0, 2));
        var length = BinaryPrimitives.ReadUInt16LittleEndian(responseHeader.AsSpan(2, 2));
        var responseSession = BinaryPrimitives.ReadUInt32LittleEndian(responseHeader.AsSpan(4, 4));
        var status = BinaryPrimitives.ReadUInt32LittleEndian(responseHeader.AsSpan(8, 4));
        var responseContext = BinaryPrimitives.ReadUInt64LittleEndian(responseHeader.AsSpan(12, 8));
        if (responseCommand != command)
            throw new InvalidDataException($"EtherNet/IP response command 0x{responseCommand:X4} does not match request 0x{command:X4}.");
        if (responseContext != context)
            throw new InvalidDataException("EtherNet/IP sender context does not match the outstanding request.");
        if (command != LogixCipCodec.RegisterSessionCommand && responseSession != sessionHandle)
            throw new InvalidDataException($"EtherNet/IP response session 0x{responseSession:X8} does not match active session 0x{sessionHandle:X8}.");
        var responsePayload = new byte[length];
        if (length > 0) await stream.ReadExactlyAsync(responsePayload, cancellationToken);
        return new EncapsulationResponse(responseSession, status, responsePayload);
    }

    private async ValueTask SendUnregisterCoreAsync(uint sessionHandle, CancellationToken cancellationToken)
    {
        var stream = _stream ?? throw new IOException("EtherNet/IP stream is unavailable.");
        var header = new byte[EncapsulationHeaderLength];
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(0, 2), LogixCipCodec.UnregisterSessionCommand);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(2, 2), 0);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(4, 4), sessionHandle);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(8, 4), 0);
        BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(12, 8), unchecked(++_senderContext));
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(20, 4), 0);
        await stream.WriteAsync(header, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private async ValueTask DisconnectCoreAsync(bool sendUnregister, CancellationToken cancellationToken)
    {
        var wasConnected = IsConnected;
        if (sendUnregister && _stream is not null && _sessionHandle != 0)
        {
            try
            {
                var timeoutDuration = _options?.EffectiveRequestTimeout ?? TimeSpan.FromSeconds(1);
                using var timeout = CreateTimeoutToken(timeoutDuration, cancellationToken);
                await SendUnregisterCoreAsync(_sessionHandle, timeout.Token);
            }
            catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException or ObjectDisposedException)
            {
                RecordFailure(ex);
            }
        }
        _sessionHandle = 0;
        _stream?.Dispose();
        _stream = null;
        _tcpClient?.Dispose();
        _tcpClient = null;
        if (wasConnected)
        {
            lock (_diagnosticsGate)
            {
                _disconnections++;
                _lastDisconnectedAt = DateTimeOffset.UtcNow;
            }
        }
    }

    private ValueTask DropConnectionAfterFailureAsync()
    {
        var wasConnected = IsConnected;
        _sessionHandle = 0;
        _stream?.Dispose();
        _stream = null;
        _tcpClient?.Dispose();
        _tcpClient = null;
        if (wasConnected)
        {
            lock (_diagnosticsGate)
            {
                _disconnections++;
                _lastDisconnectedAt = DateTimeOffset.UtcNow;
            }
        }
        return ValueTask.CompletedTask;
    }

    private void EnsureConnected()
    {
        ThrowIfDisposed();
        if (!IsConnected) throw new IOException("EtherNet/IP session is not connected.");
    }

    private void EnsureStreamAvailable(bool registeringSession)
    {
        ThrowIfDisposed();
        if (_stream is null || _tcpClient?.Connected != true)
            throw new IOException("EtherNet/IP TCP stream is not connected.");
        if (!registeringSession && _sessionHandle == 0)
            throw new IOException("EtherNet/IP session is not registered.");
    }

    private void RecordTimeout(string message)
    {
        lock (_diagnosticsGate)
        {
            _failedRequests++;
            _timeouts++;
            _lastError = message;
        }
    }

    private void RecordFailure(Exception error)
    {
        lock (_diagnosticsGate)
        {
            _failedRequests++;
            _lastError = Sanitize(error.Message);
        }
    }

    private static CancellationTokenSource CreateTimeoutToken(TimeSpan timeout, CancellationToken cancellationToken)
    {
        var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        source.CancelAfter(timeout);
        return source;
    }

    private static string Sanitize(string message)
    {
        var sanitized = message.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return sanitized.Length <= 512 ? sanitized : sanitized[..512];
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        try
        {
            await _ioGate.WaitAsync();
            try { await DisconnectCoreAsync(sendUnregister: true, CancellationToken.None); }
            finally { _ioGate.Release(); }
        }
        finally
        {
            _disposed = true;
            _ioGate.Dispose();
        }
    }

    private sealed record IndexedReference(int Index, LogixSymbolReference Reference);
    private sealed record EncapsulationResponse(uint SessionHandle, uint Status, byte[] Payload);
}