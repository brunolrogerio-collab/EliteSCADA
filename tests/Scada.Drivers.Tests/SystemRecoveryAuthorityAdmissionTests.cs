using Scada.Api.ProjectPackages;
using Scada.Engineering.Contracts;
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
