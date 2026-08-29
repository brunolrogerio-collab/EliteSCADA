namespace Scada.Drivers.Mqtt;

public enum MqttProtocolMode
{
    Mqtt311,
    Mqtt5
}

public sealed record MqttConnectionSettings(
    string Host,
    int Port,
    bool UseTls,
    string ClientId,
    MqttProtocolMode ProtocolMode = MqttProtocolMode.Mqtt5,
    TimeSpan? KeepAlive = null,
    TimeSpan? ConnectTimeout = null,
    TimeSpan? ReconnectMinimumDelay = null,
    TimeSpan? ReconnectMaximumDelay = null,
    bool CleanSession = false,
    uint SessionExpirySeconds = 3600,
    int MaximumInboundPayloadBytes = 1_048_576)
{
    public TimeSpan EffectiveKeepAlive => KeepAlive ?? TimeSpan.FromSeconds(30);
    public TimeSpan EffectiveConnectTimeout => ConnectTimeout ?? TimeSpan.FromSeconds(10);
    public TimeSpan EffectiveReconnectMinimumDelay => ReconnectMinimumDelay ?? TimeSpan.FromSeconds(1);
    public TimeSpan EffectiveReconnectMaximumDelay => ReconnectMaximumDelay ?? TimeSpan.FromSeconds(30);

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Host))
            throw new ArgumentException("MQTT broker host is required.", nameof(Host));
        if (!string.Equals(Host, Host.Trim(), StringComparison.Ordinal))
            throw new ArgumentException("MQTT broker host must not contain surrounding whitespace.", nameof(Host));
        if (Port is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(Port));
        if (string.IsNullOrWhiteSpace(ClientId))
            throw new ArgumentException("MQTT Client ID is required.", nameof(ClientId));
        if (!string.Equals(ClientId, ClientId.Trim(), StringComparison.Ordinal))
            throw new ArgumentException("MQTT Client ID must not contain surrounding whitespace.", nameof(ClientId));
        if (EffectiveKeepAlive <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(KeepAlive));
        if (EffectiveConnectTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(ConnectTimeout));
        if (EffectiveReconnectMinimumDelay <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(ReconnectMinimumDelay));
        if (EffectiveReconnectMaximumDelay < EffectiveReconnectMinimumDelay)
            throw new ArgumentOutOfRangeException(nameof(ReconnectMaximumDelay));
        if (MaximumInboundPayloadBytes < 1)
            throw new ArgumentOutOfRangeException(nameof(MaximumInboundPayloadBytes));

        if (ProtocolMode == MqttProtocolMode.Mqtt311 && SessionExpirySeconds != 3600)
        {
            throw new InvalidOperationException(
                "MQTT 3.1.1 does not support Session Expiry Interval; use CleanSession to control session persistence.");
        }

        if (ProtocolMode == MqttProtocolMode.Mqtt5 && !CleanSession && SessionExpirySeconds == 0)
        {
            throw new InvalidOperationException(
                "Persistent MQTT 5 sessions require a Session Expiry Interval greater than zero.");
        }
    }
}

public sealed record MqttResolvedCredentials(
    string? Username,
    ReadOnlyMemory<byte> Password)
{
    public static MqttResolvedCredentials None { get; } = new(null, ReadOnlyMemory<byte>.Empty);
}

public sealed record MqttSubscription(
    string Topic,
    MqttQosLevel Qos);

public sealed record MqttTransportMessage(
    string Topic,
    ReadOnlyMemory<byte> Payload,
    bool Retained,
    MqttQosLevel Qos,
    DateTimeOffset ReceivedAtUtc);

public sealed record MqttPublishRequest(
    string Topic,
    ReadOnlyMemory<byte> Payload,
    MqttQosLevel Qos,
    bool Retain);

public interface IMqttClientTransport : IAsyncDisposable
{
    bool IsConnected { get; }

    ValueTask ConnectAsync(
        MqttConnectionSettings settings,
        MqttResolvedCredentials credentials,
        CancellationToken cancellationToken = default);

    ValueTask SubscribeAsync(
        IReadOnlyCollection<MqttSubscription> subscriptions,
        CancellationToken cancellationToken = default);

    ValueTask<MqttTransportMessage> ReceiveAsync(CancellationToken cancellationToken = default);

    ValueTask PublishAsync(
        MqttPublishRequest request,
        CancellationToken cancellationToken = default);

    ValueTask DisconnectAsync(CancellationToken cancellationToken = default);
}
