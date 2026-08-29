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
    private readonly Channel<TransportItem> _received;
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private long _activeGeneration;
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
        _received = Channel.CreateUnbounded<TransportItem>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });

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

            var generation = Interlocked.Increment(ref _activeGeneration);
            DrainStaleItems(generation);
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

        while (true)
        {
            var item = await _received.Reader.ReadAsync(cancellationToken);
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
            Interlocked.Increment(ref _activeGeneration);

            _client.ApplicationMessageReceivedAsync -= OnApplicationMessageReceivedAsync;
            _client.DisconnectedAsync -= OnDisconnectedAsync;
            _received.Writer.TryComplete();
            _client.Dispose();
        }
        finally
        {
            _lifecycleGate.Release();
            _lifecycleGate.Dispose();
        }
    }

    private Task OnApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs args)
    {
        if (_disposed) return Task.CompletedTask;

        var message = args.ApplicationMessage;
        var generation = Interlocked.Read(ref _activeGeneration);
        var payload = message.Payload.IsEmpty ? Array.Empty<byte>() : message.Payload.ToArray();
        var received = new MqttTransportMessage(
            message.Topic,
            payload,
            message.Retain,
            FromMqttNetQos(message.QualityOfServiceLevel),
            DateTimeOffset.UtcNow);
        _received.Writer.TryWrite(TransportItem.FromMessage(generation, received));
        return Task.CompletedTask;
    }

    private Task OnDisconnectedAsync(MqttClientDisconnectedEventArgs args)
    {
        if (_disposed || _intentionalDisconnect) return Task.CompletedTask;

        var generation = Interlocked.Read(ref _activeGeneration);
        var reason = string.IsNullOrWhiteSpace(args.ReasonString)
            ? "MQTT broker connection was lost."
            : $"MQTT broker connection was lost: {Sanitize(args.ReasonString)}";
        _received.Writer.TryWrite(TransportItem.FromError(
            generation,
            new MqttTransportException(reason)));
        return Task.CompletedTask;
    }

    private void DrainStaleItems(long currentGeneration)
    {
        while (_received.Reader.TryRead(out var item))
        {
            if (item.Generation == currentGeneration)
            {
                _received.Writer.TryWrite(item);
                return;
            }
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
        MqttClientPublishReasonCode.PayloadFormatInvalid or
        MqttClientPublishReasonCode.RetainNotSupported or
        MqttClientPublishReasonCode.QoSNotSupported;

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
