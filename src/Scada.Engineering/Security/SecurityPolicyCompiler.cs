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
                        : CompileScope(grant.Scope)))
                .ToArray());
    }

    private static AuthorizationScope CompileScope(AuthorizationScopeEngineeringDto scope)
    {
        // A non-null empty scope is malformed. Global authority is represented only
        // by a null scope; compile malformed objects to an impossible stable id so
        // callers that bypass import validation still fail closed.
        if (!scope.ScopeNodeId.HasValue && !AuthorityScopeEngineeringMigration.HasUnmigratedLegacyText(scope))
            return new AuthorizationScope(ScopeNodeId: Guid.Empty, IncludeDescendants: scope.IncludeDescendants);

        return new AuthorizationScope(
            scope.Area,
            scope.EquipmentPath,
            scope.ScreenKey,
            scope.TagPath,
            scope.CommandKey,
            scope.ScopeNodeId,
            scope.IncludeDescendants);
    }

    public static IReadOnlyCollection<RolePolicy> Compile(
        IEnumerable<SecurityRoleEngineeringDto> roles) =>
        roles.Select(Compile).ToArray();
}
