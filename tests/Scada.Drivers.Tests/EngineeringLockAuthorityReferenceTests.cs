using Scada.Api.Runtime;
using Scada.Api.Security;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;
using Scada.Engineering.Security;
using Scada.Security.Authorization;

namespace Scada.Drivers.Tests;

public sealed class EngineeringLockAuthorityReferenceTests
{
    [Fact]
    public async Task Replace_PreservesExactAuthorityReferenceWithoutMutatingAuthorityPolicy()
    {
        using var workspace = new EngineeringWorkspace(seedDemo: false);
        var authority = new InMemoryAuthorityPolicyStore();
        var role = new SecurityRoleEngineeringDto(
            Guid.Parse("47000000-0000-0000-0000-000000000001"),
            "developer",
            "Developer",
            Grants: [new CapabilityGrantEngineeringDto(SecurityCapability.EngineeringModify)]);
        var scope = new SecurityScopeEngineeringDto(
            Guid.Parse("47000000-0000-0000-0000-000000000002"),
            "engineering",
            "Engineering",
            SecurityScopeNodeKind.Plant);
        var write = await authority.TryReplaceAsync(0, [role], [scope]);
        Assert.True(write.Applied);

        var exchange = new EngineeringExchangeService(
            workspace.Tags,
            workspace.Alarms,
            workspace.DataSources,
            workspace.Assets,
            workspace.Views,
            new AuthorityPolicyRegistryView(authority));
        var beforePackage = exchange.ExportPackage();
        var beforeAuthority = authority.Snapshot();
        var configuredLock = new EngineeringLockSecretService().Configure("authority-reference-lock", locked: true);

        EngineeringLockAccess.Replace(exchange, configuredLock);

        var afterPackage = exchange.ExportPackage();
        var afterAuthority = authority.Snapshot();
        Assert.Equal(configuredLock, afterPackage.EngineeringLock);
        var beforeReference = Assert.IsType<AuthorityPolicyReferenceEngineeringDto>(beforePackage.AuthorityPolicyReference);
        var afterReference = Assert.IsType<AuthorityPolicyReferenceEngineeringDto>(afterPackage.AuthorityPolicyReference);
        Assert.Equal(beforeReference.Contract, afterReference.Contract);
        Assert.Equal(beforeReference.ContractVersion, afterReference.ContractVersion);
        Assert.Equal(beforeReference.PolicyVersion, afterReference.PolicyVersion);
        Assert.Equal(beforeReference.RoleIds.Order(), afterReference.RoleIds.Order());
        Assert.Equal(beforeReference.ScopeIds.Order(), afterReference.ScopeIds.Order());
        Assert.Equal(beforeAuthority.Version, afterAuthority.Version);
        Assert.Equal(beforeAuthority.Roles.OrderBy(role => role.Id), afterAuthority.Roles.OrderBy(role => role.Id));
        Assert.Equal(beforeAuthority.Scopes.OrderBy(scope => scope.Id), afterAuthority.Scopes.OrderBy(scope => scope.Id));
        Assert.Empty(afterPackage.SecurityRoles ?? Array.Empty<SecurityRoleEngineeringDto>());
        Assert.Empty(afterPackage.SecurityScopes ?? Array.Empty<SecurityScopeEngineeringDto>());
    }

    [Fact]
    public async Task Preview_StillFailsClosedForInvalidAuthorityReferenceAfterLockReplacement()
    {
        using var workspace = new EngineeringWorkspace(seedDemo: false);
        var authority = new InMemoryAuthorityPolicyStore();
        var role = new SecurityRoleEngineeringDto(
            Guid.Parse("47000000-0000-0000-0000-000000000011"),
            "developer",
            "Developer",
            Grants: [new CapabilityGrantEngineeringDto(SecurityCapability.EngineeringModify)]);
        var write = await authority.TryReplaceAsync(0, [role], Array.Empty<SecurityScopeEngineeringDto>());
        Assert.True(write.Applied);

        var exchange = new EngineeringExchangeService(
            workspace.Tags,
            workspace.Alarms,
            workspace.DataSources,
            workspace.Assets,
            workspace.Views,
            new AuthorityPolicyRegistryView(authority));
        EngineeringLockAccess.Replace(exchange, new EngineeringLockSecretService().Configure("authority-reference-lock"));
        var invalid = exchange.ExportPackage() with
        {
            AuthorityPolicyReference = exchange.ExportPackage().AuthorityPolicyReference! with
            {
                RoleIds = [Guid.Parse("47000000-0000-0000-0000-000000000099")]
            }
        };

        var preview = exchange.Preview(invalid, ImportMode.UpdateExisting);

        Assert.False(preview.CanApply);
        Assert.Contains(preview.Items.SelectMany(item => item.Issues), issue =>
            issue.IsError && issue.Code == "SECURITY_AUTHORITY_POLICY_REFERENCE_MISMATCH");
    }
}
