using System.Security.Cryptography;
using System.Text;
using Scada.Engineering.Contracts;

namespace Scada.Engineering.Security;

public static class AuthorityScopeEngineeringMigration
{
    public const int StableScopeSchemaVersion = 18;

    public static EngineeringPackage Normalize(EngineeringPackage package)
    {
        if (package.SchemaVersion >= StableScopeSchemaVersion)
            return package with { SecurityScopes = package.SecurityScopes ?? Array.Empty<SecurityScopeEngineeringDto>() };

        var scopes = (package.SecurityScopes ?? Array.Empty<SecurityScopeEngineeringDto>()).ToList();
        var roles = (package.SecurityRoles ?? Array.Empty<SecurityRoleEngineeringDto>())
            .Select(role => MigrateRole(role, package, scopes))
            .ToArray();
        return package with { SecurityRoles = roles, SecurityScopes = scopes.ToArray() };
    }

    public static bool HasUnmigratedLegacyText(AuthorizationScopeEngineeringDto? scope) =>
        scope is not null && (scope.Area is not null || scope.EquipmentPath is not null ||
                              scope.ScreenKey is not null || scope.TagPath is not null ||
                              scope.CommandKey is not null);

    public static bool HasUnmigratedLegacyScopes(IEnumerable<SecurityRoleEngineeringDto>? roles) =>
        (roles ?? Array.Empty<SecurityRoleEngineeringDto>()).Any(role =>
            (role.Grants ?? Array.Empty<CapabilityGrantEngineeringDto>())
            .Any(grant => HasUnmigratedLegacyText(grant.Scope)));

    private static SecurityRoleEngineeringDto MigrateRole(
        SecurityRoleEngineeringDto role,
        EngineeringPackage package,
        List<SecurityScopeEngineeringDto> scopes)
    {
        var grants = (role.Grants ?? Array.Empty<CapabilityGrantEngineeringDto>())
            .Select(grant => grant with { Scope = MigrateScope(grant.Scope, package, scopes) })
            .ToArray();
        return role with { Grants = grants };
    }

    private static AuthorizationScopeEngineeringDto? MigrateScope(
        AuthorizationScopeEngineeringDto? scope,
        EngineeringPackage package,
        List<SecurityScopeEngineeringDto> scopes)
    {
        if (!HasUnmigratedLegacyText(scope) || scope!.ScopeNodeId.HasValue)
            return scope;

        var candidates = Candidates(scope, package).ToArray();
        if (candidates.Length != 1) return scope;

        var candidate = candidates[0];
        var node = scopes.FirstOrDefault(existing =>
            existing.Kind == candidate.Kind && existing.ResourceId == candidate.ResourceId);
        if (node is null)
        {
            node = new SecurityScopeEngineeringDto(
                StableNodeId(candidate.Kind, candidate.ResourceId),
                $"legacy-{candidate.Kind.ToString().ToLowerInvariant()}-{candidate.ResourceId:N}",
                candidate.Name,
                candidate.Kind,
                ResourceId: candidate.ResourceId);
            scopes.Add(node);
        }

        return new AuthorizationScopeEngineeringDto(
            ScopeNodeId: node.Id,
            IncludeDescendants: false);
    }

    private static IEnumerable<LegacyResource> Candidates(
        AuthorizationScopeEngineeringDto scope,
        EngineeringPackage package)
    {
        var populatedDimensions = new[]
        {
            scope.Area, scope.EquipmentPath, scope.ScreenKey, scope.TagPath, scope.CommandKey
        }.Count(value => value is not null);
        if (populatedDimensions != 1) yield break;

        if (scope.TagPath is { } tagPath)
        {
            foreach (var tag in (package.Tags ?? Array.Empty<TagEngineeringDto>())
                         .Where(tag => tag is not null && tag.Id.HasValue &&
                                       tag.Path.Equals(tagPath, StringComparison.OrdinalIgnoreCase)))
                yield return new(SecurityScopeNodeKind.Tag, tag!.Id!.Value, tag.Name);
            yield break;
        }

        if (scope.EquipmentPath is { } equipmentPath)
        {
            foreach (var equipment in (package.Equipment ?? Array.Empty<EquipmentEngineeringDto>())
                         .Where(equipment => equipment is not null && equipment.Id.HasValue &&
                                             equipment.Path.Equals(equipmentPath, StringComparison.OrdinalIgnoreCase)))
                yield return new(SecurityScopeNodeKind.Equipment, equipment!.Id!.Value, equipment.Name);
            yield break;
        }

        if (scope.ScreenKey is { } screenKey)
        {
            foreach (var screen in (package.Screens ?? Array.Empty<ScreenEngineeringDto>())
                         .Where(screen => screen is not null && screen.Id.HasValue &&
                                          screen.Key.Equals(screenKey, StringComparison.OrdinalIgnoreCase)))
                yield return new(SecurityScopeNodeKind.Screen, screen!.Id!.Value, screen.Name);
            yield break;
        }

        if (scope.CommandKey is { } commandKey)
        {
            foreach (var command in (package.Commands ?? Array.Empty<CommandEngineeringDto>())
                         .Where(command => command is not null && command.Id.HasValue &&
                                            command.Key.Equals(commandKey, StringComparison.OrdinalIgnoreCase)))
                yield return new(SecurityScopeNodeKind.Command, command!.Id!.Value, command.Name);
        }
    }

    private static Guid StableNodeId(SecurityScopeNodeKind kind, Guid resourceId)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"elitescada.authority-scope/v1:{kind}:{resourceId:D}"));
        var guid = bytes[..16];
        guid[6] = (byte)((guid[6] & 0x0f) | 0x50);
        guid[8] = (byte)((guid[8] & 0x3f) | 0x80);
        return new Guid(guid);
    }

    private sealed record LegacyResource(SecurityScopeNodeKind Kind, Guid ResourceId, string Name);
}
