using System.Threading.Channels;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;
using Scada.Drivers.Mqtt;

namespace Scada.Drivers.Tests;

public sealed class MqttLifecycleConcurrencyTests
{
    [Fact]
    public async Task StartupCancellationTokenDoesNotOwnRuntimeLifetime()
    {
        var tag = CreateTag();
        var point = new MqttPoint(tag, "plant/lifecycle/value");
        var transport = new LifecycleTransport();
        var cache = new CurrentTagCache(new InMemoryScadaEventBus());
        await using var driver = CreateDriver(cache, transport, point);
        using var startupCancellation = new CancellationTokenSource();

        await driver.StartAsync(startupCancellation.Token);
        await WaitUntilAsync(() => transport.ConnectCount == 1);

        startupCancellation.Cancel();
        transport.Push(new MqttTransportMessage(
            point.SubscribeTopic,
            "17.5"u8.ToArray(),
            Retained: false,
            MqttQosLevel.AtLeastOnce,
            DateTimeOffset.UtcNow));

        await WaitUntilAsync(() =>
            cache.TryGet(tag.Id, out var sample) &&
            sample?.Quality == TagQuality.Good &&
            Equals(sample.Value, 17.5d));

        Assert.Equal(DriverState.Running, driver.Status.State);
        Assert.True(transport.IsConnected);
    }

    [Fact]
    public async Task ConcurrentStartsCreateOnlyOneRuntimeSession()
    {
        var point = new MqttPoint(CreateTag(), "plant/lifecycle/concurrent-start");
        var transport = new LifecycleTransport();
        var cache = new CurrentTagCache(new InMemoryScadaEventBus());
        await using var driver = CreateDriver(cache, transport, point);
        using var release = new ManualResetEventSlim(false);

        var starts = Enumerable.Range(0, 32)
            .Select(_ => Task.Run(async () =>
            {
                release.Wait();
                await driver.StartAsync();
            }))
            .ToArray();

        release.Set();
        await Task.WhenAll(starts);
        await WaitUntilAsync(() => transport.ConnectCount >= 1);

        Assert.Equal(1, transport.ConnectCount);
    }

    [Fact]
    public async Task ExplicitStopStartIsNotCountedAsTransportReconnect()
    {
        var point = new MqttPoint(CreateTag(), "plant/lifecycle/restart");
        var transport = new LifecycleTransport();
        var cache = new CurrentTagCache(new InMemoryScadaEventBus());
        await using var driver = CreateDriver(cache, transport, point);

        await driver.StartAsync();
        await WaitUntilAsync(() => transport.ConnectCount == 1);
        await driver.StopAsync();

        await driver.StartAsync();
        await WaitUntilAsync(() => transport.ConnectCount == 2);

        var diagnostics = driver.GetCommunicationDiagnostics();
        Assert.Equal(2, diagnostics.Counters.Connections);
        Assert.Equal(0, diagnostics.Counters.Reconnects);
    }

    [Fact]
    public async Task AcceptedStopCompletesCleanupEvenIfCallerTokenIsLaterCanceled()
    {
        var point = new MqttPoint(CreateTag(), "plant/lifecycle/stop-cancel");
        var transport = new LifecycleTransport(blockDisconnect: true);
        var cache = new CurrentTagCache(new InMemoryScadaEventBus());
        await using var driver = CreateDriver(cache, transport, point);
        using var stopCancellation = new CancellationTokenSource();

        await driver.StartAsync();
        await WaitUntilAsync(() => transport.ConnectCount == 1);

        var stopTask = driver.StopAsync(stopCancellation.Token);
        await transport.WaitForDisconnectAsync();
        stopCancellation.Cancel();
        transport.ReleaseDisconnect();

        await stopTask;

        Assert.Equal(DriverState.Stopped, driver.Status.State);
        Assert.False(transport.IsConnected);
        Assert.Equal(CommunicationDriverOperationalState.Stopped, driver.GetCommunicationDiagnostics().State);
    }

    [Fact]
    public async Task FailedStopDisconnectCanBeRetriedWithoutWedgingSessionState()
    {
        var point = new MqttPoint(CreateTag(), "plant/lifecycle/disconnect-retry");
        var transport = new LifecycleTransport(disconnectFailures: 1);
        var cache = new CurrentTagCache(new InMemoryScadaEventBus());
        await using var driver = CreateDriver(cache, transport, point);

        await driver.StartAsync();
        await WaitUntilAsync(() => transport.ConnectCount == 1);

        await Assert.ThrowsAsync<MqttTransportException>(() => driver.StopAsync());

        Assert.Equal(DriverState.Faulted, driver.Status.State);
        Assert.Equal(CommunicationDriverOperationalState.Faulted, driver.GetCommunicationDiagnostics().State);
        Assert.Equal(MqttReadinessState.Faulted, driver.GetMqttReadiness().State);
        Assert.True(transport.IsConnected);
        Assert.Equal(1, transport.DisconnectCount);

        await driver.StopAsync();

        Assert.Equal(DriverState.Stopped, driver.Status.State);
        Assert.Equal(CommunicationDriverOperationalState.Stopped, driver.GetCommunicationDiagnostics().State);
        Assert.Equal(MqttReadinessState.Stopped, driver.GetMqttReadiness().State);
        Assert.False(transport.IsConnected);
        Assert.Equal(2, transport.DisconnectCount);

        await driver.StartAsync();
        await WaitUntilAsync(() => transport.ConnectCount == 2);

        Assert.Equal(DriverState.Running, driver.Status.State);
    }

    [Fact]
    public async Task DisposeStillDisposesTransportWhenCleanDisconnectFails()
    {
        var point = new MqttPoint(CreateTag(), "plant/lifecycle/dispose-failure");
        var transport = new LifecycleTransport(disconnectFailures: 1);
        var cache = new CurrentTagCache(new InMemoryScadaEventBus());
        var driver = CreateDriver(cache, transport, point);

        await driver.StartAsync();
        await WaitUntilAsync(() => transport.ConnectCount == 1);

        await Assert.ThrowsAsync<MqttTransportException>(() => driver.DisposeAsync().AsTask());

        Assert.Equal(1, transport.DisconnectCount);
        Assert.Equal(1, transport.DisposeCount);
        Assert.False(transport.IsConnected);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => driver.StartAsync());

        await driver.DisposeAsync();
        Assert.Equal(1, transport.DisposeCount);
    }

    [Fact]
    public async Task AutomaticRecoveryDisconnectFailureBecomesTerminalFault()
    {
        var point = new MqttPoint(CreateTag(), "plant/lifecycle/recovery-disconnect-failure");
        var transport = new LifecycleTransport(disconnectFailures: 1, failReceiveOnce: true);
        var cache = new CurrentTagCache(new InMemoryScadaEventBus());
        await using var driver = CreateDriver(cache, transport, point);

        await driver.StartAsync();
        await WaitUntilAsync(() => driver.Status.State == DriverState.Faulted);

        Assert.Equal(CommunicationDriverOperationalState.Faulted, driver.GetCommunicationDiagnostics().State);
        Assert.Equal(MqttReadinessState.Faulted, driver.GetMqttReadiness().State);
        Assert.Equal(1, transport.DisconnectCount);
        Assert.True(transport.IsConnected);
    }

    [Fact]
    public async Task DisposedDriverCannotBeStartedAgain()
    {
        var point = new MqttPoint(CreateTag(), "plant/lifecycle/disposed");
        var transport = new LifecycleTransport();
        var cache = new CurrentTagCache(new InMemoryScadaEventBus());
        var driver = CreateDriver(cache, transport, point);

        await driver.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => driver.StartAsync());
        Assert.Throws<ObjectDisposedException>(() => driver.ReadAsync(point.Tag.Id));
    }

    private static MqttDriver CreateDriver(
        ICurrentTagCache cache,
        IMqttClientTransport transport,
        MqttPoint point) =>
        new(
            "mqtt.raw:lifecycle",
            "MQTT lifecycle test",
            new MqttConnectionSettings(
                "broker.local",
                1883,
                UseTls: false,
                ClientId: "elite-lifecycle",
                ReconnectMinimumDelay: TimeSpan.FromMilliseconds(5),
                ReconnectMaximumDelay: TimeSpan.FromMilliseconds(20)),
            cache,
            new InMemoryTagRegistry(),
            [point],
            transport);

    private static TagDefinition CreateTag() => new(
        Guid.NewGuid(),
        "Value",
        $"Plant.Lifecycle.Value.{Guid.NewGuid():N}",
        TagDataType.Double,
        "mqtt.raw:lifecycle",
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

    private sealed class LifecycleTransport : IMqttClientTransport
    {
        private readonly Channel<MqttTransportMessage> _messages = Channel.CreateUnbounded<MqttTransportMessage>();
        private readonly bool _blockDisconnect;
        private readonly bool _failReceiveOnce;
        private readonly TaskCompletionSource<bool> _disconnectEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _disconnectRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _connected;
        private int _connectCount;
        private int _disconnectCount;
        private int _disposeCount;
        private int _disconnectFailuresRemaining;
        private int _receiveFailureIssued;

        public LifecycleTransport(
            bool blockDisconnect = false,
            int disconnectFailures = 0,
            bool failReceiveOnce = false)
        {
            _blockDisconnect = blockDisconnect;
            _disconnectFailuresRemaining = disconnectFailures;
            _failReceiveOnce = failReceiveOnce;
        }

        public bool IsConnected => Volatile.Read(ref _connected) != 0;
        public int ConnectCount => Volatile.Read(ref _connectCount);
        public int DisconnectCount => Volatile.Read(ref _disconnectCount);
        public int DisposeCount => Volatile.Read(ref _disposeCount);

        public ValueTask ConnectAsync(
            MqttConnectionSettings settings,
            MqttResolvedCredentials credentials,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            settings.Validate();
            _ = credentials;
            Interlocked.Increment(ref _connectCount);
            Volatile.Write(ref _connected, 1);
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
            if (_failReceiveOnce && Interlocked.Exchange(ref _receiveFailureIssued, 1) == 0)
            {
                return ValueTask.FromException<MqttTransportMessage>(
                    new IOException("Synthetic MQTT receive failure for lifecycle validation."));
            }

            return _messages.Reader.ReadAsync(cancellationToken);
        }

        public ValueTask PublishAsync(
            MqttPublishRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public async ValueTask DisconnectAsync(CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _disconnectCount);
            if (_blockDisconnect)
            {
                _disconnectEntered.TrySetResult(true);
                await _disconnectRelease.Task.WaitAsync(cancellationToken);
            }

            if (Volatile.Read(ref _disconnectFailuresRemaining) > 0)
            {
                Interlocked.Decrement(ref _disconnectFailuresRemaining);
                throw new MqttTransportException("Synthetic MQTT disconnect failure for lifecycle validation.");
            }

            Volatile.Write(ref _connected, 0);
        }

        public ValueTask DisposeAsync()
        {
            Interlocked.Increment(ref _disposeCount);
            Volatile.Write(ref _connected, 0);
            _messages.Writer.TryComplete();
            _disconnectRelease.TrySetResult(true);
            return ValueTask.CompletedTask;
        }

        public void Push(MqttTransportMessage message)
        {
            if (!_messages.Writer.TryWrite(message))
                throw new InvalidOperationException("Lifecycle test transport is not accepting messages.");
        }

        public Task WaitForDisconnectAsync() => _disconnectEntered.Task;

        public void ReleaseDisconnect() => _disconnectRelease.TrySetResult(true);
    }
}
