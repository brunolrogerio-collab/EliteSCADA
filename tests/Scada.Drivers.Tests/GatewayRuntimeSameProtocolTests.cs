using Scada.Core.Events;
using Scada.Core.InternalMemory;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.DriverHost.Runtime;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;

namespace Scada.Drivers.Tests;

public sealed class GatewayRuntimeSameProtocolTests
{
    [Fact]
    public async Task IndependentModbusDataSources_TransferThroughTagGatewayWithoutDriverCoupling()
    {
        await using var sourceServer = new TestModbusTcpServer();
        await using var destinationServer = new TestModbusTcpServer();
        sourceServer.HoldingRegisters[0] = 42;
        destinationServer.HoldingRegisters[10] = 0;
        sourceServer.Start();
        destinationServer.Start();

        var sourceId = Guid.NewGuid();
        var destinationId = Guid.NewGuid();
        var bus = new InMemoryScadaEventBus();
        await using var runtime = new GatewayEngineeringRuntimeCoordinator(
            new EngineeringRuntimeCoordinator(
                bus,
                new EngineeringDriverCompiler(),
                TimeSpan.FromSeconds(3),
                new InMemoryServerMemoryRetentionStore()),
            bus);

        var package = new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            new[]
            {
                new TagEngineeringDto(
                    sourceId,
                    "Source",
                    "PLC_A.Source",
                    TagDataType.Int16,
                    Source: "plc.a",
                    Address: "holding:0",
                    ReadOnly: true),
                new TagEngineeringDto(
                    destinationId,
                    "Destination",
                    "PLC_B.Destination",
                    TagDataType.Int16,
                    Source: "plc.b",
                    Address: "holding:10",
                    ReadOnly: false)
            },
            Array.Empty<AlarmEngineeringDto>(),
            new[]
            {
                ModbusSource("plc.a", "PLC A", sourceServer.Port),
                ModbusSource("plc.b", "PLC B", destinationServer.Port)
            },
            Gateways: new[]
            {
                new GatewayRouteEngineeringDto(
                    Guid.NewGuid(),
                    "plc-a-to-plc-b",
                    "PLC A to PLC B",
                    sourceId,
                    "PLC_A.Source",
                    destinationId,
                    "PLC_B.Destination",
                    InitialTransferPolicy: GatewayInitialTransferPolicy.SynchronizeFirstAcceptableValue)
            });

        var activation = await runtime.ActivateAsync("gateway-modbus-modbus", 1, package);
        Assert.True(activation.Activated, Describe(activation));

        await WaitForAsync(() => destinationServer.HoldingRegisters[10] == 42, TimeSpan.FromSeconds(4));
        var diagnostic = Assert.Single(runtime.GatewayDiagnostics());
        Assert.Equal("plc.a", diagnostic.SourceDataSource);
        Assert.Equal("plc.b", diagnostic.DestinationDataSource);
        Assert.True(diagnostic.TransferCount >= 1);
        Assert.NotNull(diagnostic.LastSuccessfulTransferAtUtc);
        Assert.Equal(0, diagnostic.WriteFailureCount);

        sourceServer.HoldingRegisters[0] = 55;
        await WaitForAsync(() =>
        {
            var diagnostics = runtime.GatewayDiagnostics();
            return destinationServer.HoldingRegisters[10] == 55
                && diagnostics.Count == 1
                && diagnostics[0].TransferCount >= 2
                && diagnostics[0].WriteFailureCount == 0;
        }, TimeSpan.FromSeconds(4));
    }

    private static DataSourceEngineeringDto ModbusSource(string key, string name, int port) => new(
        Guid.NewGuid(),
        key,
        name,
        EngineeringDriverCompiler.ModbusTcpDriverKey,
        Settings: new Dictionary<string, string>
        {
            ["host"] = "127.0.0.1",
            ["port"] = port.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["scanIntervalMilliseconds"] = "50",
            ["requestTimeoutMilliseconds"] = "300",
            ["unitId"] = "1"
        });

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
