using Scada.Core.Events;
using Scada.Core.InternalMemory;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.DriverHost.Runtime;
using Scada.Drivers.Abstractions;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;

namespace Scada.Drivers.Tests;

public sealed class EngineeringRuntimeCommunicationDiagnosticsTests
{
    [Fact]
    public async Task ActiveRuntime_ExposesEngineeringDataSourceIdentityAndIndependentFailureRecovery()
    {
        await using var serverA = new TestModbusTcpServer();
        await using var serverB = new TestModbusTcpServer();
        serverA.HoldingRegisters[0] = 11;
        serverB.HoldingRegisters[10] = 22;
        serverA.Start();
        serverB.Start();

        var tagAId = Guid.NewGuid();
        var tagBId = Guid.NewGuid();
        var bus = new InMemoryScadaEventBus();
        await using var runtime = new EngineeringRuntimeCoordinator(
            bus,
            new EngineeringDriverCompiler(),
            TimeSpan.FromSeconds(3),
            new InMemoryServerMemoryRetentionStore());

        var package = new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            new[]
            {
                new TagEngineeringDto(
                    tagAId,
                    "Source A",
                    "PLC_A.Value",
                    TagDataType.Int16,
                    Source: "plc.a",
                    Address: "holding:0",
                    ReadOnly: true),
                new TagEngineeringDto(
                    tagBId,
                    "Source B",
                    "PLC_B.Value",
                    TagDataType.Int16,
                    Source: "plc.b",
                    Address: "holding:10",
                    ReadOnly: false)
            },
            Array.Empty<AlarmEngineeringDto>(),
            new[]
            {
                ModbusSource("plc.a", "PLC A", serverA.Port),
                ModbusSource("plc.b", "PLC B", serverB.Port)
            });

        var activation = await runtime.ActivateAsync("communication-diagnostics", 1, package);
        Assert.True(activation.Activated, Describe(activation));

        await WaitForAsync(() =>
        {
            var diagnostics = runtime.Describe().CommunicationDrivers;
            return diagnostics.Count == 2
                && diagnostics.All(item => item.State == CommunicationDriverOperationalState.Healthy)
                && diagnostics.All(item => item.TagQuality.Good == 1);
        }, TimeSpan.FromSeconds(4));

        var initial = runtime.Describe().CommunicationDrivers.ToDictionary(item => item.DataSourceKey);
        Assert.Equal(new[] { "plc.a", "plc.b" }, initial.Keys.OrderBy(key => key).ToArray());
        Assert.Equal("PLC A", initial["plc.a"].DataSourceName);
        Assert.Equal("PLC B", initial["plc.b"].DataSourceName);
        Assert.Equal(EngineeringDriverCompiler.ModbusTcpDriverKey, initial["plc.a"].DriverType);
        Assert.Equal(EngineeringDriverCompiler.ModbusTcpDriverKey, initial["plc.b"].DriverType);
        Assert.False(string.IsNullOrWhiteSpace(initial["plc.a"].RuntimeInstanceId));
        Assert.False(string.IsNullOrWhiteSpace(initial["plc.b"].RuntimeInstanceId));
        Assert.NotEqual(initial["plc.a"].RuntimeInstanceId, initial["plc.b"].RuntimeInstanceId);
        Assert.NotEqual(initial["plc.a"].DataSourceKey, initial["plc.a"].RuntimeInstanceId);
        Assert.NotEqual(initial["plc.b"].DataSourceKey, initial["plc.b"].RuntimeInstanceId);

        serverB.ResponseDelay = TimeSpan.FromMilliseconds(350);
        await WaitForAsync(() =>
        {
            var diagnostics = runtime.Describe().CommunicationDrivers.ToDictionary(item => item.DataSourceKey);
            return diagnostics["plc.b"].State == CommunicationDriverOperationalState.Reconnecting
                && diagnostics["plc.b"].Counters.Timeouts >= 1
                && diagnostics["plc.a"].State == CommunicationDriverOperationalState.Healthy;
        }, TimeSpan.FromSeconds(5));

        Assert.True(runtime.TryGetCurrent(tagAId, out var currentA));
        Assert.NotNull(currentA);
        Assert.Equal(TagQuality.Good, currentA!.Quality);
        Assert.True(runtime.TryGetCurrent(tagBId, out var currentB));
        Assert.NotNull(currentB);
        Assert.Equal(TagQuality.BadCommunication, currentB!.Quality);

        serverB.ResponseDelay = TimeSpan.Zero;
        serverB.DropConnections();
        await WaitForAsync(() =>
        {
            var diagnostics = runtime.Describe().CommunicationDrivers.ToDictionary(item => item.DataSourceKey);
            return diagnostics["plc.b"].State == CommunicationDriverOperationalState.Healthy
                && diagnostics["plc.b"].Counters.ConsecutiveFailures == 0
                && diagnostics["plc.b"].Counters.Reconnects >= 1
                && diagnostics["plc.a"].State == CommunicationDriverOperationalState.Healthy;
        }, TimeSpan.FromSeconds(5));

        await runtime.WriteAsync(tagBId, (short)77);
        await WaitForAsync(() => serverB.HoldingRegisters[10] == 77, TimeSpan.FromSeconds(2));
        Assert.Equal((ushort)11, serverA.HoldingRegisters[0]);

        var recovered = runtime.Describe().CommunicationDrivers.ToDictionary(item => item.DataSourceKey);
        Assert.True(recovered["plc.b"].Counters.WriteOperations >= 1);
        Assert.Equal(0, recovered["plc.a"].Counters.WriteOperations);
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
            ["requestTimeoutMilliseconds"] = "100",
            ["unitId"] = "1"
        });

    private static async Task WaitForAsync(Func<bool> predicate, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (predicate()) return;
            await Task.Delay(25);
        }

        Assert.True(predicate(), $"Condition was not met within {timeout}.");
    }

    private static string Describe(RuntimeActivationResult result) =>
        string.Join(" | ",
            result.CompilationIssues.Select(issue => $"{issue.Code}: {issue.Message}")
                .Concat(result.RuntimeIssues.Select(issue => $"{issue.Code}: {issue.Message}")));
}
