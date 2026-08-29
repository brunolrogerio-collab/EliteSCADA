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
            var settingsBase = new MqttConnectionSettings(
                host.Trim(),
                port,
                useTls,
                ClientId: $"elite-it-{runId[..12]}",
                ProtocolMode: protocol,
                ConnectTimeout: TimeSpan.FromSeconds(10),
                ReconnectMinimumDelay: TimeSpan.FromMilliseconds(100),
                ReconnectMaximumDelay: TimeSpan.FromSeconds(1),
                CleanSession: protocol == MqttProtocolMode.Mqtt311,
                CleanStart: protocol == MqttProtocolMode.Mqtt5,
                SessionExpirySeconds: protocol == MqttProtocolMode.Mqtt5 ? 0U : null,
                MaximumInboundPayloadBytes: 64 * 1024,
                MaximumBufferedMessages: 64);

            await using var subscriber = new MqttNetClientTransport();
            await using var publisher = new MqttNetClientTransport();
            await using var retainedSubscriber = new MqttNetClientTransport();

            await ConnectAsync(
                subscriber,
                settingsBase with { ClientId = $"elite-sub-{runId[..10]}-{ProtocolToken(protocol)}" },
                timeout.Token);
            await ConnectAsync(
                publisher,
                settingsBase with { ClientId = $"elite-pub-{runId[..10]}-{ProtocolToken(protocol)}" },
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
                settingsBase with { ClientId = $"elite-ret-{runId[..10]}-{ProtocolToken(protocol)}" },
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
