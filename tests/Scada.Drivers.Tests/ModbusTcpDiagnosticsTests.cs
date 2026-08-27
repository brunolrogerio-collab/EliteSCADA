using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;
using Scada.Drivers.Modbus;
using Scada.Drivers.Simulation;

namespace Scada.Drivers.Tests;

public sealed class ModbusTcpDiagnosticsTests
{
    [Fact]
    public async Task Driver_ExposesProtocolNeutralHealthySnapshotWithIdentityCountersQualityAndWrites()
    {
        await using var server = new TestModbusTcpServer();
        server.HoldingRegisters[0] = 10;
        server.Start();

        var cache = new CurrentTagCache(new InMemoryScadaEventBus());
        var registry = new InMemoryTagRegistry();
        var tag = TagDefinition.Create("Value", "Diagnostics.Modbus.Value", TagDataType.Double, readOnly: false);
        var point = new ModbusPoint(tag, 1, ModbusDataArea.HoldingRegister, 0, ModbusValueType.UInt16, Writable: true);

        await using var driver = new ModbusTcpDriver(
            "PLC_DIAGNOSTICS_A",
            "Diagnostics PLC A",
            "127.0.0.1",
            cache,
            registry,
            new[] { point },
            server.Port,
            scanRate: TimeSpan.FromMilliseconds(80),
            requestTimeout: TimeSpan.FromMilliseconds(500));

        Assert.IsAssignableFrom<ICommunicationDiagnosticsSource>(driver);
        await driver.StartAsync();
        await WaitForAsync(
            () => driver.GetCommunicationDiagnostics() is { State: CommunicationDriverOperationalState.Healthy, TagQuality.Good: 1 },
            TimeSpan.FromSeconds(5));

        var initial = driver.GetCommunicationDiagnostics();
        Assert.Equal("PLC_DIAGNOSTICS_A", initial.DataSourceKey);
        Assert.Equal("Diagnostics PLC A", initial.DataSourceName);
        Assert.Equal("modbus.tcp", initial.DriverType);
        Assert.False(string.IsNullOrWhiteSpace(initial.RuntimeInstanceId));
        Assert.Equal($"127.0.0.1:{server.Port}", initial.Endpoint);
        Assert.Equal(CommunicationDriverOperationalState.Healthy, initial.State);
        Assert.True(initial.StateChangedAt <= initial.CapturedAt);
        Assert.NotNull(initial.LastSuccessfulCommunicationAt);
        Assert.Null(initial.LastFailedCommunicationAt);
        Assert.NotNull(initial.DataAge);
        Assert.Equal(TimeSpan.FromMilliseconds(80), initial.ConfiguredScanInterval);
        Assert.NotNull(initial.LastOperationDuration);
        Assert.NotNull(initial.AverageOperationDuration);
        Assert.NotNull(initial.LastScanDuration);
        Assert.Equal(0d, initial.RecentFailureRate);
        Assert.Equal(1, initial.AssociatedTagCount);
        Assert.Equal(1, initial.TagQuality.Good);
        Assert.Equal(0, initial.TagQuality.BadCommunication);
        Assert.Equal(0, initial.TagQuality.NoCurrentSample);
        Assert.Equal(1, initial.TagQuality.Total);
        Assert.True(initial.Counters.Cycles > 0);
        Assert.True(initial.Counters.Requests > 0);
        Assert.True(initial.Counters.SuccessfulOperations > 0);
        Assert.Equal(0, initial.Counters.FailedOperations);
        Assert.Equal(0, initial.Counters.Timeouts);
        Assert.True(initial.Counters.Connections >= 1);
        Assert.True(initial.Counters.ReadOperations > 0);
        Assert.True(initial.Counters.UpdatesPublished > 0);
        Assert.NotNull(initial.ProtocolDetails);
        Assert.Equal("127.0.0.1", initial.ProtocolDetails!["host"]);
        Assert.Equal(server.Port.ToString(), initial.ProtocolDetails["port"]);
        Assert.Equal("1", initial.ProtocolDetails["pollBlockCount"]);
        Assert.Equal("1", initial.ProtocolDetails["unitIds"]);

        var modbus = driver.GetModbusDiagnostics();
        Assert.Equal("127.0.0.1", modbus.Host);
        Assert.Equal(server.Port, modbus.Port);
        Assert.Equal(TimeSpan.FromMilliseconds(500), modbus.RequestTimeout);
        Assert.Equal(1, modbus.PollBlockCount);
        Assert.Equal(new byte[] { 1 }, modbus.UnitIds);
        Assert.True(modbus.SuccessfulPollBlocks > 0);
        Assert.Equal(0, modbus.FailedPollBlocks);
        Assert.True(modbus.Transport.SuccessfulRequestAttempts > 0);

        var writesBefore = initial.Counters.WriteOperations;
        await driver.WriteAsync(tag.Id, 77d);
        await WaitForAsync(
            () => driver.GetCommunicationDiagnostics().Counters.WriteOperations > writesBefore,
            TimeSpan.FromSeconds(2));

        var afterWrite = driver.GetCommunicationDiagnostics();
        Assert.Equal(initial.RuntimeInstanceId, afterWrite.RuntimeInstanceId);
        Assert.True(afterWrite.Counters.WriteOperations > writesBefore);
        Assert.Equal((ushort)77, server.HoldingRegisters[0]);

        await driver.StopAsync();
        Assert.Equal(CommunicationDriverOperationalState.Stopped, driver.GetCommunicationDiagnostics().State);
    }

    [Fact]
    public async Task Driver_ReportsDegradedWriteFailureWithoutInventingTimeout()
    {
        await using var server = new TestModbusTcpServer();
        server.HoldingRegisters[0] = 1;
        server.Start();

        var cache = new CurrentTagCache(new InMemoryScadaEventBus());
        var registry = new InMemoryTagRegistry();
        var tag = TagDefinition.Create("Setpoint", "Diagnostics.Modbus.Setpoint", TagDataType.Double, readOnly: false);
        var point = new ModbusPoint(tag, 1, ModbusDataArea.HoldingRegister, 0, ModbusValueType.UInt16, Writable: true);

        await using var driver = new ModbusTcpDriver(
            "PLC_WRITE_FAILURE",
            "Write Failure PLC",
            "127.0.0.1",
            cache,
            registry,
            new[] { point },
            server.Port,
            scanRate: TimeSpan.FromSeconds(2),
            requestTimeout: TimeSpan.FromMilliseconds(500));

        await driver.StartAsync();
        await WaitForAsync(
            () => driver.GetCommunicationDiagnostics().State == CommunicationDriverOperationalState.Healthy,
            TimeSpan.FromSeconds(5));

        server.RejectWrites = true;
        await Assert.ThrowsAsync<ModbusProtocolException>(async () => await driver.WriteAsync(tag.Id, 2d));

        var failed = driver.GetCommunicationDiagnostics();
        Assert.Equal(CommunicationDriverOperationalState.Degraded, failed.State);
        Assert.True(failed.Counters.FailedOperations >= 1);
        Assert.True(failed.Counters.WriteOperations >= 1);
        Assert.Equal(0, failed.Counters.Timeouts);
        Assert.NotNull(failed.LastFailedCommunicationAt);
        Assert.False(string.IsNullOrWhiteSpace(failed.LastError));
        Assert.Equal(TagQuality.Good, Get(cache, tag.Id).Quality);

        await driver.StopAsync();
    }

    [Fact]
    public async Task Diagnostics_IsolateTwoInstancesAndRecoverOneAfterTimeouts()
    {
        await using var serverA = new TestModbusTcpServer();
        await using var serverB = new TestModbusTcpServer();
        serverA.HoldingRegisters[0] = 11;
        serverB.HoldingRegisters[0] = 22;
        serverA.Start();
        serverB.Start();

        var cache = new CurrentTagCache(new InMemoryScadaEventBus());
        var registry = new InMemoryTagRegistry();
        var tagA = TagDefinition.Create("Value A", "Diagnostics.Modbus.A", TagDataType.Double);
        var tagB = TagDefinition.Create("Value B", "Diagnostics.Modbus.B", TagDataType.Double);
        var pointA = new ModbusPoint(tagA, 1, ModbusDataArea.HoldingRegister, 0, ModbusValueType.UInt16);
        var pointB = new ModbusPoint(tagB, 2, ModbusDataArea.HoldingRegister, 0, ModbusValueType.UInt16);

        await using var driverA = new ModbusTcpDriver(
            "PLC_A",
            "PLC A",
            "127.0.0.1",
            cache,
            registry,
            new[] { pointA },
            serverA.Port,
            scanRate: TimeSpan.FromMilliseconds(60),
            requestTimeout: TimeSpan.FromMilliseconds(80));
        await using var driverB = new ModbusTcpDriver(
            "PLC_B",
            "PLC B",
            "127.0.0.1",
            cache,
            registry,
            new[] { pointB },
            serverB.Port,
            scanRate: TimeSpan.FromMilliseconds(60),
            requestTimeout: TimeSpan.FromMilliseconds(80));

        await driverA.StartAsync();
        await driverB.StartAsync();
        await WaitForAsync(
            () => driverA.GetCommunicationDiagnostics() is { State: CommunicationDriverOperationalState.Healthy, TagQuality.Good: 1 }
                && driverB.GetCommunicationDiagnostics() is { State: CommunicationDriverOperationalState.Healthy, TagQuality.Good: 1 },
            TimeSpan.FromSeconds(5));

        var healthyA = driverA.GetCommunicationDiagnostics();
        var healthyB = driverB.GetCommunicationDiagnostics();
        Assert.NotEqual(healthyA.RuntimeInstanceId, healthyB.RuntimeInstanceId);
        Assert.Equal(0, healthyA.Counters.Timeouts);
        Assert.Equal(0, healthyB.Counters.Timeouts);

        serverB.ResponseDelay = TimeSpan.FromMilliseconds(250);
        await WaitForAsync(
            () => driverB.GetCommunicationDiagnostics() is
            {
                State: CommunicationDriverOperationalState.Reconnecting,
                TagQuality.BadCommunication: 1
            } snapshot
            && snapshot.Counters.Timeouts > 0
            && snapshot.Counters.FailedOperations > 0,
            TimeSpan.FromSeconds(5));

        var failedB = driverB.GetCommunicationDiagnostics();
        var unaffectedA = driverA.GetCommunicationDiagnostics();
        Assert.NotNull(failedB.LastFailedCommunicationAt);
        Assert.False(string.IsNullOrWhiteSpace(failedB.LastError));
        Assert.True(failedB.Counters.Reconnects > 0);
        Assert.True(failedB.RecentFailureRate > 0d);
        Assert.Equal(CommunicationDriverOperationalState.Healthy, unaffectedA.State);
        Assert.Equal(1, unaffectedA.TagQuality.Good);
        Assert.Equal(0, unaffectedA.TagQuality.BadCommunication);
        Assert.Equal(0, unaffectedA.Counters.Timeouts);
        Assert.Equal(0, unaffectedA.Counters.FailedOperations);
        Assert.Equal(TagQuality.Good, Get(cache, tagA.Id).Quality);
        Assert.Equal(TagQuality.BadCommunication, Get(cache, tagB.Id).Quality);

        var failureAt = failedB.LastFailedCommunicationAt!.Value;
        serverB.ResponseDelay = TimeSpan.Zero;
        serverB.HoldingRegisters[0] = 33;
        await WaitForAsync(
            () => driverB.GetCommunicationDiagnostics() is { State: CommunicationDriverOperationalState.Healthy, TagQuality.Good: 1 } snapshot
                && snapshot.LastSuccessfulCommunicationAt > failureAt
                && Convert.ToDouble(Get(cache, tagB.Id).Value) == 33d,
            TimeSpan.FromSeconds(5));

        var recoveredB = driverB.GetCommunicationDiagnostics();
        Assert.Equal(CommunicationDriverOperationalState.Healthy, recoveredB.State);
        Assert.True(recoveredB.Counters.Timeouts > 0);
        Assert.True(recoveredB.Counters.Reconnects > 0);
        Assert.NotNull(recoveredB.LastFailedCommunicationAt);
        Assert.True(recoveredB.LastSuccessfulCommunicationAt > recoveredB.LastFailedCommunicationAt);
        Assert.Equal(1, recoveredB.TagQuality.Good);
        Assert.Equal(0, recoveredB.TagQuality.BadCommunication);

        var finalA = driverA.GetCommunicationDiagnostics();
        Assert.Equal(CommunicationDriverOperationalState.Healthy, finalA.State);
        Assert.Equal(0, finalA.Counters.Timeouts);
        Assert.Equal(0, finalA.Counters.FailedOperations);
        Assert.Equal(TagQuality.Good, Get(cache, tagA.Id).Quality);

        await driverA.StopAsync();
        await driverB.StopAsync();
    }

    [Fact]
    public void CommunicationDiagnostics_RemainOptionalForSimulation()
    {
        Assert.False(typeof(ICommunicationDiagnosticsSource).IsAssignableFrom(typeof(SimulationDriver)));
    }

    private static TagValue Get(ICurrentTagCache cache, Guid id)
    {
        Assert.True(cache.TryGet(id, out var value));
        return Assert.IsType<TagValue>(value);
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
