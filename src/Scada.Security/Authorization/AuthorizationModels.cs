namespace Scada.Security.Authorization;

public enum AuthorizationResourceKind
{
    Tag,
    Screen,
    Command,
    Equipment
}

public sealed record SecurityPrincipal(
    string SubjectId,
    string? DisplayName,
    IReadOnlyCollection<string> Roles,
    bool IsAuthenticated = true);

public sealed record AuthorizationResource(
    string? Area = null,
    string? EquipmentPath = null,
    string? ScreenKey = null,
    string? TagPath = null,
    string? CommandKey = null,
    AuthorizationResourceKind? ResourceKind = null,
    Guid? ResourceId = null,
    Guid? ScopeNodeId = null,
    IReadOnlyCollection<Guid>? ScopeNodeAncestry = null);

public sealed record AuthorizationScope(
    string? Area = null,
    string? EquipmentPath = null,
    string? ScreenKey = null,
    string? TagPath = null,
    string? CommandKey = null,
    Guid? ScopeNodeId = null,
    bool IncludeDescendants = false)
{
    public bool Matches(AuthorizationResource resource) =>
        ScopePattern.Matches(Area, resource.Area) &&
        ScopePattern.Matches(EquipmentPath, resource.EquipmentPath) &&
        ScopePattern.Matches(ScreenKey, resource.ScreenKey) &&
        ScopePattern.Matches(TagPath, resource.TagPath) &&
        ScopePattern.Matches(CommandKey, resource.CommandKey) &&
        ScopeNodeMatches(resource);

    private bool ScopeNodeMatches(AuthorizationResource resource)
    {
        if (!ScopeNodeId.HasValue) return true;
        if (!resource.ScopeNodeId.HasValue) return false;
        if (!IncludeDescendants) return resource.ScopeNodeId == ScopeNodeId;

        return (resource.ScopeNodeAncestry ?? new[] { resource.ScopeNodeId.Value })
            .Contains(ScopeNodeId.Value);
    }
}

public sealed record CapabilityGrant(
    SecurityCapability Capability,
    AuthorizationScope? Scope = null);

public sealed record RolePolicy(
    string Key,
    string Name,
    IReadOnlyCollection<CapabilityGrant> Grants);

public sealed record AuthorizationDecision(
    bool Allowed,
    SecurityCapability Capability,
    string Reason,
    IReadOnlyCollection<string> MatchedRoles)
{
    public static AuthorizationDecision Denied(SecurityCapability capability, string reason) =>
        new(false, capability, reason, Array.Empty<string>());
}

internal static class ScopePattern
{
    public static bool Matches(string? pattern, string? value)
    {
        if (string.IsNullOrWhiteSpace(pattern)) return true;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var normalizedPattern = pattern.Trim();
        if (normalizedPattern == "*") return true;

        if (normalizedPattern.EndsWith('*'))
        {
            var prefix = normalizedPattern[..^1];
            return value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        return normalizedPattern.Equals(value, StringComparison.OrdinalIgnoreCase);
    }
}
