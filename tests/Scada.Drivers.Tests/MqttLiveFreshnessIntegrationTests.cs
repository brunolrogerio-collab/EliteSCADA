using System.Globalization;
using System.Text;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;
using Scada.Drivers.Mqtt;

namespace Scada.Drivers.Tests;

public sealed class MqttLiveFreshnessIntegrationTests
{
    private const string HostVariable = "ELITESCADA_MQTT_FRESHNESS_HOST";

    [Fact]
    [Trait("Category", "BrokerFreshnessIntegration")]
    public async Task LiveBrokerSilenceMarksTagStaleAndFreshTelemetryRecoversWithoutLosingReadiness()
    {
        var host = Environment.GetEnvironmentVariable(HostVariable);
        if (string.IsNullOrWhiteSpace(host))
            return;

        var port = ParsePortEnvironment("ELITESCADA_MQTT_FRESHNESS_PORT", 1883);
        var runId = Environment.GetEnvironmentVariable("ELITESCADA_MQTT_FRESHNESS_RUN_ID")?.Trim();
        if (string.IsNullOrWhiteSpace(runId))
            throw new InvalidOperationException("ELITESCADA_MQTT_FRESHNESS_RUN_ID is required.");

        foreach (var protocol in Protocols)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var token = ProtocolToken(protocol);
            var topic = $"elitescada/freshness/{runId}/{token}";
            var tag = TagDefinition.Create(
                $"Freshness {token}",
                $"Lab.MqttFreshness.{runId}.{token}",
                TagDataType.Double,
                source: $"mqtt.raw:freshness-{token}",
                readOnly: true);
            var point = new MqttPoint(
                tag,
                topic,
                PayloadFormat: MqttPayloadFormat.Json,
                JsonPointer: "/value",
                SourceTimestampJsonPointer: "/timestamp",
                SourceTimestampRequired: true,
                FreshnessTimeout: TimeSpan.FromMilliseconds(500));
            var cache = new CurrentTagCache(new InMemoryScadaEventBus());
            var registry = new InMemoryTagRegistry();

            await using var subscriberTransport = new MqttNetClientTransport();
            await using var driver = new MqttDriver(
                $"mqtt.raw:freshness-{token}",
                $"MQTT live freshness {token}",
                CreateSettings(
                    host,
                    port,
                    protocol,
                    $"es-fresh-sub-{token}-{runId}"),
                cache,
                registry,
                [point],
                subscriberTransport);

            await driver.StartAsync(timeout.Token);
            await WaitUntilAsync(
                () => driver.GetMqttReadiness().State == MqttReadinessState.Ready,
                timeout.Token,
                "MQTT driver did not become Ready against the live broker.");

            await using var publisher = new MqttNetClientTransport();
            await publisher.ConnectAsync(
                CreateSettings(
                    host,
                    port,
                    protocol,
                    $"es-fresh-pub-{token}-{runId}"),
                MqttResolvedCredentials.None,
                timeout.Token);

            var firstSourceTimestamp = DateTimeOffset.UtcNow - TimeSpan.FromMinutes(5);
            await PublishJsonAsync(
                publisher,
                topic,
                value: 41.25d,
                firstSourceTimestamp,
                timeout.Token);

            await WaitUntilAsync(
                () => cache.TryGet(tag.Id, out var sample) &&
                      sample?.Quality == TagQuality.Good &&
                      Equals(sample.Value, 41.25d),
                timeout.Token,
                "Live MQTT sample did not become Good.");

            Assert.True(cache.TryGet(tag.Id, out var firstGood));
            Assert.NotNull(firstGood);
            Assert.Equal(firstSourceTimestamp.ToUniversalTime(), firstGood!.SourceTimestamp);
            Assert.Equal(MqttReadinessState.Ready, driver.GetMqttReadiness().State);
            Assert.Equal(CommunicationDriverOperationalState.Healthy, driver.GetCommunicationDiagnostics().State);

            await WaitUntilAsync(
                () => cache.TryGet(tag.Id, out var sample) && sample?.Quality == TagQuality.Stale,
                timeout.Token,
                "Live MQTT sample did not become Stale after broker silence.");

            Assert.True(cache.TryGet(tag.Id, out var stale));
            Assert.NotNull(stale);
            Assert.Equal(41.25d, stale!.Value);
            Assert.Equal(firstSourceTimestamp.ToUniversalTime(), stale.SourceTimestamp);

            var staleDiagnostics = driver.GetCommunicationDiagnostics();
            Assert.Equal(CommunicationDriverOperationalState.Healthy, staleDiagnostics.State);
            Assert.Equal(MqttReadinessState.Ready, driver.GetMqttReadiness().State);
            Assert.Equal(1, staleDiagnostics.Quality.Stale);
            Assert.Equal("1", staleDiagnostics.ProtocolDetails!["freshnessPointCount"]);
            Assert.True(
                long.Parse(staleDiagnostics.ProtocolDetails["freshnessTransitions"], CultureInfo.InvariantCulture) >= 1);

            var secondSourceTimestamp = DateTimeOffset.UtcNow;
            await PublishJsonAsync(
                publisher,
                topic,
                value: 77.5d,
                secondSourceTimestamp,
                timeout.Token);

            await WaitUntilAsync(
                () => cache.TryGet(tag.Id, out var sample) &&
                      sample?.Quality == TagQuality.Good &&
                      Equals(sample.Value, 77.5d),
                timeout.Token,
                "Fresh live MQTT telemetry did not recover the stale TAG to Good.");

            Assert.True(cache.TryGet(tag.Id, out var recovered));
            Assert.NotNull(recovered);
            Assert.Equal(secondSourceTimestamp.ToUniversalTime(), recovered!.SourceTimestamp);
            Assert.Equal(MqttReadinessState.Ready, driver.GetMqttReadiness().State);
            Assert.Equal(CommunicationDriverOperationalState.Healthy, driver.GetCommunicationDiagnostics().State);

            await publisher.DisconnectAsync(timeout.Token);
            await driver.StopAsync(timeout.Token);
        }
    }

    private static async Task PublishJsonAsync(
        MqttNetClientTransport publisher,
        string topic,
        double value,
        DateTimeOffset sourceTimestamp,
        CancellationToken cancellationToken)
    {
        var payload = Encoding.UTF8.GetBytes(
            $"{{\"value\":{value.ToString(CultureInfo.InvariantCulture)},\"timestamp\":\"{sourceTimestamp:O}\"}}");
        await publisher.PublishAsync(
            new MqttPublishRequest(
                topic,
                payload,
                MqttQosLevel.AtLeastOnce,
                Retain: false),
            cancellationToken);
    }

    private static MqttConnectionSettings CreateSettings(
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

    private static readonly MqttProtocolMode[] Protocols =
    [
        MqttProtocolMode.Mqtt5,
        MqttProtocolMode.Mqtt311
    ];

    private static string ProtocolToken(MqttProtocolMode protocol) => protocol switch
    {
        MqttProtocolMode.Mqtt5 => "m5",
        MqttProtocolMode.Mqtt311 => "m311",
        _ => throw new ArgumentOutOfRangeException(nameof(protocol), protocol, null)
    };

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

    private static async Task WaitUntilAsync(
        Func<bool> predicate,
        CancellationToken cancellationToken,
        string failureMessage)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (predicate())
                return;

            await Task.Delay(10, cancellationToken);
        }

        Assert.True(predicate(), failureMessage);
    }
}
