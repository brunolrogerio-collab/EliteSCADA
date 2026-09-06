using Scada.Engineering.Contracts;
using Scada.Engineering.Security;
using Scada.Security.Authentication;
using Scada.Security.Authorization;

namespace Scada.Api.ProjectPackages;

public sealed record SystemRecoveryAuthorityAdmission(
    bool Allowed,
    bool EngineeringModify,
    bool Administration,
    string Reason);

public static class SystemRecoveryAuthorityAdmissionEvaluator
{
    public static SystemRecoveryAuthorityAdmission Evaluate(
        EngineeringPackage package,
        LocalUserAccount account)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(account);

        if (!account.IsEnabled)
        {
            return new SystemRecoveryAuthorityAdmission(
                false,
                false,
                false,
                "The restored local identity is disabled.");
        }

        IReadOnlyCollection<RolePolicy> policies;
        try
        {
            policies = SecurityPolicyCompiler.Compile(
                package.SecurityRoles ?? Array.Empty<SecurityRoleEngineeringDto>());
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return new SystemRecoveryAuthorityAdmission(
                false,
                false,
                false,
                "The project package security policy is invalid.");
        }

        var authorization = new InMemoryCapabilityAuthorizationService(policies);
        var principal = new SecurityPrincipal(
            account.Id.ToString(),
            account.DisplayName,
            LocalIdentityNormalization.NormalizeRoles(account.Roles),
            true);

        var engineeringModify = authorization
            .Evaluate(principal, SecurityCapability.EngineeringModify)
            .Allowed;
        var userRoleAdmin = authorization
            .Evaluate(principal, SecurityCapability.UserRoleAdmin)
            .Allowed;
        var systemAdmin = authorization
            .Evaluate(principal, SecurityCapability.SystemAdmin)
            .Allowed;
        var administration = userRoleAdmin || systemAdmin;

        if (!engineeringModify)
        {
            return new SystemRecoveryAuthorityAdmission(
                false,
                false,
                administration,
                "The restored identity is not granted EngineeringModify by the project package policy.");
        }

        if (!administration)
        {
            return new SystemRecoveryAuthorityAdmission(
                false,
                true,
                false,
                "The restored identity is not granted UserRoleAdmin or SystemAdmin by the project package policy.");
        }

        return new SystemRecoveryAuthorityAdmission(
            true,
            true,
            true,
            "The restored identity is authorized by the prospective project package policy.");
    }
}
