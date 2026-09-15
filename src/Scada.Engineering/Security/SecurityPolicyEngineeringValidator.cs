using Scada.Engineering.Contracts;

namespace Scada.Engineering.Security;

public static class SecurityPolicyEngineeringValidator
{
    private static readonly string[] SensitiveMetadataFragments =
    {
        "password", "passwordhash", "passwd", "secret", "token", "credential", "privatekey", "apikey"
    };

    public static IReadOnlyCollection<ImportIssue> Validate(
        SecurityRoleEngineeringDto role,
        SecurityScopeGraph? scopeGraph = null,
        bool requiresStableScopeNode = false)
    {
        var issues = new List<ImportIssue>();
        var key = string.IsNullOrWhiteSpace(role.Key) ? role.Name : role.Key;

        if (string.IsNullOrWhiteSpace(role.Key))
            issues.Add(Error("SECURITY_ROLE_KEY_REQUIRED", "Security role key is required.", key));
        if (role.Key?.Any(char.IsWhiteSpace) == true)
            issues.Add(Error("SECURITY_ROLE_KEY_WHITESPACE", "Security role key cannot contain whitespace.", key));
        if (string.IsNullOrWhiteSpace(role.Name))
            issues.Add(Error("SECURITY_ROLE_NAME_REQUIRED", "Security role name is required.", key));

        ValidateMetadata(role.Metadata, key, "role", issues);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var grant in role.Grants ?? Array.Empty<CapabilityGrantEngineeringDto>())
        {
            var scopeKey = ScopeKey(grant.Scope);
            var grantKey = $"{grant.Capability}:{scopeKey}";
            if (!seen.Add(grantKey))
                issues.Add(Error(
                    "SECURITY_GRANT_DUPLICATE",
                    $"Capability '{grant.Capability}' contains the same scope more than once.",
                    key));

            ValidateScope(grant.Scope, key, issues, scopeGraph, requiresStableScopeNode);
            ValidateMetadata(grant.Metadata, key, $"grant {grant.Capability}", issues);
        }

        return issues;
    }

    private static void ValidateScope(
        AuthorizationScopeEngineeringDto? scope,
        string roleKey,
        List<ImportIssue> issues,
        SecurityScopeGraph? scopeGraph,
        bool requiresStableScopeNode)
    {
        if (scope is null) return;

        ValidateScopeField(scope.Area, "area", roleKey, issues);
        ValidateScopeField(scope.EquipmentPath, "equipmentPath", roleKey, issues);
        ValidateScopeField(scope.ScreenKey, "screenKey", roleKey, issues);
        ValidateScopeField(scope.TagPath, "tagPath", roleKey, issues);
        ValidateScopeField(scope.CommandKey, "commandKey", roleKey, issues);
        if (AuthorityScopeEngineeringMigration.HasUnmigratedLegacyText(scope))
        {
            issues.Add(Error(
                "SECURITY_LEGACY_SCOPE_MIGRATION_BLOCKED",
                "Legacy text scope could not be resolved to exactly one stable Authority scope node. Wildcards, areas and ambiguous references must be migrated explicitly.",
                roleKey));
        }
        if (requiresStableScopeNode && (!scope.ScopeNodeId.HasValue || scope.ScopeNodeId == Guid.Empty))
        {
            issues.Add(Error(
                "SECURITY_SCOPE_NODE_REQUIRED",
                "A schema-18 security scope must reference a non-empty stable scope node id. Use a null scope for an unscoped grant.",
                roleKey));
        }
        if (scope.ScopeNodeId.HasValue &&
            (scope.ScopeNodeId == Guid.Empty || scopeGraph is null || !scopeGraph.Contains(scope.ScopeNodeId.Value)))
        {
            issues.Add(Error(
                "SECURITY_SCOPE_NODE_NOT_FOUND",
                $"Security grant scope node '{scope.ScopeNodeId}' was not found in the canonical hierarchy.",
                roleKey));
        }
    }

    private static void ValidateScopeField(
        string? value,
        string field,
        string roleKey,
        List<ImportIssue> issues)
    {
        if (value is not null && string.IsNullOrWhiteSpace(value))
            issues.Add(Error(
                "SECURITY_SCOPE_EMPTY",
                $"Security grant scope field '{field}' cannot be empty or whitespace. Use null to leave the dimension unrestricted.",
                roleKey));
    }

    private static void ValidateMetadata(
        IReadOnlyDictionary<string, string>? metadata,
        string roleKey,
        string context,
        List<ImportIssue> issues)
    {
        foreach (var entry in metadata ?? new Dictionary<string, string>())
        {
            var normalized = new string(entry.Key
                .Where(char.IsLetterOrDigit)
                .Select(char.ToLowerInvariant)
                .ToArray());

            if (SensitiveMetadataFragments.Any(fragment => normalized.Contains(fragment, StringComparison.Ordinal)))
            {
                issues.Add(Error(
                    "SECURITY_SECRET_METADATA_FORBIDDEN",
                    $"Security {context} metadata key '{entry.Key}' appears to describe authentication secret material and cannot be stored in engineering.",
                    roleKey));
            }
        }
    }

    private static string ScopeKey(AuthorizationScopeEngineeringDto? scope) => scope is null
        ? "*"
        : string.Join('|', new[]
        {
            scope.Area ?? string.Empty,
            scope.EquipmentPath ?? string.Empty,
            scope.ScreenKey ?? string.Empty,
            scope.TagPath ?? string.Empty,
            scope.CommandKey ?? string.Empty,
            scope.ScopeNodeId?.ToString("D") ?? string.Empty,
            scope.IncludeDescendants.ToString()
        });

    private static ImportIssue Error(string code, string message, string key) =>
        new(code, message, ImportEntityKind.SecurityRole, key, true);
}
