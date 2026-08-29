using System.Threading.Channels;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;
using Scada.Drivers.Mqtt;

namespace Scada.Drivers.Tests;

public sealed class MqttDriverTests
{
    [Fact]
    public async Task IncomingMessageUpdatesCanonicalCacheWithoutPolling()
    {
        var tag = CreateTag(TagDataType.Double);
        var point = new MqttPoint(tag, "plant/tank/level");
        var transport = new FakeMqttTransport();
        var registry = new InMemoryTagRegistry();
        var cache = new CurrentTagCache(new InMemoryScadaEventBus());
        await using var driver = CreateDriver(cache, registry, transport, point);

        await driver.StartAsync();
        await WaitUntilAsync(() => transport.IsConnected);
        transport.Push(new MqttTransportMessage(
            point.SubscribeTopic,
            "73.25"u8.ToArray(),
            Retained: false,
            MqttQosLevel.AtLeastOnce,
            DateTimeOffset.Parse("2026-08-29T15:00:00Z")));

        await WaitUntilAsync(() => cache.TryGet(tag.Id, out var current) && current?.Quality == TagQuality.Good);

        Assert.True(cache.TryGet(tag.Id, out var value));
        Assert.Equal(73.25d, value!.Value);
        Assert.Equal(DateTimeOffset.Parse("2026-08-29T15:00:00Z"), value.Timestamp);
        Assert.Equal("mqtt.raw:test", value.Source);
        Assert.True(driver.Capabilities.HasFlag(DriverCapabilities.Subscribe));
    }

    [Fact]
    public async Task RetainedValueWithoutSourceTimestampIsPublishedAsStale()
    {
        var tag = CreateTag(TagDataType.Int32);
        var point = new MqttPoint(tag, "plant/counter");
        var transport = new FakeMqttTransport();
        var cache = new CurrentTagCache(new InMemoryScadaEventBus());
        await using var driver = CreateDriver(cache, new InMemoryTagRegistry(), transport, point);

        await driver.StartAsync();
        await WaitUntilAsync(() => transport.IsConnected);
        transport.Push(new MqttTransportMessage(
            point.SubscribeTopic,
            "9"u8.ToArray(),
            Retained: true,
            MqttQosLevel.AtLeastOnce,
            DateTimeOffset.UtcNow));

        await WaitUntilAsync(() => cache.TryGet(tag.Id, out var current) && current?.Quality == TagQuality.Stale);

        Assert.True(cache.TryGet(tag.Id, out var value));
        Assert.Equal(9, value!.Value);
        Assert.Equal(TagQuality.Stale, value.Quality);
    }

    [Fact]
    public async Task MalformedPayloadFailsClosedAndKeepsDriverAlive()
    {
        var tag = CreateTag(TagDataType.Boolean);
        var point = new MqttPoint(tag, "plant/pump/running");
        var transport = new FakeMqttTransport();
        var cache = new CurrentTagCache(new InMemoryScadaEventBus());
        await using var driver = CreateDriver(cache, new InMemoryTagRegistry(), transport, point);

        await driver.StartAsync();
        await WaitUntilAsync(() => transport.IsConnected);
        transport.Push(new MqttTransportMessage(
            point.SubscribeTopic,
            "1"u8.ToArray(),
            Retained: false,
            MqttQosLevel.AtLeastOnce,
            DateTimeOffset.UtcNow));

        await WaitUntilAsync(() => cache.TryGet(tag.Id, out var current) && current?.Quality == TagQuality.Bad);

        Assert.Equal(DriverState.Running, driver.Status.State);
        Assert.Equal(CommunicationDriverOperationalState.Degraded, driver.GetCommunicationDiagnostics().State);
        Assert.Equal(1, driver.GetCommunicationDiagnostics().Counters.ReadOperations);
    }

    [Fact]
    public async Task WritableTagPublishesButDoesNotPretendRemoteProcessAcceptedValue()
    {
        var tag = CreateTag(TagDataType.Int32, readOnly: false);
        var point = new MqttPoint(
            tag,
            "plant/setpoint/readback",
            Writable: true,
            PublishTopic: "plant/setpoint/command",
            PublishQos: MqttQosLevel.ExactlyOnce,
            PublishRetain: false);
        var transport = new FakeMqttTransport();
        var cache = new CurrentTagCache(new InMemoryScadaEventBus());
        await using var driver = CreateDriver(cache, new InMemoryTagRegistry(), transport, point);

        await driver.StartAsync();
        await WaitUntilAsync(() => transport.IsConnected);
        await driver.WriteAsync(tag.Id, 125);

        var request = Assert.Single(transport.Published);
        Assert.Equal("plant/setpoint/command", request.Topic);
        Assert.Equal("125", System.Text.Encoding.UTF8.GetString(request.Payload.Span));
        Assert.Equal(MqttQosLevel.ExactlyOnce, request.Qos);
        Assert.False(cache.TryGet(tag.Id, out _));
    }

    [Fact]
    public async Task TransportFailureReconnectsAndResubscribesDeterministically()
    {
        var tag = CreateTag(TagDataType.Double);
        var point = new MqttPoint(tag, "plant/tank/level");
        var transport = new FakeMqttTransport();
        var settings = new MqttConnectionSettings(
            "broker.local",
            1883,
            UseTls: false,
            ClientId: "elite-test",
            ReconnectMinimumDelay: TimeSpan.FromMilliseconds(5),
            ReconnectMaximumDelay: TimeSpan.FromMilliseconds(10));
        var cache = new CurrentTagCache(new InMemoryScadaEventBus());
        await using var driver = new MqttDriver(
            "mqtt.raw:test",
            "MQTT test",
            settings,
            cache,
            new InMemoryTagRegistry(),
            [point],
            transport);

        await driver.StartAsync();
        await WaitUntilAsync(() => transport.ConnectCount == 1 && transport.SubscribeCount == 1);
        transport.Fail(new MqttTransportException("connection lost"));

        await WaitUntilAsync(() => transport.ConnectCount >= 2 && transport.SubscribeCount >= 2);

        var diagnostics = driver.GetCommunicationDiagnostics();
        Assert.True(diagnostics.Counters.Reconnects >= 1);
        Assert.Equal(0, diagnostics.Counters.Cycles);
        Assert.Null(diagnostics.ConfiguredScanInterval);
        Assert.Null(diagnostics.LastScanDuration);
    }

    private static MqttDriver CreateDriver(
        ICurrentTagCache cache,
        ITagRegistry registry,
        IMqttClientTransport transport,
        params MqttPoint[] points) =>
        new(
            "mqtt.raw:test",
            "MQTT test",
            new MqttConnectionSettings(
                "broker.local",
                1883,
                UseTls: false,
                ClientId: "elite-test"),
            cache,
            registry,
            points,
            transport);

    private static TagDefinition CreateTag(TagDataType dataType, bool readOnly = true) => new(
        Guid.NewGuid(),
        "TestTag",
        $"Plant.TestTag.{Guid.NewGuid():N}",
        dataType,
        "mqtt.raw:test",
        null,
        null,
        readOnly);

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

    private sealed class FakeMqttTransport : IMqttClientTransport
    {
        private readonly Channel<object> _received = Channel.CreateUnbounded<object>();

        public bool IsConnected { get; private set; }
        public int ConnectCount { get; private set; }
        public int SubscribeCount { get; private set; }
        public List<MqttPublishRequest> Published { get; } = [];

        public ValueTask ConnectAsync(
            MqttConnectionSettings settings,
            MqttResolvedCredentials credentials,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ConnectCount++;
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
            var item = await _received.Reader.ReadAsync(cancellationToken);
            if (item is Exception error)
            {
                IsConnected = false;
                throw error;
            }
            return (MqttTransportMessage)item;
        }

        public ValueTask PublishAsync(MqttPublishRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsConnected) throw new MqttTransportException("not connected");
            Published.Add(request);
            return ValueTask.CompletedTask;
        }

        public ValueTask DisconnectAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IsConnected = false;
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            IsConnected = false;
            _received.Writer.TryComplete();
            return ValueTask.CompletedTask;
        }

        public void Push(MqttTransportMessage message) => _received.Writer.TryWrite(message);
        public void Fail(Exception error) => _received.Writer.TryWrite(error);
    }
}
