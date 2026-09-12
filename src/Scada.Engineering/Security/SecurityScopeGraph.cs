using Scada.Engineering.Contracts;
using Scada.Security.Authorization;

namespace Scada.Engineering.Security;

/// <summary>
/// Immutable resolver for the versioned Authority scope hierarchy. Ancestry is
/// derived only from explicit parent ids; display names, keys and paths never
/// participate in inheritance.
/// </summary>
public sealed class SecurityScopeGraph
{
    private readonly IReadOnlyDictionary<Guid, SecurityScopeEngineeringDto> _byId;
    private readonly IReadOnlyDictionary<(AuthorizationResourceKind Kind, Guid Id), Guid> _resourceNodes;

    private SecurityScopeGraph(
        IReadOnlyDictionary<Guid, SecurityScopeEngineeringDto> byId,
        IReadOnlyDictionary<(AuthorizationResourceKind Kind, Guid Id), Guid> resourceNodes)
    {
        _byId = byId;
        _resourceNodes = resourceNodes;
    }

    public static bool TryCreate(
        IReadOnlyCollection<SecurityScopeEngineeringDto>? scopes,
        out SecurityScopeGraph? graph,
        out IReadOnlyCollection<SecurityScopeValidationError> errors)
    {
        var nodes = scopes ?? Array.Empty<SecurityScopeEngineeringDto>();
        var issues = Validate(nodes).ToArray();
        if (issues.Any(x => x.Blocking))
        {
            graph = null;
            errors = issues;
            return false;
        }

        var byId = nodes.ToDictionary(node => node.Id);
        var resources = nodes
            .Where(node => TryResourceKind(node.Kind, out _) && node.ResourceId.HasValue)
            .ToDictionary(
                node => (ToResourceKind(node.Kind), node.ResourceId!.Value),
                node => node.Id);
        graph = new SecurityScopeGraph(byId, resources);
        errors = issues;
        return true;
    }

    public static IReadOnlyCollection<SecurityScopeValidationError> Validate(
        IReadOnlyCollection<SecurityScopeEngineeringDto>? scopes)
    {
        var nodes = scopes ?? Array.Empty<SecurityScopeEngineeringDto>();
        var issues = new List<SecurityScopeValidationError>();
        var ids = new HashSet<Guid>();
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var resourceBindings = new HashSet<(AuthorizationResourceKind Kind, Guid Id)>();

        foreach (var node in nodes)
        {
            var key = node.Key?.Trim() ?? string.Empty;
            if (node.Id == Guid.Empty)
                issues.Add(Error("SECURITY_SCOPE_ID_REQUIRED", "Security scope id must be a non-empty GUID.", node.Key));
            else if (!ids.Add(node.Id))
                issues.Add(Error("SECURITY_SCOPE_ID_DUPLICATE", $"Security scope id '{node.Id}' appears more than once.", node.Key));

            if (key.Length == 0)
                issues.Add(Error("SECURITY_SCOPE_KEY_REQUIRED", "Security scope key is required.", node.Key));
            else if (key.Any(char.IsWhiteSpace))
                issues.Add(Error("SECURITY_SCOPE_KEY_WHITESPACE", "Security scope key cannot contain whitespace.", key));
            else if (!keys.Add(key))
                issues.Add(Error("SECURITY_SCOPE_KEY_DUPLICATE", $"Security scope key '{key}' appears more than once.", key));

            if (string.IsNullOrWhiteSpace(node.Name))
                issues.Add(Error("SECURITY_SCOPE_NAME_REQUIRED", "Security scope display name is required.", node.Key));
            if (!Enum.IsDefined(node.Kind))
                issues.Add(Error("SECURITY_SCOPE_KIND_INVALID", $"Security scope kind '{node.Kind}' is not supported.", node.Key));

            if (TryResourceKind(node.Kind, out var kind))
            {
                if (!node.ResourceId.HasValue || node.ResourceId == Guid.Empty)
                    issues.Add(Error("SECURITY_SCOPE_RESOURCE_ID_REQUIRED", $"Security scope '{node.Key}' requires a stable {node.Kind} resource id.", node.Key));
                else if (!resourceBindings.Add((kind, node.ResourceId.Value)))
                    issues.Add(Error("SECURITY_SCOPE_RESOURCE_DUPLICATE", $"Resource '{node.ResourceId}' is bound to more than one {node.Kind} scope.", node.Key));
            }
            else if (node.ResourceId.HasValue)
            {
                issues.Add(Error("SECURITY_SCOPE_RESOURCE_ID_FORBIDDEN", $"Security scope kind '{node.Kind}' cannot bind a resource id.", node.Key));
            }
        }

        var validIds = nodes.Where(node => node.Id != Guid.Empty).Select(node => node.Id).ToHashSet();
        foreach (var node in nodes)
        {
            if (!node.ParentId.HasValue) continue;
            if (node.ParentId == node.Id)
                issues.Add(Error("SECURITY_SCOPE_PARENT_SELF", $"Security scope '{node.Key}' cannot be its own parent.", node.Key));
            else if (!validIds.Contains(node.ParentId.Value))
                issues.Add(Error("SECURITY_SCOPE_PARENT_NOT_FOUND", $"Security scope parent '{node.ParentId}' was not found.", node.Key));
        }

        var byId = nodes.Where(node => node.Id != Guid.Empty).GroupBy(node => node.Id).ToDictionary(group => group.Key, group => group.First());
        foreach (var node in byId.Values)
        {
            var visited = new HashSet<Guid>();
            var cursor = node;
            while (cursor.ParentId.HasValue && byId.TryGetValue(cursor.ParentId.Value, out var parent))
            {
                if (!visited.Add(cursor.Id) || parent.Id == node.Id)
                {
                    issues.Add(Error("SECURITY_SCOPE_CYCLE", $"Security scope '{node.Key}' participates in a parent cycle.", node.Key));
                    break;
                }

                cursor = parent;
            }
        }

        return issues;
    }

    public AuthorizationResource Enrich(AuthorizationResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        if (!resource.ResourceKind.HasValue || !resource.ResourceId.HasValue || resource.ResourceId == Guid.Empty)
            return resource;
        if (!_resourceNodes.TryGetValue((resource.ResourceKind.Value, resource.ResourceId.Value), out var scopeNodeId))
            return resource with { ScopeNodeId = null, ScopeNodeAncestry = null };

        return resource with
        {
            ScopeNodeId = scopeNodeId,
            ScopeNodeAncestry = Ancestors(scopeNodeId)
        };
    }

    public bool Contains(Guid scopeNodeId) => _byId.ContainsKey(scopeNodeId);

    public IReadOnlyCollection<Guid> Ancestors(Guid scopeNodeId)
    {
        if (!_byId.TryGetValue(scopeNodeId, out var node)) return Array.Empty<Guid>();

        var ancestry = new List<Guid>();
        var visited = new HashSet<Guid>();
        while (visited.Add(node.Id))
        {
            ancestry.Add(node.Id);
            if (!node.ParentId.HasValue || !_byId.TryGetValue(node.ParentId.Value, out node!))
                break;
        }

        return ancestry;
    }

    private static bool TryResourceKind(SecurityScopeNodeKind kind, out AuthorizationResourceKind resourceKind)
    {
        resourceKind = kind switch
        {
            SecurityScopeNodeKind.Equipment => AuthorizationResourceKind.Equipment,
            SecurityScopeNodeKind.Tag => AuthorizationResourceKind.Tag,
            SecurityScopeNodeKind.Screen => AuthorizationResourceKind.Screen,
            SecurityScopeNodeKind.Command => AuthorizationResourceKind.Command,
            _ => default
        };
        return kind is SecurityScopeNodeKind.Equipment or SecurityScopeNodeKind.Tag or
            SecurityScopeNodeKind.Screen or SecurityScopeNodeKind.Command;
    }

    private static AuthorizationResourceKind ToResourceKind(SecurityScopeNodeKind kind)
    {
        _ = TryResourceKind(kind, out var resourceKind);
        return resourceKind;
    }

    private static SecurityScopeValidationError Error(string code, string message, string? key) =>
        new(code, message, string.IsNullOrWhiteSpace(key) ? "scope" : key, true);
}

public sealed record SecurityScopeValidationError(
    string Code,
    string Message,
    string EntityKey,
    bool Blocking);
