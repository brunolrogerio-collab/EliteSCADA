using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Scada.Api.Runtime;
using Scada.Api.Security;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.DriverHost.Runtime;
using Scada.Drivers.Simulation;
using Scada.Engineering.ImportExport;
using Scada.Security.Authorization;

namespace Scada.Drivers.Tests;

public sealed class DistributedRuntimeFoundationTestsProductionAuthorization
{
    [Fact]
    public async Task VoluntaryViewer_PrivilegedIdentity_IsDeniedByProductionAuthorizationService()
    {
        var eventBus = new InMemoryScadaEventBus();
        using var workspace = new EngineeringWorkspace();
        using var fallback = new DemoRuntimeServices(eventBus);

        var writableTag = TagDefinition.Create(
            "Viewer downscope value",
            "Demo.ViewerDownscope.Value",
            TagDataType.Double,
            "builtin.simulation");

        await using var simulation = new SimulationDriver(
            fallback.Cache,
            fallback.Registry,
            new[]
            {
                new SimulationPoint(
                    writableTag,
                    SimulationSignalType.Constant,
                    ConstantValue: 12)
            },
            TimeSpan.FromMilliseconds(15));
        await simulation.StartAsync();

        await using var engineeringRuntime = new EngineeringRuntimeCoordinator(
            eventBus,
            new EngineeringDriverCompiler(),
            TimeSpan.FromSeconds(2));
        var runtime = new ScadaRuntimeFacade(fallback, simulation, engineeringRuntime);

        var exchange = new EngineeringExchangeService(workspace.Tags, workspace.Alarms);
        var configuration = new ConfigurationManager
        {
            ["Authentication:Enabled"] = "true"
        };
        var authorization = new ApiAuthorizationService(
            new EmptyServiceProvider(),
            workspace,
            exchange,
            configuration);

        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(
                new ClaimsIdentity(
                    new[]
                    {
                        new Claim("sub", "privileged-user"),
                        new Claim(ClaimTypes.Name, "Privileged User"),
                        new Claim("role", "developer")
                    },
                    authenticationType: "test"))
        };
        var principal = authorization.GetPrincipal(context);

        var baselineCommand = await authorization.CheckRuntimeAsync(
            principal,
            runtime,
            SecurityCapability.CommandExecute);
        var baselineWrite = await authorization.CheckRuntimeTagAsync(
            principal,
            runtime,
            writableTag,
            TagAccessOperation.Write);

        Assert.True(baselineCommand.Allowed);
        Assert.True(baselineWrite.Allowed);

        var lease = authorization.RuntimeSessions.Admit(
            principal.SubjectId,
            "browser-1",
            RuntimeConnectionClass.Viewer,
            runtime.Describe());
        context.Request.Headers[ApiAuthorizationService.RuntimeSessionHeaderName] =
            lease.SessionId.ToString("D");

        var effectiveCommand = await authorization.CheckRuntimeAsync(
            context,
            runtime,
            SecurityCapability.CommandExecute);
        var effectiveWrite = await authorization.CheckRuntimeTagAsync(
            context,
            runtime,
            writableTag,
            TagAccessOperation.Write);

        Assert.False(effectiveCommand.Allowed);
        Assert.Equal(SecurityCapability.CommandExecute, effectiveCommand.Decision?.Capability);
        Assert.False(effectiveWrite.Allowed);
        Assert.Equal(SecurityCapability.ProcessValueWrite, effectiveWrite.Decision?.Capability);
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
