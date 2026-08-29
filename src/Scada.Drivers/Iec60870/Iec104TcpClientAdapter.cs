using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Scada.Drivers.Iec60870;

/// <summary>
/// Narrow EliteSCADA-owned IEC-104 TCP/APCI client implementation.
/// It deliberately depends only on the neutral IEC-104 contracts in this assembly.
/// </summary>
public sealed class Iec104TcpClientAdapter : IIec104ClientAdapter
{
    private const int ReceiveQueueCapacity = 1024;
    private static readonly TimeSpan SupervisorResolution = TimeSpan.FromMilliseconds(100);

    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly SemaphoreSlim _controlGate = new(1, 1);
    private readonly SemaphoreSlim _sendGate = new(1, 1);
    private readonly object _sequenceGate = new();
    private readonly Queue<DateTime> _sentFrameTimes = new();

    private TcpClient? _client;
    private NetworkStream? _stream;
    private CancellationTokenSource? _sessionCts;
    private Task? _receiveLoop;
    private Task? _supervisorLoop;
    private Channel<Iec104AsduEnvelope>? _incoming;
    private SemaphoreSlim? _sendWindowSlots;
    private Iec104SessionOptions? _options;
    private Iec104SequenceState _sequence = new();
    private DateTime? _firstPendingReceiveAtUtc;
    private long _lastActivityUtcTicks;
    private TaskCompletionSource<bool>? _startConfirmation;
    private TaskCompletionSource<bool>? _stopConfirmation;
    private TaskCompletionSource<bool>? _testConfirmation;
    private int _connected;
    private int _dataTransferStarted;
    private int _readStarted;
    private int _failureSignaled;
    private bool _disposed;

    public bool IsConnected => Volatile.Read(ref _connected) == 1;

    public async Task ConnectAsync(
        string host,
        int port,
        Iec104SessionOptions options,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(host)) throw new ArgumentException("IEC-104 host is required.", nameof(host));
        if (port is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(port));
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            if (_client is not null || IsConnected)
                throw new InvalidOperationException("IEC-104 TCP adapter is already connected or has not been disconnected cleanly.");

            var client = new TcpClient
            {
                NoDelay = true
            };

            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            connectCts.CancelAfter(options.T0);

            try
            {
                await client.ConnectAsync(host.Trim(), port, connectCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                client.Dispose();
                throw new TimeoutException($"IEC-104 TCP connection to {host.Trim()}:{port} exceeded T0 ({options.T0}).");
            }
            catch
            {
                client.Dispose();
                throw;
            }

            _options = options;
            _client = client;
            _stream = client.GetStream();
            _sessionCts = new CancellationTokenSource();
            _incoming = Channel.CreateBounded<Iec104AsduEnvelope>(new BoundedChannelOptions(ReceiveQueueCapacity)
            {
                SingleReader = true,
                SingleWriter = true,
                FullMode = BoundedChannelFullMode.Wait
            });
            _sendWindowSlots = new SemaphoreSlim(options.K, options.K);

            lock (_sequenceGate)
            {
                _sequence = new Iec104SequenceState();
                _sentFrameTimes.Clear();
                _firstPendingReceiveAtUtc = null;
            }

            Volatile.Write(ref _connected, 1);
            Volatile.Write(ref _dataTransferStarted, 0);
            Volatile.Write(ref _readStarted, 0);
            Volatile.Write(ref _failureSignaled, 0);
            TouchActivity();

            _receiveLoop = ReceiveLoopAsync(_sessionCts.Token);
            _supervisorLoop = SupervisorLoopAsync(_sessionCts.Token);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async Task StartDataTransferAsync(CancellationToken cancellationToken = default)
    {
        await _controlGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureConnected();
            if (Volatile.Read(ref _dataTransferStarted) == 1)
                return;

            var confirmation = NewConfirmation();
            _startConfirmation = confirmation;
            try
            {
                await WriteFrameAsync(
                    Iec104ApciFrame.U(Iec104UFunction.StartDataTransferActivation),
                    cancellationToken).ConfigureAwait(false);

                await WaitForConfirmationAsync(confirmation.Task, "STARTDT con", cancellationToken).ConfigureAwait(false);
                Volatile.Write(ref _dataTransferStarted, 1);
            }
            finally
            {
                if (ReferenceEquals(_startConfirmation, confirmation))
                    _startConfirmation = null;
            }
        }
        finally
        {
            _controlGate.Release();
        }
    }

    public async Task StopDataTransferAsync(CancellationToken cancellationToken = default)
    {
        await _controlGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!IsConnected || Volatile.Read(ref _dataTransferStarted) == 0)
                return;

            await SendSupervisoryAcknowledgementIfPendingAsync(cancellationToken).ConfigureAwait(false);

            var confirmation = NewConfirmation();
            _stopConfirmation = confirmation;
            try
            {
                await WriteFrameAsync(
                    Iec104ApciFrame.U(Iec104UFunction.StopDataTransferActivation),
                    cancellationToken).ConfigureAwait(false);

                await WaitForConfirmationAsync(confirmation.Task, "STOPDT con", cancellationToken).ConfigureAwait(false);
                Volatile.Write(ref _dataTransferStarted, 0);
            }
            finally
            {
                if (ReferenceEquals(_stopConfirmation, confirmation))
                    _stopConfirmation = null;
            }
        }
        finally
        {
            _controlGate.Release();
        }
    }

    public async ValueTask SendAsync(
        Iec104AsduEnvelope asdu,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(asdu);
        EnsureDataTransferStarted();

        var options = GetOptions();
        var window = _sendWindowSlots ?? throw new InvalidOperationException("IEC-104 send window is not initialized.");
        var sessionToken = _sessionCts?.Token ?? throw new InvalidOperationException("IEC-104 session cancellation source is not initialized.");
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, sessionToken);

        await window.WaitAsync(linkedCts.Token).ConfigureAwait(false);
        var reservedSequence = false;

        try
        {
            await _sendGate.WaitAsync(linkedCts.Token).ConfigureAwait(false);
            try
            {
                EnsureDataTransferStarted();
                var encodedAsdu = Iec104AsduCodec.Serialize(asdu);
                Iec104ApciFrame frame;
                ushort receiveAcknowledgement;

                lock (_sequenceGate)
                {
                    var sendSequence = _sequence.ReserveSendSequence(options.K);
                    reservedSequence = true;
                    receiveAcknowledgement = _sequence.ReceiveAcknowledgementSequence;
                    _sentFrameTimes.Enqueue(DateTime.UtcNow);
                    frame = Iec104ApciFrame.I(sendSequence, receiveAcknowledgement, encodedAsdu);
                }

                try
                {
                    await WriteFrameCoreAsync(frame, linkedCts.Token).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
                {
                    SignalSessionFailure(ex);
                    throw;
                }

                lock (_sequenceGate)
                {
                    _sequence.MarkReceiveAcknowledged(receiveAcknowledgement);
                    ResetPendingReceiveTimerAfterAcknowledgement();
                }
            }
            finally
            {
                _sendGate.Release();
            }
        }
        catch
        {
            if (!reservedSequence)
                window.Release();
            throw;
        }
    }

    public async IAsyncEnumerable<Iec104AsduEnvelope> ReadAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _readStarted, 1) != 0)
            throw new InvalidOperationException("IEC-104 adapter supports one active ASDU reader per TCP session.");

        var channel = _incoming ?? throw new InvalidOperationException("IEC-104 adapter is not connected.");
        await foreach (var asdu in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            yield return asdu;
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var sessionCts = _sessionCts;
            var client = _client;
            var incoming = _incoming;
            var receiveLoop = _receiveLoop;
            var supervisorLoop = _supervisorLoop;
            var window = _sendWindowSlots;

            Volatile.Write(ref _connected, 0);
            Volatile.Write(ref _dataTransferStarted, 0);
            sessionCts?.Cancel();
            client?.Dispose();
            FailControlWaiters(new IOException("IEC-104 TCP session disconnected."));
            incoming?.Writer.TryComplete();

            var loops = new[] { receiveLoop, supervisorLoop }.Where(static task => task is not null).Cast<Task>().ToArray();
            if (loops.Length > 0)
            {
                try
                {
                    await Task.WhenAll(loops).WaitAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // Transport has already been closed. Do not resurrect it to await background teardown.
                }
                catch
                {
                    // A receive/supervision failure is already surfaced through the session/channel.
                }
            }

            _stream = null;
            _client = null;
            _receiveLoop = null;
            _supervisorLoop = null;
            _incoming = null;
            _sendWindowSlots = null;
            _sessionCts = null;
            _options = null;

            window?.Dispose();
            sessionCts?.Dispose();

            lock (_sequenceGate)
            {
                _sequence.Reset();
                _sentFrameTimes.Clear();
                _firstPendingReceiveAtUtc = null;
            }
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        try
        {
            await DisconnectAsync().ConfigureAwait(false);
        }
        finally
        {
            _disposed = true;
            _lifecycleGate.Dispose();
            _controlGate.Dispose();
            _sendGate.Dispose();
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        Exception? failure = null;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var frame = await ReadFrameAsync(cancellationToken).ConfigureAwait(false);
                TouchActivity();

                switch (frame.Format)
                {
                    case Iec104ApciFrameFormat.I:
                        await HandleInformationFrameAsync(frame, cancellationToken).ConfigureAwait(false);
                        break;
                    case Iec104ApciFrameFormat.S:
                        ApplyPeerAcknowledgement(frame.ReceiveSequence);
                        break;
                    case Iec104ApciFrameFormat.U:
                        await HandleUnnumberedFrameAsync(frame, cancellationToken).ConfigureAwait(false);
                        break;
                    default:
                        throw new Iec104ProtocolException($"Unsupported IEC-104 APCI frame format {frame.Format}.");
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal disconnect path.
        }
        catch (Exception ex)
        {
            failure = ex;
            SignalSessionFailure(ex);
        }
        finally
        {
            _incoming?.Writer.TryComplete(failure);
        }
    }

    private async Task SupervisorLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(SupervisorResolution, cancellationToken).ConfigureAwait(false);
                if (!IsConnected)
                    continue;

                var options = GetOptions();
                var now = DateTime.UtcNow;
                DateTime? oldestSentAt;
                DateTime? firstPendingReceiveAt;

                lock (_sequenceGate)
                {
                    oldestSentAt = _sentFrameTimes.Count > 0 ? _sentFrameTimes.Peek() : null;
                    firstPendingReceiveAt = _firstPendingReceiveAtUtc;
                }

                if (oldestSentAt.HasValue && now - oldestSentAt.Value >= options.T1)
                    throw new TimeoutException($"IEC-104 peer did not acknowledge an I-format frame within T1 ({options.T1}).");

                if (firstPendingReceiveAt.HasValue && now - firstPendingReceiveAt.Value >= options.T2)
                    await SendSupervisoryAcknowledgementIfPendingAsync(cancellationToken).ConfigureAwait(false);

                if (Volatile.Read(ref _dataTransferStarted) == 1)
                {
                    var lastActivity = new DateTime(Interlocked.Read(ref _lastActivityUtcTicks), DateTimeKind.Utc);
                    if (now - lastActivity >= options.T3)
                        await SendTestFrameAndAwaitConfirmationAsync(cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal disconnect path.
        }
        catch (Exception ex)
        {
            SignalSessionFailure(ex);
        }
    }

    private async Task HandleInformationFrameAsync(Iec104ApciFrame frame, CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _dataTransferStarted) == 0)
            throw new Iec104ProtocolException("IEC-104 I-format frame arrived while data transfer is stopped.");

        int acknowledgedSendFrames;
        bool sendImmediateAcknowledgement;
        var now = DateTime.UtcNow;

        lock (_sequenceGate)
        {
            var outstandingBefore = _sequence.UnacknowledgedSendCount;
            var pendingBefore = _sequence.PendingReceiveAcknowledgementCount;
            _sequence.AcceptReceivedIFrame(frame.SendSequence, frame.ReceiveSequence);
            acknowledgedSendFrames = outstandingBefore - _sequence.UnacknowledgedSendCount;
            RemoveAcknowledgedSendTimestamps(acknowledgedSendFrames);

            if (pendingBefore == 0 && _sequence.PendingReceiveAcknowledgementCount > 0)
                _firstPendingReceiveAtUtc = now;

            sendImmediateAcknowledgement = _sequence.ShouldSendSupervisoryAcknowledgement(GetOptions().W);
        }

        ReleaseSendWindowSlots(acknowledgedSendFrames);

        if (sendImmediateAcknowledgement)
            await SendSupervisoryAcknowledgementIfPendingAsync(cancellationToken).ConfigureAwait(false);

        var asdu = Iec104AsduCodec.Parse(frame.Asdu.Span);
        var incoming = _incoming ?? throw new InvalidOperationException("IEC-104 receive queue is not initialized.");
        if (!incoming.Writer.TryWrite(asdu))
            throw new Iec104ProtocolException($"IEC-104 bounded receive queue exceeded {ReceiveQueueCapacity} ASDUs; session is closed rather than silently dropping process data.");
    }

    private async Task HandleUnnumberedFrameAsync(Iec104ApciFrame frame, CancellationToken cancellationToken)
    {
        var function = frame.UFunction ?? throw new Iec104ProtocolException("IEC-104 U-format frame is missing its function.");
        switch (function)
        {
            case Iec104UFunction.StartDataTransferActivation:
                await WriteFrameAsync(Iec104ApciFrame.U(Iec104UFunction.StartDataTransferConfirmation), cancellationToken).ConfigureAwait(false);
                Volatile.Write(ref _dataTransferStarted, 1);
                break;
            case Iec104UFunction.StartDataTransferConfirmation:
                Volatile.Write(ref _dataTransferStarted, 1);
                _startConfirmation?.TrySetResult(true);
                break;
            case Iec104UFunction.StopDataTransferActivation:
                await SendSupervisoryAcknowledgementIfPendingAsync(cancellationToken).ConfigureAwait(false);
                await WriteFrameAsync(Iec104ApciFrame.U(Iec104UFunction.StopDataTransferConfirmation), cancellationToken).ConfigureAwait(false);
                Volatile.Write(ref _dataTransferStarted, 0);
                break;
            case Iec104UFunction.StopDataTransferConfirmation:
                Volatile.Write(ref _dataTransferStarted, 0);
                _stopConfirmation?.TrySetResult(true);
                break;
            case Iec104UFunction.TestFrameActivation:
                await WriteFrameAsync(Iec104ApciFrame.U(Iec104UFunction.TestFrameConfirmation), cancellationToken).ConfigureAwait(false);
                break;
            case Iec104UFunction.TestFrameConfirmation:
                _testConfirmation?.TrySetResult(true);
                break;
            default:
                throw new Iec104ProtocolException($"Unsupported IEC-104 U-format function {function}.");
        }
    }

    private void ApplyPeerAcknowledgement(ushort receiveSequence)
    {
        int acknowledgedSendFrames;
        lock (_sequenceGate)
        {
            var outstandingBefore = _sequence.UnacknowledgedSendCount;
            _sequence.AcceptPeerAcknowledgement(receiveSequence);
            acknowledgedSendFrames = outstandingBefore - _sequence.UnacknowledgedSendCount;
            RemoveAcknowledgedSendTimestamps(acknowledgedSendFrames);
        }

        ReleaseSendWindowSlots(acknowledgedSendFrames);
    }

    private async Task SendSupervisoryAcknowledgementIfPendingAsync(CancellationToken cancellationToken)
    {
        ushort receiveSequence;
        lock (_sequenceGate)
        {
            if (_sequence.PendingReceiveAcknowledgementCount == 0)
                return;
            receiveSequence = _sequence.ReceiveAcknowledgementSequence;
        }

        await WriteFrameAsync(Iec104ApciFrame.S(receiveSequence), cancellationToken).ConfigureAwait(false);

        lock (_sequenceGate)
        {
            _sequence.MarkReceiveAcknowledged(receiveSequence);
            ResetPendingReceiveTimerAfterAcknowledgement();
        }
    }

    private async Task SendTestFrameAndAwaitConfirmationAsync(CancellationToken cancellationToken)
    {
        await _controlGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!IsConnected || Volatile.Read(ref _dataTransferStarted) == 0)
                return;

            var confirmation = NewConfirmation();
            _testConfirmation = confirmation;
            try
            {
                await WriteFrameAsync(Iec104ApciFrame.U(Iec104UFunction.TestFrameActivation), cancellationToken).ConfigureAwait(false);
                await WaitForConfirmationAsync(confirmation.Task, "TESTFR con", cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                if (ReferenceEquals(_testConfirmation, confirmation))
                    _testConfirmation = null;
            }
        }
        finally
        {
            _controlGate.Release();
        }
    }

    private async Task<Iec104ApciFrame> ReadFrameAsync(CancellationToken cancellationToken)
    {
        var stream = _stream ?? throw new IOException("IEC-104 TCP stream is not available.");
        var prefix = new byte[2];
        await ReadExactlyAsync(stream, prefix, cancellationToken).ConfigureAwait(false);

        if (prefix[0] != Iec104ApciCodec.StartByte)
            throw new Iec104ProtocolException($"IEC-104 APDU start byte must be 0x{Iec104ApciCodec.StartByte:X2}; received 0x{prefix[0]:X2}.");

        var apduLength = prefix[1];
        if (apduLength is < Iec104ApciCodec.ControlFieldLength or > Iec104ApciCodec.MaximumApduLength)
            throw new Iec104ProtocolException($"IEC-104 APDU length {apduLength} is outside {Iec104ApciCodec.ControlFieldLength}..{Iec104ApciCodec.MaximumApduLength}.");

        var frameBytes = new byte[2 + apduLength];
        prefix.CopyTo(frameBytes, 0);
        await ReadExactlyAsync(stream, frameBytes.AsMemory(2, apduLength), cancellationToken).ConfigureAwait(false);
        return Iec104ApciCodec.Parse(frameBytes);
    }

    private async Task WriteFrameAsync(Iec104ApciFrame frame, CancellationToken cancellationToken)
    {
        await _sendGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await WriteFrameCoreAsync(frame, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _sendGate.Release();
        }
    }

    private async Task WriteFrameCoreAsync(Iec104ApciFrame frame, CancellationToken cancellationToken)
    {
        EnsureConnected();
        var stream = _stream ?? throw new IOException("IEC-104 TCP stream is not available.");
        var bytes = Iec104ApciCodec.Serialize(frame);
        await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        TouchActivity();
    }

    private async Task WaitForConfirmationAsync(Task confirmation, string confirmationName, CancellationToken cancellationToken)
    {
        var options = GetOptions();
        try
        {
            await confirmation.WaitAsync(options.T1, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            throw new TimeoutException($"IEC-104 {confirmationName} was not received within T1 ({options.T1}).");
        }
    }

    private void SignalSessionFailure(Exception failure)
    {
        if (Interlocked.Exchange(ref _failureSignaled, 1) != 0)
            return;

        Volatile.Write(ref _connected, 0);
        Volatile.Write(ref _dataTransferStarted, 0);
        _incoming?.Writer.TryComplete(failure);
        FailControlWaiters(failure);

        try
        {
            _sessionCts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        try
        {
            _client?.Dispose();
        }
        catch
        {
        }
    }

    private void FailControlWaiters(Exception failure)
    {
        _startConfirmation?.TrySetException(failure);
        _stopConfirmation?.TrySetException(failure);
        _testConfirmation?.TrySetException(failure);
    }

    private void RemoveAcknowledgedSendTimestamps(int count)
    {
        for (var index = 0; index < count; index++)
        {
            if (_sentFrameTimes.Count == 0)
                throw new Iec104ProtocolException("IEC-104 acknowledgement bookkeeping lost an outstanding I-format timestamp.");
            _sentFrameTimes.Dequeue();
        }
    }

    private void ReleaseSendWindowSlots(int count)
    {
        if (count <= 0)
            return;

        var window = _sendWindowSlots ?? throw new InvalidOperationException("IEC-104 send window is not initialized.");
        window.Release(count);
    }

    private void ResetPendingReceiveTimerAfterAcknowledgement()
    {
        _firstPendingReceiveAtUtc = _sequence.PendingReceiveAcknowledgementCount == 0
            ? null
            : DateTime.UtcNow;
    }

    private static async Task ReadExactlyAsync(
        NetworkStream stream,
        Memory<byte> destination,
        CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < destination.Length)
        {
            var read = await stream.ReadAsync(destination[offset..], cancellationToken).ConfigureAwait(false);
            if (read == 0)
                throw new EndOfStreamException("IEC-104 peer closed the TCP connection while an APDU was being received.");
            offset += read;
        }
    }

    private void TouchActivity() => Interlocked.Exchange(ref _lastActivityUtcTicks, DateTime.UtcNow.Ticks);

    private Iec104SessionOptions GetOptions() =>
        _options ?? throw new InvalidOperationException("IEC-104 session options are not initialized.");

    private void EnsureConnected()
    {
        ThrowIfDisposed();
        if (!IsConnected || _stream is null)
            throw new InvalidOperationException("IEC-104 TCP adapter is not connected.");
    }

    private void EnsureDataTransferStarted()
    {
        EnsureConnected();
        if (Volatile.Read(ref _dataTransferStarted) == 0)
            throw new InvalidOperationException("IEC-104 data transfer has not been started with STARTDT.");
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(Iec104TcpClientAdapter));
    }

    private static TaskCompletionSource<bool> NewConfirmation() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
