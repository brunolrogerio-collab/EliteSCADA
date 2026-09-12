using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Scada.Api.Libraries;
using Scada.Api.Runtime;
using Scada.Api.Security;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;
using Scada.Engineering.Security;
using Scada.Security.Authorization;

namespace Scada.Drivers.Tests;

public sealed class ReusableLibraryEndpointAccessTests
{
    [Fact]
    public void CheckAccess_DeniesAuthenticatedPrincipalWithoutEngineeringModifyBeforeLockEvaluation()
    {
        using var workspace = new EngineeringWorkspace();
        workspace.SecurityPolicies.UpsertRole(new SecurityRoleEngineeringDto(
            Guid.NewGuid(),
            "viewer",
            "Viewer",
            Grants: [new CapabilityGrantEngineeringDto(SecurityCapability.View)]));
        var exchange = CreateExchange(workspace);
        var security = CreateSecurity(workspace, exchange);
        var context = AuthenticatedContext("viewer");

        var access = ReusableLibraryEndpoints.CheckAccess(context, security, exchange);

        Assert.Equal("capability", access.Reason);
        var failure = Assert.IsAssignableFrom<IStatusCodeHttpResult>(access.Failure);
        Assert.Equal(StatusCodes.Status403Forbidden, failure.StatusCode);
    }

    [Fact]
    public void CheckAccess_DeniesEngineeringPrincipalWhenEngineeringLockIsLocked()
    {
        using var workspace = new EngineeringWorkspace();
        workspace.SecurityPolicies.UpsertRole(new SecurityRoleEngineeringDto(
            Guid.NewGuid(),
            "engineer",
            "Engineer",
            Grants: [new CapabilityGrantEngineeringDto(SecurityCapability.EngineeringModify)]));
        var exchange = CreateExchange(workspace);
        EngineeringLockAccess.Replace(
            exchange,
            new EngineeringLockSecretService().Configure("reusable-library-lock-secret", locked: true));
        var security = CreateSecurity(workspace, exchange);
        var context = AuthenticatedContext("engineer");

        var access = ReusableLibraryEndpoints.CheckAccess(context, security, exchange);

        Assert.True(access.Authorization.Allowed);
        Assert.Equal("engineering-lock", access.Reason);
        var failure = Assert.IsAssignableFrom<IStatusCodeHttpResult>(access.Failure);
        Assert.Equal(StatusCodes.Status403Forbidden, failure.StatusCode);
    }

    [Fact]
    public void CheckAccess_AllowsEngineeringPrincipalWhenUnlocked()
    {
        using var workspace = new EngineeringWorkspace();
        workspace.SecurityPolicies.UpsertRole(new SecurityRoleEngineeringDto(
            Guid.NewGuid(),
            "engineer",
            "Engineer",
            Grants: [new CapabilityGrantEngineeringDto(SecurityCapability.EngineeringModify)]));
        var exchange = CreateExchange(workspace);
        var security = CreateSecurity(workspace, exchange);
        var context = AuthenticatedContext("engineer");

        var access = ReusableLibraryEndpoints.CheckAccess(context, security, exchange);

        Assert.True(access.Authorization.Allowed);
        Assert.Null(access.Failure);
        Assert.Null(access.Reason);
    }

    [Fact]
    public void CheckAccess_AllowsReadOnlyEngineeringCapabilityWhenRequested()
    {
        using var workspace = new EngineeringWorkspace();
        workspace.SecurityPolicies.UpsertRole(new SecurityRoleEngineeringDto(
            Guid.NewGuid(),
            "engineering-reader",
            "Engineering Reader",
            Grants: [new CapabilityGrantEngineeringDto(SecurityCapability.EngineeringView)]));
        var exchange = CreateExchange(workspace);
        var security = CreateSecurity(workspace, exchange);
        var context = AuthenticatedContext("engineering-reader");

        var access = ReusableLibraryEndpoints.CheckAccess(
            context,
            security,
            exchange,
            SecurityCapability.EngineeringView);

        Assert.True(access.Authorization.Allowed);
        Assert.False(security.CheckWorkspace(context, SecurityCapability.EngineeringModify).Allowed);
        Assert.Null(access.Failure);
    }

    [Theory]
    [InlineData(ReusableLibraryEndpoints.CatalogRoute)]
    [InlineData(ReusableLibraryEndpoints.AssociateRoute)]
    [InlineData(ReusableLibraryEndpoints.ExportRoute)]
    [InlineData(ReusableLibraryEndpoints.InspectRoute)]
    [InlineData("/api/engineering/libraries/00000000-0000-0000-0000-000000000001/resources")]
    [InlineData("/api/engineering/libraries/00000000-0000-0000-0000-000000000001")]
    public void LibraryRoutes_AreNotEngineeringLockWorkspaceReadExemptions(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;

        Assert.False(EngineeringLockAccess.IsWorkspaceReadExempt(context.Request));
    }

    private static EngineeringExchangeService CreateExchange(EngineeringWorkspace workspace) =>
        new(
            workspace.Tags,
            workspace.Alarms,
            workspace.DataSources,
            workspace.Assets,
            workspace.Views,
            workspace.SecurityPolicies,
            workspace.Commands);

    private static ApiAuthorizationService CreateSecurity(
        EngineeringWorkspace workspace,
        IEngineeringExchangeService exchange)
    {
        var configuration = new ConfigurationManager
        {
            ["Authentication:Enabled"] = "true"
        };
        return new ApiAuthorizationService(
            new NullServiceProvider(),
            workspace,
            exchange,
            configuration);
    }

    private static DefaultHttpContext AuthenticatedContext(string role)
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("sub", "c25-library-user"),
                new Claim("role", role)
            ],
            authenticationType: "test"));
        return context;
    }

    private sealed class NullServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
