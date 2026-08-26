using Scada.Engineering.Contracts;

namespace Scada.Engineering.Commands;

public interface ICommandEngineeringRegistry
{
    IReadOnlyCollection<CommandEngineeringDto> Snapshot();
    CommandEngineeringDto? Find(Guid id);
    CommandEngineeringDto? FindByKey(string key);
    void Upsert(CommandEngineeringDto command);
}

public sealed class InMemoryCommandEngineeringRegistry : ICommandEngineeringRegistry
{
    private readonly object _sync = new();
    private readonly Dictionary<Guid, CommandEngineeringDto> _byId = new();
    private readonly Dictionary<string, Guid> _byKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly Action? _changed;

    public InMemoryCommandEngineeringRegistry(Action? changed = null)
    {
        _changed = changed;
    }

    public IReadOnlyCollection<CommandEngineeringDto> Snapshot()
    {
        lock (_sync)
            return _byId.Values.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public CommandEngineeringDto? Find(Guid id)
    {
        lock (_sync)
            return _byId.GetValueOrDefault(id);
    }

    public CommandEngineeringDto? FindByKey(string key)
    {
        lock (_sync)
            return _byKey.TryGetValue(key, out var id) ? _byId.GetValueOrDefault(id) : null;
    }

    public void Upsert(CommandEngineeringDto command)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command.Key);
        var normalized = command with { Id = command.Id ?? Guid.NewGuid() };
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
