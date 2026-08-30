using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Scada.Drivers.Iec60870;

/// <summary>
/// Narrow EliteSCADA-owned IEC-104 TCP/APCI client implementation.
/// It deliberately depends only on the neutral IEC-104 contracts in this assembly.
/// </summary>
public sealed class Iec104TcpClientAdapter : IIec104ClientAdapter, IIec104TransportDiagnosticsSource
{
    private const int ReceiveQueueCapacity = 1024;
    private const int MaximumDiagnosticErrorLength = 512;
    private static readonly TimeSpan SupervisorResolution = TimeSpan.FromMilliseconds(100);

    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly SemaphoreSlim _controlGate = new(1, 1);
    private readonly SemaphoreSlim _sendGate = new(1, 1);
    private readonly object _sequenceGate = new();
    private readonly object _diagnosticsGate = new();
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
    private long _lastFrameSentUtcTicks;
    private long _lastFrameReceivedUtcTicks;
    private TaskCompletionSource<bool>? _startConfirmation;
    private TaskCompletionSource<bool>? _stopConfirmation;
    private TaskCompletionSource<bool>? _testConfirmation;
    private int _connected;
    private int _dataTransferStarted;
    private int _readStarted;
    private int _failureSignaled;
    private bool _disposed;

    private long _connections;
    private long _disconnections;
    private long _iFramesSent;
    private long _sFramesSent;
    private long _uFramesSent;
    private long _iFramesReceived;
    private long _sFramesReceived;
    private long _uFramesReceived;
    private long _asdusSent;
    private long _asdusReceived;
    private long _startDtActivationsSent;
    private long _startDtConfirmationsReceived;
    private long _stopDtActivationsSent;
    private long _stopDtConfirmationsReceived;
    private long _testFrameActivationsSent;
    private long _testFrameActivationsReceived;
    private long _testFrameConfirmationsSent;
    private long _testFrameConfirmationsReceived;
    private long _t0Timeouts;
    private long _t1Timeouts;
    private long _t2Expirations;
    private long _t3Expirations;
    private long _protocolErrors;
    private long _sessionFailures;
    private string? _lastFailure;

    public bool IsConnected => Volatile.Read(ref _connected) == 1;

    public Iec104TcpAdapterDiagnosticSnapshot GetTransportDiagnostics()
    {
        ushort nextSend;
        ushort oldestUnacknowledgedSend;
        ushort expectedReceive;
        int unacknowledgedSendCount;
        int pendingReceiveAcknowledgementCount;

        lock (_sequenceGate)
        {
            nextSend = _sequence.NextSendSequence;
            oldestUnacknowledgedSend = _sequence.OldestUnacknowledgedSendSequence;
            expectedReceive = _sequence.ExpectedReceiveSequence;
            unacknowledgedSendCount = _sequence.UnacknowledgedSendCount;
            pendingReceiveAcknowledgementCount = _sequence.PendingReceiveAcknowledgementCount;
        }

        string? lastFailure;
        lock (_diagnosticsGate)
            lastFailure = _lastFailure;

        return new Iec104TcpAdapterDiagnosticSnapshot(
            IsConnected,
            Volatile.Read(ref _dataTransferStarted) == 1,
            nextSend,
            oldestUnacknowledgedSend,
            expectedReceive,
            unacknowledgedSendCount,
            pendingReceiveAcknowledgementCount,
            Interlocked.Read(ref _connections),
            Interlocked.Read(ref _disconnections),
            Interlocked.Read(ref _iFramesSent),
            Interlocked.Read(ref _sFramesSent),
            Interlocked.Read(ref _uFramesSent),
            Interlocked.Read(ref _iFramesReceived),
            Interlocked.Read(ref _sFramesReceived),
            Interlocked.Read(ref _uFramesReceived),
            Interlocked.Read(ref _asdusSent),
            Interlocked.Read(ref _asdusReceived),
            Interlocked.Read(ref _startDtActivationsSent),
            Interlocked.Read(ref _startDtConfirmationsReceived),
            Interlocked.Read(ref _stopDtActivationsSent),
            Interlocked.Read(ref _stopDtConfirmationsReceived),
            Interlocked.Read(ref _testFrameActivationsSent),
            Interlocked.Read(ref _testFrameActivationsReceived),
            Interlocked.Read(ref _testFrameConfirmationsSent),
            Interlocked.Read(ref _testFrameConfirmationsReceived),
            Interlocked.Read(ref _t0Timeouts),
            Interlocked.Read(ref _t1Timeouts),
            Interlocked.Read(ref _t2Expirations),
            Interlocked.Read(ref _t3Expirations),
            Interlocked.Read(ref _protocolErrors),
            Interlocked.Read(ref _sessionFailures),
            DateTimeOffset.UtcNow,
            FromUtcTicks(Interlocked.Read(ref _lastActivityUtcTicks)),
            FromUtcTicks(Interlocked.Read(ref _lastFrameSentUtcTicks)),
            FromUtcTicks(Interlocked.Read(ref _lastFrameReceivedUtcTicks)),
            lastFailure);
    }

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
                Interlocked.Increment(ref _t0Timeouts);
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
            Interlocked.Increment(ref _connections);
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
                catch (Exception ex)
                {
                    var ambiguous = new Iec104AmbiguousTransmissionException(
                        $"IEC-104 I-format transmission failed after send sequence N(S) {frame.SendSequence} was reserved; peer delivery is ambiguous.",
                        ex);
                    SignalSessionFailure(ambiguous);
                    throw ambiguous;
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
            var hadSession = client is not null || sessionCts is not null || IsConnected;

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

            if (hadSession)
                Interlocked.Increment(ref _disconnections);
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
                RecordFrameReceived(frame);

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
            if (ex is Iec104ProtocolException)
                Interlocked.Increment(ref _protocolErrors);
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
                {
                    Interlocked.Increment(ref _t1Timeouts);
                    throw new TimeoutException($"IEC-104 peer did not acknowledge an I-format frame within T1 ({options.T1}).");
                }

                if (firstPendingReceiveAt.HasValue && now - firstPendingReceiveAt.Value >= options.T2)
                {
                    Interlocked.Increment(ref _t2Expirations);
                    await SendSupervisoryAcknowledgementIfPendingAsync(cancellationToken).ConfigureAwait(false);
                }

                if (Volatile.Read(ref _dataTransferStarted) == 1)
                {
                    var lastActivity = new DateTime(Interlocked.Read(ref _lastActivityUtcTicks), DateTimeKind.Utc);
                    if (now - lastActivity >= options.T3)
                    {
                        Interlocked.Increment(ref _t3Expirations);
                        await SendTestFrameAndAwaitConfirmationAsync(cancellationToken).ConfigureAwait(false);
                    }
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
        Interlocked.Increment(ref _asdusReceived);
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
        RecordFrameSent(frame);
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
            Interlocked.Increment(ref _t1Timeouts);
            throw new TimeoutException($"IEC-104 {confirmationName} was not received within T1 ({options.T1}).");
        }
    }

    private void SignalSessionFailure(Exception failure)
    {
        if (Interlocked.Exchange(ref _failureSignaled, 1) != 0)
            return;

        Interlocked.Increment(ref _sessionFailures);
        lock (_diagnosticsGate)
            _lastFailure = SanitizeFailure(failure);

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

    private void RecordFrameSent(Iec104ApciFrame frame)
    {
        switch (frame.Format)
        {
            case Iec104ApciFrameFormat.I:
                Interlocked.Increment(ref _iFramesSent);
                Interlocked.Increment(ref _asdusSent);
                break;
            case Iec104ApciFrameFormat.S:
                Interlocked.Increment(ref _sFramesSent);
                break;
            case Iec104ApciFrameFormat.U:
                Interlocked.Increment(ref _uFramesSent);
                RecordUFunctionSent(frame.UFunction);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(frame), frame.Format, "Unsupported IEC-104 APCI frame format.");
        }

        var ticks = DateTime.UtcNow.Ticks;
        Interlocked.Exchange(ref _lastFrameSentUtcTicks, ticks);
        Interlocked.Exchange(ref _lastActivityUtcTicks, ticks);
    }

    private void RecordFrameReceived(Iec104ApciFrame frame)
    {
        switch (frame.Format)
        {
            case Iec104ApciFrameFormat.I:
                Interlocked.Increment(ref _iFramesReceived);
                break;
            case Iec104ApciFrameFormat.S:
                Interlocked.Increment(ref _sFramesReceived);
                break;
            case Iec104ApciFrameFormat.U:
                Interlocked.Increment(ref _uFramesReceived);
                RecordUFunctionReceived(frame.UFunction);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(frame), frame.Format, "Unsupported IEC-104 APCI frame format.");
        }

        var ticks = DateTime.UtcNow.Ticks;
        Interlocked.Exchange(ref _lastFrameReceivedUtcTicks, ticks);
        Interlocked.Exchange(ref _lastActivityUtcTicks, ticks);
    }

    private void RecordUFunctionSent(Iec104UFunction? function)
    {
        switch (function)
        {
            case Iec104UFunction.StartDataTransferActivation:
                Interlocked.Increment(ref _startDtActivationsSent);
                break;
            case Iec104UFunction.StopDataTransferActivation:
                Interlocked.Increment(ref _stopDtActivationsSent);
                break;
            case Iec104UFunction.TestFrameActivation:
                Interlocked.Increment(ref _testFrameActivationsSent);
                break;
            case Iec104UFunction.TestFrameConfirmation:
                Interlocked.Increment(ref _testFrameConfirmationsSent);
                break;
        }
    }

    private void RecordUFunctionReceived(Iec104UFunction? function)
    {
        switch (function)
        {
            case Iec104UFunction.StartDataTransferConfirmation:
                Interlocked.Increment(ref _startDtConfirmationsReceived);
                break;
            case Iec104UFunction.StopDataTransferConfirmation:
                Interlocked.Increment(ref _stopDtConfirmationsReceived);
                break;
            case Iec104UFunction.TestFrameActivation:
                Interlocked.Increment(ref _testFrameActivationsReceived);
                break;
            case Iec104UFunction.TestFrameConfirmation:
                Interlocked.Increment(ref _testFrameConfirmationsReceived);
                break;
        }
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

    private static DateTimeOffset? FromUtcTicks(long ticks) =>
        ticks == 0 ? null : new DateTimeOffset(new DateTime(ticks, DateTimeKind.Utc));

    private static string SanitizeFailure(Exception failure)
    {
        var message = string.IsNullOrWhiteSpace(failure.Message)
            ? failure.GetType().Name
            : failure.Message.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return message.Length <= MaximumDiagnosticErrorLength
            ? message
            : message[..MaximumDiagnosticErrorLength];
    }

    private static TaskCompletionSource<bool> NewConfirmation() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}