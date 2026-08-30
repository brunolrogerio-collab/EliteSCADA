using System.Globalization;
using System.Text;
using Scada.Drivers.Mqtt;

namespace Scada.Drivers.Tests;

public sealed class MqttBrokerIntegrationTests
{
    private const string HostVariable = "ELITESCADA_MQTT_INTEGRATION_HOST";

    [Fact]
    [Trait("Category", "BrokerIntegration")]
    public async Task ConfiguredBrokerSupportsProtocolQosAndRetainedContract()
    {
        var host = Environment.GetEnvironmentVariable(HostVariable);
        if (string.IsNullOrWhiteSpace(host))
        {
            // Intentionally opt-in. A normal unit-test run must not depend on an
            // external broker. The handoff explicitly records that this early
            // return is not evidence of broker interoperability.
            return;
        }

        var useTls = ParseBooleanEnvironment("ELITESCADA_MQTT_INTEGRATION_TLS", defaultValue: false);
        var port = ParsePortEnvironment(
            "ELITESCADA_MQTT_INTEGRATION_PORT",
            useTls ? 8883 : 1883);
        var protocols = ParseProtocols(Environment.GetEnvironmentVariable("ELITESCADA_MQTT_INTEGRATION_PROTOCOLS"));
        var runId = Guid.NewGuid().ToString("N");

        foreach (var protocol in protocols)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var settingsBase = CreateSettings(
                host,
                port,
                useTls,
                protocol,
                CreateClientId("it", runId, protocol),
                maximumBufferedMessages: 64);

            await using var subscriber = new MqttNetClientTransport();
            await using var publisher = new MqttNetClientTransport();
            await using var retainedSubscriber = new MqttNetClientTransport();

            await ConnectAsync(
                subscriber,
                settingsBase with { ClientId = CreateClientId("su", runId, protocol) },
                timeout.Token);
            await ConnectAsync(
                publisher,
                settingsBase with { ClientId = CreateClientId("pu", runId, protocol) },
                timeout.Token);

            foreach (var qos in new[]
                     {
                         MqttQosLevel.AtMostOnce,
                         MqttQosLevel.AtLeastOnce,
                         MqttQosLevel.ExactlyOnce
                     })
            {
                var topic = $"elitescada/integration/{runId}/{ProtocolToken(protocol)}/qos/{(int)qos}";
                await subscriber.SubscribeAsync([new MqttSubscription(topic, qos)], timeout.Token);

                var expected = $"roundtrip:{ProtocolToken(protocol)}:{(int)qos}:{runId}";
                await publisher.PublishAsync(
                    new MqttPublishRequest(topic, Encoding.UTF8.GetBytes(expected), qos, Retain: false),
                    timeout.Token);

                var received = await subscriber.ReceiveAsync(timeout.Token);
                Assert.Equal(topic, received.Topic);
                Assert.Equal(qos, received.Qos);
                Assert.False(received.Retained);
                Assert.Equal(expected, Encoding.UTF8.GetString(received.Payload.ToArray()));
            }

            var retainedTopic = $"elitescada/integration/{runId}/{ProtocolToken(protocol)}/retained";
            var retainedPayload = $"retained:{ProtocolToken(protocol)}:{runId}";
            await publisher.PublishAsync(
                new MqttPublishRequest(
                    retainedTopic,
                    Encoding.UTF8.GetBytes(retainedPayload),
                    MqttQosLevel.AtLeastOnce,
                    Retain: true),
                timeout.Token);

            await ConnectAsync(
                retainedSubscriber,
                settingsBase with { ClientId = CreateClientId("rt", runId, protocol) },
                timeout.Token);
            await retainedSubscriber.SubscribeAsync(
                [new MqttSubscription(retainedTopic, MqttQosLevel.AtLeastOnce)],
                timeout.Token);

            var retained = await retainedSubscriber.ReceiveAsync(timeout.Token);
            Assert.Equal(retainedTopic, retained.Topic);
            Assert.Equal(MqttQosLevel.AtLeastOnce, retained.Qos);
            Assert.True(retained.Retained);
            Assert.Equal(retainedPayload, Encoding.UTF8.GetString(retained.Payload.ToArray()));

            // Delete the retained value so repeated manual runs do not leave
            // broker-side test state behind.
            await publisher.PublishAsync(
                new MqttPublishRequest(
                    retainedTopic,
                    ReadOnlyMemory<byte>.Empty,
                    MqttQosLevel.AtLeastOnce,
                    Retain: true),
                timeout.Token);

            await retainedSubscriber.DisconnectAsync(CancellationToken.None);
            await subscriber.DisconnectAsync(CancellationToken.None);
            await publisher.DisconnectAsync(CancellationToken.None);
        }
    }

    [Fact]
    [Trait("Category", "BrokerIntegration")]
    public async Task ConfiguredBrokerPreservesBurstBeyondBoundedApplicationQueue()
    {
        var host = Environment.GetEnvironmentVariable(HostVariable);
        if (string.IsNullOrWhiteSpace(host))
            return;

        const int queueCapacity = 4;
        const int burstCount = 64;
        var useTls = ParseBooleanEnvironment("ELITESCADA_MQTT_INTEGRATION_TLS", defaultValue: false);
        var port = ParsePortEnvironment(
            "ELITESCADA_MQTT_INTEGRATION_PORT",
            useTls ? 8883 : 1883);
        var protocols = ParseProtocols(Environment.GetEnvironmentVariable("ELITESCADA_MQTT_INTEGRATION_PROTOCOLS"));
        var runId = Guid.NewGuid().ToString("N");

        foreach (var protocol in protocols)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            var settings = CreateSettings(
                host,
                port,
                useTls,
                protocol,
                CreateClientId("bt", runId, protocol),
                maximumBufferedMessages: queueCapacity);
            var topic = $"elitescada/integration/{runId}/{ProtocolToken(protocol)}/bounded-burst";

            await using var subscriber = new MqttNetClientTransport();
            await using var publisher = new MqttNetClientTransport();
            await ConnectAsync(
                subscriber,
                settings with { ClientId = CreateClientId("bs", runId, protocol) },
                timeout.Token);
            await ConnectAsync(
                publisher,
                settings with { ClientId = CreateClientId("bp", runId, protocol) },
                timeout.Token);
            await subscriber.SubscribeAsync(
                [new MqttSubscription(topic, MqttQosLevel.AtLeastOnce)],
                timeout.Token);

            // Deliberately publish substantially more messages than the local queue
            // can hold before calling ReceiveAsync. MQTTnet callbacks must therefore
            // wait for bounded EliteSCADA queue capacity instead of dropping data.
            for (var index = 0; index < burstCount; index++)
            {
                var payload = Encoding.UTF8.GetBytes($"burst:{index:D4}:{runId}");
                await publisher.PublishAsync(
                    new MqttPublishRequest(
                        topic,
                        payload,
                        MqttQosLevel.AtLeastOnce,
                        Retain: false),
                    timeout.Token);
            }

            var observed = new HashSet<int>();
            while (observed.Count < burstCount)
            {
                var received = await subscriber.ReceiveAsync(timeout.Token);
                Assert.Equal(topic, received.Topic);
                Assert.Equal(MqttQosLevel.AtLeastOnce, received.Qos);
                Assert.False(received.Retained);

                var text = Encoding.UTF8.GetString(received.Payload.ToArray());
                var parts = text.Split(':');
                Assert.Equal(3, parts.Length);
                Assert.Equal("burst", parts[0]);
                Assert.Equal(runId, parts[2]);
                Assert.True(int.TryParse(
                    parts[1],
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var index));
                Assert.InRange(index, 0, burstCount - 1);
                observed.Add(index);
            }

            Assert.Equal(burstCount, observed.Count);
            await subscriber.DisconnectAsync(CancellationToken.None);
            await publisher.DisconnectAsync(CancellationToken.None);
        }
    }

    [Fact]
    [Trait("Category", "BrokerIntegration")]
    public async Task ConfiguredBrokerRedeliversAfterShutdownInterruptsFullQueueAdmission()
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
            var clientId = CreateClientId("rs", runId, protocol);
            var settings = CreatePersistentSettings(
                host,
                port,
                useTls,
                protocol,
                clientId,
                maximumBufferedMessages: queueCapacity);
            var publisherSettings = CreateSettings(
                host,
                port,
                useTls,
                protocol,
                CreateClientId("rp", runId, protocol),
                maximumBufferedMessages: 8);
            var topic = $"elitescada/integration/{runId}/{ProtocolToken(protocol)}/shutdown-redelivery";
            var admittedPayload = $"admitted:{runId}";
            var pendingPayload = $"pending:{runId}";

            await using var subscriber = new MqttNetClientTransport();
            await using var publisher = new MqttNetClientTransport();
            await ConnectAsync(subscriber, settings, timeout.Token);
            await ConnectAsync(publisher, publisherSettings, timeout.Token);
            await subscriber.SubscribeAsync(
                [new MqttSubscription(topic, MqttQosLevel.AtLeastOnce)],
                timeout.Token);

            await publisher.PublishAsync(
                new MqttPublishRequest(
                    topic,
                    Encoding.UTF8.GetBytes(admittedPayload),
                    MqttQosLevel.AtLeastOnce,
                    Retain: false),
                timeout.Token);
            await publisher.PublishAsync(
                new MqttPublishRequest(
                    topic,
                    Encoding.UTF8.GetBytes(pendingPayload),
                    MqttQosLevel.AtLeastOnce,
                    Retain: false),
                timeout.Token);

            // The queue can hold only the first callback result. Give the broker and
            // client receive loop a bounded settling interval so the second ordered
            // QoS 1 delivery reaches the blocked application-admission boundary.
            // This is a live interoperability test, not a timing-based unit test.
            await Task.Delay(TimeSpan.FromMilliseconds(500), timeout.Token);

            // Disconnect cancels writers waiting on bounded queue capacity. The
            // blocked QoS 1 callback must leave ProcessingFailed=true and never call
            // the deferred ACK API. The persistent broker session should therefore
            // make that delivery available after reconnect.
            await subscriber.DisconnectAsync(timeout.Token);

            await ConnectAsync(subscriber, settings, timeout.Token);
            await subscriber.SubscribeAsync(
                [new MqttSubscription(topic, MqttQosLevel.AtLeastOnce)],
                timeout.Token);

            var pendingWasRedelivered = false;
            for (var attempt = 0; attempt < 8; attempt++)
            {
                var received = await subscriber.ReceiveAsync(timeout.Token);
                Assert.Equal(topic, received.Topic);
                Assert.Equal(MqttQosLevel.AtLeastOnce, received.Qos);

                var text = Encoding.UTF8.GetString(received.Payload.ToArray());
                if (text == pendingPayload)
                {
                    pendingWasRedelivered = true;
                    break;
                }
            }

            Assert.True(
                pendingWasRedelivered,
                $"Broker did not redeliver the QoS 1 message whose bounded queue admission was interrupted for {ProtocolToken(protocol)}.");

            await subscriber.DisconnectAsync(CancellationToken.None);
            await publisher.DisconnectAsync(CancellationToken.None);

            // Clear the persistent session created only for this unique validation
            // client. MQTT 3.1.1 uses CleanSession=true; MQTT 5 uses CleanStart=true
            // with zero session expiry.
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

    private static MqttConnectionSettings CreateSettings(
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

    private static string CreateClientId(string role, string runId, MqttProtocolMode protocol)
    {
        if (role.Length is < 1 or > 3)
            throw new ArgumentOutOfRangeException(nameof(role), "Integration Client ID role must contain 1 to 3 characters.");
        if (runId.Length < 16)
            throw new ArgumentException("Integration run ID must contain at least 16 characters.", nameof(runId));

        return $"{role}-{runId[..16]}-{ProtocolShortToken(protocol)}";
    }

    private static string ProtocolShortToken(MqttProtocolMode protocol) => protocol switch
    {
        MqttProtocolMode.Mqtt5 => "5",
        MqttProtocolMode.Mqtt311 => "3",
        _ => throw new ArgumentOutOfRangeException(nameof(protocol), protocol, null)
    };

    private static string ProtocolToken(MqttProtocolMode protocol) => protocol switch
    {
        MqttProtocolMode.Mqtt5 => "mqtt5",
        MqttProtocolMode.Mqtt311 => "mqtt311",
        _ => throw new ArgumentOutOfRangeException(nameof(protocol), protocol, null)
    };

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
