using System.Text.Json;
using Scada.Core.Events;
using Scada.Core.InternalMemory;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.DriverHost.Runtime;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;

namespace Scada.Drivers.Tests;

public sealed class GatewayRuntimeConversionTests
{
    [Fact]
    public async Task CheckedNumeric_AppliesTransform_AndOverflowFailsClosedPerRoute()
    {
        var transformedSourceId = Guid.NewGuid();
        var transformedDestinationId = Guid.NewGuid();
        var overflowSourceId = Guid.NewGuid();
        var overflowDestinationId = Guid.NewGuid();
        var bus = new InMemoryScadaEventBus();
        await using var runtime = CreateRuntime(bus);

        var package = new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            new[]
            {
                MemoryTag(transformedSourceId, "Server.TransformSource", TagDataType.Int16, (short)0),
                MemoryTag(transformedDestinationId, "Server.TransformDestination", TagDataType.Int32, 0),
                MemoryTag(overflowSourceId, "Server.OverflowSource", TagDataType.Int32, 0),
                MemoryTag(overflowDestinationId, "Server.OverflowDestination", TagDataType.Int16, (short)0)
            },
            Array.Empty<AlarmEngineeringDto>(),
            new[]
            {
                new DataSourceEngineeringDto(
                    Guid.NewGuid(),
                    "memory.server",
                    "Server Memory",
                    InternalMemoryRuntimePlanner.ServerMemoryDriverKey)
            },
            Gateways: new[]
            {
                new GatewayRouteEngineeringDto(
                    Guid.NewGuid(),
                    "transform",
                    "Transform",
                    transformedSourceId,
                    "Server.TransformSource",
                    transformedDestinationId,
                    "Server.TransformDestination",
                    ConversionPolicy: GatewayConversionPolicy.CheckedNumeric,
                    InitialTransferPolicy: GatewayInitialTransferPolicy.WaitForNextAcceptableValue,
                    Gain: 2,
                    Offset: 5),
                new GatewayRouteEngineeringDto(
                    Guid.NewGuid(),
                    "overflow",
                    "Overflow",
                    overflowSourceId,
                    "Server.OverflowSource",
                    overflowDestinationId,
                    "Server.OverflowDestination",
                    ConversionPolicy: GatewayConversionPolicy.CheckedNumeric,
                    InitialTransferPolicy: GatewayInitialTransferPolicy.WaitForNextAcceptableValue)
            });

        var activation = await runtime.ActivateAsync("gateway-conversion", 1, package);
        Assert.True(activation.Activated, Describe(activation));

        await runtime.WriteAsync(transformedSourceId, (short)11);
        await WaitForAsync(
            () => CurrentValue<int>(runtime, transformedDestinationId) == 27,
            TimeSpan.FromSeconds(2));

        var transformed = Assert.Single(runtime.GatewayDiagnostics(), route => route.Key == "transform");
        Assert.Equal(GatewayRouteRuntimeState.Running, transformed.State);
        Assert.Equal(1, transformed.TransferCount);
        Assert.Equal(0, transformed.WriteFailureCount);

        await runtime.WriteAsync(overflowSourceId, 40_000);
        await WaitForAsync(
            () => Assert.Single(runtime.GatewayDiagnostics(), route => route.Key == "overflow").WriteFailureCount == 1,
            TimeSpan.FromSeconds(2));

        var overflow = Assert.Single(runtime.GatewayDiagnostics(), route => route.Key == "overflow");
        Assert.Equal(GatewayRouteRuntimeState.Degraded, overflow.State);
        Assert.Equal(1, overflow.ConsecutiveFailures);
        Assert.Contains("Overflow", overflow.LastError, StringComparison.OrdinalIgnoreCase);
        Assert.Equal((short)0, CurrentValue<short>(runtime, overflowDestinationId));
        Assert.Equal(40_000, CurrentValue<int>(runtime, overflowSourceId));
        Assert.Equal(TagQuality.Good, Current(runtime, overflowSourceId).Quality);
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

    private static TagEngineeringDto MemoryTag(
        Guid id,
        string path,
        TagDataType dataType,
        object initialValue) => new(
            id,
            path.Split('.').Last(),
            path,
            dataType,
            Source: "memory.server",
            ReadOnly: false,
            InitialValue: new MemoryInitialValueDto(
                dataType,
                JsonSerializer.SerializeToElement(initialValue, initialValue.GetType())));

    private static TagValue Current(IEngineeringRuntimeCoordinator runtime, Guid tagId)
    {
        Assert.True(runtime.TryGetCurrent(tagId, out var current));
        return Assert.IsType<TagValue>(current);
    }

    private static T CurrentValue<T>(IEngineeringRuntimeCoordinator runtime, Guid tagId) =>
        Assert.IsType<T>(Current(runtime, tagId).Value);

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
