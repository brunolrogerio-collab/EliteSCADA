using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;
using Scada.Drivers.Mqtt;

namespace Scada.Drivers.Tests;

public sealed class MqttTransportSafetyTests
{
    [Fact]
    public async Task UndefinedProtocolModeFailsBeforeTransportConnect()
    {
        var settings = new MqttConnectionSettings(
            "broker.invalid",
            1883,
            UseTls: false,
            ClientId: "elite-invalid-protocol",
            ProtocolMode: (MqttProtocolMode)int.MaxValue);
        await using var transport = new MqttNetClientTransport();
        using var credentials = MqttResolvedCredentials.None;

        var error = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            transport.ConnectAsync(settings, credentials).AsTask());

        Assert.Equal("ProtocolMode", error.ParamName);
        Assert.False(transport.IsConnected);
    }

    [Fact]
    public async Task PermanentInboundPolicyFailureFaultsWithoutReconnectLoop()
    {
        var tag = new TagDefinition(
            Guid.NewGuid(),
            "Value",
            $"Plant.Value.{Guid.NewGuid():N}",
            TagDataType.Double,
            "mqtt.raw:test",
            null,
            null,
            true);
        var point = new MqttPoint(tag, "plant/value");
        var transport = new PermanentFailureTransport();
        var cache = new CurrentTagCache(new InMemoryScadaEventBus());
        await using var driver = new MqttDriver(
            "mqtt.raw:test",
            "MQTT transport safety test",
            new MqttConnectionSettings(
                "broker.local",
                1883,
                UseTls: false,
                ClientId: "elite-transport-safety",
                ReconnectMinimumDelay: TimeSpan.FromMilliseconds(5),
                ReconnectMaximumDelay: TimeSpan.FromMilliseconds(10)),
            cache,
            new InMemoryTagRegistry(),
            [point],
            transport);

        await driver.StartAsync();
        await WaitUntilAsync(() => driver.Status.State == DriverState.Faulted);

        Assert.Equal(1, transport.ConnectCount);
        Assert.False(transport.IsConnected);
        Assert.True(cache.TryGet(tag.Id, out var sample));
        Assert.Equal(TagQuality.BadCommunication, sample!.Quality);

        var diagnostics = driver.GetCommunicationDiagnostics();
        Assert.Equal(CommunicationDriverOperationalState.Faulted, diagnostics.State);
        Assert.Equal(1, diagnostics.Counters.FailedOperations);
        Assert.Equal(1, diagnostics.Counters.Disconnections);
    }

    private static async Task WaitUntilAsync(Func<bool> predicate)
    {
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(2);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (predicate()) return;
            await Task.Delay(5);
        }

        Assert.True(predicate(), "Condition did not become true before the test timeout.");
    }

    private sealed class PermanentFailureTransport : IMqttClientTransport
    {
        private bool _failureRaised;

        public bool IsConnected { get; private set; }
        public int ConnectCount { get; private set; }

        public ValueTask ConnectAsync(
            MqttConnectionSettings settings,
            MqttResolvedCredentials credentials,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            settings.Validate();
            _ = credentials;
            ConnectCount++;
            IsConnected = true;
            return ValueTask.CompletedTask;
        }

        public ValueTask SubscribeAsync(
            IReadOnlyCollection<MqttSubscription> subscriptions,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.CompletedTask;
        }

        public ValueTask<MqttTransportMessage> ReceiveAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_failureRaised)
                throw new InvalidOperationException("Receive should not continue after a permanent transport failure.");

            _failureRaised = true;
            throw new MqttTransportException(
                "MQTT inbound payload exceeded the configured transport limit.",
                isPermanent: true);
        }

        public ValueTask PublishAsync(
            MqttPublishRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask DisconnectAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IsConnected = false;
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            IsConnected = false;
            return ValueTask.CompletedTask;
        }
    }
}
