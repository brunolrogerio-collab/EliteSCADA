using Scada.Engineering.Contracts;
using Scada.Engineering.Assets;
using Scada.Engineering.Commands;
using Scada.Engineering.Security;
using Scada.Engineering.Views;
using Scada.Core.Tags;

namespace Scada.Engineering.ImportExport.Handlers;

internal sealed class SecurityScopeEngineeringHandler(
    ISecurityPolicyEngineeringRegistry registry,
    ITagRegistry tags,
    IEngineeringAssetRegistry assets,
    IEngineeringViewRegistry views,
    ICommandEngineeringRegistry commands)
{
    public void Preview(EngineeringPackage package, ImportMode mode, List<ImportPreviewItem> items)
    {
        var scopes = package.SecurityScopes ?? Array.Empty<SecurityScopeEngineeringDto>();
        var errorsByKey = SecurityScopeGraph.Validate(EffectiveScopes(package))
            .Concat(ValidateResourceBindings(package, scopes))
            .GroupBy(error => error.EntityKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);

        foreach (var scope in scopes)
        {
            var issues = errorsByKey.GetValueOrDefault(scope.Key, Array.Empty<SecurityScopeValidationError>())
                .Select(error => new ImportIssue(
                    error.Code,
                    error.Message,
                    ImportEntityKind.SecurityScope,
                    scope.Key,
                    error.Blocking))
                .ToList();

            EngineeringHandlerSupport.AddPreview(
                items,
                ImportEntityKind.SecurityScope,
                scope.Key,
                registry.FindScope(scope.Id) is not null || registry.FindScopeByKey(scope.Key) is not null,
                mode,
                issues);
        }
    }

    public IReadOnlyCollection<SecurityScopeEngineeringDto> EffectiveScopes(EngineeringPackage package)
    {
        var effective = registry.SnapshotScopes().ToDictionary(scope => scope.Id);
        foreach (var incoming in package.SecurityScopes ?? Array.Empty<SecurityScopeEngineeringDto>())
        {
            var sameKey = effective.Values.FirstOrDefault(scope =>
                scope.Key.Equals(incoming.Key, StringComparison.OrdinalIgnoreCase));
            if (sameKey is not null && sameKey.Id != incoming.Id)
                effective.Remove(sameKey.Id);
            effective[incoming.Id] = incoming;
        }

        return effective.Values.ToArray();
    }

    public void Apply(EngineeringPackage package, ImportMode mode, ref int created, ref int updated, ref int skipped)
    {
        foreach (var scope in package.SecurityScopes ?? Array.Empty<SecurityScopeEngineeringDto>())
        {
            var existing = registry.FindScope(scope.Id) ?? registry.FindScopeByKey(scope.Key);
            var operation = EngineeringHandlerSupport.Decide(existing is not null, mode);
            if (operation == ImportOperation.Skip)
            {
                skipped++;
                continue;
            }

            registry.UpsertScope(scope);
            if (existing is null) created++; else updated++;
        }
    }

    private IEnumerable<SecurityScopeValidationError> ValidateResourceBindings(
        EngineeringPackage package,
        IReadOnlyCollection<SecurityScopeEngineeringDto> scopes)
    {
        var tagIds = tags.Snapshot().Select(tag => tag.Id)
            .Concat((package.Tags ?? Array.Empty<TagEngineeringDto>())
                .Where(tag => tag is not null && tag.Id.HasValue)
                .Select(tag => tag!.Id!.Value))
            .ToHashSet();
        var equipmentIds = assets.SnapshotEquipment().Where(item => item.Id.HasValue).Select(item => item.Id!.Value)
            .Concat((package.Equipment ?? Array.Empty<EquipmentEngineeringDto>())
                .Where(item => item is not null && item.Id.HasValue)
                .Select(item => item!.Id!.Value))
            .ToHashSet();
        var screenIds = views.SnapshotScreens().Where(item => item.Id.HasValue).Select(item => item.Id!.Value)
            .Concat((package.Screens ?? Array.Empty<ScreenEngineeringDto>())
                .Where(item => item is not null && item.Id.HasValue)
                .Select(item => item!.Id!.Value))
            .ToHashSet();
        var commandIds = commands.Snapshot().Where(item => item.Id.HasValue).Select(item => item.Id!.Value)
            .Concat((package.Commands ?? Array.Empty<CommandEngineeringDto>())
                .Where(item => item is not null && item.Id.HasValue)
                .Select(item => item!.Id!.Value))
            .ToHashSet();

        foreach (var scope in scopes)
        {
            if (!scope.ResourceId.HasValue || scope.ResourceId == Guid.Empty) continue;
            var found = scope.Kind switch
            {
                SecurityScopeNodeKind.Tag => tagIds.Contains(scope.ResourceId.Value),
                SecurityScopeNodeKind.Equipment => equipmentIds.Contains(scope.ResourceId.Value),
                SecurityScopeNodeKind.Screen => screenIds.Contains(scope.ResourceId.Value),
                SecurityScopeNodeKind.Command => commandIds.Contains(scope.ResourceId.Value),
                _ => true
            };
            if (!found)
            {
                yield return new SecurityScopeValidationError(
                    "SECURITY_SCOPE_RESOURCE_NOT_FOUND",
                    $"Security scope '{scope.Key}' references missing {scope.Kind} resource id '{scope.ResourceId}'.",
                    scope.Key,
                    true);
            }
        }
    }
}
