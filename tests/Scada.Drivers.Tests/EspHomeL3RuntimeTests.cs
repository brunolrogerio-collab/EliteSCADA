using System.Threading.Channels;
using Scada.Core.Commands;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.DriverHost.Runtime;
using Scada.Drivers.Abstractions;
using Scada.Drivers.ESPHome;
using Scada.Drivers.ESPHome.Protocol;
using Scada.Engineering.Contracts;

namespace Scada.Drivers.Tests;

public sealed class EspHomeL3RuntimeTests
{
    [Fact]
    public async Task EngineeringRuntime_RoutesTagAndCanonicalCommand_ThroughAuthoritativeState()
    {
        var tagId = Guid.NewGuid();
        var commandId = Guid.NewGuid();
        var dataSourceId = Guid.NewGuid();
        var peer = new RuntimePeer(autoAcknowledge: true);
        var package = BuildPackage(dataSourceId, tagId, commandId, writeTimeoutMilliseconds: 1000);

        await using var runtime = CreateRuntime(peer);
        var activation = await runtime.ActivateAsync("project-esphome-l3", 1, package);

        Assert.True(
            activation.Activated,
            string.Join(" | ", activation.CompilationIssues.Select(x => x.Message)
                .Concat(activation.RuntimeIssues.Select(x => x.Message))));

        await WaitForAsync(() =>
            runtime.TryGetCurrent(tagId, out var value) &&
            value is not null &&
            value.Quality == TagQuality.Good &&
            value.Value is bool b && !b,
            TimeSpan.FromSeconds(2));

        Assert.Contains(runtime.Tags(), x => x.Id == tagId && x.DataSourceId == dataSourceId);

        await runtime.WriteAsync(tagId, true);
        Assert.Equal(1, peer.CommandCount);
        Assert.True(runtime.TryGetCurrent(tagId, out var afterWrite));
        Assert.True(Assert.IsType<bool>(afterWrite!.Value));
        Assert.Equal(TagQuality.Good, afterWrite.Quality);

        await runtime.ExecuteCommandAsync(commandId);
        Assert.Equal(2, peer.CommandCount);
        Assert.True(runtime.TryGetCurrent(tagId, out var afterCommand));
        Assert.False(Assert.IsType<bool>(afterCommand!.Value));
        Assert.Equal(TagQuality.Good, afterCommand.Quality);
    }

    [Fact]
    public async Task WriteAcceptanceWithoutState_DoesNotBecomeProcessTruth()
    {
        var tagId = Guid.NewGuid();
        var dataSourceId = Guid.NewGuid();
        var peer = new RuntimePeer(autoAcknowledge: false);
        var package = BuildPackage(dataSourceId, tagId, null, writeTimeoutMilliseconds: 150);

        await using var runtime = CreateRuntime(peer);
        var activation = await runtime.ActivateAsync("project-esphome-timeout", 1, package);
        Assert.True(activation.Activated);

        await WaitForAsync(() =>
            runtime.TryGetCurrent(tagId, out var value) &&
            value?.Value is bool b && !b,
            TimeSpan.FromSeconds(2));

        await Assert.ThrowsAsync<TimeoutException>(async () =>
            await runtime.WriteAsync(tagId, true));

        Assert.Equal(1, peer.CommandCount);
        Assert.True(runtime.TryGetCurrent(tagId, out var current));
        Assert.False(Assert.IsType<bool>(current!.Value));
        Assert.Equal(TagQuality.Uncertain, current.Quality);
    }

    private static EngineeringRuntimeCoordinator CreateRuntime(RuntimePeer peer)
    {
        var components = new CommunicationDriverRuntimeComponentRegistry();
        components.Register(new CommunicationDriverRuntimeComponentRegistration(
            new EspHomeCommunicationRuntimePlanner(),
            new EspHomeCommunicationRuntimeFactory((_, _) => peer),
            new EspHomeDriverDescriptorProvider().Descriptor));

        return new EngineeringRuntimeCoordinator(
            new InMemoryScadaEventBus(),
            new EngineeringDriverCompiler(components),
            TimeSpan.FromSeconds(3),
            communicationComponents: components);
    }

    private static EngineeringPackage BuildPackage(
        Guid dataSourceId,
        Guid tagId,
        Guid? commandId,
        int writeTimeoutMilliseconds)
    {
        var dataSource = new DataSourceEngineeringDto(
            dataSourceId,
            "esphome.node",
            "ESPHome Node",
            EspHomeNativeContract.DriverType,
            Settings: new Dictionary<string, string>
            {
                ["host"] = "127.0.0.1",
                ["port"] = "6053",
                ["encryptionMode"] = "plaintext",
                ["writeReconcileTimeoutMilliseconds"] = writeTimeoutMilliseconds.ToString()
            });

        var address = new EspHomeEntityAddress(EspHomeEntityKind.Switch, 0, 0x01020304, "state");
        var binding = new CommunicationTagBinding(
            CommunicationTagBinding.CurrentContractVersion,
            EspHomeNativeContract.SchemaId,
            EspHomeNativeContract.SchemaVersion,
            address.ToString(),
            new Dictionary<string, string>
            {
                ["writeKind"] = nameof(EspHomeWriteKind.SwitchState)
            });
        var tag = new TagEngineeringDto(
            tagId,
            "Relay",
            "Home.ESPHome.Relay",
            TagDataType.Boolean,
            Source: dataSource.Key,
            Address: binding.PortableAddress,
            ReadOnly: false,
            CommunicationBinding: binding,
            DataSourceId: dataSourceId);

        IReadOnlyCollection<CommandEngineeringDto>? commands = commandId.HasValue
            ?
            [
                new CommandEngineeringDto(
                    commandId,
                    "home.esphome.off",
                    "Turn ESPHome relay off",
                    CommandKind.WriteTagValue,
                    "false",
                    TargetTagId: tagId,
                    TargetTagPath: tag.Path)
            ]
            : null;

        return new EngineeringPackage(
            "scada.engineering",
            15,
            DateTimeOffset.UtcNow,
            [tag],
            Array.Empty<AlarmEngineeringDto>(),
            [dataSource],
            Commands: commands);
    }

    private static async Task WaitForAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (!condition())
        {
            if (DateTimeOffset.UtcNow >= deadline)
                throw new TimeoutException("Condition was not reached.");
            await Task.Delay(10);
        }
    }

    private sealed class RuntimePeer(bool autoAcknowledge) : IEspHomeNativeClient
    {
        private readonly Channel<EspHomeStateUpdate> _states = Channel.CreateUnbounded<EspHomeStateUpdate>();
        private readonly EspHomeEntityAddress _address =
            new(EspHomeEntityKind.Switch, 0, 0x01020304, "state");

        public int CommandCount { get; private set; }
        public bool Connected { get; private set; }

        public ValueTask<EspHomeNativeInventory> ConnectAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Connected = true;
            return ValueTask.FromResult(new EspHomeNativeInventory(
                new EspHomeApiVersion(1, 15),
                new EspHomeDeviceIdentity(
                    "AA:BB:CC:DD:EE:FF",
                    "AA:BB:CC:DD:EE:FF",
                    "node",
                    "Node",
                    "2026.9.0",
                    "2026-09-30",
                    "Espressif",
                    "ESP32",
                    "fixture",
                    "1",
                    false),
                [new EspHomeEntityDescriptor(EspHomeEntityKind.Switch, 0x01020304, 0, "relay", "Relay", 0)]));
        }

        public ValueTask SubscribeStatesAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _states.Writer.TryWrite(new EspHomeStateUpdate(_address, false));
            return ValueTask.CompletedTask;
        }

        public async ValueTask<EspHomeStateUpdate> ReceiveStateAsync(CancellationToken cancellationToken = default) =>
            await _states.Reader.ReadAsync(cancellationToken);

        public ValueTask SendCommandAsync(EspHomeCommand command, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Equal(_address, command.Address);
            Assert.Equal(EspHomeWriteKind.SwitchState, command.Kind);
            CommandCount++;
            if (autoAcknowledge)
                _states.Writer.TryWrite(new EspHomeStateUpdate(_address, Convert.ToBoolean(command.Value)));
            return ValueTask.CompletedTask;
        }

        public ValueTask DisconnectAsync(CancellationToken cancellationToken = default)
        {
            Connected = false;
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            _states.Writer.TryComplete();
            return ValueTask.CompletedTask;
        }
    }
}
