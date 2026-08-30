using System.Threading.Channels;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;
using Scada.Drivers.Mqtt;

namespace Scada.Drivers.Tests;

public sealed class MqttReadinessEvidenceTests
{
    [Fact]
    public async Task ReadyRemainsLatchedDuringTransientReconnect()
    {
        var tag = CreateTag();
        var point = new MqttPoint(tag, "plant/readiness/value");
        var transport = new BlockingReconnectTransport();
        var cache = new CurrentTagCache(new InMemoryScadaEventBus());
        await using var driver = CreateDriver(cache, transport, point);

        await driver.StartAsync();
        await WaitUntilAsync(() => driver.GetMqttReadiness().State == MqttReadinessState.Ready);

        var ready = driver.GetMqttReadiness();
        Assert.True(ready.InitialHandshakeCompleted);
        Assert.Equal(1, ready.AcceptedSubscriptionCount);

        transport.FailCurrentSession();
        await WaitUntilAsync(() => transport.ConnectCount >= 2);

        var reconnecting = driver.GetCommunicationDiagnostics();
        var duringReconnect = driver.GetMqttReadiness();
        Assert.Equal(CommunicationDriverOperationalState.Reconnecting, reconnecting.State);
        Assert.Equal(MqttReadinessState.Ready, duringReconnect.State);
        Assert.True(duringReconnect.InitialHandshakeCompleted);
        Assert.Equal(1, duringReconnect.AcceptedSubscriptionCount);

        await driver.StopAsync();
        Assert.Equal(MqttReadinessState.Stopped, driver.GetMqttReadiness().State);
    }

    [Fact]
    public async Task ExplicitRestartRequiresMandatoryInitializationAgain()
    {
        var point = new MqttPoint(CreateTag(), "plant/readiness/restart");
        var transport = new BlockingReconnectTransport();
        var cache = new CurrentTagCache(new InMemoryScadaEventBus());
        await using var driver = CreateDriver(cache, transport, point);

        await driver.StartAsync();
        await WaitUntilAsync(() => driver.GetMqttReadiness().State == MqttReadinessState.Ready);
        await driver.StopAsync();

        Assert.Equal(MqttReadinessState.Stopped, driver.GetMqttReadiness().State);

        await driver.StartAsync();
        await WaitUntilAsync(() => transport.ConnectCount >= 2);

        var restarting = driver.GetMqttReadiness();
        Assert.Equal(MqttReadinessState.Starting, restarting.State);
        Assert.False(restarting.InitialHandshakeCompleted);
        Assert.Equal(1, restarting.ExpectedSubscriptionCount);
        Assert.Equal(0, restarting.AcceptedSubscriptionCount);
        Assert.Null(restarting.Detail);

        await driver.StopAsync();
        Assert.Equal(MqttReadinessState.Stopped, driver.GetMqttReadiness().State);
    }

    [Fact]
    public async Task PermanentInitializationFailureFaultsWithoutEverReportingReady()
    {
        var point = new MqttPoint(CreateTag(), "plant/readiness/fault");
        var transport = new PermanentInitializationFailureTransport();
        var cache = new CurrentTagCache(new InMemoryScadaEventBus());
        await using var driver = CreateDriver(cache, transport, point);

        await driver.StartAsync();
        await WaitUntilAsync(() => driver.Status.State == DriverState.Faulted);

        var readiness = driver.GetMqttReadiness();
        Assert.Equal(MqttReadinessState.Faulted, readiness.State);
        Assert.False(readiness.InitialHandshakeCompleted);
        Assert.Equal(1, readiness.ExpectedSubscriptionCount);
        Assert.Equal(0, readiness.AcceptedSubscriptionCount);
        var detail = Assert.IsType<string>(readiness.Detail);
        Assert.Contains("credential", detail, StringComparison.OrdinalIgnoreCase);
    }

    private static MqttDriver CreateDriver(
        ICurrentTagCache cache,
        IMqttClientTransport transport,
        MqttPoint point) =>
        new(
            "mqtt.raw:readiness",
            "MQTT readiness test",
            new MqttConnectionSettings(
                "broker.local",
                1883,
                UseTls: false,
                ClientId: "elite-ready",
                ReconnectMinimumDelay: TimeSpan.FromMilliseconds(5),
                ReconnectMaximumDelay: TimeSpan.FromMilliseconds(10)),
            cache,
            new InMemoryTagRegistry(),
            [point],
            transport);

    private static TagDefinition CreateTag() => new(
        Guid.NewGuid(),
        "Value",
        $"Plant.Readiness.Value.{Guid.NewGuid():N}",
        TagDataType.Double,
        "mqtt.raw:readiness",
        null,
        null,
        true);

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

    private sealed class BlockingReconnectTransport : IMqttClientTransport
    {
        private readonly Channel<Exception> _failures = Channel.CreateUnbounded<Exception>();
        private int _connected;
        private int _connectCount;

        public bool IsConnected => Volatile.Read(ref _connected) != 0;
        public int ConnectCount => Volatile.Read(ref _connectCount);

        public async ValueTask ConnectAsync(
            MqttConnectionSettings settings,
            MqttResolvedCredentials credentials,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            settings.Validate();
            _ = credentials;

            var attempt = Interlocked.Increment(ref _connectCount);
            if (attempt > 1)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return;
            }

            Volatile.Write(ref _connected, 1);
        }

        public ValueTask SubscribeAsync(
            IReadOnlyCollection<MqttSubscription> subscriptions,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.NotEmpty(subscriptions);
            return ValueTask.CompletedTask;
        }

        public async ValueTask<MqttTransportMessage> ReceiveAsync(CancellationToken cancellationToken = default)
        {
            var failure = await _failures.Reader.ReadAsync(cancellationToken);
            throw failure;
        }

        public ValueTask PublishAsync(MqttPublishRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask DisconnectAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Volatile.Write(ref _connected, 0);
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            Volatile.Write(ref _connected, 0);
            _failures.Writer.TryComplete();
            return ValueTask.CompletedTask;
        }

        public void FailCurrentSession()
        {
            Volatile.Write(ref _connected, 0);
            if (!_failures.Writer.TryWrite(new MqttTransportException("transient broker loss")))
                throw new InvalidOperationException("Failure channel is not accepting messages.");
        }
    }

    private sealed class PermanentInitializationFailureTransport : IMqttClientTransport
    {
        public bool IsConnected => false;

        public ValueTask ConnectAsync(
            MqttConnectionSettings settings,
            MqttResolvedCredentials credentials,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new MqttTransportException("credential rejected", isPermanent: true);
        }

        public ValueTask SubscribeAsync(
            IReadOnlyCollection<MqttSubscription> subscriptions,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Subscription must not run after permanent initialization failure.");

        public ValueTask<MqttTransportMessage> ReceiveAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Receive must not run after permanent initialization failure.");

        public ValueTask PublishAsync(MqttPublishRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask DisconnectAsync(CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
