using System.Security.Cryptography;

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
    bool CleanStart = false,
    uint? SessionExpirySeconds = null,
    int MaximumInboundPayloadBytes = 1_048_576,
    int MaximumConsecutiveConnectFailures = 5,
    int MaximumBufferedMessages = 4_096)
{
    public TimeSpan EffectiveKeepAlive => KeepAlive ?? TimeSpan.FromSeconds(30);
    public TimeSpan EffectiveConnectTimeout => ConnectTimeout ?? TimeSpan.FromSeconds(10);
    public TimeSpan EffectiveReconnectMinimumDelay => ReconnectMinimumDelay ?? TimeSpan.FromSeconds(1);
    public TimeSpan EffectiveReconnectMaximumDelay => ReconnectMaximumDelay ?? TimeSpan.FromSeconds(30);
    public uint EffectiveSessionExpirySeconds => SessionExpirySeconds ?? 3600U;

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
        MqttProtocolText.ValidateUtf8EncodedString(ClientId, nameof(ClientId), allowEmpty: false);
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
        if (MaximumConsecutiveConnectFailures < 1)
            throw new ArgumentOutOfRangeException(nameof(MaximumConsecutiveConnectFailures));
        if (MaximumBufferedMessages is < 1 or > 1_000_000)
            throw new ArgumentOutOfRangeException(nameof(MaximumBufferedMessages));

        switch (ProtocolMode)
        {
            case MqttProtocolMode.Mqtt311:
                if (CleanStart)
                    throw new InvalidOperationException("MQTT 3.1.1 does not support Clean Start; use CleanSession.");
                if (SessionExpirySeconds.HasValue)
                    throw new InvalidOperationException("MQTT 3.1.1 does not support Session Expiry Interval.");
                break;

            case MqttProtocolMode.Mqtt5:
                if (CleanSession)
                    throw new InvalidOperationException("MQTT 5 uses Clean Start plus Session Expiry instead of MQTT 3.1.1 CleanSession.");
                if (!CleanStart && EffectiveSessionExpirySeconds == 0)
                {
                    throw new InvalidOperationException(
                        "Persistent MQTT 5 sessions require a Session Expiry Interval greater than zero.");
                }
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(ProtocolMode),
                    ProtocolMode,
                    "Unsupported MQTT protocol mode.");
        }
    }
}

/// <summary>
/// Short-lived resolved authentication material. The instance owns its password
/// buffer and clears it when disposed. Callers must not reuse an owned byte array
/// after passing it to this type.
/// </summary>
public sealed class MqttResolvedCredentials : IDisposable
{
    private byte[] _password;
    private bool _disposed;

    public MqttResolvedCredentials(string? username, byte[]? password = null)
    {
        if (username is not null)
            MqttProtocolText.ValidateUtf8EncodedString(username, nameof(username));
        Username = username;
        _password = password ?? Array.Empty<byte>();
    }

    public MqttResolvedCredentials(string? username, ReadOnlyMemory<byte> password)
        : this(username, password.IsEmpty ? Array.Empty<byte>() : password.ToArray())
    {
    }

    public string? Username { get; }

    public ReadOnlyMemory<byte> Password
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _password;
        }
    }

    public static MqttResolvedCredentials None => new(null);

    public void Dispose()
    {
        if (_disposed) return;
        if (_password.Length > 0)
            CryptographicOperations.ZeroMemory(_password);
        _password = Array.Empty<byte>();
        _disposed = true;
    }
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

public class MqttTransportException : IOException
{
    public MqttTransportException(string message, bool isPermanent = false, Exception? innerException = null)
        : base(message, innerException)
    {
        IsPermanent = isPermanent;
    }

    public bool IsPermanent { get; }
}

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
