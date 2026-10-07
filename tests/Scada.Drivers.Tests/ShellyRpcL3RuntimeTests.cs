using System.Text.Json;
using System.Threading.Channels;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.DriverHost.Runtime;
using Scada.Drivers.Abstractions;
using Scada.Drivers.Shelly;
using Scada.Engineering.Contracts;

namespace Scada.Drivers.Tests;

public sealed class ShellyRpcL3RuntimeTests
{
    [Fact]
    public async Task EngineeringRuntime_ActivatesShelly_AndRoutesCanonicalWriteThroughDriver()
    {
        var dataSourceId = Guid.NewGuid();
        var tagId = Guid.NewGuid();
        var dataSource = new DataSourceEngineeringDto(
            dataSourceId,
            "shelly.pump",
            "Pump Shelly",
            ShellyRpcContract.DriverType,
            Settings: new Dictionary<string, string>
            {
                ["host"] = "127.0.0.1",
                ["port"] = "80",
                ["tls"] = "false"
            });
        var binding = new CommunicationTagBinding(
            CommunicationTagBinding.CurrentContractVersion,
            ShellyRpcContract.SchemaId,
            ShellyRpcContract.SchemaVersion,
            "switch:0.output",
            new Dictionary<string, string>
            {
                ["writeMethod"] = "Switch.Set",
                ["writeParameter"] = "on"
            });
        var tag = new TagEngineeringDto(
            tagId,
            "Output",
            "Home.Pump.Output",
            TagDataType.Boolean,
            Source: dataSource.Key,
            Address: binding.PortableAddress,
            ReadOnly: false,
            CommunicationBinding: binding,
            DataSourceId: dataSourceId);
        var package = new EngineeringPackage(
            "scada.engineering",
            15,
            DateTimeOffset.UtcNow,
            [tag],
            Array.Empty<AlarmEngineeringDto>(),
            [dataSource]);

        var peer = new RuntimePeer();
        var components = new CommunicationDriverRuntimeComponentRegistry();
        components.Register(new CommunicationDriverRuntimeComponentRegistration(
            new ShellyCommunicationRuntimePlanner(),
            new ShellyCommunicationRuntimeFactory((_, _) => peer),
            new ShellyDriverDescriptorProvider().Descriptor));

        await using var runtime = new EngineeringRuntimeCoordinator(
            new InMemoryScadaEventBus(),
            new EngineeringDriverCompiler(components),
            TimeSpan.FromSeconds(3),
            communicationComponents: components);

        var activation = await runtime.ActivateAsync("project-shelly-l3", 1, package);

        Assert.True(
            activation.Activated,
            string.Join(" | ", activation.CompilationIssues.Select(x => x.Message)
                .Concat(activation.RuntimeIssues.Select(x => x.Message))));
        Assert.True(runtime.TryGetCurrent(tagId, out var initial));
        Assert.False(Assert.IsType<bool>(initial!.Value));
        Assert.Contains(runtime.Tags(), x => x.Id == tagId && x.DataSourceId == dataSourceId);

        await runtime.WriteAsync(tagId, true);

        Assert.Equal(1, peer.SwitchSetCalls);
        Assert.True(runtime.TryGetCurrent(tagId, out var current));
        Assert.True(Assert.IsType<bool>(current!.Value));
        Assert.Equal(TagQuality.Good, current.Quality);

        var diagnostics = Assert.Single(runtime.Describe().CommunicationDrivers);
        Assert.Equal(ShellyRpcContract.DriverType, diagnostics.DriverType);
        Assert.Equal("shelly.pump", diagnostics.DataSourceKey);
        Assert.Equal("shellyplus1pm-l3", diagnostics.ProtocolDetails!["stableDeviceIdentity"]);
        Assert.True(diagnostics.Counters.WriteOperations >= 1);
    }

    private sealed class RuntimePeer : IShellyRpcClient
    {
        private readonly Channel<ShellyNotification> _notifications = Channel.CreateUnbounded<ShellyNotification>();
        private bool _output;

        public int SwitchSetCalls { get; private set; }
        public bool WebSocketConnected { get; private set; }

        public ValueTask<JsonElement> CallHttpAsync(
            string method,
            object? parameters,
            ReadOnlyMemory<byte> password,
            bool legacyAuthentication,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return method switch
            {
                "Shelly.GetDeviceInfo" => ValueTask.FromResult(Parse("""
                    {"id":"shellyplus1pm-l3","mac":"001122334455","model":"SNSW-001P16EU","gen":2,"ver":"2.0.1","auth_en":false}
                    """)),
                "Shelly.GetComponents" => ValueTask.FromResult(Parse("""
                    {"components":[{"key":"switch:0"}],"total":1,"offset":0}
                    """)),
                "Shelly.GetStatus" => ValueTask.FromResult(JsonSerializer.SerializeToElement(new Dictionary<string, object?>
                {
                    ["switch:0"] = new { id = 0, output = _output, apower = _output ? 10.0 : 0.0 }
                })),
                "Switch.Set" => HandleSwitchSet(parameters),
                _ => throw new InvalidOperationException($"Unexpected Shelly L3 method '{method}'.")
            };
        }

        private ValueTask<JsonElement> HandleSwitchSet(object? parameters)
        {
            var json = JsonSerializer.SerializeToElement(parameters);
            _output = json.GetProperty("on").GetBoolean();
            SwitchSetCalls++;
            return ValueTask.FromResult(Parse("""{"was_on":false}"""));
        }

        public ValueTask ConnectNotificationsAsync(
            ReadOnlyMemory<byte> password,
            bool legacyAuthentication,
            CancellationToken cancellationToken = default)
        {
            WebSocketConnected = true;
            return ValueTask.CompletedTask;
        }

        public async ValueTask<ShellyNotification> ReceiveNotificationAsync(CancellationToken cancellationToken = default) =>
            await _notifications.Reader.ReadAsync(cancellationToken);

        public ValueTask DisconnectNotificationsAsync(CancellationToken cancellationToken = default)
        {
            WebSocketConnected = false;
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            _notifications.Writer.TryComplete();
            return ValueTask.CompletedTask;
        }

        private static JsonElement Parse(string json)
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }
    }
}
