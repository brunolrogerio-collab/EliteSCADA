using Scada.Engineering.Contracts;
using Scada.Security.Authorization;

namespace Scada.Engineering.Security;

internal static class AuthorityPolicyEngineeringMigration
{
    public const int EngineeringViewIntroducedSchemaVersion = 17;

    public static IReadOnlyCollection<SecurityRoleEngineeringDto> NormalizeRoles(
        IReadOnlyCollection<SecurityRoleEngineeringDto>? roles,
        int sourceSchemaVersion)
    {
        var source = roles ?? Array.Empty<SecurityRoleEngineeringDto>();
        if (sourceSchemaVersion >= EngineeringViewIntroducedSchemaVersion)
            return source;

        return source.Select(AddLegacyEngineeringView).ToArray();
    }

    private static SecurityRoleEngineeringDto AddLegacyEngineeringView(SecurityRoleEngineeringDto role)
    {
        var grants = (role.Grants ?? Array.Empty<CapabilityGrantEngineeringDto>()).ToList();
        foreach (var legacyGrant in grants
                     .Where(grant => grant.Capability == SecurityCapability.EngineeringModify)
                     .ToArray())
        {
            if (grants.Any(grant =>
                    grant.Capability == SecurityCapability.EngineeringView &&
                    SameScope(grant.Scope, legacyGrant.Scope)))
            {
                continue;
            }

            grants.Add(new CapabilityGrantEngineeringDto(
                SecurityCapability.EngineeringView,
                legacyGrant.Scope,
                legacyGrant.Metadata is null
                    ? null
                    : new Dictionary<string, string>(legacyGrant.Metadata)));
        }

        return role with { Grants = grants.ToArray() };
    }

    private static bool SameScope(
        AuthorizationScopeEngineeringDto? left,
        AuthorizationScopeEngineeringDto? right)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is null || right is null) return false;

        return Same(left.Area, right.Area) &&
               Same(left.EquipmentPath, right.EquipmentPath) &&
               Same(left.ScreenKey, right.ScreenKey) &&
               Same(left.TagPath, right.TagPath) &&
               Same(left.CommandKey, right.CommandKey);
    }

    private static bool Same(string? left, string? right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
