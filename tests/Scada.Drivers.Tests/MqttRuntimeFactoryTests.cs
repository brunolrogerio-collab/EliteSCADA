using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.DriverHost.Runtime;
using Scada.Drivers.Mqtt;

namespace Scada.Drivers.Tests;

public sealed class MqttRuntimeFactoryTests
{
    [Fact]
    public async Task Create_ComposesUsernameOnlyPlanWithoutSecretStore()
    {
        var transport = new CaptureTransport();
        var factory = new MqttRuntimeFactory(() => transport);
        var plan = CreatePlan(username: "operator");
        var cache = new CurrentTagCache(new InMemoryScadaEventBus());

        await using var driver = factory.Create(plan, cache, new InMemoryTagRegistry());
        await driver.StartAsync();
        await WaitUntilAsync(() => transport.ConnectCount == 1 && transport.SubscribeCount == 1);

        Assert.Equal("operator", transport.LastUsername);
        Assert.Equal(0, transport.LastPasswordLength);
        Assert.Equal(plan.DriverId, driver.DriverId);
    }

    [Fact]
    public void Create_FailsClosedWhenProtectedCredentialHasNoHostResolver()
    {
        var factory = new MqttRuntimeFactory(() => new CaptureTransport());
        var plan = CreatePlan(username: "operator", passwordSecretReference: "secret://mqtt/operator");
        var cache = new CurrentTagCache(new InMemoryScadaEventBus());

        var error = Assert.Throws<InvalidOperationException>(() =>
            factory.Create(plan, cache, new InMemoryTagRegistry()));

        Assert.Contains("secret resolver", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_UsesHostCredentialResolverWithoutPersistingSecretInPlan()
    {
        var transport = new CaptureTransport();
        var resolverCalls = 0;
        var factory = new MqttRuntimeFactory(
            () => transport,
            (plan, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                resolverCalls++;
                Assert.Equal("secret://mqtt/operator", plan.PasswordSecretReference);
                return ValueTask.FromResult(new MqttResolvedCredentials(
                    plan.Username,
                    "ephemeral-test-secret"u8.ToArray()));
            });
        var runtimePlan = CreatePlan(
            username: "operator",
            passwordSecretReference: "secret://mqtt/operator");
        var cache = new CurrentTagCache(new InMemoryScadaEventBus());

        await using var driver = factory.Create(runtimePlan, cache, new InMemoryTagRegistry());
        await driver.StartAsync();
        await WaitUntilAsync(() => transport.ConnectCount == 1 && transport.SubscribeCount == 1);

        Assert.Equal(1, resolverCalls);
        Assert.Equal("operator", transport.LastUsername);
        Assert.True(transport.LastPasswordLength > 0);
        Assert.Equal("secret://mqtt/operator", runtimePlan.PasswordSecretReference);
    }

    private static MqttRuntimePlan CreatePlan(
        string? username = null,
        string? passwordSecretReference = null)
    {
        var tag = TagDefinition.Create(
            "Value",
            $"Plant.Value.{Guid.NewGuid():N}",
            TagDataType.Double,
            source: "mqtt.raw:plant",
            readOnly: true);

        return new MqttRuntimePlan(
            "mqtt.plant",
            "mqtt.raw:mqtt.plant",
            "Plant MQTT",
            new MqttConnectionSettings(
                "broker.local",
                1883,
                UseTls: false,
                ClientId: "elite-runtime-factory-test"),
            username,
            passwordSecretReference,
            [new MqttPoint(tag, "plant/value")]);
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

    private sealed class CaptureTransport : IMqttClientTransport
    {
        public bool IsConnected { get; private set; }
        public int ConnectCount { get; private set; }
        public int SubscribeCount { get; private set; }
        public string? LastUsername { get; private set; }
        public int LastPasswordLength { get; private set; }

        public ValueTask ConnectAsync(
            MqttConnectionSettings settings,
            MqttResolvedCredentials credentials,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _ = settings;
            ConnectCount++;
            LastUsername = credentials.Username;
            LastPasswordLength = credentials.Password.Length;
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
