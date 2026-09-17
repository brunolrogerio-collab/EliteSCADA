using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
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

        // A modified authenticated REST client cannot discard Runtime session identity to
        // recover its baseline mutation Authority.
        var missingSessionCommand = await authorization.CheckRuntimeAsync(
            context,
            runtime,
            SecurityCapability.CommandExecute);
        var missingSessionWrite = await authorization.CheckRuntimeTagAsync(
            context,
            runtime,
            writableTag,
            TagAccessOperation.Write);
        Assert.False(missingSessionCommand.Allowed);
        Assert.Equal(SecurityCapability.CommandExecute, missingSessionCommand.Decision?.Capability);
        Assert.False(missingSessionWrite.Allowed);
        Assert.Equal(SecurityCapability.ProcessValueWrite, missingSessionWrite.Decision?.Capability);

        var lease = await authorization.RuntimeSessions.AdmitAsync(
            principal.SubjectId,
            "browser-1",
            RuntimeConnectionClass.Viewer,
            runtime.Describe());

        // WebSocket admission uses the same lease identity as REST and rejects direct or
        // modified clients before the socket is accepted.
        var missingSocketLease = await RuntimeSessionWebSocketAdmission.ValidateAsync(
            authorization,
            principal,
            runtime,
            StringValues.Empty,
            new StringValues(lease.ClientInstanceId));
        var partialSocketLease = await RuntimeSessionWebSocketAdmission.ValidateAsync(
            authorization,
            principal,
            runtime,
            new StringValues(lease.SessionId.ToString("D")),
            StringValues.Empty);
        var tamperedSocketLease = await RuntimeSessionWebSocketAdmission.ValidateAsync(
            authorization,
            principal,
            runtime,
            new StringValues(lease.SessionId.ToString("D")),
            new StringValues("tampered-browser"));
        var validSocketLease = await RuntimeSessionWebSocketAdmission.ValidateAsync(
            authorization,
            principal,
            runtime,
            new StringValues(lease.SessionId.ToString("D")),
            new StringValues(lease.ClientInstanceId));
        Assert.False(missingSocketLease.IsValid);
        Assert.Equal("invalid-runtime-session-identity", missingSocketLease.FailureCode);
        Assert.False(partialSocketLease.IsValid);
        Assert.Equal("invalid-runtime-session-identity", partialSocketLease.FailureCode);
        Assert.False(tamperedSocketLease.IsValid);
        Assert.Equal("session-client-mismatch", tamperedSocketLease.FailureCode);
        Assert.True(validSocketLease.IsValid);
        Assert.Equal(lease.SessionId, validSocketLease.Lease!.SessionId);
        Assert.True(RuntimeSessionWebSocketAdmission.IsLegacyEngineeringSocket(
            StringValues.Empty,
            StringValues.Empty));
        Assert.False(RuntimeSessionWebSocketAdmission.IsLegacyEngineeringSocket(
            new StringValues(lease.SessionId.ToString("D")),
            StringValues.Empty));

        context.Request.Headers[ApiAuthorizationService.RuntimeSessionHeaderName] =
            lease.SessionId.ToString("D");
        context.Request.Headers[ApiAuthorizationService.RuntimeSessionClientInstanceHeaderName] =
            lease.ClientInstanceId;

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

        // A lease-bearing request without its logical client identity cannot fall back to
        // an Interactive session decision.
        context.Request.Headers.Remove(ApiAuthorizationService.RuntimeSessionClientInstanceHeaderName);
        var missingClientWrite = await authorization.CheckRuntimeTagAsync(
            context,
            runtime,
            writableTag,
            TagAccessOperation.Write);
        Assert.False(missingClientWrite.Allowed);
        Assert.Equal(SecurityCapability.ProcessValueWrite, missingClientWrite.Decision?.Capability);

        // A modified REST client cannot replay the lease with a different logical client identity.
        context.Request.Headers[ApiAuthorizationService.RuntimeSessionClientInstanceHeaderName] =
            "tampered-browser";
        var tamperedWrite = await authorization.CheckRuntimeTagAsync(
            context,
            runtime,
            writableTag,
            TagAccessOperation.Write);
        Assert.False(tamperedWrite.Allowed);
        Assert.Equal(SecurityCapability.ProcessValueWrite, tamperedWrite.Decision?.Capability);
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
