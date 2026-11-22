using System.Text.Json;
using Scada.Core.Events;
using Scada.Api.Runtime;
using Scada.Core.InternalMemory;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.DriverHost.Runtime;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;
using Scada.Engineering.Gateways;

namespace Scada.Drivers.Tests;

public sealed class GatewayRuntimeConversionTests
{
    [Theory]
    [InlineData("pt-BR")]
    [InlineData("en")]
    [InlineData("es")]
    public async Task ExactPublishedGatewayCsvTransfersElevenToTwentySevenThroughActualRuntime(string locale)
    {
        var guide = ContextualHelpCatalog.Build(locale).Topics.Single(topic => topic.Id == "gateway.configure-test");
        var csv = Assert.Single(guide.Sections, section => section.Code is not null).Code!;
        var sourceId = Guid.NewGuid();
        var destinationId = Guid.NewGuid();
        var sourceTag = MemoryTag(sourceId, "Server.Source", TagDataType.Int16, (short)0);
        var destinationTag = MemoryTag(destinationId, "Server.Destination", TagDataType.Int32, 0);
        var current = new EngineeringPackage(EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion, DateTimeOffset.UtcNow,
            [sourceTag, destinationTag], [], DataSources: [new DataSourceEngineeringDto(
                Guid.NewGuid(), "memory.server", "Server Memory", InternalMemoryRuntimePlanner.ServerMemoryDriverKey)]);
        var route = Assert.Single(new EngineeringTabularExchange().ParseCsv(current,
            EngineeringTabularEntity.GatewayRoutes, csv).Gateways!);
        // Follow the documented Preview/Apply boundary: CSV references are readable
        // paths; Apply owns allocating stable route IDs and resolving TAG IDs.
        using var workspace = new EngineeringWorkspace();
        var routes = new InMemoryGatewayEngineeringRegistry();
        var exchange = new EngineeringExchangeService(workspace.Tags, workspace.Alarms,
            workspace.DataSources, workspace.Assets, workspace.Views, workspace.SecurityPolicies,
            workspace.Commands, routes, workspace.Scripts, workspace.VisualAssets);
        var candidate = current with { Gateways = [route] };
        var preview = exchange.Preview(candidate, ImportMode.CreateAndUpdate);
        Assert.True(preview.CanApply, string.Join("; ", preview.Items.SelectMany(item => item.Issues).Select(issue => issue.Message)));
        Assert.Empty(exchange.Apply(candidate, ImportMode.CreateAndUpdate).Issues);
        var package = exchange.ParseJson(exchange.ExportJson());
        var appliedRoute = Assert.Single(package.Gateways!);
        Assert.NotNull(appliedRoute.Id);
        Assert.NotEqual(Guid.Empty, appliedRoute.Id);
        Assert.Equal(sourceId, appliedRoute.SourceTagId);
        Assert.Equal(destinationId, appliedRoute.DestinationTagId);
        await using var runtime = CreateRuntime(new InMemoryScadaEventBus());
        var activation = await runtime.ActivateAsync("help-gateway", 1, package);
        Assert.True(activation.Activated, Describe(activation));
        await runtime.WriteAsync(sourceId, (short)11);
        await WaitForRouteActivityAsync(runtime, route.Key, TimeSpan.FromSeconds(2));
        var diagnostic = Assert.Single(runtime.GatewayDiagnostics(), item => item.Key == route.Key);
        Assert.Equal(1, diagnostic.TransferCount);
        Assert.Equal(0, diagnostic.WriteFailureCount);
        Assert.Equal(27, CurrentValue<int>(runtime, destinationId));
        Assert.Equal((short)11, CurrentValue<short>(runtime, sourceId));
    }

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
