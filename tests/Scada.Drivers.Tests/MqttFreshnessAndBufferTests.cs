using System.Threading.Channels;
using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.Drivers.Mqtt;
using Scada.Engineering.Contracts;

namespace Scada.Drivers.Tests;

public sealed class MqttFreshnessAndBufferTests
{
    [Fact]
    public void PointRejectsNonPositiveFreshnessTimeout()
    {
        var tag = TagDefinition.Create(
            "Value",
            "Plant.Value",
            TagDataType.Double,
            source: "mqtt.raw:test",
            readOnly: true);
        var point = new MqttPoint(
            tag,
            "plant/value",
            FreshnessTimeout: TimeSpan.Zero);

        Assert.Throws<ArgumentOutOfRangeException>(() => point.Validate());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1000001)]
    public void ConnectionRejectsUnsafeBufferCapacity(int capacity)
    {
        var settings = new MqttConnectionSettings(
            "broker.local",
            1883,
            UseTls: false,
            ClientId: "elite-buffer-test",
            MaximumBufferedMessages: capacity);

        Assert.Throws<ArgumentOutOfRangeException>(() => settings.Validate());
    }

    [Fact]
    public void EngineeringCompilerCarriesFreshnessAndBufferPolicies()
    {
        var source = new DataSourceEngineeringDto(
            null,
            "mqtt.plant",
            "Plant MQTT",
            MqttDriverDescriptorProvider.DriverType,
            Settings: new Dictionary<string, string>
            {
                ["host"] = "broker.local",
                ["port"] = "1883",
                ["tls"] = "false",
                ["clientId"] = "elite-plant",
                ["maximumBufferedMessages"] = "17"
            });
        var tag = new TagEngineeringDto(
            Guid.NewGuid(),
            "Temperature",
            "Plant.Temperature",
            TagDataType.Double,
            source.Key,
            "plant/temperature",
            ReadOnly: true,
            Metadata: new Dictionary<string, string>
            {
                ["mqtt.freshnessTimeoutMilliseconds"] = "2500"
            });
        var package = new EngineeringPackage(
            "scada.engineering",
            5,
            DateTimeOffset.UtcNow,
            [tag],
            Array.Empty<AlarmEngineeringDto>(),
            [source]);

        var result = new MqttEngineeringCompiler().Compile(package);

        Assert.True(result.CanActivate);
        var plan = Assert.Single(result.Plans);
        Assert.Equal(17, plan.Connection.MaximumBufferedMessages);
        var point = Assert.Single(plan.Points);
        Assert.Equal(TimeSpan.FromMilliseconds(2500), point.FreshnessTimeout);

        var descriptor = new MqttDriverDescriptorProvider().Descriptor;
        Assert.Single(descriptor.ConfigurationSchema.DataSourceFields, field => field.Key == "maximumBufferedMessages");
        Assert.Single(descriptor.ConfigurationSchema.TagBindingFields, field => field.Key == "mqtt.freshnessTimeoutMilliseconds");
    }

    [Fact]
    public async Task ValidSampleBecomesStaleAndRecoversWhenFreshTelemetryArrives()
    {
        var tag = TagDefinition.Create(
            "Value",
            $"Plant.Value.{Guid.NewGuid():N}",
            TagDataType.Double,
            source: "mqtt.raw:test",
            readOnly: true);
        var point = new MqttPoint(
            tag,
            "plant/value",
            FreshnessTimeout: TimeSpan.FromMilliseconds(60));
        var transport = new QueueTransport();
        var cache = new CurrentTagCache(new InMemoryScadaEventBus());
        await using var driver = CreateDriver(point, transport, cache);

        await driver.StartAsync();
        await WaitUntilAsync(() => transport.IsConnected);
        transport.Push(new MqttTransportMessage(
            point.SubscribeTopic,
            "1.5"u8.ToArray(),
            Retained: false,
            MqttQosLevel.AtLeastOnce,
            DateTimeOffset.UtcNow));

        await WaitUntilAsync(() => cache.TryGet(tag.Id, out var sample) && sample?.Quality == TagQuality.Good);
        await WaitUntilAsync(() => cache.TryGet(tag.Id, out var sample) && sample?.Quality == TagQuality.Stale);

        var diagnostics = driver.GetCommunicationDiagnostics();
        Assert.Equal("1", diagnostics.ProtocolDetails!["freshnessPointCount"]);
        Assert.True(long.Parse(diagnostics.ProtocolDetails["freshnessTransitions"], System.Globalization.CultureInfo.InvariantCulture) >= 1);

        transport.Push(new MqttTransportMessage(
            point.SubscribeTopic,
            "2.5"u8.ToArray(),
            Retained: false,
            MqttQosLevel.AtLeastOnce,
            DateTimeOffset.UtcNow));

        await WaitUntilAsync(() =>
            cache.TryGet(tag.Id, out var sample) &&
            sample?.Quality == TagQuality.Good &&
            Equals(sample.Value, 2.5d));
    }

    [Fact]
    public async Task OldMappedSourceTimestampDoesNotShortCircuitReceiveFreshness()
    {
        var tag = TagDefinition.Create(
            "Value",
            $"Plant.Timestamped.{Guid.NewGuid():N}",
            TagDataType.Double,
            source: "mqtt.raw:test",
            readOnly: true);
        var point = new MqttPoint(
            tag,
            "plant/timestamped",
            PayloadFormat: MqttPayloadFormat.Json,
            JsonPointer: "/value",
            SourceTimestampJsonPointer: "/timestamp",
            SourceTimestampRequired: true,
            FreshnessTimeout: TimeSpan.FromMilliseconds(250));
        var transport = new QueueTransport();
        var cache = new CurrentTagCache(new InMemoryScadaEventBus());
        await using var driver = CreateDriver(point, transport, cache);

        await driver.StartAsync();
        await WaitUntilAsync(() => transport.IsConnected);
        var receivedAt = DateTimeOffset.UtcNow;
        var sourceTimestamp = receivedAt - TimeSpan.FromSeconds(5);
        var payload = System.Text.Encoding.UTF8.GetBytes(
            $"{{\"value\":42.5,\"timestamp\":\"{sourceTimestamp:O}\"}}");
        transport.Push(new MqttTransportMessage(
            point.SubscribeTopic,
            payload,
            Retained: true,
            MqttQosLevel.AtLeastOnce,
            receivedAt));

        await WaitUntilAsync(() => cache.TryGet(tag.Id, out var sample) && sample?.Quality == TagQuality.Good);
        Assert.True(cache.TryGet(tag.Id, out var fresh));
        Assert.Equal(sourceTimestamp.ToUniversalTime(), fresh!.SourceTimestamp);
        Assert.Equal(42.5d, fresh.Value);

        await WaitUntilAsync(() => cache.TryGet(tag.Id, out var sample) && sample?.Quality == TagQuality.Stale);

        Assert.True(cache.TryGet(tag.Id, out var stale));
        Assert.Equal(sourceTimestamp.ToUniversalTime(), stale!.SourceTimestamp);
        Assert.Equal(42.5d, stale.Value);
    }

    private static MqttDriver CreateDriver(
        MqttPoint point,
        IMqttClientTransport transport,
        ICurrentTagCache cache) =>
        new(
            "mqtt.raw:test",
            "MQTT freshness test",
            new MqttConnectionSettings(
                "broker.local",
                1883,
                UseTls: false,
                ClientId: "elite-freshness-test",
                MaximumBufferedMessages: 8),
            cache,
            new InMemoryTagRegistry(),
            [point],
            transport);

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

    private sealed class QueueTransport : IMqttClientTransport
    {
        private readonly Channel<MqttTransportMessage> _messages = Channel.CreateUnbounded<MqttTransportMessage>();

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
            Assert.NotEmpty(subscriptions);
            return ValueTask.CompletedTask;
        }

        public ValueTask<MqttTransportMessage> ReceiveAsync(CancellationToken cancellationToken = default) =>
            _messages.Reader.ReadAsync(cancellationToken);

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
            _messages.Writer.TryComplete();
            return ValueTask.CompletedTask;
        }

        public void Push(MqttTransportMessage message) => _messages.Writer.TryWrite(message);
    }
}
