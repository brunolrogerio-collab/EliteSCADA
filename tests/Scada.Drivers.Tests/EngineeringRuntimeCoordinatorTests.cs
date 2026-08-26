using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.DriverHost.Runtime;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;

namespace Scada.Drivers.Tests;

public sealed class EngineeringRuntimeCoordinatorTests
{
    [Fact]
    public async Task ActivateAsync_CommitsReadyModbusRuntimeAndRoutesWritesAndAlarms()
    {
        await using var server = new TestModbusTcpServer();
        server.HoldingRegisters[10] = 123;
        server.Start();

        var tagId = Guid.NewGuid();
        var alarmId = Guid.NewGuid();
        var package = CreatePackage(server.Port, tagId, alarmId, "holding:10", readOnly: false);
        var externalBus = new InMemoryScadaEventBus();
        var forwardedTagEvents = 0;
        using var subscription = externalBus.Subscribe<TagValueChanged>(evt =>
        {
            if (evt.Current.TagId == tagId) Interlocked.Increment(ref forwardedTagEvents);
            return ValueTask.CompletedTask;
        });

        await using var runtime = new EngineeringRuntimeCoordinator(
            externalBus,
            new EngineeringDriverCompiler(),
            TimeSpan.FromSeconds(2));

        var result = await runtime.ActivateAsync("plant-a", 1, package);

        Assert.True(result.Activated);
        Assert.Equal(1, runtime.Describe().Revision);
        Assert.True(runtime.TryGetTag(tagId, out var activeTag));
        Assert.Equal("Plant.Setpoint", activeTag!.Path);
        Assert.True(runtime.TryGetCurrent(tagId, out var current));
        Assert.Equal(123d, Convert.ToDouble(current!.Value));
        await WaitForAsync(
            () => runtime.Alarms(activeOnly: true).Any(x => x.DefinitionId == alarmId),
            TimeSpan.FromSeconds(2));

        await runtime.WriteAsync(tagId, (short)77);
        Assert.Equal((ushort)77, server.HoldingRegisters[10]);

        await WaitForAsync(() => Volatile.Read(ref forwardedTagEvents) > 0, TimeSpan.FromSeconds(2));
        await WaitForAsync(
            () => !runtime.Alarms(activeOnly: true).Any(x => x.DefinitionId == alarmId),
            TimeSpan.FromSeconds(2));

        Assert.Contains(runtime.Describe().Drivers, x => x.DriverId == "modbus.tcp:plc-a");
        Assert.Contains(runtime.AlarmDefinitions(), x => x.Id == alarmId);
    }

    [Fact]
    public async Task ActivateAsync_FailedCandidateKeepsPreviousRuntimeAndDoesNotLeakCandidateEvents()
    {
        await using var healthyServer = new TestModbusTcpServer();
        healthyServer.HoldingRegisters[10] = 111;
        healthyServer.Start();

        var activeTagId = Guid.NewGuid();
        var activeAlarmId = Guid.NewGuid();
        var healthyPackage = CreatePackage(healthyServer.Port, activeTagId, activeAlarmId, "holding:10");

        var externalBus = new InMemoryScadaEventBus();
        await using var runtime = new EngineeringRuntimeCoordinator(
            externalBus,
            new EngineeringDriverCompiler(),
            TimeSpan.FromMilliseconds(450));

        var first = await runtime.ActivateAsync("plant-a", 1, healthyPackage);
        Assert.True(first.Activated);
        Assert.Equal(1, runtime.Describe().Revision);

        await using var unavailableServer = new TestModbusTcpServer();
        unavailableServer.Start();
        var unavailablePort = unavailableServer.Port;
        await unavailableServer.StopAsync();

        var candidateTagId = Guid.NewGuid();
        var candidateAlarmId = Guid.NewGuid();
        var candidatePackage = CreatePackage(unavailablePort, candidateTagId, candidateAlarmId, "holding:20");
        var leakedCandidateEvents = 0;
        using var subscription = externalBus.Subscribe<TagValueChanged>(evt =>
        {
            if (evt.Current.TagId == candidateTagId) Interlocked.Increment(ref leakedCandidateEvents);
            return ValueTask.CompletedTask;
        });

        var second = await runtime.ActivateAsync("plant-a", 2, candidatePackage);

        Assert.False(second.Activated);
        Assert.Contains(second.RuntimeIssues, x => x.Code == "RUNTIME_CANDIDATE_NOT_READY" && x.IsError);
        Assert.Equal(1, runtime.Describe().Revision);
        Assert.True(runtime.TryGetTag(activeTagId, out _));
        Assert.False(runtime.TryGetTag(candidateTagId, out _));
        Assert.True(runtime.TryGetCurrent(activeTagId, out var activeValue));
        Assert.Equal(111d, Convert.ToDouble(activeValue!.Value));
        Assert.Equal(0, Volatile.Read(ref leakedCandidateEvents));
    }

    [Fact]
    public async Task ActivateAsync_CommitFailureKeepsPreviousRuntimeAndCandidateEventsGated()
    {
        await using var activeServer = new TestModbusTcpServer();
        activeServer.HoldingRegisters[10] = 90;
        activeServer.Start();

        await using var candidateServer = new TestModbusTcpServer();
        candidateServer.HoldingRegisters[20] = 140;
        candidateServer.Start();

        var activeTagId = Guid.NewGuid();
        var candidateTagId = Guid.NewGuid();
        var externalBus = new InMemoryScadaEventBus();
        var leakedCandidateEvents = 0;
        using var subscription = externalBus.Subscribe<TagValueChanged>(evt =>
        {
            if (evt.Current.TagId == candidateTagId) Interlocked.Increment(ref leakedCandidateEvents);
            return ValueTask.CompletedTask;
        });

        await using var runtime = new EngineeringRuntimeCoordinator(
            externalBus,
            new EngineeringDriverCompiler(),
            TimeSpan.FromSeconds(2));

        Assert.True((await runtime.ActivateAsync(
            "plant-a",
            1,
            CreatePackage(activeServer.Port, activeTagId, Guid.NewGuid(), "holding:10"))).Activated);

        var commitCalled = false;
        var result = await runtime.ActivateAsync(
            "plant-a",
            2,
            CreatePackage(candidateServer.Port, candidateTagId, Guid.NewGuid(), "holding:20"),
            (_, _) =>
            {
                commitCalled = true;
                throw new InvalidOperationException("Persistence rejected activation.");
            });

        Assert.True(commitCalled);
        Assert.False(result.Activated);
        Assert.Contains(result.RuntimeIssues, x => x.Code == "RUNTIME_ACTIVATION_COMMIT_FAILED" && x.IsError);
        Assert.Equal(1, runtime.Describe().Revision);
        Assert.True(runtime.TryGetTag(activeTagId, out _));
        Assert.False(runtime.TryGetTag(candidateTagId, out _));
        Assert.Equal(0, Volatile.Read(ref leakedCandidateEvents));
    }

    [Fact]
    public async Task ActivateAsync_CompilationFailureDoesNotReplaceActiveRuntime()
    {
        await using var server = new TestModbusTcpServer();
        server.HoldingRegisters[10] = 50;
        server.Start();

        var activeTagId = Guid.NewGuid();
        var package = CreatePackage(server.Port, activeTagId, Guid.NewGuid(), "holding:10");
        var externalBus = new InMemoryScadaEventBus();
        await using var runtime = new EngineeringRuntimeCoordinator(
            externalBus,
            new EngineeringDriverCompiler(),
            TimeSpan.FromSeconds(1));

        Assert.True((await runtime.ActivateAsync("plant-a", 1, package)).Activated);

        var invalidTagId = Guid.NewGuid();
        var invalid = CreatePackage(server.Port, invalidTagId, Guid.NewGuid(), "40001");
        var result = await runtime.ActivateAsync("plant-a", 2, invalid);

        Assert.False(result.Activated);
        Assert.Contains(result.CompilationIssues, x => x.Code == "MODBUS_TAG_ADDRESS_INVALID" && x.IsError);
        Assert.Equal(1, runtime.Describe().Revision);
        Assert.True(runtime.TryGetTag(activeTagId, out _));
        Assert.False(runtime.TryGetTag(invalidTagId, out _));
    }

    private static EngineeringPackage CreatePackage(
        int port,
        Guid tagId,
        Guid alarmId,
        string address,
        bool readOnly = true)
    {
        var tag = new TagEngineeringDto(
            tagId,
            "Setpoint",
            "Plant.Setpoint",
            TagDataType.Int16,
            Source: "plc-a",
            Address: address,
            ReadOnly: readOnly);

        var alarm = new AlarmEngineeringDto(
            alarmId,
            "High setpoint",
            tagId,
            tag.Path,
            AlarmType.High,
            AlarmPriority.High,
            Setpoint: 100,
            Area: "Plant",
            Message: "Setpoint above 100");

        var dataSource = new DataSourceEngineeringDto(
            null,
            "plc-a",
            "PLC A",
            EngineeringDriverCompiler.ModbusTcpDriverKey,
            Settings: new Dictionary<string, string>
            {
                ["host"] = "127.0.0.1",
                ["port"] = port.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["scanIntervalMilliseconds"] = "25",
                ["requestTimeoutMilliseconds"] = "100",
                ["unitId"] = "1"
            });

        return new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            new[] { tag },
            new[] { alarm },
            new[] { dataSource });
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
}
