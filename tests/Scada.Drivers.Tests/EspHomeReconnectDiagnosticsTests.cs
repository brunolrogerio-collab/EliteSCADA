using System.Threading.Channels;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;
using Scada.Drivers.ESPHome;
using Scada.Drivers.ESPHome.Protocol;

namespace Scada.Drivers.Tests;

public sealed class EspHomeReconnectDiagnosticsTests
{
    private static readonly EspHomeEntityAddress RelayAddress =
        new(EspHomeEntityKind.Switch, 0, 0x01020304, "state");

    [Fact]
    public async Task Disconnect_MarksBadCommunication_ReconnectsAndResubscribes()
    {
        var client = new ScriptedClient([Inventory("AA:BB:CC:DD:EE:FF")]);
        var settings = Settings(
            EspHomeDeepSleepPolicy.Normal,
            reconnectMin: TimeSpan.FromMilliseconds(20),
            reconnectMax: TimeSpan.FromMilliseconds(80));

        var (driver, cache, tag) = CreateDriver(settings, client);
        await using (driver)
        {
            await driver.StartAsync();
            await WaitForAsync(() => CurrentQuality(cache, tag.Id) == TagQuality.Good, TimeSpan.FromSeconds(2));

            client.Drop(new IOException("fixture disconnect"));
            await WaitForAsync(
                () => CurrentQuality(cache, tag.Id) == TagQuality.BadCommunication,
                TimeSpan.FromSeconds(2));

            await WaitForAsync(() =>
            {
                var diagnostics = driver.GetCommunicationDiagnostics();
                return diagnostics.State == CommunicationDriverOperationalState.Healthy
                    && diagnostics.Counters.Reconnects >= 1
                    && client.SubscribeCount >= 2
                    && CurrentQuality(cache, tag.Id) == TagQuality.Good;
            }, TimeSpan.FromSeconds(3));

            var recovered = driver.GetCommunicationDiagnostics();
            Assert.True(recovered.Counters.Connections >= 2);
            Assert.True(recovered.Counters.Disconnections >= 1);
            Assert.True(recovered.Counters.Reconnects >= 1);
            Assert.Equal("false", recovered.ProtocolDetails!["expectedOffline"]);
        }
    }

    [Fact]
    public async Task ExpectedDeepSleep_ActivatesOfflineWithoutPretendingHealthy_AndAvoidsUpdateStorm()
    {
        var client = new ScriptedClient([Inventory("AA:BB:CC:DD:EE:FF", hasDeepSleep: true)])
        {
            AllowConnections = false
        };
        var settings = Settings(
            EspHomeDeepSleepPolicy.Expected,
            reconnectMin: TimeSpan.FromMilliseconds(25),
            reconnectMax: TimeSpan.FromMilliseconds(80));

        var (driver, cache, tag) = CreateDriver(settings, client);
        await using (driver)
        {
            await driver.StartAsync();

            var offline = driver.GetCommunicationDiagnostics();
            Assert.Equal(CommunicationDriverOperationalState.Reconnecting, offline.State);
            Assert.Equal(TagQuality.Unavailable, CurrentQuality(cache, tag.Id));
            Assert.Equal("true", offline.ProtocolDetails!["expectedOffline"]);
            Assert.Equal("expected", offline.ProtocolDetails["deepSleepPolicy"]);
            Assert.Equal("true", offline.ProtocolDetails["deviceReportsDeepSleep"]);
            Assert.Equal(1, offline.Counters.UpdatesPublished);

            await Task.Delay(260);
            var attemptsWhileSleeping = client.ConnectAttempts;
            var stillOffline = driver.GetCommunicationDiagnostics();
            Assert.InRange(attemptsWhileSleeping, 2, 7);
            Assert.Equal(
                1,
                stillOffline.Counters.UpdatesPublished);

            client.AllowConnections = true;
            await WaitForAsync(() =>
            {
                var diagnostics = driver.GetCommunicationDiagnostics();
                return diagnostics.State == CommunicationDriverOperationalState.Healthy
                    && diagnostics.Counters.Reconnects >= 1
                    && CurrentQuality(cache, tag.Id) == TagQuality.Good;
            }, TimeSpan.FromSeconds(3));

            Assert.Equal("false", driver.GetCommunicationDiagnostics().ProtocolDetails!["expectedOffline"]);
        }
    }

    [Fact]
    public void ExpectedDeepSleep_DefaultReconnectCadence_IsDeliberatelySlower()
    {
        var normal = new EspHomeConnectionSettings("127.0.0.1", EncryptionMode: EspHomeNativeEncryptionMode.Plaintext);
        var sleeping = new EspHomeConnectionSettings(
            "127.0.0.1",
            EncryptionMode: EspHomeNativeEncryptionMode.Plaintext,
            DeepSleepPolicy: EspHomeDeepSleepPolicy.Expected);

        Assert.Equal(TimeSpan.FromMilliseconds(500), normal.EffectiveReconnectMinimumDelay);
        Assert.Equal(TimeSpan.FromSeconds(30), normal.EffectiveReconnectMaximumDelay);
        Assert.Equal(TimeSpan.FromSeconds(10), sleeping.EffectiveReconnectMinimumDelay);
        Assert.Equal(TimeSpan.FromMinutes(1), sleeping.EffectiveReconnectMaximumDelay);
    }

    [Fact]
    public async Task InventoryChange_MissingConfiguredEntity_DegradesAndMarksBadDevice()
    {
        var client = new ScriptedClient(
        [
            Inventory("AA:BB:CC:DD:EE:FF"),
            Inventory("AA:BB:CC:DD:EE:FF", includeRelay: false)
        ]);
        var settings = Settings(
            EspHomeDeepSleepPolicy.Normal,
            reconnectMin: TimeSpan.FromMilliseconds(20),
            reconnectMax: TimeSpan.FromMilliseconds(60));

        var (driver, cache, tag) = CreateDriver(settings, client);
        await using (driver)
        {
            await driver.StartAsync();
            await WaitForAsync(() => CurrentQuality(cache, tag.Id) == TagQuality.Good, TimeSpan.FromSeconds(2));

            client.Drop(new IOException("inventory refresh"));
            await WaitForAsync(() =>
            {
                var diagnostics = driver.GetCommunicationDiagnostics();
                return diagnostics.State == CommunicationDriverOperationalState.Degraded
                    && diagnostics.ProtocolDetails?["missingConfiguredEntityCount"] == "1";
            }, TimeSpan.FromSeconds(3));

            Assert.Equal(TagQuality.BadDevice, CurrentQuality(cache, tag.Id));
            Assert.Equal(2, client.SubscribeCount);
            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await driver.WriteAsync(tag.Id, true));
        }
    }

    [Fact]
    public async Task StableIdentityChange_FaultsAndDoesNotResubscribeToDifferentPhysicalDevice()
    {
        var client = new ScriptedClient(
        [
            Inventory("AA:BB:CC:DD:EE:FF"),
            Inventory("11:22:33:44:55:66")
        ]);
        var settings = Settings(
            EspHomeDeepSleepPolicy.Normal,
            reconnectMin: TimeSpan.FromMilliseconds(20),
            reconnectMax: TimeSpan.FromMilliseconds(60));

        var (driver, cache, tag) = CreateDriver(settings, client);
        await using (driver)
        {
            await driver.StartAsync();
            await WaitForAsync(() => CurrentQuality(cache, tag.Id) == TagQuality.Good, TimeSpan.FromSeconds(2));

            client.Drop(new IOException("endpoint moved"));
            await WaitForAsync(
                () => driver.GetCommunicationDiagnostics().State == CommunicationDriverOperationalState.Faulted,
                TimeSpan.FromSeconds(3));

            var diagnostics = driver.GetCommunicationDiagnostics();
            Assert.Equal(TagQuality.BadDevice, CurrentQuality(cache, tag.Id));
            Assert.Contains("identity changed", diagnostics.LastError!, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(1, client.SubscribeCount);
            Assert.Equal("AA:BB:CC:DD:EE:FF", diagnostics.ProtocolDetails!["stableDeviceIdentity"]);
        }
    }

    [Fact]
    public async Task Diagnostics_UseCommonAuthority_AndExposeOnlySanitizedProtocolDetails()
    {
        var client = new ScriptedClient([Inventory("AA:BB:CC:DD:EE:FF", hasDeepSleep: true)]);
        var settings = Settings(EspHomeDeepSleepPolicy.Normal);
        var (driver, cache, tag) = CreateDriver(settings, client);

        await using (driver)
        {
            await driver.StartAsync();
            await WaitForAsync(() => CurrentQuality(cache, tag.Id) == TagQuality.Good, TimeSpan.FromSeconds(2));

            var diagnostics = driver.GetCommunicationDiagnostics();
            Assert.Equal("esphome.node", diagnostics.DataSourceKey);
            Assert.Equal(EspHomeNativeContract.DriverType, diagnostics.DriverType);
            Assert.Equal(CommunicationDriverOperationalState.Healthy, diagnostics.State);
            Assert.Equal(1, diagnostics.AssociatedTagCount);
            Assert.Equal(1, diagnostics.TagQuality.Good);
            Assert.Equal("1.15", diagnostics.ProtocolDetails!["apiVersion"]);
            Assert.Equal("2026.9.0", diagnostics.ProtocolDetails["firmware"]);
            Assert.Equal("AA:BB:CC:DD:EE:FF", diagnostics.ProtocolDetails["stableDeviceIdentity"]);
            Assert.Equal("plaintext", diagnostics.ProtocolDetails["encryptionMode"]);
            Assert.Equal("plaintext-insecure-explicit", diagnostics.ProtocolDetails["transportSecurity"]);
            Assert.Equal("true", diagnostics.ProtocolDetails["deviceReportsDeepSleep"]);
            Assert.DoesNotContain(
                diagnostics.ProtocolDetails.Keys,
                key => key.Contains("password", StringComparison.OrdinalIgnoreCase)
                    || key.Contains("encryptionKey", StringComparison.OrdinalIgnoreCase)
                    || key.Contains("secret", StringComparison.OrdinalIgnoreCase));
        }
    }

    private static EspHomeConnectionSettings Settings(
        EspHomeDeepSleepPolicy deepSleepPolicy,
        TimeSpan? reconnectMin = null,
        TimeSpan? reconnectMax = null) =>
        new(
            "127.0.0.1",
            6053,
            EspHomeNativeEncryptionMode.Plaintext,
            TimeSpan.FromMilliseconds(250),
            TimeSpan.FromMilliseconds(250),
            reconnectMin,
            reconnectMax,
            TimeSpan.FromSeconds(1),
            deepSleepPolicy);

    private static (EspHomeDriver Driver, CurrentTagCache Cache, TagDefinition Tag) CreateDriver(
        EspHomeConnectionSettings settings,
        ScriptedClient client)
    {
        var cache = new CurrentTagCache(new InMemoryScadaEventBus());
        var registry = new InMemoryTagRegistry();
        var tag = TagDefinition.Create(
            "Relay",
            "Home.ESPHome.Relay",
            TagDataType.Boolean,
            source: "esphome.node",
            readOnly: false);
        var point = new EspHomePoint(tag, RelayAddress, EspHomeWriteKind.SwitchState);
        var driver = new EspHomeDriver(
            "esphome.node",
            "ESPHome Node",
            settings,
            cache,
            registry,
            [point],
            client);
        return (driver, cache, tag);
    }

    private static EspHomeNativeInventory Inventory(
        string mac,
        bool hasDeepSleep = false,
        bool includeRelay = true)
    {
        IReadOnlyCollection<EspHomeEntityDescriptor> entities = includeRelay
            ? [new EspHomeEntityDescriptor(EspHomeEntityKind.Switch, 0x01020304, 0, "relay", "Relay", 0)]
            : Array.Empty<EspHomeEntityDescriptor>();

        return new EspHomeNativeInventory(
            new EspHomeApiVersion(1, 15),
            new EspHomeDeviceIdentity(
                mac,
                mac,
                "node",
                "Node",
                "2026.9.0",
                "2026-09-30",
                "Espressif",
                "ESP32",
                "fixture",
                "1",
                hasDeepSleep),
            entities);
    }

    private static TagQuality? CurrentQuality(CurrentTagCache cache, Guid tagId) =>
        cache.TryGet(tagId, out var current) ? current?.Quality : null;

    private static async Task WaitForAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (!condition())
        {
            if (DateTimeOffset.UtcNow >= deadline)
                throw new TimeoutException($"Condition was not reached within {timeout}.");
            await Task.Delay(10);
        }
    }

    private sealed record ClientEvent(EspHomeStateUpdate? State, Exception? Error);

    private sealed class ScriptedClient : IEspHomeNativeClient
    {
        private readonly IReadOnlyList<EspHomeNativeInventory> _inventories;
        private readonly Channel<ClientEvent> _events = Channel.CreateUnbounded<ClientEvent>();
        private int _successfulConnections;
        private EspHomeNativeInventory? _currentInventory;
        private bool _currentState;

        public ScriptedClient(IReadOnlyList<EspHomeNativeInventory> inventories) =>
            _inventories = inventories;

        public bool AllowConnections { get; set; } = true;
        public bool Connected { get; private set; }
        public int ConnectAttempts { get; private set; }
        public int SubscribeCount { get; private set; }
        public int PingCount { get; private set; }
        public int CommandCount { get; private set; }
        public int DisconnectCount { get; private set; }

        public ValueTask<EspHomeNativeInventory> ConnectAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ConnectAttempts++;
            if (!AllowConnections)
                return ValueTask.FromException<EspHomeNativeInventory>(new IOException("fixture offline"));

            var index = Math.Min(_successfulConnections, _inventories.Count - 1);
            _currentInventory = _inventories[index];
            _successfulConnections++;
            Connected = true;
            return ValueTask.FromResult(_currentInventory);
        }

        public ValueTask SubscribeStatesAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Connected)
                return ValueTask.FromException(new IOException("fixture is not connected"));

            SubscribeCount++;
            if (_currentInventory!.Entities.Any(x =>
                x.Kind == EspHomeEntityKind.Switch &&
                x.Key == RelayAddress.Key &&
                x.DeviceId == RelayAddress.DeviceId))
            {
                _events.Writer.TryWrite(new ClientEvent(
                    new EspHomeStateUpdate(RelayAddress, _currentState),
                    null));
            }

            return ValueTask.CompletedTask;
        }

        public async ValueTask<EspHomeStateUpdate> ReceiveStateAsync(CancellationToken cancellationToken = default)
        {
            var item = await _events.Reader.ReadAsync(cancellationToken);
            if (item.Error is not null) throw item.Error;
            return item.State!;
        }

        public ValueTask SendCommandAsync(EspHomeCommand command, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Connected)
                return ValueTask.FromException(new IOException("fixture is not connected"));

            CommandCount++;
            _currentState = Convert.ToBoolean(command.Value);
            _events.Writer.TryWrite(new ClientEvent(
                new EspHomeStateUpdate(RelayAddress, _currentState),
                null));
            return ValueTask.CompletedTask;
        }

        public ValueTask SendPingAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Connected)
                return ValueTask.FromException(new IOException("fixture is not connected"));
            PingCount++;
            return ValueTask.CompletedTask;
        }

        public ValueTask DisconnectAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DisconnectCount++;
            Connected = false;
            return ValueTask.CompletedTask;
        }

        public void Drop(Exception error)
        {
            Connected = false;
            _events.Writer.TryWrite(new ClientEvent(null, error));
        }

        public ValueTask DisposeAsync()
        {
            Connected = false;
            _events.Writer.TryComplete();
            return ValueTask.CompletedTask;
        }
    }
}
