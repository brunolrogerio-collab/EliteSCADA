using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.DriverHost.Runtime;
using Scada.Drivers.Abstractions;
using Scada.Drivers.HostResources;
using Scada.Engineering.Contracts;

namespace Scada.Drivers.Tests;

public sealed partial class ZigbeeNativeRuntimeTests
{
    [Fact]
    public async Task EngineeringRuntime_ActivatesNativeZigbeeAndUsesReadbackAsProcessTruth()
    {
        var fixture = CreateEngineering();
        var resources = CreateCoordinatorRegistry("ti-zStack", "CC2652P2", NetworkIdentity);
        var session = new FakeNativeZigbeeSession { ReadValue = false, WriteReadback = true };
        var factory = new ZigbeeNativeCommunicationRuntimeFactory(
            resources,
            new HostResourceLeaseCoordinator(resources),
            new ZigbeeNativeSidecarDeploymentOptions("node", "sidecar", "state"),
            (_, _) => session);
        var components = new CommunicationDriverRuntimeComponentRegistry();
        components.Register(new CommunicationDriverRuntimeComponentRegistration(
            new ZigbeeNativeCommunicationRuntimePlanner(),
            factory,
            new ZigbeeNativeDriverDescriptorProvider().Descriptor));
        await using var runtime = new EngineeringRuntimeCoordinator(
            new InMemoryScadaEventBus(),
            new EngineeringDriverCompiler(components),
            TimeSpan.FromSeconds(3),
            communicationComponents: components);
        using var activationRequest = new CancellationTokenSource();

        var activation = await runtime.ActivateAsync("project-zigbee-l3", 1, fixture.Package, activationRequest.Token);

        Assert.True(activation.Activated,
            string.Join(" | ", activation.CompilationIssues.Select(issue => issue.Message)
                .Concat(activation.RuntimeIssues.Select(issue => issue.Message))));
        Assert.Equal(1, session.StartCount);
        Assert.Equal(1, session.ReadCount);
        Assert.True(runtime.TryGetCurrent(fixture.TagId, out var initial));
        Assert.False(Assert.IsType<bool>(initial!.Value));
        Assert.Equal(TagQuality.Good, initial.Quality);
        Assert.Contains(runtime.Tags(), tag => tag.Id == fixture.TagId && tag.DataSourceId == fixture.DataSource.Id);
        Assert.Equal(CommunicationDriverOperationalState.Healthy,
            Assert.Single(runtime.Describe().CommunicationDrivers).State);

        activationRequest.Cancel();
        await runtime.WriteAsync(fixture.TagId, true);

        Assert.Equal(1, session.WriteCount);
        Assert.True(runtime.TryGetCurrent(fixture.TagId, out var afterWrite));
        Assert.True(Assert.IsType<bool>(afterWrite!.Value));
        Assert.Equal("zigbee.native.write-readback", afterWrite.Source);
    }

    [Fact]
    public async Task Driver_CancellationDuringInitialReadStopsTheStartedSidecar()
    {
        var fixture = CreateEngineering();
        var plan = Assert.IsType<ZigbeeNativeCommunicationRuntimePlan>(
            new ZigbeeNativeCommunicationRuntimePlanner().Plan(fixture.Package, fixture.DataSource).Plan);
        var session = new FakeNativeZigbeeSession { ReadDelay = TimeSpan.FromSeconds(1) };
        await using var driver = new ZigbeeNativeCommunicationDriver(plan, CreateServices(() => true), session);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(25));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => driver.StartAsync(cancellation.Token));

        Assert.Equal(1, session.StartCount);
        Assert.Equal(1, session.StopCount);
        Assert.False(session.IsRunning);
        Assert.Equal(DriverState.Faulted, driver.Status.State);
    }
}
