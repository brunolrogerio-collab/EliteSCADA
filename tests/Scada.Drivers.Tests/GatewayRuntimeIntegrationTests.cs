using System.Text.Json;
using Scada.Core.Events;
using Scada.Core.InternalMemory;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.DriverHost.Runtime;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;

namespace Scada.Drivers.Tests;

public sealed class GatewayRuntimeIntegrationTests
{
    [Fact]
    public async Task ModbusSource_TransfersToServerMemory_AndBadQualitySuppressesWrites()
    {
        await using var server = new TestModbusTcpServer();
        server.HoldingRegisters[0] = 123;
        server.Start();

        var sourceId = Guid.NewGuid();
        var destinationId = Guid.NewGuid();
        var bus = new InMemoryScadaEventBus();
        await using var runtime = CreateRuntime(bus);

        var package = Package(
            tags: new[]
            {
                ModbusTag(sourceId, "PLC.Source", "plc.main", "holding:0", readOnly: true),
                MemoryTag(destinationId, "Server.Destination", "memory.server", 0)
            },
            dataSources: new[]
            {
                ModbusSource("plc.main", server.Port),
                ServerMemorySource("memory.server")
            },
            gateways: new[]
            {
                Route(
                    "modbus-to-memory",
                    sourceId,
                    "PLC.Source",
                    destinationId,
                    "Server.Destination",
                    initialTransferPolicy: GatewayInitialTransferPolicy.SynchronizeFirstAcceptableValue)
            });

        var activation = await runtime.ActivateAsync("gateway-modbus-memory", 1, package);
        Assert.True(activation.Activated, Describe(activation));

        await WaitForAsync(
            () => runtime.TryGetCurrent(destinationId, out var current) && Convert.ToInt16(current!.Value) == 123,
            TimeSpan.FromSeconds(4));

        await Task.Delay(250);
        var running = Assert.Single(runtime.GatewayDiagnostics());
        Assert.Equal(GatewayRouteRuntimeState.Running, running.State);
        Assert.Equal(1, running.TransferCount);

        await server.StopAsync();
        await WaitForAsync(
            () => runtime.TryGetCurrent(sourceId, out var current) && current?.Quality == TagQuality.BadCommunication,
            TimeSpan.FromSeconds(4));
        await WaitForAsync(
            () => Assert.Single(runtime.GatewayDiagnostics()).SkippedTransferCount > 0,
            TimeSpan.FromSeconds(2));

        Assert.True(runtime.TryGetCurrent(destinationId, out var destination));
        Assert.Equal((short)123, Convert.ToInt16(destination!.Value));
        Assert.Equal(TagQuality.BadCommunication, GetCurrent(runtime, sourceId).Quality);
    }

    [Fact]
    public async Task ServerMemorySource_WritesModbus_AndRouteRecoversAfterDestinationRejectsWrite()
    {
        await using var server = new TestModbusTcpServer();
        server.HoldingRegisters[10] = 0;
        server.Start();

        var sourceId = Guid.NewGuid();
        var destinationId = Guid.NewGuid();
        var bus = new InMemoryScadaEventBus();
        await using var runtime = CreateRuntime(bus);

        var package = Package(
            tags: new[]
            {
                MemoryTag(sourceId, "Server.Source", "memory.server", 0),
                ModbusTag(destinationId, "PLC.Destination", "plc.main", "holding:10", readOnly: false)
            },
            dataSources: new[]
            {
                ServerMemorySource("memory.server"),
                ModbusSource("plc.main", server.Port)
            },
            gateways: new[]
            {
                Route("memory-to-modbus", sourceId, "Server.Source", destinationId, "PLC.Destination")
            });

        var activation = await runtime.ActivateAsync("gateway-memory-modbus", 1, package);
        Assert.True(activation.Activated, Describe(activation));

        server.RejectWrites = true;
        await runtime.WriteAsync(sourceId, (short)77);
        await WaitForAsync(
            () => Assert.Single(runtime.GatewayDiagnostics()).WriteFailureCount >= 1,
            TimeSpan.FromSeconds(3));

        var failed = Assert.Single(runtime.GatewayDiagnostics());
        Assert.Equal(GatewayRouteRuntimeState.Degraded, failed.State);
        Assert.Equal((ushort)0, server.HoldingRegisters[10]);
        Assert.Equal(TagQuality.Good, GetCurrent(runtime, sourceId).Quality);
        Assert.Equal((short)77, Convert.ToInt16(GetCurrent(runtime, sourceId).Value));

        server.RejectWrites = false;
        await runtime.WriteAsync(sourceId, (short)88);
        await WaitForAsync(() => server.HoldingRegisters[10] == 88, TimeSpan.FromSeconds(3));
        await WaitForAsync(
            () => Assert.Single(runtime.GatewayDiagnostics()).State == GatewayRouteRuntimeState.Running,
            TimeSpan.FromSeconds(2));

        var recovered = Assert.Single(runtime.GatewayDiagnostics());
        Assert.Equal(1, recovered.WriteFailureCount);
        Assert.Equal(0, recovered.ConsecutiveFailures);
        Assert.True(recovered.TransferCount >= 1);
        Assert.Null(recovered.LastError);
    }

    [Fact]
    public async Task OnChange_FansOut_AppliesDeadband_AndCoalescesRateLimitedUpdates()
    {
        var sourceId = Guid.NewGuid();
        var deadbandDestinationId = Guid.NewGuid();
        var throttledDestinationId = Guid.NewGuid();
        var bus = new InMemoryScadaEventBus();
        await using var runtime = CreateRuntime(bus);

        var package = Package(
            tags: new[]
            {
                MemoryTag(sourceId, "Server.Source", "memory.server", 0),
                MemoryTag(deadbandDestinationId, "Server.Deadband", "memory.server", 0),
                MemoryTag(throttledDestinationId, "Server.Throttled", "memory.server", 0)
            },
            dataSources: new[] { ServerMemorySource("memory.server") },
            gateways: new[]
            {
                Route(
                    "deadband-route",
                    sourceId,
                    "Server.Source",
                    deadbandDestinationId,
                    "Server.Deadband",
                    deadband: 5),
                Route(
                    "throttled-route",
                    sourceId,
                    "Server.Source",
                    throttledDestinationId,
                    "Server.Throttled",
                    minimumIntervalMilliseconds: 200)
            });

        var activation = await runtime.ActivateAsync("gateway-onchange", 1, package);
        Assert.True(activation.Activated, Describe(activation));

        await runtime.WriteAsync(sourceId, (short)10);
        await WaitForAsync(() => Int16Value(runtime, deadbandDestinationId) == 10, TimeSpan.FromSeconds(2));
        await WaitForAsync(() => Int16Value(runtime, throttledDestinationId) == 10, TimeSpan.FromSeconds(2));

        await runtime.WriteAsync(sourceId, (short)11);
        await runtime.WriteAsync(sourceId, (short)12);
        await runtime.WriteAsync(sourceId, (short)13);

        await WaitForAsync(() => Int16Value(runtime, throttledDestinationId) == 13, TimeSpan.FromSeconds(2));
        await Task.Delay(100);
        Assert.Equal((short)10, Int16Value(runtime, deadbandDestinationId));

        var throttled = Assert.Single(runtime.GatewayDiagnostics(), x => x.Key == "throttled-route");
        Assert.True(throttled.CoalescedUpdateCount >= 1);
        Assert.True(throttled.TransferCount < 4);

        await runtime.WriteAsync(sourceId, (short)16);
        await WaitForAsync(() => Int16Value(runtime, deadbandDestinationId) == 16, TimeSpan.FromSeconds(2));
        await WaitForAsync(() => Int16Value(runtime, throttledDestinationId) == 16, TimeSpan.FromSeconds(2));

        var beforeSameValue = runtime.GatewayDiagnostics().ToDictionary(x => x.Key, x => x.TransferCount);
        await runtime.WriteAsync(sourceId, (short)16);
        await Task.Delay(300);
        var afterSameValue = runtime.GatewayDiagnostics().ToDictionary(x => x.Key, x => x.TransferCount);
        Assert.Equal(beforeSameValue["deadband-route"], afterSameValue["deadband-route"]);
        Assert.Equal(beforeSameValue["throttled-route"], afterSameValue["throttled-route"]);
    }

    [Fact]
    public async Task Periodic_UsesBoundedCadence_AndLatestGoodValue()
    {
        var sourceId = Guid.NewGuid();
        var destinationId = Guid.NewGuid();
        var bus = new InMemoryScadaEventBus();
        await using var runtime = CreateRuntime(bus);

        var package = Package(
            tags: new[]
            {
                MemoryTag(sourceId, "Server.Source", "memory.server", 21),
                MemoryTag(destinationId, "Server.Destination", "memory.server", 0)
            },
            dataSources: new[] { ServerMemorySource("memory.server") },
            gateways: new[]
            {
                new GatewayRouteEngineeringDto(
                    Guid.NewGuid(),
                    "periodic-route",
                    "Periodic route",
                    sourceId,
                    "Server.Source",
                    destinationId,
                    "Server.Destination",
                    TransferMode: GatewayTransferMode.Periodic,
                    InitialTransferPolicy: GatewayInitialTransferPolicy.WaitForNextAcceptableValue,
                    PeriodMilliseconds: 100)
            });

        var activation = await runtime.ActivateAsync("gateway-periodic", 1, package);
        Assert.True(activation.Activated, Describe(activation));

        await WaitForAsync(() => Int16Value(runtime, destinationId) == 21, TimeSpan.FromSeconds(2));
        var baseline = Assert.Single(runtime.GatewayDiagnostics());
        Assert.NotNull(baseline.LastSuccessfulTransferAtUtc);
        var targetTransferCount = baseline.TransferCount + 2;

        await WaitForAsync(
            () => Assert.Single(runtime.GatewayDiagnostics()).TransferCount >= targetTransferCount,
            TimeSpan.FromSeconds(2));

        var diagnostic = Assert.Single(runtime.GatewayDiagnostics());
        Assert.Equal(GatewayTransferMode.Periodic, diagnostic.TransferMode);
        Assert.Equal(100, diagnostic.EffectiveIntervalMilliseconds);
        Assert.True(diagnostic.TransferCount >= targetTransferCount);
        Assert.NotNull(diagnostic.LastSuccessfulTransferAtUtc);

        var observedCadence = diagnostic.LastSuccessfulTransferAtUtc.Value - baseline.LastSuccessfulTransferAtUtc.Value;
        Assert.True(
            observedCadence >= TimeSpan.FromMilliseconds(180),
            $"Expected two additional 100 ms periodic transfers to span at least 180 ms, but observed {observedCadence.TotalMilliseconds:F0} ms.");

        await runtime.WriteAsync(sourceId, (short)35);
        await WaitForAsync(() => Int16Value(runtime, destinationId) == 35, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task ActivationSwitch_ReplacesGatewayRoutesWithNewActiveRevision()
    {
        var sourceAId = Guid.NewGuid();
        var sourceBId = Guid.NewGuid();
        var destinationId = Guid.NewGuid();
        var bus = new InMemoryScadaEventBus();
        await using var runtime = CreateRuntime(bus);

        var tags = new[]
        {
            MemoryTag(sourceAId, "Server.SourceA", "memory.server", 0),
            MemoryTag(sourceBId, "Server.SourceB", "memory.server", 0),
            MemoryTag(destinationId, "Server.Destination", "memory.server", 0)
        };
        var sources = new[] { ServerMemorySource("memory.server") };

        var revision1 = Package(
            tags,
            sources,
            new[] { Route("a-to-destination", sourceAId, "Server.SourceA", destinationId, "Server.Destination") });
        Assert.True((await runtime.ActivateAsync("gateway-switch", 1, revision1)).Activated);

        await runtime.WriteAsync(sourceAId, (short)5);
        await WaitForAsync(() => Int16Value(runtime, destinationId) == 5, TimeSpan.FromSeconds(2));

        var revision2 = Package(
            tags,
            sources,
            new[] { Route("b-to-destination", sourceBId, "Server.SourceB", destinationId, "Server.Destination") });
        var switched = await runtime.ActivateAsync("gateway-switch", 2, revision2);
        Assert.True(switched.Activated, Describe(switched));
        Assert.Equal("b-to-destination", Assert.Single(runtime.GatewayDiagnostics()).Key);

        await runtime.WriteAsync(sourceAId, (short)6);
        await Task.Delay(250);
        Assert.Equal((short)5, Int16Value(runtime, destinationId));

        await runtime.WriteAsync(sourceBId, (short)7);
        await WaitForAsync(() => Int16Value(runtime, destinationId) == 7, TimeSpan.FromSeconds(2));
    }

    private static GatewayEngineeringRuntimeCoordinator CreateRuntime(IScadaEventBus bus)
    {
        var inner = new EngineeringRuntimeCoordinator(
            bus,
            new EngineeringDriverCompiler(),
            TimeSpan.FromSeconds(3),
            new InMemoryServerMemoryRetentionStore());
        return new GatewayEngineeringRuntimeCoordinator(inner, bus);
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

    private static DataSourceEngineeringDto ServerMemorySource(string key) => new(
        Guid.NewGuid(),
        key,
        "Server Memory",
        InternalMemoryRuntimePlanner.ServerMemoryDriverKey);

    private static DataSourceEngineeringDto ModbusSource(string key, int port) => new(
        Guid.NewGuid(),
        key,
        "Test PLC",
        EngineeringDriverCompiler.ModbusTcpDriverKey,
        Settings: new Dictionary<string, string>
        {
            ["host"] = "127.0.0.1",
            ["port"] = port.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["scanIntervalMilliseconds"] = "50",
            ["requestTimeoutMilliseconds"] = "300",
            ["unitId"] = "1"
        });

    private static TagEngineeringDto MemoryTag(Guid id, string path, string source, short initialValue) => new(
        id,
        path.Split('.').Last(),
        path,
        TagDataType.Int16,
        Source: source,
        ReadOnly: false,
        InitialValue: Initial(TagDataType.Int16, initialValue));

    private static TagEngineeringDto ModbusTag(
        Guid id,
        string path,
        string source,
        string address,
        bool readOnly) => new(
        id,
        path.Split('.').Last(),
        path,
        TagDataType.Int16,
        Source: source,
        Address: address,
        ReadOnly: readOnly);

    private static GatewayRouteEngineeringDto Route(
        string key,
        Guid sourceId,
        string sourcePath,
        Guid destinationId,
        string destinationPath,
        double? deadband = null,
        int? minimumIntervalMilliseconds = null,
        GatewayInitialTransferPolicy initialTransferPolicy = GatewayInitialTransferPolicy.WaitForNextAcceptableValue) => new(
            Guid.NewGuid(),
            key,
            key,
            sourceId,
            sourcePath,
            destinationId,
            destinationPath,
            TransferMode: GatewayTransferMode.OnChange,
            InitialTransferPolicy: initialTransferPolicy,
            Deadband: deadband,
            MinimumIntervalMilliseconds: minimumIntervalMilliseconds);

    private static MemoryInitialValueDto Initial(TagDataType type, object value) =>
        new(type, JsonSerializer.SerializeToElement(value, value.GetType()));

    private static TagValue GetCurrent(IEngineeringRuntimeCoordinator runtime, Guid tagId)
    {
        Assert.True(runtime.TryGetCurrent(tagId, out var current));
        return Assert.IsType<TagValue>(current);
    }

    private static short Int16Value(IEngineeringRuntimeCoordinator runtime, Guid tagId) =>
        Convert.ToInt16(GetCurrent(runtime, tagId).Value, System.Globalization.CultureInfo.InvariantCulture);

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