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
    public void BuiltInDeveloperRoleHasFullAdministrativeAndIntelligenceAccess()
    {
        using var workspace = new EngineeringWorkspace();
        var developer = workspace.SecurityPolicies.FindRoleByKey("developer");

        Assert.NotNull(developer);
        Assert.Equal(Enum.GetValues<SecurityCapability>().OrderBy(capability => capability),
            developer!.Grants!.Select(grant => grant.Capability).OrderBy(capability => capability));
        Assert.Contains(developer!.Grants!, grant => grant.Capability == SecurityCapability.EngineeringView);
        Assert.Contains(developer.Grants!, grant => grant.Capability == SecurityCapability.HighAvailabilityObserve);
        Assert.Contains(developer.Grants!, grant => grant.Capability == SecurityCapability.HighAvailabilityTransfer);
        Assert.Contains(developer.Grants!, grant => grant.Capability == SecurityCapability.HighAvailabilityAdmin);
        Assert.Contains(developer.Grants!, grant => grant.Capability == SecurityCapability.SystemAdmin);
        Assert.Contains(developer.Grants!, grant =>
            grant.Capability == SecurityCapability.AssetProcessIntelligenceManage && grant.Scope is null);

        var exchange = new EngineeringExchangeService(
            workspace.Tags,
            workspace.Alarms,
            workspace.DataSources,
            workspace.Assets,
            workspace.Views,
            workspace.SecurityPolicies,
            workspace.Commands);
        var security = new ApiAuthorizationService(
            new NullServiceProvider(),
            workspace,
            exchange,
            new ConfigurationManager { ["Authentication:Enabled"] = "true" });
        var context = AuthenticatedContext("developer");

        Assert.True(security.CheckWorkspace(context, SecurityCapability.HighAvailabilityObserve).Allowed);
        Assert.True(security.CheckWorkspace(context, SecurityCapability.HighAvailabilityAdmin).Allowed);
        Assert.True(security.CheckWorkspace(context, SecurityCapability.UserRoleAdmin).Allowed);
        Assert.True(security.CheckWorkspace(context, SecurityCapability.EngineeringModify).Allowed);
        Assert.True(security.CheckWorkspace(context, SecurityCapability.AssetProcessIntelligenceManage).Allowed);
        Assert.True(security.CheckWorkspace(
            context,
            SecurityCapability.AssetProcessIntelligenceManage,
            new AuthorizationResource(
                ResourceKind: AuthorizationResourceKind.Equipment,
                ResourceId: Guid.NewGuid())).Allowed);
    }

    [Fact]
    public async Task IntelligenceManagementRequiresItsExplicitGrantAndSupportsGlobalNonAdministratorRoles()
    {
        var role = new SecurityRoleEngineeringDto(
            Guid.NewGuid(), "process-engineer", "Process Engineer",
            Grants: [new CapabilityGrantEngineeringDto(SecurityCapability.SystemAdmin)]);
        var store = new InMemoryAuthorityPolicyStore([role]);
        var security = new ApiAuthorizationService(new NullServiceProvider(), store,
            new ConfigurationManager { ["Authentication:Enabled"] = "true" });
        var context = AuthenticatedContext(role.Key);
        var equipment = new AuthorizationResource(
            ResourceKind: AuthorizationResourceKind.Equipment, ResourceId: Guid.NewGuid());

        Assert.False(security.CheckWorkspace(context, SecurityCapability.AssetProcessIntelligenceManage).Allowed);
        Assert.False(security.CheckWorkspace(context, SecurityCapability.AssetProcessIntelligenceManage, equipment).Allowed);

        var granted = await store.TryReplaceAsync(0,
            [role with { Grants = [new CapabilityGrantEngineeringDto(SecurityCapability.AssetProcessIntelligenceManage)] }], []);
        Assert.True(granted.Applied);
        Assert.True(security.CheckWorkspace(context, SecurityCapability.AssetProcessIntelligenceManage).Allowed);
        Assert.True(security.CheckWorkspace(context, SecurityCapability.AssetProcessIntelligenceManage, equipment).Allowed);
        Assert.False(security.CheckWorkspace(context, SecurityCapability.SystemAdmin).Allowed);

        var revoked = await store.TryReplaceAsync(granted.Snapshot.Version, [role with { Grants = [] }], []);
        Assert.True(revoked.Applied);
        Assert.False(security.CheckWorkspace(context, SecurityCapability.AssetProcessIntelligenceManage, equipment).Allowed);
    }

    [Fact]
    public void ServerScriptRichCommandAuthorizationRequiresTheExactCommandScope()
    {
        const string projectKey = "project-alpha";
        var scriptId = Guid.Parse("81000000-0000-0000-0000-000000000021");
        var commandId = Guid.Parse("81000000-0000-0000-0000-000000000022");
        var otherCommandId = Guid.Parse("81000000-0000-0000-0000-000000000023");
        var commandScopeId = Guid.Parse("81000000-0000-0000-0000-000000000024");
        var roleKey = ServerScriptCommandIdentity.RoleKey(projectKey, scriptId);
        var scope = new SecurityScopeEngineeringDto(
            commandScopeId,
            "rich-command-alpha",
            "Rich Command Alpha",
            SecurityScopeNodeKind.Command,
            ResourceId: commandId);
        var role = new SecurityRoleEngineeringDto(
            Guid.NewGuid(),
            roleKey,
            "Server Script Alpha",
            Grants:
            [new CapabilityGrantEngineeringDto(
                SecurityCapability.CommandExecute,
                new AuthorizationScopeEngineeringDto(ScopeNodeId: commandScopeId))]);
        var policies = new InMemoryAuthorityPolicyStore([role], [scope]);
        var security = new ApiAuthorizationService(
            new NullServiceProvider(),
            policies,
            new ConfigurationManager { ["Authentication:Enabled"] = "true" });

        var allowed = security.CheckServerScriptCommand(projectKey, 1, scriptId, commandId);
        var notGranted = security.CheckServerScriptCommand(projectKey, 1, scriptId, otherCommandId);

        Assert.True(allowed.Allowed);
        Assert.Equal(roleKey, Assert.Single(allowed.Principal.Roles));
        Assert.False(notGranted.Allowed);
    }

    [Fact]
    public void ServerScriptRichCommandAuthorizationRejectsGlobalAndDescendantGrants()
    {
        const string projectKey = "project-alpha";
        var scriptId = Guid.Parse("81000000-0000-0000-0000-000000000031");
        var commandId = Guid.Parse("81000000-0000-0000-0000-000000000032");
        var commandScopeId = Guid.Parse("81000000-0000-0000-0000-000000000033");
        var scope = new SecurityScopeEngineeringDto(
            commandScopeId,
            "rich-command-alpha",
            "Rich Command Alpha",
            SecurityScopeNodeKind.Command,
            ResourceId: commandId);
        var configuration = new ConfigurationManager { ["Authentication:Enabled"] = "true" };

        foreach (var grantScope in new AuthorizationScopeEngineeringDto?[]
                 {
                     null,
                     new AuthorizationScopeEngineeringDto(ScopeNodeId: commandScopeId, IncludeDescendants: true)
                 })
        {
            var role = new SecurityRoleEngineeringDto(
                Guid.NewGuid(),
                ServerScriptCommandIdentity.RoleKey(projectKey, scriptId),
                "Server Script Alpha",
                Grants:
                [new CapabilityGrantEngineeringDto(SecurityCapability.CommandExecute, grantScope)]);
            var security = new ApiAuthorizationService(
                new NullServiceProvider(),
                new InMemoryAuthorityPolicyStore([role], [scope]),
                configuration);

            Assert.False(security.CheckServerScriptCommand(projectKey, 1, scriptId, commandId).Allowed);
        }
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
