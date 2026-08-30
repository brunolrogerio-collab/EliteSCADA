using Scada.Core.Tags;
using Scada.Drivers.Abstractions;
using Scada.Drivers.Mqtt;

namespace Scada.Drivers.Tests;

public sealed class MqttWorkerSupervisionTests
{
    [Fact]
    public async Task TerminalReceiveFaultCancelsFreshnessWorker()
    {
        const string topic = "plant/supervision/receive";
        var tag = CreateTag("Receive");
        var point = new MqttPoint(
            tag,
            topic,
            FreshnessTimeout: TimeSpan.FromMilliseconds(60));
        var cache = new SupervisionTagCache();
        var transport = new SampleThenPermanentFailureTransport(topic);
        await using var driver = CreateDriver(point, cache, transport);

        await driver.StartAsync();
        await WaitUntilAsync(() => driver.Status.State == DriverState.Faulted);

        // Give the receive supervisor enough time to observe the terminal return
        // and cancel the sibling freshness worker before taking the baseline.
        await Task.Delay(150);
        var tryGetCountAfterCancellation = cache.TryGetCount;
        await Task.Delay(150);

        Assert.Equal(tryGetCountAfterCancellation, cache.TryGetCount);
        Assert.False(transport.IsConnected);
        Assert.Equal(MqttReadinessState.Faulted, driver.GetMqttReadiness().State);
    }

    [Fact]
    public async Task FreshnessFaultCancelsReceiveDisconnectsTransportAndRemainsStoppable()
    {
        const string topic = "plant/supervision/freshness";
        var tag = CreateTag("Freshness");
        var point = new MqttPoint(
            tag,
            topic,
            FreshnessTimeout: TimeSpan.FromMilliseconds(60));
        var cache = new SupervisionTagCache(throwOnStaleUpdate: true);
        var transport = new SampleThenBlockingTransport(topic);
        await using var driver = CreateDriver(point, cache, transport);

        await driver.StartAsync();
        await WaitUntilAsync(() =>
            driver.Status.State == DriverState.Faulted &&
            transport.ReceiveCancellationObserved &&
            transport.DisconnectCount == 1);

        Assert.False(transport.IsConnected);
        Assert.Equal(MqttReadinessState.Faulted, driver.GetMqttReadiness().State);
        var message = Assert.IsType<string>(driver.Status.Message);
        Assert.Contains("Injected freshness cache failure", message, StringComparison.Ordinal);

        await driver.StopAsync();

        Assert.Equal(DriverState.Stopped, driver.Status.State);
        Assert.Equal(MqttReadinessState.Stopped, driver.GetMqttReadiness().State);
    }

    private static MqttDriver CreateDriver(
        MqttPoint point,
        ICurrentTagCache cache,
        IMqttClientTransport transport) => new(
            "mqtt.raw:worker-supervision",
            "MQTT worker supervision test",
            new MqttConnectionSettings(
                "broker.local",
                1883,
                UseTls: false,
                ClientId: $"elite-supervision-{Guid.NewGuid():N}",
                ReconnectMinimumDelay: TimeSpan.FromMilliseconds(5),
                ReconnectMaximumDelay: TimeSpan.FromMilliseconds(10)),
            cache,
            new InMemoryTagRegistry(),
            [point],
            transport);

    private static TagDefinition CreateTag(string suffix) => new(
        Guid.NewGuid(),
        "Value",
        $"Plant.Supervision.{suffix}.{Guid.NewGuid():N}",
        TagDataType.Double,
        "mqtt.raw:worker-supervision",
        null,
        null,
        true);

    private static async Task WaitUntilAsync(Func<bool> predicate)
    {
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(3);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (predicate()) return;
            await Task.Delay(10);
        }

        Assert.True(predicate(), "Condition did not become true before the test timeout.");
    }

    private sealed class SupervisionTagCache(bool throwOnStaleUpdate = false) : ICurrentTagCache
    {
        private readonly object _gate = new();
        private readonly Dictionary<Guid, TagValue> _values = new();
        private long _tryGetCount;

        public long TryGetCount => Interlocked.Read(ref _tryGetCount);

        public bool TryGet(Guid tagId, out TagValue? value)
        {
            Interlocked.Increment(ref _tryGetCount);
            lock (_gate)
            {
                var found = _values.TryGetValue(tagId, out var stored);
                value = stored;
                return found;
            }
        }

        public IReadOnlyCollection<TagValue> Snapshot()
        {
            lock (_gate) return _values.Values.ToArray();
        }

        public ValueTask<TagValue?> UpdateAsync(
            TagDefinition tag,
            TagValue value,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (throwOnStaleUpdate && value.Quality == TagQuality.Stale)
                throw new InvalidOperationException("Injected freshness cache failure.");

            lock (_gate)
            {
                _values.TryGetValue(tag.Id, out var previous);
                _values[tag.Id] = value;
                return ValueTask.FromResult<TagValue?>(previous);
            }
        }
    }

    private sealed class SampleThenPermanentFailureTransport(string topic) : IMqttClientTransport
    {
        private int _receiveCount;

        public bool IsConnected { get; private set; }

        public ValueTask ConnectAsync(
            MqttConnectionSettings settings,
            MqttResolvedCredentials credentials,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            settings.Validate();
            _ = credentials;
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
            if (Interlocked.Increment(ref _receiveCount) == 1)
            {
                return ValueTask.FromResult(new MqttTransportMessage(
                    topic,
                    "12.5"u8.ToArray(),
                    Retained: false,
                    MqttQosLevel.AtLeastOnce,
                    DateTimeOffset.UtcNow));
            }

            throw new MqttTransportException("Injected permanent receive failure.", isPermanent: true);
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

    private sealed class SampleThenBlockingTransport(string topic) : IMqttClientTransport
    {
        private int _receiveCount;
        private int _receiveCancellationObserved;
        private int _disconnectCount;

        public bool IsConnected { get; private set; }
        public bool ReceiveCancellationObserved => Volatile.Read(ref _receiveCancellationObserved) != 0;
        public int DisconnectCount => Volatile.Read(ref _disconnectCount);

        public ValueTask ConnectAsync(
            MqttConnectionSettings settings,
            MqttResolvedCredentials credentials,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            settings.Validate();
            _ = credentials;
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

        public async ValueTask<MqttTransportMessage> ReceiveAsync(CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _receiveCount) == 1)
            {
                return new MqttTransportMessage(
                    topic,
                    "12.5"u8.ToArray(),
                    Retained: false,
                    MqttQosLevel.AtLeastOnce,
                    DateTimeOffset.UtcNow);
            }

            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                throw new InvalidOperationException("Infinite receive delay completed unexpectedly.");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                Volatile.Write(ref _receiveCancellationObserved, 1);
                throw;
            }
        }

        public ValueTask PublishAsync(
            MqttPublishRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask DisconnectAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref _disconnectCount);
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
