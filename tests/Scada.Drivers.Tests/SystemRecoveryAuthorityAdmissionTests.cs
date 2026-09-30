using Scada.Api.ProjectPackages;
using Scada.Engineering.Contracts;
using Scada.Engineering.Security;
using Scada.Security.Authentication;
using Scada.Security.Authorization;

namespace Scada.Drivers.Tests;

public sealed class SystemRecoveryAuthorityAdmissionTests
{
    [Fact]
    public void Evaluate_AllowsOnlyWhenPackageGrantsEngineeringAndAdministration()
    {
        var admission = SystemRecoveryAuthorityAdmissionEvaluator.Evaluate(
            PackageWithRole(
                "developer",
                SecurityCapability.EngineeringModify,
                SecurityCapability.UserRoleAdmin),
            Account("developer"));

        Assert.True(admission.Allowed);
        Assert.True(admission.EngineeringModify);
        Assert.True(admission.Administration);
    }

    [Fact]
    public void Evaluate_DoesNotTrustDeveloperRoleNameWithoutGrants()
    {
        var admission = SystemRecoveryAuthorityAdmissionEvaluator.Evaluate(
            PackageWithRole("developer"),
            Account("developer"));

        Assert.False(admission.Allowed);
        Assert.False(admission.EngineeringModify);
        Assert.False(admission.Administration);
    }

    [Fact]
    public void Evaluate_RejectsAdministrationWithoutEngineeringModify()
    {
        var admission = SystemRecoveryAuthorityAdmissionEvaluator.Evaluate(
            PackageWithRole("developer", SecurityCapability.SystemAdmin),
            Account("developer"));

        Assert.False(admission.Allowed);
        Assert.False(admission.EngineeringModify);
        Assert.True(admission.Administration);
    }

    [Fact]
    public void Evaluate_RejectsEngineeringModifyWithoutAdministration()
    {
        var admission = SystemRecoveryAuthorityAdmissionEvaluator.Evaluate(
            PackageWithRole("developer", SecurityCapability.EngineeringModify),
            Account("developer"));

        Assert.False(admission.Allowed);
        Assert.True(admission.EngineeringModify);
        Assert.False(admission.Administration);
    }

    [Fact]
    public void Evaluate_RejectsDisabledRestoredIdentityBeforePolicyEvaluation()
    {
        var admission = SystemRecoveryAuthorityAdmissionEvaluator.Evaluate(
            PackageWithRole(
                "developer",
                SecurityCapability.EngineeringModify,
                SecurityCapability.UserRoleAdmin),
            Account("developer") with { IsEnabled = false });

        Assert.False(admission.Allowed);
        Assert.False(admission.EngineeringModify);
        Assert.False(admission.Administration);
    }

    [Fact]
    public void Evaluate_ResolvesModernAuthorityReferenceAgainstRestoredCanonicalPolicy()
    {
        var roleId = Guid.Parse("93000000-0000-0000-0000-000000000011");
        var authority = new InMemoryAuthorityPolicyStore(
        [
            new SecurityRoleEngineeringDto(
                roleId,
                "developer",
                "Developer",
                Grants:
                [
                    new CapabilityGrantEngineeringDto(SecurityCapability.EngineeringModify),
                    new CapabilityGrantEngineeringDto(SecurityCapability.UserRoleAdmin)
                ])
        ]).Snapshot();
        var package = EmptyPackage() with
        {
            AuthorityPolicyReference = new AuthorityPolicyReferenceEngineeringDto(
                AuthorityPolicyContract.Schema,
                AuthorityPolicyContract.SchemaVersion,
                authority.Version,
                [roleId],
                []),
            SecurityRoles = []
        };

        var admission = SystemRecoveryAuthorityAdmissionEvaluator.Evaluate(
            package,
            Account("developer"),
            authority);

        Assert.True(admission.Allowed);
        Assert.True(admission.EngineeringModify);
        Assert.True(admission.Administration);
    }

    [Fact]
    public void Evaluate_RejectsModernAuthorityReferenceThatDoesNotMatchRestoredPolicy()
    {
        var roleId = Guid.Parse("93000000-0000-0000-0000-000000000012");
        var authority = new InMemoryAuthorityPolicyStore(
        [
            new SecurityRoleEngineeringDto(
                roleId,
                "developer",
                "Developer",
                Grants:
                [
                    new CapabilityGrantEngineeringDto(SecurityCapability.EngineeringModify),
                    new CapabilityGrantEngineeringDto(SecurityCapability.UserRoleAdmin)
                ])
        ]).Snapshot();
        var package = EmptyPackage() with
        {
            AuthorityPolicyReference = new AuthorityPolicyReferenceEngineeringDto(
                AuthorityPolicyContract.Schema,
                AuthorityPolicyContract.SchemaVersion,
                authority.Version + 1,
                [roleId],
                [])
        };

        var admission = SystemRecoveryAuthorityAdmissionEvaluator.Evaluate(
            package,
            Account("developer"),
            authority);

        Assert.False(admission.Allowed);
        Assert.Contains("does not match", admission.Reason, StringComparison.Ordinal);
    }

    private static EngineeringPackage PackageWithRole(
        string roleKey,
        params SecurityCapability[] capabilities) =>
        new(
            "scada.engineering",
            16,
            new DateTimeOffset(2026, 9, 6, 4, 0, 0, TimeSpan.Zero),
            Array.Empty<TagEngineeringDto>(),
            Array.Empty<AlarmEngineeringDto>(),
            SecurityRoles:
            [
                new SecurityRoleEngineeringDto(
                    Guid.Parse("93000000-0000-0000-0000-000000000001"),
                    roleKey,
                    "Recovery Administrator",
                    Grants: capabilities
                        .Select(capability => new CapabilityGrantEngineeringDto(capability))
                        .ToArray())
            ]);

    private static EngineeringPackage EmptyPackage() =>
        new(
            "scada.engineering",
            20,
            new DateTimeOffset(2026, 9, 6, 4, 0, 0, TimeSpan.Zero),
            Array.Empty<TagEngineeringDto>(),
            Array.Empty<AlarmEngineeringDto>());

    private static LocalUserAccount Account(params string[] roles)
    {
        var timestamp = new DateTimeOffset(2026, 9, 6, 4, 0, 0, TimeSpan.Zero);
        return new LocalUserAccount(
            Guid.Parse("93000000-0000-0000-0000-000000000002"),
            "administrator",
            LocalIdentityNormalization.NormalizeUsername("administrator"),
            "Administrator",
            true,
            roles,
            new PasswordCredential(new byte[32], new byte[32], 100_000),
            timestamp,
            timestamp);
    }
}
