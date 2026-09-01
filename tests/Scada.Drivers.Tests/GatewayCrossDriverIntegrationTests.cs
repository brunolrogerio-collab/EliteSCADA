using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.DriverHost.Runtime;
using Scada.Drivers.Abstractions;
using Scada.Drivers.SiemensS7Iso;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;

namespace Scada.Drivers.Tests;

public sealed class GatewayCrossDriverIntegrationTests
{
    [Fact]
    public async Task S7SourceTag_GatewayWritesValueToModbusDestinationTag()
    {
        await using var s7Server = new TestS7IsoServer(240);
        s7Server.SetBytes(S7IsoArea.Merker, 0, 0, new byte[] { 0x00, 0x2A });

        await using var modbusServer = new TestModbusTcpServer();
        modbusServer.HoldingRegisters[20] = 0;
        modbusServer.Start();

        var sourceId = Guid.NewGuid();
        var destinationId = Guid.NewGuid();
        var sourceBinding = S7Binding(new S7IsoTagBinding(
            S7IsoTagBinding.CurrentSchemaVersion,
            S7IsoArea.Merker,
            0,
            S7IsoValueType.Int16));

        var package = Package(
            tags:
            [
                new TagEngineeringDto(
                    sourceId,
                    "Source",
                    "S7.Source",
                    TagDataType.Int16,
                    Source: "s7.source",
                    Address: sourceBinding.PortableAddress,
                    ReadOnly: true,
                    CommunicationBinding: sourceBinding),
                new TagEngineeringDto(
                    destinationId,
                    "Destination",
                    "Modbus.Destination",
                    TagDataType.Int16,
                    Source: "modbus.destination",
                    Address: "holding:20",
                    ReadOnly: false)
            ],
            dataSources:
            [
                S7Source("s7.source", s7Server.Port),
                ModbusSource("modbus.destination", modbusServer.Port)
            ],
            gateways:
            [
                new GatewayRouteEngineeringDto(
                    Guid.NewGuid(),
                    "s7-to-modbus",
                    "S7 to Modbus",
                    sourceId,
                    "S7.Source",
                    destinationId,
                    "Modbus.Destination",
                    TransferMode: GatewayTransferMode.OnChange,
                    InitialTransferPolicy: GatewayInitialTransferPolicy.SynchronizeFirstAcceptableValue)
            ]);

        var bus = new InMemoryScadaEventBus();
        var components = CommunicationDriverRuntimeComposition.BuildForCurrentSchema();
        var inner = new EngineeringRuntimeCoordinator(
            bus,
            new EngineeringDriverCompiler(components),
            TimeSpan.FromSeconds(5),
            communicationComponents: components);
        await using var runtime = new GatewayEngineeringRuntimeCoordinator(inner, bus);

        var activation = await runtime.ActivateAsync("gateway-cross-driver", 1, package);
        Assert.True(activation.Activated, Describe(activation));

        await WaitForAsync(
            () => runtime.TryGetCurrent(sourceId, out var current) &&
                  current?.Quality == TagQuality.Good &&
                  Convert.ToInt16(current.Value, System.Globalization.CultureInfo.InvariantCulture) == 42,
            TimeSpan.FromSeconds(4));
        await WaitForAsync(
            () => modbusServer.HoldingRegisters[20] == 42,
            TimeSpan.FromSeconds(4));

        var initialGateway = Assert.Single(runtime.GatewayDiagnostics());
        Assert.Equal(GatewayRouteRuntimeState.Running, initialGateway.State);
        Assert.True(initialGateway.TransferCount >= 1);

        var driverDiagnostics = runtime.Describe().CommunicationDrivers;
        Assert.Contains(driverDiagnostics, x => x.DriverType == S7IsoCommunicationRuntimePlan.DriverTypeKey);
        Assert.Contains(driverDiagnostics, x => x.DriverType == EngineeringDriverCompiler.ModbusTcpDriverKey);

        s7Server.SetBytes(S7IsoArea.Merker, 0, 0, new byte[] { 0x00, 0x63 });

        await WaitForAsync(
            () => runtime.TryGetCurrent(sourceId, out var current) &&
                  current?.Quality == TagQuality.Good &&
                  Convert.ToInt16(current.Value, System.Globalization.CultureInfo.InvariantCulture) == 99,
            TimeSpan.FromSeconds(4));
        await WaitForAsync(
            () => modbusServer.HoldingRegisters[20] == 99,
            TimeSpan.FromSeconds(4));
        await WaitForAsync(
            () =>
            {
                var diagnostics = runtime.GatewayDiagnostics();
                if (diagnostics.Count != 1)
                {
                    return false;
                }

                var gateway = diagnostics.Single();
                return gateway.State == GatewayRouteRuntimeState.Running &&
                       gateway.TransferCount >= 2 &&
                       gateway.WriteFailureCount == 0;
            },
            TimeSpan.FromSeconds(4));

        var updatedGateway = Assert.Single(runtime.GatewayDiagnostics());
        Assert.Equal(GatewayRouteRuntimeState.Running, updatedGateway.State);
        Assert.True(updatedGateway.TransferCount >= 2);
        Assert.Equal(0, updatedGateway.WriteFailureCount);
    }

    private static EngineeringPackage Package(
        IReadOnlyCollection<TagEngineeringDto> tags,
        IReadOnlyCollection<DataSourceEngineeringDto> dataSources,
        IReadOnlyCollection<GatewayRouteEngineeringDto> gateways) => new(
        EngineeringExchangeService.CurrentSchema,
        EngineeringExchangeService.CurrentSchemaVersion,
        DateTimeOffset.UtcNow,
        tags,
        Array.Empty<AlarmEngineeringDto>(),
        dataSources,
        Gateways: gateways);

    private static DataSourceEngineeringDto S7Source(string key, int port) => new(
        Guid.NewGuid(),
        key,
        "Siemens S7 source",
        S7IsoCommunicationRuntimePlan.DriverTypeKey,
        Settings: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["host"] = "127.0.0.1",
            ["port"] = port.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["cpuFamily"] = nameof(S7CpuFamily.S71500),
            ["connectionMode"] = nameof(S7IsoConnectionMode.RackSlot),
            ["rack"] = "0",
            ["slot"] = "1",
            ["connectionRole"] = nameof(S7IsoConnectionRole.Basic),
            ["writeEnabled"] = "false",
            ["sourceTsap"] = "0x0100",
            ["connectTimeoutMs"] = "1000",
            ["requestTimeoutMs"] = "1000",
            ["reconnectDelayMs"] = "50",
            ["requestedPduSize"] = "480"
        });

    private static DataSourceEngineeringDto ModbusSource(string key, int port) => new(
        Guid.NewGuid(),
        key,
        "Modbus destination",
        EngineeringDriverCompiler.ModbusTcpDriverKey,
        Settings: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["host"] = "127.0.0.1",
            ["port"] = port.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["scanIntervalMilliseconds"] = "50",
            ["requestTimeoutMilliseconds"] = "500",
            ["unitId"] = "1"
        });

    private static CommunicationTagBinding S7Binding(S7IsoTagBinding binding)
    {
        var transform = S7IsoCommunicationBindingProjection.GetPhysicalValueTransform(binding);
        return new CommunicationTagBinding(
            CommunicationTagBinding.CurrentContractVersion,
            S7IsoCommunicationBindingProjection.SchemaId,
            S7IsoCommunicationBindingProjection.SchemaVersion,
            S7IsoCommunicationBindingProjection.ToCanonicalPortableAddress(binding),
            S7IsoCommunicationBindingProjection.ToCanonicalSettings(binding),
            new TagPhysicalValueTransform(
                ByteSwap: transform.ByteSwap,
                WordSwap: transform.WordSwap));
    }

    private static async Task WaitForAsync(Func<bool> predicate, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (predicate()) return;
            await Task.Delay(20);
        }

        Assert.True(predicate(), $"Condition was not met within {timeout}.");
    }

    private static string Describe(RuntimeActivationResult result) =>
        string.Join(" | ",
            result.CompilationIssues.Select(issue => $"{issue.Code}: {issue.Message}")
                .Concat(result.RuntimeIssues.Select(issue => $"{issue.Code}: {issue.Message}")));
}
