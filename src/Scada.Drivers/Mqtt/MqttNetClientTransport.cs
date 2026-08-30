using System.Buffers;
using System.Security.Cryptography;
using System.Threading.Channels;
using MQTTnet;
using MQTTnet.Formatter;
using MQTTnet.Protocol;

namespace Scada.Drivers.Mqtt;

/// <summary>
/// MQTTnet 5 transport adapter. MQTTnet types stay behind the EliteSCADA-owned
/// IMqttClientTransport contract so protocol-library upgrades do not become
/// canonical TAG/Engineering changes.
/// </summary>
public sealed class MqttNetClientTransport : IMqttClientTransport
{
    private readonly MqttClientFactory _factory;
    private readonly IMqttClient _client;
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly object _callbackStateGate = new();
    private Channel<TransportItem>? _received;
    private CancellationTokenSource? _receiveWriteCts;
    private Func<MqttApplicationMessageReceivedEventArgs, Task>? _applicationMessageReceivedHandler;
    private Func<MqttClientDisconnectedEventArgs, Task>? _disconnectedHandler;
    private int? _bufferCapacity;
    private long _activeGeneration;
    private volatile int _maximumInboundPayloadBytes = 1_048_576;
    private volatile bool _acceptInboundEvents;
    private volatile bool _intentionalDisconnect;
    private volatile bool _disposed;

    public MqttNetClientTransport()
        : this(new MqttClientFactory())
    {
    }

    internal MqttNetClientTransport(MqttClientFactory factory)
        : this(factory, null)
    {
    }

    internal MqttNetClientTransport(MqttClientFactory factory, IMqttClient? client)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _client = client ?? _factory.CreateMqttClient();
    }

    public bool IsConnected => !_disposed && _client.IsConnected;

    public async ValueTask ConnectAsync(
        MqttConnectionSettings settings,
        MqttResolvedCredentials credentials,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(credentials);
        settings.Validate();

        if (credentials.Username is null && !credentials.Password.IsEmpty)
        {
            throw new MqttTransportException(
                "MQTT password material cannot be supplied without a username.",
                isPermanent: true);
        }

        await _lifecycleGate.WaitAsync(cancellationToken);
        try
        {
            ThrowIfDisposed();
            if (_client.IsConnected)
                throw new MqttTransportException("MQTT client is already connected.");

            EnsureReceiveChannel(settings.MaximumBufferedMessages);
            _maximumInboundPayloadBytes = settings.MaximumInboundPayloadBytes;
            _acceptInboundEvents = false;
            var generation = Interlocked.Increment(ref _activeGeneration);
            DrainBufferedItems();
            ResetReceiveWriteCancellation();
            _intentionalDisconnect = false;
            InstallSessionHandlers(generation);

            var sessionEstablished = false;
            byte[]? passwordBuffer = null;
            try
            {
                var builder = new MqttClientOptionsBuilder()
                    .WithTcpServer(settings.Host, settings.Port)
                    .WithClientId(settings.ClientId)
                    .WithKeepAlivePeriod(settings.EffectiveKeepAlive)
                    .WithTimeout(settings.EffectiveConnectTimeout)
                    .WithProtocolVersion(settings.ProtocolMode == MqttProtocolMode.Mqtt5
                        ? MqttProtocolVersion.V500
                        : MqttProtocolVersion.V311);

                if (settings.ProtocolMode == MqttProtocolMode.Mqtt5)
                {
                    // MQTTnet's regular client queues decoded PUBLISH packets before invoking
                    // the application callback. Bound broker-side unacknowledged QoS 1/2 inflight
                    // to no more than the EliteSCADA application queue budget. MQTT 3.1.1 and
                    // QoS 0 have no equivalent protocol flow-control guarantee.
                    var receiveMaximum = (ushort)Math.Min(settings.MaximumBufferedMessages, ushort.MaxValue);
                    builder
                        .WithCleanStart(settings.CleanStart)
                        .WithSessionExpiryInterval(settings.EffectiveSessionExpirySeconds)
                        .WithReceiveMaximum(receiveMaximum);
                }
                else
                {
                    builder.WithCleanSession(settings.CleanSession);
                }

                if (settings.UseTls)
                {
                    // The MQTTnet TLS builder enables TLS by default. We intentionally
                    // leave certificate validation on the platform fail-closed defaults.
                    builder.WithTlsOptions(tls => tls.WithTargetHost(settings.Host));
                }
                else
                {
                    builder.WithTlsOptions(tls => tls.UseTls(false));
                }

                if (credentials.Username is not null)
                {
                    passwordBuffer = credentials.Password.ToArray();
                    builder.WithCredentials(credentials.Username, passwordBuffer);
                }

                var result = await _client.ConnectAsync(builder.Build(), cancellationToken);
                if (result.ResultCode != MqttClientConnectResultCode.Success)
                {
                    throw new MqttTransportException(
                        $"MQTT broker rejected the connection with CONNACK '{result.ResultCode}'.",
                        IsPermanentConnectFailure(result.ResultCode));
                }

                _acceptInboundEvents = true;
                sessionEstablished = true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (MqttTransportException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new MqttTransportException("MQTT connection failed at the transport boundary.", innerException: ex);
            }
            finally
            {
                if (!sessionEstablished)
                {
                    _acceptInboundEvents = false;
                    RemoveSessionHandlers();
                }

                if (passwordBuffer is { Length: > 0 })
                    CryptographicOperations.ZeroMemory(passwordBuffer);
            }
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async ValueTask SubscribeAsync(
        IReadOnlyCollection<MqttSubscription> subscriptions,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(subscriptions);
        if (subscriptions.Count == 0) return;

        foreach (var subscription in subscriptions)
        {
            MqttPoint.ValidateExactTopic(subscription.Topic, nameof(subscriptions));
            _ = ToMqttNetQos(subscription.Qos);
        }

        await _lifecycleGate.WaitAsync(cancellationToken);
        try
        {
            ThrowIfDisposed();
            if (!_client.IsConnected)
                throw new MqttTransportException("MQTT client is not connected.");

            var builder = _factory.CreateSubscribeOptionsBuilder();
            foreach (var subscription in subscriptions)
            {
                var captured = subscription;
                builder.WithTopicFilter(filter => filter
                    .WithTopic(captured.Topic)
                    .WithQualityOfServiceLevel(ToMqttNetQos(captured.Qos)));
            }

            MqttClientSubscribeResult result;
            try
            {
                result = await _client.SubscribeAsync(builder.Build(), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new MqttTransportException("MQTT subscription request failed at the transport boundary.", innerException: ex);
            }

            var rejected = result.Items.FirstOrDefault(item => (int)item.ResultCode >= 0x80);
            if (rejected is not null)
            {
                throw new MqttTransportException(
                    $"MQTT broker rejected subscription '{rejected.TopicFilter.Topic}' with SUBACK '{rejected.ResultCode}'.",
                    isPermanent: IsPermanentSubscribeFailure(rejected.ResultCode));
            }
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async ValueTask<MqttTransportMessage> ReceiveAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        var channel = _received ?? throw new MqttTransportException("MQTT receive buffer is not initialized; connect before receiving.");

        while (true)
        {
            TransportItem item;
            try
            {
                item = await channel.Reader.ReadAsync(cancellationToken);
            }
            catch (ChannelClosedException) when (_disposed)
            {
                ThrowIfDisposed();
                throw;
            }

            var activeGeneration = Interlocked.Read(ref _activeGeneration);
            if (item.Generation != activeGeneration) continue;
            if (item.Error is not null) throw item.Error;
            return item.Message!;
        }
    }

    public async ValueTask PublishAsync(
        MqttPublishRequest request,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        MqttPoint.ValidateExactTopic(request.Topic, nameof(request));
        var qos = ToMqttNetQos(request.Qos);

        await _lifecycleGate.WaitAsync(cancellationToken);
        try
        {
            ThrowIfDisposed();
            if (!_client.IsConnected)
                throw new MqttTransportException("MQTT client is not connected.");

            var message = new MqttApplicationMessageBuilder()
                .WithTopic(request.Topic)
                .WithPayloadSegment(request.Payload)
                .WithQualityOfServiceLevel(qos)
                .WithRetainFlag(request.Retain)
                .Build();

            MqttClientPublishResult result;
            try
            {
                result = await _client.PublishAsync(message, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new MqttTransportException("MQTT publish failed at the transport boundary.", innerException: ex);
            }

            if (!result.IsSuccess)
            {
                throw new MqttTransportException(
                    $"MQTT broker rejected publish on topic '{request.Topic}' with PUBACK '{result.ReasonCode}'.",
                    isPermanent: IsPermanentPublishFailure(result.ReasonCode));
            }
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async ValueTask DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed) return;

        await _lifecycleGate.WaitAsync(cancellationToken);
        try
        {
            if (_disposed) return;
            _intentionalDisconnect = true;
            _acceptInboundEvents = false;
            CancelReceiveWriters();
            RemoveSessionHandlers();
            try
            {
                if (_client.IsConnected)
                {
                    var options = _factory.CreateClientDisconnectOptionsBuilder()
                        .WithReason(MqttClientDisconnectOptionsReason.NormalDisconnection)
                        .Build();
                    // The caller token controls admission to teardown only. Once the lifecycle
                    // gate is acquired, complete the accepted disconnect instead of leaving a
                    // connected MQTTnet client with EliteSCADA callbacks already detached.
                    await _client.DisconnectAsync(options, CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                throw new MqttTransportException("MQTT clean disconnect failed at the transport boundary.", innerException: ex);
            }
            finally
            {
                Interlocked.Increment(ref _activeGeneration);
                _intentionalDisconnect = false;
            }
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        await _lifecycleGate.WaitAsync();
        try
        {
            if (_disposed) return;
            _disposed = true;
            _intentionalDisconnect = true;
            _acceptInboundEvents = false;
            Interlocked.Increment(ref _activeGeneration);
            CancelReceiveWriters();
            RemoveSessionHandlers();

            _received?.Writer.TryComplete();
            _client.Dispose();

            CancellationTokenSource? receiveWriteCts;
            lock (_callbackStateGate)
            {
                receiveWriteCts = _receiveWriteCts;
                _receiveWriteCts = null;
            }
            receiveWriteCts?.Dispose();
        }
        finally
        {
            // Keep the coordination semaphore alive after public disposal. A concurrent caller
            // may already have passed its disposed pre-check and be queued on this gate.
            _lifecycleGate.Release();
        }
    }

    private async Task OnApplicationMessageReceivedAsync(
        MqttApplicationMessageReceivedEventArgs args,
        long generation)
    {
        var message = args.ApplicationMessage;
        var requiresAcknowledgement = message.QualityOfServiceLevel != MqttQualityOfServiceLevel.AtMostOnce;
        if (requiresAcknowledgement)
            args.AutoAcknowledge = false;

        if (!TryCaptureCallbackState(generation, out var channel, out var writeCancellation))
        {
            if (requiresAcknowledgement) args.ProcessingFailed = true;
            return;
        }

        if (message.Payload.Length > _maximumInboundPayloadBytes)
        {
            _acceptInboundEvents = false;
            if (requiresAcknowledgement) args.ProcessingFailed = true;

            var error = new MqttTransportException(
                $"MQTT payload on topic '{Sanitize(message.Topic)}' exceeds the configured maximum of {_maximumInboundPayloadBytes} bytes and was rejected before the EliteSCADA application copy.",
                isPermanent: true);
            try
            {
                await channel.Writer.WriteAsync(
                    TransportItem.FromError(generation, error),
                    writeCancellation);
            }
            catch (OperationCanceledException) when (_disposed || !_acceptInboundEvents || writeCancellation.IsCancellationRequested)
            {
            }
            catch (ChannelClosedException) when (_disposed)
            {
            }
            return;
        }

        var payload = message.Payload.IsEmpty ? Array.Empty<byte>() : message.Payload.ToArray();
        var received = new MqttTransportMessage(
            message.Topic,
            payload,
            message.Retain,
            FromMqttNetQos(message.QualityOfServiceLevel),
            DateTimeOffset.UtcNow);

        try
        {
            await channel.Writer.WriteAsync(
                TransportItem.FromMessage(generation, received),
                writeCancellation);

            if (requiresAcknowledgement)
                await args.AcknowledgeAsync(writeCancellation);
        }
        catch (OperationCanceledException) when (_disposed || !_acceptInboundEvents || writeCancellation.IsCancellationRequested)
        {
            if (requiresAcknowledgement) args.ProcessingFailed = true;
        }
        catch (ChannelClosedException) when (_disposed)
        {
            if (requiresAcknowledgement) args.ProcessingFailed = true;
        }
    }

    private async Task OnDisconnectedAsync(MqttClientDisconnectedEventArgs args, long generation)
    {
        if (generation != Interlocked.Read(ref _activeGeneration) ||
            _disposed ||
            _intentionalDisconnect ||
            !_acceptInboundEvents)
        {
            return;
        }

        _acceptInboundEvents = false;
        if (!TryCaptureCallbackState(
                generation,
                out var channel,
                out var writeCancellation,
                requireAcceptingEvents: false))
        {
            return;
        }

        var reason = string.IsNullOrWhiteSpace(args.ReasonString)
            ? "MQTT broker connection was lost."
            : $"MQTT broker connection was lost: {Sanitize(args.ReasonString)}";

        try
        {
            await channel.Writer.WriteAsync(
                TransportItem.FromError(generation, new MqttTransportException(reason)),
                writeCancellation);
        }
        catch (OperationCanceledException) when (_disposed || _intentionalDisconnect || writeCancellation.IsCancellationRequested)
        {
        }
        catch (ChannelClosedException) when (_disposed)
        {
        }
    }

    private bool TryCaptureCallbackState(
        long generation,
        out Channel<TransportItem> channel,
        out CancellationToken writeCancellation,
        bool requireAcceptingEvents = true)
    {
        lock (_callbackStateGate)
        {
            if (_disposed ||
                generation != Interlocked.Read(ref _activeGeneration) ||
                (requireAcceptingEvents && !_acceptInboundEvents) ||
                _received is null ||
                _receiveWriteCts is null)
            {
                channel = null!;
                writeCancellation = default;
                return false;
            }

            channel = _received;
            writeCancellation = _receiveWriteCts.Token;
            return true;
        }
    }

    private void InstallSessionHandlers(long generation)
    {
        RemoveSessionHandlers();

        _applicationMessageReceivedHandler = args => OnApplicationMessageReceivedAsync(args, generation);
        _disconnectedHandler = args => OnDisconnectedAsync(args, generation);
        _client.ApplicationMessageReceivedAsync += _applicationMessageReceivedHandler;
        _client.DisconnectedAsync += _disconnectedHandler;
    }

    private void RemoveSessionHandlers()
    {
        if (_applicationMessageReceivedHandler is not null)
        {
            _client.ApplicationMessageReceivedAsync -= _applicationMessageReceivedHandler;
            _applicationMessageReceivedHandler = null;
        }

        if (_disconnectedHandler is not null)
        {
            _client.DisconnectedAsync -= _disconnectedHandler;
            _disconnectedHandler = null;
        }
    }

    private void EnsureReceiveChannel(int capacity)
    {
        if (_received is not null)
        {
            if (_bufferCapacity != capacity)
            {
                throw new MqttTransportException(
                    $"MQTT transport buffer capacity is already fixed at {_bufferCapacity}; requested {capacity}.",
                    isPermanent: true);
            }
            return;
        }

        _bufferCapacity = capacity;
        _received = Channel.CreateBounded<TransportItem>(new BoundedChannelOptions(capacity)
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false,
            FullMode = BoundedChannelFullMode.Wait
        });
    }

    private void ResetReceiveWriteCancellation()
    {
        CancellationTokenSource? previous;
        lock (_callbackStateGate)
        {
            previous = _receiveWriteCts;
            _receiveWriteCts = new CancellationTokenSource();
        }

        previous?.Cancel();
        previous?.Dispose();
    }

    private void CancelReceiveWriters()
    {
        CancellationTokenSource? source;
        lock (_callbackStateGate) source = _receiveWriteCts;
        source?.Cancel();
    }

    private void DrainBufferedItems()
    {
        if (_received is null) return;
        while (_received.Reader.TryRead(out _))
        {
        }
    }

    private static MqttQualityOfServiceLevel ToMqttNetQos(MqttQosLevel qos) => qos switch
    {
        MqttQosLevel.AtMostOnce => MqttQualityOfServiceLevel.AtMostOnce,
        MqttQosLevel.AtLeastOnce => MqttQualityOfServiceLevel.AtLeastOnce,
        MqttQosLevel.ExactlyOnce => MqttQualityOfServiceLevel.ExactlyOnce,
        _ => throw new ArgumentOutOfRangeException(nameof(qos), qos, "Unsupported MQTT QoS level.")
    };

    private static MqttQosLevel FromMqttNetQos(MqttQualityOfServiceLevel qos) => qos switch
    {
        MqttQualityOfServiceLevel.AtMostOnce => MqttQosLevel.AtMostOnce,
        MqttQualityOfServiceLevel.AtLeastOnce => MqttQosLevel.AtLeastOnce,
        MqttQualityOfServiceLevel.ExactlyOnce => MqttQosLevel.ExactlyOnce,
        _ => throw new MqttTransportException($"MQTTnet returned unsupported QoS value '{qos}'.")
    };

    private static bool IsPermanentConnectFailure(MqttClientConnectResultCode resultCode) => resultCode is
        MqttClientConnectResultCode.UnsupportedProtocolVersion or
        MqttClientConnectResultCode.ClientIdentifierNotValid or
        MqttClientConnectResultCode.BadUserNameOrPassword or
        MqttClientConnectResultCode.NotAuthorized or
        MqttClientConnectResultCode.Banned or
        MqttClientConnectResultCode.BadAuthenticationMethod;

    private static bool IsPermanentSubscribeFailure(MqttClientSubscribeResultCode resultCode) => resultCode is
        MqttClientSubscribeResultCode.TopicFilterInvalid or
        MqttClientSubscribeResultCode.NotAuthorized or
        MqttClientSubscribeResultCode.WildcardSubscriptionsNotSupported or
        MqttClientSubscribeResultCode.SubscriptionIdentifiersNotSupported;

    private static bool IsPermanentPublishFailure(MqttClientPublishReasonCode reasonCode) => reasonCode is
        MqttClientPublishReasonCode.NotAuthorized or
        MqttClientPublishReasonCode.TopicNameInvalid or
        MqttClientPublishReasonCode.PayloadFormatInvalid;

    private static string Sanitize(string text)
    {
        var sanitized = text.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return sanitized.Length <= 256 ? sanitized : sanitized[..256];
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private sealed record TransportItem(
        long Generation,
        MqttTransportMessage? Message,
        MqttTransportException? Error)
    {
        public static TransportItem FromMessage(long generation, MqttTransportMessage message) =>
            new(generation, message, null);

        public static TransportItem FromError(long generation, MqttTransportException error) =>
            new(generation, null, error);
    }
}
