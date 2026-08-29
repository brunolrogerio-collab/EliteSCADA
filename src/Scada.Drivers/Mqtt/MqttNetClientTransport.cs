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
    private Channel<TransportItem>? _received;
    private CancellationTokenSource? _receiveWriteCts;
    private int? _bufferCapacity;
    private long _activeGeneration;
    private bool _acceptInboundEvents;
    private bool _intentionalDisconnect;
    private bool _disposed;

    public MqttNetClientTransport()
        : this(new MqttClientFactory())
    {
    }

    internal MqttNetClientTransport(MqttClientFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _client = _factory.CreateMqttClient();

        _client.ApplicationMessageReceivedAsync += OnApplicationMessageReceivedAsync;
        _client.DisconnectedAsync += OnDisconnectedAsync;
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
            _acceptInboundEvents = false;
            Interlocked.Increment(ref _activeGeneration);
            DrainBufferedItems();
            ResetReceiveWriteCancellation();
            _intentionalDisconnect = false;

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
                    builder
                        .WithCleanStart(settings.CleanStart)
                        .WithSessionExpiryInterval(settings.EffectiveSessionExpirySeconds);
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
            var item = await channel.Reader.ReadAsync(cancellationToken);
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
            _receiveWriteCts?.Cancel();
            try
            {
                if (_client.IsConnected)
                {
                    var options = _factory.CreateClientDisconnectOptionsBuilder()
                        .WithReason(MqttClientDisconnectOptionsReason.NormalDisconnection)
                        .Build();
                    await _client.DisconnectAsync(options, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
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
            _receiveWriteCts?.Cancel();

            _client.ApplicationMessageReceivedAsync -= OnApplicationMessageReceivedAsync;
            _client.DisconnectedAsync -= OnDisconnectedAsync;
            _received?.Writer.TryComplete();
            _client.Dispose();
            _receiveWriteCts?.Dispose();
            _receiveWriteCts = null;
        }
        finally
        {
            _lifecycleGate.Release();
            _lifecycleGate.Dispose();
        }
    }

    private async Task OnApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs args)
    {
        var channel = _received;
        var writeCancellation = _receiveWriteCts;
        if (_disposed || !_acceptInboundEvents || channel is null || writeCancellation is null)
            return;

        var message = args.ApplicationMessage;
        var generation = Interlocked.Read(ref _activeGeneration);
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
                writeCancellation.Token);
        }
        catch (OperationCanceledException) when (_disposed || !_acceptInboundEvents || writeCancellation.IsCancellationRequested)
        {
        }
        catch (ChannelClosedException) when (_disposed)
        {
        }
    }

    private async Task OnDisconnectedAsync(MqttClientDisconnectedEventArgs args)
    {
        var channel = _received;
        var writeCancellation = _receiveWriteCts;
        if (_disposed || _intentionalDisconnect || !_acceptInboundEvents || channel is null || writeCancellation is null)
            return;

        _acceptInboundEvents = false;
        var generation = Interlocked.Read(ref _activeGeneration);
        var reason = string.IsNullOrWhiteSpace(args.ReasonString)
            ? "MQTT broker connection was lost."
            : $"MQTT broker connection was lost: {Sanitize(args.ReasonString)}";

        try
        {
            await channel.Writer.WriteAsync(
                TransportItem.FromError(generation, new MqttTransportException(reason)),
                writeCancellation.Token);
        }
        catch (OperationCanceledException) when (_disposed || _intentionalDisconnect || writeCancellation.IsCancellationRequested)
        {
        }
        catch (ChannelClosedException) when (_disposed)
        {
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
        _receiveWriteCts?.Cancel();
        _receiveWriteCts?.Dispose();
        _receiveWriteCts = new CancellationTokenSource();
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
