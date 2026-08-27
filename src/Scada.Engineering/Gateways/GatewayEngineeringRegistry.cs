using Scada.Engineering.Contracts;

namespace Scada.Engineering.Gateways;

public interface IGatewayEngineeringRegistry
{
    IReadOnlyCollection<GatewayRouteEngineeringDto> Snapshot();
    GatewayRouteEngineeringDto? Find(Guid id);
    GatewayRouteEngineeringDto? FindByKey(string key);
    void Upsert(GatewayRouteEngineeringDto route);
}

public sealed class InMemoryGatewayEngineeringRegistry : IGatewayEngineeringRegistry
{
    private readonly object _sync = new();
    private readonly Dictionary<Guid, GatewayRouteEngineeringDto> _byId = new();
    private readonly Dictionary<string, Guid> _byKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly Action? _changed;

    public InMemoryGatewayEngineeringRegistry(Action? changed = null)
    {
        _changed = changed;
    }

    public IReadOnlyCollection<GatewayRouteEngineeringDto> Snapshot()
    {
        lock (_sync)
            return _byId.Values.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public GatewayRouteEngineeringDto? Find(Guid id)
    {
        lock (_sync)
            return _byId.GetValueOrDefault(id);
    }

    public GatewayRouteEngineeringDto? FindByKey(string key)
    {
        lock (_sync)
            return _byKey.TryGetValue(key, out var id) ? _byId.GetValueOrDefault(id) : null;
    }

    public void Upsert(GatewayRouteEngineeringDto route)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(route.Key);
        var normalized = route with { Id = route.Id ?? Guid.NewGuid() };
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

    public bool Remove(Guid id)
    {
        GatewayRouteEngineeringDto? removed;
        lock (_sync)
        {
            if (!_byId.Remove(id, out removed)) return false;
            _byKey.Remove(removed.Key);
        }

        _changed?.Invoke();
        return true;
    }

    public void Clear()
    {
        lock (_sync)
        {
            _byId.Clear();
            _byKey.Clear();
        }

        _changed?.Invoke();
    }
}