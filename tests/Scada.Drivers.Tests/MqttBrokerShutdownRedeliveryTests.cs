using System.Globalization;
using System.Text;
using Scada.Drivers.Mqtt;

namespace Scada.Drivers.Tests;

public sealed class MqttBrokerShutdownRedeliveryTests
{
    private const string HostVariable = "ELITESCADA_MQTT_INTEGRATION_HOST";

    [Fact]
    [Trait("Category", "BrokerIntegration")]
    public async Task ConfiguredBrokerRedeliversQos2AfterShutdownInterruptsFullQueueAdmission()
    {
        var host = Environment.GetEnvironmentVariable(HostVariable);
        if (string.IsNullOrWhiteSpace(host))
            return;

        const int queueCapacity = 1;
        var useTls = ParseBooleanEnvironment("ELITESCADA_MQTT_INTEGRATION_TLS", defaultValue: false);
        var port = ParsePortEnvironment(
            "ELITESCADA_MQTT_INTEGRATION_PORT",
            useTls ? 8883 : 1883);
        var protocols = ParseProtocols(Environment.GetEnvironmentVariable("ELITESCADA_MQTT_INTEGRATION_PROTOCOLS"));
        var runId = Guid.NewGuid().ToString("N");

        foreach (var protocol in protocols)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            var clientId = $"elite-q2red-{runId[..9]}-{ProtocolToken(protocol)}";
            var settings = CreatePersistentSettings(
                host,
                port,
                useTls,
                protocol,
                clientId,
                maximumBufferedMessages: queueCapacity);
            var publisherSettings = CreateCleanSettings(
                host,
                port,
                useTls,
                protocol,
                $"elite-q2pub-{runId[..9]}-{ProtocolToken(protocol)}",
                maximumBufferedMessages: 8);
            var topic = $"elitescada/integration/{runId}/{ProtocolToken(protocol)}/qos2-shutdown-redelivery";
            var admittedPayload = $"qos2-admitted:{runId}";
            var pendingPayload = $"qos2-pending:{runId}";

            await using var subscriber = new MqttNetClientTransport();
            await using var publisher = new MqttNetClientTransport();
            await ConnectAsync(subscriber, settings, timeout.Token);
            await ConnectAsync(publisher, publisherSettings, timeout.Token);
            await subscriber.SubscribeAsync(
                [new MqttSubscription(topic, MqttQosLevel.ExactlyOnce)],
                timeout.Token);

            await publisher.PublishAsync(
                new MqttPublishRequest(
                    topic,
                    Encoding.UTF8.GetBytes(admittedPayload),
                    MqttQosLevel.ExactlyOnce,
                    Retain: false),
                timeout.Token);
            await publisher.PublishAsync(
                new MqttPublishRequest(
                    topic,
                    Encoding.UTF8.GetBytes(pendingPayload),
                    MqttQosLevel.ExactlyOnce,
                    Retain: false),
                timeout.Token);

            // The first callback can occupy the single application queue slot.
            // The second ordered QoS 2 delivery is then expected to wait at the
            // bounded admission boundary until shutdown cancels receive writers.
            await Task.Delay(TimeSpan.FromMilliseconds(500), timeout.Token);
            await subscriber.DisconnectAsync(timeout.Token);

            await ConnectAsync(subscriber, settings, timeout.Token);
            await subscriber.SubscribeAsync(
                [new MqttSubscription(topic, MqttQosLevel.ExactlyOnce)],
                timeout.Token);

            var pendingWasRedelivered = false;
            for (var attempt = 0; attempt < 8; attempt++)
            {
                var received = await subscriber.ReceiveAsync(timeout.Token);
                Assert.Equal(topic, received.Topic);
                Assert.Equal(MqttQosLevel.ExactlyOnce, received.Qos);

                var text = Encoding.UTF8.GetString(received.Payload.ToArray());
                if (text == pendingPayload)
                {
                    pendingWasRedelivered = true;
                    break;
                }
            }

            Assert.True(
                pendingWasRedelivered,
                $"Broker did not redeliver the QoS 2 message whose bounded queue admission was interrupted for {ProtocolToken(protocol)}.");

            await subscriber.DisconnectAsync(CancellationToken.None);
            await publisher.DisconnectAsync(CancellationToken.None);

            var cleanupSettings = settings with
            {
                CleanSession = protocol == MqttProtocolMode.Mqtt311,
                CleanStart = protocol == MqttProtocolMode.Mqtt5,
                SessionExpirySeconds = protocol == MqttProtocolMode.Mqtt5 ? 0U : null
            };
            await ConnectAsync(subscriber, cleanupSettings, timeout.Token);
            await subscriber.DisconnectAsync(CancellationToken.None);
        }
    }

    private static MqttConnectionSettings CreatePersistentSettings(
        string host,
        int port,
        bool useTls,
        MqttProtocolMode protocol,
        string clientId,
        int maximumBufferedMessages) =>
        new(
            host.Trim(),
            port,
            useTls,
            clientId,
            ProtocolMode: protocol,
            ConnectTimeout: TimeSpan.FromSeconds(10),
            ReconnectMinimumDelay: TimeSpan.FromMilliseconds(100),
            ReconnectMaximumDelay: TimeSpan.FromSeconds(1),
            CleanSession: false,
            CleanStart: false,
            SessionExpirySeconds: protocol == MqttProtocolMode.Mqtt5 ? 60U : null,
            MaximumInboundPayloadBytes: 64 * 1024,
            MaximumBufferedMessages: maximumBufferedMessages);

    private static MqttConnectionSettings CreateCleanSettings(
        string host,
        int port,
        bool useTls,
        MqttProtocolMode protocol,
        string clientId,
        int maximumBufferedMessages) =>
        new(
            host.Trim(),
            port,
            useTls,
            clientId,
            ProtocolMode: protocol,
            ConnectTimeout: TimeSpan.FromSeconds(10),
            ReconnectMinimumDelay: TimeSpan.FromMilliseconds(100),
            ReconnectMaximumDelay: TimeSpan.FromSeconds(1),
            CleanSession: protocol == MqttProtocolMode.Mqtt311,
            CleanStart: protocol == MqttProtocolMode.Mqtt5,
            SessionExpirySeconds: protocol == MqttProtocolMode.Mqtt5 ? 0U : null,
            MaximumInboundPayloadBytes: 64 * 1024,
            MaximumBufferedMessages: maximumBufferedMessages);

    private static async Task ConnectAsync(
        MqttNetClientTransport transport,
        MqttConnectionSettings settings,
        CancellationToken cancellationToken)
    {
        using var credentials = CreateCredentials();
        await transport.ConnectAsync(settings, credentials, cancellationToken);
    }

    private static MqttResolvedCredentials CreateCredentials()
    {
        var username = NullIfWhiteSpace(Environment.GetEnvironmentVariable("ELITESCADA_MQTT_INTEGRATION_USERNAME"));
        var password = Environment.GetEnvironmentVariable("ELITESCADA_MQTT_INTEGRATION_PASSWORD");

        if (password is not null && username is null)
        {
            throw new InvalidOperationException(
                "ELITESCADA_MQTT_INTEGRATION_PASSWORD requires ELITESCADA_MQTT_INTEGRATION_USERNAME.");
        }

        return password is null
            ? new MqttResolvedCredentials(username)
            : new MqttResolvedCredentials(username, Encoding.UTF8.GetBytes(password));
    }

    private static IReadOnlyList<MqttProtocolMode> ParseProtocols(string? value)
    {
        var tokens = string.IsNullOrWhiteSpace(value)
            ? new[] { "mqtt5", "mqtt311" }
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (tokens.Length == 0)
            throw new InvalidOperationException("At least one MQTT integration protocol must be configured.");

        var protocols = new List<MqttProtocolMode>(tokens.Length);
        foreach (var token in tokens)
        {
            protocols.Add(token.ToLowerInvariant() switch
            {
                "mqtt5" or "5" or "5.0" => MqttProtocolMode.Mqtt5,
                "mqtt311" or "3.1.1" or "311" => MqttProtocolMode.Mqtt311,
                _ => throw new InvalidOperationException(
                    $"Unsupported MQTT integration protocol '{token}'. Use mqtt5 and/or mqtt311.")
            });
        }

        return protocols.Distinct().ToArray();
    }

    private static bool ParseBooleanEnvironment(string name, bool defaultValue)
    {
        var text = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(text)) return defaultValue;
        if (bool.TryParse(text, out var parsed)) return parsed;
        throw new InvalidOperationException($"Environment variable '{name}' must be true or false.");
    }

    private static int ParsePortEnvironment(string name, int defaultValue)
    {
        var text = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(text)) return defaultValue;
        if (int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) &&
            parsed is >= 1 and <= 65535)
        {
            return parsed;
        }

        throw new InvalidOperationException($"Environment variable '{name}' must be a TCP port from 1 to 65535.");
    }

    private static string ProtocolToken(MqttProtocolMode protocol) => protocol switch
    {
        MqttProtocolMode.Mqtt5 => "mqtt5",
        MqttProtocolMode.Mqtt311 => "mqtt311",
        _ => throw new ArgumentOutOfRangeException(nameof(protocol), protocol, null)
    };

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}