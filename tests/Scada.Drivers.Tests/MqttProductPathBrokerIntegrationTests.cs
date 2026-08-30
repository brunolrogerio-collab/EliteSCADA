using System.Globalization;
using System.Text;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.DriverHost.Runtime;
using Scada.Drivers.Mqtt;

namespace Scada.Drivers.Tests;

public sealed class MqttProductPathBrokerIntegrationTests
{
    private const string HostVariable = "ELITESCADA_MQTT_INTEGRATION_HOST";

    [Fact]
    [Trait("Category", "BrokerIntegration")]
    public async Task ConfiguredBrokerFlowsThroughRuntimeFactoryDriverAndCanonicalTagCache()
    {
        var host = Environment.GetEnvironmentVariable(HostVariable);
        if (string.IsNullOrWhiteSpace(host))
        {
            // Intentionally opt-in. A normal CI pass without a configured broker
            // is not product-path interoperability evidence.
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
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            var topic = $"elitescada/product-path/{runId}/{ProtocolToken(protocol)}/value";
            var tag = TagDefinition.Create(
                "BrokerValue",
                $"Integration.Mqtt.{runId}.{ProtocolToken(protocol)}.BrokerValue",
                TagDataType.Double,
                source: "mqtt.product-path",
                readOnly: true);
            var connection = new MqttConnectionSettings(
                host.Trim(),
                port,
                useTls,
                CreateClientId("rt", runId, protocol),
                ProtocolMode: protocol,
                ConnectTimeout: TimeSpan.FromSeconds(10),
                ReconnectMinimumDelay: TimeSpan.FromMilliseconds(100),
                ReconnectMaximumDelay: TimeSpan.FromSeconds(1),
                CleanSession: protocol == MqttProtocolMode.Mqtt311,
                CleanStart: protocol == MqttProtocolMode.Mqtt5,
                SessionExpirySeconds: protocol == MqttProtocolMode.Mqtt5 ? 0U : null,
                MaximumInboundPayloadBytes: 64 * 1024,
                MaximumBufferedMessages: 64);
            var plan = new MqttRuntimePlan(
                "mqtt.product-path",
                $"mqtt.raw:mqtt.product-path:{ProtocolToken(protocol)}",
                "MQTT live product-path acceptance",
                connection,
                NullIfWhiteSpace(Environment.GetEnvironmentVariable("ELITESCADA_MQTT_INTEGRATION_USERNAME")),
                PasswordSecretReference: null,
                [new MqttPoint(tag, topic, Qos: MqttQosLevel.AtLeastOnce)]);

            var cache = new CurrentTagCache(new InMemoryScadaEventBus());
            var registry = new InMemoryTagRegistry();
            var factory = new MqttRuntimeFactory(
                credentialResolver: CreateRuntimeCredentialResolver());

            await using var driver = factory.Create(plan, cache, registry);
            await driver.StartAsync(timeout.Token);
            await WaitUntilAsync(
                () => driver.GetMqttReadiness().State == MqttReadinessState.Ready,
                timeout.Token,
                "MQTT driver did not become Ready after broker handshake and subscription acceptance.");

            await using var publisher = new MqttNetClientTransport();
            using (var publisherCredentials = CreateCredentials())
            {
                await publisher.ConnectAsync(
                    connection with { ClientId = CreateClientId("pu", runId, protocol) },
                    publisherCredentials,
                    timeout.Token);
            }

            const double expected = 1234.567;
            await publisher.PublishAsync(
                new MqttPublishRequest(
                    topic,
                    Encoding.UTF8.GetBytes(expected.ToString("R", CultureInfo.InvariantCulture)),
                    MqttQosLevel.AtLeastOnce,
                    Retain: false),
                timeout.Token);

            await WaitUntilAsync(
                () => cache.TryGet(tag.Id, out var current) &&
                      current is not null &&
                      current.Quality == TagQuality.Good &&
                      current.Value is double value &&
                      value.Equals(expected),
                timeout.Token,
                "Live broker telemetry did not reach the canonical CurrentTagCache through MqttRuntimeFactory -> MqttDriver.");

            Assert.True(cache.TryGet(tag.Id, out var observed));
            Assert.NotNull(observed);
            Assert.Equal(TagQuality.Good, observed!.Quality);
            Assert.Equal(expected, Assert.IsType<double>(observed.Value));

            await publisher.DisconnectAsync(CancellationToken.None);
            await driver.StopAsync(CancellationToken.None);
        }
    }

    private static MqttRuntimeCredentialResolver? CreateRuntimeCredentialResolver()
    {
        var username = NullIfWhiteSpace(Environment.GetEnvironmentVariable("ELITESCADA_MQTT_INTEGRATION_USERNAME"));
        var password = Environment.GetEnvironmentVariable("ELITESCADA_MQTT_INTEGRATION_PASSWORD");

        if (password is not null && username is null)
        {
            throw new InvalidOperationException(
                "ELITESCADA_MQTT_INTEGRATION_PASSWORD requires ELITESCADA_MQTT_INTEGRATION_USERNAME.");
        }

        if (password is null) return null;

        return (plan, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(
                new MqttResolvedCredentials(plan.Username, Encoding.UTF8.GetBytes(password)));
        };
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

    private static async Task WaitUntilAsync(
        Func<bool> predicate,
        CancellationToken cancellationToken,
        string failureMessage)
    {
        while (!predicate())
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(25, cancellationToken);
        }

        Assert.True(predicate(), failureMessage);
    }

    private static IReadOnlyList<MqttProtocolMode> ParseProtocols(string? value)
    {
        var tokens = string.IsNullOrWhiteSpace(value)
            ? new[] { "mqtt5", "mqtt311" }
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (tokens.Length == 0)
            throw new InvalidOperationException("At least one MQTT integration protocol must be configured.");

        return tokens.Select(token => token.ToLowerInvariant() switch
            {
                "mqtt5" or "5" or "5.0" => MqttProtocolMode.Mqtt5,
                "mqtt311" or "3.1.1" or "311" => MqttProtocolMode.Mqtt311,
                _ => throw new InvalidOperationException(
                    $"Unsupported MQTT integration protocol '{token}'. Use mqtt5 and/or mqtt311.")
            })
            .Distinct()
            .ToArray();
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

    private static string CreateClientId(string role, string runId, MqttProtocolMode protocol) =>
        $"elite-{role}-{ProtocolToken(protocol)}-{runId[..12]}";

    private static string ProtocolToken(MqttProtocolMode protocol) =>
        protocol == MqttProtocolMode.Mqtt5 ? "mqtt5" : "mqtt311";

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
