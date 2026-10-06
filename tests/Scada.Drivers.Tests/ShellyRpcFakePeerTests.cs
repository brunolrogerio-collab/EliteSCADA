using System.Text.Json;
using System.Threading.Channels;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Drivers.Shelly;

namespace Scada.Drivers.Tests;

public sealed class ShellyRpcFakePeerTests
{
    [Fact]
    public async Task Runtime_UsesDeviceReadback_ThenMergesPartialNotifications_AndReconnects()
    {
        var outputTag = TagDefinition.Create(
            "Output",
            "Home.Pump.Output",
            TagDataType.Boolean,
            source: "shelly.pump",
            readOnly: false);
        var powerTag = TagDefinition.Create(
            "Power",
            "Home.Pump.Power",
            TagDataType.Double,
            source: "shelly.pump",
            engineeringUnit: "W",
            readOnly: true);
        var points = new[]
        {
            new ShellyPoint(outputTag, "switch:0", "output", "Switch.Set", "on"),
            new ShellyPoint(powerTag, "switch:0", "apower")
        };

        var peer = new FakeShellyRpcClient();
        var cache = new CurrentTagCache(new InMemoryScadaEventBus());
        await using var driver = new ShellyDriver(
            "shelly.pump",
            "Pump Shelly",
            new ShellyConnectionSettings("127.0.0.1"),
            cache,
            points,
            peer,
            _ => ValueTask.FromResult(new ShellyResolvedCredential(ReadOnlyMemory<byte>.Empty)));

        await driver.StartAsync();
        Assert.True(cache.TryGet(outputTag.Id, out var initial));
        Assert.False(Assert.IsType<bool>(initial!.Value));
        Assert.Equal(TagQuality.Good, initial.Quality);

        await driver.WriteAsync(outputTag.Id, true);

        Assert.Contains("Switch.Set", peer.Calls);
        Assert.True(peer.GetStatusCalls >= 2);
        Assert.True(cache.TryGet(outputTag.Id, out var afterWrite));
        Assert.True(Assert.IsType<bool>(afterWrite!.Value));

        peer.EnqueueNotification("NotifyStatus", """
            {"ts":123.5,"switch:0":{"apower":17.25}}
            """);
        await WaitUntilAsync(() =>
            cache.TryGet(powerTag.Id, out var power) &&
            power?.Value is double value &&
            Math.Abs(value - 17.25d) < 0.001);

        Assert.True(cache.TryGet(outputTag.Id, out var afterPartial));
        Assert.True(Assert.IsType<bool>(afterPartial!.Value));

        peer.EnqueueNotification("NotifyEvent", """
            {"ts":124.0,"events":[{"component":"input:0","id":0,"event":"single_push","ts":124.0}]}
            """);
        await WaitUntilAsync(() =>
            driver.GetCommunicationDiagnostics().ProtocolDetails?["transientEventsObserved"] == "1");

        var reconciliationsBeforeDisconnect = peer.GetStatusCalls;
        peer.EnqueueFailure(new IOException("peer reset"));
        await WaitUntilAsync(() => peer.ConnectCount >= 2);
        await WaitUntilAsync(() => peer.GetStatusCalls > reconciliationsBeforeDisconnect);

        var diagnostics = driver.GetCommunicationDiagnostics();
        Assert.True(diagnostics.Counters.Reconnects >= 1);
        Assert.Equal("shellyplus1pm-aabbcc", diagnostics.ProtocolDetails!["stableDeviceIdentity"]);
        Assert.True(DateTimeOffset.TryParse(diagnostics.ProtocolDetails["lastFullReconciliation"], out _));

        await driver.StopAsync();
    }

    [Fact]
    public async Task Runtime_DoesNotPublishRequestedValueBeforeReadback()
    {
        var tag = TagDefinition.Create(
            "Output",
            "Home.Pump.Output",
            TagDataType.Boolean,
            source: "shelly.pump",
            readOnly: false);
        var peer = new FakeShellyRpcClient { SuppressWriteEffect = true };
        var cache = new CurrentTagCache(new InMemoryScadaEventBus());
        await using var driver = new ShellyDriver(
            "shelly.pump",
            "Pump Shelly",
            new ShellyConnectionSettings("127.0.0.1"),
            cache,
            [new ShellyPoint(tag, "switch:0", "output", "Switch.Set", "on")],
            peer,
            _ => ValueTask.FromResult(new ShellyResolvedCredential(ReadOnlyMemory<byte>.Empty)));

        await driver.StartAsync();
        await driver.WriteAsync(tag.Id, true);

        Assert.True(cache.TryGet(tag.Id, out var sample));
        Assert.False(Assert.IsType<bool>(sample!.Value));
        await driver.StopAsync();
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(3);
        while (!condition())
        {
            if (DateTimeOffset.UtcNow >= deadline)
                throw new TimeoutException("Fake Shelly peer condition was not reached.");
            await Task.Delay(20);
        }
    }

    private sealed class FakeShellyRpcClient : IShellyRpcClient
    {
        private readonly Channel<object> _incoming = Channel.CreateUnbounded<object>();
        private bool _output;
        private double _power = 4.5;

        public List<string> Calls { get; } = [];
        public int GetStatusCalls { get; private set; }
        public int ConnectCount { get; private set; }
        public bool SuppressWriteEffect { get; init; }
        public bool WebSocketConnected { get; private set; }

        public ValueTask<JsonElement> CallHttpAsync(
            string method,
            object? parameters,
            ReadOnlyMemory<byte> password,
            bool legacyAuthentication,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls.Add(method);

            if (method == "Shelly.GetDeviceInfo")
            {
                return ValueTask.FromResult(Parse("""
                    {"id":"shellyplus1pm-aabbcc","mac":"AABBCC","model":"SNSW-001P16EU","gen":2,"fw_id":"20261001","ver":"2.0.1","auth_en":false}
                    """));
            }

            if (method == "Shelly.GetComponents")
            {
                return ValueTask.FromResult(Parse("""
                    {"components":[{"key":"switch:0","status":{"id":0}}],"total":1,"offset":0}
                    """));
            }

            if (method == "Shelly.GetStatus")
            {
                GetStatusCalls++;
                return ValueTask.FromResult(JsonSerializer.SerializeToElement(new Dictionary<string, object?>
                {
                    ["switch:0"] = new Dictionary<string, object?>
                    {
                        ["id"] = 0,
                        ["output"] = _output,
                        ["apower"] = _power,
                        ["voltage"] = 230.0,
                        ["current"] = 0.1,
                        ["aenergy"] = new Dictionary<string, object?> { ["total"] = 12.5 }
                    }
                }));
            }

            if (method == "Switch.Set")
            {
                if (!SuppressWriteEffect)
                {
                    var json = JsonSerializer.SerializeToElement(parameters);
                    _output = json.GetProperty("on").GetBoolean();
                }
                return ValueTask.FromResult(Parse("""{"was_on":false}"""));
            }

            throw new InvalidOperationException($"Unexpected fake Shelly RPC method '{method}'.");
        }

        public ValueTask ConnectNotificationsAsync(
            ReadOnlyMemory<byte> password,
            bool legacyAuthentication,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ConnectCount++;
            WebSocketConnected = true;
            return ValueTask.CompletedTask;
        }

        public async ValueTask<ShellyNotification> ReceiveNotificationAsync(
            CancellationToken cancellationToken = default)
        {
            var item = await _incoming.Reader.ReadAsync(cancellationToken);
            if (item is Exception failure) throw failure;
            return (ShellyNotification)item;
        }

        public ValueTask DisconnectNotificationsAsync(CancellationToken cancellationToken = default)
        {
            WebSocketConnected = false;
            return ValueTask.CompletedTask;
        }

        public void EnqueueNotification(string method, string parametersJson)
        {
            _incoming.Writer.TryWrite(new ShellyNotification(method, Parse(parametersJson)));
        }

        public void EnqueueFailure(Exception failure) => _incoming.Writer.TryWrite(failure);

        public ValueTask DisposeAsync()
        {
            _incoming.Writer.TryComplete();
            return ValueTask.CompletedTask;
        }

        private static JsonElement Parse(string json)
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }
    }
}
