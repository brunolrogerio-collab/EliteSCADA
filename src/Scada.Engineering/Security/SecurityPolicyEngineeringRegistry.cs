using Scada.Engineering.Contracts;

namespace Scada.Engineering.Security;

public interface ISecurityPolicyEngineeringRegistry
{
    IReadOnlyCollection<SecurityRoleEngineeringDto> SnapshotRoles();
    IReadOnlyCollection<SecurityScopeEngineeringDto> SnapshotScopes();
    SecurityRoleEngineeringDto? FindRole(Guid id);
    SecurityRoleEngineeringDto? FindRoleByKey(string key);
    SecurityScopeEngineeringDto? FindScope(Guid id);
    SecurityScopeEngineeringDto? FindScopeByKey(string key);
    void UpsertRole(SecurityRoleEngineeringDto role);
    void UpsertScope(SecurityScopeEngineeringDto scope);
}

public sealed class InMemorySecurityPolicyEngineeringRegistry : ISecurityPolicyEngineeringRegistry
{
    private readonly object _sync = new();
    private readonly Dictionary<Guid, SecurityRoleEngineeringDto> _byId = new();
    private readonly Dictionary<string, Guid> _byKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<Guid, SecurityScopeEngineeringDto> _scopesById = new();
    private readonly Dictionary<string, Guid> _scopeIdsByKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly Action? _changed;

    public InMemorySecurityPolicyEngineeringRegistry(Action? changed = null)
    {
        _changed = changed;
    }

    public IReadOnlyCollection<SecurityRoleEngineeringDto> SnapshotRoles()
    {
        lock (_sync)
            return _byId.Values.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public IReadOnlyCollection<SecurityScopeEngineeringDto> SnapshotScopes()
    {
        lock (_sync)
            return _scopesById.Values.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public SecurityRoleEngineeringDto? FindRole(Guid id)
    {
        lock (_sync)
            return _byId.GetValueOrDefault(id);
    }

    public SecurityRoleEngineeringDto? FindRoleByKey(string key)
    {
        lock (_sync)
            return _byKey.TryGetValue(key, out var id) ? _byId.GetValueOrDefault(id) : null;
    }

    public SecurityScopeEngineeringDto? FindScope(Guid id)
    {
        lock (_sync)
            return _scopesById.GetValueOrDefault(id);
    }

    public SecurityScopeEngineeringDto? FindScopeByKey(string key)
    {
        lock (_sync)
            return _scopeIdsByKey.TryGetValue(key, out var id) ? _scopesById.GetValueOrDefault(id) : null;
    }

    public void UpsertRole(SecurityRoleEngineeringDto role)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(role.Key);
        var normalized = role with { Id = role.Id ?? Guid.NewGuid() };
        var id = normalized.Id!.Value;

        lock (_sync)
        {
            if (_byId.TryGetValue(id, out var previous) && !previous.Key.Equals(normalized.Key, StringComparison.OrdinalIgnoreCase))
                _byKey.Remove(previous.Key);

            if (_byKey.TryGetValue(normalized.Key, out var otherId) && otherId != id)
                _byId.Remove(otherId);

            _byId[id] = normalized;
            _byKey[normalized.Key] = id;
        }

        _changed?.Invoke();
    }

    public void UpsertScope(SecurityScopeEngineeringDto scope)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope.Key);
        if (scope.Id == Guid.Empty)
            throw new ArgumentException("Security scope id is required.", nameof(scope));

        lock (_sync)
        {
            if (_scopesById.TryGetValue(scope.Id, out var previous) &&
                !previous.Key.Equals(scope.Key, StringComparison.OrdinalIgnoreCase))
            {
                _scopeIdsByKey.Remove(previous.Key);
            }

            if (_scopeIdsByKey.TryGetValue(scope.Key, out var otherId) && otherId != scope.Id)
                _scopesById.Remove(otherId);

            _scopesById[scope.Id] = scope;
            _scopeIdsByKey[scope.Key] = scope.Id;
        }

        _changed?.Invoke();
    }

    public void Clear()
    {
        lock (_sync)
        {
            _byId.Clear();
            _byKey.Clear();
            _scopesById.Clear();
            _scopeIdsByKey.Clear();
        }

        _changed?.Invoke();
    }
}
