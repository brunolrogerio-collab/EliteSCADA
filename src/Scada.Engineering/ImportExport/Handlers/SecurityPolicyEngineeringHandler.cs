using Scada.Engineering.Contracts;
using Scada.Engineering.Security;

namespace Scada.Engineering.ImportExport.Handlers;

internal sealed class SecurityPolicyEngineeringHandler
{
    private readonly ISecurityPolicyEngineeringRegistry _registry;

    public SecurityPolicyEngineeringHandler(ISecurityPolicyEngineeringRegistry registry) => _registry = registry;

    public void Preview(EngineeringPackage package, ImportMode mode, List<ImportPreviewItem> items)
    {
        var roles = package.SecurityRoles ?? Array.Empty<SecurityRoleEngineeringDto>();
        var duplicates = EngineeringHandlerSupport.Duplicates(roles.Select(x => x.Key));

        foreach (var role in roles)
        {
            var issues = SecurityPolicyEngineeringValidator.Validate(role).ToList();
            if (duplicates.Contains(role.Key))
            {
                issues.Add(new ImportIssue(
                    "SECURITY_ROLE_DUPLICATE_IN_FILE",
                    $"Security role key '{role.Key}' appears more than once in the import package.",
                    ImportEntityKind.SecurityRole,
                    role.Key,
                    true));
            }

            EngineeringHandlerSupport.AddPreview(
                items,
                ImportEntityKind.SecurityRole,
                role.Key,
                ResolveExisting(role) is not null,
                mode,
                issues);
        }
    }

    public void Apply(EngineeringPackage package, ImportMode mode, ref int created, ref int updated, ref int skipped)
    {
        foreach (var role in package.SecurityRoles ?? Array.Empty<SecurityRoleEngineeringDto>())
        {
            var existing = ResolveExisting(role);
            var operation = EngineeringHandlerSupport.Decide(existing is not null, mode);
            if (operation == ImportOperation.Skip)
            {
                skipped++;
                continue;
            }

            _registry.UpsertRole(role with { Id = existing?.Id ?? role.Id ?? Guid.NewGuid() });
            if (existing is null) created++; else updated++;
        }
    }

    private SecurityRoleEngineeringDto? ResolveExisting(SecurityRoleEngineeringDto role)
    {
        if (role.Id.HasValue)
        {
            var byId = _registry.FindRole(role.Id.Value);
            if (byId is not null) return byId;
        }

        return _registry.FindRoleByKey(role.Key);
    }
}
