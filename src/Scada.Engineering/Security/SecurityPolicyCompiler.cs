using Scada.Engineering.Contracts;
using Scada.Security.Authorization;

namespace Scada.Engineering.Security;

public static class SecurityPolicyCompiler
{
    public static RolePolicy Compile(SecurityRoleEngineeringDto role)
    {
        ArgumentNullException.ThrowIfNull(role);
        ArgumentException.ThrowIfNullOrWhiteSpace(role.Key);
        ArgumentException.ThrowIfNullOrWhiteSpace(role.Name);

        return new RolePolicy(
            role.Key,
            role.Name,
            (role.Grants ?? Array.Empty<CapabilityGrantEngineeringDto>())
                .Select(grant => new CapabilityGrant(
                    grant.Capability,
                    grant.Scope is null
                        ? null
                        : new AuthorizationScope(
                            grant.Scope.Area,
                            grant.Scope.EquipmentPath,
                            grant.Scope.ScreenKey,
                            grant.Scope.TagPath,
                            grant.Scope.CommandKey)))
                .ToArray());
    }

    public static IReadOnlyCollection<RolePolicy> Compile(
        IEnumerable<SecurityRoleEngineeringDto> roles) =>
        roles.Select(Compile).ToArray();
}
