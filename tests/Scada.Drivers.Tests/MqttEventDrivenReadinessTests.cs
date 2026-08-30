using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;
using Scada.Drivers.Mqtt;

namespace Scada.Drivers.Tests;

public sealed class MqttEventDrivenReadinessTests
{
    [Fact]
    public async Task ConnectedAndSubscribedIsReadyBeforeFirstTelemetrySample()
    {
        var tag = TagDefinition.Create(
            "Temperature",
            "Plant.Area01.Temperature",
            TagDataType.Double,
            source: "mqtt.raw:plant",
            readOnly: true);
        var point = new MqttPoint(tag, "plant/area01/temperature");
        var cache = new CurrentTagCache(new InMemoryScadaEventBus());
        var transport = new IdleConnectedTransport();

        await using var driver = new MqttDriver(
            "mqtt.raw:plant",
            "Plant MQTT",
            new MqttConnectionSettings(
                "broker.local",
                1883,
                UseTls: false,
                ClientId: "elite-readiness-test"),
            cache,
            new InMemoryTagRegistry(),
            [point],
            transport);

        var beforeStart = driver.GetMqttReadiness();
        Assert.Equal(MqttReadinessState.NotStarted, beforeStart.State);
        Assert.False(beforeStart.InitialHandshakeCompleted);
        Assert.Equal(1, beforeStart.ExpectedSubscriptionCount);
        Assert.Equal(0, beforeStart.AcceptedSubscriptionCount);

        await driver.StartAsync();
        await WaitUntilAsync(() => transport.SubscribeCount == 1);
        await WaitUntilAsync(() => driver.GetMqttReadiness().State == MqttReadinessState.Ready);

        var diagnostics = driver.GetCommunicationDiagnostics();
        var readiness = driver.GetMqttReadiness();
        Assert.Equal(CommunicationDriverOperationalState.Healthy, diagnostics.State);
        Assert.Equal(DriverState.Running, driver.Status.State);
        Assert.Equal(MqttReadinessState.Ready, readiness.State);
        Assert.True(readiness.InitialHandshakeCompleted);
        Assert.Equal(1, readiness.ExpectedSubscriptionCount);
        Assert.Equal(1, readiness.AcceptedSubscriptionCount);
        Assert.Null(readiness.Detail);
        Assert.False(cache.TryGet(tag.Id, out _));
        Assert.Equal(0, diagnostics.Counters.Cycles);
        Assert.Null(diagnostics.ConfiguredScanInterval);
        Assert.Null(diagnostics.LastScanDuration);

        await driver.StopAsync();
        Assert.Equal(MqttReadinessState.Stopped, driver.GetMqttReadiness().State);
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

    private sealed class IdleConnectedTransport : IMqttClientTransport
    {
        public bool IsConnected { get; private set; }
        public int SubscribeCount { get; private set; }

        public ValueTask ConnectAsync(
            MqttConnectionSettings settings,
            MqttResolvedCredentials credentials,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IsConnected = true;
            return ValueTask.CompletedTask;
        }

        public ValueTask SubscribeAsync(
            IReadOnlyCollection<MqttSubscription> subscriptions,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.NotEmpty(subscriptions);
            SubscribeCount++;
            return ValueTask.CompletedTask;
        }

        public async ValueTask<MqttTransportMessage> ReceiveAsync(CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("Unreachable after cancellation.");
        }

        public ValueTask PublishAsync(MqttPublishRequest request, CancellationToken cancellationToken = default) =>
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
