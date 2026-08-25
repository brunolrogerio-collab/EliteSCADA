using Scada.Engineering.Contracts;

namespace Scada.Engineering.DataSources;

public interface IDataSourceEngineeringRegistry
{
    IReadOnlyCollection<DataSourceEngineeringDto> Snapshot();
    DataSourceEngineeringDto? Find(Guid id);
    DataSourceEngineeringDto? FindByKey(string key);
    void Upsert(DataSourceEngineeringDto dataSource);
}

public sealed class InMemoryDataSourceEngineeringRegistry : IDataSourceEngineeringRegistry
{
    private readonly object _sync = new();
    private readonly Dictionary<Guid, DataSourceEngineeringDto> _byId = new();
    private readonly Dictionary<string, Guid> _byKey = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<DataSourceEngineeringDto> Snapshot()
    {
        lock (_sync)
            return _byId.Values.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public DataSourceEngineeringDto? Find(Guid id)
    {
        lock (_sync)
            return _byId.GetValueOrDefault(id);
    }

    public DataSourceEngineeringDto? FindByKey(string key)
    {
        lock (_sync)
            return _byKey.TryGetValue(key, out var id) ? _byId.GetValueOrDefault(id) : null;
    }

    public void Upsert(DataSourceEngineeringDto dataSource)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataSource.Key);
        var normalized = dataSource with { Id = dataSource.Id ?? Guid.NewGuid() };
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
    }
}
