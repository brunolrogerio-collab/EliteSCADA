using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Scada.Api.Runtime;
using Scada.Api.Security;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;
using Scada.Engineering.Security;
using Scada.Security.Authorization;

namespace Scada.Drivers.Tests;

public sealed class EngineeringAuthorityCapabilityTests
{
    [Fact]
    public void EngineeringViewCanReadWithoutReceivingMutationAuthority()
    {
        using var workspace = new EngineeringWorkspace();
        workspace.SecurityPolicies.UpsertRole(new SecurityRoleEngineeringDto(
            Guid.NewGuid(),
            "engineering-reader",
            "Engineering Reader",
            Grants: [new CapabilityGrantEngineeringDto(SecurityCapability.EngineeringView)]));
        var exchange = new EngineeringExchangeService(
            workspace.Tags,
            workspace.Alarms,
            workspace.DataSources,
            workspace.Assets,
            workspace.Views,
            workspace.SecurityPolicies,
            workspace.Commands);
        var configuration = new ConfigurationManager { ["Authentication:Enabled"] = "true" };
        var security = new ApiAuthorizationService(
            new NullServiceProvider(),
            workspace,
            exchange,
            configuration);
        var context = AuthenticatedContext("engineering-reader");

        Assert.True(security.CheckWorkspace(context, SecurityCapability.EngineeringView).Allowed);
        Assert.False(security.CheckWorkspace(context, SecurityCapability.EngineeringModify).Allowed);
    }

    [Fact]
    public void BuiltInDeveloperRoleDoesNotReceiveHighAvailabilityAuthorityByEnumeration()
    {
        using var workspace = new EngineeringWorkspace();
        var developer = workspace.SecurityPolicies.FindRoleByKey("developer");

        Assert.NotNull(developer);
        Assert.Contains(developer!.Grants!, grant => grant.Capability == SecurityCapability.EngineeringView);
        Assert.DoesNotContain(developer.Grants!, grant =>
            grant.Capability is SecurityCapability.HighAvailabilityObserve or
                SecurityCapability.HighAvailabilityTransfer or
                SecurityCapability.HighAvailabilityAdmin);
    }

    [Fact]
    public void ScreenScopeFiltersProductionScreenListingByStableIdentityAfterRename()
    {
        using var workspace = new EngineeringWorkspace(seedDemo: false);
        var firstScreenId = Guid.Parse("81000000-0000-0000-0000-000000000001");
        var secondScreenId = Guid.Parse("81000000-0000-0000-0000-000000000002");
        var firstScopeId = Guid.Parse("81000000-0000-0000-0000-000000000011");
        workspace.Views.UpsertScreen(new ScreenEngineeringDto(firstScreenId, "overview", "Overview"));
        workspace.Views.UpsertScreen(new ScreenEngineeringDto(secondScreenId, "restricted", "Restricted"));
        workspace.SecurityPolicies.UpsertScope(new SecurityScopeEngineeringDto(
            firstScopeId,
            "screen-overview",
            "Overview",
            SecurityScopeNodeKind.Screen,
            ResourceId: firstScreenId));
        workspace.SecurityPolicies.UpsertScope(new SecurityScopeEngineeringDto(
            Guid.Parse("81000000-0000-0000-0000-000000000012"),
            "screen-restricted",
            "Restricted",
            SecurityScopeNodeKind.Screen,
            ResourceId: secondScreenId));
        workspace.SecurityPolicies.UpsertRole(new SecurityRoleEngineeringDto(
            Guid.NewGuid(),
            "screen-reader",
            "Screen Reader",
            Grants: new[]
            {
                new CapabilityGrantEngineeringDto(
                    SecurityCapability.EngineeringView,
                    new AuthorizationScopeEngineeringDto(ScopeNodeId: firstScopeId))
            }));
        var exchange = new EngineeringExchangeService(
            workspace.Tags,
            workspace.Alarms,
            workspace.DataSources,
            workspace.Assets,
            workspace.Views,
            workspace.SecurityPolicies,
            workspace.Commands);
        var configuration = new ConfigurationManager { ["Authentication:Enabled"] = "true" };
        var security = new ApiAuthorizationService(new NullServiceProvider(), workspace, exchange, configuration);
        var context = AuthenticatedContext("screen-reader");

        var readable = EngineeringScreenAuthorization.FilterReadable(
            context,
            security,
            workspace.Views.SnapshotScreens());

        var screen = Assert.Single(readable);
        Assert.Equal(firstScreenId, screen.Id);
        workspace.Views.UpsertScreen(screen with { Key = "overview-renamed", Name = "Overview renamed" });

        var afterRename = EngineeringScreenAuthorization.FilterReadable(
            context,
            security,
            workspace.Views.SnapshotScreens());

        Assert.Equal("overview-renamed", Assert.Single(afterRename).Key);
        Assert.DoesNotContain(afterRename, item => item.Id == secondScreenId);
    }

    private static DefaultHttpContext AuthenticatedContext(string role)
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", "authority-test-user"), new Claim("role", role)],
            authenticationType: "test"));
        return context;
    }

    private sealed class NullServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
