using System.Globalization;
using System.Text;
using Scada.Drivers.Mqtt;

namespace Scada.Drivers.Tests;

public sealed class MqttBrokerRestartIntegrationTests
{
    private const string HostVariable = "ELITESCADA_MQTT_RESTART_HOST";
    private const string RunIdVariable = "ELITESCADA_MQTT_RESTART_RUN_ID";

    [Fact]
    [Trait("Category", "BrokerRestartIntegration")]
    public async Task PreparePersistentSessionBeforeBrokerRestart()
    {
        var host = Environment.GetEnvironmentVariable(HostVariable);
        if (string.IsNullOrWhiteSpace(host))
            return;

        var port = ParsePortEnvironment("ELITESCADA_MQTT_RESTART_PORT", 1885);
        var runId = RequireSafeRunId();

        foreach (var protocol in Protocols)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var topic = Topic(runId, protocol);
            var subscriberSettings = CreatePersistentSettings(
                host,
                port,
                protocol,
                SubscriberClientId(runId, protocol));
            var publisherSettings = CreateCleanSettings(
                host,
                port,
                protocol,
                PublisherClientId(runId, protocol));

            await using var subscriber = new MqttNetClientTransport();
            await subscriber.ConnectAsync(subscriberSettings, MqttResolvedCredentials.None, timeout.Token);
            await subscriber.SubscribeAsync(
                [new MqttSubscription(topic, MqttQosLevel.ExactlyOnce)],
                timeout.Token);
            await subscriber.DisconnectAsync(timeout.Token);

            await using var publisher = new MqttNetClientTransport();
            await publisher.ConnectAsync(publisherSettings, MqttResolvedCredentials.None, timeout.Token);
            await publisher.PublishAsync(
                new MqttPublishRequest(
                    topic,
                    Encoding.UTF8.GetBytes(Payload(runId, protocol, "qos1")),
                    MqttQosLevel.AtLeastOnce,
                    Retain: false),
                timeout.Token);
            await publisher.PublishAsync(
                new MqttPublishRequest(
                    topic,
                    Encoding.UTF8.GetBytes(Payload(runId, protocol, "qos2")),
                    MqttQosLevel.ExactlyOnce,
                    Retain: false),
                timeout.Token);
            await publisher.DisconnectAsync(timeout.Token);
        }
    }

    [Fact]
    [Trait("Category", "BrokerRestartIntegration")]
    public async Task RecoverPersistentSessionAfterBrokerRestartWithoutResubscribe()
    {
        var host = Environment.GetEnvironmentVariable(HostVariable);
        if (string.IsNullOrWhiteSpace(host))
            return;

        var port = ParsePortEnvironment("ELITESCADA_MQTT_RESTART_PORT", 1885);
        var runId = RequireSafeRunId();

        foreach (var protocol in Protocols)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var topic = Topic(runId, protocol);
            var subscriberSettings = CreatePersistentSettings(
                host,
                port,
                protocol,
                SubscriberClientId(runId, protocol));

            await using var subscriber = new MqttNetClientTransport();
            await subscriber.ConnectAsync(subscriberSettings, MqttResolvedCredentials.None, timeout.Token);

            var expected = new HashSet<string>(StringComparer.Ordinal)
            {
                Payload(runId, protocol, "qos1"),
                Payload(runId, protocol, "qos2")
            };
            var observed = new HashSet<string>(StringComparer.Ordinal);

            while (observed.Count < expected.Count)
            {
                var received = await subscriber.ReceiveAsync(timeout.Token);
                Assert.Equal(topic, received.Topic);
                Assert.False(received.Retained);
                Assert.Contains(received.Qos, new[] { MqttQosLevel.AtLeastOnce, MqttQosLevel.ExactlyOnce });

                var payload = Encoding.UTF8.GetString(received.Payload.ToArray());
                Assert.Contains(payload, expected);
                observed.Add(payload);
            }

            Assert.True(expected.SetEquals(observed));
            await subscriber.DisconnectAsync(timeout.Token);

            var cleanupSettings = CreateCleanSettings(
                host,
                port,
                protocol,
                SubscriberClientId(runId, protocol));
            await subscriber.ConnectAsync(cleanupSettings, MqttResolvedCredentials.None, timeout.Token);
            await subscriber.DisconnectAsync(timeout.Token);
        }
    }

    private static readonly MqttProtocolMode[] Protocols =
    [
        MqttProtocolMode.Mqtt5,
        MqttProtocolMode.Mqtt311
    ];

    private static MqttConnectionSettings CreatePersistentSettings(
        string host,
        int port,
        MqttProtocolMode protocol,
        string clientId) =>
        new(
            host.Trim(),
            port,
            UseTls: false,
            clientId,
            ProtocolMode: protocol,
            ConnectTimeout: TimeSpan.FromSeconds(10),
            ReconnectMinimumDelay: TimeSpan.FromMilliseconds(100),
            ReconnectMaximumDelay: TimeSpan.FromSeconds(1),
            CleanSession: false,
            CleanStart: false,
            SessionExpirySeconds: protocol == MqttProtocolMode.Mqtt5 ? 300U : null,
            MaximumInboundPayloadBytes: 64 * 1024,
            MaximumBufferedMessages: 16);

    private static MqttConnectionSettings CreateCleanSettings(
        string host,
        int port,
        MqttProtocolMode protocol,
        string clientId) =>
        new(
            host.Trim(),
            port,
            UseTls: false,
            clientId,
            ProtocolMode: protocol,
            ConnectTimeout: TimeSpan.FromSeconds(10),
            ReconnectMinimumDelay: TimeSpan.FromMilliseconds(100),
            ReconnectMaximumDelay: TimeSpan.FromSeconds(1),
            CleanSession: protocol == MqttProtocolMode.Mqtt311,
            CleanStart: protocol == MqttProtocolMode.Mqtt5,
            SessionExpirySeconds: protocol == MqttProtocolMode.Mqtt5 ? 0U : null,
            MaximumInboundPayloadBytes: 64 * 1024,
            MaximumBufferedMessages: 16);

    private static string RequireSafeRunId()
    {
        var runId = Environment.GetEnvironmentVariable(RunIdVariable);
        if (string.IsNullOrWhiteSpace(runId))
            throw new InvalidOperationException($"Environment variable '{RunIdVariable}' is required.");

        var trimmed = runId.Trim();
        if (trimmed.Length is < 1 or > 32 || trimmed.Any(ch => !char.IsLetterOrDigit(ch)))
        {
            throw new InvalidOperationException(
                $"Environment variable '{RunIdVariable}' must contain 1-32 ASCII letters or digits.");
        }

        return trimmed;
    }

    private static int ParsePortEnvironment(string name, int defaultValue)
    {
        var text = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(text))
            return defaultValue;

        if (int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) &&
            parsed is >= 1 and <= 65535)
        {
            return parsed;
        }

        throw new InvalidOperationException($"Environment variable '{name}' must be a TCP port from 1 to 65535.");
    }

    private static string SubscriberClientId(string runId, MqttProtocolMode protocol) =>
        $"es-rs-{ProtocolToken(protocol)}-{runId}";

    private static string PublisherClientId(string runId, MqttProtocolMode protocol) =>
        $"es-rp-{ProtocolToken(protocol)}-{runId}";

    private static string Topic(string runId, MqttProtocolMode protocol) =>
        $"elitescada/restart/{runId}/{ProtocolToken(protocol)}";

    private static string Payload(string runId, MqttProtocolMode protocol, string suffix) =>
        $"restart:{runId}:{ProtocolToken(protocol)}:{suffix}";

    private static string ProtocolToken(MqttProtocolMode protocol) => protocol switch
    {
        MqttProtocolMode.Mqtt5 => "m5",
        MqttProtocolMode.Mqtt311 => "m311",
        _ => throw new ArgumentOutOfRangeException(nameof(protocol), protocol, null)
    };
}
