using Scada.Engineering.Contracts;
using Scada.Engineering.Security;
using Scada.Security.Authentication;
using Scada.Security.Authorization;

namespace Scada.Api.ProjectPackages;

public sealed record SystemRecoveryAuthorityAdmission(
    bool Allowed,
    bool BootstrapAuthority,
    bool EngineeringModify,
    bool Administration,
    string Reason);

public static class SystemRecoveryAuthorityAdmissionEvaluator
{
    public static SystemRecoveryAuthorityAdmission Evaluate(
        EngineeringPackage package,
        LocalUserAccount account,
        AuthorityPolicySnapshot? authority = null)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(account);

        if (!account.IsEnabled)
        {
            return new SystemRecoveryAuthorityAdmission(
                false,
                false,
                false,
                false,
                "The restored local identity is disabled.");
        }

        var bootstrapAuthority = account.Roles.Any(role =>
            string.Equals(
                role,
                LocalIdentityBootstrapService.InitialAdministratorRole,
                StringComparison.OrdinalIgnoreCase));
        if (!bootstrapAuthority)
        {
            return new SystemRecoveryAuthorityAdmission(
                false,
                false,
                false,
                false,
                $"The restored identity is not assigned the Authority bootstrap administrator role '{LocalIdentityBootstrapService.InitialAdministratorRole}'.");
        }

        var packageRoles = package.SecurityRoles ?? Array.Empty<SecurityRoleEngineeringDto>();
        if (package.AuthorityPolicyReference is { } reference)
        {
            if (!IsExactAuthorityReference(reference, authority))
            {
                return new SystemRecoveryAuthorityAdmission(
                    false,
                    true,
                    false,
                    false,
                    "The recovered Application Authority reference does not match the restored Security Authority.");
            }

            // Modern Engineering packages intentionally carry only stable Authority
            // identities. Resolve their grants from the restored canonical Authority;
            // treating the empty package-owned SecurityRoles collection as policy would
            // incorrectly reject every modern recovery Administrator.
            packageRoles = authority!.Roles;
        }

        IReadOnlyCollection<RolePolicy> policies;
        try
        {
            policies = SecurityPolicyCompiler.Compile(
                packageRoles);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return new SystemRecoveryAuthorityAdmission(
                false,
                true,
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
                true,
                false,
                administration,
                "The restored bootstrap Administrator is not granted EngineeringModify by the project package policy.");
        }

        if (!administration)
        {
            return new SystemRecoveryAuthorityAdmission(
                false,
                true,
                true,
                false,
                "The restored bootstrap Administrator is not granted UserRoleAdmin or SystemAdmin by the project package policy.");
        }

        return new SystemRecoveryAuthorityAdmission(
            true,
            true,
            true,
            true,
            "The restored Authority bootstrap Administrator is authorized by the prospective project package policy.");
    }

    private static bool IsExactAuthorityReference(
        AuthorityPolicyReferenceEngineeringDto reference,
        AuthorityPolicySnapshot? authority)
    {
        if (authority is null ||
            !string.Equals(reference.Contract, AuthorityPolicyContract.Schema, StringComparison.Ordinal) ||
            reference.ContractVersion != AuthorityPolicyContract.SchemaVersion ||
            reference.PolicyVersion != authority.Version ||
            reference.RoleIds is null ||
            reference.ScopeIds is null ||
            reference.RoleIds.Any(id => id == Guid.Empty) ||
            reference.ScopeIds.Any(id => id == Guid.Empty))
        {
            return false;
        }

        var authorityRoleIds = authority.Roles
            .Where(role => role.Id.HasValue)
            .Select(role => role.Id!.Value)
            .ToHashSet();
        var authorityScopeIds = authority.Scopes.Select(scope => scope.Id).ToHashSet();
        return reference.RoleIds.Distinct().Count() == reference.RoleIds.Count &&
               reference.ScopeIds.Distinct().Count() == reference.ScopeIds.Count &&
               reference.RoleIds.ToHashSet().SetEquals(authorityRoleIds) &&
               reference.ScopeIds.ToHashSet().SetEquals(authorityScopeIds);
    }
}
