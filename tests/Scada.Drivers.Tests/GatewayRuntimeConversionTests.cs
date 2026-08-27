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
        await WaitForRouteActivityAsync(runtime, "transform", TimeSpan.FromSeconds(2));
        var transformed = Assert.Single(runtime.GatewayDiagnostics(), route => route.Key == "transform");
        Assert.True(
            transformed.TransferCount == 1 && transformed.WriteFailureCount == 0,
            DiagnosticText(transformed));
        Assert.Equal(27, CurrentValue<int>(runtime, transformedDestinationId));
        Assert.Equal(GatewayRouteRuntimeState.Running, transformed.State);

        await runtime.WriteAsync(overflowSourceId, 40_000);
        await WaitForRouteActivityAsync(runtime, "overflow", TimeSpan.FromSeconds(2));

        var overflow = Assert.Single(runtime.GatewayDiagnostics(), route => route.Key == "overflow");
        Assert.Equal(GatewayRouteRuntimeState.Degraded, overflow.State);
        Assert.Equal(1, overflow.WriteFailureCount);
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

    private static async Task WaitForRouteActivityAsync(
        GatewayEngineeringRuntimeCoordinator runtime,
        string key,
        TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            var route = runtime.GatewayDiagnostics().Single(item => item.Key == key);
            if (route.TransferCount > 0 || route.WriteFailureCount > 0) return;
            await Task.Delay(20);
        }

        var diagnostic = runtime.GatewayDiagnostics().Single(item => item.Key == key);
        Assert.Fail($"Gateway route produced no transfer activity within {timeout}. {DiagnosticText(diagnostic)}");
    }

    private static string DiagnosticText(GatewayRouteRuntimeDiagnostic route) =>
        $"state={route.State}; transfers={route.TransferCount}; failures={route.WriteFailureCount}; skipped={route.SkippedTransferCount}; pending={route.HasPendingValue}; error={route.LastError ?? "<none>"}";

    private static string Describe(RuntimeActivationResult result) =>
        string.Join(" | ",
            result.CompilationIssues.Select(issue => $"{issue.Code}: {issue.Message}")
                .Concat(result.RuntimeIssues.Select(issue => $"{issue.Code}: {issue.Message}")));
}
