using Scada.Engineering.Contracts;

namespace Scada.Engineering.Events;

public interface IOperationalEventEngineeringRegistry
{
    IReadOnlyCollection<OperationalEventEngineeringDto> SnapshotOperationalEvents();
    OperationalEventEngineeringDto? FindOperationalEvent(Guid id);
    OperationalEventEngineeringDto? FindOperationalEventByKey(string key);
    void UpsertOperationalEvent(OperationalEventEngineeringDto definition);
    void ClearOperationalEvents();
}

public sealed class InMemoryOperationalEventEngineeringRegistry : IOperationalEventEngineeringRegistry
{
    private readonly object _sync = new();
    private readonly Dictionary<Guid, OperationalEventEngineeringDto> _byId = new();
    private readonly Dictionary<string, Guid> _byKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly Action? _changed;

    public InMemoryOperationalEventEngineeringRegistry(Action? changed = null)
    {
        _changed = changed;
    }

    public IReadOnlyCollection<OperationalEventEngineeringDto> SnapshotOperationalEvents()
    {
        lock (_sync)
            return _byId.Values
                .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Id)
                .ToArray();
    }

    public OperationalEventEngineeringDto? FindOperationalEvent(Guid id)
    {
        lock (_sync)
            return _byId.GetValueOrDefault(id);
    }

    public OperationalEventEngineeringDto? FindOperationalEventByKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        lock (_sync)
            return _byKey.TryGetValue(key, out var id) ? _byId.GetValueOrDefault(id) : null;
    }

    public void UpsertOperationalEvent(OperationalEventEngineeringDto definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (string.IsNullOrWhiteSpace(definition.Key))
            throw new ArgumentException("Operational Event key is required.", nameof(definition));

        var normalized = definition with { Id = definition.Id ?? Guid.NewGuid() };
        if (normalized.Id == Guid.Empty)
            throw new ArgumentException("Operational Event stable ID cannot be empty.", nameof(definition));

        lock (_sync)
        {
            var id = normalized.Id!.Value;
            if (_byId.TryGetValue(id, out var previous) &&
                !previous.Key.Equals(normalized.Key, StringComparison.OrdinalIgnoreCase))
                _byKey.Remove(previous.Key);

            if (_byKey.TryGetValue(normalized.Key, out var otherId) && otherId != id)
                _byId.Remove(otherId);

            _byId[id] = normalized;
            _byKey[normalized.Key] = id;
        }

        _changed?.Invoke();
    }

    public void ClearOperationalEvents()
    {
        lock (_sync)
        {
            _byId.Clear();
            _byKey.Clear();
        }
        _changed?.Invoke();
    }
}